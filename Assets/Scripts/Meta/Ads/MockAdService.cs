using System;
using System.Collections.Generic;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Developer-only simulated advertising for automated tests and UI work: no SDK, no network, no real ads.
    /// Outcomes are scripted. With <see cref="Manual"/> the presentation stays "on screen" until a test calls
    /// <see cref="Finish"/>, which also lets tests deliver the reward before or after the close, twice, or not at all.
    /// </summary>
    public sealed class MockAdService : IAdService
    {
        public AdMode Mode => AdMode.Mock;
        public bool CanRequestAds { get; private set; }
        public bool PrivacyOptionsRequired { get; set; }
        public bool IsShowing { get; private set; }
        public bool Initialized { get; private set; }

        // ---- script
        public bool ConsentAllowsAds = true;
        public bool InitializationFails;
        public bool LoadSucceeds = true;
        public bool ShowFails;
        /// <summary>Rewarded: whether the simulated viewer watches to the end (the reward event fires).</summary>
        public bool WatchToEnd = true;
        /// <summary>Simulates a misbehaving SDK that repeats its callbacks.</summary>
        public bool DuplicateCallbacks;
        /// <summary>Simulates the reward event arriving after the close (allowed by the SDK on some platforms).</summary>
        public bool RewardAfterClose;
        /// <summary>When true, a shown ad stays open until <see cref="Finish"/> is called.</summary>
        public bool Manual;

        // ---- observations
        public int InitializeCalls, LoadRequests, InterstitialsShown, RewardedShown, PrivacyFormsShown;
        public readonly List<string> Log = new List<string>();

        private readonly HashSet<AdFormat> _loaded = new HashSet<AdFormat>();
        private Action _pendingEarned;
        private Action<AdShowOutcome> _pendingFinished;
        private bool _pendingRewarded;

        public void Initialize(Action<bool> done)
        {
            InitializeCalls++;
            Initialized = true;
            CanRequestAds = ConsentAllowsAds && !InitializationFails;
            Log.Add("initialize:" + CanRequestAds);
            done?.Invoke(CanRequestAds);
        }

        public bool IsReady(AdFormat format) => CanRequestAds && _loaded.Contains(format);

        public void Load(AdFormat format, Action<bool> done)
        {
            if (!CanRequestAds) { done?.Invoke(false); return; }
            LoadRequests++;
            Log.Add("load:" + format);
            if (LoadSucceeds) _loaded.Add(format);
            done?.Invoke(LoadSucceeds);
        }

        /// <summary>Test helper: drop the loaded ad (expiry, consent change).</summary>
        public void Unload(AdFormat format) => _loaded.Remove(format);

        public bool ShowInterstitial(Action opened, Action<AdShowOutcome> finished)
            => Present(AdFormat.Interstitial, opened, null, finished);

        public bool ShowRewarded(Action opened, Action earned, Action<AdShowOutcome> finished)
            => Present(AdFormat.Rewarded, opened, earned, finished);

        private bool Present(AdFormat format, Action opened, Action earned, Action<AdShowOutcome> finished)
        {
            if (!IsReady(format) || IsShowing) return false;
            _loaded.Remove(format); // ads are single-use
            Log.Add("show:" + format);
            if (ShowFails)
            {
                finished?.Invoke(AdShowOutcome.FailedToShow);
                if (DuplicateCallbacks) finished?.Invoke(AdShowOutcome.FailedToShow);
                return true;
            }
            IsShowing = true;
            if (format == AdFormat.Interstitial) InterstitialsShown++; else RewardedShown++;
            opened?.Invoke();
            _pendingEarned = earned;
            _pendingFinished = finished;
            _pendingRewarded = format == AdFormat.Rewarded;
            if (!Manual) Finish();
            return true;
        }

        /// <summary>Closes the simulated ad: reward event (when watched to the end), then the close callback.</summary>
        public void Finish()
        {
            if (!IsShowing) return;
            var earned = _pendingEarned;
            var finished = _pendingFinished;
            bool rewarded = _pendingRewarded && WatchToEnd;
            _pendingEarned = null;
            _pendingFinished = null;
            IsShowing = false;
            if (rewarded && !RewardAfterClose) { earned?.Invoke(); if (DuplicateCallbacks) earned?.Invoke(); }
            finished?.Invoke(AdShowOutcome.Closed);
            if (DuplicateCallbacks) finished?.Invoke(AdShowOutcome.Closed);
            if (rewarded && RewardAfterClose) { earned?.Invoke(); if (DuplicateCallbacks) earned?.Invoke(); }
        }

        /// <summary>The last mute state the game passed before a presentation.</summary>
        public bool Muted { get; private set; }

        public void SetMuted(bool muted) => Muted = muted;

        public void ShowPrivacyOptions(Action done)
        {
            PrivacyFormsShown++;
            done?.Invoke();
        }

        public void Dispose()
        {
            _loaded.Clear();
            IsShowing = false;
            CanRequestAds = false;
        }
    }
}
