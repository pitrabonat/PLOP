# CLAUDE.md — PLOP! (Sleepy Puzzle)

Shared guidance for Claude Code, Codex, and other contributors. Start with
[PROJECT_STATE.md](PROJECT_STATE.md) for the verified handoff, then use
[ARCHITECTURE.md](ARCHITECTURE.md) and the [development log](DEVELOPMENT_LOG.md).

## Project
- **Name:** PLOP! — Sleepy Puzzle (Unity project folder: `PLOP`)
- **Engine:** Unity **6000.3.29f1** (Unity 6.3 LTS), Apple Silicon (arm64) editor
- **Render pipeline:** URP with 2D Renderer (from the Universal 2D template)
- **Language:** C# (Unity default API compatibility, .NET Standard 2.1)
- **Targets:** Android (IL2CPP, ARM64, min API 26, target = highest installed) and iOS (min iOS 15.0)
- **Orientation:** Portrait only
- **Repository:** https://github.com/pitrabonat/PLOP (remote `origin`, branch `main`; public)
- **Status:** Phase 11 rebalanced campaign (`PLOP_Pack_003`), mechanic-tiered Daily pool, hint grace, quality tooling and dev playtest metrics, on top of Phase 10 mechanics (Frost, Lava, Breeze, Loops), nine Worlds, Limited Moves, Phase 9 characters and Phase 8 depth. See PROJECT_STATE.md.
- **Visual style:** "Soft Toy Minimalism" — see `Documentation/VISUAL_STYLE.md` (palette, corner radii, animation).
- **Docs:** `Documentation/` — ARCHITECTURE, GAME_RULES, SOLVER, LEVEL_GENERATION, PUZZLE_QUALITY, CHARACTERS, LEVEL_FORMAT, VISUAL_STYLE, TESTING, DEVELOPMENT_LOG.

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
  Scenes/            Gameplay.unity = play and build entry scene; Boot.unity = URP 2D template scene
  Scripts/
    Core/            PLOP.Core        — pure C# rules/state/engine/validation/JSON (NO UnityEngine)
    Solver/          PLOP.Solver      — BFS solver + state-space analyzer (NO UnityEngine)
    LevelGeneration/ PLOP.Generation  — PRNG, generator, verifier, duplicates, difficulty, PuzzleInsight,
                                        QualitySearch, CampaignPlan, CampaignInsights (NO UnityEngine)
    Meta/            PLOP.Meta        — Dream Dust wallet, level records/Perfect, Daily Gift/Puzzle rules,
                                        cosmetic catalog/collection/presets (NO UnityEngine)
    Unity/           PLOP.Unity       — board, HUD, MainMenu, CharacterStudio, input, worlds, audio, PlopGameController
      Characters/    CharacterArt recipes, ShapeAtlas character painters, PlopPart, MoveEffects, validator
    CharacterTools/Editor/ PLOP.CharacterTools — Animation Lab, cosmetic validator, reference-sheet capture (Editor only)
  Data/Cosmetics/Resources/Cosmetics/PLOP_Cosmetics_001.json — cosmetic catalog (IDs are save keys)
      Worlds/        WorldCatalog, WorldTheme, WorldBackdrop
  Art/Resources/Worlds/                cosmetic chapter ScriptableObjects
  Art/Resources/WorldBackdrops/        authored portrait environments
  Art/WorldPreviews/                   chapter-card sprites
  Plugins/iOS/                         UIKit haptic bridge
    LevelLab/Editor/ PLOP.LevelLab    — editor-only Level Lab window (PLOP → Level Lab)
    Editor/          build + scene setup scripts (PlopBuild, PlopSceneSetup)
    Gameplay/ UI/ SaveSystem/          reserved (empty)
  Tests/EditMode/    PLOP.Tests          — NUnit tests (also run by DotNet/Plop.Tests)
  Tests/PlayMode/    PLOP.Tests.PlayMode — Unity integration tests
  Levels/Resources/Levels/PLOP_Pack_003.json  — shipped level pack (160 verified levels, Phase 11 rebalance)
  Levels/Resources/Levels/PLOP_Deadlocks_003.bytes — generated exact deadlock data for Pack_003
  Levels/Resources/Levels/PLOP_Pack_002_Order.json — Phase 10 level order + aliases, for save migration
  Levels/Resources/Levels/PLOP_Daily_002.json — 60-puzzle offline Daily pool (classic + 4 mechanic tiers)
  Levels/Archive/ — Pack_001/002, Deadlocks_001/002, Daily_001: read for migration, comparison and reuse
  Levels/Resources/Levels/PLOP_Pack_001_SlotHistory.json — old→new level IDs per slot for save migration
  Art/ Audio/ Animations/ Materials/ Prefabs/ Settings/
