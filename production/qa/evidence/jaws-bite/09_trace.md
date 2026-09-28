# The bite and the answer on the playhead (bite_timeline.py), the bite at 7000

# the bite at 7017 (Core at=7000)
| t (ms) | what |
|---|---|
| -1000 | **CONTACT: EnemyStrike** amount=4 crit=False |
| -984 | leader root x=-60 (slot 0) |
| -967 | leader root x=-59 (slot 0) |
| -950 | leader root x=-47 (slot 0) |
| -934 | leader root x=-37 (slot 0) |
| -917 | leader root x=-28 (slot 0) |
| -900 | enemy ANTICIPATION starts (bite=7000) |
| -900 | leader root x=-21 (slot 0) |
| -884 | leader root x=-15 (slot 0) |
| -867 | leader root x=-10 (slot 0) |
| -850 | leader root x=-7 (slot 0) |
| -834 | leader root x=-4 (slot 0) |
| -817 | leader root x=-2 (slot 0) |
| -800 | leader root x=-1 (slot 0) |
| -784 | enemy HOME |
| -734 | leader root x=2 (slot 0) |
| -717 | enemy follow-through ends |
| -700 | leader root x=3 (slot 0) |
| -650 | leader root x=4 (slot 0) |
| -600 | leader root x=5 (slot 0) |
| -567 | leader root x=6 (slot 0) |
| -517 | leader root x=7 (slot 0) |
| -484 | leader root x=8 (slot 0) |
| -434 | leader root x=9 (slot 0) |
| -367 | leader root x=10 (slot 0) |
| -300 | champion's Strike on slot 0 amount=150 crit=False |
| -300 | flash on slot 0: peak 0.45, 80 ms, rise 0.15 |
| -284 | leader root x=11 (slot 0) |
| -234 | enemy COMMIT starts |
| -234 | leader root x=10 (slot 0) |
| -217 | leader root x=7 (slot 0) |
| -200 | leader root x=2 (slot 0) |
| -184 | leader root x=-2 (slot 0) |
| -167 | leader root x=-7 (slot 0) |
| -150 | leader root x=-11 (slot 0) |
| -134 | leader root x=-16 (slot 0) |
| -117 | leader root x=-21 (slot 0) |
| -100 | leader root x=-27 (slot 0) |
| -84 | leader root x=-32 (slot 0) |
| -67 | leader root x=-37 (slot 0) |
| -50 | leader root x=-43 (slot 0) |
| -34 | leader root x=-48 (slot 0) |
| -17 | leader root x=-54 (slot 0) |
| +0 | **CONTACT: EnemyStrike** amount=4 crit=False |
| +0 | champion's Strike on slot 0 amount=7 crit=True |
| +16 | leader root x=-60 (slot 0) |
| +33 | leader root x=-59 (slot 0) |
| +50 | leader root x=-47 (slot 0) |
| +66 | leader root x=-37 (slot 0) |
| +83 | leader root x=-28 (slot 0) |
| +100 | enemy ANTICIPATION starts (bite=8000) |
| +100 | leader root x=-21 (slot 0) |
| +116 | leader root x=-15 (slot 0) |
| +133 | leader root x=-10 (slot 0) |
| +150 | leader root x=-7 (slot 0) |
| +166 | leader root x=-4 (slot 0) |
| +183 | leader root x=-2 (slot 0) |
| +200 | leader root x=-1 (slot 0) |
| +216 | enemy HOME |
| +266 | leader root x=2 (slot 0) |
| +283 | enemy follow-through ends |
| +300 | leader root x=3 (slot 0) |
| +350 | leader root x=4 (slot 0) |
| +400 | leader root x=5 (slot 0) |

## The reaction layer (RH_PRESENT_TRACE): `at=` is ms after the bite's ms; the first frame that shows the bite is +17

```
present	4400	7017	reaction-spawn	seeker.jaws	slot=3	contact=7000	targets=0	belt=0,0
present	4717	7333	reaction-cue	seeker.jaws	contact=7000	at=333	cue=sfx_seeker_jaws_fangs
present	4717	7333	reaction-clamp	seeker.jaws	contact=7000	at=333	clamp=1195,792	belt=647,700	body=1115,636,268,245
present	4767	7383	reaction-number	seeker.jaws	contact=7000	at=383
present	5083	7700	reaction-end	seeker.jaws	contact=7000	lived=700
```

reaction-draw lines: 82; distinct cost fields: alloc=0; sprites per frame (both passes: the rows, their glow and the impact's parts): sprites=1, sprites=10, sprites=2, sprites=3, sprites=4, sprites=5, sprites=6, sprites=8
