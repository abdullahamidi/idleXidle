# Audit — Gear, item layers, sets, forge, loot, and the numerical stacking

Date: 2026-08-31 · Branch: `feat/hunter-cutout-rig` (HEAD `ae30f2a`) · Scope: `src/ResonanceHunter.Core/{Economy,Loot,Forge,Builds/GearShape.cs,Expeditions/{WaveSpoils,ExpeditionLoot,Chest,GiftChests}.cs}`, the Game screens that consume them, the Economy/Loot/Forge tests, and the four design docs named in the brief.

Method: every file in scope was read end to end; every consumer claim below is backed by a `grep -rn` over `src/` (excluding `bin/ obj/ .git/ docs/art-reference/`) and the line numbers quoted were read, not inferred. Where I did arithmetic on the curves the inputs are quoted so the numbers can be re-derived.

---

## 0. Headline

The item system is in better shape than the rest of the brief's "dormant feature" history suggests: every layer that exists today has a fight consumer, and the 2026-08-23 runaway ("1.5m item power") was closed by making the eight worn slots stack **additively with a saturating ceiling** (`GearMods.Stack`, `GearTraits.cs:40-66`). The remaining problems are structural, not liveness:

1. **Form is still load-bearing in three gear layers** — six enchantments need a `Form` (`Enchantments.cs:175-180`), weapon families favour `Form`s through `SkillShape.FormPower` (`ItemFamilies.cs:49-55`, `GearShape.cs:25-29`), and the battle gates those enchants on `form == Form.Projectile` / `Form.Strike` (`SoloBattle.cs:1551, 1613`). Because `PlayerLoadout.SetSkill` bridges every catalogue skill to its Style's `LegacyForm` (`PlayerLoadout.cs:194-197`, `SkillCatalogue.cs:646-655`), Form and Style are one-to-one today, so the migration is a rename of the key, not a redesign — but 5 of 8 slots draw their whole enchant pool from the Form-combo list (`Enchantments.cs:236-245`).
2. **The original three slots are double-layered.** Weapon, Charm and Focus keep a rarity-DIRECT bonus (`Gear.WeaponDamageMultiplier`, `CharmDefenseBonus`, `CharmHealthBonus`, `Hunter.CharmToughness`, `Hunter.FocusAttunement`) on top of the trait + affix + family + gem + enchant + set layers every slot has. The Charm's rarity enters health **twice** (flat `+25×RP×ILF` into `Hunter.MaxHealth`, `HunterProgression.cs:225`; and `×(1+0.06×RP×ILF)` into `SquadHealthMultiplier`, `:400-416`), and both reach `SoloBattle.ChampionHealth` (`SoloBattle.cs:2052-2059`).
3. **The stacking is additive-saturating inside one channel but multiplicative across channels and layers.** Damage on a skill hit is `WeaponMult(≤15×) × WornMods.Damage(<4×) × FormPower(1.25) × SourceBonus(1.16) × Cull(1.12) × crit(≤2.35)` before the build's own chain; skill rate is `FocusAttunement(≤1.7) × WornMods.SkillRate(<2.5) × TEMPO(≤1.78) × keystones × passives × shape.SkillRate` with only ONE of those six factors ceilinged. Defence is an **unbounded additive sum** (affixes + WARD gems) under a hyperbolic `100/(100+D)`.
4. **Roughly half of `LootSystem.Roll`'s work is discarded**: it always mints a `CreatureCore` (`LootSystem.cs:255`) that `ExpeditionLoot.RollBoss` filters out (`ExpeditionLoot.cs:115`), and 45% of drops are `ItemBaseType.Material` items (`:263`) that `Chests.Open` filters out (`Chest.cs:344`). Neither type ever reaches a live bag. Nine of `MergeRecipe.HybridTable`'s ten authored recipes are therefore unreachable.
5. **Dead members**: `ItemInstance.EquippedToCreatureId` is never assigned anywhere (so `Forge.CheckEligible` can only return `Eligible`), `IneligibleReason.VowBound` is never produced, `ForgeOperation` has zero references, `Forge.Feed`/`FeedConversionRate` have no caller outside tests, `Charter.Merge` is never dropped and only converted on load, `Gear.ItemScore`/`Gear.WearableTypes`/`Gear.ClassOf` have no runtime caller, and the manual-combat `KillContext.IsBoss/AutomationStage/ActiveEfficiencyPercent` knobs are never set by any live path.
6. The three design docs for this area (`item-data-schema.md`, `loot-drop-system.md`, `the-forge-system.md`) describe a different game — creature cores, part-break loot buckets, feed-to-creature, Vow binding, `enchantment_material` — and do not mention affixes, prefixes, sets, gems or classes. Only `design/art/equipment-design-language.md` describes the shipped model, and it is one revision behind (11 enchants, not 15).

---

## 1. Item layer inventory

`ItemInstance` (`Loot/LootSystem.cs:37-139`) carries: `InstanceId, BaseType, Rarity, SellValue, ItemLevel, Upgrades, Element, EquippedToCreatureId, Gems, TraitOverride, EnchantOverride, Class, Family`. Everything else is **derived** — from the id (affixes, enchant, gem stat, legacy family/art seed) or from the rarity/level curves.

