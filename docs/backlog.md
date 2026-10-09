# Backlog & Open Questions

## Done

- [x] 2026-10-09 — Shared simulation skeleton: `Fix` (Q47.16), PCG32, BFS pathing, wave generator, `Battle` with 2 towers, 2 enemy types, Sluice Gate intervention, rolling state hash, server-replay verification; Sandbox + HTML replay viewer.

## Next Sessions

- [ ] Enemy roster stats (HP, speed, armor, damage types) and Authority AI personalities.
- [ ] Full Hearth 1–10 progression curve in a spreadsheet model (income vs. cost vs. time-to-level).
- [ ] Unity project: import the Simulation DLL, render a Night from live `Battle` state at fixed 20 Hz.
- [ ] Cross-platform determinism CI: run recorded battles on IL2CPP (iOS/Android) and compare hashes with .NET.
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
