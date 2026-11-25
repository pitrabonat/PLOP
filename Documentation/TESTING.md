# Testing

The same NUnit test files run in two environments:

| Environment | What runs | Command |
|---|---|---|
| **dotnet** (fast loop, seconds) | `Assets/Tests/EditMode/*.cs` against Core/Solver/Generation compiled as netstandard2.1, C# 9 | `export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet; dotnet test DotNet/Plop.Tests/Plop.Tests.csproj` |
| **Unity EditMode** | same files as assembly `PLOP.Tests` | `Unity -batchmode -nographics -projectPath <PLOP> -runTests -testPlatform EditMode -testResults <file.xml>` |
| **Unity PlayMode** | `Assets/Tests/PlayMode` (`PLOP.Tests.PlayMode`): controller + engine + BoardView + HUD integration | `Unity -batchmode -projectPath <PLOP> -runTests -testPlatform PlayMode -testResults <file.xml>` |

(`Unity` = `/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity`; close the Editor first.)
Tests use only NUnit features available in Unity's NUnit 3.5 fork.

## Phase 10 results (2026-11-25, executed)

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| dotnet test (net10.0) | 179 | 179 | 0 | 0 |
| Unity 6000.3.29f1 EditMode | 179 | 179 | 0 | 0 |
| Unity 6000.3.29f1 PlayMode | 100 | 100 | 0 | 0 |
| `Bench --verify-production` on `PLOP_Pack_002.json` | 160 levels | verified | | |

Phase 10 coverage:

| Requirement | Tests |
|---|---|
| Frost slides until a wall, the edge, a Plop or a matching Nest; one swipe = one move | `MechanicsTests.Frost_*`, `MoveLimit_ForcedMovementCostsNoExtraMoves` |
| Lava fails the attempt (step, slide, push), the first hazard in order wins, Undo and Restart recover exactly | `Lava_*`, PlayMode `Lava_ShowsAFriendlyCard_AndUndoRecoversWithoutPenalty`, `Lava_RestartFromTheCard` |
| Breeze pushes by power, stops before walls, redirects slides, facing fans terminate (no chain pushing) | `Breeze_*` |
| Loops teleport to the pair keeping direction, an occupied exit cancels, one teleport per command | `Loop_*` |
| Deterministic precedence and bounded resolution; random mechanic boards; loop-risk inspection of the shipped pack | `RandomMechanicBoards_AreDeterministic_Bounded_AndOverlapFree`, `ForcedChainInspectorTests.*` |
| Solver avoids Lava and counts swipes; analyzer fail sink; codec stores live states only | `Solver_AvoidsLava_AndCountsSwipesNotCells`, `Analyzer_HasOneFailureSink_ThatIsNeverAState`, `StateCodec_StoresOnlyLiveStates` |
| Validation, schema 2 round trip (classic levels stay schema 1), canonical IDs through symmetry | `Validator_RejectsBrokenMechanics`, `Serialization_RoundTripsTilesAndLimits_AndKeepsClassicSchema`, `CanonicalId_*` |
| Quality metrics and the 160-level campaign: verified optimum, Worlds in order, early onboarding, every mechanic World uses its mechanic, levels 64+ not greedy-solvable, challenges = optimal + 2, deterministic slot search, Phase 8 IDs reused | `Insight_CountsMechanicSteps`, `Phase10CampaignTests.*` |
| Limited Moves: counter, out-of-moves card, Undo restores a move | `MoveLimit_EndsTheAttempt_AndUndoRestoresTheCounter`, PlayMode `LimitedMoves_ShowsTheLimit_AndTheOutOfMovesCard` |
| Paid hints: once per board state, never without Dust, survives relaunch, free on intros | `HintEconomyTests.*`, PlayMode `PaidHint_ChargesOncePerBoard_AndNeverWithoutDust`, `IntroHints_AreFree` |
| Presentation: every mechanic drawn from the shared atlas with shape cues, forced chain plays to the logical cell and holds the input gate, Reduced Motion lands at once | `Board_DrawsEveryMechanic_WithShapeCues`, `ForcedChain_PlaysToTheLogicalCell_AndHoldsTheInputGate`, `ReducedMotion_LandsTheWholeChainAtOnce` |
| Board decorations: catalog, old saves fall back, atomic purchase, tint only (puzzle untouched) | `CosmeticTests.BoardSlot_*`, `ShippedCatalog_SellsBoardDecorations`, PlayMode `BoardDecor_IsBoughtAtomically_AndOnlyTintsTheTray` |
| Save migration keeps unchanged puzzles and access, never invents completions | PlayMode `Migration_KeepsUnchangedPuzzlesAndAccess_ButNeverInventsCompletions`, `SavedProgress_CarriesOver_ForUnchangedPuzzles` |
| The shipped scene loads Pack 002 (found on the device build) | `GameplayScene_LoadsTheShippedPack` |
| Updated for 160 levels / nine Worlds | `WorldAssetsTests`, `WorldCampaignTests`, `WorldHudTests`, `ProductionCatalogTests`, `DeadlockRecoveryTests` (finds a shipped trap with the solver) |

