# Cult of Comfort — Play Console, ready to paste (open testing, 0.1.0)

Everything Play Console asks for, in the order it asks. Every sentence must be true of the build being published
(checked against the code on 2026-10-20, see the end). Lengths are checked by `node store-assets/_src/count.mjs`.

Package `com.lechdigital.cultofcomfort` · upload: `client/CultOfComfort.Unity/Builds/Android/CultOfComfort.aab`
(build: see "Release build" below).

---

## 1. Create app

| Field | Answer |
|---|---|
| App name | Cult of Comfort: Tower Defense |
| Default language | English (United States) – en-US |
| App or game | Game |
| Free or paid | Free |
| Declarations | Developer Program Policies ✔ · US export laws ✔ (the user ticks these) |

## 2. Store listing → Main store listing

### App name — 30 / 30

```
Cult of Comfort: Tower Defense
```

Why: "tower defense" is the genre people type; the brand comes first so it still reads as a name.
Alternate if you'd rather lead on building: `Cult of Comfort: Village Defense` is 32, too long;
`Cult of Comfort: Base Builder` (29) is the best second choice.

### Short description — counted by count.mjs

```
Build a cozy medieval village by day. Defend it with towers when raiders come.
```

### Full description — counted by count.mjs

```
Stuck in the year 950, a cynical time traveler does the only sensible thing: builds an absurdly comfortable spa out of junk and basic physics. Your village runs on comfort. The neighbors would like to take it.

Cult of Comfort is a medieval village builder with tower defense. Build and upgrade by day. When the raiders come, hold the road at night.

BUILD A VILLAGE THAT RUNS ON COMFORT
• Bathhouses, beehives, a tavern, a bell tower, woodcutters, peat cuts and a blacksmith.
• Gather Timber, Peat, Scrap and Devotion. They keep coming in while you are away.
• Grow the Hearth through five levels, up to a golden dome.
• Put your cultists to work, and give named villagers the jobs they are best at.
• Big upgrades take hours, the top ones days: a village you come back to, not one you finish in an evening.

TOWER DEFENSE AT NIGHT
• Raids come down the roads into your village, and your towers fight live.
• Place towers anywhere, turn them to face the road, upgrade them, or pack them all up and start again.
• Build a Guardhouse: its wardens man the towers or fight in the street.
• Lay a mud trap or a decoy duck at dusk to turn a hard night.
• Three roads, and different raiders on each. Gate the bridges over the river from Hearth 4.
• Raiders smash what stands by the road. Wrecks look wrecked until you repair them.

YOU DECIDE WHEN TO FIGHT
• Tension rises over time. When it is high enough, a raid is announced with hours of warning.
• Get ready, then light the lamps when it suits you.
• After a night your village is shielded for a while.
• Away when they come? Your towers fight alone, and you get a report of how it went.

A PROPHET, A VALLEY, A WORLD
• Your Prophet fights at night, earns experience, climbs ten levels and picks perks.
• Send missionaries across the world map, and pay tithes to keep the lords calm.
• Your cult shares the world with other players' cults.
• Seasons change the valley and the raids: spring floods a bridge, winter ices the river.

A REASON TO COME BACK
• The Comfort Festival: a theme every day of the week, three rewards a day.
• The Pilgrims' Way: an endless ladder of challenge nights, with a boss every fifth stage.
• Visitors: a pilgrim, a pedlar or a lost sheep turns up through the day.
• Welcome back: what the village made, who moved in and what finished while you were away.

FAIR BY DESIGN
• Every raid is replayed on the server from what you did, so nobody can fake a result.
• No ads.
• Sign in with Google to keep your village on any phone. Without it, you can still play.

Cult of Comfort is in open testing. It is free, and it is still growing: tell us what you think from the Play Store or at andrzej@lechdigital.nl.
```

Keywords it covers without stuffing: tower defense, village builder, base building, medieval, strategy, cozy,
raids, defend, upgrade, offline progress ("keep coming in while you are away").

## 3. Store listing → Graphics

| Asset | File | Size |
|---|---|---|
| App icon | `icon-512.png` | 512×512 PNG (from `art/branding/app_icon.png`, the duck in its tub) |
| Feature graphic | `feature-1024x500.png` | 1024×500 |
| Phone screenshots | `phone/phone-*.png` | 1080×1920, 8 of them (see the plan below) |
| Video | none | optional |

