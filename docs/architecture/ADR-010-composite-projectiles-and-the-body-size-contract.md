# ADR-010 — A projectile is composed at runtime, and its body has one size

| Field | Value |
|---|---|
| **Status** | Proposed. The Seeker pilot (`fx_seeker_projectile`) is built and filmed. It becomes Accepted when the owner approves the pilot. No other projectile uses it until then. |
| **Date** | 2026-09-23 |
| **Deciders** | user (asked for a layered projectile with a runtime trail, and ruled out PixelLab-generated motion) + technical-artist, lead-programmer |
| **Related** | ADR-009 (the premultiplied additive blend every layer draws through), `design/art/arena-art-contract.md` §3.7, `tools/asset-pipeline/v2/spec.json` `effects.archetypes.PROJECTILE` |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/vfx_projectile_composite_test.cs` |
| **Scope** | travelling effects (`VfxTravel.ToTarget`) whose strip key is listed in `ProjectileLooks`. Today that is one key, `fx_seeker_projectile_strip8_512`. |

## Summary

A listed projectile is no longer drawn as its strip. `VfxPlayer` still spawns it, places it and runs its
clock exactly as before. A `ProjectileVisual` draws it instead, from the layers below, dimmest first:

1. a long soft **wake**;
2. a short, hot **core** trail;
3. at most three **sparks**;
4. the **head**;
5. one **glint** that crosses the head.

When the head reaches the target, the composite throws a **directional impact**. Both trails are drawn
from the positions the projectile actually occupied. The head's size is a share of the caster's height,
measured on its own texture, and nothing else can change it.

## Context

Two PixelLab pilots tried to solve projectile motion inside an eight-frame strip, and both failed.

- **The cohort knife.** It came back as one static picture. Flown across the arena, it read as a PNG being
  translated.
- **The pinned pilot (7ea56119).** It tumbled through about 110°, which read as an inventory icon spinning.
  It also shrank the knife to about 40 % of its old size.

The shrinking is structural. `VfxResolver` sizes a strip so that the box around **all** its frames fits
the profile (0.28 × the hunter's height, on the content box's height). A level knife's box is only as
tall as the blade, but a tumbling knife's box is as tall as the knife is long. So every readable rotation
was paid for in body size.

The rest of the motion a thrown thing shows is its trail, and no strip can draw that honestly. The
trail depends on where the projectile actually was, and only the runtime knows that.

## Decision

1. **The renderer owns the flight; the composite owns the picture.**
   - `VfxPlayer.Play` attaches a `ProjectileVisual` when the profile travels and `ProjectileLooks.For(key)`
     has a look.
   - `Resolve` places it once, with the same `From`/`To` every travelling effect gets.
   - `Update` flies it on the anim's own `Life` through `ProjectileMotion.Ease`, which is the same
     1 − (1 − t)² ease-out as `Anim.Drift`.
   - The world trajectory and the travel timing are unchanged. A test restates the curve and checks
     every frame against it.

2. **THE BODY-SIZE CONTRACT.**
   - Head length = `ProjectileLook.HeadLength` × the caster's visible height.
   - The head's content box is measured once, on its own one-frame texture (`UiKit.Content`, alpha > 8).
   - Neither the strip's union box, nor the trail, nor the wobble enters the size. The wobble rotates the
     head but never scales it.
   - The Seeker's knife is 0.68 × the hunter, which is 280 px at the fixture's 412 px hunter. The old
     strip's blade read at about 270 px.

3. **Contact, not clock-end.**
   - The head gives way to the impact when its centre is `ContactReach` × its length from the target's
     centre (0.2 for the knife: the blade is well into the body).
   - The ease-out means that point comes at about 0.67 of the flight, which is where the old strip began
     its tail fade.
   - The anim stays in `_active` until its clock ends, so `AnyPlaying` and the fight's pacing do not
     change.
   - After the anim ends, its residue moves to `_landed`, which pacing never reads.

4. **The runtime trail.**
   - `TrailHistory` is a fixed ring of 32 samples. Each update it records the head's rear edge
     (0.46 × length behind the centre) with the flight's time.
   - Both trails are drawn as segments of one soft streak texture (`fxp_trail_soft`: a Gaussian across,
     uniform along) laid between consecutive samples.
   - **Core:** lives 0.09 s. Width 0.32 × the head's thickness, tapering with age. Opacity 1.0, pulled
     50 % toward white.
   - **Wake:** lives 0.30 s. Width 0.9 × thickness × √(age left). Opacity 0.9, falling with exponent
     0.85. It carries a small lateral wave that grows with age.
   - A trail's length on screen is therefore the distance the projectile covered in that time, so it is
     long when the knife is fast and short as it slows.
   - Once the head has landed, the trail's front is exposed. It tapers over 0.35 × the head's length and
     the whole trail dims away in 0.12 s, so the trail never stands in the arena as a block.

5. **Secondary motion.**
   - **Wobble:** ±6° at 2.2 Hz around the true direction; never a spin.
   - **Glint:** crosses the blade once, from 0.30 to 0.46 of the flight, rear to tip, with a sine
     envelope.
   - **Sparks:** at most `Sparks` (3), shed from the rear quarter and falling back.
   - **Pre-impact:** over the last `PreImpactSeconds` (0.08 s) the head gains a little light and the core
     tightens and whitens. The head never grows.
   - **Afterimages:** supported, but 0 for the knife. When evaluated at two, the ghosts smeared a long
     blade's silhouette.

6. **Directional impact.**
   - A 0.09 s contact flash.
   - 8 slim shards (thickness 0.28 × length). 75 % of them go forward inside a ±32° cone around the
     incoming direction, and the rest go radially at 0.45 of the reach.
   - The shards are drag-damped (they coast about 0.55 × the head's length) and gone within 0.26 s.

7. **One batch, one blend.** Every layer draws in the VFX pass's existing `VfxBlend.PremultipliedAdditive`
   batch, and every colour goes through `VfxBlend.Light` (ADR-009). There is no new blend state and no
   per-entity `Effect`.

8. **The colour is the Source.** The Source tint colours every layer, as it colours every strip (§3.1 and
   §3.3). The Seeker's red/pink is Body's glow; cast in Mind, the same knife is cyan.

9. **Looks are data.**
   - `ProjectileLook` is a record of shares (of the caster's height, of the head's length, of the
     flight) and lifetimes.
   - An orb, a shard or a poison dart is another instance. A motion a look needs but the record lacks
     (a pulse, orbiting particles, a small tumble for a shard) is added as an optional field whose
     default is off.
   - Nothing in the generic path rotates the head beyond the wobble.

10. **Parts, not strips.**
    - Part textures are white single images in `assets/art/VFX/parts/`, built by
      `tools/asset-pipeline/v2/fxparts.py`.
    - Drawn art (the knife head, the glint) comes from PixelLab through the approved post-pass
      (whiten → glow → soften ≈ 1 source px → feather).
    - Pure gradients (the streak, the spark, the flash, the shard) are written procedurally.
    - The strip key stays: it is what `HuntScreen.FxFor` resolves and `VfxResolver` places. It also
      stays as the fallback, because if a part is missing, `PlaceComposite` returns false and the strip
      is drawn.

## Consequences

- **The strip under a composite is not drawn.** `fx_seeker_projectile` keeps the pre-cohort original as
  its fallback. The unapproved tumbling strip (7ea56119) is no longer installed.
- **Cost per live projectile (measured):**
  - ≤ 32 sprites a frame (mean ≈ 18);
  - ≤ 32 retained trail samples;
  - 3 spark and 8 shard slots;
  - **0 bytes allocated per frame** (`GC.GetAllocatedBytesForCurrentThread` over 441 fight frames, and a
    unit test);
  - under 2 KB at launch (a unit test);
  - at most about 5 texture runs, so up to 5 draw calls against the strip's 1. Over the same fight
    window the VFX pass went from 18.4 to 19.7 draw calls on average, and from 23 to 24 at peak.
- **Scaling.** About 10–20 simultaneous projectiles fit the planning budget of "low hundreds of draw
  calls". Beyond that, draw all composites layer by layer (all wakes, then all heads) so the texture runs
  collapse to about 5 for the whole pass. Nothing does that yet.
- `RH_VFX_COMPOSITE=0` draws strips again. It is a review switch only, so old and new can be filmed
  through the same fight.
- `RH_VFX_METRICS=1` prints the live cost each frame.

## Known and unchanged (reported, not decided here)

- **The ease-out decelerates the head toward the target.** At contact the knife moves at about a third of
  its launch speed. Changing that changes the travel timing, which this decision was told not to touch.
- **The sim's hit reaction fires at cast time.** The creature's hit flash and weak-hit puff play
  ≈ 70–130 ms after the cast, which is about 0.55 s before the knife arrives. The composite does not
  move them.

## Alternatives rejected

- **An eight-frame projectile animation from PixelLab.** Tried twice (see Context), and ruled out by the
  owner.
- **Baking the trail into the strip.** A baked trail cannot follow the real path, and its pixels would
  enter the union box and shrink the body.
- **Sizing every strip by length instead of by the union box.** That would move every travelling strip's
  size at once, including unapproved ones. The composite needs its own head measure, and gets it.
- **A general particle engine.** One projectile needs 11 particle slots. Fixed arrays per flight are
  enough, and they allocate nothing.
