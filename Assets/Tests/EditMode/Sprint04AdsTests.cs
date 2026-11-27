using System;
using System.Linq;
using NUnit.Framework;
using Plop.Meta;
using Plop.Meta.Ads;

namespace Plop.Tests
{
    /// <summary>
    /// Sprint 04 advertising rules that need no engine: configuration floors and the production lock, the interstitial
    /// frequency policy, reward transactions (idempotency, caps, profile binding, hint entitlements), the mock service
    /// and the house-ad catalog. Runs in Unity EditMode and in the .NET suite.
    /// </summary>
    public class Sprint04AdsTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

        static AdFrequencyPolicy ReadyPolicy(MemoryStore store, AdConfiguration config)
        {
            var policy = new AdFrequencyPolicy(store, config);
            for (int i = 0; i < 400; i++) policy.AddActiveSeconds(1);
            for (int i = 0; i < config.LevelsBetweenInterstitials; i++) policy.RecordNewCompletion();
            return policy;
        }

        static InterstitialContext Level(int number, bool newly = true, bool tutorial = false) =>
            new InterstitialContext { CompletedLevelNumber = number, NewlyCompleted = newly, Tutorial = tutorial };

        // ------------------------------------------------------------------ configuration

        [Test]
        public void Config_DefaultsAreDisabledAndConservative()
        {
            var c = AdConfiguration.Default;
            Assert.AreEqual(AdMode.Disabled, c.Mode);
            Assert.AreEqual(AdMode.Disabled, c.EffectiveMode());
            Assert.IsFalse(c.ServesAds);
            Assert.IsFalse(c.HouseAdsEnabled);
            Assert.AreEqual(AudienceTreatment.Unconfirmed, c.Audience);
            Assert.AreEqual("G", c.MaxAdContentRating);
            Assert.GreaterOrEqual(c.AdFreeLevels, 10);
            Assert.GreaterOrEqual(c.LevelsBetweenInterstitials, 4);
            Assert.GreaterOrEqual(c.MinActiveSecondsBetween, 180);
            Assert.LessOrEqual(c.MaxInterstitialsPerWindow, 4);
            Assert.GreaterOrEqual(c.WindowMinutes, 30);
            Assert.GreaterOrEqual(c.NewPlayerProtectionSeconds, 300);
            Assert.DoesNotThrow(c.Validate);
            foreach (var id in new[] { c.ProductionAndroidAppId, c.ProductionIosAppId, c.ProductionAndroidInterstitial,
                         c.ProductionAndroidRewarded, c.ProductionIosInterstitial, c.ProductionIosRewarded })
                Assert.AreEqual("", id, "no production ID may be committed in Sprint 04");
        }

        [Test]
        public void Config_ProductionIsLockedEvenWithIdsAndAudience()
        {
            Assert.IsFalse(AdConfiguration.ProductionUnlocked);
            var c = new AdConfiguration
            {
                Mode = AdMode.Production,
                Audience = AudienceTreatment.GeneralAudience,
                ProductionAndroidAppId = "ca-app-pub-1111111111111111~1111111111",
                ProductionIosAppId = "ca-app-pub-1111111111111111~2222222222",
                ProductionAndroidInterstitial = "ca-app-pub-1111111111111111/1",
                ProductionAndroidRewarded = "ca-app-pub-1111111111111111/2",
                ProductionIosInterstitial = "ca-app-pub-1111111111111111/3",
                ProductionIosRewarded = "ca-app-pub-1111111111111111/4"
            };
            Assert.AreEqual(AdMode.Disabled, c.EffectiveMode(out string reason));
            StringAssert.Contains("locked", reason);
            Assert.IsFalse(c.ServesAds);
        }

