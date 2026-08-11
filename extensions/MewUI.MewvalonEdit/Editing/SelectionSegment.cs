using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Editing;

/// <summary>
/// One selected range. Both constructors order their ends, so a segment built from a backwards drag
/// still reads forwards.
/// </summary>
public readonly record struct SelectionSegment : ISegment
{
    /// <summary>A range whose visual columns are unknown.</summary>
    public SelectionSegment(int startOffset, int endOffset)
    {
        StartOffset = Math.Min(startOffset, endOffset);
        EndOffset = Math.Max(startOffset, endOffset);
        StartVisualColumn = -1;
        EndVisualColumn = -1;
    }

    public SelectionSegment(int startOffset, int startVisualColumn, int endOffset, int endVisualColumn)
    {
        if (startOffset < endOffset || (startOffset == endOffset && startVisualColumn <= endVisualColumn))
        {
            StartOffset = startOffset;
            StartVisualColumn = startVisualColumn;
            EndOffset = endOffset;
            EndVisualColumn = endVisualColumn;
        }
        else
        {
            StartOffset = endOffset;
            StartVisualColumn = endVisualColumn;
            EndOffset = startOffset;
            EndVisualColumn = startVisualColumn;
        }
    }

    public int StartOffset { get; }

    public int EndOffset { get; }

    /// <summary>Visual column the range starts at, or -1 when it is not known.</summary>
    public int StartVisualColumn { get; }

    /// <summary>Visual column the range ends at, or -1 when it is not known.</summary>
    public int EndVisualColumn { get; }

    int ISegment.Offset => StartOffset;

    public int Length => EndOffset - StartOffset;
}
