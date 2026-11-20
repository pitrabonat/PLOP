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
    ///   dotnet run -c Release --project DotNet/Plop.Bench -- --explore 200
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
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--count": count = int.Parse(args[++i]); break;
                    case "--seed": seed = ParseSeed(args[++i]); break;
                    case "--pack": pack = args[++i]; break;
                    case "--report": report = args[++i]; break;
                    case "--explore": explore = int.Parse(args[++i]); break;
                    default: Console.Error.WriteLine("Unknown argument " + args[i]); return 2;
                }
            }
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
            if (!string.IsNullOrEmpty(packPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(packPath)));
                File.WriteAllText(packPath, LevelSerializer.PackToJson("PLOP_Pack_001", named) + "\n");
                Console.WriteLine("Wrote pack: " + packPath);
            }
            if (!string.IsNullOrEmpty(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllText(reportPath, md.ToString());
                Console.WriteLine("Wrote report: " + reportPath);
            }
            return verify.IsValid && deterministic && stats.Accepted == count && stats.ReplayFailures == 0 ? 0 : 1;
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
    }
}