DotNet/              Plop.Lib (netstandard2.1 build of Core/Solver/Generation), Plop.Tests, Plop.Bench
Documentation/       design & technical docs, benchmarks and running-game previews
```

## Architecture rules (do not break)
- **One rules implementation:** `Plop.Core.MoveRules.Apply`. Engine, solver, generator and tools call it.
  Phase 10 mechanics resolve in a fixed precedence (Lava → Nest → Loop → fan → Frost) under a step budget;
  a failed (Lava) or out-of-moves attempt accepts no swipes until Undo or Restart.
- Core/Solver/Generation must never reference UnityEngine (`noEngineReferences: true`), and must compile
  under C# 9 / netstandard2.1 (checked by `DotNet/Plop.Lib`).
- Gameplay never touches visuals: presentation reacts to `GameEvent`s. Views never decide logical positions:
  a Plop's root position comes only from `PlopMoved.To` / `Sync(state)`; animation touches child transforms.
- Visual rule: no sharp corners. New board/UI shapes go into `ShapeAtlas` / `UiSprites` as SDF shapes with
  radii as fractions of the cell; colors only from `PlopPalette`; no pure black shadows or pure white highlights.
- No generation or solver search in `Update`. Runtime uses exact, versioned
  deadlock proofs from the sidecar and expands keys only for the current level.
  Hints and the bounded automatic fallback run off the main thread. Cancellation
  or a search limit leaves the result unknown.
- Any change that can alter generated output bumps `GeneratorInfo.Version`; solver output → `SolverInfo.Version`;
  JSON breaking change → `LevelSerializer.SchemaVersion`.
- Levels that cannot be verified within budget are rejected — never assumed solvable.
- `optimalMoves` in the pack is the BFS shortest solution and is the Perfect target. Perfect = engine history
  length (Undo removes moves) equals it with no hint used this visit. Dream Dust is granted once per key
  (`complete:ID`, `perfect:ID`, `gift:date`, `daily:date`, `dailyPerfect:date`); never pay a reward twice.
- Never silently reset saves. When a campaign slot gets a new level ID, append the old ID to
  `PLOP_Pack_001_SlotHistory.json` (`--production` does this) so `LevelProgress` migrates progress.
  Pack 002 imports Phase 8 progress once via `MigrateFromPreviousCampaign`: completions only for IDs
  that still exist, access as levels beaten + 1. A changed puzzle never inherits a completion.
- Economy (`RewardRules`): completion 8, Perfect +4, Gift 10, Daily 12 (+6), hint 15 (free on levels 1–6
  and mechanic intros). Hint spends use `TrySpend` keys `hint:<level>:<state>`: one charge per board state.

## Handoff and Git

- Check the branch and working tree before edits. Keep an identifiable checkpoint
  for significant work, use meaningful commits, and preserve existing changes.
- Use one coding agent as the working-tree editor. Update `PROJECT_STATE.md`
  and `DEVELOPMENT_LOG.md` after milestones and record executed verification.
- Keep generated levels and Unity .meta changes with the source changes they belong to.

## Conventions
- Namespaces: `Plop.<Area>` (e.g. `Plop.Core`, `Plop.Solver`); editor code in `Plop.EditorTools`.
- One public type per file; file name = type name. PascalCase types/methods, `_camelCase` private fields, `[SerializeField] private` over public fields.
- Keep game logic (Solver, LevelGeneration) free of `MonoBehaviour` where possible so it is unit-testable.
- Use the Input System package (already installed), not the legacy Input Manager.
- One asmdef per Scripts area (see table in Documentation/ARCHITECTURE.md).
- Tests: shared NUnit files must stick to NUnit 3.5 APIs (Unity's fork).
- Always commit `.meta` files; never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds.
- Binary assets (png, psd, wav, fbx, …) go through **Git LFS** (see `.gitattributes`).
- Minimal dependencies: ask before adding packages. Removed from template on purpose: Collab, Rider, Visual Scripting, Timeline, SpriteShape, Tilemap Extras, Aseprite, PSD Importer, 2D Animation, 2D Tooling.

## Tooling
- IDE: VS Code + "Unity" extension (`visualstudiotoolsforunity.vstuc`, pulls in C# Dev Kit). .NET SDK 10 at `~/.dotnet`.
  In Unity: Preferences → External Tools → External Script Editor = Visual Studio Code.
- Git + Git LFS (`git lfs install` done for the user).
- ADB: `~/Library/Android/sdk/platform-tools/adb` (same version, 37.0.1, as Unity's bundled one).

## Play in Unity

Open the existing project and choose **PLOP → Play Game**, or open
`Assets/Scenes/Gameplay.unity` and click Play. Drag in the Game tab or use
arrow keys/WASD. The Levels button opens Worlds. Resume returns to the
highest unlocked level after a replay. Settings has independent ambience
and effects volume, master mute, haptics, reduced motion and the 30/60 FPS cap.

Gestures require a dominant axis and a density-aware minimum distance. One
gesture can wait for the current slide. Undo, Restart, pause and navigation
clear the buffer. The F3 readout is available in the Editor and development
builds. On a development mobile install, the local integer preference
`plop.debug.diagnostics` set to 1 enables it at launch. Game-loop intervals
in the readout and presentation intervals from Android SurfaceFlinger
measure different parts of the frame.

## World art and campaign

World assets declare compatible one-based catalog ranges, color accents,
lighting and audio. Add future cosmetic chapters through these assets.
Never encode world state in progression saves. To regenerate the shipped
portrait and card textures from the authored painter, run
`python3 Tools/WorldArt/render_worlds.py "$PWD"`. Keep sprite GUIDs stable
and re-check the four portrait profiles.

The original World ambience beds are generated by
`python3 Tools/AudioArt/render_ambience.py`. Use the same filenames
and Unity import metadata when regenerating them. The runtime crossfades
between two loop sources. The ten short Foley cues are authored in C#
and played on a fixed three-voice pool.

## Tests, levels and benchmark
```bash
export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet          # keep NuGet cache off the internal SSD
dotnet test DotNet/Plop.Tests/Plop.Tests.csproj                # fast engine-free tests
dotnet run -c Release --project DotNet/Plop.Bench -- --production \
  --pack Assets/Levels/Resources/Levels/PLOP_Pack_001.json \
  --report Documentation/benchmarks/generation_benchmark_v3.md   # quality search, ~80 s on 10 threads
