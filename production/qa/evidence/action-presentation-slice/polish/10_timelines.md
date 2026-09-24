# Timelines (RH_PRESENT_TRACE, tools/asset-pipeline/action_timeline.py)

t = 0 is the fight's beat. What happens on it is presented on the first 60 fps frame after it (+0 or +17 ms), together.

# First cast, 4 targets

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
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |
| contact audio | 5217 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.12 |
| contact tick | 5233 | +33 | x=1564 y=745 |
| contact tick | 5250 | +50 | x=1159 y=736 |
| follow-through end (f4 -> f5) | 5233 | +33 |  |
| recovery (f6) | 5333 | +133 |  |
| recovery end / settle (f7) | 5417 | +217 |  |
| clip end | 5533 | +333 |  |
| idle restart | 5533 | +333 |  |

other sounds inside the phrase (release .. beat+200):
      5017   -183  sfx_hit vol=0.30 pitch=-0.25 pan=0.00 duck=0.45

# Five targets (a real five-creature wave, ~67 s into the fight)

## cast 10: beat 5200 ms, targets 0,1,2,3,4

| moment | playhead ms | vs beat | detail |
|---|---:|---:|---|
| clip start (ready, f0) | 4600 | -600 |  |
| anticipation (f1) | 4700 | -500 |  |
| extreme hold (f2) | 4800 | -400 |  |
| release pose (f3) | 4950 | -250 |  |
| release sound | 4950 | -250 | sfx_seeker_spray_release vol=0.40 pitch=0.00 pan=-0.12 |
| projectile creation | 4950 | -250 | x=748 y=564 knives=5 |
| first visible blade | 4950 | -250 | 5 sprites |
| CONTACT (blades land) | 5200 | +0 | x=1294 y=733 knives=5 |
| health (hits crossed) | 5200 | +0 | 5 x, last at +0 |
| flash | 5200 | +0 | 5 x, last at +0 |
| number | 5200 | +0 | 5 x, last at +0 |
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |
| contact audio | 5200 | +0 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.10 |
| contact tick | 5233 | +33 | x=1564 y=731 |
| contact tick | 5250 | +50 | x=1027 y=731 |
| follow-through end (f4 -> f5) | 5233 | +33 |  |
| recovery (f6) | 5333 | +133 |  |
| recovery end / settle (f7) | 5417 | +217 |  |
| clip end | 5533 | +333 |  |
| idle restart | 5533 | +333 |  |

other sounds inside the phrase (release .. beat+200):
      5000   -200  sfx_hit vol=0.30 pitch=-0.25 pan=0.00 duck=0.45

# Fast TEMPO build (bite, swift, quick, road_sign_2, brisk, rhythm, blitz, volley), every cast in 8 s

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
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |
| contact audio | 5817 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.15 |
| contact tick | 5833 | +33 | x=1292 y=725 |
| contact tick | 5850 | +50 | x=1564 y=741 |
| follow-through end (f4 -> f5) | 5833 | +33 |  |
| recovery (f6) | 5933 | +133 |  |
| recovery end / settle (f7) | 6017 | +217 |  |
| clip end | 6133 | +333 |  |
| idle restart | 6133 | +333 |  |

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
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |
| contact audio | 9517 | +17 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.18 |
| follow-through end (f4 -> f5) | 9533 | +33 |  |
| recovery (f6) | 9633 | +133 |  |
| recovery end / settle (f7) | 9717 | +217 |  |
| clip end | 9833 | +333 |  |
| idle restart | 9833 | +333 |  |

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
| generic impact puff | nan | +nan | none (replaced by the blades' own contact) |
| contact audio | 12400 | +0 | sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.20 |
| follow-through end (f4 -> f5) | 12433 | +33 |  |
| recovery (f6) | 12533 | +133 |  |
| recovery end / settle (f7) | 12617 | +217 |  |

other sounds inside the phrase (release .. beat+200):
     12400     +0  sfx_trait_lit vol=0.16 pitch=0.00 pan=0.00
