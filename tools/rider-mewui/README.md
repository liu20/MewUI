# rider-mewui

MewUI 에디터 프리뷰의 JetBrains Rider 플러그인.

프론트엔드(JVM) 단독 구성: VS Code 확장이 node에서 하던 세션 레이어(dotnet watch 스폰, 루프백 프로토콜 서버)를 Kotlin이 그대로 수행하므로 ReSharper 백엔드 컴포넌트가 필요 없다. `PreviewSession.kt`/`PreviewProtocol.kt`는 `tools/vscode-mewui`의 session.ts/protocol.ts 포팅.

- 프로세스 트리 종료는 `ProcessHandle.descendants()` 기반이라 Windows/macOS/Linux 공통 (VS Code 확장의 taskkill/프로세스 그룹 분기 불필요).
- 프레임은 BGRA 바이트를 little-endian int로 읽으면 그대로 ARGB라 `BufferedImage(TYPE_INT_ARGB)`에 무변환 전송.

## 빌드

JDK 17+ 필요 (JBR 25로 Gradle 데몬을 돌리는 것은 KGP 호환 미보증이라 별도 JDK 권장, 이 저장소 검증은 `~/.jdks/jdk-21.0.11+10`).

```
set JAVA_HOME=%USERPROFILE%\.jdks\jdk-21.0.11+10
gradlew buildPlugin -PriderHome="C:\Program Files\JetBrains\JetBrains Rider 2026.2"
```

- `-PriderHome`: 로컬 Rider 설치본을 컴파일 대상으로 사용 (SDK 다운로드 생략). 생략 시 `rider("2025.1")` SDK를 내려받는다.
- Kotlin 플러그인 버전은 대상 IDE의 Kotlin 메타데이터를 읽을 수 있어야 한다 (Rider 2026.2 = Kotlin 2.4 → KGP 2.4.0, `build.gradle.kts` 주석 참조).
- 산출물: `build/distributions/rider-mewui-0.1.0.zip`

## 설치 / 실행

- 설치: Rider Settings > Plugins > ⚙ > Install Plugin from Disk... 에서 zip 선택.
- 개발 실행: `gradlew runIde -PriderHome=...` (샌드박스 Rider 인스턴스).
- 사용: 우측 도구 창 "MewUI Preview" > 프로젝트 선택 > Start.

## 상태

빌드/패키징 검증 완료 (2026-07-25, Rider 2026.2 = RD-262 대상, buildSearchableOptions의 헤드리스 IDE 로드 통과). **IDE 내 상호작용 E2E는 미검증.**
