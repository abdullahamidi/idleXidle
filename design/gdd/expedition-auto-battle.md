# Expedition & Auto-Battle — GDD

## 1. Overview

The roster fights; the player builds. An **expedition** sends a squad of your creatures into a region
to auto-fight a rising sequence of **waves**. Combat resolves itself. After every wave the player faces
the only decision that matters: **BANK** the haul and go home, or **PUSH** one wave deeper for more.
Enemies compound faster than your squad can recover, so pushing forever is never correct — the run must
end in a choice, not a wall.

This replaces manual precision combat, which playtested as *"I'm pressing 3 buttons, win the fight,
then press R."* The depth moves from reflexes to **squad composition, slot order, and gear**.

## 2. Player Fantasy

You are a hunter who *sends* the pack, not one who swings the sword. The pleasure is watching a squad
you assembled tear through a wave you weren't sure it could survive — and the knot in your stomach at
wave 9 deciding whether to take one more. Mastery looks like *"I know exactly how deep this squad
goes."*

## 3. Detailed Rules

### 3.1 The squad
- Up to **6 ordered slots**. Order is meaningful: slot 1 is the front.
- Each creature costs **Supply** by power tier. The squad must fit under the player's **Supply Cap**.
  *This is the bounded budget: you cannot field six of your best.* Six weak or three strong is a real
  choice.

### 3.2 Roles do different jobs (all five finally matter in combat)
| Role | Job in an expedition |
|---|---|
| **Attacker** | Deals damage. |
| **Defender** | Soaks: enemies target the front-most living creature, and a Defender reduces damage to its **neighbours**. |
| **Support** | Heals — **mid-wave** via MEND, and again between waves. Sustain is what makes a deep push possible. |
| **Crafter** | Raises haul **quality** (rarity odds). |
| **Producer** | Raises haul **quantity** (cores/gleam). |

The composition tradeoff is the game: **combat roles let you push deeper; economy roles make the push
pay.** A greedy economy squad dies shallow; a pure combat squad survives deep for a thin haul.

### 3.2b Skills and passives — the two axes that cross

Slot order alone failed playtest: *"Yeah it matters but that is poor choice of tactics. Everybody puts
tank in front."* Correct — a decision with a strictly-correct answer is a sorting exercise, not a
decision. The fix is two **independent** axes, so no single arrangement dominates:

- **Role decides the SKILL** — what a creature does when its cooldown comes up.
- **Source decides the PASSIVE** — how it does it, always on.

5 roles × 6 sources = **30 combinations**, which is what turns *"which five do I bring"* into the real
question — and every skill firing is something to **watch**.

| Role | Skill | Effect |
|---|---|---|
| Attacker | **REND** | Triple damage. |
| Defender | **BULWARK** | It **and its neighbours** take half damage, briefly. |
| Support | **MEND** | Heals whoever is hurt worst. |
| Crafter | **SALVAGE** | Richer haul. |
| Producer | **BLOOM** | Buds a spare core. |

| Source | Passive | Effect |
|---|---|---|
| Nature | **REGROWTH** | Self-heals each wave. |
| Machine | **PLATING** | Takes 20% less. |
| Shadow | **MALICE** | Strikes twice ~25% of the time. |
| Body | **FURY** | Up to +60% damage as health drains. |
| Mind | **CLARITY** | Skill cooldown −25%. |
| Spirit | **ECHO** | Skill fires twice ~25% of the time. |

**BULWARK covers the Defender and its neighbours ONLY — never the whole squad.** A squad-wide shield
makes the Defender's position irrelevant and deletes the tactic. Per-slot it is a real dilemma: front =
tank one flank and eat the hits; middle = shield two but tank nothing.

**The skill clock runs on EXPEDITION time, not wave time** — see §7 for why this is load-bearing.

### 3.3 Adjacency (the depth layer, not a stat stack)
Neighbours interact. Effects are emergent, not additive:
- **Defender** reduces damage taken by its immediate neighbours.
- **Support** heals its neighbours more than distant slots.
- **Attacker** beside another Attacker focuses fire (bonus damage).

Slot ORDER therefore changes outcomes with identical creatures. That is the min/max surface.

### 3.4 Waves & the ratchet (asymmetric BY DESIGN)
- Wave *n* enemy power **compounds**: `enemyScale = 1.06^n`.
- Haul per wave grows **linearly**: `haulScale = 1 + 0.35n`.
- **Squad HP persists across waves (attrition).** Only a Support heals it back, partially.

**Where the tension actually comes from — corrected after measuring it.** An earlier draft of this doc
claimed "threat compounds and reward doesn't, so you fall behind," lifted from Loop Hero. **That is
false here.** Enemy scale does not overtake haul scale in absolute terms until ~wave 50, and real runs
end around **wave 8–12** — so across the entire playable range reward is comfortably *ahead* of threat.
Loop Hero's mechanism works because *its* player power comes from mid-run loot; ours doesn't.

The two real sources of tension are:

1. **Squad power is FIXED for the whole run** (no mid-run upgrades) while threat compounds. So the win
   probability `p` falls every single wave, and attrition drags it down further. A fixed squad therefore
   *always* eventually wipes — automatically, with no curve-crossing required.
