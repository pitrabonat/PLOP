#!/usr/bin/env python3
"""Three YouTube thumbnail options (1920×1080 master → 1280×720 JPG). A hero mascot · B puzzle adventure · C gameplay first.
Every frame comes from the V2 trailer sources (rig, layered island, real gameplay)."""
import sys, os, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from plopedit2 import *
OUT = V2 + "/03_Thumbnails"; os.makedirs(OUT, exist_ok=True)
LOGO = BRAND + "/Logos/01_Jelly_Pink.png"
def logo(x, y, s, at=0): return dict(type="image", src=LOGO, start=0, end=9, size=[659 * s, 405 * s], anchor=[0.5, 0.5], kf=[{"t": 0, "x": x, "y": y, "s": 1}], shadow=26)
def txt(s, x, y, size, color=PLUM, align="center", outline=None): return dict(type="text", text=s, start=0, end=9, x=x, y=y, fontSize=size, font=HEAVY, color=color, align=align, tracking=size * 0.03, lineHeight=1.02, outline=outline, outlineWidth=size * 0.16 if outline else None)
def jpg(png, name):
    out = f"{OUT}/{name}.jpg"
    subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "92", "-z", "720", "1280", png, "--out", out], capture_output=True)
    return out
A = still("Thumb_A", 1920, 1080, [
    dict(type="scene2", scene="v2_thumb", start=0, end=9, params={"kind": 0}),
    logo(1420, 300, 1.3)], OUT + "/master/PLOP_Thumbnail_A_1920x1080.png")
B = still("Thumb_B", 1920, 1080, [
    dict(type="scene2", scene="v2_adventure", start=0, end=9, sceneOffset=1.55),
    logo(420, 220, 0.95), txt("LITTLE PLOPS.\nBIG PUZZLES.", 420, 540, 84, color="#FFFFFF", outline=PLUM)], OUT + "/master/PLOP_Thumbnail_B_1920x1080.png")
C = still("Thumb_C", 1920, 1080, [
    dict(type="fill", start=0, end=9, top="#F6EEFF", bottom="#FFE8F0"),
    dict(type="video", src=src("G_L057_Prism_PortalLava"), start=0, end=9, hold=11.6, frame=[110, 40, 904, 1000], crop=[20, 740, 1250, 1360], fit="cover", radius=34, shadow=38),
    dict(type="scene2", scene="v2_thumb", start=0, end=9, params={"kind": 2, "x": 1480, "floor": 120, "u": 560}),
    logo(1480, 175, 0.62), txt("EVERY PLOP\nMOVES AT ONCE", 1480, 430, 80, color=PLUM)], OUT + "/master/PLOP_Thumbnail_C_1920x1080.png")
for p, n in ((A, "PLOP_Thumbnail_A"), (B, "PLOP_Thumbnail_B"), (C, "PLOP_Thumbnail_C")): print(jpg(p, n))
