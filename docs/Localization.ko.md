# Localization

MewUI는 프레임워크가 직접 그리는 사용자 노출 문자열 - 메시지 박스, 비지 인디케이터, 텍스트 박스 컨텍스트 메뉴, 관리형 파일 다이얼로그, 컬러 피커 - 을 한곳, 정적 `MewUIStrings` 클래스에 모아 둡니다. 모든 항목의 기본값은 영어이며, 값을 대입해 번역합니다. 앱이 직접 그리는 문자열은 앱이 관리하며, `MewUIStrings`는 MewUI가 대신 그려 주는 텍스트만 다룹니다.

---

## 1. 동작 방식

`MewUIStrings`는 각 문자열을 `ObservableValue<string>`으로 노출합니다. 현재 텍스트는 `.Value`로 읽고, `.Value`에 대입해 바꿉니다.

```csharp
MewUIStrings.CommonOK.Value = "확인(_O)";
```

대부분의 프레임워크 UI는 이 값을 **UI를 구축하는 시점**에 읽습니다. 메시지 박스와 파일 다이얼로그(사이드바 포함)는 표시할 때마다 다시 구축되므로, UI가 나타나기 전에 값을 설정해 두면 충분합니다. 값을 바꿔도 이미 화면에 떠 있는 다이얼로그의 제목은 바뀌지 않으며, 다음에 열 때 새 텍스트가 반영됩니다.

표준 편집 Command는 예외적으로 live binding을 사용합니다. `StandardCommands.Cut`/`Copy`/`Paste` 등의
`CommandPresentation.AccessText`는 `MewUIStrings.CommandCut`/`CommandCopy`/`CommandPaste`에 바인딩됩니다.
따라서 이 값을 바꾸면 이미 열린 메뉴와 Command 표시를 사용하도록 opt-in한 Button도 즉시 갱신됩니다.
앱 Command도 `new Command(...).BindText(AppStrings.Save)`와 같이 같은 방식을 사용할 수 있습니다.

번역은 시작 시점에 한 번 설정하고, 사용자가 실행 중에 언어를 바꾸면 다음 다이얼로그가 열리기 전에 다시 설정합니다.

---

## 2. 문자열 그룹

멤버는 `{Area}{Role}` 명명 규칙을 따르며, `Area` 접두어가 소속 그룹을 나타냅니다. 그룹과 각 그룹이 다루는 범위는 다음과 같습니다.

| 그룹 (`Area`) | 다루는 범위 |
| --- | --- |
| `Common` | 공용 버튼 라벨(OK, Cancel, Yes, No, Retry, Ignore, Abort). 메시지 박스, 비지 인디케이터, 파일 다이얼로그가 함께 재사용. |
| `Prompt` | 아이콘별 메시지 박스 제목과 "자세히 보기" 토글. |
| `BusyIndicator` | 비지 인디케이터의 중단 확인 문구와 진행 텍스트. Abort/Yes/No 버튼은 `Common` 그룹을 사용. |
| `Command` | 표준 편집 Command의 live 표시 텍스트(`Undo`, `Redo`, `Cut`, `Copy`, `Paste`, `Delete`, `SelectAll`). |
| `TextBoxContextMenu` | `Command` 항목을 가리키는 호환 이름. 같은 `ObservableValue` 인스턴스를 공유합니다. |
| `FileDialog` | 관리형(인앱) 파일 다이얼로그 크롬: 창 제목, accept 버튼, 필드 라벨, 내비 툴팁, 뷰 토글, 필터 이름, 컬럼 헤더. Cancel 버튼은 `CommonCancel`을 재사용. |
| `Sidebar` | 파일 다이얼로그 사이드바 섹션 헤더(플랫폼별 관례). |
| `Folder` | 파일 다이얼로그 사이드바의 알려진 폴더 바로가기 라벨. |
| `ColorPicker` | Hex 입력 라벨. 채널 문자(R/G/B/H/S/V/A)는 로케일 중립 기호라 번역하지 않음. |

각 그룹의 정확한 멤버와 기본값은 `src/MewUI/Core/MewUIStrings.cs`가 정본입니다. 에디터에서 `MewUIStrings.`을 입력하면 접두어별로 훑어볼 수 있습니다.

---

## 3. 액세스 키(니모닉)

일부 버튼 값에는 `"_OK"`, `"_Save"`처럼 언더스코어가 들어 있습니다. 언더스코어는 **액세스 키** 표시로, 바로 뒤의 문자가 Alt 단축키가 되며 Alt를 누르는 동안 밑줄로 표시됩니다. 번역에서도 언더스코어를 유지하되, 단축키로 쓸 문자 앞에 두세요 - 예: `"확인(_O)"`.

액세스 키는 Windows와 Linux의 관례입니다. macOS는 사용하지 않으며(Option 키가 문자 입력용으로 예약됨) 그곳에서는 언더스코어가 그대로 제거됩니다.

---

## 4. 컬처 적용

`MewUIStrings`는 영어 기본값을 갖고 있고, `MewUIStrings.ResetToDefaults()`로 언제든 모든 문자열을 그 영어 베이스라인으로 되돌릴 수 있습니다. 먼저 베이스라인으로 리셋한 뒤 활성 컬처의 문자열을 재정의합니다.

```csharp
static void ApplyStrings()
{
    MewUIStrings.ResetToDefaults(); // 영어 베이스라인

    switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
    {
        case "ko": ApplyKorean(); break;
        // 다른 컬처는 여기에 추가
    }
}

static void ApplyKorean()
{
    MewUIStrings.CommonOK.Value = "확인(_O)";
    MewUIStrings.CommonCancel.Value = "취소(_C)";
    MewUIStrings.PromptError.Value = "오류";
    // 이 컬처가 설정하지 않은 문자열은 영어 기본값으로 남는다
}
```

첫 창을 표시하기 전에 `ApplyStrings`를 호출하고 언어가 바뀔 때마다 다시 호출하세요. 먼저 리셋하면 매 호출이 완전한 영어 집합에서 시작하므로, 컬처가 번역하지 않은 문자열은 - 부분 번역이라 누락됐든 이전에 선택한 언어에서 남았든 - 낡은 값 대신 영어로 유지됩니다. 구축 시점 소비자는 다음 표시부터, Command 표시 소비자는 즉시 새 값을 반영합니다.

---

## 5. 여기에 없는 것

`MewUIStrings`는 프레임워크 문자열보다 더 나은 출처가 있는 텍스트는 의도적으로 제외합니다.

- **컬처 기반 서식** - `Calendar`의 요일/월 이름은 `CultureInfo.CurrentCulture`에서 오므로 별도 대입 없이 OS/앱 컬처를 따릅니다.
- **OS 소유 명칭** - 파일 다이얼로그의 실제 드라이브/볼륨 라벨은 운영체제에서 옵니다. "Macintosh HD", "File System" 같은 루트 볼륨 fallback은 여기가 아니라 플랫폼 레이어에 남습니다. 번역 대상 UI 용어가 아니라 OS 고유명이기 때문입니다.
- **앱 자체 문자열** - 직접 작성한 창 제목/라벨/메시지는 MewUI 범위 밖이며, 앱 자체 자원으로 지역화하세요.

> 알려진 폴더 라벨(`Folder*`)은 영어가 기본값입니다. OS도 이 폴더들의 지역화된 표시명을 제공하지만, 그것은 앱의 `CurrentUICulture`가 아니라 OS 언어를 따르므로 MewUI는 여기서 앱이 제어할 수 있게 둡니다. 다른 그룹처럼 컬처별로 대입하세요.
