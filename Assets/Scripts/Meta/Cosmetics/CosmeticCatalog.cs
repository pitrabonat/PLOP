using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta
{
    /// <summary>Cosmetic slots. Each slot changes appearance only; none touches puzzle logic.</summary>
    /// <summary>Append only: values are parsed by name, and <see cref="CharacterPreset.SlotKey"/> is the save key.</summary>
    public enum CosmeticCategory { BodyStyle, BodyFinish, Palette, Face, Accessory, Nest, Effect, Board }

    public enum UnlockMethod { Free, Dust, Milestone }

    /// <summary>Progress that can unlock an item for free. Counts come from saved progress, never from play time.</summary>
    public enum MilestoneKind { None, LevelsCompleted, PerfectLevels, WorldsCompleted, DailyPuzzles }

    /// <summary>One catalog entry. Defined in data (<see cref="CosmeticCatalog.ResourcePath"/>), never in UI code.</summary>
    public sealed class CosmeticItem
    {
        public string Id;                 // stable save identifier, e.g. "finish.pearl"
        public CosmeticCategory Category;
        public string Name;
        public string Asset;              // presentation recipe, e.g. "finish:pearl" (resolved by the renderer)
        public string Preview;            // thumbnail recipe; "auto" renders the asset itself
        public UnlockMethod Unlock;
        public int Cost;                  // Dream Dust; for milestones an optional alternative price (0 = none)
        public MilestoneKind Milestone;
        public int MilestoneCount;
        public int Since = 1;             // content version that introduced the item
        public string[] CompatibleStyles = Array.Empty<string>(); // empty = every body style

        public bool IsFree => Unlock == UnlockMethod.Free;
        public bool CanBuy => Unlock == UnlockMethod.Dust || (Unlock == UnlockMethod.Milestone && Cost > 0);

        public bool CompatibleWith(string bodyStyleId)
        {
            if (CompatibleStyles.Length == 0) return true;
            foreach (var s in CompatibleStyles) if (s == bodyStyleId) return true;
            return false;
        }
    }

    /// <summary>
    /// Data-driven cosmetic catalog (JSON, schema 1). The game ships one catalog; the content version grows when
    /// items are added. Saves reference items by <see cref="CosmeticItem.Id"/> only, so removed or renamed
    /// content falls back to the category default instead of corrupting a save.
    /// </summary>
    public sealed class CosmeticCatalog
    {
        public const string ResourcePath = "Cosmetics/PLOP_Cosmetics_001";
        public const int SchemaVersion = 1;
        public const int MaxPrice = 2000;

        private readonly List<CosmeticItem> _items = new List<CosmeticItem>();
        private readonly Dictionary<string, CosmeticItem> _byId = new Dictionary<string, CosmeticItem>(StringComparer.Ordinal);
        private readonly Dictionary<CosmeticCategory, string> _defaults = new Dictionary<CosmeticCategory, string>();
        private readonly List<string> _parseIssues = new List<string>();

        public int ContentVersion { get; private set; }
        public IReadOnlyList<CosmeticItem> Items => _items;
        public static readonly CosmeticCategory[] Categories =
            (CosmeticCategory[])Enum.GetValues(typeof(CosmeticCategory));

        public CosmeticItem Find(string id) => id != null && _byId.TryGetValue(id, out var item) ? item : null;

        public CosmeticItem Default(CosmeticCategory category)
            => _defaults.TryGetValue(category, out var id) ? Find(id) : null;

        public string DefaultId(CosmeticCategory category) => _defaults.TryGetValue(category, out var id) ? id : null;

        public List<CosmeticItem> InCategory(CosmeticCategory category)
        {
            var list = new List<CosmeticItem>();
            foreach (var item in _items) if (item.Category == category) list.Add(item);
            return list;
        }

        /// <summary>Asset prefix each category must use ("style:classic", "finish:pearl", …).</summary>
        public static string AssetPrefix(CosmeticCategory c)
        {
            switch (c)
            {
                case CosmeticCategory.BodyStyle: return "style:";
                case CosmeticCategory.BodyFinish: return "finish:";
                case CosmeticCategory.Palette: return "palette:";
                case CosmeticCategory.Face: return "face:";
                case CosmeticCategory.Accessory: return "accessory:";
                case CosmeticCategory.Nest: return "nest:";
                case CosmeticCategory.Board: return "board:";
                default: return "effect:";
            }
        }

        /// <summary>Asset name without its prefix ("finish:pearl" → "pearl").</summary>
        public static string AssetName(string asset)
        {
            int colon = asset?.IndexOf(':') ?? -1;
            return colon < 0 ? asset : asset.Substring(colon + 1);
        }

        public static CosmeticCatalog Parse(string json)
        {
            var root = MiniJson.AsObject(MiniJson.Parse(json), "catalog");
            int schema = MiniJson.GetInt(root, "schemaVersion");
            if (schema != SchemaVersion) throw new FormatException("Unsupported cosmetic schema " + schema + ".");
            var catalog = new CosmeticCatalog { ContentVersion = MiniJson.GetInt(root, "contentVersion") };

            foreach (var entry in MiniJson.AsArray(MiniJson.Get(root, "items"), "items"))
            {
                var o = MiniJson.AsObject(entry, "item");
                var item = new CosmeticItem
                {
                    Id = MiniJson.GetString(o, "id"),
                    Name = MiniJson.GetString(o, "name"),
                    Asset = MiniJson.GetString(o, "asset"),
                    Preview = MiniJson.GetString(o, "preview", false) ?? "auto",
                    Cost = MiniJson.GetInt(o, "cost", 0),
                    MilestoneCount = MiniJson.GetInt(o, "count", 0),
                    Since = MiniJson.GetInt(o, "since", 1)
                };
                string category = MiniJson.GetString(o, "category");
                if (!Enum.TryParse(category, false, out item.Category) || !Enum.IsDefined(typeof(CosmeticCategory), item.Category))
                {
                    catalog._parseIssues.Add(item.Id + ": unknown category '" + category + "'.");
                    continue;
                }
                string unlock = MiniJson.GetString(o, "unlock");
                if (!Enum.TryParse(unlock, false, out item.Unlock) || !Enum.IsDefined(typeof(UnlockMethod), item.Unlock))
                {
                    catalog._parseIssues.Add(item.Id + ": unknown unlock method '" + unlock + "'.");
                    continue;
                }
                string milestone = MiniJson.GetString(o, "milestone", false);
                if (milestone != null && (!Enum.TryParse(milestone, false, out item.Milestone) ||
                                          !Enum.IsDefined(typeof(MilestoneKind), item.Milestone)))
                    catalog._parseIssues.Add(item.Id + ": unknown milestone '" + milestone + "'.");
                if (o.TryGetValue("compatibleStyles", out var styles) && styles is List<object> list)
                {
                    var ids = new List<string>();
                    foreach (var s in list) if (s is string str) ids.Add(str);
                    item.CompatibleStyles = ids.ToArray();
                }
                catalog._items.Add(item);
                if (!catalog._byId.ContainsKey(item.Id)) catalog._byId.Add(item.Id, item);
            }

            if (root.TryGetValue("defaults", out var d) && d is Dictionary<string, object> defaults)
            {
                foreach (var kv in defaults)
                {
                    if (Enum.TryParse(kv.Key, false, out CosmeticCategory c) && kv.Value is string id) catalog._defaults[c] = id;
                    else catalog._parseIssues.Add("defaults: unknown category '" + kv.Key + "'.");
                }
            }
            return catalog;
        }

        /// <summary>
        /// Every problem with the catalog's data. <paramref name="assetExists"/> lets the renderer confirm that
        /// asset and preview recipes resolve (Unity validator and PlayMode tests pass it; .NET tests do not).
        /// </summary>
        public List<string> Validate(Func<string, bool> assetExists = null)
        {
            var issues = new List<string>(_parseIssues);
            if (ContentVersion < 1) issues.Add("contentVersion must be at least 1.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in _items)
            {
                string label = string.IsNullOrEmpty(item.Id) ? "(missing id)" : item.Id;
                if (string.IsNullOrEmpty(item.Id) || !IsValidId(item.Id)) issues.Add(label + ": id must be lowercase 'group.name'.");
                else if (!seen.Add(item.Id)) issues.Add(label + ": duplicate id.");
                if (string.IsNullOrWhiteSpace(item.Name)) issues.Add(label + ": missing display name.");
                if (item.Name != null && item.Name.Length > 18) issues.Add(label + ": display name longer than 18 characters.");
                if (string.IsNullOrEmpty(item.Asset)) issues.Add(label + ": missing asset reference.");
                else if (!item.Asset.StartsWith(AssetPrefix(item.Category), StringComparison.Ordinal))
                    issues.Add(label + ": asset '" + item.Asset + "' does not match category " + item.Category + ".");
                else if (assetExists != null && !assetExists(item.Asset)) issues.Add(label + ": asset '" + item.Asset + "' is not registered.");
                if (string.IsNullOrEmpty(item.Preview)) issues.Add(label + ": missing preview reference.");
                else if (item.Preview != "auto" && assetExists != null && !assetExists(item.Preview))
                    issues.Add(label + ": preview '" + item.Preview + "' is not registered.");
                if (item.Since < 1 || item.Since > ContentVersion) issues.Add(label + ": 'since' must be within 1.." + ContentVersion + ".");

                switch (item.Unlock)
                {
                    case UnlockMethod.Free:
                        if (item.Cost != 0) issues.Add(label + ": free items cannot have a price.");
                        break;
                    case UnlockMethod.Dust:
                        if (item.Cost <= 0 || item.Cost > MaxPrice) issues.Add(label + ": price must be 1.." + MaxPrice + ".");
                        break;
                    case UnlockMethod.Milestone:
                        if (item.Milestone == MilestoneKind.None || item.MilestoneCount <= 0)
                            issues.Add(label + ": milestone items need a milestone and a positive count.");
                        if (item.Cost < 0 || item.Cost > MaxPrice) issues.Add(label + ": alternative price must be 0.." + MaxPrice + ".");
                        break;
                }
                foreach (var style in item.CompatibleStyles)
                {
                    var s = Find(style);
                    if (s == null || s.Category != CosmeticCategory.BodyStyle)
                        issues.Add(label + ": compatible style '" + style + "' is not a body style.");
                }
            }

            foreach (var c in Categories)
            {
                if (!_defaults.TryGetValue(c, out var id)) { issues.Add("No default " + c + "."); continue; }
                var item = Find(id);
                if (item == null) issues.Add("Default " + c + " '" + id + "' does not exist.");
                else if (item.Category != c) issues.Add("Default " + c + " '" + id + "' is a " + item.Category + ".");
                else if (!item.IsFree) issues.Add("Default " + c + " '" + id + "' must be free.");
                else if (item.CompatibleStyles.Length > 0) issues.Add("Default " + c + " '" + id + "' must fit every body style.");
            }
            return issues;
        }

        private static bool IsValidId(string id)
        {
            int dot = id.IndexOf('.');
            if (dot <= 0 || dot == id.Length - 1) return false;
            foreach (char ch in id)
                if (!(ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '.' || ch == '-')) return false;
            return true;
        }
    }
}
