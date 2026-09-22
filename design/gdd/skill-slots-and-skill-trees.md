# Skill Slots and Skill Trees — the 2026-08-29 rework

> **STATUS: CURRENT — the authoritative skill-system spec** (STYLE → SKILL → VARIATION →
> REINFORCEMENTS; RESONANCE / LOOT / TEMPO / ENDURE). Implemented: see §9b and the 2026-08-31
> refactor (P3–P9), which made SkillId the runtime identity and deleted Form from the sim.
> In-body passages that still say "not implemented" predate that.

## 1. Overview

The champion carries at most **two active and two passive skills** instead of four
undifferentiated ones. Progression unlocks TOTAL capacity (1 to 4); a skill brings its
own kind, the slot has none, and a build holds at most two of each — so at capacity 2
two Actives, one of each, or two Passives are all legal. There are **twelve skills, two per style** — one active and
one passive, distinct abilities rather than two faces of one — each unlocked by its
own node on the mastery tree, and each deepened by two variations and three
reinforcements bought with levels the skill earns from being used.

Build diversity moves from breadth to depth: fewer things equipped, each of them
shaped far more.

## 2. The problem this solves

Three complaints from the 2026-08-28 playtest turned out to be one defect.

**The basic attack is a leftover, not a right.** `SoloBattle.cs:1345` reads
`if (onBeat && !acted && ...)` — the champion swings only when no skill claimed the
beat. With four slots filled, the beat demand is:

| Skill | Cadence | Share of beats |
|---|---|---|
| Strike | every 6 beats | 0.17 |
| Projectile | every 4 beats | 0.25 |
| Mark | 8000 ms ≈ 5.3 beats | 0.19 |
| Transformation | 8000 ms ≈ 5.3 beats | 0.19 |
| **Total demand** | | **≈ 0.80** |

Four beats in five are somebody's cast. That is, at once: the animation chaos (the
champion's own swing clip almost never plays), the effect pile-up, and the reason
the cooldown knob is empty — `FormBehaviour.BaseCooldownMs` cannot be raised (the
wave-length ceiling, `test_cooldowns_are_sized_to_a_wave_not_to_each_other`), cannot
be lowered (`test_projectile_trades_weight_for_volume`), and any step made WEAVER a
net loss. Four skills drawing on one metronome is the cause; the cooldown numbers
were only the symptom.

**Two of the three identity axes produce no behaviour.** The game has Style, Source
and Vow. Only Style changes how a skill acts. Source resolves to a matchup
multiplier plus a garnish-sized signature (BODY +3% per wound, SHADOW +12% under
half health, MACHINE 1 armour per hit, MIND +250 ms of Mark window, NATURE 3%
leech, SPIRIT +15% to the next cast). Vow resolves to a percentage. Two thirds of
the identity system is arithmetic, which is why adding catalogue content does not
make the game feel deeper.

## 3. The model — three layers

`Form` currently does four jobs at once: identity (`AffinityFactor`), mastery
destination (the six specialisation nodes), behaviour (`CooldownBeats`, `Targets`,
`IsPassive`) and damage (`FormBaseValue`). The first two are **style**; the last two
are **skill**. Splitting them is the rework.

```
STYLE  (6 — the hexagon, affinity, the mastery roads)   <- identity. Fixed at six.
   ^ belongs to
SKILL  (6 — one per style, each with its own tree)      <- Source + behaviour + Vow
   ^ equipped in
SLOT   (capacity 1-4; at most 2 active and 2 passive across them)
```

Mastery buys **style** advantage, never skill advantage: walking the HAMMER road
strengthens every hammer skill, including ones added later.

### The data shape

```csharp
SkillDef {                    // catalogue entry, identical for every player
    Id, Name,
    Style,                    // one of six  -> the ONLY field mastery and affinity read
    Kind,                     // Active | Field | Reaction — set by the tree's ring 0
    Effect,                   // Damage | Amplify | Heal
    Timing,                   // beats between casts, or ms between ticks
    On,                       // for a Reaction: Kill | Bitten | LowHealth | WaveStart
    Targets, BasePower,
    ClipKey, FxKey
}

WovenAbility { Name, Source, SkillId, Vow }   // SkillId replaces Form
```

`Kind` (when it acts) and `Effect` (what it does) are separate axes on purpose. A
Field + Amplify skill needs no new system — it is a new setting of two boxes that
already exist.

**Evidence the architecture already wanted this:** `Build.cs:206` declares
`EquippedSkill(WovenAbility Ability, int CooldownMs)`. `CooldownMs` is written at
construction (`BuildComposer.cs:58`) and **read nowhere in the repository** — a test
comment says so outright. Someone opened a per-skill cooldown field and there was
never a system to fill it, because `Form` owned the number.

**Sim impact is a change of question, not of shape.** `SoloBattle` already branches
three ways: `IsPassive(form)` -> the Aura block, `FiresOnBeingHit(form)` -> the Trap
block, otherwise the cast block. Those become `Kind == Field`, `Kind == Reaction`,
`Kind == Active`.

**One style per skill.** A skill leaning on two styles would make "how much does the
HAMMER road strengthen this?" ambiguous, and ambiguity there is how the balance
knotted in the first place.

## 4. The six styles

### The ring was contradicting the tree

Affinity distance is the `enum Form` order: `Strike(0), Projectile(1), Aura(2),
Trap(3), Mark(4), Transformation(5)`. Three steps apart is opposite, so **Strike and
Trap are maximally opposed (x0.45)** — while `MasteryCatalog.cs:430` puts both of
them in the **Weight** branch. The tree said they deepen together; the hexagon said
they fight each other. Replacing `Form` with `SkillId` is the moment to fix it.

### The corrected ring

The ring is ordered so that each style sits opposite the one that answers a fight the
opposite way:

```
        HAMMER
   DRAIN     SNARE        opposites face each other across the ring:
   FIELD     SIGN           HAMMER-VOLLEY   SNARE-FIELD   SIGN-DRAIN
        VOLLEY
```

| Opposition | Meaning |
|---|---|
| HAMMER <-> VOLLEY | One enormous blow / many small hits |
| SNARE <-> FIELD | Waits and pays once / never stops, touches everything |
| SIGN <-> DRAIN | Front-loaded burst / slow return |

> **This ordering was first justified by branch membership** — HAMMER and SNARE were both
> Weight, VOLLEY and FIELD were both Spread, so the ring and the mastery tree were made to
> agree. **That argument is now dead:** §7 emptied Weight and Spread out, and §9 moves the
> six specialisations to the rim, where they hang off no branch at all. The ring survives on
> the three oppositions above, which are semantic and owe the mastery tree nothing — and it
> is better for standing alone, because style identity and character build are now two
> independent choices instead of one tangled in the other.

### The six, and what each says to the player

| Style | One line | Was |
|---|---|---|
| **HAMMER** | You strike rarely, and every blow is enormous. | Strike |
| **SNARE** | Being attacked works in your favour. | Trap |
| **SIGN** | You deal nothing yourself; you make everything else bigger. | Mark |
| **VOLLEY** | You strike often and reach several enemies at once. | Projectile |
| **FIELD** | You never close, and you touch everything. | Aura |
| **DRAIN** | You take part of the damage you deal back as health. | Transformation |

**Naming constraint:** `THE X` is the champion naming scheme (THE QUIVER, THE ANVIL,
THE OATHBOUND…). Styles are therefore bare single words, matching the Sources (BODY,
MIND, NATURE, MACHINE, SHADOW, SPIRIT) and the branches (WEIGHT, SPREAD, TEMPO,
ENDURE). `SkillShape.FirstCastMultiplier`'s nickname "OPENING VOLLEY" must be renamed
so it does not collide with the VOLLEY style.

## 5. Twelve skills, each with its own node

