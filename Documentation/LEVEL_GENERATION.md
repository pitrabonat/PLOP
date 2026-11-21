# Procedural level generation

## Production campaign

The first 100 levels live in `Assets/Levels/Resources/Levels/PLOP_Pack_001.json`.
Generation and analysis run offline in the .NET builder or Unity Level Lab.

| Levels | Focus |
|---|---|
| 1–10 | Authored tutorials with instructions on the relevant board |
| 11–25 | Two-Plop logical challenges and short solutions |
| 26–50 | Shared movement and wall interactions, with occasional three-Plop peaks |
| 51–75 | Three-Plop planning and settlement choices |
| 76–100 | Mastery of the same rules, including required detours and settlement traps |

After onboarding, each group of five has an explicit peak and recovery slot.
The recovery score ceiling is below the preceding peak floor. Board shapes
and density also vary within a band. The score remains a heuristic pending
human playtesting.

## Rebuild and verify

```bash
export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet
dotnet run -c Release --project DotNet/Plop.Bench -- --production \
  --pack Assets/Levels/Resources/Levels/PLOP_Pack_001.json \
  --report Documentation/benchmarks/generation_benchmark_v2.md
dotnet run -c Release --project DotNet/Plop.Bench -- --verify-production Assets/Levels/Resources/Levels/PLOP_Pack_001.json
```

The production builder checks the full catalog and repeats generation with the
same seed. The pack write requires a complete, valid, byte-identically
reproduced campaign. See the [executed benchmark](benchmarks/generation_benchmark_v2.md).
The [v1 benchmark](benchmarks/generation_benchmark_v1.md) describes the
historical prototype catalog, which is available at the
`checkpoint/pre-puzzle-intelligence` tag.

## Candidate pipeline

1. Use SplitMix64 to place walls and retain the largest connected floor region.
   Place distinct Plops and Nests on its free cells.
2. Validate structure and exhaustively analyze reachable states within the
   profile's budget. Reject unsolvable, incomplete, too-short and too-long
   candidates.
3. Apply slot-specific score and quality filters. These use shortest length
   and turns, separate wall use and coordination, and evidence of required
   detours, forced Nest order, or settlement traps. The state graph also
   records shortest-path multiplicity, branching and dead ends for inspection.
4. Reject exact matches under the eight board symmetries and color relabeling.
   Production also rejects a board with the same Plop and Nest arrangement
   and one wall changed, under the same transformations.
5. Replay the shortest solution through `GameEngine`. Use the existing
   canonical board hash as the ID and store the evidence in developer metadata.

`ProductionCatalog` stops at the first unfilled slot after 400 candidates.
`VerifyProductionPack` checks all 100 levels for structural validity, canonical
and unique IDs, duplicates, and exact shortest replay. It also verifies Undo
on each solution step and Restart after completion. The editor and CLI share
that gate.

## Difficulty metrics

`DifficultyEstimator` retains the calibrated weights for shortest length,
extra Plops, log reachable states, dead-end ratio, misleading and trap moves,
coordination moves, forced choices and wall density. The category thresholds
are 9, 16, 26 and 38. The new graph evidence has separate acceptance rules so
the historical score remains comparable.

`wallInteractionMoves` counts steps on the selected shortest replay where a
wall holds a Plop while another moves. `wallDetour` measures the extra static
walking distance to nearest matching Nests due to walls. See
[SOLVER.md](SOLVER.md) for graph proofs and settlement metrics.

## Level Lab

Use **PLOP → Level Lab**. Load a pack or import JSON, filter by name, ID or
difficulty, and select a board for editing or verified solution preview.
Use Move up, Move down, Replace selected, or Remove selected to arrange the
working catalog. **Generate 100-level campaign** builds in campaign order.
The standard batch remains available for exploration. Save writes the working
pack, with strict validation when the destination is the production asset.

## Reproducibility

Identical seed, settings, generator version and duplicate-registry contents
yield identical level JSON. The pack recipe pins slot profiles and seed
derivation. Bump `GeneratorInfo.Version` when candidate construction, acceptance
or emitted metadata change. Geometry changes create a new canonical ID.
