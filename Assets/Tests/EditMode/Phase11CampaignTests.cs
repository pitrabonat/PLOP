using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Core.Json;
using Plop.Generation;
using Plop.Meta;
using Plop.Solver;

namespace Plop.Tests
{
    /// <summary>
    /// Phase 11 rebalanced campaign (PLOP_Pack_003) and Daily pool (PLOP_Daily_002): verified, paced, gated, and
    /// migratable from the Phase 10 pack. The quality numbers themselves are heuristics; these tests pin the
    /// properties the design relies on.
    /// </summary>
    public class Phase11CampaignTests
    {
        private static List<LevelDefinition> Pack(string name)
        {
            string path = GenerationTests.FindLevelFile(name);
            if (path == null) Assert.Ignore(name + " not found in this environment.");
            return LevelSerializer.PackFromJson(File.ReadAllText(path));
        }

        private static bool Uses(LevelDefinition level, TileKind kind) => level.Tiles.Any(t => t.Kind == kind);

        [Test]
        public void Campaign_Has160StrictlyVerifiedLevels()
        {
            var levels = Pack(BalancedCampaign.PackId + ".json");
            var report = LevelVerifier.VerifyProductionPack(levels, expectedCount: BalancedCampaign.LevelCount);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(levels.Count, levels.Select(l => l.Id).Distinct().Count(), "ids are unique");
        }

        [Test]
        public void EarlyPacing_FollowsTheTeachingOrder()
        {
            var levels = Pack(BalancedCampaign.PackId + ".json");
            for (int i = 0; i < 5; i++) Assert.IsFalse(levels[i].HasMechanics, "level " + (i + 1) + " is pure swipe-and-nest");
            Assert.AreEqual(1, levels[0].Plops.Count, "L1: one Plop");
            Assert.Greater(levels[1].Walls.Count, 0, "L2: walls");
            Assert.AreEqual(2, levels[2].Plops.Count, "L3: two Plops");
            Assert.IsTrue(Uses(levels[5], TileKind.Frost), "L6 introduces Frost");
            Assert.IsFalse(string.IsNullOrEmpty(levels[5].Instruction), "L6 explains Frost");
            Assert.IsTrue(Uses(levels[7], TileKind.Frost) && levels[7].Plops.Count == 2, "L8: Frost with a second Plop");
            Assert.IsTrue(levels.Take(20).All(l => l.Tiles.All(t => t.Kind == TileKind.Frost)), "only Frost before Breeze");
        }

        [TestCase(21, TileKind.Fan)]
        [TestCase(35, TileKind.Lava)]
        [TestCase(49, TileKind.Portal)]
        public void EachMechanic_IsDiscoveredAtItsWorldsFirstLevel(int level, TileKind kind)
        {
            var levels = Pack(BalancedCampaign.PackId + ".json");
            Assert.IsTrue(Uses(levels[level - 1], kind), "level " + level);
            Assert.IsFalse(string.IsNullOrEmpty(levels[level - 1].Instruction), "level " + level + " explains " + kind);
            for (int i = 0; i < level - 1; i++) Assert.IsFalse(Uses(levels[i], kind), kind + " appears before its introduction at " + (i + 1));
            Assert.AreEqual(level, DailySchedule.Discovery.Single(d => d.Kind == kind).Level, "daily gating matches the campaign");
        }

        [Test]
        public void LimitedMoves_LeaveSlackEarly_AndAreExactLate()
        {
            var levels = Pack(BalancedCampaign.PackId + ".json");
            foreach (var (n, slack) in BalancedCampaign.Challenges)
                Assert.AreEqual(levels[n - 1].OptimalMoves + slack, levels[n - 1].MoveLimit, "level " + n);
            Assert.AreEqual(BalancedCampaign.Challenges.Length, levels.Count(l => l.MoveLimit > 0));
            Assert.Greater(BalancedCampaign.Challenges.First().Slack, 0, "the first challenge forgives");
            Assert.AreEqual(0, BalancedCampaign.Challenges.Last().Slack, "the last challenge asks for the optimum");
        }

        [Test]
        public void WorldMastery_NeedsDeepPlanning()
        {
            var levels = Pack(BalancedCampaign.PackId + ".json");
            foreach (int n in new[] { 20, 34, 48, 63 })
            {
                var level = levels[n - 1];
                var analysis = StateSpaceAnalyzer.AnalyzeWithGraph(level);
                var q = PuzzleQuality.Evaluate(level, analysis, compareMechanics: false);
                Assert.GreaterOrEqual(q.PlanningDepth, 3, "level " + n + " is a World finale");
                Assert.IsFalse(q.Player(PlayerProfile.Greedy).Solved, "greedy play does not finish level " + n);
            }
        }

