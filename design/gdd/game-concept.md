# Game Concept: Resonance Hunter

*Created: 2026-07-13*
*Status: Draft*

---

## Elevator Pitch

> It's a 2D pixel-art idle action RPG where you actively hunt corrupted spirit
> creatures — breaking weak points, reading telegraphs, timing abilities — to
> master a region, then hand that mastery to an automated farming team while
> you push into the next frontier.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Idle Action RPG (creature-hunting / automation hybrid) |
| **Platform** | PC (Steam / Epic) |
| **Target Audience** | Achievers and Strategists who enjoy build theorycrafting and systemic depth (see Player Profile) |
| **Player Count** | Single-player |
| **Session Length** | Short active bursts (10–30 min) bridging longer idle/automation stretches; also supports 1–2 hr deep optimization sessions |
| **Monetization** | Not yet decided |
| **Estimated Scope** | Large (12–24+ months, solo/small team) — full vision. See Scope Tiers for staged timelines. |
| **Comparable Titles** | Melvor Idle, Monster Hunter, Path of Exile |

---

## Core Fantasy

You are a Resonance Hunter who tames chaos. Every corrupted region you conquer
through skill becomes a living, self-sustaining part of your growing empire —
freeing you to push into ever-wilder frontiers instead of re-fighting what
you've already mastered. The fantasy isn't just "get strong" — it's "build a
world that runs without you, one hard-won region at a time."

---

## Unique Hook

It's like Monster Hunter's weak-point precision combat, **and also** Melvor
Idle's systemic automation-as-endgame — except automation isn't a passive
fallback bolted onto the end of the game. It's an earned, designed unlock with
its own mastery curve, and active play never stops paying off even in regions
you've fully automated. Most idle games treat manual play as a phase you
graduate out of; here, active and idle are two permanently co-existing modes
of every system.

---

## Visual Identity Anchor

**Selected direction**: Resonance Glyph

**One-line visual rule**: Every mechanically important state gets a distinct glyph — never just a color swap.

**Supporting principles**:

1. Clean, readable pixel-art sprites carry a glowing glyph/iconography layer marking elemental Source and current vulnerability state. *Design test: when a new mechanical state needs a visual signal (weak point, telegraph, Vow condition, evolution branch), design a glyph for it before reaching for a palette-only differentiation.*
2. The glyph layer reads like stained-glass symbolism laid over pixel sprites — ornamental but always legible at gameplay resolution and speed. *Design test: if a glyph design looks striking but can't be read in under a second mid-combat, simplify it until it can.*

**Color philosophy summary**: Color communicates broad category (element, rarity, faction); glyphs communicate specific mechanical state. Color alone must never be the sole signal for anything the player needs to react to — this reinforces both colorblind accessibility and Pillar 1 (Precision Over Reflexes).

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation** (sensory pleasure) | 4 | Glyph-based visual feedback on weak points/telegraphs, hit-stop and audio payoff on part-breaks |
| **Fantasy** (make-believe, role-playing) | 6 | The Resonance Hunter identity; taming corrupted spirit creatures |
| **Narrative** (drama, story arc) | N/A (MVP) | Not a focus for MVP; world/lore color exists but isn't the design driver — revisit post-MVP if Relatedness needs strengthening |
| **Challenge** (obstacle course, mastery) | 1 (primary) | Weak-point precision combat, telegraphed attacks, part-break mechanics, Vow-restricted builds |
| **Fellowship** (social connection) | N/A | Single-player only; no social systems planned |
| **Discovery** (exploration, secrets) | 3 | New regions, hybrid forge recipes, creature evolution branches, rare-creature capture conditions |
| **Expression** (self-expression, creativity) | 2 | Resonance Weaving (Source × Form × Vow) builds, forge hybrids, creature job/evolution choices |
| **Submission** (relaxation, comfort zone) | 5 | The automation/idle layer as a low-stress background management loop |

### Key Dynamics (Emergent player behaviors)

- Players will theorycraft Source/Form/Vow ability combos to find synergies that let a Vow's restriction still be met safely.
- Players will reroute unwanted loot toward creature evolution, discovering branches for a favorite creature line.
- Players will benchmark idle vs. active efficiency and self-select how much active time to invest per region.
- Players will chase part-break patterns and rare-creature capture windows as a skill-expression loop distinct from raw stat progression.

### Core Mechanics (Systems we build)

