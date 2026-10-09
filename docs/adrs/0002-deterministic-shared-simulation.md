# 0002 — Deterministic Shared Combat Simulation with Server Replay

- **Date:** 2026-10-09
- **Status:** Accepted

## Context

Night (tower-defense) phases are played locally on the client, possibly while offline, and submitted later. Rewards (Scrap salvage, Tension reduction, star rating) must not be forgeable. Streaming the whole battle to the server in real time is incompatible with offline play and costly. Validating only a client-reported result is trivially exploitable.

## Decision

Implement the combat simulation as a **pure, deterministic C# library** — `CultOfComfort.Simulation` (netstandard2.1, zero dependencies) — referenced by both Unity and the server.

- The sim is a function: `f(layoutSnapshot, seed, balanceVersion, inputLog) → result + finalStateHash`.
- Determinism rules:
  - Fixed-point arithmetic only (no `float`/`double` in sim state).
  - Fixed 20Hz tick; seeded PRNG owned by the sim.
  - Stable ordering everywhere (pathfinding tie-breaks, no reliance on `Dictionary`/`HashSet` iteration order).
  - Unity physics, `Time.deltaTime`, and coroutines are presentation-only.
- Flow: `/api/combat/start` locks the layout snapshot and issues a signed, single-use session (seed, layoutHash, expiry). Client plays and records only player inputs (interventions). `/api/combat/resolve` submits the input log; the server **replays** and its result is authoritative.
- Wave generation from `(budget, seed)` also lives in the shared library.
- CI asserts identical `finalStateHash` between server (.NET) and a Unity IL2CPP build for a corpus of recorded battles.

## Consequences

### Positive
- Strong anti-cheat with negligible server cost (input logs are a handful of events; replay is milliseconds).
- Offline play supported; auto-resolve for absent players reuses the same code path.
- Single source of truth for combat rules and balance formulas; replays come for free (bug reports, sharing).

### Negative
- Fixed-point math and determinism discipline constrain gameplay programming and libraries (no Unity physics for gameplay).
- Determinism bugs are subtle; requires the cross-platform hash test suite from day one.
- Balance data must be versioned and pinned per raid (`balanceVersion`), adding config management overhead.
