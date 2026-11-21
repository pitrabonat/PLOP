# PLOP! — Sleepy Puzzle

A cozy mobile puzzle game for **Android** and **iOS**, built with **Unity 6.3 LTS (6000.3.29f1)**.

Repository: https://github.com/pitrabonat/PLOP

> Phase 3 adds the verified 100-level campaign, saved progression, and hints that adapt to the current board.
>
> [Project state](PROJECT_STATE.md) · [Architecture](ARCHITECTURE.md) ·
> [Rules](Documentation/GAME_RULES.md) · [Generation](Documentation/LEVEL_GENERATION.md) ·
> [Testing](Documentation/TESTING.md) · [Development log](DEVELOPMENT_LOG.md)

## Play

Open `Assets/Scenes/Gameplay.unity` and press Play. Swipe, mouse drag, or use the
arrow keys or WASD to move all awake Plops. **Z** or **Backspace** undoes, **R**
restarts, **H** requests a hint, and **N** or **Enter** advances after completion.
Open **Levels** to replay unlocked puzzles and tap the FPS button to switch
between 30 and 60 FPS.

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
