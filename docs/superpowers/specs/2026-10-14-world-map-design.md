# World Map: the Domain, Missionaries, Offerings, Tithes (slice 1)

- **Date:** 2026-10-14
- **Status:** Approved by the owner ("let's go for world map, and the rest of the functionalities we need")
- **Implements:** GDD 04, single-player first. The shared ~200-player shard is a later slice built on the same rules.

## Why single-player first

The GDD's shard needs other players' influence on the same nodes and a server-side world service. This slice puts one Authority's **Domain** inside each village's `BaseState`. That gives it the existing properties for free:
- deterministic and lazy;
- server-authoritative through `/api/base/sync`;
- replay-safe;
- playable offline from the queue.

Moving to a shared shard later means one change: the nodes' influence moves from "you vs. the Authority's loyalty" to "everyone vs. everyone".

## Rules

**The Marchlands.** One fixed domain, the same for every village. It has 16 nodes on axial hex coordinates around the village at (0, 0), and Lord Wulfric's Keep is the seat, 5 hexes out.

| Type | Count | Offerings per hour at 100 influence | Receptivity | Tension weight |
|---|---|---|---|---|
| Hamlet | 6 | 30 Timber | 0.80 | ×1.0 |
| Village | 5 | 20 Timber, 10 Peat | 0.65 | ×1.0 |
| Market Town | 2 | 8 Scrap | 0.50 | ×1.5 |
| Abbey | 2 | 6 Devotion | 0.35 | ×2.0 |
| Keep (seat) | 1 | — (prestige) | 0.15 | ×3.0 |

Every node has a fixed **squalor**. Muddy places convert more easily: receptivity × (0.5 + squalor / 200).

- **Unlock:** the Map, Missionaries and Tithes shortcuts open at **Hearth 2**.
- **Missionaries:** you can have Hearth level − 1 out at once.
  - Each one is a villager who leaves the village while away, so crews may run short.
  - Sending one costs 25 Timber + 10 Devotion per preaching hour.
  - Travel takes `distance × 20 min` each way. Preaching lasts 1, 2, 4 or 8 hours, with efficiency 1.0 / 1.1 / 1.25 / 1.5.
  - At the end of the preaching, the node gains `12 × hours × receptivity × efficiency` influence, capped at 100. The Authority's loyalty is whatever is left.
  - The missionary walks home, and the villager is back on arrival.
- **Patron:** reaching 40 influence on a node makes you its Patron: +25% offerings and a banner. Becoming Patron gives Tension +8, and preaching at the Keep gives Tension +15.
- **Offerings:** each node pays its rate × your share every hour, plus the Patron bonus. They collect in a purse that holds 8 hours' worth, and you collect it from the map.
- **Decay:** influence drops 2% a day; it's applied at daily domain ticks.
- **Tension:** the rise per hour is the passive rate (0 / 6 / 9 by Hearth level, down from 0 / 8 / 12) plus `Σ influence × weight × 0.02`. Five full hamlets add about +10 per hour. The rate is constant between domain events, so ADR 0004's analytical crossing still holds.
- **Tithe:** pay any mix of resources. Tension drops by 1 per 20 value, where Timber is worth 1, Peat 2, Scrap 2 and Devotion 4. A second tithe within 24 hours works at half strength. If a raid is announced and the tithe brings Tension under Uneasy (40), **the raid is called off**: Wulfric takes the money.

## The map screen

- A parchment overlay drawn in UI Toolkit. Hex nodes are icon buttons connected by roads, with your village at the bottom and the Keep at the top.
- Each node has a ring showing your share, and a banner where you're Patron.
- **Tapping a node** opens its card: type, offerings, your influence, travel time, and "Send a missionary" for 1h / 2h / 4h / 8h with costs and expected influence.
- **Bottom strip:** missionaries on the road ("preaching at Muddlecombe, 1h 20m left"), the offerings purse with Collect, the Tension bar, and Pay a tithe (small / large / buy off the raid).

## Later slices

Research (tech tree eras), the Trolley Market (shop: resource swaps at bad rates), and the Cult panel (villagers, missionaries, Comfort breakdown, relics). Then the shared shard.
