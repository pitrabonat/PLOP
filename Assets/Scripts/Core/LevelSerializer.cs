using System;
using System.Collections.Generic;
using System.Globalization;
using Plop.Core.Json;

namespace Plop.Core
{
    /// <summary>
    /// Versioned, human-readable JSON format for levels, level packs and game states.
    /// Runtime-required data sits at the top level of each level; developer-only data
    /// (difficulty, solution, generator info) sits in the optional "dev" block.
    /// See Documentation/LEVEL_FORMAT.md.
    /// </summary>
    public static class LevelSerializer
    {
        public const int SchemaVersion = 1;

        // ---------------- levels ----------------

        public static string ToJson(LevelDefinition level, bool includeDevData = true, bool pretty = true)
        {
            var w = new JsonWriter(pretty);
            WriteLevel(w, level, includeDevData);
            return w.ToString();
        }

        public static LevelDefinition FromJson(string json, bool includeDevData = true)
            => ReadLevel(MiniJson.AsObject(MiniJson.Parse(json), "Level"), includeDevData);

        public static string PackToJson(string packId, IEnumerable<LevelDefinition> levels, bool includeDevData = true)
        {
            var w = new JsonWriter(true);
            w.BeginObject();
            w.Key("schemaVersion").Value(SchemaVersion);
            w.Key("packId").Value(packId ?? "");
            w.Key("levels").BeginArray();
            foreach (var level in levels) WriteLevel(w, level, includeDevData);
            w.EndArray();
            w.EndObject();
            return w.ToString();
        }

        public static List<LevelDefinition> PackFromJson(string json, bool includeDevData = true)
        {
            var root = MiniJson.AsObject(MiniJson.Parse(json), "Pack");
            CheckSchema(root);
            var list = new List<LevelDefinition>();
            foreach (var item in MiniJson.AsArray(MiniJson.Get(root, "levels"), "levels"))
                list.Add(ReadLevel(MiniJson.AsObject(item, "Level"), includeDevData));
            return list;
        }

        private static void WriteLevel(JsonWriter w, LevelDefinition level, bool includeDevData)
        {
            w.BeginObject();
            w.Key("schemaVersion").Value(SchemaVersion);
            w.Key("id").Value(level.Id);
            w.Key("name").Value(level.Name);
            if (level.Instruction != null) w.Key("instruction").Value(level.Instruction);
            if (level.OptimalMoves > 0) w.Key("optimalMoves").Value(level.OptimalMoves);
            w.Key("width").Value(level.Width);
            w.Key("height").Value(level.Height);

            var walls = new List<GridPosition>(level.Walls);
            walls.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            w.Key("walls").BeginArray();
            foreach (var p in walls) w.InlineInts(p.X, p.Y);
            w.EndArray();

            w.Key("plops").BeginArray();
            foreach (var p in level.Plops)
            {
                w.BeginObject();
                w.Key("id").Value(p.Id);
                w.Key("color").Value(p.Color.ToString());
                w.Key("pos").InlineInts(p.Start.X, p.Start.Y);
                w.EndObject();
            }
            w.EndArray();

            w.Key("nests").BeginArray();
            foreach (var n in level.Nests)
            {
                w.BeginObject();
                w.Key("color").Value(n.Color.ToString());
                w.Key("pos").InlineInts(n.Position.X, n.Position.Y);
                w.EndObject();
            }
            w.EndArray();

            var m = level.Metadata;
            if (includeDevData && m != null)
            {
                w.Key("dev").BeginObject();
                w.Key("generatorVersion").Value(m.GeneratorVersion);
                w.Key("seed").Value(m.Seed.HasValue ? "0x" + m.Seed.Value.ToString("X16", CultureInfo.InvariantCulture) : null);
                w.Key("generatorParams").Value(m.GeneratorParams);
                w.Key("solverVersion").Value(m.SolverVersion);
                w.Key("solution").Value(m.Solution);
                w.Key("solutionLength").Value(m.Solution?.Length ?? -1);
                if (m.Difficulty != null)
                {
                    w.Key("difficulty").BeginObject();
                    w.Key("score").Value(m.Difficulty.Score);
                    w.Key("category").Value(m.Difficulty.Category.ToString());
                    w.Key("metrics").BeginObject();
                    foreach (var kv in m.Difficulty.Metrics) w.Key(kv.Key).Value(kv.Value);
                    w.EndObject();
                    w.EndObject();
                }
                if (m.Author != null) w.Key("author").Value(m.Author);
                if (m.Notes != null) w.Key("notes").Value(m.Notes);
                w.EndObject();
            }
            w.EndObject();
        }

        private static void CheckSchema(Dictionary<string, object> obj)
        {
            int version = MiniJson.GetInt(obj, "schemaVersion");
            if (version < 1 || version > SchemaVersion)
                throw new FormatException("Unsupported schemaVersion " + version + " (supported: 1.." + SchemaVersion + ").");
        }

