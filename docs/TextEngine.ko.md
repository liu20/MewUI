# 텍스트 시스템과 엔진

MewUI의 텍스트 시스템은 컨트롤이 노출하는 콘텐츠 모델에서 시작해 하나의 레이아웃 및 렌더링 계약으로 수렴합니다. `TextBlock`, `Inlines`와 `Run`, 단일/멀티라인 입력, syntax view, editor extension은 서로 다른 텍스트 포매터가 아니라 같은 엔진의 소비자입니다.

이 문서는 이 공개 텍스트 모델부터 레이아웃, viewport 가상화, 백엔드 drawing까지 이어지는 전체 경로를 설명합니다. 저수준 계약은 `Aprillz.MewUI.Text`에 있습니다. 전문화된 확장 API는 [텍스트 뷰 확장](TextViewExtensions.ko.md)에서 설명합니다.

## 구조

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
                   backend realization과 drawing
```

시스템은 세 계층으로 구성됩니다.

- **콘텐츠 및 컨트롤 모델**은 일반 텍스트, styled run, editable document, caret, selection, editor 기능을 표현합니다.
- **레이아웃 및 뷰 모델**은 콘텐츠를 retained geometry로 변환합니다. 짧은 텍스트는 `ITextLayout`을 직접 사용하고, 문서 컨트롤은 `TextViewLayout`을 통해 필요한 visual line과 slice만 materialize합니다.
- **렌더링**은 플랫폼 타입을 컨트롤에 노출하지 않고 현재 graphics context를 통해 retained layout을 그립니다.

레이아웃과 그리기 surface는 의도적으로 분리되어 있습니다.

- `IGraphicsFactory.TextEngine`은 레이아웃 생성과 retained layout cache를 소유합니다. 레이아웃은 특정 프레임이나 render target에 결합되지 않습니다.
- `IGraphicsContext.Text`는 프레임에 결합된 그리기 surface입니다. 활성 백엔드와 render target에 맞게 레이아웃을 realization합니다.
- `ITextLayout`은 DirectWrite, GDI, CoreText, FreeType 같은 백엔드 타입을 노출하지 않고 geometry와 navigation query를 제공합니다.

## TextBlock, Inlines, Run

`TextBlock`은 텍스트 엔진 아키텍처의 일부이며 병렬 formatting 경로가 아닙니다. `TextBlockBase`는 컨트롤 상태를 `TextLayoutRequest`로 변환하고, 반환된 `ITextLayout`을 보유해 measure에 사용하며, `IGraphicsContext.Text`를 통해 그립니다.

`TextBlock.Inlines`는 컨트롤 사용자를 위한 authoring model입니다.

- 모든 `Run`의 텍스트를 `TextBlock.Text`로 평탄화하고, 이 문자열을 엔진에 전달합니다.
- font family, size, weight, italic, decoration override는 `GeometryStyleRun`이 되어 측정과 wrapping에 참여합니다.
- foreground와 background override는 `TextPaintSpan`이 되므로 retained geometry를 다시 만들지 않고 repaint할 수 있습니다.
- text나 geometry 변경은 layout을 무효화하지만 paint-only 변경은 render만 무효화합니다.

`Run`과 `InlineRun`은 의도적으로 다른 타입입니다. `Run`은 `TextBlock.Inlines` 안의 styled span입니다. `InlineRun`은 텍스트 범위를 자체 metric과 drawing 동작을 가진 `IInlineTextObject`로 치환하는 저수준 엔진 입력입니다. 향후 더 풍부한 inline content model도 엔진 계약을 바꾸지 않고 `InlineRun`으로 변환할 수 있습니다.

`AccessText`도 `TextBlockBase`를 상속합니다. mnemonic 표시용 display text와 paint span을 만든 뒤 같은 레이아웃 및 drawing 경로를 따릅니다.

## 레이아웃 생성과 그리기

캐시하지 않을 레이아웃에는 `CreateLayout`을 사용합니다. 같은 내용이나 owner를 반복해서 배치한다면 `GetOrCreateLayout`을 사용합니다.

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

`TextLayoutRequest`는 geometry와 paint를 분리합니다.

- `TextRunStyle`과 `GeometryStyleRun`은 font metric, glyph advance, decoration, 줄바꿈에 영향을 줍니다.
- `TextParagraphStyle`은 너비, 높이, wrapping, trimming, alignment, tab stop, line metric을 제어합니다.
- `InlineRun`은 텍스트 범위를 줄 측정과 그리기에 참여하는 `IInlineTextObject`로 바꿉니다.
- `TextPaintSpan`과 `TextOverlay`는 줄 geometry를 바꾸지 않고 색이나 범위 배경을 변경합니다.

paint만 바뀌었다면 같은 `ITextLayout`을 다시 사용할 수 있습니다.

## 레이아웃 조회

`ITextLayout`은 retained layout 결과이면서 geometry query surface입니다. 다음 정보를 제공합니다.

- 측정 크기, content height, 줄별 metric
- point에서 텍스트 위치로 hit test
- caret rectangle
- logical/visual caret 이동
- 텍스트 범위를 덮는 rectangle 목록

오프셋은 UTF-16 삽입 위치입니다. `CaretMode.TextElement`는 Unicode text-element 경계를 따라 이동하고, `CaretMode.CodeUnit`은 UTF-16 code unit 단위 이동을 노출합니다.

## Fast Path와 Full Path

두 경로는 모두 같은 `ITextLayout` 계약을 반환합니다.

Fast Path는 tab, 줄바꿈, inline object, geometry run, trimming, letter spacing이 없는 단일 LTR no-wrap run에 선택됩니다. 긴 입력을 제한된 길이의 segment로 측정하고, 조회 중인 segment에 대해서만 상세 caret advance를 구체화합니다. 그릴 때도 현재 clip과 겹치는 범위만 realization할 수 있습니다.

Full Path는 wrapping, tab, 여러 geometry style, inline object, 명시적 줄바꿈, trimming, letter spacing이 필요할 때 사용됩니다. Unicode text-element cluster를 만들고 visual line을 조립하며 hit test와 range drawing에 필요한 geometry를 유지합니다.

Fast Path는 구현 선택이지 두 번째 공개 엔진이 아닙니다. 호출자는 어떤 경로가 선택됐는지가 아니라 `ITextLayout` 계약에 의존해야 합니다.

## 문서와 텍스트 뷰

`IReadOnlyTextDocument`는 범위 단위로 텍스트를 제공하고 오프셋을 logical line에 매핑합니다. 기본 구현은 다음과 같습니다.

- `StringTextDocument`: 변경되지 않는 텍스트
- `EditableTextDocument`: 증분 편집과 line index가 필요한 텍스트

`TextViewLayout`은 문서를 viewport에 매핑하며 다음을 소유합니다.

- logical line에서 visual line 구성
- wrap/no-wrap viewport slice
- 문서 오프셋과 viewport 좌표 매핑
- line height/width index
- materialized line 재사용과 범위 무효화
- classifier, projection, generated element, geometry transform 적용

`ITextViewHost`는 컨트롤이 뷰를 노출하는 surface입니다. 현재 문서, 보이는 줄, viewport/extent metric, 스크롤, 무효화, 확장 등록, 텍스트 layer stack을 제공합니다.

## 가상화

뷰는 컨트롤이 전체 문서를 하나의 레이아웃으로 만들도록 요구하지 않습니다.

- viewport와 겹치는 줄만 materialize합니다.
- 매우 긴 wrapped logical line은 추정 row map과 보이는 row 주변의 제한된 slice로 표현합니다.
- 매우 긴 no-wrap logical line은 추정 horizontal map과 보이는 column 주변의 제한된 slice로 표현합니다.
- off-screen caret 조회는 viewport와 대상 사이의 모든 slice를 보유하지 않고 대상 slice만 구성할 수 있습니다.
- `ExtentHeight`와 `ExtentWidth`는 실제 줄 측정 결과가 생길 때 정교해집니다.

회귀 테스트는 1천만 문자의 wrap/no-wrap logical line을 사용해 viewport 초기화, 스크롤, 그리기, 끝 caret 조회가 전체 줄을 materialize하지 않는지 검증합니다.

## 텍스트 소비자

공통 엔진은 표시, 입력, editor 컨트롤이 함께 사용하지만 모든 소비자에게 문서 가상화가 필요한 것은 아닙니다.

- `TextBlock`과 `AccessText`는 하나의 retained `ITextLayout`을 직접 만듭니다. `TextBlock.Inlines`와 `Run`은 위에서 설명한 방식으로 변환됩니다.
- `Calendar`는 제한된 크기의 cell과 header label을 위해 retained layout을 직접 만듭니다.
- `TextBox`와 `PasswordBox`는 `SingleLineTextBase`를 통해 no-wrap text view 경로를 사용합니다.
- `MultiLineTextBox`와 `SyntaxViewer`는 document-to-viewport mapping과 가상화를 위해 `TextViewLayout`을 사용합니다.
- MewvalonEdit은 같은 view와 extension 계약 위에 editor UI와 language feature를 조합합니다.

`TextEditorSession`은 `EditableTextDocument`에 caret, selection, replace, undo, redo 동작을 적용합니다. 편집 컨트롤은 이 상태를 뷰 엔진과 조합합니다. `SyntaxViewer`에는 editing session이 없으며 document/view 계층만 소비합니다.

## 뷰 레이어

텍스트 호스트는 다음 네 개의 기본 anchor를 순서대로 그립니다.

1. `Background`
2. `Selection`
3. `Text`
4. `Caret`

`ITextViewLayer`는 anchor의 아래, 위 또는 anchor를 대체하는 위치에 삽입할 수 있습니다. 레이어는 `ITextRenderContext`를 받으므로 텍스트는 엔진으로 그리고, 도형은 `ITextRenderContext.Graphics`로 그릴 수 있습니다. 등록과 무효화 방법은 [텍스트 뷰 확장](TextViewExtensions.ko.md#뷰-레이어)을 참고하십시오.

## 캐시와 수명

엔진은 두 가지 managed cache policy를 제공합니다.

- `Content`는 완전한 요청 identity가 같은 레이아웃을 공유합니다. inline object는 owner별 수명을 가지므로 허용되지 않습니다.
- `Owner`는 owner와 revision마다 현재 레이아웃 하나를 유지합니다. 장기간 살아 있는 owner가 더 이상 레이아웃을 사용하지 않으면 `ReleaseOwner`를 호출합니다.

content cache는 크기가 제한되어 있습니다. owner entry는 weak owner association을 사용합니다. graphics context도 백엔드 run realization을 제한된 수만 보유하며, cache eviction이나 context dispose 시 backend handle을 해제합니다.

`ITextLayout` 자체는 disposable이 아닙니다. `TextViewLayout`은 줄별 cache owner와 구독을 소유하므로 disposable입니다.

## 백엔드 경계

공개 엔진은 측정과 font service를 활성 `IGraphicsFactory`에서 얻고, `ITextRenderContext`는 활성 `IGraphicsContext`를 통해 run을 realization합니다.

Windows 회귀 행렬은 Direct2D, GDI, MewVG Win32를 검증합니다. Linux와 macOS는 같은 계약 뒤에서 각 플랫폼의 font/그리기 구현을 사용합니다.

## 텍스트 뷰 확장

line/view 엔진은 컨트롤을 상속하지 않고도 확장할 수 있습니다. 파이프라인은 다음 기능을 지원합니다.

- paint classification
- geometry에 영향을 주는 line transform
- generated inline element
- offset map을 포함하는 projected display text
- collapsed logical line
- 사용자 정의 drawing layer

등록 API, 실행 순서, offset 규칙, 무효화와 예제는 [텍스트 뷰 확장](TextViewExtensions.ko.md)에서 설명합니다.
