# PLOP! development

Read [PROJECT_STATE.md](PROJECT_STATE.md) before changing the project. Use
[CLAUDE.md](CLAUDE.md) for the shared engineering conventions, exact Unity version,
mounted volume paths, and build commands. [ARCHITECTURE.md](ARCHITECTURE.md)
describes the system boundaries and [DEVELOPMENT_LOG.md](DEVELOPMENT_LOG.md) records milestones.

- Work in the existing checkout at `/Volumes/PLOP_DEV/Unity/Projects/PLOP`.
  Inspect the Git branch and working tree before editing. Preserve unrelated edits.
  Have one coding agent modify the working tree at a time.
- Keep Core, Solver and Generation free of UnityEngine. Engine, solver and tools
  use `MoveRules.Apply`. Generate and verify content offline. Regenerate the
  deadlock sidecar when rules, state encoding or campaign geometry change.
  Show recovery only for an exact offline proof or a completed solver search.
- Preserve rounded Soft Toy Minimalism and use the existing palette, SDF shapes,
  and event-driven views for new interface elements. Put cosmetic chapter
  mapping in `Resources/Worlds` and derive unlocks from saved level IDs.
  Keep non-color matching cues readable and honor reduced motion.
  Use PLOP → Play Game to preview the production gameplay scene in the Editor.
- Keep puzzle quality measurable: new or rebuilt levels must pass `PuzzleInsight`
  floors (see `CampaignQualityTests`, `Phase10CampaignTests`), store a verified `optimalMoves`,
  and keep IDs of unchanged puzzles. The shipped campaign is `PLOP_Pack_002` (160 levels, built by
  `Bench --expanded`); Phase 8's pack is archived and only read for save migration. Never reset saves
  silently, and never mark a changed puzzle complete.
- Mechanics (Frost, Lava, Breeze, Loops) live only in `MoveRules.Apply` with the documented precedence
  (GAME_RULES.md). Forced movement must stay bounded: run Level Lab → Inspect chains or
  `ForcedChainInspector` on new content. Lava failure and Limited Moves end the attempt without penalty;
  Undo and Restart always recover.
- Dream Dust grants and hint spends are one-time keys written atomically. A hint is charged only after a
  verified route for the unchanged board. Keep rewards small and cosmetic: no ads, no real-money purchases.
- Keep Meta (records, wallet, daily rules) engine-free behind `IKeyValueStore`/`IClock`.
  The game opens on `MainMenu`; PlayMode tests that load the shipped pack press PLAY first.
- Keep cosmetics visual-only: a `CharacterLook` may change sprites, faces, accessories, Nest art and effects,
  never board positions, rules, solver input or progress. Keep the colour-independent match mark on every
  body and Nest. New cosmetics are a `CharacterArt` recipe plus a catalog entry (IDs are save keys; never
  rename or reuse one); run PLOP → Characters → Validate Cosmetics. Unlocks go through
  `CosmeticCollection` (one wallet write); never add a second currency, wallet or save manager.
  See [Documentation/CHARACTERS.md](Documentation/CHARACTERS.md).
- Keep the one-command gesture buffer in presentation. Clear it on recovery,
  modal entry, navigation and suspension. Honor reduced motion when dispatching
  queued input. Keep the optional profiler readout in development builds.
- Validate changed content with the .NET suite and Unity. Keep generated project
  settings, caches, and builds out of commits, and commit the Unity asset metadata.
- Update `PROJECT_STATE.md` and the development log after meaningful milestones
  so either agent can resume from the repository and executed verification.
