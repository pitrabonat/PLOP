#!/usr/bin/env python3
"""Official cinematic trailer (1920×1080, ~37 s) + the individual cinematic shots + a storyboard sheet.

Bars at 90 BPM (B = 2.667 s), score sections: intro 2 | build 2 | montage 6 | outro 2 | ring 1.
  Shot 1  0.00– 5.33  island_wide       little world: pull back from the lantern, petals, rays, waterfall, clouds
  Shot 2  5.33– 8.00  island_plops      meet the Plops: each hops in turn, the "!" pops
  Shot 3  8.00–10.67  island_adventure  a Plop slides over the ice past the fan into the portal; camera dives in
  Shot 4 10.67–14.67  gameplay L061 (portal) revealed through a circle   ONE SWIPE. MANY POSSIBILITIES.
  Shot 5 14.67–18.67  gameplay L046 wrong swipe → "Oops! Too hot!"        THINK BEFORE YOU SWIPE.
  Shot 6 18.67–22.67  gameplay L155 Challenge (15 moves) to Perfect        LITTLE PLOPS. BIG PUZZLES.
  Shot 7 22.67–26.67  Character Studio previews                          MAKE THEM YOUR OWN.
  Shot 8 26.67–37.00  island_dusk: day turns to night, Zzz               SWEET DREAMS! → logo, PLOP! — SLEEPY PUZZLE, COMING SOON, sonic logo
Shots 1–3 and 8 are illustration (owner reference plate, re-composited); shots 4–7 are real game footage."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit import *

B = BAR
W, H = 1920, 1080
D = 37.0
SCORE = ROOT + "/09_Audio/Trailer/Score_Cinematic_90bpm.wav"
AMB = "/Volumes/PLOP_DEV/Marketing/Audio/Ambience/PLOP_Ambience_breezy-meadow.wav"
AMB_NIGHT = "/Volumes/PLOP_DEV/Marketing/Audio/Ambience/PLOP_Ambience_moonlit-clouds.wav"
STUDIO = [8.55, 11.55, 14.55, 18.55, 21.55, 24.6]


def scene(e, name, at, dur, **params):
    e.add(type="scene", scene=name, start=at, end=at + dur, params=dict(dur=dur, **params))


def gameplay(e, take, at, dur, sin, side, reveal=False):
    ph = 960; pw = round(ph * CROP_BOARD[2] / CROP_BOARD[3]); py = (H - ph) // 2
    px = W - pw - 150 if side == "right" else 150
    if reveal:   # circle wipe out of the portal flash
        r = 0.6
        e.add(type="fill", color=CREAM, start=at, end=at + r)
        for L in (dict(), dict(frame=[px, py, pw, ph], crop=CROP_BOARD, radius=40, shadow=46)):
            kw = dict(L)
            if not kw: kw = dict(crop=[0, 300, IOS_W, 2200], blur=34, tint=CREAM, tintAmount=0.42)
            e.add(type="video", src=src(take), start=at, end=at + r, **{"in": sin}, frame=kw.pop("frame", [0, 0, W, H]), fit="cover",
                  maskCircle=[W / 2, H / 2, 0, 1250], **kw)
        e.backdrop(take, at + r, dur - r, sin + r)
        e.panel(take, at + r, dur - r, sin + r, [px, py, pw, ph], sfx=False)
        for t, c in cues(take):
            o = at + (t - sin)
            if at <= o < at + dur - 0.05: e.cue(c, o, SFX_GAIN.get(c, -8))
    else:
        e.backdrop(take, at, dur, sin)
        e.panel(take, at, dur, sin, [px, py, pw, ph])
    return (px / 2) if side == "right" else (px + pw + W) / 2


def build():
    e = Edit("PLOP_OfficialTrailer_1920x1080", W, H, D, ROOT + "/02_Cinematic/Master", bitrate=16_000_000)
    e.music(SCORE, fadeOut=2.0)
    e.tracks.append({"file": AMB, "at": 0, "in": 0, "len": 10.67, "gainDb": -14, "fadeIn": 0.8, "fadeOut": 0.6, "stem": "ambience"})
    e.tracks.append({"file": AMB_NIGHT, "at": 26.67, "in": 0, "len": D - 26.67, "gainDb": -15, "fadeIn": 1.0, "fadeOut": 2.0, "stem": "ambience"})
    # 1–3 island
    scene(e, "island_wide", 0, 2 * B)
    e.fill(0, 0.9, color="#000000", fadeOut=0.9)
    e.cue("move_2", 3.6 + 0.12, -14)
    scene(e, "island_plops", 2 * B, B)
    for i, h in enumerate([0.25, 0.8, 1.25, 1.7]): e.cue(f"move_{i + 1}", 2 * B + h + 0.1, -10, pan=[-0.1, 0.1, -0.4, -0.3][i])
    e.cue("collision_1", 2 * B + 0.36, -14)
    a = 3 * B
    scene(e, "island_adventure", a, B)
    e.cue("ice_slide", a + 0.02, -8, 0.1); e.cue("ice_stop", a + 0.88, -10, 0.2); e.cue("fan", a + 0.1, -9, 0.5)
    e.cue("move_3", a + 0.92, -10, 0.3); e.cue("portal_in", a + 1.45, -7, 0.4); e.cue("portal_out", a + B - 0.3, -6)
    # 4–7 gameplay
    g = 4 * B
    cx = gameplay(e, "G_L061_Prism_Portal", g, 1.5 * B, first_move("G_L061_Prism_Portal") - 0.25, "right", reveal=True)
    e.caption("one_swipe", g + 0.7, 1.5 * B - 0.8, cx, H / 2, 84)
    g += 1.5 * B
    cx = gameplay(e, "G_L046_EmberHollow_WrongSwipe", g, 1.5 * B, first_move("G_L046_EmberHollow_WrongSwipe") - 0.5, "left")
    e.caption("think_swipe", g + 0.12, 1.5 * B - 0.2, cx, H / 2, 84)
    g += 1.5 * B
    cx = gameplay(e, "G_L155_Challenge15_AllMechanics", g, 1.5 * B, 21.95 - 1.5 * B, "right")
    e.caption("little_big", g + 0.12, 1.5 * B - 0.2, cx, H / 2, 84)
    g += 1.5 * B
    sw, sh = 946, round(946 * CROP_STUDIO[3] / CROP_STUDIO[2]); sx, sy = 150, (H - sh) // 2
    step = 1.5 * B / len(STUDIO)
    for j, sin in enumerate(STUDIO):
        at = g + j * step
        e.video("A_Studio_Showcase", at, step, sin, crop=CROP_STUDIO, fit="cover", blur=40, tint=CREAM, tintAmount=0.35, sfx=False)
        e.video("A_Studio_Showcase", at, step, sin, frame=[sx, sy, sw, sh], crop=CROP_STUDIO, fit="cover", radius=40, shadow=46, sfx=False)
        e.cue("ui_tap", at + 0.05, -12)
    e.caption("own", g + 0.12, 1.5 * B - 0.2, (sx + sw + W) / 2, H / 2, 84)
    # 8 dusk + end card
    d0 = 10 * B
    scene(e, "island_dusk", d0, D - d0)
    e.fill(d0, 0.5, color=CREAM, fadeOut=0.5)
    e.caption("sweet_dreams", d0 + 0.8, 12 * B - d0 - 1.0, W / 2, 170, 96, pill={"color": "#00000000"}, color="#FFFFFF", textShadow=True)
    lo = 12 * B   # ring bar: logo + sonic logo
    e.add(type="image", src=BRAND + "/Logos/01_Jelly_Pink.png", start=lo, end=D, frame=[(W - 700) / 2, 120, 700, 700 * 405 / 659], fit="contain", fadeIn=0.45)
    e.cue("sonic_logo", lo + 0.1, SFX_GAIN["sonic_logo"]); e.duck(lo, lo + 1.6, -6)
    e.text(T("tagline"), lo + 0.6, D - lo - 0.6, W / 2, 610, 50, color="#FFFFFF", tracking=3, pill={"color": PLUM + "B8", "padX": 36, "padY": 18, "radius": 40}, fadeOut=0)
    e.text(T("coming_soon"), lo + 1.1, D - lo - 1.1, W / 2, 720, 44, color="#FFFFFF", tracking=4,
           pill={"color": BERRY_D, "padX": 40, "padY": 20, "radius": 40}, fadeOut=0)
    e.captions.append((lo + 0.6, D, "tagline"))
    e.fill(D - 0.7, 0.7, color="#000000", fadeIn=0.7)
    e.target_lufs = -16
    e.poster = 33.5
    return e


def shots():
    """Clean cinematic shots (no captions/logo) for re-editing."""
    out = ROOT + "/02_Cinematic/Shots"
    res = []
    for i, (n, d) in enumerate([("island_wide", 2 * B), ("island_plops", B), ("island_adventure", B), ("island_dusk", 8.0)], 1):
        e = Edit(f"PLOP_Cinematic_Shot{i:02d}_{n}", W, H, round(d, 3), out, bitrate=16_000_000)
        scene(e, n, 0, d); res.append(e)
    return res


if __name__ == "__main__":
    f = sys.argv[1] if len(sys.argv) > 1 else ""
    if not os.path.exists(SCORE): score("score_cinematic", SCORE)
    for e in [build()] + shots():
        if f in e.name:
            print("==", e.name); e.render(video="--novideo" not in sys.argv, audio=not e.name.startswith("PLOP_Cinematic_Shot"))