> **SUPERSEDED 2026-08-30.** This section used to say "six skills, one per style, each with two
> faces" — the same skill woven either as a move that takes the champion's turn or as a state that
> never does. The designer replaced it:
>
> *"Aynı skilleri aktif/pasif versiyon gibi kullanmak yerine farklı skiller olarak tasarlasak daha
> güzel olur. Bu skilleri de mastery tree üzerinden açsak ve skillerin kullandıkça yükseltebildiğimiz
> çok detaylı olmayan davranış seçenekleri (başlangıç için 2 varyasyon bile yeterli) ve seçilen
> varyasyonun 2-3 nodelik güçlendirmeleri olsa."*
>
> The two-faces model was a layer: the player had to hold "this is HAMMER, but its passive half" in
> their head. Distinct skills delete that layer. §6 below is kept because its 72 nodes are the raw
> material this section is built from, not because its structure still stands.

### The shape

**Twelve skills. Two per style — one active, one passive — and they are different abilities that
share a style, not one ability twice.** Each is unlocked by **its own node** on the mastery tree.

```
MASTERY TREE   four character branches (§9) + six style roads on the rim
      | a node unlocks
SKILL          active (takes the champion's action) or passive (never does). Levels by being used.
      | its levels buy
VARIATION      one of two. Changes what the skill DOES.
      | then
REINFORCEMENT  three nodes belonging to that variation, and worthless to the other one.
```

Four purchases per skill: one variation, three reinforcements. That is the whole of a skill's depth,
and it is deliberately small — the previous design gave each skill a five-ring twelve-node tree and
the designer's verdict on the first draft of that was that it was too clever to read.

### Two currencies, two questions

| Currency | Earned by | Buys |
|---|---|---|
| **Mastery points** | first-time depth, per region | character branches, and the node that unlocks a skill |
| **Skill levels** | using that skill | its variation, then that variation's three reinforcements |

Levels are never lost when a skill is unequipped, and respec inside a skill is free. The cost of
switching is time-to-catch-up, never destroyed progress — the loudest complaint about Last Epoch's
system is that per-skill progress makes players afraid to change anything.

### Why a node per skill

The designer chose this over "one gate per style hands out both of that style's skills". It is the
finest-grained option: you can enter a style far enough for one skill and stop, or walk it out. It is
also the cheapest to grow — **a new skill is a new node and nothing else changes**, which is the
property that lets the catalogue expand for the life of the game without touching a screen.

Its price is that the tree looks busier and "how many skills do I have" is not answerable at a
glance. Accepted knowingly.

### What survives from the two-faces model

Everything that shipped. The slot machinery does not care how a skill got its kind:

| Still true | Now different |
|---|---|
| At most two active skills and two passive ones | A skill has ONE kind; the slot follows from the skill |
| Only an Active costs a beat | The catalogue is twelve entries, not six with two faces each |
| The sim forks on the skill's Kind | `SkillDef.Active`/`.Passive` collapse to `SkillDef.Kind` |
| A Field's damage scales to its own cadence | A spilled skill becomes its style's PASSIVE skill |
| Old saves keep all four woven skills | |

## 6. The twelve skills

Authored 2026-08-30, gated against the two laws (§8) and the ownership table, then corrected. The
72-node design this section used to hold is superseded; it was the raw material.

### Mechanic ownership — each style owns its verbs

| Style | Owns |
|---|---|
| **HAMMER** | defence break, defence ignore, stun, execute threshold |
| **VOLLEY** | hit count, bleed, cooldown reduction, spread on kill |
| **SNARE** | reflect, shield, damage taken |
| **SIGN** | amplify only — magnitude, duration, uptime. It deals no damage itself. |
| **FIELD** | slow, area damage, scaling with the number of living enemies |
| **DRAIN** | lifesteal, healing, attack break, scaling with health |

> **TAUNT WAS REMOVED from SNARE's list.** Every creature in a wave attacks the champion already, so
> "the enemies target you" names nothing. What the taunt reinforcements actually did was *pull the
> wave's next attack forward* — which put a third writer on the enemy bite clock, alongside HAMMER's
> stun (pushes it back) and FIELD's slow (stretches the interval). A build carrying HAMMER's PIN and
> SNARE's HOOK cancelled itself out and nothing on the screen said so. **Only two styles write that
> clock now:** FIELD scales the interval, HAMMER offsets the next bite. They compose; they do not fight.

> **THE REINFORCEMENT LINES BELOW ARE THE ONES THAT SHIPPED, AND 51 OF THE 72 WERE REWRITTEN TO GET
> THERE (2026-08-30).** They were authored as *design* prose — "the total is not cleared when it pays,
> it halves instead", "when the front enemy dies its broken defence carries to the next" — and each of
> those sentences quietly asked the fight for state it does not keep: a bank that survives its own
> spend, a break that outlives its creature, a per-arrow ramp inside a cast the sim resolves as one
> pool. Written that way a reinforcement is not a small feature, it is a sim change wearing a one-line
> disguise, and seventy-two of them is a rewrite of the battle loop.
>
> So each was rewritten to the nearest thing the fight can actually do, and `ReinforcementLivenessTests`
> is what decides "actually": every one runs against the same skill and variation without it, at two
> depths, and damage, health, healing, shield or depth reached must move. **Do not restore a line from
> an earlier draft of this section without a delta and a passing liveness case** — the version in
> `SkillCatalogue` is the specification now, and this table follows it rather than leading it.
>
> Four dials that this section already assumed turned out to be unread by the sim, and were repaired
> rather than written around: armour was clamped at zero (so every floor below it was unreachable
> text), Fields ignored their own `IntervalMs` and all ticked on one global clock, PIN's stun ended
> its tick before any break could run, and IRON cleared its own reflect one line after computing it.

<!-- SKILLS: generated by tools/check_skill_doc.py — edit SkillCatalogue.cs, not this -->

