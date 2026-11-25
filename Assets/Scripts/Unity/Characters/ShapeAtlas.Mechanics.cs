using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 10 tile art, baked into the board atlas: each mechanic has its own silhouette, not just a colour.
    /// Frost: faceted pale ice. Lava: scalloped warning rim with an ember glow. Breeze: a fan housing with
    /// separate blades (they spin when the fan pushes) and a direction chevron. Loops: a ring with a soft swirl.
    /// </summary>
    public sealed partial class ShapeAtlas
    {
        public const string FrostKey = "tile.frost";
        public const string LavaKey = "tile.lava";
        public const string FanKey = "tile.fan";
        public const string FanBladesKey = "tile.fanBlades";
        public const string FanArrowKey = "tile.fanArrow";
        public const string FanPipKey = "tile.fanPip";
        public const string BurnKey = "fx.burn";
        public const int PortalColors = 6;
        public static string PortalKey(int pair) => "tile.portal" + (pair % PortalColors);

        private static readonly Color[] PortalTints =
        {
            PlopPalette.Hex(0xB6ADE3), PlopPalette.Hex(0x9FD8C9), PlopPalette.Hex(0xF3BEA9),
            PlopPalette.Hex(0xAFCDEB), PlopPalette.Hex(0xE7BC85), PlopPalette.Hex(0xE9AEC4)
        };

        private void AddMechanicTiles(List<Spec> specs)
        {
            specs.Add(Make(FrostKey, -0.5f, -0.5f, 1f, 1f, PaintFrost));
            specs.Add(Make(LavaKey, -0.5f, -0.5f, 1f, 1f, PaintLava));
            specs.Add(Make(FanKey, -0.5f, -0.5f, 1f, 1f, PaintFanBase));
            specs.Add(Make(FanBladesKey, -0.3f, -0.3f, 0.6f, 0.6f, PaintFanBlades));
            specs.Add(Make(FanArrowKey, -0.12f, -0.09f, 0.24f, 0.18f, PaintFanArrow, 192));
            specs.Add(Make(FanPipKey, -0.04f, -0.04f, 0.08f, 0.08f, (x, y, px) =>
                FanInk.WithAlpha(Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, 0.028f), px)), 128));
            for (int i = 0; i < PortalColors; i++)
            {
                var tint = PortalTints[i];
                specs.Add(Make(PortalKey(i), -0.5f, -0.5f, 1f, 1f, (x, y, px) => PaintPortal(x, y, px, tint)));
            }
            specs.Add(Make(BurnKey, -0.18f, -0.18f, 0.36f, 0.36f, (x, y, px) =>
            {
                float d = Sdf.SmoothMin(Sdf.Circle(x, y, -0.05f, -0.01f, 0.08f), Sdf.Circle(x, y, 0.05f, 0.02f, 0.07f), 0.04f);
                d = Sdf.SmoothMin(d, Sdf.Circle(x, y, 0f, 0.07f, 0.06f), 0.04f);
                var c = Color.Lerp(PlopPalette.Hex(0xFFE2B0), PlopPalette.Hex(0xF0A070), Smooth(0.5f - y * 3f));
                return c.WithAlpha(0.9f * Sdf.Soft(d, 0.06f));
            }, 128));
        }

        private static readonly Color FanInk = PlopPalette.Hex(0x4E7A76);

        private static Color PaintFrost(float x, float y, float pixel)
        {
            float half = TileSize * 0.5f;
            var deep = PlopPalette.Hex(0x9CC4DE);
            float contact = Sdf.RoundBox(x, y, 0.014f, -0.021f, half - 0.016f, half - 0.016f, TileRadius);
            var c = Sdf.Over(deep.WithAlpha(0f), PlopPalette.Shadow, 0.26f * Sdf.Soft(contact, 0.027f));
            float d = TileDistance(x, y);
            float inside = Sdf.Coverage(d, pixel);
            if (inside <= 0f) return c;

            float diffuse = Smooth(0.45f + 0.62f * y - 0.42f * x);
            var ice = Color.Lerp(PlopPalette.Hex(0xAFD1E8), PlopPalette.Hex(0xE9F6FD), diffuse);
            // Translucent depth: the lower-right edge sinks into a deeper blue, the upper-left rim catches light.
            float rim = 1f - Smooth(-d / 0.07f);
            ice = Color.Lerp(ice, deep, rim * 0.55f * Smooth(0.5f - 0.8f * y + 0.6f * x));
            ice = Color.Lerp(ice, PlopPalette.Highlight, rim * 0.65f * Smooth(0.4f + 0.9f * y - 0.7f * x));
            // Crystal facets: a few soft bright creases, fading toward the edge.
            float facets = Mathf.Min(Mathf.Min(Mathf.Abs(x + y - 0.16f), Mathf.Abs(x + y + 0.24f)), Mathf.Abs(x - y - 0.18f)) * 0.7071f;
            ice = Color.Lerp(ice, PlopPalette.Highlight, 0.55f * (1f - Smooth(facets / 0.011f)) * Smooth(-d / 0.09f));
            // A tiny sparkle.
            float sx = Mathf.Abs(x + 0.19f), sy = Mathf.Abs(y - 0.21f);
            float sparkle = (Mathf.Sqrt(sx) + Mathf.Sqrt(sy) - Mathf.Sqrt(0.035f)) * 0.12f;
            ice = Color.Lerp(ice, PlopPalette.Highlight, 0.9f * Sdf.Coverage(sparkle, pixel));
            return Sdf.Over(c, ice, inside);
        }

        private static Color PaintLava(float x, float y, float pixel)
        {
            float half = TileSize * 0.5f;
            float angle = Mathf.Atan2(y, x);
            float scallop = 0.016f * Mathf.Sin(angle * 8f);
            var ember = PlopPalette.Hex(0xA8473A);
            float contact = Sdf.RoundBox(x, y, 0.014f, -0.021f, half - 0.016f, half - 0.016f, TileRadius);
            var c = Sdf.Over(ember.WithAlpha(0f), PlopPalette.Shadow, 0.30f * Sdf.Soft(contact, 0.03f));
            float d = Sdf.RoundBox(x, y, 0f, 0f, half - 0.02f, half - 0.02f, TileRadius) + scallop;
            float inside = Sdf.Coverage(d, pixel);
            if (inside <= 0f) return c;

            // Notched warning rim, then a warm glowing pool.
            float band = Smooth(-d / 0.07f);
            var pool = Color.Lerp(PlopPalette.Hex(0xE5713F), PlopPalette.Hex(0xF8A75A), Smooth(0.75f - Mathf.Sqrt(x * x + y * y) * 2.2f));
            float glowA = Sdf.Soft(Sdf.Ellipse(x, y, -0.10f, 0.08f, 0.13f, 0.09f), 0.10f);
            float glowB = Sdf.Soft(Sdf.Ellipse(x, y, 0.14f, -0.12f, 0.09f, 0.06f), 0.08f);
            pool = Color.Lerp(pool, PlopPalette.Hex(0xFFD58A), 0.75f * Mathf.Max(glowA, glowB));
            float swirl = Mathf.Abs(Mathf.Sin(Mathf.Sqrt(x * x + y * y) * 26f - angle * 1.5f));
            pool = Color.Lerp(pool, PlopPalette.Hex(0xFFC27A), 0.18f * (1f - Smooth(swirl / 0.25f)) * band);
            var rim = Color.Lerp(ember, PlopPalette.Hex(0xD9764F), Smooth(0.45f + 0.7f * y - 0.5f * x));
            return Sdf.Over(c, Color.Lerp(rim, pool, band), inside);
        }

        private static Color PaintFanBase(float x, float y, float pixel)
        {
            var c = PaintTile(x, y, pixel);
            float housing = Sdf.Circle(x, y, 0f, 0f, 0.335f);
            float shade = Sdf.Soft(Sdf.Circle(x, y, 0.012f, -0.022f, 0.335f), 0.03f);
            c = Sdf.Over(c, PlopPalette.Shadow, 0.22f * shade);
            var shell = Color.Lerp(PlopPalette.Hex(0xE9E0D4), PlopPalette.Hex(0xFFF8EE), Smooth(0.5f + 1.3f * y - 0.9f * x));
            c = Sdf.Over(c, shell, Sdf.Coverage(housing, pixel));
            float well = Sdf.Circle(x, y, 0f, 0f, 0.285f);
            var hollow = Color.Lerp(PlopPalette.Hex(0xBFDDD4), PlopPalette.Hex(0xDCEFE8), Smooth(0.5f - 1.2f * y + 0.8f * x));
            return Sdf.Over(c, hollow, Sdf.Coverage(well, pixel));
        }

        private static Color PaintFanBlades(float x, float y, float pixel)
        {
            var blade = PlopPalette.Hex(0x8FCDBB);
            float d = 10f;
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float u = x * c + y * s, v = -x * s + y * c;
                d = Mathf.Min(d, Sdf.Ellipse(u - 0.13f, v + 0.02f * (u - 0.13f) * 6f, 0f, 0f, 0.13f, 0.065f));
            }
            var color = Color.Lerp(Color.Lerp(blade, PlopPalette.Ink, 0.12f), Color.Lerp(blade, PlopPalette.Highlight, 0.35f), Smooth(0.5f + 3f * (y - x)));
            var outC = Sdf.Over(blade.WithAlpha(0f), color, Sdf.Coverage(d, pixel));
            float hub = Sdf.Circle(x, y, 0f, 0f, 0.055f);
            var hubColor = Color.Lerp(PlopPalette.Hex(0xE5B97E), PlopPalette.Hex(0xFFE3B0), Smooth(0.5f + 8f * (y - x)));
            return Sdf.Over(outC, hubColor, Sdf.Coverage(hub, pixel));
        }

        /// <summary>Chevron pointing up (+y); the board rotates it to each fan's direction.</summary>
        private static Color PaintFanArrow(float x, float y, float pixel)
        {
            float d = Mathf.Min(Segment(x, y, -0.065f, -0.035f, 0f, 0.035f), Segment(x, y, 0f, 0.035f, 0.065f, -0.035f)) - 0.022f;
            return FanInk.WithAlpha(0.95f * Sdf.Coverage(d, pixel));
        }

        private static Color PaintPortal(float x, float y, float pixel, Color tint)
        {
            var c = PaintTile(x, y, pixel);
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            c = Sdf.Over(c, PlopPalette.Shadow, 0.20f * Sdf.Soft(Sdf.Circle(x, y, 0.01f, -0.02f, 0.33f), 0.03f));
            float ring = Mathf.Abs(r - 0.27f) - 0.065f;
            var ringColor = Color.Lerp(Color.Lerp(tint, PlopPalette.Ink, 0.18f), Color.Lerp(tint, PlopPalette.Highlight, 0.45f), Smooth(0.5f + 1.6f * y - 1.1f * x));
            c = Sdf.Over(c, ringColor, Sdf.Coverage(ring, pixel));
            float inner = r - 0.205f;
            float swirl = 0.5f + 0.5f * Mathf.Sin(a * 2f + r * 22f);
            var core = Color.Lerp(Color.Lerp(tint, PlopPalette.Highlight, 0.55f), PlopPalette.Highlight, 0.35f * swirl + 0.5f * Smooth(1f - r / 0.2f));
            c = Sdf.Over(c, core, Sdf.Coverage(inner, pixel));
            float glint = Sdf.Ellipse(x, y, -0.13f, 0.2f, 0.05f, 0.022f);
            return Sdf.Over(c, PlopPalette.Highlight, 0.7f * Sdf.Soft(glint, 0.02f));
        }
    }
}
