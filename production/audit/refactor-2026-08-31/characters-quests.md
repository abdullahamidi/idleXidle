# Audit — Characters (ten champions) and character-unlock Quests

Date: 2026-08-31 · Branch: `feat/hunter-cutout-rig` · Mode: read-only source audit
Scope files: `src/ResonanceHunter.Core/Characters/{Character,CharacterRoster,CharacterState,LegacyUnlocks}.cs`,
`Quests/Quests.cs`, `Progression/Unlocks.cs`, `Economy/ItemClasses.cs`, `Game/{RosterScreen,CharacterScreen,Game1}.cs`,
`design/gdd/{characters,quests}.md`, the six test files under `tests/unit/ResonanceHunter.Core.Tests/`.

Every line reference below was read in this session. Greps are quoted with what they returned.
"Runtime" means the game binary; "test-only" means the symbol's only non-declaring consumer is a test.

---

## 0. Headline

The roster is in good structural shape: ten champions reach the simulation through exactly one seam
(`BuildComposer.Compose`, `BuildComposer.cs:120-133`) via the same three channels the trees use
(`BuildMods`, `SkillShape`, `BuildTrigger`), there is no `switch (character.Id)` anywhere in Core, quests
are an enum-plus-threshold over a flat snapshot, and unlocks are banked-and-derived with a working
legacy seed. The problems are at the edges. **(1) `Character.Aptitude` is a legacy-Form concept**: it
writes `SkillShape.FormPower[Form]`, the Form is derived from the skill's `LegacyForm` so it is really a
Style bonus wearing the old enum, and for THE OATHBOUND (`Form.Mark`) it is provably dead because a Mark
never enters the damage path — the roster card still prints "BEST AT MARK +30%". **(2) Two quest counters
live outside Core**: `ChestsOpened` is a field on `ForgeScreen` and `RunsWithVowKept` is a field on
`Game1`, both persisted through the host; THE MAGPIE's quest is the only gate in the game that depends
on a drop roll (flat 20% boss chest). **(3) Switching champion is not free**: the gear shed is announced,
but a woven skill known only through the old champion's `StartingSkillId` is silently dropped from the
composed build (`BuildComposer.cs:178`) while the ROSTER banner promises "YOU KEEP SKILLS". **(4) Stale
vocabulary**: WEIGHT/SPREAD survive in a player-facing class sentence, both GDDs describe a roster that
no longer exists (two quests, derived-only unlocks), and `Lean`, `ItemClassDef.Road`, `ClassTier`,
`CharacterUnlock.QuestText` and `QuestGoal.RegionsConquered` are UI-only, test-only or dead.

---

## 1. The ten champions (Q1)

