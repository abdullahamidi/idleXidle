# JAWS art history (never played)

Rejected JAWS presentations, kept on disk as history at the owner's request ("do not delete historical assets; just
remove them from production playback"). They live here, outside `assets/`, so the game does not load them at boot and
the asset-consumer gate (`tools/check_asset_consumers.py`) does not count them as orphans.

- `prop_seeker_jaws_far_edge.png`, `prop_seeker_jaws_near_edge.png`: the spring-loaded bear trap (rejected 2026-09-25).
- `fxp_seeker_jaws_open.png`, `_half.png`, `_shut.png`: the Shadow piranha (rejected 2026-09-27).
- `fxp_seeker_fang.png`: the hard Shadow Fang sprite (replaced 2026-09-28 by the misty fang strip `fxp_seeker_mistfang`).
- `fxp_seeker_mist_a.png`, `fxp_seeker_mist_b.png`: the floor mist under the creature (removed 2026-09-28: the teeth themselves are the mist now).

Their generators still write into `assets/art/VFX/parts/` if re-run (`seeker_piranha.py`, `seeker_fangs.py`,
`seeker_mist.py`, `seeker_jaws.py`); move the output back here if you do.
- `fxp_seeker_mistfang.png`: the four misty teeth that closed like triangles (replaced 2026-09-28 by the misty maw `fxp_seeker_maw`, `seeker_maw.py`).
- `fxp_seeker_maw.png`: the side-view Shadow maw made of mist (two jaw pieces, 16 states); the owner rejected it the same day ("a bad jaw biting from the side"; replaced 2026-09-28 by the FRONTAL bite, `fxp_seeker_bite_*`, `seeker_bite.py`). Its generator `seeker_maw.py` now writes here.
- `fxp_seeker_bite_slash.png`: the frontal bite's diagonal magenta slash, removed in the shadow-mist polish (2026-09-28): a directional stroke in a radial burst read as a blade, and JAWS is a bite. `seeker_bite.py` writes it here only.
- `fxp_seeker_bite_upper_solid.png`, `fxp_seeker_bite_lower_solid.png`: the frontal bite's rows as SOLID leaf-shaped teeth (flat glow, bright rim, 6 mist-to-crisp states), replaced 2026-09-28 after the owner's word on the shadow-mist polish ("more like smoke, more like mist, a little lower opacity") by teeth made of smoke (`seeker_bite.py` `smoky_row`).
- `fxp_seeker_press_{field,wave,clamp}_smooth.png`, `seeker_press_sheet_smooth.png`: PRESS's first-slice parts (2026-09-28), smooth high-resolution shapes with Gaussian softness; the owner approved the direction and asked for a pixel-hard material ("too smooth, too soft, too vector-like"): replaced by low-resolution, NEAREST-upscaled parts with value bands, a broken edge and pixel-chunk dissolve states.
- `fxp_seeker_press_crease.png`: the pixel-hard pass's compression accent, a chunky shaftless chevron ">" drawn at both sides of the crushed creature ("> TARGET <"); removed the same day: two of three judges read the pair as arrowheads / a lock-on reticle (the brief bans arrows), in lunges the far one landed on the next creature, and pinching from the sides contradicted the top-down crush.
- `fxp_seeker_press_wave_pixelhard.png`, `seeker_press_sheet_pixelhard.png`: PRESS's approved pixel-hard front (2026-09-28, four cells) before the field-vs-projectile polish: its edge carried a pale PEAK accent at the middle (a glowing centre gives the wall a projectile's head); replaced by the three-cell front (body, edge, dissolve: no accent, and no ECHO cell -- a one-frame trailing copy is a projectile's tail) and the new `fxp_seeker_press_fold` part (the front folding into the crush); a launch CONTOUR cell was tried and removed before it was ever committed.
