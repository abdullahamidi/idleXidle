# UI/UX V2 — the owner's brief (received 2026-09-01, verbatim)

> Persisted here because it is the specification for a multi-checkpoint refactor and must survive
> context compaction. The runtime truth it is applied against is `main` at `210db97` (the completed
> 2026-08-31 architectural refactor — see `production/audit/refactor-2026-08-31/REPORT.md`).

---

This task is a comprehensive **UI/UX V2 refactor** based on the current live game screens after the recent gameplay architecture refactor.

This is NOT a request to redesign the game's visual identity from scratch.

The existing art direction is good and should be preserved:

* dark fantasy presentation
* near-black surfaces
* Hearth Gold earned/active emphasis
* Source colors
* ornate frames for important surfaces
* pixel-art / illustrated world backgrounds
* current Hunter art
* current icon families
* current panel assets
* current typography infrastructure
* current `UiKit`
* current deterministic screenshot/fixture workflow

The problem is primarily:

* information hierarchy
* readability
* excessive decorative framing
* screen density
* stale terminology
* layout responsibility
* contextual relevance
* inconsistent emphasis

The goal is:

> Make IDLExIDLE feel like a deliberate commercial game UI rather than a collection of individually designed screens.

---

# 0. FIRST: AUDIT BEFORE IMPLEMENTING

Before changing code:

1. inspect the current `main` branch,
2. inspect every player-facing screen,
3. inspect current screenshots / fixture captures if present,
4. inspect the current UI standard,
5. inspect `UiKit`,
6. inspect `UiTypography`,
7. inspect navigation,
8. inspect onboarding/tours,
9. inspect all current screen classes,
10. inspect recent gameplay refactor changes.

Do not blindly implement this prompt against stale assumptions.

The gameplay architecture changed recently.

UI must represent the CURRENT runtime truth.

Specifically audit old UI references to obsolete concepts such as:

* Form
* Woven Ability
* Source × Form composition
* old Aptitude terminology
* old Build/Mastery screen assumptions

Do not reintroduce retired gameplay architecture through UI.

---

# 1. CORE UX PRINCIPLE

The main IDLExIDLE interaction loop should feel like:

# OBSERVE → UNDERSTAND WHY → CHANGE ONE THING → RETURN TO HUNT

The player should spend most of their time either:

* observing combat,
* understanding why progress stopped,
* making a meaningful build/progression decision.

The UI must NOT turn the game into eleven independent management dashboards.

Do not require unnecessary clicks.

Do not make the player repeatedly navigate through confirmation screens to perform low-risk reversible actions.

---

# 2. DO NOT REDESIGN THE WHOLE VISUAL LANGUAGE

Preserve:

* existing world backgrounds
* current gold/black fantasy identity
* current Hunter art
* existing Source colors
* existing ornament assets
* current typography family
* current navigation rail unless concrete usability evidence suggests otherwise

This task is primarily a hierarchy/layout/UX pass.

The game currently already has good foundations on screens such as:

* MAP
* GEAR
* ROSTER
* TRAITS
* EXPEDITION LOG

Improve them rather than replacing them.

---

# 3. GLOBAL P0 — READABILITY / TYPOGRAPHY

This is the most important global issue.

The game is authored against a 1920×1080 layout but is frequently displayed around 1280×720.

Current typography values become physically too small after downscaling.

The current ladder is approximately:

* ScreenTitle 36
* PrimaryValue 30
* PanelTitle 26
* Headline 24
* NavigationLabel 21
* Body 19
* Secondary 16
* Caption 14

At 720p these become visually much smaller than intended.

Do NOT solve this by shrinking content further.

Implement a proper UI readability strategy.

---

# 4. UI SCALE

Add a user-facing UI Scale setting if the rendering architecture supports it cleanly.

Suggested options:

* 100%
* 125%
* 150%

Potentially:

* Auto

where Auto chooses a suitable value based on output resolution.

Do not distort pixel art unnecessarily.

