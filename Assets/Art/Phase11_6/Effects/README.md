# Effects (Phase 11.6)

The art pack ships no effect sprites or animation frames. The mechanic effects are lightweight and procedural:

- Wind streaks: `Resources/Shaders/PlopWind.shader`, one quad per cell of airflow.
- Lava flow, heat pulses and bubbles: `Resources/Shaders/PlopLava.shader`, animating the supplied Lava art.
- Ice glint, twinkles and slide shimmer: `Resources/Shaders/PlopFrost.shader`, on the supplied Frost art.
- Particles (frost flakes, wind dots, portal sparks, lava steam): small sprites baked into the runtime
  `ShapeAtlas` (`fx.*` keys) and pooled by `BoardFx`.

Drop future hand-made effect sprites here; keep them small, tintable (light grey on transparent) and
event-driven (see `BoardEnvironment`).
