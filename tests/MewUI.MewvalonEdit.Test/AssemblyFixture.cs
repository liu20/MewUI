using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public static class AssemblyFixture
{
    /// <summary>
    /// Registers the graphics backend once before any test runs. Tests that read line metrics or
    /// lay the editor out measure text through it; swapping factories mid-run would race them.
    /// </summary>
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        // Headless tests exercise the in-surface popup path; a native popup would try to open a real
        // OS window, which needs a running Application.
        PopupManager.PreferNativePopups = false;

        if (OperatingSystem.IsWindows())
        {
            GdiBackend.Register();
        }
    }
}
