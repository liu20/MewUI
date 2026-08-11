namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>
/// The search members of a text source worked out from the characters alone, so every source gets
/// the same answers. A character search reads one character at a time; a string search materializes
/// the range it was given, as the original does, because a comparison that is not ordinal cannot be
/// decided character by character.
/// </summary>
internal static class TextSourceSearch
{
    public static int IndexOf(ITextSource source, char value, int startIndex, int count)
    {
        VerifyRange(source, startIndex, count);
        for (int offset = startIndex; offset < startIndex + count; offset++)
        {
            if (source.GetCharAt(offset) == value)
            {
                return offset;
            }
        }
        return -1;
    }

    public static int LastIndexOf(ITextSource source, char value, int startIndex, int count)
    {
        VerifyRange(source, startIndex, count);
        for (int offset = startIndex + count - 1; offset >= startIndex; offset--)
        {
            if (source.GetCharAt(offset) == value)
            {
                return offset;
            }
        }
        return -1;
    }

    public static int IndexOfAny(ITextSource source, char[] anyOf, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(anyOf);
        VerifyRange(source, startIndex, count);
        for (int offset = startIndex; offset < startIndex + count; offset++)
        {
            char character = source.GetCharAt(offset);
            foreach (char candidate in anyOf)
            {
                if (character == candidate)
                {
                    return offset;
                }
            }
        }
        return -1;
    }

    public static int IndexOf(
        ITextSource source, string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(searchText);
        VerifyRange(source, startIndex, count);
        int position = source.GetText(startIndex, count).IndexOf(searchText, comparisonType);
        return position < 0 ? -1 : position + startIndex;
    }

    public static int LastIndexOf(
        ITextSource source, string searchText, int startIndex, int count, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(searchText);
        VerifyRange(source, startIndex, count);
        int position = source.GetText(startIndex, count).LastIndexOf(searchText, comparisonType);
        return position < 0 ? -1 : position + startIndex;
    }

    private static void VerifyRange(ITextSource source, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex, source.TextLength);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex + count, source.TextLength);
    }
}
