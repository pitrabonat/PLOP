# Level Format (JSON, `schemaVersion: 1`)

Read/written only by `Plop.Core.LevelSerializer` (dependency-free `MiniJson`). Output is deterministic
(stable key order, walls sorted by y then x) so diffs are reviewable.

## Level

```json
{
  "schemaVersion": 1,
  "id": "PLOP-870CEC087725",
  "name": "Level 1",
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
