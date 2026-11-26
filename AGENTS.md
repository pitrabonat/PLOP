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
  and `PuzzleQuality` floors (see `CampaignQualityTests`, `Phase11CampaignTests`), store a verified
  `optimalMoves`, and keep IDs of unchanged puzzles. The shipped campaign is `PLOP_Pack_003` (160 levels,
  built deterministically by `Bench --balanced`); Packs 001/002 are archived and only read for save
  migration and comparison. A Move-limit change is a new id; add an alias only when the rule got looser.
  Never reset saves silently, and never mark a changed puzzle complete. Audit with
  `Bench --quality-audit` or Level Lab → Balance & quality before and after content changes.
- Mechanics (Frost, Lava, Breeze, Loops) live only in `MoveRules.Apply` with the documented precedence
  (GAME_RULES.md). Forced movement must stay bounded: run Level Lab → Inspect chains or
  `ForcedChainInspector` on new content. Lava failure and Limited Moves end the attempt without penalty;
  Undo and Restart always recover.
- Dream Dust grants and hint spends are one-time keys written atomically. A hint is charged only after a
  verified route for the unchanged board (`HintPolicy`: after two tries the first hint on a level is free
  once when Dust is short). Keep rewards small and cosmetic: no ads, no real-money purchases. Rerun
  `Bench --economy` after changing rewards or prices.
- Playtest metrics stay local and dev-only (`[Conditional("DEVELOPMENT_BUILD")]`): no analytics SDKs.
  Never present simulated-player numbers as human playtest results.
- Keep Meta (records, wallet, daily rules) engine-free behind `IKeyValueStore`/`IClock`.
  The game opens on `MainMenu`; PlayMode tests that load the shipped pack press PLAY first.
- Keep cosmetics visual-only: a `CharacterLook` may change sprites, faces, accessories, Nest art and effects,
  never board positions, rules, solver input or progress. Keep the colour-independent match mark on every
  body and Nest. New cosmetics are a `CharacterArt` recipe plus a catalog entry (IDs are save keys; never
  rename or reuse one); run PLOP → Characters → Validate Cosmetics. Unlocks go through
  `CosmeticCollection` (one wallet write); never add a second currency, wallet or save manager.
  See [Documentation/CHARACTERS.md](Documentation/CHARACTERS.md).
- Environment visuals (Phase 11.5) are presentation only: they read the Core trace and never change positions,
  rules, solver input, levels or prices. Mechanic looks live in `Resources/Shaders` (one shared material per
  shader, per-renderer data packed with `PlopShaders.Pack`); World materials and living backgrounds are data in
  `Resources/Worlds/BoardMaterials.json` and `Ambient.json` (their colours are the documented exception to
  "PlopPalette only"). No per-object `Update`: one ambient clock, event-driven effects from the pooled
  `BoardFx`, and every pending effect cleared by `BoardEnvironment.Sync`. Keep only the current World's
  ambient alive, nothing full-screen, Reduced Motion frozen (fan direction and Loop pair pips still visible)
  and Battery Saver thinned. Preview with PLOP → Visual Lab → Preview Window; run `Phase11_5EnvironmentTests`.
- Keep the one-command gesture buffer in presentation. Clear it on recovery,
  modal entry, navigation and suspension. Honor reduced motion when dispatching
  queued input. Keep the optional profiler readout in development builds.
- Validate changed content with the .NET suite and Unity. Keep generated project
  settings, caches, and builds out of commits, and commit the Unity asset metadata.
- Update `PROJECT_STATE.md` and the development log after meaningful milestones
  so either agent can resume from the repository and executed verification.
