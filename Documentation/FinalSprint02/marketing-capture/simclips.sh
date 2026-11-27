#!/bin/zsh
# simclips.sh — raw gameplay clips on the iOS Simulator (dev build): for each World's shot-list level, jump there via the
# development debug prefs, let the build replay the verified solution itself (animated, 0.45 s per move), and record.
# Real game output only. The simctl recordings carry no audio track; the sound for edits is in Marketing/Audio.
export DEVELOPER_DIR=/Volumes/PLOP_DEV/Xcode/Xcode-26.3.app/Contents/Developer
SIM=0543BA99-F261-48F3-BF4A-F84A717AE739; APP=com.plop.sleepypuzzle
O=/Volumes/PLOP_DEV/Marketing/RawGameplay; mkdir -p $O
clip() { # name level moves seconds
  xcrun simctl terminate $SIM $APP 2>/dev/null
  xcrun simctl spawn $SIM defaults write $APP plop.debug.level -int $2
  xcrun simctl spawn $SIM defaults write $APP plop.debug.moves -string "$3"
  # Record from launch (splash included; trim in the edit). A warm launch reaches the board in ~5 s, so a fixed
  # pre-roll misses the solve. Length = start/splash + the replay's 1.5 s lead-in + 0.45 s per move + the Perfect card.
  xcrun simctl io $SIM recordVideo --codec=h264 --force $O/$1.mp4 &
  local rec=$!; sleep 1
  xcrun simctl launch $SIM $APP >/dev/null
  sleep $(( 16 + ${#3} / 2 + 5 )); kill -INT $rec   # the sim takes 10–14 s to reach the board; wait $rec 2>/dev/null
  echo "$1 $(stat -f%z $O/$1.mp4) bytes"
}
clip 01_CozyNest_L004 4 DULULU 8
clip 02_FrostedPond_L013 13 RDDLURU 9
clip 03_BreezyMeadow_L025 25 LLURRUR 9
clip 04_EmberHollow_L046 46 RDRLUUR 9
clip 05_PrismObservatory_L056 56 URRRUL 8
clip 06_DewdropGarden_L076 76 DRDLUDR 9
clip 07_RainyWindow_L087 87 DLRDDDUR 10
clip 08_TwilightAttic_L118 118 DDRUDDLDL 10
clip 09_MoonlitClouds_L144 144 UDLLLUUDLUL 11
xcrun simctl terminate $SIM $APP
xcrun simctl spawn $SIM defaults delete $APP plop.debug.level
xcrun simctl spawn $SIM defaults delete $APP plop.debug.moves
