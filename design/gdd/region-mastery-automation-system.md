# Region Mastery & Automation System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | systems-designer |
| **Status** | Complete — authored in rush mode per `production/session-state/active.md` (no user available this session). Every ambiguity is resolved with an explicit, flagged design call in the Assumptions Log below, never a placeholder. **This is the game's thesis system — Pillars 2 and 3 both live here.** Validate at `/design-review` and `/gate-check` before Production. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #14). MVP ships **Automation Stages 1–2 only** (auto-attack + auto-loot); Stages 3–4 are fully designed here but Vertical-Slice-deferred, per game-concept.md's explicit MVP scope. |
| **Depends On** | `design/gdd/combat-encounter-system.md`, `design/gdd/creature-jobs-evolution-system.md`, `design/gdd/save-load-persistence.md`, `design/gdd/loot-drop-system.md` |
| **Depended On By** | `automation-config-ui`, `region-view-world-map-ui`, `memory-dust-prestige-system`, `audio-system` (none yet written) |

## Source Material Read

`design/gdd/creature-jobs-evolution-system.md` (full — the Healthy/Blocked/Starved work-state
machine and the five per-Role production/consumption profiles this document sits on top of, and
whose two external boolean signals, `input_resource_available`/`output_capacity_available`, this
document is the one that must compute), `design/gdd/combat-encounter-system.md` (full —
`active_efficiency_percent`, Formula 7, the active side of the contract this document must balance
against — both sides are now measured against the same fixed `par_clear_time_seconds` anchor owned by
`encounter-spawn-system`; this document supplies **nothing** into that formula, which was Blocker B1's
bug), `design/gdd/save-load-
persistence.md` (full — `last_ticked_at` per-region and Formula 3's `ElapsedMs`/`elapsed_time_delta`,
built specifically for this document to consume), `design/gdd/loot-drop-system.md` (full — the
Stage-3 inflation-spiral finding this document is tasked with resolving, and the `automation_stage`
input this document must supply to that system's Formula 2), `design/gdd/game-concept.md` (Pillars,
Core Loop, MVP Definition), `design/art/art-bible.md` §2 (the Mastery Transition's locked emotional
beat and glyph-inversion visual, whose mechanical trigger this document owns), `design/gdd/systems-
index.md` (this system's entry and dependents), `design/registry/entities.yaml` (read for reusable
constants — `power_tier_output_scalar`/`tier_health_scalar` convention confirmed and reused for
internal consistency).

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Mastery *level* (the four-tier efficiency ladder) is driven by a new scalar meter this document owns, Region Mastery Points (RMP)** — filled by sustained *healthy team work-ticks* (creature-jobs-evolution-system's own Formula 2 signal, summed across the assigned team regardless of Role) plus a bonus for *active* kills fought inside the region — never by wall-clock time alone and never by raw kill-count alone. RMP never decreases and mastery level never demotes. | Wall-clock time alone would let a player leave a single Attacker idling and pyramid mastery for free, contradicting Pillar 2 ("automation is earned"). Raw kill-count alone would reward spamming a single cheap Role over a composed team. Gating on *healthy* ticks specifically means Blocked/Starved time earns nothing (§3.5, mirroring creature-jobs-evolution-system A8's identical logic for evolution progress) — mastery has to be *worked* for, not merely waited out. Never demoting matches that document's own A7 precedent (no punishment through neglect) applied at the region level. |
| A2 | **The top mastery tier, Optimized, requires both an RMP threshold *and* a structurally complete team (all four "chain roles" filled) at the moment of the threshold check.** If RMP clears the threshold but the team is incomplete, mastery caps at Fully Mastered until a complete team is assigned on some later check. | This is the direct, mechanical answer to the brief's composition-puzzle requirement: it is now *structurally impossible* to reach the top efficiency band by piling up high-tier creatures in a narrow role set — the gate itself demands the same "generator + Crafter + Defender + Support" chain creature-jobs-evolution-system §3.6 already named as "what makes a good team," not just this document's own bonus multiplier rewarding it (§4 Formula 3/4). |
| A3 | **The continuous, within-tier efficiency modifier (`team_quality_score`) is a slow-moving *structural* signal (roster composition + average power tier), never a *momentary* one (current Healthy/Blocked/Starved fraction).** | creature-jobs-evolution-system's own Formula 5 already zeroes a Blocked/Starved creature's individual output. If this document's region-wide multiplier *also* dropped every time any one creature Blocked, a single stalled Crafter would double-penalize the entire team's output — once at the creature level (correctly) and once again at the region level (redundant, and punishing in a way Pillar 3 doesn't intend). Structural composition/quality change slowly and deliberately, by player choice, not by momentary supply-chain hiccups. |
| A4 | **This document does NOT supply the reference the active player is scored against.** `idle_efficiency_percent` (Formula 4) and `combat-encounter-system`'s `active_efficiency_percent` (its Formula 7) are **both** percentages of the same fixed, externally-authored `par_clear_time_seconds` (owned by `encounter-spawn-system`, per encounter template). This document *consumes* par to derive `automation_clear_time_seconds` (Formula 6) as a **reporting output**; it produces no input to any formula that scores the player. | **Rewritten 2026-07-14 to close Blocker B1.** A4 originally had this document *supply* `automation_baseline_clear_time_seconds` into `combat-encounter-system` Formula 7's `CTR` denominator. Because that value is derived from `idle_efficiency_percent`, which **rises with mastery**, the reference the active player was measured against *moved as their own automation improved* — so identical play scored 166% → 92.5% → 63.2% → 50% across the mastery ladder while automation did ~3.1× the kills/hour. Pillar 3 ("Active Is Better") was arithmetically inverted. The anchor must be a constant the player cannot move, and it must live with *the encounter*, not with *the player's progress*. Note the old derivation was quietly computing par all along: at `idle_efficiency_percent = 100` — i.e. a competent fully-mastered team, which is par's definition — it returned exactly the 15s Attacker cadence. It had the right number and the wrong owner. |
| A5 | **The four Automation Capability Stages (auto-attack / auto-loot / auto-craft-sell / offline progression) are gated at *escalating* mastery levels, not all four at the single boss-kill moment.** Stage 1–2 unlock together at Newly Conquered (the boss kill itself — matching game-concept.md's MVP text exactly). Stage 3 (economy-touching) requires Fully Mastered. Stage 4 (time-skip) requires Optimized. | game-concept.md's MVP Definition locks only the Stage 1–2 gate explicitly; it does not specify Stage 3/4's gate, leaving it to this document (its own stated scope). Escalating the gate with the stage's *economic risk* is a direct, finer-grained application of Pillar 2 ("automation is earned") and is also this document's primary structural defense against the Stage-3 inflation spiral (§4 Formula 7) — the riskiest capability is deliberately the hardest-earned, not just rate-capped after the fact. |
| A6 | **loot-drop-system's `automation_stage` input (its Formula 2, the rarity-parity dial) is redefined here as `mastery_level_index + 1`** (1–4), decoupled from the four Automation *Capability* Stages of A5 above — a genuine naming collision between the two documents, resolved by this document since loot-drop-system A4 explicitly left `automation_stage`'s exact semantics and its Stage-3/4 parity values to this document. Fully Mastered → 75% parity, Optimized → 100% parity (extending that document's own authored Stage 1 → 0% / Stage 2 → 50%). | Under this document's Capability Stage gating (A5), Stages 1 and 2 both unlock at the single Newly Conquered moment — so a literal "Capability Stage count" would jump straight to 2 and never move again, making loot-drop-system's Stage 1-vs-2 rarity distinction meaningless in practice. Re-keying `automation_stage` to mastery *level* instead is a far better semantic fit (a more-mastered region's automation finds better loot) and closes the exact open item that document's A4 flagged as unauthored ("later stages... not authored here"). |
| A7 | **This document owns `region_storage_capacity`** (per resource type, a concrete number) **as the literal implementation of creature-jobs-evolution-system's externally-supplied `output_capacity_available` boolean.** | That document's §3.5/Formula 6 defines the Blocked state entirely in terms of an external boolean it does not compute, naming this document as the owner. Giving it a real, tunable number is also the direct answer to the brief's "storage caps that force player return" option — already half-designed by that document's Blocked state; this document supplies the missing number. |
| A8 | **The Stage-3 Gleam-realization cap (`region_auto_sell_gleam_cap_per_hour`) is a single GLOBAL, account-wide cap shared across every region's Stage-3 automation simultaneously — never a per-region cap.** | A per-region cap would be trivially multiplied by mastering additional regions (a Full-Vision-scale concern, since MVP/VS both explicitly ship only one region — game-concept.md's MVP Definition). Making the cap global now, while there is only one region to test it against, avoids a future retrofit once multi-region play exists and directly protects the numeric proof in §4 Formula 7 from being invalidated by scope growth. |
| A9 | **Offline Progression (Stage 4) is computed with a closed-form, time-to-storage-saturation approximation, not a full tick-by-tick replay of the elapsed offline window.** | game-concept.md's own Key Technical Challenges line names exactly this tension: "offline-progression simulation (accurate, performant time-elapsed catch-up)." A full replay of a 72-hour clamp window at multi-second tick cadences is a real performance concern this document is right to design around rather than ignore; the closed-form approximation (§4 Formula 8) is accurate at the one moment that actually matters for the brief's own named edge case — the instant production would have first stalled on a full input/output constraint — while remaining O(1) per resource, not O(ticks). |
| A10 | **`offline_elapsed_upper_clamp_hours`** (the field save-load-persistence's own Tuning Knobs table explicitly left unset and named this document as the owner of) **is set at a finite default of 72 hours.** A 30-day absence is treated identically to a 72-hour one — no additional benefit accrues past the clamp. | Matches creature-jobs-evolution-system's own "no catch-up for missed time beyond what the rules define" philosophy (its A9/Edge Case 9) extended to the offline case: nothing is destroyed or penalized by a long absence, but idle progression cannot be worth literally unbounded real time either — that would make the very first offline-capable region a source of unbounded currency/material generation, the same failure class this document's whole Stage-3 analysis (§4 Formula 7) exists to prevent. |

---

## 1. Overview

The region mastery & automation system is Resonance Hunter's **thesis system** — the single
document where Pillar 2 (Automation Is Earned, Not Assumed) and Pillar 3 (Active Is Better, Idle Is
Never Worthless) both become concrete, testable rules rather than aspirations. It owns four things:
the **region state machine** (unconquered → mastered, and the four-tier mastery-*level* ladder that
follows a boss kill), the **idle efficiency curve** that converts a region's mastery level and its
assigned team's quality into a single percentage governing how much an automated region produces
relative to active play, the **four Automation Capability Stages** (auto-attack, auto-loot,
auto-craft/sell, offline progression) and the mastery gate each one requires, and the **economic
faucet cap** that keeps Stage 3's auto-sell capability from inflating the game's currency by two
orders of magnitude, the exact failure mode `loot-drop-system` proved was otherwise inevitable. It
consumes `creature-jobs-evolution-system`'s Healthy/Blocked/Starved work-state machine and per-Role
production formulas directly rather than redefining them, and it is the system that finally
supplies the one external input `combat-encounter-system`'s own active/idle contract (Formula 7)
has been waiting for since it was authored. Every other system in the project that touches "what
happens when the player isn't looking" is built on top of what this document defines.

## 2. Player Fantasy

> **You fought for this, and now it works for you.** Not a menu toggle that was always available —
> a region you bled for, one boss at a time, that now runs without you because you earned the right
> to walk away from it. The empire that runs itself, one hard-won region at a time.

Concretely, this system delivers on that fantasy by making three things always true:

- **Nothing automates before it's earned.** A region produces exactly nothing — no auto-attack, no
  auto-loot, no idle income of any kind — until its boss falls to the player's own hands (§3.2). The
  Mastery Transition (§3.7) is the moment the game keeps its promise that automation is a reward for
  mastery, not a default state the player merely hasn't turned off yet — this is Pillar 2 made
  literal, at the exact moment art-bible §2 calls "the game's core emotional thesis."
- **The mastery ladder is a real, climbable goal, not a hidden number.** A freshly conquered region
  runs at a fraction of what it could — visibly, legibly worse than a region the player has kept
  fed, well-composed, and returned to. Climbing from Newly Conquered to Optimized (§3.3, §4 Formula
  4) is the same kind of explicit, provable mastery curve game-concept.md's own Retention Hooks
  names as this game's core Competence loop — and reaching the top of it demands the same
  thoughtful, composed team the brief asked this document to reward, not a pile of the
  highest-tier creatures money can buy (A2).
- **Idle time is never a lie.** Every material an automated region produces is the same material
  active play produces — never a fake currency, never a diluted substitute (§8 AC). The active
  player still, always, comes out ahead (combat-encounter-system's own hard-locked ~3x ceiling,
  §4), but the idle player is never told "come back later for the real rewards." This is Pillar 3's
  own design test, answered structurally: yes, every core resource is idle-farmable, because nothing
  here requires a decision idle genuinely can't make.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the region state machine and mastery-level ladder, the idle efficiency curve,
the four Automation Capability Stages and their gates, team-slot assignment rules and the region's
demand profile, the two boolean work-state signals `creature-jobs-evolution-system` consumes as
external input, `region_storage_capacity`, the Stage-3 Gleam-realization throttle, and offline
progression. It does **not** own, and defers to the cited document in every case: a creature's Role,
its production/consumption behavior, and the Healthy/Blocked/Starved transition rule itself
(`creature-jobs-evolution-system`), the encounter state machine and `active_efficiency_percent`
(`combat-encounter-system`), the loot table/rarity/drop-count math and the Loot Filter Rule Engine's
evaluation semantics (`loot-drop-system`), the save envelope and `ElapsedMs` mechanism
(`save-load-persistence`), and the-forge-system's own Sell/Dismantle operations (this document
*invokes* them for Stage 3, per a throttle it owns, but does not redefine what selling or
dismantling means).

### 3.1 The Region State Machine

Two states, one irreversible transition:

```
unconquered → mastered
```

| State | Meaning | What's Available |
|---|---|---|
| `unconquered` | The region's boss has not yet been defeated by the player. | Active combat only (`combat-encounter-system`, full state machine). No team assignment, no automation of any kind — `creature-jobs-evolution-system` §3.2 step 3 enforces this precondition independently on its own consumption path. |
| `mastered` | The region's boss has been defeated at least once. Permanent — a region never reverts to `unconquered` (no destructive mechanic exists anywhere in this project's design that would justify it, matching the "no permadeath / no un-earning progress" precedent `creature-jobs-evolution-system` A7 already set for creatures). | Team assignment; Automation Capability Stages per their own mastery-level gates (§3.6); active combat remains fully available and concurrent with automation (§5 Edge Case 4). |

