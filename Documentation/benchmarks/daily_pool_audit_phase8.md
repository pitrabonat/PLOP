# Campaign audit — Daily pool

Pack: `PLOP_Daily_001.json` (60 levels). Metrics from `PuzzleInsight` on the exact state graph.
Greedy = naive player who always takes the unvisited swipe that brings Plops closest to free matching Nests.
Random = exact probability that random state-changing swipes finish within 2·optimal + 4 swipes.

## Summary by campaign band

| Levels | Avg optimal | Greedy solves | Greedy optimal-length | Avg random solve | Avg counter-intuitive steps | Levels with a trap | Avg decision points | Avg repetition | Avg unused floor | Avg challenge |
|---|---|---|---|---|---|---|---|---|---|---|
| 1–10 | 11.1 | 0/10 | 0/10 | 0.5% | 1.60 | 10/10 | 10.8 | 35% | 38% | 36.4 |
| 11–25 | 10.7 | 0/15 | 0/15 | 0.5% | 2.13 | 15/15 | 10.1 | 37% | 38% | 38.1 |
| 26–50 | 10.3 | 0/25 | 0/25 | 0.6% | 1.84 | 25/25 | 9.8 | 28% | 41% | 37.2 |
| 51–75 | 10.9 | 0/10 | 0/10 | 0.5% | 2.70 | 10/10 | 10.2 | 37% | 38% | 40.1 |

Whole pack: greedy solves **0/60** (optimally 0); random swiping finishes with ≥ 50% probability on **0** levels and ≥ 10% on **0**; 0 levels contain no counter-intuitive step; 0 levels contain no trap; 0 levels show ≤ 1 design pattern.

Patterns: A coordination · B settlement order · C retreat · D strategic blocking · E multi-step planning · F false opportunity · G efficiency.

## Per level

