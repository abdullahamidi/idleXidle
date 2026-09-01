# Test-suite map — refactor audit 2026-08-31

Branch `feat/hunter-cutout-rig`, HEAD `ae30f2a`. READ-ONLY audit; the only file written is this one.

## 0. Ground truth

| Fact | Evidence |
|---|---|
| 120 test `.cs` files: 119 unit + 1 integration | `find tests -name "*.cs"` (excluding bin/obj) |
| 994 test METHODS declared (`[Fact]` + `[Theory]`) | `grep -nE "public (async )?(void|Task) test_"` per file → 994 (`scratchpad/test_names.txt`) |
| **1225 discovered test cases** in the unit project | `dotnet test tests/unit/ResonanceHunter.Core.Tests --list-tests` → 1225 lines (`scratchpad/list_tests.txt`). The 231 extra cases are Theory expansion, dominated by four MemberData liveness theories (73 + 52 + 24 + 18). |
| **Suite is green at HEAD**: 1225 passed, 0 failed, 0 skipped, 1 m 07 s | `dotnet test ... --no-build` (`scratchpad/run_tests.txt`) |
| 2 integration facts | `tests/integration/ResonanceHunter.Integration.Tests/FullLoopTests.cs:33,99` |
| No `Skip=`, no `[Trait(...)]` categories anywhere | `grep -nE "Skip\s*=|\[Trait\("` over tests → 0 hits |
| Both test csproj reference ONLY `src/ResonanceHunter.Core/ResonanceHunter.Core.csproj`; no Game reference (headless by design) | `tests/unit/.../ResonanceHunter.Core.Tests.csproj:24`, `tests/integration/.../ResonanceHunter.Integration.Tests.csproj:24` |
| CI runs unit then integration with `-warnaserror`; paths hardcode `tests/unit/ResonanceHunter.Core.Tests` and `tests/integration/ResonanceHunter.Integration.Tests` | `.github/workflows/ci.yml:26-36` |
| 424 `using ResonanceHunter.*` lines and 120 `namespace ResonanceHunter.*` declarations in tests; 49 files import `ResonanceHunter.Core.Abilities` (the Weaving namespace) | `grep -h "^using ResonanceHunter" tests` → 424; `grep -l "using ResonanceHunter.Core.Abilities"` → 49 |

### The architectural fact every classification below hangs on

The "current" skill model is still bolted onto the legacy one at the type level. `EquippedSkill` is
`record EquippedSkill(WovenAbility Ability, int CooldownMs, bool? PassiveSlot)` and resolves its
`SkillDef` through the **Form bridge**: `SkillCatalogue.Resolve(Form, Passive)`, then applies
`Variation.Modify` and each `Reinforcement.Modify` (`src/ResonanceHunter.Core/Builds/Build.cs:206-261`).
`WovenAbility` is still `{Name, Source, Form, Vow?}` (`src/ResonanceHunter.Core/Weaving/ResonanceWeaving.cs:257-263`).
`BuildComposer.SkillPick` is `(Source, Form, VowId, Name, bool? Passive, string? SkillId = null)` — the
SkillId is an optional override added on 2026-08-30 (`src/ResonanceHunter.Core/Builds/BuildComposer.cs:35-36`).
`SkillDef` carries `Form? LegacyForm` "a migration bridge and nothing more" (`SkillCatalogue.cs:150-155`).

Consequence: **there is no test in the repo that constructs a skill without naming a `Form`.** Every
fixture — `Sk(Form)`, `Skill(name, Form, Source)`, `SkillPick(Source, Form, ...)` — reaches the current
catalogue through the bridge. That is why REWRITE is the largest category below even though most of
those files pin current, valuable behaviour.

## 1. Category totals

Counts are DISCOVERED cases (Theory-expanded), summed per file from `list_tests.txt`.

| Category | Files | Cases | Meaning |
|---|---|---|---|
| KEEP | 67 | 605 | pins current architecture / a liveness chain; no legacy vocabulary in the fixture (or a single trivial `WovenAbility` site, noted) |
| REWRITE (fixture) | 33 | 359 | pins current behaviour, but constructs the build through `WovenAbility`/`Form`/`SkillPick(Form)` or measures through `FormBehaviour`/`Weaving.*`; assertions survive, the top-of-file helper changes |
| REWRITE (split) | 11 | 218 | file mixes live tests with Form-bound ones; delete the Form half, keep the rest — listed per test in §2 |
| DELETE | 4 | 9 | tests a system slated for removal (Form affinity ×2, family→Form favour, one-Form-discipline). Form-aptitude tests are inside split file `characters_roster_test.cs` |
| MIGRATION-FIXTURE | 3 | 34 | compatibility fixtures for old saves; keep, extend with a Form→SkillId case |
| fixture-only file | 1 (`taught_tree.cs`) | 0 | shared `Taught.Everything()` helper |
| integration | 1 | 2 | economy loop; untouched by the skill refactor |

(Split files are counted once, in the split row; their per-test fate is in §2. Totals: 67+33+11+4+3+1 = 119 unit files; 605+359+218+9+34 = 1225 cases — both checked against `list_tests.txt`.)

## 2. Per-file table

Columns: cases = discovered; Form/WA/Weav = raw grep hit counts for `\bForm\b`-style tokens, `WovenAbility`, `Weaving\.` in that file (from `grep -c`); Fate; why.

### Animation/ — KEEP (20)
| File | Cases | Fate | Note |
|---|---|---|---|
| `Animation/ClipTests.cs` | 9 | KEEP | Core/Animation Clip sampling, determinism (`test_sampling_is_deterministic`). No legacy tokens. |
| `Animation/RigTests.cs` | 11 | KEEP | Rig hierarchy, snap grid, determinism. |

### Automation/ — KEEP (7)
| `Automation/RegionFarmTests.cs` | 7 | KEEP | Region record that survived the creature-farm retirement (header, :7-10). Namespace `Automation` is a stale folder name; the content is live (`Region.RecordActiveKill`, depth record). |

