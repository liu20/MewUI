# MewvalonEdit

A code editor for MewUI: syntax highlighting, folding, search, code completion, snippets, rectangle
selection and an undo stack, on top of the MewUI text engine. It follows
[AvalonEdit](https://github.com/icsharpcode/AvalonEdit)'s API and behavior so editor code written
against it ports with little change.

- Namespace: `Aprillz.MewUI.MewvalonEdit`
- Targets: `net8.0`, `net10.0`
- Part of [MewUI](https://github.com/aprillz/MewUI). Modeled on [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) (MIT - see `LICENSE.AvalonEdit`).
- 한국어: [README.ko.md](README.ko.md)

The implementation is native MewUI code, not a source translation. Layout, hit testing, viewport
virtualization and the editing baseline come from `Aprillz.MewUI.Text` and `MultiLineTextBox`; this
assembly adds the editor on top. Where the two designs disagree the MewUI one wins, and the surface
that leaves is listed under [Differences from AvalonEdit](#differences-from-avalonedit).

## Quick start

```csharp
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Highlighting;

var editor = new TextEditor
{
    ShowLineNumbers = true,
    SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#"),
    Text = File.ReadAllText("Program.cs"),
};

var window = new Window().Resizable(1000, 700).Content(editor);
Application.Run(window);
```

Features that own keys or windows are installed onto the editor rather than switched on:

```csharp
SearchPanel.Install(editor);      // Ctrl+F, F3, Shift+F3, Escape
FoldingManager.Install(editor);   // folding margin beside the line numbers
```

## Concepts

- **`TextEditor`** - the control. Owns the frame, the margins and the options, and forwards to the
  text surface underneath. This is what a host holds.
- **`TextArea`** - the editing surface's own state: caret, selection, input handlers, and the layers
  drawn over the text. Reached through `editor.TextArea`.
- **`TextView`** - what is on screen: visual lines, element generators, background renderers,
  margins. Reached through `editor.TextArea.TextView`.
- **`TextDocument`** - the text and everything anchored to it: lines, anchors, segments, the undo
  stack. A document can outlive the control that shows it.
- **Extension points** are the ones the original has. A **line transformer** recolours a laid out
  line, an **element generator** replaces a document range with something else (a link, a folded
  block), a **background renderer** draws at a named layer, a **margin** occupies the strip beside
  the text, and a **layer** can be inserted or replaced outright.

## `TextEditor`

| Member | Description |
|---|---|
| `Text : string` | The whole document. Assigning it starts over: the caret returns to the beginning and the undo history goes. |
| `Document : TextDocument` | The document being edited. Assignable, so two editors can share one. |
| `Options : TextEditorOptions` | Editing and display options; see below. |
| `SyntaxHighlighting : IHighlightingDefinition?` | Null leaves the text unhighlighted. |
| `ShowLineNumbers : bool`, `LineNumbersForeground : Color?` | The line number margin. |
| `WordWrap : bool`, `IsReadOnly : bool` | |
| `CaretOffset`, `SelectionStart`, `SelectionLength`, `SelectedText` | Editing state. |
| `Select(start, length)`, `SelectAll()`, `MoveCaret(position, extend)` | |
| `Copy()`, `Cut()`, `Paste()`, `AppendText(text)` | |
| `Undo() : bool`, `Redo() : bool`, `CanUndo`, `CanRedo` | Through the document's undo stack. |
| `BeginChange()` / `EndChange()` / `DeclareChangeBlock() : IDisposable` | Group edits into one undo step. |
| `IsModified : bool`, `IsModifiedChanged` | Distance from the last save point, as the original counts it. |
| `Load(fileName \| Stream)`, `Save(fileName \| Stream)`, `Encoding` | Loading works the encoding out and keeps it for saving. |
| `ScrollTo(line, column)`, `ScrollToLine`, `LineUp/Down`, `PageUp/Down`, `ScrollToHome/End` | |
| `VerticalOffset`, `HorizontalOffset`, `ExtentWidth/Height`, `ViewportWidth/Height` | |
| `GetPositionFromPoint(Point) : TextViewPosition?` | Null outside the text. |
| `IndentationStrategy : IIndentationStrategy?`, `IndentSelection()` | Ctrl+I runs the strategy over the selection. |
| `GetService<T>() : T?` | Editor, then text area, then view, then document. |
| `TextChanged`, `DocumentChanged`, `OptionChanged` | |

### `TextEditorOptions`

Every option is `virtual`, so a host can derive and override. Defaults match the original's.

| Option | Default | Description |
|---|---|---|
| `IndentationSize` | `4` | |
| `ConvertTabsToSpaces` | `false` | |
| `EnableVirtualSpace` | `false` | Caret past the end of a line. Rectangle selection uses it regardless. |
| `EnableRectangularSelection` | `true` | Alt+Shift movement and Alt-drag. |
| `AllowToggleOverstrikeMode` | `false` | Whether Insert switches to typing over. |
| `EnableImeSupport` | `true` | |
| `HideCursorWhileTyping` | `true` | |
| `CutCopyWholeLine` | `true` | Cut and copy take the caret's line when nothing is selected. |
| `ShowSpaces`, `ShowTabs`, `ShowEndOfLine`, `ShowBoxForControlCharacters` | `false` | Whitespace and control marks. |
| `ShowColumnRuler`, `ColumnRulerPosition` | `false`, `80` | |
| `HighlightCurrentLine` | `false` | |
| `EnableHyperlinks`, `EnableEmailHyperlinks` | `true` | |
| `RequireControlModifierForHyperlinkClick` | `true` | |

## Document

`TextDocument` carries the text and everything that points into it. Offsets move with edits, so
anchors and segments stay on the text they marked.

| Type | Description |
|---|---|
| `TextDocument` | Text, lines, `Insert`/`Remove`/`Replace`, `RunUpdate`, the `IndexOf` family, `FileName`. Notifications are `Changed` (with the offset and the inserted and removed lengths), `TextChanged`, `TextLengthChanged`, `LineCountChanged`, `FileNameChanged`. |
| `DocumentLine` | A line: offset, length, delimiter length, line number. Read from the document each time, so it survives edits. |
| `TextAnchor` | A position that rides edits, with `AnchorMovementType` deciding which way it leans on an insertion at it. |
| `ISegment`, `TextSegment`, `TextSegmentCollection<T>`, `AnchorSegment` | A document range. `ISegment` asks only for an offset and a length; ranges put in the collection follow edits on their own. |
| `UndoStack` | `Undo`/`Redo`, `OpenUndoGroup`, `SizeLimit`, `IsOriginalFile`, and change notifications. |
| `ITextSource`, `ITextSourceVersion`, `OffsetChangeMap` | Snapshots and the offset mapping between two versions. |
| `TextUtilities` | Word boundaries, caret positioning, line terminators, character classes. |

Line endings are kept per document. A document made from a file keeps the CR/LF form it was read
with instead of normalizing it.

## Highlighting

```csharp
editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
HighlightingManager.Instance.RegisterHighlighting("MyLang", [".mylang"], myDefinition);
```

Definitions are xshd, the format the original reads, and 21 are built in: C#, C++, Java, JavaScript,
HTML, ASPX, XML, XmlDoc, CSS, JSON, PHP, Python, PowerShell, VB, Boo, Coco, MarkDown (two), Patch,
TeX and TSQL. `RegisterHighlighting` also takes a factory, so a definition a host adds is read on
first use.

Colours are answered per paint by `HighlightingPalette`. A definition carries one colour name and
the palette gives the light or dark value for the current theme. A host with its own scheme replaces
`HighlightingPalette.Current`.

Highlighting that is not rule based derives `DocumentColorizingTransformer` and goes into
`TextView.LineTransformers`. `IHighlighter` and `DocumentHighlighter` are open for callers that need
to ask the engine for its state (span stacks and colours per line).

> **Highlighting is moving to TextMate.** xshd is AvalonEdit-era and does not keep up with current
> language grammars or themes; the goal is VS Code level highlighting from TextMate grammars and
> themes. It ships as a separate assembly because it brings a native dependency along. xshd is not
> removed: `HighlightingManager` and the definitions a host registers keep working.

## Folding

```csharp
var foldings = FoldingManager.Install(editor);
new BraceFoldingStrategy().UpdateFoldings(foldings, editor.Document);
```

`FoldingManager` owns the sections and installs the margin. `BraceFoldingStrategy` and
`XmlFoldingStrategy` come with it; a host that folds by language hands `NewFolding` ranges to
`UpdateFoldings`. A section is a `FoldingSection`: `IsFolded` opens and closes it, and `AllFoldings`
with `FoldingsChanged` reads the current state. A folded section draws as an outlined placeholder
that opens on a double click,
and the rest of the line the fold ends on stays on the same visual line.

## Search

```csharp
var search = SearchPanel.Install(editor);
search.SearchPattern = "TODO";
search.FindNext();
```

Installing wires the keys: Ctrl+F opens the panel, F3 and Shift+F3 walk the matches, Escape closes
it. `MatchCase`, `WholeWords` and `SearchMode` (`Normal`, `RegEx`, `Wildcard`) choose the strategy,
and `SearchStrategy` replaces it outright with an `ISearchStrategy`. `SearchStrategyFactory.Create`
builds one from the same options on its own. `ReplaceAll` returns how many it replaced. `Localization`
carries the strings the panel shows, and matches are painted with `MarkerBrush`.

Typing in the box searches as it goes and takes the first match at or after the selection. A pattern
that will not compile leaves the box in its invalid state while typing and says why when the reader
asks to search.

## Code completion

```csharp
var window = new CompletionWindow(editor.TextArea) { StartOffset = wordStart, EndOffset = caret };
window.CompletionList.CompletionData.Add(new CompletionData("Console", "System.Console"));
window.Show();
```

`CompletionWindow` is a popup list that filters as the reader types and commits on Enter or Tab.
`ICompletionData` carries the text, the description and an optional image; `CompletionData` is the
ready-made implementation. `InsightWindow` shows a signature beside the caret, and
`OverloadInsightWindow` with `IOverloadProvider` walks overloads with the arrow keys.

`CompletionList` and `OverloadViewer` are templated controls, so their appearance is a
`ControlTemplate` and their rows an `IDataTemplate`.

## Snippets

```csharp
var counter = new SnippetReplaceableTextElement { Text = "i" };
var snippet = new Snippet();
snippet.Elements.Add(new SnippetTextElement { Text = "for (int " });
snippet.Elements.Add(counter);
snippet.Elements.Add(new SnippetTextElement { Text = " = 0; " });
snippet.Elements.Add(new SnippetBoundElement { TargetElement = counter });
snippet.Insert(editor.TextArea);
```

Inserting starts an interactive session: Tab and Shift+Tab walk the replaceable elements, typing in
one updates every element bound to it, and `SnippetCaretElement` says where the caret lands at the
end. Escape, or an undo past the insertion, leaves the session.

## Editing

| Type | Description |
|---|---|
| `Caret` | Position, `Show`/`Hide`, `BringCaretToView`, and the caret colours. |
| `Selection` | `EmptySelection`, `SimpleSelection`, `RectangleSelection`. Segments, text, replacement. |
| `TextAreaInputHandler` | The input handler stack. A feature takes the keyboard with `Push`/`PopStackedInputHandler`. |
| `IReadOnlySectionProvider` | Which ranges refuse edits. `NoReadOnlySections` is the default. |
| `EditingCommands.IndentSelection` | Ctrl+I. Runs the indentation strategy; the default strategy does nothing. |

Rectangle selection is Alt+Shift with the arrow keys, Home and End, or Alt with a mouse drag. It
uses virtual space whatever `EnableVirtualSpace` says, types into every line it covers, and copies
and pastes column blocks within the process.

## Rendering

| Extension point | Add to | Purpose |
|---|---|---|
| `DocumentColorizingTransformer` | `TextView.LineTransformers` | Recolour a laid out line. |
| `VisualLineElementGenerator` | `TextView.ElementGenerators` | Replace a document range with an element. |
| `IBackgroundRenderer` | `TextView.BackgroundRenderers` | Draw at a `KnownLayer`. |
| `AbstractMargin` | `TextArea.LeftMargins` | Occupy the strip beside the text. |
| `ITextViewLayer` | `TextView.InsertLayer` | Insert a layer relative to a `KnownLayer` (`LayerInsertionPosition`), or replace one outright. |

The elements a generator makes derive from `VisualLineElement`: `TextReplacementElement` swaps the
text for another string, `InlineObjectElement` puts an arbitrary UI element in the line. A laid out
line is a `VisualLine`, and `TextViewPosition` carries an offset together with its visual column.

`LinkElementGenerator` and `MailLinkElementGenerator` are installed from the hyperlink options. The
line number margin (`LineNumberMargin`) and the folding margin (`FoldingMargin`) are margins of the
same kind.
`BackgroundGeometryBuilder` turns document ranges into the geometry a renderer draws, which is what
a squiggle or a marker service stands on. `MouseHoverLogic` raises the hover events a tooltip needs.

## Differences from AvalonEdit

In the original, not offered here.

| Surface | What to do instead |
|---|---|
| `TextDocument.LineTrackers`, `ILineTracker` | If it kept per-line data keyed by line number, update that from `TextDocument.Changed`. |
| `TextEditorOptions.InheritWordWrapIndentation`, `WordWrapIndentation` | Wrapped lines start at column zero. |
| `TextEditorOptions.AllowScrollBelowDocument` | Scrolling stops at the end of the document. |
| `TextArea.SelectionCornerRadius` | Selection corners are square. |

Added here, not in the original.

| Surface | What it does |
|---|---|
| `Caret.PrimaryCaretBrush`, `SecondaryCaretBrush` | A rectangle selection draws a caret on every line it crosses, the active corner in one colour and the rest in another. The original draws one caret. |

## Sample

`samples/MewUI.MewvalonEdit.Sample` drives the whole surface: language switching, folding, search,
completion, snippets, rectangle selection, the option toggles, and a VS Code style palette applied
to the built-in definitions.
