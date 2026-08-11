using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// While a rectangle is active the extension owns the selection: the surface keeps an empty
/// selection with the caret on the rectangle's active corner, and only a surface change that is
/// not that bookkeeping dissolves the rectangle. Before this, every caret move re-derived a simple
/// selection and a rectangle could not survive its own assignment.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SelectionOwnershipTests
{
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

    [TestMethod]
    public void AnAssignedRectangleSurvivesAndTheSurfaceStaysEmpty()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var rectangle = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        editor.TextArea.Selection = rectangle;

        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
        Assert.AreEqual(0, editor.SelectionLength, "The surface must hold no flattened block.");
        Assert.AreEqual(
            editor.Document.GetOffset(3, 5), editor.CaretOffset,
            "The caret follows the rectangle's active corner.");
    }

    [TestMethod]
    public void TypingOverTheRectangleKeepsIt()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        ((RectangleSelection)editor.TextArea.Selection).ReplaceSelectionWithText("XY");

        Assert.AreEqual("abXYef\nghXYkl\nmnXYqr", editor.Text.ReplaceLineEndings("\n"));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection,
            "Typing over a rectangle must leave a rectangle for the next keystroke.");
    }

    [TestMethod]
    public void APlainCaretMoveDissolvesTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        editor.CaretOffset = 0;

        Assert.IsNotInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
        Assert.IsTrue(editor.TextArea.Selection.IsEmpty);
    }

    [TestMethod]
    public void ASurfaceRangeSelectionDissolvesTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        editor.Select(0, 3);

        Assert.IsInstanceOfType<SimpleSelection>(editor.TextArea.Selection);
        Assert.AreEqual(3, editor.TextArea.Selection.Length);
    }

    [TestMethod]
    public void ClearSelectionDissolvesTheRectangleInPlace()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        // The caret stays on the corner, which is exactly the state the keep-rule allows; an
        // explicit clear must still win over it.
        editor.TextArea.ClearSelection();

        Assert.IsTrue(editor.TextArea.Selection.IsEmpty);
    }

    [TestMethod]
    public void AnEditAboveTheRectangleCarriesItAlong()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(2, 3, 2), new TextViewPosition(3, 5, 4));

        editor.Document.Insert(0, "zz\n");

        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
        var segments = editor.TextArea.Selection.Segments.ToArray();
        Assert.HasCount(2, segments);
        Assert.AreEqual(
            editor.Document.GetOffset(3, 3), segments[0].StartOffset,
            "The rectangle must have moved one line down with the insertion above it.");
    }

    [TestMethod]
    public void SwitchingTheDocumentDropsTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        // The rectangle's offsets belong to the old document; keeping it alive would hand them to
        // the selection layer against the new, shorter one.
        editor.Document = new Aprillz.MewUI.MewvalonEdit.Document.TextDocument("ab");
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));

        Assert.IsTrue(editor.TextArea.Selection.IsEmpty);
        Assert.IsEmpty(editor.TextArea.Selection.Segments.ToArray());
    }

    [TestMethod]
    public void TheCaretRemembersAVirtualColumnWhileOnItsOffset()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nab");
        var caret = editor.TextArea.Caret;

        // Column 3 is the end of "ab"; visual column 6 is virtual space past it.
        caret.Position = new TextViewPosition(2, 3, 6);
        Assert.AreEqual(6, caret.VisualColumn);

        caret.Offset = 0;
        Assert.AreEqual(0, caret.VisualColumn, "Leaving the offset must drop the virtual column.");
    }
}
