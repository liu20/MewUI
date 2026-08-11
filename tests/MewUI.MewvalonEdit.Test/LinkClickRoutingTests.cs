using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Coverage of link clicking, from the element's own handlers up to a press arriving at the window:
/// the generator builds the element through the factory seam, the editor finds it under the pointer,
/// and the element decides navigation.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class LinkClickRoutingTests
{
    private const string TEXT = "see https://example.com/docs now";

    private sealed class RecordingLinkText(int documentLength) : VisualLineLinkText(documentLength)
    {
        public List<string> Navigated { get; } = [];

        protected override void NavigateTo(string uri) => Navigated.Add(uri);
    }

    private sealed class RecordingLinkGenerator : LinkElementGenerator
    {
        public RecordingLinkText? LastElement { get; private set; }

        protected override VisualLineLinkText CreateLinkElement(string text, int documentLength)
            => LastElement = new RecordingLinkText(documentLength);
    }

    private static RecordingLinkText ConstructLink()
    {
        var editor = new TextEditor { Text = TEXT };
        var generator = new RecordingLinkGenerator();
        editor.TextArea.TextView.ElementGenerators.Add(generator);

        generator.StartGeneration(new ConstructionContext(editor));
        var element = generator.ConstructElement(TEXT.IndexOf("https", StringComparison.Ordinal));
        generator.FinishGeneration();
        return (RecordingLinkText)element!;
    }

    private static MouseEventArgs LeftClick(ModifierKeys modifiers)
        => new(default, default, MouseButton.Left, leftButton: true, modifiers: modifiers);

    [TestMethod]
    public void ControlClickNavigatesAndClaimsTheEvent()
    {
        var link = ConstructLink();

        var click = LeftClick(ModifierKeys.Control);
        link.OnMouseDown(click);

        Assert.ContainsSingle(link.Navigated);
        Assert.AreEqual("https://example.com/docs", link.Navigated[0]);
        Assert.IsTrue(click.Handled, "A followed link must claim the press so the caret stays put.");
    }

    [TestMethod]
    public void PlainClickLeavesTheEventForCaretPlacement()
    {
        var link = ConstructLink();

        var click = LeftClick(ModifierKeys.None);
        link.OnMouseDown(click);

        Assert.IsEmpty(link.Navigated);
        Assert.IsFalse(click.Handled, "An unclaimed press falls through to the editor's caret.");
    }

    [TestMethod]
    public void WithoutTheControlRequirementAPlainClickNavigates()
    {
        var link = ConstructLink();
        link.RequireControlModifierForClick = false;

        var click = LeftClick(ModifierKeys.None);
        link.OnMouseDown(click);

        Assert.ContainsSingle(link.Navigated);
        Assert.IsTrue(click.Handled);
    }

    /// <summary>An editor whose only link generator records instead of launching the target.</summary>
    private static (Window Window, TextEditor Editor, Point LinkPoint) RecordingEditor()
    {
        var window = ScaledWindow.Create(1.0, 800, 300);
        var editor = new TextEditor
        {
            Text = TEXT,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        editor.Options.EnableHyperlinks = false;
        editor.TextArea.TextView.ElementGenerators.Add(new RecordingLinkGenerator());
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();

        var rect = editor.Surface.GetCharRectInWindow(TEXT.IndexOf("example", StringComparison.Ordinal));
        return (window, editor, new Point(rect.X + 1, rect.Y + rect.Height / 2));
    }

    private static RecordingLinkText NavigatedLink(TextEditor editor)
        => editor.TextArea.TextView.ElementGenerators
            .OfType<RecordingLinkGenerator>().Single().LastElement!;

    private static void ClickAt(Window window, Point point, ModifierKeys modifiers)
    {
        WindowInputRouter.MouseButton(window, point, point, MouseButton.Left, true, true, false, false, 1, modifiers);
        WindowInputRouter.MouseButton(window, point, point, MouseButton.Left, false, false, false, false, 1, modifiers);
    }

    [TestMethod]
    public void AControlPressOnTheWindowReachesTheLinkAndLeavesTheCaret()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The input router drives the Windows message path.");
            return;
        }
        var (window, editor, point) = RecordingEditor();

        ClickAt(window, point, ModifierKeys.Control);

        Assert.ContainsSingle(NavigatedLink(editor).Navigated);
        Assert.AreEqual(0, editor.CaretOffset, "A followed link must claim the press so the caret stays put.");
    }

    [TestMethod]
    public void APlainPressOnTheWindowPlacesTheCaretInsteadOfNavigating()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The input router drives the Windows message path.");
            return;
        }
        var (window, editor, point) = RecordingEditor();

        ClickAt(window, point, ModifierKeys.None);

        Assert.IsEmpty(NavigatedLink(editor).Navigated);
        Assert.AreNotEqual(0, editor.CaretOffset);
    }

    [TestMethod]
    public void HoldingControlOverALinkTurnsTheCursorIntoAHandAndReleasingItTurnsItBack()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The input router drives the Windows message path.");
            return;
        }
        var (window, editor, point) = RecordingEditor();

        WindowInputRouter.MouseMove(window, point, point, false, false, false);
        Assert.AreEqual(CursorType.IBeam, editor.Surface.Cursor);

        // A modifier press carries no key of its own, only the modifiers it just changed.
        WindowInputRouter.KeyDown(window, new KeyEventArgs(Key.None, 0, ModifierKeys.Control));
        Assert.AreEqual(CursorType.Hand, editor.Surface.Cursor, "Control over a link makes it clickable.");

        WindowInputRouter.KeyUp(window, new KeyEventArgs(Key.None, 0));
        Assert.AreEqual(CursorType.IBeam, editor.Surface.Cursor, "Letting go of Control makes it text again.");
    }

    private sealed class ConstructionContext(TextEditor editor) : ITextRunConstructionContext
    {
        public Aprillz.MewUI.MewvalonEdit.Document.TextDocument Document => editor.Document;
        public Aprillz.MewUI.MewvalonEdit.Rendering.TextView TextView => editor.TextArea.TextView;
        public Aprillz.MewUI.MewvalonEdit.Document.DocumentLine CurrentDocumentLine
            => editor.Document.GetLineByNumber(1);
        public TextRunStyle DefaultStyle => new(editor.FontFamily, editor.FontSize, editor.FontWeight);
    }
}
