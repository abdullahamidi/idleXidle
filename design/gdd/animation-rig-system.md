# Animation & Rig System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Ambiguities resolved using `design/gdd/creature-data-schema.md` and `design/art/art-bible.md` §§5.3, 5.5, 5.6, 8.4, 8.5 as authority; every resolution is flagged inline as an assumption. |
| **Priority / Tier** | MVP — Core layer (`design/gdd/systems-index.md` #4). **Highest-risk system in the project** (systems-index.md's High-Risk Systems table). |
| **Depends On** | `design/gdd/creature-data-schema.md` |
| **Depended On By** | `design/gdd/input-targeting-system.md` (already written — depends on this system for per-part transforms, see §3.18), `combat-encounter-system`, `creature-ai-telegraph-system`, `region-mastery-automation-system` (see §6) |

> **PROTOTYPE SPIKE REQUIRED — read before building against this spec.** This document specifies
> a genuinely novel piece of engineering (MonoGame ships no animation/rigging system at all — this
> is a homebrew solution) and is flagged as the single highest-risk system in the project
> (`systems-index.md`'s High-Risk Systems table; art-bible §8.5's own Open Items list). Before the
> full ~30-creature roster is committed to this rig: **(1)** prototype the Attacker's
> strike-and-recover cycle first — it is explicitly the widest motion arc in the game (art-bible
> §5.3) and the first place angle-snapped rotation risk (§3.10 below) will be visible; **(2)** file
> a formal Architecture Decision Record at `/create-architecture` covering the rig implementation,
> the angle-snap mitigation, and the `MonoGame.Extended.Tweening` integration. Nothing in this
> document should be treated as validated until that spike passes its own acceptance check (§8,
> criterion 4).

## Assumptions Log (resolved this session, no placeholders left in the spec below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | A **Clip** is a timed sequence of references into a shared, per-creature **Pose Library** (§3.4), not an independently hand-authored frame sequence. `Clip.frame_count` describes how many discrete *playback steps* the clip is sampled at, not how many additional hand-authored poses exist. | Reconciles art-bible §8.4's "~6 key poses hand-authored once per creature, reused everywhere" with the same section's per-role frame-count/fps table (Crafter 8 frames @ 12fps, etc.) — the per-role numbers describe playback cadence and sampling granularity, not a second, larger pose budget. This is what keeps the ~25–60 frame per-creature budget (art-bible §8.4) mathematically true under this rig's data model. |
| A2 | Angle-variant count and angle range are authored **per rotating `Part`**, not as one fleet-wide constant. | Art-bible §8.5 mandates "a small set of fixed angle variants" without naming a number, and a creature's parts genuinely differ in motion-arc width (an Attacker's striking limb sweeps far wider than a Support's tethered satellite's gentle bob) — a single global constant would either under-serve wide-arc parts or waste art budget on subtle ones. |
| A3 | Angle-snap ties (a continuous angle exactly equidistant between two authored variants) resolve **round-half-up**, toward the higher-index (larger-angle) variant. | Reuses the exact rounding convention `creature-data-schema.md` §4 already established project-wide, for consistency across every derived-value formula in the project rather than inventing a second convention. |
| A4 | Rig playback state (current clip id, current frame, hold flag, elapsed time) is **not** part of the persisted `CreatureInstance` schema and is re-derived at load/spawn time purely from `bind_state`, `bound_state.work_state`, and `hostile_state.part_states` (`creature-data-schema.md` §§3.5–3.7). | Avoids a second source of truth for state creature-data-schema already owns. `save-load-persistence.md`'s schema-versioning scope has no rig-state fields, and none are needed — see §6. |
| A5 | A crack-decal asset for a `Part` whose `rotation_mode = angle_snapped` (§3.10) must itself be authored per angle-variant, indexed identically to that part's own variant set — not as one rotation-agnostic decal. | The task brief's decal requirement doesn't address rotating parts. Without this, a decal on a rotating part would either fail to rotate with it (visibly detaching from the crack location) or reintroduce the exact continuous-rotation blur problem §3.10 exists to solve. Flagged here as a genuine production-cost consequence, not left implicit. |
| A6 | Cross-creature draw ordering in a busy Region View (which creature's silhouette occludes which) is resolved by sorting creatures by ground-anchor Y before submission. | No source document specifies this, but a concrete, buildable batching contract (§3.16) needs a deterministic order. Sorting by a value every creature already has (a ground-anchor position, per art-bible §8.6) adds no new data. |
| A7 | The Curve Library referenced by `easing_curve_id` (§3.12) is assumed to be a small, curated set of named curves sourced from `MonoGame.Extended.Tweening`'s built-in easing functions (already an approved dependency, art-bible §8.7) — not hand-rolled custom curve math. | Keeps the rig "a scoped systems task," per art-bible §8.5's own framing, rather than growing a second animation-curve authoring surface. Exact API names are a `lead-programmer`/ADR-level implementation detail, not fixed here. |
| A8 | Part-break decal art is packed into its **own small, shared, permanently-resident decal atlas** (mirroring the glyph atlas), not baked into each region's per-creature atlas. | Art-bible doesn't specify this, but it follows directly from principles it does lock: §8.5's "one small shared glyph atlas" pattern, and §8.4's "never a baked full-body variant" mandate extended in spirit — a handful of generic, reusable crack shapes composited at each part's transform is cheaper and more consistent with the rest of this bible's atlas discipline than a bespoke decal bake per creature. It also keeps the draw-submission contract (§3.16) cleanly batched — see that section for why interleaving decals into the body atlas would be worse, not better, for batching. |

---

## 1. Overview

The animation & rig system is Resonance Hunter's answer to a hard constraint: MonoGame ships no
animation or rigging system whatsoever. This document specifies a **homebrew cutout rig** — a
parent-child bone hierarchy built on vanilla `SpriteBatch`, with `MonoGame.Extended.Tweening`
driving curve evaluation — that renders every creature's full performance surface (hostile
threat-display, bound work-loops in Healthy/Blocked/Starved variants, telegraphs, hit-reactions,
part-break, the Mastery Transition) from a small, shared, per-creature library of ~6 hand-authored
key poses, at a total budget of roughly 25–60 unique frames per creature (art-bible §8.4). Because
skeletal animation is a continuous-transform technique and rotating hard-edge pixel art to
non-cardinal angles produces blur or stair-stepping, the rig's single most load-bearing rule is its
mandatory mitigation: **skeletal transforms drive position and timing; rotation is resolved by
discrete, angle-snapped part-sprite swaps**, never continuous `SpriteBatch` rotation, for any part
whose motion is wide enough to be visually at risk. This document specifies that mitigation, the
full data model, the draw-submission/batching contract, and every formula a programmer needs to
build the rig — not the creature-specific animation content itself (which poses, which curves, per
creature/role — that is authoring, produced against this spec, not part of it).

## 2. Player Fantasy

This is an invisible, structural system — the player never sees a "rig" — so its player fantasy is
what it makes legible without a word of explanation:

> **The same creature that terrified you a moment ago is now visibly, contentedly working for you
> — and you never had to be told. You just saw it.**

Concretely, this system is what guarantees:

