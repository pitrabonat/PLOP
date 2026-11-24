using System;
using System.Collections.Generic;
using System.Diagnostics;
using Plop.Core;

namespace Plop.Solver
{
    /// <summary>
    /// Full reachable-state-space analysis of a level (used for difficulty metrics and verification).
    /// Explores every reachable state (bounded by a budget), then runs a reverse BFS from all
    /// solved states to get the exact distance-to-goal of every state.
    /// </summary>
    public sealed class StateSpaceAnalysis
    {
        /// <summary>True when every reachable state was explored (otherwise metrics are partial and unusable).</summary>
        public bool Complete;
        public bool Valid = true;
        public string Message;
        public int ReachableStates;
        public int GoalStates;
        /// <summary>Reachable states from which no solved state is reachable.</summary>
        public int DeadEndStates;
        /// <summary>Shortest solution length from the start, or -1 if unsolvable.</summary>
        public int StartDistance = -1;
        /// <summary>A shortest solution (deterministic: first optimal direction in U,D,L,R order).</summary>
        public Direction[] Solution;
        public double ElapsedMs;
        public PathStats Path;
        /// <summary>Number of distinct shortest command sequences, capped at one million.</summary>
        public int ShortestSolutionCount;
        /// <summary>Different first-settling Nest groups among all completing paths.</summary>
        public int FirstSettlementChoices;
        /// <summary>Number of Nest pairs whose settlement must occur in a strict order.</summary>
        public int RequiredSettlementPairs;
        /// <summary>Transitions from solvable states that settle a Plop and make completion impossible.</summary>
        public int DeadEndSettlements;
        /// <summary>Every completing path has an awake Plop move farther from its nearest matching Nest.</summary>
        public bool RequiresAwayMove;
        /// <summary>Every completing path has a swipe that blocks one awake Plop while another moves.</summary>
        public bool RequiresCoordination;

        /// <summary>The explored graph; only kept when requested (<see cref="StateSpaceAnalyzer.AnalyzeWithGraph"/>).</summary>
        public StateGraph Graph;

        public bool Solvable => Complete && StartDistance >= 0;
        public double DeadEndRatio => ReachableStates == 0 ? 0 : (double)DeadEndStates / ReachableStates;
    }

    /// <summary>Statistics of the choices available along the shortest solution.</summary>
    public struct PathStats
    {
        /// <summary>Sum over path states of state-changing moves that do NOT bring the player closer (excl. traps).</summary>
        public int MisleadingMoves;
        /// <summary>Sum over path states of moves that lead into a dead-end (unsolvable) state.</summary>
        public int TrapMoves;
        /// <summary>Path steps where exactly one move is optimal.</summary>
        public int ForcedSteps;
        /// <summary>Average number of state-changing moves per path state.</summary>
        public double AverageBranching;
    }

    /// <summary>
    /// Complete reachable state graph of a level: canonical keys, four successor slots per state
    /// (target index or -1 for a no-op, in U, D, L, R order) and exact distance to completion
    /// (-1 = proven dead end). State 0 is the start. Offline analysis only.
    /// </summary>
    public sealed class StateGraph
    {
        public ulong[] Keys;
        public int[] Edges;
        public int[] Distance;
        public int Count => Keys.Length;
        public int Next(int state, int direction) => Edges[state * 4 + direction];
    }

    public static class StateSpaceAnalyzer
    {
        private const int SolutionCountCap = 1_000_000;

        public static StateSpaceAnalysis Analyze(LevelDefinition level, int maxStates = 2_000_000)
            => Analyze(level, maxStates, null);

        /// <summary>Same as <see cref="Analyze(LevelDefinition,int)"/> but also returns the explored graph.</summary>
        public static StateSpaceAnalysis AnalyzeWithGraph(LevelDefinition level, int maxStates = 2_000_000)
            => Analyze(level, maxStates, null, keepGraph: true);

