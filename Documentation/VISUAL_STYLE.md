# Soft Toy Minimalism

PLOP! uses warm colors, soft highlights and clear, physical-looking shapes.
The board is the focus of the portrait screen. The same lightweight art
system serves every board and each Plop color.

## Palette

| Token | Value | Use |
|---|---|---|
| Milk | `#F7F2EB` | Screen and secondary controls |
| Berry | `#E9AEC4` | Berry Plop and primary action |
| Mint | `#A8D7CD` | Mint Plop, slider fill and completion tint |
| Lilac | `#B6ADE3` | Lilac Plop |
| Honey | `#E7BC85` | Honey Plop |
| Ink | `#4B4652` | Text, eyes and control symbols |

`PlopPalette` also defines complementary tiles, walls, tinted shadows, and
warm highlights. Its game-to-art mapping retains Berry, Mint, Honey, Lilac,
Sky and Peach slots. Nests use softened versions of those same colors.

## Board and character materials

| Piece | Geometry | Material treatment |
|---|---|---|
| Floor tile | 0.92-cell edge, corner radius 19% of edge | Satin center and rounded molded lip |
| Pebble wall | 0.86-cell edge, corner radius 22% of edge | Toy-block profile with soft sides and highlight |
| Nest | 0.88-cell edge, corner radius 24% of edge | Padded rim, inset well and a tinted edge |
| Board tray | Nine-sliced, 0.46-world-unit corner radius | Raised rim with inset face and baked shadow |
| Plop | Common asymmetric gel silhouette | Off-center crown, rounded foot, gel rim and gentle highlights |

`ShapeAtlas` rasterizes signed-distance masks with one-pixel edge coverage
straight into a shared RGBA upload buffer. It caches textures by screen
density and releases the readable CPU texture copy after upload. Body, Nest
and neutral shape sprites share the same atlas. A layered face provides
blinking eyes, cheeks, an awake smile, an impact mouth, and a sleeping
expression. Each sprite is anchored to the authoritative grid.

## Motion

| Event or state | Timing and response |
|---|---|
| Idle | Slow breathing, subtle body tilt, and occasional blinking |
| Move | 18 ms anticipation within a 155 ms slide, then a damped settle |
| Collision | 185 ms squash and elastic recoil on the child pose |
| Sleep | Eyes close and the body relaxes into the pillow over 380 ms after landing |
| Completion | A brief 300 ms staggered hop and a 200 ms card entrance. Next Level is immediately available. |

`BoardView` translates `GameEvent` moves, blocked swipes, settlements and
completion into animation. Position interpolation runs between authoritative
endpoints. Child transforms carry the deformation and recoil. Undo and Restart
snap the view to the saved state and clear any unfinished motion.

## Interaction and type

`HudTheme` sets canvas dimensions, corner radii, gaps, type scale, shadow
offset, press feedback and completion timing. The reference canvas is
1080 × 1920 with a 0.5 width/height match. The board fits between the 326-unit
header and 294-unit footer inside the safe area. The modal card scales down on
compact windows. Rounded capsules use a 58-unit design radius and cards use
a 50-unit radius. Control glyphs share a small baked alpha strip.

The top area presents Levels, Settings, the current level, moves and any
tutorial instruction. Undo, Restart and Hint are available along the bottom.
Completion says **Sweet Dreams!** and offers Next Level. Settings contains
independent Music and Sound sliders and the 30/60 FPS choice. Open menus
capture board input.

The HUD embeds Nunito weights 500 and 800 as static Latin subsets. The source
revision is `8c6a9bb9732545b9ed53f29ec5e1ab0ff53c4e6f` and the SIL OFL 1.1
license ships alongside the fonts. [Nunito project](https://github.com/googlefonts/nunito/tree/8c6a9bb9732545b9ed53f29ec5e1ab0ff53c4e6f)

## Audio

`PlopAudio` plays original mono synthesized cues for travel, recoil,
settlement, UI actions and completion. Each effect sounds at most once per
swipe, with quieter layers when several events coincide. The original
16-second harmony loop and the short effects are cached on first use. The
music and effects channels can both be set to 0%.

## Frame budget

Rounded geometry and shading are baked into sprites. Animation uses local
transforms and never modifies game state. Idle render throttling remains
active when feedback has settled. The 30 FPS cap is available under Settings.
Physical-device frame pacing, battery draw and temperatures need profiling
on target hardware.

[Gameplay previews](Previews/README.md)
