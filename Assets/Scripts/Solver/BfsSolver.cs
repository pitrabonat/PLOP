using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Plop.Core;

namespace Plop.Solver
{
    public enum SolverStatus
    {
        /// <summary>A shortest solution was found.</summary>
        Solved,
        /// <summary>Every reachable state was explored and none is solved.</summary>
        ProvenUnsolvable,
        /// <summary>The state/time budget ran out or the search was cancelled. Says NOTHING about solvability.</summary>
        SearchLimitReached,
        /// <summary>The level or start state failed structural validation.</summary>
        InvalidLevel
    }

    public sealed class SolverOptions
    {
        /// <summary>Maximum number of distinct states stored. Deterministic budget.</summary>
        public int MaxStates = 2_000_000;
        /// <summary>Optional wall-clock budget in milliseconds (0 = none). Non-deterministic; prefer MaxStates.</summary>
        public int MaxMilliseconds = 0;
        public CancellationToken Cancellation = CancellationToken.None;

        public static SolverOptions Default => new SolverOptions();
    }

    public sealed class SolverResult
    {
        public SolverStatus Status;
        /// <summary>Shortest move sequence when <see cref="Status"/> is Solved, otherwise null.</summary>
        public Direction[] Moves;
        public int StatesVisited;
        public int StatesExpanded;
        public double ElapsedMs;
        public string Message;

        public int SolutionLength => Moves?.Length ?? -1;
        public override string ToString() =>
            Status + (Moves != null ? " len=" + Moves.Length + " " + DirectionUtil.ToMoveString(Moves) : "") +
            " visited=" + StatesVisited + (Message != null ? " (" + Message + ")" : "");
    }

    public static class SolverInfo
    {
        /// <summary>Bump when solver output can change for the same input.</summary>
        public const int Version = 1;
    }

    /// <summary>
    /// Breadth-first search over canonical state keys (<see cref="StateCodec"/>). Transitions use
    /// <see cref="MoveRules.Apply"/> — the same code the game uses. Returns a shortest solution.
    /// Memory: three parallel arrays (key, parent, move) plus one hash map; no per-node objects.
    /// </summary>
    public static class BfsSolver
    {
        public static SolverResult Solve(LevelDefinition level, GameState start = null, SolverOptions options = null)
        {
            options = options ?? SolverOptions.Default;
            var sw = Stopwatch.StartNew();
            var result = new SolverResult();

            var validation = LevelValidator.Validate(level);
            if (validation.IsValid && start != null) validation = LevelValidator.ValidateState(level, start);
            if (!validation.IsValid)
            {
                result.Status = SolverStatus.InvalidLevel;
                result.Message = validation.ToString();
                return result;
            }

            var codec = new StateCodec(level);
            var current = start != null ? start.Clone() : level.CreateInitialState();
            if (current.AllAsleep)
            {
                result.Status = SolverStatus.Solved;
                result.Moves = Array.Empty<Direction>();
                result.StatesVisited = 1;
                return result;
            }

            if (options.Cancellation.IsCancellationRequested)
            {
                result.Status = SolverStatus.SearchLimitReached;
                result.Message = "cancelled";
                return result;
            }
            if (options.MaxStates < 1)
            {
                result.Status = SolverStatus.SearchLimitReached;
                result.Message = "state budget < 1";
                return result;
            }
            int capacity = Math.Min(options.MaxStates, 1 << 16);
            var keys = new ulong[capacity];
            var parents = new int[capacity];
            var moves = new byte[capacity];
            var index = new Dictionary<ulong, int>(capacity);

            int count = 0;
            keys[0] = codec.Encode(current);
            parents[0] = -1;
            index[keys[0]] = 0;
            count = 1;

            var scratch = new GameState(current.PlopCount);
            var order = new int[current.PlopCount];
            int head = 0;
            bool limit = false;

            while (head < count)
            {
                if ((head & 1023) == 0 && head > 0)
                {
                    if (options.Cancellation.IsCancellationRequested ||
                        (options.MaxMilliseconds > 0 && sw.ElapsedMilliseconds > options.MaxMilliseconds))
                    {
                        limit = true;
                        result.Message = options.Cancellation.IsCancellationRequested ? "cancelled" : "time budget exhausted";
                        break;
                    }
                }

                ulong key = keys[head];
                for (int d = 0; d < 4; d++)
                {
                    codec.Decode(key, scratch);
                    if (!MoveRules.Apply(level, scratch, (Direction)d, null, order)) continue; // no-op
                    ulong next = codec.Encode(scratch);
                    if (index.ContainsKey(next)) continue;

                    if (count >= options.MaxStates)
                    {
                        limit = true;
                        result.Message = "state budget exhausted (" + options.MaxStates + ")";
                        break;
                    }
                    if (count == keys.Length)
                    {
                        int newCap = Math.Min(options.MaxStates, keys.Length * 2);
                        Array.Resize(ref keys, newCap);
                        Array.Resize(ref parents, newCap);
                        Array.Resize(ref moves, newCap);
                    }
                    keys[count] = next;
                    parents[count] = head;
                    moves[count] = (byte)d;
                    index[next] = count;
                    count++;

                    if (scratch.AllAsleep)
                    {
                        result.Status = SolverStatus.Solved;
                        result.Moves = Reconstruct(parents, moves, count - 1);
                        result.StatesVisited = count;
                        result.StatesExpanded = head + 1;
                        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                        return result;
                    }
                }
                if (limit) break;
                head++;
            }

            result.Status = limit ? SolverStatus.SearchLimitReached : SolverStatus.ProvenUnsolvable;
            result.StatesVisited = count;
            result.StatesExpanded = head;
            result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static Direction[] Reconstruct(int[] parents, byte[] moves, int node)
        {
            var path = new List<Direction>();
            while (parents[node] >= 0)
            {
                path.Add((Direction)moves[node]);
                node = parents[node];
            }
            path.Reverse();
            return path.ToArray();
        }

        /// <summary>
        /// Replays a move sequence through a fresh <see cref="GameEngine"/>. True when every move changes
        /// the state and the level ends completed exactly at the last move.
        /// </summary>
        public static bool VerifySolution(LevelDefinition level, IReadOnlyList<Direction> moves, GameState start = null)
        {
            var engine = new GameEngine();
            if (start == null) engine.LoadLevel(level); else engine.LoadState(level, start);
            if (moves.Count == 0) return engine.IsCompleted;
            for (int i = 0; i < moves.Count; i++)
            {
                if (engine.IsCompleted) return false;
                if (engine.TryMove(moves[i]).Status != MoveStatus.Moved) return false;
            }
            return engine.IsCompleted;
        }
    }
}
