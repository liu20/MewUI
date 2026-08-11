using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The column rule and the current-line highlight are drawn, not laid out, so only a real render
/// shows them. Each case toggles one option and compares the same document either way.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ColumnRulerAndCurrentLineTests
{
    private const int WIDTH = 320;
    private const int HEIGHT = 120;
    private const string TEXT = "first line\nsecond line\nthird line\n";

    [TestMethod]
    public void TheColumnRuleIsDrawnWhenEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] without = Render(editor => editor.Options.ShowColumnRuler = false);
        byte[] with = Render(editor =>
        {
            editor.Options.ColumnRulerPosition = 10;
            editor.Options.ShowColumnRuler = true;
        });

        Assert.IsGreaterThan(0, CountDifferingPixels(without, with), "No column rule was drawn.");
    }

    [TestMethod]
    public void ThePositionMovesTheRule()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] near = Render(editor =>
        {
            editor.Options.ColumnRulerPosition = 5;
            editor.Options.ShowColumnRuler = true;
        });
        byte[] far = Render(editor =>
        {
            editor.Options.ColumnRulerPosition = 20;
            editor.Options.ShowColumnRuler = true;
        });

        Assert.IsGreaterThan(0, CountDifferingPixels(near, far), "The rule ignored its position.");
    }

    [TestMethod]
    public void TheCurrentLineIsPaintedAndFollowsTheCaret()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        byte[] off = Render(editor => editor.Options.HighlightCurrentLine = false);
        byte[] onFirst = Render(editor =>
        {
            editor.Options.HighlightCurrentLine = true;
            editor.CaretOffset = 0;
        });
        byte[] onThird = Render(editor =>
        {
            editor.Options.HighlightCurrentLine = true;
            editor.CaretOffset = TEXT.IndexOf("third", StringComparison.Ordinal);
        });

        Assert.IsGreaterThan(0, CountDifferingPixels(off, onFirst), "The current line was not painted.");
        Assert.IsGreaterThan(0, CountDifferingPixels(onFirst, onThird),
            "The highlight stayed put when the caret moved to another line.");
    }

    private static byte[] Render(Action<TextEditor> configure)
    {
        var editor = new TextEditor { Text = TEXT, ShowLineNumbers = false, SkipViewportCull = true };
        configure(editor);
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
