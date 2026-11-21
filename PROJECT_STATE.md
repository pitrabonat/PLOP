# PLOP! project state

**Phase 5 · Portrait visual overhaul and deadlock recovery**

The checkout is on `feature/visual-overhaul-deadlocks`, based on `704a58c`
(`checkpoint/pre-phase5`). Open the project in Unity `6000.3.29f1` and
choose **PLOP → Play Game** or press Play in `Assets/Scenes/Gameplay.unity`.

## Implemented

- A safe-area portrait fit that targets 87.5% of the safe width for small
  trays and scales tall or wide boards uniformly within the content region.
- A deep peach tray, ivory floor tiles, matte Pebbles, moon-emblem pillow
  Nests, and more prominent glossy jelly Plops. Sprite artwork and faces
  come from the same per-density atlas. Undo and Restart reset motion.
- A compact reference-led HUD with Settings, contextual tutorial messages,
  raised controls, and an immediate **Sweet Dreams!** completion action.
- A friendly recovery overlay with Undo Move, Restart Level, and dismissal.
  It appears after movement settles when the current board is proven deadlocked.
- The original 100-level campaign, saved progress, and 30/60 FPS preferences.

## Verification

- .NET 98/98, Unity EditMode 98/98 and Unity PlayMode 40/40. The full
  PlayMode campaign test replayed all 100 shipped boards. Strict catalog
  checks and deterministic sidecar regeneration passed.
- The production sidecar covers all 100 boards. It has 913,919 reachable
  keys and 10,490 proven deadlocks across 51 levels. Level 8’s `UL`
  route is a proven trap with a state-changing move still available.
- Tests cover the four target portrait sizes, a bounded unknown result,
  stale search cancellation, recovery, and completion priority.
- Build and device verification are recorded in
  [Documentation/TESTING.md](Documentation/TESTING.md).
  The [visual gap analysis](Documentation/VISUAL_GAP_ANALYSIS.md)
  records the two target concepts and the sprite integration points.

## Resume

Read [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md), and
[ARCHITECTURE.md](ARCHITECTURE.md). Use the `--deadlocks` command in
[CLAUDE.md](CLAUDE.md) to regenerate the sidecar after solver encoding,
puzzle rules, or level geometry changes.
