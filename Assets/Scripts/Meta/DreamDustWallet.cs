using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta
{
    /// <summary>
    /// Dream Dust: soft cosmetic currency. Every reward is granted under a unique key ("complete:ID",
    /// "perfect:ID", "gift:2026-11-24", "daily:2026-11-24", …) and each key pays out at most once, so
    /// replays, relaunches or repeated taps can never duplicate a reward. Never required to play.
    /// Owned cosmetics live in the same record as the balance, so a purchase (spend + grant ownership) is one
    /// write: it is either fully saved or not saved at all. After every successful write the same JSON is copied
    /// to <c>.bak</c>; a torn or corrupt main record is kept aside as <c>.corrupt</c> and the last good backup
    /// is restored, so an interrupted purchase can never leave Dust spent without the item (or the reverse).
    /// </summary>
    public sealed class DreamDustWallet
    {
        public const string StoreKey = "plop.wallet.v1";
        public const string BackupKey = StoreKey + ".bak";
        private readonly IKeyValueStore _store;
        private readonly HashSet<string> _grants = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);

        public int Balance { get; private set; }
        public int LifetimeEarned { get; private set; }
        public IEnumerable<string> OwnedCosmetics => _owned;

        public event Action<int, string> Granted; // amount, key
        /// <summary>True when the main record was unreadable and the backup was restored at load.</summary>
        public bool RecoveredFromBackup { get; private set; }

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
            if (_grants.Contains(key)) return false;
            Commit(() =>
            {
                _grants.Add(key);
                Balance += amount;
                LifetimeEarned += amount;
            });
            Granted?.Invoke(amount, key);
            return true;
        }

        public bool Owns(string cosmeticId) => _owned.Contains(cosmeticId);

        /// <summary>
        /// Spends <paramref name="cost"/> and records ownership of <paramref name="itemId"/> in one write.
        /// Returns false (and changes nothing) when already owned or unaffordable. If the save throws, the
        /// in-memory state is rolled back and the exception propagates.
        /// </summary>
        public bool TryPurchase(string itemId, int cost)
        {
            if (string.IsNullOrEmpty(itemId) || cost < 0) throw new ArgumentException("Invalid purchase.");
            if (_owned.Contains(itemId) || Balance < cost) return false;
            Commit(() =>
            {
                Balance -= cost;
                _owned.Add(itemId);
            });
            return true;
        }

        /// <summary>Records free ownership (milestone unlocks). One write for the whole batch; no Dust changes.</summary>
        public int GrantOwnership(IEnumerable<string> itemIds)
        {
            var fresh = new List<string>();
            foreach (var id in itemIds) if (!string.IsNullOrEmpty(id) && !_owned.Contains(id) && !fresh.Contains(id)) fresh.Add(id);
            if (fresh.Count == 0) return 0;
            Commit(() => { foreach (var id in fresh) _owned.Add(id); });
            return fresh.Count;
        }

        /// <summary>Applies a change and persists it; restores the previous in-memory state if saving fails.</summary>
        private void Commit(Action change)
        {
            int balance = Balance, earned = LifetimeEarned;
            var grants = new List<string>(_grants);
            var owned = new List<string>(_owned);
            change();
            try { Persist(); }
            catch
            {
                Balance = balance;
                LifetimeEarned = earned;
                _grants.Clear(); foreach (var g in grants) _grants.Add(g);
                _owned.Clear(); foreach (var o in owned) _owned.Add(o);
                throw;
            }
        }

        private void Load()
        {
            string json = _store.GetString(StoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            if (TryRead(json)) return;
            _store.SetString(StoreKey + ".corrupt", json); // keep evidence, never silently destroy
            string backup = _store.GetString(BackupKey, "");
            if (!string.IsNullOrEmpty(backup) && TryRead(backup))
            {
                RecoveredFromBackup = true;
                _store.SetString(StoreKey, backup);
                _store.Save();
            }
        }

        private bool TryRead(string json)
        {
            Balance = 0;
            LifetimeEarned = 0;
            _grants.Clear();
            _owned.Clear();
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "wallet");
                Balance = Math.Max(0, MiniJson.GetInt(o, "balance", 0));
                LifetimeEarned = Math.Max(Balance, MiniJson.GetInt(o, "earned", 0));
                if (o.TryGetValue("grants", out var g) && g is List<object> grants)
                    foreach (var k in grants) if (k is string s) _grants.Add(s);
                if (o.TryGetValue("owned", out var w) && w is List<object> owned)
                    foreach (var k in owned) if (k is string s) _owned.Add(s);
                return true;
            }
            catch (FormatException)
            {
                Balance = 0;
                LifetimeEarned = 0;
                _grants.Clear();
                _owned.Clear();
                return false;
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
            string json = w.ToString();
            _store.SetString(StoreKey, json);
            _store.SetString(BackupKey, json); // written only after the main record succeeded
            _store.Save();
        }
    }
}
