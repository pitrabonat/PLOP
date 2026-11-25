using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Generation;

namespace Plop.Bench
{
    /// <summary>Feasibility probe for Phase 10 mechanic targets: --mech-probe [seedsPerTarget]</summary>
    internal static class MechanicProbe
    {
        public static int Run(int seeds)
        {
            var targets = new (string name, QualityTarget t)[]
            {
                ("frost 5x5 p1", new QualityTarget { Width = 5, Height = 5, Plops = 1, MinOptimal = 4, MaxOptimal = 8, Mechanics = MechanicSet.Frost, RequiredUse = MechanicSet.Frost, MinMechanicSteps = 2, RequireGreedyFail = true, MinChallenge = 10, MaxRandomSolve = 0.25, MaxUnusedSpace = 0.7 }),
                ("frost 5x5 p2", new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 5, MaxOptimal = 10, Mechanics = MechanicSet.Frost, RequiredUse = MechanicSet.Frost, MinMechanicSteps = 2, RequireGreedyFail = true, MinTraps = 1, MinChallenge = 18 }),
                ("breeze 5x5 p2", new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 5, MaxOptimal = 10, Mechanics = MechanicSet.Breeze, RequiredUse = MechanicSet.Breeze, MinMechanicSteps = 2, RequireGreedyFail = true, MinChallenge = 16 }),
                ("lava 5x5 p2", new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 6, MaxOptimal = 11, Mechanics = MechanicSet.Lava | MechanicSet.Frost, RequiredUse = MechanicSet.Lava, RequireGreedyFail = true, MinTraps = 1, MinChallenge = 18 }),
                ("loops 5x6 p2", new QualityTarget { Width = 5, Height = 6, Plops = 2, MinOptimal = 5, MaxOptimal = 11, Mechanics = MechanicSet.Loops, RequiredUse = MechanicSet.Loops, MinMechanicSteps = 1, RequireGreedyFail = true, MinChallenge = 16 }),
                ("frost+breeze 6x6 p2", new QualityTarget { Width = 6, Height = 6, Plops = 2, MinOptimal = 6, MaxOptimal = 12, Mechanics = MechanicSet.Frost | MechanicSet.Breeze, RequiredUse = MechanicSet.Frost | MechanicSet.Breeze, MinMechanicSteps = 3, RequireGreedyFail = true, MinTraps = 1, MinChallenge = 24 }),
                ("loop+frost+lava 6x6 p3", new QualityTarget { Width = 6, Height = 6, Plops = 3, MinOptimal = 8, MaxOptimal = 14, Mechanics = MechanicSet.All, RequiredUse = MechanicSet.Frost | MechanicSet.Loops | MechanicSet.Lava, MinMechanicSteps = 3, RequireGreedyFail = true, MinTraps = 2, MinCounterIntuitive = 2, MinChallenge = 32, SolverMaxStates = 600_000 }),
            };
            var ci = CultureInfo.InvariantCulture;
            foreach (var (name, t) in targets)
            {
                var lines = new string[seeds];
                Parallel.For(0, seeds, s =>
                {
                    var sw = Stopwatch.StartNew();
                    var r = QualitySearch.Search(SplitMix64.Derive(0xA11CE, (ulong)s), t);
                    var i = r.Insight;
                    lines[s] = string.Format(ci, "{0,-24} seed{1}: {2} evals={3} {4:0.0}s", name, s, r.Success ? "OK  " : "FAIL", r.Evaluations, sw.Elapsed.TotalSeconds) +
                        (i == null ? "  " + r.Message : string.Format(ci, "  opt={0} greedy={1} random={2:0.0%} counter={3} traps={4}/{5} hazards={6} mech={7} (s{8} p{9} t{10}) states={11} challenge={12:0.0} tiles={13}",
                            i.Optimal, i.GreedySolves ? i.GreedyLength.ToString(ci) : "fails", i.RandomSolveProbability, i.CounterIntuitiveSteps, i.TemptingTraps, i.EarlySettleTraps,
                            i.HazardAlternatives, i.MechanicSteps, i.Slides, i.Pushes, i.Teleports, i.ReachableStates, i.Challenge, r.Level.Tiles.Count)) +
                        (r.Success && s == 0 ? "\n" + LevelText.Render(r.Level) : "");
                });
                foreach (var l in lines) Console.WriteLine(l);
            }
            return 0;
        }
    }
}
