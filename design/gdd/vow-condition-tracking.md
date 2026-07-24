# GDD: Vow Condition Tracking

## Document Status

- **Status**: Complete — all 8 sections authored autonomously this session (rush mode; no user
  available; matching the precedent set this session by `resonance-weaving-system.md` and
  `combat-encounter-system.md`, both authored the same way — see
  `production/session-state/active.md`, "rush mode"). Every ambiguity left open by upstream
  documents and pointed at this system has been resolved with an explicit, flagged design call
  rather than a placeholder. Validate at `/design-review` and `/gate-check` before Production.
- **Owned By**: systems-designer
- **Priority**: MVP, Feature tier — 11th in the recommended design order
  (`design/gdd/systems-index.md`), scheduled immediately after `combat-encounter-system` because
  it cannot be finalized until both `resonance-weaving-system` and `combat-encounter-system` are
  stable (`systems-index.md`, "Circular Dependencies" note — not a true circular *design*
  dependency, see §6). **No reduced MVP scope**: unlike `resonance-weaving-system` (curated
  ability list) or `the-forge-system` (merge-only), Vow-binding itself is "fully live" at MVP
  (resonance-weaving §1) and several curated MVP abilities are already Vow-bound (Thornveil,
  Iron Bulwark, Snare of Whispers, Blightmark — resonance-weaving §3.4), so this document's full
  live-evaluation contract is required from MVP day one, not deferred.
- **Flagged risk** (`systems-index.md`, High-Risk Systems table): "The belt-charm HUD signal's
  legibility at 10–15% frame height was explicitly flagged as unresolved-on-paper in art-bible
  §7.5, with a pre-authorized fallback (mirror larger in the corner cluster). Playtest at
  `/vertical-slice`; fallback is already designed, not blocking." This document's HUD data
  contract (§3.3.6) is deliberately render-location-agnostic so that fallback requires zero
  changes here if it is ever triggered.

## Source Material Read

