using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The editing surface's own scope routes undo to its TextBase history; the editor rebinds it to
/// the document's UndoStack so the command path (menus, toolbars) and the keyboard path undo the
/// same history.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EditorCommandRoutingTests
{
    [TestMethod]
    public void TheCommandPathUndoDrivesTheDocumentUndoStack()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var window = ScaledWindow.Create(1.0);
        var editor = new TextEditor { Text = "hello" };
        window.Content = editor;
        window.PerformLayout();
        window.FocusManager.SetFocus(editor.Surface);

        editor.Document.Insert(0, "x");
        Assert.AreEqual("xhello", editor.Text);
        Assert.IsTrue(editor.Document.UndoStack.CanUndo);

        bool undone = window.CommandRouter.ExecuteAsync(StandardCommands.Undo).GetAwaiter().GetResult();

        Assert.IsTrue(undone, "The router found no undo handler for the focused editor.");
        Assert.AreEqual("hello", editor.Text);
        Assert.IsTrue(editor.Document.UndoStack.CanRedo,
            "The document's stack must have performed the undo; a redo living elsewhere means the surface history took it.");

        bool redone = window.CommandRouter.ExecuteAsync(StandardCommands.Redo).GetAwaiter().GetResult();

        Assert.IsTrue(redone);
        Assert.AreEqual("xhello", editor.Text);
    }
}
