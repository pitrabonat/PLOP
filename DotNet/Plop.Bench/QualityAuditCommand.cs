using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Generation;

namespace Plop.Bench
{
    /// <summary>
    /// Phase 11: <c>--quality-audit pack.json out.json [out.md] [label]</c> audits every level of a pack with
    /// <see cref="QualityAudit"/> (solver replay, PuzzleQuality, simulated players, similarity) in parallel.
    /// </summary>
    public static class QualityAuditCommand
    {
        public static int Run(string packPath, string jsonPath, string mdPath, string label)
        {
            var levels = LevelSerializer.PackFromJson(File.ReadAllText(packPath));
            var rows = Audit(levels, out double seconds);
            QualityAudit.Finish(rows);
            if (jsonPath != null) File.WriteAllText(jsonPath, QualityAudit.ToJson(rows, Path.GetFileNameWithoutExtension(packPath)));
            var summary = Summary(rows, label ?? Path.GetFileName(packPath), seconds);
            if (mdPath != null) File.WriteAllText(mdPath, summary + "\n## Per level\n\n" + QualityAudit.ToMarkdownTable(rows));
            Console.WriteLine(summary);
            return rows.All(r => r.Verified) ? 0 : 1;
        }

        public static List<QualityAuditRow> Audit(IReadOnlyList<LevelDefinition> levels, out double seconds, Func<int, string> worldOf = null)
        {
            var watch = Stopwatch.StartNew();
            var rows = new QualityAuditRow[levels.Count];
            int done = 0;
            Parallel.For(0, levels.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                rows[i] = QualityAudit.AnalyzeLevel(levels[i], i, worldOf != null ? worldOf(i + 1) : levels.Count == ExpandedCampaign.LevelCount ? ExpandedCampaign.WorldOf(i + 1) : "daily");
                int n = System.Threading.Interlocked.Increment(ref done);
                if (n % 20 == 0) Console.Error.WriteLine("  audited " + n + "/" + levels.Count);
            });
            seconds = watch.Elapsed.TotalSeconds;
            return rows.ToList();
        }

        public static string Summary(IReadOnlyList<QualityAuditRow> rows, string label, double seconds)
        {
            var sb = new StringBuilder();
            sb.Append("# Quality audit — ").Append(label).Append("\n\n");
            sb.Append("Levels: ").Append(rows.Count).Append(" · verified: ").Append(rows.Count(r => r.Verified))
              .Append(" · analysed: ").Append(rows.Count(r => r.Analysed)).Append(" · ").Append(seconds.ToString("0.0")).Append(" s on ")
              .Append(Environment.ProcessorCount).Append(" threads · PuzzleQuality v").Append(PuzzleQuality.Version)
              .Append(", random player ").Append(SimulatedPlayers.RandomRuns).Append(" seeded runs, look-ahead up to ")
              .Append(SimulatedPlayers.MaxLookAhead).Append(".\n\n");
            var bad = rows.Where(r => !r.Verified).ToList();
            if (bad.Count > 0) sb.Append("**Not verified:** ").Append(string.Join(", ", bad.Select(r => (r.Index + 1) + " (" + r.Problem + ")"))).Append("\n\n");

            sb.Append("| World | Levels | Avg opt | Avg states | Avg density | Avg quality | Avg challenge | Greedy solves | LA2 solves | LA3 solves | LimInfo solves | Random ≥25% | Flagged |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var g in rows.Where(r => r.Analysed).GroupBy(r => r.WorldId))
            {
                var list = g.ToList();
                sb.Append("| ").Append(g.Key).Append(" | ").Append(list.Min(r => r.Index) + 1).Append('–').Append(list.Max(r => r.Index) + 1)
                  .Append(" | ").Append(list.Average(r => r.Optimal).ToString("0.0"))
                  .Append(" | ").Append(list.Average(r => r.ReachableStates).ToString("0"))
                  .Append(" | ").Append(list.Average(r => r.Quality.DecisionDensity).ToString("0.00"))
                  .Append(" | ").Append(list.Average(r => r.Quality.Quality).ToString("0.0"))
                  .Append(" | ").Append(list.Average(r => r.Insight.Challenge).ToString("0.0"))
                  .Append(" | ").Append(list.Count(r => r.Quality.Player(PlayerProfile.Greedy).Solved))
                  .Append(" | ").Append(list.Count(r => r.Quality.Player(PlayerProfile.LookAhead2).Solved))
                  .Append(" | ").Append(list.Count(r => r.Quality.Player(PlayerProfile.LookAhead3).Solved))
                  .Append(" | ").Append(list.Count(r => r.Quality.Player(PlayerProfile.LimitedInformation).Solved))
                  .Append(" | ").Append(list.Count(r => r.Quality.Player(PlayerProfile.Random).SuccessRate >= 0.25))
                  .Append(" | ").Append(list.Count(r => r.Flags.Count > 0)).Append(" |\n");
            }
            var flagCounts = rows.SelectMany(r => r.Flags.Select(f => f.StartsWith("similar-to") ? "similar (≥85%)" : f))
                .GroupBy(f => f).OrderByDescending(g => g.Count());
            sb.Append("\nFlags: ").Append(string.Join(" · ", flagCounts.Select(g => g.Key + " " + g.Count()))).Append('\n');
            var cats = rows.Where(r => r.Analysed).GroupBy(r => r.Category).OrderBy(g => g.Key);
            sb.Append("\nCategories: ").Append(string.Join(" · ", cats.Select(g => g.Key + " " + g.Count()))).Append('\n');
            var depth = rows.Where(r => r.Analysed).GroupBy(r => r.Quality.PlanningDepth).OrderBy(g => g.Key);
            sb.Append("\nPlanning depth (smallest look-ahead that solves): ")
              .Append(string.Join(" · ", depth.Select(g => (g.Key > SimulatedPlayers.MaxLookAhead ? ">" + SimulatedPlayers.MaxLookAhead : g.Key.ToString()) + ": " + g.Count()))).Append('\n');
            var opts = rows.Where(r => r.Analysed).Select(r => r.Optimal).OrderBy(x => x).ToList();
            if (opts.Count > 0)
                sb.Append("\nShortest solutions: min ").Append(opts.First()).Append(", median ").Append(opts[opts.Count / 2])
                  .Append(", mean ").Append(opts.Average().ToString("0.0")).Append(", max ").Append(opts.Last()).Append('\n');
            return sb.ToString();
        }
    }
}
