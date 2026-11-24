using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Plop.Meta;

namespace Plop.Tests
{
    /// <summary>Phase 9 cosmetic catalog, ownership, Dream Dust transactions, presets and save migration.</summary>
    public class CosmeticTests
    {
        internal static string CatalogPath()
        {
            string pack = GenerationTests.FindPack();
            if (pack == null) return null;
            var assets = Directory.GetParent(pack).Parent.Parent.Parent.FullName; // …/Assets
            string path = Path.Combine(assets, "Data", "Cosmetics", "Resources", "Cosmetics", "PLOP_Cosmetics_001.json");
            return File.Exists(path) ? path : null;
        }

        private static CosmeticCatalog Shipped()
        {
            string path = CatalogPath();
            if (path == null) Assert.Ignore("Cosmetic catalog not found in this environment.");
            return CosmeticCatalog.Parse(File.ReadAllText(path));
        }

        private const string Tiny = @"{""schemaVersion"":1,""contentVersion"":1,
          ""defaults"":{""BodyStyle"":""body.a"",""BodyFinish"":""finish.a"",""Palette"":""palette.a"",""Face"":""face.a"",
                       ""Accessory"":""accessory.none"",""Nest"":""nest.a"",""Effect"":""effect.none""},
          ""items"":[
            {""id"":""body.a"",""category"":""BodyStyle"",""name"":""A"",""asset"":""style:a"",""unlock"":""Free""},
            {""id"":""body.b"",""category"":""BodyStyle"",""name"":""B"",""asset"":""style:b"",""unlock"":""Dust"",""cost"":30},
            {""id"":""finish.a"",""category"":""BodyFinish"",""name"":""A"",""asset"":""finish:a"",""unlock"":""Free""},
            {""id"":""finish.b"",""category"":""BodyFinish"",""name"":""B"",""asset"":""finish:b"",""unlock"":""Dust"",""cost"":50},
            {""id"":""palette.a"",""category"":""Palette"",""name"":""A"",""asset"":""palette:a"",""unlock"":""Free""},
            {""id"":""face.a"",""category"":""Face"",""name"":""A"",""asset"":""face:a"",""unlock"":""Free""},
            {""id"":""accessory.none"",""category"":""Accessory"",""name"":""None"",""asset"":""accessory:none"",""unlock"":""Free""},
            {""id"":""accessory.cap"",""category"":""Accessory"",""name"":""Cap"",""asset"":""accessory:cap"",""unlock"":""Dust"",""cost"":20,""compatibleStyles"":[""body.a""]},
            {""id"":""accessory.leaf"",""category"":""Accessory"",""name"":""Leaf"",""asset"":""accessory:leaf"",""unlock"":""Milestone"",""milestone"":""LevelsCompleted"",""count"":5},
            {""id"":""nest.a"",""category"":""Nest"",""name"":""A"",""asset"":""nest:a"",""unlock"":""Free""},
            {""id"":""nest.b"",""category"":""Nest"",""name"":""B"",""asset"":""nest:b"",""unlock"":""Milestone"",""milestone"":""PerfectLevels"",""count"":2,""cost"":40},
            {""id"":""effect.none"",""category"":""Effect"",""name"":""None"",""asset"":""effect:none"",""unlock"":""Free""}
          ]}";

        private static (CosmeticCollection Collection, MemoryStore Store) Fresh(int dust, IKeyValueStore store = null)
        {
            var memory = store as MemoryStore ?? new MemoryStore();
            store = store ?? memory;
            var wallet = new DreamDustWallet(store);
            if (dust > 0) wallet.Grant("test:seed", dust);
            var catalog = CosmeticCatalog.Parse(Tiny);
            return (new CosmeticCollection(catalog, wallet, new AppearanceStore(store, catalog)), memory);
        }

        // ------------------------------------------------------------------ catalog

        [Test]
        public void ShippedCatalog_IsValid_AndCoversThePhase9Content()
        {
            var catalog = Shipped();
            CollectionAssert.IsEmpty(catalog.Validate(), "shipped catalog problems");
            Assert.GreaterOrEqual(catalog.InCategory(CosmeticCategory.BodyStyle).Count, 3);
            Assert.GreaterOrEqual(catalog.InCategory(CosmeticCategory.BodyFinish).Count, 6);
            Assert.GreaterOrEqual(catalog.InCategory(CosmeticCategory.Accessory).Count - 1, 8, "eight accessories plus None");
            Assert.GreaterOrEqual(catalog.InCategory(CosmeticCategory.Nest).Count, 5);
            Assert.GreaterOrEqual(catalog.InCategory(CosmeticCategory.Effect).Count - 1, 4);
            int free = 0;
            foreach (var item in catalog.Items) if (item.IsFree) free++;
            Assert.GreaterOrEqual(free, 8, "several free starter cosmetics");
            foreach (var item in catalog.Items)
                Assert.IsTrue(item.IsFree || item.CanBuy || item.Unlock == UnlockMethod.Milestone, item.Id + " has a clear unlock route");
        }

