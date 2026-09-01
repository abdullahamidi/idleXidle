# Skill Tree and Trait Tree

> **TWO HALVES, OPPOSITE FATES (2026-09-01).** The SKILL-tree half (§1, §3.1-3.3, §4.1) is SUPERSEDED by `skill-slots-and-skill-trees.md` — WEIGHT/SPREAD died at the 2026-08-30 re-axe. The TRAIT-tree half (§3.4-3.6, §4.2, §5) is the ONLY prose spec of the live trait tree (Prestige/MemoryDust.cs, TraitRoads.cs) — current, with the budget at 34 points since P7. The 256-node tree shape in §3.2 is also still the live tree (MasteryTreeTests pins it).

*Created: 2026-08-12*
*Status: Draft*

> Depends on `game-flow.md`. The archetype engine defined there is what gives these
> trees something to be about; read §3.2 and §3.6 of that document first.

---

## 1. Overview

Two trees, two jobs.

The **Skill Tree** is scarce but fluid: points come from depth, respec is free, and
it decides *how the Hunter fights right now*. It is organised as four branches, one
per demand the content makes (§3.2 of the flow doc) — **Weight**, **Spread**,
**Tempo**, **Endure** — arranged as two opposed pairs. About a quarter to a third of
the tree is affordable at full progress, which buys one branch outright and half of a
second.

The **Trait Tree** is scarce and permanent: points come from conquests and mastery
goals, there is no respec, and it decides *who the Hunter is*. Four paths of
keystones sit around a cheap structural spine that grants slots, sockets and
unlocks. Its terminals are priced so that two of four is the realistic ceiling.

Neither tree may ever be completed, and neither may be a list of percentages.

---

## 2. Player Fantasy

The Skill Tree should feel like a workbench: you come back to it after every wall,
move six points, and go again. Nothing about it is precious, and that is the point —
the cost of being wrong is a minute, so experimenting is the correct behaviour.

The Trait Tree should feel like the opposite. You visit it rarely, you think for a
long time, and what you choose is still true fifty hours later. When a player says
"I'm a Bloodlust hunter", that sentence comes from this tree.

The two must never feel like the same screen with different colours. That is the
failure the audit found: three trees multiplying the same five numbers.

---

## 3. Detailed Rules

### 3.1 The organising principle

The content asks four questions (flow doc §3.2). The Skill Tree has exactly four
answers, and they are arranged so that the two most natural specialisations pull
against each other:

```
                 WEIGHT
          (few, enormous hits)
                    |
     ENDURE ——— [ START ] ——— TEMPO
   (health,             (speed, burst,
    leech,               front-loaded)
    mitigation)           |
                     SPREAD
              (many targets)
```

- **WEIGHT ↔ SPREAD** are opposed: hit size and target count trade against each
  other on every node that touches them.
- **TEMPO ↔ ENDURE** are opposed: front-loading damage and surviving a long fight
  want different resources.

A build that walks one axis to its end is excellent in one band and stalls in the
one that asks the opposite question. That stall is the game.

The six existing Forms already distribute across these four axes, which is why the
scheme fits the code rather than replacing it:

| Axis | Forms that live there | Why |
|---|---|---|
| Weight | Strike, Trap | Single target, largest per-hit damage (45 / 55 base) |
| Spread | Aura, Projectile | Aura ticks everything; Projectile is fast and multi-hit |
| Tempo | Mark | An amplify window is pure front-loading |
| Endure | Transformation | Leeches 12% — it is the sustain Form (was 50%; see the heal ceiling below) |

### 3.2 Skill Tree — shape and budget

**Ring structure.** Each of the four branches has four rings that get more expensive
and more consequential as they go out:

