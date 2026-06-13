# Coding Rules

Repo-wide rules here. Feature-specific detail stay out.

## General Style

- Indentation: tabs only
- No Hungarian notation like `btn_`, `txt_`, `img_`
- Korean comments + UI text normal here
- Explicit readable names > short abbreviations
- Reuse existing classes, methods, scene wiring before parallel impls

## Structure

- One responsibility per class
- Public UI/event entry points short. Delegate work to private helpers
- Scene controllers follow existing pattern:
  - `Start()` reads `GameSessionManager`
  - init UI + view state
  - expose public methods for button callbacks
- Runtime behavior in runtime components. Editor-only setup in builder scripts

## Control Flow

- Validate before mutating state
- Early returns when preconditions fail
- Null handling explicit:
  - skip safely when optional
  - log warning when missing ref = broken wiring

## Unity-Specific Guardrails

- No obsolete Unity 6 APIs
- No runtime `Shader.Find()` for required build assets. Inject materials from builders instead
- Prefer `NonAlloc` physics APIs in hot paths
- Physics dice: no `ReadTopFace()` until die settled

## Logging

- `Debug.Log(...)` for temporary/informational flow
- `Debug.LogWarning(...)` for missing wiring or recoverable config issues
- Avoid `Debug.LogError(...)` unless truly unrecoverable