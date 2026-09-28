# PRESS, the first slice of the PERSISTENT FIELD / AURA reference (ADR-011, 2026-09-28)

Review page: https://claude.ai/artifact/EL1ZzQ9EjMYhqw13aF83CG

**The brief** (the owner, Concept A): "A persistent pressure field around the Seeker periodically sends a force pulse
outward. When the pulse reaches the front enemy, that enemy is visibly crushed / compressed for a moment." One visual
sentence: the Seeker's field pulses -> a wave goes out -> that enemy gets crushed. The player must read three things:
the field is active, a tick occurred, which enemy was affected. Motion reference: Syndra E (League of Legends), a broad
force wave from the caster toward the target, in the Seeker's lavender / violet / shadow palette.

**Core, unchanged.** PRESS is a Field: every 2000 ms the fight emits one `Aura` event for its slot and, while the target's
defence can still drop, a `Break` on the first creature standing. The presentation reads those events for the wave and
changes nothing the fight decides.

**What is in the build** (films `build/shots/press/p5`, made by `tools/asset-pipeline/films_press.sh`):

| state | ms from the tick | what the player sees |
|---|---|---|
| A · quiet field | always | a soft pressure haze leaning toward the enemy side, just in front of the Seeker's facing side, breathing slowly (alpha ~0.26 and a faint edge glow); never a ring, never a floor circle |
| B · compression | -520 → -300 | the field draws in (scale 0.86) and gathers (alpha 0.5) |
| C · emission / travel | -300 → 0 | the field springs back out and ONE crescent pressure front leaves its leading edge: a thick smoky wake behind a rim that brightens as it arrives; it grows from half the Seeker's height to 1.7× the target's and ARRIVES on the fight's tick (ADR-011's contact on the beat) |
| D · crush | 0 → ~170 | the crescent's tips fold over and under the target and become two pressing arcs biting into its drawn silhouette, pale-hot on the tick (the brightest moment), cooling to violet; the creature's body buckles (height ×0.85, width ×1.07, feet on the floor) |
| E · settle | ~170 → 380 | the arcs let go and fade; the field is quiet again |

- **The target is the fight's**: the creature the tick's `Break` names (the front enemy), else the first still standing.
  The arcs sit on that creature's drawn body (0.8 of its width), never on the next one.
- **Giving way to JAWS**: on a tick where a presented reaction (JAWS) is triggered from 700 ms before to 400 ms after,
  PRESS draws no arcs above and below the creature (with JAWS' fang rows they read as one jaw closing); the front
  flattens against the creature's facing side and the body still buckles.
- **Quiet** (×0.6) while the champion performs SPRAY or HARD HANDS.
- **Three procedural parts** (`tools/asset-pipeline/v2/seeker_press.py`): the field haze, the pressure front (crisp +
  softened afterimage cell), the crush arc. `FieldRecipe` / `FieldPerformance` (keyed by skill id: `FieldRecipes.For`),
  drawn on the reaction layer's three passes; the generic held aura gives way to it. `UiKit.Buckle` scales a placed
  frame about its feet (the resolver's pure helper; the renderer law holds).

**Checked.** A three-lens judge panel (the sentence; craft; context): all ACCEPT WITH NOTES. Two must-fixes, both done in
this slice: the quiet field was invisible behind the Seeker (moved forward, alpha raised, an edge glow); PRESS's arcs and
JAWS' fang rows merged into one jaw on a shared creature (PRESS now gives way). Other changes from the review: a thicker,
darker wake and a rim dimmer on the way (it read as a sword beam), the arcs made of the crescent's tips (it read as two
effects), the arcs on the target's body (they sat toward the next creature), a longer hold. Two adversarial re-checks:
both must-fixes RESOLVED WITH NOTES; from their notes, the arcs' start is capped inside the stage (they crossed the health
bars and the dock for a frame), their hot colour is a pale lavender (near-white read as SPRAY's steel), and the front
that gives way to JAWS is gone by 100 ms (it bracketed the fang crown), with a true-speed film of that tick (04b).

**Proof.** 817 game tests (8 new in `press_field_test.cs`) and 1888 Core tests pass; the asset gate is clean. The field's
draw allocates 0 bytes on 981 of the traced frames; the exceptions are the once-per-tick silhouette probe (the same cost
JAWS pays once). At most 10 sprites a frame.

| file | what |
|---|---|
| 01_press_true_speed_sound.mp4 | normal TEMPO, the tick at 6000 |
| 02_press_MUTED.mp4 | the same, muted |
| 03_close_crop_true_speed_MUTED.mp4 | the Seeker, the front and the crush, close |
| 04_repeated_ticks_sound.mp4 | three ticks over 5 s (one picture per 2 frames); the target moves as creatures fall; the tick at 10000 gives way to JAWS |
| 04b_gives_way_to_jaws_true_speed_sound.mp4 | the tick at 10000, every frame: PRESS gives way to JAWS on the same creature |
| 05_with_spray_sound.mp4 | fast TEMPO: SPRAY at 1400, then the tick at 2000 |
| 06_with_hard_hands_sound.mp4 | fast TEMPO: HARD HANDS at 3600, then the tick at 4000 (SPRAY right after) |
| 07_before_generic_aura_sound.mp4 | BEFORE: the generic held aura (RH_FIELD_RECIPES=0) |
| 08_phase_sheet_REST_COMPRESS_EMIT_TRAVEL_CRUSH_SETTLE.png | every phase, the slice over the before |
| 09_trace.md | the ticks and every drawn frame of the phrase |

Regenerate: `bash tools/asset-pipeline/films_press.sh p5 normal repeated fast normal_off yield`, then
`PYTHONUTF8=1 python tools/asset-pipeline/press_field_evidence.py p5 production/qa/evidence/press-field`.
