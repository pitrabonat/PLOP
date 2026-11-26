# PLOP! — Game Rules (as implemented)

The single implementation of these rules is `Plop.Core.MoveRules.Apply` (`Assets/Scripts/Core/MoveRules.cs`).
Everything else (GameEngine, solver, generator, Level Lab, tests) calls it.

## Board

- Rectangular grid, `Width × Height` cells (validator allows 1..16 per side; the generator uses 4..7).
- **Coordinates:** integer `GridPosition(x, y)`, origin `(0,0)` is the **bottom-left** cell, `x` grows right,
  `y` grows **up**. Cell index = `y * Width + x`.
- Cells are floor or wall. Anything outside the grid behaves like a wall (reported as `BlockReason.Edge`).
- **Plops**: colored creatures with ids `0..n-1`. **Nests**: colored destination cells.
- Colors: Red, Blue, Yellow, Green, Purple, Orange. For every color, #Plops = #Nests.

## Commands

`Up`, `Down`, `Left`, `Right`. One command attempts to move **every awake Plop exactly one cell** in that direction.

## Movement resolution (one atomic transition)

1. Collect awake Plops and sort them **front-to-back** for the direction:

   | Direction | Primary | Secondary tie-break | Final tie-break |
   |---|---|---|---|
   | Right | larger `x` first | smaller `y` first | smaller id |
   | Left | smaller `x` first | smaller `y` first | smaller id |
   | Up | larger `y` first | smaller `x` first | smaller id |
   | Down | smaller `y` first | smaller `x` first | smaller id |

   Two Plops never share a cell, so primary+secondary are always distinct: the final id tie-break exists
   only for completeness, and the outcome never depends on ids or collection order (tested).
2. Process Plops in that order. A Plop moves to the target cell if it is inside the grid, not a wall and
   **not occupied by any Plop** (awake or asleep), using positions already updated by Plops processed earlier.
   Consequence: a Plop directly behind a moving Plop follows it in the same command ("train");
   a Plop behind a blocked Plop is blocked too.
3. A Plop that enters a **Nest of its own color falls asleep immediately**. Sleeping Plops never move again
   and act as obstacles. **Non-matching Nests are ordinary floor.**
4. A Plop moves at most one cell per command.

## Phase 10 mechanics

Four special floor tiles. A cell holds at most one tile. Walls are separate. Nests and Plop starts are only allowed on plain floor.

| Tile | Text | Shape cue | Rule |
|---|---|---|---|
| **Frost** | `~` | Pale-blue rounded ice with crystal facets | Entering it starts a **slide** in the current direction. The slide continues across any floor until something stops it. |
| **Lava** | `!` | Warm orange cushion with an ember glow and a notched warning rim | An awake Plop entering it **fails the attempt**. |
| **Breeze** fan | `^ v < >` | Rounded fan with a chevron that points its way | Pushes a Plop that enters it, in the fan's fixed direction. The **power** (1–3) is the maximum number of cells. |
| **Loop** | `0`–`9` | A small ring; both ends of a pair share colour and symbol | Exactly two ends per pair. Entering one end **teleports** the Plop to the other. |

### One command, two phases (`MoveRules.Apply`)

1. **Step.** Every awake Plop takes its normal one-cell step, exactly as above: front-to-back, trains follow, a matching Nest puts the Plop to sleep.
   - A Plop that steps onto Lava fails the attempt immediately, and nothing else resolves.
2. **Effects.** Each Plop that stepped onto a mechanic tile now resolves its whole forced-movement chain, **one Plop at a time, in the same front-to-back order**. Plops that have not resolved yet are obstacles where the Step phase left them.

On every cell a Plop enters, these checks apply in order:

