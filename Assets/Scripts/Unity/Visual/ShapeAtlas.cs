using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Runtime-generated sprite atlas holding every board shape (rounded tiles, pebbles, nests, Plop bodies,
    /// eyes, shadows, 9-sliced board panel). Shapes are rasterized from signed distance functions with a
    /// one-pixel anti-aliased edge, so corners are genuinely round (not blurred) at any screen density:
    /// the atlas resolution is chosen from the on-screen pixels per grid cell and cached per size bucket.
    /// All sprites share one texture, so the board renders in very few batches.
    ///
    /// Corner radii are fractions of the cell size (1 world unit), so they scale with the board.
    /// </summary>
    public sealed partial class ShapeAtlas
    {
        // ---- geometry constants (fractions of one cell) ----
        public const float TileSize = 0.94f;            // 0.06 cell gap between ivory pieces
        public const float TileRadius = 0.20f * TileSize;
        public const float PebbleSize = 0.89f;
        public const float PebbleRadius = 0.245f * PebbleSize;
        public const float NestSize = 0.90f;
        public const float NestRadius = 0.245f * NestSize;
        public const float PanelRadius = 0.49f;          // board container corner radius (world units)
        public const float PanelShadowBlur = 0.20f;
        public const string WorriedPlopKey = "plopWorried";

        private static readonly int[] Buckets = { 64, 96, 128, 160, 192, 256 };
        private static readonly Dictionary<string, ShapeAtlas> Cache = new Dictionary<string, ShapeAtlas>(StringComparer.Ordinal);
        private static readonly int[] AllColors = { 0, 1, 2, 3, 4, 5 };

        /// <summary>What an atlas contains. Gameplay atlases hold everything; Studio previews only characters.</summary>
        [Flags]
        public enum Content { Board = 1, Characters = 2, Nests = 4, All = Board | Characters | Nests }

        public int PixelsPerUnit { get; }
        public Texture2D Texture { get; }
        public CharacterLook Look { get; }
        /// <summary>Milliseconds spent rasterizing this atlas (profiling).</summary>
        public float BakeMilliseconds { get; }
        public long TextureBytes => Texture == null ? 0 : (long)Texture.width * Texture.height * 4;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        /// <summary>Returns the cached default-look atlas for the bucket closest to the given on-screen density.</summary>
        public static ShapeAtlas ForDensity(float pixelsPerUnit) => ForDensity(pixelsPerUnit, CharacterLook.Default);

        /// <summary>Returns the cached atlas for a density bucket and cosmetic look.</summary>
        public static ShapeAtlas ForDensity(float pixelsPerUnit, CharacterLook look)
        {
            look = look ?? CharacterLook.Default;
            int bucket = Bucket(pixelsPerUnit);
            string key = bucket + "|" + look.Key;
            if (!Cache.TryGetValue(key, out var atlas) || atlas.Texture == null)
            {
                atlas = new ShapeAtlas(bucket, look, Content.All, AllColors);
                Cache[key] = atlas;
            }
            return atlas;
        }

        /// <summary>An uncached atlas owned by the caller (Studio previews). Call <see cref="Dispose"/> when done.</summary>
        public static ShapeAtlas Build(int pixelsPerUnit, CharacterLook look, Content content, params int[] colors)
            => new ShapeAtlas(pixelsPerUnit, look ?? CharacterLook.Default, content, colors.Length == 0 ? AllColors : colors);

        /// <summary>Destroys cached atlases of other looks (the default look is always kept for tools and tests).</summary>
        public static int ReleaseUnused(CharacterLook keep)
        {
            var remove = new List<string>();
            foreach (var kv in Cache)
            {
                string look = kv.Value.Look?.Key;
                if (look == keep?.Key || look == CharacterLook.Default.Key) continue;
                kv.Value.Dispose();
                remove.Add(kv.Key);
            }
            foreach (var k in remove) Cache.Remove(k);
            return remove.Count;
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            _sprites.Clear();
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
        }

        public bool Has(string key) => _sprites.ContainsKey(key);

        public static int Bucket(float pixelsPerUnit)
        {
            foreach (int b in Buckets) if (b >= pixelsPerUnit * 0.9f) return b;
            return Buckets[Buckets.Length - 1];
        }

        public Sprite Get(string key)
        {
            if (_sprites.TryGetValue(key, out var s)) return s;
            throw new KeyNotFoundException("Shape '" + key + "' not in atlas.");
        }

        public static string NestKey(int colorId) => "nest" + colorId;
        public static string PlopKey(int colorId) => "plop" + colorId;
        public static string NestSealKey(int colorId) => "nestSeal" + colorId;

        // ============================================================================================

        private delegate Color Painter(float x, float y, float pixel);

        private sealed class Spec
        {
            public string Key;
            public int W, H;                // pixels
            public int Density;
            public Vector2 Pivot;           // normalized
            public Vector4 Border;          // 9-slice border in pixels (L, B, R, T)
            public float X0, Y0;            // world-space coordinate of the bottom-left pixel corner (relative to pivot)
            public Painter Paint;
            public RectInt Slot;
        }

        /// <summary>Painter host only: exposes <see cref="Make"/> at a density without rasterizing anything.</summary>
        private ShapeAtlas(int ppu)
        {
            PixelsPerUnit = ppu;
        }

        private ShapeAtlas(int ppu, CharacterLook look, Content content, int[] colors, Action<List<Spec>> extra = null)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            PixelsPerUnit = ppu;
            Look = look;
            var specs = new List<Spec>();
            if ((content & Content.Board) != 0)
            {
                AddBoardShapes(specs);
                AddMechanicTiles(specs);
                for (int c = 0; c < PlopPalette.SlotCount; c++) AddNestSeal(specs, c);
                AddWorriedPlop(specs);
            }
            if ((content & Content.Characters) != 0) AddCharacterParts(specs, look, colors);
            if ((content & Content.Nests) != 0)
                foreach (int c in colors) AddStyledNest(specs, NestKey(c), look.Nest, look.Palette, c);
            extra?.Invoke(specs);

            int width = ppu >= 192 ? 2048 : 1024;
            int height = Pack(specs, width);
            var all = new Color32[width * height];
            foreach (var spec in specs) Rasterize(spec, all, width);
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PLOP ShapeAtlas " + ppu,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Texture.SetPixels32(all);
            Texture.Apply(false, true); // upload, then free the CPU copy

            foreach (var s in specs)
            {
                var sprite = Sprite.Create(Texture, new Rect(s.Slot.x, s.Slot.y, s.W, s.H), s.Pivot, s.Density, 0,
                    SpriteMeshType.FullRect, s.Border);
                sprite.name = s.Key;
                _sprites[s.Key] = sprite;
            }
            // Earlier key names stay valid for existing tools and tests.
            Alias("eye", EyeKey(EyeShape.Open));
            Alias("eyeClosed", EyeKey(EyeShape.Closed));
            Alias("mouth", MouthKey(MouthShape.Neutral));
            Alias("mouthSleep", MouthKey(MouthShape.Sleep));
            Alias("mouthBump", MouthKey(MouthShape.Open));
            BakeMilliseconds = (float)clock.Elapsed.TotalMilliseconds;
        }

        private void Alias(string alias, string key)
        {
            if (_sprites.TryGetValue(key, out var sprite) && !_sprites.ContainsKey(alias)) _sprites[alias] = sprite;
        }

        /// <summary>Packs sprites into the first shelf with room and leaves two pixels between shapes.</summary>
        private static int Pack(List<Spec> specs, int width)
        {
            var order = new List<Spec>(specs);
            order.Sort((a, b) => b.H.CompareTo(a.H));
            var shelves = new List<(int NextX, int Y)>();
            int nextY = 1;
            foreach (var spec in order)
            {
                // The descending height order means all existing shelves are tall enough.
                int slot = 0;
                while (slot < shelves.Count && shelves[slot].NextX + spec.W + 1 > width) slot++;
                if (slot == shelves.Count)
                {
                    shelves.Add((1, nextY));
                    nextY += spec.H + 2;
                }
                var shelf = shelves[slot];
                spec.Slot = new RectInt(shelf.NextX, shelf.Y, spec.W, spec.H);
                shelves[slot] = (shelf.NextX + spec.W + 2, shelf.Y);
            }

            int height = 64;
            while (height < nextY - 1) height *= 2;
            return height;
        }

        private static void Rasterize(Spec spec, Color32[] atlas, int width)
        {
            float pixel = 1f / spec.Density;
            for (int py = 0; py < spec.H; py++)
            {
                float y = spec.Y0 + (py + 0.5f) * pixel;
                int start = (spec.Slot.y + py) * width + spec.Slot.x;
                for (int px = 0; px < spec.W; px++)
                {
                    float x = spec.X0 + (px + 0.5f) * pixel;
                    atlas[start + px] = spec.Paint(x, y, pixel);
                }
            }
        }

        /// <summary>Spec covering [x0, x0+w] × [y0, y0+h] world units, with the pivot at world (0,0).</summary>
        private Spec Make(string key, float x0, float y0, float w, float h, Painter paint, int maxDensity = int.MaxValue)
        {
            int density = Mathf.Min(PixelsPerUnit, maxDensity);
            int pw = Mathf.CeilToInt(w * density), ph = Mathf.CeilToInt(h * density);
            return new Spec
            {
                Key = key, W = pw, H = ph, Density = density, X0 = x0, Y0 = y0, Paint = paint,
                Pivot = new Vector2(-x0 * density / pw, -y0 * density / ph)
            };
        }

        // ================================== shapes ==================================

        private void AddBoardShapes(List<Spec> specs)
        {
            specs.Add(Make("tile", -0.5f, -0.5f, 1f, 1f, PaintTile));
            specs.Add(Make("pebble", -0.5f, -0.61f, 1f, 1.11f, PaintPebble));
            AddTray(specs);
        }

        private static Color PaintTile(float x, float y, float pixel)
        {
            float half = TileSize * 0.5f;
            Color warmBase = Color.Lerp(PlopPalette.TileShade, PlopPalette.ContactShadow, 0.36f);
            float contact = Sdf.RoundBox(x, y, 0.014f, -0.021f, half - 0.016f, half - 0.016f, TileRadius);
            var c = Sdf.Over(warmBase.WithAlpha(0f), PlopPalette.ContactShadow, 0.29f * Sdf.Soft(contact, 0.027f));

            float baseDistance = TileDistance(x, y);
            float inside = Sdf.Coverage(baseDistance, pixel);
            c = Sdf.Over(c, warmBase, inside);

            // A slightly raised cake of satin ivory. The lower side stays visible beneath its bevel.
            const float faceY = 0.020f;
            float faceHalfHeight = half - 0.023f;
            float face = Sdf.RoundBox(x, y, 0f, faceY, half - 0.008f, faceHalfHeight, TileRadius);
            float faceAlpha = Sdf.Coverage(face, pixel);
            if (faceAlpha <= 0f) return c;
            float diffuse = Smooth((0.40f + 0.72f * y - 0.32f * x) / 0.93f);
            var ivory = Color.Lerp(PlopPalette.TileShade, PlopPalette.Tile, 0.63f + 0.29f * diffuse);
            float edgeLight = RoundedEdgeLight(x, y - faceY, half - 0.008f, faceHalfHeight, TileRadius);
            var edge = Color.Lerp(Color.Lerp(warmBase, ivory, 0.48f), PlopPalette.Highlight, 0.13f + 0.84f * edgeLight);
            var glazed = Color.Lerp(edge, ivory, Smooth(-face / 0.061f));
            return Sdf.Over(c, glazed, faceAlpha);
        }

        private static Color PaintPebble(float x, float y, float pixel)
        {
            float half = PebbleSize * 0.5f;
            var sideColor = Color.Lerp(PlopPalette.PebbleSide, PlopPalette.Shadow, 0.24f);
            float contact = Sdf.RoundBox(x, y, 0.025f, -0.105f, half - 0.018f, half * 0.95f, PebbleRadius);
            var c = Sdf.Over(PaintTile(x, y, pixel), PlopPalette.Shadow, 0.40f * Sdf.Soft(contact, 0.047f));

            float side = Sdf.RoundBox(x, y, 0.013f, -0.071f, half, half * 0.95f, PebbleRadius);
            var skirt = Color.Lerp(sideColor, PlopPalette.PebbleSide, Smooth((0.20f - x + 0.23f * y) / 0.65f));
            c = Sdf.Over(c, skirt, Sdf.Coverage(side, pixel));

            float face = PebbleTopDistance(x, y);
            float inside = Sdf.Coverage(face, pixel);
            if (inside <= 0f) return c;
            float faceLight = RoundedEdgeLight(x, y, half, half * 0.93f, PebbleRadius);
            float diffuse = Smooth(0.50f + 0.68f * y - 0.46f * x);
            var satin = Color.Lerp(PlopPalette.PebbleTop, PlopPalette.PebbleHighlight, 0.72f * diffuse);
            var bevel = Color.Lerp(sideColor, PlopPalette.Highlight, 0.18f + 0.61f * faceLight);
            var fill = Color.Lerp(bevel, satin, Smooth(-face / 0.096f));

            float reflection = Sdf.Ellipse(x + (y - 0.195f) * 0.24f, y, -0.13f, 0.195f, 0.22f, 0.098f);
            fill = Color.Lerp(fill, PlopPalette.Highlight, 0.24f * Sdf.Soft(reflection, 0.083f));
            // Slightly irregular pinholes suggest a molded pebble without noisy high-frequency texture.
            float localX = Mathf.Abs(x + y * 0.10f) - 0.142f;
            float localY = Mathf.Abs(y + 0.016f + x * 0.07f) - 0.143f;
            float pit = Sdf.Circle(localX, localY, 0f, 0f, 0.031f);
            float pitLight = Smooth(0.50f + 13f * localX - 11f * localY);
            var pitFace = Color.Lerp(sideColor, PlopPalette.PebbleHighlight, 0.53f + 0.36f * pitLight);
            fill = Color.Lerp(fill, pitFace, 0.66f * Sdf.Soft(pit, 0.012f));
            return Sdf.Over(c, fill, inside);
        }

        private void AddTray(List<Spec> specs)
        {
            // Include the rim's complete depth in the border. The stretchable nine-slice center is flat.
            int borderPx = Mathf.CeilToInt((PanelRadius + 0.14f) * PixelsPerUnit);
            float panelSize = (2 * borderPx + 2) / (float)PixelsPerUnit;
            var panel = Make("panel", -panelSize * 0.5f, -panelSize * 0.5f, panelSize, panelSize,
                (x, y, px) => PaintTray(x, y, px, panelSize * 0.5f));
            panel.Border = new Vector4(borderPx, borderPx, borderPx, borderPx);
            specs.Add(panel);

            int shadowPx = Mathf.CeilToInt((PanelRadius + PanelShadowBlur) * PixelsPerUnit);
            float shadowSize = (2 * shadowPx + 2) / (float)PixelsPerUnit;
            float inner = shadowSize * 0.5f - PanelShadowBlur;
            var shadow = Make("panelShadow", -shadowSize * 0.5f, -shadowSize * 0.5f, shadowSize, shadowSize,
                (x, y, px) => PlopPalette.ContactShadow.WithAlpha(
                    Sdf.Soft(Sdf.RoundBox(x, y, 0f, 0f, inner, inner, PanelRadius), PanelShadowBlur)));
            shadow.Border = new Vector4(shadowPx, shadowPx, shadowPx, shadowPx);
            specs.Add(shadow);
        }

        private static Color PaintTray(float x, float y, float pixel, float half)
        {
            var baseColor = Color.Lerp(PlopPalette.BoardPanel, PlopPalette.ContactShadow, 0.23f);
            float foot = Sdf.RoundBox(x, y, 0f, 0f, half, half, PanelRadius);
            float footInside = Sdf.Coverage(foot, pixel);
            if (footInside <= 0f) return baseColor.WithAlpha(0f);
            var c = Color.Lerp(baseColor, PlopPalette.TrayRim, Smooth((y + half) / 0.10f) * 0.73f).WithAlpha(footInside);

            const float lift = 0.039f;
            float topHalfWidth = half - 0.007f;
            float topHalfHeight = half - lift - 0.006f;
            float top = Sdf.RoundBox(x, y, 0f, lift, topHalfWidth, topHalfHeight, PanelRadius);
            float topInside = Sdf.Coverage(top, pixel);
            if (topInside <= 0f) return c;
            float lit = RoundedEdgeLight(x, y - lift, topHalfWidth, topHalfHeight, PanelRadius);
            var rim = Color.Lerp(PlopPalette.TrayRim, PlopPalette.Highlight, 0.50f + 0.45f * lit);
            var bed = PlopPalette.BoardPanel;
            float inward = -top;
            var lip = Color.Lerp(Color.Lerp(baseColor, bed, 0.36f), rim, Smooth(inward / 0.047f));
            // The outer rim rolls down into a shaded groove and ends in the tray's flat bed.
            lip = Color.Lerp(lip, Color.Lerp(baseColor, bed, 0.29f + lit * 0.39f), Smooth((inward - 0.090f) / 0.065f));
            lip = Color.Lerp(lip, bed, Smooth((inward - 0.153f) / 0.071f));
            return Sdf.Over(c, lip, topInside);
        }

        private static Color PaintEye(float x, float y, float pixel)
        {
            Color ink = PlopPalette.FaceInk;
            float outline = Sdf.Ellipse(x, y, 0f, 0f, 0.040f, 0.058f);
            var shell = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(outline, pixel));
            return Sdf.Over(shell, PlopPalette.Highlight,
                0.98f * Sdf.Coverage(Sdf.Circle(x, y, -0.013f, 0.021f, 0.0155f), pixel));
        }

        private static Painter PillowPainter(Color cover, Color lining)
        {
            var binding = Color.Lerp(cover, PlopPalette.Ink, 0.34f);
            var piping = Color.Lerp(cover, PlopPalette.Highlight, 0.70f);
            return (x, y, px) =>
            {
                float softContact = PillowDistance(x - 0.020f, y + 0.046f);
                var c = Sdf.Over(cover.WithAlpha(0f), PlopPalette.ContactShadow, 0.36f * Sdf.Soft(softContact, 0.038f));
                float d = PillowDistance(x, y);
                float inside = Sdf.Coverage(d, px);
                if (inside <= 0f) return c;
                float light = RoundedEdgeLight(x, y, NestSize * 0.5f, NestSize * 0.46f, NestRadius);
                var hem = Color.Lerp(binding, piping, 0.14f + 0.66f * light);
                var cushion = Color.Lerp(hem, cover, Smooth(-d / 0.073f));

                // Four softly stuffed corners sit on the stitched perimeter of the cushion.
                float tuftX = Mathf.Abs(x) - 0.29f;
                float tuftY = Mathf.Abs(y) - 0.252f;
                float poof = Sdf.Ellipse(tuftX + tuftY * 0.31f, tuftY - tuftX * 0.31f, 0f, 0f, 0.155f, 0.089f);
                float crease = Sdf.Coverage(poof, 0.057f);
                float crest = Sdf.Soft(Sdf.Ellipse(x, y, -0.10f, 0.23f, 0.38f, 0.18f), 0.12f);
                cushion = Color.Lerp(cushion, binding, crease * 0.22f);
                cushion = Color.Lerp(cushion, piping, (0.22f + 0.57f * crest) * Smooth(-poof / 0.083f));

                float seam = 1f - Smooth(Mathf.Abs(d + 0.024f) / 0.014f);
                cushion = Color.Lerp(cushion, Color.Lerp(binding, piping, light), 0.41f * seam);
                float center = Sdf.RoundBox(x, y, 0f, 0.008f, 0.299f, 0.268f, 0.138f);
                float bounce = Smooth(0.55f - 1.31f * y + 0.80f * x);
                var innerHem = Color.Lerp(binding, cover, 0.16f + 0.68f * bounce);
                var seat = Color.Lerp(cover, lining, Smooth(0.61f + 1.03f * y - 0.37f * x));
                var inset = Color.Lerp(innerHem, seat, Smooth(-center / 0.084f));
                cushion = Color.Lerp(cushion, inset, Sdf.Soft(center, 0.025f));

                // The ivory crescent is sculpted into the sprite. Each Nest stays a single renderer.
                float moonShadow = CrescentDistance(x - 0.012f, y + 0.024f);
                cushion = Color.Lerp(cushion, binding, 0.48f * Sdf.Soft(moonShadow, 0.025f));
                float moon = CrescentDistance(x, y);
                var moonEdge = Color.Lerp(PlopPalette.TileShade, PlopPalette.Highlight, Smooth(0.55f + 4f * y - 2.95f * x));
                var moonFace = Color.Lerp(moonEdge, PlopPalette.Tile, Smooth(-moon / 0.023f));
                cushion = Color.Lerp(cushion, moonFace, Sdf.Coverage(moon, px));
                return Sdf.Over(c, cushion, inside);
            };
        }

        private static float CrescentDistance(float x, float y)
        {
            float face = Sdf.Circle(x, y, -0.007f, 0.005f, 0.147f);
            float cutout = Sdf.Circle(x, y, 0.088f, 0.087f, 0.125f);
            return -Sdf.SmoothMin(-face, cutout, 0.031f);
        }

        private static float PillowDistance(float x, float y)
        {
            float pinchX = 0.033f * Smooth(1f - Mathf.Abs(y) / 0.38f);
            float pinchY = 0.035f * Smooth(1f - Mathf.Abs(x) / 0.40f);
            return Sdf.RoundBox(x, y, 0f, 0f, NestSize * 0.5f - pinchX, NestSize * 0.46f - pinchY, NestRadius);
        }

        // Matching seals stay visible on a pillow's corner when another Plop crosses it.
        // The same shape is pressed into the lower body sprite, leaving the face unobstructed.
        private void AddNestSeal(List<Spec> specs, int colorId)
        {
            var low = Color.Lerp(PlopPalette.ContactShadow, PlopPalette.TileShade, 0.42f);
            specs.Add(Make(NestSealKey(colorId), -0.145f, -0.145f, 0.29f, 0.29f, (x, y, px) =>
            {
                float edge = Sdf.Circle(x, y, 0f, 0f, 0.112f);
                float shade = Sdf.Soft(Sdf.Circle(x, y, 0.007f, -0.012f, 0.112f), 0.023f);
                var c = low.WithAlpha(0.53f * shade);
                var rim = Color.Lerp(PlopPalette.TileShade, PlopPalette.Highlight,
                    Smooth(0.58f + y * 5.2f - x * 2.8f));
                var ceramic = Color.Lerp(rim, PlopPalette.SealCream, Smooth(-edge / 0.025f));
                ceramic = Color.Lerp(ceramic, PlopPalette.SealInk, SealInkCoverage(colorId, x, y, 0.075f, px));
                return Sdf.Over(c, ceramic, Sdf.Coverage(edge, px));
            }, maxDensity: 128));
        }

        private static float SealInkCoverage(int colorId, float x, float y, float scale, float pixel)
            => Sdf.Coverage(MatchMarkDistance(colorId, x / scale, y / scale) * scale, pixel);

        /// <summary>Normalized outlines for the six color-independent match marks.</summary>
        internal static float MatchMarkDistance(int colorId, float x, float y)
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            switch (colorId)
            {
                case 0: // heart
                    return Sdf.SmoothMin(Sdf.Circle(ax, y, 0.30f, 0.22f, 0.40f),
                        TaperedTip(ax, -y, -0.31f, 0.79f, 0.69f), 0.08f);
                case 1: // drop
                    return Sdf.SmoothMin(Sdf.Circle(x, y, 0f, -0.23f, 0.56f),
                        TaperedTip(ax, y, -0.24f, 0.85f, 0.51f), 0.10f);
                case 2: // four-point sparkle
                    return Mathf.Sqrt(ax) + Mathf.Sqrt(ay) - 0.94f;
                case 3: // leaf
                    float u = x * 0.77f - y * 0.64f;
                    float v = y * 0.77f + x * 0.64f;
                    return Mathf.Max(Sdf.Circle(u, v, -0.46f, 0f, 0.90f),
                                     Sdf.Circle(u, v, 0.46f, 0f, 0.90f));
                case 4: // diamond
                    return (ax + ay * 0.91f - 0.79f) * 0.71f;
                default: // four-petal flower
                    return Sdf.SmoothMin(Sdf.Circle(ax, ay, 0.275f, 0.275f, 0.33f),
                        Sdf.Circle(x, y, 0f, 0f, 0.39f), 0.09f);
            }
        }

        private static float TaperedTip(float x, float y, float bottom, float tip, float halfWidth)
            => Mathf.Max(bottom - y, x - halfWidth * (tip - y) / (tip - bottom));

        private void AddWorriedPlop(List<Spec> specs)
        {
            var material = new JellyBody(CharacterArt.Styles["classic"], CharacterArt.Finishes["classic"], CharacterArt.Palettes["classic"], 0);
            var blush = Color.Lerp(PlopPalette.Berry, PlopPalette.Ink, 0.13f);
            specs.Add(Make(WorriedPlopKey, -0.49f, -0.10f, 0.98f, 0.90f, (x, y, px) =>
            {
                float ground = ContactFalloff(x - 0.024f, y - 0.005f, 0.407f, 0.092f);
                var c = PlopPalette.ContactShadow.WithAlpha(0.29f * ground);
                c = Sdf.Over(c, material.Paint(x, y, px), 1f);
                float cheek = Sdf.Ellipse(Mathf.Abs(x + 0.006f), y, 0.224f, 0.287f, 0.056f, 0.028f);
                c = Sdf.Over(c, blush, 0.64f * Sdf.Soft(cheek, 0.026f));
                c = Sdf.Over(c, PaintEye(Mathf.Abs(x + 0.008f) - 0.119f, (y - 0.394f) * 1.08f, px), 1f);
                float brow = Sdf.LowerArc(Mathf.Abs(x + 0.008f) - 0.119f, -y, 0.012f, -0.487f, 0.026f, 0.77f, 0.008f);
                float frown = Sdf.LowerArc(x, -y, -0.009f, -0.246f, 0.030f, 1.03f, 0.013f);
                return Sdf.Over(c, PlopPalette.FaceInk, 0.88f * Sdf.Coverage(Mathf.Min(brow, frown), px));
            }, maxDensity: 128));
        }

        // The normalized radius reaches zero opacity before the sprite bounds on both axes.
        private static float ContactFalloff(float x, float y, float radiusX, float radiusY)
        {
            float nx = x / radiusX;
            float ny = y / radiusY;
            return 1f - Smooth(nx * nx + ny * ny);
        }

        // Outward bevel normals face a single upper-left key light. Flat faces use diffuse gradients.
        private static float RoundedEdgeLight(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(x) - halfWidth + radius;
            float qy = Mathf.Abs(y) - halfHeight + radius;
            float nx = Mathf.Max(qx, 0f);
            float ny = Mathf.Max(qy, 0f);
            float length = Mathf.Sqrt(nx * nx + ny * ny);
            if (length > 0.0001f)
            {
                nx /= length;
                ny /= length;
            }
            else
            {
                nx = qx > qy ? 1f : 0f;
                ny = 1f - nx;
            }
            float light = -0.57f * nx * Mathf.Sign(x) + 0.82f * ny * Mathf.Sign(y);
            return Mathf.Clamp01(0.5f + 0.5f * light);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Distance to the floor tile outline (units, origin = cell center). Used by the painter and tests.</summary>
        public static float TileDistance(float x, float y)
            => Sdf.RoundBox(x, y, 0f, 0f, TileSize * 0.5f, TileSize * 0.5f, TileRadius);

        /// <summary>Distance to the pebble's top face (units, origin = cell center).</summary>
        public static float PebbleTopDistance(float x, float y)
            => Sdf.RoundBox(x, y, 0f, 0f, PebbleSize * 0.5f, PebbleSize * 0.5f * 0.93f, PebbleRadius);

        /// <summary>Distance to the Plop silhouette (units, origin = ground center).</summary>
        public static float BodyDistance(float x, float y)
        {
            // The dome leans slightly left. Three joined feet compress into a subtly scalloped skirt.
            float dome = Sdf.Ellipse(x + Mathf.Max(0f, y - 0.42f) * 0.10f, y, -0.022f, 0.329f, 0.371f, 0.382f);
            dome = -Sdf.SmoothMin(-dome, y - 0.067f, 0.051f);
            float center = Sdf.Ellipse(x, y, -0.003f, 0.130f, 0.249f, 0.129f);
            float left = Sdf.Ellipse(x, y, -0.273f, 0.150f, 0.124f, 0.137f);
            float right = Sdf.Ellipse(x, y, 0.269f, 0.156f, 0.128f, 0.140f);
            float skirt = Sdf.SmoothMin(center, Mathf.Min(left, right), 0.041f);
            return Sdf.SmoothMin(dome, skirt, 0.056f);
        }
    }
}
