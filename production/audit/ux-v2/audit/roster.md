# UX V2 audit — ROSTER

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0–15, §30–34, §82–102 and the runtime on
`feat/hunter-cutout-rig` (post 2026-08-31 refactor). Read-only on source. Evidence is `file:line` or a
screenshot observation; measurements were taken off the baseline PNGs with PIL (ink-row cap heights).

Files read in full: `src/IdleXIdle.Game/RosterScreen.cs` (359 lines), `src/IdleXIdle.Core/Characters/
CharacterRoster.cs`, `Character.cs`, `CharacterState.cs`, `src/IdleXIdle.Core/Quests/Quests.cs`; plus the
roster-relevant parts of `Game1.cs`, `UiKit.cs`, `UiTypography.cs`, `Onboarding.cs`, `SkillCatalogue.cs`,
`ItemClasses.cs`, `Career.cs`, `MasteryTree.cs`, `tools/asset-pipeline/capture.sh`.

Screenshots: `baseline/roster.png`, `baseline/rosterlocked.png`, `baseline/720/roster.png`,
`baseline/720/rosterlocked.png`.

---

## 0. One fact that changes every number on this screen

Every menu screen — the roster included — is drawn through `Game1.OverlayTransform`
(`Game1.cs:4028-4029`), a uniform scale of `OverlayScale = (1920 - NavRailWidth - 20) / 1920`
(`Game1.cs:5323`, `NavRailWidth = 180` at `:5305`) = **0.896**. So a `UiTypography` rung is rendered at
89.6 % of its value on the 1080 canvas, and at **0.896 × 0.667 = 0.597** of its value on a 1280×720
display. The brief's ladder (§3) is therefore optimistic for every screen except HUNT:

| Rung | Logical | On 1080 canvas | On a 720p display | Measured cap height 1080 → 720 (roster) |
|---|---|---|---|---|
| ScreenTitle | 36 | 32.3 | 21.5 | "ROSTER" 11 px @720 |
| PanelTitle | 26 | 23.3 | 15.5 | — |
| Headline | 24 | 21.5 | 14.3 | inspector name 12 → **8 px** |
| Body | 19 | 17.0 | 11.3 | card "READY"/"NO ROAD" 9 → **6 px** |
| Secondary | 16 | 14.3 | 9.6 | card name "THE SEEKER" 8 → **5 px**; blurb 10 → ~5 px |
| Caption | 14 | 12.5 | 8.4 | tier line "FIRST OF THE WANDERERS" 7 → **~4 px** (a smear) |

The 720p captures under `baseline/720/` are a bilinear downscale of the canvas — the same path
`DisplaySettings.PresentFit` takes — so the numbers above are what a 720p player sees.

---

## 1. Does the screen answer its §100 question — "Who should I play?"

**Partly.** It answers "who exists, which class they wear, what their innate power is, and what unlocks
them" — a good answer to "who *can* I play". It does not answer "who *should* I play":

- The starting skill — which the brief (§33) calls "an important part of identity" and which Game1
  latches permanently the moment you play a champion (`Game1.cs:3603-3606`,
  `_mastery.LearnSkill(_characters.Active.StartingSkillId)`) — appears nowhere on the screen
  (no reference to `StartingSkillId` or `SkillCatalogue` in `RosterScreen.cs`).
- A conquest-gated locked card says only `LOCKED` (`RosterScreen.cs:258-260` — the progress line is
  only substituted for a *quest* gate), and the inspector says `CONQUER CINDERWORKS` with no count
  (`:119-121`), although the region depth is already in the snapshot the screen receives (see §7).
- The text that would let the player compare two champions is the smallest text on the screen
  (see P0 below), so at 720p the comparison cannot physically be read.

---

## 2. KEEP — what is good and must be preserved

1. **The 2×5 class-column grid** (`CharacterRoster.Grid`, `CharacterRoster.cs:265-284`; walked by
   `RosterScreen.cs:199`) with the FIRST of each class above the SECOND. Data-driven, tested
   (`tests/unit/IdleXIdle.Core.Tests/Characters/roster_grid_test.cs`), and it reads as a map. Brief §30.
