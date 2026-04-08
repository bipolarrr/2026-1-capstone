다음 SceneBuilder 작성을 수행할 때, 아래에 명시된 규칙을 반드시 따라라.

[목표]
아래에 적은 규칙은 기존 MainMenuSceneBuilder 코드에서 추출한 스타일 가이드다.
이번 작업에서는 이 규칙만을 기준으로 구현하라.

중요:
- 원본 MainMenuSceneBuilder 파일 전체를 다시 읽어서 분석하려고 하지 마라.
- 아래 규칙만을 신뢰 가능한 기준으로 사용하라.
- 추가 근거가 꼭 필요하면 파일 전체를 읽지 말고, 필요한 최소 코드 조각만 요청하라.
- 구현 디테일보다 네이밍, 구조, 책임 분리, UI 생성 방식, 참조 연결 방식의 일관성을 우선하라.

[가장 중요하게 따라야 할 규칙]

1. 네이밍 규칙
- 클래스, 메서드, private const, struct 이름은 PascalCase를 사용한다.
- 지역 변수는 camelCase를 사용한다.
- 축약어는 최대한 피하고, 의미가 드러나는 긴 이름을 사용한다.
  - 예: `cameraObject`, `menuButtonsTransform`, `settingsPopupObject`
- GameObject 이름도 역할이 명확한 PascalCase 문자열로 작성한다.
  - 예: `"MainCamera"`, `"MenuRoot"`, `"SettingsPopup"`, `"SpeechBubble"`

2. 코드 구조 규칙
- 큰 진입 메서드에서 전체 흐름을 위에서 아래로 읽히게 구성한다.
- 섹션 단위 주석으로 블록을 명확히 구분한다.
  - 예: `// ── Canvas ──────────────────────────────────────────────────`
- 반복되거나 의미 단위가 있는 생성 로직은 private helper 메서드로 분리한다.
  - 예: `CreateUIPanel`, `CreateTMPText`, `CreateMenuButton`, `CenterPopup`
- “한 메서드가 하나의 역할”을 갖도록 유지한다.
- 여러 UI 요소를 묶는 복합 생성 로직은 별도 빌드 메서드로 분리한다.
  - 예: `BuildSettingsPopup`, `BuildCreditsPopup`
- 여러 값을 반환해야 하면 무리하게 out/ref를 남발하지 말고, 작은 private struct를 정의해 반환한다.
  - 예: `SliderRowResult`

3. 책임 분리 규칙
- 씬 생성/오브젝트 배치/기본 참조 연결은 Builder가 담당한다.
- 실제 런타임 동작 로직은 별도 MonoBehaviour 컨트롤러가 담당한다.
  - 예: `MainMenuController`, `MainMenuButtonHandler`, `CharacterEasterEggController`, `SettingsPopupController`
- Builder 안에 게임 진행 로직, UI 상태 머신, 버튼별 세부 비즈니스 로직을 직접 넣지 않는다.
- “무엇을 생성하는가”와 “생성된 오브젝트가 런타임에 어떻게 동작하는가”를 분리한다.

4. Unity 스타일 규칙
- UI는 코드로 명시적으로 생성하고, 부모-자식 관계를 분명하게 설정한다.
- RectTransform 설정은 anchorMin / anchorMax / offsetMin / offsetMax / pivot / sizeDelta / anchoredPosition 등을 직접 명확히 지정한다.
- 스트레치가 필요한 경우 공통 helper를 사용해 일관되게 처리한다.
  - 예: `SetStretch`
- Canvas, EventSystem, InputSystemUIInputModule, TextMeshProUGUI 등 현재 프로젝트의 UI 스택을 유지한다.
- 레거시 UI 방식보다 현재 코드가 이미 사용 중인 방식(TMP, Input System)을 우선한다.

5. 참조 연결 규칙
- Inspector 수동 연결을 전제로 하지 말고, 가능하면 Builder에서 자동 연결한다.
- 자동 연결 시 기존 코드처럼 private field 연결을 허용한다.
- 연결 실패 시 조용히 무시하지 말고 경고를 남긴다.
  - 예: `Debug.LogWarning(...)`
