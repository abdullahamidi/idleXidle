# Art Bible: IDLExIDLE

## Document Status
- **Version**: 1.0
- **Last Updated**: 2026-07-14
- **Owned By**: art-director
- **Status**: Complete — all 9 sections authored
- **Review Mode**: `lean` — AD-ART-BIBLE creative-director sign-off skipped (director gates run only at phase transitions; see `production/review-mode.txt`). Validate at `/gate-check`.

> **⚠ PARTIALLY SUPERSEDED 2026-07-29 — the render/art pipeline pivoted to hand-drawn "vector" art.**
> The game now ships **smooth, painterly, front-facing** character/creature art rendered with
> `SamplerState.LinearClamp` (non-integer scaling, premultiplied-at-load) — **not** the flat-fill,
> hard-1px-edge, PointClamp, 480×270 pixel-perfect model this bible describes in §3 / §6.4 / §7 / §8.
> The **structural** guidance still holds — silhouette-first readability, colorblind-safe-by-construction
> (value + shape, not hue alone), and the homebrew **cutout rig** (§8.5). But treat every *flat-fill /
> PointClamp / indexed-palette / integer-scale / "10 colors per creature" / 480×270-canvas* rule as
> **stale**. Authoritative art contract: `design/art/asset-integration-spec.md`; champion canvas is
> **800×1040 @2x, feet y=992**; current character work: `design/art/character-art-spec-batch1.md`.
> See also `ADR-003` (Superseded) and `.claude/docs/technical-preferences.md` (updated).

## The Bible in One Page

**Master rule**: *Read the world, don't just watch it — every mechanically important state renders
as a distinct glyph, never a color swap alone.*

Everything below follows from that. Shape carries category; edge quality carries Source; mass
distribution carries Role; value-steps carry part-seams. **Color is never the only channel for
anything the player must react to** — which is what makes the system colorblind-safe by construction
rather than by retrofit.

| Section | What it locks |
|---|---|
| 1. Visual Identity | The master rule + three principles. Glyphs on the creature, legible in under a second, mastery drawn onto the world |
| 2. Mood & Atmosphere | Eight game states, each with a distinct emotional target and signature visual. **Glyph inversion**: the boss's vulnerability glyph heals into the ward-seal that binds it |
| 3. Shape Language | Unbroken contour + internal seam lines. Glyph grammar = container (category) + mark (value). "The crack never moves, it only closes" |
| 4. Color System | 6 core colors + 6 Source colors. Hearth Gold spent like a resource. Rarity rejects the genre rainbow for a bone→gold ramp + countable rings |
| 5. Character Design | Hunter visible but subordinate. Vow scars (body, permanent) vs. enchantment inlays (gear, removable). Bound creatures visibly *work* |
| 6. Environment | Region identity = dominant Source, not biome. Hand-crafted, not procedural. Automation becomes standing infrastructure |
| 7. UI/HUD | Chrome vs. content split. Part-break renders on the creature, not the HUD. Cycle-and-confirm targeting |
| 8. Asset Standards | Homebrew cutout rig with angle-snapping. 10 colors/creature. ~125–160 MB, not ~700 MB |
| 9. Reference Direction | Six references, each with a binding "what to avoid" |

## Open Items (carry forward)

These are not gaps in the bible — they are decisions it correctly defers to a later stage.

| Item | Owner | When |
|---|---|---|
| **Homebrew cutout rig** needs a formal ADR, and the **Attacker strike arc** should be spiked first — it's the widest motion arc in the game and where pixel-art rotation risk is most visible (8.5) | `technical-director` | `/create-architecture` |
| **Library ADRs** — Myra, FontStashSharp, MonoGame.Extended, MonoGame.Aseprite (8.7) | `technical-director` | `/create-architecture` |
| **Hand-crafted vs. procedural** environment — art-side recommendation is hand-crafted; needs pipeline confirmation (6.8) | `technical-artist`, `producer` | `/create-architecture` |
| **Belt-charm legibility at 10–15% frame height** — cannot be settled on paper. Fallback pre-authorized: mirror pip/ring states larger in the corner cluster (7.5) | Playtesting | `/vertical-slice` |
| **Cycle-and-confirm target-highlight visuals** — needs its own selected-target rendering state, distinct from the vulnerability glyph (7.7) | `ux-designer` | `/ux-design` |
| **Region count** — determines whether multiple regions share a dominant Source (6.3) | `game-designer` | `/map-systems` |
| **Is Resonance one pool or split per-Source?** — affects the Resonance meter's color treatment (7.5) | `systems-designer` | `/design-system` |
| **Aseprite as the authoring tool?** — determines whether MonoGame.Aseprite or a custom MGCB extension handles atlasing (8.7) | Art team | Before first asset |

---

## 1. Visual Identity Statement

### Master Visual Rule

> **Read the world, don't just watch it: every mechanically important state renders as a distinct glyph — never a color swap alone.**

Every corrupted creature, every region, and every choice that matters to survival
or strategy earns its own glyph. IDLExIDLE is a game about *seeing* mastery
before you *feel* it — a player's skill ceiling is defined by how quickly they can
read a glyph, not how fast they can react to one. This rule arbitrates every visual
ambiguity that follows in this bible: if a decision could go toward "palette
shorthand" or toward "a discrete symbolic mark," the glyph wins.

### Supporting Principles

**1. Every creature wears its identity and its opening.**

A corrupted creature carries two always-legible glyph marks rendered directly on its
pixel-art sprite: a Source glyph (its elemental category and functional role) and a
vulnerability glyph (its current breakable weak point). Neither is ever conveyed
through color alone or offloaded to a health bar / UI element separate from the
creature's body.

- *Design test*: When a new mechanical state needs a visual signal (weak point, telegraph, Vow condition, evolution branch), design a glyph for it before reaching for a palette-only differentiation.
- *Serves*: **Pillar 1 — Precision Over Reflexes** (the glyph is the pattern the player is tested on, not a reflex cue); secondarily **Pillar 5 — Creatures Are Systems, Not Trophies** (the Source glyph doubles as the creature's functional-role marker, so identity is never purely cosmetic).

**2. Ornament earns its keep only if it reads in under a second.**

The glyph layer is drawn like stained-glass symbolism laid over pixel sprites — rich,
mythic, worth mastering visually — but every glyph is simplified until it can be
identified mid-combat, at a glance, without pausing the action.

- *Design test*: If a glyph design looks striking but can't be read in under a second mid-combat, simplify it until it can.
- *Serves*: **Pillar 1 — Precision Over Reflexes** (precision play is only fair if the signal it rewards is legible under pressure — an unreadable glyph quietly becomes a reflex/twitch test in disguise).

**3. Mastery is drawn onto the world, not just tracked in a menu.**

A region's ambient glyph-work — ward-stones, sigil-scars, resonance cracks in the
environment art — visibly shifts from chaotic/fractured to bound/steady the moment
its region boss is broken and automation unlocks. The environment itself is the
mastery meter.

- *Design test*: When deciding how to represent a region's mastery or automation-unlocked state, render it as a persistent environmental glyph change — never a status icon, checkbox, or menu-only flag.
- *Serves*: **Pillar 2 — Automation Is Earned, Not Assumed** (the world must visibly prove automation was won through an active kill, not granted by default).

### Deferred Pillar Coverage

Pillars 3 and 4 do not have dedicated visual principles at the identity-statement
level (kept intentionally tight at three). They are addressed downstream:

- **Pillar 3 (Active Is Better, Idle Is Never Worthless)** → an idle-creature "still working" visual signal, specified in Section 5 (Character Design Direction) and/or VFX standards.
- **Pillar 4 (Builds Are Trade-offs, Not Stat Stacks)** → a Vow/enchantment silhouette-change standard on the hunter, specified in Section 5 (Character Design Direction).

---

## 2. Mood & Atmosphere

Section 1 defines how mechanical states *render* as glyphs. This section defines how
each major game state *feels* to stand inside — its emotional target, its light, its
pace — so that mood alone, before a single glyph is read, already tells the player what
kind of moment they're in. Two states are held to a higher bar than the rest: the
Mastery Transition, because it carries the game's core emotional thesis (automation as
reward, not loss), and the Unconquered-vs-Mastered contrast, because it is the one place
in the game where identical geometry must carry two opposite moods.

### Active Hunting / Combat Encounter

- **Primary Emotional Target**: Predatory clarity — the calm confidence of a hunter who has already read the pattern, not the panic of someone reacting blind. Tension is anticipatory, not chaotic.
- **Lighting Character**: Mid-key, flattened ambient base — deliberately less contrasty than the region's establishing shots — so a narrow band of accent light (telegraph glows, weak-point pulses) can spike well above it and read instantly. Light direction stays fixed and neutral; drama is reserved for glyph accents, never the environment.
- **Atmospheric Descriptors**: alert, poised, exposed, taut, crackling
- **Energy Level**: Coiled — stillness punctuated by sharp, readable bursts. Never frenetic screen-noise; frenzy would smuggle in a reflex test the game explicitly rejects.
- **Signature Visual Element**: The telegraph wind-up pulse — a creature's vulnerability glyph brightens on a fixed, readable timing curve (slow build, hard cutoff) before an attack fires, color-keyed to the attack's element. This pulse is the literal metronome of the encounter's coiled energy.
- *Serves*: Pillar 1 (Precision Over Reflexes) — the mood is built to reward reading, not flinching.

### Region Boss Fight

