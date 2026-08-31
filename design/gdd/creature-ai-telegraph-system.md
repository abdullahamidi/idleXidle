# Creature AI & Telegraph System: IDLExIDLE

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | game-designer |
| **Status** | Complete — authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Ambiguities resolved using `design/gdd/creature-data-schema.md`, `design/gdd/animation-rig-system.md`, `design/gdd/game-concept.md`, and `design/art/art-bible.md` (§§2, 3.5, 4.3, 5.5, 7.7) as authority; every resolution is flagged inline as an assumption. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #8). **Pillar-critical**: this is the system that makes Pillar 1 (Precision Over Reflexes) mechanically real rather than aspirational — if its telegraphs are unreadable, precision play silently degrades into a reflex test. |
| **Depends On** | `design/gdd/creature-data-schema.md`, `design/gdd/animation-rig-system.md` |
| **Depended On By** | `combat-encounter-system`, `combat-hud`, `audio-system` |

## Assumptions Log (resolved this session, no placeholders left in the spec below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | The accessibility floor is fixed at **`TELEGRAPH_WINDUP_FLOOR_MS = 600ms`**, paired with a **`TELEGRAPH_MIN_RESET_GAP_MS = 150ms`** minimum low-intensity hold between consecutive telegraphs. | Art-bible §7.7 mandates a floor but names no value. §4's Formula 1 derives 600ms from two independent constraints: (a) WCAG 2.3.1's 3-flashes-per-second threshold, worked through for a chained-telegraph worst case, and (b) Hick's Law reaction-time research for a 3-way defensive decision (block/dodge/interrupt), which takes measurably longer than a simple reflex. 600ms clears both with headroom; the reset gap independently caps worst-case chained flash frequency well under half the WCAG threshold even if a creature telegraphs back-to-back with zero pause. |
| A2 | "Stance change" (game-concept.md's term) and "boss phase" (art-bible §2's term) are modeled as **one mechanism — `Phase`** — not two separate systems, scaled by authoring depth (standard creatures typically 1–2 phases, bosses 3–4). | game-concept.md's own phrasing ("breaking a core triggers a desperation phase") already uses "phase" for what the task brief separately calls a stance change. Treating them as one mechanism avoids two overlapping systems describing the same "the fight's rules just changed" event at two different creature tiers. |
| A3 | Attack selection is a **weighted-random pool with a no-immediate-repeat rule and a guaranteed fallback**, not a strictly authored linear sequence. | The task brief asks for "a sequence/state-machine of attacks with conditions, weights, and cooldowns" — weights are explicitly requested. A strict fixed sequence would let a pattern be fully memorized after one read, at which point zero ongoing reading skill is required — this degrades toward the exact "reflex test in disguise" failure Pillar 1 forbids, just via memorization instead of movement. A weighted, no-repeat, fully-authored pool keeps behavior 100% designer-authored (never emergent AI) while preserving genuine per-encounter pattern-reading. |
| A4 | **Interrupt** is performed by landing a qualifying player ability during the interrupt window (not a dedicated universal button); **block** and **dodge** are dedicated buttons. | game-concept.md separates "a dodge or guard button" from "interrupt major attacks," and the latter reads as an ability outcome, consistent with the game's Resonance Weaving ability system. This document defines the window/rule an interrupt must land inside; which player ability qualifies as "counts as an interrupt" is `resonance-weaving-system`'s job. |
| A5 | The defensive reward gradient is deliberately **transitive**: interrupt > dodge > block, in both risk/cost and reward. | Applies Sirlin's "Playing to Win" framing directly — cost should track reward. Interrupt costs a precisely-timed ability use and the tightest window; dodge costs a cooldown/resource and a tighter timing than block; block is the safest, most forgiving option and is deliberately the lowest-reward. This gives players a legible risk ladder rather than one dominant "always do this" choice. |
| A6 | Vulnerability windows open **only** from (a) a successfully avoided (dodged/blocked-exempt/interrupted) telegraphed attack, or (b) a `Phase` entry ("stance change") — explicitly **not** from a periodic timer. | A timer-driven window would reward passively waiting rather than reading and reacting correctly — directly working against Pillar 1. Every window in this design is a consequence of the player doing something right. |
| A7 | Vulnerability reward is split into **two channels**: a flat damage multiplier (any avoidance type that opens a window) and an **interrupt-exclusive** break-progress multiplier. | Directly answers the task brief's "bonus damage? guaranteed part-break progress?" with "both, but not identically" — giving interrupt a *qualitatively* distinct reward (accelerated part-breaking) rather than only a quantitatively bigger number, which is what makes interrupt feel like a different tool, not just a harder-timed dodge. |
| A8 | A `Phase`'s attack-pool swap is **deferred** until an in-progress telegraph resolves, **unless** the transition is caused by the part executing that exact telegraph breaking. | Canceling an attack the player has already correctly read and committed a defensive input against is an unreadable rug-pull — it would punish a player for doing exactly what Pillar 1 asks of them. The one exception (the executing part itself breaking) is a case where the attack is no longer physically possible to complete, not a fairness violation. |
| A9 | A new gate knob, `min_post_cancel_delay_ms`, is introduced to buffer the gap between a cancelled telegraph and the creature's next telegraph. | Not named in any source document, but required to prevent a part-break-triggered phase transition immediately followed by a fresh telegraph from reading as an unreadable double-attack. A small, tunable buffer restores fairness without being explicitly demanded by the read documents. |

---

## 1. Overview

The creature AI & telegraph system is IDLExIDLE's authored-behavior layer for hostile
creatures and the mechanism that makes every creature attack **readable before it is dangerous**.
It has two halves that are inseparable in practice: an **attack-pattern engine** (a data-driven,
per-creature pool of authored attacks, organized into `Phase`s that react to HP thresholds and
part-breaks — never emergent or randomly improvised behavior) and a **telegraph model** (a fixed,
data-driven timing curve — slow brightness build, hard cutoff — that gives the player enough
warning to read, then block, dodge, or interrupt every dangerous attack before it lands). This is
the system that turns game-concept.md's combat description into buildable rules: which attack a
creature performs next, how long its warning lasts, when a body-part break changes what it can do,
when a boss escalates into a new phase, and when the player's correct read is rewarded with a
vulnerability window. It does not resolve hits, calculate damage, or drive animation playback
directly — those remain `combat-encounter-system`'s job; this document defines
the timing, selection, and reward rules that system consumes.

## 2. Player Fantasy

> **The calm of a hunter who has already read the pattern.** Not the panic of someone reacting
> blind — the quiet confidence of someone who saw the wind-up begin, already knew what was coming,
> and had already decided what to do about it.

This is art-bible §2's mood target for Active Hunting ("predatory clarity... the calm confidence
of a hunter who has already read the pattern, not the panic of someone reacting blind"), and it is
this system's entire reason to exist. Concretely, this system is what guarantees:

- **Every dangerous moment announces itself honestly.** A creature never simply "does damage" —
  it always shows its hand first, on a fixed and legible timing curve, color-keyed but never
  color-*dependent*. The player's skill ceiling is how well they read the tell, never how fast
  their thumb moves.
- **The fight is a text the player learns to read, not a random event they survive.** Because
  every attack comes from a small, fully-authored, weighted pool per `Phase`, a returning player's
  growing mastery of "which attacks this creature can do right now" is real, transferable
  knowledge — not a memorized script and not an illegible dice roll.
- **Breaking a part is a tactical choice with fight-shaping consequences, not just a damage
  optimization.** Removing the claw attack from the pool by breaking the claw, or forcing a
  desperation phase by exposing the core, is a decision the player makes mid-fight that visibly
  changes what they have to read next.
- **The reward for reading correctly is immediate and legible.** Successfully avoiding a
  telegraphed attack — especially interrupting it outright — opens a vulnerability window the
  player can see and act on, turning "I read that right" into a burst of high-value offense within
  seconds. This is the "recognize the pattern, execute a burst" loop game-concept.md names as the
  core satisfaction of combat.
- **Reaching a boss's next phase feels like the fight raising its own stakes, not a wall.** Every
  phase transition is legible (a guaranteed breather window, a visibly changed pool) — the fight
  gets harder because the player made it flinch, not because a hidden timer expired.

