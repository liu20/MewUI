using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Adds hover support to an element: the pointer resting in one place raises
/// <see cref="MouseHover"/>, and leaving that place or the element raises
/// <see cref="MouseHoverStopped"/>.
/// </summary>
public class MouseHoverLogic : IDisposable
{
    // What Windows reports through SystemParameters, which MewUI does not surface.
    private static readonly TimeSpan _hoverTime = TimeSpan.FromMilliseconds(400);
    private const double HOVER_WIDTH = 4;
    private const double HOVER_HEIGHT = 4;

    private readonly UIElement _target;
    private DispatcherTimer? _hoverTimer;
    private Point _hoverStartPoint;
    private MouseEventArgs? _hoverLastEventArgs;
    private bool _hovering;
    private bool _disposed;

    /// <summary>Attaches hover tracking to <paramref name="target"/>.</summary>
    public MouseHoverLogic(UIElement target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _target.MouseLeave += OnMouseLeave;
        _target.MouseMove += OnMouseMove;
    }

    /// <summary>Occurs when the pointer starts hovering over a certain location.</summary>
    public event EventHandler<MouseEventArgs>? MouseHover;

    /// <summary>Occurs when the pointer stops hovering over a certain location.</summary>
    public event EventHandler<MouseEventArgs>? MouseHoverStopped;

    protected virtual void OnMouseHover(MouseEventArgs e) => MouseHover?.Invoke(this, e);

    protected virtual void OnMouseHoverStopped(MouseEventArgs e) => MouseHoverStopped?.Invoke(this, e);

    private void OnMouseMove(MouseEventArgs e)
    {
        var position = e.GetPosition(_target);
        // Small movement leaves the wait running, so a hand that is not quite still still hovers.
        if (Math.Abs(_hoverStartPoint.X - position.X) > HOVER_WIDTH ||
            Math.Abs(_hoverStartPoint.Y - position.Y) > HOVER_HEIGHT)
        {
            StartHovering(e);
        }
        // Handled is left alone so others still see the move.
    }

    private void OnMouseLeave() => StopHovering();

    private void StartHovering(MouseEventArgs e)
    {
        StopHovering();
        _hoverStartPoint = e.GetPosition(_target);
        _hoverLastEventArgs = e;
        _hoverTimer = new DispatcherTimer(_hoverTime);
        _hoverTimer.Tick += OnHoverTimerElapsed;
        _hoverTimer.Start();
    }

    private void StopHovering()
    {
        if (_hoverTimer is not null)
        {
            _hoverTimer.Stop();
            _hoverTimer.Tick -= OnHoverTimerElapsed;
            _hoverTimer = null;
        }
        if (_hovering)
        {
            _hovering = false;
            if (_hoverLastEventArgs is MouseEventArgs last)
            {
                OnMouseHoverStopped(last);
            }
        }
    }

    private void OnHoverTimerElapsed()
    {
        if (_hoverTimer is not null)
        {
            _hoverTimer.Stop();
            _hoverTimer.Tick -= OnHoverTimerElapsed;
            _hoverTimer = null;
        }

        _hovering = true;
        if (_hoverLastEventArgs is MouseEventArgs last)
        {
            OnMouseHover(last);
        }
    }

    /// <summary>Removes the hover tracking from the element.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (disposing)
        {
            StopHovering();
            _target.MouseLeave -= OnMouseLeave;
            _target.MouseMove -= OnMouseMove;
        }
    }
}
