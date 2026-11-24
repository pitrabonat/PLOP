using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta
{
    /// <summary>
    /// Dream Dust: soft cosmetic currency. Every reward is granted under a unique key ("complete:ID",
    /// "perfect:ID", "gift:2026-11-24", "daily:2026-11-24", …) and each key pays out at most once, so
    /// replays, relaunches or repeated taps can never duplicate a reward. Never required to play.
    /// Corrupt saves are kept aside (not overwritten) and the wallet starts empty.
    /// </summary>
    public sealed class DreamDustWallet
    {
        public const string StoreKey = "plop.wallet.v1";
        private readonly IKeyValueStore _store;
        private readonly HashSet<string> _grants = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);

        public int Balance { get; private set; }
        public int LifetimeEarned { get; private set; }
        public IEnumerable<string> OwnedCosmetics => _owned;

        public event Action<int, string> Granted; // amount, key

        public DreamDustWallet(IKeyValueStore store)
        {
            _store = store;
            Load();
        }

        public bool HasGrant(string key) => _grants.Contains(key);

        /// <summary>Pays <paramref name="amount"/> once for <paramref name="key"/>. Returns false if already granted.</summary>
        public bool Grant(string key, int amount)
        {
            if (string.IsNullOrEmpty(key) || amount < 0) throw new ArgumentException("Invalid grant.");
            if (!_grants.Add(key)) return false;
            Balance += amount;
            LifetimeEarned += amount;
            Persist();
            Granted?.Invoke(amount, key);
            return true;
        }

        public bool Owns(string cosmeticId) => _owned.Contains(cosmeticId);

        /// <summary>Unlocks a cosmetic if affordable and not owned. No shop UI yet (data model only).</summary>
        public bool TryUnlock(CosmeticItem item)
        {
            if (item == null || _owned.Contains(item.Id) || Balance < item.Cost) return false;
            Balance -= item.Cost;
            _owned.Add(item.Id);
            Persist();
            return true;
        }

        private void Load()
        {
            string json = _store.GetString(StoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "wallet");
                Balance = Math.Max(0, MiniJson.GetInt(o, "balance", 0));
                LifetimeEarned = Math.Max(Balance, MiniJson.GetInt(o, "earned", 0));
                if (o.TryGetValue("grants", out var g) && g is List<object> grants)
                    foreach (var k in grants) if (k is string s) _grants.Add(s);
                if (o.TryGetValue("owned", out var w) && w is List<object> owned)
                    foreach (var k in owned) if (k is string s) _owned.Add(s);
            }
            catch (FormatException)
            {
                _store.SetString(StoreKey + ".corrupt", json); // keep evidence, never silently destroy
                Balance = 0;
                _grants.Clear();
                _owned.Clear();
            }
        }

        private void Persist()
        {
            var w = new JsonWriter(false);
            w.BeginObject();
            w.Key("version").Value(1);
            w.Key("balance").Value(Balance);
            w.Key("earned").Value(LifetimeEarned);
            w.Key("grants").BeginArray();
            var grants = new List<string>(_grants);
            grants.Sort(StringComparer.Ordinal);
            foreach (var k in grants) w.Value(k);
            w.EndArray();
            w.Key("owned").BeginArray();
            var owned = new List<string>(_owned);
            owned.Sort(StringComparer.Ordinal);
            foreach (var k in owned) w.Value(k);
            w.EndArray();
            w.EndObject();
            _store.SetString(StoreKey, w.ToString());
            _store.Save();
        }
    }
}
