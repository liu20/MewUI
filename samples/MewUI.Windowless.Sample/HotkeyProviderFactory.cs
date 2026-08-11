namespace Aprillz.MewUI.Windowless.Sample;

internal static class HotkeyProviderFactory
{
    public static IHotkeyProvider Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Win32HotkeyProvider();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOSHotkeyProvider();
        }

        if (OperatingSystem.IsLinux())
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                return new PortalHotkeyProvider(static () => new X11HotkeyProvider());
            }

            return new X11HotkeyProvider();
        }

        throw new PlatformNotSupportedException("The windowless hotkey sample supports Windows, macOS, and Linux.");
    }
}