1. **Weak-point precision combat** — single-screen encounters with targetable creature parts, telegraphed attacks, a limited dodge/block, 3 equipped abilities + 1 ultimate, timed against vulnerability windows.
2. **Resonance Weaving** — abilities built from Source × Form × Vow combinations; Vows trade a restriction for greater power.
3. **The Forge** — merge, hybridize, and enchant gear; enchantments change gameplay behavior, not just stats.
4. **Creature capture, jobs & evolution** — branching evolution driven by element, equipped items, job assignment, combat behavior, and consumed materials.
5. **Region mastery & staged automation** — boss-gated unlock of automated farming (auto-attack → auto-loot → auto-craft/sell → offline progression), governed by an explicit active/idle efficiency curve.

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** | Free choice of weak-point targeting, Vow/ability loadout, creature job assignment, region prioritization, forge recipe choices | Core |
| **Competence** | Explicit, legible mastery curve (25–40% → 100–120% idle efficiency vs. active play), part-break execution skill, build theorycrafting depth | Core |
| **Relatedness** | Creature roster provides light companion feel via job/evolution identity; no deep narrative or social layer in MVP | Minimal (flagged as a future opportunity, not a current gap) |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — region mastery unlocks, the automation efficiency ladder, full evolution/forge completion goals
- [x] **Explorers** — hybrid forge recipes, evolution branch discovery, rare-creature capture conditions, the Resonance Weaving combo space
- [ ] **Socializers** — not targeted; single-player only in current scope
- [ ] **Killers/Competitors** — not targeted; no PvP or leaderboards planned

### Flow State Design

- **Onboarding curve**: The first region teaches weak-point targeting and telegraph reading through low-stakes encounters before introducing Vow restrictions.
- **Difficulty scaling**: New regions raise part-break complexity and telegraph speed/density; automation removes the need to keep re-executing mastered content manually.
- **Feedback clarity**: Visible glyph states on weak points/telegraphs (per the Resonance Glyph visual anchor), plus the explicit idle-efficiency percentage as a legible mastery readout.
- **Recovery from failure**: Encounters are short (single-screen, minutes-long) so failure costs little time; a botched capture attempt simply becomes a kill instead of a hard fail state.

---

## Core Loop

### Moment-to-Moment (30 seconds)

Single-screen encounter. Basic attacks auto-fire; the player reads telegraphs
and weak-point windows, clicks precisely, blocks/interrupts/dodges (limited
dodge), and times 3 equipped abilities + 1 ultimate against Vow conditions and
accumulated Resonance energy. Breaking a specific body part changes the fight
state and the loot table live. This is monster-hunting decision-making
combined with a clicker's immediacy and a boss-rush game's readable attack
patterns — satisfying through pattern recognition and precise execution, not
movement.

### Short-Term (5-15 minutes)

Chain 3–6 encounters into a region "expedition." Bank loot, decide whether to
push for a rarer encounter or retreat, watch for capture-without-killing
windows on rare creatures. The "one more fight" pull comes from part-break
streaks and rare sightings.

### Session-Level (30-120 minutes)

Push a region to its boss, defeat it, unlock Mastery. Return to the forge:
merge/hybridize the run's gear, feed unwanted loot to a creature to nudge its
evolution, assign creatures to jobs. Configure or upgrade an automated farming
team for a mastered region. Natural stop point: boss kill → forge pass → team
assignment — a clean chapter close.

### Long-Term Progression

Growth compounds across four axes: power (gear/enchantments), knowledge
(weak-point patterns, Vow timing, hybrid recipes), roster (creature collection
+ evolution branches), and automation breadth (number of regions running idle
simultaneously). There's no hard "done" — the soft target is a fully automated
empire plus endgame Vow-restricted challenge builds, reinforced by Memory Dust
prestige resets.

### Retention Hooks

- **Curiosity**: Unexplored hybrid forge recipes, undiscovered evolution branches, the next region's creature roster.
- **Investment**: A built farming team and forge progress the player doesn't want to lose; evolved creatures they've shaped.
- **Social**: None in current scope (single-player).
- **Mastery**: The explicit idle-efficiency ladder (25–40% → 100–120%) gives players a visible skill/optimization target to climb.

---

## Game Pillars

### Pillar 1: Precision Over Reflexes

Mastery is expressed through pattern recognition and high-value execution,
not movement or twitch reflexes.

*Design test*: If we're debating free-roam dodging vs. deeper weak-point/
telegraph mechanics, this pillar says we choose weak-point depth.

### Pillar 2: Automation Is Earned, Not Assumed

Every idle system unlocks through active mastery (a region boss kill) before
it exists — nothing is automatic by default.

*Design test*: If we're debating whether a new mechanic should be
auto-playable from the start or require an active-mastery unlock first, this
pillar says we choose the unlock.

