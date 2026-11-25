using System;
using System.Collections.Generic;
using System.Text;
using Plop.Meta;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Visual-only personality of a body style. Never feeds the simulation or input timing.</summary>
    public sealed class Personality
    {
        public float BlinkMin = 2.6f, BlinkRange = 3.7f;   // seconds between blinks
        public float BreathAmount = 0.023f;                // idle scale breathing
        public float BreathPeriod = 3.4f;
        public float Wobble = 0.85f;                       // idle tilt in degrees
        public float Reaction = 1f;                        // squash/recoil multiplier (cosmetic)
        public float NeutralEyeOpen = 1f;                  // relaxed lids for sleepy characters
        public Expression Neutral = Expression.Neutral;
    }

    /// <summary>Body silhouette: a softly inflated dome joined to a three-lobed skirt.</summary>
    public sealed class BodyStyle
    {
        public string Name;
        public float DomeX, DomeY, DomeRx, DomeRy, Lean, Flatten;
        public float SkirtScale = 1f, SkirtHeight = 1f;
        public Personality Personality = new Personality();
        // Classic reference geometry; every anchor maps from it onto this dome.
        private const float RefX = -0.022f, RefY = 0.329f, RefRx = 0.371f, RefRy = 0.382f;

        public Vector2 MapFromClassic(float x, float y)
            => new Vector2(DomeX + (x - RefX) * DomeRx / RefRx, DomeY + (y - RefY) * DomeRy / RefRy);

        public float Spacing => DomeRx / RefRx;
        public Vector2 EyeL => MapFromClassic(-0.138f, 0.407f);
        public Vector2 EyeR => MapFromClassic(0.122f, 0.403f);
        public Vector2 Mouth => MapFromClassic(-0.005f, 0.300f);
        public Vector2 CheekL => MapFromClassic(-0.219f, 0.320f);
        public Vector2 CheekR => MapFromClassic(0.204f, 0.319f);
        public Vector2 BrowL => MapFromClassic(-0.140f, 0.488f);
        public Vector2 BrowR => MapFromClassic(0.124f, 0.484f);
        public Vector2 TopAnchor => new Vector2(DomeX + 0.02f, DomeY + DomeRy - 0.035f);
        public Vector2 SideAnchor => MapFromClassic(0.215f, 0.585f);

        /// <summary>Sprite bounds (x0, y0, width, height) in cell units, origin at the ground centre.</summary>
        public Rect Bounds
        {
            get
            {
                float half = Mathf.Max(DomeRx + Mathf.Abs(DomeX) + 0.07f, 0.41f * SkirtScale + 0.07f);
                return new Rect(-half, -0.06f, 2f * half, DomeY + DomeRy + 0.12f);
            }
        }

        /// <summary>Signed distance to the silhouette (negative inside).</summary>
        public float Distance(float x, float y)
        {
            float dome = Sdf.Ellipse(x + Mathf.Max(0f, y - (DomeY + 0.09f)) * Lean, y, DomeX, DomeY, DomeRx, DomeRy);
            dome = -Sdf.SmoothMin(-dome, y - Flatten, 0.051f);
            float s = SkirtScale, h = SkirtHeight;
            float center = Sdf.Ellipse(x, y, -0.003f, 0.130f * h, 0.249f * s, 0.129f * h);
            float left = Sdf.Ellipse(x, y, -0.273f * s, 0.150f * h, 0.124f * s, 0.137f * h);
            float right = Sdf.Ellipse(x, y, 0.269f * s, 0.156f * h, 0.128f * s, 0.140f * h);
            float skirt = Sdf.SmoothMin(center, Mathf.Min(left, right), 0.041f);
            return Sdf.SmoothMin(dome, skirt, 0.056f);
        }
    }

    /// <summary>Material parameters of the baked jelly shading. Values are blend weights, not physical units.</summary>
    public sealed class JellyFinish
    {
        public string Name;
        public float Scatter = 0.51f, Lit = 0.34f, Core = 0.25f;
        public float CoreDepth = 0.72f, Diffuse = 0.61f, Rim = 0.60f, Bounce = 0.83f;
        public float Softbox = 0.52f, Sheen = 0.77f, SheenSize = 1f, SheenSoftness = 1f, Secondary = 0.40f;
        public float AlphaThin = 0.89f, AlphaThick = 0.99f;
        public float Milk;     // creamy, more opaque body
        public float Pearl;    // angle-dependent pastel sheen
        public float Glow;     // brighter thin edge
        public float Velvet;   // matte centre with a soft grazing rim
        public float Dew;      // extra droplet highlights
        public JellyFinish Clone() => (JellyFinish)MemberwiseClone();
    }

    /// <summary>Six body colours, one per logical colour slot (identity order never changes).</summary>
    public sealed class CosmeticPalette
    {
        public string Name;
        public Color[] Slots;
        public Color Body(int colorId) => Color.Lerp(Slots[Mathf.Clamp(colorId, 0, Slots.Length - 1)], PlopPalette.Ink, 0.04f);
        public Color Toy(int colorId) => Slots[Mathf.Clamp(colorId, 0, Slots.Length - 1)];
        public Color NestOuter(int colorId) => Color.Lerp(Toy(colorId), PlopPalette.TrayRim, 0.14f);
        public Color NestInner(int colorId) => Color.Lerp(Toy(colorId), PlopPalette.Highlight, 0.32f);
    }

    public enum EyeKind { Classic, Button, Dozy, Starry, Blush }
    public enum MouthKind { Smile, Cat }

    public sealed class FaceStyle
    {
        public string Name;
        public EyeKind Eye;
        public MouthKind Mouth = MouthKind.Smile;
        public float CheekScale = 1f, CheekAlpha = 0.48f;
    }

    public enum AccessoryAnchor { None, Top, Side }

    public sealed class AccessoryStyle
    {
        public string Name;
        public AccessoryAnchor Anchor;
        public Vector2 Offset;       // from the anchor, cell units
        public float Rotation;       // degrees
        public Rect Bounds;          // sprite bounds around its own pivot (the attachment point)
    }

    public enum NestShape { Pillow, Cloud, Marshmallow, Flower, Moon }

    public sealed class NestStyle
    {
        public string Name;
        public NestShape Shape;
    }

    public enum EffectKind { None, Puff, Sparkle, Bubble, Stars }

    public sealed class MoveEffectStyle
    {
        public string Name;
        public EffectKind Kind;
        public int Particles;
        public float Life;
    }

    /// <summary>
    /// A resolved cosmetic look: one recipe per slot. Built from the equipped <see cref="CharacterPreset"/>;
    /// shared by gameplay, menu and Studio. <see cref="Key"/> identifies baked atlases.
    /// </summary>
    public sealed class CharacterLook
    {
        public BodyStyle Style;
        public JellyFinish Finish;
        public CosmeticPalette Palette;
        public FaceStyle Face;
        public AccessoryStyle Accessory;
        public NestStyle Nest;
        public MoveEffectStyle Effect;
        public string Key;

        public static CharacterLook Default => CharacterArt.DefaultLook;

        public CharacterLook With(BodyStyle style = null, JellyFinish finish = null, CosmeticPalette palette = null,
            FaceStyle face = null, AccessoryStyle accessory = null, NestStyle nest = null, MoveEffectStyle effect = null)
            => CharacterArt.Compose(style ?? Style, finish ?? Finish, palette ?? Palette, face ?? Face,
                accessory ?? Accessory, nest ?? Nest, effect ?? Effect);
    }

    /// <summary>
    /// Registry of every cosmetic recipe, keyed by catalog asset ("finish:pearl"). The catalog decides prices and
    /// names; this class decides how an asset looks. Adding an asset here plus a catalog entry is all a new
    /// cosmetic needs: no new prefab, rig or material.
    /// </summary>
    public static class CharacterArt
    {
        public static readonly Dictionary<string, BodyStyle> Styles = new Dictionary<string, BodyStyle>
        {
            ["classic"] = new BodyStyle { Name = "classic", DomeX = -0.022f, DomeY = 0.329f, DomeRx = 0.371f, DomeRy = 0.382f, Lean = 0.10f, Flatten = 0.067f },
            ["bouncy"] = new BodyStyle
            {
                Name = "bouncy", DomeX = -0.008f, DomeY = 0.366f, DomeRx = 0.348f, DomeRy = 0.432f, Lean = 0.11f, Flatten = 0.072f,
                SkirtScale = 0.92f, SkirtHeight = 1.04f,
                Personality = new Personality { BlinkMin = 1.9f, BlinkRange = 2.6f, BreathAmount = 0.030f, BreathPeriod = 2.6f, Wobble = 1.5f, Reaction = 1.2f, Neutral = Expression.Neutral }
            },
            ["snoozy"] = new BodyStyle
            {
                Name = "snoozy", DomeX = -0.030f, DomeY = 0.290f, DomeRx = 0.418f, DomeRy = 0.320f, Lean = 0.06f, Flatten = 0.060f,
                SkirtScale = 1.12f, SkirtHeight = 0.92f,
                Personality = new Personality { BlinkMin = 3.8f, BlinkRange = 4.5f, BreathAmount = 0.019f, BreathPeriod = 4.6f, Wobble = 0.5f, Reaction = 0.8f, NeutralEyeOpen = 0.74f, Neutral = Expression.Neutral }
            },
        };

        public static readonly Dictionary<string, JellyFinish> Finishes = new Dictionary<string, JellyFinish>
        {
            ["classic"] = new JellyFinish { Name = "classic" },
            ["frosted"] = new JellyFinish { Name = "frosted", Sheen = 0.42f, SheenSize = 1.30f, SheenSoftness = 2.3f, Softbox = 0.64f, Secondary = 0.16f, Milk = 0.15f, AlphaThin = 0.95f, Rim = 0.50f, Diffuse = 0.50f },
            ["milky"] = new JellyFinish { Name = "milky", Milk = 0.40f, AlphaThin = 0.98f, AlphaThick = 1f, Rim = 0.30f, CoreDepth = 0.38f, Sheen = 0.62f, Secondary = 0.30f, Diffuse = 0.48f },
            ["pearl"] = new JellyFinish { Name = "pearl", Pearl = 0.58f, Sheen = 0.86f, Secondary = 0.55f, Milk = 0.10f, AlphaThin = 0.94f, Rim = 0.55f },
            ["dewdrop"] = new JellyFinish { Name = "dewdrop", Glow = 0.55f, Sheen = 0.95f, SheenSize = 0.82f, SheenSoftness = 0.75f, Secondary = 0.65f, AlphaThin = 0.82f, CoreDepth = 0.86f, Rim = 0.72f, Dew = 1f },
            ["velvet"] = new JellyFinish { Name = "velvet", Velvet = 1f, Sheen = 0.10f, Secondary = 0f, Softbox = 0.22f, Diffuse = 0.36f, AlphaThin = 1f, AlphaThick = 1f, Rim = 0.22f, Milk = 0.05f },
        };

        public static readonly Dictionary<string, CosmeticPalette> Palettes = new Dictionary<string, CosmeticPalette>
        {
            ["classic"] = new CosmeticPalette { Name = "classic", Slots = new[] { PlopPalette.ToySlot(0), PlopPalette.ToySlot(1), PlopPalette.ToySlot(2), PlopPalette.ToySlot(3), PlopPalette.ToySlot(4), PlopPalette.ToySlot(5) } },
            // The approved brief colours: Berry, Mint, Honey, Lilac, Cloud Blue, Peach.
            ["pastel"] = new CosmeticPalette { Name = "pastel", Slots = new[] { PlopPalette.Hex(0xE9AEC4), PlopPalette.Hex(0xA8D7CD), PlopPalette.Hex(0xE7BC85), PlopPalette.Hex(0xB6ADE3), PlopPalette.Hex(0xAFCDEB), PlopPalette.Hex(0xF3BEA9) } },
            // Brighter fruit tones: strawberry, blue raspberry, lemon, grape, pistachio, apricot.
            ["sorbet"] = new CosmeticPalette { Name = "sorbet", Slots = new[] { PlopPalette.Hex(0xF4899F), PlopPalette.Hex(0x86C3EA), PlopPalette.Hex(0xF3CB62), PlopPalette.Hex(0xB69BEC), PlopPalette.Hex(0xA9D78F), PlopPalette.Hex(0xF5A06C) } },
        };

        public static readonly Dictionary<string, FaceStyle> Faces = new Dictionary<string, FaceStyle>
        {
            ["classic"] = new FaceStyle { Name = "classic", Eye = EyeKind.Classic },
            ["button"] = new FaceStyle { Name = "button", Eye = EyeKind.Button, CheekScale = 0.85f },
            ["sleepy"] = new FaceStyle { Name = "sleepy", Eye = EyeKind.Dozy, CheekScale = 1.05f },
            ["blush"] = new FaceStyle { Name = "blush", Eye = EyeKind.Blush, Mouth = MouthKind.Cat, CheekScale = 1.35f, CheekAlpha = 0.62f },
            ["starry"] = new FaceStyle { Name = "starry", Eye = EyeKind.Starry },
        };

        public static readonly Dictionary<string, AccessoryStyle> Accessories = new Dictionary<string, AccessoryStyle>
        {
            ["none"] = new AccessoryStyle { Name = "none", Anchor = AccessoryAnchor.None },
            ["bow"] = new AccessoryStyle { Name = "bow", Anchor = AccessoryAnchor.Side, Offset = new Vector2(0.005f, 0.010f), Rotation = -14f, Bounds = new Rect(-0.16f, -0.10f, 0.32f, 0.20f) },
            ["flower"] = new AccessoryStyle { Name = "flower", Anchor = AccessoryAnchor.Side, Offset = new Vector2(0.006f, 0.016f), Rotation = 8f, Bounds = new Rect(-0.11f, -0.11f, 0.22f, 0.22f) },
            ["leaf"] = new AccessoryStyle { Name = "leaf", Anchor = AccessoryAnchor.Top, Offset = new Vector2(0.0f, -0.010f), Rotation = 0f, Bounds = new Rect(-0.12f, -0.02f, 0.24f, 0.22f) },
            ["ribbon"] = new AccessoryStyle { Name = "ribbon", Anchor = AccessoryAnchor.Side, Offset = new Vector2(0.000f, 0.005f), Rotation = -22f, Bounds = new Rect(-0.16f, -0.20f, 0.32f, 0.30f) },
            ["star"] = new AccessoryStyle { Name = "star", Anchor = AccessoryAnchor.Side, Offset = new Vector2(0.012f, 0.020f), Rotation = 12f, Bounds = new Rect(-0.10f, -0.10f, 0.20f, 0.20f) },
            ["nightcap"] = new AccessoryStyle { Name = "nightcap", Anchor = AccessoryAnchor.Top, Offset = new Vector2(0.01f, -0.050f), Rotation = -6f, Bounds = new Rect(-0.22f, -0.03f, 0.46f, 0.30f) },
            ["cloud"] = new AccessoryStyle { Name = "cloud", Anchor = AccessoryAnchor.Top, Offset = new Vector2(0.0f, 0.030f), Rotation = 0f, Bounds = new Rect(-0.15f, -0.07f, 0.30f, 0.16f) },
            ["crown"] = new AccessoryStyle { Name = "crown", Anchor = AccessoryAnchor.Top, Offset = new Vector2(0.0f, -0.022f), Rotation = 4f, Bounds = new Rect(-0.12f, -0.03f, 0.24f, 0.18f) },
        };

        public static readonly Dictionary<string, NestStyle> Nests = new Dictionary<string, NestStyle>
        {
            ["classic"] = new NestStyle { Name = "classic", Shape = NestShape.Pillow },
            ["cloud"] = new NestStyle { Name = "cloud", Shape = NestShape.Cloud },
            ["marshmallow"] = new NestStyle { Name = "marshmallow", Shape = NestShape.Marshmallow },
            ["flower"] = new NestStyle { Name = "flower", Shape = NestShape.Flower },
            ["moon"] = new NestStyle { Name = "moon", Shape = NestShape.Moon },
        };

        public static readonly Dictionary<string, MoveEffectStyle> Effects = new Dictionary<string, MoveEffectStyle>
        {
            ["none"] = new MoveEffectStyle { Name = "none", Kind = EffectKind.None },
            ["puff"] = new MoveEffectStyle { Name = "puff", Kind = EffectKind.Puff, Particles = 2, Life = 0.34f },
            ["sparkle"] = new MoveEffectStyle { Name = "sparkle", Kind = EffectKind.Sparkle, Particles = 2, Life = 0.36f },
            ["bubble"] = new MoveEffectStyle { Name = "bubble", Kind = EffectKind.Bubble, Particles = 3, Life = 0.42f },
            ["stars"] = new MoveEffectStyle { Name = "stars", Kind = EffectKind.Stars, Particles = 2, Life = 0.40f },
        };

        private static CharacterLook _default;
        public static CharacterLook DefaultLook => _default ?? (_default = Compose(Styles["classic"], Finishes["classic"],
            Palettes["classic"], Faces["classic"], Accessories["none"], Nests["classic"], Effects["none"]));

        public static CharacterLook Compose(BodyStyle style, JellyFinish finish, CosmeticPalette palette, FaceStyle face,
            AccessoryStyle accessory, NestStyle nest, MoveEffectStyle effect)
        {
            var key = new StringBuilder();
            key.Append(style.Name).Append('/').Append(finish.Name).Append('/').Append(palette.Name).Append('/')
               .Append(face.Name).Append('/').Append(accessory.Name).Append('/').Append(nest.Name).Append('/').Append(effect.Name);
            return new CharacterLook
            {
                Style = style, Finish = finish, Palette = palette, Face = face, Accessory = accessory, Nest = nest,
                Effect = effect, Key = key.ToString()
            };
        }

        /// <summary>True when an asset reference such as "nest:cloud" has a recipe. Used by the validator.</summary>
        public static bool HasAsset(string asset)
        {
            if (string.IsNullOrEmpty(asset)) return false;
            string name = CosmeticCatalog.AssetName(asset);
            if (asset.StartsWith("style:", StringComparison.Ordinal)) return Styles.ContainsKey(name);
            if (asset.StartsWith("finish:", StringComparison.Ordinal)) return Finishes.ContainsKey(name);
            if (asset.StartsWith("palette:", StringComparison.Ordinal)) return Palettes.ContainsKey(name);
            if (asset.StartsWith("face:", StringComparison.Ordinal)) return Faces.ContainsKey(name);
            if (asset.StartsWith("accessory:", StringComparison.Ordinal)) return Accessories.ContainsKey(name);
            if (asset.StartsWith("nest:", StringComparison.Ordinal)) return Nests.ContainsKey(name);
            if (asset.StartsWith("effect:", StringComparison.Ordinal)) return Effects.ContainsKey(name);
            return false;
        }

        /// <summary>Resolves a preset; any missing item or recipe falls back to the default for that slot.</summary>
        public static CharacterLook Resolve(CharacterPreset preset, CosmeticCatalog catalog)
        {
            string Asset(CosmeticCategory c)
            {
                var item = catalog?.Find(preset?.Get(c)) ?? catalog?.Default(c);
                return item == null ? null : CosmeticCatalog.AssetName(item.Asset);
            }
            var d = DefaultLook;
            return Compose(
                Lookup(Styles, Asset(CosmeticCategory.BodyStyle), d.Style),
                Lookup(Finishes, Asset(CosmeticCategory.BodyFinish), d.Finish),
                Lookup(Palettes, Asset(CosmeticCategory.Palette), d.Palette),
                Lookup(Faces, Asset(CosmeticCategory.Face), d.Face),
                Lookup(Accessories, Asset(CosmeticCategory.Accessory), d.Accessory),
                Lookup(Nests, Asset(CosmeticCategory.Nest), d.Nest),
                Lookup(Effects, Asset(CosmeticCategory.Effect), d.Effect));
        }

        /// <summary>Applies a single catalog item to a look (Studio preview of an unowned item).</summary>
        public static CharacterLook WithItem(CharacterLook look, CosmeticItem item)
        {
            if (item == null) return look;
            string name = CosmeticCatalog.AssetName(item.Asset);
            switch (item.Category)
            {
                case CosmeticCategory.BodyStyle: return look.With(style: Lookup(Styles, name, look.Style));
                case CosmeticCategory.BodyFinish: return look.With(finish: Lookup(Finishes, name, look.Finish));
                case CosmeticCategory.Palette: return look.With(palette: Lookup(Palettes, name, look.Palette));
                case CosmeticCategory.Face: return look.With(face: Lookup(Faces, name, look.Face));
                case CosmeticCategory.Accessory: return look.With(accessory: Lookup(Accessories, name, look.Accessory));
                case CosmeticCategory.Nest: return look.With(nest: Lookup(Nests, name, look.Nest));
                default: return look.With(effect: Lookup(Effects, name, look.Effect));
            }
        }

        private static T Lookup<T>(Dictionary<string, T> table, string name, T fallback) where T : class
            => name != null && table.TryGetValue(name, out var value) ? value : fallback;
    }
}
