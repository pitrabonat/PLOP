using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Bench
{
    /// <summary>
    /// Phase 11 authoring aid: <c>--candidates W H plops mech(F/L/B/P or -) minOpt maxOpt minDepth count [maxRandom] [seeds] [fanMax]</c>
    /// runs seeded quality searches and prints the best distinct boards that pass <see cref="QualityFloors"/>.
    /// </summary>
    public static class CandidateSearch
    {
        public static MechanicSet ParseMechanics(string s)
        {
            var m = MechanicSet.None;
            foreach (char c in s.ToUpperInvariant())
            {
                if (c == 'F') m |= MechanicSet.Frost;
                if (c == 'L') m |= MechanicSet.Lava;
                if (c == 'B') m |= MechanicSet.Breeze;
                if (c == 'P') m |= MechanicSet.Loops;
            }
            return m;
        }

        public static int Run(string[] a)
        {
            int w = int.Parse(a[0]), h = int.Parse(a[1]), plops = int.Parse(a[2]);
            var mech = ParseMechanics(a[3]);
            int minOpt = int.Parse(a[4]), maxOpt = int.Parse(a[5]), minDepth = int.Parse(a[6]), count = int.Parse(a[7]);
            double maxRandom = a.Length > 8 ? double.Parse(a[8]) : 0.25;
            int seeds = a.Length > 9 ? int.Parse(a[9]) : 120;
            int maxTiles = a.Length > 10 ? int.Parse(a[10]) : 3;
            int maxWalls = a.Length > 11 ? int.Parse(a[11]) : 99;
            var target = new QualityTarget
            {
                Width = w, Height = h, Plops = plops, MinOptimal = minOpt, MaxOptimal = maxOpt, Mechanics = mech, RequiredUse = mech,
                MinMechanicSteps = mech == MechanicSet.None || mech == MechanicSet.Lava ? 0 : 1, MaxTiles = maxTiles, MaxFans = 2,
                MaxRandomSolve = 0.5, MaxUnusedSpace = 0.55, MaxRepetition = 0.5, Restarts = 12, StepsPerRestart = 120, MaxWalls = maxWalls
            };
            var floors = new QualityFloors { MinPlanningDepth = minDepth, MaxRandomSuccess = maxRandom, MaxLongestRun = 3, MaxIdleTiles = 1 };
            var found = new ConcurrentDictionary<string, (LevelDefinition Level, PuzzleQualityReport Q, PuzzleInsight I)>();
            Parallel.For(0, seeds, i =>
            {
                var r = QualitySearch.Search(SplitMix64.Derive(0x504C4F50_31314341UL, (ulong)(i * 7919 + w * 131 + h * 17 + plops)), target);
                if (!r.Success) return;
                var q = PuzzleQuality.Evaluate(r.Level, r.Analysis, r.Insight);
                if (floors.Check(q, mech).Count > 0) return;
                found.TryAdd(LevelCanonicalizer.CanonicalId(r.Level), (r.Level, q, r.Insight));
            });
            Console.WriteLine(found.Count + " distinct boards pass (" + target.Describe() + ", depth≥" + minDepth + ", random≤" + maxRandom + ")\n");
            foreach (var c in found.Values.OrderByDescending(c => c.Q.Quality).ThenBy(c => c.Q.Optimal).Take(count))
            {
                var rows = LevelText.Render(c.Level).Split('\n');
                Console.WriteLine(string.Join("/", rows) + "   fans: " + string.Join(",", c.Level.Tiles.Where(t => t.Kind == TileKind.Fan).Select(t => t.Direction + "x" + t.Power)));
                Console.WriteLine(LevelText.Render(c.Level));
                Console.WriteLine("  opt " + c.Q.Optimal + " · q " + c.Q.Quality + " · ch " + c.I.Challenge + " · dec " + c.Q.MeaningfulDecisions + " dens " + c.Q.DecisionDensity +
                    " · depth " + c.Q.PlanningDepth + " · mis " + c.Q.MisleadingChoices + " · coord " + c.Q.Coordination + " · rnd " + c.Q.Player(PlayerProfile.Random).SuccessRate +
                    " · LI " + c.Q.Player(PlayerProfile.LimitedInformation).Describe() + " · " + c.Q.MechanicSummary() + " · states " + c.Q.ReachableStates + "\n");
            }
            return 0;
        }
    }
}
