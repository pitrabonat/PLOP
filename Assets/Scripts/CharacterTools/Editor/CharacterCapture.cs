using System;
using System.IO;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Renders reference sheets of the runtime Plop rig (styles, finishes, expressions, accessories, Nests,
    /// palettes and small-size readability) to PNG. These are renders of the shipped code, not concept art.
    /// Menu: PLOP → Characters → Capture Reference Sheets. Batch: -executeMethod Plop.CharacterTools.CharacterCapture.CaptureBatch
    /// with PLOP_CAPTURE_DIR set.
    /// </summary>
    public static class CharacterCapture
    {
        private static readonly Color Light = PlopPalette.PlayBackground;
        private static readonly Color Dark = PlopPalette.Hex(0x40364F); // close to the Moonlit Clouds night sky
        private const float T = 103.7f; // fixed animation time: identical renders every run

        [MenuItem("PLOP/Characters/Capture Reference Sheets", priority = 22)]
        public static void CaptureFromMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Save Plop reference sheets", "Documentation/Previews", "Phase9");
            if (!string.IsNullOrEmpty(dir)) CaptureAll(dir);
        }

        public static void CaptureBatch()
        {
            string dir = Environment.GetEnvironmentVariable("PLOP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("Documentation/Previews/Phase9/sheets");
            try
            {
                CaptureAll(dir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void CaptureAll(string dir)
        {
            Directory.CreateDirectory(dir);
            var d = CharacterLook.Default;
            using (var stage = new CharacterStage(Light))
            {
                // Close-ups of the three body styles.
                foreach (var style in new[] { "classic", "bouncy", "snoozy" })
                {
                    stage.Clear();
                    var look = d.With(style: CharacterArt.Styles[style]);
                    Tile(stage, look, Vector2.zero);
                    Plop(stage, look, 0, Vector2.zero);
                    Save(stage.Capture(new Vector2(0f, 0.05f), new Vector2(1.35f, 1.35f), 900, 900), dir, "style-" + style + ".png");
                }

                // The family side by side, with a gameplay-scale label.
                stage.Clear();
                string[] styles = { "classic", "bouncy", "snoozy" };
                for (int i = 0; i < 3; i++)
                {
                    var look = d.With(style: CharacterArt.Styles[styles[i]]);
                    Tile(stage, look, new Vector2(i * 1.1f, 0f));
                    Plop(stage, look, i, new Vector2(i * 1.1f, 0f));
                    Label(stage, Title(styles[i]), new Vector2(i * 1.1f, -0.68f));
                }
                Save(stage.Capture(new Vector2(1.1f, -0.05f), new Vector2(3.4f, 1.6f), 1500, 700), dir, "styles.png");

                // Six finishes on two colours.
                stage.Clear();
                string[] finishes = { "classic", "frosted", "milky", "pearl", "dewdrop", "velvet" };
                for (int i = 0; i < finishes.Length; i++)
                for (int row = 0; row < 2; row++)
                {
                    var look = d.With(finish: CharacterArt.Finishes[finishes[i]]);
                    var at = new Vector2(i * 1.05f, -row * 1.05f);
                    Tile(stage, look, at);
                    Plop(stage, look, row == 0 ? 0 : 1, at);
                    if (row == 1) Label(stage, Title(finishes[i]), at + new Vector2(0f, -0.66f));
                }
                Save(stage.Capture(new Vector2(2.625f, -0.6f), new Vector2(6.5f, 2.5f), 1800, 700), dir, "finishes.png");

                // Ten expressions.
                stage.Clear();
                var expressions = (Expression[])Enum.GetValues(typeof(Expression));
                for (int i = 0; i < expressions.Length; i++)
                {
                    var at = new Vector2(i % 5 * 1.05f, -(i / 5) * 1.25f);
                    Tile(stage, d, at);
                    var view = Plop(stage, d, i % 3, at, expressions[i] == Expression.Sleeping);
                    if (expressions[i] != Expression.Sleeping) view.SetExpression(expressions[i]);
                    view.Tick(T);
                    Label(stage, expressions[i].ToString(), at + new Vector2(0f, -0.66f));
                }
                Save(stage.Capture(new Vector2(2.1f, -0.68f), new Vector2(5.5f, 2.75f), 1600, 800), dir, "expressions.png");

                // Accessories.
                stage.Clear();
                string[] accessories = { "bow", "flower", "leaf", "ribbon", "star", "nightcap", "cloud", "crown" };
                for (int i = 0; i < accessories.Length; i++)
                {
                    var look = d.With(accessory: CharacterArt.Accessories[accessories[i]]);
                    var at = new Vector2(i % 4 * 1.1f, -(i / 4) * 1.35f);
                    Tile(stage, look, at);
                    Plop(stage, look, i % 6, at);
                    Label(stage, Title(accessories[i]), at + new Vector2(0f, -0.66f));
                }
                Save(stage.Capture(new Vector2(1.65f, -0.62f), new Vector2(4.6f, 2.9f), 1400, 880), dir, "accessories.png");

                // Nest styles: empty, then with a sleeping Plop (identity seal stays visible on the corner).
                stage.Clear();
                string[] nests = { "classic", "cloud", "marshmallow", "flower", "moon" };
                for (int i = 0; i < nests.Length; i++)
                {
                    var look = d.With(nest: CharacterArt.Nests[nests[i]]);
                    var top = new Vector2(i * 1.1f, 0f);
                    var bottom = new Vector2(i * 1.1f, -1.15f);
                    Tile(stage, look, top);
                    Tile(stage, look, bottom);
                    Nest(stage, look, i % 6, top);
                    Nest(stage, look, i % 6, bottom);
                    Plop(stage, look, i % 6, bottom, asleep: true);
                    Label(stage, Title(nests[i]), bottom + new Vector2(0f, -0.66f));
                }
                Save(stage.Capture(new Vector2(2.2f, -0.65f), new Vector2(5.8f, 2.7f), 1700, 800), dir, "nests.png");

                // Palettes: six logical colours, each with its match mark.
                stage.Clear();
                string[] palettes = { "classic", "pastel", "sorbet" };
                for (int p = 0; p < palettes.Length; p++)
                for (int c = 0; c < 6; c++)
                {
                    var look = d.With(palette: CharacterArt.Palettes[palettes[p]]);
                    var at = new Vector2(c * 1.0f, -p * 1.05f);
                    Tile(stage, look, at);
                    Plop(stage, look, c, at);
                }
                Save(stage.Capture(new Vector2(2.5f, -1.05f), new Vector2(6.2f, 3.3f), 1800, 960), dir, "palettes.png");
            }

            Readability(dir);
            ShapeAtlas.ReleaseUnused(CharacterLook.Default);
            Debug.Log("[PLOP] Character reference sheets written to " + dir);
        }

        /// <summary>Each style at 24–96 px per cell, awake and asleep, on light and dark backgrounds.</summary>
        private static void Readability(string dir)
        {
            int[] sizes = { 24, 32, 48, 64, 96 };
            string[] styles = { "classic", "bouncy", "snoozy" };
            int sheetW = 0;
            foreach (int s in sizes) sheetW += s * 2 + 24;
            int rowH = 96 + 24;
            var sheet = new Texture2D(sheetW + 24, rowH * styles.Length * 2 + 24, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = (Color32)Color.Lerp(Light, Color.white, 0.4f);
            sheet.SetPixels32(fill);
            int y = sheet.height - 12;
            for (int bg = 0; bg < 2; bg++)
            {
                using (var stage = new CharacterStage(bg == 0 ? Light : Dark))
                foreach (var style in styles)
                {
                    y -= rowH;
                    int x = 12;
                    foreach (int px in sizes)
                    {
                        stage.Clear();
                        var look = CharacterLook.Default.With(style: CharacterArt.Styles[style]);
                        var atlas = ShapeAtlas.ForDensity(px, look);
                        Plop(stage, look, 1, Vector2.zero, false, atlas);
                        Nest(stage, look, 0, new Vector2(1f, 0f), atlas);
                        Plop(stage, look, 0, new Vector2(1f, 0f), true, atlas);
                        var tex = stage.Capture(new Vector2(0.5f, 0f), new Vector2(2f, 1f), px * 2, px);
                        sheet.SetPixels(x, y + (96 - px) / 2, tex.width, tex.height, tex.GetPixels());
                        UnityEngine.Object.DestroyImmediate(tex);
                        x += px * 2 + 24;
                    }
                }
            }
            sheet.Apply();
            Save(sheet, dir, "readability-sizes.png");
        }

        // ------------------------------------------------------------------ helpers

        private static PlopView Plop(CharacterStage stage, CharacterLook look, int color, Vector2 at, bool asleep = false, ShapeAtlas atlas = null)
        {
            atlas = atlas ?? ShapeAtlas.ForDensity(256, look);
            var holder = stage.Add("Plop");
            var view = new PlopView(holder.transform, atlas, color, 7 + color, false);
            view.SetSortingBase(0); // face, brows and accessory above the body, as on the board
            view.Snap(at, asleep);
            view.Tick(T);
            return view;
        }

        private static void Tile(CharacterStage stage, CharacterLook look, Vector2 at)
        {
            var r = stage.Add("Tile").AddComponent<SpriteRenderer>();
            r.sprite = ShapeAtlas.ForDensity(256, look).Get("tile");
            r.sortingOrder = -30;
            r.transform.localPosition = at;
        }

        private static void Nest(CharacterStage stage, CharacterLook look, int color, Vector2 at, ShapeAtlas atlas = null)
        {
            atlas = atlas ?? ShapeAtlas.ForDensity(256, look);
            var r = stage.Add("Nest").AddComponent<SpriteRenderer>();
            r.sprite = atlas.Get(ShapeAtlas.NestKey(color));
            r.sortingOrder = -20;
            r.transform.localPosition = at;
            var seal = stage.Add("Seal").AddComponent<SpriteRenderer>();
            seal.sprite = atlas.Get(ShapeAtlas.NestSealKey(color));
            seal.sortingOrder = 1000;
            seal.transform.localPosition = (Vector3)at + new Vector3(0.325f, 0.315f, 0f);
        }

        private static void Label(CharacterStage stage, string text, Vector2 at)
        {
            var go = stage.Add("Label " + text);
            go.transform.localPosition = at;
            var mesh = go.AddComponent<TextMesh>();
            var font = Resources.Load<Font>("Typography/Nunito-Text") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.022f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = HudInk;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 2000;
        }

        private static readonly Color HudInk = PlopPalette.Hex(0x633F55);

        private static string Title(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static void Save(Texture2D tex, string dir, string name)
        {
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
