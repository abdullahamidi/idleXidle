# Audit — Skills and the combat loop (2026-08-31)

Branch `feat/hunter-cutout-rig`, HEAD `ae30f2a`. Read-only audit. Every line reference below was read in
this pass; every "dead"/"no consumer" claim carries the grep that produced it (bin/ obj/ .git/ excluded).

Files read in full: `src/ResonanceHunter.Core/Builds/{SkillCatalogue,SkillShape,SkillProgress,Build,
BuildComposer,FormBehaviour,SoloBattle(2149 lines),SoloExpedition,Keystones,HealTuning,DamageBench,GearShape}.cs`,
`Combat/AttackBias.cs`, `Expeditions/WaveModel.cs`, `Weaving/ResonanceWeaving.cs`; partial: `Expeditions/WaveReplay.cs`,
`Game/SoloExpeditionScreen.cs` (event decode + FxFor), `Game/PlayerLoadout.cs` (SetSkill), `MasteryCatalog.cs`
(specialisations), `CharacterRoster.cs`, `ElementSets.cs`, `Enchantments.cs`, `SaveGame.cs`, the Builds test folder
(names + two bodies).

---

## 0. Headline

The runtime skill model is **two models stacked**: the twelve `SkillDef`s (STYLE → SKILL → VARIATION →
REINFORCEMENT, all as data deltas) sit on top of the legacy `Form` machinery, and the fight loop
(`SoloBattle.ResolveWave`) still asks **Form** for every number the new model did not explicitly override:
base damage (`FormBehaviour.BaseDamage`, SoloBattle.cs:1573/1420/1926/1707), the amplify/heal forks
(`IsAmplifier`/`Heals`, :1376/1503/1435/1721), the affinity hexagon (:709-719, :786-788, :1584-1586), target
baselines for Fields and Reactions (`shape.TargetsFor(form)`, :1424/1943/1715), the six Form-combo triggers
(:1551/1601/1613/1261/1382/1507/1909/1730) and the event payload the screen decodes (`(int)Form` in
`BattleEventKind.Skill/Aura`, :1543/1558/1272/1714/1942). `SkillDef.Effect` — the field that should replace
the amplify/heal fork — is read by **nothing** (grep §4). Because six of the twelve skills have no
`LegacyForm`, they borrow their sibling's Form (PlayerLoadout.cs:194-197) and inherit its numbers: **PULSE
casts FormBaseValue[Aura] = 12** while BLOW casts 500 (§10.1), **REPAY/JAWS re-arm at the 8 000 ms Trap fallback**
although their text says 3 s / 6 s (§10.4), and the SIGN skills double-apply the legacy 1.6× mark on top
of their own amplify dials, with the dial never expiring (§10.2). Eighteen `SkillShape` fields are read by
the sim but **set by no production code** — the mastery nodes that fed them were deleted and the sim
branches (and the tests pinning them) stayed (§2.2). The loop itself is deterministic (integer-ms ticks,
one seeded `rng` draw per Harvest clear, expected-value crit) and headless; there is **no sim-driven
fast-forward** — offline income is a separate rate estimator (Game1.cs:770-774) and the live sim's pace is
gated by the replay finishing (SoloExpeditionScreen.cs:1309), so "simulation never waits for presentation"
holds inside a wave and fails at the wave boundary.

---

## 1. The battle loop, mapped

### 1.1 Entry, and what is resolved once per wave (SoloBattle.cs:419-660)

| Step | Lines | What |
|---|---|---|
| `mods = build.Resolve(hunter)` | 441, Build.cs:513-534 | keystones × hunter.Squad*Multiplier × PassiveMods (dust × mastery × character) — BuildMods, all multiplicative |
| `shape = Combine(build.Shape, GearShape.Of(hunter))` | 448 | SkillShape (mastery + character + dust vow power) + gear (weapon family FormPower, element sets) |
| `weaveCtx = DescribeBuild(...)` | 452, 2116-2148 | Vow demand context (DistinctForms/Sources, slots, crit, rate, defence, keystones, bare slots) |
| triggers | 472, Build.cs:583-592 | HashSet of BuildTrigger from keystones ∪ mastery ExtraTriggers ∪ worn enchant kinds |
| charge gating | 475-480 | `chargeLive` only if Rend/Capacitor/Dynamo/Lodestone present |
| resonance stat | 486 | `hunter.ValueOf(ResonanceAffinity) × (1+ResonanceWorth)` |
| venom / harvest magnitudes | 491-502 | from worn enchant magnitude, floor when only a keystone grants |
| opening pause (ReadyAt) | 517-534 | **dead for the current catalogue** — every Active has `Beats>0`, so `CooldownBeats(f0)>0 → continue` for the four with a LegacyForm; PULSE/REPAY borrow Aura/Trap (Beats 0) and get a `ReadyAt` entry that a beat-counted skill never reads (:1472-1473) |
| Fervour/Bulwark/Reverb/Tithe | 536-544 | enchant magnitudes, only paid inside their partner's branch |
| crit → expected value | 549-551 | `critFactor = 1 + chance×(mult−1)` — no rng |
| defenseFactor, fragilityMult | 552-568 | champion mitigation curve; distinct-Vow damage-taken price |
| per-wave skill state | 585-627 | attackBreak, frontBreak, slowTicks, executes, secondBreak, bites, breaks, bankedShield, steadyAmp, bleedRate, bleedShed, bleedFromHits, swingIgnoresArmour, swingLifesteal, markBonus, markDeepen, markWholeWave, slowFactor, takenSinceCast — **all closure locals, unreachable by presentation or save** |
| heal budget | 638-640 | `heal.BudgetFor(MaxHealth, siphon)` = 40% (60% with Siphon) of pool per wave |
| beat | 643-644 | `beatLen = BeatFor(mods.SkillRate×shape.SkillRate)`; `nextBeat = min(beatLen, WaveOpeningMs=700)` |

### 1.2 Time model

Integer milliseconds. `for (ms = TickMs(100); ms <= TickCeilingMs(120 000); ms += TickMs)` (:1198). `abs = since + ms`
is expedition-absolute (`Champion.ElapsedMs`). Two clocks coexist:

- **Beat clock** — `nextBeat` accumulated (`ms >= nextBeat`, :1202; `nextBeat = ms + BeatFor(RateNow()×brisk)`, :1775). On a beat the champion takes exactly one action: first ready Active in slot order, else the swing. `Champion.BeatCount` is the cooldown ruler for Actives (`ReadyAtBeat[i] = BeatCount + beats`, :1473) and is published as `BattleEventKind.Beat` (:1776/:683).
- **Millisecond clock** — Fields tick on `ms % IntervalMs == 0` (:1270), poison bleeds on `ms % 500` (:1222), regen on `ms % 1000` (:1211/1218), Reactions re-arm on `ReadyAt[idx]` in absolute ms (:1911-1912), enemy bites accumulate `nextBite += round(interval×(1+slow))` (:1795-1798).

Frame delta never enters the sim; the beat length is rounded to whole ms (not to the 100 ms tick), so a beat lands on the first tick ≥ `nextBeat` — deterministic drift, not frame-dependent.

### 1.3 Per-tick phases (:1198-1972), in order

