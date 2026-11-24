using System;
using System.Collections.Generic;
using System.Globalization;

namespace Plop.Meta
{
    /// <summary>Dream Dust amounts and grant keys. Small and cosmetic-only by design.</summary>
    public static class RewardRules
    {
        public const int LevelComplete = 5;
        public const int LevelPerfect = 10;
        public const int DailyGift = 20;
        public const int DailyPuzzle = 15;
        public const int DailyPuzzlePerfect = 10;

        public static string CompleteKey(string levelId) => "complete:" + levelId;
        public static string PerfectKey(string levelId) => "perfect:" + levelId;
        public static string GiftKey(DateTime date) => "gift:" + DailySchedule.DateKey(date);
        public static string DailyKey(DateTime date) => "daily:" + DailySchedule.DateKey(date);
        public static string DailyPerfectKey(DateTime date) => "dailyPerfect:" + DailySchedule.DateKey(date);
    }

    /// <summary>What the completion card shows. Dust is paid once per key, so replays earn only what is new.</summary>
    public struct CompletionReward
    {
        public AttemptResult Attempt;
        public int DustEarned;
    }

    /// <summary>Applies records and one-time Dream Dust for a finished campaign level or daily puzzle.</summary>
    public static class CompletionRewards
    {
        public static CompletionReward ForLevel(DreamDustWallet wallet, LevelRecords records, string levelId,
            int moves, int optimalMoves, bool assisted)
        {
            var reward = new CompletionReward { Attempt = records.Submit(levelId, moves, optimalMoves, assisted) };
            if (wallet.Grant(RewardRules.CompleteKey(levelId), RewardRules.LevelComplete)) reward.DustEarned += RewardRules.LevelComplete;
            if (reward.Attempt.Perfect && wallet.Grant(RewardRules.PerfectKey(levelId), RewardRules.LevelPerfect)) reward.DustEarned += RewardRules.LevelPerfect;
            return reward;
        }

        /// <summary>Daily puzzle: records under the puzzle ID; rewards keyed by the calendar date it was published for.</summary>
        public static CompletionReward ForDaily(DreamDustWallet wallet, LevelRecords records, string levelId, DateTime date,
            int moves, int optimalMoves, bool assisted)
        {
            var reward = new CompletionReward { Attempt = records.Submit(levelId, moves, optimalMoves, assisted) };
            if (wallet.Grant(RewardRules.DailyKey(date), RewardRules.DailyPuzzle)) reward.DustEarned += RewardRules.DailyPuzzle;
            if (reward.Attempt.Perfect && wallet.Grant(RewardRules.DailyPerfectKey(date), RewardRules.DailyPuzzlePerfect)) reward.DustEarned += RewardRules.DailyPuzzlePerfect;
            return reward;
        }
    }

    /// <summary>Once-per-local-calendar-day gift. No streaks; missing days costs nothing.</summary>
    public static class DailyGift
    {
        public static bool IsAvailable(DreamDustWallet wallet, DateTime localNow) => !wallet.HasGrant(RewardRules.GiftKey(localNow));

        /// <summary>Returns the amount granted, or 0 if today's gift was already claimed.</summary>
        public static int Claim(DreamDustWallet wallet, DateTime localNow)
            => wallet.Grant(RewardRules.GiftKey(localNow), RewardRules.DailyGift) ? RewardRules.DailyGift : 0;
    }

    /// <summary>
    /// Deterministic date → daily-puzzle mapping over a prevalidated offline pool. Uses only the device's local
    /// calendar date (no time zones, no network). A stride coprime with the pool size visits every puzzle once
    /// per cycle without an obvious order.
    /// </summary>
    public static class DailySchedule
    {
        public static readonly DateTime Epoch = new DateTime(2026, 1, 1);
        public const int Stride = 37;

        public static string DateKey(DateTime local) => local.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static int DayNumber(DateTime local) => (int)Math.Floor((local.Date - Epoch).TotalDays);

        public static int PoolIndex(DateTime local, int poolSize)
        {
            if (poolSize <= 0) throw new ArgumentOutOfRangeException(nameof(poolSize));
            int stride = Gcd(Stride, poolSize) == 1 ? Stride : 1;
            long day = ((long)DayNumber(local) % poolSize + poolSize) % poolSize;
            return (int)(day * stride % poolSize);
        }

        private static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }
    }
}
