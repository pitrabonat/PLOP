# Phase 11 puzzle balance report

How puzzle quality is measured, what the simulated players are, which floors new levels had to pass, and what the rebalanced campaign (`PLOP_Pack_003`) and Daily pool (`PLOP_Daily_002`) look like. **All results are offline and heuristic. No person has played the new levels yet.** Use [PLAYTEST_GUIDE.md](PLAYTEST_GUIDE.md) to collect real data.

## The evaluator (`Assets/Scripts/LevelGeneration/PuzzleQuality.cs`)

`PuzzleQuality.Evaluate(level, analysis)` takes the full state graph from `StateSpaceAnalyzer.AnalyzeWithGraph` and reports:

| Metric | Definition |
|---|---|
| Meaningful decisions / density | Steps on the shortest route where a costlier or fatal alternative looks at least as good to the nest-distance heuristic, or which settle a Plop. Repeats of the same choice count once. Density = decisions ÷ optimum. |
| Coordination | Each Plop is solved on its own (others removed). Coordination is high when solo optima differ from the joint optimum, or a Plop cannot be solved alone. |
| Settlement dependency | Whether the settling order matters, plus early-settle traps (settling a Plop that later blocks a route). |
| Mechanic dependency | Removal test per tile kind. **Essential**: removing it changes the optimum or solvability. **Hazard**: Lava that the best route avoids but greedy routes hit. **Incidental**, **Decorative**: never matters. **Unknown**: the analysis hit its budget. |
| Planning depth | The smallest look-ahead k (1–4) with which a heuristic player solves without revisiting a board. "> 4" means none does. |
| Misleading choices | Shortest-route states where the best-looking swipe is not optimal. |
| Route efficiency | Longest run of one direction, the share of repeats, and unused floor. |
| Solution diversity | Number of shortest solutions, solutions within +2 moves, and the number of good first swipes. |
| Quality (0–100) | A weighted sum: 30·density/0.5, 12·coordination, 8·settlement, 15·(depth−1)/3, 10·mechanic contribution, 10·misleading/2, 15·efficiency. Then −20·(random success/0.5) and −8 per decorative kind. Clamped to 0–100. |

Flags: `random-solvable` (random success ≥ 25 %), `greedy-solvable`, `shallow` (depth ≤ 2), `few-decisions` (density < 0.2), `repetitive` (repeat share > 0.4 or a run ≥ 4), `open` (> 50 % unused floor), `decorative-mechanic`, `idle-tiles`, `long-for-its-decisions` (optimum ≥ 10 and density < 0.25), `easy-for-position` (challenge more than 12 below the World median), `similar-to-N` (tile-aware layout similarity ≥ 85 %).

## Simulated players (`SimulatedPlayers.cs`)

| Player | Model |
|---|---|
| Random | 400 seeded runs of uniform random swipes, with a step limit of 4·optimum + 20. Reports success and Lava rates. |
| Greedy | Always takes the swipe that most reduces total nest distance (no revisits). |
| Look-ahead 2 / 3 | Searches 2 or 3 swipes ahead with the same heuristic. |
| Limited information | Each awake Plop votes for swipes that shorten its own walk to its Nest. Avoids a swipe whose next cell is Lava, but still falls into Lava through slides and fans. Never revisits a board. |
| Optimal | BFS (always solves; used as the reference). |

These players model typical shortcuts (rush to the Nest, avoid the obvious hazard, think two swipes ahead). They do not model learning, fatigue or creativity.

## Acceptance floors per learning stage (`BalancedCampaign.FloorsFor`)

| Stage | Depth ≥ | Random ≤ | Density ≥ | Other |
|---|---|---|---|---|
| A Discovery | – | 0.6 | – | ≥ 1 decision, the new tile must be Essential |
| B Application | – | 0.3 | 0.25 | ≥ 2 decisions, greedy must fail |
| C Reasoning | 3 | 0.12 | 0.25 | quality ≥ 45, greedy must fail |
| D Combination | 3 | 0.08 | 0.25 | quality ≥ 50, two mechanics both Essential |
| E Mastery | 4 | 0.03 | 0.30 | quality ≥ 65, three mechanics, at least one tempting trap |
| Breather | 2 | 0.2 | 0.25 | quality ≥ 40 |
| Mixed (64–160, Daily) | 3 | 0.05 | 0.30 | quality ≥ 55 |

Every stage also requires: no decorative mechanic, a longest single-direction run ≤ 3, at most one idle tile, strict BFS verification of the stored solution, no duplicate or near-duplicate, and < 85 % similarity to every other level.

## Campaign before → after

