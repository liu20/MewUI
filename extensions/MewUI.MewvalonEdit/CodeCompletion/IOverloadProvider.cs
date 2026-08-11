using System.ComponentModel;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

/// <summary>Provides the items for the <see cref="OverloadViewer"/>.</summary>
public interface IOverloadProvider : INotifyPropertyChanged
{
    /// <summary>The selected index.</summary>
    int SelectedIndex { get; set; }

    /// <summary>The number of overloads.</summary>
    int Count { get; }

    /// <summary>The text "SelectedIndex of Count".</summary>
    string CurrentIndexText { get; }

    /// <summary>The current header. A string renders wrapped; an element renders as is.</summary>
    object? CurrentHeader { get; }

    /// <summary>The current content. A string renders wrapped; an element renders as is.</summary>
    object? CurrentContent { get; }
}
