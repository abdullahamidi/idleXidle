# Liveness audit — every build-resolution dial

**Area:** "a mechanic without a consumer does not exist", applied mechanically to every dial the build resolves into.
**Repo:** `idleXidle` @ `ae30f2a` (`feat/hunter-cutout-rig`), 2026-08-31.
**Scope:** `Builds/SkillShape.cs` (83 data dials), `Builds/Build.cs` (`BuildMods` 5 fields, `BuildTrigger` 22 members), `Builds/SkillCatalogue.cs` (`SkillDef` 43 optional dials + 14 required fields), `Economy/HunterProgression.cs` (`HunterStat` 9), `Economy/Enchantments.cs` (`EnchantKind` 16), `Builds/Keystones.cs` (19 keystones), `Economy/GearTraits.cs` (`GearTrait` 10), `Economy/ItemAffixes.cs` (`AffixStat` 6).
**Method:** for every member, `grep -rn -w <Name> src/ResonanceHunter.Core src/ResonanceHunter.Game` (bin/obj excluded), every hit line read and classified as DECLARED / PRODUCED / COMBINED / CONSUMED-SIM / CONSUMED-UI-ONLY / TEST-ONLY. Ambiguous short names (`Leech`, `SkillRate`, `MaxHealth`, `DamageTaken`, `ReflectFraction`, `AutoAttackDamage`) were re-grepped with a `shape.`/`Shape.`/initialiser-qualified pattern and the hit lines read. All 2,149 lines of `SoloBattle.cs` and all of `SoloExpedition.cs`, `BuildComposer.cs`, `MasteryCatalog.cs`, `ElementSets.cs`, `CharacterRoster.cs`, `Character.cs`, `GearShape.cs`, `DustEffects.cs`, `FormBehaviour.cs`, `HealTuning.cs` were read in full. `tests/` was grepped only to separate "no producer anywhere" from "test-only producer".

**Status vocabulary.** LIVE = has a runtime producer AND a sim consumer (SoloBattle / SoloExpedition / HaulForWave / ChampionHealth). ORPHAN = sim consumer exists, nothing at runtime ever sets it. DEAD = produced but nothing reads it. UI-ONLY = only drawn/described. TEST-ONLY = the only producer is a test fixture.

Paths below are relative to `src/ResonanceHunter.` (so `Core/Builds/SoloBattle.cs:807` is `src/ResonanceHunter.Core/Builds/SoloBattle.cs:807`).

---

## 0. Headline

1. **SkillShape has no dead dial but eighteen orphans.** Every one of its 83 data dials is read by `SoloBattle.ResolveWave`, `SoloExpedition.HaulForWave`/`PushWave` or `SoloBattle.ChampionHealth` — the record's own claim at `SkillShape.cs:21` holds. But 18 dials have **no runtime producer**: no mastery node, element set, character, weapon family or Dust node ever sets them. They are the residue of the WEIGHT and SPREAD branches retired in the 2026-08-30 re-axe (`MasteryCatalog.cs:20-26, 33-36`); the nodes were deleted and the sim code + fields were kept. Nine of the eighteen are not even set by a test. The sim carries ~120 lines of conditional logic (`SoloBattle.cs:956-964, 977-985, 1005-1016, 1024, 1026, 1076-1077, 1094-1102, 1129-1149, 833-843, 665`) that no player can ever reach.
2. **The one DEAD field in scope is `SkillDef.Effect`.** All twelve skills declare a `SkillEffect` (`SkillCatalogue.cs:264-593`) and nothing in Core or Game reads it. The sim still branches on `FormBehaviour.IsAmplifier(form)` / `FormBehaviour.Heals(form)` (`SoloBattle.cs:1376, 1435, 1503, 1705, 1721`) — Form-keyed content checks doing the job the dead field was written to do. `SkillDef.ClipKey` is likewise produced and never read (the hunt screen derives the clip from Form at `Game/SoloExpeditionScreen.cs:3280`); `ReactionOn.Kill/LowHealth/WaveStart` are never dispatched on.
3. **Every BuildTrigger, EnchantKind, HunterStat, AffixStat, GearTrait and Keystone is LIVE.** The HARVEST/LODESTONE "Cores" channel that `SoloExpedition.cs:486-488` and `WaveModel.cs:318` say "nothing consumes" **is** consumed — `Game/Game1.cs:3646-3648` converts `Haul.Cores` to `Material.Core`. The comments are stale, not the mechanic.
4. **Form is still the sim's key.** Five BuildTriggers (Rend, Execute, Overdraw, Siphon, Weaver), the affinity system, base damage, target count, the Field per-tick scaling and every `BattleEvent` payload are keyed on `Form`, not `Style` or `SkillDef`. The Style ring helpers written to replace it (`SkillCatalogue.RingDistance/Opposite`, `SkillCatalogue.cs:661-670`) have **zero consumers** in `src/`.
5. **Two-layer duplicates.** The same mechanic exists once as a `SkillShape` dial and once as a `SkillDef` dial in at least five places (per-living-creature damage, once-per-wave execute, reflect, stun/push-back, lifesteal), and the same all-damage / rate / health multiplier exists once in `BuildMods` and once (or twice) in `SkillShape`.

---

## 1. SkillShape — full table (83 dials)

Column key — **P** producers (runtime), **C** sim consumers, **T** test-only producer if no runtime one.

### 1.1 RESONANCE branch dials (all LIVE)

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| HitSize | SkillShape.cs:37 | MasteryCatalog.cs:244 (second_look 0.92), :319 (blitz 0.80), :411 (volley 0.75); CharacterRoster.cs:62 (seeker 1.08) | SoloBattle.cs:807 `m *= shape.HitSize * shape.DamageDealt` | LIVE |
| VowPowerMultiplier | :55 | MasteryCatalog.cs:179 (pledge 1.30), :198 (zealot 1.60); CharacterRoster.cs:201 (oathbound 1.5); DustEffects.cs:243 (artifice_vows 1.25) | SoloBattle.cs:2111 (VowFactor) | LIVE |
| StrongMatchupBonus | :63 | MasteryCatalog.cs:177 (keyed) | SoloBattle.cs:730 | LIVE |
| WeakMatchupRelief | :66 | MasteryCatalog.cs:193 (discord) | SoloBattle.cs:731 | LIVE |
| AllMatchupsStrong | :69 | MasteryCatalog.cs:202 (chord) | SoloBattle.cs:727 | LIVE |
| AffinityStyleBonus | :72 | MasteryCatalog.cs:181 (narrow) | SoloBattle.cs:717 | LIVE — Form-keyed (`aff == f` on Form) |
| OffStylePenalty | :73 | MasteryCatalog.cs:181 (narrow) | SoloBattle.cs:718 | LIVE — Form-keyed |
| OppositePenaltyRelief | :76 | MasteryCatalog.cs:183 (broad) | SoloBattle.cs:714-715 | LIVE — Form-keyed |
| ResonanceWorth | :79 | MasteryCatalog.cs:186 (deep) | SoloBattle.cs:487 | LIVE |
| OneSourceBonus | :82 | MasteryCatalog.cs:191 (pure) | SoloBattle.cs:742 | LIVE |

