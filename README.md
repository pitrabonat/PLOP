# PLOP! — Sleepy Puzzle

A cozy mobile puzzle game for **Android** and **iOS**, built with **Unity 6.3 LTS (6000.3.29f1)**.

Repository: https://github.com/pitrabonat/PLOP

> Phase 7 refines swipe recognition and jelly motion across five Worlds.
> It adds original tactile audio, master mute and clearer hints.
>
> [Phase 7 gameplay previews](Documentation/Previews/Phase7/README.md) · [Project state](PROJECT_STATE.md) ·
> [Architecture](ARCHITECTURE.md) · [Rules](Documentation/GAME_RULES.md) ·
> [Testing](Documentation/TESTING.md) · [Development log](DEVELOPMENT_LOG.md)

## Play

Open the PLOP project in Unity 6000.3.29f1 and choose **PLOP → Play Game**.
You can also open `Assets/Scenes/Gameplay.unity` and press Play. Focus the
Game tab, then drag across the board or use the arrow keys or WASD. **Z**
or **Backspace** undoes, **R** restarts, **H** requests a hint, and **N** or
**Enter** advances after completion. Open **Levels** to choose a World or
return to the campaign frontier with Resume. Use **Settings** to change
ambience, effects, master mute, haptics, reduced motion or frame rate. If a move leaves the
board without a route to completion, the recovery card offers Undo Move,
Restart Level, or Keep playing.

## Install the Android development APK

The Phase 7 development APK location is
`/Volumes/PLOP_DEV/Builds/Android/Phase7/PLOP-phase7-dev.apk`.
With an ADB-authorized Android phone connected through USB, run:

```bash
adb install -r /Volumes/PLOP_DEV/Builds/Android/Phase7/PLOP-phase7-dev.apk
```

Then tap **PLOP!** on the phone. On this Mac, ADB is at
`~/Library/Android/sdk/platform-tools/adb`.

## Quick start

1. Connect the SanDisk drive, then mount the dev volume:
   `plop-mount` (or double-click `SanDisk/PLOP_DEV/Mount PLOP_DEV.command`)
2. Open the project in Unity Hub → **Projects** → `PLOP`
   (path: `/Volumes/PLOP_DEV/Unity/Projects/PLOP`)
3. Edit code in VS Code: Unity → **Assets → Open C# Project**
4. Build: Unity menu **PLOP → Build → Android APK (Development)** or **iOS Xcode Project**

Before unplugging SanDisk: quit Unity/VS Code/Xcode, run `plop-eject`, then eject SanDisk in Finder.

## Layout

| Path | Purpose |
|---|---|
| `Assets/Art`, `Audio`, `Animations`, `Materials`, `Prefabs` | Content |
| `Assets/Scenes` | Scenes (`Gameplay.unity` is the build entry scene) |
| `Assets/Scripts/{Core,Solver,LevelGeneration,Unity,LevelLab}` | Rules, tools and presentation |
| `Assets/Scripts/Editor` | Editor-only tooling (build script) |
| `Assets/Levels` | Level data |
| `Assets/Tests` | Unity EditMode and PlayMode suites |
| `Documentation/` | Design & technical docs |

See [CLAUDE.md](CLAUDE.md) for conventions, storage paths and build commands.
