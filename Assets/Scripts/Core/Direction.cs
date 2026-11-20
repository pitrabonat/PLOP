using System;
using System.Collections.Generic;
using System.Text;

namespace Plop.Core
{
    /// <summary>Movement command. Up = +Y, Right = +X (see <see cref="GridPosition"/>).</summary>
    public enum Direction : byte
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    public static class DirectionUtil
    {
        /// <summary>All directions in canonical order (Up, Down, Left, Right). Do not modify.</summary>
        public static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        public static char ToChar(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return 'U';
                case Direction.Down: return 'D';
                case Direction.Left: return 'L';
                case Direction.Right: return 'R';
                default: throw new ArgumentOutOfRangeException(nameof(d));
            }
        }

        public static bool TryParse(char c, out Direction d)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'U': d = Direction.Up; return true;
                case 'D': d = Direction.Down; return true;
                case 'L': d = Direction.Left; return true;
                case 'R': d = Direction.Right; return true;
                default: d = Direction.Up; return false;
            }
        }

        /// <summary>Encodes a move sequence as a compact string, e.g. "RRUL".</summary>
        public static string ToMoveString(IReadOnlyList<Direction> moves)
        {
            if (moves == null) return string.Empty;
            var sb = new StringBuilder(moves.Count);
            for (int i = 0; i < moves.Count; i++) sb.Append(ToChar(moves[i]));
            return sb.ToString();
        }

        public static Direction[] ParseMoveString(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<Direction>();
            var result = new Direction[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                if (!TryParse(s[i], out result[i]))
                    throw new FormatException("Invalid move character '" + s[i] + "' at index " + i);
            }
            return result;
        }
    }
}
