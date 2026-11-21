using System;
using System.Diagnostics;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Offline recipe for the ordered first campaign.</summary>
    public static class ProductionCatalog
    {
        public const int LevelCount = 100;

        private const int TutorialCount = 10;
        private const int AttemptsPerSlot = 400;
        private static readonly string[] TutorialInstructions =
        {
            "Swipe right to reach the matching Nest.",
            "Each swipe moves a Plop one tile.",
            "Combine swipes to reach the Nest.",
            "Go around the pebble.",
            "One swipe moves every awake Plop.",
            "A Plop stays asleep in its own Nest.",
            "Use a pebble to hold one Plop back.",
            "Sleeping Plops can block the way.",
            "A Plop can pass through another color's Nest.",
            "Try Undo to take back your last move."
        };

        private static readonly string[][] TutorialBoards =
        {
            new[] { "....", "....", "Rr..", "...." },
            new[] { "..r.", "....", "..R.", "....", "...." },
            new[] { "...r", "....", ".R..", "...." },
            new[] { "....", "....", "R#r.", "...." },
            new[] { "....", "R.r.", "B.b.", "...." },
            new[] { ".....", "Rr...", "B...b", "....." },
            new[] { "..rR", "..B#", "....", "...b" },
            new[] { "..b", "Rr.", "#B#" },
            new[] { ".....", "r.bRB", "....." },
            new[] { "r...", ".#..", ".R.b", "B..." }
        };

        /// <summary>
        /// Curated onboarding followed by five-level waves. Stops at the first unfilled slot so
        /// callers can inspect partial results without shifting the campaign's intended order.
        /// </summary>
        public static BatchResult Generate(ulong seed, DuplicateRegistry registry = null)
        {
            var watch = Stopwatch.StartNew();
            registry = registry ?? new DuplicateRegistry();
            var result = new BatchResult();

            for (int i = 0; i < LevelCount; i++)
            {
                var generated = i < TutorialCount ? BuildTutorial(i, registry)
                    : LevelGenerator.Generate(SplitMix64.Derive(seed, (ulong)i), ProfileFor(i), registry);
                result.Stats.RecordGeneration(generated);
                if (!generated.Success)
                {
                    result.Stats.StoppedByLimit = true;
                    break;
                }

                var level = generated.Level.WithMetadata(generated.Level.Metadata, name: "Level " + (i + 1));
                result.Levels.Add(level);
                result.Stats.RecordAcceptance(level);
            }

            var stats = result.Stats;
            stats.ElapsedMs = watch.Elapsed.TotalMilliseconds;
            stats.AverageCandidateMs = stats.Candidates == 0 ? 0 : stats.ElapsedMs / stats.Candidates;
            return result;
        }

        private static GenerationResult BuildTutorial(int index, DuplicateRegistry registry)
        {
            var layout = LevelText.Parse("Tutorial", TutorialBoards[index]);
            string id = LevelCanonicalizer.CanonicalId(layout);
            var level = new LevelDefinition(id, id, layout.Width, layout.Height, layout.Walls, layout.Plops,
                layout.Nests, instruction: TutorialInstructions[index]);
            var analysis = StateSpaceAnalyzer.Analyze(level);
            var result = new GenerationResult { Attempts = 1, Analysis = analysis };
            if (!analysis.Solvable || !BfsSolver.VerifySolution(level, analysis.Solution))
                result.Rejections[(int)RejectReason.VerificationFailed]++;
            else if (registry.Contains(level))
                result.Rejections[(int)RejectReason.Duplicate]++;
            else if (registry.ContainsNearDuplicate(level))
                result.Rejections[(int)RejectReason.NearDuplicate]++;
            else
            {
                registry.TryAdd(level);
                result.Level = level.WithMetadata(new LevelMetadata
                {
                    SolverVersion = SolverInfo.Version,
                    Solution = DirectionUtil.ToMoveString(analysis.Solution),
                    Difficulty = DifficultyEstimator.Estimate(level, analysis)
                });
                result.Success = true;
            }
            return result;
        }

        private static GeneratorSettings ProfileFor(int index)
        {
            // Public numbering: 11–25 basic, 26–50 coordination, 51–75 planning, 76–100 mastery.
            int stage = index < 25 ? 0 : index < 50 ? 1 : index < 75 ? 2 : 3;
            int offset = index - (stage == 0 ? 10 : stage * 25);
            int wave = offset / 5;
            int slot = offset % 5;
            bool peak = slot == 3, recovery = slot == 4;
            double baseline = 14 + 8 * stage + 1.25 * wave;
            int size = 4 + (stage + 1) / 2 + ((index + wave) % 2);

            return new GeneratorSettings
            {
                Width = size,
                Height = size + (index % 3 == 0 ? 1 : 0),
                PlopCount = stage == 0 || (stage == 1 && !peak) || (stage == 2 && recovery) ? 2 : 3,
                WallDensity = 0.14 + 0.034 * stage + 0.02 * ((index + wave) % 3),
                MaxAttempts = AttemptsPerSlot,
                SolverMaxStates = 150_000,
                MinSolutionLength = Math.Max(3, 3 + 2 * stage + (peak ? 2 : recovery ? -1 : 0)),
                MaxSolutionLength = 10 + 5 * stage,
                MinCoordinationMoves = recovery ? 1 : 1 + stage,
                MinSolutionTurns = stage < 2 || recovery ? 1 : 2,
                MinimumScore = baseline + (peak ? 6 : recovery ? -3 : slot),
                MaximumScore = baseline + (peak ? 15 : recovery ? 3 : 10),
                RequireCoordination = stage > 0 && !recovery,
                RequireWallUse = stage > 0 && !recovery,
                RequirePlanning = stage > 1 && !recovery,
                RejectNearDuplicates = true
            };
        }
    }
}