Not covered automatically: phone frame time for long forced chains, and human difficulty of the new Worlds.
`DeadlockRecoveryTests.ANewSwipeSupersedesTheOldAnalysis` waits 5 s of real time for a background search; it timed out once while
an APK build and the emulator loaded the Mac, then passed in isolation (twice) and in the final full run.

## Phase 9 results (2026-11-25, executed)

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| dotnet test (net10.0) | 134 | 134 | 0 | 0 |
| Unity 6000.3.29f1 EditMode | 134 | 134 | 0 | 0 |
| Unity 6000.3.29f1 PlayMode | 89 | 89 | 0 | 0 |
| Cosmetic validator (batch) | 36 items | 0 problems | | |

Phase 9 coverage:

| Requirement | Tests |
|---|---|
| Catalog validation (duplicates, categories, prices, defaults, compatibility, unregistered assets and previews), stable shipped IDs | `CosmeticTests.ShippedCatalog_*`, `ShippedIds_AreStable`, `Validator_*` |
| Preset serialization and relaunch, removed/unknown items fall back, corrupt appearance kept aside, Phase 8 save migration | `Preset_RoundTrips_*`, `RemovedOrUnknownCosmetics_*`, `CorruptAppearance_*`, `Phase8Save_*` |
| Unlock eligibility, insufficient Dust, duplicate unlocks, failed saves roll back, torn writes recover from the backup, milestones grant once, equip rules | `Unlock_*`, `DuplicateUnlock_*`, `FailedSave_*`, `TornWrite_*`, `Milestones_*`, `Equip_*` |
| Shipped content end to end (recipes, attachments, palettes, finishes, expressions, thumbnails) | `Phase9CharacterTests.ShippedCosmeticContent_HasNoProblems`, `EveryCatalogAsset_*` |
| Cosmetics never change logic; logical colours preserved; Undo/Restart exact; sleeping face and wake on Undo; look survives restart; milestones from progress | `Cosmetics_NeverChangeLogic_*`, `UndoAndRestart_*`, `Sleeping_*`, `EquippedLook_SurvivesARestart`, `Milestones_*` |
| Studio navigation, preview of locked items, two-tap unlock, equip, return to menu, look applied to board and menu, preview released | `Studio_*`, `StudioPreview_*` |
| Reduced Motion, effect pool bounds and Battery Saver, interrupted animation, one texture/material, no per-frame allocation, faces attached to the body | `ReducedMotion_*`, `MoveEffects_*`, `InterruptedReactionAndMove_*`, `Board_RendersFromOneTexture_*`, `RigTick_*`, `StyleFaces_*` |

