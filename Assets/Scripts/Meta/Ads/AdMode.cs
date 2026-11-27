namespace Plop.Meta.Ads
{
    /// <summary>
    /// Advertising environment of a build. Disabled is the default and the only mode a store build may use in
    /// Sprint 04. Mock is developer-only (simulated ads, no SDK, no network). Test requests only Google's official
    /// test ads. Production exists as configuration only and stays locked (see <see cref="AdConfiguration.ProductionUnlocked"/>).
    /// </summary>
    public enum AdMode
    {
        Disabled = 0,
        Mock = 1,
        Test = 2,
        Production = 3
    }
}
