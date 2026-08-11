using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.Platform;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Editing keys over a rectangle drive the rectangle rather than the surface's empty selection:
/// typing writes every covered line, Backspace and Delete clear the column, Ctrl+C/X read the
/// column text, and Ctrl+V distributes a block. Without a rectangle every key declines and the
/// surface behaves as always.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RectangleEditingTests
{
    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; set; }

        public bool TrySetText(string text)
        {
            Text = text;
            return true;
        }

        public bool TryGetText(out string text)
        {
            text = Text ?? string.Empty;
            return Text is not null;
        }
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

    private static RectangleSelection SelectBlock(TextEditor editor)
    {
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));
        editor.TextArea.Selection = selection;
        return selection;
    }

    private static void Press(TextEditor editor, Key key, ModifierKeys modifiers = ModifierKeys.None)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, modifiers));

    [TestMethod]
    public void TypedTextGoesThroughTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        SelectBlock(editor);

        editor.TextArea.PerformTextInput("X");

        Assert.AreEqual("abXef\nghXkl\nmnXqr", editor.Text.ReplaceLineEndings("\n"));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
    }

    [TestMethod]
    public void BackspaceClearsTheColumnAndKeepsTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        SelectBlock(editor);

        Press(editor, Key.Backspace);

        Assert.AreEqual("abef\nghkl\nmnqr", editor.Text.ReplaceLineEndings("\n"));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection,
            "Deleting the column must leave the collapsed rectangle for the next keystroke.");
    }

    [TestMethod]
    public void CtrlCCopiesTheColumnText()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var clipboard = new FakeClipboard();
        editor.Surface.ClipboardService = clipboard;
        SelectBlock(editor);

        Press(editor, Key.C, ModifierKeys.Control);

        Assert.AreEqual("cd\nij\nop", clipboard.Text?.ReplaceLineEndings("\n"));
        Assert.AreEqual("abcdef\nghijkl\nmnopqr", editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void CtrlXCutsTheColumn()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var clipboard = new FakeClipboard();
        editor.Surface.ClipboardService = clipboard;
        SelectBlock(editor);

        Press(editor, Key.X, ModifierKeys.Control);

        Assert.AreEqual("cd\nij\nop", clipboard.Text?.ReplaceLineEndings("\n"));
        Assert.AreEqual("abef\nghkl\nmnqr", editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void CtrlVDistributesABlock()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var clipboard = new FakeClipboard { Text = "11\n22\n33" };
        editor.Surface.ClipboardService = clipboard;
        SelectBlock(editor);

        Press(editor, Key.V, ModifierKeys.Control);

        Assert.AreEqual("ab11ef\ngh22kl\nmn33qr", editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void TheCommandPathCopyReadsTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var window = ScaledWindow.Create(1.0);
        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        window.Content = editor;
        window.PerformLayout();
        var clipboard = new FakeClipboard();
        editor.Surface.ClipboardService = clipboard;
        window.FocusManager.SetFocus(editor.Surface);
        SelectBlock(editor);

        bool copied = window.CommandRouter.ExecuteAsync(Aprillz.MewUI.StandardCommands.Copy)
            .GetAwaiter().GetResult();

        Assert.IsTrue(copied);
        Assert.AreEqual("cd\nij\nop", clipboard.Text?.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void WithoutARectangleTheKeysDecline()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef");
        var args = new KeyEventArgs(Key.Backspace, platformKey: 0, ModifierKeys.None);

        editor.TextArea.HandleKeyDown(args);

        Assert.IsFalse(args.Handled, "Backspace must fall through to the surface without a rectangle.");
    }
}
