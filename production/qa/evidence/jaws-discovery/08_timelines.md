# JAWS discovery: every bite, and what the fight and the screen did (tools/asset-pipeline/reaction_timeline.py)

`att` is the EnemyStrike slot (Core's aggregate bite: always 0, the champion's slot). `JAWS` yes = its Skill event answered this bite. `figure doing` is the champion's committed clip/frame at that instant. Times are ms from the bite; +17 is the first 60 fps frame after it.

## normal TEMPO, the trigger at 7000 in full

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4   yes                      attack/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:4@+0
            +0  event        EnemyStrike slot=0 amount=4 at=4000 crit=False skill=False
            +0  sound        sfx_hit vol=0.30 pitch=-0.25 pan=0.00
            +0  vfx-spawn    fx_hit profile=impact.bite subject=Champion travel=-
            +0  event        Skill slot=3 amount=0 at=4000 crit=False skill=False
            +0  callout      SNARE
            +0  vfx-spawn    fx_seeker_trap_strip8_512 profile=cast.trap subject=EnemyRow travel=-
            +0  sound        sfx_cast vol=0.42 pitch=0.00 pan=0.00
            +0  sound        sfx_hit vol=0.40 pitch=0.25 pan=0.00
            +0  event        Strike slot=0 amount=4 at=4000 crit=False skill=True
            +0  champ-frame  attack frame=7 box=420,451,400,430
          +100  enemy-windup bite=5000 following=True
          +383  clip-end     attack
          +383  champ-frame  idle frame=0 box=420,451,400,430
  2     5000    0    4    no                  projectile/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 snd:sfx_seeker_spray_hit(0.50,0.00)@+217 Strike:0:248@+217 number:0=248@+217 flash:0@+217 Strike:1:274@+217 number:1=274@+217 flash:1@+217 Strike:2:381@+217 number:2=381@+217 snd:sfx_crit(0.46,0.00)@+217 flash:2@+217 Strike:3:274@+217 number:3=274@+217 flash:3@+217 snd:sfx_seeker_spray_tick(0.20,0.00)@+233 snd:sfx_seeker_spray_tick(0.20,0.00)@+250
           +17  event        EnemyStrike slot=0 amount=4 at=5000 crit=False skill=False
           +17  sound        sfx_hit vol=0.30 pitch=-0.25 pan=0.00 duck=0.45
           +17  vfx-spawn    fx_hit profile=impact.bite subject=Champion travel=-
           +17  champ-frame  projectile frame=4 box=420,451,400,430
           +17  perf-draw    sprites=36 draws=34 released=True
           +33  perf-draw    sprites=40 draws=34 released=True
           +50  perf-draw    sprites=44 draws=34 released=True
           +67  perf-draw    sprites=49 draws=35 released=True
           +83  perf-draw    sprites=52 draws=38 released=True
          +100  perf-draw    sprites=44 draws=30 released=True
          +117  enemy-windup bite=6000 following=True
          +117  perf-draw    sprites=44 draws=30 released=True
          +133  perf-draw    sprites=44 draws=30 released=True
          +150  perf-draw    sprites=48 draws=30 released=True
          +167  perf-draw    sprites=48 draws=30 released=True
          +183  perf-draw    sprites=48 draws=30 released=True
          +200  perf-draw    sprites=48 draws=30 released=True
          +217  contact      seeker.spray x=1361 y=737 knives=4
          +217  sound        sfx_seeker_spray_hit vol=0.50 pitch=0.00 pan=0.12
          +217  event        Skill slot=1 amount=0 at=5200 crit=False skill=False
          +217  event        Strike slot=0 amount=248 at=5200 crit=False skill=True
          +217  number       slot=0 amount=248 hits=1 crit=False skill=True
          +217  flash        slot=0
          +217  event        Strike slot=1 amount=274 at=5200 crit=False skill=True
          +217  number       slot=1 amount=274 hits=1 crit=False skill=True
          +217  flash        slot=1
          +217  event        Strike slot=2 amount=381 at=5200 crit=True skill=True
          +217  number       slot=2 amount=381 hits=1 crit=True skill=True
          +217  sound        sfx_crit vol=0.46 pitch=0.00 pan=0.00 duck=0.45
          +217  flash        slot=2
          +217  event        Strike slot=3 amount=274 at=5200 crit=False skill=True
          +217  number       slot=3 amount=274 hits=1 crit=False skill=True
          +217  flash        slot=3
          +217  event        Beat slot=0 amount=4 at=5200 crit=False skill=False
          +217  perf-draw    sprites=60 draws=41 released=True
          +233  contact-tick x=1564 y=745
          +233  sound        sfx_seeker_spray_tick vol=0.20 pitch=0.00 pan=0.21
          +233  champ-frame  projectile frame=5 box=420,451,400,430
          +233  perf-draw    sprites=51 draws=39 released=True
          +250  contact-tick x=1159 y=736
          +250  sound        sfx_seeker_spray_tick vol=0.20 pitch=0.00 pan=0.04
          +250  perf-draw    sprites=44 draws=33 released=True
          +267  perf-draw    sprites=40 draws=33 released=True
          +283  perf-draw    sprites=32 draws=29 released=True
          +300  perf-draw    sprites=24 draws=29 released=True
          +317  perf-draw    sprites=20 draws=22 released=True
          +333  champ-frame  projectile frame=6 box=420,451,400,430
          +333  perf-draw    sprites=20 draws=22 released=True
          +350  perf-draw    sprites=20 draws=18 released=True
          +367  perf-draw    sprites=20 draws=18 released=True
          +383  perf-draw    sprites=12 draws=18 released=True
          +400  perf-draw    sprites=12 draws=18 released=True
          +417  champ-frame  projectile frame=7 box=420,451,400,430
          +417  perf-draw    sprites=0 draws=17 released=True
          +433  perf-draw    sprites=0 draws=17 released=True
          +450  perf-draw    sprites=0 draws=17 released=True
  3     6000    0    4    no                        idle/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
           +17  event        EnemyStrike slot=0 amount=4 at=6000 crit=False skill=False
           +17  sound        sfx_hit vol=0.30 pitch=-0.25 pan=0.00
           +17  vfx-spawn    fx_hit profile=impact.bite subject=Champion travel=-
           +50  champ-frame  idle frame=4 box=420,451,400,430
          +117  enemy-windup bite=7000 following=True
          +167  champ-frame  idle frame=5 box=420,451,400,430
          +217  clip-start   attack beat=6700 speed=1.293 contactMs=486 frameMs=97
          +217  champ-frame  attack frame=0 box=420,451,400,430
          +317  champ-frame  attack frame=1 box=420,451,400,430
          +417  champ-frame  attack frame=2 box=420,451,400,430
  4     7000    0    4   yes       3000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:4@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=4@+17 flash:0@+17 fx:fx_weakhit/impact.weak/Creature/0@+17
           +17  event        EnemyStrike slot=0 amount=4 at=7000 crit=False skill=False
           +17  sound        sfx_hit vol=0.30 pitch=-0.25 pan=0.00
           +17  vfx-spawn    fx_hit profile=impact.bite subject=Champion travel=-
           +17  event        Skill slot=3 amount=0 at=7000 crit=False skill=False
           +17  callout      SNARE
           +17  vfx-spawn    fx_seeker_trap_strip8_512 profile=cast.trap subject=EnemyRow travel=-
           +17  sound        sfx_cast vol=0.42 pitch=0.00 pan=0.00
           +17  sound        sfx_hit vol=0.40 pitch=0.25 pan=0.00
           +17  event        Strike slot=0 amount=4 at=7000 crit=False skill=True
           +17  sound        sfx_hit vol=0.22 pitch=0.00 pan=0.00
           +17  number       slot=0 amount=4 hits=1 crit=False skill=True
           +17  flash        slot=0
           +17  vfx-spawn    fx_weakhit profile=impact.weak subject=Creature/0 travel=-
          +117  enemy-windup bite=8000 following=True
          +300  clip-end     attack
          +300  champ-frame  idle frame=0 box=420,451,400,430
          +433  champ-frame  idle frame=1 box=420,451,400,430
  5     8000    0    4    no                  hard_hands/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83
           +17  event        EnemyStrike slot=0 amount=4 at=8000 crit=False skill=False
           +17  sound        sfx_hit vol=0.30 pitch=-0.25 pan=0.00
           +17  vfx-spawn    fx_hit profile=impact.bite subject=Champion travel=-
           +17  root         x=-12 y=0
           +17  perf-draw    sprites=0 draws=17 released=False
           +33  root         x=-14 y=0
           +33  perf-draw    sprites=0 draws=17 released=False
           +50  root         x=-15 y=0
           +50  perf-draw    sprites=0 draws=17 released=False
           +67  root         x=-16 y=0
           +67  perf-draw    sprites=0 draws=17 released=False
           +83  release      seeker.hard_hands x=750 y=508 knives=1 launch=1192,739 reach=399
           +83  callout      HAMMER
           +83  sound        sfx_seeker_hard_hands_commit vol=0.34 pitch=0.00 pan=-0.12
           +83  root         x=-13 y=0
           +83  champ-frame  hard_hands frame=3 box=407,451,400,430
           +83  perf-draw    sprites=0 draws=17 released=True
          +100  root         x=20 y=0
          +100  perf-draw    sprites=0 draws=17 released=True
