using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI.Windowless.Sample;

internal sealed class PaletteController(Action exit)
{
    private Window? _palette;
    private bool _paletteVisible;
    private bool _hideOnClose;
    private bool _exiting;

    public void Toggle()
    {
        if (_palette is { } openPalette && _paletteVisible)
        {
            if (_hideOnClose)
            {
                Hide(openPalette);
            }
            else
            {
                _palette = null;
                _paletteVisible = false;
                openPalette.Close();
                Console.Error.WriteLine($"[windowless] palette closed; user windows={Application.Current.AllWindows.Count}");
            }
            return;
        }

        if (_palette != null)
        {
            _palette.Show();
            _paletteVisible = true;
            Console.Error.WriteLine($"[windowless] palette shown again; user windows={Application.Current.AllWindows.Count}");
            return;
        }

        var palette = new Window()
            .Title("MewUI Windowless Palette")
            .Resizable(420, 220)
            .Content(
                new StackPanel()
                    .Padding(20)
                    .Spacing(12)
                    .Children(
                        new TextBlock().Text("The application started without a main window."),
                        new TextBlock().Text("Press Ctrl+Alt+Space again to close and reopen this palette."),
                        new CheckBox()
                            .Content("Hide instead of close")
                            .IsChecked(_hideOnClose)
                            .OnCheckedChanged(value => _hideOnClose = value == true),
                        new Button().Content("Exit application").OnClick(exit)));

        palette.Closing += args =>
        {
            if (_hideOnClose && !_exiting)
            {
                args.Cancel = true;
                Hide(palette);
            }
        };
        palette.Closed += () =>
        {
            if (ReferenceEquals(_palette, palette))
            {
                _palette = null;
                _paletteVisible = false;
            }
        };
        _palette = palette;
        _paletteVisible = true;
        palette.Show();
        Console.Error.WriteLine($"[windowless] palette shown; user windows={Application.Current.AllWindows.Count}");
    }

    public void PrepareToExit() => _exiting = true;

    private void Hide(Window palette)
    {
        // The global hotkey is raised while Space is still physically down. Drop the focused
        // checkbox before hiding so the trailing Space key-up cannot toggle it after a later Show.
        palette.FocusManager.ClearFocus();
        palette.Hide();
        _paletteVisible = false;
        Console.Error.WriteLine($"[windowless] palette hidden; user windows={Application.Current.AllWindows.Count}");
    }
}
