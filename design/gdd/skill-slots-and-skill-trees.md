# Skill Slots and Skill Trees — the 2026-08-29 rework

> **STATUS: DESIGN IN PROGRESS.** Sections 1–9 are agreed with the designer.
> Sections 10–11 are still drafts. Nothing here is implemented yet.

## 1. Overview

The champion carries **two active and two passive skill slots** instead of four
undifferentiated ones. There are **six skills, one per style**, and each has its
own specialisation tree. A skill's tree decides whether it is an active (it takes
the champion's turn) or a passive (it never does) — so the active/passive split is
a player decision inside the tree, not a property of the catalogue.

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
SLOT   (2 active + 2 passive)
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

## 5. Six skills, six trees

**One skill per style. Each has its own tree. The tree's first ring decides what kind
of thing the skill is** — a move that takes the champion's turn, or a state that
never does.

This is why the catalogue is six rows and not sixty: everything that would have been
a separate skill is a node instead. A first draft of this design proposed twelve
fixed skills (an active and a passive per style) and the designer rejected it —
correctly. Those twelve were derived backwards from what the sim could already do,
which produces relabelled code paths rather than abilities. The content was not
wrong; its home was. FOLLOW-THROUGH is a poor skill and a fine node.

### Worked example — the HAMMER tree

This is the quality bar for the other five. Written under the two laws in §8: game
terms, one mechanic per node, and a passive that never touches the champion's animation.

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **BLOW** | Active. Heavy damage to one target. |
| | **PRESS** | Passive. A weight sits on the front enemy: damage every 2s, defence breaks steadily. |
| **1 · How it hits** | **OVERHEAD** | +50% damage, +50% cooldown. |
| | **CHARGE** | +25% damage per basic attack landed since the last cast. |
| | **SPLIT** | Hits 2 targets for 50% each. |
| **2 · What it beats** | **CRACK** | Defence break — permanently lowers the target's defence. |
| | **FLATTEN** | Defence ignore. |
| **3 · After the hit** | **SHOCK** | 1s stun. |
| | **DENT** | Attack break — permanently lowers the target's damage. |
| | **FINISH** | Execute threshold: instantly kills a target under 15% health. |
| **4 · Capstone** | **CRATER** | Area damage: hits the whole wave for 50%. |
| | **EXECUTIONER** | Always targets the highest-health enemy, double damage to it. |

`BLOW -> CHARGE -> FLATTEN -> FINISH -> EXECUTIONER` is a single-target executioner.
`PRESS -> SPLIT -> CRACK -> SHOCK -> CRATER` never takes a beat until the capstone and
grinds the whole wave down. Same skill, same style, same art.

> **Three earlier drafts of this tree were rejected, and the reasons are the laws in §8.**
> The first was prose ("the blow lands one beat late and hits far harder"). The second kept
> WEIGHT as the passive — "every fourth basic attack becomes a hammer blow" — which changes
> the champion's animation and so defeats the point of the rework. The third used WIND
> ("every turn you do not spend it, it grows"), which is incoherent when skills fire
> automatically, and FOLLOW ("a kill readies it at once"), which fires nonstop because
> enemies die constantly.
>
> Node names were also renamed away from live mastery nodes: WEIGHT (a mastery *branch*
> name) -> PRESS, SUNDER -> CRACK, OVERWHELM -> FLATTEN, STAGGER -> SHOCK, FOLLOW -> RING
> -> deleted. CARRY was deleted outright: it was the same rule as the existing mastery
> greater BREAKER. Capstones were THE ANVIL and THE PIN until it was noticed that `THE X`
> is the champion naming scheme and THE ANVIL is literally one of the ten champions.

### Where the points come from

**A skill earns its own levels by being used.** An equipped skill levels as you hunt,
and its levels are its tree points. Natural for an idle game: the skill grows while
you wait, and descending deep buys identity as well as loot.

Two rules make experimenting safe, because the loudest complaint about Last Epoch's
system is that per-skill progress makes players afraid to change anything:

- **Levels are never lost.** Unequipping a skill keeps everything it earned; coming
  back to it resumes where you left off.
- **Respec inside a tree is free**, at the workbench.

