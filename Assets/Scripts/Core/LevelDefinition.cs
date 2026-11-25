using System;
using System.Collections.Generic;

namespace Plop.Core
{
    /// <summary>Immutable description of one Plop at level start. Ids must be 0..n-1.</summary>
    public readonly struct PlopDefinition
    {
        public readonly int Id;
        public readonly PlopColor Color;
        public readonly GridPosition Start;

        public PlopDefinition(int id, PlopColor color, GridPosition start)
        {
            Id = id;
            Color = color;
            Start = start;
        }
    }

    /// <summary>Immutable Nest (destination cell). A Plop of the same color falls asleep on it.</summary>
    public readonly struct NestDefinition
    {
        public readonly PlopColor Color;
        public readonly GridPosition Position;

        public NestDefinition(PlopColor color, GridPosition position)
        {
            Color = color;
            Position = position;
        }
    }

    /// <summary>
    /// Immutable level: geometry (size, walls, nests) plus the starting Plops.
    /// Mutable gameplay data lives in <see cref="GameState"/>.
    /// Cells are addressed either by <see cref="GridPosition"/> or by cell index = y * Width + x.
    /// Positions outside the grid behave like walls.
    /// </summary>
    public sealed class LevelDefinition
    {
        public const int NoNest = -1;

        public string Id { get; }
        public string Name { get; }
        public string Instruction { get; }
        /// <summary>Verified shortest solution length (the Perfect target), or 0 when unknown. Runtime data.</summary>
        public int OptimalMoves { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public IReadOnlyList<GridPosition> Walls => _wallList;
        public IReadOnlyList<PlopDefinition> Plops => _plops;
        public IReadOnlyList<NestDefinition> Nests => _nests;
        /// <summary>Phase 10 mechanic tiles (Frost, Lava, Breeze fans, Loop portals). Empty for classic levels.</summary>
        public IReadOnlyList<MechanicTile> Tiles => _tiles;
        public bool HasMechanics => _tiles.Length > 0;
        /// <summary>Optional Limited Moves challenge: the attempt ends after this many commands (0 = no limit).</summary>
        public int MoveLimit { get; }
        /// <summary>Number of fans; fans are numbered 0..FanCount-1 in tile order (stable per level).</summary>
        public int FanCount { get; }

        /// <summary>Developer-only data (difficulty, solution, generator info). May be null. Not needed at runtime.</summary>
        public LevelMetadata Metadata { get; }

        private readonly GridPosition[] _wallList;
        private readonly PlopDefinition[] _plops;
        private readonly NestDefinition[] _nests;
        private readonly bool[] _isWall;      // per cell
        private readonly sbyte[] _nestColor;  // per cell, NoNest if none
        private readonly PlopColor[] _plopColors; // per plop id
        private readonly MechanicTile[] _tiles;
        private readonly TileKind[] _tileKind;    // per cell
        private readonly sbyte[] _fanIndex;       // per cell, -1 if none
        private readonly int[] _portalExit;       // per cell, linked portal cell or -1

        public LevelDefinition(string id, string name, int width, int height,
            IEnumerable<GridPosition> walls, IEnumerable<PlopDefinition> plops, IEnumerable<NestDefinition> nests,
            LevelMetadata metadata = null, string instruction = null, int optimalMoves = 0,
            IEnumerable<MechanicTile> tiles = null, int moveLimit = 0)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Width and height must be positive.");
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Instruction = string.IsNullOrWhiteSpace(instruction) ? null : instruction.Trim();
            OptimalMoves = Math.Max(0, optimalMoves);
            Width = width;
            Height = height;
            Metadata = metadata;
            _wallList = walls == null ? Array.Empty<GridPosition>() : new List<GridPosition>(walls).ToArray();
            _plops = plops == null ? Array.Empty<PlopDefinition>() : new List<PlopDefinition>(plops).ToArray();
            _nests = nests == null ? Array.Empty<NestDefinition>() : new List<NestDefinition>(nests).ToArray();

            _isWall = new bool[width * height];
            foreach (var w in _wallList)
                if (InBounds(w)) _isWall[CellIndex(w)] = true;

            _nestColor = new sbyte[width * height];
            for (int i = 0; i < _nestColor.Length; i++) _nestColor[i] = NoNest;
            foreach (var n in _nests)
                if (InBounds(n.Position)) _nestColor[CellIndex(n.Position)] = (sbyte)n.Color;

            MoveLimit = Math.Max(0, moveLimit);
            _tiles = tiles == null ? Array.Empty<MechanicTile>() : new List<MechanicTile>(tiles).ToArray();
            _tileKind = new TileKind[width * height];
            _fanIndex = new sbyte[width * height];
            _portalExit = new int[width * height];
            for (int i = 0; i < _fanIndex.Length; i++) { _fanIndex[i] = -1; _portalExit[i] = -1; }
            int fans = 0;
            foreach (var t in _tiles)
            {
                if (!InBounds(t.Position)) continue;
                int cell = CellIndex(t.Position);
                _tileKind[cell] = t.Kind;
                if (t.Kind == TileKind.Fan && fans < 64) _fanIndex[cell] = (sbyte)fans++;
            }
            FanCount = fans;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Kind != TileKind.Portal || !InBounds(_tiles[i].Position)) continue;
                for (int j = 0; j < _tiles.Length; j++)
                {
                    if (j == i || _tiles[j].Kind != TileKind.Portal || _tiles[j].Pair != _tiles[i].Pair || !InBounds(_tiles[j].Position)) continue;
                    _portalExit[CellIndex(_tiles[i].Position)] = CellIndex(_tiles[j].Position);
                }
            }