### 1.2 LOOT branch dials (all LIVE, consumed in SoloExpedition.HaulForWave)

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| HaulPerCleanSecond | :93 | MasteryCatalog.cs:229 (untouched) | SoloExpedition.cs:444-446 | LIVE |
| HaulCleanCap | :94 | MasteryCatalog.cs:229 | SoloExpedition.cs:447 | LIVE |
| HaulUntouchedWave | :107 | MasteryCatalog.cs:235 (spotless), :246 (vein) | SoloExpedition.cs:457-459 | LIVE |
| HaulWhenHurt | :110 | MasteryCatalog.cs:231 (bloodprice), :242 (gamble) | SoloExpedition.cs:463-464 | LIVE |
| HaulPerWavePastDepth | :113 | MasteryCatalog.cs:238, :249, :253 | SoloExpedition.cs:469-470 | LIVE |
| HaulDepthFloor | :114 | MasteryCatalog.cs:238, :253 | SoloExpedition.cs:470 | LIVE |
| RarityFromClean | :117 | MasteryCatalog.cs:244 (second_look) | SoloExpedition.cs:509 | LIVE |
| HaulEveryNthWave | :120 | MasteryCatalog.cs:233 (cache) | SoloExpedition.cs:473 | LIVE |
| HaulNthWaveBonus | :121 | MasteryCatalog.cs:233 | SoloExpedition.cs:474 | LIVE |

### 1.3 Former WEIGHT dials

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| ArmourPenetration | :123 | MasteryCatalog.cs:408 (executioner bridge, 9) | SoloBattle.cs:981 | LIVE (single producer) |
| ArmourIgnoreFraction | :126 | **none** (grep -w: only SkillShape.cs:126,497 + SoloBattle.cs:984; 0 test files) | SoloBattle.cs:984 | **ORPHAN** |
| CrushArmourMultiple | :129 | **none** (0 test files) | SoloBattle.cs:982-983 | **ORPHAN** |
| OverwhelmFloor | :140 | **none at runtime**; T: SkillShapeBattleTests.cs:140, MasteryTreeTests.cs:375 | SoloBattle.cs:956-964 | **ORPHAN** (test-only) |
| SunderThreshold | :143 | **none**; T: SkillShapeBattleTests.cs:168 | SoloBattle.cs:1007 | **ORPHAN** (test-only) |
| SunderAmount | :144 | **none**; T: SkillShapeBattleTests.cs:168 | SoloBattle.cs:1008 | **ORPHAN** (test-only) |
| OverkillCarry | :155 | CharacterRoster.cs:79 (anvil 0.33) | SoloBattle.cs:1076-1077 | LIVE (single producer) |
| FreshThreshold | :166 | **none** (0 test files) | SoloBattle.cs:835 | **ORPHAN** |
| FreshBonus | :167 | **none** (0 test files) | SoloBattle.cs:835-836 | **ORPHAN** |
| StaggerThreshold | :178 | **none**; T: mastery_new_nodes_liveness_test.cs:77 | SoloBattle.cs:1012 | **ORPHAN** (test-only) |
| StaggerMs | :179 | **none**; T: same | SoloBattle.cs:1012-1014 | **ORPHAN** (test-only) |

### 1.4 Former SPREAD dials

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| ExtraTargets | :184 | MasteryCatalog.cs:411 (volley bridge, +1) | SkillShape.cs:442 (TargetsFor) → SoloBattle.cs:1424,1654,1715,1943 | LIVE (single producer) |
| FormTargets | :187 | MasteryCatalog.cs:439 (spec_projectile, `[Form.Projectile]=1`) | SkillShape.cs:442 → same | LIVE (single producer) — **Form-keyed** `Dictionary<Form,int>` |
| FormPower | :202 | Character.cs:208 (Aptitude → `[f]=AptitudePower`, 7 champions); GearShape.cs:27-29 (ItemFamilies.FavouredForms × 1.25) | SkillShape.cs:205 FormPowerFor → SoloBattle.cs:811 | LIVE — **Form-keyed** `Dictionary<Form,float>` |
| StrikesEveryCreature | :208 | **none**; T: SkillShapeBattleTests.cs:183 | SkillShape.cs:438 (TargetsFor) | **ORPHAN** (test-only) |
| ChainFraction | :211 | **none**; T: SkillShapeBattleTests.cs:197 | SoloBattle.cs:1132 | **ORPHAN** (test-only) |
| RicochetChance | :214 | **none** (0 test files) | SoloBattle.cs:1133 | **ORPHAN** |
| RicochetFraction | :215 | **none** (0 test files) | SoloBattle.cs:1134 | **ORPHAN** |
| CascadeOnKill | :218 | **none** (0 test files) | SoloBattle.cs:1024, 1096 | **ORPHAN** |
| CooldownRefundOnKillMs | :230 | ElementSets.cs:99 (Shadow 5-piece, one beat) | SoloBattle.cs:1061-1068 | LIVE (single producer) |
| NextSkillAfterKillBonus | :238 | **none** (0 test files) | SoloBattle.cs:1026, 1101 | **ORPHAN** |
| RatePerCreature | :246 | **none** (0 test files) | SoloBattle.cs:665 (RateNow) | **ORPHAN** |

