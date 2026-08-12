# Regions, Rosters and Band Cycles

*Created: 2026-08-12*
*Status: Draft*

> Depends on `game-flow.md` (§3.2 archetypes, §3.3 bands) and
> `skill-and-trait-trees.md` (the four branches these bands interrogate).
> This document is the **content**: the questions the trees are answers to.

---

## 1. Overview

Six regions, each with a three-creature roster and a five-band cycle. A region's
identity is not its enemy art — it is **which archetype it leans on and which affix
it repeats**, because those are the things a build has to answer.

The six existing regions already imply their archetypes in their own flavour text:
Cinderworks grinds with "slow, heavy industrial blows" (Armoured), the Still Archive
kills by "a thousand cuts" (Caster), Marrow Wastes is "a slaughterhouse" (Bruiser).
This document makes that implication mechanical.

Crucially, **an archetype is a role, not a sprite.** The same creature appears as a
Swarm of three or as a single Bruiser depending on the band, distinguished by scale
and by an archetype glyph. That is not a shortcut — it is the game's stated visual
anchor applied to its most important mechanical state: *"every mechanically
important state gets a distinct glyph, never just a colour swap."* It also means new
content costs a table row rather than a creature sprite, which is what makes this
layer expandable.

---

## 2. Player Fantasy

A region should be a place with a *reputation*. The player should be able to say
"I don't go to Cinderworks with a Spread build" the way an ARPG player knows which
map mods to reroll.

The first visit to a region is a diagnosis: descend, hit its signature band, get
walled, read the report, come back with an answer. The second visit is the payoff —
the same band that stopped you at 23 falls in twenty seconds because you brought the
right shape.

What must never happen: a region that is simply *bigger*. If Marrow Wastes is
Cinderworks with larger numbers, the player has six copies of one place and the
tree has one question to answer.

---

## 3. Detailed Rules

### 3.1 A creature is a template plus a role

A creature in a wave is the product of two things:

- **Template** — its family and Source. Supplies art, name and base stat ratios.
  Each region has three, drawn from its own Source plus two neighbours on the
  Source wheel, so no region is a single-matchup lookup.
- **Archetype role** — Swarm, Armoured, Caster or Bruiser. Supplies the stat
  *shape*, the spawn count and the glyph.

The same `wisp` template is a Swarm at three-fifths scale in one band and a Bruiser
at full scale in another. The player reads which from the glyph and the count, not
from the silhouette.

### 3.2 Archetype stat shapes

Multipliers against the wave's baseline health and damage (flow doc §4.7).

| Archetype | Count | Health each | Damage each | Defense | Notes |
|---|---|---|---|---|---|
| **Swarm** | 3–5 | ×0.30 | ×0.45 | 0 | Combined damage exceeds a Bruiser's; combined health does not |
| **Armoured** | 1–2 | ×1.20 | ×0.90 | 25–120 flat, scaling with depth | Carries a breakable plate (§3.7) |
| **Caster** | 1–2 | ×0.55 | ×2.20 | 0 | Long windup; damage arrives in rare, huge hits |
| **Bruiser** | 1 | ×2.40 | ×1.30 | 10 | The honest check — no gimmick to solve |

The design intent behind each row:

- **Swarm's** combined damage is deliberately higher than a Bruiser's, so ignoring
  action economy is punished immediately rather than slowly.
- **Armoured's** Defense is the only number in the game that uses flat subtraction
  (flow §4.1), which is what makes hit *size* matter at all.
- **Caster's** health is low enough that a Tempo build deletes it before the windup
  resolves, and its damage is high enough that a slow build eats the whole hit.
- **Bruiser** has no trick. It exists so that a player who has solved the other three
  with gimmicks still has to have real numbers.

### 3.3 The affix catalogue

Ten affixes. Each one pressures a specific branch of the Skill Tree, so a band's
affix is a second question stacked on its composition.

