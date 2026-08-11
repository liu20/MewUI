using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>A code snippet that can be inserted into the text editor.</summary>
public class Snippet : SnippetContainerElement
{
    /// <summary>Inserts the snippet into the text area, as one undo step.</summary>
    public void Insert(TextArea textArea)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        var selection = textArea.Selection.SurroundingSegment;
        int insertionPosition = textArea.Caret.Offset;
        if (selection is ISegment selected)
        {
            // Use the selection start instead of the caret position, because the caret could be
            // at the end of the selection or anywhere inside. Removal of the selected text makes
            // the caret position invalid.
            insertionPosition = selected.Offset
                + TextUtilities.GetWhitespaceAfter(textArea.Document, selected.Offset).Length;
        }

        // The context snapshots the selected text and surroundings, so it exists before the
        // selection is removed.
        var context = new InsertionContext(textArea, insertionPosition);

        context.Document.RunUpdate(() =>
        {
            if (selection is ISegment removed)
            {
                textArea.Document.Remove(insertionPosition, removed.EndOffset - insertionPosition);
            }
            Insert(context);
            context.RaiseInsertionCompleted(EventArgs.Empty);
        });
    }
}
