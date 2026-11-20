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
| Xcode 26.3 | `/Volumes/PLOP_DEV/Xcode/Xcode-26.3.app` (used via `DEVELOPER_DIR`) |
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
GUI: Unity menu **PLOP → Build → Android APK (Development)** / **iOS Xcode Project (Device)** / **iOS Xcode Project (Simulator)**.

CLI (quit any open Editor on this project first):
```bash
UNITY=/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity
PROJ=/Volumes/PLOP_DEV/Unity/Projects/PLOP
# Android dev APK -> /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget Android -executeMethod Plop.EditorTools.PlopBuild.BuildAndroid -logFile -
# Install & launch on a connected phone
adb install -r /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
adb shell monkey -p com.plop.sleepypuzzle -c android.intent.category.LAUNCHER 1
export DEVELOPER_DIR=/Volumes/PLOP_DEV/Xcode/Xcode-26.3.app/Contents/Developer
# iOS Simulator: export (arm64 simulator) -> build -> run in "PLOP iPhone 15 Pro Max" simulator
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget iOS -executeMethod Plop.EditorTools.PlopBuild.ExportIOSSimulator -logFile -
xcodebuild -project /Volumes/PLOP_DEV/Builds/iOS/PLOP-Xcode-Sim/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' \
  -derivedDataPath /Volumes/PLOP_DEV/Builds/iOS/DerivedData-Sim CODE_SIGNING_ALLOWED=NO build
SIM=$(xcrun simctl list devices | grep "PLOP iPhone 15 Pro Max" | grep -o '[0-9A-F-]\{36\}' | head -1)
xcrun simctl boot "$SIM" 2>/dev/null; open -a "$DEVELOPER_DIR/Applications/Simulator.app"
xcrun simctl install "$SIM" /Volumes/PLOP_DEV/Builds/iOS/DerivedData-Sim/Build/Products/Debug-iphonesimulator/PLOP.app
xcrun simctl launch "$SIM" com.plop.sleepypuzzle
# iOS device (signed with the free Personal Team 78P9NBFG37; Apple ID must be in Xcode → Settings → Accounts)
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget iOS -executeMethod Plop.EditorTools.PlopBuild.ExportIOS -logFile -
xcodebuild -project /Volumes/PLOP_DEV/Builds/iOS/PLOP-Xcode/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug \
  -destination "id=<iPhone hardware UDID>" -derivedDataPath /Volumes/PLOP_DEV/Builds/iOS/DerivedData \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration DEVELOPMENT_TEAM=78P9NBFG37 CODE_SIGN_STYLE=Automatic build
xcrun devicectl device install app --device <CoreDevice id> /Volumes/PLOP_DEV/Builds/iOS/DerivedData/Build/Products/Debug-iphoneos/PLOP.app
```
Bundle id placeholder: `com.plop.sleepypuzzle` — change before publishing.

## Known constraints
- macOS **Sequoia 15.8.3** + **Xcode 26.3** (iOS 26.2 SDK). Xcode 26.3 is the newest Xcode that runs on Sequoia (26.4+ needs Tahoe 26.2+). It is new enough for App Store uploads (Xcode 26+ required since April 2026).
- iOS platform component installed with `xcodebuild -downloadPlatform iOS -architectureVariant arm64` (simulator runtime iOS 26.3.1, ~7.8 GB on the **internal** SSD — unavoidable; Xcode needs it even to compile LaunchScreen storyboards).
- Unity's iOS Simulator export defaults to x86_64; `ExportIOSSimulator` forces arm64 (required on this Apple-silicon Mac with the arm64-only runtime).
- The test iPhone 15 Pro Max (iOS 26.6.1) builds, signs and installs fine, but **won't launch dev builds**: a device-management (MDM) profile on the phone blocks trusting developer apps ("Your device management settings do not allow using apps from developer…"). Use the iOS Simulator, remove the profile (only if not needed), or use TestFlight (paid Apple Developer Program).
- Free Personal Team profiles expire after 7 days — rebuild/reinstall to refresh.
- Xiaomi/HyperOS test phone blocks `adb install` unless Developer options → "Install via USB" is enabled; fallback: `adb push` the APK to `/sdcard/Download` and install from Files.
- The built-in Claude Code iOS Simulator panel needs `sudo xcode-select -s` to a full Xcode; not configured (system-wide change). The Simulator app works without it.
