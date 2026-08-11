using System.Collections.ObjectModel;

namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>How an anchor at the exact offset of an insertion moves.</summary>
public enum AnchorMovementType
{
    /// <summary>The anchor decides for itself; a plain insertion pushes it along.</summary>
    Default,

    /// <summary>The anchor stays in front of inserted text.</summary>
    BeforeInsertion,

    /// <summary>The anchor moves behind inserted text.</summary>
    AfterInsertion
}

/// <summary>One replace in a document, enough to move an offset across it.</summary>
public readonly struct OffsetChangeMapEntry(int offset, int removalLength, int insertionLength)
    : IEquatable<OffsetChangeMapEntry>
{
    public int Offset { get; } = offset;
    public int RemovalLength { get; } = removalLength;
    public int InsertionLength { get; } = insertionLength;

    /// <summary>Where <paramref name="oldOffset"/> lands after this change.</summary>
    public int GetNewOffset(int oldOffset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        // The two range tests below would both apply to an insertion at the offset itself, so that
        // case falls through to the movement type instead.
        if (RemovalLength != 0 || oldOffset != Offset)
        {
            if (oldOffset <= Offset)
            {
                return oldOffset;
            }
            if (oldOffset >= Offset + RemovalLength)
            {
                return oldOffset + InsertionLength - RemovalLength;
            }
        }
        // Default follows AfterInsertion here. The original decides it per change through a flag on
        // the entry, and every change a document raises leaves that flag clear.
        return movementType == AnchorMovementType.BeforeInsertion
            ? Offset
            : Offset + InsertionLength;
    }

    public bool Equals(OffsetChangeMapEntry other)
        => Offset == other.Offset
        && RemovalLength == other.RemovalLength
        && InsertionLength == other.InsertionLength;

    public override bool Equals(object? obj) => obj is OffsetChangeMapEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Offset, RemovalLength, InsertionLength);

    public static bool operator ==(OffsetChangeMapEntry left, OffsetChangeMapEntry right) => left.Equals(right);

    public static bool operator !=(OffsetChangeMapEntry left, OffsetChangeMapEntry right) => !left.Equals(right);
}

/// <summary>
/// The changes between two document versions, in order, so an offset can be carried across all of
/// them at once.
/// </summary>
public sealed class OffsetChangeMap : Collection<OffsetChangeMapEntry>
{
    public static readonly OffsetChangeMap Empty = new(frozen: true);

    private bool _frozen;

    public OffsetChangeMap()
    {
    }

    public OffsetChangeMap(IEnumerable<OffsetChangeMapEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            Items.Add(entry);
        }
    }

    private OffsetChangeMap(bool frozen) => _frozen = frozen;

    public static OffsetChangeMap FromSingleElement(OffsetChangeMapEntry entry)
    {
        var map = new OffsetChangeMap();
        map.Add(entry);
        map.Freeze();
        return map;
    }

    /// <summary>Whether the map rejects further edits.</summary>
    public bool IsFrozen => _frozen;

    /// <summary>Makes the map read-only, which is what lets a version share it with any caller.</summary>
    public void Freeze() => _frozen = true;

    /// <summary>Carries <paramref name="offset"/> through every entry in order.</summary>
    public int GetNewOffset(int offset, AnchorMovementType movementType = AnchorMovementType.Default)
    {
        for (int index = 0; index < Items.Count; index++)
        {
            offset = Items[index].GetNewOffset(offset, movementType);
        }
        return offset;
    }

    protected override void ClearItems()
    {
        ThrowIfFrozen();
        base.ClearItems();
    }

    protected override void InsertItem(int index, OffsetChangeMapEntry item)
    {
        ThrowIfFrozen();
        base.InsertItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        ThrowIfFrozen();
        base.RemoveItem(index);
    }

    protected override void SetItem(int index, OffsetChangeMapEntry item)
    {
        ThrowIfFrozen();
        base.SetItem(index, item);
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("This OffsetChangeMap is frozen.");
        }
    }
}
