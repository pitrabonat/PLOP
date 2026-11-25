# PLOP! project state

**Phase 9 · Character Studio, living jelly characters and cosmetic customization**

The project is on `feature/character-studio`. It branches from the verified Phase 8 work (`8ad9388`, tagged `checkpoint/pre-phase9`; Phase 8 is pushed as `feature/puzzle-depth`). Open the project in Unity `6000.3.29f1` with **PLOP → Play Game** or `Assets/Scenes/Gameplay.unity`. The game opens on the main menu. **The build target was last switched to iOS** (for the Simulator export); switch back to Android before Android work, or let the build script switch it.

## Implemented in Phase 9

Full design notes: [Characters and cosmetics](Documentation/CHARACTERS.md).

- **Gameplay identity is separate from appearance.** Logical colour, Nest matching, positions, rules, solver and progress never read cosmetics. Every body and Nest keeps the colour-independent match symbol. Tests check that cosmetics never change the simulation, that logical colours are preserved, and that Undo and Restart land on exact cells.
- **Rendering.** The look is baked into the existing runtime SDF atlas, so the board, characters, accessories, Nests and effects share one texture and one material (test-verified). There are no new shaders and no soft-body physics: the jelly is baked shading plus authored squash and stretch.
- **One rig.** `PlopView` drives the board (SpriteRenderers), the menu mascots and the Studio preview (UI Images) with the same animation code.
  - 10 expressions, brows, cheeks, and an accessory parented to the deforming body.
  - A personality per style (blink rhythm, breathing, wobble, reaction strength).
  - Tap reactions with a cooldown.
  - Reduced Motion keeps only face changes. Move effects are pooled, off under Reduced Motion and halved at 30 FPS.
- **Content (36 catalog items):**

  | Category | Items |
  |---|---|
  | Body styles | Classic, Bouncy, Snoozy |
  | Finishes | Classic, Frosted, Milky, Pearl, Dewdrop, Velvet |
  | Palettes | Toy Box, Pastel (brief colours), Sorbet |
  | Faces | Sweet, Button, Dozy, Blush, Starry |
  | Accessories | None plus Bow, Flower, Leaf, Ribbon, Star, Nightcap, Cloud, Crown |
  | Nests | Moon Pillow, Cloud Cushion, Marshmallow, Soft Flower, Crescent Bed |
  | Effects | None, Dust Puff, Sparkle, Bubbles, Star Trail |

  The catalog is data: `Assets/Data/Cosmetics/Resources/Cosmetics/PLOP_Cosmetics_001.json`. Its IDs are save keys.
- **Dream Dust.** Reuses the Phase 8 wallet. A purchase spends Dust and records ownership in one write. A failed save rolls back, and a torn write recovers from `plop.wallet.v1.bak`. Milestone items (levels, Perfects, Worlds, Daily Puzzles) unlock from saved progress once; most can also be bought. There is no paid currency, randomness or streaks.
- **Saves.** `plop.appearance.v1` holds the presets (one active preset; the format allows four). Removed, unknown or unowned items fall back to defaults. Phase 8 saves migrate with Dust, records and progress untouched.
- **Character Studio** (Main menu → Studio):
  - preview room with a large reacting Plop;
  - 7 category chips and a 3-column item grid with thumbnails and states;
  - preview before unlocking, then Equip / two-tap Unlock / Equipped / "Need N more" / the milestone condition.
  - Back returns to the menu; board and mascots then wear the new look. Preview textures are disposed on close.
- **Editor tools** (PLOP → Characters): Animation Lab, Validate Cosmetics (also a batch method; 0 problems), Capture Reference Sheets.
- **Problems found and fixed through screenshots:**
  - faces hidden by sorting in the capture tool;
  - Worried brows reading as angry, and flat lids making Sleepy read as annoyed;
  - the Star drawn as a square;
  - a thin Ribbon;
  - the Studio grid cut off on 20:9 phones;
  - a side accessory hidden by the Nest's match symbol on a sleeping Plop. Side accessories moved to the upper left, and the validator now enforces it.

