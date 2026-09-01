# GDD: Combat HUD

> **SUPERSEDED 2026-09-01 by the hunt HUD in SoloExpeditionScreen.cs.** Describes the manual-combat HUD model, none of which is in the runtime. Kept for vocabulary and history — do not treat its rules as current.

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | ux-designer |
| **Status** | Complete — all 8 sections authored autonomously this session (rush mode; no user available), matching the precedent set this session by `combat-encounter-system.md`, `creature-ai-telegraph-system.md`, `vow-condition-tracking.md`, `accessibility-settings-system.md`, and `forge-ui.md` — all authored the same way. Every ambiguity left open by upstream documents and pointed at this system is resolved with an explicit, flagged design call, never a placeholder. Validate at `/ux-review` and `/gate-check` before Production. |
| **Priority / Tier** | MVP — Presentation layer (`design/gdd/systems-index.md` #17, currently listed there as "combat-hud (inferred)"). This document formally authors and closes that inferred entry. Flagged by `art-bible.md` §7.5 and this session's own task brief as **the hardest UI problem in the project**: it must carry health, Resonance, 3 ability cooldowns + 1 ultimate, up to 4 live Vow-condition states, and (by explicit design) zero part-break widgets, inside a frame where the vulnerability glyph must remain the loudest continuously-animated shape on screen. |
| **Depends On** | `combat-encounter-system.md`, `vow-condition-tracking.md`, `accessibility-settings-system.md` (all written); `creature-ai-telegraph-system.md`, `creature-data-schema.md` (both written, read for **boundary-setting** — see §3.12) |
| **Depended On By** | `onboarding-tutorial-system` |

## Source Material Read

`design/art/art-bible.md` §3.5 (Hero Shapes vs. Supporting Shapes — the animation/brightness hierarchy this entire document operates under), §7.1 (typography, the Micro/Caption exemption this document's numerals rely on), §7.2 (chrome vs. glyph-grammar iconography), §7.3 (the locked UI Animation Language decision — HUD elements get one-shot beats only, never a loop), §7.4 (Diegetic vs. Screen-Space Information — the diegetic-first, tiered-by-precision philosophy this whole layout follows), §7.5 (the Combat HUD Specification — the near-complete spec this document formalizes, does not re-litigate), §7.6 (Ornamentation Tier by Screen — Combat HUD is locked at "None"), §7.7 (binding accessibility requirements: chrome interaction-state matrix, cycle-and-confirm targeting, motor/motion/low-vision/cognitive requirements), §8.2 (sprite resolution tiers — HUD pip/segment/glyph native sizes), §8.3 (color budget — chrome-neutral fill discipline), §4.1/§4.3 (Hearth Gold's strict reservation rule, reused here to justify the Vow ring's chrome-neutral color and the Ultimate pip's "scale marks importance, not color" rule), §5.1/§5.2 (Hunter frame presence and the Vow-scar belt zone this document's diegetic layer attaches beside). `design/gdd/combat-encounter-system.md` (full — the exact field names and defaults for player HP, Resonance, the defensive triad, dodge charges, ability/ultimate cooldown gating, and the encounter state machine this document renders against). `design/gdd/vow-condition-tracking.md` (full — the `VowConditionSlotState` data contract this document is the sole consumer of, the 600ms anti-strobe floor, and the explicitly deferred `flash_duration_ms` value this document owns). `design/gdd/creature-ai-telegraph-system.md` (full — read to confirm the exact boundary: telegraph/vulnerability-window state is fully diegetic and explicitly not this document's job, per that system's own "Depended On By" entry for `combat-hud`). `design/gdd/creature-data-schema.md` (read for `PartState.current_stage`/`accumulated_value` — confirming part-break rendering is creature-owned, not HUD-owned, and to note the bidirectionality gap this creates, §6). `design/gdd/accessibility-settings-system.md` (full — `HUD_GLYPH_MIN_PX_AT_1080P`, `ui_scale_percent` Formulas 1–2, `reduced_motion_enabled`'s exact suppression list, `high_contrast_mode`'s scope, and the binding requirement that the combat HUD's corner cluster reserve a margin proportional to `ui_scale_percent`). `design/gdd/systems-index.md` (this system's entry, dependency map, and the two recorded edges — `combat-encounter-system` and `vow-condition-tracking` — this document confirms and extends). `.claude/rules/design-docs.md` and `design/CLAUDE.md` (the 8-section structure and testability requirements this document is written against).

## Assumptions Log (resolved this session, no placeholders left in the design below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | The screen-space corner cluster anchors **top-left**, not top-right or a bottom corner. | Art-bible §7.5 says only "screen corner," not which one. Two independent reasons converge on top-left: (a) it is the genre-standard location for vitals, minimizing new-player learning cost; (b) the Hunter's own frame presence is locked at "lower third, offset to one side (foreground-left by convention)" (art-bible §5.1) — anchoring vitals top-left keeps both player-relevant clusters (Hunter body, HUD chrome) on the same screen-left reading zone, minimizing the saccade distance between "check my vitals" and "check my Hunter" versus a top-right placement that would maximize eye travel across the entire frame. |
| A2 | Dodge charges get their **own diegetic belt pip-track**, positioned in the belt zone but not tied to any Vow-scar or ability slot. | Art-bible §7.5's locked table never mentions dodge charges at all, but `combat-encounter-system.md`'s own "Depended On By" entry explicitly requires this document to render "dodge charge count." Dodge is a coarse/categorical, discrete-charge resource (0–2), structurally identical in kind to the already-locked ability-cooldown pip-track device — art-bible §7.4's diegetic-first, tiered-by-precision philosophy places it on the body, not in chrome. It cannot share a Vow-scar belt-charm zone (those are capped at exactly 4, one per equipped ability/ultimate slot, art-bible §5.2) because dodge is a universal Hunter mechanic, not a slotted, Vow-bindable ability — so it occupies an adjacent, structurally distinct belt position using the identical pip-device shape grammar. |
| A3 | **Exact-precision numerals (cooldown seconds, dodge-regen seconds) render only in the screen-space corner cluster, never on the belt at any scale.** HP/Resonance numerals, by contrast, are always available in the corner cluster regardless of belt/fallback mode. | The belt occupies only 10–15% of frame height (art-bible §5.1) and its native pip/brand assets are authored at 6–8px (art-bible §8.2) — well below any numeral's practical legibility floor. Forcing a numeral onto the belt would either violate the explicit "no separate cooldown icon row" rule (by effectively becoming one) or render illegibly. Because HP/Resonance are *already* screen-space chrome elements with dedicated room, their numerals cost nothing extra to add there. Ability/ultimate/dodge numerals, being diegetic-primary, only gain a legible home once the corner-cluster fallback is active (§3.9) — giving that fallback a second, concrete benefit beyond legibility: it is also the only configuration that can offer true "deliberate-look" second-level precision for cooldowns at all. |
| A4 | **Health pips are strictly binary (lit/unlit, no partial fill). Resonance segments and the dodge-charge regen pip use a partial radial/linear fill within the currently-filling unit.** | Art-bible §7.5's table uses two different verbs for the two meters: Health gets "**discrete** pip loss, no tween"; Resonance gets "**segmented wedge-fill**, no smooth sweep" and is explicitly described as "a partial radial fill inside a fixed glyph container, like a gauge in a vessel." Read literally and side by side, these are two different rendering contracts, not synonyms — Health never shows a fractional pip; Resonance (and, by the same "gauge" logic, the regenerating dodge pip, since knowing *how soon* the next charge lands is tactically load-bearing) shows a static, snap-updated fractional fill of its current unit. |
| A5 | **Damage-taken/avoidance-type confirmation (block/dodge/interrupt) reuses existing HUD elements — no new widget is added.** Block's confirmation is the HP pip-bar's own discrete pip loss. Dodge's and Interrupt's confirmation is the Resonance meter's own discrete wedge-fill jump (their respective Resonance gains, per `combat-encounter-system` §3.6, are already different in magnitude). Interrupt's primary, unmistakable confirmation is fully diegetic and already specified elsewhere: the creature's own telegraph glyph hard-cuts to resting intensity **early** (before its natural cutoff instant) the moment an interrupt lands (`creature-ai-telegraph-system` §5 Edge Case #2) — zero combat-hud involvement required. | Directly answers the task's "how does the HUD confirm which happened, without violating §3.5's animation monopoly" — by never adding a new animated element at all. Every one of these state changes is already a locked, discrete, non-tweened rendering event on an existing widget; layering dedicated new iconography on top would add HUD area and would risk a second, competing animated cue, both directly working against the hero-shape hierarchy (art-bible §3.5) and the "no bottom-spanning ability bar, no separate cooldown icon row" footprint rule (§7.5). |
| A6 | The belt-primary vs. corner-cluster-fallback choice is a **single, build-wide configuration flag set once from Vertical Slice playtest data** (§3.9's trigger criteria) — **not** a per-player, runtime-adjustable setting. | `accessibility-settings-system.md` §3.6 already states explicitly: "Combat HUD density: not addressed by a setting, because there is very little to reduce... No additional combat-HUD density control is proposed." Adding a player-facing belt/corner toggle here would silently contradict that already-locked sibling document. This document respects that boundary and treats the switch as a shipping decision, flagging (not authoring) the possibility of a future accessibility-settings-system revision if playtesting later shows the legibility problem is per-player rather than universal. |
| A7 | `combat-encounter-system.md` does not name an explicit runtime field for "time remaining on an ability's cooldown" or "current dodge charge count / regen progress." This document assumes reasonably-named fields exist for it to read: `cooldown_remaining_ms` / `cooldown_total_ms` per ability/ultimate slot, and `dodge_charges_current` / `dodge_charge_regen_progress_ms` for dodge. | `combat-encounter-system.md`'s own "Depended On By" entry for `combat-hud` states plainly that this document reads "each ability/ultimate's cooldown state... and dodge charge count," confirming the *data* is meant to exist, without formally naming the field. Flagged here, matching the established project pattern (e.g. `vow-condition-tracking.md`'s own flagged field-naming gaps against its dependencies) rather than inventing a fifth undocumented convention. |
| A8 | The Vow-condition ring renders in **chrome-neutral fill only** (Void Ink / Bone Parchment / Cold Slate, art-bible §4.5) — no reinforcing Hearth Gold or Ember Threat tint on the closed/open states. | Art-bible §4.3's "Hearth Gold is spent like a resource... reserved exclusively for boss-fall / Vow-bind / Legendary-drop" rule is the exact reasoning the task brief already applies to the Ultimate pip-track ("does NOT earn Hearth Gold... scale marks importance, not color"). A Vow becoming satisfied mid-fight is not one of those three reserved events, so tinting the ring gold would dilute the one color the whole game trains players to want. The task's own instruction that the ring be "shape-driven, NEVER color-only" is satisfied more cleanly by making shape the *only* channel, with zero risk of an accidental reserved-color spend. |
| A9 | A brief (150ms default), color-neutral **value-step highlight beat** plays on the exact HP pip that just extinguished, as a purely supplementary attention-directing flourish layered on top of the pip's own state change — never required for correctness (the missing pip alone is the load-bearing signal), never a second animated element outside the one-shot budget. | Strengthens block's damage-taken feedback (A5) beyond "a pip is now missing" (a static-frame fact a player might miss entirely if not already looking at the corner) to "something in the corner just changed" (a brief, pre-attentive motion cue) — while staying inside the one-shot, never-looping, never-exceeding-vulnerability-glyph-brightness budget (art-bible §7.3, §5.1). |

---

## 1. Overview

The combat HUD is the presentation-layer system that renders every piece of moment-to-moment combat state `combat-encounter-system` and `vow-condition-tracking` already compute — health, Resonance, three ability cooldowns plus one ultimate, up to four live Vow-condition states, and dodge charges — onto the screen, inside a frame where art-bible §3.5 mandates the creature's own vulnerability glyph must remain the loudest, and only continuously-animated, shape in view. It renders nothing itself; it formalizes and completes the near-complete specification art-bible §7.5 already locked (screen-space chrome for continuous magnitudes, diegetic belt placement for discrete/categorical states, zero widgets for part-break) into an unambiguous, implementable contract: exact screen regions and anchor points, the pip-count-to-value math behind every meter, a formal glanceable/deliberate-look information hierarchy, both the belt-primary and pre-authorized corner-cluster-fallback layouts side by side with a concrete, testable trigger for switching between them, and the accessibility floors every element must clear. This document owns none of the underlying game state (health, Resonance, cooldown timers, Vow conditions all remain their owning systems' exclusive domain) and none of the diegetic creature-side rendering (the vulnerability glyph, telegraph, and part-break crack escalation remain fully outside this document's footprint, by design) — its entire job is the small, disciplined layer of chrome and belt-adjacent glyph work sitting on top of that state, kept as close to invisible as the information demands allow.

## 2. Player Fantasy

> **The HUD's success is that you stop seeing it. All attention belongs to the creature.**

Every other document in this project's combat stack earns the player a moment of "I read that right, and I can see it worked" (`combat-encounter-system` §2's own framing). This document's job is the opposite kind of success: the player should be able to describe, after a fight, exactly what their HP band was, whether their ultimate was ready, and which of their Vows were live — without being able to say they ever consciously *looked* at the HUD to find out. That is only possible if every element here earns its place by being read pre-attentively, in the same glance already spent on the creature, rather than pulling focus away from it.

Concretely, this system exists to guarantee:

- **The vulnerability glyph never has competition.** No HUD element is ever brighter, busier, or more continuously in motion than the creature's own weak point (art-bible §3.5, §5.1). A player's eye should never have a reason to leave the creature to "check the corner" during the moment that matters.
- **A glance answers "am I okay, is my kit ready, are my bets live" as three or four chunks, not a dozen facts.** Working-memory research (Cowan's ~4-item capacity) is not a decoration here — it is the literal design constraint art-bible §7.5 already invoked ("roughly four chunks... defensible against working-memory limits") and this document formalizes it precisely (§3.2).
- **Nothing important is ever silent.** A Vow flipping from satisfied to violated changes what an ability slot *does* — the player must know this in real time (`vow-condition-tracking` §2's own framing, restated here for the rendering layer that fulfills it), but the confirmation is a single, honest, one-shot beat, never a loop competing for attention it was never meant to hold.
- **The HUD survives contact with failure gracefully.** A Retreat is an *educational* fail state, not a punishing one (`combat-encounter-system` §2) — this document's job on the way out of a fight is to freeze cleanly and disappear, never to editorialize with a dramatic "you lost" HUD flourish that would undercut that framing.
- **This system does not exempt itself from the accessibility standard it helps enforce elsewhere.** Every glyph here clears the same floors art-bible §7.7 and `accessibility-settings-system` set for the rest of the game — this is the layer players look at hardest, under the most time pressure, and it cannot be the layer that quietly fails them.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the screen-space corner cluster (health, Resonance, optional numeric backups), the diegetic belt-adjacent pip-tracks and Vow-condition rings (both the belt-primary layout and the pre-authorized corner-cluster-fallback layout), the formal glanceable/deliberate-look information hierarchy, the damage-taken/avoidance-type confirmation rules, and fail-state HUD behavior.

It explicitly does **not** own, and defers entirely to the cited document in every case:

- The vulnerability glyph, its telegraph brightness/hue curve, or any interrupt-window/desperation-phase state — fully diegetic, fully owned by `creature-ai-telegraph-system`. This document reads nothing from that system to drive its own rendering; that system's own "Depended On By" entry for `combat-hud` explicitly frames any future consumption as a `MAY`, not a requirement, and this document exercises the "does not" branch of that option (§3.12).
- Part-break progress. **No HUD element of any kind renders it.** It is rendered entirely on the creature's own part-seams (`creature-data-schema`'s `PartState.current_stage`/`accumulated_value`, composited by `animation-rig-system`'s crack-decal overlay per art-bible §8.4) — this document's only relationship to that data is confirming, for the record, that it owns none of it (§3.12).
- The cycle-and-confirm target-selection ring. Art-bible's own Open Items table names this a **separate, not-yet-authored** open item ("Cycle-and-confirm target-highlight visuals — needs its own selected-target rendering state, distinct from the vulnerability glyph, owner: `ux-designer`, at `/ux-design`"). It is world-space, drawn on the creature per `input-targeting-system`'s `selected_part_id`/`selection_render_source` fields, not a HUD element, and is out of scope for this document.
- Hunter body animation, pose, or clip direction (dodge-roll poses, block-guard stances) — `animation-rig-system`'s domain.
- Any underlying formula, threshold, or state value (health, Resonance, cooldown durations, Vow condition logic) — every number this document renders is read, never recomputed, from its owning system.

### 3.1 Screen Regions and Anchor Points

Two coordinate systems apply, matching the project's established rendering pipeline (art-bible §8.2, §8.6):

- **The corner cluster** is screen-space chrome, anchored **top-left**, inset from the screen edge by `corner_cluster_margin_percent` of the *shorter* viewport dimension (resolution-independent, avoiding distortion on ultra-wide or portrait aspect ratios). Its content scales per `accessibility-settings-system` Formula 1/2 (`ui_scale_percent`, §3.6 here), clamped to a maximum footprint (§3.8) so it can never encroach on the Arena's central creature-viewing area, per that document's own binding requirement (§5 Edge Case #1 there).
- **The belt zone** is diegetic, anchored to the Hunter's own sprite at the belt/bandolier band (art-bible §5.2) — it is not a screen-space region at all; it moves with, and is scaled by, the Hunter's own frame presence (10–15% of total frame height, art-bible §5.1), never independently.

**Corner cluster, primary mode (belt-primary shipping):**

| Row | Content | Approx. footprint |
|---|---|---|
| 1 | HP pip-bar, Resonance medallion, optional HP/Resonance numerals | `corner_cluster_primary_max_footprint_percent` (§7) |

**Corner cluster, fallback mode (§3.9 trigger met):**

| Row | Content | Approx. footprint |
|---|---|---|
| 1 | HP pip-bar, Resonance medallion, optional HP/Resonance numerals (unchanged from primary mode) | same as above |
| 2 | The 4 ability/ultimate pip-tracks (mirrored from the belt, larger scale) + their Vow-condition rings + the dodge-charge pip-track + optional cooldown/regen numerals | `corner_cluster_fallback_max_footprint_percent` (§7), reflowing to 2×2 if the cap would otherwise be exceeded |

Row 2 exists *only* in fallback mode. In primary mode, ability/ultimate/Vow/dodge state renders exclusively on the belt — the corner cluster never duplicates it, avoiding the exact redundant-widget problem art-bible §7.5's "net HUD footprint" language was written to prevent.

### 3.2 The Four-Chunk Glanceable Model

Art-bible §7.5 already asserts the glanceable combat load reduces to "roughly four chunks" once part-break is removed from the HUD entirely. This document formalizes that claim as a concrete design rule, citing the same working-memory constraint (Cowan's ~4±1 item capacity for pre-attentive chunking, the tighter and more defensible modern refinement of Miller's classic 7±2):

| Chunk | Contents (multiple underlying facts, one gestalt read) | Grouping mechanism |
|---|---|---|
| 1. Vulnerability glyph | Diegetic, owned by `creature-ai-telegraph-system`. Not this document's element, but the chunk every other chunk is graded against (art-bible §3.5). | — |
| 2. HP band | `player_current_hp`/`hunter_max_health`, reduced to one of 3 qualitative bands (fine/low/critical, §4 Formula 2) — never the exact number at glance-tier. | Pip-count silhouette density (how many pips are lit) — a single Gestalt "how much is left" read, not individually counted digits. |
| 3. Ability-readiness cluster | All 3 ability pip-tracks + the ultimate pip-track + the dodge pip-track — **5 underlying facts, read as ONE chunk.** | Gestalt proximity + common region: all 5 pip-tracks occupy the same fixed belt zone (or the same Row 2 in fallback mode), sharing an identical pip-device shape, so a glance registers "my belt is full" (all ready) vs. "there are gaps" (something's down) without requiring the player to consciously enumerate which of the 5 it is — the same silhouette-density logic art-bible §5.2 already uses for "how marked is this build." |
| 4. Vow-state cluster | Up to 4 Vow-condition rings — **up to 4 underlying facts, read as ONE chunk.** | Gestalt proximity + shared shape (ring, distinct from the pip-track's own shape) + shared belt-adjacent position: a glance registers "how many of my bets are currently live" as an open/closed-ring count, without individually parsing each ring's specific ability. |

**The arithmetic that matters**: four chunks total — vulnerability glyph (creature-owned), HP band, ability-readiness, Vow-state — matching art-bible §7.5's own claim exactly, and staying at or under Cowan's ~4-item working-memory ceiling even under combat time pressure.

**Deliberate-look tier** (requires a conscious, held gaze — never required to play correctly, always available on request): exact HP number, exact Resonance number (both always available as corner-cluster numerals, §3.3–3.4), exact cooldown seconds and exact dodge-regen seconds (available **only** when the corner-cluster fallback is active, per Assumption A3 — belt-primary mode does not expose second-level cooldown precision at all, relying on pip-count granularity as its ceiling of precision).

### 3.3 Health — Screen-Space Pip-Bar

Renders in the corner cluster, Row 1, in both modes identically. A fixed count of `hp_pip_count` (§7) binary pips — each pip is either fully lit (Bone Parchment fill) or fully unlit (Void Ink fill, matching chrome discipline, art-bible §4.5) — **never a partial fill** (Assumption A4). Fill state is computed by §4 Formula 1. Pip loss is a same-frame, discontinuous state change — "discrete pip loss, no tween" (art-bible §7.5) — optionally accompanied by the one-shot `hp_pip_loss_beat_ms` highlight (Assumption A9, §7) on the specific pip that just extinguished. A small Micro/Caption-tier numeral (`hp_current`/`hp_max`, e.g. "137/250") renders adjacent to the pip-bar at all times, in the UI/Data face with tabular figures (art-bible §7.1), satisfying the deliberate-look HP-number tier without ever being the sole carrier of the information — the pip-bar's own fill state is always primary.

### 3.4 Resonance — Screen-Space Wedge-Fill Medallion

Renders in the corner cluster, Row 1, beside the HP pip-bar, in both modes identically. Reuses the Source-medallion closed-ring container shape (art-bible §3.2/§7.5) — a fixed, unbroken circular glyph container — with a partial radial fill inside it, segmented into `resonance_segment_count` (§7) wedges. Fully-lit wedges are a discrete, non-tweened state (a wedge either has or has not been fully crossed); the currently-filling wedge shows a static, snap-updated partial fill (Assumption A4), matching art-bible's own "a gauge in a vessel" description. Fill state is computed by §4 Formula 3. Rendered entirely in chrome-neutral value (Bone Parchment fill against Void Ink container) — **never a Source hue**, per art-bible §7.5's explicit instruction. A Micro/Caption-tier numeral (`resonance_current`/`resonance_cap`) renders adjacent, identical treatment to §3.3.

### 3.5 Ability & Ultimate Cooldowns — Diegetic Pip-Tracks

**Belt-primary**: each of the 3 ability slots (`ability_1`, `ability_2`, `ability_3`) gets a small stepped pip-track of `ability_pip_segment_count` pips, positioned beside its slot's Vow-scar belt charm (art-bible §5.2) — never replacing it, the brand itself stays untouched per that section's permanence rule. The ultimate slot gets the same pip device at `ultimate_pip_scale_multiplier` larger physical scale (art-bible §7.5: "same pip device, larger scale to reflect slot importance... does not earn Hearth Gold — scale marks importance, not color," Assumption A8). Fill state is computed by §4 Formula 4 — pips fill from empty toward full as the cooldown counts down, snapping discretely, never a smooth sweep. An unequipped ability slot (no item, or an item with no bound ability) renders as a minimal, low-contrast Void-Ink-only outline placeholder at that slot's fixed belt position — no pips, no fill — clearly distinct from both "ready" (full pip-track) and "on cooldown" (partial pip-track) by the total absence of the pip shape itself, never by color alone.

**Corner-cluster fallback**: identical shape grammar, mirrored into Row 2 at larger physical scale — "more pixels, not a redesign" (art-bible §7.5). Additionally carries an optional Micro/Caption-tier numeral (`cooldown_remaining_ms` formatted as seconds, e.g. "2.4s") beside each pip-track while that slot is on cooldown, hidden entirely when ready (Assumption A3) — the only configuration in which exact cooldown-seconds precision is exposed at all.

### 3.6 Dodge Charges — Diegetic Pip-Track

**Belt-primary**: a dedicated pip-track of exactly `dodge_charge_max` pips (default 2, `combat-encounter-system` §7's own knob — this document never redefines that value, only reads it), positioned in the belt zone adjacent to, but visually distinct from, the 4 Vow-scar-linked ability/ultimate pip-tracks (Assumption A2) — it carries no Vow-scar brand beside it, since dodge is never Vow-bindable. Each fully-available charge is a fully-lit pip; a charge currently regenerating shows a partial linear fill using the same gauge idiom as Resonance (Assumption A4), computed by §4 Formula 5.

**Corner-cluster fallback**: identical mirrored treatment in Row 2, with an optional numeral for regen-seconds-remaining, same rule as §3.5.

### 3.7 Vow-Condition Rings

Renders `vow-condition-tracking`'s `VowConditionSlotState` contract unmodified — this document performs zero evaluation logic of its own, only shape-mapping (art-bible §3.2's container grammar, already locked by that system's §3.6: `is_satisfied: true` → closed ring, `false` → open ring). For each of the 4 loadout slots:

- If `ring_applicable = false` (a `static_cost` Vow or an empty slot), **no ring renders at all** — never a meaningless permanently-closed placeholder (`vow-condition-tracking` §3.6, restated here as this document's own rendering obligation).
- If `ring_applicable = true`, the ring renders beside that slot's ability pip-track (belt-primary) or in the mirrored Row 2 position (fallback), in chrome-neutral fill only (Assumption A8) — shape is the entire signal.
- The one-shot flash on a flip is computed by §4 Formula 7, consuming `last_flip_timestamp_ms` and this document's own `vow_flash_duration_ms` (§7) — the exact value `vow-condition-tracking` §3.6 explicitly deferred to this document to define. Simultaneous multi-slot flips consume that document's own `flash_fire_time[i]` staggering (its Formula 4) unmodified — this document never recomputes or re-derives its own stagger order (§5 Edge Case #2).

### 3.8 Corner-Cluster Footprint Discipline

Per `accessibility-settings-system` §5 Edge Case #1's binding requirement, the corner cluster must reserve a screen margin proportional to `ui_scale_percent` and must never encroach on the Arena's central creature-viewing area at any scale. This document satisfies that requirement with two caps, both expressed as a percentage of viewport area rather than a fixed pixel size (so they hold at any resolution):

- **Primary mode**: capped at `corner_cluster_primary_max_footprint_percent` (§7).
- **Fallback mode**: capped at `corner_cluster_fallback_max_footprint_percent` (§7). If Row 2's content would exceed that cap at the player's current `ui_scale_percent` (up to the maximum 200%, `accessibility-settings-system` §3.2), it **reflows** — the 4 ability/ultimate pip-tracks wrap from a single row into a 2×2 grid — rather than growing the cluster past its cap or overlapping the Arena. This mirrors `accessibility-settings-system`'s own established "reflow, not overlap" principle for its dense list-based screens (§5 Edge Case #1 there), applied here to the one screen-space HUD region dense enough to need it.

### 3.9 The Fallback Trigger Condition (Vertical Slice Playtest Gate)

Per art-bible §7.5's own flagged open risk, whether the belt-charm pip-track and ring-toggle stay legible at 10–15% frame height "cannot be settled on paper." This document defines a concrete, testable gate rather than leaving the decision to intuition, consistent with `.claude/rules/design-docs.md`'s "no hand-waving" requirement:

**The corner-cluster fallback replaces belt-primary as the shipped default if, across a representative Vertical Slice playtest sample (recommended n ≥ 12) at native 1080p resolution and `ui_scale_percent = 100`, any of the following holds:**

1. Fewer than 80% of playtesters can correctly identify, within 1 second of a glance at the belt zone, whether at least one of their 3 ability slots is currently on cooldown versus fully ready (the direct test of §3.2's "ability-readiness as one chunk" claim).
2. Fewer than 80% of playtesters can correctly identify a single Vow ring's current open/closed state from the belt zone within 1 second, tested in isolation (motion frozen, one ring shown at a time, at native belt scale).
3. A qualitative complaint about belt-scale legibility is raised unprompted by more than 25% of playtesters during a standard combat playtest session.

If none of these trigger, belt-primary ships as the sole configuration. This is a **single, build-wide flag** (Assumption A6), not a per-player runtime setting — `vow-condition-tracking`'s `VowConditionSlotState` contract is deliberately render-location-agnostic (its own §3.6), so switching this flag requires zero changes to any upstream document, only a change to which of §3.5–§3.7's two layouts this document's implementation draws.

### 3.10 Damage-Taken and Avoidance-Type Feedback

Per Assumption A5, no new HUD widget is added for this. The confirmation channel per outcome:

| Outcome | Confirmation channel | Owner |
|---|---|---|
| **Block** (chip damage lands) | The HP pip-bar's own discrete pip loss (§3.3), optionally with the `hp_pip_loss_beat_ms` highlight (Assumption A9) | This document (reuses an existing element) |
| **Dodge** (fully negated, Resonance gained) | The Resonance medallion's own discrete wedge-fill jump (§3.4) — the gain amount (`resonance_per_dodge`, default 8, `combat-encounter-system` §3.6) is smaller than interrupt's | This document (reuses an existing element) |
| **Interrupt** (fully negated, telegraph hard-cancelled, larger Resonance gained) | **Primary**: the creature's own telegraph glyph snapping to resting intensity early, before its natural cutoff instant (`creature-ai-telegraph-system` §5 Edge Case #2) — fully diegetic, zero HUD involvement. **Secondary**: the same Resonance wedge-fill jump as dodge, at a visibly larger magnitude (`resonance_per_interrupt`, default 15) | Primary: `creature-ai-telegraph-system`. Secondary: this document (reuses an existing element) |

No dedicated "you blocked/dodged/interrupted" icon, banner, or text ever appears. This is deliberate: every one of the three outcomes is already legible through a state change an existing, already-locked HUD or diegetic element must make anyway, and adding a fourth confirmatory element would both exceed the "no separate cooldown icon row" footprint discipline and introduce a competing animated cue against the hero-shape hierarchy.

### 3.11 Fail-State (Retreat) HUD Behavior

Combat HUD's ornamentation tier is locked at **None** (art-bible §7.6) — this holds even for the fail state. On a Retreat outcome (`combat-encounter-system` §3.8–§3.9):

1. At the `ACTIVE → RESOLVING` transition, this document's elements **freeze at their last live values** — no further pip loss, no new flashes, no new cooldown countdowns render, matching "No new actions; in-flight actions only" (`combat-encounter-system` §3.2's `RESOLVING` row).
2. When `player_current_hp` is subsequently set to 1 (`combat-encounter-system` §5 Edge Case #7, the deliberate anti-confusion floor), the HP pip-bar correctly renders exactly 1 lit pip under §4 Formula 1 with no special-casing required — `ceil(1 / hp_per_pip)` always evaluates to 1 for any `hp_per_pip > 0`, so this document's own formula already produces the correct result for that upstream rule without modification.
3. **No dedicated "you failed" banner, dramatic flourish, or ceremony-tier motion plays.** This is a direct requirement of §7.6's locked "None" ceremony tier, and a deliberate reinforcement of `combat-encounter-system`'s own "failure costs little time... educational, not punishing" framing — a shame-inducing HUD treatment would directly undercut that design intent.
4. At `RESOLVING → COMPLETE` and the hand-off back to the region hub, every element this document owns is torn down entirely — the corner cluster and belt-adjacent pip-tracks/rings are combat-only chrome; the hub/idle view has its own, separately-scoped (and, per art-bible §7.4, near-nonexistent) status UI that is not this document's concern.

### 3.12 Explicitly Zero Footprint: Part-Break, Telegraph, Target Selection

Stated once, plainly, for the record (restating §3.0): this document renders **no element of any kind** for part-break progress, telegraph wind-up state, or the cycle-and-confirm target-selection highlight. All three are read as adjacent, informational-only context in this document's Source Material (§ above) purely so this boundary can be stated with full knowledge of what is being deliberately excluded, not out of an oversight. Any future revision that adds a HUD element for any of these three must treat that as a reversal of a locked art-bible §7.5 decision (part-break) or a genuinely new open item (telegraph, target selection), not a natural extension of this document.

## 4. Formulas

All formulas share `creature-data-schema`'s round-half-up convention for any value that must resolve to an integer (pip counts, segment counts), matching every sibling document in this project's combat stack.

### Formula 1 — Health Pip Count and Fill State

```
hp_per_pip = hunter_max_health / hp_pip_count
pips_filled = ceil(player_current_hp / hp_per_pip)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `hunter_max_health` | float, external input | > 0 | `combat-encounter-system` §3.1's effective `max_health` stat, read fresh at the start of each encounter (§5 Edge Case #5) and treated as fixed for that encounter's duration. |
| `hp_pip_count` | int, tuning knob | 6–14 (default 10) | **Fixed regardless of `hunter_max_health`** — this is the Edge Case #1 resolution (§5): pips never scale in *count*, only in the HP each one represents. |
| `hp_per_pip` | float | > 0 | HP value one pip represents, recomputed whenever `hunter_max_health` changes between encounters. |
| `player_current_hp` | float, external input | `[0, hunter_max_health]` | `combat-encounter-system` §3.8's live value. |
| `pips_filled` | int | `[0, hp_pip_count]` | Output — number of lit pips. Binary per pip (Assumption A4); the value itself may exceed the true fractional HP ratio slightly, by design (a pip stays lit until its full HP allocation is exhausted, never showing partial loss). |

**Output range**: `pips_filled = 0` only when `player_current_hp = 0` exactly; for any `player_current_hp > 0`, `pips_filled ≥ 1` by construction (the `ceil` of any positive value under a positive divisor is always ≥ 1) — this is what guarantees §3.11's Retreat-floor-of-1 case renders correctly with no special handling.

**Worked example**: `hunter_max_health = 250`, `hp_pip_count = 10` → `hp_per_pip = 25`. At `player_current_hp = 137`: `pips_filled = ceil(137 ÷ 25) = ceil(5.48) = 6`. At `player_current_hp = 1` (the Retreat floor): `pips_filled = ceil(1 ÷ 25) = ceil(0.04) = 1`.

### Formula 2 — HP Band (Glanceable Tier)

```
hp_percent = (player_current_hp ÷ hunter_max_health) × 100
band = "fine"      if hp_percent > hp_band_low_threshold_percent
     = "low"       if hp_band_critical_threshold_percent < hp_percent ≤ hp_band_low_threshold_percent
     = "critical"  if hp_percent ≤ hp_band_critical_threshold_percent
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `hp_band_low_threshold_percent` | float, tuning knob | 35–65 (default 50) | Boundary between "fine" and "low." |
| `hp_band_critical_threshold_percent` | float, tuning knob | 10–35 (default 25), must be `<` the low threshold | Boundary between "low" and "critical." |
| `band` | enum | `{fine, low, critical}` | Output — the sole glanceable-tier HP signal (§3.2). The exact `hp_percent`/pip count remains available at deliberate-look tier via §3.3's numeral; `band` is never itself rendered as a separate widget, only expressed through the pip-bar's own silhouette density. |

**No hysteresis/debounce mechanism is required here** (unlike `vow-condition-tracking`'s Category A conditions, §4 Formula 1 of that document). `combat-encounter-system` §3.8 defines no in-combat healing at MVP — `player_current_hp` is monotonically non-increasing for the duration of an encounter, so a band boundary can be crossed at most once per encounter, in one direction only, and can never oscillate. **Forward-compatibility flag**: if a future recovery item or mechanic introduces in-combat healing, this monotonicity guarantee breaks, and this formula would need its own hysteresis layer mirroring `vow-condition-tracking` Formula 1 — noted here, not built now, since no such mechanic exists in the currently locked design.

**Worked example**: `hunter_max_health = 250`, default thresholds. `player_current_hp = 137` → `hp_percent = 54.8` → **fine**. `player_current_hp = 100` → `40.0` → **low**. `player_current_hp = 50` → `20.0` → **critical**.

### Formula 3 — Resonance Segment Fill

```
resonance_per_segment = resonance_cap ÷ resonance_segment_count
segments_filled = floor(current_resonance ÷ resonance_per_segment)
partial_fill_fraction = (current_resonance mod resonance_per_segment) ÷ resonance_per_segment
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `resonance_cap` | float, external input | `combat-encounter-system` §7's own knob, default 100 | Read, never redefined. |
| `resonance_segment_count` | int, tuning knob | 6–12 (default 8) | Fixed wedge count of the medallion container. |
| `current_resonance` | float, external input | `[0, resonance_cap]` | `combat-encounter-system` §3.6's live value. |
| `segments_filled` | int | `[0, resonance_segment_count]` | Fully-lit wedges, discrete state. |
| `partial_fill_fraction` | float | `[0, 1)` | Static, snap-updated partial fill of the currently-filling wedge (Assumption A4) — `0` exactly when `current_resonance` is a whole multiple of `resonance_per_segment`. |

**No hysteresis required**: `combat-encounter-system` §3.6 states Resonance is "never reduced except by spending it" (a single, deliberate, atomic Ultimate-cast deduction) — it does not decay or hover near a boundary, so no anti-flicker mechanism is needed.

**Worked example**: `resonance_cap = 100`, `resonance_segment_count = 8` → `resonance_per_segment = 12.5`. At `current_resonance = 47`: `segments_filled = floor(47 ÷ 12.5) = 3`, `partial_fill_fraction = (47 mod 12.5) ÷ 12.5 = 9.5 ÷ 12.5 = 0.76` — three full wedges lit, a fourth at 76%.

### Formula 4 — Ability/Ultimate Cooldown Pip Fill

```
pips_lit = pip_segment_count                                              if cooldown_remaining_ms = 0
         = round(pip_segment_count × (1 − cooldown_remaining_ms ÷ cooldown_total_ms))    otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `pip_segment_count` | int, tuning knob | `ability_pip_segment_count` (default 4) for the 3 ability slots; `ultimate_pip_segment_count` (default 6) for the ultimate slot | §7. |
| `cooldown_remaining_ms` | float, external input (Assumption A7) | `[0, cooldown_total_ms]` | Time left until this slot is castable again. |
| `cooldown_total_ms` | float, external input | > 0 | That slot's own `cooldown_ms` (`resonance-weaving-system`'s `WovenAbilityDefinition` for the 3 abilities; `ultimate_cooldown_ms` for the ultimate, `combat-encounter-system` §7, default 3000ms). |
| `pips_lit` | int | `[0, pip_segment_count]` | Output. `pip_segment_count` (full track) means ready/castable. |

**Worked example (ability slot)**: `ability_pip_segment_count = 4`, `cooldown_total_ms = 8000`, `cooldown_remaining_ms = 2000`: `pips_lit = round(4 × (1 − 2000÷8000)) = round(4 × 0.75) = round(3.0) = 3` of 4. At `cooldown_remaining_ms = 0`: `pips_lit = 4` (full, ready).

**Worked example (ultimate slot)**: `ultimate_pip_segment_count = 6`, `cooldown_total_ms = 3000` (default `ultimate_cooldown_ms`), `cooldown_remaining_ms = 1000`: `pips_lit = round(6 × (1 − 1000÷3000)) = round(6 × 0.667) = round(4.0) = 4` of 6.

### Formula 5 — Dodge Charge Pip Fill

```
pips_lit_full = dodge_charges_current
regen_progress_fraction = dodge_charge_regen_progress_ms ÷ dodge_charge_regen_ms    (0 if dodge_charges_current = dodge_charge_max)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `dodge_charges_current` | int, external input (Assumption A7) | `[0, dodge_charge_max]` | Current whole charge count. |
| `dodge_charge_max` | int, external input | `combat-encounter-system` §7's own knob, default 2 | Read, never redefined — also the fixed pip count for this track. |
| `dodge_charge_regen_progress_ms` | float, external input (Assumption A7) | `[0, dodge_charge_regen_ms]` | Elapsed time toward the next charge, reset to 0 the instant a charge completes. |
| `dodge_charge_regen_ms` | float, external input | `combat-encounter-system` §7's own knob, default 4000 | Read, never redefined. |
| `pips_lit_full` | int | `[0, dodge_charge_max]` | Fully-lit pips (available charges). |
| `regen_progress_fraction` | float | `[0, 1)` | Partial fill of the next pip in the sequence, only rendered if `dodge_charges_current < dodge_charge_max` (Assumption A4's gauge idiom). |

**Worked example**: `dodge_charge_max = 2`, `dodge_charge_regen_ms = 4000`, `dodge_charges_current = 1`, `dodge_charge_regen_progress_ms = 1500`: one pip fully lit (an available charge), the second pip shows `regen_progress_fraction = 1500 ÷ 4000 = 0.375` (37.5% filled).

### Formula 6 — UI-Space Size Floors (Glyphs and Numerals)

```
FinalRenderedSize_px(glyph)   = max(ReferenceSize_px(glyph) × S, HUD_GLYPH_MIN_PX_AT_1080P)
FinalRenderedSize_px(numeral) = max(ReferenceSize_px(numeral) × S, HUD_NUMERAL_MIN_PX_AT_1080P)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `S` | float | ≥ 1.0 at 100% scale on ≥1080p displays | `accessibility-settings-system` Formula 1's own combined scale factor — read and reused unmodified, never redefined here. |
| `HUD_GLYPH_MIN_PX_AT_1080P` | constant | fixed, 16 | `accessibility-settings-system`'s own non-tunable floor. Applies to every pip, ring, and medallion container this document renders. |
| `HUD_NUMERAL_MIN_PX_AT_1080P` | constant, **this document's own** | fixed, 12 | A distinct, smaller floor for this document's confirmatory Micro/Caption-tier numerals — justified by, and matched to, art-bible §8.2's own "HUD health pip / Resonance segment: 8–12px per segment" scale band, and by `accessibility-settings-system` §3.8's precedent that Micro/Caption-tier text may sit below the general body-text floor specifically because it is confirmatory to an already-primary glyph, never the sole channel. |

**Applies to**: every element in §3.3–§3.7 (glyphs: pips, medallion, rings; numerals: HP/Resonance/cooldown/regen text). Belt-primary elements are diegetic (scaled with the Hunter's own sprite, not this formula) — this formula governs the screen-space corner cluster in both modes, and the corner-cluster fallback's Row 2 content specifically.

### Formula 7 — Vow Ring Flash Window

```
flash_active(t_now) = (last_flip_timestamp_ms ≠ null) AND ((t_now − last_flip_timestamp_ms) < vow_flash_duration_ms)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `last_flip_timestamp_ms` | float \| null, external input | ≥ 0, or `null` | `vow-condition-tracking`'s own `VowConditionSlotState.last_flip_timestamp_ms`, read and never recomputed. |
| `vow_flash_duration_ms` | int, **this document's own tuning knob** | 200–600 (default 350) | The exact value `vow-condition-tracking` §3.6 explicitly deferred to this document. |
| `flash_active(t_now)` | bool | — | Whether that slot's one-shot flash beat is still playing this frame. |

**Worked example**: consuming `vow-condition-tracking` §4 Formula 4's own worked example (4 slots flip simultaneously at `t = 8000ms`, staggered `flash_fire_time` at `8000, 8080, 8160, 8240ms`), at `vow_flash_duration_ms = 350`: `ability_1`'s flash window is `[8000, 8350)`; `ultimate`'s is `[8240, 8590)`. At `t_now = 8200`: `ability_1` (`flash_active = true`), `ability_2` (fires at 8080, window `[8080,8430)`, `true`), `ability_3` (fires at 8160, window `[8160,8510)`, `true`), `ultimate` (fires at 8240 — has not fired yet at `t=8200`, `flash_active = false`). Multiple overlapping windows are expected and safe — each individual ring still flashes exactly once, satisfying `vow-condition-tracking` §5 Edge Case #6's own proof that this scenario is not a strobe.

## 5. Edge Cases

1. **`hunter_max_health` exceeds what `hp_pip_count` pips could represent one-to-one.** Resolved by Formula 1 directly: pip **count** is fixed (`hp_pip_count`, §7); each pip's HP **value** (`hp_per_pip`) scales up as `hunter_max_health` grows. Pips never subdivide or multiply — glanceability (a fixed, small, countable number of pips) is preserved regardless of how large the Hunter's build-driven max health becomes.
2. **All 4 Vows flip in the same frame.** Fully resolved by consuming `vow-condition-tracking` Formula 4 unmodified (§4 Formula 7 here): all 4 ring shapes update instantly and simultaneously (mechanical correctness is never delayed), while the *flash* beats are staggered by `SIMULTANEOUS_FLIP_STAGGER_MS` (that document's own knob, default 80ms) across at most 240ms at default tuning. Each individual belt-charm location still flashes exactly once — nowhere near a strobe at any single screen location, with or without staggering (`vow-condition-tracking` §5 Edge Case #6's own proof, which this document inherits rather than re-derives).
3. **`ui_scale_percent` is at 200% and the corner cluster would encroach on the creature.** Resolved by §3.8's footprint caps and reflow rule: the cluster's total area is capped as a percentage of viewport, never a fixed pixel size, and Row 2 content reflows into a 2×2 grid before the cap would be exceeded — it never grows unbounded or overlaps the Arena's central viewing area, satisfying `accessibility-settings-system`'s binding requirement directly.
4. **Fewer than 4 Vows are equipped (empty belt slots).** Per §3.7, a slot with `ring_applicable = false` renders **no ring at all** — not a hidden placeholder, not a default-closed ring, genuinely absent — matching `vow-condition-tracking` §3.6's own contract exactly. The ability pip-track for that slot still renders normally if an ability (just not a conditional Vow) occupies it; if the slot is fully empty, §3.5's minimal outline placeholder renders instead.
5. **`hunter_max_health` changes between encounters** (a gear change made in the region hub). Formula 1 is recomputed fresh at the start of every `ENGAGING` state (`combat-encounter-system` §3.2) — this document never caches `hp_per_pip` across encounters.
6. **The player's HP is floored to 1 on a Retreat outcome** (`combat-encounter-system` §5 Edge Case #7). Already proven to render correctly with no special-casing under Formula 1 (see that formula's own Output Range note) — `ceil(1 ÷ hp_per_pip) = 1` for any positive `hp_per_pip`.
7. **The encounter transitions to `RESOLVING`.** Per §3.11, every element this document owns freezes at its last live value; no new pip loss, cooldown countdown, or Vow flash is queued or begins during `RESOLVING`, matching `combat-encounter-system`'s own "no new actions" rule for that state.
8. **`reduced_motion_enabled` is on.** None of this document's motion is suppressed by it. Per `accessibility-settings-system` §3.4's own suppression table, only *ambient/decorative* motion is gated by that setting; every motion this document defines (pip snaps, wedge-fill snaps, the one-shot Vow flash, the optional HP pip-loss highlight beat) is information-bearing, not decorative — exactly the same category as that document's own named exception for work-loop pulses. This document adds no new gate; it simply confirms none of its own elements were ever eligible for suppression in the first place.
9. **The corner-cluster fallback is active at the same time `ui_scale_percent = 200%`.** The two caps in §3.8 compose independently — the fallback's larger base cap (`corner_cluster_fallback_max_footprint_percent`) is itself subject to the same proportional-margin and reflow rules as the primary cap, just measured against a larger content set. Reflow (2×2 grid) is the release valve in both cases; the cluster's outer boundary never grows past its cap regardless of which mode is active.
10. **A dodge charge finishes regenerating to full while an adjacent Vow ring is mid-flash.** No conflict: the two are visually and mechanically independent (different shape — pip-track vs. ring; different trigger — a charge threshold vs. a debounced Vow condition), each governed by its own formula (§4 Formulas 5 and 7 respectively), and neither this document nor any upstream system couples them.
11. **A `gate`-mode Vow-bound ability is cast in the same frame its own Vow condition flips** (e.g., the cast itself is what satisfies a `cyclic_attack_trigger` condition). The ability's own cooldown pip-track (Formula 4) and its Vow ring (Formula 7) are driven by two entirely separate upstream signals (`cooldown_remaining_ms` vs. `is_satisfied`/`last_flip_timestamp_ms`) and render independently in the same frame with no ordering dependency between them — a pip-track snap and a ring flash beginning in the same frame is expected, safe, and distinguishable by shape alone.
12. **`colorblind_preview_mode = grayscale` and/or `high_contrast_mode` are active while combat is rendering.** No element in this document relies on color as a sole channel (§3.3–§3.7 are all shape/fill-state driven, per Assumption A8 for the Vow ring specifically) — every state this document renders remains fully distinguishable under full grayscale, satisfying `accessibility-settings-system` Acceptance Criterion #4 by construction rather than by a special-case check.

## 6. Dependencies

### Depends On

- **`combat-encounter-system.md`** — reads `player_current_hp`/`hunter_max_health` (§3.3, §4 Formula 1–2), `current_resonance`/`resonance_cap` (§3.4, §4 Formula 3), each ability/ultimate slot's `cooldown_remaining_ms`/`cooldown_total_ms` (§3.5, §4 Formula 4, Assumption A7), `dodge_charges_current`/`dodge_charge_regen_progress_ms`/`dodge_charge_max`/`dodge_charge_regen_ms` (§3.6, §4 Formula 5, Assumption A7), `resonance_per_dodge`/`resonance_per_interrupt` (§3.10), and the `ACTIVE`/`RESOLVING`/`COMPLETE` state machine (§3.11). This document is the "screen-space or diegetic display of state this document owns and updates" that `combat-encounter-system`'s own §6 "Depended On By" entry already names in advance — this document fulfills that contract exactly. Never writes to any field that document owns.
- **`vow-condition-tracking.md`** — the sole consumer of its `VowConditionSlotState` contract (§3.7), reading `slot_id`, `vow_id`, `ring_applicable`, `effect_mode`, `is_satisfied`, `last_flip_timestamp_ms` unmodified, and supplying the `vow_flash_duration_ms` value that document's own §3.6 explicitly deferred to this document to define (§4 Formula 7). This document is the exact "primary and only consumer" that document's own §6 already names.
- **`accessibility-settings-system.md`** — reads `HUD_GLYPH_MIN_PX_AT_1080P` and `ui_scale_percent`/Formula 1–2 (§4 Formula 6), `reduced_motion_enabled`'s exact suppression list (§5 Edge Case #8, confirming none of this document's elements are gated by it), and `high_contrast_mode`/`colorblind_preview_mode`'s scope (§5 Edge Case #12). Also implements the binding requirement that document's own §5 Edge Case #1 imposes specifically on the combat HUD's corner cluster (§3.8). This document is one of the six UI systems that document's own §6 "Depended On By" list already names.
- **`creature-ai-telegraph-system.md`** (boundary dependency only, §3.12) — read to confirm, not to consume for rendering: telegraph/vulnerability-window state remains fully diegetic and out of this document's scope, per that system's own §6 "Depended On By" entry for `combat-hud`, which frames any consumption as optional (`MAY`) and explicitly notes "the strong design intent is that telegraph and vulnerability state remain fully diegetic... with zero dedicated HUD widgets." This document exercises the "does not consume" branch of that option. The one exception is informational, not a rendering dependency: §3.10 cites that system's Edge Case #2 (the early telegraph cutoff on a successful interrupt) as *context* for why no HUD widget is needed for interrupt confirmation — this document renders nothing from that system either way.
- **`creature-data-schema.md`** (boundary dependency only, §3.12) — read to confirm `PartState.current_stage`/`accumulated_value` are the data source for the creature's own crack-decal rendering (owned by `animation-rig-system`'s compositing pipeline per art-bible §8.4), never this document's. **Flagged bidirectionality gap, not self-edited**: `creature-data-schema.md`'s own "Depended On By" list (`combat-encounter-system`, `animation-rig-system`, `creature-ai-telegraph-system`, `creature-jobs-evolution-system`, `loot-drop-system`) does not include `combat-hud`, because that document predates this one and part-break's "no HUD element" resolution was only locked afterward, in art-bible §7.5. Per this session's file discipline, `creature-data-schema.md` is not edited here; this relationship (a confirmed *absence* of a rendering dependency, not a data dependency) is documented fully in this GDD for a future centralized `systems-index.md`/schema-cross-reference pass to reconcile.

### Depended On By

- **`onboarding-tutorial-system`** — will sequence a first-encounter tutorial that most likely gates advanced mechanics (interrupt, Resonance spend, Vow-condition reading) behind a scaffolded introduction to this document's own glanceable/deliberate-look hierarchy (§3.2), per `combat-encounter-system` §6's own note that tutorial sequencing should follow a flow-channel introduction order. That system's own GDD must reference this document's exact chunk names and formulas when authored. `systems-index.md` already records this edge (`24. onboarding-tutorial-system — depends on: combat-encounter-system, combat-hud`).

### `systems-index.md` Gaps (flagged, not self-edited)

Per this session's file discipline, `design/gdd/systems-index.md` is not edited by this document. Two gaps are flagged here for a future centralized update, following the precedent `input-targeting-system.md` and `accessibility-settings-system.md` set for their own equivalent corrections:

1. `systems-index.md`'s Dependency Map currently lists `combat-hud` as depending only on `combat-encounter-system` and `vow-condition-tracking`. This document establishes two additional read-only/boundary edges (`creature-ai-telegraph-system`, `creature-data-schema`, both explicitly non-rendering) and one full consuming edge (`accessibility-settings-system`) not yet reflected there.
2. `systems-index.md`'s entry #17 currently labels this system "combat-hud (inferred)." This document formally authors it; the "(inferred)" qualifier and the "Not Started"/"—" status/path fields should be updated to "Designed" / `design/gdd/combat-hud.md` in a future centralized pass.

## 7. Tuning Knobs

All values below are unbalanced placeholders pending Vertical Slice playtesting, per this project's established convention (matching every sibling document's own tuning-knob framing) — they are exposed as external data, never hardcoded (`.claude/docs/coding-standards.md`).

| Knob | Field | Safe Range | Default | Affects |
|---|---|---|---|---|
| HP pip count | `hp_pip_count` | 6–14 | 10 | §4 Formula 1. Fixed regardless of `hunter_max_health` (Edge Case #1) — the single biggest lever on how coarse/fine the HP pip-bar's granularity feels. |
| HP band — low threshold | `hp_band_low_threshold_percent` | 35–65% | 50% | §4 Formula 2. Boundary between "fine" and "low." |
| HP band — critical threshold | `hp_band_critical_threshold_percent` | 10–35%, must be `<` low threshold | 25% | §4 Formula 2. Boundary between "low" and "critical." |
| Resonance segment count | `resonance_segment_count` | 6–12 | 8 | §4 Formula 3. Wedge granularity of the medallion. |
| Ability pip segment count | `ability_pip_segment_count` | 3–6 | 4 | §4 Formula 4. Cooldown pip-track granularity for the 3 regular ability slots. |
| Ultimate pip segment count | `ultimate_pip_segment_count` | 4–8 | 6 | §4 Formula 4. Slightly finer granularity than regular slots, matching the ultimate's typically longer cooldown. |
| Ultimate pip scale multiplier | `ultimate_pip_scale_multiplier` | 1.25–2.0 | 1.5 | Physical rendered size multiplier vs. a regular ability pip — "scale marks importance, not color" (art-bible §7.5). |
| Vow ring flash duration | `vow_flash_duration_ms` | 200–600ms | 350ms | §4 Formula 7. **The value `vow-condition-tracking` §3.6 explicitly deferred to this document.** Must stay a single, discrete, one-shot beat — never long enough to read as a hold or a loop. |
| HP pip-loss highlight beat | `hp_pip_loss_beat_ms` | 100–250ms | 150ms | §3.3, Assumption A9. Purely supplementary; the pip's own state change remains correct with this at 0 (disabled). |
| HUD numeral floor | `HUD_NUMERAL_MIN_PX_AT_1080P` | fixed | 12px | §4 Formula 6. **This document's own constant**, distinct from and smaller than `HUD_GLYPH_MIN_PX_AT_1080P` (16px, non-tunable, owned by `accessibility-settings-system`). Governs Micro/Caption-tier confirmatory numerals only. |
| Corner cluster margin | `corner_cluster_margin_percent` | 2–5% of shorter viewport dimension | 3% | §3.1, §3.8. Inset from the top-left screen edge. |
| Corner cluster footprint — primary | `corner_cluster_primary_max_footprint_percent` | width 8–16%, height 5–10% | 12% × 8% | §3.8. HP + Resonance only. |
| Corner cluster footprint — fallback | `corner_cluster_fallback_max_footprint_percent` | width 12–22%, height 15–28% | 18% × 22% | §3.8. Triggers 2×2 reflow of ability/ultimate pip-tracks if exceeded. |
| Belt-vs-corner configuration flag | `belt_fallback_active` | boolean, build-wide | `false` (belt-primary ships first) | §3.9. Set exactly once from Vertical Slice playtest data per that section's trigger criteria — **never a player-facing runtime setting** (Assumption A6). |
| Playtest sample size | (methodology parameter, not a runtime knob) | n ≥ 12 recommended | n = 12 | §3.9. This document's own proposed testing methodology for the fallback trigger gate. |

## 8. Acceptance Criteria

### Functional

1. **The gray-and-freeze design test** (art-bible §7.5's own phrasing, applied here as a formal, testable criterion): with all motion frozen and the entire frame rendered in `colorblind_preview_mode = grayscale`, a QA tester correctly distinguishes "ability ready" (a fully-lit pip-track) from "ability on cooldown" (a partial pip-track) from "ability slot empty" (the outline placeholder) by shape and position alone, on a fixture combat encounter — verified as a manual walkthrough per `design/CLAUDE.md`'s UI evidence standard. Separately, the same fixture confirms "Vow satisfied" (closed ring) is distinguishable from "Vow violated" (open ring) by shape alone. **"This part is about to break" is explicitly out of this document's own test scope** — verified instead as part of the creature's own crack-rendering pipeline (`animation-rig-system`/art-bible §8.4); this document's contribution to that combined art-bible design test is limited to confirming it adds no competing HUD element that could interfere (§3.12).
2. No HUD element rendered by this document ever exceeds the vulnerability glyph's peak rendered brightness, in any state (ready, on-cooldown, flashing, or the HP pip-loss highlight beat) — verified by a fixture combat encounter sampling every element's peak brightness value against the glyph's own peak (art-bible §5.1's absolute, frame-wide ceiling).
3. No HUD element rendered by this document ever animates continuously or loops — every motion this document defines (§3.3–§3.7, §3.10) resolves to a single, bounded, one-shot event with a defined end state — verified by a fixture recording every element's animation curve over a full encounter and asserting zero elements exceed their own defined one-shot duration (`vow_flash_duration_ms`, `hp_pip_loss_beat_ms`) or repeat.
4. Every glyph-grammar element (pip, ring, medallion container) this document renders meets `HUD_GLYPH_MIN_PX_AT_1080P` (16px) at 1080p and `ui_scale_percent = 100`, and every confirmatory numeral meets `HUD_NUMERAL_MIN_PX_AT_1080P` (12px) under the same conditions — verified per `accessibility-settings-system` Formula 1's own Worked Example 1 methodology, applied to this document's element list.
5. Formula 1's worked examples reproduce exactly: `hunter_max_health = 250`, `hp_pip_count = 10`, `player_current_hp = 137` → `pips_filled = 6`; `player_current_hp = 1` → `pips_filled = 1`.
6. Formula 2's worked examples reproduce exactly: at default thresholds and `hunter_max_health = 250` — `player_current_hp = 137` → `band = "fine"`; `= 100` → `"low"`; `= 50` → `"critical"`.
7. Formula 3's worked example reproduces exactly: `resonance_cap = 100`, `resonance_segment_count = 8`, `current_resonance = 47` → `segments_filled = 3`, `partial_fill_fraction = 0.76`.
8. Formula 4's worked examples reproduce exactly: `ability_pip_segment_count = 4`, `cooldown_total_ms = 8000`, `cooldown_remaining_ms = 2000` → `pips_lit = 3`; `ultimate_pip_segment_count = 6`, `cooldown_total_ms = 3000`, `cooldown_remaining_ms = 1000` → `pips_lit = 4`.
9. Formula 5's worked example reproduces exactly: `dodge_charge_max = 2`, `dodge_charge_regen_ms = 4000`, `dodge_charges_current = 1`, `dodge_charge_regen_progress_ms = 1500` → one pip fully lit, second pip `regen_progress_fraction = 0.375`.
10. Formula 7's worked example reproduces exactly the overlapping flash-window states at `t_now = 8000ms` and worked-example values, given `vow-condition-tracking`'s own Formula 4 stagger output as input.
11. A fixture equipping fewer than 4 conditional Vows (mixing conditional, `static_cost`, and empty slots) results in `ring_applicable = false` slots rendering zero ring elements — verified against `vow-condition-tracking`'s own `VowConditionSlotState` fixture data.
12. A fixture forcing the corner cluster to its fallback-mode maximum content at `ui_scale_percent = 200` triggers the 2×2 reflow rather than exceeding `corner_cluster_fallback_max_footprint_percent` or overlapping a fixed Arena-center test region — verified by asserting the cluster's rendered bounding box never crosses the Arena-center boundary at any tested scale from 100–200%.
13. A fixture forcing a Retreat outcome (`player_current_hp` reaching 0, then floored to 1 per `combat-encounter-system` §5 Edge Case #7) results in the HP pip-bar showing exactly 1 lit pip with no special-case code path required, and zero new flash/animation events queued during the `RESOLVING` state — verified across the full Retreat transition sequence.
14. A fixture with `reduced_motion_enabled = true` confirms every motion element this document defines (pip snaps, wedge-fill snaps, Vow ring flash, HP pip-loss highlight beat) still plays, unaffected — cross-verified against `accessibility-settings-system` Acceptance Criterion #10's own suppression-list fixture.

### Experiential (validated by playtest, per §3.9's own flagged risk)

15. §3.9's three trigger criteria are formally evaluated at the first Vertical Slice playtest pass with a real belt-primary implementation available, and the resulting `belt_fallback_active` flag value is recorded in `production/session-state/active.md` or an equivalent build-configuration record (not this document, per file discipline) — this criterion is satisfied by the *evaluation having occurred*, regardless of which way the flag resolves.
16. A playtester who has just landed a successful interrupt can, without prompting, correctly state whether it was a block, a dodge, or an interrupt they just performed, based solely on what they saw happen on screen — the direct behavioral validation of §3.10's reused-element confirmation design, tested across at least 3 playtesters, each covering all three outcome types at least once.
17. A playtester finishing a fixture Retreat encounter does not describe the HUD's failure presentation as "punishing," "harsh," or similar — the direct validation of §3.11's deliberately unornamented fail-state treatment matching `combat-encounter-system`'s own "educational, not punishing" design intent.
