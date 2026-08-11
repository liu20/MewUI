using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// What a document tells its listeners. The file name is tracked apart from the text, since the
/// document itself never reads it.
/// </summary>
[TestClass]
public sealed class DocumentNotificationTests
{
    [TestMethod]
    public void TheFileNameRaisesOnlyWhenItActuallyChanges()
    {
        var document = new TextDocument("text");
        int raised = 0;
        document.FileNameChanged += (_, _) => raised++;

        document.FileName = "a.cs";
        document.FileName = "a.cs";

        Assert.AreEqual("a.cs", document.FileName);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void EveryChangeRaisesTextChanged()
    {
        var document = new TextDocument("one");
        int raised = 0;
        document.TextChanged += (_, _) => raised++;

        document.Insert(3, "\ntwo");
        document.Replace(0, 3, "ONE");

        Assert.AreEqual(2, raised);
    }

    /// <summary>
    /// The two counts report separately and only when they moved, which is what lets the line
    /// number margin resize on a new line without doing it on every keystroke.
    /// </summary>
    [TestMethod]
    public void TheCountsReportOnlyWhenTheyMove()
    {
        var document = new TextDocument("one");
        int lengths = 0;
        int lines = 0;
        document.TextLengthChanged += (_, _) => lengths++;
        document.LineCountChanged += (_, _) => lines++;

        document.Insert(3, "\ntwo");
        Assert.AreEqual(1, lengths);
        Assert.AreEqual(1, lines);

        // Same length, same line count: neither moved.
        document.Replace(0, 3, "ONE");
        Assert.AreEqual(1, lengths);
        Assert.AreEqual(1, lines);

        // Longer, but still one line more than it started with.
        document.Insert(0, "xx");
        Assert.AreEqual(2, lengths);
        Assert.AreEqual(1, lines);
    }
}
