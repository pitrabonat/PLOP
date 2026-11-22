# Testing

The same NUnit test files run in two environments:

| Environment | What runs | Command |
|---|---|---|
| **dotnet** (fast loop, seconds) | `Assets/Tests/EditMode/*.cs` against Core/Solver/Generation compiled as netstandard2.1, C# 9 | `export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet; dotnet test DotNet/Plop.Tests/Plop.Tests.csproj` |
| **Unity EditMode** | same files as assembly `PLOP.Tests` | `Unity -batchmode -nographics -projectPath <PLOP> -runTests -testPlatform EditMode -testResults <file.xml>` |
| **Unity PlayMode** | `Assets/Tests/PlayMode` (`PLOP.Tests.PlayMode`): controller + engine + BoardView + HUD integration | `Unity -batchmode -projectPath <PLOP> -runTests -testPlatform PlayMode -testResults <file.xml>` |

(`Unity` = `/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity`; close the Editor first.)
Tests use only NUnit features available in Unity's NUnit 3.5 fork.

## Latest results (2026-11-22, executed)

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| dotnet test (net10.0) | 98 | 98 | 0 | 0 |
| Unity 6000.3.29f1 EditMode | 98 | 98 | 0 | 0 |
| Unity 6000.3.29f1 PlayMode | 51 | 51 | 0 | 0 |

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
| Portrait fit | `ResponsivePortraitTests` checks 1080×1920, 1170×2532, 1440×3200 and 720×1280, plus asymmetric insets and tall or wide boards |
| Deadlock proofs | `DeadlockCatalogTests` covers exact solvable and proven-deadlocked states, incomplete graphs, unknown states, geometry changes and corrupt data |
| Recovery UI | `DeadlockRecoveryTests` checks production Level 8’s movable trap, Undo, Restart, dismissal, a bounded search, completion priority, superseded swipes and a pending result during level changes |

## Phase 6 campaign and presentation

Strict production verification passed for all 100 original levels.
PlayMode exercised all four chapter boundaries, the World picker,
locked rows, the campaign frontier after replay, preference controls,
five independent ambience profiles, symbol legibility and reduced
motion. The existing hint, recovery and catalog replay suites passed.

Unity's ProfilerRecorder sampled Level 100 with a 720×1280 render
target in Editor PlayMode. The board and backdrop reported 87 draw calls,
87 batches and 86 active sprites in the sampled frame. This measures
the desktop test scene before HUD rendering. The per-density board
atlas has a maximum 8 MiB texture at 256 pixels per unit. World
backdrops render as one sprite and their compressed footprint depends
on the player format.

### Android development player

The signed ARM64 IL2CPP build is
`/Volumes/PLOP_DEV/Builds/Android/Phase6/PLOP-phase6-dev.apk`.
The APK is 45,166,348 bytes. The v2 signature and 16 KB ZIP alignment
passed verification. Its SHA-256 is
`f154fd43f164fc1851d97c87464a2f3d7221dfb3809f7e930641e2170447a043`.

In an isolated API 37 emulator with airplane mode enabled, a Phase 5
touch-play save completed Level 1 and advanced to Level 2. Installing
Phase 6 over it with `adb install -r` preserved the same resume,
completed and unlocked level IDs. Play then advanced to Level 3. Both
haptics and reduced motion stayed enabled after a force-stop and
cold launch. This checks settings persistence. Tactile feedback
requires a physical phone.

A separate campaign fixture put the player at Level 20. Four touch
swipes solved the level, unlocked Dewdrop Garden and let Next Level
open Level 21. A second fixture at Level 41 checked that a locked
World ignored a tap, an available World supported replaying Level 21
and Resume returned to Level 41 with the correct progress. The
[preview set](Previews/Phase6/README.md) contains live gameplay from
all five Worlds.

SurfaceFlinger TimeStats recorded the Level 2 game surface during
touch play at 1080×1920. The selected cap was confirmed in preferences
for each sample.

| Selected cap | Present intervals | Median | 90th percentile |
|---|---:|---:|---:|
| 60 FPS | 103 | 36 ms | 54 ms |
| 30 FPS | 99 | 36 ms | 58 ms |

The emulator fell short of sustained 60 FPS. At the 30 FPS cap,
`dumpsys meminfo` measured 268,137 KiB PSS and 390,360 KiB RSS.
Frame pacing, audio mix, battery and thermals on physical phones
remain unmeasured. Raw results are under
`/Volumes/PLOP_DEV/Builds/Logs/phase6/`.

### iOS compatibility

Unity exported the ARM64 simulator player. Xcode 26.3 compiled the
UIKit haptics bridge and built the player with simulator signing
disabled. The iPhone 15 Pro Max simulator displayed Level 100 and
reproduced Level 8's recovery at 1290×2796. The game controls and
recovery actions fit below the Dynamic Island. Signed installation
and haptic feel on physical iOS hardware remain unverified.

## Phase 5 level verification

Strict production catalog verification passed. The deadlock sidecar regenerated
byte-for-byte at a deterministic 100,000-state budget per level. Full analysis
classified 913,919 states across all 100 levels. It found 10,490 proven dead
ends across 51 levels. The largest graph has 77,196 states.
The game rules and the production pack match the Phase 4 checkpoint.

## Phase 5 Android development player

The signed ARM64 APK is at
`/Volumes/PLOP_DEV/Builds/Android/Phase5/PLOP-phase5-dev.apk`.
It is 44,948,830 bytes, uses APK signature scheme v2, and passed the
16 KB ZIP alignment check. The API 37 emulator installed and loaded
the native player and its proof tables for all 100 levels. ADB showed
the emulator as the sole device during the run.

Android portrait captures were inspected at 1080×1920, 1170×2532,
1440×3200 and 720×1280, including the 7×8 tray of Level 100. A touch swipe
triggered Level 8 recovery. Undo restored the previous one-move state,
Keep playing retained the deadlocked two-move state, and Restart restored
zero moves. A normal save run advanced to Level 2 and retained the
level, the 30 FPS preference and both muted volumes after relaunch.
The final APK rendered the corrected **Sweet Dreams!** heading after
Level 29’s nine-move solution and advanced to Level 30 by touch.

SurfaceFlinger TimeStats recorded the game surface while swiping in Level 1
at 1080×1920 on the first Phase 5 build. Unity’s Settings and stored cap were
checked for each sample. The later completion heading change is confined
to the end-of-level card.

| Selected cap | Present intervals | Median | 90th percentile |
|---|---:|---:|---:|
| 30 FPS | 99 | 33 ms | 36 ms |
| 60 FPS | 113 | 33 ms | 50 ms |

The emulator did not sustain the 60 FPS target. At 30 FPS, `dumpsys meminfo`
reported 265,627 KiB PSS and 391,088 KiB RSS. These readings describe the
hosted development player. Physical-device frame pacing, power draw,
temperature and audio mixing remain unmeasured. Raw logs are under
`/Volumes/PLOP_DEV/Builds/Logs/phase5/`.

## Phase 5 iOS compatibility

Unity exported the Phase 5 ARM64 iOS Simulator project. Xcode 26.3 built
and ran it on the iPhone 15 Pro Max simulator with simulator signing disabled.
Level 8 reproduced the recovery card at 1290×2796 with all three actions
within the safe area. This compatibility build preceded the Android
completion-heading correction. The modal scrim follows the safe-area bounds
on this profile. Signed physical iOS installation was outside this check.

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