2. **Main content + right inspector** shape (`GridPanel` 1180×718 / `DetailPanel` 630×718,
   `RosterScreen.cs:53-54`). This is the §14 pattern already in place.
3. **The class badge strip** on every card — colour + class icon + class word (`RosterScreen.cs:221-228`).
   It is §8-compliant (never colour alone) and answers the 2026-08-26 playtest "I cannot see the
   characters' classes". In the 720p crop the badge is the one card element that still reads at a glance.
4. **Gold means state**: card edge Gold only for the ACTIVE champion, Bone for the selected one, class/lean
   colour for unlocked, Dim for locked (`RosterScreen.cs:210`). `PLAYING` in Gold, `READY` in green
   (`:261-262`). This already obeys §10 — do not dilute it.
5. **Full-body animated portraits**, each breathing on its own phase (`RosterScreen.cs:239-240`). The
   art is the screen's identity (brief §2 "current Hunter art").
6. **Switching is free and unconfirmed** (`Confirm`, `RosterScreen.cs:140-149`; `CharacterState.Select`
   `CharacterState.cs:85-90`). Brief §34 and §85 are already satisfied by the *mechanics*.
7. **Locked cards explain themselves**: the quest demand comes from the quest, not a second copy
   (`UnlockText`, `RosterScreen.cs:116-124`), and quest-gated cards show a live count
   (`Quest.ProgressLine`, `Quests.cs:93-94`; `RosterScreen.cs:259-260`). Brief §32/§83.
8. **New-champion teaching is already contextual**: a toast `X JOINS YOU` + a `NEW` mark on the ROSTER
   rail tile until the roster is opened (`Game1.cs:3587-3591`, `:5280`, `:5426`). This is exactly what
   §12 asks for; the persistent guide banner does *not* appear on either roster capture.
9. **Layout is cursor-driven** in the inspector (`var y = UiKit.BodyTop(...)`, `RosterScreen.cs:289`)
   and frame-aware (`UiKit.TitleTop/BodyTop/PadX/FrameDrop`). The refactor should keep this discipline.
10. **The house typography is obeyed** — every size is a `UiTypography` rung; `tools/check_ui_type.py`
    stays green. The problem is *which* rung, not drift.

---

## 3. P0 — readability and hierarchy

### P0-1 Card text is physically illegible at 720p (and marginal at 1080)
- Card name `c.Name` is drawn at **Secondary 16** (`RosterScreen.cs:242-243`) → 5 px cap height at 720p
  (`720/roster.png`, first card: measured ink rows `(238,5)`). The most important object on the card
  is its smallest bright text. Brief §6: the name is the card's Headline.
- Tier line `c.TierLine` ("FIRST OF THE WANDERERS") at **Caption 14** in a *tinted class colour*
  (`:246-247`) → ~4 px at 720p; in the 3× crop it is a coloured smear. Brief §6: Caption is for tags,
  never a phrase; §7: tinted-down colour reads as disabled.
- Road word and status at Body 19 → 6 px cap at 720p — readable only because they are short.
- Column headers `THE WANDERERS …` at Secondary 16 in class colour (`:193-194`) → ~5 px at 720p
  (`720/roster.png` y≈124: one ink row detected above threshold).
- The `2 / 10` counter at Secondary right-aligned on the title row (`:182-184`) → ~5 px at 720p.

### P0-2 Locked-card text is below any contrast floor
- The road word on a locked card is drawn in `Dim` (0x3A3A44) over the card fill 0x161220
  (`RosterScreen.cs:249`, `:37`) ≈ 1.4:1. In `720/rosterlocked.png` the words `LOOT` / `TEMPO` /
  `ENDURE` / `RESONANCE` on the five dim cards are effectively invisible (see the
  `720_locked_row1_dim` crop). Brief §7: "less important" must not look like "unavailable" — and here
  the *information* (which road) is what is being hidden, not a state.
