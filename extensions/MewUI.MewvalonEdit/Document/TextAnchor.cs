namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>
/// A position that rides along with the text around it. Holding one is how code keeps a place in
/// the document through edits it did not make.
/// </summary>
public interface ITextAnchor
{
    /// <summary>Line and column of the anchor.</summary>
    TextLocation Location { get; }

    /// <summary>Offset of the anchor.</summary>
    int Offset { get; }

    /// <summary>Where the anchor goes when text is inserted at its exact offset.</summary>
    AnchorMovementType MovementType { get; set; }

    /// <summary>Whether the anchor survives text being deleted around it instead of dying with it.</summary>
    bool SurviveDeletion { get; set; }

    /// <summary>Whether the text the anchor sat in was deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Raised once, when the text the anchor sat in is deleted.</summary>
    event EventHandler? Deleted;

    /// <summary>Line the anchor is on.</summary>
    int Line { get; }

    /// <summary>Column the anchor is at.</summary>
    int Column { get; }
}

/// <summary>An anchor created by <see cref="TextDocument.CreateAnchor"/>.</summary>
public sealed class TextAnchor : ITextAnchor
{
    private readonly TextDocument _document;
    private int _offset;

    internal TextAnchor(TextDocument document, int offset)
    {
        _document = document;
        _offset = offset;
    }

    public AnchorMovementType MovementType { get; set; }
    public bool SurviveDeletion { get; set; }
    public bool IsDeleted { get; private set; }

    public event EventHandler? Deleted;

    public int Offset => IsDeleted
        ? throw new InvalidOperationException("The text this anchor was in has been deleted.")
        : _offset;

    public TextLocation Location => _document.GetLocation(Offset);

    public int Line => Location.Line;

    public int Column => Location.Column;

    /// <summary>Moves the anchor across one change, or kills it if its text went away.</summary>
    internal void Update(in OffsetChangeMapEntry change)
    {
        if (IsDeleted)
        {
            return;
        }
        if (!SurviveDeletion && change.RemovalLength > 0 &&
            _offset > change.Offset && _offset < change.Offset + change.RemovalLength)
        {
            IsDeleted = true;
            Deleted?.Invoke(this, EventArgs.Empty);
            return;
        }
        _offset = change.GetNewOffset(_offset, MovementType);
    }
}
