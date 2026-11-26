using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plop.Presentation;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Phase 13: brings the selected variants of the branding pack into the project. The pack itself (every icon, logo,
    /// feature graphic and splash alternative, previews and references) stays untouched in
    /// <c>Documentation/Branding/Phase13_Source/</c>, outside the build. Only the selection is copied to
    /// <c>Assets/Art/Branding/</c>; the icon is assigned in Player Settings and the splash layers and menu logo are
    /// referenced from <see cref="BrandingProfile"/>, so nothing else reaches a player.
    /// Menu: PLOP → Branding. Batch: <c>-executeMethod Plop.CharacterTools.BrandingImporter.ApplyBatch
    /// [-plopIcon Hero_Pink] [-plopSplash Day] [-plopLogo 01_Jelly_Pink|text]</c>.
    /// </summary>
    public static class BrandingImporter
    {
        public const string PackRoot = "Documentation/Branding/Phase13_Source/PLOP_Phase13_BrandingPack";
        public const string Folder = "Assets/Art/Branding";
        public const string ProfilePath = Folder + "/Resources/Branding/BrandingProfile.asset";

        // Recommended defaults from README_FIRST — pending the owner's approval.
        public const string DefaultIcon = "Hero_Pink";
        public const string DefaultSplash = "Day";
        public const string DefaultLogo = "01_Jelly_Pink";

        public static readonly string[] Icons = { "Hero_Pink", "Lilac", "Mint" };
        public static readonly string[] Splashes = { "Day", "Night" };
        public static readonly string[] Logos = { "01_Jelly_Pink", "02_Plum_Cloud", "03_Cream_Puff", "04_Berry_Mint", "05_Minimal_Plum", "06_Minimal_Cream" };

        private const string IconFolder = Folder + "/Icon";
        private const string SplashFolder = Folder + "/Splash";
        private const string LogoFolder = Folder + "/Logo";

        [MenuItem("PLOP/Branding/Apply Recommended Defaults", priority = 30)]
        public static void ApplyDefaultsMenu() => Apply(DefaultIcon, DefaultSplash, DefaultLogo);

        [MenuItem("PLOP/Branding/Use Night Splash", priority = 31)]
        public static void NightMenu() => Apply(CurrentOr(p => p.iconVariant, DefaultIcon), "Night", CurrentOr(p => p.menuLogoName, DefaultLogo));

        [MenuItem("PLOP/Branding/Use Day Splash", priority = 32)]
        public static void DayMenu() => Apply(CurrentOr(p => p.iconVariant, DefaultIcon), "Day", CurrentOr(p => p.menuLogoName, DefaultLogo));

        public static void ApplyBatch()
        {
            try
            {
                Apply(Arg("-plopIcon", DefaultIcon), Arg("-plopSplash", DefaultSplash), Arg("-plopLogo", DefaultLogo));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <param name="logo">A logo file name from 02_Logos, or "text" to keep the text title on the main menu.</param>
        public static void Apply(string icon, string splash, string logo)
        {
            if (!Icons.Contains(icon)) throw new ArgumentException("Unknown icon variant " + icon);
            if (!Splashes.Contains(splash)) throw new ArgumentException("Unknown splash variant " + splash);
            if (logo != "text" && !Logos.Contains(logo)) throw new ArgumentException("Unknown logo " + logo);
            if (!Directory.Exists(PackRoot)) throw new DirectoryNotFoundException(PackRoot);

            // 1. Copy the selection (and only the selection) into Assets.
            string iconSrc = $"{PackRoot}/01_Icons/{icon}";
            var iconFiles = new[] { "Master_iOS_1024.png", "Adaptive_Background_432.png", "Adaptive_Foreground_432.png", "Adaptive_Monochrome_432.png" };
            Sync(IconFolder, iconFiles.Select(f => ($"{iconSrc}/{f}", f)));
            string splashSrc = $"{PackRoot}/04_Splash/Layers_{splash}";
            var splashFiles = new[] { "Background_1080x1920.png", "Mascot.png", "Shadow.png", "Logo.png", "Animation_Timeline.json" };
            Sync(SplashFolder, splashFiles.Select(f => ($"{splashSrc}/{f}", f)));
            Sync(LogoFolder, logo == "text" ? Enumerable.Empty<(string, string)>() : new[] { ($"{PackRoot}/02_Logos/{logo}.png", logo + ".png") });
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Import settings.
            foreach (var f in iconFiles) ConfigureIcon($"{IconFolder}/{f}", f.StartsWith("Master") || f.Contains("Background"));
            ConfigureSprite($"{SplashFolder}/Background_1080x1920.png", compressed: true, maxSize: 2048);
            ConfigureSprite($"{SplashFolder}/Mascot.png", compressed: false, maxSize: 1024);
            ConfigureSprite($"{SplashFolder}/Shadow.png", compressed: false, maxSize: 1024);
            ConfigureSprite($"{SplashFolder}/Logo.png", compressed: false, maxSize: 1024);
            if (logo != "text") ConfigureSprite($"{LogoFolder}/{logo}.png", compressed: false, maxSize: 1024);

            // 3. Runtime profile.
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = AssetDatabase.LoadAssetAtPath<BrandingProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BrandingProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.iconVariant = icon;
            profile.splashVariant = splash;
            profile.menuLogoName = logo;
            profile.splashBackground = Sprite($"{SplashFolder}/Background_1080x1920.png");
            profile.splashMascot = Sprite($"{SplashFolder}/Mascot.png");
            profile.splashShadow = Sprite($"{SplashFolder}/Shadow.png");
            profile.splashLogo = Sprite($"{SplashFolder}/Logo.png");
            profile.splashTimeline = AssetDatabase.LoadAssetAtPath<TextAsset>($"{SplashFolder}/Animation_Timeline.json");
            profile.menuLogo = logo == "text" ? null : Sprite($"{LogoFolder}/{logo}.png");
            profile.launchColor = TopColor($"{splashSrc}/Background_1080x1920.png");
            EditorUtility.SetDirty(profile);

            // 4. Player Settings: icons and the native launch colour (Android window / iOS launch screen), so the first
            // frame already matches the splash instead of flashing Unity's dark default.
            AssignIcons();
            PlayerSettings.SplashScreen.backgroundColor = profile.launchColor;
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
            Debug.Log($"[PLOP] Branding applied: icon {icon}, splash {splash}, menu logo {logo}, launch colour #{ColorUtility.ToHtmlStringRGB(profile.launchColor)}");
        }

        private static string CurrentOr(Func<BrandingProfile, string> pick, string fallback)
        {
            var p = AssetDatabase.LoadAssetAtPath<BrandingProfile>(ProfilePath);
            return p != null && !string.IsNullOrEmpty(pick(p)) ? pick(p) : fallback;
        }

        private static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        /// <summary>Makes <paramref name="folder"/> hold exactly these copies (earlier selections are removed; the pack is never touched).</summary>
        private static void Sync(string folder, IEnumerable<(string src, string name)> files)
        {
            Directory.CreateDirectory(folder);
            var keep = new HashSet<string>();
            foreach (var (src, name) in files)
            {
                if (!File.Exists(src)) throw new FileNotFoundException(src);
                string dst = $"{folder}/{name}";
                if (!File.Exists(dst) || !File.ReadAllBytes(dst).SequenceEqual(File.ReadAllBytes(src))) File.Copy(src, dst, true);
                keep.Add(name);
            }
            foreach (var path in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".meta") || keep.Contains(name)) continue;
                AssetDatabase.DeleteAsset($"{folder}/{name}");
            }
        }

        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void ConfigureIcon(string path, bool opaque)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = opaque ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !opaque;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureSprite(string path, bool compressed, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = compressed ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var p = importer.GetPlatformTextureSettings(platform);
                p.overridden = true;
                // The full-screen background is a soft gradient: ASTC 6x6 keeps it ~1 MB instead of 8 MB without visible banding.
                p.format = compressed ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.RGBA32;
                p.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(p);
            }
            importer.SaveAndReimport();
        }

        /// <summary>Average of the background's top rows (the colour the native launch screen should show).</summary>
        private static Color TopColor(string pngPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(pngPath));
            var px = tex.GetPixels(0, tex.height - 8, tex.width, 8);
            UnityEngine.Object.DestroyImmediate(tex);
            var sum = Color.clear;
            foreach (var c in px) sum += c;
            var avg = sum / px.Length;
            avg.a = 1f;
            return avg;
        }

        private static void AssignIcons()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconFolder}/Master_iOS_1024.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconFolder}/Adaptive_Background_432.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconFolder}/Adaptive_Foreground_432.png");
            var mono = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconFolder}/Adaptive_Monochrome_432.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                {
                    if (kind == AndroidPlatformIconKind.Adaptive)
                    {
                        if (icon.maxLayerCount >= 3) icon.SetTextures(bg, fg, mono);
                        else icon.SetTextures(bg, fg);
                    }
                    else icon.SetTexture(full);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var icon in icons) icon.SetTexture(full);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }
        }
    }
}