- The locked portrait tint `new Color(0x2A, 0x28, 0x30)` (`:237`) makes the figure a near-black
  silhouette; at 720p THE CHORUS and THE METRONOME dissolve into the card. Brief §27/§83: locked
  content must stay discoverable. A desaturated ~50 % figure plus a lock glyph keeps it legible.
- The locked class badge at `classColor * 0.45f` with `Bone * 0.8f` ink (`:223`, `:228`) halves the one
  element that still reads.

### P0-3 The inspector's identity text is the wrong rung
- Blurb at Secondary 16 Slate (`RosterScreen.cs:293`) → ~5 px at 720p (not detected above threshold at
  all in `720/roster.png` y≈181). Flavour is Body text, not a caption.
- `ROAD`, `ALWAYS ON`, `EARNED/LOCKED` labels at Secondary 16 (`:300`, `:320`, `:328`) → 6 px at 720p
  (`720/rosterlocked.png`: LOCKED label ink `(363,6)`). Acceptable as *labels* only once the global
  typography pass lifts Secondary; today they are the sub-floor.
- The panel title `WHO THEY ARE` (PanelTitle 26, Gold, `:284-285`) outranks the character's name
  (Headline 24, `:291`) directly beneath it. Two headlines in 60 px, the generic one louder. Brief §6:
  the panel title should be the identity of the panel — here that *is* the name.

### P0-4 The safety line is a sentence at caption size in the header
- `SWITCH FREELY — YOU KEEP SKILLS, TRAITS, GEAR AND THE WARREN` at Secondary 16 Slate, centred at
  y=80 (`RosterScreen.cs:162-163`) → 2 ink rows detected at 720p, ~5 px visual
  (`720_roster_title_sub` crop). The screen's own remarks call this "the line that makes the screen
  safe to use" (`:20-23`) — and it is the least readable line on it. It also repeats on every visit
  (design law 4).

### P0-5 Frame hierarchy is inverted against both the brief and the house rule
- The card grid — a QUIET list surface per §9 — wears the ornate gold `_ui.Panel`
  (`RosterScreen.cs:177`), while the inspector — the focal interactive surface — wears `PanelQuiet`
  (`:268`). `UiKit.PanelQuiet`'s own remarks state the house rule: "Every panel that lives IN a
  screen wears this quiet brown, both columns alike; a screen with one gold column and one brown
  column reads as two screens" (`UiKit.cs:558-562`). In `roster.png` the eye lands on the gold
  filigree of the grid frame first, then the gold `THE ROSTER` title, then the gold `WHO THEY ARE`,
  and only then on the champion. Brief §9/§10.

### P0-6 Almost a third of the screen is empty
- Both panels end at logical y=862 (`:53-54`) = canvas 772; nothing is drawn in the 308 canvas px
  (28.5 % of the height) beneath them in either capture. Inside the inspector, content ends between
  y≈590 (unlocked) and y≈662 (locked, two-line passive) while the button starts at 766 (`:337`) —
  100–175 px of dead space above the primary action. Inside the grid, 52 px of slack under row two.
  Brief §28/§82: do not pack information into tiny labels while the panel is half empty. The guide
  banner, if it survives §11, is drawn in *canvas* space from y ≥ ~930 (`Game1.cs:3142-3147`, height
  58 + 22·lines, bottom margin 26), so the roster can safely grow to logical y=1000 (canvas 896).

---

## 4. P1 — layout and UX

### P1-1 Cards carry five text lines; the brief wants three
Today per card: class badge, name, tier line, road, status (`RosterScreen.cs:227-262`). Brief §31:
class, portrait, name, status/unlock progress, *optionally* one identity label. The tier line and the
road duplicate what the column and the inspector say. See §6 for what moves where.

