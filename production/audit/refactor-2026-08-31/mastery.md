# Audit — Mastery tree (the respeccable "how am I specialising now" layer)

*Audit date: 2026-08-31 · branch `feat/hunter-cutout-rig` · read-only on source.*
*Evidence convention: every claim carries `file:line` that was read in this session; every "dead" claim carries the grep that was run and what it returned. Line numbers are as of commit `ae30f2a`.*

Test evidence gathered for this audit:
`dotnet test tests/unit/ResonanceHunter.Core.Tests --filter "FullyQualifiedName~Mastery|FullyQualifiedName~mastery|FullyQualifiedName~OneDiscipline|FullyQualifiedName~LootNode|FullyQualifiedName~skill_slot_kinds|FullyQualifiedName~Affinity"` → **150 passed, 0 failed** (6 s).

---

## 0. Headline

The mastery tree is in the middle of a half-finished migration and the halves disagree. The **engine** (`MasteryTree`, `MasteryPoints`, `MasteryLayout`, the `Stats`→`Hunter.ValueOf` channel, the liveness test files) is sound, small and well-tested — every non-road node is proven to move the fight or the haul, respec is genuinely free, points are derived and never persisted. The **catalogue**, however, still carries the pre-re-axe skeleton: six Form-keyed *specialisations* that grant Form-combo triggers (Execute/Overdraw/Linger/Radiance/Coiled/Siphon), a `Form?` discipline that the battle loop multiplies every skill by, and a TEMPO branch that sells execute (ASSASSINATE) and cooldown reduction (BLITZ, RUSH, the VOLLEY bridge) — exactly the verbs design §7 says belong to HAMMER and VOLLEY. The 12 skill-unlock road nodes work end to end, but respec **re-locks** them (opposite of the brief's preference), and because a hunter may hold only one specialisation and every road hangs off one, a champion can learn at most **three distinct skills** (two from one style's road plus its birth skill) for four slots. `SkillShape` carries 18 fields no content in the game writes any more (WEIGHT/SPREAD remnants) while `SoloBattle` still branches on all of them; the tree screen has no case for `MasteryKind.SkillRoad` and captions a road node "START"; and player-facing tour copy still explains WEIGHT and SPREAD. Three unrelated things are called "mastery" (build tree, region mastery that pays trait points, Warren INSIGHT).

---

## 1. Node inventory

### 1.1 Counts

Source: `src/ResonanceHunter.Core/Builds/MasteryCatalog.cs` `Build()` (133-147) and the four branch methods (160-402), `Bridges` (404-418), `Specialisations` (432-472).

| Kind | Per branch | Total | Cost each | Cost total |
|---|---|---|---|---|
| Start | — | 1 | 0 | 0 |
| Minor (5 spine + 1 spur) | 6 | 24 | 1 | 24 |
| Notable | 5 | 20 | 3 | 60 |
| Greater | 4 | 16 | 5 | 80 |
| Mastery (capstone) | 1 | 4 | 8 | 32 |
| Bridge | — | 4 | 6 | 24 |
| Specialisation (Form-keyed) | — | 6 | 6 | 36 |
| SkillRoad | — | 12 | 5 | 60 |
| **Total** | 16 | **87 nodes** | | **316 points** |

`MasteryTreeTests.cs:84-85` pins `TotalCost == 316`; `:52-56` pins `BranchCost == 49` for each branch; `:65-75` pins 6/5/4/1 and exactly one spur per branch.

Per-branch composition is identical: RESONANCE (160-203), LOOT (213-255), TEMPO (278-335), ENDURE (356-402).

### 1.2 Pure stat vs rule-changing

- **24 pure-stat nodes** — every ring-1 minor: `Stats` dictionary only, `Shape == SkillShape.None` (`MasteryCatalog.cs:165-173, 218-225, 282-288, 360-366`). They pay `HunterStat` values (`ResonanceAffinity`, `Guile`, `AttackPower`, `Engineering`, `CriticalChance`, `Focus`, `MaxHealth`, `Defense`, `Vitality`) and reach the fight only via `MasteryTree.Stats()` → `Game1.cs:2757 _hunter.SetMasteryStats(_mastery.Stats())` → `HunterProgression.cs:198-199 ValueOf`.
- **1 mixed node** — DEEP (notable): `Stats` +12 resonance AND `Shape.ResonanceWorth = 0.20` (`:185-187`).
- **44 rule nodes** — 20 notables, 16 greaters, 4 capstones, 4 bridges: each sets one or more `SkillShape` fields.
- **6 specialisations** — each grants a `BuildTrigger`; three also set a shape (`spec_strike` Cull, `spec_projectile` FormTargets, `spec_mark` MarkWindowMultiplier) (`:434-448`).
- **12 road nodes** — `GrantsSkillId` only, `Shape == None` (`:479-487`).

Of the 44 rule nodes, these are **bare-multiplier trades** (their whole content is scalar multipliers on `DamageDealt`/`HitSize`/`SkillRate`/`DamageTaken`/`AutoAttackDamage`):
- RESONANT — `DamageDealt 1.25, AutoAttackDamage 0.85` (`:194-195`)
- BLITZ — `SkillRate 1.43, HitSize 0.80` (`:318-320`)
- RUSH — `SkillRate 1.25, DamageTaken 1.15` (`:326-328`)
- BULWARK — `DamageTaken 0.70, DamageDealt 0.85` (`:388-390`)
- DISCORD — half rule (`WeakMatchupRelief 1`) half multiplier (`DamageDealt 0.90`) (`:192-193`)

They satisfy `test_every_node_changes_a_shape` (`MasteryTreeTests.cs:350-357`) because that test accepts any non-`None` shape, including a shape that is only multipliers. The "no bare multiplier" law is therefore enforced only by convention above ring 1.

### 1.3 Capstones ("stars") and specialisations

| Node | Kind | What it does | Source |
|---|---|---|---|
| CHORD | Resonance capstone | every Source matchup counts as strong; all skills ×0.85 | `MasteryCatalog.cs:201-202` → `SoloBattle.cs:727-731` |
| PROSPECTOR | Loot capstone | +3% haul per wave past depth 30; skills ×0.85 | `:252-254` → `SoloExpedition.cs:469-470` |
| FIRST STRIKE | Tempo capstone | +150% for 3 s each wave, −40% after | `:332-334` |
| ENDLESS | Endure capstone | full heal every wave; max health ×0.5 | `:399-401` → `SoloExpedition.cs:367` |
| STRIKE SPECIALIST (`spec_strike`) | Specialisation, Form.Strike | grants `Execute` + Cull <35% +40% | `:434-435` → `SoloBattle.cs:1613, 830` |
| TRAP SPECIALIST (`spec_trap`) | Form.Trap | grants `Coiled` (trap re-arms faster) | `:436-437` → `SoloBattle.cs:1909` |
| VOLLEY SPECIALIST (`spec_projectile`) | Form.Projectile | grants `Overdraw` (+1 cast) + `FormTargets[Projectile]+1` | `:438-439` → `SoloBattle.cs:1551`, `SkillShape.cs:442` |
| AURA SPECIALIST (`spec_aura`) | Form.Aura | grants `Radiance` (aura ticks 3/5 interval) | `:440-441` → `SoloBattle.cs:1261` |
| MARK SPECIALIST (`spec_mark`) | Form.Mark | grants `Linger` + `MarkWindowMultiplier 1.3` | `:442-443` → `SoloBattle.cs:1382, 1507` |
| MORPH SPECIALIST (`spec_transformation`) | Form.Transformation | grants `Siphon` (leech ×, heal ceiling raised) | `:446-448` → `SoloBattle.cs:639, 1438, 1730` |