Regenerate: `node store-assets/_src/build.mjs`. Screens come from real phone captures in `_src/shots/`.

### Phone screenshot plan (8, in this order)

| File | Headline / subline | Capture (`_src/shots/…`) |
|---|---|---|
| `phone-1-village.png` | Build a spa *out of junk.* | `village.png`: Hearth 5 village by day |
| `phone-2-night.png` | When the raiders come, *hold the Hearth.* | `night.png`: a Night, the Prophet's torch lit at the Hearth |
| `phone-3-towers.png` | Place, turn and *upgrade towers.* | `towers.png`: placing a Sapling Flinger, range and Turn showing |
| `phone-4-warning.png` | Every raid *is announced.* | `warning.png`: Dusk, "Punitive raid · 7h 59m" |
| `phone-5-world.png` | A world shared *with other cults.* | `world.png`: the lands map |
| `phone-6-people.png` | Villagers with *a talent each.* | `prophet.png`: Cult → People, the notables |
| `phone-7-festival.png` | Seven themed days *every week.* | `festival.png`: the Comfort Festival sheet |
| `phone-8-verified.png` | Every raid replayed *and verified.* | `verified.png`: dawn card, 3 stars, VERIFIED |

Captures are portrait, 1080×2424, from the `coc-shots` emulator (Android 36, Pixel 9 profile, GPU on) running a
non-development APK (`BuildAndroid -noDevelopment`) with the dev key, played up to Hearth 5 on 2026-10-20.

## 4. Store settings

| Field | Answer |
|---|---|
| App category | Game → **Strategy** |
| Tags (pick up to 5 in Console) | Tower defense · Base building · Strategy · Casual · Medieval (or the closest ones Play offers) |
| Email | andrzej@lechdigital.nl |
| Website | https://lechdigital.nl/projects/cult-of-comfort/ |
| Phone | leave empty |
| External marketing | on |

## 5. Policy → App content

### Privacy policy

https://lechdigital.nl/projects/cult-of-comfort/privacy/

### App access

**All functionality is available without special access.** No login needed: the game makes a guest player on
first launch. Google sign-in is optional.

### Ads

**No, my app does not contain ads.** (The merged manifest asks only for INTERNET; no AD_ID permission, no ads SDK.)

### Content rating (IARC questionnaire)

- Category: **Game**.
- Violence: **yes, cartoon/fantasy violence**. Stylised low-poly villagers and raiders fight with towers and
  sticks; nobody bleeds, wrecked buildings smoke. No realistic violence, no gore, no violence against
  real-looking humans in a realistic setting.
- Fear / horror: no. Sexuality / nudity: no (bathhouses are tubs and steam). Language: no swearing.
- Controlled substances: **alcohol reference — the Tavern exists as a building**; no use is shown. Answer yes
  to "references to alcohol" if asked, otherwise no. Tobacco: no. Drugs: no.
- Gambling: no real or simulated gambling; the festival rewards are fixed, not random.
- Crude humour: mild (a duck as a god).
- Religion: **satirical** — "Comfort" is a made-up faith; no real religion is named or mocked.
- User interaction: **no** chat, no user-generated content, no sharing. Players see other cults on the world map;
  the names are made up by the game.
- Shares location: no. Digital purchases: no.

Expected result: PEGI 7 / ESRB Everyone 10+ or similar.

### Target audience and content

- Target age groups: **13–15, 16–17, 18 and over**. Not under 13 (the privacy policy says it is not directed to
  children under 13).
- Could it unintentionally appeal to children? **No** — strategy game, text-heavy, no child-focused marketing.
- Not in Designed for Families.

### Data safety

Data collection and security:

| Question | Answer |
|---|---|
| Does the app collect or share user data? | **Yes**, collects |
| Is all collected data encrypted in transit? | **Yes** (HTTPS only) |
| Can users request deletion? | **Yes** — https://lechdigital.nl/projects/cult-of-comfort/delete-account/ |
| Shared with third parties? | **No** for every type |

Data types (all **collected, not shared, processed not ephemerally**):

