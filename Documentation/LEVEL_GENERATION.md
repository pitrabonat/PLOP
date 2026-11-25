# Procedural level generation

## Phase 10 campaign (shipped): 160 levels, nine Worlds

`ExpandedCampaign` builds `Assets/Levels/Resources/Levels/PLOP_Pack_002.json` offline. Every level is solved
exhaustively, stores its BFS `optimalMoves`, replays through `GameEngine`, and passes strict production
verification. The [Phase 10 report](benchmarks/campaign_phase10.md) lists every level with its mechanics,
challenge score and greedy-player result.

| Levels | World | Focus |
|---|---|---|
| 1–6 | Cozy Nest | Phase 8 tutorials 1–5 (same IDs) and one classic puzzle: swipes, shared movement, Nests |
| 7–20 | Frosted Pond | Frost: two authored intros, then slides that must be stopped by walls, Plops or Nests |
| 21–34 | Breezy Meadow | Breeze fans (power 1–3), mixed with Frost; Level 30 is a Limited Moves challenge |
| 35–48 | Ember Hollow | Lava as a hazard the route must avoid, mixed with Frost and Breeze |
| 49–63 | Prism Observatory | Loops (portal pairs), then Loops with Lava; Level 60 is a challenge |
| 64–85 | Dewdrop Garden | Two-mechanic combinations plus reused Phase 8 pure-logic puzzles |
| 86–108 | Rainy Window | Harder combinations; Level 90 is a challenge |
| 109–133 | Twilight Attic | Three-mechanic boards (6×7); Level 120 is a challenge |
| 134–160 | Moonlit Clouds | Mastery: all four mechanics; Level 150 is a challenge |

- **Onboarding is faster than Phase 8:** each mechanic's first level is a tiny authored board with one short
  line (7 Frost, 21 Breeze, 35 Lava, 49 Loops); the second shows one twist; generated levels follow at once.
- **Every level in a mechanic World really uses it** on the optimal route (a slide, a push, a teleport, or a
  Lava alternative the player must avoid) — checked by `Phase10CampaignTests`.
- **Levels 64–160 are never solvable by the greedy "always closer" player**, and most combine mechanics.
- **Challenges** (30, 60, 90, 120, 150) set `moveLimit = optimalMoves + 2` and are verified within the limit.
- **Reuse:** 13 Phase 8 pure-logic puzzles (plus tutorials 1–5) keep their IDs, so their records and completions carry over.
- Average challenge by World: 8.3 → 24.9 → 28.0 → 33.8 → 34.9 → 40.6 → 47.5 → 53.7 → 61.1. The score is a
  heuristic, not a human playtest.

### Mechanic-aware quality search (`QualitySearch` v2)

`QualityTarget.Mechanics` (a `MechanicSet` of Frost, Lava, Breeze, Loops) lets the hill climber add, move,
retype and remove tiles alongside walls, Plops and Nests, within `MaxTiles`, `MaxLava`, `MaxFans` and
`MaxLoopPairs`. `RequiredUse` rejects boards whose optimal route ignores a mechanic, and `MinMechanicSteps`
counts optimal steps whose outcome depends on forced movement. `PuzzleInsight` gained `Slides`, `Pushes`,
`Teleports`, `HazardAlternatives` (Lava moves next to the route), `MechanicSteps` and `MechanicKindsUsed`; the
challenge score adds small weights for hazard alternatives, mechanic steps and variety. The greedy player and
the random-swipe model treat a Lava move as a failure sink (`StateGraph.FailIndex`). Classic targets (no
mechanics) consume the RNG exactly as before, so Phase 8 output is unchanged.

### Rebuild and verify Phase 10 content

```bash
export NUGET_PACKAGES=/Volumes/PLOP_DEV/Cache/NuGet
cd DotNet   # --expanded resolves the project root as ".."
dotnet run -c Release --project Plop.Bench -- --expanded            # ~90 s on 10 threads; writes Pack_002 + report
dotnet run -c Release --project Plop.Bench -- --verify-production ../Assets/Levels/Resources/Levels/PLOP_Pack_002.json
dotnet run -c Release --project Plop.Bench -- --deadlocks ../Assets/Levels/Resources/Levels/PLOP_Pack_002.json \
  ../Assets/Levels/Resources/Levels/PLOP_Deadlocks_002.bytes 600000
dotnet run -c Release --project Plop.Bench -- --mech-probe 3        # quick mechanic search probe
```

The same seed (`0x504C4F5031300000`) reproduces the pack; a sequential re-search of sampled slots is compared
during the build. Regenerate the deadlock sidecar whenever rules, state encoding or the pack change.

## Phase 8 campaign (archived, `Assets/Levels/Archive/PLOP_Pack_001.json`)

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

**Phase 10 additions:** a second tool row places Frost, Lava, Fan (direction and power 1–3) and Loop ends (pair
number; two ends per pair); mechanic tiles go on plain floor only. A Move limit field sets a Limited Moves
challenge. **Search mechanic level** runs `QualitySearch` with the chosen mechanics (all required on the
optimal route). **Inspect chains** walks every reachable state through `MoveRules` (`ForcedChainInspector`)
and reports the longest forced chain, reachable Lava failures and any swipe that would not resolve (loop
risk — must be none to ship; the shipped campaign is tested for it). During replay, **Try a swipe** and Step
show an effect-resolution list for the last command (steps, slides, pushes, teleports, settles, Lava).

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
