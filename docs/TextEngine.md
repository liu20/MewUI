# Text System and Engine

MewUI's text system starts at the control-facing content models and converges on one layout and rendering contract. `TextBlock`, its `Inlines` and `Run` values, single-line input, multiline input, syntax views, and editor extensions are different consumers of the same engine rather than separate text formatters.

This document describes the complete path from those public text models to layout, viewport virtualization, and backend drawing. The lower-level contracts live in `Aprillz.MewUI.Text`. The specialized extension API is covered in [Text View Extensions](TextViewExtensions.md).

## Architecture

```text
TextBlock + Inlines/Run       TextBox / MultiLineTextBox       SyntaxViewer / editor
           |                           |                              |
           |                           +-- EditableTextDocument ------+
           |                                          |
           v                                          v
 TextLayoutRequest                         TextViewLayout + extensions
           |                                          |
           +--------------------+---------------------+
                                v
                    IGraphicsFactory.TextEngine
                                |
                                v
                         ITextLayout
                                |
                                v
                      IGraphicsContext.Text
                                |
                                v
                   backend realization and drawing
```

The system has three layers:

- **Content and control models** present ordinary text, styled runs, editable documents, carets, selections, and editor features.
- **Layout and view models** turn content into retained geometry. Small text uses `ITextLayout` directly; document controls use `TextViewLayout` to materialize only the required visual lines and slices.
- **Rendering** draws retained layouts through the current graphics context without leaking platform types into controls.

The layout and drawing surfaces are deliberately separate:

- `IGraphicsFactory.TextEngine` owns layout creation and retained layout caches. A layout is independent of a frame or render target.
- `IGraphicsContext.Text` is the frame-bound drawing surface. It realizes a layout for the active backend and render target.
- `ITextLayout` exposes geometry and navigation queries without exposing DirectWrite, GDI, CoreText, FreeType, or another backend type.

## TextBlock, Inlines, and Run

`TextBlock` is part of the text-engine architecture, not a parallel formatting path. `TextBlockBase` converts the control state into a `TextLayoutRequest`, retains the resulting `ITextLayout`, uses it during measure, and draws it through `IGraphicsContext.Text`.

`TextBlock.Inlines` is a control-facing authoring model:

- the text of all `Run` values is flattened into `TextBlock.Text`, which is the string sent to the engine;
- font family, size, weight, italic, and decoration overrides become `GeometryStyleRun` values and therefore participate in measurement and wrapping;
- foreground and background overrides become `TextPaintSpan` values and can repaint the retained geometry without rebuilding it;
- a text or geometry change invalidates layout, while a paint-only change invalidates rendering only.

`Run` and `InlineRun` are deliberately different types. `Run` is a styled span in `TextBlock.Inlines`. `InlineRun` is a lower-level engine input that replaces a text range with an `IInlineTextObject` having its own metrics and drawing behavior. A future richer inline content model can target `InlineRun` without changing the engine contract.

`AccessText` also derives from `TextBlockBase`: it produces display text and paint spans for mnemonic presentation, then follows the same layout and drawing path.

## Creating and drawing a layout

Use `CreateLayout` for an uncached layout. Use `GetOrCreateLayout` when the same content or owner will be laid out repeatedly.

```csharp
using Aprillz.MewUI.Text;

var request = new TextLayoutRequest
{
    Text = "Hello, MewUI".AsMemory(),
    Dpi = 96,
    DefaultStyle = new TextRunStyle("Segoe UI", 14),
    Paragraph = new TextParagraphStyle
    {
        MaxWidth = 320,
        Wrapping = TextWrapping.Wrap,
        Alignment = TextAlignment.Left
    },
    Revision = 1
};

var layout = factory.TextEngine.GetOrCreateLayout(
    request,
    TextLayoutCachePolicy.Owner,
    owner);

var options = new TextDrawOptions(Color.White, Owner: owner);
context.Text.Draw(layout, new Point(8, 8), in options);
```

`TextLayoutRequest` separates geometry from paint:

- `TextRunStyle` and `GeometryStyleRun` change font metrics, glyph advances, decorations, and wrapping.
- `TextParagraphStyle` controls width, height, wrapping, trimming, alignment, tab stops, and line metrics.
- `InlineRun` replaces a text range with an `IInlineTextObject` that participates in line measurement and drawing.
- `TextPaintSpan` and `TextOverlay` change colors or paint range backgrounds without changing line geometry.

Paint-only changes can reuse the same `ITextLayout`.

## Layout queries

`ITextLayout` is both the retained layout result and the geometry query surface. It provides:

- measured size, content height, and per-line metrics;
- point-to-text hit testing;
- caret rectangles;
- logical and visual caret movement;
- rectangles covering a text range.

