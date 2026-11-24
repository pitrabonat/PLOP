# PLOP! project state

**Phase 8 · Gameplay depth, puzzle rebalancing and main menu**

The project is on `feature/puzzle-depth`, which branches from the Phase 7 work (`64dddcc`). Open the project in Unity `6000.3.29f1` with **PLOP → Play Game** or `Assets/Scenes/Gameplay.unity`. The game opens on the main menu. The build target is currently Android.

## Implemented in Phase 8

- **Diagnosis.** The [Phase 7 audit](Documentation/benchmarks/campaign_audit_phase7.md) found that a greedy "always closer" player solved 72/100 levels and 88 levels had no trap. The rules were deep enough; level selection was the problem. See [Puzzle quality](Documentation/PUZZLE_QUALITY.md).
- **Quality pipeline.**
  - `PuzzleInsight` measures each board with player models and route metrics. The weights in `ChallengeWeights` are configurable.
  - `QualitySearch` runs deterministic hill climbing toward per-slot targets.
  - `CampaignPlan` sets the build/peak/breather waves and the Tutorial/Easy/Normal/Hard/Expert thresholds (8 / 18 / 30 / 42).
- **Campaign rebuilt.**
  - 100 verified levels. Levels 1–10 are authored and keep their IDs.
  - Greedy now solves 14/100, all at level 25 or earlier. Only 21 levels have no trap.
  - The challenge average rises by band: 3.6 → 23.6 → 39.1 → 45.9 → 55.1.
  - There is also a 60-puzzle Daily pool. The [Phase 8 audit](Documentation/benchmarks/campaign_audit_phase8.md) has the details.
- **Perfect Moves.**
  - Every level stores `optimalMoves`, which is the BFS optimum.
  - The HUD shows `moves / perfect` and a star. The completion card shows "Perfect!" or "Sweet Dreams!" with a result line such as "6 moves · Perfect: 4 · +5".
  - The level list labels levels "Perfect".
  - The best result per level is saved. Using a hint during a visit disqualifies Perfect. Undo cannot cheat the counter.
- **Main menu.**
  - Shows the logo, three sleepy Plops, a large PLAY button that resumes the frontier, Daily Gift, Daily Puzzle, Levels, Settings and the Dream Dust balance.
  - A 0.22 s fade leads into gameplay. The gameplay header shows the level title instead of the game name, and a Home button returns to the menu.
- **Dream Dust.** The wallet (`plop.wallet.v1`) uses one-time grant keys:

  | Source | Dust |
  |---|---|
  | Completion | 5 |
  | Perfect | 10 |
  | Daily Gift | 20 |
  | Daily Puzzle | 15 (+10 for a Perfect) |

  There is a cosmetics data model only: no shop, no monetization.
- **Daily features.**
  - The Gift can be claimed once per local calendar day. There are no streaks and no notifications.
  - The Daily Puzzle is chosen from the date, deterministically and offline. It is tracked separately, shows the date under its title, and returns to the menu when done.
- **Save compatibility.** `PLOP_Pack_001_SlotHistory.json` maps old level IDs to the rebuilt slots. `LevelProgress` migrates completed/unlocked/resume IDs at launch. Corrupt meta saves are kept as `*.corrupt`.
- **Board scaling.** The header reserve is now 300 (was 414) and the side margin 3.5% (was 6.25%). Small boards fill about 93% of the safe width.
- **Level Lab.**
  - Added a player-model insight panel, showing first-swipe alternative lengths, dead ends, settlement order, traps and trivial flags.
  - Added a campaign overview with Plop, size and trivial filters, click-to-open and a most-similar comparison.
  - `Bench --insights` exports the same table: [campaign_insights_phase8.tsv](Documentation/benchmarks/campaign_insights_phase8.tsv).

## Verification (executed 2026-11-25)

- **Tests:** .NET 121/121, Unity EditMode 121/121, Unity PlayMode 74/74.
- **Content checks:** strict production verification and a sequential re-search reproduce the pack. The deadlock sidecar was regenerated at 600k states and covers all 100 levels.
- **Android APK:** `/Volumes/PLOP_DEV/Builds/Android/Phase8/PLOP-phase8-dev.apk`, an ARM64 development build. It was installed on the `Medium_Phone_API_37.0` emulator (1080×2400), because no phone was connected.
  - The menu, Daily Gift (+20 once, then "Tomorrow"), Daily Puzzle, Worlds and the level list were exercised by touch.
  - Levels 1–4 were completed with keyboard input, giving one non-Perfect result card.
- **Screenshots:** [Phase 8 screenshots](Documentation/Previews/Phase8/README.md).
- **Not verified:**
  - A physical phone.
  - iOS for Phase 8.
  - Hint timing on a device for the largest 4-Plop boards (worst start 99k states / 34 ms on desktop; the budget is 300k states / 200 ms).

## Next steps

- Playtest levels 11–30 for the Easy → Normal ramp. Levels 12 and 13 are still greedy-solvable by design (breathers).
- Optional: gesture demonstrations instead of text in tutorials 1–3.
- Possible new mechanics for later phases are listed in [Puzzle quality](Documentation/PUZZLE_QUALITY.md#recommended-future-mechanics).

## Continue development

Start with [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md) and [ARCHITECTURE.md](ARCHITECTURE.md). Rebuild content with `Bench --production`, then `--deadlocks … 600000`. Never delete `PLOP_Pack_001_SlotHistory.json`.
