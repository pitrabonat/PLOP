using System;
using System.Collections.Generic;
using System.Globalization;
using Plop.Core;

namespace Plop.Meta
{
    /// <summary>Dream Dust amounts and grant keys. Small and cosmetic-only by design.</summary>
    public static class RewardRules
    {
        // Phase 10 economy (only new grants use these; earned balances are never touched).
        public const int LevelComplete = 8;
        public const int LevelPerfect = 4;
        public const int DailyGift = 10;
        public const int DailyPuzzle = 12;
        public const int DailyPuzzlePerfect = 6;
        /// <summary>Dream Dust price of a verified hint for one exact board state (free on intro levels).</summary>
        public const int HintCost = 15;

        /// <summary>Spend key for a hint: one charge per level and exact state, ever.</summary>
        public static string HintKey(string levelId, ulong stateKey) => "hint:" + levelId + ":" + stateKey.ToString("X");

        public static string CompleteKey(string levelId) => "complete:" + levelId;
        public static string PerfectKey(string levelId) => "perfect:" + levelId;
        public static string GiftKey(DateTime date) => "gift:" + DailySchedule.DateKey(date);
        public static string DailyKey(DateTime date) => "daily:" + DailySchedule.DateKey(date);
        public static string DailyPerfectKey(DateTime date) => "dailyPerfect:" + DailySchedule.DateKey(date);
    }

    public enum HintCharge { Charged, AlreadyPaid, Grace, Insufficient }

    /// <summary>
    /// Phase 11 hint fairness. A hint costs <see cref="RewardRules.HintCost"/>; a player who cannot afford it and
    /// has already tried the level <see cref="GraceAttempts"/> times (restart, Lava, out of moves, proven dead
    /// end) gets that level's first hint free, once per level ever. Nobody is stuck for lack of Dust, and the
    /// grace cannot be farmed: it is keyed by level id in the wallet.
    /// </summary>
    public static class HintPolicy
    {
        public const int GraceAttempts = 2;

        public static string GraceKey(string levelId) => "hintgrace:" + levelId;

        public static bool GraceAvailable(DreamDustWallet wallet, string levelId, int attempts)
            => attempts >= GraceAttempts && !wallet.HasSpent(GraceKey(levelId));

        public static HintCharge Charge(DreamDustWallet wallet, string levelId, ulong stateKey, int attempts)
        {
            string key = RewardRules.HintKey(levelId, stateKey);
            var spend = wallet.TrySpend(key, RewardRules.HintCost);
            if (spend == DreamDustWallet.SpendResult.Charged) return HintCharge.Charged;
            if (spend == DreamDustWallet.SpendResult.AlreadyPaid) return HintCharge.AlreadyPaid;
            if (!GraceAvailable(wallet, levelId, attempts)) return HintCharge.Insufficient;
            wallet.TrySpend(GraceKey(levelId), 0);
            wallet.TrySpend(key, 0); // re-showing this board's hint stays free
            return HintCharge.Grace;
        }
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

        /// <summary>
        /// Phase 11: the campaign level that introduces each mechanic (PLOP_Pack_003). A Daily Puzzle only uses
        /// mechanics the player has already met: completed the introducing level.
        /// </summary>
        public static readonly (TileKind Kind, int Level)[] Discovery =
        {
            (TileKind.Frost, 6), (TileKind.Fan, 21), (TileKind.Lava, 35), (TileKind.Portal, 49)
        };

        /// <summary>True when every mechanic in <paramref name="level"/> was introduced at or before <paramref name="highestCompletedLevel"/> (1-based).</summary>
        public static bool IsLearned(LevelDefinition level, int highestCompletedLevel)
        {
            foreach (var tile in level.Tiles)
            {
                int needed = int.MaxValue;
                foreach (var d in Discovery) if (d.Kind == tile.Kind) needed = d.Level;
                if (highestCompletedLevel < needed) return false;
            }
            return true;
        }

        /// <summary>
        /// Today's puzzle among those the player has learned (pool order is kept, so the classic puzzles at the
        /// front are always available). Returns -1 only for an empty pool.
        /// </summary>
        public static int PickIndex(IReadOnlyList<LevelDefinition> pool, DateTime local, int highestCompletedLevel)
        {
            var eligible = new List<int>();
            for (int i = 0; i < pool.Count; i++) if (IsLearned(pool[i], highestCompletedLevel)) eligible.Add(i);
            if (eligible.Count == 0)
            {
                if (pool.Count == 0) return -1;
                for (int i = 0; i < pool.Count; i++) eligible.Add(i);
            }
            return eligible[PoolIndex(local, eligible.Count)];
        }

        private static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }
    }
}
