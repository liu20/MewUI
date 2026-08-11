using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// An IME composed over a rectangle selection: the preedit rides the active corner while the reader
/// composes, and each committed syllable is what the rectangle writes to every line. The events run
/// in the order the Win32 backend sends them, where a keystroke that commits one syllable and opens
/// the next delivers the composition update before the committed text.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ImeRectangleCompositionTests
{
    [TestMethod]
    public void EachCommittedSyllableWritesEveryLineOfTheRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = HostWithRectangle(out _);
        var composition = (ITextCompositionClient)editor.Surface;
        var input = (ITextInputClient)editor.Surface;

        composition.HandleTextCompositionStart(new TextCompositionEventArgs());
        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("ㅇ"));
        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("안"));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection,
            "the preedit dissolved the rectangle it was composed over");
        Assert.AreEqual("abc\ndef\ng안hi", Text(editor), "the preedit belongs on the corner line alone");

        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("ㄴ"));
        input.HandleTextInput(new TextInputEventArgs("안"));
        Assert.AreEqual("a안bc\nd안ef\ng안hi", Text(editor), "a committed syllable writes every line");

        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("녕"));
        input.HandleTextInput(new TextInputEventArgs("녕"));
        composition.HandleTextCompositionEnd(new TextCompositionEventArgs());

        Assert.AreEqual("a안녕bc\nd안녕ef\ng안녕hi", Text(editor));
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection,
            "the rectangle should outlive the composition for the next syllable");
    }

    /// <summary>
    /// The other way a platform delivers a commit: the preedit standing in the document is what is
    /// committed, with no text input of its own. The rectangle has to write every line for that
    /// shape as well, or the syllable lands on the corner line only.
    /// </summary>
    [TestMethod]
    public void ACommittedPreeditWritesEveryLineToo()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = HostWithRectangle(out _);
        var composition = (ITextCompositionClient)editor.Surface;

        composition.HandleTextCompositionStart(new TextCompositionEventArgs());
        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("안"));
        Assert.AreEqual("abc\ndef\ng안hi", Text(editor), "the preedit belongs on the corner line alone");

        ((ITextCompositionEditor)editor.Surface).CommitActiveComposition();

        Assert.AreEqual("a안bc\nd안ef\ng안hi", Text(editor),
            "the committed preedit landed on the corner line only");
        Assert.IsFalse(composition.IsComposing);
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);
    }

    /// <summary>
    /// Composing without a rectangle stays exactly what the surface does on its own: one line,
    /// preedit replaced by the committed text.
    /// </summary>
    [TestMethod]
    public void ComposingWithoutARectangleIsUntouched()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var editor = HostWithRectangle(out var window);
        editor.TextArea.ClearSelection();
        editor.CaretOffset = 1;
        window.PerformLayout();
        var composition = (ITextCompositionClient)editor.Surface;
        var input = (ITextInputClient)editor.Surface;

        composition.HandleTextCompositionStart(new TextCompositionEventArgs());
        composition.HandleTextCompositionUpdate(new TextCompositionEventArgs("안"));
        input.HandleTextInput(new TextInputEventArgs("안"));
        composition.HandleTextCompositionEnd(new TextCompositionEventArgs());

        Assert.AreEqual("a안bc\ndef\nghi", Text(editor));
    }

    private static string Text(TextEditor editor) => editor.Text.ReplaceLineEndings("\n");

    private static TextEditor HostWithRectangle(out Window window)
    {
        window = ScaledWindow.Create(1.0, 800, 300);
        var editor = new TextEditor
        {
            Text = "abc\ndef\nghi",
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();
        editor.CaretOffset = 1;
        window.PerformLayout();
        CaretNavigationCommandHandler.MoveCaretBoxSelection(editor.TextArea, CaretMovementType.LineDown);
        CaretNavigationCommandHandler.MoveCaretBoxSelection(editor.TextArea, CaretMovementType.LineDown);
        window.PerformLayout();
        return editor;
    }
}