## Verification (executed 2026-11-25)

- **Tests:** .NET 134/134, Unity EditMode 134/134, Unity PlayMode 89/89, cosmetic validator 36 items / 0 problems.
- **Measured in the Editor:**

  | Item | Value |
  |---|---|
  | `PlopView.Tick` | ~0.4 µs per Plop per frame |
  | Allocation over 1,000 frames | 0 bytes (heap-growth counter; the exact per-thread counter isn't available in the Editor's Mono) |
  | Gameplay atlas at 128 px/cell | 1024×512, 2 MB, 317 ms bake |
  | Studio thumbnails | 229 ms bake |
  | Studio preview look | 157 ms bake |

  Phone timings, frame time, memory and battery were **not** measured.
- **Android:** `/Volumes/PLOP_DEV/Builds/Android/Phase9/PLOP-phase9-dev.apk` (ARM64 development build) on the `Medium_Phone_API_37.0` emulator. No phone was connected. Exercised:
  - menu → Studio, and every category previewed;
  - a Dust unlock with confirm: Dozy face, 60 → 10 Dust;
  - equipping the free Bow, returning to the menu, and playing;
  - a Level 5 Perfect with sleeping Plops in the look;
  - an app relaunch keeping the equipped look;
  - previews of the Dewdrop Garden and Moonlit Clouds Worlds.
- **Screenshots:** [Phase 9 previews](Documentation/Previews/Phase9/README.md).
- **iOS:** Unity exported the ARM64 Simulator project, and Xcode 26.3 (`/Volumes/PLOP_DEV/Xcode`) built it: **BUILD SUCCEEDED**. It ran on the iPhone 15 Pro Max simulator: the main menu with live mascot rigs and the Studio entry inside the Dynamic Island safe area, and Level 90 gameplay with the new rig. The older save on that simulator migrated. The Studio was not tapped through on iOS: the simulator tap tool needs a system-wide `xcode-select` to the Xcode on the external volume, which this session did not change. The shader and texture path is unchanged: the same runtime `RGBA32` atlas and the default sprite material. No device build.

## Not done / known limits

- **Not verified:** a physical device, and 30/60 FPS frame time on hardware.
- **Bakes:** the character bakes are main-thread C#. A look change rebakes the gameplay atlas once; opening the Studio costs one thumbnail bake.
- **Art:** procedural SDF art: consistent and readable from 24 to 96 px per cell, but not hand-illustrated. Finishes differ subtly. See CHARACTERS.md for how to swap in authored art.
- **Presets:** one preset is used; the save format already allows more.

## Phase 8 summary

### Implemented in Phase 8

- **Diagnosis.** The [Phase 7 audit](Documentation/benchmarks/campaign_audit_phase7.md) found that a greedy "always closer" player solved 72/100 levels and 88 levels had no trap. The rules were deep enough; level selection was the problem. See [Puzzle quality](Documentation/PUZZLE_QUALITY.md).
- **Quality pipeline.**
  - `PuzzleInsight` measures each board with player models and route metrics. The weights in `ChallengeWeights` are configurable.
  - `QualitySearch` runs deterministic hill climbing toward per-slot targets.
  - `CampaignPlan` sets the build/peak/breather waves and the Tutorial/Easy/Normal/Hard/Expert thresholds (8 / 18 / 30 / 42).
- **Campaign rebuilt.**
  - 100 verified levels. Levels 1–10 are authored and keep their IDs.
  - Greedy now solves 14/100, all at level 25 or earlier. Only 21 levels have no trap.
  - The challenge average rises by band: 3.6 → 23.6 → 39.1 → 45.9 → 55.1.
  - There is also a 60-puzzle Daily pool. The [Phase 8 audit](Documentation/benchmarks/campaign_audit_phase8.md) has the details.
