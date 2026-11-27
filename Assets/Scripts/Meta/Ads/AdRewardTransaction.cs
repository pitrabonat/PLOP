using System;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// One offered rewarded ad. Created before the ad is shown, completed only by the SDK reward event. The grant key
    /// is derived from the unique id, so a repeated callback can never pay twice.
    /// </summary>
    public sealed class AdRewardTransaction
    {
        public enum State { Pending, Rewarded, Closed }

        public string Id { get; }
        public AdRewardKind Kind { get; }
        /// <summary>Local calendar date the offer was made (daily cap bucket), yyyy-MM-dd.</summary>
        public string Date { get; }
        public int Amount { get; }
        /// <summary>Hint offers: the level and exact board state the player asked about.</summary>
        public string LevelId { get; }
        public ulong StateKey { get; }
        public State Status { get; internal set; } = State.Pending;
        public bool RewardGranted { get; internal set; }
        internal DreamDustWallet Wallet { get; }

        internal AdRewardTransaction(string id, AdRewardKind kind, string date, int amount, string levelId, ulong stateKey,
            DreamDustWallet wallet)
        {
            Id = id;
            Kind = kind;
            Date = date;
            Amount = amount;
            LevelId = levelId;
            StateKey = stateKey;
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        /// <summary>The wallet record key written when the reward is granted.</summary>
        public string GrantKey => Kind == AdRewardKind.DreamDust
            ? RewardTransactionService.DustPrefix + Date + ":" + Id
            : RewardTransactionService.HintEntitlementPrefix + Id;
    }
}
