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
records that running build. Phase 4 builds and device checks are under way.

## Resume

Use the build commands in [CLAUDE.md](CLAUDE.md) and the test instructions
in [Documentation/TESTING.md](Documentation/TESTING.md). The source branch
retains Phase 3 and avoids changes to the puzzle catalog and rules.
