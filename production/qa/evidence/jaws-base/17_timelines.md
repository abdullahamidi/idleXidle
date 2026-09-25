# Base JAWS: every trigger's life on the fight's playhead (tools/asset-pipeline/reaction_timeline.py --report)

Times are ms from the contact (the bite); +17 is the first 60 fps frame after it. `armed before it` is the
fight's own ReactionArmed report: how long JAWS waited armed before this bite.

## normal TEMPO, the bite at 7000 (a basic swing)

```
== trigger 1 at 4000 ==
   enemy wind-up start           -583  bite=4000
   contact (the bite)              +0  at=4000
   champion damage                 +0  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=4000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=4
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1119,806 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +2830  at=6830 ran=2830
   next trigger                 +3000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=7
   damage number                  +17  slot=0 amount=7 hits=1 crit=True skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,807 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## normal TEMPO, 16 s

```
== trigger 1 at 4000 ==
   enemy wind-up start           -583  bite=4000
   contact (the bite)              +0  at=4000
   champion damage                 +0  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=4000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=4
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1119,806 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +2830  at=6830 ran=2830
   next trigger                 +3000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=7
   damage number                  +17  slot=0 amount=7 hits=1 crit=True skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,807 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +2830  at=9830 ran=2830
   next trigger                 +3000  

== trigger 3 at 10000 ==
   enemy wind-up start           -883  bite=10000
   contact (the bite)             +17  at=10000
   champion damage                +17  amount=3
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=10000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=3
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1252,799 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +2830  at=12830 ran=2830
   next trigger                 +3000  

== trigger 4 at 13000 ==
   enemy wind-up start           -900  bite=13000
   contact (the bite)              +0  at=13000
   champion damage                 +0  amount=3
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=13000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike                +0  creature=1 amount=3
   damage number                   +0  slot=1 amount=3 hits=1 crit=False skill=True
   target flash                    +0  slot=1
   jaws shut (snap)               +50  at=50 clamp=1255,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## fast TEMPO, 5 s

```
== trigger 1 at 5000 ==
   enemy wind-up start           -900  bite=5000
   contact (the bite)             +17  at=5000
   champion damage                +17  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=5000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=3
   damage number                  +17  slot=0 amount=3 hits=1 crit=False skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                       +133  at=133 early=True
   world effect end              +250  lived=250
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=6583 ran=1583
   next trigger                 +2000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=3
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=1,2 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=2
   damage number                  +17  slot=1 amount=2 hits=1 crit=False skill=True
   target flash                   +17  slot=1
   jaws shut (snap)               +50  at=50 clamp=1252,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=8583 ran=1583
   next trigger                 +2000  

== trigger 3 at 9000 ==
   enemy wind-up start           -883  bite=9000
   contact (the bite)             +17  at=9000
   champion damage                +17  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=9000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike               +17  creature=2 amount=1
   damage number                  +17  slot=2 amount=1 hits=1 crit=False skill=True
   target flash                   +17  slot=2
   jaws shut (snap)               +50  at=50 clamp=1388,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=10583 ran=1583
   next trigger                 +2000  

== trigger 4 at 11000 ==
   enemy wind-up start           -900  bite=11000
   contact (the bite)              +0  at=11000
   champion damage                 +0  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=11000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike                +0  creature=2 amount=2
   damage number                   +0  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1391,810 belt=680,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +1583  at=12583 ran=1583
   next trigger                 +2000  

== trigger 5 at 13000 ==
   enemy wind-up start           -900  bite=13000
   contact (the bite)              +0  at=13000
   champion damage                 +0  amount=1
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=13000 targets=3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.28
   reflected strike                +0  creature=3 amount=1
   damage number                   +0  slot=3 amount=1 hits=1 crit=False skill=True
   target flash                    +0  slot=3
   jaws shut (snap)               +50  at=50 clamp=1527,809 belt=633,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed               -12417  at=583 ran=1583
   next trigger                -12000  

== trigger 6 at 1000 ==
   enemy wind-up start           -900  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=1
   damage number                   +0  slot=0 amount=1 hits=1 crit=False skill=True
   target flash                    +0  slot=0
   jaws shut (snap)               +50  at=50 clamp=1119,802 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +1531  at=2531 ran=1531
   next trigger                 +2000  

== trigger 7 at 3000 ==
   enemy wind-up start           -900  bite=3000
   contact (the bite)              +0  at=3000
   champion damage                 +0  amount=4
   armed before it (window)      -469  ran=1531 ms; armed 469 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=3000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=2
   damage number                   +0  slot=0 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1119,810 belt=660,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +1531  at=4531 ran=1531
   next trigger                 +2000  

== trigger 8 at 5000 ==
   enemy wind-up start           -900  bite=5000
   contact (the bite)              +0  at=5000
   champion damage                 +0  amount=4
   armed before it (window)      -469  ran=1531 ms; armed 469 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=5000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=3
   damage number                   +0  slot=0 amount=3 hits=1 crit=False skill=True
   target flash                    +0  slot=0
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end                 -  -
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## fast TEMPO, mid-swing

```
== trigger 1 at 5000 ==
   enemy wind-up start           -900  bite=5000
   contact (the bite)             +17  at=5000
   champion damage                +17  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=5000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=3
   damage number                  +17  slot=0 amount=3 hits=1 crit=False skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                       +133  at=133 early=True
   world effect end              +250  lived=250
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=6583 ran=1583
   next trigger                 +2000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=3
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=1,2 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=2
   damage number                  +17  slot=1 amount=2 hits=1 crit=False skill=True
   target flash                   +17  slot=1
   jaws shut (snap)               +50  at=50 clamp=1252,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=8583 ran=1583
   next trigger                 +2000  

