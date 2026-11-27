# PLOP! Final Sprint 03/06: launch marketing package handoff

The finished files are in **`/Volumes/PLOP_DEV/Marketing/FinalSprint03/`** (1.4 GB, on PLOP_DEV and not in git). The pipeline that rebuilds them is in this repo at `Tools/Marketing/Sprint03/`. The game code, levels and scenes were not changed in this sprint.

Begin with `11_QA/` in that folder. It has the contact sheets, `EXPORT_MANIFEST.md`, and `PROVENANCE_AND_LICENSES.md`, which are also copied into `Tools/Marketing/Sprint03/`.

## Status

The automatic QA covers 495 exported files: **206 FINAL**, **40 AWAITING OWNER APPROVAL** and **249 DRAFT**. It found no spec issues. Status means:

- **FINAL**: meets its spec and is ready to use.
- **AWAITING OWNER APPROVAL**: depends on an owner decision (rights to the reference illustration, copy sign-off).
- **DRAFT**: AI translation or localized screenshot that needs native-speaker review.
- **BLOCKED**: cannot be produced under the current rules or tools. See the end of this file.

## Deliverables

| Deliverable | File(s) | Spec (measured) | Status |
|---|---|---|---|
| Official cinematic trailer | `02_Cinematic/Master/PLOP_OfficialTrailer_1920x1080.mp4`, 4 clean shots in `02_Cinematic/Shots/`, storyboard in `02_Cinematic/Storyboard/` | 1920×1080, 37.0 s, H.264 High, AAC stereo, −16 LUFS | AWAITING OWNER APPROVAL (uses the reference illustration) |
| Gameplay trailer | `04_Trailers/PLOP_GameplayTrailer_1920x1080_{Captioned,Clean}.mp4` | 29.9 s, real iOS gameplay | FINAL |
| Vertical trailer | `04_Trailers/PLOP_VerticalTrailer_1080x1920.mp4` | 24.9 s; opens with "EVERY PLOP MOVES AT ONCE"; captions kept inside the safe zones | FINAL |
| App Store App Preview | `06_StoreAssets/AppStore/AppPreview/PLOP_AppPreview_886x1920_{Captioned,Clean}.mp4` | 886×1920, 29.5 s, 30 fps, H.264 High L4.0, 11.3 Mbps CBR, AAC 44.1 kHz stereo; iOS-captured footage only | FINAL |
| 5 challenge videos | `05_ShortVideos/PLOP_Challenge01–05_*.mp4` | 1080×1920, 10.4–12.7 s, real level solutions (L027, L033, L019, L046, L061) | FINAL |
| Mascot showcase | `05_ShortVideos/PLOP_MascotShowcase_1080x1920.mp4` | 9.0 s | FINAL |
| Store screenshots (8 headlines) | `06_StoreAssets/AppStore/Screenshots_*` (1320×2868, 1290×2796, 1284×2778, 1242×2688), `GooglePlay/Screenshots_1080x1920`, plus raw and textless versions | Opaque RGB, no alpha | EN FINAL; DE, FR, ES, IT, PT-BR DRAFT |
| Feature graphic | `06_StoreAssets/FeatureGraphic/` (A, B, C), `GooglePlay/PLOP_FeatureGraphic_1024x500.png` (= C) | 1024×500, no alpha | C FINAL; A and B AWAITING OWNER APPROVAL |
| Posters | `07_SocialMedia/Posters/` (5 × 1080×1350) and `Posters/Square/` (5 × 1080×1080) | | keyart and dreams AWAITING OWNER APPROVAL; others FINAL |
| Social brand kit | `07_SocialMedia/{Avatars,Banners,Templates}`, `01_Brand/{Logos,AppIcons,Mascots,Sprites,Palette,Typography}` | | FINAL (logo and icon choice still provisional) |
| Store listings | `08_Localization/StoreDescriptions/<lang>/` with `GooglePlay_*.md`, `AppStore_*.md` and `listing_*.json`, in 8 languages; EN copies in `06_StoreAssets/` | Every field within store limits (`11_QA/store_copy_limits.json`) | EN AWAITING OWNER APPROVAL; others DRAFT |
| Video captions | `08_Localization/VideoCaptions/<lang>/*.srt/.vtt` | 8 languages | EN FINAL; others DRAFT |
| Launch announcements | `07_SocialMedia/Captions/Launch_Announcements.md` (TikTok, Reels, Shorts, YouTube, general) | Nothing posted | AWAITING OWNER APPROVAL |
| Audio | `09_Audio/Trailer/` (scores, mixes, music/SFX/ambience stems) and copied masters | Every mix −16.0 LUFS integrated, peaks −1.5 to −3.7 dBFS | FINAL (measured, not yet listened to) |

