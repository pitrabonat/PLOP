# Quality audit — PLOP_Pack_002 (Phase 10, before balancing)

Levels: 160 · verified: 160 · analysed: 160 · 0.7 s on 10 threads · PuzzleQuality v1, random player 400 seeded runs, look-ahead up to 4.

| World | Levels | Avg opt | Avg states | Avg density | Avg quality | Avg challenge | Greedy solves | LA2 solves | LA3 solves | LimInfo solves | Random ≥25% | Flagged |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| cozy-nest | 1–6 | 3.8 | 74 | 0.06 | 10.0 | 8.3 | 4 | 6 | 6 | 6 | 5 | 6 |
| frosted-pond | 7–20 | 7.9 | 185 | 0.20 | 39.0 | 24.9 | 4 | 7 | 9 | 7 | 3 | 9 |
| breezy-meadow | 21–34 | 8.1 | 209 | 0.17 | 37.3 | 28.0 | 3 | 7 | 10 | 4 | 5 | 12 |
| ember-hollow | 35–48 | 9.2 | 218 | 0.44 | 56.8 | 33.8 | 2 | 5 | 9 | 4 | 0 | 10 |
| prism-observatory | 49–63 | 8.7 | 271 | 0.33 | 53.9 | 34.9 | 1 | 6 | 8 | 3 | 1 | 11 |
| dewdrop-garden | 64–85 | 10.6 | 1710 | 0.33 | 63.8 | 40.6 | 0 | 1 | 6 | 2 | 0 | 7 |
| rainy-window | 86–108 | 11.6 | 6180 | 0.38 | 71.1 | 47.5 | 0 | 4 | 4 | 5 | 0 | 10 |
| twilight-attic | 109–133 | 13.3 | 10546 | 0.38 | 73.5 | 53.7 | 0 | 1 | 4 | 3 | 0 | 6 |
| moonlit-clouds | 134–160 | 14.6 | 8682 | 0.43 | 77.2 | 61.1 | 0 | 3 | 3 | 0 | 0 | 10 |

Flags: repetitive 35 · few-decisions 34 · shallow 27 · open 24 · long-for-its-decisions 20 · random-solvable 14 · greedy-solvable 14 · easy-for-position 9 · decorative-mechanic 3

Categories: Easy 2 · Expert 88 · Hard 42 · Normal 17 · Tutorial 11

Planning depth (smallest look-ahead that solves): 1: 14 · 2: 27 · 3: 21 · 4: 18 · >4: 80

Shortest solutions: min 1, median 11, mean 10.8, max 17

## Per level

