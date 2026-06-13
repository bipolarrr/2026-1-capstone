# Scene Source Of Truth Decision

## Purpose

This memo decides how this Unity project should handle `.unity` scene files and generated scenes.

The current project already has a strong builder-based scene construction model:

- `AGENTS.md` says scenes are generated from builders in `Assets/Editor/` and are not the source of truth.
- `README.md` says scene files are not included in Git and lists five builder menu entries.
- `.claude/rules/scene-builder.md` says scene files are generated, builders own object creation, layout, reference wiring, and persistent callback registration.
- `Assets/Editor/*SceneBuilder.cs` contains builders for `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, and `MahjongBattleScene`.
- `SceneBuilderUtility.SaveSceneAndShowDialog()` saves generated scenes and calls `AddSceneToBuildSettings()`.

However, the repository is inconsistent today:

- `.gitignore` ignores all of `Assets/Scenes/`.
- Local scene files exist in `Assets/Scenes/`.
- `ProjectSettings/EditorBuildSettings.asset` is tracked and references scene paths that are not present in a clean checkout.
- `ProjectSettings/EditorBuildSettings.asset` also references `SampleScene`, `YachtDice`, and `DiceTest`, but those scene files are not present locally under `Assets/Scenes/`.

Default preference for this decision: prefer reproducibility and clean checkout safety, avoid hidden local-only state, and avoid making Codex depend on untracked generated scene files.

## Policy A: Generated-only scenes

`Assets/Scenes/` remains ignored. Builders are the source of truth. Generated `.unity` scene files are local build products.

### Advantages

- Matches the current stated policy in `AGENTS.md`, `README.md`, and `.claude/rules/scene-builder.md`.
- Keeps large generated YAML scene files out of source control.
- Forces scene changes through builders instead of manual Inspector edits.
- Preserves the current Codex rule that generated scenes and prefabs should not be edited manually.
- Reduces merge conflicts in `.unity` files.
- Keeps serialized field wiring visible in C# builders through `SceneBuilderUtility.SetField()`.

### Risks

- A clean checkout does not contain scene files, so the project is not immediately buildable unless scenes are regenerated first.
- `ProjectSettings/EditorBuildSettings.asset` can reference missing local scene files.
- Build settings become partly misleading if they point at ignored generated artifacts.
- Scene builder output can drift without detection unless validation explicitly regenerates scenes.
- New contributors may open the project and see missing scenes until they know to run the builders.
- Codex cannot inspect generated scene contents from Git; it must reason from builders and any local foreground-generated files.

### Required repo changes

- Keep `/[Aa]ssets/[Ss]cenes/` ignored in `.gitignore`.
- Update documentation to say generated scenes must be rebuilt after a clean checkout before play/build validation.
- Decide whether `ProjectSettings/EditorBuildSettings.asset` should be tracked with generated-scene paths, generated during validation, or cleaned to remove missing/stale entries.
- Remove or regenerate stale build settings entries for `SampleScene`, `YachtDice`, and `DiceTest` in a separate implementation task.
- Add a repeatable "build all scenes" validation procedure if one does not already exist.

### Required validation steps

- In a separate validation worktree, run Unity batchmode against the validation project only.
- Run all five builders:
  - `Tools > Build MainMenu Scene`
  - `Tools > Build CharacterSelect Scene`
  - `Tools > Build GameExplore Scene`
  - `Tools > Build DiceBattle Scene`
  - `Tools > Build MahjongBattle Scene`
- Confirm generated scene paths exist after builder execution.
- Confirm `EditorBuildSettings.scenes` contains only intentional scene paths.
- Run EditMode tests from the validation worktree.
- Run a Windows Standalone x64 build validation with explicit `-buildTarget`.
- Write logs, test results, and build outputs outside the Unity project root.
- Run `git status --porcelain` in the validation worktree afterward.
- Treat tracked file changes as validation failure unless the task explicitly allowed generated project-setting updates.

### Impact on Codex

- Codex should treat builders as source of truth and never depend on untracked local scene files.
- Codex can read local `Assets/Scenes/` only as diagnostic evidence, not as authoritative project state.
- Any scene-affecting work remains high risk and must edit builders, not `.unity` files.
- Codex validation must regenerate scenes in an isolated worktree before build or scene-level testing.

### Impact on ChatGPT Project documentation

- Documentation must clearly state that `.unity` files are generated artifacts.
- Scene behavior docs should reference builders and runtime controllers, not local scene YAML.
- Any setup guide must include the scene generation step for clean checkouts.
- Documentation should list the five supported generated scenes and call out stale or removed scene names separately.

### Impact on clean checkout

- Clean checkout is incomplete until scenes are generated.
- This is acceptable only if the repo provides a clear, repeatable generation path.
- Hidden local-only state remains a risk unless validation always starts from clean checkout plus builder regeneration.

### Impact on Unity build settings

- Build settings must be treated carefully because they are tracked while generated scene files are ignored.
- The current tracked build settings are not clean-checkout safe because they reference ignored and missing scene paths.
- Best fit under this policy: either generate/update build settings as part of scene builder validation, or keep tracked build settings free of stale paths and document that builders repopulate them.

## Policy B: Track generated `.unity` scenes

Scene files become source-controlled artifacts. `Assets/Scenes/*.unity` and matching `.meta` files are committed.

### Advantages

- Clean checkout contains scenes immediately.
- Unity build settings can reference tracked scene assets safely.
- Build validation does not require a scene-generation pre-step.
- Reviewers can inspect scene YAML diffs when generated output changes.
- Unity users can open scenes directly after clone without running builder menus first.

### Risks

- Contradicts current `AGENTS.md`, `README.md`, and `.claude/rules/scene-builder.md` source-of-truth rules.
- Creates two sources of truth: builders and committed generated scenes.
- Manual Inspector edits can slip into tracked scene files.
- `.unity` YAML diffs are noisy and merge-conflict prone.
- Codex may be tempted to patch generated scenes directly, which violates the current operating model.
- Builder changes require scene regeneration and committed scene diffs, increasing task size and risk.
- Generated scene GUID and `.meta` handling becomes part of every scene policy decision.

### Required repo changes

- Modify `.gitignore` to stop ignoring at least the intended scene files under `Assets/Scenes/`.
- Add the selected `.unity` scene files and `.meta` files to Git.
- Update `AGENTS.md`, `README.md`, and `.claude/rules/scene-builder.md` to say scenes are tracked generated artifacts, while builders remain the authoring source.
- Define whether manual scene edits are prohibited or allowed only in exceptional cases.
- Regenerate all tracked scenes from builders before initial commit.
- Clean or regenerate stale entries for `SampleScene`, `YachtDice`, and `DiceTest`.

### Required validation steps

- Regenerate all tracked scenes in an isolated validation worktree.
- Compare regenerated scene files against tracked files.
- Fail validation if builder output differs from tracked scene files unless the task intentionally updates generated scenes.
- Run EditMode tests.
- Run build validation with explicit build target.
- Confirm `ProjectSettings/EditorBuildSettings.asset` references only tracked scenes.
- Confirm `git status --porcelain` contains only expected generated scene diffs.

### Impact on Codex

- Codex must edit builders first, then regenerate scene files when scene output changes.
- Codex must not hand-edit `.unity` files unless a task explicitly targets generated artifacts.
- More tasks become high risk because builder changes can require generated scene and `.meta` changes.
- Review output becomes larger and harder to reason about.

### Impact on ChatGPT Project documentation

- Documentation must distinguish "authoring source" from "committed generated artifact."
- Setup docs become simpler for Unity users because scenes exist after checkout.
- Maintenance docs must explain when to regenerate scenes and how to review generated scene diffs.

### Impact on clean checkout

- Strongest clean-checkout behavior.
- Project can open scenes and satisfy build settings without a hidden local generation step.
- Reproducibility depends on keeping tracked scenes synchronized with builders.

### Impact on Unity build settings

- Build settings can safely reference tracked scenes.
- `ProjectSettings/EditorBuildSettings.asset` should be kept tracked and cleaned to include only intentional scene paths.
- Missing entries such as `SampleScene`, `YachtDice`, and `DiceTest` must be removed or restored as tracked scenes.

## Policy C: Hybrid

Track only stable bootstrap/build scenes. Generate volatile test/prototype scenes.

In this project, candidates for tracked stable scenes would likely be `MainMenu`, `CharacterSelect`, and possibly `GameExploreScene`. Volatile generated scenes would likely include battle/prototype-heavy scenes such as `DiceBattleScene` and `MahjongBattleScene`, unless the team decides they are required build scenes.

### Advantages

- Improves clean-checkout safety for stable entry scenes.
- Avoids tracking the noisiest generated scenes if battle scenes change frequently.
- Allows `EditorBuildSettings.asset` to reference tracked bootstrap scenes while volatile scenes remain builder-owned.
- Can reduce friction for opening the project while retaining builder-first development for complex scenes.

### Risks

- Introduces policy complexity: every scene needs a category.
- Still creates mixed source-of-truth behavior.
- Runtime scene loading may fail if untracked battle scenes are required but not generated.
- Codex must know which scenes are tracked artifacts and which are generated-only artifacts.
- Build settings become tricky if a final player build needs scenes that remain ignored.
- The current runtime flow uses `GameExploreScene` to route into `DiceBattleScene` or `MahjongBattleScene`; if battle scenes are not tracked, a clean checkout still cannot run the full game without generation.

### Required repo changes

- Modify `.gitignore` with explicit exceptions for tracked stable scenes and `.meta` files.
- Add selected stable `.unity` and `.meta` files to Git.
- Keep volatile scene paths ignored.
- Update `AGENTS.md`, `README.md`, `.claude/rules/scene-builder.md`, and ChatGPT Project docs with the scene category table.
- Clean `EditorBuildSettings.asset` so it references only scenes valid for the chosen category model.
- Decide whether battle scenes are build scenes or generated validation artifacts.

### Required validation steps

- On clean checkout, verify tracked stable scenes exist before generation.
- Regenerate volatile scenes in an isolated validation worktree.
- Verify tracked scenes either match builder output or are intentionally stable handoff artifacts.
- Run EditMode tests.
- Run full build validation after all build-required scenes exist.
- Confirm `EditorBuildSettings.asset` does not require absent ignored scenes before the generation step, unless that is explicitly documented.
- Run `git status --porcelain` and fail on unexpected tracked changes.

### Impact on Codex

- Codex must apply different rules per scene.
- Codex still needs builder inspection for generated-only scenes.
- Scene-affecting tasks must state whether tracked artifacts should be regenerated.
- The risk of accidentally relying on local-only ignored scene files remains unless the hybrid boundary is strict.

### Impact on ChatGPT Project documentation

- Documentation must include a scene ownership table with columns for tracked, generated, build-required, and validation procedure.
- Clean-checkout setup docs must explain which scenes are present immediately and which must be generated.
- Project docs must avoid calling all scenes "not source of truth" if some generated outputs are tracked.

### Impact on clean checkout

- Better than Policy A for tracked stable scenes.
- Worse than Policy B for the full game if battle scenes are required and ignored.
- Clean checkout safety depends on whether every build-required scene is tracked or generated before validation.

### Impact on Unity build settings

- Build settings must not reference ignored volatile scenes unless generation is guaranteed before build.
- If final builds include battle scenes, either those scenes must be tracked or the build pipeline must regenerate them before build.
- `SampleScene`, `YachtDice`, and `DiceTest` still need explicit resolution.

## Current inconsistencies to resolve

### `Assets/Scenes/` ignored but local scene files exist

Evidence:

- `.gitignore` contains `/[Aa]ssets/[Ss]cenes/`.
- Local foreground checkout contains:
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/CharacterSelect.unity`
  - `Assets/Scenes/GameExploreScene.unity`
  - `Assets/Scenes/DiceBattleScene.unity`
  - `Assets/Scenes/MahjongBattleScene.unity`
- `docs/00_actual_project_audit.md` records that no `Assets/Scenes/*` files are tracked.

Decision needed:

- Either keep these as ignored generated outputs and make generation mandatory, or intentionally track selected/all scenes and update `.gitignore` plus documentation.

### `EditorBuildSettings.asset` references missing scene paths

Evidence:

- `ProjectSettings/EditorBuildSettings.asset` references all of:
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/SampleScene.unity`
  - `Assets/Scenes/CharacterSelect.unity`
  - `Assets/Scenes/YachtDice.unity`
  - `Assets/Scenes/DiceTest.unity`
  - `Assets/Scenes/DiceBattleScene.unity`
  - `Assets/Scenes/GameExploreScene.unity`
  - `Assets/Scenes/MahjongBattleScene.unity`
- Local `Assets/Scenes/` does not contain `SampleScene.unity`, `YachtDice.unity`, or `DiceTest.unity`.
- `Assets/Editor/*SceneBuilder.cs` only builds `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, and `MahjongBattleScene`.

Decision needed:

- Build settings should reference only intentional scenes.
- Missing scene entries should not remain silently enabled.

### `SampleScene`, `YachtDice`, and `DiceTest`

These three names have no matching current builder in `Assets/Editor/*SceneBuilder.cs` and no matching local `.unity` file in `Assets/Scenes/`.

Recommended resolution:

- `SampleScene`: remove from build settings unless it is intentionally restored as a tracked bootstrap or tutorial scene.
- `YachtDice`: remove from build settings unless it is intentionally replaced by `DiceBattleScene` or a new builder-owned scene.
- `DiceTest`: remove from build settings unless it is intentionally restored as a generated validation scene. If it is only a local manual test scene, it should not be enabled in tracked build settings.

If any of the three are intentional placeholders, document them explicitly with owner, builder or tracking policy, and whether they are build-required.

## Recommendation

Recommend Policy A as the default: generated-only scenes, builders as source of truth, and `Assets/Scenes/` ignored.

This recommendation matches the strongest existing project evidence:

- `AGENTS.md` already states that `.unity` scenes are generated from builders and are not source of truth.
- `README.md` already says scene files are not included in Git.
- `.claude/rules/scene-builder.md` already says builders own scene creation and wiring.
- The current builder set is comprehensive for the implemented runtime flow: menu, character select, explore, dice battle, and mahjong battle.
- The current scene wiring model depends on builder-side `SceneBuilderUtility.SetField()` and persistent listener registration.

Policy A does not fully satisfy clean-checkout safety by itself. To make it acceptable, the project must also resolve build settings and validation:

- Keep generated scenes ignored.
- Do not make Codex depend on ignored local scene files.
- Treat builders as authoritative.
- Add a documented isolated validation step that regenerates scenes before tests/builds.
- Clean `ProjectSettings/EditorBuildSettings.asset` in a separate implementation task so it does not contain stale enabled entries for missing scenes.
- Decide whether build settings should be a tracked stable list or a builder-regenerated artifact, then document that rule.

Policy B has better immediate clean-checkout behavior, but it conflicts with the current operating rules and creates a second source of truth. Policy C is useful only if the team wants some immediately openable bootstrap scenes, but it adds categorization complexity and still leaves the full game dependent on generated battle scenes unless those scenes are tracked too.

Therefore, the default path should be:

1. Keep generated scenes ignored.
2. Make scene generation explicit and repeatable.
3. Remove or explicitly document stale build settings entries.
4. Validate from clean checkout plus builder regeneration in a separate worktree.