- A creature's hostile threat-display and its bound work-loop are built from the *same* skeleton
  and the *same* six authored poses, yet a player can tell which is which from a single paused
  frame, pose alone, no color or glyph required (art-bible §5.5's design test) — because this rig
  makes retiming with a different easing curve, not redrawing a different animation, the mechanism
  that carries that entire emotional flip.
- Watching a mixed roster of bound creatures in the Idle Region View, a player reads "this one is
  working, this one is stalled, this one is starving" purely from motion and pulse cadence (art-bible
  §5.3) — automation's health is drawn on the world, never buried in a menu (Pillar 3).
- A telegraphed attack's wind-up is the same pose the creature already knows how to hold — reading
  it is a recognition skill the player builds once and reuses forever (Pillar 1) — not a new asset
  the player has to learn from scratch every time a designer tunes an attack's timing, because this
  rig makes timing a pure data value (§3.11), decoupled from art.
- A body part the player broke stays visibly cracked on that exact creature, in that exact
  position, through every subsequent pose it strikes (§3.14) — persistence the Monster Hunter
  reference (art-bible §9.2) promises, delivered without a 12× texture-memory tax (art-bible §8.4).
- The Mastery Transition's single loudest beat — the vulnerability glyph healing into a Ward seal —
  reads as *this creature*, frozen in its final combat pose, quietly transforming, not a cutscene
  swapped in from somewhere else (§3.15).

## 3. Detailed Rules

### 3.0 Field Naming Note

Field names below are logical identifiers for design purposes. When implemented as C# data
classes, field names should follow the project's C# naming convention (PascalCase properties,
`.claude/docs/technical-preferences.md`) rather than the snake_case used here for readability.

### 3.1 CreatureRig (Top-Level Container)

One `CreatureRig` exists per `CreatureTemplate.template_id` (`creature-data-schema.md` §3.2) — the
rig is authored content, keyed identically to how art assets are already keyed (that schema's §8.1
naming convention). A `CreatureInstance` (schema §3.5) does not own a rig; it references one
indirectly via its current `template_id`, and a new `CreatureRig` is resolved automatically whenever
that `template_id` changes (evolution — Edge Case 10).

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `template_id` | string | Must match an existing `CreatureTemplate.template_id` | 1:1 link to the creature this rig defines. |
| `bones` | list of `Bone` (§3.2) | ≥ 1, exactly one root | The skeleton. |
| `parts` | list of `Part` (§3.3) | Length equals the template's `parts.length` (schema invariant 2) for non-decorative parts, plus 0+ decorative parts | The renderable surface. |
| `pose_library` | map of `pose_id` → `PoseFrame` (§3.4) | ~6 canonical entries typical (art-bible §8.4), more permitted for role-specific work-loop poses | The hand-authored pose set. |
| `clips` | map of `clip_id` → `Clip` (§3.5) | ≥ 1 | Every playable animation state for this creature. |
| `additive_layers` | map of `layer_id` → `AdditiveLayer` (§3.6) | 0+ | Secondary-motion layers (glyph pulse, twitch, breathing). |
| `part_decal_bindings` | map of `part_id` → map of `crack_stage` → `DecalAsset` (§3.7) | Only for non-decorative parts | Crack-overlay lookup table. |
| `glyph_overlay` | `GlyphOverlaySpec` (§3.8) | Exactly one per rig | Anchor and texture references for the vulnerability-ring/Ward-seal overlay. |

### 3.2 Bone

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `bone_id` | string | Unique within the rig | Stable identifier for a hierarchy node. |
| `parent_bone_id` | string, nullable | Must reference another `bone_id` in the same rig, or `null` | `null` only for the rig's single root bone. |
| `rest_local_position` | Vector2 (px) | Unbounded, authored | Pivot-relative offset from the parent bone's origin at the "rest" pose. |
| `rest_local_rotation_deg` | float (deg) | Normalized to (-180, 180] | Authored rest-pose rotation, parent-relative. |
| `rest_local_scale` | Vector2 (unitless) | > 0, default (1,1) | Authored rest-pose scale, parent-relative. |

### 3.3 Part

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `part_id` | string, nullable | Matches a `PartDefinition.part_id` on the rig's template (`creature-data-schema.md` §3.3), or `null` | Links this visual piece to a targetable/breakable creature part. `null` = decorative-only (e.g. a non-targetable frill) — never breakable, never decal-eligible. |
| `bone_id` | string | Must reference a `Bone.bone_id` in the same rig | Which bone this part is rigidly attached to. |
| `texture_region` | rect (atlas coords) | Within the creature's region atlas | Source rect for the base (unrotated / rest-angle) sprite. |
| `pivot` | Vector2 (px, local to `texture_region`) | Within `texture_region` bounds | `SpriteBatch` `origin` — the point held fixed under rotation/scale. |
| `rotation_mode` | enum `continuous` \| `angle_snapped` | — | `continuous` = bone rotation applied directly as `SpriteBatch` rotation. `angle_snapped` = §3.10 applies. |
| `angle_range_deg` | [float, float], required iff `angle_snapped` | `min < max`, both within (-180, 180] | Full rotation arc this part is ever posed across (authored per part, per A2). |
| `angle_variant_count` (`V`) | int, required iff `angle_snapped` | 3–9 (§7) | Number of pre-authored angle variants spanning `angle_range_deg`. |
| `angle_variant_textures` | list of `V` `{texture_region, pivot}` pairs, required iff `angle_snapped` | — | Pre-rotated sprite + matching pivot per variant index `0..V-1`, evenly spaced (Formula 1). Pivot is authored per variant independently, since a differently-rotated crop can shift the pixel-space anchor even when the conceptual anchor point stays constant. |
| `draw_order` | int | Unique within the rig, ≥ 0 | Ascending painter's-order index for this creature's own parts (§3.9) — independent of bone hierarchy. |

### 3.4 PoseFrame (the Pose Library)

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `pose_id` | string | Unique within the rig. MVP vocabulary (art-bible §8.4): `rest`, `windup`, `peak`, `recovery`, `hit`, `break`. Additional `pose_id`s are permitted for role-specific work-loop content. | Named, hand-authored bone-transform set. |
| `bone_offsets` | map of `bone_id` → `{position_offset (Vector2, px, additive), rotation_offset_deg (float, additive), scale_factor (Vector2, unitless, multiplicative, default (1,1))}` | One entry per bone the pose touches; bones with no entry default to zero offset (i.e. rest) | Per-bone delta from that bone's `rest_local_*` values. Position/rotation are additive deltas; scale is a multiplicative factor (so "no change" = 1.0, not 0) — this distinction matters for Formula 4. |

### 3.5 Clip and ClipKey

| Field (`Clip`) | Type | Range / Values | Description |
|---|---|---|---|
| `clip_id` | string | Unique within the rig | Identifies one playable animation state (e.g. `hostile_idle`, `bound_healthy_loop`, `attack_strike`, `telegraph_windup`). |
| `frame_count` (`N`) | int | ≥ 1 | Number of discrete playback sample-steps this clip is divided into. Independent of how many `keys` (poses) it references — see A1. |
| `playback_fps` | float | > 0 | Nominal playback rate. Ignored when `duration_ms_override` is set (Formula 2). |
| `keys` | list of `ClipKey` | ≥ 1, `at_frame` values non-decreasing across the list, all within `[0, N-1]` | The pose sequence this clip interpolates between. |
| `loop` | bool | — | If `true`, playback wraps (Formula 2). If `false`, playback holds on the final frame once complete and the clip signals completion (§3.11). |
| `hold_frame` | int, nullable | `[0, N-1]` if set | If a caller sets `is_held = true` on this clip instance, playback freezes at this frame regardless of elapsed time. Used for the Blocked work-state (art-bible §5.3). |
| `duration_ms_override` | float, nullable | > 0 if set | If present, the clip's total wall-clock length is exactly this value, independent of `frame_count`/`playback_fps` (Formula 2). Used for the telegraph wind-up's data-driven duration (art-bible §8.4). |
| `default_easing_curve_id` | string | Must reference an entry in the project Curve Library (A7) | Applied to any `ClipKey` that does not specify its own override. |

