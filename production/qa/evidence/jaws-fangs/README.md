# JAWS, SHADOW FANGS: the review package (ADR-011, the final direction, 2026-09-27)

Review page: https://claude.ai/artifact/Vucb1s8aFXvr6A98UXGV6R

**The decision.** The Shadow piranha is rejected for production. Not for its timing: at the game's normal combat scale
a tiny creature carrying a head silhouette, an eye, a mouth, teeth and body/tail detail is below the useful perceptual
budget, and at true speed it becomes coloured motion. JAWS is now a simple reactive-damage effect, SHADOW FANGS:
ENEMY HITS SEEKER → LARGE SHADOW FANGS SNAP ONTO THE ATTACKER → JAWS DAMAGE → FANGS RELEASE → GONE. No literal
creature needs to be identified; the shapes themselves are the effect.

**What is in the build.**
- **Four large simple shapes.** One hand-authored fang (`fxp_seeker_fang`, a broad, slightly curved triangle: dark
  Shadow body, strong violet edge, a small pale tip; `tools/asset-pipeline/v2/seeker_fangs.py`, no PixelLab), drawn
  four times: two from above, two from below, each leaning a little toward the body's centre. Each fang is ~34 % of
  the creature's visible height; the upper and lower fangs together ~68 %. No piranha body, eyes, tails, separate
  head, glint, residue, secondary bites, particles or trails.
- **OPEN outside the silhouette.** At first appearance the points sit ~12 % of the body's height OUTSIDE its top and
  bottom edges, with visible empty space between the upper fangs, the body and the lower fangs: something surrounds
  the creature. Nothing begins inside it. "The body" is the creature's DRAWN silhouette (its opaque box, read once
  off the pose it bites in), not its layout rectangle, which holds a good deal of empty canvas above a crouching head.
- **SNAP.** The open pose stands to 25 ms; the fangs then close rapidly inward (upper down, lower up) and snap at 55 ms
  onto the creature's outer silhouette, the points ~16 % inside its edges; the body stays visibly between them.
- **HOLD.** 85 ms clamped: several frames of TARGET BETWEEN SHADOW FANGS. Then a 6 px outward release and a quick
  fade; gone by 200 ms. No lingering residue.
- **No JAWS target flash.** The fangs are the reaction's hit feedback; there is no grey/white flash under them. The
  global F2 for ordinary attacks is unchanged.
- **The number is delayed.** "−X JAWS" lands 40 ms after the snap, above the creature, clear of the fangs.
- **One cue.** `sfx_seeker_jaws_fangs`: dark, dry, sharp, short; its transient on the snap; no ticks, no metal, no
  bone crunch.
- **Kept.** The skill-id recipe, the actual reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering (a kill falls on the snap), the overlap with SPRAY and HARD HANDS. Core untouched. Tests
  rewritten: 801 green.

| # | File | What |
|---|---|---|
| 1 | `01_*` | CURRENT: the one Shadow piranha (rejected), true speed, sound |
| 2 | `02_*` | NEW: SHADOW FANGS, true speed, sound |
| 3 | `03_*` | the same, muted |
| 4 | `04_*` | repeated triggers at fast tempo |
| 5 | `05_*` | during SPRAY |
| 6 | `06_*` | during HARD HANDS |
| — | `07_trace.md` | the playhead trace: spawn on the bite's frame, the snap and the cue at +55, the number at +95 |

Items 1–3 show the bite at 7000 in the seeded normal-tempo fight, the JAWS trigger a film can hold (the one at 4000
lands before the first frame, and the bite at 5000 finds JAWS still rearming). The answer kills the front whelp there,
so its fall follows the snap.

**Acceptance.** The first normal-speed viewing of film 02 clearly communicates "something sharp / jaws snapped onto
that enemy because it hit me". The viewer does not need to identify a piranha, a bear trap or any specific object;
it only needs to read as a REACTIVE SHADOW BITE / THORNS. If that reads, JAWS is accepted and closed.