| Affix | Effect | Pressures |
|---|---|---|
| **NUMBERS** | +1 creature in every wave | Spread |
| **PLATED** | enemy Defense +50% | Weight |
| **RITUAL** | enemy damage +8% per wave elapsed within the band | Tempo — the band must be cleared quickly |
| **ENDLESS** | leech and regeneration halved | Endure |
| **BRITTLE** | enemy health ×0.6, enemy damage ×1.6 | Endure — everything dies fast, including you |
| **ENTRENCHED** | the first hit on each creature deals 25% | Tempo — specifically punishes Alpha and First Strike |
| **SWIFT** | enemy attack interval −30% | Endure |
| **WARDED** | enemies take 60% less damage from whichever Form dealt the most damage last wave | Mono-Form builds |
| **HOLLOW** | no chests drop; haul ×2 | Neither — an economy decision |
| **LEGION** | each creature splits into two half-strength copies once on death | Spread |

**WARDED deserves its own note.** It is the only affix that punishes *concentration*
rather than a shape, and it is the strongest pressure toward carrying more than one
Form. It reads the previous wave rather than the current one so that it is
predictable — the player can see it coming and plan, which is required when they
cannot react mid-run.

### 3.4 The six regions

Each region's cycle contains its signature archetype in **two of five bands**, plus
exactly one band of the archetype that answers it least well. No region is a
single-answer region; every region has one band that punishes the build the rest of
it rewards.

#### 1 · VERDANT HOLLOW — Nature · the baseline

*Teaches: what a wave is, and that the report is the game.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | Swarm 40 / Bruiser 30 / Caster 30 | — |
| 2 | 11–20 | Swarm 55 / Armoured 25 / Bruiser 20 | NUMBERS |
| 3 | 21–30 | Bruiser 50 / Caster 30 / Swarm 20 | — |
| 4 | 31–40 | Swarm 45 / Caster 35 / Armoured 20 | LEGION |
| 5 | 41–50 | Armoured 40 / Bruiser 35 / Swarm 25 | PLATED |

Band 1 has no affix at all — the only band in the game that does not. Band 5 is
deliberately the region's own counter: a Spread build that walked bands 1–4 hits a
Weight question and learns what a wall is somewhere safe.

#### 2 · CINDERWORKS — Machine · the plated region

*Teaches: hit size. This is where a small-hit build learns it has a ceiling.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | Armoured 50 / Bruiser 30 / Swarm 20 | — |
| 2 | 11–20 | Armoured 45 / Caster 30 / Bruiser 25 | PLATED |
| 3 | 21–30 | Swarm 55 / Armoured 25 / Caster 20 | NUMBERS |
| 4 | 31–40 | Bruiser 45 / Armoured 35 / Caster 20 | SWIFT |
| 5 | 41–50 | Armoured 60 / Bruiser 40 | PLATED |

Band 3 is the counter-band: a pure Weight build built for bands 1–2 meets five
creatures at once and cannot kill them in time.

#### 3 · UMBRAL REACH — Shadow · the swarm region

*Teaches: action economy.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | Swarm 60 / Caster 25 / Bruiser 15 | — |
| 2 | 11–20 | Swarm 50 / Caster 35 / Armoured 15 | LEGION |
| 3 | 21–30 | Armoured 55 / Bruiser 30 / Swarm 15 | PLATED |
| 4 | 31–40 | Swarm 55 / Caster 30 / Bruiser 15 | NUMBERS |
| 5 | 41–50 | Caster 45 / Swarm 40 / Bruiser 15 | BRITTLE |

#### 4 · MARROW WASTES — Body · the attrition region

*Teaches: sustain. The first region where health is the binding constraint rather
than damage.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | Bruiser 55 / Armoured 25 / Swarm 20 | — |
| 2 | 11–20 | Bruiser 50 / Swarm 30 / Caster 20 | ENDLESS |
| 3 | 21–30 | Caster 50 / Swarm 30 / Bruiser 20 | BRITTLE |
| 4 | 31–40 | Bruiser 60 / Armoured 40 | SWIFT |
| 5 | 41–50 | Bruiser 45 / Armoured 30 / Caster 25 | ENDLESS |

Two ENDLESS bands make this the region that a leech build cannot brute-force. Band 3
is its counter: everything is fragile and fast, so a slow tanky build takes more
total damage than a fast one.

#### 5 · THE STILL ARCHIVE — Mind · the caster region

*Teaches: speed, and that a mono-Form build has a ceiling.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | Caster 55 / Swarm 25 / Bruiser 20 | — |
| 2 | 11–20 | Caster 50 / Swarm 35 / Armoured 15 | WARDED |
| 3 | 21–30 | Bruiser 50 / Armoured 35 / Caster 15 | ENTRENCHED |
| 4 | 31–40 | Caster 45 / Swarm 40 / Armoured 15 | RITUAL |
| 5 | 41–50 | Caster 40 / Armoured 35 / Bruiser 25 | WARDED |

