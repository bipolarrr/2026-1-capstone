# Open Questions

Factual baseline: `docs/00_actual_project_audit.md`. Suggested defaults below are recommendations, not facts.

## Game Design

### Is Texas Hold'em in scope for the current milestone?

- Why it matters: It affects character selection UX, battle routing, test scope, and whether Hold'em should remain selectable.
- Current status: Answered by first-pass implementation. Hold'em is source-level implemented and routes to `HoldemBattleScene`, but Unity validation is still pending.
- Evidence from repo: `Assets/Scripts/Holdem/`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Editor/Tests/Holdem/`, and `GameExploreController.ResolveBattleSceneName()`.
- Suggested default decision: Treat Hold'em as implemented-but-unvalidated. Do not add more Hold'em mechanics until compile, scene generation, tests, play, and build validation pass in an isolated worktree.

### Should progression remain linear stage rounds or become a node map later?

- Why it matters: A node map would require new data structures, UI, save/session state, encounter selection, and validation.
- Evidence from repo: `StageData.rounds` is a `List<StageRoundType>`; `Stage1Forest` and `Stage2Cave` define linear round sequences; no graph/node-map model was found.
- Suggested default decision: Recommendation: keep the current docs and v0.1 implementation framed as linear stage-round progression.

### Are shops, relics, jokers, or deck/card modifiers intended?

- Why it matters: These systems would change reward design, balancing, persistence, and combat modifiers.
- Evidence from repo: `PowerUpType` contains `ReviveOnce`, `OddEvenDouble`, and `AllOrNothing`; `GameExploreController.SetupItemEncounter()` presents item-box choices; no shop/relic/joker implementation was found.
- Suggested default decision: Recommendation: document only the current item-box power-up system and defer broader reward systems until explicitly designed.

### Should Balatro-like scoring mean an explicit chips/mult model?

- Why it matters: A shared chips/mult model would be a major scoring architecture change across combat modes.
- Evidence from repo: Dice scoring is in `DamageCalculator`; mahjong scoring is in `YakuEvaluator` and `MahjongDamageTable`; `EnemyDiceResult.GetMultiplier()` is an enemy damage multiplier, not a shared Balatro-style scoring model.
- Suggested default decision: Recommendation: do not introduce chips/mult terminology in factual docs except to say it is not implemented.

### What is the intended final combat-mode set?

- Why it matters: The answer drives scene count, character select copy, architecture boundaries, tests, and asset planning.
- Evidence from repo: Dice, mahjong, and Hold'em battle controllers/rule classes exist. Hold'em now routes to `HoldemBattleScene`.
- Suggested default decision: Define v0.1 validation scope as dice plus mahjong plus first-pass Hold'em, with Hold'em explicitly marked Unity-unvalidated until the required isolated checks pass.

## Technical Architecture

### Should `GameSessionManager` remain static global state for now?

- Why it matters: Replacing it would affect every scene transition and battle controller.
- Evidence from repo: `GameSessionManager` owns character, hearts, power-ups, current event index, stage ID, battle enemies, boss flag, and last battle result; docs state it owns mutable cross-scene state.
- Suggested default decision: Recommendation: keep `GameSessionManager` as the current owner and add tests before any state architecture change.

### Should scene routing be centralized?

- Why it matters: Scene names are hard-coded in multiple controllers, including the new Hold'em route.
- Evidence from repo: `GameExploreController.ResolveBattleSceneName()` maps `Mahjong` to `MahjongBattleScene`, `Holdem` to `HoldemBattleScene`, and default/Dice to `DiceBattleScene`; battle controllers load `GameExploreScene` and `MainMenu` by string.
- Suggested default decision: Recommendation: centralize scene-name constants later as a small scoped task, but do not refactor during this documentation pass.

### Should `SceneBuilderUtility` be split?

- Why it matters: It is a high-coupling editor utility covering UI, battle construction, stage bundle loading, audio setup, build settings, and reflection wiring.
- Evidence from repo: `SceneBuilderUtility.cs` contains `SetField()`, `BuildSceneShell()`, `BuildBattleRootBase()`, `BuildAudioManager()`, `AddSceneToBuildSettings()`, sprite loading, fallback generation, and many UI helpers.
- Suggested default decision: Recommendation: do not split broadly now; extract one focused helper at a time only after tests/validation are in place.

### Which known test failures or validation risks should be triaged first?

- Why it matters: Refactors and builder edits need a reliable baseline.
- Evidence from repo: The audit mentions failing/uncertain areas around `EnemyAttackPositionResolverTests`, `EnemyProjectileAttachmentFollowerTests`, and `YakuEvaluatorTests`; `AGENTS.md` says current compile/test status must be checked in Unity.
- Suggested default decision: Recommendation: run isolated Unity validation and classify failures before gameplay or architecture refactors.

## Scene / Build Policy

### Should generated `.unity` scenes stay ignored?

- Why it matters: Ignored generated scenes reduce merge churn but make clean checkout/build validation dependent on reliable builders.
- Evidence from repo: `.gitignore` ignores `/[Aa]ssets/[Ss]cenes/`; local scene files exist under `Assets/Scenes/`; builders generate five scenes.
- Suggested default decision: Recommendation: keep scenes generated-only for now, but document and automate the regeneration/validation procedure.

### Should stale build-settings entries be removed or regenerated?

- Why it matters: Build settings reference missing scenes, which can confuse validation and runtime build behavior.
- Evidence from repo: `ProjectSettings/EditorBuildSettings.asset` references `SampleScene`, `YachtDice`, and `DiceTest`; those files were not found under local `Assets/Scenes/`.
- Suggested default decision: Recommendation: remove stale entries in a separate explicit scene/build-settings task after the human owner confirms policy.

### Are generated scene files allowed to be modified manually?

- Why it matters: Manual scene edits can be overwritten by builders and are hard to review if scenes remain ignored.
- Evidence from repo: `AGENTS.md` and `.claude/rules/scene-builder.md` say scene changes must be made through scene builders.
- Suggested default decision: Recommendation: no manual scene edits; update builders and regenerate scenes only in a validation worktree.

### Should builder validation include build settings?

- Why it matters: Scene generation can succeed while build settings remain stale or incomplete.
- Evidence from repo: `SceneBuilderUtility.AddSceneToBuildSettings()` exists; current build settings still include stale/missing scene references.
- Suggested default decision: Recommendation: include build-settings inspection in builder validation, but make any settings edit a separate explicit task.

## Asset Pipeline

### Which asset form is source of truth?

- Why it matters: The repo contains source-looking images, MP4s, extracted frames, transparent outputs, and Unity-imported sprites.
- Evidence from repo: `Assets/Player/Sprites/`, `Assets/Mobs/Sprites/`, `_transparent`, `_transparent_clean`, `_764x640` folders, and `docs/grok-imagine-sprite-prompts.md` all exist.
- Suggested default decision: Recommendation: treat Unity-ready PNG sprite frames plus prompt docs as current practical source of truth until a formal pipeline manifest exists.

### Should MP4 files under `Assets/` be tracked long-term?

- Why it matters: Videos are large intermediate artifacts and may not be needed at runtime.
- Evidence from repo: MP4 files exist under player and mob sprite folders; the audit classifies them as generated video source/intermediate evidence.
- Suggested default decision: Recommendation: keep current files unchanged for now and decide track/ignore policy in a dedicated asset pipeline task.

### Where should extraction/background-removal scripts live?

- Why it matters: Without tracked scripts, generated sprite outputs may not be reproducible.
- Evidence from repo: `docs/grok-imagine-sprite-prompts.md` describes image-to-video and frame extraction/background cleanup workflow; no tracked `extract_frames`, `remove_bg`, or `rembg` scripts were found in inspected paths.
- Suggested default decision: Recommendation: add a tracked asset-pipeline scripts folder later only after deciding source/intermediate/final asset policy.

### Should `_transparent`, `_transparent_clean`, and `_764x640` naming be normalized?

- Why it matters: Inconsistent generated folder naming makes builder paths and asset cleanup riskier.
- Evidence from repo: Multiple generated folder suffixes exist under `Assets/Player/Sprites/` and `Assets/Mobs/Sprites/`; builder constants reference specific folders.
- Suggested default decision: Recommendation: do not rename assets now; document naming as observed until an explicit asset normalization task is approved.

## Production / Roadmap

### What is the v0.1 acceptance target?

- Why it matters: It determines whether the team should stabilize dice/mahjong/Hold'em or expand macro progression.
- Evidence from repo: Dice, mahjong, and first-pass Hold'em are implemented at source level; `REFACTOR_BACKLOG.md` is a cleanup backlog, not a feature roadmap.
- Suggested default decision: Define v0.1 around stabilizing the existing linear loop across Dice, Mahjong, and first-pass Hold'em.

### Should CI be introduced?

- Why it matters: Unity batchmode validation has strict isolation requirements and currently no CI/CD exists.
- Evidence from repo: `AGENTS.md` says no CI/CD or linting; Unity validation must run in a separate worktree.
- Suggested default decision: Recommendation: keep validation local for now and document exact isolated validation commands before adding CI.

### Who owns product design decisions that are not proven by repo evidence?

- Why it matters: ChatGPT Project docs should not invent unimplemented systems or present Unity-unvalidated source work as fully validated.
- Evidence from repo: Several concepts are absent or partial: node map, shop, relic/joker systems, shared chips/mult scoring. Hold'em is source-level implemented but Unity-unvalidated.
- Suggested default decision: Recommendation: human owner decides product intent; Codex documents repo-proven facts and marks recommendations separately.

### Should a roadmap/backlog doc be created now?

- Why it matters: A roadmap could accidentally mix intended features with implemented facts.
- Evidence from repo: `REFACTOR_BACKLOG.md` already exists for cleanup sequencing; `docs/00_actual_project_audit.md` recommends roadmap only after scope confirmation.
- Suggested default decision: Recommendation: do not create roadmap/backlog docs until the owner confirms Hold'em, progression, reward systems, and v0.1 scope.
