using System;
using System.IO;
using Plop.Presentation;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Phase 12: renders the app icon from the game's own default Plop (same rig and painter as gameplay) and assigns
    /// it as the default icon (iOS uses it for every size) plus Android legacy, round and adaptive icons.
    /// Re-run after changing the default look: PLOP → Release → Build App Icons, or batch
    /// <c>-executeMethod Plop.CharacterTools.AppIconBuilder.BuildBatch</c>.
    /// </summary>
    public static class AppIconBuilder
    {
        public const string Folder = "Assets/Art/AppIcon";
        private const string FullPath = Folder + "/PLOP-icon-1024.png";
        private const string ForegroundPath = Folder + "/PLOP-adaptive-foreground.png";
        private const string BackgroundPath = Folder + "/PLOP-adaptive-background.png";
        private const float T = 103.7f;
        private static readonly Color Cream = PlopPalette.Hex(0xFBE6D6);
        private static readonly Color Blush = PlopPalette.Hex(0xF6CFC4);

        [MenuItem("PLOP/Release/Build App Icons", priority = 40)]
        public static void BuildFromMenu() => Build();

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var background = Gradient(1024);
            using (var stage = new CharacterStage(new Color(0f, 0f, 0f, 0f)))
            {
                var look = CharacterLook.Default;
                var atlas = ShapeAtlas.ForDensity(512, look);
                var view = new PlopView(stage.Add("Plop").transform, atlas, 0, 7, false);
                view.SetSortingBase(0);
                view.Snap(Vector2.zero, false);
                view.SetExpression(Expression.Happy);
                view.Tick(T);

                // Full icon: the Plop fills ~72 % of the square, on the soft cream gradient, fully opaque (iOS).
                var full = Compose(background, CaptureStraight(stage, new Vector2(0f, 0.03f), new Vector2(1.32f, 1.32f)));
                // Adaptive foreground: content inside the 66 % safe zone of the 108 dp canvas, transparent around it.
                var foreground = CaptureStraight(stage, new Vector2(0f, 0.03f), new Vector2(2.05f, 2.05f));
                Write(full, FullPath);
                Write(foreground, ForegroundPath);
                Write(background, BackgroundPath);
                ShapeAtlas.ReleaseUnused(look);
            }

            Import(FullPath, opaque: true);
            Import(ForegroundPath, opaque: false);
            Import(BackgroundPath, opaque: true);
            Assign();
            Debug.Log("[PLOP] App icons written to " + Folder + " and assigned (default, iOS, Android legacy/round/adaptive).");
        }

        private static Texture2D Gradient(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.62f;
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.45f);
                var c = Color.Lerp(Cream, Blush, t * t);
                c.a = 1f;
                px[y * size + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Straight-alpha capture: rendered over black and over white, alpha = 1 − (white − black).</summary>
        private static Texture2D CaptureStraight(CharacterStage stage, Vector2 center, Vector2 size)
        {
            stage.Camera.backgroundColor = Color.black;
            var k = stage.Capture(center, size, 1024, 1024);
            stage.Camera.backgroundColor = Color.white;
            var w = stage.Capture(center, size, 1024, 1024);
            var kb = k.GetPixels();
            var wb = w.GetPixels();
            for (int i = 0; i < kb.Length; i++)
            {
                float a = Mathf.Clamp01(1f - ((wb[i].r - kb[i].r) + (wb[i].g - kb[i].g) + (wb[i].b - kb[i].b)) / 3f);
                kb[i] = a < 0.002f ? new Color(0f, 0f, 0f, 0f)
                    : new Color(Mathf.Clamp01(kb[i].r / a), Mathf.Clamp01(kb[i].g / a), Mathf.Clamp01(kb[i].b / a), a);
            }
            k.SetPixels(kb);
            k.Apply();
            UnityEngine.Object.DestroyImmediate(w);
            return k;
        }

        private static Texture2D Compose(Texture2D background, Texture2D layer)
        {
            var b = background.GetPixels();
            var l = layer.GetPixels();
            for (int i = 0; i < b.Length; i++)
            {
                var c = l[i];
                b[i] = new Color(Mathf.Lerp(b[i].r, c.r, c.a), Mathf.Lerp(b[i].g, c.g, c.a), Mathf.Lerp(b[i].b, c.b, c.a), 1f);
            }
            var tex = new Texture2D(background.width, background.height, TextureFormat.RGBA32, false);
            tex.SetPixels(b);
            tex.Apply();
            UnityEngine.Object.DestroyImmediate(layer);
            return tex;
        }

        private static void Write(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void Import(string path, bool opaque)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = opaque ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !opaque;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void Assign()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(FullPath);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                {
                    if (kind == AndroidPlatformIconKind.Adaptive) icon.SetTextures(bg, fg);
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
            AssetDatabase.SaveAssets();
        }
    }
}