### Builds/ (the refactor's centre — 479 cases in 34 files)
| File | Cases | Form/WA/Weav | Fate | Note |
|---|---|---|---|---|
| `affinity_test.cs` | 3 | 16/3/0 | **DELETE** | Form specialisation affinity: `new Build { Affinity = form }`, `FormBehaviour.BaseCooldownMs`, MARK SPECIALIST (:32-54). Property worth re-authoring against Style if a Style specialisation survives. |
| `affinity_vow_buyback_test.cs` | 2 | 6/1/1 | **DELETE** | `FormBehaviour.AffinityFactor(affinity, skill, vowSworn: true)` hexagon buy-back (:27-28). Form-only concept. |
| `BalanceSweepTests.cs` | 8 | 7/4/6 | REWRITE-fixture | Balance of branches/roads/vows (KEEP intent). Fixture `Sk(Form, Source.Spirit)` :58, `BuildWith` weaves `Strike/Projectile/Aura/Mark` :63-73, `MidCareerHunter` :89. |
| `beat_cadence_test.cs` | 11 | 32/1/0 | REWRITE-fixture | Beat counter and readout pinned together (KEEP intent). `OneRhythmSkill(Form)` :42, `FourSkillLoadout` :264, `Taught.Everything()` + `Compose`. |
| `break_badge_test.cs` | 3 | 4/0/0 | KEEP (minor) | Defence-break publish → replay state. Uses `Compose`+`Taught` with `SkillPick(Form)` twice — trivial edit. |
| `build_glossary_test.cs` | 6 | 5/0/3 | REWRITE-split | KEEP: `test_the_strong_and_weak_claims_match_what_the_fight_actually_does` (Source table, :29), `test_an_items_trait_effect_states_its_real_numbers`, `test_every_source_line_names_the_regions_lever`. DELETE: `test_every_form_is_described_and_the_numbers_are_the_real_ones` (:56, `BuildGlossary.FormHeadline/FormRule`), `test_the_form_that_deals_no_damage_says_so` (:79), `test_the_form_that_only_pays_when_attacked_says_so` (:90). |
| `BuildTests.cs` | 19 | 4/1/3 | REWRITE-split | Keystone laws, slot capacity, triggers — KEEP. Fixture `Skill(name, Form, Source)` :21. DELETE `test_there_are_more_forms_than_slots` (:156, asserts `Enum.GetValues<Form>().Length > Build.SkillSlots`). `test_the_vow_of_completion_is_meetable_during_onboarding` uses `Weaving.Catalog`/`Weaving.IsActive` (:280-304) — KEEP, Vows stay. |
| `champion_starting_skill_test.cs` | 2 | 2/0/0 | REWRITE-fixture | Current model (StartingSkillId → composed build). Reaches the skill through the "LegacyForm door" (:47-56) — should become `SkillPick(SkillId:)`. |
| `charge_keystone_test.cs` | 6 | 6/3/0 | REWRITE-fixture | **Not Form-bound.** REND/CAPACITOR/DYNAMO/LODESTONE are `BuildTrigger` keystones with `Mods` + `Grants` and no Form requirement (`Keystones.cs:142-146`). Fixture `TwoSkills` weaves `WovenAbility{Form.Strike}`/`{Form.Projectile}` (:21-31) only to have two casters. KEEP the tests, swap the fixture. |
| `DamageBenchTests.cs` | 4 | 1/1/0 | REWRITE-fixture | `DamageBench` determinism + "did that change the damage". `StrikeBuild` :14 uses `WovenAbility`. |
| `family_form_affinity_test.cs` | 3 | 8/1/0 | **DELETE** | Weapon family → favoured Forms (`ItemFamilies.FavouredForms`, `GearShape.Of(h).FormPowerFor(Form.Projectile)` :48-58). Re-author only if families get re-pointed at Styles. |
| `heal_balance_test.cs` | 9 | 12/1/0 | REWRITE-fixture | Heal ceiling / blood magic / siphon (KEEP intent). Fixture `Sk(Form, Source)` :70, `Morphs`/`NatureAuras` heal builds are Transformation/Aura Forms; second copy of `MidCareerHunter` :52. |
| `HunterStatWiringTests.cs` | 3 | 1/1/0 | REWRITE-fixture | CRIT/FOCUS/DEFENSE reach the fight. `StrikeBuild` :18 uses `WovenAbility`. |
| `live_build_test.cs` | 4 | 6/1/0 | REWRITE-fixture | `SoloExpedition.ReplaceBuild` mid-run. `Sk(Form)` :19. |
| `loot_node_liveness_test.cs` | 18 | 4/0/0 | REWRITE-fixture | LOOT branch chain (KEEP intent). `Compose` with `SkillPick(Source, Form, ...)` :62. |
| `mastery_new_nodes_liveness_test.cs` | 7 | 4/1/0 | REWRITE-fixture | 12 nodes of 2026-08-27 measured at end of chain. `Sk(Form)` :29, `BuildWith(shape, params Form[])` :33. |
| `mastery_node_liveness_test.cs` | 52 | 5/0/0 | REWRITE-fixture | RESONANCE/TEMPO/ENDURE chain (KEEP intent). `Compose(mastery, oneSource)` :88-100 uses four `SkillPick(Source, Form.*)`; poses a `Specialisation` node (:124-128) which is a Form node today. |
| `mastery_points_test.cs` | 12 | 0 | KEEP | Mastery-point curve. |
| `MasteryLayoutTests.cs` | 11 | 0 | KEEP | Node layout / no overlap. |
| `MasteryTreeTests.cs` | 27 | 3/0/0 | REWRITE-split | KEEP 25. DELETE/REWRITE: `test_only_form_combo_triggers_are_granted` (:470, whitelist = Execute/Coiled/Overdraw/Radiance/Linger/Siphon), `test_every_form_has_one_specialisation` (:484, `specs.Select(n => n.Form)`). `test_the_whole_tree_costs_two_hundred_and_fifty_six` pins 316 incl. 6 specialisations × 6 + 12 roads × 5 (:79-88) — will move if specialisation nodes change shape. |
| `one_discipline_test.cs` | 1 | 2/0/0 | **DELETE** | `tree.Affinity() == Form.Strike`, `spec_strike`/`spec_trap` (:19-38). Re-author for Style if one-specialisation rule survives. |
| `PassiveTreeTests.cs` | 9 | 1/1/0 | REWRITE-fixture | **Model chain test** (MemoryDust purchase → damage). Fixture `DamageDealt` weaves one `WovenAbility{Form.Strike}` :66-70. |
| `reinforcement_liveness_test.cs` | 73 | 1/0/0 | REWRITE-fixture | **Core of the current model**: every reinforcement moves damage or health. `Build(def, variation, reinforcement)` :64 picks via `def.LegacyForm` / sibling's LegacyForm — needs `SkillPick(SkillId:)`. |
| `skill_catalogue_test.cs` | 17 | 6/0/0 | REWRITE-split | KEEP 14 (two per style, ids stable, beats, ring distance, variations×reinforcements). MIGRATION/DELETE when the bridge goes: `test_every_saved_form_resolves_in_both_slots` (:167), `test_a_spilled_active_becomes_its_styles_passive_skill` (:184). Check `test_no_name_is_a_style_name_or_uses_the_champion_form` (:237). |
| `skill_progress_test.cs` | 10 | 5/0/0 | REWRITE-fixture (trivial) | Current model (levels, variation, reinforcement, save). Four `SkillPick(Source.Body, Form.Strike, ...)` + `Taught.Everything()` (:136-165). |
| `skill_slot_kinds_test.cs` | 30 | **73**/5/0 | REWRITE-split | Heaviest Form footprint. KEEP: slot budgets, beat demand, swing share, reachability (`SkillReachabilityTests` :624). MIGRATION/DELETE: `test_every_legacy_form_resolves_inside_its_own_style` (:58), `test_the_form_table_now_answers_from_the_catalogue` (:597, `FormBehaviour.CooldownBeats` vs catalogue), `test_an_old_four_active_build_keeps_all_four_skills` (:203, the spill migration), `test_aura_and_trap_still_cost_no_beat_after_the_split` (:284), `test_a_spilled_transformation_becomes_wilt...` (:446). Fixtures `Skill(Form, passive)` :34, `S(name, Form)` :170, `OneSkill(Form, passive)` :496, `WithCooldown(Form, ms)` :561. |
| `skill_stagger_test.cs` | 1 | 2/1/0 | REWRITE-fixture | CastGap lock. `Sk(Form)` :25. |
| `SkillShapeBattleTests.cs` | 17 | 27/1/0 | REWRITE-fixture | **Model chain test** (shape → sim). `Sk(Form)` :32, `BuildWith(shape, params Form[])` :36. |
| `slot_split_balance_test.cs` | 1 | 2/1/0 | REWRITE-fixture (or DELETE) | Measures OldModel (4 actives) vs TwoAndTwo (:56-67). A one-time migration probe kept as a test; the "old model" it compares against no longer exists. |
| `SoloBattleTests.cs` | 47 | **107**/1/6 | REWRITE-split | Header: "keep the Forms DIFFERENT" (:12-18). Fixture `Sk(Form, Source, Vow)` :25, `With(params Form[])` :37, `SkillCasts(e, Form)` :55 (asserts `BattleEvent.Amount == (int)form` — an event-encoding coupling). Split: (a) Form-identity tests :97-200 (projectile volume, aura no cooldown, trap pays on bite, mark amplifies, transformation heals) → REWRITE as SkillDef/Style identities; (b) Form-combo enchant tests :437-600 (`overdraw`, `radiance`, `linger`, `execute`, `coiled`, `siphon`) and `test_the_new_combos_do_nothing_without_their_form` :755 → DELETE with the Form enchants; (c) affinity tests :405-435 → DELETE; (d) keystones (bloodlust/juggernaut/undying/glass cannon), vows (:325-383, :767-810), armour/swarm/overkill (:815-967), source matchup :303 → KEEP with fixture swap. |
| `SoloExpeditionTests.cs` | 17 | 21/1/0 | REWRITE-fixture | Across-waves loop (KEEP intent). `Sk(Form, Source)` :22, `BuildOf(mods, params Form[])` :26. |
| `source_signature_test.cs` | 6 | 20/6/3 | REWRITE-fixture | Source signatures (Body wound ramp, Shadow below-half, Machine armour bend, Nature heal, Spirit prime, Mind mark window). **Source is a live concept** (each variation carries one; `SoloBattle.cs` reads `Weaving.SourceEffectiveness`). Control pairing searched from `Weaving.SourceEffectiveness` :24-27; `OneSkill(Source, Form)` :29. Mind's signature "stretches the mark window" is Form-shaped. |
| `taught_tree.cs` | 0 | 0 | KEEP (fixture) | `Taught.Everything()` restores all `MasteryKind.SkillRoad` nodes (:31-39). Used by 6 files (13 sites). |
| `TriggerLivenessTests.cs` | 16 | 8/2/0 | REWRITE-split | KEEP 14 + the hand-maintained index `test_every_trigger_is_claimed_by_a_test_in_this_file` (:115-158). The index names six Form-combo triggers (Overdraw/Linger/Radiance/Execute/Coiled/Siphon → proofs in SoloBattleTests) — shrink when those enums go. `Strike(Form)` :41. |
| `variation_liveness_test.cs` | 24 | 2/0/0 | REWRITE-fixture | **Core of the current model**: every variation moves damage or health, 6-wave real expedition (:97-118). Same LegacyForm-door pick as reinforcement_liveness (:78-93). |

