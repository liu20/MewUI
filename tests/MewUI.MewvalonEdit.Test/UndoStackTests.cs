using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Indentation;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// Grouping is what the stack is mostly used for: a routine that edits line by line would otherwise
/// cost one undo step per line.
/// </summary>
[TestClass]
public sealed class UndoStackTests
{
    [TestMethod]
    public void TheStackBelongsToTheDocument()
    {
        var document = new TextDocument("abc");
        Assert.AreSame(document.UndoStack, document.UndoStack);
    }

    [TestMethod]
    public void AGroupUndoesAsOneStep()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var stack = editor.Document.UndoStack;

        using (stack.OpenUndoGroup())
        {
            editor.Document.Insert(0, "a");
            editor.Document.Insert(0, "b");
            editor.Document.Insert(0, "c");
        }
        Assert.AreEqual("cba" + "one\ntwo", editor.Document.Text);

        Assert.IsTrue(stack.Undo());

        Assert.AreEqual("one\ntwo", editor.Document.Text);
        Assert.IsFalse(stack.CanUndo);
    }

    [TestMethod]
    public void CanUndoFollowsTheDocument()
    {
        var editor = new TextEditor { Text = "abc" };
        var stack = editor.Document.UndoStack;
        Assert.IsFalse(stack.CanUndo);

        editor.Document.Insert(0, "x");

        Assert.IsTrue(stack.CanUndo);
        Assert.IsFalse(stack.CanRedo);

        stack.Undo();

        Assert.IsFalse(stack.CanUndo);
        Assert.IsTrue(stack.CanRedo);
    }

    [TestMethod]
    public void RedoPutsTheWholeGroupBack()
    {
        var editor = new TextEditor { Text = string.Empty };
        var stack = editor.Document.UndoStack;
        using (stack.OpenUndoGroup())
        {
            editor.Document.Insert(0, "a");
            editor.Document.Insert(1, "b");
        }
        stack.Undo();

        Assert.IsTrue(stack.Redo());

        Assert.AreEqual("ab", editor.Document.Text);
        Assert.IsFalse(stack.CanRedo);
    }

    /// <summary>
    /// A toolbar button follows these. The document's own change event is no substitute: it runs
    /// while the edit is still being recorded, so the history has not changed yet when it fires.
    /// </summary>
    [TestMethod]
    public void TheStackReportsWhenUndoBecomesAvailable()
    {
        var document = new TextDocument("abc");
        var stack = document.UndoStack;
        int undoChanges = 0;
        int redoChanges = 0;
        stack.CanUndoChanged += (_, _) => undoChanges++;
        stack.CanRedoChanged += (_, _) => redoChanges++;

        document.Insert(0, "x");
        Assert.AreEqual(1, undoChanges);
        Assert.AreEqual(0, redoChanges);

        stack.Undo();
        Assert.AreEqual(2, undoChanges, "Undo emptied the undo stack.");
        Assert.AreEqual(1, redoChanges);
    }

    [TestMethod]
    public void TheStackReportsNothingWhenAvailabilityDidNotChange()
    {
        var document = new TextDocument("abc");
        var stack = document.UndoStack;
        document.Insert(0, "x");
        int undoChanges = 0;
        stack.CanUndoChanged += (_, _) => undoChanges++;

        document.Insert(0, "y");

        Assert.AreEqual(0, undoChanges, "Undo was already available.");
    }

    [TestMethod]
    public void TypingReportsThroughTheEditor()
    {
        var editor = new TextEditor { Text = "abc" };
        int undoChanges = 0;
        editor.Document.UndoStack.CanUndoChanged += (_, _) => undoChanges++;

        editor.Document.Insert(0, "x");

        Assert.AreEqual(1, undoChanges);
        Assert.IsTrue(editor.Document.UndoStack.CanUndo);
    }

    /// <summary>
    /// A file view shows its dirty marker off this, so undoing back to the saved state has to clear
    /// it again rather than leave the file looking changed.
    /// </summary>
    [TestMethod]
    public void UndoingBackToTheMarkedStateMakesTheFileOriginalAgain()
    {
        var document = new TextDocument("saved");
        var stack = document.UndoStack;
        stack.MarkAsOriginalFile();
        Assert.IsTrue(stack.IsOriginalFile);

        document.Insert(0, "x");
        Assert.IsFalse(stack.IsOriginalFile);

        stack.Undo();
        Assert.IsTrue(stack.IsOriginalFile);

        stack.Redo();
        Assert.IsFalse(stack.IsOriginalFile);
    }

    [TestMethod]
    public void AGroupCountsAsOneStepTowardsTheMarker()
    {
        var document = new TextDocument("saved");
        var stack = document.UndoStack;
        stack.MarkAsOriginalFile();

        using (stack.OpenUndoGroup())
        {
            document.Insert(0, "a");
            document.Insert(0, "b");
            document.Insert(0, "c");
        }
        stack.Undo();

        Assert.IsTrue(stack.IsOriginalFile, "The group was one step away from the marked state.");
    }

    /// <summary>
    /// Editing after undoing past the marker throws away the redo branch the marker sat in, so no
    /// state reachable from here is the marked one any more.
    /// </summary>
    [TestMethod]
    public void EditingPastTheMarkerLosesIt()
    {
        var document = new TextDocument("saved");
        var stack = document.UndoStack;
        document.Insert(0, "a");
        stack.MarkAsOriginalFile();
        stack.Undo();
        Assert.IsFalse(stack.IsOriginalFile);

        document.Insert(0, "b");
        stack.Undo();

        Assert.IsFalse(stack.IsOriginalFile);
    }

    [TestMethod]
    public void DiscardingTheMarkerLeavesNothingOriginal()
    {
        var document = new TextDocument("saved");
        var stack = document.UndoStack;
        stack.MarkAsOriginalFile();

        stack.DiscardOriginalFileMarker();

        Assert.IsFalse(stack.IsOriginalFile);
    }

    [TestMethod]
    public void TheStackReportsWhenTheFileStopsBeingOriginal()
    {
        var document = new TextDocument("saved");
        var stack = document.UndoStack;
        stack.MarkAsOriginalFile();
        int changes = 0;
        stack.IsOriginalFileChanged += (_, _) => changes++;

        document.Insert(0, "a");
        Assert.AreEqual(1, changes);

        document.Insert(0, "b");
        Assert.AreEqual(1, changes, "It was already not original.");

        stack.Undo();
        stack.Undo();
        Assert.AreEqual(2, changes);
    }

    [TestMethod]
    public void IsModifiedFollowsTheMarker()
    {
        var editor = new TextEditor { Text = "saved" };
        editor.IsModified = false;
        Assert.IsFalse(editor.IsModified);

        editor.Document.Insert(0, "x");
        Assert.IsTrue(editor.IsModified);

        editor.Undo();
        Assert.IsFalse(editor.IsModified);
    }

    [TestMethod]
    public void SettingIsModifiedTrueDropsTheMarker()
    {
        var editor = new TextEditor { Text = "saved" };
        editor.IsModified = false;

        editor.IsModified = true;

        Assert.IsTrue(editor.IsModified);
        editor.Undo();
        Assert.IsTrue(editor.IsModified, "There was no marked state left to return to.");
    }

    [TestMethod]
    public void TheSizeLimitReportsWhenItChanges()
    {
        var document = new TextDocument("abc");
        int changes = 0;
        document.UndoStack.SizeLimitChanged += (_, _) => changes++;

        document.UndoStack.SizeLimit = 4;
        document.UndoStack.SizeLimit = 4;

        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void EndingAGroupThatWasNeverStartedThrows()
    {
        var document = new TextDocument("abc");
        Assert.ThrowsExactly<InvalidOperationException>(() => document.UndoStack.EndUndoGroup());
    }

    [TestMethod]
    public void NestingExtendsTheOutermostGroup()
    {
        var editor = new TextEditor { Text = string.Empty };
        var stack = editor.Document.UndoStack;

        stack.StartUndoGroup();
        editor.Document.Insert(0, "a");
        stack.StartUndoGroup();
        Assert.IsTrue(stack.IsInUndoGroup);
        editor.Document.Insert(1, "b");
        stack.EndUndoGroup();
        Assert.IsTrue(stack.IsInUndoGroup, "The outer group is still open.");
        editor.Document.Insert(2, "c");
        stack.EndUndoGroup();

        Assert.IsFalse(stack.IsInUndoGroup);
        stack.Undo();
        Assert.AreEqual(string.Empty, editor.Document.Text);
    }

    [TestMethod]
    public void ClearAllLeavesTheTextAlone()
    {
        var editor = new TextEditor { Text = "abc" };
        editor.Document.Insert(0, "x");

        editor.Document.UndoStack.ClearAll();

        Assert.IsFalse(editor.Document.UndoStack.CanUndo);
        Assert.AreEqual("xabc", editor.Document.Text);
    }

    [TestMethod]
    public void TheSizeLimitReachesTheDocument()
    {
        var document = new TextDocument("abc");
        document.UndoStack.SizeLimit = 5;
        Assert.AreEqual(5, document.UndoStack.SizeLimit);
    }

    [TestMethod]
    public void ADeclaredChangeBlockGroupsWhatIsInsideIt()
    {
        var editor = new TextEditor { Text = string.Empty };

        using (editor.DeclareChangeBlock())
        {
            editor.Document.Insert(0, "a");
            editor.Document.Insert(1, "b");
        }
        editor.Document.UndoStack.Undo();

        Assert.AreEqual(string.Empty, editor.Document.Text);
    }

    [TestMethod]
    public void RunUpdateGroupsWhatItRuns()
    {
        var document = new TextDocument(string.Empty);

        document.RunUpdate(() =>
        {
            document.Insert(0, "a");
            document.Insert(1, "b");
        });
        document.UndoStack.Undo();

        Assert.AreEqual(string.Empty, document.Text);
    }

    /// <summary>
    /// The case the group was measured against: reindenting a block edits every line in it, and
    /// undoing it a line at a time is not what the caller asked for. The grouping belongs to the
    /// caller, which is why a strategy that edits line by line still undoes as one step.
    /// </summary>
    [TestMethod]
    public void IndentingABlockUndoesAsOneStep()
    {
        var document = new TextDocument("a\n    b\nc\nd");
        var strategy = new PrefixingIndentationStrategy();

        document.RunUpdate(() => strategy.IndentLines(document, 2, 4));
        Assert.AreNotEqual("a\n    b\nc\nd", document.Text, "The strategy changed nothing to undo.");

        document.UndoStack.Undo();

        Assert.AreEqual("a\n    b\nc\nd", document.Text);
        Assert.IsFalse(document.UndoStack.CanUndo);
    }

    /// <summary>Reindents by giving every line one more level, so each line is its own edit.</summary>
    private sealed class PrefixingIndentationStrategy : DefaultIndentationStrategy
    {
        public override void IndentLines(TextDocument document, int beginLine, int endLine)
        {
            for (int lineNumber = beginLine; lineNumber <= endLine; lineNumber++)
            {
                document.Insert(document.GetLineByNumber(lineNumber).Offset, "  ");
            }
        }
    }
}
