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
    public sealed class ShapeAtlas
    {
        // ---- geometry constants (fractions of one cell) ----
        public const float TileSize = 0.92f;            // floor tile edge (gap = 0.08)
        public const float TileRadius = 0.19f * TileSize;   // 19% of tile width
        public const float PebbleSize = 0.86f;
        public const float PebbleRadius = 0.22f * PebbleSize;  // 22% of block width
        public const float NestSize = 0.88f;
        public const float NestRadius = 0.24f * NestSize;
        public const float PanelRadius = 0.40f;          // board container corner radius (world units)
        public const float PanelShadowBlur = 0.22f;

        private static readonly int[] Buckets = { 64, 96, 128, 160, 192, 256 };
        private static readonly Dictionary<int, ShapeAtlas> Cache = new Dictionary<int, ShapeAtlas>();

        public int PixelsPerUnit { get; }
        public Texture2D Texture { get; }
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        /// <summary>Returns the cached atlas for the bucket closest to the given on-screen density.</summary>
        public static ShapeAtlas ForDensity(float pixelsPerUnit)
        {
            int bucket = Bucket(pixelsPerUnit);
            if (!Cache.TryGetValue(bucket, out var atlas) || atlas.Texture == null)
            {
                atlas = new ShapeAtlas(bucket);
                Cache[bucket] = atlas;
            }
            return atlas;
        }

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

        // ============================================================================================

        private delegate Color Painter(float x, float y, float pixel);

        private sealed class Spec
        {
            public string Key;
            public int W, H;                // pixels
            public Vector2 Pivot;           // normalized
            public Vector4 Border;          // 9-slice border in pixels (L, B, R, T)
            public float X0, Y0;            // world-space coordinate of the bottom-left pixel corner (relative to pivot)
            public Painter Paint;
            public Color[] Pixels;
            public RectInt Slot;
        }

        private ShapeAtlas(int ppu)
        {
            PixelsPerUnit = ppu;
            var specs = new List<Spec>();
            AddBoardShapes(specs);
            for (int c = 0; c < PlopPalette.SlotCount; c++) { AddNest(specs, c); AddPlop(specs, c); }

            foreach (var s in specs) Rasterize(s, ppu);
            int width = ppu >= 192 ? 2048 : 1024;
            int height = Pack(specs, width);
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PLOP ShapeAtlas " + ppu,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var all = new Color32[width * height];
            foreach (var s in specs)
                for (int y = 0; y < s.H; y++)
                    for (int x = 0; x < s.W; x++)
                        all[(s.Slot.y + y) * width + s.Slot.x + x] = s.Pixels[y * s.W + x];
            Texture.SetPixels32(all);
            Texture.Apply(false, true); // upload, then free the CPU copy

            foreach (var s in specs)
            {
                var sprite = Sprite.Create(Texture, new Rect(s.Slot.x, s.Slot.y, s.W, s.H), s.Pivot, ppu, 0,
                    SpriteMeshType.FullRect, s.Border);
                sprite.name = s.Key;
                _sprites[s.Key] = sprite;
                s.Pixels = null;
            }
        }

        /// <summary>Simple shelf packer with 2 px padding. Returns the power-of-two atlas height.</summary>
        private static int Pack(List<Spec> specs, int width)
        {
            var order = new List<Spec>(specs);
            order.Sort((a, b) => b.H.CompareTo(a.H));
            int x = 1, y = 1, shelf = 0;
            foreach (var s in order)
            {
                if (x + s.W + 1 > width) { x = 1; y += shelf + 2; shelf = 0; }
                s.Slot = new RectInt(x, y, s.W, s.H);
                x += s.W + 2;
                shelf = Mathf.Max(shelf, s.H);
            }
            int needed = y + shelf + 1, h = 64;
            while (h < needed) h *= 2;
            return h;
        }

        private static void Rasterize(Spec s, int ppu)
        {
            float pixel = 1f / ppu;
            s.Pixels = new Color[s.W * s.H];
            for (int py = 0; py < s.H; py++)
            {
                float y = s.Y0 + (py + 0.5f) * pixel;
                for (int px = 0; px < s.W; px++)
                {
                    float x = s.X0 + (px + 0.5f) * pixel;
                    s.Pixels[py * s.W + px] = s.Paint(x, y, pixel);
                }
            }
        }

        /// <summary>Spec covering [x0, x0+w] × [y0, y0+h] world units, with the pivot at world (0,0).</summary>
        private Spec Make(string key, float x0, float y0, float w, float h, Painter paint)
        {
            int pw = Mathf.CeilToInt(w * PixelsPerUnit), ph = Mathf.CeilToInt(h * PixelsPerUnit);
            return new Spec
            {
                Key = key, W = pw, H = ph, X0 = x0, Y0 = y0, Paint = paint,
                Pivot = new Vector2(-x0 * PixelsPerUnit / pw, -y0 * PixelsPerUnit / ph)
            };
        }

        // ================================== shapes ==================================

        private void AddBoardShapes(List<Spec> specs)
        {
            // Floor tile: soft rounded square with a whisper of vertical shading.
            float th = TileSize * 0.5f;
            specs.Add(Make("tile", -0.5f, -0.5f, 1f, 1f, (x, y, px) =>
            {
                var c = PlopPalette.Tile.WithAlpha(0f);
                float d = TileDistance(x, y);
                var fill = Color.Lerp(PlopPalette.TileShade, PlopPalette.Tile, Mathf.SmoothStep(0f, 1f, (y + th) / (TileSize * 0.7f)));
                return Sdf.Over(c, fill, Sdf.Coverage(d, px));
            }));

            // Pebble (wall): toy block with a darker side, soft tinted drop shadow and a gentle top highlight.
            float ph = PebbleSize * 0.5f;
            specs.Add(Make("pebble", -0.5f, -0.72f, 1f, 1.22f, (x, y, px) =>
            {
                var c = PlopPalette.PebbleSide.WithAlpha(0f);
                c = Sdf.Over(c, PlopPalette.Shadow, 0.22f * Sdf.Soft(Sdf.RoundBox(x, y, 0, -0.13f, ph, ph * 0.92f, PebbleRadius), 0.09f));
                c = Sdf.Over(c, PlopPalette.PebbleSide, Sdf.Coverage(Sdf.RoundBox(x, y, 0, -0.06f, ph, ph, PebbleRadius), px));
                float top = PebbleTopDistance(x, y);
                var topColor = Color.Lerp(PlopPalette.PebbleTop, PlopPalette.PebbleHighlight, Mathf.Clamp01((y + 0.1f) / 0.55f) * 0.55f);
                c = Sdf.Over(c, topColor, Sdf.Coverage(top, px));
                c = Sdf.Over(c, PlopPalette.Highlight, 0.45f * Sdf.Soft(Sdf.Ellipse(x, y, -0.12f, 0.24f, 0.15f, 0.045f), 0.035f) * Sdf.Coverage(top, px));
                return c;
            }));

            // Plop ground shadow (tinted, soft).
            specs.Add(Make("plopShadow", -0.42f, -0.16f, 0.84f, 0.32f, (x, y, px) =>
                Sdf.Over(PlopPalette.Shadow.WithAlpha(0f), PlopPalette.Shadow, 0.26f * Sdf.Soft(Sdf.Ellipse(x, y, 0, 0, 0.29f, 0.075f), 0.07f))));

            // Eyes: Ink ovals with a small warm glint; closed eyes are soft lower arcs.
            specs.Add(Make("eye", -0.07f, -0.08f, 0.14f, 0.16f, (x, y, px) =>
            {
                var c = PlopPalette.Ink.WithAlpha(0f);
                float d = Sdf.Ellipse(x, y, 0, 0, 0.040f, 0.054f);
                c = Sdf.Over(c, PlopPalette.Ink, Sdf.Coverage(d, px));
                c = Sdf.Over(c, PlopPalette.Highlight, 0.95f * Sdf.Coverage(Sdf.Circle(x, y, 0.013f, 0.021f, 0.0135f), px));
                return c;
            }));
            specs.Add(Make("eyeClosed", -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(PlopPalette.Ink.WithAlpha(0f), PlopPalette.Ink, Sdf.Coverage(Sdf.LowerArc(x, y, 0, 0.022f, 0.042f, 1.05f, 0.0105f), px))));
            specs.Add(Make("cheek", -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(PlopPalette.Berry.WithAlpha(0f), Color.Lerp(PlopPalette.Berry, PlopPalette.Ink, 0.08f), 0.55f * Sdf.Soft(Sdf.Ellipse(x, y, 0, 0, 0.045f, 0.027f), 0.022f))));

            // Board container: 9-sliced rounded panel + its soft shadow. Border = corner radius.
            int rPx = Mathf.CeilToInt(PanelRadius * PixelsPerUnit);
            float size = (2 * rPx + 2) / (float)PixelsPerUnit;
            var panel = Make("panel", -size * 0.5f, -size * 0.5f, size, size, (x, y, px) =>
                Sdf.Over(PlopPalette.BoardPanel.WithAlpha(0f), PlopPalette.BoardPanel,
                    Sdf.Coverage(Sdf.RoundBox(x, y, 0, 0, size * 0.5f, size * 0.5f, PanelRadius), px)));
            panel.Border = new Vector4(rPx, rPx, rPx, rPx);
            specs.Add(panel);

            int sPx = Mathf.CeilToInt((PanelRadius + PanelShadowBlur) * PixelsPerUnit);
            float shadowSize = (2 * sPx + 2) / (float)PixelsPerUnit;
            float inner = shadowSize * 0.5f - PanelShadowBlur;
            var panelShadow = Make("panelShadow", -shadowSize * 0.5f, -shadowSize * 0.5f, shadowSize, shadowSize, (x, y, px) =>
                Sdf.Over(PlopPalette.Shadow.WithAlpha(0f), PlopPalette.Shadow,
                    Sdf.Soft(Sdf.RoundBox(x, y, 0, 0, inner, inner, PanelRadius), PanelShadowBlur)));
            panelShadow.Border = new Vector4(sPx, sPx, sPx, sPx);
            specs.Add(panelShadow);
        }

        /// <summary>Nest: a pillow — rounded outer cushion, softer inner well, subtle rim and top highlight.</summary>
        private void AddNest(List<Spec> specs, int colorId)
        {
            Color outer = PlopPalette.NestOuter(colorId), innerColor = PlopPalette.NestInner(colorId);
            Color rim = Color.Lerp(outer, PlopPalette.Ink, 0.10f);
            float h = NestSize * 0.5f;
            specs.Add(Make(NestKey(colorId), -0.5f, -0.5f, 1f, 1f, (x, y, px) =>
            {
                var c = outer.WithAlpha(0f);
                float d = Sdf.RoundBox(x, y, 0, 0, h, h, NestRadius);
                var cushion = Color.Lerp(rim, outer, Mathf.Clamp01(-d / 0.045f));
                c = Sdf.Over(c, cushion, Sdf.Coverage(d, px));
                float well = Sdf.RoundBox(x, y, 0, -0.015f, h * 0.62f, h * 0.58f, NestRadius * 0.62f);
                var wellColor = Color.Lerp(innerColor, Color.Lerp(innerColor, outer, 0.35f), Mathf.Clamp01((0.18f - y) / 0.4f));
                c = Sdf.Over(c, wellColor, Sdf.Soft(well, 0.03f));
                c = Sdf.Over(c, PlopPalette.Highlight, 0.35f * Sdf.Soft(Sdf.Ellipse(x, y, -0.10f, 0.30f, 0.16f, 0.035f), 0.03f) * Sdf.Coverage(d, px));
                return c;
            }));
        }

        /// <summary>
        /// Plop body: a soft gumdrop (smooth union of a rounded base and a dome), lighter on top, slightly
        /// deeper at the rim, gently translucent, with a warm specular highlight. Pivot = ground contact point.
        /// </summary>
        private void AddPlop(List<Spec> specs, int colorId)
        {
            Color body = PlopPalette.PlopBody(colorId);
            Color top = Color.Lerp(body, PlopPalette.Milk, 0.30f);
            Color bottom = Color.Lerp(body, PlopPalette.Ink, 0.10f);
            Color rim = Color.Lerp(body, PlopPalette.Ink, 0.16f);
            specs.Add(Make(PlopKey(colorId), -0.48f, -0.06f, 0.96f, 0.86f, (x, y, px) =>
            {
                float d = BodyDistance(x, y);
                var c = body.WithAlpha(0f);
                var fill = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, (y - 0.02f) / 0.66f));
                fill = Color.Lerp(rim, fill, Mathf.Clamp01(-d / 0.05f));       // soft deeper rim
                fill.a = Mathf.Lerp(1f, 0.93f, Mathf.Clamp01(-d / 0.12f));     // slightly translucent jelly core
                c = Sdf.Over(c, fill, Sdf.Coverage(d, px));
                float inside = Sdf.Coverage(d, px);
                c = Sdf.Over(c, PlopPalette.Highlight, 0.78f * inside * Sdf.Soft(Sdf.Ellipse(x, y, -0.15f, 0.53f, 0.085f, 0.05f), 0.025f));
                c = Sdf.Over(c, PlopPalette.Highlight, 0.65f * inside * Sdf.Coverage(Sdf.Circle(x, y, -0.04f, 0.60f, 0.018f), px));
                return c;
            }));
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
            float baseShape = Sdf.RoundBox(x, y, 0f, 0.22f, 0.38f, 0.22f, 0.20f);
            float dome = Sdf.Circle(x, y, 0f, 0.37f, 0.31f);
            return Sdf.SmoothMin(baseShape, dome, 0.10f);
        }
    }
}
