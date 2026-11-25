using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Bench
{
    /// <summary>
    /// Phase 10 campaign build: --expanded [seed] — parallel slot searches, then the ordered, deduplicated
    /// campaign (byte-identical to the sequential path), strict verification and a per-level report.
    /// Writes Assets/Levels/Resources/Levels/PLOP_Pack_002.json and Documentation/benchmarks/campaign_phase10.md.
    /// </summary>
    internal static class ExpandedBuild
    {
        public static int Run(ulong seed, string root)
        {
            string levelsDir = Path.Combine(root, "Assets", "Levels", "Resources", "Levels");
            string classicPath = Path.Combine(root, "Assets", "Levels", "Archive", "PLOP_Pack_001.json");
            if (!File.Exists(classicPath)) classicPath = Path.Combine(levelsDir, "PLOP_Pack_001.json");
            var classic = LevelSerializer.PackFromJson(File.ReadAllText(classicPath));
            var sw = Stopwatch.StartNew();
            var plan = ExpandedCampaign.Plan();
            var generated = plan.Where(s => s.Kind == SlotKind.Generated).Select(s => s.Index).ToArray();
            Console.WriteLine($"Phase 10 campaign: seed 0x{seed:X}, {generated.Length} generated slots, {Environment.ProcessorCount} threads");

            var cache = new ConcurrentDictionary<int, QualitySearchResult>();
            int done = 0;
            Parallel.ForEach(generated.OrderByDescending(i => i), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                cache[i] = ExpandedCampaign.SearchSlotRaw(seed, i, 0);
                int n = System.Threading.Interlocked.Increment(ref done);
                if (n % 10 == 0) Console.WriteLine($"  searched {n}/{generated.Length} slots ({sw.Elapsed.TotalSeconds:0}s)");
            });
            double parallelSeconds = sw.Elapsed.TotalSeconds;
            var missed = generated.Where(i => !cache[i].Success).ToList();
            Console.WriteLine($"  first-search misses: {missed.Count}/{generated.Length}");
            foreach (int i in missed) Console.WriteLine($"    slot {i + 1}: {ExpandedCampaign.SlotFor(i).Target.Describe()} — {cache[i].Message}");
            var campaign = ExpandedCampaign.Generate(seed, classic, i => cache[i],
                i => { if (i % 20 == 0) Console.WriteLine($"  ordering slot {i + 1} ({sw.Elapsed.TotalSeconds:0}s)"); });
            if (campaign.Levels.Count != ExpandedCampaign.LevelCount)
            {
                Console.Error.WriteLine("Campaign incomplete: " + campaign.Levels.Count + " levels. " + campaign.Message);
                return 1;
            }

            var verify = LevelVerifier.VerifyProductionPack(campaign.Levels, expectedCount: ExpandedCampaign.LevelCount);
            bool deterministic = true;
            foreach (int i in new[] { generated[0], generated[generated.Length / 2], generated[generated.Length - 1] })
            {
                var again = ExpandedCampaign.SearchSlotRaw(seed, i, 0);
                if (again.Success != cache[i].Success ||
                    (again.Success && LevelCanonicalizer.Canonicalize(again.Level) != LevelCanonicalizer.Canonicalize(cache[i].Level)))
                    deterministic = false;
            }
            Directory.CreateDirectory(levelsDir);
            File.WriteAllText(Path.Combine(levelsDir, ExpandedCampaign.PackId + ".json"), LevelSerializer.PackToJson(ExpandedCampaign.PackId, campaign.Levels) + "\n");

            var ci = CultureInfo.InvariantCulture;
            var report = new StringBuilder();
            report.AppendLine("# Phase 10 campaign — 160 levels, nine Worlds");
            report.AppendLine();
            report.AppendLine($"Seed `0x{seed:X}`, quality search v{QualitySearch.Version}, expanded plan v{ExpandedCampaign.Version}, solver v{SolverInfo.Version}. " +
                              $"Parallel search {parallelSeconds:0} s, total {sw.Elapsed.TotalSeconds:0} s on {Environment.ProcessorCount} threads, {campaign.Stats.Candidates} candidate searches.");
            report.AppendLine();
            report.AppendLine($"Strict verification: **{(verify.IsValid ? "passed" : "FAILED")}** ({verify.Errors.Count} errors, {verify.Warnings.Count} warnings). Sequential re-search of sampled slots identical: **{deterministic}**.");
            report.AppendLine();
            report.AppendLine("| World | Levels | Authored | Generated | Phase 8 classic | Avg optimal | Avg challenge | Greedy solves | Uses Frost | Breeze | Lava (hazard) | Loops |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var w in ExpandedCampaign.Worlds)
            {
                var range = Enumerable.Range(w.First - 1, w.Last - w.First + 1).ToList();
                var levels = range.Select(i => campaign.Levels[i]).ToList();
                double M(LevelDefinition l, string k) => l.Metadata?.Difficulty != null && l.Metadata.Difficulty.Metrics.TryGetValue(k, out var v) ? v : 0;
                report.AppendLine(string.Format(ci, "| {0} | {1}–{2} | {3} | {4} | {5} | {6:0.0} | {7:0.0} | {8} | {9} | {10} | {11} | {12} |",
                    w.Id, w.First, w.Last,
                    range.Count(i => plan[i].Kind == SlotKind.Authored), range.Count(i => plan[i].Kind == SlotKind.Generated), range.Count(i => plan[i].Kind == SlotKind.Classic),
                    levels.Average(l => l.OptimalMoves), levels.Average(l => l.Metadata?.Difficulty?.Score ?? 0),
                    levels.Count(l => M(l, "greedySolves") > 0),
                    levels.Count(l => M(l, "slides") > 0), levels.Count(l => M(l, "pushes") > 0), levels.Count(l => M(l, "hazardAlternatives") > 0), levels.Count(l => M(l, "teleports") > 0)));
            }
            report.AppendLine();
            report.AppendLine("## Per level");
            report.AppendLine();
            report.AppendLine("| # | World | Kind | Board | Plops | Tiles | Opt | Limit | Challenge | Category | Greedy | Hazards | Mech steps | Slides/Pushes/Teleports | Id |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < campaign.Levels.Count; i++)
            {
                var l = campaign.Levels[i];
                var d = l.Metadata?.Difficulty;
                double M(string k) => d != null && d.Metrics.TryGetValue(k, out var v) ? v : 0;
                string tiles = string.Join("", l.Tiles.GroupBy(t => t.Kind).Select(g => g.Key.ToString()[0] + "" + g.Count()));
                report.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3}×{4} | {5} | {6} | {7} | {8} | {9:0.0} | {10} | {11} | {12} | {13} | {14}/{15}/{16} | `{17}` |",
                    i + 1, plan[i].WorldId, plan[i].Kind, l.Width, l.Height, l.Plops.Count, tiles == "" ? "–" : tiles, l.OptimalMoves,
                    l.MoveLimit > 0 ? l.MoveLimit.ToString(ci) : "–", d?.Score ?? 0, d?.Category.ToString() ?? "?",
                    M("greedySolves") > 0 ? "solves" : "fails", M("hazardAlternatives"), M("mechanicSteps"), M("slides"), M("pushes"), M("teleports"), l.Id));
            }
            if (!verify.IsValid)
            {
                report.AppendLine();
                report.AppendLine("## Verification errors");
                foreach (var e in verify.Errors) report.AppendLine("- " + e);
            }
            string reportPath = Path.Combine(root, "Documentation", "benchmarks", "campaign_phase10.md");
            File.WriteAllText(reportPath, report.ToString());
            Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:0}s. Verification {(verify.IsValid ? "passed" : "FAILED")}; deterministic={deterministic}. Report: {reportPath}");
            if (!verify.IsValid) foreach (var e in verify.Errors.Take(20)) Console.WriteLine("  " + e);
            return verify.IsValid && deterministic ? 0 : 1;
        }
    }
}
