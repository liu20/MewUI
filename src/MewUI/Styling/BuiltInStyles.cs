using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI;

/// <summary>
/// Provides built-in named styles that can be referenced via <see cref="Control.StyleName"/>.
/// These styles are automatically registered in the application-level <see cref="StyleSheet"/>.
/// </summary>
public static class BuiltInStyles
{
    /// <summary>StyleName key for a flat (borderless) button.</summary>
    public const string FlatButton = "flat-button";

    /// <summary>StyleName key for an accent-colored button.</summary>
    public const string AccentButton = "accent-button";

    /// <summary>StyleName key for a ComboBox dropdown list popup.</summary>
    public const string ComboBoxPopup = "combobox-popup";

    /// <summary>StyleName key for a DatePicker calendar popup.</summary>
    public const string DatePickerPopup = "datepicker-popup";

    internal static void Register(StyleSheet sheet)
    {
        sheet.Define(FlatButton, CreateFlatButtonStyle);
        sheet.Define(AccentButton, CreateAccentButtonStyle);
        sheet.Define(ComboBoxPopup, CreateComboBoxPopupStyle);
        sheet.Define(DatePickerPopup, CreateDatePickerPopupStyle);
    }

    private static Style CreateFlatButtonStyle()
    {
        return Style.DeriveFromDefault<Button>(
            transitions:
            [
                Transition.Create(Control.BackgroundProperty),
                Transition.Create(TextElement.ForegroundProperty),
            ],
            setters:
            [
                Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonHoverBackground.WithAlpha(0)),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            triggers:
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonHoverBackground.WithAlpha(128)),
                    ],
                },
                // Keyboard focus: a flat button has no chrome, so an accent-tinted face is its focus signal.
                new StateTrigger
                {
                    Match = VisualStateFlags.Focused,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.Accent.WithAlpha(56)),
                    ],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Focused | VisualStateFlags.Hot,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.Accent.WithAlpha(88)),
                    ],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Pressed,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonPressedBackground),
                    ],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.None,
                    Exclude = VisualStateFlags.Enabled,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonDisabledBackground.WithAlpha(128)),
                        Setter.Create(TextElement.ForegroundProperty, t => t.Palette.DisabledText),
                    ],
                },
            ]);
    }

    private static Style CreateComboBoxPopupStyle()
    {
        return Style.DeriveFromDefault<ListBox>(
            setters:
            [
                Setter.Create(Control.BorderBrushProperty, t => t.Palette.ControlBorder.Lerp(t.Palette.Accent, 0.5)),
            ]);
    }

    private static Style CreateDatePickerPopupStyle()
    {
        return Style.DeriveFromDefault<Calendar>(
            setters:
            [
                Setter.Create(Control.BorderBrushProperty, t => t.Palette.ControlBorder.Lerp(t.Palette.Accent, 0.5)),
            ]);
    }

    private static Style CreateAccentButtonStyle()
    {
        return Style.DeriveFromDefault<Button>(
            transitions:
            [
                Transition.Create(Control.BackgroundProperty),
                Transition.Create(TextElement.ForegroundProperty),
            ],
            setters:
            [
                Setter.Create(Control.BackgroundProperty, t => t.Palette.Accent),
                Setter.Create(TextElement.ForegroundProperty, t => t.Palette.AccentText),
                Setter.Create(Control.BorderThicknessProperty, 0.0),
            ],
            triggers:
            [
                new StateTrigger
                {
                    Match = VisualStateFlags.Hot,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.Accent.Lerp(t.Palette.WindowBackground, 0.15)),
                    ],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.Pressed,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.Accent.Lerp(t.Palette.WindowBackground, 0.25)),
                    ],
                },
                new StateTrigger
                {
                    Match = VisualStateFlags.None,
                    Exclude = VisualStateFlags.Enabled,
                    Setters =
                    [
                        Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonDisabledBackground),
                        Setter.Create(TextElement.ForegroundProperty, t => t.Palette.DisabledText),
                    ],
                },
            ]);
    }
}
