# The first day: a defended first Night, a fuller middle, research in four rooms

- **Date:** 2026-10-16
- **Status:** Approved by the owner ("makes sense, lets do it :) and we should add more research, and make them into categories").

Playing a fresh village on the Pixel and running the pace bot showed three problems:

- **The first Night is always lost.** The first defence (the Slurry Chute) came four errands after the first Night.
- **The middle is empty.** Hearth 2 comes at about 25 minutes. Hearth 3 then takes about 30 hours with almost nothing to do. Scrap is the wall: the Midden Sorter makes 12 an hour, and the Hearth asks for 300.
- **Research is a short list of flat bonuses**, with nine studies across two "eras".

## 1. Defend before the first Night

The errand chain now reads:

1. Lean-to
2. Storage Hut
3. Cottage
4. Peat Cut
5. Cultist Hut
6. **Slurry Chute beside the raiders' road**
7. **Gate across the road**
8. First Night
9. Hearth 2
10. Missionary (in the Mission House)
11. **Midden Sorter**
12. Study
13. **Sapling Flinger**
14. Lean-to level 2
15. **Cultist Hut level 2**
16. **Peat Cut level 2**
17. Hearth 3
18. Cistern
19. **A second study**
20. Patron
21. Win a raid

The defence errands repay what they cost, so the tutorial clock doesn't slow down. The coach tells a tower to go *right beside the raiders' road* (a tower away from it stands unconnected). It tells the gate to go *onto* the road.

Errands used to be saved by position, so this order would have moved existing players to a different errand. The save now also carries the errand's **id**. A save without one is read through the old order (`ErrandBook.LegacyOrder`): the player stays on the same errand. Errands that moved in front of them are skipped.

## 2. Hour one to day one

- **The lord's first letter comes 30 minutes after Hearth 2** (was 10 hours). The hall has something to argue with on the first evening. Later letters keep the 10–14 hour gap.
- **Scrap:**
  - The Midden Sorter makes 30/h at level 1 and 50/h at level 2 (was 12 and 22).
  - Two may stand at Hearth 2 and three at Hearth 3 (was one and two).
- **The Hearth 3 upgrade** asks for 1,200 Timber, 150 Peat, 200 Scrap and 100 Devotion, for 45 minutes (was 2,000 / 200 / 300 / 100 and 60 minutes).
- The Peat Cut upgrade comes before the Hearth errand: once Scrap was fixed, Peat was the next wall.

### Pace (the Sandbox `pace` bot, 2026-10-16)

| | Before | After |
|---|---|---|
| Errand player, Hearth 2 | 24 min | 27 min |
| Errand player, Hearth 3 | 30.4 h | **6.7 h** |
| Errand player, longest wait between errands | ~29 h | ~3 h (saving for the Hearth: all four stores run out together) |
| Builder (ignores errands), Hearth 3 | not in 48 h | 14.3 h |

The bot can now send preachers and study, and it saves for whatever the current errand asks (it used to spend every Timber on extra cottages first).

## 3. Research in four rooms

Every study belongs to a **category**, shown as a tab on the Research sheet:

- **Workshop:** what the village makes.
- **Night:** fighting.
- **Pulpit:** faith, the lord and preachers.
- **Bath:** Comfort.

Each room has studies at Hearth 2 and deeper ones at Hearth 3, some of which need an earlier one. 25 studies in all, still one at a time.

New effects:

| Effect | Where it acts |
|---|---|
| Scrap +% / Devotion +% | producer rates, like Timber and Peat |
| Faster building −% | construction and upgrade times (set when the work starts) |
| Salvage +% | the Scrap a Night leaves behind |
| Sluice +1 | Open Sluice charges (into the Night plan, like Cisterns) |
| Tower damage +% | both towers' damage (into the Night plan) |
| Prophet health +% | his health (into the Night plan) |
| Smaller demands −% | the lord's letters ask for less |
| Slower temper −% | the lord's temper rises more slowly |

Night effects are written into the **Night plan** (`TowerDamagePercent`, `ProphetHealthPercent`) and saved with it. Client and server fight the same Night, and Nights planned before this change replay exactly as they did (both 0: the default balance).

## Not done

- Four facings for buildings (still two: `Rotated`).
- New towers and Hearth 4+.
