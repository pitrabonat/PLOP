# PLOP! architecture

| System | Responsibility |
|---|---|
| Core | Immutable board geometry, mutable session state, shared transitions, Undo and Restart, validation and JSON |
| Solver | Shortest paths from arbitrary state, exhaustive graph analysis through Core, and versioned exact deadlock proof data |
| Generation | Offline seeded candidates, player-model puzzle insight, quality search, campaign plan, duplicate checks and the ordered production catalog and daily pool |
| Meta | Perfect Moves records, Dream Dust wallet with one-time grants, Daily Gift and Daily Puzzle rules, cosmetic catalog, ownership and presets (engine-free) |
| Characters | `CharacterArt` recipes, look-aware atlas painters, the shared `PlopView` rig (board and UI), move effects, Character Studio, content validator |
| Character tools | Editor-only Animation Lab, cosmetic validator and reference-sheet capture |
| Level Lab | Editor tools for authoring, inspecting, replaying, filtering, ordering and verifying level packs, plus the player-model campaign overview |
| Unity | Main menu, gameplay scene, portrait board, themed worlds, campaign navigation, audio and haptics, hints, recovery and progression |
| Tests | Shared engine-free NUnit tests and Unity PlayMode integration |

The authority for every swipe is `MoveRules.Apply`. Runtime loads the ordered
Resources pack and renders engine events. Content-derived IDs identify puzzles
across renames or reordering. Local saves track completed IDs, unlocked IDs and a
resume ID. An explicit hint request sends a snapshot to a bounded worker, then
the controller displays the first direction of the verified route if that state
is still current. The board and audio receive the same swipe events. A touch or mouse
release must produce a dominant direction. The presentation can hold
one direction until slide travel completes. Recovery, navigation and
suspension clear that direction. The HUD uses shared shape and size
tokens. Master mute preserves the separate sound and ambience gains. Portrait layout fits the full tray to safe-area
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
Reduced motion changes presentation only and bypasses the travel
wait. Short audio cues share three voices, and two bed sources
crossfade authored world loops. App focus and suspension govern
playback. Opt-in haptics use Android View feedback or UIKit on
supported physical devices.

Phase 8 adds a quality layer and meta progression. `PuzzleInsight` replays each board with a greedy player and an
exact random-swipe model to measure traps, counter-intuitive steps and wasted space; `QualitySearch` hill-climbs
boards toward a `CampaignPlan` slot target. Every level stores its verified `optimalMoves`, the Perfect target.
The game opens on the main menu; Dream Dust is paid once per grant key for completions, Perfects, the Daily Gift
and the Daily Puzzle. A slot-history file maps rebuilt level IDs so saves migrate instead of resetting. See
[Puzzle quality](Documentation/PUZZLE_QUALITY.md).

Phase 9 separates gameplay identity from appearance. A `CharacterLook` (resolved from the equipped preset)
selects body style, finish, palette, face, accessory, Nest style and move effect. The atlas bakes that look
beside the board, so nothing adds materials or draw-call groups. `PlopView` follows board endpoints exactly
and adds expressions, personality and reactions on child transforms only. Purchases spend Dust and record
ownership in one wallet write. See [Characters and cosmetics](Documentation/CHARACTERS.md).

For assembly dependencies, data flow and performance details, see the
[technical architecture](Documentation/ARCHITECTURE.md). See
[PROJECT_STATE.md](PROJECT_STATE.md) for the current handoff.
