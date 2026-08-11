using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class TextDocumentTests
{
    /// <summary>
    /// A programmatic document edit is undoable, as in AvalonEdit. Editing the core document
    /// straight through is unrecorded and drops the whole history, so the document has to route
    /// its edits through the surface once an editor adopts it.
    /// </summary>
    [TestMethod]
    public void ProgrammaticEditsStayUndoable()
    {
        var editor = new TextEditor { Text = "hello world" };
        editor.CaretOffset = editor.Text.Length;
        editor.TextArea.PerformTextInput("!");

        editor.Document.Replace(0, 5, "bye");

        Assert.AreEqual("bye world!", editor.Text);
        Assert.IsTrue(editor.CanUndo, "The programmatic replace must be undoable.");
        editor.Undo();
        Assert.AreEqual("hello world!", editor.Text, "Undo rolls back only the programmatic replace.");
    }

    /// <summary>
    /// Assigning the editor's text starts over, as in the original: the caret returns to the
    /// beginning and the history goes with it.
    /// </summary>
    [TestMethod]
    public void AssigningEditorTextResetsTheCaretAndDropsTheHistory()
    {
        var editor = new TextEditor { Text = "hello world" };
        editor.CaretOffset = editor.Text.Length;
        editor.TextArea.PerformTextInput("!");
        Assert.IsTrue(editor.CanUndo);

        editor.Text = "replaced";

        Assert.AreEqual("replaced", editor.Text);
        Assert.AreEqual(0, editor.CaretOffset);
        Assert.IsFalse(editor.CanUndo, "The text that was replaced cannot be brought back.");
    }

    /// <summary>
    /// Assigning the document's text is an ordinary replace, so it stays undoable. Only the editor's
    /// own Text setter starts over.
    /// </summary>
    [TestMethod]
    public void AssigningDocumentTextStaysUndoable()
    {
        var editor = new TextEditor { Text = "hello world" };

        editor.Document.Text = "replaced";

        Assert.AreEqual("replaced", editor.Text);
        Assert.IsTrue(editor.CanUndo);
        editor.Undo();
        Assert.AreEqual("hello world", editor.Text);
    }

    /// <summary>The caret rides along with the text instead of landing on a programmatic edit.</summary>
    [TestMethod]
    public void ProgrammaticEditKeepsTheCaretWithItsText()
    {
        var editor = new TextEditor { Text = "hello world" };
        editor.CaretOffset = 9;

        editor.Document.Replace(0, 5, "bye");

        Assert.AreEqual(7, editor.CaretOffset, "Three characters replaced five, so the caret moved back two.");
    }

    /// <summary>
    /// A version is a checkpoint: an offset taken at one carries across the edits that followed,
    /// which is what lets a caller hold a position through changes it did not make.
    /// </summary>
    [TestMethod]
    public void VersionsCarryOffsetsAcrossLaterEdits()
    {
        var document = new TextDocument("hello world");
        var before = document.Version;

        document.Replace(0, 5, "bye");
        var after = document.Version;

        Assert.AreEqual(-1, before.CompareAge(after));
        Assert.IsTrue(before.BelongsToSameDocumentAs(after));
        Assert.AreEqual(7, before.MoveOffsetTo(after, 9), "Three characters replaced five, so the offset moved back two.");
        Assert.AreEqual(9, after.MoveOffsetTo(before, 7), "Walking back reverses the shift.");
    }

    [TestMethod]
    public void VersionsOfDifferentDocumentsDoNotCompare()
    {
        var version = new TextDocument("a").Version;
        var other = new TextDocument("b").Version;

        Assert.IsFalse(version.BelongsToSameDocumentAs(other));
        Assert.IsFalse(version.BelongsToSameDocumentAs(null));
        Assert.ThrowsExactly<ArgumentException>(() => version.CompareAge(other));
    }

    [TestMethod]
    public void SnapshotKeepsTheTextItWasTakenFrom()
    {
        var document = new TextDocument("hello");
        var snapshot = document.CreateSnapshot();
        var ranged = document.CreateSnapshot(1, 3);

        document.Replace(0, 5, "bye");

        Assert.AreEqual("hello", snapshot.ToString());
        Assert.AreEqual("ell", ranged.ToString(), "A ranged snapshot keeps only what it covered.");
    }

    /// <summary>An offset inside a removed range collapses to where the removal started.</summary>
    [TestMethod]
    public void OffsetInsideARemovalCollapsesToItsStart()
    {
        var entry = new OffsetChangeMapEntry(4, removalLength: 6, insertionLength: 0);

        Assert.AreEqual(2, entry.GetNewOffset(2));
        Assert.AreEqual(4, entry.GetNewOffset(7));
        Assert.AreEqual(4, entry.GetNewOffset(10));
        Assert.AreEqual(5, entry.GetNewOffset(11));
    }

    [TestMethod]
    public void AnchorMovementDecidesWhereAnInsertionLeavesTheOffset()
    {
        var entry = new OffsetChangeMapEntry(4, removalLength: 0, insertionLength: 3);

        Assert.AreEqual(4, entry.GetNewOffset(4, AnchorMovementType.BeforeInsertion));
        Assert.AreEqual(7, entry.GetNewOffset(4, AnchorMovementType.AfterInsertion));
    }

    /// <summary>A file opened in the editor has to come back out with the terminators it arrived with.</summary>
    [TestMethod]
    public void EditorRoundTripsTheTerminatorsItWasGiven()
    {
        const string SOURCE = "one\r\ntwo\r\nthree";
        var editor = new TextEditor { Text = SOURCE };

        Assert.AreEqual(SOURCE, editor.Text);
        Assert.AreEqual(2, editor.Document.GetLineByNumber(1).DelimiterLength);
        Assert.AreEqual(3, editor.Document.LineCount);
    }

    /// <summary>
    /// Pressing Enter continues the terminator already in use, so a CRLF file does not gain a lone
    /// line feed. AvalonEdit takes the same terminator from the caret's line.
    /// </summary>
    [TestMethod]
    public void EnterContinuesTheSurroundingTerminator()
    {
        var editor = new TextEditor { Text = "one\r\ntwo" };
        editor.CaretOffset = editor.Text.Length;

        Assert.AreEqual("\r\n", TextUtilities.GetNewLineFromDocument(editor.Document, 2));

        editor.Document.Replace(editor.Text.Length, 0, TextUtilities.GetNewLineFromDocument(editor.Document, 2));

        Assert.AreEqual("one\r\ntwo\r\n", editor.Text);
        Assert.AreEqual(3, editor.Document.LineCount);
    }

    [TestMethod]
    public void AnchorsRideAlongWithTheirText()
    {
        var document = new TextDocument("hello world");
        var anchor = document.CreateAnchor(9);

        document.Replace(0, 5, "bye");

        Assert.AreEqual(7, anchor.Offset, "Three characters replaced five, so the anchor moved back two.");
        Assert.IsFalse(anchor.IsDeleted);
        Assert.AreEqual(1, anchor.Line);
        Assert.AreEqual(8, anchor.Column);
    }

    /// <summary>An anchor dies with the text it sat in unless it was told to survive.</summary>
    [TestMethod]
    public void AnchorInsideRemovedTextIsDeletedUnlessItSurvives()
    {
        var document = new TextDocument("hello world");
        var dying = document.CreateAnchor(3);
        var surviving = document.CreateAnchor(3);
        surviving.SurviveDeletion = true;
        int deletedRaised = 0;
        dying.Deleted += (_, _) => deletedRaised++;

        document.Replace(1, 6, string.Empty);

        Assert.IsTrue(dying.IsDeleted);
        Assert.AreEqual(1, deletedRaised);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = dying.Offset);
        Assert.IsFalse(surviving.IsDeleted);
        Assert.AreEqual(1, surviving.Offset, "A survivor collapses to where the removal started.");
    }

    [TestMethod]
    public void AnchorMovementDecidesWhichSideOfAnInsertionItStaysOn()
    {
        var document = new TextDocument("ab");
        var byDefault = document.CreateAnchor(1);
        var before = document.CreateAnchor(1);
        before.MovementType = AnchorMovementType.BeforeInsertion;
        var after = document.CreateAnchor(1);
        after.MovementType = AnchorMovementType.AfterInsertion;

        document.Insert(1, "XYZ");

        Assert.AreEqual(1, before.Offset);
        Assert.AreEqual(4, after.Offset);
        Assert.AreEqual(4, byDefault.Offset, "Default moves behind the insertion, as in the original.");
    }

    [TestMethod]
    public void DocumentUsesAvalonEditOneBasedLocations()
    {
        var document = new TextDocument("one\ntwo");

        Assert.AreEqual(2, document.LineCount);
        Assert.AreEqual(4, document.GetLineByNumber(2).Offset);
        Assert.AreEqual(new TextLocation(2, 2), document.GetLocation(5));
        Assert.AreEqual(5, document.GetOffset(2, 2));
    }

    /// <summary>
    /// A column outside the line clamps instead of throwing, which is what the original does and
    /// what its callers rely on when they hand over a position from another document.
    /// </summary>
    [TestMethod]
    public void GetOffsetClampsAColumnOutsideTheLine()
    {
        var document = new TextDocument("one\ntwo");

        Assert.AreEqual(4, document.GetOffset(2, 0), "A column at or before the start lands on it.");
        Assert.AreEqual(4, document.GetOffset(2, -5));
        Assert.AreEqual(7, document.GetOffset(2, 99), "A column past the end lands on the line end.");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => document.GetOffset(0, 1),
            "Only the line number is validated.");
    }

    /// <summary>
    /// A line read after an edit reports where it is now. A snapshot keeps handing back the offsets
    /// it was built with, which reads as a working line while pointing at the wrong text.
    /// </summary>
    [TestMethod]
    public void ALineHeldAcrossAnEditReportsItsCurrentPlace()
    {
        var document = new TextDocument("one\ntwo\nthree");
        var second = document.GetLineByNumber(2);
        Assert.AreEqual(4, second.Offset);

        document.Insert(0, "XX");

        Assert.AreEqual(6, second.Offset, "The line moved with the text inserted before it.");
        Assert.AreEqual(3, second.Length);
        Assert.AreEqual(9, second.EndOffset);
    }

    [TestMethod]
    public void ALineWhoseNumberIsGoneReadsAsDeleted()
    {
        var document = new TextDocument("one\ntwo\nthree");
        var third = document.GetLineByNumber(3);
        Assert.IsFalse(third.IsDeleted);

        document.Remove(3, document.TextLength - 3);

        Assert.IsTrue(third.IsDeleted);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = third.Offset);
    }

    [TestMethod]
    public void LinesReachTheirNeighbours()
    {
        var document = new TextDocument("one\ntwo\nthree");
        var second = document.GetLineByNumber(2);

        Assert.AreEqual(1, second.PreviousLine?.LineNumber);
        Assert.AreEqual(3, second.NextLine?.LineNumber);
        Assert.IsNull(document.GetLineByNumber(1).PreviousLine);
        Assert.IsNull(document.GetLineByNumber(3).NextLine);
    }

    /// <summary>A line is a segment, so it can be handed to anything that takes one.</summary>
    [TestMethod]
    public void ALineIsASegment()
    {
        var document = new TextDocument("one\ntwo");

        ISegment segment = document.GetLineByNumber(2);

        Assert.AreEqual(4, segment.Offset);
        Assert.AreEqual(3, segment.Length);
        Assert.AreEqual("two", document.GetText(segment));
    }

    /// <summary>
    /// A change reports the text on both sides of it, which is what lets a subscriber tell what a
    /// replace actually did rather than only how long it was.
    /// </summary>
    [TestMethod]
    public void AChangeCarriesTheTextItReplaced()
    {
        var document = new TextDocument("hello world");
        DocumentChangeEventArgs? change = null;
        document.Changed += (_, args) => change = args;

        document.Replace(6, 5, "there");

        Assert.IsNotNull(change);
        Assert.AreEqual("world", change.RemovedText.ToString());
        Assert.AreEqual("there", change.InsertedText.ToString());
        Assert.AreEqual(5, change.RemovalLength);
        Assert.AreEqual(5, change.InsertionLength);
    }

    /// <summary>An insertion removes nothing, and pays nothing to say so.</summary>
    [TestMethod]
    public void AnInsertionReportsNoRemovedText()
    {
        var document = new TextDocument("hello");
        DocumentChangeEventArgs? change = null;
        document.Changed += (_, args) => change = args;

        document.Insert(5, " world");

        Assert.IsNotNull(change);
        Assert.AreEqual(string.Empty, change.RemovedText.ToString());
        Assert.AreEqual(" world", change.InsertedText.ToString());
    }

    /// <summary>The change carries an offset across itself, which is how a held position survives it.</summary>
    [TestMethod]
    public void AChangeMovesAnOffsetAcrossItself()
    {
        var document = new TextDocument("hello world");
        DocumentChangeEventArgs? change = null;
        document.Changed += (_, args) => change = args;

        document.Replace(0, 5, "bye");

        Assert.IsNotNull(change);
        Assert.AreEqual(7, change.GetNewOffset(9), "Past the change, an offset shifts by the delta.");
        Assert.AreEqual(0, change.GetNewOffset(0), "At the start of the change, an offset stays.");
        Assert.AreEqual(3, change.GetNewOffset(2), "Inside the removal, it lands after what replaced it.");
        Assert.HasCount(1, change.OffsetChangeMap);
    }

    [TestMethod]
    public void MutationsRaiseDocumentAndTextEvents()
    {
        var document = new TextDocument("abc");
        DocumentChangeEventArgs? change = null;
        int textChanges = 0;
        document.Changed += (_, args) => change = args;
        document.TextChanged += (_, _) => textChanges++;

        document.Replace(1, 1, "XYZ");

        Assert.AreEqual("aXYZc", document.Text);
        Assert.IsNotNull(change);
        Assert.AreEqual(1, change.Offset);
        Assert.AreEqual(1, change.RemovalLength);
        Assert.AreEqual(3, change.InsertionLength);
        Assert.AreEqual(1, textChanges);
    }
}
