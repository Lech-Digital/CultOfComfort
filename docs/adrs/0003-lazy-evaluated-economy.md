# 0003 — Lazy-Evaluated, Server-Authoritative Economy

- **Date:** 2026-10-09
- **Status:** Accepted

## Context

Players accumulate resources continuously, including while offline, across potentially many thousands of bases. Ticking every base on a server timer is wasteful and scales poorly. Letting the client compute and upload resource amounts is trivially exploitable (memory editing, clock manipulation).

## Decision

- Each resource is stored as `{ amount, ratePerHour, cap, asOfUtc }` on the BaseState aggregate.
- Current value is computed on read/command: `min(cap, amount + rate × (serverNow − asOfUtc))`, honoring the offline accrual cap from entitlements. Any rate change (building finished, worker reassigned, Peat upkeep) first **materializes** the value at `serverNow`, then updates the rate.
- Timers (construction, research, missionaries) are stored as `endsAtUtc` and resolved lazily on the next access, or by a scheduler only when a side effect must occur without a player request.
- The client sends **commands (intents)** via `/api/base/sync` — never amounts. Commands carry idempotency keys; the batch carries `expectedStateVersion`. The aggregate uses Postgres `xmin` as an optimistic concurrency token; conflicts return `409` and the client re-pulls.
- The client runs the same formulas (from the shared Simulation library) for responsive UI, but server state always wins.

## Consequences

### Positive
- No per-base background ticking; cost scales with player activity, not player count.
- Device clock and memory manipulation have no effect on economy.
- Idempotency + optimistic concurrency prevent double-spend from retries or multiple devices.

### Negative
- Rates must be piecewise-constant between events; non-linear effects (e.g., upkeep driving a resource to zero mid-interval) need careful materialization logic (compute the crossing point).
- Client must handle rejections and rollback of optimistic UI.
- Debugging historical values requires event logs, since intermediate states are not stored.
