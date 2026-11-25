using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta
{
    /// <summary>
    /// One equipped appearance: an item ID per <see cref="CosmeticCategory"/>. Applied to every Plop in a level
    /// (one global look); the logical colour of each Plop still decides its body colour and match mark.
    /// </summary>
    public sealed class CharacterPreset
    {
        private readonly Dictionary<CosmeticCategory, string> _slots = new Dictionary<CosmeticCategory, string>();

        public string Get(CosmeticCategory category) => _slots.TryGetValue(category, out var id) ? id : null;
        public void Set(CosmeticCategory category, string id) => _slots[category] = id;

        public CharacterPreset Clone()
        {
            var copy = new CharacterPreset();
            foreach (var kv in _slots) copy._slots[kv.Key] = kv.Value;
            return copy;
        }

        public bool SameAs(CharacterPreset other)
        {
            if (other == null) return false;
            foreach (var c in CosmeticCatalog.Categories) if (Get(c) != other.Get(c)) return false;
            return true;
        }

        public static CharacterPreset Defaults(CosmeticCatalog catalog)
        {
            var p = new CharacterPreset();
            foreach (var c in CosmeticCatalog.Categories) p.Set(c, catalog.DefaultId(c));
            return p;
        }

        /// <summary>JSON key for each slot, stable across versions.</summary>
        public static string SlotKey(CosmeticCategory c)
        {
            switch (c)
            {
                case CosmeticCategory.BodyStyle: return "body";
                case CosmeticCategory.BodyFinish: return "finish";
                case CosmeticCategory.Palette: return "palette";
                case CosmeticCategory.Face: return "face";
                case CosmeticCategory.Accessory: return "accessory";
                case CosmeticCategory.Nest: return "nest";
                case CosmeticCategory.Board: return "board";
                default: return "effect";
            }
        }

        public void Write(JsonWriter w)
        {
            w.BeginObject();
            foreach (var c in CosmeticCatalog.Categories)
            {
                var id = Get(c);
                if (id != null) w.Key(SlotKey(c)).Value(id);
            }
            w.EndObject();
        }

        public static CharacterPreset Read(Dictionary<string, object> o)
        {
            var p = new CharacterPreset();
            foreach (var c in CosmeticCatalog.Categories)
                if (o.TryGetValue(SlotKey(c), out var v) && v is string id) p.Set(c, id);
            return p;
        }

        /// <summary>
        /// Replaces unknown, wrong-category, unowned or incompatible items with the category default.
        /// Returns how many slots were repaired (0 for a healthy preset).
        /// </summary>
        public int Sanitize(CosmeticCatalog catalog, Func<CosmeticItem, bool> isOwned)
        {
            int repaired = 0;
            foreach (var c in CosmeticCatalog.Categories)
            {
                var item = catalog.Find(Get(c));
                if (item != null && item.Category == c && (isOwned == null || isOwned(item))) continue;
                Set(c, catalog.DefaultId(c));
                repaired++;
            }
            var style = Get(CosmeticCategory.BodyStyle);
            foreach (var c in CosmeticCatalog.Categories)
            {
                var item = catalog.Find(Get(c));
                if (item == null || item.CompatibleWith(style)) continue;
                Set(c, catalog.DefaultId(c));
                repaired++;
            }
            return repaired;
        }
    }

    /// <summary>
    /// Saved appearance (<c>plop.appearance.v1</c>): the active preset index and the preset list (one preset
    /// today; the format already holds several). Missing or corrupt data falls back to catalog defaults and
    /// never touches campaign progress or the wallet.
    /// </summary>
    public sealed class AppearanceStore
    {
        public const string StoreKey = "plop.appearance.v1";
        public const int Version = 1;
        public const int MaxPresets = 4;

        private readonly IKeyValueStore _store;
        private readonly CosmeticCatalog _catalog;
        private readonly List<CharacterPreset> _presets = new List<CharacterPreset>();

        public int ActiveIndex { get; private set; }
        public CharacterPreset Active => _presets[ActiveIndex];
        public IReadOnlyList<CharacterPreset> Presets => _presets;
        /// <summary>Content version recorded in the save (0 when no appearance was ever saved).</summary>
        public int SavedContentVersion { get; private set; }
        public bool LoadedCorrupt { get; private set; }

        public AppearanceStore(IKeyValueStore store, CosmeticCatalog catalog)
        {
            _store = store;
            _catalog = catalog;
            Load();
            if (_presets.Count == 0) _presets.Add(CharacterPreset.Defaults(catalog));
            ActiveIndex = Math.Max(0, Math.Min(ActiveIndex, _presets.Count - 1));
        }

        public void SetActive(CharacterPreset preset)
        {
            _presets[ActiveIndex] = preset.Clone();
            Persist();
        }

        /// <summary>Repairs every preset against the catalog and ownership. Persists only if something changed.</summary>
        public int Sanitize(Func<CosmeticItem, bool> isOwned)
        {
            int repaired = 0;
            foreach (var p in _presets) repaired += p.Sanitize(_catalog, isOwned);
            if (repaired > 0 || SavedContentVersion != _catalog.ContentVersion) Persist();
            return repaired;
        }

        private void Load()
        {
            string json = _store.GetString(StoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "appearance");
                SavedContentVersion = MiniJson.GetInt(o, "contentVersion", 0);
                ActiveIndex = MiniJson.GetInt(o, "active", 0);
                foreach (var entry in MiniJson.AsArray(MiniJson.Get(o, "presets"), "presets"))
                {
                    if (_presets.Count >= MaxPresets) break;
                    _presets.Add(CharacterPreset.Read(MiniJson.AsObject(entry, "preset")));
                }
            }
            catch (FormatException)
            {
                _store.SetString(StoreKey + ".corrupt", json);
                LoadedCorrupt = true;
                _presets.Clear();
                ActiveIndex = 0;
            }
        }

        private void Persist()
        {
            var w = new JsonWriter(false);
            w.BeginObject();
            w.Key("version").Value(Version);
            w.Key("contentVersion").Value(_catalog.ContentVersion);
            w.Key("active").Value(ActiveIndex);
            w.Key("presets").BeginArray();
            foreach (var p in _presets) p.Write(w);
            w.EndArray();
            w.EndObject();
            _store.SetString(StoreKey, w.ToString());
            _store.Save();
            SavedContentVersion = _catalog.ContentVersion;
        }
    }
}