UI Scale should affect UI layout/text, not the game-world camera unless intentionally required.

---

# 5. TYPOGRAPHY TARGET

Evaluate actual physical readability at:

* 1280×720
* 1920×1080
* 2560×1440

Body text must remain comfortably readable at 720p.

As a starting point, evaluate an effective 720p scale comparable to:

* ScreenTitle: ~38–40
* PrimaryValue: ~32
* PanelTitle: ~28
* Headline: ~26
* NavigationLabel: ~24
* Body: ~24–26
* Secondary: ~20–21
* Caption: ~17–18

Do NOT blindly use these exact values.

Use screenshots and actual text measurement.

The requirement is readable physical output, not specific constants.

---

# 6. TEXT ROLE RULES

Use these semantics consistently:

## Screen title — Only the name of the screen.
## Panel title — The identity of a major panel.
## Headline — The most important object/value inside a panel.
## Body — Readable prose and normal rows.
## Secondary — Supporting metadata and category labels.
## Caption — Tags/badges only. Caption must NOT carry sentences.

Do not reduce text size simply to force content into a bad layout. Fix the layout instead.

---

# 7. SECONDARY TEXT MUST NOT LOOK DISABLED

Current UI sometimes makes secondary information visually similar to disabled information.

Establish clear hierarchy:

## Primary — Bone / bright readable text.
## Secondary — Readable muted color.
## Disabled — Actual dim state.

Players must never confuse "less important" with "unavailable".

---

# 8. INFORMATION MUST NOT RELY ON COLOR ALONE

Source, Style, rarity and states should use combinations of: color, glyph, text, shape, icon.

Do not rely solely on color. Example: NATURE should be communicated through the Nature color, the Nature glyph, and a `NATURE` label where necessary — not only green.

---

# 9. GLOBAL P0 — DECORATIVE FRAME REDUCTION

The game currently uses ornate gold framing on too many surfaces. This flattens visual hierarchy because everything looks equally important.

Preserve ornate frames, but use them intentionally. Use the existing conceptual hierarchy:

# PRIMARY — Important focal interactive surface. Ornate framing allowed.
# SECONDARY — Supporting panel. Use simpler bronze/thin frame.
# QUIET — Lists, grids, metadata, filters, stats, resource rows. Prefer: dark translucent surface, subtle border, thin divider, hover highlight — instead of full ornate framing.

---

# 10. GOLD IS MEANING

Continue the design law: **Hearth Gold indicates earned, active, selected or important state.**

Do not use gold merely because a border exists. Gold should answer: selected? earned? available? important? primary action? If not, use quieter styling.

---

# 11. GLOBAL P0 — ONBOARDING BANNER

The persistent bottom banner `THE BUILD IS THE GAME` currently appears on many unrelated screens. This consumes significant vertical space and repeatedly competes with screen content.

Change onboarding behavior. The existing banner may appear when relevant during initial onboarding. Once dismissed or learned: DO NOT show the full banner persistently across every screen.

Use: first-time spotlight tour, `NEW` marker, contextual toast, optional Help, contextual small hint — instead.

---

# 12. CONTEXTUAL TEACHING

Tutorial messaging should describe the current screen.

MAP: `A NEW REGION IS AVAILABLE` · TRAITS: `YOU HAVE 1 TRAIT POINT` · ROSTER: `A NEW HUNTER HAS JOINED` · BUILD: `A NEW SKILL SLOT IS AVAILABLE`

Do not show BUILD teaching over MAP, GEAR, WARREN, etc.

---

# 13. KEEP THE CURRENT NAVIGATION RAIL FOR NOW

Do NOT automatically replace the existing left rail. After visual review, the current navigation is reasonably readable because it has: stable position, icons, labels, selected frame, locked dim state, notification badges.

The number of entries is not currently the highest priority UX issue. Improve content/readability first. Only group or reduce navigation if actual usability testing shows that players cannot locate screens.

---

# 14. SHARED INSPECTOR PATTERN

The game already organically uses a successful pattern on MAP, ROSTER, TRAITS, GEAR. Formalize it.

