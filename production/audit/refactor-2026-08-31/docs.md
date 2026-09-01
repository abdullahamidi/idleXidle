# Documentation truth audit — design/, docs/, README, production/, registries

**Date**: 2026-08-31 · **Branch**: `feat/hunter-cutout-rig` (HEAD `ae30f2a`, clean) · **Mode**: read-only on source; this file is the only write.
**Scope**: every file in `design/gdd/` (40), `design/*.md` (4), `docs/architecture/` (4 ADRs + `tr-registry.yaml`), `design/registry/entities.yaml`, `docs/registry/architecture.yaml`, `README.md`, `production/*.md`, the CLAUDE.md-referenced docs under `.claude/docs/`, plus the store copy under `docs/store/` because it is player-facing. `design/art`, `design/audio`, `design/references` are noted by header only.
**Method**: for every file I read the title, status block and section outline (`head`, `grep -nE '^## '`), read the load-bearing sections in full for the docs the refactor will touch, and checked each claim against the code with `grep -rn` over `src/` and `tests/` (excluding `bin/ obj/ .git/ docs/art-reference/`). Every classification below carries the code `file:line` it was tested against. Where I did not verify something, it is in §15 Open questions, not asserted.

Sibling audits in this directory that already cover the code side of several docs and are cited rather than repeated: `mastery.md`, `traits.md`, `legacy-weaving.md`, `characters-quests.md`, `warren-economy.md`, `gear-items.md`, `persistence.md`, `presentation.md`, `product-identity.md`.

---

## 0. Headline

The design corpus is **two generations behind the runtime**. Of the 40 files in `design/gdd/`, **27 describe the July-2026 manual-combat / creature-roster / Source×Form game** that `game-flow.md` itself declared dead on 2026-08-12 (`game-flow.md:6-10`) — yet only two of them carry a SUPERSEDED banner (`combat-encounter-system.md:4`, `expedition-auto-battle.md:4`). `systems-index.md` (git 2026-08-14, header still says `Last Updated 2026-07-14`) indexes all 27 as "Designed" and has five new rows pasted into its table without a `#` column (`systems-index.md:27-31`). The one document that describes the live skill model — `skill-slots-and-skill-trees.md`, STYLE→SKILL→VARIATION→REINFORCEMENTS, twelve skills, RESONANCE/LOOT/TEMPO/ENDURE — opens with **"Nothing here is implemented yet"** (`:3-4`) while its own §9b says "Built 2026-08-30" (`:700-707`) and `MasteryCatalog.cs:14-58` / `SkillCatalogue.cs:264-568` implement it. Eight docs are MIXED and each holds sections that are the *only* spec for a live system (the trait tree in `skill-and-trait-trees.md` §3.4-3.5; the Vow model in `vows.md`; item classes in `characters.md` §9; the five quests in `quests.md`; the expedition loop and its five addenda in `game-flow.md`); those sections must be moved before the files are archived. Player-facing copy is also stale: the live itch.io page still sells "the six forms: Strike, Projectile, Aura, Trap, Mark, Transformation" (`docs/store/itch/description.html:18-19`). The product name "Resonance Hunter" is in 23 doc titles; `IDLExIDLE` appears in exactly two project docs (`docs/store/store-page.md`, `docs/store/itch/README.md`).

**Counts**: CURRENT 1 (`skill-slots-and-skill-trees.md`, header stale) · MIXED 8 · SUPERSEDED 27 · ORPHAN 3 (no code at all: `rare-creature-capture-system.md`, `creature-jobs-evolution-system.md`, `vow-condition-tracking.md`) · historical reports 2 (`gdd-cross-review-2026-07-14.md`, `system-audit-2026-08-12.md`) · registries 3 (all stale or empty).

---

## 1. Ground truth used (the live runtime, with the lines I read)

| Fact | Evidence |
|---|---|
| Six Styles: Hammer, Snare, Sign, Volley, Field, Drain; the enum order is the affinity ring | `src/ResonanceHunter.Core/Builds/SkillCatalogue.cs:34-52` |
| Twelve skills, two per style, ids `hammer_blow, hammer_press, snare_repay, snare_jaws, sign_call, sign_brand, volley_spray, volley_weep, field_pulse, field_mire, drain_drink, drain_wilt` | `SkillCatalogue.cs:264, 291, 321, 348, 376, 403, 431, 458, 486, 513, 541, 568` |
| `SkillDef` record; `SkillVariation(Name, Line, Reinforcements, Modify, Source)`; `Reinforcement(Name, Line, Modify)` — variations/reinforcements are `Func<SkillDef,SkillDef>` deltas | `SkillCatalogue.cs:105, 120-125, 155-175` |
| `SkillDef.LegacyForm` is "a migration bridge and nothing more"; `SkillCatalogue.Resolve(Form, passive)` still exists | `SkillCatalogue.cs:150-153, 646` |
| Mastery branches `Resonance, Loot, Tempo, Endure`; WEIGHT/SPREAD retired 2026-08-30 | `Builds/MasteryCatalog.cs:14-58` |
| Six Specialisation nodes are road heads; `Teaches(...)` road nodes unlock skills; `MasteryKind.SkillRoad` | `MasteryCatalog.cs:434-463`; `Builds/MasteryTree.cs:10-43` |
| Skill points = `floor(sqrt(bestDepth) × 0.9)` per region | `Builds/MasteryPoints.cs:36-40` |
| Trait roads SPINE / RUIN / AEGIS / ARTIFICE / AVARICE | `Prestige/TraitRoads.cs:38-46` |
| The permanent tree spends **trait points**, "NOT Memory Dust"; Dust is a Warren/checkpoint currency; 51 nodes | `Prestige/MemoryDust.cs:113, 121-132, 155, 309-535`; `MemoryDustText.cs:79-83`; `Game/PrestigeScreen.cs:468, 527`; `Game/Game1.cs:5151-5160` |
| Vow catalogue: 11 demand Vows + 2 static (13); static conversion **8.0** "from 3.0 (2026-08-28)"; Formula 1 = `1 + MaxPowerBonus(1.5) × (1-uptime)^exp` | `Weaving/ResonanceWeaving.cs:147, 150-182, 292-304, 381-470` |
| Vow-teaching nodes: `vow_study_1..3`, `vow_binding`, `vow_sacrifice` named LEARN VOWS I/II/III, GEAR-SLOT VOWS, SACRIFICE VOWS | `Prestige/DustEffects.cs:103-116`; `MemoryDust.cs:319-332` |
| Ten champions, `StartingSkillId`, `Lean = Branch.Resonance/Loot/...`, `Aptitude = Form.*` (legacy), `Class = ItemClass.*` | `Characters/CharacterRoster.cs:45-213`; `Characters/Character.cs:117-133` |
| Item classes Warden/Ranger/Mystic/Bulwark/Wanderer; `Road = Branch.Resonance` (Warden), `Branch.Loot` (Ranger) | `Economy/ItemClasses.cs:28-43, 97, 104` |
| Five quests (`q_cinder_deep 50`, `q_three_vows 3`, `q_magpie_chests 30`, `q_marrow_hold ConquestWave×4`, `q_quiver_hollow 60`) | `Quests/Quests.cs:135-171` |
| Conquest at wave **20**; checkpoints every 10; 25 Dust per wave | `Encounters/Checkpoints.cs:29, 32, 35` |
| Six regions: verdant_hollow, cinderworks, umbral_reach, marrow_wastes, still_archive, pale_choir | `Encounters/Regions.cs:68-94` |
| Archetypes Swarm/Armoured/Caster/Bruiser (code comment still says "answered by Spread/Weight") | `Encounters/Archetypes.cs:18-21, 31-33` |
| Element sets: rungs at 2/3/4/5 pieces, +8% own-skill per rung | `Economy/ElementSets.cs:15, 38` |
| Warren: 8 facilities; `WarrenResource {Gleam, Mastery, Dust}`; Mastery is shown as INSIGHT and "used only here, on upgrades"; rates 17/16/450/14/122/118/12/108 per min; `GleamCostBase 180 × 1.50^L`; milestones every 5 levels +15%; conquest +10%/region; `DepthPerFacilityLevel = 5` | `Warrens/Warren.cs:13, 48-55, 74-102, 195-198, 248-251`; `Game/WarrenScreen.cs:104, 117, 258-263` |
| Warren mastery pool does **not** feed the build tree | `Game/Game1.cs:296-297` ("THE COMMENT HERE USED TO SAY 'feeds SetEarned', AND IT DOES NOT") |
| Creature den / AutomationScreen / CORES "fully retired 2026-08-24" | `Game/Game1.cs:290`; `find src -name AutomationScreen.cs` → none |
| Build: `SkillSlots = 4`, split active/passive (`ActiveSlotsFor`), `KeystoneSlots = 3`, `EquippedSkill(..., bool? PassiveSlot)` | `Builds/Build.cs:206, 311, 402, 489` |
| Save: single `Version = 2`; `SavedSkill {Source, Form, VowId}` (legacy); `SavedSkillProgress {SkillId, Variation, Reinforcements}`; `save.json`, ten-second autosave, corrupt-aside | `Persistence/SaveGame.cs:17, 293-298, 698-709`; `SaveStore.cs:14, 30, 112` |
| Hunter training IS reachable: `StatsScreen.ConsumeTrain()` → `_hunter.Train(stat)`; reset for one Crystal; 9 stats, cost 25 × 1.13^rank, cap 60 | `Game/StatsScreen.cs:109`; `Game1.cs:4074-4075`; `Economy/HunterProgression.cs:9-13, 15-29` |
| A settings panel exists (`_showSettings`) | `Game1.cs:242, 1314-1315, 1438` |
| Forge merge has a hybrid table; Reforge and GemCraft exist | `Forge/Forge.cs:224-227`; `Forge/MergeRecipe.cs:54, 69, 93`; `Forge/Reforge.cs`; `Economy/GemCraft.cs` |
| Tests today: 991 `[Fact]`/`[Theory]` attributes | `grep -rE '^\s*\[(Fact|Theory)' tests --include=*.cs | wc -l` → 991 |

