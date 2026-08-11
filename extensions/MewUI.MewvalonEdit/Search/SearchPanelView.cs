using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Search;

/// <summary>
/// The panel the reader types into. AvalonEdit puts the same row of controls in an adorner over the
/// text area; here it is a child of the editor.s overlay layer, which keeps it inside the editor.s
/// input scope. Only the message it shows floats on an adorner.
/// </summary>
internal sealed class SearchPanelView
{
    private readonly SearchPanel _panel;
    private readonly TextBox _patternBox;
    private readonly ObservableValue<string> _patternSource;
    private readonly TextBlock _status;

    /// <summary>The element put on the editor.s overlay layer.</summary>
    public Border Root { get; }

    /// <summary>The message shown below the panel, on an adorner of its own.</summary>
    public Border MessageRoot { get; }

    public SearchPanelView(SearchPanel panel)
    {
        _panel = panel;
        Root = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6),
            Padding = new Thickness(8),
            BorderThickness = 1
        };
        Root.WithTheme(static (theme, root) =>
        {
            root.CornerRadius = theme.Metrics.ControlCornerRadius;
            root.Background = theme.Palette.ContainerBackground;
            root.BorderBrush = theme.Palette.ControlBorder;
            // The panel resolves against the editor, whose document font and ink would otherwise reach
            // these controls. It is chrome, so it takes the theme's own.
            root.Foreground = theme.Palette.WindowText;
            root.FontFamily(theme.Metrics.FontFamily).FontSize(theme.Metrics.FontSize);
        });
        _patternBox = new TextBox().Width(160).Placeholder("Find");
        // Bound rather than assigned from the changed event, and validated on the way back to the
        // source: a pattern that cannot be searched with makes the conversion throw, the binding
        // reports that as a validation error, and the box goes into its invalid state - which is how
        // the reader sees the trouble while still typing. The original arrives at the same state
        // through a validation rule on this box's binding.
        _patternSource = new ObservableValue<string>(panel.SearchPattern);
        _patternSource.Changed += () =>
        {
            _panel.SearchPattern = _patternSource.Value;
            UpdateStatus();
        };
        _patternBox.Bind(
            TextBox.TextProperty,
            _patternSource,
            static value => value,
            value => { _panel.ValidatePattern(value); return value; },
            BindingMode.TwoWay);
        // F3 and Escape belong to the editor.s input map, which the panel sits inside. Enter is the
        // panel.s own key: its map is nearer to the focused search box than the editor.s, so it wins
        // exactly inside the panel.
        Root.InputMap.Map(new KeyGesture(Key.Enter), () => { _panel.FindNext(); UpdateStatus(showPatternError: true); });
        Root.InputMap.Map(new KeyGesture(Key.Enter, ModifierKeys.Shift), () => { _panel.FindPrevious(); UpdateStatus(showPatternError: true); });

        // The message hangs below the panel on its own adorner rather than joining the panel, so
        // saying something does not resize the controls under the reader's hands. The original
        // places its message view against the search box for the same reason.
        new TextBlock()
            .Ref(out _status)
            .TextWrapping(TextWrapping.Wrap);
        MessageRoot = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6),
            Padding = new Thickness(8, 4, 8, 4),
            MaxWidth = 280,
            BorderThickness = 1,
            IsVisible = false,
            Child = _status
        };
        MessageRoot.WithTheme(static (theme, root) =>
        {
            root.CornerRadius = theme.Metrics.ControlCornerRadius;
            root.Background = theme.Palette.ContainerBackground;
            root.BorderBrush = theme.Palette.ControlBorder;
            root.Foreground = theme.Palette.WindowText;
            root.FontFamily(theme.Metrics.FontFamily).FontSize(theme.Metrics.FontSize);
        });

        var matchCase = OptionToggle(
            new TextBlock().Text("Aa"),
            panel.Localization.MatchCaseText,
            panel.MatchCase,
            value => _panel.MatchCase = value);
        var wholeWords = OptionToggle(
            new TextBlock()
                .WithTheme((t, c) => c.FontSize(t.Metrics.FontSizeSmall))
                .Inlines(new Run().Text("abc").Decoration(TextDecoration.Underline)),
            panel.Localization.MatchWholeWordsText,
            panel.WholeWords,
            value => _panel.WholeWords = value);
        var useRegex = OptionToggle(
            new TextBlock().Text(".*"),
            panel.Localization.UseRegexText,
            panel.UseRegex,
            value => _panel.UseRegex = value);

        Root.Child = new StackPanel()
            .Vertical()
            .Spacing(8)
            .Children(
                new StackPanel()
                    .Horizontal()
                    .Spacing(8)
                    .Children(
                        _patternBox,
                        GlyphButton(GlyphKind.ChevronUp, () => { _panel.FindPrevious(); UpdateStatus(showPatternError: true); }),
                        GlyphButton(GlyphKind.ChevronDown, () => { _panel.FindNext(); UpdateStatus(showPatternError: true); }),
                        GlyphButton(GlyphKind.Cross, _panel.Close)),
                new StackPanel()
                    .Horizontal()
                    .Spacing(6)
                    .Children(
                        matchCase, wholeWords, useRegex));
    }

    /// <summary>A search option as a compact toggle, its meaning carried by the tooltip.</summary>
    private ToggleButton OptionToggle(TextBlock glyph, string tooltip, bool initial, Action<bool> apply)
    {
        var toggle = new ToggleButton
        {
            Content = glyph.Center(),
            Padding = new Thickness(0),
            IsChecked = initial
        };
        return toggle
            .WithTheme((t, c) => c.Width(t.Metrics.BaseControlHeight))
            .ToolTip(tooltip)
            .OnCheckedChanged(value => { apply(value); UpdateStatus(); });
    }

    /// <summary>The walk and close buttons: a bare 20x20 square around a stroke glyph.</summary>
    private static Button GlyphButton(GlyphKind kind, Action onClick)
    {
        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Content = new GlyphElement { Kind = kind },
            Width = 20,
            Height = 20,
            MinHeight = 0,
            Padding = new Thickness(0)
        };
        button.StyleName = BuiltInStyles.FlatButton;
        return button.OnClick(onClick);
    }

    /// <summary>Puts the caret in the search box and selects what is there, as reopening should.</summary>
    public void Reactivate()
    {
        _patternSource.Value = _panel.SearchPattern;
        _patternBox.Focus();
        _patternBox.SelectAll();
    }

    /// <summary>
    /// What the status line says. While a pattern is being typed only the box's invalid state shows
    /// the trouble; the reason is spelled out once the reader asks to search, which is where the
    /// original reads it off the box as well.
    /// </summary>
    public void UpdateStatus(bool showPatternError = false)
    {
        string message;
        if (showPatternError && PatternError() is string error)
        {
            message = _panel.Localization.ErrorText + error;
        }
        else
        {
            message = _panel.Results.Count == 0 && _panel.SearchPattern.Length > 0
                ? _panel.Localization.NoMatchesFoundText
                : string.Empty;
        }
        _status.Text = message;
        MessageRoot.IsVisible = message.Length > 0;
    }

    /// <summary>
    /// Why the pattern cannot be searched with: what the box rejected while it was typed, or what
    /// the panel hit when an option change put the stored pattern out of use.
    /// </summary>
    private string? PatternError()
    {
        foreach (var error in _patternBox.ValidationErrors)
        {
            return error.Message;
        }
        return _panel.PatternError;
    }
}
