"""Set or clear PLOP development-build debug prefs on the Android emulator (debuggable build, via run-as).
Usage:
  debugprefs.py <level|0> [moves]      jump to a level (and replay moves); 0 clears both keys
  debugprefs.py playtest on|off        Phase 11 local playtest metrics (one CSV row per level visit)
  debugprefs.py pull-playtest out.csv  copy the metrics file from the device
  debugprefs.py diagnostics on|off     frame-time / GC / draw-call overlay
Progress keys are left untouched. Force-stops the app before editing prefs."""
import re, subprocess, sys
PKG = "com.plop.sleepypuzzle"
XML = "shared_prefs/%s.v2.playerprefs.xml" % PKG
CSV = "/sdcard/Android/data/%s/files/playtest_metrics.csv" % PKG
adb = ["adb"]


def edit(remove, extra):
    subprocess.run(adb + ["shell", "am", "force-stop", PKG], check=True)
    xml = subprocess.run(adb + ["shell", "run-as", PKG, "cat", XML], check=True, capture_output=True, text=True).stdout
    xml = re.sub(r'\s*<(int|string) name="plop\.debug\.(%s)"[^\n]*' % remove, "", xml)
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
elif len(sys.argv) >= 3 and sys.argv[1] == "pull-playtest":
    subprocess.run(adb + ["pull", CSV, sys.argv[2]], check=True)
else:
    level = int(sys.argv[1]); moves = sys.argv[2] if len(sys.argv) > 2 else ""
    extra = ""
    if level > 0: extra += '\n    <int name="plop.debug.level" value="%d" />' % level
    if moves: extra += '\n    <string name="plop.debug.moves">%s</string>' % moves
    edit("level|moves", extra)
    print("debug level=%d moves=%s" % (level, moves or "-"))
