using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>Line context handed to transformers and generators while a visual line is built.</summary>
public interface ITextRunConstructionContext
{
    TextDocument Document { get; }

    /// <summary>View the line is being built for.</summary>
    TextView TextView { get; }

    /// <summary>Document line currently being built.</summary>
    DocumentLine CurrentDocumentLine { get; }

    /// <summary>Font the line is drawn with before any override. AvalonEdit's global text run properties.</summary>
    TextRunStyle DefaultStyle { get; }
}

/// <summary>Restyles ranges of a visual line. AvalonEdit's transformer contract.</summary>
public interface IVisualLineTransformer
{
    void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements);
}

/// <summary>Base for transformers that restyle ranges by visual column.</summary>
public abstract class ColorizingTransformer : IVisualLineTransformer
{
    /// <summary>Elements produced for the line being transformed.</summary>
    protected IList<VisualLineElement>? CurrentElements { get; private set; }

    public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        CurrentElements = elements;
        try
        {
            Colorize(context);
        }
        finally
        {
            CurrentElements = null;
        }
    }

    protected abstract void Colorize(ITextRunConstructionContext context);

    /// <summary>Applies <paramref name="action"/> to the given visual column range of the current line.</summary>
    protected void ChangeVisualElements(int visualStartColumn, int visualEndColumn, Action<VisualLineElement> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CurrentElements is null || visualEndColumn <= visualStartColumn)
        {
            return;
        }
        var element = new StyleOverrideElement(visualStartColumn, visualEndColumn - visualStartColumn);
        action(element);
        CurrentElements.Add(element);
    }
}

/// <summary>
/// Base for transformers that work in document offsets. Derived classes override
/// <see cref="ColorizeLine"/> and call <see cref="ChangeLinePart"/>.
/// </summary>
public abstract class DocumentColorizingTransformer : ColorizingTransformer
{
    private int _lineStartOffset;
    private int _lineEndOffset;

    protected ITextRunConstructionContext? CurrentContext { get; private set; }

    protected override void Colorize(ITextRunConstructionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CurrentContext = context;
        try
        {
            var line = context.CurrentDocumentLine;
            _lineStartOffset = line.Offset;
            _lineEndOffset = line.Offset + line.Length;
            ColorizeLine(line);
        }
        finally
        {
            CurrentContext = null;
        }
    }

    protected abstract void ColorizeLine(DocumentLine line);

    /// <summary>Restyles a document offset range inside the line being colorized.</summary>
    protected void ChangeLinePart(int startOffset, int endOffset, Action<VisualLineElement> action)
    {
        if (startOffset < _lineStartOffset || startOffset > _lineEndOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset,
                $"Value must be between {_lineStartOffset} and {_lineEndOffset}.");
        }
        if (endOffset < startOffset || endOffset > _lineEndOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset,
                $"Value must be between {startOffset} and {_lineEndOffset}.");
        }
        ChangeVisualElements(startOffset - _lineStartOffset, endOffset - _lineStartOffset, action);
    }
}
