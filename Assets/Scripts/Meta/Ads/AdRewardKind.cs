namespace Plop.Meta.Ads
{
    /// <summary>What a voluntary rewarded ad pays. Never currency for cash, never extra moves, never progression.</summary>
    public enum AdRewardKind
    {
        /// <summary>Bonus Dream Dust from the main menu (daily cap).</summary>
        DreamDust = 0,
        /// <summary>One free verified hint (kept as an entitlement if the board changed before it could be shown).</summary>
        Hint = 1
    }
}