Taking any specialisation also sets the hunter's **discipline** (`MasteryTree.Affinity()` `:253-257` → `Build.Affinity` → `FormBehaviour.AffinityFactor` `:245-247`, ×2.00 on the Form, ×0.45 opposite), which `SoloBattle.Amp` applies to every skill hit (`SoloBattle.cs:709-719`). Only one may be held (`MasteryTree.CanTake` `:196`), only one capstone may be held (`:191`).

### 1.4 Nodes that duplicate a Style's own combat identity

Design §7 (`design/gdd/skill-slots-and-skill-trees.md:446-518`) states the rule — "if a rule is about a skill, it belongs in the skill tree" — and lists what leaves and where. The following are still in `MasteryCatalog.cs` and still read by the battle loop:

| Mastery node | Verb it sells | §7 says it belongs to | Skill-side owner already in code |
|---|---|---|---|
| `spec_strike` (Execute trigger, Cull) `:434-435` | execute weakened foe | HAMMER (`:475`: "ASSASSINATE, CULL → HAMMER — execute threshold") | `SkillDef.ExecuteFraction` HAMMER/FINISH, read `SoloBattle.cs:1664-1665`; the trigger path is `SoloBattle.cs:1613-1616` keyed `form == Form.Strike` |
| `assassinate` (Tempo greater) `:321-323` | once-a-wave execute | HAMMER | same — `SoloBattle.cs:1621-1628` is a third execute rule |
| `blitz` `:318-320`, `rush` `:326-328`, bridge `volley` `:409-411` | cooldown reduction (`SkillRate`) | VOLLEY (`:478-479`: "BLITZ, RUSH → VOLLEY — cooldown reduction"); §9 `:592-593`: "RESONANCE must never sell cooldown reduction — that mechanic belongs to VOLLEY" | `SkillDef.CooldownMultiplier`, Spirit set `SkillRate 1.06` (`ElementSets.cs:102`) |
| `spec_projectile` (Overdraw, FormTargets+1), bridge `volley` (`ExtraTargets 1`) | hit count / target count | VOLLEY / FIELD (`:477`) | `SkillDef.Targets`, `TargetsBonus`, `MinimumHits`; VOLLEY/SPLAY |
| `alpha`, `opening_volley`, `rhythm` `:300-302, 315-317, 297-299` | first-hit / first-cast / cast-ramp | SIGN (`:481`: "MARK MASTERY, OPENER, ALPHA, OPENING VOLLEY, PREPARATION, RHYTHM → SIGN") | `SkillDef.AmplifyPercent/AmplifyMs/AmplifyPerCast` |
| `spec_mark` (Linger, MarkWindowMultiplier) | mark window length | SIGN | `SkillDef.AmplifyMs` SIGN/CALL |
| `thorns` `:381-383`, `rebound` `:385-387` | reflect | SNARE (`:483`: "THORNS, REBOUND → SNARE — reflect") | `SkillDef.ReflectFraction` SNARE/JAWS (same field name, different record) |
| bridge `feedback` `:412-414`, `spec_transformation` (Siphon) | lifesteal | DRAIN (`:484`: "LEECH, FEEDBACK → DRAIN — lifesteal") | `SkillDef.Lifesteal`, `SwingLifesteal`, Nature set `Leech` |
| bridge `executioner` `:406-408` (FirstHit ×1.10, ArmourPenetration 9) | first hit + defence break | HAMMER (`:475`, `:485`) | `SkillDef.DefenceBreakPerTick` HAMMER/PRESS, `DefenceIgnore` |
| `spec_aura` (Radiance) | field cadence | FIELD | `SkillDef.IntervalMs` |

§7 itself ends with "Nothing here is implemented" (`:516`); §9b says RESONANCE/LOOT were "BUILT 2026-08-30" and TEMPO/ENDURE re-minored, but the TEMPO rules named above were **kept** (§9b `:790-792` lists what was cut: OPENER, HASTEN, PREPARATION, FOCUS, FLASH, MARK MASTERY). So the document contradicts itself: §7's table says ALPHA/OPENING VOLLEY/RHYTHM/ASSASSINATE/BLITZ/RUSH leave; §9b/`MasteryCatalog.cs:278-335` keeps them. The specialisations were never re-examined at all — `MasteryCatalog.cs:420-431` still describes them as "the six FORM-COMBO triggers".

Also relevant: `Branch` enum doc (`MasteryCatalog.cs:25`) says "Never cooldown: VOLLEY owns it" about RESONANCE — and TEMPO sells it three times.

---

## 2. The 12 skill-unlock ("road") nodes

### 2.1 Definition and gating

- Declared `MasteryCatalog.cs:460-471` via `Teaches()` `:479-487`: kind `MasteryKind.SkillRoad`, ring 3, cost `SkillRoadCost = 5` (`:115`), `Shape = None`, `GrantsSkillId = <skill id>`, label `"{def.Name} — {def.Line}"`.
- Wiring (two per style, chained): `road_hammer` ← `spec_strike`; `road_hammer_2` ← `road_hammer`; `road_snare` ← `spec_trap`; `road_volley` ← `spec_projectile`; `road_field` ← `spec_aura`; `road_sign` ← `spec_mark`; `road_drain` ← `spec_transformation` (and each `_2` off its first).
- A specialisation needs any Notable of its branch (`Spec()` `:542-545` → `NotablesOf`), costs 6, and **only one may be taken per hunter** (`MasteryTree.cs:196`).
- **Cheapest path to one taught skill:** minor 1 + notable 3 + specialisation 6 + road 5 = **15 points**; the style's second skill +5 = **20**. The comment at `MasteryCatalog.cs:455-457` ("a whole road to one skill is 11 points… Six nodes, six skills") is stale on both counts.
- The tree opens at wave 25 (`Unlocks.cs:136, 181`); wave 40 in one region pays 5 points (`MasteryPoints.cs:21`), so a specialisation is deliberately out of reach in the first quarter hour (`mastery_points_test.cs:23-30`).

### 2.2 Where learned skills live — the exact path

There is **no separate "learned skills" state anywhere**. The chain is:

