# ADR-012: The bite is performed and the hit is received — the pack's spaced attack, its front-led lunge, the champion's recoil, and the quiet default flash

| Field | Value |
|-------|-------|
| **Status** | **Proposed** (2026-09-26). Built on `fix/vfx-fade` as the combat-presentation foundation the JAWS concept study exposed; awaiting the owner's review of the battery (`production/qa/evidence/bite-foundation/`, review page https://claude.ai/artifact/2kS886KRf84XjHRw2EwmqJ). JAWS stays paused; Concept D is not built. |
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
   (`WindupPhase`, exponent 2.4): the first frame holds for about half the wind-up, the last two rush into the
   contact inside its final ~150 ms. The contact frame is still frame 5 of 8 on the `EnemyStrike` beat.
2. **The whelp's attack is re-authored as a BITE** with a distinct contact pose: coil (compress, pull back) → commit
   (the head leads forward, a smear behind) → CONTACT (full forward extension, the strongest forward silhouette) →
   follow-through (compressed on landing) → recover. Authored from the creature's own frames by deterministic edits
   (`tools/asset-pipeline/v2/umbral_swarm_bite.py`); identity, proportions, material and lighting are untouched.
3. **The pack lunges, front-led, in lockstep** (`Lunge`, in VISIBLE body widths): back 4 % in anticipation over the
   first 70 % of the wind-up, forward to 30 % at contact (fast at the end), one 3 % overshoot at +30 ms, home by
   +260 ms. The LEADER, the front living creature (the lowest slot alive; the row lays slot 0 nearest the champion,
   and a death moves the lead to the next living slot), gets the full travel; the rest of the pack 40 % of it. All
   creatures share one anticipation and one contact beat, because Core's bite is one simultaneous event; contacts
   are never staggered. The boss performs the same curve at half the share. The contact-time shove is removed.
4. **The champion recoils** (`Recoil`): away from the incoming force, 4 % of his visible width, peaking at +33 ms and
   home by +130 ms with a cubic ease and no bounce. It is ADDED to the performed action's root motion in the draw
   transform (`_champDrawBox`), so idle, the swing, SPRAY and HARD HANDS keep their clips and their timing. A new
   bite restarts the clock (re-impulse); it never adds to itself, so repeated bites cannot walk him. The effects
   pinned to him ride the same transform.
5. **The contact is directional.** The bite burst sits forward of centre on his enemy-facing side
   (`VfxProfiles.ImpactBite`, OffsetX 0.20 forward, a little smaller), and two short ember streaks at that edge
   point back along the force for the recoil's life (`DrawBiteContact`), on his measured visible body.
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
