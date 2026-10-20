# Roads, four facings and a paved raiders' road

- **Date:** 2026-10-20
- **Status:** Asked for by the owner ("the roads are not looking great, and you cannot rotate the buildings properly: there are only two ways to place them, instead of 4. also, you cannot rotate the defensive buildings. also, we should be able to upgrade the look of the raiders road, maybe … cobbled").

## Four facings

A building used to store one flag, `Rotated` (a quarter turn: width and depth swap). Turning it again only undid the quarter turn, so every building had two looks. Towers ignored even that: the model always faced the camera.

- The save and the commands now also carry `Flipped` (a half turn more). The facing is `Rotated + 2 × Flipped` quarter turns, 0–3.
- The footprint still depends on `Rotated` only. A half turn covers the same tiles, so nothing about fitting, roads or Nights changes.
- ↻ turns a quarter each time: 0 → 1 → 2 → 3 → 0.
- Towers turn like everything else, in the village and in the ghost.
- An old save has `Flipped = false` and looks as it did.

## Roads

The ground is painted at 32 pixels a tile (was 16). Each road is a band joined to its neighbours, as before, but now:

- its edges wander, and the road is a little wider and narrower along its length;
- bends and ends are rounded;
- a worn verge (darker mud, the odd pebble) runs along the village's roads;
- dirt has cart ruts **only on straight stretches**, fading out before a bend or crossing (crossed ruts drew a grid of lines);
- each plank is its own shade, with nails; cobbles are rounded stones, lit from the top left, with kerb stones along the sides.

## Paving the raiders' road

Tap the raiders' road to see its card. It can be paved twice:

| | Opens | Cost | Comfort |
|---|---|---|---|
| Gravel | Hearth 2 | 300 Timber · 150 Scrap | +3 |
| Cobbles | Hearth 3 | 900 Timber · 500 Scrap · 100 Devotion | +8 (in all) |

It's for looks and a little Comfort; **the Night is unchanged** (a test checks the plan is identical). Not during a Night. Saved as `RaidersRoad` (0: trodden mud).