| Field (`ClipKey`) | Type | Range / Values | Description |
|---|---|---|---|
| `pose_id` | string | Must reference a `pose_id` in the rig's `pose_library` | The pose this key resolves to exactly, at `at_frame`. |
| `at_frame` | int | `[0, N-1]` | The nominal frame at which the clip's evaluated pose equals `pose_id` exactly. |
| `easing_curve_id` | string, nullable | Must reference an entry in the Curve Library, or `null` to inherit `Clip.default_easing_curve_id` | Governs the segment *from* this key *to* the next key in the list. Ignored on the list's final key (nothing follows it). |

### 3.6 AdditiveLayer

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `layer_id` | string | Unique within the rig | Identifies one secondary-motion layer (e.g. `glyph_pulse`, `breathing`, `manipulator_twitch`). |
| `affected_bone_ids` | list of `bone_id` | Each must reference an existing `Bone.bone_id` | The (usually small) subset of bones this layer touches. |
| `frame_count`, `playback_fps`, `loop` | Same types/ranges as `Clip` | — | Independent playback clock from whatever base `Clip` it composites onto (§3.13). |
| `keys` | list of `ClipKey` | 2–4 typical (art-bible §8.4) | Same shape as a `Clip`'s keys, but `bone_offsets` on the referenced poses are always interpreted as pure deltas (§3.13), never absolute. |

### 3.7 PartDecalBinding

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `part_id` | string | Must be a non-`null` `Part.part_id` on this rig | Which part this binding applies to. |
| `crack_stage` | int | `[1, that part's PartDefinition.crack_stage_count + 1]` (`creature-data-schema.md` §3.3) | Which break-progress stage this decal represents. The final value (`crack_stage_count + 1`) represents the fully-broken visual, still rendered as a composited overlay — never a swapped base part texture (art-bible §8.4's "never a baked full-body/per-part variant" mandate). |
| `decal_asset` | `DecalAsset` (below) | — | The overlay art for this stage. |

| Field (`DecalAsset`, `Part.rotation_mode = continuous`) | Type | Description |
|---|---|---|
| `texture_region` | rect (shared decal atlas, A8) | The crack-overlay sprite. |
| `local_offset` | Vector2 (px, part-pivot-relative) | Position of the decal relative to the part's pivot. |
| `pivot` | Vector2 (px) | Decal's own rotation/scale anchor. |

| Field (`DecalAsset`, `Part.rotation_mode = angle_snapped`) | Type | Description |
|---|---|---|
| `variants` | list of `V` `{texture_region, local_offset, pivot}` | One entry per the bound part's own `angle_variant_count`, indexed identically (A5) — the decal reuses whichever variant index the part itself resolved that frame (§3.10); no separate angle computation. |

### 3.8 GlyphOverlay

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `anchor_bone_id` | string | Must reference an existing `Bone.bone_id` | Which bone's resolved world transform the glyph overlay is attached to. |
| `anchor_local_offset` | Vector2 (px) | Authored, identical convention across every creature (art-bible §8.6) | Fixed body-relative offset from the anchor bone's resolved position. |
| `vuln_ring_texture_region` | rect (shared glyph atlas) | — | Drawn when `CreatureInstance.bind_state = hostile`. |
| `ward_seal_texture_region` | rect (shared glyph atlas) | — | Drawn when `CreatureInstance.bind_state = bound`. |

The glyph overlay's runtime brightness/hue (the telegraph wind-up pulse, the Starved-state Ember
Threat flicker) is driven by an HLSL palette-lookup shader on this same draw, per art-bible §8.5.
This rig submits the draw call and exposes the shader-parameter hook; it does not own the
brightness curve or timing rule that drives it — that belongs to `creature-ai-telegraph-system`
(§6), matching the split pattern `creature-data-schema.md` already established for
`vulnerable_parts`/`vulnerability_window_ms`.

### 3.9 Transform Hierarchy and the Recursive Draw Walk

Every bone's world transform is the composition of its parent's world transform with its own
resolved local transform:

```
world_transform(root)  = creature_world_transform  (supplied per-instance, per-frame, by the calling system — combat-encounter-system or region-mastery-automation-system; out of scope here)
world_transform(bone)  = world_transform(parent(bone)) ∘ local_transform(bone, f)
local_transform(bone,f) = rest_local_transform(bone) + resolved_pose_offset(bone,f)   [§3.11–3.13]
```

Position composes by rotating the child's local offset by the parent's accumulated rotation, then
translating; rotation composes by summation (normalized to (-180, 180] after each composition);
scale composes component-wise multiplicatively — standard 2D affine hierarchy composition.

The draw walk is depth-first over the bone tree **only to resolve transforms**. Actual `Draw()`
**submission order is governed by `Part.draw_order` ascending, not tree traversal order** — this
decouples an artist's painter's-order needs (e.g. drawing a background limb before a foreground
overlay) from whatever shape the bone hierarchy happens to take.

### 3.10 Angle-Snapped Rotation (the Locked Mitigation)

Per the locked engineering decision (art-bible §8.5): skeletal transforms drive **position and
timing**; **rotation for any part flagged `angle_snapped` is resolved by swapping to the nearest
pre-authored angle-variant texture, drawn with zero additional `SpriteBatch` rotation** — never by
passing a continuous angle into `Draw()`'s rotation parameter for that part. This is the mandatory
mitigation against rotating hard-edge pixel art to non-cardinal angles, which produces filtered
blur (linear sampling) or stair-stepping (point sampling) either way — violating art-bible §6.4's
clean-pixel mandate and §3.1's fixed 1–2px seam weight.

