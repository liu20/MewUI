using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <param name="definition">Rule set whose regex matches become paint spans.</param>
public class HighlightingColorizer(IHighlightingDefinition definition) : DocumentColorizingTransformer
{
    private DocumentHighlighter? _highlighter;
    private TextDocument? _highlighterDocument;
    private int _lineNumberBeingColorized;
    private bool _isInHighlightingGroup;

    internal IHighlightingDefinition Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));

    protected override void ColorizeLine(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (CurrentContext is not ITextRunConstructionContext context)
        {
            return;
        }

        // From the view per line, so a theme switch repaints in the other palette without rebuild.
        bool isDark = context.TextView.IsDarkTheme;
        _lineNumberBeingColorized = line.LineNumber;
        var highlightedLine = GetHighlighter(context.Document).HighlightLine(line.LineNumber);
        _lineNumberBeingColorized = 0;
        // Sections carry document offsets and are ordered outermost first, so applying them in
        // order lets an inner section paint over the one enclosing it.
        foreach (var section in highlightedLine.Sections)
        {
            ApplyColorToElement(section.Offset, section.Length, section.Color, isDark);
        }
    }

    /// <summary>
    /// Advances the highlighting state to just above the first line in view before lines are
    /// built, so a state change above the viewport redraws before stale lines are reused.
    /// </summary>
    internal void OnVisualLineConstructionStarting(TextDocument document, int firstDocumentLine)
    {
        var highlighter = GetHighlighter(document);
        _lineNumberBeingColorized = Math.Clamp(firstDocumentLine - 1, 0, document.LineCount);
        if (!_isInHighlightingGroup)
        {
            highlighter.BeginHighlighting();
            _isInHighlightingGroup = true;
        }
        highlighter.UpdateHighlightingState(_lineNumberBeingColorized);
        _lineNumberBeingColorized = 0;
    }

    /// <summary>Closes the highlighting group opened when line construction started.</summary>
    internal void OnVisualLinesChanged()
    {
        if (_isInHighlightingGroup)
        {
            _highlighter?.EndHighlighting();
            _isInHighlightingGroup = false;
        }
    }

    /// <summary>Raised with the line range that must be repainted after a span crossed lines.</summary>
    public event HighlightingStateChangedEventHandler? HighlightingStateChanged;

    /// <summary>The highlighter for this document, built on first use and reused after.</summary>
    internal DocumentHighlighter GetHighlighter(TextDocument document)
    {
        if (_highlighter is null || !ReferenceEquals(_highlighterDocument, document))
        {
            if (_highlighter is DocumentHighlighter previous)
            {
                previous.HighlightingStateChanged -= OnHighlightStateChanged;
                previous.Dispose();
            }
            _isInHighlightingGroup = false;
            _highlighter = new DocumentHighlighter(document, Definition);
            _highlighter.HighlightingStateChanged += OnHighlightStateChanged;
            _highlighterDocument = document;
        }
        return _highlighter;
    }

    private void OnHighlightStateChanged(int fromLineNumber, int toLineNumber)
    {
        // Scanning the state up to the viewport raises one notification per line; lines at or
        // above the one being colorized are rebuilt by the ongoing top-to-bottom pass anyway, so
        // repainting them here would issue one full rebuild per scanned line (original guard).
        if (_lineNumberBeingColorized != 0 && toLineNumber <= _lineNumberBeingColorized)
        {
            return;
        }
        HighlightingStateChanged?.Invoke(fromLineNumber, toLineNumber);
    }

    /// <summary>Applies one highlighting color to a document range. Override to adjust how colors reach the view.</summary>
    protected virtual void ApplyColorToElement(int offset, int length, HighlightingColor color, bool isDarkTheme)
    {
        ArgumentNullException.ThrowIfNull(color);
        ChangeLinePart(offset, offset + length, element =>
        {
            var properties = element.TextRunProperties;
            if (color.ResolveForeground(isDarkTheme) is Color foreground)
            {
                properties.SetForegroundBrush(foreground);
            }
            if (color.ResolveBackground(isDarkTheme) is Color background)
            {
                properties.SetBackgroundBrush(background);
            }
            var decoration = TextDecoration.None;
            if (color.Underline == true) decoration |= TextDecoration.Underline;
            if (color.Strikethrough == true) decoration |= TextDecoration.Strikethrough;
            if (decoration != TextDecoration.None)
            {
                properties.SetTextDecorations(decoration);
            }
            // One facet at a time: a definition writes fontWeight="bold" with no family far more
            // often than it names one, and setting the family to nothing leaves the run with no
            // font at all.
            if (color.FontFamily is string fontFamily)
            {
                properties.SetFontFamily(fontFamily);
            }
            if (color.FontWeight is FontWeight weight)
            {
                properties.SetFontWeight(weight);
            }
            if (color.Italic is bool italic)
            {
                properties.SetItalic(italic);
            }
            if (color.FontSize is int fontSize)
            {
                properties.SetFontRenderingEmSize(fontSize);
            }
        });
    }
}
