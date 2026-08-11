using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A wholesale text assignment is unrecorded, so the core hands over the removal length without
/// materializing the removed text. The change still has to carry that length: every offset in the
/// document crosses the whole of it, and a rectangle selection that maps its corners across a
/// change reporting no removal lands past the end of the new text.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentTextAssignmentTests
{
    [TestMethod]
    public void AssignmentReportsTheRemovalLengthItDidNotMaterialize()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = CreateEditor("0123456789");
        DocumentChangeEventArgs? captured = null;
        editor.Document.Changed += (_, e) => captured = e;

        editor.Text = "ab";

        Assert.IsNotNull(captured);
        Assert.AreEqual(10, captured.RemovalLength, "the change reported no removal, so offsets shift by the insertion alone");
        Assert.AreEqual(2, captured.InsertionLength);
        Assert.AreEqual(2, captured.GetNewOffset(7), "an offset inside the removed range must land in the new text");
    }

    [TestMethod]
    public void AssigningTextDropsTheSelectionItPointedInto()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = CreateEditor(string.Join('\n', Enumerable.Repeat("0123456789", 40)));
        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(2, 2, 1), new TextViewPosition(6, 6, 5));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);

        editor.Text = "short";

        Assert.IsNotInstanceOfType<RectangleSelection>(editor.TextArea.Selection,
            "the rectangle survived a load and now points into text that no longer exists");
        Assert.IsTrue(editor.TextArea.Selection.IsEmpty, "the selection survived a load");
        Assert.IsEmpty(editor.TextArea.Selection.Segments);
    }

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
}
