# PLOP! project state

**Phase 3 · Puzzle intelligence and level progression**

The production campaign and runtime features passed Unity verification.
The Android IL2CPP development build and offline emulator smoke test passed.

## Completed systems

- Deterministic Core, shared gameplay transitions, GameEngine, shortest solver
  and exhaustive analysis.
- Soft Toy board, jelly Plops, responsive rounded HUD, and portrait swipe input.
- An offline 100-level production campaign with 10 authored tutorials and 18
  planned peak-to-recovery dips across the remaining four bands.
- Level Lab authoring, batch generation, replay, metrics, import/export,
  duplicate checks, filtered catalog editing and strict production saves.
- Stable-ID unlocks, completed levels, replay, automatic local progress saving,
  Undo, Restart, verified current-state hints, and a saved 30/60 FPS preference.

## Recent changes and verification

- This phase started from `ee033b2`, also tagged `checkpoint/pre-puzzle-intelligence`.
- The v2 campaign passed strict validation and deterministic regeneration.
  [Measured generation report](Documentation/benchmarks/generation_benchmark_v2.md).
- Engine-free .NET passed 92/92. Unity 6000.3.29f1 compiled without C#
  errors or warnings and passed 92/92 EditMode and 22/22 PlayMode.
- PlayMode loaded, solved, undid, restarted, and re-solved all 100 Resources
  boards through the Unity controller. It also exercised live-state and stale
  hints, unlocks, replay, legacy migration, and the 30 FPS preference.
- The latest successful build is Android ARM64 IL2CPP, 2026-11-21, at
  `/Volumes/PLOP_DEV/Builds/Android/Phase3/PLOP-phase3-dev.apk`.
  The API 37 emulator in airplane mode verified swipe input, an updated hint
  after a detour, completion and unlocks, replay, relaunch and 30 FPS persistence.
  Build logs and Unity test XML are in `/Volumes/PLOP_DEV/Builds/Logs/phase3`.
- The previous iOS Simulator build and launch are recorded in the
  [historical log](Documentation/DEVELOPMENT_LOG.md).

## Remaining work and known issues

- Difficulty calibration, touch feel, battery use and thermals need physical
  device playtests. Level Lab authoring actions need a manual editor pass.
- The Phase 3 iOS player build and simulator smoke test are pending. The
  previous test iPhone is MDM-managed and blocked developer app launches.
- Legacy progress records only an index. The migration preserves unlocked
  access without marking levels complete on that evidence.

## Git

- Branch `feature/puzzle-intelligence`.
- Verified code commits `7e87830` (catalog and Level Lab) and `aa3c2d6`
  (progression, hints, mobile options and integration tests). The final
  tested tutorial copy is in `10ad418`.
- Use `git rev-parse --short HEAD` for the current documentation-inclusive tip.

## Resume here

1. Mount the existing APFS volume and open
   `/Volumes/PLOP_DEV/Unity/Projects/PLOP` in Unity `6000.3.29f1`.
2. Check `git status --short --branch` and `git log -5 --oneline`. The Phase 3
   branch is `feature/puzzle-intelligence` and the checkpoint is `ee033b2`.
3. Read [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md) and
   [ARCHITECTURE.md](ARCHITECTURE.md). Use the test/build commands in CLAUDE.md.
4. Start the next pass with representative onboarding and mastery puzzles on
   a physical device and measure the 30/60 FPS modes before retuning the curve.
