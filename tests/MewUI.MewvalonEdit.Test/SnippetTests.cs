using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Snippets;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A snippet inserts as one undo step and stays interactive: Tab walks the editable fields,
/// typing into a field mirrors into its bound copies, and Escape, Return, deletion (undo
/// included) or another snippet end the mode.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SnippetTests
{
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

    private static Snippet ForSnippet()
    {
        // for (int |field| = 0; |bound| < count; |bound|++) { caret }
        var counter = new SnippetReplaceableTextElement { Text = "i" };
        var snippet = new Snippet();
        snippet.Elements.Add(new SnippetTextElement { Text = "for (int " });
        snippet.Elements.Add(counter);
        snippet.Elements.Add(new SnippetTextElement { Text = " = 0; " });
        snippet.Elements.Add(new SnippetBoundElement { TargetElement = counter });
        snippet.Elements.Add(new SnippetTextElement { Text = " < count; " });
        snippet.Elements.Add(new SnippetBoundElement { TargetElement = counter });
        snippet.Elements.Add(new SnippetTextElement { Text = "++)" });
        return snippet;
    }

    private static void Press(TextEditor editor, Key key, ModifierKeys modifiers = ModifierKeys.None)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, modifiers));

    [TestMethod]
    public void InsertionIndentsEveryLineAndIsOneUndoStep()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("  x");
        editor.CaretOffset = 3;
        var snippet = new Snippet();
        snippet.Elements.Add(new SnippetTextElement { Text = "a\nb" });

        snippet.Insert(editor.TextArea);

        Assert.AreEqual("  xa\n  b", editor.Text.ReplaceLineEndings("\n"),
            "Every inserted line must carry the insertion position's indentation.");

        editor.Document.UndoStack.Undo();
        Assert.AreEqual("  x", editor.Text);
    }

    [TestMethod]
    public void TheFirstFieldIsSelectedAndTypingMirrorsIntoBoundCopies()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        ForSnippet().Insert(editor.TextArea);

        Assert.AreEqual("i", editor.TextArea.Selection.GetText(),
            "Entering interactive mode must select the first editable field.");

        editor.TextArea.PerformTextInput("index");

        Assert.AreEqual("for (int index = 0; index < count; index++)", editor.Text);
    }

    [TestMethod]
    public void TabWalksTheEditableFieldsAndWraps()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var first = new SnippetReplaceableTextElement { Text = "one" };
        var second = new SnippetReplaceableTextElement { Text = "two" };
        var snippet = new Snippet();
        snippet.Elements.Add(first);
        snippet.Elements.Add(new SnippetTextElement { Text = " " });
        snippet.Elements.Add(second);
        snippet.Insert(editor.TextArea);

        Assert.AreEqual("one", editor.TextArea.Selection.GetText());

        Press(editor, Key.Tab);
        Assert.AreEqual("two", editor.TextArea.Selection.GetText());

        Press(editor, Key.Tab);
        Assert.AreEqual("one", editor.TextArea.Selection.GetText(), "Tab past the last field wraps to the first.");

        Press(editor, Key.Tab, ModifierKeys.Shift);
        Assert.AreEqual("two", editor.TextArea.Selection.GetText(), "Shift+Tab walks backwards, wrapping too.");
    }

    [TestMethod]
    public void ReturnEndsTheModeAndPlacesTheCaret()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var context = new InsertionContext(editor.TextArea, 0);
        DeactivateReason? reason = null;
        context.Deactivated += (_, e) => reason = e.Reason;
        new SnippetTextElement { Text = "(" }.Insert(context);
        new SnippetReplaceableTextElement { Text = "x" }.Insert(context);
        new SnippetTextElement { Text = ")" }.Insert(context);
        new SnippetCaretElement().Insert(context);
        context.RaiseInsertionCompleted(null);

        Press(editor, Key.Enter);

        Assert.AreEqual(DeactivateReason.ReturnPressed, reason);
        Assert.AreEqual(editor.Document.TextLength, editor.CaretOffset,
            "The caret element placed the caret after the snippet.");
        Assert.IsEmpty(editor.TextArea.StackedInputHandlers.ToArray(),
            "Ending the mode must pop the snippet input handler.");
    }

    [TestMethod]
    public void EscapeEndsTheModeAndKeepsTheText()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        ForSnippet().Insert(editor.TextArea);

        Press(editor, Key.Escape);

        Assert.AreEqual("for (int i = 0; i < count; i++)", editor.Text);
        Assert.IsEmpty(editor.TextArea.StackedInputHandlers.ToArray());
    }

    [TestMethod]
    public void UndoDeletesTheSnippetAndLeavesInteractiveMode()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        ForSnippet().Insert(editor.TextArea);
        Assert.IsNotEmpty(editor.TextArea.StackedInputHandlers.ToArray());

        editor.Document.UndoStack.Undo();

        Assert.AreEqual("", editor.Text);
        Assert.IsEmpty(editor.TextArea.StackedInputHandlers.ToArray(),
            "Deleting the whole snippet by undo must leave interactive mode.");
    }

    [TestMethod]
    public void ASecondSnippetTakesOverFromTheFirst()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        ForSnippet().Insert(editor.TextArea);
        editor.CaretOffset = editor.Document.TextLength;

        ForSnippet().Insert(editor.TextArea);

        Assert.HasCount(1, editor.TextArea.StackedInputHandlers.ToArray(),
            "There can be only one active snippet.");
    }

    [TestMethod]
    public void ATextOnlySnippetDeactivatesImmediately()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("");
        var snippet = new Snippet();
        snippet.Elements.Add(new SnippetTextElement { Text = "plain" });

        snippet.Insert(editor.TextArea);

        Assert.AreEqual("plain", editor.Text);
        Assert.IsEmpty(editor.TextArea.StackedInputHandlers.ToArray(),
            "With no active elements there is nothing to stay interactive for.");
    }

}
