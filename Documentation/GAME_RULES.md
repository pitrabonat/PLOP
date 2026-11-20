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

## Outcomes

- If no Plop moved, the command is a **no-op**: state, history and move count are unchanged
  (blocked events are still reported so views can play a "bump").
- The level is **solved** when every Plop is asleep. Further commands are ignored (`MoveStatus.Ignored`).
- **Undo** restores the exact state before the last state-changing command (works after completion too).
- **Restart** restores the level's starting state and clears history.

## Invariants (enforced by validation / tests)

- A Plop can never start on its matching Nest (that would be a free sleep and makes states ambiguous).
- Therefore "asleep" ⇔ "standing on a matching Nest" — the solver's state key relies on this.
- Plops and Nests never start inside walls; Plops never overlap; Nests never overlap.

## Events (for presentation)

`PlopMoved`, `PlopBlocked` (reason Edge / Wall / Plop + blocker id), `PlopSettled`, `LevelCompleted`,
`MoveUndone`, `LevelRestarted`, `LevelLoaded`. Gameplay never touches visuals; views react to events.
