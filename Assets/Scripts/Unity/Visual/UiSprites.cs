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
        private static Sprite _pill, _pillShadow, _card, _cardShadow, _volumeTrack, _control, _controlShadow, _badge;

        /// <summary>The shared capsule corner radius in canvas units.</summary>
        public static Sprite Pill => _pill != null ? _pill : (_pill = Rounded("ui-pill", (int)(HudTheme.PillRadius * 2), 0));
        public static Sprite PillShadow => _pillShadow != null ? _pillShadow : (_pillShadow = Rounded("ui-pill-shadow", (int)(HudTheme.PillRadius * 2), (int)(HudTheme.PillShadowSpread * 2)));
        /// <summary>The shared rounded modal/card corner radius.</summary>
        public static Sprite Card => _card != null ? _card : (_card = Rounded("ui-card", (int)(HudTheme.CardRadius * 2), 0));
        public static Sprite CardShadow => _cardShadow != null ? _cardShadow : (_cardShadow = Rounded("ui-card-shadow", (int)(HudTheme.CardRadius * 2), (int)(HudTheme.CardShadowSpread * 2)));

        internal static Sprite Control => _control != null ? _control :
            (_control = Rounded("ui-control", (int)(HudTheme.ControlRadius * 2), 0, raised: true));
        internal static Sprite ControlShadow => _controlShadow != null ? _controlShadow :
            (_controlShadow = Rounded("ui-control-shadow", (int)(HudTheme.ControlRadius * 2), (int)(HudTheme.PillShadowSpread * 2)));

        internal static Sprite Badge => _badge != null ? _badge :
            (_badge = Rounded("ui-badge", (int)(HudTheme.CardRadius * 2), 0, raised: true));

        public static Sprite VolumeTrack => _volumeTrack != null ? _volumeTrack :
            (_volumeTrack = Rounded("ui-volume-track", (int)(HudTheme.VolumeTrackRadius * 2), 0));

        /// <summary>Canvas units a shadow sprite extends beyond the shape on each side.</summary>
        public static float ShadowSpread(Sprite shadow) => shadow == _pillShadow || shadow == _controlShadow ? HudTheme.PillShadowSpread : shadow == _cardShadow ? HudTheme.CardShadowSpread : 0f;

        /// <summary>White rounded square, radius r px; with blur > 0 a soft shadow mask instead.</summary>
        private static Sprite Rounded(string name, int radiusPx, int blurPx, bool raised = false)
        {
            int border = radiusPx + blurPx;
            int size = 2 * border + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            var cream = new Color(1f, 0.97f, 0.94f);
            var bakedShadow = new Color(0.92f, 0.70f, 0.61f);
            float half = size * 0.5f, inner = half - blurPx - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Sdf.RoundBox(x + 0.5f, y + 0.5f, half, half, inner, inner, radiusPx);
                    float a = blurPx > 0 ? Sdf.Soft(d, blurPx) : Sdf.Coverage(d, 1f);
                    var fill = Color.white;
                    if (raised)
                    {
                        float upper = Mathf.SmoothStep(0f, 1f, ((y - half) - (x - half) * 0.22f) / size + 0.5f);
                        float bevel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-d / 23f));
                        float lip = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - 3f) / 18f))) * 0.35f;
                        fill = Color.Lerp(cream, Color.white, upper * 0.55f);
                        fill = Color.Lerp(fill, Color.Lerp(bakedShadow, Color.white, upper), bevel * 0.64f);
                        fill = Color.Lerp(fill, bakedShadow, lip);
                    }
                    fill.a = a;
                    px[y * size + x] = fill;
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
