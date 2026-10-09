# Cult of Comfort — Documentation

Mobile F2P base-building / tower-defense. A cynical time traveler stuck in the Dark Ages (900–1000 AD) builds an absurdly comfortable SPA-utopia from junk and basic physics.

## Pillars

- **Tone:** dry British / dark Polish humour. Grounded, no fantasy tropes. Physics masquerading as "magic".
- **Zero Pay-to-Win:** monetization only via QoL (build queues, offline accrual) and minimalist cosmetics.
- **Asynchronous PvE multiplayer:** players compete for influence over NPC settlements; influence breeds Tension; Tension triggers PvE raids.
- **Stack:** Unity (client) + C# .NET modular monolith (REST, EF Core, PostgreSQL).

## Index

### Game Design (GDD)
| # | Document | Contents |
|---|---|---|
| 01 | [Core Loop & State Machine](gdd/01-core-loop.md) | Day / Dusk / Night / Dawn, win/loss rules |
| 02 | [Economy & Resource Sinks](gdd/02-economy.md) | Resources, Hearth 1–5 progression, balance targets, sinks |
| 03 | [Tech Tree](gdd/03-tech-tree.md) | Mud / Comfort / Enlightenment eras, towers, economic buildings |
| 04 | [World Map & Tension](gdd/04-world-map-and-tension.md) | Influence, missionaries, Tension formula, raid triggering |

### Architecture
| Document | Contents |
|---|---|
| [Technical Blueprint](architecture/technical-blueprint.md) | Modules, data models, API, combat verification & anti-cheat |

### ADRs
| # | Decision |
|---|---|
| [0001](adrs/0001-modular-monolith-backend.md) | Modular monolith backend with hexagonal modules |
| [0002](adrs/0002-deterministic-shared-simulation.md) | Deterministic shared combat simulation with server replay |
| [0003](adrs/0003-lazy-evaluated-economy.md) | Lazy-evaluated, server-authoritative economy |
| [0004](adrs/0004-analytical-tension-scheduling.md) | Analytical Tension evaluation and raid scheduling |

### Planning
- [Backlog & Open Questions](backlog.md)
