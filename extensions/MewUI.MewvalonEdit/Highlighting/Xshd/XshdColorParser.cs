using System.Globalization;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>Reads the colour and font attributes an xshd file writes, in both file versions.</summary>
internal static class XshdColorParser
{
    private const string SYSTEM_COLOR_PREFIX = "SystemColors.";

    /// <summary>Parses a colour written as <c>#AARRGGBB</c>, <c>#RRGGBB</c>, or a colour name.</summary>
    /// <returns>Null when <paramref name="value"/> is empty, meaning the attribute was absent.</returns>
    public static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        value = value.Trim();
        if (value.StartsWith('#'))
        {
            return ParseHex(value.AsSpan(1), value);
        }
        if (value.StartsWith(SYSTEM_COLOR_PREFIX, StringComparison.Ordinal))
        {
            // A system colour is a scope by another name: the palette decides it, and until it
            // carries one the definition simply does not colour that element.
            string scope = value[SYSTEM_COLOR_PREFIX.Length..];
            return HighlightingPalette.Current.TryGet(scope, out var entry)
                ? entry.Dark ?? entry.Light
                : null;
        }
        if (NamedColors.TryGetValue(value, out var named))
        {
            return named;
        }
        throw new HighlightingDefinitionInvalidException($"Cannot parse color '{value}'.");
    }

    public static FontWeight? ParseFontWeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (Enum.TryParse<FontWeight>(value.Trim(), ignoreCase: true, out var weight))
        {
            return weight;
        }
        // The xshd schema also allows an OpenType weight number.
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            return (FontWeight)numeric;
        }
        throw new HighlightingDefinitionInvalidException($"Cannot parse font weight '{value}'.");
    }

    /// <summary>Parses the xshd <c>fontStyle</c> attribute into MewUI's italic flag.</summary>
    public static bool? ParseItalic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim().ToLowerInvariant() switch
        {
            "normal" => false,
            // MewUI draws one slanted face, so oblique and italic are the same request.
            "italic" or "oblique" => true,
            _ => throw new HighlightingDefinitionInvalidException($"Cannot parse font style '{value}'.")
        };
    }

    private static Color ParseHex(ReadOnlySpan<char> digits, string original)
    {
        // WPF writes alpha first; Color.FromHex reads it last, so the components are taken here.
        if (digits.Length == 8)
        {
            return Color.FromArgb(
                byte.Parse(digits[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(digits[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(digits[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        if (digits.Length == 6)
        {
            return Color.FromRgb(
                byte.Parse(digits[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(digits[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        throw new HighlightingDefinitionInvalidException($"Cannot parse color '{original}'.");
    }

    // The X11 names, which is what the WPF converter the original uses accepts.
    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AliceBlue"] = Color.FromRgb(240, 248, 255),
        ["AntiqueWhite"] = Color.FromRgb(250, 235, 215),
        ["Aqua"] = Color.FromRgb(0, 255, 255),
        ["Aquamarine"] = Color.FromRgb(127, 255, 212),
        ["Azure"] = Color.FromRgb(240, 255, 255),
        ["Beige"] = Color.FromRgb(245, 245, 220),
        ["Bisque"] = Color.FromRgb(255, 228, 196),
        ["Black"] = Color.FromRgb(0, 0, 0),
        ["BlanchedAlmond"] = Color.FromRgb(255, 235, 205),
        ["Blue"] = Color.FromRgb(0, 0, 255),
        ["BlueViolet"] = Color.FromRgb(138, 43, 226),
        ["Brown"] = Color.FromRgb(165, 42, 42),
        ["BurlyWood"] = Color.FromRgb(222, 184, 135),
        ["CadetBlue"] = Color.FromRgb(95, 158, 160),
        ["Chartreuse"] = Color.FromRgb(127, 255, 0),
        ["Chocolate"] = Color.FromRgb(210, 105, 30),
        ["Coral"] = Color.FromRgb(255, 127, 80),
        ["CornflowerBlue"] = Color.FromRgb(100, 149, 237),
        ["Cornsilk"] = Color.FromRgb(255, 248, 220),
        ["Crimson"] = Color.FromRgb(220, 20, 60),
        ["Cyan"] = Color.FromRgb(0, 255, 255),
        ["DarkBlue"] = Color.FromRgb(0, 0, 139),
        ["DarkCyan"] = Color.FromRgb(0, 139, 139),
        ["DarkGoldenrod"] = Color.FromRgb(184, 134, 11),
        ["DarkGray"] = Color.FromRgb(169, 169, 169),
        ["DarkGreen"] = Color.FromRgb(0, 100, 0),
        ["DarkKhaki"] = Color.FromRgb(189, 183, 107),
        ["DarkMagenta"] = Color.FromRgb(139, 0, 139),
        ["DarkOliveGreen"] = Color.FromRgb(85, 107, 47),
        ["DarkOrange"] = Color.FromRgb(255, 140, 0),
        ["DarkOrchid"] = Color.FromRgb(153, 50, 204),
        ["DarkRed"] = Color.FromRgb(139, 0, 0),
        ["DarkSalmon"] = Color.FromRgb(233, 150, 122),
        ["DarkSeaGreen"] = Color.FromRgb(143, 188, 143),
        ["DarkSlateBlue"] = Color.FromRgb(72, 61, 139),
        ["DarkSlateGray"] = Color.FromRgb(47, 79, 79),
        ["DarkTurquoise"] = Color.FromRgb(0, 206, 209),
        ["DarkViolet"] = Color.FromRgb(148, 0, 211),
        ["DeepPink"] = Color.FromRgb(255, 20, 147),
        ["DeepSkyBlue"] = Color.FromRgb(0, 191, 255),
        ["DimGray"] = Color.FromRgb(105, 105, 105),
        ["DodgerBlue"] = Color.FromRgb(30, 144, 255),
        ["Firebrick"] = Color.FromRgb(178, 34, 34),
        ["FloralWhite"] = Color.FromRgb(255, 250, 240),
        ["ForestGreen"] = Color.FromRgb(34, 139, 34),
        ["Fuchsia"] = Color.FromRgb(255, 0, 255),
        ["Gainsboro"] = Color.FromRgb(220, 220, 220),
        ["GhostWhite"] = Color.FromRgb(248, 248, 255),
        ["Gold"] = Color.FromRgb(255, 215, 0),
        ["Goldenrod"] = Color.FromRgb(218, 165, 32),
        ["Gray"] = Color.FromRgb(128, 128, 128),
        ["Green"] = Color.FromRgb(0, 128, 0),
        ["GreenYellow"] = Color.FromRgb(173, 255, 47),
        ["Honeydew"] = Color.FromRgb(240, 255, 240),
        ["HotPink"] = Color.FromRgb(255, 105, 180),
        ["IndianRed"] = Color.FromRgb(205, 92, 92),
        ["Indigo"] = Color.FromRgb(75, 0, 130),
        ["Ivory"] = Color.FromRgb(255, 255, 240),
        ["Khaki"] = Color.FromRgb(240, 230, 140),
        ["Lavender"] = Color.FromRgb(230, 230, 250),
        ["LavenderBlush"] = Color.FromRgb(255, 240, 245),
        ["LawnGreen"] = Color.FromRgb(124, 252, 0),
        ["LemonChiffon"] = Color.FromRgb(255, 250, 205),
        ["LightBlue"] = Color.FromRgb(173, 216, 230),
        ["LightCoral"] = Color.FromRgb(240, 128, 128),
        ["LightCyan"] = Color.FromRgb(224, 255, 255),
        ["LightGoldenrodYellow"] = Color.FromRgb(250, 250, 210),
        ["LightGray"] = Color.FromRgb(211, 211, 211),
        ["LightGreen"] = Color.FromRgb(144, 238, 144),
        ["LightPink"] = Color.FromRgb(255, 182, 193),
        ["LightSalmon"] = Color.FromRgb(255, 160, 122),
        ["LightSeaGreen"] = Color.FromRgb(32, 178, 170),
        ["LightSkyBlue"] = Color.FromRgb(135, 206, 250),
        ["LightSlateGray"] = Color.FromRgb(119, 136, 153),
        ["LightSteelBlue"] = Color.FromRgb(176, 196, 222),
        ["LightYellow"] = Color.FromRgb(255, 255, 224),
        ["Lime"] = Color.FromRgb(0, 255, 0),
        ["LimeGreen"] = Color.FromRgb(50, 205, 50),
        ["Linen"] = Color.FromRgb(250, 240, 230),
        ["Magenta"] = Color.FromRgb(255, 0, 255),
        ["Maroon"] = Color.FromRgb(128, 0, 0),
        ["MediumAquamarine"] = Color.FromRgb(102, 205, 170),
        ["MediumBlue"] = Color.FromRgb(0, 0, 205),
        ["MediumOrchid"] = Color.FromRgb(186, 85, 211),
        ["MediumPurple"] = Color.FromRgb(147, 112, 219),
        ["MediumSeaGreen"] = Color.FromRgb(60, 179, 113),
        ["MediumSlateBlue"] = Color.FromRgb(123, 104, 238),
        ["MediumSpringGreen"] = Color.FromRgb(0, 250, 154),
        ["MediumTurquoise"] = Color.FromRgb(72, 209, 204),
        ["MediumVioletRed"] = Color.FromRgb(199, 21, 133),
        ["MidnightBlue"] = Color.FromRgb(25, 25, 112),
        ["MintCream"] = Color.FromRgb(245, 255, 250),
        ["MistyRose"] = Color.FromRgb(255, 228, 225),
        ["Moccasin"] = Color.FromRgb(255, 228, 181),
        ["NavajoWhite"] = Color.FromRgb(255, 222, 173),
        ["Navy"] = Color.FromRgb(0, 0, 128),
        ["OldLace"] = Color.FromRgb(253, 245, 230),
        ["Olive"] = Color.FromRgb(128, 128, 0),
        ["OliveDrab"] = Color.FromRgb(107, 142, 35),
        ["Orange"] = Color.FromRgb(255, 165, 0),
        ["OrangeRed"] = Color.FromRgb(255, 69, 0),
        ["Orchid"] = Color.FromRgb(218, 112, 214),
        ["PaleGoldenrod"] = Color.FromRgb(238, 232, 170),
        ["PaleGreen"] = Color.FromRgb(152, 251, 152),
        ["PaleTurquoise"] = Color.FromRgb(175, 238, 238),
        ["PaleVioletRed"] = Color.FromRgb(219, 112, 147),
        ["PapayaWhip"] = Color.FromRgb(255, 239, 213),
        ["PeachPuff"] = Color.FromRgb(255, 218, 185),
        ["Peru"] = Color.FromRgb(205, 133, 63),
        ["Pink"] = Color.FromRgb(255, 192, 203),
        ["Plum"] = Color.FromRgb(221, 160, 221),
        ["PowderBlue"] = Color.FromRgb(176, 224, 230),
        ["Purple"] = Color.FromRgb(128, 0, 128),
        ["Red"] = Color.FromRgb(255, 0, 0),
        ["RosyBrown"] = Color.FromRgb(188, 143, 143),
        ["RoyalBlue"] = Color.FromRgb(65, 105, 225),
        ["SaddleBrown"] = Color.FromRgb(139, 69, 19),
        ["Salmon"] = Color.FromRgb(250, 128, 114),
        ["SandyBrown"] = Color.FromRgb(244, 164, 96),
        ["SeaGreen"] = Color.FromRgb(46, 139, 87),
        ["SeaShell"] = Color.FromRgb(255, 245, 238),
        ["Sienna"] = Color.FromRgb(160, 82, 45),
        ["Silver"] = Color.FromRgb(192, 192, 192),
        ["SkyBlue"] = Color.FromRgb(135, 206, 235),
        ["SlateBlue"] = Color.FromRgb(106, 90, 205),
        ["SlateGray"] = Color.FromRgb(112, 128, 144),
        ["Snow"] = Color.FromRgb(255, 250, 250),
        ["SpringGreen"] = Color.FromRgb(0, 255, 127),
        ["SteelBlue"] = Color.FromRgb(70, 130, 180),
        ["Tan"] = Color.FromRgb(210, 180, 140),
        ["Teal"] = Color.FromRgb(0, 128, 128),
        ["Thistle"] = Color.FromRgb(216, 191, 216),
        ["Tomato"] = Color.FromRgb(255, 99, 71),
        ["Transparent"] = Color.FromArgb(0, 255, 255, 255),
        ["Turquoise"] = Color.FromRgb(64, 224, 208),
        ["Violet"] = Color.FromRgb(238, 130, 238),
        ["Wheat"] = Color.FromRgb(245, 222, 179),
        ["White"] = Color.FromRgb(255, 255, 255),
        ["WhiteSmoke"] = Color.FromRgb(245, 245, 245),
        ["Yellow"] = Color.FromRgb(255, 255, 0),
        ["YellowGreen"] = Color.FromRgb(154, 205, 50)
    };
}
