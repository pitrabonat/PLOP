using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Plop.Core;
using Plop.Solver;

namespace Plop.Tests
{
    public class SolverTests
    {
        [Test]
        public void Solves_StraightLine_Shortest()
        {
            var level = LevelText.Parse("s", "R..r");
            var r = BfsSolver.Solve(level);
            Assert.AreEqual(SolverStatus.Solved, r.Status);
            Assert.AreEqual("RRR", DirectionUtil.ToMoveString(r.Moves));
        }

        [Test]
        public void Solves_TwoPlops_NeedingWallToDesync()
        {
            // Both Plops move together; R must be held back by the wall to line up with its nest.
            var level = LevelText.Parse("w",
                "R#..",
                "...r",
                "B..b");
            var r = BfsSolver.Solve(level);
            Assert.AreEqual(SolverStatus.Solved, r.Status, r.ToString());
            Assert.IsTrue(BfsSolver.VerifySolution(level, r.Moves));
            Assert.AreEqual(IddfsShortest(level, 12), r.SolutionLength);
        }

        [Test]
        public void ProvenUnsolvable_WhenNestUnreachable()
        {
            var level = LevelText.Parse("u", "R#r");
            var r = BfsSolver.Solve(level);
            Assert.AreEqual(SolverStatus.ProvenUnsolvable, r.Status);
            Assert.IsNull(r.Moves);
        }

        [Test]
        public void ProvenUnsolvable_WhenPlopsCanNeverDesync()
        {
            // Two Plops in an open corridor always move together; their nests require different offsets.
            var level = LevelText.Parse("u2",
                "R...r.",
                "B..b..");
            var r = BfsSolver.Solve(level);
            var a = StateSpaceAnalyzer.Analyze(level);
            Assert.AreEqual(a.Solvable ? SolverStatus.Solved : SolverStatus.ProvenUnsolvable, r.Status);
            Assert.AreEqual(a.StartDistance, r.Status == SolverStatus.Solved ? r.SolutionLength : -1);
        }

        [Test]
        public void BudgetExhaustion_IsNotReportedAsUnsolvable()
        {
            var unsolvable = LevelText.Parse("u", "R......#r");
            var limited = BfsSolver.Solve(unsolvable, null, new SolverOptions { MaxStates = 3 });
            Assert.AreEqual(SolverStatus.SearchLimitReached, limited.Status);
            var full = BfsSolver.Solve(unsolvable);
            Assert.AreEqual(SolverStatus.ProvenUnsolvable, full.Status);

            var solvable = LevelText.Parse("s", "R......r");
            Assert.AreEqual(SolverStatus.SearchLimitReached, BfsSolver.Solve(solvable, null, new SolverOptions { MaxStates = 2 }).Status);
        }

        [Test]
        public void Cancellation_ReportsSearchLimit()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var r = BfsSolver.Solve(LevelText.Parse("c", "R..r"), null, new SolverOptions { Cancellation = cts.Token });
            Assert.AreEqual(SolverStatus.SearchLimitReached, r.Status);
        }

        [Test]
        public void InvalidLevel_Reported()
        {
            var r = BfsSolver.Solve(LevelText.Parse("i", "R.B", "r.."));
            Assert.AreEqual(SolverStatus.InvalidLevel, r.Status);
            Assert.IsNotNull(r.Message);
        }

        [Test]
        public void SolvesFromArbitraryState_ForHints()
        {
            var level = LevelText.Parse("h", "R...r", "B#..b");
            var engine = new GameEngine();
            engine.LoadLevel(level);
            int full = BfsSolver.Solve(level).SolutionLength;
            engine.TryMove(Direction.Right);
            var fromHere = BfsSolver.Solve(level, engine.CurrentState);
            Assert.AreEqual(SolverStatus.Solved, fromHere.Status);
            Assert.IsTrue(BfsSolver.VerifySolution(level, fromHere.Moves, engine.CurrentState));
            Assert.GreaterOrEqual(fromHere.SolutionLength, full - 1);
        }

        [Test]
        public void AlreadySolvedState_ReturnsEmptySolution()
        {
            var level = LevelText.Parse("z", "R.r");
            var e = new GameEngine();
            e.LoadLevel(level);
            e.TryMove(Direction.Right);
            e.TryMove(Direction.Right);
            var r = BfsSolver.Solve(level, e.CurrentState);
            Assert.AreEqual(SolverStatus.Solved, r.Status);
            Assert.AreEqual(0, r.SolutionLength);
        }

