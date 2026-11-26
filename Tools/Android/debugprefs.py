"""Set or clear PLOP development-build debug prefs on the Android emulator (debuggable build, via run-as).
Usage:
  debugprefs.py <level|0> [moves]      jump to a level (and replay moves); 0 clears both keys
  debugprefs.py playtest on|off        Phase 11 local playtest metrics (one CSV row per level visit)
  debugprefs.py pull-playtest out.csv  copy the metrics file from the device
  debugprefs.py diagnostics on|off     frame-time / GC / draw-call overlay
  debugprefs.py lab <n|0> [world] [moves]  Phase 11.5 Visual Lab board n (1-7) in a World, optional demo moves
  debugprefs.py world <id|->           show the jumped-to level dressed in another World (look only)
  debugprefs.py branding push          copy every splash/logo alternative from the Phase 13 pack to the app's files
  debugprefs.py splash <off|selected|splash-day|splash-night|-> [hold-seconds]  Phase 13 splash review (- = normal)
  debugprefs.py logo <file.png|text|->  main-menu logo review (- = the selected profile logo)
Progress keys are left untouched. Force-stops the app before editing prefs."""
import os, re, subprocess, sys
PKG = "com.plop.sleepypuzzle"
XML = "shared_prefs/%s.v2.playerprefs.xml" % PKG
CSV = "/sdcard/Android/data/%s/files/playtest_metrics.csv" % PKG
FILES = "/sdcard/Android/data/%s/files" % PKG
PACK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Documentation", "Branding",
                    "Phase13_Source", "PLOP_Phase13_BrandingPack")
adb = ["adb"]


def edit(remove, extra):
    subprocess.run(adb + ["shell", "am", "force-stop", PKG], check=True)
    xml = subprocess.run(adb + ["shell", "run-as", PKG, "cat", XML], check=True, capture_output=True, text=True).stdout
    xml = re.sub(r'\s*<(int|string|float) name="plop\.debug\.(%s)"[^\n]*' % remove, "", xml)
    xml = xml.replace("<map>", "<map>" + extra, 1)
    subprocess.run(adb + ["shell", "cat > /data/local/tmp/plop_prefs.xml"], input=xml, text=True, check=True)
    subprocess.run(adb + ["shell", "run-as", PKG, "cp", "/data/local/tmp/plop_prefs.xml", XML], check=True)
    subprocess.run(adb + ["shell", "rm", "/data/local/tmp/plop_prefs.xml"], check=True)


if len(sys.argv) >= 2 and sys.argv[1] == "playtest":
    on = len(sys.argv) > 2 and sys.argv[2] == "on"
    edit("playtest", '\n    <int name="plop.debug.playtest" value="1" />' if on else "")
    print("playtest metrics %s" % ("on" if on else "off"))
elif len(sys.argv) >= 2 and sys.argv[1] == "diagnostics":
    on = len(sys.argv) > 2 and sys.argv[2] == "on"
    edit("diagnostics", '\n    <int name="plop.debug.diagnostics" value="1" />' if on else "")
    print("diagnostics overlay %s" % ("on" if on else "off"))
elif len(sys.argv) >= 3 and sys.argv[1] == "lab":
    n = int(sys.argv[2]); world = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] != "-" else ""
    moves = sys.argv[4] if len(sys.argv) > 4 else ""
    extra = ""
    if n > 0: extra += '\n    <int name="plop.debug.lab" value="%d" />' % n
    if world: extra += '\n    <string name="plop.debug.world">%s</string>' % world
    if moves: extra += '\n    <string name="plop.debug.moves">%s</string>' % moves
    edit("lab|world|moves|level", extra)
    print("visual lab=%d world=%s moves=%s" % (n, world or "-", moves or "-"))
elif len(sys.argv) >= 3 and sys.argv[1] == "world":
    world = sys.argv[2] if sys.argv[2] != "-" else ""
    edit("world|lab", '\n    <string name="plop.debug.world">%s</string>' % world if world else "")
    print("world override=%s" % (world or "-"))
elif len(sys.argv) >= 3 and sys.argv[1] == "branding" and sys.argv[2] == "push":
    for variant in ("Day", "Night"):
        src = os.path.join(PACK, "04_Splash", "Layers_" + variant)
        dst = "%s/branding/splash-%s" % (FILES, variant.lower())
        subprocess.run(adb + ["shell", "mkdir", "-p", dst], check=True)
        for f in sorted(os.listdir(src)):
            subprocess.run(adb + ["push", "-q", os.path.join(src, f), dst + "/" + f], check=True)
    logos = os.path.join(PACK, "02_Logos")
    subprocess.run(adb + ["shell", "mkdir", "-p", FILES + "/branding/logos"], check=True)
    for f in sorted(os.listdir(logos)):
        subprocess.run(adb + ["push", "-q", os.path.join(logos, f), FILES + "/branding/logos/" + f], check=True)
    print("branding alternatives pushed to " + FILES + "/branding")
elif len(sys.argv) >= 3 and sys.argv[1] == "splash":
    mode = sys.argv[2] if sys.argv[2] != "-" else ""
    extra = '\n    <string name="plop.debug.splash">%s</string>' % mode if mode else ""
    if mode and len(sys.argv) > 3: extra += '\n    <float name="plop.debug.splash.hold" value="%s" />' % float(sys.argv[3])
    edit(r"splash|splash\.hold", extra)
    print("splash review=%s hold=%s" % (mode or "-", sys.argv[3] if len(sys.argv) > 3 else "-"))
elif len(sys.argv) >= 3 and sys.argv[1] == "logo":
    logo = sys.argv[2] if sys.argv[2] != "-" else ""
    edit("logo", '\n    <string name="plop.debug.logo">%s</string>' % logo if logo else "")
    print("menu logo review=%s" % (logo or "-"))
elif len(sys.argv) >= 3 and sys.argv[1] == "pull-playtest":
    subprocess.run(adb + ["pull", CSV, sys.argv[2]], check=True)
else:
    level = int(sys.argv[1]); moves = sys.argv[2] if len(sys.argv) > 2 else ""
    extra = ""
    if level > 0: extra += '\n    <int name="plop.debug.level" value="%d" />' % level
    if moves: extra += '\n    <string name="plop.debug.moves">%s</string>' % moves
    edit("level|moves|lab", extra)
    print("debug level=%d moves=%s" % (level, moves or "-"))
