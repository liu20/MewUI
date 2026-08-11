using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Search;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Where a search starts from, for the two things that move a reader through the document: asking
/// for the next match, and changing what is being looked for.
/// </summary>
[TestClass]
public sealed class SearchWalkTests
{
    //          0123456789
    private const string TEXT = "cat cat cat";

    private static (TextEditor Editor, SearchPanel Panel) Panel(string pattern = "cat")
    {
        var editor = new TextEditor { Text = TEXT };
        var panel = SearchPanel.Install(editor);
        panel.SearchPattern = pattern;
        return (editor, panel);
    }

    [TestMethod]
    public void FindNextStepsOverTheMatchTheSelectionStartsOn()
    {
        var (editor, panel) = Panel();
        editor.Select(4, 0);

        var result = panel.FindNext();

        Assert.IsNotNull(result);
        Assert.AreEqual(8, result.Value.Offset, "the match under the caret was found again instead of the next one");
        panel.Uninstall();
    }

    /// <summary>
    /// Every match is reached in turn, including one starting exactly where the previous ended: a
    /// walk keyed off the end of the selection would step over it.
    /// </summary>
    [TestMethod]
    public void RepeatedFindNextReachesEveryMatchAndWraps()
    {
        var editor = new TextEditor { Text = "abab" };
        var panel = SearchPanel.Install(editor);
        panel.SearchPattern = "ab";

        int[] offsets = [panel.FindNext()!.Value.Offset, panel.FindNext()!.Value.Offset, panel.FindNext()!.Value.Offset];

        Assert.AreEqual(2, offsets[0]);
        Assert.AreEqual(0, offsets[1], "the walk wrapped past the second match instead of onto the first");
        Assert.AreEqual(2, offsets[2]);
        panel.Uninstall();
    }

    [TestMethod]
    public void ChangingThePatternSelectsTheFirstMatchAtOrAfterTheSelection()
    {
        var editor = new TextEditor { Text = TEXT };
        var panel = SearchPanel.Install(editor);
        editor.Select(5, 0);

        panel.SearchPattern = "cat";

        Assert.AreEqual(8, editor.SelectionStart);
        Assert.AreEqual(3, editor.SelectionLength);
        panel.Uninstall();
    }

    /// <summary>
    /// Narrowing a pattern keeps the reader on the match already selected rather than walking on,
    /// because the search restarts at where that match begins.
    /// </summary>
    [TestMethod]
    public void RefiningThePatternStaysOnTheSameMatch()
    {
        var (editor, panel) = Panel("ca");
        Assert.AreEqual(0, editor.SelectionStart);

        panel.SearchPattern = "cat";

        Assert.AreEqual(0, editor.SelectionStart);
        Assert.AreEqual(3, editor.SelectionLength);
        panel.Uninstall();
    }

    [TestMethod]
    public void AnOptionThatLeavesNoMatchAheadSelectsNothing()
    {
        var editor = new TextEditor { Text = TEXT };
        var panel = SearchPanel.Install(editor);
        editor.Select(9, 0);

        panel.SearchPattern = "cat";

        Assert.AreEqual(0, editor.SelectionLength, "a search does not wrap until the reader asks for the next match");
        panel.Uninstall();
    }

    [TestMethod]
    public void RefreshingAfterAnEditLeavesTheSelectionAlone()
    {
        var (editor, panel) = Panel();
        editor.Select(4, 3);

        panel.Refresh();

        Assert.AreEqual(4, editor.SelectionStart);
        Assert.AreEqual(3, editor.SelectionLength);
        panel.Uninstall();
    }
}
