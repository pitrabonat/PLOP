# Phase 6 visual review

The starting point is commit `a93708f`, tagged `checkpoint/pre-phase6`.
The existing Gameplay scene ran at 1080×1920 portrait in Unity 6000.3.29f1.
Up and Left opened Level 8's proven recovery card, and two Undo actions
restored zero moves. The archived PNGs capture the Phase 5 Android player
at the same portrait size.

| Priority | Baseline observation | Production change | Verification |
|---|---|---|---|
| Characters and identity | A single peach key light works well. Body highlights read as painted patches, and matching a Plop to a Nest relies chiefly on color. | Add soft edge transmission, restrained glints and matching shape stamps. Keep eyes readable and anchor clear markers on Nests. | Inspect awake, overlapping and sleeping states in the game and check texture and renderer costs. |
| Motion and board | Move and collision respond promptly, but Undo immediately snaps the visuals. The board has reliable portrait fit. | Ease short Undo returns with cancellable child offsets. Keep rule positions authoritative and add a reduced-motion option. | Exercise rapid input, Undo, Restart and reduced motion. Review real motion footage. |
| World art | The peach field gives the tray room. Every level has the same setting and time of day. | Establish five illustrated settings, pale board surfaces and a consistent soft key. Place environmental details around a quiet center. | Inspect each World in play and keep environment rendering to one backdrop with a brief reveal. |
| Campaign interface | The HUD reads on small screens. A 100-level index gives little chapter context. | Open five illustrated cards from Levels. Keep chapter progress, locks and existing per-level access. Resume returns to the highest unlocked level. | Verify each chapter boundary, replay and existing saves. |
| Feedback and battery | Phase 5 had independent volume controls. Its Android emulator showed a 33 ms median interval at both frame caps. | Add quiet per-world audio, short opt-in native haptics and reduced motion. Keep an event-driven runtime and a shared board atlas. | Profile actual Unity and emulator runs and record the limits of device coverage. |

[Gameplay baseline](Previews/Phase6/baseline-phase5-level8.png) ·
[Completion baseline](Previews/Phase6/baseline-phase5-completion.png)