### Pillar 3: Active Is Better, Idle Is Never Worthless

Active play should feel meaningfully more rewarding (~2x) than automation, but
automation must always produce genuine core progression — never busywork or
fake numbers.

*Design test*: If we're debating whether a resource/creature/evolution should
be idle-farmable, this pillar says yes — unless it fundamentally requires a
decision idle can't make.

### Pillar 4: Builds Are Trade-offs, Not Stat Stacks

Power comes bundled with restriction (Vows) and behavior change
(enchantments), not flat stat stacking.

*Design test*: If we're debating adding a pure +X% stat modifier vs. a
build-altering effect (Vow or enchantment behavior change), this pillar says
we choose the behavior change.

### Pillar 5: Creatures Are Systems, Not Trophies

Every captured/evolved creature must have a functional job (attacker,
defender, support, crafter, producer) that plugs into the automation economy —
not just a collection checkbox.

*Design test*: If we're debating adding a creature type purely for collection
variety with no distinct job role, this pillar says no — give it a role or
cut it.

### Anti-Pillars (What This Game Is NOT)

- **NOT free-roaming overworld combat traversal**: Would compromise Precision Over Reflexes — encounters are contained arenas, not overworld navigation challenges.
- **NOT idle progression deliberately nerfed to sell active-play shortcuts (including via monetization)**: Would compromise Active Is Better, Idle Is Never Worthless.
- **NOT flat stat-only gear dominating the loot pool**: Would compromise Builds Are Trade-offs — every rarity tier must include behavior-changing options, not just bigger numbers.
- **NOT cosmetic-only creature variants with no job/behavior differences**: Would compromise Creatures Are Systems, Not Trophies.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| Melvor Idle | Proven systemic depth combined with idle progression; validates market appetite for "idle with real decisions" | Real-time weak-point combat replaces turn-based/passive combat resolution | Validates the core market thesis: idle + genuine decision-making + system depth works |
| Monster Hunter | Weak-point/part-break combat feel; capture mechanics | Condensed to single-screen encounters for automatability; part-breaks feed a forge/evolution economy, not just monster parts | Validates that precision-targeting combat can be deeply satisfying without free-roam movement |
| Path of Exile | Build theorycrafting depth; trade-off-based itemization | Trade-offs are explicit Vows/enchantment behaviors rather than passive tree nodes; build layer extends to creatures, not just gear | Validates that restriction-for-power design creates lasting build identity |

