#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Plop.Core;

namespace Plop.Presentation
{
    /// <summary>
    /// Development-only showcase boards for environmental art and trace effects (connected Lava and Frost,
    /// fan directions and strengths, Loop pairs, combined chains). Never part of the campaign, never saved,
    /// compiled out of release players. Each board has an optional demo move string that triggers its effects.
    /// Open on a device with <c>Tools/Android/debugprefs.py lab N [world-id] [moves]</c>, or in the Editor with
    /// PLOP → Visual Lab.
    /// </summary>
    public static class VisualLab
    {
        public sealed class Board
        {
            public readonly string Title, Demo, World;
            public readonly LevelDefinition Level;
            public Board(string title, LevelDefinition level, string demo, string world)
            {
                Title = title; Level = level; Demo = demo; World = world;
            }
        }

        private static List<Board> _boards;

        public static IReadOnlyList<Board> Boards => _boards ??= Create();

        /// <summary>1-based, like the debug pref. Null when out of range.</summary>
        public static Board Get(int number) => number >= 1 && number <= Boards.Count ? Boards[number - 1] : null;

        private static List<Board> Create()
        {
            var list = new List<Board>();

            // 1. Lava: isolated cells, a pair, a cross, a 2×2 pool with a tail. Demo: step into the cross.
            list.Add(new Board("Lab 1 · Lava", Named("Lab 1 · Lava", LevelText.Parse("lab-lava",
                "!.....!",
                "..!!...",
                ".....!.",
                "!!..!!!",
                "!!..R!.",
                ".!.....",
                "r......")), "U", "ember-hollow"));

            // 2. Frost: a horizontal path, a vertical path, a 3×3 field and a path ending in the Nest.
            list.Add(new Board("Lab 2 · Frost", Named("Lab 2 · Frost", LevelText.Parse("lab-frost",
                "~~~~~..",
                "......~",
                "~.~~~.~",
                "~.~~~.~",
                "~.~~~.~",
                "~......",
                "..R~~~r")), "R", "frosted-pond"));

            // 3. Breeze: four directions, powers 1–3; the demo walks into a power-3 fan.
            var breeze = LevelText.Parse("lab-breeze",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".R.....",
                "#.....r");
            var fans = new List<MechanicTile>
            {
                MechanicTile.Fan(1, 5, Direction.Up, 1),
                MechanicTile.Fan(5, 5, Direction.Down, 2),
                MechanicTile.Fan(0, 3, Direction.Right, 3),
                MechanicTile.Fan(6, 3, Direction.Left, 2),
                MechanicTile.Fan(2, 1, Direction.Up, 3),
            };
            list.Add(new Board("Lab 3 · Breeze", Named("Lab 3 · Breeze", breeze.WithTiles(fans)), "R", "breezy-meadow"));

            // 4. Loops: four pairs (beads 1–4); the demo walks into pair 3 and exits two cells right.
            list.Add(new Board("Lab 4 · Loops", Named("Lab 4 · Loops", LevelText.Parse("lab-loops",
                "0.....1",
                ".......",
                ".R2.2..",
                ".......",
                "1.....0",
                ".......",
                "..3..3r")), "R", "prism-observatory"));

            // 5. Combined chain: slide → fan turns the slide upward → Loop → keeps sliding to the edge.
            var combo = LevelText.Parse("lab-combo",
                ".......",
                ".....0.",
                ".......",
                "...0...",
                ".......",
                "R~....r",
                ".......");
            var comboTiles = new List<MechanicTile>(combo.Tiles) { MechanicTile.Fan(3, 1, Direction.Up, 2) };
            list.Add(new Board("Lab 5 · Combo", Named("Lab 5 · Combo", combo.WithTiles(comboTiles)), "R", "twilight-attic"));

            // 6. Blocked Loop: swipe 1 sends Blue through the Loop to rest on the far end against a pebble;
            //    Red then reaches the Loop while its exit is occupied, so the teleport is cancelled.
            list.Add(new Board("Lab 6 · Blocked", Named("Lab 6 · Blocked", LevelText.Parse("lab-blocked",
                ".......",
                ".......",
                "RB0....",
                ".......",
                "....0#.",
                ".......",
                "r....b.")), "RRR", "moonlit-clouds"));

            // 7. Everything together on one board (World profile previews).
            var all = LevelText.Parse("lab-all",
                "..!!..#",
                "~~~...b",
                "R...0..",
                "..#....",
                ".0..~~.",
                "B..!...",
                "r......");
            var allTiles = new List<MechanicTile>(all.Tiles) { MechanicTile.Fan(5, 3, Direction.Left, 2) };
            list.Add(new Board("Lab 7 · All", Named("Lab 7 · All", all.WithTiles(allTiles)), "", "cozy-nest"));
            return list;
        }

        private static LevelDefinition Named(string name, LevelDefinition level) =>
            new LevelDefinition(level.Id, name, level.Width, level.Height, Walls(level), level.Plops, level.Nests, tiles: level.Tiles);

        private static List<GridPosition> Walls(LevelDefinition level)
        {
            var walls = new List<GridPosition>();
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                if (level.IsWall(new GridPosition(x, y))) walls.Add(new GridPosition(x, y));
            return walls;
        }
    }
}
#endif
