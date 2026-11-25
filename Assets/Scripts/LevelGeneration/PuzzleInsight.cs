using System;
using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Configurable weights of the challenge score (see Documentation/PUZZLE_QUALITY.md).</summary>
    public sealed class ChallengeWeights
    {
        public double GreedyFails = 6.0;            // a naive "always get closer" player cannot finish
        public double GreedyDetourPerOptimal = 3.0; // per extra (greedyLength/optimal − 1), capped at 2
        public double RandomResistance = 2.5;       // × −log10(probability a random swiper finishes)
        public double CounterIntuitiveStep = 2.0;   // optimal step where every "closer" swipe is wrong
        public double RetreatStep = 1.5;            // optimal step that must move a Plop away from its Nest
        public double DecisionPoint = 1.0;          // step with a costly or fatal alternative
        public double TemptingTrap = 2.0;           // greedy-preferred swipe that is a dead end
        public double EarlySettleTrap = 2.5;        // swipe that settles a Plop and makes the level unsolvable
        public double SettlementOrder = 2.0;        // some Nest pair must be filled in a fixed order
        public double RequiredCoordination = 1.5;   // every solution holds one Plop back while another moves
        public double SleeperBlock = 1.0;           // optimal steps that use a sleeping Plop as an obstacle
        public double RepetitionPenalty = 4.0;      // × share of repeated consecutive swipes
        public double UnusedSpacePenalty = 3.0;     // × share of floor never touched by any Plop on the route
        // ---- Phase 10 mechanics (weights stay modest: mechanics must create thinking, not just motion) ----
        public double HazardAlternative = 1.2;      // per optimal step that has a Lava (fatal) alternative, capped at 4
        public double MechanicStep = 0.6;           // per optimal step whose result depends on Frost/Breeze/Loops, capped at 6
        public double MechanicVariety = 1.0;        // per distinct mechanic kind the solution actually uses, beyond the first

        public static ChallengeWeights Default => new ChallengeWeights();
    }

    /// <summary>
    /// Player-centred puzzle analysis on the exact state graph. These are design heuristics — not a proof
    /// of human difficulty. Real playtests must calibrate them.
    /// </summary>
    public sealed class PuzzleInsight
    {
        public int Optimal;
        public int ReachableStates;
        public double DeadEndRatio;

        /// <summary>Naive player: always takes the unvisited swipe that minimises total walking distance to free matching Nests.</summary>
        public bool GreedySolves;
        public int GreedyLength;
        /// <summary>Exact probability that uniformly random state-changing swipes finish within 2·optimal + 4 swipes.</summary>
        public double RandomSolveProbability;

        public int CounterIntuitiveSteps;
        public int RetreatSteps;
        public int DecisionPoints;
        public int TemptingTraps;
        public int EarlySettleTraps;
        public bool SettlementOrderMatters;
        public bool RequiresCoordination;
        public int SleeperBlocks;
        public int WallBlocks;
        public int SolutionRuns;
        public double RepetitionRatio;
        public double UnusedSpaceRatio;
        public int ShortestSolutionCount;
        public int SolvableAlternatives;   // non-optimal swipes along the route that keep the level solvable
        // ---- Phase 10 ----
        public int HazardAlternatives;     // optimal steps where another swipe would fail (Lava)
        public int MechanicSteps;          // optimal steps that slide, get pushed or teleport
        public int Slides, Pushes, Teleports;
        public int MechanicKindsUsed;      // distinct kinds (Frost, Breeze, Loop) on the solution route
        public int FailingMoves;           // fatal edges in the whole reachable graph

        public double Challenge;
        /// <summary>Which design patterns (A–G from the Phase 8 brief) this puzzle exhibits.</summary>
        public string Patterns = "";

        public int PatternCount
        {
            get { int n = 0; foreach (char c in Patterns) if (c != ' ') n++; return n; }
        }

        public static PuzzleInsight Analyze(LevelDefinition level, StateSpaceAnalysis analysis, ChallengeWeights weights = null)
        {
            if (analysis == null || !analysis.Solvable || analysis.Graph == null)
                throw new ArgumentException("PuzzleInsight needs a complete, solvable analysis with its graph (AnalyzeWithGraph).");
            weights = weights ?? ChallengeWeights.Default;
            var g = analysis.Graph;
            var codec = new StateCodec(level);
            var heuristic = new Heuristic(level);
            var state = new GameState(level.Plops.Count);

            int[] h = new int[g.Count];
            uint[] asleep = new uint[g.Count];
            for (int s = 0; s < g.Count; s++)
            {
                if (g.IsFail(s)) { h[s] = int.MaxValue / 4; continue; } // a player sees Lava and avoids it
                codec.Decode(g.Keys[s], state);
                h[s] = heuristic.Evaluate(state);
                asleep[s] = SettledNestMask(level, state);
            }

            var r = new PuzzleInsight
            {
                Optimal = analysis.StartDistance,
                ReachableStates = analysis.ReachableStates,
                DeadEndRatio = analysis.DeadEndRatio,
                SettlementOrderMatters = analysis.RequiredSettlementPairs > 0,
                RequiresCoordination = analysis.RequiresCoordination,
                ShortestSolutionCount = analysis.ShortestSolutionCount,
                FailingMoves = analysis.FailingMoves
            };

            RunGreedy(g, h, r);
            r.RandomSolveProbability = RandomSolve(g, 2 * r.Optimal + 4);
            WalkOptimalRoute(level, analysis, g, h, asleep, r);
            r.Challenge = Score(r, weights);
            r.Patterns = (r.RequiresCoordination ? "A" : " ") +
                         (r.SettlementOrderMatters || r.EarlySettleTraps > 0 ? "B" : " ") +
                         (r.RetreatSteps > 0 ? "C" : " ") +
                         (r.SleeperBlocks > 0 || r.WallBlocks > 1 ? "D" : " ") +
                         (r.CounterIntuitiveSteps > 0 && !r.GreedySolves ? "E" : " ") +
                         (r.TemptingTraps > 0 ? "F" : " ") +
                         (r.SolvableAlternatives > 0 && r.ShortestSolutionCount <= 8 ? "G" : " ");
            return r;
        }

        public static double Score(PuzzleInsight r, ChallengeWeights w)
        {
            double greedyGap = r.GreedySolves ? Math.Min(2.0, Math.Max(0, (double)r.GreedyLength / Math.Max(1, r.Optimal) - 1)) : 2.0;
            double score =
                (r.GreedySolves ? 0 : w.GreedyFails) +
                w.GreedyDetourPerOptimal * greedyGap +
                w.RandomResistance * -Math.Log10(Math.Max(1e-4, r.RandomSolveProbability)) +
                w.CounterIntuitiveStep * r.CounterIntuitiveSteps +
                w.RetreatStep * r.RetreatSteps +
                w.DecisionPoint * r.DecisionPoints +
                w.TemptingTrap * r.TemptingTraps +
                w.EarlySettleTrap * Math.Min(3, r.EarlySettleTraps) +
                (r.SettlementOrderMatters ? w.SettlementOrder : 0) +
                (r.RequiresCoordination ? w.RequiredCoordination : 0) +
                w.SleeperBlock * r.SleeperBlocks +
                w.HazardAlternative * Math.Min(4, r.HazardAlternatives) +
                w.MechanicStep * Math.Min(6, r.MechanicSteps) +
                w.MechanicVariety * Math.Max(0, r.MechanicKindsUsed - 1) -
                w.RepetitionPenalty * r.RepetitionRatio -
                w.UnusedSpacePenalty * r.UnusedSpaceRatio;
            return Math.Round(Math.Max(0, score), 2);
        }

        // ------------------------------------------------------------------ player models

        private static void RunGreedy(StateGraph g, int[] h, PuzzleInsight r)
        {
            var visited = new HashSet<int> { 0 };
            int cur = 0, steps = 0, limit = 4 * r.Optimal + 20;
            while (g.Distance[cur] != 0 && steps < limit)
            {
                int best = -1;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur || g.IsFail(t) || visited.Contains(t)) continue;
                    if (best < 0 || h[t] < h[best]) best = t;
                }
                if (best < 0) break; // stuck: every swipe revisits a known board
                visited.Add(best);
                cur = best;
                steps++;
            }
            r.GreedySolves = g.Distance[cur] == 0;
            r.GreedyLength = r.GreedySolves ? steps : -1;
        }

        /// <summary>Exact absorbing-chain probability of finishing within <paramref name="horizon"/> random swipes.</summary>
        private static double RandomSolve(StateGraph g, int horizon)
        {
            int n = g.Count;
            var p = new double[n];
            var next = new double[n];
            for (int s = 0; s < n; s++) p[s] = g.Distance[s] == 0 ? 1 : 0;
            for (int t = 0; t < horizon; t++)
            {
                for (int s = 0; s < n; s++)
                {
                    if (g.Distance[s] == 0) { next[s] = 1; continue; }
                    if (g.Distance[s] < 0) { next[s] = 0; continue; }
                    double sum = 0; int moves = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int target = g.Next(s, d);
                        if (target < 0 || target == s) continue;
                        sum += p[target];
                        moves++;
                    }
                    next[s] = moves == 0 ? 0 : sum / moves;
                }
                var swap = p; p = next; next = swap;
            }
            return p[0];
        }

        private static void WalkOptimalRoute(LevelDefinition level, StateSpaceAnalysis analysis, StateGraph g, int[] h,
            uint[] asleep, PuzzleInsight r)
        {
            var codec = new StateCodec(level);
            var touched = new HashSet<int>();
            var engine = new GameEngine();
            engine.LoadLevel(level);
            for (int id = 0; id < level.Plops.Count; id++) touched.Add(engine.CurrentState.CellOf(id));

            int cur = 0, kinds = 0;
            foreach (var move in analysis.Solution)
            {
                int bestH = int.MaxValue, optimalCount = 0;
                bool greedyChoiceOptimal = false, retreatNeeded = true, costlyAlternative = false, fatalAlternative = false;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t >= 0 && t != cur) bestH = Math.Min(bestH, h[t]);
                }
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur) continue;
                    bool optimal = g.Distance[t] == g.Distance[cur] - 1;
                    if (optimal)
                    {
                        optimalCount++;
                        if (h[t] == bestH) greedyChoiceOptimal = true;
                        if (h[t] <= h[cur]) retreatNeeded = false;
                        continue;
                    }
                    if (g.IsFail(t))
                    {
                        costlyAlternative = fatalAlternative = true;
                        continue;
                    }
                    if (g.Distance[t] < 0)
                    {
                        costlyAlternative = true;
                        if (h[t] == bestH) r.TemptingTraps++;
                        if ((asleep[t] & ~asleep[cur]) != 0) r.EarlySettleTraps++;
                    }
                    else
                    {
                        r.SolvableAlternatives++;
                        if (g.Distance[t] >= g.Distance[cur] + 1) costlyAlternative = true;
                    }
                }
                if (!greedyChoiceOptimal) r.CounterIntuitiveSteps++;
                if (retreatNeeded && optimalCount > 0) r.RetreatSteps++;
                if (costlyAlternative) r.DecisionPoints++;
                if (fatalAlternative) r.HazardAlternatives++;

                var result = engine.TryMove(move);
                bool moved = false, blockedBySleeper = false, blockedByWall = false, mechanicStep = false;
                foreach (var e in result.Events)
                {
                    if (e.Type == GameEventType.PlopMoved) { moved = true; touched.Add(level.CellIndex(e.To)); }
                    if (e.Type == GameEventType.PlopSlid || e.Type == GameEventType.PlopPushed || e.Type == GameEventType.PlopTeleported)
                    {
                        mechanicStep = true;
                        touched.Add(level.CellIndex(e.To));
                        if (e.Type == GameEventType.PlopSlid) { r.Slides++; kinds |= 1; }
                        if (e.Type == GameEventType.PlopPushed) { r.Pushes++; kinds |= 2; }
                        if (e.Type == GameEventType.PlopTeleported) { r.Teleports++; kinds |= 4; }
                    }
                    if (e.Type != GameEventType.PlopBlocked) continue;
                    if (e.BlockReason == BlockReason.Wall || e.BlockReason == BlockReason.Edge) blockedByWall = true;
                    if (e.BlockReason == BlockReason.Plop && e.BlockerId >= 0 && engine.CurrentState.IsAsleep(e.BlockerId)) blockedBySleeper = true;
                }
                if (moved && blockedBySleeper) r.SleeperBlocks++;
                if (moved && blockedByWall) r.WallBlocks++;
                if (mechanicStep) r.MechanicSteps++;
                cur = g.Next(cur, (int)move);
            }

            r.MechanicKindsUsed = (kinds & 1) + ((kinds >> 1) & 1) + ((kinds >> 2) & 1);
            var sol = analysis.Solution;
            int runs = sol.Length == 0 ? 0 : 1;
            for (int i = 1; i < sol.Length; i++) if (sol[i] != sol[i - 1]) runs++;
            r.SolutionRuns = runs;
            r.RepetitionRatio = sol.Length <= 1 ? 0 : (double)(sol.Length - runs) / (sol.Length - 1);
            int floor = level.CellCount - level.Walls.Count;
            r.UnusedSpaceRatio = floor <= 0 ? 0 : Math.Max(0, 1.0 - (double)touched.Count / floor);
        }

        private static uint SettledNestMask(LevelDefinition level, GameState state)
        {
            uint mask = 0;
            for (int id = 0; id < state.PlopCount; id++)
            {
                if (!state.IsAsleep(id)) continue;
                int cell = state.CellOf(id);
                for (int i = 0; i < level.Nests.Count; i++)
                    if (level.CellIndex(level.Nests[i].Position) == cell) mask |= 1u << i;
            }
            return mask;
        }

        /// <summary>Wall-aware walking distance of every awake Plop to its nearest unfilled matching Nest.</summary>
        private sealed class Heuristic
        {
            private readonly LevelDefinition _level;
            private readonly int[][] _distanceFromNest; // per nest index, per cell
            private readonly int[] _nestCells;

            public Heuristic(LevelDefinition level)
            {
                _level = level;
                _distanceFromNest = new int[level.Nests.Count][];
                _nestCells = new int[level.Nests.Count];
                var queue = new int[level.CellCount];
                for (int i = 0; i < level.Nests.Count; i++)
                {
                    var dist = new int[level.CellCount];
                    for (int c = 0; c < dist.Length; c++) dist[c] = 999;
                    int start = level.CellIndex(level.Nests[i].Position);
                    _nestCells[i] = start;
                    int head = 0, tail = 0;
                    dist[start] = 0;
                    queue[tail++] = start;
                    while (head < tail)
                    {
                        int c = queue[head++];
                        int x = c % level.Width, y = c / level.Width;
                        Try(x - 1, y); Try(x + 1, y); Try(x, y - 1); Try(x, y + 1);
                        void Try(int nx, int ny)
                        {
                            if (!level.InBounds(nx, ny)) return;
                            int n = level.CellIndex(nx, ny);
                            if (level.IsWallCell(n) || level.TileAt(n) == TileKind.Lava || dist[n] <= dist[c] + 1) return;
                            dist[n] = dist[c] + 1;
                            queue[tail++] = n;
                        }
                    }
                    _distanceFromNest[i] = dist;
                }
            }

            public int Evaluate(GameState state)
            {
                int total = 0;
                for (int id = 0; id < state.PlopCount; id++)
                {
                    if (state.IsAsleep(id)) continue;
                    int cell = state.CellOf(id), best = 999;
                    var color = _level.PlopColorOf(id);
                    for (int i = 0; i < _nestCells.Length; i++)
                    {
                        if (_level.Nests[i].Color != color) continue;
                        int occupant = state.PlopAt(_nestCells[i]);
                        if (occupant >= 0 && state.IsAsleep(occupant)) continue; // already filled
                        best = Math.Min(best, _distanceFromNest[i][cell]);
                    }
                    total += best;
                }
                return total;
            }
        }
    }
}
