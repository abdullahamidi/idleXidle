# Game Flow

*Created: 2026-08-12*
*Status: Complete draft — all 8 sections written, awaiting implementation planning*

> **This document supersedes the end-to-end loop described in `game-concept.md`,
> `combat-encounter-system.md` and `expedition-auto-battle.md`.** Those describe a
> weak-point/telegraph action game that has zero implementation in `src/` and is no
> longer the design. See `design/system-audit-2026-08-12.md` for what the game
> actually is today.

---

## 1. Overview

IDLExIDLE is a **build laboratory with an automatic benchmark**. The player
assembles a Hunter — four woven skills, two permanent trees, eight gear slots — and
sends it down into a region. The descent runs itself: no targeting, no abilities to
fire, no retreat. It ends when the Hunter dies, and it reports how deep it got and
*why it stopped*. The player reads that report, changes the build, and descends
again.

Nothing is lost on death; the only cost is the time to descend again, and waves the
player has already proven are fast-forwarded so that cost stays small. Depth is the
score, and the reason a run ends is the game's core information.

The content is built to demand **different** builds rather than a bigger one. Every
ten waves the enemy composition and the active affix rotate, so a build tuned for
one band stalls in the next. Because both trees are on a hard point budget that can
never be completed, the player cannot answer every band at once — they specialise,
find their ceiling, and then decide what to give up in order to move it.

---

## 2. Player Fantasy

You are a strategist reading a machine you built.

The fantasy is not *"I dodged that"* — it is *"I know why I stopped at 23, and I
know what it costs to reach 30."* The satisfying moment is not a kill; it is the
moment a report tells you your damage is arriving in too many small pieces, you
trade a Projectile for a Trap and give up two mastery nodes to afford it, and the
next run ends at 31 for a completely different reason.

That loop should feel like tuning an engine, not like grinding. Every run is
information. A run that ends *earlier* than the last one is still useful if it ends
for a new reason.

The failure state to avoid is opacity. If the player cannot tell the difference
between "my build is wrong for this band" and "my numbers are just too small", the
game is a slot machine.

---

## 3. Detailed Rules

### 3.1 The expedition

An **expedition** is one descent into one region. It is the only place combat
happens and the only source of depth.

**Starting.** The player picks an unlocked region and descends. The build is
whatever is equipped at that moment; it cannot be changed until the expedition
ends.

**Descending.** The Hunter fights wave 1, then 2, then 3, and so on without limit.
Each wave is an enemy composition (§3.2). Clearing a wave pays its haul immediately
— haul is never at risk. Every 5th wave is a boss wave.

**No input.** There is no targeting, no ability button, no dodge, no retreat and no
pause. The only control on the screen is the speed selector, which multiplies the
playback rate of an already-decided result.

**Health is the run's budget.** The Hunter starts each expedition at full health
and **does not regenerate between waves**. Damage decides how *fast* the Hunter
clears; health decides how *deep* it gets. Sustain — leech, regeneration,
mitigation — is therefore a build axis with real weight rather than a rounding
error, and it is the axis a pure-damage build must consciously refuse.

**Ending.** An expedition ends when the Hunter's health reaches zero, or when a
wave cannot be cleared inside the wave tick ceiling (a *stall*). Both produce the
same post-run report (§3.4); a stall names the wall explicitly rather than letting
the player wonder why the numbers stopped moving.

**Cost of failure: time only.** Nothing is lost. Depth records, tree points,
currency, gear and unlocks all persist. The expedition simply ends.

**Fast-forward.** On re-entry to a region, every wave up to `bestDepth − 5` resolves
instantly: no animation, full haul paid, health attrition applied. Live playback
resumes for the last five proven waves and everything beyond. This is what keeps
"time only" an honest cost — without it, the price of testing a build change would
grow with progress until iteration stopped being worth it.

> **Design note.** Fast-forward is not a convenience feature. In a game with no
> in-run decisions, the loop *is* build → run → report → build, and the run is the
> slowest link. Anything that lengthens it without adding information is taxing the
> only thing the player is actually doing.

### 3.2 Wave composition and enemy archetypes

A wave is not one sprite. It is a **composition**: one to five creatures drawn from
the region's roster, each carrying a Source and an **archetype**.

The archetype is the load-bearing idea of the whole design. With combat automatic,
the only way a build can be *wrong* rather than merely *small* is if different
content punishes different build shapes. Four archetypes cover the four ways a
build can be shaped:

