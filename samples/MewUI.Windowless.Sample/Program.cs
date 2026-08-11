using Aprillz.MewUI;
using Aprillz.MewUI.Windowless.Sample;

RegisterPlatformAndBackend();

IHotkeyProvider? hotkey = null;
PaletteController? palette = null;
DispatcherTimer? smokeTimer = null;

Application.Create()
    .WithShutdownMode(ShutdownMode.OnExplicitShutdown)
    .OnStartup(args =>
    {
        bool runSmoke = args.Contains("--smoke", StringComparer.Ordinal);
        var dispatcher = Application.Current.Dispatcher
            ?? throw new InvalidOperationException("The UI dispatcher was not installed before startup.");

        void Exit()
        {
            smokeTimer?.Dispose();
            smokeTimer = null;
            hotkey?.Dispose();
            hotkey = null;
            palette?.PrepareToExit();
            Application.Shutdown();
        }

        palette = new PaletteController(Exit);
        try
        {
            hotkey = HotkeyProviderFactory.Create();
            hotkey.Start(() => dispatcher.BeginInvoke(palette.Toggle));
            Console.Error.WriteLine($"[windowless] {hotkey.Name} registered; user windows={Application.Current.AllWindows.Count}");
            Console.Error.WriteLine("[windowless] press Ctrl+Alt+Space to toggle the palette");
            if (runSmoke)
            {
                int phase = 0;
                smokeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400));
                smokeTimer.Tick += () =>
                {
                    phase++;
                    switch (phase)
                    {
                        case 1:
                        case 2:
                        case 3:
                            palette.Toggle();
                            break;
                        default:
                            palette.Toggle();
                            Console.Error.WriteLine("[windowless] smoke lifecycle completed");
                            Exit();
                            break;
                    }
                };
                smokeTimer.Start();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[windowless] hotkey registration failed: {ex}");
            Exit();
        }
    })
    .Run();

hotkey?.Dispose();

static void RegisterPlatformAndBackend()
{
    if (OperatingSystem.IsWindows())
    {
        Win32Platform.Register();
        Direct2DBackend.Register();
    }
    else if (OperatingSystem.IsMacOS())
    {
        MacOSPlatform.Register();
        MewVGMacOSBackend.Register();
    }
    else if (OperatingSystem.IsLinux())
    {
        X11Platform.Register();
        MewVGX11Backend.Register();
    }
    else
    {
        throw new PlatformNotSupportedException();
    }
}