### P1-2 Conquest-gated cards show no progress
`stateText` is replaced by a count only when `QuestCatalogue.Find(c.Unlock.QuestId)` resolves
(`RosterScreen.cs:259-260`); a `UnlockKind.Conquest` card stays at the bare word `LOCKED`
(`rosterlocked.png`: THE CHORUS, THE METRONOME, THE UNBROKEN) and the inspector says `CONQUER UMBRAL
REACH` with no number (`:119-121`). The data already exists in the snapshot the screen is handed —
see §7. Brief §32/§83.

### P1-3 No progress *bar*; the count is text only
Brief §32 asks for "progress bar, 80 / 100". `UiKit.Bar` exists (`UiKit.cs:798`); the roster never
calls it. The count is Body 19 (`:261`), which at 720p is a 6 px line the eye has to find.

### P1-4 The disabled primary button is a mystery button
For a locked champion the button reads `LOCKED` in the disabled art (`RosterScreen.cs:338-339`;
`rosterlocked.png` bottom right). Brief §29/§83/§84: no disabled mystery button — show the requirement
(`REACH WAVE 50 IN CINDERWORKS FIRST`) or drop the button and let the requirement stand where it was.
For the active champion the button reads `PLAYING` (disabled) — a state shown as a dead control.

### P1-5 Post-action feedback is a stray line
`Confirm` writes `_msg` (`:147`) which is drawn in Gold at `DetailPanel.Bottom - 128` (`:342-343`) —
above the button, in the dead zone, in Body 19, with no fade and no toast. The card's `PLAYING` word
does move (good). Brief §86: a short toast or a value transition; the nav-rail toast channel
(`Game1.PostNotice`) already exists and is what the JOINS YOU message uses (`Game1.cs:3590`).

### P1-6 Inspector order does not follow the shared §14 structure
Today: title → NAME → blurb → ROAD → CLASS+tier → class sentence → ALWAYS ON → passive → EARNED/LOCKED →
demand → button. Brief §14/§33: CATEGORY → NAME → IDENTITY → WHAT IT DOES (starting skill, innate) →
REQUIREMENTS → STATE → [PRIMARY ACTION]. The class sentence "MANY TARGETS. BUILT FOR THE LOOT ROAD.
WEARS RANGER GEAR." (`:316`, from `ItemClasses.Description`) mixes identity, road and gear into one
line, and for THE OATHBOUND it says "BUILT FOR THE LOOT ROAD" one row under `ROAD — NONE — ANY WORKS`
(`roster.png`, right panel) — the class's road and the champion's road contradict on screen.

### P1-7 The inspector's frame choice costs it 56 px of width
`DetailPanel` is 630×718 → aspect 0.877 → `ui_panel_square` (`UiKit.PanelArtKey`, `UiKit.cs:583-588`)
→ `FrameDrop` 28 and `PadX` 68 (`UiKit.cs:600`, `:618-625`), leaving 494 px of content in a 630 px
panel. A taller panel (aspect < 0.82, e.g. 630×880) selects `ui_panel_vertical`: `FrameDrop` 0, `PadX`
40, content 550 px — more room *and* a higher title, for free.

### P1-8 Lean colour is carried by the card edge alone
`edge = … unlocked ? lean : Dim` (`:210`) — a Ranger card with a Loot lean and a Ranger card with no
lean differ only by edge hue. The road *word* on the card is what makes this §8-compliant today; if the
word leaves the card (P1-1), the edge must not be the only carrier — the road belongs in the inspector
as text + colour.

---

## 5. P2 — polish

- **No hover state** on cards (`RosterScreen.cs:126-137` handles clicks only; no `Contains(hit)` in
  `DrawGrid`). Brief §15: hover = quick highlight. A 1-px Bone hairline or +6 % fill on hover.
- **No keyboard grammar**: the only key is the rail hotkey `R` (`Game1.cs:5068`). Arrow keys across
  `CharacterRoster.Grid` and Enter = primary action would make the screen gamepad-complete (§89).
- **`THE ROSTER` panel title duplicates the screen title `ROSTER`** 60 px above it
  (`RosterScreen.cs:158`, `:179`). Remove the panel title; the grid needs none.
