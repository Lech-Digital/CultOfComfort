# Playtest on the emulator, 2026-11-17 (0.1.0, main at 88bb2c8)

A fresh install played from the intro to Hearth 2: the tutorial's errands, the first Night, Dawn, the Hearth 2 unlocks
(every side button opened), and stage 1 of the Pilgrims' Way. Pixel 9 emulator (x86_64, Android 16, 3 GB), development
build. Screenshots are in [2026-11-17/](2026-11-17/), numbered in play order.

The question was whether it's fun, and where the game repeats itself.

## In one paragraph

The intro and the writing are good: the jokes land, and the parchment and sticky notes have character. Everything between
those jokes is waiting. The first ten minutes are seven times the same chore (tap a card, drag, ✓, wait a minute, claim).
The first Night is the first real moment, and it lasts about a minute. You can barely see it, your input barely matters,
and it wrecks the village you just built. Then the game shields you for 12 hours. At Hearth 2, six new buttons appear at
once, and three of them are the same feature.

## The first ten minutes: one loop, seven times

| Errand | What the player does | Wait |
|---|---|---|
| Woodcutter's Lean-to | card → ✓ → claim | 30 s |
| Storage Hut | card → ✓ → claim | 1 min |
| Cottage | card → ✓ → claim | 1 min |
| Peat Cut | card → ✓ → claim | 1 min |
| Cultist Hut | card → ✓ → claim | 2 min |
| Slurry Chute | card → ✓ → claim | instant |
| Light the Lamps | the first Night | |

- The coach chooses every building and the drawer tab, and the ghost starts where it fits, so where to build isn't a
  decision either ([03](2026-11-17/03-place-woodcutter.jpg), [05](2026-11-17/05-place-cottage.jpg),
  [06](2026-11-17/06-place-cultist-hut.jpg)).
- The ✓ ✗ Turn buttons sit on top of the ghost and the buildings beside it. Twice they covered the Hearth itself
  ([06](2026-11-17/06-place-cultist-hut.jpg)).
- "Drag it right beside the raiders' road": the raiders' road looks just like the other dark tracks
  ([07](2026-11-17/07-place-chute.jpg)).
- Seven Towels are in stock from the start, and one Towel takes an hour off a job. The 10-minute Hearth upgrade was one
  tap. That makes the waits pointless rather than tense.
- The village fills only about a tenth of the screen. Mountains take the top quarter behind the HUD, and the rest is
  brown ground ([02](2026-11-17/02-first-screen.jpg)).

## The first Night

- **About a minute, three waves in 16 seconds.** The labels flash past ("Nosy locals…", "A wild boar…", "Wave 3 of 3")
  ([09](2026-11-17/09-night1-wave1.jpg)).
- **Hard to see.** Raiders are 15–20 px tall at 1080 px wide. I couldn't find the Prophet during the Night. Everything
  is dark brown on dark green.
- **Input mostly refused.** "He is not walking into that." "He can't throw that far. He has tried." No range ring or
  path is shown before the tap, so it reads as the game saying no ([11](2026-11-17/11-night1-cant-throw.jpg)).
- **Tips land on the fight.** The Voice and Sluice explanations cover the road where the raiders are, the first time
  each skill is used ([10](2026-11-17/10-night1-voice-tip.jpg)).
- **Disabled skills read as noise.** "Laser 4s", "Lit 2s", "Sun's down": greyed notes take a row of the panel
  ([31](2026-11-17/31-pilgrims-night.jpg)).
- **The village is flattened.** Six buildings out of six wrecked: "Repair all (6)". The storage cap drops with the wrecked
  Storage Hut (1.5k → 1k) until repaired ([12](2026-11-17/12-night1-wrecks.jpg),
  [14](2026-11-17/14-after-night1-wrecked.jpg)).
- **And still 2 stars.** "The Hearth stands, 67/100." There is no sense of what I did right or wrong
  ([13](2026-11-17/13-night1-dawn.jpg)).
- **Dawn says "VERIFIED".** That's the server replay check: true, but it means nothing to a player.
- **Then: "Shielded · 11h 58m".** The next real Night is half a day away.

## Hearth 2: six buttons at once, three of them the same

Hearth 2 (a Towel on the 10-minute upgrade) unlocks Map, Research, Pilgrims, Missionaries, Tithes and Festival in one go
([20](2026-11-17/20-hearth2-unlocks.jpg), [21](2026-11-17/21-hearth2-village.jpg)). The Hearth card's "At level 2" list
shows them as one paragraph among ten ([18](2026-11-17/18-cult-sheet.jpg)).

### The world, three times

