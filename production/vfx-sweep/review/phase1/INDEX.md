# Phase 1 review film: index (2026-10-04, the director's p1_* plan)

This film is for the presentation director's review of Phase 1. It covers the ten basic attacks performed, the first
champion missiles, the archetype and identity cues, and the closed references reused on other champions (ByAnySkill).
It replaces the P1.7 film. That film is kept for history in `p17_superseded/`, with its own `INDEX_p17.md`.
Everything was filmed from the working tree on `fix/vfx-fade` (the Phase 1 code over 702576d6).

## How the takes were made
- Each take was filmed on its own with `bash tools/asset-pipeline/films_sweep.sh --keep --mp4 phase1 <take>`. Every take
  is a named `p1_*` line in that script, with RH_SHOT_SEED=7, RH_PRESENT_TRACE=1, the disk guard, and the take's whole
  four-slot build in RH_SHOT_BUILD (at most 2 Active + 2 Passive). Each take is 150 frames at stride 2 (5 s of game at 30 fps).
- The sound comes from the trace through film_audio.py and sound_throttle.json. Each mp4 is 720p at CRF 28.
- The pieces were made by `review_take.py`. The 8-cell strips are spaced 2 frames apart, which is 67 ms of game, so they
  show true speed. The trace excerpt now also keeps the `swing-*` rows. After each take, every frame was deleted, so
  build/shots/sweep holds 0 PNGs.
- `_slow.mp4` files are 0.25x renders (`film_audio.py --slow 4`) made from the same frames, cut to a single swing. The
  source is 30 fps, so a 0.25x render plays at about 7.5 distinct pictures per second.
- **Two rig fixes were needed before beat 1 could be filmed.** A seek rebuilds the replay after the live run has
  already crossed beat 1. Before the fixes, that swing drew but made no sound and left no trace row, and FIRST BEAT's
  outline was gone on the replay. A rebuild now forgets the swing's voiced ms (`SwingPerformance.ForgetVoiced`) and
  rebuilds FIRST BEAT's struck set from the rewound wave. This is rig-path code only (the DevSeek block). The five
  references are IDENTICAL x10 against both ae20a7fd baselines (below).
- **Disregard on every take:** the "WELCOME BACK / +140 GLEAM" notice sits over the top-left of the arena. It is the
  fixture save's offline notice and is not Phase 1 work.

Files per take: `<take>.mp4` (with sound), `<take>_strip.jpg` (the gold-outlined cell is the decisive playhead),
`<take>_still_<ms>.jpg` (full frames), `<take>_trace.txt` (the window's rows plus per-layer alloc), and `<take>.log` (the
full trace; also in `production/qa/evidence/vfx-sweep/phase1/`).

## Numbers per take (over each film's window; `measure_phase1.py` -> `evidence/.../phase1/measured_p1.md`)