- **`2 / 10` counter** — a genuine value but placed as a caption on the title row; fold it into the
  screen header (`2 OF 10 HUNTERS`) at Body or drop it.
- **Currency pills** (Gleam/Dust/Scrap) are chrome on a screen that spends nothing (design law 15).
  Global chrome owner's call; noting it here.
- **Hunter vs Champion**: the roster's tour and toasts say CHAMPION (`Onboarding.cs:367`,
  `Game1.cs:3590`, `:5068`); the STATS panel says HUNTER (`StatsScreen.cs:192`); the brief says
  Hunter throughout (§12, §19, §62). One word, globally.
- **Column header + badge redundancy**: both name the class. Either is enough; the badge is the one
  that survives 720p.
- **`ItemClasses.Description` ends "Built for the RESONANCE road"** while the roster also draws a
  ROAD row — say the road once.

---

## 6. Legacy vocabulary reaching the player, or living on the classes

| # | Where | Text | Verdict |
|---|---|---|---|
| L1 | `Quests.cs:203` (`Demand`), mirrored at `CharacterRoster.cs:162` (fallback `QuestText`), rendered by `RosterScreen.cs:335` via `:122` | "Clear 150 waves with VOLLEY skills **woven**" | Weave vocabulary reaching the player on the roster and wherever the quest gates. `WeaveScreen.cs:691` ("SLOT WOVEN."), `:860` still use the verb live, so this is a BUILD-vocabulary decision the roster inherits; list it, do not fix it locally. |
| L2 | `Onboarding.cs:371-373` (tour card WHO THEY ARE) | "The champion's always-on power, **what it is best at**, and its gear class." | Describes the BEST AT / aptitude row deleted 2026-08-31 (`RosterScreen.cs:304-305`). Player-facing and stale. |
| L3 | `Character.cs:66` | class summary "a fixed appearance, **an aptitude**, and one passive" | Stale class doc; Aptitude is deleted. |
| L4 | `Character.cs:157-190` (`StripKeys`, `GenericClipFor`) | "a character's OWN clip for a **Form**", "per-Form clip"; clip ids `strike/projectile/mark/transformation/aura/trap` are the six Form names | Class-level Form vocabulary. Drives asset keys (`char_<id>_<clip>_strip8_512`), not player text; renaming is an art-pipeline task. |
| L5 | `CharacterRoster.cs:174`, `:201` | "the legacy **Form aptitude**", "the **Mark aptitude**" | Comments only. |
| L6 | `RosterScreen.cs:296` | "Lean **and aptitude**, side by side" | Comment describing a row that no longer exists (see `:304`). |
| L7 | `RosterScreen.cs:338`, `Onboarding.cs:375-377` | button `BECOME THEM` | Not Form-legacy; brief §33 names the action `SET ACTIVE`. Rename (and the tour card with it). |
| L8 | `Onboarding.cs:367`, `Game1.cs:3590`, `:5068` vs `StatsScreen.cs:192` | CHAMPION vs HUNTER | Naming inconsistency, not legacy; settle globally. |

Checked and **not** legacy: "Marks" in TWICE SWORN (`CharacterRoster.cs:199`) — the SIGN skills still
call their amplify effect "the mark" in live text (`SkillCatalogue.cs:429-449`); "DESCENTS" —
`Descent` is a live Core type; "ROAD" — live (brief §44).

---

## 7. Data honesty — what the brief wants shown, and whether Core already computes it

