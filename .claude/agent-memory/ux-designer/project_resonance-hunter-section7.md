---
name: resonance-hunter-section7-ux-alignment
description: Resonance Hunter art-bible Section 7 (UI/HUD) is now locked — all 6 tensions flagged 2026-07-13 are resolved as of 2026-07-14; one resolved opposite to this agent's original recommendation
metadata:
  type: project
---

Resonance Hunter (idleXidle repo, MonoGame/C#, 2D pixel-art idle action RPG) —
`design/art/art-bible.md` Section 7 (UI/HUD Visual Direction) is now **Complete**
(verified by reading it in full on 2026-07-14, while authoring `design/gdd/forge-ui.md`).
It was authored jointly by `art-director` and `ux-designer` "in parallel, then
reconciled," with three conflicts explicitly recorded inline and decided by the user.

**Resolution status of the six tensions flagged 2026-07-13** (see the original alignment
note below for full detail on each):

1. **Vow live-condition-state visual mechanism — resolved.** §7.3 scopes §3.5's
   "only the vulnerability glyph animates" rule to the *diegetic combat stage* only,
   and gives the HUD overlay tier its own narrow motion budget: "continuous, rhythmic,
   looping motion remains the vulnerability glyph's exclusive signature. HUD elements
   may only play a single, discrete, one-shot beat on a state transition — never a
   loop." A Vow condition flip gets exactly one discrete flash. This is recorded as a
   **DECISION (user)** in the doc, resolving the art-director/ux-designer disagreement
   in favor of this agent's original recommendation.
2. **Part-break progress HUD bars — resolved, exactly as recommended.** §7.5: "Part-break
   progress... **Diegetic** — crack/value escalation directly on the creature's own
   part-seam (3.1). **No HUD element at all.**" Reuses 3.3's crack grammar in reverse.
3. **HP/Energy/cooldowns/Vow-state placement — resolved, but OPPOSITE to what this
   agent recommended.** §7.5's Combat HUD Specification keeps Health and Resonance as
   conventional screen-space HUD (as recommended), but puts the **3 ability cooldowns,
   the ultimate cooldown, AND live Vow-condition state on the Hunter's own body** — a
   diegetic pip-track and ring-toggle beside the belt-charm Vow scar (art-bible §5.2),
   not an off-body HUD cluster. Rationale given: §7.4's "diegetic-first, tiered by
   precision" philosophy — coarse/categorical states (ability ready, Vow satisfied)
   belong on the world/body; only precise magnitudes (exact HP, exact seconds) get
   screen-space chrome. **The legibility risk this agent flagged is explicitly
   acknowledged in the doc itself**, not overlooked: §7.5 has an "Open flag
   (playtesting)" noting the belt charm sits at only 10–15% of frame height (art-bible
   §5.1) and whether the pip-track/ring-toggle stay legible at that scale "cannot be
   settled on paper." A **pre-authorized fallback** is already specified: mirror the
   same pip/ring states in the corner screen-space chrome cluster at larger scale if
   playtesting shows the belt reads poorly — "same shape grammar, more pixels, not a
   redesign." **How to apply**: if a future combat-hud or playtest pass raises exactly
   this legibility concern, the answer is already pre-approved in §7.5 — don't
   re-litigate diegetic-vs-screen-space placement, just check whether the fallback
   needs to trigger.
4. **Gamepad-only/keyboard-only targeting equivalent — resolved.** §7.7 locks a
   discrete cycle-and-confirm model (bumper/D-pad cycles valid part targets, a confirm
   button commits), explicitly rejecting stick-cursor mouse emulation as a Pillar-1
   violation — recorded as a **DECISION (user)**, matching this agent's original
   recommendation almost exactly. `input-targeting-system.md` and
   `accessibility-settings-system.md` both build on this decision.
5. **Flash-frequency ceiling for telegraph pulse — resolved.** §7.7: "A minimum
   telegraph wind-up duration is fixed as a floor, regardless of region difficulty
   scaling... guarantees no region ever crosses into strobe territory (WCAG 2.3.1
   three-flashes-per-second threshold)." `accessibility-settings-system.md` §3.4/§3.7
   makes this floor non-adjustable and never exposed as a player-facing (or difficulty)
   setting.
6. **HUD-space glyph minimum size floor — resolved.** §7.7: "HUD-tier glyphs need an
   independent minimum on-screen size floor... does **not** transfer" from §5.6's
   world-space 12–16px rule. `accessibility-settings-system.md` §3.2 operationalizes
   this as `HUD_GLYPH_MIN_PX_AT_1080P = 16` (Formula 1), matching art-bible §8.2's
   glyph-micro/chrome-icon native asset size.

**Why**: Confirms the six gaps flagged in this agent's first UX pass on Section 7 are
now closed in the locked document, and records the one case (item 3) where the final
decision diverged from this agent's own recommendation, plus the specific fallback
already pre-approved for the risk that divergence reintroduces.

**How to apply**: Section 7 no longer needs re-auditing against the original six-item
list — treat it as locked. When designing or reviewing `combat-hud` (Presentation-tier,
not yet authored per `systems-index.md`), remember cooldowns and Vow-condition state are
diegetic-on-body per §7.5, not off-body HUD widgets, and that the corner-cluster mirror
fallback is pre-authorized (not a new design decision) if belt-scale legibility fails in
practice. `design/gdd/forge-ui.md` (authored 2026-07-14) is the first Presentation-tier
document to build on this locked Section 7 plus `accessibility-settings-system.md`'s
binding requirements.

**Update 2026-07-14 (later same day)**: `design/gdd/combat-hud.md` has now been authored,
closing this section's own Open Items entry ("Belt-charm legibility... owner: ux-designer,
`/ux-design`" is now discharged as a concrete, testable trigger condition rather than an open
question). See [[resonance-hunter-combat-hud]] for the specific design calls made — several
gaps art-bible §7.5 left genuinely open (dodge charge display, numeral-precision tiering,
damage-taken confirmation mechanism) required new decisions, not just formalization.
