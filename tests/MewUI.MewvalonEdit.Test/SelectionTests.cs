using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// A selection is a value: changing one produces another. These pin what each of the two kinds
/// answers and how one survives an edit to the document under it.
/// </summary>
[TestClass]
public sealed class SelectionTests
{
    [TestMethod]
    public void AnEmptySelectionSelectsNothing()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var selection = editor.TextArea.Selection;

        Assert.IsTrue(selection.IsEmpty);
        Assert.AreEqual(0, selection.Length);
        Assert.IsEmpty(selection.Segments);
        Assert.IsNull(selection.SurroundingSegment);
        Assert.AreEqual(string.Empty, selection.GetText());
        Assert.IsFalse(selection.Contains(0));
    }

    [TestMethod]
    public void EqualOffsetsGiveTheEmptySelection()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        var selection = Selection.Create(editor.TextArea, 2, 2);

        Assert.IsInstanceOfType<EmptySelection>(selection);
        Assert.AreSame(editor.TextArea.Selection, selection, "There is one empty selection per area.");
    }

    [TestMethod]
    public void ASimpleSelectionReportsItsRange()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        var selection = Selection.Create(editor.TextArea, 1, 6);

        Assert.IsInstanceOfType<SimpleSelection>(selection);
        Assert.IsFalse(selection.IsEmpty);
        Assert.AreEqual(5, selection.Length);
        Assert.AreEqual("ne\ntw", selection.GetText());
        Assert.IsTrue(selection.IsMultiline);
        Assert.HasCount(1, selection.Segments);
        Assert.IsTrue(selection.Contains(1), "The border is included.");
        Assert.IsTrue(selection.Contains(6), "The border is included.");
        Assert.IsFalse(selection.Contains(0));
    }

    [TestMethod]
    public void AssigningASelectionMovesTheEditingSurface()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        editor.TextArea.Selection = Selection.Create(editor.TextArea, 1, 5);

        Assert.AreEqual(1, editor.SelectionStart);
        Assert.AreEqual(4, editor.SelectionLength);
        Assert.AreEqual("ne\nt", editor.SelectedText);
    }

    /// <summary>A selection the surface makes on its own is what the text area then reports.</summary>
    [TestMethod]
    public void ASurfaceSelectionBecomesTheTextAreaSelection()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        editor.Select(2, 3);

        Assert.AreEqual(3, editor.TextArea.Selection.Length);
        Assert.AreEqual("e\nt", editor.TextArea.Selection.GetText());
    }

    [TestMethod]
    public void MovingTheEndpointKeepsTheStart()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var selection = Selection.Create(editor.TextArea, 1, 3);

        var moved = selection.SetEndpoint(new TextViewPosition(editor.Document.GetLocation(6)));

        Assert.AreEqual(1, ((ISegment)moved.SurroundingSegment!).Offset);
        Assert.AreEqual(5, moved.Length);
        Assert.AreEqual(2, selection.Length, "The original selection is unchanged.");
    }

    [TestMethod]
    public void AnEmptySelectionHasNoEndpointToMove()
    {
        var editor = new TextEditor { Text = "one" };

        Assert.ThrowsExactly<NotSupportedException>(
            () => editor.TextArea.Selection.SetEndpoint(new TextViewPosition(1, 2)));
    }

    [TestMethod]
    public void AnEmptySelectionStartsOneInstead()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        var started = editor.TextArea.Selection.StartSelectionOrSetEndpoint(
            new TextViewPosition(editor.Document.GetLocation(1)),
            new TextViewPosition(editor.Document.GetLocation(3)));

        Assert.IsInstanceOfType<SimpleSelection>(started);
        Assert.AreEqual(2, started.Length);
    }

    /// <summary>
    /// Text inserted before the selection pushes it along; text inserted at its end lands outside
    /// it, which is why the end does not follow an insertion the way the start does.
    /// </summary>
    [TestMethod]
    public void ASelectionCarriesAcrossAnEdit()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var selection = Selection.Create(editor.TextArea, 4, 7);
        Selection? moved = null;
        // The offsets are worked out against the document as it is after the change, so the real
        // event is what this has to run on.
        editor.Document.Changed += (_, e) => moved = selection.UpdateOnDocumentChange(e);

        editor.Document.Insert(0, "xx");

        Assert.IsNotNull(moved);
        Assert.AreEqual(6, ((ISegment)moved.SurroundingSegment!).Offset);
        Assert.AreEqual(3, moved.Length);
    }

    [TestMethod]
    public void SelectionsOfTheSameRangeAreEqual()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        Assert.AreEqual(Selection.Create(editor.TextArea, 1, 4), Selection.Create(editor.TextArea, 1, 4));
        Assert.AreNotEqual(Selection.Create(editor.TextArea, 1, 4), Selection.Create(editor.TextArea, 1, 5));
    }

    /// <summary>Both ends are ordered, so a backwards range still reads forwards.</summary>
    [TestMethod]
    public void ASegmentOrdersItsEnds()
    {
        var segment = new SelectionSegment(9, 2);

        Assert.AreEqual(2, segment.StartOffset);
        Assert.AreEqual(9, segment.EndOffset);
        Assert.AreEqual(7, segment.Length);
        Assert.AreEqual(-1, segment.StartVisualColumn, "Offsets alone leave the columns unknown.");
    }

    [TestMethod]
    public void ReplacingASelectionLeavesTheReplacementBehind()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var selection = Selection.Create(editor.TextArea, 0, 3);

        selection.ReplaceSelectionWithText("ONE");

        Assert.AreEqual("ONE\ntwo", editor.Text);
    }
}
