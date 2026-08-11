namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>
/// The strings the search panel shows. Assign a subclass to <see cref="SearchPanel.Localization"/>
/// to translate them.
/// </summary>
public class Localization
{
    public virtual string MatchCaseText => "Match case";

    public virtual string MatchWholeWordsText => "Match whole words";

    public virtual string UseRegexText => "Use regular expressions";

    public virtual string FindNextText => "Find next (F3)";

    public virtual string FindPreviousText => "Find previous (Shift+F3)";

    public virtual string ErrorText => "Error: ";

    public virtual string NoMatchesFoundText => "No matches found!";
}