## 3. Detailed Rules

### 3.0 Field Naming Note

Field names below are logical identifiers for design purposes, matching the convention already
established by `creature-data-schema.md` §3.0 and `animation-rig-system.md` §3.0. When implemented
as C# data classes, field names should follow the project's C# naming convention (PascalCase
properties, `.claude/docs/technical-preferences.md`) rather than the snake_case used here.

### 3.1 Scope and Boundaries

This document defines: which attack a creature performs next (§3.5–3.6, §4 Formula 6), how long
and on what curve an attack telegraphs (§3.7–3.8, §4 Formulas 1–2), when block/dodge/interrupt are
valid and what they cost/reward (§3.9, §4 Formulas 3–4), when a `Phase` transition fires and what
changes (§3.10–3.11, §4 Formula 5), and when/why/how long a vulnerability window stays open (§3.12,
§4 Formula 4).

It explicitly does **not** define, and defers entirely to `combat-encounter-system` (not yet
written):

- Hit-testing or whether a given input actually lands within a stated window (that is a per-frame
  input-reading responsibility; this document defines the window boundaries only).
- Damage formulas, health resolution, or how a `vulnerability_damage_multiplier` combines with
  base damage math.
- Driving `Clip` playback transitions (idle → windup → peak → recovery → hit) — this document
  supplies the `duration_ms_override` value and the `AttackDefinition` selected; actually starting
  and advancing that `Clip` on the creature's `CreatureRig` (`animation-rig-system.md` §3.5, §3.11)
  is `combat-encounter-system`'s job.
- Which player ability, if any, "counts as" landing an interrupt — owned by
  `resonance-weaving-system` (A4).
- Persisted save data. This system's runtime state (current phase, per-attack cooldown timers, the
  in-progress telegraph, if any) is **not** added to `creature-data-schema.md`'s `CreatureInstance`
  schema — it is transient combat-encounter state, consistent with `save-load-persistence.md`'s
  decision (per `production/session-state/active.md`) that combat encounters are explicitly
  non-persistent. §3.13 specifies this runtime state's shape conceptually, for
  `combat-encounter-system` to own and instantiate.

### 3.2 Terminology: Phase ≡ Stance Change

Per A2, this document uses **`Phase`** as the single mechanism underlying both "a boss phase" and
"a creature changing stance." A standard creature with a single desperation phase and a
multi-phase region boss are the same data structure at different authored depth — there is no
separate "stance system." Where game-concept.md says "changes stance," read "transitions to a new
`Phase`."

### 3.3 CreatureBehaviorProfile (Top-Level Container)

One `CreatureBehaviorProfile` exists per `CreatureTemplate.template_id`
(`creature-data-schema.md` §3.2) — resolved identically to how `animation-rig-system.md` §3.1
resolves one `CreatureRig` per template. A `CreatureInstance` does not own a profile directly; it
resolves one via its current `template_id`, re-resolved automatically on evolution
(`creature-data-schema.md` §3.4), mirroring that document's own re-resolution rule for rigs.

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `template_id` | string | Must match an existing `CreatureTemplate.template_id` | 1:1 link to the creature this profile governs. |
| `phases` | list of `Phase` (§3.4) | ≥ 1; standard creatures typically 1–2, bosses typically 3–4 (recommended, not enforced — §7) | The full set of behavioral phases this creature can enter. |
| `attacks` | map of `attack_id` → `AttackDefinition` (§3.6) | ≥ 1 | Every attack this creature can perform, across all phases. |
| `default_phase_id` | string | Must reference a `Phase.phase_id` in `phases` whose `entry_condition` is the "start" condition (§3.4) and whose `phase_priority = 0` | The phase active the instant a hostile encounter begins. |