        [Test]
        public void Rebalance_KeepsMostPhase10Puzzles_AndTheOrderFileMatchesTheArchive()
        {
            var old = Pack(ExpandedCampaign.PackId + ".json");
            var levels = Pack(BalancedCampaign.PackId + ".json");
            string orderPath = GenerationTests.FindLevelFile("PLOP_Pack_002_Order.json");
            Assert.IsNotNull(orderPath);
            var file = MiniJson.AsObject(MiniJson.Parse(File.ReadAllText(orderPath)), "order");
            var order = MiniJson.AsArray(file["levelIds"], "levelIds").Select(o => (string)o).ToList();
            var aliases = MiniJson.AsArray(file["aliases"], "aliases").Select(o => ((string)o).Split('>')).ToList();
            CollectionAssert.AreEqual(old.Select(l => l.Id).ToList(), order);
            var oldIds = new HashSet<string>(order);
            int kept = levels.Count(l => oldIds.Contains(l.Id));
            Assert.GreaterOrEqual(kept, 85, "good Phase 10 puzzles are preserved under their ids");
            var newById = levels.ToDictionary(l => l.Id);
            foreach (var pair in aliases)
            {
                Assert.IsTrue(oldIds.Contains(pair[0]) && newById.ContainsKey(pair[1]), string.Join(">", pair));
                var now = newById[pair[1]];
                Assert.IsTrue(now.MoveLimit == 0 || now.MoveLimit >= old.Single(l => l.Id == pair[0]).MoveLimit, "an alias only loosens the rule");
            }
            Assert.AreEqual(old[0].Id, levels[0].Id, "the very first puzzle is unchanged");
            var byId = old.ToDictionary(l => l.Id);
            foreach (var level in levels.Where(l => byId.ContainsKey(l.Id)))
            {
                var before = byId[level.Id];
                Assert.AreEqual(before.Width, level.Width, level.Id + " kept id ⇒ same board");
                CollectionAssert.AreEquivalent(before.Walls, level.Walls, level.Id);
                CollectionAssert.AreEquivalent(before.Tiles, level.Tiles, level.Id);
            }
        }

        [Test]
        public void DailyPool_IsVerified_Distinct_AndCoversEveryMechanic()
        {
            var daily = Pack(BalancedCampaign.DailyPackId + ".json");
            var campaign = Pack(BalancedCampaign.PackId + ".json");
            Assert.AreEqual(BalancedCampaign.DailyCount, daily.Count);
            var report = LevelVerifier.VerifyProductionPack(daily, expectedCount: BalancedCampaign.DailyCount);
            Assert.IsTrue(report.IsValid, report.ToString());
            var registry = new DuplicateRegistry();
            foreach (var level in campaign) registry.TryAdd(level);
            foreach (var level in daily) Assert.IsFalse(registry.Contains(level), level.Name + " duplicates a campaign level");
            Assert.GreaterOrEqual(daily.Count(l => !l.HasMechanics), 20, "classic puzzles for new players");
            foreach (var kind in new[] { TileKind.Frost, TileKind.Fan, TileKind.Lava, TileKind.Portal })
                Assert.GreaterOrEqual(daily.Count(l => Uses(l, kind)), 10, kind + " appears in the Daily pool");
            Assert.IsTrue(daily.All(l => l.MoveLimit == 0), "Daily Puzzles never fail on a move limit");
        }

        [Test]
        public void DailyGating_OnlyServesLearnedMechanics_AndIsDeterministic()
        {
            var daily = Pack(BalancedCampaign.DailyPackId + ".json");
            var start = new DateTime(2026, 11, 25);
            var seenNovice = new HashSet<string>();
            var seenExpert = new HashSet<string>();
            for (int d = 0; d < daily.Count; d++)
            {
                var day = start.AddDays(d);
                int novice = DailySchedule.PickIndex(daily, day, 0);
                Assert.IsFalse(daily[novice].HasMechanics, "a new player gets a classic puzzle");
                int frost = DailySchedule.PickIndex(daily, day, 6);
                Assert.IsTrue(daily[frost].Tiles.All(t => t.Kind == TileKind.Frost), "after level 6 only Frost is known");
                int expert = DailySchedule.PickIndex(daily, day, 160);
                Assert.AreEqual(expert, DailySchedule.PickIndex(daily, day.AddHours(13), 160), "same calendar day → same puzzle");
                seenNovice.Add(daily[novice].Id);
                seenExpert.Add(daily[expert].Id);
            }
            Assert.AreEqual(daily.Count, seenExpert.Count, "an expert sees every puzzle once per cycle");
            Assert.AreEqual(daily.Count(l => !l.HasMechanics), seenNovice.Count, "a new player cycles through all classic puzzles");
            Assert.AreEqual(-1, DailySchedule.PickIndex(new List<LevelDefinition>(), start, 0));
        }
    }
}
