# The Village, Slice 1 (Hearth 1 → 3 on One Map) — Design

- **Date:** 2026-10-11
- **Status:** Approved by the owner (2026-10-11)
- **Implements:** roadmap step 3 "building creation"; GDD `01-core-loop.md` (Day phase), `02-economy.md` (resources, Hearth 1–3); ADR 0003 (lazy-evaluated, command-driven economy)
- **Out of scope:** the server, raiders damaging buildings, research, missionaries, tithes, map expansion, cultist assignment, Peat heating upkeep, the Midden Sorter, Comfort/Squalor, Silver Pennies, Tension

## Intent

Turn the build screen into a village: real resources that grow while you are away, buildings you place, collect from and
upgrade, and a Hearth that grows from level 1 to 3 — on the same grid the raiders march across. It runs on the phone
today, behind interfaces that let a server take over later without rewriting the rules (owner: "proper interfaces so
implementing server later don't need lots of changes").

## Decisions

| Topic | Decision |
|---|---|
| Map | **One map**: buildings, towers and the Hearth share the battle grid; buildings block the road like towers; a path must stay open |
| Authority | **Phone now, server-ready**: rules in the shared Simulation library; the client changes the village only through commands via `IBaseGateway` |
| Time | Always passed in (`DateTimeOffset nowUtc`); the domain never reads a clock. Client clock via `IClock` |
| Economy maths | Integer only: amounts in thousandths, rates per hour; value = `min(cap, amount + rate × elapsed)` computed on read (ADR 0003) |
| Construction | One builder; timers are `EndsAtUtc`, resolved lazily on the next read/command; dev builds get "Finish now" |
| Towers | Buildings of kind *tower*: Scrap cost, instant, no levels, full refund on demolish (layout puzzle stays cheap to rearrange) |

## 1. Architecture

**Domain** — `src/CultOfComfort.Simulation/Base/` (no dependencies, netstandard2.1):

- `BuildingType` (`Hearth`, `Woodcutter`, `PeatCut`, `StorageHut`, `CultistHut`, `Cistern`, `SlurryChute`, `SaplingFlinger`), `ResourceType` (`Timber`, `Peat`, `Scrap`, `Devotion`).
- `ResourceBag` (record): four amounts, used for costs and storage.
- `Producer` buffer per producing building: `{ Milli, RatePerHour, CapacityUnits, AsOfUtc }`, value computed on read.
- `Building` entity: `Id`, `Type`, `Level`, `Position`, `Producer?`, `UpgradeEndsAtUtc?`.
- `VillageMap` (width, height, spawn, hearth tile, rocks) — moved from the client's `DemoMap` so placement can be validated by whoever holds authority.
- `BuildingTable` (balance, versioned with the rest): per type and level — cost, build seconds, production per hour, buffer hours, storage bonus, sluice charges, unlock Hearth level, max count per Hearth level.
- `BaseState` aggregate with behaviour: `Apply(BaseCommand, nowUtc) → CommandOutcome`, `Snapshot(nowUtc) → BaseSnapshot`, `ToBattleLayout() → BattleLayout`, `SluiceChargesBonus`. Keeps `Version` (incremented per accepted command) and the last 64 command ids (duplicates are accepted without effect).
- Commands (records, each with `CommandId: Guid`): `PlaceBuilding(type, position)`, `UpgradeBuilding(buildingId)`, `UpgradeHearth()`, `DemolishBuilding(buildingId)`, `Collect(buildingId?)` (null = all), `RecordNightResult(night, stars)`, `FinishConstructionNow()` (development only; a server will refuse or charge for it).
- `CommandOutcome`: `Accepted | Duplicate | Rejected(reason)` plus the new `Version`.

**Client application layer** — `client/.../Assets/Scripts/Village/` (plain C#):

- `IBaseGateway`: `Task<BaseSnapshot> LoadAsync(CancellationToken)`, `Task<CommandResult> SendAsync(BaseCommand, CancellationToken)` (`CommandResult` = outcome + fresh snapshot).
- `IClock` (`UtcNow`), `IBaseStore` (`Task<BaseSaveData?> LoadAsync(ct)`, `Task SaveAsync(BaseSaveData, ct)`).
- `LocalBaseGateway(IBaseStore, IClock, BuildingTable, VillageMap)`: runs `BaseState`, saves after each accepted command.
- `FileBaseStore`: `Application.persistentDataPath/village.json` via `JsonUtility` DTOs (anti-corruption mapping between domain and save format); a missing or corrupt file starts a fresh village and logs a warning.
- Later: `RemoteBaseGateway` posts the same commands to `/api/base/sync` with `expectedStateVersion`; nothing above the gateway changes.

## 2. Content (`BuildingTable` version `village-2026.10.1`; combat balance unchanged)

Storage cap per resource: Hearth L1 1 000, L2 2 500, L3 6 000 (GDD) plus Storage Huts. Start: Hearth 1, Timber 500,
Peat 60, Scrap 380, Devotion 0.

| Building | Unlock | Max count H1/H2/H3 | Levels: cost · build time · effect |
|---|---|---|---|
| Hearth | — | 1 | L2: 600 T, 50 S · 10 min. L3: 2 000 T, 300 S, 200 P, 100 D · 60 min (GDD) |
| Woodcutter's Lean-to | H1 | 2/3/4 | L1 50 T · 30 s · 100 T/h. L2 150 T, 10 S · 5 min · 170 T/h. L3 400 T, 40 S · 20 min · 260 T/h |
| Peat Cut | H1 | 1/2/3 | L1 80 T, 10 S · 1 min · 25 P/h. L2 200 T, 30 S · 8 min · 45 P/h. L3 500 T, 80 S · 30 min · 70 P/h |
| Storage Hut | H1 | 1/2/3 | L1 100 T · 1 min · +500 cap. L2 300 T · 10 min · +1 000. L3 800 T, 50 S · 40 min · +2 000 |
| Cultist Hut | H1 | 1/2/3 | L1 120 T · 2 min · 4 D/h. L2 350 T, 50 P · 12 min · 8 D/h. L3 900 T, 150 P · 45 min · 14 D/h |
| Cistern | H2 | 0/1/2 | L1 150 T, 30 S · 2 min · +1 sluice charge. L2 400 T, 100 S · 15 min · +2 |
| Slurry Chute (tower) | H1 | unlimited | 50 S, instant |
| Sapling Flinger (tower) | H2 (GDD) | unlimited | 70 S, instant |

Producers buffer up to 8 hours of output; **Collect** moves the buffer into storage up to the cap (the rest stays).
Night salvage (Dawn): Scrap = 40 + 40 × stars. Demolishing a non-tower refunds half its last level's cost; towers refund fully.

## 3. One map

- `BaseState.ToBattleLayout()`: towers → `PlacedTower`s; other buildings (and rocks) → obstacles; the Hearth stays the target tile.
- Placement is rejected on the spawn, the Hearth tile, rocks, occupied tiles, outside the grid, or when the road would be sealed (`BattleLayout.Validate`).
- `BattleSetup` gains `ExtraSluiceCharges` (default 0, so existing hashes are unchanged); the client passes the Cisterns' bonus.

## 4. Phases and interface

- The build phase is the **Day** (today's Dusk): place, collect, upgrade, demolish; then **Light the Lamps** starts the Night. Dawn's **Back to Dusk** sends `RecordNightResult`.
- **Build drawer** in the Day bar: a horizontal row of sticky-note cards (icon rendered from the model, name, cost, time); locked cards show "Hearth N". Selecting a card enters placement (tile cursor valid/invalid); tapping a tile sends `PlaceBuilding`; tapping the card again cancels.
- **Tap a building**: if its buffer holds at least 1 unit → `Collect` and a floating "+N Timber" note; otherwise a parchment **building card**: name, level, output per hour, buffer, Upgrade (cost · time, or "Max level"), Demolish, Close; while upgrading: remaining time and, in development builds, **Finish now**. Tapping the Hearth opens the Hearth card (upgrade costs, what the next level unlocks).
- **On the board**: buildings use their models (Hearth L1/L2/L3 by level); a construction scaffold bar over anything upgrading; a small resource icon over producers with something to collect.
- **Frame**: Timber, Peat, Scrap and Devotion pills show real values and caps (no demo mark); Hearth badge shows the real level. Pennies, Comfort and Tension stay demo.

## 5. Verification

- **.NET (xUnit):** accrual over time, capping, accrual from the moment construction ends; collect respects storage caps and leaves the rest; costs deducted, rejected when short; one construction at a time; lazy completion at `EndsAtUtc`; unlocks and max counts per Hearth level; Hearth upgrade raises caps and limits; placement rules incl. sealing the road; demolish refunds; duplicate `CommandId` applied once; `Version` increments only on accepted commands; `ToBattleLayout` maps towers and obstacles; night salvage; `ExtraSluiceCharges`; save-data round trip at the domain boundary.
- **Unity EditMode:** `LocalBaseGateway` — send/load, persistence round trip through an in-memory store, corrupt save → fresh village; `FileBaseStore` DTO mapping round trip.
- **Unity PlayMode:** build a Woodcutter from the drawer; tap to collect after time passes (fake clock); Hearth upgrade with Finish now; a building blocks the road in the Night.
- **Device:** Pixel 10 run, 60 fps, screenshots; determinism probe unchanged.
