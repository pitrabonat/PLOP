# Asset and license inventory (1.0.0 RC1)

Inspected 2026-11-26 on `release/1.0.0-rc1`. Scope: everything a player build can contain (`Assets/`, Unity
packages, native plugins). This is an inventory from the repository, not legal advice.

## Third-party content in the player build

| Item | Location | Source | License | Attribution needed | Status |
|---|---|---|---|---|---|
| Nunito (two subset files, Display and Text) | `Assets/Art/Resources/Typography/Nunito-*.ttf` | The Nunito Project Authors, github.com/googlefonts/nunito | SIL Open Font License 1.1 (full text shipped beside the fonts in `Nunito-OFL.txt`) | Keep the OFL text with the font. OFL needs no in-app credit; listing it in credits/store notes is good practice | OK |
| Unity engine runtime and packages (URP 17.3.0, Input System 1.20.1, uGUI 2.0.0, 2D Sprite/Tilemap, Burst, Mathematics, Collections pulled in as dependencies) | `Packages/manifest.json`, `packages-lock.json` | Unity Technologies | Unity Terms of Service / Unity Companion License | Governed by the Unity plan of the publisher | OK (owner's Unity plan must cover a commercial release) |
| Unity default resources (`unity default resources`, `LegacyRuntime.ttf` fallback font) | engine | Unity | Unity Terms of Service | None | OK |

No other third-party fonts, music, sound effects, textures, shaders, SDKs or native libraries were found.
`Assets/Plugins/iOS/PlopHaptics.mm` is project code. There are no Android plugins, AARs or JARs beyond Unity's own.

## Original project content

| Content | How it was made | Location |
|---|---|---|
| Characters (Plops, expressions, cosmetics), board, UI shapes, icons | Procedural SDF painters in C# (`ShapeAtlas`, `UiSprites`, `CharacterArt`) | `Assets/Scripts/Unity/...` |
| World backdrops and previews | `Tools/WorldArt/render_worlds.py` (procedural) | `Assets/Art/Resources/WorldBackdrops`, `Assets/Art/WorldPreviews` |
| World ambience beds (9 WAVs) | `Tools/AudioArt/render_ambience.py` (synthesised) | `Assets/Audio/Resources/Ambience` |
| Foley / UI cues | Synthesised in C# at runtime (`PlopAudio`) | code |
| Shaders (Lava, Frost, Wind, Ambient, Ambient particles) | Written for the project | `Assets/Art/Resources/Shaders` |
| Levels, Daily pool, deadlock tables | Project generator and solver | `Assets/Levels/Resources/Levels` |

## Supplied art pack (Phase 11.6): provenance to confirm

| Item | Location | Provenance stated in the pack | Status |
|---|---|---|---|
| 16 sprites (floor, pebble, Lava, Frost, fan housing/blades, portal frame/vortex, 4 Nests, 4 Plops), source sheet, `Visual_Target.png` | originals: `Documentation/ArtPack/Phase11_6/`; processed: `Assets/Art/Phase11_6/` | README: "sliced from a generated concept sheet … starter sprites, not QA-approved production assets" | **BLOCKER for store release: owner must confirm** which tool generated the sheet and that its terms allow commercial use in a game and on store pages. Not verifiable from the repository. |

Only the board sprites are referenced at runtime (`Resources/ArtPack/Phase11_6.asset`). The 4 Plop sprites, the
source sheet and `Visual_Target.png` are not in any build (they are outside `Resources` and unreferenced).
The repository on GitHub is public, so the original pack is publicly visible there.

## Attribution text (for credits / store listing)

> Nunito typeface © 2014 The Nunito Project Authors, licensed under the SIL Open Font License 1.1.
> Made with Unity.

## Open items

1. Confirm the generator and usage rights of the Phase 11.6 art pack (see above).
2. Confirm the Unity plan/licence of the publishing entity before release.
3. Phase 13 localization: the Nunito files are subset to Basic Latin + Latin-1 (no Latin Extended-A, Cyrillic,
   Greek or Vietnamese). Re-subsetting from the upstream OFL source is allowed by the OFL (keep the licence; a
   modified font must not use a Reserved Font Name if one is declared — Nunito declares none).