### Characters/ + root (63)
| File | Cases | Fate | Note |
|---|---|---|---|
| `Characters/champion_tiers_test.cs` | 22 | KEEP | Tiered roster, quest gates evaluable from a snapshot. |
| `Characters/roster_grid_test.cs` | 6 | KEEP | Roster grid data. |
| `Characters/roster_parity_test.cs` | 5 | REWRITE-fixture | Ten-character gauntlet (balance). `BuildFor(Character, Form, Vow)` :134-160 uses `FormBehaviour.IsAmplifier(form)` and `WovenAbility`; `test_single_target_throughput_per_form_is_reported` is Form-axis. |
| `characters_roster_test.cs` | 16 | REWRITE-split | KEEP 13. **DELETE** `test_an_aptitude_lifts_only_its_own_form` (:123, `quiver.Aptitude == Form.Projectile`, `FormPowerFor`), `test_aptitudes_compound_with_a_tree_node_on_the_same_form` (:135, `SkillShape.FormPower`). REWRITE `test_twice_sworn_actually_makes_a_vow_pay_more` (:162, asserts `MarkWindowMultiplier > 1` — Form-based set/passive bonus). `DamageWith` fixture :188 uses `WovenAbility`. |
| `quests_test.cs` | 14 | KEEP | Quest catalogue + `CharacterState` chain (:91-115). |

