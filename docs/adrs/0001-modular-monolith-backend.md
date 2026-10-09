# 0001 — Modular Monolith Backend with Hexagonal Modules

- **Date:** 2026-10-09
- **Status:** Accepted

## Context

The backend must serve a mobile F2P game with asynchronous multiplayer: base economy, world map influence, Tension/raid scheduling, combat verification, store, and notifications. The team is small (one full-stack engineer initially). Domains are distinct but share transactional needs (e.g., resolving a raid touches Base, Tension, and Notifications). Microservices would add operational overhead (deployments, network failure modes, distributed transactions) disproportionate to the team size and early-stage load.

## Decision

Build a **modular monolith** in .NET with a single deployable host and a single PostgreSQL database:

- Modules: Identity, Profile, Base, WorldMap, Tension, Combat, Store, Notifications.
- Each module is hexagonal: `Domain` (no project dependencies), `Application` (inbound/outbound ports), `Infrastructure` (EF Core, external SDKs). DI wired in the host's composition root only.
- **One PostgreSQL schema per module**; a module never queries another module's tables.
- Cross-module communication via in-process integration events and a **transactional outbox** for anything with side effects outside the transaction (push notifications, deferred jobs).
- Implementation classes are `internal`; module public surface is limited to its Application contracts and integration events.

## Consequences

### Positive
- Single deploy, simple local dev and debugging; low ops cost.
- Strict module boundaries keep a future extraction (e.g., Combat verification workers) cheap.
- Local transactions available where needed; outbox gives reliable side effects without distributed transactions.

### Negative
- Boundaries are enforced by convention and tests (architecture tests, e.g., NetArchTest), not by network — needs discipline.
- Shared database means a noisy module (e.g., WorldMap queries) can affect others until extracted or scaled.
- All modules scale together; heavy combat replay may later require splitting into a worker process.
