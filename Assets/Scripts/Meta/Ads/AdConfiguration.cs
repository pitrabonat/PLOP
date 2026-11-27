using System;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Every advertising setting in one place (Sprint 04). Values are conservative on purpose: the player experience
    /// comes before showing every eligible ad. Each format can be switched off on its own.
    /// </summary>
    public sealed class AdConfiguration
    {
        /// <summary>
        /// Hard gate for live advertising. False in Sprint 04: Production mode resolves to Disabled until the real
        /// publisher has an eligible AdMob account, verified app, confirmed audience, privacy policy and store review.
        /// Changing it is a publisher decision for Sprint 05, never a code-only change.
        /// </summary>
        public const bool ProductionUnlocked = false;

        public AdMode Mode = AdMode.Disabled;

        // ---- formats (independently switchable)
        public bool InterstitialsEnabled = true;
        public bool RewardedDustEnabled = true;
        public bool RewardedHintEnabled = true;
        /// <summary>First-party promotion of the publisher's own games. Off: there is no approved catalog yet.</summary>
        public bool HouseAdsEnabled = false;

        // ---- interstitial frequency policy (all conditions must hold)
        /// <summary>No interstitial after any of the first N campaign levels.</summary>
        public int AdFreeLevels = 10;
        /// <summary>Newly completed campaign levels required since the previous interstitial.</summary>
        public int LevelsBetweenInterstitials = 4;
        /// <summary>Active foreground gameplay seconds required since the previous interstitial.</summary>
        public double MinActiveSecondsBetween = 180;
        /// <summary>At most this many interstitial impressions in any rolling window.</summary>
        public int MaxInterstitialsPerWindow = 4;
        public double WindowMinutes = 30;
        /// <summary>A new player's first N seconds of total active gameplay never see an interstitial.</summary>
        public double NewPlayerProtectionSeconds = 300;
        /// <summary>After a rewarded ad: at least one more newly completed level and this many seconds before an interstitial.</summary>
        public double RewardedCooldownSeconds = 180;

        // ---- rewarded
        /// <summary>Dream Dust per completed bonus ad. Provisional (see Documentation/Sprint04/ECONOMY_REVIEW.md).</summary>
        public int RewardedDustAmount = 15;
        public int RewardedDustDailyCap = 3;

        // ---- loading
        /// <summary>Bounded retry: a failed load waits RetryBaseSeconds × 2^n, at most MaxLoadRetries times per session.</summary>
        public double RetryBaseSeconds = 30;
        public int MaxLoadRetries = 4;

        // ---- audience and content safety
        public AudienceTreatment Audience = AudienceTreatment.Unconfirmed;
        /// <summary>Maximum ad content rating requested from the SDK: "G" (general audiences) by default.</summary>
        public string MaxAdContentRating = "G";

        // ---- production IDs (empty: supplied by the authorized publisher in Sprint 05; never invented)
        public string ProductionAndroidAppId = "";
        public string ProductionIosAppId = "";
        public string ProductionAndroidInterstitial = "";
        public string ProductionAndroidRewarded = "";
        public string ProductionIosInterstitial = "";
        public string ProductionIosRewarded = "";

        public static AdConfiguration Default => new AdConfiguration();

        /// <summary>
        /// The mode this build may actually run in. Production always falls back to Disabled while it is locked, while
        /// the audience is unconfirmed, or while any production ID is missing or is a Google test ID.
        /// </summary>
        public AdMode EffectiveMode(out string reason)
        {
            reason = "";
            if (Mode != AdMode.Production) return Mode;
#pragma warning disable CS0162 // ProductionUnlocked is a deliberate compile-time gate
            if (!ProductionUnlocked) { reason = "production advertising is locked in this sprint"; return AdMode.Disabled; }
            if (Audience == AudienceTreatment.Unconfirmed) { reason = "audience classification is unconfirmed"; return AdMode.Disabled; }
            foreach (var id in new[] { ProductionAndroidAppId, ProductionIosAppId, ProductionAndroidInterstitial,
                         ProductionAndroidRewarded, ProductionIosInterstitial, ProductionIosRewarded })
                if (string.IsNullOrWhiteSpace(id) || AdUnitIds.IsGoogleTestId(id))
                { reason = "production IDs are missing"; return AdMode.Disabled; }
            return AdMode.Production;
#pragma warning restore CS0162
        }

        public AdMode EffectiveMode() => EffectiveMode(out _);

        /// <summary>True when the mode can show anything to the player (simulated or SDK ads).</summary>
        public bool ServesAds => EffectiveMode() != AdMode.Disabled;

        /// <summary>Throws for values that would make the policy more aggressive than the Sprint 04 floor.</summary>
        public void Validate()
        {
            if (AdFreeLevels < 10) throw new InvalidOperationException("AdFreeLevels must be at least 10.");
            if (LevelsBetweenInterstitials < 4) throw new InvalidOperationException("LevelsBetweenInterstitials must be at least 4.");
            if (MinActiveSecondsBetween < 180) throw new InvalidOperationException("MinActiveSecondsBetween must be at least 180.");
            if (MaxInterstitialsPerWindow > 4 || MaxInterstitialsPerWindow < 0) throw new InvalidOperationException("MaxInterstitialsPerWindow must be 0–4.");
            if (WindowMinutes < 30) throw new InvalidOperationException("WindowMinutes must be at least 30.");
            if (NewPlayerProtectionSeconds < 300) throw new InvalidOperationException("NewPlayerProtectionSeconds must be at least 300.");
            if (RewardedDustAmount < 0 || RewardedDustAmount > 25) throw new InvalidOperationException("RewardedDustAmount must be 0–25.");
            if (RewardedDustDailyCap < 0 || RewardedDustDailyCap > 3) throw new InvalidOperationException("RewardedDustDailyCap must be 0–3.");
            if (MaxLoadRetries < 0 || RetryBaseSeconds < 10) throw new InvalidOperationException("Load retries must be bounded.");
            if (MaxAdContentRating != "G" && MaxAdContentRating != "PG" && MaxAdContentRating != "T")
                throw new InvalidOperationException("MaxAdContentRating must be G, PG or T.");
        }
    }
}
