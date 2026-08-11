import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";
import { PreviewPanel } from "./panel";
import { PreviewSession, SessionOptions } from "./session";
import { FrameHeader, PreviewTargetInfo, StatusInfo } from "./protocol";

let session: PreviewSession | undefined;
let panel: PreviewPanel | undefined;
let outputChannel: vscode.OutputChannel;
let sessionSubscriptions: vscode.Disposable[] = [];
let lastTargets: PreviewTargetInfo[] = [];
let activeTargetId = "";
// A manual dropdown pick wins over auto-match until the user moves to another editor file.
let manualOverride = false;
// Sessions kept warm after their panel closed, so reopening the preview reattaches instantly
// instead of paying the full build-and-launch cost again.
const keptSessions = new Map<string, { session: PreviewSession; timer: NodeJS.Timeout }>();

export function activate(context: vscode.ExtensionContext): void {
    outputChannel = vscode.window.createOutputChannel("MewUI Preview");
    context.subscriptions.push(
        outputChannel,
        vscode.commands.registerCommand("mewui.preview.start", startPreview),
        vscode.commands.registerCommand("mewui.preview.stop", stopPreview),
        vscode.commands.registerCommand("mewui.preview.refreshTarget", () => session?.refreshTarget()),
        vscode.commands.registerCommand("mewui.preview.restart", () => session?.restartProcess()),
    );
}

export function deactivate(): void {
    stopPreview();
}

async function startPreview(): Promise<void> {
    if (!vscode.workspace.isTrusted) {
        void vscode.window.showWarningMessage("MewUI Preview requires a trusted workspace (it builds and runs project code).");
        return;
    }

    const projectPath = await resolveExecutableProject();
    if (!projectPath) {
        return;
    }

    detachPanel();

    const configuration = vscode.workspace.getConfiguration("mewui.preview");
    const options: SessionOptions = {
        reloadDriver: configuration.get<"auto" | "watch" | "buildRestart">("reloadDriver", "auto"),
        sessionStartTimeoutMs: configuration.get<number>("sessionStartTimeoutSeconds", 60) * 1000,
    };
    const autoSelectTarget = configuration.get<boolean>("autoSelectTarget", true);

    const newPanel = new PreviewPanel(
        `MewUI Preview: ${path.basename(projectPath, ".csproj")}`,
        configuration.get<boolean>("openSourceOnSelect", true),
        {
        onSelectTarget: (id) => {
            activeTargetId = id;
            manualOverride = true;
            session?.selectTarget(id);
            // Reverse sync (opt-in): picking a target jumps the editor to its declaration.
            if (vscode.workspace.getConfiguration("mewui.preview").get<boolean>("openSourceOnSelect", true)) {
                const target = lastTargets.find((candidate) => candidate.id === id);
                if (target?.sourcePath) {
                    void revealSource(target.sourcePath, target.sourceLine ?? undefined);
                }
            }
        },
        onRefresh: () => session?.refreshTarget(),
        onRestart: () => session?.restartProcess(),
        onRendered: (seq) => session?.ackFrame(seq),
        onViewport: debounce((width: number, height: number, dpi: number) => {
            session?.setViewport(width, height, dpi);
        }, 250),
        onSetTheme: (mode) => session?.setTheme(mode as "light" | "dark" | "system"),
        onInput: (kind, body) => session?.sendInput(kind, body),
        onSetNavigate: (value) => {
            void vscode.workspace.getConfiguration("mewui.preview")
                .update("openSourceOnSelect", value, vscode.ConfigurationTarget.Global);
        },
        onDisposed: () => {
            panel = undefined;
            detachPanel();
        },
    });
    panel = newPanel;

    const callbacks = {
        onLog: (line: string) => outputChannel.appendLine(`${timestamp()} ${line}`),
        onState: (state: string, detail?: string) => {
            outputChannel.appendLine(`${timestamp()} [session] ${state}${detail ? `: ${detail}` : ""}`);
            newPanel.showSessionState(state, detail);
        },
        onTargets: (targets: PreviewTargetInfo[], activeId: string) => {
            lastTargets = targets;
            activeTargetId = activeId;
            newPanel.showTargets(targets, activeId);
            if (autoSelectTarget) {
                autoMatchTarget(vscode.window.activeTextEditor);
            }
        },
        onFrame: (header: FrameHeader, pixels: Buffer) => newPanel.showFrame(header, pixels),
        onStatus: (status: StatusInfo) => newPanel.showStatus(status),
    };

    const kept = keptSessions.get(projectPath);
    let activeSession: PreviewSession;
    if (kept !== undefined && !kept.session.isStopped) {
        clearTimeout(kept.timer);
        keptSessions.delete(projectPath);
        activeSession = kept.session;
        session = activeSession;
        outputChannel.appendLine(`${timestamp()} [session] reattached to the kept-alive session`);
        activeSession.rebind(callbacks);
    } else {
        activeSession = new PreviewSession(projectPath, callbacks, options);
        session = activeSession;
    }

    // The buildRestart driver relies on IDE save events (watch owns detection otherwise, and
    // notifySourceChanged is a no-op there). Auto-match follows the active editor (plan.md 4.5).
    sessionSubscriptions.push(
        vscode.workspace.onDidSaveTextDocument((document) => activeSession.notifySourceChanged(document.uri.fsPath)),
    );
    if (autoSelectTarget) {
        sessionSubscriptions.push(
            vscode.window.onDidChangeActiveTextEditor((editor) => autoMatchTarget(editor, true)),
        );
    }

    if (kept === undefined || kept.session.isStopped) {
        try {
            await activeSession.start();
        } catch (error) {
            void vscode.window.showErrorMessage(`MewUI Preview failed to start: ${error}`);
            stopPreview();
        }
    }
}

