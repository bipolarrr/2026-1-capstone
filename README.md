# 2026-1 Capstone — 무기 선택 턴제 로그라이크

Unity 6 (`6000.3.11f1`) 기반 턴제 전투 프로토타입. 플레이어는 캐릭터 선택에서 **주사위** 또는 **마작패** 무기를 고른 뒤, 탐험-전투-보상 루프를 돌며 스테이지를 진행합니다.

## 게임 흐름

```
MainMenu → CharacterSelect → GameExplore ↔ (DiceBattle | MahjongBattle) → 보스 → 승리
                                     ↕
                                  아이템 상자
```

| 씬 | 역할 |
|---|---|
| MainMenu | 시작, 설정 |
| CharacterSelect | 컷씬 슬라이드 + 무기 선택 (주사위 / 마작패 / *플레잉카드*†) |
| GameExplore | 걷기 → 일반전투 / 아이템 상자 / 보스전 순환 |
| DiceBattleScene | 주사위 전투 (야트 룰 기반) |
| MahjongBattleScene | 1인 리치마작 전투 |

† 플레잉카드(홀덤) 캐릭터 슬롯은 UI에만 존재하며 아직 미구현.

## 스테이지 시스템

스테이지는 데이터 주도 — `Assets/Scripts/Stages/` 에 `static Build()` 메서드를 가진 파일을 만들어 `StageRegistry` 에 등록합니다. 몹 풀, 배경, 보스, 라운드 시퀀스가 `StageData` 하나에 선언적으로 담깁니다.

| ID | 이름 | 배경 | 몹 풀 | 보스 |
|---|---|---|---|---|
| `forest_1` | 어둠의 숲 | `Fight_Background_0_Forest.png` | 슬라임 · 고블린 · 박쥐 · 해골 | 어둠의 지배자 |
| `cave_2` | 끝없는 동굴 | `Fight_Background_1_Cave.png` | 박쥐 · 골렘 · 물의 정령 · 고블린 | 동굴의 수호자 |

몹별 `bodyXMin/Max · bodyYMin/Max` 로 바디 앵커를 튜닝, 스테이지별 `playerGroundYOffset` 로 플레이어 지면 위치를 보정합니다.

## 전투 시스템 — 주사위

### 공격 페이즈

5개의 d6 물리 주사위를 최대 3회 굴립니다. 클릭으로 홀드/해제, 홀드된 주사위는 다음 굴림에서 제외.

| 족보 | 데미지 |
|---|---:|
| YACHT (5개 동일) | 50 |
| Four of a Kind | 40 |
| Large Straight (2-6) | 35 |
| Large Straight (1-5) | 30 |
| Full House (3+2) | 25 |
| Small Straight (4연속) | 20 |
| 없음 | 눈의 합 |

- 족보 공격: 대상 100%, 비대상 50% 스플래시
- 일반 공격: 스플래시 없음

### 방어 페이즈 (적별 개별 처리)

| 적 랭크 | 방어 굴림 | 판정 |
|---|---|---|
| 1~3 (★~★★★) | 1회, 자동 판정 | 적 주사위 구성이 내 5개 주사위의 부분집합이면 100% 방어 |
| 4~5 + 족보 | 3회 (홀드 가능) | 같은 족보 성공 시 100% 방어, 실패 시 피격 |

적 공격 데미지(절반하트): `ceil(rank × multiplier)` — 기본 0.5, 족보 구간 1.5~3.

## 전투 시스템 — 마작

표준 136장 1인 리치마작. 치·퐁·명깡 없음. 도라 인디케이터 5장 + 쯔모 131장. 손패 13+1.

- 1쯔모 → 1버림 반복. 버림 즉시 모든 적에게 `EnemyMahjongState.OnPlayerDiscard` 통지
- **전체 화료**: 한수 기반 테이블 → 절반하트 AOE 데미지 (1한=1, 3한=3, 満貫=4, 跳満=6, 倍満=8, 三倍満=12, 役満=16)
- **중간 포기**: `(멘츠×0.5 + 머리×0.25 + 깡×0.75) × 0.5` 절반하트 AOE
- 적 1~3랭크: 대기 조합 2개(슌쯔/커쯔/또이츠 40/40/20 분포). 발동 시 로그에 `[백][백][백]의 커쯔! 데미지 N.` 형태로 표기
- 4·5랭크 마작 전투는 미설계 (추후 확장)

