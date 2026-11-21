using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Plop.Core;

namespace Plop.Solver
{
    /// <summary>
    /// Versioned binary sidecar. Delta-varint keys are expanded only for the active level.
    /// The content hash detects corruption and the geometry fingerprint prevents stale proofs
    /// from applying to a changed board with the same id.
    /// </summary>
    public sealed class DeadlockCatalog
    {
        // Bump when the binary format or the canonical StateCodec representation changes.
        private const int SchemaVersion = 1;
        private const int Magic = 0x4B4C4450; // PDLK (little-endian)
        private const int HashSize = 32;
        private const int MaxLevels = 10_000;
        private const int MaxBytes = 32 * 1024 * 1024;
        private const int MaxStatesPerLevel = 2_000_000;

        private readonly Dictionary<string, Entry> _levels;
        private readonly byte[] _bytes;

        public int LevelCount => _levels.Count;

        private DeadlockCatalog(Dictionary<string, Entry> levels, byte[] bytes)
        {
            _levels = levels;
            _bytes = bytes;
        }

        public DeadlockTable ForLevel(LevelDefinition level)
        {
            if (level == null || !_levels.TryGetValue(level.Id, out var entry) ||
                !LevelValidator.Validate(level).IsValid ||
                !GeometryHash(level).AsSpan().SequenceEqual(entry.Fingerprint)) return null;

            try
            {
                var solvable = ReadKeys(entry.Solvable);
                var deadlocks = ReadKeys(entry.Deadlocks);
                int left = 0, right = 0;
                while (left < solvable.Length && right < deadlocks.Length)
                {
                    if (solvable[left] == deadlocks[right]) return null;
                    if (solvable[left] < deadlocks[right]) left++;
                    else right++;
                }
                return new DeadlockTable(level, solvable, deadlocks);
            }
            catch (FormatException) { return null; }
        }

        public static bool TryRead(byte[] bytes, out DeadlockCatalog catalog)
        {
            catalog = null;
            if (bytes == null || bytes.Length < 4 * sizeof(int) + HashSize || bytes.Length > MaxBytes) return false;
            int payloadEnd = bytes.Length - HashSize;
            var ownedBytes = (byte[])bytes.Clone();
            using var sha = SHA256.Create();
            if (!sha.ComputeHash(ownedBytes, 0, payloadEnd).AsSpan().SequenceEqual(ownedBytes.AsSpan(payloadEnd)))
                return false;
            using var stream = new MemoryStream(ownedBytes, 0, payloadEnd, false);
            using var reader = new BinaryReader(stream);
            try
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != SchemaVersion ||
                    reader.ReadInt32() != SolverInfo.Version) return false;
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxLevels) return false;
                var levels = new Dictionary<string, Entry>(count, StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    string id = reader.ReadString();
                    byte[] fingerprint = reader.ReadBytes(HashSize);
                    var solvable = ReadKeyRange(reader);
                    var deadlocks = ReadKeyRange(reader);
                    if (id.Length == 0 || solvable.Count + deadlocks.Count < 1 ||
                        solvable.Count + deadlocks.Count > MaxStatesPerLevel || levels.ContainsKey(id)) return false;
                    levels.Add(id, new Entry(fingerprint, solvable, deadlocks));
                }