- **Primary Emotional Target**: Ritual confrontation — the sense of standing an exam the region has set for you; bigger and more ceremonial than a standard encounter, but never so loud it stops being legible.
- **Lighting Character**: High contrast, single dominant light source (usually radiating from the boss's own glyph-marks), with the surrounding arena pushed darker and cooler to isolate the fight. Contrast and glow intensity step up with each phase transition — the arena visibly gets louder as the stakes do.
- **Atmospheric Descriptors**: monumental, ceremonial, charged, isolating, relentless
- **Energy Level**: Escalating — begins at standard-encounter tension and builds in discrete steps, each phase transition raising the visual and emotional stakes.
- **Signature Visual Element**: A resonance halo — a glyph-ring scored into the arena floor beneath the boss that cracks further open and brightens with every phase transition, visibly counting down the fight in real time.
- *Serves*: Principle 3 / Pillar 2 — mastery is already being drawn onto the world before the kill lands; the halo is the first frame of the transition sequence below.

### The Mastery Transition (Boss Falls → Region Binds)

This is the single most important beat in the game's visual language and the primary
evidence that automation is a reward, not a demotion. It must never read as a
fade-to-black "task complete" cut.

- **Primary Emotional Target**: Coronation, not shutdown — the feeling of watching a wild, dangerous thing kneel and become yours. Pride and warmth, not the flatness of a completed checklist.
- **Lighting Character**: The sequence opens at the boss fight's harshest contrast, then breaks into an expanding warm light sweep — like dawn crossing a landscape — that visibly travels outward from the fallen boss to the region's edges. Direction is the load-bearing choice here: the light must grow outward and upward, never shrink or contract inward. Contraction reads as loss; expansion reads as growth.
- **Atmospheric Descriptors**: triumphant, dawning, claimed, resonant, settling
- **Energy Level**: Released — a held-breath exhale. Camera and animation hold on the payoff for a beat before handing control back to the player; there is no instant cut to an automation panel.
- **Signature Visual Element**: Glyph inversion — the boss's vulnerability glyph, the exact mark the player exploited to win, heals and inverts into a ward-glyph that becomes the seal binding the creature to its new job. The player never sees a generic "captured" icon; they watch the very glyph they fought become the emblem of ownership.
- *Serves*: Principles 1 & 3 together (glyph identity persists across the transition instead of being replaced) and Pillar 5 (the creature keeps its identity — it is bound to a job, not erased into a trophy). This is the visual proof that the region was earned, directly serving Pillar 2.

### Idle / Automated Region View

- **Primary Emotional Target**: Quiet pride — looking over a settled homestead you built, not a loading screen or a "done" state. The screen must read as alive and working, never dormant.
- **Lighting Character**: Warm, stable, low-contrast ambient light — a held golden-hour or hearth tone with no harsh shadows, the deliberate opposite of combat's alert mid-key flatness.
- **Atmospheric Descriptors**: settled, industrious, unhurried, wholesome, warm
- **Energy Level**: Ambient-contemplative — steady, looping, never static. Lingering attention should be rewarded with small satisfying motion, not punished with stillness.
- **Signature Visual Element**: The work-loop glyph pulse — each bound creature's ward-glyph flares softly on its own production tick (a crafter flickers on cycle completion, a producer glows on yield), giving the region a visible heartbeat instead of a frozen diorama.
- *Serves*: Pillar 3 (Active Is Better, Idle Is Never Worthless) — idle must visibly produce, not just imply production off-screen. This is the mood-level seed for the fuller creature-animation spec owed in Section 5.

### The Forge

- **Primary Emotional Target**: Alchemical focus — a tinkerer's intimacy with material and consequence; curiosity and deliberate craft, not combat urgency. This is where trade-offs are made, and the mood should slow the player down to feel the weight of the decision.
- **Lighting Character**: A single warm, localized key light (forge-fire, hearth-glow, or crystal-light) directly on the crafting surface, with the periphery falling into a cozy, unlit dark — workshop lighting, not epic lighting.
- **Atmospheric Descriptors**: intimate, tactile, molten, deliberate, hushed
- **Energy Level**: Contemplative — time feels suspended; no timers, no urgency cues.
- **Signature Visual Element**: The Vow scar — when a Vow (restriction) binds to a piece of gear, a glyph mark visibly sears into the item's silhouette in real time as the player commits, and the item's material cools from molten orange to a fixed inlaid color. The trade-off is forged, not logged in a stat line.
- *Serves*: Pillar 4 (Builds Are Trade-offs, Not Stat Stacks) — the restriction is a permanent, visible mark on the object itself, matching the master rule that mechanically important states are never color-only or menu-only.

### Creature Management

- **Primary Emotional Target**: Stewardship — the calm attentiveness of a keeper reading a ledger of living things, not a spreadsheet of stats.
- **Lighting Character**: Even, cool-neutral, gallery-style light — consistent and non-dramatic, so glyph work stays the visual focus rather than mood lighting.
- **Atmospheric Descriptors**: orderly, attentive, cataloged, purposeful, alive
- **Energy Level**: Measured-administrative — active planning energy, unhurried but not sleepy.
- **Signature Visual Element**: The role-glyph roster — every creature entry shows its Source glyph plus a role-badge (attacker / defender / support / crafter / producer) rendered in the same stained-glass glyph language used in combat, so the screen reads as a glyph ledger, never a flat stat table.
- *Serves*: Principle 1 and Pillar 5 — a creature's functional identity stays symbolically consistent whether you're fighting it, farming it, or assigning it.

### Unconquered Region

- **Primary Emotional Target**: Beautiful danger — the region should be seductive as much as threatening, pulling the player toward exploration despite visible risk, not just warning them off.
- **Lighting Character**: Cool, desaturated ambient light with no single reliable source — flickering, directionless, punctuated by sickly corruption-color bleed through cracks in the environment.
- **Atmospheric Descriptors**: feral, fractured, unstable, dissonant, seductive
- **Energy Level**: Restless — the environment has a low idle hostility: drifting corruption particles, flickering broken glyphs, unstable ambient motion.
- **Signature Visual Element**: Sigil-scars rendered as jagged, broken glyph fragments embedded in ward-stones and terrain, flickering and incomplete — visibly unbound.

### Mastered Region

- **Primary Emotional Target**: Earned peace — the same ground now reads as home territory, warm and settled, but visibly yours-through-effort rather than a generic "safe zone" reskin.
- **Lighting Character**: Warm, stable, higher-key ambient light with a consistent shadow direction — the visual equivalent of a sun having risen and stayed.
- **Atmospheric Descriptors**: settled, luminous, steadfast, orderly, warm
- **Energy Level**: Calm-productive — echoes the Idle Region View's ambient-contemplative energy, now grounded in a specific place.
- **Signature Visual Element**: The same sigil-scars, now whole and glowing warm gold, with visible mend-lines in the glyph art where the fracture used to run — the scar of the old wound stays legible under the repair.

### Contrast Mechanism (Unconquered ↔ Mastered)

The Unconquered and Mastered states must share **identical glyph geometry and identical
scar placement** — only the glyph's integrity, color temperature, and light behavior
change. If mastery reused a different symbol set instead of healing the original one,
the causal link between "the fight you won" and "the change to the world" would be lost,
and Principle 3 (Section 1) would collapse into set-dressing.

### Cross-State Distinctness Check

| State | Color Temp | Contrast | Energy | Light Behavior |
|---|---|---|---|---|
| Active Hunting | Neutral, flattened | Low ambient / high accent-spike | Coiled | Fixed direction, accent-only drama |
| Region Boss Fight | Cool ambient, hot accents | High, escalating | Escalating | Single dominant source, intensifies by phase |
| Mastery Transition | Cool → warm sweep | High → resolving | Released | Expands outward/upward |
| Idle Region View | Warm, golden-hour | Low | Ambient-contemplative | Stable, looping pulses |
| The Forge | Warm, localized | High local / dark periphery | Contemplative | Single fixed key light |
| Creature Management | Cool-neutral | Even | Measured-administrative | Flat gallery light |
| Unconquered Region | Cool, desaturated | Unstable, flickering | Restless | Directionless, flickering |
| Mastered Region | Warm, high-key | Stable | Calm-productive | Consistent single direction |

---

## 3. Shape Language

Section 1 established *what* must always be legible (Source and vulnerability glyphs,
mastery states). Section 2 established *how* each state should *feel*. This section
defines the actual geometric vocabulary — the literal shape families every creature,
glyph, environment scar, and UI panel is built from — so that any artist drawing a new
asset is drawing from one shared system, not improvising a one-off look. Every choice
below answers the same test the Master Visual Rule sets: **can this be told apart from
its neighbors by silhouette alone, in under a second, without relying on color?**

### 3.1 Creature Silhouette Philosophy

A creature must satisfy two silhouette requirements that are normally in tension: it
must read as **one cohesive living thing** at a glance, and its **targetable body parts
must read as distinct clickable zones** in the middle of combat. IDLExIDLE
resolves this by putting the two jobs on two different layers of the silhouette,
borrowing directly from the stained-glass metaphor established in Section 1, Principle 2:

- **The outer contour stays unbroken.** From a distance, a creature is always one continuous, uninterrupted silhouette — this is what sells it as a single organism and lets the eye separate creature from arena (figure-ground) before any glyph is read.
- **Breakable parts are divided by internal seam lines, not silhouette notches.** A thin, fixed-value "leading" line (like the lead came between stained-glass panes) marks the boundary of each targetable region *inside* the contour. The outer edge never bites inward to show a part boundary — that would fragment the cohesive read Principle 1 depends on.
- **Seam value is palette-independent.** Regardless of a creature's Source colors, the seam renders as a fixed value-step darker than its neighboring fill (not a hue choice). This guarantees part-boundaries stay readable under any color scheme, including for colorblind players — a direct extension of the Master Rule's "never a color swap alone."
- **Each part differs in scale or proportion from its neighbors.** Gestalt similarity/proximity means a player can count "how many clickable zones" pre-attentively, before consciously reading anything — no two adjacent breakable parts should be the same size and shape.

*Design test*: If a targetable part can only be told apart from its neighbor by color, it
needs a seam-line and proportion pass before it ships.

*Serves*: **Pillar 1 — Precision Over Reflexes** (the player is reading part-geometry, not
scanning for a color); **Pillar 5 — Creatures Are Systems, Not Trophies** (a creature's
functional shape stays legible whether it's whole or broken mid-fight).

**Role reads through mass distribution.** A creature's overall proportions — independent
of any part-seam detail — telegraph its combat role before a single glyph is visible:

| Role | Mass Distribution | Silhouette Signature |
|---|---|---|
| Attacker | Forward-weighted, asymmetric | A single leading point or blade-limb the mass visibly leans into |
| Defender | Bottom-heavy, symmetric, broad base | Widest silhouette at the base; thickest plating at center mass |
| Support | Two connected forms | Main body + a smaller satellite shape joined by a visible tether |
| Crafter | Compact core, many small extremities | Bristling cluster of small manipulator-shapes, no single dominant limb |
| Producer | Single dominant swollen mass | Largest volume-to-limb ratio of any role; sac/pod/hopper-like core |

**Source reads through edge quality.** Independent of role, a creature's elemental Source
is legible purely from the *character of its contour line* — not from surface pattern, and
never from hue alone:

| Source | Edge Quality | Silhouette Signature |
|---|---|---|
| Body | Convex, smooth, symmetric | Continuous rounded arcs, musculature-implying bulges |
| Mind | Hard, faceted, symmetric | Straight-edged crystalline cuts, mirrored halves |
| Nature | Branching, asymmetric | Irregular non-repeating outgrowths — thorns, forks, flares |
| Machine | Orthogonal, rigid | Right angles, modular stacked/bolted plate segments |
| Shadow | Torn, discontinuous | Jagged bite-notches, frayed trailing edges in the contour itself |
| Spirit | Soft, dissolving | Contour thins and breaks into tapering wisps at the extremities |

Because Role (mass distribution) and Source (edge quality) sit on two independent axes,
every creature is a legible combination of the two — a Machine Defender and a Nature
Defender share the same bottom-heavy massing but never the same edge character, so role
and identity never collapse into each other visually.

*Serves*: **Pillar 5** (role is never a stat-sheet fact — it's drawn) and **Principle 1**
(Source identity is on the body, not gated behind a tooltip).

### 3.2 Glyph Shape Grammar

Every glyph in the game — Source, Vulnerability, Role badge, Ward/Ownership seal, Vow
scar — is built from the same two-tier grammar: an **outer container** that tells the
player *what category of information this is*, and an **interior mark** that tells them
*which specific value it holds*. This split matters for pixel-legibility: outer silhouette
differences survive at far smaller sizes and heavier downsampling than fine interior
linework, so the container can be read almost pre-attentively while the mark confirms the
detail with a single foveal glance — a two-stage read built for a one-second budget.

**Container grammar (category):**

| Category | Container Shape | Why |
|---|---|---|
| Source glyph | Closed circular medallion | A full, unbroken ring — Source is a stable, permanent identity, so its container never opens |
| Vulnerability glyph | Broken/open ring (a circle with a literal gap) | The gap *is* the weak point, made literal — "this is incomplete, this is where you get in" |
| Ward / Ownership seal | Closed ring + a second concentric ring | The healed vulnerability glyph, plus a reinforcing outer ring — the exact shape the mastery-transition inversion produces (Section 2) |
| Role badge | Shield/quatrefoil | Deliberately non-circular so a roster screen never confuses a role-badge for a Source medallion at a glance |
| Vow scar | A linear brand/crack-line, not a container | Marks an object's edge directly rather than floating a medallion on it, matching Section 2's "seared into the silhouette" description |

**Mark grammar (specific value):** interior marks for Source glyphs echo the same
edge-quality language used in creature silhouettes (3.1), so a player who has learned the
silhouette grammar recognizes the glyph grammar for free: Body = a solid convex droplet
stroke, Mind = a faceted triangle stroke, Nature = a branching fork stroke, Machine = a
right-angled bracket stroke, Shadow = a jagged tear stroke, Spirit = a radiating dash-dot
burst that fades outward. No interior mark exceeds roughly 3–5 strokes, so it holds up at
small pixel counts.

**Scope note — two different "healing" events, not one:** Section 2 describes two glyph
transformations that must not be conflated. The **boss's personal vulnerability→ward
inversion** is a one-time, per-creature cinematic beat where the container shape itself is
permitted to change (open ring → closed double ring) — that change *is* the reward. The
**region's ambient sigil-scars** embedded in terrain and ward-stones are numerous,
always-visible, and must obey the Contrast Mechanism from Section 2: identical crack
geometry in both states, with only edge-quality/integrity, color temperature, and light
changing (see 3.3). The personal inversion is the loud, singular event; the ambient scars
are the quiet, everywhere proof.

*Design test*: A new glyph should be nameable by its container alone before anyone has
looked at the interior mark. If two glyph categories need the same container to tell
apart, the grammar has failed and needs a new container, not a color fix.

*Serves*: **Principle 2 — ornament earns its keep only if it reads in under a second** (the
container/mark split is the concrete mechanism that makes the one-second rule achievable);
**Pillar 1** (the grammar is learned once and reused everywhere, so mastery of the visual
language is itself a skill the player builds).

### 3.3 Environment Geometry

Two geometry families coexist in every region, and they are deliberately treated
differently:

- **Terrain and architecture ("the bones")** — rock formations, ruins, structural silhouettes — stay organic, irregular, and largely **constant** between the Unconquered and Mastered states. This is a figure-ground choice as much as a budget one: if the whole environment geometry changed on mastery, the player would have to re-parse the entire scene, and the mastery signal would drown in noise instead of standing out against a stable backdrop. It also means only the glyph-overlay layer needs two states, not the base geometry — a concrete production efficiency reflected in Section 8's asset budgets.
- **Glyph-work (sigil-scars, ward-stones)** — the geometry that actually carries mastery information — follows the Contrast Mechanism locked in Section 2: **identical crack path in both states**, but the edge quality of that path changes completely. Unconquered renders the path as a torn, jagged, open void-gap (matching Shadow's edge-quality language from 3.1 — chaos reads as broken geometry regardless of biome). Mastered renders the exact same path as a smooth, continuous, closed, glowing inlay seam, with the old fracture line still visible as a mend-scar underneath the glow.

The rule in one sentence: **the crack never moves, it only closes.** This is the
environment-scale expression of the same open→closed container logic used for the
vulnerability→ward glyph in 3.2 — the same shape idea operating at two different scales of
the game (one creature's wound, one region's wound) is what keeps "mastery is drawn onto
the world" (Section 1, Principle 3) feeling like one coherent visual law instead of two
unrelated art tricks.

*Serves*: **Pillar 2 — Automation Is Earned, Not Assumed** (the terrain proves the region
is the *same place*, won rather than swapped for a reward-skin).

### 3.4 UI Shape Grammar

Given the information density this game's UI has to carry — forge, full creature roster,
automation configuration, loot filters — the UI shape language is a deliberate **hybrid**,
split along the same line as everything above: chrome versus content.

- **UI chrome (panels, frames, buttons, grid/table structure) is a distinct, neutral, modular language** — simple rounded-rect cards, strict column/row alignment, minimal ornamentation. Dense tabular information (a roster of dozens of creatures, a loot filter list, an automation config grid) needs uniform connectedness and strict alignment to stay scannable; dressing every panel edge in mythic stained-glass ornament would directly violate Principle 2's one-second rule at the system level, and a cluttered frame slows parsing exactly where speed matters most.
- **UI content that represents diegetic game information reuses the glyph grammar from 3.2 unmodified.** A creature's roster entry shows the *same* Source medallion and role-badge shapes the player learns in combat — not a simplified icon set that means the same thing but looks different. This was already locked for Creature Management in Section 2 ("rendered in the same stained-glass glyph language used in combat, so the screen reads as a glyph ledger, never a flat stat table") — 3.2's container grammar is what makes that promise concretely buildable, since the same glyph assets drop into both combat and UI contexts without redesign.

**Ornamentation is allowed to flex with ceremony, never with density.** High-ceremony
one-off screens (a Mastery Transition summary, a Forge Vow-binding confirmation) may carry
heavier frame ornament, matching the mood established for those moments in Section 2.
High-density utility screens (loot filters, automation config) never do, regardless of how
important the decision being made there is — importance is carried by the glyph content,
not by frame decoration.

*Design test*: If a UI element is showing the player a Source, a role, a vulnerability, or
a Ward state, it must be built from a 3.2 glyph — not a bespoke icon. If it's chrome (a
panel, a button, a scrollbar), it must not borrow stained-glass ornament from the world
layer.

*Serves*: **Pillar 1** (one learned visual language instead of two, so precision reading
transfers between combat and menus) and the **Master Rule** directly (color-only
iconography in dense UI is the single easiest place for the "color swap" anti-pattern to
sneak back in — the chrome/content split is the guardrail).

### 3.5 Hero Shapes vs. Supporting Shapes

Combat has exactly one job for the eye: find the vulnerability glyph before anything else.
Every other shape on screen is graded by how much it is allowed to compete with that read.
The hierarchy, loudest to quietest:

1. **Vulnerability glyph — the hero shape.** The only element allowed to animate (the telegraph pulse from Section 2) and the only element allowed to exceed the flattened mid-key combat lighting with an accent spike. Its open-container shape (3.2) is itself already the most visually "unresolved" — literally a broken form — before it even lights up.
2. **Creature outer silhouette.** Stable, clean, mid-contrast. Its job is pure figure-ground separation from the arena — it must read as a whole creature at all times, but it never carries detail bright or busy enough to compete with the vulnerability glyph sitting on its surface.
3. **Source glyph and part-seams.** Present, correct, and inspectable — but deliberately quiet during live combat: fixed low-to-mid contrast, no animation. They answer "what is this" and "where are the parts," which the player already knows by now; they must never re-answer "what do I click right now," which is the vulnerability glyph's job alone.
4. **Arena/background — recedes furthest.** Per Section 2, the arena is pushed darker, cooler, and flatter than the boss's own glyph-marks; any environmental ornamentation from 3.3 is suppressed during live combat and only permitted to "bloom" in idle or establishing shots, never mid-fight.

| Layer | Contrast | Motion | Rule |
|---|---|---|---|
| Vulnerability glyph | Highest, accent-spike | Animated (telegraph pulse) | Only element allowed to exceed ambient light |
| Creature silhouette | Mid | Static | Figure-ground only, never competes in detail |
| Source glyph / seams | Low-to-mid | Static | Inspectable, never animated in combat |
| Arena background | Lowest | Ambient only | Ornament suppressed during live combat |

The one sanctioned exception is the **Mastery Transition** (Section 2): when the
vulnerability glyph completes its inversion into the Ward seal, the *entire creature* is
deliberately allowed to become the hero shape as the light sweep expands outward — the
hierarchy is suspended for exactly one payoff beat, then resets for the next encounter.

*Serves*: **Pillar 1 — Precision Over Reflexes**, directly. This hierarchy exists so that
the one thing the player must act on is unambiguously the loudest shape in the frame —
success is a reading skill, never a clutter-parsing reflex test, which is the Master Visual
Rule's entire thesis applied to a single frame of combat.

---

## 4. Color System

Sections 1–3 already did the heavy lifting: shape carries category, edge quality carries
Source, mass distribution carries Role, value-steps carry part-boundaries. Because color is
never asked to be the *only* signal for anything mechanically important, this system can
afford to be small and disciplined instead of sprawling to cover every state on its own.
The palette below is built to be spent precisely, not spread thin.

### 4.1 Primary Palette

| Color | Hex | Role | Usage Notes |
|---|---|---|---|
| **Void Ink** | `#1B1620` | Universal seam/structure | The "leading" line from 3.1/3.2 — part-seams, glyph containers, UI chrome borders. Never a fill; always a line or a depth-shadow. |
| **Bone Parchment** | `#E8DFC8` | Neutral warm light | Base UI chrome fill, resting text color, Common-rarity baseline — the "nothing special is happening here" default. |
| **Hearth Gold** | `#F0A830` | Mastery / ownership | Reserved exclusively for earned states: ward seals, mastered sigil-scars, Legendary rarity, Forge inlay. Never decorative. |
| **Ember Threat** | `#D8483A` | Danger / opportunity | The vulnerability glyph's home color, at rest and at full telegraph (4.3). |
| **Cold Slate** | `#57616F` | Neutral cool ambient | Flattened combat lighting, gallery-neutral Creature Management light, architectural "bones." Explicitly *not* the unconquered-region signal (4.4). |
| **Corruption Bloom** | `#8B3F82` | Wrongness | Sickly violet-magenta bleed through unconquered sigil-scars and corruption VFX. Deliberately pushed off-harmony toward a sour undertone so it reads as biologically wrong, not regal. |

*Design test*: any new color proposed for the game must be justifiable as one of these six
roles, or a shade/tint of a Source color (4.2) — if it isn't, it's probably a decoration
trying to sneak in as a signal.

### 4.2 Source Color Assignments

Per Section 3, edge quality carries Source identity; mass distribution carries Role. These
colors are **reinforcing, not load-bearing** — a colorblind player, or a player at a glance
too quick to register hue, must still be able to identify Source from silhouette alone.
Color's job here is to reward a closer look, not to be required for a correct one.

| Source | Color | Hex | Why This Color | Edge Quality (3.1) |
|---|---|---|---|---|
| Body | Blood Crimson | `#8C2E42` | Flesh, muscle, vitality — the color of something warm-blooded, pushed deep/magenta rather than bright orange-red so it never reads as Ember Threat. | Convex, smooth, symmetric arcs |
| Mind | Crystal Cyan | `#3FA9C9` | Cool, clear, crystalline — clarity and precision-thought rather than warmth. | Hard, faceted, symmetric cuts |
| Nature | Moss Green | `#5C8A3A` | Growth, photosynthesis — deliberately the one Source where an unlearned player's real-world color instinct should just work for free. | Branching, asymmetric outgrowths |
| Machine | Rust Iron | `#9C5A32` | Worn metal, oxidation, industry — kept muddy and desaturated specifically so it never competes with Hearth Gold's clean saturated amber. Worn iron vs. polished treasure is itself a meaningful distinction. | Orthogonal, rigid plates |
| Shadow | Umbra Indigo | `#2E2438` | Near-black violet — a space with light physically removed from it, not merely dim. | Torn, discontinuous edges |
| Spirit | Wisp Lavender | `#C7BFE0` | Pale, high-value, low-chroma — the color of a thing whose substance has mostly dissolved out of it. | Soft, dissolving wisps |

**Role badges are not separately color-coded.** Role already reads through mass distribution
(3.1), so badges render in neutral chrome tones (Void Ink line, Bone Parchment fill) rather
than growing a seventh categorical color dimension the palette doesn't need.

**Flagged risk**: Corruption Bloom and Shadow both sit in the violet family. This is
addressed directly in 4.6, not incidentally — they occupy different game layers (world-state
overlay vs. creature Source identity) but can co-occur (a Shadow creature in an unconquered
region), so the disambiguation is spelled out rather than assumed.

*Serves*: **Pillar 5** (Source color is a bonus read on top of a body already legible without
it) and the **Master Rule** (color explicitly demoted from primary to secondary channel here,
by design).

### 4.3 Semantic Color Vocabulary

#### Danger/Telegraph & Vulnerability/Opportunity — one glyph, one color, two states

The vulnerability glyph (the open-ring container from 3.2) lives entirely in Ember Threat. It
never changes hue to communicate its own state — it changes **intensity and motion**, with one
deliberate exception that reconciles with Section 2's telegraph description.

- **Opportunity (resting)**: Ember Threat at low, steady intensity. Present on every vulnerability glyph, on every creature, regardless of Source — this consistency is what makes "this shape + this glow = hit here" a single pattern learnable once and reused forever.
- **Danger (telegraphing)**: the same glyph brightens along Section 2's locked slow-build/hard-cutoff curve and drifts in hue toward the *incoming attack's Source color* (this is the "color-keyed to the attack's element" language from Section 2, now formalized). Brightness and timing are the load-bearing "react now" signal; the hue drift is a secondary, non-load-bearing "which element" tell for players who've learned to read it — the skill ceiling Section 1 promises, not a requirement to survive.

*Design test*: cover the glyph's hue with a gray filter — if a player can still tell "safe to
approach" from "about to hit me," the system is working correctly.

*Serves*: **Pillar 1** directly — precision reading is rewarded, not required, by the extra
channel.

#### Mastery/Ownership — Hearth Gold, spent like a resource

Already set as the warm high-key color of the mastered state in Section 2. The rule going
forward: **if an asset isn't the direct result of a boss falling, a Vow binding, or a
Legendary drop, it does not earn Hearth Gold.** It appears on: Ward seals, mastered
sigil-scar glow, Forge cooled-inlay metal, Legendary rarity. Nowhere else — a background tint
or button hover that borrows gold without an earned event behind it dilutes the one color the
whole game trains players to want.

#### Corruption — a bleed, not a wash

Corruption Bloom is a **punctuation color**, not the unconquered region's ambient wash (that
stays in the cool, desaturated Cold-Slate-family range per Section 2 and 4.4). It appears
specifically bleeding through sigil-scar cracks and in corruption particle VFX. Corruption
Bloom and Hearth Gold are mutually exclusive on the same glyph outside exactly one sanctioned
moment: the Mastery Transition's outward sweep, where their coexistence in one frame *is* the
point.

#### Rarity Tiers — a value ramp, not a hue rainbow

The genre-standard rarity rainbow (gray→green→blue→purple→orange) was considered and rejected:
it would put Nature's green, a blue Source-adjacent hue, and Corruption Bloom's violet directly
into loot-tier duty, contradicting a system that otherwise refuses to let a single hue carry
two unrelated meanings. Instead, rarity moves along **one hue family only** — bone through gold
— via increasing saturation, value, and glow, paired with a discrete, countable frame ornament
that borrows the ring-count grammar from 3.2's Ward seal.

| Tier | Hex | Frame Ornament |
|---|---|---|
| Common | `#B7AF9C` | Plain Void Ink border, no ring |
| Uncommon | `#C9B27E` | Single ring |
| Rare | `#D9B34E` | Double concentric ring |
| Epic | `#E89A2E` | Double ring + four corner flourishes |
| Legendary | `#F0A830` (= Hearth Gold) | Full Ward-seal ornament — concentric rings + radiating flourish |

Legendary deliberately reuses Hearth Gold exactly, not a lookalike — a Legendary item is framed
as a thing you've mastered, the same claim a Ward seal makes.

*Design test*: **the frame ring-count is the mechanical rarity signal; the color is a confirming
glow only.** If a rarity distinction can only be told by color, it needs a ring before it ships.

*Serves*: the **Master Rule** and **4.6** directly — this is the classic loot-game color-only
failure point, solved at the design level rather than patched at the accessibility level.

### 4.4 The Temperature Law (Corrupted vs. Mastered)

**Any color tied to a region's or creature's conquest-state sits on one warm↔cool axis with
exactly two poles — never a third neutral, never a mix, outside one sanctioned exception.**

- **Unconquered pole**: ambient hue in the cool 190°–230° range (Cold-Slate-family wash), desaturated (roughly 20–35% saturation) and unstable in value (flicker) — with Corruption Bloom (~307°) reserved specifically for crack-bleed *within* that wash, not the ambient itself.
- **Mastered pole**: ambient hue in the warm 30°–45° range (gold-family wash), saturated (roughly 50–70%), stable in value (no flicker) — the exact same crack geometry, now glowing Hearth Gold.
- **The one sanctioned exception**: the Mastery Transition's outward light sweep (Section 2), where both poles are allowed in the same frame because their collision is the entire emotional point of the beat.

**The clarifying nuance that keeps this from colliding with combat's neutral mood**:
conquest-state color is an *establishing-shot and idle-view* signal, not a *combat-frame*
signal. During Active Hunting, ambient conquest-color is suppressed to Cold Slate neutral
regardless of whether the region is actually unconquered or mastered — this is a direct
application of 3.5's rule that environmental ornament recedes during live combat, so the
temperature story never competes with the vulnerability-glyph read. The conquest-state wash
returns the moment the encounter ends. This is also why Cold Slate is never itself "the
unconquered color" — that specific claim belongs only to the desaturated cool wash plus
Corruption Bloom bleed, and only outside live combat.

*Design test*: "If you're not sure whether an asset's color should express conquest-state, ask
whether the camera is mid-encounter (suppress) or in an establishing/idle view (express)."

*Serves*: **Pillar 2** and **Principle 3** (Section 1) — the world's color temperature is one
more way mastery is drawn onto the world rather than tracked in a menu.

### 4.5 UI Palette

Per 3.4's chrome/content split, color diverges the same way shape does.

**Chrome** (panels, frames, buttons, grid/table structure) draws from a restricted neutral
subset of 4.1 only: Void Ink (borders/backgrounds), Bone Parchment (fills/text), Cold Slate
(disabled/inactive states). Chrome must never spend Hearth Gold, Ember Threat, Corruption
Bloom, or any Source color as decorative fill — those remain reserved for meaningful
glyph-content signals, exactly as 3.4 reserves ornament for content over chrome. The one
exception: a chrome element may briefly borrow a reserved color when it is itself the trigger
for that exact meaning (a Forge "Bind Vow" confirmation button may glow Hearth-Gold-adjacent on
hover, because pressing it *creates* a mastery-tier commitment) — this must stay rare enough to
remain an event, never a decoration.

**Content** (Source medallions, role badges, rarity frames, Ward seals rendered in menus)
reuses the exact hex values from 4.1/4.2 unmodified — no separate "UI-safe" recolor pass,
matching 3.4's "reuses the glyph grammar unmodified" rule extended into color.

**Text contrast**: default body text is Bone Parchment (`#E8DFC8`) on Void Ink (`#1B1620`)
chrome — comfortably clears the WCAG AA 4.5:1 minimum for body text. Any future alternate
chrome background (light-mode, colorblind-alt palette) must be checked against the same 4.5:1
floor before it ships.

*Serves*: the **Master Rule** (the chrome/content split is the guardrail against color-only
iconography sneaking into dense UI) and **Pillar 1**.

### 4.6 Colorblind Safety

The architecture is already unusually colorblind-safe by construction — shape carries category
(3.2), edge quality and mass distribution carry Source and Role (3.1), value-steps carry
part-boundaries (3.1). Color is never the only channel for anything mechanically important.
This section audits the specific places where two *different* meanings could visually converge
for a colorblind player, and names what survives.

| Color Pair | Affected CVD | Risk | Backup Cue That Survives |
|---|---|---|---|
| Nature (Moss Green) vs. Machine (Rust Iron) | Deuteranopia / Protanopia | Highest risk in the palette — green and red-brown both shift toward a similar olive/yellow-brown | Edge quality: branching asymmetric outgrowths vs. orthogonal rigid plates (3.1) — resolved by shape alone |
| Body (Blood Crimson) vs. Nature (Moss Green) | Deuteranopia / Protanopia | Red and green can both desaturate toward a similar muddy yellow | Edge quality: convex smooth arcs vs. branching outgrowths (3.1) |
| Corruption Bloom vs. Shadow (Umbra Indigo) | Tritanopia primarily; general value-clustering for all types | Both sit in the violet family and can co-occur (a Shadow creature in an unconquered region) | Value contrast (Corruption is mid-value/saturated, Shadow is near-black) + different container shapes entirely (creature Source medallion vs. environment crack, 3.2 vs. 3.3) + torn/discontinuous edge quality |
| Danger telegraph hue-drift (Ember Threat → attacker's Source color) | Deuteranopia / Protanopia, specifically when drifting toward Nature green | The red→green drift crosses the single worst CVD axis | Explicitly non-load-bearing by design (4.3) — brightness curve and fixed timing carry the actual "react now" signal; losing the hue drift loses only the bonus "which element" read |
| Rarity tiers (Common → Legendary) | All types | Five gradations of one hue family by saturation/glow alone is a fine discrimination task for any viewer, harder for low-vision/CVD players | Frame ring-count/ornament-tier (0 / 1 / 2 / 2+flourish / full-seal) is a discrete, countable shape signal, not a color judgment — plus item tooltips state rarity in text as a final fallback |
| Mind (Crystal Cyan) vs. Spirit (Wisp Lavender) | Tritanopia | Low risk — both blue-family but separated by strong value contrast (mid-saturated vs. near-white) | Edge quality: hard faceted cuts vs. soft dissolving wisps (3.1) |

**Closing rule**: no new semantic color may ship without being checked against this table's
method — name the CVD type it's most at risk under, and name the non-color cue (shape, motion,
value-step, or text) that survives if the hue information is lost entirely.

*Serves*: the **Master Rule** directly, and **Pillar 1** — a precision-reading game is only fair
if its signals are perceivable by every player attempting to read them.

---

## 5. Character Design Direction

Sections 1–4 established the vocabulary; this section puts it on the two things players spend
the most time looking at — the Hunter, and the creatures they hunt, farm, and eventually feel
ownership over. It also formally discharges the two pillar debts Section 1 deferred: **5.2**
resolves Pillar 4 (the Vow/enchantment silhouette standard), and **5.3** resolves Pillar 3 (the
idle-creature "still working" spec).

### 5.1 The Hunter — Visual Archetype & Frame Presence

**Archetype**: a practical tracker-alchemist, not a knight or a mage. Layered cloth and worn
leather, a tool-harness silhouette (satchels, vials, a bound ward-kit) rather than armor-plate
bulk, built around one clear held-weapon silhouette. The base, unmarked Hunter is deliberately
the **plainest character model in the game** — Bone Parchment cloth, Cold Slate leather, Void
Ink stitch-lines — using the two most neutral colors in the palette (4.1) and none of the
reserved ones. A Hunter who has taken zero Vows and socketed zero enchantments should look
genuinely undecorated, so that every mark added later (5.2) reads as *earned*.

**Frame presence — the Hunter is visible on screen throughout combat, but small and
subordinate.**

- **Scale**: roughly 20–30% of the current creature's on-screen height for a standard encounter (smaller against a boss), and roughly 10–15% of total frame height.
- **Position**: lower third of frame, offset to one side (foreground-left by convention) — never centered. The creature holds the visual center; the Hunter is a grounding anchor, not a competing subject.
- **Rendering discipline**: the Hunter's body and base gear sit at the same contrast/animation tier as **Source glyph and part-seams** in the 3.5 hierarchy — present, inspectable, static, never brighter than ambient combat lighting. Nothing on the Hunter may ever exceed the vulnerability glyph's peak brightness; that ceiling applies to the whole frame.

**Why visible rather than abstracted (the alternative considered and rejected)**: a
camera-on-creature-only staging, with the Hunter reduced to an off-screen cursor, would
maximally protect the vulnerability-glyph hierarchy — but it costs two things this bible can't
afford. First, it removes the only canvas the Pillar 4 debt (5.2) has to render on *at the
moment it's mechanically live* — Vow scars would only ever be inspectable in a menu, exactly
the "tracked in a stat panel instead of drawn on the world" failure the Master Rule exists to
prevent. Second, it's a functional loss: the player needs to see their own position to judge
dodge distance and block timing against a telegraph. An invisible avatar quietly reintroduces a
*guessing* problem into a game whose thesis (Pillar 1) is that success is a reading skill.

*Design test*: if the Hunter's presence ever becomes detailed or bright enough that a
playtester's eye goes to the Hunter before the vulnerability glyph, shrink or flatten the Hunter
— never brighten or shrink the creature to compensate.

### 5.2 Vow & Enchantment Silhouette Standard (Pillar 4 Debt)

Section 2 locked the *item-side* half of this: a Vow binds to gear at the Forge, and a glyph
mark sears into that item's silhouette as it cools. This section resolves the half left open:
**once that gear is equipped, how the Hunter's own silhouette carries it — legibly, without
collapsing into noise once several Vows and enchantments are stacked.**

| | Vow Scar | Enchantment Inlay |
|---|---|---|
| **Represents** | A restriction the Hunter personally accepted (Source × Form × **Vow**) | A behavior change carried by a piece of forged gear |
| **Container (3.2 family)** | Linear brand/crack-line — **not** a container, per 3.2's existing entry | A closed diamond/lozenge inlay — **a new container**, filling the gap 3.2 left open for enchantments |
| **Body zone** | The belt/bandolier band — worn charms, one per equipped ability/ultimate slot | The gear silhouette itself — weapon, armor, accessory |
| **Cap** | Bounded by the existing ability loadout (3 abilities + 1 ultimate) — **at most 4 Vow scars visible at once**, because there is nowhere to hang a fifth | Bounded by equipped-gear-slot count (owned by the itemization GDD; this standard holds regardless of the final number) |
| **Permanence** | Permanent once seared. If the Vow is later replaced, the old scar remains as a pale, healed line under the new one — never erased | Fully present while equipped, fully absent when unequipped — a loadout, not a life-mark |
| **Color** | Void-Ink-family line only, matching seam-line value language (3.1) — deliberately colorless | A tint of the enchantment's Source-color family (4.2) as a reinforcing-only accent |
| **Combat behavior** | Static, low-contrast, never animated — same discipline tier as Source glyph/seams (3.5) | Same — static, low-contrast, never animated in combat |

**Why the belt is a natural cap, not an arbitrary one**: the game already limits the player to 3
abilities plus 1 ultimate. Tying Vow scars to *ability slots that already exist* means the
silhouette standard can never run away from itself — the player physically cannot equip a fifth
Vow-bearing ability, so the Hunter can never wear a fifth scar. The art system inherits its cap
from a mechanic that's already locked.

**Why the scar/inlay distinction matters beyond taxonomy**: a Vow is a *restriction the Hunter
chose to carry* — thematically that belongs on the body, permanent, like a brand. An enchantment
is a *property of equipment* — that belongs on the object, removable, like a fitting. Rendering
them identically would blur the exact conceptual line Pillar 4 is built on.

**Legibility at scale — "silhouette as build summary"**: the belt-charm cluster and the
gear-inlay marks occupy two fixed, separate screen bands, so a player can pre-attentively
register "how marked is this build" as a silhouette-density read — the same Gestalt counting
logic 3.1 uses for creature part-seams. **The price of the build is written on the body,
readable before a single glyph is individually parsed.**

*Design test*: cover every glyph's color in gray — a player must still tell "this Hunter has
taken Vows" from "this Hunter has none" by scar presence and belt density alone, and "this is a
Vow" from "this is an enchantment" by container shape (crack-line vs. diamond) alone.

*Dependency note*: this assumes Vows attach to a small, fixed number of ability-focus items. If
the Resonance Weaving GDD binds Vows elsewhere, the zoning maps onto whichever slots exist — the
container shapes, permanence asymmetry, and belt-vs-gear separation hold regardless.

### 5.3 Bound Creature Work-Loop Spec (Pillar 3 Debt)

Section 2 seeded this with the work-loop glyph pulse. This section expands it into a full spec:
**per-role animation identity, pulse cadence, and an at-a-glance health signal**, so that
"automation is running," "automation is stalled," and "automation is starving" are three visibly
different things readable across a whole region without opening a panel.

**Per-role work loops** — the Role vocabulary from 3.1's mass-distribution table, now expressed
as motion:

| Role | Mass Distribution (3.1) | Work-Loop Performance | Work-Tick Moment |
|---|---|---|---|
| Attacker | Forward-weighted, blade-limb | A repeating strike-and-recover cycle against a resource node — the widest motion arc of any role | The strike-impact frame |
| Defender | Bottom-heavy, broad base | A held brace/guard stance with slow, low-amplitude weight-shifts — production reads as *holding*, not moving | The peak of the brace-flex |
| Support | Body + tethered satellite | The satellite drifts a slow orbit around the main body, dipping toward a resource point and back | The satellite's contact moment |
| Crafter | Compact core, many manipulators | Rapid, small, busy manipulator motion around the core — the highest-frequency animation of any role | A brief synchronized flourish across all manipulators at cycle-end |
| Producer | Single swollen sac/pod | The slowest cadence of all: a rhythmic swell-and-release breathing pulse of the body mass itself — production is the body, not a limb | The release/exhale frame |

Every role gets a **visibly different verb** — striking, bracing, orbiting, fidgeting, breathing
— not the same idle loop recolored. A player scanning a mixed-role region should tell what a
creature *is doing* from silhouette-in-motion alone.

**Pulse cadence — relative ordering only** (exact seconds owned by the automation-systems GDD):
Crafter fastest → Attacker and Support mid → Defender slow → Producer slowest. Art commits to the
*ordering*, not to tuning numbers that belong to balance.

**The Work-State Signal Set**: a region full of creatures pulsing in healthy rhythm has an
emergent, readable heartbeat (Gestalt common fate). A creature in trouble is the one thing that
visibly *breaks that rhythm* — spotted pre-attentively before any glyph is consciously read.

| State | Pose/Motion | Glyph Behavior | Color (existing palette only) |
|---|---|---|---|
| **Healthy** | Full role-specific work loop, playing continuously | Pulses on schedule at its role's cadence | Hearth Gold — an earned, working state |
| **Blocked** (output has nowhere to go) | Animation stalls mid-cycle — visibly never reaches its work-tick moment (the crafter's flourish never lands, the producer never exhales) | Holds at a steady, non-pulsing glow — lit but frozen | Desaturated toward Cold Slate — the glow goes flat, not brighter |
| **Starved** (no input resource) | Drops the work loop for an agitated/searching secondary pose layered on the role's base stance — reaching toward an empty resource node | Pulses on a slow, irregular "distress" cadence, deliberately different from the healthy rhythm | Hearth Gold with a faint Ember Threat flicker bleeding at its edge — reusing the game's existing "needs your attention" color exactly as combat trained the player to read it |

All three states are distinguishable by **pose and motion alone**. Cover the glyph in gray and a
player must still tell Healthy from Blocked from Starved by silhouette animation.

**Why this satisfies Pillar 3**: "idle is never worthless" fails the moment a bound creature's
default appearance is a static, parked sprite — that's indistinguishable from dormant regardless
of what the simulation is doing underneath. By giving every role a distinct, continuous,
non-idle-looking verb, the *baseline* state of automation is already "visibly working," and
Blocked/Starved become genuine alerts layered on top.

*Design test*: if a creature's Healthy work loop and its hostile-encounter idle animation (5.4)
could be mistaken for each other with the sound off, the work loop needs a more distinct verb.

### 5.4 Creature-State Distinguishing Rules — Hostile / Bound / Rare

Three states, told apart in strict priority order: shape/motion first, glyph container second,
ambient color third — never the reverse.

| State | Primary Tell (motion) | Secondary Tell (glyph container, 3.2) | Reinforcing Tell (ambient color, 4.4) |
|---|---|---|---|
| **Hostile / unconquered** | Alert threat-display idle — prowling, bristling, aggressive weight shifts | Open/broken-ring vulnerability glyph, visible and live | Cool, desaturated, flickering wash |
| **Bound / working** | The role-specific Healthy work loop (5.3) | Closed double-ring Ward seal — the vulnerability glyph no longer exists on this creature at all | Warm, stable, golden-hour wash |
| **Rare / capturable** (deferred post-MVP, spec'd now to avoid retrofit) | Same hostile threat-display as any unconquered creature — it is still a live encounter | Open vulnerability glyph, **plus** a satellite aura-ring drawn just outside the outer contour, reusing 4.3's exact rarity ring-count grammar | Same cool/unstable wash as any hostile creature — rarity is not a conquest-state signal and must not borrow that axis |

The hostile-vs-bound distinction falls almost free out of two things already locked (the Mastery
Transition's glyph inversion, and the Temperature Law) — this section's contribution is naming it
as the **at-a-glance rule** and pairing it with a motion-language contrast (threat-display vs.
labor-display) so the read holds in peripheral vision.

The rare-creature aura-ring is deliberately the lightest possible touch: it borrows 4.3's rarity
grammar wholesale and sits *outside* the contour rather than notching it, so it can never violate
3.1's unbroken-outer-contour rule. When capture-without-killing ships, no existing creature asset
needs a redesign — only an additional ring layer.

*Design test*: a player with color entirely disabled must still sort a mixed group of creatures
into "hostile," "bound," and "notably rare" using only motion-language and glyph-container shape.

### 5.5 Expression & Pose Style

The tonal problem: the same creature must be genuinely threatening as a hostile boss and
genuinely endearing as an owned, working creature — without a redesign between the two. The
solution is to **separate identity from performance**: a creature's geometry (Source edge-quality,
Role mass-distribution, silhouette — all locked in 3.1) stays completely fixed across every state;
only the *animation performance* riding on that fixed geometry changes. This is the same "the
crack never moves, it only closes" logic from 3.3, applied to a creature's rig instead of a
region's terrain.

**Style: grounded expressive** — not stiff, not cartoon-exaggerated.

- **Anticipation and follow-through are exaggerated enough to read at combat speed** (serving telegraph-reading directly) — but amplitude stays restrained enough that creatures read as living systems (Pillar 5), not slapstick mascots. A deliberate middle point between "stiff" (which undercuts the telegraph-reading Pillar 1 depends on) and "bouncy-cartoon" (which undercuts the "beautiful danger" and "ritual confrontation" moods).
- **The hostile performance** uses sharp, sudden curves — minimal easing, wide-amplitude threat-display idles, each Source's edge-quality language pushed to its most emphasized (a Nature creature's asymmetric branches flare and twitch; a Machine creature's plates lock rigid and vent).
- **The bound performance** uses softened easing — slow ease-in/out, more settle and follow-through, smaller amplitude. Emotion is expressed *within* each Source's existing movement grammar rather than a universal "cute wiggle" borrowed across all of them — a happy Machine creature gets a stiff, orthogonal little salute-bow, not a soft round bounce, so Source identity never blurs just because the creature is content.
- **One cheap, high-leverage addition for ownership warmth**: bound creatures gain a brief "acknowledge" beat — a glance or orient toward the player's cursor when attention lingers nearby in the Idle Region or Creature Management views. One short animation on the same rig does most of the emotional work of "this creature is mine and it knows it," without touching combat assets.

*Design test*: play the hostile and bound animation sets back to back, paused on a single frame
from each — a viewer should tell which is which from pose alone, without color, lighting, or glyph
state as a hint.

### 5.6 LOD & Detail Philosophy

**Sprite scale is tiered by ceremony, not a single fixed size:**

| Tier | Recommended Canvas Height (native) | Rationale |
|---|---|---|
| Standard encounter creature | ~96–160px | Enough room for 3–5 seam-divided parts (3.1) and one Source glyph + one relocating vulnerability glyph |
| Region boss | ~192–320px | Matches Section 2's "monumental, ceremonial" boss mood; more canvas supports more breakable parts and a vulnerability glyph that relocates across phases |

**The floor rule — complexity scales down with canvas, never the reverse.** A smaller creature has
*fewer* seam-divided parts, not the same part-count crammed into less space. Legibility minimums
are fixed regardless of tier: a glyph container needs roughly 12–16px in its smallest dimension to
keep its 3–5-stroke interior mark distinguishable (per 3.2's stroke-count ceiling), and a seam-line
stays at the same 1–2px "leading" weight from 3.1 at every size. If a design wants more parts than
a canvas supports at those minimums, **the canvas grows — the minimums never shrink.**

**Palette-layer separation**: every creature's indexed palette keeps body-fill colors (Source ramp +
Void Ink seam + neutral rim-light) and glyph-accent colors (Ember Threat, Hearth Gold) on
**separate ramps that never blend** — Section 1's "the glyph is an overlay layer" made literal at
the pixel level, and what keeps the glyph legible against any Source color the body happens to be.

**Two contexts, two assets, not one asset stretched to fit both**: the full multi-part combat
silhouette is one asset; the Creature Management roster portrait is a separate, cropped close-up
centered on the creature's core mass and Source glyph — *not* a shrunk-down copy of the full body.
Shrinking the full silhouette to icon size would destroy the part-seam legibility 3.1 depends on;
cropping sidesteps the problem rather than fighting it.

**Animation authoring principle** (for technical-artist collaboration): a creature's performance
surface is **its own single role's work loop** (in Healthy / Blocked / Starved variants) × the
hostile-vs-bound performance sets × the standard combat states (idle / telegraph / attack / hit /
death). The five role work loops in 5.3 are the game's total *vocabulary* across the roster — they
are **not** a per-creature multiplier. Role is a fixed one-of-five identity axis per creature (3.1);
an Attacker never performs a Producer's breathing loop.

Even so scoped, the load is significant, and it should be treated as **one shared rig per creature
with varying timing curves and a small set of additive secondary-motion layers** (glyph pulse, limb
twitch, breathing amplitude), not fully separate hand-authored frame sets per state. Section 8.4
turns this into concrete frame budgets; Section 8.5 records the animation-system decision that makes
it buildable in MonoGame.

*Design test*: at final in-game scale (not zoomed for review), a first-time player must be able to
(a) count a creature's clickable parts and (b) locate its active vulnerability glyph within the
one-second budget from Section 1, Principle 2. If either fails, the canvas tier is too small for
that creature's part-count — not the art too subtle.

---

## 6. Environment Design Language

Sections 2–4 already locked the environment's mood targets, its core geometry law (3.3's
"the crack never moves, it only closes"), and its color law (4.4's Temperature Law). This
section turns those locked rules into buildable environment art.

*Scope note*: this section covers terrain, architecture, ambient dressing, and
infrastructure only. Creature and hunter-character specifications are owned by Section 5.

### 6.1 Two Compositions, One Region

Because combat is a contained single-screen arena with no free-roam traversal (an explicit
anti-pillar), every region is authored as **two camera compositions of the same place**, not
one background asset reused at different zoom levels:

1. **The Arena** — the tight, playfield-scaled composition used only during an active encounter. Composed to serve Section 3.5's hero-shape hierarchy: nothing in it may outrank the creature or its vulnerability glyph.
2. **The Region View** — the wide, diorama-scaled composition used for the Idle/Automated Region View (Section 2) and any region-overview screen. Carries mood, color-temperature storytelling (4.4), automation infrastructure (6.7), and the full sigil-scar network (6.6) — everything the Arena is required to suppress.

Both share the same "bones" (3.3's constant terrain/architecture) and the same
dominant-Source identity language (6.3) — the same region through two lenses, not two
places. The Arena is a tightly cropped, gameplay-staged fragment of the Region View's
landscape: fewer sigil-scars in frame, less infrastructure visible, same rock, same ruins.

*Design test*: if a piece of region art only exists in one composition and can't be explained
as "the same location, differently framed," check whether it belongs there at all.

### 6.2 Arena Composition Rules

The Arena has exactly one job: let the creature and its vulnerability glyph win the
figure-ground fight, every frame, without exception.

| Layer | Depth | Contrast Ceiling | Motion Allowed | Combat-State Treatment |
|---|---|---|---|---|
| Backdrop | Furthest | Below "Arena background" row, 3.5 | One slow ambient layer only (drifting cloud, haze) | Desaturated, Cold-Slate-neutral regardless of conquest state (4.4) |
| Playfield floor/walls | Mid, but close (defines the contained bounds) | Must sit below creature silhouette's mid-contrast band (3.5) | None beyond the boss's own resonance halo (Section 2), which is diegetic | Sigil-scars present but dimmed and static — no glow-bloom, no color-temperature signal, no flicker VFX |
| Foreground framing | Nearest | Flat Void Ink silhouette, zero internal detail | Static | Frame edges/corners only — reinforces boundedness, never overlaps the creature's silhouette zone |

**Forbidden in an Arena during live combat**:

- Any animated background ornament beyond the single sanctioned ambient layer (corruption drift, foliage sway, ember particles all read as noise the moment they compete with the telegraph pulse)
- Any conquest-state color wash, warm or cool — the arena is Cold-Slate-neutral throughout combat regardless of whether the region is unconquered or mastered (4.4)
- Any sigil-scar rendered at its "bloomed" state — both states drop to the same suppressed neutral treatment mid-fight
- Any change to arena layout or geometry mid-encounter — spatial memory (where the walls are, where the creature stands) must not shift once a fight begins, or precision-targeting (Pillar 1) degrades into a spatial-tracking problem
- Any foreground element intruding on the space where the creature or its vulnerability glyph will appear

*Serves*: **Pillar 1** (Section 3.5's hierarchy translated into a literal build checklist) and
**Pillar 2**'s companion rule from 4.4.

### 6.3 Region Identity: Dominant Source, Shared Architecture

**The call**: each region's identity is carried primarily by its **dominant elemental
Source** (body, mind, nature, machine, shadow, spirit), expressed through the same
edge-quality and color grammar already locked for creatures in 3.1 and 4.2 — not by an
independent biome system and not by unique per-region architecture.

Source-first wins on three counts:

1. **Predictive legibility (Pillar 5)**: walking into a region whose terrain reads "branching, asymmetric, Moss Green" tells the player what kind of creatures they're about to fight before a single one appears — the world telegraphs its own systems.
2. **Production efficiency**: no second design system is needed. An artist building region terrain pulls from the exact edge-quality and color tables already locked for creatures.
3. **Worldbuilding coherence (feeds 6.6)**: if all regions are fragments of one thing, a single shared identity axis is what makes a growing region roster read as one coherent, systemic world instead of disconnected vignettes.

**Biome falls out as a consequence, not a separate axis.** A Nature-dominant region naturally
reads as overgrowth reclaiming ruins; a Machine-dominant region as industrial wreckage; a
Shadow-dominant region as a lightless void-cave. No biome is designed independently of its
Source.

**Architecture is the connective tissue, not the identity axis.** Every region shares one
underlying architectural language — the ward-network ruins described in 6.6 — so that a
Nature region's vine-choked ward-stone and a Machine region's corroded, riveted ward-stone
are recognizably the *same kind of structure*, differing only in the Source edge-quality
applied to it. This is what keeps regions feeling like one shattered civilization rather than
unrelated tilesets.

*Design test*: if a region's identity relies on a detail that can't be traced back to its
dominant Source's edge-quality/color entry (3.1/4.2) or the shared ward-architecture, that
detail is likely inventing a redundant identity system and should be cut or re-derived.

*Note on region count*: not yet locked (resolved at `/map-systems`). If the Full Vision roster
ends up with more regions than Sources, multiple regions may share a dominant Source at
different corruption intensities or ward-network "eras" (6.6) rather than needing a seventh
identity axis.

### 6.4 Texture & Rendering Philosophy

**Fidelity target: clean-pixel, not painterly.** The entire legibility model — glyphs read in
under a second (Principle 2), colorblind-safe shape/edge/value cues (3.1, 4.6), a strict
contrast hierarchy per frame (3.5) — depends on flat, controlled value steps. A painterly or
heavily-textured style adds visual information the eye must filter out before it can find the
vulnerability glyph.

**Two fidelity modes**, matching the Temperature Law's combat/establishing split (4.4):

- **Combat-suppressed mode**: flat fills, minimal value range, no gradients, no dithering — the arena's job (6.2) is to disappear, not to impress.
- **Establishing/idle-bloom mode**: full mood expression permitted — richer value range, ambient particle work, dithered gradient bands for skies/fog, the full unconquered-flicker or mastered-glow treatment on sigil-scars.

**Where dithering is and isn't allowed**: reserved for large, flat gradient fields with no
mechanical meaning — sky-to-horizon transitions, fog volumes, distant haze. **Never** on a
sigil-scar crack line, a part-seam, or any edge carrying the fixed-value "leading line" logic
from 3.1/3.2 — dithering softens exactly the crisp edge that legibility and colorblind-safety
depend on. Hard flat fills stay the rule for anything load-bearing; dithering is decoration
for the sky, never structure for the story.

**How backgrounds stay quiet *and* beautiful**: the two modes resolve this directly — a
background is never asked to be both suppressed and gorgeous in the same frame. It earns full
"beautiful danger" or "earned peace" treatment only in the Region View and idle establishing
shots, and steps back to flat neutral the instant an encounter begins.

### 6.5 The Three-State Region System

| Layer | Unconquered | Transitioning | Mastered | Authored As |
|---|---|---|---|---|
| Terrain/architecture ("bones") | — same — | — same — | — same — | **1×** per region, never duplicated |
| Sigil-scar crack path/mask | — same — | — same — | — same — | **1×** per region, a shape/mask, not a rendered image |
| Sigil-scar treatment | Torn, jagged, void-gap, Corruption Bloom bleed, flicker | Live interpolation between the two end-states | Closed, glowing inlay, Hearth Gold, faint mend-scar visible beneath | **2×** — the only doubled content; a "skin" pass over the shared mask, not a redraw |
| Ambient lighting/color wash | Cool, desaturated, unstable (4.4) | Animated sweep, cool → warm, expanding outward/upward from the boss's fall point | Warm, saturated, stable (4.4) | **2×** lighting/grade passes over the same constant geometry |
| Automation infrastructure | Absent | Appears progressively as the sweep passes (6.7) | Present, cumulative by stage unlocked | Additive overlay, mastered-only, never touches the bones |

**The Transitioning state is not a third hand-painted environment** — it is a timed animation
that interpolates the *existing* Unconquered and Mastered sigil-scar treatments along the
*same* mask, while the ambient wash sweeps cool → warm. This satisfies "the crack never moves,
it only closes" as a literal build instruction and avoids authoring a third full environment
pass per region.

**Sweep origin**: per Section 2, the environmental sweep originates at the boss's fall position
and travels outward to the Region View's edges — the same directional rule the boss's personal
glyph-inversion cinematic uses, applied at region scale.

**What an artist authors per region, concretely**:

1. One base terrain/architecture silhouette (the "bones"), built to the dominant-Source edge-quality language (6.3)
2. One sigil-scar crack path/mask, integrated into the bones at ward-stone/ruin junctions (6.6)
3. Two sigil-scar treatment passes over that mask (torn/unconquered, inlay/mastered)
4. Two ambient lighting/color-grade passes over the constant geometry
5. One transition sweep animation (interpolates 3 and 4, introduces no new geometry)
6. Automation infrastructure objects, additive and cumulative by stage (6.7) — authored once each, mastered-state-only

Five authored layers per region, only one of which (the sigil-scar treatment) is genuinely
doubled.

### 6.6 Environmental Storytelling

Narrative is deliberately thin for MVP. Environment art builds history through **systems and
scars**, not plot or text: the player should reconstruct "what happened here" from shape alone,
the same way they reconstruct a creature's role from its silhouette.

**The implied history (visual only, never stated in text)**: every region's ward-stones and
sigil-scars share one architectural language (6.3) because they are fragments of a single,
older **ward network** — something built long before the player arrived to hold each elemental
Source in check. The player never needs to learn who built it or why it broke; the environment
only needs to *look* like an engineered system that failed, not like random ruin decay.

- **The crack follows an engineered seam, not a random fracture.** The channel the sigil-scar occupies reads as a carved inlay groove even in its torn state — implying the joint was *built* to hold light/order, and corruption tore an existing seam open rather than smashing something never meant to bend. This is what makes "mastery re-seals it" read as restoration rather than repair-from-scratch.
- **Debris matches the region's dominant Source.** A Machine region's rubble is broken gearwork; a Nature region's ward-stones are swallowed in asymmetric overgrowth. This implies *duration* — corruption has been active long enough for the environment to visibly react.
- **Corruption Bloom bleeds outward from the crack**, staining surrounding terrain, never the reverse — reading as active, spreading wrongness rather than old contained damage.
- **Scale stays small and personal, never lost-civilization-grand.** Ward-stones are sized to a single hunter/worker's scale. This keeps "who built the ward network" a permanently open, cheap-to-leave-unresolved mystery while cleanly separating "the old, unseen builders" from "your own current operation" (6.7).

**Recurring motif — the empty plinth**: a small, identical ward-alcove/plinth, scattered a
handful of times through every region, dark and cracked when Unconquered, lit with a single
small Hearth Gold flame-glyph once Mastered. Cheap (one shape, two states, reusing the existing
crack-treatment grammar) and does three jobs: multiplies the "did I do this" reward beat across
many small moments instead of only the one big transition; gives the Region View many legible
micro-signals of "this whole area is now productive" (Pillar 3); and sets up automation
infrastructure as literally plugging into sockets the world already implied were waiting.

### 6.7 Automation Infrastructure Visuals

Each of the four staged unlocks gets one additive, cumulative piece of infrastructure, built
from grammar the player already knows rather than inventing new iconography per stage.

| Stage | Infrastructure Object | Visual Grammar Reused | Primary Read Distance |
|---|---|---|---|
| Auto-attack | A standing ward-post at the region's most visible vantage, carved with the bound creature's Source medallion + role badge (3.2) | Glyph container grammar, unmodified | Mid — the first proof "someone lives here now" |
| Auto-loot | A collection cairn/hopper beside the ward-post, with a visible rising fill-level | Producer-role mass-distribution logic (3.1) translated to architecture — a swollen single-volume shape reads as "collection point" the same way it reads as "producer creature" | Mid — legible on approach in the Region View |
| Auto-craft/sell | A modest forge-stall structure, warm localized glow, periodic work-loop pulse | The Forge's mood language (Section 2) plus the work-loop glyph pulse | Mid-to-near — the largest structural addition |
| Offline progression | A tall ward-lantern/beacon at the region's highest point, always lit | Hearth Gold, sized and simplified to survive a far-zoom or overview thumbnail | Far — must read at thumbnail scale, since its whole job is reassurance without opening a menu |

**Rules governing this layer**:

- **Additive only.** Every structure sits on top of the constant bones; none alters terrain or architecture silhouette. A region's mastery progress is legible purely by counting which structures are present.
- **Cumulative and simultaneous.** A fully-progressed region shows all four standing together — a glance at the Region View tells the player exactly which automation stages are unlocked there without opening a menu, directly satisfying Principle 3's design test.
- **Combat-suppressed like everything else.** If the player re-engages combat in a mastered region, these structures remain in the Arena background but drop to the same dimmed, static treatment as sigil-scars under 6.2.

*Scope note*: the bound creature's own idle "still working" animation is a Section 5 spec; this
section covers only the built structures the creature works *at*.

### 6.8 Procedural vs. Hand-Crafted: Recommendation

**Recommendation: region terrain, architecture, and sigil-scar crack-path geometry should be
100% hand-crafted, once per region, per the layer system in 6.5. Reserve any procedural
technique exclusively for non-mechanical decorative variance.**

**Why hand-crafted wins here, specifically**:

1. **A locked design rule makes procedural crack generation a compatibility problem, not a quality preference.** Section 3.3 requires the *exact same* crack path in both Unconquered and Mastered states. Procedural regeneration producing a different path per instance breaks that rule outright — this isn't "procedural might look worse," it's "procedural cannot satisfy this constraint without being constrained down to something no longer meaningfully procedural."
2. **Region identity (6.3) depends on curated, intentional application of the Source edge-quality grammar.** Procedural terrain typically needs heavy hand-authored constraint to avoid a generic, noisy result — paying procedural tooling costs while still doing hand-crafting labor.
3. **The mood targets (Section 2) are precisely art-directed, not statistically averaged.** "Beautiful danger" and "earned peace" are specific composed decisions per region — exactly what procedural variation dilutes.
4. **The volume doesn't justify the tooling cost.** Even a generous Full Vision roster is a curation problem, not a volume problem — and 6.5's per-region authoring list is efficient enough that hand-crafting stays comfortably in scope for a Large (12–24+ month) solo/small-team budget.

**Where light procedural technique is still worth using** (purely decorative, never touching
mechanically legible geometry):

- Scatter placement of non-mechanical debris/flora props within artist-painted bounds
- Ambient particle drift and idle-view flicker timing offsets
- Minor per-session randomization of which bound creature is shown mid-work-pulse in the Region View

*Design test*: before applying any procedural technique to environment art, ask whether it
touches the crack path, the bones, or any Source-identity color/light composition. If yes, it's
hand-crafted. If it's pure decorative scatter or ambient motion timing, procedural assistance is
fine.

*Status*: art-side recommendation. `technical-artist` and `producer` should confirm against
actual pipeline tooling and final region count when `/create-architecture` runs.

---

## 7. UI/HUD Visual Direction

This section was authored by `art-director` and `ux-designer` in parallel, then reconciled.
Three conflicts were surfaced and decided by the user; each is recorded inline below at the
point it applies.

*Scope note*: 3.4 locked the chrome/content **shape** split and 4.5 locked the chrome/content
**color** split. This section extends both into typography, iconography, motion, and the
diegetic/screen-space division. It does not re-derive them.

### 7.1 Typography Direction

**A deliberate hybrid: pixel display face + scalable data face.**

- **Ceremony/Display face — a true pixel bitmap font.** Slightly irregular stroke terminals, a "carved" quality echoing the stained-glass voice from Section 1. Used only at large sizes: screen titles (The Forge, Creature Roster), ability and ultimate names, rarity tier names, and headline text on the Mastery Transition summary and Forge Vow-binding confirmation. It never has to prove itself small, so it can spend personality freely.
- **UI/Data face — a clean, scalable (non-bitmap) font.** Used for all dense tabular content: loot filters, automation config, the creature roster, stat rolls, efficiency percentages, tuning values. **Tabular (fixed-width) numerals are mandatory** wherever numbers appear in a column, so a player can scan a column vertically without re-parsing each row. Digit shapes must stay unambiguous at small size (0/O, 1/l/I, 5/S, 8/B).

> **DECISION (user, reconciling art-director vs. ux-designer)**: The art-director's position
> was pixel bitmap fonts throughout, for total coherence with 6.4's clean-pixel rule. The
> ux-designer's objection: pixel bitmap fonts scale badly (non-integer scaling breaks pixel
> alignment), and the densest screens in the game — loot filters, automation config — are
> exactly where that fails the "text readable at minimum font size" and "UI scales correctly
> at all supported resolutions" accessibility gates. **Resolution: hybrid.** A scalable data
> face is accepted as the one sanctioned break from the all-pixel rendering rule, because
> dense tabular legibility and text-scaling accessibility outrank visual purity on screens
> the player revisits constantly.

**No italics anywhere.** Diagonal strokes fight the pixel grid in the display face and undercut
6.4's flat, controlled value-step discipline. Emphasis is carried by size, weight, or — where
the information is mechanically important — a glyph. Never by slanting letterforms.

**Size hierarchy** (relative, not fixed measurements): Ceremony Display > Screen Title >
Section/Column Label > Body/Data (the workhorse tier) > Micro/Caption. A Micro-tier number is
never the *sole* carrier of a mechanically important value — it always has a glyph doing primary
identification, with the number confirming. This is the Master Rule's "never the only channel"
logic applied to type scale.

*Design test*: if a number will be compared against other numbers in a list, it renders in the
UI/Data face with tabular figures. A display-face number in a data column is a scanning-speed
bug, not a style choice.

### 7.2 Iconography Style and the Glyph/Chrome Line

Icons are where the two languages sit physically closest — a Source medallion and a settings gear
can end up three pixels apart on the same panel. The line:

**The test**: *would this icon still mean the same thing bolted onto a completely different
game?* A gear means settings, an X means close, a caret means "more below" — pre-learned by every
player before they open this game. That is chrome. An open ring meaning "this is where you get
in," or a quatrefoil meaning "this is what this creature does" — that meaning exists *only*
because this game's glyph grammar taught it. That is content.

- **Chrome iconography** (settings gear, close X, scroll/sort arrows, checkbox, drag handle, search, pagination, drop-down caret, input prompts): flat, single-weight outline, drawn only in the chrome palette from 4.5 (Void Ink line, Bone Parchment fill, Cold Slate disabled). Never filled with a reserved color, never illustrated, never borrowing stained-glass linework.
- **Glyph-grammar iconography** (Source medallion, vulnerability ring, Ward seal, role badge, Vow scar, enchantment inlay, rarity frame): rendered exactly as 3.2/4.2 specify — filled color bounded by Void Ink leading, stained-glass quality intact. **Never** simplified into a flat monochrome icon-set version for menus.
- **A third category — established world-object silhouettes.** The automation-config screen needs to represent "which infrastructure stage is this" (6.7's ward-post, cairn, forge-stall, beacon). These are diegetic objects with a locked silhouette identity, so they appear in UI as small flat silhouettes of the *actual object* — never a generic gear-cog "automation" icon. This extends 3.4's "content reuses grammar unmodified" one step: it isn't only the 3.2 containers that qualify as reusable content-grammar, it's any asset the world has already taught the player to recognize.
- **Loot-filter logic icons** (include/exclude, AND/OR, threshold comparators) are bespoke to this game but carry no diegetic meaning — they are utility logic controls, and stay in chrome style. Loot filters are maximum-density utility and never carry ornament weight, no matter how custom an icon's meaning is.

*Design test*: if an icon represents a Source, a Role, a Vulnerability, a Ward state, or a real
placed world-object, it is content and must reuse an existing established asset. If covering it
with a generic icon library from any other game would lose no meaning, it is chrome.

*Serves*: the **Master Rule** — this is the exact seam where a "simplified icon set that means
the same thing but looks different" could quietly reintroduce the color-swap anti-pattern.

### 7.3 UI Animation Language

Motion is an ornament channel, exactly like frame decoration in 3.4: **it may flex with ceremony,
never with density.**

#### The combat animation scope — resolved

> **DECISION (user, reconciling art-director vs. ux-designer)**: Section 3.5's "only the
> vulnerability glyph animates" rule names only four rows — vulnerability glyph, creature
> silhouette, Source glyphs/seams, arena background. It describes the **diegetic combat stage**
> and is silent on the HUD, because the HUD is this section's job. The art-director proposed
> extending the monopoly frame-wide, with Vow state swapping silently between two static shapes.
> The ux-designer objected that a build-critical state flip, rendered as a silent swap on a belt
> charm at 10–15% frame height, is likely to be missed entirely — and a Vow whose condition is
> unmet means the build does nothing.
>
> **Resolution: 3.5's animation monopoly is scoped to the diegetic stage.** The HUD overlay tier
> gets its own narrow motion budget, governed by one rule: **continuous, rhythmic, looping motion
> remains the vulnerability glyph's exclusive signature. HUD elements may only play a single,
> discrete, one-shot beat on a state transition — never a loop.** The two are perceptually
> distinct and players read the difference reliably.

**In combat**:

- **Meters (health, Resonance) render as discrete stepped/segmented fills, never a smooth analog tween.** A segment extinguishing is an instantaneous state swap, not sustained motion — this satisfies the hierarchy and 6.4's flat-fill rule in a single stroke.
- **Vow condition flips get one discrete flash** on the belt mark when the condition changes state — a single beat, no loop, no pulse.
- **Nothing in the HUD exceeds the vulnerability glyph's peak brightness**, flash included. The brightness ceiling from 5.1 remains absolute and frame-wide even though the *animation* rule is now stage-scoped.

**Outside combat** — no hierarchy to protect, so motion carries meaning again, governed by
ceremony tier (7.6):

- **Utility screens** (loot filters, automation config, roster): motion is purely functional — instant or near-instant state changes on scroll, sort, toggle. No lingering transitions, no flourish. These screens are revisited constantly; motion here is friction, not delight. Easing: fast linear or a slight ease-out, nothing bouncier.
- **Standard menu transitions**: fast slide or fade, ease-out on entry, ease-in on exit. No overshoot, no elastic bounce — 5.5's "grounded expressive, not cartoon" discipline extended from creatures to chrome.
- **Ceremony screens** (Forge Vow-binding, Mastery Transition summary): motion may carry emotional weight, and a small overshoot/settle is permitted here specifically. The Vow-binding button can play the item's own molten-to-cooled material transition (Section 2) directly on the confirmation UI before commit, rather than being a flat button that cuts to a separate cinematic.
- **Content glyphs animate only when the motion *is* the mechanical signal.** Work-loop pulses (5.3) must animate in the Region View and Roster, because the motion is the "is this creature working" information. A Source medallion in a tooltip stays static — its job is identification, not live status.

*Design test*: before animating any UI element, ask what information the motion itself carries. If
the answer is "none, it just looks nice," cut it on a utility screen; downgrade it to a single
settle-beat at most on a ceremony screen.

### 7.4 Diegetic vs. Screen-Space Information

**Diegetic-first, tiered by how much precision the information demands.** Where a state is
categorical and coarse (is this available; is this creature working; which automation stage is
this), it belongs on the world or the body. Where it is a precise magnitude a player times a
decision against (exact seconds remaining, exact HP), a small quiet screen-space number backs it
up. The diegetic cue is the always-visible primary signal; the chrome number is a precision
backup — never the reverse.

**6.7 and 5.3 substantially shrink the HUD's job outside combat.** The Idle/Automated Region View
needs almost no status UI: automation stage is legible by counting standing infrastructure (6.7),
and per-creature health (Healthy/Blocked/Starved) is legible from pose and pulse cadence alone
(5.3). What remains is navigation and, at most, an aggregate yield readout. This is the clearest
evidence in the bible that Principle 3 ("mastery is drawn onto the world, not just tracked in a
menu") pays off in **UI design cost**, not only emotion.

**Where screen-space chrome remains correct**: exact HP and exact cooldown seconds (precision that
changes a split-second decision), and the entirety of the pure-management screens (Forge, Roster,
Automation Config, Loot Filters) — abstracted planning interfaces with no "world" to be diegetic
in. Chrome-heavy UI is the right tool there.

*Design test*: before adding a screen-space HUD element, ask whether the creature, the Hunter's
body, or standing world infrastructure could carry the same information as a coarse signal — and
if so, whether a chrome readout is still needed *only* for precision, not for the primary read.

### 7.5 Combat HUD Specification

The full constraint: health, Resonance, three ability cooldowns, one ultimate, live Vow-condition
state, and part-break progress — inside a frame where the vulnerability glyph must remain the
loudest shape.

| HUD Need | Primary Channel | Where It Lives | Motion Discipline |
|---|---|---|---|
| Health | Screen-space segmented pip-bar (a continuous magnitude, not a categorical state — the Master Rule targets categories, so a pip display is correct here, not a glyph) | Small chrome cluster, screen corner | Discrete pip loss, no tween |
| Resonance | Screen-space, but the meter container **reuses the Source-medallion closed-ring shape** — a partial radial fill inside a fixed glyph container, like a gauge inside a vessel | Same chrome cluster | Segmented wedge-fill, no smooth sweep. Rendered in chrome-neutral value, not a Source hue |
| 3 ability cooldowns | **Diegetic** — a small stepped pip-track beside (not replacing) the Vow-scar belt charm from 5.2 | Hunter's belt, anchored to the existing charm zone | Discrete pip loss/gain only; the Vow-scar brand itself is untouched, preserving 5.2's permanence rule |
| Ultimate cooldown | Same pip device, larger scale to reflect slot importance | Same belt zone | Same discrete pip logic. **Does not earn Hearth Gold** — per 4.3, that color is reserved for boss-fall / Vow-bind / Legendary-drop events. Scale marks importance, not color |
| **Vow condition** (satisfied / violated) | **Diegetic** — a closed-ring (satisfied) / open-ring (violated) companion mark beside the belt charm, reusing 3.2's container logic (closed = stable, open = exposed). Shape-driven, never color-only | Same belt zone | **One discrete flash on the flip** (per the 7.3 decision), then static. Never a loop |
| Part-break progress | **Diegetic** — crack/value escalation directly on the creature's own part-seam (3.1). **No HUD element at all** | On the creature | Escalating value shift; the break moment is a discrete one-time snap |

**Part-break deserves a note**: both the art-director and the ux-designer independently concluded
it should carry no HUD widgets. It reuses 3.3's crack grammar running in reverse — there the crack
only *closes*; here it only *opens* — costs zero HUD real estate, and is exactly what the Master
Rule argues for: *read the world, don't just watch it*. Removing it drops the glanceable combat
load to roughly four chunks (vulnerability glyph, HP band, ability-readiness cluster, Vow-state
cluster), which is defensible against working-memory limits under load. With it as a HUD array,
the frame is over budget and the "coiled, never frenetic" mood target (Section 2) cannot hold.

**Net HUD footprint**: a small corner cluster (health pips, Resonance medallion, optional numeric
backups) plus the belt-charm zone the Hunter already carries for unrelated reasons (5.2). **No
bottom-spanning ability bar, no separate cooldown icon row.** The kit lives on the body it belongs
to. That reduction in HUD *area* — not just brightness — is itself a defense of 3.5's hierarchy:
less chrome for the eye to filter past on the way to the vulnerability glyph.

*Open flag (playtesting)*: the belt charm sits at 10–15% of frame height (5.1). Whether the
pip-track and ring-toggle stay legible at that scale in real combat cannot be settled on paper.
**Pre-authorized fallback**: mirror the same pip/ring states in the corner chrome cluster at larger
scale — same shape grammar, more pixels, not a redesign.

*Design test*: freeze all motion and cover the entire HUD in gray — a player must still tell
"ability ready," "Vow currently violated," and "this part is about to break" apart from each other
by shape and position alone.

### 7.6 Ornamentation Tier by Screen

| Screen | Ceremony Tier | Why |
|---|---|---|
| Combat HUD | **None** | 3.5's hierarchy makes any frame ornament here a direct tax on vulnerability-glyph legibility. What richness exists is diegetic (belt, creature), not frame decoration |
| The Forge — general workspace | **Medium** | Section 2's "intimate, tactile, deliberate" mood earns frame richness beyond bare utility, but it's a repeated multi-slot working interface, not a one-off |
| The Forge — Vow-binding confirmation | **High** | 3.4's own named example. A one-off commitment beat; heavier ornament and ceremony-tier motion both licensed |
| Creature roster / management | **None** (chrome) | Dense tabular content — Section 2's "glyph ledger." Richness lives entirely in content (Source medallions, role badges per row), never in frame ornament |
| Automation config | **None** | Named in 3.4 as high-density utility that never flexes, regardless of how important the decision being made there is |
| Loot filters | **None** | Same — named directly in 3.4 |
| Region overview / idle view | **Medium** | Neither a one-off beat nor a dense grid. Section 2's "quiet pride" earns modest frame warmth, but most richness is diegetic (6.5–6.7 environment, 5.3 work-loops) |
| Mastery Transition summary | **High** (ceiling case) | The single highest-ceremony screen in the game. Full expressive range of ceremony-tier motion and ornament, matching the coronation-not-shutdown mood |

*Design test*: if a screen's importance is being expressed through frame ornament rather than
through its glyph content or its one-off status, re-check it against this table — importance is
carried by content and rarity of occurrence, never by decoration density.

### 7.7 Interaction States and Accessibility Requirements

Section 4.6 handled colorblindness thoroughly. These are the axes it did not cover, raised by the
`ux-designer` pass and adopted here as binding requirements.

#### Chrome interaction-state matrix

4.5 restricts chrome to three neutral colors, but dense screens need at least five distinguishable
control states: **hover, keyboard-focus, selected, disabled, drag-preview/drop-target.** These must
be built from **value steps and border weight** (extending 3.1's fixed-value-step seam precedent),
not from additional hues. A **visible keyboard-focus indicator is mandatory** (WCAG 2.4.7) and must
survive the three-color budget on its own — it is not an implementation afterthought.

#### Keyboard and gamepad targeting — cycle-and-confirm

> **DECISION (user)**: Gamepad support is **not** stick-cursor emulation. Stick-cursor would turn
> "click the weak point you correctly read" into "steer a low-precision analog pointer onto a small
> target" — reintroducing execution difficulty through *input* the same way an unreadable glyph
> would reintroduce it through *art*. Same Pillar 1 violation, different door.
>
> **Combat targeting uses a discrete cycle-and-confirm model**: bumper/D-pad (or keyboard) cycles
> through currently-valid part targets; a confirm button commits. This sidesteps precision-pointing
> entirely and doubles as the motor-accessibility path for keyboard-only and reduced-precision-mouse
> players. It requires its own **selected-target rendering state** — a shape-based selection ring on
> the currently-cycled part, distinct from the vulnerability glyph itself.
>
> *Action*: `.claude/docs/technical-preferences.md` gamepad support should be revised from
> "Partial" to reflect this commitment. Full flow design belongs to `/ux-design`.

#### Motor accessibility

- **Click-hitboxes are padded generously beyond the visual seam boundary.** 3.1 deliberately varies part size and proportion for legibility — but by Fitts's Law, small and adjacent parts are measurably harder to acquire for players with tremor or limited fine motor control. Hitbox padding is an engineering-layer fix that costs nothing visually and is **required regardless of any other decision**.
- An optional aim-assist / target-magnetism setting is recommended.
- **Any drag-to-reorder (loot filter priority) or drag-to-assign (creature-to-slot) pattern requires a non-drag keyboard equivalent** — a numeric priority field, or explicit move-up/move-down/assign-via-dropdown controls. Drag-and-drop alone fails the keyboard-only gate. This constrains what chrome must render (visible priority numbers or move buttons alongside any drag handle), so it is locked now rather than left to implementation.

#### Motion sensitivity

- **A minimum telegraph wind-up duration is fixed as a floor, regardless of region difficulty scaling.** Game difficulty raises telegraph speed and density — this floor guarantees no region ever crosses into strobe territory (the WCAG 2.3.1 three-flashes-per-second threshold).
- **A redundant audio pre-cue** on the same slow-build/hard-cutoff curve, offered as an accessibility assist (not a balance change), for players who process fast visual change poorly.
- A **reduced-motion option** must exist. The telegraph pulse is the core mechanic and cannot simply be disabled, but ambient/decorative motion (particle drift, idle-view flicker, menu transitions) must be suppressible independently.

#### Low vision and text scaling

- **HUD-tier glyphs need an independent minimum on-screen size floor.** 5.6's 12–16px minimum is a *world-space, native-canvas* rule tied to creature sprite size — it does **not** transfer to UI-space, which scales with resolution and window size. Without a separate UI-space floor, a compliant world-space guarantee can still ship a HUD that shrinks below legibility at high resolutions.
- A **UI-scale setting** is required, as is a high-contrast mode. The hybrid typography decision in 7.1 exists specifically so the dense screens can satisfy this.

#### Cognitive load

Pillar 1 explicitly rejects reflex-testing in favor of reading-skill, which means this game's
fairness thesis is *already* aligned with slower-processing players in principle. A HUD-density
reduction option, and/or a low-stakes practice mode for learning telegraph patterns without full
combat consequences, would make that principle real rather than incidental. Recommended, not
mandatory — flagged for `/ux-design`.

---

## 8. Asset Standards

Authored by `art-director` (preferences) and `technical-artist` (MonoGame constraints) in
parallel, then reconciled. Engine: **MonoGame 3.8.4.1**, C#/.NET 8+, PC, 60 FPS / 16.6 ms.

> **Headline finding**: implemented naively — every animation state baked as sprite frames,
> every environment lighting pass baked as duplicate art — this game's asset load lands at
> roughly **700 MB** of texture memory and **~1,230+ hand-drawn creature frames**. Implemented
> per the techniques below, it lands at roughly **125–160 MB** and a fraction of the art labor.
> The art bible describes the right *what*; this section records the *how* that makes it
> affordable. **The techniques in 8.4–8.6 are not optimizations — they are the plan.**

### 8.1 Naming Convention

Pattern: `[category]_[subject]_[descriptor]_[size].[ext]`

| Prefix | Covers |
|---|---|
| `crea` | Creature combat sprite (multi-part, shared rig) |
| `crport` | Creature roster portrait (separate authored asset, 5.6) |
| `char` | Hunter assets |
| `glyph` | Glyph-grammar assets (all six containers + rarity frames) |
| `env` | Environment layers (per 6.5's layer list) |
| `infra` | Automation infrastructure objects (6.7) |
| `ui` | UI chrome (panels, buttons, chrome icons) |
| `vfx` | Particle/effect sprites |

**Creatures** — `crea_[source]_[role]_[variant]_[tier].png`
- `source` ∈ `body | mind | nature | machine | shadow | spirit`
- `role` ∈ `atk | def | sup | crf | prd`
- `variant` — 2-digit index within a Source×Role cell (`01`, `02`…)
- `tier` ∈ `std | boss`. Bosses replace `variant` with a name-slug.

Examples: `crea_nature_def_01_std.png`, `crea_shadow_atk_hollowmaw_boss.png`

**Rig parts** — `crea_[source]_[role]_[variant]_part-[name]_[angle].png`, where `angle` is the
snapped rotation index (see 8.5). **Roster portraits** — `crport_[source]_[role]_[variant]_portrait.png`,
authored independently, never a crop of the combat sheet.

**Hunter** — `char_hunter_[part-or-state]_[variant].png`; Vow scars as
`char_hunter_vow-scar_[abilityid]_belt.png`; enchantment inlays as
`char_hunter_enchant-inlay_[enchantid]_[slot].png`

**Glyphs** — `glyph_[container]_[key]_[size].png`, `container` ∈
`medallion | role | vulnring | wardseal | vowscar | enchantinlay | rarity`
Examples: `glyph_medallion_nature_h32.png`, `glyph_vulnring_generic_h16.png`

**Environment** — `env_[regionid]_[layer]_[state]_[composition].png`, `layer` ∈
`bones | scarmask | scar | plinth | sweep`, `state` ∈ `unconquered | mastered` (omitted for
state-invariant layers), `composition` ∈ `arena | view`. **Note**: there is no `grade` layer file —
lighting/color-grade is a runtime shader (8.6), not baked art.

**Infrastructure** — `infra_[stage]_base.png` + `infra_[stage]_[source].png`, `stage` ∈
`wardpost | cairn | forgestall | beacon`. The beacon additionally needs a separately
hand-simplified `infra_beacon_[source]_thumb.png` for region-select thumbnails (6.7) — never a
downscale of the full asset.

*Design test*: given only a filename, an artist should state the asset's Source/Role/state without
opening it.

### 8.2 Sprite Resolution Tiers

**Governing rule**: every category renders at the *same effective pixel density* on screen. Mixed
"big pixel" sizes read as several pixel-art styles stitched together — a direct violation of 6.4.

| Category | Native size |
|---|---|
| Standard creature | 96–160px height (locked, 5.6) |
| Region boss | 192–320px height (locked, 5.6) |
| Roster portrait | 96×96px canvas |
| Hunter — combat rig | 48px height, single master canvas (never runtime-rescaled — see 8.6) |
| Hunter — belt Vow-condition ring | 16px (respects the 12–16px container floor even at belt scale) |
| Hunter — Vow-scar brand / ability pip | 8px / 6px (not containers, so the floor doesn't apply) |
| Glyph — micro / standard / display | 16px / 32px / 64px (clean ×2 steps for lossless nearest-neighbor scaling) |
| **Arena virtual canvas** | **480×270** — 16:9, integer-scales to 1080p (×4) and 4K (×8) |
| Region View canvas | Wider variant, same vertical pixel density |
| Infra — ward-post / cairn / forge-stall / beacon | ~48–64 / ~32–48 / ~64–96 / ~128–192px height |
| Infra — beacon thumbnail | ~24px, separately hand-simplified silhouette |
| UI chrome icon | 16px or 24px (matched to glyph micro/standard so chrome and content icons don't clash in size when adjacent — 7.2) |
| HUD health pip / Resonance segment | 8–12px per segment |
| VFX — ambient particle | 4–8px |
| VFX — mastery sweep | Full Region View canvas (one-off ceremony overlay) |

**If the canvas and the creature ever conflict, the canvas yields.** 5.6's "the canvas grows,
the minimums never shrink" extends to the frame itself.

### 8.3 Per-Sprite Color Budget

**Hard cap: 10 unique colors per creature**, split across two ramps that never blend (5.6):

| Ramp | Colors | Contents |
|---|---|---|
| **Body-fill** (baked into the creature texture) | 6–7 | 4-step Source ramp (highlight / base-mid / shadow / deep-shadow of the creature's assigned Source hex) + 1 Void Ink seam (`#1B1620`, fixed across every creature) + 1–2 neutral rim-light |
| **Glyph-accent** (separate file, composited at runtime) | 2–3 | Live-state accent — Ember Threat *or* Hearth Gold, never both baked at once (a creature is hostile or bound, never simultaneously, per 5.4): container color + interior-mark line |

Rarity aura rings (5.4, post-MVP) are excluded from the cap — they reuse an existing rarity hex in
their own overlay file and never touch the body texture.

**Why a hard cap**: 6.4 requires flat, controlled value steps and forbids painterly blending. A
countable color budget is the literal mechanism that enforces that at the pixel level. Every extra
color is either a genuine new value step (defensible, rare) or the first crack toward a gradient
blur that erodes the seam-line legibility 3.1's whole targeting system depends on. Ten is generous
enough that a 4-step ramp reads as modeled form, tight enough that an artist can name every color's
job on request.

*Design test*: open the palette. If any color's purpose can't be stated as "Source ramp step N,"
"seam," "rim," or "accent," it shouldn't be there.

**Format**: creature and glyph sprites export as **8-bit indexed PNG**, so the cap is an auditable
file property rather than a discipline that silently erodes over production.

### 8.4 Animation Frame Budgets

**Base pose library — ~6 key poses hand-authored once per creature**, reused everywhere: rest/idle,
wind-up/anticipation, peak/action, recovery/follow-through, hit-reaction, break/damaged.

| Role | Loop length | Playback | Cycle time |
|---|---|---|---|
| Crafter | 8 frames | 12 fps | ~0.67s |
| Attacker / Support | 10 frames | 10 fps | ~1.0s |
| Defender | 8 frames | 6 fps | ~1.33s |
| Producer | 12 frames | 4 fps | ~3.0s |

**How the rest of the state matrix is covered with no new hand-drawn frames:**

| State | Cost | Technique |
|---|---|---|
| **Hostile vs. Bound performance sets** | **Zero new frames** | Identical key poses, retimed with different easing curves (sharp/sudden vs. softened). Section 5.5's "separate identity from performance" is the single largest production economy in this bible — it is load-bearing guidance, not a mood note |
| **Blocked work-state** | **Zero new frames** | The Healthy loop *held* on its pre-work-tick frame (5.3) — a playback state, not art |
| **Starved work-state** | ~4 frames | One additive secondary layer per role, over the idle pose |
| **Telegraph wind-up** | **Zero new body frames** | Reuses the anticipation pose; brightness/hue driven on the *glyph layer* by shader (8.5), with duration as a tunable data value, not a fixed frame count |
| **Part-break** | 1–2 overlay stages | **A crack decal composited over existing pose frames** — reusing 3.3's crack grammar at creature scale. **Never a baked full-body variant**: baking 3 stages × ~4 parts × 41 frames is a ~12× multiplier and makes the budget unaffordable. This is mandatory, not preferred |
| **Mastery Transition glyph inversion** | 1–2 glyph frames | Creature holds its final combat pose; only the glyph-accent overlay animates. The light-sweep VFX carries the rest of the beat |
| **Additive motion layers** (glyph pulse, twitch, breath) | 2–4 frames each | Authored once per creature, layered onto every state that needs them |

**Budget: ~25–40 unique hand-authored frames per standard creature, ~50–60 per boss** (larger canvas,
more parts, phase-transition poses). Across a 6-Source × 5-Role matrix, keeping this tight has a large
fleet-wide multiplier.

*Design test*: if a new state can't be produced from the 6-pose library via retiming or an additive
layer, redefine the state against 5.3/5.4/5.5's vocabulary before defaulting to a new hand-drawn set.

### 8.5 Animation System, Compositing, and Batching

> **DECISION (user)**: **Homebrew cutout rig with angle-snapped rotation.**
>
> MonoGame ships no animation or rigging system. The three options were sprite sheets (native,
> perfect fidelity, but ~1,230+ hand-drawn frames across the roster — a lot of art labor for a
> solo/small team), Spine (industry-standard skeletal, official `spine-monogame` runtime, but a
> commercial editor license), or a hand-rolled cutout rig.
>
> **Chosen: a hand-rolled parent-child bone hierarchy on vanilla `SpriteBatch`** — which already
> supports per-draw rotation, scale, and origin. A `Bone` carries a local transform; a `Part`
> carries a texture region + pivot; a recursive draw walk applies parent transforms.
> `MonoGame.Extended.Tweening` drives the curve evaluation. This is a scoped systems task, not
> "build an engine," and it carries no licensing or dependency-maintenance risk.
>
> **⚠ CORRECTED 2026-07-14 — the rotation problem this section predicted DOES NOT OCCUR.**
> The spike was built and reviewed in-game. See `docs/architecture/ADR-002`.
>
> ~~**The pixel-art rotation problem, and the mitigation.** Skeletal animation is a continuous-transform
> technique. Rotating hard-edge pixel art to a non-cardinal angle produces either filtered blur
> (linear sampling) or stair-stepping (point sampling) — either way it fights 6.4's clean-pixel
> mandate and 3.1's fixed 1–2px seam weight.~~
>
> **What the spike actually found.** The Attacker's strike-and-recover cycle — the widest motion arc in
> the game (5.3), and by this section's own reasoning the worst case — was rendered with real pixel-art
> limbs (flat fill, unbroken 1px contour, internal seam lines) and reviewed at **4× zoom** under point
> sampling, in **both** render paths, including the pixel-perfect 480×270-canvas path where
> re-quantization onto the coarse grid should be worst. **Continuous rotation showed no blur, no
> stair-step artifacting, and no edge crawl in either path.** Point-sampled rotation at this game's
> sprite scale (~20×6 px limbs) simply looks clean. The premise above was wrong.
>
> ~~**Mitigation (mandatory): discrete angle-snapped part-sprite swaps.** Each rotating part is authored
> at a small set of fixed angle variants and the rig swaps between them rather than rotating
> continuously. This reintroduces a *small* per-part multiplier.~~
>
> **This mitigation is withdrawn, and with it its texture cost.** It required authoring every rotating
> part at N fixed angle variants — a real per-part texture multiplier. Nothing needed mitigating, so
> **no angle variants are authored at all**: a part is one sprite, rotated at runtime. The per-part
> multiplier this section introduced is **removed from the texture budget entirely.**
>
> **Angle-snapping is nevertheless RETAINED — as an art-direction choice.** Reviewed side by side, the
> snapped arm reads as deliberate, stepped, stop-motion-like movement and was preferred on feel.
> Implementation is `SnapSteps = 16` (22.5° increments), applied to the *rotation angle at draw time* on
> a single sprite — not to a library of pre-authored variants. Same look, zero texture cost.
>
> **This is now a knob, not a constraint.** Nothing breaks without it. The step count may be tuned per
> creature, or snapping disabled entirely, with no correctness consequence. Do not "fix" the stepping as
> though it were a bug — it is deliberate.
>
> *Status*: **ADR-002, Accepted.** The spike is permanently available: run the game and press **Tab**.

#### Palette-layer separation — implementation

> **DECISION (user)**: **Baked RGBA body-fill layers, with a narrow palette shader on the glyph layer
> only.**
>
> The body sprite and the glyph-accent (vulnerability ring / ward seal) are **separate PNG files**,
> composited at runtime at a fixed anchor defined in the creature's metadata. This makes "separate
> ramps that never blend" (5.6) a literal fact about the files, not a painting discipline. It also
> makes the hostile→bound swap a simple sprite-swap — draw the ward-seal file instead of the
> vuln-ring file — with no shader work.
>
> A narrow **HLSL palette-lookup shader applies to the glyph layer only**, where two confirmed
> runtime color shifts genuinely require it: the telegraph hue-drift toward the attacker's Source
> color (4.3), and the Starved-state Ember Threat flicker bleed (5.3). Both would otherwise need
> baked frames per brightness/hue step.
>
> A full-body indexed-palette shader was considered — it would cut creature texture memory roughly
> 4× (1 byte/px vs 4) — but was declined as a pipeline and authoring change not justified by any
> confirmed gameplay need. Source colors (4.2) are fixed per Source, not swappable. Revisit only if
> texture memory becomes a measured problem.

#### Batching rules (MonoGame SpriteBatch)

`SpriteBatch` merges *consecutive* `Draw()` calls sharing the same `Texture2D` into a single GPU draw
call. Batching efficiency is driven by **texture-switch count in submission order**, not sprite count.
An extra `Draw()` sharing a texture costs a few dozen bytes appended to a vertex buffer — **not** a
state change. What forces a flush: a different bound texture, a different `BlendState`/`SamplerState`/
`Effect` (all set per-`Begin()`, not per-`Draw()`), `End()`, or `SpriteSortMode.Immediate`.

**Consequences for this game's systems:**

- **The glyph overlay layer is effectively free** — provided all glyphs live in **one shared atlas** reused by every creature and drawn contiguously. Never bake glyphs into body sprites: baking would break palette-layer separation (5.6) and make the telegraph hue-drift impossible without re-baking frames.
- **Never switch `Effect` per creature.** Use one shared palette shader for the entire creature pass, and pass per-creature selection via the per-`Draw()` **Color tint parameter** (a free per-vertex attribute). Switching Effects per creature would mean 40+ flush points in a busy Region View.
- **One creature atlas per active region** (that region's roster + boss), loaded and unloaded with the region. Never per-creature textures. One small, permanently-resident shared glyph atlas.
- **The idle Region View is fine at scale.** Dozens of simultaneously animating creatures collapse to a handful of GPU draw calls given shared atlases and a shared Effect. The real ceiling is UX legibility (how many creatures a player can usefully parse), not batching.
- **Frame structure**: ~3–5 `Begin()`/`End()` passes total (environment → creatures+glyphs → HUD). Never use `SpriteSortMode.Immediate` outside debugging.

### 8.6 Formats, Pipeline, and Export

**Texture format: RGBA8 uncompressed** (or 8-bit indexed PNG source). **BC/DXT block compression is
forbidden on any creature, glyph, or UI-content art** — it works in 4×4 blocks with interpolated
colors and reliably produces bleed and banding on exactly the hard flat-fill edges (seams, glyph
containers, value-step boundaries) that Sections 3.1, 3.2, and 6.4 make load-bearing. Compression is
acceptable only for decorative layers (ambient particles, background haze) where edge softening
threatens no mechanical read.

**Export rules — non-negotiable against 6.4:**

- **Point/nearest-neighbor filtering only** (`SamplerState.PointClamp`). No bilinear, no anisotropic. Any smoothing undoes the crisp seams (3.1) and glyph edges (3.2) the legibility model rests on.
- **Integer scaling only.** ×1, ×2, ×3 of native. Non-integer scaling (×1.5) forces sub-pixel sampling and reintroduces blur *regardless of filter mode*. This is why the Hunter's frame-presence ratios (5.1) are an authoring target, not a runtime resize.
- **No mipmaps on pixel art.** Mipmapping blends adjacent pixels on minification and will visibly soften glyph containers and seams below native scale.
- **Straight (non-premultiplied) alpha**, unless the render pipeline is confirmed premultiplied end-to-end. A mismatch produces dark or light fringing exactly on the edges this bible has repeatedly made load-bearing. Glyph overlays composite over many different Source body colors — this must be clean.
- **sRGB, no embedded color profiles.** Section 4's palette is specified as exact hex and must arrive on screen as that exact hex.
- **2–4px transparent padding** between packed atlas sprites, as insurance against edge bleed.
- **Consistent anchor convention**: creatures anchor at ground contact; glyph and motion overlays anchor at a body-relative point in the creature's metadata, authored identically across every creature so runtime compositing needs no per-creature special-casing.

**Environment lighting — runtime shader, not baked art.**

> The 2× lighting/color-grade passes in 6.5 are implemented as a **runtime LUT-based color-grade
> shader**, not duplicate painted art. Baked duplicates cost roughly **480 MB** across a full region
> roster; a LUT is a few KB. And a LUT preserves clean-pixel discipline perfectly — it is a per-pixel
> color *substitution*, not a spatial filter.
>
> The **scar-treatment skins remain genuinely baked, doubled art** (6.5 already scopes them as such) —
> they are localized to crack and ward-stone regions, not full-screen, so the doubling is cheap.
>
> The **transition sweep** is one `.fx` pixel shader: sample the shared crack mask, blend the two
> treatments by a `transitionProgress` uniform, and drive an outward-traveling wavefront from a
> boss-position-relative radial gradient (Section 2's "expanding outward/upward"). Active only during
> the cinematic. This literally implements "the crack never moves, it only closes" — one mask, two
> states, one animated blend parameter.
>
> **Bloom/glow** (Hearth Gold ward-glow, resonance halo, telegraph accent-spike): render bright regions
> to a smaller `RenderTarget2D`, blur, composite back additively. Stylistically correct here — the
> bible's own language wants a soft halo — provided the blur stays confined to the additive glow layer
> and never touches the crisp base art.

### 8.7 Approved Dependencies

> **DECISION (user)**: library choices made here rather than deferred. Each should still be recorded
> as a formal ADR during `/create-architecture`, and added to `.claude/docs/technical-preferences.md`
> under Allowed Libraries as it is actually integrated.

| Gap | Decision | Rationale |
|---|---|---|
| **UI framework** (Forge, roster, automation config, loot filters, HUD) | **Myra** | MonoGame ships no UI framework. Myra is mature, actively maintained, and — decisively — has real grid/list/property-grid primitives, which is exactly what the dense tabular screens (3.4, 7.6) need. Gum is the alternative; hand-rolling a data grid is not a good use of the budget |
| **Scalable font** (7.1's UI/Data face) | **FontStashSharp** | `SpriteFont` is bitmap-only. 7.1's hybrid-typography decision makes a dynamic TTF rasterizer a hard requirement, not an option. FontStashSharp is actively maintained and standard in MonoGame projects |
| **Atlas packing** | **`MonoGame.Aseprite`** (if creature art is authored in Aseprite, the likely pixel-art tool), else a **custom MGCB content-pipeline extension** | MonoGame has no built-in atlasing. `MonoGame.Aseprite` imports `.aseprite` files directly — frames, tags, slices — removing an export step entirely. A custom MGCB extension is the no-dependency fallback and gives full control over format |
| **Tweening / curve evaluation** (drives the homebrew rig) | **`MonoGame.Extended.Tweening`** | Needed by the 8.5 rig decision. `MonoGame.Extended` is worth evaluating as a single dependency covering several gaps (camera, particles, atlas importer) rather than picking one-off libraries per gap |
| **Tilemaps** | **Not needed** | Regions are hand-painted Arena and Region-View compositions (6.1), not tile-based traversal |
| **Skeletal animation library** | **Not adopted** | Superseded by the homebrew cutout rig decision in 8.5 |

### 8.8 Hard Limits

| Limit | Value |
|---|---|
| Max atlas texture dimension | 4096×4096 (8192 permissible pending min-spec confirmation). Power-of-two is **not** required on this target |
| Texture format | RGBA8 uncompressed / 8-bit indexed. **No BC/DXT on content art** |
| Colors per creature | **10** (8.3) |
| Frames per animation state | Loop states 6–12; action states 6–10; hit reactions 2–4 |
| Creature texture budget | ~19–42 MB (30 standard creatures) |
| Boss texture budget | ~23 MB (6 bosses) |
| Environment texture budget | ~40–60 MB (shader-based lighting — 8.6) |
| UI chrome + glyph atlas | ~20 MB combined |
| **Total resident texture memory** | **512 MB ceiling** (working estimate ~125–160 MB — generous headroom for roster growth) |
| Draw-call planning budget | ~10–20 `Begin()`/`End()` boundaries per frame; low hundreds of GPU draw calls |
| Max simultaneous animating creatures (idle view) | No hard engine ceiling given correct batching (8.5). Soft planning ceiling ~100–150 before per-entity curve evaluation is worth profiling. **The real ceiling is UX legibility, not the engine** |

### 8.9 Asset Review Checklist

**Universal**
- [ ] Cover all color in gray — does every *mechanically important* distinction still read? (Master Rule)
- [ ] Every color justifiable as one of the 6 core roles, or a Source/rarity shade (4.1–4.3)? No new decorative hue
- [ ] Any new semantic color checked against the colorblind-risk table (4.6), with a named non-color backup cue?
- [ ] Indexed palette count within the 8.3 cap?

**Creature**
- [ ] At final in-game scale, can a first-time viewer count clickable parts and locate the active vulnerability glyph within one second? (5.6)
- [ ] Body-fill and glyph-accent in genuinely separate files — never blended into one gradient? (5.6, 8.5)
- [ ] Role legible from mass distribution, Source from edge quality, independent of color? (3.1)
- [ ] Seams at fixed Void Ink value-step, 1–2px leading weight at this canvas size? (3.1)
- [ ] Roster portrait separately authored — not a shrunk combat sprite? (5.6)
- [ ] Hostile and bound animation sets, paused on one frame each — distinguishable by pose alone? (5.5)
- [ ] Healthy / Blocked / Starved distinguishable by motion alone with the glyph grayed? (5.3)
- [ ] Any rotating rig part authored at snapped angle variants, not continuously rotated? (8.5)

**Glyph**
- [ ] Nameable by container shape alone, before the interior mark is examined? (3.2)
- [ ] Interior mark 3–5 strokes maximum? (3.2)
- [ ] Container meets the 12–16px minimum at its smallest deployed size? (5.6)

**Hunter**
- [ ] Nothing on the Hunter exceeds the vulnerability glyph's peak brightness? (5.1)
- [ ] Gray-test: Vow presence still legible from belt density; Vow vs. enchantment still legible from container shape (crack-line vs. diamond)? (5.2)

**Environment**
- [ ] Explicable as "the same location, differently framed" between Arena and Region View? (6.1)
- [ ] Arena assets: no extra animated ornament, no conquest-state wash, no bloomed scar, no mid-encounter geometry change? (6.2)
- [ ] Sigil-scar crack path **pixel-identical** between unconquered and mastered treatments? (6.5)
- [ ] Every region-identity detail traceable to the dominant Source's edge-quality/color entry or the shared ward-architecture? (6.3)
- [ ] Dithering only on non-mechanical gradient fields — never on a crack line, seam, or leading edge? (6.4)

**UI**
- [ ] Anything representing Source/Role/Vulnerability/Ward/a world-object reuses the existing asset unmodified — not a bespoke icon? (7.2)
- [ ] Chrome stays within the 3-color neutral palette and carries no stained-glass ornament? (4.5, 3.4)
- [ ] Any number compared in a list rendered in the UI/Data face with tabular figures? (7.1)
- [ ] Freeze motion, gray the HUD — "ability ready," "Vow violated," "part about to break" still distinguishable by shape and position? (7.5)
- [ ] Keyboard-focus indicator present; every drag interaction has a keyboard equivalent? (7.7)

---

## 9. Reference Direction

Every other section in this bible was derived forward, from concept and pillars outward. This section
runs the process in reverse: no reference games, films, or artists were supplied at the start, so
nothing here was a starting influence — each entry was selected *after* the system existed, because it
demonstrates (or nearly demonstrates) a technique this bible already committed to.

That ordering matters for how these are used. **A reference here is not license to import its whole
visual identity, only the one specific mechanism named under "what to take." The "what to avoid" line
is exactly as binding as the "take" line** — its job is to stop a well-meaning artist from quietly
re-deriving the reference's *entire* look instead of the one thing it was cited for.

### 9.1 Gothic Stained Glass (Sainte-Chapelle, Chartres) and Harry Clarke

*The container grammar's own ancestry.*

**Take**: two mechanisms 3.2's glyph grammar already borrows, made literal. First, **medallion and
quatrefoil framing as narrative units** — Sainte-Chapelle's upper windows are a grid of small,
consistently-shaped roundels and lobed frames, each holding exactly one legible scene, readable across
the length of a nave without walking closer. That is 3.2's "a new glyph should be nameable by its
container alone" design test, solved at architectural scale seven centuries before pixel art existed.
Second, **reserved-color economy** — cobalt blue and gold-ruby were the most expensive pigments a
medieval glazier had, and were spent deliberately on the most important figures, never spread evenly.
That is Hearth Gold's "spent like a resource" rule (4.3), independently arrived at from a completely
different production constraint. For a more graphic, less devotional execution of the same
leading-as-symbol logic, look at **Harry Clarke** — his glass (the Geneva Window) and book illustration
(*Tales of Mystery and Imagination*) use heavier, darker, more angular leading than typical rose-window
work, much closer to this game's "beautiful danger" register.

**Avoid**: real stained glass is **backlit and translucent** — its whole identity is light passing
*through* saturated color. This game's sprites are opaque, front-lit, flat-fill pixel art (6.4).
Nothing here justifies reaching for a soft luminous glow as a default rendering strategy for glyph
fills; bloom is a narrow additive layer over crisp base art (8.6), never the base look. And a cathedral
window is a single static composition, viewed once, never re-parsed under time pressure — it offers
nothing about motion hierarchy (3.5), 16px container survival, or a 10-color indexed cap (8.3).

*Serves*: Section 1 (Principle 2), 3.2, 4.1/4.3.

### 9.2 Monster Hunter

*Persistent, on-body part-break as trophy.*

**Take**: the mechanic this bible independently arrived at from a different direction — **breakable
parts that visibly, permanently change the model for the rest of the encounter.** A severed tail stays
severed; a broken horn stays a stump. That persistence is exactly what 3.1's seam-divided parts and
8.4's crack-decal overlay are reaching for: destruction as a standing mark on the creature, not a
counter that resets. It is also evidence at the design-test level — Monster Hunter proves players
reliably learn to target specific body regions from silhouette and posture alone, mid-combat, once the
grammar is taught. That is precisely the bet 3.1 is making.

**Avoid**: Monster Hunter keeps elemental-weakness information almost entirely **off the model** — in
the Hunter's Notes, in a menu, in a wiki. That is the exact anti-pattern the Master Rule exists to
prevent; "never a color swap alone" extends to "never a menu-only fact" (3.2, 7.4). Do not let "Monster
Hunter" as a reference quietly smuggle *weakness lives in a tooltip* back in through the side door.
Its monsters also render in semi-realistic 3D with soft fur/scale shading — the opposite of a
10-color flat indexed discipline (8.3); nothing about its surface rendering transfers. And it has no
answer at all for the threatening-then-endearing flip — it never rebinds hunted creatures as workers,
so don't look to it for 5.4/5.5's problem.

*Serves*: 3.1, 5.4, 8.4.

### 9.3 *How to Train Your Dragon*

*Same rig, different performance.*

**Take**: the most literal precedent for 5.5's central claim — separate identity from performance; only
the animation riding on fixed geometry changes. Toothless was designed and animated from cat-and-dog
behavioral reference specifically so **a single unchanging model could read as an apex predator in the
opening raid and a trusted companion an hour later, with no dialogue**: pupil aperture, ear/frill angle,
weight forward-and-coiled versus settled-and-loose, tail taut versus curled. A small, countable
vocabulary of readable tells layered onto one silhouette — structurally identical to what 5.5 asks for
(a Nature creature's branches flare and twitch when hostile; a Machine creature's plates lock rigid and
vent).

**Avoid**: this was built for one hero character on a feature-film CG budget, with a fully continuous rig
animating every tell simultaneously. This game needs the same principle across dozens of archetypes on a
homebrew angle-snapped cutout rig built from ~6 key poses retimed with easing curves (8.4/8.5). **Reduce
each Source/Role to the one or two cheapest tells it can afford.** Also, Toothless's arc is a single,
gradual, one-directional journey across a film. This game's flip is instant and **runs in both
directions repeatedly** — a bound creature reads endearing at rest and threatening again the moment it's
re-encountered in combat (5.4). Don't import HTTYD's slow-build pacing as a model for how the switch
itself should feel.

*Serves*: 5.4, 5.5, 8.4.

### 9.4 The Settlers

*Legible labor at a glance, at scale.*

**Take**: the reference case for an entire simulated economy being legible purely by watching small
worker sprites perform **visibly different, continuously-looping task animations** — a woodcutter's
chop, a miner's swing, a baker's knead — readable zoomed out, dozens on screen, no menu required. A
direct precedent for 5.3's per-role work-loop verbs (strike / brace / orbit / fidget / breathe) and for
6.7's promise that automation stage is countable by looking at what's standing. It is also an
existence-proof for 8.5's batching plan: a screen full of small continuously-animating units is a solved
problem in this genre, not a novel risk.

**Avoid**: Settlers' workers are **anonymous and interchangeable** — losing one is a logistics
inconvenience, not a loss, because no unit carries persistent identity. Every bound creature here is a
specific individual the player personally fought (5.5's "acknowledge" beat exists precisely because
ownership is meant to feel personal), so the work-loop verb must carry a persistent Source/Role glyph
identity *on top of* the labor animation — a layer Settlers never had to solve. It also renders
multi-directional isometric sprites with many facing angles per unit; this game's creatures are
single side-view rigs reused in the idle view under 6.1's "same bones, different camera" logic. Don't
import a multi-directional production requirement that isn't needed.

*Serves*: 5.3, 6.7, Section 2 (Idle Region View).

### 9.5 *Ori and the Blind Forest*

*The light that spreads, not the world that redraws.*

**Take**: Ori's environment-restoration sequences are the closest existing precedent for the exact
choreography Section 2 and 6.5 locked — **a wave of light and color-state change spreading outward from
a single point, across a landscape whose underlying geometry never changes**, turning corrupted into
restored through color temperature and light behavior alone. That is "the crack never moves, it only
closes" (3.3), already built and shipped elsewhere — useful proof that the beat reads as emotionally
significant rather than as a palette swap, which is the risk this bible guards against everywhere.

**Avoid**: Ori's visual language is thoroughly **painterly** — soft painted backdrops, dense particle
atmosphere, continuous gradients, motion blur. Everything 6.4 rules out. If an artist studies Ori
footage and comes away reaching for soft glow-bloom skies as a default look, that directly violates 6.4
and 8.6's rule that bloom is a narrow confined additive layer, never a general strategy. Just as
important: Ori's transformation is a **rare, singular, story-critical spectacle**, staged a handful of
times with a large one-off art budget behind each. This bible needs the same trick as a repeatable,
budget-disciplined *system* across every region (6.5's five-layer plan exists to make it affordable at
scale). **Take the wavefront choreography only — not its frequency, scale, or spend.**

*Serves*: Section 2 (Mastery Transition), 3.3, 6.5.

### 9.6 Path of Exile

*Proof density can stay fast — and a warning about how it stops being fast.*

**Take**: the strongest existing proof that a genuinely maximalist, systems-dense itemization interface —
the same category of screen this game's loot filters and automation config occupy — **can still be
scanned quickly by a player who has invested in learning its visual vocabulary**. It is also the direct
precedent for the Loot Filters screen (7.6): PoE effectively popularized player-facing loot-filter
scripting as a genre feature, and there are years of banked community UX lessons behind that one screen
worth mining before this game's version is designed. Its base chrome (inventory grid, stash tabs) stays
flat and restrained, reserving richness for a small set of genuinely special objects — a real-world
precedent for 7.6's "ornamentation flexes with ceremony, never with density," arrived at independently.

**Avoid**: PoE is also the industry's most-cited cautionary tale for onboarding failure. Its density
becomes legible **only after tens of hours of memorization**, because its modifier information leans on
stacked text and color-coded tags rather than a container-tells-you-category-before-you-read-the-detail
shape grammar — the exact inverse of 3.2's design test. Its color tagging also leans on hue as a primary
channel more than 4.6's colorblind architecture permits anywhere in this game. **Don't borrow PoE's
text-first, memorization-first route to density.** This bible already has a stricter shape-first
discipline (3.2, 3.4, 7.2) specifically so dense screens don't cost players the hours PoE does. The
correct takeaway is *"density is achievable,"* not *"this is how to achieve it."*

*Serves*: 3.4, 4.5, 7.1/7.2, 7.6.

---

**How to use this section**: none of the above is a mood board. Each entry earns its place by naming one
mechanism this bible had already locked *before* the reference was chosen, and each "avoid" line protects
an earlier rule that the reference's surrounding aesthetic would otherwise quietly erode. If a future
reference is proposed for this list, it must pass the same test this section was built on: **name the
specific locked rule it demonstrates, and name what about the source would break that rule if copied
wholesale. A reference that can only be described as "the general vibe" does not belong here.**
