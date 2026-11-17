# Design review: HUD, screens and the Night (2026-11-17)

A UX/UI and game-design review of the [emulator playtest](2026-11-17-emulator-playtest.md). It is based on its 35
screenshots, the store screenshots and the HUD code. **[V]** means verified in a screenshot or the code; **[I]** means
inferred. Screenshot numbers refer to [2026-11-17/](2026-11-17/).

## 1. Verdict

1. **Fun: not yet.** The fantasy, the writing and the sticky-note voice are strong. But the first 15 minutes have no
   decisions, and the Night plays itself (passive play wins 2★).
2. **Readable: the frame is, the battlefield isn't.** Tiny units, no visible Prophet, no ranges, tips over the fight.
   The menus are walls of serif text on parchment.
3. **The biggest problem.** The core loop (the Night) is short, unreadable and doesn't need the player. Then it's locked
   for 12 h, while six text-heavy meta screens open at once to fill the gap. The game front-loads its spreadsheet and
   hides its toy.
4. **The second problem: scale and camera.** The village is about 20% of the screen in a large empty brown field, so
   nothing reads as "my place".
5. **The good news.** Most fixes are small or medium. The pieces exist (Pilgrims' Way, aim rings, skill notes); they're
   gated or laid out wrong.

## 2. The HUD, element by element

**Top bar (portrait, pills, ?, settings)** [V 02, 05, 20, store village.png]
- *Works:* the portrait with the Hearth badge is clear. Settings and ? sit out of thumb reach, which is right for rare
  actions.
- *Doesn't:*
  - Five pills and the villagers note wrap to 2–3 rows and take the top ~18% of the screen.
  - The layout jumps: villagers sit on row 2 in one shot and row 3 in another.
  - `13/3k` and `22087/25.5k` (`HudView.SetPill` / `Short`) mix two number styles, and the cap is noise most of the
    time.
  - Comfort (the duck pill) has no label.
- *Fix:*
  - One row of four resource chips (~64×28 pt) with a compact value (`13`, `22.1k`).
  - The cap becomes a thin fill line under the number that turns red when full (the `.full` class exists).
  - Villagers and Comfort become badges on the Cult button.
  - The top band should end at about 10% of the screen height.

**Tension strip** [V 02, 14, 21; `HudView.cs` ~637]
- *Works:* the hazard tape is a great "man from 2024" signifier. "Shielded · 11h 56m · Next: the Levy, north road" is
  useful.
- *Doesn't:*
  - In every Day shot the tape looks fully lit, even with the Bishop's temper at 0/150 (22, 27), so it doesn't read as
    a meter [I].
  - Yellow marker text on mid-green trees at 12–14 pt.
  - "Summer — Calm" leads the line, and it isn't information.
- *Fix:*
  - Make it a real meter: an empty grey track filling with tape, the lord's face token riding the edge.
  - **Tapping the strip opens the lord's hall**, which removes a side button.
  - Put a dark plate behind the text.

**Side shortcuts (six)** [V 20, 21; `GameController.RefreshMap` ~755–763]
- *Doesn't:*
  - All six land on one refresh at Hearth 2, each with its own "New: …" toast.
  - 42 pt circles with 10 pt labels sit over the build area and cover the village's edges (21).
  - "Missionaries" (button), "Mission House" (sheet), "preacher" (rows) and "missionary" (Map help) are four names for
    one thing.
  - "Tithes" opens "Cathedral of St. Sludgeric", while the locked note says Wulfric.
- *Fix:* at most three side buttons on the left; the right column is for timed events (see section 3).

**Errand note** [V 02–07, 14, 21, 35]
- *Works:* the sticky note with the bouncing arrow is clear.
- *Doesn't:*
  - "Done! Tap to claim…" sits at about 25% of the screen height, out of thumb reach, and gets truncated (35).
  - It stacks with toasts.
  - It survives into the late game: store warning.png shows "Tap the Cottage card" at Hearth 5.
- *Fix:*
  - A fixed slot just above the bottom bar.
  - Auto-claim, with resources flying to the chips.
  - Never truncate.
  - End coach steps at Hearth 3 or once done.

**Build drawer** [V 02–08]
- *Works:* sticky-note cards in categories, a clear selected state, red cost when unaffordable.
- *Doesn't:*
  - Card text is 9–10 px (`.card-name` 10px; `.card-cost` / `.card-time` 9px, `Hud.uss` ~232–235), and icons are 36 pt.
  - "Max built" and "Hearth 2" are faint red on pastel.
  - Note colours cycle per card, so colour means nothing.
  - Three cards are visible per tab with no hint that the row scrolls.
- *Fix:*
  - 56 pt icons, 13 pt names, costs as resource icons with numbers.
  - Give colour a meaning: production, homes, defence, special.
  - Show the next card peeking in at the edge.

**Ghost ✓ / ✗ / Turn** [V 03, 05–07]
- *Doesn't:* the buttons cover the tiles you're placing on (06 covers the Hearth).
- *Fix:* place them below the ghost, offset away from it. Tint the footprint green or red instead of explaining
  placement in text.

**The Lamps button (the main CTA)** [V 02, 08, 14, store village.png]
- *Works:* big, central, within thumb reach.
- *Doesn't:* most of its life it's disabled ("Lamps can wait · build first", even at Hearth 5) or a dead 12 h clock.
- *Fix:* make it contextual:
  - while shielded: **"Walk the Pilgrims' Way · Stage N"**, with the shield timer small above it;
  - during a warning: "Face the raid · 7h 59m";
  - when ready: "Light the Lamps".

**Night skill panel** [V 09–12, 31, 33, store night.png]
- *Doesn't:*
  - The speed toggle is styled as a skill.
  - Cooldowns show only as text plus opacity: no radial and no ready pop.
  - Passive relics ("Laser · auto", "Torch · auto", "Sun's down") take a whole row (night.png: ~15% of the screen).
  - The movement hint is permanent.
- *Fix:*
  - 3–4 active skills as 72 pt round notes in an arc, bottom right, with radial cooldowns.
  - Relics become 28 pt chips with ring timers.
  - Speed toggle top right.
  - Drop the hint after Night 2.
  - The panel should take no more than 18% of the screen height.

**Health bars** [V 09–12, 33]
- *Doesn't:*
  - The Hearth and the Prophet use the same hazard pattern.
  - The Hearth reads 100 while buildings are wrecked; building damage isn't in the HUD.
  - No enemy health is visible at this zoom.
- *Fix:*
  - Hearth: warm orange with its icon. Prophet: a ring round the portrait.
  - World-space bars over damaged enemies and buildings.
  - A red edge pulse and an off-screen arrow when a building is hit.

**Cards and sheets** [V 13, 16–19, 25–30]
- *Doesn't:*
  - Every sheet is a full-screen parchment list with small greyed buttons.
  - No icons in costs, and no reason given for a disabled button (Research 28, Preach 26).
  - The Prophet sheet is 80% empty (19).
  - The Hearth card doesn't say what the upgrade unlocks; the Cult sheet does (18).
- *Fix:*
  - One sheet template: a header with art, cost chips (green or red, with icons), and an orange primary button that says
    why it's disabled ("Need 287 Timber").
  - Bottom sheets at ~70% height, so the village stays visible.
  - The unlock preview goes on the Hearth card.

**Dawn card** [V 13, 34]
- *Doesn't:*
  - "VERIFIED" is the centrepiece and means nothing to a player.
  - "6 reached the Hearth" reads as failure next to 2★.
  - Settings shows "Deterministic: OK · 1ddcdf…".
  - **Tap-through:** `GestureRecognizer` checks `isOverUi` only when a gesture starts [V], so a tap that began before
    the card appeared falls through to the world (34).
- *Fix:*
  - Big stars, a loot row, Prophet XP, and "Next: Pilgrims' Way" as a second button. The stamp goes to debug builds
    only.
  - Swallow gestures that straddle a modal opening, and block world taps for 0.3 s after a modal shows.

## 3. Redundant and overlapping features

| Screen | Does | Overlaps | Recommendation |
|---|---|---|---|
| Map (22–24) | Places; tap to send a missionary; Pay a tithe; Collect; minimap; a house icon **and** a Home button | Mission House, Tithes | **Keep as "Lands".** Merge the Mission House into it; drop one of the two home buttons; open on the nearest reachable place (the first tap gave "15h 20m, too far") |
| Mission House (25–26) | Preachers; the same places as a list (all 0%); Preach 1/2/4/8 h; Show opens the Map | Map | **Merge** as a Preachers tray inside Lands: 3–5 recommended rows, not 20+ |
| Tithes / lord's hall (27) | Temper, demand, tithes, "Show his seat on the map" | Map's Pay a tithe; the Tension strip | **Move** behind a tap on the Tension strip, plus the lord's crest on the Map |
| Research (28) | Studies in four rooms | Hearth card's "13 new studies" | Keep |
| Pilgrims' Way (29) | Challenge Nights any time, alms box | Fills the shield gap | **Promote** to the CTA while shielded |
| Festival (30) | Daily theme points | Scores the other systems | Keep, in a right-hand events column with its timer; unlock on day 2 |
| Shop (17) | Resource swaps | none | Keep; call it what it is (Trolley or Trade) |
| Cult sheet (18) | Hearth, People, Comfort, Relics, Errand | Hearth card, errand note | Keep People, Comfort and Relics; the Hearth info goes to the Hearth card; cut the Errand tab |
| Prophet sheet (19) | Level, XP, empty perks | none | Keep, but about the Night: skills, ranges, perk preview |
| Hearth card (16, 20) | Upgrade | Cult sheet's Hearth tab | Absorb the unlock preview |

**Navigation: 8 entry points instead of 12.**
- **Top:** portrait (Prophet), resource chips, settings. The Tension strip opens the lord.
- **Left:** **Lands** (map, preachers, offerings) and **Study** (research).
- **Right (timed):** **Pilgrims' Way** with a stage badge, and **Festival** with its countdown.
- **Bottom:** Shop · contextual CTA · Cult, with Build above.

**Unlock order:** one new thing per session, each brought in by an errand rather than six toasts.
- Pilgrims' Way right after Night 1.
- Research at Hearth 2.
- Lands once a place is in reach.
- Tithes on the strip when the temper first rises.
- Festival on day 2.

## 4. The Night

**As it is [V].**
- Night 1 is 3 waves of a handful of units and lasts about a minute. The GDD targets 3–5 waves of ~60 s
  (`docs/gdd/01-core-loop.md`).
- The camera stays at the Day distance. The Prophet is a brown dot.
- Tips and barks appear where raiders enter.
- In the Pilgrims' Night the village is the top-right ~15% of the screen and the rest is dark forest (`NightBounds`
  frames the last 3/5 of a long road [I]).