1. `MasteryTree._taken : HashSet<string>` — the only mutable state (`MasteryTree.cs:153`).
2. `MasteryTree.LearnedSkills()` — derived each call: every taken node with `GrantsSkillId` (`:275-279`).
3. `BuildComposer.Compose` — `taughtSkills = mastery.LearnedSkills() ∪ { character.StartingSkillId }` (`BuildComposer.cs:154-155`); the gate is `if (!taughtSkills.Contains(def.Id)) continue;` (`:178`) — an unlearned pick is **silently not woven** into the `Build`.
4. `PlayerLoadout.ToBuild` only gathers lists and calls `Compose` (`PlayerLoadout.cs:255-267`). The loadout keeps the pick; only the composed `Build` drops it.
5. Screens: `WeaveScreen.KnownSkills()` (`WeaveScreen.cs:1373-1378`) = same union; used to refuse a kind toggle (`:692-695`, message "… IS LEARNED ON THE MASTERY TREE, ON HAMMER'S ROAD"), refuse a library pick (`:835-838`), draw the slot as empty (`:1044`), and grey the library (`:1489-1491` "NOT LEARNED").
6. Persistence: `SaveGame.MasteryTaken : List<string>` (`SaveGame.cs:161-162`), written `Game1.cs:892`, restored `Game1.cs:642 _mastery.RestoreTaken(save.MasteryTaken)`; unknown ids dropped (`MasteryTree.cs:172-179`, `MasteryTreeTests.cs:601-609`). Share codes carry the same list (`ShareCodes.cs:50`, `WeaveScreen.cs:755`).

Note: `BuildComposer.SlotKinds(skills, capacity, taught)` accepts a `taught` set and discards it — `_ = taught;` (`BuildComposer.cs:64-76`). The parameter is dead; the gate lives only in `Compose`.

### 2.3 Does a respec un-learn the skill?

**Yes — respec re-locks.** `MasteryTree.Respec()` clears `_taken` (`:230-234`); `LearnedSkills()` is then empty; the next `Compose` drops every taught skill. Documented as the designer's call: `MasteryTree.cs:88-91` ("Respec RE-LOCKS (designer, 2026-08-30). Nothing here is monotone"), `BuildComposer.cs:160-161`, design §11 stage 8c (`skill-slots-and-skill-trees.md:929`), and asserted by `skill_slot_kinds_test.cs:264-267` (`walked.Respec(); Assert.Empty(With(walked).Skills)`).

Single-node `Refund("spec_x")` is refused while a road stands on it (`MasteryTree.cs:215-225` stranding check; `one_discipline_test.cs:36-38` shows refunding the spec re-opens the discipline).

The brief prefers *once discovered, permanently learned; the node stays the discovery requirement*. Implementing that requires a new persisted set (there is none today — see §8 serialization). Candidate: derive it on first load from `MasteryTaken` roads so old saves keep what they learned.

### 2.4 A structural consequence worth a decision

Because (a) roads hang only off specialisations, (b) one specialisation per hunter (`MasteryTree.cs:196`), and (c) `taughtSkills` is roads ∪ birth skill (`BuildComposer.cs:154-155`), a champion can know at most **3 distinct skills** (2 if its birth skill is in the chosen style, e.g. THE ANVIL `hammer_press` + HAMMER road, `CharacterRoster.cs:70`). The build has 4 slots at capacity (2 active + 2 passive, `BuildComposer.cs:141-142`). `Build.Weave` does not refuse the same skill twice (`Build.cs` Weave body: capacity checks only; grep `duplicate` in Build.cs → only an unrelated comment at line 295), so the fourth slot is either a duplicate or empty. Design §11 (`:912-914`) says duplicates "must be refused" — not implemented. I could not verify in this pass what two copies of one active do in the fight; flagged as an open question.

---

## 3. Point economy

