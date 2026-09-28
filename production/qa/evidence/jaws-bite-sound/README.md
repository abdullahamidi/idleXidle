# JAWS bite sound: the review package (ADR-011, 2026-09-28)

Review page (Turkish): https://claude.ai/artifact/UyNseCpfkpYrHioX4P5ucX

**The owner** approved the smoke-teeth picture (https://claude.ai/artifact/PGbMWLcVTPtJzxaeDj2Jr1), in Turkish: "Bu hali çok güzel, onaylıyorum. Son
olarak ses efektini de daha çok bite/ısırılma sesi yaparsan bu skilli tamamen onaylarız." In English: this version is
very nice, I approve it. Finally, if you make the sound effect more of a bite / being-bitten sound, we approve this skill
completely.

**What changed:** only the ONE cue on the snap. `ReactionRecipe.SnapCues[0]` is now `sfx_seeker_jaws_bite`
(`tools/asset-pipeline/make_action_sfx.py`, `_bite` / `jaws_bite`), built in a mouth's order, its first transient on
the snap (+316 ms, traced in all four takes):
1. the teeth MEET: two enamel clacks (click + a damped resonance dying inside ~4 ms + a small body), the lower row 4 ms
   behind the upper;
2. they SINK IN: granular crunch bursts over ~40 ms, each quieter than the last, and a short WET squish (a band
   falling 1500 → 420 Hz);
3. the JAW'S WEIGHT: a short low thud (165 → 88 Hz);
4. a whisper of the Shadow (a dark tone squeezed 700 → 120 Hz, far under everything).
One bite: every layer starts inside the first ~50 ms; the envelope has one attack and decays monotonically.

The old thorn impact `sfx_seeker_jaws_fangs` stays on disk as history; nothing plays it. The picture, the timing, the
volume (0.42) and the duck under an action (0.45, traced) are unchanged.

**Candidates** (the owner picks; A is in the game):

| cue | ms | peak dB | rms dB | centroid Hz | decay20 ms |
|---|---|---|---|---|---|
| old: `sfx_seeker_jaws_fangs` | 130 | −9.9 | −30.3 | 2039 | 29 |
| A CHOMP: `sfx_seeker_jaws_bite` | 180 | −9.4 | −23.3 | 1476 | 74 |
| B CRUNCH: `candidates/sfx_seeker_jaws_bite_crunch` | 180 | −9.4 | −26.1 | 2049 | 58 |
| C HEAVY: `candidates/sfx_seeker_jaws_bite_heavy` | 200 | −9.4 | −22.2 | 798 | 104 |

For scale: the approved SPRAY hit is −26.6 dB RMS and the HARD HANDS blow −21.1 dB; every candidate is lighter than
the blow. B and C are rendered into the same film from the trace (`film_audio.py --cue`), so only the sound differs.

**Proof.** 808 game tests and 1888 Core tests pass; the asset gate reports no new orphans; one bite cue per JAWS in
every take.

| file | what |
|---|---|
| 01_before_thorn_impact_sound.mp4 | the approved picture with the old cue |
| 02_A_chomp_sound.mp4 | A, in the game |
| 03_B_crunch_sound.mp4 | B |
| 04_C_heavy_sound.mp4 | C |
| 05_A_during_spray_sound.mp4 | A during SPRAY (ducked) |
| 06_A_during_hard_hands_sound.mp4 | A during HARD HANDS (ducked) |
| 07_A_repeated_fast_tempo_sound.mp4 | A repeated at fast TEMPO |
| sfx_seeker_jaws_bite.wav, sfx_seeker_jaws_fangs.wav, candidates/*.wav | the cues on their own |

Regenerate: `PYTHONUTF8=1 python tools/asset-pipeline/make_action_sfx.py`, `bash build/shots/jaws/films_jaws.sh bitesfx
normal fast_rearm during_spray during_hh`, then `PYTHONUTF8=1 python tools/asset-pipeline/jaws_bite_sound_evidence.py
production/qa/evidence/jaws-bite-sound`.