| | Phase 10 (`PLOP_Pack_002`) | Phase 11 (`PLOP_Pack_003`) |
|---|---|---|
| Mean quality | 60.6 | **71.6** |
| Mean decision density | 0.34 | 0.41 |
| Random player, mean success | 9.2 % | 5.3 % |
| Random ≥ 25 % | 14 | 7 (L1, 2, 3, 6, 21, 22, 49) |
| Greedy solves | 14 | 8 (L1, 2, 3, 6, 7, 21, 35, 49) |
| Look-ahead 2 / 3 solves | 40 / 59 | 10 / 29 |
| Limited-information solves | 34 | 21 |
| Planning depth 1 / 2 / 3 / 4 / > 4 | 14 / 27 / 21 / 18 / 80 | 8 / 2 / 20 / 27 / 103 |
| Bands T / E / N / H / X | 12 / 35 / 32 / 51 / 30 | 8 / 10 / 38 / 61 / 43 |

Per World (mean quality, before → after): Cozy Nest 10 → 16 (five teaching levels; low by design), Frosted Pond 39 → 66, Breezy Meadow 37 → 69, Ember Hollow 57 → 68, Prism Observatory 54 → 66, Dewdrop Garden 64 → 71, Rainy Window 71 → 78, Twilight Attic 74 → 77, Moonlit Clouds 77 → 81. Exact per-World tables are in `audit_before.md` and `audit_after.md`.

### Solution statistics (Pack_003)

- Shortest solutions: min 1, median 11, mean 11.0, max 17 moves.
- 55 levels have a unique shortest solution (Phase 10: 45). The median level has 2 shortest solutions.
- The largest state graph is 25,930 states (Level 136). BFS solves every level in 1.3 ms on average on an Apple M4.

## Limited Moves

| Level | Optimum | Limit | Slack | Role |
|---|---|---|---|---|
| 30 | 9 | 11 | +2 | Introduction, forgives two slips |
| 45 | 10 | 12 | +2 | |
| 60 | 10 | 12 | +2 | |
| 80 | 9 | 10 | +1 | |
| 100 | 12 | 13 | +1 | |
| 120 | 15 | 16 | +1 | |
| 140 | 13 | 13 | 0 | Expert: the optimum only |
| 155 | 15 | 15 | 0 | Expert: the optimum only |

Phase 10 had five challenges, each with +2 slack. When a limit is set, removed or changed, the level gets a new id, because the rules changed. Completions of three boards whose rule only got looser carry over through `aliases` in `PLOP_Pack_002_Order.json`.

## Perfect scoring audit

- A Perfect needs moves == the BFS-verified optimum and no hint during that visit (`LevelRecords.Submit`, `_assisted`). Undo shortens the counted history, so an undone detour still counts as Perfect.
- Every stored optimum was re-verified by BFS in `VerifyProductionPack`. A move count below the optimum is rejected as corrupt.
- On the slack-0 challenges (140, 155), every success is a Perfect by construction. That is consistent and intended.
- Perfect Dust is paid once per level id (`perfect:ID`). Replays never pay twice (economy simulation: Perfect Dust ≤ 160 × 4).

## Daily pool (`PLOP_Daily_002`)

| | Daily_001 | Daily_002 |
|---|---|---|
| Puzzles | 60, classic only | 20 classic (kept from Daily_001) + 10 Frost, 10 Breeze, 10 Lava, 10 Loops |
| Mean quality | 60.4 | 73.6 |
| Mean density | 0.26 | 0.41 |
| Random ≥ 5 % | 22 | 5 |
| Look-ahead 2 / 3 solves | 4 / 13 | 0 / 7 |
| Flagged | 41 (long-for-decisions 25, repetitive 22, few-decisions 18) | 8 (repetitive 6, easy-for-position 2) |
| Bands | E 6 · N 46 · H 8 | E 2 · N 28 · H 25 · X 5 |

Gating: `DailySchedule.PickIndex` serves only puzzles whose mechanics the player has completed. Frost counts after Level 6, Breeze after 21, Lava after 35 and Loops after 49. Among eligible puzzles, the date picks one with stride 37. The choice is saved per calendar day (`plop.daily.pick.v2`), so finishing a World mid-day never swaps a puzzle you started. A new player cycles through the 20 classic puzzles; a player past Level 49 sees all 60 once per 60-day cycle. Daily puzzles never have a move limit. Rewards stay one per date (`daily:yyyy-MM-dd`). There are no streaks and nothing punishes a missed day.

## Reproduce

```bash
cd DotNet && dotnet run -c Release --project Plop.Bench -- --balanced
dotnet run -c Release --project Plop.Bench -- --quality-audit ../Assets/Levels/Resources/Levels/PLOP_Pack_003.json out.json out.md
```

`--balanced` is deterministic (seed `0x504C4F5031310000`). It writes the pack, the Daily pool, the migration order file and the reports in this folder. About 2 minutes on 10 threads.
