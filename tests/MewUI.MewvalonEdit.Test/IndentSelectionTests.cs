using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Indentation;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Reindenting through the strategy: the selected lines, or the whole document when nothing is
/// selected, as one undo step. The default strategy reindents nothing, so a host that wants the
/// command to do something supplies one that reads the language.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class IndentSelectionTests
{
    private const string TEXT = "one\ntwo\nthree\nfour";

    /// <summary>Records the range asked for and gives every line one more level.</summary>
    private sealed class RecordingStrategy : DefaultIndentationStrategy
    {
        public int BeginLine { get; private set; }
        public int EndLine { get; private set; }
        public int Calls { get; private set; }

        public override void IndentLines(TextDocument document, int beginLine, int endLine)
        {
            Calls++;
            BeginLine = beginLine;
            EndLine = endLine;
            for (int lineNumber = beginLine; lineNumber <= endLine; lineNumber++)
            {
                document.Insert(document.GetLineByNumber(lineNumber).Offset, "  ");
            }
        }
    }

    [TestMethod]
    public void AnEmptySelectionReindentsTheWholeDocument()
    {
        var (editor, strategy) = CreateEditor();

        editor.IndentSelection();

        Assert.AreEqual(1, strategy.BeginLine);
        Assert.AreEqual(4, strategy.EndLine, "nothing selected means the whole document, as the original has it");
    }

    [TestMethod]
    public void ASelectionReindentsTheLinesItTouches()
    {
        var (editor, strategy) = CreateEditor();
        // From inside line 2 to inside line 3, so a partial touch still takes the whole line.
        editor.Select(TEXT.IndexOf("wo", StringComparison.Ordinal), 4);

        editor.IndentSelection();

        Assert.AreEqual(2, strategy.BeginLine);
        Assert.AreEqual(3, strategy.EndLine);
        Assert.AreEqual("one\n  two\n  three\nfour", editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void TheWholeBlockUndoesAsOneStep()
    {
        var (editor, _) = CreateEditor();
        editor.Document.UndoStack.ClearAll();

        editor.IndentSelection();
        Assert.AreNotEqual(TEXT, editor.Text.ReplaceLineEndings("\n"));

        editor.Document.UndoStack.Undo();

        Assert.AreEqual(TEXT, editor.Text.ReplaceLineEndings("\n"));
        Assert.IsFalse(editor.Document.UndoStack.CanUndo, "the block came back one line at a time");
    }

    [TestMethod]
    public void TheDefaultStrategyReindentsNothing()
    {
        var editor = new TextEditor { Text = TEXT, SkipViewportCull = true };
        Assert.IsInstanceOfType<DefaultIndentationStrategy>(editor.IndentationStrategy,
            "an editor starts with the default strategy");

        editor.IndentSelection();

        Assert.AreEqual(TEXT, editor.Text.ReplaceLineEndings("\n"),
            "copying the line above down a block flattens it, which is why the original does nothing here");
    }

    [TestMethod]
    public void AReadOnlyEditorReindentsNothing()
    {
        var (editor, strategy) = CreateEditor();
        editor.IsReadOnly = true;

        editor.IndentSelection();

        Assert.AreEqual(0, strategy.Calls);
        Assert.AreEqual(TEXT, editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void TheCommandAnswersItsGesture()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var window = ScaledWindow.Create(1.0, 600, 300);
        var (editor, strategy) = CreateEditor();
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();

        // The platform reports the resolved modifier, which is what the Primary gesture maps to.
        WindowInputRouter.KeyDown(window, new KeyEventArgs(Key.I, platformKey: 0, ModifierKeys.Control));

        Assert.AreEqual(1, strategy.Calls, "Ctrl+I did not reach the command");
    }

    private static (TextEditor Editor, RecordingStrategy Strategy) CreateEditor()
    {
        var strategy = new RecordingStrategy();
        var editor = new TextEditor { Text = TEXT, SkipViewportCull = true, IndentationStrategy = strategy };
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        return (editor, strategy);
    }
}