### 1.5 Conditional-damage dials

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| FirstHitMultiplier | :251 | MasteryCatalog.cs:301 (alpha 1.9), :408 (executioner 1.10); CharacterRoster.cs:109 (metronome 2), :138 (tower 0.85) | SoloBattle.cs:826 | LIVE |
| LaterHitMultiplier | :254 | MasteryCatalog.cs:301 (0.80); CharacterRoster.cs:138 (1.35) | SoloBattle.cs:826 | LIVE |
| CullThreshold | :257 | MasteryCatalog.cs:435 (spec_strike 0.35); ElementSets.cs:98 (Shadow 3pc 0.30) | SoloBattle.cs:830 | LIVE |
| CullBonus | :258 | MasteryCatalog.cs:435 (0.40); ElementSets.cs:98 (0.12) | SoloBattle.cs:830-831 | LIVE |
| PerCreatureBonus | :261 | CharacterRoster.cs:93 (chorus 0.05) | SoloBattle.cs:847 | LIVE (single producer) |
| VsArmouredBonus | :264 | **none**; T: AffixLivenessTests.cs:58 | SoloBattle.cs:839-841 | **ORPHAN** (test-only) |
| VsOtherPenalty | :265 | **none**; T: AffixLivenessTests.cs:58, MasteryTreeTests.cs:375,405 | SoloBattle.cs:839-842 | **ORPHAN** (test-only) |
| OpeningSeconds | :268 | MasteryCatalog.cs:310 (surge), :333 (first_strike) | SoloBattle.cs:850-851 | LIVE |
| OpeningBonus | :269 | MasteryCatalog.cs:310, :333 | SoloBattle.cs:852 | LIVE |
| AfterOpeningPenalty | :270 | MasteryCatalog.cs:333 | SoloBattle.cs:853 | LIVE |
| InterruptBonus | :280 | MasteryCatalog.cs:304 (interrupt) | SoloBattle.cs:867-870 | LIVE |
| DamagePerMaxHealth | :283 | MasteryCatalog.cs:392 (bastion) | SoloBattle.cs:820 | LIVE |
| HitSizePerMaxHealth | :286 | MasteryCatalog.cs:417 (anchor bridge) | SoloBattle.cs:819 | LIVE |
| DamageDealt | :289 | MasteryCatalog.cs:193,195,202,246,253,386,389 | SoloBattle.cs:807 | LIVE |

### 1.6 TEMPO dials

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| SkillRate | :294 | MasteryCatalog.cs:319 (blitz), :327 (rush), :411 (volley); CharacterRoster.cs:156 (quiver); ElementSets.cs:103 (Spirit 3pc) | SoloBattle.cs:530, 643, 664, 2144 | LIVE (also UI: Game/StatsScreen.cs:333, Game/SoloExpeditionScreen.cs:856) |
| FreeOpeningCast | :297 | CharacterRoster.cs:109 (metronome) — the PREPARATION node was cut (MasteryCatalog.cs:271-272) | SoloBattle.cs:517, 1471, 1492 | LIVE (single producer; the :1492 read is on an unreachable branch, see §7) |
| BonusCritPercent | :300 | ElementSets.cs:88 (Mind 3pc, 6) | SoloBattle.cs:2004 (CritChance), 2142 | LIVE (single producer) |
| MarkWindowMultiplier | :303 | MasteryCatalog.cs:443 (spec_mark 1.3); CharacterRoster.cs:201 (oathbound 1.5); ElementSets.cs:89 (Mind 5pc 1.5) | SoloBattle.cs:1381, 1508 | LIVE |
| MarkPowerBonus | :304 | CharacterRoster.cs:201 (oathbound 0.25) | SoloBattle.cs:786 | LIVE (single producer) |
| AssassinateThreshold | :307 | MasteryCatalog.cs:322 (assassinate 0.30) | SoloBattle.cs:1621-1623 | LIVE |
| AutoAttackRate | :314 | MasteryCatalog.cs:293 (brisk 1.20) | SoloBattle.cs:1775 | LIVE (single producer) |
| AutoAttackDamage | :317 | MasteryCatalog.cs:195 (resonant 0.85), :249 (lode 0.90); ElementSets.cs:79 (Body 5pc 1.25) | SoloBattle.cs:1763 | LIVE |
| SourceBonus | :323 | ElementSets.cs:58 (2- and 4-piece rungs, +0.08 each) | SoloBattle.cs:737 | LIVE — Source-keyed `Dictionary<Source,float>` |
| CastRampPerCast | :334 | MasteryCatalog.cs:298 (rhythm) | SoloBattle.cs:666 | LIVE |
| CastRampMax | :335 | MasteryCatalog.cs:298 | SoloBattle.cs:1500 | LIVE |
| FirstCastMultiplier | :346 | MasteryCatalog.cs:316 (opening_volley); ElementSets.cs:104 (Spirit 5pc) | SoloBattle.cs:1578, 1929 | LIVE |
| LaterCastMultiplier | :347 | MasteryCatalog.cs:316 | SoloBattle.cs:1578, 1929 | LIVE |

### 1.7 ENDURE dials

| dial | declared | producers | consumers | status |
|---|---|---|---|---|
| Leech | :352 | ElementSets.cs:94 (Nature 5pc 0.02) — the LEECH mastery node was cut (MasteryCatalog.cs:347) | SoloBattle.cs:1157-1158 | LIVE (single producer) |
| HealPerTargetStruck | :355 | MasteryCatalog.cs:414 (feedback bridge) | SoloBattle.cs:1153-1154 | LIVE |
| FlatDamageReduction | :358 | MasteryCatalog.cs:372 (padding); CharacterRoster.cs:172 (thornwall); ElementSets.cs:83 (Machine 3pc) | SoloBattle.cs:1840 | LIVE |
| DamageTaken | :361 | MasteryCatalog.cs:198, 242, 327, 389; CharacterRoster.cs:172 | SoloBattle.cs:1830 | LIVE |
| AbsorbAtLowHealth | :364 | MasteryCatalog.cs:396 (absorb) | SoloBattle.cs:1834-1837 | LIVE |
| FirstBiteFree | :367 | ElementSets.cs:84 (Machine 5pc) — FORTIFY node cut | SoloBattle.cs:1844 | LIVE (single producer) |
| ReflectFraction | :380 | MasteryCatalog.cs:382 (thorns 0.15) | SoloBattle.cs:1880-1886 | LIVE (single producer) |
| HealOnClear | :383 | MasteryCatalog.cs:369 (second_wind) | SoloBattle.cs:689-690 | LIVE |
| RegenFraction | :390 | ElementSets.cs:93 (Nature 3pc) — MENDING node cut | SoloBattle.cs:1218-1219 | LIVE (single producer) |
| BiteFuelBonus | :400 | MasteryCatalog.cs:378 (payback) | SoloBattle.cs:1102, 1868 | LIVE |
| BiteFuelMax | :401 | MasteryCatalog.cs:378 | SoloBattle.cs:1868 | LIVE |
| HealOnBiteFraction | :408 | MasteryCatalog.cs:386 (rebound) | SoloBattle.cs:1869-1870 | LIVE |
| BetweenWaveRegen | :416 | MasteryCatalog.cs:375 (recovery) | SoloExpedition.cs:371, 386 | LIVE |
| FullHealBetweenWaves | :419 | MasteryCatalog.cs:400 (endless) | SoloExpedition.cs:367 | LIVE |
| MaxHealth | :422 | MasteryCatalog.cs:400 (endless 0.5); ElementSets.cs:78 (Body 3pc 1.10) | SoloBattle.cs:2074 (VowHealthMultiplier, build.Shape) and :2054 (GearShape.Of(hunter).MaxHealth) | LIVE |

