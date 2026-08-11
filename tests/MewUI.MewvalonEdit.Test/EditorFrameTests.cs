using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The editor is a templated control, and a control with a template suppresses its own chrome, so
/// the frame is drawn by the template's border. Only a real render shows whether it survived.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EditorFrameTests
{
    private const int WIDTH = 240;
    private const int HEIGHT = 60;

    [TestMethod]
    public void TheFrameIsDrawnThroughTheTemplate()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] framed = Render(borderThickness: 2);
        byte[] bare = Render(borderThickness: 0);

        // The top two rows hold the border and nothing else, so a difference there can only be it.
        int differing = CountDifferingPixels(framed, bare, rows: 2);

        Assert.IsGreaterThan(0, differing, "The editor drew no frame.");
    }

    private static byte[] Render(double borderThickness)
    {
        var editor = new TextEditor
        {
            Text = "framed",
            ShowLineNumbers = false,
            SkipViewportCull = true
        };
        // Local values, so the case does not depend on the frame style resolving: what is under
        // test is whether the editor's chrome reaches the template border at all.
        editor.BorderBrush = Color.FromRgb(0xC0, 0x20, 0x20);
        editor.BorderThickness = borderThickness;
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

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

    private static int CountDifferingPixels(byte[] left, byte[] right, int rows)
    {
        int differing = 0;
        for (int index = 0; index < Math.Min(rows * WIDTH * 4, Math.Min(left.Length, right.Length)); index += 4)
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
