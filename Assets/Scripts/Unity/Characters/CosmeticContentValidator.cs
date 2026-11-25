using System;
using System.Collections.Generic;
using Plop.Meta;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Checks cosmetic content end to end: catalog data, recipe registry, accessory attachments, palette
    /// distinctness, material ranges, expression bindings and Studio thumbnails. Used by the Editor validator
    /// (PLOP → Characters → Validate Cosmetics) and by PlayMode tests. Reports every problem; ignores none.
    /// </summary>
    public static class CosmeticContentValidator
    {
        /// <summary>
        /// Smallest allowed RGB distance between two body colours of one palette. The approved Toy Box and
        /// Pastel palettes have neighbours at 0.121–0.132 (Honey/Peach, Berry/Peach, Mint/Cloud Blue); identity
        /// never depends on colour alone because every Plop and Nest carries a matching symbol.
        /// </summary>
        public const float MinPaletteDistance = 0.11f;

        public static List<string> Run(CosmeticCatalog catalog)
        {
            var issues = catalog.Validate(CharacterArt.HasAsset);

            foreach (var kv in CharacterArt.Accessories)
            {
                var a = kv.Value;
                if (a.Anchor == AccessoryAnchor.None) continue;
                if (a.Bounds.width <= 0f || a.Bounds.height <= 0f) issues.Add("accessory:" + kv.Key + ": empty bounds.");
                if (Mathf.Max(a.Bounds.width, a.Bounds.height) > 0.5f)
                    issues.Add("accessory:" + kv.Key + ": larger than half a cell; it would cover the face or grid.");
                foreach (var style in CharacterArt.Styles.Values)
                {
                    Vector2 anchor = (a.Anchor == AccessoryAnchor.Top ? style.TopAnchor : style.SideAnchor) + a.Offset;
                    if (style.Distance(anchor.x, anchor.y) > 0.06f)
                        issues.Add("accessory:" + kv.Key + ": attachment floats off the " + style.Name + " body.");
                }
            }

            foreach (var kv in CharacterArt.Palettes)
            {
                var slots = kv.Value.Slots;
                if (slots == null || slots.Length < PlopPalette.SlotCount) { issues.Add("palette:" + kv.Key + ": needs " + PlopPalette.SlotCount + " colours."); continue; }
                for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                {
                    float d = Vector3.Distance(new Vector3(slots[i].r, slots[i].g, slots[i].b), new Vector3(slots[j].r, slots[j].g, slots[j].b));
                    if (d < MinPaletteDistance)
                        issues.Add("palette:" + kv.Key + ": slots " + i + " and " + j + " are too similar (" + d.ToString("0.000") + ").");
                }
            }

            foreach (var kv in CharacterArt.Finishes)
            {
                var f = kv.Value;
                foreach (var (name, value) in new[] { ("AlphaThin", f.AlphaThin), ("AlphaThick", f.AlphaThick), ("Sheen", f.Sheen), ("Milk", f.Milk), ("Pearl", f.Pearl), ("Glow", f.Glow), ("Velvet", f.Velvet) })
                    if (value < 0f || value > 1f) issues.Add("finish:" + kv.Key + ": " + name + " must be within 0..1.");
                if (f.AlphaThin < 0.75f) issues.Add("finish:" + kv.Key + ": too transparent to read against the board.");
            }

            foreach (var kv in CharacterArt.Styles)
            {
                var s = kv.Value;
                if (s.Personality == null) { issues.Add("style:" + kv.Key + ": missing personality."); continue; }
                if (s.Personality.BlinkMin < 0.8f) issues.Add("style:" + kv.Key + ": blinks too often.");
                if (s.Personality.Reaction <= 0f || s.Personality.Reaction > 1.6f) issues.Add("style:" + kv.Key + ": reaction strength out of range.");
                if (s.Bounds.width > 1.08f) issues.Add("style:" + kv.Key + ": wider than a cell.");
                if (s.Distance(s.EyeL.x, s.EyeL.y) > -0.05f || s.Distance(s.EyeR.x, s.EyeR.y) > -0.05f) issues.Add("style:" + kv.Key + ": eyes are not on the body.");
            }

            // Expression bindings and thumbnails need baked sprites; bake small atlases once.
            ShapeAtlas thumbs = null;
            try
            {
                foreach (var face in CharacterArt.Faces.Values)
                {
                    var look = CharacterLook.Default.With(face: face);
                    var atlas = ShapeAtlas.Build(64, look, ShapeAtlas.Content.Characters, 0);
                    try
                    {
                        foreach (EyeShape e in Enum.GetValues(typeof(EyeShape)))
                            if (!atlas.Has(ShapeAtlas.EyeKey(e))) issues.Add("face:" + face.Name + ": missing eye " + e + ".");
                        foreach (MouthShape m in Enum.GetValues(typeof(MouthShape)))
                            if (!atlas.Has(ShapeAtlas.MouthKey(m))) issues.Add("face:" + face.Name + ": missing mouth " + m + ".");
                        if (!atlas.Has(ShapeAtlas.BrowKey) || !atlas.Has(ShapeAtlas.CheekKey)) issues.Add("face:" + face.Name + ": missing brow or cheek.");
                    }
                    finally { atlas.Dispose(); }
                }

                thumbs = ShapeAtlas.StudioThumbnails(48);
                foreach (var item in catalog.Items)
                {
                    string key = ThumbnailKey(item);
                    if (key != null && !thumbs.Has(key)) issues.Add(item.Id + ": missing Studio thumbnail '" + key + "'.");
                }
            }
            finally { thumbs?.Dispose(); }
            return issues;
        }

        /// <summary>The thumbnail sprite an item's Studio card needs, or null when it reuses the base Plop.</summary>
        public static string ThumbnailKey(CosmeticItem item)
        {
            var d = CharacterLook.Default;
            string name = CosmeticCatalog.AssetName(item.Asset);
            switch (item.Category)
            {
                case CosmeticCategory.BodyStyle:
                    return CharacterArt.Styles.TryGetValue(name, out var s) ? ShapeAtlas.ThumbBodyKey(s, d.Finish, d.Palette, 0) : "t.body/" + name;
                case CosmeticCategory.BodyFinish:
                    return CharacterArt.Finishes.TryGetValue(name, out var f) ? ShapeAtlas.ThumbBodyKey(d.Style, f, d.Palette, 0) : "t.finish/" + name;
                case CosmeticCategory.Palette:
                    return CharacterArt.Palettes.TryGetValue(name, out var p) ? ShapeAtlas.ThumbBodyKey(d.Style, d.Finish, p, 2) : "t.palette/" + name;
                case CosmeticCategory.Face:
                    return CharacterArt.Faces.TryGetValue(name, out var fa) ? ShapeAtlas.ThumbFaceKey(fa, ShapeAtlas.EyeKey(EyeShape.Open)) : "t.face/" + name;
                case CosmeticCategory.Accessory:
                    if (!CharacterArt.Accessories.TryGetValue(name, out var a)) return "t.acc/" + name;
                    return a.Anchor == AccessoryAnchor.None ? null : ShapeAtlas.ThumbAccessoryKey(a);
                case CosmeticCategory.Nest:
                    return CharacterArt.Nests.TryGetValue(name, out var n) ? ShapeAtlas.ThumbNestKey(n) : "t.nest/" + name;
                default:
                    if (!CharacterArt.Effects.TryGetValue(name, out var e)) return "t.fx/" + name;
                    return e.Kind == EffectKind.None ? null : ShapeAtlas.ThumbEffectKey(e);
            }
        }
    }
}
