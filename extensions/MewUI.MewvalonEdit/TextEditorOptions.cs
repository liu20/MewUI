using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI.MewvalonEdit;

/// <summary>
/// Editing and display options of an editor. Every member is virtual so a host can force a value,
/// and every value is a <see cref="MewProperty"/> so it can be bound to.
/// </summary>
public class TextEditorOptions : MewObject
{
    public static readonly MewProperty<int> IndentationSizeProperty =
        MewProperty<int>.Register<TextEditorOptions>(nameof(IndentationSize), 4);

    public static readonly MewProperty<bool> ConvertTabsToSpacesProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ConvertTabsToSpaces), false);

    public static readonly MewProperty<bool> EnableVirtualSpaceProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(EnableVirtualSpace), false);

    public static readonly MewProperty<bool> EnableRectangularSelectionProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(EnableRectangularSelection), true);

    public static readonly MewProperty<bool> AllowToggleOverstrikeModeProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(AllowToggleOverstrikeMode), false);

    public static readonly MewProperty<bool> EnableImeSupportProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(EnableImeSupport), true);

    public static readonly MewProperty<bool> HideCursorWhileTypingProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(HideCursorWhileTyping), true);

    public static readonly MewProperty<bool> ShowSpacesProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ShowSpaces), false);

    public static readonly MewProperty<bool> ShowTabsProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ShowTabs), false);

    public static readonly MewProperty<bool> ShowEndOfLineProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ShowEndOfLine), false);

    public static readonly MewProperty<bool> ShowBoxForControlCharactersProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ShowBoxForControlCharacters), true);

    public static readonly MewProperty<bool> ShowColumnRulerProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(ShowColumnRuler), false);

    public static readonly MewProperty<int> ColumnRulerPositionProperty =
        MewProperty<int>.Register<TextEditorOptions>(nameof(ColumnRulerPosition), 80);

    public static readonly MewProperty<bool> HighlightCurrentLineProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(HighlightCurrentLine), false);

    public static readonly MewProperty<bool> EnableHyperlinksProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(EnableHyperlinks), true);

    public static readonly MewProperty<bool> EnableEmailHyperlinksProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(EnableEmailHyperlinks), true);

    public static readonly MewProperty<bool> RequireControlModifierForHyperlinkClickProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(RequireControlModifierForHyperlinkClick), true);

    public static readonly MewProperty<bool> CutCopyWholeLineProperty =
        MewProperty<bool>.Register<TextEditorOptions>(nameof(CutCopyWholeLine), true);

    /// <summary>Raised after an option changed, carrying the option that did.</summary>
    public event EventHandler<MewProperty>? OptionChanged;

    public virtual int IndentationSize
    {
        get => GetValue(IndentationSizeProperty);
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(IndentationSizeProperty, value);
        }
    }

    public virtual bool ConvertTabsToSpaces
    {
        get => GetValue(ConvertTabsToSpacesProperty);
        set => SetValue(ConvertTabsToSpacesProperty, value);
    }

    /// <summary>Text that indents from column 1 to the next indentation level.</summary>
    public string IndentationString => GetIndentationString(1);

    /// <summary>
    /// Text that indents from <paramref name="column"/> to the next indentation level. Converted
    /// tabs fill only up to the next stop, so a tab pressed mid-column does not overshoot it.
    /// </summary>
    public virtual string GetIndentationString(int column)
    {
        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Value must be at least 1.");
        }
        return ConvertTabsToSpaces
            ? new string(' ', IndentationSize - ((column - 1) % IndentationSize))
            : "\t";
    }

    /// <summary>
    /// Whether the caret can go past the end of a line, into virtual space. A rectangular selection
    /// uses virtual space whatever this says, since it spans columns rather than offsets.
    /// </summary>
    public virtual bool EnableVirtualSpace
    {
        get => GetValue(EnableVirtualSpaceProperty);
        set => SetValue(EnableVirtualSpaceProperty, value);
    }

    /// <summary>Whether the box-selection keys and Alt+drag may start a rectangular selection.</summary>
    public virtual bool EnableRectangularSelection
    {
        get => GetValue(EnableRectangularSelectionProperty);
        set => SetValue(EnableRectangularSelectionProperty, value);
    }

    /// <summary>Whether Insert switches the editor between inserting and overwriting.</summary>
    public virtual bool AllowToggleOverstrikeMode
    {
        get => GetValue(AllowToggleOverstrikeModeProperty);
        set => SetValue(AllowToggleOverstrikeModeProperty, value);
    }

    /// <summary>Whether text can be composed in the editor through an input method.</summary>
    public virtual bool EnableImeSupport
    {
        get => GetValue(EnableImeSupportProperty);
        set => SetValue(EnableImeSupportProperty, value);
    }

    /// <summary>Whether the pointer is taken out of the way while text is being typed.</summary>
    public virtual bool HideCursorWhileTyping
    {
        get => GetValue(HideCursorWhileTypingProperty);
        set => SetValue(HideCursorWhileTypingProperty, value);
    }

    public virtual bool ShowSpaces
    {
        get => GetValue(ShowSpacesProperty);
        set => SetValue(ShowSpacesProperty, value);
    }

    /// <summary>Draws a vertical rule at <see cref="ColumnRulerPosition"/>.</summary>
    public virtual bool ShowColumnRuler
    {
        get => GetValue(ShowColumnRulerProperty);
        set => SetValue(ShowColumnRulerProperty, value);
    }

    /// <summary>Column the rule sits at, counted in wide spaces.</summary>
    public virtual int ColumnRulerPosition
    {
        get => GetValue(ColumnRulerPositionProperty);
        set => SetValue(ColumnRulerPositionProperty, value);
    }

    /// <summary>Paints the line holding the caret in the view's current-line colours.</summary>
    public virtual bool HighlightCurrentLine
    {
        get => GetValue(HighlightCurrentLineProperty);
        set => SetValue(HighlightCurrentLineProperty, value);
    }

    public virtual bool ShowTabs
    {
        get => GetValue(ShowTabsProperty);
        set => SetValue(ShowTabsProperty, value);
    }

    public virtual bool ShowEndOfLine
    {
        get => GetValue(ShowEndOfLineProperty);
        set => SetValue(ShowEndOfLineProperty, value);
    }

    /// <summary>
    /// Draws a control character as a box naming it, so an otherwise invisible character shows
    /// without reading as ordinary text. On by default, as in the original.
    /// </summary>
    public virtual bool ShowBoxForControlCharacters
    {
        get => GetValue(ShowBoxForControlCharactersProperty);
        set => SetValue(ShowBoxForControlCharactersProperty, value);
    }

    /// <summary>Turns web addresses in the text into links. On by default, as in the original.</summary>
    public virtual bool EnableHyperlinks
    {
        get => GetValue(EnableHyperlinksProperty);
        set => SetValue(EnableHyperlinksProperty, value);
    }

    /// <summary>Turns mail addresses in the text into links. On by default, as in the original.</summary>
    public virtual bool EnableEmailHyperlinks
    {
        get => GetValue(EnableEmailHyperlinksProperty);
        set => SetValue(EnableEmailHyperlinksProperty, value);
    }

    /// <summary>Whether a link needs Ctrl held to follow it, rather than a plain click.</summary>
    public virtual bool RequireControlModifierForHyperlinkClick
    {
        get => GetValue(RequireControlModifierForHyperlinkClickProperty);
        set => SetValue(RequireControlModifierForHyperlinkClickProperty, value);
    }

    /// <summary>Copying with nothing selected takes the whole line. On by default, as in the original.</summary>
    public virtual bool CutCopyWholeLine
    {
        get => GetValue(CutCopyWholeLineProperty);
        set => SetValue(CutCopyWholeLineProperty, value);
    }

    /// <summary>Raises <see cref="OptionChanged"/>. Override to see every option change.</summary>
    protected override void OnMewPropertyChanged(MewProperty property)
        => OptionChanged?.Invoke(this, property);
}
