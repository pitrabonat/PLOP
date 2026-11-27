#!/usr/bin/env python3
"""Store screenshots (raw + composed, App Store and Google Play sizes, localised), Google Play feature graphics,
posters (1080×1350 + 1080×1080), and the social brand kit. Everything is rendered by plopcomp --still from editable
timelines in 10_ProductionSources/Timelines/Stills.  Usage: python3 -I graphics.py [screens|feature|posters|kit|all]"""
import sys, os, json, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit import *

STILLS = ROOT + "/03_RawGameplay/Stills"
SA = ROOT + "/06_StoreAssets"
SPR = BRAND + "/Sprites"
LOGO = BRAND + "/Logos/01_Jelly_Pink.png"
PLATE = ROOT + "/02_Cinematic/Source/Owner_Reference_Island_1774x887.png"

def fm(t): return first_move(t)

# ---- layer helpers ----
def vid(take, t, frame, crop=None, fit="cover", **kw): return dict(type="video", src=src(take), start=0, end=9, hold=t, frame=frame, crop=crop, fit=fit, **kw)
def img(path, frame, crop=None, fit="cover", **kw): return dict(type="image", src=path, start=0, end=9, frame=frame, crop=crop, fit=fit, **kw)
def fill(top, bottom=None): return dict(type="fill", start=0, end=9, top=top, bottom=bottom) if bottom else dict(type="fill", start=0, end=9, color=top)
def txt(s, x, y, size, color=PLUM, font=HEAVY, **kw): return dict(type="text", text=s, start=0, end=9, x=x, y=y, fontSize=size, color=color, font=font, **kw)
def spr(path, x, y, size, rot=0, anchor=(0.5, 0.5), shadow=None): return dict(type="image", src=path, start=0, end=9, size=[size, size], anchor=list(anchor), kf=[{"t": 0, "x": x, "y": y, "rot": rot}], shadow=shadow)
def scene(name, offset, **p): return dict(type="scene", scene=name, start=0, end=9, sceneOffset=offset, params=p)

# ---- sources: real frames (iOS takes) and real Android stills ----
SHOTS = [  # (headline key, source, bg top, bg bottom, sprite)
    ("little_big", ("ios", "G_L102_ThreePlops_FrostFans", fm("G_L102_ThreePlops_FrostFans") + 0.45 * 4 + 0.38), "#FFF1F5", "#FBD3E1", "Plop_Berry"),
    ("one_swipe", ("ios", "G_L057_Prism_PortalLava", fm("G_L057_Prism_PortalLava") + 0.45 * 5 + 0.38), "#F4EEFF", "#DCCCF7", "Plop_Lilac"),
    ("think_slide", ("ios", "G_L019_FrostedPond_Frost", fm("G_L019_FrostedPond_Frost") + 0.45 * 3 + 0.38), "#EEF8FF", "#CFE6F7", "Plop_Mint"),
    ("challenges", ("ios", "G_L155_Challenge15_AllMechanics", fm("G_L155_Challenge15_AllMechanics") + 0.45 * 8 + 0.38), "#F3F0FF", "#D9D2F5", "Plop_Honey"),
    ("worlds", ("worlds",), "#FFF6E8", "#FBE0C2", "Plop_Mint"),
    ("own", ("img", STILLS + "/SS_Acc_TinyCrown.png"), "#FFF0F4", "#F8D7E2", "Plop_Berry"),
    ("daily", ("img", STILLS + "/A_Daily.png"), "#EEF7FF", "#D3E8F8", "Plop_Lilac"),
    ("sweet_dreams", ("ios", "G_L067_ThreePlops_FrostLava", 18.2), "#F1FBF4", "#CFEEDB", "Plop_Honey"),
]
WORLD_SET = [("01_CozyNest_L004", 11.1), ("09_MoonlitClouds_L144", 15.0), ("04_EmberHollow_L046", 11.2)]
RAW_SIZE = {"ios": (1290, 2796), "img": (1080, 2400)}


