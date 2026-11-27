using System;

namespace Plop.Presentation
{
    /// <summary>One choice on the ad offer card. Labels are already localized; no option is visually pushed.</summary>
    public struct AdOfferOption
    {
        public string Label;
        public Action Chosen;
        public bool Enabled;
        /// <summary>True for the option that shows an ad (it carries the small "Ad" tag so it is never mistaken for gameplay).</summary>
        public bool IsAd;

        public AdOfferOption(string label, Action chosen, bool enabled = true, bool isAd = false)
        {
            Label = label;
            Chosen = chosen;
            Enabled = enabled;
            IsAd = isAd;
        }
    }
}
