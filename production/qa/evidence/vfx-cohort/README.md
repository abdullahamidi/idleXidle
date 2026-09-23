# VFX validation cohort — RESULT (2026-09-23)

**Review page:** [`review.html`](review.html) shows every effect together: old and new strips, the real-speed
HUNT GIF, hit-aligned stills, metrics and a verdict. Images are in `img/`.

| Strip | Archetype | Gens | Verdict |
|---|---|---|---|
| `fx_seeker_trap` | trap | 6 | **PASS**: a snare instead of a drum; scale 1.50 → 1.10 (widest row 1.79 → 1.31) |
| `fx_seeker_mark` | mark, dual | 6 | **PASS**: locks on once, loops cleanly held by BRAND (old: 3 loop warnings) |
| `fx_shield` | shield, dual | 11 | **PASS**: edge 255 → 0; first loop rejected for a 3.1x flash |
| `fx_seeker_projectile` | projectile | 6 | **PASS**: soft alpha, continuous flight (it does not spin) |
| `fx_press` | field | 10 | **CONDITIONAL**: scale 1.44 over → 0.95, clean loop, but it reads as a plain box |
| `fx_anvil_strike` | impact | 14 | **STOPPED**: both clips had the right motion and an empty last frame |

**53 generations** (4,418 → 4,365). All installed strips pass EDGE, SOFT and LIVE with no diagnostic
warning. The library went from 60 to 56 of 67 failing. `-warnaserror` 0/0; Core 1,850 · Game 712 ·
Integration 2; `check_all.sh` green. Capture prerequisites landed: `RH_SHOT_HUNTER` installs the posed
champion's signature (the real switch's loadout steps, no toast, no save), and `RH_SHOT_SWAP` weaves any
skill into a fixture slot.

---

## The proposal as approved

**Date** 2026-09-23 · **Branch** `fix/vfx-fade` · Taxonomy of all 67 strips: [`taxonomy.md`](taxonomy.md)

## The cohort — six strips, five archetypes, all live in HUNT

| # | Strip | Owner | Gameplay use (runtime route) | Archetype | Why this one |
|---|---|---|---|---|---|
| 1 | `fx_seeker_trap` | THE SEEKER | JAWS (reaction: bites back when bitten) and REPAY: one-shot under the enemy row (`cast.trap`) | TRAP | The standard `fight` fixture casts JAWS at 1.0 s and 4.0 s. One-bit (fails SOFT). **Over the scale budget** (1.50 on the standard row, 1.79 worst), like all ten traps. Commit 8596685 already listed it for a re-roll ("reads more like a wheel than a snare"). |
| 2 | `fx_seeker_mark` | THE SEEKER | CALL: one-shot over the target's head (`cast.mark`) **and** BRAND: held behind the hunter, looping (`field.aura`) | MARK, **DUAL** | `fightinspect` casts CALL. One-bit. All ten champion marks are dual-use exactly like this, which makes it the riskiest contract: it must lock on as a one-shot AND loop seamlessly as a field. |
| 3 | `fx_press` | shared | PRESS (a field any hunter weaves): held behind the hunter all fight | FIELD / AURA | In every `fight` fixture. It **already has partial alpha** and passes EDGE/SOFT/LIVE, but it is the one field drawn **over the scale budget**: honest ratio 1.44, clamped to 1.25, so it stands ~17 % shorter than its profile asks and is magnified. Regeneration is required for the budget, not the alpha. |
| 4 | `fx_shield` | shared | SHIELD barrier: held while SHIELD stands, breathing 0.40–0.60, **and** GAIN / ABSORB / UNDYING flares, one-shots of the same strip | SHIELD, **DUAL** | The `fightshield` fixture plus `RH_SHOT_SHIELDFX`. Fails EDGE+SOFT: its ring was deliberately drawn to reach almost to the frame edge. The only strip of its archetype. |
| 5 | `fx_anvil_strike` | THE ANVIL | HARDFACE (the Anvil's signature) and HAMMER BLOW (any hunter): one-shot on the creature (`cast.strike`) | IMPACT | One-bit. A **second champion's** impact tests whether the Seeker's prompt rules transfer to another identity (the Anvil's vocabulary is iron). |
| 6 | `fx_seeker_projectile` | THE SEEKER | SPRAY: one-shot the renderer flies from hunter to target (`cast.projectile`) | PROJECTILE | Every `fight` fixture casts SPRAY (5.2 s, 11.2 s). One-bit. Stands for 11 projectile strips; its identity is "a spinning thrown blade". |

Not chosen: the approved `fx_seeker_strike`; the five fallback-only strips (nothing reaches them);
`fx_bind_chain` (not a HUNT effect); `fx_aura` / `fx_wilt` (they pass every rule and are within budget, so
`fx_press` is the partial-alpha field that actually needs work).

## Capture prerequisites (rig code only, no gameplay change)

1. **`RH_SHOT_HUNTER` must install the posed champion's own SIGNATURE**, the way the real roster
   switch does (`RepairForSwitch`). Today a posed champion fights with SPRAY / PRESS / JAWS and no signature,
   which is a build the game cannot produce. So HARDFACE (#5), the Falling Tower's held field and the
   Unbroken's held trap can never be filmed. Measured: THE ANVIL, THE FALLING TOWER and THE UNBROKEN
   all fought with three skills.
2. **A skill dial for the fight fixtures**, the mechanism `fightinspect` already uses, so BRAND can
   hold the mark (#2's held use) and HAMMER BLOW can be posed on any hunter.

## Per-archetype diagnostics (`tools/fx_energy.py`, contextual, never a CI gate)

| Archetype | Reading | Flags |
|---|---|---|
| IMPACT | ONE PEAK (existing) | energy regrowth; light re-forming at the centre |
| PROJECTILE | flight continuity | a frame-to-frame jump in size or centre (a reset or second launch) |
| TRAP / MARK | settle | a new placement after it has settled (a second rise from a low) |
| FIELD / AURA, SHIELD | loop | the seam 7 → 0 breaking harder than any frame-to-frame step; a structural reset (silhouette overlap collapsing); size swinging more than a set share |

Hard rules stay EDGE + SOFT + LIVE for everything. A DUAL strip gets both readings.

## Budget and stop rule

About 5–10 generations per strip were estimated. The cap is **15 per strip** (three animation attempts
plus shapes). If a strip reaches it without a credible candidate, I stop on that strip and report what
PixelLab kept doing wrong, rather than spending more. The cohort ceiling is ~90 generations; 4,418 remain.
