# Cultist Work: Crews, Slots and Idle Hands

- **Date:** 2026-10-14
- **Status:** Approved by the owner ("go on"), as the next item after the shared world
- **Builds on:** GDD 01 ("Assign Cultists: changes production rate, diminishing returns per slot"), GDD 03 (worker slots per building)

## What changes

Producers used to be crewed automatically and all-or-nothing, in building order. A Peat Cut with one villager free produced nothing, and the player couldn't choose who worked where.

Now:
- **Every producer level has worker slots.**

  | Building | Slots by level |
  |---|---|
  | Woodcutter | 1 / 2 / 3 |
  | Peat Cut | 2 / 3 / 4 |
  | Cultist Hut | 1 / 2 / 3 |
  | Midden Sorter | 1 / 2 |
  | Bathhouse | 2 / 3 |

  Level 1 keeps the crew it needed before.
- **Diminishing returns per slot.** The workers add 100, 50, 30 and 20 shares, in that order.
  - Output = the level's full rate × (the shares of the workers present) ÷ (the shares of all its slots).
  - A full crew makes exactly what the building made before. **The pace isn't faster.**
  - A partial crew still makes something. A Peat Cut with one of its two workers makes 2/3 of its rate. The first worker is worth the most.
  - An upgraded building with the old crew still beats the old level: a level 2 Woodcutter with one worker makes 113 Timber an hour, against 100 at level 1. Upgrading never hurts, but it asks for more hands to pay off fully.
- **The player sets each building's crew** (0 to slots) on its card, with − and +. Until they touch it, a building wants a full crew.
- **Who actually works** is worked out from that setting and not stored. The villagers at home (population minus missionaries away) fill each building's chosen crew in building order, oldest first. When missionaries leave or villagers move out, the newest buildings lose hands first. When people come back, the crews refill.
- **Idle villagers** are the ones left over. The HUD shows them ("2 idle"), a cue to build or crew something. They still burn Peat.

## Rules and storage

- New command `SetCrew(buildingId, crew)`. It is refused if the building has no slots, or if the crew is below 0 or above the slots.
  - Production up to now is materialised first, as with every command.
  - It is refused during a Night, like other building changes.
- The building's chosen crew is stored as `crew` on the building (null means a full crew). Old saves load as "full crew wanted", which is exactly the old behaviour plus partial crews.
- The snapshot carries `Crew` (working), `CrewNeeded` (slots) and `CrewWanted` per building, and `IdleVillagers` for the village.
- The server applies `SetCrew` like any other command. It has a wire form.

## Not in this slice

- Dragging cultists onto buildings (the card's − and + do the same on a phone).
- Priorities other than building order.
- Night repair crews.
- Idle villagers praying for Devotion. Idle is idle, so nothing gets faster.
