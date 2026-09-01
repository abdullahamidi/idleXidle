# UI POLISH — the brief (2026-09-01)

*The user's instruction, section-numbered as given, condensed to the rules. The report answers by these numbers.*

The UX V2 refactor is done and its information architecture is broadly successful. **Do NOT perform another
UI redesign.** The screens — HUNT · GEAR · TRAINING · BUILD · VAULT · FORGE · WARREN · MAP · TRAITS · ROSTER ·
SETTINGS · EXPEDITION LOG — keep their structure. The goal: FINAL UI POLISH · INPUT CORRECTNESS · RESPONSIVE
UI SCALE · VISUAL FEEDBACK · MOTION · AUDIO FEEDBACK · COMBAT READABILITY · SOURCE/SKILL/SET/SHIELD VISUAL
COHERENCE · PIXELLAB-ASSISTED ASSET COMPLETION. The result should feel responsive, deliberate, readable,
premium, consistent, finished — without adding UI complexity.

## §0 Work against latest main
Inspect main, the UX refactor's result, the skill/set/shield rework, latest fixtures, `UiKit`, typography/layout,
input coordinate conversion, all hit-testing, the UI asset library, presentation/VFX/audio infrastructure, and
PixelLab MCP. UI must reflect current runtime truth.

## §1 Not a redesign
Preserve: HUNT (large viewport, compact hunter header, bottom Active/Passive strip, light right-side idle/reward
panel) · GEAR (paper doll, equipment, inventory, inspector) · TRAINING (grouped rows, before→after, right inspector)
· BUILD (loadout, skill library/tree, inspector) · VAULT (large chest cards, direct Open, toolbar) · FORGE (Bag, Item,
Operation, shared materials strip) · MAP (central map, region inspector) · TRAITS (tree canvas, right inspector) ·
ROSTER (2×5 grid, inspector) · WARREN (summary, facility grid, inspector) · EXPEDITION LOG (failure headline,
diagnostic, metrics, previous-run comparison). Improve feel, hierarchy, responsiveness without destabilising IA.

## §2–§6 Duplicate skills (CRITICAL)
§2 A SkillId may be equipped in at most one loadout slot — a domain invariant, not a UI rule.
§3 Enforce below UI: the authoritative loadout validation rejects duplicates; every path that changes a loadout uses
it (Build UI, save load, migration, presets, share codes, automation, tests).
§4 UI: an equipped skill shows `EQUIPPED · SLOT N`; it can still be selected/inspected/respecced; choosing another
slot while viewing it makes the Equip action explain `ALREADY EQUIPPED IN SLOT N` — never a silent disable.
§5 Migration: keep the first valid occurrence, clear later duplicates, preserve progression and
variation/reinforcement state, never delete learned skills; add migration tests.
§6 Progression counts a skill once per wave for being equipped; assert in tests.

## §7–§11 UI scale is a density profile
§7 Do NOT scale the whole canvas (1.0/1.25/1.5 on every authored coordinate) — large screens cannot fit.
§8 100/125/150 are DENSITY/ACCESSIBILITY profiles. Top-level viewport-anchored layout keeps fitting. Scale:
typography, row height, button height, icon size, hit target, padding, spacing, inspector content, controls.
§9 100% compact · 125% comfortable (fonts/rows/buttons/targets/spacing up, slight reflow) · 150% accessibility
(fewer inventory columns, scrolling, wider inspectors, less secondary info, stacked controls, wrapping). Layouts
need not be geometrically identical at 150%.
§10 ONE central source (`UiMetrics` / `UiLayoutContext`): TextScale, ControlScale, SpacingScale, RowHeight,
ButtonHeight, IconSize, PanelPadding, InspectorWidth, ScrollbarWidth, HitTargetMinimum. No scattered `* 1.25f`.
§11 Top-level rects viewport-driven (Navigation / Header / Main / Inspector / Footer), then subdivided. Don't rewrite
stable screens unnecessarily.

## §12–§16 Input
§12 ONE central float transform: screen mouse → subtract viewport origin → divide by render transform → logical mouse.
Keep it floating point; never round per screen. §13 No early quantisation. §14 Pixel-snap only the final drawn
rect. §15 One authoritative rect per control for draw, hover, click, tooltip anchor, focus. §16 Acceptance: at
100/125/150 on every major screen the visible component and clickable region overlap exactly (corners, edges,
centre, neighbours).

## §17–§21 The previous agent's open items — finish all four
§17 150% vertical pass (bottom clipping, controls off viewport, buttons under footer, unreachable last rows, text
over buttons, inspector/modal overflow) via scroll/reflow, never by shrinking fonts. §18 Scroll only where density
requires (inventory, Forge bag, long inspectors, Traits detail, Build details, Warren, Settings at small/high);
never HUNT; never hide primary actions below a scroll. §19 Resolve: 150% pass · mouse quantisation on eleven screens
· three owed fight fixtures · orphaned `Warren.Name`. §20 `Warren.Name`: delete if no consumer (else move to
migration). §21 Complete the owed fixtures against the latest combat model; deterministic; screenshot-usable.

