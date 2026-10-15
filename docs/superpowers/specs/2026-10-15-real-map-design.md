# The world map, for real

- **Date:** 2026-10-15
- **Status:** Approved by the owner ("lets go with the map, 1,2,4 and 5 then we'll do 3 and 6")
- **Owner's ask:** "I dont like the map at all, I would like it to be more of a real map, like in kingshot."

## Scope of this step

| # | Piece | This step |
|---|-------|-----------|
| 1 | A real 3D land with places as models | ✔ |
| 2 | Free pan and pinch, streamed as you move | ✔ |
| 3 | Zoom levels (detail close in, banners and territory far out) | later |
| 4 | Tap a place: a card slides up | ✔ |
| 5 | Missionaries walk the tracks with a dotted trail | ✔ |
| 6 | Tension visible on the land | later |

Tithes and missionaries still share the map's panels. Giving them a better home comes after 3 and 6.

## The land

`WorldLand` (client, pure) grows the land from the world's seed, so every phone sees the same land.

- **Hexes:** places stay on their hex, 2 units centre to corner, but each sits up to half a unit off the centre, so the land never looks like a board game.
- **Height:** rolling hills (fractal noise), lakes where the land dips under water level, and rivers where a second noise field crosses zero.
- **Edge:** mountains rise past the world's edge.
- **Places:** each stands on levelled, dry ground.
- **Tracks:** tracks join neighbouring places and bend a little, the same way every time. A place with no neighbour joins the first place two hexes off. Where a track crosses a river it becomes a raised causeway with a plank colour.
- **Props:** trees grow on the forest noise, thinly in the meadows, never on a track or a village. Crags stand on the high hills, and mountains past the edge.
- **Chunks:** the land is cut into 16×16-unit chunks. Each chunk has a 17×17 height mesh, a 64×64 colour texture and a list of props.

## The stage

`WorldStage` lives far off to the side of the village, at x 5000, with its own camera. Opening the map swaps it in for the village camera.

- **Streaming:** chunks are grown on worker threads for the ground the camera sees, plus a margin, and dropped once well out of view. Each chunk's trees and rocks are merged into one mesh, so the whole chunk costs a single draw.
- **Settlements:** every place is a small settlement that says what it is. The Keep, Cathedral, Abbey and Market are new Blender models (`world_keep`, `world_cathedral`, `world_abbey`, `world_market`); hamlets and villages are built from cottages, carts and a well.
- **Your village:** your Hearth at its current level, the cult's huts and a woad flag.
- **Rival cults:** each camp is `world_camp`, with its `Banner` part dyed in the cult's colour.
- **Missionaries:** each walks home ↔ place along the tracks, found breadth-first over the track graph. Dots crawl ahead to where they're going. Preaching is shown as sermon gestures in front of the place.
- **Camera:** `MapCamera` keeps a fixed 54° tilt, looking north. A drag sticks to the ground, a flick glides on and slows, and a pinch zooms about the fingers, from 9 to 40 units. `FlyTo` eases over to a tapped place.

## The screen

- **Top panel:** the lord's temper and the tithe button. **Bottom panel:** missionaries and offerings. The HUD steps aside while the map is open; the toast stays.
- **Plates:** each place in the server's window has a floating plate with its name and a ring of faith shares. Far out, small places drop their names. The plates never catch a finger: the controller picks the place nearest a tap, by its model or its plate.
- **Server window:** follows the camera. When the view moves 2 hexes or zooms out, the controller fetches the window round the new focus, with a radius that covers the view (up to 14).