== trigger 3 at 9000 ==
   enemy wind-up start           -883  bite=9000
   contact (the bite)             +17  at=9000
   champion damage                +17  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=9000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike               +17  creature=2 amount=1
   damage number                  +17  slot=2 amount=1 hits=1 crit=False skill=True
   target flash                   +17  slot=2
   jaws shut (snap)               +50  at=50 clamp=1388,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=10583 ran=1583
   next trigger                 +2000  

== trigger 4 at 11000 ==
   enemy wind-up start           -900  bite=11000
   contact (the bite)              +0  at=11000
   champion damage                 +0  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=11000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike                +0  creature=2 amount=2
   damage number                   +0  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1391,810 belt=680,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end                 -  -
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## fast TEMPO, during HARD HANDS

```
== trigger 1 at 5000 ==
   enemy wind-up start           -900  bite=5000
   contact (the bite)             +17  at=5000
   champion damage                +17  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=5000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=3
   damage number                  +17  slot=0 amount=3 hits=1 crit=False skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                       +133  at=133 early=True
   world effect end              +250  lived=250
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=6583 ran=1583
   next trigger                 +2000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=3
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=1,2 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=2
   damage number                  +17  slot=1 amount=2 hits=1 crit=False skill=True
   target flash                   +17  slot=1
   jaws shut (snap)               +50  at=50 clamp=1252,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=8583 ran=1583
   next trigger                 +2000  

== trigger 3 at 9000 ==
   enemy wind-up start           -883  bite=9000
   contact (the bite)             +17  at=9000
   champion damage                +17  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=9000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike               +17  creature=2 amount=1
   damage number                  +17  slot=2 amount=1 hits=1 crit=False skill=True
   target flash                   +17  slot=2
   jaws shut (snap)               +50  at=50 clamp=1388,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=10583 ran=1583
   next trigger                 +2000  

== trigger 4 at 11000 ==
   enemy wind-up start           -900  bite=11000
   contact (the bite)              +0  at=11000
   champion damage                 +0  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=11000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike                +0  creature=2 amount=2
   damage number                   +0  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1391,810 belt=680,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +1583  at=12583 ran=1583
   next trigger                 +2000  

== trigger 5 at 13000 ==
   enemy wind-up start           -900  bite=13000
   contact (the bite)              +0  at=13000
   champion damage                 +0  amount=1
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=13000 targets=3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.28
   reflected strike                +0  creature=3 amount=1
   damage number                   +0  slot=3 amount=1 hits=1 crit=False skill=True
   target flash                    +0  slot=3
   jaws shut (snap)               +50  at=50 clamp=1527,809 belt=633,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## fast TEMPO, during SPRAY

```
== trigger 1 at 5000 ==
   enemy wind-up start           -900  bite=5000
   contact (the bite)             +17  at=5000
   champion damage                +17  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=5000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=3
   damage number                  +17  slot=0 amount=3 hits=1 crit=False skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                       +133  at=133 early=True
   world effect end              +250  lived=250
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=6583 ran=1583
   next trigger                 +2000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=3
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=1,2 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=2
   damage number                  +17  slot=1 amount=2 hits=1 crit=False skill=True
   target flash                   +17  slot=1
   jaws shut (snap)               +50  at=50 clamp=1252,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=8583 ran=1583
   next trigger                 +2000  

