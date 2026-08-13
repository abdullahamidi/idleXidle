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
- Two characters are gated behind **quests**, which do not exist yet. They read as LOCKED and name
  their quest; `CharacterState.CompleteQuest` is the hook the quest system will call.
- The save carries **only the active id** and the finished-quest set.
- A character contributes to the build at exactly one place — `PlayerLoadout.ToBuild` — as the same
  three channels the two trees already use: `BuildMods`, `SkillShape`, and granted `BuildTrigger`s.

### The roster

Laid out on the mastery tree, so a player who has read the tree already knows half of it.

| Character | Road | Aptitude | Passive | Unlocked by |
|---|---|---|---|---|
| THE SEEKER | — | — | **EVEN HAND** — every skill hits 8% harder | Start |
| THE ANVIL | Weight | Strike | **DEADWEIGHT** — a third of overkill carries onward | Conquer Cinderworks |
| THE CHORUS | Spread | Aura | **MANY MOUTHS** — +5% damage per living creature | Conquer Umbral Reach |
| THE METRONOME | Tempo | Projectile | **FIRST BEAT** — opening cast free, at double force | Conquer Marrow Wastes |
| THE UNBROKEN | Endure | Transformation | **SECOND WIND** — first killing blow leaves you at 1 | Conquer The Still Archive |
| THE FALLING TOWER | Weight→Tempo | Strike | **MOMENTUM** — first hit weaker, every later hit stronger | Conquer The Pale Choir |
| THE QUIVER | Tempo→Spread | Projectile | **LOOSE AGAIN** — a kill fires the next shot immediately | Quest: the Hollow hunt |
| THE THORNWALL | Spread→Endure | Trap | **REPRISAL** — every bite worth less, every trap worth more | Conquer Verdant Hollow |
| THE OATHBOUND | — | Mark | **TWICE SWORN** — Vows pay more, Mark windows last longer | Quest: keep a Vow |
| THE MAGPIE | — | Trap | **FULL POCKETS** — richer haul, better rarity | Conquer Cinderworks |

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
- **Quests** — not built. Two unlocks wait on it.

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
