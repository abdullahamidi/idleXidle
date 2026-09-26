# ADR-012: The bite is performed and the hit is received — the pack's spaced attack, its front-led lunge, the champion's recoil, and the quiet default flash

| Field | Value |
|-------|-------|
| **Status** | **Partly Accepted** (2026-09-26, the owner's review of the first battery: `production/qa/evidence/bite-foundation/`, https://claude.ai/artifact/2kS886KRf84XjHRw2EwmqJ). ACCEPTED: the front-led pack contact grammar (decision 3); the layered receiver-reaction architecture (4: presentation transform added to the action's, no hurt clip, no retiming, re-impulse); the generic flash contract (6: peak 0.45, rise 0.15, ~80 ms; recipe overrides intact; no default rim; no restoring the 200 ms white); the capture-view cleanup (8); no global hit-stop (7). PROPOSED, NOT YET ACCEPTED: the whelp's bite art and timing as the reference enemy attack (1–2: the owner: it reads prepare → move → recover, not BITE, and the spacing reads crouch → pop); the receiver recoil's magnitude (the 6 % was too quiet) and the contact accent (5: the burst competed with the recoil); JAWS compatibility. The polish pass (`production/qa/evidence/bite-polish/`, https://claude.ai/artifact/W1w3jG8XpHJLUdbvDCbWaE) addressed those: the whelp's commit and contact poses carry a temporary Shadow maw (character art, painted from its own eyes, clipped to its own head); the wind-up ease is 1.8 and the lunge's commit runs from −220 ms near-linearly; the recoil is 10 % of the champion's visible width with a 1.5 % dip; the contact is a small mark and a compression wedge at his chest edge. On paper a blind reader named the verb ("a bite… the jaw dropped open and I saw pale teeth"), the recoil was not read at any size, and rough Concept A read as the enemy's answer in the views where the bite was seen. Awaiting the owner's true-speed review; JAWS stays paused; Concept D is not built. |
| **Date** | 2026-09-26 |
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
2. **The whelp's attack is re-authored as a BITE** with a distinct contact pose: coil (compress, pull back) → commit
   (the head leads forward, the maw opens) → PRE-CONTACT (thrust, the maw OPEN with three pale teeth, a smear behind)
   → CONTACT (full forward extension, the strongest forward silhouette, the maw SHUT with the teeth meeting) →
   follow-through (compressed on landing, the normal face) → recover. Authored from the creature's own frames by
   deterministic edits (`tools/asset-pipeline/v2/umbral_swarm_bite.py`); identity, proportions, material and lighting
   are untouched. THE SHADOW MAW is character animation art, not an effect: the idle creature has no mouth, so on the
   commit and contact poses the front-lower part of its dark head opens into a violet cavity (negative space in the
   shadow) with pale teeth, placed from the creature's own eyes, scaled by their spacing and clipped to its own
   silhouette (the lower jaw may drop a little below the chin; nothing grows sideways; no snout, no redesign). It
   exists only for the attack. The wind-up spacing (1) gives the maw's two poses ~110 ms each.
3. **The pack lunges, front-led, in lockstep** (`Lunge`, in VISIBLE body widths): back 4 % in anticipation over the
   first 75 % of the wind-up, then forward from −220 ms to 30 % at contact on a near-linear ease (1.15: a third of the
   travel done at −120 ms, ~70 % at −50; a lunge seen crossing the gap, not a position pop), one 3 % overshoot at
   +30 ms, home by +260 ms. The LEADER, the front living creature (the lowest slot alive; the row lays slot 0 nearest the champion,
   and a death moves the lead to the next living slot), gets the full travel; the rest of the pack 40 % of it. All
   creatures share one anticipation and one contact beat, because Core's bite is one simultaneous event; contacts
   are never staggered. The boss performs the same curve at half the share. The contact-time shove is removed.
4. **The champion recoils** (`Recoil`): away from the incoming force, 10 % of his visible width (4, 5, 6 and 8 % were
   not noticed) with a 1.5 %-of-height dip, peaking at +33 ms and home by +130 ms with a cubic ease and no bounce. It is ADDED to the performed action's root motion in the draw
   transform (`_champDrawBox`), so idle, the swing, SPRAY and HARD HANDS keep their clips and their timing. A new
   bite restarts the clock (re-impulse); it never adds to itself, so repeated bites cannot walk him. The effects
   pinned to him ride the same transform.
5. **The contact is directional, small and short.** The bite burst is a mark at his enemy-facing CHEST edge
   (`VfxProfiles.ImpactBite`: 0.16 of his height, ~290 ms, OffsetX 0.34 forward, OffsetY −0.10), and a compression
   wedge with two ember streaks at that edge points back along the force for the recoil's life (`DrawBiteContact`),
   on his measured visible body. Chest, not belt: at belt height his forward edge is his sword hand, and a mark there
   read as something he fired. The centred 0.36 / ~570 ms burst is gone; it competed with the recoil.
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
- The recoil is a presentation offset; the champion's published bounds move with it, so hits and effects aimed at
  him land where he is drawn.
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
