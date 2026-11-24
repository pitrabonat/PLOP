# PLOP! — Architecture

## Assemblies

| Assembly (asmdef) | Folder | Depends on | UnityEngine? | Ships in player? |
|---|---|---|---|---|
| `PLOP.Core` | `Assets/Scripts/Core` | — | **No** (`noEngineReferences`) | yes |
| `PLOP.Solver` | `Assets/Scripts/Solver` | Core | **No** | yes (hints and deadlock proofs) |
| `PLOP.Generation` | `Assets/Scripts/LevelGeneration` | Core, Solver | **No** | compiled in, never called at runtime |
| `PLOP.Meta` | `Assets/Scripts/Meta` | Core | **No** | yes (Dream Dust, records, daily rules) |
| `PLOP.Unity` | `Assets/Scripts/Unity` | Core, Solver, Meta, Input System, uGUI | yes | yes |
| `PLOP.LevelLab` | `Assets/Scripts/LevelLab/Editor` | Core, Solver, Generation | yes (Editor) | **no** (Editor platform only) |
| `PLOP.Tests` | `Assets/Tests/EditMode` | Core, Solver, Generation, Meta, NUnit | Editor | no |
| `PLOP.Tests.PlayMode` | `Assets/Tests/PlayMode` | Core, Solver, Meta, Unity, NUnit | yes | no (test assembly) |
| Assembly-CSharp-Editor | `Assets/Scripts/Editor` | auto-references | Editor | no |

Namespaces: `Plop.Core`, `Plop.Solver`, `Plop.Generation`, `Plop.Presentation` (assembly `PLOP.Unity`;
named to avoid shadowing the global `Unity.*` namespaces), `Plop.LevelLab`, `Plop.Tests`, `Plop.EditorTools`.