### Economy/ (176)
| File | Cases | Fate | Note |
|---|---|---|---|
| `attrition_test.cs` | 6 | REWRITE-fixture | Idle ladder measurement (KEEP intent). `StarterBuild` :74 uses `WovenAbility`. |
| `charters_test.cs` | 8 | KEEP | Forge charters drop/spend/reload. |
| `element_sets_test.cs` | 12 | REWRITE-fixture | Source sets 2/3/4/5 reach the fight (KEEP intent; Source is live). `Sk(Source, Form)` :55, `BuildOf(Source, params Form[])` :59; `test_mind_three_crits_more_and_five_stretches_the_mark` reads a Form-shaped bonus (`ElementSets.cs:89 MarkWindowMultiplier = 1.5f`). |
| `EnchantmentsTests.cs` | 15 | REWRITE-split | KEEP 11 (data model, pool per slot, magnitude by rarity, blurbs). DELETE/REWRITE: `test_every_form_has_a_combo_enchantment` (:116, `Enchantment.NeedsForm`), `test_every_combo_enchantment_names_what_it_needs` (:185, ten combo kinds incl. six Form ones), `test_a_requirement_is_met_only_by_the_axis_it_names` (:207, `MetBy(Form[], ...)`). Index `test_every_enchantment_is_claimed_by_a_test...` (:150-184) must shrink with the enum. |
| `first_gem_free_test.cs` | 6 | KEEP | |
| `GearTests.cs` | 11 | REWRITE-fixture (1 site) | `test_gear_makes_you_measurably_stronger_in_a_real_fight` :105 weaves a `WovenAbility`. |
| `GearTraitsTests.cs` | 19 | KEEP | Names still say "squad" (`test_wearing_a_charm_makes_the_squad_tougher` :211, `..._reaches_the_squad` :256) — stale term, live assertion (`Hunter.SquadHealthMultiplier` is still the property name). |
| `gem_craft_test.cs` | 14 | KEEP | |
| `gleam_economy_test.cs` | 4 | REWRITE-fixture | Faucet vs sink as play time. `StarterBuild` :82. |
| `item_classes_test.cs` | 19 | KEEP | |
| `item_families_test.cs` | 4 | KEEP | 0 Form tokens; the Form half of families lives in `family_form_affinity_test.cs`. |
| `ItemAffixesTests.cs` | 7 | KEEP | Affix → worn mods → defense chain. |
| `ItemNamingTests.cs` | 6 | KEEP | |
| `loot_rate_test.cs` | 3 | KEEP | |
| `pacing_test.cs` | 4 | REWRITE-fixture | First-hour pacing. `StarterBuild` :38. |
| `power_scale_test.cs` | 6 | KEEP | |
| `PowerContributionTests.cs` | 7 | KEEP | |
| `PowerRatingTests.cs` | 3 | REWRITE-fixture | POWER agrees with depth. One `WovenAbility` site. |
| `progression_curve_test.cs` | 3 | REWRITE-fixture | One `WovenAbility` site. |
| `training_respec_test.cs` | 5 | KEEP | |
| `vitality_regen_test.cs` | 4 | KEEP | |
| `wandering_trader_test.cs` | 6 | KEEP | |
| `wave_length_test.cs` | 4 | REWRITE-fixture | `FourSkill` :54 uses `WovenAbility`. |

### Encounters/ (60) — all KEEP
| File | Cases | Note |
|---|---|---|
| `AffixLivenessTests.cs` | 8 | Band affixes through `SoloExpedition.PushWave`, direction not magic number. One `WovenAbility` in `BuildWith(shape)` :38 (fixture swap). |
| `BandCycleTests.cs` | 10 | Includes `test_the_wave_seed_is_stable_across_processes` :185. |
| `checkpoints_test.cs` | 5 | |
| `CorruptionScalingTests.cs` | 7 | |
| `EncounterSpawnerTests.cs` | 6 | Header :7-12 says only the three pure static helpers survive; `ReferenceTeamEffectiveDps` is a stale "team" term. |
| `region_drops_test.cs` | 4 | |
| `RegionModifiersTests.cs` | 3 | |
| `WorldTests.cs` | 17 | |

### Expeditions/ (65)
| File | Cases | Fate | Note |
|---|---|---|---|
| `chest_dossier_test.cs` | 9 | KEEP | Opens thousands of real chests against the dossier's promises. |
| `chest_filter_test.cs` | 5 | KEEP | |
| `ChestTests.cs` | 13 | KEEP | |
| `gift_chest_test.cs` | 8 | KEEP | |
| `RunLogTests.cs` | 5 | KEEP | |
| `RunReportTests.cs` | 7 | REWRITE-fixture | `Sk(Form)` :19, `BuildOf(params Form[])` :23; 14 Form tokens (small-hit vs single-target builds). |
| `wave_spoils_test.cs` | 4 | KEEP | |
| `WaveReplayTests.cs` | 8 | REWRITE-fixture | Replay agrees with sim. `Sk(Form)` :28; header :8-11 mentions "multi-slot shielding" from the squad era. |
| `WeaveInBattleTests.cs` | 6 | REWRITE-split | KEEP: Source table (:30), Vow pricing (:46), boss-wave scaling (:99-124 — misfiled here). REWRITE: `test_the_sim_describes_a_build_the_vows_can_read` (:67-95) asserts `vow_singular`/`vow_pure` via `DistinctForms` — the `VowDemand.SingleForm` demand (`ResonanceWeaving.cs:53-54`) is Form-based. |

