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

        public static LevelDefinition Parse(string id, params string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("No rows.");
            int height = rows.Length, width = rows[0].Length;
            var walls = new List<GridPosition>();
            var plops = new List<PlopDefinition>();
            var nests = new List<NestDefinition>();
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
                    else if (c != '.') throw new ArgumentException("Unknown character '" + c + "'.");
                }
            }
            return new LevelDefinition(id, id, width, height, walls, plops, nests);
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
                    else c = '.';
                    sb.Append(c);
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
