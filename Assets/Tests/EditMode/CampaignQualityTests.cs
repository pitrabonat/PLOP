using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Plop.Core;
using Plop.Core.Json;
using Plop.Generation;
using Plop.Meta;
using Plop.Solver;

namespace Plop.Tests
{
    /// <summary>Phase 8 content guarantees: verified Perfect targets, quality floor, daily pool, migration data.</summary>
    public class CampaignQualityTests
    {
        private static string LevelsDir()
        {
            string pack = GenerationTests.FindPack();
            if (pack == null) Assert.Ignore("Level pack not found in this environment.");
            return Path.GetDirectoryName(pack);
        }

        private static List<LevelDefinition> Load(string file)
            => LevelSerializer.PackFromJson(File.ReadAllText(Path.Combine(LevelsDir(), file)));

        [Test]
        public void EveryLevel_HasVerifiedOptimalMoves_AndReplayableSolution()
        {
            foreach (var level in Load("PLOP_Pack_001.json"))
            {
                var solved = BfsSolver.Solve(level);
                Assert.AreEqual(SolverStatus.Solved, solved.Status, level.Name);
                Assert.AreEqual(solved.SolutionLength, level.OptimalMoves, level.Name + ": Perfect target must be the shortest solution");
                Assert.IsTrue(BfsSolver.VerifySolution(level, DirectionUtil.ParseMoveString(level.Metadata.Solution)), level.Name);
            }
        }

        [Test]
        public void RuntimeLoad_KeepsOptimalMoves_WithoutDevData()
        {
            var runtime = LevelSerializer.PackFromJson(File.ReadAllText(Path.Combine(LevelsDir(), "PLOP_Pack_001.json")), includeDevData: false);
            Assert.AreEqual(ProductionCatalog.LevelCount, runtime.Count);
            foreach (var level in runtime)
            {
                Assert.IsNull(level.Metadata);
                Assert.Greater(level.OptimalMoves, 0, level.Name);
            }
        }

        [Test]
        public void Campaign_MeetsTheQualityFloor()
        {
            var levels = Load("PLOP_Pack_001.json");
            int greedySolvedAfterTutorials = 0, withTrap = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                if (i < CampaignPlan.TutorialCount) continue;
                var insight = PuzzleInsight.Analyze(levels[i], StateSpaceAnalyzer.AnalyzeWithGraph(levels[i]));
                if (i >= 25) Assert.IsFalse(insight.GreedySolves, levels[i].Name + ": a naive 'always closer' player must not finish levels 26+");
                if (insight.GreedySolves) greedySolvedAfterTutorials++;
                if (insight.TemptingTraps + insight.EarlySettleTraps > 0) withTrap++;
                if (i >= 50) Assert.GreaterOrEqual(insight.CounterIntuitiveSteps, 1, levels[i].Name);
            }
            Assert.LessOrEqual(greedySolvedAfterTutorials, 8, "greedy-solvable puzzles after the tutorials");
            Assert.GreaterOrEqual(withTrap, 70, "levels with at least one tempting or early-settle trap");
        }

        [Test]
        public void Tutorials_KeepTheirIds_AndChangedSlotsHaveMigrationHistory()
        {
            var levels = Load("PLOP_Pack_001.json");
            var history = MiniJson.AsObject(MiniJson.Parse(File.ReadAllText(Path.Combine(LevelsDir(), "PLOP_Pack_001_SlotHistory.json"))), "history");
            var slots = MiniJson.AsArray(history["slots"], "slots");
            Assert.AreEqual(levels.Count, slots.Count);
            for (int i = 0; i < levels.Count; i++)
            {
                var slot = MiniJson.AsObject(slots[i], "slot");
                Assert.AreEqual(levels[i].Id, MiniJson.GetString(slot, "id"));
                var previous = MiniJson.AsArray(slot["previousIds"], "previousIds");
                if (i < CampaignPlan.TutorialCount) Assert.AreEqual(0, previous.Count, "tutorial IDs are stable");
                else Assert.Greater(previous.Count, 0, "slot " + (i + 1) + " must map its earlier ID for save migration");
            }
        }

        [Test]
        public void DailyPool_IsVerified_Unique_AndScheduled()
        {
            var daily = Load("PLOP_Daily_001.json");
            Assert.AreEqual(ProductionCatalog.DailyPoolSize, daily.Count);
            var report = LevelVerifier.VerifyPack(daily);
            Assert.IsTrue(report.IsValid, report.ToString());
            var registry = new DuplicateRegistry();
            foreach (var level in Load("PLOP_Pack_001.json")) registry.TryAdd(level);
            foreach (var level in daily)
            {
                Assert.IsFalse(registry.Contains(level), level.Name + " duplicates a campaign level");
                Assert.AreEqual(BfsSolver.Solve(level).SolutionLength, level.OptimalMoves, level.Name);
            }
            int today = DailySchedule.PoolIndex(new System.DateTime(2026, 11, 24), daily.Count);
            Assert.That(today, Is.InRange(0, daily.Count - 1));
        }

        [Test]
        public void QualitySearch_IsDeterministic()
        {
            var target = CampaignPlan.TargetFor(12);
            var a = QualitySearch.Search(77, target);
            var b = QualitySearch.Search(77, target);
            Assert.AreEqual(a.Success, b.Success);
            if (a.Success) Assert.AreEqual(LevelCanonicalizer.Canonicalize(a.Level), LevelCanonicalizer.Canonicalize(b.Level));
            Assert.AreEqual(a.Evaluations, b.Evaluations);
        }

        [Test]
        public void PuzzleInsight_DistinguishesTrivialFromDemandingBoards()
        {
            var trivial = LevelText.Parse("t", "R...r");
            var t = PuzzleInsight.Analyze(trivial, StateSpaceAnalyzer.AnalyzeWithGraph(trivial));
            Assert.IsTrue(t.GreedySolves);
            Assert.AreEqual(0, t.CounterIntuitiveSteps);
            Assert.AreEqual(0, t.TemptingTraps + t.EarlySettleTraps);
            Assert.Greater(t.RepetitionRatio, 0.5);

            var levels = Load("PLOP_Pack_001.json");
            var hard = levels[levels.Count - 1];
            var h = PuzzleInsight.Analyze(hard, StateSpaceAnalyzer.AnalyzeWithGraph(hard));
            Assert.IsFalse(h.GreedySolves);
            Assert.Greater(h.Challenge, t.Challenge + 20);
        }
    }
}
