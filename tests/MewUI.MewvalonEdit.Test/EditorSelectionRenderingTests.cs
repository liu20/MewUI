using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The editor stacks its own layers on the host's, so a bridge or marker layer can cover the
/// selection without any contract breaking. These render for real and compare pixels.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EditorSelectionRenderingTests
{
    private const int WIDTH = 320;
    private const int HEIGHT = 80;

    [TestMethod]
    public void SelectingTextInTheEditorChangesWhatIsPainted()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] unselected = Render(selectionLength: 0, custom: false);
        byte[] selected = Render(selectionLength: 6, custom: false);

        Assert.IsGreaterThan(0, CountDifferingPixels(unselected, selected),
            "Selecting text painted nothing: the default selection is invisible in the editor.");
    }

    [TestMethod]
    public void CustomSelectionColorsPaintSomethingToo()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] unselected = Render(selectionLength: 0, custom: true);
        byte[] selected = Render(selectionLength: 6, custom: true);

        Assert.IsGreaterThan(0, CountDifferingPixels(unselected, selected),
            "The replacement selection layer painted nothing.");
    }

    [TestMethod]
    public void ClearingACustomSelectionColorKeepsTheSelectionVisible()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        // Toggling a custom color on and back off leaves the replacement layer installed with no
        // color of its own; it has to fall back to the theme instead of painting nothing.
        byte[] unselected = Render(selectionLength: 0, custom: true, thenClear: true);
        byte[] selected = Render(selectionLength: 6, custom: true, thenClear: true);

        Assert.IsGreaterThan(0, CountDifferingPixels(unselected, selected),
            "Clearing the custom selection color left the selection invisible.");
    }

    private static byte[] Render(int selectionLength, bool custom, bool thenClear = false)
    {
        var editor = new TextEditor
        {
            Text = "select this line",
            ShowLineNumbers = false,
            SkipViewportCull = true
        };
        if (custom)
        {
            editor.TextArea.SelectionBrush = Color.FromRgb(0x30, 0x60, 0xC0);
        }
        if (thenClear)
        {
            editor.TextArea.SelectionBrush = null;
        }
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));
        if (selectionLength > 0)
        {
            editor.Select(0, selectionLength);
        }

        var factory = Application.DefaultGraphicsFactory;
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(WIDTH, HEIGHT, 1));
        using (var context = factory.CreateContext(surface))
        {
            context.BeginFrame(surface);
            editor.Render(context);
            context.EndFrame();
        }
        return ((ICpuPixelSurface)surface).GetReadOnlyPixelSpan().ToArray();
    }

    private static int CountDifferingPixels(byte[] left, byte[] right)
    {
        int differing = 0;
        for (int index = 0; index < Math.Min(left.Length, right.Length); index += 4)
        {
            if (left[index] != right[index] ||
                left[index + 1] != right[index + 1] ||
                left[index + 2] != right[index + 2])
            {
                differing++;
            }
        }
        return differing;
    }
}