Many screens should use: MAIN CONTENT + RIGHT-SIDE INSPECTOR. The inspector should use a predictable structure:

```text
CATEGORY / TYPE
NAME
IDENTITY / SHORT DESCRIPTION
WHAT IT DOES ...
REQUIREMENTS ...
COST / CURRENT STATE ...
[PRIMARY ACTION]
```

Use this pattern for: Map region, Character, Trait, Mastery node, Skill, Gear item, Warren facility. The player should learn one interaction model and reuse it across systems.

---

# 15. INTERACTION GRAMMAR

Hover — quick glance / highlight / short tooltip. Click — select. Inspector — full explanation. Primary button — commit/apply/equip/learn/change.

Avoid huge hover tooltips containing full documentation. Avoid different selection grammar on every screen.

---

# 16. HUNT — PURPOSE

HUNT is the most important screen. It is the heart of the game. Its purpose is: **Watch the build fight and understand its current state.** Combat should remain visually dominant. HUD must support combat, not surround it with management dashboards.

---

# 17. HUNT — REDUCE LEFT SKILL PANEL

The current vertical SKILLS panel occupies a large amount of combat space, especially early when few skills are equipped. Replace or strongly reconsider it.

Preferred direction: a horizontal skill strip near the bottom of the combat viewport.

```text
ACTIVE
[BLOW · BODY · 2.8s] [CALL · MIND · READY]

PASSIVE
[MIRE · ACTIVE] [WILT · 3 STACKS]
```

This naturally supports the intended 2 Active / 2 Passive layout. The combat scene gains more horizontal breathing room.

---

# 18. HUNT — SKILL SLOT INFORMATION

Each skill slot should communicate at a glance: unique skill glyph, skill name, Source variation, readiness/cadence, active status where relevant. Optional secondary: stacks, persistent state, proc readiness.

Do not expose full mechanical text during combat. That belongs in BUILD.

---

# 19. HUNT — COMPACT HUNTER CARD

Keep the combat-critical information. Preferred hierarchy:

```text
SEEKER · Lv 12               POWER 222
████████████░ 107 / 180
[important status icons]
```

Detailed values such as exact tempo breakdown belong in Stats/Training/Build. Reduce the Hunter panel if needed to increase combat visibility.

---

# 20. HUNT — RIGHT-SIDE UTILITY

Current HUNT shows Idle Rate, Reward Activity, Chest Filter. Keep the right side lightweight:

```text
IDLE
+63/min

REWARDS
2 CHESTS READY

[OPEN VAULT]
```

Move detailed Chest filtering to the Vault. Chest filtering is inventory/economy management, not combat-state information.

---

# 21. HUNT — DAMAGE FEEDBACK

Do not let rapid-hit builds flood the screen. For multi-hit attacks, consider aggregate feedback (`127 × 6`) when this does not obscure meaningful crit/mechanic information. Important semantic effects should visually outrank ordinary damage numbers. Priority: 1 major gameplay state, 2 skill identity, 3 damage feedback, 4 cosmetic effects.

---

# 22. HUNT — DEFEAT / FAILURE DIAGNOSTICS

Do NOT simply say `DEFEATED — WAVE 47`. Add useful diagnosis using real simulation data only:

```text
DEFEATED — WAVE 47
MAIN PRESSURE — Your build could not sustain the incoming damage.
LIMITING FACTOR — REACH — You reached 1 of 3 enemies per cast.
BEST PERFORMER — BLOW — 43% of your damage
[ADJUST BUILD] [GEAR] [RETRY]
```

Do not recommend fake solutions. Diagnostics must be backed by real telemetry.

---

# 23. EXPEDITION LOG — KEEP ITS CORE STRUCTURE

Preserve: clear failure headline, run metrics, comparison with previous run, history navigation. Especially preserve `SINCE YOUR LAST RUN HERE` as a core concept.

---

# 24. EXPEDITION LOG — EXPLAIN MEANING

