# Phase 11 previews

Captured from the Phase 11 development APK (`PLOP-phase11-dev.apk`) on the Android emulator `Medium_Phone_API_37.0` (1080×2400). No physical phone was connected. Gameplay screens were opened with the development-only jump `python3 Tools/Android/debugprefs.py <level> [moves]`, which shows the real level but never writes progress. Menu screens use the emulator's real save: a Phase 10 save that the Phase 11 build migrated on first launch.

| File | Screen | What it shows |
|---|---|---|
| [01-main-menu.png](01-main-menu.png) | Main menu | First launch after the update. The Phase 10 save migrated: 25 Dust kept, 2 of 3 old completions still exist and were kept, PLAY points at the furthest open level (6) |
| [02-early-tutorial-L4.png](02-early-tutorial-L4.png) | Level 4 (authored) | Two Plops and walls: hold one Plop with a wall while the other moves |
| [03-frost-L8.png](03-frost-L8.png) | Level 8 | Frost with a second Plop as a stopper |
| [04-breeze-L27.png](04-breeze-L27.png) | Level 27 | Two fans; direction chevron and the larger power pips (three dots = power 3) |
| [05-lava-L43.png](05-lava-L43.png) | Level 43 | Lava on Frost slides |
| [06-loops-L57.png](06-loops-L57.png) | Level 57 | A Loop pair: tint plus a pair pip (pair 1 = one pip), Lava |
| [07-advanced-combo-L155.png](07-advanced-combo-L155.png) | Level 155 | All four mechanics, three Plops, Limited Moves at the optimum (15) |
| [08-limited-moves-L30.png](08-limited-moves-L30.png) | Level 30 | First Limited Moves challenge: 11 moves for a 9-move optimum |
| [09-perfect-L3.png](09-perfect-L3.png) | Level 3 solved | "Perfect! 2 moves" with Replay / Next Level |
| [10-character-studio.png](10-character-studio.png) | Character Studio | Body tab, prices in Dust or milestones |
| [11-world-selection.png](11-world-selection.png) | Worlds | New World ranges: Cozy Nest 1–5, Frosted Pond 6–20 |
| [12-daily-puzzle.png](12-daily-puzzle.png) | Daily Puzzle | For a save at Level 3 the Daily serves a classic puzzle; mechanic tiers unlock after Levels 6/21/35/49 |
| [13-failure-recovery-L35.png](13-failure-recovery-L35.png) | Lava failure | "Oops! Too hot!" with Undo Move, Restart Level and Keep playing |
| [14-L155-perfect-diagnostics.png](14-L155-perfect-diagnostics.png) | Level 155 solved by real swipes | Perfect at the 15-move limit, with the dev frame-time overlay |

## iOS Simulator (iPhone 15 Pro Max, Xcode 26.3 build)

| File | Screen |
|---|---|
| [ios/ios-01-main-menu.png](ios/ios-01-main-menu.png) | Main menu after migrating the Simulator's Phase 10 save (55 Dust, PLAY → Level 123) |
| [ios/ios-02-loops-L57.png](ios/ios-02-loops-L57.png) | Level 57, Loops and Lava, inside the Dynamic Island safe area |
| [ios/ios-03-combo-L120.png](ios/ios-03-combo-L120.png) | Level 120, Limited Moves 16, three Plops, Lava + Breeze + Loops |
