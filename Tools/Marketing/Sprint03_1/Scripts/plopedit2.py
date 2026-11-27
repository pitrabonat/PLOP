#!/usr/bin/env python3
"""plopedit2 — Sprint 03.1 (V2) copy of the Sprint 03 edit helpers. Outputs go to FinalSprint03_1; raw gameplay takes,
cue sheets and brand files are read from FinalSprint03 (unchanged). Audio is rendered at 48 kHz.

Original notes:

An Edit collects compositor layers (for plopcomp), SFX cues and music tracks (for TrailerAudio mix), and captions
(for burned-in text and SRT/VTT caption files). Edit.render() writes:
  10_ProductionSources/Timelines/<name>.json      plopcomp timeline (editable)
  10_ProductionSources/Timelines/<name>.mix.json  TrailerAudio mix spec (editable)
  09_Audio/Trailer/Mix_<name>.wav (+ _stem_music / _stem_sfx)
  <outdir>/<name>.mp4  and  10_ProductionSources/Subtitles/<name>.<lang>.srt/.vtt for every language in copy.json
All caption text comes from 10_ProductionSources/Scripts/copy.json so it can be edited and localised without code changes.
Gameplay footage is real: iOS Simulator recordings of the dev build replaying verified solutions. SFX cues are the
game's own events for those moves (LevelProbe --cues), placed at the matching footage time.
"""
import csv, json, os, subprocess, sys

ROOT = "/Volumes/PLOP_DEV/Marketing/FinalSprint03"
V2 = "/Volumes/PLOP_DEV/Marketing/FinalSprint03_1"
REC = ROOT + "/03_RawGameplay/Recordings"
META = ROOT + "/03_RawGameplay/Metadata"
PROD = V2 + "/08_ProductionSources"
BRAND = ROOT + "/01_Brand"
SFX = "/Volumes/PLOP_DEV/Marketing/Audio/SFX"
TYPO = "/Volumes/PLOP_DEV/Unity/Projects/PLOP/Assets/Art/Resources/Typography"
FONTS = [TYPO + "/Nunito-Display.ttf", TYPO + "/Nunito-Text.ttf"]
HEAVY, MEDIUM = "Nunito-ExtraBold", "Nunito-Medium"
DOTNET = os.path.expanduser("~/.dotnet/dotnet")
PLOPCOMP = PROD + "/Compositor/plopcomp"
AUDIO = PROD + "/TrailerAudio/bin/out/TrailerAudio.dll"

# Palette (brand kit)
CREAM, BERRY, BERRY_D, MINT, LAV, HONEY, PLUM = "#FFF6EE", "#E86A92", "#D9517F", "#8FD9C0", "#B9A3E8", "#F6C66B", "#5B3A4A"

BAR = 60 / 90 * 4  # 90 BPM bar = 2.6667 s

TAKES = {r["take"]: r for r in csv.DictReader(open(META + "/takes.tsv"), delimiter="\t")}
COPY = json.load(open(ROOT + "/10_ProductionSources/Scripts/copy.json"))
LANGS = [k for k in COPY if not k.startswith("_")]

def src(take):
    if take.startswith("A_"): return f"{REC}/{take}.mp4"
    return f"{REC}/{take}.mp4"

def first_move(take):
    r = TAKES[take]; dots = len(r["moves"]) - len(r["moves"].lstrip("."))
    return float(r["first_command_s"]) + 0.45 * dots

def cues(take):
    p = f"{META}/cues/{take}.tsv"
    if not os.path.exists(p): return []
    return [(float(r["t_source"]), r["cue"]) for r in csv.DictReader(open(p), delimiter="\t")]

def T(key, lang="en"):
    return COPY[lang].get(key) or COPY["en"][key]

# iOS frame geometry (1290×2796): HUD title + moves pill + board for every board size seen in the takes.
IOS_W, IOS_H = 1290, 2796
CROP_BOARD = [0, 440, 1290, 1710]        # HUD + board
CROP_BOARD_TIGHT = [20, 760, 1250, 1400]  # board only
# Android emulator Studio (1080×2400): preview room + item name.
CROP_STUDIO = [90, 220, 900, 830]

SFX_GAIN = {"move_1": -7, "move_2": -7, "move_3": -7, "move_4": -7, "ice_slide": -9, "ice_stop": -10, "fan": -9,
            "portal_in": -8, "portal_out": -8, "collision_1": -10, "collision_2": -10, "settle": -8, "sleep": -6,
            "lava_sizzle": -6, "deadlock": -7, "perfect": -3, "complete": -3, "dust": -9, "blocked_1": -9,
            "blocked_2": -9, "sonic_logo": -1, "ui_tap": -10, "cosmetic_unlock": -8, "world_unlock": -6}


