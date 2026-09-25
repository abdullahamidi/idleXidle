# SPRAY and HARD HANDS still do not move (tools/asset-pipeline/action_regression.py)

One SEEDED fight (RH_SHOT_SEED=7), fast TEMPO, 5 s, where JAWS overlaps HARD HANDS and SPRAY. The new trap with the
reaction layer ON against the layer OFF (`RH_REACTION_RECIPES=0`) and against the identity pass's films.

## NEW ON vs NEW OFF

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  247  b=  247  moments both traced=  247  differing=0  root samples in one film only=0  other moments in one film only=0
IDENTICAL
```

## NEW ON vs identity OFF

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  247  b=  247  moments both traced=  247  differing=1  root samples in one film only=0  other moments in one film only=0
           ('9517', 'contact', 'seeker.spray'): a ['seeker.spray\tx=1493\ty=741\tknives=2']  b ['seeker.spray\tx=1496\ty=741\tknives=2']
1 DIFFERENCES
```

## NEW ON vs identity ON

```
fight    a=  137  b=  137  compared in order=  137  differing=0
actions  a=  248  b=  247  moments both traced=  247  differing=0  root samples in one film only=1  other moments in one film only=0
IDENTICAL
```
