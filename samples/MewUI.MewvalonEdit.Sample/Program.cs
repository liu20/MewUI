using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit.Sample;

if (OperatingSystem.IsWindows())
{
    Win32Platform.Register();

    if (args.Any(a => a is "--gdi"))
    {
        GdiBackend.Register();
    }
    else if (args.Any(a => a is "--vg"))
    {
        MewVGWin32Backend.Register();
    }
    else
    {
        Direct2DBackend.Register();
    }
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

bool smoke = args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);

Application
    .Create()
    .UseAccent(Accent.Purple) 
    .BuildMainWindow(() =>
    {
        var window = new MainWindow();
        if (smoke) window.EnableSmokeTest();
        return window;
    })
    .Run();
