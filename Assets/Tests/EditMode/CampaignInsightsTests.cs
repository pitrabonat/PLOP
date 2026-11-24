using NUnit.Framework;
using Plop.Core;
using Plop.Generation;

namespace Plop.Tests
{
    /// <summary>Level Lab analysis helpers: trivial flags, alternative lengths, settlement order, similarity.</summary>
    public class CampaignInsightsTests
    {
        [Test]
        public void TrivialCorridor_IsFlagged()
        {
            var row = CampaignInsights.Analyze(LevelText.Parse("t", "R...r"));
            Assert.IsTrue(row.Analysed);
            Assert.IsTrue(row.IsTrivial);
            CollectionAssert.Contains(row.TrivialFlags, "greedy finishes");
            Assert.AreEqual("R@4", row.SettlementSequence, "one cell per swipe: the Plop settles on the fourth swipe");
        }

        [Test]
        public void FirstMoveLengths_ReportAlternativesAndNoOps()
        {
            var row = CampaignInsights.Analyze(LevelText.Parse("alt", "R..r"));
            Assert.AreEqual(-1, row.FirstMoveLengths[(int)Direction.Up], "a blocked swipe changes nothing");
            Assert.AreEqual(3, row.FirstMoveLengths[(int)Direction.Right]);
            CollectionAssert.AreEqual(new[] { 3 }, row.AlternativeLengths());
        }

        [Test]
        public void SettlementSequence_FollowsTheRoute()
        {
            var level = LevelText.Parse("order", "R.r", "B.b");
            var row = CampaignInsights.Analyze(level);
            StringAssert.Contains("R@", row.SettlementSequence);
            StringAssert.Contains("B@", row.SettlementSequence);
        }

        [Test]
        public void Similarity_IsSymmetryAware()
        {
            var a = LevelText.Parse("a", "R#.r", "....");
            var mirrored = LevelText.Parse("b", "r.#R", "....");
            var different = LevelText.Parse("c", "B..b", "#..#");
            Assert.AreEqual(1.0, CampaignInsights.Similarity(a, mirrored), 1e-9);
            Assert.Less(CampaignInsights.Similarity(a, different), 0.5);
            Assert.AreEqual(0.0, CampaignInsights.Similarity(a, LevelText.Parse("d", "R.r")), "different sizes never match");
        }

        [Test]
        public void AnalyzeAll_FindsNearestNeighbour_AndRendersTable()
        {
            var levels = new[]
            {
                LevelText.Parse("x", "R#.r", "...."),
                LevelText.Parse("y", "r.#R", "...."),
                LevelText.Parse("z", "B...b", "Y...y")
            };
            var rows = CampaignInsights.AnalyzeAll(levels);
            Assert.AreEqual(1, rows[0].MostSimilarIndex);
            Assert.AreEqual(0, rows[1].MostSimilarIndex);
            string table = CampaignInsights.ToTable(rows);
            Assert.AreEqual(4, table.Trim().Split('\n').Length);
            StringAssert.StartsWith("#\tsize", table);
        }
    }
}