== trigger 3 at 9000 ==
   enemy wind-up start           -883  bite=9000
   contact (the bite)             +17  at=9000
   champion damage                +17  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=9000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike               +17  creature=2 amount=1
   damage number                  +17  slot=2 amount=1 hits=1 crit=False skill=True
   target flash                   +17  slot=2
   jaws shut (snap)               +50  at=50 clamp=1388,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +1583  at=10583 ran=1583
   next trigger                 +2000  

== trigger 4 at 11000 ==
   enemy wind-up start           -900  bite=11000
   contact (the bite)              +0  at=11000
   champion damage                 +0  amount=2
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=11000 targets=2,3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.22
   reflected strike                +0  creature=2 amount=2
   damage number                   +0  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1391,810 belt=680,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +1583  at=12583 ran=1583
   next trigger                 +2000  

== trigger 5 at 13000 ==
   enemy wind-up start           -900  bite=13000
   contact (the bite)              +0  at=13000
   champion damage                 +0  amount=1
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=13000 targets=3 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.28
   reflected strike                +0  creature=3 amount=1
   damage number                   +0  slot=3 amount=1 hits=1 crit=False skill=True
   target flash                    +0  slot=3
   jaws shut (snap)               +50  at=50 clamp=1527,809 belt=633,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed               -12417  at=583 ran=1583
   next trigger                -12000  

== trigger 6 at 1000 ==
   enemy wind-up start           -900  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=4
   armed before it (window)      -417  ran=1583 ms; armed 417 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=0,1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=1
   damage number                   +0  slot=0 amount=1 hits=1 crit=False skill=True
   target flash                    +0  slot=0
   jaws shut (snap)               +50  at=50 clamp=1119,802 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## the killing answer

```
== trigger 1 at 1000 ==
   enemy wind-up start           -783  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=126
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike                +0  creature=1 amount=59
   damage number                   +0  slot=1 amount=59 hits=1 crit=False skill=True
   target flash                    +0  slot=1
   jaws shut (snap)               +50  at=50 clamp=1255,798 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                        +83  at=83 early=True
   world effect end              +200  lived=200
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                    +0  

== trigger 2 at 1000 ==
   enemy wind-up start           -900  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=168
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=1 amount=79
   damage number                   +0  slot=1 amount=79 hits=1 crit=False skill=True
   target flash                    +0  slot=1
   jaws shut (snap)               +50  at=50 clamp=1119,810 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                        +83  at=83 early=True
   world effect end              +200  lived=200
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## the bite that fells the champion

```
== trigger 1 at 1000 ==
   enemy wind-up start           -783  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=280
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=151
   damage number                   +0  slot=0 amount=151 hits=1 crit=False skill=True
   target flash                    +0  slot=0
   jaws shut (snap)               +50  at=50 clamp=1112,804 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                        +83  at=83 early=True
   world effect end              +200  lived=200
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                    +0  

== trigger 2 at 1000 ==
   enemy wind-up start           -900  bite=1000
   contact (the bite)              +0  at=1000
   champion damage                 +0  amount=349
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=1000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.03
   reflected strike                +0  creature=0 amount=189
   damage number                   +0  slot=0 amount=189 hits=1 crit=False skill=True
   target flash                    +0  slot=0
   jaws shut (snap)               +50  at=50 clamp=983,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                       -  
   release                        +83  at=83 early=True
   world effect end              +200  lived=200
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## REPAY beside JAWS