### 3.4 Phase

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `phase_id` | string | Unique within the profile | Identifier. |
| `phase_priority` | int | Unique within the profile, ascending = deeper into the fight; the start phase is always `0` | Deterministic ordering used by Formula 5's transition resolution — phases only ever progress toward *higher* priority, never backward. |
| `entry_condition` | `EntryCondition` (below) | — | The condition under which this phase becomes eligible to become active. |
| `attack_pool` | list of `AttackPatternEntry` (§3.5) | ≥ 1 non-fallback entry recommended | The weighted pool this phase selects attacks from (§4 Formula 6). |
| `fallback_attack_id` | string | Must reference an `AttackDefinition` (§3.6) whose `requires_intact_part_ids` is empty | Guaranteed-eligible safety-net attack (Edge Case #1). |
| `desperation` | bool | default `false` | Flags this phase as a core-exposed/last-stand phase. Consumed by `combat-encounter-system` and any VFX/audio coordination (art-bible §2's escalating boss mood); stored only, not interpreted here. |
| `guaranteed_vulnerability_on_entry` | bool | default `true` for every phase with `phase_priority > 0`, `false` for the start phase | Whether entering this phase opens a guaranteed vulnerability window (§3.12, §4 Formula 4, Part A). |
| `guaranteed_vulnerability_target_part_ids` | list of string, nullable | Each must reference a `part_id` on the template | Which part(s) become vulnerable on entry. Defaults to `[the template's is_core part]` if null/empty at resolution time (§4 Formula 4, Part B). |

**Field (`EntryCondition`)**

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `hp_threshold_pct` | float, nullable | `(0.0, 1.0)` if set | Condition contributes "true" once `current_health ÷ effective_max_health` (`creature-data-schema.md` §3.6) first falls at or below this ratio. |
| `trigger_part_ids` | list of string, nullable/empty | Each must reference a `part_id` on the template | Condition contributes "true" the instant **any** listed part reaches `current_stage = crack_stage_count + 1` (broken, `creature-data-schema.md` §3.6). |

A condition is met when **either** sub-condition is true (logical OR): `(hp_threshold_pct is set
AND health ratio ≤ hp_threshold_pct) OR (trigger_part_ids is non-empty AND at least one listed
part is broken)`. The start phase's condition has both fields null/empty and is met unconditionally
at encounter start only (§3.14, invariant 2). Every non-start phase must set at least one of the
two fields (§3.14, invariant 3) — a phase with a vacuous condition could never legally trigger.

### 3.5 AttackPatternEntry

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `attack_id` | string | Must reference an `AttackDefinition.attack_id` in the profile's `attacks` map | Which attack this pool entry offers. |
| `weight` | int | ≥ 1 | Weighted-random selection weight within this phase's pool (§4 Formula 6). The same `attack_id` may appear in multiple phases' pools with different weights — eligibility fields live on `AttackDefinition` itself so they never disagree across phases. |

### 3.6 AttackDefinition

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `attack_id` | string | Unique within the profile | Identifier. |
| `display_name` | string | Free text | Designer/debug reference. |
| `telegraph_clip_id` | string | Must reference a `clip_id` in the creature's `CreatureRig.clips` (`animation-rig-system.md` §3.5) | The wind-up `Clip`. Its `keys` should include the shared `windup` `pose_id` from that document's pose vocabulary (§3.4) — this is the "telegraph reuses the anticipation pose" contract named in this document's own task brief. |
| `executing_part_id` | string, nullable | Must reference a `part_id` on the template, or `null` | Which part visually performs this attack. Non-null for single-limb attacks (a claw swipe); `null` for whole-body/AoE attacks with no single originating part. Drives which part is offered as a vulnerability target when this attack is avoided (§4 Formula 4, Part B) and is the part checked by Edge Case #4 (executing part breaking mid-telegraph cancels the attack). |
| `base_windup_ms` | int | ≥ 1 (authoring range 600–3000, §7) | Wind-up duration at `power_tier = 1`, before difficulty scaling (§4 Formula 1). Fed into the telegraph `Clip`'s `duration_ms_override` at runtime — the exact "data-driven clip duration independent of frame count" hook `animation-rig-system.md` §3.5/§4 Formula 2 names as belonging to this document. |
| `attack_source` | enum `Source` | 6 values, `creature-data-schema.md` §3.1 | Drives the hue-drift target color during wind-up (art-bible §4.3). Independent of the creature's own `source` field — an attack's element need not match the creature's identity, though same-Source is the common authoring case. |
| `requires_intact_part_ids` | list of string, possibly empty | Each must reference a `part_id` on the template | Attack is excluded from selection (§4 Formula 6) if **any** listed part is currently broken. Empty = always physically available regardless of part state — the required shape for any `fallback_attack_id`. |
| `interruptible` | bool | — | Whether the interrupt window (§4 Formula 3) applies to this attack at all. |
| `avoidance_options` | list, subset of `{block, dodge, interrupt}` | Non-empty unless rejected by §3.14 invariant 7 | Which defenses are valid against this attack. `interrupt` may only appear if `interruptible = true` (§3.14, invariant 8). An attack may legally omit `block` and/or `dodge` (an authored "must interrupt or take it" moment) but never all three. |
| `cooldown_ms` | int | ≥ 0 (authored per attack, §7) | Minimum real time before this specific `attack_id` may be selected again (§4 Formula 6). |
| `stance_change_tag` | string, nullable | Free-form tag | Marks this specific attack as one that also triggers a `Phase` transition on execution, independent of HP/part-break conditions (e.g., a boss that always shifts into a defensive stance immediately after one specific attack). Stored only; interpreted by `combat-encounter-system`, matching `creature-data-schema.md`'s established tag-storage pattern (its `fight_state_change_tag`). |
| `hazard_summon_tag` | string, nullable | Free-form tag | References an environmental hazard to spawn on execution (game-concept.md: "summons hazards"). Stored only, not interpreted here. |

### 3.7 The Telegraph Model

Every telegraphed attack follows the same fixed shape, per art-bible §2/§4.3: **brightness builds
on a slow, readable curve, then cuts hard the instant the attack fires.** Concretely:

1. At `elapsed_ms = 0` (telegraph start), the creature's vulnerability glyph — already present at
   its resting `Ember Threat` intensity (art-bible §4.3) — begins ramping brightness upward.
2. Brightness increases **monotonically** across the full `windup_duration_ms` (§4 Formula 1),
   following an eased curve (§4 Formula 2) that is deliberately slow at first and accelerates
   toward the end — the "slow build."
3. At `elapsed_ms = windup_duration_ms` exactly (the cutoff frame), the attack executes (hit
   resolution, hazard spawn — `combat-encounter-system`'s job) and brightness snaps instantly back
   to resting intensity in that same frame — the "hard cutoff." This is never a second eased
   transition; it is a discontinuity, by design, so the moment of maximum danger is unambiguous.
4. A secondary, **non-load-bearing** hue drift moves the glyph's color from `Ember Threat` toward
   `attack_source`'s Source color (art-bible §4.2) in lockstep with the same eased progress used
   for brightness (§4 Formula 2) — confirming art-bible §4.3's "color-keyed to the attack's
   element" language. Per that section's explicit design test, covering this hue in gray must not
   change whether a player can correctly read "safe" vs. "about to hit me" — only brightness and
   timing carry that load.

This is implemented by feeding `windup_duration_ms` into the telegraph `Clip`'s
`duration_ms_override` (`animation-rig-system.md` §3.5, §4 Formula 2) and driving the
`GlyphOverlay`'s shader-parameter hook (that document §3.8) with this document's Formula 2 output
every frame — fulfilling the contract that document explicitly left open for this system to own.

### 3.8 The Accessibility Floor (Hard Requirement)

**`TELEGRAPH_WINDUP_FLOOR_MS = 600`** and **`TELEGRAPH_MIN_RESET_GAP_MS = 150`** are fixed,
non-tunable constants (§7). No difficulty setting, `power_tier`, or region scaling may ever produce
a rendered wind-up shorter than the floor, and no two consecutive telegraphs on the same creature
may begin closer together than the reset gap allows once the first one's cutoff has fired. Formula
1 (§4) applies this floor as a clamp inside the formula itself — never as a separate validation
step that could be skipped. §4/§5 prove the clamp mathematically; §8 requires it be tested across
the full `power_tier` range.

### 3.9 The Audio Pre-Cue

A redundant audio pre-cue plays on the **identical normalized progress curve** as the visual
brightness ramp (§3.7, §4 Formula 2) — same start instant, same eased shape, same hard-cutoff
instant — per art-bible §7.7's accessibility requirement ("for players who process fast visual
change poorly"). This document owns only the **timing contract**: the audio channel's intensity
(volume/pitch ramp, however `audio-system` chooses to express it) must track the same `t_eased`
value computed for brightness, and must cut at the exact same `elapsed_ms = windup_duration_ms`
instant — the two channels can never legally desync, because desyncing them would silently make
one channel misleading. The actual sound asset, mix, and default prominence are `audio-system`'s
job; whether/how strongly it plays by default vs. as a boosted accessibility
option is `accessibility-settings-system`'s job.

### 3.10 Attack Selection

At each creature "decision point" (the previous action having fully resolved — `combat-encounter
-system`'s signal to request a new attack), the currently active `Phase`'s `attack_pool` is
evaluated per §4 Formula 6: eligible entries are filtered by intact-part requirements, cooldown
elapsed, and a no-immediate-repeat rule (relaxed, then bypassed via `fallback_attack_id`, if
filtering would otherwise leave nothing eligible — Edge Case #1), then one entry is chosen by
weighted random from whichever filtered set was non-empty.

### 3.11 Phase Transitions

After every change to `hostile_state` that could affect an `EntryCondition` (a health decrease, or
any part reaching broken), §4 Formula 5 evaluates all phases whose condition is currently met and
whose `phase_priority` exceeds the current phase's — selecting the single **highest-priority**
eligible phase, never stepping through intermediate phases one at a time. This lets one large burst
of damage or a single decisive part-break correctly skip straight to the fight's true current state
(Edge Case #6).

**Deferred pool swap (A8):** if a telegraph is already in progress when a transition fires, the
in-progress attack is allowed to resolve normally (fires or gets avoided) before the new phase's
`attack_pool` takes effect for the *next* selection — **unless** the transition was caused by the
part performing that exact attack (`AttackDefinition.executing_part_id`) breaking, in which case
the telegraph is cancelled immediately (Edge Cases #2–#4). The transition itself, and any
`guaranteed_vulnerability_on_entry` window, still fire immediately regardless of which branch
applies — only the *attack pool swap* is ever deferred.

### 3.12 Vulnerability Windows

A vulnerability window is the mechanism this document uses to populate
`hostile_state.vulnerable_parts` and `hostile_state.vulnerability_window_ms`
(`creature-data-schema.md` §3.6) — the exact fields that schema's §6 named as owned by this
system. Windows open **only** from two triggers (A6):

1. **A telegraphed attack is successfully avoided** — dodged, blocked (per the table below), or
   interrupted.
2. **A `Phase` entry** flagged `guaranteed_vulnerability_on_entry = true`.

Duration, target parts, and reward differ by trigger and avoidance type (§4 Formula 4). If a new
trigger fires while a window from an earlier trigger is still counting down, the two **compose**
rather than one overriding the other (§4 Formula 4, Part C) — target parts union, and the timer
extends to whichever remaining duration is longer. A window's target parts are always filtered to
exclude any part that is already broken, per `creature-data-schema.md` invariant 6; if filtering
empties the set, no window opens for that trigger.

| Avoidance Type | Opens a Window? | Relative Duration | Relative Reward |
|---|---|---|---|
| Block (success) | No | — | Damage negated/reduced only. No vulnerability window — the safe, low-reward option (A5). |
| Dodge (success) | Yes | Base | Standard damage multiplier during the window. |
| Interrupt (success) | Yes | Base × bonus multiplier (longer) | Standard damage multiplier **plus** an interrupt-exclusive break-progress multiplier (A7) — the highest-cost, highest-reward option. |
| Phase entry | Yes | Base (dodge-tier) | Standard damage multiplier only — a freebie opening, not an earned-execution reward, so it does not receive interrupt's break-progress bonus. |

### 3.13 Interrupt / Block / Dodge

Per A4, block and dodge are dedicated player inputs; interrupt is the outcome of landing a
qualifying ability during the interrupt window (which ability qualifies is
`resonance-weaving-system`'s job). All three read their timing boundaries from §4 Formula 3, which
derives every window as a ratio of `windup_duration_ms` — meaning windows scale proportionally
with difficulty automatically, from a single source of truth.

- **Dodge** and **Interrupt** are point-in-time inputs: succeeding requires the input (or
  qualifying ability hit) to land at any instant within `[window_start_ms, window_end_ms]`.
- **Block** is a **hold** requirement: succeeding requires guard to be continuously active for the
  *entire* `[block_window_start_ms, block_window_end_ms]` span, including the cutoff frame itself
  — a brief drop anywhere in that span before re-engaging is a failed block, not a partial one
  (Edge Case #11). This reflects block's identity as the safe, committal, least-precise-timing
  option (A5).
- Only avoidance types listed in the attack's `avoidance_options` are valid; `interrupt` is only
  ever valid if `interruptible = true` (§3.14, invariant 8).

`combat-encounter-system` owns reading the actual per-frame input state and evaluating it against
these windows; this document defines the windows and their outcomes only.

### 3.13.1 Runtime State Shape (Conceptual — Not Persisted)

For `combat-encounter-system` to implement against, this document specifies (but does not own or
persist) the runtime state its formulas assume exists per active hostile `CreatureInstance`:

```
CreatureBehaviorRuntimeState {
  current_phase_id: string
  phase_entered_at_ms: float
  last_selected_attack_id: string, nullable
  attack_cooldown_expiry: map<attack_id, timestamp_ms>
  active_telegraph: nullable {
    attack_id: string
    started_at_ms: float
    windup_duration_ms: float   // this attack's Formula 1 output, cached at telegraph start
  }
}
```

This state is discarded the instant the encounter ends (kill, capture, or the creature's own
defeat-transition per `creature-data-schema.md` Edge Case #3) — it is never written to
`CreatureInstance` and never persisted, consistent with `save-load-persistence.md`'s
combat-encounters-are-non-persistent decision.

### 3.14 Schema Invariants (Validation Rules)

These must hold for any valid `CreatureBehaviorProfile` and are the basis for the Acceptance
Criteria in §8:

1. All `Phase.phase_priority` values within one profile are unique.
2. Exactly one `Phase` has `entry_condition = {hp_threshold_pct: null, trigger_part_ids: []}` and
   `phase_priority = 0`; this is the profile's `default_phase_id` target.
3. Every non-start `Phase`'s `entry_condition` sets at least one of `hp_threshold_pct` or
   `trigger_part_ids` (non-vacuous).
4. Every `AttackPatternEntry.attack_id` and every `Phase.fallback_attack_id` references an
   existing key in the profile's `attacks` map.
5. Every `part_id` referenced anywhere in this profile (`AttackDefinition.requires_intact_part_ids`,
   `executing_part_id`, `EntryCondition.trigger_part_ids`,
   `Phase.guaranteed_vulnerability_target_part_ids`) references an existing `part_id` on the
   template's `CreatureTemplate.parts` (`creature-data-schema.md` §3.3).
6. Every `Phase.fallback_attack_id` references an `AttackDefinition` whose
   `requires_intact_part_ids` is empty.
7. Every `AttackDefinition` has at least one valid defense: `avoidance_options` is non-empty, or
   `interruptible = true`, or both. An attack with zero valid defenses of any kind is rejected.
8. `interrupt` appears in an `AttackDefinition.avoidance_options` only if that attack's
   `interruptible = true`.
9. Every `AttackDefinition.telegraph_clip_id` references an existing `clip_id` in the linked
   `CreatureRig.clips` (`animation-rig-system.md` §3.5) — a cross-document validation.
10. `interrupt_window_start_ratio < interrupt_window_end_ratio ≤ 1.0` (an interrupt can never
    legally extend past the cutoff — by definition it must land before the attack fires); and
    `block_window_end_ratio = 1.0` exactly (a block must cover the cutoff frame, never extend past
    impact); `dodge_window_end_ratio` is permitted up to `1.10` (a small post-cutoff grace).

## 4. Formulas

All formulas below share `creature-data-schema.md`'s round-half-up convention for any value that
must resolve to an integer (frame indices, millisecond durations); rendering-time curve outputs
(brightness, hue, `t_eased`) remain unrounded floats, matching `animation-rig-system.md` §4's own
rounding scope.

### Formula 1 — Telegraph Wind-Up Duration by Difficulty (Accessibility-Floor Clamp)

```
windup_duration_ms = max(TELEGRAPH_WINDUP_FLOOR_MS, round(base_windup_ms × (1 − windup_difficulty_scalar × (power_tier − 1))))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_windup_ms` | int | 600–3000 | `AttackDefinition.base_windup_ms`, authored per attack at `power_tier = 1`. |
| `windup_difficulty_scalar` | float | 0.00–0.06 (default 0.05) | Global tuning constant (§7). Controls how steeply wind-up shrinks per `power_tier` step. |
| `power_tier` | int | 1–20 | `CreatureInstance.power_tier` (`creature-data-schema.md` §3.5). |
| `TELEGRAPH_WINDUP_FLOOR_MS` | int | Fixed at `600` | **Non-tunable** (§3.8, A1). |
| `windup_duration_ms` | int | `[600, base_windup_ms]` | Output. Fed into the telegraph `Clip`'s `duration_ms_override`. |

**Output range**: bounded below by the fixed floor of 600ms regardless of any other input, bounded
above by `base_windup_ms` itself (the formula only ever shrinks wind-up, never grows it beyond the
authored base). The clamp is not a separate check — it is `max(...)`, evaluated every time.

**Worked example A (no clamp engaged)**: `base_windup_ms = 1800` (matching
`animation-rig-system.md` §4 Formula 2's own telegraph worked example), `windup_difficulty_scalar
= 0.05`, `power_tier = 5`:

```
factor = 1 − 0.05 × (5 − 1) = 1 − 0.20 = 0.80
raw    = 1800 × 0.80 = 1440
windup_duration_ms = max(600, round(1440)) = 1440ms
```

**Worked example B (clamp engaged — the required accessibility proof)**: same `base_windup_ms` and
scalar, `power_tier = 20` (maximum):

```
factor = 1 − 0.05 × (20 − 1) = 1 − 0.95 = 0.05
raw    = 1800 × 0.05 = 90
windup_duration_ms = max(600, round(90)) = max(600, 90) = 600ms
```

Without the floor, this creature's wind-up at maximum difficulty would collapse to 90ms — well
inside strobe territory and far below any human-reactable decision window. The clamp guarantees
600ms regardless. This is the direct, worked proof that difficulty scaling can approach but never
breach the floor.

### Formula 2 — Telegraph Brightness and Hue Curve

```
t_raw          = clamp(elapsed_ms ÷ windup_duration_ms, 0, 1)
t_eased        = ease(telegraph_brightness_curve_id, t_raw)
brightness(t)  = resting_intensity + (peak_intensity − resting_intensity) × t_eased
hue(t)         = lerp_hue(ember_threat_hue, attack_source_hue, t_eased)          [non-load-bearing]

At elapsed_ms = windup_duration_ms exactly: the attack executes, and in the same frame
brightness snaps to resting_intensity and hue snaps to ember_threat_hue (the hard cutoff).
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `elapsed_ms` | float | `[0, windup_duration_ms]` | Time since telegraph start. |
| `telegraph_brightness_curve_id` | string | A Curve Library entry (`animation-rig-system.md` A7); default `quad_ease_in` | Governs the "slow build" shape. |
| `resting_intensity` / `peak_intensity` | float | `[0, 1]`; defaults `0.2` / `1.0` | Shader-parameter range fed to the `GlyphOverlay`'s HLSL palette-lookup shader (`animation-rig-system.md` §3.8). |
| `ember_threat_hue` / `attack_source_hue` | float (deg) | Fixed hues from art-bible §4.1/§4.2 | Endpoints of the non-load-bearing hue drift. |
| `brightness(t)`, `hue(t)` | float | Unbounded rendering floats, in practice `[resting, peak]` / hue-wheel range | Output, per-frame shader parameters. |

**Output range**: `brightness(t)` is bounded to `[resting_intensity, peak_intensity]` for any
`t_eased ∈ [0,1]`, and `t_eased` is guaranteed in `[0,1]` for any monotonic easing curve given the
clamped `t_raw` input.

**Worked example**: `resting_intensity = 0.2`, `peak_intensity = 1.0`, `quad_ease_in` (`e(t) =
t²`), `windup_duration_ms = 1800`:

```
elapsed_ms = 900:
  t_raw = 900 ÷ 1800 = 0.5
  t_eased = 0.5² = 0.25
  brightness = 0.2 + (1.0 − 0.2) × 0.25 = 0.2 + 0.2 = 0.4

elapsed_ms = 1800 (cutoff):
  t_raw = 1.0, t_eased = 1.0
  brightness = 0.2 + 0.8 × 1.0 = 1.0 (peak) — then, same frame, snaps to 0.2 as the attack fires.
```

This demonstrates both halves of the locked shape: `quad_ease_in`'s low early slope produces the
"slow build" (brightness only reaches 0.4 — 25% of the way to peak — at the 50% time mark, then
accelerates through the back half), and the same-frame snap at cutoff is the "hard cutoff," never
a second eased transition.

### Formula 3 — Interrupt / Dodge / Block Window Boundaries

```
window_start_ms = windup_duration_ms × ratio_start
window_end_ms   = windup_duration_ms × ratio_end
```

| Window | `ratio_start` (default) | `ratio_end` (default) | Input model |
|---|---|---|---|
| Interrupt | `interrupt_window_start_ratio` (0.40) | `interrupt_window_end_ratio` (0.80) | Point-in-time (qualifying ability hit) |
| Dodge | `dodge_window_start_ratio` (0.65) | `dodge_window_end_ratio` (1.05) | Point-in-time (dodge input) |
| Block | `block_window_start_ratio` (0.50) | `block_window_end_ratio` (1.00) | Continuous hold through the cutoff |

**Output range**: each window is a sub-interval of `[0, windup_duration_ms]`, except dodge's end
boundary which may extend up to 5% past `windup_duration_ms` itself (a small post-cutoff grace,
capped at the 1.10 safety ceiling per §3.14 invariant 10).

**Worked example** at `windup_duration_ms = 1800` (Formula 1's Example A):

```
Interrupt window: [1800×0.40, 1800×0.80] = [720ms, 1440ms]   (720ms wide)
Dodge window:     [1800×0.65, 1800×1.05] = [1170ms, 1890ms]  (720ms wide, 90ms grace past cutoff)
Block window:     [1800×0.50, 1800×1.00] = [900ms, 1800ms]   (900ms — must be held the whole span)
```

**Worked example at the accessibility floor** (`windup_duration_ms = 600`, Formula 1's Example B):

```
Interrupt window: [240ms, 480ms]   (240ms wide)
Dodge window:     [390ms, 630ms]
Block window:     [300ms, 600ms]
```

At the floor, absolute window widths shrink proportionally with `windup_duration_ms` — this is an
accepted consequence of maximum difficulty, not a floor violation: the floor guarantees the
*wind-up itself* stays readable and non-strobing, not that every difficulty tier remains equally
easy to execute against. Note also that even the narrowest window here (240ms interrupt) opens only
*after* 240ms of pre-window telegraph time the player has already had to read and commit a
decision during — the window itself only needs to cover execution/confirmation, not initial
recognition.

### Formula 4 — Vulnerability Window Duration, Target Parts, and Reward

**Part A — Duration by trigger:**

```
window_tier_factor = max(vulnerability_window_floor_ratio, 1 − vulnerability_window_difficulty_scalar × (power_tier − 1))

vulnerability_window_ms(block)             = 0   (no window)
vulnerability_window_ms(dodge)             = round(base_vulnerability_window_ms × window_tier_factor)
vulnerability_window_ms(interrupt)         = round(base_vulnerability_window_ms × interrupt_window_bonus_multiplier × window_tier_factor)
vulnerability_window_ms(phase_entry)       = round(base_vulnerability_window_ms × window_tier_factor)   [dodge-tier — no interrupt bonus]
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_vulnerability_window_ms` | int | 800–2500 (default 1500) | Global tuning constant (§7). |
| `vulnerability_window_difficulty_scalar` | float | 0.00–0.04 (default 0.02) | Curve knob — gentler than telegraph shrinkage, since this is a reward window, not a fairness-critical read window. |
| `vulnerability_window_floor_ratio` | float | 0.4–0.7 (default 0.5) | A **design** floor (tunable — not the accessibility floor from §3.8) guaranteeing windows never shrink below this fraction of base regardless of tier. |
| `interrupt_window_bonus_multiplier` | float | 1.2–2.0 (default 1.5) | Curve knob — the length half of interrupt's premium reward (A5, A7). |

**Worked example**: `base_vulnerability_window_ms = 1500`, `interrupt_window_bonus_multiplier =
1.5`, `vulnerability_window_difficulty_scalar = 0.02`, `vulnerability_window_floor_ratio = 0.5`,
`power_tier = 10`:

```
window_tier_factor = max(0.5, 1 − 0.02 × 9) = max(0.5, 0.82) = 0.82
Dodge window     = round(1500 × 0.82) = 1230ms
Interrupt window = round(1500 × 1.5 × 0.82) = round(1845) = 1845ms
```

**Part B — Target parts by trigger:**

```
avoid_target_parts(attack)  = { attack.executing_part_id }  if non-null, else {} (no vulnerability granted)
phase_target_parts(phase)   = phase.guaranteed_vulnerability_target_part_ids, or [the template's is_core part] if null/empty

Both sets are filtered to exclude any part_id already at current_stage = crack_stage_count + 1 (broken).
If filtering empties the set, no window opens for that trigger.
```

**Part C — Composition when triggers overlap in time:**

```
new_vulnerable_parts       = (current vulnerable_parts ∪ new_trigger_target_parts), filtered per Part B
new_vulnerability_window_ms = max(current_remaining_vulnerability_window_ms, new_trigger_duration_ms)
```

**Worked example (composition)**: a player successfully interrupts an attack whose
`executing_part_id = left_claw`, opening an 1845ms window (from the example above) on
`{left_claw}` with 1200ms already elapsed when the boss's HP crosses a phase threshold, opening a
dodge-tier 1230ms guaranteed window on `{core}` (the phase's default target). Remaining time on the
existing window = `1845 − 1200 = 645ms`.

```
new_vulnerable_parts = {left_claw} ∪ {core} = {left_claw, core}
new_vulnerability_window_ms = max(645, 1230) = 1230ms
```

Both parts stay exposed for the longer remaining duration; neither trigger silently overwrites the
other.

**Reward multipliers (consumed by `combat-encounter-system`'s damage math, not defined here):**
`vulnerability_damage_multiplier` (default 1.5, range 1.25–2.5) applies to any hit landing on a
currently-vulnerable part, regardless of which trigger opened the window.
`interrupt_break_progress_multiplier` (default 2.0, range 1.25–3.0) applies **additionally**, and
only, to `PartState.accumulated_value` gains on a part whose current vulnerability window was
opened by a successful interrupt (A7) — dodge- and phase-entry-triggered windows never receive
this second multiplier.

### Formula 5 — Phase Transition Evaluation

```
new_phase = argmax_{p ∈ phases} { p.phase_priority : entry_condition_met(p, hostile_state) AND p.phase_priority > current_phase.phase_priority }
          = no transition, if that set is empty
```

Evaluated once per `hostile_state` change (not once per individual part-break within a
simultaneous multi-part-break tick — Edge Case #6), always selecting the **highest-priority**
eligible phase, never stepping sequentially through intermediate ones.

**Worked example**: a 3-phase boss — Phase 0 (`priority 0`, start), Phase 1 (`priority 1`,
`hp_threshold_pct = 0.66`), Phase 2 (`priority 2`, `hp_threshold_pct = 0.33` OR
`trigger_part_ids = [core_shell]`). The boss is at Phase 0, full HP, and takes a single burst hit
that drops it straight to 20% HP in one tick (`current_health ÷ effective_max_health = 0.20`):

```
Phase 1 condition: 0.20 ≤ 0.66 → met, priority 1 > 0 → eligible
Phase 2 condition: 0.20 ≤ 0.33 → met, priority 2 > 0 → eligible
argmax by phase_priority → Phase 2 (priority 2 beats priority 1)
```

The boss transitions directly from Phase 0 to Phase 2 in one step — it never activates Phase 1's
attack pool for even a single decision, because by the time the transition is evaluated the boss's
*actual* current state already qualifies for Phase 2.

### Formula 6 — Attack Selection (Weighted Random with Fallback)

```
1. eligible = { e ∈ attack_pool : all(requires_intact_part_ids of e.attack are NOT broken)
                                   AND (now − attack_cooldown_expiry[e.attack_id]) ≥ 0
                                   AND e.attack_id ≠ last_selected_attack_id }
2. if eligible = ∅: eligible' = same filter, dropping the "≠ last_selected_attack_id" clause
3. if eligible' = ∅: select phase.fallback_attack_id directly (ignores cooldown and repeat rule)
4. otherwise: weighted-random select from whichever non-empty set was produced —
     total_weight = Σ weight over the set
     roll = uniform_random(0, total_weight)
     walk the set accumulating weight; select the first entry where running_total > roll
```

**Worked example**: phase pool = `[ClawSwipe(weight=5), TailWhip(weight=3), Roar(weight=2)]`, all
part-requirements intact, all cooldowns elapsed, `last_selected_attack_id = ClawSwipe` (excluded by
the repeat rule):

```
eligible = {TailWhip(3), Roar(2)}, total_weight = 5
roll = 3.7 (example draw)
walk: TailWhip running_total = 3 → 3.7 is not < 3, continue
      Roar      running_total = 5 → 3.7 < 5 → select Roar
```

**Fallback worked example**: same pool, but `left_claw` (required by ClawSwipe) and the part
required by TailWhip are both currently broken, and Roar is on cooldown. `eligible = ∅` (step 1),
`eligible' = ∅` (step 2 — nothing is even part-eligible, repeat rule was never the blocker). Step 3
selects `phase.fallback_attack_id` directly — guaranteed valid by §3.14 invariant 6 — and the
creature always has a legal action (Edge Case #1).

## 5. Edge Cases

1. **A creature's entire non-fallback attack pool becomes ineligible** (e.g., every remaining
   attack requires a now-broken part). Resolved by Formula 6, steps 2–3: the no-repeat rule relaxes
   first; if that still leaves nothing eligible, the phase's `fallback_attack_id` — validated at
   load time to always have empty `requires_intact_part_ids` — is selected directly, bypassing
   cooldown and repeat filtering entirely. A creature is never left with no legal action.
2. **A telegraph is interrupted mid-wind-up.** The telegraph `Clip` is hard-cut immediately (no
   blend, matching `animation-rig-system.md` Edge Case #5's established hard-cut convention); the
   interrupted attack's `cooldown_ms` timer starts from that instant (so it cannot be immediately
   re-selected once eligible again); a vulnerability window opens per Formula 4's interrupt branch;
   the creature's next attack-selection decision point (Formula 6) begins immediately, still
   subject to the no-immediate-repeat rule against the just-cancelled `attack_id`.
3. **A phase transition fires mid-telegraph, and the transition is *not* caused by the executing
   part breaking.** Per A8, the in-progress telegraph completes normally (fires or is avoided)
   before the new phase's `attack_pool` takes effect for the *next* selection. The transition event
   itself, and any `guaranteed_vulnerability_on_entry` window, still fire immediately — only the
   pool swap is deferred. Canceling an attack the player already correctly read and reacted to
   would be an unfair rug-pull, violating the Player Fantasy this system exists to serve.
4. **A phase transition fires mid-telegraph, and the transition *is* caused by the executing part
   (`AttackDefinition.executing_part_id`) breaking.** The telegraph cancels immediately (same
   hard-cut mechanism as #2 — the attack is no longer physically possible), the phase transition
   and any guaranteed window fire immediately, and the next attack selection runs against the new
   phase's pool after a `min_post_cancel_delay_ms` buffer (§7, A9) — preventing an instant
   re-telegraph from reading as an unreadable double-attack.
5. **The creature's core part breaks (any time, including mid-telegraph).** Per
   `creature-data-schema.md` Edge Case #2, a core break ends the encounter immediately regardless
   of any other state — this overrides everything in this document. Any in-progress telegraph is
   cancelled without a cutoff/hit resolution, no `Phase` evaluation runs, no vulnerability window
   opens (the encounter is already over). This system defers entirely to that schema's hard rule.
6. **Multiple parts (not the core) break in the same tick from one large hit** (e.g., an AoE
   ultimate), each resolved in ascending `break_priority` order per `creature-data-schema.md` Edge
   Case #1. Formula 5's `EntryCondition` evaluation runs once, *after* all part-breaks in that tick
   have resolved — not once per individual part-break — so a simultaneous multi-part break
   evaluates the final post-tick state and (per Formula 5) selects the single deepest eligible
   phase directly, never stepping through intermediate phases within one tick.
7. **Difficulty scaling would mathematically push `windup_duration_ms` below the accessibility
   floor.** It cannot: Formula 1's `max(TELEGRAPH_WINDUP_FLOOR_MS, ...)` is a step inside the
   formula itself, applied on every evaluation, not a separate validation checked afterward — proven
   for the full `power_tier` range in §8's required test and demonstrated in Formula 1's Worked
   Example B (a raw 90ms result clamped to 600ms).
8. **A creature is defeated (health reaches 0 via non-core-break damage, or is captured) mid-
   telegraph or mid-vulnerability-window.** Mirrors `creature-data-schema.md` Edge Case #3: the
   instant `hostile_state` is atomically cleared, any in-flight telegraph and any open
   `vulnerable_parts`/`vulnerability_window_ms` are discarded in the same write. This system's
   transient runtime state (§3.13.1) does not persist past that transition, consistent with
   `animation-rig-system.md` A4's "re-derived, never persisted" convention for the analogous rig
   state.
9. **Two phases' `EntryCondition`s become simultaneously true in the same tick, triggered by
   different condition types** (e.g., an HP threshold and an unrelated part-break both cross in the
   same hit). Formula 5's `argmax`-by-`phase_priority` rule resolves this deterministically
   regardless of which condition type triggered it — priority order is the single authoritative
   tie-break, matching the project's established pattern (`creature-data-schema.md`'s
   `break_priority`, `animation-rig-system.md`'s `draw_order`).
10. **An `AttackDefinition` restricts `avoidance_options` to `{interrupt}` only** (an authored
    "must interrupt or take it" moment, typically reserved for late desperation-phase attacks).
    Legal and intentional. An attack with `interruptible = false` **and** an `avoidance_options`
    that also excludes both `block` and `dodge` — i.e., zero valid defenses of any kind — is a
    content-validation error and is rejected at load time (§3.14, invariant 7).
11. **A player taps guard briefly inside the block window instead of holding it continuously.** A
    block only counts as successful if guard is active for the *entire*
    `[block_window_start_ms, block_window_end_ms]` span, including the cutoff frame; a drop
    anywhere in that span, even briefly, before re-engaging is scored as a failed block — never a
    partial one. This is a deliberate departure from dodge/interrupt's point-in-time model,
    reflecting block's identity as the safe-but-committal option (A5). This document defines the
    rule; `combat-encounter-system` owns reading the per-frame input state to evaluate it.
12. **`base_windup_ms` is authored below `TELEGRAPH_WINDUP_FLOOR_MS` even at `power_tier = 1`** (an
    authoring mistake). Formula 1's clamp still applies uniformly at every tier — the output is
    simply always `TELEGRAPH_WINDUP_FLOOR_MS`, which is safe but almost certainly not the intended
    pacing (a wind-up that never scales down with difficulty because it never leaves the floor).
    Flagged as a content-validation **warning**, not a hard rejection, since the result is safe,
    only probably unintended.
13. **A new vulnerability trigger fires while a prior window is still counting down** (e.g., an
    interrupt lands on one part while a phase-entry window is still open on another). Resolved by
    Formula 4, Part C: target parts union, and the shared `vulnerability_window_ms` timer extends
    to whichever remaining duration is longer — never overwritten, never shortened by a later,
    smaller trigger. This is required because `creature-data-schema.md`'s `vulnerability_window_ms`
    is a single shared countdown for the whole `vulnerable_parts` list, not a per-part timer.

## 6. Dependencies

### Depends On

- **`encounter-spawn-system.md`** — supplies **`power_tier`** on the spawned `CreatureInstance`, which
  this document consumes to scale `windup_duration_ms` (Formula 6): a higher-tier creature telegraphs
  faster, compressing the player's read-and-react window. Before that system existed, `power_tier` had
  four consumers (this one among them) and **no producer** — Blocker B2. Note the scaling is clamped by
  `TELEGRAPH_WINDUP_FLOOR_MS = 600` at **every** tier, so no `power_tier` can ever push a windup below
  the WCAG 2.3.1 anti-strobe floor; that clamp is non-negotiable and is asserted in this document's ACs.
- **`creature-data-schema.md`** — reads `CreatureTemplate.parts[].part_id` /
  `.is_core` / `.break_priority` / `.can_be_vulnerable` / `.crack_stage_count`,
  `CreatureInstance.power_tier`, and `hostile_state.current_health` /
  `.effective_max_health` / `.part_states[].current_stage`. **Writes**
  `hostile_state.vulnerable_parts` and `hostile_state.vulnerability_window_ms` — the exact fields
  that document's §6 named as owned by this system — obeying its invariants 6 and 7 (never
  includes a broken part; the window field is non-null iff the parts list is non-empty) at every
  step, per §4 Formula 4 and §5 Edge Case #13. Also consumes `PartDefinition.fight_state_change_tag`
  as the authoring hook that document explicitly deferred interpretation of to "combat-encounter
  -system / creature-ai-telegraph-system" — this document, via `AttackDefinition.stance_change_tag`
  and the `Phase` system generally, is where that interpretation happens.
- **`animation-rig-system.md`** — reads `CreatureRig.clips[clip_id]` (the `telegraph_clip_id`
  reference) and relies on that document's `duration_ms_override` mechanism (its §4 Formula 2) to
  make a telegraph's wall-clock length a pure data value (this document's `windup_duration_ms`)
  independent of `frame_count`/`playback_fps` — directly fulfilling that document's own worked
  example, which named `duration_ms_override` as "a tunable data value owned by
  creature-ai-telegraph-system." Also drives that document's `GlyphOverlay` shader-parameter hook
  (§3.8 there) every frame with this document's Formula 2 brightness/hue output, fulfilling the
  contract that document explicitly left open ("this rig... does not own the brightness curve or
  timing rule that drives it — that belongs to creature-ai-telegraph-system"). Reuses the Curve
  Library (`easing_curve_id`, that document's A7) for `telegraph_brightness_curve_id` rather than
  inventing a parallel curve system. Does not drive `Clip` playback itself — that remains
  `combat-encounter-system`'s job per that document's own Depended-On-By note.

### Depended On By

- **`combat-encounter-system`** — will drive actual `Clip` playback transitions
  using this document's `windup_duration_ms` and `AttackDefinition` selections (§4 Formula 6); will
  resolve player dodge/block/interrupt input against this document's window boundaries (§4 Formula
  3) and apply the resulting avoidance/damage outcome; will read `vulnerability_damage_multiplier`
  and `interrupt_break_progress_multiplier` (§4 Formula 4) as inputs to its own damage-resolution
  math, which this document does not define; will read and interpret `hazard_summon_tag` and
  `stance_change_tag` off a selected `AttackDefinition`, per `creature-data-schema.md`'s established
  tag-storage pattern. This document defines target selection, timing, and reward rules only — hit
  -testing, damage formulas, and input handling remain entirely `combat-encounter-system`'s job.
- **`combat-hud`** — MAY consume this system's interrupt-window boundaries and
  current `phase_id`/`desperation` flag if a future UX pass chooses to add an ability-affordance
  cue. Per art-bible §7.4/§7.5, the strong design intent is that telegraph and vulnerability state
  remain **fully diegetic** — rendered on the creature's own glyph, with zero dedicated HUD widgets
  for part-break or telegraph state. This document exposes the state defensively for that future
  GDD to decide against, but does not itself require or design any HUD element.
- **`audio-system`** — consumes this document's §3.9/§4 Formula 2 timing contract
  (identical normalized progress curve, identical hard-cutoff instant) to render the mandatory
  redundant audio pre-cue (art-bible §7.7). This document owns the timing contract only, not the
  sound asset, mix, or default prominence.

### Adjacent Systems (informational, not a dependency in either direction)

- **`input-targeting-system.md`** (already written) — that document's own Scope and Non-Goals
  section (§3.0) already states that "telegraph timing, vulnerability window duration, or which
  parts are currently vulnerable" are entirely this system's job, and that it only *reads*
  `vulnerable_parts` as one input to its own cycle-order sort key, never writing to it. This
  document is the system that actually populates the field that document reads — the boundary that
  document assumed is confirmed here, not altered.
- **`resonance-weaving-system`** — will define which player abilities carry the
  "counts as an interrupt" property (A4). This document defines only *when* an interrupt window is
  open and *what* happens if one lands (§4 Formulas 3–4), not which player action qualifies as
  landing one.
- **`region-mastery-automation-system`** — not relevant to this document. Its
  work-loop clip selection governs *bound* creatures exclusively; this document governs *hostile*
  creatures exclusively, and per `creature-data-schema.md`'s Edge Case #3, `hostile_state` (and
  therefore this system's entire domain) does not exist on a bound instance.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Category | Locked? | Rationale |
|---|---|---|---|---|---|
| Telegraph wind-up floor | `TELEGRAPH_WINDUP_FLOOR_MS` | Fixed at `600` | Gate | **NON-TUNABLE — hard accessibility floor** | Derived in §3.8/A1 from WCAG 2.3.1 anti-strobe math plus Hick's Law reaction-time reasoning for a 3-way defensive decision. No difficulty setting may lower it. |
| Telegraph reset gap | `TELEGRAPH_MIN_RESET_GAP_MS` | Fixed at `150` | Gate | **NON-TUNABLE — hard accessibility floor** | Independently caps worst-case chained-telegraph flash frequency at 1.33/sec even at the wind-up floor — comfortably under the WCAG 3/sec threshold. |
| Per-attack base wind-up | `AttackDefinition.base_windup_ms` | 600–3000ms | Feel | Free | Authored per attack. Values at/below the floor are legal but produce a wind-up that never scales down with difficulty (Edge Case #12, flagged as a content warning). |
| Wind-up difficulty scaling | `windup_difficulty_scalar` | 0.00–0.06 (default 0.05) | Curve | Free | Controls how fast wind-ups shrink toward the floor across the `power_tier` range; the upper bound is chosen so even adjacent tiers (e.g. 1 vs 2) never feel like a cliff. |
| Telegraph brightness curve | `telegraph_brightness_curve_id` | Curve Library entry (`animation-rig-system.md` A7); default `quad_ease_in` | Feel | Free | Governs the "slow build" shape. Any monotonic ease-in curve is safe; overshoot/elastic curves are disallowed per art-bible §5.5. |
| Glyph brightness range | `resting_intensity` / `peak_intensity` | `[0,1]`; defaults `0.2` / `1.0` | Feel | Free | Shader-parameter range; primarily an art-owned value exposed as data per coding-standards.md. |
| Interrupt window ratios | `interrupt_window_start_ratio` / `_end_ratio` | 0.30–0.85 of wind-up; defaults 0.40 / 0.80 | Feel | Free (bounded by §3.14 invariant 10: end ≤ 1.0) | Positions the highest-cost, highest-reward defense window; too early trivializes reading, too late makes it razor-thin. |
| Dodge window ratios | `dodge_window_start_ratio` / `_end_ratio` | 0.55–1.10 of wind-up; defaults 0.65 / 1.05 | Feel | Free (`_end_ratio` capped at 1.10 by §3.14 invariant 10) | The small >1.0 allowance is deliberate post-cutoff grace, capped to stay a minor forgiveness, not a redesign of the cutoff's meaning. |
| Block window ratios | `block_window_start_ratio` / `_end_ratio` | 0.40–1.00 of wind-up; default 0.50 / 1.00 (`_end_ratio` fixed at 1.00) | Feel | `_end_ratio` locked at 1.00 by §3.14 invariant 10; `_start_ratio` free | Block must cover the cutoff frame exactly, never extend past it — blocking is about eating the hit, not dodging around it. |
| Base vulnerability window | `base_vulnerability_window_ms` | 800–2500ms (default 1500) | Gate | Free | Primary reward-window length; the single biggest lever on how generous the "recognize and burst" loop feels. |
| Vulnerability difficulty scaling | `vulnerability_window_difficulty_scalar` | 0.00–0.04 (default 0.02) | Curve | Free | Deliberately gentler than wind-up scaling (a reward window, not a fairness-critical read window). |
| Vulnerability window design floor | `vulnerability_window_floor_ratio` | 0.4–0.7 (default 0.5) | Curve | Free — **not** the accessibility floor | Guarantees windows stay usably long at max difficulty without being a hard/legal requirement the way §3.8's floor is. |
| Interrupt bonus multiplier | `interrupt_window_bonus_multiplier` | 1.2–2.0 (default 1.5) | Curve | Free | Interrupt's window-length premium over dodge (A5). |
| Vulnerability damage multiplier | `vulnerability_damage_multiplier` | 1.25–2.5 (default 1.5) | Curve | Free | Flat damage bonus during any open window; consumed by `combat-encounter-system`'s (undefined here) damage math. |
| Interrupt break-progress multiplier | `interrupt_break_progress_multiplier` | 1.25–3.0 (default 2.0) | Curve | Free | Interrupt-exclusive second reward channel (A7) — accelerates `PartState.accumulated_value` specifically. |
| Per-attack cooldown | `AttackDefinition.cooldown_ms` | ≥ 0, authored per attack | Gate | Free | Prevents an attack from dominating its pool's weighted draws in practice. |
| Post-cancel re-telegraph delay | `min_post_cancel_delay_ms` | 200–800ms (default 400) | Gate | Free (A9) | Buffers the gap after a part-break-triggered telegraph cancellation (Edge Case #4) so the next telegraph is still readable, not a surprise double-attack. |
| Phase HP thresholds | `Phase.entry_condition.hp_threshold_pct` | `(0.0, 1.0)`, authored per phase | Gate | Free | Recommended authoring pattern: standard creatures 1 desperation threshold (~0.25) and/or a core-adjacent part trigger; bosses 2–3 thresholds (e.g. 0.66 / 0.33) plus a desperation part trigger. |
| Audio pre-cue timing | Curve/cutoff instant (§3.9) | — | Gate | **Timing contract locked** to Formula 2 exactly; prominence/volume is `accessibility-settings-system`'s separate, tunable knob | Audio and visual telegraph channels may never legally desync — that would make one channel actively misleading. |

**MVP scope note**: this document specifies the mechanism in full generality. It does not author
any specific creature's `CreatureBehaviorProfile`, attack list, or phase count — per
`creature-data-schema.md`'s own MVP scope note, MVP populates one creature line (standard tier,
1–2 phases) plus one boss template for the region (recommended 3 phases). The mechanism places no
ceiling on phase count, attack-pool size, or `power_tier` range for Full Vision.

## 8. Acceptance Criteria

1. For every integer `power_tier` in `[1, 20]`, at `windup_difficulty_scalar = 0.06` (the maximum
   safe value, §7) and any `base_windup_ms` in its authored range, Formula 1's output is never less
   than `TELEGRAPH_WINDUP_FLOOR_MS` (600ms) — verified by sweeping the full range and asserting
   every output ≥ 600. **This is the required test proving no difficulty setting can breach the
   accessibility floor.**
2. Formula 1's two worked examples reproduce exactly: `base_windup_ms = 1800`,
   `windup_difficulty_scalar = 0.05`, `power_tier = 5` → `1440ms`; `power_tier = 20` → `600ms`
   (clamped from a raw `90ms`).
3. With hue-drift disabled (`attack_source_hue` locked to `ember_threat_hue` at all times) and
   brightness/timing left unmodified, `brightness(t)` per Formula 2 remains strictly monotonically
   non-decreasing across the entire wind-up, with the cutoff a single-frame discontinuity —
   verified by sampling the curve at every integer rendered frame and asserting no local brightness
   decrease occurs before the cutoff frame. **This is the required test proving telegraph
   brightness/timing alone is sufficient to react correctly, matching art-bible §4.3's design test
   of covering hue in gray and confirming the "safe" vs. "about to hit me" read survives.**
4. Formula 2's worked example reproduces exactly: `resting_intensity = 0.2`, `peak_intensity =
   1.0`, `quad_ease_in`, `windup_duration_ms = 1800`, `elapsed_ms = 900` → `brightness = 0.4`;
   `elapsed_ms = 1800` → `brightness = 1.0` immediately followed, same frame, by a snap to `0.2`.
5. Formula 3's worked examples reproduce exactly the interrupt/dodge/block window boundaries at
   `windup_duration_ms = 1800` (`[720,1440]` / `[1170,1890]` / `[900,1800]`) and at the clamped
   floor of `600ms` (`[240,480]` / `[390,630]` / `[300,600]`).
6. Formula 4's worked example reproduces exactly: `base_vulnerability_window_ms = 1500`,
   `power_tier = 10` → dodge window `1230ms`, interrupt window `1845ms`; and the composition worked
   example reproduces `new_vulnerability_window_ms = max(645, 1230) = 1230ms` with
   `new_vulnerable_parts = {left_claw, core}`.
7. Formula 5's worked example reproduces exactly: a single-tick burst from 100% to 20% HP against
   the 3-phase profile transitions directly from Phase 0 to Phase 2, with Phase 1's `attack_pool`
   never becoming active for even one selection.
8. Formula 6's fallback path: given a phase whose entire non-fallback `attack_pool` has every entry
   currently ineligible (broken parts and/or active cooldowns), attack selection returns
   `fallback_attack_id` on every one of 1000 simulated selection calls, with zero exceptions.
9. Formula 6's weighted-random path: given a pool of three eligible attacks weighted 5/3/2, 10,000
   simulated selections land within ±3 percentage points of the expected 50%/30%/20% distribution.
10. The no-immediate-repeat rule: across 1000 simulated consecutive selections from a pool of ≥ 2
    eligible attacks, the same `attack_id` is never selected twice in a row.
11. Interrupting an in-progress telegraph (Edge Case #2) results, within the same tick, in the
    `Clip` being hard-cut, `vulnerable_parts`/`vulnerability_window_ms` populated per Formula 4's
    interrupt branch, and the interrupted attack's `cooldown_ms` timer starting from that instant —
    verified with no intermediate frame where the cancelled telegraph is still rendering.
12. A core-part break during an in-progress telegraph (Edge Case #5) results in zero further
    telegraph frames rendered and zero `Phase`/vulnerability-window logic executing afterward —
    verified by asserting no mutation of either occurs in the same or any subsequent tick once the
    encounter-end condition has fired.
13. This system's own output into `hostile_state.vulnerable_parts` never includes a `part_id` whose
    `current_stage = crack_stage_count + 1` (broken) at write time — verified across a fixture
    where a part breaks while its own vulnerability window is still nominally open, confirming the
    Formula 4 Part B filter correctly force-clears it, satisfying `creature-data-schema.md`
    invariant 6.
14. Loading an `AttackDefinition` with `interruptible = false` and an `avoidance_options` list that
    also excludes both `block` and `dodge` (zero valid defenses of any kind) is rejected at
    content-load time (Edge Case #10; §3.14 invariant 7). A fixture with `avoidance_options =
    [interrupt]` and `interruptible = true` (interrupt-only) loads successfully.
15. A simulated block input that is active for `[block_window_start_ms, block_window_end_ms]` minus
    one frame in the middle of that span (a brief drop before re-engaging) is scored as a failed
    block, never a successful one; a block input continuously active for the full span is scored as
    a success — verified across both fixtures (Edge Case #11).
