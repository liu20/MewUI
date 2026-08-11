using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.CodeCompletion;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Resources;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The completion window keeps the focus in the editor and follows the original's lifetime: the
/// segment grows as the user types and the query re-selects the best match, a removal immediately
/// in front of the segment closes it, and any other stacked handler taking over closes it too.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CompletionWindowTests
{
    private static readonly string[] WORDS = ["DateTime", "DateTimeKind", "Debug", "console", "CodeQualityAnalysis"];

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

    private static CompletionWindow OpenWindow(TextEditor editor)
    {
        var window = new CompletionWindow(editor.TextArea);
        foreach (string word in WORDS)
        {
            window.CompletionList.CompletionData.Add(new CompletionData(word));
        }
        window.Show();
        return window;
    }

    private static void Press(TextEditor editor, Key key, ModifierKeys modifiers = ModifierKeys.None)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, modifiers));

    [TestMethod]
    public void TypingFiltersAndSelectsTheBestMatch()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);

        editor.TextArea.PerformTextInput("Da");

        Assert.IsTrue(window.IsOpen);
        Assert.AreEqual(window.StartOffset + 2, window.EndOffset, "Typing must grow the completion region.");
        Assert.AreEqual("DateTime", window.CompletionList.SelectedItem?.Text);
        Assert.IsTrue(window.CompletionList.VisibleItems.All(item => item.Text.Contains("Da")),
            "Filtering must keep only matching items.");
    }

    [TestMethod]
    public void CamelCaseQueriesMatch()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);

        editor.TextArea.PerformTextInput("CQ");

        Assert.AreEqual("CodeQualityAnalysis", window.CompletionList.SelectedItem?.Text);
    }

    [TestMethod]
    public void TabCommitsTheSelectionAndClosesFirst()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);
        editor.TextArea.PerformTextInput("Deb");

        Press(editor, Key.Tab);

        Assert.IsFalse(window.IsOpen);
        Assert.AreEqual("Debug", editor.Text);
        Assert.AreEqual(5, editor.CaretOffset);
    }

    [TestMethod]
    public void EscapeClosesWithoutInserting()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);
        editor.TextArea.PerformTextInput("De");

        Press(editor, Key.Escape);

        Assert.IsFalse(window.IsOpen);
        Assert.AreEqual("De", editor.Text);
    }

    [TestMethod]
    public void ArrowKeysWalkTheListWhileTheEditorKeepsTyping()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);
        editor.TextArea.PerformTextInput("D");

        var first = window.CompletionList.SelectedItem;
        Press(editor, Key.Down);

        Assert.AreNotEqual(first, window.CompletionList.SelectedItem);
        Assert.AreEqual("D", editor.Text, "Arrow keys must not reach the document.");
    }

    [TestMethod]
    public void RemovalInFrontOfTheSegmentCloses()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("obj.");
        editor.CaretOffset = 4;
        var window = OpenWindow(editor);
        editor.TextArea.PerformTextInput("De");

        // Backspace twice: erasing the typed text keeps the window, erasing the dot closes it.
        editor.Document.Remove(4, 2);
        Assert.IsTrue(window.IsOpen, "Removing typed text inside the segment keeps the window.");
        editor.Document.Remove(3, 1);
        Assert.IsFalse(window.IsOpen, "Removing immediately in front of the segment must close.");
    }

    [TestMethod]
    public void CaretLeavingTheSegmentCloses()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("hello world");
        editor.CaretOffset = 5;
        var window = OpenWindow(editor);

        editor.CaretOffset = 9;

        Assert.IsFalse(window.IsOpen);
    }

    [TestMethod]
    public void CloseWhenCaretAtBeginningRespectsTheFlag()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var window = OpenWindow(editor);
        window.CloseWhenCaretAtBeginning = true;
        editor.TextArea.PerformTextInput("D");

        editor.Document.Remove(window.StartOffset, 1);

        Assert.IsFalse(window.IsOpen, "Erasing back to the start must close under Ctrl+Space semantics.");
    }

    [TestMethod]
    public void AnotherStackedHandlerTakingOverCloses()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var other = new PassiveStackedHandler(editor.TextArea);
        editor.TextArea.PushStackedInputHandler(other);
        var window = OpenWindow(editor);

        // Popping a handler below the completion handler pops everything above it too, which is
        // exactly the "any other input handler becomes active" close the original relies on.
        editor.TextArea.PopStackedInputHandler(other);

        Assert.IsFalse(window.IsOpen,
            "Popping past the completion handler must close the window, as the original's dummy handler does.");
    }

    [TestMethod]
    public void ASecondWindowOfTheSameTypeReplacesTheFirst()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var first = OpenWindow(editor);
        var second = OpenWindow(editor);

        Assert.IsFalse(first.IsOpen);
        Assert.IsTrue(second.IsOpen);
    }

    [TestMethod]
    public void AnEntryCarriesTheIconTheRowDraws()
    {
        var withImage = new CompletionData("WithImage", image: new StubImageSource());
        Assert.IsNotNull(withImage.Image);

        // An implementation predating the icon must still compile and report none.
        ICompletionData without = new MinimalCompletionData();
        Assert.IsNull(without.Image, "the icon must default to none rather than force every implementation to supply one");
    }

    private sealed class StubImageSource : IImageSource
    {
        public IImage CreateImage(IGraphicsFactory factory) => throw new NotSupportedException();
    }

    private sealed class MinimalCompletionData : ICompletionData
    {
        public string Text => "Minimal";
        public object? Content => Text;
        public object? Description => null;
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
        }
    }

    private sealed class PassiveStackedHandler(TextArea textArea) : TextAreaStackedInputHandler(textArea);
}
