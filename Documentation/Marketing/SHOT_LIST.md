# Marketing shot list (dev only)

The source for store screenshots and raw gameplay captures. Each World shot names a level whose board shows that World's
mechanics. **Solutions are listed for capture only.** They come from the level data's `dev.solution`, and
UDLR means swipe up / down / left / right. They must never appear in store text.

Capture rules:
- Dev build preview jump (`Tools/Android/debugprefs.py <level>`), so no save is touched.
- Portrait, 1080×2400, no diagnostics overlay.
- Capture after the level has settled.
- Every frame comes from the running game; no mock-ups.

| # | Shot | World | Level | Level ID | Board | Plops | Mechanics | Optimal | Solution | Capture moment |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Cozy Nest gameplay | Cozy Nest | 4 | `PLOP-59D8F1DBCF71` | 4x4 | 2 | — | 6 | `DULULU` | level start |
| 2 | Frosted Pond gameplay | Frosted Pond | 13 | `PLOP-0389B44CD9AD` | 5x6 | 2 | Frost | 7 | `RDDLURU` | mid-solve, after 3 moves |
| 3 | Breezy Meadow gameplay | Breezy Meadow | 25 | `PLOP-39CAB544842F` | 5x6 | 2 | Fan, Frost | 7 | `LLURRUR` | mid-solve, after 3 moves |
| 4 | Ember Hollow gameplay | Ember Hollow | 46 | `PLOP-3E4B060A7117` | 5x6 | 2 | Fan, Lava | 7 | `RDRLUUR` | mid-solve, after 3 moves |
| 5 | Prism Observatory gameplay | Prism Observatory | 56 | `PLOP-C0E8BC52A7BB` | 5x6 | 2 | Frost, Lava, Portal | 6 | `URRRUL` | mid-solve, after 3 moves |
| 6 | Dewdrop Garden gameplay | Dewdrop Garden | 76 | `PLOP-CBCDA2F502D4` | 6x6 | 2 | Fan, Frost | 7 | `DRDLUDR` | mid-solve, after 3 moves |
| 7 | Rainy Window gameplay | Rainy Window | 87 | `PLOP-DDD2184282EA` | 6x6 | 3 | Frost, Lava | 8 | `DLRDDDUR` | mid-solve, after 4 moves |
| 8 | Twilight Attic gameplay | Twilight Attic | 118 | `PLOP-394E75C0242F` | 6x7 | 2 | Frost, Lava, Portal | 9 | `DDRUDDLDL` | mid-solve, after 4 moves |
| 9 | Moonlit Clouds gameplay | Moonlit Clouds | 144 | `PLOP-962EB8EB6902` | 6x7 | 3 | Fan, Frost, Portal | 11 | `UDLLLUUDLUL` | mid-solve, after 5 moves |
| 10 | Main menu with jelly Plops and logo | — | — | | | | | | | Menu, idle |
| 11 | Perfect! completion card | Cozy Nest | 4 | | | | | | | after the optimal solve |
| 12 | World picker | — | — | | | | | | | Levels ▸ Worlds |
| 13 | Character Studio | — | — | | | | | | | Studio, Accessories tab |
| 14 | Hint arrow | Frosted Pond | 13 | | | | | | | Hint pressed at level start |
| 15 | Lava recovery card (friendly failure) | Ember Hollow | 35 | | | | | | | after a swipe into Lava |

Files:
- Stills: `/Volumes/PLOP_DEV/Marketing/RawScreenshots/NN_<shot>.png`
- Clips: `/Volumes/PLOP_DEV/Marketing/RawGameplay/NN_<shot>.mp4` (screenrecord, no audio track; Android screenrecord can't capture audio)
- Audio for edits: `/Volumes/PLOP_DEV/Marketing/Audio/`
