# Build Flow, Move, Repair All, Joined Walls, Road Gates, Wall-Breakers & Barks — Design

- **Date:** 2026-10-12
- **Status:** Approved by the owner (2026-10-12: "yes … better gates with levels … the rest sounds really good … add dialogues")
- **Owner's words:** "we need repair all, we need move object without destroying it … when we click to build something,
  then we need to unclick it"; "when we build a fence or a wall, we need a corner piece"; "we should be able to build a
  gate on top of the road, it will just stop the raid until the gate falls"; "the raid can also randomly attack walls on
  the way"; "things that invaders shout, things that our hero says, and his followers"
- **Next build (not this one):** a raider roster that grows with the Hearth (wild animals, curious peasants → lords,
  priests…) and mixes per level.

## 1. Build preview (client)

- Picking a card no longer arms a tap-to-build. It places a **ghost** of the building on the nearest free spot near the
  middle of the view. Drag the ghost to move it; it is green where it fits, red with the reason where it doesn't (the
  village's own rule, `VillageMap.PlacementProblem`).
- Floating next to the ghost: **✓** (build: sends `PlaceBuilding`), **✗** (cancel), **↻** (rotate, when it can turn).
  Tapping empty ground or the same card again cancels. Nothing is built by a stray tap.
- **Painted things** — roads, fences, walls — keep the tool on: drag to paint a stroke (sent as one command), and a
  **Done** button ends the tool. Fences and walls get `PlaceBarrierLine` like roads (`PlaceRoad` generalised to
  `PaintTiles(type, tiles)` for any 1×1 paintable type).

## 2. Move (village rule)

- `MoveBuilding(id, anchor, rotated)`: free, instant, keeps level, stored output, construction in progress and damage.
  Same placement rules as building (ignoring the building itself). Not the Hearth, not roads (repaint instead), not
  during a Night. The client uses the same ghost with ✓/✗/↻, opened from the card's **Move** button.

## 3. Repair all (village rule)

- `RepairAll()`: repairs every wrecked building for the sum of their repair costs, or nothing (rejected with what is
  missing). A **Repair all (N) · cost** button appears above the build cards when anything is wrecked.

## 4. Self-joining fences and walls (client)

- Each fence or wall tile picks its look from its fence/wall/gate neighbours (N, E, S, W): post, end, straight, corner,
  T or cross — built from the existing models as segments from the tile centre towards each joined neighbour, plus a post.
  Rotation no longer matters for them (Rotate is hidden for fences and walls; gates still turn).

## 5. Gates on the raiders' road, with levels

- A **Gate** may stand on the raiders' road (not the spawn, the entrance clearance or the Hearth). Road gates allowed:
  **1 / 2 / 3** at Hearth 1 / 2 / 3. It doesn't move the road: it blocks it.
- Gate levels: **Wooden Gate** (40 Timber, 10 Scrap, 200 HP) → **Iron-bound Gate** (Hearth 2: 120 Timber, 60 Scrap,
  450 HP) → **Gatehouse** (Hearth 3: 300 Timber, 200 Scrap, 900 HP). Instant upgrades, no builder.
- **Combat:** a raider whose next road tile holds an intact gate stops on the tile before it and swings at it again and
  again (every swing lands; the once-per-structure rule is for raids off the road) until it falls; then the road is open.
  The gate's tile is still the road; it is not an obstacle for the road check.
- Models: `gate_wood`, `gate_iron`, `gatehouse`.

## 6. Wall-breakers

- When a raider spawns, a roll from the seed and its id makes it a **wall-breaker** (Levy 30 %, Man-at-arms 50 %,
  Zealot 0 %). Only wall-breakers leave the road to hack at fences, walls and off-road gates within raid range; everyone
  still hacks through a barrier that blocks the way to a building they want. Deterministic: the village's replay agrees.
- Combat balance `2026.10.6`. Layouts without structures are untouched (demo hash `1ddcdf5947cfcaba`).

## 7. Barks (client)

- Short speech bubbles over the speaker, from a `BarkBook` (pure, deterministic pick per event): raiders shout when they
  arrive, hack at a gate or wall, get washed back, or reach the Hearth; the Prophet speaks on his skills, when he lures
  and when he swoons; cultists comment in the Day (a building finished, a raid coming, the Peat running out).
- At most three bubbles at once; each lasts ~2.5 s; one speaker never repeats within 8 s. Lines are funny, never cruel
  (style guide), and live in one file for easy writing.

## Verification

- .NET: move (keeps state, rules, ignores itself, not during a Night), repair all (all or nothing), paint fences/walls,
  road gates (placement, limits by Hearth, levels, block and fall in combat, repeated swings, road stays the same), wall-
  breaker rolls (deterministic, only breakers raid barriers), hashes.
- Unity: ghost flow (pick → ghost → ✓ builds, ✗ cancels, tap ground cancels), move flow, repair-all button, joint shapes
  from neighbours (pure function), BarkBook picks; device run on the Pixel.
