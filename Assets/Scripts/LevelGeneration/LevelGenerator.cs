using System;
using System.Collections.Generic;
using System.Globalization;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    public static class GeneratorInfo
    {
        /// <summary>Bump whenever the same seed + settings could produce a different level.</summary>
        public const int Version = 3;
    }

    public sealed class GeneratorSettings
    {
        public int Width = 5;
        public int Height = 5;
        public int PlopCount = 2;
        /// <summary>Probability that a cell starts as a wall (before connectivity cleanup).</summary>
        public double WallDensity = 0.2;
        /// <summary>Accept only this category (null = any).</summary>
        public DifficultyCategory? TargetDifficulty;
        /// <summary>Candidate boards tried before giving up.</summary>
        public int MaxAttempts = 400;
        /// <summary>State budget of the verification analysis per candidate.</summary>
        public int SolverMaxStates = 300_000;
        /// <summary>Solutions shorter than this are rejected as trivial.</summary>
        public int MinSolutionLength = 4;
        public int MaxSolutionLength = 60;
        public DifficultyWeights Weights = DifficultyWeights.Default;

        public double MinimumScore = 0;
        public double MaximumScore = double.PositiveInfinity;
        public int MinCoordinationMoves;
        public int MinSolutionTurns;
        public bool RequireCoordination;
        public bool RequireWallUse;
        public bool RequirePlanning;
        public bool RejectNearDuplicates;

        internal bool MeetsQuality(DifficultyInfo difficulty)
        {
            var metrics = difficulty.Metrics;
            return difficulty.Score >= MinimumScore && difficulty.Score <= MaximumScore
                && metrics["coordinationMoves"] >= MinCoordinationMoves
                && (MinSolutionTurns == 0 || metrics["solutionTurns"] >= MinSolutionTurns)
                && (!RequireCoordination || metrics["requiresCoordination"] > 0)
                && (!RequireWallUse || metrics["wallInteractionMoves"] > 0 || metrics["wallDetour"] > 0)
                && (!RequirePlanning || metrics["requiresAwayMove"] > 0
                    || metrics["requiredSettlementPairs"] > 0 || metrics["deadEndSettlements"] > 0);
        }

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "w={0} h={1} plops={2} walls={3:0.###} target={4} minLen={5} maxLen={6} solverStates={7}",
            Width, Height, PlopCount, WallDensity, TargetDifficulty?.ToString() ?? "any", MinSolutionLength, MaxSolutionLength, SolverMaxStates)
            + string.Format(CultureInfo.InvariantCulture, " score={0:0.##}..{1:0.##} coordination={2} turns={3} coordinationRequired={4} wallsMatter={5} planning={6} nearDuplicates={7}",
                MinimumScore, MaximumScore, MinCoordinationMoves, MinSolutionTurns, RequireCoordination, RequireWallUse, RequirePlanning, RejectNearDuplicates);

        public void Validate()
        {
            if (Width < 2 || Height < 2 || Width > LevelValidator.MaxSize || Height > LevelValidator.MaxSize)
                throw new ArgumentException("Board size out of range.");
            if (PlopCount < 1 || PlopCount > PlopColorUtil.Count) throw new ArgumentException("PlopCount must be 1.." + PlopColorUtil.Count + ".");
            if (!(WallDensity >= 0 && WallDensity <= 0.8)) throw new ArgumentException("WallDensity must be 0..0.8.");
            if (MaxAttempts < 1 || SolverMaxStates < 1) throw new ArgumentException("Budgets must be positive.");
            if (MinSolutionLength < 0 || MaxSolutionLength < MinSolutionLength ||
                !(MinimumScore >= 0 && MaximumScore >= MinimumScore) || MinCoordinationMoves < 0 || MinSolutionTurns < 0)
                throw new ArgumentException("Quality bounds are invalid.");
        }
    }

    public enum RejectReason
    {
        StructureInvalid,
        SearchBudget,
        Unsolvable,
        Trivial,
        TooLong,
        DifficultyMismatch,
        Duplicate,
        VerificationFailed,
        QualityMismatch,
        NearDuplicate
    }

    public sealed class GenerationResult
    {
        public bool Success;
        public LevelDefinition Level;
        public StateSpaceAnalysis Analysis;
        public int Attempts;
        public readonly int[] Rejections = new int[Enum.GetValues(typeof(RejectReason)).Length];
        public string Message;

        public int RejectionCount(RejectReason r) => Rejections[(int)r];

        public string Diagnostics()
        {
            var parts = new List<string>();
            foreach (RejectReason r in Enum.GetValues(typeof(RejectReason)))
                if (Rejections[(int)r] > 0) parts.Add(r + "=" + Rejections[(int)r]);
            return (Success ? "accepted" : "FAILED") + " after " + Attempts + " attempt(s)" +
                   (parts.Count > 0 ? "; rejected: " + string.Join(", ", parts) : "") +
                   (Message != null ? "; " + Message : "");
        }
    }

    /// <summary>
    /// Deterministic procedural generator. For a given seed, settings, generator version and duplicate
    /// registry contents, the output is always identical (single SplitMix64 stream, no clocks).
    /// Pipeline per attempt: geometry -> walls -> Plops -> Nests -> structural validation ->
    /// full state-space analysis (proves solvability within budget) -> triviality/length checks ->
    /// difficulty -> duplicate check -> solution replay through GameEngine -> accept.
    /// </summary>
    public static class LevelGenerator
    {
        public static GenerationResult Generate(ulong seed, GeneratorSettings settings, DuplicateRegistry registry = null)
        {
            settings.Validate();
            var rng = new SplitMix64(seed);
            var result = new GenerationResult();

            for (int attempt = 1; attempt <= settings.MaxAttempts; attempt++)
            {
                result.Attempts = attempt;
                var candidate = BuildCandidate(rng, settings);
                if (candidate == null || !LevelValidator.Validate(candidate).IsValid)
                {
                    result.Rejections[(int)RejectReason.StructureInvalid]++;
                    continue;
                }

                var analysis = StateSpaceAnalyzer.Analyze(candidate, settings.SolverMaxStates);
                if (!analysis.Complete) { result.Rejections[(int)RejectReason.SearchBudget]++; continue; }
                if (analysis.StartDistance < 0) { result.Rejections[(int)RejectReason.Unsolvable]++; continue; }
                if (analysis.StartDistance < settings.MinSolutionLength) { result.Rejections[(int)RejectReason.Trivial]++; continue; }
                if (analysis.StartDistance > settings.MaxSolutionLength) { result.Rejections[(int)RejectReason.TooLong]++; continue; }

                var difficulty = DifficultyEstimator.Estimate(candidate, analysis, settings.Weights);
                if (settings.TargetDifficulty.HasValue && difficulty.Category != settings.TargetDifficulty.Value)
                {
                    result.Rejections[(int)RejectReason.DifficultyMismatch]++;
                    continue;
                }

                if (!settings.MeetsQuality(difficulty))
                {
                    result.Rejections[(int)RejectReason.QualityMismatch]++;
                    continue;
                }

                string canonical = LevelCanonicalizer.Canonicalize(candidate);
                if (registry != null && registry.ContainsCanonical(canonical))
                {
                    result.Rejections[(int)RejectReason.Duplicate]++;
                    continue;
                }

                if (settings.RejectNearDuplicates && registry != null && registry.ContainsNearDuplicate(candidate))
                {
                    result.Rejections[(int)RejectReason.NearDuplicate]++;
                    continue;
                }

                if (!BfsSolver.VerifySolution(candidate, analysis.Solution))
                {
                    result.Rejections[(int)RejectReason.VerificationFailed]++;
                    continue;
                }

                string id = "PLOP-" + LevelCanonicalizer.Fnv1a64(canonical).ToString("X16").Substring(0, 12);
                var meta = new LevelMetadata
                {
                    GeneratorVersion = GeneratorInfo.Version,
                    Seed = seed,
                    GeneratorParams = settings.Describe() + " attempt=" + attempt,
                    SolverVersion = SolverInfo.Version,
                    Solution = DirectionUtil.ToMoveString(analysis.Solution),
                    Difficulty = difficulty
                };
                registry?.TryAddCanonical(canonical);
                result.Success = true;
                result.Level = candidate.WithMetadata(meta, id, id);
                result.Analysis = analysis;
                return result;
            }

            result.Message = "no candidate passed all checks within " + settings.MaxAttempts + " attempts";
            return result;
        }

        /// <summary>Builds one random candidate, or null if the board has too little connected floor.</summary>
        private static LevelDefinition BuildCandidate(SplitMix64 rng, GeneratorSettings s)
        {
            int w = s.Width, h = s.Height, cells = w * h;
            var wall = new bool[cells];
            for (int i = 0; i < cells; i++) wall[i] = rng.NextDouble() < s.WallDensity;

            // Keep only the largest 4-connected floor region; isolated pockets become walls.
            var region = new int[cells];
            for (int i = 0; i < cells; i++) region[i] = -1;
            int bestRegion = -1, bestSize = 0, regionCount = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < cells; i++)
            {
                if (wall[i] || region[i] >= 0) continue;
                int size = 0;
                region[i] = regionCount;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    size++;
                    int x = c % w, y = c / w;
                    if (x > 0) Visit(c - 1);
                    if (x < w - 1) Visit(c + 1);
                    if (y > 0) Visit(c - w);
                    if (y < h - 1) Visit(c + w);
                }
                if (size > bestSize) { bestSize = size; bestRegion = regionCount; }
                regionCount++;
            }

            void Visit(int c)
            {
                if (!wall[c] && region[c] < 0) { region[c] = regionCount; stack.Push(c); }
            }

            if (bestSize < 2 * s.PlopCount + 2) return null;

            var floor = new List<int>();
            var walls = new List<GridPosition>();
            for (int i = 0; i < cells; i++)
            {
                if (region[i] == bestRegion) floor.Add(i);
                else walls.Add(new GridPosition(i % w, i / w));
            }

            // Fisher-Yates partial shuffle to pick distinct floor cells for Plops then Nests.
            int needed = 2 * s.PlopCount;
            for (int i = 0; i < needed; i++)
            {
                int j = i + rng.NextInt(floor.Count - i);
                int t = floor[i]; floor[i] = floor[j]; floor[j] = t;
            }

            var plops = new PlopDefinition[s.PlopCount];
            var nests = new NestDefinition[s.PlopCount];
            for (int i = 0; i < s.PlopCount; i++)
            {
                int pc = floor[i], nc = floor[s.PlopCount + i];
                plops[i] = new PlopDefinition(i, (PlopColor)i, new GridPosition(pc % w, pc / w));
                nests[i] = new NestDefinition((PlopColor)i, new GridPosition(nc % w, nc / w));
            }
            return new LevelDefinition("candidate", "candidate", w, h, walls, plops, nests);
        }
    }
}
