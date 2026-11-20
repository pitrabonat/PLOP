// Build & project-configuration entry points for PLOP! (editor-only, no gameplay).
// Batchmode usage (see CLAUDE.md):
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.ConfigureProject
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.BuildAndroid
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopBuild.ExportIOS
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

        [MenuItem("PLOP/Configure Project Settings")]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "PLOP";
            PlayerSettings.productName = "PLOP!";
            PlayerSettings.bundleVersion = "0.1.0";

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
            PlayerSettings.Android.bundleVersionCode = 1;

            // iOS
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, "com.plop.sleepypuzzle");
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
        }

        [MenuItem("PLOP/Build/Android APK (Development)")]
        public static void BuildAndroid()
        {
            EnsureBootScene();
            EditorUserBuildSettings.buildAppBundle = false;
            var path = Path.Combine(BuildsRoot, "Android", "PLOP-dev.apk");
            Build(BuildTarget.Android, path, BuildOptions.Development);
        }

        [MenuItem("PLOP/Build/iOS Xcode Project")]
        public static void ExportIOS()
        {
            EnsureBootScene();
            var sdk = Environment.GetEnvironmentVariable("PLOP_IOS_SDK");
            PlayerSettings.iOS.sdkVersion = sdk == "device" ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
            var path = Path.Combine(BuildsRoot, "iOS", "PLOP-Xcode");
            Build(BuildTarget.iOS, path, BuildOptions.None);
        }

        static void Build(BuildTarget target, string path, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = options
            });
            var s = report.summary;
            Debug.Log($"[PLOP] {target} build {s.result}: {path} ({s.totalSize / (1024 * 1024)} MB, {s.totalTime})");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