def app_panel(source, frame, center_frac=0.52):
    """The real app screen cropped to the panel's aspect around the board (status bar excluded)."""
    x, y, w, h = frame
    kind = source[0]
    sw, sh = RAW_SIZE[kind]
    ch = min(sh - 150, sw * h / w); cy = sh * center_frac
    y0 = min(max(150 if kind == "ios" else 0, cy - ch / 2), sh - ch)
    crop = [0, round(y0), sw, round(ch)]
    if kind == "ios": return [vid(source[1], source[2], frame, crop, radius=w * 0.055, shadow=w * 0.05)]
    return [img(source[1], frame, crop, radius=w * 0.055, shadow=w * 0.05)]


def worlds_panel(frame):
    """Three real World boards stacked diagonally (middle one on top), filling the panel area."""
    x, y, w, h = frame
    ar = CROP_BOARD[3] / CROP_BOARD[2]
    bw = min(w * 0.68, h / (ar * (1 + 2 * 0.62)))
    bh = bw * ar; step = (h - bh) / 2
    place = [(WORLD_SET[0], x, y), (WORLD_SET[2], x, y + 2 * step), (WORLD_SET[1], x + w - bw, y + step)]
    return [vid(take, t, [px, py, bw, bh], CROP_BOARD, radius=bw * 0.06, shadow=bw * 0.05) for (take, t), px, py in place]


def screenshot(W, H, key, source, top, bottom, sprite, lang="en", with_text=True):
    L = [fill(top, bottom)]
    # soft brand blobs
    L.append(dict(type="image", src=SPR + "/Floor_Ivory.png", start=0, end=9, size=[W * 0.5, W * 0.5], anchor=[0.5, 0.5], kf=[{"t": 0, "x": W * 0.92, "y": H * 0.93, "o": 0.0}]))
    head_h = H * 0.185
    pw = W * 0.86; px = (W - pw) / 2; py = head_h + H * 0.02; ph = H - py - H * 0.03
    if source[0] == "worlds": L += worlds_panel([px, py + ph * 0.01, pw, ph * 0.98])
    else: L += app_panel(source, [px, py, pw, ph], 0.53 if source[0] == "ios" else 0.5)
    if source[0] != "worlds": L.append(spr(f"{SPR}/{sprite}.png", px + pw - W * 0.02, py - W * 0.02, W * 0.18, rot=-8, shadow=W * 0.02))
    if with_text:
        size = W * 0.071 if "\n" in T(key, lang) else W * 0.08
        longest = max(len(s) for s in T(key, lang).split("\n"))
        size = min(size, W * 0.92 / (longest * 0.64))
        L.append(txt(T(key, lang), W / 2, head_h * 0.56, size, PLUM, tracking=size * 0.02, lineHeight=1.04))
    return L


def screens():
    sizes = {"AppStore/Screenshots_6.9in_1320x2868": (1320, 2868), "AppStore/Screenshots_6.7in_1290x2796": (1290, 2796),
             "AppStore/Screenshots_6.5in_1284x2778": (1284, 2778), "AppStore/Screenshots_6.5in_1242x2688": (1242, 2688),
             "GooglePlay/Screenshots_1080x1920": (1080, 1920)}
    # raw frames, full resolution, straight from the footage / emulator
    raw = SA + "/RawScreenshots"; os.makedirs(raw, exist_ok=True)
    for i, (key, source, *_r) in enumerate(SHOTS, 1):
        if source[0] == "ios":
            still(f"raw_{i:02d}", 1290, 2796, [vid(source[1], source[2], [0, 0, 1290, 2796], fit="stretch")], f"{raw}/PLOP_Raw_{i:02d}_{source[1]}_{source[2]:.2f}s_1290x2796.png")
        elif source[0] == "img":
            shutil.copyfile(source[1], f"{raw}/PLOP_Raw_{i:02d}_{os.path.basename(source[1])[:-4]}_android_1080x2400.png")
        else:
            for take, t in WORLD_SET:
                still(f"raw_{i:02d}_{take}", 1290, 2796, [vid(take, t, [0, 0, 1290, 2796], fit="stretch")], f"{raw}/PLOP_Raw_{i:02d}_{take}_{t:.2f}s_1290x2796.png")
    langs = [l for l in LANGS if l not in ("ru", "uk")]   # bundled Nunito subset has no Cyrillic → see handoff (BLOCKED on font)
    for lang in langs:
        for folder, (W, H) in sizes.items():
            if lang != "en" and W not in (1320, 1080): continue   # localised: one Apple master size + Google
            d = f"{SA}/{folder}/{lang}"
            for i, (key, source, top, bottom, sprite) in enumerate(SHOTS, 1):
                still(f"ss_{lang}_{W}x{H}_{i:02d}", W, H, screenshot(W, H, key, source, top, bottom, sprite, lang), f"{d}/PLOP_{i:02d}_{key}_{W}x{H}_{lang}.png")
            print("screens", lang, folder)
    # promotional (textless) versions for re-layout
    d = SA + "/PromotionalScreenshots/Textless_1290x2796"
    for i, (key, source, top, bottom, sprite) in enumerate(SHOTS, 1):
        still(f"ss_textless_{i:02d}", 1290, 2796, screenshot(1290, 2796, key, source, top, bottom, sprite, with_text=False), f"{d}/PLOP_{i:02d}_{key}_textless.png")


