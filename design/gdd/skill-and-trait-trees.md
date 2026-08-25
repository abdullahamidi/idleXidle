# Skill Tree and Trait Tree

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
**Tempo**, **Endure** — arranged as two opposed pairs. A third of the tree is
affordable at full progress, which buys one branch outright and most of a second.

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
| Endure | Transformation | Leeches 50% — it is the sustain Form |

### 3.2 Skill Tree — shape and budget

**Ring structure.** Each of the four branches has four rings that get more expensive
and more consequential as they go out:

| Ring | Count | Cost each | What it is |
|---|---|---|---|
| 1 — minors | 4 | 1 | Small shape changes. Connective tissue, but each one still moves a shape, not just a number. |
| 2 — notables | 3 | 3 | A real behaviour. This is where a branch starts to have an opinion. |
| 3 — greaters | 2 | 5 | A behaviour with a visible cost attached. |
| 4 — mastery | 1 | 8 | The branch's identity, and always a trade that makes some builds impossible. |

A full branch costs **31 points**.

**Bridges.** Between each pair of adjacent branches sits one bridge node, cost **6**,
requiring ring 2 of both neighbours. Bridges let a player hybridise, but at a price
that means hybridising costs them a mastery.

**Form specialisations.** Six sub-branches, one per Form, cost **6** each, each
hanging off the axis its Form belongs to. Taking one deepens that Form specifically
rather than the axis generally.

**Totals.**

```
4 branches x 31        = 124
4 bridges x 6          =  24
6 Form specialisations =  36
                        ----
total tree cost         184
```

Against roughly **60 points** at full current content (six regions, depth 150
each, on the square-root curve of §4.1) that is about **a third** — the invariant
from flow doc §4.4. One branch outright plus most of a second, or two branches to
ring 3 and nothing else.

### 3.3 Skill Tree — node catalogue

Names are working names. What matters is the effect and the trade.

#### WEIGHT — answers Armoured

*Fewer, larger hits. Flat mitigation is the enemy this branch exists to beat.*

**Minors (1)** · Heavy Hand: +25% hit size, −15% skill rate · Sharpened: hits
subtract 8 from enemy Defense before mitigation · Follow Through: first hit on each
creature +30% size · Deliberate: −20% skill rate, +30% hit size

**Notables (3)** · **Sunder**: a hit above 200 permanently reduces that creature's
Defense by 20 for the rest of the wave · **Crush**: hits above the wave's average
enemy Defense × 8 ignore half of it · **Breaker**: part-break damage doubled, and
part-breaks now also pay a chest-grade step (flow §3.5)

**Greaters (5)** · **Monolith**: all damage is delivered in half as many hits, each
twice as large · **Siege**: +45% damage to Armoured, −25% to everything else

**Mastery (8)** · **OVERWHELM**: hits below 60 damage deal nothing at all; hits
above 60 ignore enemy Defense entirely.
*This is the branch in one node: armour stops existing, and so does any build that
delivers its damage in small pieces.*

#### SPREAD — answers Swarm

*Target count and action economy. Losing to five creatures at once is this branch's
enemy.*

**Minors (1)** · Wide: Projectile +1 target · Diffuse: Aura +1 target · Quick Hands:
−15% cooldowns · Ricochet: 30% chance each hit strikes a second creature for half

**Notables (3)** · **Chain**: every hit strikes one extra creature at 50% ·
**Cull**: +80% damage to creatures below 25% health · **Swarmbane**: +8% damage per
living creature in the wave

**Greaters (5)** · **Cascade**: a kill makes the next hit strike every creature ·
**Dispersal**: all Forms +1 target, −25% hit size

**Mastery (8)** · **EVERYWHERE**: every skill strikes every creature, at 40% hit
size.
*Swarms evaporate. Anything with Defense becomes nearly immune, because 40% of a
small hit is below the flat mitigation floor.*

#### TEMPO — answers Caster

*Kill it before it acts. Every extra second against a Caster is a full hit taken.*

**Minors (1)** · Opener: +35% damage on the first hit against each creature ·
Hasten: +20% skill rate · Preparation: the first skill of each wave has no cooldown
· Focus: +10% critical chance

**Notables (3)** · **Alpha**: first hit on a creature ×2.2, every later hit ×0.75 ·
**Mark Mastery**: Mark's window is twice as long and 40% stronger · **Interrupt**:
damage dealt during an enemy windup is amplified 60%

**Greaters (5)** · **Blitz**: −40% all cooldowns, −25% hit size · **Assassinate**:
once per wave, a creature below 40% health dies to the next hit

**Mastery (8)** · **FIRST STRIKE**: +180% damage for the first three seconds of each
wave, −45% after.
*Turns every wave into an opener. Bruisers, which cannot be killed in three seconds,
become the wall.*

#### ENDURE — answers Bruiser, and extends every other band

*The only branch that raises the ceiling of all four bands, which is why its nodes
are priced honestly rather than generously.*

**Minors (1)** · Toughness: +20% health · Leech: heal 2% of damage dealt · Padding:
−6 flat damage from every hit taken · Second Wind: heal 8% of max health on wave
clear

**Notables (3)** · **Recovery**: regenerate 20% of max health between waves — the
flow doc's default is no regeneration at all, so this node is a structural exception
· **Absorb**: mitigation scales with missing health, up to −40% at 10% · **Fortify**:
the first hit taken each wave deals nothing

**Greaters (5)** · **Bulwark**: −35% damage taken, −20% damage dealt · **Bastion**:
+1% damage per 200 max health

