# PLOP! 1.0.0 RC1 — release readiness

Branch `release/1.0.0-rc1` (from Phase 11.6, commit `60c4bb1`, tag `checkpoint/pre-phase12`). Feature freeze in
effect: Phase 12 changed no rules, levels, prices, Worlds, currencies or cosmetic categories.

**Verdict: RC1 — engineering gates met for a release *candidate*; NOT store-ready.** No known P0/P1 defects remain
open. Publication is blocked by items only the owner can resolve (production signing, physical-device and
human playtests, art-pack provenance, store metadata) — see [Release blockers](#release-blockers) and
[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

## Baseline audit (Milestone A, executed 2026-11-26)

Verified against the code, tests and builds — not only the documentation.

| Area | Status | Evidence |
|---|---|---|
| Puzzle Core (`MoveRules`, `GameEngine`, events) | Implemented, tested | .NET/EditMode `CoreRulesTests`, `MechanicsTests`; deterministic replay of every shipped level |
| Solver (BFS, state-space analysis, deadlock tables) | Implemented, tested | `SolverTests`, `DeadlockCatalogTests`; 2,404 hint solves in release verification |
| Level generator, Level Lab | Implemented, tested (editor/tools only) | `GenerationTests`; Phase 12 made `PLOP.Generation` editor-only, so it no longer ships |
| Campaign progression (160 levels, 9 Worlds) | Implemented, tested | `Phase11ProgressTests`, `WorldCampaignTests`; [content verification](content-verification-Pack_003.md) |
| Frost, Lava, Breeze fans, Loops, combined chains | Implemented, tested | `MechanicsTests`, `Phase10GameplayTests`, `Phase11_5EnvironmentTests`; 83/82/62/71 campaign levels use them |
| Limited Moves | Implemented, tested | 8 campaign levels; Move limit ≥ verified optimum checked for all |
| Perfect scoring | Implemented, tested | `MetaProgressionTests`; Perfect only at the verified optimum without assistance |
| Dream Dust, paid hints with earned currency, hint grace | Implemented, tested | `MetaProgressionTests`, `Phase11EconomyTests`; charged only for a verified hint on the current board |
| Daily Gifts, Daily Puzzles | Implemented, tested | `MetaProgressionTests`; [Daily verification](content-verification-Daily_002.md) (60 levels) |
| Character Studio, cosmetic ownership and equipment | Implemented, tested | `CosmeticTests`, `Phase9CharacterTests`; purchase = one atomic wallet write |
| Save migration (Pack_001 → 002 → 003) | Implemented, tested | `Phase11ProgressTests`, EditMode migration tests |
| Environmental animations (Phase 11.5) | Implemented, tested | `Phase11_5EnvironmentTests` |
| Custom sprite integration (Phase 11.6) | Implemented, tested, committed `60c4bb1` | `Phase11_6ArtPackTests`; emulator captures in `Documentation/Previews/Phase11_6` |
| Audio and haptics | Implemented, tested (audio); haptics untested on hardware | `PlopAudioTests`; Android `performHapticFeedback`, iOS `PlopHaptics.mm` |
| Reduced Motion, Battery Saver 30 / Smooth 60 | Implemented, tested | `PlopMotionFeedbackTests`, `Phase11_5EnvironmentTests` |
| Offline-first | Verified by inspection | No networking, analytics, ads or SDK code ([PRIVACY_AUDIT.md](PRIVACY_AUDIT.md)) |

Unverified (needs hardware or people): haptics feel, battery and thermal behaviour, real GPU frame time,
touch feel on physical screens, human difficulty and fun.

## Defects found and fixed in Phase 12

| ID | Pri | Defect | Fix |
|---|---|---|---|
| R-01 | P1 | A single malformed or torn level-records write discarded **all** completion and Perfect records (only a `.corrupt` copy was kept). | `LevelRecords` writes a backup with every save, restores it when the main record is unreadable, and skips only a malformed entry. 3 regression tests. |
| R-02 | P1 | No app icon on any platform (installed builds showed Unity's default icon). | `AppIconBuilder` renders the icon from the game's own Plop rig; assigned for iOS and Android legacy/round/adaptive. |
| R-03 | P2 | Cozy Nest and Twilight Attic fairy lights (Phase 11.6 backdrops) ran behind the header, Home/Settings buttons and level title. | Lights moved to the top edge, above the header, in `render_worlds.py`; both backdrops re-rendered. |
| R-04 | P2 | Gradle incremental packaging left ~18 MB of dead space in APKs (Phase 11 K-14 recurred: 66 MB file, 48 MB content). | `PlopBuild` clears `Library/Bee/Android/Prj/*/Gradle/launcher/build/outputs` before every Android build. |
| R-05 | P2 | Level generator, simulated players and quality tooling compiled into the player. | `PLOP.Generation` assembly limited to the Editor. |
| R-06 | P2 | Version was 0.1.0 / code 1; no release or App Bundle build path. | Version 1.0.0, Android version code 100, iOS build 100 from one place (`PlopBuild`); RC entry points for dev APK, release APK, AAB, iOS device and Simulator exports. |
| R-07 | P3 | Unity splash screen delayed the first game frame. | Splash disabled (optional in Unity 6). |
| R-08 | P3 | Ambient portal motes (Phase 11.6) counted as gameplay feedback and held `IsBusy`. | Fixed before the 11.6 commit: ambient particles are counted separately. |

## Release blockers

See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) for the full list.
