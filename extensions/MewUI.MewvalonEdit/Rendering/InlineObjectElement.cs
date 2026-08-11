using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Places a UI element inline with the text, bottom-aligned to the baseline. The element is not
/// part of the editor's visual tree: it renders detached with default-theme styling, and input
/// reaches it only through this element's <see cref="VisualLineElement.OnMouseDown"/> hook.
/// </summary>
public class InlineObjectElement : VisualLineElement
{
    public InlineObjectElement(int documentLength, UIElement element)
        : base(1, documentLength)
        => Element = element ?? throw new ArgumentNullException(nameof(element));

    /// <summary>The hosted element.</summary>
    public UIElement Element { get; }

    public override InlineMetrics Measure(uint dpi)
    {
        Element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Element.DesiredSize;
        return new InlineMetrics(size.Width, size.Height, size.Height);
    }

    public override void Draw(ITextRenderContext context, Point origin, uint dpi)
    {
        ArgumentNullException.ThrowIfNull(context);
        Element.Arrange(new Rect(origin, Element.DesiredSize));
        Element.Render(context.Graphics);
    }
}
