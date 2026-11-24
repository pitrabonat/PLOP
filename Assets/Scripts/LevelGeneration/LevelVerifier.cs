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
            => VerifyCatalog(levels, maxStates, production: false);

        /// <summary>Checks all shipped boards, including strict shortest replay and exact Undo/Restart.</summary>
        public static VerificationReport VerifyProductionPack(IEnumerable<LevelDefinition> levels, int maxStates = 2_000_000)
            => VerifyCatalog(levels, maxStates, production: true);

        private static VerificationReport VerifyCatalog(IEnumerable<LevelDefinition> levels, int maxStates, bool production)
        {
            var report = new VerificationReport();
            var list = new List<LevelDefinition>(levels);
            if (production && list.Count != ProductionCatalog.LevelCount)
                report.Errors.Add("Expected " + ProductionCatalog.LevelCount + " levels, found " + list.Count + ".");
            var pack = LevelValidator.ValidatePack(list);
            report.Errors.AddRange(pack.Errors);
            if (!pack.IsValid) return report;

            var registry = new DuplicateRegistry();
            foreach (var level in list)
            {
                string prefix = "[" + level.Id + "] ";
                bool nearDuplicate = production && registry.ContainsNearDuplicate(level);
                if (!registry.TryAdd(level)) report.Errors.Add(prefix + "Duplicate of an earlier level (symmetry/recolor).");
                else if (nearDuplicate) report.Errors.Add(prefix + "Only one wall differs from an earlier puzzle.");
                if (production && level.Id != LevelCanonicalizer.CanonicalId(level))
                    report.Errors.Add(prefix + "Production level ID must match its canonical board.");

                var single = Verify(level, maxStates);
                foreach (var error in single.Errors) report.Errors.Add(prefix + error);
                if (production && single.Analysis != null && single.Analysis.Solvable && level.OptimalMoves != single.Analysis.StartDistance)
                    report.Errors.Add(prefix + "optimalMoves " + level.OptimalMoves + " differs from the verified shortest solution " + single.Analysis.StartDistance + ".");
                var warningTarget = production ? report.Errors : report.Warnings;
                foreach (var warning in single.Warnings) warningTarget.Add(prefix + warning);
                if (production && single.IsValid && single.Warnings.Count == 0 && !VerifyHistory(level))
                    report.Errors.Add(prefix + "Undo or Restart failed while replaying the verified solution.");
            }
            return report;
        }

        private static bool VerifyHistory(LevelDefinition level)
        {
            var engine = new GameEngine();
            engine.LoadLevel(level);
            var start = engine.GetState();
            var moves = DirectionUtil.ParseMoveString(level.Metadata.Solution);
            for (int i = 0; i < moves.Length; i++)
            {
                var previous = engine.GetState();
                if (!engine.TryMove(moves[i]).Changed || !engine.Undo() ||
                    engine.MoveCount != i || !engine.CurrentState.Equals(previous) ||
                    !engine.TryMove(moves[i]).Changed)
                    return false;
            }

            if (!engine.IsCompleted || engine.MoveCount != moves.Length) return false;
            engine.Restart();
            return engine.MoveCount == 0 && !engine.CanUndo && !engine.IsCompleted && engine.CurrentState.Equals(start);
        }
    }
}
