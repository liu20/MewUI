using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// Which of a selection's two positions the caret sits at is the direction it was made in. It
/// decides where replacing the selection leaves the caret, and a rectangular selection grows along
/// it, so it has to survive both the trip to the editing surface and the trip back.
/// </summary>
[TestClass]
public sealed class SelectionDirectionTests
{
    private static TextEditor CreateEditor() => new() { Text = "one two three" };

    [TestMethod]
    public void ASelectionExtendedBackwardsReadsBackwards()
    {
        var editor = CreateEditor();

        editor.MoveCaret(8, extendSelection: false);
        editor.MoveCaret(4, extendSelection: true);

        var selection = editor.TextArea.Selection;
        Assert.AreEqual(8, editor.Document.GetOffset(
            selection.StartPosition.Line, selection.StartPosition.Column), "The anchor is the start.");
        Assert.AreEqual(4, editor.Document.GetOffset(
            selection.EndPosition.Line, selection.EndPosition.Column), "The caret is the end.");
    }

    [TestMethod]
    public void ASelectionExtendedForwardsReadsForwards()
    {
        var editor = CreateEditor();

        editor.MoveCaret(4, extendSelection: false);
        editor.MoveCaret(8, extendSelection: true);

        var selection = editor.TextArea.Selection;
        Assert.AreEqual(4, editor.Document.GetOffset(
            selection.StartPosition.Line, selection.StartPosition.Column));
        Assert.AreEqual(8, editor.Document.GetOffset(
            selection.EndPosition.Line, selection.EndPosition.Column));
    }

    [TestMethod]
    public void TheSegmentReadsForwardsEitherWay()
    {
        var editor = CreateEditor();

        editor.MoveCaret(8, extendSelection: false);
        editor.MoveCaret(4, extendSelection: true);

        var segment = editor.TextArea.Selection.SurroundingSegment;
        Assert.IsNotNull(segment);
        Assert.AreEqual(4, segment.Offset);
        Assert.AreEqual(4, segment.Length);
    }

    [TestMethod]
    public void AnAssignedBackwardsSelectionSurvivesTheSurface()
    {
        var editor = CreateEditor();
        var area = editor.TextArea;

        area.Selection = Selection.Create(area, 8, 4);
        // Any surface-side change rebuilds the selection from the caret and anchor, so this is where
        // a direction that never reached the surface would be lost.
        editor.MoveCaret(4, extendSelection: true);

        var selection = area.Selection;
        Assert.AreEqual(8, editor.Document.GetOffset(
            selection.StartPosition.Line, selection.StartPosition.Column));
        Assert.AreEqual(4, editor.Document.GetOffset(
            selection.EndPosition.Line, selection.EndPosition.Column));
        Assert.AreEqual(4, editor.CaretOffset, "The caret stayed at the end the selection was made from.");
    }

    [TestMethod]
    public void AssigningASelectionPutsTheCaretAtItsEnd()
    {
        var editor = CreateEditor();
        var area = editor.TextArea;

        area.Selection = Selection.Create(area, 4, 8);

        Assert.AreEqual(8, editor.CaretOffset);
        Assert.AreEqual(4, editor.SelectionStart);
        Assert.AreEqual(4, editor.SelectionLength);
    }

    [TestMethod]
    public void CollapsingTheSelectionLeavesNoDirection()
    {
        var editor = CreateEditor();
        editor.MoveCaret(8, extendSelection: false);
        editor.MoveCaret(4, extendSelection: true);

        editor.MoveCaret(4, extendSelection: false);

        Assert.IsTrue(editor.TextArea.Selection.IsEmpty);
    }
}