| Display | Status | Source |
|---|---|---|
| Card status PLAYING / READY / LOCKED | **EXISTS** | `CharacterState.ActiveId`, `IsUnlocked` (`CharacterState.cs:41,43`) |
| Quest-gate count `23 / 50 WAVES` and a bar | **EXISTS** | `Quest.Current/Threshold/ProgressLine/IsDone` (`Quests.cs:76-94`) over `QuestProgress` built by `Career.QuestSnapshot` (`Career.cs:35-45`); fraction = `Current/Threshold` — a one-line helper, no telemetry |
| Conquest-gate progress `11 / 20 WAVES IN UMBRAL REACH` | **EXISTS, not displayed** | `QuestProgress.DepthByRegion[regionId]` (`Career.cs:41`) vs `Checkpoints.ConquestWave = 20` (`Checkpoints.cs:29`); `Regions.IsConquered` (`Regions.cs:126`). `RosterScreen.cs:119-121, 258-260` ignore it |
| STARTING SKILL — name, style, active/passive, one line | **EXISTS, not displayed** | `Character.StartingSkillId` (`Character.cs:126`) → `SkillCatalogue.Find` (`SkillCatalogue.cs:633`) → `SkillDef.Name/Style/Kind/Line` (`:150-156`). Nine of the twelve skills are someone's starting skill |
| "Already known" chip on the starting skill | **EXISTS in Core; needs plumbing** | `MasteryTree.LearnedSkills()` (`MasteryTree.cs:308`); RosterScreen receives no mastery reference today (`Draw(b, state, mouse, clicked)`, `:151`) |
| INNATE — name + one line | **EXISTS** | `Character.PassiveName/PassiveText` (`Character.cs:128-129`) |
| GEAR CLASS line | **EXISTS** | `ItemClasses.WearsLine(cls)` (`ItemClasses.cs:275`) — already used on GEAR (`CharacterScreen.cs:455`); `NameOf/PluralOf` (`:138-141`); `UiKit.ClassIcon/ClassColor` |
| ROAD | **EXISTS** | `Character.Lean` (`Character.cs:97`) |
| Tier (FIRST / SECOND of the class) | **EXISTS** | `Character.Tier`, `TierLine` (`Character.cs:114, 147-148`) |
| Roster count `2 / 10` | **EXISTS** | `CharacterRoster.All.Count` vs `IsUnlocked` (`RosterScreen.cs:178`) |
| "A NEW HUNTER HAS JOINED" teaching | **EXISTS** | `CharacterState.Refresh` returns the fresh list (`CharacterState.cs:61-82`); `Game1.cs:3587-3591` toast + `_rosterNews` badge |
| "Fits your build" / recommended champion | **DOES NOT EXIST** | No Core member summarises spend per `Branch` (grep for PointsIn/SpentOn/ByBranch finds only `SkillProgress.SpentOn(skillId)`). Omit per §92; do not invent |
| "N worn pieces would return to the bag if you switch" | **Computable, needs plumbing** | `Game1.ShedUnwearable` does it *after* the switch (`Game1.cs:3599`); a preview needs a host-side query over the loadout + `ItemClasses` wear rule. Not telemetry; omit unless asked |

---

## 8. Proposed layout (1920×1080 logical overlay space; rendered ×0.896)

Structure preserved: 2×5 grid left, inspector right (§30). Both panels grow downward to y=1000 to
reclaim the empty third (P0-6). All sizes are `UiTypography` rungs; the rung *choices* change, the
ladder is the global pass's business.

