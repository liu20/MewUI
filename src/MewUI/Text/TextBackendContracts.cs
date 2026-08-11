using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Text;

/// <summary>Backend-private entry point for text measurement sessions.</summary>
internal interface ITextBackendFactory
{
    ITextBackendMeasurementContext CreateTextMeasurementContext(uint dpi);
}

/// <summary>Backend-private measurement surface consumed by the managed text engine.</summary>
internal interface ITextBackendMeasurementContext : IDisposable
{
    bool SupportsUtf16PrefixAdvances { get; }

    Size Measure(ReadOnlySpan<char> text, IFont font);

    double[]? GetUtf16PrefixAdvances(ReadOnlySpan<char> text, IFont font);
}

/// <summary>Opaque backend realization of one positioned text run.</summary>
internal interface ITextBackendRun : IDisposable
{
    /// <summary>Native handle exposed only to lifetime diagnostics; zero when the backend has none.</summary>
    nint NativeHandle { get; }
}

/// <summary>Backend-private run realization and drawing surface.</summary>
internal interface ITextBackendRenderContext
{
    ITextBackendRun? CreateRun(ReadOnlySpan<char> text, IFont font, double width, double height);

    void DrawRun(ITextBackendRun run, Point origin, Color color, object? owner);
}
