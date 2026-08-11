using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewUI.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test.Infrastructure;

/// <summary>
/// A window at a chosen DPI, so a case is laid out and snapped on the pixel grid of a scaled
/// display. Rendering at 100% and magnifying is not the same thing: the rounding runs against the
/// grid the window reports, and that is where scale-dependent snapping goes wrong.
/// </summary>
internal static class ScaledWindow
{
    public static Window Create(double dpiScale, double width = 800, double height = 600)
    {
        var window = new Window();
        window.AttachBackend(new HeadlessWindowBackend());
        window.SetDpi((uint)Math.Round(96 * dpiScale));
        window.SetClientSizeDip(width, height);
        return window;
    }
}
