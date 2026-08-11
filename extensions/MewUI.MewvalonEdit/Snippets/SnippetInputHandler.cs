using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>
/// The stacked handler of the interactive snippet mode: Tab and Shift+Tab walk the editable
/// fields, Escape and Return end the mode. Every other key stays the editor's, which is what
/// keeps typing inside a field ordinary editing.
/// </summary>
internal sealed class SnippetInputHandler(InsertionContext context)
    : TextAreaStackedInputHandler(context.TextArea)
{
    public override void Attach()
    {
        base.Attach();
        SelectElement(FindNextEditableElement(-1, backwards: false));
    }

    public override void Detach()
    {
        base.Detach();
        context.Deactivate(new SnippetEventArgs(DeactivateReason.InputHandlerDetached));
    }

    public override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            context.Deactivate(new SnippetEventArgs(DeactivateReason.EscapePressed));
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            context.Deactivate(new SnippetEventArgs(DeactivateReason.ReturnPressed));
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // An exact comparison, as the original: Ctrl+Shift+Tab walks forwards.
            bool backwards = e.Modifiers == ModifierKeys.Shift;
            SelectElement(FindNextEditableElement(TextArea.Caret.Offset, backwards));
            e.Handled = true;
        }
    }

    private void SelectElement(IActiveElement? element)
    {
        if (element?.Segment is Document.ISegment segment)
        {
            // The original also sets the caret, because its selection does not move it; the
            // port's selection setter already leaves the caret at the selection's end, and a
            // separate caret move here would collapse the selection again.
            TextArea.Selection = Selection.Create(TextArea, segment);
        }
    }

    private IActiveElement? FindNextEditableElement(int offset, bool backwards)
    {
        var elements = context.ActiveElements.Where(
            static element => element.IsEditable && element.Segment is not null);
        if (backwards)
        {
            elements = elements.Reverse();
            foreach (var element in elements)
            {
                if (offset > element.Segment!.EndOffset)
                {
                    return element;
                }
            }
        }
        else
        {
            foreach (var element in elements)
            {
                if (offset < element.Segment!.Offset)
                {
                    return element;
                }
            }
        }
        // Wrap around; when walking backwards the sequence is already reversed, so the first is
        // the last element.
        return elements.FirstOrDefault();
    }
}
