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
