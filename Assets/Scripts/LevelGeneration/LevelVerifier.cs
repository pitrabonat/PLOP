using System.Collections.Generic;
using Plop.Core;
using Plop.Solver;

namespace Plop.Generation
{
    public sealed class VerificationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public StateSpaceAnalysis Analysis;
        public bool IsValid => Errors.Count == 0;
        public override string ToString() => IsValid ? "verified" + (Warnings.Count > 0 ? " (" + string.Join("; ", Warnings) + ")" : "") : string.Join("; ", Errors);
    }

    /// <summary>
    /// Full verification of a level: structure, solvability proven within budget, and — when the level
    /// carries metadata — that its stored solution replays and is a shortest solution.
    /// Levels that cannot be verified (budget exhausted) are rejected, never assumed solvable.
    /// </summary>
    public static class LevelVerifier
    {
        public static VerificationReport Verify(LevelDefinition level, int maxStates = 2_000_000)
        {
            var report = new VerificationReport();
            var structure = LevelValidator.Validate(level);
            if (!structure.IsValid) { report.Errors.AddRange(structure.Errors); return report; }

            var analysis = StateSpaceAnalyzer.Analyze(level, maxStates);
            report.Analysis = analysis;
            if (!analysis.Complete) { report.Errors.Add("Could not verify: " + analysis.Message); return report; }
            if (analysis.StartDistance < 0) { report.Errors.Add("Level is proven unsolvable."); return report; }

            var meta = level.Metadata;
            if (meta?.Solution != null)
            {
                Direction[] moves;
                try { moves = DirectionUtil.ParseMoveString(meta.Solution); }
                catch (System.FormatException e) { report.Errors.Add(e.Message); return report; }
                if (!BfsSolver.VerifySolution(level, moves)) report.Errors.Add("Stored solution does not solve the level.");
                else if (moves.Length != analysis.StartDistance)
                    report.Warnings.Add("Stored solution has " + moves.Length + " moves; shortest is " + analysis.StartDistance + ".");
            }
            else report.Warnings.Add("No stored solution.");
            return report;
        }

        /// <summary>Verifies a whole pack: per-level verification, unique ids, no equivalent duplicates.</summary>
        public static VerificationReport VerifyPack(IEnumerable<LevelDefinition> levels, int maxStates = 2_000_000)
        {
            var report = new VerificationReport();
            var list = new List<LevelDefinition>(levels);
            var pack = LevelValidator.ValidatePack(list);
            report.Errors.AddRange(pack.Errors);
            var registry = new DuplicateRegistry();
            foreach (var level in list)
            {
                if (!registry.TryAdd(level)) report.Errors.Add("[" + level.Id + "] duplicate of an earlier level (symmetry/recolor).");
                var single = Verify(level, maxStates);
                foreach (var e in single.Errors) report.Errors.Add("[" + level.Id + "] " + e);
                foreach (var wn in single.Warnings) report.Warnings.Add("[" + level.Id + "] " + wn);
            }
            return report;
        }
    }
}
