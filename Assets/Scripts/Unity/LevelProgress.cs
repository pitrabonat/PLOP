using System;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Campaign access and completion keyed by immutable level IDs.</summary>
    internal sealed class LevelProgress
    {
        private const string SavePrefix = "plop.progress.v1.";
        private const string LegacyIndexKey = "plop.levelIndex";
        private const string LegacyOrderResource = "Levels/PLOP_Pack_001_LegacyOrder";
        private const string SlotHistoryResource = "Levels/PLOP_Pack_001_SlotHistory";

        [Serializable]
        private sealed class SlotHistory
        {
            public SlotEntry[] slots;
        }

        [Serializable]
        private sealed class SlotEntry
        {
            public int slot;
            public string id;
            public string[] previousIds;
        }

        [Serializable]
        private sealed class ProgressData
        {
            public string resumeId;
            public string[] completedIds;
            public string[] unlockedIds;
        }

        [Serializable]
        private sealed class LegacyOrder
        {
            public string[] levelIds;
            /// <summary>"oldId&gt;newId": same board, Limited Moves removed or loosened (Phase 11).</summary>
            public string[] aliases;
        }

        private readonly IReadOnlyList<LevelDefinition> _levels;
        private readonly string _saveKey;
        private readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked = new HashSet<string>(StringComparer.Ordinal);
        private string _resumeId;

        public LevelProgress(IReadOnlyList<LevelDefinition> levels, string packResource = null)
        {
            _levels = levels;
            if (levels.Count == 0) return;

            _unlocked.Add(levels[0].Id);
            if (packResource == null) return;

            _saveKey = SavePrefix + packResource;
            var data = ReadJson<ProgressData>(PlayerPrefs.GetString(_saveKey, ""));
            if (data != null)
            {
                _resumeId = data.resumeId;
                if (data.completedIds != null) _completed.UnionWith(data.completedIds);
                if (data.unlockedIds != null) _unlocked.UnionWith(data.unlockedIds);
                // The Phase 8 slot history applies to saves of the Phase 10 pack only (never rewritten since).
                bool migrated = packResource == LevelLibrary.PreviousPack && MigrateRebuiltSlots();
                _unlocked.UnionWith(_completed);
                for (int i = 0; i < levels.Count - 1; i++)
                    if (IsCompleted(i)) _unlocked.Add(levels[i + 1].Id);
                if (migrated) Save();
            }
            else if (packResource == LevelLibrary.DefaultPack &&
                     (MigrateFromRebalancedCampaign(PlayerPrefs.GetString(SavePrefix + LevelLibrary.PreviousPack, ""), LoadPreviousOrder(out var aliases), aliases) ||
                      MigrateFromPreviousCampaign(PlayerPrefs.GetString(SavePrefix + LevelLibrary.LegacyPack, ""),
                          PlayerPrefs.GetInt(LegacyIndexKey, -1))))
            {
                Save();
            }
        }

        private static string[] LoadPreviousOrder(out string[] aliases)
        {
            aliases = null;
            var asset = Resources.Load<TextAsset>(LevelLibrary.PreviousOrder);
            if (asset == null) return null;
            var order = ReadJson<LegacyOrder>(asset.text);
            Resources.UnloadAsset(asset);
            aliases = order?.aliases;
            return order?.levelIds;
        }

        /// <summary>
        /// Phase 11 rebalanced the 160-level campaign: weak puzzles were replaced, some kept puzzles moved within
        /// their World. Players keep (a) completion of every puzzle that still exists (same id, unchanged board),
        /// (b) access to every slot up to their old frontier (the furthest unlocked Phase 10 level), and (c) their
        /// resume point when that puzzle still exists. Replaced puzzles are not marked complete: nothing is awarded
        /// twice and no record is invented. The Phase 10 save is left untouched.
        /// </summary>
        internal bool MigrateFromRebalancedCampaign(string previousJson, IReadOnlyList<string> previousOrder, IReadOnlyList<string> aliases = null)
        {
            var data = ReadJson<ProgressData>(previousJson);
            if (data == null) return false;
            var oldCompleted = new HashSet<string>(data.completedIds ?? new string[0], StringComparer.Ordinal);
            int oldCount = oldCompleted.Count;
            if (aliases != null)
                foreach (var alias in aliases)
                {
                    int bar = alias?.IndexOf('>') ?? -1;
                    if (bar > 0 && oldCompleted.Contains(alias.Substring(0, bar))) oldCompleted.Add(alias.Substring(bar + 1));
                }
            var oldUnlocked = new HashSet<string>(data.unlockedIds ?? new string[0], StringComparer.Ordinal);
            oldUnlocked.UnionWith(oldCompleted);
            int access = oldCount + 1;
            if (previousOrder != null)
            {
                for (int i = 0; i < previousOrder.Count; i++)
                {
                    if (!oldUnlocked.Contains(previousOrder[i])) continue;
                    // Completing the old frontier opened the next slot.
                    access = Math.Max(access, oldCompleted.Contains(previousOrder[i]) ? i + 2 : i + 1);
                }
            }
            access = Math.Min(_levels.Count, Math.Max(1, access));
            for (int i = 0; i < _levels.Count; i++)
                if (oldCompleted.Contains(_levels[i].Id)) _completed.Add(_levels[i].Id);
            for (int i = 0; i < access; i++) _unlocked.Add(_levels[i].Id);
            _unlocked.UnionWith(_completed);
            for (int i = 0; i < _levels.Count - 1; i++)
                if (IsCompleted(i)) _unlocked.Add(_levels[i + 1].Id);
            _resumeId = null;
            if (data.resumeId != null)
                for (int i = 0; i < _levels.Count; i++)
                    if (_levels[i].Id == data.resumeId && IsUnlocked(i)) _resumeId = data.resumeId;
            for (int i = 0; i < _levels.Count && _resumeId == null; i++)
                if (IsUnlocked(i) && !IsCompleted(i)) _resumeId = _levels[i].Id;
            Debug.Log("[PLOP] Migrated saved progress to the Phase 11 campaign (" + _completed.Count + " completions kept, " + access + " slots open).");
            return true;
        }

        /// <summary>
        /// Phase 10 replaced the 100-level campaign with 160 levels. Players keep (a) completion of every puzzle
        /// that still exists unchanged (same id: the five basics and reused pure-logic levels) and (b) access to as
        /// many campaign slots as levels they had beaten. Changed puzzles are never marked complete, so nothing
        /// is awarded twice and no record is invented. The old save is left untouched.
        /// </summary>
        internal bool MigrateFromPreviousCampaign(string previousJson, int legacyIndex)
        {
            var data = ReadJson<ProgressData>(previousJson);
            var oldCompleted = new HashSet<string>(StringComparer.Ordinal);
            if (data?.completedIds != null) oldCompleted.UnionWith(data.completedIds);
            int beaten;
            if (data != null)
            {
                // Pre-Phase-8 ids map through the Phase 8 slot history first.
                var history = Resources.Load<TextAsset>(SlotHistoryResource);
                if (history != null)
                {
                    var slots = ReadJson<SlotHistory>(history.text)?.slots;
                    Resources.UnloadAsset(history);
                    if (slots != null)
                        foreach (var slot in slots)
                            if (slot.previousIds != null)
                                foreach (var previous in slot.previousIds)
                                    if (oldCompleted.Contains(previous)) oldCompleted.Add(slot.id);
                }
                beaten = data.completedIds?.Length ?? 0;
            }
            else if (legacyIndex >= 0) beaten = legacyIndex;
            else return false;

            for (int i = 0; i < _levels.Count; i++)
                if (oldCompleted.Contains(_levels[i].Id)) _completed.Add(_levels[i].Id);
            int access = Math.Min(_levels.Count, Math.Max(1, beaten + 1));
            for (int i = 0; i < access; i++) _unlocked.Add(_levels[i].Id);
            _unlocked.UnionWith(_completed);
            for (int i = 0; i < _levels.Count - 1; i++)
                if (IsCompleted(i)) _unlocked.Add(_levels[i + 1].Id);
            _resumeId = null;
            for (int i = 0; i < _levels.Count && _resumeId == null; i++)
                if (IsUnlocked(i) && !IsCompleted(i)) _resumeId = _levels[i].Id;
            return true;
        }

        public bool IsUnlocked(int index) => InRange(index) && _unlocked.Contains(_levels[index].Id);
        public bool IsCompleted(int index) => InRange(index) && _completed.Contains(_levels[index].Id);

        public int ResumeIndex()
        {
            int fallback = -1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (!IsUnlocked(i)) continue;
                if (_levels[i].Id == _resumeId) return i;
                if (fallback < 0 && !IsCompleted(i)) fallback = i;
            }
            return Math.Max(0, fallback);
        }

        // Keep the campaign frontier available even when the resume record points to an older replay.
        public int FrontierIndex()
        {
            for (int i = _levels.Count - 1; i >= 0; i--)
                if (IsUnlocked(i)) return i;
            return 0;
        }

        public void Visit(int index, bool persist)
        {
            _resumeId = _levels[index].Id;
            if (persist) Save();
        }

        public void Complete(int index, bool persist)
        {
            if (!_completed.Add(_levels[index].Id)) return;
            _unlocked.Add(_levels[index].Id);
            int next = index + 1;
            if (InRange(next))
            {
                _unlocked.Add(_levels[next].Id);
                if (!IsCompleted(next)) _resumeId = _levels[next].Id;
            }
            if (persist) Save();
        }

        private bool InRange(int index) => index >= 0 && index < _levels.Count;

        /// <summary>
        /// Campaign rebuilds (Phase 8) replace boards in some slots. Saved IDs that no longer exist are mapped to
        /// the level now occupying the same slot, so completions, unlocks and the resume point carry over.
        /// Original IDs stay in the save (nothing is deleted). Returns true when anything was migrated.
        /// </summary>
        private bool MigrateRebuiltSlots()
        {
            var current = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _levels.Count; i++) current[_levels[i].Id] = i;
            bool unknown = (_resumeId != null && !current.ContainsKey(_resumeId));
            foreach (var id in _completed) unknown |= !current.ContainsKey(id);
            foreach (var id in _unlocked) unknown |= !current.ContainsKey(id);
            if (!unknown) return false;

            var resource = Resources.Load<TextAsset>(SlotHistoryResource);
            if (resource == null) return false;
            var history = ReadJson<SlotHistory>(resource.text);
            Resources.UnloadAsset(resource);
            return Migrate(history, current);
        }

        /// <summary>Applies a slot history (exposed for tests via <see cref="MigrateForTests"/>).</summary>
        private bool Migrate(SlotHistory history, Dictionary<string, int> current)
        {
            if (history?.slots == null) return false;
            var oldToSlot = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in history.slots)
            {
                if (entry?.previousIds == null || entry.slot < 0 || entry.slot >= _levels.Count) continue;
                foreach (var old in entry.previousIds) if (!string.IsNullOrEmpty(old)) oldToSlot[old] = entry.slot;
            }
            bool changed = false;
            foreach (var id in new List<string>(_completed))
                if (!current.ContainsKey(id) && oldToSlot.TryGetValue(id, out int slot)) changed |= _completed.Add(_levels[slot].Id);
            foreach (var id in new List<string>(_unlocked))
                if (!current.ContainsKey(id) && oldToSlot.TryGetValue(id, out int slot)) changed |= _unlocked.Add(_levels[slot].Id);
            if (_resumeId != null && !current.ContainsKey(_resumeId) && oldToSlot.TryGetValue(_resumeId, out int resumeSlot))
            {
                _resumeId = _levels[resumeSlot].Id;
                changed = true;
            }
            if (changed) Debug.Log("[PLOP] Migrated saved progress to the rebuilt campaign (slot history).");
            return changed;
        }

        /// <summary>Test hook: applies a JSON slot history to an in-memory progress object.</summary>
        internal bool MigrateForTests(string slotHistoryJson, IEnumerable<string> completed, IEnumerable<string> unlocked, string resumeId)
        {
            _completed.UnionWith(completed);
            _unlocked.UnionWith(unlocked);
            _resumeId = resumeId;
            var current = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _levels.Count; i++) current[_levels[i].Id] = i;
            bool changed = Migrate(ReadJson<SlotHistory>(slotHistoryJson), current);
            _unlocked.UnionWith(_completed);
            for (int i = 0; i < _levels.Count - 1; i++)
                if (IsCompleted(i)) _unlocked.Add(_levels[i + 1].Id);
            return changed;
        }

        internal string ResumeId => _resumeId;

        private void MigrateLegacyIndex()
        {
            int rawIndex = Math.Max(0, PlayerPrefs.GetInt(LegacyIndexKey, 0));
            int index = Math.Min(rawIndex, _levels.Count - 1);
            for (int i = 0; i <= index; i++) _unlocked.Add(_levels[i].Id);
            _resumeId = _levels[index].Id;

            // The prototype's Next shortcut could skip unsolved levels. Preserve access without
            // granting completions and prefer the previous puzzle if it still exists in the pack.
            var resource = Resources.Load<TextAsset>(LegacyOrderResource);
            if (resource == null) return;
            var order = ReadJson<LegacyOrder>(resource.text)?.levelIds;
            Resources.UnloadAsset(resource);
            if (order == null || order.Length == 0) return;
            int oldIndex = Math.Min(rawIndex, order.Length - 1);
            for (int i = 0; i <= oldIndex; i++) _unlocked.Add(order[i]);
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Id != order[oldIndex]) continue;
                _resumeId = _levels[i].Id;
                break;
            }
        }

        private static T ReadJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }

        private void Save()
        {
            if (_saveKey == null) return;
            var data = new ProgressData
            {
                resumeId = _resumeId,
                completedIds = new List<string>(_completed).ToArray(),
                unlockedIds = new List<string>(_unlocked).ToArray()
            };
            PlayerPrefs.SetString(_saveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
