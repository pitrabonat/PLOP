using System;
using Plop.Meta;
using Plop.Meta.Ads;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Sprint 04 advertising coordinator. Owns the ad service, the interstitial frequency policy and the reward
    /// transactions; pauses and restores game audio around full-screen ads; blocks input while one is on screen.
    /// Never waits for an ad: when nothing is ready (offline, no fill, no consent, disabled) the game continues at once.
    /// Puzzle rules, solver and level data never see any of this.
    /// </summary>
    public sealed class PlopAds
    {
        public struct RewardResult
        {
            public bool Shown;
            public bool Rewarded;
            public int DustGranted;
            public AdShowOutcome Outcome;
            public AdRewardTransaction Transaction;
        }

        /// <summary>Unity foreground seconds a presentation may last without an SDK callback before it is abandoned.</summary>
        internal const float WatchdogSeconds = 30f;
        /// <summary>How long after a close the coordinator waits for a late reward event before reporting the result.</summary>
        internal const float LateRewardGraceSeconds = 0.75f;

        public AdConfiguration Config { get; }
        public IAdService Service { get; private set; }
        public AdFrequencyPolicy Policy { get; private set; }
        public RewardTransactionService Rewards { get; private set; }
        public string StartupNote { get; }
        public AdMode Mode => Service.Mode;
        public bool Enabled => Service.Mode != AdMode.Disabled;
        /// <summary>
        /// A full-screen ad is on screen or finishing: gameplay input and navigation are blocked. Tracks the
        /// coordinator's own presentation (not the SDK flag), so the watchdog can always give the game back.
        /// </summary>
        public bool IsPresenting => _presenting;
        public bool PrivacyOptionsRequired => Enabled && Service.Initialized && Service.PrivacyOptionsRequired;
        public bool Started { get; private set; }

        /// <summary>Raised when readiness, consent or the privacy entry may have changed (UI refresh).</summary>
        public event Action StateChanged;

        internal Func<DateTime> UtcNow = () => DateTime.UtcNow;
        internal Func<bool> Online = () => Application.internetReachability != NetworkReachability.NotReachable;
        /// <summary>Whether an interstitial could become eligible soon (the controller knows the campaign frontier).</summary>
        internal Func<bool> InterstitialWanted = () => false;

        private readonly PlopAudio _audio;
        private bool _presenting, _audioSuspended;
        private float _presentingForeground;
        private Action<AdShowOutcome> _presentFinish;
        private readonly int[] _loadFailures = new int[2];
        private readonly float[] _nextLoadAt = new float[2];
        private readonly bool[] _loading = new bool[2];
        private float _clock;
        // pending rewarded result (late reward grace)
        private RewardResult _pendingResult;
        private Action<RewardResult> _pendingDone;
        private float _pendingDeadline = -1f;

        public PlopAds(AdConfiguration config, IAdService service, IKeyValueStore store, DreamDustWallet wallet, PlopAudio audio, string note = "")
        {
            Config = config;
            Config.Validate();
            Service = service ?? new DisabledAdService();
            Policy = new AdFrequencyPolicy(store, config);
            Rewards = new RewardTransactionService(wallet, config);
            _audio = audio;
            StartupNote = note;
        }

        /// <summary>Profile switch (tests, tools): new frequency record and wallet; old transactions cannot pay.</summary>
        public void UseProfile(IKeyValueStore store, DreamDustWallet wallet)
        {
            Policy = new AdFrequencyPolicy(store, Config);
            Rewards.UseWallet(wallet);
        }

        /// <summary>
        /// Starts consent and SDK initialisation (never during a level: the controller calls this from the main menu).
        /// Requests nothing in Disabled mode.
        /// </summary>
        public void Start()
        {
            if (Started || !Enabled) return;
            Started = true;
            Service.Initialize(ok =>
            {
                if (ok) RequestLoads();
                StateChanged?.Invoke();
            });
        }

        // ------------------------------------------------------------------ per-frame

        /// <summary>Called every frame. <paramref name="activeGameplay"/>: a board is in play in the foreground.</summary>
        public void Tick(float dt, bool activeGameplay, bool suspended)
        {
            dt = Mathf.Max(0f, dt);
            _clock += dt;
            if (activeGameplay && !suspended && !IsPresenting) Policy.AddActiveSeconds(dt);

            if (_presenting && !suspended)
            {
                _presentingForeground += dt;
                if (_presentingForeground > WatchdogSeconds)
                {
                    Debug.LogWarning("[PLOP][Ads] No callback from the ad SDK; continuing without it.");
                    FinishPresentation(AdShowOutcome.FailedToShow);
                }
            }

            if (_pendingDone != null && _pendingDeadline >= 0f && _clock >= _pendingDeadline) DeliverPending();

            if (Enabled && Service.CanRequestAds && !IsPresenting)
            {
                if (WantsRewarded && !_loading[1] && !Service.IsReady(AdFormat.Rewarded)) TryLoad(AdFormat.Rewarded);
                if (InterstitialWanted() && Config.InterstitialsEnabled && !_loading[0] && !Service.IsReady(AdFormat.Interstitial)) TryLoad(AdFormat.Interstitial);
            }
        }

        /// <summary>The app is leaving the foreground: keep the frequency record on disk.</summary>
        public void OnApplicationPause(bool paused)
        {
            if (paused) Policy.Save();
        }

        private bool WantsRewarded => Config.RewardedDustEnabled || Config.RewardedHintEnabled;

        private void RequestLoads()
        {
            if (WantsRewarded) TryLoad(AdFormat.Rewarded);
            if (InterstitialWanted() && Config.InterstitialsEnabled) TryLoad(AdFormat.Interstitial);
        }

        /// <summary>Bounded retries with exponential backoff; nothing is requested offline or without consent.</summary>
        private void TryLoad(AdFormat format)
        {
            int i = (int)format;
            if (_loading[i] || !Service.CanRequestAds || _clock < _nextLoadAt[i]) return;
            if (_loadFailures[i] >= Config.MaxLoadRetries) return; // stop for this session
            if (!Online()) { _nextLoadAt[i] = _clock + (float)Config.RetryBaseSeconds; return; }
            _loading[i] = true;
            Service.Load(format, ok =>
            {
                _loading[i] = false;
                if (ok) _loadFailures[i] = 0;
                else
                {
                    _loadFailures[i]++;
                    _nextLoadAt[i] = _clock + (float)(Config.RetryBaseSeconds * Math.Pow(2, _loadFailures[i] - 1));
                }
                StateChanged?.Invoke();
            });
        }

        // ------------------------------------------------------------------ interstitial

        /// <summary>
        /// At the natural break after a finished level (the player already pressed Next Level and every reward is
        /// saved). Returns true when an ad is being shown: <paramref name="proceed"/> runs once when it closes or
        /// fails. Returns false when no ad is shown: the caller continues immediately.
        /// </summary>
        public bool TryShowInterstitial(InterstitialContext context, Action proceed)
        {
            if (!Enabled || IsPresenting || !Service.CanRequestAds) return false;
            var verdict = Policy.Evaluate(context, UtcNow());
            if (!verdict.Eligible) return false;
            if (!Service.IsReady(AdFormat.Interstitial)) return false; // never wait for a download
            bool proceeded = false;
            Action once = () => { if (proceeded) return; proceeded = true; proceed?.Invoke(); };
            BeginPresentation(_ => once());
            bool started = Service.ShowInterstitial(() => Policy.RecordInterstitialShown(UtcNow()),
                outcome => FinishPresentation(outcome));
            if (!started) { AbandonPresentation(); return false; }
            return true;
        }

        // ------------------------------------------------------------------ rewarded

        public bool RewardedReady => Enabled && Service.CanRequestAds && !IsPresenting && _pendingDone == null &&
                                     Service.IsReady(AdFormat.Rewarded);

        public bool CanOfferDust(DateTime localNow) => Enabled && Rewards.CanOfferDust(localNow);

        public bool CanOfferHint => Enabled && Config.RewardedHintEnabled;

        /// <summary>Bonus Dream Dust: shows a rewarded ad only if one is ready and today's cap allows it.</summary>
        public bool ShowRewardedDust(DateTime localNow, Action<RewardResult> done)
        {
            if (!RewardedReady) return false;
            var tx = Rewards.BeginDust(localNow);
            return tx != null && ShowRewarded(tx, done);
        }

        /// <summary>Free hint: the reward becomes a saved entitlement; the caller redeems it for the current board.</summary>
        public bool ShowRewardedHint(string levelId, ulong stateKey, Action<RewardResult> done)
        {
            if (!RewardedReady) return false;
            var tx = Rewards.BeginHint(levelId, stateKey);
            return tx != null && ShowRewarded(tx, done);
        }

        private bool ShowRewarded(AdRewardTransaction tx, Action<RewardResult> done)
        {
            var result = new RewardResult { Transaction = tx };
            BeginPresentation(outcome =>
            {
                Rewards.OnClosed(tx);
                result.Outcome = outcome;
                result.Shown = outcome == AdShowOutcome.Closed;
                _pendingResult = result;
                _pendingDone = done;
                // Give a reward event that arrives just after the close a moment to land before reporting.
                _pendingDeadline = tx.RewardGranted ? _clock : _clock + LateRewardGraceSeconds;
                if (tx.RewardGranted) DeliverPending();
            });
            bool started = Service.ShowRewarded(() => Policy.RecordRewardedShown(UtcNow()),
                () =>
                {
                    bool paid = Rewards.OnRewardEarned(tx);
                    if (!paid) return;
                    if (_pendingDone != null && _pendingResult.Transaction == tx) DeliverPending();
                    else if (!_presenting) StateChanged?.Invoke(); // reward arrived after the result was reported
                },
                outcome => FinishPresentation(outcome));
            if (!started) { AbandonPresentation(); return false; }
            return true;
        }

        private void DeliverPending()
        {
            var done = _pendingDone;
            var result = _pendingResult;
            _pendingDone = null;
            _pendingDeadline = -1f;
            if (done == null) return;
            var tx = result.Transaction;
            result.Rewarded = tx != null && tx.RewardGranted;
            result.DustGranted = result.Rewarded && tx.Kind == AdRewardKind.DreamDust ? tx.Amount : 0;
            done(result);
            StateChanged?.Invoke();
        }

        public void ShowPrivacyOptions(Action done)
        {
            if (!Enabled || IsPresenting) { done?.Invoke(); return; }
            Service.ShowPrivacyOptions(() =>
            {
                done?.Invoke();
                StateChanged?.Invoke();
            });
        }

        // ------------------------------------------------------------------ presentation bookkeeping

        private void BeginPresentation(Action<AdShowOutcome> finish)
        {
            _presenting = true;
            _presentingForeground = 0f;
            _presentFinish = finish;
            Service.SetMuted(_audio != null && _audio.MasterMuted);
            if (_audio != null && !_audioSuspended)
            {
                _audioSuspended = true;
                _audio.SuspendForFullscreenAd();
            }
        }

        private void FinishPresentation(AdShowOutcome outcome)
        {
            if (!_presenting) return; // duplicate or late callback
            _presenting = false;
            RestoreAudio();
            var finish = _presentFinish;
            _presentFinish = null;
            finish?.Invoke(outcome);
            StateChanged?.Invoke();
        }

        private void AbandonPresentation()
        {
            _presenting = false;
            _presentFinish = null;
            RestoreAudio();
        }

        private void RestoreAudio()
        {
            if (!_audioSuspended) return;
            _audioSuspended = false;
            _audio?.ResumeAfterFullscreenAd();
        }

        public void Dispose()
        {
            if (_presenting) FinishPresentation(AdShowOutcome.FailedToShow);
            Policy.Save();
            Service.Dispose();
        }
    }
}