def feature():
    W, H = 1024, 500
    out = SA + "/FeatureGraphic"
    lw = 330; lh = lw * 405 / 659
    a = [img(PLATE, [0, 0, W, H], fit="cover"), img(LOGO, [(W - lw) / 2, 24, lw, lh], fit="contain")]
    still("fg_A", W, H, a, f"{out}/PLOP_FeatureGraphic_A_ReferenceIsland_Logo_1024x500.png")
    b = [img(PLATE, [0, 0, W, H], fit="cover"), img(LOGO, [(W - 300) / 2, 18, 300, 300 * 405 / 659], fit="contain"),
         txt(T("little_big").replace("\n", " "), W / 2, 228, 26, "#FFFFFF", tracking=1.5, pill={"color": PLUM + "B0", "padX": 20, "padY": 10, "radius": 22})]
    still("fg_B", W, H, b, f"{out}/PLOP_FeatureGraphic_B_ReferenceIsland_Tagline_1024x500.png")
    # C: fully original — only game assets (no reference illustration): safe fallback if the plate is not cleared
    c = [fill("#FFF4F7", "#F7CFDF"), spr(f"{SPR}/Portal_Vortex.png", 935, 95, 120, 20), spr(f"{SPR}/Frost_Isolated.png", 85, 95, 100, -10)]
    for n, x, s_, r in [("Plop_Mint", 120, 175, -6), ("Plop_Berry", 300, 140, -3), ("Plop_Lilac", 724, 140, 3), ("Plop_Honey", 904, 175, 6)]:
        c.append(dict(spr(f"{SPR}/{n}.png", x, 492, s_, r, anchor=(0.5, 0.92), shadow=10)))
    c.append(img(LOGO, [(W - 380) / 2, 26, 380, 380 * 405 / 659], fit="contain"))
    c.append(txt(T("little_big").replace("\n", " "), W / 2, 296, 28, PLUM, tracking=1.5))
    still("fg_C", W, H, c, f"{out}/PLOP_FeatureGraphic_C_GameArt_1024x500.png")
    shutil.copyfile(f"{out}/PLOP_FeatureGraphic_C_GameArt_1024x500.png", f"{SA}/GooglePlay/PLOP_FeatureGraphic_1024x500.png")
    shutil.copyfile(BRAND + "/AppIcons/Hero_Pink/GooglePlay_512.png", f"{SA}/GooglePlay/PLOP_Icon_512x512.png")
    os.makedirs(f"{SA}/AppStore", exist_ok=True)
    shutil.copyfile(BRAND + "/AppIcons/Hero_Pink/Master_iOS_1024.png", f"{SA}/AppStore/PLOP_Icon_1024x1024.png")


