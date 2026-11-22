# PLOP! architecture

| System | Responsibility |
|---|---|
| Core | Immutable board geometry, mutable session state, shared transitions, Undo and Restart, validation and JSON |
| Solver | Shortest paths from arbitrary state, exhaustive graph analysis through Core, and versioned exact deadlock proof data |
| Generation | Offline seeded candidates, puzzle quality metrics, duplicate checks and the ordered production catalog |
| Level Lab | Editor tools for authoring, inspecting, replaying, filtering, ordering and verifying level packs |
| Unity | Gameplay scene, portrait board, themed worlds, campaign navigation, audio and haptics, hints, recovery and progression |
| Tests | Shared engine-free NUnit tests and Unity PlayMode integration |

The authority for every swipe is `MoveRules.Apply`. Runtime loads the ordered
Resources pack and renders engine events. Content-derived IDs identify puzzles
across renames or reordering. Local saves track completed IDs, unlocked IDs and a
resume ID. An explicit hint request sends a snapshot to a bounded worker, then
the controller displays the first direction of the verified route if that state
is still current. The board and audio receive the same swipe events. The HUD
uses shared shape and size tokens, and settings persist sound and music
volumes independently. Portrait layout fits the full tray to safe-area
content bounds, and the rendering atlas bakes the tray, tiles, pillows
and translucent-looking toys under a consistent upper-left light.

Offline graph analysis marks the states that can reach any completion via
reverse traversal. The sidecar stores sorted exact keys for both known
classes and binds each set to the level geometry, state encoding and solver
version. Unity expands the indexed data for the current board and checks
state-changing commands. An unknown state can run one bounded background
search. The controller cancels stale work and checks the current state
revision before showing a recovery modal after movement settles.

WorldTheme assets map ordered catalog ranges to artwork, light and
ambience. WorldCatalog resolves the current level before rendering.
WorldBackdrop covers the camera using one opaque image and reuses the
gameplay camera. Board palette tints reuse its existing atlas. The
controller derives chapter access and progress from LevelProgress,
reveals a newly unlocked chapter once, and retains per-level replay.
Reduced motion changes presentation only. Haptic cues use Android View
feedback or UIKit on supported physical devices.

For assembly dependencies, data flow and performance details, see the
[technical architecture](Documentation/ARCHITECTURE.md). See
[PROJECT_STATE.md](PROJECT_STATE.md) for the current handoff.
