# Village Roads, Villagers & the Stone Wall — Design

- **Date:** 2026-10-11
- **Status:** Approved by the owner (2026-10-11: "1,2,3"; roads boost output; workers + Peat upkeep)
- **Implements:** owner's requests "to use buildings inside our village, we need to connect them using different types of
  roads, so someone will not build all of them far away from the invasion road", "we should be controlling our
  population by building houses", "later a wall"; GDD `02-economy.md` (Peat heating upkeep)
- **Delivered as three slices:** **A** roads, **B** villagers, **C** stone wall

## A. Village roads

- New 1×1 building types **Dirt Track** (2 Timber, Hearth 1), **Plank Walk** (6 Timber, Hearth 2), **Cobbled Road**
  (4 Scrap, Hearth 3): instant, unlimited, no builder, not smashable, not in the raiders' way, nothing built on top.
- **Painting:** `PlaceRoad(id, type, tiles)` places every valid tile in one command (invalid tiles skipped, cost per tile
  placed; rejected only if none fit). Painting a better road over a worse one replaces it (no refund); over an equal or
  better road it is skipped.
- **Network:** the Hearth, every village road tile, every gate tile and every tile of the raiders' road (the old
  track counts as a dirt road) joined orthogonally from the Hearth. A building is **connected** when a tile orthogonally
  beside its footprint is on the network (or is the Hearth). Walls cut roads; gates carry them through.
- An unconnected building produces nothing and gives no storage, sluice or bed bonus; its card says "Not connected — build
  a road to it".
- **Road bonus:** a connected producer earns +10 % beside a Plank Walk, +20 % beside a Cobbled Road (best road it touches;
  dirt and the raiders' road +0 %).
- **Old saves** (layout version 1): migrated to version 2 by laying the shortest Dirt Track (free) from each unconnected
  building to the network, through open buildable tiles; if none exists the building simply stays unconnected.
- Every change to roads, buildings, workers or warmth first settles all production up to that moment (lazy economy stays exact).

## B. Villagers

- The Hearth gives **2 beds**. New **Cottage** (2×2, 60 Timber, 1 min; L2 150 Timber + 20 Scrap, 8 min): 4 / 6 beds,
  max 3 / 5 / 8 per Hearth level. Beds count only in connected, finished, intact cottages.
- **Moving in:** one villager every **10 minutes** while there is a free bed (lazy, handled in time order with the other
  events, so it happens while away). Losing beds sends the extra villagers away at once. A new village starts with 2.
- **Work:** producers need workers — Lean-to 1, Peat Cut 2, Cultist Hut 1 (Storage Hut, Cistern, Cottage 0). Villagers
  staff connected, finished, intact producers in building order; a producer that can't get its full crew gets none and
  says "Needs 2 villagers".
- **Peat upkeep:** each villager burns **1 Peat per hour** from storage. When it runs out, everyone downs tools (all
  production stops) until Peat is back in storage; nobody leaves. Card/pill: "Cold — no Peat".
- Old saves (layout version < 3) start with the Hearth's beds filled.
- Interface: a people pill "4/6" in the frame; building cards show their crew; the Hearth card shows beds.

## C. Stone wall

- The Wooden Wall gets a second level, **Stone Wall**: 10 Timber + 20 Scrap, instant, 600 HP, needs **Hearth 2**
  (`BuildingLevel.MinHearth`). Upgrade a wall in place from its card. New model `stone_wall`.

## Verification

- .NET: network (Hearth, roads, raiders' road, gates, walls cut), connection gates production/storage/beds, road bonus,
  painting (partial, replace, skip), arrivals over time and while away, bed loss, staffing order, upkeep and running cold,
  warming up after Collect, migrations (roads laid, villagers granted), stone wall level gated by Hearth 2.
- Unity: presenter labels (not connected, crew, cold, people pill), DTO round trip, PlayMode paint a road, device run.
