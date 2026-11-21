using NUnit.Framework;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Tests
{
    public class PuzzleQualityTests
    {
        [Test]
        public void OpenBoard_CountsBothShortestRoutes()
        {
            var level = LevelText.Parse("open", "R.", ".r");
            var analysis = StateSpaceAnalyzer.Analyze(level);
            Assert.AreEqual(2, analysis.ShortestSolutionCount);
            Assert.IsFalse(analysis.RequiresAwayMove);
            Assert.AreEqual(1, DifficultyEstimator.Estimate(level, analysis).Metrics["solutionTurns"]);
        }

        [Test]
        public void WallMaze_RequiresMovementAwayFromNest()
        {
            var level = LevelText.Parse("maze", "R#r", "...");
            var analysis = StateSpaceAnalyzer.Analyze(level);
            Assert.IsTrue(analysis.RequiresAwayMove);
            var metrics = DifficultyEstimator.Estimate(level, analysis).Metrics;
            Assert.AreEqual(2, metrics["wallDetour"]);
            Assert.AreEqual(0, metrics["wallInteractionMoves"]);
            Assert.AreEqual(2, metrics["solutionTurns"]);
        }

        [Test]
        public void WallBlocksOneAwakePlop_WhileAnotherMoves()
        {
            var level = LevelText.Parse("block", "r..", "R#.", "###", "B.b");
            var analysis = StateSpaceAnalyzer.Analyze(level);
            Assert.IsTrue(analysis.RequiresCoordination);
            Assert.Greater(DifficultyEstimator.Estimate(level, analysis).Metrics["wallInteractionMoves"], 0);
        }

        [TestCase("R..r#R.r", 1)]
        [TestCase("R.r#R.r", 0)]
        public void SettlementOrder_UsesNestPositions_AndAllowsSimultaneousSettlement(string board, int requiredPairs)
        {
            var analysis = StateSpaceAnalyzer.Analyze(LevelText.Parse("order", board));
            Assert.AreEqual(1, analysis.FirstSettlementChoices);
            Assert.AreEqual(requiredPairs, analysis.RequiredSettlementPairs);
            Assert.IsFalse(analysis.RequiresCoordination);
        }

        [Test]
        public void EarlySettlement_CanPermanentlyBlockAnotherPlop()
        {
            var level = LevelText.Parse("trap", "..b", "Rr.", "#B#");
            var analysis = StateSpaceAnalyzer.Analyze(level);
            Assert.IsTrue(analysis.Solvable);
            Assert.Greater(analysis.DeadEndSettlements, 0);
        }
    }
}
