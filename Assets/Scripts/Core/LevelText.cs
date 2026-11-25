using System;
using System.Collections.Generic;
using System.Text;

namespace Plop.Core
{
    /// <summary>
    /// ASCII notation for tests, docs and debugging. Rows are listed TOP (highest Y) first.
    ///   '#' wall   '.' floor
    ///   'R','B','Y','G','P','O'  Plop of color Red/Blue/Yellow/Green/Purple/Orange
    ///   'r','b','y','g','p','o'  Nest of that color
    /// Plop ids are assigned in reading order (top-left first).
    /// </summary>
    public static class LevelText
    {
        private const string PlopChars = "RBYGPO";
        private const string NestChars = "rbygpo";

        /// <summary>
        /// Rows top to bottom. '#' wall, '.' floor, RBYGPO Plops, rbygpo Nests. Phase 10 tiles: '~' Frost,
        /// '!' Lava, '^' 'v' '&lt;' '&gt;' Breeze fans (power 1, see the overload), '0'–'9' Loop ends (pairs by digit).
        /// </summary>
        public static LevelDefinition Parse(string id, params string[] rows) => Parse(1, id, rows);

        /// <summary>Same as <see cref="Parse(string, string[])"/> with every fan given <paramref name="fanPower"/>.</summary>
        public static LevelDefinition Parse(int fanPower, string id, params string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("No rows.");
            int height = rows.Length, width = rows[0].Length;
            var walls = new List<GridPosition>();
            var plops = new List<PlopDefinition>();
            var nests = new List<NestDefinition>();
            var tiles = new List<MechanicTile>();
            for (int r = 0; r < height; r++)
            {
                if (rows[r].Length != width) throw new ArgumentException("Row " + r + " has length " + rows[r].Length + ", expected " + width + ".");
                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                {
                    char c = rows[r][x];
                    var pos = new GridPosition(x, y);
                    int pi = PlopChars.IndexOf(c), ni = NestChars.IndexOf(c);
                    if (c == '#') walls.Add(pos);
                    else if (pi >= 0) plops.Add(new PlopDefinition(plops.Count, (PlopColor)pi, pos));
                    else if (ni >= 0) nests.Add(new NestDefinition((PlopColor)ni, pos));
                    else if (c == '~') tiles.Add(MechanicTile.Frost(x, y));
                    else if (c == '!') tiles.Add(MechanicTile.Lava(x, y));
                    else if (c == '^') tiles.Add(MechanicTile.Fan(x, y, Direction.Up, fanPower));
                    else if (c == 'v') tiles.Add(MechanicTile.Fan(x, y, Direction.Down, fanPower));
                    else if (c == '<') tiles.Add(MechanicTile.Fan(x, y, Direction.Left, fanPower));
                    else if (c == '>') tiles.Add(MechanicTile.Fan(x, y, Direction.Right, fanPower));
                    else if (c >= '0' && c <= '9') tiles.Add(MechanicTile.Portal(x, y, c - '0'));
                    else if (c != '.') throw new ArgumentException("Unknown character '" + c + "'.");
                }
            }
            return new LevelDefinition(id, id, width, height, walls, plops, nests, tiles: tiles);
        }

        private static char TileChar(MechanicTile t)
        {
            switch (t.Kind)
            {
                case TileKind.Frost: return '~';
                case TileKind.Lava: return '!';
                case TileKind.Portal: return (char)('0' + t.Pair % 10);
                case TileKind.Fan:
                    return t.Direction == Direction.Up ? '^' : t.Direction == Direction.Down ? 'v' : t.Direction == Direction.Left ? '<' : '>';
                default: return '.';
            }
        }

        /// <summary>Renders a level (optionally with a state). Sleeping Plops are shown as '*'.</summary>
        public static string Render(LevelDefinition level, GameState state = null)
        {
            state = state ?? level.CreateInitialState();
            var sb = new StringBuilder();
            for (int y = level.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    int cell = level.CellIndex(x, y);
                    int plop = state.PlopAt(cell);
                    char c;
                    if (level.IsWallCell(cell)) c = '#';
                    else if (plop >= 0) c = state.IsAsleep(plop) ? '*' : PlopChars[(int)level.PlopColorOf(plop)];
                    else if (level.NestColorAt(cell) != LevelDefinition.NoNest) c = NestChars[level.NestColorAt(cell)];
                    else if (level.TileAt(cell) != TileKind.Floor) c = TileChar(level.TileDefinitionAt(cell));
                    else c = '.';
                    sb.Append(c);
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
