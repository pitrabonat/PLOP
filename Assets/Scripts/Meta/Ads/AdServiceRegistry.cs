using System;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Creates the service for a mode. The SDK-backed implementation registers itself from the optional
    /// <c>PLOP.Ads.AdMob</c> assembly, so nothing else depends on Google Mobile Ads. Any failure falls back to
    /// <see cref="DisabledAdService"/>: no ads, normal game.
    /// </summary>
    public static class AdServiceRegistry
    {
        /// <summary>Set by PLOP.Ads.AdMob on Android and iOS players. Null in the Editor and on other platforms.</summary>
        public static Func<AdConfiguration, AdMode, IAdService> SdkFactory;

        public static IAdService Create(AdConfiguration config, out string note)
        {
            note = "";
            var mode = config.EffectiveMode(out string reason);
            if (!string.IsNullOrEmpty(reason)) note = reason;
            switch (mode)
            {
                case AdMode.Mock:
                    return new MockAdService();
                case AdMode.Test:
                case AdMode.Production:
                    if (SdkFactory == null) { note = "ad SDK not available on this platform"; return new DisabledAdService(); }
                    try { return SdkFactory(config, mode) ?? new DisabledAdService(); }
                    catch (Exception e) { note = "ad SDK failed to start: " + e.GetType().Name; return new DisabledAdService(); }
                default:
                    return new DisabledAdService();
            }
        }
    }
}
