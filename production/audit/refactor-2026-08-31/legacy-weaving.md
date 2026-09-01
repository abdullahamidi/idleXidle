# Audit — the legacy SOURCE × FORM weaving system

Area: `src/ResonanceHunter.Core/Weaving/ResonanceWeaving.cs`, `Builds/FormBehaviour.cs`, and every remaining
`Form` / `WovenAbility` / `Weaving.*` / `Aptitude` reference in Core, Game, tests, design docs and tools.
Read-only audit, 2026-08-31, branch `feat/hunter-cutout-rig` (clean). Every line number below was read
in this session; every "dead" claim carries the grep that found no consumer.

## 0. Method

Greps run (all with `--include=*.cs --exclude-dir=bin --exclude-dir=obj`):

```
grep -rnE "\bForm\b|Form\.(Strike|Projectile|Aura|Trap|Mark|Transformation)|WovenAbility|FormBehaviour|LegacyForm|\.Ability\b|Weaving\.|WeaveContext|Aptitude" src/ tests/
  -> 1322 lines: Core 358, Game 243, tests 721. 96 files.
grep -rnE "\bVow\b|VowDemand|VowKind|VowMultiplier|Weaving\.IsActive|Weaving\.ById|Weaving\.Catalog|WeaveContext" src/   (Vow consumers)
grep -rn  "MetBy\|NeedsForm\|\.Needs\b" src/ tests/                                                                  (enchant Form needs)
grep -rnE "FormPowerFor|FormTargets|AptitudeShape|TotalShape|FavouredForms|GearShape\.Of|LastWaveTopForm|Affinity\(\)|build\.Affinity" src/ tests/
grep -rnE "SkillId:|SkillId =|SkillPick\(" src/ tests/                                                                (does the id ever reach the composer?)
grep -rn  "Warded\|TopForm" src/ tests/                                                                               (WARDED liveness)
grep -rnE "Weaving\.(AbilityPower|ConditionalMultiplier|StaticMultiplier|BasePower|SourceEffectiveness)" src/          (which Weaving API is live)
grep -rn  "Affinity" src/ResonanceHunter.Game/Game1.cs src/ResonanceHunter.Core/Persistence/*.cs                       (SaveGame.Affinity liveness)
grep -rnlE "\bForm\b|WovenAbility|FormBehaviour|Source ?[x×] ?Form|resonance-weaving" design/ docs/ tools/ production/ (docs + tools)
```

Per-file hit counts (top): `tests/.../Builds/SoloBattleTests.cs` 123, `skill_slot_kinds_test.cs` 84,
`Game/SoloExpeditionScreen.cs` 73, `Core/Builds/SoloBattle.cs` 68, `Game/WeaveScreen.cs` 64,
`Core/Builds/FormBehaviour.cs` 46, `tests/.../Weaving/WeavingTests.cs` 42, `beat_cadence_test.cs` 37,
`Game/BuildScreen.cs` 34, `roster_parity_test.cs` 32, `SkillShapeBattleTests.cs` 29, `BuildGlossary.cs` 29,
`SkillCatalogue.cs` 28, `element_sets_test.cs` 27, `source_signature_test.cs` 26.

## 1. Executive summary

