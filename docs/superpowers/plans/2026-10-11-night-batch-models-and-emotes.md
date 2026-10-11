# Night Batch: Dramatic Emotes + Asset Batch Implementation Plan

> **For agentic workers:** executed inline (superpowers:executing-plans) overnight with the owner's permission; owner reviews the gallery in the morning.

**Goal:** (1) Make character animation dramatic, comic and ridiculous (owner request), (2) produce as many approved-style models as possible (spec steps 1b, 2, 4), wired into the game where the fallback keeps it safe.

**Architecture:** Same pipeline as `2026-10-10-art-pipeline-and-prophet.md`. New shared `art/blender/lib/people.py` builds friendly chibi characters (head/face/body/limbs) so every character follows the owner-approved Prophet face. Emotes extend `PartPose` (pure curves, unit-tested) and `PartAnimator` (state + one-shots).

**Spec:** `docs/superpowers/specs/2026-10-10-art-direction-and-pipeline-design.md` (budgets: characters 2,200, buildings 3,000, props 300).

## Global Constraints
- Owner art feedback: friendly human faces (no faceted heads, no rectangle features); dramatic comic animation.
- Every model: Blender test (budget, palette, required parts), previews, committed FBX; Unity loads via `ArtLibrary` with primitive fallback.
- Simulation untouched; `.NET` 56/56 and determinism hash `12c3fe1f0e23fb59` unchanged.
- Commits as adziusmaster, no Co-Authored-By; work stays on branch `art/pipeline-and-prophet` (pushed, not merged).

## Tasks
1. **Emotes** — `PartPose`: dramatic swoon (stagger, hand to forehead, fall, double bounce, legs kick), bigger walk (waddle), attack (overhead wind-up + lunge), hit (knock-back), new Bored (slump, sigh, check watch, foot tap), Unimpressed (head shake, arms crossed), Annoyed (stomp, umbrella shake); `PartAnimator` auto-Bored after idle, `Annoyed()`/`Unimpressed()` one-shots; `BattleView` triggers (rejected order → annoyed, wasted skill → unimpressed, hit → annoyed sometimes). EditMode tests for every curve's rest/peak. GIFs via `AnimationShowcase`.
2. **`lib/people.py`** — shared friendly character builder; refactor Prophet onto it with identical look (tests unchanged).
3. **Enemies** — Levy (hood, pitchfork), Man-at-arms (nasal helmet, mail, kite shield in Wulfric's red/gold), Zealot (tonsure, red robe, hymnal); tests + previews + battle integration (enemy part animation).
4. **Hearth L1** — Roman bath ruin, pool, steam, blue tarp.
5. **Towers** — Sapling Flinger, Slurry Chute (wattle hopper + plank ramp); integration in BoardView.
6. **Props** — rock, pine, spawn gate (Wulfric banner), torch, traffic cone; integration.
7. **Day village** — Hearth L2, L3, Woodcutter's Lean-to, Peat Cut, Cistern, Storage Hut, cultist hut (previews only; base building comes later).
8. **Gallery** — morning review page with every preview and GIF.
