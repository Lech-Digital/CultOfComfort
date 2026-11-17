# Fun first: a Night that needs you, one thing at a time, a village you can see

- **Date:** 2026-11-17
- **Status:** Proposed, for the owner's review
- **From:** the [emulator playtest](../../playtests/2026-11-17-emulator-playtest.md) and its
  [design review](../../playtests/2026-11-17-design-review.md)

## Why

The playtest found the game charming but not yet fun. The core loop, the Night, is a minute long, hard to see and
plays itself: passive play wins 2★, and the Pilgrims' Night was won without a single input. Then the village is
shielded for 12 hours. Around the Night, the game grew three screens for one feature (Map, Missionaries, Tithes), six
buttons that unlock together, and seven identical build-wait-claim errands.

DeadWeight went through the same thing (its playtest of 2.2.9 and ADRs 0004–0005). Three lessons carry over:

- **Cut sprawl, but don't cut decisions.** DeadWeight's 3.0 cut too far and became passive ("winnable in 5 minutes"). Here
  we merge screens and slow the unlocks, but **add** decisions to the Night.
- **Choices you play, not pick.** Push-your-luck steps, the odds shown before you commit, a beat of suspense, a stamped
  result. In this game that means a boon between waves, calling a wave early, and stars you earn with your hands.
- **Measure it with bots.** The Sandbox already has a pace bot. Add an idle bot and an active bot to the Night. The
  idle bot must not reach 2★, and the active bot should reach 3★ most of the time.

## Phases

Each phase ships on its own, is played on the emulator before the next one starts, and can be stopped after. Phases
A–B change only the client. C changes the simulation (a new balance version, see "Determinism" below). The order is:
the quick wins, then make the Night readable, then make it need you, then tidy the meta, then the first session, then
the look.

### A. Quick wins (about 2 days, client only)

1. **The big button follows what you can do.**
   - Shielded: **"Walk the Pilgrims' Way · Stage N"**, with "Shielded · 11h 58m" as small text above it.
   - Raid warning: "Face the raid · 7h 59m".
   - Ready: "Light the Lamps".
   - The Pilgrims' Way opens right after the first Night, not at Hearth 2.
2. **Dawn says what happened.**
   - Big stars; what you earned, with resource icons; the Prophet's XP bar.
   - A second button: "Next: Pilgrims' Way, stage N".
   - "VERIFIED" and the state hash are shown in development builds only. The Settings sheet hides "Deterministic: OK ·
     1ddc…" the same way.
3. **One new thing at a time.**
   - The Pilgrims' Way after Night 1.
   - Research at Hearth 2.
   - The Map once there is a place within reach.
   - The lord's hall the first time his temper rises.
   - The Festival on day 2.
   Each comes with an errand or a single toast, never six together. The Hearth card shows what the next level brings
   (now only on the Cult sheet).
4. **Taps don't fall through cards.** A gesture that started before a card opened is dropped when it ends, and world
   taps are ignored for 0.3 s after any card opens. This fixes the "Wild tree" card that opened over Dawn.
5. **The coach ends.** No coach step after Hearth 3 or once its errand is done ("Tap the Cottage card" shows at Hearth 5
   in the store screenshots).

### B. A Night you can read (about 4 days, client only)

1. **The camera comes in.**
   - Frame the Hearth, the Prophet and the last stretch of road before the defences, not the whole valley. Raiders
     should be at least 40 px tall on a 1080 px wide screen.
   - Follow the Prophet softly, with a dead zone in the middle.
   - Pinch out for the overview; it eases back after three seconds.
   - Fix the Pilgrims' framing (`VillageFraming.NightBounds` frames 3/5 of a long road, so the village is a corner
     speck).
   - Keep today's pitch (about 58°) and check it on the phone before changing it.
2. **You can always find the Prophet.** A teal ring on the ground, an outline, and a little more scale at Night.
3. **Ranges are drawn, not refused.**
   - His umbrella reach is drawn solid and his lure reach dashed.
   - The Hot Stones throw range is drawn whenever Stones is ready, not only while aiming.
   - A tap outside a range snaps to its edge, or shows the ring flashing red, instead of "He can't throw that far".
4. **Health where it happens.**
   - Bars over damaged raiders and buildings.
   - A red pulse and an arrow at the screen edge when something off screen is hit.
   - The Hearth and Prophet bars get different colours.
