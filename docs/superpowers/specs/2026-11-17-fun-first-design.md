# Fun first: a Night that needs you, one thing at a time, a village you can see

- **Date:** 2026-11-17
- **Status:** Approved with changes (owner, 2026-11-17): decisions below; phase F widened to a remodel
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
the remodel. All of it ships as one release.

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

### F. The remodel: a game that looks good and reads at a glance (about 3 weeks, art and client)

The owner, 2026-11-17: "the game should look way better than it does now, it's almost unreadable … and it's ugly".
This is no longer a polish pass. Phase B already fixes the half of "unreadable" that comes from the camera and the
scale. F remakes the look itself, and it starts with a target, not with models.

**F0. A target the owner picks (2–3 days).**
- Three concept frames of the same moment: a Night at Hearth 2, with the Prophet luring two Levies past a Flinger and
  the Hearth glowing. Painted (AI image generation, as for DeadWeight), in three directions:
  1. **chunky toy diorama:** the current chibi proportions, but bevelled, hand-painted textures, strong rim light;
  2. **storybook painterly:** soft gradients, ink outlines, warm and cold light;
  3. **crisp stylised low-poly:** the Kingshot/Clash look, flat colours, big silhouettes.
- Plus a Day frame of the village in the chosen direction, and the HUD laid over both.
- The owner picks one. It becomes the art spec, replacing the "Muddy Satire" style guide of
  2026-10-10 where they differ. The palette, the 2024 junk in neon and the Prophet's design stay.

**F1. A look slice (about 4 days): one scene, all the way.** Before any asset is remade, one Night at Hearth 2 is
built to the target in Unity:
- the camera and the scale from phase B;
- the lighting: a warm, low key light by day; by Night, warm lights at doors and torches, a big warm pool round the
  Hearth, the raiders' torches and a moon rim on units;
- a URP post-processing Volume: tonemapping, warmth, bloom on fire and lamps only, a soft vignette, ambient occlusion;
- the ground: mud, grass and gravel blended, stones along the road, tufts, puddles, junk round the buildings;
- the Prophet, a Levy, a Zealot, the Hearth, a Flinger and a Cottage remade to the target.

The owner compares before and after on the phone. Only then are the rest of the assets remade.

**F2. Assets, remade to the target (about 2 weeks).**
- Characters first: they carry readability. Bigger heads and hands, one strong colour per side, an outline, and
  animation poses that read from above (the walk, the swing, the hit, the swoon).
- Then the buildings in the order you meet them: Hearth 1–5, the first six buildings, the towers, the wrecks.
- Then nature and props.

The 126 Blender scripts stay the pipeline: a remade asset is a better script, and the build stays one command. Two
helpers, if the slice shows they're worth it:
- AI-generated hero models (the Prophet, the Hearth, the lords), cleaned up and decimated in Blender;
- textures painted from the concept frames.

**F3. Frame the village, not the field.** Day framing follows the built area plus two tiles and grows with the Hearth.
Today `VillageFraming` fixes it at 22 × 16 tiles. Beyond it, forest and fog instead of empty brown ground.

**F4. The HUD in the same style.**
- One row of resource chips: a compact number, with the cap as a thin fill line that turns red when full.
- Villagers and Comfort as badges on the Cult button.
- Text at least 13 pt everywhere (cards are 9–10 px today).
- Build cards with 56 pt icons and a colour per category.
- Sheets as bottom sheets with icons in every cost, and a button that says why it's disabled ("Need 287 Timber").
- Icons re-rendered from the remade models.

**F5. New store screenshots and a new feature graphic,** taken from a close Night with effects and the Prophet
mid-skill.

#### Unreal Engine?

Staying on Unity is recommended:

- **The game's rules are C#.** The deterministic simulation (`CultOfComfort.Simulation`, netstandard 2.1) is the same
  DLL on the phone and on the server, and the server replays every Night to check it (ADR 0002). Unreal is C++: the
  simulation would have to be rewritten and kept bit-identical on both sides, or the server rewritten too.
