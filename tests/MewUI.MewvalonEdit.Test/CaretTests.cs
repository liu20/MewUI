using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// What the editor's own caret answers. Whether the caret follows the keyboard is covered where a
/// window exists to move focus in; here it is only the switch itself.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CaretTests
{
    [TestMethod]
    public void HideAndShowToggleWhetherTheCaretIsDrawnAtAll()
    {
        var caret = new TextEditor { Text = "abc" }.TextArea.Caret;

        Assert.IsFalse(caret.IsVisible, "An editor no one is typing in draws no caret.");

        caret.Show();
        Assert.IsTrue(caret.IsVisible);

        caret.Hide();
        Assert.IsFalse(caret.IsVisible);
    }

    [TestMethod]
    public void TheCaretColorIsTheOneItWasGivenAndNullFollowsTheEditor()
    {
        var caret = new TextEditor { Text = "abc" }.TextArea.Caret;

        Assert.IsNull(caret.CaretBrush, "A caret follows the editor's foreground until told otherwise.");

        Assert.IsNull(caret.PrimaryCaretBrush, "a rectangle's active corner has a built-in colour");
        Assert.IsNull(caret.SecondaryCaretBrush, "so do the lines following it");
        caret.PrimaryCaretBrush = Color.FromRgb(0, 255, 0);
        caret.SecondaryCaretBrush = Color.FromRgb(0, 0, 255);
        Assert.AreEqual(Color.FromRgb(0, 255, 0), caret.PrimaryCaretBrush);
        Assert.AreEqual(Color.FromRgb(0, 0, 255), caret.SecondaryCaretBrush);

        caret.CaretBrush = Color.FromRgb(255, 0, 0);

        Assert.AreEqual(Color.FromRgb(255, 0, 0), caret.CaretBrush);
    }

    [TestMethod]
    public void OverstrikeModeIsOffUntilItIsSet()
    {
        var editor = new TextEditor { Text = "abc" };

        Assert.IsFalse(editor.TextArea.OverstrikeMode);

        editor.TextArea.OverstrikeMode = true;

        Assert.IsTrue(editor.TextArea.OverstrikeMode);
    }

    [TestMethod]
    public void ThePositionCarriesTheLocationOfTheOffset()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        editor.CaretOffset = 5;

        var position = editor.TextArea.Caret.Position;

        Assert.AreEqual(new TextLocation(2, 2), position.Location);
        Assert.AreEqual(2, editor.TextArea.Caret.Line);
        Assert.AreEqual(2, editor.TextArea.Caret.Column);
    }

    [TestMethod]
    public void AssigningThePositionMovesTheCaret()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        editor.TextArea.Caret.Position = new TextViewPosition(2, 3);

        Assert.AreEqual(6, editor.TextArea.Caret.Offset);
    }

    /// <summary>
    /// The visual column runs ahead of the document column wherever a projection stands more columns
    /// in for the text, which is the column the caret has to be placed by.
    /// </summary>
    [TestMethod]
    public void TheVisualColumnFollowsTheProjection()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "a\tb", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Options.ShowTabs = true;
        editor.Measure(new Size(240, 80));
        editor.Arrange(new Rect(0, 0, 240, 80));
        editor.CaretOffset = 2;

        Assert.AreEqual(3, editor.TextArea.Caret.Column, "Offset 2 is the third column of the line.");
        Assert.AreEqual(3, editor.TextArea.Caret.VisualColumn, "The tab marker column was not counted.");
    }
}
