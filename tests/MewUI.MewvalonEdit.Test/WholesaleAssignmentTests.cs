using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Folding;
using Aprillz.MewUI.MewvalonEdit.Search;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Assigning the whole text is not an edit a reader made, but everything hanging off the document
/// still has to end up describing the text that is now there: a host loading a file does nothing
/// beyond the assignment.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class WholesaleAssignmentTests
{
    [TestMethod]
    public void ASearchFollowsTheTextThatReplacedTheOneItScanned()
    {
        var editor = new TextEditor { Text = "cat cat cat" };
        var panel = SearchPanel.Install(editor);
        panel.SearchPattern = "cat";
        Assert.HasCount(3, panel.Results);

        editor.Text = "dog cat";

        Assert.HasCount(1, panel.Results, "the results still described the text that was replaced");
        Assert.AreEqual(4, panel.Results[0].Offset);
        panel.Uninstall();
    }

    /// <summary>
    /// The sections belong to ranges of the text that is gone, so they go with it rather than
    /// surviving as ranges into text that never had them.
    /// </summary>
    [TestMethod]
    public void FoldingsDoNotOutliveTheTextTheyWereFoundIn()
    {
        var editor = new TextEditor { Text = "a{\nx\n}b\nc" };
        var manager = FoldingManager.Install(editor);
        manager.UpdateFoldings([new NewFolding(1, 6)], -1);
        Assert.ContainsSingle(manager.AllFoldings);

        editor.Text = "nothing to fold";

        Assert.IsEmpty(manager.AllFoldings);
        FoldingManager.Uninstall(manager);
    }

    [TestMethod]
    public void TheCaretAndSelectionStartOverWithTheText()
    {
        var editor = new TextEditor { Text = "cat cat cat" };
        editor.Select(4, 3);

        editor.Text = "dog";

        Assert.AreEqual(0, editor.CaretOffset);
        Assert.AreEqual(0, editor.SelectionLength, "a selection into the replaced text was carried over");
    }
}