The cost of switching is therefore time-to-catch-up, never destroyed progress.

## 6. The other five trees

> Rewritten 2026-08-29 under the two laws in §8. The first version of this section was
> rejected as too clever; the second was simple but had collapsed into sameness — all five
> ring 2s were "defence break / defence ignore" and SAP appeared in three trees. The fix is
> the ownership table below.

### Mechanic ownership — each style owns its verbs

A mechanic belongs to exactly ONE style. This is what stops six trees from becoming one
tree with six skins, and it is checkable: no node may use a mechanic another style owns.

| Style | Owns |
|---|---|
| **HAMMER** | defence break, defence ignore, stun, execute threshold |
| **VOLLEY** | hit count, bleed (damage over time), cooldown reduction, spread on kill |
| **SNARE** | reflect, taunt, shield, damage taken |
| **SIGN** | amplify — magnitude, duration, uptime. Nothing else; it deals no damage. |
| **FIELD** | slow, area damage, scaling with the number of living enemies |
| **DRAIN** | lifesteal, healing, attack break, scaling with health |

### VOLLEY

*You fire many small arrows and hit several enemies at once.*

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **SPRAY** | Active. Fires 5 arrows at random enemies. With fewer enemies they are split between them. |
| | **RAIN** | Passive. Arrow rain hits the whole wave every 3s. |
| **1 · How it hits** | **VOLUME** | +2 arrows, +50% cooldown. |
| | **BROADHEAD** | +60% damage per arrow, 2 fewer arrows. |
| | **SNAPSHOT** | -30% cooldown, -30% damage per arrow. |
| **2 · What it beats** | **QUARRY** | Double damage to enemies above 60% health. |
| | **THICKET** | +15% damage per living enemy. |
| **3 · After the hit** | **BLEED** | Damage over time: hit enemies bleed for 4s. |
| | **NOCK** | Each arrow that hits lowers this skill's cooldown by 0.2s. |
| | **RICOCHET** | An arrow that kills hits one more enemy. |
| **4 · Capstone** | **STORM** | Cooldown falls 10% for each living enemy. |
| | **SCATTER** | After a kill, the next volley hits every enemy. |

### SNARE

*Being attacked works in your favour.*

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **JAWS** | Passive. A trap rings you: every attack on you takes 50% back. Rearms every 3s. |
| | **SNAP** | Active. Damage to one target, raised by your maximum health. |
| **1 · How it hits** | **TEETH** | +100% damage, +50% cooldown. |
| | **NET** | Hits the whole wave for 50% each. |
| | **SPRING** | Rearms twice as fast for half the damage. |
| **2 · What it beats** | **BAIT** | Taunt: the wave attacks you 1s sooner, and the trap is armed for it. |
| | **PLATE** | Take 25% less damage while the trap is armed. |
| **3 · After the hit** | **SHELL** | Gives you a shield equal to the damage it reflected. |
| | **SALVE** | Heals you for 30% of the damage taken. |
| | **HOOK** | The enemy that set off the trap attacks you again 1s sooner. |
| **4 · Capstone** | **RECOIL** | Reflects 100% of the attack back at the enemy. |
| | **IRON** | While the trap is armed you take no damage; it rearms 50% slower. |

### SIGN

*You deal nothing yourself; you make everything else bigger.*

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **CALL** | Active. Damage amplify: all your damage +50% for 4s. |
| | **BRAND** | Passive. A mark sits on the front enemy: your damage to it is +30%. |
| **1 · How it hits** | **SWELL** | +10% amplify per hit landed since the last cast, up to +50%. |
| | **SPEND** | +150% amplify, 2s duration. |
| | **STRETCH** | +4s duration, amplify halved. |
| **2 · What it beats** | **OPENING** | Double amplify during the first 3s of a wave. |
| | **CHORUS** | The amplify applies to every enemy at half strength. |
| **3 · After the hit** | **RESIDUE** | When the amplify ends, the biggest hit under it lands again at 50%. |
| | **FOCUS** | The amplify is doubled while only one enemy is alive. |
| | **PRIME** | The amplify is at full strength from its first instant. |
| **4 · Capstone** | **BOTH WAYS** | Double amplify, double damage taken. |
| | **STANDING** | The amplify never falls below +25%. |