For `rotation_mode = continuous` parts (subtle motions — breathing swell, small weight shifts,
satellite orbit, manipulator fidgets — per art-bible §8.5, "low-risk, mostly translation and
small-angle sway"), the resolved `world_transform.rotation` is passed directly to `SpriteBatch.Draw`.
For `rotation_mode = angle_snapped` parts, `world_transform.rotation` is used **only** as the input
`θ` to Formula 1 (§4) to select a variant index; the variant's own pre-rotated texture is drawn with
`rotation = 0`.

**The Attacker's strike-and-recover cycle is the widest motion arc in the game (art-bible §5.3) and
the part most likely to require `angle_snapped` on its striking limb — it is the first spike target
per the Prototype Spike Required callout above.**

### 3.11 Clip Playback: Frame Advance, Hold, and Loop

Governed by Formula 2 (§4). Summary: an `elapsed_ms` clock (owned by whatever system is driving
this clip instance) is converted to a nominal integer `current_frame` in `[0, frame_count-1]`,
accounting for `duration_ms_override` (if set), `hold_frame`/`is_held` (if the caller has frozen
playback — the Blocked work-state mechanism), and `loop` (wraparound vs. clamp-and-signal-complete).
A non-looping clip that reaches its final frame sets a completion flag/event; consuming systems
(e.g. `combat-encounter-system` transitioning out of a hit-reaction back to idle) read that flag —
this rig does not decide what happens next, only reports that the clip finished.

### 3.12 Easing-Curve Override Mechanism

Governed by Formula 3 (§4). Given the current frame, the two `ClipKey`s bracketing it are found;
the segment's normalized progress `t_raw` is computed, then transformed through the segment's
assigned easing function (`ClipKey.easing_curve_id`, or `Clip.default_easing_curve_id` if unset) to
produce `t_eased`; every bone's transform is linearly interpolated between the two keys' `PoseFrame`
offsets using `t_eased` (rotation interpolates along the shortest arc — see Edge Case 5).

**This is the single mechanism that makes the hostile-vs-bound performance-set economy (art-bible
§8.4/§5.5) buildable at zero new frames**: two `Clip`s can reference the identical `keys` list
(same `pose_id`s, same `at_frame` values) and differ *only* in which `easing_curve_id` each key
uses — sharp/sudden for hostile, softened ease-in/out for bound. No new `PoseFrame` is authored for
either variant.

### 3.13 Additive Layer Compositing

Governed by Formula 4 (§4). An `AdditiveLayer` runs its own independent playback clock (its own
`frame_count`/`playback_fps`/`loop`) and, each frame, resolves a delta pose exactly the way a `Clip`
does (§3.12) — but that delta is **summed** (position/rotation) or **multiplied** (scale) onto the
base clip's already-resolved transform for each bone it targets, never substituted in place of it.
A layer only ever touches bones listed in its own `affected_bone_ids`; every bone always has a
well-defined base-clip transform to add onto, so "the layer and the base clip disagree" is
structurally impossible by construction (Edge Case 3). Multiple layers targeting the same bone
compose associatively — each layer's contribution is summed/multiplied in, in any order.

### 3.14 Decal Compositing (Part-Break)

Each frame, for every non-`null` `Part.part_id` on a **hostile** creature instance (bound creatures
have their `hostile_state` cleared entirely per `creature-data-schema.md` Edge Case 3/A4, so no
decal ever renders on a bound creature — a free consistency check), the rig reads that part's
`PartState.current_stage` from `CreatureInstance.hostile_state.part_states` (exact field, schema
§3.6). If `current_stage = 0` (intact), no decal is drawn. Otherwise the rig looks up
`part_decal_bindings[part_id][current_stage]` and draws it as one additional `Draw()` call. Decal
draws are batched into their own contiguous submission block (§3.16), not interleaved with body
draws — visually correct because a decal only ever occludes pixels belonging to its own part,
regardless of *when* other creatures' bodies were drawn (§3.16 explains why this is both visually
identical to per-part interleaving and strictly better for batching). For a part whose
`rotation_mode = angle_snapped`, the decal reuses whichever variant index Formula 1 already
selected for that part this frame (A5) — no separate angle lookup.

### 3.15 Glyph-Overlay Anchoring and Bind-State Swap

Each frame, the rig resolves `world_transform(anchor_bone_id)` (§3.9), offsets it by
`anchor_local_offset`, and draws whichever texture matches `CreatureInstance.bind_state` (exact
field, schema §3.5) — `vuln_ring_texture_region` for `hostile`, `ward_seal_texture_region` for
`bound` — as the topmost element in the creature's draw order (art-bible §3.5's hierarchy: the
vulnerability glyph is the hero shape). During the Mastery Transition (art-bible §2), the creature
holds its final combat pose (a single-key `Clip`, or `hold_frame` frozen on the last resolved
frame) while only this glyph draw animates, per art-bible §8.4's "1–2 glyph frames... only the
glyph-accent overlay animates" cost accounting — the rig supports this directly as "freeze the body
clip, let the glyph overlay's own shader-driven state keep updating," no special-case code path
needed.

### 3.16 Draw-Submission and Batching Contract

Per `SpriteBatch`'s actual batching mechanics (art-bible §8.5): batching is driven by
**texture-switch count in submission order**, not sprite count, and consecutive `Draw()` calls
sharing a `Texture2D` merge into one GPU draw call under `SpriteSortMode.Deferred`. This rig commits
to a **three contiguous submission blocks per creature-pass**, all inside one `Begin()`/`End()`
boundary, all sharing one `Effect` (no per-creature Effect switch — per-creature variation goes
through the per-`Draw()` Color tint parameter only, per art-bible §8.5):

1. **Body block** — for every active creature, sorted by ground-anchor Y (A6), then for each
   creature its own `Part`s in ascending `draw_order`: draw from that region's shared creature
   atlas.
2. **Decal block** — for every active creature (same order), every part whose `current_stage > 0`
   (§3.14): draw from the shared decal atlas (A8).
3. **Glyph block** — for every active creature (same order), its `GlyphOverlay` draw (§3.15): draw
   from the shared glyph atlas.

Because each block is texture-homogeneous and submitted contiguously, this produces **at most
(number of distinct region atlases active, typically 1) + 1 (decal atlas) + 1 (glyph atlas)
texture-switch boundaries per creature-pass, regardless of creature count** — an O(1) batch count
with respect to how many creatures are on screen. This is *why* decals live in their own shared
atlas rather than each region's creature atlas (A8): interleaving body and decal draws per-part
(one texture, then the other, alternating per part with an active crack) would force a texture-switch
flush on every decorated part, whereas deferring all decals to their own block costs nothing extra
and keeps the guarantee intact. Visual correctness is preserved because a decal or glyph never
occludes any pixel outside its own small, creature-localized region, so "draw all bodies, then all
decals, then all glyphs" produces an identical final image to per-creature interleaving.

**Forbidden, per art-bible §8.5 / `.claude/docs/technical-preferences.md`**: `Effect` switching
per creature; `SpriteSortMode.Immediate` outside debugging; per-creature textures (creatures share
one region atlas); baking decals or glyphs into body sprites.

### 3.17 Schema Invariants (validation rules)

These must hold for any valid `CreatureRig` and are the basis for the Acceptance Criteria in §8:

1. Every non-`null` `Part.part_id` in a rig references an existing `part_id` on that rig's
   `CreatureTemplate.parts`.
2. The count of non-decorative `Part`s (non-`null` `part_id`) in a rig equals its template's
   `parts.length` exactly — every targetable part has exactly one corresponding rig `Part`.
3. Exactly one `Bone` in a rig has `parent_bone_id = null` (the root).
4. Every non-`null` `Bone.parent_bone_id` references another `bone_id` in the same rig, and the
   parent graph is acyclic (a tree, not a general graph).
5. Every `ClipKey.pose_id` (in any `Clip` or `AdditiveLayer`) references an existing `pose_id` in
   the rig's `pose_library`.
6. Every non-`null` `easing_curve_id` (on a `ClipKey` or as a `Clip.default_easing_curve_id`)
   references an existing entry in the project Curve Library.
7. Within one `Clip` or `AdditiveLayer`, `ClipKey.at_frame` values are non-decreasing in list order
   and all lie within `[0, frame_count-1]`.
