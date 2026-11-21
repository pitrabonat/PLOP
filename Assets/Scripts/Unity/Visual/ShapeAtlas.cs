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
        public const float PanelRadius = 0.46f;          // board container corner radius (world units)
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
            public RectInt Slot;
        }

        private ShapeAtlas(int ppu)
        {
            PixelsPerUnit = ppu;
            var specs = new List<Spec>();
            AddBoardShapes(specs);
            AddFaceShapes(specs);
            for (int c = 0; c < PlopPalette.SlotCount; c++)
            {
                AddNest(specs, c);
                AddPlop(specs, c);
            }

            int width = ppu >= 192 ? 2048 : 1024;
            int height = Pack(specs, width);
            var all = new Color32[width * height];
            foreach (var spec in specs) Rasterize(spec, ppu, all, width);
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
                var sprite = Sprite.Create(Texture, new Rect(s.Slot.x, s.Slot.y, s.W, s.H), s.Pivot, ppu, 0,
                    SpriteMeshType.FullRect, s.Border);
                sprite.name = s.Key;
                _sprites[s.Key] = sprite;
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

        private static void Rasterize(Spec spec, int ppu, Color32[] atlas, int width)
        {
            float pixel = 1f / ppu;
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
            // Floor tiles have a molded lip and a satin center. The edge and gap stay on the same grid.
            float tileHalf = TileSize * 0.5f;
            var tileEdge = Color.Lerp(PlopPalette.TileShade, PlopPalette.BoardPanel, 0.35f);
            specs.Add(Make("tile", -0.5f, -0.5f, 1f, 1f, (x, y, px) =>
            {
                float d = TileDistance(x, y);
                float inside = Sdf.Coverage(d, px);
                if (inside <= 0f) return PlopPalette.Tile.WithAlpha(0f);
                float lit = Mathf.SmoothStep(0f, 1f, (y + tileHalf) / TileSize);
                var face = Color.Lerp(PlopPalette.TileShade, PlopPalette.Tile, Mathf.Lerp(0.58f, 1f, lit));
                var edge = Color.Lerp(tileEdge, PlopPalette.Highlight, lit * 0.68f);
                var fill = Color.Lerp(edge, face, Mathf.SmoothStep(0f, 1f, -d / 0.057f));
                return Sdf.Over(fill.WithAlpha(0f), fill, inside);
            }));

            // A rounded pebble with a visible lower side and a broad reflected highlight on top.
            float pebbleHalf = PebbleSize * 0.5f;
            specs.Add(Make("pebble", -0.5f, -0.67f, 1f, 1.17f, (x, y, px) =>
            {
                var c = PlopPalette.PebbleSide.WithAlpha(0f);
                float contact = Sdf.RoundBox(x, y, 0f, -0.14f, pebbleHalf, pebbleHalf * 0.92f, PebbleRadius);
                c = Sdf.Over(c, PlopPalette.Shadow, 0.22f * Sdf.Soft(contact, 0.085f));
                float side = Sdf.RoundBox(x, y, 0f, -0.068f, pebbleHalf, pebbleHalf, PebbleRadius);
                c = Sdf.Over(c, PlopPalette.PebbleSide, Sdf.Coverage(side, px));

                float d = PebbleTopDistance(x, y);
                float inside = Sdf.Coverage(d, px);
                if (inside <= 0f) return c;
                float lit = Mathf.SmoothStep(0f, 1f, (y + pebbleHalf) / PebbleSize);
                var fill = Color.Lerp(PlopPalette.PebbleTop, PlopPalette.PebbleHighlight, 0.68f * lit);
                var edge = Color.Lerp(PlopPalette.PebbleSide, PlopPalette.Highlight, 0.65f * lit);
                fill = Color.Lerp(edge, fill, Mathf.SmoothStep(0f, 1f, -d / 0.072f));
                c = Sdf.Over(c, fill, inside);
                float sheen = Sdf.Ellipse(x, y, -0.13f, 0.23f, 0.19f, 0.075f);
                return Sdf.Over(c, PlopPalette.Highlight, 0.34f * inside * Sdf.Soft(sheen, 0.065f));
            }));

            // The board's flat center and outer lip stretch separately across the 9-slice.
            int rPx = Mathf.CeilToInt(PanelRadius * PixelsPerUnit);
            float size = (2 * rPx + 2) / (float)PixelsPerUnit;
            var panelEdge = Color.Lerp(PlopPalette.BoardPanel, PlopPalette.PebbleSide, 0.22f);
            var panel = Make("panel", -size * 0.5f, -size * 0.5f, size, size, (x, y, px) =>
            {
                float d = Sdf.RoundBox(x, y, 0f, 0f, size * 0.5f, size * 0.5f, PanelRadius);
                float lit = Mathf.SmoothStep(0f, 1f, y / size + 0.5f);
                var edge = Color.Lerp(panelEdge, PlopPalette.Highlight, 0.70f * lit);
                var fill = Color.Lerp(edge, PlopPalette.BoardPanel, Mathf.SmoothStep(0f, 1f, -d / 0.105f));
                return Sdf.Over(fill.WithAlpha(0f), fill, Sdf.Coverage(d, px));
            });
            panel.Border = new Vector4(rPx, rPx, rPx, rPx);
            specs.Add(panel);

            int sPx = Mathf.CeilToInt((PanelRadius + PanelShadowBlur) * PixelsPerUnit);
            float shadowSize = (2 * sPx + 2) / (float)PixelsPerUnit;
            float inner = shadowSize * 0.5f - PanelShadowBlur;
            var panelShadow = Make("panelShadow", -shadowSize * 0.5f, -shadowSize * 0.5f, shadowSize, shadowSize, (x, y, px) =>
                Sdf.Over(PlopPalette.Shadow.WithAlpha(0f), PlopPalette.Shadow,
                    Sdf.Soft(Sdf.RoundBox(x, y, 0f, 0f, inner, inner, PanelRadius), PanelShadowBlur)));
            panelShadow.Border = new Vector4(sPx, sPx, sPx, sPx);
            specs.Add(panelShadow);
        }

        private void AddFaceShapes(List<Spec> specs)
        {
            specs.Add(Make("plopShadow", -0.42f, -0.16f, 0.84f, 0.32f, (x, y, px) =>
                Sdf.Over(PlopPalette.Shadow.WithAlpha(0f), PlopPalette.Shadow,
                    0.28f * Sdf.Soft(Sdf.Ellipse(x, y, 0f, 0f, 0.28f, 0.068f), 0.068f))));

            specs.Add(Make("eye", -0.07f, -0.08f, 0.14f, 0.16f, (x, y, px) =>
            {
                var ink = PlopPalette.Ink;
                var c = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.Ellipse(x, y, 0f, 0f, 0.034f, 0.050f), px));
                return Sdf.Over(c, PlopPalette.Highlight, 0.96f * Sdf.Coverage(Sdf.Circle(x, y, -0.010f, 0.021f, 0.013f), px));
            }));
            specs.Add(Make("eyeClosed", -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(PlopPalette.Ink.WithAlpha(0f), PlopPalette.Ink,
                    Sdf.Coverage(Sdf.LowerArc(x, y, 0f, 0.020f, 0.043f, 1.1f, 0.012f), px))));

            var cheek = Color.Lerp(PlopPalette.Berry, PlopPalette.Ink, 0.10f);
            specs.Add(Make("cheek", -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(cheek.WithAlpha(0f), cheek, 0.60f * Sdf.Soft(Sdf.Ellipse(x, y, 0f, 0f, 0.047f, 0.025f), 0.024f))));

            var mouth = PlopPalette.Ink.WithAlpha(0.91f);
            specs.Add(Make("mouth", -0.075f, -0.052f, 0.15f, 0.104f, (x, y, px) =>
                Sdf.Over(mouth.WithAlpha(0f), mouth,
                    Sdf.Coverage(Sdf.LowerArc(x, y, 0f, 0.015f, 0.034f, 1f, 0.0115f), px))));
            specs.Add(Make("mouthSleep", -0.06f, -0.05f, 0.12f, 0.10f, (x, y, px) =>
                Sdf.Over(mouth.WithAlpha(0f), mouth,
                    Sdf.Coverage(Sdf.Ellipse(x, y, 0f, -0.006f, 0.017f, 0.012f), px))));
            specs.Add(Make("mouthBump", -0.06f, -0.05f, 0.12f, 0.10f, (x, y, px) =>
                Sdf.Over(mouth.WithAlpha(0f), mouth,
                    Sdf.Coverage(Sdf.Ellipse(x, y, 0f, 0f, 0.020f, 0.026f), px))));
        }

        /// <summary>A soft pillow with a rounded hem and a shallow inset for the sleeping Plop.</summary>
        private void AddNest(List<Spec> specs, int colorId)
        {
            Color outer = PlopPalette.NestOuter(colorId), innerColor = PlopPalette.NestInner(colorId);
            Color piping = Color.Lerp(outer, PlopPalette.Ink, 0.16f);
            Color reflected = Color.Lerp(outer, PlopPalette.Highlight, 0.70f);
            float h = NestSize * 0.5f;
            specs.Add(Make(NestKey(colorId), -0.5f, -0.5f, 1f, 1f, (x, y, px) =>
            {
                var c = outer.WithAlpha(0f);
                float contact = Sdf.RoundBox(x, y, 0f, -0.035f, h - 0.016f, h - 0.024f, NestRadius);
                c = Sdf.Over(c, PlopPalette.Shadow, 0.24f * Sdf.Soft(contact, 0.040f));

                float d = Sdf.RoundBox(x, y, 0f, 0f, h, h, NestRadius);
                float inside = Sdf.Coverage(d, px);
                if (inside <= 0f) return c;
                float light = Mathf.SmoothStep(0f, 1f, (y + h) / NestSize);
                var cushion = Color.Lerp(piping, reflected, light * 0.65f);
                c = Sdf.Over(c, Color.Lerp(cushion, outer, Mathf.SmoothStep(0f, 1f, -d / 0.080f)), inside);

                float well = Sdf.RoundBox(x, y, 0f, -0.015f, h * 0.64f, h * 0.59f, NestRadius * 0.66f);
                float wellLight = Mathf.SmoothStep(0f, 1f, (0.20f - y) / 0.42f);
                var inset = Color.Lerp(Color.Lerp(outer, piping, 0.30f), Color.Lerp(innerColor, reflected, wellLight * 0.30f),
                    Mathf.SmoothStep(0f, 1f, -well / 0.067f));
                c = Sdf.Over(c, inset, Sdf.Soft(well, 0.022f));
                float highlight = Sdf.Ellipse(x, y, -0.10f, 0.314f, 0.17f, 0.043f);
                return Sdf.Over(c, PlopPalette.Highlight, 0.44f * inside * Sdf.Soft(highlight, 0.044f));
            }));
        }

        /// <summary>Shared jelly shape with baked rim, inner light and glaze for each palette slot.</summary>
        private void AddPlop(List<Spec> specs, int colorId)
        {
            Color body = PlopPalette.PlopBody(colorId);
            Color bright = Color.Lerp(body, PlopPalette.Milk, 0.33f);
            Color rim = Color.Lerp(body, PlopPalette.Ink, 0.23f);
            Color bounce = Color.Lerp(body, PlopPalette.Honey, 0.17f);
            specs.Add(Make(PlopKey(colorId), -0.48f, -0.06f, 0.96f, 0.86f, (x, y, px) =>
            {
                float d = BodyDistance(x, y);
                float inside = Sdf.Coverage(d, px);
                var c = body.WithAlpha(0f);
                if (inside <= 0f) return c;
                float light = Mathf.SmoothStep(0f, 1f, (y + 0.1f) / 0.72f);
                var fill = Color.Lerp(Color.Lerp(body, rim, 0.34f), bright, light);
                var edge = Color.Lerp(rim, bright, Mathf.Lerp(0.20f, 0.80f, light));
                fill = Color.Lerp(edge, fill, Mathf.SmoothStep(0f, 1f, -d / 0.058f));
                fill.a = Mathf.Lerp(0.99f, 0.89f, Mathf.Clamp01(-d / 0.13f));
                c = Sdf.Over(c, fill, inside);

                float innerLight = Sdf.Ellipse(x, y, 0.065f, 0.16f, 0.21f, 0.091f);
                c = Sdf.Over(c, bounce, 0.40f * inside * Sdf.Soft(innerLight, 0.104f));
                float softbox = Sdf.Ellipse(x + (y - 0.54f) * 0.32f, y, -0.160f, 0.535f, 0.125f, 0.062f);
                c = Sdf.Over(c, PlopPalette.Highlight, 0.40f * inside * Sdf.Soft(softbox, 0.055f));
                float glaze = Sdf.Ellipse(x + (y - 0.54f) * 0.40f, y, -0.181f, 0.554f, 0.079f, 0.033f);
                c = Sdf.Over(c, PlopPalette.Highlight, 0.78f * inside * Sdf.Soft(glaze, 0.019f));
                return Sdf.Over(c, PlopPalette.Highlight,
                    0.69f * inside * Sdf.Coverage(Sdf.Circle(x, y, -0.063f, 0.604f, 0.017f), px));
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
            // Weight sits on the broad foot. The crown leans slightly as if the jelly has relaxed.
            float baseShape = Sdf.RoundBox(x, y, 0.012f, 0.202f, 0.373f, 0.202f, 0.184f);
            float crown = Sdf.Ellipse(x + Mathf.Max(0f, y - 0.28f) * 0.17f, y, -0.043f, 0.353f, 0.303f, 0.347f);
            return Sdf.SmoothMin(baseShape, crown, 0.095f);
        }
    }
}
