# Production QA: Official Trailer V2 (Sprint 03.1)

**Method**

- I inspected 62 frames of the final master, one every 0.5 s (`Trailer_V2_ContactSheet.jpg`).
- I checked full-resolution stills at the critical moments: the opening, the hero wake, the portal match cut, the sleep sequence and the end card.
- Specs were measured with `mediainfo` (AVFoundation) and the audio tool (BS.1770 loudness plus a 4× true-peak meter).
- **Nobody has listened to the audio yet.** Every audio result below is a measurement.

## Exports (measured)

| File | Size | Length | fps | Video | Audio | Longest black run |
|---|---|---|---|---|---|---|
| `PLOP_OfficialTrailer_V2_1920x1080.mp4` | 1920×1080 | 30.667 s | 30 | H.264 High L4.0, 11.0 Mbps | AAC stereo, 48 kHz | 0 s |
| `PLOP_OfficialTrailer_V2_Clean.mp4` (no headlines) | 1920×1080 | 30.667 s | 30 | H.264 High L4.0, 9.6 Mbps | AAC stereo, 48 kHz | 0 s |
| `PLOP_OfficialTrailer_V2_Preview.mp4` | 1280×720 | 30.667 s | 30 | H.264 Main, 5.6 Mbps | AAC stereo, 48 kHz | 0 s |
| `PLOP_Thumbnail_A/B/C.jpg` | 1280×720 (masters 1920×1080 in `03_Thumbnails/master/`) | | | | | |

The mix measures −15.0 LUFS integrated and −1.9 dBTP true peak, against targets of −14 to −16 LUFS and ≤ −1 dBTP.

## Checklist

| Check | Result |
|---|---|
| No black opening | ✅ Frame 0 is the lit island. The measured black run is 0 s. |
| Clear visual motion | ✅ Each shot has its own motivated camera move, plus character and environment animation (see the contact sheet). |
| Consistent character designs | ✅ Every Plop in the cinematic shots is the game's own rig and atlas, so they match the gameplay Plops. |
| Natural facial animation | ✅ The game's expression set: sleep → wake (eyes open), blink, happy, surprised/excited, sleepy → asleep. ⚠ Naturalness is my judgement, not a viewer test. |
| Independent environmental motion | ✅ Fan blades, vortex, waterfall flow and foam, lava bubbles, sway of the canopy, lantern, daisies and moss tufts, wind ribbons, house window glow, and twinkling stars at night. |
| Correct contact shadows | ✅ A soft multiply shadow under each grounded Plop in S01, S02 and S08 (and the thumbnails). S03 uses the rig's own shadow plus an ice reflection. |
| Readable gameplay | ✅ Boards are 865–1000 px tall with no blurred surround. Five layouts. |
| Accurate mechanics | ✅ Real recordings of verified solutions at 1.0× speed, with SFX at the logged event times. |
| Pacing | ✅ Ten shots, with cuts on the 90 BPM grid. |
| Music sync | ✅ by timing: SFX at the logged event times, cosmetic changes on beats, and a portal duck. ⚠ Not listened to. |
| No clipping | ✅ True peak −1.9 dBTP. Headline widths were checked; the S05 headline was resized after the first render overflowed the frame. |
| No broken transitions | ✅ S04 → S05 is a circle reveal from the in-game portal tile with the tunnel underneath. The other cuts land on the beat. |
| Eyes fully closed when sleeping | ✅ S01: the sleeping frame of the rig. S09: every Plop reaches the rig's Sleeping state (closed-eye arcs, checked at 26.5 s and 27.5 s at full resolution). The z's are gated on each Plop's `asleep` flag, so they never float over open eyes. |
| No Coming Soon | ✅ None, and no Pre-register, Download Now, date or badge either. |
| Correct logo | ✅ `01_Jelly_Pink.png`, the provisional pick from Sprint 03, shown once. Subtitle "Sleepy Puzzle". |
| Truthful availability | ✅ No availability claim anywhere: video, thumbnails or metadata. |
| Correct resolution and duration | ✅ 1920×1080, 30.67 s. |
| Contact sheet | ✅ `Trailer_V2_ContactSheet.jpg` (0.0–30.5 s, every 0.5 s, read left to right). |

## Issues found and fixed during QA

1. The night Plops showed rectangular relight seams. They are now relit offscreen in image space and masked by the character's alpha.
2. Night-tinted loose petals read as dark blobs, and the canopy crop edge was visible. Fixed with a soft vertical mask.
3. The lantern had a white keying fringe. Fixed with stronger alpha cleanup (`Cinematic2/alphaclean`).
4. The crescent moon had a hard rim artifact. Replaced with a soft full moon.
5. The portal push-in was too weak. It is now faster, and the tunnel opens from the vortex's screen position.
6. In the macro shot, the foreground daisy layer showed a rectangular haze edge. Removed.
7. The "MANY POSSIBILITIES." headline overflowed the frame. Resized.

## Remaining limitations (honest)

- **Island illustration rights.** S01–S04 still use the owner's island illustration as the background. Its rights and provenance (probably AI-generated) still need the owner's confirmation.
- **Procedural moss.** The macro ledge in S02 is procedural, flatter and more graphic than the painted island. It is acceptable at speed but visible on a pause.
- **Bright canopy at night.** The night canopy is only partly relit, so it stays fairly pink and bright.
- **Small lantern rim.** A faint light rim remains on the lantern's left edge in the night close-up.
- **Not true 3D.** Camera moves are 2.5D, with parallax between flat layers. There is no 3D geometry.
- **Plain studio backdrop.** The S08 backdrop is a plain gradient and floor pad.
- **Not listened to.** Audio is measured only.
- **No device test.** No physical-device or YouTube-processed playback check has been done yet; that comes in Task 2, after approval.