### HAMMER

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **BLOW** | — | Heavy damage to one target. |
| ▸ | **FLATTEN** | BODY | Defence ignore: the blow ignores the target's defence. |
| | TOLL | | The blow deals 40% more damage. |
| | BREAKTHROUGH | | Half of the blow's wasted overkill carries into the next enemy. |
| | TRAIL | | After the blow, your next basic attack ignores defence too. |
| ▸ | **FINISH** | SHADOW | Execute threshold: the blow kills a target under 15% health. Once per wave. |
| | BRINK | | The execute threshold rises to 25% health. |
| | TWICE | | The blow may execute twice each wave, and the threshold rises to 20% health. |
| | CLEAN CUT | | When the blow executes, its wasted overkill carries into the next enemy. |
| **Passive** | **PRESS** | — | A weight sits on the front enemy: its defence drops 5 every 2s, down to -25. |
| ▸ | **CRUSHING** | BODY | The defence drop is 10 every 2s instead of 5, down to -50. |
| | SETTLE | | The floor falls from -50 to -90. |
| | SEIZE | | The weight breaks the two front enemies instead of one. |
| | UNDERMINE | | The weight works every 1s instead of every 2s. |
| ▸ | **PIN** | MACHINE | 1s stun on the front enemy every 6s. |
| | HOLD | | The stun is 1.5s instead of 1s. |
| | BUCKLE | | Each stun strips 10 defence from the front enemy. |
| | INTERCEPT | | The stun waits for the wave's next bite instead of its own clock. |
| **Active** | **HARD HANDS** *(SEEKER only)* | — | Heavy damage to one target every 6 beats. While it is woven, every basic attack you make lands 70% harder. |
| ▸ | **OPEN HAND** | BODY | The blow gives its weight to your hands: it deals a third as much, and your basic attacks land 210% harder. |
| | BROAD KNUCKLE | | Your basic attacks land 330% harder instead of 210%. |
| | SPLIT GRAIN | | Your basic attacks ignore defence. |
| | SLOW HANDS | | The blow comes round every 8 beats instead of 6, and your basic attacks land 400% harder instead of 210%. |
| ▸ | **SHUT FIST** | MIND | Your hands gain nothing more. The blow lands twice on the target, and it hits an enemy under half health 85% harder. |
| | THIRD FALL | | The blow lands three times instead of twice. |
| | CARRY ON | | If the target dies, the blows it had left drive into the next enemy. |
| | LAST INCH | | The finishing bonus is 140% instead of 85%. |
| **Active** | **HARDFACE** *(ANVIL only)* | — | Heavy damage to one target every 6 beats. While it is woven, every enemy that dies strips 12 defence from every enemy still standing, for the rest of the wave, down to -48. |
| ▸ | **PLANISH** | MACHINE | The strip is 24 defence instead of 12, and defence can be driven down to -90. |
| | BEDPLATE | | Defence can be driven down to -150 instead of -90. |
| | COLD SET | | The strip is 40 defence instead of 24. |
| | PEENING | | The blow strips the enemy in front even when it does not kill. |
| ▸ | **UPSET** | BODY | No defence stripping. The weight falls on every enemy, twice, at 45% each. |
| | THIRD DROP | | The weight falls three times instead of twice. |
| | RUNOUT | | If an enemy dies, the falls it had left drive into the next enemy. |
| | UNDERFOOT | | The falls hit an enemy under half health 75% harder. |
| **Passive** | **SLOW FALL** *(TOWER only)* | — | A stone falls on the front enemy every 3s. It never takes your action. |
| ▸ | **COURSES** | BODY | The stones fall every 1.5s, each half as heavy, they ignore defence, and each stone hits an enemy under 45% health 55% harder. |
| | DRYSTONE | | The stones fall every 1s instead of every 1.5s. |
| | PLUMBLINE | | The finishing bonus is 110% instead of 55%. |
| | HAIRLINE | | The finishing bonus reaches an enemy under 70% health instead of 45%. |
| ▸ | **ONE STONE** | NATURE | One stone every 6s, on the two enemies in front, and it lands at 2.4 times the weight. |
| | CAPSTONE | | The stone falls on the three enemies in front. |
| | FULL COURSE | | The stone lands at 3.6 times the weight instead of 2.4. |
| | BEDDING IN | | The stone falls every 4s instead of every 6s. |

### SNARE

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **REPAY** | — | Deals 200% of the health damage you have taken since its last cast. |
| ▸ | **VENGEANCE** | SHADOW | 350% instead of 200%, but only health damage taken in the last 3s counts. |
| | GRUDGE | | It pays back 500% instead of 350%. |
| | RAW NERVE | | Only the last 2s counts, but it pays back 700%. |
| | SCARRED | | Below 40% health it pays back 50% more. |
| ▸ | **BANKED** | MACHINE | Instead of dealing it, the total becomes SHIELD of equal size. |
| | LINING | | It banks 300% of the health damage taken instead of 200%. |
| | STANDING | | The shield is 50% larger. |
| | CARRIED | | Every wave begins with SHIELD worth 10% of your maximum health. |
| **Passive** | **JAWS** | — | Every bite returns 50% of it to the enemy that bit you. Rearms every 3s. |
| ▸ | **NET** | SHADOW | The reflect returns 100% of the bite instead of 50%. |
| | MESH | | The reflect grows 10% per bite taken this wave, up to +50%. |
| | SPITE | | The reflect returns 140% of the bite instead of 100%. |
| | RECOIL | | The trap rearms a third sooner. |
| ▸ | **IRON** | MACHINE | No reflect: the trap stops a whole bite, but rearms every 6s. |
| | BLUNT | | The trap rearms twice as fast. |
| | REPRISAL | | A stopped bite is returned to the enemy that made it, in full. |
| | PLATING | | A stopped bite also becomes SHIELD worth half of what it would have dealt. |
| **Passive** | **HOLD FAST** *(UNBROKEN only)* | — | Every 2s you gain shield worth 2.5% of your maximum health. It never takes your action. |
| ▸ | **BREASTWORK** | MACHINE | Twice the wall, half as often: shield worth 5.5% of your maximum health every 4s. |
| | COURSED STONE | | Each pulse is worth half again as much: 8.5% of your maximum health instead of 5.5%. |
| | FOOTINGS | | The wall is rebuilt every 2.5s instead of every 4s. |
| | GROUNDWORK | | The wall is already standing when the wave opens: every wave begins with shield worth 5.5% of your maximum health. |
| ▸ | **DEEP ROOTS** | SPIRIT | Half the wall. Each pulse also heals 1% of your maximum health. |
| | WELLSPRING | | Each pulse heals 1.8% of your maximum health instead of 1%. |
| | TAPROOT | | Each pulse heals 30% more than the one before it, for the rest of the wave. |
| | HEARTWOOD | | Each pulse is worth 3% of your maximum health instead of 1.2%. |
| **Passive** | **NARROWS** *(THORNWALL only)* | — | Every bite is answered by a fixed hit on the enemy in front, and the answer grows 22% for every bite this wall has already answered this wave. Rearms every 2.5s. |
| ▸ | **BRAMBLE** | NATURE | The answer reaches every enemy instead of one, stops growing, and hits an enemy under half health 80% harder. |
| | UNDERGROWTH | | The finishing bonus reaches an enemy under three-quarters health. |
| | BLACK THORN | | The finishing bonus is 150% instead of 80%. |
| | BRIAR | | The answer is worth 170 instead of 110. |
| ▸ | **CHOKE** | MACHINE | The answer stays on the enemy in front, the wall answers every 2s, and the answer grows 34% for every bite it has already answered. |
| | DEEP THORN | | The answer grows half again as fast: 55% for every bite instead of 34%. |
| | SNAPBACK | | The wall rearms 30% sooner still. |
| | SECOND STAKE | | The answer reaches the two enemies in front. |

### SIGN

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **CALL** | — | All your damage +60% for 6s. |
| ▸ | **SPEND** | MIND | The next 3 damaging hits land +200% harder. It waits until they land. |
| | OVERSPEND | | Each empowered hit lands +300% instead of +200%. |
| | COUNT | | It empowers 5 hits instead of 3. |
| | PERFECT CLAUSE | | A critical hit is empowered without spending one of them. |
| ▸ | **STEADY** | SPIRIT | Each cast adds +40% amplify for the rest of the wave, up to +80%. |
| | REDOUBLE | | Each cast adds +70% instead of +40%. |
| | PILLAR | | The cap rises from +80% to +160%. |
| | FOUNDATION | | Every wave begins with one stack already standing. |
| **Passive** | **BRAND** | — | Your damage to the front enemy is +70%. |
| ▸ | **SPRAWL** | MIND | The mark covers every enemy instead, at half strength. |
| | EVEN | | Every enemy's mark rises from half to three-quarters strength. |
| | ANCHOR | | The front enemy takes the full 70% mark; the rest keep the spread. |
| | WINNOW | | The marks deepen +10% every 2s, up to +50%. |
| ▸ | **ETCH** | SPIRIT | The mark deepens +50% every 2s to +170%, and keeps its depth when it moves. |
| | SINK | | The mark deepens +80% each time instead of +50%. |
| | GRAVEN | | The cap rises from +170% to +250%. |
| | PACE | | The mark deepens every 1s instead of every 2s. |
| **Passive** | **OATHMARK** *(OATHBOUND only)* | — | Every bite you take opens the mark: all your damage +90% for 3s. Rearms every 4s. |
| ▸ | **SEALED WORD** | SPIRIT | The mark falls on the enemy in front only, and it is far deeper: +260% while it holds, for 1.8s. |
| | DEEP SEAL | | The mark is half again as deep: +390% instead of +260%. |
| | LONG SEAL | | The mark holds 3s instead of 1.8s. |
| | SHORTER OATH | | The mark reopens every 2.5s instead of every 4s. |
| ▸ | **OPEN WORD** | MIND | The mark covers the whole wave but burns fast: +140% for 2s, and it reopens every 2.5s. |
| | WIDER WORD | | The mark is +230% instead of +140%. |
| | SAID AGAIN | | Each time the mark reopens it is deeper: +45% more each time, up to +180%. |
| | FIRST WORD | | The enemy in front takes a +220% mark while the rest of the wave keeps the spread. |

