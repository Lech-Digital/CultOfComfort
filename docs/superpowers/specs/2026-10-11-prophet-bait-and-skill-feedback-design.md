# The Prophet as Bait & Skill Feedback — Design

- **Date:** 2026-10-11
- **Status:** Approved by the owner (2026-10-11)
- **Scope:** raiders leave the road to fight the Prophet (shared simulation, new balance version); on-screen explanations of skills and raider states (client only)
- **Out of scope:** off-road pathfinding around towers, new skills, balance tuning beyond the two new ranges, the village

## Intent

Make Nights a readable decision. Standing beside the road should *pull* raiders off it — into tower range, at the
Prophet's own risk — and every skill should visibly say what it did. Owner's words: "when prophet stands next to the
road, the invaders will also get off the path" and "open sluice makes the path just blue. we need some explanations".

## Today

Raiders are points along the road (`Enemy.DistanceAlongPath`; `Battle.PositionOf` interpolates the path). Within
`EngageRange` of the Prophet (Levy ¾, Man-at-arms 1, Zealot 2½ tiles) they stop *on the road* and hit him; at most
`MaxEngaged` = 3 at once. Open Sluice pushes every active raider back `PushbackTiles` and muddies it (slower); the client
shows the road tiles bobbing blue. Balance version `2026.10.2`, demo hash `12c3fe1f0e23fb59`.

## 1. Raiders step off the road (simulation)

- `Enemy` gains `Offset` (`FixVector2`, tiles) from its road point. `Battle.PositionOf(enemy)` = road point + offset, so
  towers, Hot Stones, Phantom Voice and the client all see the real position with no other change.
- `EnemyStats` gains:
  - `NoticeRange` — leaves the road when the Prophet is this close to its **road point** (Levy 2, Man-at-arms 1½, Zealot 0 = never).
  - `Reach` — attacks when this close to the Prophet (Levy ½, Man-at-arms ½). For Zealots the existing `EngageRange`
    (2½, thrown from the road) stays as-is.
- `LeaderStats` gains `LureLeash` = 2½ tiles: a raider whose road point is farther than this from the Prophet walks back.
- Each tick, per active raider, in the existing order:
  1. **Frightened** (Phantom Voice): walks back onto the road first (offset shrinks), then backwards as today.
  2. **Lured** — not frightened, the Prophet is targetable (not swooned, not in revive grace), the engaged cap (3) is not
     reached, and the Prophet is within `NoticeRange` of its road point (or it is already off-road and within
     `LureLeash`): it counts as engaged; if within `Reach` it fights (existing `FightLeader`), else it steps toward him.
  3. **Otherwise**: if off the road it steps back toward it (offset → 0); only with zero offset does it advance along the path.
- Steps use the raider's speed per tick (×slow multiplier while slowed), moving each axis by at most one step
  (deterministic, no square roots; diagonals are up to √2 faster — accepted).
- Raiders walk in straight lines and may clip a tower corner; no off-road pathfinding (owner-accepted).
- Sluice pushback moves the road point back; the offset is kept (they are washed along, then walk on).
- The offset is part of the state hash. Balance version becomes **`2026.10.3`**; the demo hash changes and is re-pinned
  (Unity `DeterminismProbe.ExpectedHash`, a .NET golden test) and re-verified on the Pixel.

## 2. Explanations (client)

- **Sluice wave:** the flood sweeps the road from the Hearth end towards the spawn (staggered by path index, ~0.8 s),
  replacing today's near-simultaneous bob.
- **Floating notes:** every raider hit by the sluice gets a marker-pen "washed back!" note that rises and fades above it
  (1.2 s). Phantom Voice: "?!"; Hot Stones: "ouch!".
- **Status icons** over each raider (billboarded, from the HUD icon set): muddy → `mud` (splat), slowed → `speed` (snail),
  frightened → `voice` (tin can), fighting the Prophet → `swords`. At most two shown, in that priority order.
- **First-use tips:** the first time each skill is used, a Prophet sticky note explains it:
  - Sluice: "Opened the cistern: everyone got washed three tiles back down the road and is muddy for a bit — Sapling Flingers love muddy targets. Two per night."
  - Phantom Voice: "Tin-can megaphone: raiders near me panic and walk backwards for two seconds."
  - Hot Stones: "Hot stones: hurts and muddies everyone in the circle. Muddy raiders take extra damage from Sapling Flingers."
  Shown once ever (stored with `PlayerPrefs`); the Settings sheet gains **Show tips again**.
- New icons `mud` (reusing `fx_splat`) and `swords` (two crossed blades) join the Blender icon set.

## Verification

- **.NET (xUnit):** lured within notice range leaves the road; attacks only within reach; returns when he walks beyond the
  leash, swoons, or is in revive grace; Zealots never leave the road; the engaged cap holds with lured raiders; a tower
  hits an off-road raider that would be out of range on the road; frightened raiders return to the road first; replay
  of a lured battle matches the live hash; golden demo hash pinned.
- **Unity EditMode:** raider status selection (priority, max two); tips store shows each tip once and resets.
- **Unity PlayMode:** the Settings sheet's Show tips again resets the store; existing suites green.
- **Device:** Pixel 10 shows the new demo hash as MATCH; 60 fps in a busy Night; a screenshot of a lured raider and of the
  sluice notes.
