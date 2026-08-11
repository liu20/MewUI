// Preview tool window surface, the Swing counterpart of tools/vscode-mewui/src/panel.ts:
// toolbar (project/target pickers, zoom, theme, refresh, restart, status), frame canvas, and
// input forwarding. Frames arrive as BGRA buffers and convert to an ARGB BufferedImage; after
// each blit the session gets a FrameAck (flow control, plan.md 4.3.1).
package com.aprillz.mewui.preview

import java.awt.BorderLayout
import java.awt.Color
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.RenderingHints
import java.awt.image.BufferedImage
import java.awt.event.ComponentAdapter
import java.awt.event.ComponentEvent
import java.awt.event.KeyAdapter
import java.awt.event.KeyEvent
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.awt.event.MouseWheelEvent
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.abs
import kotlin.math.round
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.JScrollPane
import javax.swing.JTextArea
import javax.swing.SwingUtilities
import javax.swing.Timer

class PreviewPanel(
    private val logSink: (String) -> Unit,
    private val activeDocumentPathProvider: () -> String?,
    private val solutionDirectoryProvider: () -> String?,
    private val navigateToSource: (path: String, line: Int?) -> Unit,
    loadNavigatePreference: () -> Boolean = { true },
    private val saveNavigatePreference: (Boolean) -> Unit = {},
) : JPanel(BorderLayout()) {

    private val projectCombo = JComboBox<ProjectEntry>()
    private val startButton = JButton("Start")
    private val targetCombo = JComboBox<TargetEntry>()
    private val zoomCombo = JComboBox(arrayOf("Fit", "50%", "100%", "150%", "200%"))
    private val themeButton = JButton("Theme")
    private val refreshButton = JButton("Refresh")
    private val restartButton = JButton("Restart")
    private val navigateCheck = JCheckBox("Go to code", loadNavigatePreference())
    private val stateLabel = JLabel("Not running. Pick a project and press Start.")
    private val canvas = FrameCanvas()
    private val scrollPane = JScrollPane(canvas)
    private val detailArea = JTextArea().apply { isEditable = false; foreground = Color(205, 92, 92); isVisible = false; rows = 6 }
    private val viewportTimer = Timer(250) { postViewport() }.apply { isRepeats = false }

    private var session: PreviewSession? = null
    private var targets: List<PreviewTargetInfo> = emptyList()
    private var activeTargetId = ""
    private var themeMode = ""
    private var updatingCombos = false
    // A manual dropdown pick wins over auto-match until the user moves to another editor file.
    private var manualOverride = false

    init {
        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 4))
        listOf(projectCombo, startButton, targetCombo, zoomCombo, themeButton, refreshButton, restartButton, navigateCheck, stateLabel)
            .forEach(toolbar::add)
        navigateCheck.toolTipText = "Jump to the target's declaration when the preview target changes"
        navigateCheck.addActionListener { saveNavigatePreference(navigateCheck.isSelected) }
        targetCombo.isEnabled = false
        themeButton.isEnabled = false
        refreshButton.isEnabled = false
        restartButton.isEnabled = false
        refreshButton.toolTipText = "Rebuild the current target"
        restartButton.toolTipText = "Restart the preview session (full state reset)"

        add(toolbar, BorderLayout.NORTH)
        add(scrollPane, BorderLayout.CENTER)
        add(JScrollPane(detailArea).apply { isVisible = false }, BorderLayout.SOUTH)

        startButton.addActionListener { toggleSession() }
        refreshButton.addActionListener { session?.refreshTarget() }
        restartButton.addActionListener { session?.restartProcess() }
        themeButton.addActionListener { session?.setTheme(if (themeMode == "dark") "light" else "dark") }
        zoomCombo.addActionListener {
            canvas.zoom = zoomCombo.selectedItem as String
            canvas.revalidate()
            canvas.repaint()
            postViewport()
        }
        targetCombo.addActionListener {
            if (!updatingCombos) {
                (targetCombo.selectedItem as? TargetEntry)?.let { entry ->
                    if (entry.target.available) {
                        activeTargetId = entry.target.id
                        manualOverride = true
                        session?.selectTarget(entry.target.id)
                        // Reverse sync (opt-in): picking a target jumps the editor to its declaration.
                        if (navigateCheck.isSelected) {
                            entry.target.sourcePath?.let { path -> navigateToSource(path, entry.target.sourceLine) }
                        }
                    }
                }
            }
        }
        scrollPane.viewport.addComponentListener(object : ComponentAdapter() {
            override fun componentResized(event: ComponentEvent) {
                viewportTimer.restart()
            }
        })
        hookInput()
        populateProjects()
    }

    // ---- session lifecycle ---------------------------------------------------------------

    /**
     * Selects the target declared in the given file. Editor-driven calls (the user moved to
     * another file) override a manual dropdown pick; refresh-driven calls must not, or a
     * targets rebroadcast would silently revert the pick while its file is open.
     */
    fun autoMatchTarget(fsPath: String?, fromEditor: Boolean = false) {
        val current = session ?: return
        if (fsPath == null || !fsPath.endsWith(".cs", ignoreCase = true)) {
            return
        }
        if (!fromEditor && manualOverride) {
            return
        }
        val match = targets.firstOrNull {
            it.available && it.sourcePath != null
                && File(it.sourcePath).absolutePath.equals(File(fsPath).absolutePath, ignoreCase = true)
        } ?: return
        if (match.id != activeTargetId) {
            logSink("[session] auto-selecting ${match.id} for ${File(fsPath).name}")
            activeTargetId = match.id
            manualOverride = false
            current.selectTarget(match.id)
            syncTargetCombo()
        }
    }

    fun notifySourceChanged(fsPath: String) {
        session?.notifySourceChanged(fsPath)
    }

    /** Starts the session for the auto-selected project; a no-op when one is already running. */
    fun autoStart() {
        if (session == null && projectCombo.selectedItem != null) {
            toggleSession()
        }
    }

    fun stopSession() {
        val current = session
        session = null
        current?.stop()
        setSessionUiEnabled(false)
    }

    private fun toggleSession() {
        if (session != null) {
            stopSession()
            return
        }
        val projectPath = (projectCombo.selectedItem as? ProjectEntry)?.path ?: run {
            populateProjects()
            (projectCombo.selectedItem as? ProjectEntry)?.path
        }
        if (projectPath == null) {
            showState("No executable (.csproj with OutputType Exe/WinExe) project found.", isError = true)
            return
        }

        val callbacks = object : SessionCallbacks {
            override fun onLog(line: String) = onEdt { logSink(line) }
            override fun onState(state: SessionState, detail: String?) = onEdt { handleSessionState(state, detail) }
            override fun onTargets(targets: List<PreviewTargetInfo>, activeId: String) = onEdt { handleTargets(targets, activeId) }
            override fun onFrame(header: FrameHeader, pixels: ByteArray) = onEdt { handleFrame(header, pixels) }
            override fun onStatus(status: StatusInfo) = onEdt { handleStatus(status) }
        }
        val newSession = PreviewSession(projectPath, callbacks)
        session = newSession
        setSessionUiEnabled(true)
        try {
            newSession.start()
        } catch (error: Exception) {
            showState("failed to start: ${error.message}", isError = true)
            stopSession()
        }
    }

    private fun onEdt(action: () -> Unit) = SwingUtilities.invokeLater(action)

    private fun setSessionUiEnabled(running: Boolean) {
        startButton.text = if (running) "Stop" else "Start"
        projectCombo.isEnabled = !running
        targetCombo.isEnabled = running
        themeButton.isEnabled = running
        refreshButton.isEnabled = running
        restartButton.isEnabled = running
        if (!running) {
            showState("Stopped.", isError = false)
        }
    }

    private fun populateProjects() {
        val previous = (projectCombo.selectedItem as? ProjectEntry)?.path
        val solutionDirectory = solutionDirectoryProvider()
        val executables = ProjectLocator.findExecutableProjects(solutionDirectory).toMutableList()
        val activePath = activeDocumentPathProvider()
        val nearest = activePath?.let { ProjectLocator.findNearestProject(it, solutionDirectory) }
        if (nearest != null && ProjectLocator.isExecutableProject(nearest)
            && executables.none { it.equals(nearest, ignoreCase = true) }
        ) {
            executables.add(0, nearest)
        }

        updatingCombos = true
        projectCombo.removeAllItems()
        executables.forEach { projectCombo.addItem(ProjectEntry(it)) }
        updatingCombos = false

        val preferred = previous ?: nearest?.takeIf(ProjectLocator::isExecutableProject)
        if (preferred != null) {
            for (index in 0 until projectCombo.itemCount) {
                if (projectCombo.getItemAt(index).path.equals(preferred, ignoreCase = true)) {
                    projectCombo.selectedIndex = index
                    break
                }
            }
        }
    }

    private class ProjectEntry(val path: String) {
        override fun toString(): String = File(path).nameWithoutExtension
    }

    private class TargetEntry(val target: PreviewTargetInfo) {
        override fun toString(): String {
            val label = if (target.kind == "main") target.displayName else "${target.displayName} (${target.kind})"
            return if (target.available) label else "$label - unavailable"
        }
    }

    // ---- session events ------------------------------------------------------------------

    private fun handleSessionState(state: SessionState, detail: String?) {
        logSink("[session] $state${if (detail != null) ": $detail" else ""}")
        when (state) {
            SessionState.FAILED, SessionState.DISCONNECTED ->
                showState("$state${if (detail != null) ": $detail" else ""}", isError = true)
            SessionState.STARTING ->
                showState(if (detail != null) "Restarting..." else "Starting...", isError = false)
            else -> {}
        }
    }

    private fun handleTargets(newTargets: List<PreviewTargetInfo>, activeId: String) {
        targets = newTargets
        activeTargetId = activeId
        syncTargetCombo()
        autoMatchTarget(activeDocumentPathProvider())
    }

    private fun syncTargetCombo() {
        updatingCombos = true
        targetCombo.removeAllItems()
        var selected: TargetEntry? = null
        for (target in targets) {
            val entry = TargetEntry(target)
            targetCombo.addItem(entry)
            if (target.id == activeTargetId) {
                selected = entry
            }
        }
        if (selected != null) {
            targetCombo.selectedItem = selected
        }
        updatingCombos = false
    }

    private fun handleStatus(status: StatusInfo) {
        showState(status.message, status.hasError)
        detailArea.text = status.exceptionDetail ?: ""
        detailArea.parent?.parent?.isVisible = !status.exceptionDetail.isNullOrEmpty()
        detailArea.isVisible = !status.exceptionDetail.isNullOrEmpty()
        revalidate()
        if (!status.themeMode.isNullOrEmpty()) {
            themeMode = status.themeMode
            themeButton.text = when (themeMode) {
                "dark" -> "Dark"
                "light" -> "Light"
                else -> "System"
            }
        }
    }

    private fun handleFrame(header: FrameHeader, pixels: ByteArray) {
        canvas.setFrame(header, pixels)
        session?.ackFrame(header.seq)
    }

    private fun showState(message: String, isError: Boolean) {
        stateLabel.text = message
        stateLabel.foreground = if (isError) Color(205, 92, 92) else null
        stateLabel.toolTipText = message
    }

    private fun zoomFactor(): Double {
        val zoom = zoomCombo.selectedItem as? String ?: "Fit"
        if (zoom == "Fit") {
            return 1.0
        } else {
            return zoom.trimEnd('%').toInt() / 100.0
        }
    }

    private fun postViewport() {
        val current = session ?: return
        val viewport = scrollPane.viewport.size
        if (viewport.width <= 0) {
            return
        }
        current.setViewport(
            maxOf(1.0, (viewport.width - 16).toDouble()),
            maxOf(1.0, (viewport.height - 16).toDouble()),
            96.0 * deviceScale() * zoomFactor(),
        )
    }

    private fun deviceScale(): Double =
        graphicsConfiguration?.defaultTransform?.scaleX ?: 1.0

    // ---- frame canvas / input ------------------------------------------------------------

    private inner class FrameCanvas : JPanel() {
        var zoom: String = "Fit"
        private var image: BufferedImage? = null
        private var dpiScale = 1.0

        init {
            isFocusable = true
        }

        fun setFrame(header: FrameHeader, pixels: ByteArray) {
            dpiScale = if (header.dpiScale > 0) header.dpiScale else 1.0
            var current = image
            if (current == null || current.width != header.width || current.height != header.height) {
                current = BufferedImage(header.width, header.height, BufferedImage.TYPE_INT_ARGB)
                image = current
                revalidate()
            }
            // BGRA little-endian bytes read as ints are exactly ARGB values.
            val ints = IntArray(header.width)
            val byteView = ByteBuffer.wrap(pixels).order(ByteOrder.LITTLE_ENDIAN)
            for (row in 0 until header.height) {
                byteView.position(row * header.stride)
                byteView.asIntBuffer().get(ints, 0, header.width)
                current.setRGB(0, row, header.width, 1, ints, 0, header.width)
            }
            repaint()
        }

        /**
         * Displayed size in swing units. Numeric zooms ride the requested DPI (the session
         * re-renders at the zoomed scale), so the frame always shows one frame pixel per
         * device pixel; Fit remains a display-side downscale of the natural-scale render.
         */
        private fun displaySize(): Dimension {
            val current = image ?: return Dimension(1, 1)
            val scale = deviceScale()
            if (zoom == "Fit") {
                val viewport = scrollPane.viewport.size
                val fit = minOf(
                    1.0,
                    (viewport.width - 16).toDouble() / current.width,
                    (viewport.height - 16).toDouble() / current.height,
                )
                return Dimension((current.width * fit).toInt().coerceAtLeast(1), (current.height * fit).toInt().coerceAtLeast(1))
            }
            return Dimension(
                (current.width / scale).toInt().coerceAtLeast(1),
                (current.height / scale).toInt().coerceAtLeast(1),
            )
        }

        override fun getPreferredSize(): Dimension = displaySize()

        override fun paintComponent(graphics: Graphics) {
            super.paintComponent(graphics)
            val current = image ?: return
            val size = displaySize()
            val g2 = graphics.create() as Graphics2D
            try {
                // Integer device-pixel upscales keep pixels crisp; everything else (fit
                // downscale, fractional zoom) needs resampling - AWT defaults to nearest.
                val factor = size.width * deviceScale() / current.width
                val isIntegerUpscale = factor >= 1.0 && abs(factor - round(factor)) < 0.01
                g2.setRenderingHint(
                    RenderingHints.KEY_INTERPOLATION,
                    if (isIntegerUpscale) RenderingHints.VALUE_INTERPOLATION_NEAREST_NEIGHBOR
                    else RenderingHints.VALUE_INTERPOLATION_BILINEAR,
                )
                g2.drawImage(current, 0, 0, size.width, size.height, null)
            } finally {
                g2.dispose()
            }
        }

        fun toDip(event: MouseEvent): Pair<Double, Double> {
            val current = image ?: return 0.0 to 0.0
            val size = displaySize()
            val scale = if (size.width > 0) current.width.toDouble() / size.width else 1.0
            return (event.x * scale / dpiScale) to (event.y * scale / dpiScale)
        }
    }

    private fun hookInput() {
        val mouse = object : MouseAdapter() {
            override fun mousePressed(event: MouseEvent) {
                canvas.requestFocusInWindow()
                sendPointer(PreviewProtocol.POINTER_PRESSED, event)
            }

            override fun mouseReleased(event: MouseEvent) = sendPointer(PreviewProtocol.POINTER_RELEASED, event)

            override fun mouseMoved(event: MouseEvent) = sendPointer(PreviewProtocol.POINTER_MOVED, event)

            override fun mouseDragged(event: MouseEvent) = sendPointer(PreviewProtocol.POINTER_MOVED, event)

            override fun mouseWheelMoved(event: MouseWheelEvent) {
                val (x, y) = canvas.toDip(event)
                // AWT +rotation = scroll down; the wire convention is +Y = scroll-up intent in notches.
                session?.sendInput(
                    PreviewProtocol.SCROLL,
                    mapOf(
                        "x" to x, "y" to y,
                        "deltaX" to 0.0, "deltaY" to -event.preciseWheelRotation,
                        "modifiers" to W3cInput.modifiers(event),
                    ),
                )
                event.consume()
            }
        }
        canvas.addMouseListener(mouse)
        canvas.addMouseMotionListener(mouse)
        canvas.addMouseWheelListener(mouse)

        canvas.addKeyListener(object : KeyAdapter() {
            override fun keyPressed(event: KeyEvent) = sendKey(event, isDown = true)

            override fun keyReleased(event: KeyEvent) = sendKey(event, isDown = false)

            override fun keyTyped(event: KeyEvent) {
                val char = event.keyChar
                if (!char.isISOControl() && char != KeyEvent.CHAR_UNDEFINED) {
                    session?.sendInput(PreviewProtocol.TEXT_INPUT, mapOf("text" to char.toString()))
                }
            }
        })
    }

    private fun sendPointer(typeId: Int, event: MouseEvent) {
        val (x, y) = canvas.toDip(event)
        session?.sendInput(
            typeId,
            mapOf(
                "x" to x, "y" to y,
                "button" to W3cInput.button(event),
                "buttons" to W3cInput.buttons(event),
                "clickCount" to maxOf(1, event.clickCount),
                "modifiers" to W3cInput.modifiers(event),
            ),
        )
    }

    private fun sendKey(event: KeyEvent, isDown: Boolean) {
        val code = W3cInput.keyCode(event.keyCode) ?: return
        session?.sendInput(
            PreviewProtocol.KEY,
            mapOf("code" to code, "isDown" to isDown, "modifiers" to W3cInput.modifiers(event)),
        )
        // Mirror the native key-then-character sequence for keys keyTyped skips.
        if (isDown && W3cInput.modifiers(event) and 13 == 0) {
            when (event.keyCode) {
                KeyEvent.VK_ENTER -> session?.sendInput(PreviewProtocol.TEXT_INPUT, mapOf("text" to "\r"))
                KeyEvent.VK_TAB -> session?.sendInput(PreviewProtocol.TEXT_INPUT, mapOf("text" to "\t"))
            }
        }
        if (event.keyCode in intArrayOf(KeyEvent.VK_TAB, KeyEvent.VK_LEFT, KeyEvent.VK_RIGHT, KeyEvent.VK_UP, KeyEvent.VK_DOWN, KeyEvent.VK_SPACE)) {
            event.consume()
        }
    }
}
