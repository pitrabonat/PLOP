# PLOP! — Architecture

## Assemblies

| Assembly (asmdef) | Folder | Depends on | UnityEngine? | Ships in player? |
|---|---|---|---|---|
| `PLOP.Core` | `Assets/Scripts/Core` | — | **No** (`noEngineReferences`) | yes |
| `PLOP.Solver` | `Assets/Scripts/Solver` | Core | **No** | yes (for future hints) |
| `PLOP.Generation` | `Assets/Scripts/LevelGeneration` | Core, Solver | **No** | compiled in, never called at runtime |
| `PLOP.Unity` | `Assets/Scripts/Unity` | Core, Input System, uGUI | yes | yes |
| `PLOP.LevelLab` | `Assets/Scripts/LevelLab/Editor` | Core, Solver, Generation | yes (Editor) | **no** (Editor platform only) |
| `PLOP.Tests` | `Assets/Tests/EditMode` | Core, Solver, Generation, NUnit | Editor | no |
| `PLOP.Tests.PlayMode` | `Assets/Tests/PlayMode` | Core, Solver, Unity, NUnit | yes | no (test assembly) |
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
`DifficultyEstimator`, `BatchGenerator`, `GeneratorProfiles`.

## Presentation (`Plop.Presentation`, assembly `PLOP.Unity`)

```
SwipeInput ──Direction──▶ PlopGameController ──TryMove──▶ GameEngine (Core)
GameHud ──Undo/Restart/Next──▶      │                          │
                                     ◀────── GameEvent ────────┘
                         BoardView.Play(events) / Sync(state)   GameHud.SetMoves/SetCompleted
```

- `PlopGameController` owns the engine and routes input; contains **no rules**.
- `BoardView` builds the board from one runtime `ShapeAtlas` (rounded tiles, pebble walls, pillow Nests,
  9-sliced rounded container) and drives `PlopView`s (jelly Plops: squash & stretch, landing wobble, bump,
  idle breathing/blinking, sleep, completion hop) from `GameEvent`s in a single allocation-free `Update`;
  `Sync` snaps after undo/restart/load. See VISUAL_STYLE.md.
- `SwipeInput` — Input System callbacks (pointer press/release swipe, arrows/WASD, Z/Backspace, R, N/Enter).
- `GameHud` — uGUI built in code (rounded 9-sliced pills/card from `UiSprites`, `PressScale`), inside
  `Screen.safeArea`; text changes only on events. `RectChangeNotifier` reports screen/safe-area changes.
- `BoardLayout` — pure portrait layout math: fits the board between header and footer in the safe area.
- `LevelLibrary` — loads `Resources/Levels/PLOP_Pack_001.json` without the dev block.
- `PlopPalette` — Soft Toy Minimalism colors and the gameplay-color → palette-slot mapping.
- `ShapeAtlas` / `UiSprites` / `Sdf` — SDF-rasterized rounded sprites at screen-appropriate density.

## Performance design

- No generation or solving during gameplay; the pack is precomputed.
- Gameplay is event-driven; per frame there is only (a) the ~0.12 s move interpolation coroutine while a move
  animates and (b) a constant-time idle check that lowers `OnDemandRendering.renderFrameInterval`
  (default: render every 3rd frame when idle; input still processed every frame).
- `Application.targetFrameRate` 60 or 30 (`PlopGameController.SetFrameRateMode`), `vSyncCount = 0`.
- No physics; no per-frame allocations in gameplay code. `TryMove` allocates ~170 B per **command**
  (history snapshot + event array), measured with `dotnet run --project DotNet/Plop.Bench -- --perf …`.
- Development logging (`PlopLog.Info`) is compiled out of release players (`[Conditional]`).
- Solver hint support: `BfsSolver.Solve` accepts `CancellationToken` / time / state budgets so it can run
  on a worker thread (`Task.Run`) without blocking gameplay. Not wired to UI yet.

## Extension points

- New tile mechanics (portals, ice, buttons): extend `LevelDefinition` (+ serializer schema v2) and
  `MoveRules.Apply`; solver/generator/tests automatically use the new rules. If a mechanic adds mutable
  state (e.g. toggled buttons), add it to `GameState` and `StateCodec` (and bump `SolverInfo.Version`).
- Art/animation: replace sprites in `ShapeAtlas` or `PlopView` (e.g. with authored character art) without
  touching gameplay; events already carry everything needed.
- Hints: run `BfsSolver.Solve(level, engine.CurrentState)` off the main thread.
- Difficulty tuning: `DifficultyWeights` (weights + thresholds) — no code changes needed.
