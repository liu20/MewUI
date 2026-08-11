using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI;

/// <summary>
/// Invocation context passed to command handlers: the routing window, the invocation source
/// element (e.g. the toolbar button) and the invocation's cancellation token.
/// </summary>
public readonly struct CommandContext
{
    internal CommandContext(Window window, Element? source, CancellationToken cancellationToken)
    {
        Window = window;
        Source = source;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the window whose router dispatched the command.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    /// Gets the element that initiated the invocation, or null for programmatic execution.
    /// </summary>
    public Element? Source { get; }

    /// <summary>
    /// Gets the cancellation token for asynchronous handlers.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
