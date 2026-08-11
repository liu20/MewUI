using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// What counts as the original file. A wholesale text assignment drops the undo history in this
/// port, so the text it leaves is the only state there is to return to and it counts as original;
/// the original marks it modified because there an assignment is an ordinary undoable edit.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ModifiedTrackingTests
{
    [TestMethod]
    public void AnEditModifiesTheDocumentEvenBeforeAnyoneReadsTheUndoStack()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef");
        Assert.IsFalse(editor.IsModified, "assigned text is the document's own starting state");

        editor.Document.Replace(0, 1, "X");

        Assert.IsTrue(editor.IsModified, "the edit went uncounted, so the marker never left the original");
    }

    [TestMethod]
    public void LoadingLeavesTheDocumentUnmodified()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef");
        editor.Document.Replace(0, 1, "X");
        Assert.IsTrue(editor.IsModified);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("brand new document"));
        editor.Load(stream);

        Assert.AreEqual("brand new document", editor.Text);
        Assert.IsFalse(editor.IsModified, "what was just read off disk presented as modified");
        Assert.IsFalse(editor.Document.UndoStack.CanUndo, "the load left the previous document's history behind");

        editor.Document.Replace(0, 1, "Z");
        Assert.IsTrue(editor.IsModified, "editing the loaded document did not mark it modified");
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
