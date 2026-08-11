using System.Text.RegularExpressions;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>Finds matches of a compiled regular expression. Every mode is compiled down to one.</summary>
internal sealed class RegexSearchStrategy : ISearchStrategy
{
    private readonly Regex _searchPattern;
    private readonly bool _matchWholeWords;

    public RegexSearchStrategy(Regex searchPattern, bool matchWholeWords)
    {
        _searchPattern = searchPattern ?? throw new ArgumentNullException(nameof(searchPattern));
        _matchWholeWords = matchWholeWords;
    }

    public IEnumerable<ISearchResult> FindAll(ITextSource document, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(document);
        int endOffset = offset + length;
        string text = document.GetText(0, document.TextLength);
        foreach (Match result in _searchPattern.Matches(text))
        {
            int resultEndOffset = result.Length + result.Index;
            if (offset > result.Index || endOffset < resultEndOffset)
            {
                continue;
            }
            if (_matchWholeWords &&
                (!IsWordBorder(text, result.Index) || !IsWordBorder(text, resultEndOffset)))
            {
                continue;
            }
            yield return new RegexSearchResult(result);
        }
    }

    public ISearchResult? FindNext(ITextSource document, int offset, int length)
        => FindAll(document, offset, length).FirstOrDefault();

    public bool Equals(ISearchStrategy? other)
        => other is RegexSearchStrategy strategy &&
           strategy._searchPattern.ToString() == _searchPattern.ToString() &&
           strategy._searchPattern.Options == _searchPattern.Options &&
           strategy._matchWholeWords == _matchWholeWords;

    public override bool Equals(object? obj) => Equals(obj as ISearchStrategy);

    public override int GetHashCode()
        => HashCode.Combine(_searchPattern.ToString(), _searchPattern.Options, _matchWholeWords);

    /// <summary>
    /// Whether a word starts or ends here. AvalonEdit asks its caret-positioning helper for the same
    /// answer; this port reads the two characters, which is what that helper does for word borders.
    /// </summary>
    private static bool IsWordBorder(string text, int offset)
    {
        bool before = offset > 0 && IsWordCharacter(text[offset - 1]);
        bool after = offset < text.Length && IsWordCharacter(text[offset]);
        return before != after;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}

/// <summary>A match that can fill a replacement's group references in.</summary>
internal sealed class RegexSearchResult(Match match) : ISearchResult
{
    public int Offset => match.Index;
    public int Length => match.Length;
    public int EndOffset => match.Index + match.Length;

    public string ReplaceWith(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return match.Result(replacement);
    }
}
