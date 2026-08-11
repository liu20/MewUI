using Aprillz.MewUI.MewvalonEdit;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// The scroll surface ported code drives directly. Absolute positioning is assembled from the
/// smallest-scroll contract, so these pin that an asked-for offset is the offset that results.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ScrollingTests
{
    private const int WIDTH = 200;
    private const int HEIGHT = 100;

    [TestMethod]
    public void ScrollingToAnOffsetLandsOnIt()
    {
        var editor = CreateEditor();

        editor.ScrollToVerticalOffset(40);

        Assert.AreEqual(40, editor.VerticalOffset, 0.01);
    }

    /// <summary>The four scroll metrics ported code reads before deciding where to scroll.</summary>
    [TestMethod]
    public void TheScrollMetricsDescribeTheDocumentAndTheViewport()
    {
        var editor = CreateEditor();

        Assert.AreEqual(HEIGHT, editor.ViewportHeight, HEIGHT * 0.5);
        Assert.AreEqual(WIDTH, editor.ViewportWidth, WIDTH * 0.5);
        Assert.IsGreaterThan(editor.ViewportHeight, editor.ExtentHeight);
        Assert.IsGreaterThan(0, editor.ExtentWidth);
    }

    [TestMethod]
    public void OffsetsClampToTheDocument()
    {
        var editor = CreateEditor();

        editor.ScrollToVerticalOffset(-100);
        Assert.AreEqual(0, editor.VerticalOffset, 0.01);

        editor.ScrollToVerticalOffset(editor.ExtentHeight * 2);
        Assert.AreEqual(editor.ExtentHeight - editor.ViewportHeight, editor.VerticalOffset, 0.01);
    }

    [TestMethod]
    public void HomeAndEndReachBothEndsOfTheDocument()
    {
        var editor = CreateEditor();

        editor.ScrollToEnd();
        Assert.AreEqual(editor.ExtentHeight - editor.ViewportHeight, editor.VerticalOffset, 0.01);

        editor.ScrollToHome();
        Assert.AreEqual(0, editor.VerticalOffset, 0.01);
    }

    [TestMethod]
    public void ALineStepMovesOneLineAndAPageStepOneViewport()
    {
        var editor = CreateEditor();
        double lineHeight = editor.TextArea.TextView.DefaultLineHeight;

        editor.LineDown();
        Assert.AreEqual(lineHeight, editor.VerticalOffset, 0.01);

        editor.LineUp();
        Assert.AreEqual(0, editor.VerticalOffset, 0.01);

        editor.PageDown();
        Assert.AreEqual(editor.ViewportHeight, editor.VerticalOffset, 0.01);
    }

    /// <summary>
    /// A line asked for lands in the middle of the viewport. The document is tall enough that the
    /// centred position is neither clamped to the top nor to the bottom.
    /// </summary>
    [TestMethod]
    public void ScrollingToALineCentresIt()
    {
        var editor = CreateEditor();
        double lineHeight = editor.TextArea.TextView.DefaultLineHeight;

        editor.ScrollToLine(30);

        double middleOfViewport = editor.VerticalOffset + (editor.ViewportHeight / 2);
        Assert.AreEqual(29 * lineHeight, middleOfViewport - (lineHeight / 2), lineHeight);
    }

    /// <summary>A move too small to matter is skipped, which is what keeps the view from jittering.</summary>
    [TestMethod]
    public void AMoveShorterThanAThirdOfTheViewportIsSkipped()
    {
        var editor = CreateEditor();
        editor.ScrollToLine(30);
        double before = editor.VerticalOffset;

        editor.ScrollToLine(31);

        Assert.AreEqual(before, editor.VerticalOffset, 0.01);
    }

    private static TextEditor CreateEditor()
    {
        var editor = new TextEditor
        {
            Text = string.Join('\n', Enumerable.Range(1, 60).Select(number => $"line {number}")),
            ShowLineNumbers = false
        };
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));
        return editor;
    }
}
