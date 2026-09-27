# ADR-012: The bite is performed and the hit is received — the pack's spaced attack, its front-led lunge, the champion's recoil, and the quiet default flash

| Field | Value |
|-------|-------|
| **Status** | **Partly Accepted** (2026-09-26, the owner's review of the first battery: `production/qa/evidence/bite-foundation/`, https://claude.ai/artifact/2kS886KRf84XjHRw2EwmqJ). ACCEPTED: the front-led pack contact grammar (decision 3); the layered receiver-reaction architecture (4: presentation transform added to the action's, no hurt clip, no retiming, re-impulse); the generic flash contract (6: peak 0.45, rise 0.15, ~80 ms; recipe overrides intact; no default rim; no restoring the 200 ms white); the capture-view cleanup (8); no global hit-stop (7). **REJECTED by the owner (2026-09-27)**: the polish pass's painted Shadow maw on the whelp (a violet cavity, three pale teeth, open/shut mouth edits: "do not try another mouth edit; the whelp's dark faceless head and bright eyes stay") and the whole-body receiver recoil as a routine hit's language (4, 5, 6, 8 and 10 % all read as a sprite translated, not a body absorbing force). **THE RESET / PROTOTYPE (2026-09-27, `production/qa/evidence/bite-reset/`)** separates the ATTACK VERB (the whelp's body: coil → head-first lunge → contact extension → follow-through → recovery, no facial feature) from the bite's MATERIAL / CONTACT IDENTITY (a small two-fang motif that closes on the champion at the contact); the recoil is OFF by default (0 %) with its curve and dial kept; the bite burst is gone. JAWS' rough A2 MIRRORED SHADOW SNAP (the incoming motif echoed on the biter in Shadow purple, the ownership trace only after the snap begins) is a concept animatic, not production. **THE SECOND PASS (2026-09-27, the reset direction approved, its art rejected as final; `production/qa/evidence/bite-reset-2/`)**: the column-warped strip is retired and the whelp's COIL / COMMIT / CONTACT / FOLLOW-THROUGH are AUTHORED as joint-puppet poses of its own rest frame (a skeleton of 18 handles, rigid moving-least-squares between them; PixelLab used for two pose concepts as references only); the contact motif is the M4 hybrid of the three blind-read candidates (broad crescent jaws + one fang point each, converging on one contact centre at his TORSO edge, not his blade); the leader's lunge travel is 25 % (20 / 25 / 30 filmed); rough A2 is redrawn in the motif's own shape family in Shadow, with no ownership line and no trap sound. **THE THIRD PASS (2026-09-27, the second pass's strip and M4 rejected as production reference art; `production/qa/evidence/bite-reset-3/`)**: (a) ATTACK ART EXTENT IS SEPARATE FROM CANONICAL ACTOR BOUNDS (decision 9): the whelp's attack strip is 8 frames of 640 × 640 around its 512 idle frame and the renderer places it on the idle's scale, ground and anchor; (b) the four attack poses are genuinely REDRAWN (PixelLab pose edits in the wider art space, repaired deterministically: re-inked from the original's colours, eyes rebuilt), not deformations; (c) the leader's lunge travel is 20 %; (d) the contact motif is N2 OFFSET FANG SNAP, a directional asymmetric shape arriving from the enemy's side (M4's symmetric jaws read as an hourglass / status icon and are withdrawn); (e) **A2 MIRRORED SHADOW SNAP is APPROVED AS THE JAWS DESIGN DIRECTION** (the owner, 2026-09-27); its M4-derived artwork is NOT approved, and the third pass draws it in the N2 family as a rhyme (mirrored, ~1.2×, a longer serrated fang, a dark fill, a shard residue, no ownership line, no trap sound). PROPOSED, NOT ACCEPTED: the redrawn whelp attack as the reference enemy attack (root-motion-OFF gate filmed); N2 as the bite's contact; A2's artwork. The owner reviews true-speed footage; JAWS' art and audio passes wait on that; Concept D is not built. |
| **Closed (the owner, 2026-09-27)** | The whelp attack work is CLOSED: the redrawn 640-canvas lunge is kept as the enemy reference (it is clearly better than the crouch), with ~20 % presentation travel; the remaining row-distance gap (~330 px at contact) is recorded as a presentation / layout limitation, not gated on. The bite CONTACT-MOTIF requirement is REMOVED: a routine whelp hit is the authored lunge, the bite sound, the F2 flash on the biter and the number, and no glyph is drawn on the Seeker to say "bite" (`DrawBiteContact` and the motif curves are gone; the N1–N3 / M4 explorations are history). JAWS is no longer gated on literal sprite-to-sprite contact and is presented as Shadow piranha (ADR-011). |
| **Date** | 2026-09-26, reset 2026-09-27, closed 2026-09-27 |
| **Deciders** | owner; lead-programmer, technical-artist (presentation) |
| **Supersedes** | the row's contact-time shove (`_enemyLunge`, 40 px on the beat), the full-white usual flash (peak 1, ~200 ms) |

