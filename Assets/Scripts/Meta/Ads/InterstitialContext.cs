namespace Plop.Meta.Ads
{
    /// <summary>The natural break being evaluated: the player tapped Next Level after finishing a campaign level.</summary>
    public struct InterstitialContext
    {
        /// <summary>1-based campaign number of the level that was just finished.</summary>
        public int CompletedLevelNumber;
        /// <summary>True only when this visit completed the level for the first time (replays never lead to an ad).</summary>
        public bool NewlyCompleted;
        /// <summary>True when the finished or the next level teaches something (instruction text, mechanic intro).</summary>
        public bool Tutorial;
    }
}