| Archetype | Composition | Punishes | Answered by |
|---|---|---|---|
| **Swarm** | 3–5 creatures, low health each, high combined damage | single-target skills, long cooldowns — you lose on action economy | multi-target Forms (Aura, Projectile), cooldown reduction, cleave |
| **Armoured** | 1–2 creatures, high Defense, moderate health | many small hits — flat mitigation eats each one | few large hits (Strike, Trap), armour-break, Mark amplification |
| **Caster** | 1–2 creatures, low health, very high damage on a long windup | slow kill speed — every extra second is a full hit taken | burst, interrupt triggers, Mark windows |
| **Bruiser** | 1 creature, high health *and* damage | nothing in particular — it is the honest DPS-and-sustain check | raw scaling, leech, mitigation |

**Armour must be flat, not multiplicative.** Enemy Defense currently reduces damage
by `100/(100+Defense)`, which scales every hit by the same factor and is therefore
completely indifferent to how many hits you land. Under that formula the Armoured
archetype cannot exist. Defense becomes a **flat subtraction per hit**, floored so a
hit always does something:

    hitDamage = max(rawHit * minFraction, rawHit - enemyDefense)

Ten hits of 40 against Defense 25 deliver 150; one hit of 400 delivers 375. That
difference is the archetype.

**Swarm must cost action economy.** A skill that hits one target spends its whole
cooldown killing one of five creatures while the other four keep attacking. Forms
therefore carry an explicit target count, and multi-target is a property a build
buys rather than something it always has.

**Sources still matter, but stop being free.** A composition draws creatures of
*mixed* Sources, so no single Source choice is strong against a whole wave. Picking
a Source is a bet on the region's weighting, not a lookup with one correct answer.

**Parts.** Armoured creatures carry a breakable plate. Enough damage concentrated
on it removes the flat mitigation for the rest of the fight and pays a part-break
reward (§3.5). Whether a build breaks parts is a consequence of its damage shape —
large hits break plates, small hits do not — so part-break income is another thing
a build either has or does not.

### 3.3 Depth bands and affix rotation

Depth is divided into **bands of ten waves**. A band sets two things: which
archetypes are common, and which **affix** is active.

Bands rotate on a fixed, authored cycle per region. The cycle is *learnable* — the
same region always presents the same ladder — because the player cannot react
inside a run and so must be able to plan before one.

An example region ladder:

| Band | Waves | Composition weighting | Affix |
|---|---|---|---|
| 1 | 1–10 | mixed, nothing dominant | none |
| 2 | 11–20 | Swarm-heavy | **Numbers** — every wave spawns one extra creature |
| 3 | 21–30 | Armoured-heavy | **Plated** — enemy Defense +50% |
| 4 | 31–40 | Caster-heavy | **Ritual** — enemy damage grows each wave within the band |
| 5 | 41–50 | Bruiser | **Endless** — leech and regeneration are halved |
| 6+ | 51+ | cycle repeats with a scaling multiplier | as above |

This is the structure that gives a scarce tree its meaning. A build that answers
Swarm perfectly walks band 2 and stops in band 3; the report says so; the player
decides whether to buy an answer to armour and what to give up for it. The ceiling
moves because the player made a trade, not because a number went up.

**Bands are not a difficulty curve.** They are a rotation of *demands*. Raw enemy
scaling continues underneath them, but the wall a player hits should almost always
be a demand they have not answered rather than a number they have not reached —
because only the first kind tells them what to do next.

### 3.4 The post-run report

With no in-run decisions, the report is the only moment the player can learn
anything. It is the most important screen in the game and should be designed
before the combat visuals, not after.

**It gives a diagnosis, never a prescription.** It tells the player *which demand
beat them* and shows the measurements that prove it. It never says which node to
buy. That line is what keeps theorycrafting in the player's hands while still
making the game teachable — a report that says "take Heavy Strike" has played the
game for them.

The report shows:

1. **Depth reached**, and whether it is a record for this region.
2. **The wall** — the wave that ended the run, its composition, the archetypes in
   it and the active affix. Named plainly: *"Wave 27 · Armoured x2 · PLATED."*
3. **The measurements**, taken over the last band only, because that is where the
   run actually failed:
   - damage delivered vs. damage absorbed by enemy mitigation
   - average hit size and hits per second
   - targets struck per skill activation, against creatures present
   - time-to-kill per creature vs. incoming damage per second
   - health lost per wave (the attrition curve)