        [Test]
        public void ShippedIds_AreStable()
        {
            // These IDs are save keys. Removing one is allowed only with a deliberate migration note.
            var catalog = Shipped();
            foreach (var id in new[]
            {
                "body.classic", "body.bouncy", "body.snoozy", "finish.classic", "finish.frosted", "finish.milky",
                "finish.dewdrop", "finish.velvet", "finish.pearl", "palette.classic", "palette.pastel", "palette.sorbet",
                "face.classic", "face.button", "face.sleepy", "face.blush", "face.starry", "accessory.none", "accessory.bow",
                "accessory.flower", "accessory.leaf", "accessory.ribbon", "accessory.star", "accessory.nightcap",
                "accessory.cloud", "accessory.crown", "nest.classic", "nest.cloud", "nest.marshmallow", "nest.flower",
                "nest.moon", "effect.none", "effect.puff", "effect.sparkle", "effect.bubble", "effect.stars"
            })
                Assert.IsNotNull(catalog.Find(id), id);
        }

        [Test]
        public void Validator_ReportsBrokenDefinitions()
        {
            string broken = Tiny
                .Replace(@"""id"":""finish.b""", @"""id"":""finish.a""")                       // duplicate id
                .Replace(@"""asset"":""nest:a""", @"""asset"":""effect:a""")                    // wrong asset prefix
                .Replace(@"""cost"":30", @"""cost"":0")                                         // dust item without price
                .Replace(@"""compatibleStyles"":[""body.a""]", @"""compatibleStyles"":[""body.zz""]")
                .Replace(@"""Effect"":""effect.none""", @"""Effect"":""effect.gone""")         // missing default
                .Replace(@"{""id"":""face.a"",""category"":""Face""", @"{""id"":""face.a"",""category"":""Hat""");
            var issues = CosmeticCatalog.Parse(broken).Validate(asset => asset != "finish:b");
            string all = string.Join("\n", issues);
            StringAssert.Contains("duplicate id", all);
            StringAssert.Contains("does not match category", all);
            StringAssert.Contains("price must be", all);
            StringAssert.Contains("not a body style", all);
            StringAssert.Contains("Default Effect 'effect.gone' does not exist", all);
            StringAssert.Contains("unknown category 'Hat'", all);
            StringAssert.Contains("Default Face 'face.a' does not exist", all, "the face default points at a dropped item");
        }

        [Test]
        public void Validator_ReportsUnregisteredAssetsAndPreviews()
        {
            var catalog = CosmeticCatalog.Parse(Tiny.Replace(@"""asset"":""nest:a""", @"""asset"":""nest:a"",""preview"":""nest:missing"""));
            var issues = catalog.Validate(asset => asset != "finish:b" && asset != "nest:missing");
            string all = string.Join("\n", issues);
            StringAssert.Contains("finish.b: asset 'finish:b' is not registered", all);
            StringAssert.Contains("preview 'nest:missing' is not registered", all);
        }

        // ------------------------------------------------------------------ presets

        [Test]
        public void Preset_RoundTrips_AndSurvivesRelaunch()
        {
            var (c, store) = Fresh(100);
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("finish.b"));
            Assert.IsTrue(c.Equip("finish.b"));
            var reloaded = Fresh(0, store).Collection;
            Assert.AreEqual("finish.b", reloaded.Equipped.Get(CosmeticCategory.BodyFinish));
            Assert.AreEqual("body.a", reloaded.Equipped.Get(CosmeticCategory.BodyStyle));
            StringAssert.Contains("\"contentVersion\":1", store.GetString(AppearanceStore.StoreKey, ""));
        }

        [Test]
        public void RemovedOrUnknownCosmetics_FallBackToDefaults()
        {
            var store = new MemoryStore();
            store.SetString(AppearanceStore.StoreKey,
                @"{""version"":1,""contentVersion"":1,""active"":0,""presets"":[{""body"":""body.retired"",""finish"":""finish.b"",""accessory"":""accessory.gone"",""nest"":""nest.a""}]}");
            var c = Fresh(0, store).Collection;
            Assert.AreEqual("body.a", c.Equipped.Get(CosmeticCategory.BodyStyle), "removed body style");
            Assert.AreEqual("finish.a", c.Equipped.Get(CosmeticCategory.BodyFinish), "unowned finish is not kept equipped");
            Assert.AreEqual("accessory.none", c.Equipped.Get(CosmeticCategory.Accessory));
            Assert.AreEqual("palette.a", c.Equipped.Get(CosmeticCategory.Palette), "missing slot gets the default");
        }

        [Test]
        public void CorruptAppearance_IsKeptAside_AndOtherSavesAreUntouched()
        {
            var store = new MemoryStore();
            new DreamDustWallet(store).Grant("complete:L1", 5);
            store.SetString(LevelRecords.StoreKey, @"{""version"":1,""levels"":[]}");
            store.SetString(AppearanceStore.StoreKey, "{oops");
            var c = Fresh(0, store).Collection;
            Assert.AreEqual("body.a", c.Equipped.Get(CosmeticCategory.BodyStyle));
            Assert.AreEqual("{oops", store.GetString(AppearanceStore.StoreKey + ".corrupt", null));
            Assert.AreEqual(5, c.Wallet.Balance);
        }

        [Test]
        public void Phase8Save_MigratesWithoutLosingDustOrProgress()
        {
            var store = new MemoryStore();
            const string phase8Wallet = @"{""version"":1,""balance"":60,""earned"":60,""grants"":[""complete:PLOP-A"",""gift:2026-11-25""],""owned"":[""color.sunset""]}";
            const string records = @"{""version"":1,""levels"":[{""id"":""PLOP-A"",""best"":1,""perfect"":true,""completions"":1}]}";
            const string progress = @"{""resumeId"":""PLOP-B"",""completedIds"":[""PLOP-A""],""unlockedIds"":[""PLOP-A"",""PLOP-B""]}";
            store.SetString(DreamDustWallet.StoreKey, phase8Wallet);
            store.SetString(LevelRecords.StoreKey, records);
            store.SetString("plop.progress.v1.Levels/PLOP_Pack_001", progress);

            var c = Fresh(0, store).Collection;
            Assert.AreEqual(60, c.Wallet.Balance);
            Assert.IsTrue(c.Wallet.HasGrant("gift:2026-11-25"), "earlier grants still block duplicate rewards");
            Assert.AreEqual("body.a", c.Equipped.Get(CosmeticCategory.BodyStyle));
            Assert.IsFalse(c.IsOwned("body.b"));
            Assert.AreEqual(records, store.GetString(LevelRecords.StoreKey, null));
            Assert.AreEqual(progress, store.GetString("plop.progress.v1.Levels/PLOP_Pack_001", null), "campaign progress is never rewritten");
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("body.b"));
            Assert.AreEqual(30, c.Wallet.Balance);
            Assert.IsTrue(c.Wallet.Owns("color.sunset"), "unknown legacy ownership is preserved, not deleted");
        }

