namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>Which Y of a laid-out row a visual position refers to.</summary>
public enum VisualYPosition
{
    /// <summary>Top of the row.</summary>
    LineTop,

    /// <summary>
    /// Top of the text. Below the top of the row when the row holds something taller than the text.
    /// </summary>
    TextTop,

    /// <summary>Bottom of the row.</summary>
    LineBottom,

    /// <summary>Between the top and the bottom of the row.</summary>
    LineMiddle,

    /// <summary>
    /// Bottom of the text. Above the bottom of the row when the row holds something taller than it.
    /// </summary>
    TextBottom,

    /// <summary>Between the top and the bottom of the text.</summary>
    TextMiddle,

    /// <summary>Baseline of the text.</summary>
    Baseline
}