| Layer | Defined | Rolled / set at mint | Consumed in fight / economy | What it does | Notes |
|---|---|---|---|---|---|
| **Base type / slot** | `ItemBaseType` (`LootSystem.cs:11-17`), `GearSlot` + `Gear.SlotFor` (`Gear.cs:17, 39-50`) | `LootSystem.Roll` (`:263-268`: 45% Material item, else favoured-pool 50% / uniform wearable); `Chests.MintGear` (`Chest.cs:371`); trader hash (`WanderingTrader.cs:69`); gift catalogue | `Hunter.Equip` (`HunterProgression.cs:196-204`); every slot-switch below | Which of 8 slots; which trait/enchant pool; class-locked or universal | `CreatureCore`/`Material` **items** never reach a live bag (§8). |
| **Weapon family** | `ItemNaming.WeaponFamilies` `{blade,bow,spear,scythe}` (`ItemNaming.cs:22`); `ItemInstance.Family` (`LootSystem.cs:138`) | `ItemClasses.RollFamily` from the class's list (`LootSystem.cs:317`, `ItemClasses.cs:233-238`); legacy: id hash (`ItemNaming.cs:41-44`) | (a) flat channel `ItemFamilies.BonusOf` → `Hunter.AffixTotals` (`HunterProgression.cs:352-354`); (b) `FavouredForms` → `GearShape.Of` → `SkillShape.FormPower` → `SoloBattle.Amp` `:811` | Blade +DMG, Bow +CRIT, Spear +SKILL RATE, Scythe +LOOT (`ItemFamilies.cs:30-31`), `×(1+0.06×(iL−1))` capped 4×; plus ×1.25 to two **Forms** | **Form-keyed** (`ItemFamilies.cs:49-55`). Family index is a magic int shared with `ItemClasses.cs:87`. |
| **Class** | `ItemClass` (`ItemClasses.cs:28-44`), `ItemInstance.Class` | `ItemClasses.Roll` 80% own class (`LootSystem.cs:302`, `ItemClasses.cs:208-217`); merge inherits majority (`Forge.cs:132-141, 187`) | `ItemClasses.CanWear` (`:179-185`) via `Gear.CanWear`; EQUIP BEST filter (`CharacterScreen.cs:391`); auto-merge grouping (`ForgeScreen.cs:797`) | Weapon/Helm/Chest/Gloves/Boots locked to 1 of 5 classes; Charm/Ring/Focus universal (`:156-160`) | Ranger description still says "SPREAD road" (`:105`) while `Road = Branch.Loot`. |
| **Rarity** | `Rarity` (`LootSystem.cs:9`), `Gear.RarityPower` 0.2/0.5/1.2/3/7 (`Gear.cs:83-91`) | `LootSystem.RollRarity` (`:354-368`); chest floor `Elevate` (`Chest.cs:397-400`); merge `rarity+1` (`Forge.cs:174`) | Weapon mult, charm def/hp/toughness, focus attunement, trait `up`, affix count + `rarityFactor`, enchant gate (Rare+) + magnitude, socket count, sell value, salvage tier | THE ladder. `RarityPower` is read by 6 files (grep count) | Rarity is also the gem's "frame grade" (`GemCraft.cs:136`) — a presentation meaning on a gameplay field. |
| **Item level** | `ItemInstance.ItemLevel` (`:53`); three curves: `Gear.ItemLevelFactor` →2.0 (`Gear.cs:122-129`), `ItemAffixes.IlvlFactor` →2.8 (`ItemAffixes.cs:135-142`), family/gem linear→4× (`ItemFamilies.cs:81`, `GemCraft.cs:58`) | = loot tier / chest tier (`LootSystem.cs:309`, `Chest.cs:384`); `Forge.Refine` +1 (`Forge.cs:317`), `GreaterRefine` +5 (`:352`); merge takes max (`:215`) | all rarity-direct helpers, `GearTraits.ModsFor`, `ItemAffixes.Of`, `ItemFamilies.BonusOf`, `GemCraft.Magnitude` | Refines WITHIN a rarity tier | **Three different saturation shapes** (§2.6). `IlvlFactor`'s own comment (asymptote 4, iL20≈1.80) does not match the code (asymptote 2.8, iL20 = 1.55). |
| **Upgrades** (refine rung) | `ItemInstance.Upgrades` 0..15 (`:60`) | `Forge.Refine/TryRefine/GreaterRefine` | `Forge.RefineFailChance` (`:293-300`), `AtRefineCap`, UI bar (`ForgeScreen.cs:1670`) | Ladder: 5 safe rungs then +6%/rung slip to 60% cap | Fight never reads it; `ItemLevel` is what pays. Fine. |
| **Prefix (= GearTrait)** | `GearTrait` ×10 (`GearTraits.cs:8`); stored in `TraitOverride` (`LootSystem.cs:104`) | `GearTraits.RollPrefix` 55% (`:155-171`) at 3 mint sites + merge; trader hash-picks always; gifts never | `GearTraits.ModsOf` → `Hunter.WornMods` (`HunterProgression.cs:333`) → `SquadDamageMultiplier/SquadHealthMultiplier/SquadSkillRate/HaulMultiplier` → `Build.Resolve` → `SoloBattle` | Trade on 4 channels (§3) | Immutable since the redesign; `LegacyDerivedTrait` is migration-only (`:139-145`). |
| **Affixes** | `AffixStat` ×6 (`ItemAffixes.cs:8`); count by rarity 0/1/2/3/4 (`:87-95`) | Derived from id salted `|affix{i}` (`:152-174`), uniform over the six stats, variance 0.75–1.25 | `Hunter.AffixTotals` (`:341-362`): Damage/Health/Haul/SkillRate → `WornMods` as ONE stack entry (`:326-333`); Crit → `SoloBattle.CritChance` (`:2003`); Defense → `Hunter.Defense` (`:224`) | Percent bonuses on the four channels, +crit points, +flat defence | No slot weighting: a weapon can roll Defense, a helm Haul. |
| **Enchantment** | `EnchantKind` ×15 (`Enchantments.cs:25-92`), pools of 5 per slot group (`:219-245`), Rare+ only (`:204`) | Derived from id salted `|ench` (`:247-263`), or `EnchantOverride` from Reforge | `Build.Triggers` maps 11 kinds → `BuildTrigger` (`Build.cs:585-610`); 4 magnitude-only kinds read via `Enchantments.MagnitudeOf` (`SoloBattle.cs:493,501,536-539`, `SoloExpedition.cs:482`) | "What happens", not "how much" (§4) | **6 of 15 need a Form**; 5 of 8 slots draw only from those 6. |
| **Element (Source)** | `ItemInstance.Element : Source?` (`LootSystem.cs:70`) | = the **region's** Source (`LootSystem.cs:315` from `KillContext.Element` ← `Chest.Element` ← `def.Theme`, `Game1.cs:3753`); trader `hash % 6`; merge: shared element carries, mix → null (`MergeRecipe.cs:302-308`) | `ElementSets.WornCounts` (`ElementSets.cs:108-116`) → `ShapeFor` → `GearShape.Of` (`GearShape.cs:24`) → fight; naming (`ItemNaming.cs:85`) | Set membership only. Does NOT enter the matchup (that is the skill's Source) | `ItemInstance` remark `:29-35` still says the element is "a CRAFTING axis, not a combat one" — stale since 2026-08-27 (sets). |
| **Set** | `ElementSets.Catalogue` 6 × 4 rungs (`ElementSets.cs:74-105`) | n/a (derived from worn elements) | every `SkillShape` field it sets is read in `SoloBattle` (§5 table) | 2/4: own-Source skills +8%; 3: a stat; 5: a rule | Rides the same `SkillShape` fields as mastery nodes — deliberate reuse, but each field now has two authors. |
| **Gem** | `ItemBaseType.Gem`, `GemCraft` (`GemCraft.cs`); sockets 0/0/1/2/3 (`:37-43`) | `GemCraft.MintGem` in 30% of chests (`Chest.cs:354-356`), trader slot 3; stat from id `|gem` (`:46-51`) | `Hunter.AffixTotals` (`:357-360`) — same ruler as affixes | +stat scaled `0.7+0.08×(L−1)` cap 4× | Player-CHOOSEABLE flat defence (WARD gem) is the one unbounded knob (§2.5). |

**Overlaps (two layers adding the same thing):**
- Damage%: trait (Keen/Heavy/Savage/Wild) + Damage affix + Blade family + Gloves family + FURY gem — all fold into one `WornMods.Damage` stack entry set, so they share the +300% ceiling. Good. But the **weapon's rarity-direct multiplier** (`Gear.cs:132-133`) multiplies the stack from outside it.
- Health: Charm flat (`CharmHealthBonus`) + Charm multiplier (`CharmToughness`) + Health affix + Chest/Charm family + Vital/Warding traits + LIFE gem + Body 3-pc `MaxHealth 1.10` — the first two are the same rarity read twice.
- Skill rate: Focus rarity-direct (`FocusAttunement`) + Focused/Attuned/Swift traits (the pool for 5 slots) + SkillRate affix + Boots/Focus family + TEMPO gem + Spirit 3-pc `SkillRate 1.06`.
- "Finish the weak": EXECUTE enchant ×1.6 under 30% (`SoloBattle.cs:361-364, 1613`) + HAMMER's `ExecuteFraction` outright kill (`:1664`) + Shadow 3-pc `Cull +12% under 30%` (`ElementSets.cs:98`, `:830`) + mastery `AssassinateThreshold` (`:1621`). Four systems, one idea.
- Mark window: LINGER ×9/5 (`:1382, 1507`) × `shape.MarkWindowMultiplier` (Mind 5-pc ×1.5 **and** MARK MASTERY node) — compounding to ×2.7 with no ceiling.

**Layers with no consumer:** none of the shipped layers is dead. The dead things are fields/knobs, listed in §9.

---

## 2. Stacking audit

### 2.1 The folds, in order

