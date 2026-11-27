// Sprint 04 monetization build entry points (editor-only).
//   Unity -batchmode -quit -projectPath <PLOP> -executeMethod Plop.EditorTools.PlopAdsBuild.ConfigureAdMobTestSettings
//   ...PlopAdsBuild.BuildAdsDisabledApk | BuildTestAdsApk | BuildAdsDisabledBundle | ExportAdsIOSSimulator | BuildAllAndroid
// Outputs under /Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization. No build entry can select production ads:
// AdConfiguration.ProductionUnlocked is a compile-time false and the production ad unit IDs are empty.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Plop.EditorTools
{
    public static class PlopAdsBuild
    {
        const string Root = "/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization";

        // Google's published SAMPLE app IDs (https://developers.google.com/admob/android/test-ads,
        // https://developers.google.com/admob/ios/test-ads). They are not this app's IDs and earn nothing. The plugin
        // refuses to build Android without some app ID; the publisher replaces these with their own AdMob app IDs
        // only when production is approved (see Documentation/Monetization/AD_CONFIGURATION.md).
        public const string SampleAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string SampleIosAppId = "ca-app-pub-3940256099942544~1458002511";
        public const string TestAdsDefine = "PLOP_ADS_TEST";

        const string SettingsType = "GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor";

        /// <summary>
        /// Writes the sample app IDs into the plugin's settings asset (created by the plugin itself if missing).
        /// The App Tracking Transparency description stays empty: PLOP! does not ask for tracking permission.
        /// Existing non-sample IDs are never overwritten.
        /// </summary>
        [MenuItem("PLOP/Ads/Configure AdMob Test Settings")]
        public static void ConfigureAdMobTestSettings()
        {
            var type = Type.GetType(SettingsType);
            if (type == null) throw new InvalidOperationException("Google Mobile Ads editor assembly not found.");
            var load = type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var settings = (ScriptableObject)load.Invoke(null, null);
            var so = new SerializedObject(settings);
            SetIfEmptyOrSample(so, "adMobAndroidAppId", SampleAndroidAppId);
            SetIfEmptyOrSample(so, "adMobIOSAppId", SampleIosAppId);
            so.FindProperty("userTrackingUsageDescription").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[PLOP][Ads] AdMob settings: Android " + so.FindProperty("adMobAndroidAppId").stringValue +
                      ", iOS " + so.FindProperty("adMobIOSAppId").stringValue + " (Google sample IDs unless the publisher set their own)");
        }

        static void SetIfEmptyOrSample(SerializedObject so, string field, string sample)
        {
            var p = so.FindProperty(field);
            if (string.IsNullOrEmpty(p.stringValue) || p.stringValue.StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal))
                p.stringValue = sample;
            else
                Debug.LogWarning("[PLOP][Ads] Keeping existing " + field + " (not a Google sample ID).");
        }

        static string Out(string env, params string[] parts)
        {
            var path = Environment.GetEnvironmentVariable(env);
            return Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Path.Combine(Root, Path.Combine(parts)) : path);
        }

        /// Release-configured APK with advertising Disabled (the shipping default): the SDK is linked but never
        /// initialised, no consent form, no ad request, no ad UI.
        [MenuItem("PLOP/Build/Sprint 04/Android APK (Ads Disabled, Release)")]
        public static void BuildAdsDisabledApk()
        {
            ConfigureAdMobTestSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            PlopBuild.Build(BuildTarget.Android, Out("PLOP_ANDROID_APK", "01_AdDisabledBuild",
                "PLOP-" + PlopBuild.ReleaseVersion + "-ads-disabled-release-testsigned.apk"), BuildOptions.None, null);
        }

        /// Development APK that shows Google's official TEST ads only (PLOP_ADS_TEST), for QA on emulators/devices.
        [MenuItem("PLOP/Build/Sprint 04/Android APK (Google Test Ads, Development)")]
        public static void BuildTestAdsApk()
        {
            ConfigureAdMobTestSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            PlopBuild.Build(BuildTarget.Android, Out("PLOP_ANDROID_APK", "02_TestAdBuild",
                "PLOP-" + PlopBuild.ReleaseVersion + "-testads-dev.apk"), BuildOptions.Development, new[] { TestAdsDefine });
        }

        /// Release AAB with advertising Disabled (the configuration a store upload would start from; not uploaded).
        [MenuItem("PLOP/Build/Sprint 04/Android App Bundle (Ads Disabled, Release)")]
        public static void BuildAdsDisabledBundle()
        {
            ConfigureAdMobTestSettings();
            EditorUserBuildSettings.buildAppBundle = true;
            try
            {
                PlopBuild.Build(BuildTarget.Android, Out("PLOP_ANDROID_AAB", "03_Android",
                    "PLOP-" + PlopBuild.ReleaseVersion + "-ads-disabled-release-testsigned.aab"), BuildOptions.None, null);
            }
            finally { EditorUserBuildSettings.buildAppBundle = false; }
        }

        public static void BuildAllAndroid()
        {
            BuildAdsDisabledApk();
            BuildTestAdsApk();
            BuildAdsDisabledBundle();
        }

        /// iOS Simulator Xcode project with Google test ads (CocoaPods dependencies are resolved by EDM if available).
        [MenuItem("PLOP/Build/Sprint 04/iOS Xcode Project (Simulator, Test Ads)")]
        public static void ExportAdsIOSSimulator()
        {
            ConfigureAdMobTestSettings();
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                PlopBuild.Build(BuildTarget.iOS, Out("PLOP_IOS_SIM_EXPORT", "04_iOS", "Xcode-Simulator-TestAds"),
                    BuildOptions.Development, new[] { TestAdsDefine });
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
                AssetDatabase.SaveAssets();
            }
        }

        /// iOS device Xcode project with advertising Disabled (signing happens in Xcode with the owner's team).
        [MenuItem("PLOP/Build/Sprint 04/iOS Xcode Project (Device, Ads Disabled)")]
        public static void ExportAdsDisabledIOSDevice()
        {
            ConfigureAdMobTestSettings();
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlopBuild.Build(BuildTarget.iOS, Out("PLOP_IOS_EXPORT", "04_iOS", "Xcode-Device-AdsDisabled"), BuildOptions.None, null);
            }
            finally { PlayerSettings.iOS.sdkVersion = previousSdk; AssetDatabase.SaveAssets(); }
        }
    }
}
