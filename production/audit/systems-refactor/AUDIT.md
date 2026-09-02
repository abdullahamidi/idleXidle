# PHASE 1 AUDIT — the systems refactor's starting map

Produced by eight read-only agents (`audit-workflow.js`, run `wf_b158f453-2af`), each answering
five questions about one subsystem with file:line evidence. This is the record the design and the
migration are built on; where it disagrees with a comment or a GDD, the brief says believe the code.

## CHARACTERS, ROSTER, INNATES, STARTING SKILLS

### 1. The FULL roster: every character id, display name, class, innate (its exact rule and numbers), and its current "starting skill" if any.

**10 characters**, all in one list literal: `CharacterRoster.All` (`src/IdleXIdle.Core/Characters/CharacterRoster.cs:46-232`). 5 classes × 2 tiers. Every one has a `StartingSkillId`; there is no null.

| # | Id | Display Name | Class / Tier | Innate name | Innate — the EXACT fields the sim reads (file:line) | Card text (`PassiveText`) | Starting skill |
|---|---|---|---|---|---|---|---|
| 1 | `seeker` | THE SEEKER | Wanderer / First | EVEN HAND | `Shape = { HitSize = 1.08f }` (`CharacterRoster.cs:61`). Read at `SoloBattle.cs:998` — `m *= shape.HitSize * shape.DamageDealt` on every skill hit. `Lean = null` (`:71`… here `:59`), `Mods = None`, `Grants = []`. | "Any road fits you. Every skill hits 8% harder." (`:57`) | `hammer_blow` (`:52`) |
| 2 | `anvil` | THE ANVIL | Warden / First | DEADWEIGHT | `Shape = { OverkillCarry = 0.33f }` (`:78`). Read at `SoloBattle.cs:1271` `var carry = MathF.Max(shape.OverkillCarry, castCarry);` → `:1274-1276` spills `spill * carry` onto `FirstAlive()`, **one level only** (`fromSkill:false`), `ignoresArmour:true`, and only when `fromSkill \|\| impactSwing`. `Lean = Branch.Resonance` (`:72`). | "A third of the damage left over from a kill hits the next enemy." (`:74`) | `hammer_press` (`:69`) |
| 3 | `chorus` | THE CHORUS | Ranger / First | MANY MOUTHS | `Shape = { PerCreatureBonus = 0.05f }` (`:92`). Read at `SoloBattle.cs:1029` — `m *= 1f + shape.PerCreatureBonus * alive` (alive = living creatures in the wave). `Lean = Branch.Loot` (`:87`). | "+5% damage for each enemy alive in the wave." (`:89`) | `field_mire` (`:84`) |
| 4 | `metronome` | THE METRONOME | Mystic / First | FIRST BEAT | `Shape = { FreeOpeningCast = true, FirstHitMultiplier = 2f }` (`:108`). `FreeOpeningCast` read at `SoloBattle.cs:1683` — `var openingBeat = shape.FreeOpeningCast ? 0 : beats - 1;`. `FirstHitMultiplier` read at `SoloBattle.cs:1018` — applies to the first hit on **each** enemy (`struckOnce` set), not the first cast. `Lean = Branch.Tempo` (`:101`). | "The wave's first cast has no wait. Your first hit on each enemy is doubled." (`:107`) | `volley_spray` (`:98`) |
| 5 | `unbroken` | THE UNBROKEN | Bulwark / First | SECOND WIND | `Grants = [ BuildTrigger.Undying ]` (`:120`). **No Shape, no Mods.** Read at `SoloBattle.cs:2399-2410`: `if (!champ.UndyingSpent && triggers.Contains(BuildTrigger.Undying)) { champ.UndyingSpent = true; champ.Health = 1; events.Add(new BattleEvent(BattleEventKind.Undying, 0, UndyingShieldMs, ms)); }` — once per `Champion` object (`SoloBattle.cs:59`), which the host holds for the expedition. `Lean = Branch.Endure` (`:117`). | "Once per expedition, a hit that would kill you leaves you at 1 health." (`:119`) | `drain_wilt` (`:114`) |
| 6 | `tower` | THE FALLING TOWER | Warden / Second | MOMENTUM | `Shape = { FirstHitMultiplier = 0.85f, LaterHitMultiplier = 1.35f }` (`:137`). Both read at the one line `SoloBattle.cs:1018` — `m *= struckOnce.Contains(against) ? shape.LaterHitMultiplier : shape.FirstHitMultiplier`. `Lean = Branch.Resonance` (`:131`). | "Your first hit on each enemy is weaker. Every hit after it is stronger." (`:133`) | `hammer_blow` (`:128`) — **the same id as THE SEEKER's** |
| 7 | `quiver` | THE QUIVER | Mystic / Second | LOOSE AGAIN | `Grants = [ BuildTrigger.LooseAgain ]` (`:156`) **and** `Shape = { SkillRate = 1.10f }` (`:157`). `LooseAgain` read at `SoloBattle.cs:1251-1256` — on **any** kill, `for (var k = 0; k < skills.Count; k++) { champ.ReadyAt[k] = 0; champ.ReadyAtBeat[k] = 0; }` (whole cooldown table zeroed, not one entry). `SkillRate` read at `SoloBattle.cs:836` (`BeatFor(mods.SkillRate * shape.SkillRate, …)`) and `:856`. `Lean = Branch.Tempo` (`:148`). | "A kill sends the next shot immediately, and your skills come back 10% sooner." (`:152`) | `volley_weep` (`:145`) |
| 8 | `thornwall` | THE THORNWALL | Bulwark / Second | REPRISAL | `Shape = { FlatDamageReduction = 6f, DamageTaken = 0.90f, StylePower = { [Style.Snare] = 1.35f } }` (`:176-180`). Reads: `SoloBattle.cs:2201` `taken *= shape.DamageTaken`; `:2224` `taken = MathF.Max(0f, taken - shape.FlatDamageReduction)`; `:1003` `m *= shape.StylePowerFor(skillDef.Style)`. `Lean = Branch.Endure` (`:170`). | "Every hit you take does less. Every trap you set does more." (`:172`) | `snare_jaws` (`:167`) |
| 9 | `oathbound` | THE OATHBOUND | Ranger / Second | TWICE SWORN | `Shape = { VowPowerMultiplier = 1.5f, AmplifyWindowMultiplier = 1.5f, AmplifyPowerBonus = 0.25f }` (`:207-210`). Reads: `SoloBattle.cs:2559` `return 1f + bonus * MathF.Max(0f, shape.VowPowerMultiplier)` — scales the Vow's **bonus over 1**, not the total (`SkillShape.cs:38-55`); `:1613` and `:1730` stretch the amplify window; `:985` `depth = (…) + shape.AmplifyPowerBonus`. `Lean = null` (`:197`). | "Vows pay far more. Your Marks last longer and hit harder." (`:199`) | `sign_call` (`:194`) |
| 10 | `magpie` | THE MAGPIE | Wanderer / Second | FULL POCKETS | The **only** innate carried by `Mods`, not `Shape`: `Mods = new BuildMods(1f, 1f, 1f, 1.35f, 1.20f)` (`:225`) → positional order is `(Damage, Health, SkillRate, Haul, Rarity)` (`Build.cs:29-34`), so **Haul ×1.35, Rarity ×1.20**, everything else neutral. Haul read at `SoloExpedition.cs:438` (`var haul = MathF.Max(0.05f, mods.Haul)`, the gleam volume multiplier) and — only under the HOARDER keystone — at `SoloBattle.cs:1009-1010`. Rarity read at `Game1.cs:4271` `_forge.RarityBonus = wornBuild.Resolve(_hunter).Rarity;` → `ForgeScreen.cs:1741` `Chests.Open(…, rarityBonus: RarityBonus, …)`. `Lean = null` (`:220`). | "All the loot you bring home is worth more. Rare finds show up more often." (`:222`) | `snare_repay` (`:217`) |

**Unlock gates (same table, second half)** — `CharacterUnlock` (`Character.cs:44-63`), evaluated in `CharacterState.Refresh` (`CharacterState.cs:70-76`):

| Id | UnlockKind | Gate | Quest threshold |
|---|---|---|---|
| `seeker` | Start | yours from the first session (`CharacterRoster.cs:62`) | — |
| `anvil` | Conquest | `cinderworks` = CINDERWORKS (`:79`) | conquest bar is `Checkpoints.ConquestWave = 20` waves (`Checkpoints.cs:29`) |
| `chorus` | Conquest | `umbral_reach` = UMBRAL REACH (`:93`) | 20 |
| `metronome` | Conquest | `marrow_wastes` = MARROW WASTES (`:109`) | 20 |
| `unbroken` | Conquest | `still_archive` = THE STILL ARCHIVE (`:121`) | 20 |
| `tower` | Quest | `q_cinder_deep` "THE LONG FURNACE" (`:140`) | `DepthInRegion cinderworks`, Threshold 50 (`QuestCatalogue.cs:169-171`) |
| `quiver` | Quest | `q_quiver_volleys` "THE COUNTED ARROWS" (`:162`) | `WavesWithStyle Style.Volley`, Threshold 150 (`QuestCatalogue.cs:202-204`) |
| `thornwall` | Quest | `q_marrow_hold` "THE LONG STAND" (`:187`) | `DepthInRegion marrow_wastes`, Threshold `ConquestWave * 4` = **80** (`QuestCatalogue.cs:192-195`) |
| `oathbound` | Quest | `q_three_vows` "THE THIRD OATH" (`:212`) | `RunsWithVowKept`, Threshold 3 (`QuestCatalogue.cs:175-177`) |
| `magpie` | Quest | `q_magpie_bosses` "THE FULL HOLD" (`:230`) | `BossesFelled`, Threshold 40 (`QuestCatalogue.cs:183-185`) |

**Arithmetic asked for:**
- Roster size = 10. `Grid` = 2 tiers × 5 `ClassColumns` = 10 cells, built by `All.Single(c => c.Class == cls && c.Tier == tier)` (`CharacterRoster.cs:271-284`) — a class with two FIRSTs throws.
- Unlock split: 1 Start + 4 Conquest + 5 Quest = 10. (No champion is gated on `verdant_hollow` or `pale_choir` any more — those were the pre-tier gates, frozen in `LegacyUnlocks.cs:46,48`.)
- Starting skills: 10 assigned, **9 distinct** — `hammer_blow` appears twice (`seeker` `:52`, `tower` `:128`). `SkillCatalogue.All` holds **12** skills (`SkillCatalogue.cs:351-712`, 6 styles × 2). 12 − 9 = **3 skills that are nobody's starting skill**: `sign_brand` (`:514`), `field_pulse` (`:616`), `drain_drink` (`:681`).
- Active/passive split (`SkillDef.TakesABeat => Kind == SkillKind.Active`, `SkillCatalogue.cs:303`): **5 ACTIVE** (seeker/tower `hammer_blow`, metronome `volley_spray`, oathbound `sign_call`, magpie `snare_repay`) and **5 PASSIVE** (anvil `hammer_press` Field, chorus `field_mire` Field, unbroken `drain_wilt` Field, quiver `volley_weep` Reaction, thornwall `snare_jaws` Reaction). `champion_starting_skill_test.cs:66-76` asserts only that both kinds appear, not the ratio.

### 2. How a character is unlocked, and what unlocking does to the shared skill library — does a starting skill get added account-wide? Quote the code that does it.

**Unlocking does NOTHING to the skill library. BECOMING the character does — and it is permanent and account-wide.** These are two different events, one frame apart at the earliest and possibly hours apart.

**(a) The unlock itself.** `CharacterState.Refresh` is handed the world's conquered set and derives the earned set, returning only the ids that turned on this call:
```csharp
// src/IdleXIdle.Core/Characters/CharacterState.cs:61-82
public IReadOnlyList<Character> Refresh(IEnumerable<string> conqueredRegionIds)
{
    ...
    foreach (var c in CharacterRoster.All)
    {
        if (_unlocked.Contains(c.Id)) continue;
        var earned = c.Unlock.Kind switch
        {
            UnlockKind.Start => true,
            UnlockKind.Conquest => c.Unlock.RegionId is { } r && conquered.Contains(r),
            UnlockKind.Quest => c.Unlock.QuestId is { } q && _questsDone.Contains(q),
            _ => false,
        };
        if (!earned) continue;
        _unlocked.Add(c.Id);
        fresh.Add(c);
    }
    return fresh;
}
```
The host calls it every frame, after evaluating quests, and the ONLY thing it does with the returned champions is post a toast and set a NEW badge:
```csharp
// src/IdleXIdle.Game/Game1.cs:4303-4313
foreach (var got in _characters.Refresh(_world.ConqueredIds))
{
    if (!_rosterBaselined) continue;
    PostNotice($"{got.Name.ToUpperInvariant()} JOINS YOU", "SWITCH HUNTER ON THE ROSTER SCREEN");
    _rosterNews = true;
    _rosterNewName = got.Name;
}
_rosterBaselined = true;
```
No skill is touched. The set is also banked to disk (`CharacterState.SaveUnlocked()`, `CharacterState.cs:129-130` → `SaveGame.UnlockedCharacters`, `SaveGame.cs:120`), and old saves are seeded from the frozen pre-tier gate table `LegacyUnlocks.Before` (`LegacyUnlocks.cs:38-51`) via `SaveGame.RestoreCharacters` (`SaveGame.cs:715-727`).

**(b) The account-wide add — it happens on BECOMING ACTIVE, every frame, forever.** This is the one line:
```csharp
// src/IdleXIdle.Game/Game1.cs:4323-4326
// BIRTH SKILLS ARE LEARNED BY BEING SOMEONE (P9). Becoming a champion latches theirs into
// the permanent set (D7), so the roster's YOU KEEP SKILLS holds for the one skill a
// champion brings, not only for the tree's. Idempotent, so per-frame is free.
_mastery.LearnSkill(_characters.Active.StartingSkillId);
```
and its target:
```csharp
// src/IdleXIdle.Core/Builds/MasteryTree.cs:230-233
public void LearnSkill(string? skillId)
{
    if (skillId is not null && SkillCatalogue.Find(skillId) is not null) _learned.Add(skillId);
}
```
`Game1.cs:4326` is the **only** production call site of `LearnSkill` in the repo (`grep -rn "LearnSkill(" src tests` → `MasteryTree.cs:230` definition, `Game1.cs:4326`, and `mastery_learned_permanence_test.cs:84-85`).

**The set it writes into is a single account-level set, not per character.** `_mastery` is one `MasteryTree` field (`Game1.cs:368 private MasteryTree _mastery = new();`, reset only on new game at `:1164`). `_learned` is a plain `HashSet<string>` (`MasteryTree.cs:158`), never cleared by `Respec()` (`MasteryTree.cs:259-263`) or `Refund` (`:243-253`), persisted as `SaveGame.LearnedSkills` (`SaveGame.cs:177`, written `Game1.cs:1063`, restored `Game1.cs:808`).

**Consequence, stated plainly:** play THE QUIVER once and `volley_weep` is in your permanent library forever — usable by THE SEEKER, by every champion, on every future build, across every respec and every reload. That is the exact invariant BRIEF §9 ("CHARACTER UNLOCK DOES NOT UNLOCK SIGNATURE ACCOUNT-WIDE") forbids. Today it is not merely possible, it is automatic and unavoidable — nothing gates it, nothing announces it, and there is no way to un-learn.

**A second, independent path to the same skill.** Even before the latch, the composer unions the ACTIVE character's starting skill into the taught set at compose time:
```csharp
// src/IdleXIdle.Core/Builds/BuildComposer.cs:135-139
// WHAT THIS CHAMPION KNOWS: the roads it has walked, plus the one skill it was born with.
var taughtSkills = mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
if (character?.StartingSkillId is { } born) taughtSkills.Add(born);
```
and the BUILD screen's library does the same:
```csharp
// src/IdleXIdle.Game/LoadoutScreen.cs:545-550
private IReadOnlySet<string> KnownSkills()
{
    var set = Mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
    if (Character?.StartingSkillId is { } born) set.Add(born);
    return set;
}
```
So there are **two** sources of access to a starting skill (the transient per-compose union, and the permanent latch), and the refactor has to close both.

**Also note:** all 12 skills — including all 9 starting skills — are ALSO taught by mastery road nodes (`MasteryCatalog.cs:458-469`, `Teaches(…)` → `GrantsSkillId`, latched at `MasteryTree.cs:210`). Nothing in the current model makes any skill exclusive to anyone.

### 3. How the ACTIVE character is chosen and stored, and what happens to the loadout when it changes.

**Chosen.** One production path: the ROSTER screen's SET ACTIVE button, taken in `Update` on the button's rect:
```csharp
// src/IdleXIdle.Game/RosterScreen.cs:331
if (CanSetActive(state) && UiKit.ClickedIn(ActionRect, hit, clicked)) Confirm(state);

// src/IdleXIdle.Game/RosterScreen.cs:347-360
public bool Confirm(CharacterState state)
{
    var c = CharacterRoster.Get(_selectedId);
    if (state.ActiveId == c.Id || !state.IsUnlocked(c.Id)) return false;
    state.Select(c.Id);
    _notice = c.Name;
    UiMotion.Flash(CardFlashKey(c.Id), UiMotion.Transition);
    UiMotion.Flash(InspectorFlashKey, UiMotion.Transition);
    _cue = "sfx_nav";
    return true;
}
```
Gate: `CanSetActive` = unlocked && not already active (`RosterScreen.cs:335-339`). The model refuses independently:
```csharp
// src/IdleXIdle.Core/Characters/CharacterState.cs:85-90
public bool Select(string id)
{
    if (CharacterRoster.Find(id) is null || !_unlocked.Contains(id)) return false;
    _activeId = id;
    return true;
}
```
The only other callers are screenshot/dev paths: `Game1.cs:2591 _characters.Select("anvil")` and `Game1.cs:2625 _characters.Select("seeker")` (inside `RH_SHOT_*` blocks that also force-complete quests).

**Stored.** In memory as one string field — `CharacterState._activeId` (`CharacterState.cs:36`), exposed as `ActiveId` (`:41`) and `Active => CharacterRoster.Get(_activeId)` (`:39`). `Get` falls back to the starter for an unknown id (`CharacterRoster.cs:234`). On disk it is one field: `SaveGame.ActiveCharacterId` (`SaveGame.cs:106`), written at `Game1.cs:1075 ActiveCharacterId = _characters.ActiveId,` and restored via `SaveGame.RestoreCharacters` → `CharacterState.Restore` (`CharacterState.cs:107-124`), which falls back to the starter on an unknown id (`:120-122`) and always re-adds the active id to the unlocked set (`:123`).

**What happens to the loadout when it changes — the honest answer: nothing happens to the skill loadout.** The switch is detected once per frame by comparing against a cached id, and exactly two things follow:
```csharp
// src/IdleXIdle.Game/Game1.cs:4319-4330
if (_lastActiveCharacterId is { } wasId && wasId != _characters.ActiveId) ShedUnwearable();
_lastActiveCharacterId = _characters.ActiveId;
_forge.FavouredClass = _characters.Active.Class;
_mastery.LearnSkill(_characters.Active.StartingSkillId);

_expedition.Character = _characters.Active;
_gear.Character = _characters.Active;
_masteryScreen.Character = _characters.Active;
```
1. **`ShedUnwearable()`** (`Game1.cs:3783-3801`) — GEAR only. It walks `GearSlot`, unequips anything `Gear.CanWear(who, worn)` refuses, posts `"{who.Name} CANNOT WEAR YOUR {words} — IT IS BACK IN YOUR BAG"` and saves. It never touches `_loadout`.
2. **The latch** (`:4326`, see Q2) — adds the NEW character's starting skill to the permanent account library.

The woven-skill list, the sockets, the Vows, the slot order, the capacities: all untouched. `_loadout` is never re-validated, repaired, cleared or swapped on a character switch — `grep -n "_loadout" src/IdleXIdle.Game/Game1.cs` shows no write anywhere near `:4319`. The reason it doesn't break today is precisely the latch: because the OLD character's starting skill was latched permanently while they were active, a build woven around it keeps composing after the switch. Remove the latch and every switch silently drops that slot at `BuildComposer.cs:156` (`if (!taughtSkills.Contains(def.Id)) continue;`) with no toast, no repair and no UI change — the loadout screen would still draw the skill (`LoadoutScreen.SlotFilled`, `:556`, does check `KnownSkills()`, so it would grey out; the composer just skips it).

**Ordering note for the refactor:** `Game1.cs:4270` composes `wornBuild` BEFORE the latch at `:4326` runs. This is harmless today only because `BuildComposer.cs:139` unions the active character's `StartingSkillId` independently.

Also set from the active character each frame: `_training.Character` (`Game1.cs:1904, 3229`), `_loadoutScreen.Character` (`:3268`), `_expedition.Character` / `_gear.Character` / `_masteryScreen.Character` (`:4328-4330`), `_forge.FavouredClass` (`:4322`), `_forge.RarityBonus` (`:4271`), the trader's class (`:3160`), and the wear check `ItemClasses.WhyNot(_characters.Active, toWear)` (`:3088`).

### 4. Whether loadouts are per-character or global. Quote the storage.

**GLOBAL. There is exactly one loadout for the whole account. Per-character loadouts do not exist — there is no dictionary, no map, no character-keyed save row, nothing.**

**In memory** — one field, constructed once, replaced only on new game:
```csharp
// src/IdleXIdle.Game/Game1.cs:367
private PlayerLoadout _loadout = PlayerLoadout.Starter();

// src/IdleXIdle.Game/Game1.cs:1163  (new-game reset)
_loadout = PlayerLoadout.Starter();
```
`PlayerLoadout` itself holds two plain lists with no character key anywhere in the type:
```csharp
// src/IdleXIdle.Core/Builds/PlayerLoadout.cs:43-46
private readonly List<SkillChoice> _skills = new();
private readonly List<string> _keystoneIds = new();

public IReadOnlyList<SkillChoice> Skills => _skills;
```
`SkillChoice` is `(Source Source, string? VowId, bool? Passive = null, string? SkillId = null)` (`PlayerLoadout.cs:40-41`) — no character id.

**On disk** — one flat list, once:
```csharp
// src/IdleXIdle.Core/Persistence/SaveGame.cs:152-156
/// <summary>The player's woven build — four skills. Empty on a pre-solo-model save (keeps the starter).</summary>
public List<SavedSkill> WovenSkills { get; init; } = new();

/// <summary>Which learned keystones are socketed. Ids into the keystone catalog.</summary>
public List<string> SocketedKeystoneIds { get; init; } = new();
```
written as:
```csharp
// src/IdleXIdle.Game/Game1.cs:1044-1049
WovenSkills = _loadout.SaveSkills()
    .Select(s => new SavedSkill
    {
        SkillId = s.SkillId, Source = s.Source, VowId = s.VowId, Passive = s.Passive,
    }).ToList(),
SocketedKeystoneIds = _loadout.KeystoneIds.ToList(),
```
and restored once at `Game1.cs:785-802` (`_loadout.Restore(...)`, `PlayerLoadout.cs:275-338`).

**Everything the loadout depends on is likewise account-level, not per character:**
- `_mastery` — one `MasteryTree` (`Game1.cs:368`), incl. `_learned` (`MasteryTree.cs:158`) → `SaveGame.LearnedSkills` (`:177`) and `MasteryTaken` (`:170`).
- `_skillProgress` — one `SkillProgress` (`Game1.cs:456`) → `SaveGame.SkillProgress`, keyed by skill id (`SaveGame.cs:158-167`: "a skill's levels belong to the SKILL and not to where it happens to be woven").
- `_dust` — one Memory Dust tree.

**Verification:** `grep -rn "PlayerLoadout" src` returns exactly one field (`Game1.cs:367`) plus five screens holding a *reference* to that same instance (`GearScreen.cs:146`, `HuntScreen.cs:698`, `LoadoutScreen.cs:252`, `MasteryScreen.cs:331`, `TrainingScreen.cs:55`). `grep -rniE "Dictionary<string, *PlayerLoadout>|loadoutFor|perCharacterLoadout|LoadoutsBy" src tests` returns **nothing**.

This directly answers BRIEF §13's conditional "If the current game stores loadouts per Character, preserve each Character's valid loadout instead" — **it does not**. One loadout follows the player across every switch.

### 5. What RosterScreen prints for the starting skill (exact strings and the file:line).

All of it is in one block of `DrawDetail`, in the **inspector column only** — the roster CARDS print nothing about the starting skill (they print `c.ShortName`, `c.PassiveName`, and a `PLAYING` / `READY` / `LOCKED` pill; see `RosterScreen.cs:555, 564, 590, 600, 627`).

**The lookup:**
```csharp
// src/IdleXIdle.Game/RosterScreen.cs:671
var skill = c.StartingSkillId is { } sid ? SkillCatalogue.Find(sid) : null;
```

**The block — `RosterScreen.cs:743-766`:**

| What is drawn | Exact string / expression | file:line |
|---|---|---|
| Section heading | `"STARTING SKILL"` (literal, `UiTypography.Secondary`, colour `Slate`) | `RosterScreen.cs:746` |
| Right-aligned badge, only when the account already knows it | `"ALREADY KNOWN"` (colour `Met`), guarded by `if (skill is not null && Mastery?.LearnedSkills().Contains(skill.Id) == true)` | `RosterScreen.cs:747-748` |
| Fallback when no starting skill | `"NONE."` | `RosterScreen.cs:752` — **dead code today**: all ten characters have a `StartingSkillId`, and `champion_starting_skill_test.cs:41` asserts it |
| Icon | `_ui.Icon(b, $"icon_skill_{skill.Id}", …, unlocked ? Gold : Slate)` — e.g. `icon_skill_hammer_blow` | `RosterScreen.cs:759` |
| The skill line | `$"{skill.Name} · {skill.Style.ToString().ToUpperInvariant()} · {(skill.TakesABeat ? "ACTIVE" : "PASSIVE")}"`, passed through `_ui.ShortenBig(...)` | `RosterScreen.cs:760-763` |
| The description | `skill.Line`, word-wrapped, clamped to 2 lines (`skillLines`, computed `:680`) | `RosterScreen.cs:765` (measure at `:680`) |

**What that renders, per character** (`SkillDef.Name` / `Style` / `TakesABeat` from `SkillCatalogue.cs`; `Line` is the def's 2nd string arg):

| Character | Line at `:760-763` | Line at `:765` (`skill.Line`) |
|---|---|---|
| THE SEEKER | `BLOW · HAMMER · ACTIVE` | "Heavy damage to one target." (`SkillCatalogue.cs:352`) |
| THE ANVIL | `PRESS · HAMMER · PASSIVE` | "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25." (`:384`) |
| THE CHORUS | `MIRE · FIELD · PASSIVE` | "Damages every enemy every 1s, and slows their attacks by 25%." (`:647`) |
| THE METRONOME | `SPRAY · VOLLEY · ACTIVE` | "Fires 5 arrows across the wave." (`:546`) |
| THE UNBROKEN | `WILT · DRAIN · PASSIVE` | "Attack break: every enemy's damage drops 10% a pulse, down to -50%." (`:713`) |
| THE FALLING TOWER | `BLOW · HAMMER · ACTIVE` | "Heavy damage to one target." (`:352`) |
| THE QUIVER | `WEEP · VOLLEY · PASSIVE` | "When an enemy dies it leaves bleed on the wave worth 30% of its health." (`:585`) |
| THE THORNWALL | `JAWS · SNARE · PASSIVE` | "Every bite returns 50% of it to the enemy that bit you. Rearms every 3s." (`:448`) |
| THE OATHBOUND | `CALL · SIGN · ACTIVE` | "All your damage +60% for 6s." (`:478`) |
| THE MAGPIE | `REPAY · SNARE · ACTIVE` | "Deals 200% of the health damage you have taken since its last cast." (`:416`) |

**Two more strings on this screen that assert the account-wide semantics the refactor must reverse:**
- `RosterScreen.cs:315` — `private const string FreeLine = "SWITCHING IS FREE. YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY.";` drawn in the state block at `:865`. "YOUR SKILLS … STAY" is a promise made to the player about exactly the behaviour BRIEF §9 forbids.
- `RosterScreen.cs:744` (comment) and `:21-23` (class doc) — "It is latched permanently into your skill set the moment you play them." These comments are **accurate** against the code (unusually — see risks).

**Wiring:** `Mastery` is a nullable property (`RosterScreen.cs:180 public MasteryTree? Mastery { get; set; }`), set by the host on every update and draw (`Game1.cs:3287` — "so the inspector can say a starting skill is ALREADY KNOWN" — and `Game1.cs:5001`). If it were null the ALREADY KNOWN badge would silently never appear.

**The word "SIGNATURE" appears nowhere in `RosterScreen.cs`** (`grep -n "SKILL" RosterScreen.cs` → lines 21, 30, 315, 687, 743, 746 only).

**Files this area will change**

- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Characters/Character.cs — `StartingSkillId` (:126) and its doc-comment (:116-125) are the field the refactor renames/re-homes to a Signature. `Character` is a `record` with `required` members, so adding a `SignatureSkillId` is source-compatible but every object initialiser in the roster must be visited.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Characters/CharacterRoster.cs — the ten `StartingSkillId` assignments (:52, 69, 84, 98, 114, 128, 145, 167, 194, 217) and the ten innates. Also `Grid`/`ClassColumns`/`ByClass` (:249-284), which the ROSTER screen walks and three tests hold.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Characters/CharacterState.cs — where a switch happens (`Select`, :85-90). If the refactor needs loadout repair on switch (BRIEF §13), this is the only model-side hook; today it changes one string and returns.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Characters/LegacyUnlocks.cs — MUST NOT CHANGE. It is the frozen pre-2026-08-26 gate table read only by `SaveGame.RestoreCharacters` for saves without `UnlockedCharacters`. Editing it silently takes champions away from old saves.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/BuildComposer.cs — :138-139 the `taughtSkills` union that grants the active character its born skill, and :156 the unconditional gate `if (!taughtSkills.Contains(def.Id)) continue;`. This is where a Signature must become character-exclusive and where a foreign Signature must be refused.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/MasteryTree.cs — `LearnSkill` (:230-233) is the account-wide latch that violates BRIEF §9; `_learned` (:158), `RestoreLearned` (:215-221), `LearnedSkills()` (:308-314) and the D7 permanence comments (:154-157, :86-91) all describe access semantics BRIEF §15-§17 changes.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/MasteryCatalog.cs — :458-469, the twelve `Teaches(...)` road nodes that make every skill (including all nine starting skills) tree-learnable. Signatures must be exempt from this catalogue (BRIEF §18).
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/SkillCatalogue.cs — the 12 shared defs (:351-712). Ten new Signature defs land here or in a sibling catalogue; `SkillCatalogue.Find`/`ById` is the id resolver every gate uses, so a Signature must resolve or nothing will compose.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Builds/PlayerLoadout.cs — the ONE global loadout. `SetSkill` (:178-185) carries the duplicate law (BRIEF §14); `Starter()` (:359-364) hard-codes `hammer_blow`; `Restore` (:275-338) does the legacy/duplicate migrations a Signature id must survive.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Core/Persistence/SaveGame.cs — `LearnedSkills` (:177) will start carrying foreign Signatures on any existing save; `WovenSkills`/`SocketedKeystoneIds` (:152-156) are the single global loadout rows; `ActiveCharacterId`/`UnlockedCharacters`/`QuestsDone` (:106-123) and `RestoreCharacters` (:715-727) are the roster's whole persistence surface.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/Game1.cs — :4326 the per-frame account-wide latch (the single line to remove or re-point); :4319-4320 the switch detection and `ShedUnwearable`; :4270 the compose that runs before the latch; :808/:1063 learned-skill restore/save; :1075-1077 roster save; :3268/:4328-4330 the `Character` fan-out; :2588-2626 the dev/screenshot blocks that force-unlock and `Select`.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/RosterScreen.cs — :671 the lookup, :743-766 the whole STARTING SKILL block (heading, ALREADY KNOWN, NONE., icon key, `Name · STYLE · ACTIVE/PASSIVE`, description), :685-699 the measure-then-drop layout that must be re-costed if the block grows an EXCLUSIVE TO line, :315 the `FreeLine` promise "YOUR SKILLS … STAY", :21-23 the class doc.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/LoadoutScreen.cs — :545-550 `KnownSkills()`, the second independent source of starting-skill access; :556 `SlotFilled`; :542/:559/:585 the `ToBuild` call sites. This is the BUILD UI that must pin and mark the Signature (BRIEF §12).
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/MasteryScreen.cs — :1807 and :2207 read `Mastery.LearnedSkills()` to draw a road node as learned; the §15-§17 access-semantics change lands visibly here.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/src/IdleXIdle.Game/HuntScreen.cs — :3939 holds `Character`; :837 `_undyingLive = trig.Contains(BuildTrigger.Undying)` reads THE UNBROKEN's innate for the hunter card; :2728 reads `UndyingSpent`.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Builds/champion_starting_skill_test.cs — asserts every champion has a non-null `StartingSkillId`, that it resolves in `SkillCatalogue`, that it composes into slot 0 on a FRESH tree (:41-62), and that the roster mixes active and passive (:66-76). Every assertion is about the model being replaced.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Builds/mastery_learned_permanence_test.cs — :79 `test_becoming_a_champion_latches_its_birth_skill_for_good` is the test that PINS the account-wide behaviour BRIEF §9 forbids. It must be inverted, not deleted quietly.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/characters_roster_test.cs — 15 tests over unlock derivation, `Select` refusal, save round-trip, and every innate's sim-readability (`test_every_character_changes_something_the_sim_reads`, :161). The last one is the guard against a re-authored innate going dormant.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Characters/roster_parity_test.cs — the balance bench; :146-155 builds a synthetic `Character` with `StartingSkillId = skill.Id` and :277 falls back to a `Chassis` when there is none. Adding Signatures changes what every champion's measured throughput means.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Characters/champion_tiers_test.cs — 14 tests pinning one FIRST + one SECOND per class, quest gating, `TierLine`, `ShortName`, and each second champion's threshold. Any roster edit trips these.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Characters/roster_grid_test.cs — 6 tests holding `CharacterRoster.Grid` shape and column order; the ROSTER screen only walks the data.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Core.Tests/Persistence/unlocked_characters_test.cs — 8 tests on the banked-unlock migration and the frozen `LegacyUnlocks` table (:150 asserts it IS the pre-tier roster).
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/tests/unit/IdleXIdle.Game.Tests/roster_switch_feedback_test.cs — 6 tests on SET ACTIVE: press taken in `Update` on the button rect, one flash, one cue, refusal arms nothing, hit-testing at 3 density profiles. Any change to `Confirm`/`ActionRect` breaks these.
- C:/Users/sanan/OneDrive/Masaüstü/idleXidle/design/gdd/characters.md — STALE and contradicts the code on four counts (see risks). If the refactor touches characters this doc is the one design artefact that must be regenerated, not patched.

**Risks**

- THE LOAD-BEARING LINE IS `Game1.cs:4326`. `_mastery.LearnSkill(_characters.Active.StartingSkillId)` is the ONLY thing keeping a build woven around a champion's own skill alive across a switch. Remove it without a replacement and every player who wove their starting skill loses that slot the instant they change hunter — silently, at `BuildComposer.cs:156`, with no toast, no repair and no error. The composer's own union (`BuildComposer.cs:139`) only covers the CURRENTLY active character.
- THE ACCOUNT-WIDE LEAK IS ALREADY IN EVERY EXISTING SAVE. `SaveGame.LearnedSkills` (`:177`) is written as the union of the latch and the taken roads (`Game1.cs:1063`), so a player who has ever played THE QUIVER has `volley_weep` banked permanently. Making Signatures exclusive requires a MIGRATION over `LearnedSkills`, not just a code change — otherwise the invariant is enforced for new accounts and broken for every old one. The 9 starting skill ids are also legitimately learnable from mastery roads (`MasteryCatalog.cs:458-469`), so a migration CANNOT tell 'latched by being the champion' from 'learned on the tree' — the save has one flat list and no provenance.
- TWO INDEPENDENT ACCESS PATHS MUST BOTH CLOSE. `BuildComposer.cs:138-139` (the sim's gate) and `LoadoutScreen.cs:545-550` (the library's gate) each union `Character.StartingSkillId` separately. Closing one and not the other produces the exact failure this codebase is named for: a screen that offers a skill the fight refuses, or a fight that runs a skill the screen never showed.
- LOADOUTS ARE GLOBAL, SO BRIEF §13's PREFERRED PATH IS UNAVAILABLE. There is one `PlayerLoadout` (`Game1.cs:367`) and one `WovenSkills` row set (`SaveGame.cs:153`). 'Preserve each Character's valid loadout instead' would mean inventing per-character storage, a save-format change, and a migration — it is not a matter of flipping a flag.
- NOTHING REPAIRS THE LOADOUT ON SWITCH TODAY. `Game1.cs:4319` only calls `ShedUnwearable()`, which touches GEAR (`:3783-3801`). There is no equivalent for skills, no toast channel for a dropped skill, and no test covering one. A Signature-validity repair is entirely new machinery, not an edit to existing machinery.
- hammer_blow IS SHARED BY TWO CHAMPIONS (`CharacterRoster.cs:52` seeker, `:128` tower) AND IS ALSO `PlayerLoadout.Starter()`'s hard-coded slot (`PlayerLoadout.cs:362`) AND is taught by `road_hammer` (`MasteryCatalog.cs:458`). If starting skills become exclusive Signatures 1:1, this collision must be resolved deliberately — and `Starter()` needs a new answer for what a brand-new save weaves.
- THREE SKILLS ARE NOBODY'S STARTING SKILL — `sign_brand`, `field_pulse`, `drain_drink`. If the refactor reasons about 'the twelve minus the starting nine' anywhere, note the count is 9 distinct out of 10 assignments, not 10.
- INNATES ARE NOT DECORATION — EVERY ONE IS READ BY THE SIM, AND ONE TEST GUARDS THAT. `characters_roster_test.cs:161 test_every_character_changes_something_the_sim_reads` exists because `BuildMods.Rarity` was once resolved, carried and summed by correct code and read by nothing. BRIEF §5 says a Signature must not duplicate the Innate; verify against the FIELDS above (HitSize/OverkillCarry/PerCreatureBonus/FreeOpeningCast/FirstHit/LaterHit/SkillRate/FlatDamageReduction/DamageTaken/StylePower[Snare]/VowPowerMultiplier/AmplifyWindow/AmplifyPowerBonus, Undying, LooseAgain, Haul 1.35 + Rarity 1.20), not against the card text.
- THE ROSTER INSPECTOR IS HEIGHT-BUDGETED AND WILL SILENTLY DROP CONTENT. `RosterScreen.cs:685-699` measures a fixed block and then drops ROAD AND GEAR, then blurb lines, then innate lines until the state block and button fit. Adding an `EXCLUSIVE TO <CHARACTER>` line (BRIEF §11) without re-costing `fixedH` (`:685-689`) will push other content off the card at 150% density with no error.
- design/gdd/characters.md IS STALE ON FOUR COUNTS AND THE CODE IS RIGHT. It says (`:22-28`) 'Unlocks are derived, never banked' — false, they are banked (`CharacterState.SaveUnlocked` :129, `SaveGame.UnlockedCharacters` :120, `LegacyUnlocks` seeding). It says 'Two characters are gated behind quests' — five are. It says 'The save carries only the active id and the finished-quest set' — it also carries the unlocked set. Its roster table (`:36-45`) still has an 'Aptitude' column (the field no longer exists on `Character`), road names 'Weight'/'Spread' instead of `Branch.Resonance`/`Branch.Loot`, and THE QUIVER's quest as 'THE DEEP HOLLOW (reach wave 60 in the Verdant Hollow)' instead of `q_quiver_volleys` / 150 VOLLEY waves. Believe the code.
- THE ROSTER-SCREEN COPY IS ACCURATE TODAY AND WILL BECOME A LIE. `RosterScreen.cs:315` 'SWITCHING IS FREE. YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY.' and `:21-23`/`:744` 'latched permanently into your skill set the moment you play them' correctly describe current behaviour. The Signature refactor makes both false; they are drawn on the screen the player uses to switch, so they must move in the same change.
- `LegacyUnlocks.cs` IS FROZEN BY CONTRACT (`:18` 'It must never change again; that is the point of it'), and `unlocked_characters_test.cs:150` asserts it is exactly the pre-tier roster. Touching it to 'tidy' the roster would retroactively lock champions on old saves.
- `RosterScreen.NONE.` (`:752`) IS DEAD CODE — no champion has a null starting skill and `champion_starting_skill_test.cs:41` forbids one. Do not treat it as a live state; a Signature-less champion has never been rendered and that path has never been looked at.
- SWITCHING IS DETECTED BY A CACHED STRING IN THE HOST, NOT BY AN EVENT. `Game1.cs:4319 _lastActiveCharacterId` is the only switch signal, and it is compared once per frame in `Update`. Any repair hook added elsewhere (e.g. inside `CharacterState.Select`) will run on the dev/screenshot `Select` calls at `Game1.cs:2591, 2625` too.
- THE ROSTER GRID FAILS HARD ON AN ILLEGAL ROSTER. `CharacterRoster.BuildGrid` uses `All.Single(c => c.Class == cls && c.Tier == tier)` (`:280`) in a static initialiser — adding an 11th champion or a second FIRST of a class throws a `TypeInitializationException` at first touch of `CharacterRoster`, which is every screen. That is deliberate (`:278-279`), but it means a partial roster edit crashes the game rather than degrading.

## SAVE FORMAT AND MIGRATION

### 1. EVERY field of SaveGame, with its type, default and meaning. A complete table - this is the migration surface.

`SaveGame` is a `sealed record` at `src/IdleXIdle.Core/Persistence/SaveGame.cs:14`. It has **58 `get; init;` properties** (counted: `sed -n '14,286p' SaveGame.cs | grep -c 'get; init;'` → 58) plus one `const int CurrentVersion = 3` (line 17). Six nested DTOs add 51 more persisted fields: SavedChest 6, SavedSkill 5, RunReportSave 20, RegionFarmSave 4, SavedItem 12, SavedSkillProgress 4. **Total migration surface = 58 + 51 = 109 persisted fields.**

Serializer options: `WriteIndented = true`, `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (`SaveGame.cs:485-489`). Consequence, load-bearing for the refactor: **only nullable reference fields are omitted when null; every value-type field is always emitted, even at its default.** Unknown JSON members are SKIPPED on read (System.Text.Json default; nothing overrides it — `SaveGame.cs:58-60` states this and `SaveSystemTests.cs:62-64` pins it).

### SaveGame — all 58 fields

| # | Field | Type | Default | Meaning / notes | Written by | Read by |
|---|---|---|---|---|---|---|
| 1 | `Version` | `int` | `CurrentVersion` = 3 (`:17,:19`) | Format stamp. Used ONLY as a future-refusal gate + snapshot trigger | implicit | `SaveGame.cs:520`, `Game1.cs:767` |
| 2 | `SavedAtMs` | `long` | 0 (`:29`) | UTC epoch ms; basis of offline progression | `Capture :552` | `Deserialize :525` |
| 3 | `Gleam` | `int` | 0 (`:31`) | Currency | `:558` | `RestoreHunter :670` |
| 4 | `Materials` | `int` | 0 (`:34`) | SCRAP tier (legacy single stock) | `:559` | `RestoreHunter :671` |
| 5 | `Essence` | `int` | 0 (`:37`) | Material tier 2 | `:561` | `:672` |
| 6 | `Core` | `int` | 0 (`:38`) | Material tier 3 | `:562` | `:673` |
| 7 | `Crystal` | `int` | 0 (`:39`) | Material tier 4 | `:563` | `:674` |
| 8 | `Charters` | `Dictionary<string,int>` | empty (`:49`) | Single-use Forge charters, keyed by enum NAME not ordinal | `:560` | `RestoreHunter :678-686` |
| 9 | `TrainingRanks` | `Dictionary<string,int>` | empty (`:51`) | `HunterStat` name → rank bought | `:590` | `RestoreHunter :690-703` |
| 10 | `RegionMasteryPoints` | `float` | 0 (`:55`) | **Legacy single-region mastery. WRITTEN NO MORE (P14)** — Capture never sets it, so it always serializes as 0 | never | `RestoreWorld :771` (only when `RegionFarms` empty) |
| 11 | `Inventory` | `List<SavedItem>` | empty (`:62`) | The bag | `:592` | `RestoreInventory :659-666` |
| 12 | `MemoryDust` | `int` | 0 (`:64`) | The Dust WALLET (a material — Warren/checkpoints spend it). **Not trait points** | `:572` | `Game1.cs:770` |
| 13 | `MemoryDustUnlocks` | `List<string>` | empty (`:65`) | **THE TRAIT TREE STATE** — owned node ids of the 51-node `MemoryDustTree` | `:573` | `Game1.cs:770` → `MemoryDustTree.Restore` (`MemoryDust.cs:195-201`) |
| 14 | `WornWeaponId` | `string?` | null (`:68`) | Worn gear by InstanceId | `:564` | `Game1.cs:887` |
| 15 | `WornCharmId` | `string?` | null (`:69`) | ″ | `:565` | `Game1.cs:887` |
| 16 | `WornFocusId` | `string?` | null (`:70`) | ″ | `:566` | `Game1.cs:887` |
| 17 | `WornHelmId` | `string?` | null (`:73`) | 8-slot loadout addition | `:567` | `Game1.cs:888` |
| 18 | `WornChestId` | `string?` | null (`:74`) | ″ | `:568` | `Game1.cs:888` |
| 19 | `WornGlovesId` | `string?` | null (`:75`) | ″ | `:569` | `Game1.cs:888` |
| 20 | `WornBootsId` | `string?` | null (`:76`) | ″ | `:570` | `Game1.cs:889` |
| 21 | `WornRingId` | `string?` | null (`:77`) | ″ | `:571` | `Game1.cs:889` |
| 22 | `HighestMasteryAwarded` | `int` | 0 (`:80`) | Highest mastery level ever, so Dust milestones award once | `:574` | `Game1.cs:787` |
| 23 | `ConqueredRegions` | `List<string>` | empty (`:83`) | World conquest | `:575` | `RestoreWorld :756`, `RestoreCharacters :722` |
| 24 | `ActiveRegion` | `string` | `""` (`:84`) | Where the player is | `:576` | `Game1.cs:898` |
| 25 | `RegionFarms` | `List<RegionFarmSave>` | empty (`:85`) | Per-region mastery + depth record + checkpoint | `:579-589` | `RestoreWorld :758-768`, `RestoreCharacters :724` |
| 26 | `RunLog` | `List<RunReportSave>` | empty (`:94`) | Last ten run reports, newest first | `Game1.cs:1059` | `Game1.cs:831` |
| 27 | `CorruptionTier` | `int` | 0 (`:97`) | Endgame ratchet, current | `:577` | `RestoreWorld :757` |
| 28 | `CorruptionPeak` | `int` | 0 (`:101`) | Deepest tier ever. Feeds trait points | `:578` | `RestoreWorld :757` |
| 29 | `ActiveCharacterId` | `string` | `""` (`:106`) | Which champion is played; unknown falls back to starter | `Game1.cs:1075` | `RestoreCharacters :726` |
| 30 | `UnlockedCharacters` | `List<string>` | empty (`:120`) | Banked earned roster. **EMPTY IS A SENTINEL meaning "written before the field existed"** (`:117`) → seeded from `LegacyUnlocks` | `Game1.cs:1076` | `RestoreCharacters :719-725` |
| 31 | `QuestsDone` | `List<string>` | empty (`:123`) | Finished quest ids | `Game1.cs:1077` | `RestoreCharacters :726` |
| 32 | `RunsWithVowKept` | `int` | 0 (`:133`) | Latched EVENT counter — descents finished with a Vow's demand met | `Game1.cs:1078` | `Game1.cs:837`, `LegacyUnlocks.Seed :725` |
| 33 | `ChestsOpened` | `int` | 0 (`:143`) | Career chest count | `Game1.cs:1080` | `Game1.cs:840,860` |
| 34 | `BossesFelled` | `int` | 0 (`:150`) | THE MAGPIE's gate counter; seeded from `ChestsOpened` when absent | `Game1.cs:1079` | `Game1.cs:840` |
| 35 | `WovenSkills` | `List<SavedSkill>` | empty (`:153`) | The four-skill build. Empty = pre-solo save (keeps starter) | `Game1.cs:1044-1048` | `Game1.cs:785,791-794` |
| 36 | `SocketedKeystoneIds` | `List<string>` | empty (`:156`) | Which learned keystones are worn | `Game1.cs:1049` | `Game1.cs:794` |
| 37 | `SkillProgress` | `List<SavedSkillProgress>` | empty (`:167`) | Per-SKILL (not per-slot) uses/variation/reinforcements | `Game1.cs:1050-1055` | `Game1.cs:803-804` |
| 38 | `MasteryTaken` | `List<string>` | empty (`:170`) | Mastery-tree nodes taken | `Game1.cs:1060` | `Game1.cs:807` → `MasteryTree.RestoreTaken` |
| 39 | `LearnedSkills` | `List<string>` | empty (`:177`) | **Skills learned FOR GOOD (D7). Respec never removes one.** Absent → seeded from `MasteryTaken`'s roads. This is the field brief §21/§95 targets | `Game1.cs:1063` | `Game1.cs:808` → `MasteryTree.RestoreLearned` |
| 40 | `CompletedSets` | `List<string>` | empty (`:185`) | Set ladders already celebrated once | `Game1.cs:1065` | `Game1.cs:814` |
| 41 | `MasteryEarned` | `int` | 0 (`:188`) | **Comment lies:** doc-comment says "total mastery points earned"; the code stores DEEPEST-EVER WAVE (`Game1.cs:1086` `MasteryEarned = _deepestEver`, read back as `_deepestEver = save.MasteryEarned` at `Game1.cs:806`). Believe the code | `Game1.cs:1086` | `Game1.cs:806` |
| 42 | `MasteryZoom` | `float` | 0 (`:200`) | Tree camera zoom; 0 = never opened | `Game1.cs:1068` | `Game1.cs:818` |
| 43 | `MasteryPanX` | `float` | 0 (`:201`) | Tree camera pan | `Game1.cs:1069` | `Game1.cs:818` |
| 44 | `MasteryPanY` | `float` | 0 (`:202`) | Tree camera pan | `Game1.cs:1070` | `Game1.cs:818` |
| 45 | `DismissedGuideRungs` | `List<string>` | empty (`:213`) | `TutorialStep` NAMES the player closed | `Game1.cs:1088` | `Game1.cs:843-844` |
| 46 | `IntroSeen` | `bool` | false (`:220`) | Click-through intro finished/skipped | `Game1.cs:1089` | `Game1.cs:848` |
| 47 | `ExplainedScreens` | `List<string>` | empty (`:231`) | `Activity` NAMES + `SkillSlotN` entries closed | `Game1.cs:1090` | `Game1.cs:849` |
| 48 | `ChampionGleamRate` | `float` | 0 (`:234`) | Recent GLEAM/s for offline earning | `Game1.cs:1087` | `Game1.cs:949` |
| 49 | `UnopenedChests` | `List<SavedChest>` | empty (`:243`) | Uncracked boss drops | `Game1.cs:1092-1098` | `Game1.cs:874-884` |
| 50 | `FreeSocketUsed` | `bool` | false (`:254`) | The first gem-set is free; this spends it | `Game1.cs:1081` | `RestoreFreeSocketUsed :734-738` |
| 51 | `ChestKeepMinTier` | `int` | 0 (`:257`) | VAULT keep-filter tier; 0 = keep all | `Game1.cs:1082` | `Game1.cs:863` |
| 52 | `ChestKeepSlot` | `string?` | null (`:261`) | **Legacy single-slot lean. WRITTEN NO MORE (P14)** | never | `Game1.cs:871-872` (only when `ChestKeepSlots` empty) |
| 53 | `ChestKeepSlots` | `List<string>` | empty (`:265`) | Keep-filter wanted slots | `Game1.cs:1085` | `Game1.cs:868-869` |
| 54 | `TraderWeekStamp` | `int` | 0 (`:270`) | ISO week (year*100+week) the stall belongs to | `Game1.cs:1083` | `Game1.cs:864` |
| 55 | `TraderBoughtSlots` | `List<int>` | empty (`:273`) | Stall slots 0..3 bought this week | `Game1.cs:1084` | `Game1.cs:865-866` |
| 56 | `WarrenLevel` | `int` | **1** (`:277`) | The one non-zero default — pre-Warren saves load a level-1 base | `:553` | `RestoreWarren :781` |
| 57 | `WarrenXp` | `int` | 0 (`:278`) | Warren XP | `:554` | `RestoreWarren :781` |
| 58 | `WarrenFacilities` | `Dictionary<string,int>` | empty (`:281`) | `FacilityKind` NAME → level; empty → every facility at 1 | `:555-557` | `RestoreWarren :778-781` |

**Every one of the 58 is read somewhere. There is no dormant save field.** Two (`RegionMasteryPoints`, `ChestKeepSlot`) are read-only legacy: the writer never populates them again.

**Fields written from TWO different places.** `SaveSystem.Capture` (`SaveGame.cs:544-593`) covers only 20 fields; the other 38 are attached in `Game1.Save()`'s `with` block (`Game1.cs:1039-1099`). **A new field added to `SaveGame` but not to `Game1.Save()`'s `with` block silently serializes at its default forever.**

### Nested DTOs

**`SavedItem`** (`:398-450`) — 12 fields: `InstanceId` `required string`; `BaseType` `required string`; `Rarity` `required int`; `SellValue` `required int`; `ItemLevel` `int` = **1** (`:406`); `Upgrades` `int` = 0; `TraitOverride` `string?` = null, written as `"NONE"` for a plain item (`:608`) so the loader can tell "rolled plain" from "pre-redesign"; `EnchantOverride` `string?` = null; `Gems` `List<SavedItem>` = empty (recursive); `Element` `string?` = null; `Class` `string?` = null; `Family` `int?` = null.

**`SavedChest`** (`:289-309`) — 6 fields: `Rarity` `required int`; `Tier` `required int`; `Element` `string?`; `Region` `string?`; `RunTilt` `float` = **1f** (`:305`, deliberately 1 not 0 — a 0 default would make every held chest worthless); `Gift` `string?`.

**`SavedSkill`** (`:312-339`) — 5 fields: `SkillId` `string?` = null (the identity since v3, `:319`); `Source` `string?` = null (`:322`); `Form` `string?` = null — **WRITTEN NO MORE (P3-final), read only to migrate a pre-v3 row** (`:325`, and `Game1.cs:1047` omits it); `VowId` `string?` = null (`:327`); `Passive` `bool?` = null (`:338`).

**`SavedSkillProgress`** (`:800-812`) — 4 fields: `SkillId` `required string`; `Uses` `int` = 0; `Variation` `string?` = null; `Reinforcements` `List<string>` = empty.

**`RegionFarmSave`** (`:386-396`) — 4 fields: `Id` `required string`; `MasteryPoints` `float` = 0; `BestDepth` `int` = 0; `StartWave` `int` = 0.

**`RunReportSave`** (`:347-378`) — 20 fields: `RegionId` `required string`; `Depth` `int`; `IsRecord` `bool`; `Outcome` `int`; `WallWave` `int`; `WallArchetype` `int`; `WallAffixes` `List<int>`; `OutcomeName` `string?`; `WallArchetypeName` `string?`; `WallAffixNames` `List<string>`; `WallCreatures` `int`; `AbsorbedFraction` `float`; `AverageHitSize` `float`; `TargetsPerActivation` `float`; `CreaturesPerWave` `float`; `HealthLostPerWaveFraction` `float`; `ShieldAbsorbedFraction` `float`; `ShieldAbsorbedPerWaveFraction` `float`; `SecondsPerWave` `float`; `SampledWaves` `int`. (The bare `int` enum trio at `:352-355` is legacy; since P13 enums travel by NAME at `:360-362`, and the ints are read through a frozen table `RunLog.LegacyAffixByIndex`.)

### 2. How migration is done today: is there a version number, or is it additive-with-defaults? Quote an example of a past migration.

**BOTH exist, but they do different jobs — and the version number does NOT drive any migration.**

### The version number is a one-way refusal gate, not a dispatcher

`SaveGame.CurrentVersion = 3` (`SaveGame.cs:17`). `save.Version` is read in exactly **two places in the entire repo** (`grep -rn 'save\.Version'`):

```csharp
// src/IdleXIdle.Core/Persistence/SaveGame.cs:520-521
if (save.Version > SaveGame.CurrentVersion)
    return new LoadResult { Failure = LoadFailure.FromNewerVersion };
```

```csharp
// src/IdleXIdle.Game/Game1.cs:767
SaveFile.SnapshotBeforeUpgrade(save.Version);
```

**There is no `if (save.Version < 2)` / `< 3` migration branch anywhere.** A version bump buys exactly two things: (a) an older build refuses a newer file instead of mis-reading it (and `SaveStore.LocksSaving` at `SaveStore.cs:47-48` then latches saving OFF for the session); (b) `SaveStore.SnapshotBeforeUpgrade` (`SaveStore.cs:172-189`) copies the file aside once as `save.pre-v<CurrentVersion>-<time>.json` before the first write of the new format. Note its guard: `if (fileVersion >= SaveGame.CurrentVersion) return null;` — **if the version is not bumped, no snapshot is taken.**

The version history comment (`SaveGame.cs:21-26`) states what the bumps were for, and both are refusal-only:

```csharp
// Version history: 2 = the item-system redesign (BaseType "Gem", nested SavedItem.Gems, and
// TraitOverride's "NONE" sentinel). An older build's strict Enum.Parse would crash on "Gem", so
// the bump turns that crash into the designed FromNewerVersion refusal.
// 3 = SkillId becomes a woven skill's persisted identity (2026-08-31). Source/Form are still
// written as a legacy echo for now; an older build reading a v3 file must refuse it as
// FromNewerVersion rather than mis-resolve the build, which is what this bump buys.
```

(The v3 comment is now **stale**: it says "Source/Form are still written as a legacy echo". `Game1.cs:1047` writes only `SkillId, Source, VowId, Passive` — `Form` is not written, and `legacy_full_save_migration_test.cs:89` asserts `Assert.DoesNotContain("\"Form\"", json2)`. Believe the code.)

### Every actual migration is additive-with-defaults + repair-at-restore

The pattern is stated field by field in the doc-comments: a new field gets a default that means "what an old save meant", and where the default is not enough, a named restore function repairs it. `System.Text.Json` skips unknown members, so **removed** fields simply vanish. Eleven live migrations, all version-free:

**(a) Form → SkillId (the biggest one).** `LegacySkillForm` (`Persistence/LegacySkillForm.cs`) plus a FROZEN slot-kind walk, invoked from `PlayerLoadout.Restore`:
```csharp
// src/IdleXIdle.Core/Builds/PlayerLoadout.cs:294-313
var kinds = LegacySkillForm.SlotKinds(
    rows.Select(r => (r.Form, r.Passive, r.SkillId)).ToList(), SkillCapacity);
...
if (SkillCatalogue.Find(id) is { } def) { /* v3 row: the id IS the identity */ }
// A pre-v3 row: the Form-era identity, understood ONE more time.
if (sourceOk && LegacySkillForm.Resolve(form, kinds[i]) is { } migrated
    && SkillCatalogue.Find(migrated) is { } def2)
    _skills.Add(new SkillChoice(source, vow, passive ?? !def2.TakesABeat, def2.Id));
```

**(b) The banked roster, seeded from frozen old gates** — `SaveGame.cs:719-726`:
```csharp
var banked = save.UnlockedCharacters.Count > 0
    ? save.UnlockedCharacters
    : Characters.LegacyUnlocks.Seed(
        save.ConqueredRegions,
        save.QuestsDone,
        save.RegionFarms.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.Max(f => f.BestDepth)),
        save.RunsWithVowKept);
state.Restore(save.ActiveCharacterId, save.QuestsDone, banked);
```

**(c) `LearnedSkills` seeded from `MasteryTaken`** — `MasteryTree.cs:308-314`, the closest precedent for what §95 must undo:
```csharp
public IReadOnlySet<string> LearnedSkills()
{
    var set = new HashSet<string>(_learned, StringComparer.Ordinal);
    foreach (var n in _taken.Select(MasteryCatalog.ById))
        if (n?.GrantsSkillId is { } s) set.Add(s);
    return set;
}
```

**(d) `BossesFelled` seeded from `ChestsOpened`** — `Game1.cs:840`: `_bossesFelled = save.BossesFelled > 0 ? save.BossesFelled : save.ChestsOpened;`

**(e) `FreeSocketUsed` inferred from the bag** — `SaveGame.cs:737`: `return save.FreeSocketUsed || save.Inventory.Any(i => i.Gems.Count > 0);`

**(f) Single-region mastery folded into the home region** — `SaveGame.cs:769-772`.

**(g) `ChestKeepSlot` → `ChestKeepSlots`** — `Game1.cs:871-872`.

**(h) The `"NONE"` sentinel** — `SaveGame.cs:608` writes `"NONE"` for a plain item, `:647-649` reads null (pre-redesign) as `GearTraits.LegacyDerivedTrait(...)` and `"NONE"` as genuinely plain. This is the one migration that needed a *value* sentinel because "absent" already meant something else.

**(i) `RunTilt` defaulting to 1f not 0** — `SaveGame.cs:305` with a comment explaining that a 0 default would silently make every held chest worthless.

**(j) Renamed variations/reinforcements** — `SkillProgress.cs:155-171`, two tiny frozen tables (`THIRST→SIPHON`, `GREEDY→PUMP`) applied in `Restore` (`:207,:213`), with the rule that an unknown name returns the LEVEL unspent rather than losing it (`:201-204`).

**(k) Retired charters folded** — `SaveGame.cs:686`: `while (hunter.SpendCharter(Charter.Merge)) hunter.AddCharter(Charter.Salvage);`

### Loading is lenient by design, and never throws

`Deserialize` (`:501-532`) catches `JsonException` → `LoadFailure.Corrupt`, never crashes. Every catalogue name is parsed with `Enum.TryParse` and dropped if unknown: items (`CanRestore :627`), gems (`:655`), region farms (`:762`), charters (`:679`), warren facilities (`:780`), roster ids (`CharacterState.cs:118`), mastery nodes (`MasteryTree.cs:183`), learned skills (`MasteryTree.cs:220`), dust nodes (`MemoryDust.cs:200`), skill-progress rows (`SkillProgress.cs:199`). Two of these guards exist because an unknown name once crashed the game **before the window opened** (`SaveGame.cs:621-626` and `:744-751`) — a load-path failure no screenshot fixture can reach.

### 3. Which fields belong to: mastery access, trait tree state, trait points, vows, keystones, skill progression, character state, structural unlocks.

Headline: **five of these eight own no dedicated save field at all.** Trait points, vow knowledge, keystone knowledge, keystone-socket capacity and every structural unlock are all DERIVED from one list — `MemoryDustUnlocks`. That single `List<string>` is the whole load-bearing surface for four of the brief's six goals.

### MASTERY ACCESS
| Field | Line | Role |
|---|---|---|
| `MasteryTaken` | `SaveGame.cs:170` | Nodes taken → `MasteryTree.RestoreTaken` (`MasteryTree.cs:177-184`) |
| `LearnedSkills` | `SaveGame.cs:177` | **THE permanent-access latch.** "Skills learned FOR GOOD (D7). Respec never removes one." → `RestoreLearned` (`MasteryTree.cs:215-221`). This is the field brief §21 asks you to audit and §95 asks you to stop honouring |
| `ActiveCharacterId` | `SaveGame.cs:106` | Indirect but real: `Game1.cs:4326` `_mastery.LearnSkill(_characters.Active.StartingSkillId)` runs **every frame**, so every champion you have ever been has permanently written its birth skill into `LearnedSkills` |
| `MasteryEarned` | `SaveGame.cs:188` | Point budget input (actually deepest-ever wave — see Q1 #41) |
| `HighestMasteryAwarded` | `SaveGame.cs:80` | Dust-milestone latch (region mastery, not skill access) |
| `MasteryZoom/PanX/PanY` | `:200-202` | Camera only |

The live gate is `BuildComposer.cs:138-156`: `taughtSkills = mastery.LearnedSkills()` ∪ `character.StartingSkillId`, then `if (!taughtSkills.Contains(def.Id)) continue;` — described in the code as "**THE GATE, AND IT IS UNCONDITIONAL**".

### TRAIT TREE STATE
| Field | Line | Role |
|---|---|---|
| `MemoryDustUnlocks` | `SaveGame.cs:65` | Owned node ids of the 51-node `MemoryDustTree.Catalog` (`MemoryDust.cs:278-541`, 51 confirmed by `grep -c 'Id = "'`). Restored at `Game1.cs:770` |
| `MemoryDust` | `SaveGame.cs:64` | **A DIFFERENT CURRENCY.** The Dust wallet — a looted material that buys Warren facility upgrades and checkpoints. `MemoryDust.cs:121-139` is explicit: "TRAIT POINTS. What this tree spends — and it is NOT Memory Dust." Deleting the tree must NOT delete this field |

The tree's five roads (`MemoryDust.cs:29-45`): `Spine` (structural), `Ruin`, `Aegis`, `Avarice`, `Artifice`.

### TRAIT POINTS
**This does not exist as a save field.** Trait points are derived every frame and never persisted. `MemoryDust.cs:136-138`: "Derived by the host every frame from progress … there is nothing to persist but which nodes were bought."

Chain: `Game1.cs:4444` `_dust.SetEarned(TraitPointsEarned())` → `Game1.cs:6841` `Career.TraitPointsEarned(_world)` →
```csharp
// src/IdleXIdle.Core/Progression/Career.cs:56-62
var total = world.ConqueredIds.Count + 2 * world.PeakCorruptionTier;
foreach (var def in Regions.All) total += (int)world.RegionFarm(def.Id).MasteryLevel;
```
Spent = `MemoryDust.cs:142` sum of owned node costs; Available = `:144` `Earned - Spent`.

So the persisted **inputs** to trait points are: `ConqueredRegions` (`:83`), `CorruptionPeak` (`:101`), and `RegionFarms[].MasteryPoints` (`:389`). The design budget is documented at `Career.cs:52-54` as 6 + 10 + 18 = **34** points against a cheapest terminal path of 31.

### VOWS
| Where | Line | Role |
|---|---|---|
| `SavedSkill.VowId` | `SaveGame.cs:327` | The vow SWORN on each woven slot (per slot, not per character) |
| `RunsWithVowKept` | `SaveGame.cs:133` | Latched event counter; feeds THE OATHBOUND's old gate (`LegacyUnlocks.cs:76`) |
| `MemoryDustUnlocks` (derived) | `DustEffects.cs:102-126` | **Vow KNOWLEDGE has no field.** Five nodes teach 13 vows: `vow_study_1`→2, `vow_study_2`→2, `vow_study_3`→2, `vow_binding`→3, `vow_sacrifice`→4 = 13, exactly the size of `Vows.Catalog` (13, `grep -c 'Id = "' Vows.cs`) |
| `MemoryDustUnlocks` (derived) | `DustEffects.cs:226-230` | `artifice_vows` → `VowPowerMultiplier` 1.25× |

Two enforcement points read this derived set: `BuildComposer.cs:148` (an untaught vow composes as no vow) and `Game1.cs:800-802` (an untaught vow is stripped from the slot on load).

### KEYSTONES
| Where | Line | Role |
|---|---|---|
| `SocketedKeystoneIds` | `SaveGame.cs:156` | Which are WORN |
| `MemoryDustUnlocks` (derived) | `DustEffects.cs:55-63` | Which are LEARNED — `LearnedKeystones` reads `u.GrantsKeystone` off owned nodes. **19 nodes grant a keystone** (`grep -c 'GrantsKeystone = '` → 19) |
| `MemoryDustUnlocks` (derived) | `DustEffects.cs:205-209` | Socket CAPACITY: `1 + socket_2 + socket_3`. Applied at `Game1.cs:786,4334`; `PlayerLoadout.Restore` truncates worn keystones to it (`PlayerLoadout.cs:331-335`) |

### SKILL PROGRESSION
| Field | Line | Role |
|---|---|---|
| `SkillProgress` (`List<SavedSkillProgress>`) | `SaveGame.cs:167` | `{SkillId, Uses, Variation, Reinforcements}`. **Keyed by skill id, not by slot** (`:161-166`) — unequipping keeps everything earned. `SkillProgress.Restore` (`SkillProgress.cs:190-218`) restores the level FIRST, then decides what it was spent on |
| `WovenSkills` (`List<SavedSkill>`) | `SaveGame.cs:153` | The build: `{SkillId, Source, Form(legacy), VowId, Passive}` |
| `LearnedSkills` | `SaveGame.cs:177` | Access, listed above — but note it is what makes progression *usable* |

### CHARACTER STATE
| Field | Line | Role |
|---|---|---|
| `ActiveCharacterId` | `SaveGame.cs:106` | Who you are |
| `UnlockedCharacters` | `SaveGame.cs:120` | Banked earned roster; **empty = pre-2026-08-26 save**, seeded from `LegacyUnlocks.Seed` |
| `QuestsDone` | `SaveGame.cs:123` | Finished quest ids |
| `BossesFelled` | `SaveGame.cs:150` | THE MAGPIE's gate |
| `RunsWithVowKept` | `SaveGame.cs:133` | THE OATHBOUND's old gate |
| `ConqueredRegions`, `RegionFarms[].BestDepth` | `:83`, `:392` | Legacy-seed inputs |

**Nothing in the save is keyed BY character.** There is one global `WovenSkills`, one `SkillProgress`, one `SocketedKeystoneIds`, one `MemoryDustUnlocks`. `CharacterState.cs:12-16` states the design: "Switching character changes no progress whatsoever." A per-character trait loadout (brief §26) or per-character signature progression (§8) has **no existing shape to extend** — it is new save structure, not a new field on an existing one.

### STRUCTURAL UNLOCKS
**Zero dedicated fields.** Every one is `tree.Owns("id")` against `MemoryDustUnlocks`, resolved in `DustEffects`:

| Capability | Node id(s) | Resolver | Applied at |
|---|---|---|---|
| Skill slots (4→5) | `weave_5` | `DustEffects.cs:212-216` | `Game1.cs:785`, `ApplySkillCapacity` |
| Keystone sockets (1→3) | `socket_2`, `socket_3` | `:205-209` | `Game1.cs:786,4334` |
| Vow teaching | `vow_study_1/2/3`, `vow_binding`, `vow_sacrifice` | `:102-126` | `BuildComposer.cs:148` |
| Vow power +25% | `artifice_vows` | `:226-230` → `TreeShape :233-238` | `PlayerLoadout.ToBuild` |
| Auto-sell filter | `filter_common`, `filter_uncommon` | `:144-150` | Forge/loot path |
| Auto-merge | `auto_merge` | `:155-159` | post-run |
| Forge/auto-sell access gates | `ledger`, `forge_insight` | wired by the requirement graph only (`:248-252`) — their old QoL accessors had zero callers and were deleted in P7 |
| Salvage +15% | `efficient_forge` | `:85-90` | dismantle |
| Region mastery +5%/node | `recall_1..4` | `:68-75` | `RegionAutomation.RecordActiveKill` |
| Attribute mods | `ruin_edge_*`, `aegis_skin_*`, `avarice_purse_*`, `artifice_hands_*` (12 nodes) | `TreeMods :42-46` | build |
| Cosmetic | `attunement` | `TreeComplete :173-177` | TRAITS subtitle |

SCREEN-level unlocks are separate and derived from play facts, not from the tree — `Unlocks.cs:26-32` `UnlockFacts`, with `Activity.Traits => f.TraitPointsEarned >= 1` (`Unlocks.cs:159`) and `Unlocks.cs:47-52` "**Derived, never stored.**" `Onboarding.cs:107,132,142,378,588-589` also carries a `TraitPoints` tour step and a "YOU HAVE N TRAIT POINTS" hint.

### 4. What LegacySkillForm and LegacyUnlocks exist for, and whether they are still live.

**Both exist, both are still live, and both are deliberately FROZEN — they are the project's two "read the old vocabulary once, then never again" tables.** Their shared doc-comment convention is explicit: `LegacySkillForm.cs:20-22` "These six rows are frozen history, like `LegacyUnlocks` — they must never change again, whatever the catalogue does"; `LegacyUnlocks.cs:18` "It must never change again; that is the point of it."

### `LegacySkillForm` — `src/IdleXIdle.Core/Persistence/LegacySkillForm.cs` (97 lines)

**What it is for.** Until save v3, a woven skill's only persisted identity was `(Form, Passive)`, round-tripped through a runtime `SkillCatalogue.Resolve(Form, bool)`. v3 made `SavedSkill.SkillId` the identity. This class is where the Form vocabulary is understood one last time — at restore only (`:11-15`). It was deliberately built as its own hard-coded table rather than a call into `SkillCatalogue`, precisely so the refactor could delete `SkillDef.LegacyForm` and `Resolve(Form,bool)` from the runtime without keeping them alive forever (`:18-22`).

**Three frozen assets:**
1. `Map` (`:27-36`) — 6 Form names → (active skill, passive skill): `Strike`→(hammer_blow, hammer_press), `Trap`→(snare_repay, snare_jaws), `Mark`→(sign_call, sign_brand), `Projectile`→(volley_spray, volley_weep), `Aura`→(field_pulse, field_mire), `Transformation`→(drain_drink, drain_wilt).
2. `NaturallyPassive` (`:42`) — `{Aura, Trap}`, the two Forms that never took a beat.
3. `SlotKinds` (`:67-96`) — **the composer's slot-kind budget walk frozen at the day the Form vocabulary retired**, including the hard-coded `activeBudget = (Math.Max(1, capacity) + 1) / 2;   // Build.ActiveSlotsFor, frozen` (`:71`) and the frozen passive-skill id set at `:58-59`.

**LIVE — and it runs on EVERY load, not only on legacy loads.** Three call sites:
- `PlayerLoadout.cs:294-295` — `LegacySkillForm.SlotKinds(...)` is called **unconditionally** for every restore. v3 rows take the `if (rows[i].SkillId is { } id)` branch inside it (`LegacySkillForm.cs:78-83`), so the frozen walk still runs over modern saves.
- `PlayerLoadout.cs:311` — `Resolve(form, kinds[i])` for pre-v3 rows.
- `VaultScreen.cs:1510-1511` — the SHARE-CODE inspect card. A v1 build code (`RHB1`) still carries the legacy `Source`×`Form` pair (`ShareCodes.cs:242-244` explicitly validates it), so this is a second, live, *non-save* consumer.

Test coverage: `skill_identity_migration_test.cs` (7 tests, incl. `test_the_current_save_version_is_three` at `:166`), `skill_slot_kinds_test.cs:58-62,441`, `slot_split_balance_test.cs:44,60,72-77`, `legacy_full_save_migration_test.cs`.

### `LegacyUnlocks` — `src/IdleXIdle.Core/Characters/LegacyUnlocks.cs` (91 lines)

**What it is for.** Before the tiered roster (2026-08-26) the unlocked champion set was never saved — it was derived every frame from conquest and finished quests. The tiered roster **tightened** gates (THE THORNWALL moved from the first region's conquest to the whole map), and a derived set under tightened rules would take champions back. So the set is banked now, and a save written before the bank existed is seeded from the rules that were true when it was written (`:10-19`).

**Contents:** `Before` (`:38-51`) — the complete pre-tier gate for all 10 champions: `seeker`=Start; `anvil`=Conquest(cinderworks); `chorus`=Conquest(umbral_reach); `metronome`=Conquest(marrow_wastes); `unbroken`=Conquest(still_archive); `tower`=Conquest(pale_choir); `quiver`=Quest(q_hollow_hunt); `thornwall`=Conquest(verdant_hollow); `oathbound`=Quest(q_first_vow); `magpie`=Conquest(cinderworks). Plus two retired quest constants — `HollowHuntId`/`HollowHuntDepth = 20` (`:29,:32`) and `FirstVowId` (`:35`) — re-evaluated from the save's own facts (`OldQuestMet`, `:72-78`) so a player who met the old demand but saved before the quest was latched is still counted.

**LIVE.** One call site, on the main load path:
```csharp
// src/IdleXIdle.Core/Persistence/SaveGame.cs:719-725 (SaveSystem.RestoreCharacters)
var banked = save.UnlockedCharacters.Count > 0
    ? save.UnlockedCharacters
    : Characters.LegacyUnlocks.Seed(save.ConqueredRegions, save.QuestsDone,
        save.RegionFarms.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.Max(f => f.BestDepth)),
        save.RunsWithVowKept);
```
Called from `Game1.cs:836` on every load. It fires whenever `UnlockedCharacters` is empty — and `CharacterState.SaveUnlocked()` always writes at least the starter (`CharacterState.cs:129-130`), so "empty" unambiguously means "old save". Pinned by `unlocked_characters_test.cs` (8 tests, incl. `test_the_legacy_table_covers_the_whole_roster_and_is_the_pre_tier_roster` at `:150` which asserts every roster champion has a legacy gate — **so adding a character to `CharacterRoster` fails that test unless a matching legacy gate is added**).

### 5. How save round-tripping is tested today, and where those tests live.

### Where the tests live

`tests/unit/IdleXIdle.Core.Tests/Persistence/` — 13 files, ~1,565 lines, **59 test methods**. Plus save-touching tests in five other directories and one integration test.

| File | Lines | Tests | What it pins |
|---|---|---|---|
| `SaveSystemTests.cs` | 577 | 23 | The core suite: full round-trip, retired-creature-field compat, checkpoint, dedup, item element, reforge, chests, build, camera, hunter stats, missing/corrupt/newer failures, offline clamp, dust+unlocks, **byte-for-byte stability**, region farms, legacy single-region, chest counters |
| `save_store_test.cs` | 196 | 9 (+Dispose) | Disk half: quarantine, same-second collision, backup rotation, newer-version lock, delete, missing dir, backup recovery, pre-reset preservation |
| `save_armour_test.cs` | 221 | 9 (+Dispose) | **The never-throw guarantees**: unknown BaseType dropped, unknown gem dropped, out-of-range rarity clamped, unknown region id dropped, conquest/corruption restore, single-region fold, `SnapshotBeforeUpgrade` once/never/missing |
| `share_codes_test.cs` | 242 | 6 | Item/build/feedback code round-trips, hostile payloads never throw, checksum tampering, fail-loudly |
| `skill_identity_migration_test.cs` | 172 | 7 | The v3 Form→SkillId migration at 6 angles; `test_the_current_save_version_is_three` |
| `unlocked_characters_test.cs` | 159 | 8 | `LegacyUnlocks` seeding, bank round-trip, empty-means-old sentinel, dropped ids, whole-roster coverage |
| `legacy_full_save_migration_test.cs` | 100 | 1 | **The end-to-end fixture** — a hand-written v2 Form-era JSON loads → migrates → plays → saves → reloads stable |
| `legacy_trait_migration_test.cs` | 74 | 3 | The `"NONE"` gear-trait sentinel, all three cases |
| `gift_chest_save_test.cs` | 56 | 2 | Gift key round-trip; pre-gift saves load as ordinary |
| `onboarding_save_test.cs` | 49 | 2 | `IntroSeen` + `ExplainedScreens` |
| `completed_sets_save_test.cs` | 47 | 2 | `CompletedSets` |
| `dismissed_guide_test.cs` | 45 | 2 | `DismissedGuideRungs` |
| `build_stamp_test.cs` | 27 | 1 | `BuildStamp` never empty |

**Elsewhere:** `Builds/save_migration_test.cs` (8 tests — the *content* rework: deleted reinforcement/variation returns the level unspent, `THIRST→SIPHON`, `GREEDY→PUMP`, deleted skill row dropped); `Builds/loadout_duplicate_skill_test.cs:171-204` (the only place `SkillProgress` round-trips through a real `SaveGame`); `Builds/mastery_learned_permanence_test.cs` (6 tests — `LearnedSkills` at the `MasteryTree` API level); `Economy/GearTests.cs:140-147`, `Economy/first_gem_free_test.cs`, `Economy/item_classes_test.cs:288-320`, `Economy/gem_craft_test.cs`, `Expeditions/chest_filter_test.cs:82`; `tests/integration/IdleXIdle.Integration.Tests/FullLoopTests.cs:82-91` (loot→forge→train→master→save→return, two hours later).

**Outside xUnit:** `tools/check_boot.sh` — a ~40s real-binary boot check (`RH_BOOTCHECK`) that loads the REAL save, proves the boot-check mode did not write it (sha256 before/after, `:38,:79-84`), then runs a fresh directory via `RH_SAVE_DIR`, proves ten seconds of play produced a `save.json` (`:122`), and proves a second run reads it back (`:131`). This exists because the load path is unreachable by the screenshot rig (`Game1.cs:823-830`) and the game has shipped two boot crashes there.

### The three round-trip shapes used

1. **Serialize→Deserialize→assert** (most tests): `SaveSystem.Deserialize(SaveSystem.Serialize(save), Now)`.
2. **Byte-for-byte stability** — `SaveSystemTests.cs:487-495`: `Assert.Equal(SaveSystem.Serialize(save), SaveSystem.Serialize(SaveSystem.Deserialize(once, Now).Save!))`.
3. **Hand-written legacy JSON** — the only way to test a shape current code can no longer produce (`SaveSystemTests.cs:72-113`, `legacy_full_save_migration_test.cs:22-43`). The comment at `SaveSystemTests.cs:66` states the rule: "This test writes the legacy JSON by hand so no current code has to be able to produce it."

### COVERAGE GAPS — fields with NO round-trip test (grep across `tests/`)

| Field | Test hits | Note |
|---|---|---|
| `ChestKeepSlots` | **0** | `chest_filter_test.cs:82` tests only the LEGACY `ChestKeepSlot`. The `ChestKeepSlot`→`ChestKeepSlots` migration at `Game1.cs:871-872` is host code and untested |
| `TraderWeekStamp` | **0** | |
| `TraderBoughtSlots` | **0** | |
| `WornHelmId`/`ChestId`/`GlovesId`/`BootsId`/`RingId` | **0** | Only `WornWeaponId` is ever asserted (`GearTests.cs:140`, `SaveSystemTests.cs:139`) — 5 of the 8 gear slots have never been round-tripped |
| `LearnedSkills` | 10, but **0 through JSON** | All 10 hits are `MasteryTree.LearnedSkills()` API calls in `mastery_learned_permanence_test.cs` / `skill_slot_kinds_test.cs`. The `SaveGame.LearnedSkills` list itself is never serialized in a test |
| `BossesFelled` seeding | 7 hits, **0 for the seed** | `Game1.cs:840` `save.BossesFelled > 0 ? … : save.ChestsOpened` is host code with no test |
| `HighestMasteryAwarded` | 1 | `SaveSystemTests.cs:476` only |
| `CorruptionPeak` | 1 | |

The pattern: **everything in `SaveSystem.Capture` is well covered; the 38 fields attached in `Game1.Save()`'s `with` block are covered only where somebody wrote a bespoke test**, because `Game1` has no test project (`SaveStore.cs:14-16` says so outright: "the Game assembly has no test project").

**Files this area will change**

- src/IdleXIdle.Core/Persistence/SaveGame.cs — the format itself (58 fields + 6 nested DTOs + SaveSystem.Capture/Deserialize/Restore*). Every new trait/signature/discovery field lands here, and the CurrentVersion const (:17) is the only thing that triggers a pre-upgrade snapshot.
- src/IdleXIdle.Core/Persistence/SaveStore.cs — SnapshotBeforeUpgrade (:172-189) keys off SaveGame.CurrentVersion and only fires when fileVersion < CurrentVersion. No code change needed, but its behaviour changes with a version bump; LocksSaving (:47) decides whether a refused save latches writing off.
- src/IdleXIdle.Core/Persistence/LegacySkillForm.cs — FROZEN. Must survive the refactor untouched; PlayerLoadout.Restore calls SlotKinds unconditionally on every load, so any change to skill-slot capacity semantics changes what the frozen walk is handed.
- src/IdleXIdle.Core/Characters/LegacyUnlocks.cs — FROZEN. Adding a champion to CharacterRoster fails unlocked_characters_test.cs:150 unless a legacy gate is added too.
- src/IdleXIdle.Core/Persistence/ShareCodes.cs — SharedBuild (:50-55) carries Skills/Keystones/Mastery only. A signature skill or a 3-trait loadout in a shared build needs the prefix digit bumped (Version=2, :41) and the validator at :238-254 extended.
- src/IdleXIdle.Core/Prestige/MemoryDust.cs — the 51-node catalogue (:278-541) that IS the trait tree being deleted, plus Restore (:195-201) which is the only reader of SaveGame.MemoryDustUnlocks.
- src/IdleXIdle.Core/Prestige/DustEffects.cs — every structural unlock, vow grant, keystone grant, socket count and skill-slot count resolves here from MemoryDustUnlocks. This is the single file the §53-§57 ownership moves must be read out of before the tree is dropped.
- src/IdleXIdle.Core/Prestige/TraitTreeLayout.cs and MemoryDustText.cs — tree layout/copy; die with the tree.
- src/IdleXIdle.Core/Builds/MasteryTree.cs — RestoreLearned/LearnedSkills (:215-221, :308-314) is the permanent-discovery latch §21/§95 targets, and LearnSkill (:230) is the birth-skill latch §94 must not steal XP from.
- src/IdleXIdle.Core/Builds/PlayerLoadout.cs — Restore (:275-336): the Form migration, the duplicate-skill collapse (:325-328), and the keystone truncation to KeystoneCapacity (:331-335). Any loadout-repair rule (§19) lands here.
- src/IdleXIdle.Core/Builds/BuildComposer.cs — the taughtSkills access gate (:138-156) and the untaught-vow gate (:148). §16/§18 change these two lines.
- src/IdleXIdle.Core/Builds/SkillProgress.cs — Restore (:190-218) and the two frozen rename tables (:155-171). The precedent for content migration: level permanent, purchase droppable.
- src/IdleXIdle.Core/Characters/CharacterState.cs — Restore/SaveUnlocked/SaveQuests (:107-130). A per-character trait loadout or signature has no home today; it would be new state on this class.
- src/IdleXIdle.Core/Characters/Character.cs (:126 StartingSkillId) and CharacterRoster.cs (10 champions, :52-217) — the current stand-in for a signature skill.
- src/IdleXIdle.Core/Progression/Career.cs — TraitPointsEarned (:56-62). Deleting trait points deletes this method and its callers.
- src/IdleXIdle.Core/Progression/Unlocks.cs — UnlockFacts.TraitPointsEarned (:71) and Activity.Traits => f.TraitPointsEarned >= 1 (:159). The TRAITS screen gate must be rewritten when the currency goes.
- src/IdleXIdle.Core/Progression/Onboarding.cs — TourTarget.TraitPoints (:107), TraitPointsFree (:142), the tour step (:378) and the 'YOU HAVE N TRAIT POINTS' hint (:588-589).
- src/IdleXIdle.Core/Automation/RegionAutomation.cs — reads DustEffects.MasteryRate for the recall_* nodes (:104 comment names Career.TraitPointsEarned).
- src/IdleXIdle.Game/Game1.cs — the two halves of the format: LoadOrStartFresh (:760-960, restores 37 fields) and Save() (:1011-1102, the `with` block that supplies 38 of the 58 fields). Also _dust wiring (:447, :770, :1165, :4444), TraitPointsEarned() (:6841), the birth-skill latch (:4326), the untaught-vow strip (:800-802) and FeedbackCode() (:1107-1135).
- src/IdleXIdle.Game/SaveFile.cs — the Game layer's only door to disk. AppDataFolderName (:25) must not change. Likely no edit needed.
- src/IdleXIdle.Game/TraitsScreen.cs — draws the tree from MemoryDustTree; §37-§40 replace it wholesale.
- src/IdleXIdle.Game/VaultScreen.cs:1510-1511 — the share-code inspect card, the second live LegacySkillForm consumer. Breaks silently if the class is deleted.
- src/IdleXIdle.Game/LoadoutScreen.cs (:254, :548), MasteryScreen.cs (:336, :1042-1357), GearScreen.cs (:148), HuntScreen.cs (:699), TrainingScreen.cs (:56) — every screen that holds a MemoryDustTree and must be re-pointed.
- tests/unit/IdleXIdle.Core.Tests/Persistence/ — all 13 files. SaveSystemTests.cs (the round-trip and byte-stability suite), save_armour_test.cs (the never-throw guarantees + SnapshotBeforeUpgrade), legacy_full_save_migration_test.cs (the end-to-end legacy fixture that a new version must be added to), unlocked_characters_test.cs:150 (fails on any roster change), skill_identity_migration_test.cs:166 (asserts CurrentVersion == 3 — fails the moment you bump it).
- tests/unit/IdleXIdle.Core.Tests/Builds/save_migration_test.cs, loadout_duplicate_skill_test.cs, mastery_learned_permanence_test.cs — the content-migration and permanent-learning precedents the new tests should mirror.
- tools/check_boot.sh — the only test that exercises the real load path against a real binary. Run it after any format change; the screenshot rig cannot reach this code (Game1.cs:823-830).

**Risks**

- THE TEN-SECOND WINDOW. Autosave fires every 10s (Game1.cs:655, :2792-2796) and unknown JSON members are silently skipped on read. The moment the new build autosaves, any field it dropped — MemoryDustUnlocks above all — is gone from disk. The ONLY undo is SaveStore.SnapshotBeforeUpgrade, and its guard is `if (fileVersion >= SaveGame.CurrentVersion) return null;` (SaveStore.cs:176). If the refactor does not bump CurrentVersion, NO snapshot is taken and the player's trait-tree state is unrecoverable ten seconds after they launch. Bumping the version is a data-safety requirement here, not bookkeeping.
- A VERSION BUMP IS ONE-WAY AND LOUD. Deserialize refuses any save with Version > CurrentVersion (SaveGame.cs:520) and SaveStore.LocksSaving (:47-48) then latches writing OFF for the whole session. A player who launches the new build once and then rolls back gets a game that refuses to save. Correct behaviour, but it means the migration must be right first time — there is no second attempt against the same file.
- THERE IS NO VERSION-DISPATCHED MIGRATION MACHINERY. save.Version is read in exactly two places and neither branches on it. Every migration to date is additive-with-defaults plus repair-at-restore. A refactor that DELETES concepts (trait tree, trait points) rather than adding them has no precedent in this codebase: the additive pattern cannot express 'read this field once, translate it, then never write it again'. The closest precedents are LegacyUnlocks and LegacySkillForm — both frozen read-once tables — and they are the shape the trait migration must copy.
- MemoryDustUnlocks IS FOUR SYSTEMS AT ONCE. One List<string> carries: keystone knowledge (19 nodes), vow knowledge (5 nodes → 13 vows), skill-slot capacity (weave_5), keystone-socket capacity (socket_2/socket_3), auto-sell (filter_common/filter_uncommon), auto-merge, forge access (ledger/forge_insight/efficient_forge), region-mastery rate (recall_1..4) and 12 attribute mods. §53-§57 move all of these to different owners. Delete the field before reading every one of those out and existing players lose entitlements they bought — the exact thing §58 forbids.
- MemoryDust IS NOT TRAIT POINTS. MemoryDust.cs:121-139 is explicit. Dust is a looted material spent on Warren facility upgrades and checkpoints; trait points are derived from conquest (Career.cs:56-62) and never persisted. §60 removes trait points. Removing SaveGame.MemoryDust with them would delete the Warren's currency.
- TRAIT POINTS ARE DERIVED, SO THERE IS NOTHING TO CONVERT. Career.TraitPointsEarned(world) = conquests + 2×peak corruption + Σ region mastery level. §60's 'evaluate a currency conversion' has no stored balance to convert — the number is recomputed from world state every frame. Whatever replaces it must either re-derive from the same three facts or be persisted for the first time.
- NOTHING IN THE SAVE IS PER-CHARACTER. One global WovenSkills, one SkillProgress, one SocketedKeystoneIds, one MemoryDustUnlocks; only ActiveCharacterId names a champion. CharacterState.cs:12-16 states the design promise. §26 (3 trait slots per character) and §8 (per-character signature progression) are NEW save structure, not new fields on existing structure, and CharacterState has no per-character container to hang them on.
- A NEW FIELD MUST BE ADDED IN TWO PLACES. SaveSystem.Capture supplies 20 fields; Game1.Save()'s `with` block (Game1.cs:1039-1099) supplies the other 38. A field added to the record but forgotten in the `with` block serializes at its default forever and every test that only round-trips a hand-built SaveGame still passes. This is the codebase's documented failure species (dormant, built, tested, green, never runs).
- 'EMPTY MEANS OLD' SENTINELS. UnlockedCharacters empty = pre-2026-08-26 save (SaveGame.cs:117, CharacterState.cs:129-130 guarantees a fresh save is never empty). WovenSkills empty = pre-solo save (Game1.cs:791). MasteryZoom 0 = never opened. Any new list field needs its own answer to 'what does empty mean', and any change that lets a modern save write an empty UnlockedCharacters re-triggers LegacyUnlocks seeding on a current player.
- LegacySkillForm.SlotKinds RUNS ON EVERY LOAD, not only legacy ones (PlayerLoadout.cs:294). Its frozen activeBudget = (capacity+1)/2 (LegacySkillForm.cs:71) is a copy of Build.ActiveSlotsFor as it stood on the freeze date. If §54's skill-slot model (2A+2P) changes the active/passive split, the frozen walk and the live walk diverge — which is by design for old rows, but the class is also on the modern path.
- THE PERMANENT-DISCOVERY LATCH HAS TWO SOURCES. LearnedSkills is fed by mastery road nodes (MasteryTree.Take :210) AND by every champion you have ever been (Game1.cs:4326 runs per frame). §95 says access must be recomputed from current Mastery allocation — but the birth-skill half of that set is character-owned and must survive, or §94's 'do not steal BLOW XP' is violated in the other direction. The two sources are indistinguishable inside the persisted List<string>.
- LearnedSkills HAS NO JSON ROUND-TRIP TEST. All 10 test hits are MasteryTree API calls. The field most central to §95 has never been serialized in a test.
- FIVE OF EIGHT GEAR SLOTS, ChestKeepSlots, TraderWeekStamp AND TraderBoughtSlots HAVE ZERO TEST HITS. The Game assembly has no test project (SaveStore.cs:14-16), so everything attached in Game1.Save() is only as covered as somebody's bespoke test. A refactor that touches the `with` block can break these silently.
- THE LOAD PATH IS UNREACHABLE BY THE SCREENSHOT RIG. Game1.cs:823-830 and :851-859 document two shipped boot crashes that occurred BEFORE THE WINDOW OPENED, on the second launch only, invisible to every capture — one from a strict Enum.Parse on an unknown BaseType, one from a KeyNotFoundException on a renamed region id. Any new restore code that dereferences a screen built in LoadContent, or parses a catalogue name strictly, repeats it. tools/check_boot.sh is the only guard.
- SHARE CODES ARE A SECOND, SEPARATE FORMAT. ShareCodes.Version = 2 (ShareCodes.cs:41), independent of SaveGame.CurrentVersion, and RHB1 codes still decode the legacy Source×Form pair (:242-244). SharedBuild carries no signature skill, no traits, no character id. VaultScreen.cs:1510 is a live LegacySkillForm consumer outside persistence entirely.
- STALE COMMENTS THAT DISAGREE WITH THE CODE. (a) SaveGame.cs:24-26 says Source/Form are 'still written as a legacy echo' — Form is not written (Game1.cs:1047; asserted absent by legacy_full_save_migration_test.cs:89). (b) SaveGame.cs:187 documents MasteryEarned as 'total mastery points earned over the whole game' — the code stores deepest-ever wave (Game1.cs:1086, :806). Both would mislead a migration author.
- MemoryDustTree's CONSTRUCTOR VALIDATES AND THROWS. MemoryDust.cs:111 calls Validate(), which throws InvalidOperationException on a missing prerequisite or a dependency cycle (:214, :232). Editing the 51-node catalogue mid-refactor into an inconsistent state crashes at construction — including inside Game1's field initialiser (Game1.cs:447), i.e. before the window opens.
- DustEffects.WiredIds IS A LIVENESS TEST FROM BOTH ENDS. DustEffects.cs:240-253 plus 'a test asserts every id in the catalog is reachable from a method here' (:22-25) and 'every keystone in Keystones is teachable by some node'. Removing nodes without removing their wires — or vice versa — fails the build. That is the guard against this project's named failure mode (six shipped, complete, tested, entirely uncalled systems), and it will fire during the trait rewrite.

## Skill architecture, loadout, progression (src/IdleXIdle.Core/Builds + every consumer)

### 1. The exact authoring shape of a skill: every field of SkillDef / the resolved skill, what Style / Kind / variation / reinforcement mean, and how a skill is authored end to end. Give one complete authored example verbatim.

### 1a. There is no data file. A skill is C# source.

The entire catalogue is a hand-written `List<SkillDef>` initialiser at `src/IdleXIdle.Core/Builds/SkillCatalogue.cs:347-752`. No JSON, no MGCB content, no external config. `SkillDef` is a `sealed record` with **64 positional parameters** (`SkillCatalogue.cs:211-300`) plus two computed members (`SkillCatalogue.cs:303`, `:306`).

### 1b. Every field of `SkillDef`, in declaration order

| # | Field | Type | Default | File:line | Meaning / who reads it |
|---|---|---|---|---|---|
| 1 | `Id` | string | — | `SkillCatalogue.cs:212` | Stable persisted key. `SkillCatalogue.ById`/`Find` (`:754`,`:758`), every save row, every screen. |
| 2 | `Name` | string | — | `:213` | Display word ("BLOW"). |
| 3 | `Style` | Style | — | `:213` | The ring identity. ONLY field mastery + affinity read (`StyleAffinity.Factor`, `Quests.cs:80`). |
| 4 | `Kind` | SkillKind | — | `:214` | Active / Field / Reaction. The fight's three-way fork, `SoloBattle.cs:1455-1459`. |
| 5 | `Effect` | SkillEffect | — | `:215` | Damage/Amplify/Heal. **Only `Amplify` is ever read** — `SoloBattle.cs:1608`, `:1722`, `:2066`. `Damage` and `Heal` are branched on nowhere; `drain_wilt` is tagged `Heal` and its base line is an attack-break. |
| 6 | `Line` | string | — | `:217` | Player-facing sentence. Read by `MasteryCatalog.cs:481`, `RosterScreen.cs:680`, LoadoutScreen. |
| 7 | `Beats` | int | — | `:218` | Beats between casts for an Active; 0 otherwise. `Build.BeatDemand` (`Build.cs:361-368`), `HuntScreen.RailCooldownMs`. |
| 8 | `IntervalMs` | int | — | `:219` | Field tick clock. `SoloBattle.cs:1473` (`ownTick`). |
| 9 | `On` | ReactionOn | — | `:220` | **Only `Bitten` is dispatched** (`SoloBattle.cs:2247`). `volley_weep` declares `ReactionOn.Kill` and the sim never reads it — WEEP fires off the `BleedOnKillFraction` dial scanned on any kill at `SoloBattle.cs:1233-1246`. `LowHealth` and `WaveStart` (`SkillCatalogue.cs:96-97`) are used by no skill and read by nothing. |
| 10 | `Targets` | int | — | `:221` | Creatures reached; `int.MaxValue` = whole wave (`WholeWave`, `:332`). |
| 11 | `ClipKey` | string | — | `:222` | Champion animation clip. `HuntScreen.cs:1581` switch, `:3586`. |
| 12 | `FxKey` | string | — | `:223` | VFX strip. `HuntScreen.FxFor` (`:3660`, `:3662-3666`) tries `fx_{Character.Id}_{fxKey}_strip8_512` then `fx_{fxKey}`. |
| 13 | `Variations` | IReadOnlyList\<SkillVariation\> | — | `:224` | Exactly two per skill (gated by `skill_catalogue_test.cs:166`). |
| 14 | `BasePower` | float | 0 | `:231` | Per activation (Active/Reaction) or per SECOND (Field damage). Non-zero on 5 of 12: BLOW 500, SPRAY 215, DRINK 260, PULSE 140, MIRE 12. |
| 15 | `RearmMs` | int | 0 | `:234` | Reaction re-arm clock. |
| 16 | `DefenceBreakPerTick` | float | 0 | `:241` | PRESS |
| 17 | `DefenceBreakFloor` | float | 0 | `:242` | PRESS |
| 18 | `SlowFraction` | float | 0 | `:243` | MIRE |
| 19 | `AttackBreakPerTick` | float | 0 | `:244` | WILT |
| 20 | `AttackBreakFloor` | float | 0 | `:245` | WILT |
| 21 | `BleedOnKillFraction` | float | 0 | `:246` | WEEP |
| 22 | `PaysBackDamageTaken` | float | 0 | `:247` | REPAY |
| 23 | `PaybackWindowMs` | int | 0 | `:248` | REPAY/VENGEANCE |
| 24 | `DefenceIgnore` | bool | false | `:253` | HAMMER/FLATTEN |
| 25 | `ExecuteFraction` | float | 0 | `:254` | HAMMER/FINISH |
| 26 | `StunMs` | int | 0 | `:255` | HAMMER/PIN |
| 27 | `ShieldInsteadOfDamage` | bool | false | `:256` | SNARE/BANKED |
| 28 | `ShieldFromStoppedBite` | float | 0 | `:257` | SNARE/IRON/PLATING |
| 29 | `WaveStartShieldFraction` | float | 0 | `:258` | SNARE/BANKED/CARRIED |
| 30 | `ReflectFraction` | float | 0 | `:259` | SNARE/JAWS |
| 31 | `StopsWholeBite` | bool | false | `:260` | SNARE/IRON |
| 32 | `AmplifyPercent` | float | 0 | `:261` | SIGN |
| 33 | `AmplifyMs` | int | 0 | `:262` | SIGN/CALL |
| 34 | `AmplifyPerCast` | float | 0 | `:263` | SIGN/STEADY |
| 35 | `AmplifyCap` | float | 0 | `:264` | SIGN/STEADY |
| 36 | `AmplifyWholeWave` | bool | false | `:265` | SIGN/SPRAWL |
| 37 | `AmplifyDeepenPerTick` | float | 0 | `:266` | SIGN/ETCH |
| 38 | `AmplifyDeepenCap` | float | 0 | `:267` | SIGN/ETCH |
| 39 | `BleedRate` | float | 1 | `:268` | VOLLEY/TORRENT |
| 40 | `BleedCarriesWaves` | bool | false | `:269` | VOLLEY/CARRION |
| 41 | `DamagePerLivingEnemy` | float | 0 | `:270` | FIELD/THRONG |
| 42 | `SplitPool` | float | 0 | `:271` | FIELD/SHARE |
| 43 | `SplitMaxWays` | int | 0 | `:272` | FIELD/SHARE |
| 44 | `SlowDeepenPerTick` | float | 0 | `:273` | FIELD/NUMB |
| 45 | `SlowCeiling` | float | 0 | `:274` | FIELD/NUMB, TEEMING |
| 46 | `SlowPerEnemy` | float | 0 | `:275` | FIELD/TEEMING |
| 47 | `Lifesteal` | float | 0 | `:276` | DRAIN/DRINK |
| 48 | `DamagePerHealth` | float | 0 | `:277` | DRAIN/GLUT |
| 49 | `HealPerPulse` | float | 0 | `:278` | DRAIN/SUP |
| 50 | `FrontEnemyOnly` | bool | false | `:279` | DRAIN/SHRIVEL |
| 51 | `MinimumHits` | int | 0 | `:280` | VOLLEY/SPLAY |
| 52 | `HitsPerTarget` | int | 1 | `:281` | VOLLEY/CLUSTER |
| 53 | `DamageMultiplier` | float | 1 | `:286` | reinforcement: "hits harder" |
| 54 | `CooldownMultiplier` | float | 1 | `:287` | reinforcement: "comes back sooner" |
| 55 | `TargetsBonus` | int | 0 | `:288` | reinforcement: "reaches one more" |
| 56 | `ExecutesPerWave` | int | 1 | `:289` | HAMMER/TWICE |
| 57 | `CrowdedBeats` | int | 0 | `:290` | FIELD/CROWDED |
| 58 | `SwingIgnoresArmour` | bool | false | `:291` | HAMMER/TRAIL |
| 59 | `SwingLifesteal` | float | 0 | `:292` | DRAIN/TRICKLE |
| 60 | `ReflectGrowthPerBite` | float | 0 | `:293` | SNARE/MESH |
| 61 | `ReflectGrowthCap` | float | 0 | `:294` | SNARE/MESH |
| 62 | `BleedFromHits` | float | 0 | `:295` | VOLLEY/FLIGHT, RUPTURE, ONSET |
| 63 | `BreakSecondEnemy` | float | 0 | `:296` | DRAIN/HOLLOW |
| 64 | `Rules` | SkillRules? | null | `:300` | The 19-member escape hatch, below. |

Computed: `TakesABeat => Kind == SkillKind.Active` (`:303`); `Rule => Rules ?? SkillRules.None` (`:306`).

### 1c. `SkillRules` — 19 members, `SkillCatalogue.cs:162-185`

`OverkillCarry, TrailNextSwing, AmplifyHits, CritKeepsAmplifyCharge, AmplifyFrontFull, PaybackBelowHealth, PaybackBelowHealthBonus, ExtraBleedWhileBleeding, WeakenPerDeadEnemy, WeakenPerDeadCap, HealCeilingBonus, HealthScalingAbove, HealthScalingAboveBonus, AmplifyStartsPrimed, StunTimedToBite, SplitByHealth, RetargetOnDeath, BonusUnderHealth, BonusUnderHealthAmount`. It exists because `SkillDef` was already at the width "where a reader stops reading" (`:130-141`). `SkillRules.None` at `:184`.

### 1d. STYLE / KIND / VARIATION / REINFORCEMENT

- **STYLE** — `enum Style { Hammer, Snare, Sign, Volley, Field, Drain }`, `SkillCatalogue.cs:34-53`. **The enum ORDER IS THE RING** (`:21`): cyclic distance is affinity distance, three steps apart is opposite. Oppositions: HAMMER↔VOLLEY, SNARE↔FIELD, SIGN↔DRAIN. `RingDistance` `:770-775`, `Opposite` `:778-779`. Consumed by `StyleAffinity.Factor` (`StyleAffinity.cs:25-39`): distance 0 → ×2.00, 1 → ×1.15, 2 → ×0.75, 3 → ×0.45 (`:44-50`); a sworn Vow pulls one ring closer (`:34-39`). One skill belongs to exactly one Style, and a Style owns exactly one Active + one Passive (`skill_catalogue_test.cs:37`).
- **KIND** — `enum SkillKind { Active, Field, Reaction }`, `SkillCatalogue.cs:65-75`. **Only `Active` costs a beat** (`:67`, `:303`). That is the structural reason two passive slots are free. Fork at `SoloBattle.cs:1455-1459`.
- **VARIATION** — `sealed record SkillVariation(string Name, string Line, IReadOnlyList<Reinforcement> Reinforcements, Func<SkillDef,SkillDef>? Modify, Source Source = Source.Body)`, `SkillCatalogue.cs:120-125`. It is **a DELTA ON THE DEFINITION, not a branch in the fight loop** (`:114`). Two per skill, mutually exclusive, and the variation **owns the element** — `BuildComposer.cs:171` uses `variation?.Source ?? s.Source`.
- **REINFORCEMENT** — `sealed record Reinforcement(string Name, string Line, Func<SkillDef,SkillDef>? Modify)`, `SkillCatalogue.cs:105`. Three per variation, belonging to it and worthless to the other (`:101-104`). Applied in order after the variation at `BuildComposer.cs:162-168`.
- **Counts, computed**: 12 skills × 2 variations = **24 variations**; 24 × 3 = **72 reinforcements**. Verified mechanically: `grep -c '^\s*V("'` over lines 347-752 → 24; `grep -cE '^\s*\("[A-Z ]+",'` → 72. `python tools/check_skill_doc.py` (run read-only) prints *"the design doc's 12 skills, 24 variations and 72 reinforcements are the catalogue's"*, exit 0.

### 1e. Authoring a skill, end to end

1. **Add an entry** to `SkillCatalogue.All` (`:347`) in the exact shape `new("id", "NAME", Style.X, SkillKind.Y, SkillEffect.Z,` newline `"one-line effect",` then named args. The `V(...)` helper (`:343-345`) folds `(name, line, source, modify, (rName, rLine, rModify)×3)` into a `SkillVariation`.
2. **Give it a mastery node** so anyone can learn it: `Teaches(n, Branch, "road_x", "spec_y", "id")` in `MasteryCatalog.cs:458-469`; the factory is `:478-486`, priced `SkillRoadCost = 5` (`:114`).
3. **Art**: `assets/art/UI/icons/skills/icon_skill_<id>.png` (12 exist today, one per skill), plus a `ClipKey` case in `HuntScreen.cs:1581` and an `fx_<FxKey>` strip.
4. **Gates it must pass**: `skill_catalogue_test.cs` (unique ids `:69`, one active + one passive per style `:37`, two variations of three reinforcements `:166`, timing matches kind `:94`, names unique `:184`, names its art `:209`); `variation_liveness_test.cs:112` — all 24 variations must move damage-dealt or health-kept in a real fight; `reinforcement_liveness_test.cs:166` — all 72 likewise, plus `:195` every reinforcement declares a delta; `beat_cadence_test.cs`; `combat_balance_test.cs`.
5. **Docs**: `design/gdd/skill-slots-and-skill-trees.md` §6 is **generated** from the catalogue by `tools/check_skill_doc.py` (`--write` regenerates, bare run verifies, wired into `tools/check_all.sh`). The generator's regex at `check_skill_doc.py:47-52` pins the literal `new("id", "NAME", Style.X, SkillKind.Y, SkillEffect.W,\n "line"` shape — a skill authored in any other shape fails the check.

### 1f. One complete authored example, verbatim

`src/IdleXIdle.Core/Builds/SkillCatalogue.cs:383-411` (comments elided where marked):

```csharp
new("hammer_press", "PRESS", Style.Hammer, SkillKind.Field, SkillEffect.Damage,
    "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25.",
    Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
    ClipKey: "strike", FxKey: "press",
    Variations: new[]
    {
        V("CRUSHING", "The defence drop is 10 every 2s instead of 5, down to -50.",
            Source.Body,
            d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f },
            ("SETTLE",    "The floor falls from -50 to -90.",
             d => d with { DefenceBreakFloor = -90f }),
            ("SEIZE",     "The weight breaks the two front enemies instead of one.",
             d => d with { TargetsBonus = 1 }),
            ("UNDERMINE", "The weight works every 1s instead of every 2s.",
             d => d with { IntervalMs = 1000 })),
        V("PIN", "1s stun on the front enemy every 6s.",
            Source.Machine,
            d => d with { DefenceBreakPerTick = 0f, StunMs = 1000, IntervalMs = 6000 },
            ("HOLD",   "The stun is 1.5s instead of 1s.",
             d => d with { StunMs = 1500 }),
            ("BUCKLE", "Each stun strips 10 defence from the front enemy.",
             d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f }),
            // [comment elided]
            ("INTERCEPT", "The stun waits for the wave's next bite instead of its own clock.",
             d => d with { Rules = d.Rule with { StunTimedToBite = true } })),
    },
    DefenceBreakPerTick: 5f, DefenceBreakFloor: -25f),
```

### 1g. THE RESOLVED SKILL

`EquippedSkill` — `sealed record EquippedSkill(SkillDef Def, Source Source, Vow? Vow = null)` at `src/IdleXIdle.Core/Builds/Build.cs:217`, with one member `TakesABeat => Def.TakesABeat` (`:220`). Three fields, that is all.

Resolution happens exactly once, at `BuildComposer.cs:162-171`:

```csharp
var variation = progress?.VariationOf(def);
var resolved = def;
if (variation?.Modify is { } m) resolved = m(resolved);
if (variation is not null)
    foreach (var r in variation.Reinforcements)
        if (progress!.HasReinforcement(def.Id, r.Name) && r.Modify is { } rm)
            resolved = rm(resolved);
build.Equip(new EquippedSkill(resolved, variation?.Source ?? s.Source, vow));
```

The fight reads dials off that one record and never re-derives them (`Build.cs:204-210`).

### 2. All twelve shared skills: id, name, style, kind, one-line effect.

All twelve, from `src/IdleXIdle.Core/Builds/SkillCatalogue.cs:347-752`. `Line` is quoted verbatim. `Effect` and `BasePower` added because they are load-bearing and surprising.

| # | Id | Name | Style | Kind | Effect | BasePower | Cadence | One-line effect (`Line`, verbatim) | file:line |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `hammer_blow` | BLOW | Hammer | Active | Damage | 500 | 6 beats | "Heavy damage to one target." | `SkillCatalogue.cs:351-353` |
| 2 | `hammer_press` | PRESS | Hammer | Field | Damage | 0 | 2000 ms | "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25." | `:383-385` |
| 3 | `snare_repay` | REPAY | Snare | Active | Damage | 0 | 5 beats | "Deals 200% of the health damage you have taken since its last cast." | `:415-417` |
| 4 | `snare_jaws` | JAWS | Snare | Reaction | Damage | 0 | `On: Bitten`, rearm 3000 ms | "Every bite returns 50% of it to the enemy that bit you. Rearms every 3s." | `:447-449`, `:474` |
| 5 | `sign_call` | CALL | Sign | Active | **Amplify** | 0 | 5 beats | "All your damage +60% for 6s." | `:477-479` |
| 6 | `sign_brand` | BRAND | Sign | Field | **Amplify** | 0 | 2000 ms | "Your damage to the front enemy is +70%." | `:514-516` |
| 7 | `volley_spray` | SPRAY | Volley | Active | Damage | 215 | 4 beats (fastest in the game) | "Fires 5 arrows across the wave." | `:545-547` |
| 8 | `volley_weep` | WEEP | Volley | Reaction | Damage | 0 | `On: Kill` (**never dispatched**) | "When an enemy dies it leaves bleed on the wave worth 30% of its health." | `:584-586`, `:613` |
| 9 | `field_pulse` | PULSE | Field | Active | Damage | 140 | 5 beats | "Area damage to every enemy in the wave." | `:616-618` |
| 10 | `field_mire` | MIRE | Field | Field | Damage | 12 (per second) | 1000 ms | "Damages every enemy every 1s, and slows their attacks by 25%." | `:646-648` |
| 11 | `drain_drink` | DRINK | Drain | Active | **Heal** | 260 | 6 beats | "Heavy damage to one target; heals you back a share of it." | `:681-683` |
| 12 | `drain_wilt` | WILT | Drain | Field | **Heal** | 0 | 1000 ms | "Attack break: every enemy's damage drops 10% a pulse, down to -50%." | `:712-714` |

Six Actives (BLOW, REPAY, CALL, SPRAY, PULSE, DRINK), four Fields (PRESS, BRAND, MIRE, WILT), two Reactions (JAWS, WEEP). Six styles × (1 Active + 1 Passive) = 12, held by `skill_catalogue_test.cs:37`.

**Beat-demand arithmetic** (`Build.BeatDemand`, `Build.cs:361-368`, sum of 1/Beats over Actives): the fastest pair a player can weave is SPRAY (1/4 = 0.25) + PULSE (1/5 = 0.20) = **0.45**, matching the ceiling comment at `SkillCatalogue.cs:319-329`.

**Two flags on this table:**
- `Effect` is decorative except `Amplify`. `SkillEffect.Damage` and `SkillEffect.Heal` are compared against nowhere in the codebase (grep of `SkillEffect.` over src/ and tests/ returns only catalogue declarations, `SoloBattle.cs:1608`/`:1722`/`:2066`, and `build_glossary_test.cs:64` — all three sim reads test for `Amplify`). `drain_wilt` is `Heal` and its base line heals nothing; `drain_drink` is `Heal` and deals 260 base damage.
- `volley_weep`'s `On: ReactionOn.Kill` (`:586`) is never read. `SoloBattle.cs:2247` is the only `ReactionOn` dispatch and it tests `!= ReactionOn.Bitten`. WEEP works through the `BleedOnKillFraction` dial scanned across all equipped skills on every death at `SoloBattle.cs:1233-1246`. `ReactionOn.LowHealth` and `ReactionOn.WaveStart` (`SkillCatalogue.cs:96-97`) are declared and used by no skill and read by no code.

### 3. How skill progression works: what counts as a use, how levels are earned, where the level lives, and how it is saved.

### 3a. What counts as a use

**One cleared wave, per equipped skill.** Not per activation. `src/IdleXIdle.Core/Builds/SoloExpedition.cs:377-379`:

```csharp
if (Progress is { } prog)
    foreach (var sk in _build.Skills)
        prog.RecordWave(sk.Def.Id);
```

This sits **after** the `outcome != WaveOutcome.Cleared` early-return at `SoloExpedition.cs:370-374`, so a run that dies teaches nothing on the way out. The rationale is at `SkillProgress.cs:24-29`: an Active casts a few times a wave and a Field ticks a dozen, so counting activations would level a passive three times faster for the same job.

`RecordWave` itself, `SkillProgress.cs:73-77`:
```csharp
public void RecordWave(string skillId)
{
    if (string.IsNullOrEmpty(skillId)) return;
    _uses[skillId] = UsesOf(skillId) + 1;
}
```

**Offline does not level skills.** `Descent.Progress` is nullable and left null offline — `Descent.cs:69-72` ("which is also the offline setting: an unwatched descent pays gleam, not build depth"), corroborated by `OfflineHunt.cs:29`. Live wiring: `Game1.cs:4201` (`_expedition.Progress = _skillProgress`) and `HuntScreen.cs:869` (`_descent.Progress = Progress`), which forwards into the run at `Descent.cs:129`.

The only other `RecordWave` call sites are DevStart capture poses: `Game1.cs:2004`, `:2560`, `:2609`.

### 3b. How levels are earned — the arithmetic

`SkillProgress.cs:53`:
```csharp
public static int LevelFor(int uses) => Math.Min(MaxLevel, (int)MathF.Floor(MathF.Sqrt(Math.Max(0, uses)) / 2f));
```
`SkillProgress.cs:56`:
```csharp
public static int UsesForLevel(int level) => (2 * Math.Clamp(level, 0, MaxLevel)) * (2 * Math.Clamp(level, 0, MaxLevel));
```

`MaxLevel = 4` (`:43`). Computed both ways, and they are exact inverses:

| Level | `UsesForLevel(L) = (2L)²` | Check `LevelFor(uses) = floor(√uses / 2)` |
|---|---|---|
| 0 | (2·0)² = **0** | floor(√0/2) = 0 ✓ |
| 1 | (2·1)² = **4** | floor(√4/2) = floor(2/2) = 1 ✓ |
| 2 | (2·2)² = **16** | floor(√16/2) = floor(4/2) = 2 ✓ |
| 3 | (2·3)² = **36** | floor(√36/2) = floor(6/2) = 3 ✓ |
| 4 | (2·4)² = **64** | floor(√64/2) = floor(8/2) = 4 ✓ |

Four levels total, 64 cleared waves to max a single skill. Level 1 buys **the variation** (one of two); levels 2–4 buy **that variation's three reinforcements** — `SkillProgress.cs:14-15`.

Spend accounting: `SpentOn = (variation chosen ? 1 : 0) + count(taken reinforcements)` (`:66-67`); `FreeOn = max(0, LevelOf - SpentOn)` (`:70`).

Purchase rules:
- `ChooseVariation` (`:100-108`) — refused if one is already taken, if the name is not one of the skill's two, or if `FreeOn < 1`. A variation is a **commitment**, not a toggle (`:95-99`).
- `TakeReinforcement` (`:111-119`) — the variation must come first, the name must belong to **that** variation, `FreeOn ≥ 1`.
- `Respec(skillId)` (`:122-126`) — free, removes the variation and all reinforcements, **keeps every earned level**.

Call sites: `LoadoutScreen.cs:893` (variation), `:903` (reinforcement), `:771` (respec). Dev poses at `Game1.cs:2005`, `:2563`, `:2565`.

### 3c. Where the level lives

`SkillProgress` (`SkillProgress.cs:31-219`), three private dictionaries keyed by **skill id**, never by slot and never by character:
```csharp
private readonly Dictionary<string, int> _uses = new();                  // :34
private readonly Dictionary<string, string> _variation = new();          // :37
private readonly Dictionary<string, HashSet<string>> _taken = new();     // :40
```

One instance for the whole account: `Game1.cs:456` `private SkillProgress _skillProgress = new();`, reset only in `ResetAll` at `:1166`. It is **injected** into a run rather than owned by it (`SoloExpedition.cs:155-162`) precisely so a run's end does not erase it.

Consequence: **unequipping keeps everything, and switching characters keeps everything**, because nothing in the key is a slot or a character (`SkillProgress.cs:19-23`).

Second consumer: the quest layer. `SkillProgress.UsesBySkill()` (`:61`) → `Game1.cs:6827` → `Career.QuestSnapshot` (`Career.cs:44`) → `Quests.cs:80`, where `QuestGoal.WavesWithStyle` sums uses across every skill of a Style.

### 3d. How it is saved

**Shape.** `SavedSkillProgress` — `SaveGame.cs:800-810`: `{ required string SkillId; int Uses; string? Variation; List<string> Reinforcements; }`. Held as `SaveGame.SkillProgress` (`SaveGame.cs:167`), a flat account-wide list.

**Write.** `SkillProgress.ToSave()` (`:179-188`) unions the three dictionaries' key sets, orders ordinally, and emits `(id, uses, variation, taken)`. Host: `Game1.cs:1050-1057`.

**Read.** `SkillProgress.Restore(rows)` (`:190-218`). The ordering inside it is the design:
1. `if (SkillCatalogue.Find(id) is not { } def) continue;` — a skill the catalogue no longer knows is **dropped whole** (`:199`), so a reused id cannot resurrect old purchases.
2. `if (uses > 0) _uses[id] = uses;` — **the level is restored FIRST, before anything below can fail** (`:204`). The comment at `:201-203` states the rule: uses and level are permanent; only what the level was *spent on* can be lost.
3. Variation name is mapped through `RenamedVariations` (`:155-159`, exactly one entry: `THIRST → SIPHON`); an unrecognised name `continue`s and the level comes back **unspent**.
4. Reinforcements mapped through `RenamedReinforcements` (`:167-171`, exactly one entry: `GREEDY → PUMP`), then filtered to the chosen variation's own names (`:211-215`).

Host restore: `Game1.cs:803-804`.

**Signature.** `SkillProgress.Signature` (`:173-177`) folds every choice into a string. The hunt folds it into its build stamp so buying a reinforcement mid-descent re-composes the fight at the next wave boundary — `:129-133` records that leaving it out was how "the whole variation layer shipped dormant (audit 2026-08-31, finding #2)".

**Tests**: `skill_progress_test.cs:32-232` (13 tests including `test_clearing_waves_levels_the_skills_that_were_equipped` `:182` and `test_a_skill_that_was_not_equipped_earns_nothing` `:221`); `save_migration_test.cs:27-72`.

### 3e. The separate, permanent thing: ACCESS

Access is **not** in `SkillProgress`. It is `MasteryTree._learned`, a `HashSet<string>` at `MasteryTree.cs:158`, latched permanently when a road node is taken (`Take`, `:210`) or via `LearnSkill` (`:230-233`), never removed by respec (`:155-157`, marked D7 2026-08-31). Persisted as `SaveGame.LearnedSkills` (`SaveGame.cs:177`), restored at `Game1.cs:804`. Read via `LearnedSkills()` (`:308-313`), which unions the latch with whatever the currently-taken roads teach. **This is the exact rule the refactor's §15/§16 deletes.**

### 4. PlayerLoadout: slot count and kinds (how many active / passive), the duplicate-skill rule (LAW 13 from the previous pass), and every method that mutates a slot.

### 4a. Slot count

| Thing | Value | file:line |
|---|---|---|
| `Build.SkillSlots` const | **4** | `Build.cs:249` |
| `PlayerLoadout.MaxSkills` | `= Build.SkillSlots` → 4 | `PlayerLoadout.cs:66` |
| `PlayerLoadout.SkillCapacity` (instance, host-set) | default 4 | `PlayerLoadout.cs:79` |
| `Build.KeystoneSlots` / `MaxKeystones` | 3 | `Build.cs:435`, `PlayerLoadout.cs:67` |
| `PlayerLoadout.KeystoneCapacity` | default 3 | `PlayerLoadout.cs:81` |

**`MaxSkills` is NOT a ceiling.** `AddSkill` bounds on `SkillCapacity` alone (`PlayerLoadout.cs:95`: `if (_skills.Count >= SkillCapacity) return -1;`). The comment at `:58-65` records why: the old `Math.Min(MaxSkills, SkillCapacity)` clamped the bought FIFTH WEAVE straight back to 4. `Restore` likewise takes `Take(SkillCapacity)` (`:286`), not `Take(MaxSkills)`. Keystones **are** still double-clamped: `Math.Min(MaxKeystones, KeystoneCapacity)` at `:225` and `:337`.

**A FIFTH SLOT IS LIVE TODAY.** `DustEffects.SkillSlots(tree) => 4 + (tree.Owns("weave_5") ? 1 : 0)` — `DustEffects.cs:212-216`. The node exists: `MemoryDust.cs:314` `{ Id = "weave_5", Name = "FIFTH SKILL SLOT", Cost = 3, Requires = ["socket_2"] }`. Host resolution `Game1.ApplySkillCapacity` (`Game1.cs:3501-3506`):
```csharp
var gate = Unlocks.SkillSlots(GuideUnlockFacts());
var fromTree = DustEffects.SkillSlots(_dust);
var capacity = gate >= Build.SkillSlots ? fromTree : gate;
_loadout.SkillCapacity = Math.Max(capacity, _loadout.Skills.Count);
```

The onboarding gate `Unlocks.SkillSlots` (`Unlocks.cs:239-246`) walks **1 → 4**: start at 1; +1 at DeepestWave ≥ 5; +1 at DeepestWave ≥ 12; +1 at RegionsConquered ≥ 1. Copy per slot at `Unlocks.cs:254-261`. Once the gate reaches 4 the Dust tree can push to 5.

### 4b. Active / passive split

`Build.cs:340-343`:
```csharp
public static int ActiveSlotsFor(int totalSlots) => (Math.Max(1, totalSlots) + 1) / 2;
public static int PassiveSlotsFor(int totalSlots) => Math.Max(1, totalSlots) - ActiveSlotsFor(totalSlots);
```

| Total slots | Active = ⌈n/2⌉ | Passive = n − active |
|---|---|---|
| 1 | (1+1)/2 = **1** | 0 |
| 2 | (2+1)/2 = **1** | 1 |
| 3 | (3+1)/2 = **2** | 1 |
| **4** | (4+1)/2 = **2** | **2** |
| 5 | (5+1)/2 = **3** | 2 |

Unlock order is active-passive-active-passive (`Build.cs:327-330`); the fifth slot falls to the **active** side, taking beat demand back toward 0.6 (`Build.cs:332-337`).

Set on the composed build at `BuildComposer.cs:125-126`. Enforced at `Build.Equip` (`Build.cs:404-409`). Slot kinds are decided by `BuildComposer.SlotKinds` (`:58-92`), a single walk read by both the sim and the screen (`:36-42`): a named skill brings its own kind (`:72-77`), otherwise the player's `Passive` choice, otherwise an unfilled active budget **spills** to passive rather than being dropped (`:82-84`). Screen mirrors: `LoadoutScreen.cs:353-354`, `HuntScreen.cs:3325-3326`.

Note `Build.cs:307-313`: the doc-comment says `ActiveCapacity` "is deliberately still four here" pending a stage 2b. **The code disagrees with the comment** — `BuildComposer.cs:125` sets `ActiveCapacity = Build.ActiveSlotsFor(slotCapacity)` unconditionally, so a real player's four-slot build gets 2/2 today. Believe the code.

### 4c. The duplicate-skill rule (LAW 13)

**Naming warning first.** "LAW 13" in the source is the **ui-polish** pass's numbering — `production/audit/ui-polish/PLAN.md:16` ("D3 — LAW 13 lives in Core") and `REPORT.md:9`. In the *current* brief, this rule is **§14** (`systems-refactor/BRIEF.md:415-431`) and **LAW 13 means "Automation belongs to Warren"** (`BRIEF.md:2615-2617`). The comments in the code will read as false against the new brief.

The rule — *the same SkillId may not occupy two slots* — is enforced in **four** places:

1. **`PlayerLoadout.SetSkill`, `PlayerLoadout.cs:178-185`** — the point of choice:
```csharp
public bool SetSkill(int slot, string skillId)
{
    if (!InRange(slot) || SkillCatalogue.Find(skillId) is not { } def) return false;
    var elsewhere = IndexOfSkill(def.Id);
    if (elsewhere >= 0 && elsewhere != slot) return false;
    _skills[slot] = _skills[slot] with { SkillId = def.Id, Passive = !def.TakesABeat };
    return true;
}
```
Re-setting a slot to the skill it already holds is a no-op returning true. The refusing slot keeps what it had. Rationale at `:170-176`: two slots holding one skill gave it two independent cooldowns (`Champion.ReadyAtBeat` is per slot) and recorded two uses per cleared wave.

2. **`PlayerLoadout.Restore`, `PlayerLoadout.cs:325-328`** — the save migration:
```csharp
var resolved = new HashSet<string>(StringComparer.Ordinal);
for (var i = 0; i < _skills.Count; i++)
    if (_skills[i].SkillId is { } id && !resolved.Add(id))
        _skills[i] = _skills[i] with { SkillId = null, Passive = null };
```
First occurrence wins (slot order is the sim's tie-break); the later one is **cleared to empty, not removed**, so slots behind keep position, Source and Vow. Runs after legacy Form resolution so two legacy AURA rows collapsing to one MIRE are caught too (`:316-324`).

3. **`Build.Equip`, `Build.cs:396-398`** — below UI level, in Core:
```csharp
if (_skills.Count >= SlotCapacity) return false;
foreach (var worn in _skills)
    if (worn.Def.Id == skill.Def.Id) return false;
```

4. **`LoadoutScreen.cs:877-884`** — the UI defers to the model rather than pre-judging:
```csharp
if (!Loadout.SetSkill(_slot, _pickSkillId))
{
    var where = Loadout.IndexOfSkill(_pickSkillId);
    _msg = where >= 0 ? $"ALREADY EQUIPPED IN SLOT {where + 1}." : "THAT SKILL CANNOT GO THERE.";
    _cue = "sfx_error";
```
Preview is also suppressed for a pick worn elsewhere — `LoadoutScreen.cs:1143`.

Support: `IndexOfSkill` (`PlayerLoadout.cs:188-193`), `HasSkill` (`:196`), `Build.Unequip(string)` removes by id (`Build.cs:414`).

Coverage: `tests/unit/IdleXIdle.Core.Tests/Builds/loadout_duplicate_skill_test.cs` — 13 tests at `:39, :54, :65, :75, :87, :95, :108, :131, :150, :167, :212, :229, :264`, including compose-level (`test_compose_never_equips_the_same_skill_twice_even_when_handed_two_picks`), use-recording (`:229`), full save round-trip (`:167`) and share-code decode (`:264`).

### 4d. Every method that mutates a slot

`PlayerLoadout` holds `private readonly List<SkillChoice> _skills` (`:43`) and `private readonly List<string> _keystoneIds` (`:44`). `SkillChoice` is `record SkillChoice(Source Source, string? VowId, bool? Passive = null, string? SkillId = null)` (`:40-41`).

| Method | Signature | file:line | What it mutates | Guards |
|---|---|---|---|---|
| `AddSkill` | `int AddSkill()` | `PlayerLoadout.cs:93-98` | appends an **empty** `SkillChoice(Source.Body, null)`; returns index or −1 | `_skills.Count >= SkillCapacity` |
| `RemoveSkill` | `void RemoveSkill(int slot)` | `:100-103` | `_skills.RemoveAt(slot)` — removes the slot entirely, shifting the rest | range only |
| `MoveSkill` | `bool MoveSkill(int from, int to)` | `:116-125` | reorders; **slot order is the sim's beat tie-break** (`:108-114`, `SoloBattle.ResolveWave`) | `InRange(from)`, `to` clamped, no-op if equal |
| `CycleSource` | `void CycleSource(int slot, int dir)` | `:127-131` | `with { Source = … }` | `InRange` |
| `CycleVow` | `void CycleVow(int slot, int dir, IReadOnlyList<Vow> knownVows)` | `:134-147` | `with { VowId = … }` across `[null] + knownVows` | `InRange`, null-check |
| `SetSource` | `void SetSource(int slot, Source source)` | `:160-164` | `with { Source = … }` | `InRange` |
| **`SetSkill`** | `bool SetSkill(int slot, string skillId)` | `:178-185` | `with { SkillId, Passive = !def.TakesABeat }` | `InRange`, id must exist, **LAW 13** |
| `SetVow` | `bool SetVow(int slot, string? vowId, IReadOnlyList<Vow> knownVows)` | `:206-213` | `with { VowId = … }` | `InRange`, vow must be studied |
| `Restore` | `void Restore(skills, keystoneIds)` | `:275-338` | **clears and rebuilds** `_skills` and `_keystoneIds` | `Take(SkillCapacity)`, `LegacySkillForm` migration, duplicate collapse |
| `Starter` | `static PlayerLoadout Starter()` | `:359-364` | builds a loadout with **one** slot, `hammer_blow`, `Source.Body`, `Passive: false` | — |
| `SkillCapacity` setter | `int { get; set; }` | `:79` | not a slot, but bounds `AddSkill` and `Restore` | — |
| `ToggleKeystone` | `bool ToggleKeystone(string id, IReadOnlyList<Keystone> learned)` | `:220-229` | keystone sockets, not skill slots | `Math.Min(MaxKeystones, KeystoneCapacity)`, must be taught |

Read-only: `Skills` (`:46`), `Signature` (`:53-55`), `KeystoneIds` (`:56`), `IndexOfSkill` (`:188`), `HasSkill` (`:196`), `HasKeystone` (`:217`), `SaveSkills` (`:272`), `EquippedDefs` (`:371-375`), `ToBuild` (`:253-268`).

On the resolved side, `Build` mutates via `Equip` (`Build.cs:393-411`) and `Unequip` (`:414`) only.

Host call sites (all of them): `Game1.cs:802, 1940-1941, 1968-1971, 1983-1984, 1996-1997, 2011-2012, 2541-2544` (mostly DevStart poses); `LoadoutScreen.cs:662, 689, 743, 749, 877, 909, 912, 1152, 1154`.

### 5. Whether anything today expresses OWNERSHIP of a skill by a character. If not, say so plainly.

### Plainly: NO. Nothing in the codebase expresses ownership of a skill by a character. The model is the opposite — every skill is account-wide, permanently, by construction.

Searches run over `src/` and `tests/` for `signature skill`, `SignatureSkill`, `ExclusiveTo`, `OwnerCharacter`, `CharacterId.*Skill`, `skill.*CharacterId` (case-insensitive): **zero hits**.

### What exists instead

**1. `Character.StartingSkillId` — a BIRTH skill, not an owned one.** `src/IdleXIdle.Core/Characters/Character.cs:126`, `public string? StartingSkillId { get; init; }`. Nullable, a plain `SkillCatalogue` id, with no exclusivity field beside it. Its whole job (`:119-125`) is that all twelve skills live behind mastery nodes, so without it a fresh champion could weave nothing.

**2. The birth skill is latched ACCOUNT-WIDE the moment you play the character.** `src/IdleXIdle.Game/Game1.cs:4326`:
```csharp
_mastery.LearnSkill(_characters.Active.StartingSkillId);
```
running every frame in the roster-refresh path, with the comment at `:4323-4325`: *"BIRTH SKILLS ARE LEARNED BY BEING SOMEONE (P9). Becoming a champion latches theirs into the permanent set (D7), so the roster's YOU KEEP SKILLS holds…"*. `MasteryTree.LearnSkill` (`MasteryTree.cs:230-233`) adds to `_learned`, which is never removed from and is persisted as `SaveGame.LearnedSkills` (`SaveGame.cs:177`). **So: play THE ANVIL once, and every other champion can weave PRESS forever.** That is the exact inverse of the brief's LAW 1 and LAW 2.

**3. The UI states this out loud.** `src/IdleXIdle.Game/RosterScreen.cs:747-748`:
```csharp
if (skill is not null && Mastery?.LearnedSkills().Contains(skill.Id) == true)
    _ui.TextRightBig(b, "ALREADY KNOWN", right, y, Met, UiTypography.Secondary);
```
The card's block comment at `:684-685` says the starting skill "is latched permanently into your skill set the moment you play them."

**4. The equip gate is a UNION, and it never subtracts.** `BuildComposer.cs:138-139`:
```csharp
var taughtSkills = mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
if (character?.StartingSkillId is { } born) taughtSkills.Add(born);
```
Then `:156` `if (!taughtSkills.Contains(def.Id)) continue;` — described at `:153-155` as "THE GATE, AND IT IS UNCONDITIONAL". Same union in the UI: `LoadoutScreen.KnownSkills()` at `:545-550`. No character is ever asked whether a skill is *theirs*; the only question is whether the account knows it.

**5. Two characters already share one skill, so there is no 1:1 mapping to promote.** `CharacterRoster.cs` — full birth-skill table:

| Character | Id | Class / Tier | StartingSkillId | file:line |
|---|---|---|---|---|
| THE SEEKER | `seeker` | Wanderer / First (starter, `StarterId` `:44`) | `hammer_blow` | `:51-53` |
| THE ANVIL | `anvil` | Warden / First | `hammer_press` | `:68-70` |
| THE CHORUS | `chorus` | Ranger / First | `field_mire` | `:83-85` |
| THE METRONOME | `metronome` | Mystic / First | `volley_spray` | `:97-99` |
| THE UNBROKEN | `unbroken` | Bulwark / First | `drain_wilt` | `:113-115` |
| THE FALLING TOWER | `tower` | Warden / Second | **`hammer_blow`** (duplicate) | `:127-129` |
| THE QUIVER | `quiver` | Mystic / Second | `volley_weep` | `:144-146` |
| THE THORNWALL | `thornwall` | Bulwark / Second | `snare_jaws` | `:166-168` |
| THE OATHBOUND | `oathbound` | Ranger / Second | `sign_call` | `:193-195` |
| THE MAGPIE | `magpie` | Wanderer / Second | `snare_repay` | `:216-218` |

Ten characters, ten `StartingSkillId`s, but only **nine distinct skills** — SEEKER and THE FALLING TOWER both name `hammer_blow`. Two of the twelve (`sign_brand`, `field_pulse`) are nobody's birth skill. `champion_starting_skill_test.cs:71-72` asserts a mix of Active and Passive across the roster but **does not** assert uniqueness.

**6. Nothing else in `Character` is skill-shaped.** `Character.cs:86-140` holds `Id, Name, Blurb, Lean, Class, Tier, StartingSkillId, PassiveName, PassiveText, Mods, Shape, Grants, Unlock`. The passive reaches the sim as `BuildMods`/`SkillShape`/`BuildTrigger` at `BuildComposer.cs:111-115` — no skill list, no exclusivity, no per-character catalogue.

**7. There is ONE loadout for the whole account.** `Game1.cs:367` `private PlayerLoadout _loadout = PlayerLoadout.Starter();` — a single field, re-created only in `ResetAll` (`:1163`). Switching character does **not** swap loadouts; it sheds unwearable gear (`Game1.cs:4319`) and nothing else. `CharacterState` (`src/IdleXIdle.Core/Characters/CharacterState.cs`) holds `_activeId`, `_unlocked`, `_questsDone` and no build state at all.

**8. Skill progression is likewise account-wide.** `SkillProgress` keys on skill id alone (`SkillProgress.cs:34,37,40`); `SaveGame.SkillProgress` is one flat list (`SaveGame.cs:167`). This satisfies the brief's LAW 4 already, but for the wrong reason: nothing is per-character, so nothing can be lost by switching.

**9. The only per-character thing anywhere near a skill is PRESENTATION.** `HuntScreen.FxFor` (`:3662-3666`) prefers `fx_{Character.Id}_{fxKey}_strip8_512` over `fx_{fxKey}`, and `Character.StripKeys(clip)` (`Character.cs:174`) prefers `char_{Id}_{clip}_strip8_512`. Both are asset-lookup fallbacks. Neither touches the model.

**10. Save format carries no owner.** `SavedSkill` (`SaveGame.cs:312-333`) is `{ SkillId, Source, Form (legacy), VowId, Passive }`. `SavedSkillProgress` (`:800-810`) is `{ SkillId, Uses, Variation, Reinforcements }`. `SaveGame.LearnedSkills` (`:177`) is a bare `List<string>`. No character id appears in any of them.

**Files this area will change**

- src/IdleXIdle.Core/Builds/SkillCatalogue.cs — the whole authoring surface: Style/SkillKind/SkillEffect/ReactionOn enums, Reinforcement, SkillVariation, SkillRules (19 members), SkillDef (64 params), the 12-entry All list (:347-752), ById/Find/ActiveOf/PassiveOf/RingDistance/Opposite. Any signature skill is either a 13th+ entry here or a parallel list this file must still be the source of truth for.
- src/IdleXIdle.Core/Builds/SkillProgress.cs — uses/level/variation/reinforcement, keyed by skill id only. Already satisfies 'experience is permanent'; must keep doing so when access stops being permanent (brief §17). Restore's rename maps (:155-171) are where any new id/rename lands.
- src/IdleXIdle.Core/Builds/PlayerLoadout.cs — the slot list, SkillCapacity, LAW-13 refusal in SetSkill (:178-185), the duplicate collapse in Restore (:325-328), Starter() hardcoding hammer_blow (:362). Every slot mutation lives here.
- src/IdleXIdle.Core/Builds/BuildComposer.cs — THE access gate (:138-139 union, :156 refusal) and the one resolution site (:162-171). Brief §16's 'access follows current mastery allocation' is a change to these ten lines; §18's signature exemption is another.
- src/IdleXIdle.Core/Builds/Build.cs — SkillSlots=4 (:249), SlotCapacity (:273-279), ActiveSlotsFor/PassiveSlotsFor (:340-343), EquippedSkill (:217-221), Equip's dup + per-kind enforcement (:393-411), KeystoneSlots (:435).
- src/IdleXIdle.Core/Builds/MasteryTree.cs — _learned permanent latch (:158), Take's latch (:210), LearnSkill (:230-233), RestoreLearned (:217-221), LearnedSkills() union (:308-313). This is the rule brief §15 deletes.
- src/IdleXIdle.Core/Builds/MasteryCatalog.cs — the twelve Teaches() road nodes (:458-469), the factory (:478-486), SkillRoadCost=5 (:114). A thirteenth skill that is NOT mastery-gated needs an exemption here or in the composer.
- src/IdleXIdle.Core/Builds/SoloBattle.cs — reads every SkillDef dial; the three-way Kind fork (:1455-1459), the only ReactionOn dispatch (:2247), the kill-bleed scan (:1233-1246), the three Amplify reads (:1608, :1722, :2066). A new skill's verbs must land on dials this file already reads or it ships dormant.
- src/IdleXIdle.Core/Builds/SoloExpedition.cs — the single live RecordWave site (:377-379) and the injected Progress (:162). Where a signature skill's experience would be banked.
- src/IdleXIdle.Core/Builds/StyleAffinity.cs — the ring multipliers (:44-50). A signature skill still needs a Style, and its affinity factor is decided here.
- src/IdleXIdle.Core/Builds/BuildGlossary.cs — the player-facing vocabulary of skills; new terms register here (build_glossary_test.cs holds it).
- src/IdleXIdle.Core/Builds/DamageBench.cs — the bench every screen quotes; a new skill must be measurable through it.
- src/IdleXIdle.Core/Characters/Character.cs — StartingSkillId (:126) is the only per-character skill field that exists; a Signature field would go beside it.
- src/IdleXIdle.Core/Characters/CharacterRoster.cs — the ten entries (:51-218). Ten signatures means ten new authored blocks, and the seeker/tower hammer_blow collision has to be resolved.
- src/IdleXIdle.Core/Characters/CharacterState.cs — holds active id, unlocked set, quests done. If ownership becomes per-character state, it lands here.
- src/IdleXIdle.Core/Persistence/SaveGame.cs — WovenSkills (:153), SkillProgress (:167), LearnedSkills (:177), MasteryTaken (:170ish), SavedSkill (:312-333), SavedSkillProgress (:800-810). Brief §94-§95 migrations touch all of these.
- src/IdleXIdle.Core/Persistence/LegacySkillForm.cs — the FROZEN pre-v3 Form→skill walk (SlotKinds :71). Must not be disturbed by any new slot rule.
- src/IdleXIdle.Core/Prestige/DustEffects.cs — SkillSlots (:212-216, the fifth slot), KeystoneSockets (:206-209), KnowsVow/KnownVows/LearnedKeystones (composer's other gates).
- src/IdleXIdle.Core/Prestige/MemoryDust.cs — weave_5 (:314), socket_2 (:312), socket_3 (:317). Brief §54 (no fifth slot, slots from account progression) and §53 (structural unlocks leave traits) both land here.
- src/IdleXIdle.Core/Progression/Unlocks.cs — SkillSlots gate 1→4 (:239-246) and SkillSlotNote copy (:254-261). The brief wants slot access here, not on the trait tree.
- src/IdleXIdle.Core/Progression/Onboarding.cs — TourTarget.SkillSlots (:53, :278), the empty-slot hint (:592-594), the per-slot explained list (:512, :541).
- src/IdleXIdle.Core/Quests/Quests.cs — WavesWithStyle sums SkillProgress uses across a Style (:80). A signature skill with a Style silently changes quest progress.
- src/IdleXIdle.Game/Game1.cs — _skillProgress field (:456), save write (:1050-1057), restore (:803-804), reset (:1166), the account-wide birth-skill latch (:4326), ApplySkillCapacity (:3501-3506), capacity restore floor (:785), live Progress wiring (:4201), and the DevStart capture poses (:1940-2012, :2541-2609) that must keep composing.
- src/IdleXIdle.Game/LoadoutScreen.cs — the entire build screen: KnownSkills (:545-550), variation purchase (:893), reinforcement purchase (:903), respec (:771), the LAW-13 message (:877-884), preview suppression (:1143-1154), slot kind labels (:353-354), level readouts (:863-864, :1284, :1553). Brief §11/§12/§20 (signature presentation, lock state) is mostly this file.
- src/IdleXIdle.Game/HuntScreen.cs — ClipKey switch (:1581), FxFor (:3660-3666), aura fx key (:948), rail cooldown (:3696ish), Progress hand-off (:869), slot capacity mirror (:3325-3326).
- src/IdleXIdle.Game/RosterScreen.cs — the STARTING SKILL block (:671-700) and the 'ALREADY KNOWN' line (:747-748) that currently advertises account-wide access.
- src/IdleXIdle.Game/MasteryScreen.cs — SkillRoad node rendering (:1806-1807, :2207, :2278), slot name lookup (:49), Respec (:1196). Brief §19 (respec loadout repair) and §20 (lock state) start here.
- src/IdleXIdle.Game/VaultScreen.cs — share codes name skills by id (:1509-1510) with a legacy Form fallback; a new skill or a per-character one changes what a code can legally carry.
- src/IdleXIdle.Game/GearScreen.cs — SkillLevels pass-through (:150) into the DPS bench (:652).
- src/IdleXIdle.Game/TrainingScreen.cs — SkillLevels (:69), build compose (:455), and the ResonancePerPoint readouts (:1039, :1131-1134).
- src/IdleXIdle.Game/ForgeScreen.cs — ActiveDefs (:1032), the badge that reads the composed build's resolved defs.
- tools/check_skill_doc.py — generates design/gdd/skill-slots-and-skill-trees.md §6 from the catalogue; its regex (:47-52) pins the literal authored shape, so any new authoring form breaks tools/check_all.sh.
- design/gdd/skill-slots-and-skill-trees.md — the generated skill tables plus the hand-written §2/§5/§8/§9/§10 rules the refactor rewrites.
- assets/art/UI/icons/skills/icon_skill_*.png — exactly twelve today, one per skill id; every new skill needs one (drawn by RosterScreen.cs:700 and LoadoutScreen).
- tests/unit/IdleXIdle.Core.Tests/Builds/skill_catalogue_test.cs — 13 structural gates (one active + one passive per style :37, two variations of three reinforcements :166, unique ids :69, timing per kind :94, names its art :209). A signature skill outside the six-style pairing breaks :37.
- tests/unit/IdleXIdle.Core.Tests/Builds/variation_liveness_test.cs — 24 theory cases; every new variation must move the fight.
- tests/unit/IdleXIdle.Core.Tests/Builds/reinforcement_liveness_test.cs — 72 theory cases plus :195 'every reinforcement declares a delta'.
- tests/unit/IdleXIdle.Core.Tests/Builds/loadout_duplicate_skill_test.cs — the 13 LAW-13 tests; brief §14 keeps this invariant and extends it to signatures.
- tests/unit/IdleXIdle.Core.Tests/Builds/skill_progress_test.cs — 13 tests pinning the level curve, spend rules, respec and save round-trip.
- tests/unit/IdleXIdle.Core.Tests/Builds/save_migration_test.cs — pins that a level is restored before any purchase can fail; brief §94-§97 migrations must extend it.
- tests/unit/IdleXIdle.Core.Tests/Builds/champion_starting_skill_test.cs — asserts every character names a real skill and the roster mixes Active/Passive; does NOT assert uniqueness (:41-72).
- tests/unit/IdleXIdle.Core.Tests/Builds/skill_slot_kinds_test.cs — pins ActiveSlotsFor/PassiveSlotsFor and the spill walk (:190).
- tests/unit/IdleXIdle.Core.Tests/Builds/BuildTests.cs, PassiveTreeTests.cs, blow_vertical_slice_test.cs, combat_balance_test.cs, shield_test.cs, beat_cadence_test.cs, affinity_test.cs, affinity_vow_buyback_test.cs, source_matrix_test.cs, mastery_node_liveness_test.cs, build_glossary_test.cs, TestBuilds.cs — all compose builds through BuildComposer/PlayerLoadout and will need to keep compiling through any signature change.
- tests/unit/IdleXIdle.Core.Tests/Characters/roster_parity_test.cs — treats StartingSkillId as the character's whole skill contribution (:146-155, :277); a signature skill changes what parity even means.
- tests/unit/IdleXIdle.Game.Tests/loadout_feedback_test.cs — the LOADOUT screen's messages, including the LAW-13 refusal copy.

**Risks**

- OWNERSHIP DOES NOT EXIST AND THE CODE ACTIVELY FIGHTS IT. Game1.cs:4326 latches the active character's StartingSkillId into the permanent account-wide learned set every frame, and RosterScreen.cs:747-748 advertises the result as 'ALREADY KNOWN'. Playing THE ANVIL once grants PRESS to every other champion for the life of the save. Brief LAW 1 and LAW 2 are the exact inverse of shipped behaviour, and existing saves already carry the leaked set in SaveGame.LearnedSkills.
- MASTERY ACCESS IS A PERMANENT LATCH, NOT AN ALLOCATION. MasteryTree._learned (:158) is added to in Take (:210), RestoreLearned (:217-221) and LearnSkill (:230-233), and removed from NOWHERE — respec returns points, never skills (:155-157, D7 2026-08-31). BuildComposer.cs:156 gates on it unconditionally. Brief §15/§16 deletes this rule; the deletion will silently unweave skills from every existing build on the next respec unless §19's repair path lands in the same change. The comment at MasteryCatalog.cs:454-457 says the permanence is load-bearing: 'learning across respecs is the only way a four-slot build ever fills from twelve skills'.
- 'LAW 13' MEANS TWO DIFFERENT THINGS. Every code comment saying LAW 13 (PlayerLoadout.cs:171, :316; Build.cs:391; LoadoutScreen.cs:1143) refers to the ui-polish pass's numbering for the duplicate-skill rule (production/audit/ui-polish/PLAN.md:16, REPORT.md:9). In the new brief that rule is §14 and LAW 13 is 'Automation belongs to Warren' (BRIEF.md:2615). Anyone reading the code against the new brief will follow the wrong law.
- THE FIFTH SKILL SLOT IS LIVE. DustEffects.SkillSlots (:212-216) returns 5 when the Dust tree owns weave_5 (MemoryDust.cs:314, cost 3, requires socket_2), Game1.ApplySkillCapacity (:3501-3506) honours it once the onboarding gate reaches 4, and PlayerLoadout.AddSkill deliberately does NOT clamp to MaxSkills (:58-65, :95). Brief §54 says 2 active + 2 passive and no legacy fifth slot. Removing weave_5 shrinks a bought capacity — and Build.SlotCapacity's getter (:275) refuses to report fewer slots than are woven, so the fifth skill would stay equipped while the tree stopped paying for it.
- AT FIVE SLOTS THE SPLIT IS 3 ACTIVE / 2 PASSIVE, not 2/2. ActiveSlotsFor(5) = (5+1)/2 = 3 (Build.cs:340). Build.cs:332-337 records that this pushes beat demand back toward 0.6 — the exact number the whole slot rework existed to bring down to ~0.45. Any signature skill that is an Active adds to that demand again.
- SkillDef IS A 64-PARAMETER POSITIONAL RECORD. Adding a field means touching every call site's named-argument block and the doc generator's regex. SkillRules (19 members, SkillCatalogue.cs:162-185) is the intended escape hatch and its own comment (:130-141) says the record is already at the width where a reader stops reading. A signature-skill field added carelessly to SkillDef makes this worse for all twelve shared skills too.
- SkillEffect IS ALMOST ENTIRELY DEAD. Only SkillEffect.Amplify is ever compared (SoloBattle.cs:1608, :1722, :2066). Damage and Heal are branched on nowhere — drain_wilt is tagged Heal and its base line is an attack break; drain_drink is tagged Heal and deals 260 base damage. Do not build signature-skill dispatch on this field believing it means something.
- ReactionOn IS ALMOST ENTIRELY DEAD. Only ReactionOn.Bitten is dispatched (SoloBattle.cs:2247). volley_weep declares ReactionOn.Kill and the sim never reads it — WEEP runs off the BleedOnKillFraction dial scanned on every death (SoloBattle.cs:1233-1246). ReactionOn.LowHealth and ReactionOn.WaveStart are declared (SkillCatalogue.cs:96-97) and used by nothing. A signature Reaction authored on Kill/LowHealth/WaveStart will compose, display, persist and never fire — the codebase's own named failure mode.
- TWO CHARACTERS SHARE ONE BIRTH SKILL. seeker (CharacterRoster.cs:52) and tower (:128) both name hammer_blow. Ten characters map to nine distinct skills, and sign_brand and field_pulse are nobody's. There is no existing 1:1 character→skill relation to promote into a signature; ten signatures must be authored new. champion_starting_skill_test.cs does not assert uniqueness, so nothing guards this today.
- ONE LOADOUT SERVES ALL TEN CHARACTERS. Game1.cs:367 is a single PlayerLoadout, re-created only in ResetAll (:1163). Switching character sheds unwearable gear (:4319) and nothing else — no per-character loadout, no slot repair on switch. Brief §13 (character switching and signature validity) has no state to hang on; it must be created, saved and migrated.
- THE COMPOSER'S RESOLUTION ORDER IS LOAD-BEARING AND SINGLE-SITE. BuildComposer.cs:162-171 applies variation-then-reinforcements exactly once. SkillProgress.Signature (:173-177) is folded into the hunt's build stamp; the comment at :129-133 records that omitting it is how 'the whole variation layer shipped dormant (audit 2026-08-31, finding #2)'. Any signature-skill resolution path that bypasses this seam re-creates that bug.
- THE DESIGN DOC IS GENERATED AND CI-GATED. tools/check_skill_doc.py builds design/gdd/skill-slots-and-skill-trees.md §6 from the catalogue and its regex (:47-52) pins the literal shape new("id", "NAME", Style.X, SkillKind.Y, SkillEffect.Z,\n "line". A signature skill authored in a different shape, or held in a second list, breaks tools/check_all.sh with 'the catalogue's shape changed'.
- THE LIVENESS SUITE IS A HARD GATE AND IT SCALES. variation_liveness_test.cs (24 cases) and reinforcement_liveness_test.cs (72 cases + :195) run every variation and reinforcement through a real fight and demand that damage-dealt or health-kept move. Ten signature skills authored to the same STYLE→SKILL→VARIATION→REINFORCEMENTS shape (brief §7) adds 20 variations and 60 reinforcements to those theories — 96 new fight simulations that must each measurably move the needle.
- EXPERIENCE PERMANENCE IS CURRENTLY FREE BECAUSE NOTHING IS PER-CHARACTER. SkillProgress keys on skill id alone and SaveGame.SkillProgress is one flat list. LAW 4 holds today only as a side effect of that flatness. The moment a signature skill becomes character-scoped, permanence stops being automatic and needs its own test — and SkillProgress.Restore's rule (level restored first at :204, unrecognised purchases refunded as unspent) is the pattern to preserve.
- OFFLINE DELIBERATELY DOES NOT LEVEL SKILLS (Descent.cs:69-72, OfflineHunt.cs:29 — Progress stays null). Signature-skill progression inherits that silence. If a signature is expected to level while away, that is a new decision, not an existing behaviour.
- BuildComposer's SKILL GATE AND Build.Equip's CAPACITY GATE BOTH REFUSE SILENTLY (BuildComposer.cs:156 continue; Build.cs:396-409 return false). A signature skill that is supposed to always be available but is not in taughtSkills, or that arrives when the active budget is spent, will vanish from the build with no message on any screen — the same class of failure the loadout screen's own comment (LoadoutScreen.cs:874-876) was written to prevent at the UI layer.

## Mastery tree, skill unlock semantics, respec (IDLExIDLE, branch feat/hunter-cutout-rig)

### 1. How a mastery node unlocks a shared skill today. Quote the code path from node allocation to "this skill is available".

**One node kind unlocks skills: `MasteryKind.SkillRoad`.** There are exactly 12 of them, one per skill. The whole chain is five hops.

### Hop 1 — the catalogue declares the node and what it teaches
`src/IdleXIdle.Core/Builds/MasteryCatalog.cs:477-485`
```csharp
private static void Teaches(List<MasteryNode> n, Branch b, string id, string spec, string skillId)
{
    var def = SkillCatalogue.ById(skillId);
    n.Add(new(id, MasteryKind.SkillRoad, b, 3, SkillRoadCost,
              $"{def.Name} — {def.Line.ToUpperInvariant()}", S, null, new[] { spec })
    {
        GrantsSkillId = skillId,
    });
}
```
`GrantsSkillId` is declared at `src/IdleXIdle.Core/Builds/MasteryTree.cs:92`. Its ONLY prerequisite is the style's Specialisation (`new[] { spec }`), or the road's first node for the second road node.

### Hop 2 — the player clicks the node
`src/IdleXIdle.Game/MasteryScreen.cs:1218-1232` (click on the node itself) or `:1209-1214` (the inspector's TAKE button) → both call `TakeNode(node)` at `src/IdleXIdle.Game/MasteryScreen.cs:1293-1296`:
```csharp
private void TakeNode(MasteryNode node)
{
    var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
    if (Mastery.Take(node.Id))
```

### Hop 3 — the tree latches the skill (THE unlock)
`src/IdleXIdle.Core/Builds/MasteryTree.cs:190-212`
```csharp
public bool CanTake(string id)
{
    if (_taken.Contains(id)) return false;
    if (MasteryCatalog.ById(id) is not { } node) return false;
    if (Available < node.Cost) return false;
    if (!node.Unlocked(_taken.Contains)) return false;
    if (node.Kind == MasteryKind.Mastery && MasteredBranch() is not null) return false;
    if (node.Kind == MasteryKind.Specialisation && Affinity() is not null) return false;
    return true;
}

public bool Take(string id)
{
    if (!CanTake(id)) return false;
    _taken.Add(id);
    // THE LATCH (D7): a taught skill is learned the moment its node is taken, for good.
    if (MasteryCatalog.ById(id)?.GrantsSkillId is { } learned) _learned.Add(learned);
    return true;
}
```

### Hop 4 — the tree publishes the set
`src/IdleXIdle.Core/Builds/MasteryTree.cs:308-314`
```csharp
public IReadOnlySet<string> LearnedSkills()
{
    var set = new HashSet<string>(_learned, StringComparer.Ordinal);
    foreach (var n in _taken.Select(MasteryCatalog.ById))
        if (n?.GrantsSkillId is { } s) set.Add(s);
    return set;
}
```
Note the UNION: the permanent latch `_learned` **plus** what the currently-taken roads teach.

### Hop 5 — "this skill is available" (the only enforcement in the game)
`src/IdleXIdle.Core/Builds/BuildComposer.cs:135-156`
```csharp
// WHAT THIS CHAMPION KNOWS: the roads it has walked, plus the one skill it was born with.
var taughtSkills = mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
if (character?.StartingSkillId is { } born) taughtSkills.Add(born);
...
if (s.SkillId is not { } id || SkillCatalogue.Find(id) is not { } def) continue;
// THE GATE, AND IT IS UNCONDITIONAL. You weave what you know: all twelve skills are
// learned on the mastery tree — and since D7 (2026-08-31) learning is PERMANENT, so
// this set only ever grows and a respec moves points without unweaving anything.
if (!taughtSkills.Contains(def.Id)) continue;
```
This `continue` is the whole gate. **`PlayerLoadout.SetSkill` (`src/IdleXIdle.Core/Builds/PlayerLoadout.cs:178-185`) does NOT check the learned set** — an unknown skill can be written into a slot by the model; it is only silently skipped at compose time and greyed on the screen (`src/IdleXIdle.Game/LoadoutScreen.cs:815`).

### The complete road table (12 of 12)
`src/IdleXIdle.Core/Builds/MasteryCatalog.cs:458-469`

| Node id | Branch | Prereq | Teaches | Skill name | Cost |
|---|---|---|---|---|---|
| `road_hammer` | Resonance | `spec_strike` | `hammer_blow` | BLOW | 5 |
| `road_hammer_2` | Resonance | `road_hammer` | `hammer_press` | PRESS | 5 |
| `road_snare` | Resonance | `spec_trap` | `snare_jaws` | JAWS | 5 |
| `road_snare_2` | Resonance | `road_snare` | `snare_repay` | REPAY | 5 |
| `road_volley` | Loot | `spec_projectile` | `volley_spray` | SPRAY | 5 |
| `road_volley_2` | Loot | `road_volley` | `volley_weep` | WEEP | 5 |
| `road_field` | Loot | `spec_aura` | `field_mire` | MIRE | 5 |
| `road_field_2` | Loot | `road_field` | `field_pulse` | PULSE | 5 |
| `road_sign` | Tempo | `spec_mark` | `sign_call` | CALL | 5 |
| `road_sign_2` | Tempo | `road_sign` | `sign_brand` | BRAND | 5 |
| `road_drain` | Endure | `spec_transformation` | `drain_drink` | DRINK | 5 |
| `road_drain_2` | Endure | `road_drain` | `drain_wilt` | WILT | 5 |

Cost constants: `RingCost = {0,1,3,5,8}` (`MasteryCatalog.cs:107`), `BridgeCost = 6`, `SpecialisationCost = 6` (`:110-111`), `SkillRoadCost = 5` (`:114`).

### The second, undocumented unlock door: birth skills
`src/IdleXIdle.Game/Game1.cs:4323-4326` runs EVERY FRAME:
```csharp
// BIRTH SKILLS ARE LEARNED BY BEING SOMEONE (P9). Becoming a champion latches theirs into
// the permanent set (D7), so the roster's YOU KEEP SKILLS holds for the one skill a
// champion brings, not only for the tree's. Idempotent, so per-frame is free.
_mastery.LearnSkill(_characters.Active.StartingSkillId);
```
The 10 roster champions (`src/IdleXIdle.Core/Characters/CharacterRoster.cs:52,69,84,98,114,128,145,167,194,217`) carry **9 distinct** birth skills (`hammer_blow` appears twice, lines 52 and 128). **Playing all ten champions permanently latches 9 of the 12 skills with zero mastery points spent.** The 3 skills reachable only through a road are `field_pulse`, `sign_brand`, `drain_drink`.

### Arithmetic — the cheapest path to a first taught skill
Minor (RingCost[1]=1) + Notable (RingCost[2]=3) + Specialisation (6) + SkillRoad (5) = **15 points**. Second skill on the same road: +5 = **20 points**. Matches the comment at `MasteryCatalog.cs:453-454` and `MasteryPoints.cs` remarks.

Points supply: `MasteryPoints.FromDepth(d) = floor(sqrt(d) * 0.9)` (`src/IdleXIdle.Core/Builds/MasteryPoints.cs:36,39`), summed per region (`:43-51`), fed by `Game1.SkillPointsEarned()` (`src/IdleXIdle.Game/Game1.cs:6798-6804`) → `_mastery.SetEarned(...)` every frame (`Game1.cs:4443`). Six regions at depth 150: floor(sqrt(150)×0.9) = floor(11.02) = 11 each → 66 points, against a 316-point tree = **20.9%**.

### 2. THE KEY QUESTION: is skill access permanent once discovered? Find the field/method that makes it permanent (the brief calls it "permanently learned through Mastery" / RestoreLearned / LearnedSkills) and quote every site that reads or writes it.

**YES. Skill access is permanent today, deliberately and by name — decision "D7", commit `a7cf35c` (2026-08-31, `feat(mastery)!: a learned skill is learned for good`).** This is precisely the rule BRIEF §15 orders removed.

### The field
`src/IdleXIdle.Core/Builds/MasteryTree.cs:154-158`
```csharp
// ── DISCOVERY IS PERMANENT (D7, 2026-08-31). Taking a road node LATCHES its skill here; respec
//    and refund return points and never touch this set. Persisted (SaveGame.LearnedSkills) and
//    unioned with what the currently-taken roads teach, so a pre-D7 save seeds itself from its
//    own MasteryTaken on first load. ─────────────────────────────────────────────────────────────
private readonly HashSet<string> _learned = new(StringComparer.Ordinal);
```

### EVERY WRITE to `_learned` (4 sites, all in MasteryTree.cs)
| file:line | site | what it does |
|---|---|---|
| `src/IdleXIdle.Core/Builds/MasteryTree.cs:210` | `Take(id)` | `if (MasteryCatalog.ById(id)?.GrantsSkillId is { } learned) _learned.Add(learned);` — the latch |
| `src/IdleXIdle.Core/Builds/MasteryTree.cs:217` | `RestoreLearned` | `_learned.Clear();` — the only clear in the codebase |
| `src/IdleXIdle.Core/Builds/MasteryTree.cs:220` | `RestoreLearned` | `if (SkillCatalogue.Find(id) is not null) _learned.Add(id);` |
| `src/IdleXIdle.Core/Builds/MasteryTree.cs:232` | `LearnSkill(skillId)` | `if (skillId is not null && SkillCatalogue.Find(skillId) is not null) _learned.Add(skillId);` — the birth-skill door |

**Neither `Respec()` (`:259-263`) nor `Refund(id)` (`:243-253`) touches `_learned`.** Verified by reading both bodies in full.

### EVERY READ of `_learned` (1 site)
`src/IdleXIdle.Core/Builds/MasteryTree.cs:310` — inside `LearnedSkills()`:
```csharp
var set = new HashSet<string>(_learned, StringComparer.Ordinal);
```

### EVERY caller of `RestoreLearned` (production + test)
| file:line | context |
|---|---|
| `src/IdleXIdle.Game/Game1.cs:808` | `_mastery.RestoreLearned(save.LearnedSkills);   // D7 — discoveries survive every respec` — runs immediately after `RestoreTaken` at `:807` |
| `tests/unit/IdleXIdle.Core.Tests/Builds/mastery_learned_permanence_test.cs:60` | `t.RestoreLearned(new[] { "volley_spray", "ghost_skill" });` |

### EVERY caller of `LearnSkill`
| file:line | context |
|---|---|
| `src/IdleXIdle.Game/Game1.cs:4326` | `_mastery.LearnSkill(_characters.Active.StartingSkillId);` — per frame, latches the active champion's birth skill forever |
| `tests/unit/IdleXIdle.Core.Tests/Builds/mastery_learned_permanence_test.cs:84-85` | `t.LearnSkill("volley_weep"); t.LearnSkill("ghost_skill");` |

### EVERY caller of `LearnedSkills()`
| file:line | what it does with it |
|---|---|
| `src/IdleXIdle.Core/Builds/BuildComposer.cs:138` | `var taughtSkills = mastery.LearnedSkills().ToHashSet(...)` — the gate at `:156` |
| `src/IdleXIdle.Game/Game1.cs:1063` | `LearnedSkills = _mastery.LearnedSkills().OrderBy(...).ToList()` — writes the save |
| `src/IdleXIdle.Game/LoadoutScreen.cs:547` | `KnownSkills()` — drives library lock icons, EQUIP button, slot fill |
| `src/IdleXIdle.Game/MasteryScreen.cs:1807` | inspector: is this road's skill already learned |
| `src/IdleXIdle.Game/MasteryScreen.cs:2207` | canvas: the gold DISCOVERED diamond on a road node |
| `src/IdleXIdle.Game/RosterScreen.cs:747` | roster card: "ALREADY KNOWN" beside a champion's starting skill |
| `tests/.../mastery_learned_permanence_test.cs:34,41,52,63,64,75,87,88` | the five permanence tests |
| `tests/.../skill_slot_kinds_test.cs:484` | `Taught.Everything().LearnedSkills()` |

### Persistence
`src/IdleXIdle.Core/Persistence/SaveGame.cs:172-177`
```csharp
/// Skills learned FOR GOOD (D7, 2026-08-31): catalogue ids latched when a road node is taken.
/// Respec never removes one. Absent on an older save — the tree seeds the set from
/// <see cref="MasteryTaken"/>'s roads, and the next save writes the union.
public List<string> LearnedSkills { get; init; } = new();
```

### The tests that PIN permanence (these will have to be inverted)
`tests/unit/IdleXIdle.Core.Tests/Builds/mastery_learned_permanence_test.cs` — five facts:
- `:28 test_respec_returns_points_but_the_skill_stays_learned` — `t.Respec(); Assert.Equal(0, t.Spent); Assert.Contains("hammer_blow", t.LearnedSkills());`
- `:45 test_a_single_node_refund_keeps_the_skill_too`
- `:56 test_restored_learned_skills_survive_and_unknown_ids_are_dropped`
- `:68 test_an_old_save_seeds_learned_from_its_taken_roads`
- `:79 test_becoming_a_champion_latches_its_birth_skill_for_good`
- `:100 test_the_composer_weaves_a_learned_skill_after_the_points_moved_on`

### CONTRADICTION FOUND — a second test asserts the OPPOSITE and also passes
`tests/unit/IdleXIdle.Core.Tests/Builds/skill_slot_kinds_test.cs:207-243`:
```csharp
/// The gate the style roads exist for. Respec RE-LOCKS (designer, 2026-08-30), so this is asserted
/// in both directions: without the road the slot is not woven, with it the slot is.
...
walked.RestoreTaken(new[] { "spec_strike", "road_hammer", "road_hammer_2" });
...
// AND RESPEC TAKES IT BACK. The designer chose a road that re-locks, so the same build
// composed after the points are refunded weaves nothing at all.
walked.Respec();
Assert.Empty(With(walked).Skills);
```
Both tests are green because **the permanence latch is bypassed by `RestoreTaken`**: `RestoreTaken` (`MasteryTree.cs:177-184`) writes only `_taken` and never `_learned`, so a tree built that way holds skills only through the derived half of `LearnedSkills()`, which a respec DOES clear. Believe the code: **`Take()` gives permanence; `RestoreTaken()` does not.** This is a live inconsistency in the model, not just in the tests — see risks.

### Live consequence — the "free permanent-unlock exploit" of BRIEF §118
Points are recomputed, never consumed: `Spent => _taken.Select(...).Sum(n => n!.Cost)` (`MasteryTree.cs:164`), `Available => Earned - Spent` (`:166`), and `Respec()` is free, instant and unlimited (`:259-263`, class remarks `:143-148`). So the loop is: 15 points → spec + road → 1 skill latched forever → respec → 15 points again → another style's road. **15 mastery points buys every one of the 6 active skills over time; 20 points buys all 12.** `CanTake`'s one-specialisation rule (`:201`) is defeated entirely by respec, and the permanence latch is what makes it profitable.

### 3. What a mastery respec does today: what it clears, what it keeps, whether it repairs the loadout, and whether it warns first.

### The model operation
`src/IdleXIdle.Core/Builds/MasteryTree.cs:255-263` — the whole method:
```csharp
/// <summary>
/// Give every point back. Free and instant — see the class remarks. Learned SKILLS stay
/// learned (D7): the respec moves points, never discoveries.
/// </summary>
public void Respec()
{
    _taken.Clear();
    _taken.Add(MasteryCatalog.StartId);
}
```
Two lines. That is the entire respec.

### WHAT IT CLEARS (all derived from `_taken`)
| what | file:line | effect after respec |
|---|---|---|
| `Spent` | `MasteryTree.cs:164` | → 0 |
| `Available` | `MasteryTree.cs:166` | → full `Earned` |
| `MasteredBranch()` | `:266-270` | → null (the ring-4 capstone lock lifts) |
| `Affinity()` | `:282-286` | → null — **the ×2.0 style multiplier vanishes silently** |
| `Shape()` | `:289-290` | → `SkillShape.None` |
| `Stats()` | `:293-301` | → empty (every HunterStat the tree paid) |
| `Triggers()` | `:317-319` | → empty (the specialisation's `BuildTrigger` gift) |
| derived half of `LearnedSkills()` | `:311-313` | → gone (only matters for a tree built via `RestoreTaken`) |

### WHAT IT KEEPS
| what | file:line | why |
|---|---|---|
| `_learned` (permanently latched skills) | `MasteryTree.cs:158` — never touched by `:259-263` | D7 |
| `Earned` | `:161`, and re-derived from depth every frame at `Game1.cs:4443` | points are a function of depth, not a spend ledger |
| `SkillProgress` (uses/level/variation/reinforcements per skill) | `src/IdleXIdle.Core/Builds/SkillProgress.cs:34-40` — completely decoupled from the tree | BRIEF §17 ("experience is permanent") is **already true** |
| the loadout's slots, sources and vows | `src/IdleXIdle.Core/Builds/PlayerLoadout.cs` — no mastery reference at all | see below |
| keystone sockets, vows, skill-slot count | all from the Dust/trait tree (`DustEffects`), not from mastery | |

### DOES IT REPAIR THE LOADOUT? **No. There is no repair code anywhere.**
- `MasteryScreen.cs:1196` calls `Mastery.Respec()` and then only `Say(...)`, `UiMotion.Flash(...)`, `Dirty = true` (`:1197-1199`). It never touches `Loadout`.
- `MasteryScreen.Loadout` (`src/IdleXIdle.Game/MasteryScreen.cs:331`) is fed by the host at `src/IdleXIdle.Game/Game1.cs:3033` and **is never read in any live path** — the only helper that would use it, `SkillWord` (`MasteryScreen.cs:48-49`), has zero call sites. `MasteryScreen.cs:1234` says so out loud: `// Skills, Vows and keystone sockets are all LoadoutScreen's now.`
- `PlayerLoadout.Restore` (`src/IdleXIdle.Core/Builds/PlayerLoadout.cs:275-329`) migrates legacy rows and de-duplicates, but has **no learned-set check**.
- The only "repair" is silent and at compose time: `BuildComposer.cs:156` `continue`s past an unknown skill, and `LoadoutScreen.SlotFilled` (`:556`) reports the slot as unfilled so the row draws as **"EMPTY SLOT / PICK A SKILL FROM THE LIBRARY"** (`src/IdleXIdle.Game/LoadoutScreen.cs:1023-1029`) — never as "LOCKED, Lv.4". The saved `SkillId` stays in the slot; the player just sees an empty row. This is exactly the presentation BRIEF §20 forbids.

Today this state is effectively unreachable at runtime because `_learned` never shrinks — so the code path that would need repairing has never been exercised by a player.

### DOES IT WARN FIRST? **Only a two-press arm. No list, no modal, no naming of consequences.**
`src/IdleXIdle.Game/MasteryScreen.cs:1183-1201`:
```csharp
if (ResetBtn.Contains(hit) && Mastery.Spent > 0)
{
    // TWO CLICKS, because it un-spends every point in the tree and there is no undo prompt
    // anywhere else in this game. The respec itself stays free and instant ...
    if (!_resetArmed) { _resetArmed = true; Say("PRESS AGAIN TO TAKE EVERY POINT BACK.", null, "sfx_click"); return; }
    _resetArmed = false;
    Mastery.Respec();
    Say("ALL MASTERY POINTS RETURNED.", null, "sfx_click");
    UiMotion.Flash(PointsKey, UiMotion.Transition);
    Dirty = true;
    return;
}
// Any other click on the tree disarms it — but must NOT return, or no node could be taken.
_resetArmed = false;
```
Button copy: `src/IdleXIdle.Game/MasteryScreen.cs:1553` — `"TAKE EVERY POINT BACK"` / armed: `"PRESS AGAIN TO CONFIRM"`. Shown only when `Mastery.Spent > 0` (`:1552`). Arm is cleared on nav entry (`Disarm()` at `:278`, called from `Game1.cs:6919`).

**It does not warn that the style/affinity is being surrendered**, which is the one thing the respec actually destroys today. The only place the game mentions it is a passive inspector line: `MasteryScreen.cs:1849` — `"YOUR STYLE IS ALREADY {X}. ONE STYLE PER HUNTER — TAKE EVERY POINT BACK TO CHOOSE AGAIN."`

### The per-node inverse (a partial respec)
`src/IdleXIdle.Core/Builds/MasteryTree.cs:243-253` — `Refund(id)` removes one node, then re-adds it if any taken node would be stranded:
```csharp
_taken.Remove(id);
var stranded = _taken.Select(MasteryCatalog.ById)
    .Any(n => n is not null && n.Id != MasteryCatalog.StartId && !n.Unlocked(_taken.Contains));
if (stranded) { _taken.Add(id); return false; }
```
It also never touches `_learned`. UI doors: right-click a node (`MasteryScreen.cs:1134-1148`) and the inspector's GIVE BACK button (`:1209-1214`), both funnelled through `GiveBack` (`:1262-1273`). Refusal copy: `"ANOTHER NODE DEPENDS ON THIS ONE."` / `"NOTHING SPENT HERE."` — **no warning at all on a single refund**, and no arm.

### A different, unrelated "respec" that shares the word
`src/IdleXIdle.Core/Builds/SkillProgress.cs:121-126` `Respec(string skillId)` gives one skill's levels back (variation + reinforcements). Called from `src/IdleXIdle.Game/LoadoutScreen.cs:771`, message at `:774` — `"{name} IS UNSPENT AGAIN. EVERY LEVEL IT EARNED IS STILL THERE."` This one is per-skill and has nothing to do with mastery.

### Test coverage of respec today
`tests/unit/IdleXIdle.Core.Tests/Builds/MasteryTreeTests.cs:519 test_respec_gives_every_point_back`; `:538 test_a_refund_never_strands_a_node`; `:564 test_a_spur_holds_its_parent_until_it_is_refunded`; plus the six permanence facts listed in Q2. **No test asserts anything about loadout repair or a warning — because neither exists.**

### 4. The specialisation / style ceremony: what it is, what it gates.

### What it is
A full-screen modal called **THE ATTUNEMENT**, fired once, when the player takes their **first** `MasteryKind.Specialisation` node.

**Trigger** — `src/IdleXIdle.Game/MasteryScreen.cs:1293-1310`:
```csharp
private void TakeNode(MasteryNode node)
{
    var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
    if (Mastery.Take(node.Id))
    {
        ...
        if (firstSpec && node.Style is { } nf) OpenCeremony(node.Id, nf);
        return;
    }
```
**IMPORTANT — the take happens BEFORE the ceremony.** `Mastery.Take` has already mutated `_taken` when the modal opens. `src/IdleXIdle.Game/MasteryScreen.cs:1610` states it plainly: *"Sealing changes nothing the Take didn't already do; the ceremony IS the information."*

**State** — `src/IdleXIdle.Game/MasteryScreen.cs:252-257`: `_specNodeId`, `_specWasOpen`, `_specStyle`, and the public `SpecialisationOpen => _specNodeId is not null`.

**Open** — `OpenCeremony` at `:1284-1290` sets the fields and fires `UiMotion.Flash(SpecKey, UiMotion.Transition)`.

**Draw** — `DrawSpecialisation` at `:1612-1652`: a 0.75 scrim, a gold `Panel`, title `"YOUR SPECIALISATION"` (`:1625`), caption `"SIX STYLES — ONE IS YOURS."` (`:1626`), the six-seal `StyleAffinityDiagram` hexagon on its own field (`:1631-1635`), wrapped `SpecLines(name)` (`:1638-1643`), and two buttons (`:1645-1646`):
- `CHOOSE {STYLE}` → `SpecSealBtn`
- `NOT YET — TAKE THE POINTS BACK` → `SpecUndoBtn`

**Input** — `src/IdleXIdle.Game/MasteryScreen.cs:1158-1177`, and it swallows every click while open:
```csharp
if (_specNodeId is { } attId)
{
    if (SpecSealBtn.Contains(hit))
    {
        _specNodeId = null;
        Say($"{_specStyle.ToString().ToUpperInvariant()} IS YOUR STYLE — ITS SKILLS HIT TWICE AS HARD.", attId, "sfx_bind");
    }
    else if (SpecUndoBtn.Contains(hit))
    {
        Mastery.Refund(attId);
        _specNodeId = null;
        Dirty = true;
        Say("THE POINTS ARE BACK. CHOOSE A STYLE WHEN YOU ARE READY.", attId, "sfx_click");
        UiMotion.Flash(PointsKey, UiMotion.Transition);
    }
    return;
}
```
SEAL is a pure no-op on the model. UNDO is a real `Refund`. Panning and the right-click refund are both suppressed while open (`:1082`, `:1134`).

**Host-level modality** — `src/IdleXIdle.Game/Game1.cs:2932-2935` (`ceremonyHolds` blocks the nav rail and hotkeys) and `:3968` (notice toasts suppressed). It is described at `Game1.cs:3966` as *"the one modal the game stops for."*

**Dev capture hook** — `MasteryScreen.cs:323-329` `DevSpecialise(Style)`, driven by `RH_SHOT_MODE=attune` at `Game1.cs:1818`.

### The six specialisation nodes (complete)
`src/IdleXIdle.Core/Builds/MasteryCatalog.cs:431-447`, each cost 6, ring 3, prereq = any Notable of its branch (`Spec` helper at `:540-543`):

| Node id | Branch | Style | Grants trigger | Label / shape |
|---|---|---|---|---|
| `spec_strike` | Resonance | Hammer | `BuildTrigger.Execute` | STRIKE SPECIALIST — EXECUTE WEAKENED FOES (`CullThreshold 0.35`, `CullBonus 0.40`) |
| `spec_trap` | Resonance | Snare | `BuildTrigger.Coiled` | TRAP SPECIALIST — TRAPS RE-ARM ON BEING HIT |
| `spec_projectile` | Loot | Volley | `BuildTrigger.Overdraw` | VOLLEY SPECIALIST — ONE MORE SHOT, ONE MORE TARGET (`StyleTargets[Volley]=1`) |
| `spec_aura` | Loot | Field | `BuildTrigger.Radiance` | AURA SPECIALIST — TICKS 30% FASTER |
| `spec_mark` | Tempo | Sign | `BuildTrigger.Linger` | SIGN SPECIALIST — MARKS LAST LONGER (`AmplifyWindowMultiplier 1.3`) |
| `spec_transformation` | Endure | Drain | `BuildTrigger.Siphon` | MORPH SPECIALIST — LEECH DOUBLED, HEAL LIMIT … A WAVE |

### WHAT IT GATES — six things
**(a) One specialisation per hunter, hard-refused in the model.** `src/IdleXIdle.Core/Builds/MasteryTree.cs:197-201`:
```csharp
// ONE DISCIPLINE PER HUNTER. Affinity() reads the FIRST Specialisation taken, so a second
// one could only ever add its trigger while its Form silently counted for nothing ...
if (node.Kind == MasteryKind.Specialisation && Affinity() is not null) return false;
```
Only a full `Respec()` releases it — there is no other route. Pinned by `tests/unit/IdleXIdle.Core.Tests/Builds/one_discipline_test.cs:27,32,36`.

**(b) The damage ring.** `MasteryTree.Affinity()` (`:282-286`, first-taken wins) → `BuildComposer.cs:112` `Affinity = mastery.Affinity()` → `Build.Affinity` (`src/IdleXIdle.Core/Builds/Build.cs:380`) → `StyleAffinity.Factor` (`src/IdleXIdle.Core/Builds/StyleAffinity.cs:44-50`): **×2.00 own style, ×1.15 adjacent, ×0.75 off, ×0.45 opposite.** Read in the sim at `src/IdleXIdle.Core/Builds/SoloBattle.cs:901-909` (per-skill damage), `:989` (Sign amplify depth), `:1852-1854` and `:2072-2074` (the sworn-Vow one-ring buy-back).

**(c) The combo trigger.** `MasteryTree.Triggers()` (`:317-319`) → `BuildComposer.cs:113-114` → `Build.ExtraTriggers`. `MasteryCatalog.cs:424-430` records that the skill tree grants **only** these six style-combo triggers; every general trigger belongs to the Dust/trait tree. Pinned by `MasteryTreeTests.cs:468 test_only_style_combo_triggers_are_granted` and `:482 test_every_style_has_one_specialisation`.

**(d) THE SKILL ROADS — this is the load-bearing gate for the refactor.** Every `SkillRoad` node's sole prerequisite is its specialisation (`MasteryCatalog.cs:458-469`). With one specialisation allowed at a time, **a hunter who never respecs can reach only 2 of the 12 skills.** `MasteryTree.cs:88-90` names this as the reason permanence exists: *"one discipline gates one style's road, so learning across respecs is the only way a four-slot build ever fills from twelve skills."* Note also `Refund` will refuse to remove a specialisation while its road is taken (`:248-251` — the road would be stranded), so a partial undo is impossible; only the full respec works.

**(e) Build-screen presentation.** `Game1.cs:3271` `_loadoutScreen.ChosenStyle = _mastery.Affinity();` → `LoadoutScreen.cs:1229-1232` (the `YOURS` tag on the style row), `:1050-1054` (the per-slot `x2.0 YOURS` / `x0.75` factor line), `:1554`.

**(f) Mastery-screen chrome and copy.** `MasteryScreen.cs:1548-1550` — `"STYLE · {X}  x2.0"` or `"NO STYLE YET"`; `:1738` refusal `"ONE STYLE PER HUNTER — ALREADY CHOSEN."`; `:1318-1319` the click refusal; `:1843-1850` the inspector's three-way style sentence; `:1924` `KindWord` → `"SPECIALISATION — CHOOSES YOUR STYLE"`; `:1520` the page caption `"FOUR DIRECTIONS · ONE STYLE · TWELVE SKILLS TO LEARN"`. The onboarding tour card is `src/IdleXIdle.Core/Progression/Onboarding.cs:301-303` (`TourTarget.Specialisations`, spotlighting `InspectorPanel` per `MasteryScreen.cs:1036`).

**What it does NOT gate:** nothing in gear, vows, keystones, slot count or the Warren. A shared build code shows a foreign specialisation read-only and never applies it (`src/IdleXIdle.Game/VaultScreen.cs:1486-1493`, caption at `:1478` — *"READ IT, COPY THE IDEA. YOUR OWN BUILD STAYS THE SAME."*).

### 5. Every place the phrase "learned" or "discovered" appears in mastery code or copy, with file:line.

Complete, case-insensitive, over the mastery system and every file that consumes it. Keystone/vow/trait uses of the same word are marked **[other system]** so they are not mistaken for mastery state. `P` = player-visible copy, `C` = code identifier/expression, `//` = comment or doc-comment.

### Core — `src/IdleXIdle.Core/Builds/MasteryTree.cs`
| line | kind | text |
|---|---|---|
| 83 | // | "All twelve skills are **learned** on the tree; a champion that has **learned** nothing still fights" |
| 86 | // | "**Learning is PERMANENT** (D7, 2026-08-31 — it re-locked on respec until then)" |
| 87 | // | "is the **DISCOVERY** gate: taking it latches the skill into the **learned** set for good" |
| 89 | // | "so **learning** across respecs is the only way a four-slot build ever fills from twelve skills" |
| 154 | // | "── **DISCOVERY** IS PERMANENT (D7, 2026-08-31). Taking a road node LATCHES its skill here" |
| 155 | // | "Persisted (SaveGame.**Learned**Skills) and" |
| 158 | C | `private readonly HashSet<string> _learned = new(StringComparer.Ordinal);` |
| 209 | // | "THE LATCH (D7): a taught skill is **learned** the moment its node is taken, for good." |
| 210 | C | `if (MasteryCatalog.ById(id)?.GrantsSkillId is { } learned) _learned.Add(learned);` |
| 214 | // | "Restore the permanently **learned** skills. Unknown ids are dropped" |
| 215 | C | `public void RestoreLearned(IEnumerable<string> ids)` |
| 217 | C | `_learned.Clear();` |
| 220 | C | `if (SkillCatalogue.Find(id) is not null) _learned.Add(id);` |
| 224 | // | "Latch a skill as **learned** OUTSIDE the tree — a champion's birth skill" |
| 230 | C | `public void LearnSkill(string? skillId)` |
| 232 | C | `... _learned.Add(skillId);` |
| 256 | // | "**Learned** SKILLS stay" |
| 257 | // | "**learned** (D7): the respec moves points, never **discoveries**." |
| 304 | // | "Every skill this champion has EVER **learned**: the permanent latch, plus …" |
| 308 | C | `public IReadOnlySet<string> LearnedSkills()` |
| 310 | C | `var set = new HashSet<string>(_learned, StringComparer.Ordinal);` |

### Core — `src/IdleXIdle.Core/Builds/MasteryCatalog.cs`
| line | kind | text |
|---|---|---|
| 451 | // | "a champion that has **learned** nothing still fights with its birth skill." |
| 455 | // | "**LEARNING IS PERMANENT** (D7): the road is the **discovery** gate, and respec returns the points, never the skill" |

`MasteryLayout.cs`, `MasteryPoints.cs` and `Progression/MasteryLevel.cs` contain **zero** occurrences of either word.

### Game — `src/IdleXIdle.Game/MasteryScreen.cs`
| line | kind | text |
|---|---|---|
| 1520 | **P** | `"FOUR DIRECTIONS  ·  ONE STYLE  ·  TWELVE SKILLS TO LEARN"` (page caption) |
| 1807 | C | `var learned = roadDef is not null && Mastery.LearnedSkills().Contains(roadDef.Id);` |
| 1823 | C | icon tinted `learned \|\| taken ? Gold : Bone` |
| 1840 | **P** | `"LEARNED — FOR GOOD. RESPEC RETURNS THE POINTS, NEVER THE SKILL."` / `"LEARNING IS PERMANENT: RESPEC RETURNS THE POINTS, NEVER THE SKILL. EQUIP IT ON THE BUILD SCREEN."` |
| 1841 | C | colour choice on `learned` |
| 1927 | **P** | `MasteryKind.SkillRoad => "SKILL — LEARNED FOR GOOD"` (the node-kind heading) |
| 2201 | // | "A small gold mark says **DISCOVERED**" |
| 2202 | // | "the skill is **learned** for good, and the mark survives a respec that takes the points back." |
| 2207 | C | `var learnedRoad = Mastery.LearnedSkills().Contains(roadSkill);` |
| 2209 | C | glyph tint on `learnedRoad` |
| 2210 | C | `if (learnedRoad && !taken)` → the gold DISCOVERED diamond at `:2213` |

### Consumers — code
| file:line | kind | text |
|---|---|---|
| `src/IdleXIdle.Core/Builds/BuildComposer.cs:137` | // | "**learned** on the tree now and a new game has no points." |
| `src/IdleXIdle.Core/Builds/BuildComposer.cs:138` | C | `var taughtSkills = mastery.LearnedSkills().ToHashSet(...)` |
| `src/IdleXIdle.Core/Builds/BuildComposer.cs:154` | // | "**learned** on the mastery tree — and since D7 (2026-08-31) **learning** is PERMANENT" |
| `src/IdleXIdle.Core/Builds/BuildComposer.cs:131` | C | **[other system]** `learned` = keystones from the Dust tree |
| `src/IdleXIdle.Core/Persistence/SaveGame.cs:173` | // | "Skills **learned** FOR GOOD (D7, 2026-08-31)" |
| `src/IdleXIdle.Core/Persistence/SaveGame.cs:177` | C | `public List<string> LearnedSkills { get; init; } = new();` |
| `src/IdleXIdle.Core/Characters/Character.cs:120` | // | "Every one of the twelve is **learned** on the mastery tree now" |
| `src/IdleXIdle.Game/Game1.cs:808` | C+// | `_mastery.RestoreLearned(save.LearnedSkills);   // D7 — **discoveries** survive every respec` |
| `src/IdleXIdle.Game/Game1.cs:1061-1063` | C+// | "D7: the UNION (latch + currently-taken roads)…" / `LearnedSkills = _mastery.LearnedSkills()...` |
| `src/IdleXIdle.Game/Game1.cs:1815` | // | "HAMMER's PRESS is **learned** and the other five roads are not." (capture fixture) |
| `src/IdleXIdle.Game/Game1.cs:2514` | // | "Every skill is **learned** on the mastery tree" (capture fixture) |
| `src/IdleXIdle.Game/Game1.cs:4323` | // | "BIRTH SKILLS ARE **LEARNED** BY BEING SOMEONE (P9)" |
| `src/IdleXIdle.Game/Game1.cs:4326` | C | `_mastery.LearnSkill(_characters.Active.StartingSkillId);` |
| `src/IdleXIdle.Game/Game1.cs:3845` | // | quotes MASTERY's `"…TWELVE SKILLS TO LEARN"` caption |
| `src/IdleXIdle.Game/LoadoutScreen.cs:547` | C | `var set = Mastery.LearnedSkills().ToHashSet(...)` inside `KnownSkills()` |
| `src/IdleXIdle.Game/RosterScreen.cs:747` | C | `if (skill is not null && Mastery?.LearnedSkills().Contains(skill.Id) == true)` → prints `"ALREADY KNOWN"` at `:748` |

### Consumers — player-visible copy
| file:line | text |
|---|---|
| `src/IdleXIdle.Game/LoadoutScreen.cs:815` | `"LEARN ON {STYLE}'S ROAD"` (button) / `"LEARNED ON {STYLE}'S ROAD, ON THE MASTERY TREE."` (refusal) |
| `src/IdleXIdle.Game/LoadoutScreen.cs:1190,1194` | `"LEARNED {n} / 12  ·  LEARN MORE ON THE MASTERY TREE"` (library header) |
| `src/IdleXIdle.Game/LoadoutScreen.cs:1254` | tooltip: `"{name} — learned on {style}'s road, on the MASTERY tree."` |
| `src/IdleXIdle.Game/LoadoutScreen.cs:1481` | `"Pick a skill from the library and press EQUIP. A skill is learned on the MASTERY tree; once learned it is yours for good."` |
| `src/IdleXIdle.Game/LoadoutScreen.cs:1548` | `"LEARNED ON {STYLE}'S ROAD, ON THE MASTERY TREE"` (inspector lock line) |
| `src/IdleXIdle.Game/RosterScreen.cs:748` | `"ALREADY KNOWN"` |
| `src/IdleXIdle.Core/Progression/Onboarding.cs:283-284` | `"Twelve skills exist, two per style. You learn them on the MASTERY tree, and once learned they are yours for good — pick any learned one for the chosen slot."` |
| `src/IdleXIdle.Core/Progression/Onboarding.cs:303` | `"…its road teaches the Style's two skills — learned for good."` |
| `src/IdleXIdle.Core/Progression/Tutorial.cs:283-284` | `"Press B for BUILD. Pick a slot, then one of your learned skills from the library — skills are learned on the MASTERY tree (E), and stay learned for good."` |

### Same word, **[other system]** — do not touch when the mastery rule changes
`LoadoutScreen.cs:331, 360, 363, 366, 698, 699, 1101, 1102, 1103 ("LEARN THEM ON THE TRAITS SCREEN"), 1110, 1113 ("NO KEYSTONES LEARNED YET"), 1116` = Dust-tree keystones. `LoadoutScreen.cs:1578` = vows. `TraitsScreen.cs:1112 ("TRAIT LEARNED — AND IT IS PERMANENT"), 1463` = the Dust trait tree. `Onboarding.cs:108, 382, 383` = the trait LEARN button. `TraitRoads.cs:39` = structural unlocks. `LoadoutScreen.cs:84`, `RosterScreen.cs:873`, `TraitsScreen.cs:1717`, `Onboarding.cs:427`, `MasteryScreen.cs:1756` = incidental English ("discovers the body", "the lesson the mastery tree learned"), not state.

### Documentation that contradicts the code (BRIEF: believe the code)
| file:line | claim | reality |
|---|---|---|
| `design/gdd/skill-slots-and-skill-trees.md:910` | "…its road node teaches that style's second skill; **respec re-locks**" | **False since 2026-08-31.** `MasteryTree.Take` latches; `Respec` never clears it. |
| `tests/.../skill_slot_kinds_test.cs:211` | "Respec **RE-LOCKS** (designer, 2026-08-30)" | Stale; the test only passes because it builds the tree with `RestoreTaken`, which bypasses the latch. |
| `tests/.../MasteryTreeTests.cs:78` | method named `test_the_whole_tree_costs_two_hundred_and_fifty_six` | asserts **316** at `:84`. Name is stale. |
| decision "D7" itself | referenced by name in 8 code comments | **exists in no design document** — only in commit `a7cf35c` (2026-08-31) and the code. |

**Files this area will change**

- src/IdleXIdle.Core/Builds/MasteryTree.cs — owns `_learned` (:158), the latch in `Take` (:210), `RestoreLearned` (:215-221), `LearnSkill` (:230-233), `LearnedSkills()` (:308-314), `Respec()` (:259-263), `Refund()` (:243-253), `CanTake`'s one-specialisation rule (:201). Every semantic change lands here first.
- src/IdleXIdle.Core/Builds/MasteryCatalog.cs — the 12 `Teaches` declarations (:458-469) and the `Teaches` helper that sets `GrantsSkillId` (:477-485); the 6 `Spec` declarations (:433-447); the stale D7 comment block (:449-457); cost constants (:107-114).
- src/IdleXIdle.Core/Builds/BuildComposer.cs — the ONLY enforcement of skill access, at :138-139 (the set) and :156 (the `continue`). A signature-skill exemption (BRIEF §18) has to be written here.
- src/IdleXIdle.Core/Builds/PlayerLoadout.cs — `SetSkill` (:178-185) does not consult the learned set, and `Restore` (:275-329) has no mastery-based repair; BRIEF §19's loadout repair needs a home and this is it.
- src/IdleXIdle.Core/Persistence/SaveGame.cs — `LearnedSkills` (:177) and `MasteryTaken` (:170); BRIEF §21/§95 migration.
- src/IdleXIdle.Game/Game1.cs — load order `RestoreTaken`/`RestoreLearned` (:807-808), save write (:1060-1063), the per-frame birth-skill latch (:4326), points feed (:4443, :6798-6804), save-on-dirty (:3039), `ceremonyHolds` modality (:2932, :3968), the dev fixtures that use `RestoreTaken` to fake learned skills (:1954-1957, :2520-2523).
- src/IdleXIdle.Game/MasteryScreen.cs — respec arm/fire (:1183-1201, :1553), refund doors (:1134-1148, :1209-1214, :1262-1273), `TakeNode` (:1293-1322), the ceremony (:252-257, :323-329, :1158-1177, :1284-1290, :1612-1652), road/learned presentation (:1806-1841, :2203-2215), all the LEARNED FOR GOOD copy (:1520, :1840, :1927), the dead `Loadout`/`SkillWord` wiring (:48-49, :331) that §19 will need to make live.
- src/IdleXIdle.Game/LoadoutScreen.cs — `KnownSkills()` (:545-550), `SlotFilled` (:556), the EMPTY-SLOT rendering of an unknown skill (:1023-1029) that BRIEF §20 forbids, the library lock tiles (:1238-1254), the EQUIP refusal (:815), the header count (:1190-1194), the inspector lock line (:1481, :1548).
- src/IdleXIdle.Game/RosterScreen.cs — `ALREADY KNOWN` on a champion's starting skill (:747-748); BRIEF §9/§13 change what a champion switch means.
- src/IdleXIdle.Core/Characters/CharacterRoster.cs + Character.cs — the 10 `StartingSkillId` rows (CharacterRoster.cs:52,69,84,98,114,128,145,167,194,217) and `Character.StartingSkillId` (Character.cs:126) — the second, silent unlock channel that a Signature-Skill model must replace or exempt.
- src/IdleXIdle.Core/Progression/Onboarding.cs — tour copy at :283-284, :301-303 asserts "learned for good"; :295-308 is the Mastery tour.
- src/IdleXIdle.Core/Progression/Tutorial.cs — :283-284 asserts "stay learned for good".
- src/IdleXIdle.Game/VaultScreen.cs — reads a shared build's specialisation (:1486-1493); if specialisation semantics change the share-code display follows.
- tests/unit/IdleXIdle.Core.Tests/Builds/mastery_learned_permanence_test.cs — all six facts assert permanence; they must be inverted or deleted wholesale.
- tests/unit/IdleXIdle.Core.Tests/Builds/skill_slot_kinds_test.cs — :207-243 already asserts the NEW (re-locking) rule but via `RestoreTaken`; it is the template for BRIEF §99's tests and its comment at :211 is the surviving record of the pre-D7 design.
- tests/unit/IdleXIdle.Core.Tests/Builds/taught_tree.cs — the `Taught.Everything()` fixture (:31-39) builds a learned champion with `RestoreTaken` on road nodes only; if the access rule changes, this fixture changes and ~10 downstream test files change with it.
- tests/unit/IdleXIdle.Core.Tests/Builds/MasteryTreeTests.cs — :78-84 (tree cost, stale name), :326 (one capstone), :468/:482 (specialisation invariants), :519 (respec), :538/:564 (refund).
- tests/unit/IdleXIdle.Core.Tests/Builds/{loot_node_liveness_test.cs,mastery_node_liveness_test.cs,mastery_new_nodes_liveness_test.cs,reinforcement_liveness_test.cs,variation_liveness_test.cs,combat_balance_test.cs,shield_test.cs,BalanceSweepTests.cs,heal_balance_test.cs,SkillShapeBattleTests.cs} — every one poses a champion by `RestoreTaken`-ing the 12 SkillRoad nodes; all break if road nodes or the learned-set derivation move.
- tests/unit/IdleXIdle.Core.Tests/Characters/roster_parity_test.cs:131-139 and Builds/champion_starting_skill_test.cs — the birth-skill contract.
- tests/unit/IdleXIdle.Core.Tests/Persistence/SaveSystemTests.cs:328,345 — the MasteryTaken round-trip; add LearnedSkills migration coverage here.
- tests/unit/IdleXIdle.Game.Tests/loadout_feedback_test.cs:41 — poses a learned loadout via `RestoreTaken`.
- design/gdd/skill-slots-and-skill-trees.md — :910 states "respec re-locks", which is already false; §5 and the §8c row must be rewritten to whatever the new rule is.
- design/gdd/skill-and-trait-trees.md — :329 and the whole trait-tree half; the brief moves vows/keystones/structural unlocks out of it.

**Risks**

- THE LOAD-BEARING REASON FOR PERMANENCE IS STILL TRUE. `MasteryTree.cs:88-90` and commit a7cf35c: one specialisation is allowed at a time (`CanTake`, :201), and every SkillRoad hangs off a specialisation — so a hunter who cannot respec-and-keep can reach only TWO of the twelve skills, ever. Removing permanence without also breaking the one-specialisation rule, or without the new Signature/Trait channels supplying skills, leaves a 4-slot loadout that can never hold more than 2 tree skills + 1 birth skill. This is not a copy change; it is a content-reachability cliff.
- THE PERMANENCE LATCH IS ALREADY INCONSISTENT AND SOMETHING DEPENDS ON THE INCONSISTENCY. `Take()` latches `_learned` (MasteryTree.cs:210); `RestoreTaken()` (:177-184) does not. Every dev fixture (Game1.cs:1954-1957, :2520-2523) and about ten test files pose 'a champion who has learned everything' through `RestoreTaken` — i.e. through the OLD re-locking semantics. Two tests assert opposite rules and both pass (mastery_learned_permanence_test.cs:37-41 vs skill_slot_kinds_test.cs:241-242). Any refactor that unifies the two paths will flip one of those suites; whoever does it must know which behaviour each fixture was relying on.
- A LIVE SAVE-MIGRATION HAZARD EXISTS TODAY. A pre-D7 save carries no `LearnedSkills`; `RestoreLearned` CLEARS `_learned` and adds nothing (MasteryTree.cs:217, Game1.cs:808), so the account's skills survive only as the derived union with taken roads (MasteryTree.cs:311-313). `Save()` fires on the mastery screen's dirty flag (Game1.cs:3039), which the respec itself sets — so if a returning pre-D7 player's FIRST tree action is a respec, `LearnedSkills` is written empty and their skills are gone for good. BRIEF §95's migration must not inherit this ordering.
- THERE IS NO LOADOUT REPAIR CODE ANYWHERE, AND THE FALLBACK PRESENTATION IS WRONG. `MasteryScreen` never touches `Loadout` (its `Loadout` property at :331 and `SkillWord` at :48-49 are dead; the host still feeds it at Game1.cs:3033). An equipped-but-unknown skill today renders as 'EMPTY SLOT / PICK A SKILL FROM THE LIBRARY' (LoadoutScreen.cs:1023-1029) with the SkillId still silently in the slot. BRIEF §19 and §20 both target a code path that has never executed for a player, so it has never been photographed or tested.
- THE MODEL DOES NOT ENFORCE ACCESS AT ALL — ONLY THE COMPOSER DOES. `PlayerLoadout.SetSkill` (:178-185) writes any catalogue id into any slot. If access becomes revocable, the only thing standing between a saved loadout and a silently-dropped skill is `BuildComposer.cs:156`, and the failure mode is a slot that pays nothing while looking equipped. This is the project's own named failure species (built-tested-green code that never runs).
- CHAMPION BIRTH SKILLS ARE A SECOND, PERMANENT, POINT-FREE UNLOCK CHANNEL AND ARE EASY TO MISS. `Game1.cs:4326` latches the active champion's StartingSkillId into `_learned` EVERY FRAME. The 10 roster champions cover 9 of the 12 skills (CharacterRoster.cs:52,69,84,98,114,128,145,167,194,217; hammer_blow twice). Only field_pulse, sign_brand and drain_drink are mastery-exclusive. A refactor that makes mastery access temporary but leaves this channel permanent has changed almost nothing about actual skill availability.
- REVOKING ACCESS ALSO SILENTLY REVOKES DAMAGE. `Respec()` drops `Affinity()` to null, which removes the ×2.00/×1.15 style ring from every skill in the build (StyleAffinity.cs:44-50, read at SoloBattle.cs:901-909, 989, 1852-1854, 2072-2074) and every specialisation `BuildTrigger`. The current UI warns about none of this — the respec button says only 'PRESS AGAIN TO TAKE EVERY POINT BACK' (MasteryScreen.cs:1194, 1553). Whatever §19's unequip warning becomes, the affinity loss is the larger uncommunicated consequence and is already shipping.
- THE CEREMONY IS INFORMATIONAL, NOT TRANSACTIONAL, AND ITS 'SEAL' IS A NO-OP. `Mastery.Take` mutates state BEFORE the modal opens (MasteryScreen.cs:1296 then :1309); SEAL only clears `_specNodeId` (:1164). If the refactor gives the specialisation new meaning, anyone reading the ceremony as the commit point will be wrong — the commit already happened, and only `SpecUndoBtn` → `Mastery.Refund` (:1170) reverses it.
- A SPECIALISATION CANNOT BE PARTIALLY UNDONE ONCE ITS ROAD IS WALKED. `Refund` refuses to strand a node (MasteryTree.cs:248-251), so refunding a specialisation with its road taken returns false with 'ANOTHER NODE DEPENDS ON THIS ONE.' (:1271). The only exit is the full respec. A per-node access model must decide what happens to a road node whose specialisation is refunded.
- PLAYER-FACING COPY ASSERTING PERMANENCE IS SPREAD ACROSS FIVE FILES AND TWO TUTORIAL SYSTEMS: MasteryScreen.cs:1840/1927, LoadoutScreen.cs:815/1190/1254/1481/1548, RosterScreen.cs:748, Onboarding.cs:283-284/303, Tutorial.cs:283-284. Missing one leaves the game teaching a rule it no longer obeys — and the onboarding/tutorial strings are the ones least likely to be exercised by a capture.
- THE POINT ECONOMY IS TUNED AGAINST THE CURRENT RULE AND IS ALREADY FLAGGED AS UNRESOLVED. MasteryPoints.Scale = 0.9 gives ~66 points for six regions at depth 150 against a 316-point tree (20.9%); MasteryPoints.cs:31-38 explicitly defers 'should a champion earn more now that mastery also buys skills' to a playtest, and MasteryTreeTests.cs:237-241 owns the band. Making access temporary raises the real price of a build without changing the faucet, and MasteryTreeTests.cs:210 (two branches out of reach) and :238 (about-a-third reachable) will fight any correction.
- MASTERY MEANS TWO UNRELATED THINGS IN THIS CODEBASE. `src/IdleXIdle.Core/Progression/MasteryLevel.cs` is REGION mastery — a per-region conquest ladder read by RegionAutomation.cs:107-114, Career.cs:60, CorruptionScaling.MasteryLevelDust and Game1.cs:3247-3252, feeding Dust and trait points. It has nothing to do with the mastery TREE. It is listed in the audit's start-here files and is a trap for anyone grepping 'Mastery'.

## TRAIT TREE, TRAIT POINTS, STRUCTURAL NODES (Prestige/MemoryDust* + TraitsScreen)

### 1. The COMPLETE current trait catalogue: every node id, display name, road, cost, and exactly what it does. Mark each node STRUCTURAL / COMBAT / OTHER.

The catalogue is `MemoryDustTree.Catalog`, `src/IdleXIdle.Core/Prestige/MemoryDust.cs:278-541`. **51 nodes** (`grep -c 'new() { Id = '` → 51; pinned live by `tree.All.Count`). Total cost **226** (`Cost = N` values summed → 226; matches `MemoryDustTests.cs:34` which asserts `TotalTreeCost` in 215..240, and `design/gdd/skill-and-trait-trees.md:375`).

Per-road arithmetic (computed from the Cost fields):
- Spine (18 nodes): 2+3+5 + 1+2+2+3+3 + 1+2+3+2+3+2 + 1+2+2+3 + 4 = **46**
- Ruin: 4+6+8+12+6 (keystones) + 2+3+4 (minors) = **45**
- Aegis: 4+6+8+12+6 + 2+3+4 = **45**
- Avarice: 4+6+8+12+6 + 2+3+4 = **45**
- Artifice: 4+6+8+12+6 + 2+3+4 = **45**
- **46 + (4 × 45 = 180) = 226** ✓

`Road` defaults to `TraitRoad.Spine` (`MemoryDust.cs:50`) — the 18 spine nodes never write it. Roads are declared in `TraitRoad` (`MemoryDust.cs:29-45`); their printed names/sentences in `TraitRoads.cs:36-48`.

| # | id | Display name | Road | Cost | Requires | Exactly what it does (and where the wire lands) | Class |
|---|---|---|---|---|---|---|---|
| 1 | `socket_2` | KEYSTONE SOCKET II | Spine | 2 | — | +1 keystone socket (1→2). `DustEffects.KeystoneSockets` `DustEffects.cs:205-209` → `Game1.cs:786`, `Game1.cs:4334` → `PlayerLoadout.KeystoneCapacity` → enforced `PlayerLoadout.cs:225`, `PlayerLoadout.cs:337` | **STRUCTURAL** |
| 2 | `weave_5` | FIFTH SKILL SLOT | Spine | 3 | socket_2 | Skill capacity 4→5. `DustEffects.SkillSlots` `DustEffects.cs:212-216` → `Game1.cs:785`, `Game1.cs:3504` → `PlayerLoadout.SkillCapacity` → `PlayerLoadout.cs:95`, `Build.SlotCapacity` `Build.cs:273-277` | **STRUCTURAL** |
| 3 | `socket_3` | KEYSTONE SOCKET III | Spine | 5 | weave_5 | +1 socket (2→3). Same wire as #1 | **STRUCTURAL** |
| 4 | `vow_study_1` | LEARN VOWS I | Spine | 1 | — | Teaches `vow_complete`, `vow_deliberate`. `DustEffects.cs:107` (`VowGrants`) → `KnownVows` `DustEffects.cs:120-126` | **STRUCTURAL** |
| 5 | `vow_study_2` | LEARN VOWS II | Spine | 2 | vow_study_1 | Teaches `vow_pure`, `vow_frantic`. `DustEffects.cs:108` | **STRUCTURAL** |
| 6 | `vow_study_3` | LEARN VOWS III | Spine | 2 | vow_study_2 | Teaches `vow_singular`, `vow_bluntedge`. `DustEffects.cs:109` | **STRUCTURAL** |
| 7 | `vow_binding` | GEAR-SLOT VOWS | Spine | 3 | vow_study_3 | Teaches `vow_barefoot`, `vow_openhand`, `vow_bareskull`. `DustEffects.cs:112` | **STRUCTURAL** |
| 8 | `vow_sacrifice` | SACRIFICE VOWS | Spine | 3 | vow_study_3 | Teaches `vow_fragility`, `vow_reckless_offering`, `vow_unguarded`, `vow_unbound`. `DustEffects.cs:113-116` | **STRUCTURAL** |
| 9 | `ledger` | OPENS AUTO-SELL AND FORGE | Spine | 1 | — | **Pure gate — no method reads it.** Opens `filter_common`, `forge_insight`, `ks_greed`. Counted "wired" only because other nodes require it (`DustEffects.cs:252`). Its old QoL accessor `ShowExactNumbers` was deleted (`DustEffects.cs:168-171`) | **STRUCTURAL** |
| 10 | `filter_common` | AUTO-SELL COMMON DROPS | Spine | 2 | ledger | Auto-sell floor = Common. `DustEffects.AutoSellAtOrBelow` `DustEffects.cs:148` → `Game1.cs:4265` → `ForgeScreen.AutoSellFloor` (`ForgeScreen.cs:1065`), applied `ForgeScreen.cs:1748-1753`; mirrored to Vault `Game1.cs:3171` → `VaultScreen.cs:815` | **STRUCTURAL** |
| 11 | `filter_uncommon` | AUTO-SELL UNCOMMON DROPS | Spine | 3 | filter_common | Floor = Uncommon. `DustEffects.cs:147`. Hard-capped at Uncommon — no node can ever eat a Rare | **STRUCTURAL** |
| 12 | `forge_insight` | OPENS THE FORGE UPGRADES | Spine | 2 | ledger | **Pure gate — no method reads it.** Opens `efficient_forge`, `auto_merge`. Old accessor `ShowMergePreview` deleted (`DustEffects.cs:168-171`) | **STRUCTURAL** |
| 13 | `efficient_forge` | SALVAGE PAYS 15% MORE | Spine | 3 | forge_insight | Dismantle return ×1.15 (0.40 → 0.46), clamped ≤0.95. `DustEffects.DismantleRate` `DustEffects.cs:85-90` → `Game1.cs:4261` → `ForgeTuning.DismantleReturnRate` (`Forge.cs:34`) → `Forge.cs:225` | OTHER (economy rate) |
| 14 | `auto_merge` | AUTO-MERGE SPARE ITEMS | Spine | 2 | forge_insight | Turns on unprompted merge. `DustEffects.AutoMergeAfterRuns` `DustEffects.cs:155-159` → `Game1.cs:4338` → `ForgeScreen.AutoMergeOnOpen` (`ForgeScreen.cs:1107`), fires `ForgeScreen.cs:1716` | **STRUCTURAL** (automation) |
| 15 | `recall_1` | FASTER REGION MASTERY I | Spine | 1 | — | +5% region-mastery rate. `DustEffects.MasteryRate` `DustEffects.cs:68-75` → `Game1.cs:4379` → `RegionAutomation.RecordActiveKill` `RegionAutomation.cs:128-129` | OTHER (progression rate) |
| 16 | `recall_2` | FASTER REGION MASTERY II | Spine | 2 | recall_1 | +5% (10% total). Same wire | OTHER |
| 17 | `recall_3` | FASTER REGION MASTERY III | Spine | 2 | recall_2 | +5% (15% total). Same wire | OTHER |
| 18 | `recall_4` | FASTER REGION MASTERY IV | Spine | 3 | recall_3 | +5% (20% total, the cap). Same wire | OTHER |
| 19 | `ks_glass_cannon` | KEYSTONE — GLASS CANNON | Ruin | 4 | socket_2 | Learns GLASS CANNON: Damage ×2.0, Health ×0.5 (`Keystones.cs:33-36`). Node itself applies nothing — `DustEffects.LearnedKeystones` `DustEffects.cs:55-63` | **STRUCTURAL** |
| 20 | `ks_bloodlust` | KEYSTONE — BLOODLUST | Ruin | 6 | ks_glass_cannon | Learns BLOODLUST: Health ×0.75 + `BuildTrigger.Bloodlust` (`Keystones.cs:52-56`) | **STRUCTURAL** |
| 21 | `ks_blood_magic` | KEYSTONE — BLOOD MAGIC | Ruin | 8 | ks_bloodlust | Learns BLOOD MAGIC: SkillRate ×2.0 + `NoHealing` (`Keystones.cs:45-49`) | **STRUCTURAL** |
| 22 | `ks_reaper` | KEYSTONE — REAPER | Ruin | 12 | ks_blood_magic | Learns REAPER (terminal): SkillRate ×0.75 + `Splinter` (`Keystones.cs:78-82`) | **STRUCTURAL** |
| 23 | `ks_rend` | KEYSTONE — REND | Ruin | 6 | ks_glass_cannon | Learns REND: Damage ×0.9 + `Rend` (`Keystones.cs:142-146`) | **STRUCTURAL** |
| 24 | `ks_ironclad` | KEYSTONE — IRONCLAD | Aegis | 4 | socket_2 | Learns IRONCLAD: Health ×2.0, SkillRate ×0.5 (`Keystones.cs:39-42`) | **STRUCTURAL** |
| 25 | `ks_juggernaut` | KEYSTONE — JUGGERNAUT | Aegis | 6 | ks_ironclad | Learns JUGGERNAUT: no mods, `Zeal` + `NoHealing` (`Keystones.cs:103-106`) | **STRUCTURAL** |
| 26 | `ks_undying` | KEYSTONE — UNDYING | Aegis | 8 | ks_juggernaut | Learns UNDYING: Damage ×0.85 + `Undying` (`Keystones.cs:85-89`) | **STRUCTURAL** |
| 27 | `ks_titan` | KEYSTONE — TITAN | Aegis | 12 | ks_undying | Learns TITAN (terminal): Health ×3.0, SkillRate ×0.4 (`Keystones.cs:120-123`) | **STRUCTURAL** |
| 28 | `ks_dynamo` | KEYSTONE — DYNAMO | Aegis | 6 | ks_ironclad | Learns DYNAMO: Health ×0.85 + `Dynamo` (`Keystones.cs:162-166`) | **STRUCTURAL** |
| 29 | `ks_lodestone` | KEYSTONE — LODESTONE | Avarice | 6 | ks_greed | Learns LODESTONE: Rarity ×0.8 + `Lodestone` (`Keystones.cs:173-177`) | **STRUCTURAL** |
| 30 | `ks_capacitor` | KEYSTONE — CAPACITOR | Artifice | 6 | ks_echo | Learns CAPACITOR: SkillRate ×0.85 + `Capacitor` (`Keystones.cs:152-156`) | **STRUCTURAL** |
| 31 | `ks_greed` | KEYSTONE — GREED | Avarice | 4 | **ledger** | Learns GREED: Damage ×0.7, Haul ×2.0 (`Keystones.cs:66-69`) | **STRUCTURAL** |
| 32 | `ks_discerning_eye` | KEYSTONE — DISCERNING EYE | Avarice | 6 | ks_greed | Learns DISCERNING EYE: Haul ×0.5, Rarity ×2.0 (`Keystones.cs:72-75`) | **STRUCTURAL** |
| 33 | `ks_fortune` | KEYSTONE — FORTUNE | Avarice | 8 | ks_discerning_eye | Learns FORTUNE: Damage ×0.75, Rarity ×2.0 (`Keystones.cs:111-114`) | **STRUCTURAL** |
| 34 | `ks_hoarder` | KEYSTONE — HOARDER | Avarice | 12 | ks_fortune | Learns HOARDER (terminal): Rarity ×0.5 + `Hoarder` (`Keystones.cs:130-134`) | **STRUCTURAL** |
| 35 | `ks_echo` | KEYSTONE — ECHO | Artifice | 4 | **vow_study_1** | Learns ECHO: Damage ×0.6 + `Echo` (`Keystones.cs:59-63`) | **STRUCTURAL** |
| 36 | `ks_venomancer` | KEYSTONE — VENOMANCER | Artifice | 6 | ks_echo | Learns VENOMANCER: Damage ×0.8 + `Venom` (`Keystones.cs:92-96`) | **STRUCTURAL** |
| 37 | `artifice_vows` | VOWS PAY 25% MORE | Artifice | 8 | ks_venomancer | The **only non-keystone road node**: every sworn vow's *bonus* ×1.25. `DustEffects.VowPowerMultiplier` `DustEffects.cs:226-230` → `TreeShape` `DustEffects.cs:233-238` → `BuildComposer.cs:116` → `SoloBattle.VowFactor` `SoloBattle.cs:2559` | **COMBAT** |
| 38 | `ks_weaver` | KEYSTONE — WEAVER | Artifice | 12 | artifice_vows | Learns WEAVER (terminal): SkillRate ×0.70 + `Weaver` (`Keystones.cs:183-187`) | **STRUCTURAL** |
| 39 | `ruin_edge_1` | HARDER HITS I | Ruin | 2 | ks_bloodlust | `BuildMods(1.05,1,1,1,1)` — Damage ×1.05. `MemoryDust.cs:493` | **COMBAT** |
| 40 | `ruin_edge_2` | HARDER HITS II | Ruin | 3 | ks_blood_magic | Damage ×1.06. `MemoryDust.cs:496` | **COMBAT** |
| 41 | `ruin_edge_3` | HARDER HITS III | Ruin | 4 | ks_reaper | Damage ×1.12. `MemoryDust.cs:499` | **COMBAT** |
| 42 | `aegis_skin_1` | MORE HEALTH I | Aegis | 2 | ks_juggernaut | Health ×1.05. `MemoryDust.cs:504` | **COMBAT** |
| 43 | `aegis_skin_2` | MORE HEALTH II | Aegis | 3 | ks_undying | Health ×1.06. `MemoryDust.cs:507` | **COMBAT** |
| 44 | `aegis_skin_3` | MORE HEALTH III | Aegis | 4 | ks_titan | Health ×1.12. `MemoryDust.cs:510` | **COMBAT** |
| 45 | `avarice_purse_1` | MORE LOOT I | Avarice | 2 | ks_discerning_eye | Haul ×1.05. `MemoryDust.cs:516` → `SoloExpedition.cs:436-438` | **COMBAT** (run payout) |
| 46 | `avarice_purse_2` | RARER FINDS I | Avarice | 3 | ks_fortune | Rarity ×1.06. `MemoryDust.cs:519` → `SoloExpedition.cs:529` and chest rolls `Game1.cs:4271` | **COMBAT** (run payout) |
| 47 | `avarice_purse_3` | MORE AND RARER LOOT | Avarice | 4 | ks_hoarder | Haul ×1.08, Rarity ×1.05. `MemoryDust.cs:522` | **COMBAT** (run payout) |
| 48 | `artifice_hands_1` | FASTER SKILLS I | Artifice | 2 | ks_venomancer | SkillRate ×1.05. `MemoryDust.cs:527` | **COMBAT** |
| 49 | `artifice_hands_2` | FASTER SKILLS II | Artifice | 3 | artifice_vows | SkillRate ×1.06. `MemoryDust.cs:530` | **COMBAT** |
| 50 | `artifice_hands_3` | FASTER SKILLS III | Artifice | 4 | ks_weaver | SkillRate ×1.12. `MemoryDust.cs:533` | **COMBAT** |
| 51 | `attunement` | A KEEPSAKE — NO EFFECT | Spine | 4 | ks_glass_cannon, ks_ironclad, ks_greed, ks_echo, socket_3 | **Nothing in the fight.** `DustEffects.TreeComplete` `DustEffects.cs:173-177` → one subtitle string, `TraitsScreen.cs:943-945` | OTHER |

Counts: **STRUCTURAL 32** (3 capacity + 5 vow-study + 2 pure gates + 2 filters + 1 auto-merge + 19 keystone gates), **COMBAT 13** (12 attribute minors + `artifice_vows`), **OTHER 6** (`efficient_forge`, `recall_1..4`, `attunement`).

All 19 keystones in `Keystones.Catalog` are taught by exactly one node — 19 `GrantsKeystone` entries, one-to-one (pinned by `DustEffectsTests.cs:175 test_every_keystone_is_teachable_by_some_node`). All 13 vows in `Vows.Catalog` are taught by the five vow-study nodes (`DustEffectsTests.cs:278`).

### 2. Where trait POINTS come from: the earning rule, the cap, and how many a full career yields.

**The rule** — `Career.TraitPointsEarned(World)`, `src/IdleXIdle.Core/Progression/Career.cs:56-62`:

```csharp
var total = world.ConqueredIds.Count + 2 * world.PeakCorruptionTier;
foreach (var def in Regions.All) total += (int)world.RegionFarm(def.Id).MasteryLevel;
return total;
```

Three faucets:
1. **1 per region conquered** — `World.ConqueredIds` (`Regions.cs:145`), set by `World.Conquer` (`Regions.cs:138-143`), fired at `Game1.cs:4448`.
2. **2 per corruption tier reached** — `World.PeakCorruptionTier` (`Regions.cs:162`), the *peak*, not the live tier, so easing never takes a point back (`Regions.cs:157-161`).
3. **1 per region-mastery level, per region** — `MasteryLevel` is an enum cast to int, 0..3 (`MasteryLevel.cs:12-18`), derived from accrued RMP against three thresholds (`RegionAutomation.cs:107-116`). RMP only grows from the champion's own kills (`RegionAutomation.RecordActiveKill`, `RegionAutomation.cs:128-129`).

**Caps.** There is no cap on the number itself; the cap is content:
- regions: **6** (`Regions.All`, `Regions.cs:59-88`)
- corruption ladder: **`CorruptionScaling.MaxTier = 5`** (`CorruptionScaling.cs:17`), enforced by `World.CanDeepenCorruption` (`Regions.cs:168`)
- mastery: **3 per region** (`MasteryLevel.Perfected = 3`)

**Full career yield:** `6 (conquests) + 2 × 5 (corruption) + 6 × 3 (mastery) = 6 + 10 + 18 = **34**`. This is not an estimate — `MemoryDustTests.cs:233-249 test_two_terminals_are_out_of_reach` builds a world at full content and asserts `Assert.Equal(34, budgetAtFullContent)`. `Career.cs:52-55` states the same number.

**34 against a 226-point tree = 15.0% of the catalogue buyable in a whole career** (34/226 = 0.1504). `MemoryDustTree.IsComplete` (`MemoryDust.cs:206`) is therefore unreachable in play; only tests reach it. The screen's own tally admits this — the "226 FOR EVERYTHING" line was removed, `TraitsScreen.cs:988-990`, `TraitsScreen.cs:1173-1174`.

**Derived, never banked.** `MemoryDustTree.Earned` is re-set from the world every frame: `Game1.cs:4444 _dust.SetEarned(TraitPointsEarned());` → `Game1.cs:6841 private int TraitPointsEarned() => Career.TraitPointsEarned(_world);` → `MemoryDust.cs:147 SetEarned`. `Spent` = sum of owned costs (`MemoryDust.cs:142`); `Available = max(0, Earned − Spent)` (`MemoryDust.cs:144`).

**A feedback loop exists:** `recall_1..4` speed region-mastery accrual (`DustEffects.MasteryRate`), and region mastery is itself a trait-point faucet — so 8 points spent on recall make the remaining 18 mastery points arrive up to 20% sooner. It does not change the 34 total (mastery level caps at 3).

**Screen gate:** `Activity.Traits` opens at `TraitPointsEarned >= 1` (`Unlocks.cs:159`), caption "Earn a trait point" (`Unlocks.cs:191`); host fact fed at `Game1.cs:3521`. A free-points hint fires at `Onboarding.cs:588-589`.

### 3. How a purchase is stored and restored (the save fields).

**Two save fields, both on `SaveGame` (`src/IdleXIdle.Core/Persistence/SaveGame.cs`):**

| Field | Line | Type | Holds |
|---|---|---|---|
| `MemoryDust` | `SaveGame.cs:64` | `int` | the **Dust wallet**, not trait points — see the note below |
| `MemoryDustUnlocks` | `SaveGame.cs:65` | `List<string>` | the owned node ids, verbatim |

**Write:** `SaveGame.cs:572-573` — `MemoryDust = prestige?.MemoryDust ?? 0`, `MemoryDustUnlocks = prestige?.OwnedIds.ToList() ?? new List<string>()`. `OwnedIds` is `MemoryDust.cs:203`.

**Read:** exactly one call site — `Game1.cs:770 _dust.Restore(save.MemoryDust, save.MemoryDustUnlocks);` → `MemoryDustTree.Restore` `MemoryDust.cs:195-201`:
```csharp
MemoryDust = Math.Max(0, dust);
_owned.Clear();
foreach (var id in owned) if (_unlocks.ContainsKey(id)) _owned.Add(id);
```
**An id the catalogue no longer contains is silently dropped (`MemoryDust.cs:200`).** There is no migration hook, no version-gated remap, no logging. `SaveGame.CurrentVersion = 3` (`SaveGame.cs:17`) and the only version guard is a refusal to load a *newer* save (`SaveGame.cs:520`).

**Trait points themselves are NOT persisted.** `Earned` is re-derived every frame from world state (`Game1.cs:4444`), and `Spent` is re-derived from the owned set (`MemoryDust.cs:142`). What *is* persisted are the facts the derivation reads:
- `ConqueredRegions` — `SaveGame.cs:83`, written `:575`, restored via `World.RestoreConquered` (`Regions.cs:146-150`)
- `CorruptionTier` / `CorruptionPeak` — `SaveGame.cs:97`,`:101`, written `:577-578`, restored `SaveGame.cs:757` → `World.RestoreCorruption` (`Regions.cs:200-204`)
- `RegionFarms[].MasteryPoints` — `RegionFarmSave` `SaveGame.cs:386-396`, written `SaveGame.cs:579-585`

**Purchases are one-way and un-refundable.** `MemoryDustTree.Purchase` (`MemoryDust.cs:186-192`) adds to `_owned` and nothing else; the cost is implicit in `Spent`. There is **no respec, no refund and no removal path anywhere in src/**. `Restore` is the only way the owned set shrinks, and only by the caller passing a shorter list.

**Two things share one class.** `MemoryDustTree` is simultaneously (a) the trait tree and (b) the **Memory Dust wallet**, a separate currency with its own faucets and sinks that has nothing to do with traits: `AddDust` (`MemoryDust.cs:158`) is called from offline Warren yield `Game1.cs:922`, live Warren yield `Game1.cs:1266`, mastery milestones `Game1.cs:3250`, conquest `Game1.cs:4450`, corruption deepening `Game1.cs:6542`; `Spend` (`MemoryDust.cs:164-169`) is called for Warren facility upgrades `Game1.cs:1302` and expedition checkpoints `Game1.cs:4221`. Deleting the trait tree must not delete this wallet.

**Dev-only mutation paths that also write ownership** (not save paths, but they call `Restore`/`Purchase`): `TraitsScreen.cs:356-357` (`DevPoseLit` grants a node and its prerequisites so a capture can photograph the flourish), `Game1.cs:1776-1777`, `1981`, `1992-1993`, `2455-2458`, `2511`, `2654-2655`, `2682`.

### 4. Every consumer of a structural trait node: file:line where the game asks "does the player own this node" and what it gates.

Every `Owns(...)` call in `src/` (13 in Core, all inside `DustEffects`; the rest in `TraitsScreen`, which only draws). No other file calls `MemoryDustTree.Owns` — the tree reaches gameplay **only** through `DustEffects`.

**A. The ownership questions themselves (`src/IdleXIdle.Core/Prestige/DustEffects.cs`)**

| Line | Question | Gates |
|---|---|---|
| `DustEffects.cs:208` | `Owns("socket_2")`, `Owns("socket_3")` | keystone socket count, `1 + n` |
| `DustEffects.cs:215` | `Owns("weave_5")` | skill slots, `4 + n` |
| `DustEffects.cs:124` | `Owns(kv.Key)` over `VowGrants` (`vow_study_1/2/3`, `vow_binding`, `vow_sacrifice`) | which of the 13 vows are known |
| `DustEffects.cs:147` | `Owns("filter_uncommon")` | auto-sell floor = Uncommon |
| `DustEffects.cs:148` | `Owns("filter_common")` | auto-sell floor = Common |
| `DustEffects.cs:158` | `Owns("auto_merge")` | forge auto-merge on |
| `DustEffects.cs:88` | `Owns("efficient_forge")` | dismantle return ×1.15 |
| `DustEffects.cs:73` | `Owns("recall_1".."recall_4")` | region-mastery rate +5% each |
| `DustEffects.cs:229` | `Owns("artifice_vows")` | vow bonus ×1.25 |
| `DustEffects.cs:176` | `Owns("attunement")` | one subtitle string |
| `DustEffects.cs:59` | `Owns(u.Id)` where `u.GrantsKeystone != null` | which of 19 keystones are learnable |
| `DustEffects.cs:45` | `Owns(u.Id)` for every node | combined `BuildMods` from the 12 attribute minors |

`ledger` and `forge_insight` are asked by **nobody**. They are "wired" only by `DustEffects.cs:252`, which declares every id that appears in some node's `Requires` list to be wired. Their real function is the requirement graph.

**B. Where each answer is consumed**

| Structural node(s) | Consumer file:line | What it gates |
|---|---|---|
| `socket_2`, `socket_3` | `Game1.cs:786` (on load), `Game1.cs:4334` (every frame) | sets `PlayerLoadout.KeystoneCapacity` |
| ” | `PlayerLoadout.cs:225` | `ToggleKeystone` refuses when `_keystoneIds.Count >= Math.Min(MaxKeystones, KeystoneCapacity)` — the socket the player did not buy |
| ” | `PlayerLoadout.cs:337` | `Restore` truncates saved keystones to the bought socket count |
| ” | `LoadoutScreen.cs:847-848` | SOCKET button disabled: "ONLY {n} SOCKET(S) — MORE ON THE TRAITS SCREEN." |
| ” | `LoadoutScreen.cs:1103`, `1474` | "{worn} / {capacity} SOCKETS" readouts |
| ” | `Game1.cs:6606` | stats readout "KEYSTONE SOCKETS" |
| `weave_5` | `Game1.cs:785` (on load, floored by saved skill count), `Game1.cs:3504` inside `ApplySkillCapacity` (`Game1.cs:3501-3507`), called `Game1.cs:4335` | sets `PlayerLoadout.SkillCapacity` |
| ” | `PlayerLoadout.cs:95` | `AddSkill` returns −1 when full |
| ” | `PlayerLoadout.cs:286` | `Restore` truncates rows to `SkillCapacity` |
| ” | `PlayerLoadout.cs:267` → `BuildComposer.cs:117,125-126` | `Build.SlotCapacity`, and the active/passive split `Build.ActiveSlotsFor` (`Build.cs:340`) |
| ” | `SoloBattle.cs:2587` | `BuildContext.SkillSlots` — what VOW OF COMPLETION measures |
| ” | `LoadoutScreen.cs:347`, `687`, `310`; `HuntScreen.cs:3324`; `Game1.cs:6604` | slot rows drawn / ADD SKILL enabled |
| `vow_study_1/2/3`, `vow_binding`, `vow_sacrifice` | `BuildComposer.cs:148` | **the pay gate**: `DustEffects.KnowsVow(tree, s.VowId) ? Vows.ById(...) : null` — an untaught vow composes as no vow |
| ” | `Game1.cs:801-802` | on load, strips a worn vow the account no longer knows |
| ” | `LoadoutScreen.cs:538` (`Known`), used `631`, `1590`, `1632` | the vow picker's menu |
| ” | `LoadoutScreen.cs:1578` | "VOWS ARE LEARNED ON THE TRAITS SCREEN" |
| `filter_common`, `filter_uncommon` | `Game1.cs:4265` | `ForgeScreen.AutoSellFloor` (`ForgeScreen.cs:1065`) |
| ” | `ForgeScreen.cs:1748-1753` | wearables ≤ floor are sold for gleam at chest open instead of entering the bag |
| ” | `Game1.cs:3171` → `VaultScreen.cs:238`, applied `VaultScreen.cs:815` | same rule in the Vault |
| `auto_merge` | `Game1.cs:4338` → `ForgeScreen.AutoMergeOnOpen` (`ForgeScreen.cs:1107`) | `ForgeScreen.cs:1716` merges the haul on chest open |
| ” | `Game1.cs:3172` → `VaultScreen.cs:241`, caption `VaultScreen.cs:823` | Vault mirror |
| `efficient_forge` | `Game1.cs:4261` | `ForgeTuning.DismantleReturnRate` → `Forge.cs:225`, `Forge.cs:327-329` |
| `recall_1..4` | `Game1.cs:4379` | `RegionAutomation.RecordActiveKill(rate)` `RegionAutomation.cs:128-129` |
| 19 `ks_*` gates | `BuildComposer.cs:129-132` | only a *learned* keystone can be socketed into the composed build |
| ” | `LoadoutScreen.cs:695`, `909`, `1098`, `1468`, `355`, `637` | the keystone picker's list and toggle |
| ” | `Game1.cs:1995` | dev fixture |
| `artifice_vows` | `BuildComposer.cs:116` (via `DustEffects.TreeShape`) → `SoloBattle.cs:2559` | scales the *bonus* of an active vow by 1.25 |
| `attunement` | `TraitsScreen.cs:943` | swaps the screen subtitle. Nothing else |
| `ledger`, `forge_insight` | **no consumer** | requirement-graph gates only |
| the whole tree object | `PlayerLoadout.cs:253` `ToBuild(tree, …)` — called `HuntScreen.cs:829`, `LoadoutScreen.cs:542/559/585`, `GearScreen.cs:652`, `TrainingScreen.cs:455`, `Game1.cs:4271` | build composition |
| ” | `MasteryScreen.cs:336` | **dead coupling** — the property is assigned at `MasteryScreen.cs:1048` and `1327` and never read anywhere in the file |

**Two nodes whose NAME or DESCRIPTION disagrees with the code:**
- `ledger` is named **"OPENS AUTO-SELL AND FORGE"** (`MemoryDust.cs:341`). It does not open the Forge. `Activity.Forge` opens on `ChestsEverHeld >= 1 || ItemsOwned >= 2` (`Unlocks.cs:127`), caption "Earn a chest from a boss" (`Unlocks.cs:184`) — no trait node is consulted. Its *description* (`MemoryDust.cs:342`) is accurate; the name is not. **There is no Forge-access trait — brief §55's premise does not hold against this code.**
- `filter_common`'s description says items are sold "**the moment they drop**" (`MemoryDust.cs:345`) and `auto_merge`'s says the forge merges "**after every expedition**" (`MemoryDust.cs:357`). Both actually fire only when a chest is opened (`ForgeScreen.cs:1748`, `ForgeScreen.cs:1716`). There is no wave-drop item path: run loot reaches the bag only through `ForgeScreen.AddLoot` (`ForgeScreen.cs:1112`), whose only non-dev caller is the save restore at `Game1.cs:1369`.

### 5. What DustEffects does and how the tree feeds combat.

**What `DustEffects` is** (`src/IdleXIdle.Core/Prestige/DustEffects.cs`, 254 lines, static): the single translation layer between "which node ids are owned" and gameplay. Its stated purpose (`DustEffects.cs:12-26`) is to make dormant nodes impossible: `DustEffectsTests.cs:33 test_every_unlock_in_the_catalog_is_actually_wired_to_something` asserts every catalogue id is in `WiredIds`, and `:49 test_nothing_is_wired_that_does_not_exist` asserts the reverse.

**Its full public surface — 11 methods:**

| Method | Line | Returns | Read by |
|---|---|---|---|
| `TreeMods(tree)` | `:42-46` | `BuildMods` — multiplicative `Sum` over every owned node's `Mods` | `BuildComposer.cs:111` |
| `LearnedKeystones(tree)` | `:55-63` | `IReadOnlyList<Keystone>` — the menu, not the plate | `BuildComposer.cs:129`, `LoadoutScreen.cs:355/637/695/909/1098/1468`, `Game1.cs:1995` |
| `MasteryRate(tree)` | `:68-75` | `1f + 0.05 × recall nodes` (max 1.20) | `Game1.cs:4379` |
| `DismantleRate(tree, base)` | `:85-90` | `base × 1.15` if `efficient_forge`, clamped `≤0.95` | `Game1.cs:4261` |
| `KnownVows(tree)` | `:120-126` | the vows the account has studied | `Game1.cs:802`, `LoadoutScreen.cs:538` |
| `KnowsVow(tree, id)` | `:128-132` | bool | `BuildComposer.cs:148`, `Game1.cs:801` |
| `AutoSellAtOrBelow(tree)` | `:144-150` | `Rarity?`, capped at Uncommon | `Game1.cs:4265` |
| `AutoMergeAfterRuns(tree)` | `:155-159` | bool | `Game1.cs:4338` |
| `TreeComplete(tree)` | `:173-177` | `Owns("attunement")` | `TraitsScreen.cs:943` |
| `KeystoneSockets(tree)` | `:205-209` | `1 + socket_2 + socket_3` | `Game1.cs:786`, `Game1.cs:4334` |
| `SkillSlots(tree)` | `:212-216` | `4 + weave_5` | `Game1.cs:785`, `Game1.cs:3504` |
| `VowPowerMultiplier(tree)` | `:226-230` | `1.25f` if `artifice_vows` else `1f` | `TreeShape` |
| `TreeShape(tree)` | `:233-238` | `SkillShape` carrying only `VowPowerMultiplier` | `BuildComposer.cs:116` |
| `WiredIds` | `:240-253` | the hand-written `NamedWires` set (`:187-195`) ∪ every node with `Mods`/`GrantsKeystone` ∪ every id anything `Requires` | tests only |

**How the tree feeds combat — four channels, all through `BuildComposer.Compose` (`src/IdleXIdle.Core/Builds/BuildComposer.cs:94-132`), which is the one place the tree meets the sim.** Entry is `PlayerLoadout.ToBuild(tree, mastery, character, progress)` (`PlayerLoadout.cs:253-268`).

1. **Numbers.** `BuildComposer.cs:111`: `PassiveMods = DustEffects.TreeMods(tree).Combine(character?.Mods ?? None)` → `Build.PassiveMods` (`Build.cs:498`) → `Build.Resolve(hunter)` `Build.cs:479` (`fromKeystones.Combine(fromStats).Combine(PassiveMods)`) → read by `SoloBattle.cs:567`, `SoloBattle.cs:2503` (max health), `SoloBattle.cs:2579` (`DescribeBuild`) and `SoloExpedition.cs:436`. `BuildMods` is `(Damage, Health, SkillRate, Haul, Rarity)` (`Build.cs:29-34`), combined multiplicatively (`Build.cs:39-40`). Only the 12 attribute minors carry non-`None` `Mods`; the other 39 nodes contribute `BuildMods.None`.
2. **Keystones.** `BuildComposer.cs:129-132`: `learned = DustEffects.LearnedKeystones(tree)`, then `build.Take(k)` for each socketed id **that is in that list**. `Build.Take` (`Build.cs:438-445`) caps at `Build.KeystoneSlots = 3` (`Build.cs:435`). The keystone's own `Mods` fold in at `Build.cs:463`; its `Grants` become `BuildTrigger`s the sim asks about. **This is the tree's largest combat channel** — GLASS CANNON alone is Damage ×2.0 / Health ×0.5, twenty times any minor.
3. **Vows.** `BuildComposer.cs:148`: a `VowId` the tree has not taught composes as `null`, so the skill keeps firing and the bonus is simply not paid. A taught, *active* vow pays via `SoloBattle.VowFactor` (`SoloBattle.cs:2549-2560`), whose bonus term is scaled by `shape.VowPowerMultiplier` — which `artifice_vows` sets to 1.25 through `BuildComposer.cs:116`.
4. **Capacity, which the sim reads as a rule.** `BuildComposer.cs:117` `SlotCapacity = slotCapacity` (host-derived from `weave_5`), split into `ActiveCapacity`/`PassiveCapacity` at `:125-126`. `SoloBattle.DescribeBuild` (`SoloBattle.cs:2587`) puts `build.SlotCapacity` into `BuildContext.SkillSlots`, which VOW OF COMPLETION tests against; `KeystonesWorn` at `SoloBattle.cs:2594` likewise.

**Loot is a fifth channel that is not combat but rides the same `BuildMods`:** `Haul` at `SoloExpedition.HaulForWave` (`SoloExpedition.cs:436-438`), `Rarity` at `SoloExpedition.cs:529` (`Haul.Quality`) and at chest opening via `Game1.cs:4271 _forge.RarityBonus = wornBuild.Resolve(_hunter).Rarity` → `ForgeScreen.cs:1741`.

**Net combat contribution if the tree were deleted whole:** the 12 attribute minors are worth at most ×1.05·1.06·1.12 ≈ **×1.246** on one axis (and the whole set is capped by `DustEffectsTests.cs:118`), `artifice_vows` is ×1.25 on vow bonuses only — but **all 19 keystones and all 13 vows are unreachable without the tree**, and skill/keystone capacity would collapse to `4` and `1` (`DustEffects.cs:208`, `:215` return the base when nothing is owned).

**Files this area will change**

- src/IdleXIdle.Core/Prestige/MemoryDust.cs — the 51-node catalogue (:278-541), the purchase/ownership engine (:103-233), the trait-point ledger (Earned/Spent/Available :140-147) AND the Memory Dust wallet (:114, :158, :164). Four responsibilities in one class; only the first two are the trait tree.
- src/IdleXIdle.Core/Prestige/DustEffects.cs — every wire from node id to gameplay (13 Owns() calls, 13 public methods). Delete a node and this file is where the game stops asking about it.
- src/IdleXIdle.Core/Prestige/TraitRoads.cs — the five road identities and their one-line sentences; consumed by TraitsScreen.cs:1447 and TraitNamesTests.
- src/IdleXIdle.Core/Prestige/TraitTreeLayout.cs — authored world positions for all 51 nodes plus a fallback placer; purely a diagram concern, dies with the tree UI.
- src/IdleXIdle.Core/Prestige/MemoryDustText.cs — Describe/Sheet/CostSentence/RequiresSentence/ModsSentence; every trait sentence the UI prints, including the 'Costs N trait points.' copy.
- src/IdleXIdle.Core/Progression/Career.cs:56-62 — TraitPointsEarned, the only earning rule. Also the only consumer of PeakCorruptionTier for points.
- src/IdleXIdle.Core/Progression/Unlocks.cs — Activity.Traits enum member (:53), its gate (:159), requirement caption (:191), headline (:213), and the progression SkillSlots gate (:239-246) that hands over to the tree at four.
- src/IdleXIdle.Core/Progression/Onboarding.cs — TourTarget.TraitTree/TraitPoints/TraitDetail (:104-110), the three-card Traits tour (:373-381), HintFacts.TraitPointsFree (:142) and the free-points hint (:588-589).
- src/IdleXIdle.Core/Progression/MasteryLevel.cs:9-10 — doc-comment ties the mastery ladder to Career.TraitPointsEarned.
- src/IdleXIdle.Core/Builds/BuildComposer.cs:94-148 — the one place the tree reaches the sim: TreeMods, TreeShape, LearnedKeystones, KnowsVow.
- src/IdleXIdle.Core/Builds/PlayerLoadout.cs — SkillCapacity/KeystoneCapacity (:79-81), their enforcement (:95, :225, :286, :337), and ToBuild(MemoryDustTree, …) (:253-268).
- src/IdleXIdle.Core/Builds/Build.cs — SkillSlots const (:249), SlotCapacity (:273-277), KeystoneSlots const (:435), Take (:438-445), PassiveMods (:498), Resolve (:459-479). Doc-comments at :246, :254-258, :430 name trait node ids.
- src/IdleXIdle.Core/Builds/Keystones.cs — 19 keystones reachable ONLY through ks_* trait nodes. If the tree goes, these need a new owner or they become dead content.
- src/IdleXIdle.Core/Builds/Vows.cs — 13 vows reachable ONLY through the five vow-study nodes.
- src/IdleXIdle.Core/Automation/RegionAutomation.cs:107-129 — MasteryLevel thresholds (the point faucet) and RecordActiveKill(rate) (the recall_* consumer).
- src/IdleXIdle.Core/Persistence/SaveGame.cs — MemoryDust (:64) and MemoryDustUnlocks (:65) fields, written :572-573; ConqueredRegions/:575, CorruptionTier+Peak/:577-578, RegionFarms/:579-585 are the facts trait points derive from. CurrentVersion = 3 (:17).
- src/IdleXIdle.Core/Encounters/Regions.cs — World.ConqueredIds (:145), PeakCorruptionTier (:162), the 6-region list (:59-88). Any change to the world's shape moves the 34-point budget.
- src/IdleXIdle.Core/Encounters/CorruptionScaling.cs:17 — MaxTier = 5, half the point budget.
- src/IdleXIdle.Game/TraitsScreen.cs — 2,078 lines: the whole free-canvas tree screen, camera, purchase flow (TryBuy :787, Buy :801), terminal arm-and-confirm, unlock flourish, wire pulse, point plate (:977-1001), tour spotlights (:513-520), dev capture hooks (:316-390).
- src/IdleXIdle.Game/Game1.cs — _dust field (:447, :1165), save restore (:770, :785-786, :801-802), per-frame sync (:4261, :4265, :4271, :4334-4338, :4379, :4443-4444), ApplySkillCapacity (:3501-3507), GuideUnlockFacts (:3510-3521), TraitPointsEarned (:6841), stats readouts (:6604-6606), nav rail order (:6872), and ~12 dev fixture blocks that purchase node ids by name (:1776, :1981, :1992, :2455-2458, :2511, :2654, :2682).
- src/IdleXIdle.Game/LoadoutScreen.cs — every keystone and vow surface reads DustEffects: :355, :538, :637, :695, :847-848, :909, :1098, :1103, :1468, :1474, :1578. All the 'MORE ON THE TRAITS SCREEN' copy lives here.
- src/IdleXIdle.Game/ForgeScreen.cs — AutoSellFloor (:1065), RarityBonus (:1098), AutoMergeOnOpen (:1107), applied at :1716 and :1748-1753.
- src/IdleXIdle.Game/VaultScreen.cs — AutoSellFloor (:238), AutoMergeOnOpen (:241), applied :815 and :823.
- src/IdleXIdle.Game/GearScreen.cs:148/645/652, HuntScreen.cs:699/824/829, TrainingScreen.cs:56/455 — each holds a MemoryDustTree purely to call ToBuild and to build a cache signature from OwnedIds.
- src/IdleXIdle.Game/MasteryScreen.cs:336/1048/1327 — holds a MemoryDustTree that is assigned and never read. Dead coupling; safe to delete.
- tests/unit/IdleXIdle.Core.Tests/Prestige/MemoryDustTests.cs — 11 tests: finite horizon (:16), completability (:45), prerequisites (:101), permanence (:115), affordability (:128), capstone gating (:140), save round-trip (:161), no destructive effect (:182), cycle rejection (:195), harmlessness (:212), and the load-bearing two-terminals budget test (:233).
- tests/unit/IdleXIdle.Core.Tests/Prestige/DustEffectsTests.cs — 24 tests, including the two that make dormant nodes impossible (:33, :49) and every per-wire liveness test.
- tests/unit/IdleXIdle.Core.Tests/Prestige/trait_gates_test.cs — 3 tests: untaught vow pays nothing (:29), restore honours bought sockets (:47), PERFECTED pays the third point (:65).
- tests/unit/IdleXIdle.Core.Tests/Prestige/TraitNamesTests.cs — 8 tests pinning ids (:67), names, road identities, and the layout invariants (:184, :205).
- tests/unit/IdleXIdle.Core.Tests/Prestige/TraitDescriptionsTests.cs — 8 tests pinning description shape, banned words, font gate, and Description↔Mods agreement.
- tests/unit/IdleXIdle.Core.Tests/Prestige/MemoryDustTextTests.cs — 8 tests pinning the printed sentences.
- tests/unit/IdleXIdle.Core.Tests/Builds/PassiveTreeTests.cs — buys socket_2/ks_glass_cannon/ks_ironclad/ruin_edge_1 by id (:87, :121, :131, :143-144, :210, :247-248).
- tests/unit/IdleXIdle.Core.Tests/Progression/career_test.cs — the point rule (:18-27).
- tests/unit/IdleXIdle.Core.Tests/Progression/unlocks_test.cs, onboarding_test.cs, onboarding_hints_test.cs — the Traits gate and the trait-point hint copy.
- tests/unit/IdleXIdle.Core.Tests/Persistence/SaveSystemTests.cs:466-483 — the trait-tree save round-trip, asserting recall_1 and socket_2 survive.
- tests/unit/IdleXIdle.Core.Tests/Builds/{BalanceSweepTests,combat_balance_test,heal_balance_test,loot_node_liveness_test,reinforcement_liveness_test,shield_test,skill_slot_kinds_test,taught_tree,variation_liveness_test,blow_vertical_slice_test,mastery_node_liveness_test}.cs and Characters/roster_parity_test.cs — all construct a MemoryDustTree and several call SetEarned/Restore with node ids as fixtures.
- tests/unit/IdleXIdle.Game.Tests/traits_feedback_test.cs — 13 tests on the trait screen's glide and wire-pulse motion; dies with the screen.
- design/gdd/skill-and-trait-trees.md — §3.4-3.6, §4.2, §5 are the ONLY prose spec of the live tree, and are currently ACCURATE (51 nodes, 226 total, 34 points, 15%). It must be rewritten, not just re-headed.
- design/gdd/memory-dust-prestige-system.md — already headed SUPERSEDED (:3); describes a Dust-bought model that is not in the runtime.
- design/gdd/vows.md, design/gdd/characters.md, design/gdd/game-flow.md, docs/PROJECT-NOTES.md — all reference the trait tree / vow_study gating.

**Risks**

- A deleted node id is silently forgotten on load. MemoryDustTree.Restore (MemoryDust.cs:200) drops any id not in the catalogue, with no logging and no version-gated migration hook (SaveGame.CurrentVersion = 3, SaveGame.cs:17; the only version check is a refusal to load a newer save, SaveGame.cs:520). If the refactor removes ids without reading save.MemoryDustUnlocks BEFORE Restore, every existing player silently loses their keystone sockets, fifth skill slot, vow knowledge, auto-sell filters and forge upgrades. Brief §58 forbids exactly this; the code currently makes it the default behaviour.
- MemoryDustTree is TWO systems in one class. It is the trait tree AND the Memory Dust wallet — a live currency with its own faucets (Game1.cs:922, 1266, 3250, 4450, 6542) and its own sinks (Warren facility upgrades Game1.cs:1302, expedition checkpoints Game1.cs:4221). SaveGame.MemoryDust (:64) is the wallet, not trait points. Deleting 'the trait tree' without splitting this class deletes the Warren's and the checkpoint system's currency.
- The tree is the sole owner of TWO whole catalogues. All 19 keystones (Keystones.cs) and all 13 vows (Vows.cs) are reachable only through it — DustEffects.LearnedKeystones (:55-63) and KnownVows (:120-126) are the only producers, and BuildComposer.cs:129 and :148 are the only gates. Brief §49 and §43 move these to region conquest and discovery; until that lands, removing the ks_* and vow_study_* nodes makes 32 pieces of build content unreachable.
- Skill-slot ownership is already split across two systems and they hand over by a coincidence of numbers. Unlocks.SkillSlots (Unlocks.cs:239-246) returns 1..4 from progression; DustEffects.SkillSlots (DustEffects.cs:212-216) returns 4..5 from weave_5; ApplySkillCapacity (Game1.cs:3501-3507) picks the tree's answer only once the gate reaches Build.SkillSlots (4). Move slots to progression per brief §54 and this hand-over disappears — but note the fifth slot is NOT a second passive: Build.ActiveSlotsFor(5) = (5+1)/2 = 3 actives and 2 passives (Build.cs:340-343), so weave_5 today buys a THIRD ACTIVE, breaking the authoritative 2A+2P model the brief reaffirms.
- Two live wires cross into systems the brief assigns elsewhere. DustEffects.MasteryRate (recall_1..4) speeds region-mastery accrual, and region mastery is itself a trait-point faucet (Career.cs:60) — a feedback loop that vanishes if trait points do. DustEffects.DismantleRate (efficient_forge) is the only modifier on Forge dismantle value; removing it silently retunes salvage back to the flat 0.4 (Forge.cs:34).
- There is no Forge-access trait to migrate. Brief §55 assumes Forge is 'hidden behind a Trait purchase'; it is not — Activity.Forge opens on chests or two owned items (Unlocks.cs:127). Only the node NAME 'OPENS AUTO-SELL AND FORGE' (MemoryDust.cs:341) says otherwise. Acting on §55 as written would migrate a gate that does not exist.
- Two node descriptions do not match runtime. filter_common says items sell 'the moment they drop' and auto_merge says merging happens 'after every expedition' (MemoryDust.cs:345, :357); both actually fire only on chest open (ForgeScreen.cs:1748, :1716). Any migration copy that repeats these sentences will ship the same lie into the new system.
- The 34-point budget is a load-bearing invariant with a test that fails loudly. MemoryDustTests.cs:233-249 asserts Career.TraitPointsEarned at full content equals exactly 34, and that one terminal is affordable while two are not. Trait points are derived every frame (Game1.cs:4444), so removing the currency also removes the only consumer of Career.TraitPointsEarned, of UnlockFacts.TraitPointsEarned (Unlocks.cs:71, :159) and of the Traits screen gate — three call sites that will compile-break, plus one test that will need deleting rather than fixing.
- ~40 fixture call sites hard-code node ids. Game1's dev/capture modes purchase ids by name in ~12 places (Game1.cs:1776, 1981, 1992-1993, 2455-2458, 2511, 2654-2655, 2682) and TraitsScreen.DevPoseLit (:356-357) grants a node and its prerequisites. A dozen Core tests do the same. These are the capture rig's posing mechanism — per the project's own 'UI states need a fixture' rule, killing them without replacements means the new Traits screen's states cannot be photographed.
- MasteryScreen.Tree (MasteryScreen.cs:336) is assigned at :1048 and :1327 and never read — a dormant coupling that will look load-bearing during the refactor. Confirmed dead by grep over the whole file.
- TraitTreeLayout is 257 lines of authored positions plus a fallback placer that exists because the CHARGE spur once added four keystones nobody positioned, making them invisible-but-counted (TraitTreeLayout.cs:12-20). Its two layout tests (TraitNamesTests.cs:184, :205) are the only guard against that class of bug; a replacement UI that lays traits out on a grid does not inherit it.
- design/gdd/skill-and-trait-trees.md §3.4-3.6 is CURRENT, not stale — it correctly states 51 nodes, 226 total, 34 points, 15% reachable, and it is the only prose spec of the live tree. Treating it as stale documentation and skipping it would leave the refactor's one accurate design reference contradicting the new system.

## VOWS, KEYSTONES, UNLOCKS, CAREER MILESTONES

### 1. The full Vow catalogue: id, name, restriction, reward, and how a vow is unlocked and equipped today. How many can be equipped, and what raises that number.

### 1a. The catalogue — 13 entries, all in `src/IdleXIdle.Core/Builds/Vows.cs`

There is no separate "reward" field. A Vow's reward is **computed from `Severity` (demand Vows) or `StaticCostMagnitude` (static-cost Vows)** by `Vows.Multiplier` (`Vows.cs:236-247`), which multiplies **one skill's damage** — the skill the Vow is bound to — not the build. Formulas: `ConditionalMultiplier(1 - Severity) = 1 + 1.5 * (1 - (1-Severity))^1.0 = 1 + 1.5*Severity` (`Vows.cs:214-221`, `VowTuning.MaxPowerBonus = 1.5f`, `CurveExponent = 1.0f`, `Vows.cs:158-159`); `StaticMultiplier = 1 + 8.0 * StaticCostMagnitude` (`Vows.cs:233-234`, `StaticCostConversionRate = 8.0f`, `Vows.cs:172`).

| # | id | Name | Kind | Restriction (`Demand` / cost) | `Short` (UI row) | Reward multiplier (computed) | Line |
|---|---|---|---|---|---|---|---|
| 1 | `vow_singular` | VOW OF THE SINGULAR | Demand | `SingleStyle`, Severity 0.75 — every woven skill same Style | ONE STYLE ONLY | ×2.125 | `Vows.cs:258` |
| 2 | `vow_pure` | VOW OF THE PURE | Demand | `SingleSource`, Severity 0.60 — every woven skill same Source | ONE SOURCE ONLY | ×1.900 | `Vows.cs:265` |
| 3 | `vow_complete` | VOW OF COMPLETION | Demand | `EverySlotFilled`, Severity 0.20 — `SkillsWoven >= SkillSlots` | NO EMPTY SLOT | ×1.300 | `Vows.cs:274` |
| 4 | `vow_bluntedge` | VOW OF THE BLUNT EDGE | Demand | `NoCritInvestment`, Severity 0.55 — `CritPercent <= BaseCritPercent + 0.01` | NO CRITICAL BONUS | ×1.825 | `Vows.cs:284` |
| 5 | `vow_deliberate` | VOW OF THE DELIBERATE | Demand | `CadenceAtOrBelow`, Threshold 1.0, Severity 0.50 — `SkillRate <= 1.001` | SKILL RATE MAX 1.00x | ×1.750 | `Vows.cs:291` |
| 6 | `vow_frantic` | VOW OF THE FRANTIC | Demand | `CadenceAtOrAbove`, Threshold 1.4, Severity 0.50 — `SkillRate >= 1.399` | SKILL RATE MIN 1.40x | ×1.750 | `Vows.cs:298` |
| 7 | `vow_unguarded` | VOW OF THE UNGUARDED | Demand | `NoDefence`, Severity 0.70 — `Defence <= 0` | NO DEFENCE AT ALL | ×2.050 | `Vows.cs:305` |
| 8 | `vow_unbound` | VOW OF THE UNBOUND | Demand | `NoKeystone`, Severity 0.80 — `KeystonesWorn == 0` | NO KEYSTONE IN USE | ×2.200 | `Vows.cs:312` |
| 9 | `vow_barefoot` | VOW OF THE BAREFOOT | Demand | `SlotLeftBare` Bare=Boots, Severity 0.65 | NO BOOTS | ×1.975 | `Vows.cs:322` |
| 10 | `vow_openhand` | VOW OF THE OPEN HAND | Demand | `SlotLeftBare` Bare=Gloves, Severity 0.65 | NO GLOVES | ×1.975 | `Vows.cs:329` |
| 11 | `vow_bareskull` | VOW OF THE BARE SKULL | Demand | `SlotLeftBare` Bare=Helm, Severity 0.70 | NO HELM | ×2.050 | `Vows.cs:336` |
| 12 | `vow_fragility` | VOW OF FRAGILITY | StaticCost | always on; `DamageTakenIncrease = 0.125` (post-mitigation); `StaticCostMagnitude = 0.111` | ALWAYS: TAKES +12.5% | ×1.888 | `Vows.cs:345` |
| 13 | `vow_reckless_offering` | RECKLESS OFFERING | StaticCost | always on; −15% max health, charged at champion mint; `StaticCostMagnitude = 0.15` | ALWAYS: −15% MAX HEALTH | ×2.200 | `Vows.cs:353` |

Arithmetic shown for two rows: `vow_singular` → `1 + 1.5*0.75 = 2.125`. `vow_fragility` → `1 + 8.0*0.111 = 1.888`.

`BareSlot` declares `{ None, Boots, Gloves, Helm, Ring, Charm }` (`Vows.cs:100`) but **only Boots/Gloves/Helm have a Vow**; Ring and Charm are declared and unused by any catalogue entry.

Demand evaluation is `Vows.IsActive` (`Vows.cs:369-387`) against `BuildContext` (`Vows.cs:187-202`), built once per wave by `SoloBattle.DescribeBuild` (`SoloBattle.cs:2563-2596`). Static-cost Vows always return true (`Vows.cs:372`).

### 1b. How a Vow is UNLOCKED today — bought on the trait tree, nowhere else

A Vow is knowable **only** by owning a `vow_*` node in the Memory Dust tree. The map is `DustEffects.VowGrants` (`src/IdleXIdle.Core/Prestige/DustEffects.cs:102-117`):

| Trait node | Cost | Requires | Vows it teaches | Cumulative point cost | Node line |
|---|---|---|---|---|---|
| `vow_study_1` "LEARN VOWS I" | 1 | — (root) | `vow_complete`, `vow_deliberate` | 1 | `MemoryDust.cs:322` |
| `vow_study_2` "LEARN VOWS II" | 2 | `vow_study_1` | `vow_pure`, `vow_frantic` | 3 | `MemoryDust.cs:324` |
| `vow_study_3` "LEARN VOWS III" | 2 | `vow_study_2` | `vow_singular`, `vow_bluntedge` | 5 | `MemoryDust.cs:327` |
| `vow_binding` "GEAR-SLOT VOWS" | 3 | `vow_study_3` | `vow_barefoot`, `vow_openhand`, `vow_bareskull` | 8 | `MemoryDust.cs:330` |
| `vow_sacrifice` "SACRIFICE VOWS" | 3 | `vow_study_3` | `vow_fragility`, `vow_reckless_offering`, `vow_unguarded`, `vow_unbound` | 8 | `MemoryDust.cs:335` |

All 13 Vows cost **1+2+2+3+3 = 11 trait points** (matching the comment at `BuildComposer.cs:144`). Readers: `DustEffects.KnownVows` (`DustEffects.cs:120-126`), `DustEffects.KnowsVow` (`DustEffects.cs:128-132`).

**A brand-new player knows ZERO Vows.** Pinned by `tests/unit/IdleXIdle.Core.Tests/Prestige/DustEffectsTests.cs:251` `test_a_fresh_player_knows_no_vows`. Trait points come only from conquest / corruption / region mastery (`Career.cs:56-62`), so the earliest a Vow can exist for the player is after the first conquest (wave 20) or after ~100 waves in one region. The brief's "at least one tutorial Vow accessible" requirement is **not met today — there is no tutorial Vow and no Vow outside the tree.**

The gate is enforced in three places: the picker only lists `Known` (`LoadoutScreen.cs:538`), the composer nulls an untaught Vow (`BuildComposer.cs:148`), and load drops it from the save (`Game1.cs:800-802`).

### 1c. How a Vow is EQUIPPED today — one per SKILL SLOT, not a build-level slot

A Vow is a field on a skill, not on the build: `EquippedSkill(SkillDef Def, Source Source, Vow? Vow = null)` (`Build.cs:217`), persisted as `PlayerLoadout.SkillChoice(Source, string? VowId, ...)` (`PlayerLoadout.cs:40`) and saved as `SavedSkill.VowId` (`SaveGame.cs:327`).

Flow: BUILD screen → select a slot → "BIND A VOW" / "CHANGE VOW" button (`LoadoutScreen.cs:1598`) → vow list opens, header `BIND TO SLOT {n}` (`LoadoutScreen.cs:1637`), row 0 is "NO VOW" (`LoadoutScreen.cs:1657`) → `PlayerLoadout.SetVow(slot, vowId, knownVows)` (`PlayerLoadout.cs:206-212`), which **refuses an unknown Vow** (`PlayerLoadout.cs:210`). Empty state copy: `"NO VOW ON THIS SLOT — VOWS ARE LEARNED ON THE TRAITS SCREEN"` (`LoadoutScreen.cs:1578`). Live validation on the row pill: `"{Short} OK"` / `"{Short} BROKEN"` (`LoadoutScreen.cs:1038`).

### 1d. How many can be equipped, and what raises the number

**There is no Vow capacity constant anywhere.** The cap is emergent: **one Vow per skill slot**, so the number of simultaneously-sworn Vows equals `PlayerLoadout.SkillCapacity`. Today that is **1 → 4 → 5**:

| Source | Rule | file:line |
|---|---|---|
| Onboarding gate | 1 slot, +1 at DeepestWave ≥ 5, +1 at DeepestWave ≥ 12, +1 at RegionsConquered ≥ 1 → caps at 4 | `Unlocks.cs:239-246` |
| Trait tree | `weave_5` node → 5 slots | `DustEffects.cs:212-216`, node `MemoryDust.cs:314` (cost 3, requires `socket_2`) |
| Hand-over rule | while the onboarding gate is below 4 it rules; at 4 the tree takes over | `Game1.ApplySkillCapacity`, `Game1.cs:3501-3507` |

Caveat the refactor must not miss: the sim counts **DISTINCT** Vows, not equipped ones — `SoloBattle.DistinctVows` (`SoloBattle.cs:2534-2540`). Wearing FRAGILITY on four skills is one promise: it is billed once (`SoloBattle.cs:668-675`) and TITHE counts it once (`SoloBattle.cs:649-652`). The damage BENEFIT, however, is applied per skill (`SoloBattle.VowFactor`, `SoloBattle.cs:2549-2560`, called at `1641`, `1820`, `2069`, `2375`).

**Dead code found:** `PlayerLoadout.CycleVow` (`PlayerLoadout.cs:134-147`) has **zero callers** in `src/` or `tests/`.

### 2. The full Keystone catalogue: id, name, effect, and how a keystone is unlocked and socketed today. How many sockets, and what raises that number.

### 2a. The catalogue — 19 entries, all in `src/IdleXIdle.Core/Builds/Keystones.cs`

`Mods` is `BuildMods(Damage, Health, SkillRate, Haul, Rarity)`; omitted means `BuildMods.None` (all 1.0). `Grants` are `BuildTrigger` behaviours.

| # | id | Name | Mods (D/H/SR/Haul/Rar) | Grants | Blurb (player text) | Line |
|---|---|---|---|---|---|---|
| 1 | `glass_cannon` | GLASS CANNON | 2.0 / 0.5 / 1 / 1 / 1 | — | DOUBLE DAMAGE. HALF HEALTH. | `Keystones.cs:33` |
| 2 | `ironclad` | IRONCLAD | 1 / 2.0 / 0.5 / 1 / 1 | — | DOUBLE HEALTH. YOUR SKILLS COME BACK HALF AS OFTEN. | `Keystones.cs:39` |
| 3 | `blood_magic` | BLOOD MAGIC | 1 / 1 / 2.0 / 1 / 1 | NoHealing | SKILLS COME BACK TWICE AS FAST. YOU CANNOT BE HEALED. | `Keystones.cs:45` |
| 4 | `bloodlust` | BLOODLUST | 1 / 0.75 / 1 / 1 / 1 | Bloodlust | THE CLOSER TO DEATH, THE HARDER YOU HIT. YOU TAKE 25% MORE. | `Keystones.cs:52` |
| 5 | `echo` | ECHO | 0.6 / 1 / 1 / 1 / 1 | Echo | EVERY SKILL FIRES TWICE — EACH AT 60%. | `Keystones.cs:59` |
| 6 | `greed` | GREED | 0.7 / 1 / 1 / 2.0 / 1 | — | DOUBLE LOOT. YOU HIT 30% SOFTER. | `Keystones.cs:66` |
| 7 | `discerning_eye` | DISCERNING EYE | 1 / 1 / 1 / 0.5 / 2.0 | — | FAR RARER FINDS. HALF THE LOOT. | `Keystones.cs:72` |
| 8 | `reaper` | REAPER | 1 / 1 / 0.75 / 1 / 1 | Splinter | EVERY KILL GIVES RICHER LOOT. YOUR SKILLS COME BACK 25% SLOWER. | `Keystones.cs:78` |
| 9 | `undying` | UNDYING | 0.85 / 1 / 1 / 1 / 1 | Undying | THE FIRST KILLING BLOW EACH RUN LEAVES YOU ON 1 HEALTH. YOU HIT 15% SOFTER. | `Keystones.cs:85` |
| 10 | `venomancer` | VENOMANCER | 0.8 / 1 / 1 / 1 / 1 | Venom | YOUR SKILLS POISON. THEY HIT 20% SOFTER. | `Keystones.cs:92` |
| 11 | `juggernaut` | JUGGERNAUT | **none set** (all 1.0) | Zeal, NoHealing | THE FULLER YOUR HEALTH, THE HARDER YOU HIT. YOU CANNOT BE HEALED. | `Keystones.cs:103` |
| 12 | `fortune` | FORTUNE | 0.75 / 1 / 1 / 1 / 2.0 | — | DOUBLE LOOT RARITY. YOU HIT 25% SOFTER. | `Keystones.cs:111` |
| 13 | `titan` | TITAN | 1 / 3.0 / 0.4 / 1 / 1 | — | TRIPLE HEALTH. YOUR SKILLS COME BACK 60% SLOWER. | `Keystones.cs:120` |
| 14 | `hoarder` | HOARDER | 1 / 1 / 1 / 1 / 0.5 | Hoarder | MORE LOOT MEANS HARDER HITS. RARE FINDS COME HALF AS OFTEN. | `Keystones.cs:130` |
| 15 | `rend` | REND | 0.9 / 1 / 1 / 1 / 1 | Rend | EVERY SKILL USE STORES 1 CHARGE, UP TO 10. YOUR STRIKES SPEND IT ALL, +5% PER CHARGE. ALL HITS 10% SOFTER. | `Keystones.cs:142` |
| 16 | `capacitor` | CAPACITOR | 1 / 1 / 0.85 / 1 / 1 | Capacitor | YOUR CHARGE POOL HOLDS 20, NOT 10. LODESTONE THEN NEEDS ALL 20. SKILLS COME BACK 15% SLOWER. USELESS UNLESS A KEYSTONE USES THE POOL. | `Keystones.cs:152` |
| 17 | `dynamo` | DYNAMO | 1 / 0.85 / 1 / 1 / 1 | Dynamo | EVERY HIT YOU TAKE STORES 2 CHARGE. YOU TAKE 15% MORE. | `Keystones.cs:162` |
| 18 | `lodestone` | LODESTONE | 1 / 1 / 1 / 1 / 0.8 | Lodestone | CLEAR A WAVE WITH A FULL CHARGE POOL TO GET A SPARE CORE. RARE FINDS COME 20% LESS OFTEN. | `Keystones.cs:173` |
| 19 | `weaver` | WEAVER | 1 / 1 / 0.70 / 1 / 1 | Weaver | EVERY SKILL ALSO FIRES THE NEXT SKILL IN YOUR BUILD, AT 45%. SKILLS RETURN 30% SLOWER. | `Keystones.cs:183` |

The "every keystone is a trade" law is `Keystones.IsATrade` (`Keystones.cs:199-208`) — enforced by test. JUGGERNAUT passes only through the `Grants.Contains(NoHealing)` clause (`Keystones.cs:206`), since it sets no Mods at all.

### 2b. How a keystone is UNLOCKED today — one trait node each, 19 of them

`DustEffects.LearnedKeystones` (`DustEffects.cs:55-63`) reads `MemoryDustUnlock.GrantsKeystone` (`MemoryDust.cs:85`) off every owned node. Every keystone has exactly one gate node. Cumulative cost = the whole prerequisite chain.

| Keystone | Gate node id | Road | Node cost | Requires | Cumulative trait-point cost | Node line |
|---|---|---|---|---|---|---|
| glass_cannon | `ks_glass_cannon` | Ruin | 4 | `socket_2` | 6 | `MemoryDust.cs:390` |
| bloodlust | `ks_bloodlust` | Ruin | 6 | `ks_glass_cannon` | 12 | `MemoryDust.cs:393` |
| blood_magic | `ks_blood_magic` | Ruin | 8 | `ks_bloodlust` | 20 | `MemoryDust.cs:396` |
| reaper | `ks_reaper` | Ruin | 12 | `ks_blood_magic` | **32** (terminal) | `MemoryDust.cs:399` |
| rend | `ks_rend` | Ruin | 6 | `ks_glass_cannon` | 12 | `MemoryDust.cs:408` |
| ironclad | `ks_ironclad` | Aegis | 4 | `socket_2` | 6 | `MemoryDust.cs:414` |
| juggernaut | `ks_juggernaut` | Aegis | 6 | `ks_ironclad` | 12 | `MemoryDust.cs:417` |
| undying | `ks_undying` | Aegis | 8 | `ks_juggernaut` | 20 | `MemoryDust.cs:420` |
| titan | `ks_titan` | Aegis | 12 | `ks_undying` | **32** (terminal) | `MemoryDust.cs:423` |
| dynamo | `ks_dynamo` | Aegis | 6 | `ks_ironclad` | 12 | `MemoryDust.cs:426` |
| greed | `ks_greed` | Avarice | 4 | `ledger` | 5 | `MemoryDust.cs:439` |
| discerning_eye | `ks_discerning_eye` | Avarice | 6 | `ks_greed` | 11 | `MemoryDust.cs:442` |
| fortune | `ks_fortune` | Avarice | 8 | `ks_discerning_eye` | 19 | `MemoryDust.cs:445` |
| hoarder | `ks_hoarder` | Avarice | 12 | `ks_fortune` | **31** (terminal) | `MemoryDust.cs:448` |
| lodestone | `ks_lodestone` | Avarice | 6 | `ks_greed` | 11 | `MemoryDust.cs:429` |
| echo | `ks_echo` | Artifice | 4 | `vow_study_1` | 5 | `MemoryDust.cs:456` |
| venomancer | `ks_venomancer` | Artifice | 6 | `ks_echo` | 11 | `MemoryDust.cs:459` |
| capacitor | `ks_capacitor` | Artifice | 6 | `ks_echo` | 11 | `MemoryDust.cs:432` |
| weaver | `ks_weaver` | Artifice | 12 | `artifice_vows` (8, req `ks_venomancer`) | **31** (terminal) | `MemoryDust.cs:468` |

The cheapest first keystone is 5 trait points (`ledger`+`ks_greed`, or `vow_study_1`+`ks_echo`). Note the **cross-system prerequisite the refactor must break: ARTIFICE's first keystone `ks_echo` hangs off the Vow chain node `vow_study_1`** (`MemoryDust.cs:457`).

### 2c. How a keystone is SOCKETED today

BUILD screen → keystone chip strip under the slot rows → select chip → primary button says SOCKET / UNSOCKET (`LoadoutScreen.cs:844-848`) → `PlayerLoadout.ToggleKeystone(id, learned)` (`PlayerLoadout.cs:220-229`), bounded by `Math.Min(MaxKeystones, KeystoneCapacity)` (`PlayerLoadout.cs:225`). Persisted as `SaveGame.SocketedKeystoneIds` (`SaveGame.cs:156`); restore truncates with `Take(Math.Min(MaxKeystones, KeystoneCapacity))` (`PlayerLoadout.cs:337`). At compose, only LEARNED keystones are taken (`BuildComposer.cs:129-132`) and `Build.Take` re-caps at `Build.KeystoneSlots` (`Build.cs:438-445`).

UI copy: header reads `"LEARN THEM ON THE TRAITS SCREEN"` when none learned, else `"{n} / {cap} SOCKETS"` (`LoadoutScreen.cs:1103`); refusal reads `"ONLY {cap} SOCKET{S} — MORE ON THE TRAITS SCREEN."` (`LoadoutScreen.cs:848`).

### 2d. How many sockets, and what raises the number

**Sockets start at 1 and go to 3, bought on the trait tree.**

| Sockets | Rule | file:line |
|---|---|---|
| 1 (base) | `1 + (socket_2 ? 1 : 0) + (socket_3 ? 1 : 0)` | `DustEffects.KeystoneSockets`, `DustEffects.cs:205-209` |
| 2 | node `socket_2` "KEYSTONE SOCKET II", cost 2, no prereq | `MemoryDust.cs:312` |
| 3 | node `socket_3` "KEYSTONE SOCKET III", cost 5, **requires `weave_5`** (cost 3, which requires `socket_2`) → 10 points total | `MemoryDust.cs:317`, `MemoryDust.cs:314` |

Hard ceiling `Build.KeystoneSlots = 3` (`Build.cs:435`) mirrored by `PlayerLoadout.MaxKeystones` (`PlayerLoadout.cs:67`). Host pushes the tree value into the loadout at `Game1.cs:786` (load) and `Game1.cs:4334` (per frame).

**Stale comment / code disagreement (believe the code):** `Build.cs:421-432` says the Dust tree "is COMPLETABLE by design — a player can buy every node" and speaks of "three sockets and **ten** keystones"; `PlayerLoadout.cs:67` says "the tree teaches **fifteen**". The catalogue has **19** keystones, and the tree costs **226 points** against **34 earnable** (arithmetic in finding 4), i.e. it is emphatically **not** completable — `tests/unit/IdleXIdle.Core.Tests/Prestige/MemoryDustTests.cs:233` `test_two_terminals_are_out_of_reach` asserts exactly the opposite of the comment. (`DustEffectsTests.cs:331` `test_the_tree_is_still_completable` only checks graph reachability with unlimited points — `Rich()` — not affordability.)

### 3. The Unlocks/Activity gate system: every Activity, what opens it, and the exact requirement text shown to the player.

### 3a. The system

`src/IdleXIdle.Core/Progression/Unlocks.cs`. **Derived, never stored** (`Unlocks.cs:84-91`): every gate is a pure function of `UnlockFacts(WavesCleared, DeepestWave, ItemsOwned, ChestsEverHeld, RegionsConquered, TraitPointsEarned)` (`Unlocks.cs:65-71`). Facts are supplied by `Game1.GuideUnlockFacts()` (`Game1.cs:3510-3521`).

### 3b. Every Activity — 11 of them (`Unlocks.cs:13-57`)

| Activity | Gate expression | file:line | `Requirement()` — exact string | `Headline()` — exact string |
|---|---|---|---|---|
| `Hunt` | `true` | `Unlocks.cs:104` | `""` (empty) | `"THE HUNT"` |
| `Training` | `f.WavesCleared >= 1` | `Unlocks.cs:107` | `"Clear your first wave"` | `"TRAINING — MAKE YOUR HUNTER STRONGER"` |
| `Gear` | `f.ItemsOwned >= 1` | `Unlocks.cs:111` | `"Find your first item"` | `"GEAR — WHAT YOU WEAR"` |
| `Vault` | `f.ChestsEverHeld >= 1` | `Unlocks.cs:123` | `"Earn a chest from a boss"` | `"VAULT — CHESTS YOU HAVE NOT OPENED"` |
| `Forge` | `f.ChestsEverHeld >= 1 \|\| f.ItemsOwned >= 2` | `Unlocks.cs:127` | `"Earn a chest from a boss"` | `"THE FORGE — WHERE ITEMS ARE MADE"` |
| `Build` | `f.DeepestWave >= 5` | `Unlocks.cs:132` | `"Reach wave 5"` | `"THE BUILD — THE ACTUAL GAME"` |
| `Mastery` | `f.DeepestWave >= 25` | `Unlocks.cs:141` | `"Reach wave 25"` | `"THE MASTERY TREE — HOW YOUR SKILLS BEHAVE"` |
| `Map` | `true` | `Unlocks.cs:148` | `""` (empty) | `"THE MAP — THE WORLD BEYOND"` |
| `Warren` | `f.RegionsConquered >= 1` | `Unlocks.cs:156` | `"Conquer a region"` | `"THE WARREN — WORK THAT RUNS WITHOUT YOU"` |
| `Traits` | `f.TraitPointsEarned >= 1` | `Unlocks.cs:159` | `"Earn a trait point"` | `"TRAITS — PERMANENT BONUSES THAT NEVER RESET"` |
| `Roster` | `true` | `Unlocks.cs:166` | `""` (empty) | `"ROSTER — THE OTHER HUNTERS"` |

Requirement strings: `Unlocks.cs:178-199`. Headline strings: `Unlocks.cs:202-219`. Both throw `ArgumentOutOfRangeException` on an undeclared Activity rather than defaulting.

### 3c. Where the player actually sees that text

1. **Locked nav tile, on hover only** — `Unlocks.Requirement(...)` drawn under the tile label, sentence case as authored: `Game1.cs:7092-7093`.
2. **Clicking a locked tile** — toast `$"{Headline} IS NOT OPEN YET — {Requirement.ToUpperInvariant()}."` plus `sfx_error`: `Game1.cs:6886`. Duplicate for the BUILD hotkey path at `Game1.cs:2994-2995`.
3. **Onboarding tour copy quotes it once** — the BUILD tour card says `$"It opens when you {Unlocks.Requirement(Activity.Mastery).ToLowerInvariant()}."` (`Onboarding.cs:292`).
4. **NEW mark, not a modal** — an opened screen wears a gold NEW mark derived by `Onboarding.IsNew` (`Onboarding.cs:548-550`); there is **no** "NEWLY OPENED" panel any more.

`NavActivity[]` maps rail tiles to activities (`Game1.cs:6869-6872`); `NavUnlocked` gates them (`Game1.cs:6876-6877`). Parallel-array integrity is checked by `tools/check_nav_gates.py`.

### 3d. Facts wiring — two things the refactor must know

- `WavesCleared` is **fed `_deepestEver`, not a cleared-wave total** (`Game1.cs:3511`). Training's "Clear your first wave" is therefore really "reach depth 1".
- `TraitPointsEarned` is fed `_dust.Earned` (`Game1.cs:3521`), which is set from `Career.TraitPointsEarned(_world)` (`Game1.cs:4444`, `Game1.cs:6841`). So **the TRAITS screen itself is gated on a career milestone**, not on a trait node.

### 3e. Defects found in the gate layer

- **`Unlocks.NewlyOpened` (`Unlocks.cs:275-276`) is dead.** Its doc-comment claims "the host keeps the previous facts and asks this each frame"; a repo-wide grep finds exactly one caller — `tests/unit/IdleXIdle.Core.Tests/Progression/unlocks_test.cs:65`. Believe the code: nothing in `src/` calls it.
- **Forge caption drift.** The gate has two clauses (`ChestsEverHeld >= 1 || ItemsOwned >= 2`, `Unlocks.cs:127`) but the requirement line names only one (`"Earn a chest from a boss"`, `Unlocks.cs:184`). `unlocks_test.cs:113` only asserts non-emptiness, so nothing catches it.

### 4. World/region progression: what a region conquest is, what it awards today, what region MASTERY is, and every existing milestone the game already recognises (hunter level, waves, conquests).

### 4a. What a region conquest IS

Holding **wave 20** in a region: `Checkpoints.ConquestWave = 20` (`Checkpoints.cs:29`), mirrored by `Game1.ConquerWaveDepth` (`Game1.cs:4552`). Fires once, at `Game1.cs:4447`: `if (_expedition.Deepest >= ConquerWaveDepth && !_world.IsConquered(_activeRegion))`. `World.Conquer(id)` adds to `_conquered` and returns the next region (`Regions.cs:138-142`).

Six regions, a linear chain (`Regions.cs:59-88`): `verdant_hollow` (Nature, Balanced, no prereq) → `cinderworks` (Machine, Heavy) → `umbral_reach` (Shadow, Fast) → `marrow_wastes` (Body, Heavy) → `still_archive` (Mind, Fast) → `pale_choir` (Spirit, Balanced). Each region is `RegionLadder.HealthStep = 1.62×` tougher in health and `DamageStep = 1.34×` in damage than the last (`RegionLadder.cs:46,56`).

### 4b. What a conquest AWARDS today

**Directly, in the conquest block (`Game1.cs:4447-4458`):**

| Award | Value | file:line |
|---|---|---|
| The next region unlocks | `World.Conquer` returns it; `World.IsUnlocked` opens it | `Regions.cs:138-142`, `Regions.cs:129-135` |
| Memory Dust | `ConquestDust(tier) = 40 * (1 + tier)` → 40 at tier 0, 240 at tier 5 | `Game1.cs:4450`, `CorruptionScaling.cs:40-41` |
| Sound | `sfx_conquer` (fallback `sfx_levelup`) | `Game1.cs:4451` |
| Map message | `"{REGION} CONQUERED!  {NEXT} IS OPEN."` or `"THE WORLD IS YOURS."` | `Game1.cs:4452-4456` |
| Save | immediate `Save()` | `Game1.cs:4457` |

**Indirectly (derived every frame from `ConqueredIds`):**

| Consequence | Rule | file:line |
|---|---|---|
| +1 trait point | `world.ConqueredIds.Count` term | `Career.cs:59` |
| WARREN screen opens (first conquest only) | `RegionsConquered >= 1` | `Unlocks.cs:156` |
| 4th skill slot (first conquest only) | `if (f.RegionsConquered >= 1) slots++` | `Unlocks.cs:245` |
| +1 Warren facility unlocked | `UnlockedFacilityCount = min(8, 2 + ConqueredRegions)` | `Warren.cs:208` |
| +10% Warren production per region | `ConquestBonus = ConqueredRegions * 0.10f` | `Warren.cs:236`, `Warren.cs:95` |
| Checkpoints become available in that region | `Checkpoints.Options(bestDepth, conquered)` returns only `[0]` if not conquered | `Checkpoints.cs:38-44` |
| A champion unlocks (5 of the 10) | `UnlockKind.Conquest` gates | `CharacterState.cs:73`; roster entries `CharacterRoster.cs:79, 93, 109, 121` — cinderworks→THE ANVIL, umbral_reach→THE CHORUS, marrow_wastes→THE METRONOME, still_archive→THE UNBROKEN |
| Tutorial rung `Conquer` satisfied | `f.RegionsConquered >= 1` | `Tutorial.cs:144` |
| Corruption DEEPEN becomes possible (all six) | `AllConquered && CorruptionTier < 5` | `Regions.cs:165-168` |

**A conquest awards no keystone, no vow, no item, no chest and no XP today.**

### 4c. What region MASTERY is

A **per-region** 4-step ladder earned by fighting there. `MasteryLevel { NewlyConquered=0, PartiallyMastered=1, FullyMastered=2, Perfected=3 }` (`MasteryLevel.cs:12-18`), derived from `Region.RegionMasteryPoints` (RMP) at `RegionAutomation.cs:107-116`.

| Level | RMP threshold | Waves in that region at rate 1.0 | With all four `recall_*` (rate 1.20) |
|---|---|---|---|
| PartiallyMastered | 500 (`RmpThreshold1`, `RegionAutomation.cs:17`) | 500 ÷ 5 = **100** | 500 ÷ 6 ≈ **84** |
| FullyMastered | 2000 (`RmpThreshold2`, `RegionAutomation.cs:18`) | 2000 ÷ 5 = **400** | 2000 ÷ 6 ≈ **334** |
| Perfected | 5000 (`RmpThreshold3`, `RegionAutomation.cs:26`) | 5000 ÷ 5 = **1000** | 5000 ÷ 6 ≈ **834** |

RMP accrues at `RmpPerActiveKillBonus = 5f` × rate (`RegionAutomation.cs:15`, `RecordActiveKill`, `RegionAutomation.cs:128-129`). **Naming lie worth flagging: the method is called `RecordActiveKill` and the field `RmpPerActiveKillBonus`, but the single call site fires once per CLEARED WAVE inside the wave-reward loop** (`Game1.cs:4379`, loop opens `Game1.cs:4361`). The class-level comment at `RegionAutomation.cs:22` says "per cleared wave" and is the one that matches the code.

What mastery pays:

| Payout | Rule | file:line |
|---|---|---|
| +1 trait point per level, per region (max 3×6 = 18) | `total += (int)world.RegionFarm(def.Id).MasteryLevel` | `Career.cs:60` |
| Memory Dust, once per new level across all regions | `MasteryLevelDust(newLevels, tier) = newLevels * 15 * (1 + tier)`; high-water-marked by `_highestMasteryAwarded` | `Game1.cs:3247-3253`, `CorruptionScaling.cs:35-36` |
| Harder + richer region | `RegionProgressionOf = clamp(level + (level>0 ? 1 : 0), 0, 4)`; feeds enemy baseline (`110 * (1 + 0.35*prog)`, `9 * (1 + 0.20*prog)`) and the chest loot tier | `Game1.cs:4462-4463`, `Game1.cs:4492-4498`, `Game1.cs:3020`, loot tier `Game1.cs:4515` |

Mastery rate is raised only by the trait tree: `recall_1..4` at +5% each to +20% (`DustEffects.MasteryRate`, `DustEffects.cs:68-75`; nodes `MemoryDust.cs:359-368`).

### 4d. Every milestone the game already recognises

**Wave / depth milestones**

| Milestone | Threshold | file:line |
|---|---|---|
| Boss | every 5th wave (`BossEvery`) | `Tutorial.cs:107` (mirrors `ExpeditionTuning.BossEvery`) |
| TRAINING opens | depth ≥ 1 (fed as `WavesCleared`) | `Unlocks.cs:107` + `Game1.cs:3511` |
| BUILD opens · 2nd skill slot | depth ≥ 5 | `Unlocks.cs:132`, `Unlocks.cs:242` |
| Tutorial `MeetABoss` satisfied | depth ≥ 5 | `Tutorial.cs:141` |
| 3rd skill slot | depth ≥ 12 | `Unlocks.cs:243` |
| CONQUEST | depth 20 | `Checkpoints.cs:29` |
| MASTERY tree opens | depth ≥ 25 | `Unlocks.cs:141` |
| Checkpoint offered | every 10 waves ever held, conquered regions only; costs 25 Dust/wave skipped | `Checkpoints.cs:32`, `Checkpoints.cs:35`, `Checkpoints.cs:38-47` |
| Mastery points | `floor(sqrt(bestDepth) * 0.9)` **per region**, summed | `MasteryPoints.cs:38-51` |
| Quest `q_cinder_deep` | wave 50 in Cinderworks | `Quests.cs:169-172` |
| Quest `q_marrow_hold` | wave 80 (`ConquestWave * 4`) in Marrow Wastes | `Quests.cs:192-197` |
| Quest `q_quiver_volleys` | 150 waves with a VOLLEY skill equipped | `Quests.cs:201-206` |
| Warren facility ceiling | derived from `Career.DeepestAnywhere` | `Career.cs:68-74` |

**Conquest milestones** — first conquest opens WARREN + 4th skill slot; each conquest = 1 trait point, +1 facility, +10% Warren production, and (for 4 of them) a champion. Six conquests = the whole world → corruption unlocks. All cited in 4b.

**Item / chest milestones**

| Milestone | Threshold | file:line |
|---|---|---|
| GEAR opens | `ItemsOwned >= 1` | `Unlocks.cs:111` |
| VAULT opens | `ChestsEverHeld >= 1` | `Unlocks.cs:123` |
| FORGE opens | `ChestsEverHeld >= 1 \|\| ItemsOwned >= 2` | `Unlocks.cs:127` |
| Gem lesson | `gemsHeld >= 1`, once | `Onboarding.cs:476` |
| Quest `q_magpie_bosses` | 40 bosses felled | `Quests.cs:181-186` |

**Corruption milestones** — tiers 0–5 (`CorruptionScaling.MaxTier = 5`, `CorruptionScaling.cs:17`), gated on `AllConquered` (`Regions.cs:168`). Each new **peak** tier: 2 trait points (`Career.cs:59`) and `DeepeningDustAward = 60 * max(1, tier)` (`CorruptionScaling.cs:30-31`, awarded `Game1.cs:6542`). Named looks in `CorruptionLook.cs:19-27`.

**Vow milestone** — quest `q_three_vows`: three descents finished with a Vow's demand still MET (`Quests.cs:175-178`), unlocking THE OATHBOUND (`CharacterRoster.cs:212`). Latched at run-end by `Career.VowWasKept` (`Career.cs:85-91`), counter `_runsWithVowKept` (`Game1.cs:434`, incremented `Game1.cs:4439`, saved `SaveGame.cs:133`). Legacy single-vow form `q_first_vow` at `LegacyUnlocks.cs:35`.

**Trait-point budget — the arithmetic**

`Career.TraitPointsEarned(world) = ConqueredIds.Count + 2 * PeakCorruptionTier + Σ per-region MasteryLevel` (`Career.cs:56-62`).

> At full current content: **6** (conquests) + **2 × 5 = 10** (corruption peak) + **6 × 3 = 18** (mastery) = **34**.

Pinned by `MemoryDustTests.cs:249` (`Assert.Equal(34, budgetAtFullContent)`). Against that, the tree's total cost is **226 points** across **51 nodes** (summed live from `MemoryDustTree.TotalTreeCost`, `MemoryDust.cs:119`; I summed every `Cost =` literal in `MemoryDust.cs`: 51 nodes, 226 points). So a maxed career can afford **34 / 226 ≈ 15%** of the tree. The four terminals cost 32 / 32 / 31 / 31 cumulative; the two cheapest together are 62 > 34, which is the intended "one terminal only" rule (`MemoryDustTests.cs:233`). The `attunement` keepsake alone costs 32 of the 34 (union of `socket_2` 2 + `ks_glass_cannon` 4 + `ks_ironclad` 4 + `ledger` 1 + `ks_greed` 4 + `vow_study_1` 1 + `ks_echo` 4 + `weave_5` 3 + `socket_3` 5 + `attunement` 4 = 32).

**HUNTER LEVEL is not a milestone system.** `HunterLevel => 1 + _ranks.Values.Sum() / 5` (`HunterProgression.cs:168`), doc-commented "Cosmetic only — never a gate. Gating on it would make it a second progression axis." Three read sites, all display: `Game1.cs:1564` (boot log), `Game1.cs:3036` (mastery screen), `HuntScreen.cs:2674` (`"LV n"`), `TrainingScreen.cs:484`. **Nothing in the game gates on it.** If the refactor wants "early Hunter Level" as a Vow/Keystone owner (brief §43, §48, §52), that axis has to be built — it does not exist as a gate today.

### 5. Which of these currently depend on a TRAIT NODE. Name each dependency with file:line.

The "trait tree" in this codebase is `MemoryDustTree` (`src/IdleXIdle.Core/Prestige/MemoryDust.cs`), bought with TRAIT POINTS derived by `Career.TraitPointsEarned` (`Career.cs:56-62`), presented by `src/IdleXIdle.Game/TraitsScreen.cs` under `Activity.Traits`. Everything below is a live dependency on owning a node in it.

### A. VOWS — 100% trait-node dependent

| Dependency | Nodes | file:line |
|---|---|---|
| Which Vows exist for the player at all | `vow_study_1`, `vow_study_2`, `vow_study_3`, `vow_binding`, `vow_sacrifice` | `DustEffects.cs:102-117` (`VowGrants` table) |
| The known-Vow list | reads `VowGrants` | `DustEffects.KnownVows`, `DustEffects.cs:120-126` |
| Single-Vow query | reads `KnownVows` | `DustEffects.KnowsVow`, `DustEffects.cs:128-132` |
| Composer refuses to pay an untaught Vow | `DustEffects.KnowsVow(tree, s.VowId)` | `BuildComposer.cs:148` |
| The BUILD screen's Vow list | `DustEffects.KnownVows(Tree)` | `LoadoutScreen.cs:538` |
| Load-time strip of an untaught Vow | `DustEffects.KnowsVow(_dust, vid)` → `SetVow(i, null, …)` | `Game1.cs:800-802` |
| Loadout refuses an untaught Vow | `knownVows.All(v => v.Id != vowId)` return false | `PlayerLoadout.cs:210` |
| Vow POWER multiplier ×1.25 | `artifice_vows` | `DustEffects.VowPowerMultiplier`, `DustEffects.cs:226-230`; node `MemoryDust.cs:465` |
| …reaching the sim | `DustEffects.TreeShape` → `SkillShape.VowPowerMultiplier` | `DustEffects.cs:233-238`, folded at `BuildComposer.cs:115-116`, consumed `SoloBattle.cs:2559` |
| Wired-id registry naming the vow nodes | `"vow_study_1","vow_study_2","vow_study_3","vow_binding","vow_sacrifice","artifice_vows"` | `DustEffects.cs:191`, `DustEffects.cs:194` |
| Empty-state UI copy pointing at the tree | `"NO VOW ON THIS SLOT — VOWS ARE LEARNED ON THE TRAITS SCREEN"` | `LoadoutScreen.cs:1578` |
| Spine identity sentence naming vows | `"Keystone sockets, skill slots, vows, auto-selling and the forge."` | `TraitRoads.cs:38-39` |
| ARTIFICE road identity naming vows | `"Skills act differently. Vows pay more."` | `TraitRoads.cs:44-45` |
| Vow catalogue text referencing the tree | `vow_unbound` Description: `"…YOU GIVE UP THE TRAIT TREE'S PRIZE."` | `Vows.cs:315` |

### B. KEYSTONES — 100% trait-node dependent, both discovery and sockets

| Dependency | Nodes | file:line |
|---|---|---|
| Which keystones the player may socket | all 19 `ks_*` nodes via `MemoryDustUnlock.GrantsKeystone` | `DustEffects.LearnedKeystones`, `DustEffects.cs:55-63`; field `MemoryDust.cs:85`; nodes `MemoryDust.cs:390,393,396,399,408,414,417,420,423,426,429,432,439,442,445,448,456,459,468` |
| Composer only sockets learned keystones | `var learned = DustEffects.LearnedKeystones(tree)` | `BuildComposer.cs:129-132` |
| BUILD screen chip strip + count | `DustEffects.LearnedKeystones(Tree)` | `LoadoutScreen.cs:355`, `695`, `1098`, `1468` |
| Socket/unsocket action | `ToggleKeystone(id, DustEffects.LearnedKeystones(Tree))` | `LoadoutScreen.cs:909` |
| **Socket COUNT** (1→2→3) | `socket_2`, `socket_3` | `DustEffects.KeystoneSockets`, `DustEffects.cs:205-209`; nodes `MemoryDust.cs:312`, `MemoryDust.cs:317` |
| Host pushes socket count into the loadout | `_loadout.KeystoneCapacity = DustEffects.KeystoneSockets(_dust)` | `Game1.cs:786`, `Game1.cs:4334` |
| Restore truncates to the bought socket count | `Take(Math.Min(MaxKeystones, KeystoneCapacity))` | `PlayerLoadout.cs:337` |
| UI refusal copy pointing at the tree | `"ONLY {n} SOCKET{S} — MORE ON THE TRAITS SCREEN."` | `LoadoutScreen.cs:848` |
| UI empty-state copy pointing at the tree | `"LEARN THEM ON THE TRAITS SCREEN"` | `LoadoutScreen.cs:1103` |
| BUILD tour copy pointing at the tree | `"Keystones are rules learned on the TRAITS screen."` | `Onboarding.cs:286-288` |
| **Cross-system prereq**: ARTIFICE's first keystone hangs off the Vow chain | `ks_echo` `Requires = new[] { "vow_study_1" }` | `MemoryDust.cs:457` |
| **Cross-system prereq**: the 3rd socket hangs off the 5th skill slot | `socket_3` `Requires = new[] { "weave_5" }` | `MemoryDust.cs:318` |
| Keystone doc-comment ties the socket count to the tree | `Build.KeystoneSlots` remarks | `Build.cs:417-435` |

### C. STRUCTURAL UNLOCKS on trait nodes (brief §53-§57)

| Capability | Node(s) | Effect site | Consumer |
|---|---|---|---|
| **5th skill slot** | `weave_5` (cost 3, req `socket_2`) | `DustEffects.SkillSlots`, `DustEffects.cs:212-216`; node `MemoryDust.cs:314` | `Game1.cs:785`, `Game1.ApplySkillCapacity` `Game1.cs:3504-3506` |
| **Auto-sell COMMON** | `filter_common` (cost 2, req `ledger`) | `DustEffects.AutoSellAtOrBelow`, `DustEffects.cs:144-150`; node `MemoryDust.cs:343` | `_forge.AutoSellFloor = …`, `Game1.cs:4265` |
| **Auto-sell UNCOMMON** | `filter_uncommon` (cost 3, req `filter_common`) | `DustEffects.cs:147`; node `MemoryDust.cs:346` | `Game1.cs:4265` |
| **Auto-merge after runs** | `auto_merge` (cost 2, req `forge_insight`) | `DustEffects.AutoMergeAfterRuns`, `DustEffects.cs:155-159`; node `MemoryDust.cs:355` | `_forge.AutoMergeOnOpen = …`, `Game1.cs:4338` |
| **Salvage +15%** | `efficient_forge` (cost 3, req `forge_insight`) | `DustEffects.DismantleRate`, `DustEffects.cs:85-90`; node `MemoryDust.cs:352` | `Game1.cs:4261` |
| **Region mastery rate +5…20%** | `recall_1`…`recall_4` | `DustEffects.MasteryRate`, `DustEffects.cs:68-75`; nodes `MemoryDust.cs:359-368` | `_region.RecordActiveKill(DustEffects.MasteryRate(_dust))`, `Game1.cs:4379` |
| **Pure gate nodes** (buy nothing themselves) | `ledger` "OPENS AUTO-SELL AND FORGE" (`MemoryDust.cs:341`), `forge_insight` "OPENS THE FORGE UPGRADES" (`MemoryDust.cs:349`) | wired only by the requirement graph | `DustEffects.cs:248-252` |
| **Keepsake / no effect** | `attunement` (cost 4) | `DustEffects.TreeComplete`, `DustEffects.cs:173-177`; node `MemoryDust.cs:538` | `TraitsScreen.cs:943` |
| **Passive stat multipliers** | 12 minor nodes (`ruin_edge_1-3`, `aegis_skin_1-3`, `avarice_purse_1-3`, `artifice_hands_1-3`) | `DustEffects.TreeMods`, `DustEffects.cs:42-46`; nodes `MemoryDust.cs:492-533` | `BuildComposer.cs:111` |

**Important negative result:** the **FORGE screen itself is NOT gated on a trait node** — `Activity.Forge` opens on chests/items (`Unlocks.cs:127`). The node named `"OPENS AUTO-SELL AND FORGE"` (`MemoryDust.cs:341`) opens only *other trait nodes*, not the Forge. Its name will read to a player as a claim the code does not make. Brief §55 ("do not hide Forge behind a Trait purchase") is already satisfied for the screen; what is behind a trait purchase is the Forge's *upgrades* (`efficient_forge`, `auto_merge`).

### D. UNLOCKS / CAREER — one dependency, and it runs the other way

| Dependency | Rule | file:line |
|---|---|---|
| The TRAITS screen is gated on a career milestone, not a node | `Activity.Traits => f.TraitPointsEarned >= 1` | `Unlocks.cs:159` |
| …fed from the tree's earned count | `TraitPointsEarned: _dust.Earned` | `Game1.cs:3521` |
| …which is set from the career | `_dust.SetEarned(TraitPointsEarned())` → `Career.TraitPointsEarned(_world)` | `Game1.cs:4444`, `Game1.cs:6841`, `Career.cs:56-62` |
| TRAITS screen hint copy | `"YOU HAVE {n} TRAIT POINT{S}"` | `Onboarding.cs:588-589` |
| TRAITS tour copy stating the three faucets | `"You earn one for each region you conquer, one each time a region's mastery rises a level, and two for each new corruption tier."` | `Onboarding.cs:376-378` |
| Tour targets that exist only for the tree | `TourTarget.TraitTree`, `TraitPoints`, `TraitDetail` | `Onboarding.cs:105-110` |

`Unlocks.SkillSlots` (`Unlocks.cs:239-246`) and every other gate in `Unlocks.cs` depend on **no** trait node. `Tutorial.cs` depends on no trait node. `Encounters/*` depend on no trait node (only `Career.TraitPointsEarned` reads `World`, never the reverse).

### E. Summary count

- Vows: **6 nodes** control the entire system (5 teach, 1 amplifies) — 11 points for the catalogue, 8 more for the amplifier.
- Keystones: **21 nodes** (19 teach, 2 sell sockets) — 5–32 points per keystone, 10 points for all three sockets.
- Structural: **11 nodes** (`weave_5`, `ledger`, `filter_common`, `filter_uncommon`, `forge_insight`, `efficient_forge`, `auto_merge`, `recall_1..4`).
- Cosmetic/keepsake: **1 node** (`attunement`). Minors: **12 nodes**.
- Total: 6 + 21 + 11 + 1 + 12 = **51 nodes**, matching the catalogue count.

**Files this area will change**

- src/IdleXIdle.Core/Builds/Vows.cs — the 13-entry catalogue, VowDemand/VowKind/BareSlot enums, BuildContext, the pricing formulas, IsActive. Needs discovery metadata it does not have; vow_unbound's Description names 'the trait tree's prize' (Vows.cs:315).
- src/IdleXIdle.Core/Builds/Keystones.cs — the 19-entry catalogue and IsATrade. Needs acquisition metadata (which conquest/mastery reveals it) that does not exist today.
- src/IdleXIdle.Core/Prestige/MemoryDust.cs — the 51-node trait tree: 6 vow nodes (312-338), 21 keystone/socket nodes, 11 structural nodes, 12 minors, attunement. Every node the refactor moves or deletes lives here; ids are save keys (MemoryDust.cs:309-310 'IDS NEVER CHANGE').
- src/IdleXIdle.Core/Prestige/DustEffects.cs — the whole effect layer: VowGrants (102-117), KnownVows/KnowsVow, LearnedKeystones (55-63), KeystoneSockets (205-209), SkillSlots (212-216), VowPowerMultiplier (226-230), AutoSellAtOrBelow (144-150), AutoMergeAfterRuns, DismantleRate, MasteryRate, and the NamedWires/WiredIds liveness registry (187-253) that a test asserts against.
- src/IdleXIdle.Core/Prestige/TraitRoads.cs — the five road identity sentences; the Spine sentence names sockets, skill slots, vows, auto-sell and the forge (38-39), the Artifice sentence names vows (44-45).
- src/IdleXIdle.Core/Prestige/TraitTreeLayout.cs — node positioning derived from the catalogue; changes when nodes leave.
- src/IdleXIdle.Core/Prestige/MemoryDustText.cs — node card copy, appends a keystone's Blurb after a gate's description; test-pinned shape.
- src/IdleXIdle.Core/Progression/Unlocks.cs — the Activity enum (11 members incl. Traits), IsOpen, Requirement, Headline, SkillSlots (239-246), SkillSlotNote, and the dead NewlyOpened (275-276). The Traits gate at 159 disappears with the tree.
- src/IdleXIdle.Core/Progression/Career.cs — TraitPointsEarned (56-62) is the trait-point faucet; VowWasKept (85-91) is the q_three_vows latch; DeepestAnywhere feeds the Warren ceiling.
- src/IdleXIdle.Core/Progression/Onboarding.cs — TourTarget.TraitTree/TraitPoints/TraitDetail (105-110), the whole Traits tour (368-382), the Build tour card that says keystones are learned on TRAITS (286-288), the Mastery card quoting Unlocks.Requirement (292), HintFor's Traits line (588-589), SeedExplained/BannerFor which read Unlocks.SkillSlots.
- src/IdleXIdle.Core/Progression/Tutorial.cs — has no Vow or Keystone rung today; a tutorial Vow (brief §46) needs a new rung, a Satisfied clause, Title/Body copy and a Sends target.
- src/IdleXIdle.Core/Progression/MasteryLevel.cs — the 4-step region mastery ladder the brief wants keystones hung off.
- src/IdleXIdle.Core/Automation/RegionAutomation.cs — RMP thresholds 500/2000/5000 (17-26), RecordActiveKill(rate) (128-129) whose name says kill and whose call site is per wave; the recall_* rate parameter dies with those nodes.
- src/IdleXIdle.Core/Builds/Build.cs — EquippedSkill.Vow (217), Keystone record (183-196), KeystoneSlots const 3 (435) and Take/Drop (438-448), SlotCapacity/ActiveCapacity/PassiveCapacity (273-343). Its keystone-slot doc-comment (417-432) is stale on three counts (completable tree, ten keystones, vow_study_1 teaching 'Patience').
- src/IdleXIdle.Core/Builds/BuildComposer.cs — the vow gate (148) and the learned-keystone gate (129-132); folds DustEffects.TreeMods/TreeShape into the build (111, 115-116).
- src/IdleXIdle.Core/Builds/PlayerLoadout.cs — SkillChoice.VowId (40), SetVow (206-212), the dead CycleVow (134-147), MaxKeystones/KeystoneCapacity (67-81), ToggleKeystone (220-229), Restore's socket truncation (337), Signature (54-55).
- src/IdleXIdle.Core/Builds/SoloBattle.cs — DistinctVows (2534-2540), VowFactor (2549-2560) called at 1641/1820/2069/2375, VowHealthMultiplier (2511-2521), the fragility damage-taken bill (668-675), TITHE's sworn-vow count (649-652, 952-954), DescribeBuild (2563-2596), the affinity vow buy-back (1848-1852, 2072).
- src/IdleXIdle.Core/Builds/SkillShape.cs — VowPowerMultiplier (55) and its Combine (451); the seam both artifice_vows and THE OATHBOUND ride.
- src/IdleXIdle.Core/Builds/MasteryCatalog.cs — two mastery nodes already sell VowPowerMultiplier (178: 1.30f, 197: 1.60f), so vow power has three owners today.
- src/IdleXIdle.Core/Encounters/Regions.cs — World.Conquer (138-142) is where a conquest-granted keystone would have to be minted; ConqueredIds, corruption peak (159-163), AllConquered (165).
- src/IdleXIdle.Core/Encounters/CorruptionScaling.cs — ConquestDust (40-41) and MasteryLevelDust (35-36); the reward hooks a keystone award would sit beside.
- src/IdleXIdle.Core/Encounters/Checkpoints.cs — ConquestWave = 20 (29) and the conquered-only checkpoint rule (38-44).
- src/IdleXIdle.Core/Persistence/SaveGame.cs — MemoryDust/MemoryDustUnlocks (64-65), SocketedKeystoneIds (156), SavedSkill.VowId (327), ExplainedScreens (231), RunsWithVowKept (133), CurrentVersion = 3 (17). Discovered-vow and discovered-keystone lists do not exist and must be added; the old MemoryDustUnlocks list must still migrate.
- src/IdleXIdle.Game/Game1.cs — _dust.SetEarned (4444), GuideUnlockFacts (3510-3521), ApplySkillCapacity (3501-3507), KeystoneCapacity push (786, 4334), untaught-vow strip on load (800-802), the conquest block (4447-4458), mastery-level Dust (3247-3253), forge wiring (4261, 4265, 4338), RecordActiveKill (4379), NavActivity + locked-tile copy (6869-6890), rail requirement render (7092), TraitPointsEarned (6841), _runsWithVowKept (434, 4439), dev fixtures that buy node ids by name (2511, 2654).
- src/IdleXIdle.Game/LoadoutScreen.cs — the whole Vow binder (733-766, 1567-1682) and keystone chip strip (355-396, 695-699, 844-848, 908-909, 1098-1122, 1466-1471), all its 'TRAITS SCREEN' copy (848, 1103, 1578), TourTarget.Vows anchor (502).
- src/IdleXIdle.Game/TraitsScreen.cs — the entire trait-tree UI (~1900 lines), including the SocketKind.Rung/Spur labels 'KEYSTONE GATE'/'KEYSTONE SPUR' (1825-1826) and the TreeComplete readout (941-943).
- src/IdleXIdle.Game/MapScreen.cs — where a 'REGION CONQUERED / NEW KEYSTONE DISCOVERED' reveal (brief §51) would be drawn; already hosts the conquest strip and Onboarding.HintFor's map line (157).
- src/IdleXIdle.Game/ForgeScreen.cs — SwornVows (1045-1059, 2522, 2771) for TITHE, AutoSellFloor and AutoMergeOnOpen consumers.
- src/IdleXIdle.Game/GearScreen.cs — need.AnyVow / need.Keystone checks on combo enchantments (1136).
- src/IdleXIdle.Core/Warrens/Warren.cs — the destination for Auto-Sell per brief §56; FacilityKind (20-24, incl. HoardVaults and ScavengerRuns the brief names), UnlockedFacilityCount (208), ConquestBonus (236).
- src/IdleXIdle.Core/Economy/Enchantments.cs — Fervour/Reverb/Bulwark/Tithe (81-91) and EnchantNeed.AnyVow/Keystone (105-110) are dead without vows/keystones, so their reachability changes with the discovery model.
- src/IdleXIdle.Core/Characters/CharacterRoster.cs — THE OATHBOUND's VowPowerMultiplier 1.5f (209) and its q_three_vows gate (212); the five Conquest-gated champions (79, 93, 109, 121).
- src/IdleXIdle.Core/Quests/Quests.cs — q_three_vows (175-178) is the only Vow-shaped career milestone that exists.
- tests/unit/IdleXIdle.Core.Tests/Prestige/DustEffectsTests.cs — 27 tests pinning vow grants, keystone teaching, sockets, filters, wiring liveness; every one moves.
- tests/unit/IdleXIdle.Core.Tests/Prestige/MemoryDustTests.cs — the 34-point budget assertion (249) and test_two_terminals_are_out_of_reach (233).
- tests/unit/IdleXIdle.Core.Tests/Prestige/trait_gates_test.cs — untaught-vow composition (29), bought-socket restore (47), perfected-tier trait point (65).
- tests/unit/IdleXIdle.Core.Tests/Prestige/TraitNamesTests.cs and TraitDescriptionsTests.cs and MemoryDustTextTests.cs — node name/description shape rules keyed to the current catalogue.
- tests/unit/IdleXIdle.Core.Tests/Progression/unlocks_test.cs — every gate, its caption, the skill-slot ladder, the conquest-count caption check.
- tests/unit/IdleXIdle.Core.Tests/Progression/onboarding_test.cs, onboarding_hints_test.cs, tutorial_test.cs, tutorial_dismissal_test.cs, legacy_names_test.cs — tour/hint/rung copy for the Traits and Build screens.
- tests/unit/IdleXIdle.Core.Tests/Progression/career_test.cs — the trait-point arithmetic.
- tests/unit/IdleXIdle.Core.Tests/Builds/PassiveTreeTests.cs, BuildTests.cs, SoloBattleTests.cs, BalanceSweepTests.cs, affinity_vow_buyback_test.cs, blow_vertical_slice_test.cs, charge_keystone_test.cs — all construct builds with vows/keystones through the tree.
- tests/unit/IdleXIdle.Core.Tests/Persistence/SaveSystemTests.cs, legacy_full_save_migration_test.cs, onboarding_save_test.cs, share_codes_test.cs — save shape for MemoryDustUnlocks, SocketedKeystoneIds, VowId, ExplainedScreens.
- tools/check_nav_gates.py — asserts Nav[] and NavActivity[] stay parallel; changing the Activity enum trips it.
- design/gdd/vows.md, design/gdd/skill-and-trait-trees.md, design/gdd/memory-dust-prestige-system.md, design/gdd/progression.md, design/gdd/game-flow.md, design/gdd/region-mastery-automation-system.md, design/gdd/onboarding-tutorial-system.md, design/gdd/quests.md — documentation the brief (§113) requires to match runtime afterwards.

**Risks**

- THE VOW SYSTEM IS UNREACHABLE WITHOUT THE TREE, END TO END. A fresh save knows zero vows (DustEffectsTests.cs:251 pins it) and there is no tutorial vow. Trait points only exist after a conquest (wave 20) or ~100 waves of one region, so today a player cannot swear anything for the first 20+ waves. Deleting the trait tree without simultaneously landing a discovery/tutorial path deletes Vows from the game rather than moving them.
- VOWS ARE PER-SKILL-SLOT, NOT A BUILD SLOT. Vow lives on EquippedSkill (Build.cs:217) and SkillChoice.VowId (PlayerLoadout.cs:40) and is saved per woven skill (SaveGame.cs:327). 'Vow capacity' (brief §48) does not exist as a number anywhere — capacity is emergent from SkillCapacity. Introducing a real capacity means a new field, a new save shape, and a decision about what happens to slots 3-5's vows when capacity is 2.
- THE SIM'S DISTINCT-VOW RULE IS LOAD-BEARING AND SUBTLE. SoloBattle.DistinctVows (SoloBattle.cs:2534-2540) bills a vow ONCE however many skills wear it, while the benefit is applied PER SKILL (VowFactor, four call sites). The asymmetry was deliberately created to fix a measured balance bug (SoloBattle.cs:664-673: both static vows were strictly negative to swear at the old pricing). Any change to how vows are equipped must preserve 'a vow is sworn, not equipped' or it re-opens that bug.
- THREE SYSTEMS ALREADY SELL VOW POWER. artifice_vows (DustEffects.cs:229, 1.25f), two mastery nodes (MasteryCatalog.cs:178 = 1.30f, MasteryCatalog.cs:197 = 1.60f), and THE OATHBOUND (CharacterRoster.cs:209, 1.5f) all multiply SkillShape.VowPowerMultiplier, which is multiplicative in Combine (SkillShape.cs:451). Removing only the trait node leaves the other three; a maxed stack is 1.30 × 1.60 × 1.50 = ×3.12 on the bonus.
- KEYSTONE DISCOVERY HAS 19 ENTRIES AND CONQUEST HAS 6. The brief's model (first conquest → one keystone, region mastery → another) yields at most 6 conquests × 1 + 18 mastery levels = 24 slots for 19 keystones; but region mastery is a 1000-wave-per-region grind (RegionAutomation.cs:26) and corruption tiers are post-full-conquest. A naive mapping either front-loads almost the whole catalogue onto six conquests or buries most of it behind a grind measured in thousands of waves.
- THE TRAIT TREE IS NOT COMPLETABLE, AND TWO COMMENTS SAY IT IS. 226 points of nodes against 34 earnable (MemoryDustTests.cs:249). Build.cs:421-432 and PlayerLoadout.cs:67 both claim otherwise, and the 'menu is longer than the plate' invariant (DustEffectsTests.cs:199) depends on the real ratio. A refactor that reads those comments as spec will size the new system wrong.
- CROSS-SYSTEM PREREQUISITES INSIDE THE TREE. ks_echo requires vow_study_1 (MemoryDust.cs:457) — the ARTIFICE keystone road literally hangs off the Vow chain. socket_3 requires weave_5 (MemoryDust.cs:318) — the third keystone socket hangs off the fifth skill slot. Pulling vows and slots out of the tree severs both roads unless the graph is re-rooted first.
- EVERY DUST NODE ID IS A SAVE KEY. SaveGame.MemoryDustUnlocks (SaveGame.cs:65) stores raw node ids and MemoryDust.cs:309-310 states 'IDS NEVER CHANGE — saves store them'. MemoryDustTree.Restore silently drops unknown ids (MemoryDust.cs:200-206), so deleting a node silently refunds nothing and revokes what it bought. Brief §58 forbids taking functionality away, so every deleted node needs an explicit migration read BEFORE the id vanishes.
- SILENT REVOCATION PATHS ALREADY EXIST AND WILL FIRE DURING MIGRATION. Game1.cs:800-802 strips any vow the tree no longer teaches, on load, without telling the player. BuildComposer.cs:148 nulls it again at compose. PlayerLoadout.cs:337 truncates sockets to KeystoneCapacity. If capacity or knowledge is computed from a new system before the migration writes it, an existing save loses its vows and its 2nd/3rd keystone on first launch and the save it writes back agrees.
- TRAIT POINTS ARE DERIVED, NOT BANKED. Career.TraitPointsEarned (Career.cs:56-62) is recomputed every frame from World. There is nothing to migrate and nothing to refund — the 34 points a player 'has' are a function, so §60's trait-point migration has to convert SPENT nodes into new-system grants, since the points themselves will simply stop existing.
- ACTIVITY.TRAITS IS GATED ON THE TREE'S OWN CURRENCY. Unlocks.cs:159 opens the screen at TraitPointsEarned >= 1 and Game1.cs:3521 feeds it _dust.Earned. Removing trait points without replacing that gate leaves the screen either permanently locked or permanently open, and Onboarding.IsNew/TourDue/SeedExplained (Onboarding.cs:509-566) all key off the Activity enum, so a renamed or removed member re-tours every returning player.
- THE UNLOCK LAYER'S MONOTONE INVARIANT IS THE HARD-WON PART. Unlocks.cs:116-122 records a shipped bug where a non-monotone fact (ChestsHeld) re-locked the Vault and re-fired its announcement on every chest. Any new discovery fact (vows discovered, keystones revealed) must only ever grow, or the same class of bug returns as a re-firing reveal.
- UNLOCKS.NEWLYOPENED IS DEAD (Unlocks.cs:275-276, only caller is a test) AND PLAYERLOADOUT.CYCLEVOW IS DEAD (PlayerLoadout.cs:134-147, zero callers). Both have doc-comments describing a host that calls them. This is the project's signature failure mode; a new discovery engine wired the same way would ship green and never run.
- REGIONAUTOMATION.RECORDACTIVEKILL IS MISNAMED. The method and its tuning field say 'kill'; the sole call site (Game1.cs:4379) fires once per cleared wave. Anyone sizing a keystone-per-mastery award from the name will be off by the enemies-per-wave count.
- HUNTER LEVEL CANNOT CARRY A GATE TODAY. HunterProgression.cs:168 is explicitly 'Cosmetic only — never a gate' and has only display readers. Brief §43/§48/§52 propose 'early Hunter Level' as an owner for Vow access and slot capacity; that axis has to be built from nothing, and it is derived from trained ranks (Gleam spend), which is a different pacing curve from depth or conquest.
- THE FORGE SCREEN IS ALREADY FREE. Activity.Forge opens on chests/items (Unlocks.cs:127), not on a node — brief §55 is satisfied for the screen. What IS behind trait purchases is efficient_forge and auto_merge, and the node named 'OPENS AUTO-SELL AND FORGE' (MemoryDust.cs:341) opens neither. Do not 'fix' a gate that is not there and miss the two that are.
- COMBO ENCHANTMENTS DIE IF VOWS OR KEYSTONES GET RARER. Fervour needs BLOODLUST, Reverb needs ECHO, Bulwark needs ZEAL/JUGGERNAUT, Tithe needs any sworn Vow (Enchantments.cs:81-91). They are deliberately inert without their partner (Enchantments.cs:78-79). If keystone discovery moves to late-world progression, four enchantment rolls become dead loot for most of the game.
- REQUIREMENT TEXT IS ONLY ASSERTED NON-EMPTY. unlocks_test.cs:113 checks that a locked activity says something, not that it says the truth — which is how the Forge caption already omits its second clause. New gate captions will not be caught by the existing suite.
- SAVE VERSION IS 3 AND LOADS ARE FORWARD-REFUSED ABOVE IT (SaveGame.cs:520). Adding discovered-vow / discovered-keystone / trait-loadout lists means either defaulted optional fields (the pattern used at SaveGame.cs:404, 415) or a version bump plus a migration for MemoryDustUnlocks, SocketedKeystoneIds and every SavedSkill.VowId.

## VFX player, anchors, actor bounds, animation rig

### 1. How VfxPlayer works end to end: what Play takes, how a strip is animated, how a destination rectangle is computed, and what layering exists.

**File**: `src/IdleXIdle.Game/VfxPlayer.cs` (291 lines). One instance in the whole game, constructed at `src/IdleXIdle.Game/HuntScreen.cs:724` (`_vfx = new VfxPlayer(ui.Assets);`). Nothing else in `src/`, `tests/` or `tools/` constructs or calls it.

**Stale doc to disbelieve**: `src/IdleXIdle.Game/Game1.cs:3370-3373` still says *"VfxPlayer.cs is KEPT and left unwired on purpose: the auto-battle has no impact effects at all"*. The code contradicts it — HuntScreen wires it fully (`Update` at :782, `Draw` at :1703, 18 spawn sites). Believe the code.

---

### 1a. `Play` — the fire-and-forget entry point

`VfxPlayer.cs:181-208`:
```csharp
public void Play(string key, int x, int y, float scale = 2f, float fps = 18f, Color? tint = null, int frameW = 0,
                 float delay = 0f, float growTo = 1f, int? toX = null, int? toY = null)
```

| Param | Meaning | Real usage across all 16 Play sites |
|---|---|---|
| `key` | asset key, resolved via `AssetLibrary.Get` (aliases included) | 8 literals + 6 computed via `FxFor(...)` |
| `x, y` | **centre** of the destination rect, in the caller's coordinate space | always a rect-derived point (see Q2) |
| `scale` | display multiplier of `BaseUnitPx` (**not** a factor on the source frame) | ints 2–5, or `EnemyScale()`/a clamp |
| `fps` | frames per second | 8–23 |
| `tint` | colour, multiplied by `Fade`, drawn **additively** | `Steel`/`Ember`/`Gold`/`Verdant`/`SourceGlow(...)`/`White` |
| `frameW` | non-square frame width override | **never passed — dead parameter** |
| `delay` | negative start on `Elapsed`; effect exists but is not drawn until the clock crosses 0 | used once, `HuntScreen.cs:1442` (`delay: 0.45f`) |
| `growTo` | swell factor across life | **never passed — dead parameter** |
| `toX, toY` | travel destination; the effect eases from `(x,y)` toward it across its life | used once, `HuntScreen.cs:1596` (projectile) |

Guards (`VfxPlayer.cs:184-185`): `if (!Enabled) return;` (settings' FIGHT EFFECTS switch, exposed as `HuntScreen.ShowHitEffects` at `HuntScreen.cs:3931`) and `if (_assets.Get(key) is not { } sheet) return;` — **a missing key is a silent no-op**. That silence is the mechanism behind the three dead effects in Q2.

### 1b. `Hold` — the persistent entry point

`VfxPlayer.cs:218-237`. Signature `Hold(string key, int x, int y, float scale, float fps, Color tint)`. Semantics:
- Keyed in `Dictionary<string, Anim> _held` (`VfxPlayer.cs:152`) — **keyed by the ASSET KEY, not by an owner or an anchor**. Two owners wanting the same strip would collide onto one instance.
- Every call adds the key to `_heldThisFrame` (`:222`); `Update` (`:247-251`) drops any held effect not re-asked this frame. There is no `Stop()` on purpose (`:214-216`).
- `Loop = true` (`:230`) → `CurrentFrame` wraps modulo (`:82-84`), `Done` is always false (`:85`), `Fade` is hard-coded to `1f` (`:116`).
- `FrameW = sheet.Height` always (`:227`) — a non-square held strip is unsupported (no `frameW` override on `Hold`).
- Position and tint are overwritten live every frame (`:234-236`).

### 1c. Strip animation

`Anim` is a private class (`VfxPlayer.cs:27-126`):
- Frame layout inferred at spawn: `var fw = frameW > 0 ? frameW : sheet.Height;` then `var frames = Math.Max(1, sheet.Width / fw);` (`:187-188`). **Every fx asset on disk is 4096×512 → 8 square 512-px frames** (measured: all 68 `assets/art/VFX/**/fx_*.png` are exactly 4096×512).
- `SecondsPerFrame = 1f / MathF.Max(1f, fps)` (`:202`).
- `CurrentFrame` (`:82-84`): looping → `(int)(Elapsed/SecondsPerFrame) % Frames`; one-shot → `Math.Min(Frames-1, ...)` (clamps on the last frame).
- `Done` (`:85`): `!Loop && Elapsed >= SecondsPerFrame * Frames`; removed in `Update` (`:241-245`).
- `Life` (`:74`): `Elapsed / (SecondsPerFrame * Frames)`, clamped 0..1.
- `Fade` (`:112-125`): 1 for the first 65 % of life, then `k³` where `k = (1-t)/0.35`. Held effects skip it entirely.
- `At` (`:53-66`): if `Loop`, or if `ToX==CenterX && ToY==CenterY`, returns the centre unchanged; otherwise eases out (`t = 1-(1-t)²`) from centre toward `(ToX,ToY)` on the `Life` clock.
- `Update(dt)` at `HuntScreen.cs:782` is fed **wall-clock `dt`**, whereas the fight's playhead advances by `dt * 1000f * _speedMul` (`HuntScreen.cs:1248`). `_speedMul` is a `const 1.0f` today (`HuntScreen.cs:89-90`), so they agree — but the coupling is implicit.

### 1d. Destination rectangle — the whole of the sizing math

`VfxPlayer.cs:275-278`, the only place a dest rect is built:
```csharp
var h = (int)MathF.Round(a.Scale * BaseUnitPx * (a.GrowTo <= 1f ? 1f : 1f + (a.GrowTo - 1f) * a.Life) / Scale);
var w = h * a.FrameW / a.FrameH;
var (ax, ay) = a.At;
b.Draw(a.Sheet, new Rectangle(ax - w / 2, ay - h / 2, w, h), src, a.Tint * a.Fade);
```
with `public const int BaseUnitPx = 104;` (`VfxPlayer.cs:137`) and `public int Scale = 4;` (`VfxPlayer.cs:173`, the *canvas* counter-scale).

So: **destination height = round(effectScale × 104 ÷ canvasScale)**, width preserves the source aspect (all strips square → `w == h`), and the rect is **always centred on the spawn point**. There is no pivot, no anchor, no baseline, no per-effect offset, no rotation, no facing.

`HuntScreen.cs:1632` sets `_vfx.Scale = 1;` on every `Draw` ("this screen authors at canvas scale 1"), and nothing else ever writes it — the field's default of 4 is dead. **Consequence**: the two `Hold` callers pre-compute the drawn height themselves as `scale * VfxPlayer.BaseUnitPx` (`HuntScreen.cs:3824`, `:3856`) with **no `/ Scale` term** — they are hard-wired to the assumption `_vfx.Scale == 1`. If the canvas scale ever changed, the shield and aura would silently mis-anchor.

**Rendered sizes actually produced** (canvasScale = 1, growth = 1), and the §73 budget check against the 512-px frame and against the 256-px pixen source the art contract says these were authored at (`design/art/arena-art-contract.md:38` — "Effect | played at scale 1–3 | pixen 256 | 512"):

| effect `scale` | drawn px | ÷512 frame | ÷256 source | §73 verdict (0.75–1.25×) |
|---|---|---|---|---|
| 1 | 104 | 0.203× | 0.406× | under on both |
| 2 | 208 | 0.406× | 0.813× | under vs frame / **in budget** vs source |
| 2.6 (shield shell) | 270 | 0.527× | 1.055× | under vs frame / **in budget** vs source |
| 3 | 312 | 0.609× | 1.219× | under vs frame / **in budget** vs source |
| 4 | 416 | 0.813× | 1.625× | **in budget** vs frame / over vs source |
| 5 | 520 | 1.016× | 2.031× | **in budget** vs frame / over vs source |
| 6.4 (aura) | 666 | 1.301× | 2.602× | **over on both** |

Arithmetic: `104×2.6 = 270.4 → 270`; `104×6.4 = 665.6 → 666`; `270/512 = 0.527`; `666/512 = 1.301`; `666/256 = 2.602`.

The art contract's own line (`arena-art-contract.md:38`) says effects are "played at scale 1–3". The live code plays at 1–5 plus 6.4. **The contract is stale.**

### 1e. Layering — there is essentially none

`VfxPlayer.Draw` (`:255-282`):
1. `b.End()` — closes the caller's batch.
2. `b.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, Rasterizer, null, Matrix.CreateScale(Scale))`.
3. `foreach (var a in _held.Values.Concat(_active))` — **held effects first, then one-shots in spawn order**. `SpriteSortMode.Deferred` means draw order == iteration order. That is the entire z-model: one flat list, two implicit tiers (held under fired).
4. `b.End()`, then reopen `BlendState.AlphaBlend` with `RestoreRasterizer ?? Rasterizer`.
5. Early-out at `:257` when both collections are empty — the batch is then never ended/reopened, which is consistent because the caller's rasterizer already equals `RestoreRasterizer` in the one call site.

Where the pass sits, `HuntScreen.DrawArena` (`:1693-1711`): boss/enemies → champion → **`_vfx.Draw(b)`** → callouts → arena overlay. So **every effect is in front of both actors and behind the callouts and the HUD**. There is no `GroundBehind`, no `BehindActor`, no `Overhead`. The brief's §68 vocabulary has zero implementation.

Scissoring: `HuntScreen.cs:1659-1660` sets `_vfx.Rasterizer = null` (effects deliberately **unclipped** — comment at `:1652-1655`) and `_vfx.RestoreRasterizer = ArenaRasterizer`, so the arena regains its scissor after the effects pass. Cleared back to null at `:1664-1665`.

### 1f. Lifecycle

`Clear()` (`:285-290`) drops active + held + the frame set. Called at exactly two places: `HuntScreen.cs:1238` (dev-seek replay rebuild) and `HuntScreen.cs:1640` (`DevForceBoss` fixture). **`BeginWave` deliberately does NOT clear** — see the comment at `HuntScreen.cs:912-914` ("NOT _vfx.Clear(). ... clearing them at the boundary erased the aura's ring mid-flight twice a cycle").

### 2. EVERY _vfx.Play call site in the game: file:line, the effect key, and exactly how its position and size are computed. This is the list the new contract has to replace - be exhaustive.

**Complete and exhaustive: 16 `Play` + 2 `Hold` = 18 spawn sites, ALL in `src/IdleXIdle.Game/HuntScreen.cs`.** Verified by `grep -rn "\.Play(\|\.Hold(" --include=*.cs src/ tests/ tools/` — no other file constructs or calls `VfxPlayer`. (`src/IdleXIdle.Game/LoadoutScreen.cs:1720` and `src/IdleXIdle.Game/TraitsScreen.cs:1064` draw fx art, but through `UiKit.AnimSprite` / `UiKit.Sprite`, bypassing VfxPlayer entirely.)

**Layout constants these resolve against** (all derived, none literal; values shown at the 100 % density profile):
- `GroundY` = `SkillStrip.Y - UiMetrics.Space(20)` (`HuntScreen.cs:67`). `SkillStrip.Y = PageBottom(16) - SkillStripHeight` (`:3302`); at 100 %: `SkillStripHeight = 8 + Pitch(19)=24 + 2 + SlotH=113 + 16 = 163`, so `SkillStrip.Y = 1080-16-163 = 901`, **`GroundY = 881`**.
- `ChampBox` (`:164`) = `new(620-200, GroundY-430, 400, 430)` → **(420, 451, 400, 430)**; Center (620, 666), Right 820, Bottom 881.
- `EnemyBox` (`:169`) = `new(1500-218, GroundY-440, 436, 440)` → **(1282, 441, 436, 440)**; Center (1500, 661), Bottom 881.
- `BossAnchor` (`:211`) = `new(1330, GroundY+10)` → **(1330, 891)**.
- `EnemyPoint(slot, yFrac)` (`:1777-1778`) = `_creatureRect[slot]` → `(r.X, r.Top + (int)(r.H*yFrac))`, **fallback `(_rowCentreX, _rowTopY + 110)`** when the slot has not been drawn yet.
- `EnemyScale(slot, mult)` (`:1782-1783`) = `Math.Clamp((int)MathF.Round((H ?? 300f)/140f * mult), 1, 5)` — **an integer 1..5, i.e. five size buckets, not a continuous ratio**.
- `_creatureRect` is written only by `PublishCreature` (`:1776`) from inside `Draw`; the `Play` sites run in `Update`. **Positions are therefore one frame stale**, and include the per-creature bob (`±7`/`±8` px) and the enemy lunge/slide.

---

#### The 16 `Play` sites

| # | file:line | key | X | Y | size (`scale` → drawn px) | fps / extras |
|---|---|---|---|---|---|---|
| 1 | HuntScreen.cs:1375 | `"fx_weakhit"` | `hx` from `EnemyPoint(e.Slot, 0.45f)` (:1374) | `hy` from same | `EnemyScale(e.Slot, 0.6f)` → 1–2 → **104–208** | 16, tint `Steel`. Fires on every **other** Strike (`(_strikeCount++ & 1) == 0`), never on an aura tick |
| 2 | HuntScreen.cs:1384 | `"fx_hit"` | `ChampBox.Center.X` = **620** | `ChampBox.Center.Y + 40` = **706** | `2` → **208** | 14, tint `Ember`. `EnemyStrike` |
| 3 | HuntScreen.cs:1419 | `"fx_heal"` | `ChampBox.Center.X` = **620** | `ChampBox.Bottom - 156` = **725** | `3` → **312** | 10, tint `Verdant`. The `-156` is a hand-tuned magic offset, documented at :1416-1418 as "so the effect's FOOT sits on the ground line" |
| 4 | HuntScreen.cs:1423 | `"fx_shield"` | **620** | `ChampBox.Center.Y - 20` = **646** | `4` → **416** | 12, tint `Gold`. `Undying` |
| 5 | HuntScreen.cs:1427 | `"fx_death"` | **620** | `ChampBox.Center.Y` = **666** | `4` → **416** | 9, no tint. Champion `Down` |
| 6 | HuntScreen.cs:1441 | `"fx_crit"` | `dx` from `EnemyPoint(e.Slot, 0.55f)` (:1439) | `dy - 40` | `EnemyScale(e.Slot, 1.2f)` → 2–5 → **208–520** | 10, tint `Gold`. Boss waves only |
| 7 | HuntScreen.cs:1442 | `"fx_death"` | `dx` (same) | `dy` | `EnemyScale(e.Slot, 0.9f)` → 2–3 → **208–312** | 9, `delay: 0.45f` |
| 8 | HuntScreen.cs:1464 | `"fx_shield"` | **620** | **646** | `3` → **312** | 12, tint `Steel`. `ShieldGained` |
| 9 | HuntScreen.cs:1476 | `"fx_shield"` | **620** | **646** | `2` → **208** | 16, tint `Steel`. `ShieldAbsorbed` |
| 10 | HuntScreen.cs:1594-1596 | `FxFor(def)`, `ClipKey == "projectile"` | `ChampBox.Right - 40` = **780** | `ChampBox.Center.Y + 30` = **696** | `Math.Clamp((tx - ChampBox.Right) / 104, 2, 5)` — **integer division**; single enemy `tx≈1500` → `680/104 = 6` → clamp **5** → **520**; a near swarm creature `tx≈1000` → `1` → clamp **2** → **208** | 8, tint `SourceGlow`, **`toX: tx, toY: ty`** — the only travelling effect |
| 11 | HuntScreen.cs:1599 | `FxFor(def)`, `"aura"` | **620** | `ChampBox.Center.Y + 20` = **686** | `3` → **312** | 12, tint `SourceGlow` |
| 12 | HuntScreen.cs:1602 | `FxFor(def)`, `"trap"` | `_rowCentreX` (row resting centre, :1920 / :2041 / :2116) | `EnemyPoint(target, 0.7f).Y` | `EnemyScale(target, 1.3f)` → 2–5 → **208–520** | 12, tint `SourceGlow` |
| 13 | HuntScreen.cs:1605 | `FxFor(def)`, `"mark"` | `tx` from `EnemyPoint(target, 0.45f)` (:1580) | `ty` | `EnemyScale(target, 0.9f)` → 2–3 → **208–312** | 12, tint `SourceGlow` |
| 14 | HuntScreen.cs:1608 | `FxFor(def)`, `"transformation"` | **620** | `ChampBox.Center.Y` = **666** | `3` → **312** | 12, tint `SourceGlow` |
| 15 | HuntScreen.cs:1611 | `FxFor(def)`, default (`"strike"` + any future key) | `tx` | `ty` | `EnemyScale(target, 1.25f)` → 2–5 → **208–520** | 12, tint `SourceGlow` |
| 16 | HuntScreen.cs:3877-3878 | `"fx_shield_break"` | **620** | `ChampBox.Center.Y - 20` = **646** | `4` → **416** | `ShieldBreakFrames / UiMotion.Reward` = `8 / 0.35` = **22.86 fps**, tint `White` |

#### The 2 `Hold` sites

| # | file:line | key | X | Y | size | notes |
|---|---|---|---|---|---|---|
| 17 | HuntScreen.cs:3825-3826 | `FxFor(_auraFxKey ?? "aura")` | `ChampBox.Center.X` = **620** | `(int)(ChampBox.Bottom + 18 - h/2f)` where `h = AuraScale * BaseUnitPx = 6.4*104 = 665.6` → **566** | `AuraScale = 6.4` → **666 px** | fps 10, tint `colour * level` where `level = AuraRest 0.38 + (AuraPeak 1 - 0.38)*spike³` (`:3818-3819`). Called unconditionally from `UpdateFight` (`:1195`), gated only on `_auraColour != null && _mode != Mode.Downed` (`:3817`) |
| 18 | HuntScreen.cs:3857-3858 | `FxFor("shield")` | **620** | `(int)(ChampBox.Bottom - 40 - h/2f)` where `h = 2.6*104 = 270.4` → **705** | `ShieldShellScale = 2.6` → **270 px** | fps 8, tint `Steel * breathe`, `breathe = 0.16 + 0.14*(0.5+0.5*sin(_anim*1.6))`, held flat at 0.5 phase under `UiMotion.Reduced` (`:3850-3852`). Called from `UpdateFight` (`:1196`), gated on `_replay?.HasShield == true && _mode != Mode.Downed` (`:3848`) |

---

#### Facts this list exposes that the new contract must handle

**(a) Every champion-side effect ignores the champion's lunge.** `HuntScreen.cs:1699-1701` draws the champion at `cbox = new Rectangle(ChampBox.X + push, ...)` with `push = (int)(_champLunge * 40f)`. All ten champion-side spawn sites use the un-pushed `ChampBox.Center.X`. During a swing the champion is up to **40 px right** of every effect that is supposed to be on him.

**(b) Three skills' effects never draw at all.** `FxFor` (`HuntScreen.cs:3663-3667`):
```csharp
private string FxFor(string fxKey)
{
    var own = $"fx_{Character.Id}_{fxKey}_strip8_512";
    return _ui.Assets.Has(own) ? own : $"fx_{fxKey}";
}
```
`AssetLibrary` keys textures by bare filename (`AssetLibrary.cs:66`), so `"fx_press"` only resolves through the alias table. The table (`AssetLibrary.cs:176-185`) contains exactly 14 `fx_` aliases: `fx_strike, fx_projectile, fx_aura, fx_trap, fx_mark, fx_transformation, fx_hit, fx_weakhit, fx_crit, fx_death, fx_heal, fx_shield, fx_levelup, fx_shield_break`. Per-character variants exist **only** for `mark/projectile/strike/transformation/trap` (50 files, 10 characters × 5).

The catalogue defines three other FxKeys:

| skill | file:line | Kind | FxKey | own-key exists? | `fx_<key>` aliased? | result |
|---|---|---|---|---|---|---|
| PRESS | SkillCatalogue.cs:383,386 | Field | `press` | no | **no** | `fx_press` → **null → no effect** |
| WEEP | SkillCatalogue.cs:584,587 | Reaction | `weep` | no | **no** | `fx_weep` → **null → no effect** |
| WILT | SkillCatalogue.cs:712,715 | Field | `wilt` | no | **no** | `fx_wilt` → **null → no effect** |

The art exists and is loaded: `assets/art/VFX/press/fx_press_strip8_512.png`, `.../weep/fx_weep_strip8_512.png`, `.../wilt/fx_wilt_strip8_512.png` (all 4096×512). For PRESS and WILT the loss is total — they are the two of four Field skills, and `_auraFxKey = fieldSk?.Def.FxKey` (`HuntScreen.cs:948`) drives site #17, so **a build running PRESS or WILT has no held field effect whatsoever**. Field coverage: BRAND→`fx_mark` ✓, MIRE→`fx_aura` ✓, PRESS ✗, WILT ✗.

**Why no gate caught it**: `tools/check_asset_keys.py:33-39` matches `_vfx.Play` call sites but extracts only **string literals** (`LITERAL = re.compile(r'"([a-z0-9_]+)"')`). Sites #10–#15 pass `FxFor(def)`, a computed key — invisible to the gate.

**(c) `fx_levelup` is an orphan alias.** Aliased at `AssetLibrary.cs:182`, the file exists (`assets/art/VFX/levelup/fx_levelup_strip8_512.png`), and **no call site anywhere plays it**.

**(d) `growTo` and `frameW` are dead parameters** — zero call sites pass either.

**(e) Not one site uses a semantic anchor.** Every position is a literal arithmetic expression on a layout rectangle: `Center.X`, `Center.Y ± {20, 30, 40}`, `Bottom - {40, 156}`, `Right - 40`, `Bottom + 18`, `dy - 40`, `+ 110`. This is precisely the `x += 17 / y -= 23 / scale = 0.42` pattern the brief's §62 names.

### 3. How the champion and the enemies are positioned and sized on the HUNT screen: the rects, where they come from, and whether any VISUAL BOUNDS (as opposed to raw texture size) exist anywhere.

### 3a. The layout rectangles (all static or near-static, all derived from `GroundY`)

| rect | file:line | expression | value @100 % |
|---|---|---|---|
| `GroundY` | HuntScreen.cs:67 | `SkillStrip.Y - UiMetrics.Space(20)` | **881** |
| `ChampBox` | HuntScreen.cs:164 | `new(620-200, GroundY-430, 400, 430)` | (420, 451, 400, 430) |
| `EnemyBox` | HuntScreen.cs:169 | `new(1500-218, GroundY-440, 436, 440)` | (1282, 441, 436, 440) |
| `BossAnchor` | HuntScreen.cs:211 | `new Point(1330, GroundY+10)` | (1330, 891) |
| `BossTargetBodyHeight` | HuntScreen.cs:209 | `const int = 540` | 540 |
| `ArenaRect` (where actors may stand) | HuntScreen.cs:555 | `new(300, 150, 1500, SkillStrip.Y-150)` | (300,150,1500,751) |
| `ArenaClip` (where pixels may land) | HuntScreen.cs:576 | `new(186, 150, 1734, SkillStrip.Y-150)` | (186,150,1734,751) |

These are properties, not `static readonly`, precisely so they follow the density profile (comment at `:162-163`).

### 3b. The champion

`HuntScreen.cs:1699-1701`:
```csharp
var push = (int)(_champLunge * 40f);
var cbox = new Rectangle(ChampBox.X + push, ChampBox.Y, ChampBox.Width, ChampBox.Height);
DrawChampion(b, cbox, dead: _mode == Mode.Downed);
```
`_champLunge` is set to `1f` on a non-skill Strike (`:1327`) and decays at `dt*5f` (`:769`). `DrawChampion` (`:3964-4039`) then draws through `UiKit.AnimSprite(b, key, box, seconds, ChampionFps=8, loop, tint, topCrop: -1f, flip: ChampionFacesRight)`, trying `Character.StripKeys(clip)` most-specific-first, then the idle strip, then `SpriteGrounded(Character.SpriteKey, ...)`, then a flat `Fill`.

`ChampionFacesRight` (`:199`) is `ArtFacesLeft && ChampBox.Center.X < EnemyBox.Center.X`, and `ArtFacesLeft` is `const false` (`:197`) — so **nothing is ever flipped**.

### 3c. The enemies — three different code paths

| path | file:line | trigger | box |
|---|---|---|---|
| `DrawComposition` | :1927-2020 | `comp.Count > 1` | `w = (int)(EnemyBox.Width * ArchetypeScale)`, `h = (int)(EnemyBox.Height * ArchetypeScale)`; per-creature `new Rectangle(cx - w/2, EnemyBox.Bottom - h + bob, w, h)` where `bob = sin(_anim*2 + i*1.7)*7` |
| `DrawNormalEnemy` | :2022-2087 | `comp.Count <= 1` | `ab = new Rectangle(ebox.X, figTop + bob, ebox.Width, ebox.Height)` — **the raw 436×440 `EnemyBox`, with NO archetype scale applied**; `bob = sin(_anim*2)*8` |
| `DrawBoss` | :2098-2148 | `_isBossWave` | `new Rectangle(BossAnchor.X - 270 + lunge, BossAnchor.Y - 540, 540, 540)` — a **square** box |

`ArchetypeScale` (`HuntScreen.cs:1721-1727`): Swarm 0.58, Caster 0.78, Armoured 0.92, Bruiser 1.12. Applied only in `DrawComposition`. **A single Bruiser therefore draws at 440 px, not 492 px — an inconsistency between the two enemy paths.**

Resulting `_creatureRect[slot].H` values and the `EnemyScale` buckets they produce (`H/140 × mult`, rounded, clamped 1..5):

| case | H | ×0.6 (weakhit) | ×0.9 (death/mark) | ×1.2 (crit) | ×1.25 (strike) | ×1.3 (trap) |
|---|---|---|---|---|---|---|
| Swarm `440*0.58=255` | 255 | 1.09→**1** | 1.64→**2** | 2.19→**2** | 2.28→**2** | 2.37→**2** |
| Caster `440*0.78=343` | 343 | 1.47→**1** | 2.21→**2** | 2.94→**3** | 3.06→**3** | 3.19→**3** |
| Armoured `440*0.92=404` | 404 | 1.73→**2** | 2.60→**3** | 3.46→**3** | 3.61→**4** | 3.75→**4** |
| Bruiser `440*1.12=492` | 492 | 2.11→**2** | 3.16→**3** | 4.22→**4** | 4.39→**4** | 4.57→**5** |
| Single enemy (any archetype) | 440 | 1.89→**2** | 2.83→**3** | 3.77→**4** | 3.93→**4** | 4.09→**4** |
| Boss | 540 | 2.31→**2** | 3.47→**3** | 4.63→**5** | 4.82→**5** | 5.01→**5** (clamped) |
| slot not yet drawn (fallback `300f`) | 300 | 1.29→**1** | 1.93→**2** | 2.57→**3** | 2.68→**3** | 2.79→**3** |

### 3d. VISUAL BOUNDS: do they exist? — **No, not as a queryable concept.**

The measurement machinery exists and is good, but it is **entirely private to the two draw helpers in `UiKit`. Nothing can ask "where is this actor visible?"**

| measurer | file:line | what it returns | consumers |
|---|---|---|---|
| `UiKit.TopPadFraction(key)` | UiKit.cs:139-158 | fraction of texture height fully transparent across the top (alpha ≤ 8), cached | **1**: `AnimSprite` (:302) when `topCrop < 0` |
| `UiKit.SidePadFraction(key)` | UiKit.cs:194-219 | symmetric empty columns, the **least of any frame**, as a fraction of frame width | **1**: `AnimSprite` (:315) |
| `UiKit.BottomPadFraction(key)` | UiKit.cs:222-241 | empty rows under the lowest opaque row | **1**: `SpriteGrounded` (:254) |
| `UiKit.StripBottomPadFraction(key)` | UiKit.cs:336-356 | identical algorithm, per strip | **1**: `AnimSprite` (:322) |
| `UiKit.ContentPad(Texture2D)` | UiKit.cs:411-434 | full `(l,t,r,b)` transparent-margin `Vector4` — **the closest thing to a real visual-bounds API** | **0 — zero consumers anywhere in the repo** |

`grep -rn "TopPadFraction\|SidePadFraction\|BottomPadFraction\|StripBottomPadFraction" --include=*.cs .` outside `UiKit.cs` returns exactly one hit — a *comment* at `HuntScreen.cs:1995`. `ContentPad` is referenced only at its own declaration.

**Latent defect**: `BottomPadFraction` (`:224`, `:240`) and `StripBottomPadFraction` (`:338`, `:354`) **share the same `_bottomPadCache` dictionary**. Their algorithms happen to be byte-identical, so no wrong value results today — but they are two names for one function with a shared cache, and any divergence becomes a silent cross-contamination.

### 3e. What the visual bounds actually are (measured from the shipped art)

Every character/enemy/boss strip on disk is **4096×512** with a uniform bottom pad of 17 rows (lowest opaque row = 494). Top and side padding vary enormously. Applying `AnimSprite`'s own arithmetic (`cropY = topPx`, `srcH = 512-topPx`, `sc = 430/srcH`, `cropX = sidePx-1`, `w = (512-2·cropX)·sc`) to the 10 champions in `ChampBox` (430 tall):

| character | top pad px | side pad px | scale | **drawn width px** | **visible height px** | ground drop px |
|---|---|---|---|---|---|---|
| anvil | 35 | 99 | 0.9015 | **284** | 414.7 | 15 |
| chorus | 35 | 146 | 0.9015 | **200** | 414.7 | 15 |
| magpie | 102 | 113 | 1.0488 | **302** | 412.2 | 18 |
| metronome | 120 | 148 | 1.0969 | **239** | 411.4 | 19 |
| oathbound | 35 | 167 | 0.9015 | **162** | 414.7 | 15 |
| quiver | 35 | 50 | 0.9015 | **373** | 414.7 | 15 |
| seeker | 114 | 133 | 1.0804 | **267** | 411.6 | 18 |
| thornwall | 35 | 62 | 0.9015 | **351** | 414.7 | 15 |
| tower | 120 | 160 | 1.0969 | **212** | 411.4 | 19 |
| unbroken | 123 | 91 | 1.1054 | **366** | 411.2 | 19 |

**Visible height is effectively constant (411–415 px, a 0.9 % spread) because the top crop normalises it. Visible WIDTH ranges 162 → 373 px — a 2.30× spread** (`373/162 = 2.302`). Seeker 267 vs Magpie 302 is a 13 % gap; Oathbound vs Quiver is the real stress case the brief's §71 is asking about.

So the visible champion silhouette occupies roughly `x = 620 ± drawnWidth/2`, `y = ChampBox.Y + drop` (≈469) down to `≈ChampBox.Bottom` (881). **`ChampBox` itself (400 wide, top at 451) over-claims by 18 px vertically and by anything from 27 px to 238 px horizontally.** Nothing in the VFX path knows this.

Enemy strips, same measurement: bonecrawler top 50 / side 50; wisp 37 / 88; stone_sentinel 117 / 176; shadeling 114 / 186; soul_leech 35 / 176; rift_guardian 126 / 302; crystal_lich (boss) 35 / 204. Same story — the box is not the figure.

### 3f. Where the row actually lands

`DrawComposition` publishes `_rowCentreX` / `_rowTopY` at `:1920-1921` (resting position, deliberately excluding the lunge — comment at `:1911-1918`), `DrawNormalEnemy` at `:2041-2042`, `DrawBoss` at `:2116-2117`. The row's resting X is a compression-plus-clamp pipeline (`:1873-1905`): `spacing = min((int)(w*0.54f), room/(count-1))`, `lo = max(ArenaRect.X + half + 20 + wanted/2, ChampBox.Right + 80 + wanted/2)`, `hi = ArenaRect.Right - half - 20 - wanted/2`, `resting = lo > hi ? ArenaRect.Center.X : Clamp(EnemyBox.Center.X, lo, hi)`. Motion (`enter`, `lunge`) is added **after** the clamp.

### 3g. The one place bounds ARE debug-visualised

`DrawBossDebugOverlay` (`HuntScreen.cs:2165-2175`, F7 / `DevBossDebug`) draws `ArenaRect` red, `ArenaClip` steel, `_bossFullRect` cyan, `_bossBodyRect` green, plus a ground-pivot cross at `BossAnchor`. But `_bossBodyRect` and `_bossFullRect` are both just assigned `box` (`:2145-2146`) — **they are the same rectangle, and neither is a measured visual bound.** There is no equivalent overlay for the champion or ordinary enemies, and nothing anywhere draws an effect's destination rect or an anchor point (brief §70 has no implementation).

### 4. The animation rig: how a character strip is drawn, whether frames have transparent padding, and whether anything measures the opaque bounds of a sprite.

### 4a. There are TWO things called a rig; only one runs

**(i) `src/IdleXIdle.Core/Animation/` — a bone/keyframe cutout rig that is DEAD CODE.**
- `Rig.cs` (176 lines): `Vec2`, `Bone` (Id/ParentId/Offset/RestAngle), `BoneTransform` (with `SnappedAngle`), `Rig` with `SnapSteps` (default 16), a topological evaluation order, `Snap(radians)` and `Evaluate(pose, rootPosition)`.
- `Clip.cs` (176 lines): `Easing` enum, `Keyframe(TimeSeconds, Angle, Easing)`, `Clip { Name, DurationSeconds, Loops, Tracks }` with `Sample(timeSeconds)`.
- Its own doc-comment (`Rig.cs:56-59`) calls it "the project's highest technical risk … It exists to be spiked against the Attacker's strike arc". ADR-002 ("cutout animation rig, Accepted") governs it.
- **Consumers**: `grep -rn "new Rig(\|new Clip(\|BoneTransform\|SnapSteps" --include=*.cs .` returns hits only in `tests/unit/IdleXIdle.Core.Tests/Animation/RigTests.cs` and `ClipTests.cs`. **Zero production consumers.**
- `HuntScreen.cs:7` has `using IdleXIdle.Core.Animation;` but consumes nothing from it — the `Bone` identifier at `HuntScreen.cs:40` is a local `private static readonly Color Bone`, and `Descent` (`:132-136`) is `IdleXIdle.Core.Expeditions`.
- `Rig.cs:44-49` justifies angle snapping by "the hard 1px edges the whole art direction depends on (art-bible: legibility rests on flat fills with hard boundaries)" — **that art direction was abandoned**; the live renderer uses `SamplerState.LinearClamp` with non-integer scaling (CLAUDE.md's own Forbidden Patterns block says so, and ADR-003 is Superseded).

**(ii) The rig that actually runs: horizontal sprite-strip flipbooks through `UiKit.AnimSprite`.** This is the whole animation system.

### 4b. How a character strip is drawn — `UiKit.AnimSprite`, `src/IdleXIdle.Game/UiKit.cs:278-330`

```csharp
public bool AnimSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps,
                       bool loop, Color tint, float topCrop = 0f, bool flip = false)
```
Step by step:
1. `if (Assets.Get(stripKey) is not { } tex || tex.Height <= 0) return false;` (:280) — **missing art returns false so callers can chain fallbacks**.
2. `var fw = tex.Height; var frames = Math.Max(1, tex.Width / fw);` (:281-282) — **frames are assumed square**; a non-multiple width logs a `Debug.WriteLine` warning (:287-288) but still draws.
3. Frame index (:290-292): `Game1.ReducedMotion ? 0 : loop ? ((int)(seconds*fps) % frames + frames) % frames : Clamp((int)(seconds*fps), 0, frames-1)`. **Reduced-motion is honoured in this one place for the whole game.**
4. `if (topCrop < 0f) topCrop = TopPadFraction(stripKey);` (:302) — **a negative `topCrop` means "measure it"**. Every arena caller passes `-1f`.
5. `cropY = (int)(fw * Clamp(topCrop, 0, 0.6f)); srcH = tex.Height - cropY;` (:305-306).
6. `cropX = (int)(fw * Clamp(SidePadFraction(stripKey), 0, 0.4f)); srcW = fw - 2*cropX;` (:316-317).
7. `src = new Rectangle(i*fw + cropX, cropY, srcW, srcH); sc = box.Height / (float)srcH; w = (int)(srcW * sc);` (:318-320).
8. `drop = (int)MathF.Round(StripBottomPadFraction(stripKey) * fw * sc);` (:324).
9. `b.Draw(tex, new Rectangle(box.Center.X - w/2, box.Y + drop, w, box.Height), src, tint, 0f, Vector2.Zero, flip ? FlipHorizontally : None, 0f);` (:325-326).

So the contract is: **fill the box HEIGHT with the trimmed figure, centre horizontally on `box.Center.X`, plant the visible sole on `box.Bottom`.** Width is whatever the aspect gives; `box.Width` is used only for `Center.X`.

`SpriteGrounded` (`UiKit.cs:249-266`) is the static-texture twin — same crop/scale/drop shape, `BottomPadFraction` instead of `StripBottomPadFraction`. `Sprite` (`UiKit.cs:121-131`) is the un-grounded variant.

Champion callers: `HuntScreen.cs:4028` (`Character.StripKeys(clip)` loop) and `:4030` (idle fallback), both `ChampionFps = 8f` (`:4049`), both `topCrop: -1f`. Enemy callers: `HuntScreen.cs:2003-2005` (composition, fps 16/12), `:2048` (single, fps 11/9), `:2126` and `:2135` (boss, fps 10/8). Death clips: `:1810` and `:2126`, `DeathFps = 10f` (`:1772`). Flash overlay: `HuntScreen.cs:3736` draws `AssetLibrary.MaskKey(stripKey)` — a white-silhouette texture built by `AssetLibrary.WhiteMask` (`:214-231`) by replacing every pixel's colour with its alpha.

Strip naming: `Character.StripKey(clip)` = `char_<id>_<clip>_strip8_512` (`Character.cs:154`); `StripKeys(clip)` (`:174-179`) yields the specific clip then `GenericClipFor(clip)` (`:182-190`: `strike`→`attack`; `projectile|mark|transformation|aura`→`cast`; `trap`→**null, no fallback**). Enemies: `{key}_{idle|attack|death}_strip8_512` (`HuntScreen.cs:1932-1934`, `:2044-2046`).

### 4c. Do frames have transparent padding? — **Yes, substantially, and it varies wildly per asset.**

Measured directly from the shipped PNGs (alpha > 8 threshold, exactly the code's rule). All character/enemy/boss strips are **4096×512 = 8 frames of 512×512**; all have a **bottom pad of 17 rows** (lowest opaque row = 494, `botPad = 0.0332`).

Top pad (px of 512): anvil/chorus/oathbound/quiver/thornwall **35**; magpie **102**; seeker **114**; metronome/tower **120**; unbroken **123**. Enemies: soul_leech **35**, wisp **37**, bonecrawler **50**, shadeling **114**, stone_sentinel **117**, rift_guardian **126**; crystal_lich **35**.

Side pad (least of any frame, px of 512): quiver **50**, thornwall **62**, unbroken **91**, anvil **99**, magpie **113**, seeker **133**, chorus **146**, metronome **148**, tower **160**, oathbound **167**. Enemies: bonecrawler 50, wisp 88, stone_sentinel 176, shadeling 182, soul_leech 172, rift_guardian **295**, crystal_lich 199.

The strips are also inconsistent *within* a character: `char_seeker_idle` has sidePad 133 but `char_seeker_attack` has sidePad **58** — so the attack clip is drawn 2.3× wider than the idle for the same box. (This is measured per-key, so the figure does not jump *mid*-clip; it jumps *between* clips.)

The effect strips have padding too, and it is not centred. Per-frame content boxes (all 8 frames measured):

| strip | widest content | vertical span in the 512 frame | fill fraction |
|---|---|---|---|
| `fx_shield` | 386 × 234 | rows **110–361** (centre 46 % down) | 0.75 × 0.46 |
| `fx_aura` | 232 × 416 | rows **51–466** | 0.45 × 0.81 |
| `fx_shield_break` | 466 × 472 | near-full frame | 0.91 × 0.92 |
| `fx_hit` | 416 × 392 | — | 0.81 × 0.77 |
| `fx_death` | 420 × 401 | — | 0.82 × 0.78 |
| `fx_weakhit` | 350 × 334 | — | 0.68 × 0.65 |

**`VfxPlayer` does not crop or measure any of this** — it draws the whole 512 frame into `h × h` centred on the spawn point (`VfxPlayer.cs:267`, `:275-278`). So the padding is scaled up along with the art, and the effective effect is smaller than its scale number implies. Worked example for the shield shell: frame drawn 270×270 at `270/512 = 0.527` px-per-px; content rows 110–361 map to dest y `705-135 + 110·0.527 = 628` through `570 + 361·0.527 = 760`. **Visible dome = 203 × 132 px** against a **412 px** champion — `132/412 = 0.32×` the champion's visual height, sitting from 39 % to 71 % down the body. The brief's §65 target is 1.10–1.20× (≈453–494 px). The gap is **≈3.5×**.

Same method for the aura: drawn 666×666 at `666/512 = 1.301`; content rows 51–466 → dest y `566-333 + 51·1.301 = 299` through `233 + 466·1.301 = 839`; content cols 139–372 → 302 px wide. **Visible aura = 302 × 541 px, from 170 px above the champion's head to 42 px above his feet — it floats and never reaches the ground.** On Quiver (373 px wide) the aura is *narrower* than the character it wraps.

### 4d. Does anything measure opaque bounds? — Yes, five functions; four are draw-internal, one has no consumer

See Q3d for the full table. Summary: `TopPadFraction`, `SidePadFraction`, `BottomPadFraction`, `StripBottomPadFraction` all do a full `GetData` alpha scan (cached per key) and are called **only** from inside `AnimSprite`/`SpriteGrounded`. `ContentPad(Texture2D)` (`UiKit.cs:411-434`) returns the complete `(left, top, right, bottom)` fractional margin — the one function shaped like a real visual-bounds API — and **has zero consumers**.

### 4e. Test coverage

`tests/unit/IdleXIdle.Core.Tests/Animation/{RigTests,ClipTests}.cs` cover the **dead** Core rig. There is **no test anywhere touching `VfxPlayer`, `AnimSprite`, any pad fraction, or any actor rectangle** — `grep -rn "Vfx\|ShowHitEffects\|TopPad" tests/**/*.cs` is empty. `tests/unit/IdleXIdle.Core.Tests/Presentation/hunt_screen_feedback_test.cs` pins HuntScreen by reading its **source text**, and one case (`test_the_wave_total_bar_is_gone_but_the_per_creature_pips_stay`, :81-92) slices the file between the literal strings `"private void DrawComposition"` and `"private void DrawNormalEnemy"` — **renaming or reordering either method breaks that test.**

### 5. Which effects are PERSISTENT (shield barrier, fields) and how they survive a frame where their event is not replayed.

### 5a. Exactly two persistent effects exist, and both use `VfxPlayer.Hold`

The persistence model is: **re-assert every frame from state, or it is gone next `Update`.** `Hold` (`VfxPlayer.cs:218-237`) stamps the key into `_heldThisFrame`; `Update` (`:247-252`) removes any `_held` entry that key is missing from and then clears the set. There is no `Stop()` — deliberate (`:214-216`). Both callers are invoked unconditionally from `UpdateFight` at `HuntScreen.cs:1194-1196`:
```csharp
PulseAuraOnClock(dt);
HoldAura();   // re-asked every frame; VfxPlayer drops it the moment we stop
HoldShieldBarrier();
```
Critically, these three lines sit **above** both of `UpdateFight`'s early returns (the `_breakTimer` gate at `:1198-1205` and the `_enemyEnter` gate at `:1209`), so **the field and the barrier keep standing through the between-wave breath and the slide-in** — see the comment at `:1191-1193` ("THE FIELD DOES NOT STOP BETWEEN WAVES … that left the aura off screen for nearly half the cycle").

#### (1) The held FIELD / aura — `HoldAura`, `HuntScreen.cs:3812-3827`

**Source of truth**: not an event at all. `BeginWave` reads the wave's own skill list (`HuntScreen.cs:947-949`):
```csharp
var fieldSk = _waveSkills.FirstOrDefault(k => k.Def.Kind == SkillKind.Field);
_auraFxKey = fieldSk?.Def.FxKey;
_auraColour = fieldSk is null ? null : SourceColor.GetValueOrDefault(fieldSk.Source, Bone);
```
Gate (`:3817`): `if (_auraColour is not { } colour || _mode == Mode.Downed) return;` — **explicitly not gated on `Mode.Fighting`** (comment at `:3814-3816`).

**Brightness, not existence, is the event channel.** `PulseAuraOnClock` (`:3780-3786`) runs on wall-clock `dt` and simply resets `_auraSincePulse` every `AuraPulseSeconds = 1f` while fighting; `HoldAura` turns that into `level = AuraRest 0.38 + (1 - 0.38)·spike³` where `spike = 1 - clamp(_auraSincePulse/AuraSpikeSeconds 0.42, 0, 1)`. This design is explicit at `:3784` — "the tick is the BRIGHTNESS peak now, not a spawn". The `BattleEventKind.Aura` event (`:1305-1307`) is used **only** to classify which Strikes belong to the tick, never to spawn art.

So this effect survives an un-replayed frame trivially: **it was never driven by events.** It survives a wave boundary because `BeginWave` reassigns `_auraFxKey`/`_auraColour` and deliberately does not call `_vfx.Clear()` (`:912-914`). It survives a dev seek because `_vfx.Clear()` at `:1238` only drops the instance, which the very next `HoldAura()` recreates.

**But two of the four Field skills produce nothing** (see Q2b): `_auraFxKey` becomes `"press"` or `"wilt"`, `FxFor` yields `"fx_press"`/`"fx_wilt"`, `AssetLibrary` has no such key, and `Hold` returns at `VfxPlayer.cs:221` without a sound. Coverage: BRAND→`fx_mark` ✓, MIRE→`fx_aura` ✓, **PRESS ✗, WILT ✗**.

#### (2) The held SHIELD BARRIER — `HoldShieldBarrier`, `HuntScreen.cs:3846-3859`

**Source of truth**: `if (_replay?.HasShield != true || _mode == Mode.Downed) return;` (`:3848`).

`WaveReplay.HasShield => CurrentShield > 0` (`src/IdleXIdle.Core/Expeditions/WaveReplay.cs:154`). `CurrentShield` is **accumulated state**, mutated in `Apply` (`WaveReplay.cs:356-365`):
```csharp
case BattleEventKind.ShieldGained:   CurrentShield += e.Amount;
case BattleEventKind.ShieldAbsorbed: CurrentShield = Math.Max(0, CurrentShield - e.Amount);
case BattleEventKind.ShieldBroken:   CurrentShield = 0;
```
Because it is a stored integer rather than a per-frame event flag, **a frame in which no shield event replays leaves `CurrentShield` untouched, `HasShield` still true, and the barrier still asked for.** This is the one place the codebase already does what the brief's §69 asks. `WaveReplay.cs:105-109` says so directly: "Reconstructed from the wave's own events, so scrubbing to the middle of a fight shows the bar the fight actually had at that moment" — and the dev-seek path (`HuntScreen.cs:1231-1247`) proves it: it rebuilds a fresh `WaveReplay`, calls `_vfx.Clear()`, replays `Advance(seek)`, and the barrier reappears on the next frame purely from reconstructed state.

Breath: `breathe = ShieldShellRest 0.16 + ShieldShellSwing 0.14 · (0.5 + 0.5·sin(_anim·1.6))`, frozen at the 0.5 phase under `UiMotion.Reduced` (`:3850-3852`).

### 5b. The three shield ONE-SHOTS are not persistent and do not need to be

`ShieldGained` (`:1464`), `ShieldAbsorbed` (`:1476`), `ShieldBroken` → `PlayShieldBreak()` (`:3875-3882`) are ordinary `Play` calls. `PlayShieldBreak`'s doc (`:3868-3872`) is accurate: the break burst is *not* held and does not loop; the standing shell simply stops being asked for on the frame `CurrentShield` hits 0.

`_shieldSeen` (`HuntScreen.cs:3151`) is a separate latch for the HUD strip only (`ShieldStripShown`, `:3149`), reset in `BeginWave` (`:854`) and set at `:1243`, `:1457`, `:1473`, `:1506`. It has no effect on the barrier.

### 5c. Persistent states that have NO effect representation at all

| state | where it lives | representation | gap |
|---|---|---|---|
| **Defence break stacks** | `WaveReplay.CreatureBreaks(int)` (`WaveReplay.cs:85`) | a 26×26 `icon_effect_break` badge + `x{stacks}` text at `HuntScreen.cs:1969-1974` | **drawn only inside `DrawComposition`.** `DrawNormalEnemy` and `DrawBoss` never draw it — so on a single-enemy wave or a boss wave, break stacks are invisible. The design note at `:1953-1968` says "slow and attack break belong here too" — neither exists |
| **Slow / attack break** | sim-side | — | no visual at all |
| **Amplification** (`SkillEffect.Amplify`: CALL, BRAND) | sim-side | only the transient cast effect | no persistent representation |
| **Persistent Field on the enemy row** (trap/mire ground area) | sim-side | one-shot `Play` at `:1602` only | the field is a state; the picture is an event |

### 5d. The mechanism's structural limits, for the new contract

1. **`_held` is keyed by asset key** (`VfxPlayer.cs:152`, `:223-233`), not by owner + anchor. Two persistent effects that resolve to the same strip would silently share one instance, one position and one tint. Today's two keys differ, so it works by luck.
2. **A held effect has no `Fade` and no `GrowTo`** (`VfxPlayer.cs:116`, and `Anim.At` short-circuits on `Loop` at `:61`). Brightness is the caller's only channel.
3. **The two `Hold` callers pre-compute the drawn height as `scale * BaseUnitPx`** (`HuntScreen.cs:3824`, `:3856`) with no `/ _vfx.Scale` term, duplicating `VfxPlayer.Draw`'s formula (`:275`) minus the canvas-scale divisor. This is correct only while `_vfx.Scale == 1`.
4. **Both persistent effects anchor to `ChampBox`, not to the drawn champion** — so both slide out from under the champion by up to 40 px on every lunge (see Q2a).
5. **Both anchor by arithmetic on the box, not by any bound**: aura `ChampBox.Bottom + 18 - h/2` (feet-anchored), shield `ChampBox.Bottom - 40 - h/2`. Because `VfxPlayer` centres the *frame* and the frames are asymmetrically padded (§4c), the visible art lands somewhere neither constant predicts.

**Files this area will change**

- src/IdleXIdle.Game/VfxPlayer.cs — the whole spawn/animate/draw contract. `Play` (:181) and `Hold` (:218) take raw (x, y, scale) with no anchor, no pivot, no layer, no follow mode; `Draw` (:255-282) is the single flat additive pass and the only place a destination rect is built (:275-278). `growTo` and `frameW` are dead parameters. `BaseUnitPx = 104` (:137) and the `Scale` field (:173) are the whole sizing vocabulary.
- src/IdleXIdle.Game/HuntScreen.cs — 4255 lines, and every one of the 18 spawn sites lives here (:1375, :1384, :1419, :1423, :1427, :1441, :1442, :1464, :1476, :1594, :1599, :1602, :1605, :1608, :1611, :3825, :3857, :3877). Also owns `ChampBox` (:164), `EnemyBox` (:169), `BossAnchor` (:211), `ArenaRect`/`ArenaClip` (:555, :576), `EnemyPoint` (:1777), `EnemyScale` (:1782), `PublishCreature`/`_creatureRect` (:1768-1776), `FxFor` (:3660-3667), `HoldAura` (:3812), `HoldShieldBarrier` (:3846), `PlayShieldBreak` (:3875), the three enemy draw paths (:1927, :2022, :2098) and the champion draw (:3964). The lunge/draw-rect mismatch is at :1699-1701.
- src/IdleXIdle.Game/UiKit.cs — the real animation rig. `AnimSprite` (:278-330), `SpriteGrounded` (:249-266), `Sprite` (:121-131), and the four opaque-bounds measurers `TopPadFraction` (:139), `SidePadFraction` (:194), `BottomPadFraction` (:222), `StripBottomPadFraction` (:336) plus the consumer-less `ContentPad` (:411). Any queryable visual-bounds API has to be born here, and the shared `_bottomPadCache` (:91) between the two bottom-pad functions needs untangling.
- src/IdleXIdle.Game/AssetLibrary.cs — the `fx_` alias table (:176-185) is the resolution layer `FxFor` falls through to. It is missing `fx_press`, `fx_weep`, `fx_wilt`, `fx_bind_chain`, and carries an orphan `fx_levelup` (:182). `Get`/`Has` (:198-200) return null silently, which is what makes a missing effect invisible rather than loud.
- src/IdleXIdle.Core/Builds/SkillCatalogue.cs — the FxKey/ClipKey source of truth (`FxKey` in the record at :223; the 12 skills at :351, :383, :415, :447, :477, :514, :545, :584, :616, :646, :681, :712). PRESS (:386 `press`), WEEP (:587 `weep`) and WILT (:715 `wilt`) name effect keys nothing can resolve. If the new contract adds a per-skill presentation profile, this is where the anchor/scale/layer fields attach.
- src/IdleXIdle.Core/Characters/Character.cs — `SpriteKey` (:151), `StripKey` (:154), `StripKeys` (:174-179) and `GenericClipFor` (:182-190). If per-character visual bounds become authored data (brief §64), this record is where they belong, and `trap` currently has no generic fallback (:187).
- src/IdleXIdle.Core/Animation/Rig.cs and Clip.cs — the bone/keyframe cutout rig. Zero production consumers (only RigTests.cs / ClipTests.cs). Its justifying premise — angle snapping to protect "hard 1px edges" — belongs to the abandoned pixel-art direction. Either the refactor's new bounds/anchor types land here (Core is MonoGame-free, ADR-001) or this directory is deleted; leaving it is exactly the dormant-code species this repo keeps finding.
- src/IdleXIdle.Core/Presentation/CanvasFit.cs and PageFrame.cs — the presentation namespace a Core-side `ActorBounds` / `VfxAnchor` type would naturally join. `CanvasFit` also still carries an "Integer scale only, always" doc block (:16-21) that its own `PresentFit` (:73-79) contradicts — stale relative to the LinearClamp renderer.
- src/IdleXIdle.Game/Game1.cs — :3370-3373 asserts "VfxPlayer.cs is KEPT and left unwired on purpose", which is false since HuntScreen picked it up. Delete or rewrite when the player is refactored, or the next reader repeats the mistake.
- tools/check_asset_keys.py — the gate that was supposed to catch a missing effect. `CALLS` (:33-35) includes `_vfx.Play`, but `LITERAL` (:39) extracts string literals only, so every `FxFor(def)` site (:10-15 in the Play table) is invisible to it. Needs to understand the computed-key path, or the new contract needs to make keys literal again.
- design/art/arena-art-contract.md — §2 size chart (:36-38) says effects are "played at scale 1–3" and authored at "pixen 256" into a 512 frame; the code plays at 1–5 plus 6.4. §5 (:205-224) is the effect catalogue and omits `press`/`weep`/`wilt` entirely. Both are stale against the runtime.
- tests/unit/IdleXIdle.Core.Tests/Presentation/hunt_screen_feedback_test.cs — pins HuntScreen against its own SOURCE TEXT. `test_the_wave_total_bar_is_gone_but_the_per_creature_pips_stay` (:81-92) slices between the literal strings "private void DrawComposition" and "private void DrawNormalEnemy"; renaming or reordering either method fails the test even if behaviour is unchanged.
- tests/unit/IdleXIdle.Game.Tests/hunt_feedback_test.cs — the natural home for the brief's §105/§106 VFX contract tests. Today there is no test anywhere in the repo that touches VfxPlayer, AnimSprite, a pad fraction, or an actor rectangle.

**Risks**

- THE THREE DEAD EFFECTS ARE THE HEADLINE. PRESS, WEEP and WILT name FxKeys (`press`, `weep`, `wilt` at SkillCatalogue.cs:386, :587, :715) that resolve to nothing: no per-character variant exists (only mark/projectile/strike/transformation/trap were generated) and AssetLibrary's alias table (:176-185) has no entry. VfxPlayer.Play/Hold return silently on a null texture (VfxPlayer.cs:185, :221). The art is on disk and loaded — assets/art/VFX/{press,weep,wilt}/fx_*_strip8_512.png. PRESS and WILT are 2 of the 4 Field skills, and Field is what drives the held aura (HuntScreen.cs:947-949), so those builds have NO field effect at all. Fix the aliases before touching anything else, or the refactor will be measured against a broken baseline.
- THE GATE IS BLIND TO THIS CLASS OF BUG. tools/check_asset_keys.py:33-39 matches `_vfx.Play` but extracts only string LITERALS, so the six `FxFor(def)` sites are invisible. Any new contract that keeps computed keys must teach the gate the resolution rule, or it will ship the same silence again.
- EVERY CHAMPION-SIDE EFFECT IS ANCHORED TO A RECT THE CHAMPION IS NOT STANDING IN. HuntScreen.cs:1699-1701 draws the champion at `ChampBox.X + push` where `push = _champLunge * 40f`; all ten champion-side spawn sites use the un-pushed `ChampBox.Center.X`. Whatever anchor vocabulary replaces this must resolve against the DRAWN rect, not the layout rect.
- VISUAL BOUNDS ARE MEASURED BUT NOT EXPOSED. UiKit's four pad-fraction functions are private to AnimSprite/SpriteGrounded, and ContentPad (UiKit.cs:411) — the one that returns a full bounding box — has ZERO consumers. The measurement is correct and cheap (cached, one GetData per key); the missing piece is an API. Do not rewrite the measurement; expose it.
- THE SPREAD THE BRIEF WORRIES ABOUT IS IN WIDTH, NOT HEIGHT. Because AnimSprite crops the top pad and fills the box height, all ten champions render 411–415 px tall (0.9 % spread). Drawn WIDTH runs 162 px (oathbound) to 373 px (quiver) — 2.30×. Seeker 267 vs Magpie 302 is only 13 %; testing the shield on Seeker and Magpie alone will NOT catch the failure. Oathbound and Quiver are the real stress pair.
- SIDE PAD IS PER-KEY, SO A CHARACTER CHANGES WIDTH BETWEEN CLIPS. char_seeker_idle has sidePad 133 px, char_seeker_attack has 58 px — the same character draws 267 px wide idling and ~440 px wide swinging. Any width-derived anchor or scale must decide which clip it measures, or effects will jump on every cast.
- EFFECT FRAMES ARE PADDED AND OFF-CENTRE, AND VfxPlayer DOES NOT CROP THEM. fx_shield's content is 386×234 sitting in rows 110–361 of a 512 frame — 46 % down, 46 % of the height. Drawn at scale 2.6 (270 px), the visible dome is 203×132 px against a 412 px champion: 0.32× his visual height, spanning 39 %–71 % down the torso. The §65 target of 1.10–1.20× means ≈453–494 px, so the shield is ~3.5× too small. A pure scale bump would drag 46 % of empty frame with it; the contract needs content-aware sizing (or regenerated assets) or the aura's over-budget 1.301× problem reappears at the shield.
- THE AURA FLOATS AND IS OVER THE ASSET-SCALE BUDGET. Held at scale 6.4 → 666 px from a 512 frame = 1.301× native (2.602× the 256-px pixen source the contract says it was authored at). Its content spans dest y 299–839 while the champion spans 469–881: 170 px of aura above the head, 42 px of gap below its bottom and the feet. §73's 0.75–1.25× budget is already breached, and §16's "do not fix bad assets with giant runtime scale" points straight at this number.
- SIZE IS QUANTISED TO FIVE BUCKETS. `EnemyScale` (HuntScreen.cs:1782) returns `Clamp((int)Round(H/140 * mult), 1, 5)` — an integer. So 104/208/312/416/520 px are the ONLY enemy-side effect sizes the game can produce, and a boss at H=540 and an Armoured creature at H=404 collapse into adjacent buckets. A continuous relative-scale model (§65) is a behaviour change, not a refactor, and will move every effect on screen.
- THE PROJECTILE SCALE IS INTEGER DIVISION. `Math.Clamp((tx - ChampBox.Right) / 104, 2, 5)` at HuntScreen.cs:1595 — all ints. A single enemy gives 680/104 = 6 → clamped to 5; a near swarm creature gives 1 → clamped to 2. The bolt's size is a step function of the gap with only four reachable values.
- _creatureRect IS ONE FRAME STALE AND INCLUDES THE BOB. PublishCreature runs inside Draw (HuntScreen.cs:1943, :2040, :2115); all Play sites run inside UpdateFight. EnemyPoint also falls back to `(_rowCentreX, _rowTopY + 110)` (:1778) when a slot has never been drawn — the first frame of a wave, and any slot the current draw path skipped. Any anchor system that reads actor bounds inherits the same ordering hazard.
- TWO ENEMY PATHS DISAGREE ON SIZE. ArchetypeScale (0.58–1.12) is applied in DrawComposition (:1857-1859) but NOT in DrawNormalEnemy (:2033) — a lone Bruiser draws at 440 px where four of them draw at 492 px. Any bounds-derived scale will surface this inconsistency as a visible size jump between a 1-creature and a 2-creature wave.
- THERE IS NO LAYER MODEL TO PRESERVE — AND NOTHING BEHIND THE ACTOR. VfxPlayer.Draw iterates `_held.Values.Concat(_active)` in one Deferred additive batch (:264), and the whole pass runs after both actors in DrawArena (:1703). Adding a BehindActor/GroundBehind tier means splitting the pass, which means splitting the additive batch — and the batch boundary is load-bearing: it carries the End/Begin dance with Rasterizer/RestoreRasterizer (:262-263, :281) that keeps the arena scissor correct for callouts drawn afterwards (documented regression, :162-167).
- THE ADDITIVE PASS IS DELIBERATELY UNSCISSORED. HuntScreen.cs:1659-1660 sets `_vfx.Rasterizer = null` on purpose (comment :1652-1655: the clip edge cut bursts flat and "gave the arena away"). Do not re-scissor effects while widening them; the rail panels drawn afterwards are what hides the overhang.
- THE _held DICTIONARY IS KEYED BY ASSET KEY, NOT BY OWNER. VfxPlayer.cs:152, :223. Two persistent effects resolving to the same strip would silently collapse into one instance with one position and one tint. Today's two keys differ by luck (fx_aura/fx_mark vs fx_shield); a richer persistent-effect set will hit this immediately.
- THE TWO Hold CALLERS DUPLICATE THE SIZING FORMULA WITHOUT ITS CANVAS-SCALE DIVISOR. HuntScreen.cs:3824 and :3856 compute `h = scale * VfxPlayer.BaseUnitPx` to place the effect's edge; VfxPlayer.Draw:275 computes `h = round(scale * BaseUnitPx * growth / Scale)`. They agree only because HuntScreen forces `_vfx.Scale = 1` at :1632. That coupling is invisible and will break the moment anything else draws effects.
- THE SHIELD BARRIER'S PERSISTENCE MODEL IS ALREADY CORRECT — DO NOT REPLACE IT. WaveReplay.CurrentShield is accumulated state (WaveReplay.cs:356-365), so HasShield survives any frame with no event and reconstructs exactly under a dev seek (HuntScreen.cs:1231-1247). §69 asks for this shape; it exists. Extend it to Break/Amplify/Field rather than inventing a second mechanism.
- PERSISTENT ENEMY STATES ARE MISSING OR HALF-DRAWN. Defence-break stacks draw a badge only inside DrawComposition (HuntScreen.cs:1969-1974) — never on a single enemy and never on a boss. Slow, attack break and Amplify have no visual at all. §69 lists Break and Amplification as must-reconstruct.
- fx_levelup IS AN ORPHAN. Aliased at AssetLibrary.cs:182, file present, played by nothing. §112 (remove unused generated assets) applies.
- THE CORE ANIMATION RIG IS DEAD AND ITS PREMISE IS OBSOLETE. src/IdleXIdle.Core/Animation/{Rig,Clip}.cs have zero production consumers (tests only), and Rig.cs:44-49 justifies angle snapping by an art direction the project abandoned (LinearClamp, non-integer scale — CLAUDE.md Forbidden Patterns; ADR-003 Superseded). ADR-002 still says Accepted. Deleting it is a documentation change too; keeping it is dormant code by the repo's own definition.
- A SOURCE-TEXT TEST WILL BREAK ON A PURE RENAME. tests/unit/IdleXIdle.Core.Tests/Presentation/hunt_screen_feedback_test.cs:81-92 slices HuntScreen.cs between the literal strings "private void DrawComposition" and "private void DrawNormalEnemy". Renaming or reordering either method fails the suite with no behavioural change.
- NOTHING GUARDS ANY OF THIS TODAY. There is no test in the repo touching VfxPlayer, AnimSprite, a pad fraction, an actor rectangle, or a spawn position — so every regression in this area is currently a playtest-only discovery, and the refactor has no baseline to prove it did not break the working parts (projectile travel easing, the additive fade tail, the wave-boundary aura continuity, the reduced-motion freeze at UiKit.cs:290).
- ALL 68 fx STRIPS ARE 4096x512 (8 SQUARE 512-px FRAMES). VfxPlayer infers the frame count from `sheet.Width / sheet.Height` (VfxPlayer.cs:187-188) and Hold hard-codes `FrameW = sheet.Height` (:227). Any regenerated asset at a different aspect breaks Hold silently and breaks Play unless `frameW` — currently a dead parameter — is passed.

## UI frames, nine-slice, raster quality, GEAR inventory

### 1. Every frame-drawing helper in UiKit: what it does, whether it nine-slices, and what asset it uses. Quote the nine-slice implementation.

There is exactly ONE nine-slice routine in the codebase (`UiKit.NineSlice`, private) and exactly one public door onto it that is never used (`PanelNine`). Everything else is either a 3-slice, a 5-slice, or a whole-texture stretch.

### The complete table (`src/IdleXIdle.Game/UiKit.cs`)

| # | Helper | Line | Nine-slices? | Asset it uses | What it does |
|---|---|---|---|---|---|
| 1 | `Panel(b, r, gold=false)` | 518 | **YES** (via `PanelAt`→`NineSlice`, corner 40) | `ui_panel_medium` / `ui_panel_square` / `ui_panel_vertical`, **chosen by aspect** in `PanelArtKey` (589) | Tier-1 ornate surface. `gold:true` only changes the tint to `(0xFF,0xDC,0xA0)`; it does **not** change the asset. 13 call sites. |
| 2 | `PanelQuiet(b, r, alpha=1)` | 552 | **YES** (same path) | same three assets | Tier-2. The *same* art, multiplied by `QuietFrame = (0x58,0x52,0x62)` (556). 25 call sites. |
| 3 | `Plate(b, r, accent?, alpha)` | 569 | no | **none — 1×1 `_pixel`** | Tier-3. `Fill(r, UiInk.Plate)` + four 1 px `Fill` rules + optional 5 px accent bar. 36 call sites. **This is what GEAR's INVENTORY panel draws.** |
| 4 | `PanelAt(b, r, tint, gold)` | 644 | **YES** | resolved by `PanelArtKey(r)` at 646 | Private router for 1 and 2. Falls back to `Fill` + a 1 px top/bottom edge when the texture is missing (650–657). |
| 5 | `PanelNine(b, r, key, cornerPx=40, tint?)` | 771 | **YES** | *caller-chosen key* | **DEAD.** Zero call sites in `src/` — the only two occurrences of the name are its own definition (771) and a doc-comment (546). Falls back to `Panel(b,r)` when the key is missing. |
| 6 | `NineSlice(b, tex, r, srcCornerMax, tint)` | 777 | **the implementation** | any | 9 `b.Draw` calls. Quoted in full below. |
| 7 | `PanelInner(r)` | 750 | n/a | n/a | Static: `r` inset by `clamp(PanelCorner=40, …, min(w,h)/2-2)`. Layout only. |
| 8 | `Bar(b, x,y,w,h,pct,color)` | 806 | no — **whole-texture stretch** | `ui_bar_frame` + `ui_bar_fill` (both 256×64) | `b.Draw(frame, rect)` then the fill inset by a hardcoded 2 px. 3 call sites, all `RosterScreen` (608, 622, 890). |
| 9 | `BarArt(b, r, pct, type)` | 1276 | no — **5-slice when wide, whole stretch otherwise** | `ui_bar_{type}_frame` / `_fill`, 256×64 | `wide` = `r.Width > r.Height*frame.Width/frame.Height*1.15f` (1299) → `HSliceOrnament`; else `b.Draw(frame, r)` (1305). 6 call sites. |
| 10 | `Pill(b, right, y, …)` | 830 | **no — deliberately not** | `ui_panel_small` (256×256) | `b.Draw(cap, r, Color.White)` at 844, into a 60 px-tall capsule. Comment 841–843 records that a 9-slice was tried on 2026-08-23 and reverted because the ~50 px corner ornaments smeared across the text. |
| 11 | `Button(b, r, label, …)` | 881 | no — **3-slice or whole stretch** | `ui_button_primary` / `ui_button_secondary` / `ui_button_disabled` (all 256×96) | 913: `if (r.Width > r.Height * tex.Width/tex.Height * 1.15f) HSliceScaled(…, ButtonCapSrcPx=44) else b.Draw(tex, r)`. |
| 12 | `Field(b, r, enabled, lit, well?)` | 998 | no — **always 3-slice** | `ui_button_primary` / `ui_button_secondary` | 1004: `HSliceScaled(b, tex, r, ButtonCapSrcPx, …)`. Paints an opaque well first (1002) because `ui_button_primary`'s interior is transparent. |
| 13 | `HSliceOrnament(b,t,r,srcCap,ornX0,ornX1,tint)` | 1025 | no — **5-slice** | bar frames | Caps + centre ornament scale with height (`scale = r.Height/t.Height`); the two plain runs stretch. Falls back to a whole stretch when too narrow (1032). Called only from `BarArt`. |
| 14 | `HSliceScaled(b,t,r,srcCap,tint)` | 1047 | no — **3-slice, caps scaled by height** | button art | `dstCap = round(srcCap * r.Height / t.Height)`, clamped to `r.Width/2`. Called from `Button` (913) and `Field` (1004). |
| 15 | `HSlice(b,t,r,cap,tint)` | 1097 | no — **3-slice, fixed caps** | any | **DEAD.** One occurrence in the whole tree: its own definition. |
| 16 | `CloseButton(b,r,mouse,clicked)` | 1080 | no | `icon_close` (64×64) | `SpriteFit` (aspect-preserving) + a drawn `×` fallback. |
| 17 | `ScrollBar(b, track, first, visible, total)` | 1115 | no | **none** | `Fill` track + 1 px rules + a `Fill` thumb. `ui_scrollbar_track` / `ui_scrollbar_handle` exist on disk and are **never referenced**. |
| 18 | `HoverTip(b, text, anchor)` | 1245 | no | **none** | Hand-drawn: shadow `Fill`, body `Fill`, one 2 px gold top rule (1258–1260). |
| 19 | `KeyCap(b,x,y,key,…)` | 1135 | no | `ui_keycap` → aliased to `ui_keycap_square_blank` (`AssetLibrary.cs:137`) | **DEAD** (no callers) and hardcodes a 16 px height with the raw `PixelFont`. |
| 20 | `Icon(b, key, box, tint?)` | 1144 | no | any | `SpriteFit` — the only aspect-preserving path in the kit. |
| 21 | `SpriteFit(b, t, bounds, tint?)` | 470 | no | any | Float scale, may be < 1; centred. |
| 22 | `Background` (492) / `Scrim` (503) / `Fill` (464) / `Sprite` (466) | | no | scene keys / `_pixel` | Not frames. |

### The nine-slice implementation, quoted verbatim (`UiKit.cs:777–805`)

```csharp
    private void NineSlice(SpriteBatch b, Texture2D tex, Rectangle r, int srcCornerMax, Color tint)
    {
        var scale = Scale;
        var srcC = Math.Min(srcCornerMax, Math.Min(tex.Width, tex.Height) / 2 - 1);
        var dstC = Math.Max(2, Math.Min(srcC / scale, Math.Min(r.Width, r.Height) / 2));
        srcC = Math.Min(dstC * scale, Math.Min(tex.Width, tex.Height) / 2 - 1);
        var midSrcW = tex.Width - 2 * srcC;
        var midSrcH = tex.Height - 2 * srcC;
        var innerW = r.Width - 2 * dstC;
        var innerH = r.Height - 2 * dstC;

        void S(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            if (dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return;
            b.Draw(tex, new Rectangle(dx, dy, dw, dh), new Rectangle(sx, sy, sw, sh), tint);
        }

        S(0, 0, srcC, srcC, r.X, r.Y, dstC, dstC);
        S(tex.Width - srcC, 0, srcC, srcC, r.Right - dstC, r.Y, dstC, dstC);
        S(0, tex.Height - srcC, srcC, srcC, r.X, r.Bottom - dstC, dstC, dstC);
        S(tex.Width - srcC, tex.Height - srcC, srcC, srcC, r.Right - dstC, r.Bottom - dstC, dstC, dstC);
        S(srcC, 0, midSrcW, srcC, r.X + dstC, r.Y, innerW, dstC);
        S(srcC, tex.Height - srcC, midSrcW, srcC, r.X + dstC, r.Bottom - dstC, innerW, dstC);
        S(0, srcC, srcC, midSrcH, r.X, r.Y + dstC, dstC, innerH);
        S(tex.Width - srcC, srcC, srcC, midSrcH, r.Right - dstC, r.Y + dstC, dstC, innerH);
        S(srcC, srcC, midSrcW, midSrcH, r.X + dstC, r.Y + dstC, innerW, innerH);
    }
```

### Four facts about that code the refactor must know

**(a) `Scale` is always 1, so `srcC / scale` is a no-op.** `UiKit.Scale` (`UiKit.cs:26`) defaults to 4, but every batch sets it to 1: `Game1.cs:4894` (`BeginCanvas`) is called as `BeginCanvas(1)` at 4958, 4983, 5035, 5444 and as `BeginCanvas(ScreenScale())` at 4988, where `ScreenScale() => 1` (`Game1.cs:4944`); `BeginOverlayCanvas` sets `_ui.Scale = 1` outright (`Game1.cs:4903`). **The 4× counter-scale described all over UiKit's doc-comments (lines 21–26, 512–517, 774–776) is dead machinery.** Consequence: `dstC == srcC == 40` for every panel, so corners are drawn 1:1 in authored pixels.

**(b) The corner slice is 40 src px (`PanelCorner`, line 740) and that does NOT cover `ui_panel_square`'s ornament.** Measured off the alpha+colour of the PNG itself (not the comments): `ui_panel_square`'s ornament reaches **51 src px** at the top-edge midpoint, **49** at the left, **51** down the TL diagonal. Anything between 40 and 51 px falls into the *edge bands*, which are stretched 3.5×–5.3×. For `ui_panel_vertical` (top 21, left 24, diagonal 34) and `ui_panel_medium` (top 20, left 21, diagonal 37) the 40 px corner does contain the ornament.

**(c) A dead constant contradicts the live one.** `UiKit.cs:507–509` says *"Corner MUST match the asset's bevel"* and declares `private const int Corner = 34;` — which is **never read anywhere**. The value actually used is `PanelCorner = 40` (line 740). Believe 40.

**(d) The frame-tier rule documented in the code is not implemented.** `UiKit.cs:544–548` states "the gold nine-slice (`PanelNine` with `ui_panel_modal_wide`) is for MODALS only", and `assets/art/idlexidle_ux_screen_guide_standard.md:76` repeats it as the PRIMARY tier. **No code path ever draws `ui_panel_modal_wide`.** Every modal in the game calls `_ui.Panel(...)`, which routes through `PanelArtKey` and can only ever return medium/square/vertical: `Game1.cs:4628` (welcome), `Game1.cs:5568` (settings), `Game1.cs:6685` (help), `HuntScreen.cs:2265` (run log — its own comment says "the gold nine-slice", which is false), `VaultScreen.cs:1257`/`1427`/`1476`, `MasteryScreen.cs:1621`.

### 2. The frame assets on disk: path, native pixel size, and which helper draws them. Then, for the GEAR screen EQUIPPED panel specifically, compute the destination rectangle at UI scale 100/125/150 and give the effective scale ratio (destination / native). The brief says decorative raster must stay near native (0.75x-1.25x) - report the real numbers.

### (a) Every frame/chrome asset on disk, with its live drawer

Sizes read from each PNG's IHDR. `AssetLibrary` keys are flat basenames (`AssetLibrary.cs:66`); all of these basenames are unique across `assets/`, so no dedup collision applies.

| Path | Native | Drawn by | Draw mode |
|---|---|---|---|
| `assets/art/UI/panels/ui_panel_medium.png` | **256×192** | `UiKit.PanelAt:646` when `aspect ≥ 1.30` | 9-slice, corner 40 |
| `assets/art/UI/panels/ui_panel_square.png` | **256×256** | `UiKit.PanelAt:646` when `0.82 ≤ aspect < 1.30` | 9-slice, corner 40 |
| `assets/art/UI/panels/ui_panel_vertical.png` | **192×256** | `UiKit.PanelAt:646` when `aspect < 0.82` | 9-slice, corner 40 |
| `assets/art/UI/panels/ui_panel_small.png` | **256×256** | `UiKit.Pill:844` | **whole-texture stretch** (deliberate, 841–843) |
| `assets/art/UI/panels/ui_panel_large.png` | **384×256** | **NOTHING** | — |
| `assets/art/UI/panels/ui_panel_modal_wide.png` | **384×224** | **NOTHING** (named only in the doc-comment `UiKit.cs:546`) | — |
| `assets/art/UI/buttons/ui_button_primary.png` | 256×96 | `Button:913`, `Field:1004` | whole stretch or `HSliceScaled` cap 44 |
| `assets/art/UI/buttons/ui_button_secondary.png` | 256×96 | `Button:913`, `Field:1004` | same |
| `assets/art/UI/buttons/ui_button_disabled.png` | 256×96 | `Button:911` | same |
| `assets/art/UI/buttons/ui_button_confirm.png` | 256×96 | **NOTHING** | — |
| `assets/art/UI/buttons/ui_button_danger.png` | 256×96 | **NOTHING** | — |
| `assets/art/UI/buttons/ui_button_icon_back/close/square.png` | 64×64 each | **NOTHING** | — |
| `assets/art/UI/buttons/ui_tab_active.png` | **256×96** | `Game1.cs:7057` | **whole stretch into a 180×98 nav tile** |
| `assets/art/UI/buttons/ui_tab_inactive.png` | 256×96 | **NOTHING** | — |
| `assets/art/UI/slots/ui_slot_empty.png` | **128×128** | `GearScreen.cs:797` | whole stretch into `SlotBox` |
| `assets/art/UI/slots/ui_slot_trinket_round.png` | 128×128 | `GearScreen.cs:796` | whole stretch |
| `assets/art/UI/slots/ui_slot_locked.png` | 128×128 | `UiKit.Icon`→`SpriteFit`, 13 sites | aspect-preserving fit |
| `assets/art/UI/slots/ui_slot_skill_hex.png` | 128×128 | `HuntScreen.cs:3370,3396` | whole stretch |
| `assets/art/UI/bars/ui_bar_*_frame/_fill.png` (12 files) | 256×64 each | `BarArt:1299–1305` (5-slice if wide), `Bar:822` (whole stretch) | mixed |
| `assets/art/UI/decor/ui_corner_{top,bottom}_{left,right}.png` | 64×64 each | **NOTHING** | — |
| `assets/art/UI/decor/ui_divider_long.png` | 256×32 | **NOTHING** | — |
| `assets/art/UI/decor/ui_divider_short.png` | 128×32 | **NOTHING** | — |
| `assets/art/UI/decor/ui_scrollbar_track.png` | 32×256 | **NOTHING** (`UiKit.ScrollBar:1115` draws `Fill` rectangles) | — |
| `assets/art/UI/decor/ui_scrollbar_handle.png` | 32×128 | **NOTHING** | — |
| `assets/art/UI/decor/ui_keycap_square_blank.png` | 64×64 | aliased to `ui_keycap` (`AssetLibrary.cs:137`) for `UiKit.KeyCap:1138` — **but KeyCap has no callers** | effectively dead |
| `assets/art/UI/decor/ui_keycap_space_blank.png` | 192×64 | **NOTHING** | — |
| `assets/art/UI/nodes/ui_node_{start,minor,notable,greater,spec,bridge}.png` | 192×192 each | `MasteryScreen.cs:1975–1982`, `TraitsScreen.cs:684–689` | — |
| `assets/art/UI/nodes/ui_node_mastery.png` | 320×160 | **NOTHING** (`MasteryKind.Mastery` maps to `ui_node_greater`, `MasteryScreen.cs:1979`) | — |
| `assets/art/UI/icons/ui_medallion_round.png` | 128×128 | `HuntScreen.cs:2750`, `MasteryScreen.cs:1526` | whole stretch |
| `assets/art/UI/icons/ui_medallion_hex.png` | 128×128 | `StyleAffinityDiagram.cs:194` | `Icon`→`SpriteFit` |
| `assets/art/UI/icons/icon_close.png` | 64×64 | `UiKit.CloseButton:1084` | `SpriteFit` |
| `assets/art/ItemsLoot/frames/ui_frame_rarity_{common,uncommon,rare,epic,legendary}.png` | **256×256** each | `ForgeScreen.DrawItemIcon:4033` | whole stretch |
| `assets/art/UI/terminals/art_terminal_{hoarder,reaper,titan,weaver}.png` | 256×256 | `TraitsScreen` terminal art | — |

**Gate hole worth naming:** `tools/check_asset_consumers.py` marks `ui_panel_large` and `ui_panel_modal_wide` as **"content list"** (verified by running `python tools/check_asset_consumers.py --list`) because a `.md`/`.json` under `assets/`/`design/` names them — so the orphan gate passes two frames that nothing draws. The unused buttons/decor/tabs are instead parked in `tools/asset_orphans_baseline.txt` lines 62–76 and 119.

### (b) The GEAR EQUIPPED panel, computed

The chain (`src/IdleXIdle.Game/GearScreen.cs`):
```
Top = 120, Margin = 24, BottomInset = 40          (162–163)
Gutter        = UiMetrics.Space(16)               (164)
ColumnsWidth  = PageRight(24) - 24 - Gutter*2     (173)
DetailWidth   = max(ColumnsWidth*31/100, UiMetrics.InspectorWidth(1920))   (177)
EquippedWidth = (ColumnsWidth - DetailWidth) * 42 / 69                     (178)
PanelHeight   = PageBottom(40) - 120 = 920        (179)
EquippedPanel = new(24, 120, EquippedWidth, 920)  (180)
```
`UiKit.Page` is **1920×1080 at every profile** — its `internal set` is never called (grep across `src/` finds no assignment; `Game1.ApplyUiScale:6975` only calls `UiMetrics.Apply`). `Space(16)` = 16/18/20; `Control(496)` = 496/620/744.

**Arithmetic, shown:**

| | 100 % | 125 % | 150 % |
|---|---|---|---|
| Gutter = `Space(16)` | 16 | 18 | 20 |
| ColumnsWidth = 1896−24−2·G | 1840 | 1836 | 1832 |
| 31 % share (int) | 1840·31/100 = **570** | 1836·31/100 = 569 | 1832·31/100 = 567 |
| `InspectorWidth(1920)` = min(Control(496), 768) | 496 | 620 | 744 |
| **DetailWidth** = max | **570** | **620** | **744** |
| ColumnsWidth − DetailWidth | 1270 | 1216 | 1088 |
| **EquippedWidth** = ·42/69 | 1270·42=53340; /69 = **773** | 1216·42=51072; /69 = **740** | 1088·42=45696; /69 = **662** |
| **EQUIPPED destination rect** | **(24, 120, 773, 920)** | **(24, 120, 740, 920)** | **(24, 120, 662, 920)** |
| aspect w/h | 0.8402 | 0.8043 | 0.7196 |
| `PanelArtKey` → | **`ui_panel_square` (256×256)** | `ui_panel_vertical` (192×256) | `ui_panel_vertical` (192×256) |

**⚠ The art texture changes between 100 % and 125 %.** At 100 % the aspect 0.8402 is ≥ 0.82 so the panel takes the SQUARE frame; at 125 %/150 % it takes the VERTICAL one. That also flips `FrameDrop` 28→0 and `PadX` 68→40, moving `ContentLeft` from 92 to 64 (`UiKit.cs:608`, `626–636`).

**⚠ The comment at `GearScreen.cs:166–169` is wrong.** It claims the 42 % share was chosen so the panel would drop below 0.82 and wear the vertical frame. It does not: 773/920 = 0.840. The comment at `GearScreen.cs:199–201` ("this panel wears the square frame") is the one that matches the code. **Believe: square at 100 %.**

### (c) Effective scale ratio, destination ÷ native — the real numbers

Menu screens draw through `BeginOverlayCanvas` (`Game1.cs:4901`), whose matrix scales authored coords by `BaseOverlayScale = (1920−180−20)/1920 = 0.895833` (`Game1.cs:6955`). Both figures given.

**Whole-texture ratio (what "stretched" means at panel level):**

| Profile | Dest (authored) | Dest (canvas px) | Native | Ratio authored | **Ratio on canvas** |
|---|---|---|---|---|---|
| 100 % | 773 × 920 | 692.5 × 824.2 | 256 × 256 | 3.020× / 3.594× | **2.705× / 3.219×** |
| 125 % | 740 × 920 | 662.9 × 824.2 | 192 × 256 | 3.854× / 3.594× | **3.453× / 3.219×** |
| 150 % | 662 × 920 | 593.0 × 824.2 | 192 × 256 | 3.448× / 3.594× | **3.089× / 3.219×** |

**Per-slice ratio (what the 9-slice actually does), with `Scale == 1`, `PanelCorner == 40`:**

| Profile | Corner (40 src) | Top/bottom band | Left/right band | Centre |
|---|---|---|---|---|
| 100 % | 40 authored → 35.8 canvas = **0.896×** ✅ | 176 src → 693 authored → 620.8 canvas = **3.527×** | 176 src → 840 authored → 752.5 canvas = **4.276×** | 3.527× / 4.276× |
| 125 % | 40 → 35.8 = **0.896×** ✅ | 112 src → 660 → 591.2 = **5.279×** | 176 src → 840 → 752.5 = **4.276×** | 5.279× / 4.276× |
| 150 % | 40 → 35.8 = **0.896×** ✅ | 112 src → 582 → 521.4 = **4.655×** | 176 src → 840 → 752.5 = **4.276×** | 4.655× / 4.276× |

**Verdict against the brief's 0.75×–1.25× budget (§73):**
- The **corners are in budget** (0.896×) at every profile — the nine-slice is doing its job there.
- The **edge bands are 3.5×–5.3× out of budget**, and at 100 % the square frame's crest/rail ornament runs 51/49 src px, i.e. **11 and 9 px past the 40 px corner slice**, so the most decorated part of the EQUIPPED frame is inside a band being stretched 3.53× horizontally and 4.28× vertically. That is the visible degradation the brief §74 names.
- The **whole-panel ratio is 2.7×–3.5×**, which is what §79's proposed diagnostic ("flag `3.1× decorative frame scale`") would print.

**Also inside EQUIPPED:** the eight slot boxes draw `ui_slot_empty`/`ui_slot_trinket_round` (128×128) as a whole stretch (`GearScreen.cs:797`) into `SlotBox` = **99 / 99 / 78** px → **0.773× / 0.773× / 0.609×** authored, **0.693× / 0.693× / 0.546×** on canvas. Two of three profiles are already under the 0.75× floor; 150 % is well under it.

### 3. The same ratio for every other large decorative frame you can find (item detail, forge, vault, traits inspector, modal).

All computed from the same replication of `UiMetrics`/`UiTypography`/`PanelArtKey` integer arithmetic. `corner` is always `40 src → 40 authored` because `UiKit.Scale == 1` everywhere, so the corner ratio is 1.000× authored / **0.896× on canvas for overlay screens** and **1.000× for chrome-space modals** (settings, help, welcome, run log — drawn at `BeginCanvas(1)`, no overlay matrix). The numbers that vary — and that break the budget — are the edge/centre bands.

### GEAR — ITEM DETAIL (`GearScreen.cs:182`, drawn `PanelQuiet` at 993)

| Profile | Rect | aspect | Art | Whole ratio (authored) | Top/bottom band | Side band |
|---|---|---|---|---|---|---|
| 100 % | (1326,120,**570×920**) | 0.6196 | `ui_panel_vertical` 192×256 | 2.969× / 3.594× | **4.375×** | **4.773×** |
| 125 % | (1276,120,**620×920**) | 0.6739 | vertical | 3.229× / 3.594× | **4.821×** | **4.773×** |
| 150 % | (1152,120,**744×920**) | 0.8087 | vertical *(0.0113 from flipping to square)* | 3.875× / 3.594× | **5.929×** | **4.773×** |

### GEAR — INVENTORY (`GearScreen.cs:181`)

**This panel has no frame art at all.** `GearScreen.cs:902` calls `_ui.Plate(b, panel)`. Rects: (813,120,497×920) / (782,120,476×920) / (706,120,426×920). Scale ratio: **n/a — nothing raster is drawn.** See Q4.

### FORGE (`ForgeScreen.cs:440`, `447`, `454`; drawn 1883 / 2231 / 2232)

| Panel | Profile | Rect | aspect | Art | Whole ratio | Top/bottom | Side |
|---|---|---|---|---|---|---|---|
| **ForgePanel** (`Panel`, the one PRIMARY) | 100 % | (1070,243,**810×797**) | 1.0163 | **`ui_panel_square` 256×256** | 3.16× / 3.11× | **4.15×** | **4.07×** |
| | 125 % | (1070,261,810×779) | 1.0398 | square | 3.16× / 3.04× | 4.15× | 3.97× |
| | 150 % | (1124,277,756×763) | 0.9908 | square | 2.95× / 2.98× | 3.84× | 3.88× |
| **ItemPanel** (`PanelQuiet`) | 100 % | (509,243,539×797) | 0.6763 | vertical 192×256 | 2.81× / 3.11× | 4.10× | 4.07× |
| | 125 % | (509,261,539×779) | 0.6919 | vertical | 2.81× / 3.04× | 4.10× | 3.97× |
| | 150 % | (563,277,539×763) | 0.7064 | vertical | 2.81× / 2.98× | 4.10× | 3.88× |
| **BagPanel** (`PanelQuiet`) | 100 % | (38,243,449×797) | 0.5634 | vertical | 2.34× / 3.11× | 3.29× | 4.07× |
| | 125 % | (38,261,449×779) | 0.5764 | vertical | 2.34× / 3.04× | 3.29× | 3.97× |
| | 150 % | (38,277,503×763) | 0.6592 | vertical | 2.62× / 2.98× | 3.78× | 3.88× |

Note FORGE's own comment (`ForgeScreen.cs:446–450`) correctly predicts the square frame here — and `ui_panel_square`'s 51 px crest again overflows the 40 px corner into a 4.15× band.

### VAULT — main surface (`VaultScreen.cs:343 PanelAt(rows)`, drawn `PanelQuiet` at 735)

**The worst frame in the game.**

| Profile | Rows | Rect | aspect | Art | Whole ratio | Top/bottom band | Side band |
|---|---|---|---|---|---|---|---|
| 100 % | 2 | (24,150,**1872×890**) | 2.1034 | `ui_panel_medium` 256×192 | **7.31× / 4.64×** | **10.18×** | **7.23×** |
| 125 % | 1 | (24,150,1872×689) | 2.7170 | medium | 7.31× / 3.59× | **10.18×** | 5.44× |
| 150 % | 1 | (24,150,1872×811) | 2.3083 | medium | 7.31× / 4.22× | **10.18×** | 6.53× |

A 176-source-pixel top band smeared across 1712 authored px. `ui_panel_medium`'s top ornament is only 20 src px deep, so it is a thin run rather than a gem cluster — but 10.18× is 8× outside the brief's budget.

### TRAITS — inspector (`TraitsScreen.cs:464 DetailPanel`, drawn `PanelQuiet` at 1853)

| Profile | Rect | aspect | Art | Whole ratio | Top/bottom | Side |
|---|---|---|---|---|---|---|
| 100 % | (1372,170,**496×900**) | 0.5511 | vertical 192×256 | 2.58× / 3.52× | 3.71× | **4.66×** |
| 125 % | (1248,200,620×870) | 0.7126 | vertical | 3.23× / 3.40× | 4.82× | 4.49× |
| 150 % | (1124,231,**744×839**) | 0.8868 | **`ui_panel_square` 256×256 — art flips** | 2.91× / 3.28× | 3.77× | 4.31× |

The Traits inspector is the second panel whose *texture changes with UI scale*. At 150 % it takes the square frame, gains `FrameDrop = 28` and `PadX = 68`, and the 51 px crest again overflows the corner.

### MODALS (drawn in chrome space — no overlay factor, so canvas ratio == authored ratio)

| Modal | Profile | Rect | aspect | Art | Whole ratio | Top/bottom | Side |
|---|---|---|---|---|---|---|---|
| **HUNT run log** `HuntScreen.cs:2317`, drawn 2265 | 100 % | (360,90,**1200×900**) | 1.3333 | `ui_panel_medium` | 4.69× / 4.69× | **6.36×** | **7.32×** |
| | 125 % | (285,34,1350×1012) | 1.3340 | medium | 5.27× / 5.27× | 7.22× | 8.32× |
| | 150 % | (210,37,1500×1005) | 1.4925 | medium | 5.86× / 5.23× | 8.07× | 8.26× |
| **SETTINGS** `Game1.cs:5342`, drawn 5568 (`width = Control(560)*2 + Space(44) + 74`) | 100 % | (341,100,**1238×880**) | 1.4068 | medium | 4.84× / 4.58× | 6.58× | 7.14× |
| | 125 % | (198,100,**1524×880**) | 1.7318 | medium | 5.95× / 4.58× | **8.20×** | 7.14× |
| | 150 % | (56,100,**1809×880**) | 2.0557 | medium | 7.07× / 4.58× | **9.82×** | 7.14× |
| **HELP** `Game1.cs:6679`, drawn 6685 (`HelpWidth = 1696`, fixed) | all | (112,92,**1696×840**) | 2.0190 | medium | 6.62× / 4.38× | **9.18×** | 6.79× |
| **WELCOME** `Game1.cs:4625`, drawn 4628 (`width = min(Control(720), …)`) | 100 % | 720 × h | ≥1.44 | medium | 2.81× | 3.64× | — |
| | 125 % | 900 × h | | medium | 3.52× | 4.66× | — |
| | 150 % | 1080 × h | | medium | 4.22× | 5.68× | — |
| **MASTERY spec** `MasteryScreen.cs:924` | — | width forced to `≥ ceil(h·1.30)+1` (942) *specifically so `PanelArtKey` returns medium* (947) | ≈1.30 | medium | ≈4.7× at `Space(960)`=960 wide | ≈6.4× | — |
| **VAULT trader stall** `VaultScreen.cs:1257` | 100 % | `min(Control(1320), 1840)` × ≤ ~724 | ~1.82 | medium | 5.16× | ~7.1× | — |
| **VAULT build card** `VaultScreen.cs:1476` | 100 % | 800×600 (`BuildCardW/H`, 1424) | 1.333 | medium | 3.13× / 3.13× | 4.15× | 4.79× |

### Other large PRIMARY surfaces not on the brief's list

| Surface | Profile | Rect | Art | Whole ratio | Top/bottom | Side |
|---|---|---|---|---|---|---|
| **MAP canvas** `MapScreen.cs:177`, drawn 534 | 100 % | (34,150,1330×876) | medium | 5.20× / 4.56× | **7.10×** | **7.11×** |
| | 125 % | (34,150,1203×876) | medium | 4.70× / 4.56× | 6.38× | 7.11× |
| | 150 % | (34,150,**1121×876**) | **square — art flips** | 4.38× / 3.42× | 5.91× | 4.52× |
| **BUILD loadout** `LoadoutScreen.cs:286`, drawn 968 | all | (38,150,520×870) | vertical | 2.71× / 3.40× | 3.93× | 4.49× |
| **BUILD inspector** `LoadoutScreen.cs:287`, drawn 1370 | all | (1384,150,496×870) | vertical | 2.58× / 3.40× | 3.71× | 4.49× |
| **MAP inspector** `MapScreen.cs:176`, drawn 796 | 100/125/150 | 496/620/700 × 876 | vertical (width capped at `ColumnH*80/100` *for exactly this reason*, 173) | 2.58–3.65× | 3.71–5.54× | 4.52× |

### Summary against the 0.75–1.25× budget

**Nothing decorative in this game is inside the budget except the nine-slice corners (0.896×) and the ~0.6–0.8× slot/rarity art.** Every ornate frame's edge and centre bands run **3.3× to 10.2×**. The three worst are VAULT main (10.18×), SETTINGS at 150 % (9.82×) and HELP (9.18×). Three surfaces silently swap texture with the profile: GEAR EQUIPPED (square→vertical at 125 %), TRAITS inspector (vertical→square at 150 %), MAP canvas (medium→square at 150 %). There is **no runtime or test diagnostic** for any of this: `tests/unit/IdleXIdle.Game.Tests/page_layout_test.cs` only asserts every static rect lies inside the page, and no test anywhere references `PanelArtKey` or `NineSlice`.

### 4. The GEAR inventory panel as it stands: how the header, the filter tabs, the grid and the footer are drawn, and exactly which parts use ad-hoc rectangles rather than the house components.

All of `GearScreen.DrawInventory` is `src/IdleXIdle.Game/GearScreen.cs:900–987`. **The whole panel is hand-drawn `Fill` rectangles. Not one house frame, tab, header or slot component is used.**

### The panel body — `Plate`, the QUIET tier

`GearScreen.cs:901–902`:
```csharp
var panel = InventoryPanel;
_ui.Plate(b, panel);
```
`Plate` (`UiKit.cs:569–581`) = one `Fill(UiInk.Plate)` + four 1 px `Fill(UiInk.Rule)` edges. Compare the neighbours: EQUIPPED is `_ui.Panel(b, panel)` (742, Tier 1 ornate) and ITEM DETAIL is `_ui.PanelQuiet(b, panel)` (993, Tier 2 ornate-tinted). **The middle column is two tiers below both.** The house guide (`assets/art/idlexidle_ux_screen_guide_standard.md:78`) itself says Plate is for "lists, grids, metadata rows … empty cells" and PanelQuiet is for "every panel that lives IN a screen". So the current call is off-tier by the project's own rule — this is exactly the "prototype panel between two polished surfaces" of brief §80.

Rects and paddings, computed: (813,120,497×920) / (782,120,476×920) / (706,120,426×920). `InvX = UiKit.ContentLeft(InventoryPanel)` (276) and `InvW = ContentRight − InvX` (277) — i.e. **the inventory takes its content inset from `UiKit.PadX`, an ORNATE-FRAME padding rule (`UiKit.cs:626–636`), even though it draws no frame.** `PadX` returns `PanelPadNarrow = 28` at all three profiles here (width < `WidePanelFrom = 512`), so `InvX/InvW` = 841/441, 810/420, 734/370.

### The header — bare text, no component

`GearScreen.cs:903–905`:
```csharp
_ui.TextBig(b, "INVENTORY", InvX, panel.Y + UiMetrics.Space(18), Slate, UiTypography.Secondary);
var total = Wearable().Count;
_ui.TextRightBig(b, $"{total} ITEM{(total == 1 ? "" : "S")}", InvX + InvW, panel.Y + UiMetrics.Space(18), Slate, UiTypography.Secondary);
```
- **AD-HOC.** Two loose strings at a hand-written `panel.Y + Space(18)`. It does **not** use `UiKit.TitleTop(panel)` (`UiKit.cs:611`), which every framed panel on this screen uses (EQUIPPED at 743, ITEM DETAIL at 996). So the header sits at a different baseline from its two neighbours.
- Both strings are `UiTypography.Secondary` (19/24/29 px) — **the smallest text rung on the screen**, while EQUIPPED's and ITEM DETAIL's titles are `UiTypography.PanelTitle` (28/35/42). Brief §82 ("Do not use tiny debug-like text", "same typography hierarchy as other production panels") is failed by construction.
- No rule, no divider, no header plate. `ui_divider_long` / `ui_divider_short` exist on disk and are never drawn anywhere.

### The filter tabs — four hand-drawn rectangles

`GearScreen.cs:907–922`:
```csharp
for (var i = 0; i < Tabs.Length; i++)
{
    var r = TabRect(i);
    var on = i == _tab;
    var hot = r.Contains(hit);
    var lift = UiMotion.Ease(UiMotion.KeyOf(r), hot ? 1f : 0f);
    var pressed = hot && UiKit.MouseHeld;
    _ui.Fill(b, r, on ? new Color(0x2C, 0x25, 0x44) : new Color(0x0E, 0x0B, 0x16) * 0.8f);
    if (lift > 0f && !on) _ui.Fill(b, r, new Color(0x1E, 0x18, 0x2C) * lift);
    if (pressed) _ui.Fill(b, r, Color.Black * 0.18f);
    Ring(b, r, on ? Gold : Color.Lerp(Dim, Slate, lift), on ? 2 : 1);
    _ui.TextCenterBig(b, Tabs[i], r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2 + (pressed ? 1 : 0),
                      on ? Gold : Color.Lerp(Slate, Bone, lift), UiTypography.Secondary);
}
```
- **FULLY AD-HOC.** Two literal colours (`0x2C2544`, `0x0E0B16`) declared inline, a third (`0x1E182C`) for hover, plus `Ring`, which is GearScreen's own private 4-`Fill` outline helper (`GearScreen.cs:1269–1275`) — **a duplicate of the outline `UiKit.Plate` already draws**.
- It uses **neither** `UiKit.Button` (`UiKit.cs:881`) **nor** `UiKit.Field` (998), and neither of the two tab textures on disk. `ui_tab_active.png` (256×96) has exactly one consumer in the game — the nav rail, `Game1.cs:7057`, where it is whole-stretched into a 180×98 tile (0.703× × 1.021×). **`ui_tab_inactive.png` has zero consumers** and sits in `tools/asset_orphans_baseline.txt:67`.
- States present: normal / hover / pressed / selected. **No disabled state** (brief §83 lists one as optional).
- Geometry (`GearScreen.cs:280–301`): `TabH = max(HitTargetMinimum, Pitch(Secondary) + Space(14))` = **40 / 50 / 60**; `TabsTop = InventoryPanel.Y + Space(18) + Pitch(Secondary) + Space(12)` = **174 / 185 / 195**. `TabRows` (283–295) is **1 or 2 depending on a runtime text measurement** of the widest label ("ACCESSORY") — the code's own comment says it stacks to two rows at 125 % and up. Computed `InvTop` (303): 226 (1 row) at 100 %, 308 (2 rows) at 125 %, 340 (2 rows) at 150 %.

### The grid — flat `Fill` cells, no slot art, no inner surface

`GearScreen.cs:924–968`. Per cell:
```csharp
var cell = InvCellRect(vis);
_ui.Fill(b, cell, CellBg);   // flat, no border: an empty cell is nothing to look at
…
if (lift > 0f && !sel) _ui.Fill(b, cell, CellHot * lift);
var drop = pressed ? 2 : 0;
_forge.DrawItemIcon(b, item, new Rectangle(cell.X + iconInset, cell.Y + iconInset + drop, cell.Width - iconInset*2, cell.Height - iconInset*2));
if (pressed) _ui.Fill(b, Shrink(cell, 5), Color.Black * 0.18f);
_ui.Fill(b, new Rectangle(cell.X, cell.Y, 5, cell.Height), RarityColor(item.Rarity));   // rarity owns the left edge
…
if (better) Ring(b, Shrink(cell, 3), new Color(0x6E, 0xC8, 0x7A, 0x9E), 1);
if (sel) { Ring(b, cell, Gold, 3); Ring(b, Shrink(cell, 3), new Color(0x14,0x10,0x1A,0x88), 1); }
else if (lift > 0f) Ring(b, cell, Slate * lift, 2);
```
- **FULLY AD-HOC.** `CellBg = 0x14111C` and `CellHot = 0x2C2544` are private literals at `GearScreen.cs:73–74`. Every state is a `Fill` or a `Ring`.
- **No grid surface at all** — no darker inner panel, no separators, no slot recesses (brief §84 asks for all three). The cells sit straight on the same `UiInk.Plate` the panel is.
- **The slot art is not used here.** `ui_slot_empty.png` (128×128) is drawn *only* on the EQUIPPED paper doll (`GearScreen.cs:796–797`), never in the bag.
- Item states, against brief §85: normal ✅ (`CellBg`), hover ✅ (`CellHot` lift + `Ring`), selected ✅ (gold `Ring` 3 px), rarity ✅ (5 px left bar + the 256×256 rarity frame inside `DrawItemIcon`), locked ✅ (veil `0x0A0810B4` + the hand-drawn `Lock` at `GearScreen.cs:1318–1331`), "better than worn" ✅ (green hairline). **`equipped` is NOT shown at all** — a worn piece in the bag list has no badge; and **`favorite` does not exist** (`GearScreen.cs:37` states outright: "There is no lock system, so there is no LOCK button"). **`Source` is not shown on the cell** either — `DrawSourceGem` is inside `DrawItemIcon`, but there is no cell-level badge. Brief §85's "Do not communicate all of these through border color alone" is currently violated for hover/selected/better, which are three differently-coloured `Ring`s.
- Geometry (`GearScreen.cs:275, 305–316`): `InvGap = Space(8)` = 8/9/10; `InvCols = 4` while `(InvW − InvGap·3)/4 ≥ Control(64)`, else 3 → **4 / 4 / 3**; `InvCell` = **104 / 98 / 116**; `InvRows` = as many as fit → **6 / 6 / 4–5**.
- The icon box handed to `DrawItemIcon` is `InvCell − 2·Space(6)` = **92 / 84 / 100** px, into which `ui_frame_rarity_*.png` (**256×256**) is whole-stretched (`ForgeScreen.cs:4033–4034`) → **0.359× / 0.328× / 0.391×** authored, **0.322× / 0.294× / 0.350×** on canvas. Well *under* the 0.75× floor in the other direction (brief §111: "test at actual game size").
- Scrollbar: `UiKit.ScrollBar` (969–971) — the one house component the inventory does use, and it too is `Fill`-drawn with no art (`ui_scrollbar_track/handle` unused).

### The footer — two loose strings

`GearScreen.cs:982–986`:
```csharp
var fy = InvFooterTop;
_ui.TextBig(b, SortedLabel, InvX, fy, Slate, UiTypography.Secondary);
if (InvFooterStacked) fy += UiTypography.Pitch(UiTypography.Secondary);
_ui.TextRightBig(b, MoreLabel, InvX + InvW, fy, Slate, UiTypography.Secondary);
```
- **AD-HOC.** `SortedLabel = "SORTED BY RARITY"`, `MoreLabel = "RIGHT-CLICK FOR MORE"` (307). No plate, no rule, no separator above them. `InvFooterTop = InventoryPanel.Bottom − Space(16) − Pitch(Secondary)·(stacked ? 2 : 1)` (311–313). Consistent `Secondary` typography ✅ (§86's one satisfied requirement), but they are literally "floating" against the plate.

### The empty state

`GearScreen.cs:973–980` — `UiTypography.Headline` line + wrapped `Body` explanation, no art. This is the *one* place in the panel using a rung above Secondary.

### Tally of ad-hoc drawing in `DrawInventory`

| Element | House component used | Ad-hoc |
|---|---|---|
| Panel body | `UiKit.Plate` (QUIET tier — off-tier vs its neighbours) | — |
| Header "INVENTORY" / "N ITEMS" | none | 2 raw `TextBig` at `panel.Y + Space(18)`, bypassing `UiKit.TitleTop` |
| 4 filter tabs | none | 3 inline `Fill` colours + private `Ring` per tab (917–920) |
| Grid cells | none | `Fill(CellBg)`, `Fill(CellHot)`, 5 px rarity `Fill`, 3 different `Ring`s (935–962) |
| Lock badge | none | private `Lock()` drawn from 6 `Fill`s (1318–1331) |
| Item icon | `ForgeScreen.DrawItemIcon` ✅ | rarity frame whole-stretched 0.29–0.39× |
| Scrollbar | `UiKit.ScrollBar` ✅ | (itself art-free) |
| Footer | none | 2 raw `TextBig`/`TextRightBig` |

**Private helpers duplicating UiKit work, all in GearScreen:** `Ring` (1269), `Shrink` (1265), `Disc` (1283), `DiscRing` (1296), `Lock` (1318). The file's own comment at line 76 admits it: *"a shared token belongs in UiKit, which this pass may not edit"*, and again at 1277: *"Shared shapes belong in UiKit … the host pass lifts these."* They were never lifted.

### 5. What UI art already exists that a polished SECONDARY panel could reuse (thin frames, headers, tab art), with paths.

Everything below is already committed and loaded by `AssetLibrary` at boot (it scans `assets/art/**` recursively, `AssetLibrary.cs:34–37`), so any of it can be drawn today with no pipeline work.

### Tier A — shipped, currently drawn by NOTHING (immediately reusable, zero risk of changing an existing screen)

| Path | Native | Why it fits a polished secondary panel | Current status |
|---|---|---|---|
| `assets/art/UI/panels/ui_panel_large.png` | **384×256** | An ornate frame with a **thin** border — measured ornament depth **top 21 / bottom 19 / left 22 / right 21 src px, diagonal 46** — the thinnest side rails of any panel asset except `medium`. Its larger 384 px width also means less horizontal stretch than the 256 px assets at the same destination. | **Zero consumers.** Passes the orphan gate only because a `.md` names it ("content list"). |
| `assets/art/UI/panels/ui_panel_modal_wide.png` | **384×224** | The frame the code's *own* documented tier system says is the modal frame (`UiKit.cs:546`, UX guide line 76). Ornament depth **top 19 / bottom 16 / left 23 / right 23, diagonal 37** — also thin. | **Zero consumers.** `PanelNine` — the only helper that can draw it — is dead. |
| `assets/art/UI/buttons/ui_tab_inactive.png` | **256×96** | The missing half of the tab pair. Ornament: **top 13 / bottom 11 / left 75 / right 74** — heavy ends, so it wants `HSliceScaled` with a cap around 75–96, not a whole stretch. | **Zero consumers**; `tools/asset_orphans_baseline.txt:67`. |
| `assets/art/UI/decor/ui_divider_long.png` | **256×32** | A header rule / footer separator — exactly what the inventory header and footer lack. Best drawn 3-sliced or tiled horizontally at native height (32 → ~28.7 canvas px). | **Zero consumers**; baseline line 72. |
| `assets/art/UI/decor/ui_divider_short.png` | **128×32** | A shorter rule for the footer or between grid sections. | **Zero consumers**; baseline line 73. |
| `assets/art/UI/decor/ui_corner_top_left.png` | 64×64 | **A corner-only ornament kit.** These are the raw material for a *thin bronze edge* built as four native-size corners plus 1 px rules — the composition brief §78 asks for ("native-size composition"), with zero stretch. | **Zero consumers**; baseline 70. |
| `assets/art/UI/decor/ui_corner_top_right.png` | 64×64 | same | baseline 71 |
| `assets/art/UI/decor/ui_corner_bottom_left.png` | 64×64 | same | baseline 68 |
| `assets/art/UI/decor/ui_corner_bottom_right.png` | 64×64 | same | baseline 69 |
| `assets/art/UI/decor/ui_scrollbar_track.png` | **32×256** | Would dress `UiKit.ScrollBar` (`UiKit.cs:1115`), which today draws bare `Fill` rectangles. Vertical asset, correct orientation for a vertical bar. | **Zero consumers**; baseline 76. |
| `assets/art/UI/decor/ui_scrollbar_handle.png` | **32×128** | The matching thumb. | **Zero consumers**; baseline 75. |
| `assets/art/UI/buttons/ui_button_confirm.png` | 256×96 | A fourth button state (green/confirm) if tabs want a distinct selected art. | **Zero consumers**; baseline 62. |
| `assets/art/UI/buttons/ui_button_danger.png` | 256×96 | A destructive-action variant. | **Zero consumers**; baseline 63. |
| `assets/art/UI/buttons/ui_button_icon_square.png` | 64×64 | A square icon-button chassis — a sort/filter chip in a header row. | **Zero consumers**; baseline 67. |
| `assets/art/UI/buttons/ui_button_icon_back.png` / `ui_button_icon_close.png` | 64×64 each | Icon-button chassis. | **Zero consumers**; baseline 64–65. |
| `assets/art/UI/decor/ui_keycap_space_blank.png` | 192×64 | A wide chip/pill blank — could dress a footer hint. | **Zero consumers**; baseline 74. |
| `assets/art/UI/nodes/ui_node_mastery.png` | **320×160** | The only **landscape** ornament plate in the tree (2:1). A header cartouche shape. | **Zero consumers**; baseline 119. |

### Tier B — shipped and in use, reusable in a second place

| Path | Native | Current consumer | Note for reuse |
|---|---|---|---|
| `assets/art/UI/buttons/ui_tab_active.png` | 256×96 | `Game1.cs:7057` only, whole-stretched into a 180×98 nav tile | Free to reuse for a real tab row; a 4-up tab at 100 % is ~104×40, so it wants `HSliceScaled` (cap ~44, as `Button` uses) rather than a whole stretch. |
| `assets/art/UI/panels/ui_panel_vertical.png` | 192×256 | `PanelAt` when aspect < 0.82 | Already the frame ITEM DETAIL wears; the inventory column's aspect (0.54/0.52/0.46) would select it automatically the moment `Plate` became `PanelQuiet`. |
| `assets/art/UI/panels/ui_panel_small.png` | 256×256 | `UiKit.Pill:844`, whole stretch | A small chip/badge plate. Ornament depth top/bottom 38, left/right 36 (with a 14–17 px transparent margin) — too heavy for a tab. |
| `assets/art/UI/slots/ui_slot_empty.png` | 128×128 | `GearScreen.cs:797` (paper doll only) | **The obvious source of the "subtle slot recess" brief §84 asks for** — it is already the game's slot vocabulary, and the bag simply does not draw it. At `InvCell` 104/98/116 it lands at **0.81× / 0.77× / 0.91×** authored (0.73/0.69/0.81 on canvas) — the only large decorative asset in the audit that is *inside* the 0.75–1.25× budget at two of three profiles. |
| `assets/art/UI/slots/ui_slot_trinket_round.png` | 128×128 | `GearScreen.cs:796` | Round variant of the same. |
| `assets/art/UI/slots/ui_slot_locked.png` | 128×128 | 13 sites via `UiKit.Icon`→`SpriteFit` | Already the house padlock; would replace `GearScreen.Lock` (1318), which draws one from six `Fill`s. |
| `assets/art/UI/icons/icon_close.png` | 64×64 | `UiKit.CloseButton:1084` | House close icon at `UiKit.CloseRect` (1074). |
| `assets/art/UI/icons/ui_medallion_round.png` / `ui_medallion_hex.png` | 128×128 each | `HuntScreen:2750`, `MasteryScreen:1526` / `StyleAffinityDiagram:194` | Badge chassis for an item-state badge (equipped / favourite) on a grid cell. |
| `assets/art/ItemsLoot/frames/ui_frame_rarity_*.png` | 256×256 ×5 | `ForgeScreen.DrawItemIcon:4033` | Already carry the rarity encoding brief §85 wants off the border colour. |
| `assets/art/UI/bars/ui_bar_progress_frame.png` + `_fill.png` | 256×64 | `UiKit.BarArt` | A framed capacity readout for an inventory header ("14 / 60"). |

### What does NOT exist (so §88's PixelLab route is real)

- **No thin/secondary panel frame family.** There is no `PanelFrameSmall/Medium/Grand` split (brief §76); the three live frames are selected by *aspect*, not by weight, so a "quiet" surface can only be the same ornate art tinted `0x585262` (`UiKit.cs:556`).
- **No header/cartouche asset** of any kind. Every panel title in the game is bare text at `UiKit.TitleTop`.
- **No inner-surface / grid-recess plate** — nothing between `Plate`'s flat fill and the ornate frames.
- **No disabled tab art** — `ui_tab_active` and `ui_tab_inactive` are the whole set.
- **No per-size frame variants.** Every panel asset is one size, 192–384 px wide, stretched 2.3×–10.2× (see Q3).

**Files this area will change**

- src/IdleXIdle.Game/UiKit.cs — the whole frame system lives here: PanelArtKey:589 (aspect→texture), PanelAt:644, NineSlice:777 (the only 9-slice), PanelCorner:740 (=40, too small for ui_panel_square's 51px crest), dead Corner:509, dead PanelNine:771, dead HSlice:1097, dead KeyCap:1135, art-free ScrollBar:1115 and HoverTip:1245, Plate:569 (the tier GEAR's inventory sits in). Also the stale `Scale = 4` field:26 that is always 1 at runtime.
- src/IdleXIdle.Game/GearScreen.cs — DrawInventory:900–987 is entirely ad-hoc Fill/Ring; layout block 155–345 (EquippedWidth:178 lands on the square frame at 100% contrary to its own comment at 166–169; InvX/InvW:276–277 take ornate-frame padding for an unframed plate; TabRect:297, InvCellRect:315); private duplicates Ring:1269, Shrink:1265, Disc:1283, DiscRing:1296, Lock:1318 that the file's comments (76, 1277) say belong in UiKit.
- src/IdleXIdle.Game/UiTypography.cs — PanelCorner-adjacent grid: SquareFrameDrop:248 (=28, only applied to the square frame), PanelPadX:280, PanelPadNarrow:287, WidePanelFrom:290. Any new frame family needs its own drop/pad entries here.
- src/IdleXIdle.Game/UiMetrics.cs — the density profile that moves every panel width (InspectorWidth:96 is what pushes GEAR's EQUIPPED across the 0.82 aspect line between 100% and 125%).
- src/IdleXIdle.Game/Game1.cs — BeginCanvas:4892 / BeginOverlayCanvas:4901 set UiKit.Scale = 1 (making NineSlice's /scale a no-op) and BaseOverlayScale:6955 = 0.8958 is the extra factor on every menu-screen frame; the modals at 4628, 5568, 6685 and the nav tab at 7057 are frame consumers.
- src/IdleXIdle.Game/VaultScreen.cs — PanelAt:343 produces the worst frame in the game (1872 wide from 256px art, 10.18x edge stretch) and 1257/1427/1476 are modal Panel calls.
- src/IdleXIdle.Game/ForgeScreen.cs — ForgePanel:454 (square frame, 4.15x bands), ItemPanel:447, BagPanel:440; DrawItemIcon:4031 whole-stretches the 256x256 rarity frames down to 0.29–0.39x in inventory cells.
- src/IdleXIdle.Game/TraitsScreen.cs — DetailPanel:464 is the traits inspector; it silently swaps from ui_panel_vertical to ui_panel_square at 150%.
- src/IdleXIdle.Game/MapScreen.cs — MapCanvas:177 flips to the square frame at 150%; InspectorW:173 already carries a hand-written cap to dodge the same trap and documents why.
- src/IdleXIdle.Game/MasteryScreen.cs — SpecWidth:942/MediumFrameAspect:947 hard-codes the 1.30 aspect threshold to force a specific frame; this breaks if PanelArtKey's thresholds move.
- src/IdleXIdle.Game/HuntScreen.cs — LogPanel:2317 modal (comment at 2265 claims a gold nine-slice that does not exist); ui_slot_skill_hex draws at 3370/3396.
- src/IdleXIdle.Game/LoadoutScreen.cs — LoadoutPanel:286 / InspectorPanel:287 are two more large ornate surfaces on the same code path.
- src/IdleXIdle.Game/AssetLibrary.cs — flat-basename keying:66 and the alias table (ui_keycap → ui_keycap_square_blank at :137); any new frame family lands here and must not collide.
- assets/art/UI/panels/ — ui_panel_square.png (256x256, 51px crest), ui_panel_vertical.png (192x256), ui_panel_medium.png (256x192) are the three live frames; ui_panel_large.png (384x256) and ui_panel_modal_wide.png (384x224) are on disk with zero consumers.
- assets/art/UI/buttons/ui_tab_active.png and ui_tab_inactive.png (256x96) — the tab art the GEAR filter row does not use; inactive has no consumer at all.
- assets/art/UI/decor/ — ui_divider_long/short, the four ui_corner_*, ui_scrollbar_track/handle: a complete thin-chrome kit on disk that nothing draws.
- tools/asset_orphans_baseline.txt — lines 62–76 and 119 park the unused UI art; any asset deleted or newly consumed by the refactor changes this file.
- tools/check_asset_consumers.py — the orphan gate; its 'content list' rule is what lets ui_panel_large and ui_panel_modal_wide pass with no runtime draw.
- tests/unit/IdleXIdle.Game.Tests/page_layout_test.cs — the only layout gate; it checks rects stay in the page and nothing about frame art, so brief §107's frame-quality test has no existing home to extend.
- assets/art/idlexidle_ux_screen_guide_standard.md:76 — the documented three-tier table naming PanelNine/ui_panel_modal_wide as PRIMARY; it must be corrected or made true.

**Risks**

- THE FRAME A PANEL WEARS IS AN EMERGENT PROPERTY OF ITS ASPECT RATIO. `UiKit.PanelArtKey:589` picks the texture from `width/height` against 1.30 and 0.82. Changing any panel's width by a few pixels silently swaps its art AND its content geometry, because `FrameDrop:608` (28 px) and `PadX:626` (68 vs 40) key off the same test. GEAR EQUIPPED is at 0.8402 at 100% — 0.02 from flipping. TRAITS inspector is at 0.8868 at 150%. ITEM DETAIL is at 0.8087 at 150%, 0.011 from flipping. MAP canvas is at 1.2797 at 150%, 0.02 from flipping. A refactor that touches column shares will move panels across these lines without any test noticing.
- THREE SCREENS HAVE ALREADY HARD-CODED THE THRESHOLDS AS LITERALS. `MasteryScreen.cs:947` (`MediumFrameAspect = 1.30f`) forces its modal width to guarantee the medium frame; `MapScreen.cs:173` caps `InspectorW` at `ColumnH*80/100` to stay under 0.82; `GearScreen.cs:166–169` documents a share chosen for the same reason (and gets it wrong). If PanelArtKey's thresholds or the asset set change, those three compensations become active bugs.
- `UiKit.Scale` IS DEAD AND `NineSlice` DEPENDS ON IT. The field defaults to 4 (`UiKit.cs:26`) but every batch sets 1 (`Game1.cs:4894`, `4903`, `ScreenScale() => 1` at `4944`). `NineSlice:779–781` divides the source corner by it. Anyone who 'restores' the 4× path — or writes a new screen that calls `BeginCanvas(4)` — will quarter every corner to 10 px and blur the whole game's chrome. The doc-comments at 21–26, 512–517 and 774–776 all still describe the 4× model as live.
- `PanelCorner = 40` IS TOO SMALL FOR ONE OF THE THREE LIVE ASSETS. Measured off the PNGs: ui_panel_square's ornament reaches 51 src px (top), 49 (side), 51 (diagonal); vertical reaches 21/24/34 and medium 20/21/37. Raising the corner to cover the square frame would change `PanelInner:750` and, through it, the layout of every screen that insets content by it; lowering it, or leaving it, keeps the square frame's crest inside a 3.5–4.3× stretched band. `UiTypography.SquareFrameDrop = 28` and `PanelPadX + 28 = 68` are the hand-tuned compensations already built on the current value.
- THE DOCUMENTED TIER SYSTEM IS FICTION AND SOMETHING WILL BREAK WHEN IT IS MADE TRUE. `UiKit.cs:544–548` and `assets/art/idlexidle_ux_screen_guide_standard.md:76` both say the gold nine-slice with `ui_panel_modal_wide` is the modal/PRIMARY tier. `PanelNine` has zero callers and `ui_panel_modal_wide` is never drawn. Every modal today calls `_ui.Panel`, whose `gold:true` only tints. Wiring PanelNine to the eight modal sites (Game1 4628/5568/6685, HuntScreen 2265, VaultScreen 1257/1427/1476, MasteryScreen 1621) changes eight screens' silhouettes at once, and modal geometry is currently tuned to the medium/square frames' corner and drop.
- GEAR'S INVENTORY TAKES ORNATE-FRAME PADDING WHILE DRAWING NO FRAME. `InvX = UiKit.ContentLeft(InventoryPanel)` and `InvW = ContentRight − InvX` (`GearScreen.cs:276–277`) run through `UiKit.PadX`, which returns 28 today. Promoting the panel from `Plate` to `PanelQuiet` will not change that number (the width stays under `WidePanelFrom = 512` and the aspect stays under 0.82) — but the frame will then eat ~24 px of border on each side INSIDE that inset, so the grid must be re-derived or the four columns will sit on the filigree. `InvCols` is already only one integer step from dropping to 3 at 125% (`(420−27)/4 = 98 ≥ Control(64) = 80` — margin of 18 px).
- THE INVENTORY'S EVERY VISUAL STATE IS A LITERAL COLOUR IN GearScreen. `CellBg 0x14111C` / `CellHot 0x2C2544` (73–74), the tab fills `0x2C2544` / `0x0E0B16` / `0x1E182C` (917–918), the lock veil `0x0A0810B4` (954), the 'better' hairline `0x6EC87A9E` (961). None is in `UiInk`. Replacing them with components must preserve six distinguishable states (normal/hover/pressed/selected/locked/better) that currently have no token names to move.
- THE MOTION KEYS ARE THE RECTANGLES THEMSELVES. `UiMotion.KeyOf(r)` is called on the live rect for every tab (911) and every cell (943). If a refactor changes tab or cell geometry per frame (a hover grow, a reflow), the ease keys change identity and hover animations reset or leak. `tests/unit/IdleXIdle.Game.Tests/ui_motion_rest_test.cs` and `gear_feedback_test.cs` guard the feedback layer but not the key stability.
- THE ORPHAN GATE WILL NOT CATCH A NEW UNUSED FRAME. `tools/check_asset_consumers.py` treats an asset as consumed if any `.md`/`.json` under `assets/` or `design/` merely names it ('content list'), which is exactly how `ui_panel_large` and `ui_panel_modal_wide` pass today with zero draws. Generating a new frame family via PixelLab and documenting it before wiring it will produce green gates and dead art (brief §112).
- THERE IS NO FRAME-QUALITY TEST TO EXTEND AND NO RUNTIME DIAGNOSTIC. `page_layout_test.cs` asserts only that static rects lie inside the page; nothing in `tests/` or `tools/` references `PanelArtKey`, `NineSlice`, texture native size or destination/native ratio. Brief §79 and §107 both start from zero. `tools/check_all.sh` runs ten text gates, none of which look at raster scale.
- GEAR IS NOT THE ONLY OFFENDER AND FIXING ONLY GEAR WILL MAKE THE REST LOOK WORSE. VAULT's main surface stretches a 256×192 asset's top band 10.18× across 1872 px at every profile; SETTINGS at 150% is 9.82×; HELP is 9.18× and its `HelpWidth = 1696` is a fixed literal that never follows the profile. If the EQUIPPED frame is replaced with correctly-sized art, these three become the visibly worst frames in the game.
- EQUIPPED'S SLOT ART AND THE BAG'S RARITY FRAMES ARE ALREADY UNDER-SCALE, NOT OVER. ui_slot_empty (128×128) draws at 0.69–0.55× on canvas and the 256×256 rarity frames draw at 0.29–0.35×. Regenerating chrome 'larger for quality' without checking these will add VRAM and mip cost for detail that is already being thrown away at 150%, which is the other half of brief §111.
