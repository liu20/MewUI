using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The Alt+Shift movement keys grow a rectangular selection. The selection is converted to a
/// rectangle before the caret moves, which is what turns virtual space on for the movement, and
/// the caret then drags the rectangle's active corner along.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BoxSelectionKeyTests
{
    private const ModifierKeys ALT_SHIFT = ModifierKeys.Alt | ModifierKeys.Shift;

    private static TextEditor CreateEditor(string text)
    {
        var editor = new TextEditor
        {
            Text = text,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        return editor;
    }

    private static void Press(TextEditor editor, Key key, ModifierKeys modifiers)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, modifiers));

    [TestMethod]
    public void AltShiftRightStartsARectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl");

        Press(editor, Key.Right, ALT_SHIFT);

        var selection = editor.TextArea.Selection;
        Assert.IsInstanceOfType<RectangleSelection>(selection);
        var segments = selection.Segments.ToArray();
        Assert.HasCount(1, segments);
        Assert.AreEqual(0, segments[0].StartVisualColumn);
        Assert.AreEqual(1, segments[0].EndVisualColumn);
    }

    [TestMethod]
    public void AltShiftDownSpansLinesAtTheSameColumns()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.CaretOffset = 2;

        Press(editor, Key.Right, ALT_SHIFT);
        Press(editor, Key.Down, ALT_SHIFT);

        var segments = editor.TextArea.Selection.Segments.ToArray();
        Assert.HasCount(2, segments);
        foreach (var segment in segments)
        {
            Assert.AreEqual(2, segment.StartVisualColumn);
            Assert.AreEqual(3, segment.EndVisualColumn);
        }
    }

    [TestMethod]
    public void AltShiftRightWalksIntoVirtualSpace()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nab");
        var document = editor.Document;
        editor.CaretOffset = document.GetOffset(2, 3);

        Press(editor, Key.Right, ALT_SHIFT);

        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
        Assert.AreEqual(3, editor.TextArea.Caret.VisualColumn,
            "The caret must step past the line end into virtual space.");
        Assert.AreEqual(document.GetOffset(2, 3), editor.CaretOffset,
            "The surface offset stays clamped at the line end.");
    }

    [TestMethod]
    public void BoxCharLeftAtColumnZeroStaysPut()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl");
        editor.CaretOffset = editor.Document.GetOffset(2, 1);

        Press(editor, Key.Left, ALT_SHIFT);

        Assert.AreEqual(editor.Document.GetOffset(2, 1), editor.CaretOffset,
            "Box CharLeft at column zero must not move to the previous line.");
    }

    [TestMethod]
    public void BoxWordRightStopsAtTheNextWordStart()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("foo bar baz");

        Press(editor, Key.Right, ModifierKeys.Control | ALT_SHIFT);

        Assert.AreEqual(4, editor.TextArea.Caret.VisualColumn, "Ctrl+Alt+Shift+Right must stop at 'bar'.");
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
        Assert.AreEqual(4, editor.TextArea.Selection.Segments.Single().Length);
    }

    [TestMethod]
    public void AltShiftEndReachesTheLineEnd()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl");
        editor.CaretOffset = 2;

        Press(editor, Key.End, ALT_SHIFT);

        var segment = editor.TextArea.Selection.Segments.Single();
        Assert.AreEqual(2, segment.StartVisualColumn);
        Assert.AreEqual(6, segment.EndVisualColumn);
    }

    [TestMethod]
    public void MovingDownThroughAShortLineKeepsTheColumn()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nab\nmnopqr");
        editor.CaretOffset = 4;

        Press(editor, Key.Down, ALT_SHIFT);
        Assert.AreEqual(4, editor.TextArea.Caret.VisualColumn,
            "The short line must answer with a virtual column, not its line end.");

        Press(editor, Key.Down, ALT_SHIFT);
        Assert.AreEqual(4, editor.TextArea.Caret.VisualColumn,
            "The desired x survives crossing the short line.");
        var segments = editor.TextArea.Selection.Segments.ToArray();
        Assert.HasCount(3, segments);
        Assert.AreEqual(4, segments[1].EndVisualColumn, "The short line's segment runs to the virtual column.");
    }
}
