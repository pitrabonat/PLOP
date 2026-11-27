using Plop.Meta;
using Plop.Meta.Ads;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Chooses the advertising mode of this build. Default: Disabled. <c>PLOP_ADS_TEST</c> (set only by the test-ad
    /// build entry in PlopBuild) selects Google's official test ads; <c>PLOP_ADS_MOCK</c> selects simulated ads.
    /// Development builds can override with the integer-free string preference <c>plop.debug.ads</c>
    /// (off | mock | test) for QA. No symbol or preference can select Production in Sprint 04.
    /// </summary>
    public static class PlopAdsSetup
    {
        public const string DebugPreference = "plop.debug.ads";

        public static AdConfiguration BuildConfiguration()
        {
            var config = AdConfiguration.Default;
#if PLOP_ADS_TEST
            config.Mode = AdMode.Test;
#elif PLOP_ADS_MOCK
            config.Mode = AdMode.Mock;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            switch (PlayerPrefs.GetString(DebugPreference, ""))
            {
                case "off": config.Mode = AdMode.Disabled; break;
                case "mock": config.Mode = AdMode.Mock; break;
                case "test": config.Mode = AdMode.Test; break;
            }
#endif
            return config;
        }

        public static PlopAds Create(IKeyValueStore store, DreamDustWallet wallet, PlopAudio audio, AdConfiguration config = null)
        {
            config = config ?? BuildConfiguration();
            var service = AdServiceRegistry.Create(config, out string note);
            if (config.Mode != AdMode.Disabled || !string.IsNullOrEmpty(note))
                Debug.Log("[PLOP][Ads] requested " + config.Mode + ", running " + service.Mode + (note.Length > 0 ? " (" + note + ")" : ""));
            return new PlopAds(config, service, store, wallet, audio, note);
        }
    }
}
