# Input Targeting System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Ambiguities resolved using `design/gdd/game-concept.md`, `design/art/art-bible.md` (§3.5, 5.1, 7.5, 7.7), and `.claude/docs/technical-preferences.md` as authority; every resolution is flagged inline as an assumption. |
| **Priority / Tier** | MVP — Core layer (`design/gdd/systems-index.md` #5) |
| **Depends On** | `creature-data-schema` (existing) + `animation-rig-system` (newly identified runtime dependency, see Assumption A1 and §6) |
| **Depended On By** | `accessibility-settings-system`, `combat-encounter-system` (see §6) |

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | `animation-rig-system` is treated as supplying, every frame, a per-part current transform — `{ part_id, position, rotation, scale }` in virtual-canvas space — keyed by `part_id`. This GDD specifies the minimal contract it needs; `animation-rig-system`'s own GDD owns the authoritative shape. `systems-index.md`'s dependency map lists `input-targeting-system` as depending only on `creature-data-schema`, omitting this. This GDD's own edit to `systems-index.md` (see §6) corrects that gap rather than silently working around it. | Hit-testing and the cycle-order sort key both require a part's *current, animated* position/rotation/scale, which only the animation rig produces. Without this contract, hit-testing has no way to know where a part actually is on screen this frame. |
| A2 | All targeting math (hit-testing, padding, magnetism, angular sort) operates in **virtual-canvas space** (the 480×270 arena canvas, art-bible §8.2), not final display pixels. Raw OS cursor coordinates are converted into this space by a coordinate transform owned outside this system (viewport→virtual-canvas, accounting for the integer scale factor and any letterbox offset) before any rule in this document applies. The arena camera does not move during an encounter (art-bible §6.2), so virtual-canvas space and world space are equivalent for the duration of one encounter. | Keeps every tuning number in this document resolution-independent (the same `padding_px = 8` means the same thing at 1080p ×4 scale and 4K ×8 scale) and anchors the design to the actual rendering pipeline locked in the art bible rather than an assumed generic screen space. |
| A3 | The creature's angular reference center, used only for cycle ordering (§4, Formula 3), is the current-frame position of the part where `is_core = true` (`creature-data-schema` §3.3). | Reuses a field the schema already guarantees is unique per template (invariant 1) instead of inventing a new "creature center" concept — no new field needed anywhere. |
| A4 | All tie-breaking — both exact-hit collisions and padded-hitbox overlaps — resolves, as a last resort, via ascending `PartDefinition.break_priority` (`creature-data-schema` §3.3). | `break_priority` is already guaranteed unique within a template (invariant 3), so reusing it as a final tie-breaker guarantees the resolution chain always terminates in exactly one winner, with no new field. |
| A5 | The **valid target set** — what cycle-and-confirm cycles through, and what mouse hit-testing is allowed to resolve to — is **every non-broken, `can_be_vulnerable = true` part**, not only the parts currently listed in `hostile_state.vulnerable_parts`. | `vulnerable_parts` can legitimately be empty (no current opening, per `creature-data-schema` §3.6). If cycling only worked while a window was open, a keyboard/gamepad-only player would have nothing to pre-aim during closed windows while a mouse player can freely hover anywhere at any time — breaking the parity the two input paths are required to have. Vulnerability instead becomes the *first sort priority* (Formula 3), not an eligibility filter. |
| A6 | Aim-assist/target-magnetism adjusts only the **internal effective hit-test position** used by this system's own math. It never moves, warps, or otherwise touches the OS mouse cursor's rendered position. | Avoids fighting the OS cursor (jarring, and out of this system's control on some platforms) and keeps magnetism architecturally identical to hitbox padding — both are invisible hit-test generosity layers, not visual changes. |
| A7 | The cycle-and-confirm selection ring (art-bible §7.7) is rendered **only** while the most recent update to the shared selection register came from the cycle path, never from mouse hover. | The mouse's own cursor sprite already gives positional feedback; continuously drawing a ring under a moving mouse cursor would add competing motion the art bible's hero-shape hierarchy (§3.5) works hard to avoid. Cycle-and-confirm, by contrast, is a two-step interaction (browse, then confirm) that genuinely needs a persistent "here's where you currently are" indicator. |

---

## 1. Overview

The input targeting system answers exactly one question, every frame: **which of a hostile
creature's targetable body parts, if any, is the player currently aiming at, and did they just
commit to attacking it?** It implements two fully first-class input paths — direct point-and-click
precision targeting for mouse, and a discrete cycle-and-confirm model for gamepad and keyboard —
feeding a single shared selection state that the renderer, and eventually
`combat-encounter-system`, both consume identically regardless of which path produced it. It
defines the hit-testing method that turns a screen click into a specific `part_id`, the mandatory
hitbox padding that makes small or adjacent parts fairly clickable, an optional aim-assist setting,
and the cycle order and selection-rendering rules the gamepad/keyboard path needs. It explicitly
does **not** define damage, hit resolution, ability selection, or vulnerability-window timing —
those remain `combat-encounter-system`'s and `creature-ai-telegraph-system`'s jobs; this system
only ever answers "which part."

## 2. Player Fantasy

> **Reading the fight correctly and executing on that read precisely is the entire skill — and
> that promise holds no matter what's in the player's hands.**

Pillar 1 (Precision Over Reflexes) states that mastery is expressed through pattern recognition
and high-value execution, not movement or twitch reflexes. This system is where that promise is
either kept or broken, because targeting is the single point of contact between "I correctly read
which part is exposed" and "I acted on it." Concretely, this system guarantees:

- **The skill is reading, not pointing.** A player who has correctly identified the weak point
  should feel like the game rewarded their read the instant they act — a clean, immediate click on
  a part that clearly reads as breakable, or a short, predictable cycle to the part they already
  recognized as the opening. Neither path asks the player to fight the input device to convert a
  correct read into a correct action; the generous hitbox padding and the vulnerability-first cycle
  order both exist specifically so that "I knew what to hit" and "I hit it" are the same moment,
  not two separate skill checks.
- **No input device is a lesser path to mastery.** Mouse is the primary path — direct,
  immediate, satisfying to execute quickly across a fast-moving fight. Cycle-and-confirm is not a
  degraded fallback bolted on for completeness; it is a genuinely different, equally valid way to
  express the same read, built to sidestep the one thing that *would* compromise Pillar 1 on a
  gamepad — a low-precision analog stick standing in for a mouse and quietly turning "click the
  weak point you read" into "steer a cursor onto a small target." A keyboard-only player, or a
  player with a gamepad and no mouse, is never asked to accept a worse version of the core loop.
- **Precision is never punished by the input device's own imprecision.** A player with tremor, a
  worn mouse, or a controller with drift should not lose fights to hardware, and a player who
  correctly identifies a small part should not need surgical mouse control to prove it. The
  mandatory hitbox padding, and the optional aim-assist layered on top of it, exist so that a
  correct read consistently produces a correct outcome — motor precision is never allowed to
  quietly become a second, unadvertised skill test riding alongside the intended one.
- **The moment of commitment is legible.** Whether the player is watching their own cursor
  (mouse) or a selection ring advance around the creature (cycle-and-confirm), they always know
  exactly what they are about to attack before they attack it — there is no ambiguity, no
  "did that register," no silent miss against a gap between two parts.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This system defines **target selection only**. It is explicitly silent on:

- Whether an attack that lands on a given part actually deals damage, how much, or whether the
  part was "vulnerable" at the moment of the hit — that resolution math belongs entirely to
  `combat-encounter-system`.
- Which of the 3 equipped abilities, the ultimate, or the auto-firing basic attack is being used
  when a target is confirmed — ability selection is a separate input concern owned elsewhere
  (`resonance-weaving-system` / `combat-encounter-system`). "Confirming a target" in this document
  means "committing a `part_id` as the target for whatever attack/ability action is concurrently in
  flight," not "choosing which action to use."
- Telegraph timing, vulnerability window duration, or which parts are currently vulnerable —
  entirely `creature-ai-telegraph-system`'s job. This system only *reads* `vulnerable_parts` as one
  input to the cycle-order sort key (§4, Formula 3); it never writes to it.

### 3.1 Exposed Interface (Conceptual)

This system exposes one shared selection register and one commit event, consumed identically by
the renderer and (eventually) `combat-encounter-system` regardless of which input path produced
them:

| Field / Event | Type | Description |
|---|---|---|
| `selected_part_id` | string, nullable | The `part_id` (§3.3, `creature-data-schema`) that would be attacked if the player committed right now. Updated continuously by the mouse path (hover), or on cycle-input events by the cycle-and-confirm path (§3.4). `null` when no valid target exists (§5, Edge Case #2) or when the mouse is not over any hittable region and no cycle selection is active. |
| `selection_render_source` | enum `none` \| `cycle` | Rendering gate (§3.11, Assumption A7). `cycle` only while the most recent update to `selected_part_id` came from the cycle-and-confirm path; `none` otherwise, including immediately after any mouse-hover update. |
| `TargetConfirmed` (event) | `{ part_id: string, source: "mouse" \| "cycle" }` | Fired the instant the player commits to a target: a mouse click that resolves to a non-null part, or a confirm-button press while `selected_part_id` is non-null via the cycle path. `combat-encounter-system` subscribes to this event as the trigger for its own attack/ability resolution. Never fired when the resolving target is `null` (§5, Edge Cases #2, #10). |

### 3.2 Valid Target Set

Both input paths draw from the same set, recomputed fresh on every hit-test call or cycle-input
event — **never cached stale across frames** (this is what makes Edge Case #1, a part breaking
mid-selection, resolve correctly without special-case code):

> **Valid Target Set** = { `part_id` : `PartState.current_stage < PartDefinition.crack_stage_count
> + 1` (not broken, `creature-data-schema` §3.6/§4 Formula 3) **AND** `PartDefinition.can_be_vulnerable
> = true` }

Per Assumption A5, this is **not** filtered down to `hostile_state.vulnerable_parts` — a part not
currently in its vulnerability window is still a legal target to select (it simply won't resolve
to a beneficial hit once `combat-encounter-system` processes the confirmed attack; that resolution
is entirely out of this system's scope).

### 3.3 Mouse Path — Direct Point-and-Click

1. Every frame the mouse moves (or the creature's parts move under a stationary cursor, e.g. during
   idle sway), the system runs the hit-test (§3.5) against the current effective cursor position
   (§3.7 applies magnetism first, if enabled) and the Valid Target Set (§3.2).
2. The hit-test result (a `part_id` or `null`) is written to `selected_part_id` immediately, and
   `selection_render_source` is set to `none`.
3. A mouse click re-runs the hit-test at the moment of the click and, if it resolves to a non-null
   `part_id`, fires `TargetConfirmed { part_id, source: "mouse" }` in the same instant.
   Selection and commitment are **atomic** for mouse — there is no separate "confirm" step; a click
   is a deliberate, single, precise action and does not need one.
4. A click that resolves to `null` (a true miss, §5 Edge Case #5) fires no event and is silently
   discarded.

### 3.4 Cycle-and-Confirm Path — Gamepad and Keyboard

1. Two discrete cycle inputs (cycle-next, cycle-prev — bound to bumpers/D-pad on gamepad, bindable
   keys on keyboard; exact key/button bindings are `accessibility-settings-system`'s concern) and
   one confirm input drive this path. **No analog stick input is ever consumed by this path** — see
   §5 and §8 for the explicit ban.
2. On a cycle-next or cycle-prev press, the system recomputes the Valid Target Set (§3.2) fresh,
   sorts it by the cycle-order sort key (§4, Formula 3), locates the currently selected part's
   position in that fresh ordering (falling back per §5 Edge Case #1 if it's no longer present),
   and moves to the next or previous entry.
3. **Cycling wraps in both directions** — past the last entry on cycle-next returns to the first;
   past the first entry on cycle-prev returns to the last. A bounded, non-wrapping list would create
   a dead input at the ends of a 3–8-part set for no benefit.
4. Holding a cycle input auto-repeats after `cycle_initial_delay_ms`, then every
   `cycle_repeat_interval_ms` thereafter (§7, Tuning Knobs) — standard press-and-hold list
   navigation, never firing faster than the repeat interval.
5. Every cycle-next/cycle-prev update sets `selected_part_id` to the new part and
   `selection_render_source` to `cycle`.
6. Pressing confirm while `selected_part_id` is non-null fires `TargetConfirmed { part_id,
   source: "cycle" }`. Pressing confirm while `selected_part_id` is `null` is a no-op (§5 Edge Case
   #10).

### 3.5 Hit-Testing Method

**Recommendation: per-pixel alpha-mask hit-testing, in each part's own local texture space, tested
against a precomputed CPU-side opacity bitmap — not polygon hit regions, not rectangular bounds.**

Justification against the three factors this decision must be judged on:

- **(a) Accuracy for irregular pixel-art parts.** Per-pixel testing matches the artist-authored
  silhouette exactly — branching Nature outgrowths, jagged Shadow bite-notches, faceted Mind
  edges (art-bible §3.1) — with zero approximation error. Polygon hit regions would need many
  vertices to approximate those organic silhouettes without visible inaccuracy, and rectangular
  bounds fail specifically *because* art-bible §3.1 deliberately varies part scale and proportion
  for legibility: adjacent parts of different size/shape would have constantly overlapping
  rectangular bounds, directly defeating the "read the shape, count the zones" design intent the
  whole silhouette system is built on.
- **(b) MonoGame capabilities.** MonoGame ships no physics or collision system
  (`.claude/docs/technical-preferences.md`, Physics row) — every option here is hand-rolled
  regardless. Per-pixel testing needs no additional dependency; it reuses the alpha channel that
  must exist for rendering anyway. Polygon hit-testing would require either an added geometry
  dependency or hand-authored polygons *per part, per angle-snap rotation variant*
  (art-bible §8.5) — multiplying authoring effort by the same angle-snap factor that already
  multiplies the rig's sprite count, a real content cost against an already-tight ~125–160 MB asset
  budget (art-bible §8, headline finding).
- **(c) Composing with mandatory hitbox padding.** A per-pixel base method composes cleanly with a
  padding layer: padding becomes a bounded local search for the nearest opaque texel (§3.6), itself
  just another per-pixel query on data that already exists. Polygon regions could technically
  support padding via edge offsetting, but reintroduce (a)'s accuracy tension at the padded boundary
  too. Rectangular bounds' padding would only worsen their already-poor overlap behavior.

**Mechanism**:

1. **Precomputation (asset-import time, not runtime)**: for every part texture, at every
   angle-snap rotation variant, precompute a compact CPU-side boolean opacity bitmap (opaque if
   alpha exceeds a fixed threshold) and cache it alongside the texture asset. Runtime hit-testing
   only ever reads this cached array — it never calls a texture GPU readback at click time.
2. **Local-space transform**: for a candidate part, invert that part's current-frame transform
   (position, rotation, scale — from `animation-rig-system`, Assumption A1) to convert the
   virtual-canvas click point into the part's local texture space.
3. **Phase 1 — exact hit**: look up the opacity bitmap at that local-space point for every part in
   the Valid Target Set (§3.2). If exactly one part is opaque there, that is the hit — resolved
   immediately, padding never consulted. If more than one Valid Target Set part is opaque at that
   exact point (intentional authored overlap, e.g. a claw drawn slightly over the body), resolve via
   the tie-break chain in §3.6 skipping straight to the distance-tied branch (all distances are 0).
   If zero parts are opaque there, proceed to Phase 2.
4. **Phase 2 — padded fallback** (§3.6): only entered on an exact miss.
5. *(Optimization, non-normative)*: a broad-phase pass may skip any part whose current-frame
   bounding box, expanded by its own `padding_px`, does not contain the click point — a standard
   broad/narrow-phase split that reduces per-click cost without changing the result.

### 3.6 Hitbox Padding (Mandatory) and Overlap Resolution

Padding is **not optional and not a player-facing setting** — per art-bible §7.7 it is required
regardless of any other decision, and is a global tuning constant, not a per-player toggle (contrast
with aim-assist, §3.7, which *is* a toggle).

1. For every part in the Valid Target Set, compute `padding_px` via Formula 1 (§4) from that part's
   current-frame size.
2. In the part's local texture space, search a bounding box of side `2 × padding_px` (texels)
   around the click point in the precomputed opacity bitmap, and take the minimum distance to any
   opaque texel found. This bounds the search cost by `padding_max_px` regardless of part or
   creature size — no full distance-transform field is precomputed.
3. Discard any part whose minimum distance exceeds its own `padding_px`.
4. **Overlap / tie-break resolution** — applied whenever more than one part remains a candidate
   (a padded-region overlap, or an exact-hit collision from §3.5 step 3), in this fixed order,
   each tier only consulted if the previous tier is tied:
   1. **Smallest distance wins** — the part whose nearest opaque texel is closer to the click point.
   2. **Currently-vulnerable part wins** — if distances tie, a part in `hostile_state.vulnerable_parts`
      beats one that isn't; rewarding a precise read on the higher-value target is more aligned with
      Pillar 1 than an arbitrary tie-break.
   3. **Smaller part wins** — if still tied, the part with the smaller current-frame screen-space
      bounding area (Fitts's Law: the smaller target needs the assist more).
   4. **Lower `break_priority` wins** — final, always-terminating tie-break (Assumption A4); unique
      per template, so this tier never itself ties.
5. If no candidate remains within its own `padding_px`, the click is a total miss — identical
   handling to clicking open arena background (§5, Edge Case #5).

### 3.7 Aim-Assist / Target Magnetism (Optional, Mouse-Only)

A toggleable accessibility setting, owned/persisted by `accessibility-settings-system`, applied
only within this system's mouse path (§3.3). It has **no effect** on cycle-and-confirm (§5, Edge
Case #8) — that path doesn't point at anything, so there is nothing for magnetism to assist.

1. Default **off** (`magnetism_strength = 0.0`). Padding alone already guarantees baseline
   completability and fairness; magnetism is a genuinely optional comfort layer on top, not a
   requirement.
2. When enabled, every frame the mouse path computes an `effective_click_pos` (Formula 2, §4) by
   pulling the raw cursor position toward the nearest Valid Target Set part's current-frame anchor,
   only when within `magnetism_radius_px`, scaled by `magnetism_strength`.
3. `effective_click_pos` — never the raw cursor, and never the rendered OS cursor sprite
   (Assumption A6) — is what feeds the hit-test in §3.5/§3.6 whenever magnetism is enabled.

### 3.8 Frame Timing / Transform Freshness

Hit-testing always uses the **current** frame's post-animation-update transforms. Within a single
frame's update order, `animation-rig-system`'s transform update runs before this system's hit-test
step, so hit-testing never reads a stale (previous-frame) part position — even during fast motion
(e.g. an Attacker's wide strike-and-recover arc, art-bible §8.5). A click event that physically
arrives between two simulation frames (an OS-level input message) is deferred to, and tested
against, the next simulation frame's freshly computed transforms — it is never retroactively
tested against the frame that had already finished when the click occurred.

### 3.9 Input Device Coexistence

This system stores **no persistent "current input device" mode**. Mouse hover/click events and
cycle/confirm events are two independent, always-live sources feeding one shared
`selected_part_id` register (§3.1); whichever fires last simply overwrites the register — no
special "device switch" transition, event, or state is needed or fired. A player may freely
alternate mouse and gamepad/keyboard input within the same encounter, even within the same second,
with no additional handling required anywhere in this system.

### 3.10 Hostile-Only Operation

Per `creature-data-schema` §3.9 invariant 5, `bind_state = bound` ⟺ `hostile_state` is `null`. This
system's entire data dependency — `PartState.current_stage`, `hostile_state.vulnerable_parts` — lives
inside `hostile_state`. **This system has no function against a bound creature**: there is no
combat encounter, and therefore no combat targeting UI, ever routed to a bound-creature context in
the first place (bound creatures are viewed only in the Idle Region View or Creature Roster,
art-bible §5.4, both entirely different UI surfaces). If this system were ever hypothetically
queried against an instance with `bind_state = bound`, it fails closed: the Valid Target Set is
empty, `selected_part_id` is always `null`, and all input (mouse and cycle-and-confirm alike) is a
no-op.

### 3.11 Rendering Contract

This system exposes `selected_part_id` and `selection_render_source` (§3.1) for the renderer to
draw the selection ring required by art-bible §7.7. Constraints inherited directly from the art
bible, restated here as binding requirements on how that data may be visualized:

- The selection ring renders **only** when `selection_render_source = cycle` (Assumption A7) — never
  during mouse hover.
- It must be **shape-distinct** from the vulnerability glyph's open-ring container (art-bible §3.2)
  — a different container shape, not a recolor of the same one; this is new visual grammar
  explicitly owed to `/ux-design` (art-bible's Open Items table) and this system does not design the
  ring's exact shape, only guarantees the data (`selected_part_id`) it needs to be drawn from.
- It must sit at or below the "Source glyph / seams" contrast tier in art-bible §3.5's hero-shape
  hierarchy — present and inspectable, never brighter, never more animated, and never exceeding the
  vulnerability glyph's peak brightness. The vulnerability glyph remains the loudest shape in frame
  at all times, including while a target is cycle-selected.

---

## 4. Formulas

### Formula 1 — Hitbox Padding by Part Size

```
padding_px(part) = clamp( padding_base_px + padding_inverse_scalar × max(0, reference_part_size_px − part_size_px),
                           padding_min_px, padding_max_px )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `part_size_px` | float | > 0, virtual-canvas px | The diagonal length (`√(w² + h²)`) of the part's **authored local** bounding box (width × height in local texture space), scaled by the part's current-frame uniform scale factor. Deliberately **not** the rotated/axis-aligned screen-space bounding box, so padding stays stable across animation frames rather than fluctuating as a part rotates. |
| `reference_part_size_px` | float | > 0 (default 40, virtual-canvas px) | Tuning constant: the size at or above which a part is considered "easy enough to hit" and receives only the flat baseline padding. |
| `padding_base_px` | float | ≥ 0 (default 3) | Baseline padding every part in the Valid Target Set receives regardless of size. |
| `padding_inverse_scalar` | float | ≥ 0 (default 0.25) | Extra padding pixels added per pixel a part's size falls below `reference_part_size_px`. |
| `padding_min_px` / `padding_max_px` | float | defaults 3 / 14 | Clamp bounds preventing degenerate zero padding or runaway over-padding that would swallow neighboring parts. |
| `padding_px(part)` | float | `[padding_min_px, padding_max_px]`, virtual-canvas px | Output. The radius, in all directions, by which this part's hit region is expanded beyond its exact opaque silhouette — consulted only in the Phase 2 padded fallback (§3.6). |

**Output range**: bounded on both sides by the tuning clamp — never below `padding_min_px` (every
part gets at least the baseline assist) and never above `padding_max_px` (padding can never grow
large enough to make overlap resolution meaningless).

**Worked example** (defaults, standard-tier creature, native/virtual-canvas ~120px tall): a small
"Left Claw" part with `part_size_px = 18`:

```
padding_px = clamp(3 + 0.25 × max(0, 40 − 18), 3, 14)
           = clamp(3 + 0.25 × 22, 3, 14)
           = clamp(3 + 5.5, 3, 14)
           = clamp(8.5, 3, 14)
           = 8.5 px
```

A larger "Core" part with `part_size_px = 55` (already above the 40px reference):

```
padding_px = clamp(3 + 0.25 × max(0, 40 − 55), 3, 14)
           = clamp(3 + 0.25 × 0, 3, 14)
           = clamp(3, 3, 14)
           = 3 px  (baseline only — no size bonus)
```

Small parts receive proportionally more assist (padding nearly half the part's own size in the
Left Claw example); larger parts fall back to the flat floor.

### Formula 2 — Aim-Assist / Target Magnetism

```
effective_click_pos = raw_cursor_pos + (nearest_target_anchor_pos − raw_cursor_pos) × magnetism_strength
                       (applied only if distance(raw_cursor_pos, nearest_target_anchor_pos) ≤ magnetism_radius_px)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `raw_cursor_pos` | Vector2 | virtual-canvas px | The actual mouse cursor position this frame (after the viewport→virtual-canvas conversion, Assumption A2). |
| `nearest_target_anchor_pos` | Vector2 | virtual-canvas px | Current-frame anchor/pivot position of the nearest Valid Target Set part (§3.2) to `raw_cursor_pos`. |
| `distance(...)` | float | ≥ 0, virtual-canvas px | Euclidean distance between the two points above. |
| `magnetism_radius_px` | float | > 0 (default 25) | Maximum distance at which magnetism can activate at all. Beyond this, `effective_click_pos = raw_cursor_pos` unmodified. |
| `magnetism_strength` | float | `[0.0, 1.0]`, default 0.0 (off) | Player-facing accessibility slider (`accessibility-settings-system` owns the UI/persistence). 0 = no pull ever; 1 = full snap to the anchor whenever within radius. |
| `effective_click_pos` | Vector2 | virtual-canvas px, bounded | Output. Feeds the hit-test (§3.5/§3.6) in place of `raw_cursor_pos` whenever magnetism is enabled. Always lies on the line segment between `raw_cursor_pos` and `nearest_target_anchor_pos` — never overshoots. |

**Output range**: a linear interpolation between two known points, so the result is always bounded
by that segment — it can never move the effective position further from `raw_cursor_pos` than
`nearest_target_anchor_pos` itself.

**Worked example**: `magnetism_strength = 0.5`, `magnetism_radius_px = 25`,
`raw_cursor_pos = (150, 100)`, `nearest_target_anchor_pos = (165, 85)`:

```
distance = √((165−150)² + (85−100)²) = √(225 + 225) = √450 ≈ 21.2   (≤ 25 → magnetism applies)

effective_click_pos = (150, 100) + ((165, 85) − (150, 100)) × 0.5
                     = (150, 100) + (15, −15) × 0.5
                     = (150, 100) + (7.5, −7.5)
                     = (157.5, 92.5)
```

### Formula 3 — Cycle-Order Sort Key

```
sort_key(part) = vulnerability_rank(part) × 2π + angular_position(part)

vulnerability_rank(part) = 0  if part_id ∈ hostile_state.vulnerable_parts, else 1
angular_position(part)   = ( atan2( dx, −dy ) + 2π ) mod 2π,   where dx = part_anchor.x − core_anchor.x,
                                                                       dy = part_anchor.y − core_anchor.y
```

(`angular_position` is measured clockwise from screen-space "up," i.e. 12 o'clock = 0 rad.)

| Symbol | Type | Range | Description |
|---|---|---|---|
| `part_anchor` | Vector2 | virtual-canvas px | Current-frame pivot position of the candidate part (from `animation-rig-system`, Assumption A1). |
| `core_anchor` | Vector2 | virtual-canvas px | Current-frame pivot position of the part where `is_core = true` (`creature-data-schema` §3.3, Assumption A3) — the creature's angular reference center. |
| `vulnerability_rank` | int | `{0, 1}` | 0 if the part is currently in `hostile_state.vulnerable_parts` (`creature-data-schema` §3.6), else 1. Guarantees every vulnerable part sorts before every non-vulnerable part. |
| `angular_position` | float | `[0, 2π)` rad | The part's bearing from `core_anchor`, clockwise from "up." |
| `sort_key(part)` | float | `[0, 4π)` | Output. Ascending sort order for cycling: `[0, 2π)` = vulnerable parts by angle, `[2π, 4π)` = non-vulnerable parts by angle. Ties (identical `sort_key`) resolved by ascending `break_priority` (Assumption A4). |

**Output range**: `[0, 4π) ≈ [0, 12.566)`. The 2π offset between the two rank bands guarantees no
non-vulnerable part's `sort_key` can ever be lower than any vulnerable part's, regardless of angle.

**Worked example**: a standard creature, `core_anchor = (60, 40)` (virtual-canvas px),
`hostile_state.vulnerable_parts = ["left_claw"]`, four non-broken `can_be_vulnerable` parts:

| `part_id` | anchor | `dx, dy` | angle (°) | angle (rad) | `vulnerability_rank` | `sort_key` |
|---|---|---|---|---|---|---|
| `left_claw` | (45, 40) | −15, 0 | 270° (9 o'clock) | 4.712 | 0 (vulnerable) | 0 × 2π + 4.712 = **4.712** |
| `head` | (60, 20) | 0, −20 | 0° (12 o'clock) | 0.000 | 1 | 1 × 2π + 0.000 = **6.283** |
| `right_claw` | (75, 40) | 15, 0 | 90° (3 o'clock) | 1.571 | 1 | 1 × 2π + 1.571 = **7.854** |
| `tail` | (60, 60) | 0, 20 | 180° (6 o'clock) | 3.142 | 1 | 1 × 2π + 3.142 = **9.425** |

Ascending sort → cycle order: **`left_claw` → `head` → `right_claw` → `tail`** → wraps back to
`left_claw`. Note `left_claw` cycles first despite sitting at 9 o'clock (not "up") — vulnerability
outranks angle, exactly as intended: the fastest cycle path always reaches the currently-open
opening first.

---

## 5. Edge Cases

1. **The currently-selected part breaks mid-selection (cycle path).** `selected_part_id` is
   revalidated against a freshly recomputed Valid Target Set on every cycle-input press *and*
   immediately on the frame the break occurs — it is never left pointing at a now-broken part.
   When the current selection is no longer valid, the system reassigns `selected_part_id` to the
   entry with the **lowest `sort_key`** (Formula 3) in the fresh valid set — i.e. the same rule
   cycling itself uses, so no separate fallback logic is needed. The ring visibly jumps to the
   new highest-priority target the same frame the break lands.
2. **All targetable parts break (no valid target exists at all).** This is a degenerate but
   representable state (e.g. every `can_be_vulnerable = true` part is broken while a
   `can_be_vulnerable = false` core remains the only unbroken part) — distinct from the normal case
   where breaking the core ends the encounter (`creature-data-schema` §5, Edge Case #2).
   `selected_part_id` becomes `null`, `selection_render_source` becomes `none`, cycle-next/prev are
   no-ops (nothing to move to), and mouse hit-testing resolves to a miss everywhere on the creature
   (both hit-test phases find zero candidates). No error state; input is simply inert until a valid
   target exists again or the encounter ends.
3. **Two padded hitboxes overlap, including perfectly.** Resolved by the four-tier deterministic
   chain in §3.6 step 4 (nearest distance → currently-vulnerable → smaller part → lower
   `break_priority`). Because `break_priority` is guaranteed unique per template
   (`creature-data-schema` invariant 3), the chain is guaranteed to terminate in exactly one winner
   even in a perfect-overlap case where the first three tiers all tie.
4. **The player switches, or freely mixes, input devices mid-encounter.** No special handling
   exists or is needed (§3.9) — both paths write to the same `selected_part_id` register, and
   whichever event fires most recently simply wins. No "device switched" event is fired.
5. **A click lands inside the creature's outer contour but in no part's padded hitbox (a gap).**
   Resolves as a total miss — identical to clicking open arena background: no `TargetConfirmed`
   event fires, `selected_part_id` becomes `null` for that hover sample. This is expected to be rare
   in practice; `padding_min_px` should be tuned to exceed the rendered seam-line's screen-space
   width (§7) so genuine part-to-part seam gaps are essentially always covered by padding, but a
   large enough gap (e.g. deep in a concave silhouette notch) can still legitimately produce a
   miss, and this system treats that as ordinary, not an error.
6. **The creature is mid-animation; a part has moved since the last frame.** Hit-testing always
   uses the *current* frame's post-animation-update transform, never the previous frame's (§3.8).
   A click event arriving between simulation frames is tested against the next frame's freshly
   computed transforms, never retroactively against the frame that had already elapsed.
7. **The system is queried against a bound creature (`bind_state = bound`).** Fails closed
   (§3.10): `hostile_state` is `null` per `creature-data-schema` invariant 5, so there is no
   `PartState`/`vulnerable_parts` data to test against. The Valid Target Set is empty by
   construction; all input is a no-op. In practice this never occurs because bound creatures are
   never presented in a combat-targeting context in the first place.
8. **Aim-assist is enabled while the player is using cycle-and-confirm exclusively (no mouse).**
   `magnetism_strength` has zero effect on the cycle path — magnetism only ever modifies
   `effective_click_pos` inside the mouse path's hit-test (§3.7); cycling has no pointer position for
   it to act on.
9. **A part's current-frame bounding size is degenerate or near-zero** (e.g. viewed edge-on at a
   particular angle-snap rotation variant). `padding_px` still computes correctly via Formula 1 —
   a `part_size_px` of 0 clamps to `padding_max_px`, the maximum available bonus — so a
   momentarily tiny part becomes *more* generously padded, never unselectable. If the exact alpha
   mask is empty for that frame, Phase 1 (§3.5) always misses it and Phase 2 padding remains the
   only path to select it that frame.
10. **Confirm is pressed while `selected_part_id` is `null`.** No-op — no `TargetConfirmed` event
    fires, no error.
11. **Rapid or held cycle input.** Bounded by `cycle_initial_delay_ms` and
    `cycle_repeat_interval_ms` (§7) — auto-repeat never fires faster than the configured interval,
    regardless of how the input device reports the held state.
12. **The Valid Target Set has exactly one entry** (e.g. only the core remains unbroken and
    `can_be_vulnerable`). Cycle-next and cycle-prev both wrap to the same single entry — effectively
    a no-op that leaves the selection ring fixed on that one part, which is the correct behavior
    (there is nothing else to cycle to).

## 6. Dependencies

### Depends On

- **`creature-data-schema`** (`design/gdd/creature-data-schema.md`) — reads, by exact field name:
  `PartDefinition.part_id`, `.can_be_vulnerable`, `.break_priority`, `.crack_stage_count`;
  `PartState.part_id`, `.current_stage`; `hostile_state.part_states`, `.vulnerable_parts`;
  `CreatureInstance.bind_state`, `.hostile_state` (§3.5–3.6, §3.9 invariant 5). Per that schema,
  only `bind_state = hostile` instances carry the `part_states`/`vulnerable_parts` data this system
  needs — this system is therefore entirely inactive against bound creatures (§3.10).
- **`animation-rig-system`** — **newly identified runtime dependency**
  (Assumption A1), not previously listed in `systems-index.md`'s dependency map for this system.
  This system requires a per-part, per-frame transform (`part_id`, position, rotation, scale) in
  virtual-canvas space, produced every frame before this system's hit-test step runs (§3.8). This
  GDD's own edit to `systems-index.md` (see below) adds this dependency to the map so the gap does
  not silently persist; `animation-rig-system`'s own GDD, when authored, must reference this
  document back and confirm/refine the exact contract assumed here.

### Depended On By

- **`accessibility-settings-system`** —
  owns the settings-menu UI and persistence for the player-facing `magnetism_strength` slider (§3.7,
  Formula 2), and is expected to expose (read-only or configurable, its own design call) the global
  tuning constants in §7. This GDD defines the mechanism and the parameter; `accessibility-settings-system`
  owns how the player sets it. That GDD must reference this document back.
- **`combat-encounter-system`** —
  subscribes to the `TargetConfirmed` event (§3.1) as the trigger for its own attack/ability
  resolution against the given `part_id`. This system defines *what* was targeted; it explicitly
  does not define whether that targeting results in a hit, how much damage occurs, or how
  vulnerability-window state factors into the outcome — all of that is `combat-encounter-system`'s
  resolution math (§3.0). That GDD must reference this document back.

### `systems-index.md` Correction (made as part of this GDD)

`systems-index.md`'s Dependency Map previously listed `input-targeting-system` as depending only on
`creature-data-schema`. This GDD's design requires a second, runtime dependency on
`animation-rig-system` (Assumption A1) that was not captured there. This session's authoring pass
updates `systems-index.md`'s Systems Enumeration and Dependency Map to add that edge, consistent
with this project's practice of keeping the dependency graph accurate rather than letting
newly-discovered coupling drift undocumented.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Gameplay Effect |
|---|---|---|---|
| Padding baseline | `padding_base_px` (Formula 1) | 2–16 (default 3), virtual-canvas px | Flat padding every part gets regardless of size. Higher = more forgiving everywhere, but eats into the visual precision the shape-legibility system (art-bible §3.1) is built around if pushed too high. |
| Padding size-scaling | `padding_inverse_scalar` (Formula 1) | 0.0–0.5 (default 0.25) | How aggressively small parts get extra assist beyond the baseline. 0 = all parts padded equally regardless of size (defeats the Fitts's-Law rationale); higher values concentrate the assist on genuinely small/hard-to-hit parts. |
| Padding reference size | `reference_part_size_px` (Formula 1) | 25–100 (default 40), virtual-canvas px | The size threshold above which a part is "easy enough" and gets no size bonus. Should be authored relative to the typical mid-sized part on a standard-tier creature (art-bible §5.6's 96–160px canvas). |
| Padding clamp bounds | `padding_min_px` / `padding_max_px` (Formula 1) | 2–8 / 8–20 (default 3 / 14) | Prevents zero-assist parts (min) and prevents padding from growing large enough to make overlap resolution meaningless on tightly-packed small creatures (max). **`padding_min_px` should be authored to exceed the rendered seam-line's screen-space width** (a fixed 1–2px native "leading" per art-bible §3.1/§3.2, i.e. a comfortable few virtual-canvas px once accounted for) so ordinary seam gaps (§5, Edge Case #5) are essentially always covered. |
| Magnetism radius | `magnetism_radius_px` (Formula 2) | 10–50 (default 25), virtual-canvas px | How far from a part the cursor must be before magnetism can activate at all. Too large starts pulling the cursor across genuinely different parts; too small makes the setting imperceptible. |
| Magnetism strength | `magnetism_strength` (Formula 2) | 0.0–1.0 (default 0.0, off) | Player-facing accessibility slider. 0 = feature disabled entirely (default — padding alone guarantees baseline fairness); 1.0 = full snap onto the nearest part's anchor whenever in range. This is the one knob in this table meant to be end-user-configurable, not just designer-tuned. |
| Cycle auto-repeat delay | `cycle_initial_delay_ms` | 200–600 (default 350) | Time a cycle input must be held before auto-repeat begins. Too short makes a single tap feel like it double-fires; too long makes holding to cycle through a boss's 5–8 parts feel sluggish. |
| Cycle auto-repeat interval | `cycle_repeat_interval_ms` | 60–250 (default 120) | Time between auto-repeated cycle steps while held. Governs how fast a held cycle input can traverse a boss's larger part set — should stay well above single-frame speed so cycling never feels like a reflex race, consistent with Pillar 1. |

**MVP scope note**: all values above are placeholder defaults, unbalanced pending
`combat-encounter-system` design and vertical-slice playtesting (the same flagged risk noted in
`systems-index.md`'s High-Risk Systems table for the sibling combat systems). They are exposed as
data (per `.claude/docs/coding-standards.md`'s data-driven requirement), never hardcoded, so they
can be retuned without a code change once real encounters exist to test them against.

## 8. Acceptance Criteria

1. **Full-parity test**: for a fixture encounter with a known Valid Target Set, every part
   reachable and attackable via a mouse click is also reachable and attackable via cycle-and-confirm
   input alone, with zero mouse/pointer device connected or used — verified by enumerating the
   fixture's parts, confirming each is reachable by some sequence of cycle-next/cycle-prev presses,
   and confirming a `TargetConfirmed` event with the correct `part_id` fires for each after a
   confirm press.
2. **Padding does not relocate an unambiguous click**: a click landing exactly on a part's opaque
   pixels (an exact Phase 1 hit, §3.5) always resolves to that part, regardless of the current
   `padding_px` values of any neighboring part — verified by sweeping `padding_base_px` and
   `padding_max_px` across their full safe ranges (§7) and confirming an exact-hit click's resolved
   `part_id` never changes.
3. **No analog-stick cursor exists**: a static-analysis/code-review check confirms zero code paths
   convert `GamePadState` thumbstick axis values into any cursor, pointer, or targeting position —
   the only gamepad inputs consumed for targeting are the discrete cycle-next, cycle-prev, and
   confirm button/D-pad bindings (§3.4, §8's ban restated from `.claude/docs/technical-preferences.md`'s
   Forbidden Patterns).
4. Given the Formula 1 worked example (`part_size_px = 18`, defaults), `padding_px` computes to
   exactly `8.5`, matching §4 exactly.
5. Given the Formula 1 worked example (`part_size_px = 55`, defaults), `padding_px` computes to
   exactly `3` (baseline only, no size bonus), matching §4 exactly.
6. Given the Formula 2 worked example (`magnetism_strength = 0.5`, `raw_cursor_pos = (150,100)`,
   `nearest_target_anchor_pos = (165,85)`), `effective_click_pos` computes to exactly `(157.5,
   92.5)`, matching §4 exactly.
7. Given the Formula 3 worked example (four parts, `left_claw` vulnerable), the resulting cycle
   order is exactly `left_claw → head → right_claw → tail`, matching §4 exactly, and a fifth
   cycle-next press from `tail` wraps back to `left_claw`.
8. A part breaking while it is the currently `selected_part_id` (cycle path) causes
   `selected_part_id` to reassign, on the same simulation frame the break is applied, to the
   lowest-`sort_key` entry in the freshly recomputed Valid Target Set — verified by simulating a
   break event mid-selection and asserting the register's new value and the frame index it changed
   on.
9. When every `can_be_vulnerable = true` part on a fixture creature is broken, `selected_part_id`
   is `null`, cycle-next/cycle-prev are confirmed no-ops (register unchanged after the press), and a
   mouse click anywhere within the creature's outer contour fires no `TargetConfirmed` event.
10. Two fixture parts authored with intentionally identical (perfectly overlapping) padded hit
    regions resolve deterministically to a single winner on every repeated test run — verified by
    running the same click scenario 100 times and asserting the resolved `part_id` never varies,
    with the winner matching the tie-break chain's first non-tied tier (§3.6 step 4) for the fixture's
    authored data.
11. Pressing confirm while `selected_part_id` is `null` fires no `TargetConfirmed` event and does
    not throw or crash.
12. Simulating alternating mouse-hover and cycle-next events within the same combat frame sequence
    results in `selected_part_id` and `selection_render_source` matching whichever event fired most
    recently, with no intermediate "device switch" event or state observed.
13. A click that resolves to a `part_id` whose `PartState.current_stage = crack_stage_count + 1`
    (already broken, per `creature-data-schema` invariant 6) never occurs — verified that Phase 1 and
    Phase 2 (§3.5, §3.6) both exclude broken parts from candidacy by construction, so no test
    scenario can produce a `TargetConfirmed` event for a broken part.
14. Querying this system's Valid Target Set against a fixture instance with `bind_state = bound`
    returns an empty set, and both input paths remain no-ops against it, confirmed without needing a
    combat encounter to exist for a bound creature at all (§3.10, Edge Case #7).
15. `selection_render_source` is asserted `none` immediately after any mouse-hover update, and
    `cycle` immediately after any cycle-next/cycle-prev update, across a scripted sequence mixing
    both — confirming the rendering gate in §3.11/Assumption A7 never leaves a stale selection ring
    visible during mouse play.