5. **Tips between waves, never on top of them.** The first-use explanations wait for the gap after a wave and appear at
   the bottom of the screen. The scouting lines ("Nosy locals…") go on a roster card at Dusk.
6. **The skill panel.**
   - Three or four active skills as round notes in an arc, bottom right, with radial cooldowns and a pop when ready.
   - Passive relics become small chips with ring timers.
   - The speed toggle moves top right.
   - The movement hint goes after the second Night.
   - The panel takes no more than 18% of the screen height (today about 22–30%).
7. **Feel.**
   - Damage numbers; "CRIT!" when a Flinger hits a muddied raider.
   - A short hit-stop on Hot Stones; a small shake when a building breaks.
   - "+Scrap" on kills.

### C. A Night that needs you (about 5 days, simulation and client)

This is the heart of the change. Every addition is a player input, so it lives in the input log and replays on the
server.

1. **Stars you earn.**
   - 1★: the Hearth stands.
   - 2★: the Hearth is above 60%.
   - 3★: above 80%, **and** the Night's objective. The objective is shown at Dusk; examples: "Three raiders in one Hot
     Stone", "The Prophet never swoons", "No building wrecked", "Lure five into tower range".
   - A Night with no input from the player gets at most 1★, and so does auto-resolve.
2. **A boon between waves.** In the gap, three sticky notes and you pick one, or keep the gap short by ignoring them.
   - "Duct-tape the chute": towers fire half as fast again this wave.
   - "Bluetooth at full volume": the Voice cools down in half the time.
   - "Hand out umbrellas": cultists repair during the wave.
   - A small table of boons, three drawn from the seed, so replays agree.
3. **Call the next wave early.** A round button with a ring timer during the gap. Calling early pays 10% more salvage
   for that wave. It's push-your-luck in DeadWeight's sense: more reward, less breathing room.
4. **The first Night teaches instead of punishing.**
   - It lasts about 2½ minutes: three waves of about 45 seconds, 10-second gaps.
   - The towers visibly do the killing.
   - Raiders may wreck **one** building, which teaches Repair. Today they wreck all six, and the storage cap drops with
     the Storage Hut.
5. **Nights grow towards the GDD's 3–5 minutes** with the Hearth (`WaveGenerator`'s 8-second gap becomes 10 seconds;
   wave counts and budgets are re-tuned with the bots).
6. **The Prophet's first perk at level 2**, so the first Dawn ends on a choice.
7. **Bots in the Sandbox.**
   - `idle`: no input at all.
   - `active`: walks the Prophet to lure raiders, uses skills on cooldown, takes the boon that fits.
   - Targets: idle at most 1★ on any Night after the first; active 3★ on most early Nights.

### D. One world screen (about 3 days, client only)

1. **Lands = the Map.** The Mission House becomes a tray on the Map: your preachers, and three to five recommended places
   (near, cheap, a good payoff) instead of a list of twenty at 0%. Tap a place to preach there.
2. **The lord's hall opens from the Tension strip and from his seat on the map.** The Missionaries and Tithes buttons go,
   and so does the Map's second home button.
3. **One name per thing.**
   - Preachers, not missionaries, everywhere: button, sheet, errands and the Map's help.
   - The lord is named the same on the strip, the hall and the locked note.
4. **The Map opens on the nearest place you can reach**, and places out of reach are dimmed.
5. **The Tension strip becomes a real meter**: an empty track filling with tape, the lord's face at the edge. It leads
   with what matters ("Shielded · 11h · next: the Levy, north road"), not the season. The season moves under the
   portrait.
6. **The Cult sheet keeps People, Comfort and Relics.** Its Hearth tab moves to the Hearth card, and its Errand tab goes.
   **The Prophet sheet** is about the Night: skills, ranges, perks.

After D the frame has eight entry points instead of twelve:
- top: portrait, resources, settings, the strip;
- left: Lands, Study;
- right (timed): Pilgrims' Way, Festival;
- bottom: Shop, the big button, Cult.

### E. The first fifteen minutes (about 4 days, simulation and client)

1. **A fight inside three minutes.** After the Woodcutter, four nosy locals walk in and the Prophet meets them alone.
   - It teaches walking, with the range ring visible, and the Voice.
   - It's a 45-second mini Night with a tiny budget, in the input log like any other.
