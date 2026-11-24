# Level Format (JSON, `schemaVersion: 1`)

Read/written only by `Plop.Core.LevelSerializer` (dependency-free `MiniJson`). Output is deterministic
(stable key order, walls sorted by y then x) so diffs are reviewable.

## Level

```json
{
  "schemaVersion": 1,
  "id": "PLOP-870CEC087725",
  "name": "Level 1",
  "optimalMoves": 4,
  "width": 4,
  "height": 4,
  "walls": [[0, 0], [1, 0], [2, 0], [1, 2]],
  "plops": [ { "id": 0, "color": "Red", "pos": [1, 3] } ],
  "nests": [ { "color": "Red", "pos": [1, 1] } ],
  "dev": {
    "generatorVersion": 1,
    "seed": "0x9754CCA21694E164",
    "generatorParams": "w=4 h=4 plops=1 walls=0.2 target=any minLen=4 maxLen=60 solverStates=300000 attempt=10",
    "solverVersion": 1,
    "solution": "LDDR",
    "solutionLength": 4,
    "difficulty": { "score": 6.15, "category": "Tutorial", "metrics": { "solutionLength": 4, "...": 0 } }
  }
}
```

- Coordinates are `[x, y]`, origin bottom-left, y up (see GAME_RULES.md).
- **Runtime data** lives outside `"dev"`. Optional `"instruction"` is a short
  tutorial line shown during gameplay. The game loads packs with `includeDevData: false`.
- **`optimalMoves`** (runtime, optional, added in Phase 8 without a schema bump): the verified BFS shortest
  solution length. It is the Perfect Moves target ("Moves 7 / 9"). Strict production verification fails if it
  differs from the solver. Older packs without it simply show no Perfect target.
- **Developer-only data (`"dev"`, optional):** generator version, seed (hex string, avoids JSON number
  precision loss), generator parameters, solver version, verified shortest solution (`U/D/L/R`),
  difficulty score/category/metrics, optional `author`, `notes`.
- Colors: `Red, Blue, Yellow, Green, Purple, Orange` (exact spelling). Plop ids must be `0..n-1`.

## Pack

```json
{ "schemaVersion": 1, "packId": "PLOP_Pack_001", "levels": [ { ...level... }, ... ] }
```

Shipped pack: `Assets/Levels/Resources/Levels/PLOP_Pack_001.json` (100 levels, authored onboarding followed by staged waves,
named "Level 1".."Level 100"). Loaded at runtime via `Resources.Load<TextAsset>("Levels/PLOP_Pack_001")`.
Production IDs derive from canonical board geometry and survive renaming and reordering.

Daily Puzzle pool: `PLOP_Daily_001.json` (same pack format, 60 levels, never duplicating a campaign board).
The puzzle for a local calendar date is `DailySchedule.PoolIndex(date, 60)` (epoch 2026-01-01, stride 37).

## Slot history (save migration)

`PLOP_Pack_001_SlotHistory.json` records, for every campaign slot, its current ID and every earlier ID that
shipped in that slot:

```json
{ "schemaVersion": 1, "packId": "PLOP_Pack_001",
  "slots": [ { "slot": 10, "id": "PLOP-…", "previousIds": ["PLOP-…"] } ] }
```

`--production` appends to it on every rebuild. At launch `LevelProgress` maps saved completed/unlocked/resume
IDs from `previousIds` to the slot's current ID (keeping the old IDs), so rebuilding the campaign never
resets a player's progress.

## Meta saves (PlayerPrefs, JSON)

- `plop.wallet.v1`: `{version, balance, earned, grants[], owned[]}`. Grant keys make every reward one-time.
- `plop.records.v1`: `{version, levels:[{id, best, perfect, completions}]}`.
- An unreadable value is copied to `<key>.corrupt` and the store starts empty rather than crashing.

## Game state (save/hint snapshots)

```json
{"schemaVersion":1,"levelId":"PLOP-…","plops":[{"id":0,"pos":[1,2],"asleep":false}]}
```

`LevelSerializer.StateFromJson` validates the level id, ids, bounds and state consistency.

## Validation on load

Unknown schema versions, missing properties, wrong types, unknown colors/directions, duplicate JSON keys and
out-of-range sizes throw `FormatException`. Structural rules (overlaps, walls, nest counts…) are checked by
`LevelValidator`; solvability by `LevelVerifier`.

## Versioning

Bump `LevelSerializer.SchemaVersion` for incompatible changes (e.g. new tile types) and keep reading older
versions. Generator/solver versions are recorded per level so stale metadata can be detected.
