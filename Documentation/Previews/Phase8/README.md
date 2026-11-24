# PLOP! Phase 8 previews

These screenshots come from the Phase 8 ARM64 development APK running on the Android API 37 emulator (`Medium_Phone_API_37.0`, 1080×2400). No physical phone was connected.

The level boards and the Perfect run were opened with the development-only `plop.debug.level` / `plop.debug.moves` preferences, set via `Tools/Android/debugprefs.py`. These preferences never save progress or Dream Dust. All other screens were reached by tapping in the running app.

| Capture | What it shows |
|---|---|
| [Main menu, fresh](main-menu.png) | Logo, sleepy Plops, PLAY "Level 1", Daily Gift (available), Daily Puzzle, Levels, Settings, Dream Dust 0. |
| [Daily Gift claimed](daily-gift-claimed.png) | "+20 Dream Dust" toast, balance 40 → 60, tile now reads "Tomorrow". |
| [Main menu with progress](main-menu-progress.png) | After a relaunch and an in-place APK upgrade: Dust 60, PLAY resumes Level 5, gift already claimed. |
| [Daily Puzzle](daily-puzzle.png) | Date subtitle, `0 / 10` Perfect target, 5×5 board from the offline daily pool. |
| [World selection](world-selection.png) | Worlds list with progress (4/20) and the Resume button. |
| [Level selection](level-select.png) | Levels 1 and 3 are marked "Perfect", Levels 2 and 4 "Done", Level 5 is current, later levels are locked. |
| [Level 1](level-1.png) | Authored tutorial with a short instruction and `0 / 1`. |
| [Level 25](level-25.png) | Dewdrop Garden, a 5×5 board with two Plops. |
| [Level 50](level-50.png) | Rainy Window, a 6×6 board. |
| [Level 75](level-75.png) | Twilight Attic, a 6×6 board with three Plops. |
| [Level 100](level-100.png) | Moonlit Clouds, a 6×7 board with three Plops and `0 / 16`. |
| [Perfect completion](perfect-level-50.png) | Level 50 solved in the optimal 13 moves: "Perfect!", gold star, "+15". |
| [Non-Perfect completion](completion-sweet-dreams.png) | Level 4 solved in 6 moves: "Sweet Dreams!", "6 moves · Perfect: 4 · +5". |
