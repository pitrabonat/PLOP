# PLOP! Phase 5 visual gap

The visual baseline is the existing Unity implementation, including the saved
Level 2 Editor session and the Phase 4 Level 29 captures on iPhone 15 Pro Max
and iPhone SE. The target column uses the Phase 5 written art direction.
The Phase 5 upload contained the brief as text. A reference-image comparison
requires the original concept images.

| Area | Phase 4 implementation | Phase 5 target | Unity change | Expected cost and verification |
|---|---|---|---|---|
| Composition and scale | The Play Mode view uses a broad free-aspect camera. Short boards reserve at least five cells in both axes, while tall phone captures leave a large gap above the tray. | The puzzle dominates a portrait screen. Small trays use approximately 80–88% of safe width. Square cells and the tray's original proportions read clearly. | Fit the full tray to the real safe-area content rectangle. Give the header, contextual text and controls defined space and position them relative to the tray. | Layout runs on level and viewport changes. Check safe-area bounds at 1080×1920, 1170×2532, 1440×3200 and 720p-class portrait. |
| Tray and ivory tiles | Warm neutral tray, rounded floor tiles and soft edges. Large surfaces have weak separation between floor, sidewall and rim. | A molded miniature tray with an inset bed. Ivory tiles have a defined face, edge and soft contact shadow. | Bake a coherent upper-left key light and opposing rim and contact shading into the shared sprite atlas. Keep orthographic presentation and exact grid alignment. | Reuse atlas pages and renderers. Inspect corners, the bed recess and tile separation in small and large boards. |
| Pebbles and Nests | Lavender blocks and colored pads have related silhouettes. Background and far edges have similar values. | Pebbles read as smooth raised blocks. Color-matched Nests read as inflated pillows with a soft sleeping well. | Separate silhouettes, model a bevel on Pebbles and pinch pillow corners around an inset center. Keep the Berry, Mint, Honey and Lilac relationships. | Reuse tinted sprites and contact shadows. Inspect silhouettes at typical play scale with matching Plops awake and asleep. |
| Characters and faces | A sloped jelly profile and tiny eyes read in tall Level 29 screenshots. At lower display scales the face and body lose definition. | Broad, softly asymmetric marshmallow bodies occupy about 70–85% of the tile. Eyes, mouth and sleeping expression remain readable. | Refine the shared contour, translucent-looking highlights, local color variation and face values. Keep movement, compression, recoil and sleep on presentation transforms. | Bake lighting into sprites. Check the packed atlas, character close-up and Undo/Restart alignment. |
| Interface and spacing | Separate brand and level text, a large heading and three long footer pills. Action emphasis and margins stay similar across board sizes. | Compact contextual header, move/tutorial text near the board, touchable icon controls, and a small completion card. | Use shared sizing tokens, clearer icon hierarchy and responsive anchors. Retain audio and frame-rate options in Settings. | Reuse existing uGUI and sprite masks. Inspect title and action legibility at every target width, including Settings and completion. |
| Deadlock feedback | Hint can report no available route on request. Gameplay has no automatic recovery prompt. | A warm, calm modal appears when a state is proven deadlocked, with Undo, Restart and dismiss. | Attach a versioned solver-derived lookup to each shipped board and keep recovery in the controller and HUD. | Use offline graphs, with a bounded asynchronous fallback for unknown states. Verify proof, cancellation, move restoration and completion priority. |

Baseline captures are under [Phase 4 gameplay previews](Previews/README.md).
The Phase 5 review uses gameplay recordings or captures from the running
Unity player, including a close-up and both overlay states.

## Art integration

The tray, tiles, Pebbles, pillows, Plop body and face use named atlas slots.
If the concept calls for hand-painted light or material detail after
comparison, the import inputs are transparent sprite masters with the
existing artwork bounds and pivots. Provide a nine-slice tray and rim,
one ivory tile, one Pebble, one neutral tintable Plop body, one neutral
tintable pillow, and separate awake, worried and sleeping face artwork.
Light the set consistently from the upper left. Pack the production variants
together and check density, edge padding and contact-shadow layering on phone.