### VOLLEY

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **SPRAY** | — | Fires 5 arrows across the wave. |
| ▸ | **SPLAY** | MIND | An arrow at every enemy, and never fewer than 5 arrows in all. |
| | EXTRA STRING | | Two more arrows, 7 instead of 5. |
| | NOCK | | Every arrow deals 25% more. |
| | CLEANUP | | Arrows hit an enemy under half health 60% harder. |
| ▸ | **CLUSTER** | BODY | All 5 arrows hit one enemy. |
| | DRIVE | | The cast deals 50% more damage. |
| | RUPTURE | | Your casts leave bleed worth 30% of what they deal. |
| | PUNCH THROUGH | | If the target dies, the remaining arrows drive into the next enemy. |
| **Passive** | **WEEP** | — | When an enemy dies it leaves bleed on the wave worth 30% of its health. |
| ▸ | **TORRENT** | NATURE | The bleed deals its damage twice as fast. |
| | DRY | | A kill leaves 45% of the enemy's health as bleed instead of 30%. |
| | SPILLWAY | | The bleed pays out three times as fast instead of twice. |
| | FLOOD | | While the bleed is already running, each further kill adds half again. |
| ▸ | **CARRION** | SHADOW | The bleed lingers, paying out over more than twice as long. |
| | DREGS | | A kill leaves 45% of the enemy's health as bleed instead of 30%. |
| | ONSET | | Your casts leave bleed worth 10% of what they deal, and it carries too. |
| | LAST DROP | | The carried bleed pays out 50% faster. |
| **Active** | **CLOCKWORK** *(METRONOME only)* | — | Fires on a clock, not on a count: three shots across the wave every 7s. No skill rate bonus makes the clock come sooner, and it still costs the action it lands on. |
| ▸ | **ROLL** | MACHINE | Every shot lands twice on two enemies instead of once on three, each a sixth heavier, and the clock is half a second shorter: four strikes every 6.5s. |
| | RIM SHOT | | Every enemy is struck three times instead of twice. |
| | OFF BEAT | | If an enemy dies, the shots it had left drive into the next enemy. |
| | STEADY HAND | | The shots ignore defence. |
| ▸ | **HELD NOTE** | MIND | One arrival every 9s instead of three shots every 7s, and it falls on the whole wave. |
| | WHOLE BAR | | The arrival comes every 7s instead of every 9s. |
| | LATE BEAT | | The arrival hits an enemy under 40% health 110% harder. |
| | SPARE SHOT | | Against a thin wave the arrival doubles up: never fewer than 3 shots in all. |
| **Passive** | **BACKDRAW** *(QUIVER only)* | — | When an enemy dies, two arrows fly at whatever is still standing. Rearms every 2.5s. |
| ▸ | **CLEAN SWEEP** | MIND | The volley reaches every living enemy instead of two, at two thirds the weight, and every arrow hits an enemy under half health 85% harder. |
| | WIDE SWEEP | | The finishing bonus reaches an enemy under three-quarters health. |
| | BARBED SWEEP | | The finishing bonus is 160% instead of 85%. |
| | SECOND STRING | | Every enemy is struck twice, and each strike is lighter. |
| ▸ | **ONE SHAFT** | SHADOW | One arrow instead of two, at six and a half times the weight, and it comes back only every 12s. |
| | HEAVY SHAFT | | The arrow lands at ten times the weight instead of six and a half. |
| | SECOND SHAFT | | The arrow comes back every 7s instead of every 12s. |
| | BROADHEAD | | Your casts leave bleed worth 25% of what they deal. |

### FIELD

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **PULSE** | — | Area damage to every enemy in the wave. |
| ▸ | **THRONG** | NATURE | Damage rises 20% for each living enemy. |
| | HORDE | | The per-enemy bonus rises from 20% to 35%. |
| | PACKED | | The pulse deals 30% more damage. |
| | CROWDED | | While 4 or more enemies live, the pulse comes a beat sooner. |
| ▸ | **SHARE** | SPIRIT | 300% damage, split evenly between every living enemy. |
| | POOL | | The split pool rises from 300% to 450%. |
| | NARROWED | | The pool is split two ways at most, however many enemies are alive. |
| | BALANCE | | The pool favours the enemies with the most health left, wasting less. |
| **Passive** | **MIRE** | — | Damages every enemy every 1s, and slows their attacks by 25%. |
| ▸ | **NUMB** | NATURE | The slow deepens 5% each second, up to 40%. |
| | DEEPEN | | The slow deepens 10% a second instead of 5%, and its ceiling rises to 50%. |
| | SEDIMENT | | The ceiling rises from 40% to 55%. |
| | SILT | | The field's damage rises 50%. |
| ▸ | **TEEMING** | SPIRIT | Slows 6% for each living enemy on top of the 25%, up to 60%. |
| | CLOG | | 10% for each living enemy instead of 6%, and the ceiling rises to 78%. |
| | BRIM | | The slow starts at 40% instead of 25%, and its ceiling rises to 85%. |
| | REMNANT | | The wave bites 5% softer for each enemy it has lost, down to -40%. |
| **Passive** | **GRAVE SONG** *(CHORUS only)* | — | Damages every enemy every 2s, and deals 35% more for each enemy the wave has already lost. |
| ▸ | **REQUIEM** | SHADOW | The song comes every 4s instead of every 2s, at more than twice the weight, and each enemy the wave has lost is worth 70%. |
| | DIRGE | | Each enemy the wave has lost is worth 110% instead of 70%. |
| | TOLLING | | The song comes every 3s instead of every 4s. |
| | OPEN GRAVE | | The song hits an enemy under 45% health 90% harder. |
| ▸ | **CHANTRY** | SPIRIT | The song stops counting the dead for damage and counts them against the wave: it deals its plain weight, and the wave bites 14% softer for every enemy it has lost, down to -25%. |
| | HUSH | | The softening may reach -45% instead of -25%. |
| | BLACK VEIL | | The wave bites 22% softer for each enemy it has lost instead of 14%. |
| | SHROUD | | The song ignores defence. |

### DRAIN

| | Entry | Source | Effect |
|---|---|---|---|
| **Active** | **DRINK** | — | Heavy damage to one target; heals you back a share of it. |
| ▸ | **SIPHON** | MACHINE | Lifesteal doubles, and your per-wave healing limit doubles with it. |
| | PUMP | | Lifesteal is 50% stronger. |
| | PARCH | | The cast deals 30% more damage, so it drinks more. |
| | TRICKLE | | Your basic attacks lifesteal 3% of their damage too. |
| ▸ | **GLUT** | NATURE | No lifesteal. Damage rises with your current health, up to +150% at full. |
| | SURFEIT | | The health scaling counts double. |
| | STOUT | | The cast deals 30% more damage. |
| | RIPE | | Above 90% health the scaling reaches half again as far. |
| **Passive** | **WILT** | — | Attack break: every enemy's damage drops 10% a pulse, down to -50%. |
| ▸ | **SUP** | BODY | Each pulse also heals 1% of your maximum health. |
| | BROOK | | The attack break reaches -70% instead of -50%. |
| | BALM | | Each pulse heals 1.5% instead of 1%, and you can be healed 40% more each wave. |
| | RESERVE | | The attack break deepens 15% a pulse instead of 10%. |
| ▸ | **SHRIVEL** | MIND | The front enemy only: 25% a pulse, down to -90%. |
| | HOLLOW | | The break also reaches the second enemy, at half depth. |
| | SEIZED | | The break deepens 35% a pulse instead of 25%. |
| | GAUNT | | The floor falls from -90% to -97%. |
| **Active** | **PAYING WORK** *(MAGPIE only)* | — | Heavy damage to one target every 5 beats, and you take 12% of what it deals back as health. Against a boss it lands 160% harder. |
| ▸ | **STRIPPED BARE** | SHADOW | It takes nothing back, and against a boss it lands 340% harder instead of 160%. |
| | CLEANED OUT | | Against a boss it lands 520% harder instead of 340%. |
| | PRISED OPEN | | The blow ignores defence. |
| | LONG JOB | | It comes every 6 beats instead of 5, and lands half again as hard. |
| ▸ | **LIGHT FINGERS** | NATURE | No boss bonus. It strikes every enemy in the wave for a third of the weight, and each strike hits an enemy under half health 80% harder. |
| | MANY POCKETS | | The finishing bonus reaches an enemy under 72% health instead of half. |
| | SECOND HELPING | | Every enemy is struck twice, and each strike is lighter. |
| | FULL HANDS | | You take 38% of what it deals back instead of 12%. |