| # | World | Board | Plops | Walls | Tiles | Mechanics | Opt | States | Dec | Dens | Coord | Settle | Depth | Mislead | Rnd% | Greedy | LA2 | LA3 | LimInfo | Chal | Qual | Sim | Ok | Flags |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cozy-nest | 4×4 | 1 | 0 | 0 | classic | 1 | 16 | 0 | 0.00 | 0.0 | – | 1 | 0 | 91 | 1 | 1 | 1 | 1 | 0.0 | 0 | 3 (20%) | ✓ | random-solvable, greedy-solvable, few-decisions, open |
| 2 | cozy-nest | 4×4 | 1 | 0 | 0 | classic | 4 | 16 | 0 | 0.00 | 0.0 | – | 1 | 0 | 60 | 4 | 4 | 4 | 4 | 1.0 | 0 | 22 (25%) | ✓ | random-solvable, greedy-solvable, few-decisions, repetitive, open |
| 3 | cozy-nest | 4×4 | 2 | 0 | 0 | classic | 2 | 12 | 0 | 0.00 | 0.0 | – | 1 | 0 | 91 | 2 | 2 | 2 | 2 | 0.0 | 0 | 1 (20%) | ✓ | random-solvable, greedy-solvable, few-decisions, repetitive, open |
| 4 | cozy-nest | 4×4 | 2 | 1 | 0 | classic | 4 | 19 | 0 | 0.00 | 0.3 | – | 1 | 0 | 62 | 4 | 4 | 4 | 5 | 1.3 | 0 | 21 (17%) | ✓ | random-solvable, greedy-solvable, few-decisions, open |
| 5 | cozy-nest | 3×3 | 2 | 2 | 0 | classic | 4 | 14 | 1 | 0.25 | 0.5 | yes | 2 | 1 | 22 | ✗ | 4 | 4 | 4 | 23.0 | 45 | 0 (0%) | ✓ | shallow |
| 6 | cozy-nest | 5×5 | 2 | 5 | 0 | classic | 8 | 365 | 1 | 0.13 | 0.5 | – | 2 | 0 | 35 | ✗ | 10 | 8 | 15 | 24.5 | 16 | 11 (15%) | ✓ | random-solvable, shallow, few-decisions |
| 7 | frosted-pond | 5×3 | 1 | 0 | 1 | Frost | 1 | 14 | 0 | 0.00 | 0.0 | – | 1 | 0 | 90 | 1 | 1 | 1 | 1 | 0.3 | 0 | 8 (50%) | ✓ | random-solvable, greedy-solvable, few-decisions, open |
| 8 | frosted-pond | 5×3 | 1 | 1 | 1 | Frost | 2 | 12 | 0 | 0.00 | 0.0 | – | 1 | 0 | 79 | 2 | 2 | 2 | 4 | 1.8 | 0 | 36 (75%) | ✓ | random-solvable, greedy-solvable, few-decisions, open, easy-for-position |
| 9 | frosted-pond | 5×5 | 1 | 6 | 1 | Frost | 8 | 19 | 2 | 0.25 | 0.0 | – | 3 | 0 | 24 | ✗ | ✗ | 8 | 8 | 22.6 | 29 | 10 (40%) | ✓ | repetitive |
| 10 | frosted-pond | 5×5 | 1 | 6 | 4 | Frost | 8 | 18 | 1 | 0.13 | 0.0 | – | 2 | 0 | 25 | ✗ | 8 | 8 | ✗ | 21.8 | 19 | 9 (40%) | ✓ | shallow, few-decisions |
| 11 | frosted-pond | 5×5 | 2 | 5 | 1 | Frost | 8 | 335 | 2 | 0.25 | 0.5 | – | 1 | 1 | 18 | 12 | 12 | 11 | 17 | 13.0 | 23 | 9 (20%) | ✓ | greedy-solvable, easy-for-position |
| 12 | frosted-pond | 5×5 | 2 | 9 | 2 | Frost | 10 | 108 | 3 | 0.30 | 1.0 | – | 4 | 0 | 1 | ✗ | ✗ | ✗ | 16 | 29.8 | 62 | 9 (17%) | ✓ |  |
| 13 | frosted-pond | 5×6 | 2 | 8 | 1 | Frost | 8 | 365 | 3 | 0.38 | 0.5 | – | 2 | 1 | 22 | ✗ | 9 | 9 | ✗ | 27.7 | 44 | 43 (26%) | ✓ | shallow, open |
| 14 | frosted-pond | 5×6 | 2 | 13 | 1 | Frost | 8 | 129 | 0 | 0.00 | 0.5 | – | 3 | 0 | 21 | ✗ | ✗ | 9 | ✗ | 25.5 | 25 | 32 (39%) | ✓ | few-decisions |
| 15 | frosted-pond | 5×6 | 2 | 12 | 1 | Frost | 9 | 193 | 1 | 0.11 | 0.5 | yes | 2 | 1 | 4 | ✗ | 9 | 9 | 21 | 32.7 | 43 | 51 (38%) | ✓ | shallow, few-decisions, repetitive |
| 16 | frosted-pond | 5×6 | 2 | 9 | 2 | Frost | 10 | 358 | 0 | 0.00 | 0.5 | – | 1 | 0 | 29 | 17 | 10 | 14 | 15 | 17.6 | 11 | 69 (38%) | ✓ | random-solvable, greedy-solvable, few-decisions, long-for-its-decisions |
| 17 | frosted-pond | 5×6 | 2 | 9 | 2 | Frost | 9 | 204 | 4 | 0.44 | 0.5 | yes | >4 | 0 | 3 | ✗ | ✗ | ✗ | ✗ | 37.6 | 73 | 37 (35%) | ✓ |  |
| 18 | frosted-pond | 5×6 | 2 | 8 | 1 | Frost | 7 | 219 | 3 | 0.43 | 0.5 | yes | >4 | 2 | 6 | ✗ | ✗ | ✗ | ✗ | 34.6 | 79 | 15 (27%) | ✓ |  |
| 19 | frosted-pond | 5×6 | 2 | 7 | 1 | Frost | 11 | 415 | 3 | 0.27 | 0.5 | yes | >4 | 3 | 2 | ✗ | ✗ | ✗ | ✗ | 43.2 | 72 | 20 (32%) | ✓ |  |
| 20 | frosted-pond | 5×6 | 2 | 12 | 2 | Frost | 11 | 196 | 3 | 0.27 | 0.5 | yes | 4 | 1 | 8 | ✗ | ✗ | ✗ | ✗ | 40.7 | 66 | 28 (36%) | ✓ |  |
| 21 | breezy-meadow | 4×4 | 1 | 0 | 1 | Breeze | 1 | 15 | 0 | 0.00 | 0.0 | – | 1 | 0 | 97 | 1 | 1 | 1 | 4 | 0.0 | 0 | 22 (50%) | ✓ | random-solvable, greedy-solvable, few-decisions, open |
| 22 | breezy-meadow | 4×4 | 1 | 0 | 1 | Breeze | 4 | 15 | 0 | 0.00 | 0.0 | – | 1 | 0 | 69 | 4 | 4 | 4 | 6 | 3.1 | 0 | 21 (50%) | ✓ | random-solvable, greedy-solvable, few-decisions, open, easy-for-position |
| 23 | breezy-meadow | 5×6 | 1 | 9 | 2 | Breeze | 7 | 17 | 1 | 0.14 | 0.0 | – | 2 | 0 | 31 | ✗ | 8 | 7 | ✗ | 22.1 | 15 | 81 (30%) | ✓ | random-solvable, shallow, few-decisions, open |
| 24 | breezy-meadow | 5×6 | 1 | 7 | 2 | Breeze | 9 | 21 | 1 | 0.11 | 0.0 | – | 2 | 0 | 28 | ✗ | 12 | 9 | ✗ | 24.5 | 12 | 32 (33%) | ✓ | random-solvable, shallow, few-decisions, repetitive |
| 25 | breezy-meadow | 5×6 | 2 | 8 | 2 | Breeze | 9 | 143 | 0 | 0.00 | 0.5 | – | 1 | 0 | 23 | 12 | ✗ | ✗ | 12 | 18.8 | 16 | 55 (32%) | ✓ | greedy-solvable, few-decisions, easy-for-position |
| 26 | breezy-meadow | 5×6 | 2 | 10 | 2 | Breeze | 11 | 215 | 1 | 0.09 | 1.0 | yes | >4 | 0 | 10 | ✗ | ✗ | ✗ | ✗ | 34.7 | 46 | 28 (48%) | ✓ | few-decisions, long-for-its-decisions |
| 27 | breezy-meadow | 5×6 | 2 | 9 | 2 | Frost+Breeze | 10 | 324 | 1 | 0.10 | 0.5 | – | 2 | 1 | 24 | ✗ | 15 | 11 | 18 | 28.5 | 22 | 15 (33%) | ✓ | shallow, few-decisions, repetitive, long-for-its-decisions |
| 28 | breezy-meadow | 5×6 | 2 | 12 | 2 | Frost+Breeze | 6 | 186 | 3 | 0.50 | 0.5 | yes | 2 | 2 | 29 | ✗ | 11 | 6 | ✗ | 28.1 | 65 | 26 (48%) | ✓ | random-solvable, shallow |
| 29 | breezy-meadow | 5×6 | 2 | 7 | 2 | Frost+Breeze | 9 | 400 | 2 | 0.22 | 1.0 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 43.5 | 73 | 50 (25%) | ✓ |  |
| 30 | breezy-meadow | 5×6 | 2 | 5 | 1 | Breeze+Limit10 | 8 | 438 | 1 | 0.13 | 0.5 | yes | 3 | 1 | 7 | ✗ | ✗ | 8 | ✗ | 32.5 | 47 | 54 (22%) | ✓ | few-decisions, open |
| 31 | breezy-meadow | 5×6 | 2 | 7 | 2 | Frost+Breeze | 11 | 181 | 1 | 0.09 | 0.5 | yes | 4 | 1 | 3 | ✗ | ✗ | ✗ | ✗ | 40.0 | 55 | 28 (30%) | ✓ | few-decisions, long-for-its-decisions |
| 32 | breezy-meadow | 5×6 | 2 | 11 | 2 | Breeze | 11 | 158 | 4 | 0.36 | 0.5 | yes | 3 | 1 | 11 | ✗ | ✗ | 11 | ✗ | 36.8 | 63 | 14 (39%) | ✓ |  |
| 33 | breezy-meadow | 5×6 | 2 | 7 | 5 | Frost+Breeze | 10 | 370 | 3 | 0.30 | 0.5 | yes | 2 | 3 | 6 | ✗ | 10 | 10 | ✗ | 40.1 | 52 | 77 (30%) | ✓ | shallow |
| 34 | breezy-meadow | 5×6 | 2 | 5 | 1 | Breeze | 7 | 439 | 2 | 0.29 | 0.5 | yes | 3 | 1 | 2 | ✗ | ✗ | 7 | ✗ | 39.0 | 56 | 84 (26%) | ✓ | repetitive, open |
| 35 | ember-hollow | 3×2 | 1 | 0 | 1 | Lava | 4 | 5 | 2 | 0.50 | 0.0 | – | 1 | 0 | 7 | 4 | 4 | 4 | 4 | 7.2 | 45 | 0 (0%) | ✓ | greedy-solvable |
| 36 | ember-hollow | 5×3 | 1 | 0 | 2 | Frost+Lava | 4 | 12 | 1 | 0.25 | 0.0 | – | 1 | 0 | 17 | 4 | 4 | 4 | 4 | 2.9 | 5 | 8 (75%) | ✓ | greedy-solvable, repetitive, open, decorative-mechanic, easy-for-position |
| 37 | ember-hollow | 5×6 | 1 | 9 | 3 | Frost+Lava | 12 | 17 | 4 | 0.33 | 0.0 | – | 3 | 1 | 0 | ✗ | ✗ | 12 | lava | 35.1 | 54 | 17 (35%) | ✓ |  |
| 38 | ember-hollow | 6×6 | 1 | 11 | 4 | Frost+Lava | 8 | 20 | 2 | 0.25 | 0.0 | – | 2 | 1 | 2 | ✗ | 8 | 8 | lava | 31.5 | 38 | 90 (35%) | ✓ | shallow, open |
| 39 | ember-hollow | 5×6 | 2 | 5 | 1 | Lava | 9 | 142 | 6 | 0.67 | 0.0 | – | 4 | 1 | 3 | ✗ | ✗ | ✗ | ✗ | 27.2 | 60 | 92 (33%) | ✓ | repetitive, open |
| 40 | ember-hollow | 6×6 | 2 | 6 | 3 | Lava | 12 | 172 | 5 | 0.42 | 0.5 | – | 2 | 0 | 0 | ✗ | 12 | 12 | ✗ | 38.6 | 48 | 129 (29%) | ✓ | shallow, repetitive, open |
| 41 | ember-hollow | 5×6 | 2 | 10 | 3 | Frost+Lava+Breeze | 11 | 274 | 4 | 0.36 | 0.5 | yes | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 41.8 | 60 | 50 (41%) | ✓ | decorative-mechanic |
| 42 | ember-hollow | 6×6 | 2 | 10 | 3 | Frost+Lava | 10 | 402 | 6 | 0.60 | 0.5 | yes | 3 | 1 | 1 | ✗ | ✗ | 10 | ✗ | 44.1 | 75 | 74 (32%) | ✓ | open |
| 43 | ember-hollow | 5×6 | 2 | 8 | 3 | Lava+Breeze | 7 | 291 | 5 | 0.71 | 0.5 | yes | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 45.0 | 80 | 31 (30%) | ✓ | open |
| 44 | ember-hollow | 6×6 | 2 | 12 | 3 | Lava+Breeze | 10 | 288 | 5 | 0.50 | 0.5 | – | 2 | 0 | 0 | ✗ | 13 | 14 | ✗ | 39.0 | 56 | 147 (31%) | ✓ | shallow |
| 45 | ember-hollow | 5×6 | 2 | 12 | 2 | Frost+Lava | 11 | 165 | 2 | 0.18 | 0.5 | yes | >4 | 1 | 1 | ✗ | ✗ | ✗ | ✗ | 36.6 | 62 | 73 (38%) | ✓ | few-decisions, long-for-its-decisions |
| 46 | ember-hollow | 6×6 | 2 | 7 | 2 | Frost+Lava | 11 | 638 | 6 | 0.55 | 0.5 | yes | 3 | 2 | 2 | ✗ | ✗ | 12 | 17 | 42.7 | 79 | 78 (27%) | ✓ |  |
| 47 | ember-hollow | 5×6 | 2 | 12 | 2 | Frost+Lava | 10 | 123 | 3 | 0.30 | 0.5 | yes | >4 | 0 | 4 | ✗ | ✗ | ✗ | ✗ | 37.6 | 60 | 83 (35%) | ✓ |  |
| 48 | ember-hollow | 6×6 | 2 | 8 | 4 | Frost+Lava+Breeze | 10 | 499 | 6 | 0.60 | 0.5 | yes | 3 | 1 | 3 | ✗ | ✗ | 12 | 18 | 43.7 | 74 | 66 (30%) | ✓ |  |
| 49 | prism-observatory | 5×2 | 1 | 0 | 2 | Loops | 2 | 10 | 0 | 0.00 | 0.0 | – | 1 | 0 | 96 | 2 | 2 | 2 | 2 | 1.4 | 0 | 0 (0%) | ✓ | random-solvable, greedy-solvable, few-decisions, open |
| 50 | prism-observatory | 5×6 | 1 | 10 | 2 | Loops | 7 | 20 | 1 | 0.14 | 0.0 | – | 3 | 1 | 18 | ✗ | ✗ | 7 | ✗ | 23.7 | 32 | 52 (44%) | ✓ | few-decisions, open, easy-for-position |
| 51 | prism-observatory | 5×6 | 1 | 10 | 2 | Loops | 7 | 20 | 1 | 0.14 | 0.0 | – | 2 | 1 | 20 | ✗ | 7 | 7 | 9 | 22.9 | 11 | 15 (38%) | ✓ | shallow, few-decisions, repetitive, open, easy-for-position |
| 52 | prism-observatory | 5×6 | 2 | 10 | 2 | Loops | 10 | 349 | 4 | 0.40 | 0.5 | – | 2 | 2 | 9 | ✗ | 11 | 11 | ✗ | 32.6 | 57 | 50 (44%) | ✓ | shallow, repetitive |
| 53 | prism-observatory | 5×6 | 2 | 8 | 2 | Loops | 12 | 444 | 0 | 0.00 | 0.5 | – | 2 | 0 | 21 | ✗ | 14 | 14 | ✗ | 29.6 | 19 | 50 (37%) | ✓ | shallow, few-decisions, long-for-its-decisions |
| 54 | prism-observatory | 5×6 | 2 | 8 | 2 | Loops | 9 | 366 | 3 | 0.33 | 0.5 | – | 2 | 4 | 3 | ✗ | 19 | 13 | ✗ | 40.9 | 53 | 92 (28%) | ✓ | shallow, repetitive |
| 55 | prism-observatory | 5×6 | 2 | 8 | 2 | Loops | 7 | 259 | 3 | 0.43 | 0.5 | yes | 4 | 2 | 9 | ✗ | ✗ | ✗ | ✗ | 43.7 | 74 | 17 (32%) | ✓ | open |
| 56 | prism-observatory | 5×6 | 2 | 8 | 3 | Lava+Loops | 10 | 272 | 4 | 0.40 | 0.5 | yes | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 43.0 | 74 | 20 (32%) | ✓ |  |
| 57 | prism-observatory | 5×6 | 2 | 5 | 5 | Frost+Lava+Loops | 6 | 449 | 4 | 0.67 | 0.5 | yes | >4 | 2 | 9 | ✗ | ✗ | ✗ | ✗ | 41.2 | 74 | 16 (25%) | ✓ | open |
| 58 | prism-observatory | 5×6 | 2 | 8 | 3 | Lava+Loops | 9 | 375 | 3 | 0.33 | 0.5 | – | 2 | 2 | 2 | ✗ | 12 | 9 | 13 | 31.9 | 53 | 41 (35%) | ✓ | shallow |
| 59 | prism-observatory | 5×6 | 2 | 9 | 5 | Lava+Loops | 10 | 157 | 5 | 0.50 | 0.5 | yes | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 41.7 | 83 | 53 (29%) | ✓ |  |
| 60 | prism-observatory | 5×6 | 2 | 9 | 5 | Lava+Loops+Limit11 | 9 | 211 | 3 | 0.33 | 0.5 | yes | 3 | 1 | 0 | ✗ | ✗ | 13 | ✗ | 38.5 | 60 | 26 (29%) | ✓ | repetitive |
| 61 | prism-observatory | 5×6 | 2 | 8 | 3 | Lava+Loops | 10 | 307 | 3 | 0.30 | 0.5 | yes | >4 | 2 | 2 | ✗ | ✗ | ✗ | ✗ | 41.1 | 71 | 60 (29%) | ✓ |  |
| 62 | prism-observatory | 5×6 | 2 | 6 | 3 | Lava+Loops | 12 | 434 | 6 | 0.50 | 0.5 | yes | >4 | 3 | 2 | ✗ | ✗ | ✗ | ✗ | 46.9 | 80 | 51 (26%) | ✓ | repetitive, open |
| 63 | prism-observatory | 5×6 | 2 | 9 | 2 | Loops | 10 | 398 | 4 | 0.40 | 0.5 | yes | >4 | 3 | 4 | ✗ | ✗ | ✗ | ✗ | 44.4 | 68 | 15 (33%) | ✓ |  |
| 64 | dewdrop-garden | 6×6 | 2 | 5 | 3 | Frost+Breeze | 12 | 746 | 3 | 0.25 | 0.5 | yes | 4 | 1 | 2 | ✗ | ✗ | ✗ | ✗ | 38.4 | 63 | 76 (23%) | ✓ |  |
| 65 | dewdrop-garden | 5×6 | 2 | 7 | 2 | Lava+Breeze | 12 | 338 | 6 | 0.50 | 0.5 | yes | >4 | 0 | 0 | ✗ | ✗ | ✗ | ✗ | 44.0 | 78 | 45 (27%) | ✓ |  |
| 66 | dewdrop-garden | 6×6 | 2 | 12 | 3 | Frost+Loops | 12 | 265 | 3 | 0.25 | 0.5 | yes | >4 | 3 | 2 | ✗ | ✗ | ✗ | ✗ | 43.9 | 67 | 48 (30%) | ✓ |  |
| 67 | dewdrop-garden | 5×6 | 3 | 5 | 4 | Frost+Lava | 11 | 3756 | 5 | 0.46 | 0.8 | yes | 3 | 1 | 1 | ✗ | ✗ | 11 | ✗ | 45.3 | 76 | 77 (20%) | ✓ |  |
| 68 | dewdrop-garden | 6×6 | 2 | 6 | 3 | Breeze+Loops | 8 | 759 | 1 | 0.13 | 0.5 | – | 4 | 1 | 11 | ✗ | ✗ | ✗ | ✗ | 26.5 | 42 | 100 (27%) | ✓ | few-decisions, repetitive, easy-for-position |
| 69 | dewdrop-garden | 5×6 | 2 | 10 | 3 | Lava+Loops | 11 | 241 | 4 | 0.36 | 0.5 | – | >4 | 2 | 1 | ✗ | ✗ | ✗ | ✗ | 39.5 | 69 | 16 (38%) | ✓ |  |
| 70 | dewdrop-garden | 6×6 | 2 | 8 | 0 | classic | 11 | 741 | 2 | 0.18 | 0.5 | – | 2 | 2 | 5 | ✗ | 11 | 19 | 21 | 32.4 | 39 | 154 (29%) | ✓ | shallow, few-decisions, repetitive, long-for-its-decisions |
| 71 | dewdrop-garden | 5×6 | 2 | 10 | 2 | Lava+Breeze | 9 | 160 | 4 | 0.44 | 0.5 | yes | >4 | 2 | 4 | ✗ | ✗ | ✗ | ✗ | 36.6 | 79 | 14 (30%) | ✓ |  |
| 72 | dewdrop-garden | 6×6 | 3 | 9 | 3 | Frost+Loops | 11 | 10655 | 5 | 0.46 | 0.5 | yes | >4 | 2 | 3 | ✗ | ✗ | ✗ | ✗ | 53.6 | 78 | 89 (27%) | ✓ |  |
| 73 | dewdrop-garden | 5×6 | 2 | 7 | 3 | Frost+Lava | 8 | 349 | 3 | 0.38 | 0.5 | – | >4 | 1 | 3 | ✗ | ✗ | ✗ | ✗ | 33.9 | 64 | 45 (38%) | ✓ |  |
| 74 | dewdrop-garden | 6×6 | 2 | 9 | 3 | Breeze+Loops | 13 | 618 | 2 | 0.15 | 0.5 | – | 3 | 0 | 1 | ✗ | ✗ | 13 | ✗ | 38.7 | 40 | 42 (32%) | ✓ | few-decisions, long-for-its-decisions |
| 75 | dewdrop-garden | 5×6 | 2 | 9 | 4 | Lava+Loops | 12 | 214 | 5 | 0.42 | 0.5 | yes | >4 | 1 | 2 | ✗ | ✗ | ✗ | ✗ | 40.5 | 77 | 19 (32%) | ✓ |  |
| 76 | dewdrop-garden | 6×6 | 2 | 11 | 3 | Frost+Breeze | 7 | 440 | 2 | 0.29 | 0.5 | yes | 3 | 1 | 7 | ✗ | ✗ | 7 | ✗ | 40.7 | 63 | 98 (32%) | ✓ |  |
| 77 | dewdrop-garden | 5×6 | 3 | 8 | 0 | classic | 12 | 1289 | 4 | 0.33 | 0.5 | yes | >4 | 1 | 3 | ✗ | ✗ | ✗ | ✗ | 34.8 | 65 | 47 (30%) | ✓ |  |
| 78 | dewdrop-garden | 6×6 | 2 | 10 | 3 | Frost+Loops | 11 | 632 | 1 | 0.09 | 0.5 | – | 3 | 0 | 10 | ✗ | ✗ | 12 | 15 | 33.8 | 26 | 44 (30%) | ✓ | few-decisions, repetitive, long-for-its-decisions |
| 79 | dewdrop-garden | 5×6 | 2 | 6 | 3 | Frost+Lava | 10 | 345 | 6 | 0.60 | 0.5 | yes | 3 | 3 | 0 | ✗ | ✗ | 10 | ✗ | 45.7 | 79 | 47 (29%) | ✓ |  |
| 80 | dewdrop-garden | 6×6 | 2 | 13 | 4 | Breeze+Loops | 9 | 398 | 2 | 0.22 | 0.5 | yes | >4 | 3 | 24 | ✗ | ✗ | ✗ | ✗ | 48.5 | 57 | 101 (36%) | ✓ |  |
| 81 | dewdrop-garden | 5×6 | 2 | 9 | 3 | Lava+Loops | 10 | 338 | 3 | 0.30 | 0.5 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 42.0 | 70 | 23 (30%) | ✓ |  |
| 82 | dewdrop-garden | 6×6 | 3 | 10 | 2 | Frost+Breeze | 11 | 10540 | 3 | 0.27 | 0.6 | yes | >4 | 3 | 7 | ✗ | ✗ | ✗ | ✗ | 50.0 | 63 | 147 (30%) | ✓ |  |
| 83 | dewdrop-garden | 5×6 | 2 | 10 | 2 | Lava+Breeze | 12 | 264 | 2 | 0.17 | 0.5 | – | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 34.5 | 53 | 47 (35%) | ✓ | few-decisions, long-for-its-decisions |
| 84 | dewdrop-garden | 5×6 | 3 | 11 | 0 | classic | 11 | 4435 | 4 | 0.36 | 0.5 | yes | 4 | 2 | 3 | ✗ | ✗ | ✗ | ✗ | 39.7 | 72 | 71 (29%) | ✓ | repetitive |
| 85 | dewdrop-garden | 5×6 | 2 | 9 | 3 | Frost+Lava | 10 | 98 | 6 | 0.60 | 0.5 | yes | 4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 49.9 | 83 | 18 (27%) | ✓ | repetitive |
| 86 | rainy-window | 6×6 | 3 | 9 | 4 | Frost+Loops | 9 | 12112 | 4 | 0.44 | 0.5 | yes | 4 | 2 | 11 | ✗ | ✗ | ✗ | 20 | 42.2 | 74 | 95 (31%) | ✓ |  |
| 87 | rainy-window | 6×6 | 3 | 11 | 2 | Frost+Lava | 8 | 8677 | 3 | 0.38 | 0.6 | yes | 3 | 2 | 3 | ✗ | ✗ | 12 | ✗ | 38.6 | 73 | 38 (33%) | ✓ |  |
| 88 | rainy-window | 6×6 | 3 | 10 | 3 | Breeze+Loops | 13 | 8732 | 5 | 0.39 | 0.6 | yes | >4 | 2 | 2 | ✗ | ✗ | ✗ | ✗ | 51.0 | 74 | 109 (28%) | ✓ |  |
| 89 | rainy-window | 6×6 | 3 | 11 | 3 | Lava+Loops | 11 | 6403 | 6 | 0.55 | 0.5 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 50.7 | 85 | 76 (31%) | ✓ |  |
| 90 | rainy-window | 6×6 | 2 | 12 | 2 | Frost+Breeze+Limit16 | 14 | 411 | 1 | 0.07 | 0.5 | – | 4 | 1 | 5 | ✗ | ✗ | ✗ | ✗ | 37.2 | 45 | 38 (35%) | ✓ | few-decisions, long-for-its-decisions, easy-for-position |
| 91 | rainy-window | 6×6 | 3 | 9 | 3 | Lava+Breeze | 10 | 9089 | 3 | 0.30 | 0.5 | yes | >4 | 4 | 2 | ✗ | ✗ | ✗ | ✗ | 53.0 | 73 | 42 (28%) | ✓ |  |
| 92 | rainy-window | 5×6 | 3 | 7 | 0 | classic | 10 | 7119 | 4 | 0.40 | 0.5 | yes | 4 | 2 | 4 | ✗ | ✗ | ✗ | ✗ | 38.8 | 75 | 39 (33%) | ✓ |  |
| 93 | rainy-window | 6×6 | 3 | 8 | 2 | Frost+Lava | 14 | 8990 | 7 | 0.50 | 0.8 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 53.5 | 86 | 95 (24%) | ✓ |  |
| 94 | rainy-window | 6×6 | 3 | 12 | 6 | Frost+Breeze+Loops | 13 | 6505 | 3 | 0.23 | 0.5 | yes | >4 | 2 | 1 | ✗ | ✗ | ✗ | 34 | 55.7 | 73 | 113 (32%) | ✓ | long-for-its-decisions |
| 95 | rainy-window | 6×6 | 2 | 12 | 4 | Lava+Loops | 13 | 168 | 9 | 0.69 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 49.5 | 80 | 103 (38%) | ✓ | repetitive |
| 96 | rainy-window | 6×6 | 3 | 13 | 4 | Frost+Breeze | 10 | 3631 | 6 | 0.60 | 0.5 | yes | >4 | 3 | 14 | ✗ | ✗ | ✗ | ✗ | 53.0 | 73 | 131 (36%) | ✓ |  |
| 97 | rainy-window | 6×6 | 3 | 10 | 4 | Lava+Breeze | 10 | 1735 | 7 | 0.70 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 51.2 | 85 | 115 (29%) | ✓ |  |
| 98 | rainy-window | 6×6 | 3 | 13 | 3 | Frost+Loops | 8 | 7332 | 2 | 0.25 | 0.5 | yes | >4 | 2 | 9 | ✗ | ✗ | ✗ | ✗ | 43.8 | 65 | 76 (32%) | ✓ | repetitive |
| 99 | rainy-window | 5×6 | 3 | 9 | 0 | classic | 13 | 5593 | 2 | 0.15 | 0.5 | yes | >4 | 2 | 1 | ✗ | ✗ | ✗ | ✗ | 41.5 | 64 | 50 (30%) | ✓ | few-decisions, long-for-its-decisions |
| 100 | rainy-window | 6×6 | 2 | 12 | 3 | Breeze+Loops | 10 | 391 | 2 | 0.20 | 0.5 | yes | 2 | 1 | 20 | ✗ | 11 | 10 | ✗ | 38.0 | 40 | 90 (33%) | ✓ | shallow, repetitive, long-for-its-decisions |
| 101 | rainy-window | 6×6 | 3 | 13 | 3 | Lava+Loops | 11 | 3237 | 4 | 0.36 | 0.6 | yes | >4 | 3 | 3 | ✗ | ✗ | ✗ | ✗ | 52.3 | 77 | 80 (36%) | ✓ |  |
| 102 | rainy-window | 6×6 | 3 | 10 | 4 | Frost+Breeze | 14 | 8171 | 5 | 0.36 | 0.5 | yes | 2 | 1 | 2 | ✗ | 17 | 17 | ✗ | 48.4 | 64 | 147 (30%) | ✓ | shallow |
| 103 | rainy-window | 6×6 | 3 | 12 | 2 | Lava+Breeze | 13 | 4433 | 6 | 0.46 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | 17 | 51.2 | 84 | 95 (38%) | ✓ |  |
| 104 | rainy-window | 6×6 | 3 | 12 | 4 | Frost+Loops | 14 | 8293 | 4 | 0.29 | 0.5 | yes | 2 | 4 | 4 | ✗ | 18 | 14 | 33 | 51.6 | 62 | 98 (28%) | ✓ | shallow |
| 105 | rainy-window | 6×6 | 2 | 10 | 2 | Frost+Lava | 13 | 263 | 4 | 0.31 | 0.5 | yes | >4 | 2 | 1 | ✗ | ✗ | ✗ | ✗ | 44.6 | 67 | 129 (35%) | ✓ |  |
| 106 | rainy-window | 6×6 | 3 | 9 | 0 | classic | 13 | 12678 | 2 | 0.15 | 0.5 | yes | >4 | 4 | 2 | ✗ | ✗ | ✗ | ✗ | 45.0 | 58 | 119 (24%) | ✓ | few-decisions, repetitive, long-for-its-decisions |
| 107 | rainy-window | 6×6 | 3 | 7 | 4 | Lava+Loops | 10 | 11470 | 6 | 0.60 | 0.7 | yes | 2 | 2 | 0 | ✗ | 28 | ✗ | 18 | 49.4 | 79 | 80 (24%) | ✓ | shallow |
| 108 | rainy-window | 6×6 | 3 | 9 | 2 | Frost+Breeze | 13 | 6699 | 5 | 0.39 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 52.4 | 80 | 95 (28%) | ✓ |  |
| 109 | twilight-attic | 6×6 | 3 | 10 | 5 | Frost+Lava+Breeze | 10 | 4766 | 2 | 0.20 | 0.5 | yes | 4 | 0 | 0 | ✗ | ✗ | ✗ | ✗ | 41.6 | 59 | 88 (28%) | ✓ | long-for-its-decisions |
| 110 | twilight-attic | 6×7 | 3 | 13 | 5 | Frost+Lava+Loops | 14 | 3733 | 6 | 0.43 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 52.9 | 81 | 126 (26%) | ✓ |  |
| 111 | twilight-attic | 6×6 | 3 | 8 | 3 | Breeze+Loops | 11 | 14293 | 5 | 0.46 | 0.5 | yes | >4 | 4 | 1 | ✗ | ✗ | ✗ | ✗ | 44.1 | 79 | 78 (26%) | ✓ |  |
| 112 | twilight-attic | 6×7 | 3 | 13 | 5 | Lava+Breeze+Loops | 13 | 11723 | 4 | 0.31 | 0.5 | yes | 2 | 1 | 1 | ✗ | 14 | 14 | ✗ | 52.4 | 57 | 128 (24%) | ✓ | shallow, repetitive |
| 113 | twilight-attic | 6×6 | 2 | 12 | 3 | Frost+Lava+Breeze | 15 | 291 | 4 | 0.27 | 0.5 | yes | 4 | 0 | 0 | ✗ | ✗ | ✗ | 19 | 46.9 | 66 | 133 (33%) | ✓ |  |
| 114 | twilight-attic | 6×7 | 3 | 17 | 3 | Lava+Loops | 10 | 7641 | 6 | 0.60 | 0.8 | yes | 3 | 4 | 1 | ✗ | ✗ | 10 | ✗ | 49.8 | 84 | 151 (30%) | ✓ |  |
| 115 | twilight-attic | 6×6 | 3 | 12 | 0 | classic | 14 | 3212 | 5 | 0.36 | 0.6 | yes | 4 | 1 | 0 | ✗ | ✗ | ✗ | 19 | 44.1 | 67 | 147 (32%) | ✓ |  |
| 116 | twilight-attic | 6×7 | 3 | 14 | 4 | Lava+Breeze+Loops | 14 | 8363 | 6 | 0.43 | 1.0 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 54.5 | 88 | 128 (27%) | ✓ |  |
| 117 | twilight-attic | 6×6 | 3 | 7 | 3 | Lava+Breeze | 14 | 591 | 5 | 0.36 | 1.0 | yes | >4 | 1 | 0 | ✗ | ✗ | ✗ | ✗ | 54.3 | 70 | 74 (24%) | ✓ |  |
| 118 | twilight-attic | 6×7 | 2 | 12 | 6 | Frost+Lava+Loops | 9 | 304 | 5 | 0.56 | 0.5 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 45.3 | 84 | 116 (25%) | ✓ |  |
| 119 | twilight-attic | 6×6 | 3 | 9 | 4 | Frost+Breeze+Loops | 12 | 10979 | 3 | 0.25 | 1.0 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 49.6 | 76 | 129 (26%) | ✓ |  |
| 120 | twilight-attic | 6×7 | 3 | 15 | 4 | Lava+Breeze+Loops+Limit17 | 15 | 8834 | 8 | 0.53 | 0.7 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 64.4 | 88 | 139 (26%) | ✓ |  |
| 121 | twilight-attic | 6×6 | 3 | 9 | 3 | Frost+Breeze | 14 | 8292 | 4 | 0.29 | 0.5 | yes | 4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 57.9 | 75 | 101 (27%) | ✓ |  |
| 122 | twilight-attic | 6×6 | 3 | 9 | 0 | classic | 13 | 11817 | 5 | 0.39 | 0.5 | yes | >4 | 4 | 1 | ✗ | ✗ | ✗ | ✗ | 47.6 | 73 | 105 (32%) | ✓ |  |
| 123 | twilight-attic | 6×6 | 2 | 12 | 3 | Breeze+Loops | 10 | 460 | 3 | 0.30 | 0.5 | yes | 3 | 1 | 6 | ✗ | ✗ | 17 | ✗ | 43.9 | 62 | 101 (32%) | ✓ |  |
| 124 | twilight-attic | 6×7 | 3 | 12 | 3 | Lava+Loops | 14 | 17450 | 6 | 0.43 | 0.5 | yes | 4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 56.1 | 73 | 114 (28%) | ✓ |  |
| 125 | twilight-attic | 6×6 | 3 | 9 | 2 | Frost+Breeze | 15 | 10180 | 4 | 0.27 | 0.7 | yes | >4 | 0 | 2 | ✗ | ✗ | ✗ | ✗ | 52.1 | 50 | 100 (29%) | ✓ | repetitive |
| 126 | twilight-attic | 6×7 | 3 | 13 | 5 | Frost+Lava+Loops | 15 | 10742 | 5 | 0.33 | 0.5 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 67.9 | 75 | 114 (27%) | ✓ | repetitive |
| 127 | twilight-attic | 6×6 | 3 | 9 | 6 | Frost+Breeze+Loops | 14 | 8761 | 8 | 0.57 | 0.5 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 72.2 | 82 | 87 (30%) | ✓ |  |
| 128 | twilight-attic | 6×7 | 2 | 15 | 5 | Lava+Loops | 15 | 425 | 8 | 0.53 | 0.5 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 59.3 | 84 | 135 (30%) | ✓ |  |
| 129 | twilight-attic | 6×6 | 4 | 12 | 0 | classic | 15 | 93778 | 4 | 0.27 | 0.9 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 50.2 | 70 | 105 (35%) | ✓ | repetitive |
| 130 | twilight-attic | 6×7 | 3 | 8 | 5 | Frost+Lava+Loops | 13 | 8682 | 3 | 0.23 | 0.5 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 55.3 | 63 | 143 (27%) | ✓ | repetitive, long-for-its-decisions |
| 131 | twilight-attic | 6×6 | 3 | 11 | 3 | Frost+Loops | 14 | 5350 | 5 | 0.36 | 0.5 | yes | 3 | 3 | 2 | ✗ | ✗ | 19 | ✗ | 58.1 | 69 | 96 (36%) | ✓ |  |
| 132 | twilight-attic | 6×7 | 3 | 11 | 6 | Lava+Breeze+Loops | 15 | 12675 | 6 | 0.40 | 0.6 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | 36 | 61.7 | 82 | 128 (23%) | ✓ |  |
| 133 | twilight-attic | 6×6 | 2 | 10 | 4 | Frost+Lava+Breeze | 14 | 309 | 6 | 0.43 | 1.0 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 60.4 | 83 | 113 (33%) | ✓ |  |
| 134 | moonlit-clouds | 6×7 | 3 | 11 | 7 | Frost+Lava+Breeze | 14 | 7969 | 5 | 0.36 | 0.6 | yes | 2 | 2 | 0 | ✗ | 15 | 19 | ✗ | 56.3 | 71 | 158 (24%) | ✓ | shallow |
| 135 | moonlit-clouds | 6×7 | 3 | 15 | 4 | Frost+Lava+Loops | 15 | 5652 | 9 | 0.60 | 0.5 | yes | >4 | 7 | 1 | ✗ | ✗ | ✗ | ✗ | 66.1 | 80 | 128 (30%) | ✓ |  |
| 136 | moonlit-clouds | 6×7 | 3 | 14 | 4 | Breeze+Loops | 14 | 12481 | 4 | 0.29 | 0.5 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 60.2 | 60 | 157 (26%) | ✓ | repetitive |
| 137 | moonlit-clouds | 6×7 | 3 | 10 | 6 | Frost+Lava+Breeze+Loops | 15 | 3864 | 7 | 0.47 | 0.6 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | lava | 70.5 | 82 | 157 (24%) | ✓ | repetitive |
| 138 | moonlit-clouds | 6×7 | 2 | 12 | 3 | Frost+Lava+Breeze | 12 | 479 | 3 | 0.25 | 0.5 | yes | 3 | 2 | 0 | ✗ | ✗ | 12 | ✗ | 48.9 | 63 | 145 (26%) | ✓ |  |
| 139 | moonlit-clouds | 6×7 | 3 | 14 | 5 | Frost+Lava+Loops | 17 | 6033 | 8 | 0.47 | 0.7 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 64.4 | 84 | 135 (29%) | ✓ |  |
| 140 | moonlit-clouds | 6×6 | 3 | 11 | 0 | classic | 13 | 9897 | 5 | 0.39 | 0.5 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | ✗ | 49.8 | 76 | 76 (29%) | ✓ |  |
| 141 | moonlit-clouds | 6×7 | 3 | 13 | 5 | Lava+Breeze+Loops | 16 | 8713 | 5 | 0.31 | 1.0 | yes | 2 | 1 | 0 | ✗ | 19 | 16 | ✗ | 56.0 | 63 | 126 (24%) | ✓ | shallow, repetitive |
| 142 | moonlit-clouds | 6×7 | 3 | 11 | 4 | Frost+Lava+Loops | 16 | 14461 | 13 | 0.81 | 0.5 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 72.5 | 86 | 137 (23%) | ✓ |  |
| 143 | moonlit-clouds | 6×7 | 2 | 12 | 3 | Frost+Loops | 12 | 807 | 6 | 0.50 | 1.0 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 55.8 | 92 | 130 (27%) | ✓ |  |
| 144 | moonlit-clouds | 6×7 | 3 | 8 | 4 | Frost+Breeze+Loops | 11 | 25930 | 4 | 0.36 | 0.5 | yes | >4 | 3 | 3 | ✗ | ✗ | ✗ | ✗ | 54.4 | 69 | 158 (28%) | ✓ |  |
| 145 | moonlit-clouds | 6×7 | 3 | 17 | 6 | Frost+Lava+Loops | 14 | 5481 | 8 | 0.57 | 1.0 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | lava | 61.9 | 93 | 138 (26%) | ✓ |  |
| 146 | moonlit-clouds | 6×7 | 3 | 9 | 4 | Frost+Lava+Breeze | 16 | 14832 | 10 | 0.63 | 0.6 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | lava | 65.7 | 88 | 112 (24%) | ✓ |  |
| 147 | moonlit-clouds | 6×6 | 3 | 14 | 0 | classic | 16 | 6533 | 3 | 0.19 | 0.6 | yes | >4 | 3 | 1 | ✗ | ✗ | ✗ | ✗ | 53.3 | 66 | 105 (33%) | ✓ | few-decisions, long-for-its-decisions |
| 148 | moonlit-clouds | 6×7 | 2 | 13 | 3 | Breeze+Loops | 17 | 701 | 4 | 0.24 | 0.5 | yes | >4 | 4 | 8 | ✗ | ✗ | ✗ | ✗ | 50.8 | 64 | 114 (29%) | ✓ | repetitive, long-for-its-decisions |
| 149 | moonlit-clouds | 6×7 | 3 | 15 | 3 | Lava+Breeze | 15 | 346 | 7 | 0.47 | 0.5 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | ✗ | 67.0 | 86 | 114 (27%) | ✓ |  |
| 150 | moonlit-clouds | 6×7 | 3 | 13 | 4 | Frost+Lava+Loops+Limit18 | 16 | 13738 | 5 | 0.31 | 0.5 | yes | 4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 67.6 | 74 | 120 (25%) | ✓ |  |
| 151 | moonlit-clouds | 6×7 | 3 | 15 | 4 | Frost+Lava+Loops | 11 | 10238 | 6 | 0.55 | 0.5 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | ✗ | 59.0 | 82 | 114 (30%) | ✓ |  |
| 152 | moonlit-clouds | 6×7 | 3 | 9 | 4 | Frost+Lava+Loops | 17 | 12033 | 11 | 0.65 | 0.5 | yes | >4 | 4 | 0 | ✗ | ✗ | ✗ | lava | 65.2 | 85 | 158 (28%) | ✓ |  |
| 153 | moonlit-clouds | 6×7 | 2 | 13 | 4 | Lava+Breeze+Loops | 15 | 422 | 6 | 0.40 | 1.0 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 64.4 | 86 | 157 (25%) | ✓ |  |
| 154 | moonlit-clouds | 6×6 | 3 | 11 | 0 | classic | 15 | 4643 | 5 | 0.33 | 0.7 | yes | >4 | 4 | 1 | ✗ | ✗ | ✗ | ✗ | 56.0 | 72 | 95 (31%) | ✓ |  |
| 155 | moonlit-clouds | 6×7 | 3 | 12 | 6 | Frost+Lava+Breeze+Loops | 15 | 10364 | 6 | 0.40 | 0.5 | yes | >4 | 2 | 0 | ✗ | ✗ | ✗ | ✗ | 59.6 | 75 | 128 (25%) | ✓ | repetitive |
| 156 | moonlit-clouds | 6×7 | 3 | 13 | 4 | Frost+Breeze+Loops | 17 | 17245 | 4 | 0.24 | 1.0 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | ✗ | 63.9 | 66 | 114 (28%) | ✓ | decorative-mechanic, long-for-its-decisions |
| 157 | moonlit-clouds | 6×7 | 3 | 15 | 6 | Frost+Lava+Breeze+Loops | 15 | 7471 | 7 | 0.47 | 1.0 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | lava | 77.2 | 88 | 116 (27%) | ✓ | repetitive |
| 158 | moonlit-clouds | 6×7 | 2 | 12 | 3 | Frost+Lava | 14 | 477 | 6 | 0.43 | 0.5 | yes | >4 | 3 | 0 | ✗ | ✗ | ✗ | lava | 56.2 | 81 | 144 (28%) | ✓ |  |
| 159 | moonlit-clouds | 6×7 | 3 | 7 | 4 | Frost+Lava+Loops | 13 | 22335 | 8 | 0.62 | 0.5 | yes | 2 | 4 | 0 | ✗ | 18 | ✗ | ✗ | 60.2 | 76 | 134 (20%) | ✓ | shallow |
| 160 | moonlit-clouds | 6×7 | 3 | 13 | 6 | Frost+Lava+Loops | 14 | 11267 | 5 | 0.36 | 0.5 | yes | >4 | 5 | 0 | ✗ | ✗ | ✗ | ✗ | 65.8 | 75 | 128 (27%) | ✓ |  |
