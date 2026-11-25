using System.Collections.Generic;
using Plop.Core;

namespace Plop.Generation
{
    /// <summary>
    /// Phase 11 acceptance floors on top of a <see cref="QualityTarget"/>: they reject boards that are solvable
    /// but thoughtless (random-solvable, shallow, repetitive, decorative mechanics). Heuristic thresholds,
    /// calibrated on the Phase 10 audit; see Documentation/Phase11/PUZZLE_BALANCE_REPORT.md.
    /// </summary>
    public sealed class QualityFloors
    {
        /// <summary>Smallest look-ahead depth that solves must be at least this (1 = no floor).</summary>
        public int MinPlanningDepth = 1;
        public double MinDecisionDensity;
        public int MinDecisions;
        public int MaxLongestRun = 99;
        /// <summary>Seeded random player success rate (400 runs) must stay at or below this.</summary>
        public double MaxRandomSuccess = 1.0;
        public bool RejectDecorative = true;
        /// <summary>Every mechanic kind in <see cref="QualityTarget.RequiredUse"/> must be Essential (or Hazard for Lava).</summary>
        public bool RequireEssentialUse = true;
        public int MaxIdleTiles = 99;
        public double MinQuality;
        public bool RequireLimitedInfoFail;

        public static QualityFloors None => new QualityFloors { RejectDecorative = false, RequireEssentialUse = false };

        /// <summary>Reasons the report misses the floors; empty when it passes.</summary>
        public List<string> Check(PuzzleQualityReport r, MechanicSet requiredUse)
        {
            var miss = new List<string>();
            if (r.PlanningDepth < MinPlanningDepth) miss.Add("depth " + r.PlanningDepth + "<" + MinPlanningDepth);
            if (r.DecisionDensity < MinDecisionDensity) miss.Add("density " + r.DecisionDensity + "<" + MinDecisionDensity);
            if (r.MeaningfulDecisions < MinDecisions) miss.Add("decisions " + r.MeaningfulDecisions + "<" + MinDecisions);
            if (r.LongestRun > MaxLongestRun) miss.Add("run " + r.LongestRun + ">" + MaxLongestRun);
            var random = r.Player(PlayerProfile.Random);
            if (random != null && random.SuccessRate > MaxRandomSuccess) miss.Add("random " + random.SuccessRate + ">" + MaxRandomSuccess);
            if (RejectDecorative && r.DecorativeKinds > 0) miss.Add("decorative mechanic");
            if (r.IdleTiles > MaxIdleTiles) miss.Add("idle tiles " + r.IdleTiles);
            if (r.Quality < MinQuality) miss.Add("quality " + r.Quality + "<" + MinQuality);
            if (RequireLimitedInfoFail && r.Player(PlayerProfile.LimitedInformation)?.Solved == true) miss.Add("limited-information player solves");
            if (RequireEssentialUse)
            {
                foreach (var m in r.Mechanics)
                {
                    var set = m.Kind == TileKind.Frost ? MechanicSet.Frost : m.Kind == TileKind.Lava ? MechanicSet.Lava
                        : m.Kind == TileKind.Fan ? MechanicSet.Breeze : MechanicSet.Loops;
                    if ((requiredUse & set) == 0) continue;
                    bool ok = m.Role == MechanicRole.Essential || (m.Kind == TileKind.Lava && m.Role == MechanicRole.Hazard);
                    if (!ok) miss.Add(m.Kind + " is " + m.Role);
                }
            }
            return miss;
        }
    }
}
