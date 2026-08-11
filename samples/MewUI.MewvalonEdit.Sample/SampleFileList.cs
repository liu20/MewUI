using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI.MewvalonEdit.Sample;

/// <summary>
/// The reference documents deployed beside the sample, listed for opening. They come from the
/// original's sample project and are read from disk rather than embedded, so the sample opens a
/// file the way a user does.
/// </summary>
public sealed class SampleFileList : UserControl
{
    private const string FOLDER = "samples";
    private const int ICON_SIZE = 16;

    private readonly ListBox _list = new();

    public SampleFileList()
    {
        Width = 190;

        var files = EnumerateFiles().Select(static path => new SampleFile(path)).ToList();
        _list.Items(files, static file => file.Name, keySelector: static file => file.Path);
        // The shell's own icon for the file type, so the list reads like the platform's file lists.
        // A platform without one answers null and the row is left with its name alone.
        _list.ItemTemplate(new DelegateTemplate<SampleFile>(
            build: context => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
                .Children(
                    new Image().Size(ICON_SIZE).CenterVertical().Register(context, "Icon"),
                    new TextBlock().CenterVertical().Register(context, "Name")),
            bind: (_, file, _, context) =>
            {
                context.Get<Image>("Icon").Source = IconFor(file);
                context.Get<TextBlock>("Name").Text = file?.Name ?? string.Empty;
            }));
        _list.SelectionChanged += item =>
        {
            if (item is SampleFile file)
            {
                Opened?.Invoke(file);
            }
        };

        Content = new DockPanel()
            .Spacing(6)
            .Children(
                new TextBlock { Text = "Files", FontWeight = FontWeight.Bold }.DockTop(),
                _list);
    }

    /// <summary>Raised when a file in the list is picked.</summary>
    public event Action<SampleFile>? Opened;

    /// <summary>
    /// Drops the highlighted row, for when the editor was loaded from somewhere other than this
    /// list and the row would otherwise claim to be what is open.
    /// </summary>
    public void ClearSelection() => _list.ClearSelection();

    private static ImageSource? IconFor(SampleFile file)
        => Application.IsRunning
            ? Application.Current.PlatformServices.ShellIconProvider.GetIcon(file.Path, isDirectory: false, ICON_SIZE)
            : null;

    /// <summary>Files deployed next to the executable, sorted by name.</summary>
    private static IEnumerable<string> EnumerateFiles()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, FOLDER);
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            : [];
    }
}

/// <summary>One deployed document. Reads its text on demand, since the list only needs the name.</summary>
public sealed class SampleFile(string path)
{
    public string Path { get; } = path;

    public string Name { get; } = System.IO.Path.GetFileName(path);

    /// <summary>
    /// Highlighting the extension asks for, or null when there is none for it. The manager owns the
    /// mapping, so .xaml and .xshd reach XML without this having to know they are XML dialects.
    /// </summary>
    public string? HighlightingName => Highlighting.HighlightingManager.Instance
        .GetDefinitionByExtension(System.IO.Path.GetExtension(Path))?.Name;

    public string ReadText() => File.ReadAllText(Path);

    public override string ToString() => Name;
}
