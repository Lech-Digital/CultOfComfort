# Cult of Comfort

Mobile F2P base-building / tower-defense. A cynical time traveller stuck in 900–1000 AD builds an absurdly comfortable spa-utopia out of junk and basic physics. See [docs/](docs/README.md) for the GDD, architecture and ADRs.

## Repository layout

| Path | What |
|---|---|
| `src/CultOfComfort.Simulation` | Deterministic combat simulation (netstandard2.1). Shared by the Unity client and the server — see [ADR 0002](docs/adrs/0002-deterministic-shared-simulation.md) |
| `tests/CultOfComfort.Simulation.Tests` | xUnit + FluentAssertions tests, including determinism and replay-verification tests |
| `tools/CultOfComfort.Sandbox` | Console runner: plays a Night, verifies it like the server would, writes an HTML replay viewer |
| `client/CultOfComfort.Unity` | Unity 6.3 LTS client (URP, Input System). Code-first prototype: Dusk (build) → Night (live battle) → Dawn (verified result) |
| `docs/` | GDD, technical blueprint, ADRs, backlog |

## Quick start

```bash
dotnet test                                              # run the test suite
dotnet run --project tools/CultOfComfort.Sandbox -- --open   # play the demo Night and open the replay
```

Sandbox options: `--seed N` (default 947), `--budget N` (raid strength, default 54), `--waves N` (default 4), `--no-sluice` (auto-player never intervenes), `--out path`, `--open`.

The viewer supports deep links: append `#t=<tick>` to open the replay paused at that tick (20 ticks = 1 second).

## Unity client

### One-time setup (no admin rights needed)

Everything installs into the home folder, same approach as the MAUI toolchain in PurePrep:

| What | Where |
|---|---|
| Unity Hub 3.22 | `~/Applications/Unity Hub.app` |
| Unity 6.3 LTS (`6000.3.26f1`) + Android support (bundled SDK/NDK/OpenJDK) | `~/Unity/Hub/Editor/6000.3.26f1` |

Reinstall from the command line if needed:

```bash
HUB=~/Applications/"Unity Hub.app"/Contents/MacOS/"Unity Hub"
"$HUB" -- --headless install-path --set ~/Unity/Hub/Editor
"$HUB" -- --headless install --version 6000.3.26f1 --architecture arm64 \
  --module android android-sdk-ndk-tools android-open-jdk --childModules
```

Then open Unity Hub once, sign in with a Unity ID and activate a free **Personal** licence (Hub → Preferences → Licenses).

### Running the prototype

```bash
dotnet build src/CultOfComfort.Simulation   # also copies the DLL into client/…/Assets/Plugins (gitignored)
```

Open `client/CultOfComfort.Unity` in Unity Hub (Add → Add project from disk), open `Assets/Scenes/SampleScene`, press **Play**. The scene is built entirely from code by `GameBootstrap` — there is nothing to wire up in the editor.

- **Dusk:** pick a tower, tap empty ground to build, tap a tower to demolish. The road reroutes live.
- **Night:** tap the ground to move the Prophet; *Open Sluice* and *Phantom Voice* are buttons.
- **Dawn:** result plus a server-style replay of your input log, with matching hashes.

### Rules for Unity code

Unity code only *reads* simulation state (`Battle.Enemies`, `Battle.PositionOf`, `Battle.LastTickEvents`) and calls `Battle.Step()` at a fixed 20 Hz; it never feeds floats, physics or `Time.deltaTime` back into the sim. Player actions go through `Battle.Intervene(...)` so they land in the input log.