        [Test]
        public void Config_RejectsAggressiveValues()
        {
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { AdFreeLevels = 5 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { LevelsBetweenInterstitials = 2 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { MinActiveSecondsBetween = 60 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { MaxInterstitialsPerWindow = 6 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { WindowMinutes = 10 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { NewPlayerProtectionSeconds = 60 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { RewardedDustAmount = 100 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { RewardedDustDailyCap = 10 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new AdConfiguration { MaxAdContentRating = "MA" }.Validate());
        }

        [Test]
        public void TestUnitIds_AreGoogleOfficialTestIds()
        {
            Assert.AreEqual("ca-app-pub-3940256099942544/1033173712", AdUnitIds.TestUnit(AdFormat.Interstitial, false));
            Assert.AreEqual("ca-app-pub-3940256099942544/5224354917", AdUnitIds.TestUnit(AdFormat.Rewarded, false));
            Assert.AreEqual("ca-app-pub-3940256099942544/4411468910", AdUnitIds.TestUnit(AdFormat.Interstitial, true));
            Assert.AreEqual("ca-app-pub-3940256099942544/1712485313", AdUnitIds.TestUnit(AdFormat.Rewarded, true));
            Assert.IsTrue(AdUnitIds.IsGoogleTestId(AdUnitIds.TestAndroidAppId));
            Assert.IsFalse(AdUnitIds.IsGoogleTestId("ca-app-pub-1111111111111111/1"));
        }

        [Test]
        public void Registry_FallsBackToDisabledWithoutSdk()
        {
            var previous = AdServiceRegistry.SdkFactory;
            try
            {
                AdServiceRegistry.SdkFactory = null;
                var service = AdServiceRegistry.Create(new AdConfiguration { Mode = AdMode.Test }, out string note);
                Assert.AreEqual(AdMode.Disabled, service.Mode);
                Assert.IsNotEmpty(note);
                Assert.AreEqual(AdMode.Mock, AdServiceRegistry.Create(new AdConfiguration { Mode = AdMode.Mock }, out _).Mode);
                Assert.AreEqual(AdMode.Disabled, AdServiceRegistry.Create(new AdConfiguration { Mode = AdMode.Production }, out _).Mode);
            }
            finally { AdServiceRegistry.SdkFactory = previous; }
        }

        // ------------------------------------------------------------------ interstitial frequency policy

        [Test]
        public void Policy_EligibleOnlyWhenEveryConditionHolds()
        {
            var config = AdConfiguration.Default;
            var policy = ReadyPolicy(new MemoryStore(), config);
            Assert.IsTrue(policy.Evaluate(Level(20), T0).Eligible);
            Assert.AreEqual("ad-free opening levels", policy.Evaluate(Level(10), T0).Reason);
            Assert.AreEqual("replayed level", policy.Evaluate(Level(20, newly: false), T0).Reason);
            Assert.AreEqual("tutorial", policy.Evaluate(Level(20, tutorial: true), T0).Reason);
            config.InterstitialsEnabled = false;
            Assert.AreEqual("interstitials disabled", policy.Evaluate(Level(20), T0).Reason);
        }

        [Test]
        public void Policy_NewPlayerProtection_FirstFiveMinutes()
        {
            var config = AdConfiguration.Default;
            var policy = new AdFrequencyPolicy(new MemoryStore(), config);
            for (int i = 0; i < 10; i++) policy.RecordNewCompletion();
            for (int i = 0; i < 299; i++) policy.AddActiveSeconds(1);
            Assert.AreEqual("new-player protection", policy.Evaluate(Level(30), T0).Reason);
            policy.AddActiveSeconds(1);
            Assert.IsTrue(policy.Evaluate(Level(30), T0).Eligible);
        }

        [Test]
        public void Policy_ActiveSecondsAreClampedPerCall()
        {
            var policy = new AdFrequencyPolicy(new MemoryStore(), AdConfiguration.Default);
            policy.AddActiveSeconds(3600);
            policy.AddActiveSeconds(-5);
            policy.AddActiveSeconds(double.NaN);
            Assert.AreEqual(1.0, policy.TotalActiveSeconds, 1e-9, "a stalled frame never counts as minutes of play");
        }

        [Test]
        public void Policy_SpacingNeedsFourLevelsAndThreeMinutesAfterAnAd()
        {
            var config = AdConfiguration.Default;
            var policy = ReadyPolicy(new MemoryStore(), config);
            policy.RecordInterstitialShown(T0);
            for (int i = 0; i < 3; i++) policy.RecordNewCompletion();
            for (int i = 0; i < 400; i++) policy.AddActiveSeconds(1);
            Assert.AreEqual("too few new levels", policy.Evaluate(Level(25), T0.AddMinutes(10)).Reason);
            policy.RecordNewCompletion();
            Assert.IsTrue(policy.Evaluate(Level(26), T0.AddMinutes(10)).Eligible);

            policy.RecordInterstitialShown(T0.AddMinutes(10));
            for (int i = 0; i < 4; i++) policy.RecordNewCompletion();
            for (int i = 0; i < 179; i++) policy.AddActiveSeconds(1);
            Assert.AreEqual("too little play time", policy.Evaluate(Level(30), T0.AddMinutes(20)).Reason);
            policy.AddActiveSeconds(1);
            Assert.IsTrue(policy.Evaluate(Level(30), T0.AddMinutes(20)).Eligible);
        }

        [Test]
        public void Policy_RollingCap_FourPerThirtyMinutes()
        {
            var config = AdConfiguration.Default;
            var policy = ReadyPolicy(new MemoryStore(), config);
            for (int n = 0; n < 4; n++)
            {
                policy.RecordInterstitialShown(T0.AddMinutes(n));
                for (int i = 0; i < 4; i++) policy.RecordNewCompletion();
                for (int i = 0; i < 200; i++) policy.AddActiveSeconds(1);
            }
            Assert.AreEqual(4, policy.ImpressionsInWindow(T0.AddMinutes(5)));
            Assert.AreEqual("rolling cap reached", policy.Evaluate(Level(40), T0.AddMinutes(5)).Reason);
            Assert.AreEqual("rolling cap reached", policy.Evaluate(Level(40), T0.AddMinutes(29)).Reason);
            Assert.IsTrue(policy.Evaluate(Level(40), T0.AddMinutes(31)).Eligible, "the first impression left the window");
        }

        [Test]
        public void Policy_ClockMovedBack_StillCountsImpressions()
        {
            var policy = ReadyPolicy(new MemoryStore(), AdConfiguration.Default);
            for (int n = 0; n < 4; n++) policy.RecordInterstitialShown(T0);
            Assert.AreEqual(4, policy.ImpressionsInWindow(T0.AddHours(-3)), "turning the clock back cannot unlock more ads");
        }

        [Test]
        public void Policy_NoInterstitialRightAfterRewarded()
        {
            var config = AdConfiguration.Default;
            var policy = ReadyPolicy(new MemoryStore(), config);
            policy.RecordRewardedShown(T0);
            Assert.AreEqual("just after a rewarded ad", policy.Evaluate(Level(20), T0.AddMinutes(10)).Reason, "no completion since");
            policy.RecordNewCompletion();
            Assert.AreEqual("just after a rewarded ad", policy.Evaluate(Level(21), T0.AddSeconds(60)).Reason, "cooldown");
            Assert.IsTrue(policy.Evaluate(Level(21), T0.AddSeconds(config.RewardedCooldownSeconds + 1)).Eligible);
        }

        [Test]
        public void Policy_PersistsWithoutIdentifiers_AndSurvivesRelaunch()
        {
            var store = new MemoryStore();
            var config = AdConfiguration.Default;
            var policy = ReadyPolicy(store, config);
            policy.RecordInterstitialShown(T0);
            policy.RecordNewCompletion();
            policy.AddActiveSeconds(1);
            policy.Save();
            string json = store.Values[AdFrequencyPolicy.StoreKey];
            foreach (var forbidden in new[] { "id\"", "device", "advert", "gaid", "idfa", "user" })
                StringAssert.DoesNotContain(forbidden, json.ToLowerInvariant());

            var again = new AdFrequencyPolicy(store, config);
            Assert.AreEqual(1, again.InterstitialsShown);
            Assert.AreEqual(1, again.CompletionsSinceInterstitial);
            Assert.AreEqual(1, again.ImpressionsInWindow(T0.AddMinutes(1)));
            Assert.AreEqual(policy.TotalActiveSeconds, again.TotalActiveSeconds, 0.11);
        }

        [Test]
        public void Policy_CorruptRecordIsConservative()
        {
            var store = new MemoryStore();
            store.SetString(AdFrequencyPolicy.StoreKey, "{not json");
            var policy = new AdFrequencyPolicy(store, AdConfiguration.Default);
            Assert.IsFalse(policy.Evaluate(Level(50), T0).Eligible);
            Assert.AreEqual(0, policy.CompletionsSinceInterstitial);
            Assert.AreEqual(0, policy.TotalActiveSeconds);
        }

        // ------------------------------------------------------------------ reward transactions

        static readonly DateTime Today = new DateTime(2026, 11, 27, 9, 0, 0);

        [Test]
        public void Dust_PaysOnlyOnRewardEvent_AndOnlyOnce()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            var rewards = new RewardTransactionService(wallet, AdConfiguration.Default);
            var tx = rewards.BeginDust(Today);
            Assert.IsNotNull(tx);
            rewards.OnClosed(tx);
            Assert.AreEqual(0, wallet.Balance, "closing without the reward event pays nothing");

            var tx2 = rewards.BeginDust(Today);
            Assert.IsTrue(rewards.OnRewardEarned(tx2));
            Assert.IsFalse(rewards.OnRewardEarned(tx2), "duplicate callback");
            rewards.OnClosed(tx2);
            Assert.AreEqual(AdConfiguration.Default.RewardedDustAmount, wallet.Balance);
        }

        [Test]
        public void Dust_DailyCap_ResetsNextLocalDay()
        {
            var config = AdConfiguration.Default;
            var wallet = new DreamDustWallet(new MemoryStore());
            var rewards = new RewardTransactionService(wallet, config);
            for (int i = 0; i < config.RewardedDustDailyCap; i++) Assert.IsTrue(rewards.OnRewardEarned(rewards.BeginDust(Today)));
            Assert.IsFalse(rewards.CanOfferDust(Today));
            Assert.IsNull(rewards.BeginDust(Today));
            Assert.AreEqual(config.RewardedDustDailyCap * config.RewardedDustAmount, wallet.Balance);
            Assert.IsTrue(rewards.CanOfferDust(Today.AddDays(1)));
        }

        [Test]
        public void Dust_SurvivesRelaunch_AndNeverCrossesProfiles()
        {
            var storeA = new MemoryStore();
            var walletA = new DreamDustWallet(storeA);
            var rewards = new RewardTransactionService(walletA, AdConfiguration.Default);
            var pending = rewards.BeginDust(Today);
            var walletB = new DreamDustWallet(new MemoryStore());
            rewards.UseWallet(walletB);
            Assert.IsFalse(rewards.OnRewardEarned(pending), "a reward begun on profile A never pays profile B");
            Assert.AreEqual(0, walletA.Balance);
            Assert.AreEqual(0, walletB.Balance);

            rewards.UseWallet(walletA);
            Assert.IsTrue(rewards.OnRewardEarned(rewards.BeginDust(Today)));
            Assert.AreEqual(AdConfiguration.Default.RewardedDustAmount, new DreamDustWallet(storeA).Balance, "atomic save");
            Assert.AreEqual(1, new RewardTransactionService(new DreamDustWallet(storeA), AdConfiguration.Default).DustAdsToday(Today));
        }

        [Test]
        public void HintEntitlement_RedeemsOncePerBoard_AndPersists()
        {
            var store = new MemoryStore();
            var wallet = new DreamDustWallet(store);
            var rewards = new RewardTransactionService(wallet, AdConfiguration.Default);
            var tx = rewards.BeginHint("L42", 7);
            Assert.IsTrue(rewards.OnRewardEarned(tx));
            Assert.AreEqual(0, wallet.Balance, "a hint reward is not Dust");
            Assert.AreEqual(1, rewards.HintEntitlements);

            var relaunched = new RewardTransactionService(new DreamDustWallet(store), AdConfiguration.Default);
            Assert.AreEqual(1, relaunched.HintEntitlements, "an earned hint survives a restart");
            Assert.IsTrue(relaunched.TryRedeemHint("L42", 9), "board changed: the entitlement applies to the next hint");
            Assert.AreEqual(0, relaunched.HintEntitlements);
            Assert.IsFalse(relaunched.TryRedeemHint("L42", 10), "only one hint per reward");
            Assert.AreEqual(HintCharge.AlreadyPaid, HintPolicy.Charge(relaunched.Wallet, "L42", 9, 0), "redeemed board is paid");
        }

        [Test]
        public void HintEntitlement_NotSpentOnAlreadyPaidBoard()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            wallet.Grant("seed", 40);
            Assert.AreEqual(HintCharge.Charged, HintPolicy.Charge(wallet, "L", 3, 0));
            var rewards = new RewardTransactionService(wallet, AdConfiguration.Default);
            rewards.OnRewardEarned(rewards.BeginHint("L", 3));
            Assert.IsFalse(rewards.TryRedeemHint("L", 3));
            Assert.AreEqual(1, rewards.HintEntitlements, "kept for a board that still needs paying");
        }

        // ------------------------------------------------------------------ mock service

        [Test]
        public void Mock_NoConsent_NoRequests()
        {
            var mock = new MockAdService { ConsentAllowsAds = false };
            bool? ok = null;
            mock.Initialize(r => ok = r);
            Assert.AreEqual(false, ok);
            mock.Load(AdFormat.Rewarded, _ => { });
            Assert.AreEqual(0, mock.LoadRequests);
            Assert.IsFalse(mock.ShowRewarded(null, null, null));
        }

        [Test]
        public void Mock_AdsAreSingleUse_AndShowFailureReportsOnce()
        {
            var mock = new MockAdService();
            mock.Initialize(null);
            mock.Load(AdFormat.Interstitial, null);
            int finished = 0;
            Assert.IsTrue(mock.ShowInterstitial(null, _ => finished++));
            Assert.AreEqual(1, finished);
            Assert.IsFalse(mock.IsReady(AdFormat.Interstitial));
            mock.ShowFails = true;
            mock.Load(AdFormat.Interstitial, null);
            AdShowOutcome? outcome = null;
            mock.ShowInterstitial(null, o => outcome = o);
            Assert.AreEqual(AdShowOutcome.FailedToShow, outcome);
            Assert.AreEqual(1, mock.InterstitialsShown, "a failed show is not a presentation");
        }

        // ------------------------------------------------------------------ house ads

        [Test]
        public void HouseAds_EmptyAndDisabledByDefault()
        {
            Assert.AreEqual(0, HouseAdCatalog.Empty.Visible(new AdConfiguration { HouseAdsEnabled = true }).Count);
            var catalog = HouseAdCatalog.Parse("{\"enabled\":true,\"items\":[" +
                "{\"id\":\"a\",\"title\":\"A\",\"androidStoreUrl\":\"https://play.google.com/store/apps/details?id=x\"}," +
                "{\"id\":\"b\",\"title\":\"B\",\"androidStoreUrl\":\"http://evil.example/x\"}]}");
            Assert.AreEqual(1, catalog.Items.Count, "only https store links");
            Assert.AreEqual(0, catalog.Visible(AdConfiguration.Default).Count, "placement off by default");
            Assert.AreEqual(1, catalog.Visible(new AdConfiguration { HouseAdsEnabled = true }).Count);
            Assert.IsFalse(HouseAdCatalog.Parse("{broken").Enabled);
        }
    }
}