| # | Fold | Where | Kind | Ceiling |
|---|---|---|---|---|
| 1 | `Gear.ItemLevelFactor(iL) = 1 + (iL−1)/((iL−1)+25.3)` | `Gear.cs:122-129` | saturating | → 2.0 (1.70 at iL60) |
| 2 | `Gear.WeaponDamageMultiplier = 1 + RP × ILF` | `Gear.cs:132-133` | one multiplicative factor | Legendary → 15×; Rare → 3.4× (< Epic floor 4.0, pinned by `test_rarity_still_beats_a_deeply_refined_lesser_item`) |
| 3 | `GearTraits.ModsFor` — `up = 1 + 0.05×RP×ILF`; flat identity + `k×t` | `GearTraits.cs:267-295` | per-item multiplier struct | Legendary HEAVY iL∞: dmg 1.925, rate 0.80 |
| 4 | `ItemAffixes.Of` — `Base × IlvlFactor × (0.6+0.1×RP) × variance` | `ItemAffixes.cs:152-174` | per-affix magnitude | Damage affix max ≈ 0.04×2.8×1.30×1.25 = **+18.2%**; ×4 on a Legendary = +73% |
| 5 | `ItemFamilies.BonusOf` — `Base × min(1+0.06(iL−1), 4)` (weapons), half that elsewhere | `ItemFamilies.cs:75-102` | additive into totals | Blade +16% dmg at cap |
| 6 | `GemCraft.Magnitude` — `Base × min(0.7+0.08(L−1), 4)` | `GemCraft.cs:54-59` | additive into totals | FURY +16% per gem, 3 per Legendary |
| 7 | `Hunter.AffixTotals` = Σ affixes + Σ family + Σ gems, per stat | `HunterProgression.cs:341-362` | **additive** | none (this is fine because…) |
| 8 | `Hunter.WornMods = GearMods.Stack(8 trait mods ++ affixMods)` | `:318-335`, `GearTraits.cs:40-66` | **additive then saturating**: `1 + up/(1+up/C) − down/(1+down/0.75)`, floor 0.25 | C = dmg 3.0 / hp 2.0 / haul 2.0 / rate 1.5 → channel < 4.0 / 3.0 / 3.0 / 2.5 |
| 9 | `Hunter.SquadDamageMultiplier = WeaponMult × WornMods.Damage` | `:376` | multiplicative (2 factors) | ≤ 15 × 4 = 60× theoretical; ≈ 12.9 × 3.4 ≈ 44× at iL60 full Legendary |
| 10 | `Hunter.SquadHealthMultiplier = CharmToughness × WornMods.Health` | `:400-416` | multiplicative | ≤ 1.84 × 3.0 |
| 11 | `Hunter.SquadSkillRate = FocusAttunement × WornMods.SkillRate × (1+0.006×TEMPO)` | `:434-437` | multiplicative (3 factors) | ≤ 1.7 × 2.5 × 1.78 ≈ 7.6× |
| 12 | `Hunter.HaulMultiplier = (1+0.01×GUILE) × WornMods.Haul` | `:440` | multiplicative | ≤ 2.3 × 3.0 |
| 13 | `Hunter.MaxHealth = trained + CharmHealthBonus`; `Hunter.Defense = trained + CharmDefenseBonus + Σ Defense affix` | `:223-225` | **additive, unbounded** (defence) | Defence: no cap. Charm flat hp ≤ 350 |
| 14 | `Build.Resolve = keystones ⊗ (Squad* multipliers) ⊗ PassiveMods` | `Build.cs:512-530` | multiplicative (`BuildMods.Combine`, `:38-40`) | none |
| 15 | `SoloBattle.Amp` | `SoloBattle.cs:703-850` | **long multiplicative chain**: `mods.Damage × affinity(0.45–2.0) × matchup(0.87/1.15) × (1+SourceBonus) × (1+OneSource) × Bloodlust × Zeal × (1+Reverb) × (1+Tithe·vows) × Mark(1.6+) × HitSize × DamageDealt × FormPower × Hoarder × Anchor × Bastion × First/Later × Cull × Fresh × Siege × Swarmbane …` then `× critFactor` | none per se; each factor individually bounded |
| 16 | `SoloBattle.ChampionHealth = max(50?, Hunter.MaxHealth) × VowHealthMultiplier(× Shape.MaxHealth) × GearShape.MaxHealth × Resolve.Health × poolScale` | `SoloBattle.cs:2052-2059` | multiplicative (5 factors) | none |
| 17 | `SoloBattle.CritChance = clamp((trained + Σ Crit affix + shape.BonusCritPercent)/100, 0, 0.75)`; `CritMultiplier = 1.5 + 0.01×FOCUS` | `:2000-2013` | additive, **clamped** | 0.75; mult ≤ 2.8 → critFactor ≤ 2.35 |
| 18 | Defence mitigation `100/(100+D)` | `:552, 283` | hyperbolic | never zero, never capped |
| 19 | `ElementSets.ShapeFor` via `SkillShape.Combine` | `ElementSets.cs:131-136`, `SkillShape.cs:455-565` | mixed: `SourceBonus` additive (0.08+0.08), `MaxHealth/SkillRate/FirstCast/AutoAttack/MarkWindow` multiplicative, `Leech/Regen/Cull/Flat/Refund` additive | none |
| 20 | `Enchantments.MagnitudeOf` = Σ over worn of one kind | `Enchantments.cs:305-312` | additive | per-kind caps in `MagnitudeFor` (`:268-295`); Fervour/Bulwark ≤ 0.5, Reverb ≤ 0.3, Tithe ≤ 0.1/vow |
| 21 | Haul per wave | `SoloExpedition.cs:413-474` | multiplicative chain of `(1+node)` factors | none |

### 2.2 Is it additive-then-saturating or multiplicative?

**Both, at different altitudes.** Inside a channel (trait + affix + family + gem across all eight slots) it is additive with a saturating ceiling — that is the 2026-08-23 fix and it is the right shape. **Across** layers it is a product of independently bounded factors: weapon-direct × stack × family-Form × set × crit on damage; focus-direct × stack × TEMPO × keystones × passives × shape on skill rate; flat+mult charm × stack × keystones × passives × vow × set × poolScale on health. Each factor is bounded, so there is no *runaway*, but the product of 5–6 bounded factors is large: gear alone can put ≈ 44–60× on every skill hit and ≈ 7× on cadence, with the tree, keystones, Vows and affinity multiplying on top. This is the "multiplicative soup" the brief warns about — not infinite, but wide, and the ceilings are per-factor, not per-outcome.

### 2.3 Where rarity × affix × trait × set × enchant multiply "freely"

- `Gear.WeaponDamageMultiplier × WornMods.Damage` (`HunterProgression.cs:376`): the weapon's rarity is counted in the direct multiplier **and** inside the stack (its trait `up`, its affix count and `rarityFactor`, its family). The stack is capped; the direct term is not part of the stack. Bounded product ≤ 60×.
- `FocusAttunement × WornMods.SkillRate × TEMPO` (`:434-437`) then `× fromKeystones.SkillRate × PassiveMods.SkillRate × shape.SkillRate` (`SoloBattle.cs:530, 643, 664`): six factors, one ceiling. `power_scale_test.test_eight_focused_slots_cannot_run_the_skill_clock_away` (`power_scale_test.cs:65-68`) pins only `WornMods.SkillRate < 2.5`, i.e. factor 2 of 6.
- Charm rarity: `CharmDefenseBonus` (flat def), `CharmHealthBonus` (flat hp), `CharmToughness` (hp mult) — one item's rarity read three times outside the stack, plus its trait/affix/family/gem/enchant inside it. **Recommend collapsing to one**: either the flat or the multiplier.
- `LINGER × MarkWindowMultiplier` (Mind 5-pc × MARK MASTERY node) — three window multipliers compound (`SoloBattle.cs:1382, 1507-1508`).
- Enchant magnitudes are summed across slots (`Enchantments.cs:311`) and added to a keystone's own slope (`BloodlustScale + fervour`, `:749`) — additive into a multiplicative term; bounded by `MagnitudeFor` caps. OK.

### 2.4 Caps / ceilings that exist

`GearMods` channel ceilings (`GearTraits.cs:64-66`); `ItemLevelFactor` asymptote 2 (`Gear.cs:128`); `IlvlFactor` asymptote 2.8 (`ItemAffixes.cs:140-141`); family/gem `MathF.Min(…, 4f)` (`ItemFamilies.cs:81`, `GemCraft.cs:58`); crit chance 0.75 (`SoloBattle.cs:2004`, mirrored `HunterProgression.cs:262`); `MagnitudeFor` per-kind caps; `Math.Max(0.25f, …)` drawback floor; `MathF.Max(0.05f, skillRate)` and `MathF.Max(0.1f, …)` divisor floors; `StatRankCap 60`; refine `MaxUpgrades 15`. Rarity ladder ordering pinned by `progression_curve_test` (`:24-`).

### 2.5 Runaway candidates that remain

1. **Defence** (`Hunter.Defense`, `HunterProgression.cs:224`) is an unbounded additive sum. Per Defense affix at cap: `8 × 2.8 × 1.30 × 1.25 ≈ 36`; a WARD gem at cap: `8 × 4 = 32`, three per Legendary, **player-chosen**. Eight Legendaries socketed with WARD gems alone add 768 flat; with trained 120 and charm 168 → `100/(100+1056) ≈ 8.6%` damage taken (≈ 11.6× effective HP) before any HEALTH multiplier. `PowerRating` includes it as `√(… × (1+def/100))` so EQUIP BEST will chase it. No test bounds total defence.
2. **Skill rate** across six multiplicative factors (2.3). Quantised by `BeatFor` but the floor is `0.1f`.
3. **Mark window** compounding (2.3).
4. **`IlvlFactor` comment vs code** — if someone "fixes" the code to match the comment (asymptote 4), affix magnitudes rise ~43%.

### 2.6 Three item-level curves that do not agree

