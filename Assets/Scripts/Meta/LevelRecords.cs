using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta
{
    /// <summary>Outcome of submitting one completed attempt.</summary>
    public struct AttemptResult
    {
        public int Moves;
        public int OptimalMoves;
        public bool Perfect;          // this attempt reached the verified optimum without assistance
        public bool FirstCompletion;
        public bool NewBest;
        public bool FirstPerfect;
        public int BestMoves;
        public bool BestPerfect;
        public bool Assisted;
    }

    /// <summary>
    /// Best valid result per level ID. Move counts are the length of the final engine history (Undo removes
    /// moves, so the count is always the length of a real solution and can never go below the verified
    /// optimum). A Perfect requires moves == optimal AND no hint used in that attempt (hints reveal the route).
    /// Replays can only improve a record; records are never lowered or deleted.
    /// </summary>
    public sealed class LevelRecords
    {
        public const string StoreKey = "plop.records.v1";

        public struct Entry
        {
            public int Best;
            public bool Perfect;
            public int Completions;
        }

        private readonly IKeyValueStore _store;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public LevelRecords(IKeyValueStore store)
        {
            _store = store;
            Load();
        }

        public bool TryGet(string id, out Entry entry) => _entries.TryGetValue(id, out entry);
        public bool IsPerfect(string id) => _entries.TryGetValue(id, out var e) && e.Perfect;
        public int Count => _entries.Count;
        public int PerfectCount
        {
            get { int n = 0; foreach (var e in _entries.Values) if (e.Perfect) n++; return n; }
        }

        public AttemptResult Submit(string id, int moves, int optimalMoves, bool assisted)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Level id required.");
            if (moves <= 0) throw new ArgumentException("A completed attempt has at least one move.");
            if (optimalMoves > 0 && moves < optimalMoves)
                throw new InvalidOperationException("Moves below the verified optimum indicate a corrupted attempt.");

            bool perfect = optimalMoves > 0 && moves == optimalMoves && !assisted;
            bool had = _entries.TryGetValue(id, out var e);
            bool wasPerfect = had && e.Perfect;
            var r = new AttemptResult { Moves = moves, OptimalMoves = optimalMoves, Perfect = perfect, Assisted = assisted, FirstCompletion = !had };
            if (!had)
            {
                e = new Entry { Best = moves, Perfect = perfect, Completions = 0 };
                r.NewBest = true;
            }
            else if (moves < e.Best)
            {
                e.Best = moves;
                r.NewBest = true;
            }
            if (perfect) e.Perfect = true;
            r.FirstPerfect = perfect && !wasPerfect;
            e.Completions++;
            r.BestMoves = e.Best;
            r.BestPerfect = e.Perfect;
            _entries[id] = e;
            Persist();
            return r;
        }

        private void Load()
        {
            string json = _store.GetString(StoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "records");
                if (!(MiniJson.Get(o, "levels", false) is List<object> levels)) return;
                foreach (var item in levels)
                {
                    var l = MiniJson.AsObject(item, "record");
                    string id = MiniJson.GetString(l, "id");
                    int best = MiniJson.GetInt(l, "best");
                    if (best <= 0) continue;
                    _entries[id] = new Entry
                    {
                        Best = best,
                        Perfect = MiniJson.Get(l, "perfect", false) is bool p && p,
                        Completions = Math.Max(1, MiniJson.GetInt(l, "completions", 1))
                    };
                }
            }
            catch (FormatException)
            {
                _store.SetString(StoreKey + ".corrupt", json);
                _entries.Clear();
            }
        }

        private void Persist()
        {
            var ids = new List<string>(_entries.Keys);
            ids.Sort(StringComparer.Ordinal);
            var w = new JsonWriter(false);
            w.BeginObject();
            w.Key("version").Value(1);
            w.Key("levels").BeginArray();
            foreach (var id in ids)
            {
                var e = _entries[id];
                w.BeginObject();
                w.Key("id").Value(id);
                w.Key("best").Value(e.Best);
                w.Key("perfect").Value(e.Perfect);
                w.Key("completions").Value(e.Completions);
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
            _store.SetString(StoreKey, w.ToString());
            _store.Save();
        }
    }
}
