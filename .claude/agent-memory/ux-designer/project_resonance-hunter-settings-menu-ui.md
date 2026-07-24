---
name: resonance-hunter-settings-menu-ui
description: settings-menu-ui GDD (system #25) authored 2026-07-14, closing the screen-less-settings-surface gap left by accessibility-settings-system — includes a proposed schema addition flagged but not yet applied to the spine doc
metadata:
  type: project
---

`design/gdd/settings-menu-ui.md` was authored 2026-07-14 (autonomous session, no
user available) to close a gap: `accessibility-settings-system.md` (also
authored 2026-07-14, same session) defined a full settings data model/surface
with no screen designed to host it. `systems-index.md` added it as system #25
(UI tier, MVP, depends on accessibility-settings-system) during that same
session, before this document existed.

**Key design calls made, worth knowing before touching either document again**:

- **Taxonomy**: hybrid — a curated "Accessibility" tab placed *first* in tab
  order (Accessibility, Display, Audio, Input), containing live-bound
  duplicate widgets (same underlying field, two widgets) for the
  highest-impact settings, while Display/Audio/Input remain each setting's
  contextual native home. Chosen over a single dedicated tab because nearly
  the entire settings surface *is* accessibility-relevant by that document's
  own framing — a single Accessibility tab would have been lopsided against
  near-empty Display/Audio tabs.
- **Bootstrap-accessibility problem** (settings menu must be legible before
  its own legibility settings are applied) resolved via 4 mechanisms:
  guaranteed-legible default baseline (inherits accessibility-settings-system
  §3.8's 18px/16px floors), a first-run prompt surfacing UI Scale before
  gameplay, live preview (the settings screen previews itself, no separate
  preview widget), and `ui_scale_percent`'s hard `[100,200]` clamp.
- **The "evil twin"** (UI scale set so high the controls to undo it become
  unreachable) resolved via a header/footer chrome strip *structurally exempt*
  from `ui_scale_percent`'s scaling (always holds Reset controls), reflow
  (never overlap/clip), and a revert-on-timeout (15s default) applied *only*
  to `ui_scale_percent` — deliberately not to `high_contrast_mode` or
  `colorblind_preview_mode`, since only scale risks unreachability.
- **`colorblind_preview_mode` is explicitly labeled "(Simulation)"**, never
  "corrective" — it's a self-verification tool per the spine doc's Assumption
  A3 (the art is colorblind-safe by construction, nothing to correct).

**Flagged, unresolved gap — check before assuming this is settled**: this
document proposes an additive field, `first_run_prompt_shown` (bool, default
false), to `accessibility_settings.json`'s schema, needed to gate the
first-run prompt to exactly once. Per that session's file-discipline
constraint, `accessibility-settings-system.md` was **not** edited to add this
field — it exists only in `settings-menu-ui.md`'s own text (Assumption B2,
§6 Dependencies). If a future session touches
`design/gdd/accessibility-settings-system.md`, check whether this field has
been reconciled into that document's actual JSON schema example yet; if not,
it's still an open reconciliation item, not yet a contradiction, but a real
gap between what two GDDs each claim about the same file.

**Also flagged, not yet actioned**: `settings-menu-ui.md` surfaces a real
dependency on `input-targeting-system.md` (rebindable action set, device
coexistence model) that `systems-index.md`'s Dependency Map does not yet
reflect for system #25 (it currently only lists the
accessibility-settings-system edge). Not self-edited, per file discipline.

**How to apply**: before extending or reviewing the settings menu, or before
implementing `ui-programmer` work against it, re-read
`design/gdd/settings-menu-ui.md` directly rather than relying solely on this
summary — this is a snapshot of the design calls made at authoring time, not
a substitute for the current file.
