using System;

namespace Plop.Core
{
    /// <summary>
    /// Integer grid coordinate. Coordinate system: origin (0,0) is the BOTTOM-LEFT cell,
    /// X grows to the right, Y grows upwards. <see cref="Direction.Up"/> means Y + 1.
    /// </summary>
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public readonly int X;
        public readonly int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPosition Offset(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new GridPosition(X, Y + 1);
                case Direction.Down: return new GridPosition(X, Y - 1);
                case Direction.Left: return new GridPosition(X - 1, Y);
                case Direction.Right: return new GridPosition(X + 1, Y);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);
        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
