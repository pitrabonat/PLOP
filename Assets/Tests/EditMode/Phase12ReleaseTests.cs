using NUnit.Framework;
using Plop.Meta;

namespace Plop.Tests
{
    /// <summary>Phase 12 (1.0.0 RC1) save hardening: level records survive a corrupt write.</summary>
    public class Phase12ReleaseTests
    {
        [Test]
        public void Records_RestoreTheLastGoodCopy_WhenTheMainRecordIsCorrupt()
        {
            var store = new MemoryStore();
            var records = new LevelRecords(store);
            records.Submit("PLOP-0001", 4, 4, false);
            records.Submit("PLOP-0002", 7, 6, false);
            Assert.IsNotNull(store.GetString(LevelRecords.BackupKey, null), "every write keeps a backup");

            store.SetString(LevelRecords.StoreKey, "{\"version\":1,\"levels\":[{\"id\":\"PLOP-00"); // torn write
            var reloaded = new LevelRecords(store);
            Assert.IsTrue(reloaded.RecoveredFromBackup);
            Assert.AreEqual(2, reloaded.Count);
            Assert.IsTrue(reloaded.IsPerfect("PLOP-0001"));
            Assert.IsTrue(reloaded.TryGet("PLOP-0002", out var e) && e.Best == 7);
            Assert.AreEqual("{\"version\":1,\"levels\":[{\"id\":\"PLOP-00", store.GetString(LevelRecords.StoreKey + ".corrupt", null), "evidence kept");
            Assert.AreEqual(store.GetString(LevelRecords.BackupKey, null), store.GetString(LevelRecords.StoreKey, null), "main record repaired");
        }

        [Test]
        public void Records_SkipOnlyTheMalformedEntry()
        {
            var store = new MemoryStore();
            store.SetString(LevelRecords.StoreKey,
                "{\"version\":1,\"levels\":[" +
                "{\"id\":\"A\",\"best\":5,\"perfect\":true,\"completions\":2}," +
                "{\"id\":\"B\",\"best\":\"seven\"}," +
                "{\"id\":\"C\",\"best\":-3}," +
                "42," +
                "{\"id\":\"D\",\"best\":9}]}");
            var records = new LevelRecords(store);
            Assert.AreEqual(2, records.Count, "A and D survive");
            Assert.AreEqual(3, records.SkippedEntries);
            Assert.IsTrue(records.IsPerfect("A"));
            Assert.IsTrue(records.TryGet("D", out var d) && d.Best == 9 && d.Completions == 1);
            Assert.IsFalse(records.RecoveredFromBackup);
        }

        [Test]
        public void Records_WithoutABackup_StartEmptyAndKeepTheEvidence()
        {
            var store = new MemoryStore();
            store.SetString(LevelRecords.StoreKey, "[1,2");
            var records = new LevelRecords(store);
            Assert.AreEqual(0, records.Count);
            Assert.IsFalse(records.RecoveredFromBackup);
            Assert.AreEqual("[1,2", store.GetString(LevelRecords.StoreKey + ".corrupt", null));
        }
    }
}
