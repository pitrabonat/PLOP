using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Rounded 9-sliced sprites for uGUI (pill buttons, cards and their soft shadows), rasterized once at
    /// 2× resolution with an SDF edge so corners stay crisp on high-density screens.
    /// Sprite pixels-per-unit is 200 against the canvas' 100, i.e. 1 canvas unit = 2 texture pixels.
    /// </summary>
    public static class UiSprites
    {
        private const float PixelsPerUnit = 200f;
        private static Sprite _pill, _pillShadow, _card, _cardShadow;

        /// <summary>Corner radius 64 canvas units (a 128-unit-tall button becomes a perfect pill).</summary>
        public static Sprite Pill => _pill != null ? _pill : (_pill = Rounded("ui-pill", 128, 0));
        public static Sprite PillShadow => _pillShadow != null ? _pillShadow : (_pillShadow = Rounded("ui-pill-shadow", 128, 40));
        /// <summary>Corner radius 44 canvas units.</summary>
        public static Sprite Card => _card != null ? _card : (_card = Rounded("ui-card", 88, 0));
        public static Sprite CardShadow => _cardShadow != null ? _cardShadow : (_cardShadow = Rounded("ui-card-shadow", 88, 56));

        /// <summary>Canvas units a shadow sprite extends beyond the shape on each side.</summary>
        public static float ShadowSpread(Sprite shadow) => shadow == _pillShadow ? 20f : shadow == _cardShadow ? 28f : 0f;

        /// <summary>White rounded square, radius r px; with blur > 0 a soft shadow mask instead.</summary>
        private static Sprite Rounded(string name, int radiusPx, int blurPx)
        {
            int border = radiusPx + blurPx;
            int size = 2 * border + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            float half = size * 0.5f, inner = half - blurPx - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Sdf.RoundBox(x + 0.5f, y + 0.5f, half, half, inner, inner, radiusPx);
                    float a = blurPx > 0 ? Sdf.Soft(d, blurPx) : Sdf.Coverage(d, 1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var b = border + 2;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = name;
            return sprite;
        }
    }
}
