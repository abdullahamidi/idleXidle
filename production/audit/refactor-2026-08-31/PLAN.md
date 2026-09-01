# IDLExIDLE architectural refactor — implementation plan

2026-08-31 · branch `feat/hunter-cutout-rig` · baseline: 1225 unit + 2 integration tests green.
Inputs: the 14 area audits + 14 adversarial verifications + `00-critique.md` in this directory, plus
first-hand reads of SoloBattle/Build/SkillShape/SkillCatalogue/BuildComposer/SoloExpedition/
ResonanceWeaving/PlayerLoadout/SaveGame and a bench probe (PULSE measured net-negative).

## 0. The one sentence

The current game already IS `STYLE → SKILL → VARIATION → REINFORCEMENT` in its catalogue — but the
save, the composer, the fight loop and the wire still speak `(Form, Passive)`; the variation layer is
DORMANT in real play (the hunt composes without `Progress`); and base damage is a six-row Form table.
The refactor threads SkillId end-to-end, gives skills their own numbers, deletes Form from runtime,
and cleans up ownership around that spine.

## 1. Ownership matrix (target)

| System | Answers | Owns / may modify | Must NOT own |
|---|---|---|---|
| Character | "Who am I?" | StartingSkillId; ONE passive (Shape/Mods/Grants via BuildComposer); ItemClass; card copy | generic per-Style damage tax (Aptitude DELETED); battle-loop branches; separate progression |
| Mastery | "How am I specialising NOW?" | respeccable tree: branch philosophy (RESONANCE/LOOT/TEMPO/ENDURE), Style specialisations, skill DISCOVERY gates; Shape/Stats/Triggers channels | re-implementing a Style's owned verbs; un-learning discovered skills on respec; trait permanence |
| Skill (SkillDef) | "What technique?" | Id, Style, Kind, Effect, timing (Beats/IntervalMs/On), Targets, **BasePower**, typed dials, ClipKey/FxKey, its two variations | Form; another skill's numbers; presentation implementation |
| Variation | "Which interpretation?" | Source attachment + `Func<SkillDef,SkillDef>` delta | a branch in the fight loop |
| Reinforcement | "How specialised?" | delta on the chosen variation | new battle-loop state (write to dials that exist) |
| Vow | "What restriction?" | `Builds/Vows.cs`: demand catalogue, severity→multiplier, build validation (WeaveContext) | fight-state conditions; Form (SingleForm→SingleStyle) |
| Trait (Dust tree) | "What has my account become?" | permanent nodes, roads (SPINE/RUIN/AEGIS/AVARICE/ARTIFICE), keystone/vow/slot TEACHING | respeccable build tuning; duplicate mastery numerics |
| Gear | "What numeric tools?" | rarity/ilvl/affixes/traits (tradeoffs), enchant magnitudes, additive-then-fold stacking | skill identity; free multiplicative stacking |
| Set | "What equipment plan?" | 2/3/4/5 rungs per Source, 5-piece rule-changer | its own combat engine (reuses SkillShape dials) |
| Quest | "What behaviour did I prove?" | measurable Career counters → character unlocks | RNG completion; unmeasurable state |
| Warren | "What happens while I'm away?" | idle production UNDER the champion's depth cap, targeting/automation services, milestones | outrunning active play; combat multipliers; closed-loop currencies |
| Core `Descent`/`Career` (new) | "the run & the account ledger" | headless run state machine + counters (deepest, chests, vows kept, per-style tallies) | anything MonoGame |

Power ownership: raw numbers → gear+BasePower+training · build specialisation → mastery · technique
transformation → variation/reinforcement · conditional power → vow · permanence → traits · one rule →
character · commitment → sets · horizontal unlocks → quests · idle acceleration → warren.

## 2. Decisions taken (with the evidence that forced them)

