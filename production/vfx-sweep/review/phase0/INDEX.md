# Phase 0 review film: index (2026-10-03)

This is the film for the presentation director's review of Phase 0. Phase 0 covers the generic-path fixes, the
regression baseline for the Seeker's five references, and the rig. Everything here was made from the working tree on
`fix/vfx-fade` (uncommitted Phase 0 code over ae20a7fd). The BEFORE takes are the P0.1 films of the clean ae20a7fd
(`production/qa/evidence/vfx-sweep/phase0-before/`). Their frames had been deleted, so their strips and stills are
read back out of those mp4s at the trace-clock offsets film_audio rendered them at. Those images are the arena crop
only.

How the takes were made:
- One take at a time with `bash tools/asset-pipeline/films_sweep.sh --keep --mp4 review0 <take>`. Every take is a
  named `r_*` line in that script, with RH_SHOT_SEED=7 and RH_PRESENT_TRACE=1.
- The audio comes from the trace through film_audio.py and sound_throttle.json.
- Each mp4 was re-encoded at 720p, CRF 28.
- Every frame was deleted right after its pieces were made. build/shots/sweep has 0 PNGs.
- The pieces were made by the new `tools/asset-pipeline/review_take.py`.

What each take has:
- `<take>_strip.jpg`: 8 cells at true-speed spacing around the decisive playhead. Each cell is labelled with its shot
  number, playhead ms and offset, and the decisive cell is outlined in gold. Cells are half-size arena crops.
- `<take>_still_<ms>.jpg`: a full 1920x1080 frame.
- `<take>_trace.txt`: the trace rows of the window. At the bottom are the per-layer `alloc=` figures for the whole take
  (lines / non-zero / max bytes).
- `<take>.log`: the full trace.
- `<take>.mp4`: the archive film with sound.

Folder size: about 17 MB.

## Numbers per take, counted from the traces over each film's window

| take | window (playhead ms) | sfx_hit asks | most plain thuds in one ms | quiet hits / Quiet numbers | flashes | skill-skip | echo | fx_heal | shield-gain cues | callouts |
|---|---|---|---|---|---|---|---|---|---|---|
| before_multi | 5263-10697 | 7 | 1 | 0 / 0 | 3 | 0 | 0 | 0 | 0 | HAMMER |
| after_multi | 5263-10230 | 7 | 1 | 0 / 0 | 3 | 0 | 0 | 0 | 0 | HARD HANDS |
| before_bleed | 5247-10213 | **17** | 1 | 0 / 0 | **13** | 0 | 0 | 0 | 0 | HAMMER |
| after_bleed | 7533-12567 | **6** | 1 | **9 / 9** | 5 (none on a bleed) | 0 | 0 | 0 | 0 | HARD HANDS, SPRAY |
| after_fast | 4833-7317 (60 fps) | 6 | 1 | 0 / 0 | 6 | 0 | 0 | 0 | 0 | SPRAY |
| after_pulse_on_mire | 8433-13400 | 8 | 1 | 0 / 0 | 7 | 0 | 0 | 0 | 0 | PULSE, HARD HANDS, +1 |
| after_backdraw | 9233-9800, then the next wave | 3 | 1 | 0 / 0 | 5 | **1** | 0 | 0 | 0 | SPRAY |
| after_oathmark | 433-4400 | 6 | 1 | 0 / 0 | 3 | 0 | 0 | 0 | 0 | (none) |
| after_drink_heal | 9150-14133 | 8 | 1 | 0 / 0 | 3 | 0 | 0 | **0** | 0 | DRINK, +57 |
| after_holdfast_shield | 33-5000 | 12 | 1 | 0 / 0 | 5 | 0 | 0 | 0 | **2 (mid-wave); 0 at ms 0** | +4 SHIELD |
| weaver (trace only) | 10250-19900 | 12 | 1 | 0 / 0 | 10 | 0 | **1** | 0 | 0 | HARD HANDS, SPRAY |

