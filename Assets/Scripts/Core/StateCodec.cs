using System;

namespace Plop.Core
{
    /// <summary>
    /// Packs a <see cref="GameState"/> into a canonical 64-bit key.
    ///
    /// Only Plop cells are stored: a Plop is asleep exactly when it stands on a matching Nest
    /// (Plops cannot start on their Nest and fall asleep the moment they enter one), so sleep
    /// flags are derived on decode. Plops of the same color are interchangeable (movement order
    /// never depends on ids because two Plops never share a cell), so their cells are sorted
    /// within each color group — equivalent states get the same key.
    /// </summary>
    public sealed class StateCodec
    {
        private readonly LevelDefinition _level;
        private readonly int _bits;
        private readonly ulong _mask;
        private readonly int _plopCount;
        /// <summary>Plop ids grouped by color, groups in ascending color order.</summary>
        private readonly int[] _groupOrder;
        private readonly int[] _groupStart; // start index in _groupOrder per group
        private readonly int[] _groupLen;
        private readonly int[] _scratch;

        public StateCodec(LevelDefinition level)
        {
            if (!Fits(level)) throw new ArgumentException("State does not fit in 64 bits.");
            _level = level;
            _plopCount = level.Plops.Count;
            _bits = BitsFor(level.CellCount);
            _mask = (1UL << _bits) - 1UL;

            var order = new System.Collections.Generic.List<int>();
            var starts = new System.Collections.Generic.List<int>();
            var lens = new System.Collections.Generic.List<int>();
            for (int c = 0; c < PlopColorUtil.Count; c++)
            {
                int start = order.Count;
                for (int id = 0; id < _plopCount; id++)
                    if ((int)level.PlopColorOf(id) == c) order.Add(id);
                if (order.Count > start) { starts.Add(start); lens.Add(order.Count - start); }
            }
            _groupOrder = order.ToArray();
            _groupStart = starts.ToArray();
            _groupLen = lens.ToArray();
            _scratch = new int[_plopCount];
        }

        public static int BitsFor(int cellCount)
        {
            int bits = 1;
            while ((1 << bits) < cellCount) bits++;
            return bits;
        }

        public static bool Fits(LevelDefinition level)
            => level.CellCount > 0 && BitsFor(level.CellCount) * level.Plops.Count <= 64;

        /// <summary>Canonical key (same-color Plops sorted). Not thread-safe (uses scratch buffer).</summary>
        public ulong Encode(GameState state)
        {
            ulong key = 0;
            int slot = 0;
            for (int g = 0; g < _groupStart.Length; g++)
            {
                int start = _groupStart[g], len = _groupLen[g];
                for (int i = 0; i < len; i++)
                {
                    int cell = state.CellOf(_groupOrder[start + i]);
                    int pos = i;
                    while (pos > 0 && _scratch[pos - 1] > cell) { _scratch[pos] = _scratch[pos - 1]; pos--; }
                    _scratch[pos] = cell;
                }
                for (int i = 0; i < len; i++)
                {
                    key |= ((ulong)_scratch[i] & _mask) << (slot * _bits);
                    slot++;
                }
            }
            return key;
        }

        /// <summary>Writes the canonical state into <paramref name="target"/> (sleep flags derived).</summary>
        public void Decode(ulong key, GameState target)
        {
            int slot = 0;
            for (int g = 0; g < _groupStart.Length; g++)
            {
                int start = _groupStart[g], len = _groupLen[g];
                for (int i = 0; i < len; i++)
                {
                    int id = _groupOrder[start + i];
                    int cell = (int)((key >> (slot * _bits)) & _mask);
                    target.SetPlop(id, cell, _level.IsMatchingNest(id, cell));
                    slot++;
                }
            }
            target.ClearFailure(); // keys only ever describe live states; failed states are terminal sinks
        }

        public GameState Decode(ulong key)
        {
            var s = new GameState(_plopCount);
            Decode(key, s);
            return s;
        }
    }
}
