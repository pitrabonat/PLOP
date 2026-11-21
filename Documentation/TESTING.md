# Testing

The same NUnit test files run in two environments:

| Environment | What runs | Command |
|---|---|---|
| **dotnet** (fast loop, seconds) | `Assets/Tests/EditMode/*.cs` against Core/Solver/Generation compiled as netstandard2.1, C# 9 | `export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet; dotnet test DotNet/Plop.Tests/Plop.Tests.csproj` |
| **Unity EditMode** | same files as assembly `PLOP.Tests` | `Unity -batchmode -nographics -projectPath <PLOP> -runTests -testPlatform EditMode -testResults <file.xml>` |
| **Unity PlayMode** | `Assets/Tests/PlayMode` (`PLOP.Tests.PlayMode`): controller + engine + BoardView + HUD integration | `Unity -batchmode -projectPath <PLOP> -runTests -testPlatform PlayMode -testResults <file.xml>` |

(`Unity` = `/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity`; close the Editor first.)
Tests use only NUnit features available in Unity's NUnit 3.5 fork.

## Latest results (2026-11-21, executed)

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| dotnet test (net10.0) | 92 | 92 | 0 | 0 |
| Unity 6000.3.29f1 EditMode | 92 | 92 | 0 | 0 |
| Unity 6000.3.29f1 PlayMode | 22 | 22 | 0 | 0 |

An earlier mutation check (inverting the front-to-back ordering rule) made 4 rule tests fail, confirming the tests
detect rule regressions; the code was restored afterwards.

## Coverage map

| Requirement | Tests |
|---|---|
| Single / multiple Plop movement, coordinates | `CoreRulesTests.SinglePlop_*`, `Coordinates_*`, `MultiplePlops_*` |
| Walls, grid edge, partial blocking | `Wall_BlocksMovement_AndIsNoOp`, `GridEdge_BlocksMovement`, `PartialBlock_*` |
| Plop–Plop collision, movement ordering | `Ordering_*` (follow, blocked chain, up-order, head-on, id independence) |
| Matching / non-matching Nests, sleeping obstacles, completion | `MatchingNest_*`, `NonMatchingNest_IsTraversable`, `SleepingPlop_*`, `CompletedLevel_*` |
| Undo / Restart | `Undo_RestoresExactPreviousState`, `Undo_AfterCompletion_Works`, `Restart_*` |
| Events | `Events_AreRaisedForPresentation` |
| Determinism | `Simulation_IsDeterministic`, `Generator_SameSeed_SameLevel`, benchmark re-run |
| State equality / hashing / canonical key | `StateEquality_AndClone`, `StateCodec_*` |
| Serialization (level, pack, state, runtime vs dev data, invalid JSON) | `CoreDataTests.*Json*` |
| Invalid level rejection | `CoreDataTests.InvalidLevel_*`, `Engine_RejectsInvalidLevel`, `Pack_DuplicateIds_Rejected`, `ValidateState_*` |
| Randomized invariants | `Randomized_Invariants_*` (no overlaps/walls, ≤1 cell per move, sleepers static, no-op ⇒ unchanged, undo-all ⇒ initial), `Randomized_PlopIdPermutation_*` |
| Solver shortest path | `SolverTests.Solves_*`, `Randomized_BfsMatchesIndependentIddfs` |
| Solver unsolvable / budget / cancellation / invalid | `ProvenUnsolvable_*`, `BudgetExhaustion_*`, `Cancellation_*`, `InvalidLevel_Reported` |
| Hints from arbitrary states | `SolvesFromArbitraryState_ForHints`, `AlreadySolvedState_*` |
| Generator reproducibility, validity, diagnostics, budget, target difficulty | `GenerationTests.Generator_*`, `GeneratedLevels_*` |
| PRNG | `SplitMix64_MatchesReferenceValues`, `SplitMix64_NextInt_InRange` |
| Duplicate detection (8 symmetries + recolor, rules preserved) | `Duplicates_DetectedUnderAllSymmetriesAndRecoloring`, `Generator_WithRegistry_*` |
| Difficulty and graph-backed quality | `Difficulty_*`, `PuzzleQualityTests` |
| Verified solution replay / shipped pack | `Batch_ProducesUniqueVerifiedLevels`, `ShippedLevelPack_IsValidAndVerified` |
| Gameplay integration | `GameplayPrototypeTests` (movement, completion, unlocks, replay, persistence, migration, input and current-state hint cancellation) |
| Full catalog integration | `ProductionCatalogTests` loads every board from Resources, solves, Undoes, Restarts, replays and advances through the controller |
| Visual foundation | `VisualFoundationTests.*`: corner radii within the style guide, tile/pebble outlines geometrically rounded, one shared atlas texture (batching), density buckets, 9-sliced panel/UI, layout fits 7 screen profiles × 9 board sizes inside the free safe area |

## Android smoke test

The ARM64 IL2CPP development APK installed on the API 37 emulator. In airplane
mode, a touch swipe solved Level 1, Next unlocked Level 2, and a force-stop
and relaunch preserved completion, the resume level, and the selected 30 FPS
cap. The Levels picker replayed Level 1. After a deliberate detour, the
hint changed from right to down.

## Not covered by automated tests

- The Level Lab window UI (compiles in Unity; its logic calls tested Core/Solver/Generation APIs) —
  needs a manual check in the Editor.
- Real touch input on devices and visual appearance of the scene (PlayMode tests call controller methods).
- Device performance profiling (desktop measurements are recorded in the [production benchmark](benchmarks/generation_benchmark_v2.md)).
