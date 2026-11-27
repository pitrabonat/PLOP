# PLOP! project state

**Final Sprint 03.1 · Official trailer V2 (awaiting owner approval), then YouTube release and short-form package**

- Read [Documentation/FINAL_SPRINT_03_1_HANDOFF.md](Documentation/FINAL_SPRINT_03_1_HANDOFF.md) first.
- **The V2 trailer is in `/Volumes/PLOP_DEV/Marketing/FinalSprint03_1/02_OfficialTrailer/`.** It runs 30.67 s with a new 2.5D layered island, the game's own Plop rig, five gameplay layouts, a night ending with closed eyes, and no Coming Soon.
- **Approval gate.** Nothing is uploaded until the owner explicitly approves the video, the thumbnail and the visibility.
- **Game code.** Unchanged. The only addition is the editor-only `Assets/Scripts/CharacterTools/Editor/MarketingCapture.cs`, a batch-mode rig renderer used for marketing.

**Final Sprint 03/06 · Launch marketing package (trailers, store assets, listings, social kit)**

- Branch: `sprint/03-marketing`, from Sprint 02 at `b15c45c`. The game code is unchanged since Sprint 02.
- Read [Documentation/FINAL_SPRINT_03_HANDOFF.md](Documentation/FINAL_SPRINT_03_HANDOFF.md) first. Earlier records: [Sprint 02](Documentation/FINAL_SPRINT_02_HANDOFF.md) and [Sprint 01](Documentation/FINAL_SPRINT_01_HANDOFF.md).
- Open in Unity `6000.3.29f1` with **PLOP → Play Game**.
- **The build target was last switched to iOS** (Simulator export); the Android build scripts switch back on their own.

## Sprint 03 (2026-11-27)

- **Marketing package.**
  - The files are in `/Volumes/PLOP_DEV/Marketing/FinalSprint03/`, built by the pipeline in `Tools/Marketing/Sprint03/`.
  - They include the 37 s cinematic trailer, the gameplay and vertical trailers, the App Preview (886×1920, High L4.0, 11.3 Mbps), 5 challenge videos and the mascot showcase.
  - Also: 8 store screenshots at every required size, 3 feature graphics, 5 posters (plus square versions), the social kit, store listings in 8 languages, video captions, launch-announcement drafts, and audio stems.
- **QA.** Across 495 files: 206 FINAL, 40 AWAITING OWNER APPROVAL, 249 DRAFT. No spec issues. See `11_QA/EXPORT_MANIFEST.md`.
- **Owner decisions.**
  - Rights to the reference illustration (it is in the cinematic, feature graphics A and B, two posters and the banners).
  - Sign-off on the store copy.
  - Final logo and icon.
- **Blocked.**
  - Burned-in Cyrillic headlines (no Cyrillic font is bundled).
  - The Google Play video (needs a YouTube upload).
  - All uploading and posting (out of scope by rule).

## Sprint 02 state


- **Audio (the P0 is fixed).**
  - Cause: the content was effectively silent. Ambience sat at about −48 dBFS, there was no music, and the effects were quiet low sine blips.
  - Now: original synthesised music (menu plus 9 Worlds), 9 ambience beds, 30 effects and a sonic logo (`Tools/AudioSynth`).
  - `PlopAudio` has buses, settings, sync and cancellation.
  - Measured audible in a running Android build; mute verified. Nobody has listened to it yet.
- **Localization.** 8 languages with live switching via Settings ▸ Language. Seven translations are AI-drafted and **unverified**. Cyrillic falls back to the OS font. See [Documentation/LOCALIZATION.md](Documentation/LOCALIZATION.md).
- **Settings.** Music 45 %, Effects 80 %, Ambience 30 %, Master mute, frame rate, haptics (on), reduced motion, Language, Restore Defaults.
- **Branding.** Provisional (Hero_Pink icon, Jelly Pink logo, Day splash). The alternatives are in `/Volumes/PLOP_DEV/Marketing/Branding`.
- **Tests.** .NET 203/203, Unity EditMode 203/203, PlayMode 162/162.
- **Builds.** `/Volumes/PLOP_DEV/Builds/FinalSprint/02_FinalProduction/` (see `BUILD_MANIFEST.md` there).
- **Content.** Unchanged: `PLOP_Pack_003` (160 levels) and `PLOP_Daily_002` (60).
- **Still open.**
  - Human listening.
  - Native-speaker review.
  - Bundling full Nunito.
  - Owner branding choice.
  - Physical-device checks.
  - Production signing.
  - The art and animation refinement that was carried over.

