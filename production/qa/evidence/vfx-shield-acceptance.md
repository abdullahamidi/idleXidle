# SHIELD — the acceptance case for the VFX placement contract (brief §71, §105, §106)

Captured 2026-09-04 from the running game. **Result: PASSES on all four silhouettes.** §106 names SHIELD as the case the whole contract is judged
on, and §71 names THE SEEKER and THE MAGPIE as the two silhouettes it must hold for. The audit found
those two are a poor pair — `UiKit.AnimSprite` normalises every champion's drawn HEIGHT, so a
height-based barrier is identical on them by construction and they differ by only 13 % in width — so
this evidence adds the pair that can actually fail: THE OATHBOUND at 162 px drawn width against
QUIVER at 373, a 2.30× spread.

## The profile, in full

```
shield.barrier   asset fx_shield        subject Champion    anchor Center
RelativeScale 1.15   basis SubjectHeight (the hunter's VISIBLE height, 412 px)
OffsetX 0.00 widths  OffsetY -0.02 heights   (-8 px: more clearance over the crown than under the sole)
Layer OnSubject      Facing Fixed   Follow Pinned   Lifetime Held   Frames 8   Fps 8
```

`shield.gain`, `shield.absorb` and `shield.undying` carry the SAME anchor, scale and offset, so the
grant flares the dome that is standing there, the absorb shimmers across it and UNDYING flashes it —
which is the only way to guarantee §106's "absorb impact lands on barrier" on ten different bodies.
`shield.break` is the same geometry at 1.20 so the burst reads as that dome coming apart.

## What it resolves to, measured at runtime on four silhouettes

`RH_VFX_DUMP=1 RH_SHOT_SHIELDFX=hold RH_SHOT_HUNTER=<id> bash tools/asset-pipeline/vfx_dump.sh vfxdebug ...`

| hunter | drawn width | barrier frame | barrier CONTENT | native ratio | verdict |
|---|---|---|---|---|---|
| THE SEEKER | 268 px | 383,430,**474,474** | **383,430,474,474** | 0.925 | `ok` |
| THE MAGPIE | 302 px | 383,430,**474,474** | **383,430,474,474** | 0.925 | `ok` |
| QUIVER | 373 px | 381,426,**477,477** | **381,426,477,477** | 0.932 | `ok` |
| THE OATHBOUND | 162 px | 381,426,**477,477** | **381,426,477,477** | 0.932 | `ok` |

Two things changed against the earlier measurement and both are the point.

**Frame and content are now the same rectangle.** `fx_shield` fills 1.00 of every one of its eight
frames, measured at alpha thresholds 8, 32 and 64 -- the dome touches the frame edge in all of them.
The old asset filled 0.492, so its drawn dome was always less than half of what the profile asked for,
and the renderer honestly reported the shortfall rather than magnifying past LAW 16's ceiling.

**The ratio is a DOWNSCALE.** 474 / 512 = 0.925, comfortably inside budget, where before it needed
1.88x and was clamped to 1.25. Nothing is being stretched to reach the silhouette.

The drawn dome is 474 px against a visible hunter height of 412 -- **1.15x**, which is exactly the
authored `RelativeScale` and sits inside the 1.10-1.20x band sec.65 asks for. The number is no longer
being defended; it is simply met. And 474 px of width against the widest hunter's 373 leaves margin on
a 2.30x width spread, with no per-character number anywhere.

## The captures

| file | what it shows |
|---|---|
| `build/shots/vfx_shieldgain_seeker.png` | the barrier flaring around THE SEEKER |
| `build/shots/vfx_shieldgain_magpie.png` | the same rectangle around THE MAGPIE (§106's pair) |
| `build/shots/vfx_shieldgain_quiver.png` | the widest hunter, still enclosed |
| `build/shots/vfx_shieldgain_oathbound.png` | the narrowest hunter, still centred on him |
| `build/shots/vfx_debug_seeker.png` (+ magpie / quiver / oathbound) | the debug view: subject bounds, frame vs content, anchor, offset, ratio |
| `build/shots/vfx_fightshieldbroken_{100,125,150}.png` | the break, on the barrier's own centre |

Retake the whole set with `bash tools/asset-pipeline/vfx_shots.sh`.

`RH_SHOT_SHIELDFX=gain` now poses the barrier's flare as well as the bar, slowed so the shutter always
finds its first and brightest frame. Before that it posed only the bar, and whether the dome was
legible in a capture depended on where the replay happened to fire the grant — the same fixture caught
the flare on THE MAGPIE and missed it on THE SEEKER. A state no dial can pose has never been looked at.

## Verdict: PASSES

Section 106 names SHIELD as the case the whole placement contract is judged on, and sec.71 names THE
SEEKER and THE MAGPIE as the two silhouettes it must hold for. Both pass, and so do the two the audit
added because they can actually fail -- THE OATHBOUND at 162 px against QUIVER at 373.

`build/shots/acceptance_seeker.png` and `acceptance_magpie.png` are the two named silhouettes in the
debug view, each showing the champion's own bounds box inside the barrier's. On both, the ring of
light closes above the crown and below the soles, and on THE MAGPIE it clears the backpack that makes
her the widest of the pair. It is a barrier the hunter stands inside, not a sprite pasted on the
torso, which is the failure sec.106 was written to prevent.

### What it took

The profile was correct before the asset was. Re-authoring the numbers could never have fixed this:
reaching the band with a dome that filled 0.492 of its frame would have needed a 963-px draw out of a
512-px strip, which is `scale = 3.4` wearing a new coat and is what LAW 16 forbids. So the fix was the
art, generated to the order this document already carried -- 8 frames of 512 x 512, content filling
the frame height, vertically centred, authored to read at low alpha additive. `vfx_shield_test`
asserted the target before a credit was spent, and the shipped asset meets it.

### Still over budget, and not this effect

The debug overlay reports `2 EFFECTS OVER THE ASSET SCALE BUDGET` on every capture above. Neither is
the shield: they are `cast.trap` (ratio 1.37-3.80, depending on the champion's trap strip) and
`field.aura` (1.44). They are logged in `vfx-asset-scale-ledger.md` and are their own art orders. The
shield is listed there as the headline offender and is no longer one -- that ledger is stale in the
shield's favour and is corrected alongside this document.
