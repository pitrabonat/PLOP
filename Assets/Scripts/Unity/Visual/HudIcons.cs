using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Small round-ended control glyphs in a single baked alpha sheet.</summary>
    internal static class HudIcons
    {
        internal enum Kind { Levels, Settings, Undo, Restart, Hint, Next, Moves, Lock, Check, Home, Star, Gift, Calendar, Play }

        private const int Size = 64;
        private const int IconCount = 14;
        private static Sprite[] _sprites;

        internal static Sprite Get(Kind kind)
        {
            if (_sprites == null || _sprites[0] == null) Bake();
            return _sprites[(int)kind];
        }

        private static void Bake()
        {
            int width = Size * IconCount;
            var texture = new Texture2D(width, Size, TextureFormat.RGBA32, false)
            {
                name = "PLOP HUD icons", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * Size];
            var sprites = new Sprite[IconCount];
            for (int i = 0; i < IconCount; i++)
            {
                int offset = i * Size;
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float distance = Glyph((Kind)i, new Vector2(x + 0.5f, y + 0.5f));
                    pixels[y * width + offset + x] = new Color(1, 1, 1, Sdf.Coverage(distance, 1f));
                }
                sprites[i] = Sprite.Create(texture, new Rect(offset, 0, Size, Size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                sprites[i].name = "PLOP " + (Kind)i;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sprites = sprites;
        }

        private static float Glyph(Kind kind, Vector2 p)
        {
            switch (kind)
            {
                case Kind.Levels:
                    return Sdf.RoundBox(Mathf.Abs(p.x - 32f), Mathf.Abs(p.y - 32f), 10, 10, 6.2f, 6.2f, 3.4f);
                case Kind.Settings:
                    var q = new Vector2(Mathf.Abs(p.x - 32f), Mathf.Abs(p.y - 32f));
                    float teeth = Min3(Sdf.Circle(q.x, q.y, 17f, 0f, 6.2f),
                        Sdf.Circle(q.x, q.y, 0f, 17f, 6.2f), Sdf.Circle(q.x, q.y, 12.8f, 12.8f, 6.1f));
                    return Mathf.Max(Mathf.Min(q.magnitude - 18f, teeth), 9.2f - q.magnitude);
                case Kind.Undo:
                    return Min4(Arc(p, 40, 29, 14, -1.55f, 1.55f),
                        Stroke(p, 16, 43, 40, 43), Stroke(p, 16, 43, 26, 52), Stroke(p, 16, 43, 26, 34));
                case Kind.Restart:
                    float forward = Min3(Arc(p, 32, 32, 16, 0.22f, 2.80f),
                        Stroke(p, 48, 35, 47, 46), Stroke(p, 48, 35, 37, 37));
                    var reverse = new Vector2(64f - p.x, 64f - p.y);
                    float backward = Min3(Arc(reverse, 32, 32, 16, 0.22f, 2.80f),
                        Stroke(reverse, 48, 35, 47, 46), Stroke(reverse, 48, 35, 37, 37));
                    return Mathf.Min(forward, backward);
                case Kind.Hint:
                    float bulb = Mathf.Max(Sdf.Circle(p.x, p.y, 32, 34, 13),
                        -Sdf.Circle(p.x, p.y, 32, 34, 7.7f));
                    return Min4(bulb, Stroke(p, 28, 15, 36, 15),
                        Stroke(p, 28, 21, 36, 21), Min3(Stroke(p, 32, 51, 32, 56),
                            Stroke(p, 14, 43, 9, 46), Stroke(p, 50, 43, 55, 46)));
                case Kind.Lock:
                    float shackle = Mathf.Max(Arc(p, 32f, 32f, 10f, 0f, Mathf.PI), p.y - 47f);
                    float body = Sdf.RoundBox(p.x, p.y, 32f, 26f, 14f, 11f, 4f);
                    return Mathf.Max(Mathf.Min(shackle, body), -Sdf.Circle(p.x, p.y, 32f, 25f, 2.8f));
                case Kind.Check:
                    return Mathf.Min(Stroke(p, 17f, 31f, 27f, 21f), Stroke(p, 27f, 21f, 48f, 43f));
                case Kind.Home:
                {
                    float roof = Mathf.Min(Stroke(p, 13f, 33f, 32f, 50f), Stroke(p, 32f, 50f, 51f, 33f));
                    float house = Sdf.RoundBox(p.x, p.y, 32f, 23f, 13f, 12f, 3.5f);
                    float door = Sdf.RoundBox(p.x, p.y, 32f, 16f, 4.2f, 7f, 2.5f);
                    return Mathf.Min(roof, Mathf.Max(house, -door));
                }
                case Kind.Star:
                    return Star5(p - new Vector2(32f, 30f), 24f, 0.5f) - 2.2f;
                case Kind.Gift:
                {
                    float box = Sdf.RoundBox(p.x, p.y, 32f, 24f, 16f, 12f, 3.5f);
                    float lid = Sdf.RoundBox(p.x, p.y, 32f, 39f, 19f, 4.5f, 2.5f);
                    float slit = Sdf.RoundBox(p.x, p.y, 32f, 30f, 1.8f, 24f, 0f);
                    float bow = Mathf.Min(Mathf.Abs(Sdf.Circle(p.x, p.y, 26f, 48f, 5f)) - 2.4f,
                        Mathf.Abs(Sdf.Circle(p.x, p.y, 38f, 48f, 5f)) - 2.4f);
                    return Mathf.Min(Mathf.Max(Mathf.Min(box, lid), -slit), bow);
                }
                case Kind.Calendar:
                {
                    float outer = Sdf.RoundBox(p.x, p.y, 32f, 28f, 19f, 17f, 5f);
                    float inner = Sdf.RoundBox(p.x, p.y, 32f, 25f, 14f, 10f, 2.5f);
                    float frame = Mathf.Max(outer, -inner);
                    float rings = Mathf.Min(Stroke(p, 24f, 44f, 24f, 50f), Stroke(p, 40f, 44f, 40f, 50f));
                    return Mathf.Min(Mathf.Min(frame, rings), Sdf.Circle(p.x, p.y, 38f, 22f, 4f));
                }
                case Kind.Play:
                {
                    // Rounded triangle pointing right.
                    float e1 = Edge(p, new Vector2(22f, 14f), new Vector2(22f, 50f));
                    float e2 = Edge(p, new Vector2(22f, 50f), new Vector2(50f, 32f));
                    float e3 = Edge(p, new Vector2(50f, 32f), new Vector2(22f, 14f));
                    return Mathf.Max(Mathf.Max(e1, e2), e3) - 3f;
                }
                case Kind.Moves:
                    return Mathf.Min(Footprint(p - new Vector2(22, 24), -0.23f),
                        Footprint(p - new Vector2(42, 39), 0.29f));
                default:
                    return Min3(Stroke(p, 17, 32, 47, 32), Stroke(p, 47, 32, 35, 44), Stroke(p, 47, 32, 35, 20));
            }
        }

        /// <summary>Signed distance to edge a→b of a clockwise polygon: negative on the inner (right-hand) side.</summary>
        private static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = (b - a).normalized;
            return (p.y - a.y) * d.x - (p.x - a.x) * d.y;
        }

        /// <summary>Five-point star distance (Inigo Quilez), radius r, inner ratio rf.</summary>
        private static float Star5(Vector2 p, float r, float rf)
        {
            var k1 = new Vector2(0.809016994f, -0.587785252f);
            var k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }

        private static float Min3(float a, float b, float c) => Mathf.Min(Mathf.Min(a, b), c);
        private static float Min4(float a, float b, float c, float d) => Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));

        private static float Footprint(Vector2 p, float tilt)
        {
            float across = p.x - p.y * tilt;
            float forward = p.y + p.x * tilt;
            float toes = Sdf.RoundBox(across, forward - 4f, 0f, 0f, 7f, 10f, 6.5f);
            float heel = Sdf.RoundBox(across, forward + 6.3f, 0f, 0f, 5.5f, 6.5f, 4.6f);
            return Mathf.Min(toes, heel);
        }

        private static float Arc(Vector2 p, float x, float y, float radius, float start, float end)
        {
            var delta = p - new Vector2(x, y);
            float angle = Mathf.Atan2(delta.y, delta.x);
            if (angle < start && angle + 2f * Mathf.PI <= end) angle += 2f * Mathf.PI;
            angle = Mathf.Clamp(angle, start, end);
            return (delta - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius).magnitude - 2.7f;
        }

        private static float Stroke(Vector2 p, float x1, float y1, float x2, float y2)
        {
            var start = new Vector2(x1, y1);
            var delta = new Vector2(x2 - x1, y2 - y1);
            float fraction = Mathf.Clamp01(Vector2.Dot(p - start, delta) / delta.sqrMagnitude);
            return (p - start - fraction * delta).magnitude - 2.7f;
        }
    }
}
