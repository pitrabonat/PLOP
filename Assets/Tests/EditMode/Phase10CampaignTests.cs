using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Tests
{
    /// <summary>Phase 10 shipped campaign: 160 verified levels, nine Worlds, fast onboarding, challenges, saved ids.</summary>
    public class Phase10CampaignTests
    {
        private static List<LevelDefinition> Pack(string name)
        {
            string path = GenerationTests.FindLevelFile(name);
            if (path == null) Assert.Ignore(name + " not found in this environment.");
            return LevelSerializer.PackFromJson(File.ReadAllText(path));
        }

        [Test]
        public void Campaign_Has160VerifiedLevels_WithExactPerfectTargets()
        {
            var levels = Pack(ExpandedCampaign.PackId + ".json");
            Assert.AreEqual(ExpandedCampaign.LevelCount, levels.Count);
            Assert.AreEqual(levels.Count, levels.Select(l => l.Id).Distinct().Count());
            foreach (var level in levels)
            {
                Assert.IsTrue(LevelValidator.Validate(level).IsValid, level.Name);
                var solved = BfsSolver.Solve(level);
                Assert.AreEqual(SolverStatus.Solved, solved.Status, level.Name);
                Assert.AreEqual(solved.SolutionLength, level.OptimalMoves, level.Name);
                Assert.IsTrue(BfsSolver.VerifySolution(level, DirectionUtil.ParseMoveString(level.Metadata.Solution)), level.Name);
                Assert.AreEqual(LevelCanonicalizer.CanonicalId(level), level.Id, level.Name);
            }
        }

        [Test]
        public void Worlds_CoverTheCampaignInOrder()
        {
            int next = 1;
            foreach (var w in ExpandedCampaign.Worlds)
            {
                Assert.AreEqual(next, w.First, w.Id);
                Assert.GreaterOrEqual(w.Last, w.First);
                next = w.Last + 1;
            }
            Assert.AreEqual(ExpandedCampaign.LevelCount + 1, next);
            Assert.AreEqual(9, ExpandedCampaign.Worlds.Length);
            CollectionAssert.IsSubsetOf(new[] { "cozy-nest", "dewdrop-garden", "rainy-window", "twilight-attic", "moonlit-clouds" },
                ExpandedCampaign.Worlds.Select(w => w.Id).ToArray(), "the original Worlds keep their ids");
        }

        [Test]
        public void Onboarding_IntroducesEveryMechanicEarly_AndTheirWorldsUseThem()
        {
            var levels = Pack(ExpandedCampaign.PackId + ".json");
            int FirstWith(TileKind kind) => levels.FindIndex(l => l.Tiles.Any(t => t.Kind == kind)) + 1;
            Assert.LessOrEqual(FirstWith(TileKind.Frost), 7);
            Assert.LessOrEqual(FirstWith(TileKind.Fan), 21);
            Assert.LessOrEqual(FirstWith(TileKind.Lava), 35);
            Assert.LessOrEqual(FirstWith(TileKind.Portal), 49);
            Assert.IsNotNull(levels[6].Instruction, "each mechanic's first level has one short line");

            var uses = new Dictionary<string, System.Func<PuzzleInsight, bool>>
            {
                ["frosted-pond"] = i => i.Slides > 0,
                ["breezy-meadow"] = i => i.Pushes > 0,
                ["ember-hollow"] = i => i.HazardAlternatives > 0,
                ["prism-observatory"] = i => i.Teleports > 0
            };
            foreach (var w in ExpandedCampaign.Worlds)
            {
                if (!uses.TryGetValue(w.Id, out var used)) continue;
                for (int n = w.First; n <= w.Last; n++)
                {
                    var level = levels[n - 1];
                    var insight = PuzzleInsight.Analyze(level, StateSpaceAnalyzer.AnalyzeWithGraph(level));
                    Assert.IsTrue(used(insight), w.Id + " level " + n + " must really use its mechanic");
                }
            }
        }

        [Test]
        public void LaterWorlds_AreNotGreedySolvable_AndMostLevelsCombineMechanics()
        {
            var levels = Pack(ExpandedCampaign.PackId + ".json");
            int combined = 0, generated = 0;
            for (int n = 64; n <= ExpandedCampaign.LevelCount; n++)
            {
                var level = levels[n - 1];
                var insight = PuzzleInsight.Analyze(level, StateSpaceAnalyzer.AnalyzeWithGraph(level));
                Assert.IsFalse(insight.GreedySolves, "level " + n);
                if (!level.HasMechanics) continue;
                generated++;
                if (level.Tiles.Select(t => t.Kind).Distinct().Count() >= 2) combined++;
            }
            Assert.Greater(combined, generated * 3 / 4, "levels 64+ mostly combine mechanics");
        }

        [Test]
        public void LimitedMoveChallenges_AreMarked_AndSolvableWithinTheLimit()
        {
            var levels = Pack(ExpandedCampaign.PackId + ".json");
            foreach (int n in ExpandedCampaign.ChallengeLevels)
            {
                var level = levels[n - 1];
                Assert.AreEqual(level.OptimalMoves + ExpandedCampaign.ChallengeSlack, level.MoveLimit, "level " + n);
            }
            Assert.AreEqual(ExpandedCampaign.ChallengeLevels.Length, levels.Count(l => l.MoveLimit > 0));
        }

        [Test]
        public void SavedProgress_CarriesOver_ForUnchangedPuzzles()
        {
            var oldPack = Pack("PLOP_Pack_001.json");
            var levels = Pack(ExpandedCampaign.PackId + ".json");
            for (int i = 0; i < 5; i++) Assert.IsTrue(oldPack.Any(o => o.Id == levels[i].Id), "basics keep their Phase 8 tutorial ids");
            var reused = levels.Where(l => !l.HasMechanics && oldPack.Any(o => o.Id == l.Id)).ToList();
            Assert.GreaterOrEqual(reused.Count, 10, "Phase 8 pure-logic puzzles reused with their ids (records stay valid)");
        }

        [Test]
        public void ExpandedSlotSearch_IsDeterministic()
        {
            var plan = ExpandedCampaign.Plan();
            int slot = plan.First(s => s.Kind == SlotKind.Generated && s.WorldId == "breezy-meadow").Index;
            var a = ExpandedCampaign.SearchSlotRaw(0x1234, slot, 0);
            var b = ExpandedCampaign.SearchSlotRaw(0x1234, slot, 0);
            Assert.AreEqual(a.Success, b.Success);
            if (a.Success) Assert.AreEqual(LevelCanonicalizer.Canonicalize(a.Level), LevelCanonicalizer.Canonicalize(b.Level));
        }
    }
}
