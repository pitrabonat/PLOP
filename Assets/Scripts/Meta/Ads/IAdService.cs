using System;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// The only advertising surface the game sees. Puzzle Core, Solver and Generation never reference it, and only the
    /// optional <c>PLOP.Ads.AdMob</c> assembly references the Google Mobile Ads SDK. Every callback arrives on the main
    /// thread, at most once per request. Every implementation must leave the game fully playable when it fails.
    /// </summary>
    public interface IAdService : IDisposable
    {
        AdMode Mode { get; }
        /// <summary>Consent gathered (or not required) and the SDK initialised: ad requests are allowed.</summary>
        bool CanRequestAds { get; }
        /// <summary>The consent SDK says a "Privacy Choices" entry must be available in Settings.</summary>
        bool PrivacyOptionsRequired { get; }
        /// <summary>A full-screen ad is on screen.</summary>
        bool IsShowing { get; }
        /// <summary>Initialise was called (successfully or not).</summary>
        bool Initialized { get; }

        /// <summary>
        /// Updates consent information, shows the consent SDK's own form when it is required, then initialises the SDK
        /// if requests are allowed. <paramref name="done"/> receives <see cref="CanRequestAds"/>. Never requests an ad.
        /// </summary>
        void Initialize(Action<bool> done);

        bool IsReady(AdFormat format);

        /// <summary>Requests one ad of this format if none is loaded or loading. <paramref name="done"/> reports success.</summary>
        void Load(AdFormat format, Action<bool> done);

        /// <summary>
        /// Presents a loaded interstitial. Returns false (and calls nothing) when none is ready. Otherwise
        /// <paramref name="opened"/> fires when the ad is actually on screen (the impression the frequency policy counts)
        /// and <paramref name="finished"/> fires exactly once when it closes or fails.
        /// </summary>
        bool ShowInterstitial(Action opened, Action<AdShowOutcome> finished);

        /// <summary>
        /// Presents a loaded rewarded ad. <paramref name="earned"/> is the SDK reward event (it may arrive before or after
        /// the close); <paramref name="finished"/> fires exactly once.
        /// </summary>
        bool ShowRewarded(Action opened, Action earned, Action<AdShowOutcome> finished);

        /// <summary>Shows the consent SDK's privacy options form (Settings ▸ Privacy Choices).</summary>
        void ShowPrivacyOptions(Action done);

        /// <summary>Follows the game's master mute so a muted player never hears an ad.</summary>
        void SetMuted(bool muted);
    }
}
