using Plop.Meta;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>PlayerPrefs-backed storage for the engine-free meta systems (wallet, records).</summary>
    internal sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
