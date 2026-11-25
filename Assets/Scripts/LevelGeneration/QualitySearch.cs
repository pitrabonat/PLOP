using System;
using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>Phase 10 mechanic kinds a search may place (and may be required to use).</summary>
    [Flags]
    public enum MechanicSet
    {
        None = 0,
        Frost = 1,
        Lava = 2,
        Breeze = 4,
        Loops = 8,
        All = Frost | Lava | Breeze | Loops
    }

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

        // ---- Phase 10 mechanics (all off by default: classic targets search exactly as before) ----
        /// <summary>Kinds the search may place.</summary>
        public MechanicSet Mechanics;
        /// <summary>Kinds the optimal route must really use (Frost: a slide, Breeze: a push, Loops: a teleport,
        /// Lava: a fatal alternative the player must avoid).</summary>
        public MechanicSet RequiredUse;
        public int MaxTiles = 6;
        public int MaxLava = 3;
        public int MaxFans = 2;
        public int MaxLoopPairs = 1;
        /// <summary>Optimal steps whose outcome depends on forced movement.</summary>
        public int MinMechanicSteps;

        /// <summary>A shallow copy (Phase 11 derives hardened targets from Phase 10 slots).</summary>
        public QualityTarget Clone() => (QualityTarget)MemberwiseClone();

        public string Describe() => Width + "x" + Height + " p" + Plops + " opt " + MinOptimal + "-" + MaxOptimal +
            " challenge " + MinChallenge + "-" + (double.IsInfinity(MaxChallenge) ? "∞" : MaxChallenge.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
            (RequireGreedyFail ? " greedy-fails" : "") + " counter≥" + MinCounterIntuitive + " traps≥" + MinTraps +
            (RequireSettlementOrder ? " order" : "") + (RequireCoordination ? " coordination" : "") + " blocks≥" + MinStrategicBlocks +
            " patterns≥" + MinPatterns +
            (Mechanics != MechanicSet.None ? " mech " + Mechanics + " use " + RequiredUse + " steps≥" + MinMechanicSteps : "");
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
        /// <summary>2: mechanic tiles. Classic targets consume the same random numbers as version 1.</summary>
        public const int Version = 2;

        private sealed class Board
        {
            public int W, H;
            public bool[] Wall;
            public int[] PlopCell;
            public int[] NestCell;
            public List<MechanicTile> Tiles = new List<MechanicTile>();

            public Board Clone() => new Board
            {
                W = W, H = H, Wall = (bool[])Wall.Clone(), PlopCell = (int[])PlopCell.Clone(), NestCell = (int[])NestCell.Clone(),
                Tiles = new List<MechanicTile>(Tiles)
            };

            public bool Occupied(int cell)
            {
                foreach (int c in PlopCell) if (c == cell) return true;
                foreach (int c in NestCell) if (c == cell) return true;
                return TileIndex(cell) >= 0;
            }

            public int TileIndex(int cell)
            {
                for (int i = 0; i < Tiles.Count; i++) if (Tiles[i].Position.Y * W + Tiles[i].Position.X == cell) return i;
                return -1;
            }

            public int Count(TileKind kind)
            {
                int n = 0;
                foreach (var t in Tiles) if (t.Kind == kind) n++;
                return n;
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
                return new LevelDefinition(id, id, W, H, walls, plops, nests, tiles: Tiles);
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
            if (t.Mechanics != MechanicSet.None)
            {
                foreach (MechanicSet kind in new[] { MechanicSet.Frost, MechanicSet.Breeze, MechanicSet.Loops, MechanicSet.Lava })
                {
                    if ((t.Mechanics & kind) == 0) continue;
                    int copies = (t.RequiredUse & kind) != 0 ? 1 + (kind == MechanicSet.Frost ? rng.NextInt(2) : 0) : rng.NextInt(2);
                    for (int i = 0; i < copies; i++) AddTile(rng, b, t, kind);
                }
            }
            return b;
        }

        private static int RandomFreeCell(SplitMix64 rng, Board b)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int c = rng.NextInt(b.W * b.H);
                if (!b.Wall[c] && !b.Occupied(c)) return c;
            }
            return -1;
        }

        private static bool AddTile(SplitMix64 rng, Board b, QualityTarget t, MechanicSet kind)
        {
            if (b.Tiles.Count >= t.MaxTiles) return false;
            int cell = RandomFreeCell(rng, b);
            if (cell < 0) return false;
            int x = cell % b.W, y = cell / b.W;
            switch (kind)
            {
                case MechanicSet.Frost:
                    b.Tiles.Add(MechanicTile.Frost(x, y));
                    return true;
                case MechanicSet.Lava:
                    if (b.Count(TileKind.Lava) >= t.MaxLava) return false;
                    b.Tiles.Add(MechanicTile.Lava(x, y));
                    return true;
                case MechanicSet.Breeze:
                    if (b.Count(TileKind.Fan) >= t.MaxFans) return false;
                    b.Tiles.Add(MechanicTile.Fan(x, y, (Direction)rng.NextInt(4), 1 + rng.NextInt(MechanicTile.MaxFanPower)));
                    return true;
                default:
                    if (b.Count(TileKind.Portal) / 2 >= t.MaxLoopPairs || b.Tiles.Count + 2 > t.MaxTiles) return false;
                    b.Tiles.Add(MechanicTile.Portal(x, y, NextPair(b)));
                    int other = RandomFreeCell(rng, b);
                    if (other < 0) { b.Tiles.RemoveAt(b.Tiles.Count - 1); return false; }
                    b.Tiles.Add(MechanicTile.Portal(other % b.W, other / b.W, b.Tiles[b.Tiles.Count - 1].Pair));
                    return true;
            }
        }

        private static int NextPair(Board b)
        {
            int pair = 0;
            foreach (var tile in b.Tiles) if (tile.Kind == TileKind.Portal) pair = Math.Max(pair, tile.Pair + 1);
            return pair;
        }

        private static MechanicSet SetOf(TileKind kind)
            => kind == TileKind.Frost ? MechanicSet.Frost : kind == TileKind.Lava ? MechanicSet.Lava
             : kind == TileKind.Fan ? MechanicSet.Breeze : MechanicSet.Loops;

        /// <summary>Add, remove, move or re-aim a mechanic tile. Loops always move and vanish as pairs.</summary>
        private static bool MutateTiles(SplitMix64 rng, Board b, QualityTarget t)
        {
            int op = rng.NextInt(4);
            if (op == 0 || b.Tiles.Count == 0)
            {
                var kinds = new List<MechanicSet>();
                foreach (MechanicSet k in new[] { MechanicSet.Frost, MechanicSet.Lava, MechanicSet.Breeze, MechanicSet.Loops })
                    if ((t.Mechanics & k) != 0) kinds.Add(k);
                return kinds.Count > 0 && AddTile(rng, b, t, kinds[rng.NextInt(kinds.Count)]);
            }
            int i = rng.NextInt(b.Tiles.Count);
            var tile = b.Tiles[i];
            if (op == 1)
            {
                var kind = SetOf(tile.Kind);
                if ((t.RequiredUse & kind) != 0 && b.Count(tile.Kind) <= (tile.Kind == TileKind.Portal ? 2 : 1)) return false;
                if (tile.Kind == TileKind.Portal)
                    b.Tiles.RemoveAll(x => x.Kind == TileKind.Portal && x.Pair == tile.Pair);
                else b.Tiles.RemoveAt(i);
                return true;
            }
            if (op == 2)
            {
                int cell = RandomFreeCell(rng, b);
                if (cell < 0) return false;
                b.Tiles[i] = new MechanicTile(tile.Kind, new GridPosition(cell % b.W, cell / b.W), tile.Direction, tile.Power, tile.Pair);
                return true;
            }
            if (tile.Kind != TileKind.Fan) return false;
            b.Tiles[i] = rng.NextInt(2) == 0
                ? MechanicTile.Fan(tile.Position.X, tile.Position.Y, (Direction)rng.NextInt(4), tile.Power)
                : MechanicTile.Fan(tile.Position.X, tile.Position.Y, tile.Direction, 1 + rng.NextInt(MechanicTile.MaxFanPower));
            return true;
        }

        private static bool Mutate(SplitMix64 rng, Board b, QualityTarget t)
        {
            int cells = b.W * b.H;
            int kind = rng.NextInt(t.Mechanics != MechanicSet.None ? 14 : 10);
            if (kind >= 10) return MutateTiles(rng, b, t);
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
            if (t.Mechanics != MechanicSet.None) p += MechanicPenalty(board, t, r);
            if (p == 0 && registry != null && (registry.Contains(level) || registry.ContainsNearDuplicate(level))) p = 0.5;

            e.Penalty = p;
            e.Level = level;
            e.Analysis = analysis;
            e.Insight = r;
            return e;
        }

        /// <summary>Mechanics must matter: required kinds used on the route, every tile relevant, no pointless fans.</summary>
        private static double MechanicPenalty(Board b, QualityTarget t, PuzzleInsight r)
        {
            double p = 0;
            if ((t.RequiredUse & MechanicSet.Frost) != 0 && r.Slides == 0) p += 5;
            if ((t.RequiredUse & MechanicSet.Breeze) != 0 && r.Pushes == 0) p += 5;
            if ((t.RequiredUse & MechanicSet.Loops) != 0 && r.Teleports == 0) p += 5;
            if ((t.RequiredUse & MechanicSet.Lava) != 0 && r.HazardAlternatives == 0) p += 5;
            p += 2 * Math.Max(0, t.MinMechanicSteps - r.MechanicSteps);
            var route = new HashSet<int>(r.RouteCells);
            foreach (var tile in b.Tiles)
            {
                int cell = tile.Position.Y * b.W + tile.Position.X;
                if (tile.Kind == TileKind.Lava)
                {
                    // Lava must sit next to the route: a hazard nobody ever risks is decoration.
                    bool near = route.Contains(cell - 1) || route.Contains(cell + 1) || route.Contains(cell - b.W) || route.Contains(cell + b.W);
                    if (!near) p += 1;
                    continue;
                }
                if (!route.Contains(cell)) p += 1; // unused Frost, fan or Loop end
                if (tile.Kind == TileKind.Fan)
                {
                    var next = tile.Position.Offset(tile.Direction);
                    if (next.X < 0 || next.Y < 0 || next.X >= b.W || next.Y >= b.H || b.Wall[next.Y * b.W + next.X]) p += 2; // blows into a wall
                }
            }
            return p;
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
