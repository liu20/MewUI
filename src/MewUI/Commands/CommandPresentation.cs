using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI;

/// <summary>
/// Provides bindable default presentation metadata for a <see cref="Command"/>.
/// </summary>
/// <remarks>
/// Presentation is independent of command routing, handlers, targets and input gestures. A single
/// underscore in <see cref="AccessText"/> marks the following character as an access key; a double
/// underscore represents a literal underscore.
/// </remarks>
public sealed class CommandPresentation : MewObject
{
    public static readonly MewProperty<string?> AccessTextProperty =
        MewProperty<string?>.Register<CommandPresentation>(nameof(AccessText), null,
            MewPropertyOptions.None,
            static (self, _, _) => self.OnAccessTextChanged());

    public static readonly MewProperty<IconTemplate?> IconProperty =
        MewProperty<IconTemplate?>.Register<CommandPresentation>(nameof(Icon), null,
            MewPropertyOptions.None,
            static (self, _, _) => self.Changed?.Invoke());

    private string? _displayText;
    private char _accessKey;
    private int _accessKeyIndex = -1;

    public CommandPresentation(string? accessText = null, IconTemplate? icon = null)
    {
        if (accessText != null)
        {
            AccessText = accessText;
        }

        if (icon != null)
        {
            Icon = icon;
        }
    }

    /// <summary>
    /// Gets or sets the default label including optional access-key markers.
    /// </summary>
    public string? AccessText
    {
        get => GetValue(AccessTextProperty);
        set => SetValue(AccessTextProperty, value);
    }

    /// <summary>
    /// Gets the current display label with access-key markers removed.
    /// </summary>
    public string? DisplayText => _displayText;

    /// <summary>
    /// Gets the current access key, or the null character when none is defined.
    /// </summary>
    public char AccessKey => _accessKey;

    /// <summary>
    /// Gets the underline index in <see cref="DisplayText"/>, or -1 when no access key exists.
    /// </summary>
    public int AccessKeyIndex => _accessKeyIndex;

    /// <summary>
    /// Gets or sets the reusable icon template. Each presenter builds its own visual at its own size.
    /// </summary>
    public IconTemplate? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    internal event Action? Changed;

    private void OnAccessTextChanged()
    {
        var rawText = AccessText;
        if (rawText == null)
        {
            _displayText = null;
            _accessKey = default;
            _accessKeyIndex = -1;
        }
        else
        {
            bool hasAccessKey = AccessKeyHelper.TryParse(rawText, out var accessKey, out var displayText);
            _displayText = displayText;
            _accessKey = hasAccessKey ? accessKey : default;
            _accessKeyIndex = hasAccessKey ? AccessKeyHelper.GetUnderlineIndex(rawText) : -1;
        }

        Changed?.Invoke();
    }
}

internal static class CommandPresentationWeakEvents
{
    internal static readonly WeakEventKey<CommandPresentation, Action> Changed = new(
        static (source, handler) => source.Changed += handler,
        static (source, handler) => source.Changed -= handler);
}
