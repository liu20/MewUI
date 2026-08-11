using System.Globalization;
using System.Text;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>
/// A set of font properties plus a foreground and background colour, named after the scope it
/// applies to.
/// </summary>
public class HighlightingColor : IEquatable<HighlightingColor>
{
    internal static readonly HighlightingColor Empty = new();

    /// <summary>The scope this colour applies to, or null when it was written inline.</summary>
    public string? Name { get; set; }

    public string? FontFamily { get; set; }
    public int? FontSize { get; set; }
    public FontWeight? FontWeight { get; set; }

    /// <summary>Italic flag. Null when the colour leaves the slant alone.</summary>
    public bool? Italic { get; set; }

    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }
    public Color? Foreground { get; set; }
    public Color? Background { get; set; }

    /// <summary>
    /// The colour this scope is drawn in. <see cref="HighlightingPalette"/> answers first, so one
    /// definition serves both themes; a scope it does not carry keeps the definition's own colour.
    /// </summary>
    internal Color? ResolveForeground(bool isDark)
    {
        if (Name is string scope
            && HighlightingPalette.Current.TryGet(scope, out var entry)
            && (isDark ? entry.Dark : entry.Light) is Color themed)
        {
            return themed;
        }
        return Foreground;
    }

    /// <inheritdoc cref="ResolveForeground"/>
    internal Color? ResolveBackground(bool isDark)
    {
        if (Name is string scope
            && HighlightingPalette.Current.TryGet(scope, out var entry)
            && (isDark ? entry.DarkBackground : entry.LightBackground) is Color themed)
        {
            return themed;
        }
        return Background;
    }

    /// <summary>CSS declarations equivalent to this colour, for exporting highlighted text.</summary>
    public virtual string ToCss()
    {
        var css = new StringBuilder();
        if (Foreground is Color foreground)
        {
            css.AppendFormat(CultureInfo.InvariantCulture, "color: #{0:x2}{1:x2}{2:x2}; ", foreground.R, foreground.G, foreground.B);
        }
        if (Background is Color background)
        {
            css.AppendFormat(CultureInfo.InvariantCulture, "background-color: #{0:x2}{1:x2}{2:x2}; ", background.R, background.G, background.B);
        }
        if (FontWeight is FontWeight weight)
        {
            css.Append(CultureInfo.InvariantCulture, $"font-weight: {(int)weight}; ");
        }
        if (Italic is bool italic)
        {
            css.Append(italic ? "font-style: italic; " : "font-style: normal; ");
        }
        if (Underline is bool underline)
        {
            css.Append(underline ? "text-decoration: underline; " : "text-decoration: none; ");
        }
        if (Strikethrough is bool strikethrough)
        {
            css.Append(strikethrough ? "text-decoration: line-through; " : "text-decoration: none; ");
        }
        return css.ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
        => "[" + GetType().Name + " " + (string.IsNullOrEmpty(Name) ? ToCss() : Name) + "]";

    /// <summary>Returns an independent copy of this colour.</summary>
    public virtual HighlightingColor Clone() => (HighlightingColor)MemberwiseClone();

    /// <summary>
    /// Takes every property the other colour sets, leaving this one's value where the other's is
    /// null. This is how a scope layers over the one beneath it.
    /// </summary>
    public void MergeWith(HighlightingColor color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (color.FontWeight is not null) FontWeight = color.FontWeight;
        if (color.Italic is not null) Italic = color.Italic;
        if (color.Foreground is not null) Foreground = color.Foreground;
        if (color.Background is not null) Background = color.Background;
        if (color.Underline is not null) Underline = color.Underline;
        if (color.Strikethrough is not null) Strikethrough = color.Strikethrough;
        if (color.FontFamily is not null) FontFamily = color.FontFamily;
        if (color.FontSize is not null) FontSize = color.FontSize;
    }

    /// <summary>Whether this colour would change nothing if merged into another.</summary>
    internal bool IsEmptyForMerge
        => FontWeight is null && Italic is null && Underline is null && Strikethrough is null
            && Foreground is null && Background is null && FontFamily is null && FontSize is null;

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => Equals(obj as HighlightingColor);

    /// <inheritdoc/>
    public virtual bool Equals(HighlightingColor? other)
        => other is not null
            && Name == other.Name && FontWeight == other.FontWeight && Italic == other.Italic
            && Underline == other.Underline && Strikethrough == other.Strikethrough
            && Foreground == other.Foreground && Background == other.Background
            && FontFamily == other.FontFamily && FontSize == other.FontSize;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(
            Name, FontWeight, Italic, Underline, Strikethrough, Foreground, Background,
            HashCode.Combine(FontFamily, FontSize));
}