1. regen (VITALITY outside ceiling; MENDING inside) :1206-1219
2. poison bleed into the front creature (`LandOn(FirstAlive(), poison×shed×rate, ignoresArmour)`) :1222-1230
3. **skill loop** (:1233-1758) — the three-way fork on `sk.Def.Kind`:
   - `Reaction` → `continue` (answered at the bite) :1245
   - `Field` → own-clock tick (:1260-1270), `Aura` event, then a **dial ladder**: `StunMs` (:1289) → `DefenceBreakPerTick` (:1306, `continue`) → `SlowFraction|SlowPerEnemy` (:1330, falls through) → `AttackBreakPerTick` (:1351, `continue`, SUP heal) → `FormBehaviour.IsAmplifier(form)` (:1376, `continue`) → damage path using `FormBehaviour.BaseDamage(form)` × Vow × perTick × `DamageMultiplier` (:1402-1442)
   - `Active` → beat gate (:1447), cadence in beats (:1453-1474) or the dead ms path (:1476-1495), `acted = true`, RHYTHM/OPENING VOLLEY bookkeeping, then `IsAmplifier(form)` branch (:1503-1545) else the cast loop (:1550-1757): casts = Echo?2:1 (+1 Overdraw for Projectile), `Skill` event once, per cast: raw (REPAY bank or `BaseDamage(form)`) × Vow × first/later cast × affinity buy-back × Spirit prime × REND × Execute trigger; ASSASSINATE; variation dials (`DamageMultiplier`, GLUT, THRONG, SPLAY, SHARE); FINISH execute; BANKED shield (`continue`); `LandSpread`; bleedFromHits; `Lifesteal`; WEAVER echo; `Heals(form)` leech; Mind/Nature/Spirit signatures; CHARGE
4. **swing** if `onBeat && !acted` (:1761-1769): `tuning.AutoAttackDamage × hunter.AutoDamageMultiplier × shape.AutoAttackDamage`, 1 target, no Source/Form → none of the skill-tree multipliers
5. beat settle (:1770-1777)
6. **enemy bite** if `ms >= nextBite` (:1794-1971): sum living creatures' Damage (front/second break), × attackBreak × defenseFactor × fragility × DamageTaken × absorb − flat; FORTIFY; REPAY bank; BANKED shield eats; `EnemyStrike` event; PAYBACK/REBOUND; THORNS; DYNAMO; **Reactions** (`Kind==Reaction && On==Bitten`, :1904) — JAWS reflect / IRON refund; death → UNDYING or `Down`/Wiped
7. stall at ceiling → `Stalled` (:1976)

`Kill(ms)` (:679-700) settles the owed beat, SECOND WIND, Harvest rng, Lodestone, Splinter quality, `Finish`.

### 1.4 Damage landing (`LandSpread` :1085-1161 → `LandOn` :929-1079)

`LandSpread`: activation metrics, CASCADE, RALLY/PAYBACK spend, then per living creature (spawn order) `hit = raw × Amp(abs, source, form, c)`, `SignatureAmp`, `LandOn`, `SignatureLay`; CHAIN/RICOCHET extra hit; FEEDBACK; LEECH.
`LandOn`: crit (skill only), venom feed (raw, skill only), metrics.Raw, OVERWHELM gate, flat signed armour (ArmourPenetration, CRUSH, `MinHitFraction` floor, negative armour adds), metrics.Delivered, health −=, SUNDER, STAGGER, `Strike` event (`FromSkill: !swing`), on death: alive--, CASCADE/RALLY arm, `EnemyDown`, WEEP bleed, LOOSE AGAIN, MOMENTUM, BREAKER spill.

---

## 2. Dial → consumer table

### 2.1 SkillDef (SkillCatalogue.cs:155-230)