        [Test]
        public void Analyzer_MatchesSolver_AndCountsDeadEnds()
        {
            var level = LevelText.Parse("a",
                "R#..",
                "...r",
                "B..b");
            var a = StateSpaceAnalyzer.Analyze(level);
            var r = BfsSolver.Solve(level);
            Assert.IsTrue(a.Complete);
            Assert.AreEqual(r.SolutionLength, a.StartDistance);
            Assert.AreEqual(a.StartDistance, a.Solution.Length);
            Assert.IsTrue(BfsSolver.VerifySolution(level, a.Solution));
            Assert.Greater(a.ReachableStates, 1);
            Assert.GreaterOrEqual(a.GoalStates, 1);
            Assert.LessOrEqual(a.DeadEndStates, a.ReachableStates - a.GoalStates);
        }

        [Test]
        public void Analyzer_BudgetExhaustion_IsIncomplete()
        {
            var a = StateSpaceAnalyzer.Analyze(LevelText.Parse("a", "R......r"), maxStates: 3);
            Assert.IsFalse(a.Complete);
            Assert.IsFalse(a.Solvable);
        }

        /// <summary>
        /// Cross-checks BFS against an independent iterative-deepening search that only uses the public
        /// GameEngine API, on many random small levels.
        /// </summary>
        [Test]
        public void Randomized_BfsMatchesIndependentIddfs([Range(0, 4)] int batch)
        {
            var rng = new Random(500 + batch);
            int checkedLevels = 0;
            for (int trial = 0; trial < 60; trial++)
            {
                var level = RandomSmallLevel(rng);
                if (!LevelValidator.Validate(level).IsValid) continue;
                var r = BfsSolver.Solve(level);
                int iddfs = IddfsShortest(level, 9);
                if (r.Status == SolverStatus.Solved)
                {
                    Assert.IsTrue(BfsSolver.VerifySolution(level, r.Moves), "BFS solution must replay");
                    if (r.SolutionLength <= 9) Assert.AreEqual(r.SolutionLength, iddfs, LevelText.Render(level));
                    else Assert.AreEqual(-1, iddfs);
                }
                else
                {
                    Assert.AreEqual(SolverStatus.ProvenUnsolvable, r.Status);
                    Assert.AreEqual(-1, iddfs, "IDDFS found a solution BFS called unsolvable:\n" + LevelText.Render(level));
                }
                var a = StateSpaceAnalyzer.Analyze(level);
                Assert.AreEqual(r.Status == SolverStatus.Solved ? r.SolutionLength : -1, a.StartDistance);
                checkedLevels++;
            }
            Assert.Greater(checkedLevels, 20);
        }

        private static LevelDefinition RandomSmallLevel(Random rng)
        {
            int w = rng.Next(2, 5), h = rng.Next(2, 5), n = rng.Next(1, 3);
            var cells = new List<int>();
            for (int i = 0; i < w * h; i++) cells.Add(i);
            for (int i = cells.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = cells[i]; cells[i] = cells[j]; cells[j] = t; }
            if (cells.Count < 2 * n + 1) n = 1;
            int k = 0;
            var plops = new List<PlopDefinition>();
            var nests = new List<NestDefinition>();
            for (int i = 0; i < n; i++) plops.Add(new PlopDefinition(i, (PlopColor)i, new GridPosition(cells[k] % w, cells[k++] / w)));
            for (int i = 0; i < n; i++) nests.Add(new NestDefinition((PlopColor)i, new GridPosition(cells[k] % w, cells[k++] / w)));
            var walls = new List<GridPosition>();
            int wallCount = rng.Next(0, Math.Max(1, cells.Count - k));
            for (int i = 0; i < wallCount && k < cells.Count; i++) walls.Add(new GridPosition(cells[k] % w, cells[k++] / w));
            return new LevelDefinition("rnd", "rnd", w, h, walls, plops, nests);
        }

        /// <summary>Independent shortest-solution search via GameEngine + Undo. Returns -1 if none within maxDepth.</summary>
        private static int IddfsShortest(LevelDefinition level, int maxDepth)
        {
            var engine = new GameEngine();
            engine.LoadLevel(level);
            if (engine.IsCompleted) return 0;
            for (int depth = 1; depth <= maxDepth; depth++)
                if (Dfs(engine, depth)) return depth;
            return -1;
        }

        private static bool Dfs(GameEngine engine, int remaining)
        {
            foreach (var d in DirectionUtil.All)
            {
                if (engine.TryMove(d).Status != MoveStatus.Moved) continue;
                bool found = engine.IsCompleted ? remaining == 1 : remaining > 1 && Dfs(engine, remaining - 1);
                engine.Undo();
                if (found) return true;
            }
            return false;
        }
    }
}
