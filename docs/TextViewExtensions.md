# Text View Extensions

The MewUI [text engine](TextEngine.md) separates document layout from optional view behavior. `MultiLineTextBox`, `SyntaxViewer`, and MewvalonEdit expose the same `TextViewExtensionPipeline`, so syntax coloring, folding, generated markers, projected text, and custom drawing layers do not require a control subclass.

This document covers the extension API. Layout, virtualization, caching, and backend behavior are described in [Text Engine](TextEngine.md).

## Registering extensions

Core text hosts expose the pipeline directly:

```csharp
var viewer = new SyntaxViewer();
viewer.Extensions.Classifiers.Add(classifier);
viewer.Extensions.Projections.Add(projection);
viewer.InvalidateTextView();
```

`MultiLineTextBox.Extensions` and `SyntaxViewer.Extensions` are public. MewvalonEdit exposes the same pipeline through `editor.TextArea.TextView.Extensions`.

Choose the extension point by the kind of output it changes:

| Goal | Contract | Registration |
| --- | --- | --- |
| Foreground, background, underline, or strike over a range | `ITextClassifier` | `Extensions.Classifiers` |
| Font, size, weight, or another geometry-affecting style | `ITextLineTransformer` | `Extensions.Transformers` |
| Replace a document range with an inline object | `ITextElementGenerator` | `Extensions.ElementGenerators` |
| Replace the displayed text and map its offsets | `ITextProjection` | `Extensions.Projections` |
| Remove complete logical lines from the visual surface | `ITextLineCollapser` | `Extensions.LineCollapsers` |
| Draw arbitrary shapes or text in the view stack | `ITextViewLayer` | `InsertLayer` on the host |

Use a classifier for paint-only changes. Use a transformer only when glyph geometry or wrapping must change. Paint-only classification can reuse the existing layout geometry.

## First example: search highlighting

An `ITextClassifier` receives the projected display text, its logical source line, and the offset map between them. It outputs line-relative `TextPaintSpan` ranges.

```csharp
using Aprillz.MewUI.Text;

sealed class SearchHighlighter : ITextClassifier
{
    private static readonly Color MatchColor = Color.FromArgb(88, 255, 214, 0);

    public List<int> Matches { get; } = []; // absolute document offsets
    public int QueryLength { get; set; }

    public void Classify(in TextClassificationContext context, IList<TextPaintSpan> output)
    {
        int lineStart = context.LogicalLine.Offset;
        int lineEnd = lineStart + context.LogicalLine.Length;

        foreach (int matchStart in Matches)
        {
            if (matchStart >= lineEnd) break;
            int sourceStart = Math.Max(lineStart, matchStart) - lineStart;
            int sourceEnd = Math.Min(lineEnd, matchStart + QueryLength) - lineStart;
            if (sourceEnd <= sourceStart) continue;

            int displayStart = context.OffsetMap.MapFromSource(sourceStart);
            int displayEnd = context.OffsetMap.MapFromSource(sourceEnd);
            if (displayEnd > displayStart)
            {
                output.Add(new TextPaintSpan(
                    new TextRange(displayStart, displayEnd - displayStart),
                    Background: MatchColor));
            }
        }
    }
}
```

```csharp
var highlighter = new SearchHighlighter();
viewer.Extensions.Classifiers.Add(highlighter);

// Rebuild the visible lines after the query or match list changes.
viewer.InvalidateTextView();
```

Keep document-wide work such as parsing and match scanning outside `Classify`. The callback runs while a required line is being built and should only look up results intersecting that line.

## Paint spans

`TextPaintSpan` changes paint without changing the glyph layout:

```csharp
public readonly record struct TextPaintSpan(
    TextRange Range,
    Color? Foreground = null,
    Color? Background = null,
    TextDecoration Decoration = TextDecoration.None);
```

Ranges index the projected display text. Classifiers run in registration order. Later foreground values win where spans overlap, while backgrounds and decorations are painted in pipeline order.