## 체력 (하트)

- 5칸(10반칸) Red 하트로 시작
- 하트 타입: Red(일반) · Black(아머) · Blue(임시)
- 흡수 순서: Blue → Black → Red
- UI: ● 꽉참 / ◐ 반칸 / ○ 빈칸

## 파워업

탐험 중 아이템 상자에서 획득:

| 파워업 | 효과 |
|---|---|
| OddEvenDouble | 모든 눈이 홀수 또는 짝수면 데미지 ×2 |
| AllOrNothing | 족보 데미지 ÷2, 일반 데미지 ×2 |
| ReviveOnce | 치명타 1회 무효화 (1회용) |

## 프로젝트 구조

```
Assets/
  Editor/            씬 빌더 (Tools 메뉴에서 실행)
  Scripts/
    Battle/          전투 컨트롤러 베이스, 데미지/방어, 적 주사위, 하트
    CharacterSelect/ 캐릭터 선택 컨트롤러 + 컷씬 슬라이드
    Dice/            YachtDie 물리 주사위, 뷰포트 인터랙션
    Explore/         탐험 컨트롤러 (스테이지별 라운드 순환)
    Game/            GameSessionManager, 하트, 디버그 콘솔, 파워업
    MainMenu/        메인 메뉴 UI
    Mahjong/         마작 패산/손패/버림, 역 평가, 적 대기 상태
    Stages/          StageData/StageRegistry + 스테이지 정의 파일
  Dices/             d4~d20 3D 프리팹
  Mahjong/           마작 타일 스프라이트 + MahjongTileSprites.asset
  Mobs/              몹/배경 스프라이트 (Fight_Background_{idx}_{name}.png)
  Stages/            (레거시) 스테이지 맵 에셋
  Player/            플레이어 아이들/롤 스프라이트
```

## 씬 빌더

씬 파일(`.unity`)은 Git에 포함하지 않습니다. 런타임 `[SerializeField]` 참조는 빌더가 `SceneBuilderUtility.SetField()` 로 주입하므로 필드 이름 변경 시 빌더도 동기화해야 합니다.

| 메뉴 | 씬 |
|---|---|
| `Tools > Build MainMenu Scene` | MainMenu |
| `Tools > Build CharacterSelect Scene` | CharacterSelect |
| `Tools > Build GameExplore Scene` | GameExploreScene |
| `Tools > Build DiceBattle Scene` | DiceBattleScene |
| `Tools > Build MahjongBattle Scene` | MahjongBattleScene |

스테이지를 추가/수정했으면 Explore·Battle 씬을 다시 빌드해 `StageSpriteBundle[]` 캐시를 갱신해야 합니다.

## 디버그 콘솔

코나미 커맨드(↑↑↓↓←→←→BA)로 활성화, `Esc` 로 닫기.

| 명령 | 효과 |
|---|---|
| `/setdice N N N N N` | 주사위 결과 강제 (1~6, 5개) — 주사위 전투 전용 |
| `/kill player` | 플레이어 즉사 |
| `/kill mob @a` | 전체 적 처치 |
| `/kill mob 0 1 2` | 인덱스 지정 적 처치 |
| `/stage` | 등록된 스테이지 목록 (`[0] forest (id=forest_1, 어둠의 숲)`) |
| `/stage <index\|name\|id>` | 해당 스테이지 1라운드로 이동 (`/stage 0`, `/stage forest`, `/stage forest_1` 모두 동작) |
| `/nextround` | 현재 라운드 강제 클리어 (파워업 라운드 불가) |
| `/help` | 명령어 목록 |

## 테스트

- EditMode: `Window > General > Test Runner > EditMode`
- 마작 관련 단위 테스트: `Assets/Editor/Tests/Mahjong/` (패산·분해·야쿠·데미지·적 풀)

## 기술 요구사항

- Unity 6.0+ (`6000.3.11f1`), .NET Standard 2.1
- Universal Render Pipeline (URP)
- TextMesh Pro (`Assets/Folder/Mona12.otf` 계열 폰트 에셋)
- Dice 에셋 (`Assets/Dices/Prefabs/Dice_d6.prefab`)
- Input System (`InputSystemUIInputModule`)
- Windows Standalone x64
