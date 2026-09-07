# Characters

## 1. Overview

Ten playable characters share one account. A character is not a save slot and not a second
progression tree — the mastery tree, the trait tree, gear, Gleam and the Warren belong to the player
and never reset. What a character changes is the *shape* of the build the player already has: which
Form they are unusually good at, and one passive nobody else has. Switching is free and instant, so
the roster is a set of lenses on one collection of progress rather than ten parallel careers.

## 2. Player Fantasy

Not "start again as someone else" — *"take everything I have earned and hand it to a different
person."* The moment the system exists for is a player forty points into the tree who unlocks THE
QUIVER, switches, and finds their Projectile build is suddenly the build it was always trying to be.
The characters are also the game's cheapest source of authored personality: an idle game shows you
one figure for hundreds of hours, and it should be a figure you chose.

## 3. Detailed Rules

- The roster is **ten** characters. `CharacterRoster.All` is the catalogue.
- Exactly one is **active**. The starter (`seeker`) is unlocked from the first session.
- **Switching costs nothing and resets nothing.** No confirmation, no cooldown, no lost progress.
- **Unlocks are derived, never banked.** `CharacterState.Refresh` is handed the world's conquered set
  every frame, exactly as both trees derive their points. Nothing about unlocks is persisted, so a
  region rename migrates itself and a conquest can never be double-counted across a reload.
- Two characters are gated behind **quests** (`design/gdd/quests.md`). The roster shows the quest's
  demand and live progress; the host completes them through `CharacterState.CompleteQuest`.
- The save carries **only the active id** and the finished-quest set.
- A character contributes to the build at exactly one place — `PlayerLoadout.ToBuild` — as the same
  three channels the two trees already use: `BuildMods`, `SkillShape`, and granted `BuildTrigger`s.

### The roster

Laid out on the mastery tree, so a player who has read the tree already knows half of it.

| Character | Road | Aptitude | Passive | Unlocked by |
|---|---|---|---|---|
| THE SEEKER | — | — | **EVEN HAND** — every skill hits 8% harder | Start |
| THE ANVIL | Weight | Strike | **DEADWEIGHT** — a hit that leaves an enemy standing leaves a third of its damage in them; the next hit on that enemy lands it too | Conquer Cinderworks |
| THE CHORUS | Spread | Aura | **MANY MOUTHS** — +5% damage per living creature | Conquer Umbral Reach |
| THE METRONOME | Tempo | Projectile | **FIRST BEAT** — the wave's opening cast waits for nothing; the first hit on each enemy is doubled | Conquer Marrow Wastes |
| THE UNBROKEN | Endure | Transformation | **SECOND WIND** — first killing blow leaves you at 1 | Conquer The Still Archive |
| THE FALLING TOWER | Weight→Tempo | Strike | **MOMENTUM** — first hit weaker, every later hit stronger | Quest: THE LONG FURNACE (reach wave 50 in Cinderworks) |
| THE QUIVER | Tempo→Spread | Projectile | **LOOSE AGAIN** — a kill fires the next shot immediately | Quest: THE DEEP HOLLOW (reach wave 60 in the Verdant Hollow) |
| THE THORNWALL | Spread→Endure | Trap | **REPRISAL** — every bite worth less, every trap worth more | Quest: THE LONG STAND (hold wave 80 in Marrow Wastes) |
| THE OATHBOUND | — | Mark | **TWICE SWORN** — a Vow's bonus is worth +50%, Mark windows last longer | Quest: THE THIRD OATH (three descents with a Vow kept) |
| THE MAGPIE | — | Trap | **FULL POCKETS** — richer haul, better rarity | Quest: THE FULL HOLD (open 30 chests) |

Updated 2026-08-26 (tiered roster, round four): the FIRST of every class opens by conquest or at the
start; the SECOND waits on a quest no conquest can finish. The two Bulwarks used to arrive one
conquest apart (THE UNBROKEN on the fifth region, "conquer every region" on the sixth), so the
Bulwark's quest became an endurance hold; THE MAGPIE took the chest quest because its passive is
about loot, and THE QUIVER took the Hollow depth. On the ROSTER screen the ten stand in five class
columns, first above second.

The Endure+Weight bridge deliberately has no character: ANVIL and UNBROKEN already stand either side
of that corner.

## 4. Formulas

- **Aptitude** — `SkillShape.FormPower[form]`, a multiplier applied in `SoloBattle`'s damage function
  behind the same `skillForm` gate as every other tree effect, so it lifts woven skills and never the
  background auto-attack. Default 1.25; 1.15 where a character has a second strength, 1.30–1.35 where
  the aptitude *is* the character.
- **Passive** — whatever `SkillShape`/`BuildMods` fields the effect needs. Multipliers multiply,
  additives add, flags OR, exactly as `SkillShape.Combine` already defines.
- **Composition** — `PassiveMods = dust ⊗ mastery ⊗ character`, `Shape = mastery ⊕ character`,
  `ExtraTriggers = mastery ∪ character`.

## 5. Edge Cases

- **Unknown or locked id in a save** → falls back to the starter, and the id that was loaded is
  force-unlocked. You can never lose the character you were playing, even if the rules that granted
  them change.