The Archive is where WARDED lives. Band 3 stacks ENTRENCHED — which specifically
blunts the opener — onto Bruisers and Armour, and is the hardest band before the
final region.

#### 6 · THE PALE CHOIR — Spirit · the examination

*Teaches nothing. Tests everything.*

| Band | Waves | Composition | Affix |
|---|---|---|---|
| 1 | 1–10 | even 25 / 25 / 25 / 25 | — |
| 2 | 11–20 | even | WARDED |
| 3 | 21–30 | even | RITUAL |
| 4 | 31–40 | even | ENDLESS |
| 5 | 41–50 | even | NUMBERS + PLATED |

Every band is an even mix, so no shape is favoured and only breadth survives. Band 5
carries two affixes — the first place in the game that does — which is also the
template for how corruption tiers work everywhere else (§3.6).

### 3.5 Region roster assignments

Each region draws three templates: its own Source and its two neighbours on the
Source wheel. This is what stops a single Source pick from being a free 1.5× against
a whole region (flow §3.2).

| Region | Source | Roster templates |
|---|---|---|
| Verdant Hollow | Nature | wisp (Nature), bonecrawler (Body), soul_leech (Mind) |
| Cinderworks | Machine | stone_sentinel (Machine), wisp (Nature), shadeling (Shadow) |
| Umbral Reach | Shadow | shadeling (Shadow), stone_sentinel (Machine), rift_guardian (Spirit) |
| Marrow Wastes | Body | bonecrawler (Body), rift_guardian (Spirit), wisp (Nature) |
| The Still Archive | Mind | soul_leech (Mind), bonecrawler (Body), shadeling (Shadow) |
| The Pale Choir | Spirit | rift_guardian (Spirit), soul_leech (Mind), stone_sentinel (Machine) |

All six templates already have art. No new creature sprites are required to ship
this system — only archetype glyphs, of which there are four.

### 3.6 Past band 5

Bands repeat from band 1 with a scaling multiplier applied to the baseline. The
cycle stays the same so it remains learnable; only the numbers move.

Corruption tiers (flow §3.9) add a **second affix** to every band, drawn from the
catalogue and fixed per tier so it is predictable. Tier 1 adds SWIFT everywhere,
tier 2 adds ENDLESS, and so on. This is the same mechanism the Pale Choir's band 5
demonstrates, which is why that band exists where it does — it teaches the endgame's
rule before the endgame.

### 3.7 Bosses and parts

Every fifth wave is a boss: a single creature of the band's **dominant** archetype
at boss scale, always carrying a **part**.

A part is a plate with its own health pool. Damage that lands on it while it is
intact is halved; breaking it removes the creature's flat Defense for the rest of
the fight and pays the part-break reward (flow §3.5). Because a part has its own
pool, breaking it is a function of hit size — a Weight build removes it in two hits,
a Spread build may never remove it at all.

The region boss at conquest depth is a fixed authored encounter rather than a rolled
one: the same creature, the same part, every time. It is the region's exam and must
be practisable.

---

## 4. Formulas

### 4.1 Composition roll

    creatureCount   = archetype.CountRange rolled per wave
    archetypeChosen = weighted pick from band.Composition
    templateChosen  = uniform pick from region.Roster

Rolled once per wave from a seed derived from `(regionId, wave, runIndex)` so a
replayed wave is identical — required for fast-forward (flow §3.1) to pay the same
haul it originally paid.

### 4.2 Creature stats

    health  = waveBaseHealth  x archetype.HealthMult  x affixHealthMult
    damage  = waveBaseDamage  x archetype.DamageMult  x affixDamageMult
    defense = archetype.DefenseBase x (1 + 0.02 x wave) x affixDefenseMult

`waveBaseHealth` and `waveBaseDamage` come from flow §4.7, including the boss fix
(health only).

### 4.3 Part health

    partHealth = creature.health x 0.35

A single hit that removes 35% of a creature's health in one blow breaks its part
outright — which is the numeric statement of "Weight builds break parts and Spread
builds do not".

