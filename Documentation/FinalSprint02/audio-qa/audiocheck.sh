#!/bin/zsh
# audiocheck.sh <out-dir>  — measure the running build's real audio output on the emulator (gRPC stream), not a listening test.
# 1) cold start → splash (sonic logo) + menu music; 2) dev preview jump into Level 13 (Frosted Pond) and swipe the solution.
export PATH=$PATH:~/Library/Android/sdk/platform-tools
S=${0:A:h}; O=$1; mkdir -p $O
P=/Volumes/PLOP_DEV/Unity/Projects/PLOP; PKG=com.plop.sleepypuzzle
python3 -I $P/Tools/Android/debugprefs.py 0 >/dev/null
adb shell am force-stop $PKG
python3 -I $S/emucapture.py 14 $O/01-coldstart-menu.wav &
sleep 0.5; adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
wait
adb exec-out screencap -p > $O/01-menu.png
python3 -I $P/Tools/Android/debugprefs.py 13 >/dev/null
python3 -I $S/emucapture.py 26 $O/02-level13-play.wav &
sleep 0.5; adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
sleep 10
for m in R D D L U R U; do
  case $m in U) adb shell input swipe 540 1500 540 1100 120;; D) adb shell input swipe 540 1100 540 1500 120;; L) adb shell input swipe 740 1300 340 1300 120;; R) adb shell input swipe 340 1300 740 1300 120;; esac
  sleep 1.4
done
wait
adb exec-out screencap -p > $O/02-level13-done.png
python3 -I $P/Tools/Android/debugprefs.py 0 >/dev/null
python3 -I $S/wavstat.py $O/*.wav