8. Every `AdditiveLayer.affected_bone_ids` entry references an existing `Bone.bone_id`.
9. Every `part_decal_bindings` key's `crack_stage` lies within `[1, that part's PartDefinition
   .crack_stage_count + 1]` (`creature-data-schema.md` §3.3).
10. `GlyphOverlay.anchor_bone_id` references an existing `Bone.bone_id`.
11. For any `Part` with `rotation_mode = angle_snapped`: `angle_variant_textures.length ==
    angle_variant_count` exactly, and `angle_range_deg[0] < angle_range_deg[1]`.

### 3.18 Exposed Per-Part Transform Contract (Consumed by `input-targeting-system`)

Every frame, after this rig resolves all bone/part transforms (§3.9–3.13) and before hit-testing
runs (`input-targeting-system.md` §3.8 requires this ordering explicitly, and this GDD confirms
it), the rig exposes one transform per non-decorative `Part`, keyed by `part_id`, in the same
virtual-canvas space `input-targeting-system.md`'s Assumption A2 assumes:

```
{ part_id: string, position: Vector2, rotation: float (deg), scale: Vector2 }
```

**Confirming and refining `input-targeting-system.md`'s Assumption A1**: this exposed transform is
**exactly what was used to draw that part this frame** — never the underlying continuous skeletal
value it was derived from. This distinction is only visible for `angle_snapped` parts (§3.10): for
those, the exposed `rotation` is always `0`, and the exposed `position` corresponds to whichever
angle-variant's own authored pivot (§3.3) Formula 1 selected this frame — never the continuous bone
angle `θ`. If the exposed transform instead reported the continuous `θ` for a part whose pixels
were actually rendered unrotated (the entire point of §3.10's mitigation), `input-targeting-system`'s
per-pixel alpha-mask hit-testing (that GDD's §3.5 step 2 — inverting a part's current-frame
transform to map a click point into local texture space) would invert the wrong rotation and test
against the wrong pixels: a silent, hard-to-diagnose hit-testing bug, not a crash. This rig
guarantees the render transform and the exposed transform are always identical by construction —
both are read from the same resolved per-`Draw()` value; there is no second code path that could
drift out of sync. The same guarantee applies to `input-targeting-system.md` Formula 3's
`part_anchor`/`core_anchor` (cycle-order angular sort): both are this same exposed `position`
value, for the candidate part and for the template's `is_core = true` part respectively.

## 4. Formulas

All formulas below feed **rendering-time transforms**, not stored/persisted data — unlike
`creature-data-schema.md`'s formulas, no rounding is applied to continuous transform outputs
(positions, rotations, scales are floats consumed directly by `SpriteBatch`). Round-half-up
(A3) applies only to the **integer variant index** in Formula 1.

### Formula 1 — Angle-Snap Variant Selection

```
step = (angle_max − angle_min) ÷ (V − 1)
i_raw = (θ_clamped − angle_min) ÷ step,   where θ_clamped = clamp(θ, angle_min, angle_max)
variant_index = round_half_up(i_raw),   clamped to [0, V − 1]
variant_angle(i) = angle_min + i × step
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `θ` | float | Unbounded input, typically the bone's continuously-tweened world rotation this frame | The desired continuous angle before snapping. |
| `angle_min`, `angle_max` | float (deg) | `Part.angle_range_deg` | The part's authored full motion arc. |
| `V` | int | 3–9 (§7) | `Part.angle_variant_count`. |
| `step` | float (deg) | > 0 | Even spacing between authored variants. |
| `variant_index` | int | `[0, V-1]` | Output. Selects `angle_variant_textures[variant_index]`. |

**Output range**: `variant_index` is always an integer in `[0, V-1]` — clamping both the input
angle and the final index guarantees no out-of-bounds array access even if a tween momentarily
overshoots the authored range.

**Worked example (selection)**: Attacker's striking limb, `angle_range_deg = [-30, 150]`, `V = 7`.
```
step = (150 − (−30)) ÷ (7 − 1) = 180 ÷ 6 = 30
θ = 72°  (θ_clamped = 72, already within range)
i_raw = (72 − (−30)) ÷ 30 = 102 ÷ 30 = 3.4
variant_index = round_half_up(3.4) = 3
variant_angle(3) = −30 + 3 × 30 = 60°
```
The rig draws `angle_variant_textures[3]` (the pre-rendered 60° art) with `rotation = 0`, even
though the bone's true continuous rotation at this tween moment is 72°.

**Worked example (tie-break)**: same part, `θ = 15°`.
```
i_raw = (15 − (−30)) ÷ 30 = 45 ÷ 30 = 1.5   (exact tie between variants 1 and 2)
variant_index = round_half_up(1.5) = 2
variant_angle(2) = −30 + 2 × 30 = 30°
```
Per A3, an exact tie resolves toward the higher index.

### Formula 2 — Frame-Advance Timing (Current Frame from Elapsed Time)

```
frame_duration_ms = duration_ms_override ÷ frame_count     if duration_ms_override is set
                   = 1000 ÷ playback_fps                    otherwise

raw_frame = floor(elapsed_ms ÷ frame_duration_ms)

current_frame = hold_frame                        if is_held = true
              = raw_frame mod frame_count          if loop = true
              = min(raw_frame, frame_count − 1)    if loop = false
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `elapsed_ms` | float | ≥ 0, owned by the caller | Wall-clock time since this clip instance started (or since it was last un-held). |
| `frame_count` | int | ≥ 1 | `Clip.frame_count`. |
| `playback_fps` | float | > 0 | `Clip.playback_fps`. Ignored if `duration_ms_override` is set. |
| `duration_ms_override` | float, nullable | > 0 if set | `Clip.duration_ms_override`. |
| `loop` | bool | — | `Clip.loop`. |
| `is_held` | bool | — | Caller-set flag (the Blocked work-state trigger). |
| `hold_frame` | int, nullable | `[0, frame_count-1]` | `Clip.hold_frame`. |
| `current_frame` | int | `[0, frame_count-1]` | Output. Feeds Formula 3. |

**Output range**: always a valid frame index within `[0, frame_count-1]`; floor/mod/min guarantee
no overrun regardless of how large `elapsed_ms` grows.

**Worked example (loop)**: Crafter Healthy work loop, `frame_count = 8`, `playback_fps = 12`
(art-bible §8.4), `duration_ms_override = null`, `loop = true`, `is_held = false`,
`elapsed_ms = 950`.
```
frame_duration_ms = 1000 ÷ 12 = 83.33ms
raw_frame = floor(950 ÷ 83.33) = floor(11.4) = 11
current_frame = 11 mod 8 = 3
```

**Worked example (hold)**: same clip, `is_held = true`, `hold_frame = 2`, `elapsed_ms = 5000`
(irrelevant once held).
```
current_frame = 2
```
This is the entire mechanism behind the Blocked work-state costing zero new frames (art-bible
§8.4): the Healthy loop is simply frozen mid-cycle.

**Worked example (data-driven duration)**: telegraph wind-up clip, `frame_count = 60` (fine
sampling for a smooth tween despite only 2 authored keys), `duration_ms_override = 1800`
(a tunable data value owned by `creature-ai-telegraph-system`), `loop = false`,
`elapsed_ms = 900`.
```
frame_duration_ms = 1800 ÷ 60 = 30ms
raw_frame = floor(900 ÷ 30) = 30
current_frame = min(30, 59) = 30
```
Doubling `duration_ms_override` to 3600 with everything else unchanged doubles the wall-clock time
to reach any given `current_frame`, while `frame_count`/`playback_fps` — and therefore the number
of authored keys — never change. This is the "data-driven clip duration independent of frame
count" requirement, satisfied directly.

### Formula 3 — Easing-Curve Pose Interpolation

```
Given current_frame f, and the bracketing keys key_A (at frame_A ≤ f) and key_B (at frame_B > f):

