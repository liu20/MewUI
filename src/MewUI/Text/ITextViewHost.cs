namespace Aprillz.MewUI.Text;

/// <summary>Contract of a control that presents a text document through the extension pipeline.</summary>
public interface ITextViewHost
{
    /// <summary>Document whose text the view presents.</summary>
    IReadOnlyTextDocument Document { get; }

    /// <summary>Extension pipeline applied when visible lines are laid out.</summary>
    TextViewExtensionPipeline Extensions { get; }

    /// <summary>Raised after the document content changed or the document was replaced.</summary>
    event Action<ITextViewHost>? DocumentChanged;

    /// <summary>Re-runs registered classifiers, generators, projections, and layers.</summary>
    void InvalidateTextView();

    /// <summary>
    /// Rebuilds only the lines overlapping the document range, leaving every other cached line in
    /// place. Safe to call while lines are being built; the rebuild then runs once that finishes.
    /// </summary>
    void InvalidateTextRange(int offset, int length);

    /// <summary>
    /// Raised before the visible lines are built, carrying the first line number. Extensions that
    /// carry state across lines check it here, before any line is reused.
    /// </summary>
    event Action<ITextViewHost, int>? LineConstructionStarting;

    /// <summary>Raised after the visible lines were built.</summary>
    event Action<ITextViewHost>? LinesChanged;

    /// <summary>Lines currently laid out, in document order, with their document-space positions.</summary>
    IReadOnlyList<TextLineLayout> VisibleTextLines { get; }

    /// <summary>
    /// The laid-out line holding the offset, laying it out when it is outside the viewport. Null
    /// when there is no line to lay out. A line long enough to be cut into slices answers with the
    /// slice the offset falls in. The layout follows the current viewport, so a wrapping view
    /// answers meaningfully only once it has been given a width.
    /// </summary>
    TextLineLayout? GetLineLayout(int documentOffset);

    /// <summary>Area the text is drawn into, excluding chrome.</summary>
    Rect TextViewportBounds { get; }

    /// <summary>Height of the whole document in view coordinates.</summary>
    double ExtentHeight { get; }

    /// <summary>
    /// Width of the widest line laid out so far, in view coordinates. Lines that have not been laid
    /// out yet count as empty, so the value grows as more of the document is reached.
    /// </summary>
    double ExtentWidth { get; }

    /// <summary>Height of a line holding one character in the view's own style, independent of content.</summary>
    double DefaultLineHeight { get; }

    /// <summary>Baseline of a line holding one character in the view's own style.</summary>
    double DefaultBaseline { get; }

    /// <summary>Line number whose row contains the document-space <paramref name="documentY"/>.</summary>
    int FindLineByY(double documentY);

    /// <summary>Document-space top of <paramref name="lineNumber"/>.</summary>
    double GetLineY(int lineNumber);

    /// <summary>Scroll offset of the view in document coordinates.</summary>
    Point ScrollOffset { get; }

    /// <summary>Raised after <see cref="ScrollOffset"/> changed.</summary>
    event Action<ITextViewHost>? ScrollOffsetChanged;

    /// <summary>
    /// Scrolls the smallest amount that brings the document-space rectangle into view. A rectangle
    /// taller or wider than the viewport is centred on that axis instead.
    /// </summary>
    void MakeVisible(Rect documentRect);

    /// <summary>Draw order of the view. Replacing a built-in anchor hands its painting to the caller.</summary>
    TextViewLayerStack Layers { get; }

    /// <summary>Inserts a layer relative to a built-in anchor.</summary>
    void InsertLayer(ITextViewLayer layer, TextViewLayerAnchor anchor, TextLayerPosition position);

    /// <summary>
    /// Discards what the anchor group drew and repaints it, leaving every line layout in place. A
    /// host that does not cache rendered layers may repaint the whole stack instead.
    /// </summary>
    void InvalidateLayer(TextViewLayerAnchor anchor);
}
