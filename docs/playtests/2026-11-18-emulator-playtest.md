# Playtest on the emulator, 2026-11-18 (fun first, phase B, at 612c44d)

A fresh install played from the intro to the first Night, then stage 1 of the Pilgrims' Way. Pixel 9 emulator (x86_64,
Android 16, 3 GB, 2 cores, host GPU), development build, portrait only (the emulator ignored the rotation). Screenshots
are in [2026-11-18/](2026-11-18/), numbered in play order.

The question was phase B's: can you read the Night now?

## In one paragraph

Better, and in the places B aimed at. You find the Prophet at once (the teal ring and the outline), the camera goes where
he goes, the skills sit under the thumb and say when they're back, and the board gets the screen. What still makes the
Night hard to read is the top: the wave line and the two meters cover the band of board where the raiders come in and
the first buildings stand, so the edge markers mostly point at things that are only behind the HUD. And the first Night
is still over in about fifty seconds, all three waves on the road inside fifteen, six buildings wrecked: that's phase C.

## What phase B fixed

- **The Prophet is found at a glance** ([04](2026-11-18/04-night1-follow-ring.jpg)): the teal ring, the dark outline and
  his size. The camera follows him softly; walking him down the board brings the board along.
- **The skill panel** ([03](2026-11-18/03-night1-start.jpg)): no parchment band, three round notes at the bottom right,
  Hot Stones under the thumb. The cooldown ring fills gold and reads well ("Voice 3s" with an almost full ring); the
  ready pop shows.
- **The meters** are told apart (orange Hearth, teal Prophet).
- **The tally** "· 6 driven off" fits on the wave line in portrait ([06](2026-11-18/06-night1-voice-tip.jpg)).
- **Damage numbers and SMASH!** show over the fight ([05](2026-11-18/05-night1-stones-tip-over-fight.jpg)).
- **The roster card** reads well ([02](2026-11-18/02-roster-card.jpg)).

## Found, and fixed the same day (b7454dc)

| What | Why | Fix |
|---|---|---|
| The first-use tips covered the fight, top middle ([05](2026-11-18/05-night1-stones-tip-over-fight.jpg), [06](2026-11-18/06-night1-voice-tip.jpg)) | `.middle` aligns to the top, so `.night-phase .centre { justify-content: flex-end }` did nothing; the tips also showed the moment the skill was used | The centre stretches; a tip waits until no raider is on the road, and never comes over Dawn |
| The Hearth's "A boar rammed the Hearth" notes sat over the board too ([07](2026-11-18/07-night1-hearth-announcement.jpg)) | Same | Same: at the bottom now |
| The round notes were rounded squares on the device, plain to see while cooling ([08](2026-11-18/08-night1-cooling-note.jpg), [12](2026-11-18/12-notes-zoom.png)) | `border-radius: 50%` came out as a much smaller radius; the gold ring hid it in the Editor captures | Radii in px |
| "New raiders! ›" lit up on the very first screen, beside the coach's first arrow ([01](2026-11-18/01-first-screen-new-raiders.jpg)) | One thing at a time | Quiet ("Who's coming ›") while the coach talks |
| The card said "Tonight: locals" at Calm, hours before a Night | Wording | "Next Night: locals" |

Not seen on the device since the fixes: the next playtest should check the tips at the bottom and the round notes.

## Still open, then fixed the same day

1. **The top HUD covered the fight's entrance** ([09](2026-11-18/09-pilgrims-chevron-under-hud.jpg)): the chevron sat
   mid-board pointing at raiders half seen through the HUD, and the hit pulse was a faint line under the meters.
   **Fixed:** in portrait the two meters share one row under the wave line, with thinner tracks; the top ends about a
   tenth of the screen higher. The bubbles keep above an announcement at the bottom instead of being pushed under it
   onto the skills.
2. **Half a screen of empty forest under the Hearth** ([03](2026-11-18/03-night1-start.jpg)). **Fixed:** the Night camera
   puts the room the stage doesn't fill on the side the raiders come from (`CameraRig.LeanShift`), not half behind the
   Hearth. At Hearth 2 the stage fills the height and nothing moves; check the first Night on the device.
3. **The first Night is still a rush** (phase C): wave 3 of 3 on the road about fifteen seconds in, the Night over in
   about fifty, all six buildings wrecked. Open.
4. **The Dawn card after the Pilgrims' Way** ([11](2026-11-18/11-pilgrims-dawn-card.jpg)) showed the first Night's card.
   Not the stage's report: the card shows as soon as the Night ends, but its content comes only when the replay and
   the village's check are done (several seconds on the emulator). Until then it showed the last card, with a live
   *Back to Dusk*: a tap on it after the first Night dismissed a card that hadn't been filled in yet, and the screenshot
   after the stage caught the first Night's card seconds before the stage's own arrived (logged at 14:30:07).
   **Fixed:** the card waits for its content ("Dawn. Counting heads…" meanwhile). A PlayMode test plays a Night, then
   the big button's stage, and checks the stage's card.
5. **A villager's bark over the Dawn card** ([11](2026-11-18/11-pilgrims-dawn-card.jpg)). **Fixed:** no barks at Dawn.
6. **At Dawn the camera stayed where the Prophet ended.** **Fixed:** Dawn frames the village behind the card.
7. **The ✓ ✗ Turn buttons covered the Hearth.** **Fixed:** they go under the ghost, else over it, else beside it,
   whichever covers neither the ghost nor the Hearth (`HudView.GhostActionsAt`).
8. **The development tools** only open for an account the server marks as a developer. Left as it is: the server refuses
   the debug commands for anyone else, so showing the buttons in a development build wouldn't help. A playtest account
   marked as a developer would.

## Setting up the playtest (Linux host)

As on [2026-11-17](2026-11-17-emulator-playtest.md#setting-up-the-playtest-linux-host), and:

- Start the emulator with `-gpu host`. With `swiftshader_indirect` the game's shaders don't link ("Fragment shader
  active uniforms exceed GL_MAX_FRAGMENT_UNIFORM_VECTORS") and the screen stays black.
- `-memory 3072 -cores 2 -no-window -no-audio -no-snapshot` keeps it light; drive it with `adb shell input tap` and
  `adb exec-out screencap`. Close it with `adb emu kill` when done.
- The emulator build rewrites `ProjectSettings.asset` (x86_64, IL2CPP settings): revert it before committing.
- A launch right after boot can be closed by the system: launch again once the home screen has settled.
