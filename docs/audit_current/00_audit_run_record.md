# Audit Run Record

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: repository status, tracked/untracked/ignored inventory, runtime/editor code scans, asset reference scans, Unity validation logs/results.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Environment

- OS: Microsoft Windows NT 10.0.26200.0
- Shell: PowerShell 7.5.5
- Current working directory: `C:\Users\song\Desktop\Capstone`
- Foreground checkout path: `C:\Users\song\Desktop\Capstone`
- Current branch: `master`
- Current commit hash: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Unity version from `ProjectSettings/ProjectVersion.txt`: `6000.3.11f1`, revision `3000ef702840`
- Unity executable path: `C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe`
- Validation worktree path: `C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534`
- Validation output root: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534`

## Foreground Git State

The audit did not stop on a dirty worktree. All findings are marked as either foreground observation or clean validation worktree observation where the distinction matters.

- `git status --porcelain` summary:
  - Modified tracked paths: 105
  - Untracked paths: 2893
- `git status --ignored --short` summary:
  - Modified tracked paths: 105
  - Untracked paths: 2893
  - Ignored paths: 59690

High-impact foreground dirty paths include:

- `.gitignore`
- `AGENTS.md`
- `REFACTOR_BACKLOG.md`
- `docs/assets.md`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/Mahjong/MahjongTileSprites.asset`
- Scene builders under `Assets/Editor/**`
- Runtime controllers under `Assets/Scripts/**`
- Tests under `Assets/Editor/Tests/**`
- Runtime art under `Assets/Mobs/**`, `Assets/Story/**`, `Assets/UI/**`

High-impact foreground untracked paths include:

- `Assets/Scenes/*.unity` and `Assets/Scenes/*.unity.meta`
- `Assets/Editor/HoldemBattleSceneBuilder.cs`
- `Assets/Scripts/Holdem/**`
- `Assets/Holdem/**`
- `tools/sprite_pipeline/*.py`
- `SpritePipelineWork/**` outputs beyond the tracked subset
- Grok and pipeline docs such as `docs/grok_generation_queue.md`, `docs/grok_manual_generation_packet.md`, and `docs/asset_acceptance_contract.md`

## Validation Worktree

Created detached from commit:

- Command intent: `git worktree add --detach C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534 e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Branch isolation: detached HEAD, not the foreground `master` checkout
- Dirty foreground changes were not copied into the validation worktree

This means Unity validation reflects the clean commit, not the dirty foreground working tree.

## Unity EditMode Validation

Command shape:

- `Unity.exe`
- `-batchmode`
- `-projectPath C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534`
- `-runTests`
- `-testPlatform EditMode`
- `-testResults C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode-results.xml`
- `-buildTarget StandaloneWindows64`
- `-logFile C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode.log`

Result:

- Exit code: 0
- Total: 82
- Passed: 82
- Failed: 0
- Skipped: 0
- Inconclusive: 0
- Timeout observed: no
- Compile errors observed: no
- Result XML: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode-results.xml`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\editmode\full-editmode.log`

Post-EditMode validation worktree status:

```text
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/ShaderGraphSettings.asset
?? Capstone_audit_validation_20260611_204534.slnx
```

## Unity Player Build Validation

Command shape:

- `Unity.exe`
- `-batchmode`
- `-nographics`
- `-quit`
- `-projectPath C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534`
- `-buildTarget StandaloneWindows64`
- `-buildWindows64Player C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\CapstoneAudit.exe`
- `-logFile C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\standalone-build.log`

Result:

- Exit code: 1
- Build status: failed
- Produced executable: no
- Expected executable path: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\CapstoneAudit.exe`
- Compile errors observed: no explicit compile error captured
- Failure message: `'' is an incorrect path for a scene file. BuildPlayer expects paths relative to the project folder.`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534\player-build\standalone-build.log`

Final post-validation worktree status:

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

Tracked validation diffs were treated as validation hygiene risk and were not copied into the foreground checkout.

## Output Preservation

- Logs, test XML, and player build output folder are outside the Unity project root under `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534`.
- The validation worktree was disposable. Its dirty status was recorded before cleanup.
- Cleanup result: `git worktree remove --force` removed the worktree registration but hit a Windows long-path deletion error; after confirming the target was not the foreground checkout and validation outputs were outside the target, the orphan directory was deleted. `target_exists_after_delete=False`, `output_root_exists=True`.

## Commands Not Run

- `dotnet build` was not run.
- No Unity batchmode command was run against the foreground checkout.
- No scene builder batch execution was run.
- No image extraction, background removal, upscaling, candidate generation, asset promotion, or `.meta` editing command was run.
