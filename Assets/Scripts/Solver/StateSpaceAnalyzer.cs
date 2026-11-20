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

    public static class StateSpaceAnalyzer
    {
        public static StateSpaceAnalysis Analyze(LevelDefinition level, int maxStates = 2_000_000)
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
            var scratch = new GameState(start.PlopCount);
            var order = new int[start.PlopCount];

            for (int head = 0; head < keys.Count; head++)
            {
                ulong key = keys[head];
                for (int d = 0; d < 4; d++)
                {
                    codec.Decode(key, scratch);
                    if (!MoveRules.Apply(level, scratch, (Direction)d, null, order)) { edges.Add(-1); continue; }
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
            }

            int n = keys.Count;
            a.Complete = true;
            a.ReachableStates = n;

            // goal states + reverse adjacency (CSR)
            var dist = new int[n];
            var isGoal = new bool[n];
            var queue = new int[n];
            int qTail = 0;
            for (int i = 0; i < n; i++)
            {
                dist[i] = -1;
                codec.Decode(keys[i], scratch);
                if (scratch.AllAsleep) { isGoal[i] = true; dist[i] = 0; queue[qTail++] = i; a.GoalStates++; }
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
                    if (dist[p] >= 0) continue;
                    dist[p] = dist[s] + 1;
                    queue[qTail++] = p;
                }
            }

            for (int i = 0; i < n; i++) if (dist[i] < 0) a.DeadEndStates++;
            a.StartDistance = dist[0];

            if (dist[0] >= 0)
            {
                var path = new List<Direction>();
                int cur = 0, branchingTotal = 0, steps = 0;
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
                    cur = edges[cur * 4 + chosen];
                }
                stats.AverageBranching = steps == 0 ? 0 : (double)branchingTotal / steps;
                a.Path = stats;
                a.Solution = path.ToArray();
            }
            a.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return a;
        }
    }
}
