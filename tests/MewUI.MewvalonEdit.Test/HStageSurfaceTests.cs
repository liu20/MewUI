using System.Text;
using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Folding;
using Aprillz.MewUI.MewvalonEdit.Rendering;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class HStageSurfaceTests
{
    [TestMethod]
    public void GeometryBuilderRoundsAlignedRectanglesAndBuildsGeometry()
    {
        var editor = new TextEditor { Text = "x" };
        var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 3 };
        builder.AddRectangle(editor.TextArea.TextView, new Rect(1.4, 2.6, 10.2, 5.9));

        var geometry = builder.CreateGeometry();

        Assert.IsNotNull(geometry);
        Assert.IsFalse(geometry.IsEmpty);
    }

    [TestMethod]
    public void GeometryBuilderWithNothingAddedReturnsNull()
    {
        Assert.IsNull(new BackgroundGeometryBuilder().CreateGeometry());
    }

    [TestMethod]
    public void InstallingFoldingAddsTheMarginAndUninstallRemovesIt()
    {
        var editor = new TextEditor { Text = "{\n}\n" };

        var manager = FoldingManager.Install(editor);
        Assert.IsTrue(editor.TextArea.LeftMargins.Any(static margin => margin is FoldingMargin));

        FoldingManager.Uninstall(manager);
        Assert.IsFalse(editor.TextArea.LeftMargins.Any(static margin => margin is FoldingMargin));
    }

    [TestMethod]
    public void XmlFoldingStrategyFoldsMultiLineElementsAndComments()
    {
        var editor = new TextEditor
        {
            Text = "<root>\n  <child attr=\"1\">\n    text\n  </child>\n  <single/>\n</root>\n<!-- first\n second -->"
        };
        var manager = FoldingManager.Install(editor);
        var strategy = new XmlFoldingStrategy { ShowAttributesWhenFolded = true };

        strategy.UpdateFoldings(manager, editor.Document);

        var foldings = manager.AllFoldings.ToArray();
        Assert.HasCount(3, foldings);
        Assert.AreEqual("<root>", foldings[0].Title);
        Assert.AreEqual("<child attr=\"1\">", foldings[1].Title);
        Assert.StartsWith("<!--", foldings[2].Title);
        Assert.AreEqual(editor.Document.Text.IndexOf("<child", StringComparison.Ordinal), foldings[1].StartOffset);
    }

    [TestMethod]
    public void XmlFoldingStrategyReportsTheFirstErrorOffset()
    {
        var editor = new TextEditor { Text = "<root>\n  <broken\n</root>" };
        var strategy = new XmlFoldingStrategy();

        strategy.CreateNewFoldings(editor.Document, out int firstErrorOffset);

        Assert.IsGreaterThanOrEqualTo(0, firstErrorOffset, "Malformed XML must report where parsing stopped.");
    }

    [TestMethod]
    public void SaveAndLoadRoundTripTheTextAndEncoding()
    {
        var editor = new TextEditor { Text = "first\nsecond äöü" };
        editor.Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        using var stream = new MemoryStream();

        editor.Save(stream);
        stream.Position = 0;

        var loaded = new TextEditor();
        loaded.Load(stream);

        Assert.AreEqual(editor.Text, loaded.Text);
        Assert.IsInstanceOfType<UTF8Encoding>(loaded.Encoding);
    }

    [TestMethod]
    public void InlineObjectElementMeasuresItsHostedElement()
    {
        var element = new InlineObjectElement(1, new Aprillz.MewUI.Controls.Border { Width = 24, Height = 10 });

        var metrics = element.Measure(96);

        Assert.AreEqual(24, metrics.Width, 0.01);
        Assert.AreEqual(10, metrics.Height, 0.01);
    }
}
