# Combat Encounter System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | game-designer |
| **Status** | Complete — all 8 sections authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Every ambiguity is resolved with an explicit, flagged design call, never a placeholder. Validate at `/design-review` before Production. |
| **Priority / Tier** | MVP — Feature layer (`design/gdd/systems-index.md` #10). **The single most load-bearing system in the project.** Flagged in `systems-index.md`'s High-Risk Systems table as a DESIGN risk: "the core hunt loop's fun-factor is unproven — the concept prototype was explicitly skipped." Seven systems depend on this document. |
| **Depends On** | `design/gdd/creature-data-schema.md`, `design/gdd/animation-rig-system.md`, `design/gdd/input-targeting-system.md`, `design/gdd/creature-ai-telegraph-system.md`, `design/gdd/resonance-weaving-system.md`, `design/gdd/item-data-schema.md` |
| **Depended On By** | `vow-condition-tracking`, `loot-drop-system`, `region-mastery-automation-system`, `rare-creature-capture-system`, `combat-hud`, `audio-system`, `onboarding-tutorial-system` (none yet written) |

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **Basic attacks auto-fire on a strict fixed timer (`basic_attack_interval_ms`) against whatever `selected_part_id` is at the instant of the tick — clicking/cycling never adds an attack, only steers the next one.** Ability casting, by contrast, is instant-on-confirm. | game-concept.md's "automatically or through repeated clicks" is genuinely ambiguous and the task brief explicitly demands the tension be resolved. A model where clicking added attacks would make DPS a function of click rate — an APM test, which Pillar 1 (Precision Over Reflexes) explicitly forbids. A pure timer with click-only-steers-aim keeps the "clicker's immediacy" feel (your next hit visibly goes where you just aimed) while making the DPS cap mathematically provable regardless of input rate (§4 Formula 1, §8 AC2). Abilities remain the actual home of "repeated inputs matter" — their timing against vulnerability windows, not their cast frequency, is the skill test. |
| A2 | Resonance is **one shared pool**, not split per-Source. | Resolves art-bible's own flagged Open Item ("Is Resonance one pool or split per-Source? — owner: systems-designer"). Art-bible §7.5's Combat HUD spec already describes exactly one Resonance meter, rendered in "chrome-neutral value, not a Source hue" — a single pool is the only reading consistent with that HUD spec, so this assumption formalizes what the HUD document already implied rather than introducing a new decision. |
| A3 | Resonance is gained **only** from skilled actions (landing hits, successful defense, part-breaks, phase entries) and is **not** spent by, nor does it decay from, the passage of time. It resets to 0 at encounter end. | Mirrors creature-ai-telegraph-system's own locked rule (A6 there: vulnerability windows open only from skilled play, never a timer) applied to the player's resource loop for internal consistency. A time-based decay would introduce artificial urgency pressure — a reflex/anxiety lever, not a precision one — directly against Pillar 1. Resetting at encounter end (rather than carrying over) is what makes "decide when to spend it" a real, per-encounter decision rather than a background number that can always be banked indefinitely (task brief's explicit ask for "real tension"). |
| A4 | The Ultimate slot is gated by **both** its own short cooldown (`ultimate_cooldown_ms`) **and** a Resonance cost (`ultimate_resonance_cost`, default = the full cap) — the 3 regular ability slots are gated by cooldown only, never by Resonance. | art-bible §7.5's HUD table lists "3 ability cooldowns" and "Ultimate cooldown" as the same pip-track device, but lists "Resonance" as a wholly separate meter — meaning Resonance must gate something distinct from the three regular slots. game-concept.md's phrasing ("times 3 equipped abilities + 1 ultimate... against accumulated Resonance energy") ties Resonance specifically to ultimate timing. Gating the Ultimate by both keeps the HUD's two-pip-track-plus-one-meter layout accurate without inventing a new HUD element. |
| A5 | **Dodge is a charge-based resource** (`dodge_charge_max`, regenerating over `dodge_charge_regen_ms` per charge), not a directional (left/right) or free-roam mechanic. | The task brief explicitly asks what "limited" means concretely and offers charges or a positional binary as options. A directional dodge would require a new field on creature-ai-telegraph-system's `AttackDefinition` (a per-attack required dodge side) that this document has no authority to add under its file-discipline constraint (write only this file). A charge system is buildable entirely from data this document owns, delivers the same "you cannot spam your way through danger" limitation the brief asks for, and stays fully consistent with the Anti-Pillar rejecting free-roam mechanics. Directional dodge is flagged in §7 as a Full Vision extension point, not built here. |
| A6 | **Block reduces damage by a flat `block_damage_reduction_percent` (default 80%), it does not fully negate it.** Dodge and Interrupt fully negate. | creature-ai-telegraph-system locks the reward gradient interrupt > dodge > block (its A5) but leaves the exact mechanical shape of "block" to this document. A block that fully negates damage exactly like dodge, but with fewer restrictions (no charge limit) and no vulnerability-window reward, would make block strictly *better* than dodge in every case that doesn't need the reward — a dominant-strategy risk. Leaving genuine (if reduced) chip damage on block preserves a real reason to spend a dodge charge instead of always blocking. |
| A7 | Combat-encounter-system introduces two small, **foreign-keyed extension tables it owns**: `AttackDamageProfile` (keyed by `attack_id`, supplying the damage value creature-ai-telegraph-system's `AttackDefinition` deliberately omits) and `HazardDefinition` (keyed by `hazard_summon_tag`, giving that stored-only tag concrete behavior). | creature-ai-telegraph-system's `AttackDefinition` has no damage field at all (by design — that document explicitly scopes out "damage formulas... entirely combat-encounter-system's job"), and its `hazard_summon_tag` is explicitly "stored only, not interpreted here." Something has to own these values; per the project's established pattern (resonance-weaving-system's `WovenAbilityDefinition` referencing item-data-schema's `definition_id` as a foreign key, rather than editing that schema), this document adds its own small reference tables instead of modifying a file it doesn't own. |
| A8 | This document **resolves item-data-schema's and resonance-weaving-system's shared open item**: the authoritative Hunter combat stat catalog is `attack_power`, `focus`, `vitality`, `engineering`, `guile`, `resonance_affinity` (the six Source-scaling stats, exactly as resonance-weaving-system §3.1 proposed them) plus `max_health`, `defense`, `critical_chance`. | Both item-data-schema (§3.6, `Modifier.stat`) and resonance-weaving-system (§4.3, `scaling_stat_value`) explicitly named `combat-encounter-system` as the system that owns this catalog and deferred to it. Confirming it here — using the exact same six Source-scaling names already proposed — means neither document needs to change; this document simply closes the loop. |
| A9 | **Active Efficiency Percent is measured relative to a fixed, authored anchor** — `par_clear_time_seconds`, an opaque input owned by `encounter-spawn-system` as a field on the encounter template. It is **never** measured against automation's live throughput. | **This assumption was rewritten on 2026-07-14 to close Blocker B1.** It originally anchored to `automation_baseline_clear_time_seconds` — a value `region-mastery-automation-system` *derives from live mastery*, and which therefore **rises as the player's automation improves**. That made the active player's score fall as their own team got better, inverting Pillar 3 (see §4 Formula 7, "Why the anchor must be fixed"). The anchor must be a constant the player cannot move. `encounter-spawn-system` owns it because par is a property of *the encounter*, not of the player's progress. |
| A10 | A creature attack that goes fully unavoided deals its `AttackDamageProfile` damage to the player in full; a **successful capture attempt is not designed by this document** — only the hook (`AttemptCapture`, §3.9) that `rare-creature-capture-system` (Vertical Slice tier) will attach its own success/failure roll to. | `rare-creature-capture-system` is explicitly Vertical Slice tier; this document must not pre-empt its design, per the same non-overlap discipline resonance-weaving-system used for `vow-condition-tracking`. This document defines only what happens to the *encounter state machine* on a capture success (§3.2) and what data (`capture_favorability_score`, §4 Formula 6) it exposes for that system to consume. |

---

---

## 1. Overview

The combat encounter system is Resonance Hunter's **resolution layer** — the system that takes
every mechanic the other combat documents define in isolation (a creature's parts and health from
`creature-data-schema`, its rig and clips from `animation-rig-system`, the player's target
selection from `input-targeting-system`, its telegraph timing and vulnerability windows from
`creature-ai-telegraph-system`, its abilities and Vows from `resonance-weaving-system`, its gear
modifiers from `item-data-schema`) and actually runs the fight: it owns the encounter's start-to-
finish state machine, the full damage pipeline from a confirmed input to an applied number, the
Resonance economy, the defensive triad's player-facing mechanics (block/dodge/interrupt), the
player's own health and fail state, and — because every other system in the project balances
against it — the formal, measurable contract between active and automated play. Nothing here
redefines a rule another document already owns (telegraph timing stays telegraph-owned, Vow power
curves stay Weaving-owned, part schemas stay schema-owned); this document is where those rules are
*combined* into one number, one state transition, one encounter. If this system's core loop is not
fun, no other system in the game can compensate for it — this is the document the entire hunt
pillar either becomes real in, or doesn't.

## 2. Player Fantasy

> **Predatory clarity: the calm of a hunter who has already read the pattern.** Not the panic of
> someone reacting blind — the quiet confidence of someone who saw the wind-up begin, already knew
> what was coming, and had already decided what to do about it.

This is the exact phrase art-bible §2 and `creature-ai-telegraph-system` §2 use for the mood this
whole hunt loop must deliver, and this document is where that promise is either kept or spent —
every other document produced a *rule*; this one produces the *moment*. Concretely, this system is
what guarantees:

- **Every action the player takes is legible the instant it resolves.** A click lands, a number
  appears, a part visibly cracks further, a Resonance wedge fills — there is no invisible math
  between "I did the right thing" and "I can see that it worked." This is the direct combat-loop
  expression of the **Competence** need (Self-Determination Theory): skill growth is only real to
  the player if the feedback proving that growth is instant and unambiguous.
- **The burst is the payoff, not the grind.** Basic attacks provide a steady, unglamorous floor —
  the game never asks the player to *earn* the right to act. But the moments the player actually
  remembers are short, decisive bursts: an interrupt landed with a heartbeat to spare, an ability
  held back through two attacks and finally dropped into a vulnerability window, an ultimate spent
  at the exact instant it turns a fight. This is game-concept.md's own framing verbatim — "a
  clicker's immediacy and a boss-rush game's readable attack patterns" — and this system is the
  only place in the project where that promise becomes an actual, testable sequence of events.
- **Choosing the safe option is a real choice, not a wasted one.** Blocking, when interrupting was
  possible, should feel like a legitimate, sometimes-correct call under pressure — not a mistake
  the player made because they didn't know better. The risk/reward gradient (interrupt > dodge >
  block) is felt, not just documented: bigger risk, bigger Resonance, bigger reward, every time,
  consistently enough that the player's own read of "can I land this interrupt" becomes a skill
  worth developing (**Autonomy** — every encounter offers a genuinely different-feeling correct
  answer depending on what the player can pull off in the moment).
- **Losing costs almost nothing except the lesson.** A fight going badly should teach, not punish —
  the player walks away from a failed encounter having learned the creature's pattern, not having
  lost hours of progress. This is what makes "try again" feel like curiosity, not dread.
- **Active play visibly, measurably outperforms walking away — and the player can feel why.**
  Every system in this document that rewards precision (vulnerability multipliers, interrupt
  bonuses, part-break-driven loot, capture favorability) exists to make the active/idle contract
  (§4 Formula 7) a felt truth during play, not a hidden spreadsheet fact the player has to be told
  about.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the encounter state machine, the full damage pipeline (basic attack and
ability, creature-to-player and player-to-creature), the Resonance economy, the player-facing
mechanics of block/dodge/interrupt (inputs, costs, cooldowns), player health and the fail state,
encounter end and reward hand-off, and the active-vs-idle efficiency contract. It does **not**
own, and explicitly defers to the cited document in every case: which part is targeted
(`input-targeting-system`), when an attack telegraphs and when its avoidance windows open
(`creature-ai-telegraph-system`), what a Vow's condition means or how ability power is derived
from Source/Form/Vow (`resonance-weaving-system`), a creature's parts/health/break-thresholds
(`creature-data-schema`), what clip is playing (`animation-rig-system`), an item's stats/rarity/
enchantments (`item-data-schema`), loot table contents (`loot-drop-system`),
capture success chance (`rare-creature-capture-system`), and the automated
baseline this system's own efficiency formula is measured against (`region-mastery-automation-
system`).

### 3.1 Hunter Stat Catalog (Resolves item-data-schema / resonance-weaving-system Open Item)

Per A8, this document is the authoritative owner of the Hunter combat stat catalog both of those
documents deferred to it. Nine stats, all open string keys matching item-data-schema §3.6's
`Modifier.stat` convention exactly:

| Stat | Source (resonance-weaving §3.1) | Role |
|---|---|---|
| `attack_power` | `body` | Scales `body`-Source ability power (Formula 3, resonance-weaving) and basic attack damage (§4 Formula 1 here). |
| `focus` | `mind` | Scales `mind`-Source ability power. |
| `vitality` | `nature` | Scales `nature`-Source ability power. |
| `engineering` | `machine` | Scales `machine`-Source ability power. |
| `guile` | `shadow` | Scales `shadow`-Source ability power. |
| `resonance_affinity` | `spirit` | Scales `spirit`-Source ability power **and** all Resonance gain amounts (§3.6, §4 Formula 5). |
| `max_health` | — | Player HP pool (§3.8). |
| `defense` | — | Mitigates incoming creature/hazard damage (**§4 Formula 3b Step 4**, its sole consumer in the project). Base **0**, +2 per Training rank to a cap of **120** — written by `hunter-progression-system`. Never reduced by a Vow: `vow_fragility` applies its penalty to Formula 3b's *output* (Step 5) precisely so that a 0-defense build cannot dodge the cost. |
| `critical_chance` | — | Probability any single damage-dealing event critically strikes (§4 Formula 2). |

Every stat's **effective value** is `hunter_base_[stat] + Σ(modifier.value for every equipped
item's `modifiers` where `modifier.stat == "[stat]"` and `modifier.value_type == "flat"`)`
(item-data-schema §3.6). `hunter_base_[stat]` values themselves (base Hunter progression before
gear) are external inputs — owned by whichever future progression system levels the Hunter; this
document treats them as opaque non-negative floats, matching resonance-weaving §4.3's own stance
on `scaling_stat_value`.

### 3.2 Encounter State Machine

Four states, strictly ordered, no state is ever re-entered once left:

```
NOT_STARTED → ENGAGING → ACTIVE → RESOLVING → COMPLETE
```

| State | Duration | What Happens | Player Input Accepted? |
|---|---|---|---|
| `NOT_STARTED` | — | The encounter has not begun; used only as the pre-transition marker for tooling/tests. | No |
| `ENGAGING` | Fixed, `engaging_duration_ms` (default 1500ms, §7) | Creature spawn-in beat. `CreatureBehaviorProfile.default_phase_id` is resolved, `hostile_state` is initialized (full health, all parts intact, `power_tier` clamped per `creature-data-schema` Edge Case #5), Resonance resets to 0, player HP carries in at whatever it currently is (§3.8). No telegraph may begin, no damage may be exchanged in either direction. | No — a deliberate, short "the fight hasn't started yet" beat, not a combat-relevant window. |
| `ACTIVE` | Until an end condition fires (§3.9) | The main loop. All systems below (§3.3–§3.10) run concurrently, every frame, for the duration of this state. | Yes — full input surface (targeting, basic-attack aiming is passive per A1, ability/ultimate activation, block/dodge/interrupt, manual retreat). |
| `RESOLVING` | Fixed per outcome (§3.9), 800–3000ms | The outcome cinematic plays (Mastery Transition for a boss kill per art-bible §2, a shorter defeat/retreat beat otherwise, or the capture-hook's own transition). No new player actions begin (§5 Edge Case #2), but any action already fully committed before `RESOLVING` began finishes resolving. | No new actions; in-flight actions only. |
| `COMPLETE` | — | Terminal. `hostile_state` (if the instance still exists as `hostile`) and all of this system's own runtime state are torn down per `creature-data-schema` Edge Case #3 / `creature-ai-telegraph-system` Edge Case #8. Rewards are handed to `loot-drop-system`/`rare-creature-capture-system`; control returns to the region hub. | No |

**Within `ACTIVE`, nothing is a sub-state — it is a set of concurrently-ticking systems, not a
sequential machine**, because the brief's "telegraph → response → vulnerability → burst" loop
happens continuously and overlappingly (a new telegraph can begin while a previous vulnerability
window is still open, per `creature-ai-telegraph-system` Formula 4 Part C's composition rule).
Every frame `ACTIVE` is live, in this order:

1. **Creature Behavior** (`creature-ai-telegraph-system`, entirely that system's own logic) — advances any in-progress telegraph, evaluates `Phase` transitions (its Formula 5) after any `hostile_state` mutation from step 5, and requests a new attack (its Formula 6) at each decision point.
2. **Basic Attack Timer** (§3.3) — ticks toward its next scheduled fire.
3. **Player Input Resolution** — ability/ultimate activation attempts (§3.4), block hold-state, dodge press, and any qualifying-ability interrupt cast (§3.5), all evaluated against `creature-ai-telegraph-system` Formula 3's window boundaries for whichever telegraph is currently in progress, if any.
4. **Creature Attack Resolution** — if the current telegraph's `elapsed_ms` reaches `windup_duration_ms` this frame and it was not successfully avoided in step 3, it fires (§3.10).
5. **Damage/Health/Part-State Application** — every damage-dealing event resolved this frame (basic attack, ability, creature attack, hazard tick) is applied via the full pipeline (§4), in the order those events occurred within the frame; part-break accumulation and Resonance gain are applied in the same step as the triggering damage event, never deferred to a later frame.
6. **Encounter-End Check** — run unconditionally after step 5, every frame, checking every condition in §3.9 in the fixed priority order specified there.

### 3.3 Basic Attacks (Auto-Fire, Aim-Only — A1)

1. From the instant `ACTIVE` begins, a basic-attack timer runs continuously: it fires exactly once
   every `basic_attack_interval_ms` (default 1200ms, §7), with no player input required to sustain
   it and no way to speed it up.
2. At the instant of firing, the attack resolves against whichever `part_id` is currently
   `input-targeting-system`'s `selected_part_id` (that system's exact field, §3.1 there). If
   `selected_part_id` is `null` at that instant, the tick fires against the fallback target (§5
   Edge Case #1) instead of being skipped — a basic attack is never simply lost to an empty
   selection.
3. **Clicking or cycling never adds, removes, or reschedules a tick.** It only ever changes which
   `part_id` the *next* scheduled tick will resolve against, exactly as fast as
   `input-targeting-system` already updates `selected_part_id` (continuously for mouse hover,
   per-cycle-input for gamepad/keyboard). This is the entire resolution of A1: the timer's rate is
   fixed and provable (§4 Formula 1, §8 AC2); only aim is player-controlled.
4. Basic attack damage is computed by §4 Formula 1 and applied through the full damage pipeline
   (§4 Formula 3).

### 3.4 Abilities and the Ultimate

1. The loadout is exactly the 3 ability slots + 1 ultimate slot locked by resonance-weaving-system
   §3.5 — this document does not redefine loadout rules, only how a slot's ability actually fires
   in combat.
2. **Activating one of the 3 ability slots**: valid whenever that slot's `WovenAbilityDefinition`-
   backed ability is off cooldown (`AttackDefinition`-style per-ability `cooldown_ms`, authored
   per ability, distinct from a creature's own `cooldown_ms`) **and**, for a `gate`-mode Vow-bound
   ability, its condition currently reads true (`vow-condition-tracking`'s live signal,
   resonance-weaving §3.3.2). Firing is instant on confirm — no queueing, no wind-up owned by this
   document (a Form's own cast/activation feel is `animation-rig-system`'s clip, not a delay this
   system adds).
3. **Activating the Ultimate slot**: valid whenever, additionally, `current_resonance ≥
   ultimate_resonance_cost` (A4) **and** the Ultimate's own `ultimate_cooldown_ms` has elapsed
   since its last use. On a successful activation, `ultimate_resonance_cost` Resonance is deducted
   immediately (§3.6).
4. **Interrupt qualification**: whether a specific ability "counts as" landing an interrupt (per
   `creature-ai-telegraph-system` A4) is a flag resonance-weaving-system's `WovenAbilityDefinition`
   should expose (this document does not define which abilities qualify — that is content
   authored against that schema). When a qualifying ability's cast resolves at the instant it
   lands within the active telegraph's interrupt window (`creature-ai-telegraph-system` Formula 3),
   this document treats it as a successful interrupt for all purposes in §3.5 and §3.6 — whether or
   not that same cast also deals damage through the normal ability pipeline (both apply; interrupt
   and damage are not exclusive outcomes of the same cast).
5. Every ability and ultimate cast resolves damage through the full ability pipeline (resonance-
   weaving §4.1–4.7, continued by this document's §4 Formula 3), snapshotting any bound Vow's
   `power_multiplier` at the exact instant of cast (resonance-weaving §5 Edge Case #3 — restated,
   not redefined, here: this document is the system that actually performs that snapshot read).

### 3.5 The Defensive Triad — Block / Dodge / Interrupt

`creature-ai-telegraph-system` owns the *windows* (Formula 3) and the *reward gradient* (interrupt
> dodge > block, its A5). This document owns the player-facing mechanics:

| | Input | Cost | Limit | On Success |
|---|---|---|---|---|
| **Block** | Hold a dedicated guard input continuously through the attack's block window (`creature-ai-telegraph-system` §3.13 — the *entire* span, including the cutoff frame) | None — always available | None — no charge, no cooldown | Damage reduced by `block_damage_reduction_percent` (default 80%, A6). No vulnerability window. Small Resonance gain (§3.6). |
| **Dodge** | A single discrete press landing inside the attack's dodge window | 1 dodge charge | `dodge_charge_max` (default 2) charges, each regenerating independently after `dodge_charge_regen_ms` (default 4000ms) of not being spent (A5) | Damage fully negated. Opens a vulnerability window (`creature-ai-telegraph-system` Formula 4, dodge branch). Medium Resonance gain. |
| **Interrupt** | Landing a qualifying ability cast inside the attack's interrupt window (§3.4.4) | The ability's own cooldown (no separate resource) | Bounded only by ability cooldown availability | Damage fully negated, the telegraph is hard-cancelled (`creature-ai-telegraph-system` Edge Case #2). Opens the longest vulnerability window with the exclusive `interrupt_break_progress_multiplier` bonus (§3.7). Large Resonance gain. |

**Why a player ever chooses block over dodge**: dodge is a limited resource (A5) — spending it on
a low-value attack leaves the player exposed later in the same encounter, while block is always
available at the cost of chip damage and zero reward. This is the concrete mechanical reason the
"safe option" (art-bible's Player Fantasy framing) is a genuine tactical call, not a fallback for
players who failed to read the attack.

**Simultaneous dodge + interrupt** (both an input landing inside their respective windows against
the same telegraph): resolved per §5 Edge Case #4 — the interrupt's reward branch applies and the
dodge charge is refunded, never consumed, since the attack was already fully avoided by the
interrupt.

### 3.6 Resonance Energy

A single shared meter (A2), `current_resonance ∈ [0, resonance_cap]` (default cap 100).

**Gain** (never from time, always from a discrete skilled action, per A3 — each amount is scaled
by `(1 + resonance_affinity_scaling_coefficient × hunter_resonance_affinity)`, §4 Formula 5):

| Trigger | Base Gain |
|---|---|
| Basic attack lands | `resonance_per_basic_hit` (default 1) |
| Ability hit lands | `resonance_per_ability_hit` (default 2) |
| Successful block | `resonance_per_block` (default 3) |
| Successful dodge | `resonance_per_dodge` (default 8) |
| Successful interrupt | `resonance_per_interrupt` (default 15) |
| A `Phase` entry fires (guaranteed vulnerability window opens) | `resonance_per_phase_entry` (default 6) |
| Any part reaches `broken` | `resonance_per_part_break` (default 10) |

**Spend**: only the Ultimate (§3.4.3), which deducts `ultimate_resonance_cost` (default = the full
cap, 100) the instant it activates.

**No decay.** Resonance is never reduced except by spending it. It resets to 0 the instant
`ENGAGING` begins for the next encounter (§3.2) — unspent Resonance never carries between
encounters (§5 Edge Case #5).

### 3.7 Part-Break Damage Accumulation

For every damage-dealing event that resolves against a specific `part_id` (basic attack, ability
hit — never a creature attack or hazard, which never target creature parts):

1. Read that part's `PartDefinition.break_threshold_type` (`creature-data-schema` §3.3).
2. If `cumulative_damage`: add the event's **final, post-mitigation damage value** (§4 Formula 3's
   output) to that part's `PartState.accumulated_value`.
3. If `hit_count`: add exactly `1` if the event's final damage value is `> 0` (this resolves
   `creature-data-schema` Edge Case #6's open question: a "hit," for this schema's purposes, is any
   damage-dealing event from this document's pipeline that resolves to a nonzero post-mitigation
   value against that part — a DoT tick, if this game ever authors one, would count identically,
   since nothing here distinguishes damage sources once they reach this step).
4. **Interrupt bonus**: if the part currently has an open vulnerability window whose trigger was a
   successful interrupt (`creature-ai-telegraph-system` §3.12), the amount added in steps 2–3 is
   first multiplied by `interrupt_break_progress_multiplier` (that document's Formula 4, default
   2.0) — this bonus applies to the accumulation added to `PartState.accumulated_value` only, never
   to the health damage applied in the same event.
5. `PartState.current_stage` is recomputed immediately per `creature-data-schema` Formula 3 — this
   document never writes `current_stage` directly, only `accumulated_value`, matching that
   schema's own "never authored directly" rule.
6. If this write causes `accumulated_value ≥ effective_break_threshold` for the first time, the
   part transitions to `broken` in the same step, `resonance_per_part_break` fires (§3.6), the
   part's `loot_modifier_tag`/`fight_state_change_tag` are handed to `loot-drop-system`/
   `creature-ai-telegraph-system` respectively (both already own how they interpret those tags),
   and — if this was the `is_core` part — the encounter-end check (§3.9, priority 1) fires
   immediately, overriding every other rule in this document (`creature-data-schema` Edge Case #2).

### 3.8 Player Health and the Fail State

- `player_current_hp ∈ [0, hunter_max_health]` (§3.1). Carries over between encounters within the
  same hub visit — it is **not** reset to full at the start of every `ENGAGING` (§3.2); it is
  restored to full only on returning to the region hub after an encounter ends, or explicitly by a
  future recovery item/mechanic outside this document's scope.
- Damage is applied by §4 Formula 3's mitigation step whenever a creature attack (§3.10) or hazard
  tick (§3.11) resolves unavoided or partially-avoided (blocked).
- **Fail state — Retreat, not death.** The instant `player_current_hp` reaches 0, the encounter
  immediately transitions to `RESOLVING` with the **Retreat** outcome (§3.9): the creature is
  **not** killed and **not** captured — it remains exactly as damaged/phased as it was, fully
  `hostile`, for the *next* time the player engages it (a fresh `hostile_state` is still
  initialized at that next `ENGAGING`, per §3.2 — no persistent wound-state carries between
  separate encounter instances, since `hostile_state` does not persist past `COMPLETE` per
  `creature-data-schema` Edge Case #3). No items, currency, Vow-bound gear, or roster creatures are
  ever lost on a Retreat. The player is returned to the region hub with `player_current_hp` set to
  1 (not 0 — see §5 Edge Case #7) and full HP is restored on the next hub visit's recovery tick,
  same as any other encounter end. This is the direct mechanical expression of game-concept.md's
  "failure costs little time" and "educational, not punishing" framing (task brief).

### 3.9 Encounter End, Capture Hook, and Rewards

Checked every frame (§3.2 step 6), in this fixed priority order — the first condition met wins,
even if multiple would technically be true in the same frame:

| Priority | Condition | Outcome | `RESOLVING` Duration |
|---|---|---|---|
| 1 | The `is_core` part reaches `broken` (`creature-data-schema` Edge Case #2) | **Kill** | 3000ms (boss) / 1500ms (standard) — the Mastery Transition (art-bible §2) for a boss, a shorter defeat beat otherwise |
| 2 | `hostile_state.current_health` reaches 0 via non-core-break damage | **Kill** | Same as above |
| 3 | A pending `AttemptCapture` (below) resolves successful | **Capture** | 2000ms — hands off to whatever transition `rare-creature-capture-system` specifies when authored; until then, a placeholder beat identical in shape to Kill's, per §5 Edge Case #9 |
| 4 | `player_current_hp` reaches 0 | **Retreat** (§3.8) | 1000ms |
| 5 | The player issues a manual Retreat input (available at any time during `ACTIVE`, no cost beyond forfeiting this encounter's rewards) | **Retreat** | 500ms |

**Kill** rewards: this document fires a `CreatureDefeated` event — `{instance_id, template_id,
broken_part_ids, power_tier}` — consumed entirely by `loot-drop-system`. This
document also computes and attaches `part_break_loot_bonus_percent` (§4 Formula 4) to that event —
the mechanism behind the task brief's "better part-break rewards" requirement.

**Capture hook** (A10): `AttemptCapture` is an action the player may issue during `ACTIVE`
whenever `rare-creature-capture-system`'s own eligibility flag (a field that system will add,
not this document) is true on the target instance. This document's only responsibility is: (a)
expose `capture_favorability_score` (§4 Formula 6) as a running per-encounter value that system may
read at the moment of the attempt, and (b) if that system reports success, transition immediately
to `RESOLVING`/**Capture** instead of continuing the fight — an atomic `hostile → bound`
transition per `creature-data-schema` Edge Case #3. **If it reports failure, the encounter simply
continues** — per game-concept.md, "a botched capture attempt simply becomes a kill instead of a
hard fail state," meaning a failed attempt has no penalty of its own; the fight proceeds normally
and may still end in Kill, Retreat, or a later successful Capture attempt.

**Retreat** rewards: none. See §3.8.

### 3.10 Creature Attacks Against the Player

When a telegraphed attack's `elapsed_ms` reaches `windup_duration_ms` unavoided (§3.2 step 4):

1. Look up that `attack_id`'s `AttackDamageProfile` (§3.11, A7) for `raw_damage_base` and the
   creature's current `base_stats.base_attack_power` (`creature-data-schema` §3.2).
2. **Resolve the hit through §4 Formula 3b — The Full Damage Pipeline (Creature-to-Player).** That
   pipeline handles all four defensive outcomes internally at its Step 3 (dodge → 0, interrupt → 0,
   block → `× (1 − block_damage_reduction_percent)`, unavoided → full), then mitigates against the
   **Hunter's** `defense` stat.
   > **Do not route incoming damage through Formula 3.** Formula 3 is the *player-to-creature*
   > pipeline: it mitigates using the **creature's** `base_defense`. Sending incoming attacks through
   > it — which this section originally did — means an attack is reduced by the *attacker's own*
   > armor, and the Hunter's `defense` stat is never read at all. That was **Blocker B3**.
3. Formula 3b's `final_damage` reduces `player_current_hp` (floor 0) in the same frame. Reaching 0
   triggers **RETREAT** (§3.2), never death.
4. If `AttackDefinition.hazard_summon_tag` is non-null, look up its `HazardDefinition` (§3.11) and
   spawn it — hazards persist and tick independently of the attack that summoned them (below).

**Hazards**: once spawned, a hazard ticks every `hazard_tick_interval_ms` (per its
`HazardDefinition`), dealing `hazard_damage_per_tick` to the player through **§4 Formula 3b** (Step 0
takes `hazard_damage_per_tick` as its `raw_damage_base`; only the dodge branch of Step 3 is reachable,
per the rule below), for `hazard_duration_seconds` or until the encounter ends, whichever is first. A hazard
tick is avoidable only by a dodge input landing within `hazard_dodge_grace_ms` before the tick
(block and interrupt do not apply to hazards — they are not telegraphed creature attacks in the
sense `creature-ai-telegraph-system` defines, they are persistent field effects). Hazards never
carry over between encounters (`resonance-weaving-system` §5 Edge Case #9 sets this same precedent
for an armed, unresolved Trap; this document applies the identical rule to hazards for consistency).

### 3.11 New Extension Tables Owned by This Document (A7)

**`AttackDamageProfile`** — one entry per `attack_id` in a `CreatureBehaviorProfile.attacks` map
(`creature-ai-telegraph-system` §3.3):

| Field | Type | Description |
|---|---|---|
| `attack_id` | string | Foreign key into `CreatureBehaviorProfile.attacks` (`creature-ai-telegraph-system` §3.6). |
| `damage_multiplier_of_base_attack_power` | float, > 0 | `raw_damage_base = damage_multiplier_of_base_attack_power × CreatureTemplate.base_stats.base_attack_power`. Authored per attack (a heavy telegraphed attack gets a higher multiplier than a fast jab). |

**`HazardDefinition`** — one entry per `hazard_summon_tag` value referenced by any
`AttackDefinition`:

| Field | Type | Description |
|---|---|---|
| `hazard_tag` | string | Matches an `AttackDefinition.hazard_summon_tag` value. |
| `hazard_damage_per_tick` | float, ≥ 0 | Pre-mitigation damage per tick. |
| `hazard_tick_interval_ms` | int, > 0 | Time between ticks. |
| `hazard_duration_seconds` | float, > 0 | Total hazard lifetime. |
| `hazard_dodge_grace_ms` | int, ≥ 0 | Window before a tick during which a dodge press avoids that tick. |

## 4. Formulas

All formulas share `creature-data-schema`'s round-half-up convention for any value that must
resolve to an integer (damage totals, HP, Resonance); intermediate multiplier chains stay
unrounded floats until the final `final_damage` step, matching the rounding discipline every
sibling document in this project already uses.

### Formula 1 — Basic Attack Base Damage, and the DPS-Cap Proof (A1)

```
basic_attack_damage = basic_attack_base_value × (1 + basic_attack_scaling_coefficient × hunter_attack_power)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `basic_attack_base_value` | float, tuning knob | 5–25 (default 12) | Baseline damage before stat scaling. |
| `basic_attack_scaling_coefficient` | float, tuning knob | 0.003–0.02 (default 0.008 — deliberately identical to resonance-weaving's `source_scaling_coefficient`, so basic attacks and abilities scale at the same rate against the same stat) | How strongly `hunter_attack_power` feeds basic attack damage. |
| `hunter_attack_power` | float, external input | ≥ 0 | §3.1's effective `attack_power` stat. |
| `basic_attack_damage` | float | ≥ `basic_attack_base_value` | Output. Pre-mitigation, pre-crit, pre-vulnerability damage for one basic attack tick. Feeds Formula 3. |

**Worked example**: `hunter_attack_power = 120` (the same value used in resonance-weaving §4.7's
own worked example, for direct comparability), default tuning:
`basic_attack_damage = 12 × (1 + 0.008 × 120) = 12 × 1.96 = 23.52`.

**The DPS-cap proof (A1, required by §8 AC2)**: because the basic-attack timer fires on a strict
schedule independent of input events (§3.3, rule 3), the number of basic-attack ticks in any
wall-clock window `T` is exactly `⌊T ÷ basic_attack_interval_ms⌋` (± 1 for phase alignment at the
window's edges), **regardless of how many click or cycle-input events occur in that window** —
clicking changes `selected_part_id` but never inserts, removes, or reschedules a tick. Total
basic-attack DPS is therefore hard-bounded above by `basic_attack_damage_max ÷
(basic_attack_interval_ms ÷ 1000)`, a value fully independent of player input rate.

### Formula 2 — Critical Hit Roll

```
is_critical = uniform_random(0, 1) < effective_crit_chance
effective_crit_chance = clamp(hunter_critical_chance, 0, crit_chance_cap)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `hunter_critical_chance` | float, external input | ≥ 0 | §3.1's effective `critical_chance` stat (base + gear modifiers). |
| `crit_chance_cap` | float, tuning knob | 0.5–0.9 (default 0.75) | Hard ceiling — no build may reach guaranteed crits, preserving crit as variance rather than a solved stat-stack target. |
| `is_critical` | bool | — | Rolled independently for every discrete damage-dealing event (one basic attack tick, one ability hit, one Aura tick, one Summon attack, one Trap trigger). |

**Output range**: `effective_crit_chance` is always in `[0, 0.75]` at default tuning, so
`is_critical` is `true` with probability strictly less than 1 — a build can never eliminate crit
variance entirely, by construction.

### Formula 3 — The Full Damage Pipeline (Player-to-Creature)

The single most important formula in this document — every basic attack and every ability hit
resolves through this exact sequence:

```
Step 0 (source-specific): compute pre_pipeline_damage —
  basic attack: Formula 1's output
  ability:      resonance-weaving Formula 5's form_output_value (or Formula 6's
                mark_damage_taken_bonus_percent applied as a % amplifier to a
                *separate* concurrent damage source, per that document's §3.2 — Mark
                itself deals no direct pre_pipeline_damage and is excluded from this
                pipeline's steps 1-6 entirely; see §5 Edge Case #8)

Step 1: source_multiplied = pre_pipeline_damage × source_effectiveness_multiplier(ability_or_weapon_source, target_source)
         — resonance-weaving Formula 4, 6x6 table. For a basic attack, ability_or_weapon_source
           is the equipped weapon Item Instance's `source` field (item-data-schema §3.3 field 5);
           if that field is null (unaligned weapon), this step is skipped (×1.0).

Step 2: vuln_multiplied = source_multiplied × (vulnerability_damage_multiplier if target_part_id ∈ hostile_state.vulnerable_parts else 1.0)
         — creature-ai-telegraph-system Formula 4's reward multiplier, default 1.5.

Step 3: crit_multiplied = vuln_multiplied × (crit_multiplier if is_critical else 1.0)
         — Formula 2's roll, this document's own crit_multiplier (default 2.0).

Step 4: effective_defense = round(CreatureTemplate.base_stats.base_defense × (1 + tier_defense_scalar × (power_tier - 1)))
        mitigated = crit_multiplied × (100 / (100 + effective_defense))

Step 5: final_damage = max(min_damage_floor, round(mitigated))

Step 6: apply final_damage to hostile_state.current_health (floor 0);
        feed final_damage into §3.7's part-break accumulation for target_part_id.
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `tier_defense_scalar` | float, tuning knob | 0.05–0.50 (default 0.15 — matches `creature-data-schema`'s own `tier_health_scalar`/`tier_break_scalar` defaults for consistency across the three stats that scale by `power_tier`) | Controls how steeply a creature's mitigation grows per power tier. |
| `min_damage_floor` | int, tuning knob | 1–5 (default 1) | Guarantees a hit is never fully absorbed to 0, however high `effective_defense` climbs (Formula's own asymptotic-but-never-zero shape already trends this way; the floor is a defensive backstop against floating-point edge cases, not a load-bearing balance lever). |
| `final_damage` | int | ≥ `min_damage_floor` | Output. The number actually subtracted from `current_health` and fed to part-break accumulation. |

**Output range**: `final_damage` is always a positive integer ≥ `min_damage_floor` — the mitigation
term `100 / (100 + effective_defense)` asymptotically approaches but never reaches 0 as
`effective_defense → ∞`, and the floor guarantees the same even in a pathological input case.

**Worked example A — a full basic attack, click to applied damage**: `hunter_attack_power = 120`
(Formula 1 → `basic_attack_damage = 23.52`), equipped weapon `source = body`, target creature
`source = nature` → strong (`1.5`, resonance-weaving Formula 4), target part currently vulnerable
(`×1.5`), no crit this swing, target `power_tier = 3` with `base_defense = 20`
(`effective_defense = round(20 × (1 + 0.15 × 2)) = round(26) = 26`):

```
Step 0: pre_pipeline_damage = 23.52
Step 1: 23.52 × 1.5 (strong)         = 35.28
Step 2: 35.28 × 1.5 (vulnerable)     = 52.92
Step 3: 52.92 × 1.0 (no crit)        = 52.92
Step 4: mitigated = 52.92 × (100/126) = 42.0
Step 5: final_damage = max(1, round(42.0)) = 42
```

**Worked example B — continuing resonance-weaving §4.7's ability pipeline end-to-end**: that
document's own worked example ends at `268.13` (a `body`-Source `projectile`, `vow_bloodied`
active, `body` vs. `nature` strong, `attack_power = 120`) and explicitly hands that number to this
document. Continuing it here: the hit lands on a part whose vulnerability window was opened by a
successful **interrupt**, and rolls a **critical hit**, against the same `power_tier = 3`,
`base_defense = 20` target (`effective_defense = 26`):

```
Step 0: pre_pipeline_damage = 268.13   (resonance-weaving's own output — Source effectiveness
                                          is already folded in there, so this pipeline's Step 1
                                          is skipped for abilities that already applied it — see
                                          note below)
Step 2: 268.13 × 1.5 (vulnerable)    = 402.195
Step 3: 402.195 × 2.0 (crit)         = 804.39
Step 4: mitigated = 804.39 × (100/126) = 638.4
Step 5: final_damage = max(1, round(638.4)) = 638
Step 6 (part-break, §3.7): interrupt bonus applies to accumulation only:
        accumulated_value += 638 × interrupt_break_progress_multiplier (2.0) = 1276
```

**Integration note**: for abilities, resonance-weaving Formula 4 (Source effectiveness) is already
applied *inside* that document's own §4.7 pipeline before handing off `ability_effective_power`/
`form_output_value` to this document — so this pipeline's Step 1 is redundant for ability damage
and applies only to basic attacks, which have no equivalent upstream step. This document never
double-applies Source effectiveness.

### Formula 3b — The Full Damage Pipeline (Creature-to-Player)

> **Added 2026-07-14 to close Blocker B3.** §3.10 previously routed *incoming* damage through
> Formula 3 — a pipeline that mitigates using the **creature's** `base_defense` against the
> **creature's** health. The player's `defense` stat was therefore **read by nothing in the entire
> project**: it appeared in the Hunter Stat Catalog (A8) and in no formula anywhere. Every incoming
> hit was mitigated by the attacker's own defense, which is meaningless. This is that missing
> pipeline. §3.10 now routes through **3b**, never 3.

Every creature attack and every hazard tick that reaches the player resolves through this sequence:

```
Step 0: raw_damage_base                 — from AttackDamageProfile (§3.11), or
                                          hazard_damage_per_tick for a hazard tick.

Step 1: tier_scaled = raw_damage_base × (1 + tier_attack_scalar × (power_tier − 1))
        — incoming damage scales with the creature's power_tier, mirroring how Formula 3 Step 4
          scales the creature's defense. power_tier is supplied by `encounter-spawn-system`.

Step 2: source_multiplied = tier_scaled × source_effectiveness_multiplier(creature_source, hunter_armor_source)
        — resonance-weaving Formula 4, the SAME 6×6 table, read in the opposite direction.
          If the Hunter has no Source-aligned armor equipped, this step is skipped (×1.0).

Step 3 (defensive verb — mutually exclusive, exactly one applies):
        dodged successfully      → final_damage = 0. Pipeline exits here. No mitigation needed.
        interrupted successfully → final_damage = 0. Pipeline exits here. The attack never lands.
        blocked successfully     → verb_adjusted = source_multiplied × (1 − block_damage_reduction_percent)
        unavoided                → verb_adjusted = source_multiplied

Step 4: mitigated = verb_adjusted × (100 / (100 + hunter_defense))
        — hunter_defense is the Hunter Stat Catalog value (A8), written by
          `hunter-progression-system` (base 0, up to 120 via Training ranks).
          This is the ONLY consumer of that stat in the project.

Step 5: vow_adjusted = mitigated × (1 + Σ vow_damage_taken_increases)
        — where `vow_fragility` (resonance-weaving-system §4.2) contributes
          `fragility_damage_taken_increase` = 0.125 (+12.5%).
          Applied HERE, to the pipeline's OUTPUT — deliberately NOT as a reduction of
          `hunter_defense`. See the note below; this is load-bearing.

Step 6: final_damage = max(min_incoming_damage_floor, round(vow_adjusted))

Step 7: player_current_hp -= final_damage (floor 0).
        If player_current_hp reaches 0 → RETREAT (§3.2), never death.
```

> **Why the Vow penalty is applied to the pipeline's OUTPUT (Step 5), not to `hunter_defense` (Step 4).**
> `vow_fragility` was originally specified as *"−20% of your `defense`."* That cannot work, and the
> reason is a boundary case that looks fine until you check it: `hunter-progression-system` sets
> `defense` at **base 0**, rising to 120 only through purchased Training ranks. At `defense = 0`,
> −20% of 0 is **0** — so a player who simply never trains defense would pay **nothing**, and the Vow
> would be a free +33% power multiplier all over again. *Any cost expressed as a fraction of an
> investable stat is avoidable by declining the investment.* A damage-taken multiplier on the output
> is not: you cannot decline to take damage. This keeps the Vow's price **invariant across every
> possible build** (see `resonance-weaving-system` §4.2 for the eHP derivation).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `hunter_defense` | int, **external input** | 0–120 | The `defense` stat from the Hunter Stat Catalog (A8). Base **0**, +2 per Training rank to a cap of 120 (`hunter-progression-system`). **This formula is its only consumer in the entire project** — and the reason the stat exists at all. Before Formula 3b, `defense` was read by nothing (Blocker B3). |
| `vow_damage_taken_increases` | float, external input | Σ ∈ `[0, ~0.5]` | Sum of all active Vows' damage-taken penalties. Currently only `vow_fragility` (+0.125). Applied post-mitigation so it can never be nullified by a build choice. |
| `tier_attack_scalar` | float, tuning knob | 0.05–0.50 (default **0.15**) | Deliberately identical to Formula 3's `tier_defense_scalar` and `creature-data-schema`'s `tier_health_scalar`. All four `power_tier` curves in the project share one scalar so difficulty scales coherently rather than compounding in one direction. |
| `block_damage_reduction_percent` | float, tuning knob | 0.50–0.95 (default **0.80**) | **Not a new knob** — this is the existing value locked by A6 and §3.5, reused verbatim. Formula 3b is where it is finally *applied* arithmetically; before 3b existed, the knob was defined in three places and consumed in none. A block is a large mitigation but never total: total negation would make block strictly dominate dodge (no charge cost, no cooldown), collapsing the defensive triad to a single verb — A6's stated dominant-strategy risk. |
| `min_incoming_damage_floor` | int, tuning knob | 1–5 (default **1**) | An unavoided hit always costs the player *something*, however much defense they stack. This is the structural guarantee that defense cannot become immunity. |
| `final_damage` | int | ≥ 0 | 0 only via a successful dodge or interrupt (Step 3 exit). Otherwise ≥ `min_incoming_damage_floor`. |

**Mitigation asymptote — why defense cannot be stacked to immunity.** The term
`100 / (100 + hunter_defense)` is hyperbolic: it approaches 0 but never reaches it, and its *marginal*
value collapses as it climbs. Across the **full achievable range** (`hunter_defense` 0 → 120, i.e. all
60 Training ranks bought, per `hunter-progression-system`):

| `hunter_defense` | Training ranks | Damage taken | Marginal gain per 20 pts |
|---|---|---|---|
| 0 (untrained) | 0 | 100.0% | — |
| 20 | 10 | 83.3% | −16.7 pp |
| 40 | 20 | 71.4% | −11.9 pp |
| 60 | 30 | 62.5% | −8.9 pp |
| 80 | 40 | 55.6% | −6.9 pp |
| 100 | 50 | 50.0% | −5.6 pp |
| **120 (hard cap)** | **60 (maxed)** | **45.5%** | **−4.5 pp** |
| (∞, unreachable) | — | → 0% (never reached; `min_incoming_damage_floor` binds first) | — |

**A Hunter who spends every defense Training rank still takes 45.5% of incoming damage** — and the last
20 points bought only 4.5 percentage points, versus 16.7 for the first 20. Even the *unreachable*
limit is asymptotic, and `min_incoming_damage_floor` binds before zero regardless.

**This is load-bearing for Pillar 1 (Precision Over Reflexes).** If defense could approach immunity,
telegraphs would become ignorable and the read-the-attack skill the entire game is built on would
collapse into a stat check. The curve guarantees that *reading the telegraph and dodging* (Step 3 — a
full zero) is always strictly better than *tanking it* (Step 4 — capped at a 54.5% reduction, ever), no
matter how much the player invests. **Dodging is the only route to zero. Defense is a smaller error
budget, never a licence to stop reading.**

**Worked example A — low tier, unavoided.** `power_tier = 1`, `raw_damage_base = 40`, unaligned armor,
`hunter_defense = 25`:

```
Step 1: 40 × (1 + 0.15 × 0) = 40
Step 2: 40 × 1.0 (unaligned) = 40
Step 3: unavoided            → 40
Step 4: 40 × (100 / 125)     = 32.0
Step 5: no Vow bound         → 32.0 × 1.0 = 32.0
Step 6: final_damage = 32
```

**Worked example B — mid tier, blocked.** `power_tier = 8`, `raw_damage_base = 40`, creature `source =
shadow` vs. Hunter armor `source = spirit` → strong for the creature (`1.5`), `hunter_defense = 100`,
attack **blocked**:

```
Step 1: 40 × (1 + 0.15 × 7)  = 40 × 2.05 = 82.0
Step 2: 82.0 × 1.5 (strong)  = 123.0
Step 3: blocked              → 123.0 × (1 − 0.80) = 24.6
Step 4: 24.6 × (100 / 200)   = 12.3
Step 5: no Vow bound         → 12.3
Step 6: final_damage = 12
```

**Worked example C — high tier, unavoided (the punish case).** Same creature as B, `power_tier = 20`,
`hunter_defense = 100`, attack **unavoided**:

```
Step 1: 40 × (1 + 0.15 × 19) = 40 × 3.85 = 154.0
Step 2: 154.0 × 1.5 (strong) = 231.0
Step 3: unavoided            → 231.0
Step 4: 231.0 × (100 / 200)  = 115.5
Step 5: no Vow bound         → 115.5
Step 6: final_damage = 116
```

**Worked example D — the same hit with `vow_fragility` bound (the Vow's price, made concrete).**
Identical to C, but the Hunter has `vow_fragility` active (+12.5% damage taken, in exchange for
×1.33 ability power):

```
Steps 1–4: identical to C     → 115.5
Step 5: 115.5 × (1 + 0.125)   = 129.9
Step 6: final_damage = 130
```

**130 vs. 116 — a real, unavoidable +12.5%.** Note what makes this honest: the penalty lands *after*
mitigation, so it is **the same +12.5% for every build**. A Hunter with 0 defense and a Hunter with a
maxed 120 defense both pay exactly 12.5% more. Had the Vow instead been specified as *"−20% of your
`defense`"* — as it originally was — the 0-defense Hunter would have paid **nothing at all**, since 20%
of 0 is 0. That is the difference between a cost and a decoration.

**The defensive gradient, verified.** Take the *same* attack (Worked Example B's creature, at
`power_tier = 20`, `hunter_defense = 100`) and vary only the player's defensive verb:

| Verb | `final_damage` | vs. unavoided |
|---|---|---|
| **Interrupt** | **0** | fully negated — *and* opens a vulnerability window |
| **Dodge** | **0** | fully negated |
| **Block** | **23** | 5.0× cheaper than eating it |
| **Unavoided** | **116** | — |

This reproduces `creature-ai-telegraph-system`'s locked reward gradient (**interrupt > dodge >
block**, its A5) as an arithmetic consequence rather than an assertion: interrupt and dodge both
reach zero, but only interrupt *also* opens a vulnerability window (Formula 3 Step 2's ×1.5), which
is what breaks their tie. Block is always available and costs no charge, so it must stay strictly
worse than both — 23 is a real cost, not a rounding error. The spread widens with `power_tier`, so
reading attacks matters *more* as the game gets harder, never less.

(Block at tier 20 = `231.0 × 0.20 × (100/200) = 23.1 → 23`.)

**Retreat threshold.** `player_current_hp` reaching 0 triggers **RETREAT** (§3.2), not death. Per the
project's locked no-permadeath rule, Retreat costs nothing but the encounter: no items lost, no
creature lost, HP restored on hub return. Formula 3b's only job is to make the *encounter* fail
honestly — never to punish the player's account.

### Formula 4 — Part-Break Loot Bonus (Hook for `loot-drop-system`)

```
part_break_loot_bonus_percent = min(part_break_loot_bonus_cap_percent, parts_broken_before_kill × part_break_loot_bonus_per_part_percent)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `parts_broken_before_kill` | int, external count | 0 to the template's total non-core part count | Number of non-core parts broken (`current_stage = crack_stage_count + 1`) at the moment the Kill outcome fires (§3.9, priorities 1–2). |
| `part_break_loot_bonus_per_part_percent` | float, tuning knob | 5–15 (default 8) | Percent loot-value uplift per broken part. |
| `part_break_loot_bonus_cap_percent` | float, tuning knob | 25–60 (default 40) | Hard ceiling, mirroring resonance-weaving Formula 6's identical capping discipline for Mark. |
| `part_break_loot_bonus_percent` | float | `[0, 40]` at default tuning | Attached to the `CreatureDefeated` event (§3.9); `loot-drop-system` is expected to apply it as a value/quantity uplift on that Kill's loot roll — the exact application is that system's own design. |

**Worked example**: a standard creature (4 non-core parts) killed with 3 broken:
`min(40, 3 × 8) = 24%`.

### Formula 5 — Resonance Gain (with `resonance_affinity` Scaling)

```
resonance_gain = round(base_gain[trigger] × (1 + resonance_affinity_scaling_coefficient × hunter_resonance_affinity))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `base_gain[trigger]` | int, tuning knob per trigger | See §3.6's table | The 7 base amounts (1/2/3/8/15/6/10). |
| `resonance_affinity_scaling_coefficient` | float, tuning knob | 0.001–0.01 (default 0.004) | Deliberately gentler than ability-power scaling (0.008) — Resonance gain is a secondary reward for the `spirit`-Source stat, not its primary purpose. |
| `hunter_resonance_affinity` | float, external input | ≥ 0 | §3.1's effective `resonance_affinity` stat. |
| `resonance_gain` | int | ≥ `base_gain[trigger]` | Output, added to `current_resonance` and clamped to `resonance_cap`. |

**Worked example**: `hunter_resonance_affinity = 50`, a successful interrupt (`base_gain = 15`):
`round(15 × (1 + 0.004 × 50)) = round(15 × 1.2) = 18`.

### Formula 6 — Capture Favorability Score (Hook for `rare-creature-capture-system`)

```
capture_favorability_score = successful_blocks + (2 × successful_dodges) + (4 × successful_interrupts) − (1 × unavoided_hits_taken)
```

A simple running per-encounter accumulator, weighted to mirror the same interrupt > dodge > block
gradient the rest of this document already applies, minus a light penalty for unavoided hits so
sloppy play cannot out-favor clean play by sheer volume of blocked chip damage. Deliberately not
clamped or normalized here — `rare-creature-capture-system` is expected to define
its own mapping from this raw score to an actual capture-chance percentage, matching resonance-
weaving's own precedent of exposing a raw value for a dependent, not-yet-written system to
interpret rather than pre-empting that system's formula.

### Formula 7 — Active Efficiency Percent (BALANCE-CRITICAL, A9)

The formal, measurable form of the task brief's active/idle contract. Defines what "active play
efficiency" **is**: a composite score built from three independently-observable, per-encounter
measurements — never a vibe.

```
active_efficiency_percent = clamp(
    100 × CTR × (1 + VWS × vws_bonus_scalar + IR × ir_bonus_scalar),
    active_efficiency_floor_percent,
    active_efficiency_cap_percent
)

CTR (Clear-Time Ratio)        = par_clear_time_seconds / active_clear_time_seconds
VWS (Vulnerability-hit Share) = (damage-dealing hits landed while target_part_id ∈ vulnerable_parts) / (total damage-dealing hits)
IR  (Interrupt Rate)          = successful_interrupts / (successful_interrupts + successful_dodges + successful_blocks + unavoided_hits_taken)
```

> **⚠ ANCHOR RULE — the single most important constraint in this formula.**
> `CTR`'s numerator is a **fixed, hand-authored constant**. It is *never* automation's live,
> measured throughput. Wiring a live automation number in here is precisely the bug that
> `/review-all-gdds` (2026-07-14) caught as **Blocker B1** — see the "Why the anchor must be fixed"
> subsection below. If a future edit makes this numerator vary with the player's mastery, team, or
> automation stage, **Pillar 3 inverts** and the game's central balance promise breaks.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `par_clear_time_seconds` | float, **external input — a fixed authored constant** | > 0 | Owned by `encounter-spawn-system` as a field on the **encounter template**. Defined as: *the clear time achieved by a competent, fully-mastered automated team on this encounter.* Hand-authored per template. It does **not** vary with the player's live mastery level, team composition, or automation throughput. It is the 100% anchor for **both** the active-efficiency band and (in `region-mastery-automation-system`) the idle-efficiency band. |
| `active_clear_time_seconds` | float, measured | > 0 | Wall-clock duration of `ACTIVE` for one encounter, measured directly by this system. |
| `CTR` | float | > 0, unbounded | > 1 means the active player cleared faster than par; < 1 means slower. |
| `VWS` | float | `[0, 1]` | Fraction of the player's own damage output landing during an open vulnerability window — only ever opens from skilled play (`creature-ai-telegraph-system` A6), so this term is structurally impossible to farm passively. |
| `IR` | float | `[0, 1]` | Fraction of all defensive resolutions that were interrupts specifically — the highest-skill defensive action. |
| `vws_bonus_scalar` | float, tuning knob | 0.4–1.2 (default 0.8) | How strongly vulnerability-hit discipline converts into efficiency %. |
| `ir_bonus_scalar` | float, tuning knob | 0.8–1.6 (default 1.2) | How strongly interrupt discipline converts into efficiency % — deliberately the larger of the two scalars, matching the reward gradient. |
| `active_efficiency_floor_percent` | float, tuning knob | 30–60 (default 50) | A degenerate-low-skill clamp — even a barely-engaged active session still deals *some* basic-attack damage, so this is never 0. |
| `active_efficiency_cap_percent` | float, tuning knob | **hard-locked at 300** | Direct implementation of the task brief's "avoid sustained active play being more than ~3x better." |

**Output range**: `[50, 300]` at default tuning, by construction (the outer `clamp`).

**Worked examples** (illustrative representative inputs per named play style — unbalanced
placeholders pending Vertical Slice playtesting, matching this project's established convention for
every other numeric default):

| Play Style | CTR | VWS | IR | Calculation | `active_efficiency_percent` |
|---|---|---|---|---|---|
| Light interaction | 1.05 | 0.20 | 0.05 | `100 × 1.05 × (1 + 0.20×0.8 + 0.05×1.2)` = `100 × 1.05 × 1.22` | **128.1%** (target band 120–140%) |
| Focused active play (lower bound) | 1.15 | 0.40 | 0.20 | `100 × 1.15 × (1 + 0.32 + 0.24)` = `100 × 1.15 × 1.56` | **179.4%** (target band 180–220%) |
| Focused active play (upper bound) | 1.20 | 0.50 | 0.30 | `100 × 1.20 × (1 + 0.40 + 0.36)` = `100 × 1.20 × 1.76` | **211.2%** |
| Highly skilled, special encounter | 1.30 | 0.65 | 0.45 | `100 × 1.30 × (1 + 0.52 + 0.54)` = `100 × 1.30 × 2.06` | **267.8%** (target band 250–300%, briefly) |
| Highly skilled, peak | 1.35 | 0.70 | 0.50 | `100 × 1.35 × (1 + 0.56 + 0.60)` = `100 × 1.35 × 2.16` | **291.6%**, clamped nowhere near the 300 ceiling |

**Why the advantage is more than raw damage (task brief's explicit requirement)**: `CTR` already
captures faster clear times directly. `VWS` and `IR` are not damage terms at all in isolation —
they are *rates of skilled action* that happen to correlate with, but are formally distinct from,
raw DPS. The same skilled actions that raise `VWS`/`IR` also independently raise
`part_break_loot_bonus_percent` (Formula 4), `capture_favorability_score` (Formula 6), and reduce
wasted ability casts (a high `VWS` is definitionally a low rate of abilities landing at
unmultiplied baseline power) — four separate reward channels driven by the same underlying skilled
behavior, exactly matching the brief's list (faster clears, better part-break rewards, higher
capture chances, more loot control, reduced resource waste).

#### Why the anchor must be fixed (Blocker B1 — the bug this formula used to have)

**The bug.** `CTR`'s numerator was originally `automation_baseline_clear_time_seconds`, an input
`region-mastery-automation-system` derives from its *own* `idle_efficiency_percent` — which **rises
with mastery**. So the denominator of the player's score moved as the player's automation improved.
Holding player skill perfectly constant, `active_efficiency_percent` **fell** as their team got better.

Worked at `par = 15 s`, a fixed active clear of 27 s, `VWS = 0.40`, `IR = 0.20`, and a maxed team
(`team_quality_score = 1.0`, i.e. each mastery band's ceiling):

| Region state | `idle_efficiency` | Automation clear time | Active clear time (skill FIXED) | `active_efficiency_percent` |
|---|---|---|---|---|
| Newly conquered | 40% | 37.5 s | 27 s | **216.7%** |
| Partially mastered | 70% | 21.4 s | 27 s | **123.8%** |
| Fully mastered | 100% | 15.0 s | 27 s | **86.7%** |
| Optimized team | 120% | 12.5 s | 27 s | **72.2%** |

**The same performance, worth a third as much** — purely because the player built a good farm. And
note the fourth row: on a well-mastered region a *skilled* active player scored **below 100**, i.e.
worse than the very automation baseline they were being measured against. Playing well had become a
net negative.

Worse still, `loot-drop-system`'s active bonus is `max(0, (active_eff − 100) / 100)` — so it **clamped
to exactly zero at Fully Mastered**, deleting the entire reward premium for playing actively. Pillar 3
("Active Is Better / Idle Is Never Worthless") was not merely weakened; it was **arithmetically
inverted**.

> **These figures are now verified by executable test, not by hand.** See
> `tests/unit/ResonanceHunter.Core.Tests/Progression/BlockerB1RegressionTests.cs`, which reconstructs
> the original formula and asserts the inversion, then asserts the fixed formula holds steady on the
> same inputs. An earlier hand-computed version of this table published *166 / 92.5 / 63.2 / 50* —
> those numbers came from an unstated parameterization and **did not reproduce**. The inversion was
> real; the specific figures were not. This is exactly the class of error that put a broken contract
> into 25 documents behind two hand-written "proofs", so the numbers now live in a test.

**The fix.** Anchor to `par_clear_time_seconds` — fixed, authored, mastery-independent.

**Why this changes none of the project's locked numbers.** `par` *is* the "fully automated = 100%
baseline" from the original brief's own table. Anchoring **both** efficiency tables to that one fixed
number makes both hold literally and simultaneously, with no circularity:

| Anchored to `par` | 100% | Band 2 | Band 3 | Band 4 |
|---|---|---|---|---|
| **Active** (this doc, Formula 7) | fully automated | 120–140% light | 180–220% focused | 250–300% skilled |
| **Idle** (`region-mastery-automation-system` F4) | — | 25–40% newly conquered | 50–70% partial · 80–100% full | 100–120% optimized |

Idle's "fully mastered = 80–100%" **converges on par**, and "optimized = 100–120%" modestly exceeds
it — exactly what you would expect if par is *defined* as a competent fully-mastered automated team.
The two tables were never in conflict with each other. Only the moving anchor was.

**Pillar 3 now holds at every mastery level — verified, not asserted:**

- *Active is always better.* The weakest engaged active play (light interaction, **128.1%**) still
  beats the strongest possible idle (optimized team, **120%**). At every band below that, the gap
  widens.
- *Active is never more than ~3× better.* The hard ceiling (**300%**, clamped) against the strongest
  idle (**120%**) is **2.5×** — inside the brief's explicit "avoid sustained active play being more
  than roughly 3× better."
- *Idle is never worthless.* Its floor (25% of par, a freshly conquered region) is non-zero and rises
  monotonically to 120% with earned mastery — Pillar 2's whole thesis.

**One consequence worth stating plainly, because it is a design choice and not an accident.** Against
a *newly conquered* region (idle 25–40%), skilled active play (up to 300%) is **7.5–12× more
efficient** — far outside the "~3×" figure. This is intended and is Pillar 2 ("Automation Is Earned,
Not Assumed") doing its job: fresh automation is *supposed* to be poor, and the brief's ~3× ceiling
describes the relationship against the **reference automation** (par), which is what its own table
measures. The ratio collapses to 2.5× once mastery is actually earned. If this is ever judged too
punishing on fresh regions, the knob is `idle_efficiency_band_min` — not this formula's anchor.

**Both prior "proofs" of the efficiency contract in this document were invalid** and have been
removed: each compared an active number and an idle number taken from *different* mastery levels,
which the moving anchor made meaningless.

## 5. Edge Cases

1. **The player has no valid target — every `can_be_vulnerable` part is broken but the creature is
   still alive.** This occurs specifically when the `is_core` part has `can_be_vulnerable = false`
   and every other part is broken (`input-targeting-system` Edge Case #2's exact degenerate case).
   **Ruling: both the basic-attack timer (§3.3) and any ability activation fall back to targeting
   the `is_core` part directly**, bypassing the normal `selected_part_id` register entirely,
   whenever the Valid Target Set (`input-targeting-system` §3.2) is empty and
   `hostile_state.current_health > 0`. This guarantees the encounter can always be finished — an
   empty Valid Target Set is never a soft-lock. The player is not required to do anything special
   to trigger this; it activates automatically the instant it becomes true.
2. **An ability's cast fully resolves in the same frame the creature dies.** **Ruling: honored, not
   cancelled.** Damage resolution (§3.2 step 5) processes every event that occurred within the
   frame in order; if a basic attack or an earlier ability in the same frame already reduced
   `current_health` to 0 or broke the core, a *concurrently resolving* ability whose input was
   already committed (cast confirmed, cooldown/Resonance already spent) still fully applies its
   damage, part-break accumulation, and Resonance gain — including counting toward
   `parts_broken_before_kill` (Formula 4) if it lands the breaking hit itself. This is a deliberate
   fairness rule: a player's already-committed action is never retroactively voided by a race with
   another damage source in the same frame. **What is not permitted**: a *new* ability activation
   attempted after the encounter-end check (§3.2 step 6) has already transitioned the state to
   `RESOLVING` — that input is simply rejected as a no-op (§3.2's state table, `RESOLVING` row).
3. **A Vow's condition flips mid-cast.** Not this document's rule to define — resonance-weaving §5
   Edge Case #3 already governs it (the `vow_power_multiplier` is snapshotted at cast instant and
   held for the cast's full active duration). This document's only obligation, restated for
   clarity: the snapshot read happens at the exact frame this document processes the cast
   activation (§3.4 step 5), never earlier or later, so both documents agree on what "the instant
   of cast" means operationally.
4. **The player dodges and interrupts the same telegraph simultaneously** (a dodge press and a
   qualifying interrupt-ability cast both land inside their respective windows against the same
   in-progress telegraph). **Ruling**: the interrupt's outcome (§3.5) is authoritative for the
   vulnerability window and Resonance gain — dodge's branch is never also applied on top of it,
   since that would double-grant a window. **The dodge charge itself is refunded** (not consumed)
   in this specific case, since the attack was already fully avoided by the interrupt and the
   player's dodge input turned out to be unnecessary — refunding it rewards decisive, overlapping
   defensive execution instead of punishing it as a wasted resource.
5. **Resonance is spent (the Ultimate is cast) in the same frame the encounter ends.** Two
   sub-cases: **(a) the Ultimate's own damage causes the kill** — honored per Edge Case #2's rule
   (an already-committed cast completes fully; the Resonance deduction and the kill both apply in
   the same frame, in that order). **(b) Unspent Resonance simply remains on the meter when a
   *different* end condition fires** (e.g. a basic attack lands the kill while Resonance sits at
   80) — per §3.6, that Resonance is lost; it does not carry into the next encounter and is not
   refunded or banked. This is the intended tension named in the task brief, not an oversight: the
   player who banks Resonance too conservatively risks losing it to an encounter ending sooner than
   expected.
6. **A part breaks during an in-progress telegraph.** Governed jointly by this document and
   `creature-ai-telegraph-system`. This document's exact obligation at the moment §3.7 step 6 fires
   a break: (a) if the broken part is the telegraph's own `executing_part_id`, the telegraph is
   hard-cancelled immediately (`creature-ai-telegraph-system` Edge Case #4 — this document is the
   system that actually applied the damage causing the break, and must signal that system's
   Formula 5 evaluation in the same frame, not a later one); (b) if it is any other part, the
   telegraph is unaffected and resolves normally (that document's A8, deferred pool swap); (c) if
   it is the `is_core` part, every other rule in this section is superseded — the encounter ends
   immediately (§3.9 priority 1), the in-progress telegraph is discarded without a cutoff/hit
   resolution, and no further §3.2 step 1–5 processing occurs for that tick.
7. **The player's HP is set to 1 rather than 0 on a Retreat outcome** (§3.8). This is a deliberate
   floor, not an inconsistency with "HP reached 0 triggers Retreat": the *trigger* condition is
   `current_hp` reaching 0 during `ACTIVE`; the *displayed/carried* value the moment the player
   lands back in the region hub is clamped to a minimum of 1 so that a subsequent, unrelated source
   of chip damage (a hazard tick still resolving in the same frame, for instance) cannot display a
   confusing "already dead again" state before the hub's full-HP recovery tick applies.
8. **A Mark-Form ability is in this document's damage pipeline.** Mark deals no `pre_pipeline_damage`
   of its own (§4 Formula 3, Step 0's note) — `mark_damage_taken_bonus_percent` (resonance-weaving
   Formula 6) is a percentage amplifier this document applies to *other, separately-resolving*
   damage-dealing events landing on the same marked part for `mark_duration_seconds`, by inserting
   an additional `× (1 + mark_damage_taken_bonus_percent / 100)` step immediately after Step 3
   (crit) and before Step 4 (mitigation) of Formula 3, for any subsequent hit against that part
   while the mark is active. A Mark cast itself never has a `final_damage` value, never contributes
   to part-break accumulation directly, and is excluded from `total damage-dealing hits` in
   Formula 7's `VWS` denominator.
9. **A `Capture` attempt (§3.9) is in flight when `rare-creature-capture-system` does not yet
   exist** (current MVP state — that system is Vertical Slice tier). **Ruling**: the
   `AttemptCapture` action point and `capture_favorability_score` (Formula 6) are both fully
   specified and functional as of this document, but with no consuming system yet authored, any
   `AttemptCapture` input during MVP is a no-op that produces no outcome change — the encounter
   simply continues as if the input were never issued. This is intentional forward-compatibility,
   not a bug: this document must not block on a system it does not own.
10. **A creature's `power_tier` places `effective_defense` (Formula 3) high enough that
    `final_damage` would mathematically approach the `min_damage_floor` for every hit.** Not
    treated as an error — a very high-defense encounter genuinely demanding sustained, high-power
    hits (crits, vulnerability windows, Source-favorable matchups) to make meaningful progress is a
    legitimate difficulty lever, not a formula failure. `min_damage_floor` guarantees the encounter
    is never mathematically unwinnable (every hit deals at least 1), only very slow, at which point
    it is a balance-tuning concern for `tier_defense_scalar`/`base_defense` authoring, not a rule
    violation.
11. **An encounter template is missing its `par_clear_time_seconds`** (an authoring error — the field
    is mandatory on every `EncounterTemplate`, per `encounter-spawn-system`). **Ruling**: `CTR` is
    undefined, so `active_efficiency_percent` **falls back to exactly 100** (the par-equivalent
    baseline) rather than 0, the floor, or a divide-by-zero. The encounter remains fully playable and
    all loot still drops; only the *efficiency premium* is neutralized, since a premium cannot be
    measured against a par that does not exist. **The build must fail content validation on any
    template missing this field** (`encounter-spawn-system` AC) — this ruling is a runtime safety net,
    never a licence to ship an unauthored par.

    *This edge case previously read: "`active_efficiency_percent` is computed with
    `automation_baseline_clear_time_seconds` not yet available… the formula is inert until
    `region-mastery-automation-system` supplies its baseline." That is exactly the dependency that
    caused Blocker B1 — the anchor must never come from the automation system, because that system's
    clear time improves with mastery and would drag the active player's score down as their own farm
    got better. The anchor now comes from the encounter template, and Formula 7 is live at MVP.*
12. **The player issues a manual Retreat (§3.9 priority 5) while an ability with an active,
    ongoing effect (Aura, Summon, an armed Trap) is still running.** All active effects are
    discarded immediately at the `ACTIVE → RESOLVING` transition, identical to how
    `resonance-weaving-system` §5 Edge Case #9 handles an unresolved Trap at any other form of
    encounter end — no partial payout, no carry-over, consistent treatment regardless of *why* the
    encounter ended.

## 6. Dependencies

### Depends On

- **`creature-data-schema.md`** — reads/writes `hostile_state.current_health`,
  `.effective_max_health`, `.part_states[].accumulated_value`, `.current_stage`,
  `.vulnerable_parts`, `.vulnerability_window_ms`; reads `CreatureTemplate.base_stats.
  base_attack_power`/`.base_defense`, `PartDefinition.part_id`/`.is_core`/`.can_be_vulnerable`/
  `.break_threshold_type`/`.loot_modifier_tag`/`.fight_state_change_tag`, `CreatureInstance.
  power_tier`/`.bind_state`. Obeys that schema's invariants (never writes a broken `part_id` into
  `vulnerable_parts`; a core break always ends the encounter, Edge Case #2) at every step. This
  document is the primary writer of `hostile_state.current_health` and `part_states[].
  accumulated_value` — no other system mutates those fields during combat.
- **`animation-rig-system.md`** — triggers `Clip` transitions (idle → windup → peak → recovery →
  hit-reaction → break) in sync with this document's own event timeline (§3.2), and reads a clip's
  completion signal (that document §3.11) to know when control returns to idle. This document does
  not define *how* a clip plays, only *when* combat logic requests a transition.
- **`input-targeting-system.md`** — subscribes to its `TargetConfirmed` event (§3.1 there) as one
  input to ability/interrupt confirmation (§3.4), and reads its shared `selected_part_id` register
  every basic-attack tick (§3.3). This document never writes to that register — targeting stays
  entirely that system's domain.
- **`creature-ai-telegraph-system.md`** — reads `windup_duration_ms`, the selected `AttackDefinition`
  (Formula 6 there), and Formula 3's window boundaries to evaluate block/dodge/interrupt inputs
  (§3.5); reads `vulnerability_damage_multiplier` and `interrupt_break_progress_multiplier`
  (Formula 4 there) as direct inputs to this document's own Formula 3 and §3.7. This document is
  the system that document's own §6 named as the eventual owner of hit-testing, damage formulas,
  and input-state reading against its windows — this document fulfills that contract exactly.
- **`resonance-weaving-system.md`** — reads `WovenAbilityDefinition`s per equipped slot, calls its
  Formulas 1–6 in full (§4 Formula 3's Worked Example B continues that document's own §4.7 pipeline
  numerically), and reads `power_multiplier`/`effect_mode` for gate/scale eligibility (§3.4). This
  document is the "primary consumer" that document's own §6 named in advance.
- **`item-data-schema.md`** — reads equipped Item Instances' `modifiers` array (§3.1's stat catalog
  resolution) and a weapon's `source` field (§4 Formula 3 Step 1). Confirms (A8) the stat catalog
  that document's §3.6 left as an open item pointed at this system.
- **`encounter-spawn-system.md`** — supplies the two inputs this document cannot compute for itself:
  **`par_clear_time_seconds`** (the fixed anchor for §4 Formula 7 — see A9) and **`power_tier`** (which
  scales creature health, Formula 3's `effective_defense`, and Formula 3b's incoming damage). This
  document *runs* an encounter; that system *decides which encounter*. Before it existed, `power_tier`
  had four consumers and no producer at all (Blocker B2).
- **`hunter-progression-system.md`** — is the **sole writer** of the Hunter Stat Catalog this document
  defines in A8. Every `hunter_*` value read here (`hunter_attack_power` in Formula 1,
  `hunter_defense` in Formula 3b, `hunter_max_health` in §3.8) originates there and nowhere else.
  Before it existed, this document's stat catalog had readers but no source (Blocker B4).

### Depended On By

- **`vow-condition-tracking`** — reads this document's per-frame combat
  state (player HP, Resonance, encounter elapsed time, attack-cycle counters) as the live inputs
  its `condition_type` evaluators need (e.g. `health_below_percent_threshold` reads
  `player_current_hp` directly from this document, §3.8).
- **`loot-drop-system`** — consumes the `CreatureDefeated` event and
  `part_break_loot_bonus_percent` (§3.9, §4 Formula 4) as direct inputs to its own drop-table roll.
  Also consumes `active_efficiency_percent` (§4 Formula 7) for its active loot bonus — which is why
  Blocker B1's inverted anchor silently zeroed that bonus. See Formula 7's "Why the anchor must be
  fixed."
- **`region-mastery-automation-system`** — measures its own `idle_efficiency_percent` against **the
  same fixed `par_clear_time_seconds` anchor** this document's Formula 7 uses, so that the active and
  idle bands are directly comparable. It does **not** supply this document's anchor — that was the B1
  bug. Causation now runs the other way: that system *derives* automation's clear time from par and
  its own idle-efficiency band, rather than defining the reference the player is scored against.
- **`hunter-progression-system`** — is the **writer** of the Hunter Stat Catalog this document defines
  in A8 (`attack_power`, `focus`, `vitality`, `engineering`, `guile`, `resonance_affinity`,
  `max_health`, `defense`, `critical_chance`). This document reads those stats every encounter; that
  system is their only source. The catalog is a stable contract between the two — neither may add a
  stat unilaterally.
- **`rare-creature-capture-system`** (Vertical Slice tier) — attaches its own
  success-chance formula to this document's `AttemptCapture` action point and `capture_favorability_
  score` (§3.9, §4 Formula 6). Inert at MVP (§5 Edge Case #9).
- **`combat-hud`** — reads `player_current_hp`/`hunter_max_health` (health pips),
  `current_resonance`/`resonance_cap` (the Resonance meter), each ability/ultimate's cooldown state
  and Vow-condition signal (belt-charm pip-tracks, art-bible §7.5), and dodge charge count — all
  screen-space or diegetic display of state this document owns and updates; this document defines
  no rendering itself.
- **`audio-system`** — consumes this document's discrete combat events (basic
  attack fire, ability/ultimate cast, block/dodge/interrupt success or failure, part-break, Kill/
  Capture/Retreat outcome) as audio cue triggers. This document defines the events' timing; that
  system owns the sound assets.
- **`onboarding-tutorial-system`** — will sequence a first-encounter tutorial
  against this document's exact state machine (§3.2) and defensive-triad mechanics (§3.5), most
  likely gating advanced mechanics (interrupt, Resonance spend) behind a scaffolded introduction
  order matching Csikszentmihalyi's flow-channel principle this project's own agent instructions
  cite. That system's own GDD must reference this document's states/events by name.

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** — §2 (mood target this document's Player Fantasy section quotes
  directly), §3.5 (the hero-shape hierarchy this document's HUD-facing outputs must never violate),
  §7.5 (the Combat HUD spec `combat-hud` will build against this document's exposed state).
- **`design/gdd/game-concept.md`** — Pillar 1 (Precision Over Reflexes, the direct justification for
  A1's DPS-cap design), Pillar 3 (Active Is Better, Idle Is Never Worthless — the entire basis for
  §4 Formula 7), the Core Loop section (the moment-to-moment description this document implements
  literally, clause by clause).

## 7. Tuning Knobs

All values below are unbalanced placeholders pending Vertical Slice playtesting, per this
project's established convention (matching every sibling document's own tuning knob framing) —
they are exposed as external data (`.claude/docs/coding-standards.md`'s data-driven requirement),
never hardcoded.

### Feel Knobs (tuned by playtesting intuition)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Basic attack cadence | `basic_attack_interval_ms` | 800–2000ms | 1200ms | The hard DPS-cap floor (A1) — the single biggest lever on how "busy" idle basic-attacking feels. |
| Basic attack base value | `basic_attack_base_value` | 5–25 | 12 | Baseline basic-attack damage before stat scaling. |
| Block mitigation | `block_damage_reduction_percent` | 50–95% | 80% | How safe the always-available option is. Too high erodes the reason to ever dodge/interrupt (A6's dominant-strategy risk). Applied in Formula 3b Step 3. |
| Incoming tier scaling | `tier_attack_scalar` | 0.05–0.50 | 0.15 | **BALANCE-CRITICAL** (Formula 3b Step 1) — how much harder a creature hits per `power_tier`. Kept identical to `tier_defense_scalar` and `creature-data-schema`'s `tier_health_scalar` on purpose: three curves, one scalar, so tier difficulty scales coherently instead of compounding. |
| Incoming damage floor | `min_incoming_damage_floor` | 1–5 | 1 | **Structural, not a feel knob** (Formula 3b Step 5) — an unavoided hit always costs *something*, however much `defense` is stacked. This is the backstop that keeps defense from ever becoming immunity, which would make telegraphs ignorable and flatten Pillar 1. Do not set to 0. |
| Crit multiplier | `crit_multiplier` | 1.5–3.0 | 2.0 | Swing size of a critical hit. |
| Dodge charge regen | `dodge_charge_regen_ms` | 2000–8000ms | 4000ms | How quickly a spent dodge charge becomes available again. |
| Ultimate cooldown | `ultimate_cooldown_ms` | 1000–6000ms | 3000ms | Secondary throttle on the Ultimate beyond its Resonance cost (A4). |
| `engaging_duration_ms` | `engaging_duration_ms` | 800–3000ms | 1500ms | Length of the no-input spawn-in beat (§3.2). |

### Curve Knobs (tuned by mathematical modeling)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Basic attack stat scaling | `basic_attack_scaling_coefficient` | 0.003–0.02 | 0.008 | Locked to match resonance-weaving's `source_scaling_coefficient` so basic attacks and abilities scale at the same rate. |
| Defense tier scaling | `tier_defense_scalar` | 0.05–0.50 | 0.15 | Mirrors `creature-data-schema`'s own tier scalars for consistency. |
| Crit chance cap | `crit_chance_cap` | 0.5–0.9 | 0.75 | Prevents guaranteed crits at any build extreme (Formula 2). |
| Resonance affinity scaling | `resonance_affinity_scaling_coefficient` | 0.001–0.01 | 0.004 | Deliberately gentler than ability-power scaling — secondary benefit of the `spirit` stat. |
| Part-break loot bonus per part | `part_break_loot_bonus_per_part_percent` | 5–15% | 8% | Direct implementation of "better part-break rewards" (task brief). |
| Part-break loot bonus cap | `part_break_loot_bonus_cap_percent` | 25–60% | 40% | Ceiling, mirrors resonance-weaving Formula 6's Mark cap discipline. |
| **`vws_bonus_scalar`** | Formula 7 | 0.4–1.2 | 0.8 | **BALANCE-CRITICAL** — directly sets how much vulnerability-window discipline contributes to active efficiency. |
| **`ir_bonus_scalar`** | Formula 7 | 0.8–1.6 | 1.2 | **BALANCE-CRITICAL** — directly sets how much interrupt discipline contributes to active efficiency; deliberately larger than `vws_bonus_scalar` to preserve the reward gradient. |

### Gate Knobs (tuned by session-length / pacing targets)

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Dodge charge cap | `dodge_charge_max` | 1–3 | 2 | How many "free passes" the player banks against reads they can't safely block. |
| Resonance cap | `resonance_cap` | 60–150 | 100 | Ceiling on the shared meter. |
| Ultimate Resonance cost | `ultimate_resonance_cost` | 60–100 | 100 (full cap) | Whether the Ultimate is a "spend it all" climax action (100) or leaves a partial reserve (< 100). |
| Resonance gain amounts | `resonance_per_*` (7 values, §3.6) | 1–20 each | 1 / 2 / 3 / 8 / 15 / 6 / 10 | Individually tunable per trigger; must preserve the ordering block < dodge < interrupt to hold the reward gradient. |
| Min damage floor | `min_damage_floor` | 1–5 | 1 | Guarantees no encounter is mathematically frozen (Edge Case #10). |
| Hazard tick interval | `hazard_tick_interval_ms` | 1000–4000ms | tuned per `HazardDefinition` | Pacing of periodic field-effect damage. |
| **`active_efficiency_floor_percent`** | Formula 7 | 30–60% | 50% | **BALANCE-CRITICAL** — the lower bound a badly-played active session can still register at. |
| **`active_efficiency_cap_percent`** | Formula 7 | **hard-locked at 300** | 300% | **BALANCE-CRITICAL, NON-NEGOTIABLE** — direct implementation of "avoid sustained active play being more than ~3x better" (task brief). This is the one knob in this table that must never be raised without a deliberate, explicit pillar-level decision, since it is the ceiling the entire active/idle contract is measured against. |

### BALANCE-CRITICAL Flag Summary

Per the task brief's explicit instruction, the following are flagged as directly governing the
game's core active-vs-idle thesis (Pillar 3) and must be the first values revisited during
Vertical Slice playtesting, in this priority order: `vws_bonus_scalar`, `ir_bonus_scalar`,
`active_efficiency_floor_percent`, `active_efficiency_cap_percent`, and the per-template
`par_clear_time_seconds` values that `encounter-spawn-system` authors. No other knob in this document
changes the shape of that contract — only its feel around the edges.

> **`par_clear_time_seconds` is a tuning knob but NOT a free one.** It may be re-authored per encounter
> template to tune difficulty. It may **never** be made to vary at runtime with the player's mastery,
> team, or automation stage — that is the definition of Blocker B1, and it inverts Pillar 3. If a
> future change needs automation to feel stronger, the knob is `region-mastery-automation-system`'s
> `idle_efficiency_band_*` values, which move idle *within* the fixed frame. The frame itself is fixed.

## 8. Acceptance Criteria

Functional criteria (does it do the right thing) and experiential criteria (does it feel right) are
both included, per this project's documentation standard.

### Functional

1. **DPS-cap proof (Pillar 1, required by the task brief)**: simulating 1,000 click/cycle events
   within a 10-second window produces exactly the same number of basic-attack ticks (± 1 for phase
   alignment) as simulating zero click events in the same window at the same `basic_attack_
   interval_ms` — verified by asserting `⌊10000 ÷ basic_attack_interval_ms⌋` ticks fire in both
   cases, confirming clicking cannot increase basic-attack DPS beyond the fixed cadence (§4 Formula
   1, A1).
2. **Active efficiency band test (required by the task brief)**: given the Formula 7 worked
   example's "Focused active play" inputs (`CTR=1.15–1.20`, `VWS=0.40–0.50`, `IR=0.20–0.30`) at
   default tuning, `active_efficiency_percent` computes to a value within `[180, 220]` in every
   case — verified by sweeping the full input sub-range and asserting no output falls outside that
   band.
3. **Cycle-and-confirm completeness (required by the task brief)**: every combat action defined in
   this document — basic-attack targeting (inherited from `input-targeting-system`), all 3 ability
   slots, the Ultimate, block, dodge, interrupt (via a qualifying ability cast), manual Retreat, and
   `AttemptCapture` — is fully completable using only discrete button inputs (cycle-next, cycle-
   prev, confirm, guard-hold, dodge-press, ability-press ×4, retreat-press), with zero mouse/pointer
   device connected — verified by a fixture encounter scripted end-to-end (spawn → several
   telegraphs avoided by each defensive type → an ability cast during a vulnerability window → an
   Ultimate cast → a Kill) using only that input set.
4. Formula 3's worked examples (A and B) reproduce exactly: Example A → `final_damage = 42`;
   Example B → `final_damage = 638` and part-break accumulation `+1276`.
5. Formula 1's worked example reproduces exactly: `hunter_attack_power = 120` → `basic_attack_
   damage = 23.52`.
6. Formula 4's worked example reproduces exactly: 3 broken parts → `part_break_loot_bonus_percent
   = 24`.
7. Formula 5's worked example reproduces exactly: `hunter_resonance_affinity = 50`, successful
   interrupt → `resonance_gain = 18`.
8. A core-part break (any source, at any point including mid-telegraph) results, within the same
   frame, in the encounter transitioning to `RESOLVING`/Kill and zero further `ACTIVE`-state
   processing (§3.2 step 1–5) executing for that instance afterward — verified with no intermediate
   frame where any further damage, Resonance, or telegraph logic runs.
9. `player_current_hp` reaching 0 during `ACTIVE` results in a Retreat outcome — never a Kill,
   never a Capture, never a hard game-over — with the target creature's `bind_state` remaining
   `hostile` and no item, currency, or roster loss recorded, verified across a fixture encounter
   forcing HP to 0 via repeated unavoided hits.
10. Simultaneous dodge + interrupt inputs against the same telegraph (§5 Edge Case #4) result in
    exactly one vulnerability window opening (the interrupt branch), zero dodge charges consumed
    net of the refund, and zero double-application of any reward — verified by asserting
    `dodge_charges` before and after the scenario are equal.
11. Unspent `current_resonance` at the instant `ENGAGING` begins for a new encounter is always 0,
    regardless of its value when the previous encounter's `COMPLETE` state was entered — verified
    across a fixture ending an encounter with `current_resonance = 80` and asserting the next
    encounter starts at 0.
12. A basic attack or ability resolving against an empty Valid Target Set (§5 Edge Case #1) always
    resolves against the `is_core` part instead of being silently dropped — verified by a fixture
    where every `can_be_vulnerable` part is broken and `is_core.can_be_vulnerable = false`.
13. `final_damage` (Formula 3) never resolves below `min_damage_floor` for any combination of
    `effective_defense` values up to `999,999` (`creature-data-schema`'s own stated field ceiling)
    — verified by sweeping `base_defense` and `power_tier` to their extremes and asserting the
    output floor holds.
14. `is_critical` (Formula 2) never resolves `true` with probability ≥ `crit_chance_cap` regardless
    of `hunter_critical_chance`'s input value, including values far exceeding the cap — verified by
    a statistical test at an extreme input (`hunter_critical_chance = 5.0`) confirming the observed
    crit rate over 10,000 rolls stays within a normal binomial-noise band of `crit_chance_cap`.
15. `active_efficiency_percent` never resolves outside `[active_efficiency_floor_percent,
    active_efficiency_cap_percent]` (default `[50, 300]`) for any combination of `CTR`, `VWS`, `IR`
    inputs, including pathological extremes (`CTR = 100`, `VWS = 1`, `IR = 1`) — verified by
    sweeping all three inputs to their theoretical extremes and confirming the outer `clamp` holds
    in every case.

### Experiential (validated by playtest, per the High-Risk flag on this system)

16. A playtester who has never seen a given creature can correctly predict, out loud, which
    defensive action (block/dodge/interrupt) they are about to attempt *before* the telegraph's
    cutoff frame, on at least 70% of telegraphs by their third encounter against that creature —
    the direct behavioral proof of "predatory clarity" (§2) rather than reactive guessing.
17. In a blind A/B playtest comparing (a) a fully-automated clear of a region and (b) an actively-
    played clear of the same region by a moderately-skilled player, playtesters asked "which felt
    more rewarding to play" prefer the active session, and playtesters shown both sessions' loot
    totals correctly perceive the active session as meaningfully — not marginally, not
    overwhelmingly — better, consistent with the 180–220% target band feeling like "clearly worth
    it" rather than "irrelevant" or "mandatory."
18. A playtester who deliberately chooses to block every telegraph (never dodging or interrupting)
    reports the choice as "the safe option I'm consciously trading power for," not as "obviously the
    correct answer" or "obviously a trap" — confirming A6's dominant-strategy risk did not
    materialize at shipped tuning values.
19. A playtester who loses an encounter (Retreat outcome) can, within one sentence, correctly
    identify which telegraphed attack or misread cost them the fight, and reports wanting to
    immediately retry rather than feeling discouraged — the direct validation of "failure costs
    little time... educational, not punishing" (game-concept.md, quoted in the task brief).
20. Recording a full encounter's input log against this document's state machine and formulas
    reproduces, byte-for-byte, the same `final_damage`, Resonance, and outcome sequence on replay —
    confirming the entire pipeline is deterministic given identical inputs (no hidden randomness
    beyond the explicitly-modeled `is_critical` roll and, if authored, capture-chance rolls owned by
    a different system).

---

### Acceptance Criteria added 2026-07-14 (Blockers B1 and B3)

These exist because the original 20 criteria were all passable while the game's central balance
contract was arithmetically inverted and the player's `defense` stat was inert. A criterion that
cannot catch that is not a criterion.

21. **The active-efficiency anchor is immovable (B1).** Run the *same recorded input log* (identical
    player skill, identical encounter template) against four different automation states — Newly
    Conquered, Partially Mastered, Fully Mastered, and Optimized — and assert
    `active_efficiency_percent` is **identical to within floating-point tolerance in all four runs.**
    Before this fix the same log scored 166% / 92.5% / 63.2% / 50%. A test that reproduces that spread
    is a FAIL, not a curiosity.

22. **Pillar 3 holds at every mastery level (B1).** For each of the four region states, assert both:
    (a) light-interaction active efficiency (≈128%) **exceeds** that state's `idle_efficiency_percent`
    ceiling; and (b) the ratio of `active_efficiency_cap_percent` (300) to the *optimized* idle ceiling
    (120) is **≤ 3.0** (it is 2.5). Active is always better; never more than ~3× better.

23. **`loot-drop-system`'s active bonus never decays with mastery (B1).** Assert
    `max(0, (active_efficiency_percent − 100) / 100)` is **strictly positive at Optimized mastery** for
    any engaged active session. Before this fix it clamped to zero after roughly 26 minutes of team
    work — the loot premium for playing actively silently vanished.

24. **`defense` is actually read (B3).** Run the same unavoided attack against
    `hunter_defense ∈ {0, 25, 100, 400}` and assert `final_damage` **strictly decreases** across the
    four. Before Formula 3b, all four returned the *same* number — incoming damage was mitigated by the
    creature's own defense, and the player's `defense` stat was read by nothing anywhere in the project.

25. **Defense cannot buy immunity (B3, Pillar 1).** Assert that as `hunter_defense → ∞`, `final_damage`
    converges to `min_incoming_damage_floor` and **never reaches 0**; and that dodging the same attack
    **does** return exactly 0. Formally: *no amount of defense makes a telegraph safe to ignore, and
    dodging is the only route to zero.* If this ever fails, the read-the-attack skill the game is built
    on has become a stat check.

26. **The defensive triad keeps its gradient (B3).** For the same attack at any `power_tier`, assert
    `interrupt (0) == dodge (0) < block < unavoided`, and that **interrupt alone** opens a vulnerability
    window. This must hold at *every* tier — the gradient must widen with difficulty, never invert.

27. **No Vow is free — and its price does not depend on the build (B3 knock-on).** Bind `vow_fragility`
    and assert `final_damage` increases by **exactly 12.5%** (Formula 3b Step 5) — and critically, that
    it does so **identically at `hunter_defense = 0` and at `hunter_defense = 120`.**

    This Vow has now been free *twice*, for two different reasons, and this criterion is written to
    catch both:
    - Originally, `defense` was read by **no formula anywhere in the project** (B3), so "−20% defense"
      cost nothing at all — +60% ability power for free.
    - Then, once Formula 3b made `defense` live, "−20% of `defense`" *still* cost nothing to any player
      who never trained defense, because the stat's **base value is 0** and 20% of 0 is 0.

    A cost expressed as a fraction of an investable stat is always avoidable by declining the
    investment. Assert the penalty is applied post-mitigation, to the pipeline's output. If a future
    edit moves it back onto the `defense` stat, this criterion must fail.

---

### Rule added 2026-07-14 during implementation — a core part never breaks

**Found by a unit test, not by review.** `EncounterTests.test_breaking_every_limb_falls_through_to_the_
core_rather_than_soft_locking` failed, expecting 2 broken parts and finding 3: once every breakable
limb is destroyed, §5 Edge Case 1's fall-through routes all subsequent basic attacks into the
`is_core` part — which then accumulated break progress like any other part and eventually **broke,
while the creature was still alive.**

**Ruling: a part with `is_core = true` accrues no part-break progress and can never enter the
`broken` state.** It absorbs damage as *health loss*, which is already its role in the fight; it does
not need a second one.

**Why this must be a rule and not an authoring convention** (i.e. "just give cores a huge
`break_threshold`"): a threshold, however large, is still reachable against a high-health boss, and the
resulting state is one **no system in the project can represent**. `loot-drop-system` has no drop
mapping for a broken core; `animation-rig-system` and art-bible §8.4's part-break decals assume a broken
part is detached or shattered, and a creature fighting on with a shattered core is visually incoherent;
`combat-hud` has no pip state for it. The state was reachable, undefined, and silently so.

It also removes a latent soft-lock class: with the core marked broken, `ResolveTarget()`'s fall-through
would have been returning an already-broken part as the attack target. Damage still applied to creature
health, so the fight remained winnable — but only by accident, not by design.

Acceptance criterion: grinding a creature until every non-core part is broken must leave the core
**intact**, still targetable, and the encounter still winnable. Asserted in `EncounterTests`.