### 1.8 Totals

- **83 dials.** LIVE 65, ORPHAN 18, DEAD 0, UI-ONLY 0.
- **Orphans (18):** ArmourIgnoreFraction, CrushArmourMultiple, OverwhelmFloor, SunderThreshold, SunderAmount, FreshThreshold, FreshBonus, StaggerThreshold, StaggerMs, StrikesEveryCreature, ChainFraction, RicochetChance, RicochetFraction, CascadeOnKill, NextSkillAfterKillBonus, RatePerCreature, VsArmouredBonus, VsOtherPenalty.
  - Of these, **9 have no producer even in tests**: ArmourIgnoreFraction, CrushArmourMultiple, FreshThreshold, FreshBonus, RicochetChance, RicochetFraction, CascadeOnKill, NextSkillAfterKillBonus, RatePerCreature.
  - The other 9 are set only by `SkillShapeBattleTests.cs`, `mastery_new_nodes_liveness_test.cs`, `AffixLivenessTests.cs`, `MasteryTreeTests.cs` — tests that prove the *sim code* reacts, for nodes that no longer exist.
- **Combine coverage:** every one of the 83 dials appears in `SkillShape.Combine` (SkillShape.cs:473-564). No dial is silently dropped on combine.
- **Sim state carried only for orphans:** `cascadeArmed` (SoloBattle.cs:457, 1024, 1096), `killFuelArmed` (:467, 1026, 1101). `staggeredThisBite` (:470) is shared with the live PIN stun (:1294-1298) so it stays.

---

## 2. BuildMods and BuildTrigger (Build.cs)

### 2.1 BuildMods (5 fields) — all LIVE

| field | producers | consumers |
|---|---|---|
| Damage | Keystones (Build.cs:517), Hunter.SquadDamageMultiplier (:523), Dust attribute nodes via PassiveMods (MemoryDust.cs:490-524, 12 nodes), Character.Mods (CharacterRoster.cs:217) | SoloBattle.cs:705 `var m = mods.Damage` |
| Health | same channels | SoloBattle.cs:2055 (ChampionHealth) |
| SkillRate | same | SoloBattle.cs:530, 643, 664, 2144 |
| Haul | same | SoloExpedition.cs:418; SoloBattle.cs:818 (Hoarder) |
| Rarity | Keystones, Character (magpie 1.20), Dust nodes | SoloExpedition.cs:508 |

Note: `MasteryTree.Mods()` returns `BuildMods.None` unconditionally (MasteryTree.cs:294) and is still folded at BuildComposer.cs:126 — a dead channel by design ("kept for the call sites that still speak BuildMods"). `Build.Resolve` computes `fromGear` and discards it (Build.cs:519-531).

### 2.2 BuildTrigger (22 members) — all LIVE

| trigger | producers | sim consumer | notes |
|---|---|---|---|
| Splinter | Keystones.cs:81 (reaper); EnchantKind.Splinter → Build.cs:597 | SoloBattle.cs:697 `bonus?.AddQuality(0.15f)` | flat 0.15 — the enchant's rarity-scaled magnitude (Enchantments.cs:276) is never read |
| Harvest | EnchantKind.Harvest only (Build.cs:598) | SoloBattle.cs:500-501, 692 → `bonus.AddCores` → Haul.Cores → **Game/Game1.cs:3646-3648 `AddMaterial(Material.Core, …)`** | LIVE. SoloExpedition.cs:486-488 and WaveModel.cs:318 say nothing consumes Cores — **stale comments**. The `Math.Max(0.10f, …)` floor at :501 is unreachable (only producer is an enchant with magnitude ≥ 0.10). |
| Venom | Keystones.cs:95 (venomancer); EnchantKind.Venom | SoloBattle.cs:491-494, 943 | `VenomBasePoison` reachable via VENOMANCER |
| Desperation | EnchantKind.Desperation only (Build.cs:600) | SoloExpedition.cs:476-483 | `DesperationHaulBonus` fallback (:120, :483) unreachable — no mastery/keystone grants the trigger any more |
| Undying | Keystones.cs:88; CharacterRoster.cs:121 (unbroken); EnchantKind.Undying | SoloBattle.cs:1954 | |
| NoHealing | Keystones.cs:48 (blood_magic), :105 (juggernaut) | SoloBattle.cs:1169; SoloExpedition.cs:371 | |
| Echo | Keystones.cs:62 | SoloBattle.cs:1550, 763 | |
| Bloodlust | Keystones.cs:55 | SoloBattle.cs:745 | |
| Zeal | Keystones.cs:105 | SoloBattle.cs:755 | |
| Rend | Keystones.cs:145 | SoloBattle.cs:475, 1601 | **Form-keyed**: `form == Form.Strike` |
| Capacitor | Keystones.cs:155 | SoloBattle.cs:476, 479 | |
| Dynamo | Keystones.cs:165 | SoloBattle.cs:477, 1894 | |
| Lodestone | Keystones.cs:176 | SoloBattle.cs:478, 695 → Cores → Game1.cs:3648 | LIVE (see Harvest) |
| LooseAgain | CharacterRoster.cs:155 (quiver) | SoloBattle.cs:1049 | |
| Overdraw | EnchantKind.Overdraw; MasteryCatalog.cs:439 (spec_projectile) | SoloBattle.cs:1551 | **Form-keyed**: `form == Form.Projectile` |
| Linger | EnchantKind.Linger; MasteryCatalog.cs:443 (spec_mark) | SoloBattle.cs:1382, 1507 | |
| Radiance | EnchantKind.Radiance; MasteryCatalog.cs:441 (spec_aura) | SoloBattle.cs:1261 | applies to **every** `SkillKind.Field` (PRESS, BRAND, WILT, MIRE), while `EnchantNeed` says `Form.Aura` (Enchantments.cs:177) — Forge greys it for a PRESS-only build that in fact benefits |
| Execute | EnchantKind.Execute; MasteryCatalog.cs:435 (spec_strike) | SoloBattle.cs:1613 | **Form-keyed**: `form == Form.Strike`, cast path only — PRESS carries Form.Strike (PlayerLoadout.cs:194-196) so satisfies `EnchantNeed` while never reaching the branch |
| Coiled | EnchantKind.Coiled; MasteryCatalog.cs:437 (spec_trap) | SoloBattle.cs:1909 | |
| Siphon | EnchantKind.Siphon; MasteryCatalog.cs:448 (spec_transformation) | SoloBattle.cs:639, 1438, 1730 | **Form-keyed**: gated on `FormBehaviour.Heals(form)` |
| Hoarder | Keystones.cs:133 | SoloBattle.cs:817 | |
| Weaver | Keystones.cs:186 | SoloBattle.cs:1702-1718 | **Form-keyed**: `IsAmplifier/FiresOnBeingHit(woven.Form)`, `BaseDamage(woven.Form)` |

