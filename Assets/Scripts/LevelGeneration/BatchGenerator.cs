using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Plop.Core;

namespace Plop.Generation
{
    public sealed class BatchSettings
    {
        public int TargetCount = 100;
        public ulong BaseSeed = 0x504C4F5021UL; // "PLOP!"
        /// <summary>Profiles are used round-robin, one generation call per seed.</summary>
        public List<GeneratorSettings> Profiles = new List<GeneratorSettings>();
        /// <summary>Hard cap on generation calls (each call tries up to profile.MaxAttempts candidates).</summary>
        public int MaxCalls = 2000;
        /// <summary>Optional wall-clock cap in milliseconds (0 = none).</summary>
        public int MaxMilliseconds = 0;
        public CancellationToken Cancellation = CancellationToken.None;
    }

    public sealed class BatchStats
    {
        public int Calls;
        public int FailedCalls;
        public int Candidates;
        public int Accepted;
        public readonly int[] Rejections = new int[Enum.GetValues(typeof(RejectReason)).Length];
        public double ElapsedMs;
        public readonly int[] CategoryCounts = new int[Enum.GetValues(typeof(DifficultyCategory)).Length];
        public double AverageSolutionLength;
        public int MinSolutionLength = int.MaxValue;
        public int MaxSolutionLength;
        public double AverageCandidateMs;
        public int ReplayFailures;
        public bool StoppedByLimit;

        internal void RecordGeneration(GenerationResult result)
        {
            Calls++;
            Candidates += result.Attempts;
            if (!result.Success) FailedCalls++;
            for (int i = 0; i < result.Rejections.Length; i++) Rejections[i] += result.Rejections[i];
        }

        internal void RecordAcceptance(LevelDefinition level)
        {
            var moves = DirectionUtil.ParseMoveString(level.Metadata.Solution);
            if (!Plop.Solver.BfsSolver.VerifySolution(level, moves)) ReplayFailures++;
            AverageSolutionLength = (AverageSolutionLength * Accepted + moves.Length) / (Accepted + 1);
            Accepted++;
            MinSolutionLength = Math.Min(MinSolutionLength, moves.Length);
            MaxSolutionLength = Math.Max(MaxSolutionLength, moves.Length);
            CategoryCounts[(int)level.Metadata.Difficulty.Category]++;
        }

        public int Rejected
        {
            get { int n = 0; foreach (int r in Rejections) n += r; return n; }
        }

        public double DuplicateRejectionRate => Candidates == 0 ? 0 : (double)Rejections[(int)RejectReason.Duplicate] / Candidates;

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Generation calls | " + Calls + " (" + FailedCalls + " exhausted attempts) |");
            sb.AppendLine("| Candidates generated | " + Candidates + " |");
            sb.AppendLine("| Rejected candidates | " + Rejected + " |");
            sb.AppendLine("| Accepted levels | " + Accepted + " |");
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)))
                sb.AppendLine("| Rejected: " + r + " | " + Rejections[(int)r] + " |");
            sb.AppendLine("| Solver failures (proven unsolvable) | " + Rejections[(int)RejectReason.Unsolvable] + " |");
            sb.AppendLine("| Search-budget failures | " + Rejections[(int)RejectReason.SearchBudget] + " |");
            sb.AppendLine("| Duplicate rejection rate | " + (DuplicateRejectionRate * 100).ToString("0.00", ci) + "% of candidates |");
            sb.AppendLine("| Generation duration | " + (ElapsedMs / 1000.0).ToString("0.00", ci) + " s |");
            sb.AppendLine("| Avg generation time per candidate | " + AverageCandidateMs.ToString("0.00", ci) + " ms |");
            sb.AppendLine("| Solution length avg / min / max | " + AverageSolutionLength.ToString("0.0", ci) + " / " + (Accepted > 0 ? MinSolutionLength : 0) + " / " + MaxSolutionLength + " |");
            foreach (DifficultyCategory c in Enum.GetValues(typeof(DifficultyCategory)))
                sb.AppendLine("| Difficulty " + c + " | " + CategoryCounts[(int)c] + " |");
            sb.AppendLine("| Accepted solutions replay-verified via GameEngine | " + (Accepted - ReplayFailures) + " / " + Accepted + " |");
            return sb.ToString();
        }
    }

    public sealed class BatchResult
    {
        public readonly List<LevelDefinition> Levels = new List<LevelDefinition>();
        public readonly BatchStats Stats = new BatchStats();
        public string Message;
    }

    /// <summary>Generates a set of unique verified levels across several generator profiles.</summary>
    public static class BatchGenerator
    {
        public static BatchResult Generate(BatchSettings settings, DuplicateRegistry registry = null, Action<int, LevelDefinition> onAccepted = null)
        {
            if (settings.Profiles == null || settings.Profiles.Count == 0) throw new ArgumentException("At least one profile is required.");
            registry = registry ?? new DuplicateRegistry();
            var result = new BatchResult();
            var stats = result.Stats;
            var sw = Stopwatch.StartNew();

            for (int call = 0; result.Levels.Count < settings.TargetCount; call++)
            {
                if (call >= settings.MaxCalls || settings.Cancellation.IsCancellationRequested ||
                    (settings.MaxMilliseconds > 0 && sw.ElapsedMilliseconds > settings.MaxMilliseconds))
                {
                    stats.StoppedByLimit = true;
                    break;
                }

                var profile = settings.Profiles[call % settings.Profiles.Count];
                ulong seed = SplitMix64.Derive(settings.BaseSeed, (ulong)call);
                var gen = LevelGenerator.Generate(seed, profile, registry);
                stats.RecordGeneration(gen);
                if (!gen.Success) continue;

                var level = gen.Level;
                result.Levels.Add(level);
                stats.RecordAcceptance(level);
                onAccepted?.Invoke(result.Levels.Count - 1, level);
            }

            stats.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            stats.AverageCandidateMs = stats.Candidates == 0 ? 0 : stats.ElapsedMs / stats.Candidates;
            return result;
        }
    }
}
