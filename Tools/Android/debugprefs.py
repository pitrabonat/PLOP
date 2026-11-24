"""Set or clear PLOP development-build debug prefs on the Android emulator (debuggable build, via run-as).
Usage: debugprefs.py <level|0> [moves]   (0 clears both keys). Progress keys are left untouched."""
import re, subprocess, sys
PKG = "com.plop.sleepypuzzle"
XML = "shared_prefs/%s.v2.playerprefs.xml" % PKG
adb = ["adb"]
subprocess.run(adb + ["shell", "am", "force-stop", PKG], check=True)
xml = subprocess.run(adb + ["shell", "run-as", PKG, "cat", XML], check=True, capture_output=True, text=True).stdout
xml = re.sub(r'\s*<(int|string) name="plop\.debug\.(level|moves)"[^\n]*', "", xml)
level = int(sys.argv[1]); moves = sys.argv[2] if len(sys.argv) > 2 else ""
extra = ""
if level > 0: extra += '\n    <int name="plop.debug.level" value="%d" />' % level
if moves: extra += '\n    <string name="plop.debug.moves">%s</string>' % moves
xml = xml.replace("<map>", "<map>" + extra, 1)
subprocess.run(adb + ["shell", "cat > /data/local/tmp/plop_prefs.xml"], input=xml, text=True, check=True)
subprocess.run(adb + ["shell", "run-as", PKG, "cp", "/data/local/tmp/plop_prefs.xml", XML], check=True)
subprocess.run(adb + ["shell", "rm", "/data/local/tmp/plop_prefs.xml"], check=True)
print("debug level=%d moves=%s" % (level, moves or "-"))
