namespace Plop.Meta.Ads
{
    /// <summary>
    /// Google's official sample AdMob IDs (developers.google.com/admob/unity/test-ads, read 2026-11-27). They always
    /// serve labelled test ads and can never earn revenue or generate live impressions. PLOP! has no AdMob account of
    /// its own yet: no real publisher, app or ad unit ID exists in this repository, and none may be invented.
    /// </summary>
    public static class AdUnitIds
    {
        /// <summary>The publisher prefix shared by every official Google test ID.</summary>
        public const string GoogleTestPublisher = "ca-app-pub-3940256099942544";

        public const string TestAndroidAppId = GoogleTestPublisher + "~3347511713";
        public const string TestIosAppId = GoogleTestPublisher + "~1458002511";

        public const string TestAndroidInterstitial = GoogleTestPublisher + "/1033173712";
        public const string TestAndroidRewarded = GoogleTestPublisher + "/5224354917";
        public const string TestIosInterstitial = GoogleTestPublisher + "/4411468910";
        public const string TestIosRewarded = GoogleTestPublisher + "/1712485313";

        public static bool IsGoogleTestId(string id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith(GoogleTestPublisher, System.StringComparison.Ordinal);

        /// <summary>Official test unit for a format on Android (<paramref name="ios"/> false) or iOS.</summary>
        public static string TestUnit(AdFormat format, bool ios) => format == AdFormat.Interstitial
            ? (ios ? TestIosInterstitial : TestAndroidInterstitial)
            : (ios ? TestIosRewarded : TestAndroidRewarded);
    }
}
