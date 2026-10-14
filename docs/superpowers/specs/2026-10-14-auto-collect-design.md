# Auto-Collect: Production Goes Straight into the Stores

- **Date:** 2026-10-14
- **Status:** Asked for by the owner ("tapping on a producer object makes no sense"). Real time was chosen over a tick.

## Rules

- Every producer's output flows into the village stores continuously, capped by storage. Nothing waits in a building and there is nothing to tap.
- The economy is still analytical: between events the stores change linearly, so the stock is exact at any moment and the HUD simply shows it. There is no timer and no tick.
- **Peat is one net rate**: the cutters' output minus the fires' burn.
  - When the burn wins, the Peat runs out (an event) and the village goes cold. Only the peat cutters work in the cold, and nothing burns.
  - The fires are relit once **an hour's burn** is stacked (an event), so they don't flicker on and off at zero.
  - Peat bought or filled warms the village under the same rule.
- Full stores take nothing more and lose nothing. A store over its cap (after a Storage Hut is demolished) keeps what it has.
- A new producer's starter pile (a quarter of an hour's output) goes straight into the stores.
- Old saves (layout version below 4) pour their producers' buffers into the stores once, capped. What doesn't fit is lost.
- `Collect` is still parsed for old clients and is refused ("the stores fill up by themselves now").

## Knock-on changes

- The errand "Tap the Lean-to to collect" became "Build a Storage Hut" (errands-2026.10.3). It pays 100 Timber, which covers the hut, so the tutorial stays near its clock (the pace bot reaches Hearth 2 at 37 minutes, against 22 before).
- The client floats "+3 Timber" over working producers as their output comes in: a busy one about every 30 seconds, a slow one per whole unit.
- Building cards say "Goes straight into the stores." and no longer show "x/y ready".

## Pace

- Offline accumulation is now bounded by storage alone. Before, it was storage plus 8 hours of buffer in every producer, so it is never faster.
- The builder policy of the pace bot reaches Hearth 2 at the same minute (167) as before.
