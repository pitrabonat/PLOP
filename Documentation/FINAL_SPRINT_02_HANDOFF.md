# Final Sprint 02/06 — Final production: handoff

Branch `sprint/02-final-production`, based on Sprint 01 at `e52e5d5`.

**Status:**
- The audio P0 is fixed and verified in a running build.
- Original music, ambience, effects and the sonic logo are done, with exported masters and a manifest.
- Eight-language localization is in, but the translations are unverified.
- The polish pass was small.
- Builds are listed below.

Honest limits are collected at the end. Nobody has *listened* to the audio: every audio claim below comes from measurement, never from hearing.

## 1. P0 — "the game has no audible sound"

**Root cause.** The wiring worked; the content was effectively silent.
1. The only beds were the Phase 10 ambience WAVs, played through the music slider's old default of 0.24. That came out at about −48 dBFS, which is inaudible on a phone speaker.
2. There was no music at all.
3. The effects were low-gain, low-frequency sine blips, mostly below a phone speaker's ~400 Hz roll-off.

**Fix.** I didn't add a second audio manager: `PlopAudio` was rewritten in place.
- All-new content, levelled for phone speakers: music at −20 dBFS RMS, ambience at −26, effects peak-normalised with most energy in the 400 Hz–4 kHz speaker band.
- The new defaults (music 45 %, effects 80 %, ambience 30 %) put the mix in a normal mobile range.

**Verified in a running build.** The Android development build ran on the emulator. I recorded the emulator's actual audio output through its gRPC `streamAudio` endpoint, with guest media volume at 15/15. Script: `Documentation/FinalSprint02/audio-qa/emucapture.py`.

| Capture (files in `Documentation/FinalSprint02/audio-qa/`) | Peak | RMS |
|---|---:|---:|
| Sprint 02 build, Level 13 gameplay at default volumes (music, ambience, moves, recovery cue) | −5.4 dBFS | −25.7 dBFS |
| Sprint 02 build, cold start → splash → menu music. This emulator's save keeps a legacy music/effects choice of 15 %, which migration correctly preserves | −20.1 dBFS | −36.1 dBFS (≈ −27 at the 45 % default) |
| Sprint 02 build, cold start with Master mute on (sonic logo and music must stay silent) | −78.3 dBFS | −104.8 dBFS (noise floor) |
| Phase 12 build (before the fix), menu | −84.3 dBFS | −85.3 dBFS |

## 2. Audio identity and content

- **Identity:**
  - Warm, soft, playful and cozy, at 74–90 BPM.
  - One original 16-bar F-major motif with a counter-line. The menu tells it plainly; each World re-voices it (instruments, key, tempo).
- **Music:**
  - The menu loop plays continuously across Menu, Studio, World select and Settings.
  - There are 9 World arrangements.
  - The Menu ↔ Gameplay context crossfades over 1.2 s, and a level change inside a World never restarts the music.
- **Ambience:** 9 beds of 30 s each, built from noise and drone beds with scattered events. They only play during gameplay.
- **Effects:**
  - Movement: 30 cues, including 4 move variants, blocked ×2, collision ×2, settle and sleep.
  - Mechanics: frost slide and stop, fan, portal in/out/blocked, lava sizzle and bubble.
  - Flow: complete, perfect, deadlock, undo, restart.
  - UI and rewards: tap, back, toggle, Dream Dust, cosmetic unlock, World unlock.
- **Sonic logo:** 1.15 s; it plays with the splash and stays silent when muted.
- **Mixing:**
  - Buses Master (mute), Music, SFX, Ambience and UI (0.85 trim).
  - Six effect voices with priority stealing; variants never repeat back to back.
  - Hero cues duck the beds to 0.55.
  - Cues are timed on the game's own clock and synced to authoritative engine events: sleep after settle, the jingle after the board settles, Dream Dust after the jingle.
  - Pending cues are cancelled on Undo, Restart, level change, mute and pause.
- **Settings:**
  - Music 45 %, Effects 80 % (previewed while dragging, throttled), Ambience 30 %, Master mute, haptics on by default.
  - Restore Defaults resets sound and haptics; frame rate and Reduced Motion stay as the player set them.
  - Everything persists, and legacy settings are migrated: an untouched old default becomes the new default, a chosen value is kept.
  - When muted, no confirmation sound plays.
- **iOS:** the audio session is Ambient, so game audio follows the ring/silent switch and mixes with the player's own music.
- **Provenance:**
  - Everything is synthesised by `Tools/AudioSynth`: deterministic, seed 20261127, runtime version `S02-audio-1`.
  - No samples, loops, sample packs or third-party recordings; the music is an original composition.
  - `/Volumes/PLOP_DEV/Marketing/Audio/MANIFEST.md` (and `manifest.json`) lists every file with its duration, use, source, licence and runtime path.
  - The masters there are 24-bit 44.1 kHz WAV. The sonic logo for Sprint 03 is `Marketing/Audio/SFX/PLOP_Sfx_sonic_logo.wav`, with a copy at `Marketing/Branding/PLOP_SonicLogo_master.wav`.

### Audio QA gate

| Check | How | Result |
|---|---|---|
| Clipping, levels | Peak/RMS per file (manifest) and waveform sheets | No clipping; music −20 RMS / peaks −5 to −7; ambience −26; effects peak-normalised |
| Speaker band | Log spectrograms with 400 Hz / 4 kHz lines (`audio-qa/qa-*.png`) | Every effect has most of its energy in the band (low-band offenders were re-pitched) |
| Loop seams | End→start step and seam ratio (manifest, the `PlopAudioTests` WAV seam test) | Seamless: music ≤ 3× the median step; ambience crossfade-folded |
| Tails | Tail sheet | Every effect fades to exactly 0 (they were truncated before the fix) |
| Audible in build | Emulator output capture (above) | Audible output, mute verified |
| **Human listening** | — | **Not done.** Nobody has listened to this audio. Taste, balance on a real phone speaker and fatigue over a long session are still open (see Remaining work) |

