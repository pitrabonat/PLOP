# Phase 11 economy balance report

Dream Dust is the only currency. It is earned by playing and spent on hints and cosmetics. There are no ads, no real-money purchases, no paid currency, no accounts and no server. This report audits the Phase 10 rules and simulates four kinds of player. **The behaviour rates are assumptions, not measurements.** Replace them with playtest data when you have some ([PLAYTEST_GUIDE.md](PLAYTEST_GUIDE.md)).

## Rules (unchanged amounts)

| Source / sink | Amount | Once per | Key |
|---|---|---|---|
| First completion of a level | +8 | level id | `complete:ID` |
| First Perfect of a level | +4 | level id | `perfect:ID` |
| Daily gift | +10 | calendar day | `gift:date` |
| Daily Puzzle | +12 | date | `daily:date` |
| Daily Puzzle Perfect | +6 | date | `dailyPerfect:date` |
| Hint | −15 | level × exact board state | `hint:ID:state` |
| Cosmetics | 40–120 each (18 items, 1,300 in total) | item | `owned` |
| Milestone cosmetics | free on reaching a milestone (11 items, some also buyable) | item | – |

Hints are free on levels 1–6 and on every level with an instruction (21, 35 and 49). Daily Puzzles charge like campaign levels (`HintIsFree` excludes the Daily), and grace applies there too. Re-showing a hint you already paid for on the same board is free.

### Audit findings

| # | Finding | Severity | Action |
|---|---|---|---|
| E-1 | A player whose Dust has run out (spent on cosmetics, or a heavy hint user) can be stuck with no way to get help. | P2 | **New `HintPolicy` grace** (below) |
| E-2 | Rewards cannot be farmed. Every grant is keyed and paid once; replays and repeat Daily completions pay nothing. | OK | Covered by `Simulation_IsDeterministic_AndPaysEachRewardOnce` |
| E-3 | Nothing punishes a missed day: gifts and Daily Puzzles never stack, decay or need a streak. | OK | – |
| E-4 | Migration to Pack_003 never re-pays a reward: completions carry over by id, and replaced puzzles are new ids that pay once as new content. | OK | `MigrateFromRebalancedCampaign` |
| E-5 | Damaged wallet data never yields negative Dust, and a torn write restores from the backup. | OK | `Phase11StabilityTests` fuzzing |

### Hint grace (`Assets/Scripts/Meta/Rewards.cs` → `HintPolicy`)

When a hint costs more Dust than the player has, and the player has already tried the level **twice** this visit (a restart after moving, a Lava failure, running out of moves, or a proven dead end), that level's first hint is free. This happens **once per level id, ever**, recorded as the wallet spend key `hintgrace:ID`. It grants no Dust, it does not apply while Dust is sufficient, and it cannot be farmed by restarting. The hint card says "This one is on the house." Before the second try it says how many more tries unlock the free hint.

## Simulation (`EconomySimulator`, `dotnet run --project DotNet/Plop.Bench -- --economy`)

The simulation uses the real `DreamDustWallet` and `RewardRules`, the shipped `PLOP_Pack_003` (difficulty from its bands) and the real cosmetic prices. Each profile was simulated 200 times with fixed seeds; medians are shown. A simulated player who wants a hint has always restarted twice first, so grace applies. Players buy the cheapest cosmetic whenever their balance minus a reserve allows.

| Profile | Levels/day | Perfect rate | Wants a hint | Daily Puzzle | Gift | Reserve |
|---|---|---|---|---|---|---|
| Casual | 2 | 25 % | 12 % ×1 | 30 % of days | 50 % | 0 |
| Regular | 5 | 45 % | 8 % ×1 | 70 % | 80 % | 0 |
| Enthusiast | 12 | 75 % (+ Perfect replays) | 2 % ×1 | 95 % | 95 % | 0 |
| High-Hint | 4 | 20 % | 55 % ×2 | 50 % | 70 % | 30 |

Hint and Perfect chances scale with level difficulty.

### Results at 10 / 25 / 50 / 100 / 160 levels

| Profile | Levels | Days | Dust earned | Hints bought | Grace hints | Hints refused | Cosmetics owned | Balance |
|---|---|---|---|---|---|---|---|---|
| Casual | 10 | 5 | 134 | 0 | 0 | 0 | 2/18 | 45 |
| | 25 | 13 | 332 | 3 | 0 | 0 | 5/18 | 45 |
| | 50 | 25 | 640 | 7 | 0 | 0 | 9/18 | 61 |
| | 100 | 50 | 1,294 | 15 | 1 | 0 | 15/18 | 74 |
| | 160 | 80 | 2,100 | 28 | 1 | 0 | 18/18 | 388 |
| Regular | 10 | 2 | 132 | 0 | 0 | 0 | 1/18 | 82 |
| | 25 | 5 | 324 | 2 | 0 | 0 | 4/18 | 90 |
| | 50 | 10 | 650 | 4 | 0 | 0 | 9/18 | 92 |
| | 100 | 20 | 1,304 | 9 | 0 | 0 | 16/18 | 113 |
| | 160 | 32 | 2,068 | 20 | 0 | 0 | 18/18 | 467 |
| Enthusiast | 10 | 1 | 140 | 0 | 0 | 0 | 0/18 | 140 |
| | 25 | 3 | 348 | 0 | 0 | 0 | 6/18 | 56 |
| | 50 | 5 | 658 | 1 | 0 | 0 | 10/18 | 82 |
| | 100 | 9 | 1,268 | 3 | 0 | 0 | 16/18 | 131 |
| | 160 | 14 | 1,986 | 5 | 0 | 0 | 18/18 | 604 |
| High-Hint | 10 | 3 | 126 | 4 | 0 | 0 | 1/18 | 44 |
| | 25 | 7 | 304 | 15 | 4 | 1 | 1/18 | 39 |
| | 50 | 13 | 600 | 34 | 13 | 4 | 1/18 | 29 |
| | 100 | 25 | 1,182 | 74 | 40 | 12 | 1/18 | 17 |
| | 160 | 40 | 1,896 | 122 | 85 | 29 | 1/18 | 16 |

Full tables, including Dust per source (levels, Perfect, Daily, gift) and the share of runs with a refusal, are in [economy_simulation.md](economy_simulation.md) and [economy_simulation.json](economy_simulation.json).

### Reading the results

- **Before grace**, with the same seeds, Casual players were refused a hint in 67 % of runs by level 160 (cosmetic buying drained their balance), and High-Hint players 114 times. **With grace**, Casual, Regular and Enthusiast players are never refused a hint. A High-Hint player always gets at least one hint per stuck level; only the second hint on the same level can be refused when Dust is short (29 by level 160).
- Cosmetics pace: a Regular player owns about half the shop by level 50 and all of it by level 160 (32 days). A Casual player gets there in about 80 days. Milestone items arrive on top of that.
- Hints cost roughly two level completions (15 vs 8). A player who hints on 1 level in 8 stays solvent with room for cosmetics, which matches the "occasional help, not pay-to-progress" intent.
- High-Hint players spend almost everything on hints and own one cosmetic. That is a choice the game allows rather than a dead end, and grace keeps them moving.

### Not changed, on purpose

Reward amounts and prices are as in Phase 10: balances players already earned are never touched. If playtests show hints are still too scarce for struggling players, the cheapest next lever is `HintPolicy.GraceAttempts` (2 → 1), not cheaper hints.
