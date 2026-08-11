using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The rectangle is made of two x pixels, and every line gives up the columns those pixels land on.
/// These pin the re-ported write path: typing lands on every line, virtual space is padded into
/// existence, a block paste distributes its lines, and the whole edit is one undo step.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RectangleSelectionTests
{
    private static TextEditor CreateEditor(string text)
    {
        // Monospace, so a column's x is the same on every line and the pixel-to-column round trip
        // in the assertions is exact; the pixel model itself is what makes proportional fonts work.
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

    [TestMethod]
    public void EveryCoveredLineGivesUpTheSameColumns()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        var segments = selection.Segments.ToArray();
        Assert.HasCount(3, segments);
        for (int index = 0; index < segments.Length; index++)
        {
            Assert.AreEqual(2, segments[index].StartVisualColumn, $"line {index + 1} start column");
            Assert.AreEqual(4, segments[index].EndVisualColumn, $"line {index + 1} end column");
            Assert.AreEqual(2, segments[index].Length, $"line {index + 1} length");
        }
        Assert.AreEqual("cd\nij\nop", selection.GetText().ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void AShortLineExtendsIntoVirtualSpace()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nab");
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(2, 3, 5));

        var segments = selection.Segments.ToArray();
        Assert.HasCount(2, segments);
        Assert.AreEqual(5, segments[0].EndVisualColumn);
        var document = editor.Document;
        var shortLine = document.GetLineByNumber(2);
        Assert.AreEqual(shortLine.Offset + shortLine.Length, segments[1].EndOffset,
            "The short line's segment must stop at its end while the column runs on virtually.");
        Assert.AreEqual(5, segments[1].EndVisualColumn);
    }

    [TestMethod]
    public void TypingLandsOnEveryLineAndUndoesAsOneStep()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        selection.ReplaceSelectionWithText("XY");

        Assert.AreEqual("abXYef\nghXYkl\nmnXYqr", editor.Text.ReplaceLineEndings("\n"));

        editor.Document.UndoStack.Undo();
        Assert.AreEqual("abcdef\nghijkl\nmnopqr", editor.Text.ReplaceLineEndings("\n"),
            "The whole rectangle edit must undo as a single step.");
    }

    [TestMethod]
    public void TypingIntoVirtualSpacePadsTheShortLine()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nab");
        // A zero-width rectangle at visual column 4: the short line only has two columns, so the
        // missing two must be created as spaces before the typed text.
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 5, 4), new TextViewPosition(2, 3, 4));

        selection.ReplaceSelectionWithText("X");

        Assert.AreEqual("abcdXef\nab  X", editor.Text.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void AMultiLineReplacementDistributesItsLines()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl\nmnopqr");
        var selection = new RectangleSelection(
            editor.TextArea, new TextViewPosition(1, 3, 2), new TextViewPosition(3, 5, 4));

        selection.ReplaceSelectionWithText("11\n22\n33");

        Assert.AreEqual("ab11ef\ngh22kl\nmn33qr", editor.Text.ReplaceLineEndings("\n"));
        Assert.IsTrue(editor.TextArea.Selection.IsEmpty, "A block paste ends the selection.");
    }

    [TestMethod]
    public void RectangularPasteRefusesABlockTallerThanTheDocument()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("abcdef\nghijkl");

        bool pasted = RectangleSelection.PerformRectangularPaste(
            editor.TextArea, new TextViewPosition(1, 1, 0), "1\n2\n3", selectInsertedText: false);

        Assert.IsFalse(pasted, "Three block lines cannot land on a two-line document.");
        Assert.AreEqual("abcdef\nghijkl", editor.Text.ReplaceLineEndings("\n"));
    }
}
