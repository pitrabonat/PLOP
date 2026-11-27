namespace Plop.Meta.Ads
{
    /// <summary>How a full-screen ad presentation ended. Only the SDK reward event grants rewards, never a closure.</summary>
    public enum AdShowOutcome
    {
        /// <summary>The ad was shown and the player closed it.</summary>
        Closed = 0,
        /// <summary>The SDK could not present the ad.</summary>
        FailedToShow = 1,
        /// <summary>No ad was loaded, consent does not allow requests, or ads are disabled.</summary>
        NotReady = 2
    }
}
