# Mahjong Tile Asset Fix Result

Date: 2026-06-11

## 1. 작업 요약

- 마작패 정렬 기준을 `TileOrdering`으로 분리하고 `Tile.CompareTo()`와 `Hand` 정렬 호출이 같은 identity 기반 sort key를 쓰게 했다.
- 자패 7종 PNG를 생성했다: `z_east.png`, `z_south.png`, `z_west.png`, `z_north.png`, `z_white.png`, `z_green.png`, `z_red.png`.
- 적오패 3종 PNG를 보정했다: `m_5_red.png`, `p_5_red.png`, `s_5_red.png`.
- 뒷면 PNG `tile_back_acorn.png`를 단색 녹색 계열 배경 + 중앙 갈색 도토리 1개로 교체했다.
- `MahjongTileSpriteDatabase`에 wind/dragon/back texture lookup을 추가하고 `MahjongTileSprites.asset`에 연결했다.
- 적 숨김 대기패가 DB back sprite를 우선 사용하게 했다.

## 2. 정렬 버그 원인

검색 결과 현재 코드에는 과거 `456 -> 654` 보정용 `Reverse`, `swap`, `reorder` 로직은 남아 있지 않았다.

실제 정렬 지점은 `Hand.DealInitial()`, `Hand.Discard()`, `Hand.ConcealedFourteen()`의 `List<Tile>.Sort()`였고, 기준은 `Tile.CompareTo()`였다. 기존 `CompareTo()`는 suit/value만 비교해 일반 5와 red five가 같은 비교값이 되는 문제가 있었다.

수정:

- `Assets/Scripts/Mahjong/Tile.cs`: `TileOrdering.SortKey()` 추가.
- `Assets/Scripts/Mahjong/Tile.cs`: `Tile.CompareTo()`가 `TileOrdering.Compare()`를 사용.
- `Assets/Scripts/Mahjong/Hand.cs`: 직접 `List.Sort()` 대신 `TileOrdering.Sort()` 사용.

정렬 순서:

- `m1..m5,m5_red,m6..m9`
- `p1..p5,p5_red,p6..p9`
- `s1..s5,s5_red,s6..s9`
- `east,south,west,north`
- `white,green,red`

## 3. 생성한 자패 에셋

모두 `Assets/Mahjong/` 아래에 생성했다. 캔버스는 `w.png`와 같은 `447x619 RGBA`를 사용했다.

- `z_east.png` / GUID `067cb14a0be1c7f45b82788690f98ad0`
- `z_south.png` / GUID `47d3e10d37590f04084eb1763b74c436`
- `z_west.png` / GUID `34d7be1db130fb84ca3c19bd73425ea4`
- `z_north.png` / GUID `8fd8a10089eba6a4bb2b30ce45b271de`
- `z_white.png` / GUID `7eba9f2fd6744d649bd6b80c6b2b6ec2`
- `z_green.png` / GUID `2cbab9c53e8334d4f85198fb22a39f22`
- `z_red.png` / GUID `15f91f26f5df56d43835f96cb43c89f3`

`.meta`는 foreground에서 수동 작성하지 않고, detached validation worktree에서 Unity 6 import로 자동 생성한 뒤 복사했다.

## 4. 적오패 에셋

수정 대상:

- `Assets/Mahjong/m_5_red.png`
- `Assets/Mahjong/p_5_red.png`
- `Assets/Mahjong/s_5_red.png`

변경:

- 기존 원형 빨간 점을 작은 빨간 도토리로 교체.
- 3종 모두 같은 red palette 사용.
- `p_5_red` 중앙 문양과 marker가 서로 다른 빨강 계열로 보이던 문제를 보정.

대표 red palette:

- `#65070A`
- `#8C0B0E`
- `#B71418`
- `#D8272D`
- `#F26A6D`

`p_5_red` 보정 전 주요 빨강:

- `#72080B`, `#75080C`, `#78080C`, `#7A090C`
- 사전 스캔에서 밝은 계열 `#FF6B70`도 확인됨

`p_5_red` 보정 후 주요 빨강:

- `#65070A`, `#8C0B0E`, `#B71418`, `#D8272D`, `#F26A6D`

## 5. 뒷면 에셋

수정 대상:

- `Assets/Mahjong/tile_back_acorn.png`

변경:

