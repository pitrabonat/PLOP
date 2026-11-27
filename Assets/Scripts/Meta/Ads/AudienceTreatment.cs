namespace Plop.Meta.Ads
{
    /// <summary>
    /// The publisher's audience decision, mapped to the SDK's age treatment. Unconfirmed (the Sprint 04 state) blocks
    /// production advertising entirely; test builds then use the most restrictive content rating.
    /// </summary>
    public enum AudienceTreatment
    {
        /// <summary>The publisher has not classified the audience yet. Production ads are blocked.</summary>
        Unconfirmed = 0,
        /// <summary>Audience confirmed as adults and teens only (no children in the target audience).</summary>
        GeneralAudience = 1,
        /// <summary>Children are part of the target audience along with older players (Families "mixed audience").</summary>
        MixedAudience = 2,
        /// <summary>Primarily child-directed.</summary>
        ChildDirected = 3
    }
}
