# 텍스트 뷰 확장

MewUI [텍스트 엔진](TextEngine.ko.md)은 문서 레이아웃과 선택적인 뷰 동작을 분리합니다. `MultiLineTextBox`, `SyntaxViewer`, MewvalonEdit은 같은 `TextViewExtensionPipeline`을 노출하므로 컨트롤을 상속하지 않고 syntax coloring, folding, generated marker, projected text, 사용자 정의 drawing layer를 추가할 수 있습니다.

이 문서는 확장 API를 설명합니다. 레이아웃, 가상화, 캐시, 백엔드 동작은 [텍스트 엔진](TextEngine.ko.md)을 참고하십시오.

## 확장 등록

코어 텍스트 호스트는 파이프라인을 직접 노출합니다.

```csharp
var viewer = new SyntaxViewer();
viewer.Extensions.Classifiers.Add(classifier);
viewer.Extensions.Projections.Add(projection);
viewer.InvalidateTextView();
```

`MultiLineTextBox.Extensions`와 `SyntaxViewer.Extensions`는 public입니다. MewvalonEdit은 `editor.TextArea.TextView.Extensions`를 통해 같은 파이프라인을 노출합니다.

변경하려는 결과에 따라 확장 지점을 선택합니다.

| 목적 | 계약 | 등록 위치 |
| --- | --- | --- |
| 범위의 전경색, 배경색, 밑줄, 취소선 | `ITextClassifier` | `Extensions.Classifiers` |
| font, 크기, 굵기처럼 geometry에 영향을 주는 스타일 | `ITextLineTransformer` | `Extensions.Transformers` |
| 문서 범위를 inline object로 치환 | `ITextElementGenerator` | `Extensions.ElementGenerators` |
| 표시 텍스트를 바꾸고 offset을 매핑 | `ITextProjection` | `Extensions.Projections` |
| 완전한 logical line을 visual surface에서 제외 | `ITextLineCollapser` | `Extensions.LineCollapsers` |
| 뷰 stack에 임의의 도형이나 텍스트 그리기 | `ITextViewLayer` | 호스트의 `InsertLayer` |

paint만 바꿀 때는 classifier를 사용합니다. glyph geometry나 wrapping이 바뀔 때만 transformer를 사용합니다. paint-only classification은 기존 layout geometry를 재사용할 수 있습니다.

## 첫 예제: 검색 하이라이트

`ITextClassifier`는 projected display text, 그 텍스트의 logical source line, 두 좌표계를 연결하는 offset map을 받습니다. 출력하는 `TextPaintSpan` 범위는 줄 내부의 display offset입니다.

```csharp
using Aprillz.MewUI.Text;

sealed class SearchHighlighter : ITextClassifier
{
    private static readonly Color MatchColor = Color.FromArgb(88, 255, 214, 0);

    public List<int> Matches { get; } = []; // 문서 절대 오프셋
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

// 검색어나 match 목록이 바뀐 뒤 보이는 줄을 다시 구성합니다.
viewer.InvalidateTextView();
```

문서 전체 parsing이나 match 검색은 `Classify` 밖에서 수행하십시오. callback은 필요한 줄을 구성하는 중에 실행되므로 해당 줄과 겹치는 결과를 조회하는 일만 해야 합니다.

## Paint span

`TextPaintSpan`은 glyph layout을 변경하지 않고 paint를 바꿉니다.

```csharp
public readonly record struct TextPaintSpan(
    TextRange Range,
    Color? Foreground = null,
    Color? Background = null,
    TextDecoration Decoration = TextDecoration.None);
```

범위는 projected display text를 기준으로 합니다. classifier는 등록 순서대로 실행됩니다. span이 겹치면 나중 전경색이 우선하며, 배경과 decoration은 파이프라인 순서로 그려집니다.

## Geometry transform

`ITextLineTransformer`는 projection이 display text를 만든 뒤 `GeometryStyleRun`과 `InlineRun`을 추가할 수 있습니다. context에는 default style과 현재 `ITextOffsetMap`이 포함됩니다.

더 큰 font, bold text, inline object처럼 측정 결과에 영향을 주는 변경에 transformer를 사용합니다. 전경색만 바꾸는 syntax color에 사용하면 불필요하게 geometry가 무효화됩니다.

## Generated element

`ITextElementGenerator`는 줄 텍스트를 읽기 전에 문서 오프셋을 탐색합니다. 문서 범위를 `IInlineTextObject`로 치환하거나, 텍스트는 그대로 두고 범위만 장식하거나, 여러 logical line에 걸친 범위를 선택할 수 있습니다.