- 빨간 도토리 점 4개 제거.
- 금색 다이아몬드 장식 제거.
- 단색 녹색 계열 배경 + 중앙 갈색 도토리 1개로 교체.
- `MahjongTileSprites.asset`의 `backTexture`에 기존 `tile_back_acorn.png.meta` GUID `6afec82fa99abbb4a8e7f1c5a5559c8d`로 연결.
- `EnemyWaitTilesDisplay.Init()`이 DB back sprite를 우선 사용하고 없을 때만 기존 코드 생성 fallback을 사용.

## 6. DB 연결

`Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs`:

- `windTextures[4]`, `dragonTextures[3]`, `backTexture` 추가.
- `GetWind()`, `GetDragon()`, `GetBackSprite()` 추가.
- `GetSprite(Tile)`이 `Suit.Wind`, `Suit.Dragon`을 직접 반환.
- editor fallback path 유지:
  - `Assets/Mahjong/z_east.png`
  - `Assets/Mahjong/z_south.png`
  - `Assets/Mahjong/z_west.png`
  - `Assets/Mahjong/z_north.png`
  - `Assets/Mahjong/z_white.png`
  - `Assets/Mahjong/z_green.png`
  - `Assets/Mahjong/z_red.png`
  - `Assets/Mahjong/tile_back_acorn.png`

`Assets/Mahjong/MahjongTileSprites.asset`:

- wind/dragon/back/red texture refs 연결.
- red five 3종 기존 GUID 유지:
  - `m_5_red`: `8ca7f00fd6b46984a951711ebc9b1e4b`
  - `p_5_red`: `82dd6e8fb4fa47f4886786b320f23a3f`
  - `s_5_red`: `b8cd15058a020b447b56d8cd9bf06773`

## 7. 백업과 rollback

최종 overwrite 전 백업:

- `SpritePipelineWork/mahjong_tile_asset_fix/backup_20260611_215026/`

백업 포함:

- `m_5_red.png`
- `p_5_red.png`
- `s_5_red.png`
- `tile_back_acorn.png`
- 각 대상의 기존 `.meta`

Rollback:

- 위 백업 폴더의 PNG를 `Assets/Mahjong/`에 다시 복사하면 된다.
- red/back `.meta`는 수정하지 않았으므로 보통 PNG만 복원하면 된다.
- 신규 honor PNG/meta는 이번 작업 산출물이므로 되돌릴 때는 해당 파일들을 제외하면 된다.

## 8. 비교/contact sheet

최종 리뷰 폴더:

- `SpritePipelineWork/mahjong_tile_asset_fix/review_20260611_215026/`

주요 산출물:

- `back_before_after.png`
- `m_5_red_before_after.png`
- `p_5_red_before_after.png`
- `s_5_red_before_after.png`
- `red_five_before_after_sheet.png`
- `honor_tiles_contact_sheet.png`
- `all_mahjong_tiles_contact_sheet.png`
- `mahjong_tile_asset_fix_manifest.json`

## 9. 검증 결과

정적 검증:

- `git status --short` 확인 완료.
- `git diff --check` 실행 완료. 오류 없음. CRLF 경고만 있음.
- `rg`로 `Reverse`, `swap`, `reorder`류 보정 검색 완료. 마작 런타임에는 남은 보정 코드 없음.
- PNG 크기와 `.meta` 존재 확인 완료.
- `MahjongTileSprites.asset`의 wind/dragon/back/red GUID 연결 확인 완료.

테스트:

- 정렬 테스트 추가:
  - `m6,m5,m4 -> m4,m5,m6`
  - `m4,m5,m6` 유지
  - `p4,p5,p5_red,p6`
  - `east,south,west,north,white,green,red`
- DB/back 테스트 추가:
  - honor/back texture lookup
  - 적 숨김 대기패가 DB back sprite 우선 사용
- EditMode 테스트 실행은 하지 않았다. 현재 foreground worktree가 작업 전부터 광범위하게 dirty 상태이고, 별도 validation worktree에 이번 좁은 변경만 복사해도 현재 foreground compile 상태를 대표하지 못한다. Unity foreground validation은 규칙상 금지이므로 정적 검증과 import-only validation으로 대체했다.

Unity import validation:

