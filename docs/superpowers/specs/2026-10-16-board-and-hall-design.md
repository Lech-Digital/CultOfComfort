# The mission board and the lord's hall

- **Date:** 2026-10-16
- **Status:** Built. Written up from the code after the design session was lost; the owner's goal was "improving missionaries and tithes, and general feeling".

Two changes to how the Domain feels: preachers come home with a story you finish (the mission board), and the lord gets a screen of his own where a demand is something you can argue with (the lord's hall).

## The mission board: road tales

- A preacher who gets home still earns sermon experience at once. What the road gave is no longer rolled for you: they bring home a **road tale** (`ChooseTale`), with **two endings** to pick from, shown on their card in the Mission House.
- The tale is picked from the missionary and preacher ids, so it's the same on client and server. Amounts scale with the sermon's hours; a **Lucky** preacher finds twice as much.
- Up to **4** tales wait. When a fifth comes in, the oldest ends **the free way** (its first ending that costs nothing and angers nobody).
- Ending a tale writes the road report (with the tale and the choice; a gamble that failed is stored as `-1 - choice`). A gift never overfills the stores. A failed gamble still gives 1 xp. The daily "road-find" errand counts a tale that gave something.

| Tale | Ending A | Ending B |
|------|----------|----------|
| Goat | 8 Timber/h | 3 Devotion/h |
| Widow | 2 Devotion/h, +2 xp | 6 Timber/h |
| Ditch | 3 Scrap/h | 7 Scrap/h at 60% |
| Toll | pay 4 Timber/h: 3 Devotion/h, +2 xp | 4 Peat/h |
| Turnips | +3 xp | 5 Peat/h |
| Pedlar | 3 Scrap/h | 2 Devotion/h, +1 xp |
| Bog | +2 xp | 6 Peat/h |
| Shrine | +3 xp | 3 Devotion/h, temper +2 |

## The lord's hall

- A screen of its own (not the map): his portrait and mood (Content → Grumpy → Seething → Furious, by temper), a line of speech, the temper bar, his open demand as a **letter** with Deliver, three **pleas**, and the voluntary tithe below.
- With no demand open, it says when his next letter comes (or the Hearth level when demands start).
- Pleas (`PleadWithLord`), only over the open demand and not during a Night:

| Plea | Effect | Limit |
|------|--------|-------|
| **Stall** | +3 h to the deadline, temper +5 | once per demand |
| **Haggle** | 50%: the demand drops to 60% (rounded down to tens); otherwise he refuses, temper +8 | once per demand |
| **Flatter** | costs 15 Devotion × Hearth level; the next haggle can't fail | once, and only before haggling |

- The haggle's odds come from the demand id, so trying again after a reload can't change the result.
- Stalled, haggled and flattered are saved with the demand (`DomainDto.demandStalled/demandHaggle/demandFlattered`); waiting tales are saved as `DomainDto.tales`.

## Not done

- Not checked on the Pixel 10 yet.
