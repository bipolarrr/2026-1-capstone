# Unity Validation Current

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`

## Latest Validation Delta After Master Commits

This section supersedes the initial player-build status for latest `master`. The original 2026-06-11 validation remains below as historical baseline.

Evidence sources:

- `docs/audit_current/12_holdem_promotion_validation_result.md`
- `docs/audit_current/13_runtime_asset_source_of_truth_validation_result.md`
- Runtime output root verified locally: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046`
- Runtime raw artifacts verified locally:
  - `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\editmode\full-editmode-results.xml`
  - `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\standalone-build.log`
  - `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\CapstoneRuntimeAssets.exe`
- Hold'em raw artifacts verified locally:
  - `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\editmode\full-editmode-results.xml`
  - `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\standalone-build.log`
  - `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\CapstoneHoldemPromotion.exe`
- Mahjong raw artifacts verified locally:
  - `C:\Users\song\Desktop\Capstone_validation_outputs\mahjong_back_20260612_040025\editmode-mahjong\mahjong-editmode-results.xml`
  - `C:\Users\song\Desktop\Capstone_validation_outputs\mahjong_back_20260612_040025\editmode-full\full-editmode-results.xml`
- No Unity rerun was performed during this closure because the existing docs and raw output artifacts were available.

| Validation target | Status | Total | Passed | Failed | Build result | Evidence |
|---|---|---:|---:|---:|---|---|
| Hold'em EditMode | Passed | 130 | 130 | 0 | n/a | Raw XML; `HoldemRoutesToHoldemBattleScene` result `Passed`; `12_holdem_promotion_validation_result.md` |
| Hold'em player build | Passed | n/a | n/a | n/a | exit 0, executable produced | Raw build log contains `Build Finished, Result: Success.` and loaded `Assets/Scenes/HoldemBattleScene.unity`; `CapstoneHoldemPromotion.exe` exists |
| Mahjong focused EditMode | Passed | 43 | 43 | 0 | n/a | Raw focused Mahjong XML under `mahjong_back_20260612_040025` |
| Mahjong full EditMode | Passed | 131 | 131 | 0 | n/a | Raw full XML under `mahjong_back_20260612_040025`; database back-sprite test passed |
| Runtime assets full EditMode | Passed | 132 | 132 | 0 | n/a | Raw runtime XML; `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks` result `Passed`; `13_runtime_asset_source_of_truth_validation_result.md` |
| Runtime assets Windows Standalone x64 build | Passed with validation hygiene risk | n/a | n/a | n/a | exit 0, executable produced | Raw runtime build log contains `Build Finished, Result: Success.`; `CapstoneRuntimeAssets.exe` exists |

Latest interpretation:

- The initial audit baseline player build failure (`'' is an incorrect path for a scene file`) is historical for `e6de7c9`.
- Latest runtime asset validation on `311ae2e6` passed full EditMode 132/132 and produced a Windows Standalone x64 executable.
- Latest runtime asset validation is classified as `pass-with-validation-hygiene-risk` because Unity dirtied tracked URP/project settings in the disposable validation worktree.
- Validation-worktree settings churn is not an intended foreground edit and was not copied back.

Coverage limits:

- Latest validation evidence covers the committed Hold'em promotion, Mahjong back tile alignment, and runtime asset source-of-truth commit.
- It does not prove every remaining dirty/untracked foreground file compiles or behaves.
- Manual visual QA and interactive playthrough QA were not newly performed in this closure.
- Hold'em builder validation still has scene/settings churn risk even though clean EditMode/player build passed.

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: Unity batchmode commands, result XML, logs, validation worktree git status.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Validation Isolation

- Foreground checkout: `C:\Users\song\Desktop\Capstone`
- Validation worktree: `C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534`
- Validation worktree mode: detached HEAD
- Validation output root: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534`
- Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe`
- Unity project version: `6000.3.11f1`
- Build target used: `StandaloneWindows64`

No Unity batchmode command was run against the foreground checkout.

## EditMode Tests

Command:

```text
Unity.exe -batchmode -projectPath C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534 -runTests -testPlatform EditMode -testResults C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode-results.xml -buildTarget StandaloneWindows64 -logFile C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode.log
```

Result:

| Field | Value |
|---|---|
| Exit code | 0 |
| Status | Passed |
| Total | 82 |
| Passed | 82 |
| Failed | 0 |
| Skipped | 0 |
| Inconclusive | 0 |
| Compile errors | none observed |
| Timeout | no |
| Result XML | `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode-results.xml` |
| Log | `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode.log` |

Failure test names: none.

Notable log behavior:

- Unity license connection retried before the run completed.
- Test run completed with code 0.

Post-run validation worktree status:

```text
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/ShaderGraphSettings.asset
?? Capstone_audit_validation_20260611_204534.slnx
```

Interpretation:

- Functional test result is passing.
- Hygiene risk exists because Unity changed tracked project setting files and created an untracked solution file inside the validation worktree.

## Windows Standalone x64 Player Build

Command:

```text
Unity.exe -batchmode -nographics -quit -projectPath C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534 -buildTarget StandaloneWindows64 -buildWindows64Player C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\CapstoneAudit.exe -logFile C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\standalone-build.log
```

Result:

| Field | Value |
|---|---|
| Exit code | 1 |
| Status | Failed |
| Produced executable | no |
| Expected executable | `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\CapstoneAudit.exe` |
| Compile errors | none observed in captured failure summary |
| Failure message | `'' is an incorrect path for a scene file. BuildPlayer expects paths relative to the project folder.` |
| Log | `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\standalone-build.log` |

Interpretation:

- The build did not fail on source compilation in the observed log.
- The immediate blocker is scene/build settings hygiene: BuildPlayer received an invalid empty scene path.

## Final Validation Worktree Dirtiness

Final `git status --porcelain` in validation worktree:

```text
 M Assets/Settings/UniversalRP.asset
 M Assets/UniversalRenderPipelineGlobalSettings.asset
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/GraphicsSettings.asset
 M ProjectSettings/ProjectSettings.asset
 M ProjectSettings/ShaderGraphSettings.asset
?? Assets/Resources.meta
?? Assets/Resources/PerformanceTestRunInfo.json
?? Assets/Resources/PerformanceTestRunInfo.json.meta
?? Assets/Resources/PerformanceTestRunSettings.json
?? Assets/Resources/PerformanceTestRunSettings.json.meta
?? Capstone_audit_validation_20260611_204534.slnx
```

Tracked diff summary:

- `Assets/Settings/UniversalRP.asset`
- `Assets/UniversalRenderPipelineGlobalSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- Line-ending/status-only changes also appeared for other settings files.

Classification: validation hygiene risk. These files were not copied to the foreground checkout and were not treated as intended edits.

## Validation Coverage Limits

- Validation ran on clean commit `e6de7c9`, not the dirty foreground checkout.
- Foreground untracked Holdem files, scene files, pipeline scripts, and asset additions were not part of clean validation unless they were already tracked at HEAD.
- The player build failure blocks executable-level QA.
- Visual/runtime playthrough QA was not performed.

## Reproduction Notes

To reproduce safely, use a separate validation worktree and output root outside the project. Do not run Unity batchmode against the foreground checkout.
