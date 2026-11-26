# Phase 11 performance report

What was measured, where, and what was **not** measured. Host: Apple M4 Mac (10 threads), .NET 10 Release builds, Unity 6000.3.29f1. **No physical phone was available, so there are no device frame times, battery or thermal numbers.** The emulator numbers below show relative cost on a virtual GPU and are not a substitute for a real mid-range phone.

## Solver and engine (offline, `Bench --perf`)

`--perf` solves every level of a pack with BFS, runs the full state-graph analysis on the largest level and replays 100,000 random engine commands.

| Pack | Run | Solver total / avg per level | Largest level | BFS states / time | Full analysis | `TryMove` |
|---|---|---|---|---|---|---|
| Pack_003 (160) | idle Mac, earlier in Phase 11 | 210 ms / 1.31 ms | L136, 6×7, 3 Plops | 17,975 / 6.1 ms | 25,097 states / 27.9 ms | 0.26 µs, 165 B per command |
| Pack_003 (160) | final, 3 runs with the Android emulator running | 423–505 ms / 2.6–3.2 ms | L136 | 17,975 / 10–40 ms | 53–83 ms | 0.44–0.73 µs, 165 B |
| Daily_002 (60) | final, emulator running | 216 ms / 3.6 ms | Daily 33, 5×6, 3 Plops | 3,697 / 9.1 ms | 6,222 states / 94 ms | 0.63 µs |
| Pack_002 (Phase 10, archived) | final, emulator running | 452 ms / 2.8 ms | L129, 6×6, 4 Plops | 28,361 / 57 ms | 93,778 states / 275 ms | 0.75 µs |

Reading: the rebalanced campaign is **smaller to analyse** than Phase 10's. Its largest state graph is 25 k states, against 94 k for Phase 10's L129. That matters because the in-game hint and the deadlock fallback run the same solver. The emulator roughly doubles the noise. Compare rows taken under the same conditions only.

## In-game cost

- **No solver work per frame.** Hints and deadlock checks run on a worker thread with a state budget; the result is applied on the main thread only if the board is unchanged.
- **Deadlock proofs** ship precomputed: `PLOP_Deadlocks_003.bytes` (634 KB, 160 levels) is loaded once at start, so stuck-state detection is a hash lookup in normal play.
- **Level data**: `PLOP_Pack_003.json` 473 KB, `PLOP_Daily_002.json` 168 KB, `PLOP_Pack_002_Order.json` 4 KB (read once, only for a Phase 10 save).
- **Forced chains** are bounded by the step budget; the longest shipped chain is 6 steps, and none can loop.
- **Allocation**: 165 B per engine command (history snapshot + event array). The controller's `Update` allocates nothing in steady state (code review; the diagnostics overlay's own text does allocate while it is shown).
- **Render rate**: 60 FPS while anything moves; after 0.75 s idle `OnDemandRendering` drops to every 3rd frame. A 30 FPS battery-saver mode is in Settings.
- **Local playtest recorder**: compiled out of release builds; in a dev build it appends one CSV row per level visit, with no per-frame work.

## Android emulator (development build)

Final dev APK on the `Medium_Phone_API_37.0` emulator (1080×2400, arm64 image, virtual GPU), with the dev overlay (`debugprefs.py diagnostics on`) and the Mac otherwise idle. Level 155 (6×7, three Plops, all four mechanics) played through its 15-move route by swipes, ending in Perfect:

| Moment | Loop FPS | Frame p50 / p90 | Main thread | GC alloc (frame) | Draw calls | Unity allocated |
|---|---|---|---|---|---|---|
| After move 8 (idle between swipes) | 56.7 | 17.7 / 53.3 ms | 0.7 ms | 0 B | 0 (idle frame skipped) | 79.2 MiB |
| Result card shown | 56.0 | 17.8 / 50.0 ms | 90.2 ms (the frame that builds the card) | 1,586 B | 18 | 79.4 MiB |

- p50 ≈ 16.7–17.8 ms is the 60 FPS cap. The p90 of about 50 ms is **intended**: after 0.75 s without movement the game renders every 3rd frame (`idleRenderInterval = 3`) to save battery.
- The one 90 ms main-thread frame is the frame that shows the result card and its Dust line. It is a single frame at level end.
- An earlier sample, taken while Unity tests and a build loaded the Mac, read 38 FPS. It is not representative and is not used.
- Emulator numbers say nothing about a real GPU or thermal throttling.

## Build size

| Build | APK | Content (compressed) | Notes |
|---|---|---|---|
| Phase 10 dev APK | 64.3 MB | 46.1 MB | about 18 MB of dead space left by Gradle's incremental packaging |
| Phase 11 dev APK (clean) | 46.5 MB | 46.4 MB | built after deleting the cached `launcher-debug.apk` |

Phase 11 adds about 0.3 MB of compressed content: the new pack, Daily pool, deadlock proofs and migration order. The archived packs are outside `Resources` and do not ship.

## Not measured

- Frame time, memory and load time on a **real** Android phone or iPhone.
- Battery drain and thermal throttling (neither the emulator nor the Simulator reports meaningful values).
- Cold start on a low-end device.

Suggested next measurement: a 20-minute session on a mid-range Android phone with `debugprefs.py diagnostics on`, covering Levels 120–160 (largest boards, three Plops, long chains), recording p50/p90 frame time and the "Unity allocated" line.
