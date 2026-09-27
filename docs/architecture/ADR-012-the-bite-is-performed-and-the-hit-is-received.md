# ADR-012: The bite is performed and the hit is received — the pack's spaced attack, its front-led lunge, the champion's recoil, and the quiet default flash

| Field | Value |
|-------|-------|
| **Status** | **Partly Accepted** (2026-09-26, the owner's review of the first battery: `production/qa/evidence/bite-foundation/`, https://claude.ai/artifact/2kS886KRf84XjHRw2EwmqJ). ACCEPTED: the front-led pack contact grammar (decision 3); the layered receiver-reaction architecture (4: presentation transform added to the action's, no hurt clip, no retiming, re-impulse); the generic flash contract (6: peak 0.45, rise 0.15, ~80 ms; recipe overrides intact; no default rim; no restoring the 200 ms white); the capture-view cleanup (8); no global hit-stop (7). **REJECTED by the owner (2026-09-27)**: the polish pass's painted Shadow maw on the whelp (a violet cavity, three pale teeth, open/shut mouth edits: "do not try another mouth edit; the whelp's dark faceless head and bright eyes stay") and the whole-body receiver recoil as a routine hit's language (4, 5, 6, 8 and 10 % all read as a sprite translated, not a body absorbing force). **THE RESET / PROTOTYPE (2026-09-27, `production/qa/evidence/bite-reset/`)** separates the ATTACK VERB (the whelp's body: coil → head-first lunge → contact extension → follow-through → recovery, no facial feature) from the bite's MATERIAL / CONTACT IDENTITY (a small two-fang motif that closes on the champion at the contact); the recoil is OFF by default (0 %) with its curve and dial kept; the bite burst is gone. JAWS' rough A2 MIRRORED SHADOW SNAP (the incoming motif echoed on the biter in Shadow purple, the ownership trace only after the snap begins) is a concept animatic, not production. **THE SECOND PASS (2026-09-27, the reset direction approved, its art rejected as final; `production/qa/evidence/bite-reset-2/`)**: the column-warped strip is retired and the whelp's COIL / COMMIT / CONTACT / FOLLOW-THROUGH are AUTHORED as joint-puppet poses of its own rest frame (a skeleton of 18 handles, rigid moving-least-squares between them; PixelLab used for two pose concepts as references only); the contact motif is the M4 hybrid of the three blind-read candidates (broad crescent jaws + one fang point each, converging on one contact centre at his TORSO edge, not his blade); the leader's lunge travel is 25 % (20 / 25 / 30 filmed); rough A2 is redrawn in the motif's own shape family in Shadow, with no ownership line and no trap sound. PROPOSED, NOT ACCEPTED: the authored whelp attack as the reference enemy attack (root-motion-OFF acceptance filmed: coil → commit → contact → follow-through read forward-and-down, the contact pose "impact / lunge" in every read); the selected motif (static reads: clamp / pinch / bite, never slash); JAWS A2 as a magical reaction. The owner reviews true-speed footage; JAWS is not accepted; Concept D is not built. |
| **Date** | 2026-09-26, reset 2026-09-27 |
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
   bright eyes, no mouth) is intact in every frame. The poses are AUTHORED AS A JOINT PUPPET of the creature's own
   rest frame (`tools/asset-pipeline/v2/umbral_swarm_lunge.py`): a skeleton of 18 handles (head, crown, neck,
   shoulder, back, hip, both hands, an elbow, a knee, both feet, six along the tail) is placed per pose and the image
   deformed between them by rigid moving-least-squares, so the head drives forward and DOWN while the hips trail back
   and the tail streams, the way a 2D puppet rig poses a cut-out. The column-warp pass before it (a per-column shear)
   is retired: it could not turn a crouching body toward its target and read coil → rear up → settle. The 512 px
   canvas still caps the head at x ≈ 70 (every attack frame is drawn in the idle frame's box), so the pose-internal
   extension is authored by the hips and rear moving BACK and the body flattening toward the target; the row's root
   motion (3) amplifies it and never replaces it. The acceptance test is the strip with root motion off
   (`RH_SHOT_NOROOT=1`): the poses alone must read attack-toward-the-target, and the reference is not accepted
   until they do in the owner's eye. PixelLab (`edit_image_pro_flash`, 256 px) produced two pose concepts used as
   references only; no generated frame is in the strip.
   **THE BITE'S CONTACT IS A MOTIF, not a burst**: UPPER and LOWER jaws converging on one contact centre. Each half
   is a broad ember crescent (convex away from the bite line, its horns left open so two of them never close into a
   ring) with ONE strong fang point from its middle to the centre, pale at the tip, on a dark edge (`HuntScreen.Jaw`).
   It was chosen from three rough candidates (opposing crescents / two-fang snap / serrated clamp) and their hybrid
   by static blind reads on the target alone: every reader said "two things closed on him" and named clamp, pinch
   or bite, never slash or claw. It appears OPEN on the frame before the contact and SNAPS shut on it, holds with
   the fang tips crossed for ~16 ms, fades as residue and is gone by ~70 ms (`BitePresentation.MotifOpen` /
   `MotifStrength`, `DrawBiteContact`; ~0.13 of his height, on his TORSO's enemy-facing edge under the hood, at
   `body.X + 0.57 w, body.Y + 0.39 h`: measured from a champion-free plate, because his visible rect's right edge is
   his sword tip and "Right − a little" had put the first motif on the blade). It is not the recoil's accessory
   (`RH_SHOT_NORECOIL` leaves it; `RH_SHOT_NOVFX` hides it).
3. **The pack lunges, front-led, in lockstep** (`Lunge`, in VISIBLE body widths): back 4 % in anticipation over the
   first 75 % of the wind-up, then forward from −220 ms to 25 % at contact (the second pass filmed 20 / 25 / 30 %
   over the authored strip through `RH_SHOT_LUNGE`; the art now carries the approach, so the travel came down from
   30, and the smallest convincing approach is the rule) on a near-linear ease (1.15: a third of the
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
- JAWS (ADR-011's REACTION / TRAP reference) is re-read under this grammar as a MAGICAL REACTION, not a physical
  machine: Concept A2 MIRRORED SHADOW SNAP is the incoming motif's OWN shape family (crescent jaws + fang points)
  in Shadow purple on the creature that bit, ~1.25× the incoming footprint, beginning 0–16 ms after the contact,
  snapping at +16–33, rebounding, gone by ~+130; NO ownership line by default (the visual rhyme is the test) and no
  trap sound (the dry-steel snap belonged to the rejected physical-trap fantasy; audio is a later pass). It exists
  as an animatic (`prototypes/jaws-concepts/animatic.py A2`) over the second pass's plates; nothing of it is
  production, and JAWS is not accepted.
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
