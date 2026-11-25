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

namespace Plop.Presentation
{
    /// <summary>Reads through to another store; writes stay in memory for the session and are never saved.</summary>
    internal sealed class ReadOnlyStore : IKeyValueStore
    {
        private readonly IKeyValueStore _inner;
        private readonly MemoryStore _overlay = new MemoryStore();

        public ReadOnlyStore(IKeyValueStore inner) { _inner = inner; }

        public string GetString(string key, string fallback)
            => _overlay.Values.TryGetValue(key, out var v) ? v : _inner.GetString(key, fallback);

        public void SetString(string key, string value) => _overlay.SetString(key, value);
        public void Save() { }
    }
}