- **D1 SkillId becomes the identity in one commit** (critique #1): `SavedSkill.SkillId` + Version 3 +
  `Persistence/LegacySkillForm` map (Form,Passive)→SkillId + `EquippedSkill` stores the resolved
  `SkillDef` (also the perf fix: `.Def` re-resolves via 2 LINQ scans ~100k times/wave today) + share
  codes `RHB2` (RHB1 accepted) + JSON fixtures of real pre-08-30 saves.
- **D2 `SkillDef.BasePower` replaces `FormBaseValue`** — the six null-LegacyForm skills get AUTHORED,
  bench-measured numbers (PULSE is net-negative today; JAWS/IRON re-arm at an 8000ms fallback vs
  their 3s/6s cards). Resonance scaling stays one multiplier beside it.
- **D3 Affinity ports Form-hexagon → Style-ring** and is re-measured. The Style ring's opposites are
  the DESIGNED ones (SkillCatalogue doc: HAMMER↔VOLLEY, SNARE↔FIELD, SIGN↔DRAIN); the Form ring
  contradicted the tree. This IS a balance change (verified): re-run sweeps, retune only what leaves
  its band. FormHexDiagram → Style ring diagram; `spec_*` node ids KEPT (opaque strings).
- **D4 Variation layer goes live** (critique #2): pass `Progress` at SoloExpeditionScreen:775,
  WeaveScreen:610/1370, StatsScreen:174; include progress in the build stamp; hunt-path test. The
  newly-live defects are fixed in the same phase: SIGN double-apply + no-expiry + STEADY int
  overflow, GLUT "no lifesteal" healing, VENGEANCE windowless, CLUSTER (gains `HitsPerTarget` — a
  real RepeatedHits need), reaction re-arm dial.
- **D5 Aptitude is REMOVED** (brief §36 preferred direction). Where a card TEXT promises a style
  bonus (THORNWALL "every trap you set does more") it moves into that character's `Shape.StylePower`;
  unadvertised aptitudes (e.g. MAGPIE's) just go. `FormPower/FormTargets` → `StylePower/StyleTargets`
  (weapon families keep their favour, re-keyed on Style).
- **D6 Vows survive, move, stay per-skill.** `Builds/Vows.cs`; `SingleForm`→`SingleStyle` (verified
  semantics-preserving for every savable build); copy rewritten STYLE. Per-skill attachment is kept —
  it is the live design, not legacy.
- **D7 Skill discovery is permanent** (brief §25): additive `SaveGame.LearnedSkills`, seeded from
  MasteryTaken road nodes + StartingSkillId (the UnlockedCharacters pattern); respec refunds points
  but never un-learns; the road node remains the discovery gate; UI says so.
- **D8 Events re-key on slot index** with typed payloads; `Shield` split (duration vs amount —
  confirmed bug); Aura/Skill stop carrying `(int)Form`. Presentation resolves clip/fx via the
  equipped Def. Persistent-state gaps the screen actually draws (slow, attack-break, stun, amplify
  depth, poison, banked shield) get state on WaveCreature/typed events — only where a visual
  consumes them.
- **D9 Core `Descent` + `Career` extracted from Game1/hunt screen** (critique G2/#6): headless run
  state machine + account ledger; PlayerLoadout moves to Core; offline credit becomes real
  simulation (capped), replacing the rate estimate. Unblocks quest counters, loop probes, and
  migration tests.
- **D10 Six combo enchants re-key**: Overdraw/Execute/Coiled → Style (Volley/Hammer/Snare);
  Radiance/Linger/Siphon → Kind/Effect (Field/Amplify/Heal) — matching what the sim actually gates
  (verified C8). All three producer channels (enchants, mastery Spec nodes, character grants) move
  together (C10). The Forge "FITS/NEEDS" badge tells the truth again.
- **D11 Save armour is step 0** (G8): TryParse for `ItemBaseType`, guarded region-farm restore,
  pre-migration snapshot `save.pre-v<N>-<ts>.json`, fixture corpus, boot-crash tests. Load never
  throws.
- **D12 Identity rename**: classic `IdleXIdle.sln` (SDK 8 parses it; CI+local converge) +
  folder/csproj/namespace sed (`ResonanceHunter.Client`→`IdleXIdle.Game` first, then generic). Save
  folder `%LOCALAPPDATA%\ResonanceHunter\` is HOISTED to one constant and KEPT (renaming orphans
  every player's save). `RH_*` env vars: `DevEnv` helper accepts `IXI_*` and `RH_*`; tools keep
  working.
- **D13 Warren INSIGHT / trait-budget / dead-affix decisions** are made in their phases against
  their reports — flagged now: INSIGHT is a closed loop (produced & spent on one screen); live trait
  budget 28 < cheapest terminal 31 (all four terminals unreachable — the pinning test passes via a
  dead enum member); WARDED/LEGION/ENTRENCHED/HOLLOW affixes do nothing while bands advertise them.

## 3. Deliberately NOT done

No ECS/DSL/scripting/property-bags (brief §76). No `Delivery` enum — `Targets` + Kind + On already
express current content; adding one would be Form-under-a-new-name. No event-sourcing rewrite — the
resolve-wave-then-replay model already gives determinism + fast-forward. Asset keys
(`strike`,`aura`,…) stay opaque strings (renaming 40 PNGs is churn). Save folder name stays. `Solo*`
class names stay (one mode; renaming is churn — recorded as an accepted stale term). Set 2/3/4/5
structure stays. Build-screen LAYOUT untouched (active.md: three failed passes; open design
question stays paused).

## 4. Phase order (each ends: build green, tests green, committed)

- **P0** save armour + fixtures (D11)
- **P1** identity rename (D12)
- **P2** SkillId spine (D1)
- **P3** de-Form the sim (D2, D3, D6, D8, D10: BasePower, Effect read, Style ring, Vows.cs,
  SourceMatchup, events, EnchantNeed, StylePower, WeaveContext, FormBehaviour + WovenAbility deleted,
  Form → migration only; test-fixture rewrite via a shared TestBuilds helper)
- **P4** variation layer live + BLOW vertical slice end-to-end test + newly-live defect fixes +
  balance re-baseline (D4)
- **P5** Core Descent/Career + offline simulation + PlayerLoadout→Core (D9)
- **P6** Mastery (Style specs, discovery permanence D7, orphan-dial verdicts, stale copy incl.
  Onboarding/Tutorial — TutorialStep names are persisted; point economy)
- **P7** Traits (budget vs terminals, gate enforcement: KnowsVow + KeystoneCapacity, SPINE audit,
  Dust-vs-TraitPoints terminology)
- **P8** Vow polish (validation UX, catalogue copy; behaviour-buying rewards only where they fit)
- **P9** Characters (D5; Lean re-point; passive liveness)
- **P10** Quests (Career-backed, diversified)
- **P11** Gear/Sets (stacking ledger + ceilings test, set rung liveness + renames, loot pipeline
  waste, readability)
- **P12** Warren/economy (INSIGHT verdict, facility identity slice, depth-cap test, wallet
  placement, host-literal faucets → Core tuning)
- **P13** Encounters (dead affixes implement-or-cut; RunReportSave enums by name)
- **P14** persistence finish (runtime Form zero; old→new→play→save→reload fixture suite; dead fields
  dropped w/ clearing migrations)
- **P15** UI/docs (terminology, SUPERSEDED banners, sources.md, systems-index, README, memory files)
- **P16** final sweep (grep table with justifications) + report (brief §110) + repo hygiene (tracked
  audit_character.png / lfs-trace.txt, stale release folder)

Each phase begins by reading its area report + verify file in this directory. Cite symbols, not the
reports' line numbers (several drifted — critique §5).

## 5. Acceptable ResonanceHunter/Form survivors (target end-state)

save-folder constant (+ check_boot.sh:32) · `Persistence/LegacySkillForm` migration map ·
`SavedSkill.Form` as optional legacy member · RHB1/RHI1 share-code readers · dated historical docs &
session logs · README "formerly" line · RH_* accepted-alias reads in DevEnv.
