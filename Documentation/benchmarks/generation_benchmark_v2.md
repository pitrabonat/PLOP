# Production campaign generation

Seed `0x504C4F5021`, generator v2, solver v1, .NET 10.0.15. Single-threaded desktop run.

| Metric | Value |
|---|---|
| Generation calls | 100 (0 exhausted attempts) |
| Candidates generated | 558 |
| Rejected candidates | 458 |
| Accepted levels | 100 |
| Rejected: StructureInvalid | 0 |
| Rejected: SearchBudget | 0 |
| Rejected: Unsolvable | 5 |
| Rejected: Trivial | 129 |
| Rejected: TooLong | 5 |
| Rejected: DifficultyMismatch | 0 |
| Rejected: Duplicate | 1 |
| Rejected: VerificationFailed | 0 |
| Rejected: QualityMismatch | 318 |
| Rejected: NearDuplicate | 0 |
| Solver failures (proven unsolvable) | 5 |
| Search-budget failures | 0 |
| Duplicate rejection rate | 0.18% of candidates |
| Generation duration | 1.48 s |
| Avg generation time per candidate | 2.65 ms |
| Solution length avg / min / max | 10.5 / 1 / 20 |
| Difficulty Tutorial | 4 |
| Difficulty Easy | 6 |
| Difficulty Normal | 22 |
| Difficulty Hard | 34 |
| Difficulty Expert | 34 |
| Accepted solutions replay-verified via GameEngine | 100 / 100 |
| Strict verification (structure, IDs, duplicates, shortest replay, Undo and Restart) | PASS |
| Full deterministic regeneration | byte-identical |

| Levels | Count | Shortest moves | Score range | Peak-to-recovery dips | Wall use | Forced coordination | Must move away | Forced order or settlement traps |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1–10 | 10 | 1–6 | 4.4–20.1 | 0 | 3 | 2 | 2 | 1 |
| 11–25 | 15 | 4–9 | 13.5–24.3 | 3 | 9 | 12 | 7 | 6 |
| 26–50 | 25 | 6–15 | 19.9–38.7 | 5 | 24 | 24 | 16 | 10 |
| 51–75 | 25 | 9–16 | 29.4–48.8 | 5 | 25 | 25 | 23 | 17 |
| 76–100 | 25 | 12–20 | 38.9–56.0 | 5 | 25 | 25 | 25 | 17 |

## Desktop runtime measurements

This run used the precomputed v2 pack on Apple Silicon with .NET 10.0.15.
BFS solved all 100 initial states in 214.0 ms total (2.14 ms average). Level 98
had the largest BFS search at 38,999 states in 7.4 ms. Its full graph had
45,350 states and took 25.6 ms. The per-command benchmark measured 139 bytes
allocated and 0.12 µs per call across 100,000 `GameEngine.TryMove` commands.
These results measure desktop execution. Device frame times, energy use and
thermals still need on-device measurement.
