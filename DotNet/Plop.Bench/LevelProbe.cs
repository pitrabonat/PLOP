using System;
using System.Linq;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;

namespace Plop.Bench
{
    /// <summary>Phase 11 authoring aid: <c>--probe [fanPower] "row/row/row" ...</c> prints solver and quality numbers per board.</summary>
    public static class LevelProbe
    {
        public static int Run(string[] args)
        {
            int power = 1;
            foreach (var arg in args)
            {
                if (int.TryParse(arg, out int p)) { power = p; continue; }
                var level = LevelText.Parse(power, "probe", arg.Split('/'));
                Console.WriteLine(LevelText.Render(level));
                var a = StateSpaceAnalyzer.AnalyzeWithGraph(level);
                if (!a.Solvable) { Console.WriteLine("  NOT SOLVABLE (" + (a.Complete ? "proven" : "budget") + ")\n"); continue; }
                var ins = PuzzleInsight.Analyze(level, a);
                var q = PuzzleQuality.Evaluate(level, a, ins);
                Console.WriteLine("  opt " + a.StartDistance + " " + DirectionUtil.ToMoveString(a.Solution) + " · states " + a.ReachableStates +
                    " · deadEnd " + (a.DeadEndRatio * 100).ToString("0") + "% · shortest " + a.ShortestSolutionCount + " · near-opt " + q.NearOptimalSolutions);
                Console.WriteLine("  challenge " + ins.Challenge + " (" + CampaignPlan.Categorize(ins.Challenge) + ") · quality " + q.Quality +
                    " · decisions " + q.MeaningfulDecisions + " dens " + q.DecisionDensity + " · depth " + q.PlanningDepth + " · misleading " + q.MisleadingChoices +
                    " · traps " + (ins.TemptingTraps + ins.EarlySettleTraps) + " · coord " + q.Coordination + " solo[" + string.Join(",", q.SoloOptimal) + "]" +
                    " · settle " + q.SettlementMatters + " · run " + q.LongestRun + " · unused " + q.UnusedSpaceRatio.ToString("0.00"));
                Console.WriteLine("  players " + string.Join(" · ", q.Players.Select(r => r.Profile + " " + r.Describe())) +
                    " · mech " + q.MechanicSummary() + " · flags " + string.Join(",", q.Flags) + "\n");
            }
            return 0;
        }
    }
}
