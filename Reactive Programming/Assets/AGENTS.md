# Project Rules

This is a Unity C# game project.

## Architecture

- UI architecture uses MVVM.
- Reactive programming uses R3.
- ScriptableObjects are definitions/configuration only.
- Runtime mutable state must live in runtime models/services.
- Save/load code must not be mixed into gameplay calculations.
- Views must not contain domain logic.
- ViewModels should expose state and commands, not Unity scene object behavior.
- Services own domain operations and coordination.

## Unity rules

- Dispose R3 subscriptions explicitly.
- Avoid allocations in hot paths.
- Avoid LINQ in Update/tick-heavy code.
- Avoid repeated GetComponent calls in hot paths.
- Cache references when objects are used repeatedly.
- Be careful with Awake/OnEnable/Start ordering.
- Use OnDisable/OnDestroy for cleanup depending on subscription lifetime.

## Unity MCP rules

- Unity MCP may be used for verification:
    - read console errors
    - inspect scene hierarchy
    - inspect prefabs/assets
    - check serialized references
    - run editmode/playmode tests when available

- Unity MCP must not be used to make scene/prefab/asset/project-setting changes unless the user explicitly requested editor-side changes.

- Code review agents may inspect Unity Editor state but must not mutate it.

- Worker may use Unity MCP for editor-side implementation only after explaining the intended action.

## Code style

- Prefer simple concrete classes.
- Do not add interfaces unless there are multiple real implementations or tests need a seam.
- Do not add factories/builders/strategies unless the system actually needs them.
- Prefer readable direct control flow over clever generic abstractions.
- Keep small systems small.

## Verification

Before saying the task is complete:

- Check git diff.
- Run available compile/test command if practical.
- If Unity editor validation is required but not available, say so.
- List what was not verified.