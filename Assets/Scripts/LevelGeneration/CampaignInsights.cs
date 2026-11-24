using System;
using System.Collections.Generic;
using System.Text;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>One analysed level for the Level Lab's campaign overview.</summary>
    public sealed class LevelInsightRow
    {
        public int Index;
        public LevelDefinition Level;
        public PuzzleInsight Insight;            // null when the analysis was incomplete or unsolvable
        public int ReachableStates;
        public int DeadEndStates;
        public double DeadEndRatio;
        /// <summary>Total solution length when starting with Up/Right/Down/Left; −1 = swipe changes nothing, −2 = dead end.</summary>
        public int[] FirstMoveLengths = { -1, -1, -1, -1 };
        /// <summary>Plops in the order they settle on the shortest route, e.g. "B@3 R@7 Y@9".</summary>
        public string SettlementSequence = "";
        public List<string> TrivialFlags = new List<string>();
        /// <summary>Why the level could not be analysed ("unsolvable", "analysis incomplete"); empty when analysed.</summary>
        public string Problem = "";
        public int MostSimilarIndex = -1;
        public double MostSimilarScore;
        public string Category = "";

        public bool IsTrivial => Analysed && TrivialFlags.Count > 0;
        public bool Analysed => Insight != null;

        /// <summary>The distinct total lengths a player can reach from the first swipe, shortest first.</summary>
        public List<int> AlternativeLengths()
        {
            var list = new List<int>();
            foreach (int n in FirstMoveLengths) if (n > 0 && !list.Contains(n)) list.Add(n);
            list.Sort();
            return list;
        }
    }

    /// <summary>
    /// Developer analysis for the Level Lab: player-model insight, trivial-puzzle flags, alternative solution
    /// lengths, dead ends, settlement order and nearest-neighbour similarity. Engine-free so it is unit-tested
    /// in .NET and Unity alike.
    /// </summary>
    public static class CampaignInsights
    {
        public const int DefaultMaxStates = 2_000_000; // matches StateSpaceAnalyzer; large 4-Plop boards exceed 250k states

        /// <summary>Random swipes finish this often or more → the level plays itself.</summary>
        public const double TrivialRandomSolve = 0.20;

        public static LevelInsightRow Analyze(LevelDefinition level, int index = 0, int maxStates = DefaultMaxStates)
        {
            var row = new LevelInsightRow { Index = index, Level = level };
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(level, maxStates);
            row.ReachableStates = analysis.ReachableStates;
            row.DeadEndStates = analysis.DeadEndStates;
            row.DeadEndRatio = analysis.DeadEndRatio;
            if (!analysis.Solvable || analysis.Graph == null)
            {
                row.Problem = analysis.Complete ? "unsolvable" : "analysis incomplete";
                return row;
            }

            var g = analysis.Graph;
            for (int d = 0; d < 4; d++)
            {
                int next = g.Next(0, d);
                if (next < 0 || next == 0) row.FirstMoveLengths[d] = -1;
                else row.FirstMoveLengths[d] = g.Distance[next] >= 0 ? 1 + g.Distance[next] : -2;
            }

            row.Insight = PuzzleInsight.Analyze(level, analysis);
            row.SettlementSequence = Settlements(level, analysis.Solution);
            row.Category = CampaignPlan.Categorize(row.Insight.Challenge).ToString();
            row.TrivialFlags.AddRange(TrivialFlags(row.Insight));
            return row;
        }

        public static List<LevelInsightRow> AnalyzeAll(IReadOnlyList<LevelDefinition> levels, int maxStates = DefaultMaxStates,
            Action<int> progress = null)
        {
            var rows = new List<LevelInsightRow>(levels.Count);
            for (int i = 0; i < levels.Count; i++)
            {
                progress?.Invoke(i);
                rows.Add(Analyze(levels[i], i, maxStates));
            }
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < rows.Count; j++)
                {
                    if (i == j) continue;
                    double s = Similarity(levels[i], levels[j]);
                    if (s > rows[i].MostSimilarScore) { rows[i].MostSimilarScore = s; rows[i].MostSimilarIndex = j; }
                }
            }
            return rows;
        }

        /// <summary>Reasons a level would feel automatic. Empty for a level with a real decision.</summary>
        public static List<string> TrivialFlags(PuzzleInsight insight)
        {
            var flags = new List<string>();
            if (insight == null) return flags;
            if (insight.GreedySolves && insight.GreedyLength <= insight.Optimal + 1) flags.Add("greedy finishes");
            if (insight.RandomSolveProbability >= TrivialRandomSolve) flags.Add("random swipes finish " + (insight.RandomSolveProbability * 100).ToString("0") + "%");
            if (insight.DecisionPoints == 0 && insight.TemptingTraps + insight.EarlySettleTraps == 0) flags.Add("no decision");
            if (insight.RepetitionRatio > 0.5) flags.Add("repetitive route");
            if (insight.UnusedSpaceRatio > 0.5) flags.Add("mostly unused floor");
            return flags;
        }

        /// <summary>Order in which Plops fall asleep on the given route, with the step on which each settled.</summary>
        public static string Settlements(LevelDefinition level, Direction[] route)
        {
            if (route == null) return "";
            var state = level.CreateInitialState();
            var sb = new StringBuilder();
            uint before = state.AsleepMask;
            for (int step = 0; step < route.Length; step++)
            {
                MoveRules.Apply(level, state, route[step]);
                uint now = state.AsleepMask;
                for (int p = 0; p < state.PlopCount; p++)
                {
                    uint bit = 1u << p;
                    if ((now & bit) == 0 || (before & bit) != 0) continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(ColorLetter(level.PlopColorOf(p))).Append('@').Append(step + 1);
                }
                before = now;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Layout similarity in [0, 1]: the best cell-by-cell agreement over the eight board symmetries
        /// (walls, Plop colours and Nest colours must match). Boards of different size score 0.
        /// </summary>
        public static double Similarity(LevelDefinition a, LevelDefinition b)
        {
            var ga = Grid(a);
            var gb = Grid(b);
            double best = 0;
            for (int t = 0; t < 8; t++)
            {
                bool swap = (t & 4) != 0;
                int w = swap ? b.Height : b.Width, h = swap ? b.Width : b.Height;
                if (w != a.Width || h != a.Height) continue;
                int same = 0, considered = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        char ca = ga[y * a.Width + x];
                        char cb = gb[Source(t, x, y, b.Width, b.Height)];
                        if (ca == '.' && cb == '.') continue; // shared empty floor says little
                        considered++;
                        if (ca == cb) same++;
                    }
                }
                if (considered > 0) best = Math.Max(best, (double)same / considered);
            }
            return best;
        }

        private static int Source(int t, int x, int y, int bw, int bh)
        {
            // Map a cell of the transformed board back to b. Bit 4 transposes, bits 1/2 mirror.
            int sx = x, sy = y;
            if ((t & 4) != 0) { sx = y; sy = x; }
            if ((t & 1) != 0) sx = bw - 1 - sx;
            if ((t & 2) != 0) sy = bh - 1 - sy;
            return sy * bw + sx;
        }

        private static char[] Grid(LevelDefinition level)
        {
            var g = new char[level.CellCount];
            var state = level.CreateInitialState();
            for (int cell = 0; cell < g.Length; cell++)
            {
                int plop = state.PlopAt(cell);
                if (level.IsWallCell(cell)) g[cell] = '#';
                else if (plop >= 0) g[cell] = ColorLetter(level.PlopColorOf(plop));
                else if (level.NestColorAt(cell) != LevelDefinition.NoNest) g[cell] = char.ToLowerInvariant(ColorLetter((PlopColor)level.NestColorAt(cell)));
                else g[cell] = '.';
            }
            return g;
        }

        private static char ColorLetter(PlopColor c) => c.ToString()[0];

        /// <summary>Tab-separated overview for spreadsheets and agent handoff notes.</summary>
        public static string ToTable(IReadOnlyList<LevelInsightRow> rows)
        {
            var sb = new StringBuilder("#\tsize\tplops\topt\tchallenge\tcategory\tgreedy\trandom%\ttraps\tcounter\tdecisions\tdeadEnd%\tsettle\tpatterns\tsimilar\tflags\n");
            foreach (var r in rows)
            {
                var i = r.Insight;
                sb.Append(r.Index + 1).Append('\t').Append(r.Level.Width).Append('x').Append(r.Level.Height).Append('\t')
                  .Append(r.Level.Plops.Count).Append('\t');
                if (i == null) { sb.Append("-\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\t").Append(r.Problem).Append('\n'); continue; }
                sb.Append(i.Optimal).Append('\t').Append(i.Challenge.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                  .Append(r.Category).Append('\t').Append(i.GreedySolves ? "yes" : "no").Append('\t')
                  .Append((i.RandomSolveProbability * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                  .Append(i.TemptingTraps + i.EarlySettleTraps).Append('\t').Append(i.CounterIntuitiveSteps).Append('\t')
                  .Append(i.DecisionPoints).Append('\t').Append((r.DeadEndRatio * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                  .Append(r.SettlementSequence).Append('\t').Append(i.Patterns.Replace(" ", "")).Append('\t')
                  .Append(r.MostSimilarIndex >= 0 ? (r.MostSimilarIndex + 1) + " (" + (r.MostSimilarScore * 100).ToString("0") + "%)" : "-").Append('\t')
                  .Append(string.Join(", ", r.TrivialFlags)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
