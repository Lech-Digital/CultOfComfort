# 02 — Economy & Resource Sinks

## Primary Resources

| Resource | Fiction | Role | Sources | Main sinks |
|---|---|---|---|---|
| **Timber** | Wood, wattle, daub | Bulk construction; never the bottleneck after Hearth 3 | Woodcutter, Sawmill, Hamlet nodes | Buildings, walls, missionary provisions |
| **Scrap** | Bent nails, cracked cauldrons, horseshoes, one heroically salvaged zipper | **Gating resource** for mechanisms & towers | Midden Sorter, raid salvage, Market Town nodes | Towers, upgrades, research, repairs |
| **Peat** | Fuel — no electricity, only things that burn | **Upkeep currency**: continuous drain for heat & steam | Peat Cut, Biogas Digester | Heating (hourly), steam towers (per Night), smelting |
| **Devotion** | Cultists' belief this man is a prophet rather than just tired | Soft "people" currency | Comfort-driven passive income, Abbey nodes | Missionaries, research, Hearth upgrades, Tithes |

### Derived Stats (not spendable)

- **Comfort** = Σ(building comfort) − Squalor.
  - Scales Devotion: `devotionRate = base × (1 + Comfort / 200)`.
  - Raises Tension gain via Envy (see [04](04-world-map-and-tension.md)).
- **Squalor** — produced by dirty buildings (Midden Sorter, Biogas Digester); placement puzzle.

### Premium Currency

- **Silver Pennies** — QoL and cosmetics only. **Never** convertible into the four core resources.

> **Slice 1 (2026-10-11):** Hearth 1–3 costs and caps as below; building costs, rates and limits are in `BuildingTable` (`village-2026.10.1`, spec `docs/superpowers/specs/2026-10-11-village-slice-1-design.md`). Night salvage: 40 + 40 × stars Scrap.

## Hearth Progression (Levels 1–5)

| Lvl | Timber | Scrap | Peat | Devotion | Build time | Storage cap (each) | Max cultists | Unlocks |
|---|---|---|---|---|---|---|---|---|
| 1 | — | — | — | — | — | 1,000 | 5 | Woodcutter, Peat Cut, Slurry Chute, 1 missionary, map radius 1 |
| 2 | 600 | 50 | — | — | 10m | 2,500 | 8 | Midden Sorter, Sapling Flinger, Walls T1, **raids enabled** (new-player shield ends) |
| 3 | 2,000 | 300 | 200 | 100 | 1h | 6,000 | 12 | Research table, 2nd missionary, map radius 2, Vault (20% protected) |
| 4 | 6,000 | 1,200 | 800 | 500 | 4h | 15,000 | 16 | **Comfort Era** research branch, Bathhouse, 3rd missionary, Tithes |
| 5 | 15,000 | 4,000 | 2,500 | 2,000 | 10h | 35,000 | 20 | Pressure Sluice, Bishop-tier raids, Missionary T2, map radius 3, Vault 30% |

## Balance Targets

Baseline net income for a "fully built for its level" economy:

| At Hearth | Timber/h | Scrap/h | Peat/h (net) | Devotion/h | Time to afford next Hearth | Bottleneck |
|---|---|---|---|---|---|---|
| 1 | 200 | 10 | 30 | 5 | ~20 min (tutorial) | Timber |
| 2 | 600 | 40 | 60 | 20 | ~3.5h | Timber / Scrap |
| 3 | 1,000 | 90 | 120 | 50 | ~13h | **Scrap** |
| 4 | 1,600 | 150 | 200 | 100 | ~27h | **Scrap**, Devotion |

- **Cost curve:** ~×2.5–3 per level vs. income ~×1.6 → each level takes ~2× longer. Formula for Hearth 6+: `cost(L) = base × 2.6^(L−1)`.
- **Raid salvage** should supply ~25–35% of Scrap income at Hearth 3–5, making Nights economically necessary.
- **Offline accrual** cap: 8h free; premium QoL extends to 12h / 16h. Storage caps still apply.

## Resource Sinks

| Sink | Type | Consumes | Notes |
|---|---|---|---|
| Construction / Upgrades | One-off | All | Primary sink |
| Heating upkeep | Continuous | Peat | Netted against production. If net < 0, heated buildings go **Lukewarm** (−50% Comfort). Never negative, never destructive |
| Steam towers | Per Night | Peat | Pre-loaded at Dusk |
| Repairs | Post-raid | Scrap + Timber | ≤ 10% of a raid's loot value — no death spiral |
| Missionaries | Per dispatch | Devotion + Timber (provisions) | Scales with distance & duration |
| **Tithes** | Voluntary | Whatever the Authority demands | Buys Tension down; large late-game sink; strategic choice: bribe or fight |
| Research | One-off | Scrap + Devotion | |

## Monetization (Ethical, No P2W)

| Product | Type | Notes |
|---|---|---|
| Extra build queue | QoL | Also earnable through play (e.g., "Apprentice" questline) |
| Offline accrual extension | QoL | 8h → 12h / 16h |
| Cultist outfits | Cosmetic | E.g., muddy rags → neat linen & cotton stand-collar shirts |
| Hearth / building skins | Cosmetic | Minimalist, clean (whitewashed, oiled oak) |
| Node banners | Cosmetic | Shown on world map nodes where player is Patron |


## Implemented (2026-10-14, village-2026.10.6)

- **Comfort** = Hearth (10 / 20 / 35) + each finished, intact building's Comfort (Cottage +4/+6, Cultist Hut +3/+5/+8, Bathhouse +40/+70) − Squalor (Midden Sorter −10/−15, plus −5 for every home, meaning a Cottage, Cultist Hut or Bathhouse, within 3 tiles of it). Shown in the HUD frame.
- **Devotion** producers make × (1 + Comfort / 200), kept between ×½ and ×3.
- **Midden Sorter** (Hearth 2, 2×2, 1 worker): 12 / 22 Scrap/h. The placement puzzle is keeping it away from homes.
- **Bathhouse** (Hearth 3, 3×3, 2 workers): 15 / 25 Devotion/h and +40 / +70 Comfort. It burns 10 / 15 Peat/h on top of the villagers' upkeep.
- **Bishop** (Hearth 3 raids): slow (0.4 tiles/s) and tough (320 HP), and blesses raiders within 3 tiles (a Priest blesses within 2). The laser pointer frightens him off ("it's a miracle") instead of blinding him.
