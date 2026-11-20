using System;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Tunable weights of the difficulty heuristic (score = weighted sum of metrics).</summary>
    public sealed class DifficultyWeights
    {
        public double SolutionLength = 1.0;
        public double ExtraPlop = 3.0;            // per Plop beyond the first
        public double Log2ReachableStates = 0.6;
        public double DeadEndRatio = 8.0;         // fraction of reachable states that are dead ends
        public double MisleadingPerStep = 2.0;    // misleading moves per solution step
        public double TrapsPerStep = 6.0;         // moves into dead ends per solution step
        public double CoordinationMove = 1.5;     // per step where an obstacle holds one Plop back while others move
        public double ForcedRatio = -3.0;         // share of steps with a single optimal move (makes it easier)
        public double WallDensity = 3.0;

        /// <summary>
        /// Category thresholds on the score: below [0] Tutorial, below [1] Easy, below [2] Normal, below [3] Hard, else Expert.
        /// Calibrated on generator v1 output (see Documentation/LEVEL_GENERATION.md): 1 Plop scores ~7–14,
        /// 2 Plops ~15–38, 3 Plops ~25–75.
        /// </summary>
        public double[] CategoryThresholds = { 9, 16, 26, 38 };

        public static DifficultyWeights Default => new DifficultyWeights();
    }

    /// <summary>
    /// Heuristic difficulty estimate. NOT a proven measure of human challenge — it combines several
    /// state-space metrics so that difficulty does not depend on solution length alone.
    /// Requires a complete <see cref="StateSpaceAnalysis"/> of a solvable level.
    /// </summary>
    public static class DifficultyEstimator
    {
        public static DifficultyInfo Estimate(LevelDefinition level, StateSpaceAnalysis analysis, DifficultyWeights weights = null)
        {
            if (analysis == null || !analysis.Solvable) throw new ArgumentException("Difficulty needs a complete analysis of a solvable level.");
            weights = weights ?? DifficultyWeights.Default;

            int len = analysis.StartDistance;
            int plops = level.Plops.Count;
            int area = level.CellCount;
            int walls = level.Walls.Count;
            int coordination = CountCoordinationMoves(level, analysis.Solution);
            double steps = Math.Max(1, len);
            double misleadingPerStep = analysis.Path.MisleadingMoves / steps;
            double trapsPerStep = analysis.Path.TrapMoves / steps;
            double forcedRatio = analysis.Path.ForcedSteps / steps;
            double wallDensity = area == 0 ? 0 : (double)walls / area;
            double log2States = Math.Log(Math.Max(1, analysis.ReachableStates), 2);

            double score =
                weights.SolutionLength * len +
                weights.ExtraPlop * (plops - 1) +
                weights.Log2ReachableStates * log2States +
                weights.DeadEndRatio * analysis.DeadEndRatio +
                weights.MisleadingPerStep * misleadingPerStep +
                weights.TrapsPerStep * trapsPerStep +
                weights.CoordinationMove * coordination +
                weights.ForcedRatio * forcedRatio +
                weights.WallDensity * wallDensity;
            score = Math.Max(0, score);

            var info = new DifficultyInfo { Score = Math.Round(score, 2), Category = Categorize(score, weights) };
            info.Metrics["solutionLength"] = len;
            info.Metrics["plops"] = plops;
            info.Metrics["reachableStates"] = analysis.ReachableStates;
            info.Metrics["deadEndStates"] = analysis.DeadEndStates;
            info.Metrics["deadEndRatio"] = Math.Round(analysis.DeadEndRatio, 4);
            info.Metrics["misleadingMoves"] = analysis.Path.MisleadingMoves;
            info.Metrics["trapMoves"] = analysis.Path.TrapMoves;
            info.Metrics["forcedSteps"] = analysis.Path.ForcedSteps;
            info.Metrics["avgBranching"] = Math.Round(analysis.Path.AverageBranching, 3);
            info.Metrics["coordinationMoves"] = coordination;
            info.Metrics["walls"] = walls;
            info.Metrics["area"] = area;
            return info;
        }

        public static DifficultyCategory Categorize(double score, DifficultyWeights weights = null)
        {
            var t = (weights ?? DifficultyWeights.Default).CategoryThresholds;
            if (score < t[0]) return DifficultyCategory.Tutorial;
            if (score < t[1]) return DifficultyCategory.Easy;
            if (score < t[2]) return DifficultyCategory.Normal;
            if (score < t[3]) return DifficultyCategory.Hard;
            return DifficultyCategory.Expert;
        }

        /// <summary>
        /// Replays the solution through <see cref="GameEngine"/> and counts steps where at least one awake
        /// Plop was blocked while another moved — i.e. the player used an obstacle to desynchronize Plops.
        /// </summary>
        public static int CountCoordinationMoves(LevelDefinition level, Direction[] solution)
        {
            if (solution == null) return 0;
            var engine = new GameEngine();
            engine.LoadLevel(level);
            int count = 0;
            foreach (var d in solution)
            {
                var result = engine.TryMove(d);
                bool moved = false, blocked = false;
                foreach (var e in result.Events)
                {
                    if (e.Type == GameEventType.PlopMoved) moved = true;
                    else if (e.Type == GameEventType.PlopBlocked) blocked = true;
                }
                if (moved && blocked) count++;
            }
            return count;
        }
    }
}
