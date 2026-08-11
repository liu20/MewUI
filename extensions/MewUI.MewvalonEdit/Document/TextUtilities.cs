using System.Globalization;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>How <see cref="TextUtilities.GetNextCaretPosition"/> decides where a caret may stop.</summary>
public enum CaretPositioningMode
{
    /// <summary>Normal positioning (stop after every grapheme).</summary>
    Normal,

    /// <summary>Stop only on word borders.</summary>
    WordBorder,

    /// <summary>Stop only at the beginning of words. This is used for Ctrl+Left/Ctrl+Right.</summary>
    WordStart,

    /// <summary>Stop only at the beginning of words, and anywhere in the middle of symbols.</summary>
    WordStartOrSymbol,

    /// <summary>Stop only on word borders, and anywhere in the middle of symbols.</summary>
    WordBorderOrSymbol,

    /// <summary>
    /// Stop between every Unicode codepoint, even within the same grapheme. This is what deleting
    /// the previous grapheme with Backspace steps by.
    /// </summary>
    EveryCodepoint
}

/// <summary>Classifies a character as whitespace, line terminator, part of an identifier, or other.</summary>
public enum CharacterClass
{
    /// <summary>Not whitespace, a line terminator, or part of an identifier.</summary>
    Other,

    /// <summary>Whitespace that is not a line terminator.</summary>
    Whitespace,

    /// <summary>May be part of an identifier: a letter, a digit or an underscore.</summary>
    IdentifierPart,

    /// <summary>A line terminator, '\r' or '\n'.</summary>
    LineTerminator,

    /// <summary>A combining mark that modifies the previous character.</summary>
    CombiningMark
}

/// <summary>Helpers over document text.</summary>
public static class TextUtilities
{
    // The first 32 ASCII characters, the Unicode C0 block.
    private static readonly string[] _c0Names =
    [
        "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "HT",
        "LF", "VT", "FF", "CR", "SO", "SI", "DLE", "DC1", "DC2", "DC3",
        "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS",
        "RS", "US"
    ];

    // DEL, then the C1 block from 128 to 159.
    private static readonly string[] _delAndC1Names =
    [
        "DEL",
        "PAD", "HOP", "BPH", "NBH", "IND", "NEL", "SSA", "ESA", "HTS", "HTJ",
        "VTS", "PLD", "PLU", "RI", "SS2", "SS3", "DCS", "PU1", "PU2", "STS",
        "CCH", "MW", "SPA", "EPA", "SOS", "SGCI", "SCI", "CSI", "ST", "OSC",
        "PM", "APC"
    ];

    /// <summary>Whether the string is one of the terminators a document recognises.</summary>
    public static bool IsNewLine(string? newLine)
        => newLine is "\r\n" or "\n" or "\r";