        // Invoked only after the entire reachable graph has been explored.
        internal static StateSpaceAnalysis Analyze(LevelDefinition level, int maxStates, Action<ulong, bool> classify,
            bool keepGraph = false)
        {
            var sw = Stopwatch.StartNew();
            var a = new StateSpaceAnalysis();
            var validation = LevelValidator.Validate(level);
            if (!validation.IsValid)
            {
                a.Valid = false;
                a.Message = validation.ToString();
                return a;
            }

            var codec = new StateCodec(level);
            var start = level.CreateInitialState();
            var keys = new List<ulong>(1024) { codec.Encode(start) };
            var index = new Dictionary<ulong, int>(1024) { [keys[0]] = 0 };
            var edges = new List<int>(4096); // 4 per state: target index or -1 for no-op
            var awayMoves = new List<byte>(1024);
            var coordinationMoves = new List<byte>(1024);
            var settledNests = new List<uint>(1024);
            var nestBits = NestBitsByCell(level);
            var nestDistances = NearestNestDistances(level);
            var state = new GameState(start.PlopCount);
            var scratch = new GameState(start.PlopCount);
            var order = new int[start.PlopCount];

            for (int head = 0; head < keys.Count; head++)
            {
                codec.Decode(keys[head], state);
                uint settled = 0;
                for (int id = 0; id < state.PlopCount; id++)
                    if (state.IsAsleep(id)) settled |= nestBits[state.CellOf(id)];
                settledNests.Add(settled);

                byte awayDirections = 0, coordinationDirections = 0;
                for (int d = 0; d < 4; d++)
                {
                    scratch.CopyFrom(state);
                    if (!MoveRules.Apply(level, scratch, (Direction)d, null, order)) { edges.Add(-1); continue; }

                    // Compare live ids before the codec reorders interchangeable same-color Plops.
                    for (int id = 0; id < state.PlopCount; id++)
                    {
                        if (state.IsAsleep(id)) continue;
                        int from = state.CellOf(id), to = scratch.CellOf(id);
                        if (from == to) coordinationDirections |= (byte)(1 << d);
                        var distances = nestDistances[(int)level.PlopColorOf(id)];
                        if (distances[to] > distances[from]) awayDirections |= (byte)(1 << d);
                    }

                    ulong next = codec.Encode(scratch);
                    if (!index.TryGetValue(next, out int target))
                    {
                        if (keys.Count >= maxStates)
                        {
                            a.Complete = false;
                            a.ReachableStates = keys.Count;
                            a.Message = "state budget exhausted (" + maxStates + ")";
                            a.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                            return a;
                        }
                        target = keys.Count;
                        keys.Add(next);
                        index[next] = target;
                    }
                    edges.Add(target);
                }
                awayMoves.Add(awayDirections);
                coordinationMoves.Add(coordinationDirections);
            }

            int n = keys.Count;
            a.Complete = true;
            a.ReachableStates = n;

            // goal states + reverse adjacency (CSR)
            var dist = new int[n];
            var shortestSolutions = new int[n];
            var queue = new int[n];
            uint allNests = GameState.AllMask(level.Nests.Count);
            int qTail = 0;
            for (int i = 0; i < n; i++)
            {
                dist[i] = -1;
                if (settledNests[i] != allNests) continue;
                dist[i] = 0;
                shortestSolutions[i] = 1;
                queue[qTail++] = i;
                a.GoalStates++;
            }

            var revCount = new int[n + 1];
            for (int i = 0; i < edges.Count; i++) if (edges[i] >= 0 && edges[i] != i / 4) revCount[edges[i] + 1]++;
            for (int i = 0; i < n; i++) revCount[i + 1] += revCount[i];
            var revAdj = new int[revCount[n]];
            var fill = new int[n];
            for (int i = 0; i < edges.Count; i++)
            {
                int t = edges[i];
                if (t < 0 || t == i / 4) continue;
                revAdj[revCount[t] + fill[t]++] = i / 4;
            }

            for (int qHead = 0; qHead < qTail; qHead++)
            {
                int s = queue[qHead];
                for (int k = revCount[s]; k < revCount[s + 1]; k++)
                {
                    int p = revAdj[k];
                    int distance = dist[s] + 1;
                    if (dist[p] < 0)
                    {
                        dist[p] = distance;
                        queue[qTail++] = p;
                    }
                    if (dist[p] == distance)
                        shortestSolutions[p] = Math.Min(SolutionCountCap, shortestSolutions[p] + shortestSolutions[s]);
                }
            }

            for (int i = 0; i < n; i++)
            {
                bool canFinish = dist[i] >= 0;
                if (!canFinish) a.DeadEndStates++;
                classify?.Invoke(keys[i], canFinish);
            }
            a.StartDistance = dist[0];
            if (keepGraph) a.Graph = new StateGraph { Keys = keys.ToArray(), Edges = edges.ToArray(), Distance = dist };

            if (dist[0] >= 0)
            {
                var path = new List<Direction>();
                int cur = 0, branchingTotal = 0, steps = 0;
                bool pathHasAwayMove = false, pathHasCoordination = false;
                var stats = new PathStats();
                while (dist[cur] > 0)
                {
                    int chosen = -1, optimalCount = 0, changing = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int t = edges[cur * 4 + d];
                        if (t < 0) continue;
                        changing++;
                        if (dist[t] < 0) stats.TrapMoves++;
                        else if (dist[t] == dist[cur] - 1) { optimalCount++; if (chosen < 0) chosen = d; }
                        else stats.MisleadingMoves++;
                    }
                    if (optimalCount == 1) stats.ForcedSteps++;
                    branchingTotal += changing;
                    steps++;
                    path.Add((Direction)chosen);
                    pathHasAwayMove |= (awayMoves[cur] & (1 << chosen)) != 0;
                    pathHasCoordination |= (coordinationMoves[cur] & (1 << chosen)) != 0;
                    cur = edges[cur * 4 + chosen];
                }
                stats.AverageBranching = steps == 0 ? 0 : (double)branchingTotal / steps;
                a.Path = stats;
                a.Solution = path.ToArray();
                a.ShortestSolutionCount = shortestSolutions[0];
                AnalyzeSettlements(a, edges, dist, settledNests, queue, qTail, level.Nests.Count);
                a.RequiresAwayMove = pathHasAwayMove && !HasPathWithoutMoves(edges, dist, awayMoves);
                a.RequiresCoordination = pathHasCoordination && !HasPathWithoutMoves(edges, dist, coordinationMoves);
            }
            a.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return a;
        }

