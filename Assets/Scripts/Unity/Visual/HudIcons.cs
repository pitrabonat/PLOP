using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Small round-ended control glyphs in a single baked alpha sheet.</summary>
    internal static class HudIcons
    {
        internal enum Kind { Levels, Settings, Undo, Restart, Hint, Next }

        private const int Size = 64;
        private const int IconCount = 6;
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
                    return Min3(Rail(p, 21, 25), Rail(p, 32, 43), Rail(p, 43, 27));
                case Kind.Undo:
                    return Min4(Arc(p, 40, 29, 14, -1.55f, 1.55f),
                        Stroke(p, 16, 43, 40, 43), Stroke(p, 16, 43, 26, 52), Stroke(p, 16, 43, 26, 34));
                case Kind.Restart:
                    return Min3(Arc(p, 32, 31, 17, -1.55f, 3.16f),
                        Stroke(p, 15, 30, 11, 43), Stroke(p, 15, 30, 26, 36));
                case Kind.Hint:
                    return Min4(Stroke(p, 32, 16, 32, 48), Stroke(p, 16, 32, 48, 32),
                        Stroke(p, 24, 24, 40, 40), Stroke(p, 24, 40, 40, 24));
                default:
                    return Min3(Stroke(p, 17, 32, 47, 32), Stroke(p, 47, 32, 35, 44), Stroke(p, 47, 32, 35, 20));
            }
        }

        private static float Min3(float a, float b, float c) => Mathf.Min(Mathf.Min(a, b), c);
        private static float Min4(float a, float b, float c, float d) => Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));

        private static float Rail(Vector2 p, float y, float thumb) =>
            Mathf.Min(Stroke(p, 15, y, 49, y), Sdf.Circle(p.x, p.y, thumb, y, 4.4f));

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
