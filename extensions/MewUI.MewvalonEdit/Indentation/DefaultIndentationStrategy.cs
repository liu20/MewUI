using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Indentation;

public class DefaultIndentationStrategy : IIndentationStrategy
{
    public virtual void IndentLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        if (line.LineNumber <= 1) return;

        var previous = document.GetLineByNumber(line.LineNumber - 1);
        string previousText = document.GetText(previous.Offset, previous.Length);
        string currentText = document.GetText(line.Offset, line.Length);
        string indentation = TakeIndentation(previousText);
        int currentIndentationLength = TakeIndentation(currentText).Length;
        document.Replace(line.Offset, currentIndentationLength, indentation);
    }

    /// <summary>
    /// Does nothing, as the original leaves it. Copying the previous line's indentation is the
    /// right answer for one new line and the wrong one for a block: run down a range and each line
    /// takes what the line above it was just given, flattening the whole block to the indentation
    /// it started at. Reindenting a block needs a strategy that reads the language.
    /// </summary>
    public virtual void IndentLines(TextDocument document, int beginLine, int endLine)
    {
    }

    private static string TakeIndentation(string text)
    {
        int length = 0;
        while (length < text.Length && (text[length] == ' ' || text[length] == '\t')) length++;
        return text[..length];
    }
}