## How it was made

- **Gameplay footage.**
  - Recorded with `simctl io recordVideo` from the Sprint 02 build (commit `f82e273`) on the iPhone 15 Pro Max Simulator.
  - Each level replays its verified solution.
  - Sound effects are placed at real game-event times taken from `LevelProbe --cues`.
- **Cinematic shots 1–3 and 8.**
  - The Plops were lifted out of the reference illustration by colour segmentation (`Cinematic/seg.swift`).
  - The compositor re-animates them (`Compositor/Cinematic.swift`): breathing, hops, fan and portal spin, water, clouds, a dusk grade, and the portal dive.
  - This is real animation, not a zoom on a still image.
  - The cut into gameplay is a portal circle reveal.
- **Rendering.** Everything is rendered by `plopcomp`, a Swift program built on AVFoundation, CoreGraphics and CoreText. No ffmpeg or other third-party media tools were installed.
- **Music.** Original 90 BPM scores from the game's own synthesiser code (`TrailerAudio`).

### Rebuild

```bash
cd Tools/Marketing/Sprint03/Compositor && xcrun --sdk macosx swiftc -O main.swift Cinematic.swift -o plopcomp
python3 Scripts/build_cues.py && python3 Scripts/edit_trailers.py && python3 Scripts/edit_cinematic.py && python3 Scripts/edit_shorts.py
python3 Scripts/graphics.py && python3 Scripts/store_copy.py && python3 -I Scripts/qa.py
```

The scripts expect the `/Volumes/PLOP_DEV/Marketing/FinalSprint03` layout. That layout holds the raw takes and the reference plate, which are not in git.

## Blocked

- **Burned-in Russian and Ukrainian headlines.**
  - The bundled Nunito subset has no Cyrillic.
  - The RU and UK store text and SRT/VTT files are delivered.
  - Rendering the headlines needs full Nunito (OFL), and downloading it needs the owner's OK.
- **Google Play promo video.** Play takes a YouTube link, so the owner has to upload `04_Trailers/PLOP_GameplayTrailer_1920x1080_Clean.mp4` first.
- **Store upload, publishing and posting.** Out of scope by rule. Nothing was uploaded, posted or bought.

## Needs the owner

1. **Reference illustration rights.**
   - Confirm the rights to "Pastel Jelly Puzzle Island", which is probably AI-generated.
   - It is used in the official trailer, feature graphics A and B, the keyart and dreams posters, and the banners.
   - If it can't be cleared, use feature graphic C, the gameplay trailers, the screenshots and the challenge videos instead. None of those use the illustration.
2. **Sign-off.** Approve the store copy and the launch announcements, and make the final logo and icon choice.
3. **Native-speaker review** of the seven non-English languages.
4. **Listening pass** on the trailer mixes.
5. **Optional iOS re-capture.** The Studio and Daily screenshots (06 and 07) are from the Android emulator. The UI is identical, but you may want to re-capture them on iOS.
6. **Localized screenshot footage.** The localized screenshots show the English in-game UI. Re-capture them with the game language switched if you want fully localized frames.

## Not verified

- No physical device was used.
- Nobody has listened to the mixes.
- No translation has been reviewed by a native speaker.
- Store specs were read on 2026-11-27. Re-check them at upload.
