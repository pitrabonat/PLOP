# PLOP! project state

**Phase 7 · Game feel, audio, accessibility and mobile optimization**

The project is on `feature/game-feel-polish`, based on `bd57c25`
(`checkpoint/pre-phase7`). Open the original project in Unity
`6000.3.29f1` with **PLOP → Play Game** or
`Assets/Scenes/Gameplay.unity`.

## Implemented

- A density-aware swipe recognizer rejects near-diagonal gestures. One
  command can wait during slide travel. Undo, Restart, menus, pause and
  level changes clear pending commands and in-progress gestures.
- Plop-wall and Plop-to-Plop recoil differ. Rapid retargets and Undo
  preserve visual continuity. Reduced motion dispatches immediately.
- Ten original tactile cues, five quiet World loops, short crossfades,
  independent gains and persistent master mute. The existing optional
  haptics recognize successful moves, blocks, settles and completion.
- Verified hints display a direction marker. Deadlock recovery is
  proof-only and appears once for an unchanged board. The five Worlds
  and ID-based campaign retain their original level mapping.
- Development builds can show an F3 diagnostic snapshot with the local
  level, World, queue state, frame distribution and Unity counters.

## Verification

- .NET and Unity EditMode each passed 98 tests. Unity PlayMode passed 66.
  The 100 shipped boards passed strict replay. The levels, gameplay
  scene, Core, Solver and generator match the Phase 6 checkpoint.
- The signed ARM64 Android APK is
  `/Volumes/PLOP_DEV/Builds/Android/Phase7/PLOP-phase7-dev.apk`.
  The offline in-place upgrade kept the Phase 6 save and comfort
  preferences. The Android emulator accepted the tested 48 px drag
  and rejected the near-diagonal gesture.
- The [campaign playtest checklist](Documentation/PHASE7_CAMPAIGN_PLAYTEST.md)
  prioritizes Level 8's first trap, the climbs into 51 and 76, and the
  peak and finale at 99 and 100.
- [Testing notes](Documentation/TESTING.md) record the Phase 7 baseline,
  runtime regression checks and mobile performance measurements.

## Continue development

Start with [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md) and
[ARCHITECTURE.md](ARCHITECTURE.md).
