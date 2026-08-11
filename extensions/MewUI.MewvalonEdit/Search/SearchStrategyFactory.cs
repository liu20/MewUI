using System.Text;
using System.Text.RegularExpressions;

namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>Builds the strategy a set of search options asks for.</summary>
public static class SearchStrategyFactory
{
    /// <summary>
    /// The strategy for these options.
    /// </summary>
    /// <exception cref="SearchPatternException">The pattern is not a usable expression.</exception>
    public static ISearchStrategy Create(
        string searchPattern, bool ignoreCase, bool matchWholeWords, SearchMode mode)
    {
        ArgumentNullException.ThrowIfNull(searchPattern);
        var options = RegexOptions.Multiline;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        // Every mode ends up a regular expression, so one matcher serves all three.
        searchPattern = mode switch
        {
            SearchMode.Normal => Regex.Escape(searchPattern),
            SearchMode.Wildcard => ConvertWildcardsToRegex(searchPattern),
            _ => searchPattern
        };
        try
        {
            return new RegexSearchStrategy(new Regex(searchPattern, options), matchWholeWords);
        }
        catch (ArgumentException exception)
        {
            throw new SearchPatternException(exception.Message, exception);
        }
    }

    private static string ConvertWildcardsToRegex(string searchPattern)
    {
        if (string.IsNullOrEmpty(searchPattern))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (char value in searchPattern)
        {
            switch (value)
            {
                case '?':
                    builder.Append('.');
                    break;
                case '*':
                    builder.Append(".*");
                    break;
                default:
                    builder.Append(Regex.Escape(value.ToString()));
                    break;
            }
        }
        return builder.ToString();
    }
}