---

## 3. SkillDef (SkillCatalogue.cs:155-230)

### 3.1 Optional dials (43) — all LIVE

| dial | declared | producers (catalogue) | consumer (SoloBattle) | note |
|---|---|---|---|---|
| DefenceBreakPerTick | :176 | :299, :308, :312, :316 (PRESS base 5) | :1303, 1306, 1318 | |
| DefenceBreakFloor | :177 | :299, :301, :312, :316 | :1318 | |
| SlowFraction | :178 | :534, :538 (MIRE base 0.25) | :1330, 1341 | |
| AttackBreakPerTick | :179 | :582, :585, :589, :593 (WILT base 0.10) | :1351, 1359, 1364, 1367 | |
| AttackBreakFloor | :180 | :578, :585, :591, :593 | :1359, 1363, 1367 | |
| BleedOnKillFraction | :181 | :468, :477, :483 (WEEP base 0.30) | :1040-1042 | scanned over all woven skills regardless of Kind/On |
| PaysBackDamageTaken | :182 | :329, :331, :344, :346 (REPAY base 2.0) | :1566-1568, 1852 | |
| DefenceIgnore | :187 | :272 (FLATTEN) | :1683 | |
| ExecuteFraction | :188 | :281, :283, :285 (FINISH/BRINK/TWICE) | :1664-1665 | |
| StunMs | :189 | :308, :310 (PIN/HOLD) | :1289, 1296 | |
| ShieldInsteadOfDamage | :190 | :338 (BANKED) | :1674 | |
| ReflectFraction | :191 | :356, :362, :365, :367 (NET/SPITE/IRON/REPRISAL) | :1920 | same name as SkillShape.ReflectFraction, different base (see §6) |
| StopsWholeBite | :192 | :365 (IRON) | :1934 | |
| AmplifyPercent | :193 | :384, :386, :393, :411, :413 | :1389, 1528, 1530 | |
| AmplifyMs | :194 | :384, :386, :390 | :1518 | |
| AmplifyPerCast | :195 | :393, :395 (STEADY/REDOUBLE) | :1519-1523 | |
| AmplifyCap | :196 | :393, :397 | :1521-1522 | |
| AmplifyWholeWave | :197 | :411 (SPRAWL) | :1398 | |
| AmplifyDeepenPerTick | :198 | :415, :420, :422 | :1390-1394 | |
| AmplifyDeepenCap | :199 | :415, :420, :424 | :1392-1393 | |
| BleedRate | :200 | :466, :470, :481 | :605 | |
| BleedCarriesWaves | :201 | :475 (CARRION) | :606 | |
| DamagePerLivingEnemy | :202 | :494, :496 (THRONG/HORDE) | :1643 | duplicate of SkillShape.PerCreatureBonus (same formula at :847) |
| SplitPool | :203 | :503, :505 | :1655, 1658 | |
| SplitMaxWays | :204 | :503, :507 | :1657 | |
| SlowDeepenPerTick | :205 | :521, :523 | :1344 | |
| SlowCeiling | :206 | :521, :523, :525, :530, :532, :534 | :1346 | |
| SlowPerEnemy | :207 | :530, :532 (TEEMING/CLOG) | :1330, 1345 | |
| Lifesteal | :208 | :549, :551, :558 | :1691-1692 | stacks with the Form-keyed `Heals(form)` heal at :1721-1731 (see §6) |
| DamagePerHealth | :209 | :558, :560 (GLUT/SURFEIT) | :1639-1640 | |
| HealPerPulse | :210 | :576 (SUP) | :1371-1372 | |
| FrontEnemyOnly | :211 | :585 (SHRIVEL) | :1357 | |
| MinimumHits | :212 | :439, :441 (SPLAY/TWIN) | :1649-1650 | |
| DamageMultiplier | :217 | :274, :333, :340, :443, :450, :498, :527, :553, :562 | :1423 (Field damage path), :1636 (Active) | not read on the Reaction path — none produced there |
| CooldownMultiplier | :218 | :360, :369 (RECOIL/BLUNT, both JAWS) | :1908 (Reaction path **only**) | a CooldownMultiplier on an Active or Field would be silently ignored — trap for future authoring |
| TargetsBonus | :219 | :276, :303, :454 | :1312 (PRESS), :1424 (Field damage), :1654 (Active) | |
| ExecutesPerWave | :220 | :285 (TWICE) | :1664 | |
| SwingIgnoresArmour | :221 | :278 (TRAIL) | :620 → :1765 | |
| SwingLifesteal | :222 | :555 (TRICKLE) | :621 → :1766-1767 | |
| ReflectGrowthPerBite | :223 | :358, :371 | :1921-1922 | |
| ReflectGrowthCap | :224 | :358, :371 | :1922 | |
| BleedFromHits | :225 | :445, :452, :472, :479 | :611 → :1687 | gathered from the whole weave (WEEP is a Reaction that never casts) |
| BreakSecondEnemy | :226 | :587 (HOLLOW) | :1362-1364 | |

### 3.2 Required fields (14)

| field | producers | consumers | status |
|---|---|---|---|
| Id, Name, Line | catalogue | UI, SkillProgress, MasteryCatalog.Teaches (:481-483) | LIVE |
| Style | catalogue | SkillCatalogue.ActiveOf/PassiveOf (:603, :606), PlayerLoadout.cs:195, WeaveScreen.cs:694 | LIVE. **`RingDistance`/`Opposite` (:661-670) have zero consumers in src** — affinity is still computed on Form via FormBehaviour.AffinityFactor |
| Kind | catalogue | SoloBattle.cs:1242, 1904; TakesABeat (:229) → Build.cs:282 | LIVE |
| **Effect** | catalogue (all 12) | **none** — `grep "SkillEffect\.\|\.Effect\b" src` returns only the 12 declarations (SkillCatalogue.cs:264-568) and an unrelated `UnlockEffect` at Game/PrestigeScreen.cs:375 | **DEAD.** The sim branches on `FormBehaviour.IsAmplifier(form)` (SoloBattle.cs:1376, 1503, 1705) and `FormBehaviour.Heals(form)` (:1435, 1721) instead |
| Beats | catalogue | SoloBattle.cs:1453; Build.cs:429 (BeatDemand); BuildComposer.cs:179 | LIVE |
| IntervalMs | catalogue | SoloBattle.cs:1260 | LIVE |
| On | catalogue | SoloBattle.cs:1904 — **only `ReactionOn.Bitten`** | PARTIAL. `ReactionOn.Kill` (WEEP, :460) is produced but never dispatched on — WEEP works through `BleedOnKillFraction` (:1037-1044) scanned for every woven skill. `ReactionOn.LowHealth`, `ReactionOn.WaveStart` (:96-97): never produced, never consumed — dead enum members |
| Targets | catalogue | SoloBattle.cs:1654 — **Active path only** | PARTIAL. Field path uses `shape.TargetsFor(form)` = `FormBehaviour.Targets(form)` (:1424); Reaction path hard-codes `TargetsFor(Form.Trap)` (:1943). WEEP `Targets: WholeWave`, JAWS/PRESS/BRAND `Targets: 1`, MIRE/WILT `WholeWave` are never read; they happen to agree with the Form table today |
| **ClipKey** | catalogue | **none in src** — Game/SoloExpeditionScreen.cs:3280 derives the clip as `((Form)nextSkill.Amount).ToString().ToLowerInvariant()`; only tests read it (skill_catalogue_test.cs:254, skill_slot_kinds_test.cs:53) | **DEAD (test-only)** |
| FxKey | catalogue | Game/SoloExpeditionScreen.cs:3356 | LIVE (presentation) |
| Variations | catalogue | Build.cs:260 (Def), SkillProgress, WeaveScreen.cs:1620 | LIVE |
| LegacyForm | catalogue (6 non-null) | SkillCatalogue.Resolve (:653), PlayerLoadout.cs:194, WeaveScreen.cs:1492 | LIVE (migration bridge) |