### FIELD

*You hit the whole wave, all the time.*

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **PULSE** | Active. Area damage to every enemy. |
| | **GLOW** | Passive. Damage every 1s to every enemy. |
| **1 · How it hits** | **DENSE** | +50% damage, +50% cooldown. |
| | **RAPID** | -40% damage, -40% cooldown. |
| | **SHARE** | 300% damage, split between all enemies. |
| **2 · What it beats** | **CHILL** | Slow: enemies attack 25% slower. |
| | **CREEP** | +10% damage for every second the field has been up this wave. |
| **3 · After the hit** | **NUMB** | The slow deepens 5% each second, up to 40%. |
| | **SPARK** | Every 5th hit deals triple damage. |
| | **FADE** | Enemies under 10% health die when the field hits them. |
| **4 · Capstone** | **TEMPEST** | +25% damage per living enemy. |
| | **OVERLOAD** | Every 3s the field deals 5x damage to every enemy. |

### DRAIN

*You take part of the damage you deal back as health.*

| Ring | Node | Effect |
|---|---|---|
| **0 · What it is** | **DRINK** | Active. Heavy damage to one target; heals you for 50% of it. |
| | **SEEP** | Passive. Damage over time on the front enemy; heals you for 50% of it. |
| **1 · How it hits** | **GULP** | +50% damage, +50% cooldown. |
| | **SPILL** | Hits 3 enemies for 50% each. |
| | **THIRST** | Double lifesteal, -25% damage. |
| **2 · What it beats** | **GORGE** | +100% damage to enemies above 80% health. |
| | **SAP** | Attack break: permanently lowers the target's damage. |
| **3 · After the hit** | **HUSK** | Take up to 30% less damage as your health falls. |
| | **TAP** | Heals 5% of your maximum health per enemy hit. |
| | **SCAB** | Healing above full becomes a shield. |
| **4 · Capstone** | **VESSEL** | The front enemy's attacks heal you instead of hurting you. |
| | **FEAST** | Double damage to enemies below 30% health. |

### What the ownership table cost

Nodes cut in the differentiation pass, and why:

| Cut | Was in | Reason |
|---|---|---|
| PILE, CLAMP, STRIP, RUST, ROT | VOLLEY, SNARE, SIGN, FIELD, DRAIN ring 2 | Five defence-break nodes with five names. Defence break is HAMMER's. |
| BARB, PIERCE, SLIP | SNARE, FIELD, DRAIN ring 2 | Three defence-ignore nodes. Also HAMMER's. |
| SAP (x2 of 3) | SNARE, FIELD ring 3 | Same attack break in three trees. DRAIN keeps it. |
| DRAG (x2) | SNARE ring 3, SIGN ring 2 | Same word, same mechanic (slow). Slow is FIELD's. |
| SNAP (SIGN), SPILL (SIGN), STORM (FIELD) | — | Duplicate names across trees. |
| CLIP, SCAR, FEED | VOLLEY, SIGN, VOLLEY | Slow, damage over time and lifesteal used outside their owner. |
| PLAGUE | DRAIN capstone | Area damage is FIELD's; replaced by VESSEL. |

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

A specialisation grants **access**: how far that style's skill tree opens. No road walked
and the skill plays in its base form; the road walked to its end and the capstone ring
unlocks. **Mastery grants access, skill level grants progress.** No player can take six
styles deep, so choosing an affinity is finally the real commitment the hexagon wanted.

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

| Tier | Node | Effect |
|---|---|---|
| Minor | **CHIME** | +6 resonance |
| Minor | **HUM** | +6 resonance |
| Minor | **TONE** | +8 resonance |
| Minor | **RESERVE** | +6 resonance, +5 health |
| Minor | **CLARITY** | +8 resonance |
| Minor | **TIMBRE** | +6 resonance, +4 attack power |
| Notable | **KEYED** | Your strong Source matchup pays 25% more. |
| Notable | **PLEDGE** | Vows pay 30% more. |
| Notable | **NARROW** | Your affinity style hits 20% harder, every other style 10% weaker. |
| Notable | **BROAD** | The opposite style's penalty is halved. |
| Notable | **DEEP** | +12 resonance, and resonance is worth 20% more. |
| Greater | **PURE** | While every woven skill shares one Source, all skills hit 35% harder. |
| Greater | **DISCORD** | Your weak Source matchup no longer weakens you. |
| Greater | **ZEALOT** | Vows pay 60% more; you cannot unswear one during a run. |
| Greater | **RESONANT** | +25% skill power, -15% basic attack damage. |
| Mastery | **CHORD** | Every Source matchup counts as strong. |

