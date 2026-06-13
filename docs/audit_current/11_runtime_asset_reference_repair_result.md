# Runtime Asset Reference Repair Result

- Date: 2026-06-12 KST
- Baseline commit: `84d87febc3767456dff0e66b7f18e5ef645d5d58`
- Validation worktree: `C:\Users\song\arv_20260612_001`
- Validation output root: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime-asset-reference-repair_20260612_001`
- Scope note: foreground checkout already had broad unrelated dirty changes. This repair did not revert them. Unity validation used a separate worktree with foreground `Assets/` copied in, so the validation worktree was intentionally dirty.

## Foreground Changes Made In This Pass

- `.gitignore`
  - Kept the imported `Assets/Dices` package ignored by default.
  - Opened exact Git-tracking exceptions for the current custom D6 builder chain:
    - `Assets/Dices.meta`
    - `Assets/Dices/D6_mine.png(.meta)`
    - `Assets/Dices/Dice_Tray.png(.meta)`
    - `Assets/Dices/Generated.meta`
    - `Assets/Dices/Generated/D6Mine.mat(.meta)`
    - `Assets/Dices/Generated/D6MineAtlas.png(.meta)`
    - `Assets/Dices/Generated/D6MineMesh.asset(.meta)`
    - `Assets/Dices/Prefabs.meta`
    - `Assets/Dices/Prefabs/Dice_d6_mine.prefab(.meta)`
  - `Assets/Dices/Prefabs/Dice_d6.prefab` remains ignored because it depends on the broader imported dice package.
- `Assets/Scripts/Stages/Stage1Forest.cs`
  - Changed Goblin `hitSpriteFrameCount` from `69` to `29`, matching the direct PNG count in `Assets/Mobs/Sprites/Goblin/Hit`.
  - Removed Skeleton `attackSpriteFolderPath` and `hitSpriteFolderPath` references so runtime falls back intentionally instead of pointing at empty folders.
- `Assets/Scripts/Stages/Stage2Cave.cs`
  - Changed Goblin `hitSpriteFrameCount` from `69` to `29`.
- `Assets/Editor/Tests/Battle/SceneBuilderUtilityTests.cs`
  - Added `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks`.
  - The test treats empty paths as intentional fallback and requires non-empty file/folder paths to exist with enough direct PNG frames.

## Runtime Asset Tracking Candidates

The following existing foreground assets are runtime or builder referenced and should be staged in a later narrow commit, excluding source videos and pipeline intermediates:

- Dice builder inputs:
  - `Assets/Dices/D6_mine.png(.meta)`
  - `Assets/Dices/Dice_Tray.png(.meta)`
  - `Assets/Dices/Generated/D6Mine.mat(.meta)`
  - `Assets/Dices/Generated/D6MineAtlas.png(.meta)`
  - `Assets/Dices/Generated/D6MineMesh.asset(.meta)`
  - `Assets/Dices/Prefabs/Dice_d6_mine.prefab(.meta)`
- Stage sprite assets:
  - `Assets/Mobs/Sprites/Slime/Attack/*.png(.meta)`
  - `Assets/Mobs/Sprites/Slime/Hit/*.png(.meta)`
  - `Assets/Mobs/Sprites/Skeleton/Dead/*.png(.meta)`
  - `Assets/Mobs/Sprites/Golem/InGame/{Idle,Attack,Hit,Dead}/*.png(.meta)`
  - `Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png(.meta)`
- Explore map builder assets:
  - `Assets/UI/UI_Map.png(.meta)`
  - `Assets/UI/UI_MapIcon.png(.meta)`
  - `Assets/UI/MapIcons/*.png(.meta)`
- Mahjong tile assets:
  - `Assets/Mahjong/m_5_red.png(.meta)`
  - `Assets/Mahjong/p_5_red.png(.meta)`
  - `Assets/Mahjong/s_5_red.png(.meta)`
  - `Assets/Mahjong/tile_back_acorn.png(.meta)`
  - `Assets/Mahjong/z_{east,south,west,north,white,green,red}.png(.meta)`

No image processing, sprite promotion, asset generation, or manual `.meta` edits were performed.

## Validation

### EditMode

- Command: Unity batchmode EditMode tests in validation worktree, no `-quit`.
- Exit code: `2`
- Result XML: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime-asset-reference-repair_20260612_001\editmode\full-editmode-results.xml`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime-asset-reference-repair_20260612_001\editmode\full-editmode.log`
- Results: total `370`, passed `368`, failed `2`, skipped `0`
- Compile errors: none observed in log.
- Failures:
  - `HoldemTests.HoldemRoutingTests.HoldemRoutesToHoldemBattleScene`
    - Expected `HoldemBattleScene`, actual `DiceBattleScene`.
    - This is outside this repair scope because Hold'em routing/promotion was not changed.
  - `MahjongTests.MahjongBattleControllerTests.EnemyWaitTilesDisplay_RevealWaitTile_KeepsHiddenSlotsBackedAndUsesFaceSpriteForRevealedSlot`
    - Expected generated hidden back sprite, actual `tile_back_acorn`.
    - This reflects current Mahjong tile asset/back-sprite behavior and needs a follow-up test/design alignment.

### Windows Standalone x64 Player Build

- Command: Unity batchmode `-buildTarget StandaloneWindows64 -buildWindows64Player`.
- Exit code: `0`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime-asset-reference-repair_20260612_001\player-build\standalone-build.log`
- Executable: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime-asset-reference-repair_20260612_001\player-build\CapstoneRuntimeAssetRepair.exe`
- Result: `Build Finished, Result: Success.`
- Scene path error: no `'' is an incorrect path for a scene file` occurrence found.

## Validation Worktree Dirty State

- Post-run `git status --porcelain --untracked-files=all` count: `8850`
- Reason: validation intentionally copied foreground `Assets/` into a clean worktree to validate the current compile/build surface with existing dirty code and runtime assets.
- No validation worktree changes were copied back to foreground.

## Hold'em Handling

- Hold'em was not added to build settings.
- Hold'em files were not deleted or demoted.
- The failing Hold'em routing test is recorded as a product-feature promotion task, not as part of this asset reference repair.

## Remaining Risks

- Foreground remains broadly dirty with many changes outside this repair. A clean, reviewable commit requires explicit staging of only the intended hunks and runtime assets.
- `Dice_d6.prefab` fallback is still ignored. The primary `Dice_d6_mine.prefab` chain is the tracked candidate; fallback package tracking remains a separate decision.
- Source videos and pipeline work products are present in foreground folders but should not be staged as runtime source-of-truth assets.
- Mahjong hidden tile behavior and tests disagree after `tile_back_acorn` becomes the active back texture.
- Hold'em routing is still not promoted into tracked runtime/build source of truth.

## Next Work Plan

1. Runtime asset source-of-truth commit
   - Stage only the runtime PNG/prefab/material/mesh assets listed above and the narrow code/test hunks from this repair.
   - Exclude mp4/webp/source/pipeline/intermediate files.
   - Validate with a clean candidate worktree, not a full foreground `Assets` copy.
2. Mahjong back-tile alignment
   - Decide whether `tile_back_acorn` is the intended hidden tile back.
   - If yes, update the failing test expectation; if no, revert the serialized database to generated back-sprite behavior.
3. Hold'em feature promotion
   - Promote Hold'em only in a separate task covering scripts, builder, assets, scene, routing, tests, and build settings together.