---

## 4. HunterStat, AffixStat, GearTrait, EnchantKind, Keystones — all LIVE

### 4.1 HunterStat (9) — every stat reaches a sim formula

| stat | producers | sim consumer |
|---|---|---|
| AttackPower | training; MasteryCatalog.cs:173, 282, 283 | Hunter.AttackPower (:441) → AutoDamageMultiplier (:388) → SoloBattle.cs:1763 |
| Focus | training; MasteryCatalog.cs:225, 288 | SoloBattle.cs:2012 (CritMultiplier) |
| Vitality | training; MasteryCatalog.cs:364, 366 | Hunter.RegenPerSecond (:401) → SoloBattle.cs:1211-1214 |
| Engineering | training; MasteryCatalog.cs:284, 285 | Hunter.SquadSkillRate (:434) → Build.Resolve → mods.SkillRate |
| Guile | training; MasteryCatalog.cs:218-225 | Hunter.HaulMultiplier (:440) → mods.Haul → SoloExpedition.cs:418 |
| ResonanceAffinity | training; MasteryCatalog.cs:165-187 | SoloBattle.cs:486 |
| MaxHealth | training; MasteryCatalog.cs:169, 222, 360, 361, 366 | Hunter.MaxHealth (:236) → SoloBattle.cs:2052 |
| Defense | training; MasteryCatalog.cs:362, 363 | Hunter.Defense (:234) → SoloBattle.cs:552 |
| CriticalChance | training; MasteryCatalog.cs:286 | SoloBattle.cs:2003 |

Mastery stat minors reach the hunter through `Hunter.SetMasteryStats` (HunterProgression.cs:192), called from Game/Game1.cs:2757. `Hunter.MasteryBonus(stat)` (:196) has no consumer — dead accessor.

### 4.2 AffixStat (6)

Damage/Health/Haul/SkillRate → `Hunter.WornMods` (HunterProgression.cs:324-327) → Squad* → Build.Resolve. Crit → SoloBattle.cs:2003. Defense → Hunter.Defense (:235). All LIVE. Produced by ItemAffixes.Of, ItemFamilies.BonusOf, GemCraft.

### 4.3 GearTrait (10)

All ten in a pool (GearTraits.cs:95-97; Wild in CharmPool), all ten priced in `ModsFor` (:278-294; Wild is the `_` arm), consumed via `ModsOf` → `Hunter.WornMods` (:327). LIVE. `GearMods.Combine` (GearTraits.cs:18) has no consumer — dead method.

### 4.4 EnchantKind (16)

All in a slot pool (Enchantments.cs:219-237). Eleven map to BuildTriggers (Build.cs:595-611); Fervour/Reverb/Bulwark/Tithe are read as magnitudes at SoloBattle.cs:536-539 and consumed at :748, :763, :758, :767. LIVE. Dead sub-values: `MagnitudeFor(Splinter)` (:276) — the sim uses a flat 0.15 (SoloBattle.cs:697) and the blurb prints no number.

### 4.5 Keystones (19)

Every id is granted by a MemoryDust node (`GrantsKeystone`, MemoryDust.cs:388-466), learned via `DustEffects.LearnedKeystones` (:56-64), socketed by `BuildComposer.Compose` (:145-148). `Mods` → `Build.Resolve` (Build.cs:517); `Grants` → `Build.Triggers` (:587). All 19 LIVE.

---

## 5. Form-keyed dials and the Style-keyed replacement

Everything below still keys on `Abilities.Form` (declared in the legacy `Weaving/ResonanceWeaving.cs:9`). The brief's current model is STYLE → SKILL; the replacement column proposes the minimum move.

