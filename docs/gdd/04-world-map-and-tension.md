# 04 — Asynchronous Multiplayer & the Tension System

## World Map Structure

- **Shard** — hex-grid region shared by ~200 players and ~600 NPC nodes. Players spawn at the edges and expand inward.
- **Domain** — cluster of 15–30 nodes ruled by one NPC **Authority** (Warlord or Bishop) from a seat node (Castle / Cathedral).
- Player bases are **not** map targets. Players compete only via influence over NPC nodes. No direct PvP.

| Node type | Yield to contributors | Base receptivity | Authority weighting |
|---|---|---|---|
| Hamlet | Timber | 0.80 | ×1.0 |
| Village | Timber + Peat | 0.65 | ×1.0 |
| Market Town | Scrap | 0.50 | ×1.5 |
| Abbey | Devotion | 0.35 | ×2.0 if Bishop |
| Castle / Cathedral (seat) | None — prestige, Domain-wide bonus | 0.15 | ×3.0 |

## Claiming Influence

1. **Dispatch** a Missionary: 1 cultist + Devotion + Timber provisions. `travelTime = hexDistance × 20 min`.
2. **Preach** for 1 / 2 / 4 / 8h. Longer = better efficiency per hour, but missionary is unavailable longer.
3. **Influence gain per hour:**
   ```
   gain/h      = MissionaryPower × Receptivity × (1 − Suspicion) × DurationEff
   Receptivity = base × (0.5 + nodeSqualor / 200) × saturationPenalty
   ```
   Muddy, miserable villages convert easiest. The pitch writes itself.
4. **Node capacity = 100 influence.** Once full, new influence displaces existing holders proportionally — competitive but non-destructive.
5. **Patron** — top holder with ≥ 40 influence. +25% yield and node banner (cosmetic).
6. **Yield** — paid hourly to all holders proportionally to share; delivered lazily on collection.
7. **Decay** — −2% influence/day without refresh. Inactive players drift off the map.

## Tension

Tracked **per player per Authority**, `T ∈ [0, 150]`.

```
dT/dt = Σ_{nodes ∈ domain} ( influence_n × weight_n × k )   // k = 0.02 per hour
        × Envy                                              // Envy = 1 + min(Comfort, 1000) / 1000  (max ×2)
        − decay                                             // 1.5 / h baseline

Event spikes:
  becoming Patron of a node            +8
  preaching at the seat node           +15
  conversion of Abbey / Castle         +20

Tithe:
  −T proportional to value paid; 2nd tithe within 24h at 50% efficiency
```

| Tension | State | Consequence |
|---|---|---|
| 0–39 | Indifferent | Nothing |
| 40–69 | Uneasy | Suspicion +10% on Domain nodes; possible "Inspection" mini-raid (1 wave, low stakes) |
| 70–99 | Hostile | **Standard raid scheduled** |
| 100+ | Punitive | **Major raid** — 5 waves, siege units, Authority's champion, bigger loot |

## Authorities

| Authority | Units | Targets | Counterplay |
|---|---|---|---|
| **Warlord** | Levies, men-at-arms (resist Blunt), rams, fire archers | Hearth HP, towers | Slow + Heat, Architonnerre |
| **Bishop** | Flagellants, Inquisitors (preach at buildings → Morale damage), relic bearers (aura buff) | Morale, Devotion buildings | Lens Lantern, Comfort-based Morale buffs |

## Raid Triggering (Backend)

See [ADR 0004](../adrs/0004-analytical-tension-scheduling.md).

Between events, Tension is linear, so threshold crossings are computed analytically:

```
T(t)       = clamp(T0 + rate × (t − t0), 0, 150)
crossingAt = t0 + (threshold − T0) / rate          // only if rate > 0
```

1. Any influence change (missionary tick, displacement, tithe, Comfort change) → `TensionService.Recalculate(playerId, authorityId)` stores `T0`, `rate`, `t0`, `predictedCrossingAt`.
2. Scheduler picks due rows: `WHERE predicted_crossing_at <= now() LIMIT n FOR UPDATE SKIP LOCKED`.
3. Guards: no active raid, no shield, < 2 raids / 24h, Hearth ≥ 2. If blocked → push `predictedCrossingAt` to shield end.
4. **One transaction:** insert `RaidEvent` (seed, power budget, roster, `warningEndsAt = now + 8h`, `balanceVersion`) + outbox message `RaidScheduled`.
5. Outbox relay → push notification; raid becomes visible to client.
6. Deadline job at `warningEndsAt + grace` → auto-resolve still-`Scheduled` raids via the shared sim with an empty input log.

### Raid Power Budget

```
budget = basePower[HearthLevel] × (1 + (T − 70) / 100) × authorityLevelMult
```

Wave composition = deterministic generator (`budget`, `seed`) in the shared simulation library.
