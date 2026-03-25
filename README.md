# 2026-1 Capstone

Unity 6 기반 턴제 주사위 전투 게임 프로토타입.

## 프로젝트 구조

```
Assets/
  Editor/          씬 빌더 (에디터 전용)
  Scripts/
    CharacterSelect/   캐릭터 선택 화면
    Dice/              주사위 전투 시스템
    MainMenu/          메인 메뉴
```

- **씬 파일(.unity)은 Git에 포함하지 않습니다.** 씬은 에디터 메뉴에서 빌드합니다.
- 유료 에셋, 폰트, Unity 자동 생성 파일 등은 `.gitignore`로 제외됩니다.

## 씬 빌더 아키텍처

이 프로젝트는 Unity 씬을 `.unity` 파일로 직접 편집하지 않고, **C# 씬 빌더 스크립트**로 프로그래매틱하게 생성합니다.

### 왜 씬 빌더인가

- **재현성**: 씬 빌더를 실행하면 언제나 동일한 씬이 생성됩니다. 에셋 경로나 컴포넌트 설정이 코드로 명시되어 있어 환경 차이로 인한 불일치가 발생하지 않습니다.
- **코드 리뷰 가능**: `.unity` 파일은 YAML 직렬화 특성상 diff가 사실상 불가능하지만, 씬 빌더는 일반 C# 코드이므로 PR 리뷰와 변경 추적이 용이합니다.
- **일관성**: UI 레이아웃, 앵커, 폰트, 색상 등을 상수와 헬퍼 함수로 관리하여 화면 간 시각적 일관성을 유지합니다.
- **머지 충돌 방지**: 바이너리에 가까운 `.unity` 파일의 머지 충돌을 근본적으로 회피합니다.

### 씬 빌드 방법

Unity 에디터 메뉴에서 실행:

| 메뉴 경로 | 생성 씬 |
|-----------|---------|
| `Tools > Build MainMenu Scene` | 메인 메뉴 |
| `Tools > Build CharacterSelect Scene` | 캐릭터 선택 |
| `Tools > Build DiceTest Scene` | 주사위 전투 테스트 |

## 코딩 컨벤션

- 들여쓰기: **탭 1개** (스페이스 금지)
- 헝가리안 표기법 금지 (`btn`, `txt`, `img` 등 타입 접두/접미사 사용하지 않음)
- 한 줄짜리 제어문은 중괄호 없이 다음 줄에 작성
  ```csharp
  if (condition)
      DoSomething();
  ```
- `[SerializeField]` 필드명은 씬 빌더의 `SetField` 리플렉션과 연결되므로 변경 시 양쪽 동기화 필요

## 주사위 전투 시스템

5개의 d6 주사위를 최대 3회 굴려 콤보 데미지를 적에게 가합니다.

| 콤보 | 조건 | 데미지 |
|------|------|--------|
| Yacht | 5개 동일 | 50 |
| Four of a Kind | 4개 동일 | 40 |
| Large Straight | 2-3-4-5-6 | 35 |
| Large Straight | 1-2-3-4-5 | 30 |
| Full House | 3개 + 2개 | 25 |
| Small Straight | 4개 연속 | 20 |
| 없음 | - | 눈의 합 |

주사위를 클릭하면 보관(빨간 테두리), 다시 클릭하면 복귀합니다. 보관된 주사위는 다음 굴리기에서 제외됩니다.

## 기술 요구사항

- Unity 6.0+
- Universal Render Pipeline (URP)
- TextMesh Pro (Mona12 폰트 에셋)
- Dice 에셋 팩 (`Assets/Dices/Prefabs/Dice_d6.prefab`)
