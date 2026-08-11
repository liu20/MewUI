using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aprillz.MewUI.Windowless.Sample;

internal sealed class X11HotkeyProvider : IHotkeyProvider
{
    private const int KEY_PRESS = 2;
    private const long KEY_PRESS_MASK = 1L << 0;
    private const uint CONTROL_MASK = 1u << 2;
    private const uint MOD1_MASK = 1u << 3;
    private const uint LOCK_MASK = 1u << 1;
    private const uint MOD2_MASK = 1u << 4;
    private const ulong XK_SPACE = 0x20;

    private readonly ManualResetEventSlim _ready = new();
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private Action? _activated;
    private Exception? _startupError;

    public string Name => "X11 XGrabKey";

    public void Start(Action activated)
    {
        ArgumentNullException.ThrowIfNull(activated);
        _activated = activated;
        _thread = new Thread(EventLoop)
        {
            IsBackground = true,
            Name = "MewUI X11 global hotkey",
        };
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The X11 hotkey thread did not initialize.");
        }
        if (_startupError != null)
        {
            throw new InvalidOperationException("XGrabKey initialization failed.", _startupError);
        }
    }

    private void EventLoop()
    {
        XInitThreads();
        nint display = XOpenDisplay(0);
        if (display == 0)
        {
            _startupError = new InvalidOperationException("XOpenDisplay failed.");
            _ready.Set();
            return;
        }

        nint root = XDefaultRootWindow(display);
        int keycode = XKeysymToKeycode(display, XK_SPACE);
        uint modifiers = CONTROL_MASK | MOD1_MASK;
        uint[] lockVariants = [0, LOCK_MASK, MOD2_MASK, LOCK_MASK | MOD2_MASK];
        try
        {
            foreach (uint locks in lockVariants)
            {
                XGrabKey(display, keycode, modifiers | locks, root, false, 1, 1);
            }
            XSelectInput(display, root, KEY_PRESS_MASK);
            XSync(display, false);
            _ready.Set();

            while (!_stop.IsCancellationRequested)
            {
                while (XPending(display) > 0)
                {
                    XNextEvent(display, out var @event);
                    if (@event.Type == KEY_PRESS)
                    {
                        _activated?.Invoke();
                    }
                }
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            _startupError ??= ex;
            _ready.Set();
        }
        finally
        {
            foreach (uint locks in lockVariants)
            {
                XUngrabKey(display, keycode, modifiers | locks, root);
            }
            XSync(display, false);
            XCloseDisplay(display);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        var thread = Interlocked.Exchange(ref _thread, null);
        thread?.Join(TimeSpan.FromSeconds(2));
        _activated = null;
        _stop.Dispose();
        _ready.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public int Type;
    }

    [DllImport("libX11.so.6")]
    private static extern int XInitThreads();

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XDefaultRootWindow(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XKeysymToKeycode(nint display, ulong keysym);

    [DllImport("libX11.so.6")]
    private static extern int XGrabKey(nint display, int keycode, uint modifiers, nint window,
        [MarshalAs(UnmanagedType.Bool)] bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport("libX11.so.6")]
    private static extern int XUngrabKey(nint display, int keycode, uint modifiers, nint window);

    [DllImport("libX11.so.6")]
    private static extern int XSelectInput(nint display, nint window, long eventMask);

    [DllImport("libX11.so.6")]
    private static extern int XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    [DllImport("libX11.so.6")]
    private static extern int XPending(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(nint display, out XEvent @event);
}
