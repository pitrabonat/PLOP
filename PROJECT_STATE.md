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
- The Unity render sample, platform checks and playback details are in
  [Documentation/TESTING.md](Documentation/TESTING.md). The starting
  reference and priorities are in
  [Documentation/PHASE6_VISUAL_PLAN.md](Documentation/PHASE6_VISUAL_PLAN.md).

## Continue development

Read [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md), and
[ARCHITECTURE.md](ARCHITECTURE.md). Add Worlds through
`Assets/Art/Resources/Worlds`. Use the offline art painter to regenerate
the layered backdrops and previews with their original GUIDs. World
definitions require no gameplay or save-schema edits.
