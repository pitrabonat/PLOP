#!/usr/bin/env python3
"""Gameplay trailers: landscape (captioned + clean), App Store preview (captioned + clean), vertical social trailer.
Usage: python3 -I edit_trailers.py [name-filter]   (cut points are on 90 BPM bars, B = 2.667 s)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit import *

OUT = ROOT + "/04_Trailers"
SC_GAME = ROOT + "/09_Audio/Trailer/Score_gameplay.wav"
SC_VERT = ROOT + "/09_Audio/Trailer/Score_vertical.wav"
B = BAR
FM = first_move

# Studio cuts (Android emulator footage, preview room): item changes just after these source times.
STUDIO_CUTS = [8.55, 11.55, 14.55, 18.55]   # Tiny Crown, Nightcap, Little Star, Ribbon
WORLD_CUTS = [("02_FrostedPond_L013", 10.9), ("05_PrismObservatory_L056", 10.8), ("07_RainyWindow_L087", 14.8), ("09_MoonlitClouds_L144", 14.8)]

# (take, start, dur, src_in, caption-key or None)
def gameplay_shots(appstore=False):
    s = [
        ("G_L102_ThreePlops_FrostFans", 0.0, 1.5 * B, FM("G_L102_ThreePlops_FrostFans") - 0.35, "one_swipe"),
        ("G_L019_FrostedPond_Frost", 1.5 * B, 1.5 * B, FM("G_L019_FrostedPond_Frost") - 0.3, "every_plop"),
        ("G_L027_BreezyMeadow_Fans", 3 * B, B, FM("G_L027_BreezyMeadow_Fans") - 0.3, "elements"),
        ("G_L061_Prism_Portal", 4 * B, B, FM("G_L061_Prism_Portal") - 0.25, None),
        ("G_L046_EmberHollow_WrongSwipe", 5 * B, B, FM("G_L046_EmberHollow_WrongSwipe") - 0.5, "think_swipe"),
        ("G_L155_Challenge15_AllMechanics", 6 * B, 2 * B, 21.95 - 2 * B, "challenges"),
    ]
    return s


def landscape(name, captioned=True):
    W, H = 1920, 1080
    e = Edit(name, W, H, 29.9, OUT, profile=None, bitrate=14_000_000)
    e.music(SC_GAME, fadeOut=0.6)
    ph = 960; pw = round(ph * CROP_BOARD[2] / CROP_BOARD[3]); py = (H - ph) // 2
    for i, (take, at, dur, sin, cap) in enumerate(gameplay_shots()):
        right = [True, False, True, True, False, True][i]   # fans+portal share one caption, so keep their side
        px = (W - pw) // 2 if not captioned else (W - pw - 150 if right else 150)
        e.backdrop(take, at, dur, sin)
        e.panel(take, at, dur, sin, [px, py, pw, ph])
        if cap:
            span = dur if take != "G_L027_BreezyMeadow_Fans" else 2 * B   # elements caption spans fans + portal
            cx = px / 2 if right else (px + pw + W) / 2
            e.caption(cap, at + 0.12, span - 0.2, cx, H / 2, 84, burn=captioned)
    # worlds montage
    at0 = 8 * B
    for j, (take, sin) in enumerate(WORLD_CUTS):
        at = at0 + j * B / 4
        px = (W - pw - 150) if captioned else (W - pw) // 2
        e.backdrop(take, at, B / 4, sin)
        e.panel(take, at, B / 4, sin, [px, py, pw, ph])
    e.caption("worlds", at0 + 0.1, B - 0.15, (W - pw - 150) / 2, H / 2 - 40, 84, burn=captioned)
    if captioned: e.text(T("worlds_sub"), at0 + 0.35, B - 0.4, (W - pw - 150) / 2, H / 2 + 150, 40, font=HEAVY, color=BERRY_D, tracking=3)
    # studio
    at0 = 9 * B
    sw, sh = 946, 820; sx = (W - sw - 120) if captioned else (W - sw) // 2; sy = (H - sh) // 2
    for j, sin in enumerate(STUDIO_CUTS):
        at = at0 + j * B / 4
        e.video("A_Studio_Showcase", at, B / 4, sin, crop=CROP_STUDIO, fit="cover", blur=40, tint=CREAM, tintAmount=0.35, sfx=False)
        e.video("A_Studio_Showcase", at, B / 4, sin, frame=[sx, sy, sw, sh], crop=CROP_STUDIO, fit="cover", radius=40, shadow=46, sfx=False)
        e.cue("ui_tap", at + 0.05, -12)
    e.caption("own", at0 + 0.1, B - 0.15, (W - sw - 120) / 2, H / 2, 84, burn=captioned)
    end_card(e, 10 * B, 29.9 - 10 * B, captioned)
    e.poster = 5.0
    return e


def end_card(e, at, dur, captioned=True, vertical=False):
    W, H = e.W, e.H
    e.fill(at, dur, top=CREAM, bottom="#FBD6E3", fadeIn=0.3)
    S = BRAND + "/Sprites"
    if vertical:
        lw = 860; lx, ly = (W - lw) / 2, 330
        plops = [("Plop_Berry", 175, 1430, 220, 0.0), ("Plop_Honey", 415, 1440, 240, 0.12), ("Plop_Mint", 665, 1440, 240, 0.24), ("Plop_Lilac", 905, 1430, 220, 0.36)]
        tag_y, cs_y = 960, 1100
    else:
        lw = 740; lx, ly = (W - lw) / 2, 150
        plops = [("Plop_Berry", 300, 800, 300, 0.0), ("Plop_Mint", 1620, 800, 300, 0.12), ("Plop_Honey", 520, 930, 190, 0.24), ("Plop_Lilac", 1400, 930, 190, 0.36)]
        tag_y, cs_y = 690, 830
    lh = lw * 405 / 659
    for name, x, y, sz, d in plops:
        e.sprite(f"{S}/{name}.png", at + d, dur - d, size=[sz, sz], anchor=[0.5, 0.9],
                 kf=[{"t": 0, "x": x, "y": y + 260, "s": 0.6, "o": 0}, {"t": 0.45, "x": x, "y": y, "s": 1.0, "o": 1, "ease": "back"}],
                 squash=[0.035, 1.1, d * 9], bob=[10, 0.75, d * 7])
    e.add(type="image", src=BRAND + "/Logos/01_Jelly_Pink.png", start=at + 0.15, end=at + dur, frame=[lx, ly, lw, lh], fit="contain", fadeIn=0.35)
    e.cue("sonic_logo", at + 0.2, SFX_GAIN["sonic_logo"])
    e.duck(at, at + 1.6, -6)
    if captioned:
        e.caption("little_big", at + 0.7, dur - 0.7, W / 2, tag_y, 60 if not vertical else 68, pill={"color": "#00000000"}, fadeOut=0)
        e.text(T("coming_soon"), at + 1.2, dur - 1.2, W / 2, cs_y, 44 if not vertical else 50, color="#FFFFFF", tracking=4,
               pill={"color": BERRY_D, "padX": 40, "padY": 20, "radius": 40}, fadeOut=0)
    else:
        e.text(T("coming_soon"), at + 1.0, dur - 1.0, W / 2, tag_y + 40, 44 if not vertical else 50, color="#FFFFFF", tracking=4,
               pill={"color": BERRY_D, "padX": 40, "padY": 20, "radius": 40}, fadeOut=0)


def app_preview(name, captioned=True):
    """App Store App Preview: 886×1920, 30 fps, H.264 High 4.0, ≤30 s, in-app footage only (iOS Simulator captures)."""
    W, H = 886, 1920
    e = Edit(name, W, H, 29.5, ROOT + "/06_StoreAssets/AppStore/AppPreview", profile="high40cbr", bitrate=11_500_000)
    e.music(SC_GAME, length=29.5, fadeOut=1.0)
    shots = gameplay_shots() + [("G_L067_ThreePlops_FrostLava", 8 * B, 29.5 - 8 * B, 11.93, None)]
    for take, at, dur, sin, cap in shots:
        e.video(take, at, dur, sin, frame=[0, 0, W, H], fit="cover")
        if cap:
            span = 2 * B if take == "G_L027_BreezyMeadow_Fans" else dur
            e.caption(cap, at + 0.12, span - 0.2, W / 2, 168, 50, burn=captioned)
    e.caption("little_big", 8 * B + 0.2, 29.5 - 8 * B - 0.4, W / 2, 168, 50, burn=captioned)
    e.poster = 2.0
    return e


def vertical(name):
    W, H = 1080, 1920
    D = 24.9
    e = Edit(name, W, H, D, OUT, bitrate=14_000_000)
    e.music(SC_VERT, fadeOut=0.8)
    pw = 780; ph = round(pw * CROP_BOARD[3] / CROP_BOARD[2]); px = (W - pw) // 2; py = 500
    shots = [
        ("G_L102_ThreePlops_FrostFans", 0.0, 1.5 * B, FM("G_L102_ThreePlops_FrostFans") - 0.35, "every_plop"),
        ("G_L019_FrostedPond_Frost", 1.5 * B, B, FM("G_L019_FrostedPond_Frost") + 0.6, "think_slide"),
        ("G_L061_Prism_Portal", 2.5 * B, B, FM("G_L061_Prism_Portal") - 0.25, "elements"),
        ("G_L027_BreezyMeadow_Fans", 3.5 * B, B, FM("G_L027_BreezyMeadow_Fans") - 0.3, None),
        ("G_L046_EmberHollow_WrongSwipe", 4.5 * B, B, FM("G_L046_EmberHollow_WrongSwipe") - 0.5, "think_swipe"),
        ("G_L155_Challenge15_AllMechanics", 5.5 * B, 1.5 * B, 21.95 - 1.5 * B, "challenges"),
    ]
    for take, at, dur, sin, cap in shots:
        e.backdrop(take, at, dur, sin)
        e.panel(take, at, dur, sin, [px, py, pw, ph])
        if cap:
            span = 2 * B if take == "G_L061_Prism_Portal" else dur
            e.caption(cap, at + 0.1, span - 0.18, W / 2, 330, 70 if cap != "every_plop" else 76)
    at0 = 7 * B
    sw = 880; sh = round(sw * CROP_STUDIO[3] / CROP_STUDIO[2]); sx = (W - sw) // 2
    for j, sin in enumerate(STUDIO_CUTS):
        at = at0 + j * B / 4
        e.video("A_Studio_Showcase", at, B / 4, sin, crop=CROP_STUDIO, fit="cover", blur=40, tint=CREAM, tintAmount=0.35, sfx=False)
        e.video("A_Studio_Showcase", at, B / 4, sin, frame=[sx, 560, sw, sh], crop=CROP_STUDIO, fit="cover", radius=44, shadow=46, sfx=False)
        e.cue("ui_tap", at + 0.05, -12)
    e.caption("own", at0 + 0.1, B - 0.15, W / 2, 330, 72)
    end_card(e, 8 * B, D - 8 * B, True, vertical=True)
    e.poster = 1.2
    return e


if __name__ == "__main__":
    f = sys.argv[1] if len(sys.argv) > 1 else ""
    jobs = [landscape("PLOP_GameplayTrailer_1920x1080_Captioned", True), landscape("PLOP_GameplayTrailer_1920x1080_Clean", False),
            app_preview("PLOP_AppPreview_886x1920_Captioned", True), app_preview("PLOP_AppPreview_886x1920_Clean", False),
            vertical("PLOP_VerticalTrailer_1080x1920")]
    for e in jobs:
        if f in e.name:
            print("==", e.name); e.render(video="--novideo" not in sys.argv)
