namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>One scope's colours, either of which may be left to the syntax definition.</summary>
public readonly record struct PaletteEntry(Color? Dark = null, Color? Light = null, Color? DarkBackground = null, Color? LightBackground = null);

/// <summary>
/// Colours for the scopes a syntax definition names, looked up while drawing. A scope is a
/// definition's <c>Color name</c>; one absent from here keeps the colour the definition gave it.
/// </summary>
public sealed class HighlightingPalette
{
    private readonly Dictionary<string, PaletteEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The palette the colorizers read. Replaceable, so a host can theme code its own way.</summary>
    public static HighlightingPalette Current { get; set; } = CreateDefault();

    /// <summary>Raised after any palette entry changed, or after <see cref="Current"/> was replaced.</summary>
    public static event EventHandler? CurrentChanged;

    public void Set(string scope, PaletteEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        _entries[scope] = entry;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGet(string scope, out PaletteEntry entry) => _entries.TryGetValue(scope, out entry);

    /// <summary>
    /// Starts out empty: every definition then draws in the colours it carries, which is what the
    /// original does. Fill it to re-theme a scope; the names to use are the <c>Color name</c>
    /// values in the .xshd files, reachable through <see cref="IHighlightingDefinition.NamedHighlightingColors"/>.
    /// </summary>
    private static HighlightingPalette CreateDefault() => new();
}
