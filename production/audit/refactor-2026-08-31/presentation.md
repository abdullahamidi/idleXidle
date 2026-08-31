# Presentation integration audit — 2026-08-31

**Area:** how the Game project (`src/ResonanceHunter.Game`) observes the Core simulation, and every Core↔presentation coupling.
**Branch audited:** `feat/hunter-cutout-rig` at `ae30f2a`. Read-only; every claim below carries a file:line that was actually read, and every "dead" claim carries the grep that was run.

Files read in full: `SoloExpeditionScreen.cs` (3804 lines), `VfxPlayer.cs`, `AssetLibrary.cs`, `PlayerLoadout.cs`, `Core/Animation/Rig.cs`, `Core/Animation/Clip.cs`, `Core/Presentation/CanvasFit.cs`, `Expeditions/WaveReplay.cs`, `WaveModel.cs`, `RunLog.cs`, `RunReport.cs`, `Builds/SoloExpedition.cs`, `tests/unit/.../Presentation/hunt_screen_feedback_test.cs`, `tools/check_asset_keys.py`, `tools/check_nav_gates.py`. Read in part: `Game1.cs`, `SoloBattle.cs`, `SkillCatalogue.cs`, `Build.cs`, `BuildComposer.cs`, `FormBehaviour.cs`, `Character.cs`, `UiKit.cs`, `DisplaySettings.cs`, `SoundBank.cs`, ADR-001/002, the three GDDs named in the brief, `design/art/arena-art-contract.md`.

---

## 1. Headline

The Core/Game split is honoured at the type level — `grep -rn "Microsoft.Xna" src/ResonanceHunter.Core --include=*.cs` returns one hit and it is a doc comment (`Rig.cs:7`). The timing contract is the right one: a wave is resolved **instantly** by `SoloExpedition.PushWave` (`SoloExpedition.cs:255-394`) stepping `SoloBattle.ResolveWave` in fixed 100 ms ticks (`SoloBattle.cs:1198`, `WaveModel.cs:203`), and the screen **replays** the resulting `BattleEvent` stream against a real-time playhead (`SoloExpeditionScreen.cs:1140,1176`). Nothing in Core waits on an animation; `BusyUntilMs` no longer exists anywhere (`grep -rn BusyUntil src tests` → 0 hits). Frame-rate independence holds by construction.

But the presentation is keyed on the **legacy skill model**. Every `Skill` event carries `(int)Source` in `Slot` and `(int)Form` in `Amount` (`SoloBattle.cs:1558`, `WaveModel.cs:286-306`); the screen turns that back into a `Form` (`SoloExpeditionScreen.cs:1254`) and switches on it for the callout text (`:682-690`), the VFX placement (`:1375-1407`), the champion clip name (`:3277-3280`) and the rail's cooldown arithmetic (`:2926,2963,2972,3039-3049`). `SkillDef.ClipKey` is declared (`SkillCatalogue.cs:166`) and asserted non-empty by tests but **never read by the Game** (`grep -rn ClipKey src/ResonanceHunter.Game` → 0 hits). Source changes only a tint; variation and reinforcement change nothing visible.

Two liveness defects were confirmed on the UI→sim edge. (1) **The hunt never sees a chosen variation or reinforcement**: `ComposeBuild` calls `Loadout.ToBuild(Tree, Mastery, Character)` without `Progress` (`SoloExpeditionScreen.cs:775`), so `BuildComposer.cs:184` resolves `variation = null` and the whole STYLE→SKILL→VARIATION→REINFORCEMENT layer runs at its base line in every real fight — while the WeaveScreen lets the player buy them (`WeaveScreen.cs:792,810`) and the liveness tests compose with `progress:` directly (`variation_liveness_test.cs:92-94`), which is exactly this codebase's dominant bug species. (2) Two passive Field skills draw **no effect at all**: `FxFor` resolves PRESS→`fx_press` and WILT→`fx_wilt` (`:3350-3359`), no per-champion strip exists for either, and the alias table has no `fx_press`/`fx_wilt` entry (`AssetLibrary.cs:177-183`), so `VfxPlayer.Hold` returns silently (`VfxPlayer.cs:221`). `tools/check_asset_keys.py` cannot see this because the key is built at runtime (its own docstring, `:17-20`).

Core also hosts presentation that nothing runs: `Core/Animation/Rig.cs` + `Clip.cs` have **zero runtime consumers** (only `RigTests`/`ClipTests`; the `using ResonanceHunter.Core.Animation;` at `SoloExpeditionScreen.cs:8` binds nothing), the champion was moved to sprite strips (`:3559-3577`), yet ADR-002 still reads *Accepted — adopted for the player champion*. `FormBehaviour.CastClipMs` (`FormBehaviour.cs:194`) has zero consumers and cites a `SoloBattle.CastGapFor` that does not exist; `ClipShareOfBeat`/`SkillClipShareOfBeat` (`:200,:223`) are presentation constants read only by the screen.

---

## 2. Q1 — Timing contract

### 2.1 How the sim advances
| Step | Evidence |
|---|---|
| The host calls `_expedition.Update(gameTime, hunter, enemyHp, enemyDmg)` once per frame | `Game1.cs:3605-3615` |
| The screen accumulates real seconds (`dt = ElapsedGameTime.TotalSeconds`) | `SoloExpeditionScreen.cs:705` |
| At a wave boundary it calls `_run.PushWave()` **once**; the whole wave resolves synchronously | `:883`; `SoloExpedition.cs:255-394` |
| `ResolveWave` steps `for (ms = TickMs; ms <= TickCeilingMs; ms += TickMs)` — fixed 100 ms, hard ceiling 120 s | `SoloBattle.cs:1198`; `WaveModel.cs:202-203` |
| Champion actions land on **the beat** (`beatLen = BeatFor(rate, tuning.BeatMs)`, `nextBeat`), cooldowns are counted in beats (`ReadyAtBeat`) | `SoloBattle.cs:643-644, 1472-1473, 1775` |
| The screen then replays: `_replay = new WaveReplay(_run.LastWaveEvents, …)`; `_playheadMs += dt * 1000f * _speedMul`; `_replay.Advance(_playheadMs)` | `:904, :1140, :1176` |
| `PlaybackSpeed = 1.0f` is a `const`; the x1/x2/x4/x8 control was removed | `:68-86` |