/**
 * Selects the preview target declared in the active editor's file (server-provided sourcePath).
 * Editor-driven calls (the user moved to another file) override a manual dropdown pick;
 * refresh-driven calls must not, or a targets rebroadcast would silently revert the pick
 * while its file is open.
 */
function autoMatchTarget(editor: vscode.TextEditor | undefined, fromEditor = false): void {
    const fsPath = editor?.document.uri.fsPath;
    if (!session || !fsPath || !fsPath.toLowerCase().endsWith(".cs")) {
        return;
    }
    if (!fromEditor && manualOverride) {
        return;
    }

    const match = lastTargets.find(
        (target) => target.available !== false && target.sourcePath && samePath(target.sourcePath, fsPath),
    );
    if (match && match.id !== activeTargetId) {
        outputChannel.appendLine(`${timestamp()} [session] auto-selecting ${match.id} for ${path.basename(fsPath)}`);
        activeTargetId = match.id;
        manualOverride = false;
        session.selectTarget(match.id);
    }
}

/** Opens the target's declaring source in the editor (reverse of auto-match). */
async function revealSource(sourcePath: string, sourceLine: number | undefined): Promise<void> {
    try {
        const document = await vscode.workspace.openTextDocument(sourcePath);
        const line = Math.max(0, (sourceLine ?? 1) - 1);
        const selection = new vscode.Range(line, 0, line, 0);
        await vscode.window.showTextDocument(document, { preview: true, preserveFocus: true, selection });
    } catch {
        // The scanned path may no longer exist (renamed file); the selection itself still applies.
    }
}

function samePath(left: string, right: string): boolean {
    const normalizedLeft = path.normalize(left);
    const normalizedRight = path.normalize(right);
    if (process.platform === "win32" || process.platform === "darwin") {
        return normalizedLeft.toLowerCase() === normalizedRight.toLowerCase();
    }
    return normalizedLeft === normalizedRight;
}

/**
 * Closes the panel and either keeps the session warm for reattachment (per the keep-alive
 * setting) or stops it.
 */
