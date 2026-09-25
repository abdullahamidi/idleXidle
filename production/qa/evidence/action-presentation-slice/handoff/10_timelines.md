# Handoff timelines (RH_PRESENT_TRACE, tools/asset-pipeline/action_timeline.py)

t = 0 is SPRAY's beat. The first rows are the handoff that gave SPRAY the figure.

# AFTER: fast TEMPO build, every cast in 8 s
## cast 0: beat 5800 ms, targets 1,2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 5106 | -694 | its beat 5100 |
| previous recovery start | 5214 | -586 | protected frames end here (381 ms into its clip) |
| previous exit pose reached | 5248 | -552 | recovery 54 ms nominal (+300 ms settle) in 33 ms: x1.63 (Floor) |
| clip start (ready, f0) | 5250 | -550 |  |
| anticipation (f1) | 5333 | -467 |  |
| extreme hold (f2) | 5417 | -383 |  |
| release pose (f3) | 5567 | -233 |  |
| release sound | 5567 | -233 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 5567 | -233 | x=748 y=564 knives=3 |
| first visible blade | 5567 | -233 | 9 sprites |
| CONTACT (blades land) | 5817 | +17 | x=1428 y=737 knives=3 |
| health (hits crossed) | 5817 | +17 | 3 x, last at +17 |
| flash | 5817 | +17 | 3 x, last at +17 |
| number | 5817 | +17 | 3 x, last at +17 |
| contact audio | 5817 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.15 |
| contact tick | 5833 | +33 | x=1292 y=725 |
| follow-through end (f4 -> f5) | 5833 | +33 |  |
| contact tick | 5850 | +50 | x=1564 y=741 |
| recovery (f6) | 5933 | +133 |  |
| recovery end / settle (f7) | 6017 | +217 |  |
| clip end | 6133 | +333 |  |
| idle restart | 6133 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

## cast 1: beat 9500 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (strike) contact | 8700 | -800 | its beat 8700 |
| previous recovery start | 8873 | -627 | protected frames end here (607 ms into its clip) |
| previous exit pose reached | 8907 | -593 | recovery 87 ms nominal (+99 ms settle) in 33 ms: x2.60 (Floor) |
| clip start (ready, f0) | 8917 | -583 |  |
| anticipation (f1) | 9000 | -500 |  |
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

## cast 2: beat 12400 ms, targets 3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 11706 | -694 | its beat 11700 |
| previous recovery start | 11815 | -585 | protected frames end here (381 ms into its clip) |
| previous exit pose reached | 11848 | -552 | recovery 54 ms nominal (+300 ms settle) in 33 ms: x1.63 (Floor) |
| clip start (ready, f0) | 11850 | -550 |  |
| anticipation (f1) | 11933 | -467 |  |
| extreme hold (f2) | 12000 | -400 |  |
| release pose (f3) | 12150 | -250 |  |
| release sound | 12150 | -250 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 12150 | -250 | x=748 y=564 knives=1 |
| first visible blade | 12150 | -250 | 1 sprites |
| CONTACT (blades land) | 12400 | +0 | x=1551 y=742 knives=1 |
| health (hits crossed) | 12400 | +0 | 1 x, last at +0 |
| flash | 12400 | +0 | 1 x, last at +0 |
| number | 12400 | +0 | 1 x, last at +0 |
| contact audio | 12400 | +0 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.20 |
| follow-through end (f4 -> f5) | 12433 | +33 |  |
| recovery (f6) | 12533 | +133 |  |
| recovery end / settle (f7) | 12617 | +217 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

other sounds inside the phrase (release .. beat+200):
     12400     +0  sfx_trait_lit vol=0.16 pitch=0.00 pan=0.00

# BEFORE: the same build and seconds, with the recovery cut
## cast 0: beat 5800 ms, targets 1,2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 5317 | -483 |  |
| anticipation (f1) | 5367 | -433 |  |
| extreme hold (f2) | 5417 | -383 |  |
| release pose (f3) | 5567 | -233 |  |
| release sound | 5567 | -233 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 5567 | -233 | x=748 y=564 knives=3 |
| first visible blade | 5567 | -233 | 9 sprites |
| CONTACT (blades land) | 5817 | +17 | x=1428 y=737 knives=3 |
| health (hits crossed) | 5817 | +17 | 3 x, last at +17 |
| flash | 5817 | +17 | 3 x, last at +17 |
| number | 5817 | +17 | 3 x, last at +17 |
| contact audio | 5817 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.15 |
| contact tick | 5833 | +33 | x=1292 y=725 |
| follow-through end (f4 -> f5) | 5833 | +33 |  |
| contact tick | 5850 | +50 | x=1564 y=741 |
| recovery (f6) | 5933 | +133 |  |
| recovery end / settle (f7) | 6017 | +217 |  |
| clip end | 6133 | +333 |  |
| idle restart | 6133 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

