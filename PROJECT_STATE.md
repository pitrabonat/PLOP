# PLOP! project state

**Phase 4 · Playable experience and visual production**

The checkout is on `feature/visual-production`, based on `f5c5cac`
(`checkpoint/pre-visual-production`). Open **PLOP → Play Game** in Unity
`6000.3.29f1` or press Play in `Assets/Scenes/Gameplay.unity`.

## Implemented

- The deterministic puzzle engine and 100-level production campaign from
  Phase 3, including unlocks, replay, hints, Undo and Restart.
- A molded rounded puzzle tray, padded Nests and an asymmetric jelly
  character with awake, collision and sleeping expressions.
- Event-driven anticipation, slide, recoil, sleep and completion animation.
  Undo and Restart clear visual motion and snap to authoritative positions.
- A responsive HUD with **Sweet Dreams!**, immediate Next Level, and a
  Settings card for music, sound and 30/60 FPS.
- Five original synthesized effects and an original ambient loop, with
  independent persisted volumes.

## Verification

The Phase 3 scene launched in Unity before edits and reacted to keyboard
input. The Phase 3 APK installed and solved Level 1 by touch in the emulator.
The [baseline screenshot](Documentation/Previews/phase3-android-baseline.png)
records that running build. The Phase 4 Editor session reopened at the saved
Level 2 with the production HUD.

- Unity compiled the production presentation. .NET and Unity EditMode
  each passed 92 tests. Unity PlayMode passed 29, including loading, solving,
  undoing, restarting, and advancing through all 100 shipped levels.
- Unity exported the ARM64 iOS Simulator project to
  `/Volumes/PLOP_DEV/Builds/iOS/Phase4/PLOP-Xcode-Sim`. Xcode 26.3 built it
  with simulator signing disabled. The iPhone 15 Pro Max ran Level 29 and
  showed the sleeping Plops and completion screen after nine moves. The
  compact iPhone SE profile also rendered Level 29 in portrait.
- The Android ARM64 IL2CPP development APK is at
  `/Volumes/PLOP_DEV/Builds/Android/Phase4/PLOP-phase4-dev.apk`.
  The 44,646,700-byte APK passed v2 signature and 16 KB ZIP alignment
  checks. The API 37 emulator ran it offline, solved Level 1 by touch,
  advanced to Level 2, and retained progress, muted audio and the 30 FPS
  cap after relaunch.
- The [running game previews](Documentation/Previews/README.md) include
  both iPhone screen sizes, the completion UI and the final Android Settings screen.

The main production commit is `2f3a055`. The title and volume control
layout corrections and their regression assertions are in `e0d3017` and
`55b9b62`.

## Remaining device checks

No physical Android device was available for this pass. The iOS build uses
the simulator target; the existing device management restriction is
documented in [CLAUDE.md](CLAUDE.md). Measure 30/60 FPS frame pacing, memory,
touch responsiveness and the audio mix on physical phones before tuning
release defaults.

## Resume

Use the build commands in [CLAUDE.md](CLAUDE.md) and the test instructions
in [Documentation/TESTING.md](Documentation/TESTING.md). The source branch
retains Phase 3 and avoids changes to the puzzle catalog and rules.