**Non-game inspirations**: Not yet discussed — revisit if narrative/tone touchstones become relevant during `/design-system` or `/art-bible`.

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18–35 (inferred from comparable-titles analysis — adjust if you have specific audience data) |
| **Gaming experience** | Mid-core to hardcore — build theorycrafting depth demands system fluency |
| **Time availability** | Short active bursts (10–30 min) on weekdays; longer optimization/session pushes on weekends. The idle layer accommodates both busy and dedicated schedules. |
| **Platform preference** | PC / Steam |
| **Current games they play** | Melvor Idle, Path of Exile, Monster Hunter (inferred from comparable titles) |
| **What they're looking for** | Real decision-making and skill expression inside an idle game, not just number-watching |
| **What would turn them away** | Games demanding constant twitch-reflex attention with no idle payoff, or idle progression with zero meaningful decisions |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | MonoGame (C#) — explicit choice. Note: this studio template has no dedicated engine-specialist agent for MonoGame (only Godot/Unity/Unreal); general and lead-programmer agents cover this gap. |
| **Key Technical Challenges** | Dual-mode systems (every mechanic needs an active AND an automated version); tuning automation efficiency curves across many regions/creatures without a degenerate optimal strategy; offline-progression simulation (accurate, performant time-elapsed catch-up) |
| **Art Style** | 2D pixel-art with a glyph/iconography overlay layer (Resonance Glyph visual anchor) |
| **Art Pipeline Complexity** | Medium — custom 2D pixel-art plus a consistent glyph/icon system across many creatures and items |
| **Audio Needs** | Moderate — telegraph/weak-point audio cues are functionally important, not just decorative, plus ambient region themes |
| **Networking** | None (single-player) |
| **Content Volume** | TBD — depends on final region count. MVP = 1 region; Full Vision = many regions (see Scope Tiers) |
| **Procedural Systems** | None confirmed yet. Loot/forge recipe combinatorics are rule-based rather than procedurally generated. Open question: are region layouts hand-crafted or procedurally varied? |

---

## Risks and Open Questions

### Design Risks

- The active→idle handoff must feel satisfying at every mastery transition, not just once — this is the core untested assumption of the whole game.
- The automation ceiling (25–40% → 100–120%) is a fine balance; too generous trivializes active play, too stingy makes idle feel punishing.
- Relatedness (SDT) is currently thin — no confirmed narrative or social hook yet.

### Technical Risks

- Every system needs a dual authored form (active depth + idle-automatable simplification) — roughly doubles the design/implementation surface per system.
- Offline-progression simulation is nontrivial, especially once multiple regions run in parallel.
- MonoGame has no dedicated engine-specialist agent in this template — architecture guidance leans on general/lead-programmer agents instead.

### Market Risks

- Niche at the intersection of two audiences (active-combat fans and idle-game fans) — could underperform if it doesn't clearly land with either.
- The idle-hybrid genre has a strong incumbent (Melvor Idle) with an established, loyal audience.

### Scope Risks

- The full vision is Large (12–24+ months) against a much shorter stated development timeline — mitigated via the Prototype-First path and tiered scope, but only if scope discipline holds.
- The Forge (merge + hybrid + enchantment behaviors) and Resonance Weaving (Source × Form × Vow combinatorics) are each large enough to be their own project; MVP intentionally simplifies both.

### Open Questions

- Does the active→idle handoff actually feel rewarding rather than deflating? → Resolved by the `/prototype` core-loop test.
- How many regions/creature lines are needed for the Full Vision to feel complete vs. bloated? → Resolved during `/map-systems`.
- What replaces or supplements the thin Relatedness axis (lore, factions, hunter NPCs)? → Open, revisit post-MVP.
- Is region content hand-crafted, or does any part use procedural variation? → Resolved during `/create-architecture`.

---

## MVP Definition

**Core hypothesis**: Players find the weak-point precision combat satisfying
on its own, AND the moment automation takes over a mastered encounter feels
like a rewarding payoff rather than a loss of agency.

**Required for MVP**:

1. One region, single-screen weak-point/telegraph combat (3 abilities + 1 ultimate, limited dodge/block)
2. Merge-only forge (no hybrids or enchantments yet)
3. One creature line with 2–3 evolution branches tied to job assignment
4. A curated (non-combinatorial) Resonance Weaving ability list
5. Automation Stages 1–2 (auto-attack + auto-loot) unlocked by the region boss kill

**Explicitly NOT in MVP** (defer to later):

- Hybrid forge combinations and behavior-changing enchantments
- The full Source × Form × Vow combinatorial ability-creation system
- Multiple regions / region-to-region progression
- Full offline-progression simulation
- Memory Dust prestige system
- Rare-creature capture-without-killing mechanics (defer unless the prototype shows it's core to the fun)

### Scope Tiers (if budget/time shrinks)

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **Prototype** | One enemy type, no art investment | Manual weak-point combat + a single automation toggle | 1–3 weeks |
| **MVP** | One region, one creature line | Core loop, merge-only forge, curated Weaving list, Automation Stages 1–2 | 3–6 months |
| **Vertical Slice** | One region, fully polished | Core + full forge (merge/hybrid/enchant) + full evolution + full automation stack incl. offline progression + Weaving combinatorics | TBD — scoped during `/vertical-slice` |
| **Alpha** | All planned regions, placeholder art | All systems implemented, rough | TBD — scoped during `/sprint-plan` |
| **Full Vision** | Complete region/creature/item content | All features polished, incl. Memory Dust prestige | 12–24+ months total |

---

## Next Steps

- [ ] ~~Get concept approval from creative-director~~ — skipped, review mode is `lean` (director gates only run at phase transitions; see `production/review-mode.txt`)
- [ ] Fill in CLAUDE.md technology stack based on engine choice (`/setup-engine` — MonoGame, C#)
- [ ] **Prototype core idea** (`/prototype`) — validate the active→idle handoff feels good before writing any GDDs (Prototype-First path, per timeline constraints)
- [ ] If prototype PROCEEDS: create the Art Bible (`/art-bible`), then decompose concept into systems (`/map-systems`)
- [ ] Design each system (`/design-system [system-name]`) — use prototype learnings in Tuning Knobs and Formulas sections
- [ ] Cross-system consistency check (`/review-all-gdds`)
- [ ] Validate readiness for architecture (`/gate-check`)
- [ ] Build vertical slice in Pre-Production (`/vertical-slice`) — validate full game loop before committing to Production
- [ ] Validate core loop with playtest (`/playtest-report`)
- [ ] Plan first milestone (`/sprint-plan new`)