4. **Haul** earned and **unlocks** crossed.
5. **What changed since the last run in this region** — the same measurements,
   diffed. This is what makes iteration legible: the player sees that their change
   raised average hit size by 60% and their wall moved from Armoured to Swarm.

**Every number in the report must map to something the player can change.** That
is the acceptance test for adding one. "61% of your damage was absorbed" points at
armour. "1.2 targets struck per cast against 4 creatures" points at action economy.
"22% health lost per wave" points at sustain. A number that does not point at a
lever is decoration and belongs somewhere else.

### 3.5 What an expedition pays

Three different things come out of a descent, and they deliberately buy three
different layers.

**Haul — pays gear.** Gleam and materials, credited per wave, never at risk. Boss
waves add a chest. This is the fast, continuous currency.

**Mastery points — pay the build tree.** One point per **first-time** depth
milestone (every 5 waves) in each region. Only the first time: farming a depth you
have already reached pays haul but no points. Pushing deeper is the only way to
earn them.

**Trait points — REMOVED 2026-09-04 with the Memory tree they paid for.** They were rare and
lumpy — region conquest, corruption tiers and mastery goals — and they bought a permanent passive
tree. That tree is deleted, nothing spends the currency, and a currency with no sink is a number
that means nothing. The progression those terms measured still happens; it simply pays keystones,
Vow capacity, sockets and skill slots directly (see `Unlocks`) instead of a point pool.

**Part-breaks** pay materials and raise the chest grade of the wave that produced
them. Whether a build breaks parts at all is a consequence of its damage shape
(§3.2), so this is income a build either has access to or does not — another
reason for large-hit builds to exist.

**The Warren pays neither kind of point.** It produces Gleam and materials only.
This is Pillar 3 in its revised form: idle buys gear, play buys identity. A player
who leaves the game running overnight comes back richer, not further along the
tree.

### 3.6 The two trees

The audit found three permanent trees all multiplying the same five numbers, with
the mastery tree handing out for free the triggers the Dust tree sold at a
permanent cost. The fix is not to merge them but to give them genuinely different
jobs.

**Mastery tree — scarce but fluid.** Buys how skills *behave*: target counts, hit
sizes, cooldowns, triggers, affinity. Points come from depth. **Respec is free and
instant.** The scarcity bites not because a choice is permanent but because the
player cannot have everything *at once* — so the tree is where they answer the band
in front of them. Re-solving it after a wall is the main activity of the game, and
charging for that would tax the thing the player is supposed to be doing.

**Trait tree — scarce and permanent.** Buys who the Hunter *is*: keystones,
structural unlocks, new build axes. Points come from conquests and goals.
**No respec.** Keystones sit at branch ends so reaching one costs the path to
another. These are the decisions the player lives with.

Two rules govern both:

- **Neither tree may be completed.** The point budget is authored so that clearing
  all current content reaches roughly a third of the nodes. When content is added,
  points and nodes grow together — the fraction is the invariant, not the count.
- **A node must change a shape, not just a number.** The audit found 31 of 43
  mastery nodes were flat percentage bonuses; a tree of those is a shopping list
  with a progress bar. Nodes change target counts, hit sizes, cooldowns, triggers
  and thresholds. Flat nodes exist only as connective tissue between real ones and
  are capped as a share of the tree.

### 3.7 Gear and the Forge

Gear is the third layer and the one idle time buys. It supplies the raw scaling
numbers plus, on each piece, one **trait** (a material property) and — on Rare and
above — one **enchant** (a behaviour trigger).

Three fixes the audit makes non-optional:

- **`PowerRating` must read what it ranks.** It currently reads a weapon
  multiplier, Defense and Health, and is blind to every trait and enchant — which
  means the EQUIP BEST button confidently equips the wrong item. Either it learns
  the whole item, or the button goes.
- **Rarity needs a sink.** It is a full build axis that is computed all the way
  through the simulation and then read by nothing. Rarity determines affix count
  and enchant availability.
- **Traits must interact with build shape.** HEAVY trading skill rate for hit size
  is a good answer to Armoured and a bad one against Swarm. That is the whole point
  of having traits; today they are flavour text on a number.

The Forge spends materials — the Warren's output — on upgrade (item level), reforge
(reroll the trait) and merge (combine duplicates). This is the loop that turns idle
time into build capability without letting idle time buy a decision.

### 3.8 The Warren as a gated idle layer

Today the Warren is eight always-unlocked, mechanically identical facilities that
mint every currency in the game from the first frame, on every screen and while the
game is closed. It out-produces every other faucet by three to four orders of
magnitude, which is why nothing in the game is ever scarce.

