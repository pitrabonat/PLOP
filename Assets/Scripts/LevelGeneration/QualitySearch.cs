using System;
using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>What a campaign slot needs. All thresholds are design targets, not proofs of human difficulty.</summary>
    public sealed class QualityTarget
    {
        public int Width = 5, Height = 5, Plops = 2;
        public int MinOptimal = 5, MaxOptimal = 12;
        public double MinChallenge = 0, MaxChallenge = double.PositiveInfinity;
        /// <summary>The naive "always closer" player must fail to finish.</summary>
        public bool RequireGreedyFail;
        public int MinCounterIntuitive;
        /// <summary>Tempting traps + early-settle traps along the optimal route.</summary>
        public int MinTraps;
        public int MinDecisionPoints;
        public bool RequireSettlementOrder;
        public bool RequireCoordination;
        /// <summary>Optimal route blocks a Plop with a sleeping Plop or wall while another moves at least this often.</summary>
        public int MinStrategicBlocks;
        public int MinPatterns;
        public double MaxRandomSolve = 1.0;
        public double MaxRepetition = 0.5;
        public double MaxUnusedSpace = 0.5;
        public int MaxWalls = 99;
        public int Restarts = 24;
        public int StepsPerRestart = 160;
        public int SolverMaxStates = 250_000;

        public string Describe() => Width + "x" + Height + " p" + Plops + " opt " + MinOptimal + "-" + MaxOptimal +
            " challenge " + MinChallenge + "-" + (double.IsInfinity(MaxChallenge) ? "∞" : MaxChallenge.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
            (RequireGreedyFail ? " greedy-fails" : "") + " counter≥" + MinCounterIntuitive + " traps≥" + MinTraps +
            (RequireSettlementOrder ? " order" : "") + (RequireCoordination ? " coordination" : "") + " blocks≥" + MinStrategicBlocks +
            " patterns≥" + MinPatterns;
    }

    public sealed class QualitySearchResult
    {
        public bool Success;
        public LevelDefinition Level;
        public StateSpaceAnalysis Analysis;
        public PuzzleInsight Insight;
        public int Evaluations;
        public double BestPenalty;
        public string Message;
    }

    /// <summary>
    /// Deterministic quality-driven level search: seeded random compact boards improved by hill climbing
    /// (toggle a wall, move a Plop, move a Nest, swap Plops) toward a <see cref="QualityTarget"/>. Every
    /// candidate is analysed exhaustively; unverifiable or unsolvable boards are never accepted.
    /// Same seed + target + registry contents ⇒ same level.
    /// </summary>
    public static class QualitySearch
    {
        public const int Version = 1;

        private sealed class Board
        {
            public int W, H;
            public bool[] Wall;
            public int[] PlopCell;
            public int[] NestCell;

            public Board Clone() => new Board
            {
                W = W, H = H, Wall = (bool[])Wall.Clone(), PlopCell = (int[])PlopCell.Clone(), NestCell = (int[])NestCell.Clone()
            };

            public bool Occupied(int cell)
            {
                foreach (int c in PlopCell) if (c == cell) return true;
                foreach (int c in NestCell) if (c == cell) return true;
                return false;
            }

            public LevelDefinition ToLevel(string id)
            {
                var walls = new List<GridPosition>();
                for (int i = 0; i < Wall.Length; i++) if (Wall[i]) walls.Add(new GridPosition(i % W, i / W));
                var plops = new PlopDefinition[PlopCell.Length];
                var nests = new NestDefinition[NestCell.Length];
                for (int i = 0; i < PlopCell.Length; i++)
                {
                    plops[i] = new PlopDefinition(i, (PlopColor)i, new GridPosition(PlopCell[i] % W, PlopCell[i] / W));
                    nests[i] = new NestDefinition((PlopColor)i, new GridPosition(NestCell[i] % W, NestCell[i] / W));
                }
                return new LevelDefinition(id, id, W, H, walls, plops, nests);
            }
        }

        private sealed class Eval
        {
            public double Penalty = double.MaxValue;
            public LevelDefinition Level;
            public StateSpaceAnalysis Analysis;
            public PuzzleInsight Insight;
        }

        public static QualitySearchResult Search(ulong seed, QualityTarget target, DuplicateRegistry registry = null,
            ChallengeWeights weights = null)
        {
            var rng = new SplitMix64(seed);
            var result = new QualitySearchResult { BestPenalty = double.MaxValue };
            for (int restart = 0; restart < target.Restarts; restart++)
            {
                var board = RandomBoard(rng, target);
                var current = Evaluate(board, target, registry, weights);
                result.Evaluations++;
                for (int step = 0; step < target.StepsPerRestart && current.Penalty > 0; step++)
                {
                    var next = board.Clone();
                    if (!Mutate(rng, next, target)) continue;
                    var e = Evaluate(next, target, registry, weights);
                    result.Evaluations++;
                    if (e.Penalty <= current.Penalty)
                    {
                        board = next;
                        current = e;
                    }
                }
                if (current.Penalty < result.BestPenalty) result.BestPenalty = current.Penalty;
                if (current.Penalty <= 0)
                {
                    result.Success = true;
                    result.Level = current.Level;
                    result.Analysis = current.Analysis;
                    result.Insight = current.Insight;
                    return result;
                }
            }
            result.Message = "no board met the target after " + result.Evaluations + " evaluations (best penalty " +
                             result.BestPenalty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";
            return result;
        }

        private static Board RandomBoard(SplitMix64 rng, QualityTarget t)
        {
            int cells = t.Width * t.Height;
            var b = new Board { W = t.Width, H = t.Height, Wall = new bool[cells], PlopCell = new int[t.Plops], NestCell = new int[t.Plops] };
            double density = 0.10 + 0.18 * rng.NextDouble();
            for (int i = 0; i < cells; i++) b.Wall[i] = rng.NextDouble() < density;
            var free = new List<int>();
            for (int i = 0; i < cells; i++) if (!b.Wall[i]) free.Add(i);
            while (free.Count < 2 * t.Plops + 2)
            {
                int c = rng.NextInt(cells);
                if (b.Wall[c]) { b.Wall[c] = false; free.Add(c); }
            }
            for (int i = 0; i < 2 * t.Plops; i++)
            {
                int j = i + rng.NextInt(free.Count - i);
                int tmp = free[i]; free[i] = free[j]; free[j] = tmp;
            }
            for (int i = 0; i < t.Plops; i++) { b.PlopCell[i] = free[i]; b.NestCell[i] = free[t.Plops + i]; }
            return b;
        }

        private static bool Mutate(SplitMix64 rng, Board b, QualityTarget t)
        {
            int cells = b.W * b.H;
            int kind = rng.NextInt(10);
            if (kind < 5)
            {
                int c = rng.NextInt(cells);
                if (b.Occupied(c)) return false;
                b.Wall[c] = !b.Wall[c];
                return true;
            }
            int cell = rng.NextInt(cells);
            if (b.Wall[cell] || b.Occupied(cell)) return false;
            int which = rng.NextInt(t.Plops);
            if (kind < 7) b.PlopCell[which] = cell;
            else if (kind < 9) b.NestCell[which] = cell;
            else
            {
                int other = rng.NextInt(t.Plops);
                if (other == which) return false;
                int tmp = b.NestCell[which]; b.NestCell[which] = b.NestCell[other]; b.NestCell[other] = tmp;
            }
            return true;
        }

        /// <summary>0 = meets every requirement; larger = further away. Structural failures are huge.</summary>
        private static Eval Evaluate(Board board, QualityTarget t, DuplicateRegistry registry, ChallengeWeights weights)
        {
            var e = new Eval();
            int walls = 0;
            foreach (bool w in board.Wall) if (w) walls++;
            var level = board.ToLevel("candidate");
            if (!LevelValidator.Validate(level).IsValid || !FloorConnected(board)) { e.Penalty = 1e6; return e; }
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(level, t.SolverMaxStates);
            if (!analysis.Complete) { e.Penalty = 5e5; return e; }
            if (analysis.StartDistance < 0) { e.Penalty = 1e5 + analysis.DeadEndRatio; return e; }
            var r = PuzzleInsight.Analyze(level, analysis, weights);

            double p = 0;
            p += 4 * Gap(analysis.StartDistance, t.MinOptimal, t.MaxOptimal);
            p += Gap(r.Challenge, t.MinChallenge, t.MaxChallenge);
            if (t.RequireGreedyFail && r.GreedySolves) p += 6;
            p += 3 * Math.Max(0, t.MinCounterIntuitive - r.CounterIntuitiveSteps);
            p += 3 * Math.Max(0, t.MinTraps - (r.TemptingTraps + r.EarlySettleTraps));
            p += 1.5 * Math.Max(0, t.MinDecisionPoints - r.DecisionPoints);
            if (t.RequireSettlementOrder && !r.SettlementOrderMatters) p += 4;
            if (t.RequireCoordination && !r.RequiresCoordination) p += 4;
            p += 2 * Math.Max(0, t.MinStrategicBlocks - (r.SleeperBlocks + r.WallBlocks));
            p += 2 * Math.Max(0, t.MinPatterns - r.PatternCount);
            p += 20 * Math.Max(0, r.RandomSolveProbability - t.MaxRandomSolve);
            p += 10 * Math.Max(0, r.RepetitionRatio - t.MaxRepetition);
            p += 10 * Math.Max(0, r.UnusedSpaceRatio - t.MaxUnusedSpace);
            p += Math.Max(0, walls - t.MaxWalls);
            p += 2 * FullWallLines(board);   // a whole row/column of walls is wasted screen space
            if (p == 0 && registry != null && (registry.Contains(level) || registry.ContainsNearDuplicate(level))) p = 0.5;

            e.Penalty = p;
            e.Level = level;
            e.Analysis = analysis;
            e.Insight = r;
            return e;
        }

        private static int FullWallLines(Board b)
        {
            int lines = 0;
            for (int y = 0; y < b.H; y++)
            {
                bool full = true;
                for (int x = 0; x < b.W && full; x++) full = b.Wall[y * b.W + x];
                if (full) lines++;
            }
            for (int x = 0; x < b.W; x++)
            {
                bool full = true;
                for (int y = 0; y < b.H && full; y++) full = b.Wall[y * b.W + x];
                if (full) lines++;
            }
            return lines;
        }

        private static double Gap(double value, double min, double max)
            => value < min ? min - value : value > max ? value - max : 0;

        private static bool FloorConnected(Board b)
        {
            int cells = b.W * b.H, start = -1, floor = 0;
            for (int i = 0; i < cells; i++) if (!b.Wall[i]) { floor++; if (start < 0) start = i; }
            if (start < 0) return false;
            var seen = new bool[cells];
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            int count = 0;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                count++;
                int x = c % b.W, y = c / b.W;
                if (x > 0) Visit(c - 1);
                if (x < b.W - 1) Visit(c + 1);
                if (y > 0) Visit(c - b.W);
                if (y < b.H - 1) Visit(c + b.W);
            }
            return count == floor;

            void Visit(int n)
            {
                if (b.Wall[n] || seen[n]) return;
                seen[n] = true;
                stack.Push(n);
            }
        }
    }
}
