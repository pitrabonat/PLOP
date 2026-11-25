using System.Linq;
using NUnit.Framework;
using Plop.Meta;

namespace Plop.Tests
{
    /// <summary>Phase 11 hint fairness and the offline economy simulation.</summary>
    public class Phase11EconomyTests
    {
        [Test]
        public void Hint_IsChargedOncePerBoard()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            wallet.Grant("seed", 40);
            Assert.AreEqual(HintCharge.Charged, HintPolicy.Charge(wallet, "L", 7, 0));
            Assert.AreEqual(HintCharge.AlreadyPaid, HintPolicy.Charge(wallet, "L", 7, 0));
            Assert.AreEqual(40 - RewardRules.HintCost, wallet.Balance);
        }

        [Test]
        public void Grace_NeedsTries_IsFreeOncePerLevel_AndCannotBeFarmed()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            Assert.AreEqual(HintCharge.Insufficient, HintPolicy.Charge(wallet, "L", 1, HintPolicy.GraceAttempts - 1), "first try again");
            Assert.AreEqual(HintCharge.Grace, HintPolicy.Charge(wallet, "L", 1, HintPolicy.GraceAttempts));
            Assert.AreEqual(HintCharge.AlreadyPaid, HintPolicy.Charge(wallet, "L", 1, 0), "re-showing the same board stays free");
            Assert.AreEqual(HintCharge.Insufficient, HintPolicy.Charge(wallet, "L", 2, 99), "one grace per level, ever");
            Assert.AreEqual(HintCharge.Grace, HintPolicy.Charge(wallet, "M", 2, 99), "each level has its own grace");
            Assert.AreEqual(0, wallet.Balance);
            Assert.AreEqual(0, wallet.LifetimeEarned, "grace grants no Dust");
        }

        [Test]
        public void Grace_IsNotUsedWhenTheHintIsAffordable()
        {
            var wallet = new DreamDustWallet(new MemoryStore());
            wallet.Grant("seed", RewardRules.HintCost);
            Assert.AreEqual(HintCharge.Charged, HintPolicy.Charge(wallet, "L", 1, 10));
            Assert.IsTrue(HintPolicy.GraceAvailable(wallet, "L", 10), "the grace is kept for when Dust runs out");
        }

        private static readonly double[] Difficulty = Enumerable.Range(0, 160).Select(i => i / 159.0).ToArray();
        private static readonly bool[] Free = Enumerable.Range(0, 160).Select(i => i < 6).ToArray();
        private static readonly int[] Prices = { 40, 60, 80, 100, 120 };

        [Test]
        public void Simulation_IsDeterministic_AndPaysEachRewardOnce()
        {
            var profile = EconomySimulator.Profiles.First(p => p.Name == "Regular");
            var a = EconomySimulator.Run(profile, Difficulty, Free, Prices, runs: 20);
            var b = EconomySimulator.Run(profile, Difficulty, Free, Prices, runs: 20);
            Assert.AreEqual(a.Median.Select(s => s.Earned), b.Median.Select(s => s.Earned));
            var end = a.Median.Last();
            Assert.AreEqual(160, end.Levels);
            Assert.AreEqual(160 * RewardRules.LevelComplete, end.FromLevels, "replays never pay a completion twice");
            Assert.LessOrEqual(end.FromPerfect, 160 * RewardRules.LevelPerfect);
            Assert.LessOrEqual(end.FromGift, end.Days * RewardRules.DailyGift, "one gift per day");
        }

        [Test]
        public void Simulation_NoStandardProfileIsRefusedAHint()
        {
            foreach (var profile in EconomySimulator.Profiles.Where(p => p.HintsWhenStuck == 1))
            {
                var r = EconomySimulator.Run(profile, Difficulty, Free, Prices, runs: 40);
                Assert.AreEqual(0, r.Median.Last().HintsDenied, profile.Name);
            }
        }
    }
}
