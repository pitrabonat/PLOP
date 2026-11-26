# PLOP! architecture

| System | Responsibility |
|---|---|
| Core | Immutable board geometry, mutable session state, shared transitions, Undo and Restart, validation and JSON |
| Solver | Shortest paths from arbitrary state, exhaustive graph analysis through Core, and versioned exact deadlock proof data |
| Generation | Offline seeded candidates, player-model puzzle insight, quality search, campaign plan, duplicate checks and the ordered production catalog and daily pool |
| Meta | Perfect Moves records, Dream Dust wallet with one-time grants, Daily Gift and Daily Puzzle rules, cosmetic catalog, ownership and presets (engine-free) |
| Characters | `CharacterArt` recipes, look-aware atlas painters, the shared `PlopView` rig (board and UI), move effects, Character Studio, content validator |
| Character tools | Editor-only Animation Lab, cosmetic validator and reference-sheet capture |
| Level Lab | Editor tools for authoring (including mechanic tiles and move limits), inspecting, replaying with an effect-resolution debugger, loop-risk checks, filtering, ordering and verifying level packs, plus the player-model campaign overview |
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

Phase 10 adds four tile mechanics inside the same rules function. `MoveRules.Apply` takes the usual one-cell
step, then resolves each Plop's forced chain in a fixed order (Lava, matching Nest, Loop, fan, Frost) under a
step budget, so one swipe is one move however far the chain goes. Lava failure lives in `GameState`, so Undo
and Restart recover exactly; the solver skips failed states and the analyzer routes fatal moves into one fail
sink. `ExpandedCampaign` builds 160 verified levels over nine Worlds with mechanic-aware quality search, and
Limited Moves challenges are solver-verified. Paid hints spend Dream Dust once per board state, only after a
verified route. The board shows forced chains as queued steps and keeps the input gate closed until they
finish. Level Lab places mechanics, sets move limits, debugs effect resolution and checks for loop risk.
See [Game rules](Documentation/GAME_RULES.md#phase-10-mechanics) and the
[technical architecture](Documentation/ARCHITECTURE.md#phase-10-mechanics-failure-limits-and-the-hint-economy).

Phase 11 adds measurement and balancing, all offline. `PuzzleQuality` scores a board from its full state graph:
decisions, coordination, settlement, a mechanic-removal test, planning depth, misleading choices, efficiency and
solution diversity. `SimulatedPlayers` adds Random, Greedy, Look-ahead and Limited-information players.
`QualityAudit` audits a whole pack. `BalancedCampaign` rebuilds the campaign (`PLOP_Pack_003`) and the Daily
pool (`PLOP_Daily_002`) deterministically against per-stage `QualityFloors`, keeping strong levels by id.
`LevelProgress` migrates Phase 10 saves by id and old frontier. In `Plop.Meta`, `HintPolicy` (hint grace),
`DailySchedule.PickIndex` (mechanic-gated Daily), `EconomySimulator` (offline Dust model) and `PlaytestLog`
(dev-only local CSV) are engine-free, so .NET tests cover them. The Level Lab "Balance & quality" view puts
the audit in the Editor. See [Phase 11 reports](Documentation/Phase11/).
System Back (Android back gesture, Escape) arrives through SwipeInput's `Dismiss` action, so a press shorter than a
frame still counts. `PlopGameController.HandleBack` closes the top screen: Studio, then any HUD modal
(`GameHud.HandleBack`), then a level returns to the menu, and the menu sends the app to the background.

For assembly dependencies, data flow and performance details, see the
[technical architecture](Documentation/ARCHITECTURE.md). See
[PROJECT_STATE.md](PROJECT_STATE.md) for the current handoff.