| Ring | Count | Cost each | What it is |
|---|---|---|---|
| 1 — minors | 6 | 1 | Small shape changes. Connective tissue, but each one still moves a shape, not just a number. Five hang off START; the sixth is a **spur** that hangs off the outermost of the five. |
| 2 — notables | 5 | 3 | A real behaviour. This is where a branch starts to have an opinion. Four accept any spine minor; the fifth needs the spur. |
| 3 — greaters | 4 | 5 | A behaviour with a visible cost attached. Three accept any fan notable; the fourth needs the side notable. |
| 4 — mastery | 1 | 8 | The branch's identity, and always a trade that makes some builds impossible. Accepts any of the four greaters. |

A full branch costs **49 points**.

**The 2026-08-27 pass.** *(Playtest: "add new nodes and lower the numbers on the
current nodes a little.")* Every ring gained one node — a sixth minor, a fifth notable
and a fourth greater per branch, twelve in all, each a new shape the sim reads — and
every existing bonus came down by roughly a fifth to a third, so a full walk lands
about where it did with three more decisions in it. The new node of each ring sits in
the fan (any spine node of the ring below unlocks it); the side road is unchanged.
The branch sweep was re-measured at the same 18-point budget: best/worst depth ratio
1.38 → 1.38, inside the 1.40 band.

**The side road.** *(Added 2026-08-25, after the post-pre-alpha playtest: "almost at a
specialisation in the first 15 minutes.")* Each branch was 4 → 3 → 2 → 1 and cost 31.
One minor, one notable and one greater per branch are not spread across the existing
any-of fans — they are strung end to end as one **side road**: outermost minor → spur
minor → side notable → side greater → capstone. Walked as a road it costs
1 + 1 + 3 + 5 + 8 = 18 and needs every step in order, so nine more points per branch
actually slow the walk down instead of offering nine more ways to skip ahead. Each road
carries its branch's identity from end to end and its greater pays a visible price, like
the two beside it.

**Bridges.** Between each pair of adjacent branches sits one bridge node, cost **6**,
requiring ring 2 of both neighbours. Bridges let a player hybridise, but at a price
that means hybridising costs them a mastery.

**Form specialisations.** Six sub-branches, one per Form, cost **6** each, each
hanging off the axis its Form belongs to. Taking one deepens that Form specifically
rather than the axis generally.

**Totals.**

```
4 branches x 49        = 196
4 bridges x 6          =  24
6 Form specialisations =  36
                        ----
total tree cost         256
```

Against roughly **66–78 points** at full current content (six regions walked to
depth 150–225 each, on the square-root curve of §4.1) that is **26–30%** — the
"about a third" invariant from flow doc §4.4, at the low end on purpose: the tree
grew to slow the walk and the points curve grew with it, but less. One branch
outright plus half of a second, or two branches to ring 3 and nothing else.

### 3.3 Skill Tree — node catalogue

Names are working names. What matters is the effect and the trade.

#### WEIGHT — answers Armoured

*Fewer, larger hits. Flat mitigation is the enemy this branch exists to beat.*

**Minors (1)** · **Heft** *(new)*: Strike and Trap hit 20% harder — Weight's two Forms
· Heavy Hand: +18% hit size, −12% skill rate · Sharpened: hits subtract 6 from enemy
Defense before mitigation · Follow Through: first hit on each creature +20% size ·
Deliberate: −15% skill rate, +22% hit size · **Dent** *(spur, off Deliberate)*: a hit
over 150 strips 6 Defense for the rest of the wave — with Sunder the threshold takes
the kinder 150 and the strips add

**Notables (3)** · **Headlong** *(new)*: +30% against creatures above half health —
Cull's mirror; Weight puts its few big hits into a full pool (sim field
`FreshThreshold` / `FreshBonus`) · **Sunder**: a hit above 200 permanently reduces that
creature's Defense by 15 for the rest of the wave · **Crush**: a hit 8× bigger than the
armour ignores 40% of it · **Breaker**: 40% of any overkill carries to the next
creature · **Second Blow** *(side road, needs Dent)*: every hit after the first on a
creature +15% — the mirror of Tempo's Alpha, a single-target commitment stated as a
shape

**Greaters (5)** · **Stagger** *(new)*: a hit over 200 pushes the wave's next bite
back half a second, once per bite; cooldowns +15% (sim field `StaggerThreshold` /
`StaggerMs`, read in `LandOn` against the raw swing) · **Monolith**: all damage is
delivered in half as many hits, each twice as large · **Siege**: +35% damage to
Armoured, −20% to everything else · **Shatter** *(side road, needs Second Blow)*: a hit
over 120 strips 22 Defense for the wave; cooldowns +18% — with Dent and Sunder the
strips sum to 43, so a plated wave is bare after two swings, and the price is Weight's
own currency

**Mastery (8)** · **OVERWHELM**: hits below 60 damage deal nothing at all; hits
above 60 ignore enemy Defense entirely.
*This is the branch in one node: armour stops existing, and so does any build that
delivers its damage in small pieces.*

#### SPREAD — answers Swarm

*Target count and action economy. Losing to five creatures at once is this branch's
enemy.*

**Minors (1)** · **Fan** *(new)*: Trap reaches one more creature — Weight's other Form
pulled onto the Spread axis · Wide: Projectile +1 target · Diffuse: cooldowns −8%,
hits −4% *(the card used to claim "Aura ticks faster" over a shape that never touched
the Aura; it says what it does now)* · Quick Hands: −10% cooldowns · Ricochet: 25%
chance each hit strikes a second creature for half · **Sweep** *(spur, off Ricochet)*:
Strike reaches one more creature

**Notables (3)** · **Rally** *(new)*: after a kill, the next skill activation hits 40%
harder, every hit of it — fed by kills like Momentum (sim field
`NextSkillAfterKillBonus`, armed in `LandOn`, spent in `LandSpread`) · **Chain**: every
hit strikes one extra creature at 40% · **Cull**: +60% damage to creatures below 25%
health · **Swarmbane**: +6% damage per living creature in the wave · **Momentum**
*(side road, needs Sweep)*: every kill takes three-quarters of a second off every
cooldown — action economy fed by kills, a torrent in a Swarm and a trickle against a
Bruiser (sim field `CooldownRefundOnKillMs`, read in `LandOn` on any kill)

**Greaters (5)** · **Tide** *(new)*: cooldowns 4% shorter per living creature, hits
−15% — Swarmbane's cousin in the other currency (sim field `RatePerCreature`, read
through the sim's live rate at every cast) · **Cascade**: a kill makes the next hit
strike every creature · **Dispersal**: all Forms +1 target, −25% hit size ·
**Outnumbered** *(side road, needs Momentum)*: +9% damage per living creature, all
hits −15% — worse than nothing against one creature (×1.09 × 0.85), far better at six

**Mastery (8)** · **EVERYWHERE**: every skill strikes every creature, at 40% hit
size.
*Swarms evaporate. Anything with Defense becomes nearly immune, because 40% of a
small hit is below the flat mitigation floor.*

#### TEMPO — answers Caster

*Kill it before it acts. Every extra second against a Caster is a full hit taken.*

**Minors (1)** · **Brisk** *(new)*: the basic swing comes 20% sooner; skills keep
their own pace (sim field `AutoAttackRate`, read only where the swing's interval is
set) · Opener: +25% damage on the first hit against each creature · Hasten: +15%
skill rate · Preparation: the first skill of each wave has no cooldown · Focus: +8%
critical chance · **Flash** *(spur, off Focus)*: +20% for the first two seconds of each
wave — the capstone's shape at a minor's size, no price; two seconds because the wave
opens with a 700 ms breath

**Notables (3)** · **Rhythm** *(new)*: each cast makes the next come 8% sooner, up to
five times; a bite resets it — front-loading as a clock, fast in the Caster band and
nothing in the Bruiser band (sim field `CastRampPerCast` / `CastRampMax`, read through
the live rate; reset in the bite block) · **Alpha**: first hit on a creature ×1.9,
every later hit ×0.8 · **Mark Mastery**: Mark's window lasts 60% longer and hits 30%
harder · **Interrupt**: damage dealt in the run-up to an enemy swing +45% · **Surge**
*(side road, needs Flash)*: +40% for the first three seconds of each wave — the same
window as First Strike, so it deepens the capstone's opening rather than stretching it

**Greaters (5)** · **Opening Volley** *(new)*: each skill's first cast of a wave hits
+50%, every later cast −10% — per skill, which is neither Opener (per creature) nor
Flash (per second) (sim field `FirstCastMultiplier` / `LaterCastMultiplier`) ·
**Blitz**: −30% all cooldowns, −20% hit size · **Assassinate**: once per wave, a
creature below 30% health dies to the next hit · **Rush** *(side road, needs Surge)*:
cooldowns −20%, every bite deals +15% — speed bought in the opposed branch's currency

**Mastery (8)** · **FIRST STRIKE**: +150% damage for the first three seconds of each
wave, −40% after.
*Turns every wave into an opener. Bruisers, which cannot be killed in three seconds,
become the wall.*

#### ENDURE — answers Bruiser, and extends every other band

*The only branch that raises the ceiling of all four bands, which is why its nodes
are priced honestly rather than generously.*

**The heal ceiling (2026-08-26).** Playtest: "the health-recovery skills are far too
strong (broken)." Measured, a four-Transformation build was bitten for 117% of its pool
per wave and healed 118% of it back — the pool had stopped being what kept it alive —
and a fresh champion walked to wave 38 on Transformations where the first region is
tuned to end a run between waves 4 and 20. Two rules now govern every skill heal
(`HealTuning` in Core, read by `SoloBattle`):

- **Transformation leeches 12%** of the damage it deals (was 50%).
- **Skills can restore at most 40% of maximum health in one wave.** Leech, Feedback,
  the Transformation's leech, Siphon, the Nature signature and Second Wind all draw
  from this one allowance; healing at full health does not spend it. Recovery's
  between-wave regain is *not* under it — it is bounded by its own 20%, halved by the
  Endless band, and is the structural exception game-flow.md §3.3 already prices.
  Blood Magic and Juggernaut ("you cannot be healed") now refuse Recovery too.
- **Siphon** doubles the leech *and* raises the wearer's ceiling to 60% — measured,
  the doubling alone bought nothing under any ceiling.

Measured after (geared mid-career fixture, 16 seeds): a four-Transformation build
reaches a median depth 1.68× its no-heal twin and dies in every run; a four-Nature-Aura
build 1.68×; a fresh Transformation champion reaches wave 10 against its twin's 4.
`heal_balance_test` prints the before/after table on every run and pins the bands.

**Minors (1)** · **Mending** *(new)*: regain 0.3% of maximum health every second,
inside the fight and under the heal ceiling — the third kind of heal beside Leech (per
hit) and Second Wind (per clear) (sim field `RegenFraction`, read on the regeneration
tick) · Toughness: +15% health · Leech: heal 1.5% of damage dealt · Padding: −5 flat
damage from every hit taken · Second Wind: heal 6% of max health on wave clear ·
**Thick Skin** *(spur, off Second Wind)*: −4 flat damage from every hit taken — adds to
Padding, nine off every bite with both

**Notables (3)** · **Payback** *(new)*: every bite makes your next skill hit 15%
harder, up to four times — Rhythm's mirror across the axis; Endure builds up *from*
being bitten (sim field `BiteFuelBonus` / `BiteFuelMax`, banked in the bite block,
spent in `LandSpread`) · **Recovery**: regenerate 15% of max health between waves —
the flow doc's default is no regeneration at all, so this node is a structural
exception · **Absorb**: mitigation scales with missing health, up to −30% at 10% ·
**Fortify**: the first hit taken each wave deals nothing · **Thorns** *(side road,
needs Thick Skin)*: every creature that bites you takes 15% of its own bite back —
read against the raw bite so the rest of the branch cannot shrink it; lands like
poison, through armour, never a skill hit (sim field `ReflectFraction`, read in the
enemy-bite block)

**Greaters (5)** · **Rebound** *(new)*: a fifth of every bite that lands comes back as
health; damage dealt −15% — read against the bite after the branch's cuts, under the
heal ceiling (sim field `HealOnBiteFraction`) · **Bulwark**: −30% damage taken, −15%
damage dealt · **Bastion**: +1% damage per 250 max health · **Brace** *(side road,
needs Thorns)*: damage taken −20%, cooldowns +20% — the same wall as Bulwark at a
different price, so the two are not one price twice

**Mastery (8)** · **ENDLESS**: full health restored between every wave; maximum
health halved.
*The depth-pusher's node. It converts a health pool into a per-wave allowance, which
is strictly better the deeper you go and strictly worse in a single hard wave.*

#### Bridges (6 each)

- **Executioner** (Weight ↔ Tempo): the first hit on each creature +10%, and it cuts
  9 armour.
- **Volley** (Tempo ↔ Spread): +1 target and −20% cooldown, −25% hit size.
- **Feedback** (Spread ↔ Endure): heal 1.2% of max health per creature struck.
- **Anchor** (Endure ↔ Weight): +1% hit size per 200 max health.

#### Form specialisations (6 each)

One per Form, hanging off its axis. Each grants a Form-combo trigger the item
enchantments already define, plus a shape change:

| Form | Axis | Grants | Shape change |
|---|---|---|---|
| Strike | Weight | `Execute` | +40% damage to creatures below 35% health |
| Trap | Weight | `Coiled` | Traps re-arm on enemy hit rather than on a timer |
| Projectile | Spread | `Overdraw` | +1 target |
| Aura | Spread | `Radiance` | Ticks 30% faster |
| Mark | Tempo | `Linger` | Window applies to every creature in the wave |
| Transformation | Endure | `Siphon` | Leech doubled, and the per-wave heal limit rises 40% → 60% |

> This is also how the tree stops giving away what the Trait Tree sells. Today the
> mastery tree hands out triggers for free while the Dust tree charges a permanent
> tax for the same ones. Here, the Skill Tree grants only the six **Form-combo**
> triggers — which are dead weight without their Form, so they are a specialisation
> rather than a gift — and every general trigger (`Echo`, `Bloodlust`, `Undying`,
> `Venom`, `Splinter`, `Harvest`, `Desperation`, `Zeal`, `NoHealing`) belongs to the
> Trait Tree alone.

### 3.4 Trait Tree — shape and budget

**Income** (`Game1.TraitPointsEarned`). One point per region conquered, **two** per
corruption tier reached (the peak — going shallower never costs a point), and one per
region-mastery level held in each region. At the current content that is roughly
**34 points**, arriving in lumps rather than a stream.

**Names say what a node does, effect first** (traits pass, 2026-08-25). The playtest
could not connect "SHARP EDGE" or "THE GLASS ROAD" to the fight, so every node is now
named after its effect ("HARDER HITS I", "AUTO-SELL COMMON DROPS") and every keystone
gate after the keystone it teaches ("KEYSTONE — GLASS CANNON"). Ids never change —
saves store them — and `TraitNamesTests` pins the ids, forbids the old metaphors, and
holds each road's identity sentence (`TraitRoads`) against what its nodes do.

**The spine (cheap, structural).** The roots below the ground line, which every
player grows. It grants capacity and never a number:

| Node (id) | Cost | Grants |
|---|---|---|
| KEYSTONE SOCKET II (`socket_2`) | 2 | A second keystone socket |
| FIFTH SKILL SLOT (`weave_5`) | 3 | A fifth skill slot |
| KEYSTONE SOCKET III (`socket_3`) | 5 | A third keystone socket |
| LEARN VOWS I / II / III (`vow_study_1..3`) | 1 / 2 / 2 | Two vows each, gentlest first |
| GEAR-SLOT VOWS (`vow_binding`) | 3 | The three vows that each leave one gear slot empty |
| SACRIFICE VOWS (`vow_sacrifice`) | 3 | Four vows that always cost something |
| OPENS AUTO-SELL AND FORGE (`ledger`) | 1 | A gate: auto-selling, the forge upgrades, the Avarice road |
| AUTO-SELL COMMON DROPS / UNCOMMON DROPS (`filter_common` / `filter_uncommon`) | 2 / 3 | Drops at or below that rarity are sold on sight |
| OPENS THE FORGE UPGRADES (`forge_insight`) | 2 | A gate to the two forge nodes |
| SALVAGE PAYS 15% MORE (`efficient_forge`) | 3 | Salvaging returns 15% more material |
| AUTO-MERGE SPARE ITEMS (`auto_merge`) | 2 | The forge merges spares by itself after every expedition |
| FASTER REGION MASTERY I–IV (`recall_1..4`) | 1 / 2 / 2 / 3 | Region mastery grows 5% faster each, 20% in all (`RegionAutomation.RecordActiveKill`) |
| A MARK — NO EFFECT (`attunement`) | 4 | Needs the head of all four roads plus the third socket; changes nothing |

**Four roads.** Each is three keystones and a terminal, at 4 + 6 + 8 + 12 = **30**,
plus a side strand of a CHARGE-spur keystone (6) and three small attribute nodes
(2 + 3 + 4) that hang off rungs of the road and never sit on the terminal's path —
**45** a road.

```
spine (46) + 4 roads x 45 (180) = 226 total cost
~34 points available            = 15%
```

Two full paths cost 60 — more than the whole budget. **Two terminals is not
affordable; one terminal plus most of a second path is.** That is deliberate: the
terminal you did not take is the permanent shape of your character.

### 3.5 Trait Tree — the four paths

Each road is built from the keystones in `Keystones.cs`. They were written to the
right rule — every one gives something and takes something — so the design here is
arrangement, not replacement. Every keystone gate is named `KEYSTONE — <name>`,
because teaching that keystone is the node's whole effect. Each road prints a
one-line identity under its header on the screen and in the detail panel; the four
sentences live in `TraitRoads` and a test holds them against the nodes.

**RUIN — "Hit harder. Live closer to death."**
`KEYSTONE — GLASS CANNON` (4) → `KEYSTONE — BLOODLUST` (6) → `KEYSTONE — BLOOD MAGIC`
(8) → **`KEYSTONE — REAPER`** (12). Beside the road: `KEYSTONE — REND` (6, the CHARGE
spur) and `HARDER HITS I / II / III` (+5% / +6% / +12% damage; 2 / 3 / 4).
Damage doubles or scales as health falls; healing is traded away; the terminal makes
kills feed kills. A Ruin hunter lives at low health on purpose, which makes the Endure
branch of the Skill Tree nearly worthless to them — the two trees interact rather
than stack.

**AEGIS — "Live longer. Strike less often."**
`KEYSTONE — IRONCLAD` (4) → `KEYSTONE — JUGGERNAUT` (6) → `KEYSTONE — UNDYING` (8) →
**`KEYSTONE — TITAN`** (12). Beside the road: `KEYSTONE — DYNAMO` (6) and
`MORE HEALTH I / II / III` (+5% / +6% / +12% health; 2 / 3 / 4).
Double or triple health, paid for in skill speed. Aegis pairs naturally with Endure
and is the only road that makes Bruiser bands routine.

**AVARICE — "More loot and rarer loot. Softer hits."**
`KEYSTONE — GREED` (4) → `KEYSTONE — DISCERNING EYE` (6) → `KEYSTONE — FORTUNE` (8) →
**`KEYSTONE — HOARDER`** (12). Beside the road: `KEYSTONE — LODESTONE` (6),
`MORE LOOT I` (+5% loot; 2), `RARER FINDS I` (+6% rarity; 3) and `MORE AND RARER LOOT`
(+8% loot, +5% rarity; 4).
This road buys almost no combat power, which is what makes it a real choice: it trades
depth for the gear that eventually buys depth. `HOARDER` stops it being a dead end —
the haul becomes hit size.

**ARTIFICE — "Skills act differently. Vows pay more."**
`KEYSTONE — ECHO` (4) → `KEYSTONE — VENOMANCER` (6) → `VOWS PAY 25% MORE` (8) →
**`KEYSTONE — WEAVER`** (12). Beside the road: `KEYSTONE — CAPACITOR` (6) and
`FASTER SKILLS I / II / III` (+5% / +6% / +12% skill speed; 2 / 3 / 4).
Every skill fires twice, skills poison, vows pay more, and the terminal fires every
skill as the next Form it carries. Artifice is the road for players who want their
build to do something strange rather than something large.

Note that "more skills, more vows, a cheaper forge" is the **spine**, not Artifice:
skill slots, vow studies and the forge nodes are roots every hunter grows.

### 3.6 Rules both trees obey

1. **No node may be a bare multiplier.** Every node changes a shape — hit size,
   target count, cooldown, threshold, trigger — or is an explicit trade. The audit
   found 31 of 43 current mastery nodes were flat percentages; that ratio inverts.
   Connective minors may carry a number, but each one still moves a *shape* (hit
   size, rate, targets), never a bare `+x% damage`.
2. **Every mastery and every terminal is a trade.** It must make at least one thing
   worse. This is already enforced for keystones by test (`Keystones.cs` requires a
   value below 1); the same test extends to Skill Tree masteries.
3. **The budget invariant governs additions.** New nodes ship with the points that
   keep the reachable fraction near a third. A content update that adds nodes without
   adding depth to earn from is a stealth nerf; one that adds points without nodes
   dissolves the choice.
4. **Respec asymmetry is the whole distinction.** Skill Tree: free, instant,
   unlimited. Trait Tree: never. If either rule softens, the two trees collapse into
   one screen with different colours.

---

## 4. Formulas

### 4.1 Skill point income

    skillPoints = Σ over regions of floor(0.9 × sqrt(bestDepth_region))

Per region: wave 25 → 4, wave 40 → 5, wave 64 → 7, wave 100 → 9, wave 150 → 11,
wave 225 → 13. Six regions at depth 150 → 66 points; at depth 225 → 78. Tree cost
256 → 26–30% reachable (the code's guard is 25–38%). (Was `floor(depth / 5) + 3`,
linear with three free points — playtest 2026-08-25 reached a specialisation inside
fifteen minutes on it. The square root pays fast early and slows for ever after,
which is the shape a wall should have. The scale went 0.8 → 0.9 on 2026-08-27 when
the tree grew from 220 to 256; wave 40 still pays 5, short of the 10 a
specialisation's shortest path costs. The Mastery screen itself opens at wave 25,
the fifth boss, not wave 8: the tree is a mid-game workbench, not a first errand.
Code: `MasteryPoints`.)

### 4.2 Trait point income

    traitPoints = conquests + corruptionTiers + masteryGoalsCompleted

At current content: 6 + ~10 + ~20 = 36. Tree cost 137 → 26.3% reachable.

### 4.3 Branch affordability

    fullBranch      = 6x1 + 5x3 + 4x5 + 1x8 = 49
    sideRoad        = 1 + 1 + 3 + 5 + 8    = 18   (outer minor, spur, notable, greater, capstone)
    twoFullBranches = 98 > 66–78 available

Two complete branches are exactly, deliberately out of reach. A player may own one
branch and 17–29 points of anything else.

### 4.4 The trade test (enforced in code)

For every mastery and terminal node `n`:

    ∃ field f in n.Mods where f < 1.0   OR   n grants a BuildTrigger with a stated cost

A node failing this is an attribute node wearing a hat.

---

## 5. Edge Cases

| Situation | What happens |
|---|---|
| Player respecs the Skill Tree mid-expedition | Blocked. The build is locked at descend (flow §3.1); the tree screen is readable but not editable. |
| Skill points reduced (content rebalance lowers a region's depth cap) | Allocation is preserved and the player goes into debt; nodes stay lit but no new node may be taken until the debt clears. Never silently un-take a node. |
| A Form specialisation is taken for a Form not equipped | Legal and inert. The tree does not know or care what is equipped, and locking it would make respeccing gear a tree operation. |
| Two masteries taken whose effects contradict (`OVERWHELM` + `EVERYWHERE`) | Both apply in order: hit size is reduced to 40% by EVERYWHERE, then OVERWHELM's 60 threshold is tested against the reduced value. The combination is deliberately terrible and is left legal — a player who buys 16 points of mutually destructive masteries has made a real, informative mistake. |
| `ENDLESS` taken with `Recovery` | Both apply; `ENDLESS` overwrites to full, so `Recovery` becomes redundant. Redundancy is allowed, contradiction is allowed; hidden overrides are not — the tree screen marks a node as superseded. |
| A trait terminal is affordable but its path is not owned | Prerequisites are absolute. Cost is not the only gate. |
| Keystone socket count falls below equipped keystones | Cannot happen — sockets are only ever granted. If a rebalance removes one, extra keystones unequip in reverse purchase order and the player is told. |
| All mastery goals complete, no corruption tiers left | Trait income stops. This is the intended end state of the current content, and the trait tree is deliberately still incomplete at that point. |

---

## 6. Dependencies

| System | Relationship |
|---|---|
| `game-flow.md` | Defines the four archetypes these branches answer, the point income, and the budget invariant. |
| `resonance-weaving-system.md` | Supplies the six Forms. **Needs `targets` per Form** so Spread has something to modify. |
| `memory-dust-prestige-system.md` | Is the Trait Tree. Its `MemoryDustUnlock` record already carries `Mods`, `Requires` and `GrantsKeystone` — the engine stays, the catalogue is replaced. |
| `item-data-schema.md` | Item enchantments grant the same `BuildTrigger` vocabulary; Form specialisations must not duplicate an enchant a player can also find. |
| `the-forge-system.md` | AVARICE is only a real path if the Forge can absorb what it earns. |

---

## 7. Tuning Knobs

| Knob | Default | Safe range | Affects |
|---|---|---|---|
| Ring costs (1/3/5/8) | — | ±2 each | How sharply specialisation is forced. Flattening these makes breadth affordable and dissolves the branch identity. |
| `fullBranch` vs available points | 49 vs 66–78 | keep 1.4–1.8× | Whether two branches are reachable. Below 1.4 a player owns two and the opposed pairs stop opposing; above 1.8 a career does not buy one branch and a real second start. |
| Side-road length | 4 nodes (spur → notable → greater → capstone) | 3–4 | How long the "other way round" is. Shorter than 3 and the spur is a fifth petal in the fan; the walk to a specialisation speeds back up. |
| Bridge cost | 6 | 4–10 | The price of hybridising. Cheap bridges are the fastest way to make every build the same. |
| Terminal cost | 12 | 10–16 | How many permanent identities a player accumulates over the game's life. |
| `OVERWHELM` threshold | 60 | 40–100 | Which builds it deletes. |
| `EVERYWHERE` hit fraction | 0.40 | 0.30–0.55 | Whether it can also handle Armoured (it should not). |
| `ENDLESS` health penalty | 0.50 | 0.40–0.65 | Depth-push power vs single-wave survival. |

---

## 8. Acceptance Criteria

1. No player can own two complete Skill Tree branches at current maximum content.
2. Both trees sit between 25% and 45% purchased at full current progress.
3. Every mastery and terminal node fails a "strictly better than not taking it" test —
   verified by the existing keystone trade test, extended to Skill Tree masteries.
4. Fewer than 25% of Skill Tree nodes are pure numeric bonuses with no shape change.
5. A build that owns WEIGHT to mastery reaches a deeper band against Armoured than
   against Swarm, and the inverse for SPREAD — at least one full band of difference.
6. Respeccing the Skill Tree costs nothing and takes no time; the Trait Tree offers
   no respec at any price.
7. No `BuildTrigger` outside the six Form-combo triggers is obtainable from the Skill
   Tree.
8. Two independent builds that both reach the same depth differ in at least six
   allocated nodes — if the deepest builds converge, the opposed pairs have failed.
