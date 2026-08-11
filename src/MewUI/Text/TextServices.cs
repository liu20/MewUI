using System.Runtime.CompilerServices;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Text;

internal static class TextServices
{
    private static readonly ConditionalWeakTable<IGraphicsFactory, ManagedTextEngine> Engines = new();
    private static readonly ConditionalWeakTable<IGraphicsContext, ITextRenderContext> RenderContexts = new();

    public static ITextEngine GetEngine(IGraphicsFactory factory)
        => Engines.GetValue(factory, static value => new ManagedTextEngine(value));

    public static ITextRenderContext GetRenderContext(IGraphicsContext context)
        => RenderContexts.GetValue(context, static value => new ManagedTextRenderContext(value));

    public static void ReleaseRenderContext(IGraphicsContext context)
    {
        if (RenderContexts.TryGetValue(context, out var renderContext))
        {
            RenderContexts.Remove(context);
            (renderContext as IDisposable)?.Dispose();
        }
    }

    public static void ReleaseEngine(IGraphicsFactory factory)
    {
        if (Engines.TryGetValue(factory, out var engine))
        {
            Engines.Remove(factory);
            engine.Dispose();
        }
    }
}