### 4.4 Band lookup

    band(wave)  = floor((wave - 1) / 10)
    cycleIndex  = band(wave) mod 5
    scaleTier   = floor(band(wave) / 5)
    baseline   x= BandRepeatScale ^ scaleTier          BandRepeatScale = 1.9

---

## 5. Edge Cases

| Situation | What happens |
|---|---|
| A band's composition would roll zero creatures | Floor of 1. A wave always has an enemy. |
| LEGION on a Swarm wave | Splits are capped at the wave's maximum creature count (8) so a Swarm band cannot become a soft lock. |
| WARDED on the first wave of a region (no previous wave) | No Form is warded until wave 2. The affix needs history and must not guess. |
| WARDED when two Forms tie for most damage | The tie breaks toward the Form with more total activations, then by Form enum order. Deterministic, because the player must be able to predict it. |
| A boss rolls an archetype with a multi-creature count | Bosses are always count 1; the dominant archetype supplies the stat shape only. |
| PLATED on a band with no Armoured creatures | It still applies to whatever Defense exists, which for Swarm is zero. A wasted affix is acceptable; the alternative is re-rolling affixes, which breaks learnability. |
| HOLLOW on a boss wave | No chest, doubled haul. This is the intended tension of the affix and is not exempted for bosses. |
| Corruption tier adds an affix a band already has | The band keeps one instance; the tier's affix is substituted with the next in its fixed order. |
| Fast-forward replays a wave whose seed inputs changed | Cannot happen — the seed uses `runIndex`, not wall time, and fast-forward replays the recorded depth, not a new roll. |

---

## 6. Dependencies

| System | Relationship |
|---|---|
| `game-flow.md` | Supplies archetype definitions, band structure, boss scaling and the part-break reward. |
| `skill-and-trait-trees.md` | Every affix here pressures a named branch there. Adding an affix without naming its branch is how a difficulty modifier becomes noise. |
| `creature-data-schema.md` | **Needs an archetype role field** separate from the template, plus the four archetype glyphs. |
| `encounter-spawn-system.md` | Supersedes its spawn rules with §4.1. |
| `region-view-world-map-ui.md` | The map must show each region's **signature archetype and affixes** before entry — a region's reputation is only useful if it is legible from outside. |
| `loot-drop-system.md` | HOLLOW and part-breaks both modify drops. |

---

## 7. Tuning Knobs

| Knob | Default | Safe range | Affects |
|---|---|---|---|
| Swarm count | 3–5 | 2–8 | How hard action economy bites. Above 6 the fight becomes unreadable on screen. |
| Swarm damage each | ×0.45 | 0.30–0.60 | Whether ignoring Spread is fatal or merely slow. |
| Armoured Defense base | 25–120 | 10–200 | The whole Weight axis. Tune against `MinHitFraction` (flow §7), never alone. |
| Caster damage | ×2.20 | 1.6–3.0 | How sharply slow builds are punished. |
| Bruiser health | ×2.40 | 1.8–3.2 | Run length in Bruiser bands. |
| `partHealth` fraction | 0.35 | 0.25–0.50 | Which builds can break parts at all. |
| `BandRepeatScale` | 1.9 | 1.5–2.5 | How far a cycle repeat pushes the ceiling. |
| Signature bands per region | 2 of 5 | 2–3 | Region identity. At 4 a region becomes a single-answer region. |
| Counter-band per region | 1 of 5 | 1 | Must stay at 1. Removing it makes each region solvable with one shape. |

---

## 8. Acceptance Criteria

1. Every region contains at least one band whose dominant archetype is *not*
   answered by the build that clears the rest of that region.
2. No region can be cleared to band 5 by a build that has allocated to only one
   Skill Tree branch.
3. A Weight-specialised build reaches a deeper band in Cinderworks than in Umbral
   Reach, and a Spread-specialised build reaches a deeper band in Umbral Reach than
   in Cinderworks — at least one full band of difference in each direction.
4. The same wave number in the same region with the same run index produces an
   identical composition on replay.
5. WARDED never applies on wave 1 of a region.
6. All six regions ship without new creature art; only four archetype glyphs are new.
7. The map screen shows a region's signature archetype and its affix set before the
   player enters.
8. A boss's part can be broken by a build with Weight ring 3 and cannot be broken by
   a build with Spread mastery, at equal total damage output.