## §22–§29 Polish principle, ornament, states
§22 Polish adds FEEL, not information. §23 Three intensities — QUIET (lists, empty cells, metadata, filters: dark
surface, subtle border), SELECTED/FOCUSED (thin gold accent, subtle glow, clear focus), IMPORTANT/PRIMARY (ornate
gold: primary action, focal panel, capstone, set completion, permanent decision). §24 Gold = selected/active/earned/
available/important/primary; Source colour = content identity; keep the roles separate. §25 Every control:
NORMAL · HOVER · PRESSED · SELECTED · DISABLED · FOCUSED, standardised. §26 Hover restrained (luminance, thin inner
glow, small accent shift; ~80–120 ms). §27 Pressed: 1–2 px depression, reduced glow, click sound; immediate.
§28 Selected ≠ hover; persistent structure. §29 Disabled stays readable and says why (`NEEDS 1 CRYSTAL`,
`ALREADY EQUIPPED IN SLOT 1`, `CONQUER CINDERWORKS`).

## §30–§38 Motion and feedback
§30 Animate CHANGE, not everything. §31 Durations: fast 80–120 ms (hover/press/select) · transition 150–220 ms
(equip/purchase/unlock/inspector/number) · reward 250–450 ms (chest, capstone, set completion, shield break).
§32 Reduced Motion: drop scale/movement/idle motion/slides; keep immediate state changes, static highlight,
simple fades. §33 Screen switches: quick content crossfade / short settle, 100–150 ms; nav stays. §34 Modals:
quick backdrop fade + short panel fade/scale. §35 Inspector: content fades, frame stays. §36 Number feedback on
player action (`160 → 162`, brief emphasis; Forge stat highlight; Warren pulse; Gear power tick). §37 Spending
reacts at the resource pill (tween/pulse/cost flash), no flying resources. §38 Substantial gain may show `+420`
near the resource, thresholded/batched.

## §39–§44 TRAINING and BUILD
§39 TRAINING: hover/selected row, purchase state, before/after transition, spend feedback, rank progress; on train:
cost paid → stat highlights → progress animates → inspector updates; no modal. §40–§42 BUILD: equip/variation/
reinforcement/level-max states, Source identity; `EQUIPPED · SLOT N` text badge; hover highlight, selected border,
slot indicator, restrained mastery marker at max. §43 Tree: selected branch brightens/reveals; new reinforcement
pulses once. §44 `RESPEC — FREE` stays calm; one click.

## §45–§47 Source and Style tokens
§45 Six Sources (BODY MACHINE MIND NATURE SHADOW SPIRIT): glyph, accent colour, optional motif, consistent name
typography — the same tokens in Build, tree, Gear, set ladder, Map, Vault, VFX, tooltips. §46 Source colour is an
accent (glyph, thin line, node, small glow, set indicator) — never a panel fill. §47 Styles (HAMMER VOLLEY SNARE SIGN
FIELD DRAIN) via glyph/motion identity and small label; must not compete with Source.

## §48–§52 GEAR and sets
§48 Item hover, equip feedback, inventory clarity, set ladder, comparison. §49 Equip: slot pulses, Gear Power updates,
ladder updates, detail reflects Equipped. §50 Empty cells quiet. §51 Ladder `MACHINE SET 4 / 5` with ● / ○ rungs;
active rung gets a one-time Source-accent reveal; the 5-piece capstone is visually more important. §52 First
completion: short one-time `MACHINE SET COMPLETE · PLATING ACTIVE` toast/banner/pulse, never a blocking modal.

## §53–§55 FORGE
Before→after, cost, outcome, action feel. Upgrade: before→after transitions, changed stats highlight, materials
react, brief forge flash; no screen shake. Distinct sounds: upgrade metallic impact · re-roll lighter shuffle ·
socket crystal insertion · salvage dry break.

## §56–§60 VAULT
Opening feedback, rarity reveal, mass-open summary. Open: immediate response, brief animation, reward revealed,
rarity-scaled emphasis, short. Rarity intensity: common small, uncommon/rare stronger, epic/legendary richer, no
gacha fireworks. Open All: aggregate; highlight set completion, legendary, major upgrade; summarise the rest.
`PASTE A CODE`: secondary utility, never above Open / Open All.

## §61–§64 HUNT
No more management UI. Skill bar states: ready · waiting/cooldown · passive active · reaction available · disabled;
no constant flashing. Presentation priority: normal damage < critical < major skill impact < Break/Stun/Execute/
Shield Break/major state. Multi-hit (VOLLEY): aggregate or restrained repeats; keep crit/kill/execute/proc distinct.

