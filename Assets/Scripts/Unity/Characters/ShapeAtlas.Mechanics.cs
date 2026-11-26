using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 10 tile art, baked into the board atlas: each mechanic has its own silhouette, not just a colour.
    /// Frost: faceted pale ice. Lava: scalloped warning rim with an ember glow. Breeze: a fan housing with
    /// separate blades (they spin when the fan pushes) and a direction chevron. Loops: a ring with a soft swirl.
    /// Phase 11: each Loop pair also carries 1–6 dice pips, so pairs never rely on colour alone, and fan power
    /// pips are larger so one, two and three cells of push read at phone size.
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
        // Phase 11.5 effect particles (tiny, shared by every board effect).
        public const string FxDotKey = "fx.dot";
        public const string FxRingKey = "fx.ring";
        public const string FxSparkKey = "fx.spark";
        public const string FxFlakeKey = "fx.flake";
        public const string FxPuffKey = "fx.puff";
        /// <summary>Phase 11.5: a shallow recessed bed under every Nest (depth without touching cosmetic Nest art).</summary>
        public const string NestBedKey = "nest.bed";
        public const int PortalColors = 6;
        public static string PortalKey(int pair) => "tile.portal" + (pair % PortalColors);

        internal static readonly Color[] PortalTints =
        {
            PlopPalette.Hex(0xB6ADE3), PlopPalette.Hex(0x9FD8C9), PlopPalette.Hex(0xF3BEA9),
            PlopPalette.Hex(0xAFCDEB), PlopPalette.Hex(0xE7BC85), PlopPalette.Hex(0xE9AEC4)
        };

        private void AddMechanicTiles(List<Spec> specs)
        {
            specs.Add(Make(FrostKey, -0.5f, -0.5f, 1f, 1f, PaintFrost));
            specs.Add(Make(LavaKey, -0.5f, -0.5f, 1f, 1f, PaintLava));
            specs.Add(Make(FanKey, -0.5f, -0.5f, 1f, 1f, PaintFanBase));
            float blade = UsesArt ? ArtPack.BladeRadiusCells + 0.02f : 0.3f;
            specs.Add(Make(FanBladesKey, -blade, -blade, 2f * blade, 2f * blade, PaintFanBlades));
            specs.Add(Make(FanChevronKey, -0.06f, -0.045f, 0.12f, 0.09f, PaintFanChevron, 192));
            specs.Add(Make(FanArrowKey, -0.13f, -0.10f, 0.26f, 0.20f, PaintFanArrow, 192));
            AddEffectParticles(specs);
            specs.Add(Make(NestBedKey, -0.5f, -0.5f, 1f, 1f, PaintNestBed));
            specs.Add(Make(FanPipKey, -0.055f, -0.055f, 0.11f, 0.11f, (x, y, px) =>
            {
                // Ink dot with a pale halo so it reads on both the tile and the fan housing.
                var halo = PlopPalette.Highlight.WithAlpha(0.85f * Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, 0.047f), px));
                return Sdf.Over(halo, FanInk, Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, 0.036f), px));
            }, 128));
            for (int i = 0; i < PortalColors; i++)
            {
                var tint = PortalTints[i];
                int pips = i + 1;
                if (!UsesArt)
                {
                    specs.Add(Make(PortalKey(i), -0.5f, -0.5f, 1f, 1f, (x, y, px) => PaintPortal(x, y, px, tint, pips)));
                    continue;
                }
                // Phase 11.6: the supplied frame (static, carries the pair pips) above a separate rotating vortex.
                specs.Add(Make(PortalKey(i), -0.5f, -0.5f, 1f, 1f, (x, y, px) => ArtPortalFrame(x, y, px, tint, pips)));
                float vr = ArtPack.VortexRadiusCells + 0.01f;
                specs.Add(Make(PortalVortexKey(i), -vr, -vr, 2f * vr, 2f * vr, (x, y, px) => ArtPortalVortex(x, y, px, tint)));
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
            if (UsesArt) return ArtSurfaceTile(ArtPack.Frost, x, y, pixel);
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
            if (UsesArt) return ArtSurfaceTile(ArtPack.Lava, x, y, pixel);
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

        // Phase 11.5 fan: a rounded porcelain drum with a recessed grille well, four swept blades on a hub
        // (rotated at runtime) and a nozzle tab carrying the direction chevron. Only the housing is painted
        // here; the floor tile underneath is its own sprite.
        private static Color PaintFanBase(float x, float y, float pixel)
        {
            if (UsesArt) return ArtFanHousing(x, y, pixel);
            var c = PlopPalette.Shadow.WithAlpha(0f);
            const float rim = 0.368f, well = 0.302f;
            float r = Mathf.Sqrt(x * x + y * y);
            c = Sdf.Over(c, PlopPalette.Shadow, 0.30f * Sdf.Soft(Sdf.Circle(x, y, 0.014f, -0.030f, rim), 0.035f));
            float nx = r > 1e-4f ? x / r : 0f, ny = r > 1e-4f ? y / r : 1f;
            float band = Mathf.Clamp01((r - well) / (rim - well));
            float bulge = (band - 0.5f) * 2f;
            float lit = Mathf.Clamp01(0.5f + 0.5f * (-0.57f * nx + 0.82f * ny) * bulge + 0.22f * (1f - Mathf.Abs(bulge)));
            var shell = Color.Lerp(PlopPalette.Hex(0xCFC5B7), PlopPalette.Hex(0xFFFAF2), lit);
            c = Sdf.Over(c, shell, Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, rim), pixel));
            // Four tiny screws on the rim.
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                float sx = Mathf.Cos(a) * 0.336f, sy = Mathf.Sin(a) * 0.336f;
                float screw = Sdf.Circle(x, y, sx, sy, 0.014f);
                c = Sdf.Over(c, Color.Lerp(PlopPalette.Hex(0xB8AC9C), PlopPalette.Hex(0xEDE4D6), Smooth(0.5f + 30f * (y - sy) - 20f * (x - sx))), Sdf.Coverage(screw, pixel));
            }
            // Recessed well: shaded on the upper left (the lip casts into it), lit on the lower right.
            float inner = Sdf.Circle(x, y, 0f, 0f, well);
            float recess = Smooth(0.5f - 1.1f * y + 0.8f * x);
            var hollow = Color.Lerp(PlopPalette.Hex(0x8FC2B6), PlopPalette.Hex(0xD3EEE7), recess);
            float lipShade = (1f - Smooth(-inner / 0.05f)) * Smooth(0.5f + 1.4f * y - 1.0f * x);
            hollow = Color.Lerp(hollow, PlopPalette.Hex(0x6E9F95), 0.45f * lipShade);
            // Grille: two thin concentric rings and a cross brace behind the blades.
            float ring1 = Mathf.Abs(r - 0.205f) - 0.006f;
            float ring2 = Mathf.Abs(r - 0.125f) - 0.005f;
            float brace = Mathf.Min(Mathf.Abs(x), Mathf.Abs(y)) - 0.005f;
            float grille = Mathf.Min(Mathf.Min(ring1, ring2), r < well - 0.02f ? brace : 1f);
            hollow = Color.Lerp(hollow, PlopPalette.Hex(0x5F8E85), 0.35f * Sdf.Coverage(grille, pixel));
            return Sdf.Over(c, hollow, Sdf.Coverage(inner, pixel));
        }

        private static Color PaintFanBlades(float x, float y, float pixel)
        {
            if (UsesArt) return ArtFanBlades(x, y, pixel);
            var blade = PlopPalette.Hex(0x7DC3AE);
            float d = 10f;
            float lightAcc = 0f;
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.35f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float u = x * ca + y * sa, v = -x * sa + y * ca;
                // Swept blade: an ellipse bent along its length.
                float bend = v - 0.55f * (u - 0.05f) * (u - 0.05f);
                float e = Sdf.Ellipse(u - 0.155f, bend + 0.012f, 0f, 0f, 0.128f, 0.071f);
                if (e < d) { d = e; lightAcc = Smooth(0.5f + 4f * bend); }
            }
            var face = Color.Lerp(Color.Lerp(blade, PlopPalette.Ink, 0.16f), Color.Lerp(blade, PlopPalette.Highlight, 0.42f), lightAcc);
            face = Color.Lerp(face, Color.Lerp(blade, PlopPalette.Ink, 0.30f), 0.6f * (1f - Smooth(-d / 0.018f)));
            var outC = Sdf.Over(blade.WithAlpha(0f), PlopPalette.Shadow, 0.22f * Sdf.Soft(d + 0.004f, 0.02f));
            outC = Sdf.Over(outC, face, Sdf.Coverage(d, pixel));
            float hub = Sdf.Circle(x, y, 0f, 0f, 0.068f);
            var hubColor = Color.Lerp(PlopPalette.Hex(0xD9A866), PlopPalette.Hex(0xFFE6B8), Smooth(0.5f + 8f * (y - x)));
            outC = Sdf.Over(outC, PlopPalette.Shadow, 0.25f * Sdf.Soft(Sdf.Circle(x, y, 0.008f, -0.012f, 0.068f), 0.02f));
            outC = Sdf.Over(outC, hubColor, Sdf.Coverage(hub, pixel));
            return Sdf.Over(outC, PlopPalette.Highlight, 0.8f * Sdf.Coverage(Sdf.Circle(x, y, -0.020f, 0.022f, 0.016f), pixel));
        }

        /// <summary>Nozzle tab with a chevron, pointing up (+y); the board rotates it to each fan's direction.</summary>
        private static Color PaintFanArrow(float x, float y, float pixel)
        {
            float tab = Sdf.RoundBox(x, y, 0f, -0.012f, 0.105f, 0.062f, 0.055f);
            var c = PlopPalette.Shadow.WithAlpha(0.25f * Sdf.Soft(Sdf.RoundBox(x, y, 0.006f, -0.024f, 0.105f, 0.062f, 0.055f), 0.02f));
            var porcelain = Color.Lerp(PlopPalette.Hex(0xE3D9CB), PlopPalette.Hex(0xFFFBF4), Smooth(0.5f + 6f * y));
            c = Sdf.Over(c, porcelain, Sdf.Coverage(tab, pixel));
            float d = Mathf.Min(Segment(x, y, -0.058f, -0.040f, 0f, 0.022f), Segment(x, y, 0f, 0.022f, 0.058f, -0.040f)) - 0.021f;
            return Sdf.Over(c, FanInk, 0.95f * Sdf.Coverage(d, pixel));
        }

        /// <summary>
        /// A soft recess pressed into the tile: the upper-left wall falls into shadow and the lower-right lip
        /// catches the light (same upper-left key light as the rest of the board). Transparent elsewhere.
        /// </summary>
        private static Color PaintNestBed(float x, float y, float pixel)
        {
            float d = Sdf.RoundBox(x, y, 0f, 0.012f, 0.425f, 0.425f, 0.19f);
            var c = PlopPalette.ContactShadow.WithAlpha(0f);
            float inside = Sdf.Coverage(d, pixel);
            if (inside > 0f)
            {
                float wall = 1f - Smooth(-d / 0.075f);
                float shaded = Smooth(0.5f + (y - x) * 1.1f);
                c = Sdf.Over(c, PlopPalette.ContactShadow, inside * (0.10f + 0.26f * wall * shaded));
            }
            float lip = (1f - Smooth(Mathf.Abs(d - 0.010f) / 0.012f)) * Smooth(0.5f + (x - y) * 1.2f);
            return Sdf.Over(c, PlopPalette.Highlight, 0.55f * lip);
        }

        private void AddEffectParticles(List<Spec> specs)
        {
            specs.Add(Make(FxDotKey, -0.08f, -0.08f, 0.16f, 0.16f, (x, y, px) =>
            {
                float r = Mathf.Sqrt(x * x + y * y) / 0.075f;
                return Color.white.WithAlpha(Mathf.Clamp01(1f - r * r) * Mathf.Clamp01(1.4f - r));
            }, 96));
            specs.Add(Make(FxRingKey, -0.25f, -0.25f, 0.5f, 0.5f, (x, y, px) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return Color.white.WithAlpha(Mathf.Clamp01(1f - Mathf.Abs(r - 0.21f) / 0.022f) * 0.95f);
            }, 128));
            specs.Add(Make(FxSparkKey, -0.08f, -0.08f, 0.16f, 0.16f, (x, y, px) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                float star = (Mathf.Sqrt(ax) + Mathf.Sqrt(ay) - Mathf.Sqrt(0.06f)) * 0.25f;
                float glow = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 0.07f) * 0.35f;
                return Color.white.WithAlpha(Mathf.Max(Sdf.Coverage(star, px), glow));
            }, 96));
            specs.Add(Make(FxFlakeKey, -0.07f, -0.07f, 0.14f, 0.14f, (x, y, px) =>
            {
                float d = 10f;
                for (int k = 0; k < 3; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    d = Mathf.Min(d, Segment(x, y, -ca * 0.055f, -sa * 0.055f, ca * 0.055f, sa * 0.055f) - 0.009f);
                }
                d = Mathf.Min(d, Sdf.Circle(x, y, 0f, 0f, 0.018f));
                return Color.white.WithAlpha(Sdf.Coverage(d, px));
            }, 96));
            specs.Add(Make(FxPuffKey, -0.16f, -0.16f, 0.32f, 0.32f, (x, y, px) =>
            {
                float d = Sdf.SmoothMin(Sdf.Circle(x, y, -0.05f, -0.02f, 0.085f), Sdf.Circle(x, y, 0.055f, -0.01f, 0.075f), 0.05f);
                d = Sdf.SmoothMin(d, Sdf.Circle(x, y, 0.0f, 0.055f, 0.08f), 0.05f);
                return Color.white.WithAlpha(0.85f * Sdf.Soft(d, 0.05f));
            }, 96));
        }

        /// <summary>Dice layouts (unit square ±1) for 1–6 pips.</summary>
        private static readonly Vector2[][] DicePips =
        {
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, 1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(-1, -1), new Vector2(1, -1) },
        };

        private static Color PaintPortal(float x, float y, float pixel, Color tint, int pips)
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
            c = Sdf.Over(c, PlopPalette.Highlight, 0.7f * Sdf.Soft(glint, 0.02f));
            // Pair mark: the two ends of a Loop show the same pip count.
            float pip = 10f;
            foreach (var p in DicePips[Mathf.Clamp(pips, 1, 6) - 1])
                pip = Mathf.Min(pip, Sdf.Circle(x, y, p.x * 0.075f, p.y * 0.075f, 0.03f));
            var ink = Color.Lerp(tint, PlopPalette.Ink, 0.72f);
            return Sdf.Over(c, ink, 0.92f * Sdf.Coverage(pip, pixel));
        }
    }
}