```

## normal TEMPO, 16 s

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4   yes                      attack/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:4@+0 number:0=4@+517
  2     5000    0    4    no                  projectile/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 snd:sfx_seeker_spray_hit(0.50,0.00)@+217 Strike:0:248@+217 number:0=248@+217 flash:0@+217 Strike:1:274@+217 number:1=274@+217 flash:1@+217 Strike:2:238@+217 number:2=238@+217 flash:2@+217 Strike:3:274@+217 number:3=274@+217 flash:3@+217 snd:sfx_seeker_spray_tick(0.20,0.00)@+233 snd:sfx_seeker_spray_tick(0.20,0.00)@+250
  3     6000    0    4    no                        idle/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    4   yes       3000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:4@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=4@+17 flash:0@+17 fx:fx_weakhit/impact.weak/Creature/0@+17
  5     8000    0    4    no                  hard_hands/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+217 Strike:0:477@+217 number:0=477@+217 flash:0@+217 EnemyDown:0:0@+217 snd:sfx_enemy_down(0.36,0.00)@+217 fx:fx_death/death.creature/Creature/0@+217
  6     9000    0    3    no                        idle/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  7    10000    0    3   yes       3000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:3@+17 number:1=3@+500
  8    11000    0    3    no                  projectile/3  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_seeker_spray_hit(0.50,0.00)@+200 Strike:1:287@+200 number:1=287@+200 flash:1@+200 Strike:2:246@+200 number:2=246@+200 flash:2@+200 Strike:3:282@+200 number:3=282@+200 flash:3@+200 snd:sfx_seeker_spray_tick(0.20,0.00)@+233 snd:sfx_seeker_spray_tick(0.20,0.00)@+250
  9    12000    0    3    no                        idle/3  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0
 10    13000    0    3   yes       3000           attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:1:3@+0 snd:sfx_hit(0.22,0.00)@+0 number:1=3@+0 flash:1@+0
```

