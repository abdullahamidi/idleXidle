# Encounter Spawn System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored in rush mode per `production/session-state/active.md` (no user available this session). Written specifically to close Blocker B2 from `design/gdd/gdd-cross-review-2026-07-14.md` and to supply the `par_clear_time_seconds` anchor Blocker B1's fix (being applied concurrently to `combat-encounter-system.md` and `region-mastery-automation-system.md`) depends on. Every ambiguity is resolved with an explicit, flagged design call, never a placeholder. Validate at `/design-review` before Production. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #27). |
| **Depends On** | `design/gdd/creature-data-schema.md` **only** — deliberately. See Assumption A1 and §6 for why an edge to `region-mastery-automation-system` would close a dependency cycle. |
| **Depended On By** | `combat-encounter-system`, `creature-ai-telegraph-system`, `loot-drop-system`, `region-view-world-map-ui` (direct, per `systems-index.md`'s Dependency Map); `region-mastery-automation-system`, `creature-jobs-evolution-system`, `automation-config-ui`, `rare-creature-capture-system`, `onboarding-tutorial-system` (indirect — consume `power_tier` or the spawn event downstream of a capture/hatch or automation hand-off, not this document directly) |

## Source Material Read

`design/gdd/gdd-cross-review-2026-07-14.md` (Blocker B2 in full — the missing-owner finding this document resolves, and Blocker B1 — the active/idle circularity `par_clear_time_seconds` is built to fix), `design/gdd/systems-index.md` (system #27's entry and footnote ⁴ — the locked dependency-direction ruling and the pre-authored one-paragraph brief for this exact document), `design/gdd/creature-data-schema.md` (full — `CreatureTemplate`, `PartDefinition`, `CreatureInstance.power_tier`, Formula 1 `effective_max_health_by_power_tier`, the registered `tier_health_scalar` constant), `design/gdd/combat-encounter-system.md` (§3.2 Encounter State Machine, §3.9 Encounter End/rewards, §4 Formula 3 damage pipeline and Formula 7 `active_efficiency_percent` — read to understand what this document's outputs feed, not copied as a dependency), `design/gdd/creature-ai-telegraph-system.md` (Formula 1 windup scaling by `power_tier`, read for consumer confirmation only), `design/gdd/loot-drop-system.md` (Formula 2 rarity tilt by `power_tier`, read for consumer confirmation only), `design/gdd/region-mastery-automation-system.md` (§3.1–3.6 region state machine, mastery levels, RMP, Automation Capability Stages, Formula 6 — read to source the `region_progression_index`/`boss_available` external-input contract, never to create a dependency edge), `design/gdd/rare-creature-capture-system.md` (§3.0, §4 Formula 1 rarity roll, A1/A6/A9 — read to confirm the spawn-time trigger point and the locked boss-never-rare rule), `design/gdd/region-view-world-map-ui.md` (§3.2 Region View chrome, and its `systems-index.md` dependency-map note that it "renders the selectable encounters"), `design/gdd/onboarding-tutorial-system.md` (§3.8, §4 Formula 1/2 — the curated first-region pacing table this document's override hook must reproduce exactly), `design/gdd/game-concept.md` (Pillars 1–5 and Anti-Pillars, full), `design/registry/entities.yaml` (read for `tier_health_scalar` — reused, not redefined).

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **This document depends on `creature-data-schema.md` only.** `region_progression_index` and `boss_available` are consumed as opaque external **input values** passed into `SpawnEncounter` at call time, never read by this document from `region-mastery-automation-system` as a system dependency. | `systems-index.md` footnote ⁴ locks this explicitly: an edge to `region-mastery-automation-system` would close the cycle `region-mastery → combat-encounter → encounter-spawn → region-mastery`, since `region-mastery-automation-system` already depends on `combat-encounter-system`, which now depends on this document. Treating mastery state as data-in, not a system-in, is the only acyclic shape available. |
| A2 | **The region's boss encounter template is never included in the weighted random draw (Formula 1).** It is spawned only via an explicit `requested_template_id` call, gated by `boss_available`. | art-bible §2 and `region-mastery-automation-system` §3.7 both treat the Mastery Transition as the single most ceremonially important beat in the game ("coronation, not shutdown"). Letting the boss appear via bad luck on a routine Hunt would undercut that ceremony and could ambush an unprepared player. An explicit, deliberate trigger keeps the boss a chosen event, matching how every other document treats it (a milestone, never a random encounter). |
| A3 | **Boss templates are structurally excluded from the idle/automated spawn path — `SpawnEncounter(trigger = idle)` never returns an `is_boss = true` template, even if one is mistakenly requested.** | Two independent reasons converge: (1) `region-mastery-automation-system` §3.5 frames Stage 1 Auto-Attack as triggering kills "against the region's hostile population," which reads naturally as the standard population, not the singular boss; (2) a boss's `par_clear_time_seconds` (Formula 4) is necessarily far longer than a standard encounter's (§4 worked examples: ~15–24s standard vs. ~591s boss) — folding it into a routine automated cadence would produce a wildly unstable comparison denominator for no gameplay benefit, since (§3.7) `par_clear_time_seconds` is a comparison constant, not a scheduling delay, and automated production ticks proceed on their own fixed cadence regardless of which template is nominally credited. |
| A4 | **Every boss template's `creature_template_pool` must reference a creature that is also reachable (directly or via evolution) from at least one standard, idle-eligible encounter template in the same region.** | Direct implementation of the task brief's Pillar 3 requirement ("idle must not gate any essential material behind an encounter only reachable by active play"). This document cannot control what `loot-drop-system` attaches to a boss kill, but it *can* guarantee the underlying species/template is never boss-exclusive — the one lever this document actually owns toward that guarantee. |
| A5 | **A `power_tier_override` parameter on `SpawnEncounter` lets a calling system (specifically `onboarding-tutorial-system`) bypass Formula 2 entirely and supply an exact `power_tier`.** | `onboarding-tutorial-system` §3.8/§4 Formula 2 already authors an exact `power_tier` per first-region encounter (1, 2, 3, 3–4) as "ordinary content-authoring decisions" and explicitly states the ramp is achieved by *choosing* those inputs, not by a new curve. Rather than inventing parallel curated-template variants, this document exposes the one hook that document's own design already assumes exists. The override is still hard-clamped to `[1, 20]` (Edge Case #7) — the global safety net is never bypassable, only the formula's own drift/variance math is. |
| A6 | **`region_progression_index` is an integer `0`–`4`: `0` = unconquered, `1`–`4` = `mastery_level_index + 1` once mastered** (Newly Conquered → 1 … Optimized → 4). | This is the exact same mapping `region-mastery-automation-system` A6 already uses for `loot-drop-system`'s `automation_stage` input. Reusing it (as a numeric convention, not a dependency) means a single external integer already computed elsewhere in the project satisfies this document's input without inventing a second, competing region-progress scale. |
| A7 | **`par_clear_time_seconds` is derived from the template's own `power_tier_base` (its single canonical anchor tier), never from a spawn's live, rolled `power_tier`.** | The task brief and `systems-index.md` footnote ⁴ both require this value to be FIXED — "independent of the player's live mastery, team, or automation throughput." A value that moved with each spawn's rolled `power_tier` would silently reintroduce exactly the circularity Blocker B1 exists to remove, just one hop later. |
| A8 | **A creature-species sub-draw (which entry of a template's `creature_template_pool` actually spawns) reuses the identical weighted-categorical mechanism as Formula 1**, rather than being specified as a second, separate formula. | The two draws are mathematically identical (weighted categorical selection over an authored list) — writing a second formula block for the same math would be redundant, not more rigorous. Both worked examples in §4 demonstrate the shared mechanism at each of its two call sites. |

---

## 1. Overview

The encounter spawn system is Resonance Hunter's **producer layer** — the system that answers the question every other combat- and progression-facing document had been silently deferring: *what creature does the player actually fight, at what power, and how often?* It owns three things: the **encounter template** schema (the authored content unit a region's hostile population is built from), the **`power_tier` assignment rule** (a deterministic-but-varied formula that scales a spawn's difficulty with the region's own progression, satisfying every downstream consumer that reads `CreatureInstance.power_tier`), and **`par_clear_time_seconds`** (a fixed, hand-authored-per-template reference clear time that is the single 100%-anchor both the active and idle efficiency contracts are measured against — the exact value Blocker B1's fix needs and did not, until now, have anywhere to read from). It exposes one call, `SpawnEncounter`, used identically by the player's active "Hunt" action and by a region's automated Attacker — the same pool, the same formula, the same guarantees, satisfying Pillar 3's requirement that idle and active draw from one real population, never two.

## 2. Player Fantasy

This is a foundation/producer system, not a moment-to-moment mechanic — its player fantasy, like `creature-data-schema`'s, is what it *makes possible* rather than what it directly delivers:

> **The region you're standing in is inhabited, not conjured.** Every creature you meet is drawn from a real, legible population that belongs to this specific place — not a random number wearing a reskinned sprite, and not a scripted gauntlet with a fixed answer key. The deeper your command over a region grows, the more dangerous and more rewarding that population visibly becomes, whether you're the one swinging or your automated team is.

Concretely, this document is what guarantees:

- **A region has a population, not a script.** Which creature shows up next is a real weighted draw over an authored pool (§3.3, §4 Formula 1) — replayable, never a fixed sequence past the curated first four encounters, so returning to a mastered region still feels like hunting, not repeating a memorized script.
- **Difficulty is a visible consequence of your own progress, not an arbitrary dial.** `power_tier` climbs with the region's own mastery level (§4 Formula 2), so a player who has fought for a region's automation feels that region's wildlife get correspondingly tougher and more valuable — the same "you earned this" logic `region-mastery-automation-system` already applies to production, now applied to what you fight.
- **The boss is an event, not a gacha roll.** It never ambushes a routine hunt (A2) — reaching it is always a deliberate choice, matching the ceremonial weight art-bible §2 locks for that moment.
- **Idle farming fights something real.** An automated region's Attacker draws from the exact same standard population (§3.7) an active hunter does — never a fake, simplified, or lesser encounter set, directly answering Pillar 3's "idle is never worthless."

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the `EncounterTemplate` schema, a region's encounter pool (implicitly, every template sharing a `region_id`), the weighted-random encounter-selection algorithm, the `power_tier` assignment formula, the `par_clear_time_seconds` authoring formula, and the `SpawnEncounter` call contract both active and automated triggers use. It does **not** own, and defers to the cited document in every case: what a `CreatureTemplate`/`PartDefinition` actually contains (`creature-data-schema`), the encounter state machine once a creature is spawned, damage resolution, or `active_efficiency_percent` (`combat-encounter-system`), telegraph timing (`creature-ai-telegraph-system`), loot table contents (`loot-drop-system`), the region state machine, mastery levels, RMP, or automation cadence (`region-mastery-automation-system`), rarity rolls or capture mechanics (`rare-creature-capture-system`), and the curated first-region pacing values themselves (`onboarding-tutorial-system` — this document only supplies the bypass hook, A5).

### 3.1 Two Producer Concepts: EncounterTemplate and the Region Pool

Every spawn is drawn from an **`EncounterTemplate`** — an authored, static content unit, one per distinct "thing a player can encounter in a region" (analogous to `creature-data-schema`'s `CreatureTemplate` split: templates are content, not save data). A region's **encounter pool** is simply every `EncounterTemplate` sharing that region's `region_id` — no separate pool table exists; grouping by `region_id` is the pool. Within a region's pool, exactly one template must have `is_boss = true` (§3.7); the rest are `is_boss = false` ("standard").

An `EncounterTemplate` does not itself define a creature — it references one or more entries in `creature-data-schema`'s `CreatureTemplate` catalog via `creature_template_pool` (§3.2), so the same encounter slot (e.g., "Whelp Thicket") can present visual/stat variety across repeated visits without needing a 1:1 encounter-to-creature mapping.

### 3.2 EncounterTemplate Schema

| Field | Type | Range / Values | Description |
|---|---|---|---|
| `template_id` | string | Unique, pattern `enc_[region_id]_[variant]_[std\|boss]`, e.g. `enc_verdanthollow_whelp_std`, `enc_verdanthollow_thornmaw_boss` | Canonical unique ID. |
| `display_name` | string | Free text, 1–48 chars | Shown wherever a region's population is previewed (`region-view-world-map-ui`, not defined by this document). |
| `region_id` | string | Opaque reference to a Region entity | The region this encounter belongs to. Region itself is owned by `region-mastery-automation-system` (not a dependency of this document, A1) — this field is a plain foreign key, read but never validated against that system's own state. |
| `is_boss` | bool | — | Marks the region's single ceremonial boss encounter. Exactly one `true` per `region_id` (Edge Case #8 covers the load-time invariant). |
| `creature_template_pool` | list of `{creature_template_id: string, weight: float}` | 1+ entries. Every entry's `creature-data-schema` `tier` must equal `boss` if `is_boss = true`, else `standard` (Edge Case #8). | Candidate `CreatureTemplate`s this encounter may spawn. One is chosen per spawn via the same weighted-categorical mechanism as Formula 1 (A8). MVP templates typically carry exactly one entry. |
| `power_tier_base` | int | 1–20 | Authoring anchor difficulty — the `power_tier` this encounter would resolve to at `region_progression_index = 0` with zero variance roll (Formula 2). Also the tier Formula 4 evaluates `par_clear_time_seconds` at (A7). |
| `power_tier_min` | int | 1–20, ≤ `power_tier_max` | Authored floor clamp on Formula 2's output for this template. |
| `power_tier_max` | int | 1–20, ≥ `power_tier_min` | Authored ceiling clamp on Formula 2's output for this template — the mechanism that keeps a single MVP region's difficulty ramp bounded even at the region's highest mastery level (§4 Formula 2, Worked Example C). |
| `selection_weight` | float | ≥ 0. `0` excludes the template from Formula 1's random draw (still spawnable via `requested_template_id`). Ignored entirely for `is_boss = true` templates (A2). | Relative weight in the region's standard-pool draw (Formula 1). |
| `rare_eligible` | bool | Default `true` if `is_boss = false`; **hard-forced `false`** if `is_boss = true`, regardless of authored value (Edge Case #9) | Whether `rare-creature-capture-system`'s rarity roll (its Formula 1, invoked at this document's spawn event, §3.6) is permitted to run against this spawn. Exists as authoring-level control beyond the `tier = standard` gate that system's own Formula 1 already applies, e.g. to exclude a specific scripted non-rare standard encounter. |
| `par_clear_time_seconds` | float | > 0 | The fixed, hand-authored reference clear time (§3.5, §4 Formula 4) — the 100% anchor for both the active-efficiency band and the idle-efficiency band per `systems-index.md` footnote ⁴. Never recomputed at runtime; a cached authored value a designer re-derives by hand when its inputs change (Edge Case #10). |
| `par_clear_time_source_dps` | float, cached | > 0 | The `reference_team_effective_dps` value Formula 4 used when `par_clear_time_seconds` was last authored — stored purely for re-derivation auditability, never read by any other system. |

**Invariant (validated at content-load time, never silently at runtime — Edge Cases #5, #8, #9)**: `power_tier_base ∈ [power_tier_min, power_tier_max]`; every `creature_template_pool` entry's `tier` matches `is_boss`; `is_boss = true ⇒ rare_eligible = false`.

### 3.3 Encounter Selection

**Standard draw** (used whenever a caller does not supply `requested_template_id`): a weighted-categorical draw over every `is_boss = false` template sharing the requested `region_id`, per §4 Formula 1. This is the only path idle/automated calls use (A3).

**Explicit request** (`requested_template_id` supplied): bypasses the random draw entirely and spawns exactly that template, subject to two checks — (1) if `is_boss = true`, `boss_available` (an external input, §3.6) must be `true`, else the request is rejected and this document falls back to a standard draw instead (Edge Case #11); (2) if the trigger is `idle`, an `is_boss = true` request is always rejected regardless of `boss_available` (A3, Edge Case #11) — this document enforces the idle/boss exclusion itself, not merely by calling convention.

This dual-mode contract is deliberately compatible with either a single "Hunt" button (which simply omits `requested_template_id` and lets Formula 1 decide) or a future selection UI that lets the player see and choose among specific upcoming encounters (`region-view-world-map-ui`'s own dependency-map note that it "renders the selectable encounters") — this document does not require or preclude either UI shape.

### 3.4 Power Tier Assignment

Once a template (and, via the same mechanism, a specific `creature_template_id` from its pool) is resolved, the spawned `CreatureInstance.power_tier` is computed by §4 Formula 2 — a drift term (the template's own base difficulty plus a scaled contribution from how far the region has progressed) plus a bounded random variance roll, clamped to the template's own authored band. This is the formula that gives the MVP region "a real difficulty ramp" the task brief requires: the same "Whelp Thicket" encounter spawns visibly weaker creatures in a freshly-conquered region than in a fully-mastered one, without ever needing a second template.

`power_tier_override` (A5) bypasses this formula entirely for a single spawn — used by `onboarding-tutorial-system` to reproduce its own curated first-region table exactly (§4 Formula 2, Worked Example D).

### 3.5 par_clear_time_seconds — Authoring Contract

`par_clear_time_seconds` answers one question and one question only: *how long would a competent, fully-mastered automated team take to clear this specific encounter template?* It is computed once, by a designer, using §4 Formula 4, at the moment the template is authored (or re-authored after a balance pass), and stored as a plain data field (§3.2) — never recomputed live. Two properties make it fit for its job as the shared efficiency anchor:

1. **It is evaluated at the template's own `power_tier_base`** (A7), never at a spawn's live rolled `power_tier` — so it does not move as the region's mastery climbs, closing exactly the circularity Blocker B1 flagged.
2. **It is a comparison constant, not a scheduling delay.** Neither active play's `active_clear_time_seconds` measurement (`combat-encounter-system` Formula 7) nor automation's own production cadence (`region-mastery-automation-system`, a fixed `work_tick_interval_seconds`) waits on this value — it exists purely as the denominator/reference both systems' own efficiency percentages divide against (A3).

### 3.6 The Spawn Event — Interface Contract

```
SpawnEncounter(
    region_id:              string,
    trigger:                "active" | "idle",
    region_progression_index: int [0-4],       // external input, A6 — not a system dependency
    boss_available:          bool,               // external input — not a system dependency
    requested_template_id:   string, nullable,   // §3.3
    power_tier_override:     int, nullable        // A5, clamped [1,20] regardless (Edge Case #7)
) -> SpawnResult {
    creature_instance:       CreatureInstance,    // creature-data-schema §3.5, bind_state = hostile,
                                                    // power_tier populated per Formula 2 or the override
    encounter_template_id:   string,
    par_clear_time_seconds:  float                 // passthrough of the resolved template's field
}
```

Immediately after `creature_instance` is minted (fresh `hostile_state`, all parts intact, per `creature-data-schema` §3.5/§3.6) and before `SpawnResult` is returned, this is the trigger point `rare-creature-capture-system`'s own Formula 1 rarity roll fires against, whenever `rare_eligible = true` on the resolved template — this document supplies the moment, never the roll or the resulting `rarity_tier` (owned entirely by that system, its own side table, per its A10).

Both the active "Hunt" flow and a region's automated Attacker call this exact same function — the only difference is `trigger`, which affects nothing except the idle/boss exclusion (A3) and which of `combat-encounter-system`'s full state machine vs. `region-mastery-automation-system`'s instant production-tick resolution consumes the result.

### 3.7 Boss Availability and Repeatability

`boss_available` is computed entirely outside this document (by `onboarding-tutorial-system` pre-mastery, per its own Formula 1 gate on `encounters_completed_in_region`; trivially `true` for any already-mastered region) and consumed here as an opaque bool. Once `true`, the boss template becomes requestable (§3.3) for `active` triggers indefinitely — a region's boss is not a one-time-only encounter; a player may re-enter it via an explicit request for repeat rewards, matching `region-view-world-map-ui`'s "Hunt... for both unconquered and mastered regions" with no separate re-entry restriction. It is never requestable for `idle` triggers, permanently (A3).

**Note on `encounters_completed_in_region`**: `onboarding-tutorial-system` §4 Formula 1 names this counter as tracked by "a not-yet-written region-progression system" it coordinates with. This document is not that system — it holds no persistent per-region or per-session counters of any kind (every `SpawnEncounter` call is a pure function of its inputs, A1). `region-mastery-automation-system` is the more natural owner, since it already owns the region state machine this counter gates; flagged forward for that document's own confirmation, not resolved here.

### 3.8 MVP Region Content (Worked Example Pool)

Illustrative, unbalanced-pending-playtest content for the single MVP region ("Verdant Hollow"), used throughout §4's worked examples:

| `template_id` | `is_boss` | `creature_template_pool` (id, `base_health`) | `power_tier_base` | `power_tier_min`–`max` | `selection_weight` |
|---|---|---|---|---|---|
| `enc_verdanthollow_whelp_std` | false | `nature_atk_whelp_std`, 80 | 1 | 1–3 | 10 |
| `enc_verdanthollow_mossling_std` | false | `nature_sup_mossling_std`, 65 | 1 | 1–3 | 10 |
| `enc_verdanthollow_bramblehide_std` | false | `nature_def_bramblehide_std`, 110 | 2 | 1–4 | 8 |
| `enc_verdanthollow_thornmaw_boss` | true | `nature_atk_thornmaw_boss`, 1800 | 4 | 3–5 | n/a (A2) |

`base_health` values deliberately reuse `creature-data-schema`'s own worked-example figure (80) and `rare-creature-capture-system`'s own "Verdant Whelp" worked example (`power_tier = 2 → effective_max_health = 92`, which requires `base_health = 80`) — the same creature, consistently, across three independently-authored documents. Flagged in this document's closing summary as a candidate for `design/registry/entities.yaml`.

## 4. Formulas

All outputs round to the nearest integer using round-half-up, matching this project's established convention, unless stated otherwise.

### Formula 1 — Encounter Template Selection (Weighted Categorical Draw)

```
P(template_i) = selection_weight_i / Σ(selection_weight_j for all j in StandardPool(region_id))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `StandardPool(region_id)` | set, external input | Every `EncounterTemplate` with matching `region_id` and `is_boss = false` | The draw's eligible set (A2 — boss is never a member). |
| `selection_weight_i` | float, authored per template | ≥ 0 (§3.2) | The template's relative share of the draw. |
| `P(template_i)` | float | `[0, 1]`, sums to `1.0` over the pool | Output. Probability this specific template is chosen. |

**Output range**: exactly one template chosen per draw; probabilities sum to `1.0` by construction as long as at least one template has `selection_weight > 0` (Edge Case #1 covers the degenerate all-zero case). The same mechanism, applied to a template's `creature_template_pool` weights instead of region templates, resolves which specific `CreatureTemplate` spawns (A8) — not a second formula, the identical math at a second call site.

**Worked example**: Verdant Hollow's standard pool (§3.8): `{Whelp: 10, Mossling: 10, Bramblehide: 8}`, `Σ = 28`.
`P(Whelp) = 10/28 ≈ 35.7%`, `P(Mossling) = 10/28 ≈ 35.7%`, `P(Bramblehide) = 8/28 ≈ 28.6%`. The boss template is absent from this set entirely, by construction, regardless of `boss_available`.

### Formula 2 — Power Tier Assignment at Spawn

```
power_tier_drift  = round(power_tier_base + region_progression_index × mastery_power_tier_step_per_level)
power_tier_rolled = power_tier_drift + variance_roll
power_tier        = clamp(power_tier_rolled, power_tier_min, power_tier_max)

variance_roll ~ uniform_int[-power_tier_variance, +power_tier_variance]   (drawn from the shared spawn RNG stream)
```

If `power_tier_override` is supplied (A5), this entire formula is bypassed: `power_tier = clamp(power_tier_override, 1, 20)`.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `power_tier_base` | int, template field | 1–20 (§3.2) | The template's authored anchor difficulty. |
| `region_progression_index` | int, external input | `0`–`4` (A6) | `0` unconquered; `1`–`4` = `mastery_level_index + 1` once mastered. |
| `mastery_power_tier_step_per_level` | float, tuning knob | 0.5–3.0 (default `1.0`) | How many `power_tier` points one full mastery-level step contributes. |
| `variance_roll` | int | `[-power_tier_variance, +power_tier_variance]` | Random per-spawn spread, from the shared game RNG stream in production; injected as a fixed value under test (deterministic-for-testing, per A8's framing and §8 AC8). |
| `power_tier_variance` | int, tuning knob | 0–3 (default `1`) | Ceiling on `variance_roll`'s magnitude. |
| `power_tier_min` / `power_tier_max` | int, template fields | 1–20 each (§3.2) | Authored per-template safety band; always the final clamp, even if drift + variance would exceed it. |
| `power_tier_override` | int, optional external input | Any int (hard-clamped `[1, 20]` regardless, Edge Case #7) | Bypass path for curated content (A5). |
| `power_tier` | int | `[1, 20]` always | Output. Written to the spawned `CreatureInstance.power_tier`. |

**Output range**: always an integer in `[1, 20]` — either `[power_tier_min, power_tier_max] ⊆ [1,20]` via the formula path, or the hard `[1,20]` clamp via the override path.

**Worked examples** (Verdant Hollow templates, §3.8, default tuning `mastery_power_tier_step_per_level = 1.0`, `power_tier_variance = 1`):

| # | Template | `region_progression_index` | `variance_roll` | Calculation | `power_tier` |
|---|---|---|---|---|---|
| A | Whelp Thicket (base 1, band 1–3) | 0 (unconquered) | 0 | `round(1+0×1.0)=1`; `1+0=1`; `clamp(1,1,3)` | **1** |
| B | Bramblehide Den (base 2, band 1–4) | 3 (fully_mastered) | +1 | `round(2+3×1.0)=5`; `5+1=6`; `clamp(6,1,4)` | **4** (clamped) |
| C | Bramblehide Den (base 2, band 1–4) | 4 (optimized) | −1 | `round(2+4×1.0)=6`; `6−1=5`; `clamp(5,1,4)` | **4** (clamped — the template's own ceiling holds even at the region's maximum mastery, keeping the MVP region's difficulty bounded by design, not by accident) |
| D | Whelp Thicket, `power_tier_override = 1` (onboarding's curated Encounter 1, A5) | n/a — bypassed | n/a — bypassed | `clamp(1, 1, 20)` | **1** (formula never runs; reproduces `onboarding-tutorial-system` §4 Formula 2's own Encounter 1 row exactly) |

### Formula 3 — Reference Team Effective DPS by Power Tier

A support formula for Formula 4 — models how much a hypothetical "competent, fully-mastered automated team" can sustain against a target at a given `power_tier`, standing in for the fact that higher-tier creatures mitigate more damage.

```
reference_team_effective_dps(power_tier) = max(
    reference_team_dps_floor,
    reference_team_base_dps × (1 − reference_team_dps_falloff_scalar × (power_tier − 1))
)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `reference_team_base_dps` | float, tuning knob | 3–15 (default `6.0`, damage/second) | This document's own authored estimate of a fully-mastered reference team's sustained damage output against a `power_tier = 1` target — deliberately independent of any live team's actual composition (A1); a designer input, not a simulated one. |
| `reference_team_dps_falloff_scalar` | float, tuning knob | 0.02–0.15 (default `0.05`) | How much the reference team's effective DPS erodes per power tier, standing in for rising target mitigation. Moves in the same direction as, but is tuned independently of, `combat-encounter-system`'s `tier_defense_scalar` — no dependency edge is created by that similarity. |
| `reference_team_dps_floor` | float, tuning knob | 0.5–2.0 (default `1.0`) | Guards against zero or negative effective DPS at extreme `power_tier` values. |
| `power_tier` | int, input | 1–20 | The tier Formula 4 is deriving a `par_clear_time_seconds` for — always the template's `power_tier_base` (A7). |
| `reference_team_effective_dps` | float | `≥ reference_team_dps_floor` | Output, fed into Formula 4. |

**Output range**: `[reference_team_dps_floor, reference_team_base_dps]`, monotonically non-increasing in `power_tier` at default tuning (§8 AC10).

**Worked example**: default tuning, `power_tier = 4`: `max(1.0, 6.0 × (1 − 0.05×3)) = max(1.0, 6.0×0.85) = 5.1`.

### Formula 4 — par_clear_time_seconds Derivation

```
par_clear_time_seconds = round(
    (effective_max_health / reference_team_effective_dps(power_tier_base)) × (boss_overhead_multiplier if is_boss else 1)
    + par_clear_time_overhead_seconds
)

effective_max_health = effective_max_health_by_power_tier(base_health, tier_health_scalar, power_tier_base)
                        — creature-data-schema Formula 1, reused by exact name (design/registry/entities.yaml)
```

`base_health` is taken from the **highest-`base_health` entry** in the template's `creature_template_pool`, so `par_clear_time_seconds` reflects the hardest creature the encounter can actually present, never an optimistic average.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `effective_max_health` | int, derived | ≥ 1 | `creature-data-schema` Formula 1's output at `power_tier_base` — see that document's exact variable table for `base_health`/`tier_health_scalar`. |
| `reference_team_effective_dps(power_tier_base)` | float, derived | Formula 3's output | Evaluated at the template's own anchor tier (A7), never a spawn's rolled tier. |
| `boss_overhead_multiplier` | float, tuning knob | 1.0–1.5 (default `1.15`) | Accounts for boss-only dead time (phase transitions, telegraph-heavy attack windows a reference team must still respect) that a naive HP/DPS division would undercount. Applied only when `is_boss = true`. |
| `par_clear_time_overhead_seconds` | float, tuning knob | 0–5 (default `2.0`) | Flat non-DPS overhead common to every encounter (spawn-in beat, first-engage delay). |
| `par_clear_time_seconds` | float | > 0, unbounded above | Output. Authored once, cached on the template (§3.2), never recomputed live (A7, §3.5). |

**Output range**: strictly positive, unbounded above — a boss template's much higher `effective_max_health` and `boss_overhead_multiplier` are expected to, and do, produce a far larger value than a standard template's (see worked examples below); this is intentional, not a bug, since `par_clear_time_seconds` is a comparison constant, never a scheduling delay (§3.5).

**Worked examples** (Verdant Hollow, §3.8, default tuning: `reference_team_base_dps = 6.0`, `reference_team_dps_falloff_scalar = 0.05`, `reference_team_dps_floor = 1.0`, `par_clear_time_overhead_seconds = 2.0`, `boss_overhead_multiplier = 1.15`, `tier_health_scalar = 0.15`):

| Template | `base_health` | `power_tier_base` | `effective_max_health` | `reference_team_effective_dps` | Calculation | `par_clear_time_seconds` |
|---|---|---|---|---|---|---|
| Whelp Thicket | 80 | 1 | `round(80×(1+0.15×0))=80` | `6.0` | `round(80/6.0 × 1 + 2.0) = round(15.33)` | **15s** |
| Mossling Grove | 65 | 1 | `round(65×1)=65` | `6.0` | `round(65/6.0 + 2.0) = round(12.83)` | **13s** |
| Bramblehide Den | 110 | 2 | `round(110×(1+0.15×1))=round(126.5)=127` | `6.0×0.95=5.7` | `round(127/5.7 + 2.0) = round(24.28)` | **24s** |
| Thornmaw (boss) | 1800 | 4 | `round(1800×(1+0.15×3))=round(2610)=2610` | `6.0×0.85=5.1` | `round((2610/5.1)×1.15 + 2.0) = round(590.53)` | **591s** (~9.85 min) |

## 5. Edge Cases

1. **A region's standard pool is empty, or every standard template has `selection_weight = 0`.** Formula 1 has no valid draw. This is a content-authoring defect, not a live-game state — validated and rejected at content-load time (a hard load failure, never a silent empty/null spawn at runtime). Every region must ship with at least one `selection_weight > 0` standard template; two or more is recommended so the weight field is meaningful, not enforced.
2. **`boss_available = true` but `requested_template_id` for that boss is never supplied by any caller during a session.** Not an error — the boss simply remains available-but-unfought; no timer or forced trigger exists (matches "no free-roam traversal / player always chooses" throughout the project).
3. **A `requested_template_id` for an `is_boss = true` template arrives while `boss_available = false`.** Rejected; this document falls back to a standard weighted draw (§3.3) rather than erroring the caller — a boss request is a *hope*, not a hard demand, from the caller's perspective.
4. **Concurrent active and idle spawn calls for the same `region_id`** (the player Hunts while that region's own Attacker also ticks automation in the background, per `region-mastery-automation-system` §5 Edge Case 4's explicit concurrency allowance). No race exists — `SpawnEncounter` holds no shared mutable per-region state (A1); each call independently draws its own RNG value and returns its own `instance_id`.
5. **A template is authored with `power_tier_base` outside `[power_tier_min, power_tier_max]`, or `power_tier_min > power_tier_max`.** Hard invariant violation, rejected at content-load time (§3.2's Invariant line) — never resolved by silent clamping at runtime, since that would hide an authoring mistake rather than surface it.
6. **`region_progression_index` arrives out of range** (e.g., negative, or `> 4`, from a malformed or future-version caller). Defensively clamped to `[0, 4]` before use in Formula 2, rather than raising — mirrors `combat-encounter-system`'s own established precedent (its Edge Case #5, `power_tier` clamped at `ENGAGING`) of failing safe on an external input rather than crashing the encounter pipeline.
7. **`power_tier_override` supplied outside `[1, 20]`** (e.g., a future content bug in `onboarding-tutorial-system`). Still hard-clamped to `[1, 20]` — the global safety net applies unconditionally, even though the override otherwise bypasses Formula 2 entirely (A5).
8. **A `creature_template_pool` entry's `creature-data-schema` `tier` does not match the encounter template's own `is_boss` flag** (e.g., a standard encounter accidentally referencing a `tier = boss` `CreatureTemplate`). Rejected at content-load time (§3.2's Invariant line) — never silently spawned.
9. **A template is authored with `is_boss = true` and `rare_eligible = true`**, contradicting the locked rule (`rare-creature-capture-system` A6: bosses never roll rare). Rejected at content-load time as a defense-in-depth check, and additionally hard-forced to `false` at runtime regardless of the authored value, so a load-time validation gap can never surface as a live rule violation.
10. **`par_clear_time_seconds` drifts stale** after a later balance pass changes `base_health`, `power_tier_base`, or `tier_health_scalar` on a referenced `CreatureTemplate`. This document does not auto-recompute the cached value (A7, §3.5) — a designer must explicitly re-run Formula 4 and re-author it. Flagged here as a content-maintenance responsibility; a build-time lint step that recomputes Formula 4 for every template and warns on drift beyond a tolerance is recommended tooling, not specified by this design document.
11. **An `idle`-triggered call supplies `requested_template_id` referencing an `is_boss = true` template** (a calling-convention bug, e.g., a misconfigured automation hook). Rejected unconditionally, regardless of `boss_available`; this document falls back to a standard weighted draw for that call instead (A3) — the idle/boss exclusion is enforced by this document itself, never merely assumed of its callers.
12. **A captured creature's `power_tier` traces back to a spawn that used `power_tier_override`** (a curated onboarding encounter later captured via `rare-creature-capture-system`). No special case exists — `power_tier` is `power_tier` regardless of which path (Formula 2 or the override) produced it, exactly matching `rare-creature-capture-system` A4's own framing ("inherits the hostile instance's own value at the moment of capture").

## 6. Dependencies

### Producer Contract

Every downstream consumer of `power_tier` found by `grep -rn "power_tier" design/gdd/`, and every consumer of `par_clear_time_seconds`, cross-checked against what this document supplies:

| Consumer | What it reads | Satisfied by |
|---|---|---|
| `creature-data-schema.md` | `CreatureInstance.power_tier` field, `1`–`20`, "defaults to 1 if unset" | §3.6's `SpawnEncounter` always populates `power_tier` via Formula 2 or the override (never leaves it unset for a spawned instance); output range `[1,20]` matches exactly (§4 Formula 2). |
| `combat-encounter-system.md` | (a) `power_tier` at `ENGAGING` to init `hostile_state`; (b) Formula 3 Step 4 `effective_defense` scales by `power_tier`; (c) Formula 7's `CTR` term needs a fixed clear-time anchor (post-B1-fix) in place of the circular `automation_baseline_clear_time_seconds`; (d) `CreatureDefeated` event payload includes `power_tier` | (a)/(b)/(d): `power_tier` is set before `SpawnResult` is returned, always in `[1,20]`. (c): `SpawnResult.par_clear_time_seconds` (§3.6) is the exact fixed-anchor value `systems-index.md` footnote ⁴ specifies; §3.5 states explicitly it never moves with live mastery/team/throughput, which is the property Formula 7's fix requires. |
| `creature-ai-telegraph-system.md` | Formula 1 `windup_duration_ms` and the vulnerability-window `window_tier_factor` formula both read `CreatureInstance.power_tier` directly, range `1`–`20` | Same `power_tier` field, same range, populated identically regardless of which system reads it after spawn. |
| `loot-drop-system.md` | Formula 2 `tilt_multiplier` reads `power_tier`, range `1`–`20`, external input | Same field/range. |
| `creature-jobs-evolution-system.md` | Formula 5 `role_output_per_tick` reads a **bound** `CreatureInstance.power_tier` (for creatures assigned to jobs) | Indirect: a bound creature's `power_tier` originates either from `rare-creature-capture-system` A4 (inherits the hostile instance's `power_tier` at capture — sourced from this document's spawn) or defaults to `1` for a hatched creature (a different path this document does not touch). This document's contract is satisfied at the point of capture, not directly. |
| `region-mastery-automation-system.md` | Formula 3 `PQS` reads mean assigned-team `power_tier`; A4/Formula 6 needs a fixed clear-time anchor to replace `automation_baseline_clear_time_seconds` (the B1 fix) | `PQS` indirectly via the same capture/hatch path as above. The B1 fix itself: this document supplies `par_clear_time_seconds` per template (§3.5); wiring `region-mastery-automation-system`'s own idle-efficiency Formula 6 to read it by name is that document's own edit, made by the concurrent agent resolving B1 — not this document's file to touch. |
| `automation-config-ui.md` | `power_tier` of team-assignment candidates, `power_tier_reference_ceiling` (20) | Same field/range; `power_tier_reference_ceiling` is `creature-data-schema`'s own registered ceiling, unchanged by this document. |
| `rare-creature-capture-system.md` | (a) Formula 1's rarity roll needs a spawn-time trigger; (b) A4: captured `power_tier` inherits the hostile instance's value at capture | (a) §3.6 explicitly names the moment a fresh `hostile_state` is minted as that trigger point. (b) That value is exactly this document's Formula 2/override output, unmodified by anything between spawn and capture. **Flag**: that document's own §6 currently attributes the spawn-time trigger to `region-mastery-automation-system` ("must supply... the trigger/timing for Formula 1's rarity roll at hostile spawn time") — written before this document existed, per the B2 finding. This document is the correct owner of that trigger now; flagged here for that document's own future correction pass, not fixed here under this session's single-file-ownership constraint. |
| `onboarding-tutorial-system.md` | Recommended `power_tier` per curated first-region encounter (1, 2, 3, 3–4); `encounters_completed_in_region` counter | `power_tier_override` (A5) reproduces that document's exact Formula 2 table (§4 Formula 2, Worked Example D). The counter itself is out of this document's scope (§3.7's note) — flagged toward `region-mastery-automation-system`, not resolved here. |
| `creature-roster-ui.md` | Hatched creature `power_tier = 1` override | Not this document's path (hatching bypasses `SpawnEncounter` entirely) — no contradiction; noted for completeness only. |
| `region-view-world-map-ui.md` | `systems-index.md`'s dependency map: "it renders the selectable encounters" | §3.3's dual-mode contract (standard draw vs. explicit request) supports either a single-button Hunt flow or a future selection UI without requiring this document to assume which. |

### Standard Dependency Listing

**Depends On**: `design/gdd/creature-data-schema.md` (`CreatureTemplate.template_id`/`tier`/`base_stats.base_health`, `CreatureInstance.power_tier` field shape, Formula 1 `effective_max_health_by_power_tier` reused by exact name in Formula 4) — **only**, by explicit design (A1).

**Note on bidirectionality**: `creature-data-schema.md`'s own "Depended On By" list does not yet name this document — it was authored before this system existed, per the B2 finding that created it. This is a known, flagged gap for that document's own future maintenance pass; this document cannot correct it under its single-file-ownership constraint this session.

**Depended On By** (direct, per `systems-index.md`): `combat-encounter-system`, `creature-ai-telegraph-system`, `loot-drop-system`, `region-view-world-map-ui`.

**Depended On By** (indirect — via capture/hatch or automation hand-off, not a direct read of this document): `region-mastery-automation-system`, `creature-jobs-evolution-system`, `automation-config-ui`, `rare-creature-capture-system`, `onboarding-tutorial-system`.

**External inputs consumed, not system dependencies** (A1, per `systems-index.md` footnote ⁴): `region_progression_index` (int, `0`–`4`), `boss_available` (bool) — both computed elsewhere (`region-mastery-automation-system`, `onboarding-tutorial-system`) and passed into `SpawnEncounter` as plain call-time data.

## 7. Tuning Knobs

| Knob | Formula | Safe Range | Default | Gameplay Impact |
|---|---|---|---|---|
| `mastery_power_tier_step_per_level` | Formula 2 | 0.5–3.0 | 1.0 | How steeply a region's difficulty ramps per mastery level. Higher = a fully-mastered region's wildlife noticeably outpaces a freshly-conquered one; too high risks a mastered region's standard encounters overshooting the boss's own `power_tier_base`. |
| `power_tier_variance` | Formula 2 | 0–3 | 1 | Spawn-to-spawn difficulty spread within a single mastery state. `0` makes every spawn at a given progression point identically difficult (useful for curated/tutorial templates via tight `power_tier_min`/`max` bands); higher adds unpredictability players must read on the fly. |
| `power_tier_min` / `power_tier_max` (per template) | Formula 2 | 1–20 each, template-authored | Template-specific (§3.8 example: `1–3`, `1–4`, `3–5`) | The hard ceiling/floor on any single encounter template's rolled difficulty — the primary lever for keeping the MVP region's ramp bounded regardless of mastery-level math. |
| `selection_weight` (per template) | Formula 1 | ≥ 0, template-authored | Template-specific (§3.8 example: 8–10) | Relative frequency of a standard encounter within its region's pool. `0` retires a template from random rotation without deleting it. |
| `reference_team_base_dps` | Formula 3 | 3–15 | 6.0 | The single biggest lever on every `par_clear_time_seconds` value — and therefore, once the B1 fix wires it in, on both the active and idle efficiency bands' real-world calibration. Raising it shortens every derived `par_clear_time_seconds`, which *raises* `CTR` for a fixed `active_clear_time_seconds`, inflating `active_efficiency_percent` project-wide. **BALANCE-CRITICAL.** |
| `reference_team_dps_falloff_scalar` | Formula 3 | 0.02–0.15 | 0.05 | How much a higher `power_tier` inflates a boss/high-tier encounter's derived `par_clear_time_seconds` relative to a low-tier one. |
| `reference_team_dps_floor` | Formula 3 | 0.5–2.0 | 1.0 | Prevents `par_clear_time_seconds` from exploding toward infinity for very high `power_tier_base` templates in Full Vision content. |
| `par_clear_time_overhead_seconds` | Formula 4 | 0–5 | 2.0 | Flat floor added to every derived `par_clear_time_seconds` — keeps even a trivial `power_tier = 1` encounter's anchor above zero. |
| `boss_overhead_multiplier` | Formula 4 | 1.0–1.5 | 1.15 | Inflates a boss's `par_clear_time_seconds` beyond a pure HP/DPS estimate, accounting for phase/telegraph dead time a reference team must still respect. |

## 8. Acceptance Criteria

| # | Criterion | Verification |
|---|---|---|
| AC1 | Formula 2's four worked examples (§4) reproduce exactly: Whelp Thicket at unconquered → `power_tier = 1`; Bramblehide Den at fully-mastered with `+1` variance → clamped to `4`; Bramblehide Den at optimized with `−1` variance → clamped to `4`; Whelp Thicket with `power_tier_override = 1` → `1` (formula bypassed). | Unit test against the fixture, asserting all four exact outputs. |
| AC2 | Formula 1's weighted draw, run 100,000 times against the Verdant Hollow standard pool (§3.8) with a fixed RNG seed sequence, produces observed per-template frequencies within 1 percentage point of the theoretical `P(template_i)` values in §4's worked example. | Statistical unit test sweeping the fixed pool. |
| AC3 | For every `power_tier_base` (1–20), every `region_progression_index` (0–4), and every `variance_roll` within `[-power_tier_variance, +power_tier_variance]`, Formula 2's output always falls within `[power_tier_min, power_tier_max]` for that template — never outside the authored band, at any tuning-knob value in its safe range. | Unit test sweeping the full input space against the clamp. |
| AC4 | An `is_boss = true` template is never returned by `SpawnEncounter(trigger = "idle")`, for any combination of `requested_template_id` and `boss_available`, including a deliberately malformed request naming the boss template. | Regression unit test against Edge Case #11. |
| AC5 | `rare_eligible` resolves to `false` for every template with `is_boss = true` in a fixture sweep, regardless of the field's authored value. | Unit test asserting the runtime hard-force (Edge Case #9) independent of content authoring. |
| AC6 | Formula 4's four MVP worked examples (§4) reproduce exactly: Whelp Thicket `15s`, Mossling Grove `13s`, Bramblehide Den `24s`, Thornmaw (boss) `591s`, given the stated default tuning and `base_health` inputs. | Unit test against the fixture, asserting all four exact outputs. |
| AC7 | A `power_tier_override` supplied at any pathological value (e.g., `-500`, `0`, `999`) is always clamped into `[1, 20]` in the resulting `CreatureInstance.power_tier`. | Unit test sweeping extreme override inputs. |
| AC8 | Given a fixed RNG seed, two consecutive `SpawnEncounter` calls with byte-identical inputs (`region_id`, `trigger`, `region_progression_index`, `boss_available`, no override) produce byte-identical `SpawnResult`s (determinism). Given two different seeds, the same inputs produce a different `variance_roll` and/or template selection in at least one of 20 repeated trials (variance is genuinely live, not accidentally hardcoded). | Unit test pair — one asserting equality under a fixed stubbed RNG, one asserting divergence under two distinct stubbed RNGs. |
| AC9 | Every `EncounterTemplate` in a validated content fixture has a positive `par_clear_time_seconds`, and every `SpawnResult` returned by `SpawnEncounter` in a fixture sweep has a non-null `CreatureInstance.power_tier` in `[1,20]` — the two fields every downstream consumer in the Producer Contract (§6) requires are never absent. | Structural fixture test. |
| AC10 | `reference_team_effective_dps` (Formula 3) is monotonically non-increasing across `power_tier = 1..20` at default tuning — it never rises as `power_tier` increases. | Unit test sweeping the full tier range and asserting each successive value is `≤` the previous. |
| AC11 | Every `EncounterTemplate` with `is_boss = true` in a region's content fixture has at least one `creature_template_pool` entry whose species (or an evolution-tree descendant of it, per `creature-data-schema` §3.4) also appears in at least one `is_boss = false, selection_weight > 0` template in the same `region_id` (A4 — the Pillar 3 species-availability guarantee this document can actually enforce). | Structural fixture test cross-referencing the boss and standard pools per region. |