It becomes a **support layer whose ceiling is set by play**:

- **Facilities unlock by conquest.** One per region conquered, plus a few from
  mastery goals. A new player has one facility, not eight.
- **Each facility's level is capped by depth.** `maxLevel = floor(deepestDepth / 5)`.
  Idle output can never run ahead of the player's own progress, and a player who
  stops descending stops growing. This single rule is what converts the Warren from
  a printer into a reward for playing.
- **It produces Gleam and materials only.** Never mastery points, never trait
  points. Idle buys gear; play buys identity.
- **Facilities produce different material tiers**, so which one to raise is a real
  question tied to what the Forge currently needs rather than a fixed optimal order.
- **Offline pays the full rate, capped at a generous window.** The cap respects a
  player's time without letting an absence become the best way to play.

### 3.9 The long-term ladder

Four chase axes run in parallel, so a player who exhausts one still has somewhere
to go.

**Corruption tiers — the world deepens.** Once every region is conquered, the world
ratchets: enemy scaling rises, the loot tier rises, and each band gains a second
affix stacked on its first. The ladder has FIVE named tiers (STIRRING, RESTLESS, FEVERED,
RAVENOUS, ABYSSAL — `CorruptionScaling.MaxTier`); from the map it can be climbed DEEPER
and stepped back SHALLOWER, the world wears each tier (tinted creatures, an epithet on
the boss, a darker arena), and each tier pays its dust ONCE, the first time it is reached (it
paid trait points too, until those were removed with the Memory tree). Bounded, so that the climb reads as a climb and not a counter
(playtest 2026-08-23).

**Content unlocks — new shapes, not bigger numbers.** New regions, new creature
families, and above all new **Forms and Sources**. A new Form is a new build shape
and is worth more to this design than any multiplier, because the game's depth
lives in how a build is shaped rather than how large it is.

**Vows and keystones — restriction for power.** A Vow trades a condition for
strength on one skill. Because the player cannot react inside a run, a Vow's
condition must be a **build** condition rather than a reaction: *"this skill deals
+60% but cannot fire while any enemy is above 80% health"* rewards a build that
opens with something else; *"+80% but costs 15% of max health per cast"* plugs
directly into the health budget from §3.1. Vows are therefore another way for
content to demand a shape.

**Mastery goals — depth without content.** Authored challenges against the content
that already exists: reach depth N using a single Form, with a named Vow active,
with no flat nodes taken, or with nothing above Rare equipped. Each pays a trait
point. This is the cheapest depth in the game to author and the most demanding to
complete.

### 3.10 Session shape

**First ten minutes.** Descend, die somewhere around depth 8–12, read the report,
spend two mastery points, descend again, go deeper. The lesson being taught is not
"combat is automatic" — the player will work that out — but *the report is the
game*. If a new player does not open the report and then change something, the
onboarding has failed.

**First hour.** Reach the first region's conquest depth. That unlocks the second
region and the first Warren facility. Somewhere in here the player meets an
Armoured band and stalls for the first time on a *demand* rather than a number —
their first real build decision, and the moment the game explains itself.

**First week.** Three or four regions conquered. The mastery tree is perhaps a
quarter filled and has been respecced several times, because respeccing is free and
each new band asks a different question. The first keystone is chosen from the
trait tree and will not be given back. The Forge is turning Warren materials into
item levels.

**Long term.** Corruption tiers, mastery goals, and the real endgame problem: a
build that spans several bands instead of dominating one. A specialist reaches
band 3 quickly and stops; a generalist crawls but does not wall. Finding the shape
that does both is the thing the player is actually optimising.

---

## 4. Formulas

### 4.1 Flat mitigation (this is what makes Armoured exist)

    hitDamage = max(rawHit × MinHitFraction, rawHit − enemyDefense)

| Variable | Meaning | Range |
|---|---|---|
| `rawHit` | one hit's damage before enemy mitigation | 5 – 50,000 |
| `enemyDefense` | flat per-hit reduction, authored per creature | 0 – 400 |
| `MinHitFraction` | floor so a hit always lands for something | 0.10 – 0.25 (default 0.15) |

*Example.* Against `enemyDefense = 25`: ten hits of 40 deliver
`10 × max(6, 15) = 150`. One hit of 400 delivers `max(60, 375) = 375`. Same total
raw damage, 2.5× the result — that gap is the Armoured archetype.