t_raw = clamp((f − frame_A) ÷ (frame_B − frame_A), 0, 1)
t_eased = ease(easing_curve_id, t_raw)
bone_transform(bone, f) = lerp(poseA.bone_offset[bone], poseB.bone_offset[bone], t_eased)
```
(Rotation lerps along the shortest arc — see Edge Case 5. If `f == frame_A` exactly, or only one
key exists, the resolved pose is `poseA` exactly, no interpolation performed.)

| Symbol | Type | Range | Description |
|---|---|---|---|
| `f` | int | `[0, frame_count-1]` | Formula 2's output. |
| `frame_A`, `frame_B` | int | `frame_A ≤ f < frame_B` | The two `ClipKey.at_frame` values bracketing `f`. |
| `t_raw` | float | `[0, 1]` | Linear progress through the segment. |
| `easing_curve_id` | string | Curve Library entry (A7) | `ClipKey.easing_curve_id` (or the clip's default). |
| `t_eased` | float | Typically `[0, 1]` (curve-dependent; overshoot curves are disallowed per art-bible §5.5's "no elastic bounce" rule for creature clips) | Eased progress. |
| `bone_transform` | Vector2/float/Vector2 (pos/rot/scale) | Unbounded (rendering float) | Output. Feeds §3.9's `local_transform`. |

**Output range**: unbounded (a rendering float), but bounded in practice by the two bracketing
poses' authored values and the chosen curve's own range.

**Worked example — the hostile-vs-bound zero-new-frames proof**: segment `key_A = {pose_id: rest,
at_frame: 0}`, `key_B = {pose_id: peak, at_frame: 5}`. Bone `upper_arm`'s `rotation_offset_deg`:
`rest = 0°`, `peak = 110°`. Evaluated at `f = 2` for two clips sharing this identical key data,
differing only in `easing_curve_id`:

```
t_raw = (2 − 0) ÷ (5 − 0) = 0.4

Hostile clip, easing_curve_id = "quad_ease_in" [e(t) = t²]:
  t_eased = 0.4² = 0.16
  rotation = lerp(0, 110, 0.16) = 17.6°

Bound clip, easing_curve_id = "smooth_ease_in_out" [e(t) = 3t² − 2t³]:
  t_eased = 3(0.16) − 2(0.064) = 0.48 − 0.128 = 0.352
  rotation = lerp(0, 110, 0.352) = 38.7°