Avoid presenting only technical numbers (`1.0 of 3.0 creatures per cast`) without interpretation. Add a short diagnosis layer:

```text
MAIN LIMIT — REACH
Your attacks are reaching only 1 of the 3 enemies in this wave.
```

Then show the numbers below. Numbers explain magnitude; the diagnosis explains meaning.

---

# 25. OFFLINE / WELCOME BACK EXPERIENCE

Returning should feel rewarding. Do not reduce offline earnings to a tiny transient line. Use a compact return summary:

```text
WELCOME BACK
AWAY 6h 42m
HUNT +11,320 Gleam
WARREN +7,100 Gleam · +1,240 Dust
NOTABLE — BLOW reached Level 14 · BODY set reached 5 pieces · Rare Weapon found
37 ordinary items auto-salvaged
[CONTINUE]
```

Prioritize: major progression, skill levels, set completion, rare/new items, quest completion, milestone unlock. Do not spam every ordinary reward.

---

# 26. MAP — PRESERVE CURRENT FOUNDATION

Preserve: central world map, route layout, selected region highlight, right-side region inspector, large visual identity. Do NOT replace it with a list.

# 27. MAP — LOCKED REGIONS

A locked region should still be discoverable: `CINDERWORKS / MACHINE / LOCKED / Conquer Verdant Hollow`. Do not make locked content indistinguishable from background decoration.

# 28. MAP — USE EMPTY SPACE

The region inspector can be visually underfilled. Increase readability and hierarchy (YOUR POWER, RECOMMENDED + difficulty word, ENEMY THEME, REGION MODIFIER, GOAL, DROPS, [RESUME HERE]). Use larger text; do not pack information into tiny labels while half the panel remains empty.

# 29. MAP — CTA STATES

Selected current region: `RESUME HERE`. Selected available region: `HUNT HERE`. Locked region: no disabled mystery button — show the requirement: `CONQUER VERDANT HOLLOW FIRST`.

---

# 30. ROSTER — PRESERVE 2×5 STRUCTURE

Keep the 10-character grid + right-side inspector. Do not redesign into a carousel or giant character sheet.

# 31. ROSTER — SIMPLIFY CHARACTER CARDS

Cards should primarily communicate: class, portrait, name, current status or unlock progress; optionally a short passive identity label. Full information belongs in the inspector.

# 32. ROSTER — LOCKED CHARACTER PROGRESS

Locked cards communicate how to unlock (progress bar, `80 / 100`). Show the quest's understandable player-facing requirement. Do not expose internal counters or IDs.

# 33. ROSTER — CHARACTER INSPECTOR

Show: THE SEEKER / WANDERER / flavor / STARTING SKILL — BLOW / INNATE — EVEN HAND + one line / GEAR CLASS / [SET ACTIVE]. Starting Skill is an important part of identity — show it.

# 34. CHARACTER SWITCHING

Switching is free: click `SET ACTIVE` → switch, short feedback, no confirmation.

---

# 35. SETTINGS — INCREASE CONTROL SIZE

Use the available space: bigger dropdown labels, toggle labels, sliders, value labels, button text.

# 36. SETTINGS — GROUP BY PURPOSE

DISPLAY (Mode, Resolution, UI Scale) · AUDIO (Music, Effects) · GAMEPLAY (Ask before sell/salvage, default combat speed where applicable) · ACCESSIBILITY (Damage numbers, Skill names/callouts, Hit effects, Screen flash, Screen shake, Reduced motion, high-contrast combat states if feasible) · CONTROLS (key bindings/help where supported). Do not add fake unsupported settings.

# 37. SETTINGS — DESTRUCTIVE ACTIONS

`START A NEW GAME` must not sit alongside harmless utilities. Create a clear DANGER ZONE with explanation and confirmation.

---

# 38. STATS SCREEN SEMANTIC PROBLEM

The screen called `STATS` is mostly a stat-purchasing system; its primary purpose is TRAINING. Audit the runtime responsibility. Option A: rename to `TRAINING`. Option B: `STATS | TRAINING` tabs if both are substantial. Do not call a purchase screen `STATS` merely because it affects stats.

