# Validation Triage

## Scope

Focused EditMode test triage for:

- `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`
- `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`
- `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs`

Initial triage did not apply fixes. The follow-up validation-baseline task applied the test-only resolutions recorded in "Final Resolution Notes".

## Validation Setup

Validation worktree:

- `C:\Users\song\desktop\Capstone_validation_triage`
- Detached HEAD: `3a9c6dda`
- Foreground checkout was not used as Unity `-projectPath`.

Output directory:

- `C:\Users\song\desktop\Capstone_validation_outputs\triage`

Unity version observed:

- `6000.3.11f1 (3000ef702840)`

Important validation note:

- Running with `-quit` caused Unity to import the project and exit without producing test results.
- Successful test runs omitted `-quit`; the Unity Test Framework exited Unity after completing the run.
- The validation worktree ended with `git status --porcelain` reporting:
  - `M ProjectSettings/EditorBuildSettings.asset`
  - `M ProjectSettings/ShaderGraphSettings.asset`
  - `?? Capstone_validation_triage.slnx`
- `git diff` showed no content diff for the two tracked settings files, only line-ending warnings. This is still a validation hygiene issue under `AGENTS.md` because tracked files were reported modified.

## Test Command Used

Command pattern used for each focused class:

```powershell
$unity='C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'
$out='C:\Users\song\desktop\Capstone_validation_outputs\triage'
$args=@(
  '-batchmode',
  '-projectPath','C:\Users\song\desktop\Capstone_validation_triage',
  '-runTests',
  '-testPlatform','EditMode',
  '-testFilter','<test-class-filter>',
  '-testResults',"$out\<result-file>.xml",
  '-buildTarget','StandaloneWindows64',
  '-logFile',"$out\<log-file>.log"
)
Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru -WindowStyle Hidden
```

Filters run:

- `BattleTests.EnemyAttackPositionResolverTests`
- `BattleTests.EnemyProjectileAttachmentFollowerTests`
- `MahjongTests.YakuEvaluatorTests`

## Result Summary

| Test class | Total | Passed | Failed | Result file |
|---|---:|---:|---:|---|
| `BattleTests.EnemyAttackPositionResolverTests` | 5 | 3 | 2 | `enemy-attack-position-results.xml` |
| `BattleTests.EnemyProjectileAttachmentFollowerTests` | 2 | 1 | 1 | `enemy-projectile-attachment-results.xml` |
| `MahjongTests.YakuEvaluatorTests` | 12 | 11 | 1 | `yaku-evaluator-results.xml` |

## Failing Tests

### `BattleTests.EnemyAttackPositionResolverTests.Melee_StandsInFrontOfPlayer`

Failure message:

```text
Expected: less than 0.00100000005f
But was:  82.7999878f
```

Stack:

```text
Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs:97
Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs:28
```

Classification:

- Test drift.

Reasoning:

- The test hard-codes an old expected melee stand position of `(500.8, 10, 0)`.
- Current `EnemyAttackPositionResolver` computes melee stand position from visual horizontal bounds, enemy width, player width, and a clamped melee gap.
- With the test transforms, the current resolver contract computes a much closer player-front position. The battle spec only requires "player-front melee position"; it does not require the old hard-coded coordinate.
- `Ranged_StaysAtHome`, `Unique_StaysAtHome`, and `DefaultProjectileResolvesAsRanged` still pass, so the failure is localized to stale exact-position expectations.

Smallest safe fix:

- Update the test expectation to match the current visual-bounds contract.
- Prefer deriving the expected value from the same public inputs used by the test setup instead of retaining unexplained magic constants.
- Also update the same test's `impactWorldPosition` assertion, because after the stand assertion is corrected the old expected impact point may become the next stale assertion.

Files that would need to change:

- `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`

Risk:

- Low if the test is updated only to reflect the current public positioning contract.
- Medium if runtime positioning constants are changed, because enemy melee animation spacing may shift in both dice and mahjong battles.

### `BattleTests.EnemyAttackPositionResolverTests.MidRange_StandsBetweenHomeAndMelee`

Failure message:

```text
Expected: less than 0.00100000005f
But was:  45.5399933f
```

Stack:

```text
Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs:97
Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs:51
```

Classification:

- Test drift.

Reasoning:

- This test derives its expected midpoint from the same stale melee coordinate used by `Melee_StandsInFrontOfPlayer`.
- Current implementation still uses `Vector3.Lerp(slotWorld, meleeStand, 0.55f)` for midrange; the stale part is the old `meleeStand` value.

Smallest safe fix:

- Update the midrange expected value to interpolate from slot position to the current melee stand expectation.
- Keep the test focused on the public contract: midrange stands partway between home and melee.

Files that would need to change:

- `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`

Risk:

- Low if changed together with the melee expectation.

### `BattleTests.EnemyProjectileAttachmentFollowerTests.EnemyProjectileAttack_WithoutFollower_UsesShooterBodyFallbackStart`

Failure message:

