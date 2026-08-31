# Rare Creature Capture System: IDLExIDLE

> **SUPERSEDED 2026-09-01 by nothing — no capture code ever shipped.** Describes the creature capture model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | game-designer |
| **Status** | Complete — all 8 sections authored in a single session, building directly on `combat-encounter-system.md`'s pre-anticipated `capture_favorability_score` hook (its Formula 6, authored in advance specifically for this document) and `creature-jobs-evolution-system.md`'s explicit delegation (its Edge Case 12) of the "fresh or preserved `EvolutionProgressRecord`" decision to this document. Every ambiguity is resolved with an explicit, flagged design call, never a placeholder. Validate at `/design-review` before Production. |
| **Priority / Tier** | Vertical Slice — deferred from MVP per `game-concept.md`'s explicit scope note and `systems-index.md` #15. Mechanically specified now, in full, so it needs no retrofit later — matching how `design/art/art-bible.md` §5.4 already specced this system's visual grammar for the same reason. |
| **Depends On** | `design/gdd/combat-encounter-system.md`, `design/gdd/creature-jobs-evolution-system.md`, `design/gdd/creature-data-schema.md` |
| **Depended On By** | `creature-roster-ui`, `region-mastery-automation-system` |

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Capture is exclusively a rare-creature mechanic.** A Common-tier (non-rare) hostile creature is never capture-eligible, under any circumstance. | The system's own name and every mention of it in `game-concept.md` tie "capture conditions" specifically to rare creatures ("Rare creatures introduce capture conditions"). Extending eligibility to any creature would dilute the mechanic's identity and multiply UI/logic scope onto every encounter — art-bible §5.4 already scoped its visual treatment (the aura-ring) to rare creatures only, and this document keeps the mechanical scope identical to that visual scope. |
| A2 | **The overkill problem is resolved by a one-time-per-encounter "Capture Ward"** (a mercy floor clamping a lethal non-core hit to 1 HP, once) — not by a part-break-gated trigger and not by a player-toggled non-lethal damage mode. | `game-concept.md` frames the tension explicitly in HP terms ("weakening... without killing"), making an HP-band design the textually faithful reading. A part-break gate would overload `creature-data-schema`'s part-break system — already serving loot and fight-state duty — with a second, conflicting purpose. A toggled non-lethal mode would require rewriting every damage source in `combat-encounter-system`'s Formula 3 pipeline, a change outside this document's authority. The Ward is a single, surgical, well-precedented mechanism (functionally identical to Pokémon's Sturdy/Focus Sash — guaranteed 1 HP survival, once) that removes exactly the named failure mode (an accidental finishing blow) while leaving deliberate defeat fully intact as a real, informed choice. |
| A3 | **A captured creature never starts at a non-root `EvolutionNode`.** | `creature-jobs-evolution-system` §3.2 step 4 explicitly requires the capture path to "produce a valid post-transition `CreatureInstance` using the same fields as step 2" (hatching) — which locks `current_node_id = root_node_id` for both paths. This document has no authority to violate a rule a dependency document already locked. What capture offers instead is specified in A4/A5. |
| A4 | **A captured creature inherits the hostile instance's own `power_tier` at the moment of capture** (which may exceed 1, depending on the difficulty of the encounter the player chose to take on); a hatched creature always starts at `power_tier = 1` (`creature-data-schema`'s own default for an unset value). | This is the single most natural, non-arbitrary advantage available — it needs no new field and no new formula category, and is a direct, legible consequence of "the creature you fought becomes the creature that works for you" (`creature-jobs-evolution-system` §2), not an arbitrary bonus grafted on. |
| A5 | **A captured creature's `evolution_progress` (Vector 5) starts at a nonzero value** derived from how skillfully the capture was executed and the creature's rarity tier, rather than zero. `healthy_work_ticks` (Vector 3) and `equipped_charm_id` (Vector 2) are never pre-seeded. | Vector 5 is the one evolution vector `creature-jobs-evolution-system` describes as generic and additive ("the vector every other feed material contributes to"), unlike Vector 4 (a closed, Kill-event-derived tag enum this document has no authority to extend) or Vector 3 (deliberately not shortcut-able by origin path, per that document's own stated intent: "prove yourself at your current job before the game lets you transcend it"). Seeding only Vector 5 respects both documents' existing boundaries while still giving capture genuine, provable, bounded value. |
| A6 | **Boss-tier creatures never roll rare and are never capture-eligible.** | A region's boss is the sole, singular Mastery Transition gate every dependent system assumes is resolved by a kill (`region-mastery-automation-system`'s entire unlock model, is described only in terms of a boss kill). An alternate capture-unlock path for a boss would create an unspecified branch no other document accounts for, and would undercut the boss encounter's ceremonial "coronation, not shutdown" weight (art-bible §2) — earned by defeat, not deferred by mercy. |
| A7 | **A rare creature never flees**, and a failed capture attempt has no consequence beyond a short cooldown before the next attempt is accepted. | Flee behavior would require extending `creature-ai-telegraph-system`'s AI model, a system this document has no authority to modify. `game-concept.md`'s own framing — "a botched capture attempt simply becomes a kill instead of a hard fail state" — already establishes that failure costs nothing beyond continuing the fight; a flee mechanic would be an additional, unrequested punishment layered on top of an already-resolved design question. |
| A8 | **Idle/automated encounters may attempt capture**, substituting a fixed, deliberately lower `automated_capture_favorability_baseline` for the live-tracked `capture_favorability_score`. | Pillar 3 forbids locking idle-farmable content behind active-only play unless it "fundamentally requires a decision idle can't make." Capture doesn't fundamentally require such a decision — it only requires *skilled defensive execution*, which this document expresses as a statistically weaker, never-zero baseline. This mirrors the exact asymmetric-but-nonzero shape `combat-encounter-system`'s own active/idle efficiency contract (its Formula 7) already uses. |
| A9 | **Creature rarity (`RarityTier`) is a new, creature-specific enum this document owns** — structurally identical in tier count and ring-count visual grammar to `item-data-schema`'s item rarity (per art-bible §5.4's explicit instruction to reuse §4.3's grammar "wholesale"), but a data-independent field: never the same roll, never the same schema entry. | Art-bible locks the *visual* reuse but never states the two systems share a roll or a table. Keeping them independent lets this document tune encounter-frequency pacing freely without disturbing `item-data-schema`'s already-designed loot economy. |
| A10 | The extension data this document needs — `RareCreatureCaptureRecord` (`rarity_tier`, `capture_ward_consumed`) — **lives in this document's own side table, keyed by `instance_id`**, never written into `creature-data-schema`'s file. | Matches the established project pattern (`creature-jobs-evolution-system`'s `EvolutionProgressRecord`, `combat-encounter-system`'s `AttackDamageProfile`) and is explicitly pre-authorized by `creature-data-schema`'s own A8/Edge Case #11: "the expected pattern is an additive field or a separate lookup table... added by that system's own GDD when it is written, without modifying any field defined here." |

