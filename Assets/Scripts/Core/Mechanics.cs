using System;

namespace Plop.Core
{
    /// <summary>
    /// Special floor surfaces (Phase 10). A cell holds at most one: walls stay separate, Nests and
    /// Plop starts are only allowed on plain floor (see <see cref="LevelValidator"/>).
    /// </summary>
    public enum TileKind : byte
    {
        Floor = 0,
        /// <summary>Entering Frost starts a slide that continues until something stops it.</summary>
        Frost = 1,
        /// <summary>An awake Plop entering Lava fails the attempt.</summary>
        Lava = 2,
        /// <summary>Breeze fan: pushes a Plop that enters it in a fixed direction.</summary>
        Fan = 3,
        /// <summary>Loop: one end of a portal pair.</summary>
        Portal = 4
    }

    /// <summary>
    /// One mechanic tile. <see cref="Direction"/> and <see cref="Power"/> are used by fans (power 1–3);
    /// <see cref="Pair"/> links the two ends of a portal pair (0–15).
    /// </summary>
    public readonly struct MechanicTile : IEquatable<MechanicTile>
    {
        public const int MaxFanPower = 3;
        public const int MaxPortalPairs = 16;

        public readonly TileKind Kind;
        public readonly GridPosition Position;
        public readonly Direction Direction;
        public readonly int Power;
        public readonly int Pair;

        public MechanicTile(TileKind kind, GridPosition position, Direction direction = Direction.Up, int power = 0, int pair = -1)
        {
            Kind = kind;
            Position = position;
            Direction = direction;
            Power = power;
            Pair = pair;
        }

        public static MechanicTile Frost(int x, int y) => new MechanicTile(TileKind.Frost, new GridPosition(x, y));
        public static MechanicTile Lava(int x, int y) => new MechanicTile(TileKind.Lava, new GridPosition(x, y));
        public static MechanicTile Fan(int x, int y, Direction direction, int power) => new MechanicTile(TileKind.Fan, new GridPosition(x, y), direction, power);
        public static MechanicTile Portal(int x, int y, int pair) => new MechanicTile(TileKind.Portal, new GridPosition(x, y), pair: pair);

        public bool Equals(MechanicTile other) => Kind == other.Kind && Position.Equals(other.Position) &&
                                                   Direction == other.Direction && Power == other.Power && Pair == other.Pair;
        public override bool Equals(object obj) => obj is MechanicTile t && Equals(t);
        public override int GetHashCode() => ((int)Kind * 397) ^ Position.GetHashCode() ^ ((int)Direction << 8) ^ (Power << 12) ^ (Pair << 16);

        public override string ToString()
        {
            switch (Kind)
            {
                case TileKind.Fan: return "Fan" + Position + " " + Direction + " x" + Power;
                case TileKind.Portal: return "Loop" + Position + " pair " + Pair;
                default: return Kind + "" + Position;
            }
        }
    }

    /// <summary>Why an attempt failed. Part of <see cref="GameState"/>, so failed states are never confused with live ones.</summary>
    public enum FailureReason : byte
    {
        None = 0,
        Lava = 1
    }

    /// <summary>Thrown when forced movement exceeds its safety budget. Never treated as a legal outcome.</summary>
    public sealed class UnresolvedMovementException : Exception
    {
        public UnresolvedMovementException(string message) : base(message) { }
    }
}
