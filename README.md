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

- **Camera:** drag with one finger to pan, pinch to zoom (editor: mouse drag + scroll wheel). *Recenter* resets the view.
- **Orientation:** portrait and landscape both work and follow the phone's auto-rotate. In portrait the board turns 90° so the road runs top (spawn) to bottom (Hearth); the camera always fits the board between the HUD panels.
- **HUD:** UI Toolkit, built in code (`Assets/Scripts/Hud/HudView.cs`) and styled by `Assets/Resources/UI/Hud.uss`; a `portrait` / `landscape` class on the root restacks the bars.
- **Dusk:** pick a tower, tap empty ground to build, tap a tower to demolish. The road reroutes live.
- **Night:** tap the ground to move the Prophet. Enemies that reach him stop to fight (towers love that), Zealots pelt him from range, and at 0 HP he swoons for 10 s. *Open Sluice* and *Phantom Voice* fire at once; *Hot Stones* arms a targeted throw: tap it, then a tile inside the pebble ring.
- **Dawn:** result plus a server-style replay of your input log, with matching hashes.

### Android build (device via adb)

```bash
UNITY=~/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath client/CultOfComfort.Unity -buildTarget Android \
  -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroid -logFile -
~/android-sdk/platform-tools/adb install -r client/CultOfComfort.Unity/Builds/Android/CultOfComfort.apk
~/android-sdk/platform-tools/adb shell monkey -p com.lechdigital.cultofcomfort 1
~/android-sdk/platform-tools/adb logcat -s Unity | grep "\[CoC\]"   # determinism probe result
```

IL2CPP + ARM64 (required by 64-bit-only devices such as the Pixel 10), development build, debug-signed. Also available in the editor menu: **Cult of Comfort → Build Android APK**.

On startup the client runs `DeterminismProbe`: it replays the Sandbox demo Night and compares the state hash with the .NET value. The Dusk HUD shows `Deterministic: OK` when the device produces the bit-identical result.

### Rules for Unity code

Unity code only *reads* simulation state (`Battle.Enemies`, `Battle.PositionOf`, `Battle.LastTickEvents`) and calls `Battle.Step()` at a fixed 20 Hz; it never feeds floats, physics or `Time.deltaTime` back into the sim. Player actions go through `Battle.Intervene(...)` so they land in the input log.

## Art pipeline

Models are Python scripts run by headless Blender (`art/blender/models/*.py`), not hand-made files.
Design rules live in `docs/superpowers/specs/2026-10-10-art-direction-and-pipeline-design.md`.

Install Blender 4.5 LTS without sudo (same pattern as Unity):

    curl -fLO https://download.blender.org/release/Blender4.5/blender-4.5.14-macos-arm64.dmg
    hdiutil attach -nobrowse -mountpoint ./mnt blender-4.5.14-macos-arm64.dmg
    cp -R ./mnt/Blender.app ~/Applications/ && hdiutil detach ./mnt

Then:

    ./art/build.sh --test        # pipeline tests (inside Blender)
    ./art/build.sh prophet       # build one asset: FBX → Assets/Resources/Art/Models, previews → art/previews
    ./art/build.sh --all         # rebuild everything

Override the Blender location with `BLENDER=/path/to/Blender`.

Review tools (Unity batch mode, `-executeMethod`):

- `CultOfComfort.Editor.AnimationShowcase.RenderProphet` / `RenderCast` → frames in `art/previews/anim/`, then `python3 art/make_gifs.py`.
- `CultOfComfort.Editor.BoardScreenshot.Render` / `RenderVillage` → `art/previews/board_*.png`, `village_*.png`.
- `python3 art/make_gallery.py` rebuilds the gallery at `art/previews/README.md`.
