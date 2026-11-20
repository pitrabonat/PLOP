using System.Collections.Generic;

namespace Plop.Core
{
    /// <summary>
    /// The ONLY implementation of the PLOP! movement rules. Used by <see cref="GameEngine"/>,
    /// the solver, the generator and all tools — never duplicate this logic elsewhere.
    ///
    /// One command moves every awake Plop at most one cell in the given direction:
    ///  * Plops are processed front-to-back relative to the direction (e.g. Right: larger X first).
    ///    Ties: secondary coordinate ascending, then Plop id ascending (stable and order-independent).
    ///  * Each Plop moves if the target cell is inside the grid, not a wall and not occupied by any
    ///    Plop (awake or asleep, using positions already updated by Plops processed earlier).
    ///    Because the front Plop moves first, a Plop directly behind it follows in the same command.
    ///  * A Plop entering a Nest of its own color falls asleep immediately and never moves again.
    ///  * Non-matching Nests are ordinary floor.
    /// </summary>
    public static class MoveRules
    {
        /// <summary>
        /// Applies one command to <paramref name="state"/> in place.
        /// </summary>
        /// <param name="events">Optional sink for PlopMoved / PlopBlocked / PlopSettled events (MoveCount = -1).
        /// Pass null in hot paths (solver) to avoid any event work.</param>
        /// <param name="orderScratch">Optional reusable buffer (length >= plop count) to avoid allocation.</param>
        /// <returns>True when at least one Plop moved (the state changed); false for a no-op.</returns>
        public static bool Apply(LevelDefinition level, GameState state, Direction dir,
            List<GameEvent> events = null, int[] orderScratch = null)
        {
            int n = state.PlopCount;
            int[] order = orderScratch != null && orderScratch.Length >= n ? orderScratch : new int[n];
            int count = BuildOrder(level, state, dir, order);
            int width = level.Width;
            bool changed = false;

            for (int k = 0; k < count; k++)
            {
                int id = order[k];
                int cell = state.CellOf(id);
                int x = cell % width, y = cell / width;
                int tx = x, ty = y;
                switch (dir)
                {
                    case Direction.Up: ty++; break;
                    case Direction.Down: ty--; break;
                    case Direction.Left: tx--; break;
                    default: tx++; break;
                }

                BlockReason reason = BlockReason.None;
                int blocker = -1;
                int target = ty * width + tx;
                if (!level.InBounds(tx, ty)) reason = BlockReason.Edge;
                else if (level.IsWallCell(target)) reason = BlockReason.Wall;
                else
                {
                    blocker = state.PlopAt(target);
                    if (blocker >= 0) reason = BlockReason.Plop;
                }

                if (reason != BlockReason.None)
                {
                    events?.Add(new GameEvent(GameEventType.PlopBlocked, id, new GridPosition(x, y), new GridPosition(x, y),
                        dir, reason, blocker, -1));
                    continue;
                }

                state.SetCell(id, target);
                changed = true;
                var to = new GridPosition(tx, ty);
                events?.Add(new GameEvent(GameEventType.PlopMoved, id, new GridPosition(x, y), to, dir, BlockReason.None, -1, -1));
                if (level.IsMatchingNest(id, target))
                {
                    state.SetAsleep(id);
                    events?.Add(new GameEvent(GameEventType.PlopSettled, id, to, to, dir, BlockReason.None, -1, -1));
                }
            }
            return changed;
        }

        /// <summary>
        /// Writes the ids of awake Plops into <paramref name="order"/> in front-to-back processing order.
        /// Returns how many ids were written.
        /// </summary>
        public static int BuildOrder(LevelDefinition level, GameState state, Direction dir, int[] order)
        {
            int count = 0;
            int n = state.PlopCount;
            for (int id = 0; id < n; id++)
            {
                if (state.IsAsleep(id)) continue;
                // insertion sort (n is tiny); ids are visited ascending so the final id tie-break is stable
                int pos = count++;
                while (pos > 0 && Before(level, state, dir, id, order[pos - 1]))
                {
                    order[pos] = order[pos - 1];
                    pos--;
                }
                order[pos] = id;
            }
            return count;
        }

        /// <summary>True when Plop a must be processed before Plop b for this direction.</summary>
        private static bool Before(LevelDefinition level, GameState state, Direction dir, int a, int b)
        {
            int w = level.Width;
            int ca = state.CellOf(a), cb = state.CellOf(b);
            int ax = ca % w, ay = ca / w, bx = cb % w, by = cb / w;
            int primary, secondary;
            switch (dir)
            {
                case Direction.Right: primary = bx - ax; secondary = ay - by; break; // larger X first, then smaller Y
                case Direction.Left: primary = ax - bx; secondary = ay - by; break;  // smaller X first, then smaller Y
                case Direction.Up: primary = by - ay; secondary = ax - bx; break;    // larger Y first, then smaller X
                default: primary = ay - by; secondary = ax - bx; break;              // Down: smaller Y first, then smaller X
            }
            if (primary != 0) return primary < 0;
            if (secondary != 0) return secondary < 0;
            return a < b;
        }
    }
}
