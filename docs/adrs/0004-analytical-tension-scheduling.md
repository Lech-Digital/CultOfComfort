# 0004 — Analytical Tension Evaluation and Raid Scheduling

- **Date:** 2026-10-09
- **Status:** Accepted

## Context

Tension is tracked per player per NPC Authority and rises with the player's influence in that Authority's Domain. Crossing thresholds must schedule PvE raids reliably, exactly once, with a warning window and push notification. Polling all player–authority pairs on a fixed interval scales poorly and introduces latency proportional to the polling period.

## Decision

- Between influence-changing events, Tension changes linearly: `T(t) = clamp(T0 + rate × (t − t0), 0, 150)`.
- On any relevant event (missionary tick, influence displacement, tithe, Comfort change, raid outcome), `TensionService.Recalculate` materializes `T` at now, recomputes `rate`, and stores `predictedCrossingAt = t0 + (threshold − T0) / rate` (null if `rate ≤ 0`).
- A scheduler (`BackgroundService` or Hangfire/Quartz) selects due rows with `WHERE predicted_crossing_at <= now() ... FOR UPDATE SKIP LOCKED`, applies guards (no active raid, no shield, < 2 raids / 24h, Hearth ≥ 2), and if blocked defers `predictedCrossingAt` to the shield end.
- Raid creation is a single transaction: insert `RaidEvent` (seed, budget, roster, `warningEndsAt`, `balanceVersion`) + outbox `RaidScheduled`. The outbox relay sends the push notification.
- A deadline job auto-resolves raids still `Scheduled` after `warningEndsAt + grace` using the shared simulation with an empty input log.

## Consequences

### Positive
- Work is proportional to events, not to player count × time.
- Raids trigger at the exact crossing time; `predictedCrossingAt` can be surfaced to players as "rumours", improving legibility.
- `SKIP LOCKED` + transactional outbox give exactly-once raid creation across multiple worker instances.

### Negative
- Every influence-affecting code path must call `Recalculate`; a missed call yields stale predictions (mitigate with a low-frequency safety sweep).
- Influence decay and Envy (Comfort-dependent) make the true curve only piecewise-linear; recalculation on Comfort changes is required.
- Adds a scheduler component to operate and monitor.
