namespace Aprillz.MewUI.MewvalonEdit.Document;

public interface ITextSource
{
    int TextLength { get; }
    char GetCharAt(int offset);
    string GetText(int offset, int length);

    /// <summary>Offset of the character in the range, or -1. Both searches take a range, not an end.</summary>
    int IndexOf(char value, int startIndex, int count);

    int LastIndexOf(char value, int startIndex, int count);

    /// <summary>Offset of the first of any of the characters in the range, or -1.</summary>
    int IndexOfAny(char[] anyOf, int startIndex, int count);

    int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType);

    int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType);
}

public interface ISegment
{
    int Offset { get; }
    int Length { get; }
    int EndOffset => Offset + Length;
}

public readonly record struct SimpleSegment(int Offset, int Length) : ISegment
{
    /// <summary>The segment no range can be, used where a range is missing.</summary>
    public static readonly SimpleSegment Invalid = new(-1, -1);

    public int EndOffset => Offset + Length;

    /// <summary>
    /// The overlapping portion of the segments, or <see cref="Invalid"/> when they do not overlap.
    /// </summary>
    public static SimpleSegment GetOverlap(ISegment segment1, ISegment segment2)
    {
        int start = Math.Max(segment1.Offset, segment2.Offset);
        int end = Math.Min(segment1.EndOffset, segment2.EndOffset);
        return end < start ? Invalid : new SimpleSegment(start, end - start);
    }
}

/// <summary>
/// A segment using <see cref="TextAnchor"/>s as start and end positions. For the constructors
/// creating new anchors, the start rides after insertions and the end stays before them. Should
/// the end move before the start, the segment has length 0.
/// </summary>
public sealed class AnchorSegment : ISegment
{
    private readonly TextAnchor _start;
    private readonly TextAnchor _end;

    public int Offset => _start.Offset;

    // Math.Max takes care of the fact that the end anchor might move before the start.
    public int Length => Math.Max(0, _end.Offset - _start.Offset);

    public int EndOffset => Math.Max(_start.Offset, _end.Offset);

    /// <summary>
    /// Wraps two existing anchors. Both must have <see cref="ITextAnchor.SurviveDeletion"/> set,
    /// since a dead anchor has no offset to answer with.
    /// </summary>
    public AnchorSegment(TextAnchor start, TextAnchor end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (!start.SurviveDeletion)
        {
            throw new ArgumentException("Anchors for AnchorSegment must use SurviveDeletion", nameof(start));
        }
        if (!end.SurviveDeletion)
        {
            throw new ArgumentException("Anchors for AnchorSegment must use SurviveDeletion", nameof(end));
        }
        _start = start;
        _end = end;
    }

    public AnchorSegment(TextDocument document, ISegment segment)
        : this(document, (segment ?? throw new ArgumentNullException(nameof(segment))).Offset, segment.Length)
    {
    }

    public AnchorSegment(TextDocument document, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(document);
        _start = document.CreateAnchor(offset);
        _start.SurviveDeletion = true;
        _start.MovementType = AnchorMovementType.AfterInsertion;
        _end = document.CreateAnchor(offset + length);
        _end.SurviveDeletion = true;
        _end.MovementType = AnchorMovementType.BeforeInsertion;
    }

    public override string ToString() => $"[Offset={Offset}, EndOffset={EndOffset}]";
}

/// <summary>A one-based line and column in a document. Ordered by line, then column.</summary>
public readonly record struct TextLocation(int Line, int Column) : IComparable<TextLocation>
{
    /// <summary>The location no line and column can be, used where a location is missing.</summary>
    public static readonly TextLocation Empty = new(0, 0);

    public bool IsEmpty => Line <= 0 && Column <= 0;

    public int CompareTo(TextLocation other)
        => Line != other.Line ? Line.CompareTo(other.Line) : Column.CompareTo(other.Column);

    public static bool operator <(TextLocation left, TextLocation right) => left.CompareTo(right) < 0;

    public static bool operator >(TextLocation left, TextLocation right) => left.CompareTo(right) > 0;

    public static bool operator <=(TextLocation left, TextLocation right) => left.CompareTo(right) <= 0;

    public static bool operator >=(TextLocation left, TextLocation right) => left.CompareTo(right) >= 0;
}

/// <summary>A replace that happened to a text source, with the text on both sides of it.</summary>
public class TextChangeEventArgs : EventArgs
{
    private readonly int _removalLength;

    public TextChangeEventArgs(int offset, string? removedText, string? insertedText)
        : this(offset, removedText, insertedText, (removedText ?? string.Empty).Length)
    {
    }

    /// <summary>
    /// Carries a removal length that the removed text does not account for. A wholesale text
    /// assignment is unrecorded and hands over no removed text, but every offset still has to move
    /// across the whole of it.
    /// </summary>
    public TextChangeEventArgs(int offset, string? removedText, string? insertedText, int removalLength)
    {
        Offset = offset >= 0
            ? offset
            : throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must not be negative.");
        RemovedText = new StringTextSource(removedText ?? string.Empty);
        InsertedText = new StringTextSource(insertedText ?? string.Empty);
        _removalLength = Math.Max(removalLength, RemovedText.TextLength);
    }

    public int Offset { get; }

    /// <summary>The text that was removed. Empty when the change did not materialize it.</summary>
    public ITextSource RemovedText { get; }

    /// <summary>The text that was inserted.</summary>
    public ITextSource InsertedText { get; }

    public int RemovalLength => _removalLength;
    public int InsertionLength => InsertedText.TextLength;

    /// <summary>Where <paramref name="offset"/> lands after this change.</summary>
    public virtual int GetNewOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        if (offset >= Offset && offset <= Offset + RemovalLength)
        {
            return movementType == AnchorMovementType.BeforeInsertion
                ? Offset
                : Offset + InsertionLength;
        }
        return offset > Offset ? offset + InsertionLength - RemovalLength : offset;
    }
}

/// <summary>A replace that happened to a document.</summary>
public class DocumentChangeEventArgs : TextChangeEventArgs
{
    public DocumentChangeEventArgs(int offset, string? removedText, string? insertedText)
        : base(offset, removedText, insertedText)
    {
    }

    /// <inheritdoc cref="TextChangeEventArgs(int, string, string, int)"/>
    public DocumentChangeEventArgs(int offset, string? removedText, string? insertedText, int removalLength)
        : base(offset, removedText, insertedText, removalLength)
    {
    }

    private OffsetChangeMap? _offsetChangeMap;

    /// <summary>The change as an offset map, so an offset can be carried across it.</summary>
    public OffsetChangeMap OffsetChangeMap
        => _offsetChangeMap ??= OffsetChangeMap.FromSingleElement(
            new OffsetChangeMapEntry(Offset, RemovalLength, InsertionLength));

    public override int GetNewOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
        => OffsetChangeMap.GetNewOffset(offset, movementType);
}
