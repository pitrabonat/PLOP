using System;
using System.Collections.Generic;

namespace Plop.Meta
{
    /// <summary>Persistent string storage. Unity uses PlayerPrefs; tests use <see cref="MemoryStore"/>.</summary>
    public interface IKeyValueStore
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Save();
    }

    public sealed class MemoryStore : IKeyValueStore
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        public int SaveCount;
        public string GetString(string key, string fallback) => Values.TryGetValue(key, out var v) ? v : fallback;
        public void SetString(string key, string value) => Values[key] = value;
        public void Save() => SaveCount++;
    }

    /// <summary>Local calendar time source (injectable for tests). No network, no time zones beyond the device's.</summary>
    public interface IClock
    {
        DateTime LocalNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime LocalNow => DateTime.Now;
    }

    public sealed class FixedClock : IClock
    {
        public DateTime LocalNow { get; set; }
        public FixedClock(DateTime now) { LocalNow = now; }
    }
}
