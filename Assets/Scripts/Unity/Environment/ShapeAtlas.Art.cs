using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 11.6 painters that draw the supplied art pack (<see cref="ArtPack"/>) into the runtime atlases.
    /// Every board piece stays one sprite in one atlas (batching and the logical grid are unchanged); World
    /// materials and cosmetic palettes re-dye the art instead of replacing it. Thread-safe: no Unity API calls.
    /// </summary>
    public sealed partial class ShapeAtlas
    {
        public static string PortalVortexKey(int pair) => "tile.portalVortex" + (pair % PortalColors);
        public const string FanChevronKey = "tile.fanChevron";

        /// <summary>True when the painters draw the art pack (loaded on the main thread before any bake).</summary>
        internal static bool UsesArt => ArtPack.Painting;

        private static Color ArtFloor(float x, float y, float pixel)
        {
            var art = ArtPack.Floor;
            // Soft contact shadow under the raised tile, like every board piece (key light upper left).
            float contact = Sdf.RoundBox(x, y, 0.012f, -0.024f, TileSize * 0.5f - 0.02f, TileSize * 0.5f - 0.02f, TileRadius);
            var c = PlopPalette.ContactShadow.WithAlpha(0.22f * Sdf.Soft(contact, 0.03f));
            return Over(c, art.At(x, y, ArtPack.Scale(art, ArtPack.TileFootprint), pixel));
        }

        private const float PebbleArtY = -0.015f;

        /// <summary>The supplied pebble, optionally re-dyed onto a World ramp (shade → base → light).</summary>
        private static Color ArtPebbleColor(float x, float y, float pixel, bool recolor, Color shade, Color mid, Color light)
        {
            var art = ArtPack.Pebble;
            var a = art.At(x, y - PebbleArtY, ArtPack.Scale(art, ArtPack.PebbleFootprint), pixel);
            if (!recolor || a.a <= 0f) return a;
            var mapped = ArtPack.GradientMap(a, art, shade, mid, light);
            var mixed = Color.Lerp(mapped, a, 0.18f);
            mixed.a = a.a;
            return mixed;
        }

        private static Color ArtPebble(float x, float y, float pixel)
        {
            float half = ArtPack.PebbleFootprint * 0.5f;
            float contact = Sdf.RoundBox(x, y, 0.025f, -0.085f, half - 0.02f, half * 0.95f, PebbleRadius);
            var c = Sdf.Over(PaintTile(x, y, pixel), PlopPalette.Shadow, 0.40f * Sdf.Soft(contact, 0.047f));
            return Over(c, ArtPebbleColor(x, y, pixel, false, default, default, default));
        }

        private static Color ArtSurfaceTile(ArtImage art, float x, float y, float pixel)
        {
            float contact = Sdf.RoundBox(x, y, 0.012f, -0.024f, TileSize * 0.5f - 0.02f, TileSize * 0.5f - 0.02f, TileRadius);
            var c = PlopPalette.Shadow.WithAlpha(0.24f * Sdf.Soft(contact, 0.03f));
            return Over(c, art.At(x, y, ArtPack.Scale(art, ArtPack.TileFootprint), pixel));
        }

        private static Color ArtFanHousing(float x, float y, float pixel)
        {
            var art = ArtPack.FanHousing;
            float r = ArtPack.FanFootprint * 0.5f;
            var c = PlopPalette.Shadow.WithAlpha(0.26f * Sdf.Soft(Sdf.Circle(x, y, 0.012f, -0.03f, r - 0.03f), 0.04f));
            return Over(c, art.At(x, y, ArtPack.Scale(art, ArtPack.FanFootprint), pixel));
        }

        private static Color ArtFanBlades(float x, float y, float pixel)
            => ArtPack.FanBlades.At(x, y, ArtPack.BladeScale, pixel);

        /// <summary>Strength chevron (pointing +y); one per cell of push, rotated to the fan's direction.</summary>
        private static Color PaintFanChevron(float x, float y, float pixel)
        {
            float d = Mathf.Min(Segment(x, y, -0.046f, -0.020f, 0f, 0.018f), Segment(x, y, 0f, 0.018f, 0.046f, -0.020f));
            var halo = PlopPalette.Highlight.WithAlpha(0.92f * Sdf.Coverage(d - 0.024f, pixel));
            return Sdf.Over(halo, FanInk, Sdf.Coverage(d - 0.0135f, pixel));
        }

        private static Color ArtPortalFrame(float x, float y, float pixel, Color tint, int pips)
        {
            var art = ArtPack.PortalFrame;
            var f = art.At(x, y, ArtPack.Scale(art, ArtPack.PortalFootprint), pixel);
            f = ArtPack.Redye(f, art.DyeHsv, HueOnly(art.DyeHsv, tint, 0.35f));
            var c = Over(PlopPalette.Shadow.WithAlpha(0.22f * Sdf.Soft(Sdf.Circle(x, y, 0.012f, -0.028f, ArtPack.PortalFootprint * 0.5f - 0.04f), 0.04f)), f);
            // Pair mark over the vortex: the two ends of a Loop show the same pip count (colour-independent).
            float pip = 10f;
            foreach (var p in DicePips[Mathf.Clamp(pips, 1, 6) - 1])
                pip = Mathf.Min(pip, Sdf.Circle(x, y, p.x * 0.062f, p.y * 0.062f, 0.026f));
            c = Sdf.Over(c, PlopPalette.Highlight, 0.95f * Sdf.Coverage(pip - 0.012f, pixel));
            return Sdf.Over(c, Color.Lerp(tint, PlopPalette.Ink, 0.78f), Sdf.Coverage(pip, pixel));
        }

        private static Color ArtPortalVortex(float x, float y, float pixel, Color tint)
        {
            var art = ArtPack.PortalVortex;
            var v = art.At(x, y, ArtPack.VortexScale, pixel);
            return ArtPack.Redye(v, art.DyeHsv, HueOnly(art.DyeHsv, tint, 0.15f));
        }

        /// <summary>Target dye: the tint's hue, the art's own saturation/value nudged toward the tint by <paramref name="follow"/>.</summary>
        private static Vector3 HueOnly(Vector3 source, Color tint, float follow)
        {
            var t = ArtPack.Hsv(tint);
            return new Vector3(t.x, Mathf.Lerp(source.y, t.y, follow), Mathf.Lerp(source.z, t.z, follow));
        }

        /// <summary>The supplied pillow Nest re-dyed to a palette colour (the closest-hue cushion is the source).</summary>
        private static Painter ArtNestPainter(Color cover)
        {
            var src = ArtPack.NestFor(cover);
            var target = ArtPack.Hsv(cover);
            // Keep the painting's richer fabric: follow the palette's hue fully, its saturation/value halfway.
            var dye = new Vector3(target.x, Mathf.Lerp(src.DyeHsv.y, target.y, 0.5f), Mathf.Lerp(src.DyeHsv.z, target.z, 0.5f));
            bool same = ArtPack.HueDistance(src.DyeHsv.x, target.x) < 0.015f && Mathf.Abs(src.DyeHsv.y - dye.y) < 0.04f;
            float scale = ArtPack.Scale(src, ArtPack.NestFootprint);
            return (x, y, px) =>
            {
                var a = src.At(x, y, scale, px);
                return same ? a : ArtPack.Redye(a, src.DyeHsv, dye);
            };
        }

        /// <summary>Straight-alpha "over" with the source's own alpha.</summary>
        private static Color Over(Color dst, Color src) => src.a <= 0f ? dst : Sdf.Over(dst, src, 1f);
    }
}
