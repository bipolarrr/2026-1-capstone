# Game Flow Spec

This document owns cross-scene flow and session-state invariants.

## Scene Loop

```text
MainMenu -> CharacterSelect -> GameExploreScene <-> GameBattleScene -> (victory) -> MainMenu
```

## Session State Owners

- `GameSessionManager` is a pure static class and owns mutable cross-scene state
- `CharacterSelectionContext` is a minimal static bridge from character selection into game start

Current session fields called out by the project:

- `PlayerHearts` (HeartContainer — replaces old int PlayerHp/PlayerMaxHp)
- `PowerUps`
- `BattleEnemies` (EnemyInfo now uses `rank` instead of `attack`)
- `CurrentEventIndex`
- `IsBossBattle`
- `LastBattleResult`

## Required Flow

1. `CharacterSelect` calls `GameSessionManager.StartNewGame()` and then loads `GameExploreScene`
2. `GameExploreController` runs the fixed event sequence `NormalCombat -> ItemBox -> BossCombat`
3. `GameExploreController` prepares battle state on `GameSessionManager` and loads `GameBattleScene`
4. `BattleSceneController` reads state from `GameSessionManager`, resolves the battle, writes `LastBattleResult`, and returns to `GameExploreScene`
5. `GameExploreController` checks `LastBattleResult` on `Start()`:
   - `Won` advances the event index and proceeds
   - `Cancelled` re-shows the current encounter
6. After all three events, return to `MainMenu`

## Invariants

- `CurrentEventIndex` advances only in `GameExploreController`
- Do not directly mutate `GameSessionManager.BattleEnemies` by shared reference in scene-local runtime code; clone before modifying
- Clamp stored indices before using them after scene transitions
- Every new session field must be reset inside `StartNewGame()`
- If the player dies during enemy counterattack, return directly to `MainMenu`

## Direct Scene Run Safety

- `GameExploreController.Start()` should recover safely when `PlayerHearts.IsAlive` is false
- The intended safety behavior is to call `PlayerHearts.Reset()` so the scene still runs when launched directly
