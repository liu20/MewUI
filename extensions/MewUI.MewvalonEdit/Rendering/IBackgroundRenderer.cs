using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>Draw order of a background renderer, matching AvalonEdit's KnownLayer.</summary>
public enum KnownLayer
{
    /// <summary>Below the selection highlight.</summary>
    Background,

    /// <summary>Above the selection highlight, below the text.</summary>
    Selection,

    /// <summary>Above the text, below the caret.</summary>
    Text,

    /// <summary>Above the caret.</summary>
    Caret
}

/// <summary>Paints into one of the editor's known layers. The AvalonEdit signature with the WPF drawing context replaced by <see cref="IGraphicsContext"/>.</summary>
public interface IBackgroundRenderer
{
    KnownLayer Layer { get; }

    void Draw(TextView textView, IGraphicsContext context);
}
