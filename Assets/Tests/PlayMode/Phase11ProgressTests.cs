using System.Collections.Generic;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    /// <summary>Phase 11: Phase 10 saves migrate to the rebalanced campaign by id and frontier, never inventing completions.</summary>
    public sealed class Phase11ProgressTests
    {
        private static List<LevelDefinition> Levels(params string[] ids)
        {
            var levels = new List<LevelDefinition>();
            for (int i = 0; i < ids.Length; i++) levels.Add(LevelText.Parse(ids[i], "R" + new string('.', i + 1) + "r"));
            return levels;
        }

        [Test]
        public void Rebalance_KeepsSameIdCompletions_AndOpensUpToTheOldFrontier()
        {
            // Old order a b c d e f; new campaign kept a, c, f (f moved), replaced the rest.
            var levels = Levels("a", "n1", "c", "f", "n2", "n3", "n4");
            var progress = new LevelProgress(levels);
            const string save = "{\"resumeId\":\"e\",\"completedIds\":[\"a\",\"b\",\"c\",\"d\"],\"unlockedIds\":[\"a\",\"b\",\"c\",\"d\",\"e\"]}";
            Assert.IsTrue(progress.MigrateFromRebalancedCampaign(save, new[] { "a", "b", "c", "d", "e", "f" }));
            Assert.IsTrue(progress.IsCompleted(0));
            Assert.IsFalse(progress.IsCompleted(1), "a replacement is never marked complete");
            Assert.IsTrue(progress.IsCompleted(2));
            Assert.IsFalse(progress.IsCompleted(3), "f was never beaten");
            for (int i = 0; i < 5; i++) Assert.IsTrue(progress.IsUnlocked(i), "old frontier e (slot 5) stays open: " + i);
            Assert.IsFalse(progress.IsUnlocked(5));
            Assert.AreEqual(1, progress.ResumeIndex(), "the old resume puzzle is gone → first open unsolved slot");
        }

        [Test]
        public void Rebalance_FinishedFrontierOpensTheNextSlot_AndAliasesCarryCompletion()
        {
            var levels = Levels("a", "b2", "n1");
            var progress = new LevelProgress(levels);
            const string save = "{\"resumeId\":\"a\",\"completedIds\":[\"a\",\"b\"],\"unlockedIds\":[\"a\",\"b\"]}";
            Assert.IsTrue(progress.MigrateFromRebalancedCampaign(save, new[] { "a", "b" }, new[] { "b>b2", "zz>n1" }));
            Assert.IsTrue(progress.IsCompleted(1), "same board, looser Limited Moves → completion carries");
            Assert.IsFalse(progress.IsCompleted(2), "an alias of something never beaten grants nothing");
            Assert.IsTrue(progress.IsUnlocked(2), "beating the old frontier opened the next slot");
            Assert.AreEqual(0, progress.ResumeIndex(), "resume stays on a puzzle that still exists");
        }

        [Test]
        public void Rebalance_IgnoresMissingOrCorruptSaves()
        {
            var progress = new LevelProgress(Levels("a", "b"));
            Assert.IsFalse(progress.MigrateFromRebalancedCampaign("", new[] { "a" }));
            Assert.IsFalse(progress.MigrateFromRebalancedCampaign("{not json", new[] { "a" }));
            Assert.IsTrue(progress.MigrateFromRebalancedCampaign("{}", null), "an empty but valid save still opens level 1");
            Assert.IsTrue(progress.IsUnlocked(0));
            Assert.IsFalse(progress.IsUnlocked(1));
        }

        [Test]
        public void ShippedMigrationData_IsInResources()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>(LevelLibrary.DefaultPack));
            Assert.IsNotNull(Resources.Load<TextAsset>(LevelLibrary.DailyPack));
            Assert.IsNotNull(Resources.Load<TextAsset>(LevelLibrary.DeadlockPack));
            Assert.IsNotNull(Resources.Load<TextAsset>(LevelLibrary.PreviousOrder));
            Assert.IsNull(Resources.Load<TextAsset>(LevelLibrary.PreviousPack), "the retired pack is archived, not shipped");
            Assert.IsNull(Resources.Load<TextAsset>(LevelLibrary.LegacyPack));
            Assert.IsTrue(LevelLibrary.IsRetired(LevelLibrary.PreviousPack) && !LevelLibrary.IsRetired(LevelLibrary.DefaultPack));
        }
    }
}