- Validation worktree: `C:\Users\song\desktop\Capstone_mahjong_asset_import_20260611_215026`
- 상태 기록 후 삭제 완료.
- 로그:
  - `C:\Users\song\desktop\Capstone_validation_outputs\mahjong_asset_import_20260611_215026\unity_import.log`
  - `C:\Users\song\desktop\Capstone_validation_outputs\mahjong_asset_import_20260611_215026\unity_import_execute_method.log`
- validation worktree `git status --porcelain` 결과: 새 honor PNG/meta와 임시 importer 파일만 untracked. tracked file 변경 없음.
- Build output: 없음. 테스트/빌드 validation은 실행하지 않음.

## 10. 변경 파일 목록

수정:

- `Assets/Scripts/Mahjong/Tile.cs`
- `Assets/Scripts/Mahjong/Hand.cs`
- `Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs`
- `Assets/Scripts/Mahjong/EnemyWaitTilesDisplay.cs`
- `Assets/Editor/Tests/Mahjong/HandTests.cs`
- `Assets/Editor/Tests/Mahjong/MahjongBattleControllerTests.cs`
- `Assets/Mahjong/MahjongTileSprites.asset`
- `Assets/Mahjong/m_5_red.png`
- `Assets/Mahjong/p_5_red.png`
- `Assets/Mahjong/s_5_red.png`
- `Assets/Mahjong/tile_back_acorn.png`

생성:

- `Assets/Mahjong/z_east.png`
- `Assets/Mahjong/z_east.png.meta`
- `Assets/Mahjong/z_south.png`
- `Assets/Mahjong/z_south.png.meta`
- `Assets/Mahjong/z_west.png`
- `Assets/Mahjong/z_west.png.meta`
- `Assets/Mahjong/z_north.png`
- `Assets/Mahjong/z_north.png.meta`
- `Assets/Mahjong/z_white.png`
- `Assets/Mahjong/z_white.png.meta`
- `Assets/Mahjong/z_green.png`
- `Assets/Mahjong/z_green.png.meta`
- `Assets/Mahjong/z_red.png`
- `Assets/Mahjong/z_red.png.meta`
- `SpritePipelineWork/mahjong_tile_asset_fix/generate_mahjong_tile_assets.py`
- `SpritePipelineWork/mahjong_tile_asset_fix/backup_20260611_215026/`
- `SpritePipelineWork/mahjong_tile_asset_fix/review_20260611_215026/`
- `docs/mahjong_tile_asset_fix_result.md`

기존 미추적 상태에서 overwrite:

- `Assets/Mahjong/m_5_red.png`
- `Assets/Mahjong/m_5_red.png.meta`
- `Assets/Mahjong/p_5_red.png`
- `Assets/Mahjong/p_5_red.png.meta`
- `Assets/Mahjong/s_5_red.png`
- `Assets/Mahjong/s_5_red.png.meta`
- `Assets/Mahjong/tile_back_acorn.png`
- `Assets/Mahjong/tile_back_acorn.png.meta`

## 11. 금지 영역 확인

- `.meta` 수동 편집: 없음.
- 새 honor `.meta`: Unity validation worktree에서 자동 생성 후 복사.
- `.unity` 직접 편집: 없음.
- `.prefab` 직접 편집: 없음.
- `ProjectSettings/` 직접 편집: 없음.
- `Packages/`, `Library/`, `Temp/`, `UserSettings/` 직접 편집: 없음.
- 삭제/이동/rename: 없음.

작업 전부터 `ProjectSettings/EditorBuildSettings.asset` 등 다른 dirty 파일이 있었지만 이번 작업에서는 수정하지 않았다.

## 12. 남은 리스크 / 사람이 확인할 항목

- Unity EditMode 테스트는 아직 실제 실행하지 않았다. `Assets/Editor/Tests/Mahjong/`의 마작 관련 테스트를 별도 validation worktree에서 실행해야 한다.
- Unity Editor에서 `MahjongTileSprites.asset` Inspector가 새 배열 필드를 정상 표시하고 refs를 유지하는지 확인해야 한다.
- MahjongBattleScene 재생성은 하지 않았다. 씬이 기존 builder/runtime 주입을 그대로 쓰므로 필요 시 `Tools > Build MahjongBattle Scene`으로 scene regeneration을 별도 판단해야 한다.
- 실제 화면에서 손패, 쯔모패, 버림패, 도라, 적 대기패, wait info panel에 honor/red/back sprite가 예상 크기로 보이는지 수동 확인이 필요하다.
