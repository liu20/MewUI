using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>
/// Text element that is supposed to be replaced by the user.
/// Will register an <see cref="IReplaceableActiveElement"/>.
/// </summary>
public class SnippetReplaceableTextElement : SnippetTextElement
{
    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int start = context.InsertionPosition;
        base.Insert(context);
        int end = context.InsertionPosition;
        context.RegisterActiveElement(this, new ReplaceableActiveElement(context, start, end));
    }
}

/// <summary>Interface for the active element registered by <see cref="SnippetReplaceableTextElement"/>.</summary>
public interface IReplaceableActiveElement : IActiveElement
{
    /// <summary>The current text inside the element.</summary>
    string Text { get; }

    /// <summary>Occurs when the text inside the element changes.</summary>
    event EventHandler? TextChanged;
}

internal sealed class ReplaceableActiveElement : IReplaceableActiveElement
{
    internal readonly InsertionContext Context;
    private readonly int _startOffset;
    private readonly int _endOffset;
    private TextAnchor? _start;
    private TextAnchor? _end;
    private Renderer? _background;
    private Renderer? _foreground;
    internal bool IsCaretInside;

    public ReplaceableActiveElement(InsertionContext context, int startOffset, int endOffset)
    {
        Context = context;
        _startOffset = startOffset;
        _endOffset = endOffset;
    }

    public string Text { get; private set; } = string.Empty;

    public event EventHandler? TextChanged;

    public bool IsEditable => true;

    public ISegment? Segment
        => _start is null || _end is null || _start.IsDeleted || _end.IsDeleted
            ? null
            : new SimpleSegment(_start.Offset, Math.Max(0, _end.Offset - _start.Offset));

    public void OnInsertionCompleted()
    {
        // The anchors must be created here rather than in Insert: they should move only due to
        // user insertions, not due to insertions of the other snippet parts.
        _start = Context.Document.CreateAnchor(_startOffset);
        _start.MovementType = AnchorMovementType.BeforeInsertion;
        _end = Context.Document.CreateAnchor(_endOffset);
        _end.MovementType = AnchorMovementType.AfterInsertion;
        _start.Deleted += OnAnchorDeleted;
        _end.Deleted += OnAnchorDeleted;

        // The original uses weak events here to keep the document from holding the snippet layer
        // alive; the port unsubscribes symmetrically in Deactivate instead.
        Context.Document.TextChanged += OnDocumentTextChanged;

        _background = new Renderer { Layer = KnownLayer.Background, Element = this };
        _foreground = new Renderer { Layer = KnownLayer.Text, Element = this };
        Context.TextArea.TextView.BackgroundRenderers.Add(_background);
        Context.TextArea.TextView.BackgroundRenderers.Add(_foreground);
        Context.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        OnCaretPositionChanged(null, EventArgs.Empty);

        Text = GetText();
    }

    public void Deactivate(SnippetEventArgs e)
    {
        Context.Document.TextChanged -= OnDocumentTextChanged;
        if (_background is not null)
        {
            Context.TextArea.TextView.BackgroundRenderers.Remove(_background);
        }
        if (_foreground is not null)
        {
            Context.TextArea.TextView.BackgroundRenderers.Remove(_foreground);
        }
        Context.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
    }

    private void OnAnchorDeleted(object? sender, EventArgs e)
        => Context.Deactivate(new SnippetEventArgs(DeactivateReason.Deleted));

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (Segment is ISegment segment && _foreground is Renderer foreground)
        {
            bool newIsCaretInside = Context.TextArea.Caret.Offset >= segment.Offset
                && Context.TextArea.Caret.Offset <= segment.EndOffset;
            if (newIsCaretInside != IsCaretInside)
            {
                IsCaretInside = newIsCaretInside;
                Context.TextArea.TextView.InvalidateLayer(foreground.Layer);
            }
        }
    }

    private string GetText()
        => _start is null || _end is null || _start.IsDeleted || _end.IsDeleted
            ? string.Empty
            : Context.Document.GetText(_start.Offset, Math.Max(0, _end.Offset - _start.Offset));

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        string newText = GetText();
        if (Text != newText)
        {
            Text = newText;
            TextChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Paints the field: a translucent fill on the background layer, and while the caret is
    /// inside, an outline around the field and every copy bound to it. The original's outline is
    /// dotted; the port draws it solid, as the graphics contract carries no dash style.
    /// </summary>
    private sealed class Renderer : IBackgroundRenderer
    {
        private static readonly Color _fieldBackground = Color.FromArgb(102, 50, 205, 50);
        private static readonly Color _activeBorder = Color.FromArgb(255, 0, 0, 0);

        internal ReplaceableActiveElement? Element;

        public KnownLayer Layer { get; init; }

        public void Draw(TextView textView, IGraphicsContext context)
        {
            if (Element?.Segment is not ISegment segment)
            {
                return;
            }
            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                BorderThickness = 1
            };
            if (Layer == KnownLayer.Background)
            {
                builder.AddSegment(textView, segment);
                if (builder.CreateGeometry() is PathGeometry geometry)
                {
                    context.FillPath(geometry, _fieldBackground);
                }
            }
            else if (Element.IsCaretInside)
            {
                builder.AddSegment(textView, segment);
                foreach (var active in Element.Context.ActiveElements.OfType<BoundActiveElement>())
                {
                    if (ReferenceEquals(active.TargetElement, Element) && active.Segment is ISegment bound)
                    {
                        builder.AddSegment(textView, bound);
                        builder.CloseFigure();
                    }
                }
                if (builder.CreateGeometry() is PathGeometry geometry)
                {
                    context.DrawPath(geometry, _activeBorder, 1);
                }
            }
        }
    }
}
