using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A margin repaints when the visible lines change. Standing the same lines up again and calling
/// that a change makes every rendered frame ask for the next one, because the repaint request walks
/// up to the window.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RenderLoopTests
{
    private const int WIDTH = 360;
    private const int HEIGHT = 200;
    private const string TEXT = "class A\n{\n    int x;\n}\nafter\n";

    [TestMethod]
    public void RenderingAgainAsksForNoFurtherRender()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = TEXT, ShowLineNumbers = false, SkipViewportCull = true };
        var margin = new CountingMargin();
        editor.TextArea.LeftMargins.Add(margin);
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

        var factory = Application.DefaultGraphicsFactory;
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(WIDTH, HEIGHT, 1));
        Render(editor, factory, surface);
        int settled = margin.Invalidations;
        for (int frame = 0; frame < 3; frame++)
        {
            Render(editor, factory, surface);
        }

        Assert.AreEqual(settled, margin.Invalidations,
            "A frame that changed nothing still asked for a repaint, which schedules the next frame.");
    }

    /// <summary>Scrolling does change the visible lines, so the margin must hear about that one.</summary>
    [TestMethod]
    public void ScrollingStillTellsTheMargin()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor
        {
            Text = string.Join('\n', Enumerable.Range(0, 400).Select(static index => $"line {index}")),
            ShowLineNumbers = false
        };
        var margin = new CountingMargin();
        editor.TextArea.LeftMargins.Add(margin);
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

        var factory = Application.DefaultGraphicsFactory;
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(WIDTH, HEIGHT, 1));
        Render(editor, factory, surface);
        int settled = margin.Invalidations;
        int topBefore = editor.TextArea.TextView.Host.VisibleTextLines[0].LogicalLine.LineNumber;

        editor.ScrollToVerticalOffset(600);
        Render(editor, factory, surface);

        Assert.IsGreaterThan(topBefore, editor.TextArea.TextView.Host.VisibleTextLines[0].LogicalLine.LineNumber,
            "Scrolling did not move the visible lines.");
        Assert.IsGreaterThan(settled, margin.Invalidations, "The margin was not told the lines moved.");
    }

    private static void Render(TextEditor editor, IGraphicsFactory factory, IRenderSurface surface)
    {
        using var context = factory.CreateContext(surface);
        context.BeginFrame(surface);
        editor.Render(context);
        context.EndFrame();
    }

    private sealed class CountingMargin : AbstractMargin
    {
        public int Invalidations { get; private set; }

        public override void InvalidateVisual()
        {
            Invalidations++;
            base.InvalidateVisual();
        }

        protected override void OnRenderTextViewport(IGraphicsContext context, Rect textViewport)
        {
        }
    }
}
