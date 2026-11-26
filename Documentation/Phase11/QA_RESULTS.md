# Phase 11 QA results

Executed 2026-11-26 on an Apple M4 Mac (10 threads), Unity 6000.3.29f1, Xcode 26.3. Everything below was run in this phase. **There was no human playtest and no physical phone or iPhone.** Battery and thermal behaviour were not measured.

## Automated tests

| Suite | Result | Phase 10 | New in Phase 11 |
|---|---|---|---|
| .NET (`dotnet test DotNet/Plop.Tests`) | **200 / 200** | 179 | 21 |
| Unity EditMode | **200 / 200** | 179 | 21 (the same files as .NET) |
| Unity PlayMode | **106 / 106** | 100 | 6 |

New tests by area:

| Area | Tests |
|---|---|
| Campaign | 160 levels strictly verified (BFS + replay); early pacing L1–L10 follows the teaching order; Limited Moves slack +2 → +1 → 0; World Mastery levels need deep planning; most Phase 10 puzzles kept and the order file matches the archive |
| Daily | Pool verified, distinct from the campaign, covers every mechanic; gating serves only learned mechanics and is deterministic per date |
| Economy | A hint is charged once per board; grace needs two tries, is free once per level and cannot be farmed; grace is unused when the hint is affordable; the simulation is deterministic and pays each reward once; no standard profile is refused a hint |
| Save migration | Same-id completions kept, access up to the old frontier, replacements never marked complete; finishing the old frontier opens the next slot; aliases carry completion only for beaten boards; missing or corrupt saves are ignored; the migration data ships in Resources |
| Stability | Corrupt wallet and records never throw and stay sane; a torn wallet write recovers from the backup; random sessions (Undo, Restart, failures) on every shipped level keep the engine consistent |
| Playtest log | A visit counts help and ends once; starting another level closes the open visit as "left" and detects Perfect; the summary skips damaged lines |
| Layout | Every shipped board fits six reference screens (phones, a notched phone, a tablet) with cells ≥ 44 pt |
| Player flow | System Back closes Studio and modals, a level returns to the menu, and the menu sends the app to the background |

## Content verification

- `LevelVerifier.VerifyProductionPack`: Pack_003 160/160 and Daily_002 60/60, the stored solution replays and BFS confirms the optimum.
- `ForcedChainInspector` at 1M states: no unresolved command, longest chain 6, no loop risk.
- `PLOP_Deadlocks_003.bytes`: 160/160 levels fully analysed at 600k states.
- Determinism: `--balanced` run twice gave byte-identical Pack_003 (`9be754d4…`) and Daily_002 (`32efc5b7…`); the second run was immediately before the APK build.

## Bugs found and fixed in Phase 11

| Id | Pri | Found by | Bug | Fix |
|---|---|---|---|---|
| Q-01 | P1 | Campaign audit | Opening levels were trivial (greedy or random solves) | New authored L2, L4, L6–L10 |
| Q-02 | P2 | Campaign audit | Three levels had decorative mechanics | One stripped (L42), two replaced |
| Q-03 | P2 | Economy simulation | A Casual player was refused a hint in 67 % of runs; no recourse when out of Dust | `HintPolicy` grace |
| Q-04 | P2 | Daily audit | Daily pool used no mechanics; 41/60 flagged | `PLOP_Daily_002` with gating |
| Q-05 | P2 | Readability review | Loop pairs distinguished only by colour | Dice pips per pair |
| Q-06 | P3 | Readability review | Fan power pips too small to count | Larger pips with a halo |
| Q-07 | P2 | Build v2 | A kept puzzle was judged by its old slot (L11 at quality 29) | Judge kept puzzles by their new slot, on a separate seed range |
| Q-08 | P2 | Build v2 | Changing a rule version re-rolled every search seed | `SeedVersion` decoupled from the rule `Version` |
| Q-09 | P3 | Test run | Layout test estimated px/pt from width, failing on iPad | Per-device scale |
| Q-10 | P3 | Interrupted build | A killed Android build left `preloadedAssets` in ProjectSettings | Reverted; builds checked for ProjectSettings drift afterwards |
| Q-11 | P2 | Emulator player-flow test | **Android system Back did nothing anywhere** (Studio, settings, level picker, recovery card, gameplay) | `PlopGameController.HandleBack` + `GameHud.HandleBack`; PlayMode test; re-verified on the emulator |
| Q-12 | P3 | Emulator capture | `adb shell monkey … 1` can inject a random touch after launch, which made one capture start at move 1 | Captures use `am start`; documented for testers |

