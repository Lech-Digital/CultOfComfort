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

## Step 2: the realm, the lord's temper, tithes and missionaries

The owner approved this step: "fix this thing and then work on the next points, then work on tithe and missionaries".

### Far out (point 3)

- **Zoom range:** the camera now zooms out to 62.
- **Domain borders:** each lord's lands are drawn on the ground. They are the ground nearest his seat, with a dashed ink line where two seats are about as near.
- **Plates:** past distance 30, only seats (on dark plates), camps, your village and your holdings keep a plate.
- **Faith rings:** each place in view gets rings on the ground. Yours, in hi-vis, is on the inside; the strongest rival's, in its colour, is on the outside. Each ring widens with its share.

### The lord's temper on the land (point 6)

- **War banner:** past a third of his temper, his banner flies over his seat and grows as his temper rises.
- **Patrols:** past 35%, his men-at-arms walk the road from his seat to your village, 1 to 4 of them.
- **Raid warning:** a column of six marches on your village behind a red trail.

### Tithes and missionaries: one map, three ways in

| Button | Opens the map… |
|--------|----------------|
| Map | on your village |
| Missionaries | with the roster |
| Tithes | flown to the lord's seat, with its card open |

- **The roster:** one row for every missionary out (what they're doing, where, and a progress bar for this leg or sermon) and one for every slot ready at home. Tapping a row flies the camera to that missionary.
- **The tithe is paid at the seat:** the seat's card shows his temper, what it does, and the tithe options. After paying, a cart trundles from your village to his seat.
- **Map panels:**
  - The top panel is just his temper and "Pay a tithe", which flies to the seat.
  - The bottom panel is a missionaries button (who's out, or how many are ready) and the offerings with Collect.
