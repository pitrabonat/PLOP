PLOP! Phase 11.5 — Art handoff
16 separate 320x320 RGBA PNGs, sliced from a generated concept sheet.
These are starter sprites, not QA-approved production assets. Check edge halos and pivots at actual game scale.
Source sheet and visual target included. Visual_Target.png is a reference only, not a gameplay texture.

Unity:
Import individual sprites as Sprite (2D and UI), Single, alpha transparency enabled, mipmaps off for screen-space sprites, initially no compression for visual review. Keep existing logical positions, sorting, colliders, and identity cues.
Fit sprites within existing tile dimensions; keep consistent pixel density. Adjust fan blade pivot to hub and align it to housing; align portal vortex to frame and rotate independently. Preserve existing character rig and expressions; Plop and Nest sprites are optional visual candidates, not replacements for the complete customization system.
Lava and Frost are isolated tile variants. For connected surfaces, create adjacency-aware masks/edge overlays and seamless interior material layers using the current level definition. Do not repeat closed stone borders across connected lava pools. Do not change puzzle rules to match the art.
Use light shader or procedural particles for flowing lava, directional wind, ice trails and portal animation; no animation frames or dedicated VFX sprite sheet are included.
