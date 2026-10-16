# One thing at a time

- **Date:** 2026-10-16
- **Status:** Owner: "feel free to do what you need to fix it. go for it", after an outside playtest. A new player said she had no idea how to play, the screen is crowded, the errand note takes a big part of the screen, the HUD looks messy, and the models and animations look cheap.

## What a new player sees today

The first build step (`art/previews/intro/coach_1_card.png`) gives three instructions at once: the errand note, a toast ("Build from the drawer below…") and the coach's line with its arrow. Around them are four padlocked side buttons, a padlocked Shop and Cult, and a disabled "Lamps can wait" button. Before any of that, a four-section "How to play" wall opens over the village.

During a Night, the stores (useless in a fight), the wave strip and the skill bar cover about half the screen. The fight happens in a dark strip in between, with raiders about 15 px tall.

## Rules

1. **One voice.** While the coach is pointing at something, it is the only instruction on screen: the errand note and teaching toasts wait.
2. **Nothing locked on show.** A shortcut appears when it opens, with a "New" toast and the red dot, not as a padlock months early. A padlocked shortcut you can't use is clutter.
3. **No reading wall.** The coach teaches by pointing; "How to play" stays behind the ? button and no longer opens by itself.
4. **The Night is for the fight.** The stores fold away during a Night; the wave strip and meters stay.
5. **No developer talk for players.** The Dawn card keeps the stars, the line and the VERIFIED stamp; the hashes and command count move to the settings' debug section.

6. **The coach follows every errand.** Not just the first three: a build card (its tab opens, the drawer scrolls to it), ✓, the building to upgrade, Light the Lamps, the Map or Research shortcut. At the note it is the arrow alone (the note already says "Done! Tap to claim"). Quiet while an upgrade runs, since that can take hours.
7. **Tapping the coach's note hides it until the next step**, never for good. On the Pixel, a tap meant for the errand note beside it switched the coach off, and the player had nothing left to follow.
8. **What you can build comes first.** In each drawer tab, locked cards ("Hearth 2", "Max built") go to the end.
9. **Everything turns.** Square buildings too (a Slurry Chute, a Storage Hut); only fence and wall pieces don't, as they join their neighbours by themselves (the gate still turns). The rule still has two facings (`Rotated`); four facings would need a save field on the server.

## Later (not in this pass)

- The Night camera nearer and brighter units (the "cheap models" complaint is mostly scale and darkness).
- One slimmer resource bar.
