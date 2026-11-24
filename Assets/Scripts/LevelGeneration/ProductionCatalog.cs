using System;
using System.Collections.Generic;
using System.Diagnostics;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>
    /// Offline recipe for the ordered first campaign (Phase 8): the ten authored tutorials, then 90
    /// quality-searched puzzles following <see cref="CampaignPlan"/>. Also builds the daily-puzzle pool.
    /// </summary>
    public static class ProductionCatalog
    {
        public const int LevelCount = 100;

        private const int TutorialCount = CampaignPlan.TutorialCount;
        private static readonly string[] TutorialInstructions =
        {
            "Swipe the Plop into its matching Nest.",
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

        public const int DailyPoolSize = 60;
        public const int MaxSlotRetries = 12;

        /// <summary>
        /// Builds the campaign in order. Slot searches depend only on (seed, slot, retry), never on other
        /// slots, so <paramref name="precomputed"/> (e.g. a parallel cache keyed by slot index for retry 0)
        /// yields byte-identical output. Duplicates are rejected in campaign order and retried with the next
        /// derived seed. Stops at the first slot that cannot be filled.
        /// </summary>
        public static BatchResult Generate(ulong seed, DuplicateRegistry registry = null,
            Func<int, QualitySearchResult> precomputed = null, Action<int> progress = null)
        {
            var watch = Stopwatch.StartNew();
            registry = registry ?? new DuplicateRegistry();
            var result = new BatchResult();

            for (int i = 0; i < LevelCount; i++)
            {
                progress?.Invoke(i);
                GenerationResult generated;
                if (i < TutorialCount) generated = BuildTutorial(i, registry);
                else
                {
                    // Difficulty waves: a breather (5th level of a wave) must score below the peak before it.
                    bool breather = (i - TutorialCount) % 5 == 4;
                    double? ceiling = breather && result.Levels.Count > 0
                        ? result.Levels[result.Levels.Count - 1].Metadata.Difficulty.Score : (double?)null;
                    generated = SearchSlot(seed, i, registry, precomputed, ceiling);
                }
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

        /// <summary>Quality search for one campaign slot (retry 0 has no dependency on other slots).</summary>
        public static QualitySearchResult SearchSlotRaw(ulong seed, int index, int retry)
            => QualitySearch.Search(CampaignPlan.SlotSeed(seed, index, retry), CampaignPlan.TargetFor(index));

        private static GenerationResult SearchSlot(ulong seed, int index, DuplicateRegistry registry,
            Func<int, QualitySearchResult> precomputed, double? challengeCeiling)
        {
            var result = new GenerationResult();
            for (int retry = 0; retry < MaxSlotRetries; retry++)
            {
                var found = retry == 0 && precomputed != null ? precomputed(index) : SearchSlotRaw(seed, index, retry);
                result.Attempts += found.Evaluations;
                if (!found.Success) { result.Rejections[(int)RejectReason.QualityMismatch]++; continue; }
                if (challengeCeiling.HasValue && found.Insight.Challenge >= challengeCeiling.Value)
                {
                    result.Rejections[(int)RejectReason.DifficultyMismatch]++;
                    continue;
                }
                if (registry.Contains(found.Level)) { result.Rejections[(int)RejectReason.Duplicate]++; continue; }
                if (registry.ContainsNearDuplicate(found.Level)) { result.Rejections[(int)RejectReason.NearDuplicate]++; continue; }
                if (!BfsSolver.VerifySolution(found.Level, found.Analysis.Solution)) { result.Rejections[(int)RejectReason.VerificationFailed]++; continue; }
                registry.TryAdd(found.Level);
                result.Level = Finalize(found, CampaignPlan.SlotSeed(seed, index, retry), CampaignPlan.TargetFor(index).Describe() + " retry=" + retry);
                result.Analysis = found.Analysis;
                result.Success = true;
                return result;
            }
            result.Message = "slot " + (index + 1) + " not filled after " + MaxSlotRetries + " searches";
            return result;
        }

        /// <summary>Daily-puzzle pool: <see cref="DailyPoolSize"/> verified puzzles, unique against the campaign.</summary>
        public static List<LevelDefinition> GenerateDailyPool(ulong seed, IEnumerable<LevelDefinition> campaign,
            Func<int, QualitySearchResult> precomputed = null)
        {
            var registry = new DuplicateRegistry();
            foreach (var level in campaign) registry.TryAdd(level);
            var pool = new List<LevelDefinition>();
            for (int i = 0; i < DailyPoolSize; i++)
            {
                LevelDefinition accepted = null;
                for (int retry = 0; retry < MaxSlotRetries && accepted == null; retry++)
                {
                    var found = retry == 0 && precomputed != null ? precomputed(i) : SearchDailyRaw(seed, i, retry);
                    if (!found.Success || registry.Contains(found.Level) || registry.ContainsNearDuplicate(found.Level)) continue;
                    if (!BfsSolver.VerifySolution(found.Level, found.Analysis.Solution)) continue;
                    registry.TryAdd(found.Level);
                    var level = Finalize(found, DailySeed(seed, i, retry), "daily " + CampaignPlan.DailyTarget(i).Describe() + " retry=" + retry);
                    accepted = level.WithMetadata(level.Metadata, name: "Daily " + (i + 1));
                }
                if (accepted == null) throw new InvalidOperationException("Daily puzzle " + (i + 1) + " could not be generated.");
                pool.Add(accepted);
            }
            return pool;
        }

        public static QualitySearchResult SearchDailyRaw(ulong seed, int index, int retry)
            => QualitySearch.Search(DailySeed(seed, index, retry), CampaignPlan.DailyTarget(index));

        private static ulong DailySeed(ulong seed, int index, int retry)
            => SplitMix64.Derive(seed ^ 0xDA11_0000_0000_0000UL, (ulong)index * 64UL + (ulong)retry);

        /// <summary>Production level from a search result: canonical id, verified solution, metrics, Perfect target.</summary>
        private static LevelDefinition Finalize(QualitySearchResult found, ulong seed, string parameters)
        {
            var level = found.Level;
            string id = LevelCanonicalizer.CanonicalId(level);
            var difficulty = DifficultyEstimator.Estimate(level, found.Analysis);
            difficulty.Score = found.Insight.Challenge;
            difficulty.Category = CampaignPlan.Categorize(found.Insight.Challenge);
            AddInsightMetrics(difficulty, found.Insight);
            var meta = new LevelMetadata
            {
                GeneratorVersion = GeneratorInfo.Version,
                Seed = seed,
                GeneratorParams = "quality-search v" + QualitySearch.Version + " plan v" + CampaignPlan.Version + " " + parameters,
                SolverVersion = SolverInfo.Version,
                Solution = DirectionUtil.ToMoveString(found.Analysis.Solution),
                Difficulty = difficulty
            };
            return level.WithMetadata(meta, id, id).WithOptimalMoves(found.Analysis.StartDistance);
        }

        public static void AddInsightMetrics(DifficultyInfo difficulty, PuzzleInsight insight)
        {
            var m = difficulty.Metrics;
            m["challenge"] = insight.Challenge;
            m["greedySolves"] = insight.GreedySolves ? 1 : 0;
            m["greedyLength"] = insight.GreedyLength;
            m["randomSolveProbability"] = Math.Round(insight.RandomSolveProbability, 5);
            m["counterIntuitiveSteps"] = insight.CounterIntuitiveSteps;
            m["retreatSteps"] = insight.RetreatSteps;
            m["decisionPoints"] = insight.DecisionPoints;
            m["temptingTraps"] = insight.TemptingTraps;
            m["earlySettleTraps"] = insight.EarlySettleTraps;
            m["sleeperBlocks"] = insight.SleeperBlocks;
            m["strategicBlocks"] = insight.SleeperBlocks + insight.WallBlocks;
            m["repetitionRatio"] = Math.Round(insight.RepetitionRatio, 3);
            m["unusedSpaceRatio"] = Math.Round(insight.UnusedSpaceRatio, 3);
            m["patternCount"] = insight.PatternCount;
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
                var insight = PuzzleInsight.Analyze(level, StateSpaceAnalyzer.AnalyzeWithGraph(level));
                var difficulty = DifficultyEstimator.Estimate(level, analysis);
                difficulty.Score = insight.Challenge;
                difficulty.Category = CampaignPlan.Categorize(insight.Challenge);
                AddInsightMetrics(difficulty, insight);
                result.Level = level.WithMetadata(new LevelMetadata
                {
                    SolverVersion = SolverInfo.Version,
                    Solution = DirectionUtil.ToMoveString(analysis.Solution),
                    Difficulty = difficulty
                }).WithOptimalMoves(analysis.StartDistance);
                result.Success = true;
            }
            return result;
        }
    }
}
