# COMBAT & CONTENT V2 — plan

Brief: `BRIEF.md` beside this file (the owner's text, verbatim). This is the working plan and the
running defects register.

---

## 1. What §0's inspection found

Read on 2026-09-01 at `4269437`, before any change.

| Thing | Where | State |
|---|---|---|
| Styles / skills / variations | `Core/Builds/SkillCatalogue.cs` (655 lines) | 6 styles, 12 skills, 24 variations, 3 reinforcements each — the structure §1 says to keep is **already correct**. |
| Skill parameters | `SkillDef` record | **~40 dials on one record.** Variations and reinforcements are `Func<SkillDef,SkillDef>` deltas applied in order. |
| Shared modifiers | `Core/Builds/SkillShape.cs` (~70 init-only fields) | The bag gear, sets, keystones, mastery and vows all combine into. `SkillShape.Combine`. |
| Simulation | `Core/Builds/SoloBattle.cs` (2081 lines) | One deterministic `ResolveWave` loop stepping `TickMs`. Locals hold per-wave state. |
| Shield today | a `bankedShield` **local float** in `ResolveWave` | Not state, not exposed, not capped, not reset-documented. Exactly what §7 forbids. |
| Bite path | `SoloBattle.cs:1719–1903` | Order today: mitigation → REPAY banks `taken` → `bankedShield` absorbs → Health. |
| IRON | `SoloBattle.cs:1863–1870` | Deducts Health first and **refunds** it after. Cannot compose with a second preventer. |
| Events | `BattleEvent(Kind, Slot, Amount, AtMs, FromSkill)` — `Expeditions/WaveModel.cs` | Flat struct; `BattleEventKind` already has a `Shield` member (payload = a banked health figure). |
| Battle state for presentation | `Champion` (`MaxHealth`, `Health`, `ElapsedMs`, `UndyingSpent`, `BeatCount`, `ReadyAt`) | The snapshot §19 needs a Shield field on. |
| Wave telemetry | `WaveMetrics` (`RawDamage`, `DeliveredDamage`, `Hits`, `HealthLost`, …) | Has no shield/prevention counters. |
| Sets | `Core/Economy/ElementSets.cs` (138 lines) | 2p/4p are `SourceBonus` matching (+8% each) — the model §49 deletes. 3p a stat, 5p a rule. All expressed as `SkillShape`. |
| Skill progression | `Core/Builds/SkillProgress.cs` | `_uses` / `_variation` / `_taken`. Level = ⌊√uses/2⌋, max 4. |
| Save | `SavedSkillProgress { SkillId, Uses, Variation, Reinforcements }` | Flat, by name. Migration hooks land here. |

### The three findings that shape the work

1. **The structure is right; the content is not.** §1's list is already true in the repository. This
   pass changes what the twelve skills SAY, not how a skill is modelled.
2. **`bankedShield` is a local, so Shield cannot be state.** Every §8–§20 requirement — cap, reset,
   snapshot exposure, ordering against prevention, exclusion from REPAY — needs Shield lifted onto
   `Champion` first. **C1 is therefore the foundation and goes first.**
3. **The bite path resolves in the wrong order for §12 and §14.** REPAY banks pre-shield damage
   (§14 forbids), and IRON refunds after deducting rather than preventing before (§12 forbids, and it
   is why MACHINE 5p could not compose). Both are in the same forty lines.

### The architecture decision this pass makes, and why

§79 says not to reintroduce a giant `SkillDef`; the repository still has one. §1 says this is not an
architecture rewrite, and §80 blesses small typed states.

**Decision.** Keep the dial-delta model for skill PARAMETERS — it is deterministic, it composes by
construction, and replacing it is the rewrite §1 forbids. Add new mechanics as **typed combat state
with a named owner, reset rule and consumer** (§80), not as more booleans on `SkillDef`. Where a skill
must declare it participates in a mechanic, prefer one typed field over several flags. No scripting
runtime, no `Dictionary<string,object>` (§80).

---

## 2. Checkpoints

Each is: build · unit tests · gates (`tools/check_all.sh`) · boot (`tools/check_boot.sh`) · commit ·
push. Nothing is called done on a compile.

### C1 — SHIELD, end to end (§7–§20, §86) — **DONE**
`Champion.CurrentShield` / `MaxShield` · 50% cap · wave-start reset · the bite path re-ordered so
prevention precedes absorption and absorption precedes Health · REPAY reads Health damage only ·
`ShieldGained` / `ShieldAbsorbed` / `ShieldBroken` events · `WaveMetrics` gains attempted / prevented /
absorbed / health-damage · IRON rewritten as prevention rather than refund. 20 tests from §86.

### C2 — The catalogue (§27–§48, §65, §88)
The Source matrix as an invariant test FIRST, then the 24 variations and their reinforcements. New
mechanics needed by the content: skill overkill carry, execute overkill, SPEND charges, INTERCEPT
targeting, SPLAY/CLUSTER retargeting, FLOOD, REMNANT, RIPE, ANCHOR, BALANCE, TRAIL. Liveness test per
reinforcement (§65) — 72 of them.

### C3 — The six sets (§49–§62, §87)
Delete matching-Source bonuses. Six ladders with typed state: `MindFocus`, `ShadeCount`,
`BodyImpactPending`, Spirit activation mask + Harmony charges, Shield (from C1). Per-tier tests and the
5+3 / 4+4 / 3+3+2 comparison.

### C4 — Persistence (§73–§78, §89)
THIRST → SIPHON · renamed-reinforcement mapping · unmapped purchases refunded as unspent levels ·
Source reassignment must not force a respec · dead-field sweep. Old-save fixtures.

### C5 — Presentation (§21–§26, §69–§72, §90)
Hunt HUD shield bar · shield VFX (persistent rim, gain, absorb, break) · Expedition Log
`SHIELD ABSORBED` · skill-tree Sources · set-ladder UI · one canonical SHIELD word and glyph.

### C6 — Balance (§63, §64, §66, §67, §68, §83, §84)
Mono-Source builds ×6 · mixed builds ×8 · variation balance · beat budget audit · Shield balance ·
set-shape comparison, all against the repository's real bands.

### C7 — Documentation and the §92 report
One current truth in `design/gdd/`; stale claims deleted; `REPORT.md` written.

---

## 3. Defects register

*Opened as they are found; each closes with the checkpoint that fixes it or is carried with an owner.*

- ~~REPAY banks pre-shield damage~~ **CLOSED C1** — the bank now takes `healthDamage`, computed after absorption.
- ~~IRON refunds instead of preventing~~ **CLOSED C1** — prevention is decided before the pool or the
  shield is touched, so a second preventer can see the bite is already gone and keep its charge.
- ~~`bankedShield` is uncapped and undocumented~~ **CLOSED C1** — `Champion.CurrentShield` / `MaxShield`,
  one cap, one wave reset, one grant door.
- ~~`BattleEventKind.Shield`'s payload is a banked health figure~~ **CLOSED C1** — replaced by
  `ShieldGained` / `ShieldAbsorbed` / `ShieldBroken`; `WaveReplay` tracks `CurrentShield` / `MaxShield`.
- **Sets are matching-Source damage** — `ElementSets.cs`. §49. → C3.
- **`SkillDef.ShieldInsteadOfDamage`** becomes a typed grant in C1 and its old dial is swept in C4. §78.
