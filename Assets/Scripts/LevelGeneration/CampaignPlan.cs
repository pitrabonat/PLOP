using System;
using Plop.Core;

namespace Plop.Generation
{
    /// <summary>
    /// Phase 8 campaign design: quality targets for every slot of the 100-level campaign and the
    /// daily-puzzle pool. Levels 1–10 stay the authored tutorials. From 11 on, every five levels form a
    /// difficulty wave: three building puzzles, one peak, one breather. Thresholds are on the
    /// <see cref="PuzzleInsight.Challenge"/> scale (see Documentation/PUZZLE_QUALITY.md) and must be
    /// re-calibrated with playtest data.
    /// </summary>
    public static class CampaignPlan
    {
        public const int Version = 1;
        public const int TutorialCount = 10;
        public const int LevelCount = 100;

        /// <summary>Challenge-score boundaries: below [0] Tutorial, [1] Easy, [2] Normal, [3] Hard, else Expert.</summary>
        public static readonly double[] CategoryThresholds = { 8, 18, 30, 42 };

        public static DifficultyCategory Categorize(double challenge)
        {
            var t = CategoryThresholds;
            if (challenge < t[0]) return DifficultyCategory.Tutorial;
            if (challenge < t[1]) return DifficultyCategory.Easy;
            if (challenge < t[2]) return DifficultyCategory.Normal;
            if (challenge < t[3]) return DifficultyCategory.Hard;
            return DifficultyCategory.Expert;
        }

        /// <summary>Quality target for a zero-based campaign index ≥ <see cref="TutorialCount"/>.</summary>
        public static QualityTarget TargetFor(int index)
        {
            if (index < TutorialCount || index >= LevelCount) throw new ArgumentOutOfRangeException(nameof(index));
            int slot = (index - TutorialCount) % 5;          // 0,1,2 build · 3 peak · 4 breather
            bool peak = slot == 3, breather = slot == 4;
            int level = index + 1;                            // public 1-based number
            double ramp = slot * 1.5 + (peak ? 3 : 0) - (breather ? 4 : 0);

            QualityTarget t;
            if (level <= 15)
            {
                // Two-Plop coordination with simple obstacles.
                t = new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 4, MaxOptimal = 8, RequireCoordination = true,
                    MinChallenge = 8 + ramp, MaxChallenge = 20 + ramp, MaxRandomSolve = 0.25, MaxWalls = 8 };
            }
            else if (level <= 30)
            {
                // Planning several moves ahead.
                t = new QualityTarget { Width = 5, Height = level % 2 == 0 ? 6 : 5, Plops = 2, MinOptimal = 6, MaxOptimal = 10,
                    RequireCoordination = true, MinCounterIntuitive = 1, RequireGreedyFail = !breather,
                    MinTraps = level > 20 && !breather ? 1 : 0, MinPatterns = 3,
                    MinChallenge = 16 + ramp + (level - 16) * 0.3, MaxChallenge = 30 + ramp + (level - 16) * 0.3, MaxRandomSolve = 0.05, MaxWalls = 9 };
                if (peak && level > 25) t.Plops = 3;
            }
            else if (level <= 50)
            {
                // Three-Plop coordination and settlement order.
                t = new QualityTarget { Width = level % 2 == 0 ? 6 : 5, Height = 6, Plops = breather ? 2 : 3, MinOptimal = 8, MaxOptimal = 13,
                    RequireGreedyFail = true, RequireSettlementOrder = !breather, MinTraps = breather ? 0 : 1, MinCounterIntuitive = 1,
                    MinPatterns = 4, MinChallenge = 26 + ramp + (level - 31) * 0.3, MaxChallenge = 40 + ramp + (level - 31) * 0.3,
                    MaxRandomSolve = 0.02, MaxWalls = 11 };
            }
            else if (level <= 75)
            {
                // Complex interactions and strategic blocking.
                t = new QualityTarget { Width = 6, Height = 6, Plops = peak ? 4 : 3, MinOptimal = 9, MaxOptimal = 15,
                    RequireGreedyFail = true, MinTraps = breather ? 1 : 2, MinCounterIntuitive = breather ? 1 : 2, MinStrategicBlocks = 3,
                    MinPatterns = 5, MinChallenge = 34 + ramp + (level - 51) * 0.3, MaxChallenge = 48 + ramp + (level - 51) * 0.3,
                    MaxRandomSolve = 0.01, MaxWalls = 12, MaxUnusedSpace = 0.45, SolverMaxStates = 400_000 };
            }
            else
            {
                // Advanced multi-Plop planning.
                bool four = peak || level % 4 == 0;
                t = new QualityTarget { Width = 6, Height = four ? 7 : 6, Plops = breather ? 3 : four ? 4 : 3, MinOptimal = 11, MaxOptimal = 18,
                    RequireGreedyFail = true, MinTraps = breather ? 1 : 2, MinCounterIntuitive = breather ? 2 : 3, MinStrategicBlocks = 3,
                    MinPatterns = breather ? 5 : 6, MinChallenge = 42 + ramp + (level - 76) * 0.3, MaxChallenge = 60 + ramp + (level - 76) * 0.4,
                    MaxRandomSolve = 0.005, MaxWalls = 14, MaxUnusedSpace = 0.42, SolverMaxStates = 700_000 };
            }
            return t;
        }

        /// <summary>Daily puzzles: compact Normal-to-Hard boards with real decisions, varied by pool index.</summary>
        public static QualityTarget DailyTarget(int poolIndex)
        {
            int kind = poolIndex % 4;
            return new QualityTarget
            {
                Width = kind == 3 ? 6 : 5, Height = kind == 0 ? 5 : 6, Plops = kind == 0 ? 2 : 3,
                MinOptimal = 7, MaxOptimal = 13, RequireGreedyFail = true, MinTraps = 1, MinCounterIntuitive = 1,
                MinPatterns = 4, MinChallenge = 24, MaxChallenge = 44, MaxRandomSolve = 0.02, MaxWalls = 11
            };
        }

        public static ulong SlotSeed(ulong campaignSeed, int index, int retry)
            => SplitMix64.Derive(campaignSeed ^ ((ulong)Version << 56), (ulong)index * 64UL + (ulong)retry);
    }
}
