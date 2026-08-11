using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A box selection walks the caret past the end of the lines it crosses. Those columns hold no
/// characters, so the document offset stays at the line's end and only the visual column says where
/// the caret is; drawing it from the offset alone would snap it back into the text.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CaretVirtualSpaceTests
{
    private const string TEXT = "a long first line with plenty of text\n\nthird";
    private const int CARET_IN_LONG_LINE = 30;

    [TestMethod]
    public void TheCaretIsDrawnPastTheEndOfAShorterLine()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor, layer) = CreateHost();
        double startX = layer.GetCaretRectangle(editor.Surface).X;

        // Onto the empty line, then onto the short one.
        StepDown(window, editor);
        double emptyLineX = layer.GetCaretRectangle(editor.Surface).X;
        StepDown(window, editor);
        double shortLineX = layer.GetCaretRectangle(editor.Surface).X;

        Assert.AreEqual(CARET_IN_LONG_LINE, editor.TextArea.Caret.Position.VisualColumn,
            "the box selection did not keep the column it started from");
        Assert.AreEqual(startX, emptyLineX, 1.0, "the caret was drawn at the start of the empty line");
        Assert.AreEqual(startX, shortLineX, 1.0, "the caret was drawn at the end of the short line's text");
    }

    /// <summary>Without virtual space the caret is where the text ends, and drawing follows that.</summary>
    [TestMethod]
    public void AnOrdinaryMoveDrawsTheCaretWhereTheTextEnds()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor, layer) = CreateHost();
        double startX = layer.GetCaretRectangle(editor.Surface).X;

        CaretNavigationCommandHandler.MoveCaret(editor.TextArea, CaretMovementType.LineDown);
        window.PerformLayout();

        Assert.IsLessThan(startX, layer.GetCaretRectangle(editor.Surface).X,
            "an ordinary move left the caret hanging past the empty line");
    }

    /// <summary>
    /// A rectangle edits every line it crosses, so each of them carries a caret, all in the same
    /// column the box was walked to.
    /// </summary>
    [TestMethod]
    public void EveryLineOfARectangleCarriesACaret()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor, layer) = CreateHost();
        Assert.ContainsSingle(layer.GetCaretRectangles(editor.Surface),
            "an editor with no rectangle drew more than its one caret");

        StepDown(window, editor);
        StepDown(window, editor);

        var carets = layer.GetCaretRectangles(editor.Surface).ToArray();
        Assert.HasCount(3, carets, "the box crossed three lines but not every line got a caret");
        foreach (var caret in carets)
        {
            Assert.AreEqual(carets[0].Rectangle.X, caret.Rectangle.X, 1.0,
                "the carets did not line up under one another");
        }
        Assert.AreEqual(3, carets.Select(static caret => caret.Rectangle.Y).Distinct().Count(),
            "the carets landed on top of each other instead of one per line");
        Assert.ContainsSingle(carets.Where(static caret => caret.Primary),
            "exactly one caret is the one the reader is driving");
        Assert.AreEqual(editor.CaretOffset, editor.TextArea.Caret.Offset);
    }

    private static void StepDown(Window window, TextEditor editor)
    {
        CaretNavigationCommandHandler.MoveCaretBoxSelection(editor.TextArea, CaretMovementType.LineDown);
        window.PerformLayout();
    }

    private static (Window Window, TextEditor Editor, CaretLayer Layer) CreateHost()
    {
        var window = ScaledWindow.Create(1.0, 800, 300);
        var editor = new TextEditor
        {
            Text = TEXT,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();
        editor.CaretOffset = CARET_IN_LONG_LINE;
        window.PerformLayout();
        return (window, editor, new CaretLayer(editor.TextArea));
    }
}
