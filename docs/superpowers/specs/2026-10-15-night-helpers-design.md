# Night helpers: relics that fire themselves, cultists who defend, gates that open

- **Date:** 2026-10-15
- **Status:** Asked for by the owner:
  - "pointing with laser is not really handy… laser should act automatically, maybe other weapons too?"
  - "the gate should open when prophet or other of our allies are coming through, because our cultists should also defend the base"

## Relics fire themselves

A Night planned since this change has `AutoRelics` on its plan. Each tick, a ready relic fires when it's worth it:

- **Laser:** blinds the raider within reach that is at a building (raiding or swinging). Failing that, it picks the raider furthest along the road.
- **Torch:** lights when at least 2 raiders (`TorchAutoCrowd`) are within its radius of the Prophet.
- **Magnifier:** while the sun is up, focuses on the raider within reach with the most others inside its radius.

The relic buttons read "Laser · auto" and so on. Tapping one explains what it does; it doesn't fire it.

**Hot Stones aims itself.** A tap on the card throws at the tile within reach with the most raiders under the splash (`Battle.BestHotStonesTarget`). With nobody in reach, it falls back to tapping a tile inside the pebble ring.

## Cultists defend

Each standing Cultist Hut sends out as many cultists as its level. They are all posted on the open tile beside the hut that is nearest the Hearth.

**How a cultist behaves:**
- Within 4 tiles of their post, they go after the raider nearest them, re-planning their path twice a second.
- When a raider is in reach (1 tile), they hit it for 4 every 1.2 s.
- With nobody to fight, they walk back to their post.

**Stats:** 30 health and 1.6 tiles/s.

**Raiders fight back.** A brawling raider with a cultist within swing stops and fights them, using its anti-Prophet damage and cooldown.

**When a cultist is beaten:** they're patched up at the hut for 15 s, then come back out at their post with half health.

**On screen:** cultists have a woad ring at their feet and a health bar. They walk, swing, and swoon when beaten, and a puff marks their return.

## Gates open for friends

A gate's leaves swing back while the Prophet or a cultist is within 1.5 tiles of it. A gatehouse lifts its portcullis instead. The gate models now have `DoorL`/`DoorR` and `Portcullis` parts.

This is visual only. In the simulation, the Prophet already walks through gates and raiders still have to break them.

## Replays

- **Old Nights:** a Night planned before this change has neither part on its plan and replays exactly as it was.
- **New Nights:** the plan carries `autoRelics` and `defenders` in its saved form. Defenders are only added to the state hash when there are some.

## Pace

The pace bots are unchanged: Hearth 2 at 24 minutes for the errands bot and 159 for the builder bot.
