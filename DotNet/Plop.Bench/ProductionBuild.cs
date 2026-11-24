using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Phase 8 production build: campaign (parallel slot search, sequential ordering/dedup — byte-identical
    /// to the sequential Level Lab path), daily pool, slot history for save migration, strict verification.
    /// Usage: --production --pack Assets/Levels/Resources/Levels/PLOP_Pack_001.json --report Documentation/benchmarks/generation_benchmark_v3.md
    /// </summary>
    internal static class ProductionBuild
    {
        public static int Run(ulong seed, string packPath, string reportPath)
        {
            if (string.IsNullOrEmpty(packPath)) { Console.Error.WriteLine("--pack is required"); return 2; }
            string dir = Path.GetDirectoryName(Path.GetFullPath(packPath));
            string dailyPath = Path.Combine(dir, "PLOP_Daily_001.json");
            string historyPath = Path.Combine(dir, "PLOP_Pack_001_SlotHistory.json");

            var previous = File.Exists(packPath) ? LevelSerializer.PackFromJson(File.ReadAllText(packPath)) : new List<LevelDefinition>();
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"Phase 8 campaign: seed 0x{seed:X}, generator v{GeneratorInfo.Version}, quality search v{QualitySearch.Version}, plan v{CampaignPlan.Version}, {Environment.ProcessorCount} threads");

            var slotCache = new ConcurrentDictionary<int, QualitySearchResult>();
            int done = 0;
            Parallel.For(CampaignPlan.TutorialCount, ProductionCatalog.LevelCount, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var r = ProductionCatalog.SearchSlotRaw(seed, i, 0);
                slotCache[i] = r;
                int n = System.Threading.Interlocked.Increment(ref done);
                if (n % 10 == 0) Console.WriteLine($"  searched {n}/90 slots ({sw.Elapsed.TotalSeconds:0}s)");
            });
            double parallelSeconds = sw.Elapsed.TotalSeconds;
            var campaign = ProductionCatalog.Generate(seed, null, i => slotCache[i]);
            if (campaign.Levels.Count != ProductionCatalog.LevelCount)
            {
                Console.Error.WriteLine("Campaign incomplete: " + campaign.Levels.Count + " levels.");
                return 1;
            }

            var dailyCache = new ConcurrentDictionary<int, QualitySearchResult>();
            Parallel.For(0, ProductionCatalog.DailyPoolSize, i => dailyCache[i] = ProductionCatalog.SearchDailyRaw(seed, i, 0));
            var daily = ProductionCatalog.GenerateDailyPool(seed, campaign.Levels, i => dailyCache[i]);
            double totalSeconds = sw.Elapsed.TotalSeconds;

            // Strict checks
            var verify = LevelVerifier.VerifyProductionPack(campaign.Levels);
            var dailyVerify = LevelVerifier.VerifyPack(daily);
            bool dailyOptimal = daily.All(l => l.OptimalMoves == DirectionUtil.ParseMoveString(l.Metadata.Solution).Length);
            // Determinism: sequential search (no cache) must reproduce sampled slots exactly.
            bool deterministic = true;
            foreach (int i in new[] { 10, 47, 99 })
            {
                var again = ProductionCatalog.SearchSlotRaw(seed, i, 0);
                if (!again.Success || LevelCanonicalizer.Canonicalize(again.Level) != LevelCanonicalizer.Canonicalize(slotCache[i].Level))
                    deterministic = false;
            }

            string json = LevelSerializer.PackToJson("PLOP_Pack_001", campaign.Levels) + "\n";
            string dailyJson = LevelSerializer.PackToJson("PLOP_Daily_001", daily) + "\n";
            bool success = verify.IsValid && dailyVerify.IsValid && dailyOptimal && deterministic && campaign.Stats.ReplayFailures == 0;

            var report = new StringBuilder();
            report.AppendLine("# Production campaign generation — Phase 8 (quality search)");
            report.AppendLine();
            report.AppendLine($"Seed `0x{seed:X}`, generator v{GeneratorInfo.Version}, quality search v{QualitySearch.Version}, campaign plan v{CampaignPlan.Version}, solver v{SolverInfo.Version}, .NET {Environment.Version}, {Environment.ProcessorCount} worker threads.");
            report.AppendLine();
            report.AppendLine("| Metric | Value |");
            report.AppendLine("|---|---|");
            report.AppendLine($"| Campaign levels | {campaign.Levels.Count} (10 authored tutorials + 90 quality-searched) |");
            report.AppendLine($"| Board evaluations (exhaustive analyses) | {campaign.Stats.Candidates} |");
            report.AppendLine($"| Slot searches that needed a retry (failed/duplicate) | {campaign.Stats.Rejected} |");
            report.AppendLine($"| Daily pool | {daily.Count} puzzles |");
            report.AppendLine($"| Wall time | {parallelSeconds:0.0} s parallel slot search, {totalSeconds:0.0} s total |");
            report.AppendLine($"| Strict campaign verification (structure, canonical IDs, duplicates, shortest replay, optimalMoves, Undo/Restart) | {(verify.IsValid ? "PASS" : "FAIL: " + verify)} |");
            report.AppendLine($"| Daily pool verification + optimalMoves | {(dailyVerify.IsValid && dailyOptimal ? "PASS" : "FAIL: " + dailyVerify)} |");
            report.AppendLine($"| Sequential re-search of slots 11, 48, 100 | {(deterministic ? "identical" : "DIFFERENT")} |");
            report.AppendLine();
            report.AppendLine("Per-level player-model metrics: see `campaign_audit_phase8.md`.");

            Console.WriteLine(report.ToString());
            if (!success) { Console.Error.WriteLine("Verification failed; nothing written."); return 1; }

            File.WriteAllText(packPath, json);
            File.WriteAllText(dailyPath, dailyJson);
            WriteSlotHistory(historyPath, previous, campaign.Levels);
            if (!string.IsNullOrEmpty(reportPath)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))); File.WriteAllText(reportPath, report.ToString()); }
            Console.WriteLine("Wrote " + packPath + ", " + dailyPath + ", " + historyPath);
            return 0;
        }

        /// <summary>
        /// Save migration data: for every campaign slot, the IDs that previously occupied it (accumulated across
        /// rebuilds). LevelProgress maps saved IDs that are no longer in the pack to the same slot.
        /// </summary>
        private static void WriteSlotHistory(string path, List<LevelDefinition> previous, List<LevelDefinition> current)
        {
            var slots = new List<List<string>>();
            for (int i = 0; i < current.Count; i++) slots.Add(new List<string>());
            if (File.Exists(path))
            {
                var old = (Dictionary<string, object>)Plop.Core.Json.MiniJson.Parse(File.ReadAllText(path));
                var arr = (List<object>)old["slots"];
                for (int i = 0; i < arr.Count && i < slots.Count; i++)
                    foreach (var id in (List<object>)((Dictionary<string, object>)arr[i])["previousIds"]) slots[i].Add((string)id);
            }
            for (int i = 0; i < previous.Count && i < current.Count; i++)
                if (previous[i].Id != current[i].Id && !slots[i].Contains(previous[i].Id)) slots[i].Add(previous[i].Id);

            var w = new Plop.Core.Json.JsonWriter(true);
            w.BeginObject();
            w.Key("schemaVersion").Value(1);
            w.Key("packId").Value("PLOP_Pack_001");
            w.Key("note").Value("Slot i (0-based) lists level IDs that occupied it in earlier campaign versions. Used only to migrate saves.");
            w.Key("slots").BeginArray();
            for (int i = 0; i < current.Count; i++)
            {
                w.BeginObject();
                w.Key("slot").Value(i);
                w.Key("id").Value(current[i].Id);
                w.Key("previousIds").BeginArray();
                foreach (var id in slots[i]) w.Value(id);
                w.EndArray();
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
            File.WriteAllText(path, w + "\n");
        }
    }
}