---

## Earlier: Phase 11.5

**Phase 11.5 · Environmental art, advanced VFX and living Worlds (presentation only)**

The project is on `feature/living-worlds`, branched from the Phase 11 work (`feature/gameplay-mastery`, commit `3a92469`; tag `checkpoint/pre-phase11.5`). Nothing from Phase 11 or 11.5 is pushed. Open the project in Unity `6000.3.29f1` with **PLOP → Play Game**, or **PLOP → Visual Lab → Preview Window** for the environment preview. **The build target was last switched to iOS** (Simulator export); the Android build script switches back on its own.

Previews (real captures, not concept art): [Documentation/Previews/Phase11_5/README.md](Documentation/Previews/Phase11_5/README.md)

## Implemented in Phase 11.5

- **No rule, solver, level, price or UI-layout change.** 0 campaign or Daily levels modified (Pack_003 and Daily_002 byte-identical). Header, Home, Settings, level number, move counter, Limited Moves pips, Undo/Restart/Hint and board placement are untouched.
- **Mechanic shaders** (`Assets/Art/Resources/Shaders`, one shared material each, per-renderer data packed in the SpriteRenderer colour): `PlopLava` (flowing molten surface, glowing cracks, cooled-stone rounded rim, bubbles; adjacent cells merge into one pool), `PlopFrost` (translucent crystalline ice, cracks, frosted edge, adjacency-aware for isolated/straight/corner/T/cross/area without thick borders), `PlopWind` (curved streaks and particles aligned with the fan, length = push run), `PlopPortal` (rounded frame, luminous ring, vortex, pair pips besides the tint). `SurfaceConnectivity` computes 8-neighbour masks, shapes and wind runs. If a shader is unsupported the atlas sprites are used.
- **Fans**: rounded housing, recognisable blades, continuous rotation paused when hidden or under Reduced Motion; 1–3 strength chevrons readable without animation; airflow never drawn through obstacles.
- **Interaction VFX follow the Core trace** (`BoardEnvironment` + `BoardFx` pool of 40 renderers + `TraceTiming`): icy trail, stretch and frost particles on slides; wind gust, stretch and settle on pushes; shrink → entry swirl → exit reveal on teleports (blocked exit: small wobble, no exit effect); warm puff, ripple and surprised face before the unchanged Lava failure card. Long chains compress (`CompressAfter` 2, factor 0.8, min 0.6). `Sync` (Undo, Restart, recovery, level/World change) clears every pending effect; the one-command input buffer is unchanged.
- **Audio**: seven new synthesised cues (ice slide, fan whoosh, portal in/out/blocked, lava hiss, quiet lava bubble) raised by the trace, through the existing `PlopAudio` and its settings.
- **Board materials per World** (`Resources/Worlds/BoardMaterials.json`, `ShapeAtlas.WorldSurfaces`): tray, floor tile and pebble painted per World on a worker thread with the shared geometry (Cozy satin cream tray and toy pebbles; Frosted ice tray, frosted tiles, ice pebbles with snow caps; Meadow wood tray, mossy stones; Ember dark stone tray with an amber seam, speckled tiles, stones with ember fissures; Prism glass tray, glazed tiles, crystal pebbles; Dewdrop sage ceramic with dew-capped stones; Rainy misted glazed ceramic; Attic warm wood with toy blocks; Moonlit celestial ivory with cratered moon pebbles). Nests gained a recessed bed. Two World atlases cached at most.
- **Living Worlds** (`Resources/Worlds/Ambient.json`, `WorldAmbient`, `PlopAmbient`, `PlopAmbientParticles`): far = authored backdrop; mid = a few region quads (windmill sails with lattice frames, grass, prism rays, attic beam, clouds, ember glow); near = one static particle mesh (≤ 58 quads per World today, 64 max per set: motes and bokeh, pollen and glints, rain streaks and drops, attic dust, stars, snowflakes, dandelion seeds, embers, prism glints). Clean centre over the board, no camera motion, one clock, only the current World exists, Battery Saver thins (density 0.55, speed 0.7), Reduced Motion freezes.
- **Tools**: development-only Visual Lab boards (`debugprefs.py lab <n> [world] [moves]`, compiled out of release), editor-only **PLOP → Visual Lab → Preview Window** (board, World, time scrubber, Reduced Motion, Battery Saver, Play in Editor) and **PLOP → Visual Lab → Capture Close-ups** (`EnvironmentCapture`).
- **Tests**: `Phase11_5EnvironmentTests` (18): adjacency, Lava diagonals, mask packing, edge selection, wind direction/alignment and pips, paused blades, portal pairs, trace order of the combined demo, effect cleanup on Sync, teleport landing, Reduced Motion, chain compression, every World's materials and ambient, grid unchanged, async World change, ambient lifecycle and particle budget.