## fast TEMPO, 5 s

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4    no                  hard_hands/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:HAMMER@+183 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+183 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+300 Strike:0:284@+300 number:0=284@+300 flash:0@+300 Strike:1:315@+300 number:1=315@+300 flash:1@+300
  2     5000    0    4   yes                      attack/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:3@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=3@+17 flash:0@+17 Strike:1:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=1@+17 flash:1@+17 fx:fx_weakhit/impact.weak/Creature/1@+17 snd:sfx_trait_lit(0.16,0.00)@+117 Strike:0:145@+117 snd:sfx_hit(0.38,0.00)@+117 number:0=145@+117 EnemyDown:0:0@+117 snd:sfx_enemy_down(0.36,0.00)@+117 fx:fx_death/death.creature/Creature/0@+117 callout:VOLLEY@+567 snd:sfx_seeker_spray_release(0.40,0.00)@+567
  3     6000    0    3    no                  projectile/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    3   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=2@+17 flash:1@+17 Strike:2:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=2@+17 snd:sfx_crit(0.46,0.00)@+17 flash:2@+17 fx:fx_weakhit/impact.weak/Creature/2@+17 Strike:1:140@+317 snd:sfx_hit(0.38,0.00)@+317 number:1=140@+317 flash:1@+317
  5     8000    0    2    no                      attack/4  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+583 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+583
  6     9000    0    2   yes       2000       projectile/0  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 Strike:3:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:3=1@+17 flash:3@+17 fx:fx_weakhit/impact.weak/Creature/3@+17 callout:VOLLEY@+267 snd:sfx_seeker_spray_release(0.40,0.00)@+267 snd:sfx_seeker_spray_hit(0.50,0.00)@+517 Strike:2:156@+517 number:2=156@+517 flash:2@+517 Strike:3:179@+517 number:3=179@+517 flash:3@+517
  7    10000    0    2    no                        idle/1  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:2:140@+300 snd:sfx_hit(0.38,0.00)@+300 number:2=140@+300 flash:2@+300
  8    11000    0    2   yes       2000           attack/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:2:2@+0 snd:sfx_hit(0.22,0.00)@+0 number:2=2@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0
  9    12000    0    1    no                  projectile/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:VOLLEY@+150 snd:sfx_seeker_spray_release(0.40,0.00)@+150 snd:sfx_trait_lit(0.16,0.00)@+400 snd:sfx_seeker_spray_hit(0.50,0.00)@+400 Strike:3:189@+400 number:3=189@+400 flash:3@+400
 10    13000    0    1   yes       2000       hard_hands/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+200 Strike:3:357@+200 number:3=357@+200 flash:3@+200 EnemyDown:3:0@+200 snd:sfx_enemy_down(0.36,0.00)@+200 fx:fx_death/death.creature/Creature/3@+200
 11     1000    0    4    no                      attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+400 Strike:0:140@+400 snd:sfx_hit(0.38,0.00)@+400 number:0=140@+400 flash:0@+400 fx:fx_weakhit/impact.weak/Creature/0@+400
 12     2000    0    4   yes     -11000       projectile/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:2@+0 Strike:1:1@+0 snd:sfx_seeker_spray_hit(0.50,0.00)@+100 Strike:0:169@+100 number:0=169@+100 flash:0@+100 Strike:1:164@+100 number:1=164@+100 flash:1@+100 Strike:2:143@+100 number:2=143@+100 flash:2@+100 Strike:3:164@+100 number:3=164@+100 flash:3@+100 snd:sfx_seeker_spray_tick(0.20,0.00)@+133 snd:sfx_seeker_spray_tick(0.20,0.00)@+150 number:0=3@+500
 13     3000    0    4    no                      attack/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+500 Strike:0:145@+500 snd:sfx_hit(0.38,0.00)@+500 number:0=145@+500 flash:0@+500 fx:fx_weakhit/impact.weak/Creature/0@+500
 14     4000    0    4   yes       2000       hard_hands/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:2@+0 Strike:1:1@+0 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_trait_lit(0.16,0.00)@+200 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+200 Strike:0:325@+200 number:0=325@+200 flash:0@+200 Strike:1:315@+200 number:1=315@+200 flash:1@+200 number:0=3@+500
 15     5000    0    3    no                  projectile/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_seeker_spray_tick(0.20,0.00)@+33 snd:sfx_seeker_spray_tick(0.20,0.00)@+50
