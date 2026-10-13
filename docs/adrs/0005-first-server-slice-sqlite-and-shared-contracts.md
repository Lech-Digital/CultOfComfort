# 0005 — First Server Slice: SQLite on the Shared Host, One Village Document, Guest Tokens

- **Date:** 2026-10-13
- **Status:** Accepted

## Context

The village runs on the device today (`LocalBaseGateway`). The owner asked for the server to be built and deployed the
way PurePrep's is: one container on the shared Hetzner host behind Coldstart's Caddy, SQLite on a Docker volume,
deployed by rsync + `docker compose up --build`. ADR 0001 plans a modular monolith on PostgreSQL with JWT device
auth; that is more than the first slice (Identity + Base, one player per village, no cross-player reads) needs.

The client and server must agree on the village format exactly, and the `IBaseGateway` contract already says a
remote gateway keeps a local `BaseState` mirror fed by server snapshots.

## Decision

- **Hexagonal projects** per ADR 0001: the Simulation is the domain; `CultOfComfort.Server.Application` holds the
  use cases (`IVillageService`) and outbound ports (`IPlayerRepository`, `IVillageRepository`);
  `CultOfComfort.Server.Infrastructure` holds the EF Core SQLite adapters; `CultOfComfort.Server` is the composition
  root and the minimal-API endpoints.
- **SQLite** (EF Core, `/data/cultofcomfort.db` on a volume) instead of PostgreSQL for this slice. A village is one
  row: the village document plus its version. Writes are a single conditional `UPDATE … WHERE Version = expected`,
  which gives the optimistic concurrency the blueprint asks for (409 → the client re-pulls).
- **One document format** for the device save and the wire: `CultOfComfort.Contracts` (netstandard2.1, copied into
  Unity's Plugins like the Simulation) holds the `[Serializable]` DTOs and the mapping to `BaseData` and the commands.
  Unity writes it with `JsonUtility`, the server with `System.Text.Json` (`IncludeFields`) — the same JSON.
- **Guest accounts with opaque bearer tokens** (`POST /api/auth/guest`): 32 random bytes, only the SHA-256 hash is
  stored. No JWT signing key to manage yet; account linking (Google/Apple) replaces this before any real players.
- **Commands, not state** (`POST /api/base/sync`): the server applies intents at its own clock, replays Nights from
  their input logs, answers retries as `Duplicate`, and refuses development commands unless configured.
- Hostname `api.cultofcomfort.lechdigital.nl`, container `cultofcomfort-api` (unique name on the shared network).

## Consequences

### Positive
- Deployed with the same tooling and on the same box as PurePrep/CoreChoice/Coo: no new infrastructure or cost.
- The device save and the server cannot drift apart: one DTO set, one mapping, tested from both sides.
- Moving to PostgreSQL later changes only the Infrastructure adapters (the ports stay).

### Negative
- SQLite allows one writer at a time; fine for a test audience, not for launch load (PostgreSQL per ADR 0001 then).
- A guest who loses their token loses their village until account linking exists.
- The whole village is rewritten on every sync (a few KB); acceptable now, revisit with per-module tables.
- The Unity client still plays locally; `RemoteBaseGateway` (mirror + sync queue) is the next step.
