using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Generation;
using Plop.Meta;

namespace Plop.Tests
{
    /// <summary>Phase 11 stability: seeded save-corruption fuzzing and long random play sessions on every shipped level.</summary>
    public class Phase11StabilityTests
    {
        private static string ValidWallet()
        {
            var store = new MemoryStore();
            var w = new DreamDustWallet(store);
            w.Grant("complete:A", 8);
            w.Grant("perfect:A", 4);
            w.TrySpend("hint:A:1", 10);
            w.TryPurchase("finish.milky", 2);
            return store.GetString(DreamDustWallet.StoreKey, null);
        }

        private static IEnumerable<string> Mutations(string valid, int count, ulong seed)
        {
            var rng = new SplitMix64(seed);
            yield return "";
            yield return "null";
            yield return "[]";
            yield return "{\"balance\": 99999999999999999999}";
            yield return "{\"balance\": -5, \"earned\": -9}";
            yield return "{\"balance\": \"lots\", \"grants\": {\"a\":1}}";
            yield return "{\"grants\": [1, null, true, \"complete:A\"]}";
            for (int i = 1; i < valid.Length; i += Math.Max(1, valid.Length / 25)) yield return valid.Substring(0, i);
            const string alphabet = "{}[]\":,0123456789-.eE ntrufalsx\\";
            for (int i = 0; i < count; i++)
            {
                var chars = valid.ToCharArray();
                int edits = 1 + rng.NextInt(4);
                for (int e = 0; e < edits; e++) chars[rng.NextInt(chars.Length)] = alphabet[rng.NextInt(alphabet.Length)];
                yield return new string(chars);
            }
        }

        [Test]
        public void CorruptWalletAndRecords_NeverThrow_AndStaySane()
        {
            string valid = ValidWallet();
            foreach (var text in Mutations(valid, 400, 0x504C4F50_46555A5AUL))
            {
                var store = new MemoryStore();
                store.SetString(DreamDustWallet.StoreKey, text);
                store.SetString(DreamDustWallet.BackupKey, valid);
                store.SetString(LevelRecords.StoreKey, text);
                DreamDustWallet wallet = null;
                Assert.DoesNotThrow(() => wallet = new DreamDustWallet(store), text);
                Assert.GreaterOrEqual(wallet.Balance, 0, text);
                Assert.GreaterOrEqual(wallet.LifetimeEarned, wallet.Balance, text);
                Assert.DoesNotThrow(() => new LevelRecords(store), text);
                // A damaged wallet can still earn, and a grant that survived is never paid twice.
                bool had = wallet.HasGrant("complete:A");
                Assert.AreEqual(!had, wallet.Grant("complete:A", 8), text);
                Assert.DoesNotThrow(() => new DreamDustWallet(store), "the rewritten save loads: " + text);
            }
        }

        [Test]
        public void TornWallet_RecoversFromBackup_WithEverythingIntact()
        {
            string valid = ValidWallet();
            var store = new MemoryStore();
            store.SetString(DreamDustWallet.StoreKey, valid.Substring(0, valid.Length / 2));
            store.SetString(DreamDustWallet.BackupKey, valid);
            var wallet = new DreamDustWallet(store);
            Assert.IsTrue(wallet.RecoveredFromBackup);
            Assert.AreEqual(0, wallet.Balance, "8 + 4 − 10 hint − 2 cosmetic");
            Assert.IsTrue(wallet.HasGrant("perfect:A") && wallet.HasSpent("hint:A:1") && wallet.Owns("finish.milky"));
        }

        [Test]
        public void RandomSessions_OnEveryShippedLevel_KeepTheEngineConsistent()
        {
            string path = GenerationTests.FindLevelFile(BalancedCampaign.PackId + ".json");
            if (path == null) Assert.Ignore("campaign pack not found");
            var levels = LevelSerializer.PackFromJson(File.ReadAllText(path)).ToList();
            string daily = GenerationTests.FindLevelFile(BalancedCampaign.DailyPackId + ".json");
            if (daily != null) levels.AddRange(LevelSerializer.PackFromJson(File.ReadAllText(daily)));
            var rng = new SplitMix64(0x504C4F50_53455353UL);
            var engine = new GameEngine();
            foreach (var level in levels)
            {
                engine.LoadLevel(level);
                var initial = engine.InitialState;
                for (int step = 0; step < 120; step++)
                {
                    int roll = rng.NextInt(10);
                    if (roll == 0) { engine.Restart(); Assert.AreEqual(initial, engine.CurrentState, level.Id); continue; }
                    if (roll == 1) { engine.Undo(); continue; }
                    var before = engine.GetState();
                    int movesBefore = engine.MoveCount;
                    bool couldPlay = engine.CanPlay;
                    var result = engine.TryMove((Direction)rng.NextInt(4));
                    if (!couldPlay) Assert.IsFalse(result.Changed, level.Id + ": no move after the attempt ended");
                    if (result.Changed)
                    {
                        Assert.AreEqual(movesBefore + 1, engine.MoveCount, level.Id);
                        Assert.IsTrue(engine.Undo(), level.Id);
                        Assert.AreEqual(before, engine.CurrentState, level.Id + ": undo restores the exact board");
                        engine.TryMove(result.Direction);
                    }
                    var s = engine.CurrentState;
                    var cells = Enumerable.Range(0, s.PlopCount).Select(s.CellOf).ToList();
                    if (!s.Failed) Assert.AreEqual(cells.Count, cells.Distinct().Count(), level.Id + ": two Plops share a cell");
                    if (level.MoveLimit > 0) Assert.LessOrEqual(engine.MoveCount, level.MoveLimit, level.Id);
                    // Replaying the history from the start reaches the same board.
                    if (step % 30 == 29)
                    {
                        var replay = new GameEngine();
                        replay.LoadLevel(level);
                        foreach (var d in engine.GetMoveHistory()) replay.TryMove(d);
                        Assert.AreEqual(engine.CurrentState, replay.CurrentState, level.Id + ": deterministic replay");
                    }
                }
            }
        }
    }
}
