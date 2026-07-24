---
name: resonance-hunter-region-view-world-map-ui
description: region-view-world-map-ui GDD authored 2026-07-14, closing systems-index.md #22; formalizes art-bible's own named UI showcase (Principle 3 payoff) — invents the map-scale problem-signal grammar and the precise Mastery Transition presentation sequence, both genuine new gaps
metadata:
  type: project
---

`design/gdd/region-view-world-map-ui.md` was authored 2026-07-14 (autonomous session, no user
available) as the interface layer for `region-mastery-automation-system.md` (region state, mastery
levels, RMP, idle efficiency) and `creature-jobs-evolution-system.md` (Healthy/Blocked/Starved
work-state, per-role work-loop animation). Closes `systems-index.md` entry #22. This is the screen
art-bible invested more design depth in than any other (§2, §6.1, §6.5, §6.7, §7.4, §7.6 all speak to
it directly) — most of the task really was formalizing an already-rich spec, but two genuinely new
design problems had no existing answer and required real invention, not just transcription.

**Key design calls made, worth knowing before touching this document or art-bible again**:

- **The World Map is a region-select grid (each tile a reduced-scale render of that region's own
  Region View composition), not a geographic atlas** — justified directly by art-bible §6.7's own
  phrase "simplified to read at **region-select** thumbnail scale," which presupposes a select-grid
  mental model. This avoids inventing an unbudgeted new background-art asset class nowhere named in
  art-bible §8's asset standards. Tile size is deliberately "medium thumbnail," not a minimap pin —
  chosen specifically so Mid-read-distance infrastructure (ward-post, cairn) stays countable at map
  scale, not only the Far-read beacon art-bible explicitly designed for that job.
- **The map-scale "spot the troubled region" signal was a genuine gap — no document specified it.**
  Resolved by reusing art-bible §5.3's exact Healthy/Blocked/Starved color-and-motion grammar
  *verbatim*, aggregated to the assigned team's worst state (Starved > Blocked > Healthy, Starved-
  first matching `creature-jobs-evolution-system`'s own priority order), rendered on the region's own
  standing infrastructure rather than any new chrome badge/icon — the direct application of art-bible
  §7.2's diegetic-first icon test to a case that test hadn't been asked to cover yet. A 4th state,
  **Unstaffed** (no team assigned), was added and deliberately given *no* alarm treatment — only the
  literal absence of the work-loop pulse distinguishes it from Healthy — specifically to avoid
  semantically overloading the one channel the whole game trains players to read as urgent ("not yet
  configured" and "actively struggling" are different player problems).
- **New Formula 2 (Region Problem Severity Score)** answers the task's explicit "which region needs
  attention MOST" requirement: `state_weight × 1000 + affected_fraction × 100`, guaranteeing any-
  Starved region always outranks any-Blocked-only region regardless of team size affected. Drives only
  an *optional* needs-attention sort — the per-tile visual signal itself needs no ranking to be
  legible, kept as a secondary convenience (A9) so the map is never silently reordered on the player.
- **The Mastery Transition presentation sequence is fully specified as 7 numbered phases** (Arena
  glyph-inversion → mechanical commit → camera hand-off Arena-to-Region-View → environmental sweep →
  held silence → High-ceremony summary overlay → control returns), deliberately decoupling the
  *mechanical* state write (which fires instantly at Phase 2, per `region-mastery-automation-system`
  §3.7 step 4) from the *presentation* sequence that continues independently of it. This decoupling is
  what makes a safe interrupted-ceremony resume possible without ever risking a double-applied or
  ambiguous state transition.
- **New UI-only field, not registered anywhere else**: `mastery_transition_presentation_pending`
  (bool, per region) — set true at Phase 2, cleared at Phase 7 — resumes an interrupted ceremony
  (player quit mid-cinematic) on next load rather than silently skipping the game's single most
  important visual beat. Flagged as needing a persistence home in whatever document owns per-region
  save state (likely alongside `region-mastery-automation-system`'s own `last_ticked_at`); that
  document was not edited, per file discipline.
- **A skip-eligibility rule not requested by any source doc, added as a deliberate UX call**: the full
  ceremony (including the environmental sweep) is 100% unskippable on a player's *first-ever* Mastery
  Transition in a save file, protecting art-bible §2's explicit "no instant cut" instruction for the
  one moment it matters most. From the second transition onward, skip becomes available starting at
  the mandatory ~2s payoff hold (never during the sweep itself). Reasoned as respecting both the
  first-time emotional beat and, at Full Vision scale (many regions), the player's long-term time.
- **Reduced-motion handling is stated explicitly rather than left implicit**: the new map-scale
  distress signal inherits the *same* "never suppressed, information-bearing" classification
  `accessibility-settings-system` already gives per-creature work-loop pulses (since it's structurally
  the same signal, just aggregated and relocated) — flagged explicitly so a future implementer doesn't
  mistakenly treat a "flicker" as decorative-and-therefore-suppressible by default.

**Flagged, not yet actioned** (matching the accumulating pattern across this session's autonomous
GDDs — see [[resonance-hunter-creature-roster-ui]], [[resonance-hunter-loot-filter-ui]],
[[resonance-hunter-combat-hud]]): `creature-jobs-evolution-system.md`'s and
`combat-encounter-system.md`'s own "Depended On By" lists do not yet name
`region-view-world-map-ui`; `systems-index.md` entry #22's Dependency Map (line 139) lists only
`region-mastery-automation-system`, missing the real dependencies on `creature-jobs-evolution-system`,
`accessibility-settings-system`, and `combat-encounter-system`. None self-edited, per this session's
file discipline. The centralized `systems-index.md`/dependency-map reconciliation pass this session
keeps flagging across every autonomous GDD (now at least five documents deep) is increasingly overdue.

**How to apply**: before extending or reviewing `region-view-world-map-ui.md`, or before designing
`automation-config-ui` (the last named UI-tier system still Not Started per `systems-index.md` #20,
and this document's own forward navigation target via the Manage Team button), re-read the file
directly — this is a snapshot of design calls made at authoring time, not a substitute for the current
file. The worst-state-aggregation-onto-existing-diegetic-grammar technique (rather than inventing new
chrome) and the mechanical-write/presentation-sequence decoupling pattern are both good precedent to
reuse if `automation-config-ui` or any future screen needs a similar "surface an aggregate state
without a new UI language" or "a ceremony beat must survive an interruption" problem solved.
