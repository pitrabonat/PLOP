using System.Collections.Generic;

namespace Plop.Core
{
    public sealed class ValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void Add(string error) => Errors.Add(error);
        public override string ToString() => IsValid ? "valid" : string.Join("; ", Errors);
    }

    /// <summary>
    /// Structural validation (no solving). Solvability is verified separately by the generator /
    /// Level Lab using the solver.
    /// </summary>
    public static class LevelValidator
    {
        public const int MinSize = 1;
        public const int MaxSize = 16;

        public static ValidationResult Validate(LevelDefinition level)
        {
            var r = new ValidationResult();
            if (level == null) { r.Add("Level is null."); return r; }
            if (string.IsNullOrWhiteSpace(level.Id)) r.Add("Level id is empty.");
            if (level.Width < MinSize || level.Height < MinSize || level.Width > MaxSize || level.Height > MaxSize)
                r.Add("Board size " + level.Width + "x" + level.Height + " outside " + MinSize + ".." + MaxSize + ".");

            var wallCells = new HashSet<int>();
            foreach (var w in level.Walls)
            {
                if (!level.InBounds(w)) r.Add("Wall " + w + " is outside the grid.");
                else if (!wallCells.Add(level.CellIndex(w))) r.Add("Duplicate wall at " + w + ".");
            }

            int n = level.Plops.Count;
            if (n == 0) r.Add("Level has no Plops.");
            if (n > GameState.MaxPlops) r.Add("Too many Plops (" + n + ").");

            var ids = new HashSet<int>();
            var plopCells = new HashSet<int>();
            var plopsPerColor = new int[PlopColorUtil.Count];
            foreach (var p in level.Plops)
            {
                if (p.Id < 0 || p.Id >= n) r.Add("Plop id " + p.Id + " must be in 0.." + (n - 1) + ".");
                else if (!ids.Add(p.Id)) r.Add("Duplicate Plop id " + p.Id + ".");
                if ((int)p.Color >= PlopColorUtil.Count) { r.Add("Plop " + p.Id + " has unknown color."); continue; }
                plopsPerColor[(int)p.Color]++;
                if (!level.InBounds(p.Start)) { r.Add("Plop " + p.Id + " starts outside the grid at " + p.Start + "."); continue; }
                int cell = level.CellIndex(p.Start);
                if (wallCells.Contains(cell)) r.Add("Plop " + p.Id + " starts on a wall at " + p.Start + ".");
                if (!plopCells.Add(cell)) r.Add("Plops overlap at " + p.Start + ".");
            }

            var nestCells = new HashSet<int>();
            var nestsPerColor = new int[PlopColorUtil.Count];
            foreach (var nest in level.Nests)
            {
                if ((int)nest.Color >= PlopColorUtil.Count) { r.Add("Nest has unknown color."); continue; }
                nestsPerColor[(int)nest.Color]++;
                if (!level.InBounds(nest.Position)) { r.Add("Nest " + nest.Position + " is outside the grid."); continue; }
                int cell = level.CellIndex(nest.Position);
                if (wallCells.Contains(cell)) r.Add("Nest at " + nest.Position + " is on a wall.");
                if (!nestCells.Add(cell)) r.Add("Nests overlap at " + nest.Position + ".");
            }

            ValidateTiles(level, r, wallCells, plopCells, nestCells);

            for (int c = 0; c < PlopColorUtil.Count; c++)
            {
                if (plopsPerColor[c] != nestsPerColor[c])
                    r.Add("Color " + (PlopColor)c + ": " + plopsPerColor[c] + " Plop(s) but " + nestsPerColor[c] + " Nest(s).");
            }

            if (r.IsValid)
            {
                // A Plop may not start already asleep on its own Nest (trivial / ambiguous start).
                foreach (var p in level.Plops)
                    if (level.IsMatchingNest(p.Id, level.CellIndex(p.Start)))
                        r.Add("Plop " + p.Id + " starts on its matching Nest at " + p.Start + ".");
                if (!StateCodec.Fits(level)) r.Add("Board/Plop count too large for the 64-bit state key.");
            }
            return r;
        }

        /// <summary>Phase 10 tiles: one per floor cell, no Plop starts or Nests on them, fans 1–3, Loops in pairs.</summary>
        private static void ValidateTiles(LevelDefinition level, ValidationResult r, HashSet<int> walls, HashSet<int> plops, HashSet<int> nests)
        {
            if (level.MoveLimit < 0) r.Add("Move limit must not be negative.");
            var tileCells = new HashSet<int>();
            var pairs = new Dictionary<int, int>();
            foreach (var t in level.Tiles)
            {
                if (t.Kind == TileKind.Floor || (int)t.Kind > (int)TileKind.Portal) { r.Add("Tile " + t + " has an unknown kind."); continue; }
                if (!level.InBounds(t.Position)) { r.Add("Tile " + t + " is outside the grid."); continue; }
                int cell = level.CellIndex(t.Position);
                if (!tileCells.Add(cell)) r.Add("Two tiles share " + t.Position + ".");
                if (walls.Contains(cell)) r.Add("Tile " + t + " is on a wall.");
                if (plops.Contains(cell)) r.Add("A Plop starts on tile " + t + "; Plops start on plain floor.");
                if (nests.Contains(cell)) r.Add("A Nest sits on tile " + t + "; Nests need plain floor.");
                if (t.Kind == TileKind.Fan)
                {
                    if (t.Power < 1 || t.Power > MechanicTile.MaxFanPower) r.Add("Fan " + t.Position + " power must be 1.." + MechanicTile.MaxFanPower + ".");
                    if ((int)t.Direction > 3) r.Add("Fan " + t.Position + " has no valid direction.");
                }
                if (t.Kind == TileKind.Portal)
                {
                    if (t.Pair < 0 || t.Pair >= MechanicTile.MaxPortalPairs) r.Add("Loop " + t.Position + " pair must be 0.." + (MechanicTile.MaxPortalPairs - 1) + ".");
                    else pairs[t.Pair] = pairs.TryGetValue(t.Pair, out int c) ? c + 1 : 1;
                }
            }
            foreach (var kv in pairs)
                if (kv.Value != 2) r.Add("Loop pair " + kv.Key + " has " + kv.Value + " end(s); it needs exactly 2.");
            if (level.FanCount > 64) r.Add("At most 64 fans per level.");
        }

        /// <summary>Checks that an arbitrary state is consistent with the level.</summary>
        public static ValidationResult ValidateState(LevelDefinition level, GameState state)
        {
            var r = new ValidationResult();
            if (state == null) { r.Add("State is null."); return r; }
            if (state.PlopCount != level.Plops.Count) { r.Add("State Plop count mismatch."); return r; }
            var cells = new HashSet<int>();
            for (int id = 0; id < state.PlopCount; id++)
            {
                int cell = state.CellOf(id);
                if (cell < 0 || cell >= level.CellCount) { r.Add("Plop " + id + " outside the grid."); continue; }
                if (level.IsWallCell(cell)) r.Add("Plop " + id + " inside a wall.");
                if (!cells.Add(cell)) r.Add("Plops overlap at " + level.PositionOf(cell) + ".");
                bool shouldSleep = level.IsMatchingNest(id, cell);
                if (state.IsAsleep(id) != shouldSleep)
                    r.Add("Plop " + id + " sleep flag inconsistent with Nest at " + level.PositionOf(cell) + ".");
            }
            return r;
        }

        /// <summary>Checks a collection of levels for duplicate ids and individual validity.</summary>
        public static ValidationResult ValidatePack(IEnumerable<LevelDefinition> levels)
        {
            var r = new ValidationResult();
            var ids = new HashSet<string>();
            foreach (var level in levels)
            {
                var single = Validate(level);
                foreach (var e in single.Errors) r.Add("[" + level?.Id + "] " + e);
                if (level != null && !ids.Add(level.Id)) r.Add("Duplicate level id '" + level.Id + "'.");
            }
            return r;
        }
    }
}
