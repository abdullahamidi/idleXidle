# SPRAY and HARD HANDS still do not move (tools/asset-pipeline/action_regression.py)

One SEEDED fight (RH_SHOT_SEED=7), fast TEMPO, 5 s, where JAWS overlaps HARD HANDS and SPRAY. The reaction layer is OFF
(`RH_REACTION_RECIPES=0`: JAWS presented the old way) or ON (the clamp). The fight and every SPRAY and HARD HANDS moment
(clip starts, releases, contacts, root motion, their sounds) are compared, keyed by playhead.

## The result

| Pair | Result |
|---|---|
| identity ON vs polish OFF (the clamp vs no reaction layer at all) | **IDENTICAL** (247 moments) |
| identity ON vs polish ON (the clamp vs the polished head) | **IDENTICAL** (248 moments) |
| identity ON vs identity OFF | 1 difference: a SPRAY contact 3 px apart at 9517, where the OFF film is the odd one out (below) |
| identity OFF vs polish OFF (both without the layer) | 1 difference: that same 3 px |

**The rig has noise, and it is not JAWS.** A performance plans from the DRAWN stage. The capture rig runs the game
on a fixed step, and when a real frame runs long, the game catches up with updates that draw nothing. An action
planned in such a gap reads a stale stage. Evidence:

- Two layer-OFF films of the same fight differ by 3 px at one SPRAY contact, and neither has the reaction layer.
- The first ON take drew nothing for 233 ms (wall 16517 → 16750, no `perf-draw` and no `shot` lines), exactly while
  HARD HANDS planned its leap at 3233. That one leap moved (23 moments; kept in `build/shots/jaws/identity_rerun/`).
- Re-filmed, the same leap is identical to both polish films. The HARD HANDS plan reading the drawn stage is the
  known "slow frames drop draw-time state" class, not a JAWS regression.

## The runs

### identity ON vs polish OFF

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  247  b=  248  moments both traced=  247  differing=0  root samples in one film only=1  other moments in one film only=0
IDENTICAL
```

### identity ON vs polish ON

```
fight    a=  133  b=  137  compared in order=  133  differing=0
actions  a=  248  b=  248  moments both traced=  248  differing=0  root samples in one film only=0  other moments in one film only=0
IDENTICAL
```

### identity ON vs identity OFF

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  247  b=  248  moments both traced=  247  differing=1  root samples in one film only=1  other moments in one film only=0
           ('9517', 'contact', 'seeker.spray'): a ['seeker.spray\tx=1493\ty=741\tknives=2']  b ['seeker.spray\tx=1496\ty=741\tknives=2']
1 DIFFERENCES
```

### identity OFF vs polish OFF

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  247  b=  247  moments both traced=  247  differing=1  root samples in one film only=0  other moments in one film only=0
           ('9517', 'contact', 'seeker.spray'): a ['seeker.spray\tx=1496\ty=741\tknives=2']  b ['seeker.spray\tx=1493\ty=741\tknives=2']
1 DIFFERENCES
```

### the FIRST identity ON take (the 233 ms draw gap) vs identity OFF

```
fight    a=  137  b=  140  compared in order=  137  differing=0
actions  a=  247  b=  236  moments both traced=  235  differing=24  root samples in one film only=12  other moments in one film only=1
           ('9517', 'contact', 'seeker.spray'): a ['seeker.spray\tx=1493\ty=741\tknives=2']  b ['seeker.spray\tx=1496\ty=741\tknives=2']
           ('3483', 'release', 'seeker.hard_hands'): a ['seeker.hard_hands\tx=750\ty=508\tknives=2\tlaunch=1192,739\treach=399']  b ['seeker.hard_hands\tx=769\ty=508\tknives=2\tlaunch=1169,739\treach=373']
           ('3483', 'sound', 'sfx_seeker_hard_hands_commit'): a ['sfx_seeker_hard_hands_commit\tvol=0.34\tpitch=0.00\tpan=-0.12']  b ['sfx_seeker_hard_hands_commit\tvol=0.34\tpitch=0.00\tpan=-0.11']
           ('3517', 'root', ''): a ['x=67\ty=0']  b ['x=62\ty=0']
           only in b: ('5200', 'handoff', 'attack')
25 DIFFERENCES
```