## cast 1: beat 9500 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 9017 | -483 |  |
| anticipation (f1) | 9067 | -433 |  |
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

## cast 2: beat 12400 ms, targets 3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 11917 | -483 |  |
| anticipation (f1) | 11967 | -433 |  |
| extreme hold (f2) | 12000 | -400 |  |
| release pose (f3) | 12150 | -250 |  |
| release sound | 12150 | -250 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 12150 | -250 | x=748 y=564 knives=1 |
| first visible blade | 12150 | -250 | 1 sprites |
| CONTACT (blades land) | 12400 | +0 | x=1551 y=742 knives=1 |
| health (hits crossed) | 12400 | +0 | 1 x, last at +0 |
| flash | 12400 | +0 | 1 x, last at +0 |
| number | 12400 | +0 | 1 x, last at +0 |
| contact audio | 12400 | +0 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.20 |
| follow-through end (f4 -> f5) | 12433 | +33 |  |
| recovery (f6) | 12533 | +133 |  |
| recovery end / settle (f7) | 12617 | +217 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

other sounds inside the phrase (release .. beat+200):
     12400     +0  sfx_trait_lit vol=0.16 pitch=0.00 pan=0.00

# AFTER: the fastest build (TEMPO trained to 60, the RHYTHM route + VOLLEY)
## cast 0: beat 3500 ms, targets 0,1,2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 3417 | -83 |  |
| release sound | 3417 | -83 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 3417 | -83 | x=748 y=564 knives=4 |
| CONTACT (blades land) | 3500 | +0 | x=0 y=0 knives=4 |
| health (hits crossed) | 3500 | +0 | 4 x, last at +0 |
| flash | 3500 | +0 | 4 x, last at +0 |
| number | 3500 | +0 | 4 x, last at +0 |
| contact audio | 3500 | +0 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=-0.30 |
| follow-through end (f4 -> f5) | 3533 | +33 |  |
| recovery (f6) | 3633 | +133 |  |
| recovery end / settle (f7) | 3717 | +217 |  |
| clip end | 3833 | +333 |  |
| idle restart | 3833 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

other sounds inside the phrase (release .. beat+200):
      3500     +0  sfx_enemy_down vol=0.36 pitch=0.00 pan=0.00 duck=0.45

## cast 1: beat 5700 ms, targets 2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| previous action (attack) contact | 4812 | -888 | its beat 4800 |
| previous recovery start | 4877 | -823 | protected frames end here (227 ms into its clip) |
| previous exit pose reached | 5100 | -600 | recovery 32 ms nominal (+212 ms settle) in 223 ms: x1.00 (Natural) |
| clip start (ready, f0) | 5117 | -583 |  |
| strike beat not animated (yield) | 5200 | -500 | its exit 5339 > this action's latest start 5230 |
| anticipation (f1) | 5200 | -500 |  |
| extreme hold (f2) | 5317 | -383 |  |
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

# AFTER: normal TEMPO (no handoff: every clip ends before the next wants the figure)
## cast 0: beat 5200 ms, targets 0,1,2,3

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 4617 | -583 |  |
| anticipation (f1) | 4700 | -500 |  |
| extreme hold (f2) | 4817 | -383 |  |
| release pose (f3) | 4967 | -233 |  |
| release sound | 4967 | -233 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 4967 | -233 | x=748 y=564 knives=4 |
| first visible blade | 4967 | -233 | 12 sprites |
| CONTACT (blades land) | 5217 | +17 | x=1361 y=737 knives=4 |
| health (hits crossed) | 5217 | +17 | 4 x, last at +17 |
| flash | 5217 | +17 | 4 x, last at +17 |
| number | 5217 | +17 | 4 x, last at +17 |
| contact audio | 5217 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.12 |
| contact tick | 5233 | +33 | x=1564 y=745 |
| follow-through end (f4 -> f5) | 5233 | +33 |  |
| contact tick | 5250 | +50 | x=1159 y=736 |
| recovery (f6) | 5333 | +133 |  |
| recovery end / settle (f7) | 5417 | +217 |  |
| clip end | 5533 | +333 |  |
| idle restart | 5533 | +333 |  |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |

other sounds inside the phrase (release .. beat+200):
      5017   -183  sfx_hit vol=0.30 pitch=-0.25 pan=0.00 duck=0.45
      5217    +17  sfx_crit vol=0.46 pitch=0.00 pan=0.00 duck=0.45