2. **Errands that offer a choice.**
   - From the third errand on, two options ("A Cottage: two more seekers" or "A Peat Cut: warmth"), the same reward
     either way.
   - Claims are automatic: the resources fly to the bar.
3. **Two builders from the start**, so the waits overlap.
4. **Towels:** every errand pays one today, so there were seven in hand before the first Hearth upgrade, and its
   10 minutes took one tap. Proposed: Towels come from the Night's stars and the Pilgrims' Way instead of every
   errand. A Towel then becomes a decision, and the fight pays for the shortcut.
5. The order becomes:
   1. intro;
   2. Woodcutter;
   3. the micro-raid;
   4. Chute;
   5. two errands with choices;
   6. Dusk briefing;
   7. Night 1;
   8. Dawn with a perk;
   9. Hearth 2;
   10. the Pilgrims' Way.

### F. The look (about 5 days, client and art)

1. **Frame the village, not the field.** Day framing follows the built area plus two tiles and grows with the Hearth.
   Today `VillageFraming` fixes it at 22 × 16 tiles. Beyond it, forest and fog instead of empty brown ground.
2. **Golden Mud, as the art spec says.**
   - Day: a warm, low key light, long soft shadows, a cool fill from the sky.
   - Night: warm lights at doors and torches, a big warm pool round the Hearth, the raiders' torches, a moon rim on
     units. At Night, light is how you read the board.
3. **Post-processing (a URP Volume).** Tonemapping, a little more warmth, bloom on fire and lamps only, a soft vignette,
   ambient occlusion for contact shadows.
4. **Ground with something on it.**
   - Mud, grass and gravel blended.
   - Stones along the road edges, tufts, puddles.
   - Junk round every building (crates, cones, the duck), like the intro scene, which is the best-looking shot in the
     playtest.
5. **Units at 1.5–2× their size** next to the buildings, with an outline per side.
6. **A compact HUD.**
   - One row of resource chips: a compact number, with the cap as a thin fill line that turns red when full.
   - Villagers and Comfort as badges on the Cult button.
   - Text at least 13 pt everywhere (cards are 9–10 px today).
   - Build cards with 56 pt icons and colour that means a category.
   - Sheets as bottom sheets with icons in every cost and a button that says why it's disabled ("Need 287 Timber").
7. **New store screenshots** of a close Night with effects and the Prophet mid-skill.

## Determinism

Phase C changes the Night itself. The balance table already carries a version that each raid pins (`2026.10.7`), so:

- the new rules go into a new balance version, `2026.11.1`;
- Nights fought before it keep replaying under `2026.10.7`;
- the boon pick and the early wave call are new `Intervene` inputs, so they live in the input log;
- the demo Night's hash changes, so the Sandbox's expected hash and the `DeterminismProbe` value are updated;
- the server gets the new simulation in the same release as the client.

Phase E's micro-raid and the first-Night budget are base-state changes (`BaseState`, the errand book), and they get a
new errand-book version. Old saves read through the old order, as on 2026-10-16.

## Testing

- Each phase: unit tests in `CultOfComfort.Simulation.Tests` for the rules (stars, boons, call early, errands with
  options, unlock order). PlayMode smoke tests where the client has them.
- Phases C and E: Sandbox bot runs (idle, active, pace) with the results in the commit message.
- After B, C and F: an emulator playtest written up in `docs/playtests/`, like today's.
- On the Pixel 10 before any Play release.

## Decisions for the owner

1. **Stars you earn:** should idle play cap at 1★ (proposed), or 2★?
2. **The first Night:** one building wrecked at most (proposed), or none?
3. **Towels:** from stars and the Pilgrims' Way instead of every errand (proposed), or keep a Towel per errand?
4. **Lands:** merge the Mission House into the Map (proposed), or keep a list screen and drop the Map's own sending?
5. **The micro-raid:** in the first three minutes (proposed), or keep the first fight at the end of the errands?
6. **Order of phases:** A → B → C → D → E → F (proposed). Should the look (F) come earlier, for the store listing?
7. **The release:** ship A–B as an update to the open test while C is built, or hold everything for one release?

## Not in this plan

- Bosses with weak points (after C, once the boon and objective rules are proven).
- Raiders that counter specific defences, an alliance or any social layer (still "not done" from 2026-10-20).
- New buildings or systems. This plan adds none apart from the micro-raid, and removes two buttons.