### Forge/ (69) — all KEEP
`ForgeTests.cs` 21, `MaterialTests.cs` 6, `MergeRecipeTests.cs` 14, `refine_ladder_test.cs` 6, `reforge_payment_test.cs` 6, `ReforgeTests.cs` 7 (header :5-11 calls the enchant a "build-defining Form-combo"; assertions are pool-invariant, so the tests survive the enchant list shrinking), `SalvageTests.cs` 9. Namespace is `Tests.Forging` on purpose (shadowing note in `SalvageTests.cs:5-7`).

### Loot/ (28)
| `LootElementTests.cs` | 5 | KEEP | Region element reaches loot. |
| `LootSystemTests.cs` | 12 + 7 (`HunterProgressionTests`) | KEEP (17) / DELETE (2) | `test_automation_dampens_rarity...` (:93) and `test_skilled_active_play_yields_more_items_than_an_automated_kill` (:134) drive `KillContext.AutomationStage`, which `LootSystem.cs:386` reads but nothing in `src/` ever sets (`grep -rn "AutomationStage\s*=" src` → 0; writers are `LootSystemTests.cs:22`, `FullLoopTests.cs:111` only). Test-only branch — see §9 item 4. |
| `SplinterPaysTests.cs` | 4 | REWRITE-fixture | `BuildWith(params BuildTrigger[])` :41 weaves a `WovenAbility`. |

### Persistence/ (56)
| File | Cases | Fate | Note |
|---|---|---|---|
| `build_stamp_test.cs` | 1 | KEEP | |
| `dismissed_guide_test.cs` | 2 | KEEP | |
| `gift_chest_save_test.cs` | 2 | KEEP | |
| `legacy_trait_migration_test.cs` | 3 | MIGRATION-FIXTURE | Pre-redesign item trait migration (`TraitOverride` null vs "NONE" sentinel). |
| `onboarding_save_test.cs` | 2 | KEEP | |
| `save_store_test.cs` | 9 | KEEP | Deliberate file IO (header :12-15); temp dir prefix `rh_savestore_` (:30) is a stale product prefix, harmless. |
| `SaveSystemTests.cs` | 19 + 4 (`WorldSaveTests`) | MIGRATION-FIXTURE + REWRITE | Hand-written legacy JSON fixtures: `test_a_save_carrying_retired_creature_fields_still_loads...` (:69-168), `test_a_legacy_single_region_save_still_loads` (:537). **`test_a_saved_build_survives_a_reload` (:317-357) asserts `SavedSkill{Source,Form,VowId}` string round-trip** — a link test on the record, not on a reconstructed build; keep as the Form→SkillId migration fixture and add a SkillId round trip beside it. |
| `share_codes_test.cs` | 6 | REWRITE (1 test) | `test_share_codes_build_round_trips_skills_keystones_and_mastery` (:57-83) encodes `SavedSkill{Form="Strike"/"Aura"}` — follows whatever `SavedSkill` becomes. |
| `unlocked_characters_test.cs` | 8 | MIGRATION-FIXTURE | Pre-tier saves seeded from old gates; legacy table pinned (:150). |

### Presentation/ (20)
| `CanvasFitTests.cs` | 15 | KEEP | |
| `hunt_screen_feedback_test.cs` | 5 | KEEP, **path-fragile** | Reads `src/ResonanceHunter.Game/SoloExpeditionScreen.cs` as text by walking up from the test binary (:30-40). Breaks the moment the Game project directory is renamed. |

### Prestige/ (81) — all KEEP
`DustEffectsTests.cs` 24 (wired-ids ↔ catalog both directions :33-58; `test_the_bound_hand...` :351 goes through `BuildComposer.Compose` + `Taught`), `MemoryDustTests.cs` 12, `MemoryDustTextTests.cs` 8, `TraitDescriptionsTests.cs` 12, `TraitNamesTests.cs` 25. Uses `Weaving.Catalog` for vows (DustEffectsTests :251-298) — follows wherever the Vow catalogue moves.

### Progression/ (66) — all KEEP
`EfficiencyContractTests.cs` 5 (header :4-8: active-vs-idle half already removed), `gem_tour_test.cs` 10, `onboarding_test.cs` 18, `PointIncomeTests.cs` 4, `RarityTiltTests.cs` 3, `tutorial_dismissal_test.cs` 6, `tutorial_test.cs` 12 (career walk, header :5-16), `unlocks_test.cs` 8.

### Warrens/ (16)
| `WarrenTests.cs` | 16 | KEEP (link-only) | Every assertion is on `Warren` itself (levels, milestones, `ProductionPerMinute`, restore). Nothing asserts that Warren output reaches a player wallet; consumers are `Game1.cs:764,1088 _warren.Tick(...)` and `WarrenScreen.cs` only. `WarrenResource.Mastery` is displayed as INSIGHT (`WarrenScreen.cs:94,104,117`) — vocabulary split between enum and UI. |

### Weaving/ (19)
| `WeavingTests.cs` | 19 | REWRITE-split | Namespace `Tests.Abilities`. KEEP (live in the fight — `SoloBattle.cs` calls `Weaving.SourceEffectiveness`, `Weaving.VowMultiplier`, `Weaving.IsActive`): the 4 Source-table tests (:20-60) and the Vow pricing/catalogue tests (`test_a_rarer_vow_condition_always_grants_more_power` :72, `..._rejected` :90 Theory×4, `..._priced_identically` :95, `test_no_static_vow_dominates_another` :166, `test_vow_fragility...` :185, `test_every_catalog_vow_is_well_formed` :277). REWRITE: `test_every_demand_can_be_both_met_and_unmet` (:115, `WeaveContext.DistinctForms`), `test_a_demand_does_not_depend_on_how_the_fight_goes` (:148, reflection over `WeaveContext` — fine as is). **DELETE** (the free Source×Form power formula): `test_a_conditional_vow_grants_no_power_when_its_condition_is_unmet` (:197, `Weaving.AbilityPower`/`BasePower(Form.Strike)`), `test_a_static_vow_applies_regardless_of_condition` (:216), `test_the_matchup_wins_ties_but_no_longer_beats_real_investment` (:246), `test_mark_deals_no_direct_damage` (:272). Note `FormBehaviour.cs` still calls `Weaving.BasePower` and `StatsScreen.cs` reads it, so BasePower is live until FormBehaviour goes. |

