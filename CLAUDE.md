# CLAUDE.md — PLOP! (Sleepy Puzzle)

Guidance for Claude Code and other contributors working in this repository.

## Project
- **Name:** PLOP! — Sleepy Puzzle (Unity project folder: `PLOP`)
- **Engine:** Unity **6000.3.29f1** (Unity 6.3 LTS), Apple Silicon (arm64) editor
- **Render pipeline:** URP with 2D Renderer (from the Universal 2D template)
- **Language:** C# (Unity default API compatibility, .NET Standard 2.1)
- **Targets:** Android (IL2CPP, ARM64, min API 26, target = highest installed) and iOS (min iOS 15.0)
- **Orientation:** Portrait only
- **Repository:** https://github.com/pitrabonat/PLOP (remote `origin`, branch `main`; public)
- **Status:** Scaffold only. No gameplay, art, ads or analytics yet. Do not add ad/analytics SDKs without explicit approval.

## Storage layout (important)
The SanDisk drive is **exFAT**, which is unsuitable for Unity (no symlinks/permissions, 128 KB clusters).
All dev data therefore lives inside an **APFS sparse-bundle image stored on SanDisk**:

| What | Path |
|---|---|
| Image file (on exFAT SanDisk) | `/Volumes/SanDisk/PLOP_DEV/PLOP_DEV.sparsebundle` |
| Mounted dev volume (APFS) | `/Volumes/PLOP_DEV` |
| This project | `/Volumes/PLOP_DEV/Unity/Projects/PLOP` |
| Unity Editor | `/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app` |
| Android SDK / NDK r27c / OpenJDK 17 (Unity-bundled) | `/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/PlaybackEngines/AndroidPlayer/{SDK,NDK,OpenJDK}` |
| Build outputs | `/Volumes/PLOP_DEV/Builds/{Android,iOS}` |
| Unity package (UPM) cache | `/Volumes/PLOP_DEV/Cache/UPM` (set in `~/.upmconfig.toml`) |
| Xcode (when installed) | `/Volumes/PLOP_DEV/Xcode/Xcode.app` |
| Raw source assets / docs / backups | `/Volumes/PLOP_DEV/{Assets,Documentation,Backups}` |

Rules:
- **Mount before working:** `plop-mount` (or double-click `SanDisk/PLOP_DEV/Mount PLOP_DEV.command`).
- **Eject before unplugging:** quit Unity/VS Code/Xcode → `plop-eject` → eject SanDisk in Finder. Yanking the drive while mounted risks corrupting the image.
- The drive is a USB flash stick on a 480 Mb/s link: first launches/imports are slow; warm launches are fine.
- Never write project files directly on the exFAT volume.
- Internal SSD is tight: keep large caches and outputs on `/Volumes/PLOP_DEV`.

## Directory architecture
```
Assets/
  Art/  Audio/  Animations/  Materials/  Prefabs/
  Scenes/            Boot.unity = build entry scene
  Scripts/
    Core/            bootstrapping, services, shared utilities
    Gameplay/        board, pieces, rules (runtime)
    LevelGeneration/ procedural level creation
    Solver/          puzzle solver / validation
    UI/              menus, HUD
    SaveSystem/      persistence
    Editor/          editor-only tools (PlopBuild.cs) — never referenced by runtime code
  Levels/            level data assets
  Settings/          URP / 2D renderer / Input System assets (from template)
Tests/               test plans & notes (put Unity test assemblies in Assets/Tests/{EditMode,PlayMode})
Documentation/       design & technical docs
```

## Conventions
- Namespaces: `Plop.<Area>` (e.g. `Plop.Core`, `Plop.Solver`); editor code in `Plop.EditorTools`.
- One public type per file; file name = type name. PascalCase types/methods, `_camelCase` private fields, `[SerializeField] private` over public fields.
- Keep game logic (Solver, LevelGeneration) free of `MonoBehaviour` where possible so it is unit-testable.
- Use the Input System package (already installed), not the legacy Input Manager.
- Add an assembly definition (`.asmdef`) per Scripts area once code exists.
- Always commit `.meta` files; never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds.
- Binary assets (png, psd, wav, fbx, …) go through **Git LFS** (see `.gitattributes`).
- Minimal dependencies: ask before adding packages. Removed from template on purpose: Collab, Rider, Visual Scripting, Timeline, SpriteShape, Tilemap Extras, Aseprite, PSD Importer, 2D Animation, 2D Tooling.

## Tooling
- IDE: VS Code + "Unity" extension (`visualstudiotoolsforunity.vstuc`, pulls in C# Dev Kit). .NET SDK 10 at `~/.dotnet`.
  In Unity: Preferences → External Tools → External Script Editor = Visual Studio Code.
- Git + Git LFS (`git lfs install` done for the user).
- ADB: `~/Library/Android/sdk/platform-tools/adb` (same version, 37.0.1, as Unity's bundled one).

## Build instructions
GUI: Unity menu **PLOP → Build → Android APK (Development)** / **iOS Xcode Project**.

CLI (quit any open Editor on this project first):
```bash
UNITY=/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity
PROJ=/Volumes/PLOP_DEV/Unity/Projects/PLOP
# Android dev APK -> /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget Android -executeMethod Plop.EditorTools.PlopBuild.BuildAndroid -logFile -
# Install & launch on a connected phone
adb install -r /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
adb shell monkey -p com.plop.sleepypuzzle -c android.intent.category.LAUNCHER 1
# iOS Xcode project (Simulator SDK by default; PLOP_IOS_SDK=device for device SDK)
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget iOS -executeMethod Plop.EditorTools.PlopBuild.ExportIOS -logFile -
# Build the exported project for the simulator (no signing needed)
DEVELOPER_DIR=/Volumes/PLOP_DEV/Xcode/Xcode.app/Contents/Developer \
  xcodebuild -project /Volumes/PLOP_DEV/Builds/iOS/PLOP-Xcode/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -sdk iphonesimulator -configuration Debug \
  -derivedDataPath /Volumes/PLOP_DEV/Builds/iOS/DerivedData build
```
Bundle id placeholder: `com.plop.sleepypuzzle` — change before publishing.

## Known constraints
- macOS 15.5 supports Xcode up to 16.4 (iOS 18.5 SDK). App Store uploads require Xcode 26+, which needs macOS Tahoe 26.2+. Upgrade macOS before shipping to the App Store.
- Xcode 16.4 is installed at `/Volumes/PLOP_DEV/Xcode/Xcode.app` (license accepted). Use it via `DEVELOPER_DIR` (no `sudo xcode-select`).
- The test iPhone (iPhone 15 Pro Max) runs **iOS 26.6.1**; Xcode 16.4 cannot install/debug on iOS 26 ("no DDI"). Real-iPhone testing needs macOS Tahoe 26.2+ and Xcode 26.x.
- Xcode builds that compile storyboards (Unity's LaunchScreen) require the iOS platform component (`xcodebuild -downloadPlatform iOS`), which also installs a simulator runtime (~8 GB on the **internal** SSD). Not installed yet to save internal space. Without it, all code (IL2CPP + UnityFramework) compiles; only the LaunchScreen storyboards fail.
- Xiaomi/HyperOS test phone blocks `adb install` unless Developer options → "Install via USB" is enabled; fallback: `adb push` the APK to `/sdcard/Download` and install from Files.
- Physical iPhone testing requires an Apple ID in Xcode → Settings → Accounts (free Personal Team works).
