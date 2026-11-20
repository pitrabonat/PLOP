using System;
using System.Text;

namespace Plop.Core
{
    /// <summary>
    /// Mutable gameplay state: the cell of every Plop (indexed by Plop id) and which Plops are asleep.
    /// Geometry is NOT stored here; it lives in the immutable <see cref="LevelDefinition"/>.
    /// Cheap to clone (one small int array) and comparable for exact equality.
    /// </summary>
    public sealed class GameState : IEquatable<GameState>
    {
        public const int MaxPlops = 32;

        private readonly int[] _cells;
        private uint _asleepMask;

        public GameState(int plopCount)
        {
            if (plopCount < 0 || plopCount > MaxPlops) throw new ArgumentOutOfRangeException(nameof(plopCount));
            _cells = new int[plopCount];
        }

        public int PlopCount => _cells.Length;

        /// <summary>Cell index (y * width + x) of a Plop.</summary>
        public int CellOf(int plopId) => _cells[plopId];

        public bool IsAsleep(int plopId) => (_asleepMask & (1u << plopId)) != 0;

        public uint AsleepMask => _asleepMask;

        public bool AllAsleep => PlopCount > 0 && _asleepMask == AllMask(PlopCount);

        public int AwakeCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _cells.Length; i++) if (!IsAsleep(i)) n++;
                return n;
            }
        }

        public void SetPlop(int plopId, int cell, bool asleep)
        {
            _cells[plopId] = cell;
            if (asleep) _asleepMask |= 1u << plopId;
            else _asleepMask &= ~(1u << plopId);
        }

        internal void SetCell(int plopId, int cell) => _cells[plopId] = cell;
        internal void SetAsleep(int plopId) => _asleepMask |= 1u << plopId;

        /// <summary>Returns the id of the Plop occupying a cell, or -1.</summary>
        public int PlopAt(int cell)
        {
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] == cell) return i;
            return -1;
        }

        public GameState Clone()
        {
            var c = new GameState(_cells.Length);
            Array.Copy(_cells, c._cells, _cells.Length);
            c._asleepMask = _asleepMask;
            return c;
        }

        /// <summary>Copies another state into this one without allocating. Plop counts must match.</summary>
        public void CopyFrom(GameState other)
        {
            if (other._cells.Length != _cells.Length) throw new ArgumentException("Plop count mismatch.");
            Array.Copy(other._cells, _cells, _cells.Length);
            _asleepMask = other._asleepMask;
        }

        /// <summary>Exact equality: every Plop id has the same cell and sleep flag.</summary>
        public bool Equals(GameState other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || other._cells.Length != _cells.Length || other._asleepMask != _asleepMask) return false;
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] != other._cells[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as GameState);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)2166136261 ^ (int)_asleepMask;
                for (int i = 0; i < _cells.Length; i++) h = (h * 16777619) ^ _cells[i];
                return h;
            }
        }

        public static uint AllMask(int plopCount) => plopCount >= 32 ? uint.MaxValue : (1u << plopCount) - 1u;

        public string ToString(LevelDefinition level)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _cells.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i).Append(':').Append(level.PositionOf(_cells[i]));
                if (IsAsleep(i)) sb.Append('z');
            }
            return sb.ToString();
        }
    }
}
