using System.Collections;
using System.Collections.ObjectModel;

namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>Segment whose offsets follow document edits while it lives in a <see cref="TextSegmentCollection{T}"/>.</summary>
public class TextSegment : ISegment
{
    public int StartOffset { get; set; }
    public int Length
    {
        get => EndOffset - StartOffset;
        set => EndOffset = StartOffset + Math.Max(0, value);
    }
    public int EndOffset { get; set; }

    int ISegment.Offset => StartOffset;
    int ISegment.Length => Length;

    public override string ToString() => $"[{StartOffset}..{EndOffset}]";
}

/// <summary>
/// Segments kept up to date across document edits, enumerated by start offset. AvalonEdit backs
/// this with an interval tree; this port keeps a sorted list, which is enough for the marker counts
/// editors hold in practice and still answers a start-offset lookup by bisection.
/// </summary>
public sealed class TextSegmentCollection<T> : ICollection<T> where T : TextSegment
{
    private readonly List<T> _segments = [];
    private readonly TextDocument? _document;

    public TextSegmentCollection()
    {
    }

    public TextSegmentCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.Changed += OnDocumentChanged;
    }

    public int Count => _segments.Count;
    bool ICollection<T>.IsReadOnly => false;

    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _segments.Insert(FindFirstIndexWithStartAfter(item.StartOffset), item);
    }

    public bool Remove(T item) => _segments.Remove(item);
    public void Clear() => _segments.Clear();
    public bool Contains(T item) => _segments.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _segments.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => _segments.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Segment at <paramref name="index"/> in start-offset order.</summary>
    public T this[int index] => _segments[index];

    /// <summary>
    /// Segments overlapping the given range, touching ones included, ordered by start offset. A
    /// segment an edit emptied still touches the edit, which is how the caller gets to drop it.
    /// </summary>
    public ReadOnlyCollection<T> FindOverlappingSegments(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        int end = offset + length;
        return new ReadOnlyCollection<T>(_segments
            .Where(segment => segment.StartOffset <= end && segment.EndOffset >= offset)
            .ToList());
    }

    public ReadOnlyCollection<T> FindOverlappingSegments(ISegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return FindOverlappingSegments(segment.Offset, segment.Length);
    }

    /// <summary>Segments covering the given offset.</summary>
    public ReadOnlyCollection<T> FindSegmentsContaining(int offset)
        => FindOverlappingSegments(offset, 0);

    public T? FindFirstSegmentWithStartAfter(int offset)
    {
        int index = FindFirstIndexWithStartAfter(offset);
        return index < _segments.Count ? _segments[index] : null;
    }

    /// <summary>
    /// Index of the first segment starting at or after <paramref name="offset"/>, or
    /// <see cref="Count"/> when none does.
    /// </summary>
    public int FindFirstIndexWithStartAfter(int offset)
    {
        int low = 0;
        int high = _segments.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_segments[middle].StartOffset < offset) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    /// <summary>Shifts the stored offsets for an edit. Called automatically when built with a document.</summary>
    public void UpdateOffsets(DocumentChangeEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        UpdateOffsets(e.Offset, e.RemovalLength, e.InsertionLength);
    }

    /// <remarks>
    /// The shift is monotonic, so segments that were in start order stay in it and the list needs
    /// no resorting.
    /// </remarks>
    private void UpdateOffsets(int offset, int removalLength, int insertionLength)
    {
        int delta = insertionLength - removalLength;
        int removalEnd = offset + removalLength;
        for (int index = _segments.Count - 1; index >= 0; index--)
        {
            var segment = _segments[index];
            segment.StartOffset = Shift(segment.StartOffset, offset, removalEnd, delta);
            segment.EndOffset = Shift(segment.EndOffset, offset, removalEnd, delta);
            if (segment.EndOffset < segment.StartOffset)
            {
                segment.EndOffset = segment.StartOffset;
            }
        }
    }

    private static int Shift(int position, int offset, int removalEnd, int delta)
    {
        if (position <= offset) return position;
        return position >= removalEnd ? position + delta : offset;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
        => UpdateOffsets(e.Offset, e.RemovalLength, e.InsertionLength);
}