The allocation test uses `GC.GetAllocatedBytesForCurrentThread` when the runtime supports it, otherwise managed
heap growth (Unity's Mono in the Editor uses the fallback; measured 0 bytes over 1,000 frames).

## Phase 8 results (2026-11-25, executed)

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| dotnet test (net10.0) | 121 | 121 | 0 | 0 |
| Unity 6000.3.29f1 EditMode | 121 | 121 | 0 | 0 |
| Unity 6000.3.29f1 PlayMode | 74 | 74 | 0 | 0 |

Phase 8 coverage:

| Requirement | Tests |
|---|---|
| Perfect = optimal, hint disqualifies, best result only improves and persists, below-optimum rejected, Undo cannot exploit the counter | `MetaProgressionTests.Perfect_*`, `BestResult_*`, `MovesBelowOptimum_*`, `UndoCannotExploitTheCounter` |
| Dream Dust once per key, survives relaunch, corrupt saves kept aside, cosmetics data model | `Wallet_*`, `LevelRewards_*`, `CorruptSaves_*`, `Cosmetics_*` |
| Daily Gift per local calendar day, no streak penalty, clock rollback; Daily schedule determinism and coverage; daily rewards once per date | `DailyGift_*`, `DailySchedule_*`, `DailyPuzzle_*` |
| Every shipped `optimalMoves` = BFS optimum, kept at runtime; quality floor (26+ not greedy-solvable, ≥ 70 trap levels, 51+ counter-intuitive); tutorial IDs stable and slot history complete; daily pool verified and unique; QualitySearch determinism; PuzzleInsight separates trivial from demanding | `CampaignQualityTests.*` |
| Level Lab analysis: trivial flags, first-swipe alternatives, settlement order, symmetry-aware similarity, overview table | `CampaignInsightsTests.*` |
| Menu blocks swipes and PLAY resumes the frontier; Perfect badge and Dust once; slow solve then improve; hinted solve not Perfect; Daily Puzzle once per day and returns to menu; Daily Gift once per local day; save migration of rebuilt slots; small boards use most of the width | `Phase8ProgressionTests.*` (PlayMode) |
| Existing flows behind the title menu (shipped pack opens on the menu; tests press PLAY) | `DeadlockRecoveryTests`, `WorldCampaignTests` |

## Phase 7 results (2026-11-22, executed)

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

## Phase 7 baseline and regression coverage

The Phase 6 checkpoint passed 98 .NET, 98 Unity EditMode and 51
Unity PlayMode tests on the mounted checkout. An offline API 37
Android emulator reproduced rejected 48 px swipes and accepted
near-diagonal 180×175 px swipes at 1080×1920. A 610 px swipe
registered once. Swiping from Hint left the board alone. Cold launch,
Home and return, and screen off and wake preserved the original
Level 3 save and settings.

The Phase 7 candidate passed 98 .NET, 98 EditMode and 66 PlayMode
tests. Added or expanded coverage includes real Input System events,
the single pending command, rapid restoration, recoil, pause,
persistent audio controls, crossfade and absolute cue priority.
The tests also cover verified directional hints, reduced motion,
unchanged-board deadlock deduplication and world progression.

Strict production verification passed all 100 levels. Their serialized
data, original solutions, Core transitions and deadlock table match
`checkpoint/pre-phase7`. The [campaign playtest checklist](PHASE7_CAMPAIGN_PLAYTEST.md)
records potential difficulty outliers and a plan for blind playtesting.

At 720×1280, Unity ProfilerRecorder reported 87 board-and-backdrop
draw calls, 87 batches and 86 active sprites on Level 100 in both
the Phase 6 baseline and Phase 7 Editor PlayMode. It measures the
board scene before HUD drawing. Awake Plops skip redundant stationary
face and root transforms. That is a reduction in setter calls from
code-path inspection. Frame and time counters need a device sample.

On the Phase 6 APK, SurfaceFlinger sampled 18 swipes per mode on
Level 2 at 1080×1920 with the stale Editor suspended and 10 logical
host cores. The host's one-minute load was 17.23–19.16.

| Selected cap | Present intervals | Median | 90th percentile | PSS |
|---|---:|---:|---:|---:|
| 60 FPS | 86 | 45 ms | 66 ms | 266,490 KiB |
| 30 FPS | 89 | 46 ms | 70 ms | 266,827 KiB |

### Signed Android build

The 45,478,501-byte ARM64 development APK is at
`/Volumes/PLOP_DEV/Builds/Android/Phase7/PLOP-phase7-dev.apk`.
The Android SDK aligned and repacked the Gradle archive. All entries
retained their sizes and checksums. The APK passed v2 signature and
16 KB ZIP alignment checks. The signing certificate matches Phase 6.
SHA-256 is
`3c6f6addaa7181c4761e2016261c4383182090aac6f639c37fd26548defebc5d`.

### Offline Android player

On the Android API 37 emulator, installing Phase 7 over the signed Phase
6 build preserved the earned Level 3 save byte for byte. Both completed
IDs, all three unlocked IDs and the resume ID matched. The offline
player launched into Level 3 on the same install.

At 1080×1920 and 420 dpi, a 24 px drag produced zero moves and a 48 px
drag produced exactly one. Undo restored zero. A near-diagonal
180×175 px drag produced zero moves. This is touch-injection validation
through Android and Unity. Physical touch behavior still requires a
phone.

A 610 px swipe advanced exactly once, and starting a drag over Hint
left the move count alone. The verified hint indicated Up at zero moves
and two back-to-back Up swipes completed Level 2 in two moves. At
720×1280 and 320 dpi, a 35 px swipe registered once. The largest checked
board was Level 100 at 1440×3200.

Level 8 reached its exact proof after Up and Left. Keep playing
preserved the two-move state and a blocked swipe triggered no duplicate.
Undo restored one move and Restart restored zero. With a labeled
Level 41 fixture, the World picker ignored a locked World, replayed
Level 21 and resumed Level 41 with its completed and unlocked IDs.

Home and return, and screen off and wake, returned to the same
foreground Android process with byte-identical Level 3 preferences.
Touching the frame setting persisted both 30 and 60. Music gain
0.2910 and effects gain 0.8142 survived turning on master mute, a cold
restart and unmute. The tested mute states were 0, 1, 1 and 0.
Returning from Android Settings preserved the running process and
save. An offline Level 3 solve followed by Next Level and an
immediate force-stop cold-resumed Level 4 with three completed
and four unlocked IDs.

### Phase 7 Android frame pacing

SurfaceFlinger measured Level 2 at 1080×1920 and 420 dpi with
diagnostics disabled. Each cap used a fresh fixture and 18 left swipes
of 265 px over 165 ms, continuing at the edge after the initial moves.
Xcode was building and the iOS simulator had yet to boot. The host
had 10 logical cores, 5.14–7.47 one-minute load, and QEMU sampled
282.8–418.8% CPU.

| Selected cap | Present intervals | Median | 90th percentile | PSS |
|---|---:|---:|---:|---:|
| 60 FPS | 79 | 48 ms | 78 ms | 264,254 KiB |
| 30 FPS | 88 | 54 ms | 94 ms | 263,609 KiB |

The follow-up used the same gestures after Xcode exited and the iOS
simulator shut down. Host CPU was 68.15% idle before the guest woke.
During sampling, one-minute load was 15.97–18.02 and QEMU used
324–666% CPU. Two existing Unity Package Manager processes were active.

| Selected cap | Present intervals | Median | 90th percentile | PSS |
|---|---:|---:|---:|---:|
| 60 FPS | 87 | 42 ms | 58 ms | 263,607 KiB |
| 30 FPS | 87 | 38 ms | 70 ms | 264,086 KiB |

These describe the emulator under each recorded host condition. The
runs fell short of sustained 60 FPS. The Phase 6 baseline was collected
under a different load. Physical GPU frame time, pacing, device power
and thermals remain unmeasured.

### Development diagnostics

A separate Level 2 Android sample with the diagnostics HUD active
during Xcode compilation showed loop p50 33.3 ms and p90 63.1 ms.
At capture time the HUD reported 58.4 ms main thread, 1,574 B GC
allocation for the sampled frame, 13 draw calls and 77.7 MiB of
Unity allocated memory. The render-thread counter was unavailable.
Unity game-loop intervals and allocated memory differ from Android
presentation time and process PSS.

### Audio assets

The five original 12-second mono ambience WAVs regenerate byte for byte
with `Tools/AudioArt/render_ambience.py`. PCM loop RMS ranges from −37
to −35 dBFS, and the largest measured cyclic sample or slope difference
is −90.3 dBFS. The assets and the ten synthesized event cues have zero
clipped source samples. Unity PlayMode verified playback state, crossfade
gain, focus changes and persistence. Speaker mix and haptic feel on
physical phones remain pending.

### iOS simulator

Unity exported the ARM64 simulator project to
`/Volumes/PLOP_DEV/Builds/iOS/Phase7/PLOP-Xcode-Sim`. Xcode 26.3 built
it with simulator signing disabled and compiled the UIKit haptic
bridge. The iOS 26.3 iPhone 15 Pro Max simulator displayed Levels 100,
44 and 8 at 1290×2796. Portrait controls and the recovery actions
stayed inside the safe area. Scrim sampling from Level 8 found a
5.4–5.9 percentage point dimming on all four screen edges. The
simulator preference fixture was restored after verification.
Physical iPhone installation and tactile response remain pending.

Raw Phase 7 test and platform logs are in
`/Volumes/PLOP_DEV/Builds/Logs/phase7/`.

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
