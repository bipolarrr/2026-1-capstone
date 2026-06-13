# Runtime Asset Source Of Truth Validation Result

Date: 2026-06-12 KST
Closure metadata updated: 2026-06-12 23:56:13 +09:00 during post-audit master delta closure.

Commit validated: `311ae2e6` (`Track runtime asset source of truth`)
Commit amendment: runtime asset commit `3e0c25ce` was amended to `311ae2e6`.
Document tracking state at closure: untracked (`git ls-files docs/audit_current/13_runtime_asset_source_of_truth_validation_result.md` returned empty; `git status --porcelain -- docs/audit_current` lists this file as `??`).

Note: the first runtime-assets validation attempt against `3e0c25ce` failed during script compilation because the committed `SceneBuilderUtilityTests` referenced `MobDef.attackVfxSpritePath`, a broader foreground prototype field that was intentionally not promoted in the runtime asset commit. The commit was amended to `311ae2e6` by removing that one test reference.

## Validation Workspace

- Worktree: `C:\Users\song\Desktop\Capstone_runtime_assets_validation_20260612_231046`
- Output root: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046`
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe`
- Build target: `StandaloneWindows64`

## EditMode Tests

- Command shape: Unity batchmode, nographics, validation worktree project path, `-buildTarget StandaloneWindows64`, `-runTests`, `-testPlatform EditMode`
- XML: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\editmode\full-editmode-results.xml`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\editmode\full-editmode.log`
- Result: Passed
- Total: 132
- Passed: 132
- Failed: 0
- Skipped: 0
- New runtime asset reference test: `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks` passed

## Player Build

- Command shape: Unity batchmode, nographics, quit, validation worktree project path, `-buildTarget StandaloneWindows64`, `-buildWindows64Player`
- Build log: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\standalone-build.log`
- Build output: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\CapstoneRuntimeAssets.exe`
- Unity exit code: 0
- Executable exists: yes
- Build log contains: `Build Finished, Result: Success.`
- Scene path/build-settings error scan: no matches for missing scene or build failure patterns
- Classification: `pass-with-validation-hygiene-risk`

## Post-Validation Worktree Status

`git status --porcelain` in the validation worktree reported:

```text
 M Assets/Settings/UniversalRP.asset
 M Assets/UniversalRenderPipelineGlobalSettings.asset
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/GraphicsSettings.asset
 M ProjectSettings/ProjectSettings.asset
 M ProjectSettings/ShaderGraphSettings.asset
?? Capstone_runtime_assets_validation_20260612_231046.slnx
```

`git diff --name-only` showed content changes in:

```text
Assets/Settings/UniversalRP.asset
Assets/UniversalRenderPipelineGlobalSettings.asset
ProjectSettings/ProjectSettings.asset
```

The remaining tracked status entries emitted line-ending warnings but no content diff. Per `AGENTS.md`, because validation changed tracked files, this run is classified as pass-with-validation-hygiene-risk rather than a clean validation. No validation-worktree changes were copied back to the foreground checkout.

## Cleanup

- Current validation worktree cleanup: removed after result recording
- External output folder preserved: `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046`