2. **The priced exit puts the accumulated pot at risk.** Pushing is worth it only while
   `p · h(n+1) > 0.7 · H · (1 − p)` — where `H` is everything banked so far. As `H` grows and `p` falls,
   pushing turns **−EV**. *That* is the "one more wave?" knot, and it tightens on its own.

Threat still pulls away from reward asymptotically (exponential beats linear), which keeps deep pushes
from ever becoming free — but it is the backstop, not the engine.

### 3.5 The priced exit (stolen from Loop Hero)
| Action | Haul kept |
|---|---|
| **BANK** at a wave boundary | **100%** |
| **RETREAT** mid-wave | **60%** |
| **WIPE** (whole squad dies) | **30%** |

Nothing else in this game is at risk, so this is where all the tension lives.

**The price applies to LOOT as well as Gleam and cores.** A wipe that only costs coins makes pushing
one more wave nearly free, and the decision goes soft.

### 3.5b What you carry out — the expedition is the ONLY source of items

An expedition yields three things: **Gleam** (spent on COMMAND), **cores** (hatch the roster), and
**items** (the Forge — wear, merge, sell). Items are rolled from waves held, scaled by the exit price
above, and tilted in rarity by the haul's **quality** (Crafters and SALVAGE) and by region/corruption
tier. This is the loop's only power curve: gear worn in the Forge multiplies squad damage, which buys
depth, which buys better gear.

> **This link did not exist and nothing noticed.** The pivot replaced manual combat but left the sole
> `LootSystem.Roll` inside the old encounter results screen, whose update method stopped being called.
> **No item had ever dropped.** The Forge could not even be opened — its key was gated on
> `_encounter.Phase == Complete`, which stopped being reachable the moment combat stopped updating —
> while the help screen went on advertising "F — FORGE — EQUIP". `Gear.WeaponDamageMultiplier` was
> pinned at 1× forever, so the power curve had no source, and the Crafter was a pure trap: quality was
> its only payoff and quality was computed every wave and discarded unread. Every unit test passed
> throughout, because every system was individually correct and only the WIRING was severed.

