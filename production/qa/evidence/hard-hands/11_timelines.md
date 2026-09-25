# HARD HANDS timelines (RH_PRESENT_TRACE, tools/asset-pipeline/action_timeline.py)

t = 0 is the fight's beat. Every row is the frame it was PRESENTED on (60 fps frames: the beat itself is presented on the first frame after it, +17 ms).

# NEW, normal TEMPO (the reference)

## cast 1: beat 8200 ms, targets 0

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 7833 | -367 |  |
| anticipation (f1) | 7900 | -300 |  |
| loaded hold (f2) | 8000 | -200 |  |
| body drawn back (deepest) | 8067 | -133 | x -16 px |
| COMMIT: the leap (f3) | 8083 | -117 |  |
| commit sound | 8083 | -117 | sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12 |
| lunge launched | 8083 | -117 | x=750 y=508 reach=399 |
| first speed line | 8117 | -83 | 3 sprites |
| body reaches the target | 8200 | +0 | x +399 px |
| contact pose (f4) | 8217 | +17 |  |
| CONTACT (the fist lands) | 8217 | +17 | x=1192 y=739 |
| health (hits crossed) | 8217 | +17 | 1 x, last at +17 |
| flash | 8217 | +17 | 1 x, last at +17 |
| number | 8217 | +17 | 1 x, last at +17 |
| contact audio | 8217 | +17 | sfx_seeker_hard_hands_hit vol=0.55 pitch=0.00 pan=0.06 |
| follow-through (f5) | 8283 | +83 |  |
| body furthest (overshoot) | 8333 | +133 | x +411 px |
| recovery (f6) | 8383 | +183 |  |
| return hop peak | 8433 | +233 | y -25 px, x +228 px |
| recovery end / settle (f7) | 8500 | +300 |  |
| body home (landed) | 8517 | +317 |  |
| clip end | 8600 | +400 |  |
| idle restart | 8600 | +400 |  |
| generic impact puff | nan | +nan | none (replaced by the fist's own contact) |

other sounds inside the phrase (release .. beat+200):
      8217    +17  sfx_enemy_down vol=0.36 pitch=0.00 pan=0.00 duck=0.45

# NEW, fast TEMPO: swing -> HARD HANDS -> swing

## ## cast 0: beat 4300 ms, targets 0,1

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 3689 | -611 | its beat 3600 |
| previous recovery start | 3798 | -502 | protected frames end here (381 ms into its clip) |
| previous exit pose reached | 3920 | -380 | recovery 54 ms nominal (+300 ms settle) in 122 ms: x1.00 (Natural) |
| clip start (ready, f0) | 3933 | -367 |  |
| anticipation (f1) | 4000 | -300 |  |
| loaded hold (f2) | 4100 | -200 |  |
| body drawn back (deepest) | 4167 | -133 | x -16 px |
| COMMIT: the leap (f3) | 4183 | -117 |  |
| commit sound | 4183 | -117 | sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12 |
| lunge launched | 4183 | -117 | x=750 y=508 reach=399 |
| first speed line | 4217 | -83 | 3 sprites |
| contact pose (f4) | 4300 | +0 |  |
| body reaches the target | 4300 | +0 | x +399 px |
| CONTACT (the fist lands) | 4300 | +0 | x=1192 y=739 |
| health (hits crossed) | 4300 | +0 | 2 x, last at +0 |
| flash | 4300 | +0 | 2 x, last at +0 |
| number | 4300 | +0 | 2 x, last at +0 |
| contact audio | 4300 | +0 | sfx_seeker_hard_hands_hit vol=0.55 pitch=0.00 pan=0.06 |
| follow-through (f5) | 4383 | +83 |  |
| body furthest (overshoot) | 4433 | +133 | x +411 px |
| recovery (f6) | 4483 | +183 |  |
| return hop peak | 4533 | +233 | y -25 px, x +228 px |
| recovery end / settle (f7) | 4600 | +300 |  |
| body home (landed) | 4617 | +317 |  |
| clip end | 4700 | +400 |  |
| idle restart | 4700 | +400 |  |
| generic impact puff | nan | +nan | none (replaced by the fist's own contact) |


# NEW, fast TEMPO: HARD HANDS -> SPRAY

## ## cast 0: beat 4300 ms, targets 0,1

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 3689 | -611 | its beat 3600 |
| previous recovery start | 3798 | -502 | protected frames end here (381 ms into its clip) |
| previous exit pose reached | 3920 | -380 | recovery 54 ms nominal (+300 ms settle) in 122 ms: x1.00 (Natural) |
| clip start (ready, f0) | 3933 | -367 |  |
| anticipation (f1) | 4000 | -300 |  |
| loaded hold (f2) | 4100 | -200 |  |
| body drawn back (deepest) | 4167 | -133 | x -16 px |
| COMMIT: the leap (f3) | 4183 | -117 |  |
| commit sound | 4183 | -117 | sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12 |
| lunge launched | 4183 | -117 | x=750 y=508 reach=399 |
| first speed line | 4217 | -83 | 3 sprites |
| contact pose (f4) | 4300 | +0 |  |
| body reaches the target | 4300 | +0 | x +399 px |
| CONTACT (the fist lands) | 4300 | +0 | x=1192 y=739 |
| health (hits crossed) | 4300 | +0 | 2 x, last at +0 |
| flash | 4300 | +0 | 2 x, last at +0 |
| number | 4300 | +0 | 2 x, last at +0 |
| contact audio | 4300 | +0 | sfx_seeker_hard_hands_hit vol=0.55 pitch=0.00 pan=0.06 |
| follow-through (f5) | 4383 | +83 |  |
| body furthest (overshoot) | 4433 | +133 | x +411 px |
| recovery (f6) | 4483 | +183 |  |
| return hop peak | 4533 | +233 | y -25 px, x +228 px |
| recovery end / settle (f7) | 4600 | +300 |  |
| body home (landed) | 4617 | +317 |  |
| clip end | 4700 | +400 |  |
| idle restart | 4700 | +400 |  |
| generic impact puff | nan | +nan | none (replaced by the fist's own contact) |

## cast 2: beat 8700 ms, targets 1,2

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 8006 | -694 | its beat 8000 |
| previous recovery start | 8114 | -586 | protected frames end here (381 ms into its clip) |
| previous exit pose reached | 8320 | -380 | recovery 54 ms nominal (+300 ms settle) in 206 ms: x1.00 (Natural) |
| clip start (ready, f0) | 8333 | -367 |  |
| anticipation (f1) | 8400 | -300 |  |
| loaded hold (f2) | 8500 | -200 |  |
| body drawn back (deepest) | 8567 | -133 | x -16 px |
| COMMIT: the leap (f3) | 8583 | -117 |  |
| commit sound | 8583 | -117 | sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12 |
| lunge launched | 8583 | -117 | x=750 y=508 reach=535 |
| first speed line | 8617 | -83 | 3 sprites |
| body reaches the target | 8700 | +0 | x +535 px |
| contact pose (f4) | 8717 | +17 |  |
| CONTACT (the fist lands) | 8717 | +17 | x=1328 y=739 |
| health (hits crossed) | 8717 | +17 | 2 x, last at +17 |
| flash | 8717 | +17 | 2 x, last at +17 |
| number | 8717 | +17 | 2 x, last at +17 |
| contact audio | 8717 | +17 | sfx_seeker_hard_hands_hit vol=0.55 pitch=0.00 pan=0.11 |
| follow-through (f5) | 8783 | +83 |  |
| body furthest (overshoot) | 8833 | +133 | x +547 px |
| recovery (f6) | 8883 | +183 |  |
| return hop peak | 8917 | +217 | y -24 px, x +335 px |
| recovery end / settle (f7) | 8917 | +217 |  |
| clip end | 8950 | +250 |  |
| generic impact puff | nan | +nan | none (replaced by the fist's own contact) |

other sounds inside the phrase (release .. beat+200):
      8717    +17  sfx_enemy_down vol=0.36 pitch=0.00 pan=0.00 duck=0.45

## cast 3: beat 9500 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (strike) contact | 8700 | -800 | its beat 8700 |
| previous recovery start | 8870 | -630 | protected frames end here (537 ms into its clip) |
| previous exit pose reached | 8943 | -557 | recovery 220 ms nominal (+0 ms settle) in 73 ms: x3.00 (Floor) |
| clip start (ready, f0) | 8950 | -550 |  |
| anticipation (f1) | 9033 | -467 |  |
| extreme hold (f2) | 9117 | -383 |  |
| release pose (f3) | 9267 | -233 |  |
| release sound | 9267 | -233 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 9267 | -233 | x=748 y=564 knives=2 |
| first visible blade | 9267 | -233 | 6 sprites |
| CONTACT (blades land) | 9517 | +17 | x=1496 y=741 knives=2 |
| health (hits crossed) | 9517 | +17 | 2 x, last at +17 |
| flash | 9517 | +17 | 2 x, last at +17 |
| number | 9517 | +17 | 2 x, last at +17 |
| contact audio | 9517 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.18 |
| follow-through end (f4 -> f5) | 9533 | +33 |  |
| recovery (f6) | 9633 | +133 |  |
| recovery end / settle (f7) | 9717 | +217 |  |
| clip end | 9833 | +333 |  |
| idle restart | 9833 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

# NEW, the fastest build: HARD HANDS -> SPRAY 500 ms later (the yield)

## cast 1: beat 5200 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 4812 | -388 | its beat 4800 |
| previous recovery start | 4877 | -323 | protected frames end here (227 ms into its clip) |
| previous exit pose reached | 4909 | -291 | recovery 32 ms nominal (+212 ms settle) in 32 ms: x1.00 (Natural) |
| clip start (ready, f0) | 4917 | -283 |  |
| anticipation (f1) | 4950 | -250 |  |
| loaded hold (f2) | 5000 | -200 |  |
| body drawn back (deepest) | 5067 | -133 | x -16 px |
| COMMIT: the leap (f3) | 5083 | -117 |  |
| commit sound | 5083 | -117 | sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12 |
| lunge launched | 5083 | -117 | x=750 y=508 reach=648 |
| first speed line | 5100 | -100 | 3 sprites |
| body reaches the target | 5200 | +0 | x +648 px |
| contact pose (f4) | 5217 | +17 |  |
| body furthest (overshoot) | 5217 | +17 | x +658 px |
| CONTACT (the fist lands) | 5217 | +17 | x=1464 y=739 |
| health (hits crossed) | 5217 | +17 | 2 x, last at +17 |
| flash | 5217 | +17 | 2 x, last at +17 |
| number | 5217 | +17 | 2 x, last at +17 |
| contact audio | 5217 | +17 | sfx_seeker_hard_hands_hit vol=0.55 pitch=0.00 pan=0.17 |
| clip end | 5233 | +33 |  |
| generic impact puff | nan | +nan | none (replaced by the fist's own contact) |

## cast 2: beat 5700 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (strike) contact | 5200 | -500 | its beat 5200 |
| previous exit pose reached | 5230 | -470 | recovery 220 ms nominal (+0 ms settle) in 0 ms: x1.00 (Yielded) |
| clip start (ready, f0) | 5233 | -467 |  |
| anticipation (f1) | 5267 | -433 |  |
| extreme hold (f2) | 5317 | -383 |  |
| previous recovery start | 5370 | -330 | protected frames end here (453 ms into its clip) |
| release pose (f3) | 5467 | -233 |  |
| release sound | 5467 | -233 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 5467 | -233 | x=748 y=564 knives=2 |
| first visible blade | 5467 | -233 | 6 sprites |
| CONTACT (blades land) | 5717 | +17 | x=1496 y=743 knives=2 |
| health (hits crossed) | 5717 | +17 | 2 x, last at +17 |
| flash | 5717 | +17 | 2 x, last at +17 |
| number | 5717 | +17 | 2 x, last at +17 |
| contact audio | 5717 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.18 |
| follow-through end (f4 -> f5) | 5733 | +33 |  |
| recovery (f6) | 5833 | +133 |  |
| recovery end / settle (f7) | 5917 | +217 |  |
| clip end | 6033 | +333 |  |
| idle restart | 6033 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

other sounds inside the phrase (release .. beat+200):
      5717    +17  sfx_crit vol=0.46 pitch=0.00 pan=0.00 duck=0.45

# OLD, normal TEMPO: the plain strike clip (beat 8200)

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip-end attack | 7300 | -900 |  |
| champ-frame idle | 7300 | -900 | frame=0 box=420,451,400,430 |
| clip-start strike | 7417 | -783 | beat=8200 speed=0.798 contactMs=796 frameMs=157 |
| champ-frame strike | 7417 | -783 | frame=0 box=420,451,400,430 |
| champ-frame strike | 7583 | -617 | frame=1 box=420,451,400,430 |
| champ-frame strike | 7733 | -467 | frame=2 box=420,451,400,430 |
| champ-frame strike | 7900 | -300 | frame=3 box=420,451,400,430 |
| champ-frame strike | 8050 | -150 | frame=4 box=420,451,400,430 |
| vfx-spawn fx_seeker_strike_strip8_512 | 8217 | +17 | profile=cast.strike subject=Creature/0 travel=- |
| sound sfx_cast | 8217 | +17 | vol=0.42 pitch=0.00 pan=0.00 |
| sound sfx_hit | 8217 | +17 | vol=0.22 pitch=0.00 pan=0.00 |
| number slot=0 | 8217 | +17 | amount=477 hits=1 crit=False skill=True |
| flash slot=0 | 8217 | +17 |  |
| sound sfx_enemy_down | 8217 | +17 | vol=0.36 pitch=0.00 pan=0.00 |
| vfx-spawn fx_death | 8217 | +17 | profile=death.creature subject=Creature/0 travel=- |
| champ-frame strike | 8217 | +17 | frame=5 box=420,451,400,430 |
| champ-frame strike | 8367 | +167 | frame=6 box=420,451,400,430 |
| champ-frame strike | 8517 | +317 | frame=7 box=420,451,400,430 |
| clip-end strike | 8833 | +633 |  |
| champ-frame idle | 8833 | +633 | frame=0 box=420,451,400,430 |
