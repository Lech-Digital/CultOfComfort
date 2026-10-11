# Backlog & Open Questions

## Done

- [x] 2026-10-09 — Shared simulation skeleton: `Fix` (Q47.16), PCG32, BFS pathing, wave generator, `Battle` with 2 towers, 2 enemy types, Sluice Gate intervention, rolling state hash, server-replay verification; Sandbox + HTML replay viewer.

- [x] 2026-10-09 — Cult leader (tap-to-move, umbrella, Phantom Voice) in the sim; Unity 6.3 LTS client prototype (Dusk build → Night → Dawn verify); PlayMode smoke tests; Android IL2CPP ARM64 build running on a Pixel 10.
- [x] 2026-10-09 — **Cross-platform determinism verified on device:** IL2CPP/ARM64 (Pixel 10, Android 17) produces the same demo-Night state hash as .NET on macOS (`47274012541c0100` at the time).

- [x] 2026-10-10 — **Prophet threat:** enemies stop to fight him (max 3), Zealot ranged enemy, health/regen/swoon with a revive grace period, Hot Stone Therapy targeted skill. Balance 2026.10.2; demo hash `12c3fe1f0e23fb59`, verified identical on the Pixel 10.

- [x] 2026-10-11 — **Art direction + pipeline:** Muddy Satire / Marchlands / Golden Mud / Manuscript+Junk (spec `docs/superpowers/specs/2026-10-10-art-direction-and-pipeline-design.md`); Blender-scripted assets (`art/`), 65 models (Prophet, raiders, villagers, creatures, Hearth L1–L3, towers, village, nature, 2024 junk, effects); code-driven comic animation (`PartAnimator`); board, raiders, towers and effects use the models with primitive fallback. Gallery: `art/previews/README.md`.
- [x] 2026-10-11 — **HUD kit + game frame** (spec `docs/superpowers/specs/2026-10-11-hud-kit-and-game-frame-design.md`): Pillow-generated parchment/notes/tape/hazard/stamps, 18 Blender-rendered icons, IM Fell + Permanent Marker, layout C on parchment with locked shortcuts and demo-marked placeholders.
- [x] 2026-10-11 — **The Prophet as bait + skill feedback** (spec `docs/superpowers/specs/2026-10-11-prophet-bait-and-skill-feedback-design.md`): melee raiders leave the road to fight him (balance 2026.10.3, demo hash `1ddcdf5947cfcaba`); raider status icons, rising notes, sweeping flood, first-use tips.

## Next Sessions

- [ ] Replace `DemoEconomy` (demo-marked Timber, Peat, Devotion, Pennies, Comfort, Tension) with real base state — buildings work.

- [x] 2026-10-11 — On-device check of the art build (Pixel 10): 60 fps Dusk, 60 fps busy Night (11 raiders, 6 towers, all abilities at fast-forward), demo hash still `12c3fe1f0e23fb59`, server replay verified. Owner: graphics good enough for alpha/beta.
- [ ] One-off frame hitches (min 10–30 fps for a single frame) at Night start and on first ability/effect use: pool battle effects and pre-warm shaders.
- [ ] Spec step 5: Golden Mud lighting rigs (day/night), colour grading, bloom; store-page screenshots.

- [ ] Enemy roster stats (HP, speed, armor, damage types) and Authority AI personalities.
- [ ] Full Hearth 1–10 progression curve in a spreadsheet model (income vs. cost vs. time-to-level).
- [ ] Determinism CI: automate the on-device probe (Android emulator / Firebase Test Lab) and add iOS.
- [x] Touch UX: pinch/drag camera, UI Toolkit HUD instead of the IMGUI debug HUD, portrait + landscape (board turns 90° in portrait).
- [ ] Golden-hash regression corpus for recorded battles (fails on any unintended sim change).
- [ ] Morale + Bishop units (preach at Devotion buildings), Comfort-era towers (Pressure Sluice, Hypocaust Vent).
- [ ] Balance: difficulty cliff between budget ~54 and ~60 on the demo map — investigate smoothing.
- [ ] BaseState aggregate (DDD) + `/api/base/sync` command pipeline.
- [ ] Solution / project structure for the modular monolith (+ architecture tests).

## Open Design Questions

- Influence displacement: proportional to all holders, or weighted against the Patron first?
- Should Inspection mini-raids (Tension 40–69) be optional content or forced?
- Tithe pricing: fixed per Authority or dynamic (Authority demands what you have most of)?
- Era 2/3 hub levels (Hearth 6–10) unlocks and Enlightenment-era content depth.
- Shard lifecycle: persistent shards vs. seasonal resets (affects influence economy and cosmetics).
- Cosmetic catalogue scope for launch.

## Open Technical Questions

- Fixed-point library: in-house Q32.32 struct vs. existing library (licensing, IL2CPP performance).
- Scheduler: plain `BackgroundService` + `SKIP LOCKED` vs. Hangfire/Quartz.
- Push provider (FCM/APNs direct vs. a service).
- Hosting target and environments.