| # | Board | Plops | Walls | Opt | Greedy | Random | Counter | Retreat | Decisions | Traps (tempt/settle) | Order | Sleeper blocks | Repeat | Unused | Patterns | Challenge | Old score |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 5×5 | 2 | 8 | 9 | fails | 0.5% | 1 | 0 | 9 | 1/1 | – | 0 | 25% | 41% | `AB DEFG` | 32.5 | 32.5 Hard |
| 2 | 5×6 | 3 | 6 | 13 | fails | 0.3% | 3 | 0 | 13 | 1/1 | – | 0 | 50% | 38% | `AB DEFG` | 40.2 | 40.2 Hard |
| 3 | 5×6 | 3 | 10 | 13 | fails | 0.2% | 3 | 0 | 13 | 0/1 | – | 0 | 42% | 20% | `AB DE G` | 39.4 | 39.4 Hard |
| 4 | 6×6 | 3 | 11 | 12 | fails | 0.4% | 1 | 0 | 12 | 1/1 | – | 2 | 36% | 32% | `AB DEF ` | 37.5 | 37.5 Hard |
| 5 | 5×5 | 2 | 7 | 13 | fails | 0.2% | 1 | 0 | 11 | 1/1 | – | 0 | 33% | 28% | `AB DEFG` | 35.4 | 35.4 Hard |
| 6 | 5×6 | 3 | 9 | 11 | fails | 0.1% | 1 | 0 | 11 | 1/1 | – | 0 | 40% | 43% | `AB DEFG` | 35.5 | 35.5 Hard |
| 7 | 5×6 | 3 | 11 | 8 | fails | 1.3% | 2 | 1 | 8 | 0/1 | – | 0 | 43% | 42% | `ABCDE G` | 31.3 | 31.3 Hard |
| 8 | 6×6 | 3 | 9 | 12 | fails | 0.3% | 1 | 0 | 12 | 0/3 | – | 0 | 27% | 48% | `AB DE G` | 39.0 | 39 Hard |
| 9 | 5×5 | 2 | 6 | 10 | fails | 1.7% | 1 | 1 | 9 | 1/1 | – | 0 | 44% | 42% | `ABCDEFG` | 31.9 | 31.9 Hard |
| 10 | 5×6 | 3 | 10 | 10 | fails | 0.2% | 2 | 0 | 10 | 2/2 | – | 0 | 11% | 50% | `AB DEFG` | 41.4 | 41.4 Hard |
| 11 | 5×6 | 3 | 9 | 10 | fails | 0.8% | 1 | 0 | 10 | 2/2 | yes | 0 | 33% | 38% | `AB DEFG` | 39.2 | 39.2 Hard |
| 12 | 6×6 | 3 | 7 | 10 | fails | 0.8% | 2 | 1 | 8 | 1/1 | – | 0 | 44% | 41% | `ABCDEFG` | 33.8 | 33.8 Hard |
| 13 | 5×5 | 2 | 7 | 11 | fails | 0.3% | 3 | 3 | 9 | 1/1 | – | 0 | 50% | 33% | `ABCDEFG` | 40.8 | 40.8 Hard |
| 14 | 5×6 | 3 | 7 | 13 | fails | 0.4% | 3 | 1 | 11 | 0/1 | – | 0 | 25% | 22% | `ABCDE  ` | 38.9 | 38.9 Hard |
| 15 | 5×6 | 3 | 8 | 10 | fails | 0.6% | 2 | 1 | 10 | 1/1 | – | 0 | 33% | 45% | `ABCDEFG` | 36.3 | 36.3 Hard |
| 16 | 6×6 | 3 | 10 | 10 | fails | 0.2% | 2 | 2 | 10 | 0/2 | – | 0 | 44% | 31% | `ABCDE G` | 39.8 | 39.8 Hard |
| 17 | 5×5 | 2 | 6 | 10 | fails | 0.3% | 1 | 0 | 10 | 1/1 | – | 0 | 44% | 37% | `AB DEFG` | 33.3 | 33.3 Hard |
| 18 | 5×6 | 3 | 7 | 11 | fails | 0.3% | 1 | 0 | 11 | 1/3 | yes | 0 | 10% | 39% | `AB DEFG` | 42.7 | 42.7 Expert |
| 19 | 5×6 | 3 | 7 | 13 | fails | 0.1% | 3 | 0 | 12 | 0/1 | – | 0 | 42% | 43% | `AB DE G` | 38.9 | 38.9 Hard |
| 20 | 6×6 | 3 | 8 | 11 | fails | 0.2% | 2 | 0 | 11 | 0/1 | – | 0 | 50% | 43% | `AB DE G` | 34.4 | 34.4 Hard |
| 21 | 5×5 | 2 | 8 | 8 | fails | 1.9% | 1 | 0 | 8 | 1/1 | – | 0 | 29% | 47% | `AB DEFG` | 29.7 | 29.7 Normal |
| 22 | 5×6 | 3 | 9 | 13 | fails | 0.4% | 3 | 2 | 12 | 1/1 | – | 0 | 25% | 24% | `ABCDEFG` | 43.3 | 43.3 Expert |
| 23 | 5×6 | 3 | 10 | 8 | fails | 0.4% | 1 | 1 | 8 | 1/4 | – | 2 | 29% | 35% | `ABCDEFG` | 40.4 | 40.4 Hard |
| 24 | 6×6 | 3 | 7 | 13 | fails | 0.1% | 2 | 0 | 13 | 1/1 | – | 0 | 42% | 48% | `AB DEFG` | 40.0 | 40 Hard |
| 25 | 5×5 | 2 | 9 | 9 | fails | 1.1% | 5 | 1 | 9 | 1/1 | – | 0 | 50% | 38% | `ABCDEFG` | 40.2 | 40.2 Hard |
| 26 | 5×6 | 3 | 10 | 11 | fails | 1.1% | 1 | 0 | 10 | 1/1 | – | 0 | 20% | 35% | `AB DEFG` | 33.1 | 33.1 Hard |
| 27 | 5×6 | 3 | 8 | 13 | fails | 0.4% | 2 | 1 | 12 | 0/1 | – | 1 | 33% | 45% | `ABCDE G` | 37.8 | 37.8 Hard |
| 28 | 6×6 | 3 | 11 | 11 | fails | 0.2% | 2 | 0 | 11 | 1/1 | – | 0 | 50% | 44% | `AB DEFG` | 36.4 | 36.4 Hard |
| 29 | 5×5 | 2 | 8 | 8 | fails | 0.8% | 2 | 2 | 8 | 1/1 | – | 0 | 29% | 41% | `ABCDEFG` | 35.9 | 35.9 Hard |
| 30 | 5×6 | 3 | 9 | 10 | fails | 1.2% | 2 | 1 | 10 | 0/1 | – | 0 | 22% | 38% | `ABCDE G` | 34.2 | 34.2 Hard |
| 31 | 5×6 | 3 | 10 | 8 | fails | 0.9% | 1 | 1 | 8 | 1/2 | – | 0 | 14% | 50% | `ABCDEFG` | 35.0 | 35 Hard |
| 32 | 6×6 | 3 | 7 | 11 | fails | 0.2% | 2 | 0 | 11 | 2/2 | – | 0 | 20% | 48% | `AB DEF ` | 42.0 | 42 Expert |
| 33 | 5×5 | 2 | 8 | 9 | fails | 1.9% | 1 | 0 | 9 | 1/1 | – | 0 | 13% | 47% | `AB DEFG` | 31.4 | 31.4 Hard |
| 34 | 5×6 | 3 | 7 | 10 | fails | 0.8% | 2 | 1 | 10 | 0/2 | – | 0 | 22% | 39% | `ABCDE  ` | 37.2 | 37.2 Hard |
| 35 | 5×6 | 3 | 9 | 11 | fails | 0.2% | 2 | 1 | 11 | 0/1 | – | 0 | 40% | 33% | `ABCDE G` | 36.6 | 36.6 Hard |
| 36 | 6×6 | 3 | 9 | 10 | fails | 0.2% | 3 | 2 | 10 | 0/1 | – | 0 | 33% | 48% | `ABCDE G` | 39.1 | 39.1 Hard |
| 37 | 5×5 | 2 | 7 | 9 | fails | 0.8% | 1 | 1 | 9 | 2/2 | – | 0 | 25% | 50% | `ABCDEFG` | 37.7 | 37.7 Hard |
| 38 | 5×6 | 3 | 11 | 13 | fails | 0.9% | 1 | 1 | 9 | 0/1 | – | 3 | 8% | 32% | `ABCDE  ` | 35.3 | 35.3 Hard |
| 39 | 5×6 | 3 | 11 | 7 | fails | 1.0% | 2 | 0 | 7 | 0/1 | – | 0 | 33% | 37% | `AB DE G` | 29.5 | 29.5 Normal |
| 40 | 6×6 | 3 | 10 | 12 | fails | 0.1% | 3 | 1 | 10 | 1/1 | – | 0 | 36% | 46% | `ABCDEFG` | 40.6 | 40.6 Hard |
| 41 | 5×5 | 2 | 7 | 12 | fails | 0.6% | 2 | 1 | 12 | 1/2 | – | 0 | 45% | 28% | `ABCDEFG` | 41.0 | 41 Hard |
| 42 | 5×6 | 3 | 7 | 12 | fails | 0.1% | 3 | 1 | 12 | 1/1 | – | 0 | 27% | 35% | `ABCDEFG` | 42.6 | 42.6 Expert |
| 43 | 5×6 | 3 | 11 | 12 | fails | 0.3% | 1 | 0 | 11 | 1/1 | – | 0 | 18% | 32% | `AB DEFG` | 35.7 | 35.7 Hard |
| 44 | 6×6 | 3 | 10 | 11 | fails | 0.1% | 2 | 1 | 11 | 1/1 | – | 0 | 40% | 46% | `ABCDEFG` | 39.3 | 39.3 Hard |
| 45 | 5×5 | 2 | 8 | 9 | fails | 1.7% | 1 | 1 | 7 | 2/2 | – | 0 | 0% | 47% | `ABCDEFG` | 36.0 | 36 Hard |
| 46 | 5×6 | 3 | 11 | 10 | fails | 0.1% | 3 | 2 | 10 | 1/1 | – | 0 | 33% | 26% | `ABCDEFG` | 42.6 | 42.6 Expert |
| 47 | 5×6 | 3 | 9 | 10 | fails | 0.5% | 2 | 0 | 10 | 0/1 | yes | 0 | 33% | 38% | `AB DE G` | 35.3 | 35.3 Hard |
| 48 | 6×6 | 3 | 10 | 12 | fails | 0.1% | 2 | 0 | 11 | 1/1 | – | 1 | 36% | 50% | `AB DEFG` | 38.6 | 38.6 Hard |
| 49 | 5×5 | 2 | 9 | 7 | fails | 0.6% | 2 | 2 | 7 | 2/2 | – | 0 | 33% | 50% | `ABCDEFG` | 39.3 | 39.3 Hard |
| 50 | 5×6 | 3 | 10 | 10 | fails | 0.1% | 1 | 1 | 10 | 1/1 | – | 0 | 22% | 50% | `ABCDEFG` | 37.2 | 37.2 Hard |
| 51 | 5×6 | 3 | 9 | 10 | fails | 0.2% | 2 | 1 | 9 | 1/6 | – | 0 | 11% | 38% | `ABCDEFG` | 42.7 | 42.7 Expert |
| 52 | 6×6 | 3 | 8 | 11 | fails | 0.5% | 1 | 2 | 10 | 1/1 | – | 0 | 50% | 43% | `ABCDEF ` | 35.5 | 35.5 Hard |
| 53 | 5×5 | 2 | 9 | 7 | fails | 0.8% | 2 | 1 | 6 | 3/3 | – | 0 | 17% | 44% | `ABCDEFG` | 41.8 | 41.8 Hard |
| 54 | 5×6 | 3 | 11 | 12 | fails | 0.8% | 3 | 1 | 10 | 1/2 | – | 0 | 36% | 26% | `ABCDEFG` | 41.1 | 41.1 Hard |
| 55 | 5×6 | 3 | 10 | 9 | fails | 1.1% | 4 | 1 | 9 | 1/1 | – | 0 | 50% | 50% | `ABCDEFG` | 37.9 | 37.9 Hard |
| 56 | 6×6 | 3 | 10 | 10 | fails | 0.3% | 3 | 2 | 10 | 1/1 | – | 0 | 44% | 42% | `ABCDEFG` | 40.3 | 40.3 Hard |
| 57 | 5×5 | 2 | 8 | 11 | fails | 0.5% | 1 | 0 | 11 | 1/1 | – | 0 | 30% | 35% | `AB DEFG` | 34.5 | 34.5 Hard |
| 58 | 5×6 | 3 | 8 | 13 | fails | 0.1% | 3 | 0 | 12 | 1/1 | – | 0 | 42% | 32% | `AB DEFG` | 41.2 | 41.2 Hard |
| 59 | 5×6 | 3 | 5 | 13 | fails | 0.2% | 4 | 1 | 12 | 1/1 | – | 0 | 50% | 24% | `ABCDEFG` | 43.4 | 43.4 Expert |
| 60 | 6×6 | 3 | 10 | 13 | fails | 0.1% | 4 | 1 | 13 | 0/1 | – | 0 | 42% | 42% | `ABCDE G` | 43.1 | 43.1 Expert |
