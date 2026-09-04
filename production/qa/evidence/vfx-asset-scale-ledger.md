# VFX asset-scale ledger — the diagnostic the placement contract produces on its first run

Generated 2026-09-03 by the game itself, from the real textures:

```
RH_VFX_BUDGET=1 bash tools/asset-pipeline/vfx_dump.sh fight build/shots/vfx_ledger.png
```

## What the numbers mean

Every effect now carries a fraction of its SUBJECT's visible size rather than a display multiplier,
and the renderer derives the strip's destination frame from that fraction and from how much of the
frame the art actually fills. The RATIO is that frame height over the strip's authored 512, and it is
the brief's §73 / LAW 16 question computed rather than argued:

| ratio | verdict | what it means | what to do |
|---|---|---|---|
| over 1.25 | **Over** | the strip is being MAGNIFIED past the size it was drawn at. The renderer refuses — it clamps to 1.25 and the effect draws smaller than the design asked. | regenerate the asset. Do not raise the number. |
| 0.75 – 1.25 | Ok | drawn near its authored size | nothing |
| under 0.75 | Under | authored larger than it is ever drawn — waste, never ugly | schedule a re-export at 256 (§111) |

Subjects, measured from the shipped art at the 100 % density profile:
`subjects  champion 268x412  swarm 209x244  boss 497x521  row 627x244`

## OVER — 11 entries. Every one is an art order, not a number to turn.

```
shield.barrier  fx_shield  champion  visible 733x474  frame 963  native 512  ratio 1.880  Over
shield.gain  fx_shield  champion  visible 733x474  frame 963  native 512  ratio 1.880  Over
shield.absorb  fx_shield  champion  visible 733x474  frame 963  native 512  ratio 1.880  Over
shield.undying  fx_shield  champion  visible 733x474  frame 963  native 512  ratio 1.880  Over
cast.trap  fx_seeker_trap_strip8_512  row  visible 502x483  frame 713  native 512  ratio 1.393  Over
cast.trap  fx_oathbound_trap_strip8_512  row  visible 502x347  frame 649  native 512  ratio 1.267  Over
cast.trap  fx_magpie_trap_strip8_512  row  visible 502x289  frame 1976  native 512  ratio 3.858  Over
cast.transformation  fx_anvil_transformation_strip8_512  champion  visible 593x433  frame 710  native 512  ratio 1.387  Over
cast.strike  fx_magpie_strike_strip8_512  boss  visible 606x287  frame 699  native 512  ratio 1.365  Over
field.aura  fx_press  champion  visible 639x453  frame 737  native 512  ratio 1.439  Over
field.aura  fx_mark  champion  visible 469x453  frame 817  native 512  ratio 1.596  Over
```

**`fx_shield` is the headline.** Its dome fills 0.492 of its frame and sits 46 % down it, so a shield
that reaches the brief's 1.10–1.20× band would need a 963-px frame out of a 512-px strip — 1.88×,
which is `scale = 3.4` wearing a new coat. The renderer draws the honest 1.25× instead: a 488 × 315
dome, 0.76× the hunter, against the 0.32 × 412 = 132-px sprite in his torso that shipped before.
**Order**: 8 frames of 512, content at least 0.91 of the frame height, vertically centred (content
centre Y within 0.49–0.51), content aspect 0.85–0.95, authored to read at 16 % alpha additive.
`fx_shield_break` already fills 0.926 × 0.922 — it is the shape to match. At that fill every shield
moment lands at ratio 1.02 and the dome is 424 × 474 on all ten champions (`vfx_shield_test`).

**`fx_mark` and `fx_press` as HELD FIELDS.** A sigil is not a field: `fx_mark` fills 0.555 of its
frame, `fx_press` 0.615. BRAND and PRESS wear them because a Field skill's own FxKey chooses the
field's art. Either re-export those two at 0.85 fill, or alias BRAND's field to `fx_aura` (free, and
correct-shaped). `fx_wilt` needs neither — it resolves at 0.99.

**Three trap strips.** `fx_magpie_trap` is the extreme: its ring occupies 0.254 × 0.146 of a 512
frame, so covering a 627-px row would take a 1976-px frame — 3.86×. Re-export the per-character trap
family with the ring filling at least 0.90 of frame width, or retire them to the shared `fx_trap`
ring (which resolves at 1.02 and is fine). The other seven characters' traps came into budget when
the profile settled at 0.80 of the row's width.

