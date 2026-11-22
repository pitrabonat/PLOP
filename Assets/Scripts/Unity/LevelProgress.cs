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
                _unlocked.UnionWith(_completed);
                for (int i = 0; i < levels.Count - 1; i++)
                    if (IsCompleted(i)) _unlocked.Add(levels[i + 1].Id);
            }
            else if (packResource == LevelLibrary.DefaultPack && PlayerPrefs.HasKey(LegacyIndexKey))
            {
                MigrateLegacyIndex();
                Save();
            }
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
