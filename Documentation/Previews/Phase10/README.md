# PLOP! Phase 10 previews

Every image is a screenshot of the Phase 10 ARM64 development APK (`PLOP-phase10-dev.apk`) running on the
Android API 37 emulator (1080×2400). No physical phone was connected. Nothing here is concept art.

Gameplay captures use the development-only preview prefs (`Tools/Android/debugprefs.py <level> [moves]`).
Preview launches read the real look and Dream Dust but never write the save, so the paid hint below did not
change the emulator's balance.

| # | Required view | Capture |
|---|---|---|
| 1 | Frost level | [frost-level.png](frost-level.png) — Level 12, Frosted Pond |
| 2 | Lava level | [lava-level.png](lava-level.png) — Level 39, Ember Hollow |
| 3 | Breeze level | [breeze-level.png](breeze-level.png) — Level 24, Breezy Meadow (fan chevrons and power pips) |
| 4 | Loop level | [loop-level.png](loop-level.png) — Level 52, Prism Observatory (one Loop pair) |
| 5 | Frost + Breeze | [frost-breeze.png](frost-breeze.png) — Level 27 |
| 6 | Breeze + Lava | [breeze-lava.png](breeze-lava.png) — Level 43 |
| 7 | Loop + Frost | [loop-frost.png](loop-frost.png) — Level 66, Dewdrop Garden |
| 8 | Advanced multi-mechanic puzzle | [advanced-multi-mechanic.png](advanced-multi-mechanic.png) — Level 157, Moonlit Clouds (Frost, Lava, Breeze, Loops) |
| 9 | World Selection | [world-selection.png](world-selection.png) — the four new Worlds with ranges and progress |
| 10 | Dream Dust hint purchase | [dream-dust-hint-purchase.png](dream-dust-hint-purchase.png) — Level 24, "Hint 15", charge shown in the hint line |
| 11 | Cosmetic Store | [store-board-tab.png](store-board-tab.png), [store-board-preview.png](store-board-preview.png) — the new Board category |
| 12 | Limited Moves challenge | [limited-moves-challenge.png](limited-moves-challenge.png) — Level 30, "Challenge · 10 moves", counter 1 / 10 |
| 13 | Lava failure overlay | [lava-failure-overlay.png](lava-failure-overlay.png) — Level 35 after swiping into Lava |
| 14 | Frosted Pond | [frost-level.png](frost-level.png) |
| 15 | Breezy Meadow | [breeze-level.png](breeze-level.png), [limited-moves-challenge.png](limited-moves-challenge.png) |
| 16 | Ember Hollow | [lava-level.png](lava-level.png), [lava-failure-overlay.png](lava-failure-overlay.png) |
| 17 | Prism Observatory | [loop-level.png](loop-level.png) |

iOS Simulator (iPhone 15 Pro Max, Xcode 26.3 build): [ios/main-menu.png](ios/main-menu.png), [ios/level-157.png](ios/level-157.png),
[ios/level-35.png](ios/level-35.png) (Lava card).

Also: [main-menu-after-migration.png](main-menu-after-migration.png) — the emulator's Phase 9 save after the
automatic migration to the 160-level campaign: 25 Dream Dust kept, PLAY resumes at Level 6 (five levels had been
beaten), and only the three old completions whose puzzles still exist are counted (Cozy Nest 3/6 in the Worlds list).

## What the screenshots caught

- **The first Phase 10 APK showed an empty board.** `Gameplay.unity` still serialized
  `levelPackResource: Levels/PLOP_Pack_001`, which had moved to `Assets/Levels/Archive/`. The scene now names
  Pack 002, the controller falls back to the current pack if a scene names the retired one, and
  `GameplayScene_LoadsTheShippedPack` guards it. Editor tests had passed because they supply their own levels.
- **A paid hint gave no sign that Dust was spent.** The hint line now ends with "−15 Dream Dust · N left".
- **Board thumbnails drew round tiles and the Studio preview ignored the board.** Thumbnail tiles are now soft
  squares and the preview floor takes the selected decoration's tint.
