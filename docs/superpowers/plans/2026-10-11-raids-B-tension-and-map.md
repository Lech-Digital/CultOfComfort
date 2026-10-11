# Raids Slice — Plan B: Tension, Raids, Auto-Resolve, Map Expansion

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nights are caused by Tension (real-time, lazy), announced with a warning, fought early or on time, replayed by the village itself for an authoritative result, auto-resolved when the player is away; the map grows with the Hearth.

**Architecture:** `BaseState` gains a lazy `Tension` value object, a scheduled `Raid`, a `NightInProgress` and an unseen `NightReport`. `BeginNight` returns a `NightPlan` (seed, budget, waves, sluice bonus); the client fights that plan live and sends its input log with `ResolveNight`; the village replays `Battle.Simulate` on its own layout to get stars and wrecks (never trusting the client's result). Deadlines are resolved lazily in `Advance`. `VillageMap` grows to 22×14 with zones that open per Hearth level; locked tiles are obstacles.

**Tech Stack:** netstandard2.1 simulation (xUnit + FluentAssertions); Unity 6.3 client.

**Spec:** `docs/superpowers/specs/2026-10-11-raids-and-damage-design.md` (section B)

## Global Constraints

- Lazy and integer: Tension stored as thousandths with a per-hour rate and an as-of time; crossing times computed analytically (ADR 0004). Time always passed in.
- Combat balance unchanged; the determinism probe keeps its own fixed 18×11 demo layout (hash `1ddcdf5947cfcaba`).
- The village never trusts a client result: `ResolveNight` takes inputs only; the client's live result must equal the village replay (shown as "Server replay: VERIFIED").
- Old saves load (missing Tension fields = calm, no raid); DTO format stays 1 with new optional fields.
- Numbers: rate L1 0 / L2 8 / L3 12 per hour; Hostile 70, Punitive 100, max 150; warning 8 h; shield 12 h; BasePower 40/54/72; waves 4 (5 when Punitive); early budget factor `max(0.5, T/70)`; an unfinished Night older than 15 min auto-resolves.

## Review Focus

1. The app is killed mid-Night (BeginNight without ResolveNight) — after 15 min the village resolves it with an empty log; Tension/shield still reset — test in Task 2.
2. A raid deadline passes while the app is closed for days — exactly one auto-resolve, at the deadline time, then the shield and fresh Tension; no second raid inside the shield — test in Task 2.
3. A forged `ResolveNight` (inputs that don't belong to the plan / wrong night) cannot award stars — test in Task 2.
4. Building on a locked tile, or a save with a building on a now-locked tile — rejected / invalid — test in Task 3.
5. Tension at exactly 70 with rate 0 (Hearth 1) never schedules a raid — test in Task 1.

---

### Task 1: Tension and raid scheduling (domain)

**Files:** Create `src/.../Base/Tension.cs` (value object + `TensionTable`), modify `BuildingTable.cs` (holds `TensionTable Tension`), `BaseState.cs`, `BaseSnapshot.cs`, `BaseData.cs`; Test `tests/.../Base/TensionTests.cs`.

**Interfaces (produces):**
- `public sealed record TensionTable(IReadOnlyList<int> RatePerHourByHearth, int Hostile, int Punitive, int Max, int WarningHours, int ShieldHours, IReadOnlyList<int> BasePowerByHearth)` with `Default` = (0/8/12, 70, 100, 150, 8, 12, 40/54/72).
- `public readonly record struct TensionLevel(long Milli, int RatePerHour, DateTimeOffset AsOfUtc)` with `At(DateTimeOffset now, int max) : long` (milli), `Value(now, max) : int`, `CrossingTime(int threshold) : DateTimeOffset?` (null when rate ≤ 0 or already at/above).
- Snapshot additions: `int Tension`, `TensionState TensionState` (`Calm | Uneasy | Hostile | Punitive`, ≥40 Uneasy), `DateTimeOffset? RaidWarningEndsAtUtc`, `DateTimeOffset? ShieldEndsAtUtc`.
- Dev commands `DebugRaiseTension(Guid, int Amount)`, `DebugSkipWarning(Guid)`.
- Rules in `Advance(now)`: rate from Hearth level (0 while shielded; resumes at shield end, so split the interval at the shield end); when no raid is scheduled and the crossing of 70 is ≤ now → schedule at the crossing time: `Raid { Id = next raid id, Seed = hash(raidId, version), WarningEndsAt = crossing + 8 h }`. Hearth upgrades change the rate (materialize first).

- [ ] Tests: linear growth at Hearth 2 (5 h → 40); rate 0 at Hearth 1 (never schedules even at 70 via debug raise — Review Focus 5: debug raise to exactly 70 at Hearth 1 schedules? **Rule:** scheduling needs rate > 0 *or* T ≥ 70 reached by an event; a debug raise to ≥70 schedules immediately at any level — so test instead that natural time at Hearth 1 never schedules); crossing schedules a raid with a warning 8 h after the crossing (not after "now" when the app opens late); shield stops the rise for 12 h then resumes; clamp at 150; state thresholds; debug raise/skip.
- [ ] Implement; `dotnet test`; commit `"Village: lazy Tension, raid scheduling with warnings, shields"`.

### Task 2: Nights as commands — plan, replay-authoritative resolve, auto-resolve, report

**Files:** Modify `Base/BaseCommands.cs`, `BaseState.cs`, `BaseSnapshot.cs`, `BaseData.cs`; Create `Base/NightPlan.cs`; Test `tests/.../Base/NightTests.cs`.

**Interfaces (produces):**
- `public sealed record NightPlan(int Night, ulong Seed, int Budget, int Waves, int ExtraSluiceCharges, bool Scheduled)`.
- `public sealed record NightReport(int Night, bool Scheduled, bool WhileAway, int Stars, int HearthHealth, int HearthMaxHealth, IReadOnlyList<int> Wrecked, int Salvage, string StateHashHex)`.
- Commands `BeginNight(Guid)` → outcome carries `NightPlan Plan`; `ResolveNight(Guid, int Night, IReadOnlyList<PlayerInput> Inputs)` → outcome carries `NightReport Report`; `AcknowledgeReport(Guid)`.
- `CommandOutcome` gains `NightPlan? Plan = null, NightReport? Report = null`.
- Snapshot gains `NightPlan? NightInProgress`, `NightReport? UnseenReport`.
- Budget: scheduled raid → `BasePower[L] × (1 + (T − 70)/100)` at BeginNight time (T capped 150), waves 5 if T ≥ 100 else 4; early → `BasePower[L] × max(0.5, T/70)`, 4 waves. Seed: scheduled raid's seed, or `hash(night, version)` early.
- Resolve: `Battle.Simulate(new BattleSetup(ToBattleLayout(), plan.Seed, plan.Budget, plan.Waves, inputs) { ExtraSluiceCharges = plan.ExtraSluiceCharges })`; stars → salvage (capped), wrecked structures → `Wrecked`; Tension → 0, shield 12 h, raid cleared, `LastNightRecorded = plan.Night`; report stored as unseen unless resolved by the player (the client shows its own Dawn card) — `WhileAway` true only for auto-resolves.
- Auto-resolve in `Advance(now)`: raid scheduled and `now ≥ WarningEndsAt` with no Night in progress → begin + resolve at `WarningEndsAt` with no inputs; Night in progress older than 15 min → resolve with no inputs at `startedAt + 15 min`.
- Inputs whose ticks are out of order or a `ResolveNight` for another night → rejected.
- `RecordNightResult` stays for old tests but the client no longer uses it.

- [ ] Tests: early Night plan size; scheduled plan size grows with waiting; Punitive gets 5 waves; resolve replays (a client claiming nothing — we only send inputs — the village's stars equal `Battle.Simulate`); resolve resets Tension + shield; wrong night rejected; auto-resolve at the deadline exactly once (snapshot 3 days later → one report, `WhileAway`, shield from the deadline); abandoned Night after 15 min; report acknowledged clears it; save round trip of raid/night/report.
- [ ] Implement; `dotnet test`; commit `"Village: Nights as commands — plan, replay-authoritative resolve, auto-resolve while away"`.

### Task 3: Map expansion

**Files:** Modify `Base/VillageMap.cs` (zones), `BaseState.cs` (placement, layout, validation), `client/.../Scripts/DemoMap.cs` (back to its own fixed 18×11 for the probe); Test `tests/.../Base/MapExpansionTests.cs`.

**Interfaces (produces):** `VillageMap(int Width, int Height, Spawn, Hearth, Rocks, IReadOnlyList<MapZone> Zones)`; `public sealed record MapZone(int MinX, int MinY, int MaxX, int MaxY, int OpensAtHearth)`; `VillageMap.IsOpen(tile, hearthLevel)`; `VillageMap.LockedTiles(hearthLevel)`; `VillageMap.Demo` = 22×14 with zones rows 11–13 → Hearth 2, columns 18–21 → Hearth 3; layouts add locked tiles as obstacles (both layout functions take the Hearth level).

- [ ] Tests: Hearth 1 open area = 18×11; placing on (5,12) rejected "Opens at Hearth 2"; after Hearth 2 accepted; the enemy path is identical at every level; a save with a building in a locked zone is invalid; the probe's `DemoMap` stays 18×11.
- [ ] Implement; `dotnet test` + sandbox hash; commit `"The map grows with the Hearth: forest zones open at Hearth 2 and 3"`.

### Task 4: Client — Tension strip, raid button, Night flow through commands, away report, forest zones

**Files:** `Village/VillagePresenter.cs` (Tension note/fill, button label, report card model), `Hud/HudView.cs` (strip marks, button text, dev buttons in Settings, report card = Dawn card variant), `GameController.cs` (StartNight → `BeginNight` → plan; EndNight → `ResolveNight(inputs)`; verified = live hash == report hash; on load/refresh show `UnseenReport` once then `AcknowledgeReport`), `Views/BoardView.cs` (full 22×14 ground, forest props + "Hearth N" sign on locked zones, toggled by Hearth level; camera bounds = open area), `Village/FileBaseStore.cs` DTO fields; Tests EditMode presenter (notes: "Calm", "Wulfric's men are coming · 7h 12m", "Shielded · 11h"; button "Face the raid · 7h left" / "Light the Lamps (early)"), mapper round trip; PlayMode (Night flow resolves through the gateway and shows VERIFIED; away report shown once; dev "Raise Tension" schedules a raid and the strip shows the countdown).

- [ ] Implement with TDD; full suites; commit `"Client: real Tension and raids, Nights resolved by the village, away report, forest zones"`.

### Task 5: Device check and docs (B)

- [ ] Pixel 10: probe MATCH; raise Tension (dev) → countdown; skip warning → away report; early Night → VERIFIED; Hearth 2 opens the south rows; 60 fps; screenshots `art/previews/raids/`; README/GDD/backlog; push.