        private static uint[] NestBitsByCell(LevelDefinition level)
        {
            var bits = new uint[level.CellCount];
            for (int i = 0; i < level.Nests.Count; i++)
                bits[level.CellIndex(level.Nests[i].Position)] = 1u << i;
            return bits;
        }

        private static int[][] NearestNestDistances(LevelDefinition level)
        {
            var distances = new int[PlopColorUtil.Count][];
            foreach (var nest in level.Nests)
            {
                int color = (int)nest.Color;
                var byCell = distances[color];
                if (byCell == null)
                {
                    byCell = distances[color] = new int[level.CellCount];
                    Array.Fill(byCell, int.MaxValue);
                }
                for (int cell = 0; cell < level.CellCount; cell++)
                {
                    var pos = level.PositionOf(cell);
                    int distance = Math.Abs(pos.X - nest.Position.X) + Math.Abs(pos.Y - nest.Position.Y);
                    byCell[cell] = Math.Min(byCell[cell], distance);
                }
            }
            return distances;
        }

        private static bool HasPathWithoutMoves(List<int> edges, int[] dist, List<byte> excludedMoves)
        {
            var seen = new bool[dist.Length];
            var pending = new Queue<int>();
            seen[0] = true;
            pending.Enqueue(0);
            while (pending.Count > 0)
            {
                int source = pending.Dequeue();
                if (dist[source] == 0) return true;
                for (int d = 0; d < 4; d++)
                {
                    int target = edges[source * 4 + d];
                    if (target < 0 || dist[target] < 0 || seen[target] ||
                        (excludedMoves[source] & (1 << d)) != 0) continue;
                    seen[target] = true;
                    pending.Enqueue(target);
                }
            }
            return false;
        }

        private static void AnalyzeSettlements(StateSpaceAnalysis analysis, List<int> edges, int[] dist,
            List<uint> settledNests, int[] solvable, int solvableCount, int nestCount)
        {
            var firstGroups = new HashSet<uint>();
            var maySettleWithout = new uint[nestCount];
            for (int i = 0; i < solvableCount; i++)
            {
                int source = solvable[i];
                uint before = settledNests[source];
                for (int d = 0; d < 4; d++)
                {
                    int target = edges[source * 4 + d];
                    if (target < 0) continue;
                    uint added = settledNests[target] & ~before;
                    if (added == 0) continue;
                    if (dist[target] < 0)
                    {
                        analysis.DeadEndSettlements++;
                        continue;
                    }
                    if (before == 0) firstGroups.Add(added);
                    for (int nest = 0; nest < nestCount; nest++)
                        if ((added & (1u << nest)) != 0) maySettleWithout[nest] |= ~before;
                }
            }
            analysis.FirstSettlementChoices = firstGroups.Count;
            for (int nest = 0; nest < nestCount; nest++)
                for (int earlier = 0; earlier < nestCount; earlier++)
                    if (earlier != nest && (maySettleWithout[nest] & (1u << earlier)) == 0)
                        analysis.RequiredSettlementPairs++;
        }
    }
}
