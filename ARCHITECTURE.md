# PLOP! architecture

| System | Responsibility |
|---|---|
| Core | Immutable board geometry, mutable session state, shared transitions, Undo and Restart, validation and JSON |
| Solver | Shortest paths from arbitrary state and exhaustive graph analysis through Core |
| Generation | Offline seeded candidates, puzzle quality metrics, duplicate checks and the ordered production catalog |
| Level Lab | Editor tools for authoring, inspecting, replaying, filtering, ordering and verifying level packs |
| Unity | Gameplay scene, swipe input, rounded presentation, audio cues, asynchronous hints and local progression |
| Tests | Shared engine-free NUnit tests and Unity PlayMode integration |

The authority for every swipe is `MoveRules.Apply`. Runtime loads the ordered
Resources pack and renders engine events. Content-derived IDs identify puzzles
across renames or reordering. Local saves track completed IDs, unlocked IDs and a
resume ID. An explicit hint request sends a snapshot to a bounded worker, then
the controller displays the first direction of the verified route if that state
is still current. The board and audio receive the same swipe events. The HUD
uses shared shape and size tokens, and settings persist sound and music
volumes independently.

For assembly dependencies, data flow and performance details, see the
[technical architecture](Documentation/ARCHITECTURE.md). See
[PROJECT_STATE.md](PROJECT_STATE.md) for the current handoff.
