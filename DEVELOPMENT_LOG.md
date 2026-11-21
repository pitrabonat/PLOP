# Development log

Previous Unity scaffold, gameplay and visual foundation milestones are recorded
in the [historical development log](Documentation/DEVELOPMENT_LOG.md).

## 2026-11-21 · Phase 3 puzzle intelligence and progression

- Started from the visual foundation at `ee033b2` and tagged that commit
  `checkpoint/pre-puzzle-intelligence`.
- Shipped 10 authored tutorials and 90 seeded puzzles across four difficulty
  bands. Added graph-backed measures of coordination, required detours and
  settlement order, and rejection of one-wall variants.
- Reproduced all 100 boards byte-for-byte with seed `0x504C4F5021`. Strict
  verification passed on every board. See the
  [generation benchmark](Documentation/benchmarks/generation_benchmark_v2.md).
- Expanded Level Lab with filtered inspection, manual pack ordering and edits,
  guarded production saves, and generation of the ordered campaign.
- Added ID-based unlocks, completion and replay, plus an on-demand current-board
  hint and persistent 30/60 FPS selection. Legacy saves preserve access.
- The Android emulator exposed a directional tutorial line that conflicted
  with a verified hint after a detour. Changed that tutorial to explain the
  goal and regenerated the same boards with their original IDs.
- Final .NET and Unity EditMode each passed 92 tests. Unity PlayMode passed
  22, including all 100 boards through the controller. Android ARM64 IL2CPP
  built and the offline emulator verified hints, progression and replay.
- Restored the .NET harness project files to version control so a fresh
  checkout builds the same engine-free code and tests.

See [PROJECT_STATE.md](PROJECT_STATE.md) for verification and remaining device QA.

## 2026-11-21 · Phase 4 playable experience and visual production

- Started from `f5c5cac` and tagged it `checkpoint/pre-visual-production`.
  Confirmed the production scene in Unity and completed Level 1 by touch on
  the Android emulator with the Phase 3 APK.
- Reworked the board and animated jelly characters in the shared SDF atlas,
  added responsive Settings and completion controls, and introduced original
  synthesized effects and music. The core rules, solver and level catalog
  match the Phase 3 checkpoint.
- Fixed a board-sizing regression detected by PlayMode. The first simulator
  preview also exposed clipping in the new level-title font. Increased its
  layout space and added a glyph-generation assertion. Unity EditMode passed
  92/92, PlayMode passed 29/29, and .NET passed 92/92.
- The revised ARM64 iOS Simulator export built under Xcode 26.3. Captured
  Level 29 before and after its nine-move solution on iPhone 15 Pro Max.
  The screenshots show the level heading and the completion action.
- Confirmed the portrait layout at 750 × 1334 on iPhone SE. Android
  testing exposed slider anchors that stretched the thumbs over their
  labels at minimum volume. Fixed the geometry and reran Unity
  PlayMode (29/29).
- Built and verified the 44.6 MB Android development APK. Offline touch
  play, Hint, Undo, completion, Next Level and relaunch passed in the
  emulator. Both audio volumes and the 30 FPS cap persisted.
- Re-exported the final simulator player and built it in Xcode 26.3.
  Captured the finished HUD on iPhone 15 Pro Max and iPhone SE. The
  production scene reopened in Unity Play Mode at the saved Level 2.
