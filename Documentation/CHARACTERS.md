# Plop characters and cosmetics (Phase 9)

## Two separate identities

| Gameplay identity (Core, never cosmetic) | Cosmetic identity (presentation only) |
|---|---|
| Logical colour, which decides the matching Nest | Body style, finish, colour palette |
| Board position, awake/asleep state | Face style, accessory |
| Movement, collisions, settling, completion | Nest appearance, move effect |
| Solver, verification, deadlock proofs | Personality (blink rhythm, breathing, wobble) |

Cosmetics never reach `MoveRules`, the solver, saves of progress or level data:

- **Board positions.** `PlopView` only ever follows the endpoints `BoardView` supplies. Undo and Restart snap the root to the exact cell.
- **Identity cue.** Every body and its Nest carry the same colour-independent match symbol (heart, drop, sparkle, leaf, diamond, flower), whichever palette or finish is equipped. PlayMode tests check that each body sprite still follows its logical colour.
- **Scope.** The look is global: every Plop in a level wears the same style, finish, face, accessory, Nest style and effect. Each Plop's colour comes from its logical colour through the equipped palette.

## Rendering approach

The project already rasterizes signed-distance shapes into one runtime atlas (`ShapeAtlas`) drawn by SpriteRenderers, so Phase 9 extends that pipeline instead of adding a shader:

- **Look.** `CharacterArt` holds the recipes. A `CharacterLook` is one recipe per slot.
- **Atlas.** `ShapeAtlas.ForDensity(ppu, look)` bakes the board plus that look's bodies, faces, accessory, Nests and effect into the same texture.
- **Batching.** Everything on the board uses one texture and one material, so it batches. A PlayMode test asserts this.
- **What "jelly" means here.** The jelly is baked shading, not light transport:
  - thin rims scatter a lighter tone, while the thick centre keeps the tint;
  - there is a soft upper-left specular and a secondary highlight;
  - floor bounce light falls on the skirt;
  - alpha is 0.82–1.0, thicker in the middle.
- **Finishes.** Finishes are blend weights in `JellyFinish`:

  | Finish | Character |
  |---|---|
  | Classic | The reference jelly |
  | Frosted | Wide, soft highlight; slightly creamy |
  | Milky | Opaque and creamy |
  | Pearl | Angle-dependent lilac, mint and honey sheen |
  | Dewdrop | Clear edge glow, droplet highlights |
  | Velvet | Matte centre with a soft grazing rim |

- **Deformation.** The jelly feel comes from authored squash and stretch (anticipation, travel stretch, landing wobble, recoil, settle, celebration hop). There is no soft-body physics. Face, brows and accessory are children of the body transform, so they deform with it and never drift.

## Character rig: `PlopView`

One rig serves everywhere: SpriteRenderers on the board, UI Images in the main menu and Studio (`PlopPart`).

- **Parts.** Shadow, Body, CheekL/R, Mouth, EyeL/R, BrowL/R, Accessory.
- **Expressions.** Ten expressions (`Expression`): Neutral, Happy, Curious, Surprised, Worried, Sleepy, Sleeping, Excited, Confused, Collision. Each is data (`FaceFeatures`): an eye shape for each eye, a mouth, a brow pose and a cheek strength.
- **In gameplay.** The rig chooses the expression itself:
  - Collision on a hard wall bump; a squint on a soft Plop bump;
  - Sleeping when settled;
  - Happy during the completion hop;
  - otherwise the style's neutral face.
- **Studio and tools.** They can hold an expression with `SetExpression`.
- **Reactions.** `React` provides Bounce, Surprised, Wobble, HappyBlink, Squash and Sleepy. Callers rate-limit them (0.45–0.5 s cooldown).
- **Personality per style.** Blink interval, breathing amount and period, idle wobble, reaction strength, relaxed neutral lids. A per-Plop seed staggers blinks.
- **Reduced Motion.** Still pose, no move effects; reactions change only the face.
- **Allocations.** Feature sprites are cached per rig. The measured `Tick` allocates 0 bytes over 1,000 frames (heap-growth counter) and costs about 0.4 µs per Plop per frame in the Editor.

## Content

| Category | Items (catalog ID → recipe) |
|---|---|
| Body style | Classic, Bouncy (taller, more elastic, livelier), Snoozy (wider, flatter, relaxed lids, slower rhythm) |
| Finish | Classic, Frosted, Milky, Pearl, Dewdrop, Velvet |
| Palette | Toy Box (shipped colours), Pastel (the approved brief colours), Sorbet (brighter fruit tones) |
| Face | Sweet, Button, Dozy, Blush (cat mouth, larger cheeks), Starry |
| Accessory | None, Tiny Bow, Little Flower, Sprout Leaf, Ribbon, Little Star, Nightcap, Cloud Puff, Tiny Crown |
| Nest | Moon Pillow (classic), Cloud Cushion, Marshmallow, Soft Flower, Crescent Bed |
| Effect | None, Dust Puff, Sparkle, Bubbles, Star Trail (2–3 particles, 0.34–0.42 s, pooled) |

**Catalog.** The catalog is data: `Assets/Data/Cosmetics/Resources/Cosmetics/PLOP_Cosmetics_001.json`.

- **Fields per item:** stable ID, category, name, asset recipe, preview (`auto` renders the asset), unlock method (Free / Dust / Milestone), Dust cost, milestone kind and count, `since` content version, optional compatible styles.
- **ID rules:** IDs are save keys. Never rename or reuse one.

