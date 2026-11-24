# Puzzle quality pipeline (Phase 8)

## Why the Phase 7 campaign felt easy

Phase 7 used a difficulty score based on the state graph: its size, dead-end ratio, solution length and detours. That score did not model how a player thinks. The [Phase 7 audit](benchmarks/campaign_audit_phase7.md) replayed every level with two simple player models:

- **Greedy player.** Always takes the unvisited swipe that brings Plops closest to a free matching Nest. It **solved 72/100 levels**, many in the optimal number of moves.
- **Random swiper.** The exact chance that random swipes finish the level. It was high on many mid-campaign boards.
- **Traps.** 88 levels had no tempting or early-settle trap.
- **Counter-intuitive steps.** Levels 76–100 averaged only 1.7.
- **Wasted boards.** About 40% of floor cells were never touched, and routes were repetitive.

The rules were not the problem. **Selection was.** A search that rewards the player-model metrics (below) finds deep boards with the same mechanics and board sizes.

## Metrics (`PuzzleInsight`)

`PuzzleInsight.Analyze` runs on the exact state graph (`StateSpaceAnalyzer.AnalyzeWithGraph`).

| Metric | Meaning |
|---|---|
| Optimal | Verified shortest solution (BFS). It is stored as `optimalMoves` and is the Perfect target. |
| GreedySolves / GreedyLength | Whether the "always closer" player finishes, and in how many moves. |
| RandomSolveProbability | Exact absorbing-Markov probability that random state-changing swipes finish within 2·opt + 4. |
| CounterIntuitiveSteps | Optimal steps where every swipe that gets "closer" is wrong. |
| RetreatSteps | Optimal steps that must move a Plop away from its Nest. |
| DecisionPoints | Route steps where an alternative swipe is costly or fatal. |
| TemptingTraps | Greedy-preferred swipes that lead into a dead end. |
| EarlySettleTraps | Swipes that settle a Plop and make the level unsolvable. |
| SettlementOrderMatters | Some pair of Nests must be filled in a fixed order. |
| RequiresCoordination | Every solution holds one Plop back while another moves. |
| SleeperBlocks / WallBlocks | Optimal steps that use a sleeping Plop or a wall as a stopper. |
| RepetitionRatio | Share of repeated consecutive swipes on the route. This is a penalty. |
| UnusedSpaceRatio | Share of floor that no Plop touches on the route. This is a penalty. |
| ShortestSolutionCount / SolvableAlternatives | How many routes are optimal, and how much slack exists. |
| Patterns | Design patterns detected: A coordination, B settlement order, C retreat, D strategic blocking, E multi-step planning, F false opportunity, G efficiency. |

## Challenge score and weights

`Challenge` is a weighted sum. The weights live in `ChallengeWeights` (`Assets/Scripts/LevelGeneration/PuzzleInsight.cs`) and are configurable. The defaults:

| Term | Weight |
|---|---|
| Greedy player fails | 6.0 |
| Greedy detour (per greedy/opt − 1, capped at 2) | 3.0 |
| Random resistance (× −log10 P(random)) | 2.5 |
| Counter-intuitive step | 2.0 |
| Retreat step | 1.5 |
| Decision point | 1.0 |
| Tempting trap | 2.0 |
| Early-settle trap | 2.5 |
| Settlement order | 2.0 |
| Required coordination | 1.5 |
| Sleeper block | 1.0 |
| Repetition penalty (× ratio) | −4.0 |
| Unused-space penalty (× ratio) | −3.0 |

`CampaignPlan.Categorize` maps the score to a class using the thresholds **8 / 18 / 30 / 42**:

| Class | Score |
|---|---|
| Tutorial | below 8 |
| Easy | 8 to 18 |
| Normal | 18 to 30 |
| Hard | 30 to 42 |
| Expert | 42 and above |

## Search (`QualitySearch`)