## Geometry transforms

`ITextLineTransformer` can add `GeometryStyleRun` and `InlineRun` values after projections have produced the display text. Its context includes the default style and the current `ITextOffsetMap`.

Use a transformer for changes that affect measurement, such as a larger font, bold text, or an inline object. Do not use it for a foreground-only syntax color; that would invalidate geometry unnecessarily.

## Generated elements

`ITextElementGenerator` scans document offsets before the line text is read. It can replace a document range with an `IInlineTextObject`, leave the text in place while decorating the range, or reach across multiple logical lines.

An element that reaches past its starting logical line makes the visual line cover that whole source range. The swallowed logical lines must also be hidden by an `ITextLineCollapser`; otherwise they would be laid out again as independent lines.

Set `GeneratedTextElement.BreaksLine` when the generated object stands in for whitespace after which wrapping is allowed. Once whitespace becomes an object, the ordinary line breaker can no longer infer that opportunity from the source character.

## Projections and offset maps

`ITextProjection` replaces the displayed text before classification and geometry transformation:

```csharp
public interface ITextProjection
{
    ProjectedText Project(in TextProjectionContext context);
}

public readonly record struct ProjectedText(
    ReadOnlyMemory<char> Text,
    ITextOffsetMap OffsetMap);
```

Every projection must return an offset map. `MapFromSource` converts a source-line offset to a display offset; `MapToSource` converts a display offset back to the source. Return `IdentityTextOffsetMap.Instance` for a one-to-one substitution whose offsets do not change.

Multiple projections run in registration order and their maps are composed. Classifiers and transformers receive the composed map. Hit testing and caret geometry use it to return document offsets even when displayed text has a different length.

## Collapsing lines

`ITextLineCollapser.IsCollapsed` removes a complete logical line from the visual surface. Folding normally combines three pieces:

1. an element generator chooses the folded document range and optional placeholder object;
2. a projection or inline object supplies the visible placeholder;
3. a line collapser hides the logical lines covered by the first visual line.

## View layers

An `ITextViewLayer` paints arbitrary content in the view's draw stack:

```csharp
sealed class GuideLayer : ITextViewLayer
{
    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        var x = viewportBounds.X + 80;
        context.Graphics.DrawLine(
            new Point(x, viewportBounds.Y),
            new Point(x, viewportBounds.Bottom),
            Color.Gray,
            1);
    }
}

viewer.InsertLayer(
    new GuideLayer(),
    TextViewLayerAnchor.Text,
    TextLayerPosition.Above);
```

The built-in anchors are `Background`, `Selection`, `Text`, and `Caret`. A layer can be inserted `Below` or `Above` an anchor, or use `Replace` to take ownership of that built-in pass.

Layers may be cached by the host and are not guaranteed to run on every frame. When only layer appearance changed, call `InvalidateLayer(anchor)` instead of rebuilding line layouts.

## Pipeline order

For a line that must be materialized, the view performs these operations:

1. evaluate collapsed lines and scan element generators over document offsets;
2. read the required source slice;
3. apply projections in registration order and compose their offset maps;
4. run classifiers over the projected text;
5. convert generated objects to inline runs;
6. run geometry transformers;
7. create or reuse the text layout;
8. draw the layer stack.

Only materialized lines execute the line callbacks. Long logical lines may be supplied as viewport slices, so extensions must honor the offsets and lengths in their contexts rather than assume that they received a complete document line.

## Invalidation

Use the narrowest invalidation that matches the change:

- Document edits invalidate the affected view state automatically.
- Call `InvalidateTextRange(offset, length)` when cached semantic data changed for a known document range.
- Call `InvalidateTextView()` after changing registrations or global extension state. It increments the pipeline revision and rebuilds the required lines without resetting the reader's scroll position.
- Call `InvalidateLayer(anchor)` when drawing changed but line geometry did not.

Do not mutate the materialized-line collection from inside a callback. A range invalidation requested while lines are being built is deferred and applied after the current construction pass.
