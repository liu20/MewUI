using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A generated element that only decorates its range must leave the text alone. These check the
/// two things the old inline-run model took away: caret positions inside the range, and a width
/// measured at the density the view lays out at.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class LinkEditingTests
{
    private const int WIDTH = 400;
    private const int HEIGHT = 80;
    private const string TEXT = "// see https://example.com/docs now";

    [TestMethod]
    public void TheCaretHasAPositionInsideALink()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = TEXT, SkipViewportCull = true };
        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator());
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

        int start = TEXT.IndexOf("https", StringComparison.Ordinal);
        int end = start + "https://example.com/docs".Length;

        // One inline run is one cluster, so every offset inside it lands on the same x. Real text
        // advances at each character, which is what makes a link inside a comment editable.
        double previous = editor.TextArea.TextView.GetVisualPosition(start).X;
        for (int offset = start + 1; offset <= end; offset++)
        {
            double current = editor.TextArea.TextView.GetVisualPosition(offset).X;
            Assert.IsGreaterThan(previous, current,
                $"Offset {offset} shares its position with the one before it.");
            previous = current;
        }
    }

    [TestMethod]
    public void AReplacementElementMeasuresAtTheDensityItIsGiven()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var element = new TextReplacementElement("placeholder", 3, TextRunStyle.Default);

        double atDefault = element.Measure(96).Width;
        double atDouble = element.Measure(192).Width;

        // Measuring at 96 while the view lays out higher hands back an advance that is too short,
        // and the tail of the element is clipped.
        Assert.IsGreaterThan(atDefault, atDouble);
    }
}
