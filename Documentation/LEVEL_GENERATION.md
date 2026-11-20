# Procedural Level Generation (`Plop.Generation`, `GeneratorInfo.Version = 1`)

## PRNG

`SplitMix64` (Steele, Lea & Flood 2014; Vigna's reference constants). Platform-independent and verified
against reference outputs in tests. `System.Random` is never used for generation. Sub-seeds for batches are
derived with `SplitMix64.Derive(baseSeed, index)`.

**Reproducibility contract:** same seed + same `GeneratorSettings` + same generator version + same
duplicate-registry contents ⇒ identical level (byte-identical JSON). Any change to the PRNG, candidate
construction, ordering of random draws or acceptance rules must bump `GeneratorInfo.Version`.

## Inputs (`GeneratorSettings`)

`Width`, `Height` (4–7 used so far), `PlopCount` (1–3 used), `WallDensity`, `TargetDifficulty` (or any),
`MaxAttempts`, `SolverMaxStates`, `MinSolutionLength` (default 4), `MaxSolutionLength`, `Weights`.

## Pipeline per attempt (`LevelGenerator.Generate(seed, settings, registry)`)

1. **Geometry:** `Width × Height` grid.
2. **Walls:** each cell is a wall with probability `WallDensity`; only the largest 4-connected floor region is
   kept (isolated pockets become walls). Reject if fewer than `2·plops + 2` floor cells.
3. **Plops:** distinct random floor cells, colors Red, Blue, Yellow… in id order.
4. **Nests:** distinct random floor cells not used by Plops, one per Plop color.
5. **Structural validation** (`LevelValidator`).
6. **Solve:** full `StateSpaceAnalyzer` run within `SolverMaxStates`.
   Incomplete → `SearchBudget` rejection (never assumed solvable). No path → `Unsolvable`.
7. **Triviality / length:** shortest solution `< MinSolutionLength` → `Trivial`; `> MaxSolutionLength` → `TooLong`.
8. **Difficulty** (`DifficultyEstimator`); if a target is set and differs → `DifficultyMismatch`.
9. **Duplicates:** canonical form already in the registry → `Duplicate`.
10. **Replay verification:** the shortest solution is replayed through `GameEngine` → `VerificationFailed` if not.
11. **Accept:** id = `PLOP-` + first 12 hex digits of FNV-1a-64(canonical form); metadata stores seed,
    parameters + attempt, generator/solver versions, solution and difficulty metrics.

After `MaxAttempts` the result has `Success = false`, per-reason rejection counts and a message — never an
infinite loop. `BatchGenerator` additionally caps calls and optional wall-clock time.

## Duplicate detection (`LevelCanonicalizer`)

Canonical form = lexicographic minimum over the **8 square symmetries** (identity, mirrors, rotations,
transposes; a rotated W×H board becomes H×W) of a string containing size, wall bitmap and per-color
(sorted Plop cells, sorted Nest cells) groups, where **color groups are sorted by content** (color relabeling
invariance). These transforms preserve the rules exactly (front-to-back is relative to the move direction and
tie-breaks never matter); a test asserts every transformed level keeps the same shortest solution length.

## Difficulty (`DifficultyEstimator`) — a heuristic, not a measure of human challenge

`score = Σ weight·metric`, defaults (`DifficultyWeights`):

| Metric | Weight |
|---|---|
| Shortest solution length | 1.0 per move |
| Extra Plops (beyond 1) | 3.0 each |
| log2(reachable states) | 0.6 |
| Dead-end ratio (dead-end states / reachable) | 8.0 |
| Misleading moves per solution step | 2.0 |
| Trap moves (into dead ends) per step | 6.0 |
| Coordination moves (a Plop held back by an obstacle while others move) | 1.5 each |
| Forced-step ratio (only one optimal move) | −3.0 |
| Wall density | 3.0 |

Categories: score < 9 **Tutorial**, < 16 **Easy**, < 26 **Normal**, < 38 **Hard**, else **Expert**.
Thresholds were calibrated on generator v1 output (60 levels per profile): 1-Plop levels score ~7–14,
2-Plop ~15–38, 3-Plop ~25–75. All metrics are stored per level in `dev.difficulty.metrics`.

## Benchmark

`dotnet run -c Release --project DotNet/Plop.Bench -- --count 100 --pack Assets/Levels/Resources/Levels/PLOP_Pack_001.json --report Documentation/benchmarks/generation_benchmark_v1.md`

Latest actual results: [benchmarks/generation_benchmark_v1.md](benchmarks/generation_benchmark_v1.md).
Summary (base seed `0x504C4F5021`): 100/100 accepted from 181 candidates in 0.71 s; the only rejections were
81 trivial (solution < 4 moves); 0 unsolvable, 0 search-budget, 0 duplicates; all 100 solutions replay through
`GameEngine`; pack verification passed. A later independent re-run (after refactoring profiles into
`GeneratorProfiles`) reproduced `PLOP_Pack_001.json` **byte-for-byte** in 0.42 s (wall time varies run to run).

**Observation:** random placement is almost always solvable because grid edges and walls let Plops
desynchronize easily; the main quality filter is triviality/difficulty, not solvability. Profiles with denser
walls or more Plops produce more unsolvable/dead-end-heavy candidates.

## Known limitations / next steps

- Random placement yields many "walk to the nest" layouts at the easy end; designer curation (Level Lab) or
  goal-directed generation (reverse search from solved states, or mutating good levels) will improve quality.
- Difficulty weights are hand-calibrated; validate against playtest data.
- `GeneratorProfiles.Default()` is part of the pack's reproducibility; changing it changes the pack.
