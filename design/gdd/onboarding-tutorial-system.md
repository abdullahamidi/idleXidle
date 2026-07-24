# Onboarding & Tutorial System: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | ux-designer |
| **Status** | Complete — authored autonomously, no user available this session (rush mode, see `production/session-state/active.md`). Ambiguities resolved using `design/gdd/combat-encounter-system.md`, `design/gdd/combat-hud.md`, `design/gdd/creature-ai-telegraph-system.md`, `design/gdd/input-targeting-system.md`, `design/gdd/accessibility-settings-system.md`, `design/gdd/game-concept.md`, and `design/art/art-bible.md` §1–§7 as authority; every resolution is flagged inline as an assumption. |
| **Priority / Tier** | MVP — Polish layer (`design/gdd/systems-index.md` #24). This is the system that decides whether the game's entire density — a glyph language, weak-point targeting, telegraph reading, a defensive triad, Resonance/Ultimate timing, part-breaks, a boss, automation, the Forge, creature jobs, and Vows — is learnable by a first-time player without a wiki, and whether the game's single most important emotional beat (the Mastery Transition, art-bible §2) lands as *earned* rather than *arbitrary*. |
| **Depends On** | `design/gdd/combat-encounter-system.md`, `design/gdd/combat-hud.md`, `design/gdd/creature-ai-telegraph-system.md`, `design/gdd/input-targeting-system.md`, `design/gdd/accessibility-settings-system.md` |
| **Depended On By** | None — this is a leaf system. Nothing in the currently locked design consumes this document's own state or contract. |

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | The Hunter begins a new game with a **full, non-Vow-bound starter loadout already equipped** — all 3 ability slots plus the ultimate populated with simple starter Forms, and **at least one equipped ability flagged as interrupt-qualifying** (`resonance-weaving-system`'s `WovenAbilityDefinition`-level flag, `combat-encounter-system` §3.4.4). | Nothing in this document's five dependencies specifies starter-loadout content — that is `resonance-weaving-system`'s domain (already written, not read this session per this document's own dependency scope). Without *some* equipped, interrupt-capable ability from turn one, the defensive triad's third leg is mechanically unreachable in the first encounter, which would force interrupt-teaching into a separate, later "now you get an ability" beat — exactly the kind of drip-fed, lecture-shaped pacing this document argues against. This assumption is the minimum content commitment needed to teach block, dodge, *and* interrupt inside the same first fight; `resonance-weaving-system`'s own GDD (or a future revision) must confirm or correct it. |
| A2 | **There is no separate tutorial mode, no modal dialog, and no dismissible "teaching beat."** Onboarding is entirely a **content-curation and pacing layer** over the first region's already-existing encounter structure — which creature, which attack pool, which `power_tier`, in what order — never a distinct game state, screen, or gate the player interacts with directly. | Directly resolves Design Task 4. A separate tutorial mode is the exact thing mid-core/hardcore players (`game-concept.md`'s own Target Player Profile) resent, and the thing this game's own genre positioning (Path of Exile-adjacent depth) argues against pre-empting. Because standard encounters are already short and low-stakes (`combat-encounter-system` §2, §3.8), a veteran simply clears the curated early encounters at their own pace with zero forced friction — there is nothing to "skip" because nothing ever blocks input waiting for acknowledgment. |
| A3 | **A dedicated, consequence-free practice mode is explicitly deferred post-MVP**, not built here. | Resolves Design Task 3. Argued in full in §3.5 — `combat-encounter-system`'s Retreat fail-state (no death, no loss, short encounters) already delivers most of a practice mode's value for free, and the genuine remaining gap (rehearsing one specific attack in isolation, outside weighted-random selection) requires new `combat-encounter-system`-owned infrastructure this document has no authority to invent under its own file-discipline scope. |
| A4 | **`onboarding_completed` is a single global boolean**, persisted independently of any specific save — architecturally identical to `accessibility-settings-system`'s own `accessibility_settings.json` (its own A1/A2), not a new section inside `save-load-persistence.md`'s `save.json`. | Whether a *player* (not a save file) already knows how to play is a cross-save fact, the same category of fact `accessibility-settings-system` already argued preferences belong to (global, not per-profile, per that document's A2 and `game-concept.md`'s single-player/no-profile scope). Tying it to `save.json` would force it to reset on every new game, directly defeating Edge Case #4's requirement that a returning player is never re-taught. **Flagged gap, not self-edited**: neither `accessibility-settings-system.md` nor `save-load-persistence.md` currently reserves a field for this; per this session's file-discipline instruction (write only this file), that gap is documented here for a future centralized pass, following the exact precedent `input-targeting-system.md` and `accessibility-settings-system.md` set for their own equivalent corrections. |
| A5 | The **first region's expedition structure** (how many discrete encounters precede its boss, and the hub-return/expedition-sequencing UI around that) is owned by a not-yet-written region-progression system (most likely `region-mastery-automation-system`, per its already-established role as the automation/region-state owner). This document does not design that system — it only specifies a **binding content-pacing constraint** (§4 Formula 1, §7) that system must honor for the first region specifically. | `game-concept.md`'s Short-Term Loop already states expeditions chain "3–6 encounters," confirming the structure exists conceptually, but no GDD yet owns building it. This document cannot invent that system under its own scope; it can only pin where, within that already-implied range, the first region's boss sits, and flag the constraint forward. |
| A6 | The **one sanctioned departure from pure silent teaching** is a brief, non-modal, **iconographic** (never textual) input-prompt hint — e.g., a small cycle/confirm button glyph — shown once per action type, on first opportunity, fading on first successful use or after a short timeout. | Art-bible §7.2 explicitly classifies "input prompts" as **chrome iconography** (pre-learned symbols like a caret or a close-X), categorically distinct from **glyph-grammar iconography** (Source medallions, vulnerability rings), which the Master Rule forbids simplifying or explaining via text. A player still needs to discover *which physical button* does something — that is a control-scheme question, not a "what does this glyph mean" question, and art-bible already licenses chrome-tier affordances for exactly this purpose. This assumption draws the line the brief's "no modal glyph glossary" instruction leaves open: control-discovery hints are permitted; meaning-explanation is not. |
| A7 | **Interrupt usage is never mandated.** A player may complete the first region's boss having never landed a qualifying interrupt, and this is accepted as a legitimate outcome, not remediated by this document. | `combat-encounter-system` §3.5 already states block is "a legitimate, sometimes-correct call under pressure — not a mistake the player made because they didn't know better." Forcing interrupt usage (e.g., authoring an early interrupt-only attack) would contradict that document's own stance and Pillar 1's autonomy framing. §5 Edge Case #2 and §3.6 (the efficiency readout) together resolve this honestly rather than papering over it. |
| A8 | **Vow introduction is triggered by the player's first Forge visit after their first Mastery Transition**, not by a specific combat encounter or encounter count. | `game-concept.md`'s Flow State Design locks Vows as taught "before introducing Vow restrictions" only *after* the first region's basic loop, but doesn't specify a trigger. Vows are bound at the Forge (art-bible §2's Vow scar), a screen this document does not own — gating their introduction to "whenever the player naturally visits the Forge with something to bind" (rather than a forced combat-side restriction) respects that screen's own deliberately unhurried, player-paced mood ("time feels suspended; no timers, no urgency cues," art-bible §2) instead of overriding it with a combat-side timer. |

---

## 1. Overview

The onboarding & tutorial system is Resonance Hunter's answer to a genuinely hard problem: this
game asks a first-time player to learn a glyph language, weak-point targeting across two input
paths, telegraph reading, a three-tier defensive triad, part-break mechanics, a Resonance/Ultimate
economy, a boss encounter, and — immediately after — an entirely new automation/Forge/creature-job
layer, all inside roughly its first hour, with no modal tutorial and no text glossary permitted by
the game's own visual design thesis (art-bible's Master Rule: "read the world, don't just watch
it"). This document does not invent new mechanics; every skill it sequences already exists,
fully specified, in `combat-encounter-system`, `combat-hud`, `creature-ai-telegraph-system`, and
`input-targeting-system`. What this document owns is **order, pacing, and content curation**: which
creature the player fights first, how that creature's attacks are authored, how many encounters
separate the player from the first region's boss, and — critically — the explicit argument that
this sequencing is not incidental tutorial scaffolding but the deliberate *setup* for the game's
central emotional beat. The Mastery Transition (automation as reward) only means something if the
player has personally felt the labor being handed off; this document is what guarantees that labor
was real before the reward arrives.

## 2. Player Fantasy

> **The player never remembers being taught. They remember the moment the region stopped fighting
> back.**

The arc this document is responsible for is not "confusion, then instruction, then competence" —
it is **confusion dissolving into competence through play alone**, with no visible seam where
"tutorial" ends and "the real game" begins, because there never was a tutorial in the modal-dialog
sense. Concretely, this system exists to guarantee:

- **The first fight already feels like the whole game, just quieter.** A new player's first
  encounter uses the exact same state machine, HUD, targeting, and telegraph rules as every
  encounter that follows — only the *content* (which creature, how generous its timing) is turned
  down. There is no "training wheels" system to later feel the seam of removing. This is the direct
  mechanical expression of Csikszentmihalyi's flow-channel principle (challenge and skill ramp
  together, from near-zero) — the same principle `combat-encounter-system` §6 already flagged as
  this document's expected organizing idea.