### 3.6 Corruption pays IN KIND
Deepening corruption must **not** just raise numbers (the known Loop Hero failure: *"you always want to
play it safe"*). Each corruption tier unlocks **capability** — a new adjacency rule, evolution branch,
or gear archetype — so pushing is a route to *new toys*, not just bigger ones.

## 4. Formulas

```
SupplyCost(creature)  = 1 + floor(PowerTier / 4)
EnemyScale(n)         = 1.06^n                       # compounding threat
HaulScale(n)          = 1 + 0.35n                    # linear reward  -> falls behind BY DESIGN
CreatureHealth(c)     = 40 + 12 * PowerTier          # + Defender neighbour mitigation
CreatureDamage(c)     = (Role == Attacker ? 10 : 4) + 2.5 * PowerTier
AttackInterval(c)     = Role.WorkTickIntervalSeconds # reuses the existing per-role cadence
DefenderMitigation    = 0.30                         # to each immediate neighbour
SupportHealPerWave    = 0.20 * neighbour maxHP (0.10 to non-neighbours)
FocusFireBonus        = +25% damage when an Attacker neighbours an Attacker
HaulQuality(crafters) = 1 + 0.25 * crafters          # rarity odds
HaulQuantity(prods)   = 1 + 0.30 * producers
```

## 5. Edge Cases
- **Empty squad** → expedition cannot start.
- **Over Supply Cap** → cannot start; the UI must say which creature to drop.
- **No Attacker** → the squad deals chip damage only and will stall out; legal but bad (a real, learnable trap).
- **All creatures dead mid-wave** → WIPE at 30%; the squad returns at 1 HP (never permanently destroyed —
  losing the roster would be a rage-quit, not tension).
- **Wave sim exceeding a tick ceiling** (a stalled squad that can't kill) → resolved as a RETREAT at 60%,
  never an infinite loop.
- **Banking at wave 0** → returns an empty haul; legal no-op.

## 6. Dependencies
- `Creature` (Role, PowerTier, Source, evolution) — existing.
- `Region` / `Regions` for wave content and theme — existing.
- `Gear` for the Hunter's squad-wide bonuses — existing.
- `CorruptionScaling` — existing, but must be re-pointed at capability rather than numbers.
- **Replaces:** `Encounter` manual combat (telegraph, RPS counters, FLOW). To be retired.

## 7. Tuning Knobs
`SupplyCap`, `EnemyScaleBase` (1.06), `HaulScaleSlope` (0.35), `DefenderMitigation` (0.30),
`SupportHealPerWave` (0.20), `FocusFireBonus` (0.25), exit prices (100/60/30), tick ceiling.

**Skill cooldowns** — REND 1500ms, MEND 2000ms, BULWARK 2500ms, SALVAGE 2500ms, BLOOM 3000ms;
`BulwarkDurationMs` (1200ms), `RegrowthPerWave` (0.12), `MaliceChance` / `EchoChance` (0.25).

### 7.1 Why the skill clock is expedition-wide — a trap worth remembering

The first implementation restarted the skill clock every wave and fired on `ms % cooldown == 0`. Both
halves were wrong, and neither was visible from the code:

1. **A per-wave clock couples skill CADENCE to wave LENGTH.** A cooldown longer than the wave can never
   fire *at all*. Waves run 2–4s; cooldowns were authored at 5–10s. Measured on the shipped build, the
   first REND landed at **wave 15**, and BULWARK and BLOOM **never fired in an entire run** — against
   real runs that end at wave 8–12 (§3.4). The whole layer was dead code in every game anyone would
   actually play, and no test caught it because every test asserted on *outcomes* (depth, haul), which a
   dormant skill still nudges slightly.
2. **`ms % cooldown` silently requires every cooldown to divide into `TickMs`.** CLARITY's ×0.75 turned
   REND's 5000 into 3750, which no 100ms tick ever equals, so it actually fired every
   lcm(3750,100) = **7500ms — half as often as no passive at all.** The "−25% cooldown" passive made
   three of the five roles *slower*.

Both are fixed at the mechanism, not the numbers: readiness is tracked per fighter against an
**expedition-absolute clock** (`SkillClock`), which persists across waves exactly as squad HP does — the
unit of play is the expedition, not the fight. **Shortening the cooldowns alone would have re-broken the
moment wave length was retuned, which Phase 1 fully intends to do.**

Balance note for Phase 1: skills firing is a large, previously-absent power gain, so a fixed squad now
pushes materially deeper than the wave 8–12 §3.4 measured. **Re-measure that range against a playtest
before retuning `EnemyScaleBase`** — and note that raising enemy health lengthens waves, which is now
decoupled from skill cadence and so no longer changes it.

### 7.2 Loot knobs (§3.5b)

`RollsPerWave` (0.5), `QualityTiltScalar` (80), `MaxTiltPercent` (40), plus the whole of `LootTuning`.

- **`RollsPerWave` is deliberately well under 1.** A wave is not a kill's worth of loot; rolling once
  per wave turns a routine 12-wave bank into ~25 items, which drowns a Forge that merges 3 → 1 and
  trivialises depth as a reward.
- **`QualityTiltScalar` is what makes a Crafter worth its supply.** Quality runs 1.0 (none) → 1.25 (one)
  → 1.5 (two, or one plus SALVAGE); at ×80 a single Crafter reaches 20% tilt and two hit the 40%
  ceiling, so the role pays from the first one without any composition trivialising rarity.
- `KillContext.LootTiltPercent` was named `PartBreakLootBonusPercent`, after the manual-combat mechanic
  this pivot deleted. The lever is general and is now fed by haul quality.

**Second balance note:** gear now actually drops, so the squad's damage multiplier grows over a session
for the first time. Depth-per-run therefore rises across a play session as well as within it. Both this
and the skills gain above land on the same number (`EnemyScaleBase`) — **change it once, against a real
playtest, not twice against two guesses.**

## 8. Acceptance Criteria
1. A squad auto-resolves a wave with **no player input**, deterministically for a given seed.
2. **Slot order changes the outcome** with an identical set of creatures (adjacency is real).
3. Enemy scaling **outpaces** haul scaling, so a fixed squad always eventually wipes if it keeps pushing.
4. **BANK / RETREAT / WIPE** pay 100% / 60% / 30% of the accumulated haul.
5. Squad HP **persists across waves**; a Support meaningfully extends the survivable depth.
6. A Crafter raises haul rarity; a Producer raises haul quantity — both measurable.
7. Supply Cap **prevents** fielding an all-best squad.
8. A stalled squad terminates via the tick ceiling; **no infinite loop is possible**.
9. **Every role's skill actually fires inside a run somebody would really play** — not eventually, not
   in principle. Assert on the skill *happening*, never on the outcome it nudges; AC1–8 all passed with
   the entire layer dormant.
10. **CLARITY makes a skill fire more often — for every role.** Checked per-role, because the failure
    was pure arithmetic and two roles survived only by luck of their numbers.
11. **Skill readiness carries across waves.** A creature does not forget its cooldown because the next
    enemy walked up.
12. **The replay agrees with the sim.** Health shown on screen must equal simulated health, healing
    included — the bars are the only feedback an auto-battler has, and a bar that lies is worse than no
    bar. (A subtract-only replay once showed a Defender as a dead corpse while the sim had it at 188/190.)
13. **Every skill firing is visible**, distinguishable from an ordinary swing, and BULWARK's coverage is
    drawn from what the sim reports — the view never re-derives the rule.
14. **A run yields items, the items are wearable, and wearing them makes the next run go deeper.** Assert
    the whole chain, not its links: every link passed its own unit tests while the chain was severed and
    no item had ever dropped.
15. **A Crafter buys measurably better rarity than not bringing one** — end to end, from squad to item,
    not merely inside the `Haul` record (which is where AC6 checked, and why AC6 passed while the
    Crafter did nothing at all).
16. **A wipe costs LOOT, not just Gleam.**
17. **The player is shown what a run earned.** The results beat used to be erased in the same frame it
    was written, so the priced exit — the decision the game is built on — confirmed itself to nobody.