<!-- /SKILLS -->

### Names, audited

108 named entries, checked by set intersection against the style names, the keystones and item
enchantments, the live mastery nodes, the champion names and the `THE X` ban — no exact collision, and
no word used twice inside the twelve. The audit is a property of the table above, so it is checked
where the table is: `test_a_reinforcement_name_is_unique_inside_its_variation` and the Source matrix
tests beside it.

> The list of renames that used to sit here — WAKE four times, MASS twice, DEEPEN twice, DREGS twice —
> named entries the combat rework has since replaced outright, so it had become a record of collisions
> between words that no longer exist. The rework's own renames are in `production/audit/combat-v2/`,
> beside the migration that maps them for a save.

Two near-collisions are accepted knowingly: WEEP sits in the same tree as the mastery node SWEEP, and
BLOW in the same tree as SECOND BLOW. Both are one letter apart from a word the player meets on an
adjacent screen, and both should be revisited if a playtest shows anyone confusing them.

## 7. Where a rule lives — RESOLVED

**Decision (designer, 2026-08-29): if a rule is about a skill, it belongs in the skill
tree.** Not a rename — the rule moves down a layer.

Applied to the whole mastery catalogue, the test is:

> A node **stays** if it changes the CHAMPION (health, mitigation, action speed, crit,
> recovery, loot) or is a pure magnitude. It **leaves** if it changes how a SKILL behaves —
> a threshold, a trigger, a target count, an armour rule, a cooldown behaviour.

### What the test does to each branch

| Branch | Of 16 nodes | Verdict |
|---|---|---|
| **Weight** | ~2 stay | Almost the entire branch is skill behaviour |
| **Spread** | ~0 stay | The whole branch is skill behaviour |
| **Tempo** | ~6 stay | Mixed |
| **Endure** | ~13 stay | Genuinely about the champion |

### The finding this exposes

**Weight and Spread were never character branches.** Weight is the hit-size axis and
Spread is the target-count axis — and hit size and target count are exactly what a *skill*
does. They have been skill trees wearing a mastery tree's clothes since they were written.
That is why the balance knotted: the same rule was being sold twice, once per axis, at two
prices, with no owner.

Endure survives almost intact because health, mitigation and recovery really do belong to
the champion. Tempo splits: the basic swing, action speed and crit are the champion's;
cast behaviour and wave-opening burst are a skill's.

### Where the leaving nodes go

| Leaves | Destination |
|---|---|
| SHARPENED, DENT, SUNDER, SHATTER, CRUSH, OVERWHELM | HAMMER — defence break and defence ignore |
| HEAVY HAND, DELIBERATE, MONOLITH | HAMMER — the OVERHEAD trade (bigger, slower) |
| STAGGER | HAMMER — SHOCK |
| BREAKER, FOLLOW THROUGH, SECOND BLOW | HAMMER — after-the-hit rules |
| ASSASSINATE, CULL | HAMMER — execute threshold |
| FAN, WIDE, SWEEP, CHAIN, DISPERSAL, EVERYWHERE, RICOCHET | VOLLEY / FIELD — target count |
| DIFFUSE, QUICK HANDS, MOMENTUM, TIDE, BLITZ, RUSH | VOLLEY — cooldown reduction |
| RALLY, CASCADE | VOLLEY — spread on kill |
| SWARMBANE, OUTNUMBERED | FIELD — scaling with living enemies |
| MARK MASTERY, OPENER, ALPHA, OPENING VOLLEY, PREPARATION, RHYTHM | SIGN — amplify magnitude, duration and uptime |
| HEADLONG | VOLLEY — QUARRY |
| THORNS, REBOUND | SNARE — reflect |
| LEECH, FEEDBACK | DRAIN — lifesteal |
| EXECUTIONER, VOLLEY (bridge) | Split between HAMMER and VOLLEY |

### What stays, and is the whole mastery tree

Champion only: MENDING, TOUGHNESS, PADDING, THICK SKIN, SECOND WIND, RECOVERY, ABSORB,
FORTIFY, BULWARK, BASTION, BRACE, ENDLESS, PAYBACK, ANCHOR, BRISK, HASTEN, FOCUS, FLASH,
SURGE, FIRST STRIKE, INTERRUPT, SIEGE, HEFT — plus the six specialisations, whose job is
now **access** (§9).

### The consequence, which is a decision the designer still owns

Two of the four branches empty out. The mastery tree therefore has to be **re-populated
around the champion**, and the four axes may no longer be the right four: Weight and
Spread have no character content left to carry. The surviving material clusters as:

