#!/usr/bin/env python3
"""PLOP! Official Trailer V2 (Sprint 03.1) — 1920×1080, 30 fps, 30.67 s, 90 BPM (beat 0.667 s, bar 2.667 s).

  S01  0.00– 2.00  v2_world      layered island, boom down + push, canopy/lantern sway, sleeping Plop breathing (eyes closed)
  S02  2.00– 5.33  v2_hero       macro close-up: hero wakes (eyes open, blink, smile, hop with squash), friend reacts
  S03  5.33– 8.00  v2_adventure  fan spins up, wind loops round, hero slides over the ice, camera follows
  S04  8.00– 9.33  (cont.)       warp into the vortex, camera pushes through; match reveal out of the in-game portal tile
  S05  9.33–13.33  L057 portal level, large board (layout A)                    ONE SWIPE. / MANY POSSIBILITIES.
  S06 13.33–17.33  L019 frost | L027 fans (layout B) → L046 wrong swipe (0.7 s) → L046 solution (layout C)   THINK BEFORE / YOU SWIPE.
  S07 17.33–21.33  L144 Moonlit | L102 Three Plops (layout D) → L155 Challenge to Perfect (layout E)       LITTLE PLOPS. / BIG PUZZLES.
  S08 21.33–24.00  v2_studio     the hero tries real cosmetics, one per beat                               MAKE THEM / YOURS.
  S09 24.00–28.00  v2_night      new night composition: Plops hop into their Nests, eyes close, breathing, subtle z's   SWEET DREAMS.
  S10 28.00–30.67  end card      night defocused, approved logo jelly-lands, "Sleepy Puzzle", sonic logo
Gameplay is real iOS Simulator footage of the dev build replaying verified solutions; SFX are placed at the game's own
event times. Cinematic shots: owner's island illustration split into depth layers + the game's Plop rig rendered in Unity."""
import sys, os, json, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit2 import *

W, H, D = 1920, 1080, 30.667
OUT = V2 + "/02_OfficialTrailer"
SCORE = V2 + "/05_Audio/Score_OfficialV2_90bpm_48k.wav"
AMB = "/Volumes/PLOP_DEV/Marketing/Audio/Ambience/PLOP_Ambience_breezy-meadow.wav"
AMB_NIGHT = "/Volumes/PLOP_DEV/Marketing/Audio/Ambience/PLOP_Ambience_moonlit-clouds.wav"
LOGO = BRAND + "/Logos/01_Jelly_Pink.png"
TIGHT = [20, 740, 1250, 1400]            # board only (works for 5×5 … 6×7 boards)
PERFECT = [0, 740, 1290, 1800]           # board + the in-game "Perfect!" card
SEGS = []                                 # (start, end, shot, description) for Trailer_Edit_Timeline.json


def scene(e, name, at, dur, **p):
    e.add(type="scene2", scene=name, start=at, end=at + dur, params=p or None)


def bg(e, at, dur, top, bottom, **kw):
    e.fill(at, dur, top=top, bottom=bottom, **kw)


def panel(e, take, at, dur, sin, frame, crop=TIGHT, sfx=True, **kw):
    e.video(take, at, dur, sin, frame=frame, crop=crop, fit="cover", radius=34, shadow=38, sfx=sfx, **kw)


def headline(e, l1, l2, at, dur, x, y, align="center", size=86, c1=PLUM, c2=BERRY_D, caps=True):
    """Two-line headline, no container: line 1 plum, line 2 berry, staggered rise. Recorded for the caption files."""
    lh = size * 1.08
    for i, (s, c) in enumerate(((l1, c1), (l2, c2))):
        if not s: continue
        d = 0.09 * i
        e.add(type="text", text=s, start=at + d, end=at + dur, x=x, y=y + (i - 0.5) * lh if l2 else y, fontSize=size, font=HEAVY,
              color=c, align=align, tracking=size * 0.03, fadeIn=0.3, fadeOut=0.22, rise=28)
        e.overlays.append(e.layers.pop())
    if caps: e.captions.append((at, at + dur, (l1 + " " + (l2 or "")).strip()))


