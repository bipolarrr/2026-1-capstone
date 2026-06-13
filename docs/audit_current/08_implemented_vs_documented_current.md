# Implemented Vs Documented Current

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Delta commits covered: `e1337d16`, `d007d473`, `311ae2e6`

## Latest Master Matrix

This table is the current classification after the post-audit master delta. The original 2026-06-11 matrix is preserved below as historical baseline.

| Item | Latest classification | Evidence path | Confidence | Notes |
|---|---|---|---|---|
| Dice battle loop | Implemented and validated | `Assets/Scripts/Battle/BattleSceneController.cs`, tracked scenes/build settings, runtime full EditMode 132/132 | High | Manual visual QA still unknown. |
| Mahjong battle loop | Implemented and validated | `Assets/Scripts/Mahjong/**`, `Assets/Mahjong/MahjongTileSprites.asset`, `d007d473`, Mahjong focused 43/43 and full 131/131 | High | `tile_back_acorn` is now the intended hidden tile back. Manual visual QA still unknown. |
| Hold'em | Implemented and validated | `Assets/Scripts/Holdem/**`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Holdem/**`, `Assets/Scenes/HoldemBattleScene.unity`, `GameExploreController.ResolveBattleSceneName`, Hold'em XML/build log | High | Promoted in `e1337d16`; route returns `HoldemBattleScene`; builder regeneration still has scene/settings churn risk. |
| MainMenu | Implemented and validated | `Assets/Scripts/MainMenu/MainMenuController.cs`, `MainMenuSceneBuilder`, tracked `Assets/Scenes/MainMenu.unity`, latest player build success | High | Manual visual QA unknown. |
| CharacterSelect | Implemented and validated | `CharacterSelectController`, `CharacterSelectSceneBuilder`, tracked scene, latest build settings | High | Hold'em selection now routes to Hold'em battle through explore. |
| GameExplore | Implemented and validated | `GameExploreController`, `StageRegistry`, `StageData`, tracked `Assets/Scenes/GameExploreScene.unity`, latest player build success | High | Foreground map presentation still needs manual QA. |
| normal combat | Implemented and validated | `StageData.StageRoundType.NormalCombat`, `GameExploreController.SetupCombatEncounter`, runtime full EditMode 132/132 | High | Visual QA still unknown. |
| item box / power-ups | Implemented | `PowerUpType`, `PowerUpRewardCatalog`, `GameSessionManager.PowerUps`, `GameExploreController` | High | Not a shop/relic/joker system. |
| boss combat | Implemented | `StageRoundType.BossCombat`, stage data, battle controllers | High | Runtime asset reference risk is reduced, but boss visual QA remains manual. |
| defeat/victory/restart | Implemented and validated | Dice/Mahjong/Holdem battle controllers, tracked scenes, latest player build success | High | Manual playthrough not newly performed. |
| linear stage progression | Implemented | `StageRegistry`, `StageData.Rounds`, `GameExploreController.CurrentEventIndex` handling | High | Current source still supports stage ordering. |
| node map | Partial | Foreground source still contains broader map UI work; `311ae2e6` tracks map UI PNGs used by builders | Medium | Latest build validates asset availability, not full manual node-map UX. |
| shop/relic/joker | Not found | Source scan evidence from initial audit remains applicable; no delta commit adds these systems | High | Keep out of implemented feature claims. |
| Balatro chips/mult | Not found | Source scan evidence from initial audit remains applicable | High | Dice, Mahjong, and Hold'em use their own battle scoring/damage helpers. |
| scene builder | Implemented with validation hygiene risk | `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility.SetField`, `UnityEventTools.AddPersistentListener`, Hold'em builder validation doc | High | Builders exist and player build passes, but Hold'em builder regeneration dirties scene/settings. |
| tracked/generated scenes | Implemented with validation hygiene risk | `git ls-files Assets/Scenes` shows six tracked scenes/metas; latest build settings list those scenes | High | Scene files are tracked now, but generated scene churn remains open. |
| TextMeshPro / font runtime risk | Partial | `SceneBuilderUtility`, tracked TMP font assets | Medium | No new manual font visual QA. |
| audio | Implemented | `AudioManager`, `SceneBuilderUtility`, `Assets/Se/**` | High | Playback/mix QA unknown. |
| debug console | Implemented | `DebugConsoleController`, `DebugCommandProcessor`, `IBattleDebugTarget`, battle controller implementations | High | Manual input QA unknown. |
| sprite runtime paths | Partially mitigated | `311ae2e6`, `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks`, runtime XML 132/132 | High | Missing/untracked/count-mismatch risk for promoted references is reduced; visual/provenance quality remains manual QA. |
| Runtime asset source-of-truth | Implemented and validated | `311ae2e6`, `.gitignore` D6 mine exceptions, tracked runtime asset groups, runtime XML/build log | High | Classified as pass-with-validation-hygiene-risk because Unity dirtied settings in validation worktree. |
| Grok source/pipeline paths | Current-provenance / pipeline evidence | Grok docs, `tools/sprite_pipeline/**`, `SpritePipelineWork/**` | Medium | Provenance only, not proof of runtime approval. |
| sprite upscale candidates | Candidate / not runtime-current by default | `SpritePipelineWork/**/upscaled_runtime_candidate*`, pipeline scripts | High | Candidates still require human review and explicit promotion. |
| player build status | Implemented with validation hygiene risk | Runtime asset build log, Hold'em build log, produced `.exe` files | High | Latest builds pass; initial invalid-scene-path failure is historical baseline. |
| EditMode test status | Implemented and validated | Hold'em 130/130, Mahjong 43/43 and 131/131, runtime assets 132/132 raw XML | High | Does not cover unrelated remaining dirty foreground files. |