| take | window ms | swings | contact == Strike ms | step-in peak / row gap px | flashes on swing ms | cues on a swing ms (max asks) | generic thud / puff | releases | swing-draw sprites max / frames / alloc>0 |
|---|---|---|---|---|---|---|---|---|---|
| A seeker t60 | 633-5600 | 5 | 5 of 5 | 90/360 (25 %) | 5 x 0.30/110 | swing 0.36 (3 on the kill ms: + enemy_down 0.36, trait_lit 0.16) | 0 / 0 | - | 3 / 50 / 0 |
| 02 anvil | 133-5100 | 3 | 3 of 3 | 72/359 (20 %) | 3 x 0.30/110 | sfx_anvil_swing_hit 0.36 (1) | 0 / 0 | - | 2 / 33 / 0 |
| 03 metronome | 133-5100 | 1 (CLOCKWORK takes beat 1, BLOW beat 2) | 1 of 1 | 127/507, 130/518 (25 %) | 1 x 0.30/110 | sfx_metronome_swing_hit 0.36 (1) | 0 / 0 | - | 3 / 10 / 0 |
| 04 tower | 133-5100 | 3 | 3 of 3 | 79/395 (20 %) | 3 x 0.30/110 | sfx_tower_swing_hit 0.36 (1) | 0 / 0 | - | 2 / 45 / 0 |
| 05 thornwall | 133-5100 | 3 | 3 of 3 | 65/326 (20 %) | 3 x 0.30/110 | sfx_thornwall_swing_hit 0.36 (1) | 0 / 0 | - | 1 / 6 / 0 (the 2-frame flash) |
| 06 magpie | 133-5100 | 3 | 3 of 3 | 88/351 (25 %) | 3 x 0.30/110 | sfx_magpie_swing_hit 0.36 (1) | 0 / 0 | - | 2 / 30 / 0 |
| 07 quiver | 133-5100 | 4 | 4 of 4 | no step (gap 298-313) | 4 x 0.30/110 | sfx_quiver_swing_hit 0.36 (2: the pack's bite sfx_hit 0.30 shares a ms) | 0 / 0 | 4 x sfx_quiver_loose 0.18 at beat-200 | 11 / 112 / 0 |
| 08 chorus | 133-5100 | 3 | 3 of 3 | no step (gap 391-402) | 3 x 0.30/110 | sfx_chorus_swing_hit 0.36 (1) | 0 / 0 | 4 x sfx_chorus_toss 0.16 at beat-200 | 9 / 91 / 0 |
| 09 unbroken | 133-5100 | 3 | 3 of 3 | no step (gap 307-318) | 3 x 0.30/110 | sfx_unbroken_swing_hit 0.36 (1) | 0 / 0 | 4 x sfx_unbroken_toss 0.16 at beat-220 | 10 / 95 / 0 |
| 10 oathbound | 133-5100 | 3 | 3 of 3 | no step (reach; taut on 3 of 3 beats) | 3 x 0.30/110 | sfx_oathbound_swing_hit 0.36 (1) | 0 / 0 | - | 16 / 73 / 0 |

- Hierarchy (the same mix in every take): basic swing hit 0.36, under SPRAY's contact (0.50) and HARD HANDS' (0.55),
  and above PRESS's tick (0.28; 0.17 under an action). JAWS' bite is 0.42 and the pack's bite (sfx_hit) is 0.30.
- Alloc: `swing-draw` and `reaction-draw` are 0 on every frame of every take. `field-draw` (PRESS) allocates 13-14 MB
  on 4 frames at first use. This is the known open issue carried from P0.1. `mark-draw` (BRAND on the magpie)
  allocates 1 128 B on 1 frame at first use. This is new to this film, is not steady-state, and is recorded in notes.md.
- The "releases" column counts one more release than there are contacts, because the last release falls inside the
  window and its contact falls just after it.

## The takes

### A. p1_A_seeker_t60: the spine. THE SEEKER at the fastest TEMPO (also chapter 01, "THE SEEKER hits with his blade")
- Build: HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow, posed like ref_fast (RH_SHOT_TAKE = films_sweep.sh's `$TEMPO`,
  which ref_fast also uses; it carries `volley` after the seven items the plan names). The take seeks
  `skill:volley_spray` (SPRAY at 2800) with a 2.2 s lead, so the window is 633-5600.
- Files: `p1_A_seeker_t60.mp4`, `_slow.mp4` (0.25x of the first whole swing, playhead 1100-1900, which is chapter 01),
  `_strip.jpg` (around the swing at 2100, the one just before SPRAY), `_still_2100.jpg` (the cut lands) and
  `_still_2233.jpg` (home, one frame before SPRAY's first frame), `_trace.txt`.
- Look for:
  - The 25 % step-in is home before each action. The step reaches x=0 at 2217 and SPRAY's clip starts at 2250; 3850
    before HARD HANDS at 3933; 5217 before the second SPRAY at 5250. Each swing's handoff retimes its exit to the next
    clip.
  - Every cut lands on its Strike's ms (5 of 5) at the creature.
  - The swing cue (0.36) sits under SPRAY's 0.50 and HARD HANDS' 0.55.
  - There is no 40 px push and no fx_weakhit puff (0 / 0).
  - PRESS ticks at 2000 and 4000, and JAWS answers at 1000, 3000 and 5000 (the bite cue at +317). These are untouched;
    see the regression below.

### 02. p1_02_anvil: THE ANVIL hits with a lunge punch
- Build: HARDFACE, BLOW@Body, PRESS@Body, JAWS@Shadow. The take seeks beat 1 (700) with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (700), `_still_700.jpg` (the contact), `_still_1367.jpg` (the JAWS answer number), `_trace.txt`.
- Look for:
  - The 20 % lunge (peak 72 of 359 px).
  - The compressed flash plus the small flat ring.
  - The padded-fist cue (sfx_anvil_swing_hit 0.36).
- Agnostic tier:
  - JAWS answers the pack's bites at 1000 and 4000 inside the window. The plan allowed tuning RH_SHOT_ENEMY for this,
    but no tuning was needed. JAWS draws the same Shadow bite. Its numbers carry the Shadow outline (outline=9B7BFF,
    at 1367 and 4367); that outline is the only difference.
  - PRESS's picture and cue are the Seeker's (sfx_seeker_press_tick).

### 03. p1_03_metronome: THE METRONOME hits with a running punch / FIRST BEAT
- Build: CLOCKWORK, BLOW@Body, PRESS@Body, JAWS@Shadow. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (the running punch at 3700), `_still_733.jpg` (FIRST BEAT), `_still_3700.jpg`, `_trace.txt`.
- Look for:
  - The 25 % running punch (127-130 px of 507-518) and its 2 sparks.
  - The knuckle-on-wood click (sfx_metronome_swing_hit 0.36).
  - FIRST BEAT's white outline (outline=FFFFFF) is on the three numbers at 700. That is the first hit on each creature,
    and it comes from CLOCKWORK's opening cast, because the cast takes beat 1. No later number carries it. This is the
    "exactly the first hit on each creature" rule. A swing is not a first hit in this fight, because BLOW takes beat 2
    and the window holds only one swing (3700). Before the rig fix this outline was missing from the replay.

### 04. p1_04_tower: THE TOWER hits with an overhead slam
- Build: SLOW FALL, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (700), `_still_700.jpg`, `_still_767.jpg` (the dust settling), `_trace.txt`.
- Look for the 20 % step (79 of 395 px), and the dust plus the flat ring placed AT the creature, with the
  mallet-on-earth cue.
- **To judge:** in the strip, the hammer head ends at the Tower's own feet while the dust and ring are at the creature,
  about 250 px away. The step-in moves him 20 % of the gap and no further. This is the detachment P1.3 flagged; the
  film does not resolve it.

### 05. p1_05_thornwall: THE THORNWALL hits with a shield bash
- Build: NARROWS, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (700, which opens 200 ms before inside the step-in), `_still_500.jpg` (mid step-in),
  `_still_700.jpg`, `_trace.txt`.
- Look for:
  - The cleaned strip, with no baked white flash.
  - The broad, short 2-frame flash (6 swing-draw frames over 3 swings).
  - Whether the wide silhouette's 20 % step-in (65 of 326 px) reads as a slide. This is the design's named risk.
  - The shield thump with a rim rattle.

### 06. p1_06_magpie: THE MAGPIE hits with a dagger nick (BRAND agnostic)
- Build: PAYING WORK, BLOW@Body, PRESS@Body, BRAND@Shadow. BRAND replaces JAWS in this take; the swap is recorded.
  The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (700), `_still_700.jpg`, `_still_2400.jpg` (the curse on the creature), `_trace.txt`.
- Look for:
  - The fastest profile still reads (25 %, 88 of 351 px).
  - The green arc is gone; you should see a thin slash and 1 sliver.
  - The curse is the Seeker's: it is seated at 1783, applies at 2000 with the Seeker's apply and ash cues at their
    levels, and is drawn as the same territories. The Source shows only in the numbers.

### 07. p1_07_quiver: THE QUIVER hits with a bow shot (also a 0.25x render)
- Build: BACKDRAW, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_slow.mp4` (0.25x of the shot at 2000, playhead 1500-2300), `_strip.jpg` (2000; cell 1 is the
  release at 1800), `_still_1867.jpg` (the arrow in flight), `_still_2000.jpg` (it lands), `_trace.txt`.
- Look for:
  - Every arrow leaves the bow socket 200 ms before the beat (release=beat-200, clamp=0) and lands on the beat (4 of 4).
  - The loose (0.18) sits under the hit (0.36).
  - No static strip flies.
  - The pack's bite shares one swing ms (2 asks on that ms).

### 08. p1_08_chorus: THE CHORUS hits with a thrown bone charm
- Build: GRAVE SONG, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (2200), `_still_2100.jpg` (the charm mid-flight), `_still_2200.jpg`, `_trace.txt`.
- Look for:
  - ONE thrown charm per swing (flights=1).
  - The baked charms and the blue glow are gone.
  - The toss is 0.16, and the bone clatter (0.36) comes with 3 pale slivers.

### 09. p1_09_unbroken: THE UNBROKEN hits with a lobbed stone chip
- Build: HOLD FAST, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_strip.jpg` (2200), `_still_2100.jpg` (the chip at the top of its lob), `_still_2200.jpg`, `_trace.txt`.
- Look for:
  - The short, heavy 220 ms lob (release=beat-220).
  - The orange glow is gone.
  - The chip-on-rock crack (0.36) with 2 untinted chips.
  - The wall does not lunge (step 0).

### 10. p1_10_oathbound: THE OATHBOUND hits with a chain lash (also a 0.25x render)
- Build: OATHMARK, PRESS@Body, BLOW@Body, DRINK@Nature. The take seeks beat 1 with a 0.6 s lead.
- Files: `.mp4`, `_slow.mp4` (0.25x of the lash at 2200, playhead 1900-2500), `_strip.jpg` (2200),
  `_still_2200.jpg` (taut), `_still_2300.jpg` (the recoil), `_trace.txt`.
- Look for:
  - The chain stretches from the hand (648,638 every time), snaps taut ON the beat (taut=1 on 3 of 3), and recoils with
    the hook at its end.
  - The body never moves (step 0).
  - The spark plus the 0.30 flash at the hook.
- **To judge:** the trace's strand runs out at beat-204 and is home at beat+137. The 120 ms recoil the design names is
  the snap back; the last 17 ms is the slack settling.

## Reference regression (trace evidence, not for viewing)
- The five ref takes (ref_seeker, ref_fast, ref_brand, ref_brand_press, ref_jaws_kill) were re-filmed at the final code,
  including the two rig fixes. They are **IDENTICAL x 10** under `action_regression.py --refs` against both ae20a7fd
  baselines.
- The logs and `regression.txt` are in `production/qa/evidence/vfx-sweep/phase1/`.
- Release build `-warnaserror --no-incremental`: 0 warnings and 0 errors.
- Game.Tests 1214 / 1214 (+1: `test_a_rewound_replay_voices_an_already_voiced_swing_ms_again`). Core.Tests 1901 / 1901.

## Correction pass (2026-10-04): the `_fixed` takes

The director's review asked for three corrections and a short polish list. The eight affected takes were filmed again
at the corrected code, one at a time, with the same `p1_*` lines (tag `phase1_fixed`; frames deleted after each). Per take:
`<take>_fixed.mp4`, `_fixed_strip.jpg`, `_fixed_still_<ms>.jpg` (the same playheads as the first film) and
`_fixed_trace.txt`. The full logs are in `production/qa/evidence/vfx-sweep/phase1_fixed/`. A (the Seeker) and 08 (the
Chorus) were not changed and were not filmed again.

| take | what changed | where to look |
|---|---|---|
| 02 anvil | DEADWEIGHT's release on the JAWS ms now prints at Quiet grade, with no word and no outline (the trace says `amount=26 ... grade=Quiet`, no outline), still held until the snap; the real `-2 JAWS` keeps its Shadow outline. The step-in rises as u^3 | still 1367 |
| 03 metronome | FIRST BEAT's white outline now has a 1 px near-black halo outside it (13 passes) | still 733 |
| 04 tower | Frames 4-6 were repainted (`tower_slam_forward.py`): the hammer head comes over, then sits level at knee-to-waist height past his lead foot, pointing at the row. Contact is still frame 5, the step is still 20 % | stills 700 / 767 |
| 05 thornwall | `StepInPower` 3: the planted stance holds, and the translation lands in the last ~100 ms under the thrust | stills 500 vs 700 |
| 06 magpie | The steel dagger was painted back into her fist on frames 2-7 (`magpie_dagger.py`) | stills 700 / 2400 |
| 07 quiver | The baked nocked arrow was cleared from frames 5 and 6 (`quiver_one_arrow.py`), so only one arrow exists after the release | still 1867 |
| 09 unbroken | The chip is 1.4x bigger (HeadLength 0.11, about the charm's 40 px) and its edge brightness is 0.35. HOLD FAST's rings still bury it, as the review predicted (Phase 2) | still 2100 |
| 10 oathbound | `prop_seeker_chain_link` is stamped along the strand, alternating face-on and edge-on | stills 2200 / 2300 |

The five references are IDENTICAL x10 against both ae20a7fd baselines (`evidence/.../phase1_fixed/regression.txt`).
`swing-draw` alloc is 0 on every frame of the eight takes. Every swing contact still lands on its Strike ms.
