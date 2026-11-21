using System;
using Plop.Core;

namespace Plop.Solver
{
    /// <summary>
    /// Exact canonical keys from a complete reachable-state graph. Separate sorted arrays make a
    /// lookup allocation-free and distinguish unlisted states from proven classes.
    /// </summary>
    public sealed class DeadlockTable
    {
        private readonly LevelDefinition _level;
        private readonly StateCodec _codec;
        private readonly ulong[] _solvable;
        private readonly ulong[] _deadlocks;

        internal DeadlockTable(LevelDefinition level, ulong[] solvable, ulong[] deadlocks)
        {
            _level = level;
            _codec = new StateCodec(level);
            _solvable = solvable;
            _deadlocks = deadlocks;
        }

        // A table uses one StateCodec scratch buffer. Use it from one thread at a time.
        public DeadlockStatus Classify(GameState state)
        {
            if (state == null || state.PlopCount != _level.Plops.Count) return DeadlockStatus.Unknown;
            for (int id = 0; id < state.PlopCount; id++)
            {
                int cell = state.CellOf(id);
                if ((uint)cell >= (uint)_level.CellCount ||
                    state.IsAsleep(id) != _level.IsMatchingNest(id, cell)) return DeadlockStatus.Unknown;
            }

            ulong key = _codec.Encode(state);
            if (Array.BinarySearch(_deadlocks, key) >= 0) return DeadlockStatus.ProvenDeadlock;
            return Array.BinarySearch(_solvable, key) >= 0 ? DeadlockStatus.Solvable : DeadlockStatus.Unknown;
        }
    }
}
