# PLOP! Final Sprint 03.1: trailer rework handoff

Files: **`/Volumes/PLOP_DEV/Marketing/FinalSprint03_1/`** (528 MB, on PLOP_DEV and not in git). The pipeline sources and documents are copied into this repo at `Tools/Marketing/Sprint03_1/`. The V1 files in `FinalSprint03/` were left untouched.

## Status

| Task | Status |
|---|---|
| 1. Official trailer V2, three thumbnails, YouTube metadata, QA | **Done. Waiting for the owner's approval** of the video, thumbnail and visibility. |
| 2. YouTube upload ("Munchies" channel, Private first) | **Not started.** It runs only after explicit approval. |
| 3. Short-form package (4 vertical videos, covers, captions, TikTok rules) | **Not started.** Follows the approval gate. |

## Task 1 deliverables

| Item | Path |
|---|---|
| Master | `02_OfficialTrailer/PLOP_OfficialTrailer_V2_1920x1080.mp4`: 30.67 s, 1920×1080, 30 fps, H.264 High, AAC 48 kHz, −15.0 LUFS, −1.9 dBTP |
| Clean (no headlines) | `02_OfficialTrailer/PLOP_OfficialTrailer_V2_Clean.mp4` |
| Preview | `02_OfficialTrailer/PLOP_OfficialTrailer_V2_Preview.mp4` (1280×720) |
| Storyboard and timeline | `02_OfficialTrailer/Trailer_Storyboard.md`, `Trailer_Edit_Timeline.json` |
| Thumbnails | `03_Thumbnails/PLOP_Thumbnail_{A,B,C}.jpg` (1280×720, 1920×1080 masters alongside). A is recommended. |
| Store screenshots | `04_StoreScreenshots/Screenshot_Review.md`: reviewed and not re-exported (no meaningful improvement) |
| Audio | `05_Audio/` (score, mixes, stems), `Audio_Provenance.md` |
| YouTube | `06_YouTube/YouTube_Metadata.md` and the English SRT |
| QA | `09_QA/Production_QA.md`, `Trailer_V2_ContactSheet.jpg` |

## Pipeline

1. **Depth layers.** `Cinematic2/layers.swift` splits the island illustration into depth layers. `alphaclean.swift` removes keying haze.
2. **Rig renders.**
   - `Assets/Scripts/CharacterTools/Editor/MarketingCapture.cs` renders the game's `PlopView` rig in batch mode.
   - It captures each frame on black and on white to recover true alpha, and repaints the faces at capture resolution.
   - Output: `01_CinematicSources/RigRenders/<seq>/f_*.png` plus `meta.json`.
   - Run (Editor closed):
     ```bash
     Unity -batchmode -projectPath . -executeMethod Plop.CharacterTools.MarketingCapture.RunBatch
     ```
     with `PLOP_MARKETING_DIR` set, and optionally `PLOP_MARKETING_ONLY`.
3. **Compositor.** `Compositor/Cinematic2.swift` holds the shots (`v2_world`, `v2_hero`, `v2_adventure`, `v2_night`, `v2_studio`, `v2_thumb`). They are wired into `plopcomp` as `scene2` layers. `main.swift` gains an `audioRate` setting (48 kHz).
4. **Audio.** `TrailerAudio` now renders at 48 kHz by default and adds a true-peak meter and a true-peak-safe limiter.
5. **Edit.**
   ```bash
   cd Compositor && xcrun --sdk macosx swiftc -O main.swift Cinematic.swift Cinematic2.swift -o plopcomp
   python3 Scripts/edit_v2.py && python3 Scripts/thumbs_v2.py
   ```

## The owner needs to

1. **Approve or redirect.** Watch the master (or the preview), pick a thumbnail, choose the visibility, and confirm the audience setting ("made for kids" or not).
2. **Confirm the illustration rights.** Confirm the rights to the island illustration used as the S01–S04 background.
3. **Listen.** Do a listening pass on the mix (it has only been measured).