### LOOT — what you carry out

Sells haul, rarity, chest quality and the banking decision. **Must not duplicate the
Memory Dust tree**, which teaches the loot KEYSTONES (GREED, DISCERNING EYE, FORTUNE,
HOARDER) and crosses runs; this branch sells incremental value *within* a run.

| Tier | Node | Effect |
|---|---|---|
| Minor | **KEEN** | +6 guile |
| Minor | **POCKETS** | +8 guile |
| Minor | **SCAVENGE** | +6 guile |
| Minor | **LEDGER** | +6 guile, +5 health |
| Minor | **WEIGH** | +8 guile |
| Minor | **TALLY** | +6 guile |
| Notable | **CACHE** | Every 5th wave cleared drops an extra chest. |
| Notable | **PRUDENCE** | Retreating pays 75% of the haul instead of 60%. |
| Notable | **BLOODPRICE** | +30% haul while you are below half health. |
| Notable | **STASH** | Haul grows 2% per wave cleared, and resets when you bank. |
| Notable | **SIFTED** | Chests refused by your keep filter pay double compensation. |
| Greater | **GAMBLE** | +60% haul, but a wipe pays nothing instead of 30%. |
| Greater | **PATIENT** | Every 10 waves without banking, all chests gain a tier. |
| Greater | **LODE** | The last wave before you bank pays triple. |
| Greater | **SECOND LOOK** | Rarity is rolled twice; the better roll is kept. |
| Mastery | **PROSPECTOR** | Every wave cleared past depth 30 adds 3% haul for the rest of the run. |

### TEMPO — your own attack

Seven rules survive from the old branch and fill the upper tiers: FLASH, SURGE, INTERRUPT
and RHYTHM's replacement as notables, BRISK, HASTEN and FIRST STRIKE as greaters and
mastery. It has **no plain-stat minors**, because every node in the old tree was a rule.
Six are added:

| Tier | Node | Effect |
|---|---|---|
| Minor | **BITE** | +4 attack power |
| Minor | **FORCE** | +5 attack power |
| Minor | **SWIFT** | +3 engineering |
| Minor | **QUICK** | +3 engineering |
| Minor | **SHARP** | +1% critical chance |
| Minor | **POISE** | +4 focus |

### ENDURE — survival

Arrives with fourteen surviving rules against ten upper-tier slots, so four must be cut or
demoted when the branch is laid out; the overlap is between THICK SKIN and PADDING (both
flat bite reduction) and between MENDING and RECOVERY (both regeneration). Six plain-stat
minors are added:

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

### What DOES have to move, and must be measured not guessed

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

### The slot count is driven by two systems, and both change

| System | Today | After |
|---|---|---|
| `Unlocks.SkillSlots` | a new champion starts with 1 slot, earns up to 4 | unlock order becomes **active, passive, active, passive** — so the second slot a player ever earns already teaches the distinction |
| `DustEffects.SkillSlots` | the prestige node `weave_5` sells a 5th slot | the 5th becomes **the player's choice** of a third active or a third passive, decided at the workbench |

Making the fifth slot a choice rather than a fixed kind matters: a third active pushes beat
demand back up toward 0.6, which is a real cost the player should be electing.

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
| **6** | `WeaveScreen` shows 2 active + 2 passive | Screenshot |
| **7** | Skill trees: ring 0 only (the active/passive fork), then the outer rings | Per-tree tests |
| **8** | Mastery tree re-axed to TEMPO / ENDURE / RESONANCE / LOOT | Mastery tests |

Stages 1-6 are the playable slice: the fight changes, the swing comes back, and the trees
arrive afterwards as depth on top of a system that already works.

