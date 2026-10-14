# The Shared World (Server 1): a Generated Map, Rival Cults, Bot Cults

- **Date:** 2026-10-14
- **Status:** Approved by the owner ("go for it"). The owner's choices: villages are placed randomly, not at the edge; a world holds about 500 players.
- **Builds on:** the single-player Marchlands (spec 2026-10-14 world map), GDD 04, ADR 0005/0006.

## Size

| | Value | Why |
|---|---|---|
| Villages per world | **500** | The owner's number. When a world is full, World 2 opens. |
| World shape | hex disc, radius **46** (6,487 hexes) | Measured: radius 40 packs only ~427 villages at 3-hex spacing; radius 46 packs ~568, so 500 always fit. A full world has about 8 rival cults within 6 hexes of each village. |
| Places | about **35%** of hexes (~2,300): hamlets, villages, market towns, abbeys | Gives 4+ places per village (GDD: ~3). |
| Domains | about **60**, each a region around a seat (Keep or Cathedral) | The GDD's 15–30 places per domain. Authorities alternate Warlord and Bishop. |
| Bot cults | **80** at launch | The map feels alive before real players arrive. Bots retire as humans join, keeping about 150 active cults. |

## Placement

A new village goes on a random free hex, seeded by the player id:
- at least **3 hexes** from any other village and 1 from any place;
- chosen among the hexes whose surroundings hold the fewest villages, so it's random but fair: nobody spawns into a crowd, and nobody lands at the edge on purpose.

Existing villages join World 1 the same way.

## Who holds what

- **The map is generated, not stored.** `WorldGenerator(seed)` lives in the shared Simulation, so the server and the phone build the same map from the world's seed. The database keeps only what changes: villages, influence, missionaries, purses.
- **Influence is shared.** Each place holds 100 influence in total, split between its holders: cults and the Authority's loyalty. New influence displaces the existing holders proportionally (GDD 04: competitive but non-destructive). Patron is the top holder with ≥ 40.
- **Missionaries:** the village still pays the provisions and loses the villager while away (`SendMissionary`, as now). The server's **world service** owns the sermon:
  - It records the missionary when it applies the village's command.
  - A world tick grants the influence at the sermon's end, displacing others.
  - The villager comes home on the village's own clock, as now.
- **Offerings** accrue per cult, lazily, at the rate of the cult's current shares. They're settled before any share changes, and the cult collects them from the map.
- **Tension:** the world computes each cult's influence-driven Tension rate and hands it to the village when it changes, through an internal command the server applies and clients can't send. The village's Tension stays lazy and linear between changes (ADR 0004). Tithes stay in the village.
- **Decay:** 2% a day, applied by the world tick. Inactive cults fade off the map.
- **Bot cults** are world-only actors with no village. On a schedule they send missionaries to nearby places, the way a steady player would. They share the rules, so they're also the **test harness**: an in-process run simulates days of a full world with an accelerated clock and reports occupancy, contested places, Patrons and Tension.

## The map screen

The map shows a window of the world around your village. You can pan and zoom it, and it is drawn from the seed. Each place shows your share, the top holder's colour, and Patron banners. A place's card lists its holders: you, rival cults (named), and the Authority. Everything else works as in the Marchlands: preach, collect, tithe.

## Slices

1. `WorldGenerator`, shared and deterministic, with tests (counts, spacing, domains, same seed means the same world).
2. The server world module: schema, joining (placement), `/api/world` (the seed, your village, holders in view), missionaries hooked from village commands, the world tick (grants with displacement, decay, Tension rates), offerings.
3. Bot cults and the world-simulation harness (a test plus a report).
4. Client: the world map screen.
5. Deploy, and move existing villages into World 1.