This replaces the current `100/(100+Defense)` multiplicative curve, which scales
every hit identically and is therefore blind to hit count. Under the old formula
the Armoured archetype is not merely weak, it is unrepresentable.

### 4.2 Action economy (this is what makes Swarm exist)

    targetsStruck   = min(Form.targets, creaturesAlive)
    damagePerCast   = targetsStruck × hitDamage

`Form.targets` is authored per Form (Strike 1, Trap 1, Projectile 2, Aura all).
Raising it is something a build buys.

### 4.3 Depth to mastery points

    masteryPoints = Σ over regions of floor(bestDepth_region / DepthPerPoint)

`DepthPerPoint = 5`. Credited on **first** arrival only. Six regions at depth 50
each yields 60 points.

### 4.4 The budget invariant

    reachableFraction = totalPointsAvailable / totalNodeCost ≈ 1/3

This is the number that must hold, not the node count. When nodes are added, points
are added to preserve it; when a new region ships, its depth milestones are part of
the same equation. A build that can afford more than half a tree has stopped making
choices.

### 4.5 Warren ceiling

    facilityMaxLevel = floor(deepestDepthAnyRegion / 5)

### 4.6 Bands and affixes

    band(wave)  = floor((wave - 1) / WavesPerBand)          WavesPerBand = 10
    affix(wave) = region.affixCycle[band(wave) mod cycleLength]

### 4.7 Enemy scaling, with the boss bug fixed

    enemyHealth = base × EnemyScaleBase^wave × (isBoss ? BossHealthScale : 1)
    enemyDamage = base × EnemyScaleBase^wave

`EnemyScaleBase = 1.06`, `BossHealthScale = 2.2`. **The boss multiplier applies to
health only.** The code passed the same scale to both parameters, so every boss also
hit 2.2x harder than the wave before it — a spike nothing in the design asked for.

*Corrected 2026-08-12.* An earlier draft of this section claimed the bug made 20 of
21 runs end on a boss wave. That number came from the system audit and does not
reproduce: over a health sweep from 200 to 2000 the bug costs two to four waves of
depth, and the share of run-ending depths landing on a multiple of five is
unchanged. The fix is still correct — the code contradicted its own documentation,
and a boss that hits harder cannot be tuned separately from a boss that has more
health — but the justification is consistency, not a dramatic change in run shape.
**Fixed**, with a direct contract test rather than a statistical one.

### 4.8 Fast-forward threshold

    liveFrom = max(1, bestDepth_region - 5)

---

## 5. Edge Cases

| Situation | What happens |
|---|---|
| New save, no region unlocked | The first region is always unlocked; there is no state with nothing to descend into. |
| Hunter dies on wave 1 | The report still opens, shows depth 0, names the wave and pays nothing. It must not be blank — a first-run failure is exactly when the player most needs the report to explain itself. |
| A fast-forwarded wave would kill the Hunter | Fast-forward stops at that wave and plays it live. The player is watching the exact wave where their build got *worse*, which is the most useful thing that can be shown. |
| Player tries to change build mid-expedition | Not possible. The build is locked at descend; the gear and tree screens are readable but not editable until the run ends. |
| Respec while an expedition is running | Same as above — blocked, not queued. |
| All regions conquered, corruption tier 0 | The corruption ladder (SHALLOWER / DEEPER) becomes available on the map. |
| Warren facility at its depth cap | The upgrade button states the reason — "capped at level 9 by depth 47" — rather than greying out silently. |
| Points exceed total node cost | A content bug, not a player state. Assert in dev builds; the budget invariant (§4.4) is violated. |
| All creatures in a wave die to the same hit | The wave clears normally; overkill is discarded. |
| A part breaks on the hit that kills its creature | The part-break still pays. Denying it would make large-hit builds worse at the one thing they are for. |
| A wave cannot be cleared within the tick ceiling | Recorded as a **stall**, not a death. The report names it as a stall so the player knows their damage, not their health, is the wall. |
| Region changed mid-session | Depth is tracked per region; switching does not reset the other region's record or its fast-forward threshold. |

---

## 6. Dependencies

This document sets the frame; these systems supply or consume parts of it. Each
listed doc needs a matching back-reference.

