namespace Plop.Meta.Ads
{
    /// <summary>Result of the interstitial frequency policy: eligible only when every condition holds.</summary>
    public struct AdEligibility
    {
        public bool Eligible;
        /// <summary>The first condition that failed ("" when eligible). For tests and the development readout.</summary>
        public string Reason;

        public static AdEligibility No(string reason) => new AdEligibility { Eligible = false, Reason = reason };
        public static AdEligibility Yes => new AdEligibility { Eligible = true, Reason = "" };
        public override string ToString() => Eligible ? "eligible" : "blocked: " + Reason;
    }
}