## §65–§70 SHIELD
First-class mechanic. HUD: thinner bar above Health, glyph, distinct shape, cold/metallic accent. Persistent VFX
while Shield > 0: thin rim/faint shell, hunter stays visible. Gain: brief build-up. Absorb: small impact at the
barrier; bar movement communicates. Break: short crack/burst/cold flash — noticeable, shorter than Execute/boss.

## §71–§72 EXPEDITION LOG
Metric emphasis, Shield telemetry if live (`SHIELD ABSORBED`; no zero rows for non-shield builds; a SURVIVAL PRESSURE
diagnostic only when real data supports it), previous-run comparison, transitions between entries.

## §73–§82 MAP · ROSTER · TRAITS · WARREN
MAP: card hover, route/selected region, one-time reveal pulse for a newly available region. ROSTER: card hover,
active hunter, newly unlocked, locked progress, free switch (no confirm) with card/inspector update + short highlight;
keep the character art prominent. TRAITS: readability/camera — first open frames the last Road / nearest choice /
useful framing; smooth road focus (jump under Reduced Motion); purchase: node activates, connection lights, short
pulse; terminals stronger. WARREN: hover, affordability, upgrade response (level highlight, output change, cost
reaction, milestone), locked requirements, summary; don't overbuild services.

## §83–§85 SETTINGS
Preserve grouping and Danger Zone. Complete scale profiles, interaction correctness, vertical overflow. Scale change
applies immediately. The Settings modal must remain usable at 150% — Settings → UI Scale is the escape path.

## §86–§87 Audio
Semantic families: navigation tick · generic dry click · equip leather/metal · training confirmation · trait ritual ·
forge metal · chest wood/metal + rarity layer · locked/error dull · shield cold shimmer/crack. Reuse; not 50 sounds.
Understated; test rapid repeats (training, chests, inventory, forge).

## §88–§101 PixelLab
PixelLab MCP is available: never ask the user for art. Order: REUSE → DERIVE → GENERATE. Every asset: purpose →
references → real size → generate → inspect → integrate → in-game screenshot → review at 100% → refine → remove
unused. Match the house style (dark fantasy, near-black, bronze/Hearth Gold, restrained Source accents, current
outline density and scale). Test at actual size; keep pixel integrity; transparent backgrounds. Shield assets
(canonical glyph; persistent barrier if VFX insufficient; absorb/break strip). Audit the six Source glyphs as one
family; regenerate only the weak. Set capstone emblems (MOMENTUM PLATING CERTAINTY OVERGROWTH AFTERIMAGE HARMONY)
only if they materially help; then one coherent family. Don't generate for whitespace. Semantic names, no
`final2.png`; every committed asset has a live consumer.

## §102–§107 Accessibility
Keep Damage Numbers · Skill Names · Fight Effects · Red Flash · Reduced Motion · UI Scale. Test combinations
(Reduced Motion + 150%; Numbers off + Effects on; Skill Names off + Shield; Red Flash off + low health). Shield never
colour/VFX-only: bar + glyph + state text survive Fight Effects OFF. Important states combine colour, shape, glyph,
label. Keyboard/focus geometry survives scale changes. Interactive targets grow with scale; hit = visual.

## §108–§111 Fixtures and matrix
HUNT STANDARD (2 active, 2 passive, no duplicates, several enemies, statuses, numbers) · HUNT SHIELD (shield > 0,
absorption, partial penetration, break-capable) · HUNT MULTIHIT (VOLLEY, readable, crit). Scale fixtures at
100/125/150 for BUILD · GEAR · FORGE · TRAITS · SETTINGS · VAULT. Resolution matrix: 1280×720 (100/125, 150 if
reasonable) · 1600×900 · 1920×1080 at all three. Settings escape test on every combination.

## §112–§121 Acceptance, speed, performance, tests
Inspect screenshots, not just compiles. Iterate generated assets in context. No polish that slows frequent actions.
No per-frame garbage. No runtime PixelLab dependency. No UI → Core coupling (Core exports state and semantic events).
Tests: duplicate rejection, logical mouse transform, fractional scale, authoritative rects, edge clicks, scale
switches, scale layouts (no NaN, controls in viewport/scroll, Settings reachable). Final asset audit.

## §122 Order
P1 correctness (dup invariant, migration, central transform, quantisation, Warren.Name, fixtures) · P2 scale
(UiMetrics, profiles, 150% pass, scrolling, Settings safety) · P3 interaction states + number feedback · P4 screens
· P5 motion · P6 audio · P7 assets · P8 accessibility matrix · P9 validation. §123 Checkpoint commits, not one big
bang. §124 Twenty laws (no redesign; polish = state change; every click immediate; hover ≠ selected; draw = hit;
no early quantisation; 150% = reflow; gold = importance; Source = identity; motion explains change; rare > common;
no filler; one SkillId once; art is reusable; generate missing art; never hand art to the user; art is done only
in-game at size; no purposeless decoration; every asset a consumer; Core never depends on polish).

## §125 Acceptance · §126 Report (56 numbered items) · §127 "The interface feels finished."
