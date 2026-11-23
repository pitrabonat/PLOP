# PLOP! Phase 7 campaign review

The Phase 6 Android build packages 100 structurally valid puzzles with verified shortest solutions. The median route length rises from 4 to 14 moves across the five difficulty bands, and all 18 planned recovery levels dip in the difficulty score. Human testing should start with the first deadlock lesson, the two steepest band transitions, and the contrast between levels 99 and 100.

## Catalog verified

The exact 197,713-byte `PLOP_Pack_001-final.json` scratch snapshot was found intact in the Phase 6 APK at `assets/bin/Data/f0968a9187bbd43b9be322f600188303`. Its SHA-256 is `9b4c38f6b8244f1be6759d71ea6301aa718b51252ac2e639e8d0c2d20c5ed17d`. It also matches the pack beside the preserved Phase 5 gameplay, solver and generation sources.

The project's strict verifier passed structure, canonical stable IDs, duplicate and one-wall-variant checks, shortest solution replay, Undo and Restart across all 100 boards. The authoritative state-space analyzer found 1,053 total moves across the shortest routes and reproduced all 100 difficulty scores, categories and metric dictionaries. This was a desktop source replay of the exact shipped catalog. Device interaction, board readability and perceived difficulty are pending in this review.

| Band | Shortest moves, median (range) | Score, median (range) | Wall use | Forced coordination | Must move away | Settlement constraint or trap | Has a dead end |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1–10 Introduction | 4 (1–6) | 9.97 (4.40–20.06) | 3/10 | 2/10 | 2/10 | 1/10 | 1/10 |
| 11–25 Basic logic | 6 (4–9) | 18.62 (13.45–24.30) | 9/15 | 12/15 | 7/15 | 6/15 | 6/15 |
| 26–50 Coordination | 10 (6–15) | 29.23 (19.94–38.72) | 24/25 | 24/25 | 16/25 | 10/25 | 10/25 |
| 51–75 Advanced planning | 12 (9–16) | 37.21 (29.42–48.75) | 25/25 | 25/25 | 23/25 | 17/25 | 17/25 |
| 76–100 Mastery | 14 (12–20) | 44.14 (38.89–56.04) | 25/25 | 25/25 | 25/25 | 17/25 | 17/25 |

The score combines route length and board metrics. Dead-end percentages use the full reachable state graph, where player behavior can concentrate on a small subset of states. A single shortest route can coexist with many longer solutions. A strict pairwise settlement order occurs on two boards, 67 and 74. The settlement column also counts boards with a premature settlement that can make the puzzle unsolvable.

## Priority spot checks

| Levels | Signal | What to observe |
|---|---|---|
| 7–10 | The first ten levels explain the rules. Level 8 has the highest dead-end share in the catalog, 4 of 14 states. Level 9 drops to 3 moves before level 10 rises to 6. | Can the player explain why a sleeping Plop stalls a route and discover Undo? Does level 9 communicate that a wrong-color Nest is traversable? Does the Undo prompt on 10 lead to a successful recovery? |
| 15–16 | Level 16 has 5 dead-end states out of 37 (13.51%), the highest share outside 8 and 99. | Can a new player explain the stall after a premature settlement and find recovery without coaching? |
| 19–21 and 39–41 | Level 20 drops 10.85 score points and five moves from 19. Level 40 drops 13.21 points from 39, but all 11 moves on its shortest route are forced. Both precede a new world. | Does each transition feel like a break? After clearing 20 or 40, does the next world unlock clearly? Can the player replay the boundary board and return to their current level? |
| 50–51 and 75–76 | The biggest upward jumps are +12.18 and +12.72 score points. Both add a third Plop. The minimum route increases by four and three moves respectively. | Before any hint, can the player form a plan for the third Plop? Listen for confusion about the rule versus uncertainty about the best route. |
| 53–55 and 67/74 | Level 53 has one shortest route and five trap alternatives along it. Level 55's lower score comes with one 11-move shortest route. Levels 67 and 74 have mandatory relative settlement order. | Compare perceived difficulty before sharing levels' scores. Ask which Plop the tester wanted to settle first and what changed their mind. |
| 79–81 and 89–90 | Level 80 is the only recovery with a lower score and a longer shortest route, up from 12 to 14. Levels 79, 80 and 90 each have one shortest route. Level 81 introduces Moonlit Clouds. | Does 80 feel like recovery? Can the tester read the Plops and Nests on the new backdrop, and does 90 still feel manageable after 89? |
| 98–100 | Level 98 has the longest shortest route (20). Level 99 has the highest score (56.04), with 234 of 1,263 states dead ends. Level 100 falls to 14 moves, score 41.56, with zero dead ends across 77,196 states. | Does 99 feel like a fair peak and 100 like a satisfying ending? Check that clearing 100 leaves campaign progress complete and earlier worlds replayable. |

## Human playtest checklist

1. Use a dedicated fresh test profile on a compact phone and a tall phone. Record platform, device, build and starting save state. Ask a new player to play levels 1–10 in order without a demonstration. Keep this review, the benchmark and the solution routes out of the player's view.
2. On levels 1–6, observe whether the player discovers swipe direction, one-tile movement, shared input and the matching Nest. After a board, ask them to describe the rule in their own words. Record where wording, color, or touch feedback made a rule ambiguous.
3. On 7–10, let the player attempt each board before offering help. Record the first premature settlement, whether its cause was clear, and whether they found Undo, Restart or Hint. Keep unsuccessful swipes in the observation log even though the game's move counter ignores them.
4. Run a separate guided Level 8 recovery pass. From the original state, swipe `Up`, then `Left`. This position is proven unsolvable and still allows one state-changing swipe. Check that recovery is offered, use Undo to return to a solvable state, and verify Hint addresses the recovered position. Run Restart from a deadlock and verify the initial board and move counter return. Use a separate test pass so this fixture does not prime the blind run.
5. Ask a returning or experienced player to play the priority pairs in sequence. Let them try unassisted first, then use built-in help as they normally would. Record which board in each pair felt harder and why. Compare the expected recovery boards with their preceding peaks after the session.
6. Clear the world boundaries at 20, 40, 60 and 80, then background or relaunch before resuming play. Verify completed/unlocked counts, replay of a completed board, return to the right level, and reading of locked-world status. Run the ending pass through 98–100 in order.

Record one row per attempt. Use an anonymous tester code. Capture the move prefix or current board when documenting a deadlock, unclear hint or suspected input issue.

| Tester / device | Level / stable ID | Attempt result | Active solve time | Moves / no-op swipes | Undo / Restart / Hint | First stall or misunderstanding | Felt difficulty (1–5) |
|---|---|---|---|---|---|---|---|
| | | Solo / assisted / stopped | | | | | |

## Reproduce strict production verification

Run from the project root with .NET 10.

```sh
dotnet run -c Release --project DotNet/Plop.Bench -- --verify-production Assets/Levels/Resources/Levels/PLOP_Pack_001.json
```

The performance and device logs for this phase live in
`/Volumes/PLOP_DEV/Builds/Logs/phase7/`.
