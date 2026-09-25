# Prototype: JAWS concept study (literal trap vs. stylised reaction)

**Status: concluded (2026-09-25). Findings below. Awaiting the owner's direction.**
- Nothing here is read by the game. No production code, asset, sound or Core file changed.

## Hypothesis

The upright bear trap (readable-clamp pass) failed review, and the literal PHYSICAL METAPHOR may be the reason.
- A deployed trap reads because it is placed and armed BEFORE its trigger.
- JAWS has no placement, so a literal trap must establish its identity (housing, springs, chain) in ~100 ms.
- A stylised Shadow manifestation might carry CAUSE ("the Seeker did this"), REACTION ("in answer to the bite") and
  IDENTITY ("jaws / a trap snapped") with less physical burden.

Three rough concepts test this:
- **A. Shadow Jaws.** Dark serrated crescents form round the attacker, SNAP on the bite, and break into shadow. One
  brief thread comes from the Seeker's belt.
- **B. Shadow Tether Counter.** One line lashes from the Seeker, catches the attacker's limb, and snaps taut on the
  bite. It recoils, then reels back.
- **C. Trap Sigil.** A dashed ring telegraphs, two flat serrated arcs converge on the bite with a contact flash, and
  the sigil fractures.

## How to run

Build the game once with `dotnet build`. Then, from the repo root:

```
bash prototypes/jaws-concepts/film_plates.sh        # clean plates of the seeded fight -> build/shots/jaws/plates/
bash prototypes/jaws-concepts/render_concepts.sh    # A/B/C drawn over every plate    -> build/shots/jaws/concepts/
PYTHONUTF8=1 python prototypes/jaws-concepts/build_evidence.py production/qa/evidence/jaws-concepts <blind dir>
```

- `animatic.py` draws one concept over one plate. Its docstring has every key time.
- It also writes a copy of the plate's trace with the TEMP cue (the approved dry-steel `sfx_seeker_jaws_snap`) timed
  onto the SLAM frame, which `tools/asset-pipeline/film_audio.py` renders.
- `film_plates.sh` moves the legacy row ring out of the BUILD OUTPUT for the takes and puts it back on exit.
- Re-filming shifts frame-to-playhead by the startup jitter, so re-render after every re-film.

## Findings

Evidence: `production/qa/evidence/jaws-concepts/` (films, frame sheets, `08_blind_checks.md`).

1. **The literal trap is the wrong metaphor for JAWS.**
   - Deployed traps are established before the trigger: Dead Cells' Wolf Trap is thrown down and waits; Caitlyn's
     Yordle Snap Trap is placed and arms after a delay. The snap only needs a few frames because the object was seen
     for seconds.
   - JAWS has no prior object and its trigger is an attack from range. A literal trap must be conjured and identified
     inside the reaction.
   - That is the physical burden, and every attempt slid to a neighbouring read: crocodile head, hook, bear trap.
2. **Stylised is lighter, but none of A/B/C read as a COUNTER in the blind reads (7 readers, a control included).**
   - The control shows why: the enemy's bite reads as "a red burst on the hunter in the same frame". The creature
     barely moves.
   - A retaliation cannot read while its provocation is invisible. This is outside JAWS, and it bounds every concept.
3. **CAUSE comes from a line out of the Seeker.** A's thread and B's tether were given to the hunter. C "just
   appeared".
4. **Anticipation 17–50 ms before contact does not help.**
   - Dim pre-roll (A's forming, C's telegraph) was not seen at speed.
   - Bright pre-roll (B's lash) was seen and made the Seeker the initiator: "a cast".
5. **A reads as a MOUTH** ("a monster's mouth more than a metal trap"). That is the rejected crocodile family.
   - Top/bottom jaws read as a mouth. C's sideways "( )" convergence reads as "snapped shut around".
6. **The fight's own white hit flash (~150 ms, full-white body) is the loudest thing on screen.** It melts A's teeth
   and drowns C.
7. **Overlaps.**
   - SPRAY is sequential: every concept is gone before the knives fly.
   - With HARD HANDS, B reads as a combo ("hook, then dash"). That is a false causal link.

**Recommended direction (for the owner's decision, not built):**
- **A stylised Shadow answer, not a literal trap.**
  - A sideways "( )" serrated shadow snap closes on the attacker's limb ON the contact frame, never before it.
  - One Shadow line shows the answer's path from the Seeker, drawn with or after the snap, not as a lash ahead of it.
- **Give it the trap's "before" through JAWS' ARMED state.**
  - The state already exists: Core reports `ReactionArmed`, and the rearm is ~2.8 s.
  - For example, a small closed-jaw Shadow mark at the belt while armed, dim while rearming.
  - It is paid for in seconds of presence, the way a placed trap is.
- **Two prerequisites outside JAWS decide whether any counter reads.**
  - The enemy's bite must read as the creature's act.
  - The reflected hit's white flash must sit under the manifestation.
- **Suggested next test:** one more concept-only animatic (D) of that direction from the same plates, with and without
  a readable provocation, blind-read the same way.
