using System;
using System.Collections.Generic;
using System.Linq;

namespace Plop.Meta
{
    /// <summary>
    /// Phase 11 offline Dream Dust simulation. Simulated player profiles play the campaign day by day with the real
    /// <see cref="DreamDustWallet"/>, <see cref="RewardRules"/> and grant keys, so duplicate-reward protection and
    /// hint charging behave exactly as in the game. Behaviour rates are assumptions, not measurements: no human
    /// play data exists. Deterministic for a seed.
    /// </summary>
    public static class EconomySimulator
    {
        public sealed class Profile
        {
            public string Name;
            public int LevelsPerDay;
            /// <summary>Chance of a Perfect on a level of average difficulty (first clear).</summary>
            public double PerfectRate;
            /// <summary>Chance of wanting a hint on a level of average difficulty.</summary>
            public double HintRate;
            /// <summary>Hints bought when stuck (each new board state is charged once).</summary>
            public int HintsWhenStuck = 1;
            public double DailyPuzzleRate;
            public double GiftRate;
            /// <summary>Dust kept back before buying cosmetics (hint-heavy players keep some for hints).</summary>
            public int Reserve;
            /// <summary>Replays a cleared non-Perfect level for Perfect with this chance.</summary>
            public double PerfectReplayRate;
        }

        public static readonly Profile[] Profiles =
        {
            new Profile { Name = "Casual", LevelsPerDay = 2, PerfectRate = 0.25, HintRate = 0.12, DailyPuzzleRate = 0.3, GiftRate = 0.5, Reserve = 0 },
            new Profile { Name = "Regular", LevelsPerDay = 5, PerfectRate = 0.45, HintRate = 0.08, DailyPuzzleRate = 0.7, GiftRate = 0.8, Reserve = 0, PerfectReplayRate = 0.1 },
            new Profile { Name = "Enthusiast", LevelsPerDay = 12, PerfectRate = 0.75, HintRate = 0.02, DailyPuzzleRate = 0.95, GiftRate = 0.95, Reserve = 0, PerfectReplayRate = 0.5 },
            new Profile { Name = "High-Hint", LevelsPerDay = 4, PerfectRate = 0.2, HintRate = 0.55, HintsWhenStuck = 2, DailyPuzzleRate = 0.5, GiftRate = 0.7, Reserve = 30 },
        };

        public static readonly int[] Milestones = { 10, 25, 50, 100, 160 };

        public sealed class Snapshot
        {
            public int Levels, Days, Balance, Earned, FromLevels, FromPerfect, FromDaily, FromGift;
            public int HintsBought, HintsDenied, HintsFree, HintsGrace, DustOnHints, Cosmetics, DustOnCosmetics, Perfects;
        }

        public sealed class Result
        {
            public Profile Profile;
            public int Runs;
            /// <summary>Median snapshot per milestone (field-wise medians).</summary>
            public List<Snapshot> Median = new List<Snapshot>();
            /// <summary>Fraction of runs in which a wanted hint could not be afforded before each milestone.</summary>
            public List<double> DeniedRuns = new List<double>();
        }

