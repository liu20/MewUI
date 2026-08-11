namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>An element of a code snippet.</summary>
/// <remarks>
/// The original also carries ToTextRun() for WPF flow-document previews; the port leaves preview
/// rendering to the host, which has the element tree to walk.
/// </remarks>
public abstract class SnippetElement
{
    /// <summary>Performs insertion of the snippet.</summary>
    public abstract void Insert(InsertionContext context);
}

/// <summary>A snippet element that has sub-elements.</summary>
public class SnippetContainerElement : SnippetElement
{
    private readonly List<SnippetElement> _elements = [];

    /// <summary>The sub-elements, inserted in order.</summary>
    public IList<SnippetElement> Elements => _elements;

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var element in _elements)
        {
            element.Insert(context);
        }
    }
}

/// <summary>A text element in a snippet.</summary>
public class SnippetTextElement : SnippetElement
{
    /// <summary>The text to be inserted.</summary>
    public string? Text { get; set; }

    public override void Insert(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Text is string text)
        {
            context.InsertText(text);
        }
    }
}