## Context

Two blind studies (`production/qa/evidence/jaws-concepts/`, `production/qa/evidence/bite-readability/`) found that
the fight's shared combat language, upstream of any skill, did not carry its two most basic sentences:

1. **"The enemy attacked."** The Gloom Whelp's attack strip was a crouch in place: the head sank for two thirds of
   a second, the contact frame equalled the frame before it (2 px), the body never travelled toward the champion,
   and the only fast motion was standing back up after the hit. The wind-up played as a 5.5 fps slideshow with no
   acceleration. The row's only travel was a 40 px shove that BEGAN on the contact frame: a recoil grammar. Readers
   with tint and effects removed said the creatures "did nothing".
2. **"I was hit, from there."** The champion received a bite as a centred radial burst and no body response. Every
   reader: "he doesn't flinch, stagger or recoil"; "a status glow".
3. **The struck creature disappeared into its own flash.** The usual flash drew the creature's white mask at full
   strength for ~200 ms: "a silhouette with no eyes, no colour, blank", hiding whatever was drawn on it, and at fast
   tempo turning the pack into white cut-outs on every SPRAY.

Core's bite is ONE aggregate event per interval that sums every living creature's bite. Its outcomes and times are
not in question and do not change.

## Decision

All of it is presentation, a function of the replay's playhead, in one pure module (`BitePresentation`) the arena
applies; nothing reads or moves a Core position, and no action clip is retimed.

1. **The wind-up is spaced.** The attack clip's wind-up frames play as a power of the wind-up
   (`WindupPhase`, exponent 1.8): of a 900 ms wind-up the rest frame holds ~370 ms, the two coil frames ~135 ms each,
   and the two commit poses start ~225 and ~105 ms before the contact (2.4 crammed them into the last ~100 ms and
   read as "popped forward"). The contact frame is still frame 5 of 8 on the `EnemyStrike` beat.
2. **The whelp's attack is a PREDATORY LUNGE of the whole body, with no mouth** (the reset, 2026-09-27; the painted
   maw is rejected and removed): COIL (the centre of mass lower and back, the head withdrawn, the tail gathered, the
   limbs compressed) → COMMIT (the torso extends, the head leads forward and rises, the rear trails) → CONTACT (the
   longest forward silhouette, the head driven down into the champion, the rear mass low and behind) → FOLLOW-THROUGH
   (the front compresses, the trailing parts catch up) → recover. The creature's identity (a dark faceless head, two
   bright eyes, no mouth) is intact in every frame. The four attack poses are GENUINELY REDRAWN on the extended art
   canvas (decision 9; `tools/asset-pipeline/v2/umbral_swarm_attack_640.py`): PixelLab (`edit_image_pro_flash`, a
   256 × 204 canvas standing for the 640 × 512 art space, a strict same-creature prompt) drew COIL, COMMIT, CONTACT
   and FOLLOW-THROUGH from the rest frame as its reference, and each was upscaled to production scale and repaired
   deterministically: the silhouette re-thresholded and opened, the fill and the violet rim re-inked from the
   original's own colours so line weight matches the 512 idle, the eyes rebuilt at the head's front, every pose
   grounded on the rest frame's sole, COMMIT set 48 texels behind CONTACT so the reach only grows into the contact.
   REST and RECOVER are the idle pose; nothing generated is used raw and nothing is a deformation of the rest image.
   The earlier routes are retired for the record: whole-sprite shears and a painted maw (rejected), column warps
   (read coil → rear up → settle), and a joint puppet inside the 512 frame (its CONTACT read "impact / lunge" but its
   head could not pass x ≈ 70: the art-space limit decision 9 removes). The acceptance test is the strip with root
   motion OFF, VFX OFF, tint OFF, flash OFF (`RH_SHOT_NOROOT=1`): the poses alone must read prepare → launch toward
   the target → impact → recover, and the reference is not accepted until they do in the owner's eye.
   **THE BITE'S CONTACT IS A MOTIF, not a burst: the OFFSET FANG SNAP** (N2, `HuntScreen.FangSnap`). Two parts of
   ONE directional action arriving from the enemy's side: a large upper fang, a curved wedge driving from the upper
   right down-left to a point, and a short lower jaw wedge from the lower right, converging on a contact point that
   is low and left of centre, with two short root streaks on the enemy side; ember on a dark edge; no bilateral
   symmetry, no equal masses, no central dot, no centred composition (the second pass's M4, symmetric crescent jaws
   with a pale meeting point, read as an hourglass / bow-tie / status icon at play size and is withdrawn). N2 was
   chosen from three rough directional candidates (asymmetric crescents / offset fang / broken serrated arcs) by
   static reads on the target alone asking what kind of impact the shape suggests: "pinch; clamp, bite", "an event
   on his body, not an icon", 4 of 5 for closing from the right; none read slash, claw, spark or icon. The timing is
   M4's, which the owner approved: the fang lifted and the jaw dropped (OPEN) on the frame before the contact, driven
   together on it (SNAP), held ~16 ms, gone by ~70 (`BitePresentation.MotifOpen` / `MotifStrength`); ~0.13 of his
   height on his TORSO's enemy-facing edge under the hood (`body.X + 0.57 w, body.Y + 0.39 h`, measured from a
   champion-free plate: his visible rect's right edge is his sword tip). It is not the recoil's accessory
   (`RH_SHOT_NORECOIL` leaves it; `RH_SHOT_NOVFX` hides it). A single frozen frame need not say "bite" on its own:
   at true speed the question is whether two hostile shapes visibly snapped onto him from the enemy's direction,
   and the lunge and the sound supply the rest.