### Zones
- **Header** `y 0–104`: `ROSTER` ScreenTitle, gold rule (keep `:158-159`). **REMOVED**: the
  `SWITCH FREELY …` sentence (moves to the inspector's state zone). Optional: `2 OF 10` at Body,
  right of the rule.
- **Grid — QUIET surface** `Rectangle(38, 120, 1180, 880)`: `PanelQuiet` or, per §9 QUIET, a plain
  dark translucent plate. **REMOVED**: the `THE ROSTER` panel title and the `2 / 10` caption
  (`:179-184`).
  - Column headers `y 150`: keep *or* drop (P2 redundancy). If kept: Body 19, class colour, with a
    24 px `ClassIcon` left of the word; rule at `y 178`.
  - Cards `208×360`, column pitch 224, row y `192` and `580`; `GridLeft = (1180 − (224·4 + 208)) / 2 =
    38` → card x `76 + 224·col`.
  - Card anatomy (top → bottom): class badge strip 34 px (26 px icon + class word at Body 19, full
    colour even when locked); portrait `176×190`; **name at Headline 24 Bone** (Slate when locked);
    **identity label** = `PassiveName` at Body 19 Slate (the brief's "short passive identity label";
    unique per champion, unlike road or tier); **status zone** 56 px: unlocked → `PLAYING` Gold /
    `READY` green chip (word + colour); locked → `UiKit.Bar` 10 px tall × 176 px + `23 / 50 WAVES`
    Body 19 Bone, or `CONQUER CINDERWORKS` / `11 / 20 WAVES · CINDERWORKS` for a conquest gate.
  - Edges: 3 px; selected Bone 4 px, active Gold 4 px (keep `:210-215`); otherwise class colour ×0.6
    unlocked, Slate-dim locked. **REMOVED from the card**: tier line (`:246-247`), road word
    (`:248-254`). Locked portrait: desaturated ~50 % grey, not 0x2A2830; add a small lock glyph in the
    badge's right end.
- **Inspector — SECONDARY (quiet bronze) frame, the focal one by content** `Rectangle(1250, 120,
  630, 880)` → aspect 0.716 → `ui_panel_vertical`, `PadX 40`, content `x 1290–1840` (550 px). The
  §14 stack, cursor-driven as today:
  1. `y 142` CATEGORY: `ClassIcon` 24 px + `WANDERER · FIRST OF THE WANDERERS` Secondary, class colour
     (**MOVED** here from the card's tier line and the old `SECOND OF THE RANGERS` right-aligned value).
  2. `y 176` NAME: PanelTitle 26 Bone — the panel's title *is* the name. **REMOVED**: `WHO THEY ARE`.
  3. `y 214` IDENTITY: `Blurb` at Body 19 Slate (was Secondary).
  4. `y 250` rule · **STARTING SKILL** (new): label Secondary Slate; `BLOW · HAMMER · ACTIVE` Body Bone
     (style word + `SkillKind` word, so active vs passive reads without jargon); `SkillDef.Line` Body
     Bone wrapped; optional chip `ALREADY KNOWN` (Caption tag, green) when in `LearnedSkills()`.
  5. `y ≈380` rule · **INNATE** (**RENAMED** from `ALWAYS ON`, per §33; `INNATE — ALWAYS ON` if the
     single word is felt to be jargon): `PassiveName` Body Bone; `PassiveText` Body Bone wrapped.
  6. `y ≈500` rule · **ROAD**: `TEMPO ROAD` / `ANY ROAD` Body in branch colour *with the word* (§8) ·
     **GEAR**: `WearsLine` Body Bone (**REPLACES** the `Description + " Wears X gear."` sentence at
     `:316`, which contradicts the ROAD row for THE OATHBOUND).
  7. `y 640–880` STATE / REQUIREMENT zone: unlocked → `READY · SWITCHING IS FREE — SKILLS, TRAITS,
     GEAR AND THE WARREN STAY` Body Slate (**MOVED** from the header; it now sits beside the action it
     de-risks); active → `YOU ARE PLAYING THIS HUNTER` Body Gold; locked → `LOCKED` Secondary Ember,
     full-width `UiKit.Bar` 14 px, count at **PrimaryValue 30** Gold (`23 / 50`) + unit Body, demand
     sentence Body Bone (`REACH WAVE 50 IN CINDERWORKS`), conquest gate → `11 / 20 WAVES IN
     CINDERWORKS` from `DepthByRegion` and `ConquestWave`.
  8. `Rectangle(1290, 900, 550, 68)` PRIMARY ACTION: `SET ACTIVE` (**RENAMED** from `BECOME THEM`).
     Active champion → no button; the Gold state line stands. Locked → no disabled `LOCKED` button; the
     requirement sentence occupies the row (or, if a control is wanted for tour targeting, a disabled
     button whose label *is* the requirement: `REACH WAVE 50 FIRST`).
  - Feedback (§86): on click, `Game1.PostNotice("YOU ARE THE ANVIL", …)` through the existing toast
    channel, the card's `PLAYING` chip moves, and the inspector state line crossfades. **REMOVED**: the
    in-panel `_msg` line at `Bottom − 128` (`:342-343`).
- **Interaction grammar** (§15): hover = hairline highlight on the card; click = select (inspector
  crossfade); primary button = switch. Keyboard: Left/Right/Up/Down across `CharacterRoster.Grid`,
  Enter = `SET ACTIVE`, Escape = back to HUNT.

### States
- **Empty**: not applicable — the roster always has ten (the starter is never locked,
  `CharacterState.cs:29-32`).
- **Locked**: as above — desaturated portrait, full-colour badge, bar + count on the card, requirement +
  bar in the inspector, never a bare `LOCKED`.
- **Selected**: Bone edge (keep). **Active**: Gold edge + `PLAYING` (keep).
- **Disabled**: only the *portrait* dims; every text on a locked card is Slate or class colour, never
  `Dim` (P0-2).

### Tour cards (Onboarding.cs:365-378)
Card 2's body must drop "what it is best at" (L2) and mention the starting skill; card 3 renames to
`SET ACTIVE`. `RosterScreen.Spotlights` (`:60-66`) needs the new `DetailPanel` and button rectangles.

---

## 9. Fixtures

### Existing
- `RH_SHOT_MODE=roster` (`Game1.cs:2134-2145`): every region conquered, every quest completed, THE ANVIL
  active, THE OATHBOUND selected → the unlocked/active/`BECOME THEM`-enabled state (`baseline/roster.png`).
- `RH_SHOT_MODE=rosterlocked` (`Game1.cs:2146-2158`): Verdant Hollow + Cinderworks conquered (depths
  34 / 23), 12 chests, 1 vow-kept run, THE QUIVER selected → active (Seeker) + unlocked-not-active
  (Anvil) + three conquest-locked + five quest-locked cards with counts (`baseline/rosterlocked.png`).
  This already covers §95's "active + unlocked + quest-locked".
- `capture.sh tour <out> Roster <1..3>` poses the three tour cards over the roster fixture
  (`capture.sh:32-40`). Note: `capture.sh`'s mode comment (`:12-14`) does not list `roster` /
  `rosterlocked` although `Game1.cs:1490` accepts them — a stale comment.
- `RosterScreen.DevSelect(id)` and `DevRosterDebug` (`:48-50`) exist for posing.

### Gaps for §95 / the proposal
1. **Live counts on all five quest gates**: `rosterlocked` shows `0 / 40 BOSSES` and `0 / 150 WAVES`
   because `_bossesFelled` only seeds from chests on *save load* (`Game1.cs:674`) and no `SkillProgress`
   uses are posed. Pose `_bossesFelled` and a `WavesBySkill` entry for a VOLLEY skill so every count is
   non-zero.
2. **A conquest region mid-progress** (e.g. `umbral_reach` best depth 11) so the new
   `11 / 20 WAVES IN UMBRAL REACH` line and card bar can be photographed.
3. **Post-switch feedback frame**: nothing poses the toast/state transition after `SET ACTIVE`; a
   `DevConfirm()` on the screen (or `RH_SHOT_T`-style delay) is needed once feedback moves to a toast.
4. **Hover frame** on a card once hover exists.
5. **720p evidence**: keep producing `baseline/720/*.png` as bilinear downscales of the canvas (that is
   the `PresentFit` path), and add a 1440p upscale if §94's third row is attempted.
6. The tour fixture must be re-shot after the inspector rectangle changes, since
   `RosterScreen.Spotlights` is the light's source of truth.

---

## 10. Decisions for the owner

1. **"woven" (L1)** — a BUILD-vocabulary decision; the roster only inherits the quest demand.
2. **Hunter vs Champion (L8)** — one word across roster, tour, toasts, STATS.
3. **Column headers vs badges** — keep one; the badge survives 720p, the header groups the pair.
4. **Card identity label** — passive name (proposed) or road word; the brief allows either.
5. **INNATE vs ALWAYS ON** — the brief's word vs the plainer phrase; the no-jargon memory favours
   `INNATE — ALWAYS ON` on the label row.
