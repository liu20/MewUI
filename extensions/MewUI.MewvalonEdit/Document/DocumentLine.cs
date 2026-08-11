using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>
/// A line of a document, read live. Every measurement is answered from the document at the moment
/// it is asked, so a line held across an edit reports where it is now rather than where it was.
/// </summary>
/// <remarks>
/// A line is identified by its number, not by the text it held. Where the original tracks the line
/// itself and renumbers it when lines are inserted above, this one keeps answering for the number
/// it was fetched with.
/// </remarks>
public sealed class DocumentLine : ISegment
{
    private readonly TextDocument _document;

    internal DocumentLine(TextDocument document, int lineNumber)
    {
        _document = document;
        LineNumber = lineNumber;
    }

    /// <summary>Whether the document no longer has a line with this number.</summary>
    public bool IsDeleted => LineNumber > _document.LineCount;

    /// <summary>One-based number of this line.</summary>
    public int LineNumber { get; }

    /// <summary>Offset the line starts at.</summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int Offset => Current.Offset;

    /// <summary>Length of the line without its terminator.</summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int Length => Current.Length;

    /// <summary>Length of the line including its terminator.</summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int TotalLength => Current.TotalLength;

    /// <summary>Length of the line's terminator, zero on the last line.</summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int DelimiterLength
    {
        get
        {
            var line = Current;
            return line.TotalLength - line.Length;
        }
    }

    /// <summary>Offset just past the text, before the terminator.</summary>
    /// <exception cref="InvalidOperationException">The line was deleted.</exception>
    public int EndOffset
    {
        get
        {
            var line = Current;
            return line.Offset + line.Length;
        }
    }

    /// <summary>The line after this one, or null at the end of the document.</summary>
    public DocumentLine? NextLine
        => LineNumber < _document.LineCount ? _document.GetLineByNumber(LineNumber + 1) : null;

    /// <summary>The line before this one, or null at the start of the document.</summary>
    public DocumentLine? PreviousLine
        => LineNumber > 1 ? _document.GetLineByNumber(LineNumber - 1) : null;

    public override string ToString()
        => IsDeleted
            ? "[DocumentLine deleted]"
            : $"[DocumentLine Number={LineNumber} Offset={Offset} Length={Length}]";

    private IReadOnlyDocumentLine Current
        => IsDeleted
            ? throw new InvalidOperationException("The line was deleted from the document.")
            : _document.CoreDocument.GetLineByNumber(LineNumber - 1);
}
