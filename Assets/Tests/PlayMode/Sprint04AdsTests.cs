using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Meta;
using Plop.Meta.Ads;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Sprint 04 advertising in the running game, with <see cref="MockAdService"/> (no SDK, no network): the
    /// interstitial natural break, failure and unavailable paths, duplicate callbacks, audio suspension, the rewarded
    /// hint (board unchanged / changed / not watched / Dust instead / declined), bonus Dust with its daily cap,
    /// consent denied, Disabled mode, pause and resume, the privacy entry, the watchdog and SDK isolation.
    /// </summary>
    public class Sprint04AdsTests
    {
        private GameObject _root;
        private PlopGameController _controller;
        private MockAdService _mock;
        private PlopAds _ads;
        private MemoryStore _adStore;

        private static LevelDefinition Level(string id)
        {
            var l = LevelText.Parse(1, id, "R...r", ".....");
            return l.WithOptimalMoves(BfsSolver.Solve(l).SolutionLength);
        }

        private static List<LevelDefinition> Levels(int count) =>
            Enumerable.Range(0, count).Select(i => Level("ads" + i)).ToList();

        private void Create(int levels, int start, Action<MockAdService> script = null, bool warm = true)
        {
            _root = new GameObject("Sprint04 ads test");
            _controller = _root.AddComponent<PlopGameController>();
            _controller.RememberProgress = false;
            _controller.UseLevels(Levels(levels), start);
            _mock = new MockAdService();
            script?.Invoke(_mock);
            _adStore = new MemoryStore();
            _ads = new PlopAds(AdConfiguration.Default, _mock, _adStore, _controller.Wallet, Audio);
            _controller.SetAds(_ads);
            _ads.Start();
            if (warm)
            {
                for (int i = 0; i < 400; i++) _ads.Policy.AddActiveSeconds(1);
                for (int i = 0; i < 3; i++) _ads.Policy.RecordNewCompletion(); // the level about to be solved is the 4th
            }
        }

        private PlopAudio Audio => _root.GetComponent<PlopAudio>();
        private GameHud Hud => _root.GetComponent<GameHud>();
        private MainMenu Menu => _root.GetComponent<MainMenu>();

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private void Solve()
        {
            foreach (var direction in BfsSolver.Solve(_controller.Engine.Level).Moves) _controller.Move(direction);
            Assert.IsTrue(_controller.Engine.IsCompleted);
        }

        private Button FindButton(string label) =>
            _root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == label);

        private IEnumerator Frames(int n = 2)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // ------------------------------------------------------------------ interstitial

        [UnityTest]
        public IEnumerator Interstitial_OnlyAfterNext_WaitsForClose_AndNeverDuplicatesNavigation()
        {
            Create(20, 14, m => m.Manual = true);
            yield return null;
            int before = _mock.InterstitialsShown;
            Solve();
            yield return Frames();
            Assert.AreEqual(before, _mock.InterstitialsShown, "no ad when the level completes");
            Assert.IsTrue(_mock.IsReady(AdFormat.Interstitial), "preloaded past the ad-free levels");

            _controller.NextLevel();
            Assert.AreEqual(1, _mock.InterstitialsShown);
            Assert.IsTrue(_ads.IsPresenting);
            Assert.IsTrue(Audio.SuspendedForAd, "game audio pauses for the ad");
            Assert.AreEqual(14, _controller.LevelIndex, "the next level waits for the ad to close");
            _controller.NextLevel();
            _controller.Move(Direction.Right);
            Assert.AreEqual(1, _mock.InterstitialsShown, "Next during the ad does nothing");

            _mock.Finish();
            Assert.AreEqual(15, _controller.LevelIndex, "continues to exactly the next level");
            Assert.IsFalse(_ads.IsPresenting);
            Assert.IsFalse(Audio.SuspendedForAd, "audio restored");
            Assert.AreEqual(1, _ads.Policy.InterstitialsShown);
            Assert.AreEqual(0, _ads.Policy.CompletionsSinceInterstitial);
        }

        [UnityTest]
        public IEnumerator Interstitial_ClosedBeforeTheAppResumes_OpensTheNextLevelOnResume()
        {
            // Found on the emulator: the SDK reports the close before Unity regains focus.
            Create(20, 14, m => m.Manual = true);
            yield return null;
            Solve();
            yield return Frames();
            _controller.NextLevel();
            _root.SendMessage("OnApplicationPause", true);
            _mock.Finish();
            Assert.AreEqual(14, _controller.LevelIndex, "nothing changes while the app is in the background");
            _controller.NextLevel();
            _root.SendMessage("OnApplicationPause", false);
            yield return Frames();
            Assert.AreEqual(15, _controller.LevelIndex, "the chosen level opens on resume, exactly once");
            Assert.AreEqual(1, _mock.InterstitialsShown);
            Assert.IsFalse(Audio.SuspendedForAd);
        }

        [UnityTest]
        public IEnumerator Interstitial_NotLoaded_ContinuesImmediately()
        {
            Create(20, 14, m => m.LoadSucceeds = false);
            yield return null;
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.AreEqual(15, _controller.LevelIndex);
            Assert.AreEqual(0, _mock.InterstitialsShown);
            Assert.AreEqual(0, _ads.Policy.InterstitialsShown, "failed requests are not impressions");
        }

        [UnityTest]
        public IEnumerator Interstitial_ShowFailure_WithDuplicateCallbacks_AdvancesOnce()
        {
            Create(20, 14, m => { m.ShowFails = true; m.DuplicateCallbacks = true; });
            yield return null;
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.AreEqual(15, _controller.LevelIndex, "exactly one level forward");
            Assert.IsFalse(_ads.IsPresenting);
            Assert.IsFalse(Audio.SuspendedForAd);
            Assert.AreEqual(0, _ads.Policy.InterstitialsShown);
        }

        [UnityTest]
        public IEnumerator Interstitial_NeverInTheFirstTenLevels()
        {
            Create(20, 4);
            yield return null;
            Solve();
            yield return Frames();
            Assert.IsFalse(_mock.IsReady(AdFormat.Interstitial), "nothing is even preloaded");
            _controller.NextLevel();
            Assert.AreEqual(5, _controller.LevelIndex);
            Assert.AreEqual(0, _mock.InterstitialsShown);
        }

        [UnityTest]
        public IEnumerator Interstitial_WatchdogGivesTheGameBack()
        {
            Create(20, 14, m => m.Manual = true);
            yield return null;
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.IsTrue(_ads.IsPresenting);
            _ads.Tick(PlopAds.WatchdogSeconds + 1f, false, false);
            Assert.IsFalse(_ads.IsPresenting);
            Assert.AreEqual(15, _controller.LevelIndex);
            Assert.IsFalse(Audio.SuspendedForAd);
        }

        [UnityTest]
        public IEnumerator Interstitial_RespectsMute()
        {
            Create(20, 14, m => m.Manual = true);
            yield return null;
            Audio.SetMasterMuted(true);
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.IsTrue(_mock.Muted, "a muted game mutes the ad");
            _mock.Finish();
            Assert.IsTrue(Audio.MasterMuted, "mute survives the ad");
            Audio.SetMasterMuted(false);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_NeverShowAnAd()
        {
            Create(20, 14);
            yield return null;
            Solve();
            yield return Frames();
            Assert.IsTrue(_mock.IsReady(AdFormat.Interstitial));
            _root.SendMessage("OnApplicationPause", true);
            yield return null;
            _root.SendMessage("OnApplicationPause", false);
            yield return Frames(3);
            Assert.AreEqual(0, _mock.InterstitialsShown + _mock.RewardedShown, "resume never auto-shows an ad");
            Assert.IsTrue(_adStore.Values.ContainsKey(AdFrequencyPolicy.StoreKey), "frequency record saved on pause");
        }

        // ------------------------------------------------------------------ rewarded hint

        private IEnumerator AskHint()
        {
            var task = _controller.RequestHintAsync();
            yield return new WaitUntil(() => task.IsCompleted);
        }

        [UnityTest]
        public IEnumerator RewardedHint_OfferStatesBothChoices_AdDeliversVerifiedHint()
        {
            Create(10, PlopGameController.FreeHintLevels + 1);
            yield return Frames();
            Assert.IsTrue(_ads.RewardedReady);
            yield return AskHint();
            Assert.IsTrue(Hud.IsAdOfferOpen, "the player chooses");
            var dust = FindButton(Loc.F("Use {0} Dream Dust", RewardRules.HintCost));
            Assert.IsFalse(dust.interactable, "not enough Dust: that option is shown but disabled");
            FindButton(Loc.T("No, thanks"));
            _controller.Move(Direction.Down);
            Assert.AreEqual(0, _controller.Engine.MoveCount, "the card blocks the board");

            FindButton(Loc.T("Watch Ad · Free Hint")).onClick.Invoke();
            Assert.AreEqual(1, _mock.RewardedShown);
            Assert.IsTrue(_controller.HintUsedThisVisit, "verified hint shown");
            StringAssert.Contains(Loc.T("Free hint used."), Hud.HintText);
            Assert.AreEqual(0, _controller.Wallet.Balance, "no Dust spent or granted");
            Assert.AreEqual(0, _ads.Rewards.HintEntitlements);
        }

        [UnityTest]
        public IEnumerator RewardedHint_BoardChangedDuringAd_SavesTheHint()
        {
            Create(10, PlopGameController.FreeHintLevels + 1, m => m.Manual = true);
            yield return Frames();
            yield return AskHint();
            FindButton(Loc.T("Watch Ad · Free Hint")).onClick.Invoke();
            var before = _controller.Engine.GetState();
            _controller.Engine.TryMove(Direction.Down);
            Assert.AreNotEqual(before, _controller.Engine.GetState());
            _mock.Finish();
            Assert.IsFalse(_controller.HintUsedThisVisit, "a stale route is never shown");
            Assert.AreEqual(Loc.T("Free hint saved for your next hint."), Hud.HintText);
            Assert.AreEqual(1, _ads.Rewards.HintEntitlements);

            yield return Frames();
            yield return AskHint();
            Assert.IsFalse(Hud.IsAdOfferOpen, "a saved hint is used without another offer");
            StringAssert.Contains(Loc.T("Free hint used."), Hud.HintText);
            Assert.AreEqual(0, _ads.Rewards.HintEntitlements);
        }

        [UnityTest]
        public IEnumerator RewardedHint_NotWatchedToEnd_GrantsNothing()
        {
            Create(10, PlopGameController.FreeHintLevels + 1, m => m.WatchToEnd = false);
            yield return Frames();
            yield return AskHint();
            FindButton(Loc.T("Watch Ad · Free Hint")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(PlopAds.LateRewardGraceSeconds + 0.4f);
            Assert.AreEqual(Loc.T("No reward this time. The ad did not finish."), Hud.HintText);
            Assert.IsFalse(_controller.HintUsedThisVisit);
            Assert.AreEqual(0, _ads.Rewards.HintEntitlements);
        }

        [UnityTest]
        public IEnumerator RewardedHint_DustOptionKeepsTheExistingPrice()
        {
            Create(10, PlopGameController.FreeHintLevels + 1);
            yield return Frames();
            _controller.Wallet.Grant("test", 40);
            yield return AskHint();
            FindButton(Loc.F("Use {0} Dream Dust", RewardRules.HintCost)).onClick.Invoke();
            Assert.AreEqual(40 - RewardRules.HintCost, _controller.Wallet.Balance);
            Assert.IsTrue(_controller.HintUsedThisVisit);
            Assert.AreEqual(0, _mock.RewardedShown);
        }

        [UnityTest]
        public IEnumerator RewardedHint_DeclineAndBack_DoNothing()
        {
            Create(10, PlopGameController.FreeHintLevels + 1);
            yield return Frames();
            _controller.Wallet.Grant("test", 40);
            yield return AskHint();
            FindButton(Loc.T("No, thanks")).onClick.Invoke();
            Assert.IsFalse(Hud.IsAdOfferOpen);
            Assert.AreEqual(40, _controller.Wallet.Balance);
            Assert.IsFalse(_controller.HintUsedThisVisit);
            yield return AskHint();
            Assert.AreEqual("modal", _controller.HandleBack(), "Back declines the offer");
            Assert.IsFalse(Hud.IsAdOfferOpen);
            Assert.AreEqual(0, _mock.RewardedShown);
        }

        // ------------------------------------------------------------------ bonus Dust

        [UnityTest]
        public IEnumerator BonusDust_VoluntaryCard_PaysOnce_AndStopsAtDailyCap()
        {
            Create(10, 0, m => m.DuplicateCallbacks = true);
            yield return Frames();
            _controller.ShowMenu();
            yield return Frames();
            var config = _ads.Config;
            for (int n = 0; n < config.RewardedDustDailyCap; n++)
            {
                Assert.IsTrue(Menu.BonusReady, "offer " + n);
                FindButton(Loc.F("Watch Ad · +{0} Dream Dust", config.RewardedDustAmount)).onClick.Invoke();
                Assert.IsTrue(Menu.IsBonusCardOpen, "tapping only opens the card");
                Assert.AreEqual(n, _mock.RewardedShown);
                FindButton(Loc.T("Watch Ad")).onClick.Invoke();
                Assert.AreEqual((n + 1) * config.RewardedDustAmount, _controller.Wallet.Balance, "paid exactly once");
                yield return Frames(3); // next ad preloads
            }
            Assert.IsFalse(Menu.BonusVisible, "hidden after today's limit");
            Assert.AreEqual(config.RewardedDustDailyCap * config.RewardedDustAmount, _controller.Wallet.Balance);
        }

        [UnityTest]
        public IEnumerator BonusDust_CardDeclinedWithNoThanksOrBack()
        {
            Create(10, 0);
            yield return Frames();
            _controller.ShowMenu();
            yield return Frames();
            FindButton(Loc.F("Watch Ad · +{0} Dream Dust", _ads.Config.RewardedDustAmount)).onClick.Invoke();
            FindButton(Loc.T("No, thanks")).onClick.Invoke();
            Assert.IsFalse(Menu.IsBonusCardOpen);
            FindButton(Loc.F("Watch Ad · +{0} Dream Dust", _ads.Config.RewardedDustAmount)).onClick.Invoke();
            Assert.AreEqual("modal", _controller.HandleBack());
            Assert.IsFalse(Menu.IsBonusCardOpen);
            Assert.AreEqual(0, _mock.RewardedShown);
            Assert.AreEqual(0, _controller.Wallet.Balance);
        }

        // ------------------------------------------------------------------ consent, disabled, privacy

        [UnityTest]
        public IEnumerator ConsentDenied_NoRequestsNoOffersNoAds()
        {
            Create(20, 14, m => m.ConsentAllowsAds = false);
            yield return Frames();
            Assert.AreEqual(0, _mock.LoadRequests);
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.AreEqual(15, _controller.LevelIndex);
            Assert.AreEqual(0, _mock.LoadRequests, "nothing is requested without consent");
            Assert.AreEqual(0, _mock.InterstitialsShown + _mock.RewardedShown);
            _controller.ShowMenu();
            yield return null;
            Assert.IsFalse(Menu.BonusVisible);
        }

        [UnityTest]
        public IEnumerator DisabledByDefault_NoAdUiNoRequests()
        {
            _root = new GameObject("Sprint04 disabled");
            _controller = _root.AddComponent<PlopGameController>();
            _controller.RememberProgress = false;
            _controller.UseLevels(Levels(20), 14);
            yield return Frames();
            Assert.AreEqual(AdMode.Disabled, _controller.Ads.Mode);
            Assert.IsFalse(_controller.Ads.Enabled);
            Assert.IsInstanceOf<DisabledAdService>(_controller.Ads.Service);
            Solve();
            yield return Frames();
            _controller.NextLevel();
            Assert.AreEqual(15, _controller.LevelIndex);
            Assert.IsFalse(_controller.Ads.IsPresenting);
            _controller.ShowMenu();
            yield return null;
            Assert.IsFalse(Menu.BonusVisible);
            Assert.IsFalse(Hud.PrivacyChoicesVisible);
        }

        [UnityTest]
        public IEnumerator PrivacyChoices_ShownOnlyWhenRequired_OpensTheForm()
        {
            Create(10, 0, m => m.PrivacyOptionsRequired = true);
            yield return Frames();
            _controller.ShowMenu();
            yield return null;
            Assert.IsTrue(Hud.PrivacyChoicesVisible);
            Hud.ShowSettings();
            yield return null;
            FindButton(Loc.T("Privacy Choices")).onClick.Invoke();
            Assert.AreEqual(1, _mock.PrivacyFormsShown);
            Hud.CloseSettings();

            _mock.PrivacyOptionsRequired = false;
            _controller.ShowMenu();
            yield return null;
            Assert.IsFalse(Hud.PrivacyChoicesVisible);
        }

        // ------------------------------------------------------------------ isolation

        [Test]
        public void OnlyTheAdMobAdapterReferencesTheSdk()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var adapter = assemblies.SingleOrDefault(a => a.GetName().Name == "PLOP.Ads.AdMob");
            Assert.IsNotNull(adapter, "the adapter assembly is part of the project");
            Assert.IsTrue(adapter.GetReferencedAssemblies().Any(r => r.Name.StartsWith("GoogleMobileAds")));
            foreach (var asm in assemblies.Where(a => a.GetName().Name.StartsWith("PLOP") && a != adapter))
                foreach (var reference in asm.GetReferencedAssemblies())
                    Assert.IsFalse(reference.Name.StartsWith("GoogleMobileAds"), asm.GetName().Name + " must not use the ad SDK");
        }
    }
}
