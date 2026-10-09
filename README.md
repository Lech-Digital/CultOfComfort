# Cult of Comfort

Mobile F2P base-building / tower-defense. A cynical time traveller stuck in 900–1000 AD builds an absurdly comfortable spa-utopia out of junk and basic physics. See [docs/](docs/README.md) for the GDD, architecture and ADRs.

## Repository layout

| Path | What |
|---|---|
| `src/CultOfComfort.Simulation` | Deterministic combat simulation (netstandard2.1). Shared by the Unity client and the server — see [ADR 0002](docs/adrs/0002-deterministic-shared-simulation.md) |
| `tests/CultOfComfort.Simulation.Tests` | xUnit + FluentAssertions tests, including determinism and replay-verification tests |
| `tools/CultOfComfort.Sandbox` | Console runner: plays a Night, verifies it like the server would, writes an HTML replay viewer |
| `docs/` | GDD, technical blueprint, ADRs, backlog |

## Quick start

```bash
dotnet test                                              # run the test suite
dotnet run --project tools/CultOfComfort.Sandbox -- --open   # play the demo Night and open the replay
```

Sandbox options: `--seed N` (default 947), `--budget N` (raid strength, default 54), `--waves N` (default 4), `--no-sluice` (auto-player never intervenes), `--out path`, `--open`.

The viewer supports deep links: append `#t=<tick>` to open the replay paused at that tick (20 ticks = 1 second).

## Using the simulation in Unity

Build `src/CultOfComfort.Simulation` and copy `CultOfComfort.Simulation.dll` into `Assets/Plugins/`. Unity code should only *read* simulation state (`Battle.Enemies`, `Battle.PositionOf`, `Battle.LastTickEvents`) and call `Battle.Step()` at a fixed 20 Hz; never feed Unity floats, physics or `Time.deltaTime` back into the sim.
