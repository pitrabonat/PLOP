using System;
using NUnit.Framework;
using Plop.Core;
using Plop.Meta;

namespace Plop.Tests
{
    /// <summary>Perfect Moves, best results, Dream Dust, Daily Gift and Daily Puzzle rules.</summary>
    public class MetaProgressionTests
    {
        [Test]
        public void Perfect_RequiresOptimalMovesWithoutHints()
        {
            var records = new LevelRecords(new MemoryStore());
            var slow = records.Submit("L1", 12, 9, assisted: false);
            Assert.IsFalse(slow.Perfect);
            Assert.IsTrue(slow.FirstCompletion);
            var hinted = records.Submit("L1", 9, 9, assisted: true);
            Assert.IsFalse(hinted.Perfect, "a hinted optimal solve is not a Perfect");
            Assert.IsTrue(hinted.NewBest);
            var perfect = records.Submit("L1", 9, 9, assisted: false);
            Assert.IsTrue(perfect.Perfect);
            Assert.IsTrue(perfect.FirstPerfect);
            var again = records.Submit("L1", 9, 9, assisted: false);
            Assert.IsFalse(again.FirstPerfect);
            Assert.IsFalse(again.NewBest);
        }

        [Test]
        public void BestResult_OnlyImproves_AndPersists()
        {
            var store = new MemoryStore();
            var records = new LevelRecords(store);
            records.Submit("A", 15, 10, false);
            records.Submit("A", 11, 10, false);
            records.Submit("A", 14, 10, false); // worse replay never lowers the record
            var reloaded = new LevelRecords(store);
            Assert.IsTrue(reloaded.TryGet("A", out var e));
            Assert.AreEqual(11, e.Best);
            Assert.AreEqual(3, e.Completions);
            Assert.IsFalse(e.Perfect);
        }

        [Test]
        public void MovesBelowOptimum_AreRejected()
        {
            var records = new LevelRecords(new MemoryStore());
            Assert.Throws<InvalidOperationException>(() => records.Submit("A", 5, 9, false));
        }

        [Test]
        public void UndoCannotExploitTheCounter()
        {
            // The counter is the engine history length: after Undo the remaining history is still a real path,
            // so a completion always has at least the verified optimal number of moves.
            var level = LevelText.Parse("u", "R..r");
            var engine = new GameEngine();
            engine.LoadLevel(level);
            engine.TryMove(Direction.Right);
            engine.TryMove(Direction.Left);
            engine.Undo();
            engine.TryMove(Direction.Right);
            engine.TryMove(Direction.Right);
            Assert.IsTrue(engine.IsCompleted);
            Assert.AreEqual(3, engine.MoveCount, "Undo removed the wasted move; the record is the solution length");
            var records = new LevelRecords(new MemoryStore());
            Assert.IsTrue(records.Submit("u", engine.MoveCount, 3, false).Perfect);
        }

        [Test]
        public void Wallet_GrantsEachRewardOnce_AndPersists()
        {
            var store = new MemoryStore();
            var wallet = new DreamDustWallet(store);
            Assert.IsTrue(wallet.Grant("complete:A", 5));
            Assert.IsFalse(wallet.Grant("complete:A", 5), "duplicate grant must be refused");
            Assert.AreEqual(5, wallet.Balance);
            var reloaded = new DreamDustWallet(store);
            Assert.AreEqual(5, reloaded.Balance);
            Assert.IsFalse(reloaded.Grant("complete:A", 5), "duplicate prevention survives relaunch");
        }

        [Test]
        public void LevelRewards_PayCompletionOnce_AndPerfectOnce()
        {
            var store = new MemoryStore();
            var wallet = new DreamDustWallet(store);
            var records = new LevelRecords(store);
            var first = CompletionRewards.ForLevel(wallet, records, "L7", 12, 9, false);
            Assert.AreEqual(RewardRules.LevelComplete, first.DustEarned);
            var replay = CompletionRewards.ForLevel(wallet, records, "L7", 12, 9, false);
            Assert.AreEqual(0, replay.DustEarned, "replaying a solved level pays nothing new");
            var perfect = CompletionRewards.ForLevel(wallet, records, "L7", 9, 9, false);
            Assert.AreEqual(RewardRules.LevelPerfect, perfect.DustEarned);
            var perfectAgain = CompletionRewards.ForLevel(wallet, records, "L7", 9, 9, false);
            Assert.AreEqual(0, perfectAgain.DustEarned);
            Assert.AreEqual(RewardRules.LevelComplete + RewardRules.LevelPerfect, wallet.Balance);
        }

