using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>Basic interface for search algorithms.</summary>
public interface ISearchStrategy : IEquatable<ISearchStrategy>
{
    /// <summary>
    /// All matches inside the range, in order: the end of one result is at or before the start of
    /// the next, and every result lies inside the range.
    /// </summary>
    IEnumerable<ISearchResult> FindAll(ITextSource document, int offset, int length);

    /// <summary>The first match inside the range, or null.</summary>
    ISearchResult? FindNext(ITextSource document, int offset, int length);
}

/// <summary>A match, and how a replacement reads against it.</summary>
public interface ISearchResult : ISegment
{
    /// <summary>
    /// The replacement with its references to parts of the match, such as <c>$1</c>, filled in.
    /// </summary>
    string ReplaceWith(string replacement);
}

/// <summary>Supported search modes.</summary>
public enum SearchMode
{
    /// <summary>The pattern is literal text.</summary>
    Normal,

    /// <summary>The pattern is a regular expression.</summary>
    RegEx,

    /// <summary>The pattern uses <c>?</c> and <c>*</c>.</summary>
    Wildcard
}

/// <summary>Thrown when a search pattern cannot be understood.</summary>
public class SearchPatternException : Exception
{
    public SearchPatternException()
    {
    }

    public SearchPatternException(string message) : base(message)
    {
    }

    public SearchPatternException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