- **Every "aha" belongs to the player, not to a tooltip.** The moment a new player realizes "the
  glowing broken ring is where I hit it" is a moment of their own pattern recognition, not a fact
  they were handed — because the game never hands it. This is Pillar 1 (Precision Over Reflexes)
  applied one level up: the player's very first skill is *learning to read*, and that skill is only
  real if it was earned the same way every later read is earned.
- **Losing early costs nothing and teaches everything.** A new player's first Retreat is not a
  failure state to shield them from — it is data delivered for free, at zero cost, exactly as
  `combat-encounter-system` §2 already promises for every encounter in the game. Onboarding does not
  soften this; it relies on it.
- **The boss is not a wall — it is a graduation the player didn't have to ask for.** By the time a
  first-time player reaches the first region's boss, they have already, through ordinary play,
  targeted a weak point, read a telegraph, tried each leg of the defensive triad, broken a part, and
  spent a full Resonance bar on an Ultimate — none of it because a screen told them to, all of it
  because the preceding fights were built to make those the natural, low-friction things to try.
- **The Mastery Transition is allowed to mean something, because the player earned the right to feel
  relief.** Art-bible §2 names this beat "coronation, not shutdown." That framing is a promise this
  document is directly responsible for keeping — automation cannot register as a reward for labor the
  player never actually performed. This is the system's single most important obligation, explored in
  full in §3.1.
- **Veteran players are never asked to sit through anything.** Because nothing here is a screen,
  a popup, or a forced pause, an experienced player simply plays faster through the same content — the
  system degrades gracefully to "slightly generous early encounters" for someone who needs no teaching
  at all, rather than degrading to friction.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the **order** in which the game's existing mechanics are first exposed to a
new player, the **content-authoring constraints** on the first region's early encounters (creature
part count, attack windup generosity, avoidance-option composition, `power_tier` progression) that
make that order happen without any new game state, the **minimum encounter count** before the first
region's boss becomes available, the **teaching mechanism** for glyph literacy (a design pattern,
not a new render layer), the **practice-mode** and **skippability** decisions, and the **onboarding
completion flag** and its returning-player behavior.

It explicitly does **not** own, and defers entirely to the cited document in every case:

- Any combat mechanic itself — targeting, telegraph timing, the defensive triad's windows, Resonance,
  damage math, the encounter state machine. All of it is exactly as specified in
  `combat-encounter-system`, `creature-ai-telegraph-system`, and `input-targeting-system`; this
  document only decides which *content* the player meets first.
- Any HUD rendering — `combat-hud` owns every glyph, pip, and ring this document relies on being
  legible; this document adds no new HUD element.
- The Forge's screen, Vow-binding ceremony, or automation-configuration UI — all not-yet-written
  systems this document coordinates with (§6) but does not design.
- Creature template authoring beyond the pacing constraints in §4/§7 (exact part shapes, exact
  attack clip art) — `creature-data-schema` and `animation-rig-system`'s domain.

### 3.1 The Central Design Principle: Sequencing Against the Game's Own Thesis

The task this document exists to solve is not "how do we explain the mechanics" — it is **"how do
we make sure the player has done enough real work that giving it up (to automation) reads as a
gift, not a formality."** Automation is Pillar 2's "earned, not assumed"; the Mastery Transition is
the moment that pillar becomes a felt experience rather than a rule. If a new player could reach
the boss and the automation unlock in, say, ninety seconds of barely-engaged play, the transition
would have nothing to transition *from* — there would be no labor to be relieved of, and the
"coronation, not shutdown" mood art-bible §2 promises would collapse into an unearned checkbox.

This document's resolution is **not a skill gate**. It does not check whether the player performed
well, landed an interrupt, or avoided every hit — that would contradict Pillar 1's own autonomy
framing and risk trapping a struggling player behind a wall this document explicitly argues against
building (§3.2). Instead, the resolution is a **minimum-exposure gate**: the first region's boss is
placed after a fixed minimum number of standard encounters (§4 Formula 1, §7), each of which is
content-authored (§3.4) so that, in the ordinary course of simply playing them, a first-time player
organically performs every foundational action at least once — targets a part, watches a telegraph
resolve, attempts a defense, breaks a part, and spends a full Resonance bar. The gate is on *reps
taken*, never on *reps succeeded*. A player who fumbles every single defensive attempt in the first
four encounters still reaches the boss having genuinely tried, genuinely read telegraphs, and
genuinely felt the game's texture — which is exactly the labor the Mastery Transition needs to have
existed in order to mean something on the other side of it. This is the direct, mechanical answer to
the central design challenge: **the boss is not gated on mastery, it is paced against exposure**, and
exposure is what the reward is measured against.

### 3.2 The No-Modal-Tutorial Decision (Design Task 4)

**Decision: there is no separate tutorial mode. Teaching happens exclusively through unavoidable,
brief, content-curated first encounters.**

Three options were weighed:

1. **A dedicated tutorial mode** (a distinct game state, walking the player through scripted beats
   with prompts). Rejected: this is the single thing this game's own target audience (mid-core to
   hardcore, per `game-concept.md`'s Target Player Profile, explicitly comparing itself to Path of
   Exile) is most likely to resent, and it requires new state/UI this document's scope does not
   license it to invent.