- **ENDURE** — health, mitigation, recovery. Intact.
- **TEMPO** — action speed, crit, the basic swing, wave-opening stance. Intact.
- Two empty slots, which need character axes that do not exist yet. Candidates: a **loot**
  axis (haul, rarity, chest quality) and a **resonance** axis (the stat that scales every
  skill's power), since both are champion-level and neither is expressible in a skill tree.

Nothing here is implemented. This section records the rule and its consequence; the
re-population is the next design conversation.

## 8. Two laws for node text

### Law 1 — a passive never touches the champion's animation channel

A passive plays **no character clip**, never replaces a swing, never changes what the
champion is doing. It shows as an effect: around the champion, on the enemy, or in the
space between — while the champion keeps swinging normally. The AURA is the model.

Without this the passive slot is pointless. The entire rework exists so the champion's
own swing clip actually plays; a passive that hijacks the swing undoes it.

This killed HAMMER's first passive, HEAVY HANDS ("every 4th basic attack becomes a
hammer hit") — turning a swing into a hammer blow is an animation change. Replaced by
PRESS, a weight that sits on the enemy.

### Law 2 — write effects in game terms, not in prose

Use the vocabulary: **cast time, cooldown, stun, slow, defence break, defence ignore,
attack break, damage over time, execute threshold.** One short line. No narration, no
explaining-to-a-child sentences.

Three node ideas died on this law, and each was a real design error the prose was
hiding:

| Rejected | Why it was wrong |
|---|---|
| "grows every turn you don't use it" | **Skills fire automatically.** The player never chooses to hold one, so any node premised on player timing is invalid. |
| "the enemy attacks one turn later" | **The game is not turn-based** — it runs on a millisecond clock. The mechanic meant was a 1s stun. |
| "a kill readies it at once" | Enemies die constantly, so it would fire nonstop. The codebase already recorded this exact failure: `LooseAgain` cast one skill 18 times in 25 seconds. |

If an effect has no standard name, treat that as a signal the mechanic may be
incoherent, and check it against the sim before writing it down.

## 9. The mastery tree

Two trees, two questions, and after §7 they no longer compete at all:

| | Question | Scope |
|---|---|---|
| **Mastery tree** | *Who is my champion?* | The champion's own body, and which styles open |
| **Skill tree** | *What does this skill do?* | That one skill |

### The bands moved with the rules

Weight answered Armoured, Spread answered Swarm, Tempo answered Caster, Endure answered
Bruiser. Under §7 the rules that did that answering went into the skill trees — so **the
four enemy bands are now answered by your skills, not by your mastery tree.** HAMMER's
defence break answers Armoured; FIELD's area damage and VOLLEY's hit count answer Swarm;
DRAIN's lifesteal answers Bruiser; SIGN's amplify answers Caster.

This is coherent rather than a loss: a band asks whether your *damage* gets through or
reaches far enough, and damage is what skills are. What is left for the mastery tree is
everything a band does not ask about — how hard you are to kill, how fast you swing, how
strong your skills are before any of them is chosen, and how much you carry out.

### The four branches, and the stats behind them

Weight (hit size) and Spread (target count) had no character content left. The two axes
that replace them are not invented: both are `HunterStat` values the sim already reads.

| Branch | Champion stats | What it is |
|---|---|---|
| **TEMPO** | `AttackPower`, `Engineering`, `CriticalChance`, `Focus` | Your own attack — how often you swing, how hard, how often it crits |
| **ENDURE** | `MaxHealth`, `Defense`, `Vitality` | How hard you are to kill, and how you come back |
| **RESONANCE** | `ResonanceAffinity` | How strong every skill is before you pick one — skill power, Source matchup, Vow power |
| **LOOT** | `Guile` | What you carry out — haul, rarity, chest quality, banking |

The pairs are opposed, as before:

| Opposition | The trade |
|---|---|
| **TEMPO ↔ ENDURE** | Your own attack against your own survival |
| **RESONANCE ↔ LOOT** | Power this run against what the run pays. Walk one and the fight is easier; walk the other and clearing it is worth more. |

RESONANCE must never sell cooldown reduction — that mechanic belongs to VOLLEY (§6). It
sells skill POWER, the Source matchup, and Vow strength, none of which any style owns.

### What each tier gives

The previous draft made *every* node a rule. The designer rejected it: a small rule cannot
be felt in a fight and it forces far too much reading.

| Tier | Ring / cost | Gives | What the player does |
|---|---|---|---|
| **Minor** | ring 1, 1 pt | A plain stat — 5 health, 6 resonance | Does not read it; feels the accumulation |
| **Notable / Greater** | rings 2–3, 3–5 pts | A named rule | Reads it, and it is worth reading |
| **Mastery** | ring 4, 8 pts | A large rule | Defines the build |
| **Specialisation** | 6 pts | Access — how deep a style's skill tree opens | A commitment |

The codebase's standing law, "no node is a bare multiplier", applies from Notable upward.
Its real target was flat LARGE nodes — the audit that produced it found 31 of 43 — not
flat small ones. Plain stats are the connective tissue that makes walking a road feel
incremental, and the nine `HunterStat` values are exactly what they should pay in.

### Specialisations sit on the rim, not on a branch

They used to hang off branches: HAMMER and SNARE off Weight, VOLLEY and FIELD off Spread,
SIGN off Tempo, DRAIN off Endure. With Weight and Spread gone that mapping is dead — and
it should not be rebuilt, because LOOT and RESONANCE have no style affinity. A HAMMER
build and a VOLLEY build both want resonance.

So the six specialisations move to the **rim**, reachable from any branch that reaches far
enough. Your character build and your style commitment become **orthogonal**: which branch
you walk says how your champion is built, which specialisations you buy says how it fights.
Today those two are tangled, which is part of why the same rule kept getting sold twice.

**Each style road carries that style's SKILL NODES** — one node per skill, unlocked one
at a time as the road is walked (§5). Mastery points buy the skill; the skill's own levels
buy its variation and reinforcements. **Mastery grants access, skill level grants
progress**, and no player can walk six roads, so choosing an affinity is finally the real
commitment the hexagon wanted.

> This replaces the earlier rule, where a specialisation decided *how deep a skill's own
> tree could be opened*. With skills as distinct objects unlocked one node at a time that
> indirection is gone: the road hands you the skill itself.

### Many roads to one destination

The tree must not be a single file. A mastery or specialisation node should be reachable
from more than one direction.

Half of this is already built and barely used: a node's prerequisite is not one parent but
an OR-group (`Prereqs`, "any one of these"), with an optional second OR-group (`Prereqs2`,
"and any one of these"). Each branch also already carries one "side road" — a spur minor, a
notable and a greater forming a longer alternate route to the same rim. The work is to make
that the rule rather than the exception.

```
   TEMPO (your attack)              RESONANCE (skill power)
            |                              |
   HAMMER   SNARE   SIGN   VOLLEY   FIELD   DRAIN    <- the rim: six style gates,
            |                              |            reachable from any branch
   ENDURE (survival)                LOOT (what you carry)
```

The mesh is also what makes the tree **growable**: a new node names a set of nodes any one
of which opens it, so it can be attached at the rim without re-linearising a spine.

### What survives from the old catalogue

Champion-level nodes that keep their home:

| Branch | Keeps |
|---|---|
| **ENDURE** | MENDING, TOUGHNESS, PADDING, THICK SKIN, SECOND WIND, RECOVERY, ABSORB, FORTIFY, BULWARK, BASTION, BRACE, ENDLESS, PAYBACK, ANCHOR |
| **TEMPO** | BRISK, HASTEN, FOCUS, FLASH, SURGE, FIRST STRIKE, INTERRUPT |
| **RESONANCE** | HEFT and SIEGE, reworked from Form-named to style-agnostic; the rest is new |
| **LOOT** | Nothing — the branch is new |

ENDURE arrives nearly full, TEMPO about half, and RESONANCE and LOOT need writing. That
authoring is the next piece of work on this document.

**Do not duplicate the prestige tree.** The Memory Dust tree teaches KEYSTONES (GREED,
DISCERNING EYE, FORTUNE, HOARDER among them) and unlocks automation; it crosses runs. A
LOOT branch here sells incremental haul, rarity and banking rules *within* a run. Same
subject, different layer — and the boundary must stay visible when LOOT is written.

### The layer boundary — ownership vs the mastery tree

The ownership table in §6 governs **the six skill trees only**. The mastery tree legitimately
sells character-level versions of the same stat, and the two must not be confused:

> Where a style and the mastery tree touch the same stat, the **skill** version is
> CONDITIONAL and the **mastery** version is UNCONDITIONAL.

SNARE owns damage-taken reduction — but only *while the trap is armed*. ENDURE's BULWARK,
PADDING, ABSORB and BRACE reduce damage taken always, because that is what a durable
champion is. Same stat, two layers, and the condition is what tells them apart. The same
test settles DRAIN's lifesteal against ENDURE's MENDING, and FIELD's slow against nothing
in the mastery tree at all.

## 9b. The four branches, node by node

Each branch is 16 nodes: **6 minors** at 1 point, **5 notables** at 3, **4 greaters** at 5,
**1 mastery** at 8 — 49 points a branch, unchanged from today's shape.

Minors pay in plain `HunterStat` values and are not meant to be read. Everything from
notable upward is a named rule.

### RESONANCE — how strong every skill is before you choose one

Sells skill power, the Source matchup, Vow strength and how sharp your affinity lean is.
**Never cooldown reduction** — VOLLEY owns that (§6).

> **Built 2026-08-30.** Four lines changed at implementation, each against a law this codebase
> already enforced. RESERVE was renamed **STEEP** (RESERVE is a WILT/SUP reinforcement, and
> `test_every_name_in_the_catalogue_is_unique` is not the only place a repeated name hurts). CHORD was
> written as pure upside and every Mastery must be a trade, so it now softens every skill 15%. ZEALOT's
> price was *"you cannot unswear one during a run"*, which nothing models — it pays in bites taken
> instead. DISCORD was uncosted and now trades the peak for the floor. The sixteen are pinned one at a
> time by `MasteryNodeLivenessTests`.

| Tier | Node | Effect |
|---|---|---|
| Minor | **CHIME** | +6 resonance |
| Minor | **HUM** | +6 resonance |
| Minor | **TONE** | +8 resonance |
| Minor | **STEEP** | +6 resonance, +5 health |
| Minor | **CLARITY** | +8 resonance |
| Minor | **TIMBRE** | +6 resonance, +4 attack power |
| Notable | **KEYED** | Your strong Source matchup pays 25% more. |
| Notable | **PLEDGE** | Vows pay 30% more. |
| Notable | **NARROW** | Your affinity style hits 20% harder, every other style 10% weaker. |
| Notable | **BROAD** | The opposite style's penalty is halved. |
| Notable | **DEEP** | +12 resonance, and resonance is worth 20% more. |
| Greater | **PURE** | While every woven skill shares one Source, all skills hit 35% harder. |
| Greater | **DISCORD** | Your weak Source matchup no longer weakens you; skills -10%. |
| Greater | **ZEALOT** | Vows pay 60% more, and you take 15% more damage. |
| Greater | **RESONANT** | +25% skill power, -15% basic attack damage. |
| Mastery | **CHORD** | Every Source matchup counts as strong, and every skill is 15% softer. |

### LOOT — what you carry out

> **REWRITTEN 2026-08-30, AT IMPLEMENTATION.** The draft below this line sold **the banking
> decision** — PRUDENCE, STASH, GAMBLE, PATIENT and LODE, five of ten rules — and this game does not
> have one. `SoloExpedition` ends with the note that settles it: *"NO Bank / Retreat / Wipe payout,
> and no ExitShare. The idle loop pays every wave the instant it clears... Removing the priced exit
> is the whole point of the idle turn."* BLOODPRICE also duplicated the existing Desperation trigger.
>
> The designer's unblock is what the branch is built on now: *"hasar almadığın her saniye drop şansın
> artar gibi... hasar ve loot mekaniğini birleştirebilirsin"* — **every LOOT rule keys on something
> the FIGHT already knows.** That is not a compromise, it is the better branch: it gives an idle run
> the tension it actually has (how cleanly and how deep one attempt goes), and it satisfies the
> no-bare-multiplier law by construction, because a haul bonus earned inside a wave is not a flat
> percentage.
>
> The two notables at the top are a **fork, not a ladder** — UNTOUCHED pays for the quiet, BLOODPRICE
> for finishing nearly dead — and `LootNodeLivenessTests` asserts they disagree about which run they
> want. **Must not duplicate the Memory Dust tree**, which teaches the loot KEYSTONES and crosses runs.

| Tier | Node | Effect |
|---|---|---|
| Minor | **GLEAN** | +6 guile |
| Minor | **POCKETS** | +8 guile |
| Minor | **SCAVENGE** | +6 guile |
| Minor | **COUNT** | +6 guile, +5 health |
| Minor | **WEIGH** | +8 guile |
| Minor | **TALLY** | +6 guile, +4 focus (the spur — head of the side road) |
| Notable | **UNTOUCHED** | +3% haul for each second of the wave nothing bit you, up to +45%. |
| Notable | **BLOODPRICE** | +35% haul on a wave you end below half health. |
| Notable | **CACHE** | Every 5th wave cleared pays +60%. |
| Notable | **SPOTLESS** | +25% haul on a wave you finish above 90% health. |
| Notable | **PROSPECT** | +1% haul for every wave past depth 20. (side road) |
| Greater | **GAMBLE** | +70% haul, and every bite hits you 25% harder. |
| Greater | **SECOND LOOK** | A quiet wave raises chest QUALITY too, +0.04 a second; hits -8%. |
| Greater | **VEIN** | A wave finished above 90% health pays +90% instead of +25%; skills -8%. |
| Greater | **LODE** | Depth pays +2% a wave instead of +1%, and you swing 10% softer. (side road) |
| Mastery | **PROSPECTOR** | +3% haul for every wave past depth 30, and skills -15%. |

> **Two conditions were caught unreachable before they shipped**, which is the same species the
> reinforcement pass spent a day on and the reason every branch now gets a liveness file. *"A wave
> nothing bit you in"* does not exist — every creature in a wave attacks. *"A wave finished at FULL
> health"* does not either — there is no full heal between waves, so a champion chipped once stays
> chipped for the run. **Nine tenths is the first version a real build can hold.**

### TEMPO — your own attack

> **BUILT 2026-08-30.** The draft below guessed at which rules would survive; what decided it was
> not taste. **Five `SkillShape` fields have exactly one feeder in the whole game** — `AutoAttackRate`
> (BRISK), `HealOnClear` (SECOND WIND), `BetweenWaveRegen` (RECOVERY), `ReflectFraction` (THORNS),
> `AbsorbAtLowHealth` (ABSORB) — so cutting any of those nodes would have orphaned a field the sim
> reads. All five were promoted; the twelve that went all have another feeder (an element set, a
> champion) or duplicate a node one ring up. TEMPO cut OPENER, HASTEN, PREPARATION, FOCUS, FLASH and
> MARK MASTERY; SURGE inherited the side road, and BRISK became a notable.
>
> **INTERRUPT was dead when the probe reached it.** It timed its bonus with
> `(absMs - since) % enemyIntervalMs`, the modulo model the bite clock itself abandoned — an exact
> modulo only fires where the interval divides a multiple of the 100ms tick, and MIRE's slow stretches
> `nextBite` and not the interval, so the two clocks drifted apart the more a build slowed the wave.

The six plain-stat minors:

| Tier | Node | Effect |
|---|---|---|
| Minor | **BITE** | +4 attack power |
| Minor | **FORCE** | +5 attack power |
| Minor | **SWIFT** | +3 engineering |
| Minor | **QUICK** | +3 engineering |
| Minor | **SHARP** | +1% critical chance |
| Minor | **POISE** | +4 focus |

### ENDURE — survival

> **BUILT 2026-08-30.** The count was four; it was six. Sixteen rules had to become six plain-stat
> minors plus ten rule slots, and the draft undercounted because it assumed some minors would stay as
> rules. Cut: TOUGHNESS (a bare +15% maximum health, the shape the stat minors replace), THICK SKIN
> and MENDING (the two overlaps this section already named), LEECH and FORTIFY (both granted by
> element sets besides), and BRACE (BULWARK with a different price tag). SECOND WIND and PADDING were
> promoted to notables and ABSORB moved down a ring to end the side road that opens with THORNS.

The six plain-stat minors:

| Tier | Node | Effect |
|---|---|---|
| Minor | **HIDE** | +12 maximum health |
| Minor | **BULK** | +12 maximum health |
| Minor | **GUARD** | +3 defence |
| Minor | **STAND** | +3 defence |
| Minor | **KNIT** | +2 vitality |
| Minor | **ROOTED** | +10 maximum health, +1 vitality |

### What is still unwritten

The mesh itself: which nodes open which, and which routes reach the rim. That is layout
work against `MasteryCatalog`'s `Prereqs` / `Prereqs2` OR-groups and belongs with the
implementation, not with this document.

## 10. Numbers and the beat

### What the slot change does on its own

Beat demand is the share of beats claimed by a cast; the basic attack takes whatever is
left (`SoloBattle.cs:1345`, `if (onBeat && !acted)`).

| | Slots | Demand | Swings |
|---|---|---|---|
| **Today** | 4 active | ~0.80 | ~1 beat in 5 |
| **After** | 2 active | ~0.44 worst case | ~4 beats in 7 |

Worst case is the two shortest cooldowns woven together. Passive skills add **nothing** to
demand by construction: a Field ticks on its own clock above the beat gate, and a Reaction
fires on an event. That is what makes the passive slots free.

**No cooldown number has to move for this.** The three pinned invariants stay intact:
`test_cooldowns_are_sized_to_a_wave_not_to_each_other` (a cooldown longer than a wave never
fires — 6 beats is the ceiling), `test_projectile_trades_weight_for_volume` (VOLLEY must
fire more often than HAMMER), and WEAVER's price. The knob that was empty stays untouched;
the rework removes the *contention*, not the numbers.

### MEASURED 2026-08-30 — nothing had to move

The worry below did not materialise, and it was settled by running a whole expedition rather
than by reasoning about one wave:

| Build | Reached | Fight time |
|---|---|---|
| four actives | wave 12 | 57.9s |
| two actives + two passives | **wave 14** | 58.9s |

Depth x1.17, pace x0.98 — deeper, at the same pace. The plain swing landing on 60% of the
beats instead of 29%, plus passives that still work, more than covers the casts given up.
`AutoAttackDamage` and `FormBaseValue` were **not** touched. `SlotSplitBalanceTests` pins the
run to a 0.70-1.40 band so a later change cannot drift it silently, and the x1.17 is a mild
strengthening a playtest should confirm.

One real defect surfaced on the way, and it was severe: `FormBaseValue` quotes each Form in
the units it is *paid* in — AURA's 12 is a second's worth, STRIKE's 500 one nine-second cast —
and the Field branch multiplied by the tick regardless, so a skill spilled into a passive slot
paid its whole cast value every second. A Field now scales to its own cadence.

### The original worry, kept for the record

Cutting active slots from four to two roughly halves cast output. The champion gains beats
back, so the **basic attack** has to carry what the missing casts used to:

| Knob | Where | Why it moves |
|---|---|---|
| `ExpeditionTuning.AutoAttackDamage` | the swing's size | It now lands on ~56% of beats instead of ~20% |
| `WeavingTuning.FormBaseValue` | per-skill base power | Sized for a saturated 4-slot rotation |
| `EquippedSkill.CooldownMs` | per skill, finally read | Cooldown stops being one global table |

**These must be re-measured against `BalanceSweepTests`, not reasoned about.** The project
has been burned here before: the last four attempts to move cooldowns each broke a
different invariant, and every one was found by running the suite rather than by thinking.
The acceptance test is that a full rotation lands where the economy, the pacing band and
the branch sweeps were already tuned — the same bar the 2026-08-27 beat pass had to clear.

### Per-skill cooldown

`EquippedSkill(WovenAbility Ability, int CooldownMs)` already exists and is read nowhere
(§3). Under this design it becomes live: `SkillDef.Timing` seeds it, the skill's own tree
modifies it (VOLLEY owns cooldown reduction, §6), and `SoloBattle` reads it instead of
`FormBehaviour.BaseCooldownMs`. The wave-length ceiling becomes a per-skill assertion
rather than one global rule.

## 11. Migration and staging

### The slot count is driven by progression, and a slot has no kind

| System | Rule |
|---|---|
| `Unlocks.SkillSlots` | a new champion starts with 1 slot and earns up to 4. **Unlocks buy TOTAL capacity and nothing else** |
| `Build.MaxActiveSkills` / `MaxPassiveSkills` | a build holds at most **two** skills that take an action and **two** that do not, at any capacity |
| `DustEffects.SkillSlots` | *removed.* The prestige node `weave_5` sold a fifth slot; it bought a THIRD active, which is the number this rework exists to bring down |

**The slot itself has no kind. The skill brings its own.** So at capacity 2 a player may weave two
Actives, one of each, or two Passives — whichever three they like, bounded by the total. At capacity 3
the legal shapes are 2A+1P and 1A+2P; at capacity 4 the maximum composition is 2A+2P. A third skill of
either kind is illegal at every capacity.

> **CORRECTED 2026-09-22.** This table used to say the unlock order was *active, passive, active,
> passive*, "so the second slot a player ever earns already teaches the distinction". It taught
> something else instead. The BUILD screen let a player weave a second Active at capacity 2, the
> composer's per-kind budget refused it, and `BuildComposer` discarded the refusal — so the skill was
> on the screen and not in the fight, with nothing anywhere able to report it. The same arithmetic
> made the passive budget ZERO at capacity 1, so the six champions whose signature is a Field or a
> Reaction composed with no skills at all. Capacities are global caps now, never a share of the slots,
> and one rule (`Build.WouldRefuse`) answers for the editor, the load path and the simulation alike.

### Save migration

- `SavedSkill` holds a `Form`. It must map to a `SkillId`, and the six styles must be
  re-ordered to the corrected ring (§4). Both happen in one pass, so the ring reorder is
  free — nothing reads the old ordinal afterwards.
- A saved build with four beat-taking skills must be resolved: keep the first two as
  actives, and convert the rest to their styles' passive face rather than dropping them.
  Silently unweaving a player's build is not acceptable.
- `Build.Weave` currently permits duplicate Forms. A skill is now a singleton with its own
  tree and its own levels, so duplicates must be refused.

### Implementation order — every stage leaves the game playable

| Stage | What | Testable by |
|---|---|---|
| **1** | `Style` enum, `SkillDef` catalogue, `WovenAbility.SkillId`. No behaviour change. | Catalogue integrity tests; whole suite still green |
| **2** | `Build` gains active/passive slot kinds; `Weave` enforces kind and refuses duplicates | Slot tests |
| **3** | `SoloBattle` branches on `SkillDef.Kind` instead of `FormBehaviour`; per-skill cooldown goes live | Existing combat suite must stay green |
| **4** | Beat-demand assertion: a 2-active build must leave the swing more than half the beats | New test, and a capture of the hunt screen |
| **5** | Numbers re-measured against `BalanceSweepTests` | The sweeps |
| **6** | `WeaveScreen` groups by kind and refuses a third of either | Screenshot, `SkillKindCapsTest` |
| **7** | Skill trees: ring 0 only (the active/passive fork), then the outer rings | Per-tree tests |
| **8a** | ~~WEIGHT -> RESONANCE~~ and ~~SPREAD -> LOOT~~ — **done 2026-08-30** (`110e3af`, `f4052b8`) | `MasteryNodeLivenessTests`, `LootNodeLivenessTests` |
| **8b** | TEMPO and ENDURE gain §9b's plain-stat minors; the `MasteryNode.Stats` channel they need is built | Mastery tests |
| **8c** | ~~The six skill-unlock nodes~~ — **done 2026-08-30** (`3a5eab3`). Each specialisation is its style's road head and its road node teaches that style's second skill; respec re-locks | `test_a_skill_the_tree_has_not_taught_cannot_be_chosen` + a capture |

Stages 1-6 are the playable slice: the fight changes, the swing comes back, and the trees
arrive afterwards as depth on top of a system that already works.

### Where the skill gate sits, and why it is not where it looks like it should be

Three placements were tried before one held, and the two that failed are worth keeping:

- **On the SPILL.** An old four-active build spills two skills into passive slots, and those passives
  are exactly the taught ones — so gating there unwove half of someone's build on load, which §11
  forbids in as many words.
- **Keeping the overflow ACTIVE instead.** `Build.Weave` refuses a third active, so the skill was
  dropped anyway. The same silent unweaving, one layer further down.
- **On the player's own choice** (`SkillPick.Passive == true`). This is the one. A spill is the
  BUDGET's decision, not the player's, so it is exempt; what is refused is a slot deliberately set to
  a kind whose skill has not been learned. The door that leaves open — wire three actives, collect the
  passive free — is shut on the weave screen, whose toggle refuses and names the road.

**Only the six with no `LegacyForm` are gated.** Gating all twelve would leave a fresh champion with
no skills at all; the other six are reached by picking a Source and a Form, which is the door that
already exists.

