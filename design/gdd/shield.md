# SHIELD

## 1. Overview

SHIELD is a second, temporary pool that stands in front of HEALTH for the length of one wave. Damage
that reaches the champion is taken out of the shield first and out of health only when the shield is
gone. It resets to nothing at the start of every wave, it is capped at half the champion's maximum
health, and nothing carries it from one wave to the next.

It exists because the game had exactly one defensive verb — *take less* — and every defensive purchase
was therefore the same purchase at a different size. Shield is a different verb: it is a resource a
build **produces**, from wave-start grants, from bites it prevents, from healing it cannot use. A hunter
running MACHINE opens every wave already protected and turns the first dangerous bite into more
protection; a hunter running NATURE stays near full health and banks the healing that would have been
wasted. Those are two defensive builds that do not feel alike, which the flat mitigation model could
never produce.

## 2. Player fantasy

> *"I run MACHINE BANKED and IRON, so enemy pressure becomes shield. My MACHINE set starts each wave
> protected and converts the first dangerous bite into more shield."*

The feeling is **the wave paying for its own aggression**. A shield build does not survive by being
harder to hurt; it survives by turning what the wave does into what it needs.

## 3. Detailed rules

### The state

`Champion.CurrentShield` — a float, wave-local, never persisted. `Champion.MaxShield` is derived from
the pool and is not stored. Every producer goes through one door (`GrantShield`), so the cap is enforced
once, the event reports what was **actually** added, and a grant at the ceiling says so rather than
lying about its size.

### Capacity and reset

| Rule | Value | Where |
|---|---|---|
| Cap | **50% of maximum health** | `ShieldRules.CapFraction` |
| Reset | **to zero, at the start of every wave** | `champ.ResetShield()` |
| Carry between waves | **never** | — |
| Carry between runs | **never** — it is not in the save | `SaveGame` has no champion runtime state |

Wave-start grants are applied **after** the reset, so they are a fresh start rather than a carry-over.

### Resolution order — the whole of it

A bite is resolved in exactly this order, and the order is the design:

1. **Mitigation.** `DamageTaken`, low-health absorption (which reads HEALTH, never the shield),
   `ShieldedDamageTaken` while a shield stands, flat reduction.
2. **Prevention.** IRON's whole-bite stop, then MACHINE's once-a-wave PLATING. Prevention is decided
   **before** the pool or the shield is touched, which is what lets a second preventer see that the bite
   is already gone and keep its charge (§53: PLATING must not be wasted on a bite JAWS already stopped).
3. **Absorption.** What is left is eaten by the shield, up to what the shield holds.
4. **Health.** Only what gets past the shield is health damage.

### What shield is NOT

These are the rules that keep it from becoming "health but better", and each one is a test:

- **A bite the shield ate is not damage taken.** REPAY banks *health* damage only.
- **A bite that was prevented is not damage taken either**, and it feeds nothing.
- **Health-scaling reads HEALTH.** GLUT's "the healthier you are, the harder you hit" and SCARRED's
  "hurt, it hits back harder" both read the pool. A shield standing in front of a half-empty pool does
  not make a hunter well.
- **Shield is not healing.** It does not spend the wave's healing ceiling, and NATURE's overflow —
  healing wasted at full health becoming shield — cannot recurse back into healing.

## 4. Formulas

```
MaxShield        = round(MaxHealth × 0.50)
GainShield(a)    = min(MaxShield, CurrentShield + a) − CurrentShield     // returns what was ADDED
AbsorbWithShield(d) = min(CurrentShield, d)                              // returns what was EATEN
healthDamage     = takenAfterPrevention − absorbed
```

Wave-start grant: `MaxHealth × WaveStartShieldFraction`, summed across the shape and every equipped
skill, then capped by the one door above.

## 5. Edge cases

| Situation | What happens |
|---|---|
| A grant while already at the cap | Adds nothing; the event reports 0, so the HUD shows nothing happened |
| A bite larger than the shield | The shield empties, `ShieldBroken` fires, the remainder hits health |
| A bite exactly the size of the shield | The shield empties and breaks; no health damage |
| Two preventers on one bite | The first spends its charge; the second sees `taken == 0` and keeps its own |
| PLATING with no bite that would hurt | The charge is never spent — it waits for a bite that would land |
| Overheal at full health with NATURE 5p | Half the wasted healing becomes shield; the pool does not move |
| The champion dies | The shield is gone with the wave; nothing carries |
| A screen opened mid-wave | `WaveReplay` keeps the running total, so the bar is right without having seen the grant |

## 6. Dependencies

`SoloBattle` (resolution order, every producer) · `SkillShape` (`WaveStartShieldFraction`,
`ShieldedDamageTaken`, `PreventFirstDamagingBite`, `OverhealToShield`) · `ElementSets` (the MACHINE
ladder, NATURE's capstone) · `SkillCatalogue` (SNARE/BANKED and its reinforcements) ·
`WaveReplay` (the running total the HUD draws) · `RunReport` (SHIELD ABSORBED in the Expedition Log).

## 7. Tuning knobs

| Knob | Now | Where |
|---|---|---|
| Cap fraction | 0.50 | `ShieldRules.CapFraction` |
| MACHINE 3p wave-start grant | 12% of maximum health | `ElementSets` |
| MACHINE 4p shielded mitigation | −10% | `ElementSets` |
| MACHINE 5p prevented-bite conversion | 100% of the stopped bite | `ElementSets` |
| NATURE 5p overheal conversion | 50% of what the pool could not use | `ElementSets` |
| BANKED / CARRIED wave-start grant | 10% of maximum health | `SkillCatalogue` |

## 8. Acceptance criteria

Twenty behaviours, all in `tests/unit/IdleXIdle.Core.Tests/Builds/shield_test.cs`:

- absorbs before health · post-mitigation · partial and full absorption · the 50% cap · reset every wave
- no cross-wave carry, asserted across six waves of a real expedition
- a prevented bite consumes no shield · a second preventer keeps its charge
- absorbed damage does not feed REPAY · does not count as damage taken · does not satisfy a
  health-scaling rule · does not make a hunter look hurt to a low-health rule
- `ShieldGained` reports what was **added**, not what was asked for
- `ShieldAbsorbed` and `ShieldBroken` fire exactly once each per event
- the HUD strip appears for a build that has shield and never for one that does not
- `WaveReplay` reconstructs the standing figure from the events alone

## 9. What it looks like

The Hunt HUD carries a thin steel strip immediately **above** the health bar — never over it, because
an overlay hides the number the player is watching. Half the pool's height, its own hard edge, vertical
scoring so it reads as plates rather than a meter, and the word and figure beside it. It appears from
the moment a run first grants shield and stays for the rest of it: a strip that comes and goes as bites
land is a flicker, and a bar you only ever see full is a bar you cannot learn.

A standing barrier is held around the champion for as long as the shield stands — restrained, breathing
slowly, because shield is a **state** and a state that flashes is indistinguishable from an event.
Gaining says `+42 SHIELD`. Absorbing says nothing: it happens on every bite a shielded champion takes,
and a figure on each would bury the health damage beside it. Breaking is loud, because from the next
bite the player is paying in health.

**One word.** The resource is SHIELD everywhere a player can read it. PLATING, BANKED and OVERGROWTH
name things that *grant* it; the resource itself has one name (`BuildGlossary.ShieldWord`) and one
sentence (`BuildGlossary.ShieldRule`).