# 39. TRAINING — GROUP STATS

Group by intent using the ACTUAL current stat model (e.g. OFFENSE Might/Resonance/Critical/Focus · SURVIVAL Vitality/Health/Defense · TEMPO · REWARDS Guile). Do not invent categories for stats that do not exist.

# 40. TRAINING — SHOW RESULT BEFORE PURCHASE

Each row answers "what changes if I spend this?": `MIGHT 0/60 — Basic attack damage 99 → 103 — [TRAIN · 25 GLEAM]`.

# 41. TRAINING — RESET

If reset is free/cheap/reversible do not over-confirm; if it has permanent cost explain it. UI must match consequence.

---

# 42. TRAITS — PRESERVE DISTINCT ART DIRECTION

Keep the starfield background and permanent-progression atmosphere. Traits are permanent account identity.

# 43. TRAITS — DEFAULT ZOOM

Do not require reading the entire tree at minimum zoom. First-open emphasizes SPINE, road starts, nearby choices; zoom outward for macro view.

# 44. TRAITS — ROAD FOCUS

Consider click/double-click/keyboard road focus that frames a road automatically.

# 45. TRAITS — INSPECTOR STANDARD

WHAT IT DOES / WHAT IT COSTS / YOU NEED FIRST is good — formalize and reuse on Mastery. Keep `Permanent. Never resets.` clearly visible.

# 46. TRAITS — PERMANENT CAPSTONE CONFIRMATION

Minor permanent nodes: no modal spam. Major identity/capstone choices: stronger confirmation (`THIS IS A PERMANENT IDENTITY CHOICE. [COMMIT TO ARTIFICE] [CANCEL]`).

---

# 47. VAULT — HIGH PRIORITY REDESIGN

Very large panel, very small chest cards, large empty areas. Redesign the content scale.

# 48. VAULT — CHEST CARDS

Bigger cards: chest art, quantity, tier, rarity, important drop constraints, primary OPEN. Fewer larger meaningful cards.

# 49. VAULT — TOOLBAR HIERARCHY

`OPEN ALL` primary · `TRADER` secondary · `PASTE CODE` tertiary utility (overflow if appropriate).

# 50. VAULT — EMPTY STATE

`THE VAULT IS EMPTY — Chests are earned while hunting and through progression. [RETURN TO HUNT]`. Only real acquisition sources.

# 51. VAULT — OPEN ALL

Communicate auto-salvage/filter consequences when relevant (`32 low-tier items may be automatically salvaged.`). No confirmation every time unless protected items could be destroyed.

# 52. VAULT — REWARD PRIORITY

For mass rewards prioritize: set completed, new mechanic/unlock, major upgrade, useful rare item, ordinary loot summary.

---

# 53. FORGE — HIGH PRIORITY DENSITY PASS

Four major columns (Bag, Selected Item, Operation, Materials) cramp the important interaction.

# 54. FORGE — MOVE TO THREE PRIMARY COLUMNS

INVENTORY | ITEM | FORGE (operation, before → after, [APPLY]); MATERIALS as a compact shared strip.

# 55. FORGE — REMOVE PERMANENT MANUAL TEXT

`YOUR CHARTS` explanatory copy belongs in first-use tour / Help / tooltip, not a permanent panel.

# 56. FORGE — BEFORE → AFTER

The most important Forge interaction — make it prominent (ITEM LEVEL 1 → 2, POWER 12 → 13, DAMAGE 43 → 47, COST, [UPGRADE]).

# 57. FORGE — OPERATION TABS

Keep Upgrade / Re-roll / Socket / Salvage if live; each tab answers: what will happen, what changes, cost, what is uncertain, can it destroy something.

# 58. FORGE — RNG HONESTY

Show affected affix, locked affixes, potential range, whether result can be worse, consumed materials — before the click.

# 59. FORGE — ITEM PROTECTION

