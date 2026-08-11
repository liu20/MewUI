using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A selection running over several lines is one shape, not a stack of boxes. The builder joins
/// rectangles whose edges meet into a single outline, which is what makes the corners round only on
/// the outside of the run.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BackgroundGeometryBuilderTests
{
    [TestMethod]
    public void TouchingRectanglesBecomeOneFigure()
    {
        var builder = new BackgroundGeometryBuilder { CornerRadius = 3 };

        builder.AddRectangle(10, 0, 100, 20);
        builder.AddRectangle(10, 20, 60, 40);

        Assert.AreEqual(1, CountFigures(builder.CreateGeometry()),
            "Two rectangles that meet were drawn as separate outlines.");
    }

    /// <summary>
    /// The values two stacked rows produce at 125%, where snapping each edge after pushing it out
    /// by half a border leaves the shared boundary landing on two different numbers.
    /// </summary>
    [TestMethod]
    [DataRow(1.25, 17.9, 18.1)]
    [DataRow(1.5, 18.1666, 17.8333)]
    public void RowsStillJoinWhereSnappingSplitTheirSharedBoundary(double scale, double firstBottom, double secondTop)
    {
        var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, BorderThickness = 1 };

        builder.AddRectangle(5, 3.5, 100, firstBottom);
        builder.AddRectangle(5, secondTop, 60, secondTop + 15);

        Assert.AreEqual(1, CountFigures(builder.CreateGeometry()),
            $"At {scale:P0} the rows were drawn as separate outlines.");
    }

    /// <summary>
    /// A selection starting far along a line and continuing onto a short one leaves two rows that
    /// meet vertically but share no column. Joining them would fold the outline over itself and
    /// draw a line across the gap between them.
    /// </summary>
    [TestMethod]
    public void RowsThatShareNoColumnStayApart()
    {
        var builder = new BackgroundGeometryBuilder();

        builder.AddRectangle(200, 0, 300, 20);
        builder.AddRectangle(5, 20, 100, 40);

        Assert.AreEqual(2, CountFigures(builder.CreateGeometry()),
            "Rows that do not overlap in x were joined into one outline.");
    }

    /// <summary>
    /// With the border snapped to whole device pixels the builder insets by half of that same
    /// value, which leaves the stroke covering whole pixels: an odd stroke centred on a pixel
    /// middle, an even one on a pixel boundary. A one-DIP border can do neither off 100%.
    /// </summary>
    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(1.5)]
    [DataRow(2.0)]
    public void ASnappedBorderCoversWholePixels(double dpiScale)
    {
        double thickness = LayoutRounding.SnapThicknessToPixels(1, dpiScale, 1);
        int pixels = (int)Math.Round(thickness * dpiScale);
        Assert.AreEqual(pixels, thickness * dpiScale, 1e-9, "The border is not a whole number of pixels.");

        // What AddRectangle(TextView, Rect) computes for an edge.
        double edge = LayoutRounding.RoundToPixel(37.3 - (thickness / 2), dpiScale) + (thickness / 2);

        // The stroke reaches half its width either side of the centre, so its outer edge is what
        // has to land on a pixel boundary.
        double outer = (edge - (thickness / 2)) * dpiScale;
        Assert.AreEqual(0, Math.Abs(outer - Math.Round(outer)), 1e-9,
            $"At {dpiScale:P0} a {pixels}px stroke centred at {edge} straddles two pixels.");
    }

    [TestMethod]
    public void SeparatedRectanglesStayApart()
    {
        var builder = new BackgroundGeometryBuilder { CornerRadius = 3 };

        builder.AddRectangle(10, 0, 100, 20);
        builder.AddRectangle(10, 50, 60, 70);

        Assert.AreEqual(2, CountFigures(builder.CreateGeometry()),
            "Rectangles with a gap between them were joined.");
    }

    [TestMethod]
    public void AnEmptyBuilderHasNoGeometry()
        => Assert.IsNull(new BackgroundGeometryBuilder().CreateGeometry());

    /// <summary>
    /// The visual-column entry point, which is what a renderer holding a line rather than a
    /// document segment uses.
    /// </summary>
    [TestMethod]
    public void AVisualSegmentProducesOneRectanglePerRow()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "class A\nint x;\n", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));
        var line = editor.TextArea.TextView.VisualLines[0];

        var rects = BackgroundGeometryBuilder
            .GetRectsFromVisualSegment(editor.TextArea.TextView, line, 0, 5)
            .ToArray();

        Assert.HasCount(1, rects);
        Assert.IsGreaterThan(0.0, rects[0].Width, "The range covered no width.");
        Assert.AreEqual(line.TextLines[0].Bounds.Height, rects[0].Height, 0.01);
    }

    /// <summary>
    /// The whole of a short line is covered. AvalonEdit's rows carry a column for the end of the
    /// paragraph and ours do not, so taking one off the row end would drop a real character and
    /// leave a one-character line with nothing but the empty-line sliver.
    /// </summary>
    [TestMethod]
    public void AOneCharacterLineIsCoveredForItsFullWidth()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "class A\n{\n}\n", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));
        var view = editor.TextArea.TextView;
        var brace = view.VisualLines[1];
        Assert.AreEqual(1, brace.DocumentLength, "The sample line is not one character long.");

        var rects = BackgroundGeometryBuilder
            .GetRectsFromVisualSegment(view, brace, 0, brace.VisualLength)
            .ToArray();

        Assert.IsNotEmpty(rects);
        Assert.IsGreaterThan(view.EmptyLineSelectionWidth, rects[0].Width,
            "The line's only character was left out of the selection.");
    }

    /// <summary>
    /// The path the selection layer takes: whole-pixel alignment on, one segment spanning several
    /// lines. The rows have to snap and to come out as one outline.
    /// </summary>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(1.0)]
    public void ASelectionOverSeveralLinesSnapsAndFormsOneOutline(double borderThickness)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor
        {
            Text = "class A\n{\n    int x;\n}\n",
            ShowLineNumbers = false,
            SkipViewportCull = true
        };
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));
        editor.Select(0, editor.Text.Length);

        var view = editor.TextArea.TextView;
        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            BorderThickness = borderThickness
        };
        foreach (var segment in editor.TextArea.Selection.Segments)
        {
            builder.AddSegment(view, segment);
        }
        var geometry = builder.CreateGeometry();

        Assert.IsNotNull(geometry);
        double dpiScale = view.DpiScale;
        double expectedFraction = borderThickness / 2 % 1;
        foreach (var command in geometry.Commands)
        {
            if (command.Type == PathCommandType.Close)
            {
                continue;
            }
            Assert.AreEqual(expectedFraction, Math.Abs(command.Y0 * dpiScale % 1), 0.01,
                $"A y of {command.Y0} is off its pixel boundary, so the border straddles two rows of pixels.");
        }
        Assert.AreEqual(1, CountFigures(geometry),
            "The lines of one selection were drawn as separate outlines.");
    }

    private static int CountFigures(PathGeometry? geometry)
    {
        if (geometry is null)
        {
            return 0;
        }
        int figures = 0;
        foreach (var command in geometry.Commands)
        {
            if (command.Type == PathCommandType.MoveTo)
            {
                figures++;
            }
        }
        return figures;
    }
}