| Curve | Formula | iL20 | iL60 | ∞ | Says it is aligned with |
|---|---|---|---|---|---|
| `Gear.ItemLevelFactor` | `1 + x/(x+25.3)`, x = iL−1 | 1.43 | 1.70 | 2.0 | — |
| `ItemAffixes.IlvlFactor` | `1 + 1.8·iL/(iL+45)` | **1.55** (comment: 1.80) | **2.03** | **2.8** (comment: "toward 4") | "mirrors Gear.ItemLevelFactor" (`:129`) |
| `ItemFamilies.BonusOf` / `GemCraft.Magnitude` | linear `1+0.06(iL−1)` / `0.7+0.08(L−1)`, `min(…,4)` | 2.14 / 2.22 | 4.0 (cap, from iL51) / 4.0 (from L42) | 4.0 | "aligned with ItemAffixes.IlvlFactor's own saturation" (`ItemFamilies.cs:78-79`, `GemCraft.cs:57`) |

Family and gem cap at 4×; affixes asymptote at 2.8×; weapon/charm/focus/trait at 2×. The comments claim alignment that the numbers do not have. Not a bug in isolation, but a REFINE buys different amounts of different layers on the same item, and the family/gem linear ramp out-grows the others until iL51.

### 2.7 Loot rarity odds (Formula 2)

`RarityWeights` (`LootSystem.cs:384-404`): weight_i = base_i × `(1 + i·0.15·(tier−1)) × (1 + i·0.6·tilt/100)`, base = 700/250/45/4.5/0.5 (`:155`), Common (i=0) never tilted. Tilt = clamp((quality−1)×80, 0, 40) + clamp((buildTilt−1)×80, 0, 40) + region tilt 0–20 (`ExpeditionLoot.cs:216-219`, `RegionDrops.cs:75-100`). Worked example, tier 60, tilt 100: weights ≈ 700 / 3950 / 1850 / 348 / 62 → Common 10%, Uncommon 57%, Rare 27%, Epic 5%, Legendary 0.9%; then `Chests.Open` lifts to the grade floor (`ItemFloorByGrade` `Chest.cs:136-137`). Tier 1, no tilt: 70 / 25 / 4.5 / 0.45 / 0.05%.

---

## 3. GearTraits — trade or upgrade?

`GearTraits.ModsFor` (`GearTraits.cs:267-295`), `t = 0.05 × RP × ILF` (Common iL1: 0.01; Legendary iL60: 0.595; Legendary iL∞: 0.70).

| Trait | Pool(s) | Up | Down | Real trade? |
|---|---|---|---|---|
| Keen | Weapon | dmg `1+0.5t` | — | **pure upgrade** (deliberately weak, `:280-281`) |
| Vital | Charm | hp `1+0.5t` | — | pure upgrade |
| Attuned | Focus/armour | rate `1+0.5t` | — | pure upgrade |
| Heavy | Weapon | dmg `1.12+1.15t` | rate ×0.80 | trade |
| Swift | Weapon, Focus/armour | rate `1.10+1.10t` | dmg ×0.90 | trade |
| Savage | Weapon | dmg `1.10+1.10t` | hp ×0.85 | trade |
| Warding | Charm | hp `1.12+1.15t` | dmg ×0.90 | trade |
| Greedy | Charm, Focus/armour | haul `1.15+1.20t` | hp ×0.85 | trade |
| Focused | Focus/armour | rate `1.15+1.20t` | dmg ×0.85 | trade |
| Wild | Charm (default arm) | dmg & rate `1.08+1.10t` | hp ×0.70 | trade (glass cannon) |

Three of ten are upgrades with no cost; the class doc says so on purpose (`:280`) and `test_no_drawback_traits_are_the_weak_ones` pins it. Because `PoolFor` sends **Helm, Chest, Gloves, Boots and Ring to the Focus pool** (`:99-104`, `_ => FocusPool`), five slots can only roll {Attuned, Focused, Swift, Greedy}: three of four push skill rate. That is why `power_scale_test` had to guard the skill clock specifically, and why the trait layer on armour reads as "skill rate or loot" and never "health" or "defence". Upside scales with rarity+level, downside is flat (`:250-252`) — so at Legendary the trade is thin (Focused Legendary iL60: rate ×1.86 for dmg ×0.85). `GearMods.Stack` saturates drawbacks too (`:56-61`), so a sixth trade piece costs less than its first.

Consumer chain: `GearTraits.ModsOf` → `Hunter.WornMods` (`HunterProgression.cs:333`) → `Squad*` (`:376, 400, 434, 440`) → `Build.Resolve` (`Build.cs:521-526`) → `SoloBattle` (`mods.Damage` `:705`, `mods.SkillRate` `:530/643/664`, `Resolve.Health` `:2055`) and `SoloExpedition.HaulForWave` (`:418`). Player text: `EffectOf` (`:192-212`) prints the per-item numbers — good.

---

## 4. Enchantments

Pools (`Enchantments.cs:219-245`): Weapon `{Splinter, Venom, Harvest, Fervour, Reverb}`; Charm+Ring `{Undying, Desperation, Siphon, Bulwark, Tithe}`; Focus+Helm+Chest+Gloves+Boots `{Linger, Radiance, Overdraw, Execute, Coiled}`. Rare+ only (`:204, 249`). Magnitude from rarity only (`:268-295`); a reforge stores only the KIND (`LootSystem.cs:115`).

| Kind | Need (`Needs`, `:173-186`) | Trigger route | Fight consumer | Notes |
|---|---|---|---|---|
| Splinter | — | `BuildTrigger.Splinter` | `SoloBattle.cs:697` `bonus.AddQuality(0.15)` → chest `RunTilt` (`Game1.cs:3753`) | magnitude (`:276`) is **not read** — flat 0.15 quality regardless of rarity; `MagnitudeFor` computes a chance nobody uses. |
| Harvest | — | trigger + `MagnitudeOf` | `:500-501, 692` → `bonus.AddCores(1)` → `Material.Core` (`Game1.cs:3646-3648`) | "spare core" now pays the **Core material**, not a `CreatureCore` item. Name survives, meaning changed. |
| Venom | — | trigger + `MagnitudeOf` | `:491-493` poison fraction | live |
| Desperation | — | trigger + `MagnitudeOf` | `SoloExpedition.cs:482` haul swell | live |
| Undying | — | trigger | `SoloBattle.cs:1954` | live |
| **Overdraw** | `Form.Projectile` | trigger | `:1551` `form == Form.Projectile → casts += 1` | → **VOLLEY**; dial: an extra cast (`casts`) — a candidate `SkillDef` dial (e.g. `ExtraCasts`) rather than a Form test |
| **Linger** | `Form.Mark` | trigger | `:1382` standing window ×9/5; `:1507` cast window ×9/5 | → **SIGN**; gate is `FormBehaviour.IsAmplifier(form)`; dial: the amplify window (`FormBehaviour.MarkWindowMs`, `auraTick`) — a `SkillDef` "AmplifyMs" would absorb it |
| **Radiance** | `Form.Aura` | trigger | `:1261` `auraTick × 3/5` for `SkillKind.Field` | → **FIELD**; dial: `SkillDef.IntervalMs` — already the field's own clock (`:1260`) |
| **Execute** | `Form.Strike` | trigger | `:1613` `form == Form.Strike` → ×1.6 under 30% | → **HAMMER**; `SkillDef.ExecuteFraction/ExecutesPerWave` already exist (`:1664`) — the enchant duplicates FINISH's dial with a multiplier instead of a kill |
| **Coiled** | `Form.Trap` | trigger | `:1909` `trapBase × 0.5` in the Reaction/Bitten loop | → **SNARE**; `SkillDef.CooldownMultiplier` already exists (`:1908`) |
| **Siphon** | `Form.Transformation` | trigger | `:639` heal budget ceiling ×1.5; `:1438, 1730` leech × `SiphonMultiplier` 2.0 (`HealTuning.cs:86-93`) | → **DRAIN**; gate is `FormBehaviour.Heals(form)`; dial: `HealTuning.TransformationLeech` |
| Fervour | `BuildTrigger.Bloodlust` | magnitude only (`Build.cs:608-609` → null) | `:536, 749` | live; dead without keystone by design |
| Reverb | `BuildTrigger.Echo` | magnitude only | `:538, 765` | live |
| Bulwark | `BuildTrigger.Zeal` | magnitude only | `:537, 760` | live |
| Tithe | any Vow | magnitude only | `:539, 544, 769` | live |