class Edit:
    def __init__(self, name, W, H, duration, outdir, fps=30, profile=None, bitrate=None, background=CREAM):
        self.name, self.W, self.H, self.dur, self.outdir = name, W, H, duration, outdir
        self.fps, self.profile, self.bitrate, self.background = fps, profile, bitrate, background
        self.layers, self.cues, self.tracks, self.ducks, self.captions, self.overlays = [], [], [], [], [], []
        self.target_lufs = -16.0
        self.poster = None

    # ---------- picture ----------
    def add(self, **L):
        self.layers.append({k: v for k, v in L.items() if v is not None}); return self

    def fill(self, at, dur, color=None, top=None, bottom=None, **kw):
        return self.add(type="fill", start=at, end=at + dur, color=color, top=top, bottom=bottom, **kw)

    def image(self, path, at, dur, frame=None, fit="contain", **kw):
        return self.add(type="image", src=path, start=at, end=at + dur, frame=frame, fit=fit, **kw)

    def sprite(self, path, at, dur, kf, size=None, anchor=None, **kw):
        return self.add(type="image", src=path, start=at, end=at + dur, kf=kf, size=size, anchor=anchor, **kw)

    def video(self, take, at, dur, src_in, frame=None, crop=None, fit="cover", sfx=True, sfx_skip=(), sfx_gain=0.0,
              hold=None, rate=None, **kw):
        self.add(type="video", src=src(take), start=at, end=at + dur, **{"in": src_in}, frame=frame or [0, 0, self.W, self.H],
                 crop=crop, fit=fit, hold=hold, rate=rate, **kw)
        if sfx and hold is None:
            r = rate or 1.0
            for t, c in cues(take):
                o = at + (t - src_in) / r
                if at - 1e-3 <= o < at + dur - 0.05 and c not in sfx_skip:
                    self.cue(c, o, SFX_GAIN.get(c, -8) + sfx_gain)
        return self

    def backdrop(self, take, at, dur, src_in, crop=None, blur=34, tint=CREAM, tintAmount=0.42, **kw):
        """Soft blurred copy of the same footage filling the frame behind the panel."""
        return self.video(take, at, dur, src_in, crop=crop or [0, 300, IOS_W, 2200], fit="cover", blur=blur, tint=tint,
                          tintAmount=tintAmount, sfx=False, **kw)

    def panel(self, take, at, dur, src_in, frame, crop=None, radius=40, shadow=46, **kw):
        return self.video(take, at, dur, src_in, frame=frame, crop=crop or CROP_BOARD, fit="cover", radius=radius,
                          shadow=shadow, **kw)

    def text(self, s, at, dur, x, y, size, font=HEAVY, color=PLUM, fadeIn=0.25, fadeOut=0.25, rise=24, **kw):
        return self.add(type="text", text=s, start=at, end=at + dur, x=x, y=y, fontSize=size, font=font, color=color,
                        fadeIn=fadeIn, fadeOut=fadeOut, rise=rise, **kw)

    def caption(self, key, at, dur, x, y, size, burn=True, pill=None, **kw):
        """Caption from copy.json. Burned in (English) and recorded for the per-language caption files."""
        self.captions.append((at, at + dur, key))
        if burn:
            self.text(T(key), at, dur, x, y, size, tracking=size * 0.02, lineHeight=1.04,
                      pill=pill if pill is not None else {"color": CREAM + "F2", "padX": size * 0.6, "padY": size * 0.42, "radius": size * 0.55}, **kw)
            self.overlays.append(self.layers.pop())   # captions always draw above every footage layer
        return self

    # ---------- sound ----------
    def cue(self, cid, at, gainDb=-8, pan=0.0):
        if os.path.exists(f"{SFX}/PLOP_Sfx_{cid}.wav"): self.cues.append({"id": cid, "at": round(at, 3), "gainDb": gainDb, "pan": pan})
        return self

    def music(self, wav, at=0.0, src_in=0.0, length=None, gainDb=0.0, fadeIn=0.0, fadeOut=1.2):
        self.tracks.append({"file": wav, "at": at, "in": src_in, "len": length, "gainDb": gainDb, "fadeIn": fadeIn,
                            "fadeOut": fadeOut, "stem": "music"}); return self

    def duck(self, a, b, db=-5):
        self.ducks.append({"from": a, "to": b, "db": db}); return self

    # ---------- output ----------
    def write_captions(self):
        os.makedirs(PROD + "/Subtitles", exist_ok=True)
        def ts(t, sep): h, r = divmod(t, 3600); m, s = divmod(r, 60); return f"{int(h):02d}:{int(m):02d}:{int(s):02d}{sep}{int(round((s % 1) * 1000)) % 1000:03d}"
        caps = sorted(self.captions)
        literal = any(k not in COPY["en"] for _, _, k in caps)
        for lang in (["en"] if literal else LANGS):   # V2 headlines are literal English text (no localisation yet)
            srt, vtt = [], ["WEBVTT", ""]
            for i, (a, b, k) in enumerate(caps, 1):
                txt = T(k, lang) if k in COPY["en"] else k
                srt += [str(i), f"{ts(a, ',')} --> {ts(b, ',')}", txt, ""]
                vtt += [f"{ts(a, '.')} --> {ts(b, '.')}", txt, ""]
            if caps:
                open(f"{PROD}/Subtitles/{self.name}.{lang}.srt", "w").write("\n".join(srt))
                open(f"{PROD}/Subtitles/{self.name}.{lang}.vtt", "w").write("\n".join(vtt))

    def render(self, audio=True, video=True):
        os.makedirs(PROD + "/Timelines", exist_ok=True); os.makedirs(self.outdir, exist_ok=True)
        wav = None
        if audio and (self.tracks or self.cues):
            stems = V2 + "/05_Audio/Stems"; os.makedirs(stems, exist_ok=True)
            mix = {"duration": self.dur, "sfxDir": SFX, "tracks": self.tracks, "cues": sorted(self.cues, key=lambda c: c["at"]),
                   "ducks": self.ducks, "targetLufs": self.target_lufs, "ceilingDb": -1.0, "stemsDir": stems}
            mp = f"{PROD}/Timelines/{self.name}.mix.json"; json.dump(mix, open(mp, "w"), indent=1)
            wav = f"{V2}/05_Audio/Mix_{self.name}.wav"
            print(subprocess.run([DOTNET, AUDIO, "mix", mp, wav], capture_output=True, text=True).stdout.strip())
        tl = {"width": self.W, "height": self.H, "fps": self.fps, "duration": self.dur,
              "output": f"{self.outdir}/{self.name}.mp4", "background": self.background, "fonts": FONTS, "audioRate": 48000, "layers": self.layers + self.overlays}
        if wav: tl["audio"] = wav
        if self.profile: tl["profile"] = self.profile
        if self.bitrate: tl["bitrate"] = self.bitrate
        if self.poster is not None: tl["poster"] = self.poster; tl["posterOut"] = f"{self.outdir}/{self.name}_poster.png"
        tp = f"{PROD}/Timelines/{self.name}.json"; json.dump(tl, open(tp, "w"), indent=1)
        self.write_captions()
        if video:
            r = subprocess.run([PLOPCOMP, tp], capture_output=True, text=True)
            print(r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr[-800:])
        return tp

    def still(self, t, out):
        tp = f"{PROD}/Timelines/{self.name}.json"
        subprocess.run([PLOPCOMP, tp, "--still", str(t), out], capture_output=True, text=True)
        return out


def score(spec, out):
    sp = f"{PROD}/TrailerAudio/specs/{spec}.json"
    print(subprocess.run([DOTNET, AUDIO, "score", sp, out], capture_output=True, text=True).stdout.strip())
    return out


def still(name, W, H, layers, out, background=CREAM, t=0.0):
    """Render a single graphic (opaque RGB PNG) through plopcomp from a list of layer dicts. Timeline kept for editing."""
    os.makedirs(PROD + "/Timelines/Stills", exist_ok=True); os.makedirs(os.path.dirname(out), exist_ok=True)
    tl = {"width": W, "height": H, "fps": 30, "duration": 1.0, "output": out.replace(".png", ".mp4"), "background": background,
          "fonts": FONTS, "layers": [{k: v for k, v in L.items() if v is not None} for L in layers]}
    tp = f"{PROD}/Timelines/Stills/{name}.json"; json.dump(tl, open(tp, "w"), indent=1)
    r = subprocess.run([PLOPCOMP, tp, "--still", str(t), out], capture_output=True, text=True)
    if r.returncode != 0: print("FAILED", name, r.stderr[-400:])
    return out