| Form-keyed thing | where | proposed replacement |
|---|---|---|
| `SkillShape.FormPower : Dictionary<Form,float>` + `FormPowerFor(Form)` | SkillShape.cs:202-205; producers Character.cs:208 (`Aptitude : Form?`), GearShape.cs:27-29 (`ItemFamilies.FavouredForms : Form[][]`, ItemFamilies.cs:49-58); consumer SoloBattle.cs:811 | `StylePower : Dictionary<Style,float>`; `Character.Aptitude : Style?`; `ItemFamilies.FavouredStyles : Style[][]`; read as `StylePowerFor(sk.Def.Style)` |
| `SkillShape.FormTargets : Dictionary<Form,int>` | SkillShape.cs:187; one producer MasteryCatalog.cs:439 | delete; fold the +1 into the node's `SkillShape.ExtraTargets` scoped by Style, or a `StyleTargets : Dictionary<Style,int>` |
| `SkillShape.TargetsFor(Form)` default baseline = `FormBehaviour.Targets(form)` | SkillShape.cs:425; SoloBattle.cs:1424, 1715, 1943 | `TargetsFor(SkillDef)` reading `def.Targets` — the field already exists and is only honoured on the Active path |
| `Build.Affinity : Form?` ← `MasteryTree.Affinity()` ← `MasteryNode.Form` on Specialisations | Build.cs:441; MasteryTree.cs:253-258; MasteryCatalog.cs:434-448; SoloBattle.cs:709-718, 787-788, 1584-1586, 1711-1713 | `Build.Affinity : Style?`; `FormBehaviour.AffinityFactor(Form,Form)` → a factor table over `SkillCatalogue.RingDistance(Style,Style)` (SkillCatalogue.cs:661 — exists, **unconsumed**) |
| `FormBehaviour.IsAmplifier(form)` / `Heals(form)` | SoloBattle.cs:1376, 1435, 1503, 1705, 1721 | `sk.Def.Effect == SkillEffect.Amplify / Heal` — the dead field is exactly the replacement |
| `FormBehaviour.BaseDamage(form)` ← `WeavingTuning.FormBaseValue : Dictionary<Form,float>` | SoloBattle.cs:1420, 1573, 1707, 1926; ResonanceWeaving.cs:231, 335 | a `SkillDef.BaseDamage` dial (does not exist yet) — the one SkillDef number still owned by the Form table |
| `FormBehaviour.BaseCooldownMs(form)` (Field per-tick scaling for a spilled active) | SoloBattle.cs:1412 | `def.Beats * DefaultBeatMs` / `def.IntervalMs` |
| `FormBehaviour.CooldownBeats(f0)` in the opening-breath block | SoloBattle.cs:528 | block is unreachable for the current catalogue (§7); delete |
| `form == Form.Strike` (Rend, Execute) | SoloBattle.cs:1601, 1613 | `sk.Def.Style == Style.Hammer`, or a SkillDef flag (`SpendsCharge`, `Finisher`) |
| `form == Form.Projectile` (Overdraw) | SoloBattle.cs:1551 | `sk.Def.Style == Style.Volley`, or a `SkillDef.ExtraCasts` dial |
| Weaver's `IsAmplifier/FiresOnBeingHit(woven.Form)` | SoloBattle.cs:1705 | `woven.Def.Kind == Active && woven.Def.Effect == Damage` |
| `BattleEvent.Amount = (int)form` on Skill/Aura events | SoloBattle.cs:1272, 1543, 1558, 1714, 1942 → Game/SoloExpeditionScreen.cs:3280 clip derivation | carry the slot index (or SkillId) and let the screen read `Def.ClipKey`/`Def.FxKey` — makes the dead `ClipKey` live |
| `EnchantNeed.Form` / `Enchantment.NeedsForm` | Enchantments.cs:106, 175-180, 189; Game/WeaveScreen.cs:1362 | `EnchantNeed.Style` (or a SkillKind/Effect predicate for Radiance, which actually keys on Kind) |
| `WeaveContext.DistinctForms` (VOW OF THE SINGULAR) | ResonanceWeaving.cs:529, 512; SoloBattle.cs:2133 | `DistinctStyles` |
| `SoloExpedition.LastWaveTopForm` | SoloExpedition.cs:86 — **never assigned anywhere** | delete |
| `SavedSkill.Form` | Persistence/SaveGame.cs:297 | migration-only; PlayerLoadout.SetSkill already writes `SkillId` beside it (PlayerLoadout.cs:197) |

---

## 6. Consolidation candidates — two (or more) dials doing one job

| job | dials | evidence | proposal |
|---|---|---|---|
| all-damage multiplier | `BuildMods.Damage`, `SkillShape.HitSize`, `SkillShape.DamageDealt` | SoloBattle.cs:705 then :807 `m *= shape.HitSize * shape.DamageDealt` — two shape multipliers on one line, both fed by the same MasteryCatalog | merge HitSize+DamageDealt into one shape dial; keep BuildMods.Damage for the non-skill (swing) path |
| skill rate | `BuildMods.SkillRate` × `SkillShape.SkillRate` (+ orphan `RatePerCreature`, `CastRampPerCast`) | SoloBattle.cs:530, 643, 664, 2144 always multiply the pair | one rate dial; delete RatePerCreature |
| max health | `BuildMods.Health` (SoloBattle.cs:2055) × `SkillShape.MaxHealth` (:2074) | both multiply the same pool in `ChampionHealth`/`VowHealthMultiplier` | one health multiplier |
| per-living-creature damage | `SkillShape.PerCreatureBonus` (:847, chorus) vs `SkillDef.DamagePerLivingEnemy` (:1643, THRONG); also orphan `RatePerCreature`, `SkillDef.SlowPerEnemy` | identical formula `1 + x * alive` at two layers | keep the SkillDef dial; express CHORUS as a StylePower or a delta on the woven skill |
| once-per-wave outright execute | `SkillShape.AssassinateThreshold` (:1621-1628) vs `SkillDef.ExecuteFraction`+`ExecutesPerWave` (:1664-1670) | both `LandOn(target, target.Health, fromSkill:false, ignoresArmour:true)` gated on health fraction, once per wave | one mechanism; ASSASSINATE node becomes a SkillDef-scoped grant or is deleted |
| finisher bonuses | `BuildTrigger.Execute` (×1.6 < 0.30, Strike only, :1613), `CullThreshold/CullBonus` (:830), Shadow signature (:906) | three "harder below a health fraction" rules | at least fold Execute into Cull (same node grants both: MasteryCatalog.cs:435) |
| reflect | `SkillShape.ReflectFraction` (THORNS — raw bite, every biter, :1886) vs `SkillDef.ReflectFraction` (JAWS — mitigated `taken`, :1925) | same name, different base and reach | rename one (`ThornsFraction`) or route THORNS through the Reaction path |
| stun / bite push-back | orphan `StaggerThreshold/StaggerMs` (:1012-1016) vs `SkillDef.StunMs` (:1289-1298) | both `nextBite += X; staggeredThisBite = true` | delete Stagger |
| armour bypass | orphans `OverwhelmFloor`, `ArmourIgnoreFraction`, `CrushArmourMultiple`, `SunderThreshold/Amount` vs live `ArmourPenetration`, `SkillDef.DefenceIgnore`, `SkillDef.SwingIgnoresArmour`, `SkillDef.DefenceBreak*`, Machine signature (:915-919) | nine ways to reduce Defense | delete the five orphans; keep the SkillDef dials + ArmourPenetration |
| lifesteal | `SkillDef.Lifesteal` (:1691), `FormBehaviour.Heals(form)`+`HealTuning.TransformationLeech` (:1721-1731 and :1435-1440), `SkillShape.Leech` (:1157), Nature signature (:1430, 1748, 1948), `SkillDef.SwingLifesteal` (:1766) | base DRINK heals through the Form check, THIRST adds a second heal from the same `dealt` via the dial | give DRINK a base `Lifesteal = 0.12` in the catalogue and delete the `Heals(form)` branch; SIPHON multiplies the dial |
| mark window | `SkillShape.MarkWindowMultiplier` (:1381, 1508), `BuildTrigger.Linger` ×9/5 (:1382, 1507), `SkillDef.AmplifyMs` (:1518), Mind signature (:1738-1743) | four ways to lengthen one window | Linger → a `MarkWindowMultiplier = 1.8` grant |
| mark depth | `SkillShape.MarkPowerBonus` + `FormBehaviour.MarkMultiplier` 1.6 (:786-789) and `markBonus` from `SkillDef.AmplifyPercent/AmplifyPerCast/AmplifyDeepen*` (:797-801) | two independent multipliers on `m` while a mark stands | one amplify channel on the SkillDef |
| cooldown on kill | `SkillShape.CooldownRefundOnKillMs` (Shadow 5pc, :1061) vs `BuildTrigger.LooseAgain` (quiver, :1049) | refund-N-ms vs refund-all | LooseAgain = `CooldownRefundOnKillMs = int.MaxValue`-style grant |
| per-axis bonus | `FormPower` (Form), `SourceBonus` (Source), `AffinityStyleBonus` (affinity Form) | three per-axis multipliers; after the Style move FormPower and AffinityStyleBonus are both per-Style | one `StylePower` dictionary; NARROW writes `StylePower[affinity]` |
| "first" family | `FirstHitMultiplier` (per creature), `FirstCastMultiplier` (per skill per wave), `OpeningSeconds/Bonus` (per wave clock), `FreeOpeningCast`, `FirstBiteFree` | five distinct definitions of "first" | keep as distinct semantics but document; not a delete |

