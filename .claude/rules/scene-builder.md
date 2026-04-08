# Scene Builder Rules

These rules apply when creating or changing scene builders under `Assets/Editor/`.

## Ownership

- Scene files are generated, not hand-authored
- Builders own object creation, layout, reference wiring, and persistent callback registration
- Runtime logic belongs in runtime `MonoBehaviour` classes, not in builders

## Required Patterns

- Use `SceneBuilderUtility` for shared helpers such as `SetField`, `CreateImage`, `CreateEmpty`, `CreateTMPText`, `CreateButton`, `Stretch`, `CenterPopup`, `EnsureDirectory`, and `AddSceneToBuildSettings`
- Add new reusable builder helpers to `Assets/Editor/SceneBuilderUtility.cs`
- Wire button callbacks with `UnityEventTools.AddPersistentListener()`
- Prefer builder-side automatic wiring over manual Inspector hookup
- If automatic wiring fails, leave a warning instead of silently continuing

## Serialized Field Wiring

- `SceneBuilderUtility.SetField()` binds private serialized fields via reflection
- Field-name matching is case-sensitive
- Renaming a serialized field without updating the builder call breaks the wiring

## UI Stack

- Build UI programmatically
- Keep RectTransform values explicit
- Continue using the current stack: Canvas, EventSystem, Input System UI module, and TextMesh Pro
- Use `SceneBuilderUtility.CreateTMPText()` so builders consistently load `Assets/TextMesh Pro/Fonts/Mona12.asset`

## Scene Menu Entry Points

- `Tools > Build MainMenu Scene`
- `Tools > Build CharacterSelect Scene`
- `Tools > Build GameExplore Scene`
- `Tools > Build GameBattle Scene`

## Detailed References

- Cross-scene flow rules: `.claude/specs/game-flow.md`
- Battle scene behavior: `.claude/specs/battle-system.md`
- Font sizes and layout tuning: `docs/tuning.md`
- Asset generation expectations: `docs/assets.md`
