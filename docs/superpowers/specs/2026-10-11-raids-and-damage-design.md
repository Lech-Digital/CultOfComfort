# Raids, Tension & Building Damage (Village Slice 2) — Design

- **Date:** 2026-10-11
- **Status:** Approved by the owner (2026-10-11)
- **Implements:** GDD `01-core-loop.md` (Dusk warning window, auto-resolve, Soggy buildings, shields), `04-world-map-and-tension.md` (Tension states, raid budget), `02-economy.md` (repairs ≤ 10 %, map radius); ADR 0004 (analytical scheduling)
- **Delivered as two plans:** **A** — building damage, repair, Wattle Fence; **B** — Tension, raids, auto-resolve, map expansion
- **Out of scope:** the world map/missionaries/tithes (Tension's GDD sources), Morale and Bishop units, Vault, loot loss on defeat, push notifications

## Intent

Nights should be *caused* and should *cost* something. Tension rises while the village sits comfortably; a Hostile
Tension schedules a raid with a warning; raiders smash what stands near the road; wrecked things need repair. The owner
chose: real-time raids with a warning **and** the option to light the lamps early; damage dealt **during** the fight.

## A. Building damage (shared simulation)

- `BattleLayout` gains optional **structures**: `PlacedStructure(int Id, GridPosition Position, int MaxHealth, int Health)`
  for every village building and tower. Layouts without structures (the demo, all existing tests) behave exactly as before,
  so the demo hash `1ddcdf5947cfcaba` is unchanged.
- **Smashing:** a raider on the road (not chasing the Prophet, not frightened) whose position is within **1 tile** of an
  intact structure it has not hit yet stops for `StructureSwingTicks` (16 ticks), then deals its `StructureDamage`
  (Levy 15, Man-at-arms 40, Zealot 10 — a thrown hymnal) and walks on. Each raider hits each structure at most once.
- A structure at 0 health is **wrecked**; a wrecked tower stops firing for the rest of the Night. Events `StructureHit`,
  `StructureWrecked`; `Battle.Structures` exposes the final state. Structure health, each raider's swing state and the
  number of structures it has hit are hashed.
- Combat balance version **`2026.10.4`** (adds `StructureDamage`, `StructureSwingTicks`).

## A. Damage in the village

- `BuildingLevel` gains `Health`: Woodcutter/Peat Cut/Storage Hut/Cultist Hut 60/90/120, Cistern 80/120, towers 80,
  **Wattle Fence** 50 (new building: 15 Timber, instant, unlimited, no levels, model `fence`). Buildings under
  construction have 30.
- `Building.Wrecked`: a wrecked building produces nothing and gives no storage bonus or sluice charges; it still blocks
  the road. The Night layout carries wrecked structures at 0 health (a wrecked tower does not fire).
- **Repair** (`RepairBuilding(id)`): costs 10 % of the building's current level cost (rounded up, at least 1 of its main
  resource), instant, no builder.
- Until plan B, Dawn's `RecordNightResult` carries the ids of wrecked structures (plan B replaces this with a replay).
- **Client:** health bars over damaged buildings during the Night, a "SMASH!" floater on each hit, a poof on a wreck;
  wrecked buildings look soggy (grey-blue tint) in the Day; the building card offers **Repair · cost**; the drawer gets the
  fence card (`b_wattle_fence` icon).

## B. Tension and raids (village rules)

- `BaseState` keeps Tension lazily: `{ T0, RatePerHour, AsOf }`, `T(t) = clamp(T0 + rate × elapsed, 0, 150)`.
  Rate by Hearth level: L1 **0** (new-player shield), L2 **8/h**, L3 **12/h**. During a shield the rate is 0.
- At **70** (crossing time computed analytically) a **raid** is scheduled: `RaidId`, seed, `WarningEndsAt = crossing + 8 h`.
  Tension keeps rising during the warning.
- **Raid size** at the moment it is fought: `budget = BasePower[L] × (1 + (T − 70) / 100)`, BasePower L1 40, L2 54, L3 72;
  waves 4, or **5 when T ≥ 100** (Punitive). An **early** Night (no raid scheduled) uses `BasePower[L] × max(0.5, T / 70)`
  with 4 waves.
- Commands: `BeginNight()` → returns the raid parameters (seed, budget, waves, layout version) and marks the Night in
  progress; `ResolveNight(inputs)` → the village **replays the battle itself** (`Battle.Simulate` on its own layout, seed and
  the submitted input log) to get stars and wrecked structures, then applies salvage and damage. After any Night:
  Tension 0 and a **12 h shield**.
- **Auto-resolve:** when a read or command happens after `WarningEndsAt` and the raid was not fought, the village resolves
  it at `WarningEndsAt` with an empty input log (towers only) and keeps a **report** (stars, Hearth, wrecked, salvage) that
  the client shows once ("While you were away…").
- Development builds: `DebugRaiseTension(+50)` and `DebugSkipWarning()` commands (a server refuses them).

## B. Map expansion

- `VillageMap` gets a full size of **22 × 14** with zones: the current 18 × 11 is open at Hearth 1; rows 11–13 open at
  Hearth 2; columns 18–21 open at Hearth 3. Locked tiles are forest: unbuildable and obstacles for raiders (so the road
  never changes when land opens). Spawn (0,1) and Hearth (17,9) stay.
- The board draws locked land as forest with a "Hearth 2" / "Hearth 3" sign; the camera bounds follow the open area.

## B. Interface

- The Tension strip is real: fill `T / 150`, marks at 70 and 100, note "Calm", "Uneasy", "Wulfric's men are coming ·
  7h 12m", "Punitive raid · 2h", "Shielded · 11h".
- The main button: "Light the Lamps (early)" or "Face the raid · 7h left".
- Away report card on launch when an auto-resolved raid is unseen.

## Verification

- **.NET:** smashing (one swing per raider per structure, damage per type, swing pause), wrecked tower stops firing,
  layouts without structures unchanged (demo hash), wrecked/repair/fence rules, damage applied from a Night; Tension
  linear growth, shield, crossing time, scheduling, budget/waves, early Night, replay-authoritative resolution (a forged
  "3 stars" cannot be claimed), auto-resolve at the deadline (deterministic, idempotent), expansion zones per Hearth level.
- **Unity:** presenter labels (repair, Tension notes, button), PlayMode for repair and the away report, device run on
  the Pixel 10 (60 fps, probe MATCH).