        // ------------------------------------------------------------------ transactions

        [Test]
        public void Unlock_ValidatesItemCategoryOwnershipAndFunds()
        {
            var (c, _) = Fresh(40);
            Assert.AreEqual(UnlockResult.UnknownItem, c.TryUnlock("finish.nope"));
            Assert.AreEqual(UnlockResult.WrongCategory, c.TryUnlock("finish.b", CosmeticCategory.Nest));
            Assert.AreEqual(UnlockResult.AlreadyOwned, c.TryUnlock("finish.a"), "free items are owned from the start");
            Assert.AreEqual(UnlockResult.NotForSale, c.TryUnlock("accessory.leaf"), "milestone-only item");
            Assert.AreEqual(UnlockResult.InsufficientDust, c.TryUnlock("finish.b"));
            Assert.AreEqual(40, c.Wallet.Balance);
            Assert.AreEqual(CosmeticState.TooExpensive, c.StateOf(c.Catalog.Find("finish.b")));
            Assert.AreEqual(10, c.Shortfall(c.Catalog.Find("finish.b")));
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("nest.b"), "milestone item with a Dust alternative");
            Assert.AreEqual(0, c.Wallet.Balance);
        }

        [Test]
        public void DuplicateUnlock_NeverChargesTwice()
        {
            var (c, store) = Fresh(100);
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("finish.b"));
            Assert.AreEqual(UnlockResult.AlreadyOwned, c.TryUnlock("finish.b"));
            Assert.AreEqual(50, c.Wallet.Balance);
            var reloaded = Fresh(0, store).Collection;
            Assert.AreEqual(UnlockResult.AlreadyOwned, reloaded.TryUnlock("finish.b"));
            Assert.AreEqual(50, reloaded.Wallet.Balance);
            Assert.IsTrue(reloaded.IsOwned("finish.b"));
        }

        private sealed class FailingStore : IKeyValueStore
        {
            public readonly MemoryStore Inner = new MemoryStore();
            public bool Fail;
            public string GetString(string key, string fallback) => Inner.GetString(key, fallback);
            public void SetString(string key, string value)
            {
                if (Fail) throw new IOException("disk full");
                Inner.SetString(key, value);
            }
            public void Save() => Inner.Save();
        }

        [Test]
        public void FailedSave_LeavesNoPartialTransaction()
        {
            var store = new FailingStore();
            var (c, _) = Fresh(100, store);
            store.Fail = true;
            Assert.AreEqual(UnlockResult.SaveFailed, c.TryUnlock("finish.b"));
            Assert.AreEqual(100, c.Wallet.Balance, "Dust is not spent in memory");
            Assert.IsFalse(c.IsOwned("finish.b"));
            store.Fail = false;
            var reloaded = new DreamDustWallet(store);
            Assert.AreEqual(100, reloaded.Balance);
            Assert.IsFalse(reloaded.Owns("finish.b"));
        }

        [Test]
        public void TornWrite_RecoversTheLastCompleteState()
        {
            var store = new MemoryStore();
            var (c, _) = Fresh(100, store);
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("finish.b"));
            string good = store.GetString(DreamDustWallet.StoreKey, null);
            // Simulate a crash halfway through the next purchase's write of the main record.
            store.SetString(DreamDustWallet.StoreKey, good.Substring(0, good.Length / 2));
            var recovered = new DreamDustWallet(store);
            Assert.IsTrue(recovered.RecoveredFromBackup);
            Assert.AreEqual(50, recovered.Balance);
            Assert.IsTrue(recovered.Owns("finish.b"), "balance and ownership come from the same complete record");
            Assert.AreEqual(good, store.GetString(DreamDustWallet.StoreKey, null), "main record repaired");
            Assert.IsNotNull(store.GetString(DreamDustWallet.StoreKey + ".corrupt", null), "torn data kept for inspection");
        }

        // ------------------------------------------------------------------ milestones & equipping

        [Test]
        public void Milestones_GrantOnce_WithoutTouchingDust()
        {
            var (c, store) = Fresh(10);
            Assert.AreEqual(CosmeticState.MilestoneOnly, c.StateOf(c.Catalog.Find("accessory.leaf")));
            Assert.AreEqual(0, c.UpdateProgress(new ProgressSnapshot { LevelsCompleted = 4 }).Count);
            var granted = c.UpdateProgress(new ProgressSnapshot { LevelsCompleted = 5 });
            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual("accessory.leaf", granted[0].Id);
            Assert.AreEqual(0, c.UpdateProgress(new ProgressSnapshot { LevelsCompleted = 9 }).Count, "no duplicate grant");
            Assert.AreEqual(10, c.Wallet.Balance);
            var reloaded = Fresh(0, store).Collection;
            Assert.IsTrue(reloaded.IsOwned("accessory.leaf"), "milestone ownership is saved, not recomputed from progress");
            Assert.AreEqual("Complete 5 levels", CosmeticCollection.ConditionText(c.Catalog.Find("accessory.leaf")));
        }

        [Test]
        public void Equip_RequiresOwnership_AndKeepsTheLookCompatible()
        {
            var (c, _) = Fresh(100);
            Assert.IsFalse(c.Equip("finish.b"), "cannot equip before unlocking");
            Assert.IsFalse(c.Equip("finish.unknown"));
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("accessory.cap"));
            Assert.IsTrue(c.Equip("accessory.cap"));
            Assert.AreEqual(CosmeticState.Equipped, c.StateOf(c.Catalog.Find("accessory.cap")));
            Assert.AreEqual(UnlockResult.Unlocked, c.TryUnlock("body.b"));
            Assert.IsTrue(c.Equip("body.b"));
            Assert.AreEqual("accessory.none", c.Equipped.Get(CosmeticCategory.Accessory), "cap only fits body.a");
            Assert.IsFalse(c.Equip("accessory.cap"), "incompatible with the equipped style");
        }
    }
}
