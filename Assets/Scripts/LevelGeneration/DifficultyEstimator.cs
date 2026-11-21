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
            CountInteractions(level, analysis.Solution, out int coordination, out int wallInteractions);
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
            info.Metrics["requiresCoordination"] = analysis.RequiresCoordination ? 1 : 0;
            info.Metrics["wallInteractionMoves"] = wallInteractions;
            info.Metrics["wallDetour"] = CountWallDetour(level);
            info.Metrics["solutionTurns"] = CountTurns(analysis.Solution);
            info.Metrics["shortestSolutionCount"] = analysis.ShortestSolutionCount;
            info.Metrics["firstSettlementChoices"] = analysis.FirstSettlementChoices;
            info.Metrics["requiredSettlementPairs"] = analysis.RequiredSettlementPairs;
            info.Metrics["deadEndSettlements"] = analysis.DeadEndSettlements;
            info.Metrics["requiresAwayMove"] = analysis.RequiresAwayMove ? 1 : 0;
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
            CountInteractions(level, solution, out int coordination, out _);
            return coordination;
        }

        private static void CountInteractions(LevelDefinition level, Direction[] solution,
            out int coordination, out int wallInteractions)
        {
            coordination = wallInteractions = 0;
            if (solution == null) return;
            var engine = new GameEngine();
            engine.LoadLevel(level);
            foreach (var d in solution)
            {
                var result = engine.TryMove(d);
                bool blocked = false, blockedByWall = false;
                foreach (var e in result.Events)
                {
                    if (e.Type != GameEventType.PlopBlocked) continue;
                    blocked = true;
                    blockedByWall |= e.BlockReason == BlockReason.Wall;
                }
                if (result.Changed && blocked) coordination++;
                if (result.Changed && blockedByWall) wallInteractions++;
            }
        }

        private static int CountTurns(Direction[] solution)
        {
            int turns = 0;
            for (int i = 1; i < solution.Length; i++)
                if (solution[i - 1] != solution[i]) turns++;
            return turns;
        }

        // Independent walking distances to the nearest matching Nest, ignoring other Plops and assignments.
        private static int CountWallDetour(LevelDefinition level)
        {
            if (level.Walls.Count == 0) return 0;
            var distances = new int[level.CellCount];
            var queue = new int[level.CellCount];
            int detour = 0;
            for (int color = 0; color < PlopColorUtil.Count; color++)
            {
                int tail = 0;
                Array.Fill(distances, -1);
                foreach (var nest in level.Nests)
                {
                    if ((int)nest.Color != color) continue;
                    int cell = level.CellIndex(nest.Position);
                    distances[cell] = 0;
                    queue[tail++] = cell;
                }
                for (int head = 0; head < tail; head++)
                {
                    int cell = queue[head];
                    var pos = level.PositionOf(cell);
                    Visit(pos.X - 1, pos.Y, distances[cell] + 1);
                    Visit(pos.X + 1, pos.Y, distances[cell] + 1);
                    Visit(pos.X, pos.Y - 1, distances[cell] + 1);
                    Visit(pos.X, pos.Y + 1, distances[cell] + 1);
                }
                foreach (var plop in level.Plops)
                {
                    if ((int)plop.Color != color) continue;
                    int direct = int.MaxValue;
                    foreach (var nest in level.Nests)
                    {
                        if (nest.Color != plop.Color) continue;
                        int manhattan = Math.Abs(nest.Position.X - plop.Start.X) + Math.Abs(nest.Position.Y - plop.Start.Y);
                        direct = Math.Min(direct, manhattan);
                    }
                    detour += distances[level.CellIndex(plop.Start)] - direct;
                }

                void Visit(int x, int y, int distance)
                {
                    if (!level.InBounds(x, y)) return;
                    int cell = level.CellIndex(x, y);
                    if (distances[cell] >= 0 || level.IsWallCell(cell)) return;
                    distances[cell] = distance;
                    queue[tail++] = cell;
                }
            }
            return detour;
        }
    }
}
