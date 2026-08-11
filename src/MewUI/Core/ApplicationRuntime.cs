using Aprillz.MewUI.Input;

namespace Aprillz.MewUI;

/// <summary>
/// Owns the run-scoped mutable state of a single <see cref="Application.Run(Window)"/> (the window registry and
/// main-window identity) and tears the per-run state down in a fixed order when the run ends, so no
/// window or drag reference survives into the next run.
/// </summary>
internal sealed class ApplicationRuntime : IDisposable
{
    private readonly List<Window> _windows = new();
    private bool _disposed;

    internal IReadOnlyList<Window> Windows => _windows;

    internal Window? MainWindow { get; set; }

    internal void Register(Window window)
    {
        if (!_windows.Contains(window))
        {
            _windows.Add(window);
        }
    }

    internal void Unregister(Window window, ShutdownMode shutdownMode)
    {
        bool wasMainWindow = ReferenceEquals(window, MainWindow);
        _windows.Remove(window);
        if (Application.ShouldShutdownAfterClose(shutdownMode, wasMainWindow, _windows.Count))
        {
            Application.Shutdown();
        }
    }

    // Theme broadcast iterates a snapshot so a handler that registers or unregisters a window mid-broadcast
    // neither adds nor skips a notification.
    internal Window[] SnapshotWindows() => _windows.ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Drag reset must precede the registry clear: drag target resolution reads the live window
        // registry, so the registry stays populated until the drag session is torn down.
        WindowDragDropRouter.ResetForRuntimeEnd();
        _windows.Clear();
        MainWindow = null;
    }
}
