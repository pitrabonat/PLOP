# PLOP! – Phase 13 Branding Pack

## Recommended defaults (pending owner approval)
- Icon: `01_Icons/Hero_Pink/Master_iOS_1024.png` and `GooglePlay_512.png`.
- Logo: choose `05_Minimal_Plum.png` for a restrained visual identity, or `01_Jelly_Pink.png` for a more playful brand.
- Feature graphic: `02_Adventure_Jelly_1024x500.png` or `03_Adventure_Minimal_1024x500.png`. Moonlight is a simpler alternative.
- Splash: Day or Night, see MP4/GIF previews; select one, not both at random.

## Integration instructions for Claude Code / Codex
1. Read AGENTS.md, CLAUDE.md, PROJECT_STATE.md. Preserve approved UI, mechanics, save data and release configuration.
2. Copy only selected runtime assets to a suitable `Assets/Art/Branding/` folder. Keep alternate designs, previews, and references outside the runtime build. Do not overwrite source artwork.
3. Master launcher/store images are full-bleed RGB 1024x1024 and 512x512, without baked corner masks. Android adaptive foreground/background are separate 432px layers with central safe-zone positioning; inspect masking on real devices. The adaptive foreground and alternative icons use the prior Phase 11.5 mascot sprite (lower native resolution than the main hero icon), so their visual style differs slightly. Verify official store size, file size and format requirements before submission.
4. Logos are separate transparent PNGs. Generated logo art is cropped at native resolution; inspect alpha edges against cream and dark backgrounds. The two minimal wordmarks are raster alternatives. Logo 06 is meant for dark backgrounds. Do not ship font binaries from this pack (none included).
5. Feature graphics are 1024x500 RGB PNGs. The adventure variants share the same illustrated background; Moonlight is a separate composed alternative. These are promotional illustrations, not gameplay screenshots. Use actual game screenshots for store screenshots.
6. Splash: use the PNG layers and `Animation_Timeline.json`. MP4/GIF are previews ONLY; do not ship them as startup videos. Use a lightweight Unity Canvas animation with normalized responsive layout, bottom-center mascot pivot, squash/stretch, soft landing and logo pop-in. Timeline values are reference canvas pixels, Y increases downward, interpolate with smoothstep, start at t=0. Duration ~1.35s, never force a delay after game readiness; skip/reduce motion if requested and respect native Android/iOS launch-screen requirements. Stop animation when transitioning.
7. Preserve existing 30/60 FPS and Reduced Motion. No new SDKs. Test startup on Android/iOS; capture actual screenshots and record selected asset names in PROJECT_STATE.md.
8. Do not publish or claim final approval without the owner selecting a variant. Verify trademark/name and platform requirements separately.

## Notes
The prior visual examples in chat were references, not recoverable exact source images. This pack contains new original generated imagery, raster logo variants and compositions using the earlier PLOP sprite pack. Concept sheets in 05_References are inspiration only; do not crop them for final store screenshots or runtime art. Splash poster PNGs are preview renders, not background layers.
