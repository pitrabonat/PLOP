using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Placeholder sprites generated once at runtime (no art assets): square tile, circle Plop,
    /// outlined square Nest. Replace with real art later without touching gameplay code.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int Size = 64;
        private static Sprite _square, _circle, _ring;

        public static readonly Color Floor = new Color(0.90f, 0.88f, 0.84f);
        public static readonly Color Wall = new Color(0.42f, 0.43f, 0.47f);
        public static readonly Color Background = new Color(0.20f, 0.22f, 0.28f);

        private static readonly Color[] PlopColors =
        {
            new Color(0.92f, 0.30f, 0.30f), new Color(0.30f, 0.52f, 0.95f), new Color(0.96f, 0.82f, 0.25f),
            new Color(0.35f, 0.80f, 0.40f), new Color(0.65f, 0.40f, 0.85f), new Color(0.98f, 0.58f, 0.22f)
        };

        public static Color ColorOf(Core.PlopColor c) => PlopColors[(int)c];

        public static Sprite Square => _square != null ? _square : (_square = Make((x, y) => 1f));

        public static Sprite Circle => _circle != null ? _circle : (_circle = Make((x, y) =>
        {
            float dx = x - Size * 0.5f + 0.5f, dy = y - Size * 0.5f + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(Size * 0.5f - 1f - d); // 1px anti-aliased edge
        }));

        public static Sprite NestOutline => _ring != null ? _ring : (_ring = Make((x, y) =>
        {
            int border = Size / 9;
            bool edge = x < border || y < border || x >= Size - border || y >= Size - border;
            return edge ? 1f : 0f;
        }));

        private delegate float Shape(int x, int y);

        private static Sprite Make(Shape alpha)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha(x, y));
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // upload and free CPU copy
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
