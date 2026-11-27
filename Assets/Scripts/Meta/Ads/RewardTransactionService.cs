using System;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Rewarded-ad payouts through the existing atomic Dream Dust wallet (one record, one write per change).
    /// <list type="bullet">
    /// <item>Bonus Dust is granted under <c>addust:&lt;date&gt;:&lt;id&gt;</c>; the wallet pays a key once, so repeated
    /// callbacks cannot duplicate it. At most <see cref="AdConfiguration.RewardedDustDailyCap"/> offers per local day.</item>
    /// <item>A free hint is an entitlement <c>adhint:&lt;id&gt;</c> (0 Dust). Redeeming writes <c>adhint-used:&lt;id&gt;</c>
    /// and the board's hint key in the same write, so the hint is never charged and never delivered twice.</item>
    /// <item>Only the SDK reward event grants. Closing, failing to load or failing to show grants nothing.</item>
    /// <item>A transaction is bound to the wallet (profile) it started with and refuses any other.</item>
    /// </list>
    /// Limits: this is client-side bookkeeping for a small cosmetic currency. It prevents accidental duplicates, not a
    /// determined player editing local storage; server-side verification would need a backend PLOP! does not have.
    /// </summary>
    public sealed class RewardTransactionService
    {
        public const string DustPrefix = "addust:";
        public const string HintEntitlementPrefix = "adhint:";
        public const string HintUsedPrefix = "adhint-used:";

        private readonly AdConfiguration _config;
        private readonly Func<string> _newId;
        private DreamDustWallet _wallet;

        public RewardTransactionService(DreamDustWallet wallet, AdConfiguration config, Func<string> idSource = null)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _newId = idSource ?? (() => Guid.NewGuid().ToString("N").Substring(0, 16));
        }

        public DreamDustWallet Wallet => _wallet;

        /// <summary>Switches to another profile's wallet. Transactions begun on the old wallet can no longer pay.</summary>
        public void UseWallet(DreamDustWallet wallet) => _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

        public int DustAdsToday(DateTime localNow) => _wallet.CountGrants(DustPrefix + DailySchedule.DateKey(localNow) + ":");

        public bool CanOfferDust(DateTime localNow) =>
            _config.RewardedDustEnabled && _config.RewardedDustAmount > 0 && DustAdsToday(localNow) < _config.RewardedDustDailyCap;

        /// <summary>Starts a bonus-Dust offer, or returns null when today's cap is reached or the placement is off.</summary>
        public AdRewardTransaction BeginDust(DateTime localNow)
        {
            if (!CanOfferDust(localNow)) return null;
            return new AdRewardTransaction(_newId(), AdRewardKind.DreamDust, DailySchedule.DateKey(localNow),
                _config.RewardedDustAmount, null, 0, _wallet);
        }

        /// <summary>Starts a free-hint offer for one verified hint on this exact board.</summary>
        public AdRewardTransaction BeginHint(string levelId, ulong stateKey)
        {
            if (!_config.RewardedHintEnabled || string.IsNullOrEmpty(levelId)) return null;
            return new AdRewardTransaction(_newId(), AdRewardKind.Hint, "", 0, levelId, stateKey, _wallet);
        }

        /// <summary>
        /// The SDK's reward event. Returns true only the first time it pays. A transaction from another profile, a
        /// repeated callback or a transaction that is not ours pays nothing.
        /// </summary>
        public bool OnRewardEarned(AdRewardTransaction transaction)
        {
            if (transaction == null || transaction.RewardGranted) return false;
            if (!ReferenceEquals(transaction.Wallet, _wallet)) return false;
            bool paid = _wallet.Grant(transaction.GrantKey, transaction.Kind == AdRewardKind.DreamDust ? transaction.Amount : 0);
            transaction.RewardGranted = true;
            if (transaction.Status == AdRewardTransaction.State.Pending) transaction.Status = AdRewardTransaction.State.Rewarded;
            return paid;
        }

        /// <summary>The ad closed or failed. Grants nothing; a reward that already arrived stays granted.</summary>
        public void OnClosed(AdRewardTransaction transaction)
        {
            if (transaction != null) transaction.Status = AdRewardTransaction.State.Closed;
        }

        /// <summary>Free hints earned and not yet used (they survive restarts; one is spent per redeemed hint).</summary>
        public int HintEntitlements
        {
            get
            {
                int n = 0;
                foreach (var key in _wallet.GrantsWithPrefix(HintEntitlementPrefix))
                    if (!_wallet.HasSpent(HintUsedPrefix + key.Substring(HintEntitlementPrefix.Length))) n++;
                return n;
            }
        }

        /// <summary>
        /// Uses the oldest unused free hint for this board: marks the entitlement used and the board's hint paid in one
        /// write. Returns false (and writes nothing) when no entitlement is left or this board's hint was already paid.
        /// </summary>
        public bool TryRedeemHint(string levelId, ulong stateKey)
        {
            string hintKey = RewardRules.HintKey(levelId, stateKey);
            if (_wallet.HasSpent(hintKey)) return false;
            foreach (var key in _wallet.GrantsWithPrefix(HintEntitlementPrefix))
            {
                string used = HintUsedPrefix + key.Substring(HintEntitlementPrefix.Length);
                if (_wallet.HasSpent(used)) continue;
                return _wallet.MarkSpent(used, hintKey);
            }
            return false;
        }
    }
}
