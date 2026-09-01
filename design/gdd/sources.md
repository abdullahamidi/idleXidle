# Sources — the six elements

> **Status**: Current · **Created**: 2026-09-01 (refactor P15) — the first spec of the six Sources
> that matches the code. · **Code**: `Core/Sources/Source.cs`, `SoloBattle` signatures,
> `Economy/ElementSets.cs`, `Encounters/Regions.cs` themes.

## 1. Overview

BODY, NATURE, MACHINE, MIND, SHADOW, SPIRIT. A Source is the game’s element: every woven skill
carries one, every worn item carries one, every region is themed to one, and every creature in a
wave has one. Sources colour HOW things fight and WHERE things drop — they are not the build’s
specialisation axis (that is the STYLE ring — see §6).

## 2. Player Fantasy

“What my power is made of.” A Body build wounds and bleeds; a Machine build strips armour; a
Shadow build executes. Committing a wardrobe to one element makes its skills sing (sets); carrying
a spread hedges the world’s themed regions.

## 3. Detailed Rules

- **A skill’s Source** defaults to the slot’s pick; a chosen VARIATION can own the Source outright
  (`SkillVariation.Source`), and the composer resolves it once.
- **Signatures** — every SKILL hit lands its Source’s rider (never the plain swing):
  BODY lays wounds (+3% damage taken per stack, max 5, laid by any Body skill, paid by every skill
  hit) · MACHINE bends armour (−1 flat defence per hit, cap 5 per creature) · SHADOW hits +12%
  below half health · MIND extends the amplify window (+250 ms per hit, cap +1000) · NATURE leeches
  a share of aura damage (via `HealTuning`) · SPIRIT primes: the next non-Spirit skill hits +15%.
- **The matchup**: a skill’s Source is amplified against the creature it lands on (`SoloBattle.Amp`);
  rosters draw a region’s own Source plus its two wheel neighbours (`BandCycles.RosterFor`), so no
  region is a single-matchup lookup and picking a Source is a bet, not an answer.
- **Items**: a drop’s Element is the Source of the region whose chest paid it. Wearing 2/3/4/5
  pieces of one element unlocks that element’s SET rungs — +8% own-element skill damage at 2 and
  again at 4, a stat in the element’s character at 3, the element’s one special rule at 5
  (`ElementSets`). Eight worn slots, so a full five-set leaves three free.
- **Regions**: one region per Source, in conquest order — Verdant Hollow (Nature), Cinderworks
  (Machine), Umbral Reach (Shadow), Marrow Wastes (Body), The Still Archive (Mind), The Pale Choir
  (Spirit).

## 4. Formulas

Signature constants (in `SoloBattle`): wound +0.03/stack × max 5 · machine strip 1 flat, cap 5 ·
shadow threshold 0.5, bonus +0.12 · mind extend 250 ms, cap 1000 ms · spirit bonus +0.15 · nature
leech via `HealTuning.NatureSignatureLeech`. Set rungs: pieces {2,3,4,5}, own-skill +0.08 per
damage rung. Matchup factors live in `SoloBattle.Amp` and are pinned by `source_signature_test.cs`.

## 5. Edge Cases

- The basic attack carries no Source: no signature, no matchup, no set skill-bonus.
- A signature never feeds itself (wounds are laid AFTER the hit that landed).
- An unknown Source name in a save drops the slot, like every unknown catalogue name.
- Mixing elements is allowed and pays partially — each worn element counts its own pieces.

## 6. What a Source is NOT

- **Not affinity.** Specialisation is the STYLE ring (`StyleAffinity`: own ×2.00, neighbours
  ×1.15, off ×0.75, opposite ×0.45; a sworn Vow pulls one ring closer). The old Form hexagon —
  where Source-affinity lived — died with Form (P3c).
- **Not a resistance system.** Creatures have no elemental resistances; the matchup only amplifies.

## 7. Tuning Knobs

The six `Signature*` constants · `ElementSets.OwnSkillBonusPerRung` and each set’s tier-3 stat and
tier-5 rule · the roster wheel width (own + two neighbours) · per-variation Source ownership in
`SkillCatalogue`.

## 8. Acceptance Criteria

`source_signature_test.cs` (each signature fires and in the right direction) · element-set tests in
`Economy` (rungs count worn pieces; own-element bonus reaches the fight through `GearShape`) ·
`BandCycleTests` (rosters draw own + neighbours) · region themes pinned by `WorldTests`.
