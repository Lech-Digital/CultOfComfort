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
7. First Night (**smaller**: see below)
8. Hearth 2
9. Missionary (in the Mission House)
10. **Midden Sorter**
11. Study
12. **Sapling Flinger**
13. **Gate across the road**
14. Lean-to level 2
15. **Cultist Hut level 2**
16. **Peat Cut level 2**
17. Hearth 3
18. Cistern
19. **A second study**
20. Patron
21. Win a raid

**The first Night is smaller:** budget 12 and 3 waves (was 20 and 4), for the first Night only and only when you light the Lamps yourself, not for an announced raid. Found on the Pixel: with the Chute and a Gate, the first Night was still lost at 1★, Hearth 13/100. Replayed on the Mac with that layout:

| First Night | Nobody playing | Prophet using his skills |
|---|---|---|
| Old size (20, four waves) | 1★ (Hearth 13) | 2★ (Hearth 56) |
| New size (12, three waves) | 2★ (Hearth 67) | **3★ (Hearth 100)** |

**The Gate moved after the Flinger.** At Hearth 1 it made the Night worse (Hearth 13 rather than 23 without it): goats break gates, and the raiders it holds up wreck what stands near it. With a Flinger it holds them under fire.

The defence errands repay what they cost. The coach tells a tower to go *right beside the raiders' road* (a tower away from it stands unconnected). It tells the gate to go *onto* the road.

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