- **Earning:** `MasteryPoints.FromDepth(bestDepth) = floor(sqrt(depth) × 0.9)` per region; `Total` sums regions (`MasteryPoints.cs:36-49`). Curve pinned: 25→4, 40→5, 64→7, 100→9, 150→11, 225→13 (`mastery_points_test.cs:10-20`). Fed **every frame** from `Region.BestDepth` (`Game1.cs:3714 _mastery.SetEarned(SkillPointsEarned())`, `:5141-5147`; `RegionAutomation.cs:72-83`). First-time depth only, per region. Nothing else feeds it — not the Warren, not conquest (`Game1.cs:296-305`).
- **Tree cost:** 316. **Career:** six regions at depth 150 → 66 pts = **20.9%**; at depth 225 → 78 = **24.7%**. `MasteryTreeTests.cs:239-248` asserts 0.20–0.38 and "a career must buy a branch and a half"; its remark `:229-236` records the walk 256→286 (23%, the state file's number) →316 (21%) and explicitly declines to raise the curve — "a playtest question, not a number to move because a test went red". One branch = 49, two = 98 > 78 (`:211-219`).
- **Respec:** free, instant, unlimited (`MasteryTree.cs:230-234`, remarks `:145-149`); single-node refund refuses stranding (`:215-225`). UI: right-click refund (`BuildScreen.cs:544-560`), "TAKE EVERY POINT BACK" (`:614-619`). No cost anywhere.
- **Stale statements of the rule (all describe the retired `3 + depth/5` faucet):**
  - `Game1.cs:5133-5139` doc comment: "one per five waves… Three grants at the start".
  - `BuildScreen.cs:854-855`: "mastery points are 3 + deepestEver/5 + conquered*5".
  - `design/gdd/game-flow.md:215-218` ("One point per first-time depth milestone (every 5 waves)"), `:396` §4.3, `:489` tuning knob `DepthPerPoint = 5`.
  - `SaveGame.cs:164-165` `MasteryEarned` documented as "Total mastery points earned" but stores `_deepestEver` (`Game1.cs:641, 913`).
  - `SaveGame.cs:260-267` `WarrenMasteryPool` doc and `design/gdd/warren-facilities.md:32-33, 57, 68` say the Warren pool "is added into SetEarned" — false; `Game1.cs:296-305` says in bold that it does not and never did.

---

## 4. Every remaining Form reference in the mastery layer

| Where | What | Evidence |
|---|---|---|
| `MasteryNode.Form : Form?` | the specialisation's Form | `MasteryTree.cs:62-63` |
| `Spec(…, Form form, …)` and 6 specs keyed `Form.Strike/Trap/Projectile/Aura/Mark/Transformation` | | `MasteryCatalog.cs:434-448, 542-545` |
| Spec labels use **Form** names: STRIKE / TRAP / VOLLEY / AURA / MARK / **MORPH** SPECIALIST | the styles are HAMMER / SNARE / VOLLEY / FIELD / SIGN / DRAIN | `:434-447` |
| `MasteryTree.Affinity() : Form?` — "The Form this build has specialised in… ONE DISCIPLINE PER HUNTER" | | `MasteryTree.cs:243-257`, `CanTake :192-196` |
| `Build.Affinity : Form?` → `FormBehaviour.AffinityFactor(Form, Form)` → `SoloBattle.Amp` | discipline multiplier ×2.0/×0.75/×0.45 on every skill hit by hexagon distance of Forms | `Build.cs:434-441`, `FormBehaviour.cs:245-264`, `SoloBattle.cs:709-719, 787-788, 1584-1586, 1711-1713` |
| `SkillShape.FormTargets : Dictionary<Form,int>` | fed only by `spec_projectile` | `SkillShape.cs:186-187`; grep `FormTargets =` in src → 1 hit (`MasteryCatalog.cs:439`) |
| `SkillShape.FormPower : Dictionary<Form,float>` | character aptitude + weapon family | `SkillShape.cs:202-205`; feeders `Character.cs:208`, `GearShape.cs:29` |
| NARROW / BROAD labels say "FORM" while their fields are `AffinityStyleBonus` / `OffStylePenalty` | | `MasteryCatalog.cs:180-183`, `SkillShape.cs:71-76` |
| `MasteryKind.SkillRoad` doc "teaches one of the six skills that have no Form of their own"; `GrantsSkillId` doc "Six of the twelve skills have no LegacyForm… other six are free" | stale: all twelve are taught since `837a9e7` | `MasteryTree.cs:24-33, 80-93`; `SkillCatalogue.NeedsUnlock` returns `true` for all (`SkillCatalogue.cs:637-641`) |
| `MasteryKind.Specialisation` doc "A Form sub-branch… Grants that Form's combo trigger" | | `MasteryTree.cs:41-42` |
| `MasteryCatalog` class remarks: "six arms, one per Form", "six Form specialisations", "tree to 256", "Weight answers Armoured, Spread answers Swarm" | stale | `MasteryCatalog.cs:57-77`, section header `:149` "WEIGHT — answers Armoured", `:420-431` "FORM SPECIALISATIONS" |
| `MasteryLayout.AngleOf` doc "Weight up, Spread down" | stale | `MasteryLayout.cs:127-131` |
| `BuildGlossary` — `FormHeadline`, `FormRule`, `SkillSummary(Source, Form)` entirely Form-based | consumers: `WeaveScreen.cs:1746-1774` only | `BuildGlossary.cs:29-79, 166-167` |
| "discipline" = Form affinity across UI | `BuildScreen.cs` 19 hits, `WeaveScreen.cs` 8, `Onboarding.cs:275-278` ("one Form whose skills hit twice as hard"), `ChestScreen.cs:735` "DISCIPLINE: {Form}" | grep -ci discipline |
| "attune / attunement" — the first-specialisation ceremony | `BuildScreen.cs` 44 hits, `Game1.cs` 11 | grep -ci attune |
| `BuildScreen.Short(Form)`: Projectile→"VOLLEY", Transformation→"MORPH", others = Form name | inconsistent Form→Style mapping; three more copies as `FormShort` in `SoloExpeditionScreen.cs:2339`, `StatsScreen.cs:226`, `CharacterScreen.cs:553` ("X ADEPT" title) | `BuildScreen.cs:51-54` |
| `SaveGame.Affinity : string` "Legacy — the Mastery tree owns it now" | **no reader**: grep `save\.Affinity|Affinity = ` in `Game1.cs`/`Persistence/*.cs` → 0 hits | `SaveGame.cs:158-159` |
| `BuildComposer.SkillPick.Form`, `SlotKinds` fallback to `FormBehaviour.IsPassive/FiresOnBeingHit`, `SkillCatalogue.Resolve(Form, passive)` | migration bridge for pre-2026-08-30 saves | `BuildComposer.cs:35-36, 102-107, 171-173`; `PlayerLoadout.SetSkill` still writes a Form (`PlayerLoadout.cs:186-198`) |
| Test fixtures with six-Form-arm node ids `strike_1`, `strike_2`, `volley_m` | | `SaveSystemTests.cs:328, 345`; `MasteryTreeTests.cs:604` |
| Dev screenshot fixture takes retired ids `heavy_hand, sharpened, sunder, crush, breaker, monolith, opener, hasten, mark_mastery` | `Take()` returns false silently; only `alpha`, `interrupt` exist | `Game1.cs:1579-1582` |
| `MasteryTreeTests` allowlist `conditional = { "bastion", "cascade", … }` | `cascade` no longer exists | `MasteryTreeTests.cs:398` |

`FormPower` in `MasteryCatalog` — none (grep `FormPower` in MasteryCatalog.cs → 0). Character aptitude is a Form-keyed damage multiplier that stacks multiplicatively with a same-Form specialisation (`SkillShape.cs:199-201`).

---

## 5. Liveness

### 5.1 What the existing tests prove

- `mastery_node_liveness_test.cs` — **every** RESONANCE/TEMPO/ENDURE node except Start/Specialisation/SkillRoad is run with vs without on a real `SoloExpedition` in two contexts (one-Source and mixed) and must move damage, health kept or depth reached (`:38-55, 123-148`). Stats channel proven end to end (`:150-168`).
- `loot_node_liveness_test.cs` — every LOOT node must move gleam or chest quality across a clean and a bloody 40-wave run (`:104-125`); UNTOUCHED vs BLOODPRICE must disagree (`:127-147`).
- `mastery_new_nodes_liveness_test.cs` — BRISK, RHYTHM, OPENING VOLLEY, PAYBACK, REBOUND against `SoloBattle.ResolveWave` (`:89-185`).
- `affinity_test.cs` — every Form specialisation is worth taking for its Form and costs on the others (`:57-100`).
- `MasteryTreeTests.test_taken_nodes_compose_their_shapes` (`:497-513`), `test_only_form_combo_triggers_are_granted` (`:470-480`).
- Skill roads: `skill_slot_kinds_test.test_a_skill_the_tree_has_not_taught_cannot_be_chosen` (`:240-267`).
- `PassiveTreeTests.cs` (listed in the brief) is about the **MemoryDustTree** (trait tree), not this one (`:33-43`).
- All of the above ran green in this session (150 tests).

### 5.2 Spot checks by grep (five nodes + two specs)

| Node | Field | Producer → consumer chain | Verdict |
|---|---|---|---|
| CHIME | `Stats[ResonanceAffinity]+6` | `MasteryTree.Stats()` `:264-272` → `Game1.cs:2757` → `Hunter.ValueOf` `:198-199` → `SoloBattle.cs:486` | live |
| KEYED | `StrongMatchupBonus` | `SkillShape.Combine :488` → `SoloBattle.cs:730` | live |
| DEEP | `ResonanceWorth` | `Combine :494` → `SoloBattle.cs:487` | live |
| UNTOUCHED | `HaulPerCleanSecond/HaulCleanCap` | `Combine :478-479` → `SoloExpedition.cs:444-447` | live |
| BRISK | `AutoAttackRate` | 1 read in `SoloBattle` (swing interval); `test_brisk_speeds_the_swing_and_not_the_skills` | live |
| `spec_projectile` | `FormTargets[Projectile]` | `SkillShape.TargetsFor :436-444` → `SoloBattle.cs:1424, 1654, 1715, 1943` | live |
| `spec_mark` | `Linger` trigger | `Build.Triggers :583-590` → `SoloBattle.cs:1382, 1507` | live (Mark deals no damage, so `AffinityTest:57` guards the specialisation's net value) |

### 5.3 The dead half of `SkillShape` (dominant finding)

`SkillShape.cs:21-23` states the contract: "Every field here is READ by SoloBattle… If a field stops being read, its nodes must be deleted with it." The converse failure has happened: **18 fields are read by `SoloBattle` and written by nothing in `src/`.** Method: for each `public … X { get; init; }` in `SkillShape.cs`, `grep -rn --include=*.cs -E "\bX\s*=" src` excluding `bin/ obj/ SkillShape.cs`.

| Field | src feeders | SoloBattle reads | Only test writers |
|---|---|---|---|
| `ArmourIgnoreFraction` | 0 | 1 | — |
| `CrushArmourMultiple` | 0 | 2 | — |
| `OverwhelmFloor` | 0 | 2 | `SkillShapeBattleTests.cs` |
| `SunderThreshold` / `SunderAmount` | 0 / 0 | 1 / 1 | `SkillShapeBattleTests.cs` |
| `FreshThreshold` / `FreshBonus` | 0 / 0 | 1 / 2 | — |
| `StaggerThreshold` / `StaggerMs` | 0 / 0 | 1 / 2 | `mastery_new_nodes_liveness_test.cs:71-85` (hand-built shape) |
| `StrikesEveryCreature` | 0 | 0 (read in `TargetsFor` only) | `SkillShapeBattleTests.cs` |
| `ChainFraction` | 0 | 1 | `SkillShapeBattleTests.cs` |
| `RicochetChance` / `RicochetFraction` | 0 / 0 | 1 / 1 | — |
| `CascadeOnKill` | 0 | 1 | — |
| `NextSkillAfterKillBonus` | 0 | 2 | — |
| `RatePerCreature` | 0 | 1 | — |
| `VsArmouredBonus` / `VsOtherPenalty` | 0 / 0 | 2 / 2 | `AffixLivenessTests.cs` |

These are the WEIGHT (armour rules, stagger, headlong) and SPREAD (chain, ricochet, cascade, rally, tide, siege) remnants: the branches were replaced on 2026-08-30 (`110e3af`, `f4052b8`) and the fields stayed. Consequences: (a) `SoloBattle` carries ~20 conditional branches that can never be true in the shipped game; (b) `SkillShape.Combine` (`:496-507, 511-518, 525-526`) merges values nothing produces; (c) two test files keep those paths green with shapes the game cannot make, which is the "built-tested-green code that never runs" species by construction; (d) `MasteryTreeTests.test_every_mastery_is_a_trade :373-375` and `test_greaters_are_costed_or_conditional :405` still list `VsOtherPenalty` and `OverwhelmFloor` as recognised prices.

Near-dead: `FormTargets` (1 feeder, `spec_projectile`), `OverkillCarry` (1 feeder, a champion `CharacterRoster.cs:79`), `PerCreatureBonus` (1, `CharacterRoster.cs:93`), `FreeOpeningCast` (1, `:109`), `MarkPowerBonus` (1, `:201`), `BonusCritPercent`/`Leech`/`FirstBiteFree`/`RegenFraction`/`CooldownRefundOnKillMs` (1 each, element sets).

### 5.4 Other dead or vestigial members in the area

- `MasteryTree.Mods()` always returns `BuildMods.None` (`MasteryTree.cs:286-294`); `BuildComposer.cs:126` still combines it. Vestigial channel.
- `BuildComposer.SlotKinds` `taught` parameter: accepted, discarded (`BuildComposer.cs:64-76`).
- `SaveGame.Affinity` — no reader (see §4).
- `SaveGame.MasteryEarned` — reader exists but stores deepest depth, not points (`Game1.cs:641, 913`).
- `MasteryKind.SkillRoad` has no UI case (see §6).
- `ItemClassDef.Road : Branch?` (`ItemClasses.cs:58, 97-130`) — grep `\.Road\b` across src excluding the trait-tree's `TraitRoad` → 0 consumers. Cross-area; it also reuses the word "road" for a branch while the mastery tree uses "road" for a skill chain.
- `EfficiencyContract.MasteryLevel.OptimizedTeam` — declared unreachable (`RegionAutomation.cs:38-41, 97-100`).
- `BuildScreen.cs:1603`: node art `ui_node_mastery` and the bridge chain art "stay on disk, unused".

---

## 6. UI

The mastery tree is drawn by **`src/ResonanceHunter.Game/BuildScreen.cs`** (1906 lines), not `PrestigeScreen.cs` — that is the Dust/trait tree, which borrows the mastery tree's visual grammar (`PrestigeScreen.cs:337, 379, 429, 786, 1024`).

### 6.1 Skill discovery is **not** shown distinctly

- `grep -n "SkillRoad\|GrantsSkillId\|LEARN\|TEACH" BuildScreen.cs` → 0 hits. The screen has no code path for the twelve road nodes.
- `KindWord(MasteryKind)` (`BuildScreen.cs:1569-1577`) has cases Minor/Notable/Greater/Mastery/Bridge/Specialisation and `_ => "START"` — the node card's header for a road node prints **"START"**.
- `KindFrame` (`:1605-1614`) `_ => "ui_node_spec"` — a road node wears the **specialisation's frame**; nothing marks it as "teaches a skill".
- The cost ladder (`:1450-1453`) reads "MINOR 1 · NOTABLE 3 · GREATER 5 / SPECIALISATION 6 · CAPSTONE 8" — the road's 5 is missing.
- The caption "FOUR DIRECTIONS · SIX SPECIALISATIONS · ONE DISCIPLINE" (`:1134`) and the empty-card hint "RESONANCE OR LOOT. TEMPO OR ENDURE…" (`:1462`) never mention skills.
- The PASSIVES panel lists taken Notables and Masteries only (`:864`).
- What does work: the road node's `Label` is `"{Name} — {LINE}"` so hovering shows e.g. "PRESS — …"; `MasteryLayout` places roads on the specialisation's spoke (`MasteryLayout.cs:326-342`) and sizes them (`:240`).
- The **WeaveScreen** is the place discovery is explained, and its copy is correct and style-based: "A SKILL IS LEARNED ON THE MASTERY TREE, THEN WOVEN INTO A SLOT" (`WeaveScreen.cs:899`), "IS LEARNED ON HAMMER'S ROAD, ON THE MASTERY TREE" (`:694, 837, 1491`).

### 6.2 Terminology on screen

- Branch names come from the enum (`Short(Branch) => b.ToString().ToUpperInvariant()` `:49`) → RESONANCE / LOOT / TEMPO / ENDURE. Correct. Promises `:432-437`. Header `DrawBranchHeader :1244-1259`.
- **WEIGHT/SPREAD survive** in comments (`:351, 428, 1231, 1254-1255, 1717-1726, 1800`) and — player-facing — in the guided tour: `Onboarding.cs:271-273` *"WEIGHT is fewer, bigger hits. TEMPO is hitting first and often. SPREAD hits many at once, and ENDURE outlasts the enemy."* and `:275-278` *"one Form whose skills hit twice as hard."*
- Kind words: MINOR / NOTABLE / GREATER / BRANCH CAPSTONE / BRIDGE / "SPECIALISATION — YOUR DISCIPLINE" (`:1569-1577`, rationale `:1560-1567`).
- Specialisations show **Form** names inside the diamond (`Short(Form)`: STRIKE, TRAP, VOLLEY, AURA, MARK, MORPH — `:51-54, 1768`), never the style (HAMMER, SNARE, VOLLEY, FIELD, SIGN, DRAIN). The specialisation card explains the discipline multiplier in Form terms (`:1493-1511`).
- Hunter title "{FORM} ADEPT" / "SEEKER" on four screens (`BuildScreen.cs:715`, `CharacterScreen.cs:553`, `StatsScreen.cs:226`, `SoloExpeditionScreen.cs:2339-2340`).
- "Your mastery leans STRIKE." / "Your mastery is spread evenly across forms." (`:803-804`); CORE COMPOSITION lists Forms (`:758`).
- "six nodes among sixty-three" (`:1697`) — there are 87.
- The points readout is consistently "MASTERY POINTS" (`BuildScreen.cs:860`, `StatsScreen.cs:235`).

---

## 7. The name collision — three (four) "masteries"

| # | Meaning | Where it lives | Who reads it |
|---|---|---|---|
| 1 | **Build mastery tree** — respeccable node tree, points from first-time depth | `Builds/MasteryTree.cs`, `MasteryCatalog.cs`, `MasteryPoints.cs`, `MasteryLayout.cs`; `SaveGame.MasteryTaken/MasteryEarned/MasteryZoom/PanX/PanY` (`SaveGame.cs:161-179`); `Activity.Mastery` (`Unlocks.cs:136,181,205`); `TourTarget.MasteryTile/MasteryTree` (`Onboarding.cs:59,63`) | `BuildComposer`, `BuildScreen`, `WeaveScreen`, `StatsScreen` "MASTERY POINTS" |
| 1b | **`MasteryKind.Mastery`** — the ring-4 capstone node kind | `MasteryTree.cs:35-36` | UI renamed it "BRANCH CAPSTONE" (`BuildScreen.cs:1573`) precisely because of this overload (`:1560-1567`) |
| 2 | **Region mastery** — per-region points from kills, banded into `MasteryLevel` | `Progression/EfficiencyContract.cs:5-12` (`MasteryLevel` enum, in the *Progression* namespace), `Automation/RegionAutomation.cs:59-62, 101-109, 132-133` (`RegionMasteryPoints`, +5/kill × `DustEffects.MasteryRate` "FASTER REGION MASTERY" `recall_1..4`, `DustEffects.cs:69-76`; thresholds 500/2000 `:17-18`); `SaveGame.RegionMasteryPoints` (legacy single-region `:50-51`), `RegionFarm.MasteryPoints` (`:346`), `HighestMasteryAwarded` (`:75-76`) | `Region.IdleEfficiencyPercent` (map away-earnings band), `Game1.cs:2537` `_regionProgression`, and — importantly — **it pays TRAIT points**: `Game1.TraitPointsEarned` adds one per region mastery level, and `Game1.cs:2745-2750` awards Dust (15 × levels). Player copy: `Onboarding.cs:357-358` "one each time a region's mastery rises a level", `PrestigeScreen.cs:468` "RAISE A REGION'S MASTERY" |
| 3 | **Warren "Mastery" = INSIGHT** | `WarrenResource.Mastery` (`Warren.cs:13`), facilities BREEDING CHAMBER / RITUAL NEST "Makes Insight" (`:52-53`), `Warren.ResourceBonus(Mastery)` (`:210`); `Game1._warrenMasteryPool` (`:306`), `SaveGame.WarrenMasteryPool` (`:267`) | `WarrenScreen` renames it "INSIGHT" on screen (`WarrenScreen.cs:104, 117, 263`); spent only on Warren upgrades (`Game1.cs:1119-1123`) — a closed loop produced and consumed on one screen, which `Game1.cs:296-305` flags for the designer |

So: meaning 1 is paid by depth and spent on the build; meaning 2 is paid by kills and pays **trait** points and Dust; meaning 3 is paid by facilities and pays facilities. The word "mastery" in trait-point copy (`Onboarding.cs:358`, `PrestigeScreen.cs:468`) is the one a player will misread as meaning 1.

---

## 8. Serialization risks

| Field | Risk | Migration |
|---|---|---|
| `SaveGame.MasteryTaken` (node ids) | Renaming any node id (e.g. `spec_strike`→`spec_hammer`, `spec_transformation`→`spec_drain`, Form-named specs → style names) silently drops the node on `RestoreTaken` (`MasteryTree.cs:172-179`); points are refunded implicitly (Earned is derived) but the discipline and every taught skill vanish on load | Alias map inside `RestoreTaken` or in `SaveSystem` load; keep old ids accepted for one version |
| `ShareCodes.SharedBuild.Mastery` (`ShareCodes.cs:50, 222-229`) | same node ids leak into share codes; rename breaks shared builds | same alias map applied on decode |
| **No persisted "learned skills"** | Moving to "permanently learned" needs a new list (e.g. `SaveGame.LearnedSkills`); an old save has none | derive on first load: every taken `SkillRoad` node's `GrantsSkillId`; write it back on next save |
| `SaveGame.MasteryEarned` | named "points earned", holds deepest depth (`Game1.cs:641, 913`) | rename to `DeepestEver` with a read-old-name fallback, or leave and fix the doc |
| `SaveGame.Affinity` (string) | unread legacy; harmless but misleading | delete after confirming no external tool reads it |
| `SaveGame.WarrenMasteryPool` | doc claims it feeds SetEarned; it does not | doc fix; if INSIGHT is renamed in the enum, keep the JSON name or map |
| `SavedSkill` Form → `SkillId` | `SkillPick.SkillId == null` resolves via `SkillCatalogue.Resolve(Form, passive)` (`BuildComposer.cs:171-173`); deleting `LegacyForm`/`Resolve` before every save is rewritten unweaves old builds | keep `Resolve` in an isolated migration step that stamps `SkillId` once |
| `SaveGame.RegionMasteryPoints` (legacy single-region) | already superseded by `RegionFarms` (`:50-51`) | B-migration-only |

---

## 9. Duplicated responsibilities

- **Execute**: `spec_strike`'s `Execute` trigger (`SoloBattle.cs:1613-1616`, keyed `form == Form.Strike`), `assassinate` (`:1621-1628`), Cull (`:830`, fed by `spec_strike` and the Shadow set `ElementSets.cs:97`), and HAMMER/FINISH `ExecuteFraction` (`:1664-1665`). Four "weakened foe" rules in one loop.
- **Cooldown reduction**: BLITZ/RUSH/VOLLEY-bridge `SkillRate` vs VOLLEY's `CooldownMultiplier` vs Spirit set `SkillRate 1.06` vs `Hunter.SquadSkillRate` (TEMPO stat, worn Focus, `HunterProgression.cs:434`).
- **Mark window**: `spec_mark` 1.3 × Mind set 1.5 (`ElementSets.cs:89`) × THE OATHBOUND 1.5 (`CharacterRoster.cs:201`) — all multiply (`SkillShape.Combine :538`) — vs SIGN's `AmplifyMs` dial.
- **Reflect**: `thorns` `SkillShape.ReflectFraction` vs SNARE/JAWS `SkillDef.ReflectFraction` — same name, two records, two read sites.
- **Lifesteal**: Nature set `Leech`, `spec_transformation` Siphon, `feedback` bridge `HealPerTargetStruck`, DRAIN `Lifesteal`/`SwingLifesteal`, Nature signature (`BuildGlossary.cs:109`).
- **Vow power**: PLEDGE 1.30 × ZEALOT 1.60 × Oathbound 1.5 × Dust `artifice_vows` 1.25 (`DustEffects.cs:232-244`) — four feeders, multiplicative (`Combine :476`), up to ×3.9 on the vow bonus.
- **Form → display name**: `BuildScreen.Short(Form)` plus three `FormShort` copies (§6.2).
- **Point rule**: `MasteryPoints.cs` (sqrt) vs `game-flow.md` §3.5/§4.3 (linear) vs two code comments (linear).
- **Design docs**: `skill-and-trait-trees.md` (Status: Draft; WEIGHT/SPREAD; 256 total) is still indexed as the Core "Skill Tree and Trait Tree" doc (`systems-index.md:30`) and depended on by `vows.md:6`, `regions-and-rosters.md:7`; `skill-slots-and-skill-trees.md` §9/§9b is the live spec. The older doc is not marked superseded.
- **Tree drawing**: `PrestigeScreen` re-implements the mastery tree's camera, halo, frame-by-kind and edge rules by hand ("the mastery tree's, verbatim in spirit", `PrestigeScreen.cs:429`).

---

## 10. Numerical stacking (multiplicative soup candidates)

All within `SkillShape.Combine` (`SkillShape.cs:456-565`) unless noted.

| Path | Kind | Note |
|---|---|---|
| `DamageDealt` (RESONANT 1.25 × DISCORD 0.90 × CHORD 0.85 × PROSPECTOR 0.85 × BULWARK 0.85 × REBOUND 0.85 × VEIN 0.92) | multiplicative | one branch can legitimately hold RESONANT+DISCORD+CHORD (any greater opens the capstone) |
| `SkillRate` (BLITZ 1.43 × RUSH 1.25 × VOLLEY bridge 1.25 × Spirit set 1.06) × `Hunter.SquadSkillRate` × RHYTHM ramp | multiplicative | three mastery sources of the verb §9 says the tree must not sell |
| Affinity ×2.0 × NARROW (1+0.20) × KEYED matchup × PURE (1+0.35) × `FormPower` aptitude 1.25–1.35 × weapon family × set `SourceBonus` | multiplicative chain in `Amp` (`SoloBattle.cs:704-742`) | a same-Form specialisation + aptitude + family "compound instead of racing" by design (`SkillShape.cs:199-201`) |
| `VowPowerMultiplier` ×4 sources | multiplicative | §9 |
| `MarkWindowMultiplier` ×3 sources | multiplicative | §9 |
| Haul: `Hunter.HaulMultiplier` (Guile) × UNTOUCHED (≤1.45) × SPOTLESS+VEIN (additive to 0.90 then ×1.90) × BLOODPRICE+GAMBLE (additive 1.05 → ×2.05) × CACHE ×1.60 × depth rule | each `haul *=` in `SoloExpedition.cs:444-474` | |
| Depth haul rule: `HaulPerWavePastDepth` **sums** (PROSPECT 0.01 + LODE 0.01 + PROSPECTOR 0.03 = 0.05) while `HaulDepthFloor` takes the **lower** floor (`Combine :482-483`, `Pick lower`) | additive rate × lowest floor | With the full side road the real rule is **+5% per wave past depth 20**, not the PROSPECTOR label's "+3% past depth 30"; at wave 120 that is ×6.0 on haul. Label/behaviour mismatch worth a look |
| `MaxHealth` ENDLESS 0.5 × Body set 1.10 × `Hunter.SquadHealthMultiplier` | multiplicative | fine, but note ENDLESS halves everything the others multiply |

---

## 11. Hardcoded branches on content ids / enums

| Where | Keys on | Note |
|---|---|---|
| `SoloBattle.cs:1613` `form == Form.Strike && Execute`; `:1551` `form == Form.Projectile && Overdraw` | `Form` enum | trigger effects keyed on the legacy Form inside the loop; a road skill inherits its style's Form via `PlayerLoadout.SetSkill` (`:194-197`), so they do fire, but the loop still speaks Form |
| `BuildScreen.KindWord/KindFrame` (`:1569-1577, 1605-1614`) | `MasteryKind` | missing `SkillRoad` case → "START" / spec frame |
| `BuildScreen.Short(Form)` (`:51-54`) | `Form` | partial Form→Style rename map |
| `MasteryTreeTests.cs:398` `conditional = {"bastion","cascade","assassinate","pure","absorb"}` | node ids | `cascade` is gone |
| `Game1.cs:1579-1582` dev fixture node ids | node ids | nine of eleven no longer exist |
| `DustEffects.MasteryRate` `recall_1..4` (`:69-76`) | trait node ids | region-mastery meaning |
| `Onboarding.cs:271-278` tour text | prose | WEIGHT/SPREAD/Form |

---

## 12. Authoritative vs obsolete

### Authoritative (keep)
- `Builds/MasteryTree.cs` — engine: `Take/Refund/Respec/CanTake/RestoreTaken`, `Stats()`, `Shape()`, `LearnedSkills()`, `Triggers()`.
- `Builds/MasteryPoints.cs` — the only point rule; pinned by `mastery_points_test.cs`.
- `Builds/MasteryLayout.cs` — pure, tested layout (`MasteryLayoutTests.cs`, 11 tests including deeper-tree and overlap gates).
- `Builds/MasteryCatalog.cs` — the RESONANCE/LOOT branches and the TEMPO/ENDURE stat minors; the 12 `Teaches` rows; `RingCost`/`BranchCost`/`TotalCost`.
- `MasteryNode.Stats` → `Hunter.SetMasteryStats/MasteryBonus/ValueOf` (`HunterProgression.cs:180-199`) — the one funnel.
- `BuildComposer.Compose` `:111-196` — the single seam tree→build.
- Tests: `MasteryTreeTests`, `mastery_node_liveness_test`, `loot_node_liveness_test`, `mastery_points_test`, `one_discipline_test`, `MasteryLayoutTests`, `taught_tree`, `skill_slot_kinds_test:240-267`.
- Design: `skill-slots-and-skill-trees.md` §5, §9, §9b, §11 (with the §7-vs-§9b contradiction noted).

### Obsolete / to retire
| Name | Files | Class | Why | Replacement |
|---|---|---|---|---|
| 18 unfed `SkillShape` fields + their `SoloBattle` branches + `Combine` lines | `SkillShape.cs:123-179, 207-246, 263-265`; `SoloBattle` read sites | A-delete | WEIGHT/SPREAD remnants; nothing in the game writes them | delete field, reader and Combine line together; port `SkillShapeBattleTests`/`mastery_new_nodes_liveness_test.test_stagger…` cases to the skill dial that now owns the verb or delete |
| Form-keyed Specialisations + `MasteryNode.Form` + `MasteryTree.Affinity() : Form?` + `Build.Affinity : Form?` + `FormBehaviour.AffinityFactor` | `MasteryCatalog.cs:432-448, 542-545`; `MasteryTree.cs:63, 253-257`; `Build.cs:441`; `FormBehaviour.cs:245-264`; `SoloBattle.cs:709-719, 787, 1584, 1711` | C-replace | discipline is a Form concept; the current model is Style; the six Form-combo triggers duplicate style verbs (§1.4) | a `Style`-keyed "road head" node whose only job is access (§9 `:596-604` "Specialisation … Gives: Access"); if a lean multiplier survives it should key on `Style` via `SkillDef.Style` |
| `BuildTrigger.Execute/Coiled/Overdraw/Radiance/Linger/Siphon` as mastery grants | `MasteryCatalog.cs:434-448`; `SoloBattle.cs:639,1261,1382,1438,1507,1551,1613,1730,1909` | C-replace | Form-based enchantments (brief: legacy) reimplementing HAMMER/SNARE/VOLLEY/FIELD/SIGN/DRAIN verbs | the owning `SkillDef` dials |
| TEMPO nodes ASSASSINATE, BLITZ, RUSH; bridges VOLLEY, EXECUTIONER, FEEDBACK; ENDURE THORNS/REBOUND | `MasteryCatalog.cs:318-328, 381-387, 404-418` | open decision (§7 says leave; §9b kept them) | skill verbs on the champion tree | HAMMER execute/defence-break, VOLLEY cooldown/targets, SNARE reflect, DRAIN lifesteal |
| `MasteryTree.Mods()` | `MasteryTree.cs:286-294`; `BuildComposer.cs:126` | A-delete | always `None` | remove parameter from the combine |
| `BuildComposer.SlotKinds(..., taught)` | `BuildComposer.cs:64-76` | A-delete | discarded | — |
| `BuildGlossary` Form methods | `BuildGlossary.cs:29-79, 166-167` | C-replace | Form prose; only `WeaveScreen.cs:1746-1774` reads it | `SkillDef.Line` / style text |
| `SaveGame.Affinity` | `SaveGame.cs:158-159` | A-delete (after one release) | no reader | — |
| `SaveGame.MasteryEarned` naming, `WarrenMasteryPool` doc | `SaveGame.cs:164-165, 260-267` | B-migration-only | misdocumented | doc fix / rename with fallback |
| `design/gdd/skill-and-trait-trees.md` | whole file | B (mark Superseded) | WEIGHT/SPREAD, 256, linear points | `skill-slots-and-skill-trees.md` §9/§9b |
| `game-flow.md` §3.5 & §4.3 point rule, `Game1.cs:5133-5139`, `BuildScreen.cs:854-855` comments | | doc fix | linear faucet | `MasteryPoints.cs` |
| `Onboarding.cs:271-278` tour copy | | copy fix | WEIGHT/SPREAD/Form | RESONANCE/LOOT/TEMPO/ENDURE, styles |
| `Game1.cs:1579-1582` fixture ids | | A-delete/update | retired ids | current ids |
| `MasteryCatalog.cs` class remarks `:57-102`, `:149`, `:420-431`, `:450-457`; `MasteryTree.cs:24-33, 41-42, 80-93`; `MasteryLayout.cs:127-131` | | doc fix | pre-re-axe prose | |

---

## 13. Abstraction / over-engineering notes

- **Good abstraction to preserve**: `Stats` on a node → `Hunter.ValueOf` funnel; the derived-not-persisted `Earned`; `SkillShape` as the single shape record the sim reads; `Prereqs`/`SecondPrereqs` any-of groups; `MasteryLayout` as pure geometry; liveness tests that compare a fight with vs without a node.
- **Abstraction opportunity**: `MasteryKind.Specialisation` and `MasteryKind.SkillRoad` are both "access" nodes; once the discipline multiplier and trigger grants move to the skills, a specialisation is just the road's first node and could be `SkillRoad` with an extra prerequisite group — collapsing two kinds and the `Form` property.
- **Over-engineering risk**: `SkillShape` has ~90 fields; 18 are dead and ~10 have one feeder. It is on the edge of becoming the "giant effect engine" the design laws forbid. Every new node has been adding a field; the 2026-08-30 cut removed nodes but not fields.
- **Over-engineering risk**: `SkillShape.Combine` hand-merges every field with per-field policy (`Pick lower`, `Math.Max`, sum, product); a field's policy is decided in a 100-line method far from its declaration, which is how the depth-floor/rate mismatch in §10 arises.

---

## 14. Open questions for the designer/owner

1. §7 vs §9b: do ASSASSINATE, BLITZ, RUSH, ALPHA, OPENING VOLLEY, RHYTHM, THORNS, REBOUND and the three bridges stay on the champion tree, or move to HAMMER/VOLLEY/SIGN/SNARE/DRAIN as §7's table says? The code follows §9b; the brief's law ("mastery must NOT reimplement skill mechanics") follows §7.
2. Should the Form discipline multiplier (×2.0 / ×0.45) survive at all in a Style world? If yes, keyed on `SkillDef.Style` with `SkillCatalogue.RingDistance` (already exists, `SkillCatalogue.cs:661-667`) rather than `Form`.
3. Respec semantics: re-lock (current, tested) vs permanently learned (brief). If the latter, where does the learned set persist and does the road node still cost 5 after the skill is learned?
4. With one discipline per hunter and roads only off specialisations, a champion knows at most 3 distinct skills for 4 slots. Intended? If not: allow a second specialisation (then the discipline concept needs a rule) or detach roads from specialisations.
5. `Build.Weave` allows the same skill twice (§11 says refuse). Is a duplicate skill meaningful in the fight, or a bug waiting?
6. Point income: 316-point tree vs 66–78-point career (21–25%). The test remark defers raising the curve to a playtest. Decide before adding more nodes.
7. Depth-haul stacking: PROSPECT+LODE+PROSPECTOR yields +5%/wave past depth 20 (label says +3% past 30). Intended trade or Combine artefact?
8. The two design docs: mark `skill-and-trait-trees.md` superseded and repoint `systems-index.md:30`, `vows.md:6`, `regions-and-rosters.md:7`?
9. Naming: region "mastery" pays trait points and Dust; Warren "Mastery" is INSIGHT. Rename `MasteryLevel`→`RegionMasteryLevel`/`FarmTier` and `WarrenResource.Mastery`→`Insight` (JSON name kept) to leave "mastery" to the tree?
10. Specialisation labels use Form names (STRIKE/TRAP/AURA/MARK/MORPH) while the roads they head teach HAMMER/SNARE/FIELD/SIGN/DRAIN skills — rename even if the nodes stay.
