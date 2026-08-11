using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Text;

/// <summary>
/// Backend-internal source of cumulative UTF-16 prefix advances. Values must come from the same
/// metric path used to position text during drawing.
/// </summary>
internal interface ITextAdvanceSource
{
    double[] GetUtf16PrefixAdvances(ReadOnlySpan<char> text, IFont font);
}