시작 logical line을 넘어가는 element는 하나의 visual line이 전체 source range를 덮게 합니다. 이 visual line에 포함된 뒤쪽 logical line은 `ITextLineCollapser`로 함께 숨겨야 합니다. 그렇지 않으면 독립된 줄로 다시 배치됩니다.

generated object가 줄바꿈 가능한 공백을 대신한다면 `GeneratedTextElement.BreaksLine`을 설정합니다. 공백이 object로 바뀐 뒤에는 일반 line breaker가 source 문자만 보고 break opportunity를 알 수 없습니다.

## Projection과 offset map

`ITextProjection`은 classification과 geometry transform 전에 표시 텍스트를 바꿉니다.

```csharp
public interface ITextProjection
{
    ProjectedText Project(in TextProjectionContext context);
}

public readonly record struct ProjectedText(
    ReadOnlyMemory<char> Text,
    ITextOffsetMap OffsetMap);
```

모든 projection은 offset map을 반환해야 합니다. `MapFromSource`는 source-line offset을 display offset으로 바꾸고, `MapToSource`는 display offset을 source로 되돌립니다. 오프셋이 변하지 않는 일대일 치환에는 `IdentityTextOffsetMap.Instance`를 반환합니다.

여러 projection은 등록 순서대로 실행되며 offset map도 합성됩니다. classifier와 transformer는 합성된 map을 받습니다. hit test와 caret geometry 역시 이 map을 사용하므로 표시 문자열 길이가 달라져도 문서 오프셋을 반환합니다.

## 줄 접기

`ITextLineCollapser.IsCollapsed`는 완전한 logical line을 visual surface에서 제외합니다. 일반적인 folding은 세 부분을 조합합니다.

1. element generator가 접을 문서 범위와 선택적인 placeholder object를 결정합니다.
2. projection이나 inline object가 보이는 placeholder를 제공합니다.
3. line collapser가 첫 visual line에 포함된 뒤쪽 logical line을 숨깁니다.

## 뷰 레이어

`ITextViewLayer`는 뷰의 drawing stack에 임의의 내용을 그립니다.

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

기본 anchor는 `Background`, `Selection`, `Text`, `Caret`입니다. 레이어를 anchor의 `Below`나 `Above`에 삽입하거나, `Replace`로 해당 기본 drawing pass를 넘겨받을 수 있습니다.

호스트는 레이어 결과를 캐시할 수 있으므로 `Draw`가 매 프레임 호출된다고 가정하면 안 됩니다. 레이어의 모양만 바뀌었다면 line layout을 다시 만들지 말고 `InvalidateLayer(anchor)`를 호출합니다.

## 파이프라인 순서

줄을 materialize할 때 뷰는 다음 순서로 처리합니다.

1. collapsed line을 평가하고 문서 오프셋에서 element generator를 탐색합니다.
2. 필요한 source slice를 읽습니다.
3. projection을 등록 순서대로 적용하고 offset map을 합성합니다.
4. projected text에 classifier를 실행합니다.
5. generated object를 inline run으로 변환합니다.
6. geometry transformer를 실행합니다.
7. text layout을 생성하거나 재사용합니다.
8. layer stack을 그립니다.

줄 callback은 materialized line에 대해서만 실행됩니다. 매우 긴 logical line은 viewport slice로 전달될 수 있으므로, 확장 구현은 완전한 문서 줄을 받았다고 가정하지 말고 context의 offset과 length를 따라야 합니다.

## 무효화

변경 내용에 맞는 가장 좁은 무효화를 사용합니다.

- 문서 편집은 영향을 받은 view state를 자동으로 무효화합니다.
- 알려진 문서 범위의 semantic cache가 바뀌었다면 `InvalidateTextRange(offset, length)`를 호출합니다.
- 등록 목록이나 전역 확장 상태가 바뀌었다면 `InvalidateTextView()`를 호출합니다. pipeline revision을 증가시키고 스크롤 위치를 초기화하지 않은 채 필요한 줄을 다시 구성합니다.
- drawing만 바뀌고 line geometry는 그대로라면 `InvalidateLayer(anchor)`를 호출합니다.

callback 안에서 materialized-line collection을 변경하지 마십시오. 줄 구성 중 요청된 범위 무효화는 현재 construction pass가 끝난 뒤 실행되도록 연기됩니다.
