# The Hearth: the Landing, 4×4

- **Date:** 2026-10-14
- **Status:** Approved by the owner ("yes, I love this idea! go for it")
- **Owner's ask:** "from level one, it should be way bigger, maybe even 4x4 or 4x5, and there should be a piece of roman ruins (as we have), but also the half-dissasembled car that prophet came in."

## Ground

- The Hearth covers **4×4 tiles**: x 16–19, y 8–11 on the demo valley. A 4×5 plot would have run into the land the raiders' road and the first buildings use.
- The fire stays on its tile, (17, 9), one in from the north-west corner. `VillageMap.HearthSize` (4 on the demo map, 1 by default) gives `HearthTiles`, `IsHearthTile` and `DistanceFromHearth`.
- **Raiders march to the fire as before.** The raiders' road is computed on the bare valley and is unchanged. Its last two tiles, (17, 8) and (17, 9), run across the ruin's floor through a gap in its back wall. The ruin isn't an obstacle in a Night.
- **Nobody builds on the ruin.** It is the Hearth's own ground, so placement, moves and paths all refuse it.
- **The ruin carries the road network.** All 16 tiles seed it, so a building touching the ruin's edge is connected without a track.
- Wild trees are kept off the ruin and the ring of tiles around it.

## Old villages (layout version 5)

A save from before the change (layout 1–4) clears the ruin's ground once, on load:

- Every building standing on one of the 16 tiles moves to the nearest spot that fits, up to 4 tiles away, turning if that helps. This is the same rule as the shapes migration of layout 1.
- With no room nearby, the building comes down and everything paid for it is refunded.
- Tracks on the ruin simply go, because the ruin's floor carries the network now.

## What each level looks like

The model is authored at 1 unit = 1 tile, centred on the plot, with the fire in the middle. The three levels share `art/blender/lib/hearth.py`.

1. **The Landing.**
   - A sunken Roman bath: a cracked mosaic ring, broken columns (two still carry a scrap of architrave), stumps of the back and left walls, and a toppled marble head in front.
   - The Prophet's teal car sits nose-down in the dry plunge pool, bonnet up and boot open. Its right-hand door is torn off and stands by the fire as a windbreak.
   - The fire burns in a hubcap. The two front seats are pulled out on either side of it, and the blue tarp is strung over the camp from a pole to the car's aerial.
   - The cool box and the warning cone from the boot lie about. The radiator steams.
2. **Camp Comfort.**
   - The wheels are off: one tyre rings the fire, and the other stands up at the front as a sign with a woad board.
   - The engine block sits on a column stump as the altar, with candles.
   - A wing mirror on a tall pole watches the road (the Eye), and wattle patches the left wall.
   - A log banya is bolted onto the back-right corner.
   - The first copies of the car's parts appear: woad-blue hubcaps on the banya and a clay steering wheel on a post.
3. **The Temple of Comfort.**
   - The car is raised, level, on timber stilts with a ladder: the Chariot. Its headlights burn as lamps, and its hoses run into the old pool, now a steaming hot pool.
   - The banya gains a daubed and thatched upper floor, and a water wheel turns beside it.
   - Woad and madder banners hang from the columns. The mosaic is restored in woad and madder around the cult's sign, a sun in a bath.

## Pace

- Nothing in the economy changed.
- The tutorial bot reaches Hearth 2 at 24 minutes, against 37 before. The last stretch of the road now crosses the ruin, so the spots right beside the fire, where raiders used to smash buildings, are gone. Night 1 went from 2★ with 6 wrecks to 3★ with 4 wrecks: more salvage and fewer repairs.
- The builder bot went from 167 to 159 minutes.
- Both stay inside the targets. They are noted here because the owner prefers slower.
