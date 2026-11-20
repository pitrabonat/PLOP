using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Signed-distance helpers used to rasterize the procedural shapes (negative = inside, units of the
    /// sprite's coordinate space) plus straight-alpha compositing. Used only when building atlases.
    /// </summary>
    public static class Sdf
    {
        /// <summary>Rounded rectangle centered at (cx, cy) with half extents (hx, hy) and corner radius r.</summary>
        public static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r;
            float qy = Mathf.Abs(y - cy) - hy + r;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        public static float Circle(float x, float y, float cx, float cy, float r)
            => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        /// <summary>Approximate ellipse distance (exact enough for small anti-aliased features).</summary>
        public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        /// <summary>Lower circular arc ("u" shape) of radius R around (cx, cy), half-angle a (radians), stroke t.</summary>
        public static float LowerArc(float x, float y, float cx, float cy, float radius, float halfAngle, float thickness)
        {
            float dx = x - cx, dy = y - cy;
            float angle = Mathf.Atan2(dx, -dy);              // 0 = straight down
            float clamped = Mathf.Clamp(angle, -halfAngle, halfAngle);
            float px = cx + radius * Mathf.Sin(clamped), py = cy - radius * Mathf.Cos(clamped);
            return Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - thickness;
        }

        /// <summary>Polynomial smooth minimum (soft union of two shapes).</summary>
        public static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>Anti-aliased coverage of a hard edge: one pixel wide transition.</summary>
        public static float Coverage(float d, float pixel) => Mathf.Clamp01(0.5f - d / pixel);

        /// <summary>Soft coverage for shadows/glows: fades over 'blur' units outside the shape.</summary>
        public static float Soft(float d, float blur)
        {
            float t = Mathf.Clamp01((d + blur * 0.25f) / (blur * 1.25f));
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>Composites src over dst (straight alpha). Painters start every pixel as the shape's main
        /// color with alpha 0; fully transparent results keep that RGB so bilinear filtering at edges never
        /// bleeds in black or a highlight color.</summary>
        public static Color Over(Color dst, Color src, float srcAlpha)
        {
            float sa = Mathf.Clamp01(src.a * srcAlpha);
            float da = dst.a;
            float outA = sa + da * (1f - sa);
            if (outA <= 1e-5f) return new Color(dst.r, dst.g, dst.b, 0f);
            float k = da * (1f - sa);
            return new Color(
                (src.r * sa + dst.r * k) / outA,
                (src.g * sa + dst.g * k) / outA,
                (src.b * sa + dst.b * k) / outA,
                outA);
        }
    }
}
