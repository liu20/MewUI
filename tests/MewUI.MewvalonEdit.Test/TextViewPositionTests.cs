using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// A position carries both a document location and the visual column it lands on. The two differ
/// wherever a projection stands a different number of columns in for the document text.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class TextViewPositionTests
{
    private const int WIDTH = 300;
    private const int HEIGHT = 120;

    [TestMethod]
    public void AnUnknownVisualColumnIsMinusOne()
    {
        var position = new TextViewPosition(3, 5);

        Assert.AreEqual(-1, position.VisualColumn);
        Assert.AreEqual(new TextLocation(3, 5), position.Location);
        Assert.IsFalse(position.IsAtEndOfLine);
    }

    /// <summary>Location first, then the visual column, then the end of a wrap before its start.</summary>
    [TestMethod]
    public void PositionsOrderByLocationThenColumnThenWrapSide()
    {
        Assert.IsLessThan(0, new TextViewPosition(1, 9, 9).CompareTo(new TextViewPosition(2, 1, 1)));
        Assert.IsLessThan(0, new TextViewPosition(1, 1, 1).CompareTo(new TextViewPosition(1, 1, 2)));

        var atEnd = new TextViewPosition(1, 1, 1) { IsAtEndOfLine = true };
        var atStart = new TextViewPosition(1, 1, 1);
        Assert.IsLessThan(0, atEnd.CompareTo(atStart));
        Assert.AreNotEqual(atEnd, atStart);
    }

    [TestMethod]
    public void LocationsCompare()
    {
        Assert.IsTrue(new TextLocation(1, 2) < new TextLocation(1, 3));
        Assert.IsTrue(new TextLocation(2, 1) > new TextLocation(1, 99));
        Assert.IsTrue(new TextLocation(1, 1) >= new TextLocation(1, 1));
        Assert.IsTrue(TextLocation.Empty.IsEmpty);
        Assert.IsFalse(new TextLocation(1, 1).IsEmpty);
    }

    /// <summary>
    /// A marked tab stands two columns in for one character, so the visual column runs ahead of the
    /// document offset from the tab onwards.
    /// </summary>
    [TestMethod]
    public void AProjectionMovesTheVisualColumnAwayFromTheOffset()
    {
        var editor = CreateEditor("a\tb");
        var plain = FirstVisualLine(editor);
        Assert.AreEqual(2, plain.GetVisualColumn(2));
        Assert.AreEqual(3, plain.VisualLength);

        editor.Options.ShowTabs = true;
        Render(editor);
        var marked = FirstVisualLine(editor);

        Assert.AreEqual(3, marked.GetVisualColumn(2), "The marker column was not counted.");
        Assert.AreEqual(4, marked.VisualLength);
        Assert.AreEqual(2, marked.GetRelativeOffset(3), "The column did not map back to the document.");
    }

    /// <summary>A point taken from a position leads back to the same position.</summary>
    [TestMethod]
    public void APositionRoundTripsThroughItsVisualPosition()
    {
        var editor = CreateEditor("abc\ndefgh");
        var view = editor.TextArea.TextView;

        var point = view.GetVisualPosition(new TextViewPosition(2, 3), VisualYPosition.LineMiddle);
        var position = view.GetPosition(point);

        Assert.IsNotNull(position);
        Assert.AreEqual(2, position.Value.Line);
        Assert.AreEqual(3, position.Value.Column);
    }

    [TestMethod]
    public void TheRowIsWhereTheModeSaysItIs()
    {
        var editor = CreateEditor("abc\ndef");
        var view = editor.TextArea.TextView;
        var position = new TextViewPosition(2, 1);

        double top = view.GetVisualPosition(position, VisualYPosition.LineTop).Y;
        double middle = view.GetVisualPosition(position, VisualYPosition.LineMiddle).Y;
        double bottom = view.GetVisualPosition(position, VisualYPosition.LineBottom).Y;
        double baseline = view.GetVisualPosition(position, VisualYPosition.Baseline).Y;

        Assert.IsGreaterThan(top, middle);
        Assert.IsGreaterThan(middle, bottom);
        Assert.AreEqual((top + bottom) / 2, middle, 0.01);
        Assert.IsGreaterThan(top, baseline);
        Assert.IsGreaterThan(baseline, bottom);
    }

    /// <summary>
    /// A line outside the viewport answers the same as it does once scrolled into it. Every mode
    /// has to match, including the four that need the row's own baseline.
    /// </summary>
    [TestMethod]
    public void AnOffScreenLineAnswersTheSameAsAnOnScreenOne()
    {
        var editor = CreateEditor(string.Join('\n', Enumerable.Range(1, 80).Select(number => $"line {number}")));
        var view = editor.TextArea.TextView;
        var position = new TextViewPosition(70, 3);
        Assert.IsNull(view.GetVisualLineFromVisualTop(view.GetVisualPosition(position, VisualYPosition.LineTop).Y),
            "Line 70 was already on screen, so the test proves nothing.");

        var offScreen = Measure(view, position);
        editor.ScrollToLine(70);
        Render(editor);
        var onScreen = Measure(view, position);

        CollectionAssert.AreEqual(offScreen, onScreen);
    }

    private static double[] Measure(TextView view, TextViewPosition position)
        => Enum.GetValues<VisualYPosition>()
            .Select(mode => view.GetVisualPosition(position, mode))
            .SelectMany(point => new[] { point.X, point.Y })
            .ToArray();

    [TestMethod]
    public void APointBelowTheTextHasNoPosition()
    {
        var editor = CreateEditor("abc");

        Assert.IsNull(editor.TextArea.TextView.GetPosition(new Point(0, HEIGHT * 4)));
    }

    /// <summary>The editor takes a point in its own coordinates and translates it for the view.</summary>
    [TestMethod]
    public void TheEditorTakesItsOwnCoordinates()
    {
        var editor = CreateEditor("abc\ndef");
        var viewport = editor.Surface.TextViewportBounds;
        var documentPoint = editor.TextArea.TextView.GetVisualPosition(
            new TextViewPosition(2, 2), VisualYPosition.LineMiddle);

        var position = editor.GetPositionFromPoint(
            new Point(documentPoint.X + viewport.X, documentPoint.Y + viewport.Y));

        Assert.IsNotNull(position);
        Assert.AreEqual(new TextLocation(2, 2), position.Value.Location);
    }

    private static VisualLine FirstVisualLine(TextEditor editor)
    {
        var line = editor.TextArea.TextView.GetVisualLineFromVisualTop(0);
        Assert.IsNotNull(line, "The first line was not laid out.");
        return line;
    }

    private static TextEditor CreateEditor(string text)
    {
        var editor = new TextEditor { Text = text, ShowLineNumbers = false, SkipViewportCull = true };
        Render(editor);
        return editor;
    }

    private static void Render(TextEditor editor)
    {
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

        var factory = Application.DefaultGraphicsFactory;
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(WIDTH, HEIGHT, 1));
        using var context = factory.CreateContext(surface);
        context.BeginFrame(surface);
        editor.Render(context);
        context.EndFrame();
    }
}