def poster_layers(W, H, kind):
    L = []
    if kind == "keyart":
        L += [scene("island_wide", 5.33, cx=560, cy=560, cz=1.0, dur=5.33)]
        lw = W * (0.72 if W != H else 0.56)
        L += [img(LOGO, [(W - lw) / 2, H * 0.05, lw, lw * 405 / 659], fit="contain"),
              txt(T("little_big"), W / 2, H * 0.05 + lw * 405 / 659 + W * 0.1, W * 0.062, "#FFFFFF", tracking=3, pill={"color": PLUM + "A8", "padX": 40, "padY": 22, "radius": 40}),
              txt(T("coming_soon"), W / 2, H * 0.92, W * 0.042, "#FFFFFF", tracking=4, pill={"color": BERRY_D, "padX": 36, "padY": 18, "radius": 36})]
    elif kind == "dreams":
        L += [scene("island_dusk", 6.0, cx=700, cy=520, cz=1.0, dur=8)]
        lw = W * (0.56 if W != H else 0.44)
        L += [txt(T("sweet_dreams"), W / 2, H * 0.12, W * 0.085, "#FFFFFF", tracking=3, textShadow=True),
              img(LOGO, [(W - lw) / 2, H - lw * 405 / 659 - H * 0.04, lw, lw * 405 / 659], fit="contain")]
    else:
        key, source, top, bottom, sprite = {"oneswipe": ("one_swipe", SHOTS[0][1], "#FFF1F5", "#FBD3E1", "Plop_Berry"),
                                           "challenges": ("challenges", SHOTS[3][1], "#F3F0FF", "#D9D2F5", "Plop_Honey"),
                                           "own": ("own", SHOTS[5][1], "#FFF0F4", "#F8D7E2", "Plop_Mint")}[kind]
        L += [fill(top, bottom)]
        head = H * (0.2 if W != H else 0.22); foot = H * 0.15
        if kind == "own":
            sw = W * 0.86; sh = sw * 830 / 900
            if sh > H - head - foot: sh = H - head - foot; sw = sh * 900 / 830
            L += [img(source[1], [(W - sw) / 2, head, sw, sh], CROP_STUDIO, radius=40, shadow=40)]
        else:
            ph = H - head - foot; pw = ph * CROP_BOARD[2] / CROP_BOARD[3]
            if pw > W * 0.86: pw = W * 0.86; ph = pw * CROP_BOARD[3] / CROP_BOARD[2]
            L += [vid(source[1], source[2], [(W - pw) / 2, head, pw, ph], CROP_BOARD, radius=40, shadow=40)]
        L += [spr(f"{SPR}/{sprite}.png", W * 0.86, head + 10, W * 0.17, -8, shadow=14),
              txt(T(key), W / 2, head * 0.52, W * 0.068, PLUM, tracking=3),
              img(LOGO, [(W - W * 0.22) / 2, H - foot / 2 - W * 0.22 * 405 / 659 / 2, W * 0.22, W * 0.22 * 405 / 659], fit="contain")]
    return L


def posters():
    for kind in ["keyart", "oneswipe", "challenges", "own", "dreams"]:
        for W, H, folder in [(1080, 1350, "Posters"), (1080, 1080, "Posters/Square")]:
            still(f"poster_{kind}_{W}x{H}", W, H, poster_layers(W, H, kind), f"{ROOT}/07_SocialMedia/{folder}/PLOP_Poster_{kind}_{W}x{H}.png")
        print("poster", kind)


