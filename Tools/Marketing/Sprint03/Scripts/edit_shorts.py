#!/usr/bin/env python3
"""Vertical short videos (1080×1920): five challenge videos (10–15 s) and the mascot showcase (~9 s).
Every board is a real level, played by the game itself replaying its verified solution (iOS Simulator dev build).
Layout keeps text and the board inside the common TikTok / Reels / Shorts safe area (top 250 px, bottom 420 px,
right 140 px stay free of essentials)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit import *

W, H = 1080, 1920
OUT = ROOT + "/05_ShortVideos"
THINK = ROOT + "/09_Audio/Trailer/Score_Challenge_Think.wav"
MASCOT = ROOT + "/09_Audio/Trailer/Score_Mascot.wav"
PW = 800; PH_ = round(PW * CROP_BOARD[3] / CROP_BOARD[2]); PX = (W - PW) // 2 - 30; PY = 470   # nudged left of the right-side UI rail


def board(e, take, at, dur, sin=None, hold=None, sfx=True):
    if hold is not None:
        e.video(take, at, dur, 0, crop=[0, 300, IOS_W, 2200], fit="cover", blur=34, tint=CREAM, tintAmount=0.42, sfx=False, hold=hold)
        e.video(take, at, dur, 0, frame=[PX, PY, PW, PH_], crop=CROP_BOARD, fit="cover", radius=40, shadow=46, sfx=False, hold=hold)
    else:
        e.backdrop(take, at, dur, sin)
        e.panel(take, at, dur, sin, [PX, PY, PW, PH_], sfx=sfx)


def end_tag(e, at, dur):
    e.fill(at, dur, top=CREAM, bottom="#FBD6E3", fadeIn=0.25)
    lw = 700
    e.add(type="image", src=BRAND + "/Logos/01_Jelly_Pink.png", start=at + 0.1, end=at + dur, frame=[(W - lw) / 2, 560, lw, lw * 405 / 659], fit="contain", fadeIn=0.3)
    for i, (n, x) in enumerate([("Plop_Berry", 300), ("Plop_Mint", 540), ("Plop_Honey", 780)]):
        e.sprite(f"{BRAND}/Sprites/{n}.png", at + 0.1 * i, dur - 0.1 * i, size=[220, 220], anchor=[0.5, 0.9],
                 kf=[{"t": 0, "x": x, "y": 1330, "s": 0.5, "o": 0}, {"t": 0.4, "x": x, "y": 1250, "s": 1, "o": 1, "ease": "back"}], squash=[0.03, 1.2, i * 2.0])
    e.text(T("coming_soon"), at + 0.35, dur - 0.35, W / 2, 1060, 48, color="#FFFFFF", tracking=4, pill={"color": BERRY_D, "padX": 40, "padY": 20, "radius": 40}, fadeOut=0)
    e.cue("sonic_logo", at + 0.1, -2); e.duck(at, at + 1.5, -6)


def challenge(name, key, take, think=2.8, wrong=None, reveal_key=None, extra_key=None, extra_cue="sleep", tail=0.9, sub_key=None):
    """Question over the frozen opening board → countdown → the game plays the solution → Perfect card → end tag."""
    fm = first_move(take); r = TAKES[take]
    n_moves = len(r["moves"].lstrip("."))
    play_from = fm - 0.35
    play = n_moves * 0.45 + 0.35 + 1.4 + tail    # moves + Perfect card
    hold_at = float(r["board_visible_s"]) + 0.8
    t = 0.0
    segs = []
    total = think + (wrong[1] if wrong else 0) + play + 1.8
    e = Edit(name, W, H, round(total, 2), OUT, bitrate=12_000_000)
    e.music(THINK, length=total, fadeOut=1.2)
    board(e, take, 0, think, hold=hold_at)
    e.caption(key, 0.15, think + (wrong[1] if wrong else 0) - 0.2, W / 2, 300, 66)
    for k, n in enumerate(range(3, 0, -1)):   # 3 · 2 · 1
        step = (think - 0.35) / 3; at = 0.3 + k * step
        e.text(str(n), at, step - 0.05, W / 2 - 30, PY + PH_ / 2, 220, color="#FFFFFF", fadeIn=0.1, fadeOut=0.15, rise=0,
               outline=BERRY_D, outlineWidth=18, textShadow=True)
        e.cue("ui_tap", at, -14)
    t = think
    if wrong:   # the wrong swipe, played for real (lava → "Oops! Too hot!")
        wt, wdur = wrong
        board(e, wt, t, wdur, first_move(wt) - 0.35)
        t += wdur
    board(e, take, t, play, play_from)
    rk = reveal_key or "answer"
    e.text(T(rk), t + 0.05, 1.6, W / 2, 300, 64, color="#FFFFFF", tracking=2, pill={"color": BERRY_D, "padX": 44, "padY": 24, "radius": 46})
    e.captions.append((t + 0.05, t + 1.65, rk))
    if extra_key:   # e.g. "ORDER MATTERS!" when the first Plop falls asleep
        first = next((tt for tt, c in cues(take) if c == extra_cue and tt >= play_from), None)
        if first is not None:
            at = t + (first - play_from)
            e.text(T(extra_key), at, 1.8, W / 2, 300, 60, color=PLUM, tracking=2, pill={"color": CREAM + "F2", "padX": 40, "padY": 24, "radius": 44})
            e.captions.append((at, at + 1.8, extra_key))
    end_tag(e, total - 1.8, 1.8)
    e.poster = 1.0
    return e


def mascot():
    D = 9.0
    e = Edit("PLOP_MascotShowcase_1080x1920", W, H, D, OUT, bitrate=12_000_000)
    e.music(MASCOT, length=D, fadeOut=1.0)
    cuts = [(2.7, 1.1), (4.6, 1.0), (8.55, 0.75), (11.55, 0.75), (14.55, 0.75), (18.55, 0.75), (21.55, 0.75), (24.6, 0.75), (29.3, 0.8)]
    sw = 1000; sh = round(sw * CROP_STUDIO[3] / CROP_STUDIO[2]); sx = (W - sw) // 2 - 20; sy = 520
    at = 0.0
    for sin, d in cuts:
        e.video("A_Studio_Showcase", at, d, sin, crop=CROP_STUDIO, fit="cover", blur=40, tint=CREAM, tintAmount=0.35, sfx=False)
        e.video("A_Studio_Showcase", at, d, sin, frame=[sx, sy, sw, sh], crop=CROP_STUDIO, fit="cover", radius=48, shadow=50, sfx=False)
        e.cue("ui_tap", at + 0.04, -12)
        at += d
    e.cue("collision_1", 0.25, -12); e.cue("collision_2", 1.35, -12)
    e.caption("meet_plop", 0.15, 2.0, W / 2, 330, 72)
    e.caption("own", 2.2, at - 2.35, W / 2, 330, 72)
    e.text(T("studio_sub"), 2.5, at - 2.6, W / 2, 1480, 44, font=HEAVY, color=BERRY_D, tracking=3)
    e.captions.append((2.5, at - 0.1, "studio_sub"))
    end_tag(e, at, D - at)
    e.poster = 3.0
    return e


def jobs():
    return [
        challenge("PLOP_Challenge01_SolveIn6_L027", "solve_in_6", "G_L027_BreezyMeadow_Fans", think=3.3),
        challenge("PLOP_Challenge02_WhoSleepsFirst_L033", "sleep_first", "G_L033_BreezyMeadow_Coordinate", extra_key="order_matters"),
        challenge("PLOP_Challenge03_WhereIceTakesYou_L019", "ice_where", "G_L019_FrostedPond_Frost", extra_key="wheee", extra_cue="ice_slide"),
        challenge("PLOP_Challenge04_WrongSwipe_L046", "wrong_swipe", "G_L046_EmberHollow_Solution", think=1.8,
                  wrong=("G_L046_EmberHollow_WrongSwipe", 3.1), reveal_key="try_again"),
        challenge("PLOP_Challenge05_PredictThePortal_L061", "portal_predict", "G_L061_Prism_Portal", extra_key="whoosh", extra_cue="portal_out"),
        mascot(),
    ]


if __name__ == "__main__":
    f = sys.argv[1] if len(sys.argv) > 1 else ""
    if not os.path.exists(MASCOT): score("score_mascot", MASCOT)
    for e in jobs():
        if f in e.name:
            print("==", e.name, e.dur); e.render(video="--novideo" not in sys.argv)
