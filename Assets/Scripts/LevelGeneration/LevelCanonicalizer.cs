using System;
using System.Collections.Generic;
using System.Text;
using Plop.Core;

namespace Plop.Generation
{
    /// <summary>
    /// Canonical form of a level for duplicate detection. Two levels get the same canonical string
    /// when one can be turned into the other by
    ///  * any of the 8 square symmetries (rotations / reflections; rotating a WxH board gives HxW), and
    ///  * a relabeling of colors (colors are cosmetic: rules only compare Plop color with Nest color).
    /// These transforms preserve the rules exactly: "front-to-back" ordering is defined relative to the
    /// move direction and two Plops never share a coordinate pair, so tie-breaking never matters.
    /// Ids, names and metadata are ignored.
    /// </summary>
    public static class LevelCanonicalizer
    {
        public static string Canonicalize(LevelDefinition level)
        {
            string best = null;
            for (int t = 0; t < 8; t++)
            {
                string s = Build(level, t);
                if (best == null || string.CompareOrdinal(s, best) < 0) best = s;
            }
            return best;
        }

        /// <summary>Stable 64-bit FNV-1a hash of the canonical form.</summary>
        public static ulong CanonicalHash(LevelDefinition level) => Fnv1a64(Canonicalize(level));

        /// <summary>Deterministic level id derived from the canonical form, e.g. "PLOP-3F2A9C0D11E4".</summary>
        public static string CanonicalId(LevelDefinition level) => "PLOP-" + CanonicalHash(level).ToString("X16").Substring(0, 12);

        public static ulong Fnv1a64(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        public static GridPosition Transform(int t, GridPosition p, int w, int h, out int newW, out int newH)
        {
            int x = p.X, y = p.Y;
            bool swap = t >= 4;
            newW = swap ? h : w;
            newH = swap ? w : h;
            switch (t)
            {
                case 0: return new GridPosition(x, y);
                case 1: return new GridPosition(w - 1 - x, y);
                case 2: return new GridPosition(x, h - 1 - y);
                case 3: return new GridPosition(w - 1 - x, h - 1 - y);
                case 4: return new GridPosition(y, x);
                case 5: return new GridPosition(h - 1 - y, x);
                case 6: return new GridPosition(y, w - 1 - x);
                case 7: return new GridPosition(h - 1 - y, w - 1 - x);
                default: throw new ArgumentOutOfRangeException(nameof(t));
            }
        }

        /// <summary>Applies symmetry <paramref name="t"/> (0..7) to a whole level. Used by tests and tools.</summary>
        public static LevelDefinition TransformLevel(LevelDefinition level, int t)
        {
            int nw = 0, nh = 0;
            var walls = new List<GridPosition>();
            foreach (var w in level.Walls) walls.Add(Transform(t, w, level.Width, level.Height, out nw, out nh));
            var plops = new List<PlopDefinition>();
            foreach (var p in level.Plops) plops.Add(new PlopDefinition(p.Id, p.Color, Transform(t, p.Start, level.Width, level.Height, out nw, out nh)));
            var nests = new List<NestDefinition>();
            foreach (var n in level.Nests) nests.Add(new NestDefinition(n.Color, Transform(t, n.Position, level.Width, level.Height, out nw, out nh)));
            Transform(t, new GridPosition(0, 0), level.Width, level.Height, out nw, out nh);
            return new LevelDefinition(level.Id, level.Name, nw, nh, walls, plops, nests, level.Metadata);
        }

        private static string Build(LevelDefinition level, int t)
        {
            int nw = 0, nh = 0;
            Transform(t, new GridPosition(0, 0), level.Width, level.Height, out nw, out nh);

            var wallBits = new char[nw * nh];
            for (int i = 0; i < wallBits.Length; i++) wallBits[i] = '0';
            foreach (var w in level.Walls)
            {
                var p = Transform(t, w, level.Width, level.Height, out _, out _);
                wallBits[p.Y * nw + p.X] = '1';
            }

            // per color: sorted plop cells and sorted nest cells -> signature; colors ordered by signature
            var groups = new List<string>();
            for (int c = 0; c < PlopColorUtil.Count; c++)
            {
                var plopCells = new List<int>();
                var nestCells = new List<int>();
                foreach (var p in level.Plops)
                    if ((int)p.Color == c) { var q = Transform(t, p.Start, level.Width, level.Height, out _, out _); plopCells.Add(q.Y * nw + q.X); }
                foreach (var n in level.Nests)
                    if ((int)n.Color == c) { var q = Transform(t, n.Position, level.Width, level.Height, out _, out _); nestCells.Add(q.Y * nw + q.X); }
                if (plopCells.Count == 0 && nestCells.Count == 0) continue;
                plopCells.Sort();
                nestCells.Sort();
                groups.Add("p" + string.Join(",", plopCells) + "n" + string.Join(",", nestCells));
            }
            groups.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.Append(nw).Append('x').Append(nh).Append('|').Append(wallBits).Append('|');
            for (int i = 0; i < groups.Count; i++) sb.Append(i).Append(':').Append(groups[i]).Append(';');
            return sb.ToString();
        }
    }

    /// <summary>Set of canonical forms; rejects exact and symmetric/recolored duplicates.</summary>
    public sealed class DuplicateRegistry
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        public int Count => _seen.Count;
        public bool Contains(LevelDefinition level) => _seen.Contains(LevelCanonicalizer.Canonicalize(level));
        /// <summary>Adds the level; returns false if an equivalent level was already registered.</summary>
        public bool TryAdd(LevelDefinition level) => _seen.Add(LevelCanonicalizer.Canonicalize(level));
        public bool ContainsCanonical(string canonical) => _seen.Contains(canonical);
        public bool TryAddCanonical(string canonical) => _seen.Add(canonical);
    }
}
