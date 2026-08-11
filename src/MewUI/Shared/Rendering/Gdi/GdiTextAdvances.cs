using Aprillz.MewUI.Native;
using Aprillz.MewUI.Native.Structs;

namespace Aprillz.MewUI.Rendering.Gdi;

/// <summary>
/// Extracts the cumulative UTF-16 prefix extents used by the GDI text path.
/// Cluster normalization is intentionally left to the managed text engine.
/// </summary>
internal static class GdiTextAdvances
{
    public static unsafe double[] GetUtf16PrefixAdvances(
        nint hdc,
        GdiFont font,
        ReadOnlySpan<char> text,
        double dpiScale)
    {
        if (text.IsEmpty)
        {
            return [];
        }

        var cumulativePixels = new int[text.Length];
        var oldFont = Gdi32.SelectObject(hdc, font.Handle);
        try
        {
            fixed (char* textPointer = text)
            fixed (int* widthsPointer = cumulativePixels)
            {
                SIZE size;
                if (!Gdi32.GetTextExtentExPoint(
                    hdc,
                    textPointer,
                    text.Length,
                    int.MaxValue,
                    null,
                    widthsPointer,
                    &size))
                {
                    throw new InvalidOperationException("GetTextExtentExPointW failed.");
                }
            }
        }
        finally
        {
            Gdi32.SelectObject(hdc, oldFont);
        }

        double scale = dpiScale > 0 ? dpiScale : 1;
        var advances = new double[cumulativePixels.Length];
        for (int i = 0; i < cumulativePixels.Length; i++)
        {
            advances[i] = cumulativePixels[i] / scale;
        }

        return advances;
    }
}
