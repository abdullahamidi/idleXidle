# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: MonoGame 3.8.4.1
- **Language**: C# (.NET 8+)
- **Rendering**: 2D Sprite Batch (MonoGame default) — no 3D pipeline needed
- **Physics**: [TO BE CONFIGURED — MonoGame has no built-in physics engine; select a 2D physics library or custom solution when that work begins]

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Steam / Epic)
- **Input Methods**: Keyboard/Mouse (primary), Gamepad (full, via cycle-and-confirm targeting)
- **Primary Input**: Keyboard/Mouse — precision weak-point targeting is mouse-driven
- **Gamepad Support**: Full, but **not** via stick-cursor emulation. Combat targeting uses a discrete **cycle-and-confirm** model (bumper/D-pad cycles valid part targets, face button commits). See art-bible §7.7.
- **Touch Support**: None
- **Platform Notes**: Mouse targeting is the primary path, but every combat interaction must also be completable via cycle-and-confirm, which doubles as the keyboard-only and motor-accessibility path. Stick-cursor emulation is explicitly rejected — it converts a reading skill into a pointing skill and violates Pillar 1. Click-hitboxes must be padded generously beyond visual seam boundaries (Fitts's Law). No hover-only interactions.

## Naming Conventions

- **Classes**: PascalCase (e.g. `ResonanceAbility`)
- **Variables**: Public fields/properties PascalCase (e.g. `MoveSpeed`); private fields `_camelCase` (e.g. `_currentHealth`)
- **Signals/Events**: PascalCase + `EventHandler` suffix delegates (e.g. `HealthChangedEventHandler`)
- **Files**: PascalCase matching class (e.g. `ResonanceAbility.cs`)
- **Scenes/Prefabs**: N/A — MonoGame has no scene/prefab system; use PascalCase for equivalent content definitions (e.g. entity templates, level data classes)
- **Constants**: PascalCase (e.g. `MaxHealth`)

## Performance Budgets

- **Target Framerate**: 60 FPS
- **Frame Budget**: 16.6 ms
- **Draw Calls**: No hard engine cap (MonoGame is lower-level). Planning budget: ~10–20 `SpriteBatch.Begin()`/`End()` boundaries per frame, low hundreds of GPU draw calls. `SpriteBatch` merges consecutive same-texture draws — batching is driven by **texture-switch count**, not sprite count. Never switch `Effect` per-entity. See art-bible §8.5.
- **Memory Ceiling**: 512 MB resident texture memory (working estimate ~125–160 MB — headroom for roster growth). See art-bible §8.8.

## Testing

- **Framework**: xUnit
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
> **UPDATED 2026-07-29 — render pipeline pivoted to hand-drawn "vector" art.** The project moved off the
> flat-fill/pixel-perfect direction: the live renderer uses `SamplerState.LinearClamp` (smooth filtering)
> with **non-integer scaling allowed** and premultiplied-at-load alpha (see `Game1.cs`,
> `SoloExpeditionScreen.cs`; authoritative art contract: `design/art/asset-integration-spec.md`;
> `docs/architecture/ADR-003` is now Superseded). The two pixel-art bullets below were corrected to match.
- **`SpriteSortMode.Immediate`** outside debugging — disables batching entirely (one GPU draw call per `Draw()`).
- **Per-entity `Effect` switching** — forces a batch flush per entity. Use one shared shader per pass; vary per-entity via the `Draw()` Color tint parameter.
- **BC/DXT texture compression on character, creature, glyph, or UI-content art** — block compression bands/bleeds across the hand-painted gradients and edges the art depends on. Author RGBA8 uncompressed; block compression is for large decorative layers only.
- **PointClamp / integer-only scaling as a hard rule** — *no longer required.* The art is hand-drawn and rendered with `SamplerState.LinearClamp`; non-integer scaling is expected and fine. (The old rule — PointClamp + integer scale for flat-fill pixel art — described the abandoned direction; pixel art is no longer the house style.)
- **Stick-cursor gamepad emulation for combat targeting** — see Input & Platform above.
- **Baking part-break damage states as full-body frame variants** — a ~12× texture multiplier. Part-break is a composited decal overlay. See art-bible §8.4.

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here. Each should have a corresponding ADR. -->
Selected in art-bible §8.7; each still needs a formal ADR at `/create-architecture`, and should
only be added here once actually integrated:

- **Myra** — UI framework (MonoGame ships none). Chosen for real data-grid/list primitives, needed by the dense tabular screens.
- **FontStashSharp** — dynamic TTF rasterization. `SpriteFont` is bitmap-only; art-bible §7.1 requires a scalable data font.
- **MonoGame.Extended** (Tweening at minimum) — drives curve evaluation for the homebrew cutout animation rig.
- **MonoGame.Aseprite** — atlas/frame import direct from `.aseprite` source (pending confirmation that Aseprite is the authoring tool; a custom MGCB pipeline extension is the no-dependency fallback).

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- ADR-001 pure-logic Core (Accepted) · ADR-002 cutout animation rig (Accepted) · ADR-003 pixel-perfect render path (Superseded) · ADR-004 warren economy (Superseded)
- ADR-005 UI SCALE is a density profile, the cursor is mapped once, motion has one vocabulary (Accepted 2026-09-01) — `docs/architecture/ADR-005-ui-density-profile-and-one-cursor.md`
- ADR-006 Draw must not consume input: Update owns every edge, and both halves read one geometry (Accepted 2026-09-12) — `docs/architecture/ADR-006-draw-must-not-consume-input.md`
- ADR-007 One surface at a time: the attention owner, and the inbox background news goes to instead (Accepted 2026-09-15) — `docs/architecture/ADR-007-attention-ownership-and-dispatches.md`
- ADR-008 One acknowledgement grammar, and a spotlight that is never a lie (Accepted 2026-09-21) — `docs/architecture/ADR-008-one-acknowledgement-grammar-and-presentation-only-spotlights.md`
- ADR-009 A VFX texel adds its own light once: `VfxBlend.PremultipliedAdditive` (Accepted 2026-09-23) — `docs/architecture/ADR-009-vfx-premultiplied-additive-blend.md`
- ADR-010 A projectile is composed at runtime, and its body has one size (Proposed 2026-09-23, Seeker pilot awaiting owner approval) — `docs/architecture/ADR-010-composite-projectiles-and-the-body-size-contract.md`
- ADR-011 An action is performed, and its contact is the beat: authored clip timing, hand sockets, one object in the hand and in the air, the action handoff (Accepted 2026-09-25 — the Seeker's SPRAY is the PROJECTILE / TRAVEL reference and his HARD HANDS the MELEE / DIRECT-CONTACT reference, both accepted with their human-approved sounds; skill-specific recipe isolation is an accepted contract; JAWS is ACCEPTED (2026-09-28) as the GOLD-STANDARD REACTION reference on its own layer with the Core `ReactionArmed` report: the frontal smoke-teeth Shadow bite and its real-foley bite cue, all owner-approved; the bear trap, the piranha and the side-view maw were NOT approved; PRESS is ACCEPTED (2026-09-29) as the GOLD-STANDARD FIELD / AURA reference on its own field layer: the field-propagation picture of `fab25919` and its human-approved tick cue, candidate A BALANCED) — `docs/architecture/ADR-011-authored-actions-contact-on-the-beat.md`
- ADR-012 The bite is performed and the hit is received: the pack's spaced attack and front-led lunge, the quiet default flash, the generic capture views, attack art extent separate from canonical actor bounds (Partly Accepted; the whelp attack work CLOSED 2026-09-27 with the redrawn 640-canvas lunge and ~20 % travel; the bite contact-motif requirement removed; receiver recoil 0) — `docs/architecture/ADR-012-the-bite-is-performed-and-the-hit-is-received.md`
- ADR-011 addendum (2026-09-28): JAWS is A FRONTAL SHADOW BITE after the owner's reference (Roni Kangaskorte's "Bite VFX"): a crown of leaf-shaped fangs with "( )" canines and a lower row condense out of mist around the attacker's head, charge (lavender-grey to magenta), slam shut white-hot, and burst into a flash, a ring, speed lines and splinters (~677 ms; 7 procedural parts, seeker_bite.py). The concept is APPROVED; the shadow-mist polish (2026-09-28) made it one Shadow phenomenon: a near-black mist drawn BEHIND the creatures with one lifecycle (0.07 → 0.38 → 0.45 at the snap, compressed to 0.92, released to 1.22), white-hot for one frame, a 140 ms flash cooling to violet, the slash removed (it read as a blade), and a QUIET mode under SPRAY / HARD HANDS and on a second creature (no white-hot, dimmer flash, no speed lines). Teeth, glow, soft ring and splinters under the champion; the crisp ring, speed lines and flash in the light pass; one cue on the snap; no flash of the creature's sprite; the number after the snap; then, at the owner's word ("more like smoke, more like mist, a little lower opacity"), the teeth are SMOKE held in a tooth's shape (formed 0.62, snap 0.88) and on the snap frame only the smoke condenses hard into the solid SnapCell, white-hot; this PICTURE IS APPROVED (2026-09-28, page https://claude.ai/artifact/PGbMWLcVTPtJzxaeDj2Jr1). The one cue is a REAL BITE: `sfx_seeker_jaws_bite` is built by make_jaws_bite.py from CC0 recorded foley (tools/asset-pipeline/foley/jaws_bite/), shaped to the measured structure of the owner's reference (Trundle's Q in League of Legends; analysed locally, never sampled or shipped); the synthesised bites were rejected; candidates A CHOMP in the game, B BONE, C JUICY; the rejected cues live in tools/asset-pipeline/audio_history/; the owner chose A: JAWS is ACCEPTED 2026-09-28 as the GOLD-STANDARD REACTION reference and CLOSED; the approved cue is pinned by its SHA-256 in jaws_reaction_test.cs (page https://claude.ai/artifact/6tP43HjDMCtfKGWeHzHCRu).
- ADR-011 addendum (2026-09-28/29): PRESS is ACCEPTED (2026-09-29) as the GOLD-STANDARD PERSISTENT FIELD / AURA reference and CLOSED. Its history: FIRST SLICE (Concept A, after Syndra E): a quiet pressure haze forward of the Seeker, a compression before each tick, one crescent pressure front that leaves the field and ARRIVES on the fight's Aura tick, the crescent's tips folding into two arcs that crush the front enemy (its body buckles, `UiKit.Buckle`), a settle; `FieldRecipe` / `FieldPerformance`, keyed by skill id (`FieldRecipes.For`), built per wave from the Aura/Break events; gives way to JAWS on a shared tick; quiet under actions; Core untouched (page https://claude.ai/artifact/EL1ZzQ9EjMYhqw13aF83CG). The owner APPROVED the direction and asked for a PIXEL-HARD pass (2026-09-28): the front built at 1/5 and the arcs at 1/8 of their cells, upscaled NEAREST in four value bands, drawn axis-aligned on whole pixels (one art pixel ~3 screen px, the stage's), the front growing at most 1.15x; a harder tick from timing (the field snaps, the front accelerates into contact, the crush closes in 30 ms, holds 60 at x0.81 / x1.09, a pixel dissolve); the heat only a line on the arcs' pressing edge (lit whole they were as bright as SPRAY's hit); the arcs sized from the pose at launch and placed on the body as it buckles, re-pinned one frame into the crush; the chevrons and the micro push dropped; the owner APPROVED it (page https://claude.ai/artifact/YZNuW29wsUsxXkzJMpN88i). Then the FIELD-vs-PROJECTILE polish (2026-09-28): the front travels LEVEL (halfway between the field's height and the target's, clamped to hold its target), has no glowing centre and no echo (a head, a tail), the launch contours were tried and removed; on contact it stops and FOLDS (a new part, shown exactly once, lit like the front) into arcs that form where the front stopped, latched on the first frame after the contact whatever the frame phase, closing only vertically; the owner APPROVED it as the FIELD / AURA visual direction (commit `fab25919`, page https://claude.ai/artifact/2zZPnjMVQqSTtunKw8RxQw). Then the FINAL AUDIO (2026-09-29): ONE composite tick cue, `sfx_seeker_press_tick`, built by `make_press_tick.py` from CC0 recorded foley: a pressure release (air, not a tone), a compression THUMP at 40 ms, a brittle material-neutral defence CRACK +6..+13 ms after it, no reverb; asked once per tick (`FieldPerformance.CueDue`) at -20 ms so the thump lands in the crush (+23.3 at 60 Hz, between the fold and the arcs shut), never on the launch, no travel audio; `TickVolume` 0.28, never lead, 3.8 / 7.8 / 5.2 dB under SPRAY / HARD HANDS / JAWS K-weighted, x0.6 on a quiet tick (an action close by or the champion performing), the duck alone when ducked, x0.5 giving way to JAWS; a wave-ending tick plays its crush out; three candidates (A balanced, B heavier pressure, C clearer defence break; page https://claude.ai/artifact/13QvWjjw4rQvxSMgyjh2c1); the owner chose A (2026-09-29: "PRESS is APPROVED. Use Candidate A - BALANCED as the final human-approved PRESS tick cue"): A is the canonical `sfx_seeker_press_tick`, HUMAN-APPROVED, its bytes pinned by SHA-256 in `press_field_test.cs` (a change is a new approval, never a regeneration); B and C are review history in `tools/asset-pipeline/audio_history/press_tick/`; PRESS visual + audio are ACCEPTED as the FIELD / AURA gold-standard reference. Decided, not built: no special ducking of the enemy bite on the same tick (two valid events; the crack separates them; revisit only if a real gameplay mix test shows one inaudible), no dynamic fast-cadence volume (variation-specific testing).
- ADR-011 addendum (2026-09-29): BRAND is a FIRST SLICE of the MARK / PERSISTENT TARGET-ATTACHED STATE reference (the Etched Shadow Brand), NOT accepted, awaiting the owner: a small shadow coil cut into the front enemy's body (`MarkRecipe` / `MarkPerformance`, keyed by skill id; the atlas `seeker_brand.py` from a PixelLab pixen silhouette, redrawn at the game's pixel material), placed at an AUTHORED body point per strip frame (`<strip>.mark.json` for 60 idle / attack + 30 death strips, `MarkPoints`, `mark_points.py`, head boxes and the recipe's halo kept clear), quiet at rest, cut deeper by a chisel on ETCH's ticks (design D: the ring's wall into the hollow, then the hook further along the spiral; the spiral kept open), carried and re-cut after a fall, spread down the row by SPRAWL; one information-only Core hook (the field tick's `Marked` after the depth update, fingerprint-proven). Six review rounds; open for the owner: the deepest ETCH steps are small, and the material reads as lit line more than incised shadow (page https://claude.ai/artifact/AddEshDzufkCDuBmyJitpL).
- ADR-011 addendum (2026-09-30): BRAND's SECOND PASS, the ETCHED SHADOW CUT, NOT accepted, awaiting the owner: the same architecture; `seeker_brand_cut.py` redraws the coil as a broken asymmetric scar-spiral (a blind read chose design B), DARK FIRST (a near-black incision, a dark-violet outer wall, one lit rim on the light-facing arc), THREE VISIBLE DEPTHS (`StageFloors` {50, 150, 200}), never a pupil (tested: no dark texel ringed by the bands closed across a mouth under two texels), a transfer with no bridge whose seep waits for the new front's bite and reaction (`TransferSettleMs` 520), a spread from the source (`SpreadStaggerMs` 40); evidence `production/qa/evidence/brand-cut/` (page https://claude.ai/artifact/A4G3gEs7CpXXoGahBEjtZm).
- ADR-011 addendum (2026-10-01): BRAND's FINAL VISUAL CORRECTION, NOT accepted, awaiting the owner: exactly two fixes from 97efe379. DEPTH 3 TORN (its curl's pieces shifted apart, a 'Y' branch healed, no incision scrap under six texels; unprimed reads: 'bruise', 'torn smear', 'irregular damage', no eye / glyph / spiral; DEPTH 1 / 2 byte-identical) and `MarkRecipe.MaxScale` 1, MEASURED on all 24 family cells and the six bosses (`brand_scale_table.py`, `RH_SHOT_BOSS`); evidence `production/qa/evidence/brand-fix/` (page https://claude.ai/artifact/7MGguQQXQfwXE4vRGx5PkS).
- ADR-011 addendum (2026-10-01, later): BRAND CHANGES DIRECTION to a CURSE the enemy suffers (the etched-mark family ended); three concepts behind `RH_BRAND_CONCEPT=A|B|C` (`CursePrototype`, stencil-clipped to the body, dev-only): A Living Shadow Corruption, B Withering Curse, C Shadow Possession (recommended); awaiting the owner's choice, not accepted, no sound (page https://claude.ai/artifact/P7ZBWqXwpiihaMX9cDqYkN).
- ADR-011 addendum (2026-10-01, glow pass): the owner CHOSE Concept A, LIVING SHADOW CORRUPTION (B and C dropped); its glow pass grows the corruption along paths from a smoky stain along the body's long axis (no loops, tested), dark first with a clipped additive violet emission on some stretches, a hot front through new growth only; depth is reach; awaiting the owner, not accepted, no sound, still the prototype pipeline (page https://claude.ai/artifact/DMw4pyqkKtUXQPBo6DvYdJ).
- ADR-011 addendum (2026-10-01, polish): Concept A's three polish problems: light as tapered runs (no beads), a region of three territories ~120 degrees apart (no seam / sash band), depth 1 readable on every host (a pocket seated in the body's mass by the silhouette's thickness, a high-contrast region suited to the host's brightness: lit haze on dark, saturated bruise on pale; whole-body cast); screen + multiply blends; awaiting the owner's visual acceptance, no sound, prototype pipeline (page https://claude.ai/artifact/AEfLGCYB5anLk7WujnvK5n).

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: lead-programmer (architecture, ADR validation, cross-cutting code review — no dedicated MonoGame specialist agent exists in this template)
- **Language/Code Specialist**: gameplay-programmer (feature/gameplay code), engine-programmer (engine-level, performance-critical systems)
- **Shader Specialist**: technical-artist (HLSL/.fx shaders, VFX, rendering optimization — no dedicated shader specialist for MonoGame)
- **UI Specialist**: ui-programmer (MonoGame ships no UI framework — all menus/HUD/widgets are custom-built)
- **Additional Specialists**: tools-programmer (MGCB content pipeline extensions, custom importers/processors)
- **Routing Notes**: MonoGame has no dedicated engine-specialist agent in this template. Route architecture and cross-cutting decisions to `lead-programmer` (or `technical-director` for the highest-stakes technical calls). Route general gameplay/feature code to `gameplay-programmer`, and engine-level or performance-critical code to `engine-programmer`. Route shader/VFX work to `technical-artist`. Route all UI implementation to `ui-programmer`, since MonoGame provides no built-in UI system. Route content-pipeline tooling (custom MGCB importers/processors) to `tools-programmer`.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs — gameplay/feature code) | gameplay-programmer |
| Game code (.cs — engine-level/performance-critical) | engine-programmer |
| Shader / material files (.fx, HLSL) | technical-artist |
| UI / screen files (custom UI framework code) | ui-programmer |
| Content pipeline files (.mgcb, custom Content Importers/Processors) | tools-programmer |
| Project/build files (.csproj, .sln) | lead-programmer |
| General architecture review | lead-programmer |
