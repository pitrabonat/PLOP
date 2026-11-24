using System;
using System.Diagnostics;
using System.Globalization;
using Plop.Core;
using Plop.Generation;

namespace Plop.Bench
{
    /// <summary>Feasibility probe for QualitySearch targets: --search-probe [seedsPerTarget]</summary>
    internal static class SearchProbe
    {
        public static int Run(int seeds)
        {
            var targets = new (string name, QualityTarget t)[]
            {
                ("5x5 p2 coord", new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 6, MaxOptimal = 10, RequireCoordination = true, MinCounterIntuitive = 1, MinChallenge = 10 }),
                ("5x5 p2 greedy-fail+trap", new QualityTarget { Width = 5, Height = 5, Plops = 2, MinOptimal = 7, MaxOptimal = 12, RequireGreedyFail = true, MinTraps = 1, MinCounterIntuitive = 1, MinChallenge = 16 }),
                ("5x6 p3 order", new QualityTarget { Width = 5, Height = 6, Plops = 3, MinOptimal = 8, MaxOptimal = 13, RequireGreedyFail = true, RequireSettlementOrder = true, MinTraps = 1, MinCounterIntuitive = 1, MinChallenge = 20 }),
                ("6x6 p3 hard", new QualityTarget { Width = 6, Height = 6, Plops = 3, MinOptimal = 10, MaxOptimal = 15, RequireGreedyFail = true, MinTraps = 2, MinCounterIntuitive = 2, MinStrategicBlocks = 2, MinPatterns = 4, MinChallenge = 28 }),
                ("6x7 p4 expert", new QualityTarget { Width = 6, Height = 7, Plops = 4, MinOptimal = 11, MaxOptimal = 18, RequireGreedyFail = true, MinTraps = 2, MinCounterIntuitive = 3, MinStrategicBlocks = 2, MinPatterns = 5, MinChallenge = 34, SolverMaxStates = 600_000 }),
            };
            var ci = CultureInfo.InvariantCulture;
            foreach (var (name, t) in targets)
            {
                for (int s = 0; s < seeds; s++)
                {
                    var sw = Stopwatch.StartNew();
                    var r = QualitySearch.Search(SplitMix64.Derive(0xD3E9, (ulong)s), t);
                    var i = r.Insight;
                    Console.WriteLine(string.Format(ci, "{0,-24} seed{1}: {2} evals={3} {4:0.0}s", name, s, r.Success ? "OK  " : "FAIL", r.Evaluations, sw.Elapsed.TotalSeconds) +
                        (i == null ? "  " + r.Message : string.Format(ci, "  opt={0} greedy={1} random={2:0.0%} counter={3} traps={4}/{5} order={6} blocks={7} rep={8:0%} unused={9:0%} pat=[{10}] challenge={11:0.0} walls={12}",
                            i.Optimal, i.GreedySolves ? i.GreedyLength.ToString(ci) : "fails", i.RandomSolveProbability, i.CounterIntuitiveSteps, i.TemptingTraps, i.EarlySettleTraps,
                            i.SettlementOrderMatters, i.SleeperBlocks + i.WallBlocks, i.RepetitionRatio, i.UnusedSpaceRatio, i.Patterns, i.Challenge, r.Level.Walls.Count)));
                    if (r.Success && s == 0) Console.WriteLine(LevelText.Render(r.Level));
                }
            }
            return 0;
        }
    }
}
