# Solver (`Plop.Solver`, version `SolverInfo.Version = 1`)

## BfsSolver

`BfsSolver.Solve(LevelDefinition level, GameState start = null, SolverOptions options = null) → SolverResult`

- **Algorithm:** breadth-first search over canonical 64-bit state keys (`StateCodec`). Successors are
  produced by `MoveRules.Apply` — the exact game rules, no separate implementation. No-op commands are
  not edges. The goal is checked when a state is generated, so the first solution found is shortest.
- **Data structures:** three parallel arrays (`ulong key`, `int parent`, `byte move`) that double as the FIFO
  queue, plus `Dictionary<ulong,int>` for visited states. No per-node objects; one reusable scratch
  `GameState` and order buffer → negligible GC during search.
- **Result status:**
  - `Solved` — `Moves` is a shortest solution (`Array.Empty` if the start is already solved).
  - `ProvenUnsolvable` — the whole reachable state space was exhausted without a solved state.
  - `SearchLimitReached` — state budget, time budget or cancellation hit. **Never** treated as unsolvable.
  - `InvalidLevel` — structural validation of the level or start state failed (`Message` has details).
- **Budgets:** `SolverOptions.MaxStates` (deterministic, default 2,000,000), `MaxMilliseconds` (optional,
  non-deterministic), `Cancellation` (checked before starting and every 1024 expansions).
- **Arbitrary start states** (for hints): pass `engine.CurrentState`; it is validated first.
- `BfsSolver.VerifySolution(level, moves, start?)` replays moves through a fresh `GameEngine` and requires
  every move to change the state and the level to end solved exactly at the last move.

## StateSpaceAnalyzer

`StateSpaceAnalyzer.Analyze(level, maxStates)` explores **all** reachable states (forward BFS storing 4 edges
per state), then runs a reverse multi-source BFS from every solved state to get the exact distance-to-goal of
every state. Outputs: `Complete`, `ReachableStates`, `GoalStates`, `DeadEndStates` (no path to any goal),
`StartDistance` (−1 = proven unsolvable), a shortest `Solution` (first optimal direction in U, D, L, R order),
and `PathStats` along it: misleading moves (state-changing but not closer), trap moves (into dead ends),
forced steps (single optimal move), average branching. Incomplete analyses must not be used for decisions.

## Correctness evidence (tests)

- Hand-made cases: straight line, wall-desync puzzle, unreachable nest, lock-step Plops, budget, cancellation,
  invalid level, solving from a mid-game state, already-solved state.
- Randomized cross-check: on 5 × 60 random small levels (structurally invalid ones skipped, ≥ 20 checked per
  batch) BFS length equals an **independent iterative-deepening
  search that only uses the public `GameEngine` API**; BFS "unsolvable" verdicts are confirmed by IDDFS
  finding nothing; analyzer distance equals BFS length.

## Measured performance (Apple M4, .NET 10, single thread)

| Case | Result |
|---|---|
| Solve all 100 shipped levels | 123 ms total, 1.2 ms average |
| Largest search in the pack (7×7, 3 Plops) | BFS 28,636 states in 19 ms; full analysis 43,138 states in 49 ms |

Phones running IL2CPP will be slower (expect several ×); hints should still run off the main thread.

## Limitations

- State key must fit in 64 bits: `ceil(log2(W·H)) × plops ≤ 64` (e.g. 16×16 → 8 bits → 8 Plops). Larger
  configurations are rejected by validation.
- Plain BFS: memory grows with reachable states (~30 B/state + dictionary overhead). Fine for ≤ 7×7 and
  ≤ 3–4 Plops; bigger content will need A*/IDA* with an admissible heuristic or bidirectional search.