The five references were not filmed. `action_regression.py --refs` compares the P0.6 after-traces
(`production/qa/evidence/vfx-sweep/phase0/ref_*.log`) with both ae20a7fd baselines: **IDENTICAL x 10**. No .cs file
has changed since those traces were made. after_fast below is also compared under `--refs` with the ref_fast
baseline `_a`: **IDENTICAL** (210 shared moments, 0 differences, 36 fight events in order).

---

## 1. before_multi (item: one sfx_hit per batch ms, Actives call out their own name)
- Source: `../../../qa/evidence/vfx-sweep/phase0-before/before_multi.mp4` and its log. ae20a7fd, `fightmulti`, default
  Seeker: HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow.
- Files: `before_multi_strip.jpg`, `before_multi_still_8297.jpg`, `before_multi_trace.txt`.
- Look for: the callout reads **HAMMER** over HARD HANDS' crit at 8200.
- Two caveats:
  - This film saved no frame between 7830 and 8297 (a capture gap at ae20a7fd), so the strip is centred on 8297.
  - The pose seeks SPRAY's cast at 5200 with the default 0.02 s lead. The SPRAY batch lands at 5213, before the first
    saved frame (5263). The "N thuds on one cast" defect is **not** in this pose: the Seeker's SPRAY hits are
    PERFORMED, so there were 0 generic thuds on that batch before the fix as well as after. The thud rule shows on
    the bleed and fast takes instead.

## 2. after_multi (same pose at the Phase 0 code)
- Files: `after_multi_strip.jpg` (decisive 8213), `after_multi_still_8130.jpg`, `after_multi_still_8263.jpg`,
  `after_multi_trace.txt`, `after_multi.log`, `after_multi.mp4`.
- Look for:
  - The callout now reads **HARD HANDS** (8097). SPRAY's callout at 5197 reads SPRAY in the log, but it falls before
    the first saved frame.
  - Every other row matches take 1 in the trace: the same events, sounds, flashes and numbers. SPRAY, PRESS and JAWS
    are the same pictures, and their cues have the same volumes.
  - At most one plain sfx_hit per ms.

## 3. before_bleed (item: quiet derived hits, no strobe)
- Source: `../../../qa/evidence/vfx-sweep/phase0-before/before_bleed.mp4` and its log. ae20a7fd, `fightmulti`,
  `RH_SHOT_SWAP=snare_jaws:volley_weep@Shadow`.
- Files: `before_bleed_strip.jpg` (decisive 5513, cells about 133 ms apart), `before_bleed_still_5513.jpg`,
  `before_bleed_still_6013.jpg`, `before_bleed_trace.txt`.
- Look for the strobe. Each bleed tick (5500, 6000, ... amounts 40/35/31/27) is a Strike with an `sfx_hit` at 0.22,
  a full `flash` (0.45 / 80 ms) and a plain number.
- Caveat: the before pose is the fightmulti pose, not "from the first kill". That was the pose filmed at ae20a7fd
  in P0.1, and Phase 0 does not rebuild ae20a7fd.

## 4. after_bleed
- Pose: the same build, given whole by `RH_SHOT_BUILD=sig_seeker_hard_hands,volley_spray@Mind,hammer_press@Body,volley_weep@Shadow`,
  seek `hit:Bleed`, lead 1.0. The build is the same but given as a build rather than a swap, so the fight's timeline
  differs from take 3: the first bleed is at 8500.
- Files: `after_bleed_strip.jpg` (decisive 8517, about 133-167 ms apart, covering the 8500 and 9000 ticks),
  `after_bleed_still_8533.jpg`, `after_bleed_still_9033.jpg`, `after_bleed_trace.txt`, `.log`, `.mp4`.
- Look for:
  - Each tick is a `quiet-hit` with a `grade=Quiet` number (half-size, 70 % alpha), no flash and no sfx_hit.
    The trace has 9 quiet hits, 0 flashes and 0 thuds on them.
  - The front creature no longer blinks every 500 ms.
  - The only sfx_hit at 9000 is the enemy bite's pitched thud (-0.25), which is not a Strike.

