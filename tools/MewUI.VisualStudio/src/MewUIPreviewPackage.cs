using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Task = System.Threading.Tasks.Task;

namespace Aprillz.MewUI.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("MewUI Preview", "Live editor preview for MewUI windows and user controls.", "0.1")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(PreviewToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)]
    [Guid(PACKAGE_GUID)]
    public sealed class MewUIPreviewPackage : AsyncPackage
    {
        public const string PACKAGE_GUID = "6b4084d0-1b60-49a8-9255-07401588f550";
        public const string COMMAND_SET_GUID = "19af801b-62f4-4b49-a83b-023df968a6a6";
        public const int CMDID_PREVIEW_WINDOW = 0x0100;
        public const int CMDID_OPEN_PREVIEW_CONTEXT = 0x0101;
        private const string SETTINGS_COLLECTION = "MewUI.Preview";
        private const string NAVIGATE_PROPERTY = "NavigateOnSelect";

        private DTE2 _dte;
        // COM event sources are advised through these wrappers; dropping the references
        // silently disconnects the sinks, so they live for the package lifetime.
        private DocumentEvents _documentEvents;
        private WindowEvents _windowEvents;
        private IVsOutputWindowPane _outputPane;
        private PreviewToolWindowControl _control;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            commandService?.AddCommand(new MenuCommand(
                (_, __) => ShowPreviewWindow(),
                new CommandID(new Guid(COMMAND_SET_GUID), CMDID_PREVIEW_WINDOW)));
            commandService?.AddCommand(new MenuCommand(
                (_, __) => ShowPreviewWindow(),
                new CommandID(new Guid(COMMAND_SET_GUID), CMDID_OPEN_PREVIEW_CONTEXT)));
        }

        private void ShowPreviewWindow()
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                ToolWindowPane window = await ShowToolWindowAsync(typeof(PreviewToolWindow), 0, create: true, DisposalToken);
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
                await WireControlAsync((PreviewToolWindowControl)window.Content);
            }).FileAndForget("mewui/preview/showwindow");
        }

        private async Task WireControlAsync(PreviewToolWindowControl control)
        {
            if (_control == control)
            {
                return;
            }
            _control = control;

            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            _dte = (DTE2)await GetServiceAsync(typeof(SDTE));
            var outputWindow = (IVsOutputWindow)await GetServiceAsync(typeof(SVsOutputWindow));
            if (_dte == null || outputWindow == null)
            {
                return;
            }

            var paneGuid = new Guid(PACKAGE_GUID);
            outputWindow.CreatePane(ref paneGuid, "MewUI Preview", fInitVisible: 1, fClearWithSolution: 1);
            outputWindow.GetPane(ref paneGuid, out _outputPane);

            control.LogSink = line =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _outputPane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss.fff}] {line}\n");
            };
            control.ActiveDocumentPathProvider = () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    return _dte?.ActiveDocument?.FullName;
                }
                catch (Exception)
                {
                    return null;
                }
            };
            control.SolutionDirectoryProvider = () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string solutionPath = _dte?.Solution?.FullName;
                return string.IsNullOrEmpty(solutionPath) ? null : Path.GetDirectoryName(solutionPath);
            };
            var settingsStore = new ShellSettingsManager(this).GetWritableSettingsStore(SettingsScope.UserSettings);
            if (!settingsStore.CollectionExists(SETTINGS_COLLECTION))
            {
                settingsStore.CreateCollection(SETTINGS_COLLECTION);
            }
            control.SaveNavigatePreference = value =>
                settingsStore.SetBoolean(SETTINGS_COLLECTION, NAVIGATE_PROPERTY, value);
            control.InitializeNavigatePreference(
                settingsStore.GetBoolean(SETTINGS_COLLECTION, NAVIGATE_PROPERTY, defaultValue: true));

            control.NavigateToSource = (sourcePath, sourceLine) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    _dte.ItemOperations.OpenFile(sourcePath);
                    if (sourceLine != null && _dte.ActiveDocument?.Selection is TextSelection selection)
                    {
                        selection.GotoLine(sourceLine.Value, Select: false);
                    }
                }
                catch (Exception)
                {
                    // The scanned path may no longer exist (renamed file); the selection itself still applies.
                }
            };

            Events2 events = (Events2)_dte.Events;
            _documentEvents = events.DocumentEvents;
            _documentEvents.DocumentSaved += document =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _control?.NotifySourceChanged(document.FullName);
            };
            _windowEvents = events.WindowEvents;
            _windowEvents.WindowActivated += (gotFocus, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string path = gotFocus?.Document?.FullName;
                if (path != null)
                {
                    _control?.AutoMatchTarget(path, fromEditor: true);
                }
            };

            // Opening the tool window states the intent to preview: start without a manual Start.
            control.AutoStart();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control?.StopSession();
                _control = null;
            }
            base.Dispose(disposing);
        }
    }
}