Lock/favorite items if supported; salvage respects protection.

---

# 60. GEAR — PRESERVE CURRENT FOUNDATION

Keep Hunter/paper-doll, equipment slots, inventory grid, item detail, set information.

# 61. GEAR — REDUCE LOADOUT COLUMN

Move character/power/innate into the Equipped/Hunter header; more space for Inventory and Item Detail (~42% / ~27% / ~31% as a starting point, layout-tested).

# 62. GEAR — KEEP HUNTER ART

Preserve the large Hunter art.

# 63. GEAR — QUIET EMPTY INVENTORY CELLS

Empty cells quiet; rarity/Source/selected make items emerge.

# 64. GEAR — ITEM DETAIL

Set readability as a ladder: `NATURE SET 1 / 5 — ● 2 … ○ 3 … ○ 4 … ○ 5 …` with clear active/inactive states.

# 65. GEAR — "EQUIP BEST"

Audit what it means; if it only compares numerical power, rename honestly (`EQUIP HIGHEST POWER`). Never imply intelligence the system does not have.

---

# 66. BUILD — REMOVE ALL LEGACY FORM PRESENTATION

Remove or migrate `EVERY SKILL IS A SOURCE AND A FORM`, `FORMS`, `far Forms`, Form diagrams, Source/Form composer language. Current model: STYLE → SKILL → VARIATION → REINFORCEMENTS. UI must teach it.

# 67. BUILD — SEMANTIC SCREEN ROLE

Rename stale presentation classes (`WeaveScreen`, `BuildScreen`, `FormHexDiagram`) toward `LoadoutScreen`, `MasteryScreen`, `StyleHexDiagram` only if it truly represents Style — otherwise delete Form-only presentation.

# 68. BUILD / LOADOUT STRUCTURE

Master-detail: YOUR LOADOUT (ACTIVE / PASSIVE) | SKILLS / SKILL TREE (by style) | DETAILS (skill, Source, level, Vow validity). Do not force exact geometry.

# 69. BUILD — SKILL CARD

Unique glyph, name, Style, level, selected Source variation, reinforcement progress. No generic Form icon.

# 70. SKILL TREE VISUAL MODEL

The variation fork must teach: Source belongs to the variation (BLOW → FLATTEN·BODY / RUPTURE·MACHINE → reinforcements). The player should never wonder "Do I equip Source separately?"

# 71. BUILD — VOW AS VALIDATOR

Show requirement, current validity, what breaks it, reward (`THIRD OATH — ACTIVE ✓ — DEMAND — CURRENT BUILD — REWARD`; invalid: `BROKEN ✕ — Your build contains: BODY, MIND`).

---

# 72. MASTERY — SEMANTIC CLEANUP

Remove old Form assumptions. Mastery is about specialization, branches, Style philosophy, skill discovery, tradeoffs — not a Form discipline screen.

# 73. MASTERY — TREE CANVAS

Large pan/zoom canvas, compact points/status area (`18 SPENT · 6 AVAILABLE`), right-side node inspector with WHAT IT DOES / COST / [TAKE].

# 74. MASTERY — NODE VISUAL TYPES

Different node kinds look different (minor connector, passive, notable, skill discovery, Style specialization, bridge, capstone). Macro shape readable zoomed out; detail readable zoomed in.

# 75. MASTERY — SKILL DISCOVERY

Discovery is permanent: show `DISCOVERED`, still visible after respec. Use actual runtime rules.

# 76. MASTERY — PATH PREVIEW

If pathing is deterministic, show `This path costs 4 points.` and highlight the route; skip if the model does not support it cleanly.

---

# 77. WARREN — PURPOSE

IDLE SUPPORT · SERVICES · AUTOMATION · TARGETING. Communicate what each facility DOES; production rate is secondary.

# 78. WARREN — FACILITY CARD

Avoid cards that are only Level / Currency-per-min / Upgrade. Prefer: name, level, SERVICE, OUTPUT, NEXT MILESTONE, [MANAGE].

