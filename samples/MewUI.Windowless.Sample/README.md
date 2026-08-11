# MewUI windowless lifecycle sample

This sample starts MewUI without a main window, registers a global `Ctrl+Alt+Space` hotkey, and creates the palette only after the hotkey is pressed. Closing the palette returns to zero user windows while the process and hotkey remain active. Use **Exit application** to release the provider and call `Application.Shutdown()`.

Enable **Hide instead of close** to retain and reuse the palette's native window. The hotkey and the title-bar close button then hide it, and the next hotkey shows the same instance again. A hidden window remains in `Application.Current.AllWindows`, so leave the option disabled when validating the zero-window lifecycle.

The sample selects `ShutdownMode.OnExplicitShutdown` through `WithShutdownMode`; windowless `Run` does not change the application's shutdown policy automatically.

## Providers and validation

| Environment | Provider | Validation |
|---|---|---|
| Windows | `RegisterHotKey` on a dedicated message-queue thread | Registration and automated window lifecycle verified on 2026-08-10; physical hotkey activation remains manual |
| macOS | Carbon `RegisterEventHotKey` | Build verified; runtime verification required on macOS |
| Linux/X11 | `XGrabKey` on a separate display connection | Build verified; runtime verification required on X11 |
| Linux/Wayland | GlobalShortcuts portal, with X11/XWayland fallback | Build verified; runtime verification required on a portal-enabled desktop |

Run the sample, confirm the startup log reports `user windows=0`, toggle and close the palette twice, and finally exit through the button. A platform is only considered runtime-verified after that complete sequence succeeds.

Use `--smoke` to automate the window lifecycle after native provider registration: show, close to zero windows, show again, close, dispose the provider, and quit.
