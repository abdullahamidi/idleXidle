# JAWS readable-clamp pass: a bear trap you can SEE snap shut (ADR-011, 2026-09-25) — AWAITING THE OWNER'S REVIEW

The owner's verdict on the identity pass: the sound direction is better and is now the baseline. The world prop was not
approved. It overcorrected from "metal crocodile" into "mechanical hook / grabber". More important, THE CLOSE WAS NOT
READABLE AT TRUE SPEED: 30° to the stop in 16 ms is one open frame and one shut frame.

This pass changes only:
- the WORLD PROP;
- the SNAP TIMING (with the answer's on-screen feedback moved onto the stop);
- the depth order around the caught limb;
- the clack's position in the cue.

These are untouched:
- Core readiness;
- target resolution;
- reaction layering as a system;
- tether tracking;
- the retract concept;
- dock truth;
- SPRAY, HARD HANDS, REPAY, NET and IRON.

Every film is the real game, SEEDED (`RH_SHOT_SEED=7`), so CURRENT and NEW show the same fight:
- CURRENT is the identity pass (`build/shots/jaws/identity`), heard with its own clack at 17 ms (`film_audio.py --cue`).
- NEW is `build/shots/jaws/readable`.
- The recipe is `build/shots/jaws/films_jaws.sh`.

## What changed

| | CURRENT (identity pass) | NEW (this pass) |
|---|---|---|
| Object | a big riveted box with two thin quarter-arc arms: "box + two hooks" | an upright **bear trap**: a short base bar (two pins 5 source px apart, a coil under it, the chain eye at its rear end) and two **broad forged "( )" jaws** with three large teeth each. The jaws are ~75 % of the silhouette |
| Open vs shut | arms 30° off a quarter arc: open and shut look alike at play size | open: a wide toothed cup (34° each); shut: "( )" standing round the limb, tips apart where the limb comes out |
| The close | 16 ms: one open frame, one shut frame | **50 ms**, the travel as the square of the time: open (+0), still open (+17, 11 %), about half shut (+33, 44 %), **SHUT (+50)**; one 4° rebound over 30 ms; lock |
| Pins | two, 57 px apart at a plate's corners (a tall bracket) | two, 15 px apart on the base (a compact pair); each jaw turns about its own pin |
| Depth | arms drawn over the creature | the NEAR jaw is drawn BEHIND the caught creature and the far jaw in front: the limb is between the jaws |
| The answer | number and flash on the bite's frame, while the arms were already shut | on the **stop** (+50): the clack, the reflected number (and its critical cue), the soft flash, and a kill's fall. The enemy's bite keeps its own t 0 |
| Sound | the clack at 17 ms, on top of the bite | a tiny latch release at 0 ms, the same dry-steel **CLACK at 51 ms** on the stop, then chain ticks (200 ms). Same material: 87 % of the energy above 1.2 kHz |
| Retract | at 150 ms, gone at 250 | at 185 ms (the jaws hold ~135 ms after the stop, as before), gone at 285 ms |
| Size | 0.42 of the creature's height long | 0.40 of it TALL (52-96 px), chosen from 85 %, 100 % and 115 % (`13`) |

## How the object was chosen (`20`, `24_blind_checks.md`)

Three silhouettes went OPEN → LOCK at play size. Each was read blind against the words HOOK, CLAW, GRAPPLING HOOK,
PINCER, TRAP, BEAR TRAP, CLAMP and MOUTH:

- **A, the upright bear trap:** "looks like a bear trap"; best words BEAR TRAP and CLAMP; "two curved, toothed arms swing
  shut around a central post". **Chosen.**
- **B, an asymmetric mantrap along the chain:** TRAP and CLAMP. But jaws that open sideways, hinged at the back, read as
  a mouth: every earlier head and clamp failed that way.
- **C, a spring yoke:** BEAR TRAP and CLAMP, but "fang-like jaws on a stand".

The first two sheets were rejected before these three. Big crescent jaws hinged at the back read as a toothed "C", a
Pac-Man mouth (kept in the scratch record).

## The two questions (the owner's acceptance)

**1. Does it look like a trap?**
- No blind reader called it a hook, a claw, a grappling hook or a pincer.
  - On the final in-game frames, Opus said "a bear trap (a spring-loaded leg trap on a chain)", choosing BEAR TRAP and
    TRAP. Sonnet said "a hinged clamp / vise mouth", choosing CLAMP and BEAR TRAP.
  - The CURRENT clamp read as "a chained metal clamp or pincer".
- In game (`26`, `11a`), it reads as a trap clamped on the imp's forearm: the near jaw behind the claw, the far jaw in
  front.

**2. Can I clearly see it SNAP SHUT at true speed?**
- The play-size frame sheet (`11a`) shows it: a flat open cup at +0 and +17, jaws rising at +33, "( )" at +50, then the
  rebound and the lock. The CURRENT sheet (`11b`) barely changes.
- Blind readers of the six 1:1 frames:
  - CURRENT: "I couldn't see a clear closing snap … it never looks fully closed".
  - NEW: "a closing motion is visible … two moving parts: the left and right jaws".
- The honest caveat: both readers said it "happens fast, over about two frames". That is the spring spacing asked for
  (slow first degrees, most of the travel near contact). Both put "fully closed" at frame 5, one frame after the actual
  stop (frame 4 is also the flash frame). Muted at true speed (`01a`, `01d`, `02e`), the owner's eye is the test.
- **A problem caught and fixed.** The first draw order put the FAR jaw behind the creature, and it vanished on the dark
  imp: "one jaw rotating upright, not two jaws snapping together". The NEAR jaw now goes behind instead. It lies mostly
  outside the creature, so the far jaw stays visible and both jaws are seen snapping.

## The answer on the stop (presentation scheduling only)

- **What the fight does:** it resolves the reflected blow at the bite. Nothing there changed: the non-regression and
  the Core tests prove it.
- **What the screen does:** it holds the answer's JAWS-specific feedback as `ReactionEcho`s and releases them on the
  stop (`reaction-clamp`, `number` and `flash` all at +50 ms in `15_timelines.md`).
- **When a seek rewinds past the bite:** the held feedback is dropped.
- **The death-order case (reported, as the brief asked).** When the answer KILLS the creature, the fight kills it at the
  bite, but on screen the trap has not shut yet.
  - The creature is drawn STANDING in the jaws until the stop (`_deathDeferred`), then falls (its clip, sound and plume).
  - The jaws hold 50 ms, let go and are reeled home without dragging it (`09a`, `09b`: it falls on the stop frame).
  - The Seeker felled by the same bite falls at t 0, as before. JAWS still shuts, its number shows on the stop, and the
    chain goes slack (`10a`, `10b`).
- **A timing fix found by the trace.** The playhead runs in thirds of a millisecond, so the stop frame reads 49.99 ms,
  and the answer fired one frame late (+66). The stop step now allows half a millisecond.

## Evidence

Stacked films are CURRENT above and NEW below, muted. The `sfx` films carry the rendered game audio.

| File | What |
|---|---|
| **`01a`** | **TRUE SPEED, MUTED**, CURRENT above and NEW below: can you see it close? |
| `01b` / `01c` / `01d` | new with sound effects; new with music; new muted |
| **`01e`** | **TRUE SPEED WITH SOUND**: CURRENT, then NEW |
| `02a` / `02b` | the CLOSE CROP (belt, chain, trap, target): true speed and 4× slower, stacked |
| `02c` / `02d` / `02e` | the new close crop with sound; 4× slower with sound (the clack on the shut frame); true speed muted |
| `03a` | effects only (the champion hidden) during HARD HANDS |
| `04a` / `04b` | during SPRAY: stacked, and the new with sound |
| `05a` / `05b` / `05c` | during HARD HANDS: stacked, new with sound, every frame |
| `07a` / `07b` / `07c` | repeated combat: 16 s stacked; new 16 s with music; fast TEMPO with music |
| **`07d`** | **CURRENT then NEW, 16 s of repeated combat with music and sound** |
| `09a` / `09b` | the killing answer: the creature stands in the jaws and falls ON the stop |
| `10a` / `10b` | the Seeker falls on the bite |
| **`11a`** / `11b` | **OPEN → EARLY → MID → SHUT → REBOUND → LOCK at play size** (1:1, and 2× below): new, then current |
| `12a` / `12b` | the anchor overlay: the belt and the clamp point |
| `13` | size at play size: current, and the trap at 85 %, 100 % (chosen) and 115 % |
| `15_timelines.md` | every trigger's life (`reaction_timeline.py --report`) |
| `16_non_regression.md` | SPRAY and HARD HANDS with the layer on and off |
| `20` | the three silhouettes, OPEN → LOCK at play size, with their blind readings |
| `22` | the parts on their one canvas, with the pins, the chain eye and the clamp point |
| `23` | the art on the runtime curve, frame by frame |
| `24_blind_checks.md` | every blind reading, including the two it caught |
| **`26`** | **the LAYERING VIEW**: the limb between the jaws, 3× (current vs new) |
| `25` / `25a` / `25b` | the snap's waveform (the shut frame and the peak marked); the identity cue and the new cue as WAV |

## Performance, tests and cost

- **Per frame, from `reaction-draw`:** up to 47 sprites (43 with one target), 11 link accents and 0 bytes allocated.
  - The near jaw's extra draw sits behind its creature: one sprite, the same count.
  - The identity pass drew up to 49.
- **SPRAY and HARD HANDS (`16`):** NEW with the layer ON matches NEW with it OFF: IDENTICAL, 247 moments.
  - It also matches the identity pass's layer-on film (IDENTICAL).
  - Against the identity pass's layer-off film there is 1 difference: the known 3 px capture noise at one SPRAY
    contact, in that film, recorded last pass.
- **Tests:** JAWS 21, rewritten for:
  - the visible close (three frames, the travel near contact, open unmistakably not shut);
  - one 3-5° rebound;
  - near and far pins as a compact pair;
  - one jaw behind the creature;
  - the answer on the stop;
  - one canvas.
- **Gates:** Game 799, Core 1,888, Integration 2; -warnaserror 0/0; check_all green.
- **PixelLab:** no generations this pass.
- **Sound:** only `sfx_seeker_jaws_snap` changed (the clack moved to 50 ms). The approved cues are byte-identical.

## Still open

- **The owner's eye at true speed**, muted and with sound. The readers call the close "fast, over about two frames".
  - If it needs another frame: `CloseMs` 60-66 is the dial. The clack follows it via `JAWS_CLAMP_S`.
  - If the stop frame should read cleaner: the flash one frame after the stop.
- **The retimed clack is unheard.**
- **The far jaw on very dark creatures.** It is in front now, but its dark iron against a black imp relies on its outline
  and teeth.
- **`tools/check_boot.sh`** still expects `save.pre-v8` (pre-existing since `8314b03`, not touched).