- **What makes it ugly isn't the engine.** It's the camera, the scale, flat lighting, no post-processing, bare ground,
  and simple models. Unity's URP does all of what F needs on a phone: lights, bloom, ambient occlusion, colour grading,
  outlines.
- **Size and reach.** An Unreal mobile game starts at several hundred MB and favours high-end phones. URP runs at 60 fps
  on the Pixel 10 today, and the APK is 63 MB.
- **The cost.** Porting is months; F is about three weeks. The client's 27,000 lines of Unity code (HUD, views, map,
  intro) would be redone, not improved.

If the look slice (F1) fails to reach the target in Unity, the question comes back with evidence: the same scene
rebuilt in Unreal for comparison. Not before.

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

## Decisions (owner, 2026-11-17)

1. **Stars you earn:** idle play caps at 1★.
2. **The first Night:** one building wrecked at most.
3. **Towels:** from the Night's stars and the Pilgrims' Way, not every errand (the owner left it to this spec).
4. **Lands:** the Mission House merges into the Map (the owner left it to this spec). The Map already shows the places,
   their distance and the lord's seat, and a list of twenty empty rows adds nothing.
5. **The micro-raid:** in the first three minutes.
6. **Order:** A → B → C → D → E → F.
7. **Release:** one release when all of it is done. The open test stays on 0.1.0 until then.
8. **The look:** a full remodel (phase F), starting from a target the owner picks (F0) and a look slice (F1). Staying
   on Unity unless the slice fails.

9. **The art direction: A, the chunky toy diorama, with B's ink outlines** ([`art/concepts/night-a-toy.jpg`](../../../art/concepts/night-a-toy.jpg)),
   with C's readability rules ([`night-c-lowpoly.jpg`](../../../art/concepts/night-c-lowpoly.jpg)):
   - **From A:** bevelled chunky shapes; materials that read (wet mud, worn marble, logs, thatch); soft contact shadows;
     a warm rim light on every character; the big warm pool round the fire.
   - **From C:** saturated team colours (raiders in red with gold trim, never brown on brown), a clean ground under the
     fight, and torches as light sources along the road.
   - **Both frames agree on scale:** characters about twice as big next to the buildings as in the game today, and
     the Prophet's ring.

   - **From B:** a dark cartoon outline round every character, building and prop (the owner: "it looks cartoony because
     of the black frames"). In Unity, an inverted-hull outline: each mesh drawn a second time, slightly larger and
     back-facing, in dark brown. It's cheap on a phone and stays crisp at any zoom. The width is tuned on the device.
     It's thicker for characters than for props, so the fight reads first.

   The final prompts are in [`art/concepts/PROMPTS.md`](../../../art/concepts/PROMPTS.md) (Night and Day).

   **The reference frames** (round 2, `art/concepts/`):
   - [`day-final.jpg`](../../../art/concepts/day-final.jpg): the look. It's the target for the 3D models, materials,
     scale and village layout: a toy diorama, bevelled, readable, alive.
   - [`hearth-final.jpg`](../../../art/concepts/hearth-final.jpg): the Hearth and the people. The car nose-down in the
     glowing pool, its coil and taped cables, the tarp on poles, the worshippers, the cargo-cult copies (a clay cone, a
     carved duck, woad banners), and the Prophet's personality (arms open, beaming). Its flat 2D rendering is not the
     target.
   - [`night-final.jpg`](../../../art/concepts/night-final.jpg): the Night. The torch-lined road; crimson raiders
     against the mud; the pool and the car as the brightest thing on the board; fire baskets throwing warm light; the
     clean ground and the teal ring round the Prophet; the cargo-cult rule kept (one real cone, vest and speaker; clay
     cones, a carved duck and woad banners as copies).

   Where they disagree, the GDD wins: the car is nose-down (as in `hearth-final`, not flat as in `day-final`), and there
   is only one of each real relic (`day-final` has four cones, two trolleys and two tarps).

## Not in this plan

- Bosses with weak points (after C, once the boon and objective rules are proven).
- Raiders that counter specific defences, an alliance or any social layer (still "not done" from 2026-10-20).
- New buildings or systems. This plan adds none apart from the micro-raid, and removes two buttons.