2. **A skippable tutorial mode** (same as above, with a skip button). Rejected for the same reason,
   plus a UX cost: a skip button is itself a decision surface a new player has to parse ("do I know
   enough to skip this?") — the exact kind of friction a confident-feeling first hour should never
   introduce.
3. **No separate mode at all — teach entirely through the first region's own, curated content.**
   **Selected.** The first few encounters *are* the tutorial, indistinguishable in structure from
   every encounter that follows, differing only in authored generosity (§3.4, §4). Nothing is ever
   skipped because nothing ever blocks; a skilled player simply clears these encounters at normal
   speed, in seconds, the same way they'd clear any short standard encounter.

This resolves cleanly against `combat-encounter-system` §2's own framing ("encounters are short,
minutes-long... failure costs little time") — the entire game is already structurally low-friction
enough that a curated first few encounters cost a veteran nothing measurable, while giving a new
player exactly the low-stakes reps they need. No new skip mechanism, no new UI state, no new
decision for the player to make about whether to engage with "the tutorial" — because there isn't
one.

### 3.3 Teach by Doing: How the Glyph Language Is Learned Without a Glossary (Design Task 2)

Art-bible's Master Rule is explicit: "every mechanically important state renders as a distinct
glyph — never a color swap alone," and a modal glossary explaining that grammar would directly
betray it (a glyph explained in a text box is no longer being *read*, it is being *told*). This
document relies on four properties of the glyph system that are already locked upstream, and adds
exactly one new thing: deliberate first-exposure content curation.

1. **The grammar is self-teaching by construction.** Art-bible §3.2 locks one container shape per
   category — a closed ring for a Source medallion, an open/broken ring for a vulnerability glyph —
   reused, unmodified, on every single creature in the game (art-bible §3.4: "content reuses the
   glyph grammar unmodified"). There is no per-creature glossary to learn, because the first
   creature *is* the glossary. Once a player has read one open ring, they have read all of them.
2. **The vulnerability glyph is pre-attentively salient by design, not by tutorial emphasis.**
   Art-bible §3.5 locks the vulnerability glyph as the loudest, and only continuously animated,
   shape in the combat frame — brighter and busier than the creature's own silhouette, brighter than
   any HUD element. This document does not add emphasis; it relies on emphasis that already exists
   for every encounter in the game, and simply ensures the *first* encounter's creature is simple
   enough (§3.4, §7) that this salience has nothing to compete with.
3. **Feedback is instant and legible, which closes the loop without narration.** The instant a
   player commits an attack against the correct part, the part's crack visibly deepens
   (`creature-data-schema`'s `PartState.current_stage`, rendered per art-bible §3.1's seam grammar)
   and the Resonance medallion visibly gains a wedge (`combat-hud` §3.4). A new player does not need
   to be told "that worked" — they see it happen in the same frame they acted.
4. **Repetition across encounters is what turns one observation into a rule.** Because the container
   grammar never varies, a player's second and third encounters with an open-ring glyph confirm,
   rather than re-teach, the first one — this is inductive pattern learning across reps, the same
   mechanism the game asks the player to use for reading attack patterns (`creature-ai-telegraph-
   system` §2: "a fight the player learns to read"), applied one level earlier to the glyph grammar
   itself.

**The one sanctioned exception (Assumption A6)**: brief, iconographic, non-modal input-prompt hints —
never textual, never explaining a glyph's *meaning*, only surfacing which physical input performs an
already-available action (e.g., a small confirm-button glyph fading in the first time a target is
selectable via cycle-and-confirm, and fading out on first successful use or after a short timeout).
Per art-bible §7.2, input prompts are explicitly chrome iconography, not glyph-grammar content — the
line this document holds is: **control discovery may use icons; glyph meaning may never use text.**

A full, opt-in control-scheme reference remains available at any time via the Settings menu
(`accessibility-settings-system` §3.6's own "tutorial replay" coordination note) for any player who
wants explicit text — but it is never surfaced automatically, never modal, and never required to
progress.

### 3.4 The Full Teaching Sequence (Design Task 1)

The sequence below is fixed in the order specified by the task brief. Every entry states **what** is
taught, **why it sits at this point in the order**, and **the mechanism** — always a content-pacing
recommendation over an already-existing mechanic, never a new one.

**Stage 1 — Glyph literacy + weak-point targeting (both input paths).** *Encounter 1, from the first
`ENGAGING` state.*

- **Content constraint**: the first encounter's creature is authored at the low end of the standard
  tier's own already-locked part-count range (art-bible §5.6: "3–5 seam-divided parts") — recommend
  exactly 3 parts, `power_tier = 1` — so the vulnerability glyph has minimal competition for
  attention (§3.3.2) and a first click or cycle-confirm has an obvious, low-ambiguity target.
- **Why first**: nothing else in the game is legible without it. A player cannot read a telegraph,
  attempt a defense, or feel a part-break without first understanding "the open ring is what I
  aim at" — this is the literal precondition for every subsequent stage.
- **Mechanism**: passive, observational (§3.3). Because `input-targeting-system` already guarantees
  full parity between mouse and cycle-and-confirm (§3.9 there), whichever device the player has
  connected is the one that teaches this — there is no mouse-first or gamepad-first branch to author.
  Basic attacks auto-fire from the instant `ACTIVE` begins regardless of whether the player has aimed
  yet (`combat-encounter-system` §3.3) — a new player loses nothing by needing a few seconds to
  orient before their first deliberate input.
- **Advances when**: the player's first `TargetConfirmed` event (either input path, per
  `input-targeting-system` §3.1) lands on a `can_be_vulnerable` part. Not a hard gate — the encounter
  proceeds identically whether or not this happens quickly; it is simply the moment this stage's
  teaching goal is satisfied.

**Stage 2 — Telegraph reading.** *Same first encounter, the creature's first attack.*

- **Content constraint**: the first attack in encounter 1's pool is authored with `base_windup_ms`
  toward the upper end of the authored safe range (recommend 2400ms against the documented 600–
  3000ms range, `creature-ai-telegraph-system` §7) and `avoidance_options = [block, dodge]` — no
  interrupt requirement on this specific attack, keeping the very first read to the two
  lowest-commitment responses.
- **Why second**: targeting must exist before there is anything to defend against; telegraph reading
  is the next-simplest read (a single glyph brightening on a fixed curve, per `creature-ai-telegraph-
  system` §3.7) and is a strict prerequisite for every leg of the defensive triad.
- **Mechanism**: the non-tunable 600ms accessibility floor and the default-on audio pre-cue
  (`creature-ai-telegraph-system` §3.8–3.9, `accessibility-settings-system` §3.4) already guarantee
  this read is possible for every player regardless of skill or sensory profile — this document adds
  only the content choice to make the very first instance of it maximally generous, not a new
  mechanism.
- **Advances when**: the first telegraph in the encounter resolves (whether avoided or not — a hit
  taken is legible feedback too, per `combat-hud` §3.3's HP pip loss).

**Stage 3 — The defensive triad (block, dodge, interrupt — and why interrupt pays most).**
*Encounter 1's second and later attacks, continuing through encounters 2–3.*

- **Content constraint**: by the pool's second distinct attack (which the player will see within
  encounter 1 if it runs long enough, and certainly by encounter 2), `interrupt` becomes a valid,
  non-exclusive `avoidance_option` alongside block and dodge. No attack in the first three encounters
  is authored `avoidance_options = [interrupt]` (interrupt-only) — per `creature-ai-telegraph-system`
  §5 Edge Case #10, that authoring pattern is "typically reserved for late desperation-phase attacks,"
  and using it this early would force a mechanic the player may not yet have equipped confidence to
  use (see A7).
- **Why here**: this is the earliest point at which all three legs are simultaneously meaningful —
  targeting and telegraph reading are prerequisites for all three, and (per A1) the player already
  has an interrupt-qualifying ability equipped from turn one, so nothing blocks trying it early.
- **Mechanism — teaching "interrupt pays most" without a lecture**: every one of the triad's three
  outcomes already produces a *differently sized* piece of legible feedback with zero new HUD work —
  `combat-hud` §3.10 confirms block/dodge/interrupt purely through existing elements reused at
  different magnitudes (a smaller vs. larger Resonance wedge jump, `combat-encounter-system` §3.6's
  `resonance_per_block/dodge/interrupt` = 3/8/15), and a successful interrupt's exclusive
  `interrupt_break_progress_multiplier` (2.0×, `creature-ai-telegraph-system` §4 Formula 4) makes the
  struck part's crack visibly jump further than an equivalent dodge or block would. A player who
  tries all three at least once across these encounters sees this differential directly, every time,
  with no telling required — the pattern becomes obvious through repetition (§3.3.4), not
  explanation.
- **Advances when**: the player has attempted at least one of each type across encounters 1–3. Not
  hard-required (§5 Edge Case #1, #2) — a player who only ever blocks still proceeds normally.

**Stage 4 — Part-breaks and how they change loot.** *Whenever the first part reaches `broken`,
typically within encounters 1–2.*

- **Mechanism**: fully passive. A broken part is already a permanent, visible state change on the
  creature's own silhouette (`creature-data-schema`'s `current_stage`, art-bible §3.1's seam grammar)
  — there is nothing this document needs to add. The loot consequence
  (`part_break_loot_bonus_percent`, `combat-encounter-system` §4 Formula 4) is realized at the
  encounter's Kill outcome, which the player experiences as "I broke things, and the reward felt
  bigger" without needing the formula explained.
- **Why here**: this requires nothing the player hasn't already been doing since Stage 1 (landing
  hits on a target part); it is not a distinct action to teach, only a distinct consequence to
  notice.

**Stage 5 — Resonance and the Ultimate.** *Naturally, once `current_resonance` first approaches
`resonance_cap`, typically by the end of encounter 1 or during encounter 2.*

- **Content constraint**: none beyond ensuring the first encounter contains enough skilled-action
  opportunities (a landed hit, an avoided attack, a part-break) that a first-time player reaches a
  castable Ultimate within their first one to two encounters, rather than stalling for many fights.
  Given `combat-encounter-system` §3.6's base gain table, this is already the expected pace at
  default tuning and needs no special authoring.
- **Mechanism**: fully passive, driven by `combat-hud`'s always-visible Resonance medallion
  (chunk 3 of that document's four-chunk glanceable model, §3.2) filling through ordinary play. The
  first Ultimate cast is a moment of player-initiated curiosity ("this is full, what happens if I use
  it"), not a prompted action.

**Stage 6 — The boss.** *After the region's minimum encounter count (§4 Formula 1) is met.*

- **Mechanism**: the boss introduces the `Phase` system's `guaranteed_vulnerability_on_entry`
  mechanic for the first time — but this requires no new literacy, because it is mechanically the
  same "avoid correctly, get a reward window" pattern the player has already practiced three times
  over via the defensive triad; a phase transition is simply a *freebie* version of a pattern already
  known (`creature-ai-telegraph-system` §2: "the fight raising its own stakes, not a wall").
  Recommend the first region's boss author 3 phases (within `creature-ai-telegraph-system` §3.3's
  own recommended boss range of 3–4), each phase transition serving as the player's first exposure to
  an escalating, ceremonial fight without requiring any concept they haven't already used.

**Stage 7 — The Mastery Transition.** *Immediately on the boss's core-part break
(`combat-encounter-system` §3.9 priority 1).*

- **Mechanism**: none owned by this document — this is the cinematic art-bible §2 fully specifies
  (glyph inversion, expanding warm light sweep, "coronation, not shutdown"). This document's entire
  contribution is upstream of this moment: §3.1 argues, and §4 Formula 1 enforces, that enough real
  manual play preceded it for the beat to be earned rather than arbitrary.

**Stage 8 — Automation stages.** *Immediately on return to the region hub, same session.*

- **Mechanism**: passive and diegetic. The first automation structure — the auto-attack ward-post
  (art-bible §6.7) — appears as standing infrastructure in the Region View the instant control
  returns, in the same "read the world" register as everything else in this sequence: the player
  sees the structure exist rather than reading a checkbox that says "Stage 1 unlocked." This document
  recommends (flagged forward, §6) that whichever system owns the automation-configuration screen
  present it as the *direct, contiguous* next screen after the transition resolves — not deferred to
  a separate later menu visit — so the causal link between "I just won this" and "this is now working
  for me" stays unbroken.

**Stage 9 — The Forge.** *Same session, following automation exposure — matching `game-concept.md`'s
own stated Session-Level beat order ("boss kill → forge pass → team assignment").*

- **Mechanism**: passive. The player's first Forge visit is a **merge-only** action (per the MVP
  scope, `game-concept.md`) using the loot they just earned — no new literacy beyond "combine two
  items," which the Forge's own future screen design owns.

**Stage 10 — Creature jobs.** *Same session, alongside or immediately after the first Forge visit.*

- **Mechanism**: passive and diegetic, reusing art-bible §5.3's already-locked work-loop spec. The
  player's first job assignment is naturally the just-bound boss creature (per Pillar 5 — "the
  creature keeps its identity... bound to a job, not erased into a trophy," art-bible §2), and the
  moment the player assigns it, they watch it begin performing its role's distinct work-loop verb
  (striking/bracing/orbiting/fidgeting/breathing, art-bible §5.3) — the assignment's *effect* is the
  explanation, with zero text needed. This document recommends (flagged forward, §6) that the first
  assignment's options be framed by whichever automation stage is currently unlocked (e.g., only
  Attacker is meaningfully relevant if only auto-attack exists yet), narrowing a five-way decision
  down to the one that currently matters, rather than presenting the full role list's implications
  all at once.

**Stage 11 — Vows.** *First Forge visit after the Mastery Transition where Vow-binding is offered
(Assumption A8) — deliberately last, per `game-concept.md`'s locked Flow State Design.*

- **Why last**: this is not incidental sequencing — it is the one entry in this list the source
  material explicitly locks the position of ("the first region teaches weak-point targeting and
  telegraph reading... before introducing Vow restrictions," `game-concept.md`). Vows are a
  *build-affecting restriction* (Pillar 4) layered on top of a combat loop the player must already
  trust; introducing a restriction before the player has a working mental model of the loop it
  restricts would make the restriction unreadable — the player would have no baseline to feel the
  trade-off against.
- **Mechanism**: fully deferred to the Forge's own already-locked ceremony. Art-bible §2 specifies the
  Vow scar visibly searing into an item's silhouette in real time as the player commits, at a
  deliberately unhurried pace ("time feels suspended; no timers, no urgency cues"). This document
  adds no new teaching layer on top of that — the ceremony itself is already built to make the
  trade-off legible.

### 3.5 The Practice Mode Question (Design Task 3)

**Decision: a dedicated low-stakes practice mode is explicitly deferred post-MVP.**

`accessibility-settings-system` §3.6 (A7 there) recommends one as a cognitive-accessibility
affordance and explicitly passed the decision to "whoever designs `combat-encounter-system` next" —
that document, now written, did not add one; only a zero-cost manual Retreat (§3.9 there). The
recommendation has landed on this document by default, and it is resolved here rather than passed
along again.

**The honest case for redundancy**: `combat-encounter-system`'s existing fail state already delivers
most of what a practice mode would offer. Encounters are short (`combat-encounter-system` §2,
"minutes-long"), Retreat costs no items, currency, or roster progress, and the player's HP is floored
at 1 rather than reset to a punishing state (§5 Edge Case #7 there). A new player who wants to
"try that attack again" can already do so, cheaply, by re-engaging the same or a similar early
encounter. In this genuine sense, the game **already is** low-stakes by default — a separate
consequence-free mode does not add a fundamentally new capability so much as it removes the small
remaining friction of re-approaching a fresh encounter each time.

**The honest remaining gap**: what Retreat-and-retry cannot offer is **isolated repetition of one
specific attack**. `creature-ai-telegraph-system` Formula 6's weighted-random attack selection means
a player cannot currently choose "give me that exact telegraph again" — they get whatever the pool
rolls next. For a player specifically struggling with one pattern (not the general concept of
reading telegraphs, but one particular creature's one particular tell), a true practice mode would
be strictly better.

**The call**: the redundancy argument wins for MVP, on cost grounds as much as design grounds. A true
"pick one attack, replay it with zero stakes" mode requires new `combat-encounter-system`-owned
infrastructure (an isolated encounter variant with no reward hook, no weighted selection, no
state-machine outcome beyond "try again") that this document has no authority to invent under its
own file-discipline scope, and MVP's own roster is intentionally small (`game-concept.md`: "one
creature line, 2–3 evolution branches") — meaning "which specific attack am I struggling with"
is a much smaller problem at MVP scope than it will become once the creature roster grows. This
recommendation is flagged forward (§6) to `combat-encounter-system` for reconsideration once the
roster is large enough that the gap becomes a real cost.

### 3.6 The Efficiency Readout as a Teaching Tool (Design Task 5)

`game-concept.md` calls `active_efficiency_percent` "a legible mastery readout"; `combat-encounter-
system` §4 Formula 7 defines it. This document's job is narrower: **when is this number first shown
to a new player, and why does that placement teach the active/idle trade-off without a lecture.**

The number is meaningless in isolation before automation exists to compare against — there is
nothing to be "efficient relative to" until an automated baseline exists. This document recommends
(flagged forward, §6, since it does not own the rendering surface) that `active_efficiency_percent`
first be surfaced **at the automation-configuration moment immediately following the first Mastery
Transition** — the exact moment the player is deciding how much of this region they still want to
play manually versus hand off. This placement is deliberate:

- It appears exactly when it is decision-relevant, never before. A number with no decision attached
  to it is trivia; a number attached to "should I keep manually farming this, or let it run" is
  information the player actually needs in that instant.
- Because the floor (50%) and cap (300%, hard-locked, `combat-encounter-system` §7) are both visible
  in the same readout, the player learns **both halves of Pillar 3 at once, from one number**: idle
  is never worthless (the floor is a real percentage, not zero), and active is meaningfully better
  when played well (the 180–220% "focused active play" band, per that document's own worked
  examples, reads as clearly, not marginally, ahead).
- Because `IR` (Interrupt Rate) carries the larger of the two bonus scalars in Formula 7
  (`ir_bonus_scalar` = 1.2 vs. `vws_bonus_scalar` = 0.8), a player who — per A7 — never tried
  interrupt during the first region will simply see a lower number here than a player who did. This
  is the natural, self-correcting resolution to A7's accepted gap: the game does not lecture the
  player about interrupt's value, it lets the number they already care about quietly reflect it,
  creating an organic incentive to experiment further without ever requiring it.

No text explanation of the formula is proposed or needed — the number is legible as "a percentage
compared to automatic," which the game's own target audience (mid-core to hardcore, comfortable with
systemic numbers, per `game-concept.md`'s Target Player Profile) reads fluently on sight.

### 3.7 Accessibility: Both Input Paths, Keyboard-Only Completability (Binding, Design Task 6)

- **Both input paths are taught by the same content, not by a branching tutorial.** Because every
  stage in §3.4 relies on mechanics `input-targeting-system` already guarantees are fully at parity
  (§3.9 there: whichever device fires most recently simply wins, with no special-cased "device
  switch" event), this document never authors mouse-specific or gamepad/keyboard-specific content.
  There is exactly one first encounter, one attack pool, one pacing curve — read identically by
  whichever input method produced the player's actions.
- **No onboarding-specific instruction is ever phrased for one input method.** Because this document
  adds zero text (§3.3) beyond the one sanctioned iconographic exception (A6), and that exception is
  itself rendered as a symbol representing whichever binding is actually active for the connected
  device (via `accessibility-settings-system`'s `keybind_map`, §3.3 there), there is no possible
  phrasing that could assume a mouse. A cycle-and-confirm player is shown a cycle/confirm icon; a
  mouse player is shown nothing beyond their own cursor, exactly as `input-targeting-system` §3.11
  already specifies for the selection ring (rendered only for the cycle path, per that document's
  Assumption A7).
- **The entire onboarding sequence — Stage 1 through Stage 11 — is keyboard-only completable**, by
  direct inheritance: every combat action in Stages 1–7 is proven keyboard/gamepad-completable by
  `input-targeting-system` §8 AC1 and `combat-encounter-system` §8 AC3 (both already-verified
  criteria this document does not re-derive), and Stages 8–11 (automation config, Forge, job
  assignment, Vow-binding) are bound, per `accessibility-settings-system` §3.7 item 3, to be reachable
  by keyboard or gamepad navigation alone on every future UI system this document coordinates with.
  This document's own Acceptance Criteria (§8) test the sequence as a whole rather than re-testing
  each dependency's already-covered pieces in isolation.

### 3.8 Returning Players and Onboarding Replay

- Onboarding's content curation (§3.4, §4) applies **only** while `onboarding_completed = false`
  (Assumption A4). The first time a first region's boss is defeated in any save, `onboarding_
  completed` is set to `true` and never automatically reset.
- **A new game started after `onboarding_completed = true` does not re-force any of the content
  curation in §3.4.** The first region's encounters are authored with standard `power_tier` and
  windup values from encounter 1 — the same content a mastered player would expect on any later
  region — directly resolving Edge Case #4.
- **Replay, opt-in only**: per `accessibility-settings-system` §3.6's own coordination note ("if that
  system implements a replay entry point, this settings menu should surface access to it"), this
  document specifies a single Settings-menu action, "Replay Onboarding," that resets `onboarding_
  completed` to `false`. This has no effect on the player's current save's already-mastered region
  state — it only affects the content curriculum applied the *next* time a new game is started. This
  gives a concrete, minimal answer to that coordination note without inventing a mid-save practice
  mode this document has already declined to build (§3.5).

### 3.9 Runtime State (Conceptual — Owned by This Document)

```
OnboardingState {
  onboarding_completed: bool          // global, persisted independently of any save (A4)
}
```

That is the entirety of the state this document introduces. Per-encounter exposure tracking (has the
player attempted a block, a dodge, an interrupt) is **not** persisted at all — it exists only as the
soft pacing justification for §3.4's content curriculum and §4 Formula 1's gate, never as a value any
other system reads or writes. This document deliberately owns as little state as possible, consistent
with its role as a content-pacing layer over already-existing systems, not a new tracked-progress
system in its own right.

## 4. Formulas

This document is a content-pacing and curriculum layer, not a simulation system — its math is
genuinely thin, and that is stated plainly rather than padded. The two formulas below are real and
load-bearing (they are what makes §3.1's central argument enforceable rather than aspirational); a
third is included only because it is a real, testable gate this document's Edge Cases and Acceptance
Criteria both depend on.

### Formula 1 — First-Region Boss Availability Gate

```
boss_available = (encounters_completed_in_region ≥ pre_boss_encounter_count_minimum)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `encounters_completed_in_region` | int, external input | ≥ 0 | Count of standard encounters completed (any outcome — Kill, Capture, or Retreat all count, since the gate is on exposure, not success, per §3.1) in the current region's expedition. Assumed tracked by the not-yet-written region-progression system this document coordinates with (Assumption A5) — this document reads it, never owns it. |
| `pre_boss_encounter_count_minimum` | int, tuning knob | 3–6 (default 4) | §7. Pinned to the low-middle of `game-concept.md`'s own already-stated "3–6 encounters per expedition" range, specifically for the first region — see rationale in §3.1 and §7. |
| `boss_available` | bool | — | Output. Whether the boss encounter may be engaged. |

**Worked example**: a first-time player has completed 3 standard encounters in the first region at
default tuning (`pre_boss_encounter_count_minimum = 4`): `boss_available = (3 ≥ 4) = false`. After
completing a 4th encounter (any outcome): `boss_available = (4 ≥ 4) = true`.

**This is deliberately not a skill check** — every input to this formula is a count, never a
success/failure judgment, per §3.1's explicit ruling that the gate is on reps taken, not reps won.

### Formula 2 — Windup Generosity Ramp (Content-Authoring Inputs to an Existing Formula)

This is not a new formula — it is a worked demonstration of `creature-ai-telegraph-system` §4 Formula
1 (`windup_duration_ms = max(TELEGRAPH_WINDUP_FLOOR_MS, round(base_windup_ms × (1 −
windup_difficulty_scalar × (power_tier − 1))))`), applied to this document's own recommended
content-authoring inputs across the first region's pre-boss encounters, to make the difficulty ramp
concrete and testable rather than asserted.

| Encounter | Recommended `power_tier` | Recommended `base_windup_ms` | `windup_difficulty_scalar` (default) | Resulting `windup_duration_ms` |
|---|---|---|---|---|
| 1 | 1 | 2400 | 0.05 | `max(600, round(2400 × (1 − 0.05×0))) = 2400ms` |
| 2 | 2 | 2000 | 0.05 | `max(600, round(2000 × (1 − 0.05×1))) = 1900ms` |
| 3 | 3 | 1800 | 0.05 | `max(600, round(1800 × (1 − 0.05×2))) = 1620ms` |
| 4 (final, pre-boss) | 3–4 | 1800 | 0.05 | `max(600, round(1800 × (1 − 0.05×3))) = 1530ms` |
| Boss | 3–4 (per phase) | 1800 (typical) | 0.05 | Converges toward the game's normal regional baseline (~1440–1620ms) — no special generosity from this point forward |

**Output range**: every value above sits comfortably above the non-tunable 600ms accessibility floor
(`creature-ai-telegraph-system` §3.8) even before this document's own generosity — the ramp is a
*feel* choice layered on top of an already-safe floor, not a substitute for it. By encounter 4, timing
has already converged close to the region's ongoing normal difficulty, so the boss introduces no
timing discontinuity relative to what the player has just been reading.

**Why this table, not a new curve function**: the ramp is fully achieved by choosing `power_tier` and
`base_windup_ms` per encounter as ordinary content-authoring decisions — `creature-ai-telegraph-
system`'s existing Formula 1 already produces the shrinking-windup curve automatically once those
inputs are chosen. This document adds no new scaling mechanism; it only recommends *which* inputs to
author for the first four encounters specifically.

### Formula 3 — Curriculum Applicability (Returning-Player Toggle)

```
apply_curated_pacing = NOT onboarding_completed
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `onboarding_completed` | bool, persisted (§3.9, A4) | — | Global, cross-save flag. |
| `apply_curated_pacing` | bool | — | Output. When `true`, §3.4's content constraints and Formula 1's gate apply to the current new game's first region. When `false`, the first region is authored with standard (non-curated) `power_tier`/windup/avoidance-option values from encounter 1, and Formula 1's gate does not block the boss. |

**Worked example**: a first-time player (`onboarding_completed = false`) starts a new game →
`apply_curated_pacing = true`. That same player later starts a second new game (still `onboarding_
completed = true` from having finished the first) → `apply_curated_pacing = false`, unless they have
explicitly used "Replay Onboarding" (§3.8) since, which resets the flag before this evaluation runs.

## 5. Edge Cases

1. **A player never performs a given mechanic during its intended teaching window (e.g., never
   blocks, never uses cycle-and-confirm despite owning a gamepad) and then struggles later because
   they don't know it.** **Ruling**: no hard failure is possible. This document defines no skill gate
   (§3.1, §4 Formula 1) — every stage in §3.4 "advances" on exposure, not success, and the encounter
   the player is struggling with remains a free, zero-cost Retreat away from a fresh attempt
   (`combat-encounter-system` §3.8–§3.9). The same safety net that makes the whole game low-stakes
   absorbs an under-prepared player meeting a harder fight; no additional remediation is built or
   needed.
2. **A player masters the first region and defeats its boss without ever landing a qualifying
   interrupt** (a whole mechanic never used by experience). **Ruling**: accepted, not remediated
   (Assumption A7). This is explicitly consistent with `combat-encounter-system` §3.5's own framing
   of block as a legitimate tactical choice, not a failure to correct. The player is not penalized
   at the boss, and the natural incentive to eventually try interrupt is the efficiency readout
   (§3.6) — a lower `active_efficiency_percent` than an interrupt-using peer, discovered
   organically, never lectured.
3. **A player switches input device mid-tutorial** (e.g., starts encounter 1 on mouse, switches to
   gamepad partway through). **Ruling**: no special handling exists or is needed. This document's
   §3.4 stage-advancement conditions are tracked by **action outcome** (a `TargetConfirmed` event
   landed, a defensive input succeeded), never by which input path produced it — exactly mirroring
   `input-targeting-system` §3.9's own "whichever event fires most recently simply wins, no device-
   switch event" rule. Switching devices mid-encounter changes nothing about which teaching stage is
   considered satisfied.
4. **A returning player starts a new game.** **Ruling**: onboarding is **not** re-forced (§3.8, §4
   Formula 3). `onboarding_completed` is a global flag independent of any specific save; once set, a
   new game's first region uses standard content from encounter 1, and Formula 1's boss-availability
   gate does not apply. A player who explicitly wants to re-experience the curated pacing may do so
   via the opt-in "Replay Onboarding" Settings action (§3.8), which affects only the *next* new
   game, never the player's current save.
5. **A player owns only a mouse, or only a keyboard/gamepad, never both.** **Ruling**: not a gap.
   Because both input paths are taught by identical content (§3.7) and full parity between them is
   already guaranteed by `input-targeting-system` (its own §8 AC1), a player who never touches the
   other input method simply never generates the content for the input-prompt hint tied to it
   (Assumption A6) — nothing is lost, since that hint only ever appears for the device the player is
   actually using.
6. **A player re-equips a different ability loadout at the Forge after the first region, removing
   their original interrupt-qualifying ability before ever using it, then re-equips it later.**
   **Ruling**: irrelevant to this document. Onboarding's content curriculum (§3.4) applies only to
   the first region and only while `apply_curated_pacing = true` (Formula 3); it tracks no persistent
   per-ability exposure state (§3.9) and imposes no requirement on loadout composition after the
   first region concludes.
7. **A player triggers "Replay Onboarding" mid-session, with an already-in-progress or already-
   mastered save active.** **Ruling**: the toggle only ever affects `onboarding_completed`
   (Formula 3's input), which is only evaluated when a *new game* begins (§3.8). It has no effect on
   the player's current save's region-mastery state, creature roster, or any other in-progress
   progress — it cannot be used to "undo" a completed region.
8. **Rare-creature capture-without-killing** (deferred post-MVP per `game-concept.md`) as an
   alternative first bound-creature source, ahead of the first Mastery Transition. **Ruling**: not
   applicable in MVP scope. Because that mechanic does not exist in the currently locked design, this
   document's Stage 10 (creature jobs, §3.4) correctly assumes the first bound creature is always the
   boss itself; this ruling is flagged for reconsideration only if/when `rare-creature-capture-system`
   is authored.

## 6. Dependencies

### Depends On

- **`combat-encounter-system.md`** — every stage in §3.4 sequences that document's already-locked
  state machine (`ENGAGING`/`ACTIVE`/`RESOLVING`/`COMPLETE`), Retreat fail-state (§3.5's practice-mode
  argument relies on it directly), the defensive triad's mechanics (§3.5 there), Resonance/Ultimate
  economy (§3.6), and `active_efficiency_percent` (§4 Formula 7, consumed by name in §3.6 here). This
  document reads that system's exposed state and events; it writes nothing to it.
- **`combat-hud.md`** — Stage 1–5's "teach by doing" mechanism (§3.3, §3.4) relies entirely on that
  document's already-locked glanceable hierarchy (its own §3.2 four-chunk model) and damage-taken/
  avoidance confirmation design (§3.10 there) being legible without this document adding anything new.
- **`creature-ai-telegraph-system.md`** — Formula 2's worked ramp is a direct, unmodified application
  of that document's own §4 Formula 1; §3.4's content-authoring recommendations (windup generosity,
  `avoidance_options` composition, phase count) are all authored *against* that system's schema, never
  redefining it. **Flagged bidirectionality gap, not self-edited**: that document's own §6 "Depended
  On By" list (`combat-encounter-system`, `combat-hud`, `audio-system`) does not currently include
  this document, per this session's file-discipline instruction (write only this file); this gap is
  documented here for a future centralized pass, following the exact precedent set elsewhere in this
  project (e.g. `input-targeting-system.md`'s own `animation-rig-system` correction).
- **`input-targeting-system.md`** — §3.7's "both input paths taught identically" claim, and §3.4
  Stage 1's targeting mechanism, both rest entirely on that document's already-verified parity
  guarantees (§3.9, §8 AC1). **Flagged bidirectionality gap, not self-edited**: that document's own
  §6 "Depended On By" list (`accessibility-settings-system`, `combat-encounter-system`) does not
  currently include this document either, for the same reason and with the same fix deferred to a
  future centralized pass.
- **`accessibility-settings-system.md`** — §3.5's practice-mode decision directly resolves a
  recommendation that document's own A7 explicitly passed forward; §3.8's "Replay Onboarding" action
  directly fulfills that document's own §3.6 coordination note ("if that system implements a replay
  entry point, this settings menu should surface access to it"); §3.3's input-prompt-hint mechanism
  reads that document's `keybind_map` to render the correct icon per active device. This document is
  the system that document's own §3.6 already names by ID
  (`onboarding-tutorial-system`, `systems-index.md` #24) and coordinates with correctly.

### Depended On By

None — this is a leaf system. Nothing in the currently locked design consumes this document's own
state (`onboarding_completed`) or contract.

### `systems-index.md` Gaps (flagged, not self-edited)

Per this session's file-discipline instruction, `design/gdd/systems-index.md` is not edited by this
document. One gap is flagged here for a future centralized update, following the same precedent
`input-targeting-system.md` and `accessibility-settings-system.md` set for their own equivalent
corrections:

1. `systems-index.md`'s Dependency Map currently lists `onboarding-tutorial-system` as depending only
   on `combat-encounter-system` and `combat-hud`. This document establishes three additional,
   equally load-bearing dependencies — `creature-ai-telegraph-system`, `input-targeting-system`, and
   `accessibility-settings-system` — all documented in full above and not yet reflected there.

### Adjacent Systems (informational, not a dependency in either direction)

- **`resonance-weaving-system`** (already written, not read this session per this document's own
  dependency scope) — Assumption A1's starter-loadout claim (an equipped, interrupt-qualifying
  ability from turn one) is made against this system and should be confirmed or corrected by a future
  revision of either document.
- **`vow-condition-tracking.md`** (already written) — Stage 11 (§3.4) defers entirely to that
  system's already-locked Vow ring rendering; this document adds no new teaching layer for it.
- **`game-concept.md`** — Pillar 1 (autonomy in the defensive-triad framing, §3.4 Stage 3), Pillar 2
  (the entire basis for §3.1's central argument), Pillar 3 (the basis for §3.6's efficiency-readout
  reasoning), the Flow State Design section (the locked intent this entire document implements), and
  the Session-Level Core Loop's "boss kill → forge pass → team assignment" ordering (directly
  sequenced in §3.4 Stages 8–10).
- **`design/art/art-bible.md`** — the Master Rule and §3.2's glyph grammar (§3.3's entire teaching
  mechanism), §2's Mastery Transition mood (§3.1, §3.4 Stage 7), §5.3's work-loop spec (§3.4 Stage
  10), §6.7's automation infrastructure (§3.4 Stage 8), §7.2's chrome/glyph iconography line
  (Assumption A6), §7.7's cognitive-load practice-mode recommendation (§3.5).
- **`save-load-persistence.md`** (already written, not read this session) — Assumption A4's
  persistence architecture is modeled on `accessibility-settings-system`'s own precedent for this
  document; a future revision of either document should confirm the exact mechanism (a new field in
  that system's own settings-adjacent persistence, or a small dedicated file).
- **`region-mastery-automation-system`** — the presumed owner of
  `encounters_completed_in_region` (§4 Formula 1) and the first-region expedition structure
  (Assumption A5); also the recommended owner of surfacing `active_efficiency_percent` at the
  automation-configuration moment (§3.6). That system's own GDD, when authored, must reference this
  document back and confirm or correct both assumptions.
- **`creature-jobs-evolution-system`** — the recommended owner of narrowing the
  first job-assignment decision to the currently-unlocked automation stage's relevant role
  (§3.4 Stage 10). That system's own GDD, when authored, must reference this document back.
- **`settings-menu-ui`** (Designed, `design/gdd/settings-menu-ui.md`) — the presumed host of the
  "Replay Onboarding" action (§3.8), per `accessibility-settings-system`'s own coordination note.

## 7. Tuning Knobs

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Pre-boss encounter minimum | `pre_boss_encounter_count_minimum` | 3–6 | 4 | §4 Formula 1. The single biggest lever on how much manual labor precedes the Mastery Transition — directly governs whether the central design challenge (§3.1) is honored. Below 3 risks the transition feeling unearned; above 6 exceeds `game-concept.md`'s own stated expedition-length range and risks feeling padded. |
| Encounter 1 part count | (content-authoring recommendation, not a new field) | 3–5 (recommend 3, the low end of art-bible §5.6's own standard-tier range) | 3 | §3.4 Stage 1. Controls how unambiguous the very first vulnerability-glyph read is. |
| Encounter 1 `base_windup_ms` | `AttackDefinition.base_windup_ms` (content-authoring input to `creature-ai-telegraph-system`) | 2000–3000 (recommend 2400) | 2400 | §4 Formula 2. How generous the first-ever telegraph read is. |
| Pre-boss windup ramp target | (content-authoring recommendation) | Converge to the region's normal baseline (~1440–1620ms) by the final pre-boss encounter | Encounter 4 → 1530ms (Formula 2 worked example) | Ensures no timing discontinuity between the last curated encounter and the boss. |
| Interrupt-only attacks in encounters 1–3 | (content-authoring constraint) | Forbidden (must be 0) | 0 | §3.4 Stage 3. Guarantees interrupt is never a forced, only ever an available, response before the boss. |
| Input-prompt hint fade | (implementation constant) | 2000–6000ms timeout, or first successful use, whichever is first | 4000ms | Assumption A6. Controls how long a control-discovery icon lingers before disappearing on its own. |
| `onboarding_completed` | `onboarding_completed` | boolean, global | `false` (until first region's boss is defeated) | §3.8, §4 Formula 3. The single flag governing whether curated pacing applies to a new game. |

## 8. Acceptance Criteria

### Functional

1. A static content-review of every encounter in the first region's pre-boss content confirms zero
   modal or blocking dialogs of any kind exist explaining a glyph's meaning — verified by enumerating
   every UI/dialog asset reachable during Stages 1–7 (§3.4) and confirming none is gated on
   acknowledging explanatory text, directly proving Design Task 2's "no glossary" requirement in the
   implementation, not just in intent.
2. A content/data validation confirms the first region's encounter 1 attack pool contains zero
   `avoidance_options = [interrupt]` (interrupt-only) attacks, and that at least one attack in the
   pool by encounter 2 includes `interrupt` as a non-exclusive option — directly verifying §3.4 Stage
   3's authoring constraint.
3. A fixture confirms `boss_available` (§4 Formula 1) is `false` for any `encounters_completed_in_
   region < pre_boss_encounter_count_minimum` and `true` at or above it, at default tuning, with no
   input sequence (menu shortcut, developer console aside) able to bypass the gate through ordinary
   play — confirming Design Task 4's "no separate skip mode" decision is enforced, not merely
   intended.
4. Formula 2's worked ramp reproduces exactly: encounters 1–4 at the stated `power_tier`/
   `base_windup_ms` inputs produce `windup_duration_ms` of 2400 / 1900 / 1620 / 1530ms respectively,
   matching `creature-ai-telegraph-system` §4 Formula 1 applied to those inputs with zero deviation.
5. **Full keyboard-only completability** (required, per the task brief): a scripted or manual
   walkthrough of the entire onboarding sequence — Stage 1 through Stage 11 (§3.4), first region start
   to first Vow-eligible Forge visit — is completable with zero mouse or pointer device connected or
   used, verified by exercising every interactive element via keyboard alone and confirming no element
   is unreachable, extending `input-targeting-system` §8 AC1's method across this document's full
   scope.
6. **Full gamepad-only completability**: identical to Criterion 5, substituting a single connected
   gamepad (no keyboard, no mouse) as the sole input device.
7. A fixture confirms that with `onboarding_completed = true`, a new game's first region encounter 1
   is authored with standard (non-curated) `power_tier`/windup values and `boss_available` is `true`
   regardless of `encounters_completed_in_region` — directly verifying Edge Case #4 and Formula 3.
8. Triggering "Replay Onboarding" in Settings resets `onboarding_completed` to `false` and produces no
   observable change to the current save's already-mastered region state, creature roster, or Forge
   inventory — verified across a fixture save with a completed first region.
9. A fixture confirms Stage-advancement tracking (§3.4) is driven entirely by action outcome, not
   input source — alternating mouse and gamepad/keyboard inputs within the same first encounter
   produces identical stage-advancement results to using either device exclusively, mirroring
   `input-targeting-system` §8 AC12's own verification method.
10. A scripted "average" playthrough of the first region's pre-boss encounters at default tuning
    (`pre_boss_encounter_count_minimum = 4`) confirms that, without requiring any deliberate optimal
    play, a part-break, a full-Resonance Ultimate cast, and at least one attempted avoidance of each
    of block, dodge, and interrupt all occur naturally within those 4 encounters in ≥ 90% of
    simulated runs — verifying §3.4's pacing claims are load-bearing, not merely asserted.
11. A code/content review confirms no onboarding-specific UI element (the input-prompt hint,
    Assumption A6, or the "Replay Onboarding" Settings action) ever requires mouse-only interaction —
    directly verifying §3.7's binding accessibility requirement in the implementation.
12. `active_efficiency_percent` is confirmed, via a UI-flow fixture, to first render to the player at
    the automation-configuration screen immediately following the first Mastery Transition and not
    at any earlier point in the sequence — verified once `region-mastery-automation-system`/
    `automation-config-ui` exist to test against; flagged here as the acceptance bar those systems'
    own test suites must meet, matching the pattern `combat-hud` §8 already used for its own
    playtest-dependent, forward-flagged criterion.

### Experiential (validated by playtest)

13. **Required, per the task brief**: a new player, given no external documentation (no wiki, no
    written guide, no verbal walkthrough beyond the game itself), completes the first region — reaching
    the Mastery Transition — in a moderated playtest, in at least 80% of sessions across a
    representative sample (recommended n ≥ 10).
14. **Required, per the task brief**: playtesters who have never played the game before correctly
    identify the vulnerability glyph's shape and meaning without being told, within their first
    encounter, in at least 80% of moderated "think-aloud" playtest sessions — the direct behavioral
    validation that the glyph language is learnable without a text glossary (Design Task 2).
15. **Required, per the task brief — the central design challenge's own validation**: playtesters who
    reach the Mastery Transition, when asked afterward how it felt, describe automation as "earned,"
    "a reward," or an equivalent sentiment — not "arbitrary," "given to me," or "I don't know why this
    is happening now." This is tested against both the default tuning (`pre_boss_encounter_count_
    minimum = 4`) and a deliberately-thinned control (`= 1`) to confirm the felt difference the
    minimum-exposure gate (§3.1, §4 Formula 1) is designed to produce is actually perceptible, not
    merely theorized.
16. Self-identified experienced ARPG/idle players report the first region's pre-boss encounters as
    "quick" or "didn't feel like a tutorial," never "slow," "hand-holdy," or "like it was teaching me
    things I already knew" — validating Design Task 4's no-separate-tutorial-mode decision from the
    veteran-player side, complementing Criterion 13's new-player-side validation.
