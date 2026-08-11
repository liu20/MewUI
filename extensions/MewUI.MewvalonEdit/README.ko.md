# MewvalonEdit

MewUI용 코드 편집기입니다. 구문 강조, 접기, 검색, 코드 완성, 스니펫, 사각 선택, 실행 취소 스택을 MewUI 텍스트 엔진 위에서 제공합니다. [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)의 API와 동작을 따르므로, AvalonEdit 기반으로 작성한 편집기 코드를 거의 수정 없이 옮길 수 있습니다.

- 네임스페이스: `Aprillz.MewUI.MewvalonEdit`
- 타깃: `net8.0`, `net10.0`
- [MewUI](https://github.com/aprillz/MewUI)의 확장입니다. [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)을 기반으로 하며 MIT 라이선스를 따릅니다(`LICENSE.AvalonEdit`).
- English: [README.md](README.md)

소스를 번역한 것이 아니라 MewUI 코드로 새로 구현했습니다. 레이아웃, 히트 테스트, 뷰포트 가상화, 편집 기본 동작은 `Aprillz.MewUI.Text`와 `MultiLineTextBox`가 담당하고, 이 어셈블리는 그 위에 편집기 기능을 더합니다. 두 설계가 충돌하는 지점에서는 MewUI 방식을 따랐으며, 그로 인한 차이는 [원본과 다른 점](#원본과-다른-점)에 정리했습니다.

## 빠른 시작

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

단축키나 별도 창을 사용하는 기능은 속성으로 켜는 대신 편집기에 설치합니다.

```csharp
SearchPanel.Install(editor);      // Ctrl+F, F3, Shift+F3, Escape
FoldingManager.Install(editor);   // 줄 번호 옆에 접기 마진 추가
```

## 구성 요소

- **`TextEditor`** - 호스트가 직접 사용하는 컨트롤입니다. 프레임, 마진, 옵션을 소유하고 실제 편집은 내부 텍스트 컨트롤에 위임합니다.
- **`TextArea`** - 편집 상태를 담당합니다. 캐럿, 선택, 입력 핸들러, 텍스트 위에 그려지는 레이어가 여기에 있습니다. `editor.TextArea`로 접근합니다.
- **`TextView`** - 화면에 표시되는 것을 담당합니다. 시각 줄, 요소 생성기, 배경 렌더러, 마진이 여기에 있습니다. `editor.TextArea.TextView`로 접근합니다.
- **`TextDocument`** - 텍스트와 문서에 딸린 정보를 담습니다. 줄, 앵커, 구간, 실행 취소 스택이 포함됩니다. 컨트롤과 수명이 분리되어 있어 문서를 먼저 만들어 두거나 여러 편집기가 공유할 수 있습니다.
- **확장점**은 원본과 같습니다. **줄 변환기**는 조판이 끝난 줄의 색을 바꾸고, **요소 생성기**는 문서 범위를 다른 요소로 대체하며(링크, 접힌 블록 등), **배경 렌더러**는 지정한 레이어에 그림을 그리고, **마진**은 텍스트 좌우의 영역을 차지하며, **레이어**는 통째로 끼워 넣거나 교체할 수 있습니다.

## `TextEditor`

| 멤버 | 설명 |
|---|---|
| `Text : string` | 문서 전체입니다. 대입하면 새 문서를 여는 것과 같아 캐럿이 맨 앞으로 이동하고 실행 취소 이력이 사라집니다. |
| `Document : TextDocument` | 편집 중인 문서입니다. 대입할 수 있으므로 두 편집기가 한 문서를 공유할 수 있습니다. |
| `Options : TextEditorOptions` | 편집과 표시 옵션입니다. 아래 표를 참고하세요. |
| `SyntaxHighlighting : IHighlightingDefinition?` | null이면 구문 강조를 하지 않습니다. |
| `ShowLineNumbers : bool`, `LineNumbersForeground : Color?` | 줄 번호 마진입니다. |
| `WordWrap : bool`, `IsReadOnly : bool` | |
| `CaretOffset`, `SelectionStart`, `SelectionLength`, `SelectedText` | 편집 상태입니다. |
| `Select(start, length)`, `SelectAll()`, `MoveCaret(position, extend)` | |
| `Copy()`, `Cut()`, `Paste()`, `AppendText(text)` | |
| `Undo() : bool`, `Redo() : bool`, `CanUndo`, `CanRedo` | 문서의 실행 취소 스택을 사용합니다. |
| `BeginChange()` / `EndChange()` / `DeclareChangeBlock() : IDisposable` | 여러 편집을 실행 취소 한 단계로 묶습니다. |
| `IsModified : bool`, `IsModifiedChanged` | 마지막 저장 지점과의 거리로 판단하며, 원본과 같은 방식입니다. |
| `Load(파일명 \| Stream)`, `Save(파일명 \| Stream)`, `Encoding` | 읽을 때 인코딩을 판별하고 저장할 때 그대로 사용합니다. |
| `ScrollTo(line, column)`, `ScrollToLine`, `LineUp/Down`, `PageUp/Down`, `ScrollToHome/End` | |
| `VerticalOffset`, `HorizontalOffset`, `ExtentWidth/Height`, `ViewportWidth/Height` | |
| `GetPositionFromPoint(Point) : TextViewPosition?` | 텍스트 영역 밖이면 null입니다. |
| `IndentationStrategy : IIndentationStrategy?`, `IndentSelection()` | Ctrl+I로 선택 범위에 들여쓰기 전략을 적용합니다. |
| `GetService<T>() : T?` | 편집기, 텍스트 영역, 뷰, 문서 순으로 조회합니다. |
| `TextChanged`, `DocumentChanged`, `OptionChanged` | |

### `TextEditorOptions`

모든 옵션이 `virtual`이므로 호스트가 파생 클래스에서 재정의할 수 있습니다. 기본값은 원본과 같습니다.

| 옵션 | 기본값 | 설명 |
|---|---|---|
| `IndentationSize` | `4` | |
| `ConvertTabsToSpaces` | `false` | |
| `EnableVirtualSpace` | `false` | 줄 끝 너머로 캐럿을 놓을 수 있게 합니다. 사각 선택은 이 값과 무관하게 사용합니다. |
| `EnableRectangularSelection` | `true` | Alt+Shift 이동과 Alt 드래그를 허용합니다. |
| `AllowToggleOverstrikeMode` | `false` | Insert 키로 덮어쓰기 모드를 전환할지 결정합니다. |
| `EnableImeSupport` | `true` | |
| `HideCursorWhileTyping` | `true` | |
| `CutCopyWholeLine` | `true` | 선택이 없을 때 잘라내기와 복사가 캐럿이 있는 줄 전체를 대상으로 합니다. |
| `ShowSpaces`, `ShowTabs`, `ShowEndOfLine`, `ShowBoxForControlCharacters` | `false` | 공백과 제어 문자를 표시합니다. |
| `ShowColumnRuler`, `ColumnRulerPosition` | `false`, `80` | |
| `HighlightCurrentLine` | `false` | |
| `EnableHyperlinks`, `EnableEmailHyperlinks` | `true` | |
| `RequireControlModifierForHyperlinkClick` | `true` | |

## 문서

`TextDocument`는 텍스트와 텍스트 내부를 가리키는 정보를 함께 관리합니다. 편집이 일어나면 오프셋이 자동으로 조정되므로 앵커와 구간은 처음 지정한 텍스트를 계속 가리킵니다.

| 타입 | 설명 |
|---|---|
| `TextDocument` | 텍스트, 줄, `Insert`/`Remove`/`Replace`, `RunUpdate`, `IndexOf` 계열, `FileName`을 제공합니다. 변경 알림은 `Changed`(변경 위치와 삽입·삭제 길이 포함), `TextChanged`, `TextLengthChanged`, `LineCountChanged`, `FileNameChanged`입니다. |
| `DocumentLine` | 줄 하나를 나타냅니다. 오프셋, 길이, 구분자 길이, 줄 번호를 제공하며 값을 문서에서 그때그때 읽으므로 편집 후에도 유효합니다. |
| `TextAnchor` | 편집에 따라 위치가 조정되는 지점입니다. 같은 위치에 삽입이 일어났을 때 어느 쪽으로 붙을지는 `AnchorMovementType`으로 정합니다. |
| `ISegment`, `TextSegment`, `TextSegmentCollection<T>`, `AnchorSegment` | 문서 범위를 나타냅니다. `ISegment`는 오프셋과 길이만 요구하는 최소 계약이고, 컬렉션에 넣은 구간은 편집을 따라 자동으로 조정됩니다. |
| `UndoStack` | `Undo`/`Redo`, `OpenUndoGroup`, `SizeLimit`, `IsOriginalFile`과 변경 알림을 제공합니다. |
| `ITextSource`, `ITextSourceVersion`, `OffsetChangeMap` | 스냅숏과 두 버전 사이의 오프셋 대응 정보입니다. |
| `TextUtilities` | 단어 경계, 캐럿 위치 규칙, 줄 종료 문자, 문자 분류를 다룹니다. |

개행 문자는 문서 단위로 보존합니다. 파일에서 읽은 문서는 원래의 CR/LF 형태를 정규화하지 않고 유지합니다.

## 구문 강조

```csharp
editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
HighlightingManager.Instance.RegisterHighlighting("MyLang", [".mylang"], myDefinition);
```

정의 파일은 원본과 같은 xshd 형식이며 21종이 내장되어 있습니다. C#, C++, Java, JavaScript, HTML, ASPX, XML, XmlDoc, CSS, JSON, PHP, Python, PowerShell, VB, Boo, Coco, MarkDown(2종), Patch, TeX, TSQL입니다. `RegisterHighlighting`은 팩토리도 받으므로 호스트가 추가한 정의는 처음 사용할 때 읽어 들입니다.

색은 화면을 그릴 때마다 `HighlightingPalette`가 결정합니다. 정의 파일에는 색 이름만 있고 팔레트가 현재 테마에 맞는 밝은 색과 어두운 색을 제공합니다. 자체 배색을 사용하려면 `HighlightingPalette.Current`를 교체하면 됩니다.

규칙 기반이 아닌 강조가 필요하면 `DocumentColorizingTransformer`를 파생해 `TextView.LineTransformers`에 추가합니다. 강조 엔진이 계산한 결과(줄별 스팬 스택과 색)를 직접 읽어야 한다면 `IHighlighter`와 `DocumentHighlighter`를 사용합니다.

> **구문 강조는 TextMate 방식으로 전환할 예정입니다.** xshd는 AvalonEdit 시절의 형식이라 최신 언어 문법과 테마를 따라가기 어렵습니다. 앞으로는 TextMate 문법과 테마를 사용해 VS Code 수준의 강조를 제공하는 것이 목표이며, 네이티브 의존이 추가되므로 별도 어셈블리로 제공합니다. xshd는 제거하지 않고 유지하므로 `HighlightingManager`와 호스트가 등록한 정의는 계속 동작합니다.

## 접기

```csharp
var foldings = FoldingManager.Install(editor);
new BraceFoldingStrategy().UpdateFoldings(foldings, editor.Document);
```

`FoldingManager`가 접기 구간을 관리하고 마진을 설치합니다. `BraceFoldingStrategy`와 `XmlFoldingStrategy`가 기본 제공되며, 언어에 맞는 접기가 필요하면 `NewFolding` 목록을 `UpdateFoldings`에 전달합니다. 개별 구간은 `FoldingSection`으로 다루며 `IsFolded`로 접거나 펼치고, `AllFoldings`와 `FoldingsChanged`로 현재 상태를 읽습니다. 접힌 구간은 테두리가 있는 상자로 표시되고 두 번 클릭하면 펼쳐집니다. 접힌 부분 뒤에 남은 텍스트는 같은 줄에 이어서 표시됩니다.

## 검색

```csharp
var search = SearchPanel.Install(editor);
search.SearchPattern = "TODO";
search.FindNext();
```

설치하면 단축키가 함께 등록됩니다. Ctrl+F로 패널을 열고, F3과 Shift+F3으로 결과를 이동하며, Escape로 닫습니다. `MatchCase`, `WholeWords`, `SearchMode`(`Normal`, `RegEx`, `Wildcard`)로 검색 전략이 결정되고, `SearchStrategy`에 `ISearchStrategy` 구현을 넣으면 전략 전체를 교체합니다. 같은 옵션으로 전략만 따로 만들려면 `SearchStrategyFactory.Create`를 사용합니다. `ReplaceAll`은 치환한 개수를 반환합니다. 패널에 표시되는 문구는 `Localization`으로 바꿀 수 있고, 일치 구간은 `MarkerBrush` 색으로 표시됩니다.

검색어를 입력하는 동안 검색이 진행되며 현재 선택 위치 이후의 첫 일치 항목을 선택합니다. 컴파일할 수 없는 정규식은 입력 중에는 입력란을 오류 상태로 표시하고, 검색을 실행할 때 이유를 알려 줍니다.

## 코드 완성

```csharp
var window = new CompletionWindow(editor.TextArea) { StartOffset = wordStart, EndOffset = caret };
window.CompletionList.CompletionData.Add(new CompletionData("Console", "System.Console"));
window.Show();
```

`CompletionWindow`는 입력에 따라 목록이 걸러지는 팝업이며 Enter나 Tab으로 확정합니다. 항목은 `ICompletionData`로 표현하고 텍스트, 설명, 아이콘을 담습니다. `CompletionData`가 기본 구현입니다. `InsightWindow`는 캐럿 옆에 시그니처를 표시하고, `OverloadInsightWindow`는 `IOverloadProvider`와 함께 방향키로 오버로드를 넘깁니다.

`CompletionList`와 `OverloadViewer`는 템플릿 컨트롤이므로 외형은 `ControlTemplate`으로, 각 행은 `IDataTemplate`으로 교체할 수 있습니다.

## 스니펫

```csharp
var counter = new SnippetReplaceableTextElement { Text = "i" };
var snippet = new Snippet();
snippet.Elements.Add(new SnippetTextElement { Text = "for (int " });
snippet.Elements.Add(counter);
snippet.Elements.Add(new SnippetTextElement { Text = " = 0; " });
snippet.Elements.Add(new SnippetBoundElement { TargetElement = counter });
snippet.Insert(editor.TextArea);
```

삽입하면 편집 세션이 시작됩니다. Tab과 Shift+Tab으로 입력할 요소 사이를 이동하고, 한 요소를 수정하면 거기에 연결된 요소가 함께 바뀝니다. 세션이 끝났을 때 캐럿이 놓일 위치는 `SnippetCaretElement`로 지정합니다. Escape를 누르거나 삽입 시점 이전으로 실행 취소하면 세션이 종료됩니다.

## 편집

| 타입 | 설명 |
|---|---|
| `Caret` | 위치, `Show`/`Hide`, `BringCaretToView`, 캐럿 색을 제공합니다. |
| `Selection` | 선택 상태입니다. `EmptySelection`, `SimpleSelection`, `RectangleSelection` 세 가지가 있고 선택 범위, 선택된 텍스트, 치환을 다룹니다. |
| `TextAreaInputHandler` | 입력 핸들러 스택입니다. 특정 기능이 키 입력을 먼저 처리해야 할 때 `Push`/`PopStackedInputHandler`로 올리고 내립니다. |
| `IReadOnlySectionProvider` | 편집을 막을 범위를 지정합니다. 기본값은 `NoReadOnlySections`입니다. |
| `EditingCommands.IndentSelection` | Ctrl+I로 들여쓰기 전략을 실행합니다. 기본 전략은 아무 동작도 하지 않습니다. |

사각 선택은 Alt+Shift와 방향키, Home, End를 조합하거나 Alt를 누른 채 마우스로 드래그해 만듭니다. `EnableVirtualSpace` 설정과 무관하게 가상 공간을 사용하고, 선택된 모든 줄에 입력이 적용되며, 프로세스 안에서는 열 단위 복사와 붙여넣기가 가능합니다.

## 렌더링

| 확장점 | 추가 위치 | 역할 |
|---|---|---|
| `DocumentColorizingTransformer` | `TextView.LineTransformers` | 조판이 끝난 줄의 색을 바꿉니다. |
| `VisualLineElementGenerator` | `TextView.ElementGenerators` | 문서 범위를 다른 요소로 대체합니다. |
| `IBackgroundRenderer` | `TextView.BackgroundRenderers` | 지정한 `KnownLayer`에 그립니다. |
| `AbstractMargin` | `TextArea.LeftMargins` | 텍스트 옆 영역을 차지합니다. |
| `ITextViewLayer` | `TextView.InsertLayer` | `KnownLayer` 기준으로 레이어를 끼워 넣거나(`LayerInsertionPosition`) 통째로 대체합니다. |

요소 생성기가 만드는 요소는 `VisualLineElement`에서 파생하며, 텍스트를 다른 문자열로 바꾸는 `TextReplacementElement`와 임의의 UI 요소를 줄 안에 넣는 `InlineObjectElement`가 준비되어 있습니다. 조판된 줄은 `VisualLine`으로 표현하고, 문서 오프셋과 시각 열의 대응은 `TextViewPosition`이 담습니다.

`LinkElementGenerator`와 `MailLinkElementGenerator`는 하이퍼링크 옵션에 따라 자동으로 설치됩니다. 줄 번호 마진(`LineNumberMargin`)과 접기 마진(`FoldingMargin`)도 같은 방식의 마진입니다. `BackgroundGeometryBuilder`는 문서 범위를 렌더러가 그릴 도형으로 변환하며, 오류 물결선이나 마커 서비스를 이 위에 구현합니다. 툴팁에 필요한 hover 이벤트는 `MouseHoverLogic`이 제공합니다.

## 원본과 다른 점

원본에 있으나 제공하지 않는 것입니다.

| 원본의 API | 대응 |
|---|---|
| `TextDocument.LineTrackers`, `ILineTracker` | 줄 단위로 데이터를 따로 관리해야 한다면 `TextDocument.Changed`를 사용하세요. |
| `TextEditorOptions.InheritWordWrapIndentation`, `WordWrapIndentation` | 줄바꿈된 줄은 들여쓰기 없이 0열에서 시작합니다. |
| `TextEditorOptions.AllowScrollBelowDocument` | 문서 끝까지만 스크롤됩니다. |
| `TextArea.SelectionCornerRadius` | 선택 영역 모서리는 둥글게 표시되지 않습니다. |

원본에 없으나 추가한 것입니다.

| 추가한 API | 역할 |
|---|---|
| `Caret.PrimaryCaretBrush`, `SecondaryCaretBrush` | 사각 선택은 포함된 모든 줄에 캐럿을 표시하며, 조작 중인 모서리와 나머지 줄을 다른 색으로 구분합니다. 원본은 캐럿을 하나만 표시합니다. |

## 샘플

`samples/MewUI.MewvalonEdit.Sample`에서 전체 기능을 확인할 수 있습니다. 언어 전환, 접기, 검색, 코드 완성, 스니펫, 사각 선택, 옵션 토글과 내장 정의에 적용한 VS Code 계열 배색을 포함합니다.
