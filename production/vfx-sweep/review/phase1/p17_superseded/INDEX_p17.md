# Phase 1 review film: index (2026-10-04)

The film for the presentation director's review of Phase 1: the ten basic attacks performed, the first champion
missiles, the archetype and identity cues, and the closed references reused on other champions (ByAnySkill). It
follows design.md section 8 ("Film: Part 1 segment A at TEMPO 60 plus the ten swing chapters") and section 9's chapter
table (A, 01-10). Everything was filmed from the working tree on `fix/vfx-fade` (Phase 1 code over 702576d6).

How the takes were made:
- One take at a time: `bash tools/asset-pipeline/films_sweep.sh --keep --mp4 phase1 <take>`. Every take is a named
  `p17_*` line in that script, with RH_SHOT_SEED=7, RH_PRESENT_TRACE=1 and the take's WHOLE build (RH_SHOT_BUILD).
- The sound comes from the trace through film_audio.py and sound_throttle.json. Each mp4 was re-encoded at 720p, CRF 28.
- The pieces were made by `tools/asset-pipeline/review_take.py`; then every frame was deleted
  (`find build/shots/sweep -name '*.png' -delete`). build/shots/sweep holds 0 PNGs.
- Chapter 01 is marked (slow) in design.md: it was filmed at true 60 fps (150 x 1) and its `_slow.mp4` is the 0.25x
  render (`film_audio.py --slow 4`) of the SAME frames. The other chapters are 150 x 2 (30 fps, 5 s).

What each take has:
- `<take>.mp4`: the film with sound. `<take>_slow.mp4` (01 only): 0.25x, the sound slowed like tape.
- `<take>_strip.jpg`: 8 cells at true-speed spacing (about 67 ms of game apart) around the decisive playhead (a swing
  contact, outlined in gold). Cells are half-size arena crops, each labelled with its shot, playhead and offset.
- `<take>_still_<ms>.jpg`: the full 1920x1080 frame at the decisive contact.
- `<take>_trace.txt`: the trace rows of the window, then the per-layer `alloc=` summary of the whole take.
- `<take>.log`: the full trace (a copy is in `production/qa/evidence/vfx-sweep/phase1/`).

Builds (design.md 9 Part 2): an ACTIVE signature takes signature + BLOW@Body + PRESS@Body + JAWS@Shadow; a PASSIVE
signature takes signature + PRESS@Body + BLOW@Body + DRINK@Nature. One swap: the MAGPIE has BRAND@Shadow in JAWS's slot,
so the agnostic BRAND is on film once in Phase 1 (JAWS-agnostic is in the anvil and metronome chapters).

Seek: each chapter starts 0.6 s before the wave's SECOND beat, not the first. The rig replays the wave on a seek, and
the live run has already voiced beat 1's swing by then, so on the replay beat 1's swing draws but is silent and
untraced. Beat 2 onward is clean (notes.md P1.7, open issue).

## Numbers per take (over each film's window; `measure_phase1.py`)