| System | Relationship |
|---|---|
| `resonance-weaving-system.md` | Supplies Forms, Sources and Vows. **Needs a `targets` count per Form** (§4.2) and Vow conditions restated as build conditions rather than reactions (§3.9). |
| `item-data-schema.md` | Supplies traits and enchants. **Needs traits to interact with build shape** (§3.7). |
| `the-forge-system.md` | Consumes Warren materials; is the sink that makes idle time worth something. |
| `loot-drop-system.md` | **Needs a Rarity sink** (§3.7) — rarity currently has a source and no reader. |
| `region-mastery-automation-system.md` | Supersedes its automation staging with the depth-capped Warren (§3.8). |
| `memory-dust-prestige-system.md` | Becomes the **trait tree** (§3.6). No reset ever existed; the rename makes the code and the design agree. |
| `hunter-progression-system.md` | Its nine trainable stats are currently unreachable (`Hunter.Train()` has no player call site). Either they become a real spender or they are cut — the flow has no room for a frozen system four screens display. |
| `combat-encounter-system.md` | **Superseded.** Retained for enemy vocabulary only. |
| `expedition-auto-battle.md` | **Superseded.** |

---

## 7. Tuning Knobs

| Knob | Default | Safe range | Affects |
|---|---|---|---|
| `MinHitFraction` | 0.15 | 0.10 – 0.25 | How hard Armoured punishes small hits. Lower = sharper archetype, higher = gentler. Below 0.10 a small-hit build cannot function at all. |
| `WavesPerBand` | 10 | 5 – 20 | How often the demand rotates. Lower = faster variety but less time to feel a band; higher = long stretches of one question. |
| `DepthPerPoint` | 5 | 3 – 10 | Mastery point income. Directly moves the budget invariant — change with §4.4 in hand, not alone. |
| `reachableFraction` | 0.33 | 0.25 – 0.45 | How much of a tree a finished player owns. Above ~0.5 the trees stop being choices. |
| `EnemyScaleBase` | 1.06 | 1.04 – 1.08 | Run length. Compounds — small changes move the ceiling a long way. |
| `BossHealthScale` | 2.2 | 1.5 – 3.0 | Boss spike. **Health only** (§4.7). |
| `FastForwardMargin` | 5 | 3 – 10 | How many proven waves are replayed live. Lower = tighter iteration, higher = more context before the wall. |
| `WarrenDepthDivisor` | 5 | 3 – 10 | How fast idle capacity follows play. |
| `OfflineCapHours` | 8 | 4 – 24 | How much an absence is worth. |
| Region affix cycle | per region | — | The identity of a region. This is content, not a number: two regions with the same cycle are the same region. |

---

## 8. Acceptance Criteria

**Flow**

1. A new player can start an expedition, die, and see a report naming the wave, its
   composition and its affix — within 3 minutes of a new save.
2. The report shows at least one measurement that differs from the previous run in
   the same region, once a second run exists.
3. No screen offers an in-combat input other than playback speed.

**The archetype engine**

4. A build with `Form.targets = 1` and high hit size reaches a *measurably* deeper
   band against Armoured than against Swarm, and the inverse holds for a build with
   `Form.targets ≥ 3` and small hits. Deltas of at least one full band.
5. Ten hits of 40 against Defense 25 deliver less than half the damage of one hit
   of 400 (§4.1).
6. No single build reaches the maximum authored band of every region without a
   respec.

**Scarcity**

7. Clearing all shipped content leaves both trees between 25% and 45% purchased.
8. No Warren facility can exceed `floor(deepestDepth / 5)`.
9. Idle time alone — with zero expeditions — earns zero mastery points, verified over a
   simulated 24-hour absence. (It earned zero trait points too, until those were removed.)

**Regressions the audit found, which must not survive**

10. `Hunter.Train()` is reachable from the Stats screen, or the nine stats and the
    Hunter Level readout are removed from every screen.
11. `PowerRating` changes when an item's trait or enchant changes, or EQUIP BEST is
    removed.
12. `BuildMods.Rarity` is read by at least one drop calculation.
13. Boss waves multiply enemy health but not enemy damage.
14. Warren mastery and build-tree points are separate variables; spending one does
    not change the other.

---

## Addendum 2026-08-26 — conquest at 20, checkpoints, Memory Dust, VITALITY

**Conquest is wave 20** (was 7 — "seven waves is far too short to clear a map"). Past the bar the hunt's
banner reads `CONQUERED · OVERWAVE +N`, N being how far beyond 20 the current descent has gone.

**Checkpoints.** A conquered region offers a START AT WAVE choice on the Map: the top, and every ten
waves the champion has ever held there (`Checkpoints.Options`). A descent started after wave *w* skips
the waves below it and pays no haul for them. The chosen start is remembered per region in the save.