| Type | Required? | Purposes |
|---|---|---|
| Personal info → **User IDs** (Google's account identifier, only when signed in) | Optional | App functionality, Account management |
| App info and performance → none | — | — |
| **Device or other IDs** (the random player key the game makes on first launch) | Required | App functionality, Account management, Fraud prevention/security |
| App activity → **App interactions** (the game commands you play, replayed on the server) | Required | App functionality, Fraud prevention/security |

Not collected: name, email, location, contacts, photos, audio, files, messages, health, financial, web browsing,
calendar, advertising ID.

### Account deletion (Data safety asks for a web link)

https://lechdigital.nl/projects/cult-of-comfort/delete-account/ — how to ask, what is deleted, and when.

### Other declarations

- Government app: no. Financial features: none. Health: no. News: no.
- Uses the Advertising ID: **no**.

## 6. Testing → Open testing

1. **Countries:** all, or start with the EU + US + UK.
2. **Create new release** → upload `CultOfComfort.aab` (version code 1, 0.1.0). Use **Play App Signing**
   (Google keeps the app signing key; ours is the upload key).
3. **Release name:** `0.1.0 (1) — first open test`.
4. **Release notes** (en-US, under 500):

```
<en-US>
First open test. Build a village that runs on comfort, then defend it with towers when the raiders come. Sign in with Google in Settings to keep your village on any phone. Everything is still growing, so tell us what feels slow, unclear or unfair.
</en-US>
```

5. Review and roll out.

**New personal developer accounts:** Google asks for a closed test with at least 12 testers for 14 days
before an app can go to production. If Console refuses open testing for the same reason, run the same
release on a **closed** track first, with the same listing.

## 7. After the first upload — Google sign-in on Play builds

Play re-signs the app with **Google's app signing key**, so Google sign-in needs one more OAuth client:

1. Play Console → Test and release → **App integrity** → App signing key certificate → copy the **SHA-1**.
2. Google Cloud (project `cult-of-comfort`) → Google Auth Platform → Clients → **Create client → Android**,
   package `com.lechdigital.cultofcomfort`, that SHA-1. Name it "Cult of Comfort Android (Play)".
3. Optional: a third Android client with the **upload key** SHA-1
   `18:3F:3C:71:AE:D4:BE:84:32:97:53:8B:5B:C8:5B:C7:BA:A9:68:40`, for release builds installed straight from
   the AAB.

Nothing changes in the game or the server: they use the Web client id, which stays the same.

## Release build

```sh
export CULTOFCOMFORT_KEYSTORE_PASS=$(cat ~/keystores/cultofcomfort-upload.pass.txt)
~/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath client/CultOfComfort.Unity -buildTarget Android \
  -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroidRelease -versionCode 1 -versionName 0.1.0
```

The upload key is `~/keystores/cultofcomfort-upload.jks` (alias `upload`), its password in
`~/keystores/cultofcomfort-upload.pass.txt` (mode 600), both outside the repo. **Back both up** (a password
manager is fine): losing the upload key means asking Google for an upload key reset. Each new upload needs a
higher `-versionCode`.

## Claims checked against the code (2026-10-20)

- No ads, no purchases, no analytics: no billing, ads or analytics packages; the Shop is the in-game Market.
- Permissions: INTERNET only; target SDK 36; not debuggable (release AAB).
- Buildings named: Bathhouse, Beehive, Tavern, Bell Tower, Woodcutter's Lean-to, Peat Cut, Blacksmith.
- Hearth 1–5; the level-5 Hearth is a golden dome (spec 2026-10-20 deeper play).
- Towers: place anywhere, rotate, upgrade, pack up all (specs 2026-10-20 defence, roads and facings).
- Guardhouse wardens; mud trap and decoy duck; three raider roads with their own raiders; bridge gates from
  Hearth 4 (specs defence, deeper play, across the river).
- Tension, warning hours, shield after a Night, towers fight alone when away (README, GDD 01).
- Prophet: ten levels, perks; notables (spec deeper play).
- Seasons: spring floods the south bridge, winter ices the river (spec deeper play).
- Comfort Festival (a theme a day, three tiers), Pilgrims' Way (boss every fifth stage), visitors, Welcome back.
- Server replay of every Night (ADR 0002); Google sign-in optional (ADR 0007).
- Shared world with generated cult names (spec shared world; `WorldLedger`, `BotCults`).