### Integration
| `FullLoopTests.cs` | 2 | KEEP | Loop: `LootSystem.Roll` ×20 → sell (`Forge.IsEligible`, `SellValue`) → `hunter.Train(AttackPower)` → `AutoDamageMultiplier` moves → `Region.RecordActiveKill`/`RecordDepth` → `SaveSystem.Capture/Serialize/Deserialize/RestoreHunter` with 2 h offline (:33-96). Second fact: 40 000 low-tier rolls reach all 5 rarities (:99-119). **Touches no skill, build, mastery, trait, warren or quest** — it is an economy loop, not "the whole game" its header claims (:2-3). Static shared `Rng` field :30 (only one test uses it; fine). |

## 3. LINKS vs CHAINS

A LINK test asserts a record was set; a CHAIN test asserts the game changed. Five link examples worth knowing before writing new tests:

1. `characters_roster_test.cs:146-158 test_every_character_changes_something_the_sim_reads` — passes if ANY of Mods/Shape/Grants/Aptitude is non-default. `roster_parity_test.cs:5-8` header records that THE OATHBOUND sailed through it while half its passive did nothing.
2. `MasteryTreeTests.cs:350-357 test_every_node_changes_a_shape` — `node.Shape != SkillShape.None || node.Grant is not null || node.Stats.Count > 0 || node.GrantsSkillId is not null`. Declares, does not run. (The chain version is `mastery_node_liveness_test.cs`.)
3. `SaveSystemTests.cs:317-357 test_a_saved_build_survives_a_reload` — asserts `back.WovenSkills[1].Form == "Trap"` etc.; never composes the restored build and never fights with it.
4. `BuildTests.cs:176-182 test_a_keystone_grants_its_behaviour` — `Assert.Contains(BuildTrigger.NoHealing, build.Triggers(hunter))`. The trigger being read is proven elsewhere (`TriggerLivenessTests.cs:312 test_no_healing_switches_transformation_off`).
5. `WarrenTests.cs` (all 16) — every assertion is on the Warren object (`ProductionPerMinute > 0`, `Multiplier`, `UpgradeCost`); nothing asserts a wallet moved. `EnchantmentsTests.cs:116 test_every_form_has_a_combo_enchantment` is the same species (catalogue completeness).

Strongest CHAIN tests to model new ones on:

- `variation_liveness_test.cs:64-135` and `reinforcement_liveness_test.cs:64-174` — subject vs identical baseline, real `SoloExpedition` for 6 waves, "damage OR health must move", one Theory row per catalogue entry via MemberData so a new entry is tested by existence.
- `mastery_node_liveness_test.cs:88-150` — same shape plus `hunter.SetMasteryStats(mastery.Stats())` ("the same push Game1 makes every frame") and two contexts (one-source / mixed).
- `PassiveTreeTests.cs:43-105` — starts at `MemoryDustTree.Purchase`, ends at damage dealt, "refuses to know anything about the middle".
- `TriggerLivenessTests.cs:115-158` / `EnchantmentsTests.cs:150-184` — hand-maintained "every enum member is claimed by a named proof" indexes; adding an enum value fails the build with the name of the proof you owe.
- `AffixLivenessTests.cs` — goes through the real `PushWave` re-mint seam and asserts direction, not a magic number.
- `tutorial_test.cs:42 test_every_step_is_displayed_on_a_real_career` — moves facts the way play moves them (header :5-16 explains why the hand-authored version passed while the guide was deadlocked).
- `champion_starting_skill_test.cs:36-67` — composes each champion's starting skill through `BuildComposer.Compose` on a bare tree and asserts `build.Skills.Count == 1`.

## 4. Shared fixtures the refactor must keep working

There is exactly ONE shared helper class in the whole suite: `Builds/taught_tree.cs` `internal static class Taught` (`grep -rnE "static class" tests` → 1 hit). Everything else is a per-file `private static` fixture, re-implemented file by file:

| Fixture shape | Definitions | Files |
|---|---|---|
| `Sk(Form ...) => new EquippedSkill(new WovenAbility{...}, FormBehaviour.BaseCooldownMs(form))` | 12 near-identical copies | SoloBattleTests:25, SoloExpeditionTests:22, BalanceSweepTests:58, heal_balance:70, live_build:19, mastery_new_nodes:29, SkillShapeBattleTests:32, skill_stagger:25, element_sets:55, RunReportTests:19, WaveReplayTests:28, WeaveInBattleTests:69 |
| `BuildWith(...)`/`With(params Form[])`/`BuildOf(...)`/`OneSkill(...)` weaving through the above | ~25 | see §2 |
| `MidCareerHunter()` (2 M gleam, 20 ranks × 6 stats) | 2 copies | BalanceSweepTests:89, heal_balance_test:52 |
| `Trained(...)`/`TrainedHunter(...)`/`Wearing(...)` hunters | 6 | HunterStatWiringTests:25, attrition:64, wave_length:69, PowerRatingTests:36,54, element_sets:37, SoloBattleTests:620 |
| `Taught.Everything()` (all `SkillRoad` nodes restored, 9999 points) | 1 | used in 6 files / 13 sites; two more files inline the same walk under a different name (`EveryRoadWalked()` in variation_liveness:49 and reinforcement_liveness:49; `mastery_node_liveness_test.cs:65-77` and `skill_slot_kinds_test.cs:214-217` open-code it) |
| `DamageBench.Measure(build, hunter).Dps` (src, `Builds/DamageBench.cs:31`) | — | 21 sites in 5 files: affinity_test, DamageBenchTests, family_form_affinity, HunterStatWiringTests, GearTests |
| `SoloBattle.ResolveWave(champ, build, hunter, creatures|hp/dmg, enemyIntervalMs, tuning, new Random(seed), ...)` | — | the universal chain harness; two overloads at `SoloBattle.cs:398,419` |
| `SoloExpedition(build, champ, hunter, baseHp, baseDmg, tuning, source, rng)` + `PushWave()` | — | liveness theories, BalanceSweep, pacing, attrition, wave_length |
| `new Hunter()` | 221 sites in 57 files | the fixture champion; `new Build(...)` 64 sites in 25 files |
| `CharacterRoster.*` | 74 sites in 10 files | |

