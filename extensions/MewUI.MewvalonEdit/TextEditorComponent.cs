using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit;

/// <summary>
/// What the editor, its text area and its view have in common, so ported code can take any of the
/// three and still reach the document, the options and the registered services.
/// </summary>
public interface ITextEditorComponent
{
    /// <summary>The registered service, or null when neither this component nor the document has it.</summary>
    TService? GetService<TService>() where TService : class;

    TextDocument Document { get; }

    /// <summary>Raised when the document is replaced, not when its content changes.</summary>
    event EventHandler? DocumentChanged;

    TextEditorOptions Options { get; }

    /// <summary>Raised when an option changed, carrying the option that did.</summary>
    event EventHandler<MewProperty>? OptionChanged;
}
