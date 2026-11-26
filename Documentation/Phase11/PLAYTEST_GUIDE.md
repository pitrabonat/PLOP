# Phase 11 playtest guide

Phase 11 balanced the campaign with offline models. The next step is people. This guide covers what to ask testers to play, how to reach any level in a development build, how to record local metrics, and a sheet to fill in. **No playtest has been run yet.** Every difficulty below is the *intended* band from the offline audit.

## Setup (development build only)

- Android: install `/Volumes/PLOP_DEV/Builds/Android/Phase11/PLOP-phase11-dev.apk` (development, debuggable), for example with `adb install -r …`.
- iOS Simulator: build `PLOP-Xcode-Sim` as described in `Documentation/TESTING.md`.
- **Jump to a level** (Android emulator, or a phone with USB debugging): `python3 Tools/Android/debugprefs.py <level>`. Clear it with `… 0`. A jump is a *preview*: it shows the real look and Dust but never writes progress, so a tester's save stays clean. For real progression, let testers play from Level 1.
- In the Unity Editor, set the integer PlayerPref `plop.debug.level` to the level number (for example with a one-line editor script, or `PlayerPrefs.SetInt` in the console), then press Play.
- **Frame time overlay**: `python3 Tools/Android/debugprefs.py diagnostics on` (FPS, p50/p90 frame time, GC, draw calls). Turn it off for screenshots.
- Launch with `adb shell am start -n com.plop.sleepypuzzle/com.unity3d.player.UnityPlayerGameActivity`. Avoid `adb shell monkey … 1`: it can inject a random touch after launch.
- **Local metrics**: `python3 Tools/Android/debugprefs.py playtest on`. Each level visit appends one row to `playtest_metrics.csv` in the app's persistent data folder. Pull it with `python3 Tools/Android/debugprefs.py pull-playtest out.csv`, then summarize it with `cd DotNet && dotnet run -c Release --project Plop.Bench -- --playtest-summary ../out.csv`. The file never leaves the device unless you copy it. Release builds compile the recorder out (`[Conditional("DEVELOPMENT_BUILD")]`). There is no analytics SDK.

CSV columns: `session, level (0 = Daily), id, optimal, seconds, moves, undos, restarts, hints, failures, outcome (perfect | solved | left)`.

## 15 representative puzzles

Play them in this order. No solutions here, on purpose.

| # | Level | World | Mechanics | Board | Intended difficulty | What it checks |
|---|---|---|---|---|---|---|
| 1 | 4 | Cozy Nest | walls, 2 Plops | 4×4 | Easy | Using a wall to separate two Plops |
| 2 | 8 | Frosted Pond | Frost, 2 Plops | 4×5 | Normal | A sleeping Plop as a Frost stopper |
| 3 | 10 | Frosted Pond | Frost | 5×5 | Normal (first Mastery) | Planning a slide and the settling order together |
| 4 | 20 | Frosted Pond | Frost | 6×6 | Expert | Frost World finale |
| 5 | 27 | Breezy Meadow | Breeze | 5×5 | Normal | Reading fan direction and power |
| 6 | 30 | Breezy Meadow | Frost + Breeze, **Limited Moves 11 (opt 9)** | 5×6 | Hard | First move-limit challenge (+2 slack) |
| 7 | 34 | Breezy Meadow | Frost + Breeze | 6×6 | Expert | Breeze finale |
| 8 | 43 | Ember Hollow | Frost + Lava | 6×6 | Hard | Lava as a hazard on Frost slides |
| 9 | 48 | Ember Hollow | Frost + Lava | 6×6 | Hard | Lava finale |
| 10 | 57 | Prism Observatory | Lava + Loops | 5×6 | Normal | Loop pairs (pips + colour) |
| 11 | 63 | Prism Observatory | Lava + Loops | 6×6 | Hard | Loops finale |
| 12 | 72 | Dewdrop Garden | Frost + Loops, 3 Plops | 6×6 | Hard | Three-Plop coordination |
| 13 | 80 | Dewdrop Garden | Breeze + Loops, **Limited Moves 10 (opt 9)** | 6×6 | Hard | +1-slack challenge |
| 14 | 120 | Twilight Attic | Lava + Breeze + Loops, 3 Plops, **Limited Moves 16 (opt 15)** | 6×7 | Expert | Late combination under a limit |
| 15 | 155 | Moonlit Clouds | Frost + Lava + Breeze + Loops, 3 Plops, **Limited Moves 15 = optimum** | 6×7 | Expert | Exact-optimum challenge, all mechanics |

Also check: the first Daily Puzzle (Main Menu → Daily) for a new save and for a save past Level 49, Character Studio, World Selection, and failure recovery (swipe a Plop into Lava, then Undo).

## What to watch for

1. **Did the tester understand each new mechanic from its first level alone** (6 Frost, 21 Breeze, 35 Lava, 49 Loops)? Note any "what just happened?" moment.
2. **Where did they first get stuck**, and was the cause the puzzle or the rules?
3. **Limited Moves**: did running out feel fair? Did they know the counter was there?
4. **Hints**: did they buy any? Did they see the grace message ("after N more tries the first hint here is free")?
5. **Readability**: could they tell the Loop pairs apart (pips), and read fan power (dots)?
6. **Perfect**: did they notice the Perfect target, and did they replay for it?

## Playtest sheet (one per tester)

```
Tester: ________  Device: ________  Date: ________  Build: PLOP-phase11-dev  Session id (CSV): ________
Prior puzzle-game experience:  none / some / lots

Level | Solved? | Time (min) | Restarts | Hints | Perfect? | Felt (1 too easy … 5 too hard) | Notes ("aha", confusion, frustration)
  4   |         |            |          |       |          |                                |
  8   |         |            |          |       |          |                                |
 10   |         |            |          |       |          |                                |
 20   |         |            |          |       |          |                                |
 27   |         |            |          |       |          |                                |
 30   |         |            |          |       |          |                                |
 34   |         |            |          |       |          |                                |
 43   |         |            |          |       |          |                                |
 48   |         |            |          |       |          |                                |
 57   |         |            |          |       |          |                                |
 63   |         |            |          |       |          |                                |
 72   |         |            |          |       |          |                                |
 80   |         |            |          |       |          |                                |
120   |         |            |          |       |          |                                |
155   |         |            |          |       |          |                                |

Mechanic first-sight (6 / 21 / 35 / 49) understood without help?  Frost __  Breeze __  Lava __  Loops __
Loop pairs readable?  yes / no      Fan power readable?  yes / no
Daily Puzzle: tried?  __  felt fair?  __
Anything that felt unfair, confusing or slow:
```

## Turning results into changes

- Compare "Felt" with the intended band. A level two bands off in either direction for most testers is a candidate for the Level Lab: **PLOP → Level Lab → Balance & quality**, filter by World, then compare with `Assets/Levels/Archive/PLOP_Pack_002.json`.
- Feed real hint and Perfect rates into `EconomySimulator.Profiles` and rerun `--economy`.
- Record findings in `Documentation/Phase11/KNOWN_ISSUES.md` before changing levels. Never change a shipped level's board under the same id.
