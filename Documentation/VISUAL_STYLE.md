# Visual Foundation — "Soft Toy Minimalism"

Phase: Visual Foundation & Gameplay Presentation (branch `feature/visual-foundation`).
All code lives in the `PLOP.Unity` assembly (`Assets/Scripts/Unity`, namespace `Plop.Presentation`).
Gameplay rules, solver, generator and level data were **not** changed.

## Why the prototype looked sharp (root cause)

The placeholder renderer (`PlaceholderArt`, now removed) drew every floor tile and wall with one fully
opaque 64×64 square texture, Nests with a square outline, and HUD buttons as uGUI `Image`s without a
sprite (Unity renders those as hard rectangles). Nothing had a corner radius.

## Rendering approach

| Element | Technique | Corner radius |
|---|---|---|
| Floor tile | `ShapeAtlas` sprite, SDF-rasterized, 1 cell = 1 world unit, tile 0.92 of cell (gap 0.08) | 19% of tile width |
| Pebble (wall) | single baked sprite: tinted soft drop shadow + darker side + lighter top + soft highlight | 22% of block width |
| Nest | per-color baked "pillow": cushion with soft rim, lighter inner well, top highlight | 24% of nest width |
| Board container | 9-sliced rounded panel + 9-sliced soft tinted shadow (`SpriteDrawMode.Sliced`) | 0.40 world units (constant at any board size) |
| Plop | per-color gumdrop body (smooth union of rounded base + dome), separate eyes/cheeks/shadow sprites | organic silhouette |
| UI buttons / card | `UiSprites`: 9-sliced pill (radius 64 canvas units) and card (44 units) + soft shadows, rasterized at 2× | pill / 44 u |

**Why SDF-rasterized runtime sprites** (instead of a custom shader or authored PNGs):
- True geometric rounding with a 1-pixel anti-aliased edge (`Sdf.RoundBox`, coverage = clamp(0.5 − d/pixel)).
- **Density-correct:** `BoardLayout` computes on-screen pixels per cell; `ShapeAtlas.ForDensity` picks a bucket
  (64, 96, 128, 160, 192, 256 px per cell) and rasterizes at that resolution, so edges are crisp on every
  device instead of upscaled/blurry. Atlases are cached per bucket; typical cost is one atlas per session.
- **Batching:** every board sprite lives in one atlas texture → the board renders with very few batches.
- Works with the existing URP 2D renderer and default sprite material (no shader maintenance, no
  post-processing, no full-screen effects).
- Straight-alpha compositing keeps the shape color in fully transparent pixels → no dark/bright fringes.
- Geometry functions (`ShapeAtlas.TileDistance`, `PebbleTopDistance`, `BodyDistance`) are shared by the
  painters and the tests, so tests verify the exact outlines that are drawn.

Visual rounding never affects gameplay: collision is resolved on the logical grid in `PLOP.Core`; sprites
are placed at cell centers from `BoardView.CellToLocal`.

## Palette (`PlopPalette`)

| Name | Hex | Use |
|---|---|---|
| Milk | `#F7F2EB` | background (camera clear color) |
| Berry Pink | `#E9AEC4` | color slot 0 (gameplay `Red`), primary button |
| Mint | `#A8D7CD` | color slot 1 (`Blue`) |
| Honey | `#E7BC85` | color slot 2 (`Yellow`) |
| Lavender | `#B6ADE3` | color slot 3 (`Green`) |
| Sky / Peach | `#A9C7E6` / `#F1B9A3` | slots 4–5 (`Purple`, `Orange`) — extensions for >4 colors |
| Ink | `#4B4652` | primary text, eyes |
| Board panel | `#EDE3D7` | container (warm, darker milk) |
| Tile / shade | `#FCF9F4` / `#F3ECE3` | floor tile gradient |
| Pebble top / side | `#DFD7DF` / `#C8BECB` | lilac-grey toy block |
| Shadow | `#7D7184` (low alpha) | all shadows — tinted, never black |
| Highlight | `#FFFCF7` | all highlights — warm, never pure white |
| Muted text | `#8E8697` | move counter, subtitles |