`DotNet/` contains plain .NET projects that compile the **same source files** of Core/Solver/Generation
(netstandard2.1, C# 9 — Unity 6's limits) for fast `dotnet test` runs and the generation benchmark.

## Core data model (`Plop.Core`)

- `GridPosition` — readonly int pair; origin bottom-left, Y up.
- `Direction` — `Up, Down, Left, Right` (+ `DirectionUtil` for `"UDLR"` strings).
- `LevelDefinition` — **immutable** geometry + start: size, walls, `PlopDefinition`s (id, color, start),
  `NestDefinition`s (color, position), optional developer `LevelMetadata`. Precomputes per-cell wall flags
  and nest colors. (The spec's "BoardState" is this immutable geometry part.)
- `GameState` — **mutable** and compact: one `int[]` (cell per Plop id) + a sleep bit mask.
  `Clone`, `CopyFrom` (allocation-free), exact `Equals`/`GetHashCode`.
- `StateCodec` — canonical 64-bit key: cells packed at `ceil(log2(cells))` bits; same-color Plops sorted
  (they are interchangeable); sleep flags derived. Used for hashing in the solver.
- `MoveRules.Apply(level, state, dir, events?, scratch?)` — **the** transition function (see GAME_RULES.md).
- `GameEngine` — session: `LoadLevel`, `LoadState` (arbitrary state, e.g. hints), `TryMove`, `Undo`,
  `Restart`, `GetState`/`CurrentState`, `IsCompleted`, `MoveCount`, `GetMoveHistory`, `EventRaised`.
  History stores state snapshots (exact undo).
- `MoveResult` — status (`Moved`/`NoOp`/`Ignored`), move count, completion flag, `GameEvent[]`.
- `GameEvent` — allocation-free struct (type, plop id, from, to, direction, block reason, blocker, move count).
- `LevelValidator` — structural validation of levels, states and packs (unique ids).
- `LevelSerializer` + `Json/MiniJson` — versioned JSON (see LEVEL_FORMAT.md). No UnityEngine/JsonUtility.
- `LevelText` — ASCII notation for tests/debugging.

## Solver (`Plop.Solver`) — see SOLVER.md

`BfsSolver.Solve(level, startState?, options)` and `StateSpaceAnalyzer.Analyze(level, budget)`.

## Generation (`Plop.Generation`) — see LEVEL_GENERATION.md

`SplitMix64`, `LevelGenerator`, `LevelVerifier`, `LevelCanonicalizer` + `DuplicateRegistry`,
`DifficultyEstimator`, `BatchGenerator`, `GeneratorProfiles`; Phase 8: `PuzzleInsight`, `QualitySearch`, `CampaignPlan`,
`ProductionCatalog` (campaign + daily pool), `CampaignInsights` (Level Lab overview). See PUZZLE_QUALITY.md.

## Meta progression (`Plop.Meta`)

Engine-free rules behind an `IKeyValueStore` (PlayerPrefs at runtime via `PlayerPrefsStore`, `MemoryStore` in tests
and debug previews) and an `IClock` (local calendar time; tests inject `FixedClock`).

- `LevelRecords`: best moves, Perfect flag and completions per level ID. Rejects a result below the verified optimum.
- `DreamDustWallet`: balance plus a set of one-time grant keys; `TryUnlock` spends on `CosmeticCatalog` items (data only).
- `CompletionRewards.ForLevel/ForDaily`, `DailyGift`, `DailySchedule` (deterministic local-date → pool index).

`PlopGameController` owns the wallet and records. It opens the game on `MainMenu` (own canvas, sorting order 50):
PLAY resumes `CampaignResumeIndex`, Daily Puzzle loads `PLOP_Daily_001` for the local date without touching campaign
progress, and Home returns to the menu (restoring the campaign board after a daily). A hint request marks the visit
as assisted, so it cannot be Perfect. Slot-history migration in `LevelProgress` runs before the resume index is read.

## Presentation (`Plop.Presentation`, assembly `PLOP.Unity`)

```
SwipeInput ──Direction──▶ PlopGameController ──TryMove──▶ GameEngine (Core)
GameHud ──Undo/Restart/Next/Hint──▶      │                          │
                                     ◀────── GameEvent ────────┘
                  BoardView.Play(events) / Sync(state)   PlopAudio.PlayMove(events)   GameHud
```

- `PlopGameController` owns the engine and routes input; contains **no rules**.
- `BoardView` builds the board from one runtime `ShapeAtlas` (rounded tiles, pebble walls, pillow Nests,
  9-sliced rounded container) and drives `PlopView`s (jelly Plops: squash & stretch, landing wobble, bump,
  idle breathing/blinking, sleep, completion hop) from `GameEvent`s in a single allocation-free `Update`;
  `Sync` snaps after undo/restart/load. See VISUAL_STYLE.md.
- `SwipeInput` — Input System callbacks (touch or mouse swipe, arrows/WASD, Z/Backspace, R, H, N/Enter).
- `GameHud` — uGUI built in code (rounded 9-sliced pills/card from `UiSprites`, `PressScale`), inside
  `Screen.safeArea`; text changes only on events. `RectChangeNotifier` reports screen/safe-area changes.
- `PlopAudio` maps swipe events to deduplicated synthesized cues and owns
  the independent effects/music sources. Settings changes go through the HUD
  and save their gains as local preferences. Playback suspends in the background.
- `HudTheme` and `HudIcons` share canvas geometry, typography and icons across the HUD and modal cards.
- `BoardLayout` — pure portrait tray fit with 6.25% side margins in the safe
  area. It fits the complete tray against both dimensions with one scale.
  The HUD places its badges and action dock relative to those bounds.
- `LevelLibrary` loads the ordered pack and its generated binary deadlock
  sidecar. `DeadlockCatalog` checks format, integrity, and geometry identity,
  then decodes a single level at selection. The complete 100-level sidecar
  uses 926,071 bytes on disk and exact canonical keys at runtime.
- `LevelProgress` — stores completed and unlocked level IDs and a resume ID in one pack-specific
  PlayerPrefs record. Completion unlocks the next puzzle immediately. Legacy positional saves
  preserve access through that position and resume the prior board when its ID survives. They
  grant zero historical completions because the previous Next shortcut could skip unsolved puzzles.
- `PlopPalette` — Soft Toy Minimalism colors and the gameplay-color → palette-slot mapping.
- `ShapeAtlas` / `UiSprites` / `Sdf` bake rounded sprites at screen-appropriate
  density. Nest seals and Plop stamps carry matching silhouettes on the
  same atlas as the board and character shading.
- `WorldCatalog` loads data-driven chapter ranges. `WorldBackdrop` renders a
  single authored image behind the tray and only animates briefly on entry.
  Board colors use sprite tints without multiplying atlas variants.
- Chapter progress is computed from existing level access and completion.
  The Resume action uses the highest unlocked catalog index independently
  of the last visited replay. Existing save keys and serialization stay valid.
- Reduced motion skips presentation tweens and idle motion. `PlopHaptics`
  requests one short native feedback cue per settlement or completion
  when the player has enabled it.

## Performance design

- The pack is precomputed. An explicit hint request schedules one cancelable
  BFS on a worker with a 300,000-state and 200 ms search budget. The worker
  verifies the proposed route through `GameEngine` from the same snapshot.
  Moving, undoing, restarting, changing levels or disabling gameplay invalidates
  the result. The controller only displays a direction for the unchanged board.
- A state-changing swipe checks the current level's index with an
  allocation-free binary search. Full graph traversal and reverse reachability
  use the same `MoveRules.Apply` as Core. Unknown states debounce to a
  20,000-state, 80 ms cancelable fallback. Completion, revision checks and a
  motion barrier keep old results and overlays off the new board.
- Gameplay and HUD updates are event-driven. `BoardView.Update` drives the
  toy animations. Idle rendering defaults to every third frame once visuals
  settle, with input processed at the selected 30 or 60 FPS.
- No physics or normal-gameplay generation. Frame-rate preference and progress
  save only on player actions. Development logging compiles out of release builds.
- Desktop solver and per-command measurements are available through
  `dotnet run --project DotNet/Plop.Bench -- --perf <pack>`.

## Extension points

- New tile mechanics (portals, ice, buttons): extend `LevelDefinition` (+ serializer schema v2) and
  `MoveRules.Apply`; solver/generator/tests automatically use the new rules. If a mechanic adds mutable
  state (e.g. toggled buttons), add it to `GameState` and `StateCodec` (and bump `SolverInfo.Version`).
- Art/animation: replace sprites in `ShapeAtlas` or `PlopView` (e.g. with authored character art) without
  touching gameplay; events already carry everything needed.
- Difficulty tuning: `DifficultyWeights` (weights + thresholds) — no code changes needed.
