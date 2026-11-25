using System.IO;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Generation;

namespace Plop.Tests
{
    /// <summary>Level Lab's forced-chain / loop-risk inspection and effect-debugger text.</summary>
    public class ForcedChainInspectorTests
    {
        [Test]
        public void Inspect_FindsTheLongestChain_AndNoLoopRisk()
        {
            // Frost → Loop → slide: one swipe produces slide + teleport + slide.
            var level = LevelText.Parse(1, "chain", "1....r", "R~1...");
            var report = ForcedChainInspector.Inspect(level);
            Assert.IsTrue(report.Complete);
            Assert.IsFalse(report.LoopRisk);
            Assert.GreaterOrEqual(report.LongestChain, 3);
            Assert.AreEqual(Direction.Right, report.LongestChainDirection);
        }

        [Test]
        public void Inspect_CountsLavaFailures()
        {
            var report = ForcedChainInspector.Inspect(LevelText.Parse(1, "lava", "R!r", "..."));
            Assert.Greater(report.FailingCommands, 0);
            Assert.IsFalse(report.LoopRisk);
        }

        [Test]
        public void Describe_NamesEachEffect()
        {
            var level = LevelText.Parse(1, "chain", "1....r", "R~1...");
            var engine = new GameEngine();
            engine.LoadLevel(level);
            var lines = ForcedChainInspector.Describe(level, engine.TryMove(Direction.Right).Events);
            Assert.IsTrue(lines.Any(l => l.Contains("Frost")));
            Assert.IsTrue(lines.Any(l => l.Contains("Loop")));
            Assert.IsTrue(lines.Any(l => l.Contains("Nest")));
        }

        [Test]
        public void ShippedCampaign_HasNoForcedMovementLoops()
        {
            string path = GenerationTests.FindLevelFile(BalancedCampaign.PackId + ".json");
            if (path == null) Assert.Ignore("campaign pack not found in this environment.");
            foreach (var level in LevelSerializer.PackFromJson(File.ReadAllText(path)).Where(l => l.HasMechanics))
            {
                var report = ForcedChainInspector.Inspect(level, 50_000);
                Assert.IsFalse(report.LoopRisk, level.Name + ": " + report.FirstUnresolvedState + " " + report.FirstUnresolvedDirection);
            }
        }
    }
}