```

## fast TEMPO, the idle trigger

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4    no                  hard_hands/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:HAMMER@+183 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+183 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+300 Strike:0:284@+300 number:0=284@+300 flash:0@+300 Strike:1:315@+300 number:1=315@+300 flash:1@+300
  2     5000    0    4   yes                      attack/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:3@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=3@+17 flash:0@+17 Strike:1:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=1@+17 flash:1@+17 fx:fx_weakhit/impact.weak/Creature/1@+17 snd:sfx_trait_lit(0.16,0.00)@+117 Strike:0:145@+117 snd:sfx_hit(0.38,0.00)@+117 number:0=145@+117 EnemyDown:0:0@+117 snd:sfx_enemy_down(0.36,0.00)@+117 fx:fx_death/death.creature/Creature/0@+117 callout:VOLLEY@+567 snd:sfx_seeker_spray_release(0.40,0.00)@+567
  3     6000    0    3    no                  projectile/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    3   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=2@+17 flash:1@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 fx:fx_weakhit/impact.weak/Creature/2@+17 Strike:1:140@+317 snd:sfx_hit(0.38,0.00)@+317 number:1=140@+317 flash:1@+317
  5     8000    0    3    no                      attack/4  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+583 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+583
  6     9000    0    2   yes       2000       projectile/0  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 Strike:3:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:3=1@+17 flash:3@+17 fx:fx_weakhit/impact.weak/Creature/3@+17 callout:VOLLEY@+267 snd:sfx_seeker_spray_release(0.40,0.00)@+267 snd:sfx_seeker_spray_hit(0.50,0.00)@+517 Strike:2:156@+517 number:2=156@+517 flash:2@+517 Strike:3:279@+517 number:3=279@+517 snd:sfx_crit(0.46,0.00)@+517 flash:3@+517
  7    10000    0    2    no                        idle/1  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:2:140@+300 snd:sfx_hit(0.38,0.00)@+300 number:2=140@+300 flash:2@+300
  8    11000    0    2   yes       2000           attack/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:2:2@+0 snd:sfx_hit(0.22,0.00)@+0 number:2=2@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0
  9    12000    0    1    no                  projectile/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:VOLLEY@+150 snd:sfx_seeker_spray_release(0.40,0.00)@+150 snd:sfx_trait_lit(0.16,0.00)@+400 snd:sfx_seeker_spray_hit(0.50,0.00)@+400 Strike:3:184@+400 number:3=184@+400 flash:3@+400
 10    13000    0    1   yes       2000       hard_hands/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+200 Strike:3:348@+200 number:3=348@+200 flash:3@+200 EnemyDown:3:0@+200 snd:sfx_enemy_down(0.36,0.00)@+200 fx:fx_death/death.creature/Creature/3@+200
 11     1000    0    4    no                      attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+400 Strike:0:140@+400 snd:sfx_hit(0.38,0.00)@+400 number:0=140@+400 flash:0@+400 fx:fx_weakhit/impact.weak/Creature/0@+400
 12     2000    0    4   yes     -11000       projectile/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:2@+0 Strike:1:1@+0 snd:sfx_seeker_spray_hit(0.50,0.00)@+100 Strike:0:169@+100 number:0=169@+100 flash:0@+100 Strike:1:164@+100 number:1=164@+100 flash:1@+100 Strike:2:143@+100 number:2=143@+100 flash:2@+100 Strike:3:164@+100 number:3=164@+100 flash:3@+100 snd:sfx_seeker_spray_tick(0.20,0.00)@+133 snd:sfx_seeker_spray_tick(0.20,0.00)@+150 number:0=3@+500
 13     3000    0    4    no                      attack/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+500 Strike:0:145@+500 snd:sfx_hit(0.38,0.00)@+500 number:0=145@+500 flash:0@+500 fx:fx_weakhit/impact.weak/Creature/0@+500
 14     4000    0    4   yes       2000       hard_hands/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:2@+0 Strike:1:1@+0 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_trait_lit(0.16,0.00)@+200 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+200 Strike:0:325@+200 number:0=325@+200 flash:0@+200 Strike:1:315@+200 number:1=315@+200 flash:1@+200 number:0=3@+500
 15     5000    0    3    no                  projectile/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_seeker_spray_tick(0.20,0.00)@+33 snd:sfx_seeker_spray_tick(0.20,0.00)@+50
 16     6000    0    3   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:2@+17 Strike:2:1@+17 Strike:1:145@+417 snd:sfx_hit(0.38,0.00)@+417 number:1=145@+417 flash:1@+417 fx:fx_weakhit/impact.weak/Creature/1@+417 number:1=3@+517
 17     7000    0    3    no                      attack/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 snd:sfx_trait_lit(0.16,0.00)@+117 Strike:1:145@+117 snd:sfx_hit(0.38,0.00)@+117 number:1=145@+117 flash:1@+117 callout:VOLLEY@+567 snd:sfx_seeker_spray_release(0.40,0.00)@+567
 18     8000    0    2   yes       2000       projectile/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:2:2@+17 Strike:3:1@+17 callout:HAMMER@+483 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+483 number:2=3@+517
 19     9000    0    2    no                  hard_hands/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:2:145@+400 snd:sfx_hit(0.38,0.00)@+400 number:2=145@+400 flash:2@+400 fx:fx_weakhit/impact.weak/Creature/2@+400
 20    10000    0    2   yes       2000           attack/2  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:2:2@+0 Strike:3:1@+0 snd:sfx_trait_lit(0.16,0.00)@+100 Strike:2:150@+100 snd:sfx_hit(0.38,0.00)@+100 number:2=150@+100 flash:2@+100 number:2=3@+500 callout:VOLLEY@+550 snd:sfx_seeker_spray_release(0.40,0.00)@+550
 21    11000    0    1    no                  projectile/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Strike:3:140@+600 snd:sfx_hit(0.38,0.00)@+600 number:3=140@+600 flash:3@+600 fx:fx_weakhit/impact.weak/Creature/3@+600
 22    12000    0    1   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:3:1@+0 snd:sfx_trait_lit(0.16,0.00)@+300 Strike:3:145@+300 snd:sfx_hit(0.38,0.00)@+300 number:3=145@+300 flash:3@+300 EnemyDown:3:0@+300 snd:sfx_enemy_down(0.36,0.00)@+300 fx:fx_death/death.creature/Creature/3@+300 number:3=1@+300
 23     1000    0    4    no                  projectile/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:VOLLEY@+250 snd:sfx_seeker_spray_release(0.40,0.00)@+250 snd:sfx_seeker_spray_hit(0.50,0.00)@+500 Strike:0:137@+500 number:0=137@+500 flash:0@+500
 24     2000    0    4   yes     -10000             idle/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:0@+0 Strike:0:134@+300 snd:sfx_hit(0.38,0.00)@+300 number:0=134@+300 flash:0@+300 fx:fx_weakhit/impact.weak/Creature/0@+300
```

