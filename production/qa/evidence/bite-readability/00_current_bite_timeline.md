# The current bite, measured (2026-09-26)

**The creature:** GLOOM WHELP (`umbral_swarm`), the JAWS review's front imp. The darkest body in the game: its mean
brightness is 14/255 (the next darkest, the Dusk Ape, is 34; a Choir swarm is 157).

**What Core says a bite is.** One aggregate `EnemyStrike` per interval (1000 ms at normal TEMPO). It sums the bite
of EVERY living creature (`SoloBattle`: "EVERY LIVING CREATURE BITES"). There is no per-creature bite event. The
whole pack biting at once is the honest picture; the presentation draws all four on the attack clip in lockstep.

**How the clip is timed** (`HuntScreen.EnemyClipSeconds`, `ContactFraction` = 5/8, the composition at 16 fps):

| Phase | Frames of the 8-frame strip | Time relative to contact | How long each frame is on screen |
|---|---|---|---|
| wind-up | 0 → 4 | -900 .. 0 ms, mapped linearly | 180 ms each (a 5.5 fps slideshow) |
| contact | 5 | 0 .. +62 ms | 62 ms |
| follow-through | 6, 7 | +62 .. +187 ms | 62 ms each |
| hold | 7 | +187 .. +300 ms | held |
| next wind-up | 1 (frame 0 is skipped) | from +300 ms (lead 700 ms → wind-up 0.22 → frame 1) | |

- At a 1000 ms interval frame 0 is drawn only on the first bite of a wave. Every later bite plays 1 → 2 → 3 → 4 →
  5 → 6 → 7 → hold → 1: a snap back from the upright recovery pose to the first crouch, 300 ms after every bite.
- The ember tint blends the sprite toward red by up to 38 % across the wind-up (a multiply; on a body of
  brightness 14 it is invisible in practice).

**What the strip's frames DO** (alpha bounds of each 512 px frame; the horizontal centroid and the top row of the body):

| Frame | Head/top row | Centroid x | Read |
|---|---|---|---|
| 0 | 88 | 229 | upright rest |
| 1 | 109 | 229 | head lowering |
| 2 | 128 | 229 | lower |
| 3 | 173 | 231 | crouched |
| 4 | 178 | 234 | crouched, the same |
| **5 (contact)** | **176** | **230** | **the same crouch as frame 4** |
| 6 | 162 | 230 | rising |
| 7 | 133 | 229 | rising |

- The largest body change is frames 1 → 3 (the head drops 64 px of 512), between -700 and -360 ms.
- **Between the frame before contact and the contact frame nothing changes** (top 178 → 176, centroid +4 → 0). The
  moment the damage lands is the moment the body is stillest.
- The horizontal centroid never moves more than 5 px of 512 (1 %): the creature never travels toward the Seeker.
- The fastest motion in the clip is the RECOVERY (frames 5 → 7, the head rises 43 px in 125 ms), which plays AFTER
  the bite, while the red burst is on the Seeker.

**So what the eye gets, at true speed:** a creature slowly ducking for two thirds of a second, then, on the beat, a red
burst on the Seeker with no change on the creature, then the creature standing back up. The verb the body performs
is "duck", and its only fast beat is the recovery. The red burst is doing all the work of saying "you were hit", and
nothing says "by this creature".

**The existing "lunge":** `_enemyLunge` shoves the whole row 40 px toward the Seeker AT contact and decays over ~200 ms
(`dt * 5`). Measured on the silhouette take (`01f`): the front whelp's left edge jumps 31 px on the contact frame and
drifts back over the next 150 ms. It is entirely after the hit: nothing precedes the contact. A jump that begins on
the beat is a HIT grammar (something struck me and I recoiled), not an ATTACK grammar (I wound up and struck).

**The Seeker's swing and the bite overlap.** In this fight the Seeker's swing lands on the front whelp at 6700 and the
pack bites at 7000: the whelp's full-white hit flash (F0, ~200 ms) covers -300 .. -100 ms of its own wind-up, and
the bite's own answer flashes it white again at +17. On a fast TEMPO these overlaps are the norm.
