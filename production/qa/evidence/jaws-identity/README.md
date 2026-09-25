# JAWS identity pass: a mechanical hunting clamp, never a head (ADR-011, 2026-09-25) — AWAITING THE OWNER'S REVIEW

The owner approved JAWS' architecture, timing and motion. The remaining defect was SEMANTIC IDENTITY. The polish pass's
world prop read at true speed as a metal crocodile: a round hub where an eye would be, and two long toothed plates
tapering to one "nose". Its cue read as bone crunch.

This pass keeps every approved system and changes only:

- the world prop's SHAPE LANGUAGE (new art and two pins);
- the cue's TIMBRE;
- two light dials: no sparks, and a softer glint.

These are unchanged:

- `ReactionArmed`;
- the reaction layer;
- target selection;
- tether tracking;
- the recoil direction (the yank toward the Seeker);
- the retract lifecycle;
- dock timing;
- death handling;
- the SPRAY and HARD HANDS isolation.

NET, IRON and REPAY are untouched.

**JAWS is a SPRING-LOADED HUNTING CLAMP**, a mantrap mechanism. "JAWS" names its two opposing clamp arms.

Every film is the real game, SEEDED (`RH_SHOT_SEED=7`), so CURRENT and IDENTITY show the same fight:

- CURRENT is `build/shots/jaws/polish`, the committed polish pass. It is heard with its own old snap
  (`film_audio.py --cue`).
- IDENTITY is `build/shots/jaws/identity`.
- The recipe is `build/shots/jaws/films_jaws.sh`.

## What changed