- **Perfect Moves.**
  - Every level stores `optimalMoves`, which is the BFS optimum.
  - The HUD shows `moves / perfect` and a star. The completion card shows "Perfect!" or "Sweet Dreams!" with a result line such as "6 moves · Perfect: 4 · +5".
  - The level list labels levels "Perfect".
  - The best result per level is saved. Using a hint during a visit disqualifies Perfect. Undo cannot cheat the counter.
- **Main menu.**
  - Shows the logo, three sleepy Plops, a large PLAY button that resumes the frontier, Daily Gift, Daily Puzzle, Levels, Settings and the Dream Dust balance.
  - A 0.22 s fade leads into gameplay. The gameplay header shows the level title instead of the game name, and a Home button returns to the menu.
- **Dream Dust.** The wallet (`plop.wallet.v1`) uses one-time grant keys:

  | Source | Dust |
  |---|---|
  | Completion | 5 |
  | Perfect | 10 |
  | Daily Gift | 20 |
  | Daily Puzzle | 15 (+10 for a Perfect) |

  There is a cosmetics data model only: no shop, no monetization.
- **Daily features.**
  - The Gift can be claimed once per local calendar day. There are no streaks and no notifications.
  - The Daily Puzzle is chosen from the date, deterministically and offline. It is tracked separately, shows the date under its title, and returns to the menu when done.
- **Save compatibility.** `PLOP_Pack_001_SlotHistory.json` maps old level IDs to the rebuilt slots. `LevelProgress` migrates completed/unlocked/resume IDs at launch. Corrupt meta saves are kept as `*.corrupt`.
- **Board scaling.** The header reserve is now 300 (was 414) and the side margin 3.5% (was 6.25%). Small boards fill about 93% of the safe width.
- **Level Lab.**
  - Added a player-model insight panel, showing first-swipe alternative lengths, dead ends, settlement order, traps and trivial flags.
  - Added a campaign overview with Plop, size and trivial filters, click-to-open and a most-similar comparison.
  - `Bench --insights` exports the same table: [campaign_insights_phase8.tsv](Documentation/benchmarks/campaign_insights_phase8.tsv).

### Phase 8 verification

- **Tests:** .NET 121/121, Unity EditMode 121/121, Unity PlayMode 74/74.
- **Content checks:** strict production verification and a sequential re-search reproduce the pack. The deadlock sidecar was regenerated at 600k states and covers all 100 levels.
- **Android APK:** `/Volumes/PLOP_DEV/Builds/Android/Phase8/PLOP-phase8-dev.apk`, an ARM64 development build. It was installed on the `Medium_Phone_API_37.0` emulator (1080×2400), because no phone was connected.
  - The menu, Daily Gift (+20 once, then "Tomorrow"), Daily Puzzle, Worlds and the level list were exercised by touch.
  - Levels 1–4 were completed with keyboard input, giving one non-Perfect result card.
- **Screenshots:** [Phase 8 screenshots](Documentation/Previews/Phase8/README.md).
- **Not verified:**
  - A physical phone.
  - iOS for Phase 8.
  - Hint timing on a device for the largest 4-Plop boards (worst start 99k states / 34 ms on desktop; the budget is 300k states / 200 ms).

### Phase 8 next steps

- Playtest levels 11–30 for the Easy → Normal ramp. Levels 12 and 13 are still greedy-solvable by design (breathers).
- Optional: gesture demonstrations instead of text in tutorials 1–3.
- Possible new mechanics for later phases are listed in [Puzzle quality](Documentation/PUZZLE_QUALITY.md#recommended-future-mechanics).

## Continue development

Start with [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md) and [ARCHITECTURE.md](ARCHITECTURE.md). Rebuild content with `Bench --production`, then `--deadlocks … 600000`. Never delete `PLOP_Pack_001_SlotHistory.json`.
