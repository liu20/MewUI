using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Paints the selection in the editor's own colors, replacing the host's selection layer. Installed
/// only once any of <see cref="TextArea"/>'s selection appearance properties is set, so an editor
/// that leaves them alone keeps the theme's selection untouched.
/// </summary>
internal sealed class SelectionLayer(TextArea textArea) : ITextViewLayer
{
    public Color? Background { get; set; }
    public Color? Border { get; set; }

    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        var selection = textArea.Selection;
        if (selection.IsEmpty)
        {
            return;
        }

        // A hairline at every scale. A one-DIP stroke is 1.25 device pixels at 125% and lands on no
        // pixel boundary; rounding it up instead gives two pixels at 150%, and half of that reaches
        // past the top of the viewport on the first row and is clipped. One device pixel does
        // neither, and the builder insets by half the same value so the stroke stays centred.
        double dpiScale = textArea.TextView.DpiScale;
        var pen = Border is Color color
            ? new ColorPen(color, 1 / dpiScale).SnapThickness(dpiScale)
            : default;
        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            BorderThickness = pen.Thickness
        };
        foreach (var segment in selection.Segments)
        {
            builder.AddSegment(textArea.TextView, segment);
        }
        var geometry = builder.CreateGeometry();
        if (geometry is null)
        {
            return;
        }

        var graphics = context.Graphics;
        // Falls back to the theme so a layer left installed with its colors cleared paints what the
        // host would have. Without this, clearing SelectionBrush would erase the selection.
        graphics.FillPath(geometry, Background ?? textArea.Editor.ThemeSelectionBackground);
        if (Border.HasValue)
        {
            graphics.DrawPath(geometry, pen.Color, pen.Thickness);
        }
    }
}