            _plopColors = new PlopColor[_plops.Length];
            for (int i = 0; i < _plops.Length; i++)
            {
                int pid = _plops[i].Id;
                if (pid >= 0 && pid < _plopColors.Length) _plopColors[pid] = _plops[i].Color;
            }
        }

        public bool InBounds(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public int CellIndex(GridPosition p) => p.Y * Width + p.X;
        public int CellIndex(int x, int y) => y * Width + x;
        public GridPosition PositionOf(int cell) => new GridPosition(cell % Width, cell / Width);

        /// <summary>True for wall cells and for anything outside the grid.</summary>
        public bool IsWall(GridPosition p) => !InBounds(p) || _isWall[CellIndex(p)];
        public bool IsWallCell(int cell) => _isWall[cell];

        /// <summary>Nest color at a cell index, or <see cref="NoNest"/>.</summary>
        public int NestColorAt(int cell) => _nestColor[cell];

        public PlopColor PlopColorOf(int plopId) => _plopColors[plopId];

        public TileKind TileAt(int cell) => _tileKind[cell];
        public TileKind TileAt(GridPosition p) => InBounds(p) ? _tileKind[CellIndex(p)] : TileKind.Floor;
        /// <summary>Fan number (0-based, stable) at a cell, or -1.</summary>
        public int FanIndexAt(int cell) => _fanIndex[cell];
        /// <summary>The tile definition at a cell (Kind = Floor when the cell is plain).</summary>
        public MechanicTile TileDefinitionAt(int cell)
        {
            if (_tileKind[cell] == TileKind.Floor) return default;
            var p = PositionOf(cell);
            foreach (var t in _tiles) if (t.Position == p) return t;
            return default;
        }
        /// <summary>The paired portal's cell for a portal cell, else -1.</summary>
        public int PortalExitAt(int cell) => _portalExit[cell];

        /// <summary>True when the cell contains a Nest matching the given Plop's color.</summary>
        public bool IsMatchingNest(int plopId, int cell) => _nestColor[cell] == (sbyte)_plopColors[plopId];

        /// <summary>Returns a copy of this level with different metadata (geometry is shared semantics, not reference).</summary>
        public LevelDefinition WithMetadata(LevelMetadata metadata, string id = null, string name = null)
            => new LevelDefinition(id ?? Id, name ?? Name, Width, Height, _wallList, _plops, _nests, metadata, Instruction, OptimalMoves, _tiles, MoveLimit);

        /// <summary>Copy with a different tutorial line (null removes it).</summary>
        public LevelDefinition WithInstruction(string instruction)
            => new LevelDefinition(Id, Name, Width, Height, _wallList, _plops, _nests, Metadata, instruction, OptimalMoves, _tiles, MoveLimit);

        /// <summary>Copy with the verified optimal move count (Perfect target).</summary>
        public LevelDefinition WithOptimalMoves(int optimalMoves)
            => new LevelDefinition(Id, Name, Width, Height, _wallList, _plops, _nests, Metadata, Instruction, optimalMoves, _tiles, MoveLimit);

        /// <summary>Copy with mechanic tiles (replacing any existing ones).</summary>
        public LevelDefinition WithTiles(IEnumerable<MechanicTile> tiles)
            => new LevelDefinition(Id, Name, Width, Height, _wallList, _plops, _nests, Metadata, Instruction, OptimalMoves, tiles, MoveLimit);

        /// <summary>Copy with a Limited Moves challenge (0 removes it).</summary>
        public LevelDefinition WithMoveLimit(int moveLimit)
            => new LevelDefinition(Id, Name, Width, Height, _wallList, _plops, _nests, Metadata, Instruction, OptimalMoves, _tiles, moveLimit);

        /// <summary>Creates the initial mutable state for this level.</summary>
        public GameState CreateInitialState()
        {
            var state = new GameState(_plops.Length);
            for (int i = 0; i < _plops.Length; i++)
            {
                var p = _plops[i];
                int cell = CellIndex(p.Start);
                state.SetPlop(p.Id, cell, IsMatchingNest(p.Id, cell));
            }
            return state;
        }
    }

    /// <summary>Developer-only data attached to a level. Optional at runtime.</summary>
    public sealed class LevelMetadata
    {
        public int GeneratorVersion;
        /// <summary>Generation seed, if generated (null for hand-made levels).</summary>
        public ulong? Seed;
        /// <summary>Human-readable generator parameters, e.g. "w=5 h=5 plops=2 walls=0.2".</summary>
        public string GeneratorParams;
        public int SolverVersion;
        /// <summary>Verified shortest solution as U/D/L/R string, or null.</summary>
        public string Solution;
        public DifficultyInfo Difficulty;
        public string Author;
        public string Notes;
    }

    /// <summary>Difficulty estimate stored with a level. Metrics are free-form named numbers.</summary>
    public sealed class DifficultyInfo
    {
        public double Score;
        public DifficultyCategory Category;
        public readonly SortedDictionary<string, double> Metrics = new SortedDictionary<string, double>(StringComparer.Ordinal);
    }

    public enum DifficultyCategory
    {
        Tutorial = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        Expert = 4
    }
}
