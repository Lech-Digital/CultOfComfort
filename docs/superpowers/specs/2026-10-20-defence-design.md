# Defence: roads that change, towers that reach, wardens who help

- **Date:** 2026-10-20
- **Status:** Approved by the owner ("plan looks cool! cap should be the amount, bigger with each hearth level. … towers … should be effective from distance too … upgradable … a building that will allow to have cultists as defenders … or maybe even they can man the towers?").

The owner's worry: once a player finds the best tower layout, every Night is the same Night. There was one entrance and one fixed road, so the best spots never changed.

## More roads, and a scout

| Road | Opens | From |
|---|---|---|
| North road | Hearth 1 | the pass at (0, 1), as before |
| South road | Hearth 2 | the forest's edge at (28, 25), north up column 28, west along row 13, into the Hearth from below |
| East road | Hearth 3 | the east edge at (47, 17), west along row 17, joins the south road at (28, 17) |

- Each Night takes one open road. The choice is fixed by the Night's number and the raid's seed (`BaseState.TonightsRoad`), so it is known at dusk.
- The **scout** names it: "Scouts: the south road" on the Tension strip, and a red flag where the raiders will come out of the forest.
- The plan carries the road (`NightPlan.Road`, 0 for every older plan), and client and server fight the same one.
- The new roads run over open ground and the village's own tracks, round the rock and boulders. When a road opens, whatever stands on it moves to the nearest spot that fits, else comes down with its cost back. Tracks on it just go, because the raiders' road carries the network. Saves already past that Hearth level are cleared the same way when loaded.
- The south and east roads share their last stretch. Towers there cover both; towers on the north road cover only it.

## Towers

- **Three levels.** Each level past the first adds +1 tile of reach and +30% damage. Upgrades are instant, cost Timber and Scrap, and level N needs Hearth N.

  | | Reach L1 / L2 / L3 |
  |---|---|
  | Slurry Chute | 2.5 / 3.5 / 4.5 |
  | Sapling Flinger | 3.5 / 4.5 / 5.5 |
  | Pressure Sluice | 3 / 4 / 5 |

- A tower set back from the road still hits it. A ring of dots shows the reach while placing a tower and while its card is open.
- **A cap on towers**, every kind together: **4 / 7 / 10 / 14** at Hearth 1 / 2 / 3 / 4. Towers already standing past the cap stay; no more can be built.
- Level 1 unmanned towers use exactly the old stats, so older Nights replay as they were.

## The Guardhouse (Hearth 2)

A 2×2 hall with a lookout and a bell: 1 / 2 / 3 can stand at Hearth 2 / 3 / 4. Its crew are its **wardens**: 2 / 3 / 4 by level, villagers like any other crew.

- **On duty** (the default): each warden mans the nearest unmanned tower within 8 tiles. A manned tower fires every 65% of the time. A small cultist stands on it at Night.
- **On patrol:** the wardens come out and fight beside it, like a Cultist Hut's cultists.
- A warden with no tower in reach patrols anyway.
- The card's button switches the duty (`SetDuty`), and the duty is saved.

## Checked on the owner's save (Hearth 3, 25 towers along the north road; nobody playing)

| Road | Result |
|---|---|
| North | won, 3★ |
| South | lost, 0★ (no tower within 3 tiles) |
| East | lost, 0★ |

## Not done

- No new raider types per road yet; the scout names only the road.
- Moving many towers at dusk is still one at a time.