**Form-dependence today is nominal.** `EquippedSkill.Form => Ability.Form` (`Build.cs:223`) and the loadout writes `Form = def.LegacyForm ?? (sibling).LegacyForm` (`PlayerLoadout.cs:194-197`), and `SkillCatalogue.Resolve` goes `Form → Style` (`:653`). So Form ≡ Style one-to-one: Strike↔Hammer, Projectile↔Volley, Trap↔Snare, Mark↔Sign, Aura↔Field, Transformation↔Drain. The six needs map cleanly onto Styles as the brief hypothesised — **except** that two consumers gate on the skill's *behaviour*, not its Form: LINGER fires on `IsAmplifier(form)` (any amplify), SIPHON on `Heals(form)`, RADIANCE on `SkillKind.Field`. The honest post-Form keys are therefore `Style` for Overdraw/Execute/Coiled and `SkillEffect/SkillKind` for Linger/Radiance/Siphon — or, better, each becomes a `Func<SkillDef,SkillDef>` delta on an existing dial (`IntervalMs`, `CooldownMultiplier`, `ExecuteFraction`) applied to skills of the matching Style, which is exactly how variations/reinforcements already work.

**Labelling:** yes — the Forge still prints `"NEEDS {need.Label} IN YOUR BUILD — UNTIL THEN IT DOES NOTHING."` (`ForgeScreen.cs:1728-1733`) with labels `PROJECTILE / MARK / AURA / STRIKE / TRAP / TRANSFORMATION` (`Enchantments.cs:175-180`) — six Form names the current game never otherwise shows the player (the skills are BLOW/PRESS/SPRAY/…, the styles HAMMER/VOLLEY/…). `WeaveScreen.GearWants` (`:1357-1366`) also speaks Form. `EnchantNeed.MetBy` gets `ActiveForms = _loadout.Skills.Select(s => s.Form)` (`Game1.cs:2878`).

**Pool-size folklore.** The comment at `Enchantments.cs:212-218` says pools must be coprime to 4 because trait and enchant were both `hash(id)`. Since the prefix redesign the trait is **rolled by RNG at mint and stored** (`GearTraits.cs:164-171`, `LootSystem.cs:298`), not hashed — only `LegacyDerivedTrait` (migration) still hashes. The correlation the rule guards against cannot occur for any item minted since; the constraint is obsolete. No test asserts pool size or parity (`EnchantmentsTests.cs:56-74` asserts only `pairs.Count > 8`, and its own comment "4 traits x 3 weapon enchants" is stale — the pool is 5). The memory note "pools must stay size 3" does not match the code (5) or any guard.

Stale strings: `Enchantments.Worn` doc "across all three slots" (`:297`); `Reforge.cs:91` rejection `"ONLY WEAPONS, CHARMS AND FOCUSES HAVE AN ENCHANTMENT."` for a non-wearable (all 8 slots carry one); `Hunter.WornMods` remark "the three worn items" (`HunterProgression.cs:294`).

---

## 5. ElementSets

`ElementSets.cs:74-105`; all 2- and 4-piece rungs are the generic `SourceBonus[element] += 0.08` (`Own`, `:56-59`).

| Source | 3-piece | Field | Consumer | 5-piece | Field | Consumer |
|---|---|---|---|---|---|---|
| Body | +10% max health | `MaxHealth 1.10` | `SoloBattle.cs:2054` `GearShape.Of(hunter).MaxHealth` | basic attack +25% | `AutoAttackDamage 1.25` | `:1763` |
| Machine | every bite −4 | `FlatDamageReduction 4` | `:1840` | first bite of a wave free | `FirstBiteFree` | `:1844` |
| Mind | +6% crit | `BonusCritPercent 6` | `:2004, 2142` | mark window ×1.5 | `MarkWindowMultiplier 1.5` | `:1381, 1508` |
| Nature | regen 0.3%/s | `RegenFraction 0.003` | `:1218-1219` | heal 2% of damage | `Leech 0.02` | `:1157-1158` |
| Shadow | +12% under 30% | `CullThreshold 0.30, CullBonus 0.12` | `:830-831` | kill refunds a beat | `CooldownRefundOnKillMs = DefaultBeatMs` | `:1061-1068` |
| Spirit | act 6% faster | `SkillRate 1.06` | `:530, 643, 664` | first cast +25% | `FirstCastMultiplier 1.25` | `:1578, 1929` |
| all | own-Source skills +8% / +16% | `SourceBonus` | `:737-738` | | | |

Every rung has a live consumer (verified by grep above and by `element_sets_test.cs:138-283`, which drives the real `SoloBattle`). None is dead.

**How an item gets its Element:** from the region it dropped in — `KillContext.Element` (`LootSystem.cs:187, 315`) ← `Chest.Element` (`Chest.cs:36`, set from `def.Theme` at `Game1.cs:3753`) ← `RegionDef.Theme`. Materials/cores are null. Trader: `hash % 6` (`WanderingTrader.cs:90`). Merge: shared element or null (`MergeRecipe.cs:302-308`). Gift: chest element (`GiftChests.cs:153`).

**Variation Source vs item Element:** `SourceBonus` is read against `skillSource`, which is `sk.Source` = `EquippedSkill.Source => Variation?.Source ?? Ability.Source` (`Build.cs:243`; call sites `SoloBattle.cs:1424, 1682, 1943`). So **yes**: the set bonus applies through the VARIATION's Source today. A Spirit set makes the Spirit-variation of any skill hit harder, whatever region the item came from is irrelevant beyond having put the element on the item. The two "Source" meanings (item element = where it dropped; skill Source = the variation taken) never touch except at this one lookup — which is coherent, but the `ItemInstance` remark `:29-35` claiming the element is "not a combat" axis is stale.

Note: `ElementSets` reuses `SkillShape` fields that mastery nodes also set (`MarkWindowMultiplier`, `Leech`, `Cull*`, `BonusCritPercent`, `SkillRate`, `MaxHealth`). This is the correct reuse (one consumer per field), but it means every one of those fields now has two content authors and `SkillShape.Combine`'s per-field additive/multiplicative choice decides how set and tree stack (`SkillShape.cs:455-565`).

---

## 6. ItemClasses / Families

- Class-locked slots: Weapon, Helm, Chest, Gloves, Boots (`ItemClasses.cs:156-157`). Universal: Charm, Ring, Focus. Null class on a locked slot = legacy = anyone (`:174-185`).
- Families per class: Warden {Blade, Spear}, Ranger {Bow, Spear}, Mystic {Scythe, Bow}, Bulwark {Blade, Scythe}, Wanderer {all four} (`:97-134`). Two champions per class (`CharacterRoster.cs:54-210`, guarded by `item_classes_test`).
- **Form keying:** `ItemFamilies.FavouredForms` (`:49-55`) → `GearShape.Of` → `FormPower` (`GearShape.cs:25-29`) → `SoloBattle.cs:811`. Post-Form this becomes "favoured Styles" (Blade→Hammer+Drain, Bow→Volley+Sign, Spear→Hammer+Snare, Scythe→Field+Drain) on a `StylePower` dictionary, and shares that dictionary with `Character.AptitudeShape` (`Character.cs:207-209`) — three users of one Form-keyed map.
- **Old branch names:** `ItemClass.Ranger.Description = "Many targets. Built for the SPREAD road."` (`:105`) while `Road = Branch.Loot` — SPREAD was renamed LOOT in `f4052b8`. Warden was updated (`:93-98`); Ranger was not. `WEIGHT` survives only in comments (`:93-96`, `MasteryCatalog.cs:20`).
- Family index is a bare int (`Blade = 0 …` `:87`) duplicated against `ItemNaming.WeaponFamilies` string order (`ItemNaming.cs:22`) and `ItemFamilies.WeaponChannel` / `FavouredForms` array order — three arrays coupled by position.

---

## 7. Forge / Reforge / Merge / Gem

Verbs that exist in Core and what the Forge screen exposes (`ForgeScreen.cs:19, 266-269`: tabs UPGRADE / RE-ROLL / SOCKET / SALVAGE; bag foot: MERGE THREES INTO BETTER, SALVAGE ALL THE JUNK `:1435-1438`):