| Screen | What it does | Same as |
|---|---|---|
| **Map** | Places on a 3D hex land; tap one to see it; "Missionaries: 1 ready"; Collect offerings; **Pay a tithe** at the top; the lord's temper | |
| **Missionaries** (the Mission House) | The same places as a list (22+ rows, all 0%, "Village · 20m walk"); tap one to preach 1/2/4/8 h; **Show** opens the Map | The Map's places and its "send a missionary" |
| **Tithes** (the lord's hall) | The lord's temper, his demand, two tithe buttons, **Show his seat on the map** | The Map's temper line and "Pay a tithe" |

The Map's own help says it: "Tap a place and send a missionary… Pay a tithe to calm him"
([22](2026-11-17/22-map-help.jpg)). One world screen with a place card (preach here) and the lord's seat on it (tithe)
covers all three. The Mission House list adds names that blur together ("St. Agnes of the Drain's Abbey 14" and
"…16", "Nettle End Magna", "Nettlebrook Magna") and progress bars that are all empty
([25](2026-11-17/25-mission-house.jpg)).

The first place I tapped on the Map was a walk of 15 h 20 m each way: "Too far for now"
([24](2026-11-17/24-map-place.jpg)). The places you can actually reach aren't marked on the map.

### Other repetition

- **The Cult sheet** (Hearth, People, Comfort, Relics, Errand tabs) repeats the Hearth card ("You can also tap the Hearth
  itself") and the errand note ([18](2026-11-17/18-cult-sheet.jpg)).
- **The Prophet sheet** is a level and "None yet" ([19](2026-11-17/19-prophet-sheet.jpg)).
- **Festival, Pilgrims' Way, errands, the lord's letter, visitors, missionaries' tales**: six systems that each hand out
  small rewards on their own timer. The Festival scores the others ("every star", "every minute of building",
  "every hour a preacher spends preaching") ([30](2026-11-17/30-festival.jpg)).
- **Shop** (the Holy Trolley) is a list of swaps, half of them greyed out ([17](2026-11-17/17-shop.jpg)).

## The Pilgrims' Way Night (Hearth 2)

- It's the answer to the 12-hour shield ("Fight it any time… Nothing in the village gets broken"), but it's one of six
  side buttons, with no hint after the first Night ([29](2026-11-17/29-pilgrims-way.jpg)).
- **The camera is far out.** At Hearth 2 the board is the whole valley. The village is a speck in the top right, and the
  raiders are dots ([31](2026-11-17/31-pilgrims-night.jpg); [32](2026-11-17/32-pilgrims-night-crop.jpg) is a
  full-resolution crop).
- **Won without a single input.** It took about 3 minutes. The Prophet fought by the Hearth on his own down to 12 HP and
  came back. The Hearth stayed at 100 ([33](2026-11-17/33-pilgrims-night-late.jpg)).
- **Bug: a tap went through the Dawn card.** A "Wild tree" card opened on top of the result card
  ([34](2026-11-17/34-tap-through-dawn-card.jpg)).

## Where the fun is, and where it isn't

| Fun now | Not fun now |
|---|---|
| The intro, the voice of every line ("Oink. Menacingly.", "Lord Wulfric remains unconvinced.") | Seven identical build-wait-claim errands |
| Raider barks and SMASH! pops | A Night you can't see and needn't touch |
| The parchment and sticky-note look | Your village flattened by the first Night, then a 12 h wait |
| The idea of the Prophet as bait | Six buttons at once, three of them one feature |

## What this suggests (for the next ADR)

1. **Make the Night the game.** Close camera that follows the action, units twice the size, a range ring before every
   tap, real health bars, a Night that needs you to win (passive play should lose stars). Something to push your luck on
   between waves.
2. **One world screen.** Merge Map, Missionaries and Tithes. Fold the Cult sheet into the Hearth card and the Prophet
   portrait.
3. **Unlock one thing at a time,** each with its own moment, not six buttons at Hearth 2.
4. **A first Night that leaves the village standing,** and the next fight (the Pilgrims' Way) offered right on the Dawn
   card instead of a 12 h shield.
5. **Errands with choices,** not seven times card, ✓, wait, claim.

The design review of the HUD and these screens follows in [2026-11-17-design-review.md](2026-11-17-design-review.md).

## Setting up the playtest (Linux host)

- The emulator APK is built with `-emulator`: x86_64 only, a light IL2CPP setup (size-optimised code, debug C++). It
  fits in RAM beside the emulator on a 16 GB machine. Build Unity under a memory cap, and never at the same time as the
  emulator:
  `systemd-run --user --scope -p MemoryMax=7G -p MemorySwapMax=1G Unity -batchmode -quit … -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroid -emulator`
- Build both `src/CultOfComfort.Simulation` and `src/CultOfComfort.Contracts` before Unity (both copy their DLLs into
  `Assets/Plugins`).
- AVD `coc-play` (Pixel 9, `system-images;android-36;google_apis_playstore;x86_64`). At least 3 GB: at 2 GB, Android's
  system server keeps dying. Start it with `-feature -Vulkan`: on the emulator's Vulkan driver, a development build
  crashes in `vk::DebugToolsUtils` on the first frame.
- The guest account for this playtest was created on the production server
  (`api.cultofcomfort.lechdigital.nl`): the Settings sheet said "Cloud save: synced".