**Mastery (8)** · **ENDLESS**: full health restored between every wave; maximum
health halved.
*The depth-pusher's node. It converts a health pool into a per-wave allowance, which
is strictly better the deeper you go and strictly worse in a single hard wave.*

#### Bridges (6 each)

- **Executioner** (Weight ↔ Tempo): the first hit on each creature is also a Weight
  hit — it gets Weight's size bonuses even from a Tempo build.
- **Volley** (Tempo ↔ Spread): +1 target and −25% cooldown, −30% hit size.
- **Feedback** (Spread ↔ Endure): heal 1.5% of max health per creature struck.
- **Anchor** (Endure ↔ Weight): +1% hit size per 150 max health.

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
| Transformation | Endure | `Siphon` | Leech doubled |

> This is also how the tree stops giving away what the Trait Tree sells. Today the
> mastery tree hands out triggers for free while the Dust tree charges a permanent
> tax for the same ones. Here, the Skill Tree grants only the six **Form-combo**
> triggers — which are dead weight without their Form, so they are a specialisation
> rather than a gift — and every general trigger (`Echo`, `Bloodlust`, `Undying`,
> `Venom`, `Splinter`, `Harvest`, `Desperation`, `Zeal`, `NoHealing`) belongs to the
> Trait Tree alone.

### 3.4 Trait Tree — shape and budget

**Income.** Six region conquests, one per corruption tier, and one per mastery goal.
At the current content that is roughly **36 points**, arriving in lumps rather than
a stream.

**The spine (cheap, structural).** A short chain from the centre that every player
walks, granting capacity rather than power:

| Node | Cost | Grants |
|---|---|---|
| Second Socket | 2 | A second keystone socket |
| Fifth Weave | 3 | A fifth skill slot |
| Third Socket | 5 | A third keystone socket |
| Vow Discipline | 3 | A second Vow may be active |
| Warren Charter I–III | 2 each | One extra Warren facility slot each |

**Four paths.** Each is three keystones deep, ending in a terminal that costs
**12**. A path costs 4 + 6 + 8 + 12 = **30**.

```
spine (17) + 4 paths x 30 (120) = 137 total cost
36 points available            = 26%
```

Two full paths cost 60 — more than the whole budget. **Two terminals is not
affordable; one terminal plus most of a second path is.** That is deliberate: the
terminal you did not take is the permanent shape of your character.

### 3.5 Trait Tree — the four paths

Each path is built from the keystones that already exist in `Keystones.cs`. They
were written to the right rule — every one gives something and takes something — so
the design here is arrangement, not replacement.

**RUIN — power bought with safety.**
`GLASS CANNON` (4) → `BLOODLUST` (6) → `BLOOD MAGIC` (8) → **`REAPER`** (12)
Damage scales as health falls; healing is traded away; the terminal makes kills feed
kills. A Ruin hunter lives at low health on purpose, which makes the Endure branch of
the Skill Tree nearly worthless to them — the two trees interact rather than stack.

**AEGIS — the wall.**
`IRONCLAD` (4) → `JUGGERNAUT` (6) → `UNDYING` (8) → **`TITAN`** (12)
Mitigation, immovability, and a terminal that trades speed for mass. Aegis pairs
naturally with Endure and is the only path that makes Bruiser bands routine.

**AVARICE — the economy build.**
`GREED` (4) → `DISCERNING EYE` (6) → `FORTUNE` (8) → **`HOARDER`** (12, new)
Haul, rarity and drop quality. This path buys no combat power at all, which is what
makes it a real choice: it trades depth for the gear that eventually buys depth.
`HOARDER` is the missing terminal — it should convert surplus haul into a
build-relevant resource so the path pays off in something other than money.

**ARTIFICE — behaviour over numbers.**
`ECHO` (4) → `VENOMANCER` (6) → *Form-combo mastery* (8) → **`WEAVER`** (12, new)
The trigger path: every skill fires twice, skills poison, and the terminal should
let a build run **two Forms in one slot**. Artifice is the path for players who want
their build to do something strange rather than something large.

Two terminals are missing and must be authored (`HOARDER`, `WEAVER`); the other
eleven keystones already exist.

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

    skillPoints = Σ over regions of floor(0.8 × sqrt(bestDepth_region))

Per region: wave 25 → 4, wave 40 → 5, wave 100 → 8, wave 150 → 9, wave 225 → 12.
Six regions at depth 150 → 54 points; at depth 225 → 72. Tree cost 184 → roughly
a third reachable. (Was `floor(depth / 5) + 3`, linear with three free points —
playtest 2026-08-25 reached a specialisation inside fifteen minutes on it. The
square root pays fast early and slows for ever after, which is the shape a wall
should have. The Mastery screen itself opens at wave 25, the fifth boss, not wave
8: the tree is a mid-game workbench, not a first errand. Code: `MasteryPoints`.)

### 4.2 Trait point income

    traitPoints = conquests + corruptionTiers + masteryGoalsCompleted

At current content: 6 + ~10 + ~20 = 36. Tree cost 137 → 26.3% reachable.

### 4.3 Branch affordability

    fullBranch      = 4x1 + 3x3 + 2x5 + 1x8 = 31
    twoFullBranches = 62 > 60 available

Two complete branches are exactly, deliberately out of reach. A player may own one
branch and 29 points of anything else.

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
| `fullBranch` vs available points | 31 vs 60 | keep 1.8–2.1× | Whether two branches are reachable. Below 1.8 a player owns two and the opposed pairs stop opposing. |
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