## Android (emulator)

Final development APK: `/Volumes/PLOP_DEV/Builds/Android/Phase11/PLOP-phase11-dev.apk` (ARM64, 46.5 MB, Unity 6000.3.29f1). `adb devices` listed only `emulator-5554` (`Medium_Phone_API_37.0`, 1080×2400): **no physical Android phone was connected.**

| Check | Result |
|---|---|
| Upgrade over the emulator's real Phase 10 save | Migrated on first launch: "2 completions kept, 6 slots open". 25 Dust, records and cosmetics untouched. The old L2 completion was not carried, because L2 was replaced. PLAY → Level 6 (furthest open) |
| Gameplay at Levels 3, 4, 8, 27, 30, 35, 43, 57, 155 | Rendered correctly; mechanic tiles readable. Fan power dots are countable; Loop pairs show pips |
| Perfect | Level 3 by replay and Level 155 by **15 real swipes** following its stored route → "Perfect! 15 moves · +12" |
| Failure recovery | Swipe into Lava on Level 35 → "Oops! Too hot!" with Undo Move / Restart Level / Keep playing |
| Limited Moves | Level 30 shows "Challenge · 11 moves" and `0 / 11` |
| Daily Puzzle | For this save (Level 3 beaten) a classic puzzle was served and stored for the day (`plop.daily.pick.v2`) |
| Character Studio, Worlds, Settings | Open, render and close. Worlds show the new ranges (1–5, 6–20, …) |
| System Back (after the fix) | Studio → menu; Settings from the menu → menu; Level → menu; menu → app backgrounded; relaunch resumes. The **first** press after launch works (the first attempt polled `Keyboard.current` and missed it; now routed through an `InputAction`) |
| Frame time | See PERFORMANCE_REPORT.md |
| ProjectSettings after each build | Unchanged |

Screenshots: [Previews/Phase11](../Previews/Phase11/README.md).

## iOS (Simulator)

- Unity exported the ARM64 Simulator project to `/Volumes/PLOP_DEV/Builds/iOS/Phase11/PLOP-Xcode-Sim`. Xcode 26.3 built it: **BUILD SUCCEEDED** (Debug, `iphonesimulator`, unsigned).
- Installed over the Phase 10 app on the "PLOP iPhone 15 Pro Max" simulator (iOS 26.3). The Phase 10 save migrated (55 Dust kept, PLAY → Level 123, the old frontier) and a `PLOP_Pack_003` progress key was written.
- Level 57 (Loops, Lava) and Level 120 (Limited Moves 16, Lava + Breeze + Loops, three Plops) render inside the Dynamic Island safe area. Captures are in [Previews/Phase11/ios](../Previews/Phase11/ios/).
- Taps were **not** driven on iOS. The Simulator tap tool needs a system-wide `xcode-select` pointing at a full Xcode, which would mean a privileged system change; it was not made. iOS gameplay logic is the same C# that the Unity tests cover.
- No device build: there is no iPhone and no signing identity.

## Player-flow walkthrough (emulator)

Launch → migrated menu → Studio (browse, Back) → Settings (Back) → Worlds → Level 6 (Frost intro) → Home → Daily → Lava failure and recovery → Perfect on a three-Plop Expert level → Back to the menu → Back to background → relaunch. The only blocker found was the system Back (Q-11), now fixed. Two design observations are listed in KNOWN_ISSUES: PLAY skips the replaced tutorial levels for migrated players (K-13), and the single Loop pip sits in the ring centre (K-15).

## Not verified

- Any human playtest (difficulty, clarity, fun).
- Physical Android or iOS hardware: touch feel, notch and cutout safe areas on real panels, frame pacing, battery, thermals.
- Daily Puzzle on iOS for a save past Level 49 (gating is unit-tested: `DailyGating_OnlyServesLearnedMechanics_AndIsDeterministic`).
