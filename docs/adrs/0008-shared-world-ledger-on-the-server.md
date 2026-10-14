# 0008 — The Shared World: a Generated Map and One Influence Ledger per World on the Server

- **Date:** 2026-10-14
- **Status:** Accepted

## Context

The Marchlands (spec 2026-10-14 world map) are single-player: each village keeps its own influence over 16 fixed places. The owner wants a shared world, with about 500 players per world and villages placed at random rather than at the edge (spec 2026-10-14 shared world). In a shared world, influence at a place is split between rival cults, so it can no longer live inside one village's save.

What we have to work with:
- Villages are documents with optimistic versions (ADR 0005). Clients send commands, never amounts. The server applies them at its own clock.
- The rules are a shared, deterministic library that the phone and the server both run (ADR 0002).
- Tension is lazy and linear between events (ADR 0004).
- There is one server, with SQLite behind it.

## Decision

1. **The map is generated, not stored.** `WorldGenerator(seed)` is in the shared Simulation library, so the phone and the server build the same 6,487-hex world from a seed. Only what changes gets stored and sent.

2. **One `WorldLedger` per world.** It is pure and lives in `Simulation.World`.
   - It holds the cults, each place's holdings (thousandths of 100; the Authority's loyalty is the rest), the sermons under way, and a lazy offerings purse per cult.
   - New influence comes from loyalty first. Any remainder displaces the other cults in proportion to their shares.
   - The Patron is the top holder at 40 or more. Holdings fade by 2% a day.
   - The server, the bot cults and the test harness all run this one class.

3. **The server stores each world as one JSON document with a version** (table `worlds`).
   - `WorldService` keeps the loaded ledgers in memory behind one lock and stores the document after every change.
   - A second writer is detected by the version check, and the cache is then dropped.

4. **Villages and the world talk only through commands.**
   - The village still pays for each missionary and misses the villager while away (`SendWorldMissionary`).
   - The server checks the place and the distance against the world before it applies the command. Once the village is stored, it records the sermon in the ledger.
   - The world answers through three server-only commands that have no wire form, so no client can send them:
     - `JoinWorld` (where the village stands);
     - `WorldUpdate` (the influence-driven Tension rate, plus a one-off spike);
     - `ReceiveOfferings` (what fits in the stores).
   - The village's Tension stays lazy. The rate changes only when the world says so.

5. **A hosted tick every 30 seconds** does the following:
   - lands the sermons that ended and applies decay;
   - lets the bot cults act;
   - pushes changed Tension rates to the villages, retrying if the player's own sync wrote first.

6. **Bot cults** (80 when a world opens) are world-only. They play like a steady player and retire, weakest first, as humans arrive, keeping about 150 cults active. `WorldSimulation` runs a whole world on an accelerated clock as a test and as `Sandbox world`.

7. **Joining is lazy.**
   - A village joins World 1 the first time its map is opened online.
   - When World 1 has no room, World 2 opens with seed + 1.
   - A village that has never joined keeps the Marchlands, offline included.

## Consequences

### Positive

- Contested influence works for many players at once. The only shared state is the ledger, and one lock makes every change atomic.
- The map costs nothing to store or send. A window of the world is a few kilobytes of holdings.
- The rules are tested at full size: the harness runs 14 days of 500 cults in seconds and reports occupancy, contested places, Patrons, Tension and offerings.
- The village save format and the sync protocol are unchanged, apart from optional fields. Old clients keep working.

### Negative

- The world is an in-memory singleton. A second server instance would need a real lock, or one world per instance.
- A village and the world are stored separately, so a crash between the two writes can lose one sermon. The villager still walks home.
- Every `WorldUpdate` bumps the village's version, so the client's next sync gets a 409 and retries once. This is cheap, but it is extra traffic.
- The `SlowerDecay` research applies only in the Marchlands for now. Decay on the world is the same for everyone.
- The harness shows influence-driven Tension averaging about 6 per hour once a world is full. That roughly doubles the passive rate at Hearth 2, so the numbers need a pace review before launch.
