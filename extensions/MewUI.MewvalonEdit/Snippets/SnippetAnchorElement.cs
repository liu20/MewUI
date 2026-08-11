using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>Creates a named anchor that can be accessed by other snippet elements.</summary>
public sealed class SnippetAnchorElement : SnippetElement
{
    /// <summary>The name of the anchor.</summary>
    public string Name { get; }

    public SnippetAnchorElement(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var start = context.Document.CreateAnchor(context.InsertionPosition);
        start.MovementType = AnchorMovementType.BeforeInsertion;
        start.SurviveDeletion = true;
        var segment = new AnchorSegment(start, start);
        context.RegisterActiveElement(this, new AnchorElement(segment, Name, context));
    }
}

/// <summary>The active element created by <see cref="SnippetAnchorElement"/>.</summary>
public sealed class AnchorElement : IActiveElement
{
    private readonly InsertionContext _context;
    private AnchorSegment _segment;

    public AnchorElement(AnchorSegment segment, string name, InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(context);
        _segment = segment;
        _context = context;
        Name = name;
    }

    public bool IsEditable => false;

    public ISegment? Segment => _segment;

    /// <summary>The name of the anchor.</summary>
    public string Name { get; }

    /// <summary>The text at the anchor.</summary>
    public string Text
    {
        get => _context.Document.GetText(_segment);
        set
        {
            int offset = _segment.Offset;
            int length = _segment.Length;
            _context.Document.Replace(offset, length, value);
            if (length == 0)
            {
                // Replacing an empty anchor segment with text won't enlarge it; recreate it.
                _segment = new AnchorSegment(_context.Document, offset, value.Length);
            }
        }
    }

    public void OnInsertionCompleted()
    {
    }

    public void Deactivate(SnippetEventArgs e)
    {
    }
}
