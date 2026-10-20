# Across the river: bridge gates, each road's raiders, Hearth 5 and the Comfy Altar

- **Date:** 2026-10-20
- **Status:** Approved by the owner ("good ideas, build them").

## Hold the bridges (Hearth 4)

- From Hearth 4 a **gate** may stand on a bridge tile (`VillageMap.CanStand`, `BridgeGateHearth = 4`). Nothing else may; the river and the far bank stay closed.
- A gate on a bridge counts as a road gate, like one on the road.
- Raiders hack at it on the narrow crossing, under fire from towers on the near bank.
- Before Hearth 4: "A gate can hold the bridge from Hearth 4."
- In the Night layout, raiders' roads through land that isn't open are no longer counted as locked: a bridge gate stood on a tile that was both.

## Each road its own raiders

| Road | Hearth 2 | Hearth 3 | Hearth 4+ |
|---|---|---|---|
| North | Wulfric's levy (as before) | the lords and the church, Wulfric leading | (same) |
| South | **Odo's flock**: zealots, pilgrims, a priest or two | **The Bishop's pilgrimage**: zealots, priests, the Bishop leading | (same) |
| East | | **Wild beasts**: boars, goats, geese, poachers | **The hunt**: beasts and the lord's huntsmen |

- `RaidRoster.ForRoad(hearth, road)`. Road 0 is exactly `ForHearth`, so old Nights replay.
- The Tension strip says who and where: "Next: Odo's flock, south road".

## Hearth 5: across the river

- **Upgrade:** 8,000 Timber · 1,500 Peat · 1,500 Scrap · 800 Devotion, 6 hours.
- **Stores** 25,000; **Tension** 14/h, raid power 110; missionary reach 7; **tower cap** 32.
- **New land:** the far bank by the stone circle (columns 60–67, rows 18–35). It is reached over the east road's bridge, which carries the road network; a track does the rest.
- **The Comfy Altar** (Hearth 5, one only, 2×2):
  - it stands in the middle of the stone circle and nowhere else;
  - +40 Devotion an hour and Comfort +30, with a crew of two;
  - the model is a stone slab with a red cushion, candles, a folded towel and a rubber duck.
- **Errands:** a Guardhouse, Hearth 5, the Altar (32 in all).
- The Hearth keeps its level 4 model at level 5 for now.