        private static LevelDefinition ReadLevel(Dictionary<string, object> o, bool includeDevData)
        {
            CheckSchema(o);
            string id = MiniJson.GetString(o, "id");
            string name = MiniJson.GetString(o, "name", false) ?? id;
            int width = MiniJson.GetInt(o, "width");
            int height = MiniJson.GetInt(o, "height");
            if (width <= 0 || height <= 0 || width > LevelValidator.MaxSize || height > LevelValidator.MaxSize)
                throw new FormatException("Invalid board size " + width + "x" + height + ".");

            var walls = new List<GridPosition>();
            foreach (var item in MiniJson.AsArray(MiniJson.Get(o, "walls"), "walls")) walls.Add(ReadPos(item, "wall"));

            var plops = new List<PlopDefinition>();
            foreach (var item in MiniJson.AsArray(MiniJson.Get(o, "plops"), "plops"))
            {
                var p = MiniJson.AsObject(item, "plop");
                plops.Add(new PlopDefinition(MiniJson.GetInt(p, "id"), ReadColor(MiniJson.GetString(p, "color")), ReadPos(MiniJson.Get(p, "pos"), "plop.pos")));
            }

            var nests = new List<NestDefinition>();
            foreach (var item in MiniJson.AsArray(MiniJson.Get(o, "nests"), "nests"))
            {
                var n = MiniJson.AsObject(item, "nest");
                nests.Add(new NestDefinition(ReadColor(MiniJson.GetString(n, "color")), ReadPos(MiniJson.Get(n, "pos"), "nest.pos")));
            }

            LevelMetadata meta = null;
            if (includeDevData && o.TryGetValue("dev", out object devObj) && devObj != null)
                meta = ReadMeta(MiniJson.AsObject(devObj, "dev"));

            int optimal = MiniJson.GetInt(o, "optimalMoves", 0);
            if (optimal < 0) throw new FormatException("optimalMoves must be positive.");
            return new LevelDefinition(id, name, width, height, walls, plops, nests, meta, MiniJson.GetString(o, "instruction", false), optimal);
        }

        private static LevelMetadata ReadMeta(Dictionary<string, object> d)
        {
            var m = new LevelMetadata
            {
                GeneratorVersion = MiniJson.GetInt(d, "generatorVersion", 0),
                SolverVersion = MiniJson.GetInt(d, "solverVersion", 0),
                GeneratorParams = MiniJson.GetString(d, "generatorParams", false),
                Solution = MiniJson.GetString(d, "solution", false),
                Author = MiniJson.GetString(d, "author", false),
                Notes = MiniJson.GetString(d, "notes", false)
            };
            string seed = MiniJson.GetString(d, "seed", false);
            if (seed != null)
            {
                string hex = seed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? seed.Substring(2) : seed;
                if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong s))
                    throw new FormatException("Invalid seed '" + seed + "'.");
                m.Seed = s;
            }
            if (m.Solution != null) DirectionUtil.ParseMoveString(m.Solution); // validates characters
            if (d.TryGetValue("difficulty", out object diffObj) && diffObj != null)
            {
                var dd = MiniJson.AsObject(diffObj, "difficulty");
                var info = new DifficultyInfo { Score = MiniJson.GetDouble(dd, "score") };
                string cat = MiniJson.GetString(dd, "category");
                if (!Enum.TryParse(cat, false, out info.Category) || !Enum.IsDefined(typeof(DifficultyCategory), info.Category))
                    throw new FormatException("Unknown difficulty category '" + cat + "'.");
                if (dd.TryGetValue("metrics", out object metricsObj) && metricsObj != null)
                    foreach (var kv in MiniJson.AsObject(metricsObj, "metrics"))
                        info.Metrics[kv.Key] = kv.Value is long l ? l : kv.Value is double dv ? dv : throw new FormatException("Metric '" + kv.Key + "' must be numeric.");
                m.Difficulty = info;
            }
            return m;
        }

        private static GridPosition ReadPos(object item, string what)
        {
            var arr = MiniJson.AsArray(item, what);
            if (arr.Count != 2 || !(arr[0] is long x) || !(arr[1] is long y))
                throw new FormatException(what + " must be [x, y] integers.");
            return new GridPosition((int)x, (int)y);
        }

        private static PlopColor ReadColor(string s)
        {
            if (Enum.TryParse(s, false, out PlopColor c) && Enum.IsDefined(typeof(PlopColor), c)) return c;
            throw new FormatException("Unknown color '" + s + "'.");
        }

        // ---------------- game state ----------------

        public static string StateToJson(LevelDefinition level, GameState state)
        {
            var w = new JsonWriter(false);
            w.BeginObject();
            w.Key("schemaVersion").Value(SchemaVersion);
            w.Key("levelId").Value(level.Id);
            w.Key("plops").BeginArray();
            for (int id = 0; id < state.PlopCount; id++)
            {
                var p = level.PositionOf(state.CellOf(id));
                w.BeginObject();
                w.Key("id").Value(id);
                w.Key("pos").InlineInts(p.X, p.Y);
                w.Key("asleep").Value(state.IsAsleep(id));
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
            return w.ToString();
        }

        public static GameState StateFromJson(LevelDefinition level, string json)
        {
            var o = MiniJson.AsObject(MiniJson.Parse(json), "State");
            CheckSchema(o);
            string levelId = MiniJson.GetString(o, "levelId");
            if (levelId != level.Id) throw new FormatException("State belongs to level '" + levelId + "', not '" + level.Id + "'.");
            var arr = MiniJson.AsArray(MiniJson.Get(o, "plops"), "plops");
            if (arr.Count != level.Plops.Count) throw new FormatException("State Plop count mismatch.");
            var state = new GameState(arr.Count);
            var seen = new HashSet<int>();
            foreach (var item in arr)
            {
                var p = MiniJson.AsObject(item, "plop");
                int id = MiniJson.GetInt(p, "id");
                if (id < 0 || id >= arr.Count || !seen.Add(id)) throw new FormatException("Invalid Plop id " + id + " in state.");
                var pos = ReadPos(MiniJson.Get(p, "pos"), "pos");
                if (!level.InBounds(pos)) throw new FormatException("Plop " + id + " outside grid in state.");
                bool asleep = MiniJson.Get(p, "asleep") is bool b ? b : throw new FormatException("asleep must be boolean.");
                state.SetPlop(id, level.CellIndex(pos), asleep);
            }
            var v = LevelValidator.ValidateState(level, state);
            if (!v.IsValid) throw new FormatException("Invalid state: " + v);
            return state;
        }
    }
}
