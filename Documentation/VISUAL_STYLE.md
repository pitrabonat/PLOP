# Soft Toy Minimalism

PLOP! uses warm colors, soft highlights and clear, physical-looking shapes.
The board is the focus of the portrait screen. The same lightweight art
system serves every board and each Plop color.

## Palette

| Token | Value | Use |
|---|---|---|
| Play background | `#FCE8D9` | Warm canvas behind the tray |
| Milk | `#F7F2EB` | Secondary controls |
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
| Floor tile | Generously rounded square with a raised face | Warm ivory, subtle upper-left bevel light and a soft contact edge |
| Pebble wall | Rounded toy-block footprint with a visible skirt | Smoky lavender and shallow pits give the obstacle a matte, molded surface |
| Nest | Pinched pillow with raised corners and a recessed center | Glossy matched color with a small cream crescent and padded seams |
| Board tray | Nine-sliced frame and recessed interior | Cream rim over warm peach, with a directional inset shadow |
| Plop | Shared asymmetric dome with a gently lobed foot | Large oval eyes, a bright softbox reflection, tint variations and diffuse floor bounce |

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
1080 × 1920 with a 0.5 width/height match. The tray aims for 87.5% of
safe width on small boards and uses both dimensions to fit taller boards.
The level badge and move chip sit above the tray, with icon-led controls
underneath. Raised cream controls, plum icon ink and a pink move accent
follow the portrait reference. Compact windows scale the modal cards.

Level selection and Settings flank the PLOP! wordmark. Contextual tutorial
text joins the badge only when needed. Undo, Restart and Hint remain visible
during ordinary play. **Sweet Dreams!** uses a compact action card. The
deadlock card shows a worried Plop and offers Undo Move, Restart Level and
Keep playing. Settings retains independent Music and Sound sliders and the
30/60 FPS choice.

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
