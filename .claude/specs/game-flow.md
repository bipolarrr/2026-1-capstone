# Game Flow Spec

Doc owns cross-scene flow + session-state invariants.

## Scene Loop

```text
MainMenu -> CharacterSelect -> GameExploreScene <-> (DiceBattleScene | MahjongBattleScene) -> (victory) -> MainMenu
```

## Session State Owners

- `GameSessionManager` pure static class. Owns mutable cross-scene state.
- `CharacterSelectionContext` minimal static bridge. CharacterSelect → game start.

Session fields:

- `PlayerHearts` (HeartContainer — replaces old int PlayerHp/PlayerMaxHp)
- `PowerUps`
- `BattleEnemies` (EnemyInfo now uses `rank` instead of `attack`)
- `CurrentEventIndex`
- `IsBossBattle`
- `LastBattleResult`

## Required Flow

1. `CharacterSelect` calls `GameSessionManager.StartNewGame()`, loads `GameExploreScene`
2. `GameExploreController` runs fixed sequence `NormalCombat -> ItemBox -> BossCombat`
3. `GameExploreController` preps battle state on `GameSessionManager`, then calls `ResolveBattleSceneName(SelectedCharacter)` and loads either `DiceBattleScene` (default) or `MahjongBattleScene` (마작 캐릭터)
4. `BattleSceneController` reads state from `GameSessionManager`, resolves battle, writes `LastBattleResult`, returns to `GameExploreScene`
5. `GameExploreController` checks `LastBattleResult` on `Start()`:
   - `Won` advances event index, proceeds
   - `Cancelled` re-shows current encounter
6. After all three events, return to `MainMenu`

## Invariants

- `CurrentEventIndex` advances only in `GameExploreController`
- Never mutate `GameSessionManager.BattleEnemies` by shared ref in scene-local code. Clone first.
- Clamp stored indices before use after scene transitions
- Every new session field must reset inside `StartNewGame()`
- Player dies in enemy counterattack → return directly to `MainMenu`

## Direct Scene Run Safety

- `GameExploreController.Start()` must recover safely when `PlayerHearts.IsAlive` false
- Intended safety: call `PlayerHearts.Reset()` so scene runs when launched directly