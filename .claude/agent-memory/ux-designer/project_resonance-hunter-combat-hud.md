---
name: resonance-hunter-combat-hud
description: combat-hud.md (system #17) authored 2026-07-14, formalizing art-bible §7.5's near-complete spec into an implementable contract — resolves several gaps art-bible left open (dodge charge display, numeral precision tiering, damage-taken confirmation) and defines a concrete, testable belt-vs-corner fallback trigger
metadata:
  type: project
---

`design/gdd/combat-hud.md` was authored 2026-07-14 (autonomous session, no user available),
closing `systems-index.md`'s previously-inferred entry #17. It formalizes art-bible §7.5's
Combat HUD Specification (see [[resonance-hunter-section7-ux-alignment]] for that section's
full lock history) rather than re-litigating it — per that section's own instruction, this
document's job was to *complete* the spec, not redesign it.

**Key design calls made, worth knowing before touching this document or art-bible §7.5 again**:

- **Dodge charges get their own diegetic belt pip-track**, positioned adjacent to (not part of)
  the 4 Vow-scar-linked ability/ultimate pip-tracks. Art-bible §7.5's locked table never
  addressed dodge display at all, but `combat-encounter-system.md`'s own dependency note
  explicitly requires `combat-hud` to render "dodge charge count" — this was a genuine gap in
  the "nearly complete" spec that had to be resolved as a new design call, not an oversight in
  this document.
- **Numeral precision (exact cooldown seconds, exact dodge-regen seconds) is corner-cluster
  fallback-only** — belt-primary mode never shows cooldown numerals at all, only pip-count
  granularity. HP/Resonance numerals, by contrast, are always available (they're already
  screen-space chrome with room). This resolves the tension between the task's requirement for
  a "deliberate-look = exact cooldown seconds" tier and the explicit "no separate cooldown icon
  row" ban — and gives the corner-cluster fallback a second concrete benefit beyond legibility
  (it's the only mode that can offer that precision tier at all).
- **Health pips are strictly binary (lit/unlit, no partial fill); Resonance segments and the
  regenerating dodge-charge pip use a partial gauge-style fill.** This distinction is read
  directly out of art-bible §7.5's own different verbs for the two meters ("discrete pip loss"
  vs. "segmented wedge-fill... a partial radial fill") — not an arbitrary UX preference.
- **Damage-taken/avoidance-type feedback (block/dodge/interrupt) reuses existing elements —
  zero new HUD widgets.** Block confirms via the HP pip-bar's own pip loss. Dodge/interrupt
  confirm via the Resonance meter's own wedge-fill jump (different magnitudes: 8 vs. 15 base
  gain). Interrupt's *primary* confirmation is already fully diegetic and needs no HUD
  involvement at all — the creature's own telegraph glyph hard-cuts early on a successful
  interrupt (`creature-ai-telegraph-system` Edge Case #2). **How to apply**: if a future pass
  is tempted to add a dedicated "block/dodge/interrupt" icon or banner, that would be a reversal
  of this locked decision, not a natural extension.
- **The Vow-condition ring renders in chrome-neutral color only — no Hearth Gold or Ember
  Threat tint**, even though the task's own instruction allowed a shape-plus-reinforcing-color
  approach. Justified by extending art-bible §4.3's "Hearth Gold is spent like a resource,
  reserved for boss-fall/Vow-bind/Legendary-drop" rule (the same reasoning already applied to
  the Ultimate pip's "no color" rule) — a Vow flipping satisfied mid-fight isn't one of those
  three reserved events.
- **The belt-vs-corner-fallback switch is a single, build-wide flag set once from Vertical
  Slice playtest data, never a per-player runtime setting.** Concrete, testable trigger criteria
  are defined (§3.9 of the doc: <80% correct-identification rate on two specific 1-second glance
  tests, or >25% unprompted legibility complaints, across n≥12 playtesters). This was
  deliberately checked against `accessibility-settings-system.md` §3.6, which already states
  explicitly "no additional combat-HUD density control is proposed" — adding a player-facing
  toggle here would have silently contradicted that locked sibling document.

**Flagged, unresolved gaps — check before assuming these are settled**:
1. `creature-data-schema.md`'s own "Depended On By" list does not include `combat-hud` (that
   document predates the "part-break has zero HUD footprint" resolution in art-bible §7.5).
   Not self-edited per file discipline; documented as a confirmed *absence* of a rendering
   dependency in `combat-hud.md` §6, for a future centralized reconciliation pass.
2. `systems-index.md`'s Dependency Map for entry #17 still only lists `combat-encounter-system`
   and `vow-condition-tracking`; it does not yet reflect the `accessibility-settings-system`
   edge (a full consuming dependency) or the two boundary-only edges to
   `creature-ai-telegraph-system` and `creature-data-schema`. Also, entry #17's "(inferred)" /
   "Not Started" labels are now stale (this document exists) but were not self-edited.

**How to apply**: before extending or reviewing `combat-hud.md`, or before implementing
`ui-programmer` work against it, re-read the file directly rather than relying solely on this
summary. If a future session touches `systems-index.md` or `creature-data-schema.md`, both
flagged gaps above are still open reconciliation items.
