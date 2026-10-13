# First-Hour Arc — The Prophet's Errands — Design

- **Date:** 2026-10-13
- **Status:** Draft, waiting for the owner's approval
- **Owner's words:** "2, then 1 and then 3" (smoothness pass, then this, then more village content); "feel free to play
  it through"
- **Goal:** a new player always knows what to do next and why, and the Hearth levels arrive on the GDD's clock
  (`docs/gdd/02-economy.md`: Hearth 1→2 ≈ 20 min "tutorial", Hearth 2→3 ≈ 3.5 h).

## 0. What playing it showed (2026-10-13)

A fresh village on the Pixel 10, plus a pacing bot that plays the real `BaseState` on a fake clock (a "builder"
player who builds a bit of everything, and a "rush" player who builds Woodcutters and goes straight for the Hearth):

| | Hearth 2 | Hearth 3 | Builder idle |
|---|---|---|---|
| Builder player | 3 h 35 min | — | most of the time |
| Rush player | 62 min | 13 h | 82 % of 30 h |
| GDD target | ~20 min | ~3.5 h | — |

1. **No direction.** Nothing says what to do or why. The biggest button on a brand-new screen is *Light the Lamps
   (early)*.
2. **Hearth 2 is slow.** It costs 600 Timber, and you start with 500. Building anything delays it, and the natural
   player spends the 500 on a village first.
3. **Devotion blocks Hearth 3.** A Cultist Hut makes 4/h, and Hearth 3 needs 100.
4. **Scrap runs dry after Hearth 3.** Nothing produces it except Night salvage and boulders. (The Midden Sorter is
   already planned for step 3; this design does not add it.)
5. **Paper cuts:** the ghost's *Turn* button is clipped at the right edge; new ghosts appear near the raiders' road
   instead of near the Hearth; you can build a producer where no road can ever reach it (it never works, and nothing
   warns you); the resource pills have no labels; "50 demo" and "Comfort 42 · demo" placeholders show.

## 1. Errands (village rule)

The Prophet hands out **errands**: a short ordered chain of goals, each with a small reward. They are village rules in
`CultOfComfort.Simulation/Base`, so a server can enforce them later exactly like everything else:

- `ErrandBook` (data, versioned like `BuildingTable`): an ordered list of errands, each `{ id, text, condition, reward }`.
  Each condition is a plain function of the `BaseSnapshot` plus a few counters the village already keeps (Nights won,
  things collected).
- `BaseState` stores the index of the current errand. `Snapshot` shows the current errand with its progress
  ("Cottages 1/2") and whether it is **done**.
- `ClaimErrand(commandId)`: accepted only when the current errand is done. It pays the reward and moves to the next
  errand. Rewards are never automatic, because claiming one is a small, satisfying tap.
- Errands that are already true count at once. A returning village that already has two Woodcutters just claims that
  errand.
- After the last errand, the strip goes away.

### The chain (first draft, with tuned rewards)

| # | Errand | Reward | Teaches |
|---|---|---|---|
| 1 | Build a Woodcutter's Lean-to | 50 Timber | the build flow |
| 2 | Collect Timber from it | 50 Timber | collecting |
| 3 | Build a Cottage | 60 Timber | villagers and beds |
| 4 | Build a Peat Cut | 40 Peat | Peat keeps the village warm |
| 5 | Build a Cultist Hut | 20 Devotion | Devotion |
| 6 | Light the Lamps: survive your first Night | 100 Timber, 40 Scrap | Nights, the Prophet, skills |
| 7 | Raise the Hearth to level 2 | 30 Devotion | the Hearth is the goal |
| 8 | Put a Slurry Chute beside the road | 50 Scrap | towers |
| 9 | Build a Gate on the raiders' road | 100 Timber | gates |
| 10 | Upgrade a Woodcutter to level 2 | 150 Timber | upgrades |
| 11 | Build a Cistern | 1 extra sluice charge for your next Night (one-time) | the Cistern |
| 12 | Win a raid with 2 stars or more | 100 Scrap | real raids and defence |
| 13 | Raise the Hearth to level 3 | — (the end of the arc) | |

Hearth 1 goals 1–7 pay **+300 Timber in total**. With the starting 500, that pays for a Woodcutter, a Cottage, a Peat
Cut and a Cultist Hut, with Hearth 2 ready around **minute 20** (the bot checks this, see §5).

## 2. Pacing numbers (balance)

`BuildingTable` becomes `village-2026.10.4`. Night combat balance stays the same, so the demo hash is unchanged:

- **Cultist Hut Devotion:** 4 / 8 / 14 per hour → **10 / 18 / 28**. With two huts at Hearth 2 plus the errand rewards,
  the 100 Devotion for Hearth 3 takes about 3 h instead of about 12 h.
- **Hearth 3 cost** stays 2000 Timber / 200 Peat / 300 Scrap / 100 Devotion. The bot shows that with the new Devotion
  rate and errand 10, Timber is the limit at about 3.5 h, which matches the GDD.
- Everything else stays the same unless the bot's re-run (§5) says otherwise. If it does, only these two knobs move:
  errand rewards and the Hearth 3 Timber cost.

**Owner's call:** the GDD wants Hearth 3 at about **3.5 h** (two or three sessions). I suggested "within an hour"
earlier, which would make Hearth 3 a first-session thing and need much bigger cuts. **I recommend the GDD's 3.5 h.**

## 3. Guided start (client)

- **The errand note:** a sticky note pinned under the Hearth panel shows the current errand, its progress, and a
  ✓ *Claim* stamp when it's done. Tapping the note points at the thing to do: the card in the build drawer wiggles, or
  the camera glides to the building.
- **First launch:** the camera starts on the Hearth, and the Prophet says one line in a speech bubble ("A cosy spot.
  Let's make it cosier. Build us a woodcutter, would you?"). No tutorial screens.
- ***Light the Lamps* waits its turn:** before errand 6 it is a small locked note ("The lamps can wait. Build first."),
  not the big orange button. The development menu's *Raise Tension* still works for testing.
- Each errand claim gets a short bark from a cultist (`BarkKind.CultistErrandDone`).

## 4. Paper cuts fixed on the way (client)

- The ghost's ✓/✗/*Turn* row flips to the ghost's left when it would leave the screen.
- A new ghost appears on the nearest free spot that is **closest to the Hearth** and already reachable by a road, not
  in the middle of the view.
- A producer with no possible road shows a red ghost: "No road can reach this spot." (village rule: `PlacementProblem`).
- Resource pills: tapping one shows its name and income ("Timber · +200/h").
- The "demo" placeholders are hidden (Comfort and the gem counter come back when they're real).

## 5. Verification

- **Simulation (xUnit):** `ErrandBook` conditions (each errand true/false on hand-made villages); `ClaimErrand`
  rejected before the errand is done, paid exactly once, moves to the next errand; save round-trip keeps the current
  errand; Cultist Hut rates; replay/demo hash unchanged.
- **Pacing bot** moves from the scratchpad into `tools/CultOfComfort.Sandbox` as `pace`, with a policy that follows
  the errands. Targets: Hearth 2 at 15–25 min, Hearth 3 at 3–4.5 h, builder busy most of the first 20 min.
- **Unity:** EditMode tests for the errand note model and the ghost button flip; PlayMode test: fresh village →
  first errand shown → build → claim → second errand shown.
- **Device:** a fresh village on the Pixel, played via adb through errands 1–7, with the frame logger on.

## Out of scope

Scrap production (Midden Sorter, step 3), Comfort/Squalor, a server (`/api/base/sync`), voice-over and tutorial
screens, and errands beyond Hearth 3.