Offsets are UTF-16 insertion positions. `CaretMode.TextElement` moves across Unicode text-element boundaries, while `CaretMode.CodeUnit` exposes UTF-16 code-unit movement.

## Fast Path and Full Path

Both paths return the same `ITextLayout` contract.

The Fast Path is selected for a single left-to-right, no-wrap run with no tabs, line breaks, inline objects, geometry runs, trimming, or letter spacing. It measures long input in bounded segments and materializes detailed caret advances only for the segment being queried. Drawing can realize only the range intersecting the current clip.

The Full Path is used when layout needs wrapping, tabs, multiple geometry styles, inline objects, explicit line breaks, trimming, or letter spacing. It builds Unicode text-element clusters, assembles visual lines, and retains the geometry required for hit testing and range drawing.

Fast Path is an implementation choice, not a second public engine. Callers must rely on `ITextLayout`, not on which path was selected.

## Documents and text views

`IReadOnlyTextDocument` supplies text by range and maps offsets to logical lines. The built-in implementations are:

- `StringTextDocument` for immutable text;
- `EditableTextDocument` for incremental edits and line indexing.

`TextViewLayout` maps a document onto a viewport. It owns:

- logical-line to visual-line construction;
- wrapping and no-wrap viewport slices;
- document-offset and viewport-coordinate mapping;
- line height and width indexes;
- materialized-line reuse and range invalidation;
- classifiers, projections, generated elements, and geometry transforms.

`ITextViewHost` is the control-facing surface around a view. It exposes the current document, visible lines, viewport and extent metrics, scrolling, invalidation, extension registration, and the text layer stack.

## Virtualization

The view never requires a control to turn the complete document into one layout.

- Only lines intersecting the viewport are materialized.
- A very long wrapped logical line is represented by an estimated row map and a bounded slice around the visible rows.
- A very long no-wrap logical line is represented by an estimated horizontal map and a bounded slice around the visible columns.
- Caret lookup may construct an off-screen slice without retaining every slice between the viewport and the target.
- `ExtentHeight` and `ExtentWidth` are refined as line measurements become available.

The regression suite exercises 10-million-character wrapped and unwrapped logical lines and verifies that viewport initialization, scrolling, drawing, and end-caret lookup do not materialize the complete line.

## Text consumers

The common engine is shared by display, input, and editor controls, but not every consumer needs document virtualization:

- `TextBlock` and `AccessText` build one retained `ITextLayout` directly. `TextBlock.Inlines` and `Run` are adapted as described above.
- `Calendar` builds retained layouts directly for its bounded cell and header labels.
- `TextBox` and `PasswordBox` use the no-wrap text view path through `SingleLineTextBase`.
- `MultiLineTextBox` and `SyntaxViewer` use `TextViewLayout` for document-to-viewport mapping and virtualization.
- MewvalonEdit composes its editor UI and language features over the same view and extension contracts.

`TextEditorSession` applies caret, selection, replacement, undo, and redo operations to an `EditableTextDocument`. Editing controls compose that state with the view engine. `SyntaxViewer` has no editing session and consumes the document/view side only.

## View layers

Text hosts draw four built-in anchors in order:

1. `Background`
2. `Selection`
3. `Text`
4. `Caret`

An `ITextViewLayer` can be inserted below, above, or in place of an anchor. Layers receive `ITextRenderContext`, so they can draw text through the engine and shapes through `ITextRenderContext.Graphics`. See [Text View Extensions](TextViewExtensions.md#view-layers) for registration and invalidation.

## Caching and lifetime

The engine has two managed cache policies:

- `Content` shares layouts with the same complete request identity. Inline objects are not allowed because their lifetime is owner-specific.
- `Owner` retains one current layout per owner and revision. Use `ReleaseOwner` when a long-lived owner no longer needs its cached layout.

Content caching is bounded. Owner entries use weak owner association. A graphics context also keeps a bounded cache of backend run realizations and releases backend handles when entries are evicted or the context is disposed.

`ITextLayout` itself is not disposable. `TextViewLayout` is disposable because it owns per-line cache owners and subscriptions.

## Backend boundary

The public engine obtains measurement and font services from the active `IGraphicsFactory`, and `ITextRenderContext` realizes runs through the active `IGraphicsContext`.

The Windows regression matrix covers Direct2D, GDI, and MewVG Win32. Linux and macOS use their platform font and drawing implementations behind the same contracts.

## Extending a view

The line/view engine can be extended without subclassing a control. Its pipeline supports:

- paint classification;
- geometry-affecting line transforms;
- generated inline elements;
- projected display text with offset mapping;
- collapsed logical lines;
- custom drawing layers.

The registration API, execution order, offset rules, invalidation, and examples are covered in [Text View Extensions](TextViewExtensions.md).