`Form` is dead as a *design* concept (the catalogue is STYLE → SKILL → VARIATION → REINFORCEMENT since
2026-08-30) but it is still the **runtime identity of a skill**. `EquippedSkill` is built on
`WovenAbility { Name, Source, Form, Vow }` (`Build.cs:206`), `EquippedSkill.Def` re-resolves the catalogue
entry from `(Form, Passive)` on every read (`Build.cs:256`), and the sim's base damage for every cast,
tick, trap and Weaver echo is `WeavingTuning.FormBaseValue[form]` through `FormBehaviour.BaseDamage`
(`SoloBattle.cs:1420,1573,1707,1926`). `SkillDef` has **no base-power field** — the catalogue's twelve
skills carry cadence, targets and two dozen dials but borrow their hit size from the six-row Form table via
`LegacyForm`. The live session notes already record the consequence (field_pulse is net-negative on the
bench because it resolves through Form.Aura's 12).

Three further coupling defects were found by reading: (a) `PlayerLoadout.ToBuild` never passes `SkillId`
into `BuildComposer.SkillPick` (`PlayerLoadout.cs:268`), so the "THE ID WINS" path in
`BuildComposer.cs:87-92,171-173` is unreachable in production and every live build is resolved by Form;
(b) DRAIN's GLUT variation says "No lifesteal" and sets `Lifesteal = 0f` (`SkillCatalogue.cs:557-558`) but
`SoloBattle.cs:1721 if (FormBehaviour.Heals(form))` heals `TransformationLeech` regardless of the dial, so
GLUT still heals; (c) the WARDED band affix is Form-keyed (`Bands.cs:41-48`) and has no consumer at all
(`SoloExpedition.LastWaveTopForm` is declared at `:86` and never assigned; `Affix.Warded` is read nowhere
in `Bands.cs:155-180` or `SoloBattle.cs`).

The **Vow system is live and healthy** and must survive: `SoloBattle.VowFactor`/`DescribeBuild`
(`:2101-2145`), `VowHealthMultiplier` (`:2063-2072`), WeaveScreen, Game1.VowWasKept, DustEffects.KnownVows,
quests and two characters all consume it. Its only Form dependency is `VowDemand.SingleForm` →
`WeaveContext.DistinctForms` → `build.Skills.Select(s => s.Form)`. It should move out of the Weaving file
into its own `Builds/Vows.cs` with `SingleForm` renamed `SingleStyle`.

The `resonance-weaving-system.md` GDD describes a manual-combat, part-targeting, Summon-having model that
never existed in code; it should be marked SUPERSEDED. The only parts still live are the Source ring
(Formula 4's circulant table, at different multipliers) and the two Vow formulas — both already restated
in `design/gdd/vows.md` (Status: Implemented) and `BuildGlossary`.

## 2. `ResonanceWeaving.cs` — type by type (544 lines, namespace `ResonanceHunter.Core.Abilities`, folder `Weaving/`)

| Lines | Member | Class | Verdict / replacement |
|---|---|---|---|
| 9 | `enum Form { Strike, Projectile, Aura, Trap, Mark, Transformation }` | **A-delete** (B for the string→SkillId map) | Runtime concept gone. Keep only a private `string → SkillId` table inside save migration (`SavedSkill.Form` is stored as the enum *name*, `SaveGame.cs:297`). |
| 14-28 | `enum VowKind { Demand, StaticCost }` | **C — keep, move to `Builds/Vows.cs`** | Read by `SoloBattle.cs:2068,2104`, `ResonanceWeaving.cs:324,501`, WeavingTests. |
| 48-83 | `enum VowDemand` | **C — keep, move** | `SingleForm` (`:54`) → `SingleStyle`. All others untouched. |
| 90 | `enum BareSlot` | **C — keep, move** | Local mirror of `Economy.GearSlot`, mapped in `SoloBattle.DescribeBuild:2120-2129`. Ring/Charm mapped but no catalogue Vow uses them (harmless). |
| 92-142 | `record Vow` | **C — keep, move** | 15 catalogue entries; fields all read (`Severity` `:329`, `StaticCostMagnitude` `:330,2068`, `DamageTakenIncrease` `:2068`, `Bare` `:513`, `Threshold` `:509-510`, `Short`/`Name`/`Description` on screens). |
| 144-254 | `record WeavingTuning` | **split** | `MaxPowerBonus`, `CurveExponent`, `StaticCostConversionRate` → **C** `VowTuning`. `SourceScalingCoefficient` (`:195`, resonance → skill power) → **C** skill tuning (read `FormBehaviour.cs:293-296`, StatsScreen `:317-319`). `StrongMultiplier`/`WeakMultiplier` (`:201-202`) → **C** Source matchup tuning (`SoloBattle.cs:727-729`, WeaveScreen `:1333`, BuildGlossary `:88-95`). `FormBaseValue` (`:231-251`) → **A-delete** once its six numbers become `SkillDef.BasePower` on the twelve skills. |
| 256-263 | `record WovenAbility { Name, Source, Form, Vow }` | **A-delete** | Constructed only in `BuildComposer.cs:166` and ~40 test helpers. `EquippedSkill` should carry `SkillDef` (or `SkillId`) + `Source` + `Vow` directly. `Name` is read only by `Build.Unweave(string)` (`Build.cs:468`). |
| 276-289 | `Weaving.SourceEffectiveness` | **C — keep, rename/move** (`SourceMatchup`) | Live: `SoloBattle.cs:729`, WeaveScreen `:1333`, BuildGlossary `:85`, 4 test files. Not Form-dependent. |
| 299-331 | `ConditionalMultiplier`, `StaticMultiplier`, `VowMultiplier` | **C — keep, move to Vows** | `SoloBattle.cs:2110`, WeaveScreen `:1915`, BalanceSweepTests, WeavingTests. |
| 334-335 | `Weaving.BasePower(Form, resonance, tuning)` | **A-delete** | Only caller in src is `FormBehaviour.BaseDamage` (`FormBehaviour.cs:296`); tests call it directly (WeavingTests `:211,230,273`). Replace by `SkillDef.BasePower × (1 + coeff × resonance)`. |
| 344-358 | `Weaving.AbilityPower(WovenAbility, …)` | **A-delete** | `grep -rnE "Weaving\.AbilityPower" src/` → **no src caller**; only WeavingTests `:207,208,226,227,257,258,265`. The whole pipeline function is test-only. |
| 381-487 | `Weaving.Catalog` (15 Vows) | **C — keep, move** | Consumers: `DustEffects.KnownVows` `:126`, ChestScreen `:746`, tests. Vow copy speaks "FORM" at `:389-390` (VOW OF THE SINGULAR: "ONE FORM ONLY", "EVERY SKILL YOU CARRY MUST BE THE SAME FORM") → rewrite as STYLE. |
| 489 | `Weaving.ById` | **C — keep, move** | BuildComposer `:165`, BuildScreen `:759,779,830`, WeaveScreen `:1132,1710,1971`, Game1 (indirect). |
| 498-516 | `Weaving.IsActive(Vow, WeaveContext)` | **C — keep, move** | `:505 DistinctForms` → `DistinctStyles`. Consumers `SoloBattle.cs:2104`, WeaveScreen `:1145,1891`, Game1 `:5188`, tests. |
| 529-544 | `record struct WeaveContext` | **C — keep, move** | Field `DistinctForms` → `DistinctStyles`; filled by `SoloBattle.DescribeBuild:2133`. `Empty` used by WeaveScreen `:610`. |

The file header comment ("Vows are the whole point of the system", `:12`) is correct about what survives.

## 3. `FormBehaviour.cs` (298 lines) — what it still computes and who calls it

| Lines | Member | Still live in the fight? | Class / replacement |
|---|---|---|---|
| 61 `CooldownBeats(Form)` | delegates to `SkillCatalogue.Resolve(form, natural).Beats` | Yes, `SoloBattle.cs:528` (opening-breath skip). Also `SoloExpeditionScreen.cs:2926,3039,3048`, beat_cadence_test, skill_slot_kinds_test `:609`. | **A** — every caller already has `sk.Def.Beats`. |
| 72 `Native(Form)` | private bridge | — | **A** |
| 110 `BaseCooldownMs(Form)` | beats×1500 or IntervalMs or 8000 | Yes: `SoloBattle.cs:1412` (per-tick scaling of a "spilled" active), `BuildComposer.cs:180` (seeds `EquippedSkill.CooldownMs`), `BuildGlossary.cs:49`, `SoloExpeditionScreen.cs:2963,2972`, ~45 test call sites (`FormBehaviour.BaseCooldownMs(form)` is the universal test fixture). | **A** → an extension on `SkillDef` (`CooldownMs` = Beats×DefaultBeatMs / IntervalMs / reaction re-arm). |
| 132 `Targets(Form)` | Aura ∞, Projectile 2, else 1 | Yes via `SkillShape.TargetsFor(Form)` at `SoloBattle.cs:1424` (aura), `:1715` (weaver), `:1943` (trap). The cast path already uses `vdef.Targets` (`:1654`). | **A** — `SkillDef.Targets` exists. |
| 140 `IsPassive`, 160 `FiresOnBeingHit` | Aura / Trap | `Build.cs:220` (EquippedSkill.Passive fallback), `BuildComposer.cs:102-103`, `SoloBattle.cs:1705` | **A** → `Def.Kind == Field` / `Def.Kind == Reaction && On == Bitten`. |
| 163 `IsAmplifier`, 226 `Heals` | Mark / Transformation | `SoloBattle.cs:1376,1503,1705` / `:1435,1721` | **A** → `Def.Effect == Amplify` / `Def.Effect == Heal`. |
| 157 `AuraTickMs = 1000` | fallback tick | `SoloBattle.cs:1260` (only when `Def.IntervalMs == 0`), `SoloExpeditionScreen.cs:1149,3432`, BuildGlossary `:54` | **C** — keep as `SkillCatalogue.DefaultFieldTickMs` or make every Field declare IntervalMs and delete. |
| 170 `MarkWindowMs = 4 beats`, 178 `MarkMultiplier = 1.6` | SIGN's base window/depth | `SoloBattle.cs:786,1507`; BuildGlossary `:62-63`; SoloBattleTests `:182-184` | **C** → SIGN's `SkillDef.AmplifyMs` / `AmplifyPercent` base values on `sign_call`/`sign_brand` (the dials already exist, `SkillCatalogue.cs:187-188`, and are applied *on top* of the Form constant today, `SoloBattle.cs:1388-1394,1517`). `shape.MarkWindowMultiplier`/`MarkPowerBonus` stay as modifiers. |
| 187 `TransformationLeech` | → `HealTuning.Default.TransformationLeech` | `SoloBattle` reads `heal.TransformationLeech` directly (`:1437,1727`); this alias is read by BuildGlossary `:71`, heal_balance_test `:300` | **C** → DRAIN's `SkillDef.Lifesteal` base (see defect §7.2). |
| 194 `CastClipMs`, 200 `ClipShareOfBeat`, 223 `SkillClipShareOfBeat` | presentation timing | `SoloExpeditionScreen.cs:3300,3316` only (`grep CastClipMs|ClipShareOfBeat|CastGapFor src/` → no Core caller besides the file) | **C** — presentation constants living in Core; move to an animation/tuning type (Core/Animation or Game). Not Form-shaped despite the host class. |
| 245-284 `AffinityFactor(Form,Form[,vowSworn])`, `HexDistance`, `FactorAtDistance` | the Nen hexagon | `SoloBattle.cs:711,788,1585-1586,1712-1713`; WeaveScreen `:1108-1109`; FormHexDiagram `:156,172`; BuildScreen comment `:1489` | **C** → `StyleAffinity(Style, Style)` using `SkillCatalogue.RingDistance` (`SkillCatalogue.cs:661`, already written "corrected in ORDER"). `Build.Affinity` (`Build.cs:441`, `Form?`) and `MasteryTree.Affinity()` (`MasteryTree.cs:253`, `Form?`) become `Style?`; `MasteryNode.Form` (`:63`) → `Style`. |
| 293 `BaseDamage(Form, resonance, tuning)` | `Weaving.BasePower` | `SoloBattle.cs:1420,1573,1707,1926` — **every skill hit in the game** | **A** → `SkillDef.BasePower`. |

## 4. Where Form still decides the fight — `SoloBattle.cs`

| Line | Code | Kind | Replacement |
|---|---|---|---|
| 522-528 | `var f0 = skills[i].Form; … if (FormBehaviour.CooldownBeats(f0) > 0) continue;` | bridge | `skills[i].Def.Beats > 0` |
| 703 | `float Amp(int absMs, Source? skillSource, Form? skillForm = null, …)` | signature | `Style?` (or `SkillDef?`) |
| 709-720 | `build.Affinity is { } aff && skillForm is { } f … FormBehaviour.AffinityFactor(aff, f)` | affinity | Style ring |
| 729 | `Weaving.SourceEffectiveness(s, target, wt)` | Source matchup | keep (rename) |
| 786-788 | `FormBehaviour.MarkMultiplier + shape.MarkPowerBonus … AffinityFactor(markAff, Form.Mark)` | SIGN constant + content-ID affinity | SIGN base dial; `Style.Sign` |
| 811 | `m *= shape.FormPowerFor(skillForm.Value)` | aptitude / weapon favour | `StylePowerFor(style)` |
| 1085 | `LandSpread(…, Source? skillSource, Form? skillForm, …)` | signature | Style? |
| 1236 | `var form = sk.Form;` | loop identity | `sk.Def` |
| 1260 | `sk.Def.IntervalMs > 0 ? … : FormBehaviour.AuraTickMs` | fallback | default constant |
| 1288, 1543, 1559, 1714, 1942 | `new BattleEvent(BattleEventKind.Skill/Aura, (int)sk.Source, (int)form, ms)` | **event payload carries `(int)Form`** | carry the slot index (or SkillId ordinal); presentation resolves `Def.ClipKey`/`FxKey` |
| 1376, 1503, 1705 | `FormBehaviour.IsAmplifier(form / woven.Form)` | semantic via Form | `Def.Effect == Amplify` |
| 1412-1420 | `castMs = FormBehaviour.BaseCooldownMs(form); … aura = FormBehaviour.BaseDamage(form,…) * perTick` | the "spill" scaling: a Field's damage is a cast's Form value divided by its cast time | disappears when each SkillDef quotes `BasePower` in its own units (per tick for a Field) |
| 1424, 1715, 1943 | `shape.TargetsFor(form)` / `TargetsFor(Form.Trap)` | Form-keyed baseline | `TargetsFor(def)` — `:1654` already does this for casts |
| 1435, 1721 | `FormBehaviour.Heals(form)` → `heal.TransformationLeech` | semantic via Form | `Def.Effect == Heal` + `Def.Lifesteal` (see §7.2) |
| 1507 | `FormBehaviour.MarkWindowMs` (×9/5 under LINGER) | SIGN constant | `Def.AmplifyMs` base |
| **1551** | `if (form == Form.Projectile && triggers.Contains(BuildTrigger.Overdraw)) casts += 1;` | **hardcoded content branch** | `Def.Style == Style.Volley` (or a `Def.ExtraCasts` dial the trigger sets) |
| 1573 | `raw = FormBehaviour.BaseDamage(form, resonance, wt);` | base damage | `Def.BasePower` |
| 1584-1586 | `AffinityFactor(affinityForm, form, vowSworn: true) / AffinityFactor(affinityForm, form)` | Nen buy-back | Style ring |
| **1601** | `chargeLive && charge > 0 && form == Form.Strike && c == 0 && Rend` | **hardcoded content branch** | `Def.Style == Style.Hammer` |
| **1613** | `form == Form.Strike && triggers.Contains(BuildTrigger.Execute)` | **hardcoded content branch** | `Def.Style == Style.Hammer` |
| 1694-1716 | WEAVER: `!IsAmplifier(woven.Form) && !FiresOnBeingHit(woven.Form) … BaseDamage(woven.Form…) … TargetsFor(woven.Form)` | "next Form in the loadout" | `woven.Def.Kind/Effect/BasePower/Targets` |
| 1900-1943 | TRAP block: `BaseDamage(Form.Trap, …)` fallback (`:1926`), event `(int)Form.Trap` (`:1942`), `TargetsFor(Form.Trap)` (`:1943`) | content-ID | `sk.Def.BasePower/Targets`; note the block is already selected by `Def.Kind == Reaction && On == Bitten` (`:1904`) |
| 2063-2072 | `VowHealthMultiplier` — `v is { Kind: VowKind.StaticCost, … }` | Vows | keep |
| 2101-2111 | `VowFactor` — `Weaving.IsActive`, `Weaving.VowMultiplier` | Vows | keep (namespace move) |
| 2115-2145 | `DescribeBuild` — `DistinctForms: build.Skills.Select(s => s.Form).Distinct().Count()` | Vows | `DistinctStyles: … s.Def.Style …` |

Also comment-only Form mentions at `:41,343-344,388,447,623,771-774,792,1238,1253,1387,1402,1448,1478-1479,1499,1561,1745,1900,1915` — stale terms, fix with the code.

## 5. The ten questions

### Q1. What does the Vow system depend on from Form? Where should Vows live?

Exactly one thing: `VowDemand.SingleForm` (`ResonanceWeaving.cs:54`) is evaluated as
`ctx.DistinctForms <= 1` (`:505`), and `DistinctForms` is filled from `build.Skills.Select(s => s.Form)`
(`SoloBattle.cs:2133`). Everything else a Vow reads (`DistinctSources`, `SkillsWoven`, `SkillSlots`,
`CritPercent`, `SkillRate`, `Defence`, `KeystonesWorn`, `WornSlots`) is Form-free.

`SingleForm` should become **`SingleStyle`** ("every woven skill is the same STYLE"): `DistinctStyles =
build.Skills.Select(s => s.Def.Style).Distinct().Count()`. That is the honest reading of VOW OF THE SINGULAR
("go deep, not wide") under the current model — a HAMMER build carrying BLOW + PRESS is one discipline. Note
this *changes* behaviour for exactly one existing case: today a build with BLOW (Form.Strike) + PRESS
(also saved as Form.Strike, `PlayerLoadout.SetSkill:194-197`) already reads as one Form, so `SingleStyle`
is behaviour-preserving for every build a player can currently save. The copy at `:389-390` and
`WeaveScreen.cs:579` ("EVERY SKILL THE SAME FORM") must say STYLE.

Move list (all C, no semantic change):
- `VowKind`, `VowDemand`, `BareSlot`, `Vow`, `Weaving.Catalog`, `ById`, `IsActive`, `ConditionalMultiplier`,
  `StaticMultiplier`, `VowMultiplier`, `WeaveContext` → **`src/…/Builds/Vows.cs`** (static class `Vows`,
  namespace `…Core.Builds`), plus a `VowTuning { MaxPowerBonus, CurveExponent, StaticCostConversionRate }`
  carved out of `WeavingTuning` (`:147-185`).
- `Weaving.SourceEffectiveness` + `StrongMultiplier`/`WeakMultiplier` → a small `SourceMatchup` type
  (Automation or Builds; `Source` lives in `Core/Automation/Source.cs:12`).
- `SourceScalingCoefficient` → wherever `SkillDef.BasePower` is scaled (a `SkillTuning`).
- Then `Weaving/ResonanceWeaving.cs` and the `Core.Abilities` namespace are empty and can go. The GDD
  `design/gdd/vows.md` (Status: Implemented) is already the live spec for what moves.

Consumers to re-point (mechanical): `SoloBattle.cs:2068,2086-2111,2115-2145`; `BuildComposer.cs:165`;
`DustEffects.cs:121-126`; `Build.cs:241`; Game `WeaveScreen.cs:545-599,609-610,1132-1145,1710,1891-1915,1971`,
`BuildScreen.cs:759,779,830`, `ChestScreen.cs:746`, `Game1.cs:5188`, `PlayerLoadout.cs:144,208`; tests
`WeavingTests` (13 Vow tests), `WeaveInBattleTests:46-93`, `BalanceSweepTests:448-500`, `SoloBattleTests`
(vow_singular/unbound/pure/fragility/reckless), `BuildTests:295-302`, `DustEffectsTests:285-294`,
`roster_parity_test`, `characters_roster_test:182-198`.

### Q2. `WovenAbility` — who builds it, what it carries, what still reads it

Fields: `Name` (string, "BODY STRIKE" from `PlayerLoadout.NameOf:273-274`), `Source`, `Form`, `Vow?`
(`ResonanceWeaving.cs:257-263`).

Production constructor: **one** — `BuildComposer.Compose`, `BuildComposer.cs:166`:
`new WovenAbility { Name = s.Name, Source = s.Source, Form = s.Form, Vow = vow }`. Tests construct it in
~40 helper methods (every `Sk(Form)` fixture).

Readers on `EquippedSkill` (`Build.cs:206-284`):
- `Passive => PassiveSlot ?? (IsPassive(Form) || FiresOnBeingHit(Form))` (`:219-220`) — Form.
- `Name => Ability.Name` (`:222`) — read only by `Build.Unweave(string)` (`Build.cs:468`).
- `Form => Ability.Form` (`:223`) — read by SoloBattle (§4), SoloExpedition `:212`, DescribeBuild `:2133`,
  BalanceSweepTests `:464-465`.
- `Source => Variation?.Source ?? Ability.Source` (`:240`) — the woven Source is now only a *fallback* until
  a variation is chosen; **live** and Form-free.
- `Vow => Ability.Vow` (`:241`) — live.
- `Def` (`:252-263`) — **`SkillCatalogue.Resolve(Form, Passive)`** then applies Variation/Reinforcement
  deltas. This is the bridge everything in the fight reads through.

What the composer computed is thrown away: `BuildComposer.cs:171-173` resolves `def` (by id if `SkillId`
is set, else by Form) and uses it only for the taught gate (`:178`) and cooldown seed (`:179-180`); the
`EquippedSkill` it constructs (`:189`) carries the `WovenAbility`, so `Def` is re-derived from Form. Because
`PlayerLoadout.ToBuild` never passes `SkillId` (`PlayerLoadout.cs:268`: `new SkillPick(s.Source, s.Form,
s.VowId, NameOf(s), s.Passive)` — five args, `SkillId` defaulted null) the id branch is unreachable from
the game. `grep -rnE "SkillId:|SkillId =" src/ tests/` → the only writers of `SkillPick.SkillId` are none;
`PlayerLoadout.SkillChoice.SkillId` is written at `:197` and read only at `:65` (cache key) and
`WeaveScreen.cs:1485` (library highlight). It works today only because `SetSkill` (`:192-197`) also writes
the style's `LegacyForm` and `Passive = !def.TakesABeat`, and a style has exactly two skills.

Target shape: `EquippedSkill(SkillDef Def /*or string SkillId*/, Source Source, Vow? Vow, …)`; `Passive`
becomes `!Def.TakesABeat` (no fallback needed); `Form` property deleted.

### Q3. `FormBehaviour.cs` — see §3

Still live in the fight: `CooldownBeats` (`:528`), `BaseCooldownMs` (`:1412`), `Targets` via
`SkillShape.TargetsFor` (`:1424,1715,1943`), `IsPassive`/`FiresOnBeingHit` (via `EquippedSkill.Passive` and
`:1705`), `IsAmplifier` (`:1376,1503,1705`), `Heals` (`:1435,1721`), `AuraTickMs` fallback (`:1260`),
`MarkWindowMs`/`MarkMultiplier` (`:786,1507`), `AffinityFactor` (`:711,788,1585,1712`), `BaseDamage`
(`:1420,1573,1707,1926`). Nothing in it is dead; all of it is a Form-shaped door onto data the catalogue
already owns (its own remarks say so, `:53-57`). The clip-timing constants (`:194-223`) are presentation
tuning that should not live in a Core Form table.

### Q4. `SkillDef.LegacyForm` — where read

Declared `SkillCatalogue.cs:169`; set on 6 of 12 skills (`:289,373,401,456,538,566`), null on the other
six. Readers:
- `SkillCatalogue.Resolve(Form, passive)` `:653` — `All.First(s => s.LegacyForm == form).Style` (the bridge).
- `PlayerLoadout.SetSkill` `:194-196` (Game) — writes the style's Form back into the slot so old readers work.
- `WeaveScreen.cs:1492` — `hoverForm = def.LegacyForm` (hover preview).
- Tests: `champion_starting_skill_test:50-53`, `reinforcement_liveness_test:75-77`,
  `variation_liveness_test:79-88`, `skill_slot_kinds_test:151`, `skill_catalogue_test:169-199`.

So: **not** save-migration-only today — the live loadout round-trips through it on every edit and every
frame (WeaveScreen `:691,711,777,1037,1573`; SoloExpeditionScreen `:866,3010`; Game1 `:2097`). After the
refactor it belongs only in the `SavedSkill.Form → SkillId` migration (B), then deleted.

### Q5. `Character.Aptitude` / `AptitudePower`

`Character.cs:118` `Form? Aptitude`, `:133` `float AptitudePower = 1.25f`; `AptitudeShape` `:206-209` builds
`SkillShape { FormPower = { [f] = AptitudePower } }`; `TotalShape` `:212` combines it; consumed at
`BuildComposer.cs:131` (`character?.TotalShape`) → `Build.Shape` → `SoloBattle.cs:811`
`m *= shape.FormPowerFor(skillForm.Value)` (behind the `skillForm is null` gate at `:806`, so the swing is
excluded). **Live.** Nine of ten champions set it (`CharacterRoster.cs:73,88,102,118,132,149,169,189,212`);
RosterScreen prints it ("BEST AT … +25%", `:306-309`); `characters_roster_test:126-142` and
`roster_parity_test:229` pin it.

If removed outright: nine champions lose a real ×1.15–1.35 lever and `roster_parity_test` (which plays each
champion on `c.Aptitude ?? Form.Strike`) loses its fixture. Recommended **C**: `Style? Aptitude` +
`SkillShape.StylePower` (rename of `FormPower`, `SkillShape.cs:202-205`, `Combine :465-467`). The weapon
favour (`GearShape.Of` `:27-29`, `ItemFamilies.FavouredForms` `:49-58`) and the mastery spec_projectile
`FormTargets` (`MasteryCatalog.cs:439`, `SkillShape.cs:187,442`) ride the same rename. Style is the right
key: an aptitude "for the Form the QUIVER shoots" means VOLLEY's two skills, which is what a Style is.

### Q6. Enchantments `EnchantNeed.Form` and the six Form combos

`Enchantments.cs:103-129`: `EnchantNeed(Label, Abilities.Form? Form, BuildTrigger? Keystone, bool AnyVow)`
with `MetBy(IReadOnlyCollection<Form> forms, triggers, swornVows)`. The six Form combos are declared at
`:175-180` (Overdraw→Projectile, Linger→Mark, Radiance→Aura, Execute→Strike, Coiled→Trap,
Siphon→Transformation); `NeedsForm` at `:189`.

How "needs Form" is evaluated against the live build: **by the Game, from the loadout's saved Form**, not
from the skills. `Game1.cs:2878` `_forge.ActiveForms = _loadout.Skills.Select(s => s.Form).ToList()`;
`ForgeScreen.cs:551` `Needs.MetBy(ActiveForms, ActiveTriggers, SwornVows)`; `WeaveScreen.GearWants:1356-1363`
`Loadout.Skills.Select(sk => sk.Form)` vs `e.NeedsForm`. Because `SetSkill` writes the *style's* Form,
"needs PROJECTILE" is met by either VOLLEY skill — i.e. it is already a Style check wearing a Form name.
The sim side is independent: `SoloBattle` never consults `EnchantNeed`; each trigger fires inside its Form
branch (`:1551` Overdraw on `Form.Projectile`, `:1613` Execute on `Form.Strike`, `:1261` Radiance in the
Field block, `:1507` Linger in the amplifier block, `:1911` Coiled in the trap block, `:1723,1437` Siphon
under `Heals`).

**C**: `EnchantNeed.Style` (Overdraw→Volley, Linger→Sign, Radiance→Field, Execute→Hammer, Coiled→Snare,
Siphon→Drain); `MetBy(IReadOnlyCollection<Style>, …)`; hosts pass `build.Skills.Select(s => s.Def.Style)`.
Labels `:175-180` ("PROJECTILE", "MARK", …) become style names. `Blurb` copy at `:151-156` ("VOLLEY FIRES
+1", "AURA HITS FASTER") already half-speaks Style. Radiance's "ticks faster" is really a Field-kind rule and
Coiled's "re-arms faster" a Reaction-kind rule — a future option is to key those two on `SkillKind` rather
than Style, but Style is the smallest correct change. `EnchantmentsTests:118-123,211-233` need the rename.

### Q7. `ElementSets.MarkWindowMultiplier` and other Mark references

`ElementSets.cs:89` — the MIND set's 4-piece rung: `"A MARK's window lasts 50% longer.", new SkillShape {
MarkWindowMultiplier = 1.5f }`. `MarkWindowMultiplier` (`SkillShape.cs:303`) is read at
`SoloBattle.cs:1381` (standing BRAND window) and `:1508` (CALL window) — it multiplies whatever window the
skill has, so it is **already the current amplify concept** (SIGN), not Form. Same for `MarkPowerBonus`
(`:304`, read `:786`), THE OATHBOUND's shape (`CharacterRoster.cs:201`) and spec_mark (`MasteryCatalog.cs:443`).
**C — rename only** (`AmplifyWindowMultiplier` / `AmplifyPowerBonus`, copy "SIGN's window"), no semantic
change. The Form-shaped part is the *baseline* it multiplies (`FormBehaviour.MarkWindowMs` / `MarkMultiplier`,
§3), which becomes SIGN's `SkillDef.AmplifyMs`/`AmplifyPercent` base.

Other Mark sites: MIND signature stretches `champ.MarkUntilMs` (`SoloBattle.cs:1734-1739`, Source-keyed,
fine); `BuildGlossary.Signature(Source.Mind)` "stretches an open MARK window" (copy → "SIGN window").
`element_sets_test:196-197` weaves `Form.Mark` fixtures.

### Q8. Game project — what still speaks Form to the player

- **`FormHexDiagram.cs`** (whole file, 200+ lines): draws six `icon_form_*` seals on a hexagon with
  `FormBehaviour.AffinityFactor` numbers (`:123-172,196`). Called from `BuildScreen.cs:1322-1324` (mastery
  hex panel) and `:1395-1396` (attunement ceremony with `_attuneForm`). **C** → a `StyleHexDiagram` on the
  six Styles using `icon_skill_*` or new style glyphs; the six `assets/art/UI/icons/forms/icon_form_*.png`
  become orphans (tools/asset-pipeline/build_spec.py `:714-733` still generates them).
- **`WeaveScreen.cs`**: `Discipline` is `Form?` (`:43`); `Forms = Enum.GetValues<Form>()` (`:417`);
  `FormName` (`:573`); `DemandText` prints "EVERY SKILL THE SAME FORM" (`:579`); every row resolves the skill
  via `SkillCatalogue.Resolve(s.Form, passive)` (`:691,711,777,1037,1573`); `icon_form_` glyphs (`:1051,1705`);
  affinity tags via `FormBehaviour.AffinityFactor(dd, s.Form[, vowSworn])` (`:1108-1109`); hover DPS preview
  swaps `Loadout.SetForm` (`:1256-1278`, the Source/Form cycling preview from the free-composition era);
  `GearWants` by `NeedsForm` (`:1356-1363`); `hoverForm = def.LegacyForm` (`:1492`); labels "{SOURCE} {FORM}"
  (`:960,1709,1773,1955`) and `BuildGlossary.FormHeadline/FormRule` (`:1749-1750,1773-1774`). The header
  comment `:18` still says "pick each skill's Source and Form". Screen is otherwise SkillId-driven (`:1485`).
- **`BuildScreen.cs`**: `Short(Form)` (`:51-53`, "VOLLEY"/"MORPH" aliases), `_attuneForm`/`DevAttune(Form)`
  (`:83,173-176`), identity card "{SOURCE} {Short(Form)}" (`:758,829`), tree drawing with `Form? aff`
  (`:1032,1051,1294,1645`), specialisation node labels "FORM" (`:1768`), "ADEPT" title from `Mastery.Affinity()`
  (`:715,803`).
- **`PlayerLoadout.cs`**: `SkillChoice(Source, Form, VowId, Passive, SkillId)` (`:51-52`); `CycleForm`/`SetForm`
  (`:137-140,177-180`) — the free-composition verbs, still public; `SetSkill` writes Form (`:192-197`);
  `ToBuild` drops SkillId (`:268`); `NameOf` "{SOURCE} {FORM}" (`:273-274`); `SaveSkills`/`Restore` as
  `(Source, Form, VowId, Passive)` strings (`:278-296`); `Starter` = `(Body, Strike)` (`:100,319`).
- **`SoloExpeditionScreen.cs`**: decodes `(Form)e.Amount` on every Skill event (`:1254`), `CalloutFor(Form)`
  callouts "STRIKE/VOLLEY/AURA/TRAP/MARK" (`:682-688`), `PlayFormVfx(Form, Source)` switch (`:1366-1405`),
  `FxFor(form, passive)` (resolves through `SkillCatalogue.Resolve(form, passive).FxKey`, `:3350-3358`),
  clip choice `((Form)nextSkill.Amount).ToString().ToLowerInvariant()` (`:3277-3280`), rail keyed by
  `(Source, Form)` (`:347-371,2904`), rail cadence from `FormBehaviour.CooldownBeats/BaseCooldownMs(s.Form)`
  (`:2926,2963,2972,3039,3048`), `_auraForm` (`:869,3426`), "ADEPT" title (`:2339-2340`), `FormShort`
  (`:2303-2305`). **C** → events carry the slot index; the screen reads `Def.ClipKey`/`FxKey`/`Name`/`Beats`.
- Smaller: `CharacterScreen.cs:414` cache key `"{Source}:{Form}:{VowId}"`, `:553,1167-1169` "ADEPT"/`FormShort`;
  `StatsScreen.cs:226-227,635-637` "ADEPT"; `ChestScreen.cs:734-747` share-code card "DISCIPLINE: {Form}" and
  "{SOURCE} {FORM}" rows; `RosterScreen.cs:306-309` "BEST AT {Form} +N%"; `ForgeScreen.cs:524` `ActiveForms`;
  `Game1.cs:637,880,946` save round-trip of `Form` strings, `:1607` dev "attune" → `Form.Strike`, `:1711,
  1732-1734,2074-2076` dev fixtures by `(Source, Form)`, `:2097` `SkillCatalogue.Resolve(sk.Form, …)`,
  `:2878` ActiveForms, `:2932` "differs from starter" = `Form != Form.Strike`.
- Player-facing copy in Core: `Onboarding.cs:256-258` "A skill is a Source and a Form … Pick one of each
  and the skill is woven" (false since 2026-08-30 — skills are learned on the tree), `:276-277` "one Form
  whose skills hit twice as hard"; `Unlocks.cs:254` "Every Source and every Form is available to you";
  `ResonanceWeaving.cs:389-390` VOW OF THE SINGULAR copy; `Enchantments.cs` labels.

### Q9. Tests that exist only for the legacy composition

| Test file | What it pins | Recommendation |
|---|---|---|
| `tests/…/Weaving/WeavingTests.cs` (289 lines) | Source ring (4 tests, Form-free), Vow pricing/demand/catalogue (9 tests, Form-free except `WeaveContext.DistinctForms` at `:120,126`), `test_a_conditional_vow_grants_no_power_when_its_condition_is_unmet` and `test_a_static_vow_applies_regardless_of_condition` (`:196-231`, via `Weaving.AbilityPower` on a `WovenAbility`), `test_the_matchup_wins_ties_but_no_longer_beats_real_investment` (`:245-269`, `AbilityPower`), `test_mark_deals_no_direct_damage` (`:271-273`, `BasePower(Form.Mark)==0`) | **Split**: Source-ring tests → `SourceMatchupTests`; Vow tests → `VowsTests` (rename `DistinctForms`→`DistinctStyles`); the three `AbilityPower`/`BasePower` tests **delete** — `AbilityPower` has no runtime caller and the Vow-gating they prove is covered end-to-end by `SoloBattleTests` (vow_singular/unbound `:327-365`) and `BalanceSweepTests.test_every_vow_is_worth_swearing…`. "Mark deals no damage" → assert `sign_call.BasePower == 0` in `skill_catalogue_test`. |
| `Builds/affinity_test.cs` | Mark specialist node helps a Mark build; every Form specialisation pays for its Form; a specialisation costs on other Forms | **Rewrite against Style** (`spec_*` nodes become Style specialisations; the assertions are about the mastery nodes, which survive). |
| `Builds/affinity_vow_buyback_test.cs` | Vow buy-back moves an off-discipline skill one ring in; reaches the fight | **Rewrite against Style** (`StyleAffinity(a, b, vowSworn)`); keep both assertions. |
| `Builds/one_discipline_test.cs` | one Specialisation per tree; `tree.Affinity() == Form.Strike` (`:28-37`) | **Keep, rename** to `Style.Hammer`. |
| `Builds/family_form_affinity_test.cs` | every Form favoured by a weapon family; `GearShape.Of` → `FormPower`; bow benches higher for Projectile (`:38-77`) | **Rewrite against Style** (`FavouredStyles`, `StylePowerFor`); the bench assertion is the live one. |
| `Builds/source_signature_test.cs` | the six Source signatures | **Keep**; it is Source-only. Its `OneSkill(Source, Form)` fixture (`:29-34`) and `Weaving.SourceEffectiveness` calls (`:26-27`) just need the fixture swapped to a SkillId. Not a legacy test. |
| `Builds/SkillShapeBattleTests.cs` | mastery shape fields reach the fight | **Keep**; only the `Sk(Form)`/`BuildWith(params Form[])` fixture (`:32-36,66-79`) and one `BaseCooldownMs(Form.Strike)` (`:246`) change. Not a legacy test. |
| `Builds/skill_slot_kinds_test.cs` | `test_every_legacy_form_resolves_inside_its_own_style` (`:58`), `test_the_form_table_now_answers_from_the_catalogue` (`:597-609`, asserts `FormBehaviour.CooldownBeats == def.Beats`), `test_every_skill_in_the_catalogue_can_be_selected` (`:627-636`, "no (Form, slot) pair reaches"), `test_choosing_the_passive_slot_reaches_the_styles_other_skill` (`:660-663`), `test_a_spilled_skill_does_not_multiply_itself_by_its_cooldown` (`:518-522`, the spill-scaling rule) | First two and the spill test **become migration fixtures** (B) then delete; "every skill can be selected" **rewrite** as "every SkillId composes"; the rest keep with SkillId fixtures. |
| `Builds/skill_catalogue_test.cs:169-199` | "every Form ordinal resolves, in either slot" | **B — migration fixture**, then delete with `Resolve(Form,…)`. |
| `Builds/beat_cadence_test.cs` | rhythm skills cast every Nth action; parameterised on `Form.Strike/Projectile` (`:90-92,115-117,422-424`) and reads `FormBehaviour.CooldownBeats` (`:94,181,288,433`) | **Keep**; parameterise on `"hammer_blow"/"volley_spray"` and read `def.Beats`. |
| `Builds/build_glossary_test.cs:55-95` | every Form is described with real numbers | **Rewrite** as "every SkillDef.Line / every Style headline" once `BuildGlossary.FormHeadline/FormRule` become per-skill/per-style. |
| `Expeditions/WeaveInBattleTests.cs` | Source ring; Vow pricing; `DescribeBuild` satisfies the demands (`:66-93`) | **Keep**, rename `DistinctForms`; fixture → SkillId. Not legacy. |
| `Persistence/SaveSystemTests.cs:320-342`, `share_codes_test.cs:60-159` | `SavedSkill{Source, Form, VowId}` round-trips; `"Form":"Strike"` JSON | **B — keep as the old-save fixture** for the `Form → SkillId` migration; add a new-format case. |
| ~35 other files (`SoloBattleTests`, `BalanceSweepTests`, `heal_balance_test`, `RunReportTests`, `WaveReplayTests`, `element_sets_test`, `TriggerLivenessTests`, `roster_parity_test`, `characters_roster_test`, `EnchantmentsTests`, `AffixLivenessTests`, `SplinterPaysTests`, `PowerRatingTests`, `wave_length_test`, `attrition_test`, `pacing_test`, `gleam_economy_test`, `progression_curve_test`, `GearTests`, `charge_keystone_test`, `skill_stagger_test`, `live_build_test`, `mastery_*_liveness_test`, `SoloExpeditionTests`, `DamageBenchTests`, `HunterStatWiringTests`, `PassiveTreeTests`, `BuildTests`) | use `new WovenAbility { … Form = f }, FormBehaviour.BaseCooldownMs(f)` purely as a **fixture** | **Keep**; introduce one shared `TestBuilds.Skill("hammer_blow", Source, Vow?)` helper and replace the ~45 fixture sites mechanically. `SoloBattleTests.cs:110-116` (`IsPassive` only Aura), `:388-394` (cooldown ≤ wave), `:423-430` (hexagon symmetry) are Form-table assertions → restate on the catalogue/Style ring. `EnchantmentsTests:118-123` ("every Form has a combo") → every Style. |

Nothing in `tests/unit/…/Weaving/` other than `WeavingTests.cs` exists.

### Q10. `design/gdd/resonance-weaving-system.md` — obsolete, with two live remnants

Confirmed obsolete (838 lines, Status "Complete", dated 2026-07-14): it specifies **manual combat with
part-targeting and cycle-and-confirm** (`§3.2:118-137`), a **Summon** Form that never existed in code
(`grep -rn Summon src/` → nothing) and no Strike, ability-focus **charm items** with `vow_binding_log`
(`§3.5-3.7:257-330`), a **10-Vow catalogue of fight-state conditions** (`vow_bloodied`, `vow_boss_bound`,
`vow_ten_blows`… `§3.3.4:192-218`) that the code explicitly retired (`ResonanceWeaving.cs:33-47`; only
`vow_fragility` and `vow_reckless_offering` survive by id), `gate`/`scale` effect modes (`§3.3.2`), Form
output fractions (`§4.5`) and a Mark amplification cap formula (`§4.6`) that the sim never implemented
(the sim's Mark is a flat ×1.6 window, `FormBehaviour.cs:170-178`). `systems-index.md:39,43-44,51,146,166-184`
still lists it as the Gameplay-tier dependency of combat, vow-condition-tracking, forge-ui and prestige.

Still-live ideas that need a home before it is marked SUPERSEDED:
- **§3.1 Sources (6, locked)** and **§4.4 Formula 4 (circulant 6×6 matchup)** — the ring structure is
  exactly `Weaving.SourceEffectiveness` (`:276-289`) but the multipliers moved (1.5/0.667 → 1.15/0.87,
  `:197-202`) and the ring *order* differs (GDD says body→nature→spirit→shadow→machine→mind; the enum is
  Body, Mind, Nature, Machine, Shadow, Spirit, `Source.cs:12`, and the sim uses enum order). Source
  signatures (`SoloBattle.Signature*`, `BuildGlossary.Signature:104-112`) have **no GDD at all**. → needs a
  short `design/gdd/sources.md` (ring + signatures + the "nudge not counter" rationale from
  `WeavingTests:236-244`).
- **§3.3 Vows, §4.1-4.2 Formulas 1-2, AC1-AC3c** — already superseded by `design/gdd/vows.md` (Status:
  Implemented, 2026-08-13), which restates severity → multiplier (`§4.1-4.3`) and the eHP static-cost rule.
  Only the eHP/"no Vow is free" history (`§4.2:398-479`, AC3b/AC3c) is worth a pointer from vows.md.
- **§4.3 Formula 3** `base = form_base_value × (1 + coeff × stat)` — this *shape* is what `SkillDef.BasePower
  × (1 + SourceScalingCoefficient × resonance)` will be; it belongs in `skill-slots-and-skill-trees.md` §5/§10
  next to the cadence table, not here.
- `design/gdd/vow-condition-tracking.md` (per-frame condition evaluation + HUD belt-ring) is obsolete for
  the same reason (Vows read the build, not the fight — `vows.md §3.1`) and should be superseded together.
- Other docs that still teach Form: `skill-and-trait-trees.md` (Draft, Weight/Spread/Tempo/Endure — the
  pre-2026-08-30 tree, 15 hits), `game-flow.md:324-393,471,512-601` (`Form.targets`), `characters.md:8,82-107`
  (aptitude "on a Form"), `combat-encounter-system.md` (7), `regions-and-rosters.md` (5), `game-concept.md`
  (5), `memory-dust-prestige-system.md`, `hunter-progression-system.md`, `onboarding-tutorial-system.md`,
  `design/registry/entities.yaml` (Vow constants), `docs/PROJECT-NOTES.md:110-1251` (history — leave),
  `production/audit/systems-map.json` (an older audit quoting `ResonanceWeaving.cs:115-123` numbers that
  no longer exist), `docs/store/store-page.md`, `tools/marketing/make_itch_page.py:403-440` ("Source × Form
  × Vow" strip on the itch page).

## 6. Full reference-site classification (aggregated per file)

Legend: A = delete (obsolete runtime concept), B = migration-only, C = replace with current abstraction.

### Core

| File | Sites | Class | Notes |
|---|---|---|---|
| `Weaving/ResonanceWeaving.cs` | 544 lines | A: Form, WovenAbility, FormBaseValue, BasePower, AbilityPower. C: everything Vow, SourceEffectiveness, SourceScalingCoefficient, Strong/Weak. | §2 |
| `Builds/FormBehaviour.cs` | whole file | A: all Form predicates/tables/BaseDamage/CooldownBeats/BaseCooldownMs/Targets. C: AuraTickMs (default), MarkWindowMs/MarkMultiplier (→ SIGN dials), clip constants (→ presentation tuning), AffinityFactor (→ Style). | §3 |
| `Builds/SkillCatalogue.cs` | `:14-22,81,116` (remarks), `:151-153,169` `LegacyForm`, `:289…593` six values, `:609-655` `Resolve(Form,bool)` | B then A | `Resolve` and `LegacyForm` are the migration bridge; delete after `SavedSkill` carries `SkillId`. Add `BasePower` per skill. |
| `Builds/Build.cs` | `:117,156` (comments), `:199-263` `EquippedSkill(WovenAbility…)`, `Passive` fallback `:219-220`, `Form` `:223`, `Def` bridge `:244-263`, `:305` comment, `:434-441` `Form? Affinity` | A (WovenAbility/Form/Def-bridge) / C (`Affinity` → `Style?`) | §Q2 |
| `Builds/BuildComposer.cs` | `:32-36` `SkillPick(Source, Form, …, SkillId)`, `:53,102-103` natural-passive by Form, `:165-180` WovenAbility + `Resolve(s.Form…)` + `BaseCooldownMs(s.Form)` | A / B | `SkillPick` → `(SkillId, Source, VowId, Passive?)`; the id branch is currently unreachable (§7.1). |
| `Builds/SkillShape.cs` | `:71-76` comments, `:183-205` `FormTargets`/`FormPower`/`FormPowerFor`, `:424-442` `TargetsFor(Form[,baseline])`, `:461-467,509` Combine | C | `StyleTargets`/`StylePower`; `TargetsFor(SkillDef)`. |
| `Builds/GearShape.cs` | `:8-9,25-29` | C | Style-keyed favour. |
| `Builds/BuildGlossary.cs` | `:8-76` `FormHeadline`/`FormRule(Form)`, `:85` SourceEffectiveness, `:164-167` `SkillSummary(Source, Form)` | C | Per-skill `Line` already exists on `SkillDef`; keep a per-Style headline; delete `SkillSummary`. Consumers: WeaveScreen `:1749-1774`, tests. |
| `Builds/MasteryCatalog.cs` | `:59-110` remarks, `:420-448` six `Spec(…, Form.X, …)`, `:439` `FormTargets`, `:451` comment, `:542-545` `Spec` helper sets `Form` | C | Specialisations keyed on `Style`. |
| `Builds/MasteryTree.cs` | `:25,41,62-63` `MasteryNode.Form`, `:84` comment, `:193-196` one-discipline rule, `:244-257` `Form? Affinity()` | C | `Style`. |
| `Builds/MasteryLayout.cs:224` | comment | C (term) | |
| `Builds/SoloBattle.cs` | §4 | A / C | The fight loop. |
| `Builds/SoloExpedition.cs` | `:81-86` `LastWaveTopForm` (dead), `:212` `Ability.Form` compare on build swap | A (dead) / C (`Def.Id` compare) | §7.3 |
| `Builds/HealTuning.cs:85` | comment "the Transformation combo" | C (term) | |
| `Characters/Character.cs` | `:82,117-118,132-133` Aptitude, `:164-200` clip keys named after Forms (`GenericClipFor` `:189-197`), `:206-212` AptitudeShape/TotalShape | C | Aptitude → Style; clip fallback map keyed on `ClipKey` strings (already strings). |
| `Characters/CharacterRoster.cs` | `:56-212` nine `Aptitude = Form.X` | C | Style. |
| `Characters/LegacyUnlocks.cs:49`, `CharacterRoster.cs:204`, `Quests/Quests.cs:84,148`, `Progression/Onboarding.cs:257-277`, `Progression/Unlocks.cs:130,254` | Vow quest text (fine); Form copy | C (copy) | Onboarding line is factually wrong today. |
| `Economy/Enchantments.cs` | `:49-73` remarks, `:98-129` `EnchantNeed.Form`/`MetBy(Form)`, `:158,175-180,188-189` | C | Style. §Q6 |
| `Economy/ItemFamilies.cs` | `:34-64` `FavouredForms`, `FormAffinity`, `FavouredFormsOf` | C | `FavouredStyles`. |
| `Economy/ElementSets.cs:89` | "A MARK's window" + `MarkWindowMultiplier` | C (term/rename) | §Q7 |
| `Encounters/Bands.cs:41-48`, `BandCycles.cs:88-99,111` | WARDED affix ("whichever Form dealt the most") | A or C | Dead today (§7.3); decide: implement on Style or delete the affix. |
| `Expeditions/WaveModel.cs:279-293` | `BattleEvent` doc: Skill/Aura `Amount = (int)Form` | C | Payload → slot index / SkillId. |
| `Expeditions/WaveReplay.cs` | `:162-175` `NextSkillEventAfter` skips `(Form)e.Amount == Form.Trap`, `:180-215` `NextSkillAfter(ms, form)`/`(ms, source, form)`, `:299-306` `LastTrapBefore` | C | Key on slot/SkillId; "is a Reaction" comes from `Def.Kind`. |
| `Forge/Reforge.cs:22`, `Prestige/MemoryDust.cs:451`, `Prestige/TraitRoads.cs:28`, `Prestige/DustEffects.cs:121-126` | comments ("Form-combo", "another Form"); `KnownVows` reads `Weaving.Catalog` | C | namespace move for Vows. |
| `Persistence/SaveGame.cs` | `:158-159` `Affinity` string (dead), `:293-297` `SavedSkill.Form` | A (dead) / B | `SavedSkill` gains `SkillId`; `Form` kept read-only for one migration. |
| `Persistence/ShareCodes.cs:226` | validates `s.Form` length | B | Share codes serialise `SavedSkill` (`:48`), so the `RHB1` format carries Form names too. |

### Game

| File | Sites | Class |
|---|---|---|
| `FormHexDiagram.cs` | whole file | C → Style diagram |
| `WeaveScreen.cs` | 64 sites (§Q8) | C; delete `CycleForm`/`SetForm` preview path |
| `BuildScreen.cs` | 34 sites (§Q8) | C |
| `PlayerLoadout.cs` | 23 sites (§Q8) | C; `SkillChoice` → `(SkillId, Source, VowId)`; delete `CycleForm`/`SetForm`; `SaveSkills`/`Restore` by SkillId |
| `SoloExpeditionScreen.cs` | 73 sites (§Q8) | C; decode events by slot, read `Def` |
| `Game1.cs` | `:637,880,946` save Form strings (B), `:1607,1711,1732-1734,2074-2076` dev fixtures (C), `:2097` Resolve (C), `:2878` ActiveForms (C), `:2932` starter check (C) | B / C |
| `ForgeScreen.cs:521-551,958` | `ActiveForms` | C → `ActiveStyles` |
| `CharacterScreen.cs:414,553,1167-1169`, `StatsScreen.cs:226-227,317-318,635-637`, `ChestScreen.cs:734-747`, `RosterScreen.cs:306-309`, `VfxPlayer.cs:14` | labels, "ADEPT", "BEST AT", share card, comment | C (copy/rename) |

### Tools / assets

| File | Sites | Class |
|---|---|---|
| `tools/asset-pipeline/build_spec.py:714-733` | generates `icon_form_*` glyphs | A once no screen draws them (§Q8); `icon_skill_*` already specified at `:736+` |
| `tools/asset-pipeline/v2/fxclips.py:28` `FORMS = (…)`, `skillclips.py:2,49` | per-"Form" effect/clip strips | C (term): these are the `ClipKey`/`FxKey` names (`strike, projectile, aura, trap, mark, transformation` + `press, weep, wilt`), which are fine as asset keys; rename the tuple. |
| `tools/asset-pipeline/make_sfx.py:206` | comment | C (term) |
| `tools/marketing/make_itch_page.py:403-440` | "Source × Form × Vow" store strip | C (copy) |
| `assets/art/UI/icons/forms/icon_form_*.png` (6) | drawn by FormHexDiagram + WeaveScreen `:1051,1705` | A after §Q8 |
| `assets/art/VFX/<char>_<form>/fx_<char>_<form>_strip8_512.png` | keyed by `FxKey` string | keep (asset keys, not the enum) |

## 7. Liveness and coupling defects found (evidence)

### 7.1 `BuildComposer.SkillPick.SkillId` is never populated in production
`grep -rnE "SkillId:|SkillId =|SkillPick\(" src/ tests/` → every `SkillPick(...)` construction in
`PlayerLoadout.cs:268`, `SoloExpeditionScreen.cs:862,2862`, `WeaveScreen.cs:689,704,775,978,1571` and every
test passes four or five positional arguments; none sets `SkillId`. The branch `BuildComposer.cs:87-92`
("A NAMED SKILL BRINGS ITS OWN KIND") and `:171-172` ("THE ID WINS") are unreachable. The player's chosen
`SkillChoice.SkillId` (`PlayerLoadout.cs:197`) reaches the sim only as `(Form, Passive)`. Built-tested-green,
never runs.

### 7.2 DRAIN/GLUT "No lifesteal" still heals; DRINK's card says 50%
`SkillCatalogue.cs:541-566`: `drain_drink` base `Lifesteal` is 0 (default); THIRST sets `Lifesteal =
HealTuning.Default.TransformationLeech` (0.12); GLUT says "No lifesteal" and sets `Lifesteal = 0f`.
`SoloBattle.cs:1691` heals `dealt * vdef.Lifesteal`; **then** `:1721-1731` `if (FormBehaviour.Heals(form))`
heals `dealt * heal.TransformationLeech` unconditionally for any Transformation-Form skill. So: base DRINK
heals 12% (from the Form), THIRST heals 12%+12% (the "doubles" is the dial stacked on the Form constant),
GLUT heals 12% despite its text. The same Form gate at `:1435` heals WILT's field. The card line `:542`
"heals you for 50% of it" quotes the pre-rework 0.50. Fix is the refactor itself: `BasePower`/`Lifesteal`
on the SkillDef, `Effect == Heal` instead of `Heals(form)`, THIRST → `Lifesteal = 2 × base`. (Not run;
inferred from code — flagged for a test.)

### 7.3 WARDED affix and `LastWaveTopForm` are dead
`Bands.cs:41-48` defines WARDED ("60% less from whichever Form dealt the most damage LAST wave");
`BandCycles.cs:93,99,111` schedules it in three bands. `grep -rn "Warded\|TopForm" src/ tests/` → no other
site. `Bands.cs:155-180` (the affix multipliers) has no Warded case; `SoloBattle.cs` contains no `Affix.`
read at all; `SoloExpedition.LastWaveTopForm` (`:86`, `private set`) is never assigned. Players see a band
called WARDED that does nothing. Decision: implement on Style (needs a per-Style damage tally in
`WaveMetrics` and a resist read in `LandOn`) or delete the affix and its three band slots.

### 7.4 `SaveGame.Affinity` is a dead field
`SaveGame.cs:158-159` `public string Affinity { get; init; } = "";` "Legacy — the Mastery tree owns it now".
`grep -rn "Affinity" src/ResonanceHunter.Game/Game1.cs src/ResonanceHunter.Core/Persistence/*.cs` → only the
declaration and `Game1.cs:2769 _weave.Discipline = _mastery.Affinity()` (unrelated). Never written, never read.
Safe to delete (System.Text.Json ignores unknown members on load by default — verify the serializer options
in `SaveSystem` before relying on it).

### 7.5 `Weaving.AbilityPower` has no runtime caller
`grep -rnE "Weaving\.AbilityPower" src/` → none. Test-only (`WeavingTests.cs:207-265`).

### 7.6 `EquippedSkill.Name` / `WovenAbility.Name` has one reader
`Build.Unweave(string abilityName)` (`Build.cs:468`) — a name-keyed removal ("BODY STRIKE"). With SkillId on
the skill this becomes `Unweave(skillId)`.

### 7.7 Two clocks for the same rule (already noted by the code, still present)
`EquippedSkill.CooldownMs` is seeded from `FormBehaviour.BaseCooldownMs(s.Form)` (`BuildComposer.cs:179-180`)
and read for timed actives (`SoloBattle.cs:1484`) and traps (`:1908`), while beat-counted actives read
`sk.Def.Beats` (`:1453`) and the opening breath reads `FormBehaviour.CooldownBeats(f0)` (`:528`). Three
sources for one cadence; `skill_slot_kinds_test:597-609` exists only to assert they agree.

## 8. Serialization risks and migration

| Field | Risk | Migration |
|---|---|---|
| `SavedSkill.Form` (string enum name, `SaveGame.cs:297`) + `SavedSkill.Passive` (`:308`) | The skill identity is `(Form name, Passive?)`. It is lossless **only because** every Style has exactly one Active and one Passive skill; a third skill per style, or a Field that is not the style's passive, would be unrepresentable. `Form.Strike` + `Passive=null` on an old save resolves via the spill rule (`BuildComposer.SlotKinds:93-107`). | Add `SavedSkill.SkillId` (nullable). On load: `SkillId ?? SkillCatalogue.Resolve(Enum.Parse<Form>(Form), passiveDecidedBySlotKinds).Id` — i.e. today's exact path, run once, then persist `SkillId`. Keep `Form` as a read-only legacy member for one release; keep `Resolve(Form,bool)` and `LegacyForm` **only** inside the migration class. Fixture: `SaveSystemTests:320-342`, `share_codes_test:60-159`. |
| `ShareCodes` `RHB1` payload (`ShareCodes.cs:48,226`) serialises `List<SavedSkill>` | Codes in the wild carry `"Form":"Strike"`; the validator caps `Form.Length ≤ 40`. | Same migration; accept both `Form` and `SkillId` in `RHB1`, or bump to `RHB2` emitting `SkillId` and keep the `RHB1` reader. |
| `SaveGame.Affinity` (`:159`) | Dead string. | Delete; confirm the deserializer tolerates unknown members. |
| `BattleEvent.Amount = (int)Form` for `Skill`/`Aura` events (`WaveModel.cs:279-293`) | Not persisted (replay is in-memory), but the **presentation contract** is an enum ordinal. Any Style/SkillId ordinal reuse changes what the screen draws. | Change payload to slot index (the loadout is known to the screen) or a SkillId ordinal from `SkillCatalogue.All`; update `WaveReplay:162-306` and `SoloExpeditionScreen:1254,3277-3280`. |
| `MasteryTaken` node ids `spec_strike…spec_transformation` (`MasteryCatalog.cs:434-446`) | Ids embed Form names but are opaque strings; keep the ids, change `MasteryNode.Form` → `Style`. `RestoreTaken` validates ids only (`MasteryTree.cs` remark `:249-251`). | No migration if ids are kept. |
| `SavedSkillProgress` keyed by `SkillId` (`SaveGame.cs:150-156`) | Already id-based. | None. |
| `Character.Aptitude` roster data | Not persisted. | None. |

## 9. Recommended target shape (what moves where)

1. **`SkillDef` gains `float BasePower`** (per-hit for Active/Reaction, per-tick for Field), seeded from
   today's effective values: Strike 500 (BLOW), Projectile 215 (SPRAY), Trap 290 (JAWS fallback), Mark 0
   (CALL), Transformation 260 (DRINK), Aura 12/s (MIRE); the six null-`LegacyForm` skills get **measured**
   values instead of the spill formula (`SoloBattle.cs:1402-1420`) — the session note already records
   PULSE at Aura's 12 is broken. `SourceScalingCoefficient` moves beside it. `Weaving.BasePower` /
   `FormBehaviour.BaseDamage` / `WeavingTuning.FormBaseValue` delete.
2. **`EquippedSkill(SkillDef Def, Source Source, Vow? Vow, Variation, Reinforcements)`**; `WovenAbility`
   deletes; `Passive => !Def.TakesABeat`; `Build.Affinity : Style?`; `Build.Unweave(skillId)`.
3. **`Builds/Vows.cs`** (`VowKind`, `VowDemand` with `SingleStyle`, `BareSlot`, `Vow`, `VowTuning`,
   `Vows.Catalog/ById/IsActive/Multiplier`, `WeaveContext{DistinctStyles,…}`) — pure move.
4. **`SourceMatchup`** (`Effectiveness(Source, Source)`, `Strong/WeakMultiplier`) — pure move.
5. **`StyleAffinity`** in Builds: `Factor(Style affinity, Style skill, bool vowSworn)` on
   `SkillCatalogue.RingDistance`; `MasteryNode.Style`, `MasteryTree.Affinity(): Style?`.
6. **`SkillShape.StylePower / StyleTargets`**, `Character.Aptitude: Style?`, `ItemFamilies.FavouredStyles`,
   `EnchantNeed.Style`.
7. **SIGN dials own their base**: `sign_call.AmplifyMs = 4 beats`, `AmplifyPercent = 0.6` (today's
   `MarkMultiplier 1.6` expressed as the bonus the loop already adds at `:786`), `sign_brand` likewise;
   `FormBehaviour.MarkWindowMs/MarkMultiplier` delete.
8. **Fight loop asks the skill**: `Def.Kind`, `Def.Effect`, `Def.Style == Style.Hammer/Volley` at
   `:1551,1601,1613` (or, better, dials the trigger sets), `Def.BasePower`, `Def.Targets`; events carry the
   slot; `FormBehaviour.cs` deletes (clip constants → an animation tuning type).
9. **Presentation**: screens read `Def.Name/Line/ClipKey/FxKey/Beats/Style`; `FormHexDiagram` → Style
   hexagon; `PlayerLoadout.SkillChoice(SkillId, Source, VowId)`; `CycleForm/SetForm` and the hover-DPS
   Form-swap preview delete.
10. **Docs**: mark `resonance-weaving-system.md` and `vow-condition-tracking.md` SUPERSEDED (pointing at
    `vows.md`, `skill-slots-and-skill-trees.md`, and a new short `sources.md`); fix `systems-index.md`.

## 10. Open questions

- Should `VowDemand.SingleForm` become `SingleStyle` (one discipline, two skills allowed) or
  `SingleSkillKind`? SingleStyle preserves every currently-savable build's verdict; the designer should
  confirm the reading of "GO DEEP, NOT WIDE".
- The six null-`LegacyForm` skills (PRESS, REPAY, BRAND, WEEP, PULSE, WILT) need **authored** `BasePower`
  values — the spill formula was never a design, and the bench already shows PULSE net-negative. Who tunes
  them, and against which sweep (`BalanceSweepTests`, `slot_split_balance_test`)?
- WARDED: implement on Style or delete? It is the only content that asks "which Style dealt the most", and
  nothing tallies damage per Style today.
- Should Overdraw/Execute/Coiled/Linger/Radiance/Siphon stay Style-gated triggers, or become dials the
  trigger sets on the matching `SkillDef` (`ExtraCasts`, `ExecuteMultiplier`, `CooldownMultiplier`,
  `AmplifyMs ×`, `IntervalMs ×`, `Lifesteal ×`)? The dial form removes the last content-ID branches from
  `SoloBattle` but touches the enchantment liveness tests.
- The DRAIN heal stacking (§7.2): is THIRST meant to be "2 × base" (24%) — the current effective number — or
  was the double-count accidental? The heal ceiling (`HealTuning`) masks part of it.
- `SaveGame` deserializer options: does `SaveSystem` use `JsonSerializerOptions` that reject unknown
  members? If so, removing `Affinity`/`Form` needs a two-step release. (Not checked.)
- Clip/effect asset keys (`strike`, `projectile`, `aura`, `trap`, `mark`, `transformation`) are Form
  names but are opaque strings on `SkillDef.ClipKey/FxKey` and on ~40 PNGs; renaming them is cosmetic and
  costly. Recommend leaving the asset keys as they are.
