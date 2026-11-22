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
- Validate changed content with the .NET suite and Unity. Keep generated project
  settings, caches, and builds out of commits, and commit the Unity asset metadata.
- Update `PROJECT_STATE.md` and the development log after meaningful milestones
  so either agent can resume from the repository and executed verification.