| Verb | Core | Cost | UI | Status |
|---|---|---|---|---|
| Refine (+1 iL) | `Forge.Refine/TryRefine` (`Forge.cs:310-340`) | Scrap `(4+iL)×(4+rung)/4` + Gleam `(15+8·iL)×(4+rung)/4`; slip 0% ×5 then 6%/rung → 60%; or a REFINE CHART | UPGRADE tab (`:1002-1024`) | live |
| Greater refine (+5, safe) | `Forge.GreaterRefine` (`:346-357`) | 1 Crystal + Gleam ×3 | (`:1032-1045`) | live |
| Re-roll enchant | `Reforge.ReforgeEnchant` (`Reforge.cs:84-104`) | Core 45/110 (Rare/Epic) or Crystal 260 (Legendary) (`:36, 126-127`); or REFORGE CHART (`PayWith`, `:155-161`) | RE-ROLL tab (`:966-980, 1739-1744`) | live; never returns the current kind |
| Re-roll trait | (removed) | — | — | gone on purpose (`Reforge.cs:28-30`) |
| Socket gem | `GemCraft.Socket` (`GemCraft.cs:143-155`) | Essence 60/120/240 by host rarity; first gem free (`:100-125`, `SaveGame.FreeSocketUsed`) | SOCKET tab (`:1861-1871`) | live |
| Crush gem | `GemCraft.Crush` (`:158-165`) | free; gem destroyed | (`:1899, 1944`) | live |
| Sell | `Hunter.Sell` (`HunterProgression.cs:489-499`) | pays `SellValue` | **screen bypasses Core**: `SellNow` does `hunter.AddGleam(item.SellValue)` (`ForgeScreen.cs:886`) | live, but `Hunter.Sell` has no runtime caller |
| Dismantle / salvage | `Forge.Dismantle` (`Forge.cs:239-246`) `floor(SellValue×0.4)` into `MaterialTiers.ForRarity` | SALVAGE CHART doubles junk salvage (`:845-853`) | SALVAGE tab + SALVAGE ALL THE JUNK | live |
| Merge 3→1 | `Forge.Merge` (`:143-221`) | free; same rarity, same class group | **AUTO-MERGE only** (`:786-816`); no manual tray | live, but see below |
| Merge with mixed rarity | `Forge.Merge(ignoreRarity: true)` (`:164-169`) | MERGE CHART | never called in `src/` (grep) | **dead** — `Charter.Merge` never drops (`WaveSpoils.cs:71`), held ones become Salvage charts on load (`Game1.cs:611-613`) |
| Feed to creature | `Forge.Feed` (`:266-273`) | `FeedConversionRate 1.0` | none | **dead** — no caller outside tests (`ForgeTests`, `SalvageTests`) |
| Preview hybrid | `Forge.Preview` (`:224-228`) | — | none in `src/` | dead (test only) |

`IneligibleReason` (`Forge.cs:13-18`): `CheckEligible` returns only `Eligible` or `EquippedToCreature` (`:97-103`); `VowBound` is never produced, only explained (`:110`). `ForgeOperation` (`:10`) has **zero** references anywhere but its own doc comment. `EquippedToCreatureId` is never assigned outside the save round-trip (grep: `SaveGame.cs:368, 567, 587` only), so the Universal Input Gate always passes.

**Auto-merge groups by rarity and class only** (`ForgeScreen.cs:795-801`), not by base type or element. `MergeRecipe.TypeOf` then resolves a majority or hybrid type (`MergeRecipe.cs:209-223`), and `ElementOf` returns **null for a mixed-element trio** (`:302-308`) — so the button can silently fuse three different-element, different-slot Rares into an elementless piece of a slot the player did not pick. The design doc for `MergeRecipe` ("predictable is the entire feature") assumed a manual tray. Of `HybridTable`'s ten recipes (`:241-253`), nine involve `Material` or `CreatureCore` **items**, which never exist in a live bag (§8) — unreachable. The generic `HybridPriority` rule (`:265-270`) is what actually runs.

**Gems:** `GemCraft.StatOf` from id (`:46-51`); `Magnitude` (`:54-59`); folded in `Hunter.AffixTotals` (`HunterProgression.cs:357-360`) — same ruler as affixes, so a gem is a player-chosen affix. Sockets 1/2/3 by host rarity. Supply: 30% of chests (`Chest.cs:124, 354-356`), trader slot 3. Sold/salvaged hosts return their gems (`ForgeScreen.cs:898`); auto-merge refuses gemmed hosts (`:795`).

**Currencies close:** Gleam (waves + sell → training, refine gold), Scrap (waves, salvage C/U, chests, filter compensation → refine), Essence (waves ≥8, salvage Rare → socket), Core (waves ≥25, salvage Epic, HARVEST → Rare/Epic re-roll), Crystal (waves ≥60, salvage Legendary → Legendary re-roll, greater refine, training reset `HunterProgression.cs:36, 467-475`), Charters Refine/Reforge/Salvage (waves ≥5 at 2.5%). `Material` enum (`Material.cs:124`) vs `ItemBaseType.Material` (`LootSystem.cs:13`) are two unrelated things with one name.

---

## 8. Loot generation

- **The only live entry points**: `Game1.cs:3753` `Chests.RollDrop(lootTier, def.Theme, …, runTilt, region)` on a felled boss (20% flat, `Chest.cs:175, 260-261`); `ForgeScreen.cs:1182` `Chests.Open(chest, _rng, …, rarityBonus: RarityBonus, favouredClass)`; `Game1.cs:3657` `WaveSpoils.Roll(wave, _rng)` per wave (materials + charters, never items); `WanderingTrader.Stock` (hash, deterministic by week); `GiftChests.Open` (catalogue, no RNG).
- **Chest open** (`Chest.cs:304-359`): materials `12 + rand(0..6) + tier/4`; items 1 (+1 at 35%); quality `1 + RP(grade)×0.14`; build tilt `rarityBonus × RunTilt`; floor by grade C/C/U/R/E; gems 30% on their own channel. Calls `ExpeditionLoot.RollBoss` (`ExpeditionLoot.cs:202-238`) → `LootSystem.Roll` (`LootSystem.cs:246-272`) → `Mint` (`:291-319`).
- **Determinism**: everything takes an explicit `Random`; ids are `itm_{rng.Next:x8}` (`:296`), affix/enchant/gem-stat derive from the id, so a seeded run reproduces a bag. Trader and gifts are hash/catalogue — same for every player. Draw order is documented and fixture-pinned (`:294-295`).
- **Wasted work**: `LootSystem.Roll` always mints a `CreatureCore` (`:255`, "non-tunable structural guarantee… Pillar 3") that `RollBoss` immediately drops (`ExpeditionLoot.cs:115`); 45% of the count roll is `ItemBaseType.Material` (`:263`) that `Chests.Open` drops (`Chest.cs:344`) and then re-mints gear if nothing survived (`:346-347`). `KillContext.IsBoss/AutomationStage/ActiveEfficiencyPercent` and `LootTuning.EfficiencyQuantityScalar/DropCountBaseBoss/AutomationRarityParityPercent/BonusRollAttempts` are never set by a live caller (grep: only `LootSystem.cs` and tests) — manual-combat-era knobs whose "Formula 1/7" comments describe a game that no longer runs.
- **Rare+ enchant pools**: three pools of five (`Enchantments.cs:219-237`); pool membership is tested (`EnchantmentsTests.cs:127-140`), size is not. See §4 for why the coprime rule is obsolete.

---

## 9. Dead fields, obsolete members, stale terms, duplication

### 9.1 Dead / test-only (with the grep)

