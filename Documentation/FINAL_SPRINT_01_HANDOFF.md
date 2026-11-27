# Final Sprint 01/06 — Product Completion: handoff

Status: **partly complete.** The audit, content re-verification, branding fixes, tests and both Android builds are done.
The full fresh-install player journey was not repeated as its own Sprint 01 pass. Sprint 02 started straight away
because its P0 (the game had no audible sound) blocks launch on its own, and the journey is re-run on the Sprint 02
builds instead. This document records what Sprint 01 actually verified.

## Builds

`/Volumes/PLOP_DEV/Builds/FinalSprint/01_ProductCompletion/Android/`

| File | Bytes | SHA-256 | Notes |
|---|---|---|---|
| `PLOP-1.0.0-S01-dev.apk` | 49,847,192 | `a8cac52c…eacf015f` | Development build (dev level jump, frame diagnostics) |
| `PLOP-1.0.0-S01-release-testsigned.apk` | 39,438,342 | `3d3ec2ad…cfd48decbc3fa67eacf015f` | Release configuration, **signed with the debug keystore** (test only; production signing is an owner action) |
| `PLOP!_BurstDebugInformation_DoNotShip/` | — | — | Unity symbol side-output; never ship |

- Both APKs are built from commit `e52e5d5`.
- The Sprint 02 code edits started while the release build was running. I checked both APKs' `global-metadata.dat`:
  - The Sprint 02 types (`PlopLoopPlayer`, `PlayCompletion`, `LocalizedText`, `SetMusicContext`) are absent.
  - The Sprint 01 types (`PlopWorldAmbience`, `PlopAudioClips`) are present.
  - So both APKs are clean Sprint 01 builds.
- The Unity log prints "835 MB / 752 MB" for these builds. That is Unity's uncompressed estimate; the real file sizes are in the table.

## Verified in Sprint 01

| Check | Result |
|---|---|
| .NET core suite | 203/203 pass |
| Unity EditMode | 203/203 pass |
| Unity PlayMode | 146/146 pass (12 new Phase 13 branding tests included) |
| Release content verifier, `PLOP_Pack_003` (160 levels, 8 Limited Moves) | 0 errors, see `Documentation/FinalSprint01/` |
| Release content verifier, `PLOP_Daily_002` (60 levels) | 0 errors |
| Main menu: every button leads somewhere | Yes, checked on the Android emulator |
| Opening levels 1–6 played with real swipes on the emulator (preview mode, save untouched) | Clean; captures in `Documentation/Previews/FinalSprint01/baseline/` |
| Level 35 Lava failure state | Shows "Oops! Too hot!" with Undo / Restart |
| iOS Simulator: native Day splash, the six menu logos, home-screen icon | Rendered correctly; captures in `Documentation/Previews/Phase13/ios/` |
| iOS launch-screen colour | Set to #FCDBC4 (the splash's top edge), so there's no white flash before Unity |
| Cosmetic catalog (42 items) | All drawn by the game's own renderer, so no placeholder items need hiding |

## Five representative levels (dev reference)

| Level | Why it represents the campaign |
|---|---|
| 1 | First contact: a single Plop and the one-line instruction |
| 2 | Introduces two awake Plops moving together on one swipe |
| 4 | First real routing puzzle (blockers) |
| 6 | End of the opening; checks the difficulty step |
| 35 | Lava fail state and the recovery flow |

## MUST FIX BEFORE LAUNCH

1. **No audible sound.** This is the Sprint 02 P0; the root cause and fix are in `FINAL_SPRINT_02_HANDOFF.md`.
2. **Owner approval of the provisional branding.** Selected for now: Hero_Pink icon, Day splash, Jelly Pink logo. The alternatives are in `Documentation/Branding/`.
3. **Production signing keystore and upload key.** Owner action; not created by the agent.
4. **Store requirements (owner):**
   - Google Play: a new personal developer account needs a closed test with at least 12 testers for 14 days before production access.
   - Apple Developer Program: membership required.
   - The project already targets Android API 36.
5. **Privacy policy URL and data-safety form.** The game collects no data, but both stores still require the declaration.

## VISUAL POLISH FOR SPRINT 02

- Mechanic VFX and world readability pass. The art packs are integrated, but some tiles are still the Phase 11.6 versions.
- Main-menu atmosphere and logo animation timing.
- Settings card layout after the new audio controls.
- Localization layout (long German and Russian strings).

## OPTIONAL POST-LAUNCH

- A separate "Challenges" collection view for the 8 Limited Moves levels. They already show `moves / limit` in-level.
- More Daily packs once `PLOP_Daily_002` (60 days) runs out.
- More cosmetics in the existing categories.