| # | Cell | Effect |
|---|---|---|
| 1 | Lava | The attempt fails. Resolution stops for every Plop. |
| 2 | Matching Nest | The Plop falls asleep and stops. A non-matching Nest is floor. |
| 3 | Loop | Only the first Loop the Plop enters this command acts. The Plop teleports to the paired end, **keeping its direction and motion**. If the paired end is occupied, the teleport is cancelled and the Plop rests on the entry Loop (`PlopBlocked`, reason `PortalExit`). Later Loops this command are floor. |
| 4 | Breeze fan | Each fan acts at most once per Plop per command. A *walking* Plop is pushed up to the fan's power, in the fan's direction. A *sliding* Plop turns to the fan's direction and keeps sliding. A revisited fan is floor. |
| 5 | Frost | A walking or pushed Plop starts sliding in its current direction. |

**How forced movement stops:**
- It stops before walls, the board edge and any Plop, awake or asleep. Nothing is pushed or chained.
- A push stops when its power is used up, unless the Plop entered Frost on the way, which turns the push into a slide.
- A slide stops only at an obstacle, its matching Nest, Lava, or a cancelled teleport.
- A walking Plop stops on the Loop exit.

**Termination.** Each fan acts once per Plop, there is at most one teleport, and movement between them runs in straight lines. So every chain is finite: at most (fans + 3) × (width + height) cells. A safety budget throws `UnresolvedMovementException` rather than returning a truncated result; it is never reached by valid levels (fuzz-tested).

**Scoring.** One swipe is one move, however far the Plop travels. The solver and the Perfect target count swipes.

**Failure.** `GameState.Failed` records the reason (Lava) and the Plop.
- A failed state accepts no commands (`MoveStatus.Ignored`). Undo and Restart recover.
- Failures are part of state equality. The solver never expands them, and the state-space analyzer routes them to a single terminal "failed" node.

**Limited Moves.** This is optional (`moveLimit` in the level). After the last allowed command, if the level is not solved, the engine raises `OutOfMoves` and ignores commands until Undo or Restart. Forced movement never costs extra moves.

New events:
- `PlopSlid` and `PlopPushed`: one straight segment each.
- `PlopTeleported`.
- `PlopBurned`.
- `LevelFailed` and `OutOfMoves`.

Together with `PlopMoved`, these events are the full per-command chain. Level Lab's effect debugger lists them.

## Outcomes

- If no Plop moved, the command is a **no-op**: state, history and move count are unchanged
  (blocked events are still reported so views can play a "bump").
- The level is **solved** when every Plop is asleep. Further commands are ignored (`MoveStatus.Ignored`).
- **Undo** restores the exact state before the last state-changing command (works after completion too).
- **Restart** restores the level's starting state and clears history.

## Phase 11 meta rules (outside the engine)

These sit in `Plop.Meta` and the controller. The move rules above are unchanged.

- **Perfect** = solved in exactly the BFS-verified optimum with no hint shown during that visit. Undo shortens the counted history. A count below the optimum is rejected as corrupt.
- **Limited Moves challenges** (`PLOP_Pack_003`): levels 30, 45 and 60 allow optimum + 2; 80, 100 and 120 allow + 1; 140 and 155 allow exactly the optimum. Changing a level's limit gives it a new id.
- **Hints** cost 15 Dream Dust, once per level and exact board state. They are free on levels 1–6 and on levels with an instruction. **Grace:** if the player cannot afford a hint and has tried this visit at least twice (restart after moving, Lava, out of moves, proven dead end), the level's first hint is free, once per level id ever (`hintgrace:ID`).
- **Daily Puzzle**: one per local calendar date from `PLOP_Daily_002`. It only uses mechanics whose introducing level the player has completed (Frost 6, Breeze 21, Lava 35, Loops 49). The day's pick is remembered. No move limit, no streak, and missed days cost nothing. Rewards are paid once per date.

## Invariants (enforced by validation / tests)

- A Plop can never start on its matching Nest (that would be a free sleep and makes states ambiguous).
- Therefore "asleep" ⇔ "standing on a matching Nest" — the solver's state key relies on this.
- Plops and Nests never start inside walls; Plops never overlap; Nests never overlap.

## Events (for presentation)

`PlopMoved`, `PlopBlocked` (reason Edge / Wall / Plop + blocker id), `PlopSettled`, `LevelCompleted`,
`MoveUndone`, `LevelRestarted`, `LevelLoaded`. Gameplay never touches visuals; views react to events.