## Verification (executed 2026-11-26)

- **Tests:** .NET 200/200, Unity EditMode 200/200, Unity PlayMode 124/124 (Phase 11.5 suite 18/18), all on the final code. Level packs untouched (0 levels modified).
- **Android:** `/Volumes/PLOP_DEV/Builds/Android/Phase11_5/PLOP-phase11_5-dev.apk` (ARM64 development build, 46.7 MB, +0.2 MB) on the `Medium_Phone_API_37.0` emulator (SwiftShader software rendering); no phone was connected. Screenshots in `Documentation/Previews/Phase11_5/` (the emulator's own recorders only reached 2–6 fps under software rendering, so recordings were made on the iOS Simulator).
- **Performance (emulator, same protocol before/after, diagnostics overlay):** see [TESTING.md](Documentation/TESTING.md#phase-115-results-2026-11-26-executed). Loop FPS mean of four readings — L21 33.6 → 34.7, L35 36.8 → 30.9, L155 35.1 → 32.9; draw calls 12–13 → 18–31; Unity heap 78.7–79.0 → 79.7–80.0 MiB; PSS within ±10 MB. The emulator renders on the CPU, so these numbers are noisy and pessimistic for shader cost; no physical device was measured.
- **iOS:** Simulator project exported (development) to `/Volumes/PLOP_DEV/Builds/iOS/Phase11_5/PLOP-Xcode-Sim` and built by Xcode 26.3 (**BUILD SUCCEEDED**); installed and run on the iPhone 15 Pro Max simulator (Visual Lab boards inside the safe area). The recordings in `Documentation/Previews/Phase11_5/recordings/` were made there with `simctl io recordVideo`. No device build or signing.

## Not done / known limits (Phase 11.5)

- **No physical device**, so no real GPU, battery or thermal numbers. Smooth 60 / Battery Saver 30 behaviour was verified in tests and on the emulator only.
- Draw calls rose (World surfaces use their own atlas, plus ambient regions and the particle mesh). L35 averaged ~16 % lower Loop FPS on the software-rendered emulator; worth profiling on a mid-range phone.
- Multi-cell Frost paths in campaign levels were not added (optional; would require re-verification and new optimums).
- Art is procedural/shader-drawn by the team's own tools; it is production-direction placeholder quality, not final illustrated art.
- No human playtest.

## Phase 11 summary

**Phase 11 · Gameplay mastery: a rebalanced 160-level campaign, a mechanic-tiered Daily pool, fair hints, quality tooling and QA**

Reports: [Campaign audit](Documentation/Phase11/CAMPAIGN_AUDIT.md) · [Puzzle balance](Documentation/Phase11/PUZZLE_BALANCE_REPORT.md) · [Economy](Documentation/Phase11/ECONOMY_BALANCE_REPORT.md) · [Playtest guide](Documentation/Phase11/PLAYTEST_GUIDE.md) · [Performance](Documentation/Phase11/PERFORMANCE_REPORT.md) · [QA results](Documentation/Phase11/QA_RESULTS.md) · [Known issues](Documentation/Phase11/KNOWN_ISSUES.md) · [Previews](Documentation/Previews/Phase11/README.md)

### Implemented in Phase 11

- **Measurement first.** `PuzzleQuality` (decision density, coordination, settlement order, mechanic removal test, planning depth, misleading choices, route efficiency, solution diversity → quality 0–100), `SimulatedPlayers` (Random, Greedy, Look-ahead 2/3, Limited-information, Optimal) and `QualityAudit` (per-level JSON/markdown, similarity, flags). `Bench --quality-audit`, Level Lab **Balance & quality**.
- **Campaign `PLOP_Pack_003`** (`Bench --balanced`, deterministic): 95 levels kept with the same id, 1 improved, 57 replaced, 7 authored (L2, L4, L6–L10). Mean quality 60.6 → 71.6; greedy solves 14 → 8 (only tutorials and Discovery levels); planning depth > 4 on 103 levels. Frost is discovered at L6; each mechanic World runs Discovery → Application → Reasoning → Combination → Mastery. Limited Moves: eight challenges, slack +2 (30, 45, 60) → +1 (80, 100, 120) → exact (140, 155).
- **Save migration** Pack_002 → Pack_003: same-id completions kept, access up to the old frontier, the resume level kept when it still exists, `aliases` for boards whose Move limit only loosened. Never invents a completion; the old save is untouched. Verified on the emulator's real Phase 10 save.
- **Daily `PLOP_Daily_002`**: 20 strong classic puzzles kept plus 40 new across Frost, Breeze, Lava and Loops tiers. A tier is served only after the player completed its Discovery level (6, 21, 35, 49). The pick is stored per day, there is one reward per date, no streaks and no move limits.
- **Economy**: rewards and prices unchanged. `HintPolicy` grace: after two tries, the first hint on a level is free once when Dust is short. `EconomySimulator` (`Bench --economy`) shows no standard profile is refused a hint and that Casual, Regular and Enthusiast players can buy all 18 Dust-priced cosmetics by Level 160 (median).
- **Readability**: Loop pairs carry 1–6 pips besides their tint; fan power pips are larger with a halo.
- **Player flow**: Android's system Back now works everywhere (it did nothing before): it closes Studio and modals, returns from a level to the menu, and backgrounds the app from the menu.
- **Dev-only tools**: local playtest metrics CSV (`debugprefs.py playtest on`, `--playtest-summary`; no SDK, compiled out of release), `debugprefs.py diagnostics on`.
- **Tests**: .NET 200, EditMode 200, PlayMode 106 (21 + 6 new): campaign, Daily gating, economy, save fuzzing, random sessions on all 220 shipped boards, migration, layout on six screens, system Back.

### Phase 11 verification (executed 2026-11-26)

- **Tests:** .NET 200/200, Unity EditMode 200/200, Unity PlayMode 106/106. `VerifyProductionPack` passes on Pack_003 (160) and Daily_002 (60); `--balanced` is byte-identical on rerun.
- **Android:** `/Volumes/PLOP_DEV/Builds/Android/Phase11/PLOP-phase11-dev.apk` (ARM64 development build, 46.5 MB) on the `Medium_Phone_API_37.0` emulator; no phone was connected. Covered: the real Phase 10 save migrating on upgrade, every mechanic, Limited Moves, the Lava card, Perfect on Level 155 by real swipes, Daily, Studio, Worlds, Settings and system Back. Frame time on the emulator: ~57 FPS, main thread 0.7 ms on Level 155.
- **Found on the emulator and fixed:** Android's system Back did nothing; the first fix missed the first press after launch and was reworked to use an `InputAction`. A Gradle packaging cache had inflated APKs (Phase 10: 64 MB for 46 MB of content).
- **iOS:** Simulator project exported and built by Xcode 26.3 (**BUILD SUCCEEDED**); ran on the iPhone 15 Pro Max simulator (iOS 26.3): Phase 10 save migrated (55 Dust, PLAY → Level 123), Levels 57 and 120 inside the safe area. No taps (that tool needs a system `xcode-select` change, not made). No device build.
- Details: [QA_RESULTS.md](Documentation/Phase11/QA_RESULTS.md), [PERFORMANCE_REPORT.md](Documentation/Phase11/PERFORMANCE_REPORT.md), [previews](Documentation/Previews/Phase11/README.md).

### Phase 11 known limits

- **No human playtest.** Every difficulty, quality and economy number is from heuristics and simulated players.
- **No physical device**: Android was checked on the API 37 emulator and iOS on the Simulator. Battery and thermal behaviour were not measured.
- Remaining balance flags and minor issues: [KNOWN_ISSUES.md](Documentation/Phase11/KNOWN_ISSUES.md).

## Phase 10 summary

**Phase 10 · Puzzle evolution: Frost, Lava, Breeze, Loops, a 160-level campaign over nine Worlds, Limited Moves and the Dream Dust economy**

The project is on `feature/puzzle-evolution`, branched from the pushed Phase 9 work (`feature/character-studio`, tag `checkpoint/pre-phase9` marks Phase 8). Open the project in Unity `6000.3.29f1` with **PLOP → Play Game** or `Assets/Scenes/Gameplay.unity`. The game opens on the main menu. **The build target was last switched to iOS** (for the Simulator export); the Android build script switches back on its own.

### Implemented in Phase 10

Rules: [Game rules — Phase 10 mechanics](Documentation/GAME_RULES.md#phase-10-mechanics). Content: [Level generation](Documentation/LEVEL_GENERATION.md) and the [per-level report](Documentation/benchmarks/campaign_phase10.md).

- **Mechanics, all inside `MoveRules.Apply`:**

  | Mechanic | Rule |
  |---|---|
  | Frost | A Plop that ends its step on Frost keeps sliding until a wall, the edge, another Plop or its matching Nest stops it |
  | Lava | Entering Lava ends the attempt: "Oops! Too hot!" with Undo Move and Restart Level. No penalty, no auto-restart |
  | Breeze fan | Pushes a Plop that ends on it `power` (1–3) cells in the fan's direction; redirects a slide; each fan acts once per Plop per swipe, so fans never chain-push forever |
  | Loops | Paired portals: a Plop arriving on one end appears on the other, keeping its direction; an occupied exit cancels the teleport; one teleport per swipe |

  Precedence is fixed (Lava → matching Nest → Loop → fan → Frost), one swipe is one move however long the chain, and a step budget throws instead of looping. `ForcedChainInspector` checks every reachable state of the shipped campaign: no loop risk.
- **Limited Moves:** optional `moveLimit` per level. The HUD shows `moves / limit`, and "Out of moves!" offers Undo or Restart. Five challenges (levels 30, 60, 90, 120, 150) use optimal + 2 and are solver-verified.
- **Campaign: 160 levels, nine Worlds** (`PLOP_Pack_002.json`, all verified, BFS `optimalMoves` stored):

  | World | Levels | Mechanics | Avg challenge |
  |---|---|---|---|
  | Cozy Nest | 1–6 | basics (Phase 8 tutorials, same IDs) | 8.3 |
  | Frosted Pond (new) | 7–20 | Frost | 24.9 |
  | Breezy Meadow (new) | 21–34 | Breeze, Frost | 28.0 |
  | Ember Hollow (new) | 35–48 | Lava, Frost, Breeze | 33.8 |
  | Prism Observatory (new) | 49–63 | Loops, Lava | 34.9 |
  | Dewdrop Garden | 64–85 | two-mechanic mixes + classic | 40.6 |
  | Rainy Window | 86–108 | harder mixes | 47.5 |
  | Twilight Attic | 109–133 | three mechanics, 6×7 | 53.7 |
  | Moonlit Clouds | 134–160 | all four | 61.1 |

  Each mechanic is introduced by a tiny authored board with one short line (levels 7, 21, 35, 49), then a twist, then generated levels. Every level in a mechanic World uses its mechanic on the optimal route; levels 64–160 are never solved by the greedy player. 13 Phase 8 pure-logic puzzles plus tutorials 1–5 keep their IDs. **160 quality levels were reached** (the scores are heuristics, not human playtests).
- **Worlds:** four new World assets with generated portrait backdrops, cards and ambience beds; the five original World IDs are unchanged.
- **Presentation:** mechanic tiles are baked into the board atlas with shape cues (faceted ice, scalloped Lava rim, fan housing with spinning blades, direction chevron and power pips, six Loop ring tints). Forced chains play as queued steps (slide, push, teleport, settle, burn) and hold the input gate; Reduced Motion lands them at once.
- **Dream Dust economy** (`RewardRules`): completion 8, Perfect +4, Daily Gift 10, Daily Puzzle 12 (+6 Perfect), hint 15. Hints are free on levels 1–6 and mechanic intros. A paid hint is charged only after a verified route for the unchanged board, once per board state (`hint:<level>:<state>` spend key, atomic write); insufficient Dust, a failed search or a stale result charge nothing.
- **Cosmetic store:** a Board category (content version 2): World Colors (free), Honey Tray, Mint Garden, Peach Quilt, Lavender Dream (60–100 Dust) and Starlight (4 Worlds finished, or 150 Dust). Board decorations only tint the tray and tiles. Purchases stay one atomic wallet write. No ads, no real-money purchases.
- **Save migration:** the new pack imports Phase 8 progress once. Dust, records and cosmetics are untouched; completions are kept only for puzzles that still exist; access is kept as levels beaten + 1; the old save is never rewritten.
- **Level Lab:** Frost/Lava/Fan/Loop tools (fan direction and power, Loop pair), move limit, mechanic quality search, effect-resolution debugger (per-swipe event chain in replay, plus "Try a swipe"), forced-chain and loop-risk inspector. Editor only.
- **Mobile performance:** no solver or generation work in `Update`; forced movement is bounded by the step budget; mechanic art shares the existing atlas and material.

### Verification (executed 2026-11-25)

- **Tests:** .NET 179/179, Unity EditMode 179/179, Unity PlayMode 100/100 (one earlier full run had a 5 s timeout in `DeadlockRecoveryTests.ANewSwipeSupersedesTheOldAnalysis` while an APK build and the emulator loaded the Mac; it passed alone twice and in the final full run). `Bench --verify-production` passes on Pack 002 (160 levels).
- **Android:** `/Volumes/PLOP_DEV/Builds/Android/Phase10/PLOP-phase10-dev.apk` (ARM64 development build, 64 MB) on the `Medium_Phone_API_37.0` emulator (1080×2400). No phone was connected. Exercised: every mechanic and the main combinations (levels 12, 24, 27, 30, 35, 39, 43, 52, 66, 157), the Lava card, the Limited Moves counter, a paid hint, World selection, the Studio Board tab with a locked preview, and the automatic migration of the emulator's Phase 9 save (25 Dust kept; 3 of 5 old completions still exist and were kept; PLAY resumes at Level 6). Screenshots: [Phase 10 previews](Documentation/Previews/Phase10/README.md).
- **Found on the device, fixed and re-verified:** the first APK showed an empty board because `Gameplay.unity` still serialized the archived Pack 001 path (Editor tests supply their own levels, so they passed). The scene, a controller fallback and a regression test fix it. The paid hint now states the charge; Board thumbnails and the Studio preview were corrected.
- **iOS:** Unity exported the ARM64 Simulator project (`/Volumes/PLOP_DEV/Builds/iOS/Phase10/PLOP-Xcode-Sim`) and Xcode 26.3 built it: **BUILD SUCCEEDED**. It ran on the iPhone 15 Pro Max simulator: the main menu (its older save migrated: 55 Dust, PLAY → Level 123), Level 157 with all four mechanics, and the Lava card on Level 35, all inside the Dynamic Island safe area ([iOS captures](Documentation/Previews/Phase10/ios/)). Taps were not driven on iOS (the simulator tap tool needs a system-wide `xcode-select`, which was not changed). The rendering path is unchanged: the same runtime atlas and default sprite material. No device build.

### Not done / known limits

- **Not verified on hardware:** no physical phone was connected; frame time for long forced chains on a phone is unmeasured.
- **Difficulty** is the player-model score, not a human playtest. Levels 11, 16 and 25 are intentionally greedy-solvable breathers.
- **Art** for the new Worlds and mechanics is procedural (SDF tiles, generated backdrops and ambience), not hand-illustrated.
- **Board decorations** tint the tray and tiles only; they do not change board shapes or add patterns.

## Phase 9 summary

### Implemented in Phase 9

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

### Phase 9 verification (executed 2026-11-25)

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

### Phase 9 known limits

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