- **A character with no strips generated** → the roster and the arena fall back to the still base
  sprite, then to a flat block. A missing clip degrades to the right person standing still, never to
  the wrong person.
- **Selecting a locked character** → refused, with the unlock condition as the message.
- **Two characters unlocked by the same conquest** (ANVIL and MAGPIE, both Cinderworks) — both are
  announced; `Refresh` returns every id that became available on that call.
- **Aptitude on a Form the player has not woven** → contributes nothing. That is the intended price
  of switching to a character whose strength you are not using.

## 6. Dependencies

- `MasteryCatalog.Branch` — the four roads a lean points at.
- `Abilities.Form` — the six Forms an aptitude can favour.
- `SkillShape` / `BuildMods` / `BuildTrigger` — the three channels a passive speaks through.
- `Encounters.World.ConqueredIds` — the unlock source.
- `UiKit.AnimSprite` — the strips the roster and the arena both draw.
- `Quests.QuestCatalogue` — two unlocks wait on it.

## 7. Tuning Knobs

- `Character.AptitudePower` — per character.
- Every field of a character's `Mods` and `Shape`.
- Which region unlocks whom (`CharacterUnlock.Conquest`).
- `CharacterRoster.StarterId`.

## 8. Acceptance Criteria

- [ ] A fresh save has exactly one character unlocked, and it is the starter.
- [ ] Conquering a region unlocks its character within one frame, without a reload.
- [ ] Switching character changes no tree, no point total, no gear, and no currency.
- [ ] The active character survives a save/load round trip; an unknown id falls back to the starter.
- [ ] A character's aptitude measurably changes damage for skills of that Form and nothing else.
- [ ] Every passive is read by the sim — no field exists only as screen text.
- [ ] The arena and the roster screen always show the same character.
- [ ] A character whose strips are missing still draws as themselves, standing still.

## 9. Item Classes (added 2026-08-25)

Playtest request: *"Items must have classes. Not every character should be able to wear every item."*
Every champion has an **item class**; each class has exactly **two** champions with different
passives, so a class-locked drop always has two builds it can serve and switching between them costs
nothing. Weapon, helm, chest, gloves and boots are **class-locked**; charm, ring and focus are
**universal** and fit everyone. The catalogue is `Economy.ItemClasses`; the rule is
`ItemClasses.CanWear`.

| Class | Road (mastery) | Champions | Weapon shapes | Sentence |
|---|---|---|---|---|
| WARDEN | WEIGHT | THE ANVIL, THE FALLING TOWER | blade, spear | Heavy blows. Built for the WEIGHT road. |
| RANGER | SPREAD | THE CHORUS, THE OATHBOUND | bow, spear | Many targets. Built for the SPREAD road. |
| MYSTIC | TEMPO | THE METRONOME, THE QUIVER | scythe, bow | Front-loaded skills. Built for the TEMPO road. |
| BULWARK | ENDURE | THE UNBROKEN, THE THORNWALL | blade, scythe | Lasting. Built for the ENDURE road. |
| WANDERER | none | THE SEEKER (starter), THE MAGPIE | all four | Any road. Wears every weapon shape. |

The brief placed THE QUIVER in RANGER and THE OATHBOUND in MYSTIC. THE QUIVER's lean is TEMPO, so it
sits in MYSTIC — a champion's lean and its class must point down the same road — and THE OATHBOUND,
who has no lean, takes the RANGER seat. `item_classes_test.cs` pins that no champion leans a road its
class is not built for.

Rules:

- **Rolling.** Every class-locked mint (chest contents, boss loot, the trader's stall, the forge's
  merge product) decides a class. Drops and stock are the active champion's class with probability
  `ClassRollTuning.OwnClassChance` (0.80), otherwise one of the other four uniformly. A merge product
  inherits the class of the first input of its own type, so a legacy trio fuses into a legacy piece.
- **Weapon family.** A weapon minted in a class stores a family from that class's list
  (`ItemInstance.Family`); an older weapon has none and derives its family from its id exactly as
  before, so no old bow becomes a blade. `CanWear` never rejects by family.
- **Legacy.** `ItemInstance.Class == null` on a class-locked slot means "made before classes" and is
  wearable by everyone ("ANY CLASS · OLD MAKE" on the card). The save and the share code carry both
  fields, null-default.
- **Wearing.** The GEAR screen lists other-class pieces dimmed with a lock, refuses EQUIP with the
  reason ("A WARDEN'S HELM — THE ANVIL OR THE FALLING TOWER CAN WEAR IT"), and EQUIP BEST skips
  them. Switching champion takes off anything the new champion cannot wear, returns it to the bag,
  says so in a toast, and saves.

Tuning knobs: `ClassRollTuning.OwnClassChance`; each class's `WeaponFamilies` and `ChampionIds`;
`ItemClasses.IsClassLocked` (which slots lock).

Acceptance:

- [ ] Every class has exactly two champions; every champion has exactly one class.
- [ ] A universal piece, a legacy piece, and an own-class piece are wearable; another class's is not.
- [ ] Over a seeded run, four in five class-locked chest drops are the opener's class.
- [ ] A minted weapon's family is one its class carries.
- [ ] A pre-class save loads with `Class == null` and the same id-derived family it always had.
- [ ] A share code round-trips class and family.