`design/gdd/resonance-weaving-system.md` (full — owns the Vow data object (§3.3.1), the
`conditional`/`static_cost` split and `gate`/`scale`/`n/a` `effect_mode`s (§3.3.2–3.3.3), the
10-Vow starting catalog and its `condition_type` catalog (§3.3.4), and the cast-start snapshot
rule (§5 Edge Case 3) this document restates, never redefines). `design/gdd/combat-encounter-
system.md` (full — owns the `ACTIVE`-state per-frame step order (§3.2) this document's evaluation
pass slots into, the exact field names for player HP/Resonance/dodge/block/interrupt/damage
events this document reads (§3.1, §3.6, §3.8, §3.10, §4 Formula 3, §3.7 rule 3's "hit"
definition), and the ability-activation gate check (§3.4 rule 2) plus cast-time snapshot read
(§3.4 rule 5) that consume this document's output). `design/art/art-bible.md` §5.2 (the Vow-scar
vs. condition-state distinction this document's brief is built on), §7.3 (the locked "one
discrete flash, never a loop" HUD motion rule), §7.5 (the Combat HUD Specification table, the
belt-zone placement, and the pre-authorized corner-cluster fallback), §7.7 (WCAG 2.3.1 anti-
strobe requirement and the shape-only colorblind design test), §8 (the 60 FPS / 16.6ms frame
budget — `technical-preferences.md`'s own Performance Budgets section is not yet configured with
an explicit figure, so this document treats art-bible §8's stated target as authoritative until
that file is filled in). `design/gdd/creature-ai-telegraph-system.md` (the
`TELEGRAPH_WINDUP_FLOOR_MS = 600` constant and its WCAG 2.3.1 derivation, which this document's
own anti-strobe floor deliberately reuses for consistency). `design/gdd/creature-data-schema.md`
(`CreatureTemplate.tier`, enum `standard` \| `boss` — the exact field `encounter_type_restriction`
reads). `design/gdd/game-concept.md` Pillar 1 (the justification for cast-start sampling, §3.3.6).
`design/gdd/systems-index.md` (this system's dependency map and scheduling note).

## Open Items Resolved From Upstream Documents

| Item | Upstream Provisional Position | This Document's Ruling |
|---|---|---|
| "Live per-frame Vow condition evaluation + HUD signal" (resonance-weaving's own New Open Items table) | Deferred entirely to this document, owner named as `vow-condition-tracking` | §3.3.1–3.3.6 fully specify it |
| Whether a Vow condition flip can strobe the HUD (art-bible §7.7's WCAG 2.3.1 requirement names no owning system) | Open | §3.3.4 / §4 Formulas 1–2 — a two-layer, provably-bounded hysteresis/debounce mechanism |
| Does the closed/open ring apply to `static_cost` Vows? (implied by resonance-weaving §3.3.1's `effect_mode: n/a`) | Not addressed | §3.3.5/§3.3.6 — no; excluded entirely from evaluation and from the HUD data contract |
| What counts as "an attack" for `cyclic_attack_trigger` (resonance-weaving §5 Edge Case 1's own flagged ambiguity — "an ability whose own Form never counts as a 'basic attack' for that counter") | Explicitly left open; that document's own ruling is "not runtime-blocked... fails inert, not fail loud" if unsatisfiable | §3.3.2 — resolved: reuses combat-encounter-system §3.7 rule 3's exact "nonzero `final_damage`" hit definition, extended by §5 Edge Case 8's Mark-exclusion precedent |
| Does blocked chip damage or a landed interrupt count against `no_damage_taken` / `no_dodge_or_block_used`? | Not addressed by either upstream document | §3.3.2 — resolved explicitly, cross-checked against each Vow's own authored `expected_uptime` for internal consistency |

---

## 1. Overview

Vow-condition-tracking is the runtime nervous system for every `conditional` Vow resonance-
weaving-system defines: the system that decides, frame by frame, whether each of the Hunter's up
to four equipped Vow-bound ability slots is currently *satisfied* or *violated*, and feeds that
single decision to the two places it matters — the actual mechanical behavior combat-encounter-
system applies to that slot (whether the ability can be cast at all, for `gate` mode; whether its
`power_multiplier` bonus applies, for `scale` mode) and the diegetic belt-ring signal art-bible
§7.5 locks as the player's only real-time window into that fact. This document owns nothing about
what a Vow *is* or what a `condition_type` *means* (resonance-weaving-system's exclusive domain)
and nothing about how an ability's damage is ultimately computed once its multiplier is known
(combat-encounter-system's exclusive domain) — it owns exactly one thing, precisely: the live
boolean signal `is_satisfied`, per equipped conditional-Vow slot, computed cheaply enough to be
functionally free against the 60 FPS / 16.6ms frame budget, resistant enough to threshold-
hovering and event-storm noise to never breach the WCAG 2.3.1 anti-strobe floor creature-ai-
telegraph-system already established at 600ms, and honest enough that the ring the player is
trained to trust never once shows a state the ability's actual behavior disagrees with.

## 2. Player Fantasy

resonance-weaving-system already gave the player the *choice* — bind a brutal, rare condition to
an ability in exchange for a power spike no safe build can match. This document is where that
choice becomes a livable, moment-to-moment *feeling* instead of a fact printed on an item
tooltip. The fantasy is not "I have a conditional ability" — it's "I am currently below 30%
health, on purpose, because I know that's exactly the state my Ember Lance needs to hit like a
truck, and I can see, right now, on my own belt, that the gamble is live." A glance at four rings
— closed, closed, open, closed — tells a player everything: which of their four bets is currently
paying off, and which one is dead weight until the fight's rhythm swings back their way. This is
the direct combat-loop expression of Self-Determination Theory's Competence need (resonance-
weaving §2 cites the same framework for the build layer itself): a Vow's power is only real to the
player if they can watch, in real time, whether they've earned it — an invisible condition is a
condition the player cannot skillfully court, only guess at, which quietly turns a precision build
into a slot machine. It is also where Pillar 1 (Precision Over Reflexes) gets its sharpest edge: a
`vow_untouchable` player isn't reacting to a health bar, they're *reading* an open ring the instant
their last dodge lands and immediately knowing their gate-mode ultimate window is still alive — the
ring is the pattern, and reading it correctly under pressure, without it ever lying or flickering,
is the entire skill test this system exists to make fair.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the live per-encounter evaluation of every conditional Vow's `is_satisfied`
boolean (§3.3.1–3.3.4), the hysteresis/debounce mechanism that keeps that boolean's flip rate
within the WCAG 2.3.1 anti-strobe floor (§3.3.4, §4), the exact relationship between this
document's continuous evaluation and combat-encounter-system's cast-time snapshot read (§3.3.6,
restated not redefined), and the data contract `combat-hud` renders against
(§3.3.6). It does **not** own: what a Vow is, what a `condition_type` means, or the
`power_multiplier`/`gate`/`scale`/`static_cost` semantics themselves (resonance-weaving-system's
exclusive domain — restated only, never redefined, throughout this document); the encounter state
machine, the damage pipeline, or which combat events fire when (combat-encounter-system's
exclusive domain — this document only *reads* that document's already-committed per-frame state,
never writes to it, and only *writes* its own internal per-slot counters/flags); ring rendering,
belt-charm placement, or flash animation playback timing/easing (art-bible §7.5 and `combat-hud`'s
domain — this document exposes state only, §3.3.6).

### 3.1 The Evaluation Engine — When It Runs

1. **Gated by encounter state.** Evaluation occurs only while combat-encounter-system's encounter
   state machine (§3.2 there) is in `ACTIVE`. No evaluation runs during `NOT_STARTED`,
   `ENGAGING`, `RESOLVING`, or `COMPLETE` — no player input is accepted in any of those states
   either (combat-encounter-system §3.2's state table), so there is nothing for a live condition
   signal to gate.
2. **A single pass, once per frame, positioned precisely.** This document's evaluation pass runs
   immediately **after** combat-encounter-system §3.2 step 5 (Damage/Health/Part-State
   Application) commits and **before** step 6 (Encounter-End Check). Every value this pass reads
   — `player_current_hp`, `current_resonance`, elapsed `ACTIVE`-state time, the running
   attack-landed counters, the dodge/block/damage flags — is therefore that frame's fully-resolved,
   authoritative state, never a partial or mid-update read.
3. **One-frame data-flow lag, by design, not by accident.** The `is_satisfied` values this pass
   produces become the input the *next* frame's combat-encounter-system §3.2 step 3 (Player Input
   Resolution — gate-mode eligibility) and step 5 (ability cast — the `power_multiplier` snapshot
   read, §3.3.6) consume. This introduces a fixed, bounded lag of at most one frame (≤16.6ms at
   60 FPS) between an underlying condition's state changing and that change becoming legible to
   input resolution — imperceptible against any human reaction time, and it guarantees perfect
   frame-to-frame consistency: every consumer within a given frame reads the exact same immutable
   snapshot, so there is no read-during-write hazard.
4. **Bootstrap — the initial pass.** At the instant `ACTIVE` begins (immediately following
   `ENGAGING`), this document performs one initial evaluation pass for every equipped conditional
   Vow **before any player input is accepted in that frame**, establishing each slot's starting
   `is_satisfied` value from a direct, non-hysteresis evaluation of the condition's definition
   against the true starting game state (full HP, 0 Resonance, 0 elapsed time, 0 attack count, no
   marks cast, no damage taken, no dodge/block used yet — §3.3.2's table specifies each starting
   value precisely). **This initial render is a state, not a transition** — per art-bible §7.3's
   rule that a HUD flash represents a state *change*, the initial pass never queues a flash (§5
   Edge Case 5). By the time step 3 of `ACTIVE`'s very first frame runs, every slot's
   `is_satisfied` is already populated — there is no undefined/null gap at encounter start.
5. **`static_cost` Vows and empty slots are skipped entirely.** Zero evaluation work is performed
   for a slot holding a `static_cost` Vow or holding no item at all — not merely hidden, genuinely
   never evaluated (§3.3.5, §3.3.6).
6. **Teardown.** All of this document's own internal state for every slot (counters, flags,
   `last_marked_part_id`, `last_flip_time_ms`) is discarded at the `ACTIVE → RESOLVING`
   transition and never carried into the next encounter — matching the project's established
   "no carry-over between encounters" pattern (Resonance §3.6, Traps §5 Edge Case 9, Hazards
   §3.10 — all `combat-encounter-system`/`resonance-weaving-system`). The next `ACTIVE` entry
   always starts from a fresh initial pass (§5 Edge Case 7).

### 3.2 Condition-Type Taxonomy — Evaluation Cost and Cadence

Every `condition_type` resonance-weaving-system's illustrative catalog names (§3.3.4 there, 10
values) falls into exactly one of six categories, defined by *what it costs to evaluate* and
*how often its underlying state can change* — not by an arbitrary content grouping:

| `condition_type` | Category | Raw Evaluation | State Read | Stateful Memory (owned here) | Flip risk w/o debounce |
|---|---|---|---|---|---|
| `health_below_percent_threshold` | **A** — Continuous Threshold | `player_current_hp / hunter_max_health × 100 < threshold_percent` | `player_current_hp`, `hunter_max_health` (combat-encounter-system §3.1/§3.8) | None (stateless) | High — the brief's own named example |
| `health_above_percent_threshold` | **A** — Continuous Threshold | mirror, `>` | same | None | High |
| `resonance_above_threshold` | **A** — Continuous Threshold | `current_resonance / resonance_cap × 100 > threshold_percent` | `current_resonance`, `resonance_cap` (combat-encounter-system §3.6) | None | Moderate — Resonance is gain-only except a full-pool Ultimate spend today, but future content may not preserve that |
| `resonance_below_threshold` | **A** — Continuous Threshold | mirror, `<` | same | None | Moderate |
| `cyclic_attack_trigger` | **C** — Event-Counted (Stateful) | `true` for `[trigger_time_ms, trigger_time_ms + event_triggered_window_ms]`, or until consumed by a successful gated cast, whichever is first (§4 Formula 3) | An attack-landed event: any basic-attack tick, ability, or ultimate cast resolving a nonzero `final_damage` (combat-encounter-system §3.7 rule 3's exact "hit" definition, reused verbatim — §3.2 Design Call below) | A per-slot counter (`0 → trigger_every_n_attacks`, resets to `0` on trigger) + `trigger_time_ms` | Low structurally, but §4 Formula 2's universal floor is the load-bearing guarantee if trigger cadence is ever authored aggressively |
| `encounter_type_restriction` | **D** — Static Per-Encounter | `target_creature.tier == condition_params.restricted_to` (`creature-data-schema` `CreatureTemplate.tier`, enum `standard` \| `boss`) | Read once at the initial pass (§3.1 rule 4), cached; never re-evaluated | One cached boolean | None — cannot change mid-encounter |
| `target_part_lock` | **E** — Continuous + Stateful Memory | `selected_part_id != null AND selected_part_id == last_marked_part_id` | `selected_part_id` (input-targeting-system, read via combat-encounter-system §3.3 rule 2) | `last_marked_part_id`, updated whenever a Mark-Form ability cast resolves (`WovenAbilityDefinition.form == "mark"`, resonance-weaving §3.6) — **global per-encounter, not per-slot** (§3.2 Design Call below) | Moderate — `selected_part_id` updates continuously on mouse hover |
| `encounter_time_elapsed_above` | **B** — Continuous Threshold, Monotonic | `elapsed_active_ms / 1000 > threshold_seconds` | combat-encounter-system §4 Formula 7's `active_clear_time_seconds`, read as its running elapsed-so-far value during `ACTIVE` (rather than duplicating a second timer) | None (reads an existing running value) | None — elapsed time within one encounter never decreases, so once true it is true for the rest of the encounter by construction |
| `no_dodge_or_block_used` | **F** — Event-Counted, Monotonic | Starts `true`; flips to `false`, permanently, on the first successful dodge or block this encounter (never re-flips true) | combat-encounter-system §3.5 (defensive-triad resolution) | One boolean flag per slot | None — one-way, at most one flip per encounter, ever |
| `no_damage_taken` | **F** — Event-Counted, Monotonic | Starts `true`; flips to `false`, permanently, on the first nonzero `final_damage` applied to the player this encounter (§3.2 Design Call below) | combat-encounter-system §3.10 / §4 Formula 3 | One boolean flag per slot | None — one-way |

**Design Call — what counts as "an attack" for `cyclic_attack_trigger`.** resonance-weaving-system
§5 Edge Case 1 explicitly flags this as open ("an ability whose own Form never counts as a 'basic
attack' for that counter"). This document resolves it: the counter increments on **any** of the
Hunter's damage-dealing events — basic attacks and every ability/ultimate cast, from any equipped
slot, including individual Aura ticks and Summon attacks as their own discrete events — that
resolves a nonzero `final_damage` through combat-encounter-system's pipeline, reusing that
document's own §3.7 rule 3 "hit" definition verbatim rather than inventing a second one. Extending
combat-encounter-system §5 Edge Case 8's own precedent for consistency: a Mark-Form cast never
increments this counter (Mark "never contributes to part-break accumulation directly... excluded
from `total damage-dealing hits`" there — this document treats it identically for "attack"
purposes), and a Trap-Form cast increments the counter only at the moment its payload actually
resolves and deals damage, never at arm/cast time (Trap "does not deal damage immediately," §3.2
there). The counter is Hunter-wide combat rhythm, not scoped to the bound ability's own casts
alone. If two equipped items both carry a `cyclic_attack_trigger`-type Vow (resonance-weaving §5
Edge Case 7's already-blessed "duplicate `vow_id`" stacking case), each slot maintains its own
fully independent counter against the same shared stream of attack-landed events — meaning two
identical Vows on two different slots trigger in lockstep, a deliberate consequence of that Edge
Case's own ruling, not a bug introduced here.

**Design Call — does blocked damage or a landed interrupt count against `no_damage_taken` /
`no_dodge_or_block_used`?** Neither upstream document addresses this. This document resolves it by
cross-checking each Vow's own authored `expected_uptime` (resonance-weaving §3.3.4) for internal
consistency: `vow_untouchable`'s `expected_uptime = 0.05` is only coherent if "no damage taken"
means literally any nonzero HP loss — **including** block's chip damage (`block_damage_
reduction_percent`, default 80%, still leaves 20% through, combat-encounter-system A6) — because
block is "always available... no charge, no cooldown" (§3.5 there); if blocking counted as "no
damage taken," a player could trivially hold this Vow near 100% uptime just by blocking
everything, contradicting the authored 5%. Only a fully-negated attack (successful dodge or
interrupt) preserves the condition. Conversely, `vow_unshaken`'s `expected_uptime = 0.55` — a much
higher, easier-to-hold number — is only coherent if "no dodge or block **used**" means literally
that: the player may still take raw, unavoided hits (a legitimate tactical choice under this Vow,
since taking a hit costs neither a dodge charge nor a block hold) and may still land interrupts
(an aggressive, ability-based action, not a defensive *tool* this Vow restricts) without violating
it. Both readings are the only ones consistent with their own authored rarity values, which this
document treats as tie-breaking evidence rather than an arbitrary choice.

### 3.3 Hysteresis and Debounce

Every Category A/B/E raw evaluation is capable, in principle, of oscillating rapidly if its
underlying metric hovers near a boundary — the brief's own named risk. This document applies two
independent, **stacked** layers of protection to a single authoritative `is_satisfied` value —
never a separate "true" value plus a separate "display" value; a desync between what an ability
actually does and what its ring shows is precisely the failure mode the brief names as
unacceptable ("a Vow whose condition is unmet means that ability slot does nothing... the player
MUST know this in real time"):

1. **Threshold hysteresis (dead-band / Schmitt trigger, §4 Formula 1)** — applied only to Category
   A's continuous numeric-threshold conditions. A fixed percentage buffer around `threshold_percent`
   means the raw evaluation must travel *past* the threshold by `dead_band_percent` in the
   direction of a flip, then travel back *past* it by the same margin in the opposite direction,
   before a second flip can occur. A value bouncing within that band never flips at all. This is a
   **value-domain** protection: it makes the underlying oscillation itself rarer, and it is the
   direct answer to "the player hovering exactly at a threshold boundary" (§5 Edge Case 5).
2. **Minimum flip interval floor (§4 Formula 2)** — applied universally, to every `condition_type`
   in every category, with no exceptions. Regardless of how the raw evaluation behaves — a
   threshold hovering, an event firing repeatedly, a future `condition_type` this document has
   never seen — the authoritative `is_satisfied` value is hard-capped at one flip per
   `VOW_FLIP_MIN_INTERVAL_MS` (locked at 600ms, §7). This is a **time-domain** protection, and it
   is the one that actually *proves* the anti-strobe guarantee (§4 Formula 2). Formula 1 makes
   flips rarer in the common case; Formula 2 makes exceeding a safe rate mathematically impossible
   in every case, including ones Formula 1 was never designed to cover.

### 3.4 The Satisfied/Violated Contract

`is_satisfied` (the output of §3.3's two-layer debounce) is read by exactly two consumers, and
both read the identical value:

- **combat-encounter-system's ability-activation gate** (§3.4 rule 2 there) — for a `gate`-mode
  conditional Vow, `is_satisfied = false` means the ability cannot be activated at all this frame
  (rejected, as if off cooldown); `is_satisfied = true` means it may be activated, subject to its
  own cooldown independently. This document restates, and does not redefine, that rule.
- **combat-hud's ring render** (§3.3.6) — `is_satisfied = true` renders the closed ring (satisfied,
  art-bible §3.2's container logic); `is_satisfied = false` renders the open ring (violated).

For a `scale`-mode conditional Vow, `is_satisfied` never gates activation — the ability is always
castable. What it governs is which `power_multiplier` combat-encounter-system's cast-time
snapshot (§3.3.6) reads at the instant of cast: resonance-weaving-system's derived
`power_multiplier` (Formula 1 there) if `is_satisfied = true` at that instant, or an unmultiplied
baseline of exactly `1.0` if `is_satisfied = false` — this document does not compute either
number, only supplies the boolean combat-encounter-system's own formula reads (resonance-weaving
§3.3.2, restated not redefined).

For a `static_cost` Vow (`effect_mode: n/a`, resonance-weaving §3.3.1), there is no live condition
and therefore no `is_satisfied` value at all — this document performs zero evaluation work for
these slots (§3.1 rule 5) and they are entirely absent from the HUD data contract (§3.3.6), never
rendered as a permanently-closed or otherwise-meaningless ring. An empty loadout slot (resonance-
weaving §5 Edge Case 5) is handled identically — absent, never a default-false ring.

### 3.5 Mid-Cast Sampling — Restated Authoritative Rule

resonance-weaving-system §5 Edge Case 3 and combat-encounter-system §3.4 rule 5 / §5 Edge Case 3
already lock this rule jointly; this document's obligation is to state precisely how its own
continuous `is_satisfied` signal relates to that lock, since the two are easy to conflate:

- **`is_satisfied` is continuous and live, always.** This document never stops evaluating a slot's
  condition just because that slot's ability happens to be mid-cast (an Aura ticking, a Summon
  active, a Trap armed). The ring the player sees is *always* the current truth of the condition.
- **combat-encounter-system's `power_multiplier` snapshot is a one-time read of `is_satisfied`,
  taken at the exact frame combat-encounter-system processes that specific cast's activation
  (§3.2 step 5 there) — never earlier, never later, and never re-read for the remainder of that
  cast's active duration.** This document performs no snapshotting itself; it only guarantees that
  whatever value combat-encounter-system reads at that instant is the correct, fully-debounced,
  current truth.
- **A real, deliberate decoupling worth flagging**: the ring can keep flipping *while* an
  already-cast ability's locked-in multiplier keeps applying unchanged. A player who cast an Aura
  at `vow_bloodied`'s 2.28× while below 30% HP, and whose fight state changes mid-Aura, will see
  the belt ring flip to open (violated) mid-Aura — while the Aura itself keeps ticking at the
  multiplier it locked in at cast time, completely unaffected. This is not a bug; the ring answers
  "is my condition true right now, for my *next* decision," not "what multiplier is currently
  applied to my last one" (restated as §5 Edge Case 3, since it is a real, first-time-confusing
  moment worth designing for rather than discovering in playtest).

**Why cast-start sampling is the right call against Pillar 1.** This document did not invent the
rule (it is locked upstream), but the justification is worth restating here since this document's
own live signal is what makes the tempting alternatives visible. A continuously-resampled
multiplier would mean an Aura's reward is decided by whatever happens to the player's HP/
Resonance/attack-count during the several seconds *after* they committed to the cast — turning
"did I read the pattern correctly at the moment I acted" into "did I get lucky with what happened
next," a reflex/chance axis, not the planning/reading axis Pillar 1 protects. Resolution-time
sampling is worse still for anything but an instant Form: a 10-second Aura's reward would be
decided by a single frame the player has no way to anticipate at cast time, undermining the
"decide, then know" promise the belt ring exists to deliver. Cast-start sampling is the only model
where the ring's promise ("here is what's currently true, so you can decide") and the mechanical
outcome (what that decision actually earned) are the same fact, captured at the same instant —
exactly the "the hunter has already read the pattern" confidence game-concept.md and this
document's own §2 both name as the target feeling.

### 3.6 The HUD Data Contract

`combat-hud` is the sole consumer of this section. This document exposes, per
equipped loadout slot (`ability_1`, `ability_2`, `ability_3`, `ultimate` — resonance-weaving
§3.5's 4 fixed slots), the following read-only structure, recomputed at the end of every frame's
evaluation pass (§3.1 rule 2):

```
VowConditionSlotState {
  slot_id:                 enum { ability_1, ability_2, ability_3, ultimate }
  vow_id:                  string | null   // null = no item equipped, or an equipped item with no bound Vow
  ring_applicable:         bool            // true only if vow_id is non-null AND that Vow's category == "conditional"
  effect_mode:             enum { gate, scale, n/a }   // restated from resonance-weaving §3.3.1 — no second lookup needed
  is_satisfied:            bool | null     // the debounced, authoritative value (§3.3–3.4); null when ring_applicable is false
  last_flip_timestamp_ms:  float | null    // encounter-relative ms of the most recent is_satisfied transition; null if none yet (the initial pass is never a transition, §3.1 rule 4)
}
```

- **`static_cost` Vows and empty slots always resolve `ring_applicable: false`, `is_satisfied:
  null`** — `combat-hud` renders nothing for these; art-bible §7.5's table only specifies a
  Vow-condition ring for conditional Vows, and this document guarantees `combat-hud` never has to
  guess that on its own.
- **The shape mapping is this document's obligation, not `combat-hud`'s discretion**:
  `is_satisfied: true` → closed ring; `is_satisfied: false` → open ring (art-bible §3.2).
  `last_flip_timestamp_ms` lets `combat-hud` compute whether the one-shot flash animation
  (art-bible §7.3) should still be playing (`now_ms − last_flip_timestamp_ms < flash_duration_ms`,
  a value `combat-hud`'s own GDD will own) — this document supplies the timestamp, not the
  animation's duration or easing.
- **Render-location agnostic, by design.** This contract says nothing about *where* the ring
  renders — the belt-charm zone (art-bible §5.2/§7.5's primary placement) or the pre-authorized
  corner-cluster fallback (art-bible §7.5's own flagged open playtest risk at 10–15% frame height)
  both consume the identical structure unchanged. If Vertical Slice playtesting triggers that
  fallback, `combat-hud` mirrors the same `VowConditionSlotState` array at larger scale — zero
  changes required in this document.
- **Simultaneous-flip ordering.** When 2+ slots' `is_satisfied` values change in the same frame,
  this document exposes all of them with the *same* `last_flip_timestamp_ms` (the mechanical truth
  is simultaneous, §4 Formula 4). Any visual staggering of the flash *beat* itself is `combat-hud`'s
  presentation-layer responsibility, using the fixed slot-priority order (`ability_1 → ability_2 →
  ability_3 → ultimate`) and `SIMULTANEOUS_FLIP_STAGGER_MS` (§7) this document recommends, so no
  two implementations invent a different order.

## 4. Formulas

Five formulas. Formulas 1 and 2 together are the load-bearing anti-strobe guarantee the brief
calls "the most important thing in this GDD" — Formula 1 reduces flip frequency in the common
case, Formula 2 unconditionally caps it in every case. Formula 5 is the required cadence/cost
proof against the 16.6ms frame budget.

### 4.1 Formula 1 — Threshold Hysteresis Band (Schmitt Trigger)

```
For condition_direction == "below" (health_below_percent_threshold, resonance_below_threshold):
  enter_threshold = threshold_percent − dead_band_percent
  exit_threshold  = threshold_percent + dead_band_percent
  raw_satisfied(t) =
    if raw_satisfied(t⁻) == false:  metric_percent(t) < enter_threshold
    if raw_satisfied(t⁻) == true:   NOT (metric_percent(t) > exit_threshold)

For condition_direction == "above" (health_above_percent_threshold, resonance_above_threshold):
  enter_threshold = threshold_percent + dead_band_percent
  exit_threshold  = threshold_percent − dead_band_percent
  raw_satisfied(t) =
    if raw_satisfied(t⁻) == false:  metric_percent(t) > enter_threshold
    if raw_satisfied(t⁻) == true:   NOT (metric_percent(t) < exit_threshold)

Bootstrap (t = first sample of the encounter): no t⁻ exists — raw_satisfied(0) is the plain,
non-hysteresis comparison (metric_percent(0) < threshold_percent, or > for "above"). Hysteresis
engages from the second sample onward.
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `threshold_percent` | float | (0, 100) | Author-set threshold from `condition_params` (e.g. `30` for `vow_bloodied`). |
| `dead_band_percent` | float, tuning knob | 1–8 (default `3`) | Half-width of the no-flip buffer zone straddling `threshold_percent`. |
| `metric_percent(t)` | float, external input | [0, 100] | Current value of the tracked metric (HP% or Resonance%) at evaluation tick `t`. |
| `condition_direction` | enum | `{below, above}` | Which comparison direction this `condition_type` uses. |
| `raw_satisfied(t)` | bool | — | The hysteresis-filtered boolean at tick `t` — feeds directly into Formula 2 as its input. |

**Output range**: `raw_satisfied` is a plain boolean, but its *transition* count is structurally
bounded — for it to flip and flip back, `metric_percent` must travel the full `2 × dead_band_
percent` width of the band, not merely graze `threshold_percent`. This is the formula's entire
purpose: at any `dead_band_percent > 0`, a value oscillating strictly within `[enter_threshold,
exit_threshold]` produces **zero** flips, by construction.

**Worked example** (`vow_bloodied`, `threshold_percent = 30`, default `dead_band_percent = 3` →
`enter_threshold = 27`, `exit_threshold = 33`; player HP% sampled each evaluation tick, starting
`raw_satisfied = false`):

| HP% sample | Naive `< 30` check | `raw_satisfied` (hysteresis) | Why |
|---|---|---|---|
| 35 | false | false | above the band |
| 32 | false | false | inside `[27,33]` → **HOLD** |
| 29 | **true** ← would flip | false | inside `[27,33]` → **HOLD** |
| 31 | false ← would flip back | false | inside `[27,33]` → **HOLD** |
| 28 | **true** ← would flip | false | inside `[27,33]` → **HOLD** |
| 34 | false | false | above `exit_threshold`, but already `false` — no-op |
| 26 | true | **true** | `26 < 27` (enter) → **FLIPS true** |
| 32 | false | true | inside `[27,33]`, currently `true` → **HOLD** |
| 34 | false | **false** | `34 > 33` (exit) → **FLIPS false** |

The naive `< 30` check would have flipped **5 times** across this 9-sample sequence (a strobe
risk). The hysteresis-filtered `raw_satisfied` flips exactly **2 times** — once on a genuine,
sustained drop below the band, once on a genuine, sustained recovery above it. This is the direct
proof for §5 Edge Case 5 ("the player hovering exactly at a threshold boundary").

### 4.2 Formula 2 — Minimum Flip Interval Floor (the Anti-Strobe Proof)

```
can_flip(t)              = (t − last_flip_time_ms) ≥ VOW_FLIP_MIN_INTERVAL_MS
debounced_satisfied(t)   = raw_satisfied(t)              if can_flip(t) AND raw_satisfied(t) ≠ debounced_satisfied(t⁻)
                          = debounced_satisfied(t⁻)       otherwise
on flip: last_flip_time_ms ← t; queue exactly one HUD flash for this slot
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `VOW_FLIP_MIN_INTERVAL_MS` | int, **LOCKED** | fixed `600` | Non-tunable anti-strobe floor — reuses `creature-ai-telegraph-system`'s identical `TELEGRAPH_WINDUP_FLOOR_MS` constant and WCAG 2.3.1 rationale for consistency (one named floor value the whole project shares). |
| `last_flip_time_ms` | float, internal state | ≥ 0 | Encounter-relative timestamp of this slot's most recent `debounced_satisfied` transition. |
| `t` | float | ≥ 0 | Current encounter-relative frame timestamp. |
| `raw_satisfied(t)` | bool | — | Formula 1's output (Category A) or the category-appropriate raw evaluation (§3.2) for every other category. |
| `debounced_satisfied(t)` | bool | — | **The authoritative `is_satisfied` value** — read by both the ability gate/scale logic (§3.4) and the HUD (§3.6). |

**Output range and proof**: `debounced_satisfied` can change value at most once per
`VOW_FLIP_MIN_INTERVAL_MS`, by construction — `can_flip(t)` is `false` for the entire interval
following any flip, unconditionally, regardless of how `raw_satisfied` behaves during that window.
Maximum flip frequency = `1000 / 600 ≈ 1.667` flips/second. Applying the more conservative, formal
WCAG reading (a "flash" = one full opposite-direction pair, i.e. two flips), the maximum *flash*
rate is `≈ 0.833` cycles/second — a **3.6× margin** under the WCAG 2.3.1 general-flash threshold
of "more than three flashes in any one-second period." Even under the stricter interpretation that
treats every individual flip as its own potential flash event, `1.667` flips/second is still an
**1.8× margin** under the 3/second threshold. This proof holds **unconditionally** — it does not
depend on any assumption about how fast `metric_percent` or an event stream can change, unlike
Formula 1, which only reduces flip frequency for the specific Category A case it targets.

**Worked example (pathological stress test)**: a hypothetical future `cyclic_attack_trigger` Vow
authored with `trigger_every_n_attacks = 1`, paired with a build somehow landing 10 attacks/second
(unrealistic given combat-encounter-system's cadence/cooldown constraints, presented purely as an
adversarial upper bound) would produce `raw_satisfied` toggling up to 10 times/second without
Formula 2. With Formula 2 applied, `debounced_satisfied` is still hard-capped at `1.667`
flips/second regardless — the floor holds even under inputs Formula 1 was never designed to
address, which is exactly why it is the load-bearing, non-tunable half of this mechanism.

### 4.3 Formula 3 — Event-Triggered Window (`cyclic_attack_trigger`)

```
window_open_until_ms = trigger_time_ms + event_triggered_window_ms
raw_satisfied(t) = true   for trigger_time_ms ≤ t ≤ window_open_until_ms, UNLESS the gated
                           ability is successfully cast before window_open_until_ms (consumed
                           early)
                 = false  otherwise
on flip to false (consumed or expired): attack_counter resets to 0, resumes counting toward the
next trigger_every_n_attacks
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `event_triggered_window_ms` | int, tuning knob | 800–3000 (default `1500`) | How long the triggered state stays `true` before auto-expiring if the gated ability is never cast. |
| `trigger_time_ms` | float, internal state | ≥ 0 | Encounter-relative timestamp the `Nth` attack landed. |
| `window_open_until_ms` | float | ≥ `trigger_time_ms` | Computed expiry deadline. |

**Output range**: `raw_satisfied` is `true` for a bounded window of at most
`event_triggered_window_ms`, ended early by consumption. This guarantees the triggered state can
never persist indefinitely (no infinite-banked-buff degenerate case) and always resolves to a
concrete `false` before the next trigger cycle can begin.

**Worked example**: `vow_ten_blows`, `trigger_every_n_attacks = 10`, default `event_triggered_
window_ms = 1500`. The 10th attack lands at `trigger_time_ms = 12,400`.
`window_open_until_ms = 13,900`. If the gated ability is cast at `t = 13,100` → consumed,
`raw_satisfied` flips `false` immediately at `13,100` (counter resets, begins counting toward the
next 10). If it is never cast → auto-expires at `t = 13,900`, flips `false`, counter resets
identically.

### 4.4 Formula 4 — Simultaneous-Flip Flash Stagger (Presentation Only)

```
For k slots (k ≤ 4, the loadout cap) whose debounced_satisfied flips in the same frame at
frame_time_ms, ordered by fixed slot priority (ability_1, ability_2, ability_3, ultimate):

  debounced_satisfied[i] updates at frame_time_ms for ALL i — simultaneously, with zero delay
  flash_fire_time[i] = frame_time_ms + (i × SIMULTANEOUS_FLIP_STAGGER_MS),  i = 0 .. k−1
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `SIMULTANEOUS_FLIP_STAGGER_MS` | int, tuning knob | 40–150 (default `80`) | Offset between successive queued flash *beat* start times when multiple slots flip in the same frame. **Cosmetic only** — never delays the mechanical `is_satisfied` value itself. |
| `k` | int | [0, 4] | Number of slots flipping in the same frame, bounded by the locked 4-slot loadout cap (resonance-weaving §3.5). |

**Output range**: mechanical correctness (`debounced_satisfied`) is never delayed — a gate-mode
ability that becomes eligible this frame is eligible for every consumer immediately, preserving
fairness. Only the *flash* animation's playback start is spread across at most `(k−1) ×
SIMULTANEOUS_FLIP_STAGGER_MS` ≤ `3 × 150 = 450ms` at the widest safe-range extreme, `240ms` at
default tuning for the worst case (`k = 4`).

**Worked example (§5 Edge Case 6)**: 4 Vows flip in the same frame at `t = 8000ms`. All 4 ring
*shapes* update to their new state instantly at `8000ms` — no player ever sees a stale shape.
Flash beats fire at `8000, 8080, 8160, 8240ms` respectively (default `80ms` stagger) — spread
across `240ms` total. Each individual belt-charm location still flashes exactly **once**, so this
is nowhere near "3+ flashes within one second" at any single screen location — the stagger is a
legibility improvement (per art-bible §7.5's working-memory glance-load discussion), not a
requirement for WCAG compliance, which is already independently satisfied per-location.

### 4.5 Formula 5 — Evaluation Cadence Cost Bound (16.6ms Budget)

```
frame_eval_cost_ms ≈ n_active_conditional_vows × c_per_condition
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `n_active_conditional_vows` | int | [0, 4] | Number of equipped `category: conditional` Vows this frame — hard-capped by resonance-weaving §3.5's locked 4-slot loadout, and further reduced in practice since `static_cost` Vows and empty slots cost `0` (§3.1 rule 5). |
| `c_per_condition` | float, ms | conservatively bounded at `0.0005` (500 nanoseconds) | Cost of one `condition_type`'s per-frame evaluation — a handful of float comparisons and a struct field read/write (§3.2's table shows every category resolves to `O(1)` work; no category requires a loop, allocation, or search). |
| `frame_eval_cost_ms` | float, ms | ≥ 0 | Output — total per-frame cost of this document's entire evaluation pass. |

**Output range**: bounded above by `4 × 0.0005 = 0.002ms` even at this document's own deliberately
conservative per-condition cost estimate (real cost on modern hardware is very likely an order of
magnitude smaller — no branch requires anything beyond a comparison and a boolean write).

**Worked example / budget check**: `0.002ms / 16.6ms ≈ 0.012%` of the frame budget — three orders
of magnitude below any meaningful contention with rendering, creature AI (`creature-ai-telegraph-
system`), or the damage pipeline (`combat-encounter-system` §4 Formula 3). Even a `10×` pessimistic
re-estimate of `c_per_condition` (5 microseconds instead of 500 nanoseconds, covering unforeseen
overhead) still yields `4 × 0.005 = 0.02ms`, or `0.12%` of budget — this document's evaluation
pass is not, and structurally cannot become, a performance concern at the locked 4-slot cap.

## 5. Edge Cases

1. **A Vow's condition can never be satisfied given the player's other choices** (e.g.
   `vow_ten_blows` bound to an ability whose Form's own casts never land a qualifying "hit," or
   `vow_boss_bound` on a build that never fights bosses). **Ruling: aligned with resonance-weaving
   §5 Edge Case 1's "fail inert, not fail loud" precedent.** `is_satisfied` simply evaluates
   `false` forever (`raw_satisfied` never becomes `true`, no crash, no special-case detection by
   this document). The HUD ring stays open (violated) for the entire encounter and zero flashes
   ever fire for that slot, since a value that never changes never transitions. This is a valid,
   inert state, not an error condition this document is responsible for detecting.
2. **Two equipped Vows have contradictory conditions** (e.g. one slot requires health below 30%,
   another requires Resonance above 80%). **Ruling: aligned with resonance-weaving §5 Edge Case
   2's "not a real conflict."** Each slot's `is_satisfied` is computed completely independently
   against the same live game state — this document's evaluation pass reads shared underlying
   state (HP, Resonance, etc.) but never shares evaluated results between slots. Both conditions
   may be simultaneously true, simultaneously false, or split; no interaction, no special
   handling.
3. **A condition flips on the exact frame an ability resolves.** Fully specified by §3.5's
   mid-cast sampling contract: the flip is immediately reflected in `is_satisfied`/the HUD ring
   with zero effect on any already-snapshotted in-flight cast's `power_multiplier`; it affects
   eligibility only for the *next* cast attempt, per §3.1 rule 3's one-frame data-flow lag.
4. **A `static_cost` Vow — no condition to track.** §3.1 rule 5 and §3.4's final paragraph: zero
   evaluation work is performed, and the slot is entirely absent from the HUD data contract
   (`ring_applicable: false`, §3.6) — never rendered as a meaningless permanently-closed ring.
5. **The player hovering exactly at a threshold boundary.** The central hysteresis case — fully
   worked in §4 Formula 1's example: a raw HP% value oscillating within `[27, 33]` around a `30`
   threshold at default `dead_band_percent = 3` produces **zero** `is_satisfied` flips, versus 5
   flips for a naive comparison over the same 9-sample sequence.
6. **All 4 Vows flip in the same frame — is that a strobe?** **Proven not to be, per §4 Formula
   4**: each individual belt-charm location flashes exactly once in this scenario — nowhere near
   the WCAG 2.3.1 "more than 3 flashes within one second" threshold at any single location, with
   or without staggering. Staggering the flash *beat only* (never the mechanical ring-shape
   update, which is simultaneous and immediate for fairness) across `SIMULTANEOUS_FLIP_STAGGER_MS`
   intervals is applied anyway, purely as a legibility improvement matching art-bible §7.5's
   working-memory glance-load discussion, not because it is required for compliance.
7. **A loadout slot is re-equipped with a different item/Vow between encounters** (the only time
   re-slotting is possible at MVP — combat-encounter-system §3.4 rule 1 restates resonance-weaving
   §3.5's "fully free outside of combat" rule). **Ruling**: any tracked state for that slot
   (counters, flags, `last_marked_part_id` contribution, `last_flip_time_ms`) is discarded the
   moment the new item is equipped; the next `ENGAGING → ACTIVE` transition performs a fresh
   initial pass (§3.1 rule 4) for whatever Vow now occupies the slot — no flash, since it is an
   initial render, not a transition.
8. **`cyclic_attack_trigger`'s triggered window ends by consumption vs. by expiry** — both paths
   are fully specified and produce an identical end state (§4 Formula 3): `is_satisfied` flips
   `false`, the counter resets to `0`, and counting resumes toward the next `trigger_every_n_
   attacks` — the only difference is *when* the flip occurs (at the moment of a successful gated
   cast, or at `window_open_until_ms`, whichever is first). Neither path is treated as an error or
   a wasted opportunity at the system level; letting the window expire unused is a valid (if
   suboptimal) player choice.
9. **`target_part_lock` before any Mark has been cast this encounter, or when the previously-
   marked part has since broken.** `last_marked_part_id` starts `null` at the initial pass (§3.1
   rule 4); while `null`, `raw_satisfied` evaluates `false` unconditionally (nothing to match
   against) — not an error. If the marked part later breaks, `selected_part_id`'s own fallback
   behavior (combat-encounter-system §5 Edge Case 1 — falling back to `is_core` when the Valid
   Target Set is empty) governs what the player is even able to target next; this document adds
   no special-case beyond reading whatever `selected_part_id` combat-encounter-system currently
   reports, per its own existing rules.
10. **A future `condition_type` this document's §3.2 taxonomy table does not yet cover ships as
    content.** **Ruling: content-authoring-time validation rejects it, not a runtime concern**,
    matching resonance-weaving §5 Edge Case 6's identical precedent for out-of-range
    `expected_uptime` values. A `condition_type` string with no matching evaluator in §3.2's table
    is a build-time content error; if one somehow ships anyway, this document's obligation is the
    same inert-fail posture as Edge Case 1 — the slot's `is_satisfied` resolves `false` and never
    flips, never crashing the evaluation pass for the other 3 slots.

## 6. Dependencies

### Depends On

- **`resonance-weaving-system.md`** — reads the Vow data object's exact fields (`vow_id`,
  `category`, `effect_mode`, `condition_type`, `condition_params`, `slot_type` — §3.3.1 there),
  the full 10-Vow starting catalog and its `condition_type`/`condition_params` shapes (§3.3.4),
  the `gate`/`scale`/`static_cost` semantics this document's §3.4 restates verbatim, the mid-cast
  snapshot rule (§5 Edge Case 3) restated in this document's §3.5, and the inert-fail precedents
  (§5 Edge Cases 1, 6) this document's own Edge Cases 1 and 10 extend. Nothing here redefines that
  schema or its formulas — this document only ever reads `power_multiplier`/`expected_uptime` for
  context, never recomputes them.
- **`combat-encounter-system.md`** — reads the `ACTIVE`-state per-frame step order (§3.2 there)
  this document's evaluation pass slots into (§3.1); the exact field names `player_current_hp`,
  `hunter_max_health` (§3.1/§3.8), `current_resonance`, `resonance_cap` (§3.6),
  `active_clear_time_seconds` (§4 Formula 7), the defensive-triad resolution events (§3.5), the
  damage pipeline's `final_damage` output and its §3.7 rule 3 "hit" definition (reused verbatim in
  §3.2), and the ability-activation gate check (§3.4 rule 2) and cast-time snapshot read (§3.4
  rule 5) that consume this document's `is_satisfied` output. This document never writes to any
  field that document owns.
- **`input-targeting-system.md`** (indirect, via combat-encounter-system) — reads `selected_
  part_id` for `target_part_lock` (§3.2), exactly as combat-encounter-system §3.3 rule 2 itself
  reads it; never writes to it.
- **`creature-data-schema.md`** (indirect, via combat-encounter-system's `hostile_state`) — reads
  `CreatureTemplate.tier` (enum `standard` \| `boss`) for `encounter_type_restriction` (§3.2).

**Note on the apparent circularity with `combat-encounter-system`**: `systems-index.md`'s own
"Circular Dependencies" section addresses this directly — "None found. One sequencing note:
`vow-condition-tracking` depends on both `resonance-weaving-system` and `combat-encounter-system`,
so it cannot be finalized until both are stable." The relationship is a one-way **data-flow**
coupling in each direction, not a circular design dependency: this document consumes combat-
encounter-system's per-frame *state* (HP, Resonance, events) as read-only input; combat-encounter-
system consumes this document's per-frame *signal* (`is_satisfied`) as read-only input. Neither
document's own internal rules depend on the other's internal rules — only on each other's already-
committed output for that frame (§3.1 rule 3's one-frame lag is what makes this safe).

### Depended On By

- **`combat-hud`** — the primary and only consumer of §3.6's `VowConditionSlotState`
  data contract. Reads `is_satisfied`, `ring_applicable`, and `last_flip_timestamp_ms` per slot to
  render the belt-charm closed/open ring and its one-shot flash (art-bible §7.3, §7.5), at either
  the primary belt-zone placement or the pre-authorized corner-cluster fallback — both render
  against the identical contract (§3.6). That system's own GDD must reference this document's
  field names directly, not redefine its own shape.
- **`combat-encounter-system.md`** (already authored — restated here for bidirectionality) — its
  own §3.4 rule 2 and §6 "Depended On By" entry already name `vow-condition-tracking` as the
  system supplying gate-mode eligibility's "live signal." This document's `is_satisfied` output is
  what fulfills that named contract.
- **`resonance-weaving-system.md`** (already authored — restated here for bidirectionality) — its
  own §6 "Depended on by" entry already names this document as the owner of "live per-frame
  evaluation of every `condition_type` this document defines, and the HUD signal."

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** §3.2 (the closed/open ring container grammar this document's
  boolean maps to), §5.2 (the Vow-scar vs. condition-state distinction that is this document's own
  reason to exist), §7.3 (the one-shot-flash-never-a-loop rule §3.3/§4 are built to satisfy),
  §7.5 (the Combat HUD Specification's belt-zone placement and pre-authorized fallback), §7.7 (the
  WCAG 2.3.1 anti-strobe requirement §4 Formula 2 proves, and the shape-only colorblind design test
  §3.4's fixed ring mapping satisfies).
- **`design/gdd/creature-ai-telegraph-system.md`** — `TELEGRAPH_WINDUP_FLOOR_MS = 600` is the
  precedent `VOW_FLIP_MIN_INTERVAL_MS` reuses (§4 Formula 2, §7).
- **`design/gdd/game-concept.md`** — Pillar 1 (the direct justification for cast-start sampling,
  §3.5), the Player Motivation Profile's Competence/Autonomy needs (§2).

## 7. Tuning Knobs

| Knob | Default | Safe Range | Affects | Tunable? | Notes |
|---|---|---|---|---|---|
| `dead_band_percent` | `3%` | 1–8% | Width of the no-flip buffer around a Category A threshold condition (§4 Formula 1) | Yes | Applies to `health_below/above_percent_threshold`, `resonance_above/below_threshold` only |
| `VOW_FLIP_MIN_INTERVAL_MS` | `600ms` | fixed | Hard ceiling on `is_satisfied` flip frequency (and therefore HUD flash frequency), every slot, every category | **NO — NON-TUNABLE, locked accessibility floor** | Reuses `creature-ai-telegraph-system`'s identical `TELEGRAPH_WINDUP_FLOOR_MS` constant and WCAG 2.3.1 rationale. No difficulty, performance, or content-authoring setting may lower it. |
| `event_triggered_window_ms` | `1500ms` | 800–3000ms | How long an event-triggered condition (`cyclic_attack_trigger`) stays `true` before auto-expiring unused (§4 Formula 3) | Yes | |
| `SIMULTANEOUS_FLIP_STAGGER_MS` | `80ms` | 40–150ms | Visual-only stagger between queued flash beats when 2+ slots flip in the same frame (§4 Formula 4) | Yes | Cosmetic only — never delays the mechanical `is_satisfied` update itself |
| Evaluation pass position | once per frame, after combat-encounter-system §3.2 step 5, before step 6 | fixed | Where in the frame order this document's evaluation runs (§3.1) | **NO — structural, non-tunable** | Not a numeric balance lever; a fixed architectural placement guaranteeing the one-frame consistency proof in §3.1 rule 3 |
| Initial-pass no-flash rule | locked | fixed | Whether the first per-encounter evaluation emits a flash (§3.1 rule 4) | **NO — locked** | Matches art-bible §7.3: a flash represents a transition; there is no prior state to transition from at encounter start |

## 8. Acceptance Criteria

| # | Criterion | Verification Method |
|---|---|---|
| AC1 (primary — the anti-strobe proof) | For any `condition_type`, in any category, under any sequence of underlying state changes — including an adversarial worst case where the raw evaluation changes every single frame at 60 FPS — `is_satisfied` never changes value more than once per `VOW_FLIP_MIN_INTERVAL_MS` (600ms), i.e. never more than 2 flips within any rolling 1-second window. | Stress test injecting a raw evaluation that toggles every frame for 10 simulated seconds at 60 FPS; assert the observed `is_satisfied` flip count in every rolling 1-second window is ≤ 2. |
| AC2 | A player whose tracked metric (HP% or Resonance%) oscillates within `[threshold_percent − dead_band_percent, threshold_percent + dead_band_percent]` produces **zero** `is_satisfied` flips over a simulated encounter, regardless of how many times the raw value crosses `threshold_percent` itself. | Reproduce §4 Formula 1's exact 9-sample worked example programmatically; assert flip count is 2 (the two genuine out-of-band crossings), not 5 (the naive-comparison count). |
| AC3 | Vow state is legible with color entirely disabled. | Render a fixture frame in grayscale with all animation frozen; a QA tester must correctly distinguish `is_satisfied: true` (closed ring) from `is_satisfied: false` (open ring) for every equipped conditional slot using shape alone — this document's contribution is guaranteeing `is_satisfied` is always a clean boolean with no third/ambiguous state ever exposed to the ring mapping. |
| AC4 | `static_cost` Vow slots and empty slots never appear with `ring_applicable: true` in the exposed HUD data array. | Fixture equipping one of each slot state (conditional, static_cost, empty); assert exactly 1 of 3 `VowConditionSlotState` entries has `ring_applicable: true`. |
| AC5 | The initial evaluation pass at `ACTIVE` start never queues a flash for any slot, regardless of what `is_satisfied` evaluates to. | Fixture covering all 10 catalog `condition_type`s at encounter start; assert `last_flip_timestamp_ms` is `null` for every slot immediately after the initial pass, before any subsequent frame runs. |
| AC6 | A snapshotted in-flight ability's applied `power_multiplier` never changes after cast-start, even when `is_satisfied` for that same slot flips one or more times before the cast's active duration ends. | Fixture with an Aura-Form `scale`-mode ability whose bound Vow condition is forced to flip mid-duration; assert every tick's `form_output_value` (resonance-weaving Formula 5) stays constant across the Aura's full duration. |
| AC7 | For `gate`-mode Vows, an ability activation attempt is rejected in every frame where `is_satisfied = false` for that slot, and is eligible whenever `is_satisfied = true` and the ability is off cooldown. | Fixture toggling `is_satisfied` across several frames; assert cast-eligibility tracks it exactly in every frame — restated and aligned with resonance-weaving AC7. |
| AC8 | `cyclic_attack_trigger`'s counter and window behave per §4 Formula 3: the counter resets to `0` the instant `is_satisfied` flips `true`; the window closes at the earlier of a successful gated cast or `event_triggered_window_ms` elapsing. | Two fixtures — one ending the window via a successful cast, one via timeout — both asserting the counter is `0` and `is_satisfied = false` immediately after closure. |
| AC9 | All per-slot condition state (counters, flags, `last_marked_part_id`, `last_flip_time_ms`) is fully reset at the `ACTIVE → RESOLVING` transition and does not leak into the next encounter. | Fixture ending an encounter mid-window (e.g. a `cyclic_attack_trigger` armed, a `no_damage_taken` flag already `false`); assert the next encounter's initial pass starts every slot from a fresh, condition-definition-derived baseline (§3.1 rule 4). |
| AC10 | When 2–4 Vow slots flip in the same frame, every flipped slot's `is_satisfied`/ring-shape value updates within that same frame (zero mechanical delay); queued flash playback start times are staggered by `SIMULTANEOUS_FLIP_STAGGER_MS` per Formula 4. | Fixture forcing a 4-slot simultaneous flip; assert all 4 `is_satisfied` values are correct in that exact frame, `last_flip_timestamp_ms` is identical for all 4, and (if `combat-hud`'s own queue is under test) recommended flash-fire offsets differ by exactly the stagger constant. |
| AC11 | The full per-frame evaluation pass for 4 equipped conditional Vows completes well under the 16.6ms frame budget. | Performance benchmark asserting evaluation cost stays under an explicit ceiling (e.g. 0.1ms — a 166× safety margin) even on a deliberately unoptimized reference implementation, per §4 Formula 5. |
| AC12 | Every one of the 10 starting Vow catalog `condition_type`s (resonance-weaving §3.3.4) has a defined evaluator, category, and state-source mapping in this document's §3.2 taxonomy table. | Content-catalog cross-check test asserting no `condition_type` referenced by any authored Vow is missing from this document's evaluator table; reject at content-validation time if one is found missing (§5 Edge Case 10). |
| AC13 | `no_damage_taken` flips `false` on any nonzero `final_damage` applied to the player, including blocked (partially-mitigated) damage, and does **not** flip `false` on a fully-avoided (dodged or interrupted) attack; `no_dodge_or_block_used` flips `false` only on a successful dodge or block input, never on an unavoided hit or a successful interrupt. | Four fixture scenarios (dodge, block, interrupt, unavoided hit); assert the two flags' post-event states match §3.2's Design Call table exactly in every scenario. |