def kit():
    SM = ROOT + "/07_SocialMedia"
    # avatars (circle-safe: all content inside the centre 80 %)
    still("avatar_mascot", 1080, 1080, [fill("#F59AB8", "#E86A92"), spr(f"{SPR}/Plop_Berry.png", 540, 560, 760, shadow=30)], f"{SM}/Avatars/PLOP_Avatar_Mascot_1080.png")
    still("avatar_logo", 1080, 1080, [fill(CREAM, "#FBD6E3"), img(LOGO, [140, 300, 800, 800 * 405 / 659], fit="contain")], f"{SM}/Avatars/PLOP_Avatar_Logo_1080.png")
    shutil.copyfile(BRAND + "/AppIcons/Hero_Pink/Master_iOS_1024.png", f"{SM}/Avatars/PLOP_Avatar_AppIcon_1024.png")
    # banners
    def banner(W, H, safe_w, safe_h, name):
        lw = min(safe_w * 0.42, safe_h * 1.45)
        L = [scene("island_wide", 5.33, cx=887, cy=443, cz=max(1.0, (W / H) / (1774 / 887)), dur=5.33),
             img(LOGO, [(W - lw) / 2, (H - lw * 405 / 659) / 2 - safe_h * 0.08, lw, lw * 405 / 659], fit="contain"),
             txt(T("little_big").replace("\n", " "), W / 2, H / 2 + safe_h * 0.36, safe_h * 0.11, "#FFFFFF", tracking=3, pill={"color": PLUM + "B0", "padX": 30, "padY": 14, "radius": 30})]
        still(name, W, H, L, f"{SM}/Banners/{name}.png")
    banner(2560, 1440, 1546, 423, "PLOP_Banner_YouTube_2560x1440")
    banner(1500, 500, 1300, 420, "PLOP_Banner_X_1500x500")
    banner(1640, 624, 1340, 520, "PLOP_Banner_Facebook_1640x624")
    # templates (editable timelines live next to the PNGs' timelines in Timelines/Stills)
    for W, H, n in [(1080, 1920, "Story"), (1080, 1350, "Post"), (1080, 1080, "Square")]:
        L = [fill(CREAM, "#FBD6E3"), dict(type="fill", start=0, end=9, color="#FFFFFF00"),
             img(LOGO, [(W - 300) / 2, H - 230, 300, 300 * 405 / 659], fit="contain"),
             txt("HEADLINE GOES HERE", W / 2, H * 0.12 + (120 if n == "Story" else 0), 64, PLUM, tracking=2),
             dict(type="image", src=SPR + "/Plop_Berry.png", start=0, end=9, size=[180, 180], anchor=[0.5, 0.5], kf=[{"t": 0, "x": W - 130, "y": H - 330, "rot": -8}])]
        still(f"template_{n}", W, H, L, f"{SM}/Templates/PLOP_Template_{n}_{W}x{H}.png")
    # palette + typography sheets
    pal = [("Cream", CREAM), ("Berry", BERRY), ("Berry Deep", BERRY_D), ("Mint", MINT), ("Lavender", LAV), ("Honey", HONEY), ("Plum (text)", PLUM)]
    L = [fill("#FFFFFF")]
    for i, (n, hx) in enumerate(pal):
        x = 60 + i * 260
        L += [dict(type="text", text=" ", start=0, end=9, x=x + 110, y=330, fontSize=10, pill={"color": hx, "padX": 110, "padY": 150, "radius": 30}),
              txt(n, x + 110, 560, 30, PLUM), txt(hx, x + 110, 605, 28, "#8A6B7A", font=MEDIUM)]
    L += [txt("PLOP! — PALETTE", 960, 90, 54, PLUM, tracking=3)]
    still("palette", 1920, 700, L, f"{BRAND}/Palette/PLOP_Palette_1920x700.png")
    json.dump({n: hx for n, hx in pal}, open(f"{BRAND}/Palette/palette.json", "w"), indent=1)
    open(f"{BRAND}/Palette/palette.css", "w").write(":root {\n" + "".join(f"  --plop-{n.split(' (')[0].lower().replace(' ', '-')}: {hx};\n" for n, hx in pal) + "}\n")
    L = [fill(CREAM), txt("Nunito ExtraBold", 80, 110, 46, BERRY_D, align="left"),
         txt("LITTLE PLOPS. BIG PUZZLES.", 80, 220, 96, PLUM, align="left", tracking=2),
         txt("Headlines, captions, buttons · tracking +2 %, line height 1.04", 80, 320, 34, "#8A6B7A", font=MEDIUM, align="left"),
         txt("Nunito Medium", 80, 470, 46, BERRY_D, align="left", font=HEAVY),
         txt("Body copy and sub-captions. Every Plop moves at once —", 80, 560, 46, PLUM, font=MEDIUM, align="left"),
         txt("think before you swipe.  ÀÉÎÕÜ ß ñ ç ¿¡", 80, 625, 46, PLUM, font=MEDIUM, align="left"),
         txt("SIL Open Font License 1.1 · bundled subset has no Cyrillic", 80, 760, 30, "#8A6B7A", font=MEDIUM, align="left")]
    still("typography", 1920, 860, L, f"{BRAND}/Palette/PLOP_Typography_1920x860.png")
    for f in FONTS + [TYPO + "/Nunito-OFL.txt"]:
        os.makedirs(BRAND + "/Typography", exist_ok=True); shutil.copyfile(f, f"{BRAND}/Typography/{os.path.basename(f)}")
    print("kit done")


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("screens", "all"): screens()
    if what in ("feature", "all"): feature()
    if what in ("posters", "all"): posters()
    if what in ("kit", "all"): kit()
