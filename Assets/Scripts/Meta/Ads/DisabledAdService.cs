using System;

namespace Plop.Meta.Ads
{
    /// <summary>No ads, no requests, no rewards, no SDK calls. The default for every build in Sprint 04.</summary>
    public sealed class DisabledAdService : IAdService
    {
        public AdMode Mode => AdMode.Disabled;
        public bool CanRequestAds => false;
        public bool PrivacyOptionsRequired => false;
        public bool IsShowing => false;
        public bool Initialized { get; private set; }

        public void Initialize(Action<bool> done)
        {
            Initialized = true;
            done?.Invoke(false);
        }

        public bool IsReady(AdFormat format) => false;
        public void Load(AdFormat format, Action<bool> done) => done?.Invoke(false);
        public bool ShowInterstitial(Action opened, Action<AdShowOutcome> finished) => false;
        public bool ShowRewarded(Action opened, Action earned, Action<AdShowOutcome> finished) => false;
        public void ShowPrivacyOptions(Action done) => done?.Invoke();
        public void SetMuted(bool muted) { }
        public void Dispose() { }
    }
}
