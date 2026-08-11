#if DEBUG
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Diagnostics;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI;

public partial class Window
{
    private Adorner? _debugInspectorAdorner;
    private DebugVisualTreeWindow? _debugVisualTreeWindow;
    private UIElement? _lastInspectorHover;
    private bool _lastInspectorInfoPanelAvoidsMouse;

    /// <summary>The adorner-hosted inspector overlay, or null when the inspector is off.</summary>
    internal DebugInspectorOverlay? DebugInspectorOverlay { get; private set; }

    // Relays for the DevTools windows, which live outside Window and cannot see these private
    // members. Kept as index accessors so PopupManager/AdornerEntry stay unexposed.
    internal int DebugPopupCount => _popupManager.Count;

    internal Element DebugPopupAt(int index) => _popupManager.ElementAt(index);

    internal int DebugAdornerCount => _adorners.Count;

    internal Element DebugAdornerElementAt(int index) => _adorners[index].Element;

    /// <summary>
    /// Called from <see cref="UpdateLastMousePosition"/>. Triggers an overlay redraw only
    /// when the element under the cursor actually changes, so cursor moves inside a single
    /// element no longer churn the inspector at every input tick.
    /// </summary>
    private void InvalidateInspectorOverlayIfHoverChanged()
    {
        if (DebugInspectorOverlay == null)
        {
            _lastInspectorHover = null;
            return;
        }

        var hovered = HitTest(_lastMousePositionDip);
        if (hovered is Adorner)
        {
            hovered = null;
        }

        bool infoPanelAvoidsMouse = DebugInspectorOverlay.ShouldAvoidMouse(_lastMousePositionDip);
        if (ReferenceEquals(hovered, _lastInspectorHover) &&
            infoPanelAvoidsMouse == _lastInspectorInfoPanelAvoidsMouse)
        {
            return;
        }

        _lastInspectorHover = hovered;
        _lastInspectorInfoPanelAvoidsMouse = infoPanelAvoidsMouse;
        DebugInspectorOverlay.InvalidateVisual();
    }

#if DEBUG
    public void DevToolsToggleInspector() => ToggleDebugInspector();

    public void DevToolsToggleVisualTree() => ToggleDebugVisualTree();

    public bool DevToolsInspectorIsOpen => _debugInspectorAdorner != null;

    public bool DevToolsVisualTreeIsOpen => _debugVisualTreeWindow != null;

    public event Action<bool>? DevToolsInspectorOpenChanged;

    public event Action<bool>? DevToolsVisualTreeOpenChanged;
#endif

    private void InitializeDebugDevTools()
    {
        InputMap.Map(new KeyGesture(Key.I, ModifierKeys.Primary | ModifierKeys.Shift), ToggleDebugInspector);
        InputMap.Map(new KeyGesture(Key.T, ModifierKeys.Primary | ModifierKeys.Shift), ToggleDebugVisualTree);
        InitializeDebugPerformanceProfiler();
    }

    private void ToggleDebugInspector()
    {
        if (_debugInspectorAdorner != null)
        {
            AdornerLayer.Remove(_debugInspectorAdorner);
            _debugInspectorAdorner = null;
            DebugInspectorOverlay = null;
            RequestUpdatePass();
            RequestRender();
#if DEBUG
            DevToolsInspectorOpenChanged?.Invoke(false);
#endif
            return;
        }

        DebugInspectorOverlay = new DebugInspectorOverlay(this)
        {
            IsHitTestVisible = false,
            IsVisible = true,
        };

        _debugInspectorAdorner = new Adorner(this, DebugInspectorOverlay)
        {
            IsHitTestVisible = false,
            IsVisible = true,
        };

        AdornerLayer.Add(_debugInspectorAdorner);
#if DEBUG
        DevToolsInspectorOpenChanged?.Invoke(true);
#endif
    }

    private void ToggleDebugVisualTree()
    {
        if (_debugVisualTreeWindow != null)
        {
            try
            {
                _debugVisualTreeWindow.Close();
            }
            catch { }
            _debugVisualTreeWindow = null;
#if DEBUG
            DevToolsVisualTreeOpenChanged?.Invoke(false);
#endif
            return;
        }

        // The tree window is much more useful with the overlay on (selection highlighting),
        // so ensure it's enabled.
        if (DebugInspectorOverlay == null)
        {
            ToggleDebugInspector();
        }

        var treeWindow = new DebugVisualTreeWindow(this);
        _debugVisualTreeWindow = treeWindow;

        treeWindow.Closed += () =>
        {
            if (ReferenceEquals(_debugVisualTreeWindow, treeWindow))
            {
                _debugVisualTreeWindow = null;
#if DEBUG
                DevToolsVisualTreeOpenChanged?.Invoke(false);
#endif
            }

            if (DebugInspectorOverlay != null)
            {
                DebugInspectorOverlay.HighlightedElement = null;
                RequestRender();
            }
        };

        Closed += CloseTreeOnOwnerClose;
        void CloseTreeOnOwnerClose()
        {
            Closed -= CloseTreeOnOwnerClose;
            try { _debugVisualTreeWindow?.Close(); } catch { }
            _debugVisualTreeWindow = null;
#if DEBUG
            DevToolsVisualTreeOpenChanged?.Invoke(false);
#endif
        }

        treeWindow.Show();
#if DEBUG
        DevToolsVisualTreeOpenChanged?.Invoke(true);
#endif
    }

    partial void DebugOnAfterMouseDownHitTest(Point positionInWindow, MouseButton button, UIElement? element)
    {
        _debugVisualTreeWindow?.OnTargetMouseDown(positionInWindow, button, element);
    }
}
#endif
