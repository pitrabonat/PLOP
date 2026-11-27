#!/usr/bin/env python3
"""Sprint 03 QA: measures every export, checks it against its target spec, assigns a status, and writes
11_QA/EXPORT_MANIFEST.{json,tsv,md} plus contact sheets. Status vocabulary: FINAL / DRAFT / AWAITING OWNER APPROVAL / BLOCKED."""
import os, json, hashlib, subprocess, glob, re
ROOT = "/Volumes/PLOP_DEV/Marketing/FinalSprint03"
C = ROOT + "/10_ProductionSources/Compositor"
QA = ROOT + "/11_QA"
DOTNET = os.path.expanduser("~/.dotnet/dotnet")
AUDIO = ROOT + "/10_ProductionSources/TrailerAudio/bin/out/TrailerAudio.dll"
DELIVER = ["01_Brand", "02_Cinematic/Master", "02_Cinematic/Shots", "02_Cinematic/Storyboard", "04_Trailers", "05_ShortVideos", "06_StoreAssets",
           "07_SocialMedia", "08_Localization", "09_Audio/Trailer"]
PLATE_USERS = re.compile(r"OfficialTrailer|Cinematic_Shot|Storyboard|FeatureGraphic_A|FeatureGraphic_B|Poster_keyart|Poster_dreams|Banner_")

def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()

files = []
for d in DELIVER:
    for p in sorted(glob.glob(f"{ROOT}/{d}/**/*", recursive=True)):
        if os.path.isfile(p) and not os.path.basename(p).startswith("."): files.append(p)
media = [p for p in files if p.lower().endswith((".mp4", ".png", ".jpg"))]
info = {}
for i in range(0, len(media), 40):
    for r in json.loads(subprocess.run([f"{C}/mediainfo"] + media[i:i + 40], capture_output=True, text=True).stdout): info[r["path"]] = r

rows, issues = [], []
def expect(p, cond, msg):
    if not cond: issues.append(f"{os.path.relpath(p, ROOT)}: {msg}")
    return cond

for p in files:
    rel = os.path.relpath(p, ROOT); name = os.path.basename(p); m = info.get(p, {})
    status, note = "FINAL", ""
    if p.endswith(".mp4"):
        w, h, dur = m.get("width"), m.get("height"), m.get("duration", 0)
        expect(p, m.get("fps", 0) <= 30.01, f"fps {m.get('fps')}")
        expect(p, m.get("codec") == "H.264", "codec")
        if "Cinematic_Shot" not in name: expect(p, m.get("audioCh") == 2 and m.get("audioCodec") == "AAC" and m.get("audioHz") == 44100, "audio AAC 44.1k stereo")
        expect(p, m.get("blackRunS", 0) <= 1.0, f"black run {m.get('blackRunS')} s")
        mm = re.search(r"(\d{3,4})x(\d{3,4})", name)
        if mm: expect(p, (w, h) == (int(mm[1]), int(mm[2])), f"size {w}x{h}")
        if "AppPreview" in name:
            expect(p, (w, h) == (886, 1920), "App Preview must be 886x1920 portrait")
            expect(p, 15 <= dur <= 30, f"App Preview duration {dur}")
            expect(p, m.get("profile") == "High L4.0", f"profile {m.get('profile')}")
            expect(p, m.get("bytes", 0) <= 500e6, "≤500 MB")
            if not 9000 <= m.get("videoKbps", 0) <= 12500: note = f"video {m.get('videoKbps')} kbps (Apple lists 10–12 Mbps)"
        elif "Challenge" in name: expect(p, 10 <= dur <= 15, f"challenge duration {dur}")
        elif "Mascot" in name: expect(p, 6 <= dur <= 10, f"mascot duration {dur}")
        elif "VerticalTrailer" in name: expect(p, 20 <= dur <= 30, f"vertical duration {dur}")
        elif "OfficialTrailer" in name: expect(p, 30 <= dur <= 40, f"official duration {dur}")
        elif "GameplayTrailer" in name: expect(p, 25 <= dur <= 35, f"gameplay duration {dur}")
    elif p.endswith(".png"):
        w, h = m.get("width"), m.get("height")
        if "/Screenshots_" in p or "FeatureGraphic" in p or "PLOP_Icon_1024" in name:
            expect(p, not m.get("hasAlpha"), "has alpha (stores reject)")
        mm = re.search(r"(\d{3,4})x(\d{3,4})", name)
        if mm and "Raw" not in name: expect(p, (w, h) == (int(mm[1]), int(mm[2])), f"size {w}x{h}")
        if "FeatureGraphic" in name: expect(p, (w, h) == (1024, 500), "feature graphic 1024x500")
        if "PLOP_Icon_512" in name: expect(p, (w, h) == (512, 512) and os.path.getsize(p) <= 1024 * 1024, "Play icon 512, ≤1 MB")
        if "/Screenshots_" in p and "GooglePlay" in p: expect(p, max(w, h) / min(w, h) <= 2.3 and 320 <= min(w, h) and max(w, h) <= 3840, "Play ratio/size")
    # status
    if PLATE_USERS.search(name) or "02_Cinematic" in rel and "Storyboard" in rel:
        status, note = "AWAITING OWNER APPROVAL", (note + "; " if note else "") + "uses the owner's reference illustration (provenance unconfirmed)"
    if "/08_Localization/" in p and "/en/" not in p and not name.endswith("_en.json") and ".en." not in name:
        status, note = "DRAFT", "AI-drafted translation — native-speaker review required"
    if "Listing_" in name or "_en.md" in name or "listing_en.json" in name or "Launch_Announcements" in name:
        status, note = "AWAITING OWNER APPROVAL", "copy needs owner sign-off; nothing published or posted"
    if ("/Screenshots_" in p) and ("_06_own_" in name or "_07_daily_" in name):
        note = (note + "; " if note else "") + "app UI captured on the Android emulator (same Unity UI); re-capture on iOS before App Store upload if preferred"
    if "/Screenshots_" in p and "/en/" not in p:
        status, note = "DRAFT", "localised headline — native-speaker review required"
    rows.append({"path": rel, "bytes": os.path.getsize(p), "sha256": sha(p), "status": status, "note": note,
                 **{k: m[k] for k in ("width", "height", "duration", "fps", "profile", "videoKbps", "audioCh", "audioHz", "audioKbps", "blackRunS", "hasAlpha") if k in m}})