**Memory Dust's job.** A checkpoint start costs `Checkpoints.DustPerWave` (25) × *w* Dust **per
descent** (wave 30 → 750 Dust). Dust is minted by the Warren while the player is away and is now spent
buying back walked waves — a repeatable, scaling sink that grows with the content it serves. Its other
sink is Warren facility upgrades — and since the Memory tree was deleted those two sinks are the
whole of it. It buys nothing permanent. If the Dust for the
chosen start is not there, the descent starts from the top and the map's chip says so in ember.

**VITALITY is regeneration.** It no longer multiplies the health pool (that is HEALTH's job, plus gear).
Every second of a fight the champion regains `MaxHealth × VITALITY × 0.0003` (0.03% per point per
second; base value 0, so a fresh hunter regenerates nothing until trained). It flows through the same
`Heal()` funnel as every other heal, so BLOOD MAGIC's "no healing" and the ENDLESS band's halving apply.

**The cast lock is the cast clip.** One cast at a time: the sim's gap between casts is
`FormBehaviour.CastClipMs` (700 ms) ÷ the build's skill rate, and the hunt screen plays the cast clip
across exactly that window — a faster build casts and animates faster, and no cast begins before the
last clip ends.


## Addendum 2026-08-26 (round four) — the welcome chest, the first free gem, honest damage numbers, one frame

- **The welcome chest.** A new game starts with one gift chest in the VAULT (`GiftChests`, `Chest.Gift`):
  a Common, item level 1 weapon of the starting champion's class — for THE SEEKER, a plain NATURE
  BLADE from the Verdant Hollow. It opens the same on every seed and takes nothing from the random
  source. Old saves receive nothing. The VAULT's first-visit tour and the "open a chest" tutorial rung
  are therefore true from minute one.
- **The first gem is free.** `GemCraft.SocketCost` returns 0 until `SaveGame.FreeSocketUsed`; a save
  from before the field that already holds a socketed gem counts as used. The first gem drop writes a
  DISPATCH (`gem.first`), a NEW mark on FORGE and a two-card tour that opens the SOCKET tab
  (`Onboarding.GemTour`). It was a toast until 2026-09-16: background news is a letter that waits in
  the inbox, not a plate across whatever screen the drop happened to land on.
- **The damage number is the simulation's.** The hunt printed an invented figure (power × a multiplier
  × jitter, e.g. "-185" on wave one) at the row's centre; it now prints the event's amount over the
  creature that took it. The bars were always right.
- **Second champions.** THE THORNWALL (Bulwark) is earned by holding wave 80 in Marrow Wastes — an
  endurance hold no conquest can satisfy (its old "conquer every region" fired one region after the
  first Bulwark's own conquest). THE MAGPIE takes the thirty-chest quest; THE QUIVER takes wave 60 in
  the Verdant Hollow.
- **One frame standard.** In-screen panels wear the muted brown frame (`PanelQuiet`); the gold
  nine-slice is for modals only. The hunt's chest filter folds into a row that opens a popover.
- **Battle cues.** Hit and death sounds are layered impacts (sub thump with a pitch drop, band-passed
  body, transient, short tail) with per-play pitch variation and a per-cue cooldown; the boss has its
  own death cue. Measured: the hit's spectral centroid fell from 764 to 333 Hz, its decay grew from
  54 to 194 ms (`make_battle_sfx.py --measure`).

## Addendum 2026-08-26 (evening) — one action at a time; MIGHT swings, RESONANCE casts, TEMPO paces both

- **One action at a time.** The champion holds a single lock (`Champion.BusyUntilMs`) for every basic
  attack and every cast: the clip's length (700 ms) divided by the action rate. No animation is cut
  short; a ready skill goes next, never on top. The basic attack is filler — it yields when a casting
  skill will be ready before the swing would end.
- **Two damage stats, two jobs.** MIGHT multiplies the basic attack only (`AutoDamageMultiplier`, +1% per
  point on a base of 18). RESONANCE multiplies skills only (`SourceScalingCoefficient` 0.018, raised
  from 0.008 to carry alone what MIGHT and RESONANCE carried together). The weapon and worn item mods
  multiply every hit. TEMPO is ACTION SPEED: the swing's cadence, every cooldown and every animation.
- **Fewer, heavier casts.** Cooldowns rose (Strike 3000, Projectile 1500, Transformation 3500, Trap
  4000; Mark stays 4000) and each Form's base hit rose by the same factor (Strike 105, Projectile 33,
  Transformation 56, Trap 145), so damage per cooldown is unchanged and the swing is added on top.
