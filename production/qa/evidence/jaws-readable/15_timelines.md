# NEW readable bear trap: every trigger's life on the fight's playhead (tools/asset-pipeline/reaction_timeline.py --report)

Times are ms from the contact (the bite). The reaction's clock starts on the first frame that shows the bite
(+17): the OPEN trap there; `reaction-clamp` (the stop) 50 ms later, on the frame the jaws are drawn shut; the
reflected `number` and `flash` on that same frame; `reaction-retract` at 185 ms; gone at 285 ms.

## normal TEMPO, the bite at 7000

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
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +317  lived=317 slack=False
   rearm start                    +17  (the trigger)
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +250  lived=250 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +317  lived=317 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +317  lived=317 slack=False
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
   damage number                  +50  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                  +50  slot=3 amount=1 hits=1 crit=False skill=True
   target flash                   +50  slot=3
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +250  lived=250 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +317  lived=317 slack=False
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
   damage number                    -  -
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +317  lived=317 slack=False
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
   damage number                  +50  slot=2 amount=2 hits=1 crit=False skill=True
   target flash                     -  -
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                  +50  slot=3 amount=1 hits=1 crit=False skill=True
   target flash                   +50  slot=3
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                  +50  slot=0 amount=1 hits=1 crit=False skill=True
   target flash                   +50  slot=0
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +300  lived=300 slack=False
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
   damage number                  +50  slot=1 amount=59 hits=1 crit=False skill=True
   target flash                   +50  slot=1
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +200  lived=200 slack=False
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
   damage number                  +50  slot=1 amount=79 hits=1 crit=False skill=True
   target flash                   +50  slot=1
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +200  lived=200 slack=False
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
   damage number                  +50  slot=0 amount=151 hits=1 crit=False skill=True
   target flash                   +50  slot=0
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +183  lived=183 slack=True
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
   damage number                  +50  slot=0 amount=189 hits=1 crit=False skill=True
   target flash                   +50  slot=0
   jaws shut (snap)                 -  -
   recoil start                     -  -
   recoil end                       -  
   release                          -  -
   world effect end              +183  lived=183 slack=True
   rearm start                     +0  (the trigger)
   ReactionArmed                    -  (after the log)
   next trigger                     -
```
