using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Tray (board frame) finish: what the rim is made of.</summary>
    public enum TrayFinish : byte { Satin, Frost, Wood, Stone, Glass, Ceramic, Celestial }
    /// <summary>Floor tile finish: always quieter than mechanics.</summary>
    public enum TileFinish : byte { Satin, Speckle, Frosted, Glazed }
    public enum PebbleStyle : byte { Toy, Stone, Ice, Crystal, Ceramic, Moon }
    public enum PebbleCap : byte { None, Snow, Moss, Ember, Dew }

    /// <summary>
    /// One World's board materials (Phase 11.5). Data lives in Resources/Worlds/BoardMaterials.json so art can be
    /// tuned without code. Geometry is shared by every World; only finish parameters and colours differ.
    /// </summary>
    [Serializable]
    public sealed class BoardMaterialProfile
    {
        public string world = "";
        public string tray = "satin";
        public string trayBase = "#EBC4AE", trayRim = "#F5D5BF", trayBed = "#EBC4AE", trayAccent = "#FFFFFF";
        public string tile = "satin";
        public float tileDetail = 0.5f;
        public string pebble = "toy";
        public string pebbleTop = "#C8B6C2", pebbleSide = "#A18A96", pebbleHighlight = "#DFD0D9";
        public string cap = "none";
        public string capColor = "#FFFFFF";
        public string wind = "#78C8B8";

        [NonSerialized] public TrayFinish TrayFinish;
        [NonSerialized] public TileFinish TileFinish;
        [NonSerialized] public PebbleStyle PebbleStyle;
        [NonSerialized] public PebbleCap PebbleCap;
        [NonSerialized] public Color TrayBase, TrayRim, TrayBed, TrayAccent, PebbleTop, PebbleSide, PebbleHighlight, CapColor, Wind;

        public string Id => world;

        internal void Resolve()
        {
            TrayFinish = Parse(tray, TrayFinish.Satin);
            TileFinish = Parse(tile, TileFinish.Satin);
            PebbleStyle = Parse(pebble, PebbleStyle.Toy);
            PebbleCap = Parse(cap, PebbleCap.None);
            TrayBase = Hex(trayBase); TrayRim = Hex(trayRim); TrayBed = Hex(trayBed); TrayAccent = Hex(trayAccent);
            PebbleTop = Hex(pebbleTop); PebbleSide = Hex(pebbleSide); PebbleHighlight = Hex(pebbleHighlight);
            CapColor = Hex(capColor); Wind = Hex(wind);
            tileDetail = Mathf.Clamp01(tileDetail);
        }

        private static T Parse<T>(string s, T fallback) where T : struct
            => Enum.TryParse(s, true, out T v) ? v : fallback;

        private static Color Hex(string s) => ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;
    }

    public static class BoardMaterials
    {
        public const string Resource = "Worlds/BoardMaterials";

        [Serializable]
        private sealed class Set { public BoardMaterialProfile[] profiles = Array.Empty<BoardMaterialProfile>(); }

        private static Dictionary<string, BoardMaterialProfile> _byWorld;

        public static IReadOnlyDictionary<string, BoardMaterialProfile> All { get { Load(); return _byWorld; } }

        /// <summary>The profile for a World id, or null (the board then keeps the default Cozy materials).</summary>
        public static BoardMaterialProfile For(string worldId)
        {
            Load();
            return worldId != null && _byWorld.TryGetValue(worldId, out var p) ? p : null;
        }

        public static BoardMaterialProfile For(WorldTheme world) => world == null ? null : For(world.Id);

        private static void Load()
        {
            if (_byWorld != null) return;
            _byWorld = new Dictionary<string, BoardMaterialProfile>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return;
            var set = JsonUtility.FromJson<Set>(asset.text);
            foreach (var p in set?.profiles ?? Array.Empty<BoardMaterialProfile>())
            {
                if (string.IsNullOrEmpty(p.world)) continue;
                p.Resolve();
                _byWorld[p.world] = p;
            }
        }
    }
}