## 5. How tests build a skill today (the rewrite's size)

Every path names a `Form` (grep over tests, bin/obj excluded):

| Construction | Sites | Files |
|---|---|---|
| `new WovenAbility { Name, Source, Form[, Vow] }` | 56 | 37 |
| `new EquippedSkill(ability, cooldown[, PassiveSlot])` | 38 | 24 |
| `build.Weave(...)` (mostly wrapping the above via a local `Sk`) | 103 | 36 |
| `BuildComposer.Compose(tree, mastery, character, skills: SkillPick[], keystoneIds, slotCapacity[, progress])` | 21 | 11 |
| `SkillPick(Source, Form, VowId, Name[, Passive])` — none pass `SkillId:` | (inside the 21) | variation/reinforcement/mastery_node/loot_node/skill_progress/skill_slot_kinds/champion_starting_skill/break_badge/beat_cadence/slot_split/DustEffects |
| `SkillCatalogue.Resolve(Form, passive)` direct | 17 | 2 (skill_catalogue_test, skill_slot_kinds_test) |
| `SkillCatalogue.{All,ById,Find,ActiveOf,PassiveOf,Taught}` — the id-based API | 23 | 5 |
| `Weaving.Weave(...)` | 0 | 0 — there is no such factory; "weave" in tests means `Build.Weave(EquippedSkill)` |
| `FormBehaviour.*` in tests | 54 hits | affinity_test, affinity_vow_buyback, BuildGlossary, SoloBattleTests, roster_parity, skill_slot_kinds, all the `Sk` fixtures via `BaseCooldownMs` |

Sizing: if `EquippedSkill` grows a `SkillDef`-first constructor (or `SkillPick` makes `SkillId` primary), the mechanical rewrite is the 12 `Sk` fixtures + 21 `Compose` call sites + 38 direct `new EquippedSkill` sites — roughly **40 files, ~100 sites, almost all inside `private static` helpers at the top of each file**. The behavioural rewrites (SoloBattleTests Form-identity block, WeavingTests power block, the six split files) are separate and listed in §2.

## 6. Determinism / timing / randomness

- **No unseeded randomness and no wall-clock reads in the suite.** `grep -nE "new Random\(\)|Random\.Shared|DateTime\.(Now|UtcNow|Today)|Stopwatch|Environment\.TickCount|Thread\.Sleep|Task\.Delay|Parallel\."` over tests → 0 hits. 60 files use `new Random(<literal>)`.
- The one `Guid.NewGuid()` is `save_store_test.cs:30`, naming a unique temp directory (disposed in `Dispose` :34-37) — deliberate file IO, header :12-15.
- Frame-rate independence is asserted in-sim, not by timers: `beat_cadence_test.cs`, `WarrenTests.cs:140-170` (`tick accrues whole units and carries the remainder`, `many small ticks equal one big tick`), `BandCycleTests.cs:185 test_the_wave_seed_is_stable_across_processes`, `SoloExpeditionTests.cs:73 ..._is_deterministic`, `ClipTests.cs:116`, `RigTests.cs:144`, `DamageBenchTests.cs:23`, `roster_parity_test.cs:323`.
- `BalanceSweepTests.cs:106-136` documents that the injected `Random` does NOT vary a run — `Bands.Seed(region, wave, RunIndex)` does — so sweeps vary `RunIndex`; a future harness that only varies the seed will measure zero variance.
- Slowest classes (whole suite is 67 s): BalanceSweepTests (40 run-indices × 400-wave cap), roster_parity (tick ceiling lifted to 1 800 000 ms), heal_balance, attrition, gleam_economy, and the four MemberData liveness theories (each row runs 2×6 waves). None are flaky by construction.
- `FullLoopTests.cs:30` keeps a `static readonly Random Rng` shared across the class — harmless today (one consumer) but a second test using it would make results order-dependent.

## 7. Non-dotnet gates (`tools/check_*.py`, `check_boot.sh`)

All seven Python gates are text gates over the **Game** source ("there is no test project for the Game assembly" — `check_nav_gates.py:9`, `check_ui_type.py`, `check_mouse_space.py:14`, `check_font_coverage.py`). Run together by `tools/check_all.sh`. Every one hardcodes the old project path:

| Gate | Proves | Hardcoded |
|---|---|---|
| `check_font_coverage.py` | every drawn character is in the observed-rendering set | `GAME = src/ResonanceHunter.Game`, `FONT = .../PixelFont.cs` (:37-38) |
| `check_font_digits.py` | bundled TTFs have tabular digits | `assets/fonts` only — no project path |
| `check_ui_type.py` | no literal text size; every size is a `UiTypography` name | `GAME = src/ResonanceHunter.Game` (:50) |
| `check_asset_keys.py` | every literal asset key exists on disk | `GAME` (:28), `src/ResonanceHunter.Core/Encounters/Regions.cs` (:118), `src/ResonanceHunter.Core/Economy/ItemClasses.cs` (:143) |
| `check_init_order.py` | `Initialize()` never dereferences a `LoadContent()` field | `GAME` (:37) |
| `check_nav_gates.py` | `Nav` and `NavActivity` parallel arrays in `Game1` line up | `src/ResonanceHunter.Game/Game1.cs`, `src/ResonanceHunter.Core/Progression/Unlocks.cs` (:17-18) |
| `check_mouse_space.py` | every screen entry point converts `CanvasMouse` before hit-testing | `GAME`, `GAME1` (:25-26) |
| `check_boot.sh` | the game boots with a real save, boots fresh under `RH_SAVE_DIR`, does not write under `RH_BOOTCHECK`, and round-trips a save | `SAVE=$LOCALAPPDATA/ResonanceHunter/save.json` (:32), `dn build/run src/ResonanceHunter.Game` (:34,54), kills process **`IDLExIDLE`** (:65) — already the new product name, so the script is half-renamed |

