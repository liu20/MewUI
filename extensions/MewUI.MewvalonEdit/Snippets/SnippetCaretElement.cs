using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>Sets the caret position after interactive mode has finished.</summary>
public class SnippetCaretElement : SnippetElement
{
    private readonly bool _setCaretOnlyIfTextIsSelected;

    public SnippetCaretElement()
    {
    }

    /// <param name="setCaretOnlyIfTextIsSelected">
    /// If set to true, the caret is set only when some text was selected. This is useful when
    /// both SnippetCaretElement and SnippetSelectionElement are used in the same snippet.
    /// </param>
    public SnippetCaretElement(bool setCaretOnlyIfTextIsSelected)
    {
        _setCaretOnlyIfTextIsSelected = setCaretOnlyIfTextIsSelected;
    }

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_setCaretOnlyIfTextIsSelected || !string.IsNullOrEmpty(context.SelectedText))
        {
            SetCaret(context);
        }
    }

    internal static void SetCaret(InsertionContext context)
    {
        var pos = context.Document.CreateAnchor(context.InsertionPosition);
        pos.MovementType = AnchorMovementType.BeforeInsertion;
        pos.SurviveDeletion = true;
        context.Deactivated += (_, e) =>
        {
            if (e.Reason is DeactivateReason.ReturnPressed or DeactivateReason.NoActiveElements)
            {
                context.TextArea.Caret.Offset = pos.Offset;
            }
        };
    }
}

/// <summary>Inserts the previously selected text at the selection marker.</summary>
public class SnippetSelectionElement : SnippetElement
{
    /// <summary>The new indentation of the selected text, in indentation levels.</summary>
    public int Indentation { get; set; }

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string indent = string.Concat(Enumerable.Repeat(context.Tab, Indentation));
        string text = context.SelectedText.TrimStart(' ', '\t');
        text = text.Replace(context.LineTerminator, context.LineTerminator + indent);

        // Straight into the document rather than InsertText: the text carries its own
        // indentation, and InsertText would add the context's on top.
        context.Document.Insert(context.InsertionPosition, text);
        context.InsertionPosition += text.Length;

        if (string.IsNullOrEmpty(context.SelectedText))
        {
            SnippetCaretElement.SetCaret(context);
        }
    }
}
