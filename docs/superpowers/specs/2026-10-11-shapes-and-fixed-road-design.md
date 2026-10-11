# Building Shapes & the Fixed Road — Design

- **Date:** 2026-10-11
- **Status:** Accepted (owner asked for it and left the details to Claude while away: "buildings should not all be 1x1 … fence is always one way … we should NOT be able to modify the path, we can only build around it, or use fence/wooden wall and later a wall … and then install a gate")
- **Delivered in slices:** **1 (this)** — footprints, rotation, the fixed road. **2** — raiders raid buildings off the road, wooden wall and gate. **3 (later, owner: "maybe for later")** — village roads that buildings must connect to; houses and population.

## Intent

The village is the game, so its buildings should feel like buildings: a long peat trench, a squat storage hut, a
two-tile lean-to. The raiders' road is the valley's old track: it never moves, nobody builds on it, and the defence
becomes towers beside it and (slice 2) walls around what you love.

## 1. Footprints and rotation (village rules)

- `BuildingSpec` gains `Width` × `Depth` (default 1 × 1). `Building`, `BuildingView`, `BuildingData` gain `Rotated`
  (swaps width and depth). The building's `Position` is its anchor: the footprint's lowest x and y. `BuildingView.Tiles`
  lists every tile it covers; `BaseSnapshot.BuildingAt(tile)` finds a building by any of them.
- Sizes (`village-2026.10.3`): Woodcutter's Lean-to 2×1, Peat Cut 2×4 (a trench), Storage Hut 2×2, Cultist Hut 3×2,
  Cistern 2×2. Hearth, towers, Wattle Fence, Stone, Tree stay 1×1. A fence's `Rotated` turns it to run north–south.
- `PlaceBuilding(id, type, anchor, Rotated = false)`; every footprint tile must pass every placement rule.

## 2. The fixed road

- `VillageMap.Road`: the raiders' road on the bare map (all rocks and boulders standing, all land open). It never
  changes: clearing a boulder or building anything leaves it where it is.
- Placement on any road tile is rejected: "That's the raiders' road. Build beside it." The old "would seal the road"
  rule is gone (nothing can seal a road nobody may build on).
- `BattleLayout` takes an optional fixed path; `ResolveEnemyPath` returns it (checked: in bounds, spawn → Hearth, no
  blocked tile). Layouts without one (the demo, the probe) behave exactly as before — hash `1ddcdf5947cfcaba` unchanged.
- Raiders smash a structure when their road tile is next to **any** tile of its footprint.

## 3. Old saves

- `BaseData.LayoutVersion` (missing = 0; this slice writes 1). A version-0 save is migrated on load: each building, in
  id order, keeps its anchor if its new footprint fits, else tries rotating, else the nearest anchor (both
  orientations, up to 4 tiles away) that fits; if nothing fits it is removed and all its level costs are refunded.
  Buildings standing on the road are moved the same way.

## 4. Client

- The board draws each building centred on its footprint on a dirt plot the size of the footprint; the model turns
  with `Rotated` (fences finally face both ways).
- Placement shows the whole footprint as the cursor (valid/invalid) and a **Rotate** button next to the drawer while a
  card is selected. Cards show the size ("2×4").
- The raiders' road is drawn in the Day as a trodden track, so it's obvious where you can't build.

## Verification

- .NET: footprint occupancy and rotation, overlap rejection, road rejection, road fixed after clearing/building,
  structures smashed from any tile, migration (fits / rotates / moves / refunds), save round trip of `Rotated`.
- Unity: presenter size labels, DTO round trip, PlayMode place-rotated, device run (60 fps, probe MATCH).

## Slice 2 — Raids off the road, walls and gates

- **Raiding:** a raider on the road that sees an intact structure it hasn't hit within **3 tiles** (`RaidRange`, combat
  balance `2026.10.5`) leaves the road on a walking route around everything in the way (rocks, buildings, locked
  forest), swings once at it, and walks the same route back to the road before marching on. Structures right beside the
  road are still smashed from the road.
- **Barriers:** Wattle Fence (50 HP), **Wooden Wall** (new: 30 Timber, 250 HP, instant, unlimited, model `palisade`) and
  **Gate** (new: 40 Timber + 10 Scrap, 200 HP, instant). If every route to the building is walled off, the raider hacks at
  the first barrier on the way (each raider hits each barrier once); a wrecked barrier is a gap anyone can walk through.
- **Gates:** raiders treat a gate as wall; the Prophet walks through it. (Villagers using gates and roads: slice 3.)
- Raid state (target, route, step, returning) is hashed only for layouts with structures, so the demo hash stays.
