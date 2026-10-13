# Backlog & Open Questions

## Done

- [x] 2026-10-09 — Shared simulation skeleton: `Fix` (Q47.16), PCG32, BFS pathing, wave generator, `Battle` with 2 towers, 2 enemy types, Sluice Gate intervention, rolling state hash, server-replay verification; Sandbox + HTML replay viewer.

- [x] 2026-10-09 — Cult leader (tap-to-move, umbrella, Phantom Voice) in the sim; Unity 6.3 LTS client prototype (Dusk build → Night → Dawn verify); PlayMode smoke tests; Android IL2CPP ARM64 build running on a Pixel 10.
- [x] 2026-10-09 — **Cross-platform determinism verified on device:** IL2CPP/ARM64 (Pixel 10, Android 17) produces the same demo-Night state hash as .NET on macOS (`47274012541c0100` at the time).

- [x] 2026-10-10 — **Prophet threat:** enemies stop to fight him (max 3), Zealot ranged enemy, health/regen/swoon with a revive grace period, Hot Stone Therapy targeted skill. Balance 2026.10.2; demo hash `12c3fe1f0e23fb59`, verified identical on the Pixel 10.

- [x] 2026-10-11 — **Art direction + pipeline:** Muddy Satire / Marchlands / Golden Mud / Manuscript+Junk (spec `docs/superpowers/specs/2026-10-10-art-direction-and-pipeline-design.md`); Blender-scripted assets (`art/`), 65 models (Prophet, raiders, villagers, creatures, Hearth L1–L3, towers, village, nature, 2024 junk, effects); code-driven comic animation (`PartAnimator`); board, raiders, towers and effects use the models with primitive fallback. Gallery: `art/previews/README.md`.
- [x] 2026-10-11 — **HUD kit + game frame** (spec `docs/superpowers/specs/2026-10-11-hud-kit-and-game-frame-design.md`): Pillow-generated parchment/notes/tape/hazard/stamps, 18 Blender-rendered icons, IM Fell + Permanent Marker, layout C on parchment with locked shortcuts and demo-marked placeholders.
- [x] 2026-10-11 — **The Prophet as bait + skill feedback** (spec `docs/superpowers/specs/2026-10-11-prophet-bait-and-skill-feedback-design.md`): melee raiders leave the road to fight him (balance 2026.10.3, demo hash `1ddcdf5947cfcaba`); raider status icons, rising notes, sweeping flood, first-use tips.
- [x] 2026-10-11 — **Village slice 1** (spec `docs/superpowers/specs/2026-10-11-village-slice-1-design.md`): real Timber/Peat/Scrap/Devotion, buildings placed on the battle grid, collect/upgrade/demolish, Hearth 1→3, night salvage, Cistern sluice charges; server-ready `IBaseGateway`; verified on the Pixel 10 (save survives relaunch and app update, 60 fps).
- [x] 2026-10-11 — **Raids slice** (spec `docs/superpowers/specs/2026-10-11-raids-and-damage-design.md`): raiders smash buildings/towers, repair, Wattle Fence; real-time Tension, announced raids with warnings, early Nights, replay-authoritative resolution, auto-resolve while away, map grows with the Hearth. Verified on the Pixel 10.
- [x] 2026-10-13 — **Raid roster + Night mood:** raiders grow with the Hearth (geese, goats, boars, peasants at 1; Wulfric's levy at 2; priests and Lord Wulfric at 3), per-type shouts/models/announcements, darker Nights, new sluice flush. Verified on the Pixel 10.
- [x] 2026-10-13 — **First-hour arc** (spec `docs/superpowers/specs/2026-10-13-first-hour-arc-design.md`): the Prophet's 13 errands (village rule, claimable rewards, saved), errand note with hints, Light the Lamps waits for the first-Night errand, welcome line; Devotion 10/18/28, Hearth 2 at 450 Timber; producers can't stand where no road can reach; ghosts by the Hearth, Turn button stays on screen, pill readouts, demo placeholders hidden; Sandbox `pace` bot (Hearth 2 ≈ 32 min, Hearth 3 ≈ 10 h for a building player). Played errands 1–7 on the Pixel 10: Hearth 2 started at ~20 min, no hitches.

## Next Sessions

- [ ] Server: `RemoteBaseGateway` + `/api/base/sync` (commands with ids, `expectedStateVersion`, local projection mirror per the `IBaseGateway` contract).
- [ ] Map expansion with Hearth level; Midden Sorter (Scrap producer); Comfort/Squalor; Tension → real Nights.

- [x] 2026-10-11 — On-device check of the art build (Pixel 10): 60 fps Dusk, 60 fps busy Night (11 raiders, 6 towers, all abilities at fast-forward), demo hash still `12c3fe1f0e23fb59`, server replay verified. Owner: graphics good enough for alpha/beta.
- [x] 2026-10-13 — Frame hitches measured on the Pixel 10 (dev builds log every frame over 50 ms with its cause): Night start and first skills were already smooth (57–60 fps); the 150 ms freeze at Night end (two full replays on the main thread) now runs on worker threads behind a one-call-at-a-time village gateway.
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

- [ ] Found playing the errands (2026-10-13), owner to decide: the "collect" errand pays out only ~2 Timber after a minute (seed the first Lean-to's store?); 2 villagers for 4 jobs in the first 20 min (villagers arrive every 10 min); the first Night's nosy peasants wreck most of the starter village, Cottage included (beds drop to 2); the errand note is large on a phone.

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