| Member | Declared | Evidence |
|---|---|---|
| `ItemInstance.EquippedToCreatureId` | `LootSystem.cs:73` | `grep -rn EquippedToCreatureId src` → readers `Forge.cs:101`, `HunterProgression.cs:494`, round-trip `SaveGame.cs:368,567,587`; **no assignment** anywhere. Always null in every real save. |
| `IneligibleReason.VowBound` | `Forge.cs:17` | `grep -rn VowBound src` → only `Forge.cs:17,110`. Never returned. |
| `ForgeOperation` | `Forge.cs:10` | `grep -rn "ForgeOperation\." src` → only the doc comment `Forge.cs:253`. |
| `Forge.Feed`, `ForgeTuning.FeedConversionRate` | `Forge.cs:40, 266` | `grep -rn "\.Feed(" src` → none; tests only (`ForgeTests`, `SalvageTests`). |
| `Charter.Merge`, `Forge.Merge(ignoreRarity)`, `Charters.*` Merge text | `Charters.cs:37`, `Forge.cs:144` | `grep -rn "Charter\.Merge\|ignoreRarity: *true" src` → only `Game1.cs:613` (load-time conversion). `WaveSpoils.SpendablePool` excludes it (`:71`). |
| `Forge.Preview`, `MergeRecipe.HasRecipe`, `MergeEconomyIsClosed` | `Forge.cs:224, 367`, `MergeRecipe.cs:273` | no `src/` caller; tests only. |
| `MergeRecipe.HybridTable` (9 of 10 rows) | `MergeRecipe.cs:241-253` | rows need `Material`/`CreatureCore` items; `grep -rn "ItemBaseType\.CreatureCore\|ItemBaseType\.Material\b" src` outside LootSystem/MergeRecipe/Chest/naming → only dev fixtures (`Game1.cs:1497, 1923`) and label tables. |
| `Gear.ItemScore` | `Gear.cs:230` | `grep -rn "ItemScore(" src` → none (doc mentions only); one test use `progression_curve_test.cs:116`. |
| `Gear.WearableTypes` | `Gear.cs:53` | `grep -rn WearableTypes src` → only its declaration. |
| `Gear.ClassOf(item)` | `Gear.cs:66` | `grep -rn "Gear\.ClassOf" src` → none. |
| `KillContext.IsBoss/AutomationStage/ActiveEfficiencyPercent`, `LootTuning.EfficiencyQuantityScalar/DropCountBaseBoss/AutomationRarityParityPercent` | `LootSystem.cs:185, 201, 219, 147-152, 173-178` | `grep -rn "IsBoss\|AutomationStage\|ActiveEfficiencyPercent" src` outside LootSystem → only `EncounterTemplate.IsBoss` (different thing). Only tests set them (`LootSystemTests`). |
| `LootSystem.Roll`'s guaranteed core + `ItemBaseType.Material` items | `LootSystem.cs:255, 263` | filtered at `ExpeditionLoot.cs:115` and `Chest.cs:344`; no other `LootSystem.Roll` caller (`grep -rn "LootSystem\.Roll(" src`). |
| `Enchantments.MagnitudeFor(Splinter)` | `Enchantments.cs:276` | consumer `SoloBattle.cs:697` adds a flat `0.15f`; the rarity-scaled chance is computed and shown but not read. |
| `Hunter.Sell` | `HunterProgression.cs:489` | `grep -rn "\.Sell(" src` → only `ForgeScreen.cs` methods named `Sell` that call `AddGleam` directly (`:886`). |

### 9.2 Stale terms in this area

