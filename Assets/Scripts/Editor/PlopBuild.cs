// Build & project-configuration entry points for PLOP! (editor-only, no gameplay).
// Batchmode usage (see CLAUDE.md):
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.ConfigureProject
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.BuildAndroid
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.ExportIOS
// Release candidate (Phase 12, outputs under /Volumes/PLOP_DEV/Builds/ReleaseCandidate/1.0.0-RC1):
//   ...PlopBuild.BuildAndroidRCDevelopment | BuildAndroidRCRelease | BuildAndroidRCBundle
//   ...PlopBuild.ExportIOSRCDevice | ExportIOSRCSimulator
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.CodeEditor;
using UnityEngine;

namespace Plop.EditorTools
{
    public static class PlopBuild
    {
        const string BootScene = "Assets/Scenes/Boot.unity";
        const string BuildsRoot = "/Volumes/PLOP_DEV/Builds";

        // Single source of truth for the shipped version (Phase 12: 1.0.0 RC1). The Android version code and the
        // iOS build number only ever go up; 100 = 1.0.0, so later 1.0.x builds can use 101, 102, …
        public const string ReleaseVersion = "1.0.0";
        public const string ReleaseLabel = "1.0.0-RC1";
        public const int ReleaseVersionCode = 100;
        public const string ReleaseBuildNumber = "100";
        const string RCRoot = BuildsRoot + "/ReleaseCandidate/" + ReleaseLabel;

        static void ApplyReleaseVersion()
        {
            PlayerSettings.bundleVersion = ReleaseVersion;
            PlayerSettings.Android.bundleVersionCode = ReleaseVersionCode;
            PlayerSettings.iOS.buildNumber = ReleaseBuildNumber;
            // Phase 12 startup: no engine splash (optional in Unity 6 on every plan); the game scene draws its own
            // background on the first frame.
            PlayerSettings.SplashScreen.show = false;
        }

        [MenuItem("PLOP/Configure Project Settings")]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "PLOP";
            PlayerSettings.productName = "PLOP!";
            PlayerSettings.bundleVersion = ReleaseVersion;

            // Portrait only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Android: IL2CPP + ARM64 (required by Google Play), API 26+
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.plop.sleepypuzzle");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = ReleaseVersionCode;

            // iOS
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, "com.plop.sleepypuzzle");
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = ReleaseBuildNumber;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;

            EnsureBootScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[PLOP] Project settings configured.");
        }

        const string TemplateScene = "Assets/Scenes/SampleScene.unity";
        const string VSCodeApp = "/Applications/Visual Studio Code.app";

        /// One-time setup used by automation: project settings + VS Code as external editor.
        public static void SetupProject()
        {
            ConfigureProject();
            if (Directory.Exists(VSCodeApp))
            {
                CodeEditor.SetExternalScriptEditor(VSCodeApp);
                CodeEditor.CurrentEditor.SyncAll();
                Debug.Log("[PLOP] External script editor set to VS Code; solution synced.");
            }
        }

        /// Automation: setup + Android dev build in a single editor session.
        public static void SetupAndBuildAndroid()
        {
            SetupProject();
            BuildAndroid();
        }

        static void EnsureBootScene()
        {
            if (!File.Exists(BootScene) && File.Exists(TemplateScene))
            {
                // Keep the template's URP 2D scene setup (2D camera + Global Light 2D), just rename it.
                var err = AssetDatabase.MoveAsset(TemplateScene, BootScene);
                if (!string.IsNullOrEmpty(err)) Debug.LogError("[PLOP] " + err);
            }
            if (!File.Exists(BootScene))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootScene);
            }
            // Prefer the gameplay prototype scene when it exists (see PlopSceneSetup).
            var entry = File.Exists(PlopSceneSetup.GameplayScene) ? PlopSceneSetup.GameplayScene : BootScene;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(entry, true) };
        }

        [MenuItem("PLOP/Build/Android APK (Development)")]
        public static void BuildAndroid()
        {
            EnsureBootScene();
            EditorUserBuildSettings.buildAppBundle = false;
            var path = Environment.GetEnvironmentVariable("PLOP_ANDROID_APK");
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(BuildsRoot, "Android", "PLOP-dev.apk");
            path = Path.GetFullPath(path);
            Build(BuildTarget.Android, path, BuildOptions.Development);
        }

        /// iOS Xcode project for real devices -> Builds/iOS/PLOP-Xcode.
        /// Set PLOP_IOS_SDK=simulator (batchmode) to call ExportIOSSimulator instead.
        [MenuItem("PLOP/Build/iOS Xcode Project (Device)")]
        public static void ExportIOS()
        {
            if (Environment.GetEnvironmentVariable("PLOP_IOS_SDK") == "simulator") { ExportIOSSimulator(); return; }
            EnsureBootScene();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Build(BuildTarget.iOS, Path.Combine(BuildsRoot, "iOS", "PLOP-Xcode"), BuildOptions.None);
        }

        /// iOS Xcode project for the iOS Simulator -> Builds/iOS/PLOP-Xcode-Sim.
        /// Set PLOP_IOS_SIM_EXPORT to choose a separate output directory.
        [MenuItem("PLOP/Build/iOS Xcode Project (Simulator)")]
        public static void ExportIOSSimulator()
        {
            var path = Environment.GetEnvironmentVariable("PLOP_IOS_SIM_EXPORT");
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(BuildsRoot, "iOS", "PLOP-Xcode-Sim");
            path = Path.GetFullPath(path);
            EnsureBootScene();

            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                Build(BuildTarget.iOS, path, BuildOptions.Development);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
                AssetDatabase.SaveAssets();
            }
        }

        // ---------- Phase 12 release candidate ----------

        static string RCPath(string env, params string[] parts)
        {
            var path = Environment.GetEnvironmentVariable(env);
            return Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Path.Combine(RCRoot, Path.Combine(parts)) : path);
        }

        /// Development APK (Visual Lab, diagnostics and debug prefs available) for internal testing.
        [MenuItem("PLOP/Build/Release Candidate/Android APK (Development)")]
        public static void BuildAndroidRCDevelopment()
        {
            EnsureBootScene();
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, RCPath("PLOP_ANDROID_APK", "Android", "Development", "PLOP-" + ReleaseLabel + "-dev.apk"), BuildOptions.Development);
        }

        /// Release-configured APK: no development player, no dev tools. Signed with the local debug key unless a
        /// keystore is configured in Player Settings (never stored in the repository).
        [MenuItem("PLOP/Build/Release Candidate/Android APK (Release, test-signed)")]
        public static void BuildAndroidRCRelease()
        {
            EnsureBootScene();
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, RCPath("PLOP_ANDROID_APK", "Android", "Release", "PLOP-" + ReleaseLabel + "-release-testsigned.apk"), BuildOptions.None);
        }

        /// Android App Bundle for a future Google Play upload (same release configuration as the APK).
        [MenuItem("PLOP/Build/Release Candidate/Android App Bundle (Release, test-signed)")]
        public static void BuildAndroidRCBundle()
        {
            EnsureBootScene();
            EditorUserBuildSettings.buildAppBundle = true;
            try { Build(BuildTarget.Android, RCPath("PLOP_ANDROID_AAB", "Android", "AAB", "PLOP-" + ReleaseLabel + "-release-testsigned.aab"), BuildOptions.None); }
            finally { EditorUserBuildSettings.buildAppBundle = false; }
        }

        /// All three Android release-candidate outputs in one editor session (development APK, release APK, AAB).
        public static void BuildAndroidRCAll()
        {
            BuildAndroidRCDevelopment();
            BuildAndroidRCRelease();
            BuildAndroidRCBundle();
        }

        /// Both iOS release-candidate Xcode projects in one editor session.
        public static void ExportIOSRCAll()
        {
            ExportIOSRCDevice();
            ExportIOSRCSimulator();
        }

        /// Release Xcode project for physical iPhones (signing is done in Xcode with the owner's team).
        [MenuItem("PLOP/Build/Release Candidate/iOS Xcode Project (Device, Release)")]
        public static void ExportIOSRCDevice()
        {
            EnsureBootScene();
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                Build(BuildTarget.iOS, RCPath("PLOP_IOS_EXPORT", "iOS", "Xcode-Device"), BuildOptions.None);
            }
            finally { PlayerSettings.iOS.sdkVersion = previousSdk; AssetDatabase.SaveAssets(); }
        }

        /// Release-configured Xcode project for the iOS Simulator (arm64).
        [MenuItem("PLOP/Build/Release Candidate/iOS Xcode Project (Simulator, Release)")]
        public static void ExportIOSRCSimulator()
        {
            EnsureBootScene();
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                Build(BuildTarget.iOS, RCPath("PLOP_IOS_SIM_EXPORT", "iOS", "Xcode-Simulator"), BuildOptions.None);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
                AssetDatabase.SaveAssets();
            }
        }

        static void Build(BuildTarget target, string path, BuildOptions options) => Build(target, path, options, null);

        /// <summary>Sprint 04: <paramref name="extraDefines"/> (for example PLOP_ADS_TEST) apply to this build only.</summary>
        internal static void Build(BuildTarget target, string path, BuildOptions options, string[] extraDefines)
        {
            EnsureBootScene();
            ApplyReleaseVersion();
            bool release = (options & BuildOptions.Development) == 0;
            // Release players keep stack traces only for errors and exceptions (no per-log stack capture cost).
            var logTrace = PlayerSettings.GetStackTraceLogType(LogType.Log);
            var warnTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning);
            if (release)
            {
                PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            }
            try { BuildCore(target, path, options, extraDefines); }
            finally
            {
                PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, warnTrace);
                AssetDatabase.SaveAssets();
            }
        }

        /// Gradle's incremental packaging rewrites the previous launcher APK/AAB in place and leaves dead space
        /// (Phase 11 K-14: 64 MB files for 46 MB of content). The outputs folder is a regenerated build cache
        /// inside Library, so it is cleared before every Android build.
        static void ClearGradleOutputs()
        {
            var prj = Path.GetFullPath("Library/Bee/Android/Prj");
            if (!Directory.Exists(prj)) return;
            foreach (var outputs in Directory.GetDirectories(prj, "outputs", SearchOption.AllDirectories))
            {
                if (!outputs.Replace('\\', '/').EndsWith("/launcher/build/outputs")) continue;
                Directory.Delete(outputs, true);
                Debug.Log("[PLOP] Cleared Gradle outputs: " + outputs);
            }
        }

        static void BuildCore(BuildTarget target, string path, BuildOptions options, string[] extraDefines)
        {
            if (target == BuildTarget.Android) ClearGradleOutputs();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = options,
                extraScriptingDefines = extraDefines
            });
            var s = report.summary;
            Debug.Log($"[PLOP] {target} build {s.result}: {path} ({s.totalSize / (1024 * 1024)} MB, {s.totalTime})");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