## 5. after_fast (the eye check of the five-reference regression under load)
- Pose: the default Seeker, `RH_SHOT_TAKE` = films_brand's TEMPO list, T 3.4, 150 x 1 (true 60 fps, 2.5 s).
- Files: `after_fast_strip.jpg` (decisive 5817 = SPRAY's three-target contact, cells about 83 ms apart),
  `after_fast_still_5600.jpg` (SPRAY callout), `after_fast_still_5833.jpg` (contact), `after_fast_trace.txt`,
  `.log`, `.mp4`.
- Look for:
  - JAWS answers the bite at 5000 (its cue at 5333).
  - SPRAY's callout reads **SPRAY** (5567).
  - The PRESS tick lands at 6000. SPRAY's spray ticks are unthrottled, as approved.
  - One plain thud per ms.
  - The `--refs` comparison with ref_fast's baseline is IDENTICAL.
- Caveat: HARD HANDS' callout in this pose is at 4183, before the first saved frame. It reads HARD HANDS in the log.

## 6. after_pulse_on_mire (item: the last-owner rule; MIRE and PRESS in a real two-field build)
- Pose: BUILD `sig_seeker_hard_hands,field_pulse@Spirit,field_mire@Nature,hammer_press@Body`, seek
  `skill:field_pulse+ontick`, lead 0.6.
- How the pose was found:
  - Seeds 7, 8 and 9 hold no such ms. RH_SHOT_SEED does not move the fixture's wave (214 events every time).
  - Enemy baselines 1400-4500 hold none either.
  - The seek exists once TEMPO is taken up to BLITZ, so the take carries
    `RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz` (seed 7). That is committed in the take line.
- Files:
  - `after_pulse_on_mire_strip.jpg`: decisive 9000, where PULSE's cast lands on MIRE's tick.
  - `after_pulse_on_mire_still_9100.jpg`: PULSE's numbers.
  - `after_pulse_on_mire_still_9533.jpg`: MIRE's tick total.
  - `after_pulse_on_mire_trace.txt`, `.log`, `.mp4`.
  - The 0.25x slow render of the tick: `after_pulse_on_mire_slow025.mp4` (40 frames at 60 fps, lead 0.25). Its
    every-frame strip is `after_pulse_on_mire_slow_strip.jpg` and its rows are in `..._slow_trace.txt`.
- Look for:
  - PULSE's three struck creatures flash.
  - MIRE's tick total prints as its own number at 9500 (`slot=1 amount=50`).
  - MIRE's slow and PRESS's field run side by side.
- **FINDING: the rule does not hold for the NUMBERS.**
  - PULSE prints `-158 x2`, `-155 x2` and `-138 x2` (`number ... hits=2`). Those are PULSE's 134 / 143 / 124 plus
    MIRE's same-ms ticks of 24 / 12 / 14.
  - MIRE's 24 + 12 + 14 = 50 is then printed again in its own total at 9500. MIRE's damage is shown twice, and once
    under PULSE's number.
  - The flash and the sound follow the rule: one sfx_hit at 0.22, and exactly one flash per struck creature (3), so
    MIRE's same-ms ticks add no flash of their own.
  - The number accumulator merges same-ms Strikes on a slot regardless of owner.
  - This is a Phase 0 defect to fix before Phase 2's field work: the number batching must respect the owner, or skip
    aura-owned Strikes that go into the tick total.

## 7. after_backdraw (item: the phantom skip)
- Pose: quiver BUILD `sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek
  `skill:sig_quiver_backdraw+unstruck`, lead 0.6, `RH_SHOT_ENEMY=800,9`.
  - Health 300 ends the wave before BACKDRAW is armed (no such event).
  - Health 1100 holds no unstruck BACKDRAW.
  - Health 800 does: SPRAY takes the wave's last creature at 9800, and BACKDRAW fires on the same ms with no Strike.
  - 120 x 2.
- Files: `after_backdraw_strip.jpg` (decisive 9800; the playhead then rests during the wave break, so later cells
  read 9800), `after_backdraw_still_9700.jpg`, `after_backdraw_still_9800.jpg`, `after_backdraw_trace.txt`, `.log`,
  `.mp4`.
- Look for:
  - The trace has `skill-skip slot=0 skill=sig_quiver_backdraw at=9800`.
  - BACKDRAW has no callout, no ring, no cast breath and no clip. The only callout is SPRAY's.
- Caveats:
  - BLOW is not cast inside this 4 s window. P0.6's `../../../qa/evidence/vfx-sweep/phase0/after_backdraw.log`
    shows BLOW calling out BLOW.
  - The pink ring that pulses around the hunter (faint from the first cell, flaring in the shot 29 cell) is PRESS's
    generic field ring on a non-Seeker champion. Take 8 has the same ring, and no trace row belongs to BACKDRAW.
  - SPRAY's generic quiver projectile is launched on the same ms its hit resolves, so the arrow flies after the kill.
    That is the generic projectile path (Phase 1/2), not a Phase 0 item.

## 8. after_oathmark (item: no pitched thud for a no-damage reaction, no trap clip, no callout)
- Pose: oathbound BUILD `sig_oathbound_oathmark,hammer_press@Body,volley_spray@Mind,hammer_blow@Body`, seek
  `skill:sig_oathbound_oathmark`, lead 0.6, 120 x 2.
- Files: `after_oathmark_strip.jpg` (decisive 1000, cells about 100 ms apart), `after_oathmark_still_1000.jpg`,
  `after_oathmark_still_1200.jpg`, `after_oathmark_trace.txt`, `.log`, `.mp4`.
- Look for:
  - At 1000 the bite's EnemyStrike, `Marked` and OATHMARK's Skill arrive together.
  - The sounds are the bite's own pitched thud (-0.25, which is the enemy's) and `sfx_cast`. There is **no**
    +0.25 reaction thud.
  - There is no `trap` clip-start: the figure keeps its idle and swing.
  - There is no callout.
  - The mark's chain-ring (`fx_oathbound_mark_strip8_512`, cast.mark) appears on the front creature.

## 9. after_drink_heal (item: the summed heal receive)
- Pose: seeker BUILD `sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow`,
  `RH_SHOT_ENEMY=1500,25` (a hard bite, so he is hurt), seek `skill:drain_drink`, lead 0.6.
- Files: `after_drink_heal_strip.jpg` (decisive 9700, cells about 133 ms apart), `after_drink_heal_still_9800.jpg`,
  `after_drink_heal_still_10133.jpg` (the `+57`), `after_drink_heal_trace.txt`, `.log`, `.mp4`.
- Look for:
  - Two Heals at 9700 (46 + 11) become **one `+57`** at 10100.
  - There are 0 `fx_heal` spawns and no heal sound.
  - DRINK's callout reads **DRINK**.
- Caveat: the big **green rectangle column** around the hunter (9833-10100) is NOT the heal column. It is DRINK's
  legacy cast VFX, `fx_seeker_transformation_strip8_512` (cast.transformation), which has hard edges. It is
  pre-existing and is replaced by DRINK's REACH mode in Phase 3. It also covers the soft chest glow, so do not judge
  the glow from this film. DRINK plays the `transformation` clip, not a lunge.

## 10. after_holdfast_shield (item: the ShieldGained claim, the wave-open plate)
- Pose: unbroken BUILD `sig_unbroken_hold_fast,snare_jaws@Spirit,hammer_blow@Body,snare_repay@Machine`.
  - CARRIED is a reinforcement of BANKED, so the variation is `snare_repay:BANKED+CARRIED`.
    `snare_repay:CARRIED` alone is refused by the progression.
  - `RH_SHOT_ARCHETYPE=Swarm` for "Fast biters" (four creatures that bite every second). It is the wave's own
    archetype anyway.
  - Seek `beat:1`, lead 1.0.
- Files: `after_holdfast_shield_strip.jpg` (from the wave's first frame, cells about 300 ms apart to reach the 2000
  gain), `after_holdfast_shield_still_33.jpg` (the plate up at open), `after_holdfast_shield_still_2033.jpg` (the
  mid-wave gain), `after_holdfast_shield_trace.txt`, `.log`, `.mp4`.
- Look for:
  - `ShieldGained amount=9 at=0` has no `sfx_shield_gain`, no `shield.gain` spawn and no callout. The plate is simply
    up and silent.
  - HOLD FAST's mid-wave gains at 2000 and 4000 keep the unchanged generic `+4 SHIELD`, cue 0.40 and gain ring.
  - Absorbs keep `sfx_shield_hit` and `shield.absorb`.
- Caveats:
  - The white row spikes at about 1233 are JAWS@Spirit's generic `fx_unbroken_trap_strip8_512` (cast.trap) on this
    champion. Removing them is a later-phase item, not Phase 0.
  - Its +0.25 pitched thud is legal here because that reaction deals damage.

## Not filmed (trace only)
- **weaver** (`weaver_trace.log`, 20 x 30, default build plus `RH_SHOT_KEYSTONES=weaver`, T 9). At 11200 the trace has
  `echo slot=1 skill=volley_spray`.
  - There is one callout (HARD HANDS, 11083) and no second one for the echo.
  - There is no `sfx_cast` for the echo, so there is one cast breath.
  - The echo's effect spawns as `cast.projectile`. Its x0.6 size and brightness (`GenericHits.EchoScale`) are applied
    in PlaySkillVfx and are not traced.
- **the five ref_* takes**: proved by `action_regression.py --refs` (above), not by eye.

---

## Correction pass (2026-10-03): the `_fixed` takes

These takes were re-filmed after the director's review, from the same take lines (plus `r_weaver` and
`r_holdfast_trace`). Every frame was deleted after use. notes.md ("Phase 0 / correction pass") has the full record.

| take | files | what changed |
|---|---|---|
| after_pulse_on_mire_fixed | strip, stills 9033 / 9100 / 9400, trace, log, mp4 | PULSE prints `-134 / -143 / -124` with hits=1. MIRE's tick total `-50` prints AT 9000, not at 9500. No `+1` for MIRE's 1-point heal (it is under 1 % of max health). |
| after_bleed_fixed | strip, stills 8533 / 9033, trace, log, mp4 | The quiet `-40` / `-35` is born inside the struck body (0.4 h down its drawn bounds) with the one-pixel drop shadow. Plain numbers sit over their creature's life pip. |
| after_fast_fixed | strip, stills 5600 / 5833, trace, log, mp4 | Each creature's numbers stack over its own pip, and the row is about 100-150 px lower than before. There is no staircase across the row. |
| after_drink_heal_fixed | strip, stills 9800 / 10133, trace, log, mp4 | `+57` is a number at his chest (DamagePx, Verdant), not a callout under DRINK. The legacy green column still covers it. |
| after_drink_glow_novfxcol_fixed | strip, stills 9717 / 9833 / 10133, trace | The same pose with only `fx_seeker_transformation_strip8_512` replaced by a blank strip (`RH_SHOT_STRIP_FILES`, QA only), so the chest glow and `+57` can be seen. |
| after_oathmark_fixed | strip, stills 1000 / 1200, trace, log, mp4 | No `sfx_cast` on OATHMARK's open. The only sounds are the bite's own pitched thud. |
| weaver_fixed | strip, stills 11217 / 11300, trace, log, mp4 | HARD HANDS keeps its 0.55 / 120 flash on slot 0 only. SPRAY's echo strikes on slots 1-3 flash at 0.27 (UsualFlash x 0.6) and ask one `sfx_hit` at 0.11 (0.22 x 0.5). |
| after_holdfast_fixed | trace excerpt 900-1100, log (trace only) | At 1000 there is one pitched +0.25 thud. There is no `sfx_cast` and no generic 0.22 ask. |

The five references were re-traced at this code and held with `--refs` against both baselines: **IDENTICAL x 10**
(`production/qa/evidence/vfx-sweep/phase0-fix/regression.txt`).
