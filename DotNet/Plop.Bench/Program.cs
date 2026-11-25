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
    /// Usage:
    ///   dotnet run -c Release --project DotNet/Plop.Bench -- --count 100 [--seed 0x504C4F5021] [--pack path.json] [--report path.md]
    ///   dotnet run -c Release --project DotNet/Plop.Bench -- --production [--pack path.json] [--report path.md]
    ///   dotnet run -c Release --project DotNet/Plop.Bench -- --verify-production path.json
    ///   dotnet run -c Release --project DotNet/Plop.Bench -- --deadlocks pack.json output.bytes [maxStatesPerLevel]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            int count = 100;
            ulong seed = new BatchSettings().BaseSeed;
            string pack = null, report = null;
            int explore = 0;
            bool production = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--count": count = int.Parse(args[++i]); break;
                    case "--seed": seed = ParseSeed(args[++i]); break;
                    case "--pack": pack = args[++i]; break;
                    case "--report": report = args[++i]; break;
                    case "--explore": explore = int.Parse(args[++i]); break;
                    case "--perf": return Perf(args[++i]);
                    case "--expanded": return ExpandedBuild.Run(i + 1 < args.Length ? ParseSeed(args[i + 1]) : 0x504C4F50_31300000UL, System.IO.Path.GetFullPath(".."));
                    case "--mech-probe": return MechanicProbe.Run(i + 1 < args.Length ? int.Parse(args[i + 1]) : 3);
                    case "--search-probe": return SearchProbe.Run(i + 1 < args.Length ? int.Parse(args[i + 1]) : 2);
                    case "--audit": return CampaignAudit.Run(args[i + 1], i + 2 < args.Length ? args[i + 2] : null, i + 3 < args.Length ? args[i + 3] : null);
                    case "--insights": return Insights(args[i + 1], i + 2 < args.Length ? args[i + 2] : null);
                    case "--production": production = true; break;
                    case "--deadlocks": return BuildDeadlocks(args.Skip(i + 1).ToArray());
                    case "--verify-production":
                        var levels = LevelSerializer.PackFromJson(File.ReadAllText(args[++i]));
                        var verification = LevelVerifier.VerifyProductionPack(levels);
                        Console.WriteLine(verification);
                        return verification.IsValid ? 0 : 1;
                    default: Console.Error.WriteLine("Unknown argument " + args[i]); return 2;
                }
            }
            if (production) return ProductionBuild.Run(seed, pack, report);
            return explore > 0 ? Explore(explore, seed) : Benchmark(count, seed, pack, report);
        }

        private static ulong ParseSeed(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : ulong.Parse(s, CultureInfo.InvariantCulture);

        public static List<GeneratorSettings> DefaultProfiles() => GeneratorProfiles.Default();

        private static int Explore(int perProfile, ulong seed)
        {
            foreach (var profile in DefaultProfiles())
            {
                var registry = new DuplicateRegistry();
                var scores = new List<double>();
                var lens = new List<int>();
                int fails = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < perProfile; i++)
                {
                    var r = LevelGenerator.Generate(SplitMix64.Derive(seed, (ulong)i), profile, registry);
                    if (!r.Success) { fails++; continue; }
                    scores.Add(r.Level.Metadata.Difficulty.Score);
                    lens.Add(r.Level.Metadata.Solution.Length);
                }
                scores.Sort();
                Console.WriteLine($"{profile.Width}x{profile.Height} p{profile.PlopCount} d{profile.WallDensity}: ok={scores.Count} fail={fails} " +
                                  $"score p10={P(scores, .1):0.0} p50={P(scores, .5):0.0} p90={P(scores, .9):0.0} max={scores.LastOrDefault():0.0} " +
                                  $"len avg={(lens.Count > 0 ? lens.Average() : 0):0.0} time={sw.ElapsedMilliseconds}ms");
            }
            return 0;
        }

        /// <summary>Measures solver cost on every level of a pack and per-move allocations of GameEngine.</summary>
        private static int Perf(string packPath)
        {
            var levels = LevelSerializer.PackFromJson(File.ReadAllText(packPath));
            LevelDefinition worst = null; SolverResult worstResult = null;
            double total = 0;
            foreach (var l in levels)
            {
                var r = BfsSolver.Solve(l);
                total += r.ElapsedMs;
                if (worstResult == null || r.StatesVisited > worstResult.StatesVisited) { worst = l; worstResult = r; }
            }
            var analysis = StateSpaceAnalyzer.Analyze(worst);
            Console.WriteLine($"Solver over {levels.Count} levels: total {total:0.0} ms, avg {total / levels.Count:0.00} ms");
            Console.WriteLine($"Largest search: {worst.Name} {worst.Width}x{worst.Height} p{worst.Plops.Count}: BFS visited {worstResult.StatesVisited} states in {worstResult.ElapsedMs:0.0} ms; full analysis {analysis.ReachableStates} states in {analysis.ElapsedMs:0.0} ms");

            var engine = new GameEngine();
            engine.LoadLevel(worst);
            var rng = new SplitMix64(1);
            for (int i = 0; i < 1000; i++) { engine.TryMove((Direction)rng.NextInt(4)); if (engine.IsCompleted) engine.Restart(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int moves = 0, changed = 0;
            for (int i = 0; i < 100000; i++)
            {
                var r = engine.TryMove((Direction)rng.NextInt(4)); moves++;
                if (r.Changed) changed++;
                if (engine.IsCompleted || engine.MoveCount > 200) engine.Restart();
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine($"GameEngine.TryMove: {moves} commands ({changed} state-changing) in {sw.Elapsed.TotalMilliseconds:0.0} ms, {sw.Elapsed.TotalMilliseconds * 1000 / moves:0.00} us/command, {bytes / (double)moves:0} bytes allocated per command (history snapshot + event array)");
            return 0;
        }

        private static double P(List<double> sorted, double q) => sorted.Count == 0 ? 0 : sorted[(int)Math.Min(sorted.Count - 1, q * sorted.Count)];

        private static int Benchmark(int count, ulong seed, string packPath, string reportPath)
        {
            var settings = new BatchSettings { TargetCount = count, BaseSeed = seed, Profiles = DefaultProfiles(), MaxCalls = count * 20 };
            Console.WriteLine($"Generating {count} levels, base seed 0x{seed:X}, generator v{GeneratorInfo.Version}, solver v{SolverInfo.Version}...");
            var result = BatchGenerator.Generate(settings);
            var stats = result.Stats;

            // Order the pack as a difficulty curve and give friendly names.
            var ordered = result.Levels.OrderBy(l => l.Metadata.Difficulty.Score).ThenBy(l => l.Id, StringComparer.Ordinal).ToList();
            var named = new List<LevelDefinition>();
            for (int i = 0; i < ordered.Count; i++) named.Add(ordered[i].WithMetadata(ordered[i].Metadata, ordered[i].Id, "Level " + (i + 1)));

            // Independent post-checks: full pack verification (structure, ids, duplicates, solvable, solutions shortest).
            var verify = LevelVerifier.VerifyPack(named);
            // Determinism check: regenerate the first 10 seeds and compare canonical output.
            bool deterministic = CheckDeterminism(settings, 10);

            var md = new StringBuilder();
            md.AppendLine($"Base seed `0x{seed:X}`, generator v{GeneratorInfo.Version}, solver v{SolverInfo.Version}, {Environment.ProcessorCount} logical CPUs, single-threaded, .NET {Environment.Version}.");
            md.AppendLine();
            md.Append(stats.ToMarkdown());
            md.AppendLine($"| Pack verification (structure, unique ids, no symmetric duplicates, solvable, shortest) | {(verify.IsValid ? "PASS" : "FAIL: " + verify)} |");
            md.AppendLine($"| Determinism re-run (first 10 calls) | {(deterministic ? "identical" : "DIFFERENT")} |");
            md.AppendLine();
            md.AppendLine("Per-profile acceptance:");
            md.AppendLine();
            md.AppendLine("| Profile | Accepted |");
            md.AppendLine("|---|---|");
            foreach (var g in result.Levels.GroupBy(l => l.Width + "x" + l.Height + " p" + l.Plops.Count).OrderBy(g => g.Key))
                md.AppendLine($"| {g.Key} | {g.Count()} |");

            Console.WriteLine(md.ToString());
            bool success = verify.IsValid && deterministic && stats.Accepted == count && stats.ReplayFailures == 0;
            if (success && !string.IsNullOrEmpty(packPath))
                WriteOutput(packPath, LevelSerializer.PackToJson("PLOP_Pack_001", named) + "\n");
            if (!string.IsNullOrEmpty(reportPath)) WriteOutput(reportPath, md.ToString());
            return success ? 0 : 1;
        }

        private static int GenerateProduction(ulong seed, string packPath, string reportPath)
        {
            Console.WriteLine($"Generating the production campaign, base seed 0x{seed:X}, generator v{GeneratorInfo.Version}...");
            var result = ProductionCatalog.Generate(seed);
            var verify = LevelVerifier.VerifyProductionPack(result.Levels);
            string json = LevelSerializer.PackToJson("PLOP_Pack_001", result.Levels) + "\n";
            bool deterministic = verify.IsValid &&
                json == LevelSerializer.PackToJson("PLOP_Pack_001", ProductionCatalog.Generate(seed).Levels) + "\n";
            bool success = verify.IsValid && deterministic && result.Stats.ReplayFailures == 0;

            var report = new StringBuilder();
            report.AppendLine("# Production campaign generation");
            report.AppendLine();
            report.AppendLine($"Seed `0x{seed:X}`, generator v{GeneratorInfo.Version}, solver v{SolverInfo.Version}, .NET {Environment.Version}. Single-threaded desktop run.");
            report.AppendLine();
            report.Append(result.Stats.ToMarkdown());
            report.AppendLine($"| Strict verification (structure, IDs, duplicates, shortest replay, Undo and Restart) | {(verify.IsValid ? "PASS" : "FAIL: " + verify)} |");
            report.AppendLine($"| Full deterministic regeneration | {(deterministic ? "byte-identical" : "failed")} |");
            report.AppendLine();
            report.AppendLine("| Levels | Count | Shortest moves | Score range | Peak-to-recovery dips | Wall use | Forced coordination | Must move away | Forced order or settlement traps |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            AppendStage(1, 10);
            AppendStage(11, 25);
            AppendStage(26, 50);
            AppendStage(51, 75);
            AppendStage(76, 100);

            Console.WriteLine(report.ToString());
            if (success && !string.IsNullOrEmpty(packPath)) WriteOutput(packPath, json);
            if (!string.IsNullOrEmpty(reportPath)) WriteOutput(reportPath, report.ToString());
            return success ? 0 : 1;

            void AppendStage(int start, int end)
            {
                var stage = result.Levels.Skip(start - 1).Take(end - start + 1).Select(l => l.Metadata).ToList();
                if (stage.Count == 0) return;
                double Metric(LevelMetadata m, string key) => m.Difficulty.Metrics[key];
                int wallUse = stage.Count(m => Metric(m, "wallInteractionMoves") > 0 || Metric(m, "wallDetour") > 0);
                int coordination = stage.Count(m => Metric(m, "requiresCoordination") > 0);
                int away = stage.Count(m => Metric(m, "requiresAwayMove") > 0);
                int order = stage.Count(m => Metric(m, "requiredSettlementPairs") > 0 || Metric(m, "deadEndSettlements") > 0);
                int dips = 0;
                for (int i = 4; i < stage.Count; i += 5)
                    if (stage[i].Difficulty.Score < stage[i - 1].Difficulty.Score) dips++;
                report.AppendLine($"| {start}–{end} | {stage.Count} | {stage.Min(m => m.Solution.Length)}–{stage.Max(m => m.Solution.Length)} | " +
                    $"{stage.Min(m => m.Difficulty.Score):0.0}–{stage.Max(m => m.Difficulty.Score):0.0} | {dips} | {wallUse} | {coordination} | {away} | {order} |");
            }
        }

        private static int BuildDeadlocks(string[] args)
        {
            int maxStates = 100_000;
            if (args.Length < 2 || args.Length > 3 ||
                (args.Length == 3 && (!int.TryParse(args[2], out maxStates) || maxStates < 1 || maxStates > 2_000_000)))
            {
                Console.Error.WriteLine("Usage: --deadlocks pack.json output.bytes [maxStatesPerLevel, 1–2000000]");
                return 2;
            }

            string input = Path.GetFullPath(args[0]);
            string output = Path.GetFullPath(args[1]);
            if (string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Choose a separate output path for the deadlock catalog.");
                return 2;
            }

            var levels = LevelSerializer.PackFromJson(File.ReadAllText(input), includeDevData: false);
            var validation = LevelValidator.ValidatePack(levels);
            if (!validation.IsValid)
            {
                Console.Error.WriteLine(validation);
                return 1;
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            byte[] data = DeadlockCatalog.Generate(levels, maxStates, out var analyses);
            if (!DeadlockCatalog.TryRead(data, out var catalog))
            {
                Console.Error.WriteLine("Generated deadlock catalog could not be read.");
                return 1;
            }

            var complete = analyses.Where(analysis => analysis.Complete).ToList();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var analysis = analyses[i];
                var table = catalog.ForLevel(level);
                if (analysis.Complete &&
                    (table == null || table.Classify(level.CreateInitialState()) !=
                        (analysis.Solvable ? DeadlockStatus.Solvable : DeadlockStatus.ProvenDeadlock)))
                {
                    Console.Error.WriteLine("Round-trip classification failed for " + level.Id);
                    return 1;
                }

                string proof = analysis.Complete ? $"{analysis.DeadEndStates} deadlocks" : "uncovered: " + analysis.Message;
                Console.WriteLine($"{level.Name} ({level.Id}): {analysis.ReachableStates} states, {proof}, {analysis.ElapsedMs:F1} ms");
            }

            Console.WriteLine($"Complete {catalog.LevelCount}/{levels.Count} at {maxStates} states per level. " +
                              $"{complete.Sum(a => (long)a.ReachableStates):N0} exact keys, " +
                              $"{complete.Sum(a => (long)a.DeadEndStates):N0} deadlock keys, " +
                              $"{complete.Count(a => a.DeadEndStates > 0)} levels with deadlocks, " +
                              $"largest graph {complete.Select(a => a.ReachableStates).DefaultIfEmpty().Max():N0} states.");
            Console.WriteLine($"{data.Length:N0} bytes in {clock.Elapsed.TotalSeconds:F2}s.");

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, data);
                File.Move(temporary, output, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
            Console.WriteLine("Wrote " + output);
            return 0;
        }

        private static void WriteOutput(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, contents);
            Console.WriteLine("Wrote " + path);
        }

        private static bool CheckDeterminism(BatchSettings settings, int calls)
        {
            var a = new BatchSettings { TargetCount = calls, BaseSeed = settings.BaseSeed, Profiles = settings.Profiles, MaxCalls = calls };
            var r1 = BatchGenerator.Generate(a);
            var r2 = BatchGenerator.Generate(a);
            if (r1.Levels.Count != r2.Levels.Count) return false;
            for (int i = 0; i < r1.Levels.Count; i++)
                if (LevelSerializer.ToJson(r1.Levels[i]) != LevelSerializer.ToJson(r2.Levels[i])) return false;
            return true;
        }
    
        /// <summary>Writes the Level Lab campaign overview (TSV) for a pack: --insights pack.json [out.tsv].</summary>
        private static int Insights(string packPath, string output)
        {
            var levels = Plop.Core.LevelSerializer.PackFromJson(System.IO.File.ReadAllText(packPath));
            var rows = Plop.Generation.CampaignInsights.AnalyzeAll(levels);
            string table = Plop.Generation.CampaignInsights.ToTable(rows);
            if (output != null) System.IO.File.WriteAllText(output, table); else System.Console.Write(table);
            int trivial = rows.Count(r => r.IsTrivial);
            System.Console.Error.WriteLine(rows.Count + " levels analysed, " + trivial + " flagged trivial.");
            return 0;
        }
}
}
