# PLOP! project state

**Phase 6 · Visual mastery and themed worlds**

The project is on `feature/themed-worlds`, based on `a93708f`
(`checkpoint/pre-phase6`). Open the original project in Unity
`6000.3.29f1`. Use **PLOP → Play Game** or open
`Assets/Scenes/Gameplay.unity`.

## Implemented

- Five data-driven Worlds with distinct portrait art, board palettes,
  lighting and quiet original ambience. Four chapter boundaries follow
  the confirmed catalog order.
- **Levels** opens the World picker with previews, ranges, completion,
  current and locked states. Resume goes to the latest unlocked level.
  Replays recover their original theme.
- Refined jelly material, face motion, contact shadows and matching
  shape stamps. Nest seals remain visible beside an overlapping Plop.
- Reduced motion, short opt-in native haptics, independent audio gains,
  and the existing 30/60 FPS settings.
- Existing exact deadlock recovery and saved ID-based campaign progress.

## Verification

- .NET and Unity EditMode each passed 98 tests. Unity PlayMode passed 51.
  Strict replay and validation passed for all 100 production levels.
- The signed ARM64 Android development APK is at
  `/Volumes/PLOP_DEV/Builds/Android/Phase6/PLOP-phase6-dev.apk`.
  An offline, in-place upgrade from Phase 5 kept campaign progress and
  the Phase 6 comfort settings persisted across a restart.
- Xcode 26.3 built the ARM64 iOS Simulator export. The iPhone 15 Pro Max
  simulator displayed Moonlit Clouds and reproduced Level 8 recovery.
- [Phase 6 previews](Documentation/Previews/Phase6/README.md) include
  gameplay in each World and portrait captures of the campaign UI.
- [Testing notes](Documentation/TESTING.md) record Unity profiling and
  both Android frame-cap samples. Physical-device pacing, haptic feel
  and battery use remain unmeasured. The starting reference and
  priorities are in the [visual plan](Documentation/PHASE6_VISUAL_PLAN.md).

## Continue development

Read [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md), and
[ARCHITECTURE.md](ARCHITECTURE.md). Add Worlds through
`Assets/Art/Resources/Worlds`. Use the offline art painter to regenerate
the layered backdrops and previews with their original GUIDs. World
definitions require no gameplay or save-schema edits.
