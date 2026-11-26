# Phase 11 campaign audit

What the Phase 10 campaign (`PLOP_Pack_002`, 160 levels) actually looked like when Phase 11 began, what was wrong with it, and what `PLOP_Pack_003` changed. Every number here comes from the offline tools in this repository (`dotnet run --project DotNet/Plop.Bench -- --quality-audit …` and `--balanced`). **No human playtest has been run.** The metrics are heuristics, and the simulated players are models, not people.

## Machine-readable data

| File | Contents |
|---|---|
| [campaign_analysis_before.json](campaign_analysis_before.json) | Per level: id, position, World, board, Plops/Nests, walls, tiles, mechanics, Move limit, verification, states, optimum and solution, challenge, quality, decisions, coordination, settlement, dead ends, mechanic roles, planning depth, every simulated player, solution counts, similarity, flags |
| [campaign_analysis_after.json](campaign_analysis_after.json) | The same fields for `PLOP_Pack_003` |
| [audit_before.md](audit_before.md) / [audit_after.md](audit_after.md) | Per-World summary plus a one-row-per-level table (# · World · board · Plops · walls · tiles · mechanics · optimum · states · decisions · density · coordination · settlement · depth · misleading · random % · Greedy/LA2/LA3/Limited-information · challenge · quality · most similar · verified · flags) |
| [campaign_changes.md](campaign_changes.md) | All 160 slots: stage, provenance (Kept / Improved / Replaced / Authored), the old slot, the weakness that triggered a change, teaching intent, quality before → after, id |
| [daily_analysis_before.json](daily_analysis_before.json) / [daily_analysis_after.json](daily_analysis_after.json), [daily_audit_before.md](daily_audit_before.md) / [daily_audit_after.md](daily_audit_after.md) | The same audit for the Daily pools (`PLOP_Daily_001` → `PLOP_Daily_002`) |

Each level row records the mechanics it uses, how many Plops share the board, how many good first swipes it allows and how many solutions come within two moves of the best. The simulated players are described in [PUZZLE_BALANCE_REPORT.md](PUZZLE_BALANCE_REPORT.md).

## Quality baseline (Phase 10, before any change)

All 160 levels verified: the stored solution replays and BFS confirms the optimum.

| Measure | Phase 10 | Phase 11 |
|---|---|---|
| Levels verified | 160 / 160 | 160 / 160 (strict production verification + forced-chain inspection) |
| Mean quality (0–100) | 60.6 | 71.6 |
| Mean decision density | 0.34 | 0.41 |
| Greedy player finishes | 14 | 8 (levels 1, 2, 3, 6, 7 and the Discovery levels 21, 35, 49) |
| Look-ahead-2 finishes | 40 | 10 |
| Look-ahead-3 finishes | 59 | 29 |
| Limited-information finishes | 34 | 21 |
| Random player ≥ 25 % success | 14 | 7 |
| Planning depth 1 / 2 / 3 / 4 / > 4 | 14 / 27 / 21 / 18 / 80 | 8 / 2 / 20 / 27 / 103 |
| Flags: repetitive · few-decisions · shallow | 35 · 34 · 27 | 13 · 4 · 2 |
| Flags: random-solvable · greedy-solvable · decorative mechanic | 14 · 14 · 3 | 7 · 8 · 0 |
| Unique shortest solution | 45 | 55 |

Difficulty bands use the calibrated Phase 11 rule (`QualityAudit.Band`): Tutorial when planning depth ≤ 1 and challenge < 16; Easy when depth ≤ 2 or challenge < 30; then Normal < 42, Hard < 55, Expert.

| Band | Before | After |
|---|---|---|
| Tutorial | 12 | 8 |
| Easy | 35 | 10 |
| Normal | 32 | 38 |
| Hard | 51 | 61 |
| Expert | 30 | 43 |

## Issues found (P0–P3)

| Id | Pri | Issue | Evidence | Resolution |
|---|---|---|---|---|
| A-01 | P1 | The opening was trivial. Levels 1–4, 7, 8, 21, 22 and 49 fell to the Greedy player or to random swipes. | audit_before: cozy-nest quality 10, density 0.06 | New authored onboarding for L2, L4 and L6–L10. L1, L3 and L5 kept. |
| A-02 | P1 | Many levels needed planning depth 1–2 or had few real decisions: 6, 9–11, 13–16, 23–25 and others. | 27 shallow, 34 few-decisions | Weak levels were replaced through stage-specific searches with quality floors. |
| A-03 | P1 | Frost started at level 7, while World 2 assets started at 7 and the brief wants it at 6. | World asset ranges 1–6 / 7–20 | Frost is now discovered at L6; World ranges are 1–5 and 6–20. |
| A-04 | P2 | Three levels had decorative mechanics: the board solved the same way without the tiles. | removal test, `decorative-mechanic` | Old 41 was improved by removing the tiles; it is now L42. The other two were replaced. |
| A-05 | P2 | Repetitive solutions: one direction four or more times in a row on low-quality levels. | 35 repetitive | Searches now use the floors `MaxLongestRun 3` and `MaxRepetition 0.4`. 13 flagged levels remain: tutorials 2–3, and eleven kept or searched puzzles of quality 60–88 (see KNOWN_ISSUES). |
| A-06 | P2 | Limited Moves never forgave a slip early on, yet had slack +2 late on, which is backwards. | limits 30/60/90/120/150, slack 2 each | Eight challenges: slack +2 at 30, 45 and 60; +1 at 80, 100 and 120; exact at 140 and 155. |
| A-07 | P2 | The Daily pool had no mechanics, and 41 of 60 puzzles were flagged (long for their decisions, repetitive). | daily_audit_before | `PLOP_Daily_002`: 20 strong classic puzzles kept, plus 40 new across four mechanic tiers. Only 8 flagged, all mild. |
| A-08 | P2 | Hint economy: a hint-heavy or broke player could be stuck with no recourse. | economy simulation | `HintPolicy` grace: after 2 tries, the first hint on a level is free once if Dust is short. |
| A-09 | P2 | Loop pairs were told apart by colour only. | `ShapeAtlas.PaintPortal` | Each pair also carries 1–6 dice pips. |
| A-10 | P3 | Fan power pips were 0.028 cells wide and spaced 0.075 apart, which is hard to count on a phone. | `ShapeAtlas` | Pips are now 0.036 cells with a light halo, spaced 0.1 apart. |
| A-11 | P3 | No tooling showed balance problems: no quality metric, no simulated players, no similarity check across Worlds. | — | PuzzleQuality, SimulatedPlayers and QualityAudit, plus the Level Lab "Balance & quality" view. |
| A-12 | P3 | No fuzz coverage for damaged saves or long random sessions. | — | `Phase11StabilityTests`. |

No P0 problems (crash, unsolvable level, broken save) were found in the Phase 10 build.

## What happened to each level

160 slots: **95 kept** with the same puzzle and id, **1 improved** (decorative tiles removed), **57 replaced** and **7 authored** (L2, L4 and L6–L10). Levels 64–160 kept their slots, and weak ones were replaced in place. Build v2 also judges a kept puzzle by its *new* slot: old level 9 would have landed at level 11 with quality 29, so it was replaced. That re-search uses its own seed, so no other slot changed. In mechanic Worlds 2–5, kept puzzles were re-sorted by challenge, and the strongest late puzzle closes each World as its Mastery level. On ten levels the Limited Moves rule was set, moved or removed (see `campaign_changes.md`, column "Provenance").

Eight kept boards got a new id because their Move limit changed: an id covers the rules, not only the board. Old completions carry over only where the new rule is no stricter. That covers three boards (`PLOP_Pack_002_Order.json` → `aliases`).

## Early pacing (Pack_003)

| Level | Teaches | Board |
|---|---|---|
| 1 | Swipe into the matching Nest | kept |
| 2 | Walls stop you: the straight swipe bumps, so go around | authored 3×3 |
| 3 | One swipe moves every awake Plop | kept |
| 4 | Use a wall to hold one Plop while the other moves | authored 4×4 |
| 5 | First coordination: the settling order matters | kept |
| 6 | Frost discovery: a slide; find the swipe that stops under the Nest | authored, with instruction |
| 7 | Predict where a slide stops | authored |
| 8 | Frost with a second Plop: a sleeping Plop becomes a stopper | authored |
| 9 | Coordination without Frost: settle in the right order | authored |
| 10 | First Mastery: plan the slide and the settling order together (depth > 4) | authored |

Each mechanic World follows the A–E stages: Discovery, Application, Reasoning, Combination, Mastery. Frost runs L6–20, Breeze L21–34, Lava L35–48 and Loops L49–63. Breeze, Lava and Loops each open with an instruction. Later Worlds (64–160) mix every mechanic.

## Verification run for this audit

- `--balanced` (seed `0x504C4F5031310000`, build v2, search seeds v1) runs twice and gives byte-identical `PLOP_Pack_003.json` and `PLOP_Daily_002.json` (md5 `9be754d4…` and `32efc5b7…`).
- `LevelVerifier.VerifyProductionPack(160)` and `VerifyProductionPack(60)` both pass.
- Forced-chain inspection at 1M states: no level has an unresolved command, and the longest chain is 6.
- `PLOP_Deadlocks_003.bytes`: 160/160 levels complete at 600k states, 33,156 dead-end keys.