```

Identical `PoseFrame` data, identical `at_frame` values, zero additional authored art — two
visibly distinct arm positions at the same nominal frame, purely from curve choice.

### Formula 4 — Additive-Layer Compositing (Final Bone Transform)

```
position_final(bone,f) = base_position(bone,f) + Σ_i active_layer_i.position_offset(bone,f)
rotation_final(bone,f)  = normalize(base_rotation(bone,f) + Σ_i active_layer_i.rotation_offset(bone,f))
scale_final(bone,f)     = base_scale(bone,f) × Π_i active_layer_i.scale_factor(bone,f)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_*(bone,f)` | Vector2/float/Vector2 | Formula 3's output | The base `Clip`'s resolved transform for this bone. |
| `active_layer_i` | `AdditiveLayer` | 0+ layers whose `affected_bone_ids` includes `bone` | Each currently-playing additive layer touching this bone. |
| `normalize(...)` | function | — | Wraps a summed rotation back to `(-180, 180]`. |
| `*_final` | Vector2/float/Vector2 | Unbounded (rendering float) | Output. What §3.9's draw walk actually uses. |

**Output range**: unbounded, same rendering-float caveat as Formula 3. Position/rotation compose
additively (0 = no contribution); scale composes multiplicatively (1.0 = no contribution) — mixing
these conventions up is exactly the failure Edge Case 3 guards against.

**Worked example**: base clip resolves the core bone to `position = (0,0)`, `rotation = 0°`,
`scale = (1,1)` at frame `f`. Two additive layers are simultaneously active on the core bone's
scale only: `breathing` contributes `scale_factor = (1.05, 1.05)`; `glyph_pulse` contributes
`scale_factor = (1.02, 1.02)`. Neither touches position or rotation.
```
position_final = (0,0) + (0,0) + (0,0) = (0,0)
rotation_final = normalize(0 + 0 + 0) = 0°
scale_final    = (1,1) × (1.05,1.05) × (1.02,1.02) = (1.071, 1.071)
```
Both layers' contributions are present simultaneously (a 7.1% swell), neither overriding the
other or the base pose.

## 5. Edge Cases

1. **A `Clip`'s easing override references a curve that doesn't exist.** Rejected at
   content-load/validation time (§3.17, invariant 6) — never silently substituted with a default at
   runtime. A missing curve is a data-integrity bug, not a recoverable runtime condition.
2. **A part-break decal is requested for a `crack_stage` the rig's `part_decal_bindings` has no
   entry for, but the stage itself is valid per `PartDefinition.crack_stage_count`.** Falls back to
   rendering the base part with no decal, and logs a content-integrity warning — never renders the
   wrong stage's decal, never throws. (Requesting a stage *outside* the valid range is rejected at
   load time per invariant 9, a distinct, stricter failure — that is an authoring error, this is a
   coverage gap.)
3. **An additive layer and a base clip "disagree" on a part's transform.** Structurally impossible
   by construction (§3.13, Formula 4): additive layers only ever contribute deltas/factors summed or
   multiplied onto the base clip's already-resolved transform, never an absolute override. The only
   real sub-case is two layers targeting the same bone simultaneously — they compose associatively
   (both apply, Formula 4's worked example), never one silently winning over the other. If a
   composed scale factor would exceed a sane bound (e.g. an authoring mistake stacking many layers),
   the renderer clamps defensively rather than letting a creature balloon or invert.
4. **A creature is destroyed mid-clip.** Its rig instance is torn down immediately; it is removed
   from the draw-submission list (§3.16) for the very next frame, with zero partial-frame draw calls
   submitted. Any in-flight tween/interpolation state is discarded outright — no clip is allowed to
   "finish" after its owning instance no longer exists. Mirrors `creature-data-schema.md` Edge Case
   3's atomicity discipline for the analogous data-layer transition.
5. **A bound creature's work-loop is interrupted by the region being re-entered for combat
   (hostile↔bound performance switch mid-animation).** The rig does **not** cross-fade or blend
   between the outgoing and incoming clips. It hard-cuts: on the frame `bind_state` changes, the new
   state's clip begins at its own frame 0, discarding all in-progress interpolation state from the
   old clip instantly. This is a deliberate design choice, not a shortcut: art-bible §5.5's design
   test requires hostile and bound animation sets to be distinguishable "from pose alone" on any
   single paused frame — a blended in-between pose would produce an ambiguous frame that fails that
   test. (Rotation interpolation within a single clip, by contrast, does use shortest-arc lerp — the
   "no blending across a bind-state switch" rule applies only to the switch itself, not to normal
   intra-clip tweening, which must still avoid the wrong-direction wraparound a naive linear lerp on
   raw degree values would produce, e.g. lerping 170°→-170° must go the short way through 180°, not
   the long way through 0°.)
6. **A rotating part's continuous bone angle falls exactly halfway between two authored angle
   variants (a tie in Formula 1's nearest-neighbor snap).** Resolved deterministically: round-half-up,
   toward the higher-index (larger-angle) variant (A3, Formula 1's tie-break worked example).
7. **A boss-tier creature's larger part count (5–8 vs. standard's 3–5, `creature-data-schema.md`
   invariant 2) and larger frame budget (~50–60 vs. ~25–40, art-bible §8.4).** The rig's data model
   places no fixed cap on `bones`/`parts`/`pose_library` length — `CreatureRig.parts.length` is
   validated *against the template's actual part count* (§3.17, invariant 2), never hard-coded. A
   boss's rig is simply a larger instance of the same data shape, not a special case.
8. **A `Part.part_id` doesn't match any `part_id` on the creature's current template** (e.g. stale
   rig content after a template edit). Rejected at load time (§3.17, invariant 1) — fails closed,
   matching `creature-data-schema.md`'s validation philosophy. Decorative parts (`part_id = null`)
   are explicitly exempt and always valid; they are excluded from decal binding and never appear in
   `vulnerable_parts`-driven logic.
9. **An `AdditiveLayer` targets a `bone_id` that doesn't exist on the rig.** Rejected at load time
   (§3.17, invariant 8) — a silent no-op would hide an authoring mistake rather than surface it.
10. **A creature evolves mid-scene** (`creature-data-schema.md` §3.4 — `CreatureInstance
    .template_id` changes to a new template while `instance_id` persists). The old `CreatureRig`
    instance is discarded entirely and a new one is constructed against the new `template_id`'s rig
    data (Source's edge-quality persists per that schema's A1, but Role/silhouette may change
    completely, so the two rigs are not assumed compatible). Handled identically to #4/#5: a hard
    cut, no blend — any masking of the cut (a VFX flash, the evolution cinematic) is owned by
    whichever system triggers the evolution, not by this rig.
11. **Two `ClipKey`s in the same clip share an identical `at_frame` value.** Legal — a zero-length
    segment, i.e. an instant pose snap at that frame. This is the exact mechanism a "peak-hold"
    state (e.g. the Mastery Transition's frozen final combat pose, §3.15) uses: a single-key clip,
    or two keys at the same frame, both resolve to a static pose held for the clip's full duration.
    A clip with only one key is always legal for the same reason.
12. **`duration_ms_override` is set alongside `frame_count`/`playback_fps`.** No conflict:
    `duration_ms_override`, when present, is authoritative for total wall-clock length (Formula 2);
    `frame_count` continues to describe sampling granularity (structure), and `playback_fps` is
    simply ignored in that case. This is what decouples "how many discrete steps this clip is
    sampled at" from "how long it takes to play," exactly as required for the telegraph wind-up.

## 6. Dependencies

### Depends On

- **`creature-data-schema.md`** — this rig reads, and does not redefine: `CreatureTemplate
  .template_id` (rig-selection key), `CreatureTemplate.parts` (count, `part_id`, `is_core` — which
  parts must have a corresponding rig `Part`), `CreatureTemplate.tier` (canvas-size class, standard
  vs. boss, which the rig's bone/part count scales to without a hard cap — Edge Case 7),
  `PartDefinition.crack_stage_count` (bounds `part_decal_bindings`'s valid `crack_stage` range),
  `CreatureInstance.bind_state` (drives the glyph-overlay texture swap, §3.15, and gates decal
  rendering to hostile-only, §3.14), `CreatureInstance.hostile_state.part_states[].current_stage`
  (drives decal selection, §3.14), and `CreatureInstance.bound_state.work_state` (selects which
  Healthy/Blocked/Starved clip and hold-state is active — the selection rule itself belongs to
  `region-mastery-automation-system`, not this rig). This document does not define combat timing,
  telegraph rules, evolution transitions, or loot — each is a different system's job, per that
  schema's own §6 split.

### Depended On By

- **`input-targeting-system.md`** (already written) — its Assumption A1 identified this rig as a
  newly-required runtime dependency (that GDD's own edit to `systems-index.md` added the edge) for
  a per-part, per-frame `{part_id, position, rotation, scale}` transform in virtual-canvas space,
  used for hit-testing and the cycle-order angular sort. §3.18 above confirms and refines that
  contract — in particular, the critical detail that the exposed transform for `angle_snapped`
  parts always reports `rotation = 0` (matching what was actually rendered), never the underlying
  continuous skeletal angle.
- **`combat-encounter-system`** — will trigger clip transitions (idle → windup →
  peak → recovery → hit-reaction) in sync with hit resolution, and will read a clip's completion
  signal (§3.11) to know when control returns to idle/AI. This rig defines *how* a clip plays, not
  *when* combat logic should change clips — that sequencing is combat-encounter-system's design.
- **`creature-ai-telegraph-system`** — owns the telegraph wind-up's
  `duration_ms_override` value and the slow-build/hard-cutoff brightness curve driven through the
  glyph overlay's shader parameter (§3.8, §3.15). This rig exposes the mechanism (a data-driven
  clip duration, a shader-parameter hook on the glyph draw); that system owns the timing rule and
  curve itself, mirroring the split `creature-data-schema.md` already established for
  `vulnerability_window_ms`.
- **`region-mastery-automation-system`** — will select and hold/release the
  Healthy/Blocked/Starved clip variant for each bound creature's work loop, reading
  `bound_state.work_state`, and will set `is_held`/`hold_frame` (§3.11) to implement the Blocked
  state. This rig supports the mechanism (§3.11's hold semantics); that system owns *when* a
  creature transitions between work states.

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** — the source-of-truth for the locked engineering decision (§8.5),
  frame budgets (§8.4), batching rules, and every asset-standard constraint this GDD specifies
  against. Not a GDD, so not listed as a formal dependency, but every section of this document
  traces back to it.
- **`save-load-persistence.md`** — explicitly **not** a dependency in either direction (A4). Rig
  playback state (current clip, current frame, hold flag) is transient and re-derived at
  spawn/load time from already-persisted `CreatureInstance` fields; no rig-specific field needs to
  be added to that schema's persistence model.
- **`item-data-schema.md` / `the-forge-system`** — out of scope. Enchantment-inlay visuals (art-bible
  §5.2) render on the Hunter, not on creatures, and are not this rig's concern.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Locked or Free | Gameplay/Production Effect |
|---|---|---|---|---|
| Angle-variant count per rotating part | `Part.angle_variant_count` (`V`) | 3–9 | **Genuinely free** — art-bible §8.5 mandates only "a small set," no number | More variants = smoother rotation illusion, more art budget (each variant is a full pre-rendered texture). Wide-arc parts (Attacker's striking limb) need more; subtle-sway parts need fewer or none (use `continuous`). |
| Angle range per rotating part | `Part.angle_range_deg` | Authored per part to match its actual motion arc | **Free**, no fleet-wide constant (A2) | Too narrow clips legitimate poses to the endpoint variant (visible snapping at the extremes); too wide wastes variant budget on angles the part never reaches. |
| Angle-snap tie-break rule | Formula 1 | Fixed: round-half-up | **Locked by this GDD** (A3, for project-wide consistency with `creature-data-schema.md`) | Deterministic, reproducible variant selection — not a balance lever. |
| Per-role loop length / playback fps | `Clip.frame_count`/`playback_fps` for work-loop clips | Crafter 8f@12fps, Attacker/Support 10f@10fps, Defender 8f@6fps, Producer 12f@4fps | **Locked by art-bible §8.4** — not safely retunable without an art-bible revision | Defines each role's readable "verb" cadence (art-bible §5.3); changing it without a corresponding art pass risks breaking the pose-timing the poses were authored against. |
| Easing curve set (Curve Library) | `ClipKey.easing_curve_id` / `Clip.default_easing_curve_id` | A small curated set drawn from `MonoGame.Extended.Tweening`'s built-ins (A7) | **Free**, exact set is an implementation/ADR detail | Directly controls the hostile-vs-bound performance-set distinction (§3.12) — "sharp/sudden" vs. "softened ease-in/out" per art-bible §5.5, at zero art cost. |
| Additive-layer frame count | `AdditiveLayer.keys` length | 2–4 | **Locked by art-bible §8.4** | Secondary-motion layers (glyph pulse, twitch, breathing) are deliberately cheap; more frames erodes the production economy this rig exists to protect. |
| Part-break decal stage count | `PartDecalBinding.crack_stage` range | 1–2 cracking stages (+1 broken terminal state) | **Locked**, mirrors `creature-data-schema.md`'s `crack_stage_count` range (art-bible §8.4) | More stages = more granular "about to break" read; bound by the same art budget as the schema's crack-stage field. |
| Telegraph wind-up `duration_ms_override` floor | `Clip.duration_ms_override` (telegraph clips only) | Rig-level technical floor: 1 rendered frame (~16.6ms at 60 FPS, `.claude/docs/technical-preferences.md`) | **Free at the rig layer** — the game-design-level minimum (anti-strobe, WCAG 2.3.1) is owned by art-bible §7.7 / `creature-ai-telegraph-system`, not this GDD | This rig will faithfully render any duration ≥ one frame; the higher, gameplay-meaningful floor is a different system's tuning knob, not duplicated here. |
| Work-loop `hold_frame` index (per role, per clip) | `Clip.hold_frame` | Any valid frame in `[0, frame_count-1]`; art-bible §5.3 only requires it be "the pre-work-tick frame" (e.g. before the crafter's flourish lands, before the producer exhales) | **Free**, authored per creature/role clip | Which exact frame reads as "stalled, waiting to deliver" — a content-authoring choice per clip, not a global constant. |

**MVP scope note**: this document specifies the rig *mechanism* in full generality; it does not
author any specific creature's poses, clips, or curve assignments. Per art-bible §8.4's budget,
each MVP creature is expected to consume ~25–40 unique authored frames (bosses ~50–60) regardless
of how many `Clip`s reference that shared pose library — the mechanism's whole purpose is keeping
that budget flat as the creature roster and state-matrix (hostile/bound × Healthy/Blocked/Starved ×
idle/telegraph/attack/hit/break) both grow.

## 8. Acceptance Criteria

1. Given a hostile `Clip` and a bound `Clip` built from an identical `keys` list (same `pose_id`s,
   same `at_frame` values) differing only in assigned `easing_curve_id`, evaluating both at the same
   `current_frame` produces different resolved bone transforms for at least one bone, while neither
   clip references any `PoseFrame` beyond the shared library — verified against §4 Formula 3's
   worked example (17.6° hostile vs. 38.7° bound at frame 2 of the rest→peak segment).
2. Given a Healthy work-loop clip with `hold_frame` set, and a caller transition from
   `work_state = healthy` to `work_state = blocked` (setting `is_held = true`), the evaluated
   `current_frame` freezes at `hold_frame` and does not advance for any further `elapsed_ms`, and no
   new `PoseFrame` or texture asset is loaded to enter the Blocked state — confirming Blocked costs
   zero new frames per art-bible §8.4 (§4 Formula 2's hold worked example).
3. Simulating 100 simultaneously-animating creatures (art-bible §8.8's soft planning ceiling)
   sharing one region atlas and one glyph atlas, submitted per §3.16's three-block contract, produces
   at most 3 texture-switch boundaries for that creature-pass (one per block: body, decal, glyph) —
   verified by counting `Texture2D` bind/switch events across the pass in a test harness, confirmed
   independent of creature count by re-running at 10, 50, and 100 creatures and observing the same
   switch count.
4. For every `Part` with `rotation_mode = angle_snapped`, stepping a clip through its full duration
   at 60 FPS ticks, no `Draw()` call for that part is ever submitted with a non-zero rotation
   parameter — verified by asserting `rotation == 0` on every such call in a test harness. (This is
   the direct proof that no rotated part can show non-integer-angle blur: all visible rotation for
   `angle_snapped` parts is pre-baked into the swapped texture, never applied at draw time.) **This
   criterion must pass on the Attacker strike-and-recover spike (see Prototype Spike Required
   callout) before any other creature is committed to this rig.**
5. Loading a `Clip` whose `easing_curve_id` references a nonexistent Curve Library entry is
   rejected at content-load/validation time (Edge Case 1; §3.17 invariant 6) — never silently
   substituted at runtime.
6. Requesting a decal for a `(part_id, crack_stage)` pair that is within the valid range per
   `PartDefinition.crack_stage_count` but has no bound `DecalAsset` renders the base part with no
   decal and logs a content-integrity warning — never renders a wrong-stage decal, never throws
   (Edge Case 2).
7. Given two `AdditiveLayer`s both targeting bone `X`'s scale, evaluating the final pose for a frame
   where both are active produces the multiplicative composition of both factors onto the base
   clip's scale (§4 Formula 4's worked example: `1.0 × 1.05 × 1.02 = 1.071`) — neither layer
   overrides the other.
8. Destroying a `CreatureInstance` mid-clip removes its rig from the draw-submission list for the
   very next frame, with zero `Draw()` calls referencing that instance afterward (Edge Case 4).
9. Transitioning a `CreatureInstance.bind_state` mid-clip (either direction) hard-cuts to the new
   state's clip at its own frame 0 with no interpolated/blended cross-fade frame ever rendered —
   verified by inspecting the very next frame's resolved pose and confirming it matches the new
   clip's frame-0 pose exactly, with no trace of the outgoing clip's transform values (Edge Case 5).
10. Formula 1's worked example reproduces exactly: `angle_range_deg = [-30, 150]`, `V = 7`,
    `θ = 72°` → `variant_index = 3` (`variant_angle = 60°`); and the tie-break example, `θ = 15°` →
    `variant_index = 2` (`variant_angle = 30°`).
11. Formula 2's worked examples reproduce exactly: `playback_fps = 12`, `frame_count = 8`,
    `loop = true`, `elapsed_ms = 950` → `current_frame = 3`; and the `duration_ms_override = 1800`,
    `frame_count = 60`, `elapsed_ms = 900` case → `current_frame = 30`.
12. Formula 3's worked example reproduces exactly: the rest→peak segment (frames 0–5, rotation
    0°→110°) evaluated at `f = 2` yields `17.6°` under `quad_ease_in` and `38.7°` under
    `smooth_ease_in_out`.
13. A `CreatureTemplate` with `tier = boss` (5–8 parts, `creature-data-schema.md` invariant 2) loads
    a `CreatureRig` whose `parts.length` (non-decorative) matches the template's part count exactly
    — confirming no hard-coded part count exists in the rig's data model (Edge Case 7).
14. Every non-`null` `Part.part_id` in a loaded rig matches an existing `part_id` on the current
    `CreatureTemplate.parts`; a rig referencing a nonexistent template part is rejected at load time,
    never silently accepted (Edge Case 8; §3.17 invariant 1).
15. A `Clip` with `duration_ms_override` set plays back over exactly that many milliseconds of
    wall-clock time regardless of its `frame_count`/`playback_fps` values — verified by comparing two
    clips with identical `frame_count`/`playback_fps` but different `duration_ms_override` values and
    confirming they complete (reach their final frame) at different elapsed times, proportional to
    their override values.
