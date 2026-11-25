using System;
using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Automated player strategies used to compare levels offline (Phase 11).</summary>
    public enum PlayerProfile
    {
        /// <summary>Uniformly random state-changing swipes (seeded Monte Carlo). Can walk into Lava.</summary>
        Random,
        /// <summary>Always the unvisited swipe that minimises total walking distance to free matching Nests.</summary>
        Greedy,
        /// <summary>Greedy over every swipe sequence up to two swipes ahead.</summary>
        LookAhead2,
        /// <summary>Greedy over every swipe sequence up to three swipes ahead.</summary>
        LookAhead3,
        /// <summary>
        /// Local rules only: each awake Plop votes for the swipes that bring it closer on foot; the player
        /// ignores Frost, fans and Loops when predicting and only avoids Lava that is one step away.
        /// </summary>
        LimitedInformation,
        /// <summary>The authoritative BFS solver.</summary>
        Optimal
    }

    /// <summary>Outcome of one simulated player on one level.</summary>
    public sealed class PlayerRun
    {
        public PlayerProfile Profile;
        public bool Solved;
        /// <summary>The run ended in Lava (only possible for players that do not see forced movement coming).</summary>
        public bool Failed;
        /// <summary>Swipes used when solved; for Random, the mean length of the successful runs.</summary>
        public double Moves = -1;
        /// <summary>Random only: share of runs that finished / ended in Lava within the horizon.</summary>
        public double SuccessRate, FailRate;
        public int Runs = 1;

        public string Describe()
        {
            if (Profile == PlayerProfile.Random)
                return (SuccessRate * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "% of " + Runs;
            if (Solved) return "solves in " + Moves.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            return Failed ? "falls in Lava" : "stuck";
        }
    }

    /// <summary>
    /// Deterministic, bounded player models on the exact state graph. They are heuristics for ranking levels
    /// and choosing what humans should playtest — not a model of human psychology and not a proof of difficulty.
    /// Offline only: never call from gameplay code.
    /// </summary>
    public static class SimulatedPlayers
    {
        public const int RandomRuns = 400;
        public const ulong DefaultSeed = 0x504C4F50_31310000UL;
        /// <summary>Largest look-ahead depth tried when estimating planning depth.</summary>
        public const int MaxLookAhead = 4;

        public static int StepLimit(int optimal) => 4 * Math.Max(1, optimal) + 20;

        /// <summary>Heuristic value of every graph state (fail sink = very large).</summary>
        public static int[] Heuristics(LevelDefinition level, StateGraph g)
        {
            var codec = new StateCodec(level);
            var heuristic = new NestDistanceHeuristic(level);
            var state = new GameState(level.Plops.Count);
            var h = new int[g.Count];
            for (int s = 0; s < g.Count; s++)
            {
                if (g.IsFail(s)) { h[s] = int.MaxValue / 4; continue; }
                codec.Decode(g.Keys[s], state);
                h[s] = heuristic.Evaluate(state);
            }
            return h;
        }

        public static List<PlayerRun> RunAll(LevelDefinition level, StateSpaceAnalysis analysis, ulong seed = DefaultSeed)
        {
            var g = analysis.Graph ?? throw new ArgumentException("Needs AnalyzeWithGraph.");
            var h = Heuristics(level, g);
            int opt = analysis.StartDistance;
            return new List<PlayerRun>
            {
                RandomPlayer(g, opt, seed, RandomRuns),
                LookAhead(g, h, opt, 1, PlayerProfile.Greedy),
                LookAhead(g, h, opt, 2, PlayerProfile.LookAhead2),
                LookAhead(g, h, opt, 3, PlayerProfile.LookAhead3),
                LimitedInformation(level, g, opt),
                new PlayerRun { Profile = PlayerProfile.Optimal, Solved = opt >= 0, Moves = opt }
            };
        }

        /// <summary>Smallest look-ahead depth (1–<see cref="MaxLookAhead"/>) that solves; MaxLookAhead + 1 when none does.</summary>
        public static int PlanningDepth(StateGraph g, int[] h, int optimal)
        {
            for (int k = 1; k <= MaxLookAhead; k++)
                if (LookAhead(g, h, optimal, k, PlayerProfile.LookAhead3).Solved) return k;
            return MaxLookAhead + 1;
        }

        // ------------------------------------------------------------------ random

        public static PlayerRun RandomPlayer(StateGraph g, int optimal, ulong seed, int runs)
        {
            var rng = new SplitMix64(seed);
            int horizon = StepLimit(optimal), solved = 0, failed = 0;
            long solvedLength = 0;
            var options = new int[4];
            for (int run = 0; run < runs; run++)
            {
                int cur = 0;
                for (int step = 1; step <= horizon; step++)
                {
                    int n = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int t = g.Next(cur, d);
                        if (t >= 0 && t != cur) options[n++] = t;
                    }
                    if (n == 0) break;
                    cur = options[rng.NextInt(n)];
                    if (g.IsFail(cur)) { failed++; break; }
                    if (g.Distance[cur] == 0) { solved++; solvedLength += step; break; }
                }
            }
            return new PlayerRun
            {
                Profile = PlayerProfile.Random,
                Runs = runs,
                SuccessRate = (double)solved / runs,
                FailRate = (double)failed / runs,
                Solved = solved > 0,
                Moves = solved > 0 ? (double)solvedLength / solved : -1
            };
        }

        // ------------------------------------------------------------------ greedy / look-ahead

        /// <summary>
        /// Depth-k greedy: scores every first swipe by the best board it can reach within k swipes (a finished
        /// board beats everything, Lava is never chosen), never returns to a board it has already stood on.
        /// k = 1 is the classic greedy player.
        /// </summary>
        public static PlayerRun LookAhead(StateGraph g, int[] h, int optimal, int depth, PlayerProfile profile)
        {
            var visited = new HashSet<int> { 0 };
            int cur = 0, steps = 0, limit = StepLimit(optimal);
            while (g.Distance[cur] != 0 && steps < limit)
            {
                int best = -1;
                long bestScore = long.MaxValue;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur || g.IsFail(t) || visited.Contains(t)) continue;
                    long score = Best(g, h, t, depth - 1, 1);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
                if (best < 0) break;
                visited.Add(best);
                cur = best;
                steps++;
            }
            bool solved = g.Distance[cur] == 0;
            return new PlayerRun { Profile = profile, Solved = solved, Moves = solved ? steps : -1 };
        }

        private static long Best(StateGraph g, int[] h, int s, int remaining, int used)
        {
            if (g.Distance[s] == 0) return -1_000_000 + used; // finishing sooner is better
            long best = h[s];
            if (remaining <= 0) return best;
            for (int d = 0; d < 4; d++)
            {
                int t = g.Next(s, d);
                if (t < 0 || t == s || g.IsFail(t)) continue;
                best = Math.Min(best, Best(g, h, t, remaining - 1, used + 1));
            }
            return best;
        }

        // ------------------------------------------------------------------ limited information

        public static PlayerRun LimitedInformation(LevelDefinition level, StateGraph g, int optimal)
        {
            var codec = new StateCodec(level);
            var heuristic = new NestDistanceHeuristic(level);
            var state = new GameState(level.Plops.Count);
            var visited = new HashSet<int> { 0 };
            var votes = new int[4];
            int cur = 0, steps = 0, limit = StepLimit(optimal);
            bool failed = false;
            while (g.Distance[cur] != 0 && steps < limit)
            {
                codec.Decode(g.Keys[cur], state);
                Array.Clear(votes, 0, 4);
                var danger = new bool[4];
                for (int id = 0; id < state.PlopCount; id++)
                {
                    if (state.IsAsleep(id)) continue;
                    var at = level.PositionOf(state.CellOf(id));
                    int here = OwnDistance(level, heuristic, state, id, level.CellIndex(at));
                    for (int d = 0; d < 4; d++)
                    {
                        var next = at.Offset((Direction)d);
                        if (!level.InBounds(next) || level.IsWall(next)) continue;
                        int cell = level.CellIndex(next);
                        if (level.TileAt(cell) == TileKind.Lava) { danger[d] = true; continue; }
                        if (OwnDistance(level, heuristic, state, id, cell) < here) votes[d]++;
                    }
                }
                int pick = -1;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur || danger[d] || visited.Contains(t)) continue;
                    if (pick < 0 || votes[d] > votes[pick]) pick = d;
                }
                if (pick < 0) break; // stuck
                cur = g.Next(cur, pick);
                steps++;
                if (g.IsFail(cur)) { failed = true; break; }
                visited.Add(cur);
            }
            bool solved = !failed && g.Distance[cur] == 0;
            return new PlayerRun { Profile = PlayerProfile.LimitedInformation, Solved = solved, Failed = failed, Moves = solved ? steps : -1 };
        }

        private static int OwnDistance(LevelDefinition level, NestDistanceHeuristic heuristic, GameState state, int id, int cell)
        {
            int best = 999;
            var color = level.PlopColorOf(id);
            for (int n = 0; n < level.Nests.Count; n++)
            {
                if (level.Nests[n].Color != color) continue;
                int occupant = state.PlopAt(heuristic.NestCell(n));
                if (occupant >= 0 && occupant != id && state.IsAsleep(occupant)) continue;
                best = Math.Min(best, heuristic.DistanceToNest(n, cell));
            }
            return best;
        }
    }
}