| Dial | Consumer (SoloBattle.cs unless noted) | Note |
|---|---|---|
| Id | SoloExpedition.cs:359 (`prog.RecordWave(sk.Def.Id)`) | + BuildComposer taught-gate :178 |
| Name, Line, Variations | UI only (Game: 12/3/2 files) | never in sim — fine |
| Style | **not read by the sim** (`grep -c "\.Style\b" SoloBattle.cs` = 0); Game: PlayerLoadout.cs:195, WeaveScreen.cs:694 | the "identity axis mastery reads" — but `Build.Affinity` is `Form?` (Build.cs:441) |
| Kind | :1242 fork, :1904 reaction gate; Build.cs:229 `TakesABeat` | live |
| **Effect** | **none** — `grep -rn "SkillEffect\b\|\.Effect\b" src tests` → only `UnlockEffect` hits and SkillCatalogue.cs itself | the sim forks on `FormBehaviour.IsAmplifier/Heals(form)` instead |
| Beats | :1453 cadence; Build.cs:429 BeatDemand; BuildComposer.cs:179 seeds CooldownMs | live |
| IntervalMs | :1260 field tick; FormBehaviour.cs:114 | live |
| On | :1904 (`== ReactionOn.Bitten` only) | `ReactionOn.Kill/LowHealth/WaveStart` never dispatched (WEEP's on-kill is the `BleedOnKillFraction` dial scanned in LandOn :1037-1044) |
| Targets | :1654 cast path only (`shape.TargetsFor(form, vdef.Targets)`) | **Field path uses `shape.TargetsFor(form)` = Form table (:1424); Reaction path `TargetsFor(Form.Trap)` (:1943)** |
| ClipKey | **none in src** (`grep -rn ClipKey src` → catalogue only; tests skill_catalogue_test.cs:254, skill_slot_kinds_test.cs:53) | presentation key nothing renders |
| FxKey | SoloExpeditionScreen.cs:3356 via `SkillCatalogue.Resolve(form, passive).FxKey` | resolved through Form, not the equipped Def |
| LegacyForm | SkillCatalogue.cs:653 `Resolve`; PlayerLoadout.cs:194; BuildScreen/WeaveScreen | migration bridge |
| DefenceBreakPerTick/Floor | :1303/1306/1318 | PRESS |
| SlowFraction, SlowDeepenPerTick, SlowCeiling, SlowPerEnemy | :1330-1348 → `slowFactor` → :1798 | MIRE |
| AttackBreakPerTick/Floor, FrontEnemyOnly, BreakSecondEnemy, HealPerPulse | :1351-1373 → :1810-1823 | WILT |
| BleedOnKillFraction | :1040 (any kill) | WEEP |
| PaysBackDamageTaken | :1566-1569 cast, :1852 bank | REPAY — **no 3 s window for VENGEANCE** |
| DefenceIgnore | :1683 | FLATTEN |
| ExecuteFraction, ExecutesPerWave | :1664-1670 | FINISH/TWICE |
| StunMs | :1289-1304 (`nextBite +=`, shares `staggeredThisBite`) | PIN |
| ShieldInsteadOfDamage | :1674-1679 → `bankedShield` :1856 | BANKED — emits `Shield` with Amount = health |
| ReflectFraction, ReflectGrowthPerBite/Cap, StopsWholeBite | :1920-1941 | JAWS |
| AmplifyPercent | :1389 (field depth), :1528-1531 (cast) | **only ever set on variations, base BRAND has none** |
| AmplifyMs | :1518 | SPEND/AFTERGLOW |
| AmplifyPerCast/Cap | :1519-1527 | STEADY — `window = int.MaxValue−abs−1` |
| AmplifyWholeWave | :1398 | SPRAWL |
| AmplifyDeepenPerTick/Cap | :1390-1396 | ETCH |
| BleedRate, BleedCarriesWaves, BleedFromHits | :603-612 gathered from the whole weave; :1224, :1687 | VOLLEY |
| DamagePerLivingEnemy, SplitPool, SplitMaxWays, MinimumHits | :1643-1660 | FIELD/VOLLEY |
| Lifesteal, DamagePerHealth | :1691, :1639 | DRINK — base DRINK's Lifesteal is 0; heal comes from `Heals(form)` :1721 |
| DamageMultiplier | :1636 cast, :1423 field | not applied to Reactions |
| CooldownMultiplier | :1908 Reactions **only** | no Active in the catalogue uses it |
| TargetsBonus | :1312 PRESS, :1424 field, :1654 cast | live |
| SwingIgnoresArmour, SwingLifesteal | :616-622 → :1765-1768 | TRAIL/TRICKLE |

### 2.2 SkillShape (SkillShape.cs) — read-site in the sim / production set-site

Read but **never set in production** (`grep -rn "\bX\b" src tests` excluding SkillShape.cs/SoloBattle.cs — hits are tests only or nothing):

| Field | Sim read | Producer | Pinned by |
|---|---|---|---|
| ArmourIgnoreFraction, CrushArmourMultiple | :982-984 | none | — |
| OverwhelmFloor | :956-964 | none | SkillShapeBattleTests.cs:140, MasteryTreeTests.cs:375 |
| SunderThreshold, SunderAmount | :1007-1008 | none | SkillShapeBattleTests.cs:168 |
| FreshThreshold, FreshBonus | :835-836 | none | — |
| StaggerThreshold, StaggerMs | :1012-1016 | none | mastery_new_nodes_liveness_test.cs:77 |
| StrikesEveryCreature | SkillShape.cs:438 via TargetsFor | none | SkillShapeBattleTests.cs:183 |
| ChainFraction | :1132 | none | SkillShapeBattleTests.cs:197 |
| RicochetChance, RicochetFraction | :1133-1134 (the loop's second rng draw) | none | — |
| CascadeOnKill | :1024, :1096 | none | — |
| NextSkillAfterKillBonus | :1026, :1101 | none | — |
| RatePerCreature | :665 | none | — |
| VsArmouredBonus, VsOtherPenalty | :839-842 (the only `Archetype` read) | none | Encounters/AffixLivenessTests.cs:58 |

`grep -c '"CRUSH\|"OVERWHELM\|"SUNDER\|"HEADLONG\|"STAGGER\|"EVERYWHERE\|"CHAIN\|"RICOCHET\|"CASCADE\|"RALLY\|"TIDE\|"SIEGE' MasteryCatalog.cs` = 0 for each — the nodes are gone, the sim code stayed. SkillShape.cs:21-23 states the opposite rule ("if a field stops being read, its nodes must be deleted") and no rule for the inverse.

Live fields (producer in MasteryCatalog/ElementSets/CharacterRoster/DustEffects/GearShape): HitSize, VowPowerMultiplier, StrongMatchupBonus, WeakMatchupRelief, AllMatchupsStrong, AffinityStyleBonus, OffStylePenalty, OppositePenaltyRelief, ResonanceWorth, OneSourceBonus, Haul* (SoloExpedition.cs:444-474), RarityFromClean (:509), ArmourPenetration, OverkillCarry (CharacterRoster.cs:79), ExtraTargets (MasteryCatalog.cs:411), FormTargets (:439), FormPower (GearShape.cs:382, Character.cs:208), CooldownRefundOnKillMs (ElementSets.cs:99), FirstHit/LaterHitMultiplier, Cull*, PerCreatureBonus (CharacterRoster.cs:93), Opening*, AfterOpeningPenalty, InterruptBonus, DamagePerMaxHealth, HitSizePerMaxHealth, DamageDealt, SkillRate, FreeOpeningCast, BonusCritPercent, MarkWindowMultiplier, MarkPowerBonus, AssassinateThreshold, AutoAttackRate, AutoAttackDamage, SourceBonus, CastRamp*, First/LaterCastMultiplier, Leech, HealPerTargetStruck, FlatDamageReduction, DamageTaken, AbsorbAtLowHealth, FirstBiteFree, ReflectFraction, HealOnClear, RegenFraction, BiteFuel*, HealOnBiteFraction, BetweenWaveRegen, FullHealBetweenWaves, MaxHealth.

### 2.3 BuildMods

Damage :705 (Amp root) · Health → `ChampionHealth` :2056 only (`mods.Health` inside ResolveWave appears in a comment only) · SkillRate :530/643/664 · Haul :818 (HOARDER), SoloExpedition.cs:418 · Rarity SoloExpedition.cs:508. All live.

### 2.4 BuildTrigger (22 values) — all read: Splinter :697, Harvest :692, Venom :491, Desperation SoloExpedition.cs:476, Undying :1955, NoHealing :1169 + SoloExpedition.cs:371, Echo :763/1550, Bloodlust :745, Zeal :755, Rend :475/1601, Capacitor :476/479, Dynamo :477/1895, Lodestone :478/695, LooseAgain :1049, Overdraw :1551, Linger :1382/1507, Radiance :1261, Execute :1613, Coiled :1909, Siphon :639/1438/1730, Hoarder :817, Weaver :1702. None dead; six are Form-keyed (§3).

### 2.5 Champion / WaveCreature / WaveMetrics

Champion: MaxHealth, Health, Alive, ReadyAt (Reactions + LooseAgain/Momentum), ElapsedMs, UndyingSpent, **MarkUntilMs (cross-wave, never reset)**, BeatCount, ReadyAtBeat — all read. `SoloExpedition.LastWaveTopForm` (SoloExpedition.cs:86) is **never assigned and never read** (`grep -rn LastWaveTopForm src tests` → the declaration only) — dead.
WaveCreature: Damage is init-only so WILT is held as a loop factor (:585-586) rather than on the creature; Defense is mutable (PRESS/SUNDER/MACHINE); no slow/stun/mark state on the creature.
WaveMetrics: consumed by RunReport/RunLog/SaveGame/SoloExpeditionScreen — live.

---

## 3. Legacy dependencies in the loop (Form / WovenAbility / FormBehaviour / EquippedSkill.Ability)

Classification: **A** delete · **B** keep only for save migration · **C** replace with Style / SkillKind / SkillEffect / typed dial.

| Where | What | Class | Replacement |
|---|---|---|---|
| Build.cs:206 `EquippedSkill(WovenAbility Ability, int CooldownMs, bool? PassiveSlot)`; :252-263 `Def` resolved via `SkillCatalogue.Resolve(Form, Passive)` | the identity of an equipped skill is (Form, Passive) round-tripped | C | `EquippedSkill(SkillDef Def, Source, Vow?, Variation, Reinforcements)`; `SkillId` stored (GDD §11 stage 1 promised `WovenAbility.SkillId`; ResonanceWeaving.cs:257-263 never got it) |
| Build.cs:219-220, BuildComposer.cs:102-103 | passive fallback via `FormBehaviour.IsPassive/FiresOnBeingHit(Form)` | C | `!Def.TakesABeat` |
| Build.cs:441 `Form? Affinity`; MasteryTree.cs:253 `Affinity()` from specialisation `MasteryNode.Form` | affinity typed on Form | C | `Style?` using `SkillCatalogue.RingDistance` (exists, **test-only today**: `grep -rn RingDistance src` → catalogue only) |
| SoloBattle.cs:522-528 `skills[i].Form`, `FormBehaviour.CooldownBeats(f0)` | opening pause | A | block is unreachable for beat-counted Actives (every Active has Beats>0) |
| :709-719, :786-788, :1584-1586, :1711-1713 `FormBehaviour.AffinityFactor(...)` | Form hexagon multiplier, Mark affinity, Vow buy-back | C | Style ring |
| :729 `Weaving.SourceEffectiveness` | Source matchup | keep | current concept |
| :786 `FormBehaviour.MarkMultiplier`, :1507 `MarkWindowMs` | legacy flat 1.6× / 6 000 ms mark | C | base `AmplifyPercent/AmplifyMs` dials on CALL and BRAND (today only variations set them) |
| :1236 `form = sk.Form`; :1260 `AuraTickMs` fallback | | A | every Field has IntervalMs>0 → fallback dead |
| :1376, :1503, :1705 `IsAmplifier(form)` | amplify fork | C | `Def.Effect == SkillEffect.Amplify` (field is declared, unread) |
| :1435, :1721 `Heals(form)` + `heal.TransformationLeech` | base lifesteal | C | DRINK base `Lifesteal` dial; `Effect == Heal` |
| :1412 `BaseCooldownMs(form)` per-tick scaling | spilled-active scaling | A | only MIRE reaches the Field damage path (PRESS/WILT `continue`, BRAND amplifies); both branches of `castMs>0 ?` yield the same value for MIRE |
| :1420, :1573, :1707, :1926 `FormBehaviour.BaseDamage(form, resonance, wt)` → `WeavingTuning.FormBaseValue[Form]` | **base damage is a Form lookup** | C | `SkillDef.BaseDamage` dial (the one number the new model never got) |
| :1543 `(int)Form.Mark`, :1558/1272/1714 `(int)form`, :1942 `(int)Form.Trap` in `BattleEvent.Amount` | event identity by Form | C | slot index or skill id in the event |
| :1551 `form == Form.Projectile && Overdraw`, :1601 `form == Form.Strike && Rend`, :1613 `form == Form.Strike && Execute` | Form-gated triggers | C | Style-gated (`Def.Style == Style.Volley/Hammer`) or fold into reinforcements |
| :1702-1719 WEAVER: `skills[(i+1)%n]`, `IsAmplifier/FiresOnBeingHit(woven.Form)`, `BaseDamage(woven.Form)`, `TargetsFor(woven.Form)` | echo as "next Form" | C or A | one keystone, Form-native; `FiresOnBeingHit(Trap)` misclassifies REPAY (Active with Form Trap) as a trap |
| :1424 `shape.TargetsFor(form) + def.TargetsBonus`, :1943 `TargetsFor(Form.Trap)`, :1715 `TargetsFor(woven.Form)` | Form table target baseline | C | `Def.Targets` everywhere (cast path already does :1654) |
| :811 `shape.FormPowerFor(form)`; SkillShape.cs:187/202 `FormTargets/FormPower`; GearShape.cs:379-383 weapon favoured Forms; Character.cs:118/206 Aptitude | per-Form multipliers | C | per-Style |
| :2133 `WeaveContext.DistinctForms` → `VowDemand.SingleForm` ("ONE FORM ONLY") | Vow demand on Form | C | DistinctStyles |
| SoloExpedition.cs:212-213 `old[i].Ability.Form != now[i].Ability.Form` | cooldown reset on build swap | C | compare `Def.Id` |
| Enchantments.cs:106/175-180 `EnchantNeed.Form`, `NeedsForm` | Form-combo enchant requirement | C | Style/Kind |
| SkillCatalogue.cs:169 `LegacyForm`, :646-655 `Resolve(Form, bool)`; SaveGame.cs:297 `SavedSkill.Form` string; PlayerLoadout.cs:194-197 | save bridge | B | keep in a `SaveMigration` that maps (Form, Passive) → SkillId once, then delete |
| FormBehaviour.cs `CastClipMs, ClipShareOfBeat, SkillClipShareOfBeat` (:194-223) — used by Game only | presentation timing in Core | C | move to Game |
| BuildGlossary.cs:29-80 `FormHeadline/FormRule` | UI text from Form | A/C | text from SkillDef.Line |

`grep -rn "\.Ability\b" src` outside Build.cs → only SoloExpedition.cs:212-213. WovenAbility's last real job is carrying Form+Source+Vow into EquippedSkill.

---

## 4. Dead fields and dead paths (evidence)

1. `SkillDef.Effect` / `enum SkillEffect` — declared SkillCatalogue.cs:83-88, :160; `grep -rn "SkillEffect\b\|\.Effect\b" src tests --include=*.cs` → SkillCatalogue.cs only (+ unrelated `UnlockEffect`). **Zero consumers.**
2. `SkillDef.ClipKey` — `grep -rn ClipKey src` → SkillCatalogue.cs only. Tests assert it is non-empty (skill_catalogue_test.cs:254) and equal across a style's pair (skill_slot_kinds_test.cs:53) — pinned but unused.
3. `ReactionOn.Kill, LowHealth, WaveStart` — `grep -n "ReactionOn\." SoloBattle.cs` → :1904 (`Bitten`) only.
4. 18 SkillShape fields with no producer (§2.2 table).
5. `SoloExpedition.LastWaveTopForm` — declared :86, no assignment, no reader.
6. `SkillCatalogue.NeedsUnlock` always `true` (:637-641); `Taught == All` (:644).
7. `SkillCatalogue.RingDistance/Opposite` — `grep -rn "RingDistance\|Opposite(" src` → catalogue only; tests skill_catalogue_test.cs:148-160. The Style ring exists but the fight reads the Form ring.
8. SoloBattle.cs:517-534 opening-pause `ReadyAt` write and :1476-1495 time-counted Active cooldown — unreachable: all six Actives and every variation have `Beats ∈ {4,5,6}`.
9. SoloBattle.cs:1270 `ms == 0 ||` — the loop starts at `ms = TickMs` (:1198); the comment (:1262-1269) describes a loop that no longer exists.
10. SoloBattle.cs:1431-1440 Field leech block (`Heals(form)` on a Field) — WILT is the only Transformation-Form Field and `continue`s at :1373 before reaching it.
11. SoloBattle.cs:1416-1418 `: ownTick / 1000f` branch — unreachable (MIRE has `castMs = 1000 > 0`).
12. `EquippedSkill.CooldownMs` — read at :529 (dead), :1483 (dead), :1908 (Reactions, live). Effective only for JAWS.
13. `SkillDef.CooldownMultiplier` — only RECOIL/BLUNT (Reaction) set it and :1908 is the only read; correct but narrow.

---

## 5. Hardcoded branches

No `switch(skill.Id)`, no character-id, no keystone-id string in the loop (`grep -n '== "\|case "\|\.Id ==' SoloBattle.cs SoloExpedition.cs` → none; Build.cs:495/502 and BuildComposer.cs:147 are id lookups, not behaviour). What does branch on identity:

| file:line | keys on | note |
|---|---|---|
| SoloBattle.cs:1551 | `Form.Projectile` | OVERDRAW extra cast |
| :1601 | `Form.Strike` | REND spender |
| :1613 | `Form.Strike` | EXECUTE trigger |
| :1376, :1503, :1705 | `Form.Mark` via `IsAmplifier` | amplify fork — should be `Effect` |
| :1435, :1721 | `Form.Transformation` via `Heals` | base leech — should be `Effect`/dial |
| :1705 | `Form.Trap` via `FiresOnBeingHit` | Weaver skip |
| :906, :913, :915, :1429, :1540, :1590, :1738, :1747, :1750, :1947 | `Source.{Shadow,Body,Machine,Nature,Spirit,Mind}` | six per-Source "signature" behaviours hardwired at ten sites |
| :840 | `Archetype.Armoured` | SIEGE — no producer, dead |
| :1543, :1942 | `(int)Form.Mark`, `(int)Form.Trap` | event payload |
| Build.cs:219-220; BuildComposer.cs:102-103 | `IsPassive(Form)||FiresOnBeingHit(Form)` | slot kind fallback |
| SoloExpedition.cs:104-109 | `AttackBias` enum | region tempo — semantic, fine |
| SoloExpedition.cs:212-213 | `Ability.Form/Source` equality | build-swap cooldown reset |
| PlayerLoadout.cs:194-197 | SkillId → sibling's LegacyForm | the round trip the whole bridge depends on |
| SoloExpeditionScreen.cs:1254, :1268, :1270; WaveReplay.cs:172, :303 | `(Form)e.Amount`, `== Form.Trap` | presentation decodes Form |
| Enchantments.cs:175-180 | `Form.*` | enchant "needs" |

---

## 6. Determinism, headlessness, fast-forward

- **Headless**: `ResonanceHunter.Core.csproj` has no package or project references; `grep -rln Microsoft.Xna src/ResonanceHunter.Core` → Rig.cs:7 (a comment). Clean.
- **Randomness**: `grep -n "rng\." SoloBattle.cs` → :692 (Harvest proc at clear) and :1133 (Ricochet — dead, no producer). Crit is folded to expected value (:549-551). Composition rng is `new Random(Bands.Seed(RegionId, next, RunIndex))` (SoloExpedition.cs:282). Run rng defaults to `new Random(20260716)` (:143); `grep -rn "DateTime\.\|Random.Shared\|Stopwatch" src/ResonanceHunter.Core` → none. Deterministic given (build, hunter, seed, region, run index).
- **Float state**: `poison`, `slowFactor`, `attackBreak`, `bankedShield`, `steadyAmp`, creature `Health/Defense` are floats accumulated within a wave only; `Champion.Health` is int. No cross-frame float accumulation because there are no frames.
- **Fast-forward**: there is no sim fast-forward. The screen resolves a wave atomically (`_outcome = _run.PushWave()`, SoloExpeditionScreen.cs:883), replays it, and only calls `PushWave` again when `_replay.Finished` (:1309) — the run's real-time pace equals replay pace, so the simulation *does* wait for presentation at wave boundaries. `DevRunToDeath` (:3736-3745) loops `PushWave` — the only stepping-free path, dev-only. **Offline** income is `credited × save.ChampionGleamRate × 0.5` (Game1.cs:770-774), a measured rate, not the sim; by construction it cannot equal stepping.
- **Determinism hazards**: `champ.MarkUntilMs += stretch` (:1741) after STEADY set `MarkUntilMs = int.MaxValue − 1` (:1526/1534) overflows to negative when a Mind-Source skill casts — closes the mark and flips the sign of every later `MarkUntilMs > absMs` test for the rest of the run.

---

## 7. Events, state, and what a presentation layer can rebuild

### 7.1 BattleEventKind (WaveModel.cs:241-256, `BattleEvent(Kind, Slot, Amount, AtMs, FromSkill)`)

| Kind | One-shot / state | Payload | Emitted at |
|---|---|---|---|
| Strike | one-shot | Slot = creature idx, Amount = delivered, FromSkill = !swing (poison, thorns, spill, executes all `FromSkill: true` via `!swing`) | :1020 |
| EnemyStrike | one-shot | Slot 0, Amount = taken after shield | :1864 |
| Down | one-shot | | :1968 |
| Heal | one-shot | Amount = landed | :1195 |
| EnemyDown | one-shot per creature | Slot = idx | :1030 |
| Skill | one-shot | **Slot = (int)Source, Amount = (int)Form** | :1543, :1558, :1714, :1942 |
| Aura | one-shot per tick | Slot = (int)Source, Amount = (int)Form | :1272 |
| **Shield** | state (until AtMs+Amount) | **Amount = ms for UNDYING (:1962, 1 500) but = shield HEALTH for BANKED (:1677)** | WaveReplay.cs:370 `_shieldUntil = AtMs + Amount`; screen :1279-1281 says "UNDYING" for both |
| Charge | state (latest wins) | Amount = pool after | :1542, :1606, :1754, :1898 |
| Beat | state (counter) | Amount = BeatCount | :683, :1776 |
| Break | state | Slot = idx, Amount = break count (not the Defense value) | :1323 |

### 7.2 Persistent in-wave states and whether they are observable

| State | Lives in | Event? | Rebuildable from snapshot? |
|---|---|---|---|
| defence break | `WaveCreature.Defense` (mutable) + `breaks[]` | Break (count) | end-of-wave only via `LastWaveCreatures` |
| attack break (WILT) | locals `attackBreak/frontBreak/secondBreak` | none | no |
| slow (MIRE) | local `slowFactor` | none (only the bite timing) | no |
| stun/stagger | `nextBite +=` | none | no |
| amplify | `Champion.MarkUntilMs` (cross-wave) + locals `markBonus/markWholeWave/steadyAmp/markDeepen` | Skill(Mark) only; window length not published | partially (MarkUntilMs), depth never |
| poison/bleed pool | local `poison` | none (its ticks are Strikes) | no |
| shield | local `bankedShield` / UNDYING | Shield (ambiguous Amount) | no |
| wounds / bent armour (signatures) | locals | none | no |
| charge | local `charge` | Charge | yes |
| REPAY bank, RALLY, PAYBACK, RHYTHM | locals | none | no |
| cooldowns | `Champion.ReadyAt/ReadyAtBeat/BeatCount` | Beat | yes (WaveReplay.BeatAt) |
| health | `Champion.Health`, `WaveCreature.Health` | Strike/Heal/EnemyStrike/Down | yes (WaveReplay) |

The design law "persistent state must be reconstructable" is met for health, cooldowns, charge, breaks; not for slow, attack break, stun, amplify depth, poison, banked shield. The screen infers a Trap's crit-graded blow from timestamps (`trapAtMs`, SoloExpeditionScreen.cs:1233/1270) because provenance is not in the event.

### 7.3 Core ↔ presentation couplings

- WaveModel.cs:306 / SoloBattle.cs:1558: Skill/Aura identity is `(Source, Form)`. Two skills of one style share a Form (PULSE/MIRE → Aura, BLOW/PRESS → Strike, CALL/BRAND → Mark, SPRAY/WEEP → Projectile, DRINK/WILT → Transformation, REPAY/JAWS → Trap). `SkillKey(source, form)` (SoloExpeditionScreen.cs:348) and `WaveReplay.IsCastOf(e, source, form)` (:57-59) collide for a same-Source pair — a PULSE cast flashes MIRE's rail cell.
- Shield event Amount overloaded (§7.1).
- SoloExpeditionScreen.cs:1309 replay gates `PushWave`.
- SoloExpeditionScreen.cs:3356 `SkillCatalogue.Resolve(form, passive).FxKey` — art resolved through the Form bridge rather than the equipped skill.
- FormBehaviour.cs:194-223 `CastClipMs/ClipShareOfBeat/SkillClipShareOfBeat` — animation timing constants in Core, consumed only by Game (`grep -rhon "FormBehaviour\.[A-Za-z]*" src/ResonanceHunter.Game`: ClipShareOfBeat 2, SkillClipShareOfBeat 2).
- SoloBattle.cs:351 `UndyingShieldMs` "how long the shield reads as lit" — presentation duration inside the sim.
- StatsScreen.cs:307 recomputes the swing as `SoloBattle.AutoAttackDamage × AutoDamageMultiplier × mods.Damage`, omitting `shape.AutoAttackDamage` (:1763) — a second formula, already drifting.
- BuildGlossary.cs (Core) formats UI strings from FormBehaviour constants.

---

## 8. Group concepts — how delivery and effects are encoded, and where two encodings do one job

### 8.1 Delivery

| Concept | Encoding(s) |
|---|---|
| self | Heal paths (`Heal()`), BANKED shield, GLUT; no dial says "self" |
| single | `Targets: 1` (SkillDef) |
| front | implicit "first alive in spawn order" (`FirstAlive()`, :876); `FrontEnemyOnly` (WILT), PRESS `want = 1 + TargetsBonus` |
| multi | `Targets: N` + `TargetsBonus` + `shape.ExtraTargets/FormTargets` (`TargetsFor`) |
| wave | `Targets: WholeWave` (SkillDef) **and** `StrikesEveryCreature` (SkillShape, dead) **and** `FormBehaviour.Targets(Aura)=MaxValue` (Field path) **and** `AmplifyWholeWave`/`markWholeWave` (amplify scope) **and** `SplitMaxWays=99` (SHARE) |
| repeated / multi-hit | `Echo` trigger (×2 casts), `Overdraw` (+1), `MinimumHits` (raw × N/alive), `casts` loop — **no notion of "k hits on one target"**: CLUSTER "all 5 arrows hit one enemy" = `Targets=1` = one hit of 215 (:1448/:1654) |
| field / over time | `Kind.Field` + `IntervalMs` (skill) vs Field damage path using Form base ÷ cast length |
| chain | `ChainFraction`/`Ricochet*` (dead), `OverkillCarry` (BREAKER spill), `Weaver` (echo into next slot) |
| reaction | `Kind.Reaction` + `On.Bitten` (JAWS) vs WEEP's on-kill implemented as a dial scan in `LandOn` |

### 8.2 Effects — duplicated responsibilities (each row is two or more encodings of one verb)

| Verb | Encodings | Lines |
|---|---|---|
| **amplify** | legacy flat `FormBehaviour.MarkMultiplier`(1.6)+`MarkPowerBonus` while `MarkUntilMs` open; `markBonus`(+`markWholeWave`) from `AmplifyPercent`; `steadyAmp`; `markDeepen`; `Linger` ×9/5; `MarkWindowMultiplier`; Mind signature `+250 ms` | :784-801, :1381-1398, :1507-1534, :1738-1743 |
| **heal / leech** | `shape.Leech` (LandSpread), `SkillDef.Lifesteal` (DRINK), `Heals(form)`×`TransformationLeech`(+Siphon), `SwingLifesteal`, Nature signature (three sites), `HealPerPulse`, `HealPerTargetStruck`, `HealOnBiteFraction`, `RegenFraction`, `HealOnClear`, VITALITY regen — one funnel `Heal()` (good), eleven producers | :1157, :1691, :1721-1732, :1766, :1429/1747/1947, :1372, :1153, :1870, :1218, :690, :1214 |
| **reflect** | `shape.ReflectFraction` THORNS (all biters, raw bite) vs `SkillDef.ReflectFraction` JAWS (`taken × reflect`, one biter) | :1881-1887 vs :1920-1944 |
| **execute** | `BuildTrigger.Execute` ×1.6 below 30% (Strike only); `shape.AssassinateThreshold` kill once/wave; `SkillDef.ExecuteFraction/ExecutesPerWave` kill N/wave | :1613-1616, :1621-1628, :1664-1670 |
| **stun / stagger** | `shape.StaggerMs` (dead) and `SkillDef.StunMs` both `nextBite +=` behind one `staggeredThisBite` flag | :1012-1016, :1289-1298 |
| **defence break** | `DefenceBreakPerTick` (PRESS), `shape.SunderAmount` (dead), MACHINE signature strip, `ArmourPenetration` (per-hit, not a break) | :1318, :1008, :915-919, :981 |
| **armour ignore** | `DefenceIgnore`, `SwingIgnoresArmour`, `OverwhelmFloor` (dead), `ArmourIgnoreFraction`+`CrushArmourMultiple` (dead), `ignoresArmour` parameter for poison/thorns/spill/executes | :1683, :1765, :956-964, :982-984, :1077/1228/1626/1668/1886 |
| **bleed / poison** | one pool `poison` (good) fed by `venomFrac` (trigger+enchant), `BleedOnKillFraction`, `BleedFromHits`; drained by `bleedShed×bleedRate` — but named VENOM (`VenomBasePoison`, `VenomBleedPerHalfSecond`) while VOLLEY owns it | :489-495, :1040-1043, :1687, :1222-1229 |
| **cooldown** | `ReadyAtBeat` (Actives, beats), `ReadyAt` (Reactions, ms; dead for Actives), `EquippedSkill.CooldownMs` (seeded from `Beats×1500` or `BaseCooldownMs(form)`), `CooldownMultiplier` (Reactions), `Coiled` ×0.5, `RateNow()`, MOMENTUM refunds in both tables | :1453-1495, :1908-1912, :1061-1069 |
| **target count** | `Def.Targets` (cast) vs `FormBehaviour.Targets(form)` (Field/Reaction) vs `TargetsBonus` vs `ExtraTargets/FormTargets` vs `SplitMaxWays` | :1654, :1424, :1943 |
| **base damage** | `WeavingTuning.FormBaseValue[Form]` (six numbers) × `DamageMultiplier` (reinforcements) — no per-skill base | ResonanceWeaving.cs:231-251, :1573 |

---

## 9. Numerical stacking — one skill hit, in evaluation order

`raw` (cast path, :1566-1660):
1. `FormBaseValue[form] × (1 + 0.018 × resonanceStat × (1 + ResonanceWorth))` — or REPAY `takenSinceCast × PaysBackDamageTaken` (:1568/1573; ResonanceWeaving.cs:334-335) [mult]
2. × `VowFactor` = `1 + (VowMultiplier−1) × VowPowerMultiplier` (:1575, :2101-2113) [mult]
3. × `FirstCastMultiplier | LaterCastMultiplier` (:1578) [mult]
4. × affinity buy-back ratio `AffinityFactor(aff, form, vow)/AffinityFactor(aff, form)` (:1585) [mult]
5. × `1 + 0.15` Spirit prime (:1592) [mult]
6. × `1 + 0.05 × charge` REND (:1604) [mult]
7. × `1.6` EXECUTE trigger (:1616) [mult]
8. × `vdef.DamageMultiplier` (:1636) [mult]
9. × `1 + DamagePerHealth × hp%` GLUT (:1640); × `1 + DamagePerLivingEnemy × alive` THRONG (:1643); × `MinimumHits/alive` SPLAY (:1650); × `SplitPool/ways` SHARE (:1658) [mult]

`LandSpread` (:1101-1102): × `1 + NextSkillAfterKillBonus` RALLY; × `1 + BiteFuelBonus × biteFuel` PAYBACK [mult, dead producers for RALLY].

`Amp()` per target (:703-873), all multiplicative:
10. `mods.Damage` = Π keystone.Damage × `hunter.SquadDamageMultiplier` (gear × worn) × PassiveMods (dust × mastery × character)
11. Form affinity 2.0/1.15/0.75/0.45 (+BROAD relief, ×NARROW `1+AffinityStyleBonus` or `1−OffStylePenalty`)
12. Source matchup 1.15/0.87 (CHORD/KEYED/DISCORD bend it)
13. `1 + SourceBonus[source]` (element set rungs, additive across rungs, then one multiplier)
14. `1 + OneSourceBonus` PURE
15. `1 + (0.8 + fervour) × missing` BLOODLUST; `1 + (0.8 + bulwark) × present` ZEAL
16. `1 + reverb` (with Echo); `1 + tithe × swornVows`
17. `(1.6 + MarkPowerBonus) × AffinityFactor(aff, Mark)` while `MarkUntilMs > abs`
18. `1 + markBonus` if whole-wave or target is front
19. `HitSize × DamageDealt`
20. `FormPowerFor(form)` (aptitude × weapon family, multiplicative on combine)
21. `1 + 0.2 × (Haul−1)` HOARDER; `1 + HitSizePerMaxHealth × MaxHealth` ANCHOR; `1 + DamagePerMaxHealth × MaxHealth` BASTION
22. `FirstHitMultiplier | LaterHitMultiplier`; `1 + CullBonus`; `1 + FreshBonus` (dead); SIEGE (dead)
23. `1 + PerCreatureBonus × alive`; FIRST STRIKE `1+OpeningBonus | 1−AfterOpeningPenalty`; `1 + InterruptBonus`

`SignatureAmp` (:901-909): × `1 + 0.03 × wounds` (≤5); × `1.12` Shadow below half.
`LandOn` (:936-990): × `critFactor`; OVERWHELM gate; `max(dmg × 0.15, dmg − armour')` where `armour' = max(0, Defense − ArmourPenetration)` (× `1−ArmourIgnoreFraction` if CRUSH); negative armour adds `min(−armour, dmg)`.

**Additive** anywhere: `SkillShape.Combine` sums bonus fields across nodes (CullBonus, PerCreatureBonus, SourceBonus per source, Haul*, Reflect, Leech…) and multiplies multipliers (HitSize, DamageDealt, SkillRate, MaxHealth, First/Later*); thresholds take the kinder value. Reinforcements are `with` overrides, not sums (SkillCatalogue.cs:105-125, Build.cs:260-261). Everything else is a chain of ~35 independent multipliers — the "multiplicative soup" the design law warns about, with the amplify verb applied twice (17 and 18).

**Bite** (:1810-1863): Σ `creature.Damage × (1+frontBreak | 1+secondBreak)` × `1+attackBreak` × `100/(100+Defense)` × Π`(1+Vow.DamageTakenIncrease)` × `shape.DamageTaken` × `1 − Absorb × missing` − `FlatDamageReduction`; FORTIFY → 0; − bankedShield; IRON refunds.
**Heal** (:1166-1196): × band `sustain`; min(room, budget 0.40×MaxHealth [×1.5 Siphon]); NoHealing veto.
**Pool** (:2036-2061): `max(60, hunter.MaxHealth) × VowHealthMultiplier(build) (× build.Shape.MaxHealth) × GearShape.MaxHealth × mods.Health × 1.8`.
**Cadence**: Actives every `Beats` beats; beat = `max(200, round(1500 / (mods.SkillRate × shape.SkillRate × (1+RatePerCreature×alive) × (1+CastRampPerCast×castRamp))))` (:664-666, :235); swing beat × `AutoAttackRate`; Reactions `CooldownMs × CooldownMultiplier × 0.5(Coiled) / RateNow()`.

---

## 10. Text vs behaviour — liveness defects found while tracing

Each is a catalogue sentence (SkillCatalogue.cs) the sim does not do, found by reading, not by running.

1. **PULSE's base damage is the Aura tick value.** `field_pulse` has `LegacyForm: null` → PlayerLoadout.cs:194-197 gives it MIRE's `Form.Aura` → cast path `raw = FormBehaviour.BaseDamage(Form.Aura)` (:1573) = `FormBaseValue[Aura] = 12` (ResonanceWeaving.cs:249, "a second's worth"). BLOW casts 500 every 6 beats; PULSE casts 12 to the wave every 5 beats; MIRE ticks 12 to the wave every second. No test pins PULSE's output (`grep -rn "PULSE\|field_pulse" tests` → two comments).
2. **SIGN double-applies and never expires.** Amp applies `1.6 (+MarkPowerBonus)` while `MarkUntilMs` is open (:784-790) **and** `1 + markBonus` (:797-801). SPEND "+200% for 2s" → 1.6 × 3.0 for 2 s, then `markBonus` stays 2.0 for the rest of the wave (no reset on expiry — writes only at :1397/1524/1530). Base BRAND "front enemy +70%" sets no `AmplifyPercent` (SkillCatalogue.cs:403-428) → it is the global 1.6× window refreshed every tick. STEADY sets `MarkUntilMs = int.MaxValue−1` (:1526/1534) on the **Champion**, which persists into every later wave (the flat 1.6× outlives "the rest of the wave"), and a Mind cast then overflows it (:1741).
3. **GLUT "No lifesteal" still heals 12%.** `Heals(form)` (:1721) is unconditional for Form.Transformation and adds `heal.TransformationLeech` regardless of `vdef.Lifesteal = 0`. Base DRINK says "50%" but heals 12% (Lifesteal dial is 0 at base; the heal comes from the Form path). THIRST's "your per-wave healing limit doubles" has no dial: `healBudget` reads only the Siphon trigger (:639).
4. **JAWS "rearms every 3s" / IRON "rearms every 6s" both re-arm at 8 000 ms.** BuildComposer.cs:179-180 seeds `CooldownMs = FormBehaviour.BaseCooldownMs(Trap)` = `8_000` fallback (FormBehaviour.cs:114, `IntervalMs` is 0 for a Reaction). IRON sets no `CooldownMultiplier`. RECOIL/BLUNT are relative and work.
5. **VENGEANCE "only damage taken in the last 3s counts"** — `takenSinceCast` accumulates since the last cast with no window (:1852-1853, :1568). Pure upside.
6. **SPRAY "5 arrows … split between fewer enemies" / CLUSTER "all 5 arrows hit one enemy"** — `Targets` is distinct creatures, each hit once (:1107); surplus is discarded (SPLAY's `MinimumHits` exists to patch that); CLUSTER = one hit of 215 vs SPRAY's five hits of 215.
7. **RADIANCE "AURA ticks faster"** applies to every Field (`:1261`, not Form-gated) while `Enchantments.Needs` says Form.Aura (Enchantments.cs:177) — the forge shows "unmet" on a PRESS build that the sim is speeding up.
8. **ReactionOn.Kill** is declared on WEEP (SkillCatalogue.cs:460) and never dispatched as a reaction (:1904 checks Bitten only); WEEP works only because `BleedOnKillFraction` is scanned in `LandOn`.
9. The GDD still says SPRAY/PULSE "Every 3 beats" (design/gdd/skill-slots-and-skill-trees.md:354/383); catalogue is 4/5 with the reasoning at SkillCatalogue.cs:242-252. Memory says the catalogue is the spec — the GDD is stale.

---

## 11. Tests — what is pinned, and to which architecture

Form-era references counted per file (`Form.X|FormBehaviour|WovenAbility|Affinity`) vs current (`SkillCatalogue|Style.|SkillDef|SkillKind|Variation|Reinforcement|SkillId`):

**Encode the Form architecture (will break when Form leaves):** SoloBattleTests (98/0 — test_projectile_trades_weight_for_volume, test_an_aura_needs_no_cooldown…, test_a_trap_pays_ONLY…, test_a_mark_*, test_transformation_heals…, all six Form-combo trigger tests, test_affinity_*), skill_slot_kinds_test (64/14 — tests the bridge itself: test_every_legacy_form_resolves_inside_its_own_style, test_the_form_table_now_answers_from_the_catalogue, test_a_spilled_transformation_becomes_wilt…), beat_cadence_test (29/0), SkillShapeBattleTests (25/1 — `Fight(shape, creatures, Form)`; pins dead OVERWHELM/SUNDER/EVERYWHERE/CHAIN), SoloExpeditionTests (21/0), source_signature_test (20/0), affinity_test (16/0), BalanceSweepTests (14/0), heal_balance_test (12/0), affinity_vow_buyback_test (10/0), mastery_node_liveness_test (10/1), family_form_affinity_test (8/0), TriggerLivenessTests (7/0 — `Strike(Form)` helper; claims every trigger), charge_keystone_test (6/0), build_glossary_test (6/0), live_build_test (6/0), one_discipline_test (4/0), loot_node_liveness_test (4/0), slot_split_balance_test (4/0), break_badge_test (3/0), skill_stagger_test (3/0), mastery_new_nodes_liveness_test (3/0 — pins dead STAGGER), Encounters/AffixLivenessTests (pins dead SIEGE), PassiveTreeTests, BuildTests, DamageBenchTests, HunterStatWiringTests, taught_tree.

**Current architecture:** skill_catalogue_test (3/50), skill_progress_test (5/35), reinforcement_liveness_test (2/15), variation_liveness_test (2/10 — reaches the six LegacyForm-less skills via the sibling's Form, :79-90), champion_starting_skill_test (0/8), mastery_points_test, MasteryLayoutTests, MasteryTreeTests.

The two liveness theories assert only "something moved" (variation_liveness_test.cs:130); they pass for CLUSTER (a 5× nerf), GLUT (heals anyway) and PULSE (12 base) because a change is not a correctness claim.

---

## 12. Stale terms

| Term | Where | Replacement |
|---|---|---|
| `SquadDamageMultiplier/SquadHealthMultiplier/SquadSkillRate` | HunterProgression.cs:375ff; Build.cs:523-525; Gear.cs; BuildScreen/StatsScreen/SoloExpeditionScreen | `GearDamageMultiplier`… (there is one champion) |
| "squad's health" | WaveReplay.cs:13 | champion |
| Strike/Projectile/Aura/Trap/Mark/Transformation as behaviours | SoloBattle.cs comments throughout (e.g. :1249 "AURA: always on", :1901 "TRAP: the only Form…", :1505 "MARK deals nothing"), FormBehaviour constants `AuraTickMs/MarkWindowMs/MarkMultiplier/TransformationLeech` | Field/Reaction/Amplify/Heal + SkillDef dials |
| `WovenAbility`, "woven" | ResonanceWeaving.cs:257, Build.cs:206, BuildComposer.cs:166 | EquippedSkill(SkillDef…) |
| `poison`, `VenomBasePoison`, `VenomBleedPerHalfSecond` for the shared bleed pool | SoloBattle.cs:258-267, :495, :1222 | bleed |
| "Form-combo enchantment tuning" | SoloBattle.cs:343-374 | style/skill-keyed |
| "Weight branch/axis" | SkillShape.cs:34, :57-60 comments | RESONANCE (branch renamed) |
| `Solo` in `SoloBattle/SoloExpedition/SoloExpeditionScreen` | file names | there is no other mode; `Battle/Expedition` |
| `WaveBonus.Cores`, `Haul.Cores` | WaveModel.cs:324-339 | documented as retired-then-repurposed (forge CORE); name is fine, comments are archaeology |
| `AutoAttackDamage` ×3 | `SoloBattle.AutoAttackDamage` (const 72), `ExpeditionTuning.AutoAttackDamage` (knob), `SkillShape.AutoAttackDamage` (multiplier) | rename the multiplier `SwingMultiplier` |
| GDD "Every 3 beats" | design/gdd/skill-slots-and-skill-trees.md:354/383 | 4/5 per catalogue |
| `ResonanceHunter.*` namespaces | every file | IdleXIdle (out of this area's scope) |

---

## 13. Serialization risks

| Field | Risk | Migration |
|---|---|---|
| `SavedSkill.Form` (string) + `Passive` (bool?) — SaveGame.cs:294-309; written Game1.cs:880/946, read PlayerLoadout.cs:291 | the only durable identity of a woven skill is (Form, Passive); no `SkillId` is saved even though `SkillPick.SkillId` exists in memory | one-shot: `SkillId = SkillCatalogue.Resolve(Enum.Parse<Form>(Form), Passive ?? IsPassive(Form)||FiresOnBeingHit(Form)).Id`; then delete `Resolve(Form,bool)`, `LegacyForm`, `Form` |
| `SavedSkillProgress` variation/reinforcement by **display name** ("LAST DROP", "HIGH WATER") — SkillProgress.cs:323-353 | renaming a variation/reinforcement silently drops the player's purchase (Restore filters by name) | give variations/reinforcements stable ids |
| `SaveGame` legacy affinity Form (SaveGame.cs:158) | Form-typed | drop with Form |
| `MasteryNode.Form` on specialisation nodes (MasteryTree.cs:63); `MasteryTree.Affinity()` returns `Form?` | affinity semantics derived from Form | Style-typed node field |
| `Champion` mid-run state (Health, ReadyAt/ReadyAtBeat, MarkUntilMs, BeatCount, UndyingSpent) | not persisted (`grep -rn "MarkUntilMs\|ReadyAtBeat\|UndyingSpent" src/ResonanceHunter.Core/Persistence` → none); a quit mid-descent loses the run | by design? (open question) |
| `BattleEvent.Amount = (int)Form` | not persisted, but `RunLog`/report code and the screen decode it; changing the payload breaks WaveReplay tests | change event + WaveReplay + screen together |
| `Enchantment.Needs.Form` | enchant kinds are saved on items by kind, not by Form — safe to re-key | — |

---

## 14. Closed loops, good things, risks, opportunities

### Closed-loop "currencies" inside the fight (producer ↔ consumer both present)
- CHARGE: +1 per cast (:1542/1754), +2 per bite (DYNAMO :1897); spent by REND (:1604), banked by LODESTONE (:695); `chargeLive` gate — closed only when a reader is socketed (deliberate).
- Heal budget: 40% of pool per wave, spent by every in-wave heal (:1191); Siphon raises it.
- Bleed pool: fed by VENOM/WEEP/FLIGHT…, drained per 500 ms into the front creature.
- REPAY bank (`takenSinceCast`), BANKED shield, PAYBACK `biteFuel`, RHYTHM `castRamp`, RALLY `killFuelArmed` (producer dead), CASCADE `cascadeArmed` (producer dead).
- Cores → `WaveBonus.Cores` → host pays forge CORE; SkillProgress: waves cleared → levels → variation/reinforcements (SoloExpedition.cs:357-359).

### Good — preserve
- One `Heal()` funnel with ceiling, sustain, NoHealing (:1166-1196).
- `LandOn`/`LandSpread` split; flat signed armour with a floor (:973-991); expected-value crit.
- Integer-ms deterministic tick; a single seeded rng with one live draw; `Beat` published rather than inferred (WaveModel.cs:258-277).
- `SkillDef` as data with variation/reinforcement as `Func<SkillDef,SkillDef>` deltas (Build.cs:252-264) — no case per variation.
- `Kind`/`Effect` as two axes (even though `Effect` is unread yet).
- `Break` event as a state the replay can scrub (WaveReplay.cs:347-348).
- `SkillShape.Combine` threshold rule (kinder wins, no summing) (SkillShape.cs:446-455).
- `ChampionHealth` as the single mint helper; `ExpeditionTuning`/`HealTuning` injection.
- The liveness test pattern (variation/reinforcement/trigger) — extend it to correctness, not just change.

### Over-engineering risks
- `SkillDef` is a 57-parameter positional record (SkillCatalogue.cs:155-227) — one dial per mechanic is heading toward a flat effect engine; two more styles would double it.
- `SkillShape` has 85 members, 18 with no producer; `Combine` is 90 lines of hand-written merge rules.
- Amplify implemented four ways; heal eleven producers; execute three ways; target count five ways (§8).
- Six per-Source signatures hard-wired at ten sites, plus a Source matchup wheel, plus Source set rungs, plus PURE — four Source systems.
- WEAVER (one keystone) drags Form, next-slot indexing and a second full damage pipeline into the cast path (:1702-1719).
- CHARGE: four keystones and six sim sites for a mechanic with one spender.

### Abstraction opportunities (each has ≥2 real users today)
1. `SkillDef.BaseDamage` dial; delete `FormBaseValue`/`FormBehaviour.BaseDamage` — removes Form from the damage root and fixes PULSE.
2. Read `Def.Effect` for the amplify/heal forks; delete `IsAmplifier/Heals`; base `AmplifyPercent/AmplifyMs` on CALL/BRAND and base `Lifesteal` on DRINK; delete `MarkMultiplier/MarkWindowMs/TransformationLeech`.
3. One `Amplify` state (`bonus, untilMs, wholeWave, source`) on the wave, reset per wave, published as an event — replaces `MarkUntilMs` + three locals and fixes the STEADY leak/overflow.
4. Typed per-creature debuff state (`Defense`, `AttackBreak`, `SlowFraction`, `StunnedUntil`, `Wounds`, `ArmourBent`) on `WaveCreature` (make `Damage` an effective property) so presentation and `LastWaveCreatures` can read it; publish as state events like `Break`.
5. Event identity by slot index (or skill id) instead of `(Source, Form)`; split `Shield` into `ShieldDuration`/`ShieldAmount` or carry both.
6. `EquippedSkill` holds the `SkillDef` and a `SkillId`; `WovenAbility` becomes a save-migration record only.
7. `Build.Affinity : Style?` using the existing `SkillCatalogue.RingDistance`; `FormPower/FormTargets/Aptitude/FavouredForms` keyed by `Style`.
8. Reaction re-arm as a `SkillDef` dial (`IntervalMs` for Reactions too), deleting `EquippedSkill.CooldownMs`.
9. Delete the 18 orphan `SkillShape` fields and their sim branches and tests — or re-author their nodes; either way stop the silent third state.
10. Move `CastClipMs/ClipShareOfBeat/SkillClipShareOfBeat/UndyingShieldMs` to the Game project.

---

## 15. Open questions

1. Is PULSE at `FormBaseValue[Aura]=12` per cast intended? Nothing measures it; the beat-demand and slot-split tests only count casts.
2. Is the flat 1.6× mark meant to stack with SPEND/STEADY/SPRAWL depth, and should `markBonus` expire with the window?
3. `Champion.MarkUntilMs` persisting across waves — deliberate ("cooldowns persist") or a leak? STEADY makes it permanent.
4. Should a run be resumable (Champion state persisted), or is "quit = run over" the design?
5. Should the sim advance independently of the replay (a real fast-forward that also drives offline), or is wave-boundary gating acceptable given waves are atomic?
6. The 18 orphan SkillShape fields: delete, or re-author nodes for them? SkillShapeBattleTests/AffixLivenessTests currently keep them green.
7. Are the six Form-combo triggers (Overdraw/Linger/Radiance/Execute/Coiled/Siphon) worth keeping as Style-keyed enchants, or should they become reinforcements on the skill they name?
8. Should Source signatures stay six hard-wired behaviours, or become dials on `SkillVariation` (which already carries the Source)?
9. `Targets` vs hits: should the model gain "hits per target" so CLUSTER/SPRAY/TWIN can mean what they say?
10. Is WEAVER worth its Form dependency, or is it a candidate for deletion with the Form table?
