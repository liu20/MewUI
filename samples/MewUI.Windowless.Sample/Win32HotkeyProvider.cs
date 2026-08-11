using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aprillz.MewUI.Windowless.Sample;

internal sealed class Win32HotkeyProvider : IHotkeyProvider
{
    // Application-owned RegisterHotKey identifiers must be in 0x0000..0xBFFF.
    private const int HOTKEY_ID = 0x4D45;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_SPACE = 0x20;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_APP_STOP = 0x8001;

    private readonly ManualResetEventSlim _ready = new();
    private Thread? _thread;
    private Action? _activated;
    private uint _threadId;
    private Exception? _startupError;

    public string Name => "Win32 RegisterHotKey";

    public void Start(Action activated)
    {
        ArgumentNullException.ThrowIfNull(activated);
        if (_thread != null)
        {
            throw new InvalidOperationException("The hotkey provider is already running.");
        }

        _activated = activated;
        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "MewUI windowless hotkey",
        };
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("The Win32 hotkey thread did not initialize.");
        }
        if (_startupError != null)
        {
            throw new InvalidOperationException("RegisterHotKey failed.", _startupError);
        }
    }

    private void MessageLoop()
    {
        _threadId = GetCurrentThreadId();
        _ = PeekMessage(out _, 0, 0, 0, 0); // Create this thread's message queue before publishing readiness.
        if (!RegisterHotKey(0, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_SPACE))
        {
            _startupError = new Win32Exception(Marshal.GetLastWin32Error());
            _ready.Set();
            return;
        }

        _ready.Set();
        try
        {
            while (GetMessage(out var message, 0, 0, 0) > 0)
            {
                if (message.Message == WM_HOTKEY && message.WParam == HOTKEY_ID)
                {
                    _activated?.Invoke();
                }
                else if (message.Message == WM_APP_STOP)
                {
                    break;
                }
            }
        }
        finally
        {
            UnregisterHotKey(0, HOTKEY_ID);
        }
    }

    public void Dispose()
    {
        var thread = Interlocked.Exchange(ref _thread, null);
        if (thread == null)
        {
            return;
        }

        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, WM_APP_STOP, 0, 0);
        }
        thread.Join(TimeSpan.FromSeconds(2));
        _activated = null;
        _ready.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint HWnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32")]
    private static extern int GetMessage(out MSG message, nint hWnd, uint min, uint max);

    [DllImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG message, nint hWnd, uint min, uint max, uint remove);

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32")]
    private static extern uint GetCurrentThreadId();
}