## fast TEMPO, during HARD HANDS

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4    no                  hard_hands/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:HAMMER@+183 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+183 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+300 Strike:0:284@+300 number:0=284@+300 flash:0@+300 Strike:1:315@+300 number:1=315@+300 flash:1@+300
  2     5000    0    4   yes                      attack/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:3@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=3@+17 flash:0@+17 Strike:1:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=1@+17 flash:1@+17 fx:fx_weakhit/impact.weak/Creature/1@+17 snd:sfx_trait_lit(0.16,0.00)@+117 Strike:0:145@+117 snd:sfx_hit(0.38,0.00)@+117 number:0=145@+117 EnemyDown:0:0@+117 snd:sfx_enemy_down(0.36,0.00)@+117 fx:fx_death/death.creature/Creature/0@+117 callout:VOLLEY@+567 snd:sfx_seeker_spray_release(0.40,0.00)@+567
  3     6000    0    3    no                  projectile/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    3   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=2@+17 flash:1@+17 Strike:2:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=2@+17 snd:sfx_crit(0.46,0.00)@+17 flash:2@+17 fx:fx_weakhit/impact.weak/Creature/2@+17 Strike:1:140@+317 snd:sfx_hit(0.38,0.00)@+317 number:1=140@+317 flash:1@+317
  5     8000    0    2    no                      attack/4  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+583 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+583
  6     9000    0    2   yes       2000       projectile/0  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 Strike:3:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:3=1@+17 flash:3@+17 fx:fx_weakhit/impact.weak/Creature/3@+17 callout:VOLLEY@+267 snd:sfx_seeker_spray_release(0.40,0.00)@+267 snd:sfx_seeker_spray_hit(0.50,0.00)@+517 Strike:2:156@+517 number:2=156@+517 flash:2@+517 Strike:3:179@+517 number:3=179@+517 flash:3@+517
  7    10000    0    2    no                        idle/1  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:2:140@+300 snd:sfx_hit(0.38,0.00)@+300 number:2=140@+300 flash:2@+300
  8    11000    0    2   yes       2000           attack/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:2:2@+0 snd:sfx_hit(0.22,0.00)@+0 number:2=2@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0
  9    12000    0    1    no                  projectile/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:VOLLEY@+150 snd:sfx_seeker_spray_release(0.40,0.00)@+150 snd:sfx_trait_lit(0.16,0.00)@+400 snd:sfx_seeker_spray_hit(0.50,0.00)@+400 Strike:3:300@+400 number:3=300@+400 snd:sfx_crit(0.46,0.00)@+400 flash:3@+400 EnemyDown:3:0@+400 snd:sfx_enemy_down(0.36,0.00)@+400 fx:fx_death/death.creature/Creature/3@+400
 10     1000    0    4   yes     -10000       hard_hands/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:0=1@+0 flash:0@+0 fx:fx_weakhit/impact.weak/Creature/0@+0 Strike:1:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:1=1@+0 flash:1@+0 Strike:0:140@+500 snd:sfx_hit(0.38,0.00)@+500 number:0=140@+500 flash:0@+500 fx:fx_weakhit/impact.weak/Creature/0@+500
 11     2000    0    4    no                      attack/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+200 Strike:0:145@+200 snd:sfx_hit(0.38,0.00)@+200 number:0=145@+200 flash:0@+200
 12     3000    0    4   yes       2000       projectile/5  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:3@+0 snd:sfx_hit(0.22,0.00)@+0 number:0=3@+0 fx:fx_weakhit/impact.weak/Creature/0@+0 Strike:1:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:1=1@+0
 13     4000    0    4    no                      attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_trait_lit(0.16,0.00)@+400 Strike:0:150@+400 snd:sfx_hit(0.38,0.00)@+400 number:0=150@+400 flash:0@+400
 14     5000    0    4   yes       2000       hard_hands/3  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:3@+0 snd:sfx_hit(0.22,0.00)@+0 number:0=3@+0 flash:0@+0 fx:fx_weakhit/impact.weak/Creature/0@+0 Strike:1:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:1=1@+0 flash:1@+0 snd:sfx_trait_lit(0.16,0.00)@+100 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+100 Strike:0:334@+100 number:0=334@+100 EnemyDown:0:0@+100 snd:sfx_enemy_down(0.36,0.00)@+100 fx:fx_death/death.creature/Creature/0@+100 Strike:1:324@+100 number:1=324@+100
 15     6000    0    3    no                  projectile/5  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
