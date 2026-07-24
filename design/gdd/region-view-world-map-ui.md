# Region View & World Map UI: Resonance Hunter

## Document Status

| Field | Value |
|---|---|
| **Version** | 1.0 |
| **Owned By** | ux-designer |
| **Status** | Complete — authored per direct task specification; no user available this session for section-by-section approval. Every ambiguity is resolved with an explicit, flagged design call in the Assumptions Log below, never a placeholder. **This screen is the art bible's own named showcase for Principle 3** ("mastery is drawn onto the world, not just tracked in a menu") — its entire job is to prove that principle pays off in UI design cost, not only emotion. Validate at `/design-review` and `/ux-review` before Production. |
| **Priority / Tier** | MVP — UI / Presentation layer (`design/gdd/systems-index.md` #22, size **S**). Formalizes art-bible §2, §4.4, §6.1, §6.5, §6.7, §7.4, §7.6 into a buildable screen flow; introduces no new gameplay mechanics. |
| **Depends On** | `design/gdd/region-mastery-automation-system.md`, `design/gdd/creature-jobs-evolution-system.md`, `design/gdd/accessibility-settings-system.md`, `design/gdd/combat-encounter-system.md` |
| **Depended On By** | None — leaf document. (`automation-config-ui`, is a one-directional forward navigation target from this screen, not a dependent; see §6.) |

## Source Material Read

`design/art/art-bible.md` (full — §2 Mood & Atmosphere, especially "The Mastery Transition" and
"Idle / Automated Region View"; §3.4 UI Shape Grammar; §3.5 Hero Shapes vs. Supporting Shapes; §4.3
Semantic Color Vocabulary; §4.4 The Temperature Law; §4.5 UI Palette; §5.3 Bound Creature Work-Loop
Spec; §5.4 Creature-State Distinguishing Rules; §6.1 Two Compositions, One Region; §6.5 The
Three-State Region System; §6.7 Automation Infrastructure Visuals; §7.1–7.7 in full), `design/gdd/
region-mastery-automation-system.md` (full — the region state machine, mastery-level ladder, RMP,
Formulas 1–8, the Mastery Transition's mechanical sequence at §3.7, and its own named dependency on
this document), `design/gdd/creature-jobs-evolution-system.md` (full — the Healthy/Blocked/Starved
work-state machine at §3.5/Formula 6, per-role production at §3.6, and its own named dependency on
this document), `design/gdd/accessibility-settings-system.md` (relevant sections — `reduced_
motion_enabled`'s exact suppression list, `ui_scale_percent`/`HUD_GLYPH_MIN_PX_AT_1080P`, `colorblind
_preview_mode`), `design/gdd/combat-encounter-system.md` (Depended On By list, confirming the
`RESOLVING`/`COMPLETE` state contract this document's Mastery Transition sequence hands off from),
`design/gdd/systems-index.md` (this system's entry #22 and dependency map).

## Assumptions Log (resolved this session, no placeholders left unresolved below)

| # | Assumption | Why it was necessary |
|---|---|---|
| A1 | **The World Map is a region-select grid, not a geographic atlas.** Each Region Tile is a reduced-scale render of that region's own already-authored Region View composition (art-bible §6.1) — no new background-map art asset is introduced. | Art-bible §6.7 itself describes the offline-progression beacon as "simplified to read at **region-select** THUMBNAIL scale" — that exact phrase presupposes a region-select grid, not a geographic overworld. Reusing the Region View composition a second time (at reduced scale) is also the literal, licensed extension of §6.1's own rule that this composition serves "the Idle/Automated Region View... and any region-overview screen." Inventing a separate hand-painted atlas would be an unbudgeted art asset nowhere named in art-bible §8's asset standards. |
| A2 | **Region Tiles are sized as "medium thumbnails," not minimap pins** — large enough that art-bible §6.7's Mid-read-distance infrastructure (ward-post, cairn) stays individually countable at map scale, not only the Far-read beacon. | §6.7's own table only guarantees the beacon reads at extreme thumbnail scale; the other three structures are rated Mid / Mid-to-near. Choosing a tile size deliberately larger than a minimap pin is what makes Acceptance Criterion 1 (stage legibility at *any* unlocked combination, not just Stage 4) achievable, rather than silently downgrading the map to "only shows whether a region has offline progression." |
| A3 | **The map-scale problem signal reuses art-bible §5.3's exact Healthy/Blocked/Starved color-and-motion grammar verbatim, aggregated to the assigned team's worst state, and rendered on the region's own standing infrastructure** — never a new chrome badge, icon, or notification dot. | This is the direct, load-bearing application of art-bible §7.2's diegetic-first icon test ("if it represents... a real placed world-object, it is content and must reuse an existing established asset") to a genuine gap: no existing document specifies *any* map-scale problem signal. Reusing the exact grammar the player already learned from individual creatures (rather than inventing a second visual language for the same underlying states) is what keeps the signal a single, transferable pattern instead of two. |
| A4 | **A 4th tile/infrastructure state, Unstaffed (`assigned_team = ∅`), is visually distinct from Healthy *only* by the absence of the work-loop pulse** — no alarm color, no flicker. | Sharing Blocked/Starved's Ember-Threat-edged alarm treatment with "hasn't been configured yet" would semantically overload the one channel this whole game trains players to read as *urgent*. "Not yet staffed" and "actively struggling" are different player problems needing different urgency; motion-presence-vs-absence (the same "player must tell states apart by motion/shape alone" discipline art-bible §5.3/§5.4 already use) cleanly separates them without a new color. |
| A5 | **The World Map always routes through the Region View before combat or team management — there is no direct World-Map-to-Arena shortcut**, for any region state. | Keeps exactly one navigation model regardless of conquest state (no separate "conquered path" vs. "unconquered path" to maintain), and gives every region — including ones the player hasn't fought yet — a state-appropriate establishing-shot beat before combat begins, which the Region View composition is already licensed to provide ("any region-overview screen," art-bible §6.1). |
| A6 | **The Mastery Transition's mechanical state write (region-mastery-automation-system §3.7 step 4, fired at `COMPLETE`) is deliberately decoupled from this document's own presentation sequence**, which continues independently of that write's exact timing. | Prevents the ceremony's pacing from ever being able to leave mechanical state ambiguous — the region is either `unconquered` or `mastered`, cleanly, the instant `COMPLETE` fires, regardless of whether the player is still watching a cinematic, has skipped ahead, or has quit mid-ceremony (§5 Edge Case 4). This is what makes a safe interrupted-ceremony resume (A7) possible without any risk of re-triggering or double-applying the state transition. |
| A7 | **A new, UI-owned boolean field, `mastery_transition_presentation_pending`** (per region, default `false`, set `true` the instant Phase 2 of §3.4 fires and cleared `true → false` only at Phase 7), **resumes an interrupted ceremony on next load** rather than silently skipping it. Not added to any other document's schema — flagged here as needing a persistence home (§6). | A player who quits mid-cinematic still mechanically owns the mastery they just earned (A6) but would otherwise simply never see their own "coronation, not shutdown" moment (art-bible §2) — the single most important beat in the game's visual language. Silently skipping it would be a worse outcome than a small, well-scoped resume mechanism; this mirrors the pattern this session's other autonomous GDDs have already used (e.g. `loot-filter-ui`'s `rule_label`, `settings-menu-ui`'s `first_run_prompt_shown`) of introducing one small UI-owned field to close a genuine gap rather than leaving it unresolved. |
| A8 | **The Mastery Transition sequence is fully non-skippable (no player input consumed as a skip) the *first* time a save file ever experiences it. From the second Mastery Transition onward, the sequence becomes skippable starting at the beginning of Phase 5** (after the mandatory ~2s payoff hold begins), **never before** — the environmental sweep (Phase 4) itself is never skippable on any playthrough. | Protects art-bible §2's explicit, load-bearing instruction ("there is no instant cut... camera and animation hold on the payoff for a beat") in full for the one moment it matters most — a brand-new player's very first region. A player who will eventually master many regions (Full Vision scope) is not forced to sit through an unskippable multi-second cinematic dozens of times; this is a deliberate, justified divergence from "always fully unskippable," not a weakening of the rule. |
| A9 | **Default Region Tile order is the authored/progression order.** A secondary, player-toggleable "needs attention first" sort using this document's own Formula 2 (severity score) is offered, never forced. | The brief's own framing ("a player with 6 automated regions must spot the one in trouble instantly") is satisfied by the *visual signal itself* (§3.5) without requiring a sort at all — sort is a convenience for large rosters, not the load-bearing legibility mechanism, so it defaults off/authored-order rather than surprising the player by silently reordering their map. |
| A10 | **A 5th tile visual state, "locked/undiscovered" (dimmed, non-interactive silhouette), is reserved as a forward-compatible hook but not implemented or activated for MVP.** | No document read for this task (`game-concept.md`'s MVP scope, `region-mastery-automation-system`, `systems-index.md`) defines a region-unlock/prerequisite system, and MVP ships exactly one region. Reserving the state now avoids a retrofit if a future progression-gating system is added, without inventing unlock rules this document has no authority to define. |

---

## 1. Overview

The Region View & World Map UI is the player-facing surface for the game's core emotional and
mechanical thesis: it is where a player *sees* the empire they earned. It owns three things — the
**World Map**, a region-select grid where every region's conquest state and (once mastered) its
staffing health are legible at a glance from standing infrastructure alone; **the Region View**, the
wide diorama composition where a mastered region's bound creatures visibly work and an unconquered
region's beautiful danger is on display, backed by a single, deliberately minimal aggregate summary
readout; and the **Mastery Transition presentation sequence** — the precise, testable shot-by-shot
staging of the single most important beat in the game's visual language, handed off from
`combat-encounter-system`'s Arena the instant a region boss falls. This document formalizes what
art-bible invested more design depth in than any other screen (§2, §6.1, §6.5, §6.7, §7.4, §7.6) into
a buildable, accessible, and — above all — *sparse* interface. Its central design constraint, stated
directly by art-bible §7.4, is that this screen needs almost no screen-space status UI at all: that
sparseness is not an omission to fix, it is the entire point.

## 2. Player Fantasy

> **Look over the homestead you built.** Not a progress bar, not a checklist, not a menu of
> percentages — a place, standing there, working, because you fought for it. This is the screen that
> proves the empire is real.

Art-bible §2 names this mood "quiet pride" — the specific, calm satisfaction of surveying a settled
homestead, not the flatness of a "task complete" screen. This document delivers on that fantasy by
holding two things simultaneously true, in tension with every genre instinct toward denser dashboards:

- **The world itself is the status display.** A player should be able to tell exactly how their
  region-conquering campaign is going — which regions are thriving, which need attention, how far
  automation has progressed — by looking at standing structures and creatures at work, never by
  reading a table. Every design decision in this document is checked against whether it could
  instead be answered by counting something already standing in the world (art-bible §7.4's own
  design test).
- **The Mastery Transition is a coronation the player is allowed to simply watch.** No button to
  press, no panel to dismiss, no interruption of the payoff with logistics — the moment a region
  becomes theirs gets to be exactly as ceremonial as the fight that earned it, because it is the
  single clearest proof in the whole game that automation was a reward, never a default.

## 3. Detailed Rules

### 3.0 Scope and Non-Goals

This document owns: the World Map region-select grid and its per-tile presentation, the Region View
composition's chrome (as distinct from its diegetic content, which other documents already own), the
Aggregate Summary Readout's exact contents, the Mastery Transition's presentation *sequence* (not its
mechanical trigger, which `region-mastery-automation-system` §3.7 owns), and the map-scale
problem-surfacing signal. It does **not** own, and defers to the cited document in every case: the
region state machine, mastery-level math, and idle efficiency curve (`region-mastery-automation-
system`), the Healthy/Blocked/Starved work-state machine and per-role work-loop animation content
itself (`creature-jobs-evolution-system` / art-bible §5.3 — this document only *displays* those
states, never redefines them), team assignment, storage-capacity collection, and any other
dense-configuration interaction (`automation-config-ui` — this document defines only
the hand-off point to it), and the Arena/combat encounter itself (`combat-encounter-system`).

### 3.1 The World Map (Region Select)

The World Map presents every region the player has discovered as a grid of **Region Tiles**. Per A1,
each tile is not a bespoke icon but a reduced-scale render of that region's own Region View
composition (§3.2) — the same art asset, viewed at a second zoom level, exactly matching art-bible
§6.1's "same region through two lenses" philosophy extended to a third.

**What every tile shows, regardless of state**:

- The region's name (Ceremony/Display face, art-bible §7.1 — used sparingly here, matching that
  face's "large sizes only" rule).
- Ambient color-temperature wash: cool, desaturated, unstable (unconquered) or warm, stable, saturated
  (mastered) — art-bible §4.4's Temperature Law, the coarsest and always-present conquest-state
  signal, legible even before any structure is individually resolved.

**Unconquered tiles additionally show**: jagged, flickering sigil-scar texture within the cool wash
(no infrastructure exists yet — Automation Capability Stages are a `mastered`-only precondition per
`region-mastery-automation-system` §3.1). No individual hostile creature is rendered at this scale;
only the ambient wash's own restless motion (art-bible §2) is visible — full creature detail belongs
to the Region View (§3.2) and the Arena, not this thumbnail.

**Mastered tiles additionally show**: every Automation Capability Stage's standing infrastructure the
region has actually unlocked, cumulative (art-bible §6.7) — ward-post and cairn together (Stages 1–2,
present from the instant of Mastery Transition), forge-stall (Stage 3, Fully Mastered+), beacon
(Stage 4, Optimized). Per A2, tile size is chosen specifically so the Mid-read-distance structures
stay individually countable here, not just the Far-read beacon — this is what makes Acceptance
Criterion 1 (stage legibility at every unlocked combination) achievable at map scale, not only at
Region-View scale. The infrastructure additionally carries the aggregate problem-surfacing signal
(§3.5) — this is the mechanism by which a Blocked/Starved region is spotted without opening the tile.

**Grid behavior**: tiles are laid out in rows (`region_tiles_per_row`, §7), sorted by default in
authored/progression order (A9). A player-toggleable "needs attention first" sort re-orders using
Formula 2 (§4) without hiding any region. If the full region roster exceeds one screen, the grid
paginates or scrolls (`region_tiles_per_page`, §7 — see also §5 Edge Case 5).

**Selection**: confirming a tile (click, or discrete keyboard/gamepad confirm, §3.6) navigates to that
region's Region View (§3.2) — for *any* region state, per A5. There is no combat entry point on the
World Map itself.

### 3.2 The Region View (The Diorama)

The Region View is the wide, diorama-scale composition art-bible §6.1 locks as one of the two
per-region camera compositions. Its camera is a fixed establishing shot — there is no player-
controlled free camera, consistent with the "no free-roam traversal" constraint underlying the
two-composition system in the first place. An optional, purely decorative, low-amplitude ambient
drift/parallax may be layered in at implementation discretion; this document does not require it, and
any such drift must gate on `reduced_motion_enabled` exactly like art-bible §6.6's ambient particle
drift already does.

**What is diegetic here (rendered per other documents' own rules, never redefined by this one)**:

- Terrain/architecture "bones" and the sigil-scar network, at whichever of the two authored treatment
  passes matches the region's current state (art-bible §6.5).
- Ambient lighting/color wash (art-bible §4.4).
- Cumulative automation infrastructure (art-bible §6.7), identical to what the World Map tile already
  showed at reduced scale.
- Every assigned creature, rendered performing its actual role-specific work loop — strike-and-recover,
  brace, orbit, fidget, or breathe (art-bible §5.3) — in whichever of Healthy/Blocked/Starved its
  current `work_state` (`creature-jobs-evolution-system` §3.5/Formula 6) actually is. This document
  never substitutes a generic idle sprite for the real work-loop animation.

**What is chrome here (owned by this document)**:

- **Back to Map** — always present.
- **Hunt** — enters the Arena for an active encounter against this region's hostile population.
  Always present, for both unconquered and mastered regions (mastered-region re-entry runs fully
  concurrent with automation, per `region-mastery-automation-system` §5 Edge Case 4 — this document
  adds no special-case handling for that concurrency, since that document already fully specifies it).
- **Manage Team** — mastered regions only (team assignment requires the Region-Mastered precondition,
  `creature-jobs-evolution-system` §3.2 step 3). Hands off to `automation-config-ui` (not yet
  written). This document defines only the hand-off point, not that screen's contents.
- **The Aggregate Summary Readout** (§3.3) — mastered regions only.

**An unconquered Region View therefore carries almost no chrome at all**: Back to Map, Hunt, and
nothing else — pure diegetic beautiful-danger presentation (art-bible §2) plus one entry point into
combat.

### 3.3 The Aggregate Summary Readout

Exists only for mastered regions. Per art-bible §7.4, this is the single screen-space status element
this whole screen family is permitted, and it is deliberately minimal — never a per-creature or
per-structure breakdown (both already fully diegetic, §3.2). It shows exactly three things:

1. **Mastery level name** (e.g. "Fully Mastered," `region-mastery-automation-system` Formula 2's
   output as a text label). This closes a genuine legibility gap infrastructure-counting alone cannot:
   Newly Conquered and Partially Mastered share *identical* standing infrastructure (both show only
   ward-post + cairn, since Stage 3's forge-stall doesn't unlock until Fully Mastered) — the label,
   not a new diegetic asset, is what disambiguates the two.
2. **`idle_efficiency_percent`** (`region-mastery-automation-system` Formula 4), rendered as a Data-
   face tabular numeral — the precision backup to the coarse mastery-level label, exactly matching
   art-bible §7.4's "diegetic-primary, chrome-precision-backup" rule.
3. **Per-resource displayed yield rate** (this document's own Formula 1, §4) — one short row per
   resource currently flowing into storage (`region_output_per_tick[resource] > 0`), each a resource
   icon (reusing established content-grammar per art-bible §7.2 — never a generic chrome icon) plus a
   tabular numeral. Resources at zero output are omitted entirely, so an Unstaffed or fully-Blocked
   region shows a short or empty list, never a wall of zeroes.

**Explicitly excluded, and why** (so the restraint is auditable, not accidental): a per-creature status
list (redundant with §3.2's diegetic work-loops — that comprehension problem belongs to
`creature-roster-ui`, not here); a per-structure status list (redundant with §6.7's infrastructure); a
notification badge on the Manage Team button (would duplicate the ward-post's own severity signal,
§3.5, in chrome form — rejected specifically to avoid a second, competing channel for the same
information); a numeric RMP progress bar toward the next mastery tier (that precision-tier display
belongs on `automation-config-ui`'s denser surface, not this screen's minimal one).

**Visual treatment**: no panel border or frame ornament beyond a light Bone-Parchment-on-transparent
text cluster (art-bible §4.5's chrome-neutral rule), consistent with this screen's Medium ceremony
tier (art-bible §7.6 — modest warmth, never heavy). **Refresh cadence**: polled on a fixed interval
(`region_view_readout_refresh_seconds`, §7), not recomputed every frame — none of its values need
per-frame precision.

### 3.4 The Mastery Transition — Presentation Sequence

Art-bible §2 names this the single most important beat in the game's visual language: "coronation,
not shutdown." This document owns the precise shot-by-shot staging that beat rides on top of the
mechanical sequence `region-mastery-automation-system` §3.7 already locks. **No phase below skips
directly to any chrome-dense screen; control returns only at Phase 7.**

**Phase 0 — Precondition** (owned upstream, restated for context): the player is actively fighting
the region's boss in the Arena. `combat-encounter-system`'s normal Kill flow resolves the boss's
death exactly as any other Kill.

**Phase 1 — Arena Glyph-Inversion** (duration: `combat-encounter-system`'s own `RESOLVING` window,
its 3000ms boss-duration default — **not** a knob this document owns). The instant `RESOLVING`
begins, the combat HUD's corner cluster and belt-charm zone fade out over the first portion of that
fixed window, clearing the frame for art-bible §3.5's one sanctioned hierarchy-suspension exception.
The boss's vulnerability glyph — the exact mark the player exploited — plays its heal-and-invert
animation directly on the creature's body (art-bible §2's signature visual element), still framed in
the tight Arena composition. This document takes no mechanical action and adds no additional
presentation during this phase, matching `region-mastery-automation-system` §3.7 step 2's own "no new
automation begins" ruling.

**Phase 2 — Mechanical Commit** (instantaneous, at `combat-encounter-system` reaching `COMPLETE`):
`region-mastery-automation-system` §3.7 step 4 fires — `region_state → mastered`, `mastery_level → 0`,
`region_mastery_points → 0`, `last_ticked_at` initialized. This document's own
`mastery_transition_presentation_pending` flag (A7) is set `true` for this region at this exact
instant. No presentation action occurs here — the mechanical write and the visible payoff are
deliberately decoupled (A6).

**Phase 3 — Camera Hand-off** (Arena → Region View): a deliberate camera move — pull-back/dolly-out
or a clean cut, whichever the engine/rig supports; this document specifies the perceptual requirement,
not the camera-rig implementation — reveals that the Arena and the Region View are literally the same
ground (art-bible §6.1's authoring relationship, now paid off as a camera beat). Zero chrome is
visible.

**Phase 4 — Environmental Sweep** (duration: `mastery_transition_sweep_duration_seconds`, §7 — this
document's own knob, explicitly distinct from and not synchronized to Phase 1's fixed 3000ms window).
The cool→warm ambient wash and the sigil-scar torn→inlay interpolation (art-bible §6.5's
"Transitioning" state) play across the full Region View, originating at the boss's fall position and
expanding outward to the region's edges — direction is load-bearing and never reversed (art-bible §2:
"contraction reads as loss; expansion reads as growth"). Stage 1–2 infrastructure (ward-post + cairn,
always unlocked together at this exact moment) builds in at its authored position as the sweep passes.
**Never skippable, on any playthrough** (A8). Zero chrome.

**Phase 5 — Held Silence** (duration: `mastery_transition_min_hold_seconds`, §7, default 2s). Camera
holds on the fully-swept, now-warm Region View — the "held-breath exhale" (art-bible §2). On a
player's first-ever Mastery Transition, no input is accepted as a skip anywhere in Phases 1–5 (A8).
From the second Mastery Transition onward, a confirm input becomes accepted as a skip starting at the
beginning of this phase (never before).

**Phase 6 — Ceremony Summary Beat** (duration: `mastery_transition_summary_duration_seconds`, §7,
default 4s; skippable exactly as Phase 5 once eligible). A High-ceremony-tier (art-bible §7.6)
text/ornament overlay appears: the region name and a coronation-toned headline (Ceremony/Display
face, art-bible §7.1), plus the two newly-unlocked Automation Capability Stage names — the heaviest
frame ornament this document specifies anywhere in this screen family, matching the treatment already
licensed for the Forge's Vow-binding confirmation sibling screen. This is still not a menu: no control
exists here beyond an implicit "continue."

**Phase 7 — Control Returns**: the overlay clears; the player lands in the same, now-interactive,
mastered Region View (§3.2). Back to Map, Hunt, and Manage Team, plus the Aggregate Summary Readout
(currently reading Newly Conquered, its band-minimum efficiency, and an empty resource list — no team
is assigned yet), are all live for the first time. `mastery_transition_presentation_pending` clears to
`false`. **This is the first point at which any chrome-dense screen becomes reachable at all**,
and only because the player themselves presses Manage Team — satisfying "no instant cut to an
automation panel" as a literal, testable sequencing guarantee (§8 AC 3).

**Input during Phases 1–6**: exactly one input matters — confirm-to-skip, active only per the rules
above. All other input is deliberately inert (§3.6), matching `region-mastery-automation-system`
§3.7's own "no new player actions begin" framing extended to this document's input handling.

### 3.5 Problem Surfacing at Map Scale

Per creature, `work_state` (`creature-jobs-evolution-system` §3.5/Formula 6) is Healthy, Blocked, or
Starved. This document reads that state for every creature assigned to a region and computes a single
**region-aggregate worst state** (Formula 2, §4), then renders it using art-bible §5.3's *exact*
existing color-and-motion grammar — never a new one (A3) — on the region's own standing infrastructure
(primarily the ward-post, guaranteed present from the instant of Mastery Transition onward; the
beacon, if present, always mirrors the identical treatment so the signal never contradicts itself
across zoom levels):

| Aggregate State | Trigger | Visual Treatment (art-bible §5.3's grammar, reused verbatim) |
|---|---|---|
| **Healthy** | Team not empty; no member Starved or Blocked | Steady Hearth Gold glow-pulse, on schedule |
| **Blocked** | Team not empty; no member Starved, at least one Blocked | Steady glow, desaturated toward Cold Slate — flat, not brighter, no pulse |
| **Starved** | At least one member Starved (checked first, matching that document's own priority order) | Hearth Gold with a faint Ember-Threat-edged flicker, on a slow, irregular distress cadence (`map_tile_distress_flicker_period_seconds`, §7) |
| **Unstaffed** | `assigned_team = ∅` | Infrastructure present but fully static — no pulse, no flicker, no color shift from its resting value (A4) |

This exact treatment appears identically at **both** map-tile scale and Region-View scale — the same
infrastructure object, viewed at two zoom levels, per art-bible §6.1's own two-compositions
philosophy; no separate "map version" of the signal is ever authored.

**Design test** (restating art-bible §5.3's own test at this new scale): cover the tile in gray — a
player must still tell Healthy, Blocked, Starved, and Unstaffed apart by motion-presence and
motion-cadence alone.

**Never suppressed by `reduced_motion_enabled`**: this signal inherits the same "information-bearing,
not decorative" classification `accessibility-settings-system` §3.4 already gives per-creature
work-loop pulses — it is, structurally, the same signal, aggregated and relocated, not a new one.

Formula 2's severity score (§4) drives only the *optional* "needs attention first" sort (§3.1, A9) —
the per-tile visual signal itself requires no ranking to be independently legible.

### 3.6 Navigation and Input Model

**World Map**: discrete grid navigation — arrow keys, D-pad, or left-stick moves a visible focus
indicator (art-bible §7.7's mandatory keyboard-focus rule) between Region Tiles; confirm (Enter,
gamepad confirm, or mouse click) selects. This is an ordinary discrete-grid menu, **not** a re-
application of combat's cycle-and-confirm targeting decision (art-bible §7.7) — a tile grid has no
fine-grained-adjacent-target problem to solve, so standard menu-grid navigation is sufficient and
appropriate; stated explicitly to avoid conflating the two.

**Region View**: a small number of discrete, tab/D-pad-cyclable buttons (Back to Map, Hunt, Manage
Team), using the same five-state chrome matrix (hover, keyboard-focus, selected, disabled,
drag-preview/drop-target — the last unused here, since nothing on this screen is draggable) art-bible
§7.7 requires everywhere else.

**Mastery Transition**: input is inert except the single confirm-to-skip action, active only per
§3.4's phase rules.

**Motor accessibility**: tile and button hitboxes are padded generously beyond their visual bounds,
per art-bible §7.7's binding, decision-independent rule.

**Mouse**: click-to-select on tiles and buttons, standard hover states throughout.

### 3.7 The Chrome Inventory (How Little, and Why)

Stated plainly and auditably, per screen state:

| Screen State | Chrome Elements |
|---|---|
| World Map | Tile-grid card frame (minimal, neutral, per art-bible §3.4) + optional needs-attention sort toggle. Tile *content* is 100% diegetic (reused Region View renders). |
| Region View — unconquered | 2 buttons: Back to Map, Hunt. Zero readout. |
| Region View — mastered | 3 buttons: Back to Map, Hunt, Manage Team. 1 readout cluster (mastery-level label + efficiency numeral + per-resource rate rows), borderless. |
| Mastery Transition, Phases 1–5 | 0 chrome. |
| Mastery Transition, Phase 6 | 1 ceremony overlay (High ornament, one-shot, no persistent controls). |

This is dramatically smaller than every other UI-tier document in this project (`combat-hud`,
`forge-ui`, `creature-roster-ui`, `loot-filter-ui`, and the future `automation-config-ui` all carry
substantially more screen-space chrome than this document's densest state). That gap is the direct,
measurable cash-out of art-bible §7.4's claim that Principle 3 pays off in UI *design cost*, not only
emotion — restated here as a design test: **before adding any chrome element to this screen family,
ask whether standing infrastructure, a creature's own work-loop, or the Aggregate Readout's existing
three fields could already answer the question; if so, the new element is redundant and should be
cut**, exactly as §3.3 already applied to reject a notification badge, a per-creature list, and a
progress bar.

## 4. Formulas

All formulas share the project's established round-half-up convention for any output that must
resolve to an integer. Both formulas below are genuinely new to this document — neither exists
upstream — but both are thin by design: they are display/ranking derivations of already-computed
upstream state, not new gameplay rules.

### Formula 1 — Displayed Region Yield Rate

A presentation-layer conversion of `region-mastery-automation-system` Formula 5's per-tick output
into a per-hour rate suitable for the Aggregate Readout (§3.3). It is **not** authoritative game state
— it is an at-a-glance estimate that converges to the true accumulated value over time.

```
displayed_yield_rate_per_hour(resource) =
    round(
        Σ over {c ∈ assigned_team : role(c) emits resource, work_state(c) = healthy} of
            role_output_per_tick(c)
        × (idle_efficiency_percent / 100)
        × (3600 / work_tick_interval_seconds(role(c)))
    )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `role_output_per_tick(c)` | number, external input | ≥ 0 | `creature-jobs-evolution-system` Formula 5, reused directly, unmodified. |
| `idle_efficiency_percent` | float, external input | `[25, 120]` | `region-mastery-automation-system` Formula 4's output. |
| `work_tick_interval_seconds(role(c))` | float, external input | fixed per role (`creature-jobs-evolution-system` §7 defaults: Crafter 8, Attacker/Support 15, Defender 25, Producer 40) | The producing creature's own cadence, used to convert its per-tick contribution to a per-hour rate. |
| `displayed_yield_rate_per_hour(resource)` | int | ≥ 0, unbounded above | Rendered in the Aggregate Readout; omitted from display when `0`. |

**Output range**: ≥ 0, unbounded above. **Known approximation, stated explicitly**: this formula
rounds once at the hourly scale, while `region-mastery-automation-system` Formula 5 rounds once *per
resolved tick* and accumulates — the two can diverge by a small amount over any given real hour. This
document's displayed rate is an estimate for glance-legibility, never the authoritative stored
quantity; Formula 5's own tick-by-tick accumulation always governs actual game state.

**Worked example**: two Healthy Producers assigned, `role_output_per_tick = 12` each
(`creature-jobs-evolution-system` Formula 5's own worked example), `work_tick_interval_seconds = 40`
(Producer default), region at Fully Mastered with `idle_efficiency_percent = 94.75`
(`region-mastery-automation-system` Formula 4's own worked example):

```
ticks_per_hour = 3600 / 40 = 90
raw_output_per_hour = (12 + 12) × 90 = 2160
displayed_yield_rate_per_hour = round(2160 × 0.9475) = round(2046.6) = 2047 units/hour
```

### Formula 2 — Region Problem Severity Score and Worst-State Determination

Drives §3.5's map-scale visual state and the optional needs-attention sort (§3.1, A9).

```
worst_state(region) = starved    if ∃ c ∈ assigned_team : work_state(c) = starved
                     = blocked    else if ∃ c ∈ assigned_team : work_state(c) = blocked
                     = healthy    else if assigned_team ≠ ∅
                     = unstaffed  otherwise (assigned_team = ∅)

affected_fraction(region) = (count{c ∈ assigned_team : work_state(c) ∈ {starved, blocked}})
                             / |assigned_team|          [0 if unstaffed]

region_problem_severity_score = state_weight[worst_state(region)] × 1000
                               + affected_fraction(region) × 100
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `work_state(c)` | enum, external input | `{healthy, blocked, starved}` | `creature-jobs-evolution-system` §3.5/Formula 6, reused directly per assigned creature. |
| `worst_state(region)` | enum | `{healthy, blocked, starved, unstaffed}` | Checked in Starved-first priority order, mirroring that document's own priority default (§3.5 there). |
| `state_weight` | constant map | `{starved: 2, blocked: 1, healthy: 0, unstaffed: 0}` | Healthy and Unstaffed are both excluded from the "needs attention" ranking (A4) — only genuinely troubled regions are scored above `0`. |
| `affected_fraction(region)` | float | `[0, 1]` | Tie-breaker within the same `worst_state` tier. |
| `region_problem_severity_score` | float | `[0, 2100]` | Sortable ranking value. |

**Output range**: `[0, 2100]` — any-Starved region always scores at or above `2000`, strictly above
any-Blocked-only region's maximum of `1100`, guaranteeing Starved always outranks Blocked regardless
of how many teammates are affected in either.

**Worked example**: Region A, 6 assigned creatures (1 Starved, 1 Blocked, 4 Healthy):
`worst_state = starved`, `affected_fraction = 2/6 = 0.333`,
`score = 2×1000 + 0.333×100 = 2033.3`. Region B, 6 assigned creatures (3 Blocked, 3 Healthy):
`worst_state = blocked`, `affected_fraction = 3/6 = 0.5`, `score = 1×1000 + 0.5×100 = 1050`. Region A
(`2033.3`) correctly ranks above Region B (`1050`) despite Region B having more affected teammates —
Starved's greater urgency dominates the ranking exactly as intended.

## 5. Edge Cases

1. **Zero regions mastered (a fresh save).** Every World Map tile renders Unconquered (cool wash, no
   infrastructure); the grid remains fully navigable and every tile selectable. The "needs attention"
   sort toggle has no eligible candidates (Formula 2 never applies to an unconquered region) and is
   rendered in its **disabled** interaction state (art-bible §7.7's chrome matrix), not silently
   hidden.
2. **A mastered region with no team assigned.** Renders Unstaffed (§3.5) — infrastructure present,
   fully static. The Aggregate Readout shows the mastery-level label and `idle_efficiency_percent`
   (both still compute normally off `team_quality_score = 0`, `region-mastery-automation-system`
   Formula 3/4) with an **empty** resource-rate list (Formula 1 yields `0` for every resource, all
   omitted per §3.3's display rule) rather than a wall of zero rows. Manage Team carries no badge
   (§3.3's explicit rejection) — its own visibility is the call to action.
3. **All of the player's regions are simultaneously Blocked.** Each tile independently renders the
   flat/desaturated Blocked treatment (§3.5); no meta-level "everything is broken" alert is added on
   top — N independently-legible signals are already sufficient, stated explicitly rather than left
   to an implementer's discretion to invent one.
4. **A region is mid-Mastery-Transition when the player quits or the session ends.** Per A6, the
   mechanical state write (`region-mastery-automation-system` §3.7 step 4) has already committed by
   Phase 2 — before any of this document's own presentation begins. On next load, this document checks
   `mastery_transition_presentation_pending` (A7): if `true`, it replays the Region-View-only portion
   of the ceremony (Phases 3–7; Phase 1's Arena beat is never replayed, since that encounter already
   fully resolved) once, then clears the flag. This happens regardless of exactly which phase the
   interruption occurred in — the resumed ceremony always restarts cleanly from Phase 3.
5. **More regions than fit on one map screen.** The grid paginates/scrolls according to
   `region_tiles_per_page` (§7). Default authored-order sort (A9) keeps pagination predictable; the
   optional attention-sort re-orders within the same paginated structure and never removes a region
   from being reachable.
6. **A region's boss is fought and defeated a second time** (an active re-visit to an already-
   `mastered` region). Per `region-mastery-automation-system` Edge Case 7, this is an ordinary Kill,
   not a Mastery Transition — this document plays **no** ceremony sequence for it. The Region View
   simply reflects the resulting RMP active-kill bonus (and any resulting mastery-level advance) at
   the Aggregate Readout's next scheduled poll, with zero special presentation.
7. **A region advances to a new mastery level while the player is actively viewing its Region View**
   (e.g. Newly Conquered → Partially Mastered from accumulated RMP). The Aggregate Readout's
   mastery-level label and `idle_efficiency_percent` update at the readout's next scheduled poll
   (`region_view_readout_refresh_seconds`, §7) with **no** special transition animation — that
   ceremony is reserved exclusively for the Mastery Transition itself (§3.4). This is a direct
   application of art-bible §4.3's rule that Hearth-Gold-earned-event ceremony is reserved for
   boss-fall/Vow-bind/Legendary-drop specifically; a mastery-level-up is none of those three.
8. **The player attempts to navigate away mid-ceremony** (e.g. a system "back" press during Phase 4).
   Per §3.6, all input except the phase-eligible confirm-to-skip is inert during the Mastery
   Transition — the back press is consumed/ignored, never queued or buffered, so it cannot produce a
   partial or corrupted sequence state.
9. **A future system introduces locked/undiscovered regions** (not defined by any document read for
   this task). Not active for MVP (A10) — this document reserves only a 5th tile visual state
   (dimmed, non-interactive silhouette, no work-loop or sigil-scar detail rendered) as a forward hook;
   the unlock rule itself belongs entirely to whatever system eventually defines it.
10. **Two regions tie on Formula 2's severity score** (identical `worst_state` and identical
    `affected_fraction`). The needs-attention sort falls back to the default authored/progression
    order (A9) as a stable, deterministic tie-break — sufficient for a UI sort, since the score's only
    job is ordering, not a gameplay-critical value.

## 6. Dependencies

### Depends On

- **`encounter-spawn-system.md`** — supplies the **`EncounterTemplate`** records this screen renders as
  the region's selectable encounters: `template_id`, `is_boss`, `power_tier_base`/`min`/`max` (the
  difficulty band shown to the player before committing), and `rare_eligible`. It also owns
  `SpawnEncounter(...)`, which this screen invokes when the player picks a hunt — this document
  performs **selection**, never spawning. Note the boss template is excluded from the random draw and
  is reachable only by explicit request while `boss_available`, so it must render as a distinct,
  deliberate destination rather than as one entry among the rotating encounters.
- **`region-mastery-automation-system.md`** — reads `region_state`, `mastery_level` (Formula 2),
  `idle_efficiency_percent` (Formula 4, consumed directly by this document's Formula 1),
  `region_output_per_tick` (Formula 5, consumed directly by Formula 1), the four Automation
  Capability Stage gates (§3.5, driving §3.1's infrastructure display), and the Mastery Transition's
  mechanical sequence (§3.7), which this document's own presentation sequence (§3.4) rides on top of
  without altering. This document writes none of these fields — read-only throughout. That document's
  own Depended-On-By section already names `region-view-world-map-ui` explicitly, confirming this
  edge is bidirectional.
- **`creature-jobs-evolution-system.md`** — reads `work_state` (Healthy/Blocked/Starved,
  §3.5/Formula 6) per assigned creature, feeding both the Region View's diegetic work-loop rendering
  (§3.2, that document's own content, unmodified) and this document's new aggregate map-scale signal
  (§3.5, Formula 2). Read-only.
- **`accessibility-settings-system.md`** — reads `reduced_motion_enabled` and its exact suppression
  table (§3.4 there, confirming work-loop-derived signals are never suppressed while ambient/
  decorative motion is), `ui_scale_percent` and `HUD_GLYPH_MIN_PX_AT_1080P` (that document's own
  `ui_scale_percent` row already names "Region View" by name among the UI-space screens it scales),
  and `colorblind_preview_mode`.
- **`combat-encounter-system.md`** — reads the `RESOLVING` (3000ms boss-duration default) /
  `COMPLETE` state transitions and the `CreatureDefeated` event that this document's Mastery
  Transition sequence (§3.4, Phases 1–2) hands off from directly. Read-only; this document adds no
  new combat-state logic.

**Flagged bidirectionality gap, not self-edited per this task's file-discipline instruction**:
`creature-jobs-evolution-system.md`'s own Depended-On-By list and `combat-encounter-system.md`'s own
Depended-On-By list do not yet name `region-view-world-map-ui`, and `systems-index.md`'s Dependency
Map (line 139, entry #22) lists only `region-mastery-automation-system` for this document — this
document's real dependency set is broader, as stated above. Matches the same pattern this session's
other autonomous GDDs (`combat-hud`, `creature-roster-ui`, `loot-filter-ui`) have already flagged for
their own equivalent gaps.

**Flagged forward need**: `mastery_transition_presentation_pending` (A7) needs a persistence home.
`save-load-persistence.md` was not read in full for this task and was not edited — flagged here as an
open coordination note for whichever document or process reconciles new persisted fields, matching
`settings-menu-ui`'s own precedent for `first_run_prompt_shown`.

### Depended On By

None — this is a leaf document. `automation-config-ui` (`systems-index.md` #20) is
this screen's forward navigation target (the Manage Team button, §3.2), not a dependent — it does not
read or rely on anything this document defines; it is simply reached from it. When that document is
authored, it should list this document as an entry point, informationally.

### Adjacent Systems (informational, not a dependency in either direction)

- **`design/art/art-bible.md`** §2, §4.4, §6.1, §6.5, §6.7, §7.4, §7.6 — the locked mood, environment,
  and UI-ornament rules this document formalizes into a buildable screen flow throughout.
- **`onboarding-tutorial-system.md`** — may layer a "suggested starting region" affordance on top of
  the World Map grid for new players; not owned or specified here.

## 7. Tuning Knobs

| Knob | Field | Safe Range | Default | Gameplay Effect |
|---|---|---|---|---|
| Map tile grid density | `region_tiles_per_row` | 3–6 | 4 | How many Region Tiles per row before wrapping. Smaller values keep tiles larger (better infrastructure legibility, A2); larger values show more regions per screen. |
| Map pagination threshold | `region_tiles_per_page` | 12–24 | 16 | Tile count before scroll/pagination activates (§5 Edge Case 5). |
| Region View ambient camera drift | `region_view_camera_drift_enabled` (bool) | off / on | off | Purely decorative if enabled; must gate on `reduced_motion_enabled`. Not required by this document. |
| Mastery Transition sweep duration | `mastery_transition_sweep_duration_seconds` | 3–8 | 5 | Phase 4's environmental-sweep length. Explicitly independent of `combat-encounter-system`'s fixed 3000ms Arena `RESOLVING` window — never tune the two together. |
| Mastery Transition minimum hold | `mastery_transition_min_hold_seconds` | 1.5–3 | 2 | Phase 5's mandatory, non-skippable payoff hold before skip becomes eligible (A8, on any playthrough after the first). |
| Ceremony summary beat duration | `mastery_transition_summary_duration_seconds` | 3–6 | 4 | Phase 6's High-ceremony text/ornament overlay length before auto-advancing to Phase 7 if not skipped. |
| Map-tile distress flicker period | `map_tile_distress_flicker_period_seconds` | 1.5–3 | 2 | Starved-state cadence on the region's infrastructure at both map and Region-View scale. **Must stay well below the WCAG 2.3.1 three-flashes-per-second threshold** (a 2s period is 0.5 Hz — see §8 AC 8) at every value in this range. |
| Aggregate readout refresh cadence | `region_view_readout_refresh_seconds` | 1–5 | 2 | Poll interval for the Aggregate Summary Readout (§3.3) and the map-scale severity signal (§3.5). Neither needs per-frame precision. |

## 8. Acceptance Criteria

1. **Automation stage legibility without a menu (required by the brief)**: for a mastered region at
   each of its 3 reachable infrastructure combinations (Stage 1–2 only, +Stage 3, +Stage 4), a tester
   correctly identifies the exact set of standing infrastructure objects present — at **both**
   World-Map-tile scale and Region-View scale — with zero menu opened, verified by a walkthrough
   across all 3 combinations at both zoom levels.
2. **Blocked/Starved spotting at map scale (required by the brief)**: given a World Map grid of 6
   mastered region tiles with exactly 1 tile Starved (or, in a second pass, Blocked), a tester
   correctly identifies the troubled tile within a short glance-test window, verified independently
   for both states, and with a grayscale filter applied as a second pass confirming motion alone still
   carries the signal (§3.5's design test).
3. **The Mastery Transition never cuts directly to a menu (required by the brief)**: a code-level
   check confirms no execution path exists from "Mastery Transition in progress" (Phases 1–6) directly
   to `automation-config-ui`, the World Map, or any screen other than the interactive mastered Region
   View reached at Phase 7 — verified by asserting the screen-state machine has no such transition
   edge.
4. The Aggregate Readout's mastery-level label and `idle_efficiency_percent` numeral always match
   `region-mastery-automation-system`'s own live Formula 2 / Formula 4 output for that region,
   verified with one fixture per mastery-level tier.
5. Formula 1's worked example reproduces exactly: two Healthy Producers, `role_output_per_tick = 12`
   each, `work_tick_interval_seconds = 40`, Fully Mastered (`idle_efficiency_percent = 94.75`) →
   `displayed_yield_rate_per_hour = 2047` units/hour.
6. Formula 2's worked example reproduces exactly: a 6-member team with 1 Starved + 1 Blocked scores
   `2033.3`; a separate 6-member team with 3 Blocked scores `1050`; the first ranks above the second
   under the needs-attention sort.
7. Healthy, Blocked, Starved, and Unstaffed remain four distinguishable states by motion-presence/
   cadence alone under a full grayscale filter, at both map-tile and Region-View scale — verified as
   an explicit colorblind-safety pass.
8. `map_tile_distress_flicker_period_seconds` never produces a flicker frequency approaching the WCAG
   2.3.1 three-flashes-per-second threshold at any value within its safe range (1.5–3s → 0.33–0.67 Hz)
   — verified by computing Hz across the full tuning range and asserting it stays below the threshold
   with margin.
9. `reduced_motion_enabled = true` suppresses only purely ambient/decorative motion on this screen
   family (unconquered-tile idle-view flicker, any optional camera drift) while leaving every
   work-loop-derived signal (per-creature in the Region View, and the aggregate tile-level signal)
   fully active, and leaves every phase of the Mastery Transition sequence unsuppressed and
   un-shortened — verified as three independent assertions against `accessibility-settings-system`'s
   suppression table.
10. A Mastery Transition interrupted by session end at any point after Phase 2 resumes and completes
    the Region-View-only portion of the ceremony (Phases 3–7) exactly once on the next session load,
    then never again for that region — verified by simulating interruption at three distinct points
    within Phases 3–5.
11. The Region View's chrome element count matches §3.7's inventory exactly for each of its 3
    reachable interactive states (unconquered: 2 buttons/0 readout; mastered: 3 buttons/1 readout
    cluster; mid-ceremony Phases 1–5: 0 chrome, Phase 6: 1 overlay) — verified by a direct UI-element
    audit against the named inventory.
12. Every text element on this screen family remains legible, unclipped, and non-overlapping at
    `ui_scale_percent = 200` (`accessibility-settings-system`'s maximum) — verified across the World
    Map grid, both Region View states, and the Phase 6 ceremony overlay.
13. A full keyboard-only playthrough and a full gamepad-only playthrough can each complete World Map
    tile selection → Region View → Hunt or Manage Team hand-off, and skip an eligible Mastery
    Transition, with zero mouse input at any point.

---

## Cross-System Facts Proposed for Registration

This document does not write to `design/registry/entities.yaml` directly, per this task's explicit
file-discipline instruction. The following are proposed for registration by whichever process
coordinates registry writes this session:

**Formulas**:
- `displayed_region_yield_rate` — §4 Formula 1. Presentation-layer only; consumes
  `region-mastery-automation-system`'s registered `idle_efficiency_percent` and `region_production_
  rate` formulas by exact name.
- `region_problem_severity_score` — §4 Formula 2. Flagged as reusable by any future notification or
  automation-summary system that needs the same troubled-region ranking (e.g. `automation-config-ui`).

**Constants**:
- `region_tiles_per_row` (`4`), `region_tiles_per_page` (`16`) — §7.
- `mastery_transition_sweep_duration_seconds` (`5`), `mastery_transition_min_hold_seconds` (`2`),
  `mastery_transition_summary_duration_seconds` (`4`) — §7. Explicitly independent of
  `combat-encounter-system`'s own 3000ms `RESOLVING` default — never conflate the two in a future
  retune.
- `map_tile_distress_flicker_period_seconds` (`2`) — §7. Checked against the WCAG 2.3.1 flash-safety
  floor at authoring time (§8 AC 8); any retune must re-verify against that same floor.
- `region_view_readout_refresh_seconds` (`2`) — §7.

**New field flagged for a persistence home** (not registered as belonging to this document's own
schema — it needs a home in whatever document owns per-region persisted state):
- `mastery_transition_presentation_pending` (bool, per region, default `false`) — §3.4 Phase 2, A7.
  Needed to correctly resume an interrupted Mastery Transition ceremony (§5 Edge Case 4). Likely
  belongs alongside `region-mastery-automation-system`'s own `last_ticked_at` field, but that document
  was not edited per this task's file-discipline instruction — flagged here for future reconciliation.

**Not proposed for registration** (deliberately): the exact screen-flow phase sequence (§3.4) and the
chrome inventory (§3.7) — these are structural/presentation facts specific to this document with no
other canonical document to diverge against, matching the precedent other Presentation-tier documents
in this project have already set for their own structural facts.