---

## 1. Overview

The rare creature capture system is IDLExIDLE's second, additive path from a hostile encounter into a bound roster member — the live, skill-driven alternative to Core Hatching's guaranteed, encounter-independent seed. It owns exactly three things: **rarity** (which hostile standard-tier creatures are flagged rare, and at what tier, at spawn), **the capture window and its overkill safeguard** (an HP-band eligibility check, plus a one-time "Capture Ward" that guarantees the crossing hit into that band never accidentally ends the fight), and **the capture-vs-hatch value proposition** (why a captured creature is worth more than a free one, without ever being *required*). It builds directly on `combat-encounter-system`'s pre-built hooks (`AttemptCapture`, `capture_favorability_score`) rather than inventing new combat mechanics, and it hands off a fully-formed, evolution-ready `CreatureInstance` to `creature-jobs-evolution-system` using exactly the field shape that document already reserved for it. If Core Hatching answers "how do players get creatures at all," this document answers "why would a player ever choose the harder path" — and its central discipline is making that answer true without making hatching feel like the consolation prize.

## 2. Player Fantasy

> **The hunter who chooses mercy over the kill.** Not because the game stops you from finishing it — because you decided, on purpose, that this one lives.

Concretely, this system delivers on that fantasy by making three things true:

- **Restraint is a real, felt decision, not an accident of good aim.** The single failure mode that would poison this fantasy — landing the precise, skillful finishing sequence you meant as a capture and watching it register as a kill instead — is mechanically impossible for the first lethal hit against any rare creature (§3.2's Capture Ward). Once you're standing over a creature at 1 HP, still alive, still hostile, the choice to spare it or end it is entirely yours, made with full information, never made *for* you by an unlucky crit. This is the direct combat expression of **Autonomy** (Self-Determination Theory): the game removes the accident, not the decision.
- **The moment of mercy looks and reads like mercy, not like a menu action.** art-bible §3.2/§5.4 already locked the visual language for this exact beat: the creature's open, live vulnerability glyph — the same glyph the player has been reading and exploiting all fight — closes and inverts into the Ward/Ownership seal, the identical shape a boss's Mastery Transition produces, just quieter. The player watches the very weak point they were hunting become the mark of ownership. Nothing about that beat says "you won a dice roll" — it says "you earned this."
- **What you spared is measurably, permanently more than what you would have hatched for free.** A captured Verdant Whelp isn't a cosmetically-different Verdant Whelp — it remembers the fight it survived (§3.5): the power it had earned by the time you found it, and the discipline you showed sparing it, both become real, visible head starts on its evolution. This is **Competence** made legible: skillful, careful play against a rare creature produces a colleague that is provably, traceably better-started than one summoned from a core — while a hatched creature of the same line remains fully capable of reaching the exact same destination on its own terms (§8, AC3). The fantasy is generosity rewarded, not generosity required.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: which hostile creatures roll rare and at what tier (§3.1), the capture-eligibility window and the overkill safeguard (§3.2), the mechanics of the `AttemptCapture` action and its success roll (§3.3), and what a successfully captured creature carries into `creature-jobs-evolution-system` beyond a hatched creature's baseline (§3.5). It does **not** own, and explicitly defers to the cited document in every case: the encounter state machine, the damage pipeline, and the `AttemptCapture`/`capture_favorability_score` hooks themselves (`combat-encounter-system`, which pre-built them for this exact purpose — see that document's A10); evolution transition rules, the AND-gate across all five vectors, and job/combat/equip mechanics (`creature-jobs-evolution-system`); the `CreatureInstance`/`bind_state` schema and the atomic hostile→bound transition (`creature-data-schema`); telegraph timing and AI behavior, including whether a creature ever flees (`creature-ai-telegraph-system` — this document explicitly rules it does not, §5 Edge Case 4, without modifying that document); and item rarity or loot tables (`item-data-schema`/`loot-drop-system`).

A note on scope, prompted by imprecise framing elsewhere: no canonical design document (`game-concept.md` included) defines a "harvest" outcome for rare creatures. This document addresses exactly the two outcomes `combat-encounter-system`'s own encounter-end table already supports for a rare creature — **Capture** and **Defeat (Kill)** — plus the always-available **Retreat**. If a distinct "harvest" mechanic is intended, it is undefined and out of scope here; nothing in this document blocks a future extension adding one.

### 3.1 Rarity

Only `tier = standard` hostile creature instances are eligible to roll rare (A6). At the moment a hostile `CreatureInstance` is spawned — a trigger this document does not own; flagged as an integration point on whatever system performs spawning, most likely `region-mastery-automation-system` (§6) — that system is expected to call this document's rarity roll (§4 Formula 1) and attach the result as a `RareCreatureCaptureRecord` (A10), keyed by the new instance's `instance_id`:

| Field | Type | Description |
|---|---|---|
| `instance_id` | string (GUID) | Foreign key to the hostile `CreatureInstance` this record describes. |
| `rarity_tier` | enum `RarityTier` | `common` \| `uncommon` \| `rare` \| `epic` \| `legendary` (A9). `common` is the roll result for the overwhelming majority of standard encounters and carries no capture eligibility, no aura-ring, and no bonus of any kind — it exists in the enum only so every standard instance has a well-defined value, never so it can be treated as "a low tier of rare." |
| `capture_ward_consumed` | bool | Runtime-only, meaningful only while `bind_state = hostile`. Initialized `false` at `ENGAGING`, exactly mirroring `combat-encounter-system`'s own Resonance-reset precedent (§3.6 there). Never persists past `COMPLETE` (torn down with the rest of `hostile_state`, per `creature-data-schema` Edge Case #3). |

A creature whose `rarity_tier` is anything other than `common` is, for every purpose in this document, "rare" (`is_rare` below is a derived convenience, not a stored field: `is_rare := rarity_tier != common`). Its presentation follows art-bible §5.4's "Rare / capturable" row exactly: identical hostile threat-display and ambient wash to any unconquered creature, plus a satellite aura-ring outside the outer contour whose ring count matches its tier via §4.3's grammar (Uncommon = single ring, Rare = double concentric ring, Epic = double ring + four corner flourishes, Legendary = full Ward-seal ornament) — this document supplies only the tier value; rendering the ring itself is the art pipeline's job, already specced.

### 3.2 Capture Eligibility and the Overkill Safeguard

**Eligibility** (§4 Formula 2): a rare, hostile, standard-tier creature becomes capture-eligible the instant its `current_health` falls to or at `capture_eligibility_hp_threshold_percent` (default 25%) of its `effective_max_health`. This threshold is deliberately generous, not a razor's edge — the tension this system is designed around is landing the crossing hit *safely*, not maintaining a hair-thin band afterward. Once eligible, a creature remains eligible for the rest of the encounter regardless of subsequent HP changes above 0 (no re-locking if the player lands a weak, non-lethal hit that happens to tick health back toward the threshold from below — health only ever decreases in this system, never regenerates, so this case is theoretical but stated for completeness).

**The Capture Ward** (§4 Formula 3): every rare hostile creature carries one, single-use mercy floor for the encounter. The first time — and only the first time — a damage-dealing event that does **not** target the creature's `is_core` part would reduce `current_health` to 0 or below, `current_health` is instead clamped to 1 and `capture_ward_consumed` flips to `true`. This is a required integration point on `combat-encounter-system`'s own damage pipeline (Formula 3, Step 6) — flagged here as an addition that document must incorporate, not implemented by this document (matching the precedent `combat-encounter-system` itself already set for `rare-creature-capture-system`'s hooks, its A10). Every subsequent qualifying hit, once the Ward is consumed, applies damage exactly as Formula 3 already specifies with no further protection — a creature sitting at 1 HP after its Ward triggers is one ordinary hit away from a normal Kill outcome, exactly as before this system existed.

**What the Ward does not protect against**: any hit that breaks the `is_core` part. `creature-data-schema`'s own rule — a core break ends the encounter immediately, "a hard rule, not a suggestion," regardless of any other part's condition — is absolute and this document neither can nor does override it. A player pursuing a capture should avoid targeting (or being forced onto, per `combat-encounter-system` Edge Case #1's fallback-to-core rule) the core once the creature is weakened; this is a genuine, player-controlled risk this document leaves in place rather than removing (§5 Edge Case 6).

### 3.3 The Capture Action

`AttemptCapture` is the same action `combat-encounter-system` already defined as a hook (its §3.9/A10) — this document is its consumer. Concretely:

1. Available during `ACTIVE` whenever the target instance's `RareCreatureCaptureRecord.rarity_tier != common` **and** it is currently capture-eligible (§3.2) **and** the target is not `tier = boss` (A6, structurally impossible anyway since boss creatures never roll rare) **and** `capture_attempt_cooldown_ms` has elapsed since this creature's last failed attempt in this encounter (§3.3.3 below).
2. Bound to a dedicated, discrete "capture-press" input — added to the same input vocabulary `combat-encounter-system`'s AC3 already enumerates (cycle-next, cycle-prev, confirm, guard-hold, dodge-press, ability-press ×4, retreat-press) — so the entire mechanic remains completable with zero mouse/pointer device, preserving that document's accessibility guarantee.
3. On confirm, resolves instantly, in the same frame — no wind-up, no queueing — mirroring ability activation's existing "instant on confirm" precedent (`combat-encounter-system` §3.4.2). The success roll (§4 Formula 4) is evaluated once, using the live state of `capture_favorability_score`, the count of this instance's own non-core broken parts, and its `rarity_tier` at that exact instant.
4. **On success**: transitions the encounter directly to `RESOLVING`/**Capture**, per `combat-encounter-system` §3.9 priority 3 — an atomic `hostile → bound` transition (`creature-data-schema` Edge Case #3: `hostile_state` clears to null in the same write `bound_state` populates, no dangling `vulnerability_window_ms`). The new bound `CreatureInstance` is initialized per `creature-jobs-evolution-system` §3.2 step 2's field shape, modified only as specified in §3.5 below.
5. **On failure**: the encounter simply continues — no damage, no state change, no penalty beyond starting `capture_attempt_cooldown_ms` (default 3000ms, §7) before another `AttemptCapture` against this same instance is accepted. This prevents frame-spamming the action while `capture_favorability_score` keeps climbing from ordinary play in the meantime, so a longer, well-played fight naturally raises the next attempt's odds (§4 Formula 4) without requiring the player to do anything differently.
6. `AttemptCapture` issued against a target that is not currently eligible (non-rare, or rare but not yet weakened) is rejected as a no-op with no state change — surfacing that rejection to the player is a UI concern for `combat-hud`/`creature-roster-ui`, not a rule this document enforces mechanically.

### 3.4 Outcome Priority (Restated, Not Redefined)

`combat-encounter-system` §3.9 already fixes the encounter-end priority order — Kill (core break, then health-zero) above Capture, Capture above Retreat. This document restates the practical consequence for clarity: **a core break or non-core health-zero event always wins a same-frame race against a pending successful capture roll.** In practice this case is rare by construction — the Ward (§3.2) already prevents the ordinary "my last hit landed at the same instant I pressed capture" collision for the encounter's first lethal hit — but it remains possible after the Ward is spent (§5 Edge Case 5). A successful Capture, conversely, always wins a same-frame race against the player's own HP reaching 0 (Capture is priority 3, player-HP-Retreat is priority 4) — landing the capture and losing the fight in the same frame results in Capture, never Retreat.

### 3.5 What Capture Yields Over Core Hatching

A captured `CreatureInstance` is initialized exactly as `creature-jobs-evolution-system` §3.2 step 2 specifies for a hatched one — `bind_state = bound`, `assigned_region_id = null`, `work_state = null`, `current_node_id = root_node_id` (A3) — with exactly two deltas, both derived from the specific fight that produced it:

1. **`power_tier` inherits the hostile instance's own value at the moment of capture** (§4 Formula 6) instead of defaulting to 1 (A4). Since a rare creature can appear at any `power_tier` the region supports, this is a real, encounter-difficulty-gated advantage — a player who takes on a tougher rare sighting is rewarded with a creature that produces more, sooner, per `creature-jobs-evolution-system` Formula 5's own tier scaling.
2. **`evolution_progress` (Vector 5 of the `EvolutionProgressRecord`) starts at a nonzero value** (§4 Formula 5) computed from the creature's `rarity_tier` and how skillfully the capture was executed (`capture_favorability_score` at the moment of success), instead of zero (A5). This is a head start on exactly one of the four AND-gated evolution vectors `creature-jobs-evolution-system` Formula 4 requires — never enough by itself to trigger a branch (job diligence and combat-tally gates still require real, separate play), but a genuine, provable acceleration.

The captured instance also preserves its own pre-capture `instance_id` and its `rarity_tier` as a permanent lineage fact, surfaced by `creature-roster-ui` — "this is the very creature I fought," not a fresh mint wearing the same template, which `creature-jobs-evolution-system` §2 already names as the core differentiator of this path.

### 3.6 Idle/Automated Capture

Per A8, an automated encounter (a signal owned and supplied by `region-mastery-automation-system` — flagged as a required integration point) may issue `AttemptCapture` automatically the instant eligibility is reached, with no player decision to make (there is no player present to make one). The success roll (§4 Formula 4) substitutes `automated_capture_favorability_baseline` (default 3, §7) for the live-tracked `capture_favorability_score` in every other respect — the formula, the Ward, the rarity table, and the resulting creature's bonuses are identical regardless of whether the encounter was active or automated. Capture is therefore possible, never impossible, during idle play — just statistically weaker, which is exactly the shape of active-play advantage `game-concept.md`'s "higher capture chances" line describes (§5 Edge Case 8, §8 AC).

## 4. Formulas

All outputs round to the nearest integer using round-half-up, matching every sibling document's convention, unless explicitly stated otherwise.

### Formula 1 — Rarity Roll

```
rarity_tier = weighted_categorical_roll({
    legendary: p_legendary,
    epic:      p_epic,
    rare:      p_rare,
    uncommon:  p_uncommon
}, default = common)
```

Only ever invoked for a `tier = standard` hostile spawn (A6); never invoked for `boss`.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `p_uncommon` | float, tuning knob | 0–1 (default 0.07) | Probability of rolling `uncommon`. |
| `p_rare` | float, tuning knob | 0–1 (default 0.025) | Probability of rolling `rare`. |
| `p_epic` | float, tuning knob | 0–1 (default 0.004) | Probability of rolling `epic`. |
| `p_legendary` | float, tuning knob | 0–1 (default 0.001) | Probability of rolling `legendary`. |
| `rarity_tier` | enum `RarityTier` | 5 values | Output. The remainder (default `1 − 0.07 − 0.025 − 0.004 − 0.001 = 0.9` = 90%) resolves to `common`. |

**Output range**: exactly one of 5 mutually exclusive values every roll; probabilities must sum to ≤ 1 (the `common` remainder absorbs any slack).

**Worked example**: at default tuning, across 1,000 standard hostile spawns, the expected distribution is ~900 `common`, ~70 `uncommon`, ~25 `rare`, ~4 `epic`, ~1 `legendary`.

### Formula 2 — Capture Eligibility

```
capture_eligible = (rarity_tier != common)
                    AND (tier == standard)
                    AND (bind_state == hostile)
                    AND (current_health / effective_max_health) <= capture_eligibility_hp_threshold_percent / 100
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `capture_eligibility_hp_threshold_percent` | float, tuning knob | 10–40 (default 25) | The HP-percent band, at or below which, a rare creature is capturable. |
| `capture_eligible` | bool | — | Output, re-evaluated every frame (§3.2). Once `true`, never becomes `false` again for the rest of the encounter (health only decreases). |

**Worked example**: an Uncommon-tier Verdant Whelp at `power_tier = 2`, `effective_max_health = 92`: eligible once `current_health <= round(92 × 0.25) = 23`.

### Formula 3 — Capture Ward (Mercy Floor)

A state-transition rule, evaluated as an additional step in `combat-encounter-system` Formula 3's pipeline (Step 6), for every damage-dealing event against a rare creature:

```
IF rarity_tier != common
   AND capture_ward_consumed == false
   AND target_part.is_core == false
   AND (current_health − final_damage) <= 0:
       current_health = 1
       capture_ward_consumed = true
ELSE:
       current_health = max(0, current_health − final_damage)   // combat-encounter-system's normal rule, unmodified
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `final_damage` | int | ≥ `min_damage_floor` (combat-encounter-system Formula 3) | The incoming, already-mitigated hit. |
| `capture_ward_consumed` | bool | — | Flips `false → true` at most once per encounter (§3.1). |

**Output range**: `current_health` is never left at ≤ 0 by the *first* qualifying non-core lethal hit against an un-warded rare creature — it is exactly 1 in that case. Every subsequent qualifying hit resolves normally, including down to 0.

**Worked example**: a Rare-tier creature with `effective_max_health = 200`, currently at `current_health = 40` (already eligible, 20% ≤ 25%). An ability lands for `final_damage = 65`. Un-warded: `40 − 65 = −25 → clamp → current_health = 1`, `capture_ward_consumed = true`. A follow-up basic attack for `final_damage = 10` immediately after: `1 − 10 = −9 → max(0, −9) = 0` — the Ward does not re-trigger; the encounter's Kill priority (`combat-encounter-system` §3.9 priority 2) fires normally.

### Formula 4 — Capture Success Chance (central formula)

```
capture_success_chance_percent = clamp(
    base_capture_chance_percent
    + effective_favorability_score × favorability_weight_percent
    + parts_broken_count × parts_broken_capture_bonus_percent
    − rarity_tier_capture_penalty_percent[rarity_tier],
    min_capture_chance_percent,
    max_capture_chance_percent
)

capture_roll_success = uniform_random(0, 1) < capture_success_chance_percent / 100
```

Where `effective_favorability_score` is `capture_favorability_score` (`combat-encounter-system` Formula 6) for an active encounter, or `automated_capture_favorability_baseline` for an automated one (§3.6/A8).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `effective_favorability_score` | int | Unbounded, typically 0–30 in practice | Live per-encounter accumulator (active) or a fixed substitute (automated). Never clamped or reset within the encounter — see `combat-encounter-system` Formula 6. |
| `parts_broken_count` | int | 0 to the template's non-core part count | Non-core parts currently `broken` on this instance, read live from `PartState` (`creature-data-schema` §3.6). |
| `base_capture_chance_percent` | float, tuning knob | 10–40 (default 25) | Floor before any skill or rarity adjustment. |
| `favorability_weight_percent` | float, tuning knob | 2–8 (default 5) | Percent added per favorability point. |
| `parts_broken_capture_bonus_percent` | float, tuning knob | 1–5 (default 2) | Percent added per non-core part broken. |
| `rarity_tier_capture_penalty_percent[tier]` | float, tuning knob per tier | uncommon 0, rare 10, epic 20, legendary 35 | Percent subtracted, scaling with rarity. |
| `min_capture_chance_percent` | float, tuning knob | 1–15 (default 5) | Floor — capture is never mathematically impossible. |
| `max_capture_chance_percent` | float, tuning knob | 80–99 (default 95) | Ceiling — capture is never guaranteed, mirroring `combat-encounter-system` Formula 2's `crit_chance_cap` precedent. |
| `capture_success_chance_percent` | float | `[5, 95]` at default tuning | Output. |

**Output range**: `[min_capture_chance_percent, max_capture_chance_percent]` = `[5, 95]` at default tuning, by construction (the outer `clamp`), for any input combination.

**Worked examples** (all at default tuning):

| Scenario | Blocks / Dodges / Interrupts / Unavoided | Favorability (combat-encounter-system F6) | Parts Broken | Tier | Calculation | Result |
|---|---|---|---|---|---|---|
| Modest, ordinary play | 2 / 1 / 1 / 1 | `2+2−1+4=7` | 0 | Uncommon | `25+7×5+0−0` | **60%** |
| Modest, ordinary play | 2 / 1 / 1 / 1 | 7 | 0 | Rare | `25+35+0−10` | **50%** |
| Modest, ordinary play | 2 / 1 / 1 / 1 | 7 | 0 | Legendary | `25+35+0−35` | **25%** |
| Focused, skilled play | 3 / 2 / 2 / 1 | `3+4−1+8=14` | 2 | Uncommon | `25+70+4−0` | **95%** (capped) |
| Focused, skilled play | 3 / 2 / 2 / 1 | 14 | 2 | Legendary | `25+70+4−35` | **64%** |
| Idle/automated (baseline=3) | — | 3 | 0 | Uncommon | `25+15+0−0` | **40%** |

These match this document's own §8 acceptance criterion 1: modest, non-perfect defensive play already clears 50% against Uncommon and Rare, the two tiers a player will actually encounter with any frequency (90.4% of all rare rolls, per Formula 1); the rarest tiers stay a genuine, escalating challenge that rewards — but does not strictly require — deeper mastery.

### Formula 5 — Captured-Creature Evolution Progress Head Start

```
capture_contribution_value = base_capture_contribution_value × rarity_tier_contribution_multiplier[rarity_tier] × (1 + favorability_bonus_coefficient × capture_favorability_score)
evolution_progress (initial) = round(capture_contribution_value)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_capture_contribution_value` | float, tuning knob | 20–80 (default 40) | Baseline, comparable in scale to `the-forge-system`'s mid-tier `feed_contribution_value` range (~2–195, per `creature-jobs-evolution-system`'s own citation) for consistency of feel. |
| `rarity_tier_contribution_multiplier[tier]` | float, tuning knob per tier | uncommon 1.0, rare 1.8, epic 3.0, legendary 5.0 | Escalating reward for rarer captures. |
| `favorability_bonus_coefficient` | float, tuning knob | 0.01–0.04 (default 0.02) | How strongly defensive skill during the capture converts into extra head start. |
| `capture_favorability_score` | int | as observed at the instant of successful capture | Not substituted for automated captures — an automated capture always uses `automated_capture_favorability_baseline` here too, for consistency with Formula 4. |
| `evolution_progress` (initial) | int | ≥ `round(base_capture_contribution_value × rarity_tier_contribution_multiplier[tier])` | Output. Written directly into the new `CreatureInstance`'s `EvolutionProgressRecord.evolution_progress` at bind-time; behaves identically to any other progress from that point on, including `creature-jobs-evolution-system`'s own carryover rule (its Edge Case #2) once further fed. |

**Worked example**: capturing an Uncommon-tier creature with `capture_favorability_score = 14` (the "focused, skilled play" row above): `capture_contribution_value = 40 × 1.0 × (1 + 0.02 × 14) = 40 × 1.28 = 51.2 → 51`. Capturing a Legendary-tier creature under the same favorability: `40 × 5.0 × 1.28 = 256`. For reference, `creature-jobs-evolution-system`'s own MVP content gates its first evolution branch at `material:600` — a Legendary capture at strong favorability supplies well under half of that threshold instantly (256/600 ≈ 43%), a meaningful but never gate-skipping head start; an Uncommon capture supplies a much smaller fraction (51/600 ≈ 8.5%), appropriate to how much more common that tier is (7% vs. 0.1% roll odds).

### Formula 6 — Power Tier Inheritance

```
captured_instance.power_tier = hostile_state's power_tier at the instant of successful capture
hatched_instance.power_tier  = 1   (creature-data-schema's own unset default; restated, not redefined, here)
```

Not a random process — a direct pass-through, stated formally for testability (§8 AC).

**Worked example**: capturing a creature encountered at `power_tier = 3` versus hatching a core from the same region (`power_tier = 1`): applying `creature-jobs-evolution-system` Formula 5 (`power_tier_output_scalar = 0.15`) to an otherwise-identical Producer, `base_role_output = 10`: the captured instance produces `round(10 × (1 + 0.15 × 2)) = 13` per tick versus the hatched instance's `round(10 × 1) = 10` per tick — a real, bounded, encounter-difficulty-earned production edge, not a flat, arbitrary bonus.

## 5. Edge Cases

1. **A capture attempt is made mid-encounter against a creature whose health then reaches 0 via overkill (the central risk this document exists to solve).** For the *first* non-core lethal hit against any rare creature, this cannot happen — the Capture Ward (§3.2, Formula 3) guarantees `current_health` clamps to 1 instead. If overkill still occurs — after the Ward is already spent, or via a core break, which the Ward never protects — the normal Kill outcome fires (`combat-encounter-system` §3.9 priorities 1–2): `bind_state` never changes (the creature was never briefly "half-captured"), no capture bonus (§3.5) is granted, and a normal `creature_core` still drops at the usual 100% rate (`loot-drop-system`'s existing rule, unaffected by this document). The player loses only this document's bonuses (the power-tier and evolution-progress head start) — the creature line itself remains fully obtainable via Core Hatching from that dropped core, at baseline. This is exactly "a botched capture attempt simply becomes a kill instead of a hard fail state" (`game-concept.md`), made mechanically precise: nothing is lost except what this document specifically adds.
2. **A capture attempt against a boss-tier creature.** Structurally impossible, not merely rejected — boss-tier creatures never roll rare (A6, Formula 1 is never invoked for `tier = boss`), so `AttemptCapture`'s eligibility check (§3.3.1) can never resolve `true` against one. No special-case rejection logic is needed; the precondition simply never holds.
3. **A successful capture occurs while the player's creature roster is at capacity.** No roster capacity limit exists anywhere in the current design (`creature-data-schema` and `creature-jobs-evolution-system` define none). This document therefore assumes none for now. **If a future system (most likely `creature-roster-ui`) introduces one, the required ruling is stated here in advance**: a full roster must reject the `AttemptCapture` action itself (§3.3.1's eligibility check gains a roster-capacity term), never allow a successful roll to occur and then discard the result — a skilled, successful capture attempt must never be silently wasted. Flagged as a forward-compatibility placeholder, not implemented here.
4. **A rare creature "flees" a capture attempt.** Does not happen, by explicit design (A7) — rare creatures use the identical hostile threat-display and AI behavior as any other standard creature (art-bible §5.4); only a failed roll's `capture_attempt_cooldown_ms` throttles retry rate. Extending `creature-ai-telegraph-system`'s AI model to add flee behavior is out of this document's authority and was deliberately not pursued.
5. **A capture attempt during an automated/idle encounter.** Explicitly permitted (§3.6/A8) — automation may issue `AttemptCapture` automatically upon eligibility, using `automated_capture_favorability_baseline` in place of a live score. This keeps rare-creature acquisition idle-farmable in spirit (Pillar 3) while preserving "higher capture chances" as a genuine, measurable active-play advantage (§4 Formula 4's worked examples: 40% automated vs. 60–95% actively-played, same Uncommon tier).
6. **A player breaks every non-core part while pursuing a capture, forcing all further hits onto the `is_core` part** (`combat-encounter-system` Edge Case #1's fallback-to-core rule, triggered once the Valid Target Set is empty). This is a real, player-caused risk this document does not mitigate: every further attack is now a potential core-break Kill, and the Capture Ward never protects a core hit (§3.2). A player who wants to preserve capture eligibility for as long as possible should stop breaking non-core parts once eligibility (Formula 2) is reached, or attempt capture promptly rather than continuing to press an advantage that no longer needs pressing. `parts_broken_count`'s bonus to Formula 4 is deliberately modest (2% per part, capped by the creature's own non-core part count) specifically so this tension is real but never dominant.
7. **`AttemptCapture` is pressed against a target that is rare but not yet eligible** (health still above the threshold). Rejected as a no-op (§3.3.6) — no roll occurs, no cooldown starts, nothing is consumed. Surfacing this rejection to the player (e.g., a HUD cue distinguishing "not yet weak enough" from "capture available") is a `combat-hud`/`creature-roster-ui` presentation concern, not a rule this document enforces.
8. **A core break and a pending successful capture roll resolve in the same frame** (possible only after the Ward is already spent, per Edge Case 1). `combat-encounter-system` §3.9's fixed priority order is authoritative and is not redefined here: Kill (priorities 1–2) is checked before Capture (priority 3), so the core break wins — restated in §3.4, not a new rule.
9. **A successful capture and the player's own HP reaching 0 resolve in the same frame.** Also governed by `combat-encounter-system` §3.9's existing priority order: Capture (priority 3) is checked before player-HP Retreat (priority 4), so the capture is honored — the player can retreat from a fight they just won a creature from, without losing that creature (§3.4).
10. **The encounter ends via manual Retreat (or player-HP Retreat) after the Ward has already triggered on a rare creature, leaving it at 1 HP.** Per `creature-data-schema` Edge Case #3 (restated, not redefined, by `combat-encounter-system` §3.2), `hostile_state` does not persist past `COMPLETE` — a fresh `hostile_state` (full health, `capture_ward_consumed` reset to `false`) is initialized the next time this instance is engaged (`combat-encounter-system` §3.2, `ENGAGING`). The Ward's mercy does not carry between separate encounters — the next attempt starts the tension over from full health, exactly as it would for any other creature.

## 6. Dependencies

### Depends On

- **`combat-encounter-system.md`** — consumes the `AttemptCapture` action point and `capture_favorability_score` (its Formula 6) exactly as that document pre-built them for this purpose (its A10). Reads its encounter state machine and §3.9 priority table without modification (§3.4 here restates, never redefines, that ordering). **Requires two integration additions this document flags but cannot itself implement**: (1) the Capture Ward step (§3.2/Formula 3 here) inserted into that document's own Formula 3, Step 6; (2) this document's own value for the Capture outcome's `RESOLVING` duration (`capture_resolving_duration_ms`, §7) — that document's §3.9 table explicitly left this as "whatever transition `rare-creature-capture-system` specifies when authored."
- **`creature-jobs-evolution-system.md`** — produces a `CreatureInstance` conforming exactly to that document's §3.2 step 4 contract, using the "fresh or preserved `EvolutionProgressRecord`" choice its own Edge Case 12 explicitly delegated to this document. Reads and extends (never redefines) its `EvolutionProgressRecord.evolution_progress` field (Vector 5) per §3.5/Formula 5 here.
- **`encounter-spawn-system.md`** — supplies **`power_tier`** (which this document's rarity roll scales against, and which a captured creature inherits as its head start) and the encounter template's **`rare_eligible`** flag. That flag is **hard-forced to `false` on every boss template**, which is what makes this document's decision A6 — *bosses never roll rare and are never capture-eligible* — structurally true rather than merely a rule someone has to remember. It is also why the **Capture Ward** (first lethal non-core hit clamps to 1 HP) can never interact with a boss fight.
- **`creature-data-schema.md`** — reads `bind_state`, the atomic hostile→bound transition rule (Edge Case #3), `power_tier`, `CreatureTemplate.tier` (`standard`/`boss`), and `PartState`/`part_id`/`is_core` for the Ward (§3.2) and `parts_broken_count` (§4 Formula 4). Adds its own extension record (`RareCreatureCaptureRecord`, A10) exactly as that document's own A8/Edge Case #11 pre-authorized, without modifying any field defined there.

### Depended On By

- **`creature-roster-ui`** — expected to surface a captured creature's `rarity_tier` as a permanent lineage flag (§3.5), distinguishing it visually/textually from a hatched creature of the same template. That system's own GDD must reference this document's `RareCreatureCaptureRecord` fields by name.
- **`region-mastery-automation-system`** — **must supply two signals this document consumes as external inputs**: the trigger/timing for Formula 1's rarity roll at hostile spawn time (this document owns the table, not the spawn event itself), and the "this encounter is automated" flag Formula 4 uses to substitute `automated_capture_favorability_baseline` (§3.6/A8). That system's own GDD must reference this document's Formula 1 and Formula 4 by name.

### Adjacent Systems (informational, not a dependency in either direction)

- **`item-data-schema.md` / `loot-drop-system.md`** — a natural, non-required extension point: a killed rare creature's `rarity_tier` could reasonably scale its dropped `creature_core`'s own item rarity (giving "defeat" a real incentive alongside "capture," per §3.0's scope note on the two valid outcomes). Not implemented or required by this document — flagged as a suggestion for those systems' own design authority.
- **`combat-hud`** (already Designed) — expected to surface capture eligibility and `AttemptCapture` availability as a screen-space cue, and to distinguish a rejected attempt (§5 Edge Case 7) from a pending one. This document defines no rendering itself.
- **`design/art/art-bible.md`** — §5.4 (the "Rare / capturable" state row this document's `rarity_tier` field exists to drive), §4.3 (the rarity ring-count grammar this document's tiers reuse wholesale, per A9), §3.2 (the Ward/Ownership seal glyph the Capture `RESOLVING` transition plays into, per §7's `capture_resolving_duration_ms`).
- **`design/gdd/game-concept.md`** — Pillar 3 (the direct justification for A8's idle-capture ruling), the "weakening without killing" and "a botched capture attempt simply becomes a kill" framing this document implements literally (§3.2, §5 Edge Case 1).

## 7. Tuning Knobs

All values below are unbalanced placeholders pending Vertical Slice playtesting, per this project's established convention — exposed as external data, never hardcoded, per `.claude/docs/coding-standards.md`.

### Feel Knobs (tuned by playtesting intuition)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Capture resolving ceremony length | `capture_resolving_duration_ms` | 1200–2500ms | 1800ms | Length of the Capture outcome's `RESOLVING` beat (art-bible §3.2's Ward/seal inversion) — between a standard Kill's 1500ms and a boss Kill's 3000ms (`combat-encounter-system` §3.9), reflecting genuine but non-boss-scale ceremony. Supplies the value that document's own table left open. |

### Curve Knobs (tuned by mathematical modeling)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Favorability weight | `favorability_weight_percent` (Formula 4) | 2–8 | 5 | Primary lever for how strongly skilled defensive play converts into capture odds — the single biggest knob in this document. |
| Parts-broken capture bonus | `parts_broken_capture_bonus_percent` (Formula 4) | 1–5 | 2 | Secondary, self-limiting lever (capped by non-core part count) rewarding precision targeting during a capture pursuit. |
| Rarity capture penalty (per tier) | `rarity_tier_capture_penalty_percent[tier]` (Formula 4) | uncommon 0 / rare 5–15 / epic 15–25 / legendary 25–45 | 0 / 10 / 20 / 35 | Controls how much harder each successive rarity tier is to actually land, independent of its roll odds. |
| Base contribution value | `base_capture_contribution_value` (Formula 5) | 20–80 | 40 | Baseline size of the evolution-progress head start, scaled against `the-forge-system`'s own feed-value range for consistency of feel. |
| Rarity contribution multiplier (per tier) | `rarity_tier_contribution_multiplier[tier]` (Formula 5) | uncommon 1.0 / rare 1.5–2.2 / epic 2.5–3.5 / legendary 4–6 | 1.0 / 1.8 / 3.0 / 5.0 | Controls how much bigger the head start gets for rarer captures — the primary "why pursue a Legendary" lever. |
| Favorability bonus coefficient | `favorability_bonus_coefficient` (Formula 5) | 0.01–0.04 | 0.02 | How strongly *how well you fought* (not just what you caught) scales the evolution head start. |

### Gate Knobs (tuned by pacing / frequency targets)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Rarity roll odds | `p_uncommon` / `p_rare` / `p_epic` / `p_legendary` (Formula 1) | Sum ≤ 1; suggested bands 0.03–0.12 / 0.01–0.05 / 0.001–0.01 / 0.0003–0.003 | 0.07 / 0.025 / 0.004 / 0.001 | **Highest-leverage pacing knob in this document** — directly controls how often the entire mechanic is even relevant. Too low and capture never comes up; too high and Legendary loses its rarity fantasy. |
| Capture eligibility HP threshold | `capture_eligibility_hp_threshold_percent` (Formula 2) | 10–40 | 25 | How generous the "weakened" band is before the overkill tension even becomes relevant — lower values increase both the drama and the reliance on the Ward. |
| Base capture chance | `base_capture_chance_percent` (Formula 4) | 10–40 | 25 | Floor before skill/rarity adjustment — directly sets how forgiving a first, unskilled attempt is. |
| Capture chance floor/ceiling | `min_capture_chance_percent` / `max_capture_chance_percent` (Formula 4) | 1–15 / 80–99 | 5 / 95 | Guarantees capture is never impossible nor guaranteed, mirroring `combat-encounter-system`'s `crit_chance_cap` precedent. |
| Failed-attempt cooldown | `capture_attempt_cooldown_ms` | 1500–5000ms | 3000ms | Prevents frame-spamming `AttemptCapture`; matches `ultimate_cooldown_ms`'s default scale for consistency. |
| **Automated capture baseline** | `automated_capture_favorability_baseline` | 1–8 | 3 | **BALANCE-CRITICAL for Pillar 3** — the single value that determines whether idle capture feels like a real, lesser path (intended) or a non-option in practice (a Pillar 3 violation if tuned too low) or an equalizer that erases the active-play advantage (a violation of "higher capture chances" if tuned too high). Must be revisited alongside `combat-encounter-system`'s own `active_efficiency_cap_percent` during Vertical Slice playtesting. |

## 8. Acceptance Criteria

### Functional

1. **No punishing-the-competent (required)**: given Formula 4's "modest, ordinary play" worked example (`capture_favorability_score = 7`, 0 parts broken) at default tuning, `capture_success_chance_percent ≥ 50%` against Uncommon and Rare tiers — the two tiers accounting for 90.4% of all non-common rarity rolls (Formula 1) — verified against the exact worked-example table in §4.
2. **Capture offers something Core Hatching does not (required)**: for any successful capture, the resulting `CreatureInstance` differs from what an equivalent Core Hatching would produce in at least one of `power_tier` (Formula 6) or `evolution_progress` (Formula 5) whenever `power_tier > 1` at capture or `capture_favorability_score > 0` — verified by asserting the two Formula 5/6 outputs are strictly greater than a hatched creature's baseline (`power_tier = 1`, `evolution_progress = 0`) under those conditions.
3. **No essential progression material is capture-exclusive (required, Pillar 3)**: for every `EvolutionNode` in the shipped roster, `node_eligible` (`creature-jobs-evolution-system` Formula 4) can reach `true` using only Core-Hatching-obtainable inputs (ordinary Feed actions building Vector 5, ordinary job ticks building Vector 3, ordinary matching-tag core feeds building Vector 4, ordinary charm equips satisfying Vector 2) with zero contribution from this document's Formula 5 bonus — verified by a fixture that zeroes this document's head start entirely and confirms every branch remains reachable through hatching-only play.
4. Formula 1's worked-example distribution (§4) reproduces within normal statistical variance across a 10,000-trial simulation at default tuning (chi-squared goodness-of-fit against the stated probabilities).
5. Formula 2's worked example reproduces exactly: `effective_max_health = 92`, `capture_eligibility_hp_threshold_percent = 25` → eligible at `current_health ≤ 23`.
6. Formula 3's worked example reproduces exactly: an un-warded rare creature at `current_health = 40` taking `final_damage = 65` resolves to `current_health = 1`, `capture_ward_consumed = true`; a subsequent `final_damage = 10` resolves to `current_health = 0` (Kill fires) with no further Ward protection.
7. A core-part break against a rare creature, at any HP value and regardless of `capture_ward_consumed`, always results in a normal Kill outcome — never a Ward clamp — verified across a fixture forcing a core break at `current_health = 1` (post-Ward) and at full health (pre-Ward).
8. Formula 4's six worked examples (§4 table) reproduce exactly at default tuning, including the `95%` cap on the "Focused, skilled play / Uncommon" row (verifying the outer clamp engages correctly).
9. Formula 5's worked example reproduces exactly: `capture_favorability_score = 14`, Uncommon → `evolution_progress = 51`; same favorability, Legendary → `evolution_progress = 256`.
10. `AttemptCapture` issued against any `tier = boss` instance never resolves eligible, verified by a fixture attempting it against a boss at 1% HP with `rarity_tier` forcibly set to `legendary` (an intentionally invalid state) — the boss-tier check must reject regardless of rarity data integrity.
11. An automated encounter's capture roll always substitutes `automated_capture_favorability_baseline` for `capture_favorability_score`, verified by a fixture with a deliberately extreme live score (e.g., 50) that produces no effect on an automated roll's outcome.

### Experiential (validated by playtest)

12. A playtester who successfully captures a rare creature can, unprompted, articulate that the creature they now own is "the one I fought," not "a new one that happened to spawn" — validating that `instance_id`/history preservation (§3.5) is legible without an explanation.
13. A playtester who lands the hit that would have killed a rare creature reports relief and continued engagement ("oh, it survived — now what do I do?"), not confusion about why the creature didn't die — validating that the Capture Ward's mercy is read as a deliberate game grace, not a bug.
14. A playtester comparing a captured creature's roster entry to a hatched sibling of the same template can correctly identify, without being told, that the captured one is "further along" or "stronger" — validating that Formula 5/6's bonuses are perceptible in the UI, not just present in data.
15. A playtester who never actively hunts a rare creature (idle-only play across several sessions) still reports having captured at least one by the time they've automated their first region — validating A8's "possible, not essential" balance in practice, not just on paper.
