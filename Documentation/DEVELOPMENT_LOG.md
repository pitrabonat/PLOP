# Development Log

## 2026-11-20 — Gameplay foundation (branch `feature/gameplay-foundation`)

Checkpoint before work: tag `checkpoint/pre-gameplay-foundation` (= `e0bd5bd` on `main`).

### Phase 1 — Inspection & architecture
- Existing project: Unity 6000.3.29f1, URP 2D, Input System only (`activeInputHandler: 1`), API level
  .NET Standard 2.1, packages trimmed earlier; no gameplay code, no asmdefs. Only `PlopBuild.cs` existed.
- Decided: three engine-free assemblies (Core/Solver/Generation) + presentation + editor Level Lab + tests,
  and a `DotNet/` harness compiling the same sources under netstandard2.1 / C# 9 for second-scale test runs.
- Unity's `JsonUtility` is unavailable to engine-free code → small dependency-free `MiniJson`.

### Phase 2–3 — Core simulation + tests (`89a16f1`)
- `MoveRules.Apply` is the only rules implementation; front-to-back ordering with (secondary, id) tie-breaks.
- "Asleep ⇔ on matching Nest" invariant (Plops may not start on their Nest) lets the state key store cells only.
- 52 tests passed on first run; a deliberate mutation (inverted ordering) made 4 tests fail → restored.

### Phase 4–5 — Solver (`70cf8ba`)
- BFS over packed 64-bit keys with parallel arrays (no node objects); explicit `SearchLimitReached`.
- Added an early cancellation check (a pre-cancelled token was otherwise only noticed after 1024 expansions).
- Randomized cross-check against an independent IDDFS that only uses `GameEngine` — 68 tests passing.

### Phase 6–8 — Generation, validation, duplicates, difficulty (`428db8a`)
- Bug caught in review before running: flood-fill region id was read from the stack top after pop; fixed.
- Exploration run (60 levels/profile) used to calibrate difficulty thresholds to 9 / 16 / 26 / 38.
- Benchmark: 100/100 levels from 181 candidates in 0.71 s; only "trivial" rejections; pack verified;
  deterministic re-run identical. Generated `PLOP_Pack_001.json`.

### Phase 9–10 — Level Lab + Unity prototype (`96116b0`)
- Level Lab (`PLOP → Level Lab`): editor-only asmdef.
- Prototype: `Gameplay.unity` (copy of the URP 2D template scene + `PLOP Game` object); everything else is
  built at runtime from code (placeholder sprites, HUD, input).
- Unity batchmode: compile clean (0 errors, 0 warnings in our code); EditMode 84/84, PlayMode 3/3.

### Phase 11 — Benchmark, integration, docs
- `--perf`: solver 1.2 ms/level average over the pack; worst case 28,636 states / 19 ms; `TryMove`
  0.27 µs and ~170 B per command (desktop .NET).
- Android IL2CPP player build of the prototype: success (60 MB APK, `/Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk`).
- iOS Simulator (iPhone 15 Pro Max, iOS 26.3): prototype launched and rendered Level 1 correctly (board, Plop,
  Nest, HUD). Screenshot revealed the title under the Dynamic Island → fixed by laying the HUD out inside
  `Screen.safeArea`; PlayMode tests re-run 3/3.
- Benchmark re-run reproduced the committed pack byte-for-byte.
- Documentation written: ARCHITECTURE, GAME_RULES, SOLVER, LEVEL_GENERATION, LEVEL_FORMAT, TESTING.

### Open items
- No physical-device play test of the new prototype yet (Android phone not connected; iPhone is MDM-managed).
- Level Lab UI needs a manual pass in the Editor.
- Difficulty weights need playtest calibration; generator quality improvements (see LEVEL_GENERATION.md).
- Hints UI (async solver) not implemented yet.
