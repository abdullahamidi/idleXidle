# SHIELD — the acceptance case for the VFX placement contract (brief §71, §105, §106)

Captured 2026-09-04 from the running game. §106 names SHIELD as the case the whole contract is judged
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

`RH_SHOT_HUNTER=<id> RH_VFX_DUMP=1 bash tools/asset-pipeline/vfx_dump.sh vfxdebug …`

| hunter | drawn width | barrier frame | barrier CONTENT | ratio | verdict |
|---|---|---|---|---|---|
| THE SEEKER | 268 px | 300,372,640,640 | **376,509,488,315** | 1.880 | clamped to 1.25 |
| THE MAGPIE | 302 px | 300,372,640,640 | **376,509,488,315** | 1.880 | clamped to 1.25 |
| QUIVER | 373 px | 300,370,640,640 | **376,507,488,315** | 1.894 | clamped to 1.25 |
| THE OATHBOUND | 162 px | 300,370,640,640 | **376,507,488,315** | 1.894 | clamped to 1.25 |

**The four resolve to the same rectangle.** That is the contract working: one authored ratio, correct
on a 2.30× width spread, with no per-character number anywhere. The dome is 488 px wide against the
widest hunter's 373, so every silhouette is enclosed left to right with margin.

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

## What is still wrong, and why it is not fixed here

The drawn dome is 315 px tall on a 412 px hunter — **0.76×**, where §65 asks for 1.10–1.20×. It is not
a number that can be turned. `fx_shield`'s dome fills only 0.492 of its 512-px frame, so reaching the
band needs a 963-px frame out of a 512-px strip: 1.88×, which is `scale = 3.4` wearing a new coat and
is what LAW 16 forbids. The renderer clamps to the honest 1.25× and reports the shortfall as a number
instead of hiding it in a blur. Against the 0.32 × 412 = 132-px sprite that shipped in the hunter's
torso before, the drawn dome is 2.4× taller and 2.4× wider, and it is centred on him rather than on a
box he stands 40 px to the right of.

**The fix is the art.** Order, from `vfx-asset-scale-ledger.md`: 8 frames of 512 × 512, content at
least 0.91 of the frame height, vertically centred, aspect 0.85–0.95, authored to read at 16 % alpha
additive; `fx_shield_break` (0.926 × 0.922) is the shape to match. `vfx_shield_test` already asserts
that at that fill every shield moment lands at ratio 1.02 and the dome is 424 × 474 on all ten
champions — so the order is proved achievable before a credit is spent on it.

Two consequences of the un-regenerated asset are visible in the captures and are expected:

* the dome clears neither the crown (40 px short) nor the soles (57 px short) — it covers the torso and
  most of the limbs rather than enclosing the whole figure;
* `shield.break` (ratio 1.047, in budget, 494 px) therefore reads considerably LARGER than the barrier
  it replaces (315 px) instead of 20 px wider. Shrinking the break to match would be compensating for
  a bad asset by turning a good number — the inverse of LAW 16 — so it stays as authored.
