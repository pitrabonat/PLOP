using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Bench
{
    /// <summary>
    /// Player-centred audit of a level pack: greedy and random player models, route decisions, traps,
    /// repetition and board usage for every level, plus campaign-level aggregates.
    /// Usage: dotnet run -c Release --project DotNet/Plop.Bench -- --audit pack.json [report.md] [label]
    /// </summary>
    internal static class CampaignAudit
    {
        public static int Run(string packPath, string reportPath, string label)
        {
            var levels = LevelSerializer.PackFromJson(File.ReadAllText(packPath));
            var rows = new List<(int index, LevelDefinition level, PuzzleInsight insight)>();
            foreach (var level in levels)
            {
                var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(level);
                if (!analysis.Solvable) { Console.Error.WriteLine("Unsolvable or unverified: " + level.Id); return 1; }
                rows.Add((rows.Count + 1, level, PuzzleInsight.Analyze(level, analysis)));
            }

            var ci = CultureInfo.InvariantCulture;
            var md = new StringBuilder();
            md.AppendLine("# Campaign audit" + (label != null ? " — " + label : ""));
            md.AppendLine();
            md.AppendLine("Pack: `" + Path.GetFileName(packPath) + "` (" + levels.Count + " levels). Metrics from `PuzzleInsight` on the exact state graph.");
            md.AppendLine("Greedy = naive player who always takes the unvisited swipe that brings Plops closest to free matching Nests.");
            md.AppendLine("Random = exact probability that random state-changing swipes finish within 2·optimal + 4 swipes.");
            md.AppendLine();

            md.AppendLine("## Summary by campaign band");
            md.AppendLine();
            md.AppendLine("| Levels | Avg optimal | Greedy solves | Greedy optimal-length | Avg random solve | Avg counter-intuitive steps | Levels with a trap | Avg decision points | Avg repetition | Avg unused floor | Avg challenge |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (from, to) in new[] { (1, 10), (11, 25), (26, 50), (51, 75), (76, 100) })
            {
                var band = rows.Where(r => r.index >= from && r.index <= to).Select(r => r.insight).ToList();
                if (band.Count == 0) continue;
                md.AppendLine(string.Format(ci, "| {0}–{1} | {2:0.0} | {3}/{4} | {5}/{4} | {6:0.0%} | {7:0.00} | {8}/{4} | {9:0.0} | {10:0%} | {11:0%} | {12:0.0} |",
                    from, to, band.Average(b => b.Optimal), band.Count(b => b.GreedySolves), band.Count,
                    band.Count(b => b.GreedySolves && b.GreedyLength == b.Optimal),
                    band.Average(b => b.RandomSolveProbability), band.Average(b => b.CounterIntuitiveSteps),
                    band.Count(b => b.TemptingTraps + b.EarlySettleTraps > 0), band.Average(b => b.DecisionPoints),
                    band.Average(b => b.RepetitionRatio), band.Average(b => b.UnusedSpaceRatio), band.Average(b => b.Challenge)));
            }
            var all = rows.Select(r => r.insight).ToList();
            md.AppendLine();
            md.AppendLine(string.Format(ci, "Whole pack: greedy solves **{0}/{1}** (optimally {2}); random swiping finishes with ≥ 50% probability on **{3}** levels and ≥ 10% on **{4}**; " +
                                            "{5} levels contain no counter-intuitive step; {6} levels contain no trap; {7} levels show ≤ 1 design pattern.",
                all.Count(b => b.GreedySolves), all.Count, all.Count(b => b.GreedySolves && b.GreedyLength == b.Optimal),
                all.Count(b => b.RandomSolveProbability >= 0.5), all.Count(b => b.RandomSolveProbability >= 0.1),
                all.Count(b => b.CounterIntuitiveSteps == 0), all.Count(b => b.TemptingTraps + b.EarlySettleTraps == 0),
                all.Count(b => b.PatternCount <= 1)));
            md.AppendLine();
            md.AppendLine("Patterns: A coordination · B settlement order · C retreat · D strategic blocking · E multi-step planning · F false opportunity · G efficiency.");
            md.AppendLine();

            md.AppendLine("## Per level");
            md.AppendLine();
            md.AppendLine("| # | Board | Plops | Walls | Opt | Greedy | Random | Counter | Retreat | Decisions | Traps (tempt/settle) | Order | Sleeper blocks | Repeat | Unused | Patterns | Challenge | Old score |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (index, level, r) in rows)
            {
                string old = level.Metadata?.Difficulty != null ? level.Metadata.Difficulty.Score.ToString("0.#", ci) + " " + level.Metadata.Difficulty.Category : "–";
                md.AppendLine(string.Format(ci, "| {0} | {1}×{2} | {3} | {4} | {5} | {6} | {7:0.0%} | {8} | {9} | {10} | {11}/{12} | {13} | {14} | {15:0%} | {16:0%} | `{17}` | {18:0.0} | {19} |",
                    index, level.Width, level.Height, level.Plops.Count, level.Walls.Count, r.Optimal,
                    r.GreedySolves ? r.GreedyLength.ToString(ci) : "fails", r.RandomSolveProbability, r.CounterIntuitiveSteps, r.RetreatSteps,
                    r.DecisionPoints, r.TemptingTraps, r.EarlySettleTraps, r.SettlementOrderMatters ? "yes" : "–", r.SleeperBlocks,
                    r.RepetitionRatio, r.UnusedSpaceRatio, r.Patterns, r.Challenge, old));
            }

            Console.WriteLine(md.ToString().Split("## Per level")[0]);
            if (!string.IsNullOrEmpty(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllText(reportPath, md.ToString());
                Console.WriteLine("Wrote " + reportPath);
            }
            return 0;
        }
    }
}