def build(clean=False):
    name = "PLOP_OfficialTrailer_V2_Clean" if clean else "PLOP_OfficialTrailer_V2_1920x1080"
    e = Edit(name, W, H, D, OUT, bitrate=18_000_000, background="#FFF6EE")
    e.target_lufs = -15.0
    H_ = (lambda *a, **k: None) if clean else (lambda *a, **k: headline(e, *a, **k))

    # ---------------- music & ambience (three emotional sections: wonder → play → lullaby)
    e.tracks.append({"file": SCORE, "at": 0, "in": 0, "len": D, "gainDb": 0, "fadeIn": 0.0, "fadeOut": 1.8, "stem": "music"})
    e.tracks.append({"file": AMB, "at": 0, "in": 0, "len": 9.4, "gainDb": -13, "fadeIn": 0.4, "fadeOut": 0.8, "stem": "ambience"})
    e.tracks.append({"file": AMB_NIGHT, "at": 23.8, "in": 0, "len": D - 23.8, "gainDb": -14, "fadeIn": 0.8, "fadeOut": 1.8, "stem": "ambience"})
    e.duck(8.0, 9.2, -6); e.duck(28.0, D, -7)

    # ---------------- S01–S04 cinematic (layered island + game rig)
    scene(e, "v2_world", 0.0, 2.0); SEGS.append((0.0, 2.0, "S01", "World awakens: layered island, boom down, sleeping Plop breathing"))
    scene(e, "v2_hero", 2.0, 3.333); SEGS.append((2.0, 5.333, "S02", "Hero close-up: wakes, blinks, smiles, hops; mint friend reacts"))
    e.cue("ui_toggle", 2.45, -16); e.cue("ui_tap", 3.55, -15, 0.35); e.cue("move_2", 4.45, -11); e.cue("dust", 4.72, -15)
    scene(e, "v2_adventure", 5.333, 4.2); SEGS.append((5.333, 8.0, "S03", "Fan spins up, wind loops, hero slides over ice; camera follows"))
    SEGS.append((8.0, 9.333, "S04", "Warp into the vortex, push through, reveal out of the in-game portal tile"))
    e.cue("fan", 5.45, -9, -0.3); e.cue("ice_slide", 6.3, -9); e.cue("ice_stop", 7.35, -11); e.cue("portal_in", 7.83, -7); e.cue("portal_out", 9.0, -8)

    # ---------------- S05 L057 — layout A: large board left, headline right; revealed from the portal tile
    P5 = [130, 40, 904, 1000]
    portal = (P5[0] + (430 - TIGHT[0]) * P5[3] / TIGHT[3], P5[1] + (927 - TIGHT[1]) * P5[3] / TIGHT[3])
    rv = 0.5   # reveal: a circle opens out of the in-game portal tile, matching the vortex we just flew through
    e.add(type="fill", top="#F6EEFF", bottom="#FFE8F0", start=8.95, end=8.95 + rv, maskCircle=[portal[0], portal[1], 20, 2300])
    bg(e, 8.95 + rv, 4.383 - rv, "#F6EEFF", "#FFE8F0")
    panel(e, "G_L057_Prism_PortalLava", 8.95, rv, 10.35, P5, sfx=False, maskCircle=[portal[0], portal[1], 20, 2300])
    panel(e, "G_L057_Prism_PortalLava", 8.95 + rv, 4.383 - rv, 10.35 + rv, P5, sfx=False)
    for t, c in cues("G_L057_Prism_PortalLava"):
        o = 8.95 + (t - 10.35)
        if 9.2 <= o < 13.28: e.cue(c, o, SFX_GAIN.get(c, -8))
    H_("ONE SWIPE.", "MANY POSSIBILITIES.", 9.6, 3.6, 1477, 540, size=66)
    SEGS.append((9.333, 13.333, "S05", "L057 (portal + lava): two Plops move on every swipe, portal hop, Plop sleeps — large board"))

    # ---------------- S06 — layout B (two boards) → C (one board, wrong swipe → solution)
    bg(e, 13.333, 4.0, "#EAF7F3", "#FFF6EE")
    pw, ph = 760, 865; gap = 60; x0 = (W - 2 * pw - gap) / 2; y0 = 190
    panel(e, "G_L019_FrostedPond_Frost", 13.333, 1.667, 13.4, [x0, y0, pw, ph])
    panel(e, "G_L027_BreezyMeadow_Fans", 13.333, 1.667, 13.4, [x0 + pw + gap, y0, pw, ph], sfx=False)
    for t, c in cues("G_L027_BreezyMeadow_Fans"):
        o = 13.333 + (t - 13.4)
        if c == "fan" and o < 15.0: e.cue(c, o, -9, 0.4)
    C6 = [(W - 860) / 2 + 300, 150, 860, 920]
    panel(e, "G_L046_EmberHollow_WrongSwipe", 15.0, 0.667, 12.25, C6, crop=[20, 700, 1250, 1316])
    e.cue("undo", 15.66, -8)
    panel(e, "G_L046_EmberHollow_Solution", 15.667, 1.666, 13.0, C6, crop=[20, 700, 1250, 1316])
    H_("THINK BEFORE", "YOU SWIPE.", 13.45, 1.5, W / 2, 98, size=64, caps=False)
    # one caption entry for the whole shot; on the single-board layout the headline moves to the left column
    if not clean: e.captions.append((13.45, 17.25, "THINK BEFORE YOU SWIPE."))
    H_("THINK BEFORE", "YOU SWIPE.", 15.0, 2.27, 400, 540, size=72, caps=False)
    SEGS.append((13.333, 17.333, "S06", "Frost (L019) | fans (L027); L046 wrong swipe → lava, 0.67 s incl. the dialog; undo → L046 solution"))

    # ---------------- S07 — layout D (two worlds) → E (Challenge to Perfect)
    bg(e, 17.333, 4.0, "#EEE9FF", "#FFF0F4")
    panel(e, "09_MoonlitClouds_L144", 17.333, 2.0, 14.3, [x0, y0, pw, ph])
    panel(e, "G_L102_ThreePlops_FrostFans", 17.333, 2.0, 15.3, [x0 + pw + gap, y0, pw, ph], sfx=False)
    for t, c in cues("G_L102_ThreePlops_FrostFans"):
        o = 17.333 + (t - 15.3)
        if c in ("ice_slide", "collision_2") and o < 19.3: e.cue(c, o, SFX_GAIN.get(c, -8) - 3, 0.4)
    PE = [W - 717 - 170, 40, 717, 1000]
    panel(e, "G_L155_Challenge15_AllMechanics", 19.333, 2.0, 20.6, PE, crop=PERFECT)
    H_("LITTLE PLOPS.", "BIG PUZZLES.", 17.45, 1.82, W / 2, 98, size=64, caps=False)
    if not clean: e.captions.append((17.45, 21.25, "LITTLE PLOPS. BIG PUZZLES."))
    H_("LITTLE PLOPS.", "BIG PUZZLES.", 19.333, 1.95, 560, 540, size=80, caps=False)
    SEGS.append((17.333, 21.333, "S07", "Moonlit Clouds L144 | Three Plops L102; L155 Challenge (15 moves) finishing Perfect"))

    # ---------------- S08 — cosmetics on the beat
    scene(e, "v2_studio", 21.333, 2.667, beat=0.6667, cx=720, floor=250, u=600)
    for k in range(1, 4): e.cue("cosmetic_unlock", 21.333 + k * 0.6667, -9)
    H_("MAKE THEM", "YOURS.", 21.45, 2.45, 1400, 540, size=96)
    SEGS.append((21.333, 24.0, "S08", "Hero tries real cosmetics (bow, flower, crown, nightcap), one per beat"))

    # ---------------- S09–S10 — night, then end card
    scene(e, "v2_night", 24.0, D - 24.0, end=4.0)
    e.cue("settle", 24.25, -10); e.cue("sleep", 25.62, -12, -0.3); e.cue("sleep", 26.32, -9, 0.2)
    H_("SWEET DREAMS.", "", 26.75, 1.2, W / 2, 150, size=60, c1="#FFF6EE")
    SEGS.append((24.0, 28.0, "S09", "Night: four Plops in their Nests, eyes fully closed, breathing; z's only after eyes close"))
    lw, lh = 659 * 1.12, 405 * 1.12
    e.sprite(LOGO, 28.05, D - 28.05, size=[lw, lh], anchor=[0.5, 0.62], kf=[
        {"t": 0.0, "x": 960, "y": 520, "sx": 0.55, "sy": 0.55, "o": 0},
        {"t": 0.22, "x": 960, "y": 520, "sx": 1.1, "sy": 0.9, "o": 1, "ease": "out"},
        {"t": 0.42, "x": 960, "y": 520, "sx": 0.96, "sy": 1.05, "ease": "inOut"},
        {"t": 0.62, "x": 960, "y": 520, "sx": 1.01, "sy": 0.99, "ease": "inOut"},
        {"t": 0.8, "x": 960, "y": 520, "sx": 1.0, "sy": 1.0, "ease": "inOut"}], shadow=30)
    e.add(type="text", text="Sleepy Puzzle", start=28.55, end=D, x=960, y=790, fontSize=58, font=HEAVY, color="#FFF6EE",
          tracking=58 * 0.06, fadeIn=0.45, rise=18, textShadow=True)
    e.cue("sonic_logo", 28.1, -2)
    SEGS.append((28.0, D, "S10", "End card: approved logo + Sleepy Puzzle subtitle, sonic logo; no date, badge or call to action"))
    return e


if __name__ == "__main__":
    os.makedirs(V2 + "/05_Audio", exist_ok=True)
    if not os.path.exists(SCORE) or "--score" in sys.argv:
        print(subprocess.run([DOTNET, AUDIO, "score", PROD + "/TrailerAudio/specs/score_official_v2.json", SCORE], capture_output=True, text=True).stdout.strip())
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for clean in (False, True):
        SEGS.clear()
        e = build(clean)
        if only and e.name not in only: continue
        e.render(audio=True, video="--audio-only" not in sys.argv)
    json.dump({"fps": 30, "width": W, "height": H, "duration": D, "bpm": 90, "shots": [dict(start=a, end=b, shot=s, content=c) for a, b, s, c in SEGS]},
              open(PROD + "/Timelines/Trailer_Edit_Timeline_shots.json", "w"), indent=1)
