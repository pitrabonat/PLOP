using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Adjacency classification for connected surfaces (Lava pools, Frost paths) and the byte packing used to
    /// hand per-cell data to the board shaders through the SpriteRenderer colour. Pure and deterministic.
    /// </summary>
    public static class SurfaceConnectivity
    {
        public const byte N = 1, E = 2, S = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128;
        public const byte Orthogonal = N | E | S | W;

        public enum Shape : byte { Isolated, End, Straight, Corner, Tee, Cross }

        /// <summary>
        /// Neighbour mask of the cell at (x, y) for cells of <paramref name="kind"/>. A diagonal bit is set only when
        /// both orthogonal neighbours beside it are set too (it then fills the inner corner of a 2×2 block).
        /// </summary>
        public static byte Mask(LevelDefinition level, int x, int y, TileKind kind)
        {
            bool Has(int dx, int dy) => level.InBounds(x + dx, y + dy) && level.TileAt(level.CellIndex(x + dx, y + dy)) == kind;
            byte m = 0;
            if (Has(0, 1)) m |= N;
            if (Has(1, 0)) m |= E;
            if (Has(0, -1)) m |= S;
            if (Has(-1, 0)) m |= W;
            if ((m & (N | E)) == (N | E) && Has(1, 1)) m |= NE;
            if ((m & (S | E)) == (S | E) && Has(1, -1)) m |= SE;
            if ((m & (S | W)) == (S | W) && Has(-1, -1)) m |= SW;
            if ((m & (N | W)) == (N | W) && Has(-1, 1)) m |= NW;
            return m;
        }

        public static Shape Classify(byte mask)
        {
            int n = 0;
            for (int bit = 1; bit <= 8; bit <<= 1) if ((mask & bit) != 0) n++;
            switch (n)
            {
                case 0: return Shape.Isolated;
                case 1: return Shape.End;
                case 2: return (mask & (N | S)) == (N | S) || (mask & (E | W)) == (E | W) ? Shape.Straight : Shape.Corner;
                case 3: return Shape.Tee;
                default: return Shape.Cross;
            }
        }

        /// <summary>True when an edge of the cell is an outer border of the surface (drawn with a rim).</summary>
        public static bool IsOuterEdge(byte mask, Direction side) => (mask & SideBit(side)) == 0;

        public static byte SideBit(Direction side)
        {
            switch (side)
            {
                case Direction.Up: return N;
                case Direction.Right: return E;
                case Direction.Down: return S;
                default: return W;
            }
        }

        /// <summary>Stable per-cell seed (0..255) so bubbles and facets differ between cells.</summary>
        public static byte Seed(int x, int y) => (byte)((x * 73 + y * 151 + x * y * 17 + 41) & 0xFF);

        /// <summary>Lava/Frost renderer colour: r = mask, g = seed, b = transient effect amount.</summary>
        public static Color EncodeSurface(byte mask, byte seed, float effect) =>
            new Color(mask / 255f, seed / 255f, Mathf.Clamp01(effect), 1f);

        public static byte DecodeMask(Color c) => (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);

        /// <summary>Loop renderer colour: r = flash, g = blocked fizzle, b = pair (0..5).</summary>
        public static Color EncodePortal(int pair, float flash, float blocked) =>
            new Color(Mathf.Clamp01(flash), Mathf.Clamp01(blocked), PortalSlot(pair) / 255f, 1f);

        public static int PortalSlot(int pair) => ((pair % 6) + 6) % 6;

        public static int DecodePortalPair(Color c) => Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);

        /// <summary>Bead count on a Loop frame for a pair; both ends of a pair always match.</summary>
        public static int PortalBeads(int pair) => PortalSlot(pair) + 1;

        /// <summary>
        /// Cells of visible airflow in front of a fan, counting the fan cell itself: the wind stops at the first
        /// wall or board edge and never reaches further than the fan's power, so it never suggests pushing through.
        /// </summary>
        public static int WindRun(LevelDefinition level, GridPosition fan, Direction direction, int power)
        {
            int run = 1;
            var p = fan;
            for (int k = 1; k <= power; k++)
            {
                p = p.Offset(direction);
                if (!level.InBounds(p) || level.IsWall(p)) break;
                run++;
            }
            return run;
        }

        /// <summary>Wind renderer colour: r = (cell index * 16 + run), g = intensity, b = seed.</summary>
        public static Color EncodeWind(int index, int run, float intensity, float seed) =>
            new Color(((index & 15) * 16 + (run & 15)) / 255f, Mathf.Clamp01(intensity), Mathf.Clamp01(seed), 1f);

        public static (int Index, int Run) DecodeWind(Color c)
        {
            int code = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            return (code >> 4, code & 15);
        }

        /// <summary>Rotation (degrees about Z) that points local +X along a direction.</summary>
        public static float DirectionAngle(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return 90f;
                case Direction.Left: return 180f;
                case Direction.Down: return 270f;
                default: return 0f;
            }
        }
    }
}
