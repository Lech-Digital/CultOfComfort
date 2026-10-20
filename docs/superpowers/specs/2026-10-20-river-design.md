# The river, bridges, a wider world, and packing up the towers

- **Date:** 2026-10-20
- **Status:** Asked for by the owner ("the south and east roads should actually start on the other sides of our river! we should have access to rivers later, so we can defend bridges maybe? … more cool mountains, and the river should be wider and more interesting. other land features too. I think 4 towers is way to little. also, pick all the towers is a good feature").

## The map grows to 68×48

Every coordinate of an existing village stays where it was. The new land is to the south and east.

| Land | Opens |
|---|---|
| The near bank (rows 32–35, columns 48–52) | Hearth 4 |
| The river and the far bank beyond it | never (`VillageMap.Wild`) |

- The rock wall at column 11 now runs down to the river.
- Crags stand on the near bank.

## The river

- **Shape.** It is a smooth function (`VillageMap.River`: distance from the water's edge, negative in the water). It runs three to four tiles wide, winding east along rows 36–42, round a bend and north up columns 53–59.
- **In the simulation.** Its tiles are `Water`. The road tiles over it are bridges (`IsBridge`).
- **On the board.** It is painted into the ground:
  - deep water in the middle, with streaks of current;
  - foam along the edge;
  - a dark wet bank, then sand and pebbles.
- **Past the map.** It runs on as water tiles, in from the west and off to the north.
- **On it.** Plank bridges carry the roads over it. Reeds grow on the banks, and rocks stand in the current.

## The roads from across the river

| Road | From | Bridge |
|---|---|---|
| South road | the far bank's south edge (28, 47) | north over a bridge at column 28 |
| East road | the east edge (67, 17) | west over a bridge at row 17 |

- Both then run as before.
- At Hearth 4 the near bank opens, so towers and gates can hold the bridgeheads.

## The wider world

- **Mountains.** Ranges and tall snowy peaks in two staggered rows north and west, well back from the land, plus a few lone peaks beyond the river.
- **Forest.** Unopened land is a painted forest floor, not a slab, so a road through the forest stays visible. Trees keep clear of roads, the river and its banks.
- **The far bank.** A ring of standing stones in a clearing on the east bank, and a lone stone on the south bank.

## Towers

- **Cap.** 8 / 14 / 20 / 26 at Hearth 1 / 2 / 3 / 4 (was 4 / 7 / 10 / 14).
- **Packing up.** "Pack up" on a tower's card, or "Pack up all". A packed-up tower:
  - keeps its level (and its damage, if wrecked);
  - is set down again for free from the build drawer ("free · 3 packed");
  - counts towards the cap and doesn't fight while packed.

## Performance

The ground is painted at 32 pixels a tile over 68×48 tiles.
- **Smooth parts at lower resolution.** The noise and the river are worked out every 4 pixels and blended. The road and forest painters read noise fields worked out once.
- **Off the main thread.** All the painting runs in the background, and the newest request wins.
- **Timings (editor):** ground 1.1 s, forest floor 0.8 s, roads 0.26 s. Captures wait for `BoardView.GroundReady`.
