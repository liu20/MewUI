namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>
/// A point in a document's history. Offsets taken at one version can be carried to another, which
/// is how a caller holding an offset survives edits it did not make.
/// </summary>
public interface ITextSourceVersion
{
    /// <summary>Whether both checkpoints came from the same document. False for null.</summary>
    bool BelongsToSameDocumentAs(ITextSourceVersion? other);

    /// <summary>-1 when this is older than <paramref name="other"/>, 0 when equal, 1 when newer.</summary>
    int CompareAge(ITextSourceVersion other);

    /// <summary>Carries an offset from this checkpoint to <paramref name="other"/>, forwards or backwards.</summary>
    int MoveOffsetTo(ITextSourceVersion other, int oldOffset, AnchorMovementType movement = AnchorMovementType.Default);
}

/// <summary>
/// One link of a document's version chain. Each version holds the change that produced the next
/// one, so moving an offset between two versions walks the links between them.
/// </summary>
internal sealed class TextSourceVersion(object owner, int number) : ITextSourceVersion
{
    private OffsetChangeMapEntry _change;
    private TextSourceVersion? _next;

    public object Owner { get; } = owner;
    public int Number { get; } = number;

    /// <summary>Records the change leading away from this version and returns the version it produced.</summary>
    public TextSourceVersion Append(OffsetChangeMapEntry change)
    {
        _change = change;
        _next = new TextSourceVersion(Owner, Number + 1);
        return _next;
    }

    public bool BelongsToSameDocumentAs(ITextSourceVersion? other)
        => other is TextSourceVersion version && ReferenceEquals(version.Owner, Owner);

    public int CompareAge(ITextSourceVersion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is not TextSourceVersion version || !ReferenceEquals(version.Owner, Owner))
        {
            throw new ArgumentException("The versions belong to different documents.", nameof(other));
        }
        return Number.CompareTo(version.Number);
    }

    public int MoveOffsetTo(ITextSourceVersion other, int oldOffset, AnchorMovementType movement = AnchorMovementType.Default)
    {
        int age = CompareAge(other);
        var target = (TextSourceVersion)other;
        if (age < 0)
        {
            for (var version = this; version != target && version is not null; version = version._next)
            {
                oldOffset = version._change.GetNewOffset(oldOffset, movement);
            }
            return oldOffset;
        }
        if (age > 0)
        {
            // Walking back is not a reverse mapping: a deleted range has no place to return to, so
            // the offset lands where the change started, as AvalonEdit's inverted map does.
            for (var version = target; version != this && version is not null; version = version._next)
            {
                var change = version._change;
                oldOffset = new OffsetChangeMapEntry(change.Offset, change.InsertionLength, change.RemovalLength)
                    .GetNewOffset(oldOffset, movement);
            }
        }
        return oldOffset;
    }
}

/// <summary>An unchanging copy of a document's text.</summary>
public sealed class StringTextSource(string text) : ITextSource
{
    private readonly string _text = text ?? throw new ArgumentNullException(nameof(text));

    public int TextLength => _text.Length;

    public char GetCharAt(int offset) => _text[offset];

    public string GetText(int offset, int length) => _text.Substring(offset, length);

    public int IndexOf(char value, int startIndex, int count)
        => TextSourceSearch.IndexOf(this, value, startIndex, count);

    public int LastIndexOf(char value, int startIndex, int count)
        => TextSourceSearch.LastIndexOf(this, value, startIndex, count);

    public int IndexOfAny(char[] anyOf, int startIndex, int count)
        => TextSourceSearch.IndexOfAny(this, anyOf, startIndex, count);

    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        => TextSourceSearch.IndexOf(this, searchText, startIndex, count, comparisonType);

    public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        => TextSourceSearch.LastIndexOf(this, searchText, startIndex, count, comparisonType);

    public override string ToString() => _text;
}
