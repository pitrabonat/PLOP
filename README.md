# PLOP! — Sleepy Puzzle

A cozy mobile puzzle game for **Android** and **iOS**, built with **Unity 6.3 LTS (6000.3.29f1)**.

Repository: https://github.com/pitrabonat/PLOP

> Status: gameplay foundation — deterministic simulation, solver, procedural generator, Level Lab,
> placeholder prototype scene (`Assets/Scenes/Gameplay.unity`) and a 100-level pack. Placeholder graphics only.
>
> Docs: [Architecture](Documentation/ARCHITECTURE.md) · [Rules](Documentation/GAME_RULES.md) ·
> [Solver](Documentation/SOLVER.md) · [Generation](Documentation/LEVEL_GENERATION.md) ·
> [Level format](Documentation/LEVEL_FORMAT.md) · [Testing](Documentation/TESTING.md) ·
> [Dev log](Documentation/DEVELOPMENT_LOG.md)

## Play the prototype

Open `Assets/Scenes/Gameplay.unity` and press Play. Controls: swipe / mouse-drag or arrow keys & WASD to move
all awake Plops; Z or Backspace = Undo; R = Restart; N or Enter = Next level (also on-screen buttons).

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
