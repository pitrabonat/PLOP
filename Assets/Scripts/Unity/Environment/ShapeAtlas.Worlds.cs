using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 11.5 World board materials: a small per-World atlas with the tray (board frame), floor tile and
    /// pebble painted in that World's finish (see <see cref="BoardMaterialProfile"/>). Geometry matches the shared
    /// board shapes exactly, so grid positions never change. Pixels are rasterized on a worker thread; only the
    /// texture upload happens on the main thread, so changing World never hitches the board.
    /// </summary>
    public sealed partial class ShapeAtlas
    {
        public const string WorldTileKey = "tile";
        public const string WorldPebbleKey = "pebble";
        public const string WorldPanelKey = "panel";

        private static readonly Dictionary<string, ShapeAtlas> WorldCache = new Dictionary<string, ShapeAtlas>(StringComparer.Ordinal);
        private static readonly Dictionary<string, WorldBake> WorldBakes = new Dictionary<string, WorldBake>(StringComparer.Ordinal);

        public string WorldId { get; private set; }

        /// <summary>
        /// The World surface atlas for a profile at a density bucket, or null while it is still baking
        /// (call again next frame). <paramref name="synchronous"/> bakes on the calling thread (tools, first frame).
        /// Only the two most recently used World atlases are kept.
        /// </summary>
        public static ShapeAtlas WorldSurfaces(BoardMaterialProfile profile, int bucket, bool synchronous = false)
        {
            if (profile == null) return null;
            string key = profile.Id + "|" + bucket;
            if (WorldCache.TryGetValue(key, out var ready) && ready.Texture != null) return ready;
            if (!WorldBakes.TryGetValue(key, out var bake))
            {
                bake = new WorldBake(bucket, profile);
                WorldBakes[key] = bake;
                if (synchronous) bake.Run();
                else bake.Start();
            }
            else if (synchronous && !bake.IsDone) bake.Wait();
            if (!bake.IsDone) return null;
            WorldBakes.Remove(key);
            if (bake.Failed) return null;
            var atlas = bake.Finish();
            TrimWorldCache(key);
            WorldCache[key] = atlas;
            return atlas;
        }

        private static readonly List<string> WorldOrder = new List<string>();

        private static void TrimWorldCache(string newest)
        {
            WorldOrder.Remove(newest);
            WorldOrder.Add(newest);
            while (WorldOrder.Count > 2)
            {
                string old = WorldOrder[0];
                WorldOrder.RemoveAt(0);
                if (WorldCache.TryGetValue(old, out var a)) a.Release();
                WorldCache.Remove(old);
            }
        }

        /// <summary>Dispose that also works outside Play Mode (capture tools bake every World in turn).</summary>
        private void Release()
        {
            if (Application.isPlaying) { Dispose(); return; }
            foreach (var sprite in _sprites.Values) if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            _sprites.Clear();
            if (Texture != null) UnityEngine.Object.DestroyImmediate(Texture);
        }

        /// <summary>Total bytes of World surface textures currently held (profiling).</summary>
        public static long WorldSurfaceBytes
        {
            get { long n = 0; foreach (var a in WorldCache.Values) n += a.TextureBytes; return n; }
        }

        private sealed class WorldBake
        {
            private readonly ShapeAtlas _host;
            private readonly List<Spec> _specs = new List<Spec>();
            private readonly int _width, _height;
            private readonly string _id;
            private Color32[] _pixels;
            private Task _task;
            private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();

            public bool IsDone => _pixels != null || (_task != null && _task.IsCompleted);
            public bool Failed => _task != null && _task.IsFaulted;

            public WorldBake(int ppu, BoardMaterialProfile p)
            {
                _id = p.Id;
                _host = new ShapeAtlas(ppu);
                var profile = p;
                _specs.Add(_host.Make(WorldTileKey, -0.5f, -0.5f, 1f, 1f, (x, y, px) => PaintWorldTile(x, y, px, profile)));
                _specs.Add(_host.Make(WorldPebbleKey, -0.5f, -0.61f, 1f, 1.11f, (x, y, px) => PaintWorldPebble(x, y, px, profile)));
                int borderPx = Mathf.CeilToInt((PanelRadius + 0.14f) * ppu);
                float panelSize = (2 * borderPx + 2) / (float)ppu;
                var panel = _host.Make(WorldPanelKey, -panelSize * 0.5f, -panelSize * 0.5f, panelSize, panelSize,
                    (x, y, px) => PaintWorldTray(x, y, px, panelSize * 0.5f, profile));
                panel.Border = new Vector4(borderPx, borderPx, borderPx, borderPx);
                _specs.Add(panel);
                _width = 1024;
                _height = Pack(_specs, _width);
            }

            public void Start()
            {
                _clock.Start();
                _task = Task.Run(Rasterize);
            }

            public void Run()
            {
                _clock.Start();
                Rasterize();
            }

            public void Wait() => _task?.Wait();

            private void Rasterize()
            {
                var all = new Color32[_width * _height];
                foreach (var spec in _specs) ShapeAtlas.Rasterize(spec, all, _width);
                _pixels = all;
            }

            public ShapeAtlas Finish() => new ShapeAtlas(_host.PixelsPerUnit, _specs, _width, _height, _pixels,
                "PLOP World " + _id + " " + _host.PixelsPerUnit, _id, (float)_clock.Elapsed.TotalMilliseconds);
        }

        /// <summary>Upload-only constructor for atlases rasterized elsewhere (World surfaces).</summary>
        private ShapeAtlas(int ppu, List<Spec> specs, int width, int height, Color32[] pixels, string name, string worldId, float bakeMs)
        {
            PixelsPerUnit = ppu;
            Look = CharacterLook.Default;
            WorldId = worldId;
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            Texture.SetPixels32(pixels);
            Texture.Apply(false, true);
            foreach (var s in specs)
            {
                var sprite = Sprite.Create(Texture, new Rect(s.Slot.x, s.Slot.y, s.W, s.H), s.Pivot, s.Density, 0, SpriteMeshType.FullRect, s.Border);
                sprite.name = s.Key;
                _sprites[s.Key] = sprite;
            }
            BakeMilliseconds = bakeMs;
        }

        // ======================================================== painters (thread-safe: no Unity API)

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Speckles: sparse small dots on a jittered grid.</summary>
        private static float Speckle(float x, float y, float cell, float radius, float density, int seed)
        {
            float gx = x / cell, gy = y / cell;
            int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
            float best = 0f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = ix + i, cy = iy + j;
                if (Hash(cx, cy, seed) > density) continue;
                float px = (cx + 0.2f + 0.6f * Hash(cx, cy, seed + 1)) * cell;
                float py = (cy + 0.2f + 0.6f * Hash(cx, cy, seed + 2)) * cell;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                best = Mathf.Max(best, 1f - Smooth((d - radius * 0.5f) / (radius * 0.8f)));
            }
            return best;
        }

        /// <summary>The floor tile with a World finish. Same outline as <see cref="PaintTile"/>.</summary>
        private static Color PaintWorldTile(float x, float y, float pixel, BoardMaterialProfile p)
        {
            var c = PaintTile(x, y, pixel);
            if (c.a <= 0f) return c;
            float face = Sdf.RoundBox(x, y, 0f, 0.020f, TileSize * 0.5f - 0.008f, TileSize * 0.5f - 0.023f, TileRadius);
            float inFace = Smooth(-face / 0.05f);
            float k = p.tileDetail;
            switch (p.TileFinish)
            {
                case TileFinish.Speckle:
                {
                    float dark = Speckle(x, y, 0.055f, 0.011f, 0.55f, 11);
                    float light = Speckle(x + 0.3f, y, 0.07f, 0.012f, 0.35f, 23);
                    c = Color.Lerp(c, Color.Lerp(c, PlopPalette.ContactShadow, 0.55f), dark * 0.45f * k * inFace);
                    c = Color.Lerp(c, PlopPalette.Highlight, light * 0.35f * k * inFace);
                    break;
                }
                case TileFinish.Frosted:
                {
                    // Cool sheen from the upper left and powdery frost in the corners.
                    float sheen = Smooth(0.5f + 1.4f * (y - x));
                    c = Color.Lerp(c, PlopPalette.Hex(0xEEF6FC), 0.28f * k * sheen * inFace);
                    float corner = Smooth((Mathf.Abs(x) + Mathf.Abs(y) - 0.52f) / 0.12f);
                    float powder = ValueNoise(x * 38f, y * 38f, 5);
                    c = Color.Lerp(c, PlopPalette.Hex(0xF7FBFF), corner * (0.45f + 0.55f * powder) * 0.55f * k * inFace);
                    break;
                }
                case TileFinish.Glazed:
                {
                    // A glossy glaze streak across the upper-left of the face.
                    float u = (x - y) * 0.7071f, v = (x + y) * 0.7071f;
                    float streak = 1f - Smooth(Mathf.Abs(v + 0.18f) / 0.05f);
                    streak *= Smooth(0.5f - u * 2.2f);
                    c = Color.Lerp(c, PlopPalette.Highlight, 0.45f * k * streak * inFace);
                    break;
                }
                default:
                {
                    float grain = ValueNoise(x * 18f, y * 18f, 3) - 0.5f;
                    c = Color.Lerp(c, grain > 0 ? PlopPalette.Highlight : PlopPalette.TileShade, Mathf.Abs(grain) * 0.18f * k * inFace);
                    break;
                }
            }
            return c;
        }

        /// <summary>Board frame with a World finish. Features follow the rim (they survive 9-slice stretching).</summary>
        private static Color PaintWorldTray(float x, float y, float pixel, float half, BoardMaterialProfile p)
        {
            var baseColor = Color.Lerp(p.TrayBase, PlopPalette.ContactShadow, 0.23f);
            float foot = Sdf.RoundBox(x, y, 0f, 0f, half, half, PanelRadius);
            float footInside = Sdf.Coverage(foot, pixel);
            if (footInside <= 0f) return baseColor.WithAlpha(0f);
            var c = Color.Lerp(baseColor, p.TrayRim, Smooth((y + half) / 0.10f) * 0.73f).WithAlpha(footInside);

            const float lift = 0.039f;
            float topHalfWidth = half - 0.007f;
            float topHalfHeight = half - lift - 0.006f;
            float top = Sdf.RoundBox(x, y, 0f, lift, topHalfWidth, topHalfHeight, PanelRadius);
            float topInside = Sdf.Coverage(top, pixel);
            if (topInside <= 0f) return c;
            float lit = RoundedEdgeLight(x, y - lift, topHalfWidth, topHalfHeight, PanelRadius);
            var rimTone = Color.Lerp(p.TrayRim, PlopPalette.Highlight, 0.50f + 0.45f * lit);
            var bed = p.TrayBed;
            float inward = -top;
            var lip = Color.Lerp(Color.Lerp(baseColor, bed, 0.36f), rimTone, Smooth(inward / 0.047f));
            lip = Color.Lerp(lip, Color.Lerp(baseColor, bed, 0.29f + lit * 0.39f), Smooth((inward - 0.090f) / 0.065f));
            float rimZone = 1f - Smooth((inward - 0.13f) / 0.08f); // 1 on the rim, 0 on the flat bed

            switch (p.TrayFinish)
            {
                case TrayFinish.Wood:
                {
                    // Grain runs along the rim: concentric bands with a little wobble.
                    float g = Mathf.Sin(inward * 120f + 2.2f * Mathf.Sin(inward * 31f)) * 0.5f + 0.5f;
                    float ring = Smooth((g - 0.55f) / 0.2f);
                    lip = Color.Lerp(lip, p.TrayAccent, 0.52f * ring * rimZone);
                    lip = Color.Lerp(lip, Color.Lerp(lip, PlopPalette.Highlight, 0.4f), 0.25f * (1f - g) * rimZone * lit);
                    break;
                }
                case TrayFinish.Stone:
                {
                    // Layered cooled stone with an amber glow where the rim meets the bed.
                    float strata = Mathf.Sin(inward * 95f + Mathf.Sin(inward * 23f) * 1.6f) * 0.5f + 0.5f;
                    lip = Color.Lerp(lip, Color.Lerp(lip, PlopPalette.Shadow, 0.5f), 0.22f * strata * rimZone);
                    float glow = Mathf.Exp(-Mathf.Pow((inward - 0.125f) / 0.016f, 2f));
                    lip = Color.Lerp(lip, p.TrayAccent, 0.70f * glow);
                    break;
                }
                case TrayFinish.Frost:
                {
                    // Frosted outer edge, an icy inner line and a cool gradient.
                    float rimFrost = 1f - Smooth(inward / 0.035f);
                    lip = Color.Lerp(lip, p.TrayAccent, 0.55f * rimFrost);
                    float icyLine = Mathf.Exp(-Mathf.Pow((inward - 0.10f) / 0.010f, 2f));
                    lip = Color.Lerp(lip, p.TrayAccent, 0.45f * icyLine);
                    lip = Color.Lerp(lip, PlopPalette.Hex(0xA9C6DE), 0.18f * (1f - lit) * rimZone);
                    break;
                }
                case TrayFinish.Glass:
                {
                    // Glossy highlight line and an iridescent shift across the rim.
                    float spec = Mathf.Exp(-Mathf.Pow((inward - 0.030f) / 0.009f, 2f)) * lit;
                    float hue = Smooth(0.5f + (x - y) * 0.6f);
                    lip = Color.Lerp(lip, Color.Lerp(p.TrayRim, p.TrayAccent, hue), 0.35f * rimZone);
                    lip = Color.Lerp(lip, Color.white, 0.75f * spec);
                    break;
                }
                case TrayFinish.Ceramic:
                {
                    // Glaze: a deep pooled colour in the groove and a crisp specular line on the crest.
                    float pool = Mathf.Exp(-Mathf.Pow((inward - 0.105f) / 0.03f, 2f));
                    lip = Color.Lerp(lip, Color.Lerp(p.TrayBase, PlopPalette.Shadow, 0.25f), 0.30f * pool);
                    float spec = Mathf.Exp(-Mathf.Pow((inward - 0.026f) / 0.008f, 2f)) * Smooth(lit * 1.3f - 0.2f);
                    lip = Color.Lerp(lip, p.TrayAccent, 0.80f * spec);
                    break;
                }
                case TrayFinish.Celestial:
                {
                    // Ivory rim with a fine gold inlay line.
                    float inlay = Mathf.Exp(-Mathf.Pow((inward - 0.118f) / 0.007f, 2f));
                    lip = Color.Lerp(lip, Color.Lerp(p.TrayAccent, PlopPalette.Highlight, 0.3f * lit), 0.85f * inlay);
                    break;
                }
            }
            lip = Color.Lerp(lip, bed, Smooth((inward - 0.153f) / 0.071f));
            return Sdf.Over(c, lip, topInside);
        }

        /// <summary>Pebble (wall) in a World style. Same footprint and depth as <see cref="PaintPebble"/>.</summary>
        private static Color PaintWorldPebble(float x, float y, float pixel, BoardMaterialProfile p)
        {
            float half = PebbleSize * 0.5f;
            var sideColor = Color.Lerp(p.PebbleSide, PlopPalette.Shadow, 0.24f);
            float contact = Sdf.RoundBox(x, y, 0.025f, -0.105f, half - 0.018f, half * 0.95f, PebbleRadius);
            var c = Sdf.Over(PaintWorldTile(x, y, pixel, p), PlopPalette.Shadow, 0.40f * Sdf.Soft(contact, 0.047f));

            float wobble = 0f;
            if (p.PebbleStyle == PebbleStyle.Stone || p.PebbleStyle == PebbleStyle.Moon)
                wobble = (ValueNoise(x * 7f + 3f, y * 7f, 41) - 0.5f) * 0.028f;
            float side = Sdf.RoundBox(x, y, 0.013f, -0.071f, half, half * 0.95f, PebbleRadius) + wobble;
            var skirt = Color.Lerp(sideColor, p.PebbleSide, Smooth((0.20f - x + 0.23f * y) / 0.65f));
            c = Sdf.Over(c, skirt, Sdf.Coverage(side, pixel));

            float face = PebbleTopDistance(x, y) + wobble;
            float inside = Sdf.Coverage(face, pixel);
            if (inside <= 0f) return c;
            float faceLight = RoundedEdgeLight(x, y, half, half * 0.93f, PebbleRadius);
            float diffuse = Smooth(0.50f + 0.68f * y - 0.46f * x);
            var satin = Color.Lerp(p.PebbleTop, p.PebbleHighlight, 0.72f * diffuse);
            var bevel = Color.Lerp(sideColor, PlopPalette.Highlight, 0.18f + 0.61f * faceLight);
            var fill = Color.Lerp(bevel, satin, Smooth(-face / 0.096f));
            float deep = Smooth(-face / 0.05f);

            switch (p.PebbleStyle)
            {
                case PebbleStyle.Toy:
                {
                    // Moulded toy block: four soft pinholes.
                    float localX = Mathf.Abs(x + y * 0.10f) - 0.142f;
                    float localY = Mathf.Abs(y + 0.016f + x * 0.07f) - 0.143f;
                    float pit = Sdf.Circle(localX, localY, 0f, 0f, 0.031f);
                    float pitLight = Smooth(0.50f + 13f * localX - 11f * localY);
                    var pitFace = Color.Lerp(sideColor, p.PebbleHighlight, 0.53f + 0.36f * pitLight);
                    fill = Color.Lerp(fill, pitFace, 0.66f * Sdf.Soft(pit, 0.012f));
                    break;
                }
                case PebbleStyle.Stone:
                {
                    float flecks = Speckle(x, y, 0.06f, 0.013f, 0.32f, 77);
                    fill = Color.Lerp(fill, Color.Lerp(fill, sideColor, 0.6f), flecks * 0.55f * deep);
                    float mottled = ValueNoise(x * 9f, y * 9f, 13) - 0.5f;
                    fill = Color.Lerp(fill, mottled > 0 ? p.PebbleHighlight : sideColor, Mathf.Abs(mottled) * 0.35f * deep);
                    break;
                }
                case PebbleStyle.Ice:
                {
                    // Translucent block: deeper core, crisp facet lines, bright rim.
                    float core = Smooth(-face / 0.16f);
                    fill = Color.Lerp(fill, Color.Lerp(p.PebbleTop, p.PebbleSide, 0.35f), 0.35f * core);
                    float facet = Mathf.Min(Mathf.Abs(x * 0.8f + y - 0.05f), Mathf.Abs(x - y * 0.6f + 0.12f));
                    fill = Color.Lerp(fill, p.PebbleHighlight, 0.55f * (1f - Smooth(facet / 0.012f)) * deep);
                    break;
                }
                case PebbleStyle.Crystal:
                {
                    // Gem facets: flat planes around a bright table.
                    float ax = Mathf.Abs(x), ay = Mathf.Abs(y + 0.01f);
                    float table = Mathf.Max(ax, ay) - 0.17f;
                    float plane = x > y ? (x > -y ? 0.85f : 0.55f) : (x > -y ? 1.0f : 0.70f);
                    fill = Color.Lerp(fill, Color.Lerp(p.PebbleSide, p.PebbleHighlight, plane), 0.55f * deep * Smooth(table / 0.02f));
                    fill = Color.Lerp(fill, p.PebbleHighlight, 0.45f * (1f - Smooth(table / 0.02f)) * deep);
                    float edge = 1f - Smooth(Mathf.Abs(table) / 0.008f);
                    fill = Color.Lerp(fill, Color.white, 0.5f * edge * deep);
                    break;
                }
                case PebbleStyle.Ceramic:
                {
                    float spec = Sdf.Ellipse(x + (y - 0.18f) * 0.3f, y, -0.12f, 0.19f, 0.16f, 0.045f);
                    fill = Color.Lerp(fill, Color.white, 0.6f * Sdf.Soft(spec, 0.03f));
                    break;
                }
                case PebbleStyle.Moon:
                {
                    // Moon stone: two asymmetric shallow craters (never three dots, which read as a face).
                    for (int k = 0; k < 2; k++)
                    {
                        float cx = k == 0 ? 0.095f : -0.135f, cy = k == 0 ? 0.075f : -0.125f;
                        float r = k == 0 ? 0.085f : 0.040f;
                        float crater = Sdf.Circle(x, y, cx, cy, r);
                        float rimLight = Smooth(0.5f + 9f * (y - cy) - 7f * (x - cx));
                        var inner = Color.Lerp(fill, sideColor, 0.30f * (1f - rimLight));
                        fill = Color.Lerp(fill, inner, Sdf.Soft(crater, 0.02f) * deep);
                        float lip = 1f - Smooth(Mathf.Abs(crater + 0.004f) / 0.008f);
                        fill = Color.Lerp(fill, p.PebbleHighlight, 0.5f * lip * (1f - rimLight) * deep);
                    }
                    break;
                }
            }

            float reflection = Sdf.Ellipse(x + (y - 0.195f) * 0.24f, y, -0.13f, 0.195f, 0.22f, 0.098f);
            fill = Color.Lerp(fill, PlopPalette.Highlight, 0.18f * Sdf.Soft(reflection, 0.083f));

            switch (p.PebbleCap)
            {
                case PebbleCap.Snow:
                {
                    // A soft snow cap resting on the top edge.
                    float wave = 0.018f * Mathf.Sin(x * 28f) + 0.012f * Mathf.Sin(x * 61f + 1f);
                    float cap = Mathf.Max(face + 0.0f, -(y - (0.21f + wave)));
                    var snow = Color.Lerp(PlopPalette.Hex(0xDCE8F2), p.CapColor, Smooth(0.4f + 6f * (y - 0.25f)));
                    fill = Color.Lerp(fill, snow, Sdf.Coverage(cap, pixel));
                    break;
                }
                case PebbleCap.Moss:
                {
                    float n = ValueNoise(x * 16f, y * 16f, 61);
                    float cap = Mathf.Max(face, -((y + x * 0.35f) - (0.17f + 0.07f * n)));
                    var moss = Color.Lerp(Color.Lerp(p.CapColor, PlopPalette.Shadow, 0.25f), Color.Lerp(p.CapColor, PlopPalette.Highlight, 0.3f), Smooth(n));
                    fill = Color.Lerp(fill, moss, 0.92f * Sdf.Coverage(cap, pixel));
                    break;
                }
                case PebbleCap.Ember:
                {
                    // Faint warm cracks: cooled rock that remembers the heat.
                    // Ridged noise gives thin, branching cracks; a dark lip makes them read as fissures.
                    float n = ValueNoise(x * 4.2f + 1.7f, y * 4.2f - 0.4f, 91) * 0.85f + ValueNoise(x * 9f, y * 9f, 92) * 0.15f;
                    float ridge = Mathf.Abs(n - 0.5f);
                    float fissure = (1f - Smooth(ridge / 0.03f)) * deep;
                    float core = (1f - Smooth(ridge / 0.010f)) * deep;
                    fill = Color.Lerp(fill, Color.Lerp(fill, PlopPalette.Shadow, 0.7f), 0.45f * fissure);
                    fill = Color.Lerp(fill, p.CapColor, 0.75f * core);
                    break;
                }
                case PebbleCap.Dew:
                {
                    float drop = Sdf.Ellipse(x, y, 0.12f, -0.05f, 0.045f, 0.052f);
                    var water = Color.Lerp(p.PebbleSide, p.CapColor, Smooth(0.5f - 12f * (y + 0.05f)));
                    fill = Color.Lerp(fill, water, 0.8f * Sdf.Coverage(drop, pixel));
                    fill = Color.Lerp(fill, Color.white, 0.9f * Sdf.Coverage(Sdf.Circle(x, y, 0.105f, -0.03f, 0.012f), pixel));
                    break;
                }
            }
            return Sdf.Over(c, fill, inside);
        }
    }
}
