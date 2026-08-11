using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// How typed text reaches the document: the keyboard and <see cref="TextArea.PerformTextInput"/>
/// land in the same handling, a caret in virtual space grows the columns it stands in, and each
/// piece of the pipeline sees an input exactly once.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class TextInputWiringTests
{
    [TestMethod]
    public void TypingIntoVirtualSpaceGrowsTheSpacesBetween()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor) = Host("a long first line with plenty of text\n\nthird");
        editor.Options.EnableVirtualSpace = true;
        // With tabs allowed the padding tab-fills an empty line, as the original does; spaces make
        // the column the padding reaches directly assertable.
        editor.Options.ConvertTabsToSpaces = true;
        editor.CaretOffset = 30;
        window.PerformLayout();
        CaretNavigationCommandHandler.MoveCaret(editor.TextArea, CaretMovementType.LineDown);
        window.PerformLayout();
        Assert.AreEqual(30, editor.TextArea.Caret.Position.VisualColumn, "the caret was expected in virtual space");

        Type(editor, "X");

        string emptyLine = editor.Text.Split('\n')[1];
        Assert.AreEqual(new string(' ', 30) + "X", emptyLine, "the columns the caret stood in were never created");
        Assert.AreEqual(editor.Document.GetLineByNumber(2).EndOffset, editor.CaretOffset);
    }

    [TestMethod]
    public void WithoutVirtualSpaceTypingStaysAtTheLineEnd()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor) = Host("ab\n\ncd");
        editor.CaretOffset = 3;
        window.PerformLayout();

        Type(editor, "X");

        Assert.AreEqual("ab\nX\ncd", editor.Text.ReplaceLineEndings("\n"));
    }

    /// <summary>The parity PerformTextInput promises: a keystroke produces the same document.</summary>
    [TestMethod]
    public void AKeystrokeAndAProgrammaticInputWriteTheSameRectangle()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        string byKeyboard = TypeOverRectangle(static editor => Type(editor, "Z"));
        string byCall = TypeOverRectangle(static editor => editor.TextArea.PerformTextInput("Z"));

        Assert.AreEqual("aZbc\ndZef\ngZhi", byKeyboard.ReplaceLineEndings("\n"));
        Assert.AreEqual(byKeyboard, byCall, "the two entry points wrote different documents");
    }

    /// <summary>A tab is claimed by the conversion and must not be padded or converted twice.</summary>
    [TestMethod]
    public void ATabConvertsToSpacesExactlyOnce()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor) = Host("ab");
        editor.Options.ConvertTabsToSpaces = true;
        editor.CaretOffset = 0;
        window.PerformLayout();

        Type(editor, "\t");

        Assert.AreEqual(new string(' ', editor.Options.IndentationSize) + "ab", editor.Text);
    }

    private static string TypeOverRectangle(Action<TextEditor> input)
    {
        var (window, editor) = Host("abc\ndef\nghi");
        editor.CaretOffset = 1;
        window.PerformLayout();
        CaretNavigationCommandHandler.MoveCaretBoxSelection(editor.TextArea, CaretMovementType.LineDown);
        CaretNavigationCommandHandler.MoveCaretBoxSelection(editor.TextArea, CaretMovementType.LineDown);
        window.PerformLayout();
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);

        input(editor);
        return editor.Text;
    }

    private static void Type(TextEditor editor, string text)
        => ((ITextInputClient)editor.Surface).HandleTextInput(new TextInputEventArgs(text));

    private static (Window Window, TextEditor Editor) Host(string text)
    {
        var window = ScaledWindow.Create(1.0, 800, 300);
        var editor = new TextEditor
        {
            Text = text,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();
        return (window, editor);
    }
}
