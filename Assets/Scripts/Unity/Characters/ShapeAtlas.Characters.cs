using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Character and Nest painters for a <see cref="CharacterLook"/>. Everything bakes into the same atlas texture
    /// as the board, so cosmetics add no materials, shaders or draw-call groups.
    /// </summary>
    public sealed partial class ShapeAtlas
    {
        // Keys shared by every look (the atlas holds exactly one look, or prefixed variants for Studio thumbnails).
        public const string ShadowKey = "plopShadow";
        public const string CheekKey = "cheek";
        public const string BrowKey = "brow";
        public const string AccessoryKey = "accessory";
        public const string EffectKey = "fx";

        public static string EyeKey(EyeShape shape) => "eye." + shape;
        public static string MouthKey(MouthShape shape) => "mouth." + shape;

        private void AddCharacterParts(List<Spec> specs, CharacterLook look, int[] colors, string prefix = "")
        {
            var style = look.Style;
            foreach (int c in colors) AddBody(specs, prefix + PlopKey(c), style, look.Finish, look.Palette, c);
            AddShadow(specs, prefix + ShadowKey, style);
            AddFace(specs, look.Face, prefix);
            AddAccessory(specs, prefix + AccessoryKey, look.Accessory);
            AddEffect(specs, prefix + EffectKey, look.Effect.Kind);
        }

        // ------------------------------------------------------------------ body

        private void AddBody(List<Spec> specs, string key, BodyStyle style, JellyFinish finish, CosmeticPalette palette, int colorId)
        {
            var material = new JellyBody(style, finish, palette, colorId);
            var b = style.Bounds;
            specs.Add(Make(key, b.x, b.y, b.width, b.height, material.Paint));
        }

        private void AddShadow(List<Spec> specs, string key, BodyStyle style)
        {
            float w = style.DomeRx / 0.371f;
            specs.Add(Make(key, -0.46f * w - 0.02f, -0.165f, 0.92f * w + 0.04f, 0.33f, (x, y, px) =>
            {
                float ambient = ContactFalloff(x - 0.035f * w, y + 0.023f, 0.414f * w, 0.119f);
                float contact = ContactFalloff(x - 0.018f * w, y - 0.010f, 0.293f * w, 0.046f);
                return PlopPalette.ContactShadow.WithAlpha(0.25f * ambient + 0.21f * contact);
            }));
        }

        /// <summary>Baked jelly shading. Thin rims scatter light, the thick centre keeps its tint, a soft specular sits upper-left.</summary>
        private sealed class JellyBody
        {
            private readonly BodyStyle _style;
            private readonly JellyFinish _f;
            private readonly int _colorId;
            private readonly Color _body, _lit, _core, _scatter, _floor, _milk, _velvetRim;
            private readonly float _lean, _crown;
            private readonly Vector2 _spec, _secondary, _dewA, _dewB;

            public JellyBody(BodyStyle style, JellyFinish finish, CosmeticPalette palette, int colorId)
            {
                _style = style;
                _f = finish;
                _colorId = colorId;
                _body = palette.Body(colorId);
                _lit = Color.Lerp(_body, PlopPalette.Highlight, finish.Lit);
                _core = Color.Lerp(_body, PlopPalette.FaceInk, finish.Core);
                _scatter = Color.Lerp(_body, PlopPalette.GelBounce, finish.Scatter);
                _floor = Color.Lerp(_body, PlopPalette.GelBounce, 0.67f);
                _milk = Color.Lerp(_body, PlopPalette.SealCream, 0.52f);
                _velvetRim = Color.Lerp(_body, PlopPalette.Highlight, 0.30f);
                _lean = (colorId % 3 - 1) * 0.051f;
                _crown = 1f + (colorId / 3 * 2 - 1) * 0.018f;
                _spec = style.MapFromClassic(-0.199f, 0.569f);
                _secondary = style.MapFromClassic(0.093f, 0.630f);
                _dewA = style.MapFromClassic(-0.075f, 0.505f);
                _dewB = style.MapFromClassic(0.175f, 0.455f);
            }

            public Color Paint(float x, float y, float pixel)
            {
                var st = _style;
                var f = _f;
                float d = st.Distance(x + _lean * Mathf.Max(0f, y - 0.16f), y * _crown);
                float inside = Sdf.Coverage(d, pixel);
                if (inside <= 0f) return _body.WithAlpha(0f);

                float nx = (x - st.DomeX - 0.004f) / (st.DomeRx + 0.037f);
                float ny = (y - st.DomeY - 0.017f) / (st.DomeRy + 0.017f);
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float diffuse = Smooth(0.22f - 0.36f * nx + 0.45f * ny + 0.58f * nz);
                float thickness = Smooth(-d / 0.195f);
                var gel = Color.Lerp(_scatter, _body, thickness);
                float lowBody = Smooth((0.41f - y + 0.21f * x) / 0.38f);
                gel = Color.Lerp(gel, _core, f.CoreDepth * lowBody * thickness);
                gel = Color.Lerp(gel, _lit, f.Diffuse * diffuse);

                float softRim = (1f - Smooth(-d / 0.079f)) * Smooth(-d / 0.013f);
                float side = Smooth(0.66f + 0.58f * y - 0.45f * x);
                gel = Color.Lerp(gel, _scatter, softRim * side * f.Rim);
                float s = st.SkirtScale;
                float bounced = Sdf.Soft(Sdf.Ellipse(x, y, 0.013f, 0.059f * st.SkirtHeight, 0.277f * s, 0.025f), 0.051f);
                gel = Color.Lerp(gel, _floor, f.Bounce * bounced);
                if (y < 0.18f)
                {
                    float footX = Mathf.Abs(x) - 0.253f * s;
                    float fillet = Sdf.Ellipse(footX, y, 0f, 0.088f * st.SkirtHeight, 0.101f * s, 0.019f);
                    gel = Color.Lerp(gel, _floor, 0.68f * Sdf.Soft(fillet, 0.029f));
                }

                if (f.Pearl > 0f)
                {
                    // Angle-dependent pastel sheen: lilac at the crown shifting to mint and honey around the rim.
                    float angle = Mathf.Atan2(ny, nx);
                    var pearl = Color.Lerp(PlopPalette.Lavender, PlopPalette.Mint, 0.5f + 0.5f * Mathf.Sin(2f * angle + 0.6f));
                    pearl = Color.Lerp(pearl, PlopPalette.Honey, 0.25f + 0.25f * Mathf.Cos(3f * angle));
                    pearl = Color.Lerp(pearl, PlopPalette.Highlight, 0.45f);
                    float where = Mathf.Clamp01(0.65f * (1f - Smooth(-d / 0.11f)) + 0.35f * diffuse);
                    gel = Color.Lerp(gel, pearl, f.Pearl * where * 0.62f);
                }
                if (f.Milk > 0f) gel = Color.Lerp(gel, _milk, f.Milk * (0.45f + 0.55f * thickness));
                if (f.Velvet > 0f)
                {
                    float fuzz = 1f - Smooth(-d / 0.055f);
                    gel = Color.Lerp(gel, _velvetRim, f.Velvet * 0.55f * fuzz * Smooth(0.4f + y - 0.3f * x));
                }
                if (f.Glow > 0f) gel = Color.Lerp(gel, PlopPalette.Highlight, f.Glow * 0.55f * softRim * side);

                float specX = x - _spec.x;
                float specY = y - _spec.y;
                float along = (specX * 0.80f + specY * 0.60f) / f.SheenSize;
                float across = (specY * 0.80f - specX * 0.60f) / f.SheenSize;
                float softbox = Sdf.Ellipse(along, across, 0f, 0f, 0.130f, 0.075f);
                gel = Color.Lerp(gel, _scatter, f.Softbox * Sdf.Soft(softbox, 0.084f * f.SheenSoftness));
                float sheen = Sdf.Ellipse(along, across, 0f, 0f, 0.084f, 0.039f);
                gel = Color.Lerp(gel, PlopPalette.Highlight, f.Sheen * Sdf.Soft(sheen, 0.026f * f.SheenSoftness));
                if (f.Secondary > 0f)
                {
                    float secondary = Sdf.Ellipse(x + (y - _secondary.y) * 0.27f, y, _secondary.x, _secondary.y, 0.047f, 0.014f);
                    gel = Color.Lerp(gel, PlopPalette.Highlight, f.Secondary * Sdf.Soft(secondary, 0.025f * f.SheenSoftness));
                }
                if (f.Dew > 0f)
                {
                    float dewA = Sdf.Circle(x, y, _dewA.x, _dewA.y, 0.019f);
                    float dewB = Sdf.Circle(x, y, _dewB.x, _dewB.y, 0.011f);
                    gel = Color.Lerp(gel, PlopPalette.Highlight, f.Dew * 0.82f * Sdf.Soft(Mathf.Min(dewA, dewB), 0.010f));
                }

                // The colour-independent match mark: the same seal is printed on this Plop's Nest.
                float stampX = x - 0.202f * s;
                float stampY = y - 0.170f * st.SkirtHeight;
                if (Mathf.Abs(stampX) < 0.11f && Mathf.Abs(stampY) < 0.11f)
                {
                    float stamp = Sdf.Circle(stampX, stampY, 0f, 0f, 0.090f);
                    gel = Color.Lerp(gel, PlopPalette.SealCream, 0.56f * Sdf.Coverage(stamp, pixel));
                    gel = Color.Lerp(gel, PlopPalette.SealInk, 0.94f * SealInkCoverage(_colorId, stampX, stampY, 0.067f, pixel));
                }

                return gel.WithAlpha(inside * Mathf.Lerp(f.AlphaThin, f.AlphaThick, thickness));
            }
        }

        // ------------------------------------------------------------------ face

        private void AddFace(List<Spec> specs, FaceStyle face, string prefix)
        {
            var ink = PlopPalette.FaceInk;
            Painter open = OpenEye(face.Eye);
            specs.Add(Make(prefix + EyeKey(EyeShape.Open), -0.07f, -0.08f, 0.14f, 0.16f, open, 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.Wide), -0.085f, -0.095f, 0.17f, 0.19f, (x, y, px) => open(x / 1.2f, y / 1.2f, px / 1.2f), 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.Half), -0.07f, -0.07f, 0.14f, 0.12f, (x, y, px) => DrowsyEye(open, x, y, px), 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.Closed), -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.LowerArc(x, y, 0f, 0.021f, 0.048f, 1.03f, 0.0135f), px)), 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.Happy), -0.08f, -0.06f, 0.16f, 0.12f, (x, y, px) =>
                Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.LowerArc(x, -y, 0f, 0.026f, 0.046f, 1.02f, 0.0135f), px)), 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.SqueezeL), -0.07f, -0.06f, 0.14f, 0.12f, (x, y, px) =>
                Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Chevron(x, y, 1f), px)), 192));
            specs.Add(Make(prefix + EyeKey(EyeShape.SqueezeR), -0.07f, -0.06f, 0.14f, 0.12f, (x, y, px) =>
                Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Chevron(x, y, -1f), px)), 192));

            var mouthInk = ink.WithAlpha(0.95f);
            var tongue = Color.Lerp(PlopPalette.Berry, PlopPalette.FaceInk, 0.18f);
            Painter Stroke(System.Func<float, float, float> distance) =>
                (x, y, px) => Sdf.Over(mouthInk.WithAlpha(0f), mouthInk, Sdf.Coverage(distance(x, y), px));
            specs.Add(Make(prefix + MouthKey(MouthShape.Neutral), -0.075f, -0.052f, 0.15f, 0.104f, face.Mouth == MouthKind.Cat
                ? Stroke((x, y) => Mathf.Min(Sdf.LowerArc(x + 0.021f, y, 0f, 0.012f, 0.022f, 1.05f, 0.0105f),
                                             Sdf.LowerArc(x - 0.021f, y, 0f, 0.012f, 0.022f, 1.05f, 0.0105f)))
                : Stroke((x, y) => Sdf.LowerArc(x, y, 0f, 0.015f, 0.041f, 1f, 0.013f)), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Grin), -0.06f, -0.055f, 0.12f, 0.09f, (x, y, px) =>
            {
                float mouth = Mathf.Max(Sdf.Ellipse(x, y, 0f, 0.008f, 0.039f, 0.040f), y - 0.008f);
                var c = Sdf.Over(mouthInk.WithAlpha(0f), mouthInk, Sdf.Coverage(mouth, px));
                float t = Mathf.Max(Sdf.Ellipse(x, y, 0f, -0.024f, 0.022f, 0.012f), mouth + 0.006f);
                return Sdf.Over(c, tongue, Sdf.Coverage(t, px));
            }, 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Open), -0.05f, -0.05f, 0.10f, 0.10f, Stroke((x, y) => Sdf.Ellipse(x, y, 0f, 0f, 0.020f, 0.026f)), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Small), -0.04f, -0.04f, 0.08f, 0.08f, Stroke((x, y) => Sdf.Ellipse(x, y, 0f, 0f, 0.012f, 0.014f)), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Flat), -0.05f, -0.03f, 0.10f, 0.06f, Stroke((x, y) => Segment(x, y, -0.024f, 0f, 0.024f, 0f) - 0.0095f), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Wavy), -0.06f, -0.04f, 0.12f, 0.08f, Stroke((x, y) =>
                Mathf.Abs(x) > 0.036f ? Sdf.Circle(Mathf.Abs(x), y, 0.036f, 0.0075f * Mathf.Sin(0.036f * 92f) * Mathf.Sign(x), 0.0095f)
                    : Mathf.Abs(y - 0.0075f * Mathf.Sin(x * 92f)) * 0.82f - 0.0095f), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Frown), -0.06f, -0.04f, 0.12f, 0.08f, Stroke((x, y) => Sdf.LowerArc(x, -y, 0f, 0.030f, 0.036f, 0.92f, 0.012f)), 192));
            specs.Add(Make(prefix + MouthKey(MouthShape.Sleep), -0.06f, -0.05f, 0.12f, 0.10f, Stroke((x, y) => Sdf.Ellipse(x, y, 0f, -0.006f, 0.017f, 0.012f)), 192));
            specs.Add(Make(prefix + BrowKey, -0.06f, -0.04f, 0.12f, 0.08f, Stroke((x, y) => Sdf.LowerArc(x, -y, 0f, 0.052f, 0.056f, 0.50f, 0.0085f)), 192));

            var cheek = Color.Lerp(PlopPalette.ToySlot(0), PlopPalette.Ink, 0.11f);
            float cs = face.CheekScale, ca = face.CheekAlpha;
            specs.Add(Make(prefix + CheekKey, -0.09f, -0.07f, 0.18f, 0.14f, (x, y, px) =>
                Sdf.Over(cheek.WithAlpha(0f), cheek, ca * Sdf.Soft(Sdf.Ellipse(x, y, 0f, 0f, 0.054f * cs, 0.028f * cs), 0.022f)), 128));
        }

        /// <summary>Lower half of an open eye under a soft, gently convex lid (relaxed, never cross).</summary>
        private static Color DrowsyEye(Painter open, float x, float y, float px)
        {
            var ink = PlopPalette.FaceInk;
            float lidY = -0.074f + Mathf.Sqrt(Mathf.Max(0f, 0.0081f - x * x)); // circle r 0.09 centred below
            var c = y < lidY - 0.003f ? open(x, y + 0.004f, px) : ink.WithAlpha(0f);
            float lid = Mathf.Abs(x) > 0.047f
                ? Sdf.Circle(Mathf.Abs(x), y, 0.047f, -0.074f + Mathf.Sqrt(0.0081f - 0.047f * 0.047f), 0.0068f)
                : Mathf.Abs(y - lidY) - 0.0068f;
            return Sdf.Over(c, ink.WithAlpha(0.9f), Sdf.Coverage(lid, px));
        }

        private static Painter OpenEye(EyeKind kind)
        {
            Color ink = PlopPalette.FaceInk;
            switch (kind)
            {
                case EyeKind.Button:
                    return (x, y, px) =>
                    {
                        var c = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, 0.043f), px));
                        return Sdf.Over(c, PlopPalette.Highlight, 0.95f * Sdf.Coverage(Sdf.Circle(x, y, -0.014f, 0.015f, 0.012f), px));
                    };
                case EyeKind.Dozy:
                    return (x, y, px) =>
                    {
                        // Heavy-lidded but content: a round eye whose top third rests under a soft lid.
                        Painter round = (u, v, p) =>
                        {
                            var e = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.Ellipse(u, v, 0f, -0.004f, 0.042f, 0.052f), p));
                            return Sdf.Over(e, PlopPalette.Highlight, 0.95f * Sdf.Coverage(Sdf.Circle(u, v, -0.013f, -0.016f, 0.011f), p));
                        };
                        return DrowsyEye(round, x, y - 0.014f, px);
                    };
                case EyeKind.Starry:
                    return (x, y, px) =>
                    {
                        var c = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.Ellipse(x, y, 0f, 0f, 0.045f, 0.061f), px));
                        float sx = Mathf.Abs(x + 0.012f), sy = Mathf.Abs(y - 0.017f);
                        float star = (Mathf.Sqrt(sx) + Mathf.Sqrt(sy) - Mathf.Sqrt(0.024f)) * 0.09f;
                        c = Sdf.Over(c, PlopPalette.Highlight, Sdf.Coverage(star, px));
                        return Sdf.Over(c, PlopPalette.Highlight, 0.9f * Sdf.Coverage(Sdf.Circle(x, y, 0.014f, -0.022f, 0.0075f), px));
                    };
                case EyeKind.Blush:
                    return (x, y, px) =>
                    {
                        var c = Sdf.Over(ink.WithAlpha(0f), ink, Sdf.Coverage(Sdf.Ellipse(x, y, 0f, -0.004f, 0.037f, 0.052f), px));
                        c = Sdf.Over(c, PlopPalette.Highlight, 0.98f * Sdf.Coverage(Sdf.Circle(x, y, -0.011f, 0.016f, 0.0135f), px));
                        return Sdf.Over(c, PlopPalette.Highlight, 0.75f * Sdf.Coverage(Sdf.Circle(x, y, 0.012f, -0.024f, 0.0065f), px));
                    };
                default:
                    return PaintEye;
            }
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float qx = px - dx * h, qy = py - dy * h;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        /// <summary>"&gt;" (direction 1) or "&lt;" (−1) squeezed eye.</summary>
        private static float Chevron(float x, float y, float direction)
        {
            float tipX = 0.026f * direction, backX = -0.030f * direction;
            return Mathf.Min(Segment(x, y, backX, 0.030f, tipX, 0f), Segment(x, y, tipX, 0f, backX, -0.030f)) - 0.0125f;
        }

        // ------------------------------------------------------------------ accessories

        private void AddAccessory(List<Spec> specs, string key, AccessoryStyle accessory)
        {
            if (accessory == null || accessory.Anchor == AccessoryAnchor.None) return;
            var b = accessory.Bounds;
            specs.Add(Make(key, b.x, b.y, b.width, b.height, AccessoryPainter(accessory.Name), 256));
        }

        private static Color Shade(Color baseColor, float x, float y, float d, float lightScale = 1f)
        {
            float light = Smooth(0.5f + (y * 0.82f - x * 0.57f) * 6f * lightScale);
            var c = Color.Lerp(Color.Lerp(baseColor, PlopPalette.FaceInk, 0.16f), Color.Lerp(baseColor, PlopPalette.Highlight, 0.35f), light);
            float rim = 1f - Smooth(-d / 0.009f);
            return Color.Lerp(c, Color.Lerp(baseColor, PlopPalette.FaceInk, 0.22f), 0.35f * rim);
        }

        private static Color Gloss(Color c, float x, float y, float cx, float cy, float rx, float ry, float amount = 0.6f)
            => Color.Lerp(c, PlopPalette.Highlight, amount * Sdf.Soft(Sdf.Ellipse(x, y, cx, cy, rx, ry), 0.012f));

        private static Painter AccessoryPainter(string name)
        {
            var berry = PlopPalette.Hex(0xEE8FAE);
            var cream = PlopPalette.Hex(0xFFF4E6);
            var honey = PlopPalette.Hex(0xF1BE6A);
            var leafGreen = PlopPalette.Hex(0x8CCB98);
            var lilac = PlopPalette.Hex(0xA99BE6);
            var capBlue = PlopPalette.Hex(0x9DB3EA);
            var shadow = PlopPalette.ContactShadow;
            switch (name)
            {
                case "bow":
                    return (bx, by, bpx) =>
                    {
                        float x = bx / 1.2f, y = by / 1.2f, px = bpx / 1.2f;
                        float ax = Mathf.Abs(x);
                        float lobe = Sdf.Ellipse((ax - 0.064f) * 0.95f + y * 0.30f, y - (ax - 0.064f) * 0.30f, 0f, 0.004f, 0.058f, 0.040f);
                        float tails = Mathf.Min(Segment(x, y, -0.004f, -0.004f, -0.034f, -0.062f), Segment(x, y, 0.004f, -0.004f, 0.030f, -0.060f)) - 0.013f;
                        float knot = Sdf.Circle(x, y, 0f, 0f, 0.025f);
                        float body = Mathf.Min(Mathf.Min(lobe, tails), knot);
                        var c = shadow.WithAlpha(0.22f * Sdf.Soft(body + 0.004f, 0.02f) * (y < 0f ? 1f : 0.4f));
                        var fill = Shade(berry, x, y, body);
                        float fold = Sdf.Soft(Sdf.Ellipse(ax, y, 0.032f, 0f, 0.022f, 0.030f), 0.02f);
                        fill = Color.Lerp(fill, Color.Lerp(berry, PlopPalette.FaceInk, 0.25f), 0.45f * fold * (knot > 0 ? 1f : 0f));
                        fill = Gloss(fill, x, y, -0.072f, 0.020f, 0.020f, 0.010f, 0.55f);
                        fill = Gloss(fill, x, y, -0.006f, 0.010f, 0.008f, 0.006f, 0.6f);
                        return Sdf.Over(c, fill, Sdf.Coverage(body, px));
                    };
                case "flower":
                    return (x, y, px) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float petals = r - (0.062f + 0.026f * Mathf.Cos(5f * a));
                        var c = shadow.WithAlpha(0.18f * Sdf.Soft(petals + 0.006f, 0.02f));
                        var petal = Color.Lerp(cream, PlopPalette.Hex(0xF7C6D6), Smooth(r / 0.08f));
                        petal = Shade(petal, x, y, petals, 0.8f);
                        float vein = Mathf.Abs(Mathf.Sin(2.5f * a)) < 0.06f && r > 0.03f ? 0.18f : 0f;
                        petal = Color.Lerp(petal, PlopPalette.Hex(0xE9A9BF), vein);
                        c = Sdf.Over(c, petal, Sdf.Coverage(petals, px));
                        float center = Sdf.Circle(x, y, 0f, 0f, 0.024f);
                        var core = Gloss(Shade(honey, x, y, center), x, y, -0.008f, 0.008f, 0.008f, 0.006f);
                        return Sdf.Over(c, core, Sdf.Coverage(center, px));
                    };
                case "leaf":
                    return (x, y, px) =>
                    {
                        float stem = Segment(x, y, 0f, 0f, 0.004f, 0.085f) - 0.010f;
                        float l1 = Rotated(x + 0.050f, y - 0.110f, 0.55f, (u, v) => Sdf.Ellipse(u, v, 0f, 0f, 0.060f, 0.026f));
                        float l2 = Rotated(x - 0.052f, y - 0.128f, -0.62f, (u, v) => Sdf.Ellipse(u, v, 0f, 0f, 0.066f, 0.029f));
                        float shape = Mathf.Min(stem, Mathf.Min(l1, l2));
                        var fill = Shade(leafGreen, x, y - 0.1f, shape);
                        float rib = Mathf.Min(Rotated(x + 0.050f, y - 0.110f, 0.55f, (u, v) => Mathf.Abs(v) + Mathf.Max(0f, Mathf.Abs(u) - 0.045f)),
                                              Rotated(x - 0.052f, y - 0.128f, -0.62f, (u, v) => Mathf.Abs(v) + Mathf.Max(0f, Mathf.Abs(u) - 0.05f)));
                        fill = Color.Lerp(fill, Color.Lerp(leafGreen, PlopPalette.Highlight, 0.5f), 0.55f * (1f - Smooth(rib / 0.006f)));
                        return Sdf.Over(leafGreen.WithAlpha(0f), fill, Sdf.Coverage(shape, px));
                    };
                case "ribbon":
                    return (rx, ry, rpx) =>
                    {
                        float x = rx / 1.3f, y = ry / 1.3f, px = rpx / 1.3f;
                        float loopL = Sdf.Ellipse(x + 0.040f, y - 0.030f, 0f, 0f, 0.044f, 0.030f);
                        float loopR = Sdf.Ellipse(x - 0.040f, y - 0.034f, 0f, 0f, 0.044f, 0.030f);
                        float loops = Mathf.Min(loopL, loopR);
                        float tailL = Segment(x, y, -0.006f, 0.02f, -0.042f + 0.008f * Mathf.Sin(y * 60f), -0.12f) - 0.017f;
                        float tailR = Segment(x, y, 0.006f, 0.02f, 0.046f + 0.008f * Mathf.Sin(y * 60f), -0.11f) - 0.017f;
                        float knot = Sdf.Circle(x, y, 0f, 0.028f, 0.022f);
                        float shape = Mathf.Min(Mathf.Min(loops, knot), Mathf.Min(tailL, tailR));
                        var fill = Shade(lilac, x, y, shape);
                        float fold = Mathf.Min(Sdf.Ellipse(x + 0.030f, y - 0.030f, 0f, 0f, 0.016f, 0.010f), Sdf.Ellipse(x - 0.030f, y - 0.034f, 0f, 0f, 0.016f, 0.010f));
                        fill = Color.Lerp(fill, Color.Lerp(lilac, PlopPalette.FaceInk, 0.3f), 0.45f * Sdf.Soft(fold, 0.01f));
                        fill = Gloss(fill, x, y, -0.05f, 0.05f, 0.014f, 0.007f);
                        var c = PlopPalette.ContactShadow.WithAlpha(0.18f * Sdf.Soft(shape + 0.004f, 0.02f));
                        return Sdf.Over(c, fill, Sdf.Coverage(shape, px));
                    };
                case "star":
                    return (x, y, px) =>
                    {
                        float star = Star5(x, y, 0.074f, 0.44f) - 0.010f;
                        var c = shadow.WithAlpha(0.2f * Sdf.Soft(Star5(x - 0.006f, y + 0.008f, 0.074f, 0.44f), 0.02f));
                        var fill = Gloss(Shade(honey, x, y, star), x, y, -0.018f, 0.022f, 0.016f, 0.010f, 0.65f);
                        return Sdf.Over(c, fill, Sdf.Coverage(star, px));
                    };
                case "nightcap":
                    return (x, y, px) =>
                    {
                        // A soft cone swept from the brim toward a drooping tip.
                        float cone = 10f;
                        for (int i = 0; i <= 8; i++)
                        {
                            float t = i / 8f;
                            float cx = Mathf.Lerp(0f, 0.15f, t * t) + 0.02f * t;
                            float cy = Mathf.Lerp(0.04f, 0.205f, t) - 0.05f * t * t;
                            cone = Sdf.SmoothMin(cone, Sdf.Circle(x, y, cx, cy, Mathf.Lerp(0.115f, 0.022f, t)), 0.02f);
                        }
                        var c = shadow.WithAlpha(0.2f * Sdf.Soft(Sdf.RoundBox(x, y, 0f, 0.0f, 0.17f, 0.02f, 0.02f), 0.03f));
                        var fill = Shade(capBlue, x, y - 0.1f, cone);
                        float stripe = Mathf.Abs(Mathf.Sin((x * 0.6f + y) * 55f));
                        fill = Color.Lerp(fill, Color.Lerp(capBlue, PlopPalette.Highlight, 0.45f), 0.35f * Smooth((stripe - 0.75f) / 0.1f));
                        c = Sdf.Over(c, fill, Sdf.Coverage(cone, px));
                        float brim = Sdf.RoundBox(x, y, 0.0f, 0.030f, 0.178f, 0.034f, 0.032f);
                        var brimFill = Shade(cream, x, y - 0.03f, brim);
                        c = Sdf.Over(c, brimFill, Sdf.Coverage(brim, px));
                        float pom = Sdf.Circle(x, y, 0.19f, 0.15f, 0.036f);
                        return Sdf.Over(c, Gloss(Shade(cream, x - 0.19f, y - 0.15f, pom), x, y, 0.178f, 0.165f, 0.012f, 0.008f), Sdf.Coverage(pom, px));
                    };
                case "cloud":
                    return (x, y, px) =>
                    {
                        float cloud = Sdf.Circle(x, y, -0.068f, -0.005f, 0.042f);
                        cloud = Sdf.SmoothMin(cloud, Sdf.Circle(x, y, -0.018f, 0.020f, 0.056f), 0.02f);
                        cloud = Sdf.SmoothMin(cloud, Sdf.Circle(x, y, 0.048f, 0.006f, 0.047f), 0.02f);
                        cloud = Sdf.SmoothMin(cloud, Sdf.Circle(x, y, 0.094f, -0.014f, 0.032f), 0.02f);
                        cloud = Mathf.Max(cloud, -0.038f - y);
                        var fill = Color.Lerp(PlopPalette.Hex(0xDCE6F5), PlopPalette.Highlight, Smooth(0.35f + y * 9f - x * 2f));
                        fill = Color.Lerp(fill, PlopPalette.Hex(0xC9D6EC), 0.5f * (1f - Smooth((y + 0.03f) / 0.025f)));
                        return Sdf.Over(fill.WithAlpha(0f), fill, Sdf.Coverage(cloud, px));
                    };
                case "crown":
                    return (x, y, px) =>
                    {
                        float band = Sdf.RoundBox(x, y, 0f, 0.030f, 0.098f, 0.030f, 0.012f);
                        float points = 10f;
                        for (int i = -1; i <= 1; i++)
                        {
                            float tipX = i * 0.072f, tipY = i == 0 ? 0.128f : 0.110f;
                            float tri = Mathf.Max(Mathf.Abs(x - tipX) * 2.2f + (y - tipY), 0.03f - y);
                            points = Mathf.Min(points, tri * 0.42f);
                            points = Mathf.Min(points, Sdf.Circle(x, y, tipX, tipY, 0.016f));
                        }
                        float crown = Sdf.SmoothMin(band, points, 0.012f);
                        var c = shadow.WithAlpha(0.2f * Sdf.Soft(band + 0.004f, 0.02f));
                        var fill = Gloss(Shade(honey, x, y - 0.05f, crown), x, y, -0.045f, 0.04f, 0.024f, 0.008f, 0.5f);
                        c = Sdf.Over(c, fill, Sdf.Coverage(crown, px));
                        float gem = Sdf.Circle(x, y, 0f, 0.032f, 0.017f);
                        return Sdf.Over(c, Gloss(Shade(berry, x, y - 0.03f, gem), x, y, -0.005f, 0.038f, 0.006f, 0.004f, 0.7f), Sdf.Coverage(gem, px));
                    };
                default:
                    return (x, y, px) => cream.WithAlpha(0f);
            }
        }

        private static float Rotated(float x, float y, float angle, System.Func<float, float, float> sdf)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return sdf(x * c + y * s, -x * s + y * c);
        }

        /// <summary>Soft five-point star (outer radius r, inner radius r·k), first point straight up.</summary>
        internal static float Star5(float x, float y, float r, float k)
        {
            float angle = Mathf.Atan2(x, y);
            float tip = 0.5f + 0.5f * Mathf.Cos(5f * angle);   // 1 on a point, 0 between points
            float radius = Mathf.Lerp(r * k, r, Mathf.Pow(tip, 1.7f));
            return (Mathf.Sqrt(x * x + y * y) - radius) * 0.72f;
        }

        // ------------------------------------------------------------------ move effects

        private void AddEffect(List<Spec> specs, string key, EffectKind kind)
        {
            if (kind == EffectKind.None) return;
            specs.Add(Make(key, -0.08f, -0.08f, 0.16f, 0.16f, EffectPainter(kind), 128));
        }

        private static Painter EffectPainter(EffectKind kind)
        {
            var warm = PlopPalette.Highlight;
            switch (kind)
            {
                case EffectKind.Puff:
                    return (x, y, px) => PlopPalette.GelBounce.WithAlpha(0.85f * Sdf.Soft(
                        Sdf.SmoothMin(Sdf.Circle(x, y, -0.022f, -0.005f, 0.035f), Sdf.Circle(x, y, 0.024f, 0.008f, 0.032f), 0.02f), 0.03f));
                case EffectKind.Sparkle:
                    return (x, y, px) =>
                    {
                        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                        float star = (Mathf.Sqrt(ax) + Mathf.Sqrt(ay) - Mathf.Sqrt(0.05f)) * 0.12f;
                        var c = PlopPalette.Honey.WithAlpha(0.35f * Sdf.Soft(Sdf.Circle(x, y, 0f, 0f, 0.02f), 0.05f));
                        return Sdf.Over(c, warm, Sdf.Coverage(star, px));
                    };
                case EffectKind.Bubble:
                    return (x, y, px) =>
                    {
                        float ring = Mathf.Abs(Sdf.Circle(x, y, 0f, 0f, 0.052f)) - 0.006f;
                        var c = PlopPalette.Sky.WithAlpha(0.22f * Sdf.Coverage(Sdf.Circle(x, y, 0f, 0f, 0.052f), px));
                        c = Sdf.Over(c, Color.Lerp(PlopPalette.Sky, warm, 0.6f), 0.85f * Sdf.Coverage(ring, px));
                        return Sdf.Over(c, warm, 0.9f * Sdf.Coverage(Sdf.Ellipse(x, y, -0.020f, 0.022f, 0.012f, 0.008f), px));
                    };
                default:
                    return (x, y, px) =>
                    {
                        float star = Star5(x, y, 0.062f, 0.46f) - 0.006f;
                        var fill = Color.Lerp(PlopPalette.Honey, warm, Smooth(0.4f + 6f * y - 3f * x));
                        return Sdf.Over(fill.WithAlpha(0f), fill, Sdf.Coverage(star, px));
                    };
            }
        }

        // ------------------------------------------------------------------ Studio thumbnails

        public static string ThumbBodyKey(BodyStyle style, JellyFinish finish, CosmeticPalette palette, int colorId)
            => "t.body/" + style.Name + "/" + finish.Name + "/" + palette.Name + "/" + colorId;
        public static string ThumbShadowKey(BodyStyle style) => "t.shadow/" + style.Name;
        public static string ThumbFaceKey(FaceStyle face, string part) => "t.face/" + face.Name + "/" + part;
        public static string ThumbAccessoryKey(AccessoryStyle accessory) => "t.acc/" + accessory.Name;
        public static string ThumbNestKey(NestStyle nest) => "t.nest/" + nest.Name;
        public static string ThumbEffectKey(MoveEffectStyle effect) => "t.fx/" + effect.Name;

        /// <summary>
        /// One small atlas with every variant the Studio grid shows: each item drawn on a classic Plop.
        /// Parts are shared (a face is baked once, not once per item). Owned by the Studio; disposed on close.
        /// </summary>
        public static ShapeAtlas StudioThumbnails(int pixelsPerUnit)
        {
            return new ShapeAtlas(pixelsPerUnit, CharacterLook.Default, 0, AllColors, specs =>
            {
                var atlas = specs; // keys are unique per variant; duplicates are skipped below
                var seen = new HashSet<string>();
                void Add(Spec spec) { if (seen.Add(spec.Key)) atlas.Add(spec); }
                var d = CharacterLook.Default;
                var helper = new ShapeAtlas(pixelsPerUnit); // painter host (no texture)
                foreach (var style in CharacterArt.Styles.Values)
                {
                    var b = style.Bounds;
                    Add(helper.Make(ThumbBodyKey(style, d.Finish, d.Palette, 0), b.x, b.y, b.width, b.height, new JellyBody(style, d.Finish, d.Palette, 0).Paint));
                    var shadow = new List<Spec>();
                    helper.AddShadow(shadow, ThumbShadowKey(style), style);
                    Add(shadow[0]);
                }
                var classic = d.Style;
                var cb = classic.Bounds;
                foreach (var finish in CharacterArt.Finishes.Values)
                    Add(helper.Make(ThumbBodyKey(classic, finish, d.Palette, 0), cb.x, cb.y, cb.width, cb.height, new JellyBody(classic, finish, d.Palette, 0).Paint));
                foreach (var palette in CharacterArt.Palettes.Values)
                    for (int c = 0; c < 3; c++)
                        Add(helper.Make(ThumbBodyKey(classic, d.Finish, palette, c), cb.x, cb.y, cb.width, cb.height, new JellyBody(classic, d.Finish, palette, c).Paint));
                foreach (var face in CharacterArt.Faces.Values)
                {
                    var parts = new List<Spec>();
                    helper.AddFace(parts, face, "");
                    foreach (var part in parts)
                    {
                        if (part.Key != EyeKey(EyeShape.Open) && part.Key != MouthKey(MouthShape.Neutral) && part.Key != CheekKey) continue;
                        part.Key = ThumbFaceKey(face, part.Key);
                        Add(part);
                    }
                }
                foreach (var accessory in CharacterArt.Accessories.Values)
                {
                    var parts = new List<Spec>();
                    helper.AddAccessory(parts, ThumbAccessoryKey(accessory), accessory);
                    foreach (var part in parts) Add(part);
                }
                foreach (var nest in CharacterArt.Nests.Values)
                {
                    var parts = new List<Spec>();
                    helper.AddStyledNest(parts, ThumbNestKey(nest), nest, d.Palette, 0);
                    foreach (var part in parts) Add(part);
                }
                foreach (var effect in CharacterArt.Effects.Values)
                {
                    var parts = new List<Spec>();
                    helper.AddEffect(parts, ThumbEffectKey(effect), effect.Kind);
                    foreach (var part in parts) Add(part);
                }
            });
        }

        // ------------------------------------------------------------------ nests

        private void AddStyledNest(List<Spec> specs, string key, NestStyle style, CosmeticPalette palette, int colorId)
        {
            if (style.Shape == NestShape.Pillow)
            {
                specs.Add(Make(key, -0.5f, -0.5f, 1f, 1f, PillowPainter(palette.NestOuter(colorId), palette.NestInner(colorId))));
                return;
            }
            var cover = palette.NestOuter(colorId);
            var lining = palette.NestInner(colorId);
            var binding = Color.Lerp(cover, PlopPalette.Ink, 0.34f);
            var piping = Color.Lerp(cover, PlopPalette.Highlight, 0.70f);
            var shape = style.Shape;
            specs.Add(Make(key, -0.5f, -0.5f, 1f, 1f, (x, y, px) =>
            {
                float outline = NestOutline(shape, x, y);
                var c = Sdf.Over(cover.WithAlpha(0f), PlopPalette.ContactShadow, 0.36f * Sdf.Soft(NestOutline(shape, x - 0.020f, y + 0.046f), 0.038f));
                if (shape == NestShape.Marshmallow)
                {
                    // A short soft cylinder: side band below, puffy top face above.
                    float sideBand = Sdf.RoundBox(x, y, 0f, -0.045f, 0.43f, 0.37f, 0.30f);
                    var sideColor = Color.Lerp(binding, cover, 0.45f + 0.35f * Smooth(0.5f - x));
                    c = Sdf.Over(c, sideColor, Sdf.Coverage(sideBand, px));
                }
                float inside = Sdf.Coverage(outline, px);
                if (inside <= 0f) return c;
                float r = Mathf.Max(0.0001f, Mathf.Sqrt(x * x + y * y));
                float light = Mathf.Clamp01(0.5f + 0.5f * ((-0.57f * x + 0.82f * y) / r));
                var hem = Color.Lerp(binding, piping, 0.14f + 0.66f * light);
                var cushion = Color.Lerp(hem, cover, Smooth(-outline / 0.073f));
                float seam = 1f - Smooth(Mathf.Abs(outline + 0.026f) / 0.012f);
                cushion = Color.Lerp(cushion, Color.Lerp(binding, piping, light), 0.38f * seam);

                float seat = NestSeat(shape, x, y);
                float bounce = Smooth(0.55f - 1.31f * y + 0.80f * x);
                var innerHem = Color.Lerp(binding, cover, 0.16f + 0.68f * bounce);
                var seatColor = Color.Lerp(cover, lining, Smooth(0.61f + 1.03f * y - 0.37f * x));
                var inset = Color.Lerp(innerHem, seatColor, Smooth(-seat / 0.084f));
                cushion = Color.Lerp(cushion, inset, Sdf.Soft(seat, 0.025f));

                if (shape == NestShape.Flower)
                {
                    float a = Mathf.Atan2(y, x);
                    float crease = Mathf.Abs(Mathf.Cos(3f * a)) > 0.985f && r > 0.25f ? 1f : 0f;
                    cushion = Color.Lerp(cushion, binding, 0.25f * crease);
                }
                if (shape == NestShape.Moon)
                {
                    float moon = -Sdf.SmoothMin(-Sdf.Circle(x, y, -0.02f, 0.0f, 0.215f), Sdf.Circle(x, y, 0.105f, 0.10f, 0.19f), 0.03f);
                    float moonShadow = -Sdf.SmoothMin(-Sdf.Circle(x - 0.012f, y + 0.024f, -0.02f, 0.0f, 0.215f), Sdf.Circle(x - 0.012f, y + 0.024f, 0.105f, 0.10f, 0.19f), 0.03f);
                    cushion = Color.Lerp(cushion, binding, 0.42f * Sdf.Soft(moonShadow, 0.025f));
                    var moonEdge = Color.Lerp(PlopPalette.TileShade, PlopPalette.Highlight, Smooth(0.55f + 2.6f * y - 2.0f * x));
                    cushion = Color.Lerp(cushion, Color.Lerp(moonEdge, PlopPalette.Tile, Smooth(-moon / 0.03f)), Sdf.Coverage(moon, px));
                }
                if (shape == NestShape.Cloud)
                {
                    float puff = Sdf.Soft(Sdf.Ellipse(x, y, -0.12f, 0.20f, 0.18f, 0.08f), 0.10f);
                    cushion = Color.Lerp(cushion, piping, 0.35f * puff);
                }
                return Sdf.Over(c, cushion, inside);
            }));
        }

        private static float NestOutline(NestShape shape, float x, float y)
        {
            switch (shape)
            {
                case NestShape.Cloud:
                {
                    float d = Sdf.RoundBox(x, y, 0f, -0.02f, 0.30f, 0.26f, 0.2f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f + 0.2f;
                        d = Sdf.SmoothMin(d, Sdf.Circle(x, y, 0.29f * Mathf.Cos(a), 0.27f * Mathf.Sin(a) - 0.01f, 0.15f), 0.05f);
                    }
                    return d;
                }
                case NestShape.Marshmallow:
                    return Sdf.RoundBox(x, y, 0f, 0.035f, 0.43f, 0.35f, 0.30f);
                case NestShape.Flower:
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Atan2(y, x) + 0.26f;
                    return (r - (0.375f + 0.055f * Mathf.Cos(6f * a))) * 0.92f;
                }
                case NestShape.Moon:
                    return Sdf.Circle(x, y, 0f, -0.005f, 0.43f);
                default:
                    return PillowDistance(x, y);
            }
        }

        private static float NestSeat(NestShape shape, float x, float y)
        {
            switch (shape)
            {
                case NestShape.Cloud: return Sdf.Ellipse(x, y, 0f, 0.0f, 0.27f, 0.22f);
                case NestShape.Marshmallow: return Sdf.RoundBox(x, y, 0f, 0.05f, 0.30f, 0.23f, 0.2f);
                case NestShape.Flower: return Sdf.Circle(x, y, 0f, 0f, 0.235f);
                case NestShape.Moon: return Sdf.Circle(x, y, 0f, 0f, 0.31f);
                default: return Sdf.RoundBox(x, y, 0f, 0.008f, 0.299f, 0.268f, 0.138f);
            }
        }
    }
}