| Term | Where | Replacement |
|---|---|---|
| `Form` / PROJECTILE, MARK, AURA, STRIKE, TRAP, TRANSFORMATION as player labels | `Enchantments.cs:104-189`, `ItemFamilies.cs:33-64`, `GearShape.cs`, `ForgeScreen.cs:524, 1732`, `WeaveScreen.cs:1345-1366`, `Game1.cs:2878` | `Style` (HAMMER/VOLLEY/SNARE/SIGN/FIELD/DRAIN) or `SkillKind`/`SkillEffect` |
| "SPREAD road" | `ItemClasses.cs:105` | LOOT |
| "squad" (`SquadDamageMultiplier`, `SquadHealthMultiplier`, `SquadSkillRate`, `GearMods` doc "to the squad", tests `test_wearing_a_charm_makes_the_squad_tougher`) | `HunterProgression.cs:376-437`, `GearTraits.cs:11`, `equipment-design-language.md` ×4 | Champion / Build |
| "creature" (`EquippedToCreatureId`, "Feed to creature", `CreatureCore`, `Harvest` "spare core") | `LootSystem.cs:73`, `Forge.cs:248-273`, `Enchantments.cs:38` | remove / Core material |
| "three slots" | `Enchantments.cs:297`, `Reforge.cs:91`, `HunterProgression.cs:294`, `GearTests.cs:24` | eight |
| `ItemBaseType.Material` vs `Economy.Material` | `LootSystem.cs:13`, `Material.cs:124` | rename or delete the item type |
| `HunterStat.AttackPower/Engineering/ResonanceAffinity` shown as MIGHT/TEMPO/RESONANCE | `StatsScreen.cs:253-261` | rename enum members to what the player reads |
| `HunterStat.Focus` vs `GearSlot.Focus`/`AbilityFocus` | `HunterProgression.cs:11`, `Gear.cs:17` | disambiguate (crit-damage stat vs the slot) |
| "part-break", "loot table", "creature core", "enchantment_material", "Vow binding", "IPS" | `loot-drop-system.md` (19 part-break hits), `the-forge-system.md`, `item-data-schema.md` | supersede |
| `ResonanceHunter.*` namespaces | everywhere | `IdleXIdle.*` (global rename, not this area's call) |

### 9.3 Duplicated code / responsibilities

- `Fnv1a` private copy ×5: `GearTraits.cs:147`, `ItemAffixes.cs:179`, `Enchantments.cs:314`, `GemCraft.cs:167`, `ItemNaming.cs:91` (+ `WanderingTrader.Hash` variant).
- The wearable-type list ×5: `Gear.WearableTypes` (`Gear.cs:53`), `LootSystem.Wearables` (`:231`), `Chests.GearTypes` (`Chest.cs:361`), `WanderingTrader.Wearables` (`:41`), `MergeRecipe.HybridPriority` prefix (`:265`) — `LootSystem`'s comment says it is "spelled out so Loot keeps no dependency on Economy's Gear" (`:227`) while `LootSystem.Mint` already calls `Economy.GearTraits`, `ItemClasses` (`:298, 302`).
- Five mint sites re-implement the same shape (id, prefix, class-if-locked, family-if-classed-weapon, element-if-wearable): `LootSystem.Mint` (`:291-319`), `Chests.MintGear` (`Chest.cs:368-389`), `Forge.Merge` product (`Forge.cs:203-220`), `WanderingTrader.Stock` (`:72-95`), `GiftChests.Open` (`:145-159`). Five real users — an `ItemMint` factory is justified.
- `EnchantKind` ↔ `BuildTrigger` parallel enums with a name-for-name switch (`Build.cs:595-610`): 11 duplicates, 4 deliberate nulls.
- `Hunter.CritFactor` mirrors `SoloBattle` crit constants (`HunterProgression.cs:245-263`) — pinned by `test_the_crit_constants_match_the_fight`; still two implementations.
- `Gear.ItemScore` vs `Hunter.PowerContribution` — the former is the retired heuristic the latter replaced (`HunterProgression.cs:275-280`).
- Charm flat bonus vs Charm multiplier (§2.3).
- Set rungs vs mastery nodes writing the same `SkillShape` fields (§5).

### 9.4 Core ↔ presentation coupling

- `Enchantment.Blurb` is sized to a screen column: "Kept under ~21 characters — the Forge gives the blurb a fixed column from x=344" (`Enchantments.cs:140-143`).
- `ItemClasses.IconKey` returns an **asset key** (`icon_class_warden`) from Core (`:146`).
- Core owns all item copy: `GearTraits.Channels/BlurbOf/EffectOf`, `ItemAffixes.GrantLabel/StatWord`, `GemCraft.Describe`, `ItemClasses.ClassLine/WhyNot/WearsLine`, `ElementSets.Line/Progress`, `Charters.Plain` ("the verb the BUTTON says", `:74-89`), `ItemNaming`. Consistent (one source of truth), but Core is carrying UI strings and even pixel budgets.
- `Rarity` doubles as a gem's frame colour (`GemCraft.cs:136`).
- Economy transactions are executed in the Game layer: `ForgeScreen.SellNow/DismantleNow/AutoMerge` (`:795-816, 880-925`) mutate `_inv` and the hunter; Core is pure by design (`Forge.cs:281-282`), but the Sell path bypasses `Hunter.Sell`.
- The whole loot pipeline (drop → keep-filter → chest → open) is orchestrated in `Game1.cs:3745-3765` and `ForgeScreen.cs:1182`; `RarityBonus`/`FavouredClass` are pushed into the screen (`Game1.cs:3539, 3589`).

### 9.5 Serialization

`SavedItem` (`SaveGame.cs:355-408`) ↔ `ItemInstance` via `ToSavedItem/FromSavedItem` (`:559-596`), save version 2 (`:21-22`).

| Field | Risk | Migration |
|---|---|---|
| `TraitOverride` string; `null` → `LegacyDerivedTrait`, `"NONE"` → plain (`:588-590`) | Deleting `LegacyDerivedTrait` strips every pre-v2 item's prefix **and mods** on load (documented stealth nerf, `GearTraits.cs:133-137`) | keep as B-migration-only; one-shot rewrite to "NONE"/name on load would let it go later |
| `EnchantOverride` = `EnchantKind` name (`:591`) | Renaming/removing Form-combo kinds makes `Enum.TryParse` fail → silently falls back to the id-derived enchant; a paid re-roll vanishes | map old names → new kinds **before** parse; also `ShareCodes.cs:196` validates by `Enum.IsDefined` |
| `Element` = `Source` name | stable | — |
| `Class`/`Family` null = legacy | stable; share codes validate range (`ShareCodes.cs:200-201`) | — |
| `EquippedToCreatureId` | always null in real saves | drop from `ItemInstance`; keep tolerated on `SavedItem` or delete outright (nothing ever wrote a value) |
| `Gems` nested | `ShareCodes.cs:202` allows ≤ 8 vs `SocketCount` max 3 | tighten or leave lenient |
| `Upgrades` | `ShareCodes` allows ≤ 99 vs `MaxUpgrades 15` | lenient |
| `FreeSocketUsed` | inferred from any socketed gem for old saves (`:667-670`) | fine |
| `PlayerLoadout.SaveSkills` writes `Form` names (`PlayerLoadout.cs:278-291`) | out of this area, but deleting `Form` breaks `EnchantNeed.Form`, `FavouredForms`, `ActiveForms` **and** the loadout save in one move | bridge `Form`→`Style` in the loader first |

---

## 10. Design docs

- `design/gdd/item-data-schema.md` (612 lines): two-table Definition/Instance model, `base_type ∈ {weapon, armor, charm, enchantment_material, creature_core}`, `modifiers[]`/`enchantments[]` arrays with slot-count formulas, `vow_binding_log`, `equipped_to_creature_id`. Zero mentions of affix, prefix/trait, class, gem, set. Describes the schema the code does **not** have.
- `design/gdd/loot-drop-system.md` (975): `CreatureDefeated` trigger, loot tables, part-break buckets (19 hits), creature cores, pity, Formula 7 faucet model. The live faucet is `Chests.Open`; none of §3.2–3.6 exists.
- `design/gdd/the-forge-system.md` (1048): merge compatibility on `base_type`+`rarity`, Feed to Creature (59 "feed" hits), Vow binding (67 "Vow" hits), IPS formula, three disposal paths. Refine/re-roll/socket/salvage — the four tabs that ship — are absent.
- `design/art/equipment-design-language.md` (216): the one doc that describes the shipped item (10 traits, enchants, 6 affixes, rarity, per-slot silhouettes, pool table). Stale in detail: "11 enchants" (`:98`, now 15 — Fervour/Reverb/Bulwark/Tithe missing), "squad" ×4, six "Form-combo" enchants named by Form, no sets, no classes, no gems beyond a mention.
- `hunter-progression-system.md` §3.6 "Base vs. Gear" predates the eight-slot model.

Recommendation: mark the three GDDs **Superseded** and `/reverse-document` one `items-and-gear.md` from `Gear.cs / GearTraits.cs / ItemAffixes.cs / Enchantments.cs / ElementSets.cs / ItemClasses.cs / GemCraft.cs / Forge.cs / Chest.cs`, whose XML remarks already carry the rationale.

---

## 11. Things worth preserving

- `GearMods.Stack` additive-saturating channels with drawback saturation and the `0.25` floor (`GearTraits.cs:40-66`) and the tests that pin it (`GearTraitsTests.cs:179-198`, `power_scale_test.cs`).
- Rarity-first ladder with saturating item level (`Gear.cs:107-129`; `progression_curve_test`).
- Prefix rolled once at mint, immutable, `TraitOverride` field (`GearTraits.cs:106-127`).
- Id-derived affixes/enchant/gem stat — zero save weight, impossible to desync (`ItemAffixes.cs:27-32`).
- `EnchantNeed.MetBy` living in Core, unit-tested, one predicate for two screens (`Enchantments.cs:110-129`).
- `GearShape.Of` as the single seam from gear into `SkillShape` (`GearShape.cs`, consumed once at `SoloBattle.cs:448`).
- `Hunter.PowerContribution` defined as ΔPowerRating (`HunterProgression.cs:283-293`; `PowerContributionTests`).
- Forge/Reforge/GemCraft as pure functions with validate-then-spend charters (`Reforge.cs:129-161`, `HunterProgression.cs:132-144`).
- The liveness guards: `test_every_enchantment_is_claimed_by_a_test_that_proves_it_does_something` (`EnchantmentsTests.cs:150-182`), `element_sets_test` driving the real battle, `test_the_crit_constants_match_the_fight`.
- "Grade buys rarity, not quantity" chests with a rarity floor (`Chest.cs:93-137, 278-359`); deterministic merge output (`MergeRecipe.TypeOf`).
- One formatter for magnitudes and one word per stat (`ItemAffixes.GrantLabel/StatWord`).

## 12. Abstraction opportunities (each has ≥2 real users today)

1. `ItemMint.Create(type, rarity, iL, element, favouredClass, rng)` replacing the five hand-rolled mint blocks (§9.3).
2. One `ItemHash` (FNV-1a + salt) replacing five private copies.
3. One `Gear.WearableTypes` (already exists, unused) replacing five lists; `LootSystem` already depends on Economy so the stated reason for the copy is gone.
4. A Style-keyed `SkillNeed`/`StylePower` replacing `Form` in `EnchantNeed`, `ItemFamilies.FavouredForms`, `Character.Aptitude` and `GearShape` (three users of one map).
5. Fold `EnchantKind` into `BuildTrigger` (or a single table) — 11 of 15 kinds are the same enum member twice.
6. Collapse the six Form-combo enchants into `Func<SkillDef,SkillDef>` deltas on existing dials (`IntervalMs`, `CooldownMultiplier`, `ExecuteFraction`, an amplify-window dial, an extra-cast dial, a leech dial) applied to skills of the matching Style — the same mechanism variations and reinforcements already use, removing four `form ==`/`IsAmplifier`/`Heals` gates from the battle loop.

## 13. Over-engineering risks

- Three saturation curves for one concept (item level) with comments asserting an alignment the numbers lack (§2.6).
- `MergeRecipe.HybridTable` string-keyed (`"0|1|3"`) authored recipes for item types that never exist (§7).
- The coprime-pool numerology comment guarding a correlation that can no longer happen (§4).
- Four "finish the weak" rules and three Mark-window multipliers from four systems (§1 overlaps).
- `PowerRating`'s `√(dps) + √(ehp)` shape is fine for ranking but has grown two remarks' worth of exceptions (haul excluded, crit added late).
- `KillContext`/`LootTuning` still carry a manual-combat efficiency model (Formulas 1/2/7) that nothing feeds.
- Vow `SlotLeftBare` demands (`ResonanceWeaving.cs:90-105, 452-466`) couple gear slots into the Vow system via `WeaveContext.WornSlots` (`SoloBattle.cs:2120-2129`) — one more consumer of the slot list to keep in step.

## 14. Open questions for the owner

1. Post-Form, should the six combo enchants key on **Style** (Overdraw→Volley, Execute→Hammer, Coiled→Snare) or on **behaviour** (Linger→any Amplify effect, Radiance→any Field kind, Siphon→any Heal effect), which is what the battle gates actually test today?
2. Is the Charm's double health read (flat `CharmHealthBonus` + `CharmToughness` multiplier) intended, and should Weapon/Charm/Focus keep a rarity-direct bonus the five newer slots lack — or should all eight be trait+affix+family only?
3. Defence has no ceiling and WARD gems make it player-selectable — accept, cap the total, or exclude Defense from the gem stat pool?
4. Auto-merge groups by rarity+class only; mixed-type/mixed-element trios fuse into hybrid, elementless items. Intended, or should it group by type and element too (and delete `HybridTable`)?
5. `ItemAffixes.IlvlFactor`: comment (asymptote 4, iL20≈1.80) or code (2.8, 1.55) — which is the tuning intent?
6. Do `ItemBaseType.CreatureCore` and `ItemBaseType.Material` items have a future, or should `LootSystem.Roll` mint wearables only and the two filters go?
7. Should `Splinter`'s rarity-scaled magnitude be read (`SoloBattle.cs:697` adds flat 0.15) or should `MagnitudeFor(Splinter)` be removed so the blurb cannot lie?
8. `Hunter.Sell` vs `ForgeScreen.SellNow` — keep the Core method and route the screen through it, or delete it?
9. The enchant-pool "coprime to 4" rule is obsolete; is there any remaining reason pools must be odd, or can the four keystone/vow combos be spread so the five Form-combo-only slots get a non-combo option?
