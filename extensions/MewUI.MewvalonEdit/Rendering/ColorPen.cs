using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Stroke described by a colour, a thickness and a stroke style. Takes the place of the pens the
/// original editor exposes, in the colour terms the rest of this extension uses.
/// </summary>
public readonly record struct ColorPen(Color Color, double Thickness, StrokeStyle StrokeStyle)
{
    public ColorPen(Color color, double thickness = 1) : this(color, thickness, StrokeStyle.Default)
    {
    }

    /// <summary>Renderer-side stroke for this pen.</summary>
    internal Pen ToPen() => new(Color, Thickness, StrokeStyle);

    /// <summary>This pen with its thickness snapped to at least one whole device pixel.</summary>
    internal ColorPen SnapThickness(double dpiScale)
        => this with { Thickness = LayoutRounding.SnapThicknessToPixels(Thickness, dpiScale, 1) };

    /// <summary>
    /// Centre line for a stroke that is to cover whole device pixels starting at
    /// <paramref name="edge"/>. A stroke is centred on the coordinate it is given, so placing it on
    /// a snapped edge would split it across the pixels on either side.
    /// </summary>
    internal double SnapStrokeCenter(double edge, double dpiScale)
        => LayoutRounding.RoundToPixel(edge, dpiScale) + Thickness / 2;
}
