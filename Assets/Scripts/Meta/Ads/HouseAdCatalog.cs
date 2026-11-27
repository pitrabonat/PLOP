using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Data-only catalog for future first-party promotion of the publisher's own games ("More Games"). Shipped empty
    /// and disabled. Entries are plain data: no remote content, no scripts, no tracking pixels, and nothing opens on
    /// its own; a store link opens only from an explicit tap on a clearly labelled card. Never a full-screen ad.
    /// </summary>
    public sealed class HouseAdCatalog
    {
        public sealed class Item
        {
            public string Id, Title, Description, AndroidStoreUrl, IosStoreUrl;
        }

        public bool Enabled { get; private set; }
        public IReadOnlyList<Item> Items => _items;
        private readonly List<Item> _items = new List<Item>();

        public static HouseAdCatalog Empty => new HouseAdCatalog();

        /// <summary>Parses the catalog. Invalid entries are dropped; a malformed file yields an empty, disabled catalog.</summary>
        public static HouseAdCatalog Parse(string json)
        {
            var catalog = new HouseAdCatalog();
            if (string.IsNullOrWhiteSpace(json)) return catalog;
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "houseAds");
                catalog.Enabled = o.TryGetValue("enabled", out var e) && e is bool b && b;
                if (o.TryGetValue("items", out var list) && list is List<object> items)
                    foreach (var raw in items)
                    {
                        if (!(raw is Dictionary<string, object> d)) continue;
                        var item = new Item
                        {
                            Id = MiniJson.GetString(d, "id", false),
                            Title = MiniJson.GetString(d, "title", false),
                            Description = MiniJson.GetString(d, "description", false) ?? "",
                            AndroidStoreUrl = MiniJson.GetString(d, "androidStoreUrl", false) ?? "",
                            IosStoreUrl = MiniJson.GetString(d, "iosStoreUrl", false) ?? ""
                        };
                        if (IsValid(item)) catalog._items.Add(item);
                    }
            }
            catch (FormatException)
            {
                return new HouseAdCatalog();
            }
            return catalog;
        }

        /// <summary>What the game may show: nothing unless the catalog is enabled and the house-ad placement is on.</summary>
        public IReadOnlyList<Item> Visible(AdConfiguration config) =>
            Enabled && config != null && config.HouseAdsEnabled ? (IReadOnlyList<Item>)_items : Array.Empty<Item>();

        private static bool IsValid(Item item) =>
            !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title) &&
            (item.AndroidStoreUrl.Length > 0 || item.IosStoreUrl.Length > 0) &&
            SafeStoreUrl(item.AndroidStoreUrl, "https://play.google.com/store/apps/details?id=") &&
            SafeStoreUrl(item.IosStoreUrl, "https://apps.apple.com/");

        private static bool SafeStoreUrl(string url, string prefix) =>
            url.Length == 0 || (url.StartsWith(prefix, StringComparison.Ordinal) && url.IndexOfAny(new[] { ' ', '"', '<', '>' }) < 0);
    }
}