- Night 1 wrecked nearly everything and still scored 2★.

**Camera**
- Frame the action: the Hearth, the Prophet, and the 6 road tiles in front of the defences.
- Units at least 40 px tall at 1080 px wide.
- A 45–50° pitch, soft-following the Prophet with a dead zone.
- Off-screen arrows for entering raiders and buildings under attack.
- Pinch out for an overview that snaps back.

**Readability**
- **The Prophet:**
  - a bright teal ground ring, a 1.3× scale bump and an outline;
  - his **reach always drawn**: umbrella reach solid, lure radius dashed, the Hot Stones throw range faint whenever
    Stones is ready. Today the pebble ring shows only while aiming (`GameController.cs` ~2035), so "He can't throw that
    far" disappears as a message.
- **Enemies:** red outlines, health bars when damaged, and a type icon over each group's leader.
- **Tips:** never during a wave. Brief the next wave in the gap, as a sticky note at the bottom; the scouting lines
  belong on a roster card at Dusk (already in the GDD).
- **Feedback:** damage numbers, "CRIT!" when a Flinger hits a muddied raider, 50 ms hit-stop on Hot Stones, screen shake
  when a building breaks, "+Scrap" on every kill.

**Agency: a decision every 10–15 seconds**
1. **Stars that need play.** 3★ needs the Hearth at 80% or more *and* an objective shown before the Night ("hit 3 with
   one Hot Stone", "the Prophet never swoons", "no building wrecked"). Idle play and auto-resolve cap at 1★.
2. **A boon between waves.** Three sticky notes, pick one:
   - "Duct-tape the chute" (+50% fire rate this wave);
   - "Bluetooth at full volume" (Voice cooldown −50%);
   - "Hand out umbrellas" (villagers repair).
   A 2-second decision, on brand.
3. **Lure to the kill zone as the signature move.** A faint path line when a raider is lured; tower ranges light up when
   the Prophet stands within them.
4. **Call the next wave early** for +10% loot.
5. **A boss each Night,** with a weak-point mechanic.

**Pacing**
- Night 1 should be about 2.5 minutes: three 45 s waves with 10 s gaps, scripted so the towers visibly kill and *one*
  building is wrecked, which teaches Repair.
- One more Night: the Dawn card offers "Pilgrims' Way Stage 1 · Walk it now", and the Prophet's XP fills towards a
  perk choice. Move the first perk to level 2, right after Night 1.

## 5. The first session

**As it is [V 01–21].**
1. The intro.
2. Seven errands of card, drag, ✓, wait 30 s–2 min, claim.
3. "Light the Lamps (early)".
4. A 1-minute Night you can't read, ending 2★ with nearly everything wrecked.
5. "VERIFIED".
6. "Repair all" and "Shielded 11h 58m".
7. A 10-minute Hearth upgrade.
8. Six buttons and six toasts at Hearth 2. The first Map tap is "too far", and Preach and Research are all disabled for
   lack of Timber.

**As it should be: a decision or a payoff every minute.**
- **0:00–0:40.** The intro, then the camera close on the junk camp, pulling back to the valley.
- **0:40–2:00.** Build the Woodcutter (5 s). A villager walks to work; the first collect is a floating icon.
- **2:00–3:30.** **A micro-raid:** four nosy locals, the Prophet alone. Teach walking (range ring visible) and one
  skill. A 45 s win.
- **3:30–6:00.** Scrap, then the Slurry Chute. Errands offer **two options**. Two builders work in parallel, and claims
  are automatic.
- **6:00–9:00.** A Dusk briefing (roster, road, the 3★ objective), then Night 1: 2.5 min, towers matter, one boon pick,
  one wreck.
- **9:00–10:00.** Dawn: stars and loot, Prophet level 2, **choose one of two perks**, one-tap repair.
- **10:00–12:00.** The Hearth upgrade (fast the first time), with a preview of what level 2 adds.
- **12:00–15:00.** The CTA becomes "Walk the Pilgrims' Way". Stage 1 is won with 3★ and the alms box starts. The
  session ends on a timer the player chose: a study started, a preacher sent to a 20-minute village.

## 6. Visual direction, in order

1. **Camera and scale (the biggest win).** `VillageFraming` (`HomeWidth = 22`, `HomeHeight = 16`) frames far more open
   land than the village.
   - Frame the built area plus 2 tiles, growing with the Hearth; fog or forest the rest.
   - A 45–50° pitch and a slightly longer lens, so buildings show their fronts, not roof plans.
2. **Lighting ("Golden Mud").**
   - Day is flat grey-green: use a warm low key light (~4500 K, ~35°), long soft shadows and a cool sky fill.
   - Night is just dark: add warm lights at doors and torches, a moon rim, raiders' torches, and a big warm pool round
     the Hearth. In tower defence, light is readability.
3. **Post-processing (URP Volume).** Tonemapping, warmer saturation, bloom on fires and lamps only (threshold ~1.1), a
   soft vignette, SSAO for contact shadows.
4. **Ground.** Blend mud, grass and gravel. Raised road edges with stones, tufts, puddles, and junk clutter round each
   building: crates, the duck, cones, as in the intro (01, the best-looking shot of the set).
5. **Units.** 1.5–2× larger relative to buildings, chunky silhouettes, team outlines, squash-and-stretch.
6. **UI polish.**
   - Resource icons in every cost.
   - One orange primary button style, with disabled states that say why.
   - Card art on sheets.
   - At least 13 pt everywhere.
   - Serif for the world, marker for the Prophet.
7. **Store screenshots.** Today: a Hearth 5 save with 25k of everything, a tutorial prompt, and "Lamps can wait".
   Capture a close Night with effects and the Prophet mid-skill.

## 7. Top 10 changes, by impact ÷ effort

| # | Change | Effort | Files |
|---|---|---|---|
| 1 | The CTA is "Walk the Pilgrims' Way" while shielded; the Dawn card offers the next stage | S | `GameController.cs`, `Hud/HudView.cs`, `Map/FeatureSheets.cs` |
| 2 | Stagger the unlocks (Pilgrims after Night 1, Research at Hearth 2, Lands when in reach, Tithes on the first temper, Festival on day 2), one toast at a time | S | `GameController.RefreshMap`, `OpenFeature`, `Hud/LockedFeatures.cs` |
| 3 | No engineering text: VERIFIED and the hash only in debug builds; Dawn shows stars, loot and XP | S | `Hud/HudView.cs`, `Hud.uss` |
| 4 | Fix the tap-through after modals; stop the coach leaking into the late game | S | `Input/GestureRecognizer.cs`, `Hud/HudView.IsOverUi`, `Tutorial/*` |
| 5 | Night readability: Prophet ring, reach and Hot Stones rings always on, enemy health bars, off-screen arrows, tips only between waves | M | `Views/BattleView.cs`, `Views/RaiderStatus.cs`, `Hud/TipBook.cs`, `Hud/HudView.cs` |
| 6 | Night camera: closer, 45–50°, following the Prophet; fix the Pilgrims' framing | M | `Views/VillageFraming.cs`, `Views/CameraRig.cs` |
| 7 | One world screen: the Mission House into the Map (Lands), the strip opens the lord's hall, the Missionaries and Tithes buttons and the second Home button removed, one name per thing | M | `Map/MissionHouse.cs`, `Map/LordHall.cs`, `Map/WorldMapPresenter.cs`, `Map/WorldMapView.cs`, `Hud/HudView.cs` |
| 8 | A compact HUD: one row of resource chips with fill caps, a skill arc with radial cooldowns, relic chips, at least 13 pt text | S–M | `Hud/HudView.cs`, `Resources/UI/Hud.uss` |
| 9 | A new first session: a micro-raid at ~2 min, parallel builders, errands with two options, auto-claim, a scripted 2.5-min Night 1 with one wreck, a perk at Prophet level 2 | M–L | `Tutorial/*`, `Hud/ErrandNote.cs`, balance and raid budget in `src/CultOfComfort.Simulation` |
| 10 | Night agency: stars that need play, a boon between waves, call early, a boss, Nights towards 3–5 min; then the lighting and post pass | L | `src/CultOfComfort.Simulation/Combat/Battle.cs` (deterministic: server and replays too), `Hud/HudView.cs`, `Views/NightMood.cs`, URP Volume |

1–4 are about a day each and fix the worst moments of the first session. 5–6 make the core legible, which 10 needs
before it can make the core fun. 7–8 are the "Kingshot-readable" pass. 9–10 are the design work the game lives or dies
on.