## Latest Documentation Replacements

| Topic | Replace old claim with |
|---|---|
| Holdem status | Hold'em is now promoted and validated in latest `master`; builder regeneration churn remains open. |
| Scene status | Six runtime scenes and their `.meta` files are tracked in latest `master`; player build passes, but generated scene/settings churn remains open. |
| Validation status | Initial 82/82 EditMode and player build failure are historical baseline. Latest evidence: Hold'em 130/130, Mahjong 43/43 and 131/131, runtime assets 132/132, player build success with hygiene risk. |
| Asset status | Use `05_runtime_asset_reference_manifest.md` delta and `13_runtime_asset_source_of_truth_validation_result.md`; promoted runtime asset references are covered by the new asset reference test. |
| Pipeline status | Pipeline remains review/candidate-first; no asset generation or promotion was run during this docs closure. |

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: runtime/editor code, direct asset references, build settings, Unity validation, and old markdown only as drift source.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Initial Audit Baseline Matrix

This table is retained for historical context at `e6de7c9`. Use the Latest Master Matrix above for current planning.

| Item | Current classification | Evidence path | Confidence | Notes |
|---|---|---|---|---|
| Dice battle loop | Implemented | `Assets/Scripts/Battle/BattleSceneController.cs`, dice helper files, EditMode tests 82/82 passing | High | Manual visual QA unknown; player build blocked. |
| Mahjong battle loop | Implemented | `Assets/Scripts/Mahjong/MahjongBattleController.cs`, Mahjong rule files, `Assets/Mahjong/MahjongTileSprites.asset`, EditMode tests | High | Red-five art is foreground untracked; visual QA unknown. |
| Hold'em | Not found as current implemented route / foreground-only prototype | `GameExploreController.ResolveBattleSceneName` routes Holdem to `DiceBattleScene`; `Assets/Scripts/Holdem/**` untracked | High for route | Do not document as implemented until tracked route/assets/scenes are intentionally promoted and validated. |
| MainMenu | Implemented | `Assets/Scripts/MainMenu/MainMenuController.cs`, `MainMenuSceneBuilder` | High | Scene file itself is foreground ignored, not tracked in HEAD. |
| CharacterSelect | Implemented | `CharacterSelectController`, `CharacterSelectSceneBuilder`, weapon callback methods | High | Holdem selection currently starts game but battle route falls back to Dice. |
| GameExplore | Implemented | `GameExploreController`, `StageRegistry`, `StageData` | High | Foreground map UI appears partial/unvalidated. |
| normal combat | Implemented | `StageData.StageRoundType.NormalCombat`, `GameExploreController.SetupCombatEncounter` | High | Stage mob asset gaps remain. |
| item box / power-ups | Implemented | `PowerUpType`, `PowerUpRewardCatalog`, `GameSessionManager.PowerUps`, `GameExploreController` | High | This is not a shop/relic/joker system. |
| boss combat | Implemented | `StageRoundType.BossCombat`, `Stage1Forest` boss data, `GameExploreController`, battle controllers | High | Stage2 boss sprite path is null/fallback. |
| defeat/victory/restart | Implemented | `BattleSceneController`, `MahjongBattleController`, `GameSessionManager.LastBattleResult` | High | Full executable validation blocked. |
| linear stage progression | Implemented | `StageRegistry`, `StageData.Rounds`, `GameExploreController.CurrentEventIndex` handling | High | Current source still supports stage ordering. |
| node map | Partial / requires manual QA | Foreground `GameExploreController` map-node code, untracked map UI assets | Medium | Not validated in clean worktree and not the same as a full node-map game system. |
| shop/relic/joker | Not found | Source scan found no implemented relic/joker/shop economy classes; only item-box power-ups | High | Existing scope docs should not be read as implementation. |
| Balatro chips/mult | Not found | Source scan found no chips/mult shared scoring runtime | High | Dice and Mahjong use their own damage/scoring helpers. |
| scene builder | Implemented but high drift risk | `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility.SetField`, `UnityEventTools.AddPersistentListener` | High | Builders exist; generated scenes are not tracked in clean HEAD. |
| tracked/generated scenes | Blocked / drift | HEAD has no tracked `Assets/Scenes/*.unity`; foreground scenes are untracked/ignored | High | Build settings reference scene paths that are missing in clean commit. |
| TextMeshPro / font runtime risk | Partial risk | `SceneBuilderUtility` references `Assets/TextMesh Pro/Fonts/Mona12.asset`; `Assets/TextMesh Pro/Fonts/decimal_font_numbers.md` exists | Medium | Font asset tracked, but TMP runtime visual setup not manually QA'd. |
| audio | Implemented basic layer | `AudioManager`, `SceneBuilderUtility`, `Assets/Se/**` | High | Playback/mix QA unknown. |
| debug console | Implemented | `DebugConsoleController`, `DebugCommandProcessor`, `IBattleDebugTarget`, battle controller implementations | High | Manual input QA unknown. |
| sprite runtime paths | Partial / risk | `Stage1Forest`, `Stage2Cave`, `SceneBuilderUtility`, asset manifest | High | Several direct references are missing, untracked, ignored, or dirty. |
| Grok source/pipeline paths | Current-provenance / pipeline evidence | Grok docs, `tools/sprite_pipeline/**`, `SpritePipelineWork/**` | Medium | Provenance only, not proof of runtime approval. |
| sprite upscale candidates | Candidate / not runtime-current by default | `SpritePipelineWork/**/upscaled_runtime_candidate*`, pipeline scripts | High | Candidates require human review and promotion. |
| player build status | Blocked | Unity player build log | High | Exit code 1, invalid empty scene path. |
| EditMode test status | Implemented validation pass | Unity test XML | High | 82/82 passed on clean validation worktree. |

## Initial Baseline Documentation Replacements

These replacements were correct for the initial baseline. Use the Latest Documentation Replacements above for current planning.

| Topic | Replace old claim with |
|---|---|
| Holdem status | Holdem is not current validated runtime; tracked route falls back to Dice. |
| Scene status | Scenes are generated/foreground artifacts and not tracked in clean HEAD; player build is blocked. |
| Validation status | Current EditMode is 82/82 passing; current player build fails on invalid scene path. |
| Asset status | Use `05_runtime_asset_reference_manifest.md`, not old asset docs. |
| Pipeline status | Pipeline is review/candidate-first; promotion is separate approval work. |