Style → Form mapping used by the whole table: a skill's fight-time `Form` is `def.LegacyForm ?? (other
skill of the same style).LegacyForm` (`PlayerLoadout.cs:194-196`; the test does the same walk at
`champion_starting_skill_test.cs:50-53`). From `SkillCatalogue.cs` entries (lines 264-593):
Hammer=Strike, Snare=Trap, Sign=Mark, Volley=Projectile, Field=Aura, Drain=Transformation.
So "Aptitude Form X" is exactly "skills of Style S".

| Id | Name | StartingSkillId (exists? Style/Kind) | Class | Tier | Lean | Aptitude × Power | Passive | How the passive reaches the sim |
|---|---|---|---|---|---|---|---|---|
| seeker | THE SEEKER | `hammer_blow` ✓ (Hammer/Active, `SkillCatalogue.cs:264`) | Wanderer | First | null | none | EVEN HAND — "Every skill hits 8% harder." | `Shape.HitSize=1.08` (`CharacterRoster.cs:62`) → `SoloBattle.cs:807 m *= shape.HitSize * shape.DamageDealt` |
| anvil | THE ANVIL | `hammer_press` ✓ (Hammer/Field, `:291`) | Warden | First | Resonance | Strike ×1.25 (default) | DEADWEIGHT — "A third of the damage left over from a kill hits the next enemy." | `Shape.OverkillCarry=0.33` (`:79`) → `SoloBattle.cs:1076-1077`. Only writer of this field in the game (grep `OverkillCarry` → SkillShape.cs, CharacterRoster.cs, SoloBattle.cs only) |
| chorus | THE CHORUS | `field_mire` ✓ (Field/Field, `:513`) | Ranger | First | Loot | Aura ×1.25 | MANY MOUTHS — "+5% damage for each enemy alive in the wave." | `Shape.PerCreatureBonus=0.05` (`:93`) → `SoloBattle.cs:847`. Only writer |
| metronome | THE METRONOME | `volley_spray` ✓ (Volley/Active, `:431`) | Mystic | First | Tempo | Projectile ×1.25 | FIRST BEAT — "The wave's first cast has no wait. Your first hit on each enemy is doubled." | `Shape.FreeOpeningCast=true` (`:109`) → `SoloBattle.cs:517,1471,1492`; `FirstHitMultiplier=2` → `SoloBattle.cs:826`. Only writer of FreeOpeningCast |
| unbroken | THE UNBROKEN | `drain_wilt` ✓ (Drain/Field, `:568`) | Bulwark | First | Endure | Transformation ×1.25 | SECOND WIND — "Once per expedition, a hit that would kill you leaves you at 1 health." | `Grants=[Undying]` (`:121`) → `SoloBattle.cs:1954`. **Duplicates** keystone (`Keystones.cs:88`) and item enchant (`Build.cs:601 EnchantKind.Undying`) — three sources of one trigger, set-unioned so no stacking |
| tower | THE FALLING TOWER | `hammer_blow` ✓ (same as seeker) | Warden | Second | Resonance | Strike ×1.15 | MOMENTUM — "Your first hit on each enemy is weaker. Every hit after it is stronger." | `FirstHitMultiplier=0.85, LaterHitMultiplier=1.35` (`:138`) → `SoloBattle.cs:826`. Comment `:135-137` says it "cancels" ALPHA (`MasteryCatalog.cs:301` 1.9/0.80); it multiplies: 1.615 first / 1.08 later |
| quiver | THE QUIVER | `volley_weep` ✓ (Volley/Reaction, `:458`) | Mystic | Second | Tempo | Projectile ×1.35 | LOOSE AGAIN — "A kill sends the next shot immediately." | `Grants=[LooseAgain]` (`:155`) → `SoloBattle.cs:1049-1054`; **plus undisclosed** `Shape.SkillRate=1.10` (`:156`) → `SoloBattle.cs:530,643,664` |
| thornwall | THE THORNWALL | `snare_jaws` ✓ (Snare/Reaction, `:348`) | Bulwark | Second | Endure | Trap ×1.35 | REPRISAL — "Every hit you take does less. Every trap you set does more." | `FlatDamageReduction=6` → `SoloBattle.cs:1840`; `DamageTaken=0.90` → `:1830`. **The second sentence is implemented only by the Aptitude** (`TotalShape`, `Character.cs:212`) |
| oathbound | THE OATHBOUND | `sign_call` ✓ (Sign/Active, `:376`) | Ranger | Second | null | **Mark ×1.30 — DEAD** (see §2) | TWICE SWORN — "Vows pay far more. Your Marks last longer and hit harder." | `VowPowerMultiplier=1.5` → `SoloBattle.cs:2111`; `MarkWindowMultiplier=1.5` → `:1381,1508`; `MarkPowerBonus=0.25` → `:786` |
| magpie | THE MAGPIE | `snare_repay` ✓ (Snare/Active, `:321`) | Wanderer | Second | null | Trap ×1.25 | FULL POCKETS — "All the loot you bring home is worth more. Rare finds show up more often." | `Mods=(1,1,1,Haul 1.35,Rarity 1.20)` (`:217`) → Haul `SoloExpedition.cs:418`, Rarity `SoloExpedition.cs:508` and `Game1.cs:3538-3539 _forge.RarityBonus = wornBuild.Resolve(_hunter).Rarity`; Haul also feeds HOARDER (`SoloBattle.cs:818`) |

Liveness verdict: **no passive is text-only.** Every `Shape`/`Mods`/`Grants` value above has a reader in
`SoloBattle`/`SoloExpedition`. `characters_roster_test.cs:146-158` pins "touches some channel";
`roster_parity_test.cs:349-398` measures each on its claimed axis. Two text/implementation mismatches:

- **quiver**: the card never mentions the +10% skill rate the passive also grants (`CharacterRoster.cs:156`).
- **thornwall**: "every trap you set does more" has no field of its own — it is the Aptitude. Delete
  Aptitude and REPRISAL's second sentence becomes a lie with no test catching it
  (`characters_roster_test.cs:146-158` would still pass on the defence fields).

Starting skills: all ten resolve in `SkillCatalogue` (`champion_starting_skill_test.cs:36-67` composes each
one on a fresh tree). Nine distinct; **seeker and tower share `hammer_blow`** (`CharacterRoster.cs:53,129`).
Kinds are mixed (5 Active: blow, spray, call, repay, blow; 3 Field: press, mire, wilt; 2 Reaction: weep, jaws),
so the "active or passive starter" rule (`champion_starting_skill_test.cs:69-80`) holds.

---

## 2. Aptitude (Q2)

### Where it is consumed — the whole chain

1. Declared: `Character.Aptitude : Form?`, `AptitudePower = 1.25f` (`Character.cs:118,133`).
2. Expressed: `AptitudeShape => new SkillShape { FormPower = { [f] = AptitudePower } }` (`Character.cs:206-209`);
   `TotalShape = SkillShape.Combine(Shape, AptitudeShape)` (`:212`).
3. Folded: `BuildComposer.Compose … Shape = Combine(Combine(mastery.Shape(), character?.TotalShape ?? None), DustEffects.TreeShape(tree))`
   (`BuildComposer.cs:131-132`). This is the only Core consumer of `TotalShape`
   (grep `TotalShape` → `Character.cs`, `BuildComposer.cs:131`, two tests).
4. Read: **exactly one place** — `SoloBattle.cs:811 m *= shape.FormPowerFor(skillForm.Value)` inside the
   local `Amp(...)` (`:703`), behind `if (skillForm is null) return m;` (`:805`).
   grep `FormPowerFor` in `src` → `SkillShape.cs:205` (definition) and `SoloBattle.cs:811` only.
5. `Amp` is called from one site: `LandSpread` (`SoloBattle.cs:1113`; `:1141` is the same call wrapped in
   `SignatureAmp`). `LandSpread` is called for: Field ticks (`:1424`), Active casts (`:1682`), legacy
   woven extras (`:1715`), the auto-attack (`:1763`, `skillForm` null → no aptitude), Trap reactions (`:1943`).

### Consequence for THE OATHBOUND — the Mark aptitude is dead

A Sign skill never reaches `LandSpread`: the Active Mark path opens a window and `continue`s
(`SoloBattle.cs:1503-1546`, comment `:1538-1540` "It deals nothing"), and the Field amplifier path
`continue`s at `:1399` before the damage tick at `:1424`. There is no other reader of `FormPower`.
Therefore `Aptitude = Form.Mark, AptitudePower = 1.30f` (`CharacterRoster.cs:189`) multiplies nothing,
while the roster prints `BEST AT  MARK  +30%` (`RosterScreen.cs:305-309`) and the parity test sails past it
because THE OATHBOUND scores on Vows (`roster_parity_test.cs:143-165` weaves 1 Mark + 3 Strike; `:404`
claims axis "VOWS"). `characters_roster_test.cs:146-158` counts `hasAptitude` as "contributes something".

### Other writers of the same axis (numerical stacking)

- `GearShape.Of` writes `FormPower[f] = ItemFamilies.FormAffinity (1.25)` for the worn weapon family's two
  Forms (`GearShape.cs:22-29`, `ItemFamilies.cs:49-58`). `SkillShape.Combine` multiplies per key
  (`SkillShape.cs:465-467`, pinned by `characters_roster_test.cs:135-143`): quiver + bow = ×1.6875 on Projectile.
- `Build.Affinity : Form?` from a mastery Specialisation node → `FormBehaviour.AffinityFactor`
  (`SoloBattle.cs:709-712`, `MasteryTree.cs:253-257`) — a third Form-keyed multiplier on the same axis.
- RESONANCE branch fields `AffinityStyleBonus` / `OffStylePenalty` (`SkillShape.cs:72-73`) — a Style-keyed fourth.

### If Aptitude is deleted

- Loses its **only** mechanical distinction: **none.** All ten carry a non-default `Mods`, `Shape` or `Grants`
  (table above). THE MAGPIE loses its only *combat* effect and becomes pure loot, which is its stated axis
  (`roster_parity_test.cs:403`).
- Loses **half its card text**: THE THORNWALL ("every trap you set does more").
- Loses a **lie**: THE OATHBOUND's "+30% MARK".
- Distinct via starting skill + passive alone: yes for all ten on passive; on starting skill nine are
  unique and seeker/tower collide (both `hammer_blow`).
- Save impact: none — Aptitude is roster data, never serialised (grep `Aptitude` in `Persistence/` → nothing).
- Tests to change: `characters_roster_test.cs:122-158`, `roster_parity_test.cs:229 PlayedForm`,
  `champion_starting_skill_test.cs` (Form walk), RosterScreen `BEST AT` row, GDD §4/§7/§8.

If a per-champion "favoured style" is wanted after Form deletion, the current dial exists already: a
`Dictionary<Style,float>` beside `AffinityStyleBonus`, or simply fold the bonus into each passive's
`Shape` where the text promises it (thornwall). Do not keep `Form` alive for it.

---

## 3. Lean (Q3)

`Character.Lean : Branch?` (`Character.cs:98`). Consumers (grep `\.Lean\b` in `src`+`tests`):

- `RosterScreen.cs:106` card-edge colour, `:249-255` "RESONANCE"/"NO ROAD" line on the card,
  `:302` "ROAD: TEMPO / NONE — ANY WORKS" in the detail panel.
- `item_classes_test.cs:69-71` asserts `Lean is null || Lean == ItemClasses.Get(Class).Road`.

Nothing in Core reads it. `BuildComposer` does not; `SoloBattle` does not. It is **UI colour and one
label**. Its values are current (`Branch.Resonance/Loot/Tempo/Endure`, `MasteryCatalog.cs:28-51`) because the
renames were applied to the field, but the prose around it still describes the old tree: "the four
opposed roads … bridges between adjacent roads" (`CharacterRoster.cs:14-18`, `Character.cs:80`), "Weight's
structural weakness" (`:76`), "the thing Spread is for" (`:91`), "A Weight+Tempo bridge … exact inverse of
ALPHA … the two cancel" (`:135-137`). The GDD table's Road column still says Weight / Spread / Weight→Tempo /
Tempo→Spread / Spread→Endure (`design/gdd/characters.md:40-46`, `:57`).

`Lean` duplicates `ItemClassDef.Road` (`ItemClasses.cs:58,97,104,116,123,130`) except for THE OATHBOUND
(Class Ranger → Road Loot, Lean null). `ItemClassDef.Road` itself has **no runtime consumer**: grep `\.Road\b`
→ `item_classes_test.cs:69` plus `TraitRoads.cs:52` and `PrestigeScreen.cs` hits that are the unrelated
`TraitRoad` type. Two road fields, one UI reader, zero sim readers.

---

## 4. Unlock flow and LegacyUnlocks (Q4)

- `UnlockKind { Start, Conquest, Quest }` (`Character.cs:15-25`); `CharacterUnlock` factories (`:45-64`).
  Every FIRST is Start/Conquest, every SECOND is Quest (`champion_tiers_test.cs:37-45`). `ClassTier` is
  therefore derivable from `Unlock.Kind` — it exists as data for the card line and the grid
  (`Character.cs:154-155 TierLine`, `CharacterRoster.cs:263-276 BuildGrid`).
- `CharacterState` (`CharacterState.cs`): `_unlocked` HashSet seeded with the starter (`:29-32`),
  `_questsDone` (`:34`), `_activeId` (`:36`). `Refresh(conquered)` (`:61-82`) walks the roster every frame,
  banks newly earned ids, returns the fresh ones. `Select` refuses locked/unknown (`:85-90`).
  `Restore(active, quests, unlocked)` always re-adds the starter and the active id (`:115,123`) and drops
  ids the roster no longer has (`:118`). `SaveUnlocked()` is never empty (`:129-130`).
- Host: `Game1.cs:3547-3553` evaluates `QuestCatalogue.Satisfied(QuestSnapshot())`, calls
  `CompleteQuest`, toasts, saves; `:3571-3580` `Refresh(_world.ConqueredIds)` toasts each new champion
  (suppressed on the first frame by `_rosterBaselined`); dev fixture `:2126-2131` completes every quest.
- Persistence: `SaveGame.ActiveCharacterId` (`SaveGame.cs:102`), `UnlockedCharacters` (`:116`),
  `QuestsDone` (`:119`), `RunsWithVowKept` (`:129`), `ChestsOpened` (`:139`). Written at `Game1.cs:902-906`.
  Restored by `SaveSystem.RestoreCharacters` (`SaveGame.cs:648-660`): if `UnlockedCharacters` is empty the
  set is seeded from `LegacyUnlocks.Seed(conquered, questsDone, bestDepthByRegion, runsWithVowKept)`.
- `LegacyUnlocks.Before` (`LegacyUnlocks.cs:169-182`) — the pre-2026-08-26 gates, frozen:

  | id | old gate | current gate |
  |---|---|---|
  | seeker | Start | Start |
  | anvil | Conquest cinderworks | same |
  | chorus | Conquest umbral_reach | same |
  | metronome | Conquest marrow_wastes | same |
  | unbroken | Conquest still_archive | same |
  | tower | Conquest **pale_choir** | Quest q_cinder_deep (wave 50 Cinderworks) |
  | quiver | Quest **q_hollow_hunt** (depth 20 Hollow) | Quest q_quiver_hollow (wave 60 Hollow) |
  | thornwall | Conquest **verdant_hollow** | Quest q_marrow_hold (wave 80 Marrow) |
  | oathbound | Quest **q_first_vow** (1 kept Vow) | Quest q_three_vows (3) |
  | magpie | Conquest **cinderworks** | Quest q_magpie_chests (30 chests) |

  `Seed` re-evaluates the two retired quests from the save's own facts (`:203-209`) so an unlatched old
  quest still counts. Covered by `unlocked_characters_test.cs:23-102,150-158`. Three later-retired ids
  (`q_every_region`, `q_thirty_chests`, `q_hollow_deep`, documented `Quests.cs:117-133`) are **not** in
  `Before`; saves that had them done also had the bank, so nothing is lost. Classification: **B —
  migration-only, correctly isolated. Keep frozen.**

---

## 5. Quests (Q5)

### The catalogue (`Quests.cs:137-173`)

| id | name | goal | region | threshold | unit | unlocks | copy shape |
|---|---|---|---|---|---|---|---|
| q_cinder_deep | THE LONG FURNACE | DepthInRegion | cinderworks | 50 | WAVES | tower | "reach wave N in X" |
| q_three_vows | THE THIRD OATH | RunsWithVowKept | — | 3 | DESCENTS | oathbound | behaviour (latched event) |
| q_magpie_chests | THE FULL HOLD | ChestsOpened | — | 30 | CHESTS | magpie | economy counter |
| q_marrow_hold | THE LONG STAND | DepthInRegion | marrow_wastes | `ConquestWave*4 = 80` | WAVES | thornwall | "reach wave N in X" (copy says "Hold") |
| q_quiver_hollow | THE DEEP HOLLOW | DepthInRegion | verdant_hollow | 60 | WAVES | quiver | "reach wave N in X" |

Three of five are the same goal kind with a different region and number. "Hold wave 80" and "Reach wave 50"
are the same predicate (`Quest.Current`, `Quests.cs:61-68`); the verb is copy only.

### How progress is measured and whether it persists

`QuestSnapshot()` is built in the host (`Game1.cs:5170-5174`):

- **DepthByRegion** ← `_world.RegionFarm(id).BestDepth` for every region. Persisted in `SaveGame.RegionFarms`
  (`RegionFarmSave.BestDepth`, used at `SaveGame.cs:657`). Written at run end `Game1.cs:3700 RecordDepth(_expedition.Deepest)`.
  Deterministic given the build; no drop RNG; fully player-controllable. Persists.
- **RegionsConquered** ← `_world.ConqueredIds.Count`. **No quest uses this goal**
  (`Quests.cs:139-173`; `quests_test.cs:171` asserts none may). Dead enum member and dead snapshot field
  kept alive by the switch arm at `Quests.cs:64` and `champion_tiers_test.cs:179`.
- **ChestsOpened** ← `_forge.ChestsOpened` — a **presentation-layer field**: `ForgeScreen.cs:489-493`
  (`_chestsOpened`, `RestoreChestsOpened`), incremented at `ForgeScreen.cs:1183` when a chest is opened.
  Persisted as `SaveGame.ChestsOpened` (`:139`) through a host-side staging field because the Forge is
  constructed after load (`Game1.cs:677-686 _pendingChestsOpened`, `:1201 RestoreChestsOpened`). Chests
  arrive from bosses at a **flat 20% roll**: `Game1.cs:3737-3771 DropBossChest` →
  `Chests.DropChance(tier)` → `ChestTuning.DropChance = 0.20f` (`Chest.cs:175,260-261`), and the keep-filter
  can discard a rolled chest (`Game1.cs:3765-3769`). Expected ≈150 boss kills for 30 chests. **This is the one
  champion gate that depends on RNG drops.** Progress persists.
- **RunsWithVowKept** ← `Game1._runsWithVowKept` (`Game1.cs:321`), latched at run end
  (`:3710 if (VowWasKept()) _runsWithVowKept++;`) where `VowWasKept()` composes the current build and asks
  `Weaving.IsActive(v, SoloBattle.DescribeBuild(build, _hunter))` for any woven Vow. Persisted
  (`SaveGame.cs:129`). Deterministic and player-controllable, but gated behind learning a Vow on the trait
  tree (`PlayerLoadout` refuses an unstudied Vow, `PlayerLoadout.cs:203-206`). Persists.

Design-law note: two of the three live counters are **event latches owned outside Core** (Forge screen,
Game1). A Core `QuestLedger`/career-counter block would put them where the quest layer can be tested
without a host and where future behaviour quests would latch.

### Counters the sim exposes today (for behaviour-teaching quests)

- `WaveMetrics` (`SoloBattle.cs:108-146`): RawDamage, DeliveredDamage, Hits, Activations, TargetsStruck,
  CreaturesPresent, CreaturesKilled, HealthLost, DurationMs; derived AbsorbedFraction, AverageHitSize,
  TargetsPerActivation.
- `RunReport` (`RunReport.cs:27-59`): per-run averages of the above plus WallWave/WallArchetype/WallAffixes,
  CreaturesPerWave, HealthLostPerWaveFraction, SecondsPerWave, SampledWaves. Persisted per region as
  `RunReportSave` (`SaveGame.cs:318`).
- `BattleEventKind` (`WaveModel.cs:241-256`): Strike, EnemyStrike, Down, Heal, EnemyDown, Skill, Shield,
  Charge, Aura, Beat, Break — available for one wave as `SoloExpedition.LastWaveEvents` (`SoloExpedition.cs:160`).
- **Not measured today**: overkill spill (computed inline `SoloBattle.cs:1075`, not counted), reflected
  damage (REPAY/JAWS pay-back), amplify/Mark uptime (`champ.MarkUntilMs` is internal), field uptime,
  total healing (derivable from `Heal` events, not summed), defence-break depth.
- None of these is a **career** counter; a "reflect 10,000 damage" quest needs a latched, saved total exactly
  like `RunsWithVowKept`. Adding it as a Core ledger fed once per wave from `WaveMetrics` is the shape.

### Determinism summary

| quest | RNG-drop dependent | player-controllable | persists |
|---|---|---|---|
| q_cinder_deep / q_marrow_hold / q_quiver_hollow | no | yes | yes (RegionFarms) |
| q_three_vows | no | yes (needs a trait-tree Vow) | yes (latch) |
| q_magpie_chests | **yes** (20% boss roll + keep-filter) | statistically | yes |

---

## 6. Character switching (Q6)

Path: ROSTER card → `RosterScreen.Confirm` → `state.Select(id)` (`RosterScreen.cs:141-150`). Next frame,
`Game1.cs:3586-3587` detects the id change and calls `ShedUnwearable()` (`:3150-3172`): every worn piece the
new class cannot wear is `Unequip`ped back to the bag, a toast names them, `Save()`.

What is **not** per-character (all single instances in `Game1`): `_hunter` (Gleam stats, `:134`),
`_loadout` (`:250`), `_mastery` (`:251`), `_dust`, `_skillProgress` (`:340`; keyed by skill id only,
`SkillProgress.cs:34-40`), the Warren, gear. `CharacterState` holds only active id, unlocked set,
quests done (`CharacterState.cs:29-36`). So mastery, traits, skill levels, gear ownership and currencies
do not reset. The claim on the screen (`RosterScreen.cs:163` "SWITCH FREELY — YOU KEEP SKILLS, TRAITS,
GEAR AND THE WARREN") is true for ownership.

**But the composed build can change silently.** `BuildComposer.Compose` builds `taughtSkills =
mastery.LearnedSkills() ∪ { character.StartingSkillId }` (`BuildComposer.cs:154-155`) and drops any woven
pick not in it: `if (!taughtSkills.Contains(def.Id)) continue;` (`:178`). A skill known only through the old
champion (e.g. SEEKER's `hammer_blow` with no Hammer road walked) stays in `PlayerLoadout` but is omitted
from the fight after switching to THE MAGPIE. The gear shed is announced; this is not (the WEAVE screen marks
the row when opened, `WeaveScreen.cs:1039`, but nothing at the switch). Also: the class swap re-favours
drops (`Game1.cs:3589 _forge.FavouredClass`), the trader (`:2666`), and every screen's `Character`.

---

## 7. ItemClass (Q7)

- Catalogue `ItemClasses.All` (`ItemClasses.cs:89-135`): Warden {Blade, Spear} champions anvil/tower;
  Ranger {Bow, Spear} chorus/oathbound; Mystic {Scythe, Bow} metronome/quiver; Bulwark {Blade, Scythe}
  unbroken/thornwall; Wanderer {all four} seeker/magpie. Families are indices into `ItemNaming.WeaponFamilies`.
- **class → champions is stored twice**: `ItemClassDef.ChampionIds` string lists and `Character.Class`.
  `item_classes_test.cs:38-60` pins them equal; `champion_tiers_test.cs:133-141` pins the order. One could be
  derived from the other (`CharacterRoster.ByClass`, `CharacterRoster.cs:260-261`, already does).
- The rule: `CanWear(ItemClass, ItemInstance)` (`:179-185`) — universal slots always, class-locked slots
  (weapon/helm/chest/gloves/boots, `:156-157`) only own class or legacy `null`. Never by family (`:167-178`).
  `Gear.CanWear(Character, item)` is a one-line forward (`Gear.cs:76-77`).
- Mint paths: `LootSystem.cs:302,317`, `Chest.cs:376,387`, `Forge.cs:199-200`, `WanderingTrader.cs:70,93`
  roll class at `ClassRollTuning.OwnClassChance = 0.80` (`:76-82`) and a family from the class list.
- **Form/old-branch residue**: `ItemClassDef.Road : Branch?` (values current, no runtime reader, §3).
  `Description` for Ranger is player-facing and stale: `"Many targets. Built for the SPREAD road."`
  (`ItemClasses.cs:105`), drawn at `RosterScreen.cs:321`. Enum summaries `:30,33` and the class remark `:24`
  still say WEIGHT/SPREAD. The Warden entry was fixed on 2026-08-30 (`:93-98`); the Ranger entry was not.
- **Class → Form, indirectly**: the class's weapon families carry `ItemFamilies.FavouredForms`
  (`ItemFamilies.cs:49-54`: blade→Strike/Transformation, bow→Projectile/Mark, spear→Strike/Trap,
  scythe→Aura/Transformation) at ×1.25 (`:58`) through `GearShape.Of`. So a Warden's weapons favour
  Strike/Transformation/Trap Forms — this is the Form-based class mapping the brief asks to migrate; it lives
  in `ItemFamilies`, not `ItemClasses`.
- **Does class hard-lock a Style?** No. Nothing gates a weave by class (grep `Class` in `BuildComposer.cs`,
  `WeaveScreen.KnownSkills` `WeaveScreen.cs:1372-1377` → mastery + starting skill only). A Warden may weave
  Volley. The only class↔style link is the +25% weapon-family affinity above.

---

## 8. Cross-cutting findings

### 8.1 Core ↔ presentation coupling

| where | note |
|---|---|
| `ForgeScreen.cs:489-493,1183` | Career counter `ChestsOpened` that feeds a Core quest goal lives on a MonoGame screen; saved via `Game1.cs:906` and restored through `_pendingChestsOpened` (`Game1.cs:686,836,1016,1201`). |
| `Game1.cs:321,3710,5170-5174` | Quest latch `_runsWithVowKept` and the whole `QuestProgress` snapshot are host-owned; Core quests cannot be evaluated without the host assembling facts from three subsystems. |
| `Character.cs:181-197` | Core record carries animation clip-fallback logic keyed by Form-name strings (`"strike"→"attack"`, `"projectile"…→"cast"`). Consumed at `SoloExpeditionScreen.cs:3642`. |
| `SoloExpeditionScreen.cs:3277-3280` | The view derives the clip name by casting `BattleEvent.Amount` to `Form`; `SkillDef.ClipKey` (`SkillCatalogue.cs:166`, authored `:267-571`) has **zero consumers** (grep `\.ClipKey` in `src` → none). |
| `CharacterRoster.cs:230-283` | ROSTER grid layout (`ClassColumns`, `Grid`, `RosterCell`, row/column indices) is screen layout held in Core. Testable, but it is presentation data. |
| `RosterScreen.cs:91-104` | Own `BranchName`/`BranchColor` tables ("the same four colours the mastery tree uses") — a second copy of the tree's palette. |

### 8.2 Duplicated responsibilities

- `CharacterUnlock.QuestText` vs `Quest.Demand` — the same sentence authored twice (`CharacterRoster.cs:141,161,179,204,222`
  vs `Quests.cs:142,148,154,163,170`). `RosterScreen.cs:123` prefers `Demand`; the fallback can only fire when
  a quest id is missing, which `quests_test.cs:65-75` forbids. `characters_roster_test.cs:235` requires the copy anyway.
- `Character.Class` vs `ItemClassDef.ChampionIds` (§7).
- `Character.Lean` vs `ItemClassDef.Road` (§3).
- `Character.Tier` vs `Unlock.Kind` (§4) — and `RosterCell.Row == (int)Tier`.
- `UnlockFacts` (`Unlocks.cs:60-66`) vs `QuestProgress` (`Quests.cs:91-95`) — two host-built snapshots of
  overlapping facts (`RegionsConquered` in both; `ChestsEverHeld` vs `ChestsOpened`; `DeepestWave` vs
  `DepthByRegion`), assembled at `Game1.cs:2995-3005` and `:5170-5174`.
- `BuildTrigger.Undying` granted by character (`CharacterRoster.cs:121`), keystone (`Keystones.cs:88`) and
  enchant (`Build.cs:601`).
- `LegacyUnlocks.Seed` re-implements `CharacterState.Refresh`'s switch (`LegacyUnlocks.cs:211-220` vs
  `CharacterState.cs:70-76`) — acceptable for a frozen migration table.

### 8.3 Numerical stacking (multiplicative soup on one axis)

| path | kind | note |
|---|---|---|
| `SkillShape.FormPower` | aptitude × weapon family (`GearShape.cs:29`) × `Build.Affinity` factor (`SoloBattle.cs:709-712`) | three Form-keyed multipliers on "your favoured style", plus RESONANCE's `AffinityStyleBonus`. |
| `FirstHitMultiplier`/`LaterHitMultiplier` | tower 0.85/1.35 × ALPHA 1.9/0.80 (`MasteryCatalog.cs:301`) × metronome 2.0 | `CharacterRoster.cs:136-137` claims tower "cancels" ALPHA; the product is 1.615/1.08. |
| SkillRate | `mods.SkillRate * shape.SkillRate` (`SoloBattle.cs:530,643,664`) | two channels for one stat; quiver writes the `Shape` one (`:156`), gear/dust write the `Mods` one. |
| `MarkWindowMultiplier` | oathbound 1.5 × ElementSets 1.5 (`ElementSets.cs:89`) × LINGER ×1.8 (`SoloBattle.cs:1382,1507`) × mastery 1.3 (`MasteryCatalog.cs:443`) | |
| `VowPowerMultiplier` | oathbound 1.5 × mastery 1.30/1.60 (`MasteryCatalog.cs:179,198`) × Dust (`DustEffects.cs:232-243`) | |
| `DamageTaken` | thornwall 0.90 × mastery 0.70/1.15/1.25 (`MasteryCatalog.cs:198,242,327,389`) | |

### 8.4 Dead or test-only fields

| field | declared | evidence |
|---|---|---|
| `ItemClassDef.Road` | `ItemClasses.cs:58` | grep `\.Road\b` src+tests → `item_classes_test.cs:69` only; other hits are `TraitRoad` (`TraitRoads.cs:52`, `PrestigeScreen.cs`). |
| `QuestGoal.RegionsConquered` + `QuestProgress.RegionsConquered` | `Quests.cs:21,64,94` | grep `QuestGoal.RegionsConquered\|\.RegionsConquered\b` (excluding `UnlockFacts`) → `Quests.cs:64`, `Game1.cs:5172` (writer), `quests_test.cs:171` (asserts unused), `champion_tiers_test.cs:179`. No quest has this goal. |
| `CharacterUnlock.QuestText` | `Character.cs:63` | grep `QuestText` → `RosterScreen.cs:123` fallback after `QuestCatalogue.Find(...).Demand`, `characters_roster_test.cs:235`. Unreachable while `quests_test.cs:65-75` holds. |
| `Character.Aptitude` for `oathbound` (`Form.Mark`) | `CharacterRoster.cs:189` | `FormPowerFor` read only at `SoloBattle.cs:811` inside `Amp`; `Amp` only from `LandSpread` (`:1113`); Mark paths `continue` at `:1399` and `:1546` before any `LandSpread`. |
| `Character.Lean` | `Character.cs:98` | UI-only (§3). |
| `SkillDef.ClipKey` (cross-area) | `SkillCatalogue.cs:166` | grep `\.ClipKey` src → no consumers; `Character.GenericClipFor` does the job by Form name. |
| `ForgeScreen.ChestsOpened` comment | `ForgeScreen.cs:488,1183` | "credit CRAFTER evolution" — retired 2026-08-24 per `SaveGame.cs:135-137`; the counter's live consumer is the quest + STATS. |

### 8.5 Stale terms

| term | where | replacement |
|---|---|---|
| "Built for the SPREAD road." (player-facing) | `ItemClasses.cs:105`, drawn `RosterScreen.cs:321` | "Built for the LOOT road." (`Branch.Loot`) |
| WEIGHT / SPREAD in comments | `ItemClasses.cs:24,30,33,54`; `CharacterRoster.cs:76,91,135`; `Character.cs:80` "four opposed roads" | RESONANCE / LOOT; drop the "opposed roads / bridges" topology prose |
| "BEST AT  STRIKE +25%" (Form names to the player) | `RosterScreen.cs:305-309` | Style names (HAMMER…) or delete with Aptitude |
| "Every Source and every Form is available to you" (slot-4 note to the player) | `Unlocks.cs:254-255`; comment `:130-131` "what a Form does" | skill/style vocabulary |
| GDD: Road column Weight/Spread/bridges | `design/gdd/characters.md:40-46,57,123-124` | current branches or remove column |
| GDD: "Unlocks are derived, never banked … save carries only the active id" | `characters.md:24-29` | banked since 2026-08-26 (`CharacterState.cs:18-24`, `SaveGame.cs:116`) |
| GDD: "Two characters are gated behind quests" / "two unlocks wait on it" / "ANVIL and MAGPIE, both Cinderworks" | `characters.md:27,80,92` | five; magpie is quest-gated |
| GDD: "aptitude … skills of that Form" | `characters.md:62-65,82-83,96,107` | rewrite or delete with Aptitude |
| GDD quests: "Two quests", THE FIRST VOW / THE HOLLOW HUNT as live, "THE FIRST VOW is LATCHED" | `design/gdd/quests.md:5,14-16,24,83` | five quests; retired ids are in §3's own note |
| GDD quests: "ForgeScreen.ChestsOpened — for the goal … no quest currently uses" | `quests.md:71` | THE FULL HOLD uses it |
| "Resonance Hunter" / `ResonanceHunter` | namespaces/usings in every file of the area | `IdleXIdle` (product-wide, not area-specific) |

### 8.6 Serialization risks

| field | risk | migration |
|---|---|---|
| `SaveGame.UnlockedCharacters` | Empty means "pre-bank save" (`SaveGame.cs:652`). Safe today because `SaveUnlocked()` always writes the starter; a future write path that emits `[]` would re-seed from `LegacyUnlocks` and could hand back old-gate champions. | Keep the invariant pinned (`unlocked_characters_test.cs:133-137`). |
| champion id rename | `CharacterState.Restore` drops unknown ids silently (`CharacterState.cs:118`) and `ActiveCharacterId` falls back to the starter (`:120-122`) — a rename is a loss for every existing save. | Add an id-alias map in `RestoreCharacters` before any rename. |
| `SaveGame.QuestsDone` | Retired ids are retained; reusing a retired id grants a free champion (`Quests.cs:111-133`). | Never reuse; add a test that retired ids are absent from the catalogue (partly exists `quests_test.cs:170,186-187`). |
| `SaveGame.ChestsOpened` | Owned by a screen; restored through `_pendingChestsOpened` ordering (`Game1.cs:677-686` records a past crash here). | Move the counter to Core; restore with the rest of Core state. |
| `SavedSkill.Form` (cross-area) | If `Form` is deleted, `PlayerLoadout.SetSkill` and the save both lose the field the aptitude keys on; Aptitude itself is not serialised. | Aptitude deletion has no save impact; `SavedSkill.Form` migration belongs to the skill area. |

---

## 9. Classification and recommendations

**Authoritative (keep as the spec of this area)**

- `Characters/CharacterRoster.cs` — the ten champions; passives via `Mods`/`Shape`/`Grants`; `StartingSkillId`.
- `Characters/CharacterState.cs` + `Persistence/SaveGame.cs:648-660` — banked+derived unlocks, "never taken back".
- `Quests/Quests.cs` — enum goal + threshold over a flat snapshot; `QuestCatalogue`.
- `Economy/ItemClasses.cs:156-192` — `IsClassLocked` / `CanWear`, the single wear rule.
- `Builds/BuildComposer.cs:120-133,154-178` — the one seam where a champion reaches the sim.
- Tests: `champion_tiers_test.cs` (no conquest finishes a second's quest), `roster_parity_test.cs`
  (measured, per-axis), `unlocked_characters_test.cs` (legacy seed), `champion_starting_skill_test.cs`.

**Obsolete / to change**

| name | files | class | why | replacement |
|---|---|---|---|---|
| `Character.Aptitude`, `AptitudePower`, `AptitudeShape`, `TotalShape` | `Character.cs:117-133,205-212`; `CharacterRoster.cs` per entry | C | Legacy-Form concept; dead on oathbound; only real dependency is THORNWALL's second sentence. | Fold the promised bonus into each passive's `Shape` (thornwall) or a Style-keyed field beside `AffinityStyleBonus`; delete the `BEST AT` row. |
| `Character.Lean` | `Character.cs:98`; `RosterScreen.cs:106,249,302` | C | UI-only; duplicates `ItemClassDef.Road` except oathbound. | Roster reads `ItemClasses.Get(c.Class).Road` (decide oathbound's line), or delete both and show class only. |
| `ItemClassDef.Road` | `ItemClasses.cs:58,97-130` | A or C | No runtime reader. | Becomes the roster's single road source if `Lean` goes; otherwise delete. |
| `CharacterUnlock.QuestText` | `Character.cs:52-63`; `CharacterRoster.cs` quest entries | A | Duplicate of `Quest.Demand`; fallback unreachable under `quests_test`. | `Quest.Demand`; update `characters_roster_test.cs:235`. |
| `QuestGoal.RegionsConquered` + `QuestProgress.RegionsConquered` | `Quests.cs:21,64,94`; `Game1.cs:5172`; `champion_tiers_test.cs:179` | A | No quest may use it (test-enforced). | Delete; re-add if a quest needs it. |
| `Character.GenericClipFor` / `StripKeys` Form-name fallback | `Character.cs:163-197`; `SoloExpeditionScreen.cs:3277-3280,3642` | C | Presentation reads `(Form)BattleEvent.Amount`; `SkillDef.ClipKey` is authored and unread. | View resolves the clip from the fired skill's `Def.ClipKey`; keep the attack/cast fallback in the view. |
| `CharacterRoster.Grid`/`RosterCell`/`ClassColumns` | `CharacterRoster.cs:230-283` | C (low priority) | Screen layout in Core. | Derive in `RosterScreen` from `ByClass` + `Tier`; keep `roster_grid_test` against the screen helper. |
| `ClassTier` | `Character.cs:35-42,115,154` | keep (advisory) | Derivable from `Unlock.Kind`, but it is the card's word and cheap. | Consider `Tier => Unlock.Kind == Quest ? Second : First`. |
| `LegacyUnlocks` | `LegacyUnlocks.cs` | B | Migration table for pre-bank saves. | Keep frozen. |
| `ChestsOpened` on `ForgeScreen`, `_runsWithVowKept` on `Game1` | `ForgeScreen.cs:489-493,1183`; `Game1.cs:321,3710` | C | Core quest facts owned by presentation/host. | A Core career-counter block (saved as one), fed by the Forge open and the run-end frame; `QuestProgress` built from it + `World`. |

---

## 10. Open questions

1. Is Aptitude a concept the owner wants at all after Form deletion, or should each passive simply say what it does?
2. THE OATHBOUND's "+30% MARK" is dead — remove the line, or move the bonus onto Mark depth (`MarkPowerBonus`) where it would be real?
3. If `Lean` is replaced by `Class.Road`, THE OATHBOUND's card reads "LOOT" instead of "NO ROAD" — acceptable?
4. Switching champion can silently drop a woven skill known only via the old `StartingSkillId` (`BuildComposer.cs:178`). Should the switch toast it like the gear shed, or should a champion's starting skill stay taught after you have played them?
5. THE MAGPIE's gate is the only RNG-drop gate (20% boss chest, `Chest.cs:175`; ≈150 bosses). Intended "effort", or should it count chests *earned* including filtered ones?
6. `q_marrow_hold` says "Hold" but tests `BestDepth >= 80` like the other depth quests — is there a distinct "survive the wave" semantic wanted?
7. `VowWasKept()` judges the build **at run end** with `Weaving.IsActive` on the current context; if the player edits the build mid-descent the judgement follows the final build. Acceptable?
8. Should the roster's `BranchName`/`BranchColor` (`RosterScreen.cs:91-104`) read the mastery screen's palette to stop a second copy drifting?
9. Do the two snapshot structs (`UnlockFacts`, `QuestProgress`) plus the tutorial's facts want to become one host-built `PlayerFacts`?
