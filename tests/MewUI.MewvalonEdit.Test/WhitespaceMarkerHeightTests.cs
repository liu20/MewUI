using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A whitespace marker stands in for a character that is already on the line, so turning it on must
/// not change the line box. The width already follows that rule; these pin the height, which a line
/// holding nothing but whitespace has nothing else to take it from.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class WhitespaceMarkerHeightTests
{
    private const string TEXT = "int value = 1;\n\t\nint other = 2;";

    private static double LineHeight(double dpiScale, bool showTabs, bool showSpaces, int lineIndex)
    {
        var window = ScaledWindow.Create(dpiScale);
        var editor = new TextEditor
        {
            Text = TEXT,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13,
            ShowLineNumbers = false
        };
        editor.Options.ShowTabs = showTabs;
        editor.Options.ShowSpaces = showSpaces;
        window.Content = editor;
        window.PerformLayout();
        return editor.TextArea.TextView.Host.VisibleTextLines[lineIndex].Height;
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(1.5)]
    public void TheTabMarkerLeavesATabOnlyLineTheSameHeight(double dpiScale)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        double without = LineHeight(dpiScale, showTabs: false, showSpaces: false, lineIndex: 1);
        double with = LineHeight(dpiScale, showTabs: true, showSpaces: false, lineIndex: 1);

        Assert.AreEqual(without, with, 0.001,
            $"Showing tabs changed the height of a tab-only line at {dpiScale:P0}.");
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(1.5)]
    public void TheTabMarkerLeavesALineWithTextTheSameHeight(double dpiScale)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        double without = LineHeight(dpiScale, showTabs: false, showSpaces: false, lineIndex: 0);
        double with = LineHeight(dpiScale, showTabs: true, showSpaces: false, lineIndex: 0);

        Assert.AreEqual(without, with, 0.001,
            $"Showing tabs changed the height of a line carrying text at {dpiScale:P0}.");
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.25)]
    [DataRow(1.5)]
    public void TheSpaceMarkerLeavesLineHeightsAlone(double dpiScale)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        double without = LineHeight(dpiScale, showTabs: false, showSpaces: false, lineIndex: 0);
        double with = LineHeight(dpiScale, showTabs: false, showSpaces: true, lineIndex: 0);

        Assert.AreEqual(without, with, 0.001,
            $"Showing spaces changed a line's height at {dpiScale:P0}.");
    }
}
