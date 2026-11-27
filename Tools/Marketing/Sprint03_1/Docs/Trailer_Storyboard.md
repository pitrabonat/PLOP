# PLOP! Official Trailer V2: storyboard

The trailer is 1920×1080, 30 fps, **30.67 s** long, cut to 90 BPM (1 beat = 0.667 s, 1 bar = 2.667 s). Frames from every half-second are in `../09_QA/Trailer_V2_ContactSheet.jpg`, and the exact timings are in `Trailer_Edit_Timeline.json`.

## Shot list

| # | Time | Shot | Purpose | Caption |
|---|---|---|---|---|
| S01 | 0.00–2.00 | **The world wakes up.** The camera booms down and pushes in over the island, which is split into five depth layers (sky, far cliffs and waterfall, fan and portal, ground, foreground) with parallax and depth of field. The blossom canopy and lantern hang in the near foreground and sway. The waterfall flows, the fan idles, the vortex turns and lava bubbles. The pink Plop sleeps on the ledge, eyes closed and breathing. The first frame is fully lit; there is no black opening. | Establish a living world | none |
| S02 | 2.00–5.33 | **Meet the hero.** A macro close-up with the island out of focus behind. The pink Plop, animated by the game's own rig, opens its eyes, blinks, smiles, then squashes and hops, with a contact shadow. The mint friend behind wobbles and bounces in reaction. | Make the character alive | none |
| S03 | 5.33–8.00 | **Adventure.** The fan spins up with motion blur and the wind ribbons loop back across the ice. The hero, surprised and then excited, slides across the ice and stones towards the portal while the camera follows. The ice shows a soft reflection. | Show the world's mechanics as story | none |
| S04 | 8.00–9.33 | **Portal.** The hero warps into the vortex and the camera pushes through it. A spiral tunnel fills the frame, then a circle opens **out of the in-game portal tile** on the real L057 board. This is a match cut with no white flash. | Bridge cinematic → gameplay | none |
| S05 | 9.33–13.33 | **Layout A.** Level 57 (portals and lava) fills a 904×1000 board. Two Plops move on every swipe, one hops through a portal, and one reaches its Nest and falls asleep. The headline sits in the right column. | Core mechanic | ONE SWIPE. / MANY POSSIBILITIES. |
| S06 | 13.33–17.33 | **Layout B, then layout C.** First, two boards side by side: Frost (L019, sliding ice) and fans (L027). Then a single large board: L046 with a wrong swipe into lava, including the in-game "Oops! Too hot!" dialog for only 0.27 s. An undo sound plays, and the L046 solution follows as the recovery. | Strategy and consequence | THINK BEFORE / YOU SWIPE. |
| S07 | 17.33–21.33 | **Layout D, then layout E.** First, two worlds side by side: Moonlit Clouds (L144) and Three Plops (L102). Then the 15-move Challenge (L155) plays out to the real in-game **Perfect!** card. | Depth and variety | LITTLE PLOPS. / BIG PUZZLES. |
| S08 | 21.33–24.00 | **Make them yours.** The hero wears a different real game cosmetic on each beat: bow, flower, crown, then nightcap. Each change lands with a squash and a soft sparkle. | Customisation | MAKE THEM / YOURS. |
| S09 | 24.00–28.00 | **Sweet dreams.** A separate night composition: moon, stars, cloud bed, and the canopy lantern as a warm light. Four Plops hop into their pillow Nests and grow sleepy. Their eyes close **fully**, one after another, and they breathe. Small z's rise only above Plops whose eyes are already closed. The shot is relit for night with cool ambient light, a moon rim and lantern warmth, and the camera pushes in slowly. | Emotional resolution | SWEET DREAMS. (26.75–27.95, after the hero's eyes close) |
| S10 | 28.00–30.67 | **End card.** The night stays on screen with the focus pulled and slightly dimmed. The approved logo (`01_Jelly_Pink.png`) lands with a jelly squash and "Sleepy Puzzle" sits below it, followed by the sonic logo. There is no second "PLOP!", no Coming Soon, no Pre-register or Download Now, no date and no badge. | Brand | none |

## How the owner's V1 critique was addressed

| V1 problem | V2 fix |
|---|---|
| Black opening frame | The first frame is the fully lit island; QA measured the longest black run at 0 s. |
| Over-reliance on the illustration | The illustration only appears as a layered background in S01–S04. Every character is the game's real rig. S05–S07 are real gameplay. S08 is the rig with real cosmetics. S09–S10 are a new night composition. |
| Flat pans | Each shot moves its five depth layers by different amounts and uses per-layer depth of field. Boom, push and follow moves are motivated by the action. |
| Static faces | The rig plays real expressions: sleeping, waking, blinking, happy, surprised, excited, sleepy and asleep. It also squashes, hops, wobbles and breathes. |
| Small gameplay surrounded by blur | Boards are up to 1000 px tall on clean pastel gradients, with no blurred footage behind them. Five different layouts are used. |
| Repetitive pill text containers | Headlines have no container: two-line Nunito ExtraBold, plum over berry, with a staggered rise. |
| Overlong failure dialog | The dialog is on screen for 0.27 s, then an undo sound and the recovery. |
| Slideshow-style cosmetics | One real cosmetic per beat on the living rig, with squash on each beat. |
| Sleeping characters with open eyes | Eyes are fully closed before any z appears. The rig is in its Sleeping state, checked frame by frame from the `asleep` flag in `RigRenders/*/meta.json`. |
| Zs over open eyes | The z's are tied to each Plop's asleep flag and appear only after it is set. |
| Repeated title on the end card | The logo appears once, with "Sleepy Puzzle" as the subtitle. |
| Coming Soon badge | Removed completely. No availability claim of any kind remains. |

## Where it came from

- **Island backdrop (S01–S04).** The owner's reference illustration "Pastel Jelly Puzzle Island" (`Owner_Reference_Island_1774x887.png`, probably AI-generated, rights unconfirmed), cut into depth layers by `08_ProductionSources/Cinematic2/layers.swift`. The original Plops painted into the illustration were removed and the gaps filled.
- **Characters.** The game's own `PlopView` rig and `ShapeAtlas` art, rendered in Unity batch mode by `MarketingCapture.cs`. The faces were repainted at capture resolution for close-ups.
- **Gameplay.** iOS Simulator recordings of the dev build replaying verified solutions (from Sprint 03).
- **Procedural elements.** The moss ledges, wind ribbons, portal tunnel, night sky, clouds and moon are drawn in code (`Compositor/Cinematic2.swift`). They are not generated images.