3. **The pack lunges, front-led, in lockstep** (`Lunge`, in VISIBLE body widths): back 4 % in anticipation over the
   first 75 % of the wind-up, then forward from −220 ms to 20 % at contact (the third pass filmed 15 / 20 / 25 %
   over the redrawn strip through `RH_SHOT_LUNGE`; the art carries the verb and the travel supports spacing, so it
   came down from 30 to 25 to 20; 15 reads as a twitch, 25 begins to float) on a near-linear ease (1.15: a third of the
   travel done at −120 ms, ~70 % at −50; a lunge seen crossing the gap, not a position pop), one 3 % overshoot at
   +30 ms, home by +260 ms. The LEADER, the front living creature (the lowest slot alive; the row lays slot 0 nearest the champion,
   and a death moves the lead to the next living slot), gets the full travel; the rest of the pack 40 % of it. All
   creatures share one anticipation and one contact beat, because Core's bite is one simultaneous event; contacts
   are never staggered. The boss performs the same curve at half the share. The contact-time shove is removed.
4. **The champion is NOT translated by a routine hit** (the reset: `RecoilShare` 0 by default). 4, 5, 6, 8 and 10 %
   of his width were filmed and blind-read; none read as a body absorbing force, all read as the sprite sliding. A
   routine bite is told by the attacker's motion, the contact motif, the sound and the health change. The layered
   recoil CODE stays (`Recoil` / `Recoil01`: peak +33 ms, home +130 ms, cubic ease, re-impulse, added to the performed
   action's root motion in `_champDrawBox`, the effects pinned to him riding it) for stronger authored reactions later,
   and the capture rig dials it (`RH_SHOT_RECOIL=<share>`) to film a micro-impulse (1–2 %, a 1-frame jolt) beside the
   default; a micro-impulse ships only if true-speed footage shows the contact is dead without it.
5. **The contact is the motif** (decision 2). The bite burst (`impact.bite`, a small `fx_hit` mark at his chest edge)
   and the compression wedge are removed; a profile nothing spawns is a dormant feature and is gone from the table.
   The usual flash on the receiver is not added in this prototype, so the motif can be judged alone.
6. **The usual flash is quiet** (`UsualFlash`: peak 0.45, ~80 ms, a 15 % rise). Authored actions keep their own
   recipe flash (SPRAY's restrained 0.5/130, HARD HANDS' own); a generic hit is acknowledged, an authored action spends
   more. No default rim; a directional light is a recipe's to add later.
7. **No hit-stop.** The auto-battle cadence is too frequent for repeated world freezes. Authored major actions may
   decide a tiny presentation hold per recipe later; the routine bite has none.
9. **Attack art extent is separate from canonical actor bounds** (the third pass, 2026-09-27). The IDLE frame's
   bounds are the actor's canonical bounds: they decide its scale, its grounding, its presentation size and its row
   place. An authored action clip may occupy MORE art space than them: its frames may be larger squares (the whelp's
   attack is 8 × 640 × 640 around the 512 idle), with the canonical frame in each frame's bottom-right corner so the
   extra columns extend toward the side the art faces and the extra rows are headroom. `UiKit.ResolveFrame` places
   such a strip AS its idle (`placeAs`): the scale from the idle's frame and crop, the sole from the idle's pad, the
   horizontal anchor from the idle's centred symmetric crop, and texel (x + offX, y + offY) of the action lands where
   texel (x, y) of the idle lands; no side crop is taken from the action strip. A wider attack frame therefore never
   shrinks, rescales or moves the actor; the creature's REST frame in the wider strip draws pixel-identically to the
   idle. The whelp's poses are proof that the rule was needed: inside the 512 frame the rest head sat at x 39 of 512
   and no pose could extend it; on the 640 canvas the CONTACT head reaches x 8, 159 texels ahead of the rest head.
   The strip contract's "N square frames" holds; only the frame size may exceed the idle's. This is the actor-scale
   lesson of HARD HANDS applied to art space.
   **The row's distance is the next fact.** With the leader's lunge at 20 % of its visible width and the CONTACT pose
   at full extension, the whelp's front edge at the contact frame is ~330 px from the champion's torso edge (the
   row's home puts the leader's rest head ~530 px from him). No travel in the 15–30 % family closes that; the bite
   lands "at range". Whether the pack should stand closer, or the leader alone travel further, is a layout decision
   for the owner, not a tuning of this dial.
8. **Capture views are generic and central** (`CaptureViews`): `RH_SHOT_NOVFX`, `RH_SHOT_NOCHAMP`, `RH_SHOT_SOCKETS`,
   `RH_SHOT_NOTINT`, `RH_SHOT_SIL`, `RH_SHOT_NOROOT`, `RH_SHOT_NORECOIL`, `RH_SHOT_FLASH=off|peak,rise,ms`, and
   `RH_SHOT_STRIP_FILES` in `AssetLibrary`. The foundation study's one-off knobs (`RH_SHOT_NOANSWER`,
   `RH_SHOT_FLASH=F1|F2|F3`, `RH_SHOT_BITE`, `RH_SHOT_HITSTOP`) are removed.