MonoGame's default `IsFixedTimeStep = true` / 60 Hz is not overridden (`grep -n "IsFixedTimeStep\|TargetElapsedTime" Game1.cs` → none). Even so the replay advances by real elapsed time and every strip picks its frame as `(int)(seconds * fps)` (`UiKit.cs:307`), so a dropped frame changes nothing the sim or the replay computes. **Frame-rate independent: yes. Fast-forwardable: yes** — `DevRunToDeath` pushes waves in a tight loop with no presentation at all (`:3745`).

### 2.2 Does anything in Core wait for an animation?
No. `grep -rn "BusyUntil" src tests --include=*.cs` → **0 hits** (the memory note about `BusyUntilMs` is stale; the lock is the beat). `grep -rn -E "\bClip\b|ClipKey|\bFx\b|FxKey|Anim|\bRig\b" src/ResonanceHunter.Core` outside `Animation/` returns only:
- `SkillCatalogue.cs:166-167` — `ClipKey`, `FxKey` fields (data, not timing);
- `Character.cs:160` — `StripKey(clip)` name builder (string, not timing).

### 2.3 Where a clip/VFX duration lives in Core (misplacement, not a sim violation)
| Symbol | Where | Consumers | Verdict |
|---|---|---|---|
| `FormBehaviour.CastClipMs = 700` | `FormBehaviour.cs:189-194` | `grep -rn CastClipMs src tests` → **only its declaration**. Its own comment says "the sim's one-cast-at-a-time gap is this same number (SoloBattle.CastGapFor)"; `grep -rn CastGapFor src` → only that comment. | Dead constant with a stale claim about the sim. Delete. |
| `FormBehaviour.ClipShareOfBeat = 0.55f`, `SkillClipShareOfBeat = 0.90f` | `:196-223` | Only `SoloExpeditionScreen.cs:3300, :3316` | Presentation tuning living in Core. Move to the Game project. |
| `FormBehaviour.AuraTickMs = 1000` | `:157` | Sim fallback (`SoloBattle.cs:1260`) **and** screen pulse clock (`SoloExpeditionScreen.cs:3432`, `:1149`) | Legitimately shared, but the screen should read the equipped Field's `Def.IntervalMs` (which the sim now uses, `SoloBattle.cs:1260`), not the fallback constant — a PRESS at 2000 ms pulses its aura at 1000 ms on screen. |

No clip length flows **into** sim timing. The dependency direction is Core→Game only; the smell is that Core owns numbers whose only reader is the renderer.

---

## 3. Q2 — Event consumption

### 3.1 Event → visual (the replay switch, `SoloExpeditionScreen.cs:1193-1306`)
| `BattleEventKind` | Emitted at | Screen reaction | Lines |
|---|---|---|---|
| `Aura` | `SoloBattle.cs:1272` | Only sets `auraAtMs` so the following Strikes are classified as a field tick (no sound, no lunge, one aggregated number) | `:1196-1198, :1214, :1228-1233` |
| `Strike` | `:1020` | Champion lunge if `!FromSkill`; `sfx_hit` (quieter for skills, silent for aura ticks); damage number over `Slot` (`SpawnDamage`); white hit-flash on that creature; `fx_weakhit` every other blow | `:1200-1245` |
| `EnemyStrike` | `:1864` | Enemy lunge, `_enemySinceHit = 0`, next-swing lookahead, pitched-down `sfx_hit`, `fx_hit` on champion | `:1246-1253` |
| `Skill` | `:1543, :1558, :1714, :1942` | `_skillFlash[SkillKey(Source, Form)]`; callout `CalloutFor(Form)`; `PlayFormVfx(Form, Source, castTarget)`; `sfx_cast` (+`sfx_crit` if Trap) | `:1253-1272` |
| `Heal` | `:1195` | `+N` callout; `fx_heal` | `:1273-1279` |
| `Shield` | `:1677` (BANKED) and `:1962` (UNDYING) | `Say("UNDYING")`, `fx_shield` gold — **for both** | `:1280-1283` |
| `Down` | `:1967` | `sfx_champ_down`, `fx_death` | `:1283-1287` |
| `EnemyDown` | `:1030` | `_diedAt[slot]`, boss/creature down sfx, `fx_death` (delayed 0.45 s), `fx_crit` for a boss | `:1288-1302` |
| `Charge` | `:1542, :1606, :1754, :1897` | Latches `_chargeNow` (pool after change) | `:1303-1306` |
| `Break` | `:1323` | **Not in the switch.** Applied as state by `WaveReplay.Apply` (`WaveReplay.cs:347-349`); read as `CreatureBreaks(i)` and drawn as a badge | `:1763-1768` |
| `Beat` | `:683, :1776` | **Not in the switch.** Read via `WaveReplay.BeatAt/LastBeat/FirstBeat` for the rail's beat-counted ring and the cross-wave carry | `:2942-2947, :366-384` |

