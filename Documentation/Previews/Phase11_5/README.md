# Phase 11.5 previews: environmental art, VFX and living Worlds

Every image and video here is a **real capture** of the game: no concept art and no mock-ups.

- **Top level (`*.png`)**: screenshots from the Phase 11.5 Android development APK
  (`PLOP-phase11_5-dev.apk`) on the `Medium_Phone_API_37.0` emulator, 1080×2400.
- **`recordings/`**: screen recordings of the same Phase 11.5 code (development build) on the iOS Simulator (iPhone 15 Pro Max, `simctl io
  recordVideo`), scaled to 592×1280 H.264 and trimmed to start at the first frame of the board. About 20 fps: the
  Simulator recorder's rate, not the game's. (The Android emulator's recorders reached only 2–6 fps here.)
- **`editor/`**: close-ups and full-scene renders from the Unity Editor (`PLOP → Visual Lab → Capture Close-ups`),
  using the same shaders and board code at a fixed animation time.

Boards 01–07 and 20–22 are development-only **Visual Lab** boards, built to show every mechanic shape at once.
They are compiled out of release players and never saved. Boards 10–18 are real campaign levels.

## How to open

- In Finder: **Go → Go to Folder…** (⇧⌘G), then paste
  `/Volumes/PLOP_DEV/Unity/Projects/PLOP/Documentation/Previews/Phase11_5`
- From Terminal: `open /Volumes/PLOP_DEV/Unity/Projects/PLOP/Documentation/Previews/Phase11_5`
- The `PLOP_DEV` volume must be mounted (`plop-mount`).

## Emulator screenshots

| File | Shows |
|---|---|
| `01-lava-pools.png` | Lava close-up: isolated cells, a connected pair, a cross and a 2×2 pool with a tail (Ember Hollow) |
| `02-frost-paths.png` | Frost: a horizontal path, a vertical path, a 3×3 field and a path ending in a Nest, without per-cell borders (Frosted Pond) |
| `03-breeze-fans-and-strengths.png` | Fans with wind in four directions and push strengths 1–3 (chevrons) (Breezy Meadow) |
| `04-loops-multiple-pairs.png` | Animated portals: four pairs, each with its own pip count (1–4) as well as its tint (Prism Observatory) |
| `05-combined-chain-start.png` | Combined mechanics before the move: a slide that a fan turns upward into a Loop (Twilight Attic) |
| `06-blocked-loop-start.png` | A Loop whose exit will be blocked (Moonlit Clouds) |
| `07-all-mechanics-cozy.png` | Every mechanic together on the Cozy Nest board materials |
| `10-world-cozy-nest-L3.png` … `18-world-moonlit-clouds-L140.png` | Each of the 9 Worlds on a real campaign level: board materials and living background |
| `20-blocked-loop-after.png` | After the blocked-Loop demo moves |
| `21-combined-chain-after.png` | After the combined chain settles (slide → slide up → teleport) |
| `22-lava-failure-card.png` | Lava failure: the unchanged failure card after the warm reaction |
| `30-ios-sim-lab2-frost.png` | The same build on the iPhone 15 Pro Max Simulator (iOS), inside the safe area |

## Recordings (`recordings/`)

| File | Shows |
|---|---|
| `frost-slide.mp4` | Slide across Frost: icy trail, stretch, frost particles, stop reaction |
| `fan-push.mp4` | Fan push: wind intensifies, the Plop stretches and settles |
| `loop-teleport.mp4` | Teleport: entry reaction, shrink, swirl, exit reveal |
| `lava-failure.mp4` | Stepping into Lava: puff, ripple, surprised face, failure card |
| `combined-chain.mp4` | Slide → fan turns it upward → Loop → keeps sliding, in the Core's trace order |
| `blocked-loop.mp4` | Blocked Loop exit: a small wobble and no exit animation |
| `background-meadow.mp4` | Living background: windmill sails, grass and drifting seeds (Breezy Meadow, L22) |
| `background-snow.mp4` | Living background: falling snow (Frosted Pond, L8) |

## Editor renders (`editor/`)

| File | Shows |
|---|---|
| `closeup-lava-a.png`, `closeup-lava-b.png` | Lava pools close up at two animation times |
| `closeup-frost.png` | Frost path shapes close up |
| `closeup-breeze.png` | Fan housing, blades, chevrons and wind |
| `closeup-loops.png` | Portal frame, ring, vortex and pair pips |
| `lab1-…png` … `lab7-…png` | The seven Visual Lab boards in their Worlds |
| `world-<id>.png` | Each World's board materials (tray, floor tiles, pebbles, Nests) on the same board |
| `scene-<id>.png` | Each World as a full portrait phone scene with the living background |
| `scene-breezy-meadow-later.png` | The same meadow scene 2.5 s later: the sails and seeds have moved |