**`fx_anvil_transformation` (0.609 fill) and `fx_magpie_strike` (0.410 fill)** are single strips out
of their families; re-export at 0.85 fill.

## UNDER — 58 entries, listed and never enforced

```
impact.weak  fx_weakhit  swarm  visible 82x78  frame 118  native 512  ratio 0.231  Under
impact.weak  fx_weakhit  boss  visible 175x167  frame 253  native 512  ratio 0.493  Under
impact.bite  fx_hit  champion  visible 158x148  frame 194  native 512  ratio 0.378  Under
death.boss_burst  fx_crit  swarm  visible 159x159  frame 162  native 512  ratio 0.317  Under
death.boss_burst  fx_crit  boss  visible 340x339  frame 347  native 512  ratio 0.677  Under
death.creature  fx_death  swarm  visible 181x171  frame 218  native 512  ratio 0.426  Under
cast.projectile  fx_projectile  champion  visible 243x115  frame 248  native 512  ratio 0.485  Under
cast.projectile  fx_anvil_projectile_strip8_512  champion  visible 265x115  frame 295  native 512  ratio 0.577  Under
cast.projectile  fx_chorus_projectile_strip8_512  champion  visible 169x115  frame 231  native 512  ratio 0.451  Under
cast.projectile  fx_metronome_projectile_strip8_512  champion  visible 187x115  frame 226  native 512  ratio 0.442  Under
cast.projectile  fx_unbroken_projectile_strip8_512  champion  visible 190x115  frame 313  native 512  ratio 0.610  Under
cast.projectile  fx_tower_projectile_strip8_512  champion  visible 108x115  frame 152  native 512  ratio 0.297  Under
cast.projectile  fx_quiver_projectile_strip8_512  champion  visible 169x115  frame 172  native 512  ratio 0.335  Under
cast.projectile  fx_thornwall_projectile_strip8_512  champion  visible 116x115  frame 135  native 512  ratio 0.263  Under
cast.projectile  fx_oathbound_projectile_strip8_512  champion  visible 92x115  frame 126  native 512  ratio 0.246  Under
cast.projectile  fx_magpie_projectile_strip8_512  champion  visible 294x115  frame 339  native 512  ratio 0.663  Under
cast.mark  fx_mark  swarm  visible 114x110  frame 198  native 512  ratio 0.387  Under
cast.mark  fx_seeker_mark_strip8_512  swarm  visible 113x110  frame 142  native 512  ratio 0.278  Under
cast.mark  fx_seeker_mark_strip8_512  boss  visible 242x234  frame 304  native 512  ratio 0.594  Under
cast.mark  fx_anvil_mark_strip8_512  swarm  visible 108x110  frame 134  native 512  ratio 0.263  Under
cast.mark  fx_anvil_mark_strip8_512  boss  visible 231x234  frame 287  native 512  ratio 0.561  Under
cast.mark  fx_chorus_mark_strip8_512  swarm  visible 113x110  frame 131  native 512  ratio 0.256  Under
cast.mark  fx_chorus_mark_strip8_512  boss  visible 240x234  frame 280  native 512  ratio 0.547  Under
cast.mark  fx_metronome_mark_strip8_512  swarm  visible 149x110  frame 149  native 512  ratio 0.290  Under
cast.mark  fx_metronome_mark_strip8_512  boss  visible 318x234  frame 318  native 512  ratio 0.620  Under
cast.mark  fx_unbroken_mark_strip8_512  swarm  visible 110x110  frame 115  native 512  ratio 0.225  Under
cast.mark  fx_unbroken_mark_strip8_512  boss  visible 234x234  frame 246  native 512  ratio 0.480  Under
cast.mark  fx_tower_mark_strip8_512  swarm  visible 112x110  frame 119  native 512  ratio 0.232  Under
cast.mark  fx_tower_mark_strip8_512  boss  visible 238x234  frame 253  native 512  ratio 0.495  Under
cast.mark  fx_quiver_mark_strip8_512  swarm  visible 112x110  frame 162  native 512  ratio 0.317  Under
cast.mark  fx_quiver_mark_strip8_512  boss  visible 240x234  frame 347  native 512  ratio 0.678  Under
cast.mark  fx_thornwall_mark_strip8_512  swarm  visible 110x110  frame 119  native 512  ratio 0.233  Under
cast.mark  fx_thornwall_mark_strip8_512  boss  visible 234x234  frame 254  native 512  ratio 0.497  Under
cast.mark  fx_oathbound_mark_strip8_512  swarm  visible 107x110  frame 133  native 512  ratio 0.260  Under
cast.mark  fx_oathbound_mark_strip8_512  boss  visible 229x234  frame 284  native 512  ratio 0.556  Under
cast.mark  fx_magpie_mark_strip8_512  swarm  visible 110x110  frame 135  native 512  ratio 0.264  Under
cast.mark  fx_magpie_mark_strip8_512  boss  visible 236x234  frame 289  native 512  ratio 0.564  Under
cast.strike  fx_strike  swarm  visible 127x134  frame 143  native 512  ratio 0.280  Under
cast.strike  fx_strike  boss  visible 271x287  frame 306  native 512  ratio 0.597  Under
cast.strike  fx_seeker_strike_strip8_512  swarm  visible 134x134  frame 134  native 512  ratio 0.262  Under
cast.strike  fx_seeker_strike_strip8_512  boss  visible 287x287  frame 287  native 512  ratio 0.560  Under
cast.strike  fx_anvil_strike_strip8_512  swarm  visible 130x134  frame 168  native 512  ratio 0.329  Under
cast.strike  fx_anvil_strike_strip8_512  boss  visible 278x287  frame 360  native 512  ratio 0.702  Under
cast.strike  fx_chorus_strike_strip8_512  swarm  visible 124x134  frame 167  native 512  ratio 0.327  Under
cast.strike  fx_chorus_strike_strip8_512  boss  visible 265x287  frame 357  native 512  ratio 0.697  Under
cast.strike  fx_metronome_strike_strip8_512  swarm  visible 126x134  frame 160  native 512  ratio 0.313  Under
cast.strike  fx_metronome_strike_strip8_512  boss  visible 269x287  frame 342  native 512  ratio 0.668  Under
cast.strike  fx_unbroken_strike_strip8_512  swarm  visible 133x134  frame 140  native 512  ratio 0.273  Under
cast.strike  fx_unbroken_strike_strip8_512  boss  visible 285x287  frame 299  native 512  ratio 0.584  Under
cast.strike  fx_tower_strike_strip8_512  swarm  visible 137x134  frame 179  native 512  ratio 0.349  Under
cast.strike  fx_tower_strike_strip8_512  boss  visible 293x287  frame 382  native 512  ratio 0.746  Under
cast.strike  fx_quiver_strike_strip8_512  swarm  visible 142x134  frame 151  native 512  ratio 0.294  Under
cast.strike  fx_quiver_strike_strip8_512  boss  visible 303x287  frame 322  native 512  ratio 0.628  Under
cast.strike  fx_thornwall_strike_strip8_512  swarm  visible 170x134  frame 180  native 512  ratio 0.352  Under
cast.strike  fx_oathbound_strike_strip8_512  swarm  visible 133x134  frame 163  native 512  ratio 0.318  Under
cast.strike  fx_oathbound_strike_strip8_512  boss  visible 284x287  frame 348  native 512  ratio 0.679  Under
cast.strike  fx_magpie_strike_strip8_512  swarm  visible 284x134  frame 327  native 512  ratio 0.639  Under
cast.rain  fx_weep  swarm  visible 62x220  frame 227  native 512  ratio 0.443  Under
```