dotnet run -c Release --project DotNet/Plop.Bench -- --audit Assets/Levels/Resources/Levels/PLOP_Pack_001.json report.md label
dotnet run -c Release --project DotNet/Plop.Bench -- --insights Assets/Levels/Resources/Levels/PLOP_Pack_001.json out.tsv
dotnet run -c Release --project DotNet/Plop.Bench -- --verify-production Assets/Levels/Resources/Levels/PLOP_Pack_001.json
dotnet run -c Release --project DotNet/Plop.Bench -- --deadlocks \
  Assets/Levels/Resources/Levels/PLOP_Pack_001.json \
  Assets/Levels/Resources/Levels/PLOP_Deadlocks_001.bytes 600000   # 600k covers all 100 Phase 8 levels
dotnet run -c Release --project DotNet/Plop.Bench -- --perf Assets/Levels/Resources/Levels/PLOP_Pack_001.json
UNITY=/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /Volumes/PLOP_DEV/Builds/Logs/editmode.xml
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults /Volumes/PLOP_DEV/Builds/Logs/playmode.xml
"$UNITY" -batchmode -quit -projectPath "$PWD" -executeMethod Plop.EditorTools.PlopSceneSetup.CreateGameplayScene
```
Level Lab: Unity menu **PLOP → Level Lab** (edit, generate, solve, replay, metrics, import/export, batch generation, filters and manual catalog ordering;
player-model insight panel, campaign overview with Plop/size/trivial filters and most-similar comparison;
Phase 10: Frost/Lava/Fan/Loop tools, move limit, mechanic quality search, effect-resolution debugger, chain/loop-risk inspector;
Phase 11: "Balance & quality" — quality audit, simulated players, filters, similarity, before/after pack comparison, md/json export).

Phase 11 campaign: `cd DotNet && dotnet run -c Release --project Plop.Bench -- --balanced` (deterministic, ~2 min) rebuilds
`PLOP_Pack_003.json`, `PLOP_Daily_002.json`, `PLOP_Pack_002_Order.json` and the reports in `Documentation/Phase11/`; then
`--deadlocks ../Assets/Levels/Resources/Levels/PLOP_Pack_003.json ../Assets/Levels/Resources/Levels/PLOP_Deadlocks_003.bytes 600000`.
Also `--quality-audit <pack> out.json out.md`, `--economy [md] [json]`, `--playtest-summary file.csv`.

Phase 10 campaign (archived): `--expanded` rebuilt `PLOP_Pack_002.json` (now in `Assets/Levels/Archive/`) and
`Documentation/benchmarks/campaign_phase10.md`. See Documentation/LEVEL_GENERATION.md.

Characters: **PLOP → Characters → Animation Lab / Validate Cosmetics / Capture Reference Sheets** (see
`Documentation/CHARACTERS.md`). Batch validator: `-executeMethod Plop.CharacterTools.CosmeticValidatorWindow.RunBatch`.

Screenshot hooks (development builds only, never saved progress): integer pref `plop.debug.level` (1-based)
and string pref `plop.debug.moves` (e.g. `DLDRU`) skip the main menu. On the Android emulator the debuggable
build's prefs can be edited with `run-as` — run `python3 Tools/Android/debugprefs.py <level|0> [moves]` (force-stops the app; 0 clears).
Local playtest metrics (dev builds only, no SDK): `debugprefs.py playtest on|off`, `debugprefs.py pull-playtest out.csv`
(see `Documentation/Phase11/PLAYTEST_GUIDE.md`).

## Build instructions
GUI: Unity menu **PLOP → Build → Android APK (Development)** / **iOS Xcode Project (Device)** / **iOS Xcode Project (Simulator)**.

CLI (quit any open Editor on this project first):
```bash
UNITY=/Volumes/PLOP_DEV/Unity/Editors/6000.3.29f1/Unity.app/Contents/MacOS/Unity
PROJ=/Volumes/PLOP_DEV/Unity/Projects/PLOP
# Android dev APK -> /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
# Set PLOP_ANDROID_APK to use a separate output path for a verification build.
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget Android -executeMethod Plop.EditorTools.PlopBuild.BuildAndroid -logFile -
# Install & launch on a connected phone
adb install -r /Volumes/PLOP_DEV/Builds/Android/PLOP-dev.apk
adb shell am start -n com.plop.sleepypuzzle/com.unity3d.player.UnityPlayerGameActivity  # not monkey: it can inject a stray touch
export DEVELOPER_DIR=/Volumes/PLOP_DEV/Xcode/Xcode-26.3.app/Contents/Developer
# iOS Simulator: export (arm64 simulator) -> build -> run in "PLOP iPhone 15 Pro Max" simulator
# Set PLOP_IOS_SIM_EXPORT to export to a separate verification directory.
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
