# JAWS smoke teeth: the review package (ADR-011, 2026-09-28)

Review page (Turkish): https://claude.ai/artifact/PGbMWLcVTPtJzxaeDj2Jr1

**The owner's word** on the shadow-mist polish, in Turkish: "Efekt çok iyi, sadece biraz daha dumanmış gibi olsun
istiyorum, daha sis gibi, biraz daha opacity'si düşük." In English: the effect is very good; I just want it a little more
like smoke, more like mist, with a little lower opacity.

**What changed** (films `build/shots/jaws/smoke3`; before: `build/shots/jaws/mist4`):
- **The teeth are smoke held in a tooth's shape** (`tools/asset-pipeline/v2/seeker_bite.py`, `smoky_row`). Density
  billows inside each tooth in round, curling swirls (tall vertical licks read as purple flame in review). The outline wavers and is soft, but it
  hardens toward the point, so the points stay points and the bite still reads. The roots dissolve into the smoke the
  row condenses from, soft wisps drift off the gum (up off the crown, down off the lower row), and there is no glass rim.
  The six condensation states derive from this smoky row. The solid rows are kept in
  `keypose_sources/history/fxp_seeker_bite_{upper,lower}_solid.png`.
- **On the snap frame the smoke condenses hard.** Drawn as smoke, the snap was a star over a lilac haze and the jaw was
  never seen to shut (a reviewer rejected it). The strip now has a seventh cell, `ReactionRecipe.SnapCell`: the solid
  teeth, drawn on the first frame at the snap only (`ReactionPerformance.CellAt`), white-hot unless the champion is
  performing; the next frame the teeth break back as smoke. The mist compresses into a real bite, then disperses.
- **Lower opacity:** formed 0.75 → 0.62, at the snap 1.0 → 0.88 (`ReactionRecipe.FormedOpacity`, `SnapOpacity`); the
  smoke's own density thins them further. A first try at 0.55 / 0.85 made the pale early phase too faint.
- **Unchanged:** the geometry (the tips file `seeker_bite_spans.json` is identical), timing, colours, the burst, the
  mist behind the creatures, quiet mode, the single bite cue, the number after the snap.

**Checked.** Two judges (the owner's ask; readability): both said the teeth read as smoke, a little less opaque, and
the bite still reads; one REJECTED the snap frame (a star over a haze, the jaw never seen to shut), fixed by the SNAP
cell. Two adversarial re-checks (the snap; the material): both ACCEPT WITH NOTES, no defect. The snap frame is
pixel-identical to the approved snap; the density jump lands inside the white flash, so it reads as the smoke
compressing, not a new object; nothing reads as flame or fur after the billows were rounded; JAWS stays quieter under
SPRAY and HARD HANDS. Their notes: the upper crown reads as one smoky band with a saw-tooth edge (the canines and the
points carry "teeth"); if the owner wants more "teeth", the lever is softer outlines or more separation, not opacity;
the shut jaw is now on screen one frame (~17 ms) instead of two.

**Proof.** alloc 0 on all 814 traced frames; 808 game tests and 1888 Core tests pass; the asset gate reports no new
orphans.

| file | what |
|---|---|
| 01_before_solid_teeth_true_speed_sound.mp4 | before: the polish with solid teeth |
| 02_smoke_teeth_true_speed_sound.mp4 | now: smoke teeth, true speed, sound |
| 03_smoke_teeth_MUTED.mp4 | now, muted |
| 04_close_crop_true_speed_MUTED.mp4 | the bitten whelp, close |
| 05_phase_sheet_FORM_CONDENSE_CHARGE_WINDUP_SNAP_PRESSURE_RELEASE_DISSOLVE.png | every phase, now over before |
| 06_repeated_fast_tempo_sound.mp4 | repeated triggers at fast TEMPO |
| 07_during_spray_sound.mp4 | during SPRAY |
| 08_during_hard_hands_sound.mp4 | during HARD HANDS |
| 09_trace.md | the bite and the answer on the playhead |

Regenerate: `bash build/shots/jaws/films_jaws.sh smoke3 normal fast_rearm during_spray during_hh`, then
`PYTHONUTF8=1 python tools/asset-pipeline/jaws_smoke_teeth_evidence.py production/qa/evidence/jaws-smoke-teeth`.
