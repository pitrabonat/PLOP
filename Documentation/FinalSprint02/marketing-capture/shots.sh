#!/bin/zsh
# shots.sh — the 15 raw marketing stills from the running dev build on the emulator (1080×2400), per SHOT_LIST.md.
# Level shots use the dev preview jump (no save touched) and let the build replay moves itself.
export PATH=$PATH:~/Library/Android/sdk/platform-tools
P=/Volumes/PLOP_DEV/Unity/Projects/PLOP; PKG=com.plop.sleepypuzzle; dp() { python3 -I $P/Tools/Android/debugprefs.py "$@"; }
O=/Volumes/PLOP_DEV/Marketing/RawScreenshots; mkdir -p $O
BOOT=28
level() { # file level moves extra-wait
  dp $2 $3 >/dev/null
  adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
  sleep $(( BOOT + ${#3} / 2 + ${4:-0} ))
  adb exec-out screencap -p > $O/$1.png; echo "$1 $(stat -f%z $O/$1.png)"
}
level 01_CozyNest_L004 4 ""
level 02_FrostedPond_L013 13 RDD
level 03_BreezyMeadow_L025 25 LLU
level 04_EmberHollow_L046 46 RDR
level 05_PrismObservatory_L056 56 URR
level 06_DewdropGarden_L076 76 DRD
level 07_RainyWindow_L087 87 DLRD
level 08_TwilightAttic_L118 118 DDRU
level 09_MoonlitClouds_L144 144 UDLLL
level 11_PerfectCard_L004 4 DULULU 3
level 15_LavaRecovery_L035 35 R 2
# Hint arrow at the start of Level 13
dp 13 >/dev/null; adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1; sleep $BOOT
adb shell input tap 780 2136; sleep 4; adb exec-out screencap -p > $O/14_HintArrow_L013.png; echo 14
# Menu, World picker and Studio (normal launch; the save is only read)
dp 0 >/dev/null; adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1; sleep $BOOT
adb exec-out screencap -p > $O/10_MainMenu.png; echo 10
adb shell input tap 354 1914; sleep 3; adb exec-out screencap -p > $O/12_WorldPicker.png; echo 12
adb shell input keyevent KEYCODE_BACK; sleep 2
adb shell input tap 720 1914; sleep 3; adb exec-out screencap -p > $O/13_CharacterStudio.png; echo 13
adb shell input keyevent KEYCODE_BACK; sleep 1
adb shell am force-stop $PKG