The transition fires exactly once, at the **Mastery Transition** (§3.7), triggered by the specific
`CreatureDefeated` event whose `template_id` matches this region's authored boss template and whose
`tier = boss` (`creature-data-schema`'s `Tier` enum, read via `combat-encounter-system`'s event).

### 3.2 Mastery Level and Region Mastery Points (RMP)

Once `mastered`, a region additionally carries a **mastery level** — four ordered tiers driving the
idle efficiency curve (§4 Formula 4):

| Index | Mastery Level | Efficiency Band (locked contract) |
|---|---|---|
| 0 | `newly_conquered` | 25–40% |
| 1 | `partially_mastered` | 50–70% |
| 2 | `fully_mastered` | 80–100% |
| 3 | `optimized` | 100–120% |

A region enters `mastery_level = 0` (`newly_conquered`) the instant it becomes `mastered` (§3.7),
with `region_mastery_points = 0`. **Region Mastery Points (RMP)** is a monotonically
non-decreasing scalar meter (never decays, never resets — A1) filled by two sources, evaluated
every tick (§4 Formula 1):

1. **Healthy team work-ticks.** Every resolved production tick (`creature-jobs-evolution-system`
   §3.5/Formula 2's exact cadence and Healthy-only gate) completed by *any* creature assigned to
   this region contributes RMP, regardless of that creature's Role — this is deliberately the same
   underlying signal that document already tracks for evolution progress, read here a second time
   for a second purpose, not recomputed.
2. **Active kills fought inside this region**, post-mastery (i.e., the player returning to actively
   hunt a region they've already conquered) — a flat bonus per `CreatureDefeated` event whose
   originating encounter was active, rewarding direct engagement on top of whatever the assigned
   team is earning passively (§5 Edge Case 4 confirms these two sources are fully concurrent, never
   mutually exclusive).

`mastery_level` is recomputed every time RMP updates, by threshold (§4 Formula 2). Advancing to
`optimized` (index 3) additionally requires the assigned team's **Composition Completeness Score**
(§3.4, §4 Formula 3) to equal exactly `1.0` at the moment the RMP threshold is checked — a region
that clears the RMP bar with an incomplete team simply holds at `fully_mastered` (a visible,
diagnosable gate, matching `creature-jobs-evolution-system`'s own "fails visibly, not silently"
philosophy at §3.5) until a complete team is assigned on some later check (A2). Once `optimized` is
reached, it is permanent (the ratchet) — reassigning away from a complete team afterward does not
demote `mastery_level`, though it does immediately affect the *live* efficiency percentage computed
within that band (§4 Formula 4, §5 Edge Case 5).

### 3.3 Team Composition, Slots, and the Region Demand Profile

Every `mastered` region exposes **`region_team_slot_count`** slots (default 6, §7) that any bound,
unassigned `CreatureInstance` may be placed into, subject only to `creature-jobs-evolution-system`
§3.2 step 3's Region-Mastered precondition — this document adds no further per-creature eligibility
rule (Role, power tier, and source are all valid in any slot).

**The region's demand profile** — what this document actually asks a team to provide, restated
plainly — is the same four-role production chain `creature-jobs-evolution-system` §3.6 already named
as "what makes a good team": at least one **generator** (an Attacker and/or a Producer — either
satisfies this slot of the chain, matching that document's own "and/or" framing exactly), a
**Crafter**, a **Defender**, and a **Support**. This document's own contribution is making that
chain *count* mechanically, not just descriptively: the **Composition Completeness Score (CCS)**
(§4 Formula 3) is the fraction of those four chain roles present anywhere in the assigned team
(0, 0.25, 0.5, 0.75, or 1.0), and CCS is the dominant term (weighted higher than raw creature power,
§4 Formula 3) in both the continuous within-tier efficiency bonus (§4 Formula 4) and the hard gate
into the Optimized tier (§3.2). **This is the direct, load-bearing answer to the composition
puzzle**: a team of six maxed-power-tier Producers has `CCS = 0.25` (one chain role, generator, is
technically covered — but not Crafter, Defender, or Support) and can never reach Optimized and
caps out well below a modestly-powered but *complete* six-role team's efficiency within every other
tier too. A single MVP region does not need its own bespoke demand weighting to make this puzzle
real — the same four-role chain applies everywhere at MVP scope; a Full Vision region is free to
author its own weighted demand profile (e.g. a region that emphasizes raw extraction over refining)
by overriding CCS's role-weighting per region, a forward-compatible authoring hook this document
does not need to build out further until more than one region exists.

### 3.4 Work-State Signals Supplied to `creature-jobs-evolution-system`

That document's §3.5/Formula 6 defines the Blocked/Starved transition entirely in terms of two
externally-supplied booleans this document computes, once per tick, per assigned creature:

- **`input_resource_available`** — `true` for a Crafter only if this region's tracked raw-material
  stock (the accumulated output of its own generator Role(s), §4 Formula 5) has at least one
  consumable unit available this tick; `true` unconditionally for every other Role (matching that
  document's own "trivially true for roles with no input requirement" rule). For Support, `true`
  only if a valid teammate target is currently assigned to the same region (that document's own
  "requires a valid teammate target" rule, §3.6).
- **`output_capacity_available`** — `true` only if that Role's storable output resource has not yet
  reached **`region_storage_capacity`** (§7, default 500 units per resource type, per region — the
  concrete number this document supplies for the "storage caps force player return" brake, A7).
  Once a resource's stored amount reaches this cap, every Role emitting that resource reports
  `false` here until the player collects/processes the backlog (via `automation-config-ui` or a
  manual Forge visit) and frees capacity.

### 3.5 The Four Automation Capability Stages

Each stage is gated by **mastery level**, not by a separate unlock currency or timer — a deliberate,
escalating application of Pillar 2 (A5): the more economically consequential a capability is, the
higher the mastery bar required to earn it.

| Stage | Gate (mastery level ≥) | What It Does | MVP? |
|---|---|---|---|
| **1 — Auto-Attack** | `newly_conquered` (0) — i.e., the Mastery Transition itself | An assigned, Healthy Attacker automatically triggers kills against the region's hostile population, exactly as `creature-jobs-evolution-system` §3.6 already specifies for that Role — no player input required. | **Yes** |
| **2 — Auto-Loot** | `newly_conquered` (0) — unlocks simultaneously with Stage 1, matching game-concept.md's MVP text verbatim ("Automation Stages 1–2... unlocked by the region boss kill") | Every `LootResult` batch (`loot-drop-system` §3.3 step 9) produced by a Stage-1 automated kill is collected into the player's inventory automatically, without requiring presence — without this stage, Stage 1's kills would produce loot nobody ever receives. | **Yes** |
| **3 — Auto-Craft/Sell** | `fully_mastered` (2) | An assigned, Healthy Crafter automatically consumes raw materials into refined goods (already `creature-jobs-evolution-system`'s own production model, now unattended); additionally, every item the Loot Filter Rule Engine (`loot-drop-system` §3.9) tags `auto_sell`/`auto_dismantle` is automatically submitted to `the-forge-system`'s Sell/Dismantle operations — Sell specifically throttled by this document's Gleam-realization cap (§3.6, §4 Formula 7). | No — designed, VS-deferred |
| **4 — Offline Progression** | `optimized` (3) | Elapsed real-world time since the region's `last_ticked_at` (`save-load-persistence` Formula 3) is converted into bounded catch-up production the instant the region is next ticked, respecting the same Blocked/Starved constraints active play would have hit (§3.7, §4 Formula 8). | No — designed, VS-deferred |

Stages 1–2 being bundled at the single boss-kill gate is a locked MVP fact, not this document's
choice to relitigate. Stages 3–4 requiring progressively higher mastery is this document's own
design call (A5), made specifically because it is also the primary structural defense against the
Stage-3 inflation spiral this document was tasked with resolving (§4 Formula 7) — a region cannot
even attempt unattended currency realization until it has proven, through RMP, that it has been
actively and thoughtfully managed for a substantial stretch.

**Cross-document terminology note (A6)**: `loot-drop-system`'s own `automation_stage` input (its
Formula 2, gating loot rarity parity) is **not** the same axis as the four Capability Stages above.
This document supplies that system's `automation_stage` as `mastery_level_index + 1` (so Newly
Conquered → 1, Partially Mastered → 2, Fully Mastered → 3, Optimized → 4), extending that document's
own authored Stage 1 → 0% / Stage 2 → 50% rarity-parity table with this document's own Fully
Mastered → 75% / Optimized → 100% values, closing the exact gap that document's A4 left open.

### 3.6 Stage 3 — The Auto-Sell Throttle (Preview; full mechanism and proof in §4 Formula 7)

Per-item, Stage 3's automatic Sell action is subject to a single account-wide (never per-region, A8)
**`region_auto_sell_gleam_cap_per_hour`** (default 24, §7, **BALANCE-CRITICAL**) — Gleam value beyond
that hourly ceiling simply remains queued, unsold, in inventory (never lost, never destroyed, per
Pillar 3) until either more real-time hours open more of the cap, or the player visits the Forge
manually and sells the backlog instantly and uncapped (manual Sell is, and remains,
un-throttled — the firewall `loot-drop-system` Formula 8 already proved exists between drop-rate
and currency-realization is *strengthened*, not removed, by this cap). Auto-Dismantle is not subject
to this cap (its output is materials, governed by `region_storage_capacity` instead, §3.4) — only
Sell, the direct currency-creation action, is throttled. The full numeric proof this cap resolves
the inflation spiral `loot-drop-system` flagged is §4 Formula 7.

### 3.7 The Mastery Transition — Mechanical Sequence

art-bible §2 locks the emotional and visual beat ("coronation, not shutdown" — the boss's own
vulnerability glyph healing and inverting into the ward-seal that binds it) as the single most
important moment in the game's visual language. This document owns only the **mechanical** sequence
that beat rides on top of:

1. The region's designated boss's `is_core` part breaks or `hostile_state.current_health` reaches 0
   while `region_state = unconquered` (`combat-encounter-system` §3.9 priorities 1–2 — an ordinary
   Kill outcome, mechanically identical to any other boss kill up to this point).
2. `combat-encounter-system` fires `CreatureDefeated` and transitions to `RESOLVING` for
   `3000ms` (its own boss-duration default, §3.9) — the glyph-inversion cinematic plays during this
   window. This document takes **no** action during `RESOLVING` — no state changes, no automation
   ticks begin — the ceremony beat is uninterrupted (matching that document's own "no new player
   actions begin" framing, extended here to "no new automation begins," for the same reason: the
   payoff must not be stepped on by background logic).
3. `loot-drop-system` resolves the boss's guaranteed drops and creature core exactly as any other
   Kill (§3.1 there) — reward resolution is unaffected by, and does not wait for, this document's
   own transition.
4. The instant `combat-encounter-system` reaches `COMPLETE` (§3.2 there) for this specific
   `CreatureDefeated` event, this document checks: does the defeated `template_id` match this
   region's authored boss template, and was `region_state = unconquered` immediately prior? If both
   hold, this is the Mastery Transition: `region_state → mastered`, `mastery_level → 0`
   (`newly_conquered`), `region_mastery_points → 0`, and `last_ticked_at` (`save-load-persistence`'s
   per-region field) is initialized to the current timestamp — this document is the owner of that
   write from this point forward for this region.
5. Automation Capability Stages 1–2 are immediately available (§3.5) — control returns to the region
   hub with team assignment now unlocked. No further player action is required for the transition to
   be considered complete; the *first* automated production tick fires the next time this region is
   ticked (in-session background ticking, per `save-load-persistence`'s own documented mechanism, the
   instant a team is assigned).

A non-boss kill inside an `unconquered` region (any standard-tier creature) never triggers this
sequence — only the region's own designated boss, matching `combat-encounter-system`'s own
distinction between a Kill outcome's ordinary defeat beat and the ceremonial one (§3.9 there).

## 4. Formulas

All formulas share the project's established round-half-up convention for any output that must
resolve to an integer. **Formula 4 (idle efficiency) is the single most balance-critical formula in
this project** — every other system's active/idle comparison is measured against it.

### Formula 1 — Region Mastery Points (RMP) Accrual

```
rmp_gain_this_tick = (healthy_team_members_this_tick × rmp_per_healthy_member_tick)
                    + (active_kills_this_tick × rmp_per_active_kill_bonus)

region_mastery_points_new = region_mastery_points_old + rmp_gain_this_tick
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `healthy_team_members_this_tick` | int | 0 to `region_team_slot_count` | Count of assigned creatures whose `work_state = healthy` resolved a production tick this tick (`creature-jobs-evolution-system` §3.5). |
| `rmp_per_healthy_member_tick` | float, tuning knob | 0.5–2.0 (default 1) | RMP earned per Healthy creature per resolved tick, regardless of Role. |
| `active_kills_this_tick` | int | ≥ 0 | Count of `CreatureDefeated` events this tick whose originating encounter was active (`combat-encounter-system`), fought inside this region, post-mastery. |
| `rmp_per_active_kill_bonus` | float, tuning knob | 2–10 (default 5) | Flat RMP bonus per active kill — deliberately larger per-event than a single healthy tick, rewarding direct engagement without making it mandatory. |
| `region_mastery_points` | float | ≥ 0, unbounded above | Never decreases (A1). Drives mastery-level threshold checks (Formula 2). |

**Output range**: unbounded above, monotonically non-decreasing. **Worked example**: a complete
6-slot team (Attacker `work_tick_interval_seconds=15`, Crafter `=8`, Defender `=25`, Support `=15`,
Producer `=40`, all `creature-jobs-evolution-system` §7 defaults) all Healthy continuously for one
real-time hour, no active kills: ticks/hour = `3600/15 + 3600/8 + 3600/25 + 3600/15 + 3600/40 =
240+450+144+240+90 = 1164` → `rmp_gain = 1164 × 1 = 1164` RMP/hour from passive work alone.

### Formula 2 — Mastery Level Determination

```
mastery_level = 3 (optimized)             if region_mastery_points ≥ rmp_threshold_3 AND CCS = 1.0
              = 2 (fully_mastered)        else if region_mastery_points ≥ rmp_threshold_2
              = 1 (partially_mastered)    else if region_mastery_points ≥ rmp_threshold_1
              = 0 (newly_conquered)       otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rmp_threshold_1` | float, tuning knob | 300–800 (default 500) | RMP required for Partially Mastered. |
| `rmp_threshold_2` | float, tuning knob | 1200–3000 (default 2000) | RMP required for Fully Mastered. |
| `rmp_threshold_3` | float, tuning knob | 3000–8000 (default 5000) | RMP required to *attempt* Optimized. |
| `CCS` | float | `{0, 0.25, 0.5, 0.75, 1.0}` | Formula 3's Composition Completeness Score, evaluated at the moment of this check (A2). |
| `mastery_level` | int | `{0, 1, 2, 3}` | Recomputed every time RMP updates. Ratchet — once a level is reached it is never demoted by a subsequent recomputation producing a lower value (A1). |

**Output range**: a 4-value ordinal, monotonically non-decreasing over the life of the region.
**Worked example**: continuing Formula 1's worked example (1164 RMP/hour from a complete team),
Partially Mastered (`500`) is reached in `500/1164 ≈ 0.43` hours (≈26 minutes); Fully Mastered
(`2000`) at `≈1.72` hours; the Optimized *attempt* threshold (`5000`) at `≈4.30` hours — and succeeds
immediately at that check only because `CCS = 1.0` (this worked team already fills all four chain
roles, §3.3). A team missing its Defender (`CCS = 0.75`) would reach `5000` RMP on the same
schedule but remain capped at Fully Mastered until a Defender is assigned.

### Formula 3 — Team Quality Score (Composition Completeness + Power Quality)

```
CCS = (count of the 4 chain roles — generator[Attacker∨Producer], Crafter, Defender, Support —
       present anywhere in the assigned team) / 4

PQS = clamp(mean(power_tier of assigned team members) / power_tier_reference_ceiling, 0, 1)

team_quality_score = clamp(ccs_weight × CCS + pqs_weight × PQS, 0, 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `CCS` | float | `{0, 0.25, 0.5, 0.75, 1.0}` | Composition Completeness Score — a structural, not momentary, signal (A3). |
| `PQS` | float | `[0, 1]` | Power Quality Score — average assigned `power_tier` normalized against a reference ceiling. |
| `power_tier_reference_ceiling` | int, tuning knob | 10–20 (default 20, matching `creature-data-schema`'s own `power_tier` ceiling) | Normalization constant. |
| `ccs_weight` | float, tuning knob | 0.55–0.75 (default 0.65) | Deliberately the larger weight — composition dominates raw power (the composition-puzzle requirement, A2). |
| `pqs_weight` | float, tuning knob | 0.25–0.45 (default 0.35) | `1 − ccs_weight`, kept as its own explicit knob rather than derived, for authoring clarity. |
| `team_quality_score` | float | `[0, 1]` | Feeds Formula 4's within-tier continuous component. |

**Output range**: `[0, 1]` by construction. **Worked example**: a complete 6-role team, average
`power_tier = 5`: `CCS = 4/4 = 1.0`, `PQS = 5/20 = 0.25`,
`team_quality_score = 0.65×1.0 + 0.35×0.25 = 0.65 + 0.0875 = 0.7375`. A maxed-out complete team
(`power_tier = 20` average): `PQS = 1.0`, `team_quality_score = 0.65 + 0.35 = 1.0` (the theoretical
ceiling). A team of six Producers (`CCS = 0.25`, same `power_tier = 5`, `PQS = 0.25`):
`team_quality_score = 0.65×0.25 + 0.35×0.25 = 0.25` — less than half the complete team's score
despite identical average power, the direct proof of A2/§3.3's composition-over-stacking claim.

### Formula 4 — Idle Efficiency Percent (THE CENTRAL FORMULA — must hit the locked bands)

```
idle_efficiency_percent = band_min(mastery_level)
                         + (band_max(mastery_level) − band_min(mastery_level)) × team_quality_score
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `mastery_level` | int, external input | `{0, 1, 2, 3}` | Formula 2's output. |
| `band_min(mastery_level)` | int, **LOCKED, non-negotiable** | `{25, 50, 80, 100}` | The locked contract's lower bound per tier. |
| `band_max(mastery_level)` | int, **LOCKED, non-negotiable** | `{40, 70, 100, 120}` | The locked contract's upper bound per tier. |
| `team_quality_score` | float, external input | `[0, 1]` | Formula 3's output. |
| `idle_efficiency_percent` | float | `[25, 120]` overall; exactly `[band_min, band_max]` within any single `mastery_level` | Output. Applied as the region-wide multiplier in Formula 5. |

**Output range**: by construction, `idle_efficiency_percent` can never fall outside its current
tier's locked band regardless of `team_quality_score`'s value — the four bands are honored exactly,
every time, for every input.

**Worked examples** (reproducing every locked band exactly):

| Team | `mastery_level` | `CCS` | `PQS` | `team_quality_score` | `idle_efficiency_percent` | Band |
|---|---|---|---|---|---|---|
| Minimal (Attacker only, `power_tier=1`) | Newly Conquered | 0.25 | 0.05 | 0.18 | `25 + 15×0.18 = 27.7%` | 25–40 ✓ |
| Complete, `power_tier=5` avg | Newly Conquered | 1.0 | 0.25 | 0.7375 | `25 + 15×0.7375 = 36.1%` | 25–40 ✓ |
| Complete, `power_tier=5` avg | Partially Mastered | 1.0 | 0.25 | 0.7375 | `50 + 20×0.7375 = 64.75%` | 50–70 ✓ |
| Complete, `power_tier=5` avg | Fully Mastered | 1.0 | 0.25 | 0.7375 | `80 + 20×0.7375 = 94.75%` | 80–100 ✓ |
| Complete, `power_tier=5` avg | Optimized | 1.0 | 0.25 | 0.7375 | `100 + 20×0.7375 = 114.75%` | 100–120 ✓ |
| Complete, `power_tier=20` avg (max) | Optimized | 1.0 | 1.0 | 1.0 | `100 + 20×1.0 = 120%` | Ceiling, exact ✓ |

The same team's `team_quality_score` (`0.7375`) climbs from 36.1% to 114.75% purely by the region's
own mastery level advancing — proving mastery level, not team optimization, is the dominant lever
(a deliberate design choice: leveling up a region's mastery is meant to feel like a real power
spike, not a marginal nudge), while team quality still meaningfully moves the needle within any
single tier (up to the full band width).

**Reconciliation with the brief's narrative targets** ("automation reaches ~50–60% of an expert
active player's output, or ~70–80% of a casual active player's output" — flavor context, not a
separately-required formula per the task's own structure): at Optimized/complete-team defaults
(`~115–120%` idle) against `combat-encounter-system`'s own "Focused active play" band (`180–220%`,
its Formula 7), the ratio is `115/200 ≈ 57.5%` — inside the stated 50–60% band against a
moderately-skilled ("focused," not "expert-peak") active player, and against its "Light
interaction" band (`~128%`), the ratio is `115/128 ≈ 90%` — closer to parity than the 70–80% target
suggests, since "light interaction" is a lower activity bar than "casual" implies. This is flagged
as directionally consistent, not force-fit — full reconciliation depends on three independently-
tuned documents' placeholder values and is a named Vertical Slice playtesting target (§7).

### Formula 5 — Region Production Rate

```
region_output_per_tick[resource] = round(
    Σ over {c ∈ assigned_team : role(c) emits resource} of role_output_per_tick(c)
    × (idle_efficiency_percent / 100)
)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `role_output_per_tick(c)` | number, external input | ≥ 0 | `creature-jobs-evolution-system` Formula 5, reused directly, unmodified — already zero for a Blocked/Starved creature. |
| `idle_efficiency_percent` | float, external input | `[25, 120]` | Formula 4's output — applied as a single region-wide multiplier on top of the team's raw per-creature potential, representing the region's own infrastructure/mastery amplifying whatever the team can produce. |
| `region_output_per_tick[resource]` | number | ≥ 0, unbounded above (subject to `region_storage_capacity`'s own cap on *accumulation*, §3.4 — not on this formula's output) | Added to the region's stored quantity for `resource` this tick, subject to Formula 6's capacity check the *next* tick. |

**Output range**: ≥ 0, unbounded in a single tick's computation (bounded in accumulation only by
`region_storage_capacity`). **Worked example**: two Healthy Producers assigned
(`role_output_per_tick = 12` each per `creature-jobs-evolution-system`'s own Formula 5 worked
example), region at Fully Mastered with `idle_efficiency_percent = 94.75`:
`region_output_per_tick[raw_resource] = round((12+12) × 0.9475) = round(22.74) = 23` units this
tick.

### Formula 6 — Automated Clear Time (DERIVED — an output, never an anchor)

> **⚠ REWRITTEN 2026-07-14 to close Blocker B1. Read the direction-of-causation note before editing.**
>
> This formula's *output* used to be fed into `combat-encounter-system` Formula 7 as the reference the
> **active player was scored against**. That closed a feedback loop: the better the player's automation
> got, the smaller their active-efficiency score became — holding skill perfectly constant, the same
> recorded play scored 166% → 92.5% → 63.2% → 50% as mastery rose. **Pillar 3 inverted.**
>
> Causation now runs one way only: **par is the fixed anchor; automated clear time is derived from
> it.** This formula consumes `par_clear_time_seconds`; it does not produce anything anyone is scored
> against. It is a *reporting* formula — what the player's farm is actually achieving — not a
> *reference* formula.

```
automation_clear_time_seconds = par_clear_time_seconds / max(epsilon, idle_efficiency_percent / 100)
```

If no Healthy Attacker is assigned to the region at all (distinct from a low-quality team — this is
the structural absence of the one Role that triggers kills at all, `creature-jobs-evolution-system`
§3.6: "without a Healthy Attacker, an automated region produces nothing"), this document instead
reports the sentinel `automation_clear_time_ceiling_seconds` (§7, default 3600s) — a large, finite
value representing "automation is not clearing anything," never zero or undefined.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `par_clear_time_seconds` | float, **external input — fixed authored constant** | > 0 | Owned by `encounter-spawn-system`, per encounter template. *The clear time of a competent, fully-mastered automated team.* **Never varies with the player's live mastery, team, or automation stage.** This is the anchor; this document consumes it, and must never be made to produce it. |
| `idle_efficiency_percent` | float | `[25, 120]` | Formula 4's output — the same number driving Formula 5, reused here rather than introducing a third independent scalar (A4). Now interpreted as **a percentage of par**. |
| `epsilon` | float, constant | `0.01` | Pure divide-by-zero guard; never actually binding since `idle_efficiency_percent` floors at 25. |
| `automation_clear_time_seconds` | float | `(0, automation_clear_time_ceiling_seconds]` | **Output only.** Consumed for display (`automation-config-ui`, `region-view-world-map-ui`) and for simulating farm throughput. **Consumed by no formula that scores the player.** |

**The old numbers survive the rewrite exactly.** The previous version divided the Attacker Role's
authored `work_tick_interval_seconds` (15s) by idle efficiency. Note what that implies: at
`idle_efficiency_percent = 100` — the definition of *a competent, fully-mastered team* — it returned
**15s**. That is precisely what `par_clear_time_seconds` means. The old formula was already computing
par; it just had no name for it, and so leaked a *mastery-dependent* number into a slot that needed a
*mastery-independent* one. Substituting `par` for the hardcoded 15 both generalizes the formula (each
encounter template now carries its own par instead of every encounter sharing one global cadence) and
reproduces every previously-published number:

| Region state | `idle_efficiency_percent` | `automation_clear_time_seconds` (par = 15s) | Previously published |
|---|---|---|---|
| Newly conquered (band midpoint) | 33.3% | `15 / 0.333` ≈ **45.05 s** | 45.05 s ✓ |
| Partially mastered | 60% | `15 / 0.60` = **25.0 s** | 25 s ✓ |
| Fully mastered | 94.75% | `15 / 0.9475` ≈ **15.8 s** | ~16 s ✓ |
| Optimized team | 120% | `15 / 1.20` = **12.5 s** | 12.5 s ✓ |

Automation still clears ~3.6× faster at an Optimized region than at a freshly-conquered one — the
felt "automation gets meaningfully better" progression the mastery ladder promises is completely
preserved. **What changed is not how good automation gets. It is that automation getting better no
longer silently reduces the active player's score.**

### Formula 7 — Stage-3 Gleam-Realization Cap (THE FAUCET-CAP FIX, with numeric proof)

**The problem, restated from `loot-drop-system` Formula 8**: an unattended 100-hour session at
Automation Stage 2 generates ≈760,000 Gleam-equivalent of item value; if a future Stage-3 auto-sell
converted that wholesale, it would afford ≈3,800 Vow-bindings (`vow_binding_cost = 200`) against a
realistic lifetime cadence of roughly a dozen — a two-to-three order of magnitude blowout that
document explicitly declined to fix, naming this document as the owner of the fix.

**The fix**:

```
gleam_realized_this_hour = min(
    pending_auto_sell_backlog_value,
    region_auto_sell_gleam_cap_per_hour
)

gleam_realized_over_window(hours) = min(
    total_pending_auto_sell_backlog_value,
    region_auto_sell_gleam_cap_per_hour × hours
)
```

Applied **globally, across every Stage-3-capable region on the account simultaneously** (A8) — not
per region. Any backlog value beyond the cap in a given hour simply remains queued, unsold,
carried forward to the next hour's cap, or realized instantly and uncapped the moment the player
visits the Forge manually (`the-forge-system`'s Sell operation, unthrottled — this cap applies only
to *automation-invoked* Sell actions).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `pending_auto_sell_backlog_value` | float, computed | ≥ 0 | Sum of `the-forge-system` Formula 6 `sell_value` across every item currently tagged `auto_sell` and not yet sold, account-wide. |
| `region_auto_sell_gleam_cap_per_hour` | float, tuning knob, **BALANCE-CRITICAL, reverse-engineered — see proof below** | 12–48 (default **24**) | The single hardest-working number in this document's economic design. |
| `gleam_realized_over_window(hours)` | float | `[0, min(backlog, cap×hours)]` | Total Gleam actually created by Stage-3 automation over any window. |

**Output range**: hard-bounded — `gleam_realized_over_window` can never exceed
`region_auto_sell_gleam_cap_per_hour × hours`, for any backlog size, any number of regions, any
combination of items. This is the proof the cap is real and cannot be defeated by scale.

**The numeric proof (100-hour unattended session, directly answering the brief)**:

```
max_gleam_realized_100hr = 24 Gleam/hour × 100 hours = 2,400 Gleam
max_vow_bindings_affordable = floor(2,400 / 200) = 12
```

`region_auto_sell_gleam_cap_per_hour = 24` was **reverse-engineered specifically to land this exact
result**: `(realistic_cadence_target × vow_binding_cost) / clamp_window_hours = (12 × 200) / 100 =
24`. A 100-hour unattended Stage-3 session — the same worst-case scenario that, uncapped, produced
3,800 affordable Vow-bindings — now produces at most **exactly 12**, matching the realistic lifetime
cadence `loot-drop-system` itself named, not merely reducing the blowout but closing it to within
the stated target. This is a *worst-case ceiling*, not a guaranteed outcome — it assumes the full
100 hours of backlog is Stage-3-eligible (sub-Epic, since Epic/Legendary can never be auto-sold at
all, `loot-drop-system` §3.9.3's hard safety net, an independent second brake on top of this one)
and that the player never once visits the Forge manually during that window (each manual visit
instantly drains the backlog uncapped, resetting the following hour's cap to a fresh, mostly-empty
queue in practice).

**Why this satisfies Pillar 3 without violating it**: nothing is destroyed, locked, or made
unavailable — every item is still minted, still exists, still eventually sellable. Only the *rate*
at which unattended automation converts that backlog into spendable currency is bounded, and it is
bounded to land within the same order of magnitude as legitimate long-term demand, not below it.
This is the honest answer to the brief's explicit demand: the faucet stays real and idle-legible,
the sink's realization rate is what was actually unbounded, and it no longer is.

### Formula 8 — Offline Progression Conversion (Stage 4)

A closed-form, time-to-storage-saturation approximation (A9) — not a full tick-by-tick replay:

```
elapsed_ms_clamped = min(ElapsedMs(region), offline_elapsed_upper_clamp_hours × 3,600,000)

time_to_fill_storage_seconds(resource) =
    (region_storage_capacity(resource) − current_stored_amount(resource)) / production_rate_per_second(resource)
    [= +∞ if production_rate_per_second(resource) ≤ 0]

productive_seconds(resource) = min(elapsed_ms_clamped / 1000, time_to_fill_storage_seconds(resource))

offline_output(resource) = production_rate_per_second(resource) × productive_seconds(resource)
```

Chained resources (a Crafter consuming a generator's output) are resolved generator-first: the
generator's `offline_output` is computed, then used as the Crafter's available input ceiling for its
own `productive_seconds` calculation, mirroring `creature-jobs-evolution-system` §3.6's supply-chain
dependency without simulating it tick-by-tick. Any accrued `healthy_work_ticks`/`evolution_progress`
from the productive portion of the window is handed to that document's own Formula 2/4 as a single
aggregate addition, evaluated once at the end of the catch-up window (not continuously) — the same
tick-boundary-only rule that document's own §5 Edge Case 3 already requires, just applied across one
very large boundary instead of many small ones (§5 Edge Case 3 of this document).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `ElapsedMs(region)` | int64, external input | `[0, +∞)` | `save-load-persistence` Formula 3, consumed by exact name, unmodified. |
| `offline_elapsed_upper_clamp_hours` | float, tuning knob | 24–168 (default **72**) | The field `save-load-persistence` explicitly deferred to this document (A10). |
| `region_storage_capacity(resource)` | float, tuning knob | per §3.4/§7 | Same value the live Blocked-state check uses — no separate offline-only capacity exists. |
| `production_rate_per_second(resource)` | float, computed | ≥ 0 | Formula 5's `region_output_per_tick[resource]` converted to a per-second rate using that Role's `work_tick_interval_seconds`, at the region's `idle_efficiency_percent` and team composition **as they stood at the moment the region was last ticked** (not re-evaluated mid-window — a second, smaller approximation, flagged alongside A9). |
| `offline_output(resource)` | float | `[0, region_storage_capacity(resource) − current_stored_amount(resource)]` | Bounded above by remaining capacity, by construction. |

**Output range**: `offline_output` can never exceed the resource's remaining storage headroom at the
moment catch-up begins — the formula is self-limiting by construction, satisfying the "20 minutes
of production, not 8 hours" requirement directly. **Worked example (the brief's own named
scenario)**: a Crafter's refined-goods store is at `100/500` capacity (`400` headroom),
`production_rate_per_second = 0.333` units/sec (tuned so the store would fill in exactly 1,200
seconds — 20 minutes); the player returns after an 8-hour (`28,800s`) offline stretch, well under
the 72-hour clamp so `elapsed_ms_clamped` is unaffected:

```
time_to_fill_storage_seconds = 400 / 0.333 = 1,200s (20 minutes)
productive_seconds = min(28,800, 1,200) = 1,200
offline_output = 0.333 × 1,200 = 400 units (store now exactly full, at capacity)
```

The remaining `27,600` seconds of the offline window produce **zero** additional output — the
Crafter would have Blocked at the 20-minute mark had the player been watching, and offline
progression honors that exactly, rather than granting a full 8 hours of production.

## 5. Edge Cases

1. **A region's entire assigned team is Starved and/or Blocked simultaneously — for how long, and
   what happens?** No timer, no decay, no destructive consequence, indefinitely — directly inherited
   from `creature-jobs-evolution-system` A7/Edge Case 4 and Edge Case 9 (this document introduces no
   new penalty on top of that ruling). `region_output_per_tick` is `0` for every resource every tick
   this holds (Formula 5, since every summed `role_output_per_tick(c)` term is already `0`), RMP
   accrual (Formula 1) also stops (zero Healthy members), but `region_mastery_points` and
   `mastery_level` never decrease — a region that stalls for a week and is then fixed resumes exactly
   where it left off, with no catch-up granted for the stalled time (matching that document's own
   "no catch-up for missed time" precedent, extended here).
2. **The player returns after 30 days offline.** `ElapsedMs(region)` reports the full 30-day delta
   (`save-load-persistence` Formula 3 is deliberately not upper-clamped at its own layer), but this
   document's Formula 8 clamps it to `offline_elapsed_upper_clamp_hours` (default 72) before
   computing catch-up — the 30-day and the 72-hour case produce **identical** offline output. No
   additional benefit, but also no penalty — the player simply receives the same bounded catch-up
   either way (A10). `last_ticked_at` is still reset to the current timestamp regardless of how much
   of the delta was actually "spent," so a subsequent short session isn't double-counted against the
   unspent remainder.
3. **A creature evolves mid-tick, specifically during an offline catch-up window (Formula 8).**
   Because offline catch-up is a single aggregate boundary rather than a tick-by-tick replay (A9),
   this document evaluates `creature-jobs-evolution-system` Formula 4 (evolution eligibility) **once,
   at the end of the catch-up window**, using the total `healthy_work_ticks`/`evolution_progress`
   accrued across the *entire* productive portion of that window as the input — never mid-window. If
   a Role-changing evolution would have fired partway through a real-time replay, this
   approximation instead applies it at the window's end, meaning a small amount of the tail-end
   productive time is technically computed against the *old* Role's output profile rather than the
   *new* one — an accepted, explicitly-flagged imprecision (A9) in exchange for O(1)-per-resource
   performance, never a correctness violation (no resource is double-counted or lost, only
   attributed to the pre- vs. post-evolution Role with coarser-than-live granularity). During
   **live, in-session** ticking (not offline catch-up), evolution is evaluated at every tick boundary
   exactly as `creature-jobs-evolution-system` §5 Edge Case 3 specifies — this approximation applies
   only to the offline-catch-up path.
4. **A region is re-entered for active/manual combat while its automated team is also running.**
   Fully supported and expected — active and automated production are concurrent, independent
   sources of `CreatureDefeated` events against the same region (§3.2's RMP active-kill bonus is
   built assuming exactly this happens). This is a direct, literal instance of `loot-drop-system`'s
   own Player Fantasy framing: active and idle are "permanently co-existing modes, not a phase and
   its graduation." Automation does not pause, throttle, or otherwise react to the player's presence
   in any way — the two systems simply run side by side.
5. **The player un-assigns the entire team from a mastered region.** `region_output_per_tick` is `0`
   for every resource (Formula 5's sum is empty). `region_mastery_points`/`mastery_level` remain
   exactly as they were (the ratchet, A1) — an Optimized region with no team is still "Optimized" as
   an achievement record, but its *live* `idle_efficiency_percent` still computes normally off
   `team_quality_score = 0` (Formula 3, both `CCS` and `PQS` are `0` with no team), landing exactly
   at that tier's `band_min` (§4 Formula 4) — which produces nothing regardless, since there is no
   team to apply that percentage to. **Separately**, the absence of any assigned Attacker (Healthy or
   not) independently forces `automation_clear_time_seconds` to its sentinel ceiling
   (§4 Formula 6), regardless of what `idle_efficiency_percent` itself computes to — the two effects
   are related but not the same mechanism.
6. **A resource's `region_storage_capacity` is reached and every Role emitting it Blocks.**
   Exactly `creature-jobs-evolution-system` §3.5's Blocked state, using this document's concrete
   number (§3.4, A7) as the `output_capacity_available` input. No production of that resource
   resumes until the player collects/processes the backlog (freeing capacity) via
   `automation-config-ui` or a manual Forge visit — indefinite, non-destructive, identical in kind
   to Edge Case 1. This is the deliberate "storage caps force player return" brake named in the
   brief, now with real numbers behind it.
7. **A region's boss is fought and defeated a second time** (after the region is already
   `mastered` — e.g. a repeat active visit, or a boss respawn mechanic if one is ever added).
   **Ruling**: the Mastery Transition sequence (§3.7) checks `region_state = unconquered`
   *immediately prior* to the kill as a precondition — a boss kill against an already-`mastered`
   region is treated as an ordinary Kill outcome (loot, creature core, and this document's own
   active-kill RMP bonus per Formula 1 all still apply normally) but never re-fires the Mastery
   Transition's state changes. The region cannot be "re-conquered," "de-mastered," or reset by any
   subsequent boss encounter.
8. **The account holds more than one Stage-3-capable region, and both generate `auto_sell`-tagged
   backlog in the same real-time hour.** Formula 7's cap is explicitly global (A8) — both regions'
   pending backlog values are summed into a single `total_pending_auto_sell_backlog_value`, and the
   single shared `region_auto_sell_gleam_cap_per_hour` applies across that combined total, not once
   per region. Whichever items would be realized first is an implementation-level FIFO/priority
   detail this document does not further specify (not balance-relevant, since the *aggregate* ceiling
   is what the numeric proof depends on, not the ordering within it).
9. **First launch, or a region with no persisted state yet (`save-load-persistence` Edge Case 9's
   "no save file present" case, applied per-region).** A region defaults to `region_state =
   unconquered`, `mastery_level` undefined/inapplicable (only meaningful once `mastered`),
   `region_mastery_points = 0`, no team assigned, no `last_ticked_at` value — this document does not
   error or require special-case handling; the first meaningful state write for any region is its
   own Mastery Transition (§3.7).
10. **A player reassigns a complete, Optimized-qualifying team away and replaces it with an
    incomplete one, then later reassigns a complete team again.** `mastery_level` never dropped in
    between (Edge Case 5's ratchet) — the region is still `optimized`, and `idle_efficiency_percent`
    simply tracks whatever `team_quality_score` the *current* team produces at every recomputation,
    rising and falling freely within the Optimized band (`[100, 120]`) as team composition changes,
    with no re-gating required to move back up within that same band (the RMP/CCS gate in Formula 2
    is only evaluated for *advancing* `mastery_level`, never for staying within a level already
    reached).

## 6. Dependencies

### Depends On

- **`combat-encounter-system.md`** — consumes `active_efficiency_percent` (its Formula 7) as the
  active-kill signal feeding this document's own RMP bonus (§4 Formula 1) and as the comparison
  target the brief's ~2x/3x narrative is checked against (§4 Formula 4's reconciliation note); reads
  the `CreatureDefeated` event and `RESOLVING`/`COMPLETE` state transitions as the Mastery
  Transition's own trigger (§3.7). This document **supplies nothing into that document's Formula 7** —
  it once did, and that was Blocker B1. Both systems now read the *same* fixed
  `par_clear_time_seconds` from `encounter-spawn-system`, which is what makes their two efficiency
  bands directly comparable instead of circularly defined.
  this document is the arrival.
- **`creature-jobs-evolution-system.md`** — reads `role_output_per_tick` (its Formula 5, reused
  directly and unmodified in this document's own Formula 5) and `healthy_work_ticks` accrual (its
  Formula 2, reused as this document's own RMP passive-accrual signal, §4 Formula 1) — never
  recomputes either. In exchange, this document **supplies** the two externally-owned booleans that
  document's §3.5/Formula 6 explicitly deferred: `input_resource_available` and
  `output_capacity_available` (§3.4), plus the Region-Mastered precondition its §3.2 step 3 checks
  against, and the composition-validity re-check its §5 Edge Case 3 expects after any Role-changing
  evolution (satisfied here by Formula 3's `CCS` being recomputed live on every check, not cached).
- **`save-load-persistence.md`** — consumes `last_ticked_at` (per-region) and Formula 3's
  `ElapsedMs`/`elapsed_time_delta` mechanism directly, unmodified, exactly as that document's own
  Depended-On-By section anticipated ("critically depends on this GDD's elapsed-time mechanism...
  without a future save-format migration"). This document is also the explicit owner of
  `offline_elapsed_upper_clamp_hours` (§4 Formula 8, A10), the one field that document's own Tuning
  Knobs table listed only to flag as this document's to define.
- **`loot-drop-system.md`** — every automated kill's loot originates from that document's drop
  resolution (§3.1 there, triggered by this document's Stage-1 Attacker automation); reads its
  Formula 6/Formula 8 (`sell_value`, faucet-vs-sink analysis) as the direct basis for this document's
  own Formula 7 fix. In exchange, this document **supplies** `automation_stage` (redefined here as
  `mastery_level_index + 1`, A6) as that system's Formula 2 input, and is the explicit owner that
  document's own A3/A4/Edge Case 6 all named as the future authority on automated-vs-active kill
  distinction, and the "auto-craft/sell" stage design that document's Formula 8 explicitly flagged
  as needing to account for its Gleam-realization finding "before it ships" — this document is that
  accounting (§4 Formula 7).
- **`animation-rig-system.md`** — selects and holds/releases the Healthy/Blocked/Starved clip
  variant for each bound creature's work loop, reading `bound_state.work_state`, and sets
  `is_held`/`hold_frame` (§3.11 there) to implement the Blocked state. This document owns *when* a
  creature transitions between work states; that rig owns the mechanism (that document's own §6).
- **`encounter-spawn-system.md`** — supplies `par_clear_time_seconds` per encounter template (§3.5
  there), the fixed anchor this document's own Formula 6 reads by exact name to derive
  `automation_clear_time_seconds`, replacing the purged, circular `automation_baseline_clear_time_
  seconds` (the B1 fix). This document never produces that anchor, only consumes it — see this
  document's own §4 Formula 6 note ("consumed, not produced").

### Depended On By

- **`automation-config-ui`** — the presentation layer for team assignment
  (§3.3), mastery-level/RMP progress display (§3.2), Automation Capability Stage status (§3.5), and
  the storage-capacity collection action (§3.4, Edge Case 6). That system's own GDD must reference
  this document's exact field names (`mastery_level`, `region_mastery_points`, `CCS`, `team_quality_
  score`) when authored.
- **`region-view-world-map-ui`** — the wide-diorama region-overview presentation
  (art-bible §6.1's "Region View" composition) surfacing `region_state`/`mastery_level`'s cool→warm
  ambient-color transition (art-bible §4.4) and standing automation infrastructure (art-bible §6.7);
  reads this document's state directly, defines no new mechanics of its own.
- **`memory-dust-prestige-system`** (Full Vision) — per `systems-index.md`'s own
  dependency listing, expected to read a region's final `mastery_level`/RMP as part of whatever
  progression it carries across a prestige reset; no further contract is specified here since that
  system does not exist yet.
- **`hunter-progression-system`** — reuses `region_auto_sell_gleam_cap_per_hour` (24, this
  document's own Formula 7) directly as the pessimistic floor in that document's own §4.6 Scenario B
  (that document's own §6).
- **`rare-creature-capture-system`** (Vertical Slice) — consumes the "this encounter is automated"
  flag as an external input to its own Formula 4, substituting `automated_capture_favorability_
  baseline` when the flag is set (that document's own §6, A8). This document does **not** supply the
  spawn-time rarity-roll trigger for that system's Formula 1 — `encounter-spawn-system` owns that
  trigger (see that document's own corrected §6 for the full explanation).
- **`audio-system`** — consumes this document's discrete state-change events (the
  Mastery Transition firing, a mastery-level advancement, a Blocked/Starved-driven production halt)
  as audio cue triggers, matching art-bible §2's locked mood description for the Idle/Automated
  Region View and the Mastery Transition specifically; this document defines the events' timing,
  that system owns the sound assets.

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** §2/§4.4/§6 — the Mastery Transition's locked emotional beat and
  glyph-inversion visual (§3.7 here implements only the mechanical trigger it rides on), the
  Unconquered/Mastered ambient-color Temperature Law, and "automation becomes standing
  infrastructure" (§6.7) as the visual language for a `mastered` region's Region View.
- **`design/gdd/game-concept.md`** — Pillar 2 and Pillar 3 (this document's entire reason for
  existing, §1–2), the MVP Definition's Stage 1–2 gate (§3.5, honored exactly), and the Key Technical
  Challenges line naming offline-progression simulation performance as a named risk (§4 Formula 8,
  A9 directly addresses it).

## 7. Tuning Knobs

**The idle efficiency curve (Formula 4's four bands and Formula 3's weighting) is THE
single most balance-critical set of values in this entire project** — `systems-index.md`'s own
High-Risk Systems table already flags this exact curve as a delicate balance risk. Every other
system's active/idle relationship is measured against it.

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| **Efficiency band bounds (×4 tiers)** | `band_min`/`band_max` per `mastery_level` (Formula 4) | **LOCKED — not a free knob** | `[25,40]/[50,70]/[80,100]/[100,120]` | The user's own exact contract. Changing these requires an explicit pillar-level design review, not a routine retune — flagged identically to `combat-encounter-system`'s own treatment of `active_efficiency_cap_percent`. |
| `rmp_threshold_1/2/3` | Formula 2 | 1 (300–800) / 2 (1200–3000) / 3 (3000–8000) | 500 / 2000 / 5000 | Primary pacing lever for how long a region takes to climb the mastery ladder — the single most player-visible knob after the bands themselves. |
| `rmp_per_healthy_member_tick` | Formula 1 | 0.5–2.0 | 1 | Direct multiplier on how fast passive team work converts to mastery progress. |
| `rmp_per_active_kill_bonus` | Formula 1 | 2–10 | 5 | How strongly returning to actively hunt a mastered region accelerates its own mastery ladder. |
| **`ccs_weight`** | Formula 3, **BALANCE-CRITICAL** | 0.55–0.75 | 0.65 | Controls how dominant composition is over raw power in the within-tier efficiency bonus — the direct lever on the composition-puzzle's felt strength (A2). |
| `pqs_weight` | Formula 3 | `1 − ccs_weight` | 0.35 | Companion to the above. |
| `power_tier_reference_ceiling` | Formula 3 | 10–20 | 20 | Normalization ceiling for average team power tier. |
| `region_team_slot_count` | §3.3 | 5–8 | 6 | How many creatures a region's team can hold — 5 covers exactly one of each Role; the 6th is a deliberate flex slot for doubling a generator. |
| `region_storage_capacity` (per resource type) | §3.4, §4 Formula 8 | 200–1000 | 500 | The concrete "storage caps force player return" number (A7) — too low makes automation feel like it constantly needs babysitting; too high erodes the brake entirely. |
| **`region_auto_sell_gleam_cap_per_hour`** | Formula 7, **BALANCE-CRITICAL, reverse-engineered against a specific target — see proof** | 12–48 | **24** | The Stage-3 inflation-spiral fix. Must never be raised without re-running Formula 7's 100-hour proof against the current `vow_binding_cost` and realistic-cadence target. |
| `offline_elapsed_upper_clamp_hours` | Formula 8, A10 | 24–168 | 72 | Ceiling on how much real-world absence offline progression will ever credit — also bounds worst-case simulation cost. |
| `attacker_work_tick_interval_seconds` | Formula 6 — **owned by `creature-jobs-evolution-system`, read-only here** | (that document's own range) | 15 | Reused, never independently retuned by this document (A4). |
| `automation_clear_time_ceiling_seconds` | Formula 6 sentinel | 1800–7200 | 3600 | The "no Attacker assigned" fallback — represents "automation is not clearing anything." A large finite value, never zero or undefined. Display-only; feeds no scoring formula. |
| `automation_rarity_parity_percent` (Fully Mastered / Optimized) | §3.5 A6, extends `loot-drop-system` Formula 2 | monotonically non-decreasing across tiers | 75% / 100% | This document's own authored continuation of that system's Stage 1 (0%) / Stage 2 (50%) table. |
| Automation Capability Stage gates (×4) | §3.5 | must be monotonically non-decreasing in `mastery_level` | `0 / 0 / 2 / 3` (mastery-level index) | Stage 3/4's gates are this document's primary structural defense against the inflation spiral — must never be lowered without re-validating Formula 7's proof. |

### BALANCE-CRITICAL Flag Summary

In priority order, matching the convention `combat-encounter-system` and `loot-drop-system` both
already established: `band_min`/`band_max` (the locked contract itself, never retuned without a
pillar-level review), `region_auto_sell_gleam_cap_per_hour` (re-derive Formula 7's proof on any
change), `ccs_weight` (the composition-puzzle's felt strength), `rmp_threshold_1/2/3` (mastery-ladder
pacing). Any retuning of these must be re-checked against §4 Formula 4's full worked-example table
(every band must still be reachable and never overshot) and Formula 7's 100-hour proof (the
Vow-binding ceiling must stay within the same order of magnitude as realistic cadence).

## 8. Acceptance Criteria

1. **Idle efficiency band test (required by the brief)**: for every `mastery_level` value (0–3) and
   every `team_quality_score` in `[0, 1]`, `idle_efficiency_percent` (Formula 4) always falls within
   that level's exact locked band — `[25,40]`, `[50,70]`, `[80,100]`, `[100,120]` respectively —
   verified by sweeping both inputs to their full range and asserting no output ever falls outside
   its tier's band.
2. **The active/idle contract holds, and both figures are anchored to the same fixed par (Blocker B1).**
   *This criterion was rewritten on 2026-07-14. Its previous form was **unpassable** — it compared an
   active number against an idle number drawn from a **different mastery level**, an artifact of the
   moving anchor, and the comparison it asserted would have required a 4.2-second boss clear (three
   basic-attack ticks at `basic_attack_interval_ms = 1200`) to satisfy. A criterion that can only pass
   by accident is worse than no criterion.*

   Both `active_efficiency_percent` (`combat-encounter-system` Formula 7) and this document's
   `idle_efficiency_percent` (Formula 4) are now percentages **of the same fixed
   `par_clear_time_seconds`**, so they are directly comparable for the first time. Assert all three:

   a. **Anchor immobility.** For a fixed encounter template, `par_clear_time_seconds` is byte-identical
      across all four `mastery_level` values and every `team_quality_score`. Sweep both inputs; assert
      the anchor never moves. *(If this fails, everything below is meaningless.)*

   b. **Active is always better.** For every `mastery_level` (0–3) and every `team_quality_score` in
      `[0,1]`, the weakest *engaged* active play (light interaction, ≈128.1% — `combat-encounter-system`
      Formula 7's own worked example) strictly exceeds that state's `idle_efficiency_percent`. The
      binding case is the global idle ceiling: **128.1% > 120%**. Assert across the full sweep.

   c. **Active is never more than ~3× better.** The hard active ceiling (`active_efficiency_cap_percent`
      = 300, clamped) against the global idle ceiling (120%) is **300/120 = 2.5×** — inside the brief's
      "avoid sustained active play being more than roughly 3× better." Assert `≤ 3.0` for every
      same-region comparison at Fully Mastered or above.

   **Deliberately NOT asserted:** the ratio against a *newly conquered* region (idle floor 25%) reaches
   `300/25 = 12×`. This is **correct and intended** — Pillar 2 ("Automation Is Earned, Not Assumed")
   requires fresh automation to be genuinely poor, and the brief's "~3×" describes the relationship
   against the **reference automation** (par), which is exactly what its own table measures. The ratio
   collapses to 2.5× once mastery is actually earned. Do not "fix" this by raising the idle floor
   without a pillar-level decision — it would make automation free.
3. **Idle play yields every essential progression material, provably (Pillar 3, required by the
   brief)**: a Healthy automated Attacker (Stage 1) triggers `CreatureDefeated` events identically to
   an active kill; `loot-drop-system` Formula 4 guarantees a `creature_core` on **every** Kill,
   active or automated, at 100% (its §3.7 rule 1, non-tunable); that document's Formula 2 guarantees
   strictly nonzero probability at **every** rarity tier for **every** input combination including
   `automation_stage = 1` (this document's own worst-case idle-throttling value, A6) — verified by a
   chained integration test asserting a fully-idle region (Stage 1–2 only, no active play, no
   part-break performance) still produces a nonzero-probability distribution across all 5 rarity
   tiers and a guaranteed core on every simulated kill, with no code path in either document that can
   zero out a tier or skip the core for an automated kill.
4. **Stage-3 auto-sell cannot inflate the economy beyond a modeled ceiling (required by the
   brief)**: simulating a 100-hour continuous Stage-3-eligible unattended session at default tuning
   (`region_auto_sell_gleam_cap_per_hour = 24`) never realizes more than `2,400` Gleam
   account-wide, regardless of how large the underlying item-value backlog grows — verified by a
   statistical simulation feeding an artificially unbounded backlog (e.g. 10× the modeled 760,000
   Gleam-equivalent from `loot-drop-system` Formula 8) into Formula 7 across a simulated 100-hour
   window and asserting realized Gleam never exceeds `2,400` in any run.
5. Formula 1's worked example reproduces exactly: a complete 6-role team, all Healthy for one hour,
   zero active kills → `region_mastery_points += 1164`.
6. Formula 2's worked example reproduces exactly: the same team reaches `mastery_level = 1` at
   `RMP ≥ 500`, `= 2` at `RMP ≥ 2000`, and `= 3` at `RMP ≥ 5000` **only if** `CCS = 1.0` at that
   final check — verified separately with an otherwise-identical team missing its Defender
   (`CCS = 0.75`), asserting it caps at `mastery_level = 2` indefinitely regardless of how far RMP
   continues to climb past `5000`.
7. Formula 3's worked example reproduces exactly: complete team, `power_tier = 5` average →
   `team_quality_score = 0.7375`; six Producers, same average `power_tier` → `team_quality_score =
   0.25` — verified as two independent fixtures with identical total power but different
   compositions, confirming CCS's dominance (A2/§3.3).
8. Formula 6's worked examples reproduce exactly, at `par_clear_time_seconds = 15s`:
   `idle_efficiency_percent ≈ 33.3%` (Newly Conquered band midpoint) → `automation_clear_time_seconds
   ≈ 45.05s`; `60%` → `25.0s`; `94.75%` → `≈15.8s`; `120%` (Optimized) → `12.5s`. Additionally assert
   the **direction of causation**: `automation_clear_time_seconds` is an *output* consumed only by
   display surfaces (`automation-config-ui`, `region-view-world-map-ui`) and throughput simulation —
   grep-assert that **no formula that scores the player reads it.** If it ever reappears as an input to
   `combat-encounter-system` Formula 7, Blocker B1 has regressed and Pillar 3 has re-inverted.
9. Formula 8's worked example reproduces exactly: `400` units of remaining storage headroom, a
   `0.333` units/sec production rate, and an 8-hour offline window produce exactly `400` units of
   offline output (storage exactly filled) and zero further output for the remaining `27,600`
   seconds of that window — verified by asserting the returned `offline_output` value stops
   increasing once `current_stored_amount` reaches `region_storage_capacity`, regardless of how much
   further `elapsed_ms_clamped` extends beyond that point.
10. A 30-day (`2,592,000,000ms`) and a 72-hour (`259,200,000ms`) `ElapsedMs` value, fed through
    Formula 8 for an otherwise identical region/team fixture, produce byte-for-byte identical
    `offline_output` values for every resource — verified directly, confirming the clamp (A10, §5
    Edge Case 2) is applied before any downstream computation, not after.
11. A region's `mastery_level` never decreases across any sequence of simulated ticks, team
    reassignments, or Blocked/Starved states, however extreme — verified with a fixture that
    reaches `mastery_level = 3`, then fully unassigns the team and simulates 10,000 further ticks,
    asserting `mastery_level` still reads `3` at the end.
12. A `CreatureDefeated` event whose `template_id` matches an already-`mastered` region's boss
    template never re-triggers the Mastery Transition state changes (`region_state`, `mastery_level`,
    `region_mastery_points` all remain unchanged by that specific event) — verified by simulating a
    second boss kill post-mastery and asserting no state field the Mastery Transition owns changes as
    a result, while confirming the kill still produces normal loot and an RMP active-kill bonus.

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly, per this task's explicit
file-discipline instruction. The following are proposed for registration by whichever process
coordinates registry writes this session:

**Formulas**:
- `region_mastery_points_accrual` — `design/gdd/region-mastery-automation-system.md` §4 Formula 1.
- `mastery_level_determination` — §4 Formula 2.
- `team_quality_score` — §4 Formula 3. Consumes creature `power_tier` and assigned-Role composition.
- `idle_efficiency_percent` — §4 Formula 4. **Flagged as the single most balance-critical formula in
  the project** — the direct, load-bearing implementation of the locked 25–40/50–70/80–100/100–120%
  contract.
- `region_production_rate` — §4 Formula 5. Reuses `creature-jobs-evolution-system`'s registered
  `role_output_per_tick` (Formula 5 there) directly.
- `automation_clear_time_seconds` — §4 Formula 6. **Output only.** Consumed by display surfaces
  (`automation-config-ui`, `region-view-world-map-ui`) and farm-throughput simulation. Consumed by
  **no** formula that scores the player. It must never be wired back into `combat-encounter-system`
  Formula 7 — see A4 and Blocker B1.
- `par_clear_time_seconds` — **consumed, not produced.** Owned by `encounter-spawn-system` as a field
  on the encounter template.
- `stage3_gleam_realization_cap` — §4 Formula 7. Flagged as the direct resolution to
  `loot-drop-system` Formula 8's flagged forward risk.
- `offline_progression_conversion` — §4 Formula 8. Consumes `save-load-persistence`'s registered
  `elapsed_time_delta` formula directly.

**Constants**:
- Efficiency band bounds per `mastery_level` (`[25,40]/[50,70]/[80,100]/[100,120]`) — §7.
  **LOCKED, the user's own contract, never retuned without a pillar-level review.**
- `rmp_threshold_1/2/3` (`500`/`2000`/`5000`) — §7.
- `ccs_weight` (`0.65`), `pqs_weight` (`0.35`) — §7. **BALANCE-CRITICAL.**
- `region_team_slot_count` (`6`), `region_storage_capacity` (`500`, per resource) — §7.
- `region_auto_sell_gleam_cap_per_hour` (`24`) — §7. **BALANCE-CRITICAL** — the Stage-3
  inflation-spiral fix; any change requires re-running Formula 7's numeric proof.
- `offline_elapsed_upper_clamp_hours` (`72`) — §7. Resolves `save-load-persistence`'s explicitly
  deferred `offline_elapsed_upper_clamp` field.
- `automation_rarity_parity_percent` extension (Fully Mastered `75%`, Optimized `100%`) — §7.
  Extends `loot-drop-system`'s own registered constant table.
- Automation Capability Stage → mastery-level gate mapping (`Stage 1/2 → 0`, `Stage 3 → 2`,
  `Stage 4 → 3`) — §3.5.

**Not proposed for registration** (deliberately): the region state machine's two-state shape
(`unconquered`/`mastered`) and the Mastery Transition's mechanical sequence (§3.7) — these are
structural facts with no other canonical document to diverge against, cited from this document's §3
by name, matching the precedent `item-data-schema.md` and `loot-drop-system.md` both set for their
own structural/schema facts.


