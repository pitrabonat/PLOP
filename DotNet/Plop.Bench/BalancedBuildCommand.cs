using System.Collections.Generic;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Plop.Core;
using Plop.Generation;

namespace Plop.Bench
{
    /// <summary>
    /// Phase 11: <c>--balanced [seed]</c> rebuilds the campaign from PLOP_Pack_002 with <see cref="BalancedCampaign"/>,
    /// verifies it strictly, audits it and writes PLOP_Pack_003.json plus the before/after analysis files.
    /// </summary>
    internal static class BalancedBuildCommand
    {
        private static string BoardKey(LevelDefinition l) => l.Width + "x" + l.Height + "|" +
            string.Join(";", l.Walls.Select(w => w.ToString()).OrderBy(x => x, StringComparer.Ordinal)) + "|" +
            string.Join(";", l.Plops.Select(p => p.ToString())) + "|" + string.Join(";", l.Nests.Select(n => n.ToString())) + "|" +
            string.Join(";", l.Tiles.Select(t => t.ToString()).OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>
        /// Migration data for Phase 10 saves: the old campaign order, plus aliases for boards kept unchanged whose
        /// id changed only because the Limited Moves rule was removed or loosened (an old completion met the new rule).
        /// </summary>
        internal static string OrderFile(IReadOnlyList<LevelDefinition> previous, IReadOnlyList<LevelDefinition> levels)
        {
            var byBoard = new Dictionary<string, LevelDefinition>();
            foreach (var l in previous) byBoard[BoardKey(l)] = l;
            var aliases = new List<string>();
            foreach (var l in levels)
            {
                if (!byBoard.TryGetValue(BoardKey(l), out var old) || old.Id == l.Id) continue;
                bool looser = l.MoveLimit == 0 || (old.MoveLimit > 0 && l.MoveLimit >= old.MoveLimit);
                if (looser) aliases.Add(old.Id + ">" + l.Id);
            }
            return "{\n \"levelIds\": [\n  " + string.Join(",\n  ", previous.Select(l => "\"" + l.Id + "\"")) + "\n ],\n \"aliases\": [" +
                string.Join(", ", aliases.Select(a => "\"" + a + "\"")) + "]\n}\n";
        }

        public static int Run(ulong seed, string root)
        {
            string levelsDir = Path.Combine(root, "Assets", "Levels", "Resources", "Levels");
            string oldPath = Path.Combine(levelsDir, "PLOP_Pack_002.json");
            if (!File.Exists(oldPath)) oldPath = Path.Combine(root, "Assets", "Levels", "Archive", "PLOP_Pack_002.json");
            string docs = Path.Combine(root, "Documentation", "Phase11");
            Directory.CreateDirectory(docs);
            var previous = LevelSerializer.PackFromJson(File.ReadAllText(oldPath));
            Console.WriteLine($"Phase 11 rebalance: seed 0x{seed:X}, {Environment.ProcessorCount} threads, input {Path.GetFileName(oldPath)}");
            var build = BalancedCampaign.Build(previous, seed, Console.WriteLine);
            if (!build.Success) { Console.Error.WriteLine("FAILED: " + build.Message); return 1; }
            var levels = build.Levels.Select(b => b.Level).ToList();

            var verify = LevelVerifier.VerifyProductionPack(levels, expectedCount: BalancedCampaign.LevelCount);
            Console.WriteLine("verification: " + (verify.IsValid ? "passed" : "FAILED") + " — " + verify);
            int loopRisk = 0, longest = 0;
            foreach (var l in levels)
            {
                var chains = ForcedChainInspector.Inspect(l, 1_000_000);
                if (!chains.Complete || chains.UnresolvedCommands > 0) { loopRisk++; Console.Error.WriteLine("  chain risk: " + l.Name); }
                longest = Math.Max(longest, chains.LongestChain);
            }
            Console.WriteLine("forced chains: longest " + longest + ", loop risk " + loopRisk);
            if (!verify.IsValid || loopRisk > 0) return 1;

            File.WriteAllText(Path.Combine(levelsDir, BalancedCampaign.PackId + ".json"), LevelSerializer.PackToJson(BalancedCampaign.PackId, levels));
            File.WriteAllText(Path.Combine(levelsDir, "PLOP_Pack_002_Order.json"), OrderFile(previous, levels));
            var after = QualityAuditCommand.Audit(levels, out double seconds, BalancedCampaign.WorldOf);
            QualityAudit.Finish(after);
            File.WriteAllText(Path.Combine(docs, "campaign_analysis_after.json"), QualityAudit.ToJson(after, BalancedCampaign.PackId));
            File.WriteAllText(Path.Combine(docs, "audit_after.md"),
                QualityAuditCommand.Summary(after, BalancedCampaign.PackId + " (Phase 11)", seconds) + "\n## Per level\n\n" + QualityAudit.ToMarkdownTable(after));
            File.WriteAllText(Path.Combine(docs, "campaign_changes.md"), Changes(build, after));
            Console.WriteLine(QualityAuditCommand.Summary(after, BalancedCampaign.PackId, seconds));
            string dailyPath = Path.Combine(levelsDir, "PLOP_Daily_001.json");
            if (!File.Exists(dailyPath)) dailyPath = Path.Combine(root, "Assets", "Levels", "Archive", "PLOP_Daily_001.json");
            var daily = BalancedCampaign.BuildDaily(LevelSerializer.PackFromJson(File.ReadAllText(dailyPath)), levels, seed, Console.WriteLine, out string dailyProblem);
            if (daily == null) { Console.Error.WriteLine("DAILY FAILED: " + dailyProblem); return 1; }
            var dailyVerify = LevelVerifier.VerifyProductionPack(daily, expectedCount: BalancedCampaign.DailyCount);
            Console.WriteLine("daily verification: " + (dailyVerify.IsValid ? "passed" : "FAILED") + " — " + dailyVerify);
            if (!dailyVerify.IsValid) return 1;
            foreach (var l in daily)
            {
                var chains = ForcedChainInspector.Inspect(l, 1_000_000);
                if (!chains.Complete || chains.UnresolvedCommands > 0) { Console.Error.WriteLine("  daily chain risk: " + l.Name); return 1; }
            }
            File.WriteAllText(Path.Combine(levelsDir, BalancedCampaign.DailyPackId + ".json"), LevelSerializer.PackToJson(BalancedCampaign.DailyPackId, daily));
            var dailyRows = QualityAuditCommand.Audit(daily, out double dailySeconds, _ => "daily");
            QualityAudit.Finish(dailyRows);
            File.WriteAllText(Path.Combine(docs, "daily_analysis_after.json"), QualityAudit.ToJson(dailyRows, BalancedCampaign.DailyPackId));
            File.WriteAllText(Path.Combine(docs, "daily_audit_after.md"),
                QualityAuditCommand.Summary(dailyRows, BalancedCampaign.DailyPackId, dailySeconds) + "\n## Per puzzle\n\n" + QualityAudit.ToMarkdownTable(dailyRows));
            Console.WriteLine(QualityAuditCommand.Summary(dailyRows, BalancedCampaign.DailyPackId, dailySeconds));

            Console.WriteLine($"kept {build.Levels.Count(b => b.Provenance == LevelProvenance.Kept)} · improved {build.Levels.Count(b => b.Provenance == LevelProvenance.Improved)} · " +
                $"replaced {build.Levels.Count(b => b.Provenance == LevelProvenance.Replaced)} · authored {build.Levels.Count(b => b.Provenance == LevelProvenance.Authored)} · " +
                $"limit changed {build.Levels.Count(b => b.LimitChanged)} · {build.Searches} search evaluations · {build.Seconds:0} s");
            return 0;
        }

        private static string Changes(BalancedBuild build, System.Collections.Generic.IReadOnlyList<QualityAuditRow> after)
        {
            var sb = new StringBuilder("| # | World | Stage | Provenance | Was | Weakness of the Phase 10 level | Intent | Opt | Depth | Density | Quality before → after | Id |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var b in build.Levels)
            {
                var a = after[b.Index];
                string beforeQ = b.PreviousLevel > 0 && build.Previous[b.PreviousLevel - 1].Analysed
                    ? build.Previous[b.PreviousLevel - 1].Quality.Quality.ToString("0", CultureInfo.InvariantCulture) : "–";
                if (b.Provenance == LevelProvenance.Replaced && b.PreviousLevel < 0) beforeQ = "–";
                sb.Append("| ").Append(b.Index + 1).Append(" | ").Append(b.WorldId).Append(" | ").Append(b.Stage).Append(" | ").Append(b.Provenance)
                  .Append(b.LimitChanged ? " (limit " + (b.Level.MoveLimit > 0 ? "set " + b.Level.MoveLimit : "removed") + ")" : "")
                  .Append(" | ").Append(b.PreviousLevel > 0 ? b.PreviousLevel.ToString() : "–").Append(" | ").Append(b.Weakness)
                  .Append(" | ").Append(b.Intent).Append(" | ").Append(a.Optimal).Append(" | ").Append(a.Analysed ? a.Quality.PlanningDepth.ToString() : "–")
                  .Append(" | ").Append(a.Analysed ? a.Quality.DecisionDensity.ToString("0.00", CultureInfo.InvariantCulture) : "–")
                  .Append(" | ").Append(beforeQ).Append(" → ").Append(a.Analysed ? a.Quality.Quality.ToString("0", CultureInfo.InvariantCulture) : "–")
                  .Append(" | `").Append(b.Level.Id).Append("` |\n");
            }
            return sb.ToString();
        }
    }
}