- **Measured.** Mastery sweep: branch spread 1.38 (band 1.40; was 1.28). Heal probe: MORPH ×4 outlives
  its no-heal twin 2.53× (band widened to 2.6 — the twin, not the heal, moved; a MORPH follow-up).
  Basic attack share of a one-skill build's damage: ~a third (was 9–15%, and armour ate it whole).

## Addendum 2026-08-27 — THE BEAT: the champion acts on a metronome

- **One action per beat.** The champion acts only on the beat — `ExpeditionTuning.BeatMs` = 1500 ms ÷
  action speed (TEMPO) — and takes ONE action there: the first ready skill in slot order, else the basic
  attack (`SoloBattle.DefaultBeatMs`, `BeatFor`). A wave's first beat comes at the wave's breath (700 ms),
  then the metronome. Nothing happens between beats, so no animation is ever cut short; the hunt
  screen sizes every action clip to 0.65 of a beat (`FormBehaviour.ClipShareOfBeat`) and holds the last
  frame 150 ms before idle — the readable END of an action.
- **Two kinds of cooldown.** Rhythm skills count BEATS (`FormBehaviour.CooldownBeats`): Strike every 3rd
  action (opens on the second beat), Projectile every 2nd. Window skills count time: Transformation and
  Mark 4 s. Trap answers a bite; Aura ticks. MOMENTUM refunds beats to beat-counted skills.
- **Hits sized to the beat.** One action per 1.5 s is 0.67 actions/s where the old lock allowed 1.43, so
  every hit was scaled for the ceiling: Strike 210, Projectile 90, Transformation 86, Trap 145, the
  basic attack 32 (MIGHT's). Venom's bleed slowed to a quarter per half-second so a pool survives a beat.
- **The picture.** A creature that takes a blow flashes warm and rocks back 8 px for ~120 ms; a skill's
  medallion warms to gold over the 300 ms before its cast (the telegraph).
- **Measured.** Fresh champion reaches wave 4 (band 4–20); first rank affordable at 39 s (≤ 90); mastery
  sweep spread 1.44 (band widened 1.40 → 1.45); heal probe MORPH ×4 2.63 / + SIPHON 2.75 (band 2.8);
  Singular vow +2 waves once its fixture stopped one-shotting (overkill is discarded on the beat).
  Owed: a WEIGHT branch look and a MORPH look under the beat.

## Addendum 2026-08-27 (later) — ELEMENT SETS: worn pieces of one element unlock its rungs

- **The rule.** Every worn item carries an element (the Source of the region it dropped in). Wearing
  2 / 3 / 4 / 5 pieces of one element unlocks that element's set rungs (`ElementSets`, Core Economy).
  Eight slots are worn; a full five leaves three for anything; two elements can each reach two.
- **The same shape for every set,** so it is learned once: rung 2 and rung 4 make the element's OWN
  skills hit 8% harder each (16% at four; `SkillShape.SourceBonus`, read in `SoloBattle.Amp`); rung 3 is
  a plain stat in the element's character; rung 5 is the element's one special rule.
  - BODY: +10% maximum health · the basic attack hits 25% harder (`SkillShape.AutoAttackDamage`).
  - MACHINE: every bite deals 4 less · the first bite of every wave deals nothing.
  - MIND: +6% critical chance · a MARK's window lasts 50% longer.
  - NATURE: regain 0.3% of maximum health every second · heal 2% of the damage you deal.
  - SHADOW: +12% against creatures under 30% health · every kill takes a beat off your cooldowns.
  - SPIRIT: you act 6% faster · each skill's first cast of a wave hits 25% harder.
- **Where it reaches the fight.** `GearShape.Of(hunter)` folds the active rungs into the shape the sim
  combines with the build's; `SoloBattle.ChampionHealth` reads the gear shape's MaxHealth for the pool.
- **Where the player reads it.** The GEAR screen's loadout panel: a SETS row ("SPIRIT 3 OF 5"), and
  the set's card on hover — one plain line per rung, ON where reached. The Forge's element line is
  "SET PIECE · SHADOW". Every line ends in a full stop and names a number; no ornament (house rule).
- **Why sets.** The user chose them over attunement or resistances (2026-08-27): "set bonuses will add
  a nice depth" — a collection goal the vault's element glyphs already point at.
