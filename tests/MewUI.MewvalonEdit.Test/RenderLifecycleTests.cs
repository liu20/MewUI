using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Which stage of a frame is allowed to stand the visible lines up. Laying out during the paint is
/// what these pin down: the lines have to follow a scroll, and painting them must not feed scroll
/// state back into the control. Each case drives whole frames rather than one stage, so it says the
/// same thing whether the lines are built while measuring, arranging or painting.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RenderLifecycleTests
{
    private const int WIDTH = 360;
    private const int HEIGHT = 200;
    private const int LINES = 4000;
    // Far enough in that the lines above are still estimated rather than measured.
    private const double DEEP_OFFSET = 12000;

    [TestMethod]
    public void ScrollingMovesTheVisibleLinesWithinOneFrame()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        using var host = new Host();
        host.Frame();
        int topBefore = host.TopLineNumber;

        host.Editor.ScrollToVerticalOffset(600);
        host.Frame();

        Assert.IsGreaterThan(topBefore, host.TopLineNumber, "the lines stayed where they were after a scroll");
    }

    /// <summary>
    /// Painting reports what the control already decided. A frame that only paints must leave the
    /// scroll offset alone, or the state a caller reads depends on how often the window repainted.
    /// </summary>
    [TestMethod]
    public void PaintingDoesNotMoveTheScrollOffset()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        using var host = new Host();
        host.Frame();

        // Straight to a depth whose lines above are still estimated, which is where the offset gets
        // corrected. The correction belongs to the stage that stands the lines up, not to the paint.
        host.Editor.ScrollToVerticalOffset(DEEP_OFFSET);
        double asked = host.Editor.VerticalOffset;
        int notifications = 0;
        ((ITextViewHost)host.Editor.Surface).ScrollOffsetChanged += _ => notifications++;
        host.Paint();

        Assert.AreEqual(asked, host.Editor.VerticalOffset, "a paint moved the scroll offset");
        Assert.AreEqual(0, notifications, "a paint announced a scroll the caller never asked for");
    }

    /// <summary>
    /// Line heights above the viewport are estimated until they are laid out, so the offset a deep
    /// scroll lands on is corrected once the real heights arrive. The line under the anchor must not
    /// move while that correction happens, and it must stop happening.
    /// </summary>
    [TestMethod]
    public void TheAnchorLineSurvivesEstimatesTurningIntoMeasurements()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        using var host = new Host();
        host.Editor.ScrollToVerticalOffset(DEEP_OFFSET);
        host.Frame();
        host.Frame();

        int anchor = host.TopLineNumber;
        double offset = host.Editor.VerticalOffset;
        for (int frame = 0; frame < 3; frame++)
        {
            host.Frame();
        }

        Assert.AreEqual(anchor, host.TopLineNumber, "the anchor line drifted while the frames settled");
        Assert.AreEqual(offset, host.Editor.VerticalOffset, "the offset kept being corrected");
    }

    /// <summary>An editor in a window, driven a whole frame at a time.</summary>
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        private readonly IGraphicsFactory _factory = Application.DefaultGraphicsFactory;
        private readonly IRenderSurface _surface;

        public Host()
        {
            _window = ScaledWindow.Create(1.0, WIDTH, HEIGHT);
            Editor = new TextEditor
            {
                Text = string.Join('\n', Enumerable.Range(0, LINES).Select(static index => $"line {index}")),
                ShowLineNumbers = false
            };
            _window.Content = Editor;
            _surface = _factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(WIDTH, HEIGHT, 1));
        }

        public TextEditor Editor { get; }

        public int TopLineNumber => Editor.TextArea.TextView.Host.VisibleTextLines[0].LogicalLine.LineNumber;

        /// <summary>Lays out what needs it and paints, as a window does once per frame.</summary>
        public void Frame()
        {
            _window.PerformLayout();
            Paint();
        }

        public void Paint()
        {
            using var context = _factory.CreateContext(_surface);
            context.BeginFrame(_surface);
            Editor.Render(context);
            context.EndFrame();
        }

        public void Dispose() => _surface.Dispose();
    }
}