---

## 2. Per-file table — `design/gdd/` (40 files)

Legend: **CURRENT** matches the live runtime · **MIXED** needs sections updated/moved (rescue list in §11) · **SUPERSEDED** describes an abandoned model (which one is named) · **ORPHAN** describes a system with no code · **REPORT** a dated review, not a spec. "git" is the last commit touching the file.

| # | File | Title says "Resonance Hunter"? | Status block | git | Classification | Evidence (what it describes vs code) | Recommendation |
|---|---|---|---|---|---|---|---|
| 1 | accessibility-settings-system.md | yes | 1.0 Complete | — | **SUPERSEDED** — manual-combat input model (aim assist, cycle-and-confirm timing, telegraph readability; §3 lines 91-413) | No targeting code: `grep -rln 'Targeting\|WeakPoint\|Telegraph' src/ResonanceHunter.Core` → 0 files. A settings panel exists (`Game1.cs:242, 1314`) but is undocumented | Archive with banner |
| 2 | animation-rig-system.md | yes | 1.0 Complete; "highest-risk system" | — | **SUPERSEDED** — creature part rig for weak-point targeting over the 6-Source×5-Role creature matrix (depends on creature-data-schema) | `Core/Animation/Rig.cs`, `Clip.cs` exist; whether they are drawn is the presentation audit's question (`presentation.md`). No `CreatureTemplate` parts: `VerdantHollow.cs:30` templates are `{Id, BaseHealth}` only | Archive; if the cutout rig ships on this branch, write a new short doc from ADR-002 + `Rig.cs` |
| 3 | audio-system.md | yes | 1.0 Complete | — | **SUPERSEDED** — telegraph/weak-point audio cues, "mechanically load-bearing" (`:10`) | Live audio is `Game/SoundBank.cs` + `design/audio/asset-generation-audio.md` (itself stale: "The game has no sound yet", `:3`) | Archive |
| 4 | automation-config-ui.md | no (GDD:) | 1.0 Complete | — | **SUPERSEDED** — creature-team automation stages UI | `AutomationScreen` deleted; `Game1.cs:290` "fully retired 2026-08-24" | Archive |
| 5 | characters.md | no | none | 2026-08-26 | **MIXED** | Live: ten champions, one passive, free switching, quests, §9 item classes (`ItemClasses.cs:28-43`). Stale: roads "Weight/Spread" in roster table (`:29-40`) and §9 class table (`:123-124`) vs `Branch.Resonance/Loot` (`CharacterRoster.cs:73, 88`; `ItemClasses.cs:97, 104`); "Two characters are gated behind quests" (`:25`) vs five quest-gated champions (`Quests.cs:141-171`; `characters.md:34-38` itself lists five); §5 "ANVIL and MAGPIE both Cinderworks" (`:81`) vs MAGPIE quest-gated; no `StartingSkillId` (`CharacterRoster.cs:53, 70, 85 …`); Aptitude is Form-based in both doc and code (`Character.cs:117-118`) — Form is legacy | **Update** (see §11) |
| 6 | combat-encounter-system.md | yes | banner SUPERSEDED 2026-08-12 (`:4`) | — | **SUPERSEDED** — manual weak-point combat | banner already present | Move to archive (banner exists) |
| 7 | combat-hud.md | no | 1.0 Complete | — | **SUPERSEDED** — manual-combat HUD (belt-charm Vow signal, telegraph strip) | no HUD of this shape; hunt HUD lives in `SoloExpeditionScreen.cs` | Archive |
| 8 | creature-ai-telegraph-system.md | yes | 1.0 Complete | — | **SUPERSEDED / ORPHAN** — telegraph AI | `grep -rln Telegraph src` → 0 | Archive |
| 9 | creature-data-schema.md | yes | 1.0 Complete | — | **SUPERSEDED** — creature roster schema (parts, evolution tree, power_tier 1-20) | `CreatureTemplate` shrunk to `{Id, BaseHealth}` (`VerdantHollow.cs:30-45`); the six-value `Source` enum survives in `Automation/Source.cs` | Archive |
| 10 | creature-jobs-evolution-system.md | yes | 1.0 Complete | — | **ORPHAN** — creature jobs/evolution | `grep -rln 'Evolution\|Hatch(' src --include=*.cs` → only a comment hit in `Clip.cs`; den retired (`Game1.cs:290`) | Archive |
| 11 | creature-roster-ui.md | no | Complete | — | **SUPERSEDED** — creature roster UI | see #10 | Archive |
| 12 | encounter-spawn-system.md | yes | 1.0 Complete | — | **MIXED (leaning SUPERSEDED)** — encounter templates, `power_tier`, `par_clear_time_seconds` anchoring the active/idle efficiency bands | `EncounterTemplate.PowerTierBase` (`EncounterTemplate.cs:33`), par evaluated at authoring time (`EncounterSpawner.cs:33`), `EfficiencyContract.cs` still referenced by `RegionAutomation.cs` and `EncounterTemplate.cs` — but the efficiency bands serve the dormant automation | Rescue the power-tier assignment rule into a regions doc; archive the rest |
| 13 | expedition-auto-battle.md | no | banner SUPERSEDED 2026-08-12 (`:4`) | — | **SUPERSEDED** — squad expedition with BANK/PUSH | banner present; `grep -n squad` hits | Move to archive |
| 14 | forge-ui.md | no | Complete | — | **SUPERSEDED** — merge/sell/dismantle/feed/Vow-binding UI | Vow-binding and feed do not exist (`grep -rn 'VowBind\|vow_binding' src/ResonanceHunter.Core/Forge` → 0); live forge is Merge/Reforge/Refine/Gem (`Forge.cs`, `Reforge.cs`, `GemCraft.cs`) | Archive |
| 15 | game-concept.md | yes | Draft | — | **SUPERSEDED** — "Monster Hunter × Melvor" weak-point action pitch, creature farming teams, MVP = manual combat (`:8-13, 317-333`) | The live pitch is `docs/store/store-page.md:14-21` ("offline idle auto-battler … you never swing the sword") | Archive; author a one-page IDLExIDLE concept |
| 16 | game-flow.md | in body (`:16`), not title | "Complete draft"; supersedes concept/combat/expedition (`:6-10`) | 2026-08-27 | **MIXED** — the nearest thing to a current top-level spec | Live: §3.1 expedition, §3.3 bands, §3.4 report, §3.5 three payouts (trait points), §3.9 ladder, §4.1, §4.3-4.8, addenda (conquest 20 = `Checkpoints.cs:29`; Dust checkpoints 25/wave = `:35`; VITALITY regen; the beat; element sets). Stale: "Resonance Hunter is …" (`:16`); "four woven skills" (`:17`) vs 2 active + 2 passive (`Build.cs:206, 402`); "trade a Projectile for a Trap" (`:42`); Forms as band answers (`:111-112, 128`); §3.6 mastery tree "buys how skills behave: target counts, hit sizes, cooldowns" (`:242-244`) — contradicted by `skill-slots §7/§9` and `MasteryCatalog.cs:14-58` (mastery = champion; skill behaviour = skill trees); §3.8 "facilities unlock by conquest … produce Gleam and materials only … different material tiers" (`:293-305`) — not implemented (`grep -n Unlock Warren.cs` → only a doc comment at `:244`; `WarrenResource {Gleam, Mastery, Dust}` `Warren.cs:13`); §4.2 `Form.targets` (`:390-393`); §3.9 "new Forms and Sources" (`:324`) | **Update**: retitle, replace Form vocabulary, fold the five addenda into §3/§4, strike §3.8's unbuilt bullets or mark them intent |
| 17 | gdd-cross-review-2026-07-14.md | no | verdict FAIL | — | **REPORT** on the 27 old GDDs | historical | Archive (keep as history) |
| 18 | hunter-progression-system.md | no | Complete (rush) | — | **MIXED** | Live: nine-stat catalog and Gleam training (`HunterProgression.cs:9-13`; reachable via `StatsScreen.cs:109` → `Game1.cs:4074`). Stale: built on combat-encounter Formula 3b; A2 "no respec, no refund" vs `ResetTraining` for one Crystal (`Game1.cs:4075`, `HunterProgression.cs:24-29`); idle-can-progress proof against `region_auto_sell_gleam_cap_per_hour` (dormant automation); cost constants (doc's proof used 79,556 Gleam sink; code: 25 × 1.13^rank, cap 60, "~2.65M for all nine" `HunterProgression.cs:17-21`) | Rewrite as a short Training section (fold into progression.md) and archive |
| 19 | input-targeting-system.md | yes | 1.0 Complete | — | **SUPERSEDED** — mouse/cycle-and-confirm targeting | see #1 | Archive. Note `.claude/docs/technical-preferences.md` still mandates this model (§7 below) |
| 20 | item-data-schema.md | no | Complete | — | **SUPERSEDED** — `ability_focus` charms, `vow_binding_log`, modifier slots by tier | Live item model is `Economy/Gear.cs`, `ItemAffixes.cs`, `Enchantments.cs`, `GemCraft.cs`, `ItemFamilies.cs`, `ItemClasses.cs`, `ElementSets.cs` — none of these has a GDD (§14) | Archive |
| 21 | loot-drop-system.md | no | Complete | — | **SUPERSEDED** — part-break loot, `LootFilterRule` schema, power_tier rarity | Live: `Loot/LootSystem.cs`; the only descendant of the filter is the auto-sell floor (`filter_common/uncommon` traits, `MemoryDust.cs:340-343`; `ForgeScreen.cs:554`) | Archive |
| 22 | loot-filter-ui.md | no | Complete | — | **SUPERSEDED** — filter-rule UI | see #21 | Archive |
| 23 | memory-dust-prestige-system.md | yes | 1.0 Complete; "Full Vision" | — | **SUPERSEDED** — "19 unlocks, 660 Memory Dust", Dust buys Wider Roster / Attuned Ascension / filter slots (`:33-120, 160`) | Code: 51 nodes bought with trait points, "NOT Memory Dust" (`MemoryDust.cs:121-132, 309-535`). Only §3.1 "Nothing resets" survives as a principle (`MemoryDust.cs:89-94`) | Archive; the live trait tree's spec is `skill-and-trait-trees.md` §3.4-3.6 (rescue, §11) |
| 24 | onboarding-tutorial-system.md | yes | 1.0 Complete | — | **SUPERSEDED** — glyph-language / weak-point tutorial | Live onboarding is `Progression/Onboarding.cs` (tour cards, `TourTarget`) + `Tutorial.cs` — undocumented | Archive |
| 25 | progression.md | no | "Documented for v1.0" | 2026-07-29 | **MIXED (mostly stale)** | Stale: "MASTERY POINTS → DUST → BLESSING TREE" (`:22`) — Dust does not buy the tree; "Warren → mastery pool feeds the build tree" (`:29-31`) — false per `Game1.cs:296-297`; "ConquerWaveDepth (7)" (`:48`) vs 20 (`Checkpoints.cs:29`); "CreatureCore drops are now vestigial" (`:83`) — cores retired (`Game1.cs:290`); currency table (`:57-62`) omits trait points, Insight, gems. Live: six regions/elements order matches `Regions.cs:68-94`; corruption ladder; `test_conquering_regions_raises_production` exists (`tests/…/Warrens/WarrenTests.cs`) | **Update** into the currency-flow doc (it is the only doc with a currency table) |
| 26 | quests.md | no | none | 2026-08-26 | **MIXED (small)** | Table (`:32-38`) matches `Quests.cs:141-171` exactly. Stale: §1 "Two quests" (`:5`), §2 THE FIRST VOW / THE HOLLOW HUNT narrative (`:14-16`), §3 "THE FIRST VOW is LATCHED" (`:25`) — now THE THIRD OATH ×3; §6 "for the goal that exists but no quest currently uses" (`:70`) — THE FULL HOLD uses `ChestsOpened`; §8 AC 4 names THE FIRST VOW | **Update** (one pass) |
| 27 | rare-creature-capture-system.md | yes | 1.0 Complete; Vertical Slice | — | **ORPHAN** — capture-without-killing | `grep -rn 'class .*Capture' src` → 0 (the `CaptureRig` at `Game1.cs:462` is the screenshot rig) | Archive |
| 28 | region-mastery-automation-system.md | yes | 1.0 Complete | — | **SUPERSEDED** — creature-team automation stages, CCS, idle-efficiency bands | `Automation/RegionAutomation.cs` still compiles but its screen is gone (`Game1.cs:290`); the surviving descendant is "region mastery goals → trait points" (`Game1.cs:5159` "18 mastery goals") and `recall_1..4` (`MemoryDust.cs:356-364`) | Archive; document the live mastery-goal rule in the traits doc |
| 29 | regions-and-rosters.md | no | Draft | — | **MIXED** | Live: six regions, archetype-as-role (`Archetypes.cs:31-33`), band cycle (`Encounters/BandCycles.cs`). Stale vocabulary: "Spread build" (`:36, 132, 249`), "Weight" (`:96, 133, 147, 248`), "Mono-Form" (`:102, 108, 181`). The affix table names (NUMBERS, PLATED, WARDED, LEGION `:95-104`) were **not** found in `RegionModifiers.cs` (which holds MOLTEN PLATING, UMBRAL VEIL… `:25-30`); I did not read `Bands.cs` — see §15 | **Update** vocabulary to Style/Skill; verify affix table against `Bands.cs` |
| 30 | region-view-world-map-ui.md | yes | 1.0 Complete | — | **SUPERSEDED** — region-mastery map | live map is `Game/MapScreen.cs` with checkpoint chips priced in Dust (`MapScreen.cs:51, 128`) — undocumented | Archive |
| 31 | resonance-weaving-system.md | in body (`:1` is "GDD: Resonance Weaving System") | Complete (lean) | — | **SUPERSEDED** — Source×Form×Vow composition, 3 abilities + 1 ultimate, 36-combo matrix, fight-watching conditional Vows (`:54-60, 139-220, 257`) | The whole `Weaving` static class / `WovenAbility` / `Form` are the legacy the refactor deletes (`legacy-weaving.md`). But: §3.1 Sources table is the only prose spec of the six Sources; §4.1 Formula 1 is implemented verbatim (`ResonanceWeaving.cs:292-304`, `MaxPowerBonus = 1.5` `:147`); §4.2 Formula 2 is implemented with **8.0** not 3.0 (`:150-182`) | Move §3.1 + §4.1/§4.2 (corrected) into `vows.md`/a sources doc, then archive |
| 32 | save-load-persistence.md | body (`:37`) | Draft | — | **MIXED → SUPERSEDED** — per-section schema versioning envelope, section registry | Code: one `Version = 2` on the whole file (`SaveGame.cs:17`); JSON single slot `save.json`, ten-second autosave, corrupt-aside all match (`SaveStore.cs:14, 30, 112`). `persistence.md` (sibling) read it in full | Rewrite briefly from code, or archive with a pointer to `persistence.md` |
| 33 | settings-menu-ui.md | yes | 1.0 Complete | — | **SUPERSEDED** — accessibility-driven settings | a panel exists (`Game1.cs:242`, `DisplaySettings.cs`) but not this one | Archive |
| 34 | skill-and-trait-trees.md | no | Draft | 2026-08-27 | **MIXED** — two halves with opposite fates | Skill-tree half (§1, §3.1-3.3, §4.1 income ✔, §4.3, §7, AC 5/7): WEIGHT/SPREAD (`:55-66, 145, 179`), Form specialisations "deepen that Form" (`:121-122, 312-329`), tree total 256 — superseded by `skill-slots §7/§9/§9b` and `MasteryCatalog.cs:14-58, 434-463`. Trait-tree half (§3.4-3.6, §4.2, §5, AC 6): ids, names and costs match `MemoryDust.cs:309-535` (socket_2 2, weave_5 3, LEARN VOWS I 1, ks_glass_cannon 4 → ks_reaper 12, HARDER HITS I-III 2/3/4 …) and `TraitRoads.cs:38-46` — **the only spec of the live trait tree**. Internal contradiction: §3.4 "spine (46) + 4 roads × 45 = 226" (`:369-371`) vs §4.2 "Tree cost 137" (`:466`) vs `Game1.cs:5159` "~150"; §3.4 income "two per corruption tier" (`:335`) vs §4.2 `conquests + corruptionTiers + masteryGoals` (`:465`) | **Split**: move §3.4-3.6/§4.2/§5-trait rows into a new `traits.md`; banner the skill half SUPERSEDED by skill-slots |
| 35 | skill-slots-and-skill-trees.md | no | "DESIGN IN PROGRESS … Nothing here is implemented yet" (`:3-4`) | 2026-08-30 | **CURRENT** (authoritative for STYLE→SKILL→VARIATION→REINFORCEMENT; RESONANCE/LOOT/TEMPO/ENDURE) — header and several passages stale | Matches `SkillCatalogue.cs`, `MasteryCatalog.cs`, `MasteryTree.cs`; §9b "Built 2026-08-30" (`:700-707`); §11 stages 8a/8c "done" (`:928-930`). Stale inside: header; §3 "SKILL (6 — one per style)" and `WovenAbility { Name, Source, SkillId, Vow }` (`:57-81`) vs twelve skills / `SkillDef`; §4 naming note "branches (WEIGHT, SPREAD, TEMPO, ENDURE)" (`:152`); §5 carries an in-body SUPERSEDED block (`:158-160`); §7 "Nothing here is implemented" (`:515`). Not in the 8-section shape (11 numbered sections, no Player Fantasy / Edge Cases / Dependencies / Tuning Knobs / Acceptance headings) | **Update**: promote to the skill-system GDD; restructure to 8 sections; strike superseded passages |
| 36 | systems-index.md | yes | Approved; "Last Updated 2026-07-14" | 2026-08-14 | **SUPERSEDED** | see §9 | Rewrite from scratch |
| 37 | the-forge-system.md | no | Complete | — | **SUPERSEDED** — merge/hybrid/enchant/Vow-binding/feed, IPS, `vow_binding_cost` | Live: `Forge.cs` merge with hybrid table (`MergeRecipe.cs:54-93`), `Reforge.cs` (RNG reforge — see memory), `GemCraft.cs`, Refine. Whether `HybridTable` descends from this doc's §3.3 recipes is unverified (§15) | Archive; write a short forge doc from `gear-items.md` |
| 38 | vow-condition-tracking.md | no | Complete | — | **ORPHAN** — per-frame Vow condition evaluation, HUD signal | `vows.md §3.1` explicitly rejects fight-watching Vows; `WeaveContext` names nothing about the fight (`ResonanceWeaving.cs:529`, `vows.md` AC 1) | Archive |
| 39 | vows.md | no | Implemented | 2026-08-14 | **MIXED** | Live: build-demand model, three families, severity pricing, `Weaving.IsActive`, quest link (`ResonanceWeaving.cs:48-140, 381-470`). Stale: §3.3 "argues against SPREAD / WEIGHT / TEMPO's FOCUS road" (`:72-75`); §3.5 node names FIRST VOW / SECOND VOW / THIRD VOW / BINDING VOWS / SACRIFICIAL VOWS (`:97-104`) vs LEARN VOWS I/II/III, GEAR-SLOT VOWS, SACRIFICE VOWS (`MemoryDust.cs:319-332`); §4.3 "3.0 power per 1.0 effective HP" (`:139`) vs 8.0 (`ResonanceWeaving.cs:153`); §1 "one Form only" / §3.4 "four-skill weave" (`:13-14, 88`) — slots are 2+2 and Form is legacy | **Update** (see §11) |
| 40 | warren-facilities.md | no | Implemented (v1) | 2026-07-28 | **MIXED** | Live: eight named facilities (`Warren.cs:48-55`), three currencies, instant upgrades, tick determinism, default-empty save. Stale: §3 "Mastery Points … funds the build tree" (`:29, 33-35`) vs INSIGHT spent only on the Warren (`WarrenScreen.cs:258-263`, `Game1.cs:296-297`); "CREATURES sub-view" (`:14-15`) retired; §4 "Nursery 520" (`:44`) vs 17 (`Warren.cs:48`); Gleam cost `18700 × 1.40^L` (`:50`) vs `180 × 1.50^L` (`Warren.cs:85-86`); no milestones (`Warren.cs:99-102`), no conquest bonus (`:96`), no depth cap (`:248-251`); "the Nature track" for Dust (`:29`) | **Rewrite** §3-§4 from `Warren.cs` (numbers in `warren-economy.md`) |

---

## 3. Per-file — `design/*.md`, `design/art`, `design/audio`, `design/references`

| File | Classification | Evidence | Recommendation |
|---|---|---|---|
| `design/CLAUDE.md` | template standards | references `design/quick-specs/` and `design/ux/` — neither directory exists (`find design -type d`) | Update paths or drop the two sections |
| `design/build-identity-proposal.md` | historical proposal, **Form-framed** | "six Forms map to the six [Nen] categories", "mastery branch (Weight / Spread / Tempo / Endure)" (`:5-6, 24`); moves 1+2 "SHIPPED" — Source signatures are live (`tests/…/Builds/source_signature_test.cs`) | Archive as decision record; its still-open Move 3 (bands on the MAP) belongs in `regions-and-rosters.md` |
| `design/monetization.md` | **CURRENT** except the product name | "Resonance Hunter is sold once" (`:10`) | Update name only |
| `design/system-audit-2026-08-12.md` | **REPORT** (code snapshot of 2026-08-12) | many findings since fixed (training reachable `Game1.cs:4074`; Warren rates cut `Warren.cs:29-43`; den deleted) | Archive |
| `design/art/art-bible.md` | title "Resonance Hunter"; creature-matrix era (2026-07-14) | stage art superseded by `design/art/arena-art-contract.md` (self-declared AUTHORITATIVE) | Out of this audit's depth; retitle at minimum |
| `design/art/character-art-spec-batch1.md` | title "Resonance Hunter — … (rigged parts)" | relevant to the cutout-rig branch; not verified here | Retitle |
| `design/audio/asset-generation-audio.md` | stale opener | "The game has no sound yet" (`:3`); cue list names "captures, training … telegraph" | Update opener + cue list |
| `design/references/reference-games.md` | stale framing | "Resonance Hunter is a genre hybrid … creature collection" (`:3-4`) | Retitle/trim |
| `design/references/auto-battler-research.md` | research note | — | Leave |

---

## 4. `docs/architecture/` and the registries

| File | Classification | Evidence | Recommendation |
|---|---|---|---|
| ADR-001 pure-logic-core-separation | **CURRENT decision, stale evidence** | The split holds (`presentation.md` §1: one `Microsoft.Xna` hit in Core and it is a comment). Stale: names `DamagePipeline`, `CombatTuning` (`:36-37, 45`) — `grep -rln 'DamagePipeline\|CombatTuning' src --include=*.cs` → 0; "AC21-27 of combat-encounter-system.md are real xUnit tests" and `BlockerB1RegressionTests` (`:55-60`; README `:80`) — `grep -rln BlockerB1RegressionTests tests` → 0; "34 tests" vs 991 | Update Consequences/Related; assembly names will change with the identity migration (`product-identity.md`) |
| ADR-002 homebrew cutout rig | **MIXED** | Decision (cutout rig, `SnapSteps`) is the subject of this very branch; Context is the 6-Source×5-Role creature matrix (`:22-25`); `docs/PROJECT-NOTES.md:1025` says "THE RIG IS GONE. Characters are FLAT SPRITES (2026-08-13)". Liveness of `Core/Animation` is `presentation.md`'s finding | Add a dated addendum once the branch lands |
| ADR-003 pixel-perfect render path | **Superseded** — correctly bannered (`:4-10`) | — | Keep |
| ADR-004 warren facility economy | **MIXED** | Decision 1 (pure model, `Tick` returns yield) holds (`Warren.cs:167`). Decision 2 "Mastery Points … folds into `MasteryTree.SetEarned`" (`:31-35`) is **false** now (`Game1.cs:296-297`; INSIGHT "used only here", `WarrenScreen.cs:263`). Decision 3 "creature den preserved as CREATURES sub-view" (`:37-40`) is false (`Game1.cs:290`). "Rates start conservative" — since cut ~30× (`Warren.cs:29-43`) | Supersede with ADR-005 or add a dated amendment |
| `docs/architecture/tr-registry.yaml` | **empty** | `requirements: []`, `last_updated: ""` (`:27-29`) | Delete or keep as template; nothing reads it |
| `docs/registry/architecture.yaml` | **empty** | `state_ownership: []` (`:53`) | Same |
| `design/registry/entities.yaml` | **SUPERSEDED** | `last_updated: "2026-07-14"` (`:40`; git 2026-08-11); 51 facts, all old-model: `power_tier`, `telegraph_windup_floor_ms`, `hitbox_padding_by_part_size`, `active_efficiency_percent`, `par_clear_time_seconds`, `creature_core`, `static_cost_conversion_rate` (3.0 vs code 8.0) …; `grep -ciE 'style|hammer|volley|skilldef|trait road|insight'` → **0** | Archive; rebuild with ~15 live constants (`ConquestWave 20`, `DepthPerFacilityLevel 5`, set rungs 2/3/4/5, `MasteryPoints.Scale 0.9`, static-cost 8.0, `DustPerWave 25`, `OwnClassChance 0.80`, `SkillSlots 4` (2+2), `KeystoneSlots 3`, `MaxPowerBonus 1.5`) |
| `docs/architecture/control-manifest.md` (referenced by `docs/CLAUDE.md:22`) | **missing** | file does not exist (`ls docs/architecture`) | Drop the reference or write it |

---

## 5. Root, `production/`, `docs/`, store copy

| File | Classification | Evidence | Recommendation |
|---|---|---|---|
| `README.md` | **SUPERSEDED** (git 2026-07-24) | title "Resonance Hunter"; manual-combat controls table (Space dodge, F interrupt, 1/2/3 abilities `:44-56`); "hatch, farm"; "176 tests" (`:62`) vs 991; "Only Verdant Hollow exists" (`:119`) vs six regions; "27-document design" | Rewrite (short) — the store page's §1 is the current pitch |
| `production/ROADMAP-TO-RELEASE.md` | **SUPERSEDED** | "Roadmap to Release — Resonance Hunter"; Phase 0 fun gate for "combat windups, graze cost, limb-break … hatch rates" (`:52-54`) | Archive |
| `production/OVERNIGHT-REPORT.md` | **SUPERSEDED** session report | manual defences, role stations, hatching (`:12-25`) | Archive or delete |
| `production/stage.txt` = `Concept`, `review-mode.txt` = `lean` | stale | the game is a published pre-alpha (`docs/store/itch/README.md`) | Set stage |
| `docs/PROJECT-NOTES.md` | historical log (newest 2026-08-15) | contains "THE RIG IS GONE" (`:1025`), "WEIGHT ↔ SPREAD" footer notes (`:623-642`) | Leave as log; do not treat as spec |
| `docs/release-notes-v1.0.md` | **SUPERSEDED** | "Resonance Hunter — v1.0" that never shipped; "474 unit tests"; "Dust tree grew from 36 to 44 nodes … FORTUNE road" | Archive/delete |
| `docs/store/store-page.md` | **MIXED** | Title IDLExIDLE ✔ (`:1, 16`); §2 long copy "Skills are woven from a SOURCE … and a FORM" (`:37`), "6 sources, 6 forms, 12 vows" (`:52`) — self-described as "the older Steam draft" (`:31`) | Update §2 to Styles/Skills |
| `docs/store/itch/description.html` | **player-facing, stale — HIGH** | live page copy: "Skills are woven from a SOURCE … and a FORM" and `forms.png` alt "The six forms: Strike, Projectile, Aura, Trap, Mark, Transformation" (`:18-19`) | Update copy + regenerate the strip (`tools/marketing/make_itch_page.py`) |
| `docs/superpowers/specs/2026-07-28-release-readiness-design.md` | historical spec | — | Archive |
| `docs/engine-reference/{godot,unity,unreal}/` | template noise for engines not used | 40+ files | Delete (A) |
| `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md`, `WORKFLOW-GUIDE.md`, `TEMPLATE-README.md`, `docs/examples/` | template boilerplate | — | Leave |

---

## 6. CLAUDE.md-referenced docs (`.claude/docs/`) — drift

| Doc | Stale claim | Evidence |
|---|---|---|
| `technical-preferences.md` | "Gamepad (full, via cycle-and-confirm targeting)", "precision weak-point targeting is mouse-driven", "every combat interaction must also be completable via cycle-and-confirm" | no targeting code (`grep -rln Targeting src` → 0); the fight's only input is the speed selector (`game-flow.md:8-9`) |
| `technical-preferences.md` | "Architecture Decisions Log: [No ADRs yet]" (`:76`) | four ADRs exist |
| `technical-preferences.md` | Allowed libraries Myra / FontStashSharp / MonoGame.Extended / MonoGame.Aseprite "once actually integrated" | ADR-002 `:96-99` says Extended is not integrated; fonts are `PixelFont.cs` / `SmoothFont.cs`; UI is `UiKit.cs` — none of the four is in use (not re-verified against the csproj here; see `product-identity.md`) |
| `directory-structure.md`, `coding-standards.md` | generic template; fine | — |
| `docs/CLAUDE.md` | references `docs/architecture/control-manifest.md` | missing (§4) |

---

## 7. Contradictions between docs on the same topic

1. **Mastery branches** — `skill-and-trait-trees.md:55-66` WEIGHT/SPREAD/TEMPO/ENDURE with Form specialisations that "deepen that Form" (`:121-122`), vs `skill-slots-and-skill-trees.md:579-590` RESONANCE/LOOT/TEMPO/ENDURE with specialisations as road heads that teach skills. Code: `MasteryCatalog.cs:14-58` (`Branch.Resonance … "Was WEIGHT"`), `:460-463` (`Teaches`). `game-flow.md:242-244` sides with the old model ("mastery tree buys how skills behave"). `characters.md:29-40, 123-124`, `vows.md:72-75`, `regions-and-rosters.md:95-104`, `Archetypes.cs:18-21` (code comment) still say Weight/Spread. `skill-slots §4:152` lists the old branch names in its own naming note.
2. **What buys the permanent tree** — four answers in the docs, one in code. `memory-dust-prestige-system.md:35, 160`: Memory Dust, 19 unlocks, 660 MD. `progression.md:22`: "MASTERY POINTS → DUST → BLESSING TREE". `warren-facilities.md:29-35` and `ADR-004:31-35`: Warren "Mastery Points" fund the build tree. `game-flow.md:220, 552` (addendum): trait points; Dust "buys nothing permanent". Code: trait points (`MemoryDust.cs:121-132`; `PrestigeScreen.cs:468`); Dust buys Warren upgrades and checkpoints (`MemoryDust.cs:158-164`; `Checkpoints.cs:35`); Warren Insight buys only Warren upgrades (`WarrenScreen.cs:263`).
3. **characters.md vs CharacterRoster.cs** — roads Weight/Spread (`characters.md:29-31, 123-124`) vs `Lean = Branch.Resonance` / `Branch.Loot` (`CharacterRoster.cs:73, 88`; `ItemClasses.cs:97, 104`); "Two characters … quests" (`:25`) vs five (`Quests.cs:141-171`); "ANVIL and MAGPIE both Cinderworks" (`:81`) vs MAGPIE = THE FULL HOLD (`:38`, `Quests.cs:153-155`); doc has no `StartingSkillId`, code assigns one per champion (`CharacterRoster.cs:53, 70, 85, 99, 115, 129, 146, 166, 186, 209`). Aptitude is `Form?` in both (`Character.cs:117-118`) — consistent today, but Form is the legacy the refactor deletes (open question §15).
4. **warren-facilities.md vs ADR-004 vs Warren.cs** — rates (doc Nursery 520 `:44`; code 17 `Warren.cs:48`); Gleam cost (doc `18700 × 1.40^L` `:50`; code `180 × 1.50^L` `:85-86`); Mastery→tree (doc/ADR) vs INSIGHT local (`WarrenScreen.cs:258-263`); CREATURES sub-view (doc `:14`, ADR `:37-40`) vs retired (`Game1.cs:290`); milestones (`Warren.cs:99-102`), conquest bonus (`:96`) and the depth cap (`:248-251`) are in code and in neither document; `game-flow.md:293-305` adds a third version (facilities unlock by conquest, produce materials by tier) that is implemented only as the depth cap (`§4.5` = `DepthPerFacilityLevel 5`).
5. **vows.md vs resonance-weaving-system.md §3.3** — RWS: 10 conditional/static Vows watching the fight (`vow_bloodied`, `expected_uptime`, `:181-192`); vows.md: 13 build-demand Vows priced by severity (`:33-104`). Both say the static conversion is 3.0 (`RWS:387`, `vows.md:139`); code is 8.0 (`ResonanceWeaving.cs:153`). vows.md §3.5 node names (`:97-104`) are the pre-2026-08-25 names; `skill-and-trait-trees.md:339-344` and `MemoryDust.cs:319-332` have the current ones.
6. **quests.md internal** — "Two quests" (`:5`) vs a five-row table (`:32-38`); §3/§8 name THE FIRST VOW, which the same file's table lists as retired (`:41-43`).
7. **Conquest wave** — `progression.md:48` 7; `game-flow.md:542` 20; `Checkpoints.cs:29` 20.
8. **skill-and-trait-trees.md internal** — trait tree total 226 (`:369-371`) vs 137 (`:466`) vs `Game1.cs:5159` "~150"; income "two per corruption tier" (`:335`) vs `conquests + corruptionTiers + masteryGoals` (`:465`).
9. **Skill slots** — `game-flow.md:17` "four woven skills"; `vows.md:88` "four-skill weave"; `skill-slots §1` two active + two passive; code `Build.cs:311, 402` (`SkillSlots = 4`, `ActiveSlotsFor`).
10. **Static-cost Vows compound?** — `vows.md:84-94` says charged once (`SoloBattle.DistinctVows`); RWS says per bound ability (`:379-400`). Code follows vows.md (not re-read here; `legacy-weaving.md` covers `SoloBattle`).

---

## 8. `systems-index.md` — is it current?

**No.** Evidence:
- Header `Last Updated 2026-07-14` (`:5`), git says 2026-08-14 (`1b7baf7`).
- The table (`:25-58`) has 27 numbered rows for the July systems, every one marked `Designed`, including the two with SUPERSEDED banners (#10 combat-encounter, and expedition-auto-battle is not even listed).
- Five newer rows (Game Flow, Skill/Trait Trees, Regions/Rosters, Characters, Quests) were pasted at `:27-31` **without the `#`, Priority, Status or Depends-On columns**, so the table is malformed.
- Not listed at all: `vows.md`, `warren-facilities.md`, `progression.md`, `skill-slots-and-skill-trees.md`, `expedition-auto-battle.md`.
- Categories (`:120-135`), Dependency Map (`:154-200`), Recommended Design Order (`:203-240`), High-Risk table (`:258-267`) and Progress Tracker (`:269-281`, "27 design docs written, 0 approved") all describe the creature-roster game. Row #2 of the Categories table lists `rare-creature-capture-system` under Gameplay.
- The Overview paragraph (`:12-20`) names "weak-point targeting, telegraphs, part-breaks" and "a creature layer (capture, jobs, evolution)".

Recommendation: rewrite from scratch around the ~10 live systems (game flow, skills/styles, mastery, traits, vows, characters+classes, quests, regions/bands, gear/forge/loot/sets, warren, persistence, presentation), each pointing at one doc and one code root.

---

## 9. Product name in titles

"Resonance Hunter" appears in the **first line (title)** of 23 documents:
`design/gdd/`: accessibility-settings-system, animation-rig-system, audio-system, combat-encounter-system, creature-ai-telegraph-system, creature-data-schema, creature-jobs-evolution-system, encounter-spawn-system, game-concept, input-targeting-system, memory-dust-prestige-system, onboarding-tutorial-system, rare-creature-capture-system, region-mastery-automation-system, region-view-world-map-ui, settings-menu-ui, systems-index (17) · `design/art/art-bible.md`, `design/art/character-art-spec-batch1.md` · `README.md` · `production/OVERNIGHT-REPORT.md`, `production/ROADMAP-TO-RELEASE.md` · `docs/release-notes-v1.0.md`.
In body text of live docs: `game-flow.md:16`, `monetization.md:10`, `memory-dust-prestige-system.md:35`, `save-load-persistence.md:37`, `design/references/reference-games.md:3`.
`IDLExIDLE` appears in project docs only in `docs/store/store-page.md` and `docs/store/itch/README.md` (`grep -rlE 'IDLExIDLE|IdleXIdle' design docs README.md production --include=*.md` → those two plus this audit directory, session logs and art-reference READMEs). "Resonance" alone remains a valid gameplay word (mastery branch RESONANCE, stat `ResonanceAffinity`, `Resonance/Ruin/Aegis` are not affected).

---

## 10. Recommendation per doc (update / archive / delete)

**Update (keep at path, fix content)** — `skill-slots-and-skill-trees.md`, `game-flow.md`, `characters.md`, `vows.md`, `quests.md`, `warren-facilities.md`, `progression.md`, `regions-and-rosters.md`, `monetization.md` (name), `design/CLAUDE.md` (paths), `README.md` (rewrite), `docs/store/store-page.md` §2, `docs/store/itch/description.html`, `ADR-001` (evidence), `ADR-004` (amend or supersede), `.claude/docs/technical-preferences.md`, `design/audio/asset-generation-audio.md`.

**Split** — `skill-and-trait-trees.md`: trait half → new `design/gdd/traits.md`; skill half → archive with banner.

**Rescue-then-archive** — `resonance-weaving-system.md` (§3.1, §4.1, §4.2), `hunter-progression-system.md` (§3 catalog, §4 curve), `encounter-spawn-system.md` (power-tier rule), `save-load-persistence.md` (or rewrite from code).

**Archive to `design/archive/` with a SUPERSEDED banner naming the successor** — the 27 SUPERSEDED/ORPHAN GDDs (#1-4, 6-15, 19-24, 27-28, 30-31, 33, 37-38 above), `gdd-cross-review-2026-07-14.md`, `design/system-audit-2026-08-12.md`, `design/build-identity-proposal.md`, `design/registry/entities.yaml` (then rebuild), `production/ROADMAP-TO-RELEASE.md`, `production/OVERNIGHT-REPORT.md`, `docs/release-notes-v1.0.md`, `docs/superpowers/specs/…`.

**Delete** — `docs/engine-reference/{godot,unity,unreal}/` (template noise, ~45 files); the empty `tr-registry.yaml` / `architecture.yaml` unless the template skills are kept; the dangling `control-manifest.md` reference in `docs/CLAUDE.md`.

**Rewrite from scratch** — `systems-index.md`, `game-concept.md` (one page for IDLExIDLE), `entities.yaml`.

Suggested banner text for archived files (matching the existing one at `combat-encounter-system.md:4-10`): `> **SUPERSEDED <date> by <successor>.** Describes the <manual-combat | creature-roster | Source×Form weaving | Dust-bought prestige> model, none of which is in the runtime. Kept for vocabulary/history. Do not treat its rules as current.`

---

## 11. MIXED docs — sections that are the only spec for a live system (move before archiving/rewriting)

| Doc | Rescue these sections | Why they are the only spec | Fix while moving |
|---|---|---|---|
| `skill-and-trait-trees.md` | §3.4 spine table (ids, costs, grants `:329-348`), §3.5 four roads (keystone ids/costs `:350-400`), §3.6 rules both trees obey (`:402-425`), §4.2 income (`:463-466`), §5 trait rows (`:495-500`), AC 6 (`:539-540`) | Only prose for `MemoryDust.cs:309-535`, `TraitRoads.cs:38-46`, `Game1.TraitPointsEarned` | Reconcile 226 vs 137 vs ~150; one income formula; drop "Form" from ARTIFICE/WEAVER text |
| `skill-and-trait-trees.md` | §4.1 skill-point income (`:448-461`) | matches `MasteryPoints.cs:36-40` | Move into `skill-slots` §10 |
| `vows.md` | §3.1 "a Vow reads the build" (`:35-56`), §3.2 three families (`:58-68`), §3.4 static once-per-build (`:78-94`), §4 formulas (`:114-143`), §5-§8 | Only prose for `VowDemand`, severity, `IsActive` | Static conversion 3.0 → 8.0; node names → LEARN VOWS I/II/III, GEAR-SLOT VOWS, SACRIFICE VOWS; "argues against" table → RESONANCE/LOOT/TEMPO/ENDURE; decide what SINGULAR demands once Form is gone |
| `resonance-weaving-system.md` | §3.1 Sources table (`:95-116`), §4.1 Formula 1 (`:338-378`), §4.2 Formula 2 (`:379-400` + the B3 re-price note) | Formula 1 is implemented verbatim (`ResonanceWeaving.cs:292-304`); §3.1 is the only Source flavour table | Constants: `static_cost_conversion_rate` 3.0 → 8.0; drop "primary scaling stat" column unless `source_signature_test.cs` proves it |
| `characters.md` | §3 rules + roster table (`:19-40`), §9 Item Classes (`:112-161`) | Only spec for `ItemClasses.CanWear`, `ClassRollTuning.OwnClassChance 0.80`, legacy `Class == null` | Roads → RESONANCE/LOOT; five quest gates; add `StartingSkillId`; Aptitude → Style (or delete) |
| `quests.md` | §3 table (`:32-38`), latch rule (`:24-30`), §5 | matches `Quests.cs` | "Two" → five; THE FIRST VOW → THE THIRD OATH |
| `game-flow.md` | §3.1, §3.3, §3.4, §3.5, §3.9, §4.1, §4.3-4.8, all five addenda (`:540-648`) | Only prose for expedition, bands, report, checkpoints, the beat, element sets, VITALITY regen | Fold addenda into sections; Form → Style/Skill; §3.6 → point at skill-slots §7/§9; §3.8 → mark unbuilt bullets as intent or remove |
| `warren-facilities.md` | §1 overview, §5 edge cases (`:53-62`), §8 AC | still true | §3-§4 rewritten from `Warren.cs:48-102, 248` and `warren-economy.md` |
| `hunter-progression-system.md` | §3 stat catalog + training rule, §4 cost curve | only prose for `HunterProgression.cs` | 1.13 growth, cap 60, reset for one Crystal (`Game1.cs:4075`) |
| `progression.md` | §5 currency table (`:57-62`) | the only currency/sink table | add trait points, Insight, Dust→checkpoints, gems; drop the Dust→tree arrow; conquest 20 |
| `encounter-spawn-system.md` | power-tier assignment (§3.x) if `PowerTierBase` still drives item level | `EncounterTemplate.cs:33`; `progression.md:46` | verify first (§15) |

---

## 12. The 8 required GDD sections — gaps in the CURRENT docs the refactor will touch

| Doc | Missing / malformed sections (`design/CLAUDE.md` order) |
|---|---|
| `skill-slots-and-skill-trees.md` | Has §1 Overview and a rules body (§3-§9b). **Missing as headed sections**: 2 Player Fantasy, 4 Formulas (§10 covers beat demand only), 5 Edge Cases, 6 Dependencies, 7 Tuning Knobs, 8 Acceptance Criteria (test names are scattered in §11's table). Numbered 1-11 + 9b. |
| `characters.md` | All 8 present (+§9). Acceptance boxes unchecked (`:103-110`). |
| `vows.md` | All 8 present. |
| `quests.md` | All 8 present. |
| `warren-facilities.md` | All 8 present (content stale). |
| `game-flow.md` | All 8 present; five addenda live outside the structure (`:540-648`). |
| `progression.md` | §3 "core loop" and §4 "region ramp" stand in for Detailed Rules / Formulas; **no Formulas section as such, no Edge Cases**. |
| `skill-and-trait-trees.md` | All 8 present (half the content superseded). |
| `regions-and-rosters.md` | All 8 present. |

---

## 13. Undocumented live systems (the reverse of ORPHAN — code with no current doc)

These have no CURRENT or MIXED GDD; their only prose is a superseded doc or code comments:
- Gear / affixes / enchantments / gems / families / element sets / reforge / refine — `Economy/*`, `Forge/*`, `Loot/LootSystem.cs` (old docs #20, #21, #37 describe a different model). Sibling `gear-items.md` has the material.
- Skill progress (levels from use, variation/reinforcement purchase) — `Builds/SkillProgress.cs`, `SaveGame.cs:698-709`; only `skill-slots` §5/§6 prose.
- Onboarding tour / tutorial — `Progression/Onboarding.cs`, `Tutorial.cs`.
- Settings panel — `Game1.cs:242, 1314`, `DisplaySettings.cs`.
- Map screen, checkpoints — `MapScreen.cs`, `Checkpoints.cs` (only the game-flow addendum).
- Chests, gift chests, wandering trader, charters — `Expeditions/Chest*.cs`, `GiftChests.cs`, `Economy/WanderingTrader.cs`, `Charters.cs`.
- Share codes — `Persistence/ShareCodes.cs` (memory: accepted feature).
- Corruption scaling / region modifiers — `Encounters/CorruptionScaling.cs`, `RegionModifiers.cs` (only `progression.md:52` and release notes).

---

## 14. Numbers the docs get wrong (quick reference for the rewrite)

| Claim | Doc | Code |
|---|---|---|
| Conquest at wave 7 | `progression.md:48` | 20 (`Checkpoints.cs:29`) |
| Static Vow conversion 3.0 | `vows.md:139`, `RWS:387`, `entities.yaml` | 8.0 (`ResonanceWeaving.cs:153`) |
| Nursery 520/min; Gleam cost 18700 × 1.40^L | `warren-facilities.md:44, 50` | 17/min; 180 × 1.50^L (`Warren.cs:48, 85-86`) |
| 19 trait unlocks / 660 Dust | `memory-dust-prestige-system.md:160` | 51 nodes, trait points (`MemoryDust.cs:309-535`) |
| Trait tree cost 226 / 137 | `skill-and-trait-trees.md:369, 466` | "~150" (`Game1.cs:5159`) — not summed here |
| Two quests | `quests.md:5` | five (`Quests.cs:141-171`) |
| Two quest-gated characters | `characters.md:25` | five |
| Four skill slots, all active | `game-flow.md:17`, `vows.md:88` | 4 = 2 active + 2 passive (`Build.cs:311, 402`) |
| 176 / 215 / 474 tests | `README.md:62, 106`, `release-notes:35` | 991 attributes |
| One region | `README.md:119` | six (`Regions.cs:68-94`) |
| Six forms (player-facing) | `itch/description.html:18-19`, `store-page.md:52` | six styles, twelve skills (`SkillCatalogue.cs:34-52, 264-568`) |

---

## 15. Open questions (not asserted above)

1. **Rig liveness** — `SoloExpeditionScreen.cs:8` imports `ResonanceHunter.Core.Animation` but my grep for `\bRig\b|\bClip\b` in that file found only a comment (`:3393`); `presentation.md` is the authority. Until it answers, `animation-rig-system.md` and ADR-002 cannot be finally classified.
2. **Affix table** — `regions-and-rosters.md:95-104` names NUMBERS/PLATED/WARDED/LEGION; `RegionModifiers.cs:25-30` has different names. I did not read `Encounters/Bands.cs` / `BandCycles.cs`; the affix cycle may live there under these names.
3. **Aptitude after Form** — `Character.Aptitude` is `Form?` (`Character.cs:117`) and `characters.md` documents it as a Form; once Form is deleted, does Aptitude become a Style multiplier or go? Same for `VowDemand.SingleForm` (VOW OF THE SINGULAR, `ResonanceWeaving.cs:387-388`) and the six Form-named enchantments (`Enchantments.cs:55-70, 151-155`).
4. **Power tier → item level** — `progression.md:46` says a kill's item level = its power tier; `EncounterTemplate.PowerTierBase` exists (`:33`). Whether `LootSystem` reads it decides whether `encounter-spawn-system.md` has anything to rescue.
5. **Hybrid merge table provenance** — `MergeRecipe.cs:69 HybridTable` vs `the-forge-system.md` §3.3: same recipes or independent? Determines whether that doc holds any live data.
6. **Region mastery levels** — `Game1.cs:5159` counts "18 mastery goals" toward trait points and `recall_1..4` speed them; the only spec is the superseded `region-mastery-automation-system.md`. What the live goal ladder is (thresholds, per region) needs a code read of `RegionAutomation.cs` / `EfficiencyContract.cs`.
7. **Source "primary scaling stat"** — `RWS §3.1` maps each Source to a Hunter stat; whether the live Source signatures (`source_signature_test.cs`) implement that mapping or the 2026-08-20 riders (`build-identity-proposal.md:38-47`) I did not verify.
8. **Trait tree total cost** — three figures (226 / 137 / ~150); summing `MemoryDust.cs:309-535` costs would settle it (not done here).
9. **Boss HP table** in `progression.md:41-46` and the region tier column were not checked against `Regions.cs:100-130`.
10. **`design/art/art-bible.md`** was not audited beyond its title and date; `arena-art-contract.md` declares itself authoritative for stage art, so the bible is at least MIXED.