**Economy.**

- **Free from the start:** Classic body, Classic and Frosted finish, Toy Box and Pastel palettes, Sweet and Button faces, Tiny Bow, Moon Pillow, Dust Puff.
- **Prices:** 40–250 Dream Dust.
- **Milestones:** levels completed, Perfects, Worlds finished, Daily Puzzles solved. Most milestone items can also be bought with Dust.
- **Never included:** paid currency, randomness or streaks.

**Adding a cosmetic.** Add a recipe in `CharacterArt` (and a painter case if it needs a new shape), add a catalog entry, then run the validator. No prefab, rig, material or code in the UI is needed.

## Ownership, transactions and saves

- **Ownership (`CosmeticCollection`).** An item is owned if it is free, recorded in the wallet, or its milestone is met. Met milestones are recorded once in the wallet's owned list.
- **Unlocks.** `TryUnlock` checks, in order: item exists, category, not owned, for sale, affordable. Then `DreamDustWallet.TryPurchase` spends Dust and records ownership in **one** write.
- **Save failures and torn writes.**
  - If a save throws, in-memory state rolls back.
  - Each successful write is mirrored to `plop.wallet.v1.bak`.
  - A torn or corrupt main record is kept as `.corrupt`, and the last good backup is restored. So Dust and ownership can never disagree.
- **Appearance (`plop.appearance.v1`).** Stores `{version, contentVersion, active, presets[]}`. One preset is used today; up to 4 fit the format. Unknown, removed, unowned or incompatible items fall back to the category default. A corrupt record is kept as `.corrupt`.
- **Untouched:** campaign progress, records and Dream Dust.
- **Migration.** Phase 8 saves (wallet without a backup, old placeholder owned IDs) load unchanged. Unknown owned IDs are preserved.

## Character Studio

To get there: **Main menu → Studio** (a lilac pill beside Levels, smaller than PLAY).

- **Header:** Back, title, Dream Dust.
- **Preview room:** one large Plop (380 px per cell) wearing the previewed look. Tap it for reactions, with a 0.5 s cooldown. The Nest tab shows the Plop beside its Nest; the Effects tab plays the effect.
- **Category row:** Body, Colors, Finish, Face, Accessories, Nest, Effects (scrolls sideways).
- **Item grid:** 3 columns of thumbnails, each item drawn on a classic Plop. Each card has a name and its state: Free/Owned, Equipped (check), or locked (lock icon plus price or milestone text).
- **Preview before unlocking:** selecting any item, owned or not, previews it without changing what is equipped.
- **Main action:** Equip / Unlock N, then "Tap to confirm" (3 s window) / Equipped / "Need N more" / the milestone condition. A successful unlock equips the item and shows a toast.
- **Back:** returns to the main menu. The board and menu mascots then wear the new look; atlases of old looks are released.
- **Memory:** preview and thumbnail atlases exist only while the Studio is open.

## Tools (Editor only, `PLOP.CharacterTools`)

- **PLOP → Characters → Animation Lab** drives the shipped `PlopView` in an isolated preview scene:
  - every cosmetic slot and colour, plus held expressions;
  - moves, wall and Plop bumps, settle, wake, celebrate, Undo easing and all reactions;
  - speed, Reduced Motion and a dark backdrop;
  - read-outs of the accessory attachment and atlas cost.
- **PLOP → Characters → Validate Cosmetics** runs `CosmeticContentValidator`. It checks:
  - catalog data: IDs, categories, prices, defaults, compatibility;
  - registered recipes;
  - accessory size and attachment;
  - palette distinctness (minimum RGB distance 0.11);
  - finish ranges, personality ranges, and faces placed on each style;
  - every expression's sprites, and every Studio thumbnail.
  - Batch: `-executeMethod Plop.CharacterTools.CosmeticValidatorWindow.RunBatch` exits 1 on any problem.
- **PLOP → Characters → Capture Reference Sheets** renders the runtime rig to PNG. Batch: `-executeMethod Plop.CharacterTools.CharacterCapture.CaptureBatch` with `PLOP_CAPTURE_DIR`. The sheets in `Documentation/Previews/Phase9/sheets` come from this tool.

## Measured cost (Editor, Apple silicon; phone timings not measured)

| Item | Value |
|---|---|
| Gameplay atlas at 128 px/cell | 1024×512, 2 MB, 317 ms bake |
| Studio thumbnails (128 px/cell) | 229 ms bake |
| Studio preview look (320 px/cell) | 157 ms bake, cached up to 4 looks |
| `PlopView.Tick` | ~0.4 µs per Plop per frame; 0 bytes allocated over 1,000 frames |
| Move effects | ≤ 12 pooled sprites; none while idle, none under Reduced Motion, half in Battery Saver |

**Known limits:**

- **Bake time on a phone.** Bakes are main-thread C#, so a phone will take several times longer. A look change rebakes the gameplay atlas once, and opening the Studio costs one thumbnail bake. Neither is measured on device.
- **Art quality.** All art is procedural SDF. It is clean and consistent at every size, but it is not hand-painted illustration. Finishes differ subtly by design.
- **Pipeline ready for real art.** For production art upgrades, `CharacterArt` recipes and the `ShapeAtlas` painters are the import point. Swapping a painter for a sampled texture keeps keys, rig and catalog unchanged.