- 버튼 이벤트 연결은 `UnityEventTools.AddPersistentListener` 같은 방식으로 명시적으로 연결한다.
- 참조 연결 코드는 “컴포넌트 부착 → 필요한 필드 연결 → 이벤트 연결” 순서로 읽히게 정리한다.

6. 스타일 및 문체 규칙
- 불필요하게 짧은 이름, 과도한 추상화, 난해한 패턴을 피한다.
- early return을 적극 사용한다.
  - 예: 파일이 이미 존재하고 덮어쓰기를 거부하면 즉시 return
- 조건문과 반복문에서 중괄호/들여쓰기를 일관되게 유지한다.
- null 체크는 간단명료하게 처리한다.
- 코드가 “에디터 툴로서 읽기 쉬운가”를 우선한다.
- 지나친 디자인 패턴 적용보다, 직관적인 생성 절차와 명시성을 우선한다.

7. 하드코딩 및 수치 사용 규칙
- 현재 코드처럼 UI 배치 수치, 색상, 앵커 값 등은 읽기 쉬운 위치에 직접 배치해도 된다.
- 단, 같은 의미의 수치가 반복되면 helper 또는 상수화로 정리한다.
- 숫자를 감추기 위한 억지 추상화는 하지 말고, 오히려 “이 수치가 무엇을 의미하는지”가 잘 보이게 유지한다.

8. 생성 코드의 계층 규칙
- UI 트리는 의미 단위로 그룹화한다.
  - 예: `LogoGroup`, `MenuButtons`, `CharacterAnchor`, `SettingsPopup`
- 자식 오브젝트 이름은 부모 컨텍스트 안에서 읽었을 때 역할이 명확해야 한다.
  - 예: `"Body"`, `"Face"`, `"EyeLeft"`, `"EyeRight"`, `"ClickArea"`
- 단순 장식 요소라도 이름을 명확히 부여한다.
  - 예: `"BackgroundTopGradient"`, `"DecorationLineTop"`

9. 수정 시 지켜야 할 방향성
- 기존 `MainMenuSceneBuilder`와 톤이 맞지 않는 과도한 아키텍처 개편은 하지 않는다.
- DI 프레임워크, 복잡한 팩토리 계층, ScriptableObject 남용, 과한 제네릭 추상화는 추가하지 않는다.
- 기존 코드가 가진 “직접 생성 + helper 분리 + 컨트롤러 연결” 패턴을 유지한다.
- 새 기능을 추가하더라도 기존 메서드명, 계층 구성 방식, UI 배치 표현 방식을 해치지 않는다.

10. 출력 규칙
- 결과물은 바로 프로젝트에 넣을 수 있는 완성 코드로 작성한다.
- 변경 시에는 기존 규칙을 깨지 않는 최소 범위 수정부터 우선한다.
- 새 파일이 필요하면 파일명까지 자연스럽게 제안하되, 현재 스타일과 맞게 작성한다.
- 설명은 최소화하고, 코드와 구조 자체로 의도를 드러내라.

[구현 시 반드시 유지할 패턴 요약]
- 정적 Builder 클래스
- 진입점 메서드 1개 + 여러 private helper 메서드
- 명확한 섹션 주석
- 의미가 드러나는 긴 지역 변수명
- UI 오브젝트를 코드로 생성
- RectTransform 값을 명시적으로 설정
- 런타임 로직은 별도 Controller 컴포넌트에 위임
- private field 자동 연결 허용
- 경고 로그는 남기되 실패를 조용히 숨기지 않음
- 버튼 이벤트는 명시적으로 연결
- 읽는 순서만으로 씬 구조가 보이게 작성

[이 작업에서 하지 말 것]
- 기존 스타일을 무시하고 파일 전체를 다른 아키텍처로 갈아엎지 말 것
- 지역 변수명을 `go`, `obj`, `tmp`, `ctrl`처럼 짧게 축약하지 말 것
- helper로 분리 가능한 반복 UI 생성 코드를 무식하게 복붙하지 말 것
- Builder 안에 실제 게임 상태 처리 로직을 넣지 말 것
- 연결 실패를 silent fail로 덮지 말 것
- 기존에 없는 과한 프레임워크성 구조를 도입하지 말 것