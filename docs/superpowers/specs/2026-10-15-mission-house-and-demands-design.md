# The Mission House and the Lord's demands

- **Date:** 2026-10-15
- **Status:** Approved by the owner, who picked "Mission House" for missionaries and "Lord's demands" for tithes. He had asked, "why is it 3 ways into the same map? shouldnt we have something more interesting?"

## The Mission House: missionaries become people

### Preachers

- **Who they are:** every missionary slot is a preacher with a name, a quirk and a level. The village has one per slot (Hearth level − 1, as before). Preacher *n*'s name and quirk follow from *n*, so the client and the server agree without storing them. Only experience is stored.
- **Experience:** 2 points per hour preached, or 3 for the Tireless.
- **Levels:** 5 levels at 0, 16, 48, 112 and 224 points (8, 24, 56 and 112 hours of preaching). Each level above the first adds 6% to the faith a sermon wins, up to +24% at level 5.
- **Quirk order:** quirks come in a fixed order: Tireless, Thrifty, Lucky, Silver-tongued. The first preacher, the only one at Hearth 2, wins no extra faith, so the early map is no faster than before.

| Quirk | Effect |
|-------|--------|
| Silver-tongued | +15% faith won |
| Thrifty | provisions cost 30% less |
| Tireless | 1.5× experience |
| Lucky | road finds are doubled |

### Sending

- **Choosing who goes:** you pick the preacher. `SendMissionary` and `SendWorldMissionary` carry `PreacherId`; 0 means the first free one, so older clients still work.
- **Busy preachers:** a preacher who is already out can't go.
- **The sermon's bonus** is the tech bonus plus the preacher's level and quirk. On the Marchlands the village applies it. On the shared world the server reads it from the village, as each missionary's `BonusPercent` on the snapshot.

### Road finds and reports

When a preacher gets home, the road may have given something. The outcome is fixed by the missionary's id.

| Chance | Find | Amount (×2 if Lucky) |
|--------|------|------------|
| 40% | footsore, nothing found | — |
| 25% | a lost goat, sold | 8 Timber per hour preached |
| 15% | a grateful widow | 2 Devotion per hour |
| 10% | scrap from a ditch | 3 Scrap per hour |
| 10% | a turnip ambush | +2 experience |

- **Limits:** a find only fills what the stores can hold.
- **Reports:** the last 6 reports are kept and shown in the Mission House.
- **Pace:** finds average about 3 Timber of value per hour preached, while provisions cost 65 per hour. That is a small refund, not a new income.

### The screen

The Missionaries button opens the Mission House:
- a card per preacher, with name, quirk, level and an experience bar;
- what each preacher is doing (at home, walking, preaching, coming home), with a progress bar;
- "Send" (pick a place on the map) and "Show on map";
- the latest road reports.

On the map, a place's card offers "Preach" with a choice of the free preachers.

## The Lord's demands: a living contract instead of a menu

- **The clock:** from Hearth 2 the lord sends demands. The first comes 10 hours after the Hearth reaches level 2. After that, each comes 10 to 14 hours after the last one was settled (the gap is fixed by the demand's id). An older village gets its first demand 10 hours after the save's latest moment.
- **What he wants:** one resource, or a mix, scaled by Hearth level, with a reason ("for my new stables"). You have **6 hours** to deliver.
- **Deliver:** you pay, and his temper drops by twice what the same tithe would buy. That drop is never halved by a recent tithe. If a raid was announced and Tension falls under Uneasy, the raid is called off.
- **Miss it:** his temper jumps by 15 when the time is up.
- **Voluntary tithes** stay on his seat's card as before, for when there's no demand or you want more calm.
- **Where it shows:**
  - the map's top panel has the demand line with a Deliver button;
  - the Tithes button gets a badge while a demand is open;
  - the seat's card shows the demand first;
  - a toast announces each new demand and each missed one;
  - after you deliver, a cart goes to the seat.
- **Determinism:** the demand clock runs from moments both the client and the server know: a construction's end, or a delivery's or deadline's time. It never runs from when someone happened to look.
