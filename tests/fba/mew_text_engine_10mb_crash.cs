#:sdk Microsoft.NET.Sdk

#:property OutputType=Exe
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property AllowUnsafeBlocks=true

#:project ../../src/MewUI/MewUI.csproj
#:project ../../src/MewUI.Platform.Win32/MewUI.Platform.Win32.csproj
#:project ../../src/MewUI.Backend.Direct2D/MewUI.Backend.Direct2D.csproj

using System.Text;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

Win32Platform.Register();
Direct2DBackend.Register();

var editor = new MultiLineTextBox { Wrap = true };
var statusText = new TextBlock { Text = "10MB render sentinel" };
var status = new Button { Content = statusText, Height = 36 };
var window = new Window()
    .Title("10MB text engine crash repro")
    .Resizable(800, 500)
    .Build(w => w.Content(
        new DockPanel().Children(
            status.DockTop(),
            editor)));

string? multiline = null;
string? singleLine = null;
int phase = 0;
var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(750));
timer.Tick += () =>
{
    switch (phase++)
    {
        case 0:
            Console.Error.WriteLine("[repro] generate multiline 10MB");
            const string line = "The quick brown fox jumps over the lazy dog. 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ\n";
            var builder = new StringBuilder(10_000_000 + line.Length);
            while (builder.Length < 10_000_000) builder.Append(line);
            multiline = builder.ToString(0, 10_000_000);
            editor.Wrap = true;
            editor.Text = multiline;
            statusText.Text = "multiline rendered";
            Collect("multiline");
            break;
        case 1:
            Console.Error.WriteLine("[repro] generate single line 10MB, no wrap");
            singleLine = new string('x', 10_000_000);
            editor.Wrap = false;
            editor.Text = singleLine;
            statusText.Text = "no-wrap rendered";
            Collect("no-wrap");
            break;
        case 2:
            Console.Error.WriteLine("[repro] switch single line to wrap");
            editor.Wrap = true;
            statusText.Text = "wrap rendered";
            Collect("wrap");
            break;
        case 3:
            Console.Error.WriteLine("[repro] collect and render again");
            multiline = null;
            singleLine = null;
            Collect("released source strings");
            statusText.Text = "post-GC rendered";
            break;
        default:
            Console.Error.WriteLine("[repro] PASS");
            timer.Stop();
            window.Close();
            break;
    }
};

window.Loaded += timer.Start;
Application.Run(window);

static void Collect(string phase)
{
    Console.Error.WriteLine($"[repro] GC start: {phase}");
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Console.Error.WriteLine($"[repro] GC end: {phase}");
}
