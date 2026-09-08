# UX V2 audit — HUNT (arena HUD, defeat state, Expedition Log, welcome-back)

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0–15, §16–22, §25, §82–102 and the
current runtime (STYLE → SKILL → VARIATION → REINFORCEMENTS, `SkillId` identity; Form / WovenAbility /
Aptitude / Source×Form deleted from Core). Source read in full: `src/IdleXIdle.Game/SoloExpeditionScreen.cs`
(3803 lines), plus the welcome-back toast and fixtures in `src/IdleXIdle.Game/Game1.cs`.
Pixels: `baseline/fight.png`, `baseline/fightreport.png`, `baseline/runlog.png` and their `baseline/720/` copies.

All coordinates are 1920×1080 logical. 720p sizes below are the rung × 0.667 (bilinear downscale):
36→24 · 30→20 · 26→17.3 · 24→16 · 21→14 · 19→12.7 · 16→10.7 · 14→9.3 px.

---

## 0. Does the screen answer its question today?

Brief §100: HUNT = **"How is my build doing right now?"**

Partially. The header answers WHERE / WHEN / HOW FAR well, the enemy strip answers WHAT, and the skill
medallions answer WHEN EACH SKILL FIRES (a genuinely good instrument — see Keep). But the two biggest
panels describe what the build **is** (its Sources, its tempo multiplier, each skill's rule text) rather
than how it is **doing**; nothing shows which skill is carrying the fight; and the one moment the question
gets a real answer — the fall — shows only a wave number and "PRESS L".

---

## 1. Layout today (measured from code)

| Zone | Rect (1080) | Code |
|---|---|---|
| Nav rail (shared, opaque) | (0,0,180,1080) | Game1 `DrawHexNav` |
| Hunter card | (196,20,420,205) PanelQuiet | SoloExpeditionScreen.cs:2306 |
| Stage header | (630,18,560,135) PanelQuiet | :2473 |
| Enemy strip | (630,160,560,44) plate | :2457 |
| Welcome toast (host) | (630,HeaderStackBottom+8=212,560,88) PanelQuiet, 7 s | Game1.cs:3834-3846 |
| Log button | (1206,40,64,64) medallion, no label | :2367 |
| Currency pills + gear (shared) | x≈1470–1895, y 16–76 | Game1.cs:4847 |
| Right column | x 1570–1896: IDLE RATE (110,h108) · REWARD ACTIVITY (230, 62+48·rows+46) · CHEST FILTER row (h54) + popover (h420) | :2561-2573, :2601-2651, :2724 |
| SKILLS rail | (190,236,286,h) h = 208 + (n−1)·128, min 350, max 700 → 1 skill 350, 4 skills 592 | :3139-3182 |
| Arena stage / clip | ArenaRect (492,100,1230,940); ArenaClip (186,100,1734,940) | :548, :572 |
| Champion / enemy boxes | ChampBox (420,570,400,430); EnemyBox (1282,560,436,440); GroundY 1000 | :63, :158, :163 |
| Guide banner (shared) | (470, 1080−h−26 ≈ 952, 980, ~102) — over the stage floor | Game1.cs:3142-3147 |

Combat viewport actually clear of chrome ≈ x 476–1570 × y 225–1080 = **1094×855, about 45 % of the canvas**.
Chrome: 5 PanelQuiet frames (hunter, header, idle, reward, skills) + 2 bordered plates (enemy strip, filter
row) + the toast frame + the gold-lit log medallion.

---

## 2. P0 — readability / hierarchy

**P0-1. Skill rail text is illegible at 720p and Caption carries sentences.**
`720/fight.png`: skill names ("BLOW", "SPRAY") ≈10.7 px; cadence ("EVERY SIXTH ACTION") and the gold rule
text ("FIRES 5 ARROWS AT RANDOM ENEMIES, WITH FEWER ENEMIES THEY ARE SPLIT BETWEEN") ≈9.3 px. Code: name at
`Secondary` shrinking to `Caption` (:3010-3012), rhythm at `Caption` (:3032), rule text at `Caption`, two
wrapped lines (:3074-3081). Brief §6: Caption must not carry a sentence; §18: no mechanical text in combat.

**P0-2. Hunter card secondary lines are 10.7 px at 720p.**
`720/fight.png`: "TEMPO 1.06x ACTION SPEED" (Secondary, :2343), the HP figure "163/180" centred on a red bar
(Secondary, :2341), the mastery title "STRIKE ADEPT" (Caption, :2332). The HP number — the most combat-critical
value on screen — is the smallest text in the card.

**P0-3. Right column and enemy strip captions are 9.3–10.7 px at 720p.**
`720/fight.png`: "CHEST FILTER / ANY TIER · ALL SLOTS / CHANGE ›" (Secondary + Caption, :2733-2737),
"DEEPEST WAVE REACHED 1" (Secondary, :2649), "4 OF 4 STANDING" (Secondary, :2431), "CONQUEST 1 / 20"
(Secondary, :2505).

**P0-4. The Expedition Log — the "why did I stop" screen — is unreadable at 720p below the title band.**
`720/runlog.png`: measure rows Body → 12.7 px, units/points-at Secondary → 10.7 px, the whole
SINCE YOUR LAST RUN HERE block Secondary → 10.7 px, delta chips Caption → 9.3 px ("+0.0 per cast" cannot be
read). Code: :2176-2189 (rows), :2205-2219 (diff), `Chip` default `Caption` (:2537).

**P0-5. Hierarchy: the eye lands on chrome, not on the fight.**
`fight.png`: first read is the gold "UMBRAL REACH" (RegionTitle 36, display face, :2481) and the gold
"WELCOME BACK — 18 MIN AWAY" toast directly under it; second the "-10 CRITICAL" (46 px gold); the champion is
third or fourth. Left 476 px and right 350 px are chrome; the stage is boxed on three sides. Brief §16/§99-11.

**P0-6. Gold is decoration here, not meaning (§10).**
Gold with no earned/selected/primary state: every skill's rule text (:3080), "SKILLS" title (:2866),
"IDLE RATE"/"REWARD ACTIVITY" titles (`CleanPanel` :2591), "CHEST FILTER" row title at rest (:2733, `Gold*0.85`),
"LV 1" (:2334), the conquest label when conquered (:2500). The cooldown ring, the cast telegraph and NEW RECORD
are the gold that *does* mean something — they are drowned by the rest.

**P0-7. Secondary reads as disabled (§7).**
Slate `#8A96A8` is used for live facts: skill cadence (:3032), TEMPO (:2343), DEEPEST WAVE (:2649), the log's
POINTS AT column and row units (:2172, :2181), the diff labels (:2207). The empty-slot label "+B" is drawn in
`Dim` `#2C2C36` on a `#14111A` plate (:3087) — `720/fightreport.png`: invisible.

**P0-8. Frame reduction (§9).**
Seven framed surfaces plus a framed toast on the one screen that should be mostly picture. The SKILLS rail is
a 286×592 ornate frame (1 skill: 350 tall with three empty hex frames — `fightreport.png`). The enemy strip
and the filter row are QUIET already; the five PanelQuiet frames should become at most two (hunter card,
utility) and the skill strip a translucent surface.

---

## 3. P1 — layout / UX

**P1-1. Vertical SKILLS rail vs the brief's bottom strip (§17).** `SkillRailMetrics` (:3157) sizes a
286-wide column 350–700 px tall down the left of the stage; ArenaRect starts at x=492 because of it. No
ACTIVE / PASSIVE grouping although Core has it (`Build.ActiveCapacity` / `PassiveCapacity`, Build.cs:301-343;
`SkillDef.TakesABeat`, `SkillKind.Field`/`Reaction`).

**P1-2. Skill slot content (§18).** Shows glyph + name + cadence words + two lines of rule text. Missing:
the Source **named** (only a colour halo `:2985`, colour-only → §8), readiness as a word (READY / 2 ACTIONS),
persistent state (ACTIVE / x3 stacks). The rule text belongs in BUILD.

**P1-3. Hunter card is a build sheet, not a status card (§19).** TEMPO multiplier (:2343), mastery
"ADEPT" title (:2332), a row of the build's Source gems (:2350-2356) — all static build facts. POWER is an
unlabelled ember icon + number (:2335-2336). Live statuses that Core already has are not shown: shielded
(`WaveReplay.IsShielded(0)`, drawn only as a steel outline :1521), UNDYING (event :1275, `Champion.UndyingSpent`),
CHARGE pool (text in the SKILLS header :2870).

**P1-4. Right utility carries inventory management (§20).** CHEST FILTER row + 420 px popover with eight
slot medallions and a tier stepper (:2724-2818) — economy configuration on the combat screen → MOVE to Vault.
"DEEPEST WAVE REACHED N" (:2649) duplicates the header's CONQUEST N/20 (both `Deepest`) → REMOVE. Button labels
embed key hints "(K)", "(E)" (:2636, :2645).

**P1-5. Defeat says a wave number and "press L" (§22).** `DrawArenaOverlay` HunterDown: two lines in a flat
920×110 fill (:2253-2257). `RunReport.Verdict()` — the diagnosis sentence — already exists and is not shown on
the arena. No ADJUST BUILD / GEAR doors (host flags exist only for Vault/Mastery/Log: `WantsVault`,
`WantsMastery`, `WantsLog`; `WantsBuild` is only a comment :411). The champion restarts after 1.6 s
(`Descent.DownedSeconds`), banner lingers 4.5 s (:140) — the reading moment is short.

**P1-6. Damage feedback floods and inverts priority (§21).** Every Strike event spawns its own number
(:1056-1092); a 5-arrow VOLLEY prints five numbers 54 px apart in a 5-deep column (:500, :509) that wraps onto
itself at fast tempo. Only aura ticks aggregate (`FlushAuraTotal` :3556). Skill-identity callouts ("SNARE") are
36 px (`Say` :1009), crit numbers 46 px → damage outranks skill identity; "UNDYING" (a major state, :1275)
is drawn at the same 36 px as a skill name.

**P1-7. Enemy health pips float above heads.** `fight.png`: pips at y≈735, swarm heads at ≈790;
`fightreport.png`: pips ≈580, heads ≈660. Code claims the pip "sits on the creature's own visible top" (:1791,
`box.Y - 14`) but the swarm strips carry more headroom than the measured `crop = -1` trim removes.

**P1-8. Four wave numbers for one death in `fightreport.png`.** Header "WAVE 1 — RECOVERING", banner
"FELL AT WAVE 14", log "FELL AT WAVE 13 · 12 WAVES CLEARED", header conquest "13 / 20". The header is a
fixture artefact (`DevRunToDeath` pushes waves without `BeginWave`, so `_replayWave` stays 1 — :3737), but
banner `_fellWave = _run.Wave + 1` (:3746) vs log `WallWave` (13) is an off-by-one to verify in live play
(`UpdateFight` uses `_replayWave` :1342 — likely consistent there; the fixture is not).

**P1-9. Welcome-back is a transient two-line toast (§25).** 560×88, 7 s, fades (Game1.cs:3826-3846,
`_bootTimer = 7f`). Text: "WELCOME BACK — 18 MIN AWAY / +140 GLEAM (90 WARREN · 50 HUNT)" (Game1.cs:796-798).
Warren Dust, Scrap and Essence are credited (Game1.cs:756-758) but never reported. `OfflineHunt.Result` carries
`WavesCleared`, `Falls`, `DeepestWave` (OfflineHunt.cs:54-56) — none shown. No CONTINUE, nothing to re-read;
suppressed entirely under the tour (Game1.cs:3829). The fixture string is a literal, not built from the model
(Game1.cs:1793).

**P1-10. Expedition Log layout (§23/§24).** Keep the structure; fix: ~500 px of dead middle between the
figure column (right edge `x0+440` = 982, :2169) and POINTS AT at 1504 (`runlog.png`); no "MAIN LIMIT — REACH"
label above the numbers — the verdict is the sentence "You reached 1.0 of 3.0 creatures per cast." (:2154,
`RunReport.Verdict()`), the brief's exact example of a number without meaning; the POINTS AT column in Slate reads
disabled; the footer repeats "L CLOSES IT" beside a visible close icon (:2050).

**P1-11. Shared guide banner sits on the stage floor.** `GuideBannerRect` → (470, ≈952, 980, ≈102)
(Game1.cs:3142-3147); GroundY is 1000, so it covers the champion's legs and the pack's feet. Any bottom skill
strip (§17) collides with it — §11 (banner behaviour) is a prerequisite for the HUNT pass.

**P1-12. The log's only door is an unlabelled medallion** at (1206,40,64,64) with a hover tip (:2379-2389).
The screen that answers "why did I stop" should also be reachable from the defeat banner (a button) — see
proposal.

---

## 4. P2 — polish

- P2-1. Bare text sizes outside `UiTypography`: `Say` `Px = 36` (:1009) and `DrawCallouts` fallback
  `c.Px <= 0 ? 36 : c.Px` (:3689). The checker passes both (initializer / ternary). Name them (`DamageSkillName`).
- P2-2. Title shrink floor literal `28` in `DrawStageHeader` (:2480); name shrink loop in the hunter card
  (:2325) — acceptable but should name the floor.
- P2-3. Hover tips are 430 px wide at Secondary → 10.7 px at 720p (UiKit.cs:1168-1184); the filter row and
  slot medallions rely on them (:2743, :2778).
- P2-4. "CHARGE 3/6" is a Slate text pill in the SKILLS header (:2870) — it is a live pool; belongs with
  the status icons on the hunter card as icon + number.
- P2-5. Empty slot "+B" (:3087) → a quiet caption that answers "why locked" (§83): "MORE SLOTS — TRAITS".
- P2-6. Fixture `fight` poses `IdleGleamRate` at 0 → "+0/min" (`fight.png`); pose a measured rate.
- P2-7. Fades and the death flash have no Reduced Motion gate beyond the SCREEN FLASH switch (:3591, §87).
- P2-8. Class/file name `SoloExpeditionScreen` for the screen the rail calls HUNT; doc comments still narrate
  Form/Weave (list in §7 below).
- P2-9. `WaveCleared` banner draws unboxed gold text at 268 (:2273) while the other two overlays have a
  plate — three overlay styles for one slot.

---

## 5. What is GOOD and must be kept

- **The combat scene itself**: right-facing champion in `ChampBox`, pack laid out by archetype scale
  (`ArchetypeScale` :1553), per-creature pips, death clips held then faded, white hit flash, held aura with
  spike, wind-up telegraph, per-character skill VFX (`DrawComposition` :1636-1830, `DrawChampion` :3593).
- **Header stack** WHERE / WHEN / HOW FAR + WHAT (`DrawStageHeader` :2465, `DrawEnemyLine` :2408) — one
  hierarchy, quiet frame, conquest bar as the quietest fact. Keep as the one top-centre object.
- **Skill readiness instrument**: cooldown ring stepping one notch per action, dim-while-waiting, gold
  telegraph 300 ms before a cast, cadence carried across waves (`FoldSkillCarry` :336, dock :2884-3070).
  Keep the readout; change its housing.
- **Honest damage numbers**: the event's own `Amount` over the creature struck (:1056), aura ticks summed
  into one number (:3556). Keep; extend aggregation to multi-hit casts.
- **One major overlay at a time** (`ResolveOverlay` :599) and the host-owned welcome toast outranking it.
- **Reward errands as verbs that navigate**, hidden when their screen is locked (:2623-2647).
- **Expedition Log core** (§23): FELL/STALLED band, the wave that ended it, verdict, measures table,
  SINCE YOUR LAST RUN HERE with coloured delta chips, OLDER/NEWER + ENTRY N OF M, close icon (:2013-2222).
- **Persistent enemy state badge** (defence-break `x3`, :1780-1785) — exactly §18's "stacks" and §21's
  "important semantic effects"; extend rather than replace.
- **Brown PanelQuiet as the in-screen standard**, gold nine-slice only on the log modal (:2111) — right per
  the house rule; just fewer of them.
- **Dev fixtures**: `DevRunToDeath`, `DevSeek`, `DevSwingPhase`, `DevHoldFlash`, `DevForceBoss`,
  `DevBossDebug` — the screen is photographable in its hard states.

---

## 6. Proposal — HUNT V2 layout (1920×1080 logical; UI Scale multiplies later)

```
x:   0   180 196            636  660               1260 1290                    1570        1896
y 0  ┌────┬─────────────────────┬────────────────────────┬─────────────────────────┬──────────────┐
     │NAV │ HUNTER CARD         │  STAGE HEADER          │ [log]  currency pills   │ UTILITY      │
     │    │ (196,16,440,124)    │  (660,16,600,112)      │ (1206,40) …             │ (1570,110,   │
 150 │    │                     │  ENEMY STRIP (660,134) │                         │  326,150)    │
     │    ├─────────────────────┴────────────────────────┴─────────────────────────┴──────────────┤
     │    │                        COMBAT VIEWPORT  x 186–1920, y 150–900                         │
     │    │      champion ~x 560                       pack x 1000–1700        GroundY 880         │
 900 │    ├──────────────────────────────────────────────────────────────────────────────────────┤
     │    │   SKILL STRIP (496,912,1000,150):  ACTIVE  [slot][slot]   │   PASSIVE  [slot][slot]  │
1080 └────┴──────────────────────────────────────────────────────────────────────────────────────┘
```

**Hunter card (196,16,440,124), PRIMARY frame (the only ornate frame on the screen):**
line 1 `SEEKER · WANDERER   Lv 12` (Headline 24, class in class colour) … right-aligned `POWER 222`
(label Secondary + PrimaryValue 30, labelled — §19); line 2 HP bar 380×28 with `107 / 180` at Body 19 beside it,
not on it; line 3 status icons 28 px with a number where one exists: shield (`IsShielded`), UNDYING spent/ready,
CHARGE n/cap, defence-break on the pack is the enemy's, stays there.
REMOVED: TEMPO line, mastery ADEPT title, the Source-gem row (the skill strip names Sources now).

**Stage header (660,16,600,112) + enemy strip (660,134,600,40)** — keep as is, one rung larger at 720p via
UI Scale. Region title stays the one display-face line. Log medallion gains a `LOG` caption (Caption 14) —
or is removed when the defeat banner has its own LOG button.

**Utility (1570,110,326,~150), SECONDARY thin frame:**
`IDLE` (Secondary) / `+63/min` (PrimaryValue 30) · `REWARDS` / `2 CHESTS READY` (Body) · `[OPEN VAULT]`
(44 px button, no key in label) · `[SPEND 3 POINTS]` only when >0.
REMOVED from HUNT: CHEST FILTER row + popover → MOVED to Vault toolbar (KeepMinTier/KeepSlots/FilterDirty stay
host-owned; the Vault gets the editor). REMOVED: DEEPEST WAVE REACHED (duplicate of CONQUEST).

**Skill strip (496,912,1000,150), QUIET translucent surface, no ornate frame:**
two groups labelled `ACTIVE` / `PASSIVE` (Secondary, Slate→readable muted), slots 230×120 each, up to
`Build.ActiveCapacity` / `PassiveCapacity` per group. Slot: 72 px hex medallion with the existing cooldown ring
and telegraph; name at Headline 24 (16 px at 720p); second line at Body 19: readiness/cadence word —
`READY` · `2 ACTIONS` · `1.4s` (timed) · `ACTIVE` (Field) · `ON BITE` (Reaction) · `x3` when stacks exist; a
20 px Source glyph in the corner **and** the Source name in the second line's tail (`· MIND`) — colour + glyph +
text (§8). Empty slot: hex at 0.5 with `MORE SLOTS — TRAITS` (Caption). RENAMED: "SKILLS" panel → two group
captions. REMOVED: the rule text (`SkillDef.Line`) — BUILD's job. MOVED: CHARGE → hunter card.
Dependency: the shared guide banner must leave y 912–1062 (§11); the arena ground line moves to ~880 and
`ArenaRect` widens to about (300,150,1500,750) so the freed left column becomes stage.

**Defeat banner (560,190,800,220), replacing the flat fill:**
```
YOUR CHAMPION FELL AT WAVE 47                       (StageLabel 26, display, Ember)
MAIN PRESSURE — <RunReport.Verdict()>                (Body 19)
LIMITING FACTOR — REACH · 1 of 3 creatures per cast  (Body 19)   ← from the same thresholds Verdict() uses
[ADJUST BUILD]   [GEAR]   [READ THE LOG]             (44 px buttons; new host flags WantsBuild / WantsGear)
```
Hold: the banner should stay until dismissed or until the next descent's first wave clears, not 4.5 s.
No BEST PERFORMER line until per-skill damage exists (see §8). No RETRY button — the champion already
restarts; say so in a Secondary line: `THE HUNT RESTARTS IN 2s` only if the designer wants the timer visible.

**Damage feedback:** group Strike events sharing `AtMs` + `FromSkill` into one number `127 × 5` (the
replay already batches per timestamp, `_replay.Advance` :1145); skill-name callouts one rung above crits
(`DamageSkillName` = 48 or fold the skill name into the number line); UNDYING / BOSS DOWN at RegionTitle with a
plate — state > identity > damage > cosmetic (§21).

**Welcome back — a return panel, not a toast:** centred (660,240,600,~380), PRIMARY frame, held until
`[CONTINUE]`:
```
WELCOME BACK                       (PanelTitle 26, display)
AWAY 6h 42m                        (Headline 24)
HUNT      +11,320 Gleam  · 214 waves cleared · fell 3 times · deepest wave 41
WARREN    +7,100 Gleam · +1,240 Dust · +18 Scrap · +2 Essence
[CONTINUE]
```
Every line above is computed today (see §8). Under a tour: queue it after the tour instead of dropping it.

**Expedition Log (keep panel 502,130,1042,800):** add a diagnosis layer between the wall line and the table —
`MAIN LIMIT — REACH` (Headline 24, Gold — this gold means something) then the verdict sentence at Body;
collapse the table to three columns with the POINTS AT text folded under each row name at readable Body;
diff rows at Body 19 with chips at Secondary 16 (not Caption); drop the duplicate "L CLOSES IT". 720p target:
no line under 12.7 px → with UI Scale 125 % at 720p that is Body 19 → ~15.8 px.

---

## 7. Legacy vocabulary reaching the player or naming classes

Player-reaching:
- Game1.cs:5041 `"WOVEN SKILLS"` — help sheet (F1) build column.
- Game1.cs:5042 `"SOURCE x FORM x VOW"` — the deleted composition, stated as the game's rule.
- Game1.cs:5046 `"FORM IS HOW YOU FIGHT"`.
- Game1.cs:5043-5044 `"KEYSTONE SOCKETS"` / `"TRAITS (P) SELL MORE OF BOTH"` — verify against current unlock truth.
- Tutorial.cs:242 `TutorialStep.WeaveBuild => "THE BUILD IS THE GAME"` — the §11 banner; step id says Weave.
- SoloExpeditionScreen.cs:3087 `"+B"` — not legacy, but a key glyph as a slot state (see P2-5).

Class / file names:
- `src/IdleXIdle.Game/WeaveScreen.cs` (`public sealed class WeaveScreen`, :37) — the BUILD screen's class.
- `src/IdleXIdle.Game/FormHexDiagram.cs` (`internal static class FormHexDiagram`, :41) — a *Form* hex chart,
  still drawn live by BuildScreen.cs:1320-1322 and :1393 (BUILD auditor's area; listed because it is a live
  Form artefact, not a comment).
- `SoloExpeditionScreen` — not Form-legacy (`SoloExpedition` is a live Core type) but the screen is HUNT
  everywhere the player sees it.

Persistence names (not player-facing, listed for completeness): Game1.cs:625/631/882 `save.WovenSkills`,
Game1.cs:633 `(string?)s.Form` tuple element.

Comments only in SoloExpeditionScreen.cs (no runtime effect; clean up when touching the code): :314
"each Form's medallion", :789 "woven skills", :834 "A weave, a socket", :848-850 `Form.Aura`, :1246,
:1352-1355 (doc "one effect strip per FORM"), :2290 "a new Form", :2842-2846 (DrawSkillDock doc: "its Form, and
the AUTO state"), :2855 "weave_5 node", :2862, :2883, :2888, :2946, :2990-2991, :3006 "Source+Form label",
:3014, :3037 "The Weave screen explains a Form", :3072, :3252, :3317 (FxFor doc), :3328, :3354, :3638.

---

## 8. Data honesty — what the brief wants shown vs what Core computes

| Display the brief wants | Status | Evidence |
|---|---|---|
| MAIN PRESSURE sentence (defeat, log) | **EXISTS** | `RunReport.Verdict()` RunReport.cs:63-79 |
| LIMITING FACTOR label (REACH / ARMOUR / SUSTAIN / OUT-SCALED) | **EXISTS as thresholds inside Verdict()**; needs a tiny exposed enum, no new telemetry | RunReport.cs:68-77 (`AbsorbedFraction ≥ .45`, reach `< .5×CreaturesPerWave`, `HealthLost ≥ .18`) |
| BEST PERFORMER — skill X n % of damage | **NEEDS telemetry** | `WaveMetrics` has only totals (SoloBattle.cs:105-135: RawDamage, DeliveredDamage, Hits, Activations, TargetsStruck…); `BattleEvent` Strike carries `FromSkill` but no skill slot (WaveModel.cs:314) |
| Before → after vs last run here | **EXISTS** | `RunReport.DiffEntries(previous)` RunReport.cs:88-100; `RunLog.OlderThan` / `PreviousIn` |
| Skill readiness / cadence / next cast | **EXISTS** | `WaveReplay.NextSkillAfter/LastSkillBefore/BeatAt`, `SkillDef.Beats/IntervalMs/RearmMs`, carry :336 |
| Active/Passive grouping | **EXISTS** | `Build.ActiveCapacity/PassiveCapacity/ActiveSlotsFor` Build.cs:301-343; `SkillDef.TakesABeat` |
| Skill's Source (name + glyph) and variation | **EXISTS** | `EquippedSkill(Def, Source, Vow)` Build.cs:217; `SkillProgress.VariationOf(def)` SkillProgress.cs:80 |
| Enemy stacks badge (defence break) | **EXISTS** | `WaveReplay.CreatureBreaks(i)` (:1780) |
| Slow / attack-break stacks | **NEEDS** replay exposure (not in `WaveReplay` today) | — |
| Hunter statuses: shielded / UNDYING / CHARGE | **EXISTS** | `WaveReplay.IsShielded(0)` :1521; `BattleEventKind.Undying` :1275, `Champion.UndyingSpent`; `BattleEventKind.Charge` :1300, `SoloBattle.ChargeCap` |
| POWER / Level / HP | **EXISTS** | `Hunter.PowerRating`, `Hunter.HunterLevel`, `WaveReplay.HealthOf(0)` / `Champion.MaxHealth` (:2334-2341) |
| IDLE +n/min | **EXISTS** (live-measured gleam/s) | Game1.cs:254 → `IdleGleamRate` :2611 |
| REWARDS: n chests ready / n points | **EXISTS** | `ChestCount` (host-fed), `Mastery.Available` :2617-2618 |
| Aggregate multi-hit `127 × 6` | **EXISTS in the event stream** (same `AtMs`, `FromSkill`); grouping is presentation only | :1145-1236 |
| Offline: AWAY span | **EXISTS** | `SaveSystem.CreditedOfflineSeconds` Game1.cs:740 |
| Offline: HUNT gleam, waves cleared, falls, deepest | **EXISTS, unused** | `OfflineHunt.Result(Gleam, WavesCleared, Falls, DeepestWave, …)` OfflineHunt.cs:54-56 |
| Offline: WARREN gleam / dust / scrap / essence | **EXISTS, only gleam shown** | `WarrenYield(Gleam, Dust, Scrap, Essence)` Warren.cs:71; credited Game1.cs:755-758 |
| Offline NOTABLE: skill level-ups | **NOT POSSIBLE by design** — omit honestly | OfflineHunt.cs:30-31 "does NOT level skills (Progress stays null)" |
| Offline NOTABLE: set completed / rare item / n items auto-salvaged | **NEEDS design + telemetry** (offline drops no chests, materials or items) | OfflineHunt.cs:30-33 |
| Empty slot "why locked" | **EXISTS as capacity** (`Loadout.SkillCapacity`, `DustEffects.SkillSlots`); the node name needs a Dust-tree lookup | Game1.cs:625 |

---

## 9. Fixtures

Existing (Game1 `RH_SHOT_MODE`, Game1.cs:1760-1860; `tools/asset-pipeline/capture.sh` modes header):
`fight` (3rd arg = seconds into the wave, `DevSeek`), `fightgear`, `fightswing` (`RH_SHOT_SWING`), `fightreport`
(`DevRunToDeath` + fallen banner), `fightfall` (`RH_SHOT_T`, flash + collapse, banner off), `runlog` (three
descents, log open), `fightfilter` (popover open with a setting), `boss` / `bossdebug` / `corruptedboss`,
`intro` (card n), `tour … Hunt`. The fight fixtures pose: 4 skills (BLOW, SPRAY, PRESS, JAWS), a waiting Epic
chest, 8 mastery points (`_deepestEver = 25`), Umbral Reach swarm, a literal welcome toast.

Gaps against brief §95 ("HUNT: 4 skills, statuses, reward available") and this proposal:
- **Statuses are never posed**: no fixture sets shielded / UNDYING / CHARGE / defence-break stacks on a
  creature. Needs a `fightstatus` mode that seeds a build with a Field + a Reaction + a charge keystone and seeks
  past the first bite.
- **`fight` does not render SPEND POINTS** although it seeds 8 points (`fight.png` shows only OPEN 1 CHEST;
  `fightreport.png` shows both) — the MasteryOpen gate or `Mastery.Available` is not in the state `fight` draws.
- **Welcome-back is a hardcoded string** (Game1.cs:1793). Needs a `welcome` mode that runs
  `OfflineHunt.Simulate` and `Warren.Tick` on a seeded away-time (e.g. 6h42m) so the panel shows real
  `Result`/`WarrenYield` figures.
- **Defeat diagnosis**: `fightreport` should also pose the three verdict branches (armour / reach / sustain) —
  three seeds or three enemy presets — so each diagnosis line is photographed.
- **`runlog` diff is degenerate**: three identical runs → every chip "+0"; a second seed with a different
  loadout is needed so Better/Worse chip colours are exercised.
- **Multi-hit flood** for §21: a `fightvolley` seek to a Volley cast with 5 alive creatures.
- **Boss wave HUD** at 720p with skills firing (the `boss` fixture clears callouts on purpose :1521).
- **5-slot strip** (`Loadout.SkillCapacity = 5`) to prove the strip's ACTIVE/PASSIVE split at max capacity.
- **720p**: `baseline/720/*` are downscales of the 1080 canvas, which is faithful to the present path;
  once UI Scale exists a `RH_SHOT_SCALE=125` variant is needed per §94.
- **Fixture wave consistency**: `DevRunToDeath` leaves `_replayWave` at 1 (header "WAVE 1" under "FELL AT WAVE
  14") — set `_replayWave` in the fixture so header, banner and log agree in the capture.

---

## 10. Summary of REMOVED / MOVED / RENAMED

REMOVED from HUNT: TEMPO line · mastery ADEPT title · Source-gem row · per-skill rule text · DEEPEST WAVE
REACHED · "SKILLS" panel frame · key hints inside button labels · "L CLOSES IT" duplicate in the log footer.
MOVED: CHEST FILTER (row + popover) → Vault · CHARGE pill → hunter status row · skills column → bottom strip ·
verdict sentence → also onto the defeat banner · welcome toast → held return panel.
RENAMED: "SKILLS" → `ACTIVE` / `PASSIVE` group captions · "IDLE RATE" → `IDLE` · "REWARD ACTIVITY" → `REWARDS`
· "OPEN 1 CHEST (K)" → `OPEN VAULT` · power icon → `POWER 222`.
