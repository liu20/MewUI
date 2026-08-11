using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// The stack is how a search panel or a completion list takes the keyboard without detaching what
/// is already there, so what these pin is the order keys are offered in and the attach lifetime.
/// </summary>
[TestClass]
public sealed class InputHandlerStackTests
{
    private sealed class RecordingStackedHandler(TextArea textArea, string name, List<string> log, bool claim = false)
        : TextAreaStackedInputHandler(textArea)
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        public override void Attach() => AttachCount++;
        public override void Detach() => DetachCount++;

        public override void OnPreviewKeyDown(KeyEventArgs e)
        {
            log.Add(name);
            if (claim)
            {
                e.Handled = true;
            }
        }

        public override void OnPreviewKeyUp(KeyEventArgs e) => log.Add(name + ":up");
    }

    private sealed class CountingHandler(TextArea textArea) : TextAreaInputHandler(textArea)
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        public override void Attach() { base.Attach(); AttachCount++; }
        public override void Detach() { base.Detach(); DetachCount++; }
    }

    private static KeyEventArgs Press(Key key) => new(key, 0);

    [TestMethod]
    public void TheDefaultHandlerIsActiveToBeginWith()
    {
        var editor = new TextEditor();
        Assert.AreSame(editor.TextArea.DefaultInputHandler, editor.TextArea.ActiveInputHandler);
        Assert.IsTrue(editor.TextArea.DefaultInputHandler.IsAttached);
    }

    [TestMethod]
    public void StackedHandlersSeeAKeyNewestFirst()
    {
        var editor = new TextEditor();
        var log = new List<string>();
        editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(editor.TextArea, "first", log));
        editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(editor.TextArea, "second", log));

        editor.TextArea.HandleKeyDown(Press(Key.A));

        CollectionAssert.AreEqual(new[] { "second", "first" }, log);
    }

    [TestMethod]
    public void AClaimedKeyStopsAtTheHandlerThatTookIt()
    {
        var editor = new TextEditor();
        var log = new List<string>();
        editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(editor.TextArea, "first", log));
        editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(editor.TextArea, "second", log, claim: true));

        var args = Press(Key.A);
        editor.TextArea.HandleKeyDown(args);

        Assert.IsTrue(args.Handled);
        CollectionAssert.AreEqual(new[] { "second" }, log, "The handler below saw a key that was already claimed.");
    }

    [TestMethod]
    public void PoppingAHandlerAlsoPopsWhatWasPushedOnTopOfIt()
    {
        var editor = new TextEditor();
        var log = new List<string>();
        var first = new RecordingStackedHandler(editor.TextArea, "first", log);
        var second = new RecordingStackedHandler(editor.TextArea, "second", log);
        editor.TextArea.PushStackedInputHandler(first);
        editor.TextArea.PushStackedInputHandler(second);

        editor.TextArea.PopStackedInputHandler(first);

        Assert.IsEmpty(editor.TextArea.StackedInputHandlers);
        Assert.AreEqual(1, first.DetachCount);
        Assert.AreEqual(1, second.DetachCount, "The handler above the popped one stayed attached.");
    }

    [TestMethod]
    public void PoppingAHandlerThatIsNotOnTheStackDoesNothing()
    {
        var editor = new TextEditor();
        var handler = new RecordingStackedHandler(editor.TextArea, "gone", []);

        editor.TextArea.PopStackedInputHandler(handler);

        Assert.AreEqual(0, handler.DetachCount);
    }

    [TestMethod]
    public void ReplacingTheActiveHandlerDetachesTheOldOne()
    {
        var editor = new TextEditor();
        var replacement = new CountingHandler(editor.TextArea);

        editor.TextArea.ActiveInputHandler = replacement;

        Assert.IsFalse(editor.TextArea.DefaultInputHandler.IsAttached);
        Assert.AreEqual(1, replacement.AttachCount);

        editor.TextArea.ActiveInputHandler = editor.TextArea.DefaultInputHandler;

        Assert.AreEqual(1, replacement.DetachCount);
        Assert.IsTrue(editor.TextArea.DefaultInputHandler.IsAttached);
    }

    [TestMethod]
    public void TheActiveHandlerAnswersItsBindings()
    {
        var editor = new TextEditor();
        int ran = 0;
        editor.TextArea.DefaultInputHandler.AddBinding(
            new KeyGesture(Key.K, ModifierKeys.Control), () => ran++);

        var args = new KeyEventArgs(Key.K, 0, ModifierKeys.Control);
        editor.TextArea.HandleKeyDown(args);

        Assert.AreEqual(1, ran);
        Assert.IsTrue(args.Handled);
    }

    [TestMethod]
    public void ADetachedHandlerAnswersNothing()
    {
        var editor = new TextEditor();
        int ran = 0;
        editor.TextArea.DefaultInputHandler.AddBinding(
            new KeyGesture(Key.K, ModifierKeys.Control), () => ran++);
        editor.TextArea.ActiveInputHandler = new CountingHandler(editor.TextArea);

        editor.TextArea.HandleKeyDown(new KeyEventArgs(Key.K, 0, ModifierKeys.Control));

        Assert.AreEqual(0, ran);
    }

    [TestMethod]
    public void NestedHandlersFollowTheHandlerThatHostsThem()
    {
        var editor = new TextEditor();
        var host = new CountingHandler(editor.TextArea);
        var nested = new CountingHandler(editor.TextArea);
        host.AddNestedInputHandler(nested);

        editor.TextArea.ActiveInputHandler = host;
        Assert.AreEqual(1, nested.AttachCount);

        editor.TextArea.ActiveInputHandler = editor.TextArea.DefaultInputHandler;
        Assert.AreEqual(1, nested.DetachCount);
    }

    [TestMethod]
    public void ANestedHandlerAddedWhileAttachedAttachesAtOnce()
    {
        var editor = new TextEditor();
        var nested = new CountingHandler(editor.TextArea);

        editor.TextArea.DefaultInputHandler.AddNestedInputHandler(nested);

        Assert.AreEqual(1, nested.AttachCount);
    }

    [TestMethod]
    public void ANestedBindingOverridesTheHandlerThatHostsIt()
    {
        var editor = new TextEditor();
        var order = new List<string>();
        var nested = new CountingHandler(editor.TextArea);
        nested.AddBinding(new KeyGesture(Key.K, ModifierKeys.Control), () => order.Add("nested"));
        editor.TextArea.DefaultInputHandler.AddBinding(
            new KeyGesture(Key.K, ModifierKeys.Control), () => order.Add("host"));
        editor.TextArea.DefaultInputHandler.AddNestedInputHandler(nested);

        editor.TextArea.HandleKeyDown(new KeyEventArgs(Key.K, 0, ModifierKeys.Control));

        CollectionAssert.AreEqual(new[] { "nested" }, order);
    }

    [TestMethod]
    public void AHandlerFromAnotherTextAreaIsRejected()
    {
        var editor = new TextEditor();
        var other = new TextEditor();

        Assert.ThrowsExactly<ArgumentException>(
            () => editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(other.TextArea, "x", [])));
        Assert.ThrowsExactly<ArgumentException>(
            () => editor.TextArea.ActiveInputHandler = new CountingHandler(other.TextArea));
        Assert.ThrowsExactly<ArgumentException>(
            () => editor.TextArea.DefaultInputHandler.AddNestedInputHandler(new CountingHandler(other.TextArea)));
    }

    [TestMethod]
    public void KeyUpReachesTheStackedHandlersOnly()
    {
        var editor = new TextEditor();
        var log = new List<string>();
        editor.TextArea.PushStackedInputHandler(new RecordingStackedHandler(editor.TextArea, "first", log));

        editor.TextArea.HandleKeyUp(Press(Key.A));

        CollectionAssert.AreEqual(new[] { "first:up" }, log);
    }

    /// <summary>
    /// The predefined bindings sit in the groups the original exposes, so a caller can take one
    /// group out or add to it without touching the others.
    /// </summary>
    [TestMethod]
    public void UndoLivesInTheEditingGroupAndLeavesWithIt()
    {
        var editor = new TextEditor { Text = "abc" };
        var handler = editor.TextArea.DefaultInputHandler;
        editor.Document.Insert(3, "d");

        // The gesture is declared with the Primary alias; the event carries what the platform sends.
        var undo = new KeyEventArgs(Key.Z, 0, ModifierKeys.Control);
        editor.TextArea.HandleKeyDown(undo);
        Assert.IsTrue(undo.Handled);
        Assert.AreEqual("abc", editor.Text);

        handler.RemoveNestedInputHandler(handler.Editing);
        editor.Document.Insert(3, "d");
        Assert.IsFalse(handler.TryHandleKey(new KeyEventArgs(Key.Z, 0, ModifierKeys.Control)),
            "The handler still answers undo after its editing group was taken out.");
    }
}