# loudness of the mixes
loud = []
for w in sorted(glob.glob(ROOT + "/09_Audio/Trailer/*.wav")):
    loud.append(subprocess.run([DOTNET, AUDIO, "loud", w], capture_output=True, text=True).stdout.strip())

# blocked items (not files)
blocked = [
    ("Russian and Ukrainian burned-in screenshot headlines", "BLOCKED", "Bundled Nunito subset has no Cyrillic. Captions, store text and SRT/VTT are delivered; rendering needs a Cyrillic-capable font (full Nunito, OFL) — download needs owner OK."),
    ("Store upload / publishing / posting", "BLOCKED", "Out of scope by rule: no accounts, no uploads, no posts."),
    ("Google Play promo video", "BLOCKED", "Play takes a YouTube URL; uploading to YouTube is an owner action. Use 04_Trailers/PLOP_GameplayTrailer_1920x1080_Clean.mp4."),
]
os.makedirs(QA, exist_ok=True)
json.dump({"files": rows, "issues": issues, "loudness": loud, "blocked": blocked}, open(f"{QA}/EXPORT_MANIFEST.json", "w"), indent=1, ensure_ascii=False)
keys = ["path", "status", "bytes", "width", "height", "duration", "fps", "profile", "videoKbps", "audioCh", "audioHz", "blackRunS", "hasAlpha", "sha256", "note"]
with open(f"{QA}/EXPORT_MANIFEST.tsv", "w") as f:
    f.write("\t".join(keys) + "\n")
    for r in rows: f.write("\t".join(str(r.get(k, "")) for k in keys) + "\n")
from collections import Counter
cnt = Counter(r["status"] for r in rows)
vids = [r for r in rows if r["path"].endswith(".mp4")]
md = ["# PLOP! Sprint 03 — export manifest", "", f"{len(rows)} files · " + " · ".join(f"{k}: {v}" for k, v in cnt.items()), "",
      "Full table with SHA-256: `EXPORT_MANIFEST.tsv` / `.json`. Generated by `10_ProductionSources/Scripts/qa.py`.", "",
      "## Videos", "", "| File | Status | Size | Length | fps | Profile | Video kbps | Audio | Longest black | Note |", "|---|---|---|---:|---:|---|---:|---|---:|---|"]
for r in vids:
    md.append(f"| `{r['path']}` | {r['status']} | {r.get('width')}×{r.get('height')} | {r.get('duration', 0):.2f} s | {r.get('fps', 0):.0f} | {r.get('profile', '')} | {r.get('videoKbps', '')} | "
              f"{'AAC ' + str(r.get('audioCh')) + 'ch ' + str(int(r.get('audioHz', 0))) + ' Hz' if r.get('audioCh') else 'none'} | {r.get('blackRunS', 0)} s | {r['note']} |")
md += ["", "## Audio loudness (integrated, BS.1770 gated)", ""] + [f"- {l}" for l in loud]
md += ["", "## Automatic checks", ""] + ([f"- ❌ {i}" for i in issues] or ["- ✅ every measured export matches its target size, length, frame rate, codec, audio format, alpha rule and black-frame limit."])
md += ["", "## Blocked", ""] + [f"- **{a}** — {s}: {n}" for a, s, n in blocked]
open(f"{QA}/EXPORT_MANIFEST.md", "w").write("\n".join(md) + "\n")
print(len(rows), "files", dict(cnt)); print("issues:", issues or "none"); print("\n".join(loud))
