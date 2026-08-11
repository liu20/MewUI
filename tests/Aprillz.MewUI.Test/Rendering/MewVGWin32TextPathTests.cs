extern alias MewVGWin32;

using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using MewUI.Test.Infrastructure;

using MewVGWin32GraphicsFactory = MewVGWin32::Aprillz.MewUI.Rendering.MewVG.MewVGWin32GraphicsFactory;

namespace MewUI.Test.Rendering;

[TestClass]
[DoNotParallelize]
public sealed class MewVGWin32TextPathTests
{
    [TestMethod]
    public void DrawTextLayout_RealizesGdiMeasuredTextIntoMewVGSurface()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("MewVG Win32 is Windows-only.");
            return;
        }

        const int width = 240;
        const int height = 64;
        const string text = "office 한글";

        using var factory = new MewVGWin32GraphicsFactory();
        using var backgroundScope = factory.AcquireBackgroundRenderScope();
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CachedImage(width, height, 1));
        using var context = factory.CreateContext(surface);
        using var font = factory.CreateFont("Segoe UI", 18, 96);

        context.BeginFrame(surface);
        context.Clear(Color.Transparent);
        var bounds = new Rect(4, 4, width - 8, height - 8);
        var layout = TextTestHarness.CreateLayout(factory, text.AsMemory(), font, bounds);
        Assert.IsGreaterThan(0, layout.MeasuredSize.Width);
        TextTestHarness.Draw(context, layout, bounds, Color.White);
        context.EndFrame();

        var pixels = ((ICpuPixelSurface)surface).GetReadOnlyPixelSpan();
        int coveredPixels = 0;
        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] > 16)
            {
                coveredPixels++;
            }
        }

        Assert.IsGreaterThanOrEqualTo(5, coveredPixels,
            "GDI measurement succeeded, but the independent MewVG raster/image-pattern path produced no text pixels.");
    }
}