```

## fast TEMPO, during SPRAY

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4    no                  hard_hands/0  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:HAMMER@+183 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+183 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+300 Strike:0:284@+300 number:0=284@+300 flash:0@+300 Strike:1:315@+300 number:1=315@+300 flash:1@+300
  2     5000    0    4   yes                      attack/3  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:0:3@+17 snd:sfx_hit(0.22,0.00)@+17 number:0=3@+17 flash:0@+17 Strike:1:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=1@+17 flash:1@+17 fx:fx_weakhit/impact.weak/Creature/1@+17 snd:sfx_trait_lit(0.16,0.00)@+117 Strike:0:225@+117 snd:sfx_hit(0.38,0.00)@+117 number:0=225@+117 snd:sfx_crit(0.46,0.00)@+117 EnemyDown:0:0@+117 snd:sfx_enemy_down(0.36,0.00)@+117 fx:fx_death/death.creature/Creature/0@+117 callout:VOLLEY@+567 snd:sfx_seeker_spray_release(0.40,0.00)@+567
  3     6000    0    3    no                  projectile/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    3   yes       2000           attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:1:2@+17 snd:sfx_hit(0.22,0.00)@+17 number:1=2@+17 flash:1@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 fx:fx_weakhit/impact.weak/Creature/2@+17 Strike:1:140@+317 snd:sfx_hit(0.38,0.00)@+317 number:1=140@+317 flash:1@+317
  5     8000    0    3    no                      attack/4  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+583 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+583
  6     9000    0    2   yes       2000       projectile/0  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Skill:3:0@+17 callout:SNARE@+17 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+17 snd:sfx_cast(0.42,0.00)@+17 snd:sfx_hit(0.40,0.25)@+17 Strike:2:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:2=1@+17 flash:2@+17 Strike:3:1@+17 snd:sfx_hit(0.22,0.00)@+17 number:3=1@+17 flash:3@+17 fx:fx_weakhit/impact.weak/Creature/3@+17 callout:VOLLEY@+267 snd:sfx_seeker_spray_release(0.40,0.00)@+267 snd:sfx_seeker_spray_hit(0.50,0.00)@+517 Strike:2:156@+517 number:2=156@+517 flash:2@+517 Strike:3:174@+517 number:3=174@+517 flash:3@+517
  7    10000    0    2    no                        idle/1  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:2:140@+300 snd:sfx_hit(0.38,0.00)@+300 number:2=140@+300 flash:2@+300
  8    11000    0    2   yes       2000           attack/4  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:2:2@+0 snd:sfx_hit(0.22,0.00)@+0 number:2=2@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0
  9    12000    0    1    no                  projectile/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 callout:VOLLEY@+150 snd:sfx_seeker_spray_release(0.40,0.00)@+150 snd:sfx_trait_lit(0.16,0.00)@+400 snd:sfx_seeker_spray_hit(0.50,0.00)@+400 Strike:3:184@+400 number:3=184@+400 flash:3@+400
 10    13000    0    1   yes       2000       hard_hands/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:3:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:3=1@+0 flash:3@+0 fx:fx_weakhit/impact.weak/Creature/3@+0 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+200 Strike:3:348@+200 number:3=348@+200 flash:3@+200
 11     1000    0    4   yes     -12000       projectile/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Skill:3:0@+0 callout:SNARE@+0 fx:fx_seeker_trap_strip8_512/cast.trap/EnemyRow@+0 snd:sfx_cast(0.42,0.00)@+0 snd:sfx_hit(0.40,0.25)@+0 Strike:0:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:0=1@+0 flash:0@+0 Strike:1:1@+0 snd:sfx_hit(0.22,0.00)@+0 number:1=1@+0 flash:1@+0 fx:fx_weakhit/impact.weak/Creature/1@+0 callout:VOLLEY@+150 snd:sfx_seeker_spray_release(0.40,0.00)@+150 snd:sfx_seeker_spray_hit(0.50,0.00)@+400 Strike:0:164@+400 number:0=164@+400 flash:0@+400 Strike:1:164@+400 number:1=164@+400 flash:1@+400 Strike:2:143@+400 number:2=143@+400 flash:2@+400 Strike:3:164@+400 number:3=164@+400 flash:3@+400 snd:sfx_seeker_spray_tick(0.20,0.00)@+433 snd:sfx_seeker_spray_tick(0.20,0.00)@+450
```
