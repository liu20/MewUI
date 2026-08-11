using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Third parties place their own gutters through TextArea.LeftMargins, so the collection has to
/// attach what it receives and the built-in line numbers have to be an ordinary member of it.
/// </summary>
[TestClass]
public sealed class MarginExtensibilityTests
{
    [TestMethod]
    public void LineNumbersAreAnOrdinaryLeftMargin()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        Assert.ContainsSingle(editor.TextArea.LeftMargins);
        Assert.IsInstanceOfType<LineNumberMargin>(editor.TextArea.LeftMargins[0]);
    }

    [TestMethod]
    public void AddingAMarginAttachesItToTheView()
    {
        var editor = new TextEditor { Text = "one\ntwo" };
        var margin = new RecordingMargin();

        editor.TextArea.LeftMargins.Add(margin);

        Assert.AreSame(editor.TextArea.TextView, margin.TextView);
        Assert.AreSame(editor.Document, margin.Document);
        Assert.AreEqual(1, margin.AttachCount);
        Assert.AreEqual(1, margin.TextViewChangedCount);
    }

    [TestMethod]
    public void ReassigningTheViewDetachesTheOldOne()
    {
        var editor = new TextEditor { Text = "one" };
        var margin = new RecordingMargin();
        editor.TextArea.LeftMargins.Add(margin);

        margin.TextView = null;

        Assert.AreEqual(1, margin.DetachCount);
        Assert.IsNull(margin.Document);
    }

    private sealed class RecordingMargin : AbstractMargin
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }
        public int TextViewChangedCount { get; private set; }

        protected override void AddToTextView(TextView textView) => AttachCount++;

        protected override void RemoveFromTextView(TextView textView) => DetachCount++;

        protected override void OnTextViewChanged(TextView? oldValue, TextView? newValue)
            => TextViewChangedCount++;

        protected override Size MeasureContent(Size availableSize) => new(10, availableSize.Height);

        protected override void OnRenderTextViewport(IGraphicsContext context, Rect textViewport)
        {
        }
    }
}
