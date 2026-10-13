# 05 — Story and Relics (The Boot)

## The premise

The Prophet is a self-taught inventor from the Netherlands, 2024, who should not be allowed tools. On a Thursday, before lunch, he turns his teal hatchback into a time machine: a copper coil bolted to the roof, cables to the bonnet, gaffer tape where the engineering should be. It works. He arrives in **the Marchlands, 947 AD**, nose-first in the pool of a ruined Roman bath.

He did not build a way back. There is no electricity in 947, so there is no charging the car, ever. His smartwatch shows 3%, then 2%. The locals watch a shining cart fall from the sky, and a man in pink flip-flops climbs out with an umbrella. They decide he is a prophet. He decides not to correct them.

He has nothing but what he had on him and in the car. That limit is the whole joke, and it is a rule for the art.

## The car

The time machine is a landmark that never moves. It stays nose-down in the Roman pool at the centre of the Hearth at every Hearth level (`lib/car.py: crashed_car`). The pool steams because of it. A standalone, animatable copy (`time_machine`, with parts `Car` and `Boot`) plays in the intro.

## The Boot: every 2024 object in the world

| Relic | Where it came from | What the Marchlands think it is | Can become (later) |
|---|---|---|---|
| Black umbrella | In his hand | A staff of office | His melee swing (in the game) |
| Sunglasses, neon pink flip-flops, orange belt bag | On him | Holy vestments | Cosmetics / Prophet looks |
| Smartwatch (flat since 947) | On his wrist | A bracelet that once spoke | A countdown gag; morale event |
| Blue tarp | Covered the time machine in the garage | **The Tarp**, the first roof of the faith | Hearth's first roof (in the game) |
| Traffic cone | Rolling around in the boot | **The Holy Cone** | Path marker / taunt: raiders must walk round it |
| Rubber duck | Dashboard | **The Duck**, minor deity of bathing | Comfort / Devotion icon; the Chapel idol is a wooden copy |
| Shopping trolley | Still in the boot from the last shop | **The Holy Trolley** | The Shop / Tithes cart |
| Beach bag (towels, sun cream) | Back seat | Where the faith's bathing rites come from | Bathhouse lore |
| Hi-vis vest | Legally required car kit | Armour of Being Seen | Buff: worn by a champion, raiders' torches dazzle |
| Warning triangle | Legally required car kit | Holy Sign of Caution | Gate sigil; slows raiders at a gate |
| Laser pointer | Cat toy in the glovebox | **The Finger of God** | Skill: a red dot blinds or distracts one raider |
| Bluetooth speaker (87%) | Door pocket | **The Voice from the Box** | The Phantom Voice skill gets a real source; battery = charges |
| Magnifying glass | For a manual that did not exist | **Sun-fire** | Skill: on a sunny day, lights a raider's cart or thatch |
| Wind-up dynamo torch | Emergency kit | **Light without fire** | Night: a moving light that needs a cultist cranking it |

New relics need the owner's approval. A relic should be something a person could plausibly have in a car, and it should be funny when someone from 947 reveres it.

## The cargo-cult rule (art)

There is **one** of each real relic. Everything else in the village is a **copy** the faithful made in medieval materials, so the 2024 colours stay rare and crisp ("2024 things are perfect"):

| The real thing (crisp 2024 swatch) | The copies (knock-off swatch) |
|---|---|
| Cone (`cone_orange`) | Fired clay cones, `ochre_clay`, never quite straight (`clay_cone`) |
| Tarp (`tarp_blue`) | Woad-dyed cloth, sashes, robes, roof patches (`woad`) |
| Duck (`hivis_yellow` plastic) | Carved wooden ducks painted with weld (`weld`, `rubber_duck(relic=False)`) |
| Flip-flops (`flipflop_pink`) | Bast sandals dyed with madder (`madder`) |
| Hi-vis vest | Weld-dyed tabards with a linen "reflective" strip (`weld_vest`) |
| Shopping trolley | Wicker trolleys on wooden wheels (`wicker_trolley`) |
| Garden chair | Wicker chairs |

Upgrading a building makes its copies bigger and more ambitious. The Chapel of Mild Warmth is crowned by a giant carved duck, the Duck Ascendant.

## The intro (first launch)

About 40 seconds and skippable. A tap moves on to the next shot. It is staged in the engine with the game's own models, and its captions are on paper notes (`IntroScript`, `IntroDirector`):

1. **Garage, 2024.** "Somewhere in the Netherlands, 2024. A garage. A man who should not be allowed tools."
2. **The machine.** "He turned his hatchback into a time machine. On a Thursday. Before lunch."
3. **The jump.** A white flash, and the car is gone. "He did not build a way back."
4. **The Marchlands.** "The Marchlands, 947 AD. Mud. More mud. A Roman bath nobody has used in five hundred years."
5. **The landing.** The car falls out of the sky into the pool. "Until now."
6. **No sockets.** He climbs out and holds up his watch. "No sockets. No signal. Battery: 3%… 2%…" Locals peek out of the bushes.
7. **The Boot.** Relics fly out of the boot. "Everything he owned fit in the boot. Everything in the boot is now holy."
8. **The cult.** The locals kneel. "The locals decided he was a prophet. He decided not to correct them."
9. **Title.** "Cult of Comfort: keep the faithful warm. Keep the raiders wet."

After the intro, the first errands become a guided tutorial: a hand points at the next thing to tap until the player has built, collected and claimed once.
