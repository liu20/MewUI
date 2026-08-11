namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>
/// The semantic identities of the search operations, so a host can present them in menus and
/// toolbars. <see cref="SearchPanel.Install(TextEditor)"/> binds their handlers and gestures on the editor;
/// the commands carry no behavior of their own.
/// </summary>
public static class SearchCommands
{
    public static readonly Command Find = new("search.find", "Find");
    public static readonly Command FindNext = new("search.findNext", "Find Next");
    public static readonly Command FindPrevious = new("search.findPrevious", "Find Previous");
}
