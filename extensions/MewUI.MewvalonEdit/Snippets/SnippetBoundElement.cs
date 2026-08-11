using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>
/// An element that binds to a <see cref="SnippetReplaceableTextElement"/> and displays the same
/// text, following the user's edits to it.
/// </summary>
public class SnippetBoundElement : SnippetElement
{
    /// <summary>The element whose text this one mirrors.</summary>
    public SnippetReplaceableTextElement? TargetElement { get; set; }

    /// <summary>Converts the text before copying it.</summary>
    public virtual string ConvertText(string input) => input;

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (TargetElement is not SnippetReplaceableTextElement target)
        {
            return;
        }
        var start = context.Document.CreateAnchor(context.InsertionPosition);
        start.MovementType = AnchorMovementType.BeforeInsertion;
        start.SurviveDeletion = true;
        if (target.Text is string inputText)
        {
            context.InsertText(ConvertText(inputText));
        }
        var end = context.Document.CreateAnchor(context.InsertionPosition);
        end.MovementType = AnchorMovementType.BeforeInsertion;
        end.SurviveDeletion = true;
        var segment = new AnchorSegment(start, end);
        context.RegisterActiveElement(this, new BoundActiveElement(context, target, this, segment));
    }
}

internal sealed class BoundActiveElement(
    InsertionContext context,
    SnippetReplaceableTextElement targetSnippetElement,
    SnippetBoundElement boundElement,
    AnchorSegment segment) : IActiveElement
{
    private AnchorSegment _segment = segment;

    internal IReplaceableActiveElement? TargetElement { get; private set; }

    public bool IsEditable => false;

    public ISegment? Segment => _segment;

    public void OnInsertionCompleted()
    {
        TargetElement = context.GetActiveElement(targetSnippetElement) as IReplaceableActiveElement;
        if (TargetElement is not null)
        {
            TargetElement.TextChanged += OnTargetTextChanged;
        }
    }

    public void Deactivate(SnippetEventArgs e)
    {
    }

    private void OnTargetTextChanged(object? sender, EventArgs e)
    {
        // Don't copy text if the segments overlap - that would loop endlessly. It can happen when
        // the user deletes the text between the replaceable element and the bound one.
        if (TargetElement?.Segment is not ISegment targetSegment ||
            SimpleSegment.GetOverlap(_segment, targetSegment) != SimpleSegment.Invalid)
        {
            return;
        }
        int offset = _segment.Offset;
        int length = _segment.Length;
        string text = boundElement.ConvertText(TargetElement.Text);
        if (length != text.Length || text != context.Document.GetText(offset, length))
        {
            // Replace only when something actually changes; otherwise undo would gain an empty group.
            context.Document.Replace(offset, length, text);
            if (length == 0)
            {
                // Replacing an empty anchor segment with text won't enlarge it; recreate it.
                _segment = new AnchorSegment(context.Document, offset, text.Length);
            }
        }
    }
}
