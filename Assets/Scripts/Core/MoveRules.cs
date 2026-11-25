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
    ///
    /// Phase 10 mechanics resolve in two deterministic phases inside the same atomic command:
    ///  A. Step: every awake Plop takes its normal one-cell step (exactly as above). Entering Lava fails the
    ///     attempt at once and nothing else resolves.
    ///  B. Effects: each Plop that stepped onto a mechanic tile resolves its whole forced-movement chain,
    ///     one Plop at a time, in the same front-to-back order. On every entered cell, in this order:
    ///       1. Lava          — the attempt fails; resolution stops for every Plop.
    ///       2. Matching Nest — the Plop falls asleep and stops.
    ///       3. Loop          — first portal this command: teleport to its pair (direction kept) if the pair cell
    ///                          is free, else stay on the entry portal and stop. Later portals act as floor.
    ///       4. Breeze fan    — first visit of that fan this command: a walking Plop is pushed up to Power cells
    ///                          in the fan's direction; a sliding Plop turns and keeps sliding. Revisits act as floor.
    ///       5. Frost         — the Plop starts sliding in its current direction until stopped.
    ///     Forced movement stops before walls, the board edge and any Plop (awake or asleep; Plops that have not
    ///     resolved yet are where Phase A left them). Every chain is finite (each fan acts once, one teleport,
    ///     straight lines between); a safety budget throws <see cref="UnresolvedMovementException"/> instead of
    ///     ever returning a truncated result.
    /// A failed state is terminal: <see cref="Apply"/> returns false without changes.
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
            if (state.Failed) return false;
            bool mechanics = level.HasMechanics;
            uint forced = 0; // Plops that stepped onto a mechanic tile and still need Phase B
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
                if (mechanics && level.TileAt(target) == TileKind.Lava)
                {
                    Burn(state, id, to, dir, events);
                    return true;
                }
                if (level.IsMatchingNest(id, target))
                {
                    state.SetAsleep(id);
                    events?.Add(new GameEvent(GameEventType.PlopSettled, id, to, to, dir, BlockReason.None, -1, -1));
                }
                else if (mechanics && level.TileAt(target) != TileKind.Floor) forced |= 1u << id;
            }

            if (forced == 0) return changed;
            for (int k = 0; k < count; k++)
            {
                int id = order[k];
                if ((forced & (1u << id)) == 0 || state.IsAsleep(id)) continue;
                if (!ResolveForced(level, state, id, dir, events)) return true; // failed: stop all resolution
            }
            return changed;
        }

        private enum Motion : byte { Step, Push, Slide }

        private static void Burn(GameState state, int id, GridPosition at, Direction dir, List<GameEvent> events)
        {
            state.SetFailed(id, FailureReason.Lava);
            events?.Add(new GameEvent(GameEventType.PlopBurned, id, at, at, dir, BlockReason.None, -1, -1));
        }

        /// <summary>
        /// Phase B for one Plop standing on the cell it just stepped onto. Returns false when the attempt failed.
        /// </summary>
        private static bool ResolveForced(LevelDefinition level, GameState state, int id, Direction dir, List<GameEvent> events)
        {
            int width = level.Width;
            int cell = state.CellOf(id);
            var motion = Motion.Step;
            int budget = 0;
            ulong fansUsed = 0;
            bool teleported = false;
            int segmentStart = cell;
            var segmentType = GameEventType.PlopSlid;
            var segmentDir = dir;
            int guard = 0;
            int maxSteps = (level.FanCount + 3) * (level.Width + level.Height) + 8;

            while (true)
            {
                // ---- entry effects of the current cell, in precedence order ----
                var tile = level.TileAt(cell);
                if (tile == TileKind.Lava)
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    Burn(state, id, level.PositionOf(cell), dir, events);
                    return false;
                }
                if (level.IsMatchingNest(id, cell))
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    state.SetAsleep(id);
                    var at = level.PositionOf(cell);
                    events?.Add(new GameEvent(GameEventType.PlopSettled, id, at, at, dir, BlockReason.None, -1, -1));
                    return true;
                }
                if (tile == TileKind.Portal && !teleported)
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    teleported = true;
                    int exit = level.PortalExitAt(cell);
                    if (exit < 0 || state.PlopAt(exit) >= 0)
                    {
                        var here = level.PositionOf(cell);
                        events?.Add(new GameEvent(GameEventType.PlopBlocked, id, here, here, dir, BlockReason.PortalExit,
                            exit < 0 ? -1 : state.PlopAt(exit), -1));
                        return true; // the teleport is cancelled and the Plop rests on the entry portal
                    }
                    events?.Add(new GameEvent(GameEventType.PlopTeleported, id, level.PositionOf(cell), level.PositionOf(exit), dir,
                        BlockReason.None, -1, -1));
                    state.SetCell(id, exit);
                    cell = exit;
                    segmentStart = cell;
                    continue; // the exit is a portal too, so no further entry effect applies there
                }
                if (tile == TileKind.Fan)
                {
                    int fan = level.FanIndexAt(cell);
                    ulong bit = fan >= 0 ? 1UL << fan : 0;
                    if ((fansUsed & bit) == 0)
                    {
                        fansUsed |= bit;
                        Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                        var def = level.TileDefinitionAt(cell);
                        dir = def.Direction;
                        if (motion != Motion.Slide) { motion = Motion.Push; budget = def.Power; }
                    }
                }
                else if (tile == TileKind.Frost && motion != Motion.Slide)
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    motion = Motion.Slide;
                }

                // ---- continue forced movement ----
                if (motion == Motion.Step || (motion == Motion.Push && budget <= 0))
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    return true;
                }
                int x = cell % width, y = cell / width;
                switch (dir)
                {
                    case Direction.Up: y++; break;
                    case Direction.Down: y--; break;
                    case Direction.Left: x--; break;
                    default: x++; break;
                }
                int next = y * width + x;
                if (!level.InBounds(x, y) || level.IsWallCell(next) || state.PlopAt(next) >= 0)
                {
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                    return true;
                }
                if (++guard > maxSteps)
                    throw new UnresolvedMovementException("Forced movement of Plop " + id + " exceeded " + maxSteps + " steps in " + level.Id);

                var type = motion == Motion.Slide ? GameEventType.PlopSlid : GameEventType.PlopPushed;
                if (segmentStart != cell && (type != segmentType || dir != segmentDir))
                    Flush(level, id, ref segmentStart, cell, segmentType, segmentDir, events);
                if (segmentStart == cell) { segmentType = type; segmentDir = dir; }
                state.SetCell(id, next);
                cell = next;
                if (motion == Motion.Push) budget--;
            }
        }

        /// <summary>Emits the straight forced segment that ended at <paramref name="end"/> (if any) and starts a new one.</summary>
        private static void Flush(LevelDefinition level, int id, ref int start, int end, GameEventType type, Direction dir,
            List<GameEvent> events)
        {
            if (start != end)
                events?.Add(new GameEvent(type, id, level.PositionOf(start), level.PositionOf(end), dir, BlockReason.None, -1, -1));
            start = end;
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