# 79. WARREN — SHARED SUMMARY

One summary region (Warren level, offline coverage, facility cap, champion depth, total rates). Do not repeat the cap explanation on all eight facilities.

# 80. WARREN — CAPPED FACILITY

Show the requirement: `REACH DEPTH 70 TO ADVANCE`.

# 81. WARREN — SET AND FORGET

No frequent collect-click minigame. Choose target, upgrade, unlock milestone, configure, leave.

---

# 82. EMPTY STATES

Every major screen has purposeful empty states (Vault, Inventory, no Traits available, no Mastery points, no Warren targeting unlocked). No giant blank panels.

# 83. LOCKED STATES

Locked content answers "why?" with concrete requirements (`REACH WAVE 8`, `CONQUER VERDANT HOLLOW`, `LEARN BLOW`, `REQUIRES 1 TRAIT POINT`). Never only `LOCKED` when the requirement is known.

# 84. PRIMARY ACTION

Every screen has one clear primary decision (HUNT observe · MAP HUNT HERE · ROSTER SET ACTIVE · TRAITS LEARN · BUILD EQUIP/CHANGE · MASTERY TAKE · VAULT OPEN · FORGE APPLY · GEAR EQUIP · WARREN UPGRADE/MANAGE). Not five equal buttons.

# 85. ACTION SEVERITY

Safe/reversible (equip, switch character, change skill, free respec): minimal confirmation. Costly (forge, purchase, upgrade): show cost/result first. Permanent/destructive (trait identity, delete save, salvage protected item): confirmation. Do not confirm everything; do not confirm nothing.

# 86. FEEDBACK AFTER ACTION

Immediate feedback for every meaningful action (state animation, highlight, short toast, value transition). Avoid blocking modals.

# 87. ANIMATION / TRANSITION RULE

Subtle transitions that explain change (inspector crossfade, node unlock pulse, equip movement, milestone glow, chest opening, resource tick). Do not animate inactivity. Respect Reduced Motion.

---

# 88. CURRENT UX STANDARD IS STALE

Audit `assets/art/idlexidle_ux_screen_guide_standard.md` (Build using Source + Form, Form glyph fallback, stale navigation, old responsibilities, old Dust terminology, omitted screens). Replace/update into **IDLExIDLE UX SCREEN GUIDE STANDARD V2** reflecting current architecture.

# 89. UX STANDARD V2 MUST INCLUDE

For every screen: purpose; primary player question; primary action; layout zones; inspector behavior; typography roles; primary/secondary/quiet hierarchy; empty state; locked state; selected state; disabled state; overflow behavior; keyboard interaction; mouse interaction; onboarding behavior; accessibility; deterministic fixture; visual acceptance criteria.

---

# 90. SCREEN CLASS RESPONSIBILITY

Do not build a giant UI framework, but extract repeated meaningful components with multiple real users (`InspectorPanel`, `ResourceStrip`, `RequirementView`, `SetBonusLadder`, `ItemGrid`, `NodeInspector`, `ScreenHeader`, `NotificationToast`, `EmptyStateView`).

# 91. DO NOT CREATE A GENERIC WIDGET ENGINE

No JSON UI, layout DSL, reflection UI, giant Screen framework, or web-style component system. Small reusable typed components; screens stay easy to understand.

# 92. DATA HONESTY

Every displayed value comes from real game data. Never invent estimated DPS, cause of death, best skill, expected forge result, offline rate, set contribution, quest progress, recommended region — unless the model genuinely computes it. Omit, fall back honestly, or add telemetry only with real design value.

# 93. PERFORMANCE

No per-frame allocations, no Core↔MonoGame coupling, simulation never depends on UI, offline combat never requires presentation, fast-forward never blocked.

# 94. RESOLUTION TEST MATRIX

Test every major screen at 1280×720 and 1920×1080 (2560×1440 where feasible): readability, clipping, density, inspector width, tooltip bounds, selected states, modal size, navigation, bottom overlays. 720p is not an afterthought.

