using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

/// <summary>
/// Represents a text between "Up" and "Down" buttons: the index row walks the overloads and the
/// header and content below it show the selected one. The index row hides itself when there is
/// only one overload to show.
/// </summary>
public class OverloadViewer : Control
{
    private const string PART_INDEX_PANEL = "PART_IndexPanel";
    private const string PART_INDEX_TEXT = "PART_IndexText";
    private const string PART_HEADER = "PART_Header";
    private const string PART_CONTENT = "PART_Content";

    private StackPanel? _indexPanel;
    private TextBlock? _indexText;
    private Border? _headerHost;
    private Border? _contentHost;

    public static readonly MewProperty<IOverloadProvider?> ProviderProperty =
        MewProperty<IOverloadProvider?>.Register<OverloadViewer>(nameof(Provider), null,
            MewPropertyOptions.AffectsLayout,
            static (self, oldValue, newValue) => self.OnProviderChanged(oldValue, newValue));

    public OverloadViewer()
    {
        Template = new DelegateControlTemplate<OverloadViewer>(BuildTemplate);
    }

    /// <summary>The item provider. The viewer follows its property changes.</summary>
    public IOverloadProvider? Provider
    {
        get => GetValue(ProviderProperty);
        set => SetValue(ProviderProperty, value);
    }

    private static Element BuildTemplate(OverloadViewer owner, ControlTemplateContext context)
    {
        var indexText = new TextBlock { Margin = new Thickness(2, 0, 2, 0) };
        context.Register(PART_INDEX_TEXT, indexText);

        var indexPanel = new StackPanel()
            .Horizontal()
            .Children(
                GlyphButton(GlyphKind.ChevronUp, () => owner.ChangeIndex(-1)),
                indexText.CenterVertical(),
                GlyphButton(GlyphKind.ChevronDown, () => owner.ChangeIndex(+1)));
        indexPanel.Margin = new Thickness(0, 0, 4, 0);
        context.Register(PART_INDEX_PANEL, indexPanel);

        var headerHost = new Border();
        context.Register(PART_HEADER, headerHost);
        var contentHost = new Border();
        context.Register(PART_CONTENT, contentHost);

        return new StackPanel()
            .Vertical()
            .Children(
                new StackPanel().Horizontal().Children(indexPanel, headerHost),
                contentHost);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _indexPanel = GetTemplateChild<StackPanel>(PART_INDEX_PANEL);
        _indexText = GetTemplateChild<TextBlock>(PART_INDEX_TEXT);
        _headerHost = GetTemplateChild<Border>(PART_HEADER);
        _contentHost = GetTemplateChild<Border>(PART_CONTENT);
        Refresh();
    }

    /// <summary>Changes the selected index, wrapping around at either end.</summary>
    /// <param name="relativeIndexChange">The relative index change - usual values are +1 or -1.</param>
    public void ChangeIndex(int relativeIndexChange)
    {
        if (Provider is IOverloadProvider provider)
        {
            int newIndex = provider.SelectedIndex + relativeIndexChange;
            if (newIndex < 0)
            {
                newIndex = provider.Count - 1;
            }
            if (newIndex >= provider.Count)
            {
                newIndex = 0;
            }
            provider.SelectedIndex = newIndex;
        }
    }

    private void OnProviderChanged(IOverloadProvider? oldValue, IOverloadProvider? newValue)
    {
        if (oldValue is IOverloadProvider previous)
        {
            previous.PropertyChanged -= OnProviderPropertyChanged;
        }
        if (newValue is IOverloadProvider current)
        {
            current.PropertyChanged += OnProviderPropertyChanged;
        }
        Refresh();
    }

    private void OnProviderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Refresh();

    private void Refresh()
    {
        if (_indexPanel is null)
        {
            return;
        }

        var provider = Provider;
        // The index row means nothing with a single overload, as the original's converter hides it.
        _indexPanel.IsVisible = provider?.Count > 1;
        _indexText!.Text = provider?.CurrentIndexText ?? string.Empty;
        _headerHost!.Child = BuildContent(provider?.CurrentHeader);
        _contentHost!.Child = BuildContent(provider?.CurrentContent);
    }

    /// <summary>A string renders as wrapped text; an element renders as is; anything else by its text.</summary>
    private static FrameworkElement? BuildContent(object? value) => value switch
    {
        null => null,
        FrameworkElement element => element,
        string text => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        _ => new TextBlock { Text = value.ToString() ?? string.Empty, TextWrapping = TextWrapping.Wrap },
    };

    private static Button GlyphButton(GlyphKind kind, Action onClick)
    {
        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Content = new GlyphElement { Kind = kind },
            Width = 14,
            Height = 14,
            MinHeight = 0,
            Padding = new Thickness(0)
        };
        button.StyleName = BuiltInStyles.FlatButton;
        return button.OnClick(onClick);
    }
}