        /// <param name="difficulty">Per level 0 (gentle) … 1 (hardest); scales Perfect and hint chances.</param>
        /// <param name="freeHint">Per level: hints are free (introduction levels).</param>
        /// <param name="cosmeticPrices">Dust prices of buyable cosmetics.</param>
        public static Result Run(Profile profile, IReadOnlyList<double> difficulty, IReadOnlyList<bool> freeHint,
            IReadOnlyList<int> cosmeticPrices, int runs = 200, ulong seed = 0x504C4F50_45434F4EUL)
        {
            var all = new List<List<Snapshot>>();
            for (int r = 0; r < runs; r++) all.Add(RunOnce(profile, difficulty, freeHint, cosmeticPrices, seed + (ulong)r * 0x9E3779B97F4A7C15UL));
            var result = new Result { Profile = profile, Runs = runs };
            for (int m = 0; m < all[0].Count; m++)
            {
                var column = all.Select(a => a[m]).ToList();
                int Med(Func<Snapshot, int> f) { var v = column.Select(f).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
                result.Median.Add(new Snapshot
                {
                    Levels = column[0].Levels, Days = Med(s => s.Days), Balance = Med(s => s.Balance), Earned = Med(s => s.Earned),
                    FromLevels = Med(s => s.FromLevels), FromPerfect = Med(s => s.FromPerfect), FromDaily = Med(s => s.FromDaily),
                    FromGift = Med(s => s.FromGift), HintsBought = Med(s => s.HintsBought), HintsDenied = Med(s => s.HintsDenied),
                    HintsFree = Med(s => s.HintsFree), HintsGrace = Med(s => s.HintsGrace), DustOnHints = Med(s => s.DustOnHints), Cosmetics = Med(s => s.Cosmetics),
                    DustOnCosmetics = Med(s => s.DustOnCosmetics), Perfects = Med(s => s.Perfects)
                });
                result.DeniedRuns.Add(column.Count(s => s.HintsDenied > 0) / (double)runs);
            }
            return result;
        }

        private static List<Snapshot> RunOnce(Profile p, IReadOnlyList<double> difficulty, IReadOnlyList<bool> freeHint,
            IReadOnlyList<int> prices, ulong seed)
        {
            ulong state = seed;
            double Next()
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return ((z ^ (z >> 31)) >> 11) * (1.0 / (1UL << 53));
            }

            var wallet = new DreamDustWallet(new MemoryStore());
            var shop = prices.OrderBy(x => x).ToList();
            var snap = new Snapshot();
            var snaps = new List<Snapshot>();
            var cleared = new List<int>();
            var perfect = new HashSet<int>();
            var date = new DateTime(2026, 12, 1);
            int level = 0, milestone = 0;
            ulong boardState = 1;

            void Grant(string key, int amount, ref int bucket) { if (wallet.Grant(key, amount)) { bucket += amount; snap.Earned += amount; } }

            while (level < difficulty.Count && snap.Days < 3650)
            {
                snap.Days++;
                if (Next() < p.GiftRate) Grant(RewardRules.GiftKey(date), RewardRules.DailyGift, ref snap.FromGift);
                if (Next() < p.DailyPuzzleRate)
                {
                    Grant(RewardRules.DailyKey(date), RewardRules.DailyPuzzle, ref snap.FromDaily);
                    if (Next() < p.PerfectRate) Grant(RewardRules.DailyPerfectKey(date), RewardRules.DailyPuzzlePerfect, ref snap.FromDaily);
                    // A replay of today's Daily Puzzle earns nothing new.
                    Grant(RewardRules.DailyKey(date), RewardRules.DailyPuzzle, ref snap.FromDaily);
                }
                for (int k = 0; k < p.LevelsPerDay && level < difficulty.Count; k++)
                {
                    double d = difficulty[level];
                    double hintChance = Math.Min(0.95, p.HintRate * (0.4 + 1.6 * d));
                    if (Next() < hintChance)
                    {
                        for (int h = 0; h < p.HintsWhenStuck; h++)
                        {
                            boardState++;
                            if (freeHint[level]) { snap.HintsFree++; continue; }
                            // A stuck player has restarted at least twice before asking (HintPolicy grace).
                            var spend = HintPolicy.Charge(wallet, "L" + level, boardState, HintPolicy.GraceAttempts);
                            if (spend == HintCharge.Charged) { snap.HintsBought++; snap.DustOnHints += RewardRules.HintCost; }
                            else if (spend == HintCharge.Grace) snap.HintsGrace++;
                            else if (spend == HintCharge.Insufficient) snap.HintsDenied++;
                        }
                    }
                    string id = "L" + level;
                    Grant(RewardRules.CompleteKey(id), RewardRules.LevelComplete, ref snap.FromLevels);
                    double perfectChance = Math.Max(0.05, Math.Min(0.98, p.PerfectRate * (1.3 - 0.8 * d)));
                    if (Next() < perfectChance) { perfect.Add(level); Grant(RewardRules.PerfectKey(id), RewardRules.LevelPerfect, ref snap.FromPerfect); }
                    // Replaying a cleared level never pays the completion again.
                    Grant(RewardRules.CompleteKey(id), RewardRules.LevelComplete, ref snap.FromLevels);
                    cleared.Add(level);
                    level++;
                    if (milestone < Milestones.Length && level == Math.Min(Milestones[milestone], difficulty.Count))
                    {
                        snap.Levels = level;
                        snap.Balance = wallet.Balance;
                        snap.Perfects = perfect.Count;
                        snaps.Add(Copy(snap));
                        milestone++;
                    }
                }
                if (p.PerfectReplayRate > 0 && cleared.Count > 0 && Next() < p.PerfectReplayRate)
                {
                    int target = cleared[(int)(Next() * cleared.Count)];
                    if (!perfect.Contains(target) && Next() < 0.6)
                    {
                        perfect.Add(target);
                        Grant(RewardRules.PerfectKey("L" + target), RewardRules.LevelPerfect, ref snap.FromPerfect);
                    }
                }
                while (shop.Count > 0 && wallet.Balance - p.Reserve >= shop[0])
                {
                    string item = "item" + shop.Count;
                    if (!wallet.TryPurchase(item, shop[0])) break;
                    snap.Cosmetics++;
                    snap.DustOnCosmetics += shop[0];
                    shop.RemoveAt(0);
                }
                date = date.AddDays(1);
            }
            while (snaps.Count < Milestones.Length) { snap.Levels = level; snap.Balance = wallet.Balance; snaps.Add(Copy(snap)); }
            return snaps;
        }

        private static Snapshot Copy(Snapshot s) => new Snapshot
        {
            Levels = s.Levels, Days = s.Days, Balance = s.Balance, Earned = s.Earned, FromLevels = s.FromLevels,
            FromPerfect = s.FromPerfect, FromDaily = s.FromDaily, FromGift = s.FromGift, HintsBought = s.HintsBought,
            HintsDenied = s.HintsDenied, HintsFree = s.HintsFree, HintsGrace = s.HintsGrace, DustOnHints = s.DustOnHints, Cosmetics = s.Cosmetics,
            DustOnCosmetics = s.DustOnCosmetics, Perfects = s.Perfects
        };
    }
}