```text
Expected: (-42.00, 5.00, 0.00)
But was:  (-24.90, 10.82, 0.00)
```

Stack:

```text
Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs:72
```

Classification:

- Test drift, with minor design ambiguity around coroutine first-frame behavior.

Reasoning:

- The fallback start point in `BattleAnimations.EnemyProjectileAttack()` is `RectPointWorld(shooterBody, 0.08f, 0.55f)`, which matches the test's expected `(-42, 5, 0)`.
- The test calls `routine.MoveNext()` once and then asserts the projectile is still at the raw start point.
- Current coroutine behavior sets `projectileRt.position = start`, activates the projectile, immediately advances elapsed time inside the movement loop, assigns the first interpolated position, and only then yields.
- Therefore the first observable position after one `MoveNext()` is already slightly along the trajectory, not the raw start point.

Smallest safe fix:

- Test-only option: adjust the test so it accounts for coroutine execution semantics. For example, use a controlled duration or assertion that verifies the first observed position is consistent with a trajectory that starts at `RectPointWorld(shooterBody, 0.08f, 0.55f)`.
- Gameplay option, not recommended in this triage task: insert an initial yield after setting the projectile to `start` if the intended contract is that the first rendered frame must show the exact release point.

Files that would need to change:

- Test-only fix: `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`
- Gameplay fix if chosen later: `Assets/Scripts/Battle/BattleAnimations.cs`

Risk:

- Test-only fix is low risk.
- Runtime coroutine change is medium risk because it changes projectile timing and visual launch behavior.

### `MahjongTests.YakuEvaluatorTests.Toitoi_AllTriplets`

Failure message:

```text
Expected: True
But was:  False
```

Stack:

```text
Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs:75
```

Classification:

- Design ambiguity.

Reasoning:

- The test hand is four triplets plus a pair.
- Current game model treats all standard closed triplets as concealed: `ConcealedKoutsuCount()` counts every `Koutsu`/`Kantsu`, and the comment says the current game has no calls/open melds, so every triplet is concealed.
- `YakuEvaluator.Evaluate()` checks yakuman first. Four concealed triplets are `Suuankou`, so the evaluator adds `Suuankou`, finalizes yakuman, and returns before adding normal yaku such as `Toitoi`.
- Under this implementation, normal `Toitoi` is effectively unreachable without an open-meld/concealed-meld distinction or a policy of retaining normal yaku alongside yakuman.

Smallest safe fix:

- If current yakuman-priority behavior is intended: change this test to assert `Suuankou`/yakuman for the all-concealed triplet hand, and either remove or mark `Toitoi` coverage pending open-meld support.
- If `Toitoi` should be represented separately from `Suuankou`: add explicit open/concealed meld metadata to the mahjong model before expecting this test to pass. That is not a small triage fix.
- If normal yaku should be retained even on yakuman hands: change `YakuEvaluator` to continue adding normal yaku after yakuman detection. That is a scoring behavior change and should be a separate design decision.

Files that would need to change:

- Test-only current-contract fix: `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs`
- Model/scoring design fix if chosen later:
  - `Assets/Scripts/Mahjong/Hand.cs`
  - `Assets/Scripts/Mahjong/HandDecomposer.cs`
  - `Assets/Scripts/Mahjong/YakuEvaluator.cs`
  - tests under `Assets/Editor/Tests/Mahjong/`

Risk:

- Test-only current-contract fix is low risk.
- Adding open/concealed meld metadata is medium to high risk because it affects hand decomposition, yaku evaluation, controller assumptions, and scoring tests.
- Changing yakuman result composition is medium risk because damage/scoring code may assume yakuman short-circuits normal yaku.

## Environment And Validation Issues

### `-quit` suppresses command-line test execution in this project

Classification:

- Environment/validation issue.

Evidence:

- Command with `-runTests ... -quit` produced no result XML.
- Logs showed project import and clean batchmode exit without TestRunner execution.
- Command without `-quit` produced XML results and TestRunner logs.

Smallest safe fix:

- Update local validation instructions for this project to omit `-quit` when using `-runTests`.
- Let Unity Test Framework exit the editor after the test run.

Files that would need to change:

- Documentation only, if codified later.

### Validation worktree dirty after Unity invocation

Classification:

- Environment/validation issue.

Evidence:

```text
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/ShaderGraphSettings.asset
?? Capstone_validation_triage.slnx
```

Additional observation:

- `git diff` showed no content diff for the two tracked project settings files and emitted line-ending warnings.

Smallest safe fix:

- Normalize line-ending handling for Unity project settings in a separate repo hygiene task.
- Decide whether generated `.slnx` files should be ignored.
- Keep validation worktrees disposable and fail validation when tracked files are reported modified.

Files that would need to change:

- Possibly `.gitattributes` or `.gitignore`, but this triage task explicitly did not modify either.

Risk:

- Medium for validation reliability. Dirty validation status can obscure real Unity-generated project setting changes.

## Proposed Minimal Fixes

