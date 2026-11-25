using System;
using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>How one mechanic kind contributes to a level (Phase 11 mechanic-dependency test).</summary>
    public enum MechanicRole
    {
        Absent,
        /// <summary>Removing every tile of this kind makes the level unsolvable or changes its optimum.</summary>
        Essential,
        /// <summary>Lava that offers a fatal alternative on the shortest route (a real risk), without changing the optimum.</summary>
        Hazard,
        /// <summary>Triggered on the shortest route, but the level plays the same without it.</summary>
        Incidental,
        /// <summary>Present but never triggered on the route and removal changes nothing.</summary>
        Decorative,
        /// <summary>The comparison exceeded its search budget.</summary>
        Unknown
    }

    /// <summary>Per-kind result of the mechanic-dependency test.</summary>
    public sealed class MechanicDependency
    {
        public TileKind Kind;
        public int Tiles;
        public int RouteEvents;
        /// <summary>Optimal length with every tile of this kind replaced by floor; −1 unsolvable, −2 budget exceeded.</summary>
        public int OptimalWithout = -2;
        public MechanicRole Role;
    }

    /// <summary>
    /// Phase 11 offline puzzle-quality evaluation. Every value is computed from the exact state graph and the
    /// simulated players; they are heuristics that rank levels and choose what humans should playtest, not
    /// measurements of human difficulty. See Documentation/Phase11/PUZZLE_BALANCE_REPORT.md for definitions.
    /// </summary>
    public sealed class PuzzleQualityReport
    {
        public int Optimal;
        public int ReachableStates;

        // Meaningful decision density
        public int MeaningfulDecisions;
        public double DecisionDensity;

        // Coordination dependency
        /// <summary>Per Plop: optimal moves when it is alone on the board with its own Nest; −1 unsolvable alone, −2 unknown.</summary>
        public int[] SoloOptimal = Array.Empty<int>();
        public int SoloUnsolvable;
        public int CoordinationGap;
        public int PlopInteractionSteps;
        public double Coordination;

        // Settlement dependency
        public int RequiredSettlementPairs;
        public int DeadEndSettlements;
        public int EarlySettleTraps;
        public bool SettlementMatters;

        // Mechanic dependency
        public List<MechanicDependency> Mechanics = new List<MechanicDependency>();
        public int DecorativeKinds;
        public int IdleTiles;

        // Planning, misleading choices, route efficiency, diversity
        public int PlanningDepth;
        public int MisleadingChoices;
        public int LongestRun;
        public double RepetitionRatio;
        public double UnusedSpaceRatio;
        public int ShortestSolutionCount;
        /// <summary>Distinct swipe sequences of length ≤ optimal + 2 that finish (capped at one million).</summary>
        public double NearOptimalSolutions;
        public int GoodFirstMoves;

        public List<PlayerRun> Players = new List<PlayerRun>();
        public PlayerRun Player(PlayerProfile p) => Players.Find(r => r.Profile == p);

        public double Quality;
        public List<string> Flags = new List<string>();

        public string MechanicSummary()
        {
            var parts = new List<string>();
            foreach (var m in Mechanics) if (m.Role != MechanicRole.Absent) parts.Add(m.Kind + ":" + m.Role);
            return string.Join(" ", parts);
        }
    }

    public static class PuzzleQuality
    {
        public const int Version = 1;
        public const int ComparisonBudget = 600_000;
        private const double SolutionCap = 1_000_000;

        public static PuzzleQualityReport Evaluate(LevelDefinition level, StateSpaceAnalysis analysis, PuzzleInsight insight = null,
            ulong seed = SimulatedPlayers.DefaultSeed, bool compareMechanics = true)
        {
            if (analysis == null || !analysis.Solvable || analysis.Graph == null)
                throw new ArgumentException("PuzzleQuality needs a complete, solvable analysis with its graph.");
            insight = insight ?? PuzzleInsight.Analyze(level, analysis);
            var g = analysis.Graph;
            var h = SimulatedPlayers.Heuristics(level, g);
            var r = new PuzzleQualityReport
            {
                Optimal = analysis.StartDistance,
                ReachableStates = analysis.ReachableStates,
                RequiredSettlementPairs = analysis.RequiredSettlementPairs,
                DeadEndSettlements = analysis.DeadEndSettlements,
                EarlySettleTraps = insight.EarlySettleTraps,
                RepetitionRatio = insight.RepetitionRatio,
                UnusedSpaceRatio = insight.UnusedSpaceRatio,
                ShortestSolutionCount = analysis.ShortestSolutionCount
            };
            r.SettlementMatters = r.RequiredSettlementPairs > 0 || r.EarlySettleTraps > 0;
            r.Players = SimulatedPlayers.RunAll(level, analysis, seed);
            r.PlanningDepth = SimulatedPlayers.PlanningDepth(g, h, r.Optimal);

            WalkRoute(level, analysis, g, h, r);
            Coordination(level, analysis, r);
            Diversity(g, r);
            if (compareMechanics) MechanicDependencies(level, analysis, insight, r);
            r.Quality = Score(level, r);
            r.Flags.AddRange(Flags(level, r));
            return r;
        }

        // ------------------------------------------------------------------ route walk

        private static void WalkRoute(LevelDefinition level, StateSpaceAnalysis analysis, StateGraph g, int[] h, PuzzleQualityReport r)
        {
            var codec = new StateCodec(level);
            var state = new GameState(level.Plops.Count);
            var engine = new GameEngine();
            engine.LoadLevel(level);
            int cur = 0, run = 0;
            bool previousCounted = false;
            Direction? previous = null;
            foreach (var move in analysis.Solution)
            {
                codec.Decode(g.Keys[cur], state);
                uint asleepBefore = state.AsleepMask;
                int bestOptimalH = int.MaxValue, bestH = int.MaxValue;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur || g.IsFail(t)) continue;
                    bestH = Math.Min(bestH, h[t]);
                    if (g.Distance[t] == g.Distance[cur] - 1) bestOptimalH = Math.Min(bestOptimalH, h[t]);
                }
                bool plausibleWrong = false, misleading = false;
                for (int d = 0; d < 4; d++)
                {
                    int t = g.Next(cur, d);
                    if (t < 0 || t == cur) continue;
                    if (g.IsFail(t)) { plausibleWrong = true; continue; } // a visible risk the player must read
                    bool costly = g.Distance[t] < 0 || g.Distance[t] >= g.Distance[cur] + 1;
                    if (!costly) continue;
                    codec.Decode(g.Keys[t], state);
                    bool settles = (state.AsleepMask & ~asleepBefore) != 0;
                    if (h[t] <= bestOptimalH || settles) plausibleWrong = true;
                    if (h[t] == bestH && bestH < bestOptimalH) misleading = true;
                }
                bool repeat = previous.HasValue && previous.Value == move;
                bool counted = plausibleWrong && !(repeat && previousCounted);
                if (counted) r.MeaningfulDecisions++;
                if (misleading) r.MisleadingChoices++;
                previousCounted = counted;
                run = repeat ? run + 1 : 1;
                r.LongestRun = Math.Max(r.LongestRun, run);
                previous = move;

                var result = engine.TryMove(move);
                foreach (var e in result.Events)
                    if (e.Type == GameEventType.PlopBlocked && e.BlockReason == BlockReason.Plop) { r.PlopInteractionSteps++; break; }
                cur = g.Next(cur, (int)move);
            }
            r.DecisionDensity = r.Optimal == 0 ? 0 : Math.Round((double)r.MeaningfulDecisions / r.Optimal, 3);
        }

        // ------------------------------------------------------------------ coordination

        private static void Coordination(LevelDefinition level, StateSpaceAnalysis analysis, PuzzleQualityReport r)
        {
            int n = level.Plops.Count;
            r.SoloOptimal = new int[n];
            if (n <= 1)
            {
                r.SoloOptimal[0] = r.Optimal;
                r.Coordination = 0;
                return;
            }
            // Each Plop alone with the Nest it settles in on the shortest route.
            var final = level.CreateInitialState();
            foreach (var move in analysis.Solution) MoveRules.Apply(level, final, move);
            int maxSolo = 0;
            foreach (var p in level.Plops)
            {
                var nestPos = level.PositionOf(final.CellOf(p.Id));
                var solo = new LevelDefinition("solo", "solo", level.Width, level.Height, level.Walls,
                    new[] { new PlopDefinition(0, p.Color, p.Start) }, new[] { new NestDefinition(p.Color, nestPos) },
                    tiles: level.Tiles);
                var a = StateSpaceAnalyzer.Analyze(solo, ComparisonBudget);
                int v = !a.Complete ? -2 : a.StartDistance;
                r.SoloOptimal[p.Id] = v;
                if (v == -1) r.SoloUnsolvable++;
                maxSolo = Math.Max(maxSolo, v);
            }
            r.CoordinationGap = r.SoloUnsolvable > 0 ? 0 : Math.Max(0, r.Optimal - maxSolo);
            double gap = Math.Min(1.0, r.CoordinationGap / Math.Max(2.0, 0.3 * r.Optimal));
            double interaction = Math.Min(1.0, r.PlopInteractionSteps / Math.Max(1.0, r.Optimal / 3.0));
            double needs = r.SoloUnsolvable > 0 ? 1.0 : 0.0;
            double required = analysis.RequiresCoordination ? 0.5 : 0.0;
            r.Coordination = Math.Round(Math.Max(Math.Max(needs, required), 0.5 * gap + 0.5 * interaction), 3);
        }

        // ------------------------------------------------------------------ diversity

        private static void Diversity(StateGraph g, PuzzleQualityReport r)
        {
            int horizon = r.Optimal + 2, n = g.Count;
            var ways = new double[n];
            var next = new double[n];
            ways[0] = 1;
            double finished = 0;
            for (int t = 0; t < horizon; t++)
            {
                Array.Clear(next, 0, n);
                for (int s = 0; s < n; s++)
                {
                    if (ways[s] == 0 || g.Distance[s] == 0) continue;
                    int left = horizon - t - 1;
                    for (int d = 0; d < 4; d++)
                    {
                        int u = g.Next(s, d);
                        if (u < 0 || u == s || g.IsFail(u) || g.Distance[u] < 0 || g.Distance[u] > left) continue;
                        next[u] += ways[s];
                    }
                }
                for (int s = 0; s < n; s++) if (g.Distance[s] == 0) { finished += next[s]; next[s] = 0; }
                var swap = ways; ways = next; next = swap;
                if (finished > SolutionCap) { finished = SolutionCap; break; }
            }
            r.NearOptimalSolutions = Math.Min(SolutionCap, finished);
            for (int d = 0; d < 4; d++)
            {
                int u = g.Next(0, d);
                if (u >= 0 && u != 0 && !g.IsFail(u) && g.Distance[u] >= 0 && g.Distance[u] + 1 <= r.Optimal + 2) r.GoodFirstMoves++;
            }
        }

        // ------------------------------------------------------------------ mechanic dependency

        private static readonly TileKind[] Kinds = { TileKind.Frost, TileKind.Lava, TileKind.Fan, TileKind.Portal };

        private static void MechanicDependencies(LevelDefinition level, StateSpaceAnalysis analysis, PuzzleInsight insight, PuzzleQualityReport r)
        {
            var route = new HashSet<int>(insight.RouteCells);
            foreach (var kind in Kinds)
            {
                var dep = new MechanicDependency { Kind = kind };
                var keep = new List<MechanicTile>();
                foreach (var t in level.Tiles)
                {
                    if (t.Kind != kind) { keep.Add(t); continue; }
                    dep.Tiles++;
                    if (kind == TileKind.Lava ? !NextToRoute(level, t.Position, route) : !route.Contains(level.CellIndex(t.Position))) r.IdleTiles++;
                }
                if (dep.Tiles == 0) { dep.Role = MechanicRole.Absent; r.Mechanics.Add(dep); continue; }
                dep.RouteEvents = kind == TileKind.Frost ? insight.Slides : kind == TileKind.Fan ? insight.Pushes
                    : kind == TileKind.Portal ? insight.Teleports : insight.HazardAlternatives;
                var without = StateSpaceAnalyzer.Analyze(level.WithTiles(keep), ComparisonBudget);
                dep.OptimalWithout = !without.Complete ? -2 : without.StartDistance;
                if (dep.OptimalWithout == -2) dep.Role = MechanicRole.Unknown;
                else if (dep.OptimalWithout != r.Optimal) dep.Role = MechanicRole.Essential;
                else if (kind == TileKind.Lava) dep.Role = dep.RouteEvents > 0 ? MechanicRole.Hazard : MechanicRole.Decorative;
                else dep.Role = dep.RouteEvents > 0 ? MechanicRole.Incidental : MechanicRole.Decorative;
                if (dep.Role == MechanicRole.Decorative) r.DecorativeKinds++;
                r.Mechanics.Add(dep);
            }
        }

        private static bool NextToRoute(LevelDefinition level, GridPosition p, HashSet<int> route)
        {
            foreach (var d in DirectionUtil.All)
            {
                var q = p.Offset(d);
                if (level.InBounds(q) && route.Contains(level.CellIndex(q))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ score and flags

        /// <summary>
        /// 0–100 composite. Rewards decisions, coordination, settlement order, planning depth, mechanics that matter
        /// and misleading-but-fair choices; penalises random solvability, decorative mechanics, repetition and empty floor.
        /// </summary>
        public static double Score(LevelDefinition level, PuzzleQualityReport r)
        {
            double random = r.Player(PlayerProfile.Random)?.SuccessRate ?? 0;
            int present = 0, contributing = 0;
            foreach (var m in r.Mechanics)
            {
                if (m.Role == MechanicRole.Absent) continue;
                present++;
                if (m.Role == MechanicRole.Essential || m.Role == MechanicRole.Hazard) contributing++;
                else if (m.Role == MechanicRole.Incidental || m.Role == MechanicRole.Unknown) contributing += 0; // neutral
            }
            double mechanic = present == 0 ? 0.6 : (double)contributing / present;
            double score =
                30 * Math.Min(1.0, r.DecisionDensity / 0.5) +
                12 * r.Coordination +
                8 * (r.SettlementMatters ? 1 : 0) +
                15 * Math.Min(1.0, (r.PlanningDepth - 1) / 3.0) +
                10 * mechanic +
                10 * Math.Min(1.0, r.MisleadingChoices / 2.0) +
                15 * (1 - 0.5 * Math.Min(1, r.RepetitionRatio / 0.5) - 0.5 * Math.Min(1, r.UnusedSpaceRatio / 0.6)) -
                20 * Math.Min(1.0, random / 0.5) -
                8 * r.DecorativeKinds;
            return Math.Round(Math.Max(0, Math.Min(100, score)), 1);
        }

        public static List<string> Flags(LevelDefinition level, PuzzleQualityReport r)
        {
            var f = new List<string>();
            var random = r.Player(PlayerProfile.Random);
            if (random != null && random.SuccessRate >= 0.25) f.Add("random-solvable");
            if (r.Player(PlayerProfile.Greedy)?.Solved == true) f.Add("greedy-solvable");
            else if (r.PlanningDepth <= 2) f.Add("shallow");
            if (r.DecisionDensity < 0.2) f.Add("few-decisions");
            if (r.RepetitionRatio > 0.4 || r.LongestRun >= 4) f.Add("repetitive");
            if (r.UnusedSpaceRatio > 0.5) f.Add("open");
            if (r.DecorativeKinds > 0) f.Add("decorative-mechanic");
            if (r.IdleTiles >= 3 || (level.Tiles.Count >= 4 && r.IdleTiles * 2 > level.Tiles.Count)) f.Add("idle-tiles");
            if (r.Optimal >= 10 && r.DecisionDensity < 0.25) f.Add("long-for-its-decisions");
            return f;
        }

        /// <summary>Layout similarity in [0, 1] over the eight symmetries, including mechanic tiles (kind, fan direction).</summary>
        public static double Similarity(LevelDefinition a, LevelDefinition b)
        {
            if (a.Width * a.Height != b.Width * b.Height) return 0;
            var ga = Grid(a);
            var gb = Grid(b);
            double best = 0;
            for (int t = 0; t < 8; t++)
            {
                bool swap = (t & 4) != 0;
                int w = swap ? b.Height : b.Width, hh = swap ? b.Width : b.Height;
                if (w != a.Width || hh != a.Height) continue;
                int same = 0, considered = 0;
                for (int y = 0; y < hh; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = x, sy = y;
                    if (swap) { sx = y; sy = x; }
                    if ((t & 1) != 0) sx = b.Width - 1 - sx;
                    if ((t & 2) != 0) sy = b.Height - 1 - sy;
                    int ca = ga[y * a.Width + x], cb = gb[sy * b.Width + sx];
                    if (ca == 0 && cb == 0) continue;
                    considered++;
                    if (ca == cb) same++;
                }
                if (considered > 0) best = Math.Max(best, (double)same / considered);
            }
            return Math.Round(best, 3);
        }

        private static int[] Grid(LevelDefinition level)
        {
            var g = new int[level.CellCount];
            var state = level.CreateInitialState();
            for (int cell = 0; cell < g.Length; cell++)
            {
                int plop = state.PlopAt(cell);
                if (level.IsWallCell(cell)) g[cell] = 1;
                else if (plop >= 0) g[cell] = 10 + (int)level.PlopColorOf(plop);
                else if (level.NestColorAt(cell) != LevelDefinition.NoNest) g[cell] = 30 + level.NestColorAt(cell);
                else if (level.TileAt(cell) != TileKind.Floor) g[cell] = 50 + (int)level.TileAt(cell) * 4 +
                    (level.TileAt(cell) == TileKind.Fan ? (int)level.TileDefinitionAt(cell).Direction : 0);
            }
            return g;
        }
    }
}
