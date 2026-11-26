using System;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Read-only pixels of one supplied art sprite, prepared once on the main thread so the atlas painters
    /// (which may run on a worker thread) can sample it without touching the Unity API.
    /// Coordinates: pixel (0,0) is the bottom-left corner; the sprite pivot is the canvas centre (the importer
    /// recentres every sprite on its anchor: tile centre, fan hub, ring hole).
    /// </summary>
    public sealed class ArtImage
    {
        private readonly Color32[] _px;
        public readonly int Width, Height;
        public readonly string Name;
        /// <summary>Opaque (alpha ≥ 128) bounding box in pixels.</summary>
        public readonly RectInt Content;
        /// <summary>Distance from the pivot to the farthest opaque pixel.</summary>
        public readonly float Radius;
        /// <summary>Distance from the pivot to the nearest opaque pixel (the hole of a ring; 0 if solid).</summary>
        public readonly float HoleRadius;
        /// <summary>Luminance at the 5th / 95th percentile of opaque pixels (for gradient mapping).</summary>
        public readonly float LumaLow, LumaHigh;
        /// <summary>Mean hue / saturation / value of the saturated (dyed) pixels, used as the recolour source.</summary>
        public readonly Vector3 DyeHsv;

        public float CenterX => Width * 0.5f;
        public float CenterY => Height * 0.5f;

        public ArtImage(string name, Color32[] pixels, int width, int height)
        {
            Name = name;
            _px = pixels;
            Width = width;
            Height = height;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            float cx = CenterX, cy = CenterY, far = 0f;
            var hist = new int[64];
            int opaque = 0;
            double sumS = 0, sumV = 0, sumX = 0, sumY = 0, weight = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = pixels[y * width + x];
                if (c.a < 128) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                far = Mathf.Max(far, Mathf.Sqrt(dx * dx + dy * dy));
                if (c.a < 200) continue;
                opaque++;
                hist[Mathf.Clamp((int)(Luma(c) * 63.99f), 0, 63)]++;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (s < 0.25f) continue;
                // Hue is circular: average it as a vector.
                double w = s;
                sumX += Math.Cos(h * Math.PI * 2) * w;
                sumY += Math.Sin(h * Math.PI * 2) * w;
                sumS += s * w;
                sumV += v * w;
                weight += w;
            }
            Content = maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            Radius = far;
            LumaLow = Percentile(hist, opaque, 0.05f);
            LumaHigh = Mathf.Max(LumaLow + 0.05f, Percentile(hist, opaque, 0.95f));
            if (weight > 0)
            {
                double hue = Math.Atan2(sumY, sumX) / (Math.PI * 2);
                if (hue < 0) hue += 1;
                DyeHsv = new Vector3((float)hue, (float)(sumS / weight), (float)(sumV / weight));
            }
            HoleRadius = MeasureHole();
        }

        private float MeasureHole()
        {
            float best = float.MaxValue;
            for (int k = 0; k < 72; k++)
            {
                float a = k * Mathf.PI * 2f / 72f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float hit = float.MaxValue;
                for (float r = 0f; r < Width * 0.5f; r += 0.5f)
                {
                    int x = (int)(CenterX + ca * r), y = (int)(CenterY + sa * r);
                    if (x < 0 || y < 0 || x >= Width || y >= Height) break;
                    if (_px[y * Width + x].a >= 128) { hit = r; break; }
                }
                best = Mathf.Min(best, hit);
            }
            return best == float.MaxValue ? 0f : best;
        }

        private static float Percentile(int[] hist, int total, float q)
        {
            if (total == 0) return q;
            int target = Mathf.RoundToInt(total * q), acc = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                acc += hist[i];
                if (acc >= target) return (i + 0.5f) / hist.Length;
            }
            return 1f;
        }

        public static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>Bilinear sample at a pixel-space position, filtered with premultiplied alpha (no halos).</summary>
        public Color Sample(float fx, float fy)
        {
            float u = fx - 0.5f, v = fy - 0.5f;
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float tx = u - x0, ty = v - y0;
            Vector4 a = Tap(x0, y0), b = Tap(x0 + 1, y0), c = Tap(x0, y0 + 1), d = Tap(x0 + 1, y0 + 1);
            var m = Vector4.Lerp(Vector4.Lerp(a, b, tx), Vector4.Lerp(c, d, tx), ty);
            return m.w <= 1e-4f ? new Color(0f, 0f, 0f, 0f) : new Color(m.x / m.w, m.y / m.w, m.z / m.w, m.w);
        }

        private Vector4 Tap(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return Vector4.zero;
            var c = _px[y * Width + x];
            float a = c.a / 255f;
            return new Vector4(c.r / 255f * a, c.g / 255f * a, c.b / 255f * a, a);
        }

        /// <summary>
        /// Sample in board units: (x, y) relative to the pivot, <paramref name="pxPerCell"/> art pixels per cell.
        /// Minification uses four taps when one output pixel covers more than ~1.3 art pixels.
        /// </summary>
        public Color At(float x, float y, float pxPerCell, float pixel)
        {
            float fx = CenterX + x * pxPerCell, fy = CenterY + y * pxPerCell;
            float footprint = pixel * pxPerCell;
            if (footprint <= 1.3f) return Sample(fx, fy);
            float o = footprint * 0.25f;
            var s = Pre(Sample(fx - o, fy - o)) + Pre(Sample(fx + o, fy - o)) + Pre(Sample(fx - o, fy + o)) + Pre(Sample(fx + o, fy + o));
            s *= 0.25f;
            return s.w <= 1e-4f ? new Color(0f, 0f, 0f, 0f) : new Color(s.x / s.w, s.y / s.w, s.z / s.w, s.w);
        }

        private static Vector4 Pre(Color c) => new Vector4(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
    }

    /// <summary>
    /// Phase 11.6: the supplied art sprites, loaded once (main thread) and shared by every atlas bake. When the
    /// pack is missing, every painter falls back to its procedural Phase 11.5 shape.
    /// </summary>
    public static class ArtPack
    {
        // Footprints in board cells (opaque width of each sprite on the board).
        public const float TileFootprint = ShapeAtlas.TileSize;     // 0.94: same 0.06 gap as before
        public const float PebbleFootprint = 0.90f;
        public const float NestFootprint = 0.92f;
        public const float FanFootprint = 0.92f;
        public const float PortalFootprint = 0.92f;
        /// <summary>Blade tips reach a little past the housing's ring hole, over the inner lip.</summary>
        public const float BladeReach = 1.06f;
        /// <summary>The vortex fills the frame's hole and tucks under the ring.</summary>
        public const float VortexReach = 1.12f;

        private static bool _loaded;
        private static ArtPackAsset _asset;

        public static ArtImage Floor, Pebble, Lava, Frost, FanHousing, FanBlades, PortalFrame, PortalVortex;
        /// <summary>Supplied Nest cushions: Berry, Mint, Honey, Lilac.</summary>
        public static ArtImage[] Nests = Array.Empty<ArtImage>();

        /// <summary>Tests and tools: draw the procedural Phase 11.5 shapes instead of the art pack.</summary>
        internal static bool ForceProcedural;

        public static bool Ready { get { Load(); return Painting; } }
        /// <summary>Thread-safe check used by the painters (never loads; <see cref="Load"/> runs on the main thread first).</summary>
        public static bool Painting => !ForceProcedural && Floor != null;
        public static ArtPackAsset Asset { get { Load(); return _asset; } }

        /// <summary>Loads the pack (main thread). Safe to call repeatedly.</summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _asset = Resources.Load<ArtPackAsset>(ArtPackAsset.Resource);
            if (_asset == null) return;
            Floor = Read(_asset.floor);
            Pebble = Read(_asset.pebble);
            Lava = Read(_asset.lava);
            Frost = Read(_asset.frost);
            FanHousing = Read(_asset.fanHousing);
            FanBlades = Read(_asset.fanBlades);
            PortalFrame = Read(_asset.portalFrame);
            PortalVortex = Read(_asset.portalVortex);
            var nests = new System.Collections.Generic.List<ArtImage>();
            foreach (var s in _asset.nests) { var n = Read(s); if (n != null) nests.Add(n); }
            Nests = nests.ToArray();
            if (Floor == null || Pebble == null || Lava == null || Frost == null || FanHousing == null || FanBlades == null
                || PortalFrame == null || PortalVortex == null || Nests.Length == 0)
            {
                Debug.LogWarning("[PLOP] Art pack incomplete: falling back to procedural board shapes.");
                Floor = null;
            }
        }

        private static ArtImage Read(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;
            var tex = sprite.texture;
            if (!tex.isReadable) { Debug.LogWarning("[PLOP] Art sprite not readable: " + sprite.name); return null; }
            return new ArtImage(sprite.name, tex.GetPixels32(), tex.width, tex.height);
        }

        /// <summary>Art pixels per board cell when the opaque width of <paramref name="art"/> spans <paramref name="footprint"/> cells.</summary>
        public static float Scale(ArtImage art, float footprint) => art.Content.width / footprint;

        /// <summary>Fan blade tip radius in cells (housing hole × <see cref="BladeReach"/>).</summary>
        public static float BladeRadiusCells => FanHousing.HoleRadius / Scale(FanHousing, FanFootprint) * BladeReach;
        public static float BladeScale => FanBlades.Radius / BladeRadiusCells;
        public static float VortexRadiusCells => PortalFrame.HoleRadius / Scale(PortalFrame, PortalFootprint) * VortexReach;
        public static float VortexScale => PortalVortex.Radius / VortexRadiusCells;

        /// <summary>The supplied Nest whose dye is closest in hue to <paramref name="target"/>.</summary>
        public static ArtImage NestFor(Color target)
        {
            Color.RGBToHSV(target, out float h, out _, out _);
            ArtImage best = Nests[0];
            float bestD = float.MaxValue;
            foreach (var n in Nests)
            {
                float d = HueDistance(n.DyeHsv.x, h);
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        public static float HueDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1f - d);
        }

        /// <summary>
        /// Re-dyes a sampled art pixel: dyed (saturated) areas move from the source dye to <paramref name="target"/>,
        /// neutral details (ivory crescents, highlights, stitching shadows) keep their own colour.
        /// </summary>
        public static Color Redye(Color c, Vector3 sourceHsv, Vector3 targetHsv)
        {
            if (c.a <= 0f) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float dyed = Mathf.Clamp01((s - 0.10f) / 0.14f);
            if (dyed <= 0f) return c;
            float dh = targetHsv.x - sourceHsv.x;
            float nh = h + dh;
            nh -= Mathf.Floor(nh);
            float ns = Mathf.Clamp01(s * (targetHsv.y / Mathf.Max(0.05f, sourceHsv.y)));
            float nv = Mathf.Clamp01(v * (targetHsv.z / Mathf.Max(0.05f, sourceHsv.z)));
            var shifted = Color.HSVToRGB(nh, ns, nv);
            shifted.a = c.a;
            return Color.Lerp(c, shifted, dyed);
        }

        public static Vector3 Hsv(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return new Vector3(h, s, v);
        }

        /// <summary>Maps an art pixel's luminance onto a three-stop ramp (shade → base → light).</summary>
        public static Color GradientMap(Color c, ArtImage art, Color shade, Color mid, Color light)
        {
            float t = Mathf.Clamp01((ArtImage.Luma(c) - art.LumaLow) / (art.LumaHigh - art.LumaLow));
            var m = t < 0.5f ? Color.Lerp(shade, mid, t * 2f) : Color.Lerp(mid, light, (t - 0.5f) * 2f);
            m.a = c.a;
            return m;
        }
    }
}