## Consequences

- Every creature's attack clip gains the spacing (it is the shared `EnemyClipSeconds`); creatures whose strips were
  authored linear now hold their first wind-up frame longer and rush the last. That is the intended grammar; a strip
  that reads wrong under it is a strip to re-author, like the whelp's.
- The pack's lunge overlaps creatures in a compressed row more than before at contact (the leader travels further
  than its followers). The arena scissor handles overshoot as it did for the old shove.
- The recoil is a presentation offset (zero by default); when dialled, the champion's published bounds move with it,
  so hits and effects aimed at him land where he is drawn.
- JAWS (ADR-011's REACTION / TRAP reference) is a MAGICAL REACTION, not a physical machine. **A2 MIRRORED SHADOW
  SNAP is the approved JAWS design direction (the owner, 2026-09-27)**: enemy attack → hostile snap on the Seeker →
  Shadow snap back on the resolved attacker; no trap body, housing, chain or physical retract; the mechanical /
  bear-trap family stays rejected and its assets are history, never played once A2 is promoted. Its artwork RHYMES
  with the selected incoming motif without copying it: the third pass draws A2 in the N2 family, mirrored (arriving
  from the Seeker's side), ~1.2× the incoming footprint (never a giant mouth; secondary to SPRAY and HARD HANDS),
  the fang more elongated with two serrations on its inner edge, a dark internal fill under the violet rim, a
  residue that breaks into two shards; beginning ~16 ms after the incoming snap, peaking at +16–33, gone by ~+130;
  NO ownership line (the causal sequence carries ownership; a tiny after-the-answer residue may be added only if
  true-speed testing still cannot identify it); no trap sound (its audio direction, a SHADOW BITE-BACK: a very
  short dark snap, dry teeth / energy compression, a low restrained Shadow transient; never bone crack, roar, metal
  or a long whoosh, is a pass that follows visual approval). It exists as an animatic
  (`prototypes/jaws-concepts/animatic.py A2`) over the third pass's plates; the artwork is not yet accepted.
- The audio is unchanged in this pass (a controlled visual comparison); the bite's material identity is a later pass.
- Tests: `bite_presentation_test` pins the curves' shape, bounds and timing.

## ADR Dependencies

- ADR-011 (an action is performed; contact is the beat): the recoil layers over a performed action and never owns
  the figure; JAWS' reaction layer is untouched.
- ADR-009 (VFX blend): the contact streaks are drawn in the normal batch, not as light.

## Engine Compatibility

MonoGame 3.8.4.1; SpriteBatch draws only; no shader; no per-frame allocation (the curves are static arithmetic, the
leader is found by a loop over the composition).

## GDD Requirements Addressed

Readability of the fight's core loop (combat GDD: every hit legible at play speed); the JAWS reference's prerequisite
("enemy action → retaliation" must read before a reaction can).
