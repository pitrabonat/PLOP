using System.Collections.Generic;
using Plop.Core;
using Plop.Meta;
using Plop.Meta.Ads;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Sprint 04 advertising glue: the interstitial natural break after Next Level, the voluntary rewarded hint and
    /// bonus Dream Dust, consent start from the main menu, and the Settings ▸ Privacy Choices entry. Every path
    /// continues without an ad when none is ready; puzzle rules, solver and level data are never involved.
    /// </summary>
    public sealed partial class PlopGameController
    {
        /// <summary>Replaces the ad coordinator (startup and tests). The previous one is disposed.</summary>
        internal void SetAds(PlopAds ads)
        {
            if (_ads != null)
            {
                _ads.StateChanged -= RefreshAdUi;
                if (!ReferenceEquals(_ads, ads)) _ads.Dispose();
            }
            _ads = ads;
            if (_ads == null) return;
            _ads.StateChanged += RefreshAdUi;
            _ads.InterstitialWanted = InterstitialWanted;
            RefreshAdUi();
        }

        /// <summary>Preload an interstitial only once the player is past the ad-free opening levels.</summary>
        private bool InterstitialWanted() =>
            _progress != null && !_playingDaily && _progress.FrontierIndex() + 1 > _ads.Config.AdFreeLevels;

        private bool ActiveGameplay =>
            _engine.HasLevel && !_engine.IsCompleted && !_debugPreview && !_suspended &&
            !(_menu != null && _menu.IsOpen) && !(_studio != null && _studio.IsOpen) && !_hud.BlocksGameplayInput &&
            Time.unscaledTime - _lastActivity < ActiveGameplayWindow;

        private void TickAds()
        {
            if (_ads == null) return;
            // Consent and SDK start only from the main menu after the splash, never in the middle of a level.
            if (!_ads.Started && _ads.Enabled && !_suspended && _splash == null && _menu != null && _menu.IsOpen)
                _ads.Start();
            _ads.Tick(Time.unscaledDeltaTime, ActiveGameplay, _suspended);
        }

        /// <summary>Opens the level chosen with Next Level once an interstitial closed while the app was not yet resumed.</summary>
        private void FinishPendingAdvance()
        {
            int next = _pendingAdvance;
            _pendingAdvance = -1;
            _advancing = false;
            // If this fails (for example Settings is open), the completion card's Next Level button is still there.
            if (_engine.IsCompleted && _index != next) TrySelectLevel(next);
        }

        /// <summary>Menu bonus and Settings privacy entry reflect the current consent, cap and readiness.</summary>
        private void RefreshAdUi()
        {
            if (_ads == null || _menu == null || _hud == null) return;
            var now = Clock.LocalNow;
            bool offer = _ads.CanOfferDust(now) && _ads.Started && _ads.Service.CanRequestAds;
            _menu.SetBonusOffer(offer, _ads.RewardedReady, _ads.Config.RewardedDustAmount, _ads.Config.RewardedDustDailyCap);
            _hud.SetPrivacyChoicesVisible(_ads.PrivacyOptionsRequired);
        }

        /// <summary>Main menu ▸ Bonus Dream Dust ▸ Watch Ad. Pays only on the SDK reward event.</summary>
        private void WatchBonusDust()
        {
            if (_ads == null) return;
            bool started = _ads.ShowRewardedDust(Clock.LocalNow, result =>
            {
                if (this == null) return;
                if (result.Rewarded && result.DustGranted > 0)
                {
                    _menu.ShowToast(Loc.F("Thanks! +{0} Dream Dust", result.DustGranted));
                    _audio.PlayDust();
                }
                else _menu.ShowToast(Loc.T(result.Shown ? "No reward this time. The ad did not finish." : "Ad unavailable"));
                RefreshMenu();
            });
            if (!started) { _menu.ShowToast(Loc.T("Ad unavailable")); RefreshAdUi(); }
        }

        // ------------------------------------------------------------------ rewarded hint

        /// <summary>Spends a saved free hint (earned from an earlier ad) on this board, if any.</summary>
        private bool TryUseSavedFreeHint(string levelId, ulong stateKey) =>
            _ads != null && _ads.Rewards.HintEntitlements > 0 && _ads.Rewards.TryRedeemHint(levelId, stateKey);

        /// <summary>
        /// Offer the ad choice only when an ad is ready, this board's hint is not already paid, and the existing
        /// grace (first hint free after repeated tries) would not already cover a player without enough Dust.
        /// </summary>
        private bool ShouldOfferRewardedHint(string levelId, ulong stateKey)
        {
            if (_ads == null || !_ads.CanOfferHint || !_ads.RewardedReady) return false;
            if (_wallet.HasSpent(RewardRules.HintKey(levelId, stateKey))) return false;
            bool affordable = _wallet.Balance >= RewardRules.HintCost;
            return affordable || !HintPolicy.GraceAvailable(_wallet, levelId, _attempts);
        }

        private void OfferRewardedHint(LevelDefinition level, GameState state, ulong stateKey, Direction direction)
        {
            ShowHint(null);
            bool affordable = _wallet.Balance >= RewardRules.HintCost;
            var options = new List<AdOfferOption>
            {
                new AdOfferOption(Loc.F("Use {0} Dream Dust", RewardRules.HintCost),
                    () => ChargeDustForHint(level, state, stateKey, direction), affordable),
                new AdOfferOption(Loc.T("Watch Ad · Free Hint"),
                    () => WatchAdForHint(level, state, stateKey, direction), true, isAd: true)
            };
            string message = Loc.F("You have {0} Dream Dust. Or watch a short ad for one free hint.", _wallet.Balance);
            _hud.ShowAdOffer(Loc.T("Get a hint"), message, options, () => MarkActive());
        }

        private bool BoardUnchanged(LevelDefinition level, GameState state) =>
            this != null && isActiveAndEnabled && _engine.HasLevel && !_engine.IsCompleted &&
            _engine.Level == level && _engine.CurrentState.Equals(state);

        private void ChargeDustForHint(LevelDefinition level, GameState state, ulong stateKey, Direction direction)
        {
            MarkActive();
            if (!BoardUnchanged(level, state)) { ShowHint(Loc.T("The board changed. Ask for a new hint.")); return; }
            var spend = HintPolicy.Charge(_wallet, level.Id, stateKey, _attempts);
            if (spend == HintCharge.Insufficient)
            {
                ShowHint(Loc.F("A hint costs {0} Dream Dust. You have {1}.", RewardRules.HintCost, _wallet.Balance));
                return;
            }
            string charged = spend == HintCharge.Charged
                ? "\n" + Loc.F("−{0} Dream Dust · {1} left", RewardRules.HintCost, _wallet.Balance)
                : spend == HintCharge.Grace ? "\n" + Loc.T("This one is on the house.") : "";
            DeliverHint(direction, charged);
        }

        private void WatchAdForHint(LevelDefinition level, GameState state, ulong stateKey, Direction direction)
        {
            bool started = _ads.ShowRewardedHint(level.Id, stateKey, result =>
            {
                if (this == null) return;
                MarkActive();
                if (!result.Rewarded)
                {
                    ShowHint(Loc.T(result.Shown ? "No reward this time. The ad did not finish." : "Ad unavailable"));
                    return;
                }
                // Never show a stale route: the earned hint is used only on the exact board it was found for.
                if (BoardUnchanged(level, state) && _ads.Rewards.TryRedeemHint(level.Id, stateKey))
                    DeliverHint(direction, "\n" + Loc.T("Free hint used."));
                else
                    ShowHint(Loc.T("Free hint saved for your next hint."));
            });
            if (!started) ShowHint(Loc.T("Ad unavailable"));
        }
    }
}
