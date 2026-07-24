# Memory Dust Prestige System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | economy-designer |
| **Status** | Complete — authored in rush mode per this session's explicit instruction (no user available for section-by-section approval). Every ambiguity is resolved with an explicit, flagged design call below, never a placeholder. **This document makes the single most consequential structural call of any Full Vision system: what "prestige" even means in a game where nothing is supposed to be safe to throw away.** Validate at `/design-review` and `/gate-check` before this tier enters Production. |
| **Priority / Tier** | **Full Vision — explicitly deferred past MVP and Vertical Slice** (game-concept.md's own MVP Definition and Scope Tiers table). Designed fully now, per this task's explicit instruction, because every other Full-Vision-adjacent document already left hooks for it (`region-mastery-automation-system`, `the-forge-system`, `creature-jobs-evolution-system`, `resonance-weaving-system` all name it as a dependent with an open question). Those hooks needed a coherent answer before they calcify. |
| **Depends On** | `region-mastery-automation-system.md`, `the-forge-system.md`, `creature-jobs-evolution-system.md`, `resonance-weaving-system.md`, `loot-drop-system.md`, `save-load-persistence.md` |
| **Depended On By** | None (leaf system) |

## Source Material Read

`design/gdd/region-mastery-automation-system.md` (full — the mastery-level ladder, RMP accrual, the locked 25–120% idle-efficiency bands, the Stage-3 `region_auto_sell_gleam_cap_per_hour = 24` inflation fix and its numeric proof, all BALANCE-CRITICAL/LOCKED tuning-knob flags), `design/gdd/the-forge-system.md` (full — the Forge's closed-sink economic-integrity proof, `vow_binding_cost`, Formula 1 IPS and Formulas 6–8's conversion rates, the Vow-binding permanence rule), `design/gdd/creature-jobs-evolution-system.md` (full — the five evolution influence vectors, the tag grammar, the MVP-scoped Verdant Whelp tree, the "1 charm slot, VS candidate for expansion, not committed" open item), `design/gdd/resonance-weaving-system.md` (full — the Vow Data Object schema, Formulas 1–2's power-multiplier derivation, the locked Source/Form/loadout counts, its own explicitly-flagged open question for this document), `design/gdd/loot-drop-system.md` (full — the Loot Filter Rule Engine schema/evaluation semantics, Formula 7's faucet-rate model, Formula 8's faucet-vs-sink closure analysis and its own flagged forward risk), `design/gdd/game-concept.md` (full — Pillars, Core Loop's "Memory Dust prestige resets" line, MVP Definition's explicit deferral), `design/gdd/save-load-persistence.md` (Save File Structure, per-section `section_schema_version` pattern), `design/registry/entities.yaml` (checked — no existing Memory Dust or prestige-related entries; this document is the first to define this content).

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Nothing is ever destroyed, reset, or reverted by this system. Full stop.** No region de-masters, no creature reverts, no item — Vow-bound or otherwise — is touched. This is the single load-bearing structural call of the entire document (§3.1). | The task's own three bulleted constraints (Pillar 2's earned-automation guarantee, Pillar 5's creatures-as-shaped-individuals guarantee, and the Forge's explicitly irreversible Vow-binding) each independently rule out a destructive reset. Rather than trading off against one pillar to satisfy the genre convention, this document declines the convention. |
| A2 | **Memory Dust is earned exclusively from region mastery-level milestones** (the Mastery Transition itself, plus each of the three subsequent ratchet advances already defined by `region-mastery-automation-system` Formula 2) — never from raw accumulated item/currency value, and never from boss kills counted independently of the mastery ladder they already drive. | The task explicitly asks "from what?" and flags accumulated-value as a plausible source. Accumulated value was rejected: it would double-count the exact faucet `loot-drop-system` Formula 7/8 already modeled and bounded, reopening a question that document closed. Boss kills alone would be redundant with the Mastery Transition, which is itself boss-kill-gated. Keying to the mastery ladder's own ratchet gives Memory Dust a single, already-monotonic, already-bounded-per-region source for free — no new economic surface is introduced, only a milestone reward layered on an existing one. |
| A3 | **The unlock tree is finite and completable**, not an infinite scaling-multiplier system. 19 unlocks across the 6 named categories, total cost 660 Memory Dust. | Directly argued in §3.4. Infinite scaling multipliers are the idle-genre default, but this game's own stated soft-endpoint ("a fully automated empire," game-concept.md) wants a completable feeling, and — more concretely — an infinite tree would require this document's economic proofs (§4 Formula 3/4) to be re-verified forever against an ever-growing input space. A finite tree lets every proof in this document be checked once, exhaustively, and stay checked. |
| A4 | **No Memory Dust unlock may ever modify a value another system's Tuning Knobs table marks LOCKED, and `region_auto_sell_gleam_cap_per_hour` specifically is excluded from this document's authority even though it is only BALANCE-CRITICAL (not LOCKED) in its owning document.** | The task demands a rigorous proof the unlock set cannot reintroduce the exact Stage-3 inflation spiral `region-mastery-automation-system` closed. The strongest available proof is structural exclusion, not "we stayed inside the safe range" — the Gleam cap is described by its own owning document as "the single hardest-working number in this document's economic design," reverse-engineered against a specific target. This document does not re-litigate it under any framing. |
| A5 | **Unlocks are permanent, one-directional purchases — no respec, no refund.** | Mirrors item-data-schema's and the-forge-system's own Vow-binding permanence tonally and mechanically (§2), and avoids ever needing to design a Memory Dust "recompute" path that would conflict with `resonance-weaving-system` AC10's requirement that a Vow's `power_multiplier` never drifts at runtime. |
| A6 | **A player who never opens the Memory Dust screen experiences a complete, fully-realized game.** Memory Dust unlocks are bonus depth layered on top of an already-complete base game (whatever regions/content ship at a given tier) — they gate zero regions, zero forge operations, zero evolution branches that aren't *also* independently reachable through normal content progression. The one true content-gate exception (Verdant Communion's 4th evolution branch, Second Charm's 2nd charm slot) is explicitly flagged as Memory-Dust-exclusive bonus content, not a re-gating of anything already promised elsewhere. | Directly required by the task's Acceptance Criteria demand ("the game is completable without ever prestiging"). Framing Memory Dust as strictly additive, never a gate on the base experience, is what makes that AC trivially provable rather than a delicate balancing act. |
| A7 | **Three explicitly-flagged open items from other documents are resolved here, not deferred further**: `resonance-weaving-system`'s "must state whether a prestige reset wipes bound Vows, unlocks new condition_types/Sources/Forms, or both"; `creature-jobs-evolution-system`'s "expected to read evolved creatures' final Role/tree position as part of whatever it carries across a prestige reset"; `region-mastery-automation-system`'s "expected to read a region's final mastery_level/RMP as part of whatever progression it carries across a prestige reset." All three were written under the assumption a reset would exist. A1 overturns that assumption; §6 states the resolution explicitly back to each document. | Matching this project's own established practice (see `region-mastery-automation-system` A6, A8, etc.) of closing every flagged open item with an explicit ruling rather than a second deferral. |
| A8 | **On first load after this system ships into an existing save** (a player whose regions were mastered before Memory Dust existed), all milestone rewards already legitimately earned are retroactively credited once, not withheld. | Matches this project's consistent "never punish existing progress" precedent (`region-mastery-automation-system` A1's no-demotion ratchet, its own Edge Case 1's no-catch-up-but-no-penalty framing) applied to a deployment-timing scenario this document is the first to actually need. |

---

## 1. Overview

Memory Dust is Resonance Hunter's Full Vision permanent meta-progression layer — a currency earned automatically as a side effect of region mastery milestones the player is already earning through the core loop, and spent on a finite, 19-entry unlock tree spanning the six categories game-concept.md names: automation rules, loot filters, starting bonuses, evolution branches, forge upgrades, and Resonance Weaving components. It is deliberately **not** a reset system: no region de-masters, no creature reverts or loses evolution progress, no item — Vow-bound or otherwise — is touched, ever. Every unlock is a permanent, account-wide, additive bonus layered on top of a game that remains completely playable, and completable, without the player ever engaging this system at all. The document's second job, alongside designing that unlock tree, is proving — with worked numeric examples, not assertion — that no unlock or combination of unlocks can reintroduce the Stage-3 currency-realization inflation spiral `region-mastery-automation-system` closed at a hard-coded 24 Gleam/hour ceiling, or push any region's idle efficiency past the locked 120% ceiling that document's Formula 4 establishes.

## 2. Player Fantasy

> **Most prestige systems ask you to say goodbye.** They hand you a shining new multiplier and, in the same breath, take the world you built away — the regions, the roster, the gear, all of it, gone, replaced by a bigger number and the promise that this time will be faster. Resonance Hunter doesn't ask that of you. It asks something rarer: **what did all of that teach you, and what can you do with it now that you're not starting over?**

The Hunter in this game never loses a region they bled for, never loses a creature they shaped through a hundred small choices, never loses the scar of a Vow they bound in a moment of conviction. Everything the player built stays exactly where they left it — still running, still producing, still *theirs*. What Memory Dust represents is something else entirely: the residue of mastery itself, distilled every time a region climbs another rung of its own ladder, spendable on making *everything the player will ever do next* — every future region, every future evolution, every future Vow — a little sharper, a little faster, a little more theirs. It's not "you get to remember what you had." It's "what you remember makes what you still have stronger."

Concretely, this system delivers on that fantasy by making three things true:

- **Nothing you loved is ever a bargaining chip.** A Vow-bound charm with three scars layered under its current glyph is exactly as safe from this system as a creature mid-evolution or a region you haven't touched in months. Memory Dust never asks the player to weigh sentimental value against mechanical advantage — that trade doesn't exist here.
- **Every region you master, forever, makes you a little more formidable, forever.** The first region you ever fully Optimize teaches the game's own systems something permanent about you — a faster mastery ladder, a wider roster, a cheaper Vow-binding — that every region after it inherits from the moment it's conquered. Mastery compounds without erasing what came before it.
- **There is a horizon, and you can see it.** The tree is finite. A player who wants to know "am I done with this system" can look at 19 named unlocks and know the exact answer, rather than staring down an infinite treadmill dressed up as endgame content — matching the "fully automated empire" the game's own core loop already names as its soft target.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: what Memory Dust is, how it accrues, the full unlock catalog and its costs/effects, the purchase/gating rules, and the rigorous proof that the unlock set cannot violate any economic ceiling established elsewhere. It does **not** own, and defers to the cited document in every case: region state, mastery levels, RMP, and the idle efficiency curve itself (`region-mastery-automation-system`) — this document only *reads* mastery-level transitions to trigger rewards, and *adds* narrowly-scoped bonus terms on top of that system's own formulas, never redefining them; the Forge's operations, valuation (IPS), and Vow-binding action (`the-forge-system`) — this document only discounts one already-declared tuning knob within its own declared safe range; the evolution tree, tag grammar, and work-state machine (`creature-jobs-evolution-system`) — this document only adds new tree content and a slot-count bump, using that document's own schema; the Vow catalog and its derivation formulas (`resonance-weaving-system`) — this document only adds new catalog entries, computed by that document's own formulas, never altering the formulas themselves; the loot table, rarity math, and Loot Filter Rule Engine (`loot-drop-system`) — this document only proposes a rule-slot-count field that system does not yet define, flagged explicitly as a proposal, not an assumed mechanic.

### 3.1 The Core Design Call: What Resets? Nothing.

This is the question the task calls "the most important section of the GDD," and it deserves a direct, rigorously argued answer rather than a hedge.

**The naive genre default — a full reset with a permanent multiplier — is incompatible with this game, on three independent grounds, any one of which would be sufficient alone:**

1. **Pillar 2 (Automation Is Earned, Not Assumed) makes region resets actively hostile, not neutral.** Region mastery in this game is earned through active boss kills and sustained, correctly-composed team management (`region-mastery-automation-system` §3.2's RMP accrual, gated on *Healthy* work-ticks specifically so it can't be idled through). A reset that un-masters a region doesn't just cost the player time — it forces a binary bad outcome: either the fight is now trivial (the reset was tedious busywork dressed as content) or the player has since let their active-combat skill atrophy while automation carried them (the reset is punishing). Neither outcome honors "automation is earned" — it honors "automation is earned, then taken away, then re-earned as a formality." That's not the same design intent.
2. **Pillar 5 (Creatures Are Systems, Not Trophies) makes creature deletion a direct hit to the game's thinnest Self-Determination-Theory need.** game-concept.md's own Player Motivation Profile flags Relatedness as "Minimal... flagged as a future opportunity, not a current gap" — the creature roster is explicitly named as the light companion feel carrying what Relatedness this game has. `creature-jobs-evolution-system` §3.3 defines a creature's evolution path as "deterministically traceable to player actions" — a specific, individual history of feeds, job assignments, and combat behavior the player *did*, not a randomized rarity roll. Deleting that on reset doesn't just cost power, it deletes the one thing in the game closest to a relationship. A system explicitly designed to strengthen this axis should not be the system that guts it.
3. **The Forge's Vow-binding is permanent by explicit design intent, not by oversight.** art-bible §5.2 locks this precisely: "if the Vow is later replaced, the old scar remains... never erased." `the-forge-system` §3.6 enforces this mechanically — a `VowBindingEntry` is only ever appended, never deleted. A prestige reset that wipes Vow-bound items contradicts a rule the project already treats as inviolable everywhere else it appears. There is no principled way to say "permanent, except during a reset."

**Given that, the four options the task poses resolve as follows:**

- *Regions reset but creatures/items persist* — rejected. This still violates ground 1 above in full; splitting the destruction across categories doesn't remove it from any one of them.
- *A voluntary reset with escalating rewards* — rejected. Making the reset opt-in doesn't change what it destroys when chosen; it only changes who experiences grounds 1–3, and a system whose "reward" is contingent on a player choosing self-harm to their own roster is not a reward structure this project's reward-psychology standards should endorse.
- *"New Frontier" framing (push into a harder tier rather than restarting)* — the closest fit, and this document borrows its spirit (§3.5's loop is explicitly about pushing forward, never backward) — but rejected as the literal mechanic, because gating new *regions* behind a prestige action would invent a content-progression mechanic this document does not own (region-to-region unlock ordering belongs to whatever system eventually authors `region-view-world-map-ui`'s progression, not to this one), and because game-concept.md's own six named unlock categories never include "unlock new regions" — inventing that scope would exceed this document's brief.
- **"Nothing resets" (Memory Dust as a purely additive layer)** — **this is the call.** The task itself flags the risk with this option honestly: "but then it isn't really 'prestige.'" That's worth answering directly rather than ignoring: a prestige system's actual defining properties are (a) a resource earned specifically from deep, sustained progression, (b) spent on permanent bonuses, (c) that measurably compound to make subsequent progression faster. This document satisfies all three (§3.2–§3.5, with a numeric proof in §4 that later regions climb their mastery ladder measurably faster than earlier ones did). The **only** genre convention this design declines is the reset — and every one of the three grounds above independently explains why declining it is the correct call for this specific game, not a cop-out from designing a harder system.

**What Memory Dust is, restated precisely**: a currency that accrues permanently and only ever accrues (§3.2), spent on a finite set of permanent, account-wide bonuses (§3.3–§3.4) layered on top of a game state that is never rolled back, deleted, or altered destructively by this system in any way.

### 3.2 How Memory Dust Is Earned

Memory Dust accrues automatically, with no separate player action required to "activate" prestige — it is a direct side effect of `region-mastery-automation-system` Formula 2's own mastery-level ratchet, read (never written) by this document.

Every region, independently, crosses up to four milestone events over its lifetime — each one fires **exactly once per region, ever**, matching that document's own ratchet guarantee that `mastery_level` never decreases:

| Milestone Event | Region-Mastery-System Trigger | Memory Dust Awarded |
|---|---|---|
| Mastery Transition | `region_state: unconquered → mastered` (§3.7 there — the boss kill itself) | 3 |
| Reaches Partially Mastered | `mastery_level: 0 → 1` (Formula 2 there) | 7 |
| Reaches Fully Mastered | `mastery_level: 1 → 2` | 20 |
| Reaches Optimized | `mastery_level: 2 → 3` (requires `CCS = 1.0` at the check, per that document's A2) | 50 |

A single region fully climbed to Optimized contributes **80 Memory Dust** total across its lifetime, credited incrementally at each threshold-crossing tick — not in a single lump at the end. A region the player conquers but never assigns a team to still earns the flat 3 Memory Dust from its Mastery Transition alone (Edge Case 10, §5) — even the most minimally-engaged region contributes something, so no region's active-combat effort is ever "wasted" from this system's perspective.

Memory Dust is never earned from raw item value, Gleam balance, kill count, or any other source — deliberately, to avoid re-opening the exact faucet question `loot-drop-system` Formula 7/8 already modeled and closed (A2).

### 3.3 The Six Unlock Categories

Nineteen unlocks total, organized into the six categories game-concept.md names, each purchased with Memory Dust earned per §3.2, each permanent once bought (A5). Every unlock is designed against one hard rule, proven in §4:

> **No unlock in this catalog modifies any value marked LOCKED in its owning system's Tuning Knobs table, and none modifies `region_auto_sell_gleam_cap_per_hour` under any framing (A4).**

#### 3.3.1 Automation Rules (`region-mastery-automation-system`) — 130 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 1 | Wider Roster I | `region_team_slot_count` (§3.3 there, default 6, safe range 5–8) +1, account-wide, all regions | 15 | §3.3, §7 there |
| 2 | Wider Roster II | +1 more (total +2; 6→8, the declared safe-range ceiling — no further rank exists, structurally capped) | 35 | §7 there |
| 3 | Attuned Ascension I | `rmp_threshold_1/2/3` (Formula 2, §7 there) each reduced 10% from base default, account-wide, all regions (present and future) | 25 | Formula 2, §7 there |
| 4 | Attuned Ascension II | Cumulative 20% reduction from base default (self-imposed hard ceiling, §4 Formula 5) | 55 | Formula 2, §7 there |

Effect on the game: a region assigned a complete team climbs its own mastery ladder measurably faster than the very first region the player ever conquered did, without changing what the ladder's bands mean or how hard Optimized's `CCS = 1.0` gate is to satisfy.

#### 3.3.2 Loot Filters (`loot-drop-system`) — 60 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 5 | Discerning Eye I | +3 `LootFilterRule` slots (§3.9 there) — **proposed addition**: that document does not itself cap rule count; this document proposes a starting cap of 5 rules and grants capacity increases against it, flagged for that system's owner to confirm | 10 | §3.9 there |
| 6 | Discerning Eye II | +3 more slots (cumulative +6 from the proposed starting cap) | 20 | §3.9 there |
| 7 | Deep Read | Unlocks 2 new derived-predicate `field` keys usable in `FilterCondition` — `feed_contribution_value_gte`, `combat_behavior_tag_has` — pure expressiveness, exercising that document's own explicitly "open key" field design, zero effect on any drop-rate or rarity formula | 30 | §3.9.1 there |

Effect on the game: filter rules can express more nuanced, more numerous automation policies without ever touching what drops, how often, or at what rarity.

#### 3.3.3 Starting Bonuses — 90 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 8 | Veteran's Head Start I | Every future Mastery Transition (§3.7, `region-mastery-automation-system`) begins the new region at `region_mastery_points = 100` instead of `0` | 20 | §3.2, §3.7 there |
| 9 | Veteran's Head Start II | Cumulative starting RMP raised to 250 (proven safely below any reachable `rmp_threshold_1`, §4 Formula 5) | 45 | §3.2 there |
| 10 | Hunter's Cache | Flat 75 Gleam granted to the player the instant any future region completes its Mastery Transition — a currency welcome gift, not tied to any drop table | 25 | §3.7 there (trigger only); `the-forge-system` §3.5.1 (Gleam as the resource granted) |

Effect on the game: every region *after* the first meaningful Memory Dust spend starts its climb ahead of where the previous one did — the literal mechanism by which "a second run is meaningfully faster" without touching a single already-mastered region retroactively.

#### 3.3.4 Evolution Branches (`creature-jobs-evolution-system`) — 120 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 11 | Verdant Communion | Unlocks the Verdant Whelp evolution tree's 4th, Support-role destination (that document's own §7 MVP scope note names this branch as "VS-deferred... only the content... is missing" — this is that content, authored using the exact `unlock_condition_tags` grammar, §3.4 there) | 30 | §3.4, §7 there |
| 12 | Second Charm | Raises the creature equipment slot count from 1 to 2 (§3.3 Vector 2 there, explicitly flagged "VS candidate for expansion, not committed" — this unlock is what commits it). An `equip:<trait>` gate (§3.4 there) is satisfied if **any** currently equipped charm carries the trait, once 2 slots exist. | 50 | §3.3 Vector 2, §3.4 there |
| 13 | Deeper Roots | Job-diligence and combat-tally thresholds (`job:<role>:<int>`, `combat:<tag>:<int>`, both content-authored placeholders per §7 there) reduced 15%, account-wide, all evolution nodes | 40 | §7 there |

Effect on the game: creatures recruited after these unlocks have access to more evolution destinations, more equipment expressiveness, and a modestly faster path through the same branches — no existing creature's tree, progress, or role is ever rewritten.

#### 3.3.5 Forge Upgrades (`the-forge-system`) — 125 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 14 | Practiced Hand I | `vow_binding_cost` (§3.6, §7 there; default 200 Gleam + 5 materials, safe range 100–500 Gleam / 2–10 materials) reduced to 175 Gleam + 5 materials | 25 | §3.6, §7 there |
| 15 | Practiced Hand II | Reduced further to 150 Gleam + 4 materials — still comfortably above a maxed Legendary weapon's own `sell_value` of 97 Gleam (§4.6 there), preserving that document's own design intent that binding "should always cost meaningfully more than one Sell/Dismantle nets" (§3.6 there) | 55 | §3.6 there |
| 16 | Appraiser's Instinct | `sell_conversion_rate` (Formula 6, §7 there; default 15, safe range 5–30) raised to 20 — proven in §4 Formula 4 not to affect `region_auto_sell_gleam_cap_per_hour`'s realized-value ceiling | 45 | Formula 6, §7 there |

Effect on the game: the Forge's highest-ceremony action becomes more attainable over a career, and manual Sell returns modestly more per item — both proven, not merely hoped, to leave the Stage-3 automation cap completely untouched (§4 Formula 4).

#### 3.3.6 Resonance Weaving Components (`resonance-weaving-system`) — 135 MD total

| # | Unlock | Effect | Cost | Cites |
|---|---|---|---|---|
| 17 | Eleventh Vow | A new `conditional` Vow catalog entry (e.g., a Vow gating an ability on `region_mastery_level_below`, a new `condition_type` — content, not a schema change, per that document's own explicit "new `condition_type` values are content" pattern, §3.3.4 there), `power_multiplier` computed once via Formula 1 (§4.1 there) exactly like the original 10, never hand-tuned independently | 30 | §3.3.4, Formula 1 there |
| 18 | Twelfth Vow | A new `static_cost` Vow catalog entry, `power_multiplier` computed via Formula 2 (§4.2 there) | 45 | §3.3.3, Formula 2 there |
| 19 | Thirteenth Vow | A second new `conditional` Vow catalog entry, Formula 1 again | 60 | Formula 1 there |

Effect on the game: the Vow catalog grows from 10 to 13 over a full career — more build-identity options (Pillar 4) — while the loadout cap (4 total, locked), the 6 Sources, and the 6 Forms (all locked per that document's own Tuning Knobs table) are never touched, and no existing Vow's stored `power_multiplier` is ever recomputed (preserving that document's AC10 exactly).

### 3.4 The Unlock Tree Structure

**Finite and completable (A3).** 19 unlocks, 660 Memory Dust total cost. **Ranks within a single unlock line are strictly sequential** — e.g., Wider Roster II cannot be purchased before Wider Roster I (Formula 2, §4). Unlocks across different lines/categories have no ordering dependency on each other. Every purchase is permanent (A5) — there is no respec, refund, or "un-spend" mechanic anywhere in this system.

**Why finite is the right call for this game, not merely the simpler one to design**: an infinite scaling-multiplier tail is the idle genre's default because most idle games treat "number keeps growing forever" as the whole endgame fantasy. This game's own stated soft-endpoint is explicitly different — game-concept.md's Long-Term Progression section names "a fully automated empire" as the target, not "an ever-climbing power number." A finite tree lets that fantasy actually resolve: a player can look at 19 named unlocks, know exactly what remains, and eventually stand at a genuine "I finished this" moment — while every one of this document's economic proofs (§4) only has to hold across a fixed, enumerable input space, checked once and never re-opened by a hypothetical unlock #20 this document didn't anticipate.

**Supply vs. cost, worked (illustrative — final region count is content-authoring TBD per game-concept.md's own Technical Considerations table)**: at 80 Memory Dust per region fully Optimized (§3.2), and an illustrative 10-region Full Vision roster, fully Optimizing 9 of 10 regions yields `9 × 80 = 720` Memory Dust — enough to complete the entire 660-cost tree with 60 to spare, without requiring literal 100% perfection across the whole roster. If the final region count lands lower or higher than this illustrative 10, the tree's completability scales with it automatically (more regions shipped over time makes completing an already-fixed-cost tree *easier*, never harder) — a deliberate property of keeping the tree's total cost fixed while its funding source is genuinely open-ended.

**Tree completion is not required for anything.** A1/A6 already establish this; restated here structurally: no region-mastery gate, no Forge operation, no evolution branch outside the two explicitly Memory-Dust-exclusive ones (Verdant Communion, Second Charm), and no Vow beyond the original 10 requires any Memory Dust spend to reach. Completing the tree is a genuine, visible, optional achievement — not a checkpoint the base game is paced around.

### 3.5 The Memory Dust Loop

Restated end to end, since it's easy to lose in the unlock catalog's detail: the player actively hunts a region to its boss, defeats it (the Mastery Transition, `region-mastery-automation-system` §3.7), assigns and manages a team to climb that region's mastery ladder (its own Formula 2), and — as a byproduct of milestones they were already earning for the region's own sake — accrues Memory Dust (§3.2). That Memory Dust buys a permanent, account-wide bonus (§3.3) that makes the *next* region's climb, the *next* creature's evolution, the *next* Vow-binding, or the *next* Forge visit meaningfully better than it would have been otherwise. The player then does the same thing again, now measurably faster, on a region or creature or Vow they've never touched before — never on the ashes of one they have. This loop runs continuously alongside the core loop, with no discrete "prestige moment" a player opts into or out of — it is simply always accruing, in the background, exactly like `region-mastery-automation-system`'s own RMP meter.

### 3.6 Resolving Open Items Other Documents Flagged For This One

Three documents explicitly deferred a question to this one, written under the assumption a reset would exist (A7). Resolved directly:

- **`resonance-weaving-system` §6** asked whether a prestige reset "wipes bound Vows, unlocks new `condition_type`s/Sources/Forms, or both." **Answer**: neither framing applies, because there is no reset (A1) — no Vow is ever wiped, bound or otherwise. This document does unlock new `condition_type`s, exclusively as new Vow catalog *content* (§3.3.6, unlocks 17–19), computed by that document's own unchanged Formulas 1–2. It never unlocks new Sources or Forms — both are explicitly `locked, not tunable` in that document's own Tuning Knobs table (§7 there), and this document's hard rule (§3.3, A4) forbids touching any LOCKED value regardless.
- **`creature-jobs-evolution-system` §6** described itself as expecting to "read evolved creatures' final Role/tree position as part of whatever [Memory Dust] carries across a prestige reset." **Answer**: there is no reset to carry anything across (A1) — every creature's Role, tree position, `evolution_progress`, `healthy_work_ticks`, and `combat_behavior_tally` persist completely untouched, indefinitely, exactly as if this system did not exist. This document's only two writes into that system's domain are additive content (a new tree node, §3.3.4 unlock 11) and an additive slot-count/threshold change (unlocks 12–13) — never a read-then-carry-across-a-boundary operation, because no boundary exists.
- **`region-mastery-automation-system` §6** described itself as expecting this document to "read a region's final `mastery_level`/RMP as part of whatever progression it carries across a prestige reset." **Answer**: this document does read `mastery_level` transitions — continuously, as they happen, never at a one-time "final" boundary — purely to trigger the milestone rewards of §3.2. It never writes to an existing region's `region_mastery_points` or `mastery_level` retroactively; its only writes into that system's domain are the Starting-Bonus unlocks (§3.3.3), and those apply exclusively to regions undergoing their Mastery Transition **after** the unlock is purchased, never to a region already in progress.

## 4. Formulas

### Formula 1 — Memory Dust Accrual

```
memory_dust_earned_this_event = milestone_dust_value[event_type]

lifetime_memory_dust_earned = Σ over every region r, over every milestone event e ∈
    {mastery_transition, level_0_to_1, level_1_to_2, level_2_to_3} that r has reached,
    of milestone_dust_value[e]        — each (r, e) pair counted at most once, ever

memory_dust_available = lifetime_memory_dust_earned − lifetime_memory_dust_spent
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `milestone_dust_value[event_type]` | int, tuning knob (§7) | `{3, 7, 20, 50}` | Reward per milestone event, keyed to `region-mastery-automation-system` Formula 2's own mastery-level transitions. |
| `lifetime_memory_dust_earned` | int | ≥ 0, monotonically non-decreasing | Never decreases — mirrors the RMP ratchet it is keyed to. |
| `memory_dust_available` | int | ≥ 0 | What the player can currently spend (Formula 2). |

**Output range**: bounded per region at exactly `3 + 7 + 20 + 50 = 80` (a region cannot re-earn a milestone it already reached — the same idempotency guarantee `region-mastery-automation-system`'s own ratchet already provides for `mastery_level` itself). Unbounded in aggregate only in the sense that it scales with how many regions exist and how many the player masters — never with time played, kill count, or item value.

**Worked example** (reusing `region-mastery-automation-system` Formula 1/2's own worked example directly — a complete 6-role team, all Healthy continuously, reaching Optimized at `CCS = 1.0`): Mastery Transition fires at the boss kill (+3 MD, immediate); Partially Mastered at `RMP ≥ 500`, ≈26 minutes of sustained Healthy work later (+7 MD, running total 10); Fully Mastered at `RMP ≥ 2000`, ≈1.72 hours in (+20 MD, running total 30); Optimized at `RMP ≥ 5000`, ≈4.30 hours in, succeeding immediately since this worked team already has `CCS = 1.0` (+50 MD, running total **80**). This is the exact real-time pacing at which a single fully-engaged region funds roughly one-eighth of the entire unlock tree (§3.4).

### Formula 2 — Unlock Purchase Rule

```
can_purchase(unlock_id, rank) =
    (rank == 1  OR  unlock_owned(unlock_id, rank − 1))
    AND  memory_dust_available ≥ unlock_cost(unlock_id, rank)
    AND  NOT already_owned(unlock_id, rank)

on purchase:
    lifetime_memory_dust_spent += unlock_cost(unlock_id, rank)
    unlocks_owned.add((unlock_id, rank))          — permanent (A5), never removed
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `unlock_owned(unlock_id, rank)` | bool, computed | — | True once that specific rank has ever been purchased. |
| `unlock_cost(unlock_id, rank)` | int, content data | 10–60 (§3.3's tables) | Fixed per unlock, per rank — never scales with anything live. |

**Output range**: a boolean gate. **Worked example**: a player with 62 Memory Dust available who has not yet purchased Wider Roster I attempts Wider Roster II (rank 2) — rejected (`unlock_owned(wider_roster, 1) = false`), regardless of Dust balance. The same player purchases Wider Roster I (cost 15, rank 1, always purchasable) — `memory_dust_available` drops to 47, `unlocks_owned` now contains `(wider_roster, 1)`; Wider Roster II is now legally purchasable if 35 ≤ 47, which holds.

### Formula 3 — Structural Proof: The 120% Idle Efficiency Ceiling Cannot Be Broken

`region-mastery-automation-system` Formula 4 defines `idle_efficiency_percent = band_min(mastery_level) + (band_max(mastery_level) − band_min(mastery_level)) × team_quality_score`, where `team_quality_score` is itself clamped to `[0, 1]` by that document's own Formula 3 (`clamp(ccs_weight × CCS + pqs_weight × PQS, 0, 1)`). The ceiling (`120%`, at `mastery_level = optimized`, `team_quality_score = 1.0`) is therefore reachable only through four inputs: `band_min`, `band_max`, `ccs_weight`, `pqs_weight` (and its dependent, `power_tier_reference_ceiling`).

**The proof is enumeration, not argument**: this document's full unlock catalog (§3.3, 19 entries) is exhaustively listed above. None of the four values in this list is modified by any unlock in that catalog:

| Value | Modified by any Memory Dust unlock? | Why not |
|---|---|---|
| `band_min` / `band_max` | No | LOCKED in the owning document's own Tuning Knobs table (§7 there) — excluded outright by this document's hard rule (§3.3, A4). No unlock in §3.3.1–3.3.6 even references these fields. |
| `ccs_weight` / `pqs_weight` | No | Not touched by any of the 4 Automation Rules unlocks (§3.3.1) — those modify `region_team_slot_count` and `rmp_threshold_1/2/3` only, neither of which appears anywhere in Formula 3 or Formula 4's expression. |
| `power_tier_reference_ceiling` | No | Not referenced by any unlock in this document. |

**Conclusion**: since `idle_efficiency_percent`'s only inputs capable of moving its ceiling are structurally absent from every path this document can write through, `idle_efficiency_percent` cannot exceed `120%` for any region, under any combination of Memory Dust unlocks, purchased in any order, at any account age. This is not a policy the document commits to — it is a property of which fields the 19 unlocks are defined to touch, verifiable by grep against §3.3's tables.

### Formula 4 — Structural Proof: The Stage-3 Gleam-Realization Cap Is Invariant

`region-mastery-automation-system` Formula 7 defines `gleam_realized_over_window(hours) = min(total_pending_auto_sell_backlog_value, region_auto_sell_gleam_cap_per_hour × hours)`. Two of this document's Forge Upgrade unlocks (§3.3.5) touch values that *feed into* `total_pending_auto_sell_backlog_value` without touching the cap term itself:

- **Practiced Hand I/II** modify `vow_binding_cost` — this value does not appear anywhere in Formula 7's expression at all; it only affects how much Gleam a Vow-binding *consumes*, a wholly separate transaction (`the-forge-system` §3.6).
- **Appraiser's Instinct** raises `sell_conversion_rate` from 15 to 20 — this value feeds `the-forge-system` Formula 6 (`sell_value = round(IPS × sell_conversion_rate)`), which in turn feeds `total_pending_auto_sell_backlog_value` (the sum of unsold `auto_sell`-tagged items' `sell_value`).

**The proof**: `min(backlog, cap × hours)` is monotonically non-decreasing in `backlog` only up to the point `backlog ≥ cap × hours`, past which the `min()` clamps flat regardless of how much larger `backlog` grows. Since a 100-hour unattended session already produces a backlog several orders of magnitude larger than `24 × 100 = 2,400` (`loot-drop-system` Formula 8 models ≈760,000 Gleam-equivalent of gross item value at Automation Stage 2 alone, well past the clamp point even before Appraiser's Instinct is purchased), raising `sell_conversion_rate` by 33% (15→20) moves `backlog` further above a threshold it was already far past — it cannot move `gleam_realized_over_window` at all, because the `min()` is already resolving to the `cap × hours` branch, not the `backlog` branch.

**Worked worst-case numeric proof** (both Forge unlocks maxed simultaneously, the most favorable case for the player): 100-hour unattended Stage-3 session, `region_auto_sell_gleam_cap_per_hour` unchanged at its default **24** (never modified by this document, per A4):

```
max_gleam_realized_100hr = 24 × 100 = 2,400 Gleam        — identical to the original proof, unchanged
vow_binding_cost (Practiced Hand II, maxed) = 150 Gleam
max_vow_bindings_affordable = floor(2,400 / 150) = 16
```

Sixteen affordable Vow-bindings against the original document's realistic-cadence target of roughly a dozen — the **same order of magnitude**, not the 3,800-binding, two-to-three-order-of-magnitude blowout the original 24-Gleam-cap fix was built to prevent. This holds under the single worst-case combination this document's entire catalog can produce (every Forge unlock purchased, maximum theoretical backlog) — there is no unlock combination that produces a worse number than this, because no unlock in the catalog touches the one term (`region_auto_sell_gleam_cap_per_hour`) that actually bounds the result.

### Formula 5 — Starting-Bonus Safety Margin

```
banked_rmp_head_start_max = 250        — Veteran's Head Start I + II, both purchased (§3.3.3)
rmp_threshold_1_min_reachable = rmp_threshold_1_base × (1 − 0.20)     — Attuned Ascension I+II's self-imposed 20% cap (Formula 3's own bound)
                              = 500 × 0.80 = 400
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `banked_rmp_head_start_max` | int, fixed by content | 250 | Maximum RMP any Memory Dust unlock can ever pre-load into a fresh region. |
| `rmp_threshold_1_min_reachable` | int, derived | ≥ 300 (that system's own declared safe-range floor) | The lowest `rmp_threshold_1` can ever be pushed by this document's own Attuned Ascension unlocks. |

**Output**: `250 < 400` — a strict inequality, with an 150-RMP margin, proven at the maximum combination of every relevant unlock this document offers. **A brand-new region, even with every Starting Bonus and every Automation Rules unlock purchased, can never skip Partially Mastered outright** — it still requires genuine, active Healthy team work (150 more RMP at the Formula 1 worked-example rate ≈ roughly 8 additional minutes of sustained complete-team production) before crossing that first rung. This is the numeric proof that "starting bonuses" compress the ladder without ever trivializing it (§3.3.3's own stated intent), verified against this document's own most aggressive combination of unlocks, not merely its default state.

## 5. Edge Cases

1. **A player prestiges [reads: engages this system at all] with Vow-bound items in their inventory or equipped.** Nothing happens to them. There is no reset event for them to survive (A1) — `vow_binding_log` and every `VowBindingEntry` in it remain exactly as they were, forever. No formula in this document reads or writes `vow_binding_log`, any `VowBindingEntry` field, or any `item_instance_id` at all — the only Forge-adjacent writes this document makes are to two cost constants (`vow_binding_cost`, `sell_conversion_rate`), never to item or binding state. This is a structural absence, not a policy promise.
2. **A player engages this system while a creature is mid-evolution** (partway through `evolution_progress`, `healthy_work_ticks`, or `combat_behavior_tally` accrual toward its next node). Nothing interrupts it. This document's only writes into `creature-jobs-evolution-system`'s domain are additive (a new tree node, a 2nd charm slot, a threshold percentage reduction) — none of them reset, zero out, or recompute any existing creature's accrued progress. A creature mid-evolution when Deeper Roots (§3.3.4, unlock 13) is purchased simply has its *remaining* distance-to-threshold reduced from that moment forward; nothing already accrued is discounted, doubled, or lost.
3. **An unlock (real or hypothetical) that would push `idle_efficiency_percent` past 120%.** This cannot be constructed from this document's own catalog — proven exhaustively in §4 Formula 3, not merely asserted. If a future revision of this document ever proposes an unlock touching `band_min`, `band_max`, `ccs_weight`, or `pqs_weight`, that revision must re-run Formula 3's enumeration from scratch and must not ship without an explicit pillar-level review, matching the LOCKED-value review bar `region-mastery-automation-system` §7 itself sets.
4. **A player who never opens the Memory Dust screen or spends a single point.** The game remains fully completable: every region can be pushed to Optimized, every Forge operation is available at its base rate, all 3 MVP evolution branches (and the Support branch is the only content gated behind Memory Dust — a bonus 4th, not a replacement for the base 3) are reachable, and all 10 starting Vows function exactly as `resonance-weaving-system` defines them. Nothing in the base game is paced around, gated by, or discounted for a Memory Dust spend (A6).
5. **A player purchases every one of the 19 unlocks (the tree is complete).** Memory Dust continues to accrue from any regions not yet at Optimized, and from any newly-conquered regions added by future content. It is never wasted, never forces a re-spend, and is displayed thereafter purely as a lifetime achievement statistic ("Total Memory Dust Harvested") — consistent with this project's standing rule that idle time and effort are never a lie (Pillar 3, extended here to completed-tree Dust).
6. **A future content update adds a new unlock tier beyond this document's 19 entries.** Out of this document's current scope — any such addition is a new GDD revision (bumping this document's own version and, per `save-load-persistence`'s additive-fields pattern, likely not even requiring a `section_schema_version` bump if the new unlocks are purely additive rows in the same schema).
7. **A player attempts to purchase a rank out of sequence** (e.g., Wider Roster II before Wider Roster I). Rejected outright by Formula 2's `unlock_owned(unlock_id, rank − 1)` gate — no partial purchase, no Dust deducted, no state change.
8. **A save file created before this system existed is loaded after it ships** (a returning player whose regions were already mastered under an earlier build). On first load, this document retroactively evaluates every already-mastered region's *current* `mastery_level`/`region_mastery_points` against the milestone table (§3.2) and credits every `(region, event)` pair the region has already legitimately reached but was never credited for, exactly once. This is not "catch-up income" in the sense `region-mastery-automation-system` Formula 8 declines to offer for elapsed offline time — it is a one-time reconciliation of milestones the player's own active/idle play already, honestly, earned before this system was available to reward them (A8).
9. **Two regions cross a milestone threshold in the same tick.** No race condition — milestone credit is keyed independently per `(region_id, milestone_event)` pair; both are credited in full, in any evaluation order, with an identical resulting `lifetime_memory_dust_earned`.
10. **A region the player conquers but never assigns a team to.** Still earns the flat 3 Memory Dust from its Mastery Transition (§3.2) — the boss-kill milestone alone — and never the 7/20/50 tiers, since those require `mastery_level` to actually advance, which requires assigned, Healthy work (§3.2 there). No region's genuine active-combat effort nets zero Memory Dust.

## 6. Dependencies

### Depends On

- **`region-mastery-automation-system.md`** — reads `mastery_level` transitions (Formula 2 there) as the sole trigger for Formula 1's milestone rewards (§3.2, §4 Formula 1); reads `region_mastery_points`, `region_team_slot_count`, and `rmp_threshold_1/2/3` as the exact fields two of this document's own unlock categories (§3.3.1, §3.3.3) apply additive/multiplicative bonuses to, never redefining that document's own Formula 2 or Formula 4. **Never reads or writes** `region_auto_sell_gleam_cap_per_hour`, `band_min`/`band_max`, or `ccs_weight`/`pqs_weight` (A4, proven in §4 Formula 3). This document's own §3.6 explicitly resolves that document's own flagged open item about what a "prestige reset" carries across — the answer being that no reset, and therefore no carry-across, exists.
- **`the-forge-system.md`** — reads `vow_binding_cost` and `sell_conversion_rate` (§7 there) as the exact two fields two of this document's Forge Upgrade unlocks (§3.3.5) discount/raise within their own already-declared safe ranges; reads Formula 6 (`sell_value`) and Formula 7 (`gleam_realized_over_window`, actually owned by `region-mastery-automation-system` but consumed by exact name in this document's own §4 Formula 4) to construct this document's central economic-invariance proof.
- **`creature-jobs-evolution-system.md`** — reads the `EvolutionNode`/`unlock_condition_tags` schema and tag grammar (§3.4 there) to author the Verdant Communion unlock's new tree node (§3.3.4) in that document's own exact format; reads `equipped_charm_id`'s "1 charm slot, VS candidate for expansion, not committed" status (§3.3 Vector 2 there) as the explicit invitation the Second Charm unlock resolves. This document's own §3.6 explicitly resolves that document's own flagged open item.
- **`resonance-weaving-system.md`** — reads the Vow Data Object schema and Formulas 1–2 (§3.3.1, §4.1–4.2 there) to author three new Vow catalog entries (§3.3.6) computed by those formulas unmodified; reads the locked Source/Form/loadout-count rows in that document's own Tuning Knobs table (§7 there) as values this document's hard rule (A4) forbids touching. This document's own §3.6 explicitly resolves that document's own flagged open item.
- **`loot-drop-system.md`** — reads the `LootFilterRule`/`FilterCondition` schema and its evaluation semantics (§3.9 there) to define the Loot Filters unlock category (§3.3.2); proposes (does not assert) a rule-count cap that document does not itself define, flagged explicitly for that system's owner in §3.3.2 and again in this document's closing registration note.
- **`save-load-persistence.md`** — this document's own persisted state (`lifetime_memory_dust_earned`, `lifetime_memory_dust_spent`, the per-`(region_id, milestone_event)` claimed-milestone set, and the `unlocks_owned` set) is stored as one additive, independently-versioned save section, per that document's own `section_schema_version` pattern (§ "Save File Structure" there) — a new section, not a modification to any existing one, so it introduces no envelope-level `schema_version` bump.

### Depended On By

None. This is a leaf system — no other document in the current systems index reads any field this document defines.

## 7. Tuning Knobs

| Knob | Field(s) | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Milestone Dust values | `milestone_dust_value[event_type]` (Formula 1) | 1–10 (transition) / 3–15 (level 1) / 10–35 (level 2) / 25–75 (level 3), sum-preserving retunes only | `3 / 7 / 20 / 50` | Primary pacing lever for how many regions the player must engage to fund the unlock tree — the single most player-visible knob in this document. Any retune must re-run §3.4's supply-vs-cost worked example. |
| Total tree cost | Sum of all 19 `unlock_cost` values (§3.3) | Should remain within 6–10× a single region's full-Optimization yield (80 MD) for the "master nearly your whole empire" completion fantasy (§3.4) to hold at typical region counts | `660` | Governs how "finished" feels achievable — too low trivializes completion, too high makes the tree feel like an infinite grind despite being finite in name. |
| `region_team_slot_count` bonus (Wider Roster I/II) | Additive to `region-mastery-automation-system`'s own knob, §3.3.1 | Must never push the effective total above that document's own declared safe-range ceiling of 8 | `+1` / `+1` (max cumulative `+2`) | Structurally hard-capped by only offering 2 ranks — cannot be retuned to exceed the ceiling without adding a rank, which would itself require re-verifying against that document's safe range. |
| `rmp_threshold` reduction (Attuned Ascension I/II) | Percentage-off-base, §3.3.1, §4 Formula 5 | Self-imposed hard ceiling **20% cumulative** — must never be raised without re-running Formula 5's safety-margin proof against the current `banked_rmp_head_start_max` | `10%` / `10%` (cumulative `20%`) | Direct pacing lever on how much faster later regions climb than earlier ones. |
| Starting RMP bank (Veteran's Head Start I/II) | `banked_rmp_head_start_max`, §3.3.3, §4 Formula 5 | Must remain strictly below `rmp_threshold_1_min_reachable` (currently 400) with a comfortable margin | `100` / `+150` (cumulative `250`) | **Structurally load-bearing for Formula 5's proof** — raising this without re-checking the margin risks a region skipping its first mastery rung entirely on conquest alone. |
| `vow_binding_cost` discount (Practiced Hand I/II) | §3.3.5, cites `the-forge-system` §7 | Must remain within that document's own declared safe range (Gleam 100–500, materials 2–10) and must remain "meaningfully more than one Sell/Dismantle nets" per that document's own §3.6 design intent | `200→175→150` Gleam, `5→5→4` materials | Any further discount rank must re-verify against the current maximum single-item `sell_value` (currently 97 Gleam, Legendary maxed). |
| `sell_conversion_rate` bump (Appraiser's Instinct) | §3.3.5, cites `the-forge-system` §7 Formula 6 | Must remain within that document's own declared safe range (5–30) | `15→20` | Proven in §4 Formula 4 to have zero effect on the Stage-3 realized-Gleam ceiling — safe to retune anywhere in the cited safe range without re-opening that proof, since the proof does not depend on this value at all. |
| **`region_auto_sell_gleam_cap_per_hour`** | — | **Not a knob this document owns or may ever touch (A4)** | `24` (unchanged, owned entirely by `region-mastery-automation-system`) | Excluded from this document's authority by explicit design rule, not merely left at default. |
| **Efficiency bands (`band_min`/`band_max`), `ccs_weight`/`pqs_weight`** | — | **Not knobs this document owns or may ever touch (A4)** | Unchanged | Excluded outright — the entire basis of §4 Formula 3's ceiling proof. |
| Loot Filter rule-slot starting cap and per-rank increase | §3.3.2 | Proposed value only, pending `loot-drop-system` owner confirmation | Start `5`, `+3` / `+3` per rank | Purely a capacity/expressiveness lever — no interaction with any drop-rate or rarity formula. |
| Evolution threshold reduction (Deeper Roots) | §3.3.4 | Percentage-off-base placeholder content values (that document's own §7 already flags these as unbalanced pending playtest) | `15%` | Should be re-tuned in lockstep with any future retune of the base placeholder thresholds in `creature-jobs-evolution-system`. |

## 8. Acceptance Criteria

1. **(Required by the brief) No Memory Dust unlock, or combination of unlocks, ever modifies `region_auto_sell_gleam_cap_per_hour`, `band_min`, `band_max`, `ccs_weight`, or `pqs_weight`.** Verified by a static content-validation pass over all 19 catalog entries (§3.3) asserting none writes to, references, or parameterizes any of these five fields — a compile-time/content-load-time check, not a runtime simulation, since the proof (§4 Formula 3) is structural.
2. **(Required by the brief) `idle_efficiency_percent` never exceeds 120% for any region, under any Memory Dust unlock combination, at any account state.** Verified by re-running `region-mastery-automation-system`'s own Acceptance Criterion 1 (the full band-sweep test) with every Memory Dust unlock purchased simultaneously; the result must be byte-identical to that test run with zero unlocks purchased, since AC1 above guarantees no unlock touches any input to that formula.
3. **(Required by the brief) A simulated 100-hour unattended Stage-3 session, with every Forge Upgrade unlock purchased (`vow_binding_cost = 150` Gleam + 4 materials, `sell_conversion_rate = 20`), never realizes more than 2,400 Gleam account-wide, and affords no more than 16 Vow-bindings.** Verified by re-running `region-mastery-automation-system`'s own Acceptance Criterion 4 (the 100-hour Stage-3 simulation) with this document's Forge unlocks applied on top; asserts the realized-Gleam ceiling is unchanged from that test's original result (§4 Formula 4).
4. **(Required by the brief) The game is fully completable — every region reachable to Optimized, every Forge operation available, the base evolution tree and all 10 starting Vows fully functional — with zero Memory Dust ever earned or spent.** Verified by an integration test that disables this system's milestone-crediting entirely (simulating a player who never triggers it) and runs the full acceptance-criteria suites of `region-mastery-automation-system`, `the-forge-system`, `creature-jobs-evolution-system`, and `resonance-weaving-system` unmodified; all must pass identically to their own baseline runs.
5. **(Required by the brief) No creature, region, or item — Vow-bound or otherwise — is ever deleted, reverted, or reset by any code path in this system, with or without player consent.** Verified by a fuzz/property test that purchases every possible unlock combination, in every possible order, against a populated fixture save (mastered regions at every level, creatures at every evolution stage, items with multi-entry `vow_binding_log`s) and asserts zero mutation to any `CreatureInstance`, `Item Instance`, or region record outside the exact fields §3.3's unlocks are defined to touch (`region_team_slot_count`, `rmp_threshold_1/2/3`, starting `region_mastery_points` for **future** transitions only, `vow_binding_cost`, `sell_conversion_rate`, and new content additions).
6. Formula 1's worked example reproduces exactly: a complete 6-role team reaching Optimized via `region-mastery-automation-system`'s own Formula 1/2 worked example earns Memory Dust in the sequence `+3` (immediate), `+7` (≈26 min), `+20` (≈1.72 hr), `+50` (≈4.30 hr), for a running total of exactly `80`.
7. Formula 2's sequential-gating rule is enforced: purchasing any rank-2-or-higher unlock before its rank-1 prerequisite is rejected with zero Dust deducted and zero state change — verified by an explicit out-of-order purchase attempt against every multi-rank unlock line in §3.3.
8. Formula 5's safety margin holds at the maximum combination of Starting Bonus and Automation Rules unlocks: a freshly-transitioned region's banked RMP (`250` at max) never equals or exceeds the minimum reachable `rmp_threshold_1` (`400` at max reduction) — verified by a direct numeric assertion, not just the worked example.
9. A save file predating this system's existence, loaded for the first time after it ships, retroactively credits every `(region, milestone_event)` pair the save's regions have already legitimately reached, exactly once each, with no double-crediting on any subsequent load — verified by loading the same pre-existing fixture twice in sequence and asserting `lifetime_memory_dust_earned` is identical after both loads.
10. Every unlock purchase is permanent: no code path in this system ever decrements `unlocks_owned`, refunds `lifetime_memory_dust_spent`, or restores a pre-purchase state — verified by attempting to call any such operation (if one exists in the implementation) and asserting rejection, or by static analysis confirming no such operation is exposed at all.

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly, per this task's explicit file-discipline instruction. The following are proposed for registration by whichever process coordinates registry writes this session:

**Formulas**:
- `memory_dust_accrual` — `design/gdd/memory-dust-prestige-system.md` §4 Formula 1. Consumes `region-mastery-automation-system`'s registered mastery-level transition events.
- `memory_dust_unlock_purchase_rule` — §4 Formula 2.
- `idle_efficiency_ceiling_invariance_proof` — §4 Formula 3. Not a runtime formula; a structural, enumerable proof over this document's own unlock catalog — flagged for `/gate-check`/`/review-all-gdds` to re-verify any time either this document or `region-mastery-automation-system`'s Tuning Knobs table changes.
- `stage3_gleam_cap_invariance_proof` — §4 Formula 4. Same nature as above — the direct continuation of `region-mastery-automation-system` Formula 7's own numeric proof, re-verified here under this document's added unlocks.
- `starting_bonus_safety_margin` — §4 Formula 5.

**Constants**:
- `milestone_dust_value` table (`3 / 7 / 20 / 50` per mastery-level transition) — §7. Primary pacing lever, flagged for Vertical-Slice-era playtesting once Full Vision content is scheduled.
- Total unlock tree cost (`660` Memory Dust across `19` unlocks) — §3.4. **The finite-completability contract** — any future addition of a 20th unlock must re-verify §3.4's supply-vs-cost worked example.
- `banked_rmp_head_start_max` (`250`) — §7. **BALANCE-CRITICAL relative to `region-mastery-automation-system`'s `rmp_threshold_1`** — must stay strictly below that value's minimum reachable state; re-verify §4 Formula 5 on any change to either value.

**Items proposed as new registry entries** (first appearance in this document, cross-referenced by six other GDDs):
- `Memory Dust` — the currency itself. `category: prestige_currency`, no `value_gold` (never sellable, never a Forge input — a pure meta-progression resource, structurally excluded from the Forge's closed-sink item economy per this document's A1/A4).

**Not proposed for registration** (deliberately): the specific unlock names/flavor text (Wider Roster, Attuned Ascension, Veteran's Head Start, Hunter's Cache, Verdant Communion, Second Charm, Deeper Roots, Practiced Hand, Appraiser's Instinct, Eleventh/Twelfth/Thirteenth Vow) — these are this document's own content catalog, matching the restraint `item-data-schema.md` and `the-forge-system.md` both established for not pre-registering every piece of authored content, only the cross-system facts other documents must agree with.
