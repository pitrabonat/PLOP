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
| Unity 6000.3.29f1 PlayMode | 29 | 29 | 0 | 0 |

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
| Visual production | `VisualFoundationTests` covers rounded silhouettes, shared atlas, density, nine-slicing, 7 screen profiles × 9 board sizes, anticipation, rapid retargeting, recoil, and clearing the sleep pose |
| Settings and completion | `ProductionHudTests` covers title glyphs, modal input capture, round slider handles at zero, independent audio sliders, 30/60 FPS control and immediate Next Level |
| Audio | `PlopAudioTests` covers synthesized clips and independence of the effects/music controls |

## Phase 4 iOS Simulator

Unity exported ARM64 and Xcode 26.3 built the development player with
simulator signing disabled. The iPhone 15 Pro Max ran Level 29 and replayed
its nine-move solution. The captured portrait images show the corrected
heading, safe-area layout, sleeping Plops and **Sweet Dreams!** completion.
The iPhone SE also displayed Level 29 in a 750 × 1334 portrait screenshot.
[Running game previews](Previews/README.md)

## Phase 4 Android emulator

The ARM64 IL2CPP development APK installed on the API 37 emulator
(1080 × 2400). In airplane mode, a detour and Hint showed a route from
the current board. Undo and a right swipe finished Level 1 in one move
with both audio sliders at 0%. Next Level opened Level 2. The resumed
game preserved the completed and unlocked levels, mute settings and the
selected 30 FPS cap after a force-stop. The Settings screenshot shows
the labels remain clear at minimum volume.

The APK is 44,646,700 bytes (29% smaller than the Phase 3 dev APK).
Both its APK v2 signature and 16 KB ZIP alignment passed verification.
`dumpsys meminfo` measured 255,875 KiB of PSS after emulator relaunch.
SurfaceFlinger TimeStats sampled the game layer during repeated swipes.
The pre-slider-fix rehearsal with no other build running showed median
intervals of 33 ms in 30 FPS mode and 24 ms in the larger 60 FPS sample.
The final APK showed longer intervals while the host was also building iOS
(66 ms at 30 FPS and 46–50 ms at 60 FPS). These samples describe
emulator pacing under the observed host load.
Build, test and Android platform logs are under
`/Volumes/PLOP_DEV/Builds/Logs/phase4/`.

## Not covered by automated tests

- The Level Lab window UI (compiles in Unity; its logic calls tested Core/Solver/Generation APIs) —
  needs a manual check in the Editor.
- Physical phone input, frame pacing, battery, thermals and audio mix. The emulator verifies the touch flow and settings.
- The [production benchmark](benchmarks/generation_benchmark_v2.md) records desktop puzzle-engine measurements.