One finding, not thirty: **the creature-side effect family is authored at 512 for effects that draw
at 80–300 px.** Downscaling is visually safe, so this never blocks a build — but it is real waste
against the 512 MB texture ceiling, and §111's actual-size rule says the family should be re-exported
at 256 in one batch when someone is in there anyway.

## OK — 33 entries

```
heal.column  fx_heal  champion  visible 352x391  frame 401  native 512  ratio 0.783  Ok
death.champion  fx_death  champion  visible 350x330  frame 421  native 512  ratio 0.822  Ok
death.creature  fx_death  boss  visible 387x365  frame 466  native 512  ratio 0.909  Ok
shield.break  fx_shield_break  champion  visible 496x494  frame 536  native 512  ratio 1.047  Ok
cast.projectile  fx_seeker_projectile_strip8_512  champion  visible 416x115  frame 428  native 512  ratio 0.836  Ok
cast.aura  fx_aura  champion  visible 245x433  frame 532  native 512  ratio 1.040  Ok
cast.trap  fx_trap  row  visible 502x431  frame 512  native 512  ratio 0.999  Ok
cast.trap  fx_anvil_trap_strip8_512  row  visible 502x500  frame 533  native 512  ratio 1.041  Ok
cast.trap  fx_chorus_trap_strip8_512  row  visible 502x331  frame 606  native 512  ratio 1.183  Ok
cast.trap  fx_metronome_trap_strip8_512  row  visible 502x127  frame 522  native 512  ratio 1.020  Ok
cast.trap  fx_unbroken_trap_strip8_512  row  visible 502x303  frame 592  native 512  ratio 1.156  Ok
cast.trap  fx_tower_trap_strip8_512  row  visible 502x371  frame 578  native 512  ratio 1.130  Ok
cast.trap  fx_quiver_trap_strip8_512  row  visible 502x421  frame 558  native 512  ratio 1.090  Ok
cast.trap  fx_thornwall_trap_strip8_512  row  visible 502x452  frame 563  native 512  ratio 1.100  Ok
cast.mark  fx_mark  boss  visible 243x234  frame 423  native 512  ratio 0.826  Ok
cast.transformation  fx_transformation  champion  visible 393x433  frame 443  native 512  ratio 0.865  Ok
cast.transformation  fx_seeker_transformation_strip8_512  champion  visible 362x433  frame 433  native 512  ratio 0.845  Ok
cast.transformation  fx_chorus_transformation_strip8_512  champion  visible 427x433  frame 444  native 512  ratio 0.867  Ok
cast.transformation  fx_metronome_transformation_strip8_512  champion  visible 437x433  frame 474  native 512  ratio 0.926  Ok
cast.transformation  fx_unbroken_transformation_strip8_512  champion  visible 433x433  frame 565  native 512  ratio 1.104  Ok
cast.transformation  fx_tower_transformation_strip8_512  champion  visible 312x433  frame 464  native 512  ratio 0.907  Ok
cast.transformation  fx_quiver_transformation_strip8_512  champion  visible 525x433  frame 565  native 512  ratio 1.104  Ok
cast.transformation  fx_thornwall_transformation_strip8_512  champion  visible 485x433  frame 532  native 512  ratio 1.040  Ok
cast.transformation  fx_oathbound_transformation_strip8_512  champion  visible 437x433  frame 498  native 512  ratio 0.972  Ok
cast.transformation  fx_magpie_transformation_strip8_512  champion  visible 284x433  frame 586  native 512  ratio 1.144  Ok
cast.strike  fx_thornwall_strike_strip8_512  boss  visible 363x287  frame 385  native 512  ratio 0.752  Ok
cast.rain  fx_weep  boss  visible 132x469  frame 484  native 512  ratio 0.945  Ok
field.aura  fx_aura  champion  visible 257x453  frame 558  native 512  ratio 1.089  Ok
field.aura  fx_aura  champion  visible 257x453  frame 558  native 512  ratio 1.089  Ok
field.aura  fx_wilt  champion  visible 445x453  frame 507  native 512  ratio 0.990  Ok
field.aura  fx_aura  champion  visible 257x453  frame 558  native 512  ratio 1.089  Ok
field.aura  fx_trap  champion  visible 528x453  frame 538  native 512  ratio 1.052  Ok
field.aura  fx_strike  champion  visible 429x453  frame 483  native 512  ratio 0.944  Ok
```

## How to re-run it

```
RH_VFX_BUDGET=1 bash tools/asset-pipeline/vfx_dump.sh fight build/shots/vfx_ledger.png
grep '^vfx-budget' build/shots/vfx_ledger.vfx.txt | grep Over
```

It reads the PNGs through the same cached alpha scan the draw path uses, so it cannot drift away from
what the player sees — which is why it is a runtime diagnostic and not a unit test carrying every
strip's measurements as hand-typed constants. `tools/asset-pipeline/fx_bounds.py` measures one strip
at a time when you are regenerating one.