---

## 7. Other dead / unreachable code found on the way (outside the dial table, all with evidence)

| item | evidence | classification |
|---|---|---|
| Opening-breath block SoloBattle.cs:517-534 | writes `champ.ReadyAt[i]` for TakesABeat skills whose Form-native `CooldownBeats == 0`; all six catalogue Actives have `Beats` 6/5/5/4/5/6 (SkillCatalogue.cs:266, 323, 378, 433, 488, 543) so the tick loop takes the `beats > 0` branch (:1454-1474) and never reads `ReadyAt` for them; `ReadyAt` is read only at :1493 (unreachable for current Actives) and :1911 (Reactions) | unreachable for the shipped catalogue; Form-keyed (`FormBehaviour.CooldownBeats(f0)`) |
| Time-counted Active cooldown branch SoloBattle.cs:1475-1495 | same reason; `FreeOpeningCast`'s read at :1492 is on this branch | unreachable for the shipped catalogue |
| `EquippedSkill.CooldownMs` | live only via the Reaction path (SoloBattle.cs:1908) | narrow liveness |
| `MasteryTree.Mods()` | MasteryTree.cs:294 returns `BuildMods.None`; folded at BuildComposer.cs:126 | dead channel by design |
| `Build.Resolve` `fromGear` | Build.cs:519-531 computed then `_ = fromGear` | dead computation |
| `GearMods.Combine` | GearTraits.cs:18; `grep "\.Combine(GearMods\|GearMods.*\.Combine("` → no hits | dead method |
| `Hunter.MasteryBonus` | HunterProgression.cs:196; no callers | dead accessor |
| `SoloExpedition.LastWaveTopForm` | SoloExpedition.cs:86; never assigned | dead property |
| `SoloExpedition.DesperationHaulBonus` fallback | :120, :483 — Desperation only arrives via a worn enchant (magnitude ≥ 0.20) | unreachable constant |
| Harvest `Math.Max(0.10f, …)` floor | SoloBattle.cs:501 — Harvest only arrives via a worn enchant (magnitude ≥ 0.10) | unreachable |
| `Enchantments.MagnitudeFor(Splinter)` | Enchantments.cs:276 vs flat 0.15 at SoloBattle.cs:697 | dead value |
| `Weaving.AbilityPower` | ResonanceWeaving.cs:344; only tests/…/WeavingTests.cs call it | legacy, test-only |
| `SkillCatalogue.RingDistance` / `Opposite` | SkillCatalogue.cs:661-670; zero consumers in src | replacement written, never wired |
| Stale comments | SoloExpedition.cs:486-488, WaveModel.cs:318, 333-335 say Cores are unconsumed; Game/Game1.cs:3646-3648 consumes them | doc drift |

---

## 8. Serialization risks touched by this area

| field | risk | migration |
|---|---|---|
| `SavedSkill.Form` (SaveGame.cs:297) | the Form bridge is the only thing an old save has to name a skill; deleting Form from runtime orphans it | keep a read-only migration `Form → SkillId` via `SkillCatalogue.Resolve(form, passive)` (SkillCatalogue.cs:646-655); PlayerLoadout already writes SkillId beside Form |
| `SaveGame.cs:158` legacy Form affinity | mastery tree owns affinity now; a Style-typed Affinity changes the persisted `spec_*` node ids only if those nodes are renamed | keep node ids (`spec_strike` …) even if `MasteryNode.Form` becomes `Style` |
| SkillProgress `Variation` / `Reinforcements` stored by **name** (SaveGame.cs:706-709) | renaming any of the 24 variation or 72 reinforcement names strips a player's purchases | never rename; or add an id column |
| `ItemInstance.EnchantOverride`, `TraitOverride` | Enchantment/trait enums are persisted by name; removing an EnchantKind breaks saves | none of the 16 are dead, so no action |

---

## 9. Open questions

1. `SkillDef.Line` for DRINK says "heals you for 50% of it" (SkillCatalogue.cs:542) while the heal actually applied is `HealTuning.TransformationLeech = 0.12` (HealTuning.cs:83, SoloBattle.cs:1729). Text/number drift — out of this audit's scope but found while tracing Lifesteal.
2. Should the nine never-produced orphans be deleted outright, and the nine test-only ones deleted with their tests? A false "dead" is costly; here the evidence is that no runtime code path can set them, and the tests that set them exist to prove sim code for nodes that were removed on 2026-08-30.
3. `Radiance` keys on `SkillKind.Field` in the sim (SoloBattle.cs:1261) but on `Form.Aura` in `EnchantNeed` (Enchantments.cs:177). Which is the intended semantics — "AURA ticks faster" or "every Field ticks faster"?
4. `Execute` and `Rend` fire only on a **cast** with `form == Form.Strike`; PRESS carries `Form.Strike` (PlayerLoadout.cs:194-196) so the Forge's combo check passes for a build that can never reach the branch. Intended?
5. `SkillDef.Targets` is honoured only on the Active path. Is it meant to govern Field/Reaction reach too (it disagrees with the Form table for none of the twelve today)?
6. `ReactionOn.LowHealth` / `WaveStart` have no producer and no dispatch — reserved for future skills, or delete?
7. `CooldownMultiplier` is read only on the Reaction path; a future Active reinforcement that sets it would be silently ignored. Should the Active path read it (or should it be renamed `TrapRearmMultiplier`)?
