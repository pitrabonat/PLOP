# PLOP! — Sleepy Puzzle

A cozy mobile puzzle game for **Android** and **iOS**, built with **Unity 6.3 LTS (6000.3.29f1)**.

Repository: https://github.com/pitrabonat/PLOP

> Status: clean project scaffold. No gameplay implemented yet.

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
| `Assets/Scenes` | Scenes (`Boot.unity` is the build entry scene) |
| `Assets/Scripts/{Core,Gameplay,LevelGeneration,Solver,UI,SaveSystem}` | Runtime code (empty) |
| `Assets/Scripts/Editor` | Editor-only tooling (build script) |
| `Assets/Levels` | Level data |
| `Tests/` | Test plans/notes (Unity test assemblies go under `Assets/Tests` when added) |
| `Documentation/` | Design & technical docs |

See [CLAUDE.md](CLAUDE.md) for conventions, storage paths and build commands.