        [Test]
        public void DailyGift_OncePerLocalCalendarDay_NoStreakPenalty()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            var morning = new DateTime(2026, 11, 24, 8, 0, 0);
            Assert.IsTrue(DailyGift.IsAvailable(wallet, morning));
            Assert.AreEqual(RewardRules.DailyGift, DailyGift.Claim(wallet, morning));
            Assert.IsFalse(DailyGift.IsAvailable(wallet, morning.AddHours(15)), "same day, late evening");
            Assert.AreEqual(0, DailyGift.Claim(wallet, morning.AddHours(15)));
            var nextDay = new DateTime(2026, 11, 25, 0, 0, 1);
            Assert.IsTrue(DailyGift.IsAvailable(wallet, nextDay));
            var weekLater = new DateTime(2026, 12, 2, 12, 0, 0); // skipped days lose nothing
            DailyGift.Claim(wallet, weekLater);
            Assert.AreEqual(2 * RewardRules.DailyGift, wallet.Balance);
            Assert.IsFalse(DailyGift.IsAvailable(wallet, new DateTime(2026, 11, 24, 23, 0, 0)), "moving the clock back cannot re-claim");
        }

        [Test]
        public void DailySchedule_IsDeterministic_AndCoversThePool()
        {
            const int pool = 60;
            var seen = new bool[pool];
            var day = new DateTime(2026, 11, 24);
            for (int i = 0; i < pool; i++)
            {
                int index = DailySchedule.PoolIndex(day.AddDays(i), pool);
                Assert.That(index, Is.InRange(0, pool - 1));
                Assert.IsFalse(seen[index], "every pool puzzle appears once per cycle");
                seen[index] = true;
            }
            Assert.AreEqual(DailySchedule.PoolIndex(day.AddHours(1), pool), DailySchedule.PoolIndex(day.AddHours(23), pool));
            Assert.AreEqual("2026-11-24", DailySchedule.DateKey(day.AddHours(23.9)));
            Assert.That(DailySchedule.PoolIndex(new DateTime(2025, 6, 1), pool), Is.InRange(0, pool - 1), "dates before the epoch still map");
        }

        [Test]
        public void DailyPuzzle_RewardsOncePerDate_AndTracksSeparately()
        {
            var store = new MemoryStore();
            var wallet = new DreamDustWallet(store);
            var records = new LevelRecords(store);
            var date = new DateTime(2026, 11, 24, 18, 0, 0);
            var r = CompletionRewards.ForDaily(wallet, records, "PLOP-DAILY", date, 9, 9, false);
            Assert.AreEqual(RewardRules.DailyPuzzle + RewardRules.DailyPuzzlePerfect, r.DustEarned);
            Assert.AreEqual(0, CompletionRewards.ForDaily(wallet, records, "PLOP-DAILY", date, 9, 9, false).DustEarned);
            Assert.IsTrue(wallet.HasGrant(RewardRules.DailyKey(date)));
            Assert.IsFalse(wallet.HasGrant(RewardRules.CompleteKey("PLOP-DAILY")), "daily completions do not count as campaign completions");
        }

        [Test]
        public void CorruptSaves_AreKeptAside_NotDestroyed()
        {
            var store = new MemoryStore();
            store.SetString(DreamDustWallet.StoreKey, "{ not json");
            store.SetString(LevelRecords.StoreKey, "[1,2");
            var wallet = new DreamDustWallet(store);
            var records = new LevelRecords(store);
            Assert.AreEqual(0, wallet.Balance);
            Assert.AreEqual(0, records.Count);
            Assert.AreEqual("{ not json", store.GetString(DreamDustWallet.StoreKey + ".corrupt", null));
            Assert.AreEqual("[1,2", store.GetString(LevelRecords.StoreKey + ".corrupt", null));
        }

        [Test]
        public void Cosmetics_DataModel_SpendsOnlyWhenAffordable()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            var item = CosmeticCatalog.Find("face.sleepy-smile");
            Assert.IsNotNull(item);
            Assert.IsFalse(wallet.TryUnlock(item));
            wallet.Grant("test", item.Cost);
            Assert.IsTrue(wallet.TryUnlock(item));
            Assert.IsTrue(wallet.Owns(item.Id));
            Assert.AreEqual(0, wallet.Balance);
            Assert.IsFalse(wallet.TryUnlock(item), "cannot buy twice");
        }
    }
}