# 95. DETERMINISTIC UI FIXTURES

Preserve and expand: HUNT (4 skills, statuses, reward available) · MAP (selected + locked) · ROSTER (active + unlocked + quest-locked) · TRAINING (ranks + currency) · TRAITS (point + blocked node + permanent path) · VAULT (tiers) · FORGE (item with all operation states) · GEAR (set + inventory + comparison) · BUILD (4 skills, variation, reinforcement, valid/invalid Vow) · MASTERY (points + discovery node + notable/capstone) · WARREN (levels + targeting + capped). Real domain data.

# 96. SCREENSHOT REVIEW

After each screen's pass: a 1920×1080 screenshot and at least one 1280×720 screenshot. Do not mark a screen complete on code review alone.

# 97. IMPLEMENTATION PRIORITY

P0 global foundation: 1 typography/UI scale · 2 secondary-vs-disabled contrast · 3 reduce ornate framing · 4 persistent tutorial banner behavior · 5 stale Form terminology cleanup · 6 UX Standard V2.
P1 main loop: 7 HUNT · 8 Expedition Log · 9 Offline Welcome Back.
P1 build loop: 10 BUILD · 11 MASTERY · 12 TRAITS.
P1 loot loop: 13 GEAR · 14 VAULT · 15 FORGE.
P2 progression: 16 MAP · 17 ROSTER · 18 TRAINING/STATS semantic cleanup · 19 WARREN.
P3 polish: 20 Settings/accessibility · 21 transitions · 22 contextual Help · 23 final responsive pass.

# 98. DO NOT BIG-BANG ALL SCREEN LAYOUTS AT ONCE

Checkpoints: after each group — compile, run tests, run fixtures, capture screenshots, inspect at 720p and 1080p, fix, continue.

# 99. DESIGN LAWS

1 The player's current decision must be visually obvious. 2 Important information is large; secondary is quiet. 3 Gold must mean something. 4 Do not explain the same system on every screen. 5 Do not ask the player to click twice when one safe click is enough. 6 A locked action explains why. 7 A destructive action explains what will be lost. 8 An expensive action shows its result before purchase. 9 A permanent decision looks permanent. 10 A reversible experiment feels safe. 11 Combat remains visually dominant on HUNT. 12 Idle rewards respect the player's time. 13 One shared interaction grammar. 14 Do not display information merely because the model contains it. 15 Every visible element answers a player question.

# 100. SCREEN QUESTIONS

HUNT "How is my build doing right now?" · Expedition Log "Why did I stop progressing?" · MAP "Where should I hunt next?" · BUILD "What skills am I running and how are they configured?" · MASTERY "How am I specializing this build?" · TRAITS "What permanent Hunter identity am I creating?" · ROSTER "Who should I play?" · GEAR "What am I wearing and is this item useful?" · VAULT "What rewards are waiting for me?" · FORGE "What will happen if I modify this item?" · TRAINING "What permanent/basic stat should I improve?" · WARREN "What is my organization doing while I am away?" · SETTINGS "How should the game behave and present itself?"

If a panel does not support the screen's question, reconsider why it exists.

# 101. FINAL DELIVERABLE

Global: UX problems found; typography changes; UI Scale implementation; panel hierarchy changes; onboarding changes; accessibility changes; shared component changes.
Per screen: old layout; new layout; main UX improvement; removed / moved / renamed; new empty/locked states; screenshots; remaining issues.
Legacy cleanup: remaining Form / Weave / WovenAbility / Source×Form UI terminology / stale screen names, each justified.
Validation: 720p + 1080p screenshots; test status; fixture status; UI type checker status; build status.

# 102. MOST IMPORTANT FINAL INSTRUCTION

Do not optimize for fitting as much information on screen as possible, for showing off every UI asset, or for maximum ornament. Optimize for READABILITY · HIERARCHY · DECISION CLARITY · BUILD UNDERSTANDING · FAST IDLE-GAME INTERACTION.

The final game should look simpler than before while communicating more.