One open visual-QA observation: the Breezy Meadow music shows dark columns in the high-frequency noise. They're probably the flute's breath noise stopping at rests. Not investigated.

## 3. Localization

- Languages: EN, DE, UK, RU, FR, ES, IT, PT-BR. 167 source strings plus plural forms (CLDR rules: one/few/many for uk and ru).
- Labels re-translate live. The new Settings ▸ Language row cycles through the languages. The device language is used until the player chooses one.
- **All seven translations are AI-drafted and unverified by native speakers.** See `Documentation/LOCALIZATION.md`.
- Device check (Android emulator): German, French, Russian and Ukrainian menu and Settings captured (`Documentation/FinalSprint02/localization/`). Every label fits, and the new Language row fits the Settings card.
- **Cyrillic is drawn by the OS fallback font,** because the bundled Nunito subset has no Cyrillic. It's readable but not the rounded Nunito style. Fix: bundle the full OFL Nunito. That needs the owner's OK to download (name, source and size will be stated when asking).

## 4. Visual polish and branding

- Main menu: a soft dawn gradient and 14 drifting "dream dust" motes behind the controls. They're static under Reduced Motion, and no control moves.
- Art packs verified byte for byte: Phase 13 branding pack 48/48 files, Phase 11.5 art pack 19/19.
- **Branding stays provisional:**
  - The in-game defaults are Hero_Pink icon, Jelly Pink logo and Day splash.
  - Every alternative, plus the owner's Sprint 02 feature-graphic reference, is in `/Volumes/PLOP_DEV/Marketing/Branding/` with a README.
  - The splash plays the sonic logo and respects mute.
- Not done this sprint: character, mechanic-VFX and World art refinement, and a broader animation pass. The Phase 11.5/11.6 art is unchanged. This was a deliberately small, low-risk pass, because audio was the P0.

## 5. Builds

All builds are in `/Volumes/PLOP_DEV/Builds/FinalSprint/02_FinalProduction/`, with full hashes and signing details in `BUILD_MANIFEST.md` there. Source commit: `f82e273`.

| Build | Size | Result |
|---|---:|---|
| `Android/PLOP-1.0.0-S02-dev.apk` | 58.9 MB | Development. Emulator: audio measured, 15 stills, localization checks |
| `Android/PLOP-1.0.0-S02-release-testsigned.apk` | 48.5 MB | Release config, **debug-keystore signed** (test only). Emulator: installed over the dev build with the save kept; cold start → menu with audio (peak −21.3 dBFS at the saved 15 %); no crash |
| `Android/PLOP-1.0.0-S02-release-testsigned.aab` | 48.1 MB | Release App Bundle, debug-keystore signed. Built and hashed, not installed |
| `iOS/PLOP-Xcode-Simulator` → `PLOP.app` (simulator) | 376 MB app | Xcode **BUILD SUCCEEDED**. iPhone 15 Pro Max simulator (iOS 26.3): 9 runs, each replaying a World's solution to **Perfect!** |

There's no iOS device build or production signing; both are owner actions. Nothing was uploaded anywhere.

## 6. Tests

- Unity EditMode: 203/203.
- Unity PlayMode: 162/162. New suites:
  - `PlopAudioTests`: asset presence, WAV loop seams, defaults, crossfade, no restart on level change, coalescing, mute, focus/pause, variants, engine cues, completion scheduling and cancellation, ducking, haptics, legacy migration, round-trip, Restore Defaults, controller music context, sonic logo.
  - `LocalizationTests`: table completeness, placeholders, plural forms, CLDR rules, device language, live relabelling, the Settings language row, every shown label has a key.
- .NET core: 203/203. Core is unchanged.

## 7. Marketing exports

| Folder | Contents |
|---|---|
| `/Volumes/PLOP_DEV/Marketing/Audio/` | 49 masters and the manifest |
| `/Volumes/PLOP_DEV/Marketing/Branding/` | Provisional selection, alternatives, in-game review captures, sonic logo |
| `/Volumes/PLOP_DEV/Marketing/RawScreenshots/` | 15 stills (1080×2400) from the running Android dev build, per `Documentation/Marketing/SHOT_LIST.md`. All 9 Worlds plus menu, Perfect card, World picker, Studio, hint and lava recovery |
| `/Volumes/PLOP_DEV/Marketing/RawGameplay/` | 9 iOS Simulator clips (H.264, 25–27 s each), one per World: the build replaying that World's verified shot-list solution to the Perfect! card. They start with a ~10–13 s black launch lead-in to trim, and have **no audio track** (use `Marketing/Audio/`) |

## Remaining work (for Sprint 03+)

1. **Human listening pass** on real phones (speaker and headphones): balance, fatigue, taste.
2. **Native-speaker review** of the seven languages, and bundling full Nunito for Cyrillic (needs the owner's go-ahead for the download).
3. **Owner choices:** final branding variant, feature graphic, trademark check.
4. **Physical devices:** no phone was available, so there's no real speaker, haptics, battery or thermal check.
5. **Art:** character, VFX and World refinement, plus the animation pass, carried over.
6. **Production signing:** the release builds are signed with the debug keystore; production signing is an owner action.
