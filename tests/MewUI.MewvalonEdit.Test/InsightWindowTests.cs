using System.ComponentModel;
using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.CodeCompletion;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The insight window hangs on a text segment and leaves the keyboard to the editor, except that
/// Up and Down walk the overloads while more than one is offered.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InsightWindowTests
{
    private sealed class FakeOverloadProvider(int count) : IOverloadProvider
    {
        private int _selectedIndex;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIndexText)));
            }
        }

        public int Count => count;
        public string CurrentIndexText => $"{SelectedIndex + 1} of {Count}";
        public object? CurrentHeader => $"overload {SelectedIndex}";
        public object? CurrentContent => "description";
    }

    private static TextEditor CreateEditor(string text)
    {
        var editor = new TextEditor
        {
            Text = text,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        return editor;
    }

    private static void Press(TextEditor editor, Key key)
        => editor.TextArea.HandleKeyDown(new KeyEventArgs(key, platformKey: 0, ModifierKeys.None));

    [TestMethod]
    public void UpAndDownWalkTheOverloadsAndWrap()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("Method(");
        editor.CaretOffset = 7;
        var window = new OverloadInsightWindow(editor.TextArea) { Provider = new FakeOverloadProvider(3) };
        window.Show();

        Press(editor, Key.Down);
        Assert.AreEqual(1, window.Provider!.SelectedIndex);

        Press(editor, Key.Up);
        Press(editor, Key.Up);
        Assert.AreEqual(2, window.Provider.SelectedIndex, "Walking above the first overload must wrap to the last.");
    }

    [TestMethod]
    public void ASingleOverloadLeavesTheKeysToTheEditor()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("Method(");
        editor.CaretOffset = 7;
        var window = new OverloadInsightWindow(editor.TextArea) { Provider = new FakeOverloadProvider(1) };
        window.Show();

        var args = new KeyEventArgs(Key.Down, platformKey: 0, ModifierKeys.None);
        editor.TextArea.HandleKeyDown(args);

        Assert.IsFalse(args.Handled, "With one overload the movement keys stay the editor's.");
        Assert.AreEqual(0, window.Provider!.SelectedIndex);
    }

    [TestMethod]
    public void TheCaretLeavingTheSegmentCloses()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("Method(arg)");
        editor.CaretOffset = 7;
        var window = new OverloadInsightWindow(editor.TextArea) { Provider = new FakeOverloadProvider(2) };
        window.EndOffset = 10;
        window.Show();

        editor.CaretOffset = 9;
        Assert.IsTrue(window.IsOpen, "Inside the segment the window stays.");

        editor.CaretOffset = 11;
        Assert.IsFalse(window.IsOpen);
    }

    [TestMethod]
    public void AnInsightWindowAndACompletionWindowCoexist()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var editor = CreateEditor("Method(");
        editor.CaretOffset = 7;
        var insight = new OverloadInsightWindow(editor.TextArea) { Provider = new FakeOverloadProvider(2) };
        insight.Show();
        var completion = new CompletionWindow(editor.TextArea);
        completion.CompletionList.CompletionData.Add(new CompletionData("argument"));
        completion.Show();

        Assert.IsTrue(insight.IsOpen, "Windows of different types coexist, as the original allows.");
        Assert.IsTrue(completion.IsOpen);

        Press(editor, Key.Escape);
        Assert.IsFalse(completion.IsOpen, "Escape closes the newest window first.");
        Assert.IsTrue(insight.IsOpen);
    }
}
