# Coding Rules

Repository-wide coding rules live here. Keep feature-specific implementation detail out of this file.

## General Style

- Indentation: tabs only
- Avoid Hungarian notation such as `btn_`, `txt_`, `img_`
- Korean comments and UI text are normal for this project
- Prefer explicit, readable names over short abbreviations
- Reuse existing classes, methods, and scene wiring before adding parallel implementations

## Structure

- Keep classes focused on one responsibility
- Keep public UI/event entry points short and delegate real work to private helpers
- Scene controllers should follow the existing pattern:
  - `Start()` reads `GameSessionManager`
  - initialize UI and view state
  - expose public methods for button callbacks
- Keep runtime behavior in runtime components and editor-only setup in builder scripts

## Control Flow

- Validate before mutating state
- Use early returns when the preconditions are not satisfied
- Keep null handling explicit:
  - skip safely when optional behavior is acceptable
  - log a warning when a missing reference indicates broken wiring

## Unity-Specific Guardrails

- Do not use obsolete Unity 6 APIs
- Do not use runtime `Shader.Find()` for required build assets; inject materials from builders instead
- Prefer `NonAlloc` physics APIs in hot paths
- For physics dice, do not call `ReadTopFace()` until the die has settled

## Logging

- `Debug.Log(...)` for temporary or informational flow logging
- `Debug.LogWarning(...)` for missing wiring or recoverable configuration problems
- Avoid `Debug.LogError(...)` unless the situation is genuinely unrecoverable
