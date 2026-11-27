# Audio provenance: Official Trailer V2

**Nobody has listened to this mix.** All of the numbers below are measurements. The mix needs a human listening pass on headphones and on a phone speaker before release.

## Sources

| Element | Source | Rights |
|---|---|---|
| Music: `Score_OfficialV2_90bpm_48k.wav` | A new arrangement of the **original PLOP! motif**. The melody, counter-line and chords are read from the game's `Tools/AudioSynth/Score.cs`, and it is played by the game's own synthesiser instruments (`Tools/AudioSynth/Instruments.cs`, `Dsp.cs`). Rendered by `08_ProductionSources/TrailerAudio` (`score` command) from `specs/score_official_v2.json`. | Project-original. No samples, no third-party music, no AI music generator. |
| Sound effects | The game's own SFX (`/Volumes/PLOP_DEV/Marketing/Audio/SFX/PLOP_Sfx_*.wav`). Gameplay SFX are placed at the real event times from `LevelProbe --cues` (Sprint 03 cue sheets) and offset to match the footage. | Project-original |
| Ambience | The game's `PLOP_Ambience_breezy-meadow.wav` (0–9.4 s) and `PLOP_Ambience_moonlit-clouds.wav` (23.8 s to the end) | Project-original |
| Sonic logo | `PLOP_Sfx_sonic_logo.wav` at 28.10 s | Project-original |

## Structure: three emotional sections at 90 BPM

1. **Wonder, 0–8.0 s.**
   - 0–5.33 s, intro: celesta, music box and pad.
   - 5.33–8.0 s, build: felt piano and kalimba arpeggio. The fan SFX enters at 5.45 s, the ice slide at 6.30 s and the portal at 7.83 s.
2. **Play, 8.0–24.0 s.**
   - 8.0–16.0 s, montage: flute lead with a marimba bed.
   - 16.0–24.0 s, drive: marimba lead with a kalimba arpeggio and bass push.
   - The music ducks by 6 dB for the portal dive and reveal (8.0–9.2 s; the `portal_out` cue lands on the reveal at 9.0 s).
   - Gameplay SFX follow the footage. Cosmetic changes land on the beats at 22.0, 22.67 and 23.33 s.
3. **Lullaby, 24.0–30.67 s.**
   - Outro: felt piano and music box.
   - The Nest settle SFX plays at 24.25 s, and the sleep SFX at 25.62 s (honey Plop) and 26.32 s (pink Plop).
   - The music ducks by 7 dB under the sonic logo at 28.10 s and fades out by the end.

Every SFX time is listed under `sync_points` in `../02_OfficialTrailer/Trailer_Edit_Timeline.json`.

## Master (measured)

| File | Rate | Integrated loudness (BS.1770, gated) | True peak (4× oversampled) |
|---|---|---|---|
| `Mix_PLOP_OfficialTrailer_V2_1920x1080.wav` | 48 kHz, 24-bit | **−15.0 LUFS** | **−1.9 dBTP** |
| `Mix_PLOP_OfficialTrailer_V2_Clean.wav` (identical mix) | 48 kHz | −15.0 LUFS | −1.9 dBTP |

- **Target:** −14 to −16 LUFS, true peak ≤ −1 dBTP. Both are met.
- **In the MP4s:** AAC-LC, 48 kHz stereo, 256 kbps. AAC encoding can move inter-sample peaks by a few tenths of a dB; the 0.9 dB of headroom covers that.
- **Stems:** in `Stems/` (music, sfx, ambience) for both versions, at the same gain as the master.