| Failure | Classification | Minimal fix | Files |
|---|---|---|---|
| `Melee_StandsInFrontOfPlayer` | Test drift | Update expected stand and impact positions to current visual-bounds contract. | `EnemyAttackPositionResolverTests.cs` |
| `MidRange_StandsBetweenHomeAndMelee` | Test drift | Derive expected midrange from updated current melee position. | `EnemyAttackPositionResolverTests.cs` |
| `EnemyProjectileAttack_WithoutFollower_UsesShooterBodyFallbackStart` | Test drift / design ambiguity | Update test to account for first `MoveNext()` advancing the coroutine, or explicitly decide runtime should yield once at launch. | `EnemyProjectileAttachmentFollowerTests.cs`; maybe `BattleAnimations.cs` later |
| `Toitoi_AllTriplets` | Design ambiguity | Decide whether all closed triplets should assert `Suuankou` instead of `Toitoi`, or add open/concealed meld modeling later. | `YakuEvaluatorTests.cs`; maybe mahjong model/evaluator files later |

## Recommended Order Of Fixes

1. Fix the validation command documentation: omit `-quit` for `-runTests` in this Unity/Test Framework setup.
2. Resolve validation worktree hygiene separately: line-ending-only project setting modifications and generated `.slnx`.
3. Apply test-only corrections to `EnemyAttackPositionResolverTests.cs`.
4. Apply a test-only correction to `EnemyProjectileAttachmentFollowerTests.cs`, unless the team explicitly wants a gameplay timing change.
5. Make a design decision for `Toitoi` versus `Suuankou` under the current closed-only mahjong model before changing mahjong runtime behavior.

## Final Resolution Notes

Resolution date: 2026-05-27.

Validation worktree:

- `C:\Users\song\desktop\Capstone_editmode_baseline_validation`

Output directory:

- `C:\Users\song\desktop\Capstone_validation_outputs\editmode-baseline`

Unity version observed:

- `6000.3.11f1`

Initial focused reproduction:

| Test class | Total | Passed | Failed | Result file |
|---|---:|---:|---:|---|
| `MahjongTests.YakuEvaluatorTests` | 12 | 11 | 1 | `initial-yaku-results.xml` |
| `BattleTests.EnemyAttackPositionResolverTests` | 5 | 3 | 2 | `initial-enemy-attack-position-results.xml` |
| `BattleTests.EnemyProjectileAttachmentFollowerTests` | 2 | 1 | 1 | `initial-enemy-projectile-attachment-results.xml` |

Final focused validation:

| Test class | Total | Passed | Failed | Result file |
|---|---:|---:|---:|---|
| `MahjongTests.YakuEvaluatorTests` | 12 | 12 | 0 | `fixed-yaku-results.xml` |
| `BattleTests.EnemyAttackPositionResolverTests` | 5 | 5 | 0 | `fixed-enemy-attack-position-results.xml` |
| `BattleTests.EnemyProjectileAttachmentFollowerTests` | 2 | 2 | 0 | `fixed-enemy-projectile-attachment-results.xml` |

Full EditMode validation:

| Total | Passed | Failed | Skipped | Result file |
|---:|---:|---:|---:|---|
| 68 | 68 | 0 | 0 | `full-editmode-results.xml` |

Applied resolutions:

| Failure | Final classification | Resolution |
|---|---|---|
| `YakuEvaluatorTests.Toitoi_AllTriplets` | Design ambiguity resolved as current-contract test drift for v0.1 | Renamed/reframed the test to assert `Suuankou` yakuman for a closed-only four-triplet hand. The test now also asserts `Toitoi` is not retained on the yakuman result. No scoring/runtime code changed. Open-meld `Toitoi` coverage remains deferred until the model supports open/concealed meld metadata. |
| `EnemyAttackPositionResolverTests.Melee_StandsInFrontOfPlayer` | Test drift | Replaced stale hard-coded expected stand/impact coordinates with expectations derived from the current visual-bounds, minimum-gap, and impact-overlap contract. |
| `EnemyAttackPositionResolverTests.MidRange_StandsBetweenHomeAndMelee` | Test drift | Derived the midrange expected position from the corrected current melee stand position and the existing `0.55f` interpolation contract. |
| `EnemyProjectileAttachmentFollowerTests.EnemyProjectileAttack_WithoutFollower_UsesShooterBodyFallbackStart` | Test drift | Kept runtime coroutine behavior unchanged and made the test observe the fallback start deterministically by using a very long duration for the first yielded frame. |

Files changed by the resolution:

- `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs`
- `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`
- `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`
- `docs/10_validation_triage.md`
- `docs/15_validation_baseline_result.md`

Post-validation worktree status still reported expected applied patch changes plus validation hygiene noise:

- `M ProjectSettings/EditorBuildSettings.asset`
- `M ProjectSettings/ShaderGraphSettings.asset`
- `?? Capstone_editmode_baseline_validation.slnx`

This remains an environment/validation hygiene issue. The full EditMode test result is passing, but the validation worktree is not clean after Unity invocation.