                if (stream.Position != payloadEnd) return false;
                catalog = new DeadlockCatalog(levels, ownedBytes);
                return true;
            }
            catch (IOException) { return false; }
            catch (FormatException) { return false; }
        }

        /// <summary>Offline only. A budget-limited level contributes no keys to the catalog.</summary>
        public static byte[] Generate(IReadOnlyList<LevelDefinition> levels, int maxStates,
            out IReadOnlyList<StateSpaceAnalysis> analyses)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Count > MaxLevels) throw new ArgumentException("Too many levels.", nameof(levels));
            if (maxStates < 1 || maxStates > MaxStatesPerLevel) throw new ArgumentOutOfRangeException(nameof(maxStates));

            var results = new List<StateSpaceAnalysis>(levels.Count);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic);
            writer.Write(SchemaVersion);
            writer.Write(SolverInfo.Version);
            long countOffset = stream.Position;
            writer.Write(0); // filled after analyzing the pack
            int included = 0;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels)
            {
                if (level != null && !ids.Add(level.Id))
                    throw new ArgumentException("Duplicate level id: " + level.Id, nameof(levels));
                var solvable = new List<ulong>();
                var deadlocks = new List<ulong>();
                var analysis = StateSpaceAnalyzer.Analyze(level, maxStates,
                    (key, canFinish) => (canFinish ? solvable : deadlocks).Add(key));
                results.Add(analysis);
                if (!analysis.Valid || !analysis.Complete) continue;

                writer.Write(level.Id);
                writer.Write(GeometryHash(level));
                WriteKeys(writer, solvable);
                WriteKeys(writer, deadlocks);
                included++;
            }

            long payloadEnd = stream.Position;
            if (payloadEnd > MaxBytes - HashSize) throw new InvalidDataException("Deadlock catalog exceeds its size limit.");
            stream.Position = countOffset;
            writer.Write(included);
            stream.Position = payloadEnd;
            using var sha = SHA256.Create();
            writer.Write(sha.ComputeHash(stream.GetBuffer(), 0, (int)payloadEnd));
            analyses = results;
            return stream.ToArray();
        }

        private static byte[] GeometryHash(LevelDefinition level)
        {
            var initial = level.CreateInitialState();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(level.Width);
            writer.Write(level.Height);
            writer.Write(initial.PlopCount);
            for (int id = 0; id < initial.PlopCount; id++)
            {
                writer.Write((byte)level.PlopColorOf(id));
                writer.Write(initial.CellOf(id));
            }
            for (int cell = 0; cell < level.CellCount; cell++)
            {
                writer.Write(level.IsWallCell(cell));
                writer.Write((sbyte)level.NestColorAt(cell));
            }
            using var sha = SHA256.Create();
            return sha.ComputeHash(stream.GetBuffer(), 0, (int)stream.Length);
        }

        private static void WriteKeys(BinaryWriter writer, List<ulong> keys)
        {
            keys.Sort();
            writer.Write(keys.Count);
            long sizeOffset = writer.BaseStream.Position;
            writer.Write(0);
            long start = writer.BaseStream.Position;
            ulong previous = 0;
            foreach (ulong key in keys)
            {
                ulong delta = key - previous;
                while (delta >= 128)
                {
                    writer.Write((byte)((delta & 127) | 128));
                    delta >>= 7;
                }
                writer.Write((byte)delta);
                previous = key;
            }
            long end = writer.BaseStream.Position;
            writer.BaseStream.Position = sizeOffset;
            writer.Write((int)(end - start));
            writer.BaseStream.Position = end;
        }

        private static KeyRange ReadKeyRange(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            int length = reader.ReadInt32();
            var stream = reader.BaseStream;
            if (count < 0 || count > MaxStatesPerLevel || length < count || length > count * 10 ||
                length > stream.Length - stream.Position) throw new FormatException("Invalid deadlock key range.");
            int start = (int)stream.Position;
            stream.Position += length;
            return new KeyRange(count, start, start + length);
        }

        private ulong[] ReadKeys(KeyRange range)
        {
            var keys = new ulong[range.Count];
            int offset = range.Start;
            ulong previous = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                ulong delta = 0;
                for (int shift = 0; ; shift += 7)
                {
                    if (offset == range.End) throw new FormatException("Truncated deadlock key.");
                    byte next = _bytes[offset++];
                    if (shift == 63 && next > 1) throw new FormatException("Deadlock key overflow.");
                    delta |= (ulong)(next & 127) << shift;
                    if (next < 128) break;
                }
                if ((i > 0 && delta == 0) || delta > ulong.MaxValue - previous)
                    throw new FormatException("Unordered deadlock keys.");
                previous += delta;
                keys[i] = previous;
            }
            if (offset != range.End) throw new FormatException("Unexpected trailing deadlock keys.");
            return keys;
        }

        private readonly struct Entry
        {
            public readonly byte[] Fingerprint;
            public readonly KeyRange Solvable;
            public readonly KeyRange Deadlocks;

            public Entry(byte[] fingerprint, KeyRange solvable, KeyRange deadlocks)
            {
                Fingerprint = fingerprint;
                Solvable = solvable;
                Deadlocks = deadlocks;
            }
        }

        private readonly struct KeyRange
        {
            public readonly int Count;
            public readonly int Start;
            public readonly int End;

            public KeyRange(int count, int start, int end)
            {
                Count = count;
                Start = start;
                End = end;
            }
        }
    }
}