| take | window ms | swings | contact == Strike ms | step-in peak / row gap px | flashes on swing ms | asks per swing ms (max) | generic 0.38 thud / puff | releases | swing-draw sprites max / frames / alloc>0 |
|---|---|---|---|---|---|---|---|---|---|
| A tempo | 4483-6967 (60 fps) | 2 | 2 of 2 | 90/360, 124/497 (25 %) | 2 (0.30 / 110) | 3 (the swing that kills: + enemy down, + trait lit) | 0 / 0 | - | 3 / 18 / 0 |
| 01 seeker | 1633-4117 (60 fps) | 2 | 2 of 2 | 92/368, 89/357 (25 %) | 2 | 1 | 0 / 0 | - | 3 / 20 / 0 |
| 02 anvil | 1633-6600 | 3 | 3 of 3 | 72/359, 70/348 (20 %) | 3 | 1 | 0 / 0 | - | 2 / 33 / 0 |
| 03 metronome | 1633-6600 | 2 (BLOW takes beat 2) | 2 of 2 | 127/507, 130/518 (25 %) | 2 | 1 | 0 / 0 | - | 3 / 20 / 0 |
| 04 tower | 1633-6600 | 3 | 3 of 3 | 79/395, 77/384 (20 %) | 3 | 1 | 0 / 0 | - | 2 / 45 / 0 |
| 05 thornwall | 1633-6600 | 3 | 3 of 3 | 65/326, 63/315 (20 %) | 3 | 1 | 0 / 0 | - | 1 / 6 / 0 (the 2-frame flash) |
| 06 magpie | 1633-6600 | 3 | 3 of 3 | 88/351, 85/340 (25 %) | 3 | 1 | 0 / 0 | - | 2 / 30 / 0 |
| 07 quiver | 1433-6633 | 4 | 4 of 4 | no step (gap 298-313) | 4 | 2 (the pack's bite on the same ms) | 0 / 0 | 4 x sfx_quiver_loose 0.18 | 11 / 111 / 0 |
| 08 chorus | 1633-6600 | 3 | 3 of 3 | no step | 3 | 1 | 0 / 0 | 4 x sfx_chorus_toss 0.16 | 9 / 90 / 0 |
| 09 unbroken | 1633-6600 | 3 | 3 of 3 | no step | 3 | 1 | 0 / 0 | 4 x sfx_unbroken_toss 0.16 | 10 / 95 / 0 |
| 10 oathbound | 1633-6600 | 3 | 3 of 3 | no step (reach; taut on 3 of 3 beats) | 3 | 1 | 0 / 0 | - | 16 / 73 / 0 |

Every swing contact names its champion's own `sfx_<id>_swing_hit` at 0.36, one swing cue per swing ms. Every flash on
a swing ms is the T1 0.30 / 110. "Releases" counts one more than the contacts where the last release falls inside the
window and its contact just after it.

## The takes

### A. p17_a_tempo: THE SEEKER at the fastest TEMPO / every action, every rule
- Seeker, the default fixture build (HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow), `RH_SHOT_TAKE` = the fastest
  TEMPO segment, 2.5 s at 60 fps from 3.4 s. Files: `p17_a_tempo.mp4`, `_strip.jpg` (5117), `_still_5117.jpg`, `_trace.txt`.
- Look for: the step-in at TEMPO 60 (25 % of the gap, home before the next clip), the swing yielding into SPRAY's cast
  at 5567, JAWS answering at 5017. The last swing (6600) kills: its ms also carries `sfx_enemy_down` and the trait cue.

### 01. p17_c01_seeker (slow): THE SEEKER hits with his blade / one hit every beat
- Files: `.mp4` (2.5 s, 60 fps), `_slow.mp4` (0.25x, 10 s), `_strip.jpg` (2200), `_still_2200.jpg`, `_trace.txt`.
- Look for: the thin directional slash + 2 slivers on the contact frame, the 25 % step-in and its smooth return.

### 02. p17_c02_anvil: THE ANVIL hits with a lunge punch
- HARDFACE, BLOW@Body, PRESS@Body, JAWS@Shadow. Look for: the compressed flash + small flat ring, the 20 % step.
  JAWS-agnostic's answer number carries the Shadow outline (9B7BFF).

### 03. p17_c03_metronome: THE METRONOME hits with a running punch / the first hit on each enemy is doubled
- CLOCKWORK, BLOW@Body, PRESS@Body, JAWS@Shadow. Look for: the compressed flash + 2 sparks. BLOW takes beat 2 (2200),
  so the film holds two swings (3700, 5200).
- FIRST BEAT's white outline is in the log at 700 (CLOCKWORK's opening cast, slots 0-2), which is before this film's
  window; the doubled hit is a SKILL hit by Core's rule, never the swing. It is not on screen in this take.

### 04. p17_c04_tower: THE TOWER hits with a hammer slam
- Look for: dust + a flat ring 0.35 AT the creature. The open question from P1.3 stands: at 20 % the hammer lands far
  from the creature in his strip, so judge whether the contact reads detached.

### 05. p17_c05_thornwall: THE THORNWALL hits with a shield bash
- Look for: the broad short flash (two display frames, 6 swing-draw frames in the take), no baked flash in the strip.

### 06. p17_c06_magpie: THE MAGPIE hits with a dagger nick (BRAND@Shadow for JAWS)
- PAYING WORK, BLOW@Body, PRESS@Body, BRAND@Shadow. Look for: the thin bright slash + 1 sliver, the fastest profile,
  and the agnostic BRAND on the front creature from its 2000 tick (Apply / Ash cues).

### 07. p17_c07_quiver: THE QUIVER hits with a bow shot
- Look for: the arrow leaving the bow 200 ms before the beat, the forward slash on the contact. Known: the strip still
  draws its own nocked arrow on frame 5 (P1.4 open issue).

### 08. p17_c08_chorus: THE CHORUS hits with a thrown bone charm
- Look for: one bone charm (not glowing), the clatter of 3 pale slivers.

### 09. p17_c09_unbroken: THE UNBROKEN hits with a flung stone chip
- Look for: the 220 ms lob, a grey chip, 2 untinted chips at the contact. HOLD FAST's +4 SHIELD callouts sit in the
  same window.

### 10. p17_c10_oathbound: THE OATHBOUND hits with a chain lash
- Look for: the chain flicking out from the hand, taut on the beat with the hook at the creature, the 120 ms recoil.

## The references
The five Seeker reference takes were re-filmed at the final Phase 1 code and compared under
`action_regression.py --refs` with both ae20a7fd baselines: **IDENTICAL x 10**
(`production/qa/evidence/vfx-sweep/phase1/regression.txt`).
