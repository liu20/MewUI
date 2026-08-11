namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>
/// A colour written in an xshd file, before it is turned into a <see cref="HighlightingColor"/>.
/// </summary>
public sealed class XshdColor : XshdElement
{
    /// <summary>The scope this colour is registered under, or null when it is written inline.</summary>
    public string? Name { get; set; }

    public string? FontFamily { get; set; }
    public int? FontSize { get; set; }
    public Color? Foreground { get; set; }
    public Color? Background { get; set; }
    public FontWeight? FontWeight { get; set; }

    /// <summary>Italic flag. Null when the colour does not change the slant.</summary>
    public bool? Italic { get; set; }

    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }

    /// <summary>Text that demonstrates where the colour is used, for a settings UI.</summary>
    public string? ExampleText { get; set; }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitColor(this);
}