| | CURRENT (polish pass) | IDENTITY (this pass) |
|---|---|---|
| Object | a "trap head": chain eye, round hub with a bolt, two long serrated jaws tapering to one tip; shut, a lens with a zigzag of white teeth | a clamp: rectangular shackle, riveted spring box with its coil spring outside along the top, tall reinforced plate, a hinge flange at each corner, and two short forged crescent arms; shut, upper arm, then the caught limb, then lower arm |
| Pivots | ONE hub pivot behind both jaws (read as an eye) | each arm on its OWN pin at a plate corner (`UpperPivot`, `LowerPivot`), as a bear trap's jaws hinge at the two ends of its base |
| Teeth | two mirrored rows of six, bright steel, meeting on a seam (a grin) | three spikes per arm on the inner edge, the lower row one pixel along (interleaved, never mirrored), a step above the iron, never white |
| Motion | rigid rotation, open 30° to a 3° stop by 16 ms, 5° recoil | the SAME curves, each arm about its own pin; the housing never turns |
| Light | tether streak, teeth glint 0.9 for 70 ms (the teeth flashed white), 3 sparks from the "mouth" | tether streak, one tooth-edge glint at 0.6 for 60 ms, **no sparks** (two hinge sparks were reviewed at play size and were invisible 3–4 px specks) |
| Size | head 0.53 of the creature's height long | clamp 0.42 (it is about as tall as it is long), chosen at play size from 85 %, 100 % and 115 % (`13`) |
| Sound | a spring tick, then an "iron clack" built from a saturated crunch and a pitch-dropping thump (81 % of its energy under 300 Hz, ringing ~70 ms): bone | a latch tick, then a DRY STEEL CLACK at 17.3 ms (inharmonic partials dying in ~12 ms, the second arm's stop 3.5 ms later, a muted leather contact ~20 dB under), then link ticks: 91 % above 1.2 kHz, −30 dB within 24 ms |

## How the object was chosen (the owner's §9 and §19)

1. **Three silhouettes at play size** (`20`), monochrome, open, shut and shut on a limb, each read BLIND:
   - **A, a compact bear-trap clamp:** "bear trap jaws". Chosen.
   - B, a crossed-lever spring clamp: "a ninja throwing star". Rejected.
   - C, a round spring drum with crescent arms on one pivot: "a crab claw / pincer". Rejected.
2. **PixelLab concepts** (`21`, three generations; reference only, none used):
   - `42696d59`: a crescent-armed grabber on a sprung bar;
   - `7e776e95`: a sprung rat-trap on a plate, top-down;
   - `4513dcd7`: a riveted box with a shackle.

   None has the exact pins a rigid part needs, the palette is wrong, and two are the wrong perspective. They informed
   the housing.
3. **A drawn on the Seeker's grid** (`tools/asset-pipeline/v2/seeker_jaws.py`, `22` parts and pins, `23` poses). The
   arms' bands are snapped from polygons; the teeth are placed by hand.
4. **Four colour rounds with fresh blind readers**, each fixing what a reader saw (`24_blind_semantic_check.md`):
   - a V yoke plus a ribbed rod read as a **crossbow**;
   - a round knuckle over the C read as an **eye**;
   - a window of coil bars on the box read as a **robot visor**;
   - a dark square pin in a lit square read as an **eye socket**.

## The blind semantic check (`24`)

Fresh model readers were each given one image with a neutral name, and asked "what object is this?", for alternatives,
and whether any part is a face, eye or mouth. The images had no name, no icon, no particles and no Source colour.

| | First impressions |
|---|---|
| CURRENT head (control) | "a pair of chomping jaws / mandibles … the small dark circle is an eye" |
| IDENTITY clamp | Sonnet: **"mechanical bear trap / clamp jaws"**. Opus: **"a robotic claw or grabber"** ("reads as a machine part more than a creature"). Haiku: **"trap with opening jaw"** |

A weak residue remains. An open C made of two toothed arms still reads a little mouth- or mandible-like to some
readers, in isolation. In play the caught limb fills the C (`26`). This check is a proxy, not a playtest.

## Answers to the owner's questions

- **Does the world prop still resemble an animal or robot head?** Not to any blind reader's first impression. See
  `26`: the stop and the hold side by side, 2×.
- **Does it read as a mechanical hunting clamp?** Two of three readers name it (bear trap / clamp; trap). The third says
  robotic claw / grabber, with "a pincer trap or clamp on a metal housing" second.
- **Is the housing visibly mechanical?** Yes: a shackle, a riveted box, an external coil spring, a plate and hinge
  flanges. It is the heaviest mass, and it reads first in the close crop (`02a`, `02c`).
- **Are the moving arms separate rigid clamp pieces?** Yes. Each turns about its own pin, and the housing never moves
  against the line (`23`; `02b` and `02d` 4× slower).
- **Does the sound read as steel, not bone?** Measured: the bone thump and crunch are gone, and 91 % of the energy is
  above 1.2 kHz (`25`). The ear is the owner's: `25a` is the current cue and `25b` the identity cue; with the picture,
  see `01e` and `07d`.
- **Do the visual close and the main transient coincide?** The arms reach their stop at 16 ms and are drawn shut on the
  next frame (+16.7 ms). The strongest transient is at 17.3 ms (`25`; `02d`, 4× slower with sound).
- **Is belt → tether → clamp → retract still coherent?** The lifecycle is unchanged. During the retract the compact
  housing is reeled back along the chain and reads as a device going home (`02e` every frame, `15` timelines). A
  creature that falls is let go (`09`); a champion who falls leaves the chain slack (`10`).
- **Is the approved architecture unchanged?** Yes: no system changed. SPRAY and HARD HANDS were compared in one seeded
  fight. The clamp with the layer ON is IDENTICAL to the polish films, both with the layer OFF (247 moments) and ON
  (248). The capture rig showed its own noise, recorded in `16`:
  - two layer-OFF films differ by 3 px at one contact;
  - one ON take lost 233 ms of draws and moved one HARD HANDS leap. Re-filmed, it was identical.

## Evidence

Stacked films are CURRENT above and IDENTITY below, muted. The `sfx` films carry the rendered game audio.

| File | What |
|---|---|
| `01a` | CURRENT vs IDENTITY at TRUE SPEED |
| `01b` / `01c` / `01d` | identity with sound effects; identity with music; current with its own sound |
| **`01e`** | **CURRENT then IDENTITY, true speed, AUDIO ON**, one after the other (the key A/B) |
| `02a` / `02b` | the CLOSE CROP (belt, chain, clamp, target): true speed and 4× slower |
| `02c` / `02d` | the identity close crop with sound: true speed and 4× slower (the clack on the shut frame) |
| `02e` / `02f` | every frame: identity and current |
| `03a` / `03b` | effects only (champion hidden) during HARD HANDS: the pair, and every frame |
| `04a` / `04b` | during SPRAY: the pair, and identity with sound |
| `05a` / `05b` / `05c` | during HARD HANDS: the pair, identity with sound, and every frame |
| `07a` / `07b` / `07c` | repeated combat: the 16 s pair; identity 16 s with music; fast TEMPO with music |
| **`07d`** | **CURRENT then IDENTITY, 16 s of repeated combat with music and sound** |
| `09a` / `09b` | the killing answer |
| `10a` / `10b` | the champion falls on the bite |
| `12a` / `12b` | the anchor overlay: the belt (cyan), the clamp point (magenta), on the front creature and during HARD HANDS |
| `13` | the size at play size: the current head, the clamp at 85 %, 100 % (chosen) and 115 % |
| `15_timelines.md` | every trigger's life (`reaction_timeline.py --report`) |
| `16_non_regression.md` | SPRAY and HARD HANDS with the reaction layer on and off |
| `20` | the three silhouettes A, B and C at play size, with their blind readings |
| `21` | the three PixelLab concepts (reference only) |
| `22` | the source-art parts on their one canvas, with the two pins, the shackle and the clamp point |
| `23` | the arms open to the stop, rigid rotation about each pin, with play size below |
| `24` | the blind semantic check: the images and the record |
| `25` / `25a` / `25b` | the snap's waveform with the shut frame and the peak marked; the two cues as WAV |
| `26` | in game, 2×: the current head and the identity clamp at the stop (+33 ms) and held (+100 ms, no light) |

## Performance, tests and cost

- **Per frame (identity films, from `reaction-draw`):** up to 49 sprites (44 with one target; a second target gets its
  own smaller clamp), 11 link accents, and 0 bytes allocated. The polish pass drew up to 47.
- **PixelLab:** 3 generations this pass (`42696d59`, `7e776e95`, `4513dcd7`), all concepts, none used.
- **Sound:** `sfx_seeker_jaws_snap` is regenerated. The approved SPRAY and HARD HANDS cues, `sfx_cast` and `sfx_hit`
  are byte-identical.
- **Tests:** four new identity tests:
  - each arm turns about its own hinge and the housing never turns;
  - the recipe has no hub pivot and no "head";
  - sparks are few and come from the hinges;
  - the parts share one canvas.

## Still open

- **The snap is unheard.** `design/audio/seeker-jaws-audio-brief.md` says what to listen for; the leather layer under
  the clack is the dial if it sounds sterile.
- **The residual mouth reading** of an open C in isolation. In play the limb fills it; a real player's look is the check.
- **`tools/check_boot.sh`** still expects `save.pre-v8`. This is pre-existing since `8314b03` and not touched.