The search is deterministic hill climbing. Each run is seeded per campaign slot.

- **Mutations:** toggle a wall, move a Plop, move a Nest, swap two Nests.
- **Penalty:** measures the distance from the slot's `QualityTarget` limits:
  - board size, Plop count and optimal range;
  - challenge band;
  - greedy must fail;
  - minimum counter-intuitive steps, traps and patterns;
  - maximum random-solve, repetition and unused floor;
  - no full wall lines;
  - connected floor.
- **Retries:** 24 restarts × 160 steps, with a 250k-state solver cap.
- **Content checks:** `ProductionCatalog` rejects exact and near duplicates (one wall changed, any symmetry) and runs strict verification on every result.

## Campaign plan (`CampaignPlan`)

- **Tutorials:** levels 1–10 are authored and keep their IDs.
- **Bands:** levels 11–100 are split at 15, 30, 50 and 75. Each band raises board size, Plops (up to 4, only on readable boards) and the target challenge.
- **Waves:** within each block of five, slot 3 is a *peak* and slot 4 is a *breather*. A breather must score below the level before it, so difficulty comes in waves.
- **Daily pool:** 60 levels from separate targets, all greedy-fail with traps, and none duplicating a campaign level.

Rebuild from the repository:

```bash
cd DotNet && dotnet run -c Release --project Plop.Bench -- --production
```

This runs a parallel search, then strict verification, then a sequential re-search that must be identical. It writes `PLOP_Pack_001.json`, `PLOP_Daily_001.json` and `PLOP_Pack_001_SlotHistory.json`. After a rebuild, regenerate the deadlock sidecar with `--deadlocks`.

## Result (Phase 8 pack)

The full report is in the [Phase 8 audit](benchmarks/campaign_audit_phase8.md). The per-level overview is in [campaign_insights_phase8.tsv](benchmarks/campaign_insights_phase8.tsv), produced with `--insights`.

| Measure | Phase 7 | Phase 8 |
|---|---|---|
| Greedy solves | 72/100 | 14/100, all at level 25 or earlier |
| Levels with no trap | 88 | 21, mostly tutorials and early Easy |
| Avg counter-intuitive steps, 76–100 | 1.7 | 4.32 |
| Avg challenge by band | – | 3.6 → 23.6 → 39.1 → 45.9 → 55.1 |
| Flagged trivial by the Level Lab | – | 11: the tutorials plus levels 12 and 13 |

## Limits of the current mechanics

These limits were found while tuning:

- **Stepping rule.** Plops move one cell per swipe and every awake Plop moves together. Depth therefore comes from *relative* position: walls, edges and sleeping Plops act as stoppers. Small boards (4×4) run out of distinct relative arrangements quickly.
- **Board size.** The best challenge on 5×5 boards with two Plops plateaus around 35. Expert levels need 6×6 or larger, or 3–4 Plops.
- **Plop count.** Four Plops on a 6×7 board can exceed 250k–2M states. That is fine offline, but it is close to the on-device hint budget (300k states / 200 ms).
- **Trap variety.** Settlement-order traps dominate the late game. Without new rules, the late campaign repeats the same "who sleeps first" idea.

## Recommended future mechanics

These are not implemented. Each adds decisions without adding reading load.

1. **Pillow switch.** A floor button that toggles one wall while a Plop rests on it. It creates temporary routes and timing decisions.
2. **Sticky blanket.** A cell that holds a Plop for one swipe. Plops move apart without new controls.
3. **Shared Nest.** One Nest accepts either of two colours. It creates real choices instead of fixed pairings.
4. **One-way cushion.** A cell that can only be entered from one direction. It creates strong planning with a clear visual cue.
5. **Wake-up bell.** A cell that wakes an adjacent sleeping Plop. It allows deliberate un-settling, which reverses the early-settle trap.

Before shipping any of these, validate each with the existing `PuzzleInsight` metrics and a small authored set.