    /// <summary>
    /// Short name of a control character, as drawn in the box that stands in for it. An unnamed one
    /// gives its code point as four hex digits.
    /// </summary>
    public static string GetControlCharacterName(char controlCharacter)
    {
        int code = controlCharacter;
        if (code < _c0Names.Length)
        {
            return _c0Names[code];
        }
        return code is >= 127 and <= 159
            ? _delAndC1Names[code - 127]
            : code.ToString("x4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The terminator to use when inserting a line break at <paramref name="lineNumber"/>: the one
    /// that line already ends with, or the previous line's at the end of the document. An empty
    /// document has none to copy, so the platform's terminator is used.
    /// </summary>
    public static string GetNewLineFromDocument(TextDocument document, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(document);
        var line = document.GetLineByNumber(lineNumber);
        if (line.DelimiterLength == 0)
        {
            if (line.LineNumber <= 1)
            {
                return Environment.NewLine;
            }
            line = document.GetLineByNumber(line.LineNumber - 1);
        }
        return document.GetText(line.Offset + line.Length, line.DelimiterLength);
    }

    /// <summary>
    /// All whitespace (' ' and '\t', but no newlines) after <paramref name="offset"/>, as the
    /// segment containing it.
    /// </summary>
    public static ISegment GetWhitespaceAfter(ITextSource textSource, int offset)
    {
        ArgumentNullException.ThrowIfNull(textSource);
        int position;
        for (position = offset; position < textSource.TextLength; position++)
        {
            char character = textSource.GetCharAt(position);
            if (character != ' ' && character != '\t')
            {
                break;
            }
        }
        return new SimpleSegment(offset, position - offset);
    }

    /// <summary>Whether the character is whitespace, part of an identifier, or a line terminator.</summary>
    public static CharacterClass GetCharacterClass(char character)
    {
        if (character is '\r' or '\n')
        {
            return CharacterClass.LineTerminator;
        }
        if (character == '_')
        {
            return CharacterClass.IdentifierPart;
        }
        return GetCharacterClass(char.GetUnicodeCategory(character));
    }

    private static CharacterClass GetCharacterClass(char highSurrogate, char lowSurrogate)
    {
        if (char.IsSurrogatePair(highSurrogate, lowSurrogate))
        {
            return GetCharacterClass(
                char.GetUnicodeCategory(string.Concat(highSurrogate, lowSurrogate), 0));
        }
        // A malformed surrogate pair classifies as nothing in particular.
        return CharacterClass.Other;
    }

    private static CharacterClass GetCharacterClass(UnicodeCategory category) => category switch
    {
        UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or
        UnicodeCategory.ParagraphSeparator or UnicodeCategory.Control
            => CharacterClass.Whitespace,
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
        UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber
            => CharacterClass.IdentifierPart,
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
        UnicodeCategory.EnclosingMark
            => CharacterClass.CombiningMark,
        _ => CharacterClass.Other,
    };

    /// <summary>
    /// Offset of the next caret position in <paramref name="direction"/>, or -1 when there is none.
    /// Unlike real caret movement there are no extra stops at line starts and ends here; a linefeed
    /// counts as simple whitespace.
    /// </summary>
    public static int GetNextCaretPosition(
        ITextSource textSource, int offset, LogicalDirection direction, CaretPositioningMode mode)
    {
        ArgumentNullException.ThrowIfNull(textSource);
        int textLength = textSource.TextLength;
        if (textLength <= 0)
        {
            // An empty text has a normal caret position at 0, though no word borders.
            if (IsNormal(mode))
            {
                if (offset > 0 && direction == LogicalDirection.Backward)
                {
                    return 0;
                }
                if (offset < 0 && direction == LogicalDirection.Forward)
                {
                    return 0;
                }
            }
            return -1;
        }
        while (true)
        {
            int nextPos = direction == LogicalDirection.Backward ? offset - 1 : offset + 1;

            // No further caret position; this also handles offsets outside the valid range.
            if (nextPos < 0 || nextPos > textLength)
            {
                return -1;
            }

            if (nextPos == 0)
            {
                // At the text start there is only a word border if the first character is not whitespace.
                if (IsNormal(mode) || !char.IsWhiteSpace(textSource.GetCharAt(0)))
                {
                    return nextPos;
                }
            }
            else if (nextPos == textLength)
            {
                // At the text end there is never a word start, and only a word border if the last
                // character is not whitespace.
                if (mode != CaretPositioningMode.WordStart && mode != CaretPositioningMode.WordStartOrSymbol)
                {
                    if (IsNormal(mode) || !char.IsWhiteSpace(textSource.GetCharAt(textLength - 1)))
                    {
                        return nextPos;
                    }
                }
            }
            else
            {
                char charBefore = textSource.GetCharAt(nextPos - 1);
                char charAfter = textSource.GetCharAt(nextPos);
                // Never stop in the middle of a surrogate pair.
                if (!char.IsSurrogatePair(charBefore, charAfter))
                {
                    var classBefore = GetCharacterClass(charBefore);
                    var classAfter = GetCharacterClass(charAfter);
                    // The correct class for characters outside the BMP comes from the whole pair.
                    if (char.IsLowSurrogate(charBefore) && nextPos >= 2)
                    {
                        classBefore = GetCharacterClass(textSource.GetCharAt(nextPos - 2), charBefore);
                    }
                    if (char.IsHighSurrogate(charAfter) && nextPos + 1 < textLength)
                    {
                        classAfter = GetCharacterClass(charAfter, textSource.GetCharAt(nextPos + 1));
                    }
                    if (StopBetweenCharacters(mode, classBefore, classAfter))
                    {
                        return nextPos;
                    }
                }
            }
            offset = nextPos;
        }
    }

    private static bool IsNormal(CaretPositioningMode mode)
        => mode is CaretPositioningMode.Normal or CaretPositioningMode.EveryCodepoint;

    private static bool StopBetweenCharacters(
        CaretPositioningMode mode, CharacterClass charBefore, CharacterClass charAfter)
    {
        if (mode == CaretPositioningMode.EveryCodepoint)
        {
            return true;
        }
        // Never stop in the middle of a grapheme.
        if (charAfter == CharacterClass.CombiningMark)
        {
            return false;
        }
        // Normal mode stops after every grapheme.
        if (mode == CaretPositioningMode.Normal)
        {
            return true;
        }
        if (charBefore == charAfter)
        {
            // The "OrSymbol" modes have a border and a start between any two unknown characters.
            return charBefore == CharacterClass.Other &&
                mode is CaretPositioningMode.WordBorderOrSymbol or CaretPositioningMode.WordStartOrSymbol;
        }
        // A class change is a possible border. Word-start modes reject the end of a word, which is
        // where whitespace follows; plain border modes accept unconditionally.
        if ((mode == CaretPositioningMode.WordStart || mode == CaretPositioningMode.WordStartOrSymbol)
            && charAfter is CharacterClass.Whitespace or CharacterClass.LineTerminator)
        {
            return false;
        }
        return true;
    }
}