function detachPanel(): void {
    for (const subscription of sessionSubscriptions) {
        subscription.dispose();
    }
    sessionSubscriptions = [];

    const currentSession = session;
    session = undefined;
    if (currentSession !== undefined && !currentSession.isStopped) {
        const keepMinutes = vscode.workspace.getConfiguration("mewui.preview").get<number>("keepSessionMinutes", 10);
        if (keepMinutes > 0) {
            outputChannel.appendLine(`${timestamp()} [session] keeping the session warm for ${keepMinutes} minute(s)`);
            keptSessions.set(currentSession.projectPath, {
                session: currentSession,
                timer: setTimeout(() => {
                    keptSessions.delete(currentSession.projectPath);
                    currentSession.stop();
                    outputChannel.appendLine(`${timestamp()} [session] kept-alive session expired`);
                }, keepMinutes * 60_000),
            });
        } else {
            currentSession.stop();
        }
    }

    const currentPanel = panel;
    panel = undefined;
    currentPanel?.dispose();
}

/** Stops everything: the active session, all kept-alive sessions, and the panel. */
function stopPreview(): void {
    for (const subscription of sessionSubscriptions) {
        subscription.dispose();
    }
    sessionSubscriptions = [];
    lastTargets = [];
    activeTargetId = "";
    manualOverride = false;
    for (const kept of keptSessions.values()) {
        clearTimeout(kept.timer);
        kept.session.stop();
    }
    keptSessions.clear();
    session?.stop();
    session = undefined;
    const currentPanel = panel;
    panel = undefined;
    currentPanel?.dispose();
}

/** Resolves the executable project for the active document (plan.md 4.5.1/4.7). */
async function resolveExecutableProject(): Promise<string | undefined> {
    const activePath = vscode.window.activeTextEditor?.document.uri.fsPath;
    const nearest = activePath ? findNearestProject(activePath) : undefined;
    if (nearest && isExecutableProject(nearest)) {
        return nearest;
    }

    // Library (or no) context: offer the workspace's executable projects.
    const projectUris = await vscode.workspace.findFiles("**/*.csproj", "**/{bin,obj,node_modules}/**");
    const executables = projectUris.map((uri) => uri.fsPath).filter(isExecutableProject).sort();
    if (executables.length === 0) {
        void vscode.window.showWarningMessage("No executable (.csproj with OutputType Exe/WinExe) project found in the workspace.");
        return undefined;
    }
    if (executables.length === 1) {
        return executables[0];
    }

    const picked = await vscode.window.showQuickPick(
        executables.map((projectPath) => ({
            label: path.basename(projectPath, ".csproj"),
            description: vscode.workspace.asRelativePath(projectPath),
            projectPath,
        })),
        { placeHolder: "Select the executable project to run the preview session in" },
    );
    return picked?.projectPath;
}

function findNearestProject(filePath: string): string | undefined {
    let directory = path.dirname(filePath);
    const root = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(filePath))?.uri.fsPath;

    for (;;) {
        const projects = fs.readdirSync(directory).filter((entry) => entry.toLowerCase().endsWith(".csproj"));
        if (projects.length > 0) {
            return path.join(directory, projects.sort()[0]);
        }
        if (root !== undefined && path.relative(root, directory) === "") {
            return undefined;
        }
        const parent = path.dirname(directory);
        if (parent === directory) {
            return undefined;
        }
        directory = parent;
    }
}

function isExecutableProject(projectPath: string): boolean {
    try {
        const text = fs.readFileSync(projectPath, "utf8");
        return /<OutputType>\s*(WinExe|Exe)\s*<\/OutputType>/i.test(text);
    } catch {
        return false;
    }
}

function timestamp(): string {
    return `[${new Date().toISOString().slice(11, 23)}]`;
}

function debounce<TArgs extends unknown[]>(action: (...args: TArgs) => void, delayMs: number): (...args: TArgs) => void {
    let timer: NodeJS.Timeout | undefined;
    return (...args: TArgs) => {
        if (timer !== undefined) {
            clearTimeout(timer);
        }
        timer = setTimeout(() => action(...args), delayMs);
    };
}