```
== trigger 1 at 4000 ==   (!) sfx_cast still played
   enemy wind-up start           -583  bite=4000
   contact (the bite)              +0  at=4000
   champion damage                 +0  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=4000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike                +0  creature=0 amount=4
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1119,806 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                +2830  at=6830 ran=2830
   next trigger                 +3000  

== trigger 2 at 7000 ==
   enemy wind-up start           -883  bite=7000
   contact (the bite)             +17  at=7000
   champion damage                +17  amount=4
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=7000 targets=0 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.09
   reflected strike               +17  creature=0 amount=4
   damage number                  +17  slot=0 amount=4 hits=1 crit=False skill=True
   target flash                   +17  slot=0
   jaws shut (snap)               +50  at=50 clamp=1116,807 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +2830  at=9830 ran=2830
   next trigger                 +3000  

== trigger 3 at 10000 ==
   enemy wind-up start           -883  bite=10000
   contact (the bite)             +17  at=10000
   champion damage                +17  amount=3
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                   +17  slot=3
   reaction spawn                 +17  slot=3 contact=10000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                 +17  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike               +17  creature=1 amount=4
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)               +50  at=50 clamp=1252,799 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                    +17  (the trigger)
   ReactionArmed                +2830  at=12830 ran=2830
   next trigger                 +3000  

== trigger 4 at 13000 ==
   enemy wind-up start           -900  bite=13000
   contact (the bite)              +0  at=13000
   champion damage                 +0  amount=3
   armed before it (window)      -170  ran=2830 ms; armed 170 ms before this bite
   JAWS trigger                    +0  slot=3
   reaction spawn                  +0  slot=3 contact=13000 targets=1 belt=0,0 cue=sfx_seeker_jaws_snap
   reaction sound                  +0  sfx_seeker_jaws_snap vol=0.46 pitch=0.00 pan=0.15
   reflected strike                +0  creature=1 amount=3
   damage number                   +0  slot=1 amount=3 hits=1 crit=False skill=True
   target flash                    +0  slot=1
   jaws shut (snap)               +50  at=50 clamp=1255,801 belt=647,700
   recoil start                   +50  (from the snap)
   recoil end                    +250  
   release                       +217  at=217 early=False
   world effect end              +333  lived=333
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```

## REPAY beside JAWS: every Skill event and what the screen did for it

```
#  bite ms  att  amt  JAWS since last       figure doing  answer
  1     4000    0    4    no                      attack/6  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_seeker_jaws_snap(0.46,0.00)@+0 reaction-spawn@+0 Strike:0:4@+0 reaction-snap@+50 reaction-release@+217 reaction-recoil-end@+250 reaction-end@+333 number:0=4@+517
  2     5000    0    4    no                      attack/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 Strike:0:145@+217 snd:sfx_hit(0.38,0.00)@+217 number:0=145@+217 flash:0@+217
  3     6000    0    4    no                        trap/0  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  4     7000    0    4    no                        trap/6  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 snd:sfx_seeker_jaws_snap(0.46,0.00)@+17 reaction-spawn@+17 Strike:0:4@+17 number:0=4@+17 flash:0@+17 reaction-snap@+50 reaction-release@+217 reaction-recoil-end@+250 reaction-end@+333
  5     8000    0    4    no                  hard_hands/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 callout:HAMMER@+83 snd:sfx_seeker_hard_hands_commit(0.34,0.00)@+83 snd:sfx_seeker_hard_hands_hit(0.55,0.00)@+217 Strike:0:463@+217 number:0=463@+217 flash:0@+217
  6     9000    0    4    no                        idle/2  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17
  7    10000    0    3    no                      attack/7  snd:sfx_hit(0.30,-0.25)@+17 fx:fx_hit/impact.bite/Champion@+17 snd:sfx_seeker_jaws_snap(0.46,0.00)@+17 reaction-spawn@+17 Strike:1:4@+17 reaction-snap@+50 reaction-release@+217 reaction-recoil-end@+250 reaction-end@+333 number:1=4@+500
  8    11000    0    3    no                      attack/2  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 Strike:1:140@+200 snd:sfx_hit(0.38,0.00)@+200 number:1=140@+200 flash:1@+200 fx:fx_weakhit/impact.weak/Creature/1@+200
  9    12000    0    3    no                        idle/1  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0
 10    13000    0    3    no                      attack/7  snd:sfx_hit(0.30,-0.25)@+0 fx:fx_hit/impact.bite/Champion@+0 snd:sfx_seeker_jaws_snap(0.46,0.00)@+0 reaction-spawn@+0 Strike:1:3@+0 number:1=3@+0 flash:1@+0 reaction-snap@+50 reaction-release@+217 reaction-recoil-end@+250 reaction-end@+333 TRAP-CLIP@+417
```