Gameplay color ids are mapped to palette slots only in `PlopPalette` (presentation concern). Plop bodies are
the slot color pulled 7% toward Ink for readability; Nests are the slot color softened toward Milk
(22% outer, 62% inner) so a Plop always reads as "the thing" and the Nest as "its bed".

## Plop animation (`PlopView`, driven by `BoardView.Update`)

| State | Effect (durations) |
|---|---|
| Idle | slow breathing (2.6 s period, ±1.8%), blinking every 2.4–5.6 s, eyes return to center |
| Move | starts the same frame as the input; 0.13 s ease-out-cubic; 10% squash at start, 15% stretch in flight along the axis, area-conserving across; eyes look toward the move |
| Land | 0.24 s damped settle wobble along the move axis |
| Bump (blocked) | 0.17 s nudge 9% of a cell toward the obstacle and back, 16% squash against it |
| Sleep | when the move lands on a matching Nest: eyes close (soft arcs), body relaxes/flattens over 0.45 s, deeper slower breathing; the Nest gives a 0.35 s 7% pulse |
| Level complete | every Plop does a small staggered hop (0.40 s) then a landing wobble; completion card pops in after it |

Rules followed: the **root position** of a Plop only ever comes from gameplay events (`PlopMoved.To`) or
`Sync(state)`; squash, hops and breathing touch only the body's local transform. A new command while a
move is still animating continues from the drawn position with all Plops starting on the same frame and
the same duration, so Plops never desynchronize or overlap. Undo/Restart/Load snap via `Sync`.

## HUD (`GameHud`)

- Header controls open the level picker and switch between 30 and 60 FPS. Below
  them sit the level title, move count and the tutorial instruction when present.
- The footer has rounded Undo, Restart and Hint pills. Completion leaves Undo
  and Restart available and changes the right-hand pill to Next or Levels.
- Hints show a verified swipe direction above the footer. The completion card
  animates into this area after the Plops' hop.
- The picker pages through ten entries at a time and distinguishes current,
  completed and locked levels.
- Built in code; layout in canvas reference units (1080×1920, match 0.5). Text changes only on events.

## Responsive layout (`BoardLayout`)

- Everything sits inside `Screen.safeArea` (notches, Dynamic Island, punch holes, home indicator).
- The board fits the region between header (298 reference units) and footer (322) with a side margin of
  4.5% of the screen width (keeps it off rounded display corners); cells stay square; the board is centered
  in that region via camera offset; small boards are not blown up (≥ 5.2 cells of virtual width).
- Screen/safe-area changes are detected event-driven (`RectChangeNotifier.OnRectTransformDimensionsChange`)
  → camera refit, sprites rebuilt only if the density bucket changes.
- Verified by tests on 7 screen profiles (16:9, iPhone 15 Pro / Pro Max with Dynamic Island, 20:9 and QHD+
  Android, 720p, iPad 3:4) × 9 board sizes, and by iOS Simulator screenshots.

## Performance notes

- No physics, no soft-body simulation, no post-processing, no particles.
- Per frame: one `BoardView.Update` loop over ≤ 6 Plops doing a few `sin`/`exp` calls; zero allocations.
- Idle render throttling now waits until all wobble/sleep/hop/pulse animations end (`BoardView.IsBusy`);
  idle breathing then renders at the throttled rate (default every 3rd frame).
- Atlas generation is CPU work on level load only when the density bucket changes (cached afterwards).

## Developer hooks (development builds and Editor only)

PlayerPrefs `plop.debug.level` (1-based) and `plop.debug.moves` ("RRUD…") auto-load a level and play
moves — used for automated screenshots (`/Volumes/PLOP_DEV/Tools/sim_screenshots.sh`). Compiled out of
release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
