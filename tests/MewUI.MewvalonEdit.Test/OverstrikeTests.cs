using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Overstrike mode types over what is in front of the caret instead of pushing it along, except
/// where there is nothing to take the place of: the end of a line, and a line ending itself.
/// Driven through the keyboard path, which is the one a reader actually types through.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class OverstrikeTests
{
    [TestMethod]
    public void TypingTakesThePlaceOfTheCharacterInFront()
    {
        var editor = CreateEditor("abc");
        editor.TextArea.OverstrikeMode = true;
        editor.CaretOffset = 0;

        Type(editor, "X");

        Assert.AreEqual("Xbc", editor.Text);
        Assert.AreEqual(1, editor.CaretOffset);
    }

    [TestMethod]
    public void TypingAtTheEndOfALineStillInserts()
    {
        var editor = CreateEditor("ab\ncd");
        editor.TextArea.OverstrikeMode = true;
        editor.CaretOffset = 2;

        Type(editor, "X");

        Assert.AreEqual("abX\ncd", editor.Text, "there is nothing at the end of a line to take the place of");
    }

    [TestMethod]
    public void ALineEndingIsAlwaysInserted()
    {
        var editor = CreateEditor("abc");
        editor.TextArea.OverstrikeMode = true;
        editor.CaretOffset = 1;

        Type(editor, "\n");

        Assert.AreEqual("a\nbc", editor.Text);
    }

    /// <summary>
    /// The keystroke edits a range without ever holding a selection, so undo returns to the bare
    /// caret the reader had, not to a one-character selection nobody made.
    /// </summary>
    [TestMethod]
    public void UndoReturnsToTheCaretAndRedoLandsAfterTheCharacter()
    {
        var editor = CreateEditor("abc");
        editor.Document.UndoStack.ClearAll();
        editor.TextArea.OverstrikeMode = true;
        editor.CaretOffset = 0;
        Type(editor, "X");
        Assert.AreEqual("Xbc", editor.Text);

        editor.Document.UndoStack.Undo();
        Assert.AreEqual("abc", editor.Text);
        Assert.AreEqual(0, editor.CaretOffset);
        Assert.AreEqual(0, editor.SelectionLength, "undo brought back a selection nobody made");

        editor.Document.UndoStack.Redo();
        Assert.AreEqual("Xbc", editor.Text);
        Assert.AreEqual(1, editor.CaretOffset, "redo belongs after the typed character");
    }

    [TestMethod]
    public void InsertTogglesTheModeOnlyWhenTheOptionsAllowIt()
    {
        var editor = CreateEditor("abc");

        Press(editor, Key.Insert);
        Assert.IsFalse(editor.TextArea.OverstrikeMode, "the switch is off until a host asks for it");

        editor.Options.AllowToggleOverstrikeMode = true;
        Press(editor, Key.Insert);
        Assert.IsTrue(editor.TextArea.OverstrikeMode);

        Press(editor, Key.Insert);
        Assert.IsFalse(editor.TextArea.OverstrikeMode);
    }

    private static void Type(TextEditor editor, string text)
        => ((ITextInputClient)editor.Surface).HandleTextInput(new TextInputEventArgs(text));

    private static void Press(TextEditor editor, Key key)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, ModifierKeys.None));

    private static TextEditor CreateEditor(string text)
    {
        var editor = new TextEditor { Text = text, SkipViewportCull = true };
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        return editor;
    }

    [TestMethod]
    public void TurningImeSupportOffDisablesCompositionOnTheSurface()
    {
        var editor = CreateEditor("abc");
        Assert.AreEqual(ImeMode.Auto, editor.Surface.ImeMode, "composition is available unless a host turns it off");

        editor.Options.EnableImeSupport = false;

        Assert.AreEqual(ImeMode.Disabled, editor.Surface.ImeMode);
    }

    [TestMethod]
    public void TypingTakesThePointerOutOfTheWay()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abc");
        Assert.IsTrue(editor.Options.HideCursorWhileTyping, "the original hides it by default");

        editor.Options.HideCursorWhileTyping = false;
        Type(editor, "X");

        Assert.AreNotEqual(CursorType.None, editor.Surface.Cursor,
            "the pointer went away with the option turned off");
    }
}
