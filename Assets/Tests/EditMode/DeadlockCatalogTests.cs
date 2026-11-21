using NUnit.Framework;
using Plop.Core;
using Plop.Solver;

namespace Plop.Tests
{
    public sealed class DeadlockCatalogTests
    {
        [Test]
        public void MovableButUnsolvableStateIsProven()
        {
            var level = LevelText.Parse("movable-trap", "R...#r");
            var engine = new GameEngine();
            engine.LoadLevel(level);
            Assert.IsTrue(engine.TryMove(Direction.Right).Changed);

            var data = DeadlockCatalog.Generate(new[] { level }, 100, out var proofs);
            Assert.IsTrue(proofs[0].Complete);
            Assert.AreEqual(4, proofs[0].DeadEndStates);
            Assert.IsTrue(DeadlockCatalog.TryRead(data, out var catalog));
            var table = catalog.ForLevel(level);
            Assert.AreEqual(DeadlockStatus.ProvenDeadlock, table.Classify(engine.CurrentState));
            Assert.AreEqual(SolverStatus.ProvenUnsolvable, BfsSolver.Solve(level, engine.CurrentState).Status);
        }

        [Test]
        public void SolvableAndCompletedStatesAreNeverDeadlocks()
        {
            var level = LevelText.Parse("solvable", "R.r.");
            var data = DeadlockCatalog.Generate(new[] { level }, 100, out _);
            Assert.IsTrue(DeadlockCatalog.TryRead(data, out var catalog));
            var table = catalog.ForLevel(level);
            var engine = new GameEngine();
            engine.LoadLevel(level);

            Assert.AreEqual(DeadlockStatus.Solvable, table.Classify(engine.CurrentState));
            engine.TryMove(Direction.Right);
            Assert.AreEqual(DeadlockStatus.Solvable, table.Classify(engine.CurrentState));
            engine.TryMove(Direction.Right);
            Assert.IsTrue(engine.IsCompleted);
            Assert.AreEqual(DeadlockStatus.Solvable, table.Classify(engine.CurrentState));
        }

        [Test]
        public void BudgetLimitedGraphContributesNoProofs()
        {
            var limited = LevelText.Parse("limited", "R...r");
            var covered = LevelText.Parse("covered", "R#r");
            var data = DeadlockCatalog.Generate(new[] { limited, covered }, 1, out var proofs);
            Assert.IsFalse(proofs[0].Complete);
            Assert.IsTrue(proofs[1].Complete);
            Assert.IsTrue(DeadlockCatalog.TryRead(data, out var catalog));
            Assert.AreEqual(1, catalog.LevelCount);
            Assert.IsNull(catalog.ForLevel(limited));
            Assert.AreEqual(DeadlockStatus.ProvenDeadlock,
                catalog.ForLevel(covered).Classify(covered.CreateInitialState()));
        }

        [Test]
        public void MissingStateAndInvalidSleepFlagAreUnknown()
        {
            var level = LevelText.Parse("unknown", "R.r.");
            var data = DeadlockCatalog.Generate(new[] { level }, 100, out _);
            Assert.IsTrue(DeadlockCatalog.TryRead(data, out var catalog));
            var table = catalog.ForLevel(level);

            var state = level.CreateInitialState();
            state.SetPlop(0, 3, false); // A valid cell beyond a Nest the awake Plop cannot cross.
            Assert.AreEqual(DeadlockStatus.Unknown, table.Classify(state));
            state.SetPlop(0, 0, true); // Same encoded cell as the start, invalid sleep flag.
            Assert.AreEqual(DeadlockStatus.Unknown, table.Classify(state));
            state.SetPlop(0, 4, false); // Out-of-range cell would alias to 0 if masked.
            Assert.AreEqual(DeadlockStatus.Unknown, table.Classify(state));
        }

        [Test]
        public void ChangedGeometryWithSameIdIsRejectedButRenamingIsSafe()
        {
            var original = LevelText.Parse("same", "R#r");
            var changed = LevelText.Parse("same", "R.r");
            var renamed = original.WithMetadata(null, name: "A new title");
            var data = DeadlockCatalog.Generate(new[] { original }, 100, out _);
            Assert.IsTrue(DeadlockCatalog.TryRead(data, out var catalog));
            Assert.IsNull(catalog.ForLevel(changed));
            Assert.AreEqual(DeadlockStatus.ProvenDeadlock,
                catalog.ForLevel(renamed).Classify(renamed.CreateInitialState()));
        }

        [Test]
        public void DeterministicBytesRejectCorruptionAndOwnTheirBuffer()
        {
            var level = LevelText.Parse("same", "R...r");
            var first = DeadlockCatalog.Generate(new[] { level }, 100, out _);
            var second = DeadlockCatalog.Generate(new[] { level }, 100, out _);
            CollectionAssert.AreEqual(first, second);
            Assert.IsTrue(DeadlockCatalog.TryRead(first, out var catalog));

            first[first.Length / 2] ^= 0x80;
            Assert.IsFalse(DeadlockCatalog.TryRead(first, out _));
            Assert.AreEqual(DeadlockStatus.Solvable, catalog.ForLevel(level).Classify(level.CreateInitialState()));
        }
    }
}
