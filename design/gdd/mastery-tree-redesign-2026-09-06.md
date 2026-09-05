# Mastery Tree — the path redesign (2026-09-06)

The tree is re-authored around **path identity**: every branch is a short trunk, a fork, and two
named routes that each tell one build story, rejoining at the branch's capstone. Every node has ONE
explicit parent (a bridge has one on each side; a capstone accepts either route's greater), so the
graph is a fixed, readable diagram. Purchasing a node changes only its visual state — never a line,
a position or a fork. The layout is a pure function of the catalogue (`MasteryLayout`), and
`mastery_topology_test` holds it to that.

## Node taxonomy

| Kind | Cost | Rule |
|---|---|---|
| MINOR | 1 | one stat, one idea |
| NOTABLE | 3 | one coherent build statement (at most two tightly related effects) |
| SKILL | 4 | unlocks one shared skill while allocated; rewards stand on the road to it |
| GREATER | 5 | the route's strongest statement, always a trade or a condition |
| SPECIALISATION | 6 | one per hunter; strengthens or bends a Style (a leaf off the route's last notable, beside its greater or second skill) |
| CAPSTONE | 8 | one per hunter; a rule change with a price; needs either route's greater |
| BRIDGE | 6 | between two neighbouring branches; needs one named notable on each side |

Costs: a route to its capstone is 27–30 points; the whole tree is 244 (RESONANCE 62, LOOT 62, TEMPO 48,
ENDURE 48, four bridges 24) against 66 points at six regions to depth 150 and 78 at depth 225
(27–32 % reachable; more than one branch, never two). No bare multipliers: every node moves a shape.

## RESONANCE — how strong every skill is (up)

| Id | Kind | Route | Effect | Parent | Why it is there |
|---|---|---|---|---|---|
| chime | MINOR | trunk 0 | +8 RESONANCE | start | the branch's own stat, plainly |
| tone | MINOR | trunk 1 | +8 RESONANCE | chime | the fork |
| steep | MINOR | ONE SOURCE 0 | +10 RESONANCE | tone | the road's first step |
| road_hammer | SKILL | ONE SOURCE 1 | BLOW | steep | the hammer is the purist's weapon |
| keyed | NOTABLE | ONE SOURCE 2 | strong matchup pays +25 % | road_hammer | committing to a Source |
| deep | NOTABLE | ONE SOURCE 3 | +12 RESONANCE, resonance worth +20 % | keyed | the stat compounds |
| road_hammer_2 | SKILL | ONE SOURCE 4 | PRESS | deep | the second hammer |
| pure | GREATER | ONE SOURCE 5 | one shared Source: all skills +35 % | road_hammer_2 | the route's thesis |
| spec_strike | SPEC | leaf of deep | STRIKE SPECIALIST (execute) | deep | the hammer's own discipline |
| clarity_res | MINOR | SWORN 0 | +10 RESONANCE | tone | the road's first step |
| road_snare | SKILL | SWORN 1 | JAWS | clarity_res | a promise kept by being hit |
| pledge | NOTABLE | SWORN 2 | vows pay +30 % | road_snare | the route's thesis begins |
| narrow | NOTABLE | SWORN 3 | own style +20 %, others −10 % | pledge | narrowing is the price |
| road_snare_2 | SKILL | SWORN 4 | REPAY | narrow | the second snare |
| zealot | GREATER | SWORN 5 | vows pay +60 %, damage taken +15 % | road_snare_2 | the thesis at full price |
| spec_trap | SPEC | leaf of narrow | TRAP SPECIALIST (re-arm on hit) | narrow | the snare's discipline |
| chord | CAPSTONE | rim | every matchup counts as strong, every skill 15 % softer | pure or zealot | identity trade |

## TEMPO — hitting first and often (right)

| Id | Kind | Route | Effect | Parent |
|---|---|---|---|---|
| bite | MINOR | trunk 0 | +4 ATTACK POWER | start |
| swift | MINOR | trunk 1 | +3 ENGINEERING | bite |
| sharp | MINOR | OPENING 0 | +1 % CRITICAL CHANCE | swift |
| road_sign | SKILL | OPENING 1 | CALL | sharp |
| surge | NOTABLE | OPENING 2 | +40 % for the first three seconds | road_sign |
| alpha | NOTABLE | OPENING 3 | first hit ×1.9, later hits ×0.8 | surge |
| opening_volley | GREATER | OPENING 4 | first cast +50 %, later casts −10 % | alpha |
| spec_mark | SPEC | leaf of alpha | SIGN SPECIALIST (marks last longer) | alpha |
| quick | MINOR | RHYTHM 0 | +3 ENGINEERING | swift |
| road_sign_2 | SKILL | RHYTHM 1 | BRAND | quick |
| brisk | NOTABLE | RHYTHM 2 | the basic swing comes 20 % sooner | road_sign_2 |
| rhythm | NOTABLE | RHYTHM 3 | each cast hastens the next 8 %, up to five; a bite resets | brisk |
| blitz | GREATER | RHYTHM 4 | cooldowns −30 %, hits −20 % | rhythm |
| first_strike | CAPSTONE | rim | +150 % for three seconds, −40 % after | opening_volley or blitz |

## ENDURE — outlasting the enemy (left)

| Id | Kind | Route | Effect | Parent |
|---|---|---|---|---|
| hide | MINOR | trunk 0 | +12 MAXIMUM HEALTH | start |
| guard | MINOR | trunk 1 | +3 DEFENCE | hide |
| bulk | MINOR | THICK SKIN 0 | +12 MAXIMUM HEALTH | guard |
| road_drain | SKILL | THICK SKIN 1 | DRINK | bulk |
| padding | NOTABLE | THICK SKIN 2 | every bite deals 5 less | road_drain |
| recovery | NOTABLE | THICK SKIN 3 | regain 15 % between waves | padding |
| bulwark | GREATER | THICK SKIN 4 | damage taken −30 %, dealt −15 % | recovery |
| spec_transformation | SPEC | leaf of recovery | MORPH SPECIALIST (leech doubled) | recovery |
| stand | MINOR | REPRISAL 0 | +3 DEFENCE | guard |
| road_drain_2 | SKILL | REPRISAL 1 | WILT | stand |
| thorns | NOTABLE | REPRISAL 2 | every biter takes 15 % of its bite back | road_drain_2 |
| payback | NOTABLE | REPRISAL 3 | every bite makes the next skill hit +15 %, up to four | thorns |
| rebound | GREATER | REPRISAL 4 | a fifth of every bite returns as health, dealt −15 % | payback |
| endless | CAPSTONE | rim | full health every wave; maximum health halved | bulwark or rebound |

## LOOT — what you carry out (down)

| Id | Kind | Route | Effect | Parent |
|---|---|---|---|---|
| glean | MINOR | trunk 0 | +6 GUILE | start |
| pockets | MINOR | trunk 1 | +8 GUILE | glean |
| scavenge | MINOR | CLEAN RUN 0 | +6 GUILE | pockets |
| road_volley | SKILL | CLEAN RUN 1 | SPRAY | scavenge |
| untouched | NOTABLE | CLEAN RUN 2 | +3 % haul per unbitten second, to +45 % | road_volley |
| spotless | NOTABLE | CLEAN RUN 3 | +25 % haul on a wave finished above 90 % health | untouched |
| road_volley_2 | SKILL | CLEAN RUN 4 | WEEP | spotless |
| second_look | GREATER | CLEAN RUN 5 | a quiet wave raises chest quality; hits −8 % | road_volley_2 |
| spec_projectile | SPEC | leaf of spotless | VOLLEY SPECIALIST (one more shot) | spotless |
| weigh | MINOR | DEEP VEIN 0 | +8 GUILE | pockets |
| road_field | SKILL | DEEP VEIN 1 | MIRE | weigh |
| bloodprice | NOTABLE | DEEP VEIN 2 | +35 % haul on a wave ended below half health | road_field |
| prospect | NOTABLE | DEEP VEIN 3 | +1 % haul for every wave past depth 20 | bloodprice |
| road_field_2 | SKILL | DEEP VEIN 4 | PULSE | prospect |
| gamble | GREATER | DEEP VEIN 5 | +70 % haul, every bite +25 % | road_field_2 |
| spec_aura | SPEC | leaf of prospect | AURA SPECIALIST (ticks faster) | prospect |
| prospector | CAPSTONE | rim | +3 % haul per wave past 30, skills −15 % | second_look or gamble |

## BRIDGES (one notable on each side — from the two routes that FACE the bridge, so its wires never cross a trunk)

| Id | Between | Needs | Effect |
|---|---|---|---|
| executioner | RESONANCE · TEMPO | pledge + surge | first hit on each creature +10 %, cuts 9 armour |
| volley | TEMPO · LOOT | brisk + untouched | +1 target, −20 % cooldown, hits −25 % |
| feedback | LOOT · ENDURE | bloodprice + padding | heal 1.2 % of maximum per creature struck |
| anchor | ENDURE · RESONANCE | thorns + keyed | +1 % hit size per 200 maximum health |

## Retired (2026-09-06)

hum, timbre, broad, discord, resonant, count, tally, cache, vein, lode, force, poise, interrupt,
assassinate, rush, knit, rooted, second_wind, bastion, absorb — duplicated stat bundles, or effects
that had no route to belong to. A save that carried one loses it on load and gets the points back;
nodes whose parent changed are refunded the same way (`MasteryTree.RestoreTaken` repairs).

## Layout

Polar, per branch: trunk step k at radius 400 + 260·k on the branch axis; the fork is the trunk's
last node; route steps at radius 900 + 325·k, the left route at −16° and the right at +16° from the
axis; a specialisation leaf one step further out than its notable and 10° further from the axis; the
capstone on the axis at step 7, past the longest route; a bridge at the midpoint angle at radius 1100.
The world rim is 3420. Node radii are a quarter larger than the ring tree's (`MasteryLayout.NodeWorldRadius`).
`MasteryLayout.Edges` is the drawn graph: one edge per (node, parent). Nothing in it reads the
player's allocation.

## Validation

`mastery_topology_test`: every node reachable from START; every node has one explicit parent per
side; positions and edges are identical whatever is taken; no two nodes overlap; every effect changes
a shape or a stat; no early node carries more than one stat; two capstones and two specialisations are
impossible; the whole tree costs more than four times the points at full content.
