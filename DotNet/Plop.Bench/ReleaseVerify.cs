using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Bench
{
    /// <summary>
    /// Phase 12 release verification of shipped content: for every level of a pack, the production checks
    /// (validation, solvable, verified optimum, Move limit, Undo/Restart, canonical id), a serialization round trip,
    /// a deterministic replay of the verified solution (same Core events twice) and the hint path (from every state
    /// along the solution, a fresh solve is optimal and its first move keeps an optimal route).
    /// Usage: --release-verify &lt;pack.json&gt; [report.md]
    /// </summary>
    internal static class ReleaseVerify
    {
        public static int Run(string path, string report)
        {
            var sw = Stopwatch.StartNew();
            var text = File.ReadAllText(path);
            var levels = LevelSerializer.PackFromJson(text);
            var errors = new List<string>();
            var production = LevelVerifier.VerifyProductionPack(levels, expectedCount: levels.Count);
            errors.AddRange(production.Errors);

            int limited = 0, hintsChecked = 0, events = 0;
            var mechanics = new SortedDictionary<string, int>();
            foreach (var level in levels)
            {
                string p = "[" + level.Id + "] ";
                if (level.MoveLimit > 0) limited++;
                foreach (var kind in level.Tiles.Select(t => t.Kind.ToString()).Distinct())
                    mechanics[kind] = mechanics.TryGetValue(kind, out var n) ? n + 1 : 1;

                // Serialization: shipped (no dev data) and full round trips are stable.
                foreach (bool dev in new[] { false, true })
                {
                    var json = LevelSerializer.ToJson(level, dev, false);
                    var again = LevelSerializer.ToJson(LevelSerializer.FromJson(json, dev), dev, false);
                    if (json != again) errors.Add(p + "serialization round trip differs (devData " + dev + ")");
                }

                // Deterministic replay: two engines, same events, completed at the optimum.
                var moves = DirectionUtil.ParseMoveString(level.Metadata.Solution);
                string a = Replay(level, moves, out bool doneA), b = Replay(level, moves, out bool doneB);
                events += a.Length;
                if (a != b) errors.Add(p + "non-deterministic replay");
                if (!doneA || !doneB) errors.Add(p + "verified solution does not complete the level");
                if (moves.Length != level.OptimalMoves) errors.Add(p + "stored solution length " + moves.Length + " != optimum " + level.OptimalMoves);
                if (level.MoveLimit > 0 && moves.Length > level.MoveLimit) errors.Add(p + "solution exceeds the Move limit");

                // Hint path: the game's hint (BFS from the current state) stays optimal along the solution.
                var engine = new GameEngine();
                engine.LoadLevel(level);
                for (int i = 0; i < moves.Length; i++)
                {
                    int remaining = moves.Length - i;
                    var hint = BfsSolver.Solve(level, engine.GetState());
                    hintsChecked++;
                    if (hint.Status != SolverStatus.Solved || hint.SolutionLength != remaining)
                    {
                        errors.Add(p + "hint at move " + i + ": " + hint.Status + " length " + hint.SolutionLength + ", expected " + remaining);
                        break;
                    }
                    var probe = new GameEngine();
                    probe.LoadLevel(level);
                    foreach (var m in moves.Take(i)) probe.TryMove(m);
                    probe.TryMove(hint.Moves[0]);
                    var after = BfsSolver.Solve(level, probe.GetState());
                    if (remaining > 1 && (after.Status != SolverStatus.Solved || after.SolutionLength != remaining - 1))
                    {
                        errors.Add(p + "hint move at " + i + " leaves no optimal route");
                        break;
                    }
                    engine.TryMove(moves[i]);
                }
            }

            var ids = levels.Select(l => l.Id).ToList();
            if (ids.Distinct().Count() != ids.Count) errors.Add("duplicate level ids");

            var sb = new StringBuilder();
            sb.AppendLine("# Release verification: " + Path.GetFileName(path));
            sb.AppendLine();
            sb.AppendLine("| Check | Result |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Levels | " + levels.Count + " |");
            sb.AppendLine("| Limited Moves levels | " + limited + " |");
            sb.AppendLine("| Mechanic tiles (levels using each) | " + (mechanics.Count == 0 ? "none" : string.Join(", ", mechanics.Select(kv => kv.Key + " " + kv.Value))) + " |");
            sb.AppendLine("| Production checks (validation, solvable, optimum, Move limit, Undo/Restart, canonical id, duplicates) | " + (production.IsValid ? "PASS" : "FAIL (" + production.Errors.Count + ")") + " |");
            sb.AppendLine("| Serialization round trip, deterministic replay, hint path (" + hintsChecked + " hint solves) | " + (errors.Count == production.Errors.Count ? "PASS" : "FAIL") + " |");
            sb.AppendLine("| Errors | " + errors.Count + " |");
            sb.AppendLine("| Duration | " + sw.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s |");
            if (errors.Count > 0)
            {
                sb.AppendLine();
                foreach (var e in errors.Take(200)) sb.AppendLine("- " + e);
            }
            Console.Write(sb.ToString());
            if (!string.IsNullOrEmpty(report)) File.WriteAllText(report, sb.ToString());
            return errors.Count == 0 ? 0 : 1;
        }

        private static string Replay(LevelDefinition level, Direction[] moves, out bool completed)
        {
            var engine = new GameEngine();
            engine.LoadLevel(level);
            var log = new StringBuilder();
            engine.EventRaised += e => log.Append(e.ToString()).Append('|');
            foreach (var m in moves) engine.TryMove(m);
            completed = engine.IsCompleted && engine.MoveCount == moves.Length;
            return log.ToString();
        }
    }
}
