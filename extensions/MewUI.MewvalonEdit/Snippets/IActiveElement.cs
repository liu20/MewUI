using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>An active element that lets the snippet stay interactive after insertion.</summary>
public interface IActiveElement
{
    /// <summary>Called when all snippet elements have been inserted.</summary>
    void OnInsertionCompleted();

    /// <summary>Called when the interactive mode is deactivated.</summary>
    void Deactivate(SnippetEventArgs e);

    /// <summary>Whether the element is editable, which is what the user selects with Tab.</summary>
    bool IsEditable { get; }

    /// <summary>The segment associated with this element. May be null.</summary>
    ISegment? Segment { get; }
}