### 3.2 State-polled visuals vs one-shot events
**State-driven (survive a mid-wave open, re-derived from the replay's playhead):**
- Champion HP bar and numerals — `_replay.HealthOf(0)`, `HealthFractionOf(0)` (`:2346-2349`).
- Per-creature health pips — `CreatureHealthFraction(i)` (`:1786-1797`); single-creature bar `EnemyHealthFraction` (`:1877`); boss bar (`:1946`).
- Creature alive/dead → death clip from `_diedAt` (`:1744-1747, :1596-1605`). *Caveat:* `_diedAt` is screen-side wall-clock, so a `DevSeek` past a death has no death time — the corpse is skipped, not drawn.
- Champion shield outline — `_replay.IsShielded(0)` (`:1494`; `WaveReplay.cs:134-135, :370-372`).
- Defence-break badge — `CreatureBreaks(i)` (`:1763`).
- CHARGE pill — latched from the last `Charge` event (`:1304, :2883-2886`); explicitly *not* re-derived (`WaveModel.cs:239-240`).
- The **aura** — `VfxPlayer.Hold` every frame (`HoldAura`, `:3492-3507`); its brightness spike runs on a **wall-clock** metronome (`PulseAuraOnClock`, `:3460-3465`) and its *existence* comes from the **loadout** having a Field slot (`BeginWave`, `:861-870`), **not** from sim `Aura` events. The sim's `Aura` events are used only to classify damage numbers. So the aura is a state visual whose source of truth is the UI's build, not the fight's.

**One-shot only:** callouts, damage numbers, hit-flash (~200 ms), all `_vfx.Play` bursts, banners (`_bannerTimer`), the fall flash.

### 3.3 Persistent statuses with NO state-driven visual
The sim keeps these as wave-local state and emits **no event** for any of them (grep for `events.Add` near each site: none):

| Status | Sim state | Sim lines | Screen |
|---|---|---|---|
| Slow (MIRE, NUMB, TEEMING) | `slowFactor` stretches `nextBite` | `SoloBattle.cs:626, :1347, :1798` | nothing |
| Attack break (WILT, SHRIVEL) | `attackBreak` scales `incoming` | `:585, :1367, :1823` | nothing |
| Amplify window (CALL/BRAND, mark) | `markBonus`, `markWholeWave`, `window` | `:623-625, :797-800, :1397, :1518-1530` | nothing (the `Skill` callout "MARK" flashes once) |
| Stun (PIN) | `nextBite += def.StunMs` | `:1289-1296` | nothing |
| Bleed/poison (WEEP, Volley bleed pool) | `poison += …` | `:1040-1042, :598-611` | nothing (bleed ticks arrive as `Strike FromSkill:true` at odd timestamps and print as skill hits) |
| REPAY bank (`takenSinceCast`), JAWS reflect growth | `:627, :1852` | nothing |
| BANKED shield total | `bankedShield` → `Shield` event | `:1676-1677` | mis-drawn: see 3.4 |

The screen's own comment concedes the gap: *"slow and attack break belong here too, and the layout leaves room to the right"* (`:1761-1762`). Only Break was wired (commit `ae30f2a`). Under the "generalise after two real users" law, Break + the explicitly requested Slow/AttackBreak is the second user — a `StatusKind` on the badge path is now warranted, not premature.

**Badge liveness bug:** the break badge is drawn **only in `DrawComposition`** (multi-creature waves, `:1763-1768`). `grep -n "CreatureBreaks\|icon_effect_break" SoloExpeditionScreen.cs` → lines 1763, 1766 only. A single-creature wave goes through `DrawNormalEnemy` (`:1815-1881`) and a boss through `DrawBoss` (`:1893-1936`); neither draws it. PRESS's most visible target — a lone Bruiser or the boss — never wears its break.

### 3.4 One event kind, two meanings — `Shield`
- `UNDYING`: `Amount = UndyingShieldMs` (a **duration**), `SoloBattle.cs:1962`.
- `BANKED` (SNARE variation): `Amount = (int)bankedShield` (a **damage total**), `SoloBattle.cs:1677`.
- `WaveReplay.Apply` treats both as `_shieldUntil = AtMs + Amount` (`WaveReplay.cs:371`) → a BANKED shield of 340 points draws the steel outline for 340 ms; the screen says `UNDYING` and fires the gold `fx_shield` on every bank (`:1280-1282`). Confirmed by reading both emit sites; no test covers BANKED's presentation (`WaveReplayTests.cs:143` builds its own Shield event).

### 3.5 `VfxPlayer.Hold` usage
Defined `VfxPlayer.cs:210-237` (loop, never fades, dropped when not re-asked; no `Stop()` by design). **Exactly one caller:** `HoldAura` → `_vfx.Hold(FxFor(_auraForm, passive: true), …)` at `SoloExpeditionScreen.cs:3505`. The Hold mechanism is sound and is the right primitive for every status in 3.3.

---

## 4. Q3 — How skill visuals are keyed

### 4.1 The resolver chain today (ad hoc, keyed on `Form`)
```
sim: BattleEvent(Skill, Slot=(int)sk.Source, Amount=(int)sk.Form)      SoloBattle.cs:1558
  └─ screen: form = (Form)e.Amount                                     SoloExpeditionScreen.cs:1254
       ├─ CalloutFor(form)  → "STRIKE/VOLLEY/AURA/TRAP/MARK/MORPH"       :682-690   (Form names, not skill names)
       ├─ PlayFormVfx(form, (Source)e.Slot, target)                      :1366-1408
       │    └─ switch(form) → placement/scale/travel, FxFor(form)        :1375-1407
       │         └─ FxFor: SkillCatalogue.Resolve(form, passive).FxKey   :3350-3359
       │              → "fx_<char>_<key>_strip8_512" else "fx_<key>"     (alias table AssetLibrary.cs:177-183)
       └─ UpdateChampionClip: clip = ((Form)next.Amount).ToString().ToLower()   :3277-3280
            └─ Character.StripKeys(clip) → char_<id>_<clip>_strip8_512, then GenericClipFor(clip)  Character.cs:181-197
```
- **`SkillDef.ClipKey` is never read.** `grep -rn -E "ClipKey|FxKey" src/ResonanceHunter.Game tests tools` → `SoloExpeditionScreen.cs:3356` (FxKey only), `skill_catalogue_test.cs:254-255`, `skill_slot_kinds_test.cs:53`. The clip name is the **Form enum's name**, which happens to equal the ClipKey values. A dead field that tests certify.
- **Source** → tint only: `SourceGlow` (`:1411-1419`) for VFX, and a *second* palette `SourceColor` (`:2283-2288`) for the rail and aura. Two Source palettes in one file.
- **Variation** → nothing (its Source would tint, but see §6.2 — variations never reach the fight).
- **Reinforcement** → nothing.
- The **rhythm/cooldown** readout reads `FormBehaviour.CooldownBeats(s.Form)` / `BaseCooldownMs(s.Form)` (`:2926, :2963, :2972, :3039-3048`) rather than `railDef.Beats` / `railDef.IntervalMs`, i.e. the Form table, not the skill. A reinforcement that changes `CooldownMultiplier` cannot show.

### 4.2 The twelve skills — keys and assets
Catalogue rows from `SkillCatalogue.cs:263-593`. Roster clips on disk (`find assets/art -name "char_*_strip8_512.png"`): every champion has `attack, cast, death, idle, mark, projectile, strike, transformation, trap` — **no `aura`**. Shared fx on disk: `fx_{strike,press,trap,mark,projectile,weep,aura,transformation,wilt,hit,weakhit,crit,death,heal,shield,levelup,bind_chain}_strip8_512`. Per-champion fx on disk: `fx_<char>_{mark,projectile,strike,transformation,trap}` for all ten — **none for press/weep/wilt/aura**.

| Skill | Style / Kind | LegacyForm | ClipKey (unused) → clip actually chosen | FxKey → resolved key | Result |
|---|---|---|---|---|---|
| BLOW | Hammer / Active | Strike | `strike` → `char_*_strike` ✓ | `strike` → `fx_<c>_strike` ✓ | OK |
| PRESS | Hammer / Field | null (slot Form=Strike) | — (Field, no clip) | `press` → `fx_<c>_press` ✗ → `fx_press` ✗ (no alias) | **Held aura draws nothing** |
| REPAY | Snare / Active | null (slot Form=Trap) | `trap` → `char_*_trap` ✓ | `trap` → `fx_<c>_trap` ✓ | OK (callout says "TRAP") |
| JAWS | Snare / Reaction | Trap | `trap` (opportunistic, `:3297-3304`) ✓ | `trap` ✓ | OK |
| CALL | Sign / Active | Mark | `mark` ✓ | `mark` ✓ | OK |
| BRAND | Sign / Field | null (slot Form=Mark) | — | `mark` → `fx_<c>_mark` — a *cast burst* strip looped as an aura | Draws, but wrong picture |
| SPRAY | Volley / Active | Projectile | `projectile` ✓ | `projectile` ✓ (travels) | OK |
| WEEP | Volley / Reaction | null (slot Form=Projectile) | — (no event is emitted for a bleed) | `weep` → **never requested** (`FxFor` is only called with `passive:true` for a Field; `PlayFormVfx` uses `passive=false` → SPRAY) | `FxKey:"weep"` and `fx_weep_strip8_512.png` dead |
| PULSE | Field / Active | null (slot Form=Aura) | `aura` → no strip → `GenericClipFor` → `cast` ✓ | `aura` → `fx_aura` ✓ | OK (callout says "AURA") |
| MIRE | Field / Field | Aura | — | `aura` → `fx_aura` ✓ | OK |
| DRINK | Drain / Active | Transformation | `transformation` ✓ | `transformation` ✓ | OK (callout says "MORPH") |
| WILT | Drain / Field | null (slot Form=Transformation) | — | `wilt` → `fx_<c>_wilt` ✗ → `fx_wilt` ✗ (no alias) | **Held aura draws nothing** |

How a slot's Form is set for the six no-LegacyForm skills: `PlayerLoadout.SetSkill` writes the *style's active's* LegacyForm (`PlayerLoadout.cs:194-197`), so PRESS/BRAND/WEEP/WILT/REPAY/PULSE are addressed on the wire as Strike/Mark/Projectile/Transformation/Trap/Aura and disambiguated only by `Passive`.

**Callout text is the Form, not the skill** (`:682-690`): PULSE prints "AURA", DRINK prints "MORPH", REPAY prints "TRAP", every Hammer cast prints "STRIKE". The skill dock, meanwhile, prints `railDef.Name` (BLOW/PRESS…, `:3027`). The arena and the rail name the same event differently.

### 4.3 Is there a resolver?
No. Skill→clip is a Form-name string (`:3280`) plus a string switch (`Character.cs:189-197`); skill→fx is `FxFor` (catalogue lookup + two-level fallback); Source→colour is two hand-written palettes; mechanic→accent (crit gold, skill vellum, aura silent) is inline in the Strike case (`:1214-1243`). The art contract (`design/art/arena-art-contract.md` §3.1/§3.3) documents the *per-Form* clip and *per-Form-per-character* fx tables; nothing documents per-skill.

---

## 5. Q4 — Core ↔ MonoGame, and presentation types in Core

| Item | Evidence | Verdict |
|---|---|---|
| `Microsoft.Xna` in Core | one doc-comment hit, `Rig.cs:7`; `Core.csproj` has no package refs | Clean |
| `Core/Animation/Rig.cs`, `Clip.cs` (Vec2, Bone, BoneTransform, Rig, Easing, Keyframe, Clip) | `grep -rn -E "ResonanceHunter\.Core\.Animation|new Rig\(|\bRig\b|AttackerStrike|Clip\.Idle\(|BoneTransform|Keyframe" src tests tools` → Game: only the unused `using` at `SoloExpeditionScreen.cs:8` and the word "Rig" in a comment (`:3393`, meaning capture rig); tests: `RigTests.cs`, `ClipTests.cs`. `DrawChampion` remark: *"This replaced an assembled cutout RIG"* (`:3559-3577`). `Clip.cs:31` cross-references `Combat.Telegraph.WindupFloorMs`, which no longer exists (`ClipTests.cs:78`). | **Pure math, zero runtime consumers → dead.** ADR-002 (*Accepted*, "adopted for the player champion", `ADR-002:12-19`) is stale; `design/gdd/animation-rig-system.md` still titles itself "Resonance Hunter" and describes this rig as the highest-risk system. |
| `Core/Presentation/CanvasFit.cs` | Consumed by `DisplaySettings.cs:33-34,124,131` (`CanvasWidth/Height`, `PresentFit`, `ToCanvas`). `LargestIntegerScale`, `Present(vw,vh,scale)`, `WindowedScales` → **no Game consumer** (DisplaySettings' own note `:36-48`), tests only. `CanvasWidth = 480` while the live render target is `CanvasWidth * ArtScale` = 1920 (`Game1.cs:66, :1151`) and every screen authors in 1920 coords. | Acceptable as pure, tested arithmetic; the integer-scale half is dead and the 480×270 "canvas" is a legacy unit the code multiplies by 4 everywhere (`SoloExpeditionScreen.cs:1429,2028`). |
| `Expeditions/WaveReplay.cs` | Pure; proven `replayed health == simulated health` (`WaveReplayTests.cs:65-264`). Depends on `Abilities.Form` (`:172, :303`). | Right place, wrong key — see §4. |
| `FormBehaviour.CastClipMs`, `ClipShareOfBeat`, `SkillClipShareOfBeat` | §2.3 | Presentation tuning in Core. |
| `RunReport.Verdict()` / `DiffEntries` | English sentences and labels built in Core (`RunReport.cs:68-83, :106-117`); the screen re-labels them (`DiffLabel`, `SoloExpeditionScreen.cs:2079-2086`) | Two vocabularies for one row; Core owns display copy. Acceptable for now, flagged for localisation. |
| `BattleEvent` docs name their consumer ("the HUNT screen tints…", `WaveModel.cs:292`) | — | Comment coupling only. |

---

## 6. Q5 — UI → sim

### 6.1 The mechanism (works)
- `BuildStamp()` = loadout signature + dust nodes + mastery nodes + character id (`SoloExpeditionScreen.cs:768-770`).
- `BeginWave`: if the stamp changed → `_run.ReplaceBuild(ComposeBuild(h))` (`:847-848`); `SoloExpedition.ReplaceBuild` keeps the run and clears `ReadyAt/ReadyAtBeat` only for slots whose Form or Source changed (`SoloExpedition.cs:201-219`).
- `_run.RefreshPool()` every wave, since gear is not in the stamp and the sim reads worn mods live (`:849-852`; `SoloExpedition.cs:242-253`).
- Host pushes every screen input each frame (`Game1.cs:3472-3505`) and drains the reward queue (`:3634-3680`).

### 6.2 CONFIRMED: chosen variations/reinforcements never reach the fight
- `ComposeBuild` → `Loadout.ToBuild(Tree, Mastery, Character)` — **no `Progress` argument** (`SoloExpeditionScreen.cs:775`; signature `PlayerLoadout.cs:255-256` has `SkillProgress? progress = null`).
- `BuildComposer.Compose`: `var variation = progress?.VariationOf(def);` → null → `Reinforcements = []` (`BuildComposer.cs:182-193`).
- The player *can* choose: `WeaveScreen.cs:792 SkillLevels.ChooseVariation`, `:810 TakeReinforcement`, on the shared `_skillProgress` (`Game1.cs:2764`).
- The hunt holds the same object as `Progress` (`Game1.cs:3475`) but uses it only to **level** skills (`SoloExpedition.cs:357-359`), never to compose.
- Other call sites that do pass it: `Game1.cs:1532, :2828, :3538, :5186`, `CharacterScreen.cs:426`. Others that omit it like the hunt: `StatsScreen.cs:174`, `WeaveScreen.cs:610` (the weave screen's own `DescribeBuild`) and `:1370` (its DPS bench) — so the screen where you buy a variation also does not show its effect.
- `BuildStamp()` does not include progress, so even after the fix a purchase mid-descent would not trigger `ReplaceBuild` until something else changes.
- Green tests that mask it: `variation_liveness_test.cs:92-94`, `reinforcement_liveness_test.cs:80` compose with `progress:` directly.

### 6.3 LATENT: the rail keys on the slot's Source, the sim emits the variation's
- Sim: `Slot = (int)sk.Source` where `EquippedSkill.Source => Variation?.Source ?? Ability.Source` (`Build.cs:240`; `SoloBattle.cs:1558`).
- Rail: `SkillKey((int)s.Source, (int)s.Form)` and `NextSkillAfter(_playheadMs, (int)s.Source, formKey)` with `s = Loadout.Skills[i]` — the **SkillChoice's** Source (`SoloExpeditionScreen.cs:2905, :2918-2919`; also `FoldSkillCarry` `:370-371`).
- Today masked by 6.2. The moment 6.2 is fixed, any skill whose variation Source differs from the slot Source has a cooldown ring that never fills and a flash that never fires.
- Also: `SkillKey(source, form)` collides for BLOW+PRESS (both Form=Strike) woven with the same Source (`:348`). The event stream has no skill identity; it should carry the **slot index** (the sim already has `i`) or a skill id.

### 6.4 Other UI≠sim residue
- `_beatMs = BeatFor(_castRate)` (`:856-857`) omits the sim's live terms `RatePerCreature` and `CastRampPerCast` (`SoloBattle.cs:664-666`) — clip sizing drifts under TIDE/RHYTHM nodes. Cosmetic.
- `ComposeBuild` runs twice per boundary (`:848` and `:856`).
- The dock draws `Loadout.Skills` live while the replay is of the previous build — one-wave lag, acknowledged (`:425-427`).
- `SpeedMultiplier` is a `const 1.0` still consumed by the offline-rate maths (`Game1.cs:3631`) — a dead knob.
- Aura visual existence derives from the loadout (`:861-870`), not the sim; it therefore shows for a Field skill the mastery gate refused (`BuildComposer.cs:178 continue`) if the slot is still woven.

---

## 7. Q6 — Screens and legacy terminology

### 7.1 Screens (from `Game1.cs`)
Nav tiles `Game1.cs:5117-5126`, routing `OpenNav` `:5273-5286`, draw chain `:4059-4077`:

| Tile (key) | `_show*` | Class / file |
|---|---|---|
| HUNT (H) | terminal `else` | `SoloExpeditionScreen.cs` (+ `DrawLog` overlay `:4081`) |
| GEAR (C) | `_showCharacter` | `CharacterScreen.cs` |
| STATS (V) | `_showStats` | `StatsScreen.cs` |
| BUILD (B) | `_showWeave` | **`WeaveScreen.cs`** |
| MASTERY (E) | `_showBuild` + `ShowTree` | **`BuildScreen.cs`** |
| VAULT (K) | `_showChests` | `ChestScreen.cs` |
| FORGE (F) | `_showForge` | `ForgeScreen.cs` |
| WARREN (A) | `_showWarren` | `WarrenScreen.cs` |
| MAP (W) | `_showWorld` | `MapScreen.cs` |
| TRAITS (P) | `_showPrestige` | `PrestigeScreen.cs` |
| ROSTER (R) | `_showRoster` | `RosterScreen.cs` |
| — | title, settings, help, boot toast, tour | `Game1.cs` |

File names are inverted against the tiles: the BUILD tile opens `WeaveScreen.cs`; the MASTERY tile opens `BuildScreen.cs`. `FormHexDiagram.cs` (332 lines) is the Form-affinity hexagon, used by `BuildScreen.cs:1322-1324, :1395-1396`.

### 7.2 Legacy terminology in user-facing strings (grep of string literals in `src/ResonanceHunter.Game/*.cs`)
| Term | Where (file:line) | Note |
|---|---|---|
| FORM / "SOURCE AND A FORM" | `BuildScreen.cs:692` "EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH"; `:765` "FORMS"; `:781`; `:1389` "SIX FORMS ON THE LOOM"; `:1400` "THE FAR FORMS HIT SOFTER"; `:1402` "FAR-FORM SKILL"; `:1768` node label "FORM"; `Game1.cs:5009` "SOURCE x FORM x VOW", `:5013` "FORM IS HOW YOU FIGHT"; `WeaveScreen.cs:579` "EVERY SKILL THE SAME FORM", `:905` "FAR-FORM SKILL", `:1749` "FORM: …" | Contradicts the 2026-08-30 model (a skill is learned, not composed). |
| Form names as callouts | `SoloExpeditionScreen.cs:682-690` STRIKE/VOLLEY/AURA/TRAP/MARK/MORPH; `:2340` `"{FormShort(mf)} ADEPT"`; `:2303-2306` | Arena speaks Form; rail speaks skill. |
| WEAVE / WOVEN | `WeaveScreen.cs:731` "SLOT WOVEN.", `:967` "WHAT YOU ARE WEAVING", `:899`; `ChestScreen.cs:742` "NONE WOVEN"; `Game1.cs:5008` "WOVEN SKILLS" | "Weave" as *equip a learned skill* may be intended; as *Source×Form composition* it is legacy. Open question. |
| WEIGHT / SPREAD | only in comments now (`BuildScreen.cs:428-429`, `PixelFont.cs:150-151`) | Stale comments, no live copy. |
| MASTERY (two meanings) | tile/screen "MASTERY" (`Game1.cs:5123`, `BuildScreen.cs:1127`), "MASTERY POINTS" (`StatsScreen.cs:236,537`, `BuildScreen.cs:860`); vs Warren currency `WarrenResource.Mastery` shown as "INSIGHT" (`WarrenScreen.cs:104,117,263,315,324,373`); vs "REGION'S MASTERY" (`PrestigeScreen.cs:468`) | Code name `WarrenResource.Mastery` ≠ UI name INSIGHT; three different "mastery"s. |
| Resonance Hunter | No string literal in Game (`Window.Title = "IDLExIDLE — pre-alpha"`, `Game1.cs:435`). Remains in: namespace `ResonanceHunter.Client`, project folders/csproj/slnx, env-var prefix `RH_*` (`Game1.cs:459-477`), GDD titles (`animation-rig-system.md:1`), the test-path literal in `hunt_screen_feedback_test.cs:35`. | Identity debt, not player-visible. |

---

## 8. Q7 — What the presentation guards actually guard

| Guard | What it pins | Blind spots |
|---|---|---|
| `tests/unit/.../Presentation/hunt_screen_feedback_test.cs` | Five **source-text** assertions on `SoloExpeditionScreen.cs`: locked rewards hidden (`VaultOpen`/`MasteryOpen`), SPEND POINTS → (E)/`WantsMastery`, no `BarArt(` inside `DrawComposition`, the fall writes to `Log` + banner text, the death flash draws once after `DrawHunterHud(b);` (`:52-122`) | Regex over C# text; passes if the strings survive, regardless of behaviour. No Game test project exists (`check_nav_gates.py:10`). |
| `WaveReplayTests.cs` (8 tests) | replayed health == sim health with/without heals, clamp, playhead ordering, shield window, next-strike lookahead, per-creature replay, Strike carries creature index | No test for `Break` state, `Beat`, `NextSkillEventAfter` Trap skip, or a BANKED `Shield`. |
| `tools/check_asset_keys.py` | Every **literal** key in `Get/GetFirst/Has/Sprite*/AnimSprite/BarArt/Panel/Icon/_vfx.Play`, every literal `sfx_*/music_*`, the `music_arena_<theme>` and `source_<theme>` families, class icons once any ships, and every alias target | Runtime-built keys — `FxFor`'s `fx_<key>`, `Character.StripKey`, `<enemy>_<clip>_strip8_512`, `icon_skill_<id>` — by its own admission (`:17-20`). The `fx_press`/`fx_wilt` hole lives exactly there. |
| `tools/check_nav_gates.py` | `Nav` and `NavActivity` parallel arrays have equal length; every named `Activity` exists; every `Activity` has a tile | Index-keyed screen routing remains. |
| `tools/check_all.sh` | Runs the seven Python gates (fonts, ui type, asset keys, init order, nav gates, mouse space) | No build, no runtime. |
| `break_badge_test.cs` (Core) | The sim emits `Break` events | Not that the screen draws them on every enemy path. |

---

## 9. Findings catalogue

### 9.1 Authoritative (keep; this is the model)
- **Instant resolve + replay** — `SoloExpedition.PushWave` → `WaveReplay.Advance`. Sim never waits on presentation.
- **`BattleEvent` stream** as the only sim→screen channel (`WaveModel.cs:241-306`), with `Beat` published rather than inferred (`:258-277`).
- **`WaveReplay`** as tested pure state re-derivation (`WaveReplay.cs`, `WaveReplayTests.cs`).
- **`VfxPlayer.Hold`** — state effects with no `Stop()` (`VfxPlayer.cs:210-237`).
- **Committed-clip model** aimed at the next beat via replay lookahead (`UpdateChampionClip`, `SoloExpeditionScreen.cs:3255-3325`; `WaveReplay.NextChampionStrikeAfter/NextSkillEventAfter`).
- **Host-fed screen** + reward queue (`Game1.cs:3472-3680`).
- **Two-level asset fallbacks** so art ships incrementally (`Character.StripKeys`, `FxFor`) — but they need a gate (see 9.3).
- **`DisplaySettings` as the MonoGame skin over `CanvasFit`** (`DisplaySettings.cs:24-30`).

### 9.2 Obsolete
| Name | Files | Class | Why | Replacement |
|---|---|---|---|---|
| Cutout rig (`Rig`, `Bone`, `BoneTransform`, `Vec2`, `Clip`, `Keyframe`, `Easing`) | `Core/Animation/Rig.cs`, `Clip.cs`; `tests/unit/.../Animation/*` | A-delete | Zero runtime consumers (grep in §5); champion is sprite strips (`SoloExpeditionScreen.cs:3559-3577`); `Clip.cs:31` cites a deleted symbol | Delete; mark ADR-002 *Superseded* and `animation-rig-system.md` superseded by `design/art/arena-art-contract.md` §3 |
| `FormBehaviour.CastClipMs` | `FormBehaviour.cs:189-194` | A-delete | 0 consumers; cites nonexistent `SoloBattle.CastGapFor` | none |
| `CanvasFit.LargestIntegerScale`, `Present(vw,vh,scale)`, `WindowedScales` | `CanvasFit.cs:34-60` | A-delete | Only tests; `DisplaySettings.cs:36-48` says the ladder is retired | `PresentFit`/`ToCanvas` stay |
| `SkillDef.ClipKey` (as consumed today) | `SkillCatalogue.cs:166` | C-replace | Never read; clip = Form name | Make the screen read `Def.ClipKey`; delete Form-name derivation (`:3280`) and `GenericClipFor` string switch |
| Form-keyed presentation: `CalloutFor(Form)`, `PlayFormVfx(Form,…)` switch, `FormShort`, `SkillKey(source, form)`, `_auraForm` | `SoloExpeditionScreen.cs:682-690, 1366-1408, 2303-2306, 348, 3426` | C-replace | Legacy axis; names the Form not the skill | Key on `SkillDef` (id/slot) + a small placement enum |
| `WaveReplay` Form dependency | `WaveReplay.cs:172, 303` (`Abilities.Form.Trap` checks) | C-replace | Core replay knows the legacy enum | Filter on `SkillKind.Reaction`/event field |
| `FormHexDiagram.cs` | Game | B-migration-only? | Draws the Form affinity hexagon for the mastery attunement | Depends on whether affinity survives as Style ring (`SkillCatalogue.RingDistance`) — open question |
| `SoloExpeditionScreen.SpeedMultiplier`/`_speedMul` | `:85-97`, `Game1.cs:3631` | A-delete | Constant 1.0 | none |
| `DrawLayeredChampion`, `_enemyArt`, `[Obsolete] Guide`, `DrawBattleControls` body | `:3673-3682, :472/1425, :642-643, :3113-3133` | A-delete | 0 callers / write-only / tombstones | none |
| `using ResonanceHunter.Core.Animation;` | `SoloExpeditionScreen.cs:8` | A-delete | binds nothing | none |

### 9.3 Dead fields / dormant paths (with the grep)
| Field | Declared | Evidence |
|---|---|---|
| `SkillDef.ClipKey` | `SkillCatalogue.cs:166` | `grep -rn -E "ClipKey" src/ResonanceHunter.Game` → 0; tests only assert non-empty |
| `SkillDef.FxKey = "weep"` and `assets/art/VFX/weep/fx_weep_strip8_512.png` | `SkillCatalogue.cs:461` | `FxFor` only reaches a passive's key via `passive:true` (Field aura, `:3505`); WEEP is a Reaction and emits no `Skill` event (`SoloBattle.cs:1040-1042` adds poison, no event) → key never requested |
| `fx_press`, `fx_wilt` requests | `FxFor` `:3356-3358` | `AssetLibrary.cs:177-183` alias table has no `fx_press`/`fx_wilt`; `find assets/art -name "fx_*press*"` → only the shared strip whose key is `fx_press_strip8_512`; `VfxPlayer.Hold` `:221` returns on null → PRESS/WILT fields draw nothing |
| `FormBehaviour.CastClipMs` | `FormBehaviour.cs:194` | `grep -rn CastClipMs src tests` → declaration only |
| `EquippedSkill.Variation`/`Reinforcements` in the live hunt | `Build.cs:267-270` | `SoloExpeditionScreen.cs:775` omits `Progress`; `BuildComposer.cs:184` null → base line |
| Break badge on single/boss enemies | `SoloExpeditionScreen.cs:1763` | `grep -n CreatureBreaks SoloExpeditionScreen.cs` → line 1763 only (inside `DrawComposition`) |
| `_enemyArt` | `:472` | set at `:1425`, never read (2 hits total) |
| `Core.Animation.*` | `Rig.cs`, `Clip.cs` | see §5 |

### 9.4 Hardcoded branches on content IDs / legacy enums in presentation
| Where | Keys on | Note |
|---|---|---|
| `SoloExpeditionScreen.cs:682-690` `CalloutFor` | `Form` | prints Form names |
| `:1375-1407` `PlayFormVfx` switch | `Form` | placement/scale/travel per Form — semantic (on target / travels / on champion / under row) but keyed on the legacy enum |
| `:3277-3280` clip = `Form.ToString()` | `Form` | should be `Def.ClipKey` |
| `Character.cs:189-197` `GenericClipFor` | clip strings | string-keyed art fallback |
| `AssetLibrary.cs:177-183` | `fx_<form>` aliases | per-Form, missing the passive keys |
| `:2926, :2963, :2972, :3039-3049` rail rhythm | `FormBehaviour.CooldownBeats/BaseCooldownMs(Form)` | should read `railDef.Beats/IntervalMs` |
| `:194-198, :208-212, :228-232` | boss id, Source→enemy, region→boss tables | content tables inside the screen |
| `WaveReplay.cs:172, :303` | `Abilities.Form.Trap` | Core replay on legacy enum |
| `Game1.cs:5117-5126` + `:5273-5286` | nav index | guarded by `check_nav_gates.py` |
| `PlayerLoadout.SetSkill` `:194-197` | `LegacyForm` of the style's *active* | encodes six skills as another skill's Form + `Passive` |

### 9.5 Serialization risks
| Field | Risk | Migration |
|---|---|---|
| `PlayerLoadout.SaveSkills()` persists `(Source, Form, VowId, Passive)` — **not `SkillId`** (`PlayerLoadout.cs:278-292`; `SaveGame.cs:293-297`) | Identity is reconstructed by `SkillCatalogue.Resolve(Form, passive)`; a third skill per style, or a skill without a LegacyForm sibling, is unrepresentable | Persist `SkillId`; keep Form as read-only fallback in the migration path only |
| `BattleEvent.Skill` `(Slot=Source, Amount=Form)` | Not persisted, but it is the wire contract the screen and `WaveReplay` decode; changing it breaks both | Change together with `WaveReplay` and the rail |
| `RunReportSave` stores `Outcome/WallArchetype/WallAffixes` as `int` (`RunLog.cs:91-135`) | Enum reorder silently relabels history | Persist enum names |
| `SavedSkill.Form` (`SaveGame.cs:297`) | legacy enum as string | keep for migration only |
| `ChestKeepMinTier/Slots` (`SaveGame.cs:234, :241`) | presentation filter in the save — fine | none |
| Display prefs in own file (`DisplaySettings.cs:136-140`) | good separation | none |

### 9.6 Duplicated responsibilities
| A | B | Overlap |
|---|---|---|
| `SourceGlow` `:1411-1419` | `SourceColor` `:2283-2288` | two Source palettes in one file |
| `DrawComposition` `:1643` | `DrawNormalEnemy` `:1815`, `DrawBoss` `:1893` | three creature draw paths; break badge, pips vs bar, flash all re-implemented; badge only in one |
| `Outline` `:3694` | `DebugRect` `:3968` | identical 4-fill outline |
| `FormBehaviour.CooldownBeats/BaseCooldownMs(Form)` | `SkillDef.Beats/IntervalMs` | rail reads the Form table, sim reads the skill |
| `CalloutFor(Form)` | `railDef.Name` `:3027` | arena and rail name one cast differently |
| `RunReport.DiffEntries` labels | `DiffLabel` remap `:2079-2086` | two vocabularies for one row |
| `FormBehaviour.AuraTickMs` (screen pulse) | `Def.IntervalMs` (sim tick) | screen pulses at the fallback rate |
| `ComposeBuild` at `:848` | again at `:856` | same boundary, twice |

### 9.7 Core↔presentation coupling (file:line)
- `SkillCatalogue.cs:166-167` — `ClipKey`/`FxKey` art keys in the gameplay record (acceptable data; must be *read*).
- `FormBehaviour.cs:194, :200, :223` — clip timing constants in Core.
- `WaveReplay.cs:172, :303` — replay filters on `Abilities.Form`.
- `WaveModel.cs:292` — `BattleEvent` doc names the HUNT screen.
- `RunReport.cs:68-83` — display sentences in Core.
- `Core/Presentation/CanvasFit.cs` — screen geometry in Core (pure, tested; half dead).
- `Core/Animation/*` — renderer maths in Core (dead).
- `SoloExpeditionScreen.cs:861-870` — the screen re-runs `BuildComposer.SlotKinds` to decide which slot is the Field: presentation re-deriving a composition rule.

### 9.8 Numerical stacking (presentation only)
- `_clipSpeed = clamp(baseSpeed × contactMs / lead, baseSpeed, MaxClipSpeed)` with `baseSpeed = ClipMs / (_beatMs × share)` (`:3316-3322`) — a multiplicative chain on a beat the screen approximates (§6.4); low risk, note only.
- Aura brightness `AuraRest + (AuraPeak − AuraRest) × spike³` (`:3498-3499`) — fine.

### 9.9 Closed-loop currencies touching presentation
- `WaveBonus.Cores` → `Haul.Cores` → host pays `Material.Core` + `FlashSpoil(Material.Core)` (`Game1.cs:3646-3650`; `WaveModel.cs:308-339`). Live, one consumer, fine.

---

## 10. Open questions
1. Is **"weave"** still the verb for equipping a learned skill (WEAVE/WOVEN copy in `WeaveScreen.cs`, `Game1.cs:5008`), or is it legacy Source×Form vocabulary to retire?
2. Should the **aura's existence** derive from sim `Aura` events (state in `WaveReplay`) instead of the loadout's Field slot (`:861-870`)? Today a refused Field still glows.
3. Should the **Form affinity hexagon** (`FormHexDiagram.cs`, `BuildScreen.cs:1322,1395`) survive as a Style ring (`SkillCatalogue.RingDistance`), or go with Form? Out of this area's scope to decide.
4. Does the designer want **per-variation / per-Source visuals** beyond tint (the current model has none), and should reinforcement changes (e.g. `CooldownMultiplier`) be visible on the rail?
5. Should `Shield` split into two event kinds (UNDYING window vs BANKED total), or should BANKED carry a duration?
6. `CanvasFit.CanvasWidth = 480` while everything authors at 1920 — rename/redefine, or keep the ×4 unit?
7. Should `RunReport.Verdict()` sentences move to the Game project (localisation), or is Core-owned copy acceptable for an offline single-language game?
8. Is `WEEP`'s dedicated fx (`fx_weep`) intended for a future "bleed applied" event, or should the asset and key go?

## 11. Recommended order (presentation area only)
1. Pass `Progress` into `ComposeBuild` (`:775`) and add it to `BuildStamp()`; do the same in `WeaveScreen.cs:610,1370` and `StatsScreen.cs:174`. Add a **hunt-path** test that composes through `PlayerLoadout.ToBuild` with a chosen variation.
2. Put **skill identity on the wire**: `BattleEvent.Skill/Aura` carry the slot index (or id); `WaveReplay` and the rail key on it; delete `SkillKey(source, form)`.
3. Introduce one presentation resolver in the Game project: `SkillDef → (clip, fx, placement, calloutName)` reading `Def.ClipKey`/`Def.FxKey`/`Def.Name`; delete `CalloutFor(Form)`, the Form switch in `PlayFormVfx`, `GenericClipFor`'s string switch, and the Form-name clip.
4. Add `fx_press`, `fx_wilt` (and decide `fx_weep`) to the alias table **and** extend `check_asset_keys.py` to enumerate `SkillCatalogue` FxKeys × `fx_<key>` / `fx_<char>_<key>`, and `Character` clips × `ClipKey`.
5. Generalise the Break badge into a status badge driven by `WaveReplay` state for Break/Slow/AttackBreak (second real user), and draw it on all three enemy paths.
6. Delete `Core/Animation`, `CastClipMs`, the integer-scale half of `CanvasFit`; move `ClipShareOfBeat`/`SkillClipShareOfBeat` to the screen; mark ADR-002 superseded.
7. Retire the "SOURCE AND A FORM" copy (`BuildScreen.cs:692,781,1389-1402`, `Game1.cs:5009-5013`, `WeaveScreen.cs:579,905,1749`).
