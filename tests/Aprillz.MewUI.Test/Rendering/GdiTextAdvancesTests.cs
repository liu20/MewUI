using Aprillz.MewUI.Rendering.Gdi;
using Aprillz.MewUI.Text;

namespace MewUI.Test.Rendering;

[TestClass]
public sealed class GdiTextAdvancesTests
{
    [TestMethod]
    public void GetUtf16PrefixAdvances_ReturnsMonotonicDrawMetrics()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI is Windows-only.");
            return;
        }

        const string text = "office 한글 😀";
        using var factory = new GdiGraphicsFactory();
        using var context = ((ITextBackendFactory)factory).CreateTextMeasurementContext(144);
        using var font = (GdiFont)factory.CreateFont("Segoe UI", 16, 144);

        var cumulative = context.GetUtf16PrefixAdvances(text, font)!;

        Assert.HasCount(text.Length, cumulative);
        Assert.IsGreaterThan(0, cumulative[^1]);
        for (int i = 1; i < cumulative.Length; i++)
        {
            Assert.IsGreaterThanOrEqualTo(cumulative[i - 1], cumulative[i]);
        }

        var measured = context.Measure(text, font);
        Assert.AreEqual(measured.Width, cumulative[^1], 1.0,
            "Prefix extents and the GDI draw measurement must use the same horizontal metric source.");
    }
}