The `RH_` env prefix (`RH_SHOT`, `RH_SHOT_MODE`, `RH_BOOTCHECK`, `RH_SAVE_DIR`, `RH_ENV`) is read by `Game1`/`SaveStore` and used by `check_boot.sh`, `tools/run.sh`, `tools/shellenv.sh:103-112`, `tools/asset-pipeline/capture.sh`, `capture_seq.sh`, and `tools/publish/itch_push.sh:40` (which already smoke-boots `IDLExIDLE.exe` under `RH_SHOT`). Renaming the prefix touches Game code plus six scripts; renaming the project directory touches all seven Python gates, `check_boot.sh`, `ci.yml:29-36`, both test csproj `ProjectReference`s, and `hunt_screen_feedback_test.cs:35`.

## 8. Stale vocabulary inside the suite

- `ResonanceHunter.*` namespaces in every file (120 declarations, 424 usings); project/dir names `ResonanceHunter.Core.Tests`, `ResonanceHunter.Integration.Tests`.
- "squad": `GearTraitsTests.cs:211,256` test names; `Hunter.SquadDamageMultiplier/SquadHealthMultiplier` asserted in `BuildTests.cs:78-115`; `WaveReplayTests` header "multi-slot shielding".
- "team": `EncounterSpawnerTests.cs:44 test_reference_team_dps_falls_off...`.
- "automated kill / active play": `LootSystemTests.cs:93,134` (see open question).
- "Form"/"Weave"/"woven": everywhere the fixtures are; `WeaveInBattleTests` file name; `Tests.Abilities` namespace for `Weaving/WeavingTests.cs`.
- `rh_savestore_` temp prefix `save_store_test.cs:30`.
- `WarrenResource.Mastery` asserted in `WarrenTests.cs:87` while the UI calls it INSIGHT.

## 9. Open questions

1. Do Style specialisations replace Form specialisations one-for-one? If yes, `affinity_test`, `one_discipline_test`, `MasteryTreeTests.test_every_form_has_one_specialisation` are REWRITE not DELETE, and `mastery_node_liveness_test.cs:124-128` (which poses a specialisation to make NARROW/BROAD readable) needs the new node id.
2. Are Source signatures (`source_signature_test.cs`) and the Source effectiveness table staying? `SoloBattle.cs` reads `Weaving.SourceEffectiveness` and `Weaving.VowMultiplier` today, and every variation carries a Source, so I classified them KEEP-with-rewrite; if the table goes, 4 WeavingTests + 1 WeaveInBattle + 1 glossary test + 6 signature tests go with it.
3. `VowDemand.SingleForm` (`ResonanceWeaving.cs:53-54`, vows `vow_singular`/`vow_pure`) — does it become SingleStyle? Decides the fate of `WeavingTests.test_every_demand_can_be_both_met_and_unmet` and `WeaveInBattleTests.test_the_sim_describes_a_build_the_vows_can_read`.
4. RESOLVED while writing: `KillContext.AutomationStage` is read by `LootSystem.cs:386` (rarity parity dampener) but **never assigned anywhere in `src/`** — `grep -rn "AutomationStage\s*=" src` → 0 hits; the only writers are `LootSystemTests.cs:22` and `FullLoopTests.cs:111`. So `LootSystemTests.test_automation_dampens_rarity_without_ever_removing_it` (:93), `test_skilled_active_play_yields_more_items_than_an_automated_kill` (:134) and the integration fact's `AutomationStage = 1` pin a branch only tests can reach — a test-only feature, the liveness failure this codebase hunts. Candidate DELETE with the field.
5. `slot_split_balance_test.cs` compares against a four-active "old model" that no longer exists — keep as a balance record or delete?
6. `SavedSkill.Form` (`SaveGame.cs:294-301`) — the refactor's save migration needs a fixture that loads `{Source, Form, Passive}` and asserts the composed build's `Def.Id`; today `SaveSystemTests.cs:317-357` only asserts the strings round-trip.
7. `FullLoopTests` never exercises a build. Should the integration project gain a "weave → fight → level a skill → save → restore → same Def" loop? `SkillProgressLivenessTests` (`skill_progress_test.cs:178`) is the closest existing chain.

## 10. Recommendations (test-side only)

1. Before deleting `Form`, add a `SkillDef`-first constructor path (`EquippedSkill.Of(SkillDef, ...)` or `SkillPick(SkillId:)` as the primary) and migrate the 12 `Sk` fixtures + 21 `Compose` sites; the suite then stops compiling on any leftover Form use, which is the cheapest inventory.
2. Promote `Taught.Everything()`, `MidCareerHunter()`, and one canonical `Sk`/`Fight` helper into a shared `TestFixtures` class — they are copied 2–12 times today.
3. Keep the two hand-maintained proof indexes (`TriggerLivenessTests:115`, `EnchantmentsTests:150`) and add one for `SkillCatalogue.All` ids → the liveness theories already cover it by MemberData, so the index can be "every skill has ≥1 variation row in `EveryVariation`".
4. Rename `WeaveInBattleTests` boss-wave tests into an Expeditions/WaveScaling file; split `WeavingTests` into `SourceTableTests` and `VowPricingTests` and delete the four `AbilityPower/BasePower` tests with `FormBehaviour`.
5. `hunt_screen_feedback_test.cs` and all seven Python gates need the new Game directory name the day the project is renamed; `ci.yml` and both csproj `ProjectReference`s the same day.
