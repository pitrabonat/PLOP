using System;
using Plop.Core;

namespace Plop.Generation
{
    /// <summary>Wall-aware walking distance of every awake Plop to its nearest unfilled matching Nest.</summary>
    public sealed class NestDistanceHeuristic
    {
        private readonly LevelDefinition _level;
        private readonly int[][] _distanceFromNest; // per nest index, per cell
        private readonly int[] _nestCells;

        public NestDistanceHeuristic(LevelDefinition level)
        {
            _level = level;
            _distanceFromNest = new int[level.Nests.Count][];
            _nestCells = new int[level.Nests.Count];
            var queue = new int[level.CellCount];
            for (int i = 0; i < level.Nests.Count; i++)
            {
                var dist = new int[level.CellCount];
                for (int c = 0; c < dist.Length; c++) dist[c] = 999;
                int start = level.CellIndex(level.Nests[i].Position);
                _nestCells[i] = start;
                int head = 0, tail = 0;
                dist[start] = 0;
                queue[tail++] = start;
                while (head < tail)
                {
                    int c = queue[head++];
                    int x = c % level.Width, y = c / level.Width;
                    Try(x - 1, y); Try(x + 1, y); Try(x, y - 1); Try(x, y + 1);
                    void Try(int nx, int ny)
                    {
                        if (!level.InBounds(nx, ny)) return;
                        int n = level.CellIndex(nx, ny);
                        if (level.IsWallCell(n) || level.TileAt(n) == TileKind.Lava || dist[n] <= dist[c] + 1) return;
                        dist[n] = dist[c] + 1;
                        queue[tail++] = n;
                    }
                }
                _distanceFromNest[i] = dist;
            }
        }

        public int Evaluate(GameState state)
        {
            int total = 0;
            for (int id = 0; id < state.PlopCount; id++)
            {
                if (state.IsAsleep(id)) continue;
                int cell = state.CellOf(id), best = 999;
                var color = _level.PlopColorOf(id);
                for (int i = 0; i < _nestCells.Length; i++)
                {
                    if (_level.Nests[i].Color != color) continue;
                    int occupant = state.PlopAt(_nestCells[i]);
                    if (occupant >= 0 && state.IsAsleep(occupant)) continue; // already filled
                    best = Math.Min(best, _distanceFromNest[i][cell]);
                }
                total += best;
            }
            return total;
        }

        /// <summary>Walking distance (walls and Lava block, other tiles count as floor) from <paramref name="cell"/> to Nest <paramref name="nest"/>.</summary>
        public int DistanceToNest(int nest, int cell) => _distanceFromNest[nest][cell];

        public int NestCell(int nest) => _nestCells[nest];
    }
}
