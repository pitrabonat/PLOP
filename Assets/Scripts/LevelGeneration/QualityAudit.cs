using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    /// <summary>One audited campaign level (Phase 11).</summary>
    public sealed class QualityAuditRow
    {
        public int Index;
        public LevelDefinition Level;
        public string WorldId = "";
        public int Walls, Tiles;
        public string MechanicKinds = "";
        public int ReachableStates;
        public int Optimal = -1;
        /// <summary>The stored solution replays to completion and the stored optimum equals the BFS optimum.</summary>
        public bool Verified;
        public string Problem = "";
        public PuzzleInsight Insight;
        public PuzzleQualityReport Quality;
        public string Category = "";
        public int MostSimilarIndex = -1;
        public double MostSimilarScore;
        public List<string> Flags = new List<string>();
        public double ElapsedMs;

        public bool Analysed => Quality != null;
    }

    /// <summary>
    /// Phase 11 campaign audit: every production level is replayed, re-solved and evaluated with
    /// <see cref="PuzzleQuality"/>, then compared with its neighbours (similarity, difficulty for its position).
    /// Offline developer tooling; deterministic for a given pack and seed.
    /// </summary>
    public static class QualityAudit
    {
        public const double SimilarThreshold = 0.85;

        /// <summary>
        /// Phase 11 difficulty band: the Phase 8 challenge thresholds (8/18/30/42) predate the mechanics and put
        /// two thirds of the campaign in Expert. Bands now combine the challenge score with planning depth (the
        /// smallest look-ahead that solves). Heuristic, not a human measurement.
        /// </summary>
        public static DifficultyCategory Band(double challenge, int planningDepth)
        {
            if (planningDepth <= 1 && challenge < 16) return DifficultyCategory.Tutorial;
            if (planningDepth <= 2 || challenge < 30) return DifficultyCategory.Easy;
            if (challenge < 42) return DifficultyCategory.Normal;
            if (challenge < 55) return DifficultyCategory.Hard;
            return DifficultyCategory.Expert;
        }

        public static QualityAuditRow AnalyzeLevel(LevelDefinition level, int index, string worldId, int maxStates = 2_000_000)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var row = new QualityAuditRow { Index = index, Level = level, WorldId = worldId ?? "" };
            row.Walls = level.Walls.Count;
            row.Tiles = level.Tiles.Count;
            row.MechanicKinds = Kinds(level);
            var validation = LevelValidator.Validate(level);
            if (!validation.IsValid) { row.Problem = "invalid: " + validation; return row; }
            var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(level, maxStates);
            row.ReachableStates = analysis.ReachableStates;
            if (!analysis.Complete) { row.Problem = "unknown (search budget " + maxStates + " exceeded)"; return row; }
            if (!analysis.Solvable) { row.Problem = "unsolvable"; return row; }
            row.Optimal = analysis.StartDistance;
            var stored = level.Metadata?.Solution;
            Direction[] moves = null;
            try { moves = string.IsNullOrEmpty(stored) ? null : DirectionUtil.ParseMoveString(stored); }
            catch (FormatException) { moves = null; }
            bool replay = moves != null && BfsSolver.VerifySolution(level, moves) && moves.Length == analysis.StartDistance;
            row.Verified = replay && level.OptimalMoves == analysis.StartDistance &&
                           (level.MoveLimit == 0 || level.MoveLimit >= analysis.StartDistance);
            if (!row.Verified) row.Problem = "stored solution or optimum does not match the solver";
            row.Insight = PuzzleInsight.Analyze(level, analysis);
            row.Quality = PuzzleQuality.Evaluate(level, analysis, row.Insight);
            row.Category = Band(row.Insight.Challenge, row.Quality.PlanningDepth).ToString();
            row.Flags.AddRange(row.Quality.Flags);
            row.ElapsedMs = watch.Elapsed.TotalMilliseconds;
            return row;
        }

        /// <summary>Cross-level checks once every row is analysed: nearest neighbour and difficulty for the campaign position.</summary>
        public static void Finish(IList<QualityAuditRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < rows.Count; j++)
            {
                if (i == j) continue;
                double s = PuzzleQuality.Similarity(rows[i].Level, rows[j].Level);
                if (s > rows[i].MostSimilarScore) { rows[i].MostSimilarScore = s; rows[i].MostSimilarIndex = j; }
            }
            var medians = new Dictionary<string, double>();
            foreach (var world in WorldIds(rows))
            {
                var values = new List<double>();
                foreach (var r in rows) if (r.WorldId == world && r.Analysed && string.IsNullOrEmpty(r.Level.Instruction)) values.Add(r.Insight.Challenge);
                values.Sort();
                medians[world] = values.Count == 0 ? 0 : values[values.Count / 2];
            }
            foreach (var r in rows)
            {
                if (r.MostSimilarIndex >= 0 && r.MostSimilarScore >= SimilarThreshold)
                    r.Flags.Add("similar-to-" + (r.MostSimilarIndex + 1));
                if (!r.Analysed || !string.IsNullOrEmpty(r.Level.Instruction) || r.Index < 5) continue;
                if (r.Insight.Challenge < medians[r.WorldId] - 12) r.Flags.Add("easy-for-position");
            }
        }

        private static IEnumerable<string> WorldIds(IEnumerable<QualityAuditRow> rows)
        {
            var seen = new HashSet<string>();
            foreach (var r in rows) if (seen.Add(r.WorldId)) yield return r.WorldId;
        }

        public static string Kinds(LevelDefinition level)
        {
            bool f = false, l = false, b = false, p = false;
            foreach (var t in level.Tiles)
            {
                f |= t.Kind == TileKind.Frost; l |= t.Kind == TileKind.Lava;
                b |= t.Kind == TileKind.Fan; p |= t.Kind == TileKind.Portal;
            }
            var parts = new List<string>();
            if (f) parts.Add("Frost");
            if (l) parts.Add("Lava");
            if (b) parts.Add("Breeze");
            if (p) parts.Add("Loops");
            if (level.MoveLimit > 0) parts.Add("Limit" + level.MoveLimit);
            return parts.Count == 0 ? "classic" : string.Join("+", parts);
        }

        // ------------------------------------------------------------------ exports

        private static string F(double v, string format = "0.###") => v.ToString(format, CultureInfo.InvariantCulture);

        private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>Machine-readable export (one object per level).</summary>
        public static string ToJson(IReadOnlyList<QualityAuditRow> rows, string packId)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"pack\": \"").Append(Esc(packId)).Append("\",\n  \"qualityVersion\": ").Append(PuzzleQuality.Version)
              .Append(",\n  \"levels\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var q = r.Quality;
                var lv = r.Level;
                sb.Append("    {\"level\": ").Append(r.Index + 1).Append(", \"id\": \"").Append(Esc(lv.Id)).Append("\", \"world\": \"").Append(Esc(r.WorldId))
                  .Append("\", \"width\": ").Append(lv.Width).Append(", \"height\": ").Append(lv.Height)
                  .Append(", \"plops\": ").Append(lv.Plops.Count).Append(", \"nests\": ").Append(lv.Nests.Count)
                  .Append(", \"walls\": ").Append(r.Walls).Append(", \"tiles\": ").Append(r.Tiles)
                  .Append(", \"mechanics\": \"").Append(Esc(r.MechanicKinds)).Append("\", \"moveLimit\": ").Append(lv.MoveLimit)
                  .Append(", \"verified\": ").Append(r.Verified ? "true" : "false")
                  .Append(", \"problem\": \"").Append(Esc(r.Problem)).Append("\", \"states\": ").Append(r.ReachableStates)
                  .Append(", \"optimal\": ").Append(r.Optimal)
                  .Append(", \"solution\": \"").Append(Esc(lv.Metadata?.Solution)).Append("\"");
                if (r.Analysed)
                {
                    var ins = r.Insight;
                    sb.Append(", \"challenge\": ").Append(F(ins.Challenge)).Append(", \"category\": \"").Append(r.Category)
                      .Append("\", \"quality\": ").Append(F(q.Quality))
                      .Append(", \"decisions\": ").Append(q.MeaningfulDecisions).Append(", \"decisionDensity\": ").Append(F(q.DecisionDensity))
                      .Append(", \"coordination\": ").Append(F(q.Coordination)).Append(", \"soloUnsolvable\": ").Append(q.SoloUnsolvable)
                      .Append(", \"coordinationGap\": ").Append(q.CoordinationGap).Append(", \"plopInteractions\": ").Append(q.PlopInteractionSteps)
                      .Append(", \"settlementPairs\": ").Append(q.RequiredSettlementPairs).Append(", \"deadEndSettlements\": ").Append(q.DeadEndSettlements)
                      .Append(", \"earlySettleTraps\": ").Append(q.EarlySettleTraps)
                      .Append(", \"deadEndRatio\": ").Append(F(ins.DeadEndRatio)).Append(", \"failingMoves\": ").Append(ins.FailingMoves)
                      .Append(", \"forced\": {\"slides\": ").Append(ins.Slides).Append(", \"pushes\": ").Append(ins.Pushes)
                      .Append(", \"teleports\": ").Append(ins.Teleports).Append(", \"hazardAlternatives\": ").Append(ins.HazardAlternatives).Append("}")
                      .Append(", \"mechanicRoles\": \"").Append(q.MechanicSummary()).Append("\", \"idleTiles\": ").Append(q.IdleTiles)
                      .Append(", \"planningDepth\": ").Append(q.PlanningDepth).Append(", \"misleading\": ").Append(q.MisleadingChoices)
                      .Append(", \"traps\": ").Append(ins.TemptingTraps + ins.EarlySettleTraps)
                      .Append(", \"longestRun\": ").Append(q.LongestRun).Append(", \"repetition\": ").Append(F(q.RepetitionRatio))
                      .Append(", \"unusedSpace\": ").Append(F(q.UnusedSpaceRatio))
                      .Append(", \"shortestSolutions\": ").Append(q.ShortestSolutionCount).Append(", \"nearOptimalSolutions\": ").Append(F(q.NearOptimalSolutions, "0"))
                      .Append(", \"goodFirstMoves\": ").Append(q.GoodFirstMoves)
                      .Append(", \"players\": {");
                    for (int p = 0; p < q.Players.Count; p++)
                    {
                        var run = q.Players[p];
                        if (p > 0) sb.Append(", ");
                        sb.Append('"').Append(run.Profile).Append("\": ");
                        if (run.Profile == PlayerProfile.Random)
                            sb.Append("{\"success\": ").Append(F(run.SuccessRate)).Append(", \"lava\": ").Append(F(run.FailRate)).Append(", \"runs\": ").Append(run.Runs).Append('}');
                        else
                            sb.Append("{\"solved\": ").Append(run.Solved ? "true" : "false").Append(", \"moves\": ").Append(F(run.Moves, "0"))
                              .Append(run.Failed ? ", \"lava\": true" : "").Append('}');
                    }
                    sb.Append('}');
                }
                sb.Append(", \"similarTo\": ").Append(r.MostSimilarIndex + 1).Append(", \"similarity\": ").Append(F(r.MostSimilarScore))
                  .Append(", \"flags\": [");
                for (int k = 0; k < r.Flags.Count; k++) sb.Append(k > 0 ? ", " : "").Append('"').Append(Esc(r.Flags[k])).Append('"');
                sb.Append("]}").Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>Per-level Markdown table (the body of CAMPAIGN_AUDIT.md).</summary>
        public static string ToMarkdownTable(IReadOnlyList<QualityAuditRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("| # | World | Board | Plops | Walls | Tiles | Mechanics | Opt | States | Dec | Dens | Coord | Settle | Depth | Mislead | Rnd% | Greedy | LA2 | LA3 | LimInfo | Chal | Qual | Sim | Ok | Flags |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var r in rows)
            {
                var lv = r.Level;
                sb.Append("| ").Append(r.Index + 1).Append(" | ").Append(r.WorldId).Append(" | ").Append(lv.Width).Append('×').Append(lv.Height)
                  .Append(" | ").Append(lv.Plops.Count).Append(" | ").Append(r.Walls).Append(" | ").Append(r.Tiles).Append(" | ").Append(r.MechanicKinds)
                  .Append(" | ");
                if (!r.Analysed) { sb.Append(r.Optimal).Append(" | ").Append(r.ReachableStates).Append(" | ").Append(r.Problem).Append(" |||||||||||||||\n"); continue; }
                var q = r.Quality;
                string P(PlayerProfile p) { var x = q.Player(p); return x == null ? "–" : x.Solved ? F(x.Moves, "0") : x.Failed ? "lava" : "✗"; }
                sb.Append(r.Optimal).Append(" | ").Append(r.ReachableStates).Append(" | ").Append(q.MeaningfulDecisions).Append(" | ").Append(F(q.DecisionDensity, "0.00"))
                  .Append(" | ").Append(F(q.Coordination, "0.0")).Append(" | ").Append(q.SettlementMatters ? "yes" : "–")
                  .Append(" | ").Append(q.PlanningDepth > SimulatedPlayers.MaxLookAhead ? ">" + SimulatedPlayers.MaxLookAhead : q.PlanningDepth.ToString())
                  .Append(" | ").Append(q.MisleadingChoices)
                  .Append(" | ").Append(F(q.Player(PlayerProfile.Random).SuccessRate * 100, "0"))
                  .Append(" | ").Append(P(PlayerProfile.Greedy)).Append(" | ").Append(P(PlayerProfile.LookAhead2)).Append(" | ").Append(P(PlayerProfile.LookAhead3))
                  .Append(" | ").Append(P(PlayerProfile.LimitedInformation))
                  .Append(" | ").Append(F(r.Insight.Challenge, "0.0")).Append(" | ").Append(F(q.Quality, "0"))
                  .Append(" | ").Append(r.MostSimilarIndex + 1).Append(" (").Append(F(r.MostSimilarScore * 100, "0")).Append("%)")
                  .Append(" | ").Append(r.Verified ? "✓" : "✗").Append(" | ").Append(string.Join(", ", r.Flags)).Append(" |\n");
            }
            return sb.ToString();
        }
    }
}
