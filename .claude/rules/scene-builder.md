# Scene Builder Rules

Rules apply when creating/changing scene builders under `Assets/Editor/`.

## Ownership

- Scene files generated, not hand-authored
- Builders own object creation, layout, reference wiring, persistent callback registration
- Runtime logic belongs in runtime `MonoBehaviour` classes, not builders

## Required Patterns

- Use `SceneBuilderUtility` for shared helpers like `SetField`, `CreateImage`, `CreateEmpty`, `CreateTMPText`, `CreateButton`, `Stretch`, `CenterPopup`, `EnsureDirectory`, `AddSceneToBuildSettings`
- Add new reusable builder helpers to `Assets/Editor/SceneBuilderUtility.cs`
- Wire button callbacks with `UnityEventTools.AddPersistentListener()`
- Prefer builder-side auto wiring over manual Inspector hookup
- Auto wiring fails → leave warning, no silent continue

## Serialized Field Wiring

- `SceneBuilderUtility.SetField()` binds private serialized fields via reflection
- Field-name match case-sensitive
- Rename serialized field without updating builder call → wiring breaks

## UI Stack

- Build UI programmatically
- Keep RectTransform values explicit
- Keep current stack: Canvas, EventSystem, Input System UI module, TextMesh Pro
- Use `SceneBuilderUtility.CreateTMPText()` so builders load `Assets/TextMesh Pro/Fonts/Mona12.asset` consistently

## Scene Menu Entry Points

- `Tools > Build MainMenu Scene`
- `Tools > Build CharacterSelect Scene`
- `Tools > Build GameExplore Scene`
- `Tools > Build DiceBattle Scene`
- `Tools > Build MahjongBattle Scene`

## Detailed References

- Cross-scene flow rules: `.claude/specs/game-flow.md`
- Battle scene behavior: `.claude/specs/battle-system.md`
- Font sizes, layout tuning: `docs/tuning.md`
- Asset generation expectations: `docs/assets.md`