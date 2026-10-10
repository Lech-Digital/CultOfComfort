# Backlog & Open Questions

## Done

- [x] 2026-10-09 — Shared simulation skeleton: `Fix` (Q47.16), PCG32, BFS pathing, wave generator, `Battle` with 2 towers, 2 enemy types, Sluice Gate intervention, rolling state hash, server-replay verification; Sandbox + HTML replay viewer.

- [x] 2026-10-09 — Cult leader (tap-to-move, umbrella, Phantom Voice) in the sim; Unity 6.3 LTS client prototype (Dusk build → Night → Dawn verify); PlayMode smoke tests; Android IL2CPP ARM64 build running on a Pixel 10.
- [x] 2026-10-09 — **Cross-platform determinism verified on device:** IL2CPP/ARM64 (Pixel 10, Android 17) produces the same demo-Night state hash as .NET on macOS (`47274012541c0100`).

## Next Sessions

- [ ] Enemy roster stats (HP, speed, armor, damage types) and Authority AI personalities.
- [ ] Full Hearth 1–10 progression curve in a spreadsheet model (income vs. cost vs. time-to-level).
- [ ] Determinism CI: automate the on-device probe (Android emulator / Firebase Test Lab) and add iOS.
- [x] Touch UX: pinch/drag camera, UI Toolkit HUD instead of the IMGUI debug HUD, portrait + landscape (board turns 90° in portrait).
- [ ] Enemies can target the Prophet (positioning stakes); more leader skills.
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
