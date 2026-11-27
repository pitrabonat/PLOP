using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using Plop.Meta.Ads;
using UnityEngine;
using AdFormat = Plop.Meta.Ads.AdFormat;

namespace Plop.Ads.AdMob
{
    /// <summary>
    /// The only class in PLOP! that talks to the Google Mobile Ads Unity plugin (v11.7.0) and its User Messaging
    /// Platform. Order: request configuration → consent update → consent form if required → SDK initialise only when
    /// <c>ConsentInformation.CanRequestAds()</c> → loads on demand. Test mode only ever uses Google's official test unit
    /// IDs, whatever the configuration says. Every SDK callback is moved to the Unity main thread and fires once.
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        private readonly AdConfiguration _config;
        private readonly AdMode _mode;
        private readonly bool _ios;
        private InterstitialAd _interstitial;
        private RewardedAd _rewarded;
        private bool _loadingInterstitial, _loadingRewarded, _sdkInitialized, _initializing, _disposed;
        private float _interstitialLoadedAt, _rewardedLoadedAt;
        /// <summary>Loaded ads expire after an hour (official guidance); older ones are dropped and reloaded.</summary>
        private const float MaxAdAgeSeconds = 55 * 60;
        private Action<bool> _initDone;

        public AdMobAdService(AdConfiguration config, AdMode mode)
        {
            if (mode != AdMode.Test && mode != AdMode.Production) throw new ArgumentException("AdMob service needs Test or Production mode.");
            _config = config;
            _mode = mode;
            _ios = Application.platform == RuntimePlatform.IPhonePlayer;
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => AdServiceRegistry.SdkFactory = (config, mode) => new AdMobAdService(config, mode);
#endif

        public AdMode Mode => _mode;
        public bool Initialized { get; private set; }
        public bool IsShowing { get; private set; }
        public bool CanRequestAds => _sdkInitialized && !_disposed && SafeCanRequestAds();
        public bool PrivacyOptionsRequired
        {
            get
            {
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        private bool ChildTreatment => _config.Audience == AudienceTreatment.ChildDirected || _config.Audience == AudienceTreatment.MixedAudience;

        public void Initialize(Action<bool> done)
        {
            if (_disposed) { done?.Invoke(false); return; }
            if (_sdkInitialized) { done?.Invoke(CanRequestAds); return; }
            _initDone += done;
            if (_initializing) return;
            _initializing = true;
            try
            {
                // Must be set before consent and before MobileAds.Initialize (official guidance).
                MobileAds.SetRequestConfiguration(new RequestConfiguration
                {
                    MaxAdContentRating = _config.MaxAdContentRating == "T" ? MaxAdContentRating.T
                        : _config.MaxAdContentRating == "PG" ? MaxAdContentRating.PG : MaxAdContentRating.G,
                    AgeRestrictedTreatment = ChildTreatment ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified
                });
                // iOS: pause the Unity player (and its audio) while a full-screen ad covers it.
                MobileAds.SetiOSAppPauseOnBackground(true);

                var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = ChildTreatment };
                ConsentInformation.Update(request, updateError => OnMain(() =>
                {
                    if (updateError != null) Debug.Log("[PLOP][Ads] Consent update failed: " + updateError.Message);
                    // The consent SDK's own form; shown only when it is required for this user and region.
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                    {
                        if (formError != null) Debug.Log("[PLOP][Ads] Consent form: " + formError.Message);
                        if (!SafeCanRequestAds()) { FinishInit(false); return; }
                        MobileAds.Initialize(status => OnMain(() =>
                        {
                            _sdkInitialized = true;
                            FinishInit(CanRequestAds);
                        }));
                    }));
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PLOP][Ads] Initialisation failed: " + e.GetType().Name);
                FinishInit(false);
            }
        }

        private void FinishInit(bool canRequest)
        {
            _initializing = false;
            Initialized = true;
            var done = _initDone;
            _initDone = null;
            Debug.Log("[PLOP][Ads] mode=" + _mode + " canRequestAds=" + canRequest);
            done?.Invoke(canRequest);
        }

        public bool IsReady(AdFormat format)
        {
            if (!CanRequestAds) return false;
            try
            {
                float now = Time.realtimeSinceStartup;
                if (format == AdFormat.Interstitial)
                {
                    if (_interstitial != null && now - _interstitialLoadedAt > MaxAdAgeSeconds) DestroyInterstitial();
                    return _interstitial != null && _interstitial.CanShowAd();
                }
                if (_rewarded != null && now - _rewardedLoadedAt > MaxAdAgeSeconds) DestroyRewarded();
                return _rewarded != null && _rewarded.CanShowAd();
            }
            catch (Exception) { return false; }
        }

        private string UnitId(AdFormat format)
        {
            if (_mode == AdMode.Test) return AdUnitIds.TestUnit(format, _ios); // never anything else in Test mode
            return format == AdFormat.Interstitial
                ? (_ios ? _config.ProductionIosInterstitial : _config.ProductionAndroidInterstitial)
                : (_ios ? _config.ProductionIosRewarded : _config.ProductionAndroidRewarded);
        }

        public void Load(AdFormat format, Action<bool> done)
        {
            if (!CanRequestAds || IsReady(format)) { done?.Invoke(IsReady(format)); return; }
            if (format == AdFormat.Interstitial)
            {
                if (_loadingInterstitial) { done?.Invoke(false); return; }
                _loadingInterstitial = true;
                DestroyInterstitial();
                try
                {
                    InterstitialAd.Load(UnitId(format), new AdRequest(), (ad, error) => OnMain(() =>
                    {
                        _loadingInterstitial = false;
                        if (_disposed || error != null || ad == null) { ad?.Destroy(); Log("load", format, error); done?.Invoke(false); return; }
                        _interstitial = ad;
                        _interstitialLoadedAt = Time.realtimeSinceStartup;
                        done?.Invoke(true);
                    }));
                }
                catch (Exception e) { _loadingInterstitial = false; Debug.LogWarning("[PLOP][Ads] load threw " + e.GetType().Name); done?.Invoke(false); }
            }
            else
            {
                if (_loadingRewarded) { done?.Invoke(false); return; }
                _loadingRewarded = true;
                DestroyRewarded();
                try
                {
                    RewardedAd.Load(UnitId(format), new AdRequest(), (ad, error) => OnMain(() =>
                    {
                        _loadingRewarded = false;
                        if (_disposed || error != null || ad == null) { ad?.Destroy(); Log("load", format, error); done?.Invoke(false); return; }
                        _rewarded = ad;
                        _rewardedLoadedAt = Time.realtimeSinceStartup;
                        done?.Invoke(true);
                    }));
                }
                catch (Exception e) { _loadingRewarded = false; Debug.LogWarning("[PLOP][Ads] load threw " + e.GetType().Name); done?.Invoke(false); }
            }
        }

        public bool ShowInterstitial(Action opened, Action<AdShowOutcome> finished)
        {
            if (IsShowing || !IsReady(AdFormat.Interstitial)) return false;
            var ad = _interstitial;
            _interstitial = null; // single use
            var once = new Once(finished);
            ad.OnAdFullScreenContentOpened += () => OnMain(() => opened?.Invoke());
            ad.OnAdFullScreenContentClosed += () => OnMain(() => { IsShowing = false; ad.Destroy(); once.Fire(AdShowOutcome.Closed); });
            ad.OnAdFullScreenContentFailed += error => OnMain(() => { IsShowing = false; ad.Destroy(); Log("show", AdFormat.Interstitial, error); once.Fire(AdShowOutcome.FailedToShow); });
            IsShowing = true;
            try { ad.Show(); }
            catch (Exception e)
            {
                IsShowing = false;
                ad.Destroy();
                Debug.LogWarning("[PLOP][Ads] show threw " + e.GetType().Name);
                once.Fire(AdShowOutcome.FailedToShow);
            }
            return true;
        }

        public bool ShowRewarded(Action opened, Action earned, Action<AdShowOutcome> finished)
        {
            if (IsShowing || !IsReady(AdFormat.Rewarded)) return false;
            var ad = _rewarded;
            _rewarded = null;
            var once = new Once(finished);
            bool rewardSeen = false;
            ad.OnAdFullScreenContentOpened += () => OnMain(() => opened?.Invoke());
            ad.OnAdFullScreenContentClosed += () => OnMain(() => { IsShowing = false; ad.Destroy(); once.Fire(AdShowOutcome.Closed); });
            ad.OnAdFullScreenContentFailed += error => OnMain(() => { IsShowing = false; ad.Destroy(); Log("show", AdFormat.Rewarded, error); once.Fire(AdShowOutcome.FailedToShow); });
            IsShowing = true;
            try
            {
                ad.Show(reward => OnMain(() =>
                {
                    if (rewardSeen) return; // the reward event pays once per presentation
                    rewardSeen = true;
                    earned?.Invoke();
                }));
            }
            catch (Exception e)
            {
                IsShowing = false;
                ad.Destroy();
                Debug.LogWarning("[PLOP][Ads] show threw " + e.GetType().Name);
                once.Fire(AdShowOutcome.FailedToShow);
            }
            return true;
        }

        public void SetMuted(bool muted)
        {
            if (!_sdkInitialized) return;
            try { MobileAds.SetApplicationMuted(muted); }
            catch (Exception e) { Debug.Log("[PLOP][Ads] Mute not applied: " + e.GetType().Name); }
        }

        public void ShowPrivacyOptions(Action done)
        {
            try { ConsentForm.ShowPrivacyOptionsForm(error => OnMain(() => { if (error != null) Debug.Log("[PLOP][Ads] Privacy options: " + error.Message); done?.Invoke(); })); }
            catch (Exception) { done?.Invoke(); }
        }

        public void Dispose()
        {
            _disposed = true;
            DestroyInterstitial();
            DestroyRewarded();
        }

        private void DestroyInterstitial() { _interstitial?.Destroy(); _interstitial = null; }
        private void DestroyRewarded() { _rewarded?.Destroy(); _rewarded = null; }

        private static bool SafeCanRequestAds()
        {
            try { return ConsentInformation.CanRequestAds(); }
            catch (Exception) { return false; }
        }

        private static void OnMain(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);

        private void Log(string what, AdFormat format, AdError error) =>
            Debug.Log("[PLOP][Ads] " + what + " " + format + " failed" + (error != null ? ": " + error.GetCode() + " " + error.GetMessage() : ""));

        /// <summary>Guarantees a single completion callback even if the SDK reports both close and failure.</summary>
        private sealed class Once
        {
            private Action<AdShowOutcome> _callback;
            public Once(Action<AdShowOutcome> callback) { _callback = callback; }
            public void Fire(AdShowOutcome outcome)
            {
                var c = _callback;
                _callback = null;
                c?.Invoke(outcome);
            }
        }
    }
}
