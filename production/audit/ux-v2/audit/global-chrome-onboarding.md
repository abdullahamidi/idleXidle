# UX V2 audit — GLOBAL CHROME AND ONBOARDING (nav rail, pills, settings, help, banners, toasts, tours)

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0–15, §35–37, §82–102 and the current
runtime (STYLE → SKILL → VARIATION → REINFORCEMENTS with `SkillId` identity; Form / WovenAbility /
Aptitude / Source×Form deleted from Core; Warren pays Gleam / Dust / Scrap / Essence; mastery skill
discovery permanent).

Source read: `src/IdleXIdle.Game/Game1.cs` (chrome: `DrawHexNav` 5351–5455, `DrawCurrencyPills`
4847–4900, `DrawSettingsGear` 4798–4834, `DrawSettings` 4358–4540 + `DrawSettingsTips` 4740–4795,
`DrawHelp` 5023–5108, `DrawGuideBanner`/`DrawGuideStrip`/`GuideBannerRect` 3109–3150,
`DrawScreenBanner` 3240–3266, `DrawLockedToast` 3184–3196, `DrawNoticeToast` 3271–3295,
`DrawBootToast` 3826–3847, tour system 3307–3453, tour scheduling 2240–2280, input 2360–2545, facts
2919–2944 and 3007–3018, `ShotMode`/`SeedExplained` 462–486 and 1248–1277);
`src/IdleXIdle.Core/Progression/Onboarding.cs`, `Tutorial.cs`, `Unlocks.cs` in full;
`UiTypography.cs`, `UiKit.cs` (Pill 822, Button 873, CloseRect 1023, HoverTip 1168),
`DisplaySettings.cs` (GamePrefs 156), `CanvasFit.cs`; `tools/asset-pipeline/capture.sh`,
`tools/check_nav_gates.py`; tests `tests/unit/IdleXIdle.Core.Tests/Progression/*`.
Pixels: `baseline/settings.png`, `help.png`, `fight.png`, `map.png`, `stats.png` and their
`baseline/720/` copies.

All coordinates are 1920×1080 logical. A 720p size is the rung × 0.667 (bilinear downscale):
36→24 · 30→20 · 26→17.3 · 24→16 · 21→14 · 19→12.7 · 16→10.7 · 14→9.3 px.

---

## 0. Does the area answer its questions today?

Brief §100 gives the chrome one question — SETTINGS: **"How should the game behave and present
itself?"** — and §11–12 give onboarding a rule: teach the screen the player is on.

- **Settings**: answers the question functionally (every control is real and persisted,
  `DisplaySettings.cs:156`), but flat — no DISPLAY header, START A NEW GAME sits as an equal twin of
  COPY FEEDBACK CODE (`Game1.cs:4450`, `:4475`; `settings.png` y=800–852), labels are 12.7 px at 720p.
- **Onboarding**: the strip teaches the *ladder*, not the *screen*. `map.png` shows
  "YOU HAVE GLEAM TO SPEND — Press V for STATS" over the MAP; `stats.png` shows
  "CHESTS ARE WHERE ITEMS COME FROM — press K for the VAULT" over STATS. That is the §12 violation
  verbatim, produced by `DrawGuideBanner` drawing the current rung as chrome on every screen
  (`Game1.cs:3109–3115`, comment at `:3092–3101` explains it was made global on purpose).
- **Nav rail**: yes — stable position, icons, labels, selected frame, locked dim state, NEW mark,
  chest badge (`Game1.cs:5351–5455`). Brief §13 says keep it; this audit agrees.

---

## 1. Inventory — the shared chrome today (measured from code)

| Element | Rect / size (1080) | Type rung → 720p px | Code |
|---|---|---|---|
| Nav rail shelf | (0,0,180,1080) opaque `#0C0916`, 3 px gem seam at x=177 | — | Game1.cs:5362–5364 |
| Nav tile ×11 | (0, i·98, 180, 98); icon 38 px at Y+24; label at Bottom−34 | NavigationLabel 21 → 14 | :5343–5347, :5391–5406 |
| Locked tile | icon `White*0.22`, label `NavLabel*0.35`; requirement on hover only, shortened to 156 px | Secondary 16 → 10.7 | :5395, :5406, :5410–5412 |
| NEW mark | (X+10, Y+14, 50, 26) gold plate | Secondary 16 → 10.7 | :5422–5433 |
| Vault chest badge | (Right−54, Y+16, 38, 30) red | Secondary 16 → 10.7 | :5443–5450 |
| Currency pills ×3 | right edge 1826, y=16, h=60, ornate `ui_panel_small` stretched; icon 40 | Label 19 → 12.7 | :4869–4873, UiKit.cs:822–856 |
| Pill hover tip | flat plate at y=84, right-aligned to pill | Label 19 → 12.7 | :4891–4898 |
| Settings gear | (1842,16,60,60), 52 px medallion; hover label "SETTINGS — ESC" | Label 19 | :4343, :4805–4811 |
| Settings modal | Panel (335,100,1250,880) ornate gold; content column x 536–1384 | title PanelTitle 26 → 17.3; labels Label 19 → 12.7; tips Secondary 16 → 10.7 | :4214, :4361–4366, :4740 |
| Help modal | Panel (112,92,1696,840); two columns at x 176/356 and 1140/1256; row pitch 38 | Label 19 → 12.7 | :5028–5108 |
| Guide strip | (470, 1080−h−26, 980, 58+22·lines) → two-line body = (470,952,980,102) | title Body 19 → 12.7; body Secondary 16 → 10.7 | :3142–3147, :3121–3130 |
| Screen (slot-note) banner | (NavRail + 270 = 450, 86, 1200, 58+22·lines) | same as strip | :3229–3234, :3246 |
| Boot toast | (630, ToastTop, 560, 88) PanelQuiet; ToastTop = 176 on menus, header-stack+8 on HUNT | OverlayTitle 24 → 16; OverlayBody 19 → 12.7 | :3841–3847, :3852 |
| Notice toast | (560, ToastTop[+96], 800, 96) PanelQuiet, 6 s, queued one at a time | same | :3281–3287, :226 |
| Locked toast | (510, 96, 900, 62) flat fill, 3 px gem rule, 3.2 s | OverlayBody 19 → 12.7 | :3189–3195, :2526 |
| Tour card | 560 wide beside the spotlight; scrim 0.74 | title Headline 24 → 16; body Body 19 → 12.7 | :3430–3448 |

---

## 2. The guide banner today — exactly when and where it shows

**What decides it.** `_guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide)` every frame
(`Game1.cs:3538`). `Tutorial.StepFor` (Tutorial.cs:168–195) walks the ladder Watch → SpendGleam →
MeetABoss → OpenChest → EquipItem → WeaveBuild → Conquer and returns the first rung that is neither
satisfied nor dismissed; `IsReady` (Tutorial.cs:209–215) hides SpendGleam until Gleam ≥ 25 and
EquipItem until an item is owned. `Done` requires **all three** of RegionsConquered ≥ 1, ItemsWorn ≥ 1
and SkillsWoven ≥ 1 (Tutorial.cs:182), where SkillsWoven is `BuildDiffersFromStarter()`
(Game1.cs:2932, 2939–2944) — a player content with the starter Hammer Blow keeps the WeaveBuild rung
("THE BUILD IS THE GAME", Tutorial.cs:242) **indefinitely**, on every screen, until they click its ×.

**Where it draws.** `DrawGuideBanner` (Game1.cs:3109–3115) returns only for title / help / settings /
an active tour. It does **not** check `_expedition.LogOpen` or `_forge.RevealActive`, so the strip
also sits over the Expedition Log and over the chest-reveal overlay (draw order Game1.cs:4124–4131:
reveal → help → settings → boot toast → **guide banner** → screen banner → toasts → tour). Compare
`ScreenBannerShowing` (Game1.cs:3039–3043), which does suppress itself over the Log.

**On which screens.** All eleven. Evidence: `map.png` (SpendGleam rung over MAP — the fixture has
131.9M Gleam and no trained rank), `stats.png` (OpenChest rung over STATS — deepest 23, no items).
`fight.png` shows none only because the fight fixture's facts satisfy the early rungs and the
WeaveBuild rung is not reached (four skills woven).

**Size and place.** 980×102 at the bottom centre, 26 px above the canvas edge (Game1.cs:3146). On
HUNT it covers x 470–1450 × y 952–1054, i.e. the arena floor and the champion's feet (ChampBox
420–820 × 570–1000 per the HUNT audit). On MAP it sits below the map panel; on STATS below the reset
bar. It is 9.4 % of canvas height on every screen.

**Dismissal.** The × (32×32 at Right−42, Y+10; Game1.cs:3150) dismisses *that rung only* and saves it
(`_dismissedGuide`, Game1.cs:2432–2439; persisted as `DismissedGuideRungs`, :924). The next rung
appears the moment its `IsReady` holds. A player can therefore be asked to close up to seven strips
over the first hour, each one re-appearing on the next screen they open. There is no "stop teaching
me" and no memory of *having read* a rung — only of having closed it.

**The tour's promise.** The intro's last card (Onboarding.cs:213–215, target `GuideStrip`) says
"When there is something new to do, a short lesson appears down here", and `DrawTour` draws the Watch
strip in that light (Game1.cs:3424). Any replacement must rewrite this card and re-target it.

**What is already right about it.** The ladder is derived, never stored (Tutorial.cs:139–160); every
rung is gated on a fact the player produced; a rung may not depend on a resource a later rung
explains (Tutorial.cs remarks, and `test_every_step_is_displayed_on_a_real_career`); `Sends()`
(Tutorial.cs:312–325) lets a test prove the guide never names a locked door; OpenChest's body changes
when a chest is actually held (Tutorial.cs:292–298). Keep the engine; change the *rendering policy*.

---

## 3. P0 — readability / hierarchy

**P0-1. Every chrome sentence is 10.7–12.7 px at 720p.** `720/settings.png`: MODE, WINDOW SIZE,
EFFECTS VOLUME, "80%", ASK BEFORE SELL OR SALVAGE ≈ 12–13 px; `720/help.png`: all 22 rows and both
paragraphs ≈ 12.7 px, the dimmed second paragraph at `Bone*0.7` (Game1.cs:5105) reads as disabled;
`720/map.png` guide-strip body "Every wave pays Gleam. Press V for STATS…" ≈ 10.7 px (Secondary,
Game1.cs:3128). Hover tips in settings are Secondary too (UiKit.cs:1170–1184). The brief's §5 target
is Body ≈ 24–26 logical (16–17 px at 720p) and Secondary ≈ 20–21.

**P0-2. The strip teaches the wrong screen (§11–12).** `map.png` bottom: "YOU HAVE GLEAM TO SPEND /
Press V for STATS" over the MAP; `stats.png` bottom: "CHESTS ARE WHERE ITEMS COME FROM / press K for
the VAULT" over STATS. Cause: Game1.cs:3092–3115 renders the ladder's current rung as global chrome.

**P0-3. The strip is persistent and can be indefinite.** Tutorial.cs:182 (`Done` needs SkillsWoven ≥
1) + Game1.cs:2939–2944 (starter build ⇒ 0). "THE BUILD IS THE GAME" shows on all eleven screens
until dismissed by hand — the exact banner §11 names.

**P0-4. Destructive action beside a harmless utility (§37).** `settings.png` y=800–852: COPY FEEDBACK
CODE (536,800,400,52) and START A NEW GAME (984,800,400,52) are the same button, same row, same
weight (Game1.cs:4237, :4240). The armed state is a red bar with an 11-word sentence at Label size
(:4461–4464) — no explanation of *what* is lost (Warren, traits, champions), only that "this deletes
your save".

**P0-5. Secondary looks disabled in the settings (§7).** Slider percentages are `Slate`
(Game1.cs:4733); WINDOW SIZE's label goes `Slate` when genuinely disabled (:4389). Same colour, two
meanings, one panel. `720/settings.png`: "80%" and a disabled label would be indistinguishable.

**P0-6. Help sheet copy is the retired model (§0, §66).** `help.png` left column: "1 WOVEN SKILLS",
"SOURCE x FORM x VOW", "FORM IS HOW YOU FIGHT", "SOURCE VS REGION" (Game1.cs:5041–5047). This is the
one sheet a lost player opens (F1), and it describes a composition Core no longer has.

---

## 4. P1 — layout / UX

**P1-1. Settings has no DISPLAY or GAMEPLAY header and mixes purposes.** Rows run MODE, WINDOW SIZE,
[rule] SOUND, [rule] ASK BEFORE SELL OR SALVAGE (no header), [rule] FIGHT TEXT AND EFFECTS, [rule]
utilities (Game1.cs:4378–4478). Two of five groups are headed; the first group — the one the brief
wants UI Scale in — has none.

**P1-2. Settings controls are small for a 1250×880 modal (§35).** Toggle buttons 108×44
(Game1.cs:4711); slider tracks 400×30 with a 14 px handle (:4223–4226, :4732); dropdown 524×58;
column of controls ends at x=1384 leaving ~160 px of empty panel on the right and ~110 px under QUIT.
`settings.png`: the eye lands on the two gold dropdown frames, not on any group.

**P1-3. The strip draws over the Expedition Log and the chest reveal.** `DrawGuideBanner`
(Game1.cs:3109–3115) lacks the `LogOpen` / `RevealActive` guards that tours and the slot-note banner
have (:2251, :3042). A lesson about STATS over the log the player opened to think is the §12 failure
in its worst place.

**P1-4. Currency pills wear ornate frames but are QUIET content (§9).** Three stretched
`ui_panel_small` capsules (UiKit.cs:836) at the top-right of every screen; on `map.png` they sit
above the inspector's ornate frame and the map's ornate frame — five gold frames in one quadrant, so
the region inspector (the decision surface) does not win. Identity is icon + colour only (§8); the
name appears only on hover (Game1.cs:4874–4898).

**P1-5. Three toast shapes for one job (§90 `NotificationToast`).** Locked toast: flat band + gem
rule at y=96, 900 wide (Game1.cs:3189–3195). Boot toast: PanelQuiet 560×88 (:3841). Notice toast:
PanelQuiet 800×96 (:3282). Same class of message, three widths, two frames, two y positions; the
locked toast at y=96 overlaps the slot-note banner's y=86 band on BUILD.

**P1-6. Locked tiles are near-invisible at 720p.** Icon at `White*0.22`, label at `NavLabel*0.35`
(Game1.cs:5395, :5406). `720/fight.png`: GEAR's icon is a smudge; the label is legible only because
the word is known. The "why" is hover-only and shortened to 156 px at Secondary (:5410–5412) — "Earn a
chest from a boss" is ~190 px at 16 px and will ellipsize; the click-toast (OpenNav, :5259–5266) is
the real answer, which §83 accepts, but the rail itself does not say it.

**P1-7. Two hotkeys, one screen, and Help lists it as a riddle.** "T — BUILD, THE SAME PLACE AS B"
(Game1.cs:5062). T toggles the weave (:2517–2536) which *is* BUILD now (OpenNav case 3 opens
`_showWeave`, :5256–5300). Either drop the T binding or list "B or T".

**P1-8. Tour cards and NEW marks are at the wrong rungs for 720p.** Card body Body 19 (Game1.cs:3437)
→ 12.7 px; NEW mark "NEW" in a 50×26 plate at Secondary (:5432) → 10.7 px; Vault count at Secondary
(:5449) → 10.7 px. `720/fight.png`: the red "1" on VAULT is legible, "NEW" would not be.

**P1-9. Roster news is session-only.** `_rosterNews` (Game1.cs:218) is set from `Refresh` during play
and never saved; the first frame after load is absorbed (`_rosterBaselined`, :3585–3592), so a
champion who joined while the game was closed gets no mark and no toast. (`SaveUnlocked` does bank
the unlocked set — :912 — so the stale comment at :3577–3580 is wrong; only *seen* is missing.)

---

## 5. P2 — polish

- BUILD stamp at (380,1000) is outside the settings panel (Game1.cs:4497; `settings.png` bottom-left).
- Settings hover tip delay 0.35 s (:4285) is fine; the tip text is Secondary — raise with the ladder.
- The gear's hover caption "SETTINGS — ESC" (:4811) is the only on-screen hotkey hint in the chrome;
  the rail shows none of its eleven letters — a 14 px key glyph in the tile corner would teach the
  help sheet's whole right column for free.
- `Unlocks.SkillSlotNote(4)`: "The full weave" (Unlocks.cs:254) — one stray "weave" reaching the BUILD
  slot-note banner.
- Help's F1 row says "CLOSE" while Esc also closes; the sheet omits that Esc closes it.
- The pill dev-warning threshold `leftEdge < 1210` (Game1.cs:4874) is a Debug.WriteLine, fine.
- `DrawSettingsGear` hides under help/settings (:4800) — correct; the rail does too (:5353).

---

## 6. What is GOOD and must be kept

- **The rail** (brief §13): opaque shelf, 11 fixed tiles that never re-flow (Game1.cs:5372–5376
  remarks), `ui_tab_active` + purple tint for the lit tile only (:5378–5385), quiet inactive tiles,
  gold NEW mark left / red chest count right (:5422–5450), `check_nav_gates.py` keeping `Nav` and
  `NavActivity` parallel. Icon size derived from tile height (:5391–5395).
- **Every hotkey goes through `OpenNav`** (Game1.cs:2470–2490): one gate, one refusal toast, one
  flag-clear. A locked tile *says why* on click (OpenNav :5259–5266; the T key :2521–2526) — §83 done
  right.
- **Unlocks are derived and monotone** (Unlocks.cs:96–170): no stored flags, gates open on things the
  player did, `Requirement()` and `Headline()` give the toast its words.
- **The tours**: spotlight + 560 px card, click-through, once per screen, screen keeps running
  underneath (Game1.cs:3405–3453; Onboarding.cs:180–360). Copy is plain, ≤160 chars, tested
  (`test_every_card_is_plain_and_short`, `test_no_two_screens_share_a_target`). Facts read from rules
  (`Unlocks.Requirement`, `RegionLadder.StepPercent`, `GemCraft.IsFirstGemFree`).
- **The explained list** as the single memory for tours, slot notes and the gem lesson
  (Onboarding.cs:451–520) — the contextual model below reuses it unchanged.
- **The tutorial ladder's discipline** (Tutorial.cs): derived, ordered by the career, ready-gated,
  skippable by playing, tested against a simulated career. Keep the engine, change where it renders.
- **Settings mechanics**: dropdown as a `Field` with a drawn chevron and a scrolling list
  (Game1.cs:4557–4700), sliders that persist on release (:4413–4419), two-click armed reset with
  disarm-on-any-other-click (:4459–4478), one-sentence hover tips (:4740–4795), Esc opens settings
  (:2289–2301) and the vault/filter popovers outrank it. Title screen shares the modal (:4063).
- **Toast rules**: never modal, never read input (Game1.cs:3273–3277), one notice at a time on a queue
  (:2308–2313), boot toast hidden under a tour (:3831), fixed two-line welcome-back (:3835–3847).
- **PanelQuiet for toasts, gold only for modals** — the brown-frame standard is honoured here.

---

## 7. Proposal A — contextual teaching (replaces the persistent strip)

### A.1 Policy

1. **A lesson renders only on the screen it is about.** `Tutorial.Sends(step, facts)` already names
   that screen (Tutorial.cs:312–325). Watch / MeetABoss / Conquer are about the fight → HUNT only.
   SpendGleam → STATS only; OpenChest → VAULT only (HUNT while no chest is held — that rung is the one
   allowed to speak during a wait); EquipItem → GEAR only; WeaveBuild → BUILD only.
2. **Elsewhere the rung becomes a rail mark, not a banner.** The tile `Sends()` points at gets the
   existing gold NEW mark (Game1.cs:5422–5433) while the rung is current and ready. `Onboarding.IsNew`
   grows one clause: `|| Tutorial.Showing(f, dismissed) is { } s && Tutorial.Sends(s, f) == screen`.
3. **A lesson is "learned" by doing or by closing, once.** Unchanged (`_dismissedGuide`), plus: closing
   a rung on its own screen also marks it — today closing is the only memory, and that stays.
4. **HUNT keeps the first three fight lessons as its initial onboarding** (§11 allows this), one at a
   time, in the toast slot under the header stack, not across the arena floor.
5. **Per-screen hints from real state replace the ladder after onboarding.** One line under the screen
   title, dismissable, and it *disappears on its own* when the fact stops being true. No hint is a
   paragraph; Caption never carries a sentence (§6).

### A.2 The hint per screen, and the Core fact it reads (data honesty §92)

| Screen | Hint line | Reads (EXISTS unless marked) |
|---|---|---|
| MAP | `A NEW REGION IS AVAILABLE — CINDERWORKS` | `World.IsUnlocked(id)` && `!World.IsConquered(id)` && `World.RegionFarm(id).BestDepth == 0` (Regions.cs:129, :126, :124; `Region.BestDepth` read at Game1.cs:3499). "Never hunted there" is derivable; no telemetry. |
| TRAITS | `YOU HAVE 1 TRAIT POINT` | `MemoryDustTree.Available` (MemoryDust.cs:144) — `_dust.Available`. |
| MASTERY | `YOU HAVE 4 MASTERY POINTS` | `MasteryTree.Available` (MasteryTree.cs:166). |
| BUILD | `A NEW SKILL SLOT IS AVAILABLE` | `Unlocks.SkillSlots(f) > _loadout.Skills.Count` (Unlocks.cs:234–240, PlayerLoadout.cs:46); the slot *notes* already exist as `Onboarding.BannerFor` + `SlotKey` (Onboarding.cs:505–512, :451). Keep them; shorten to one line. |
| ROSTER | `A NEW HUNTER HAS JOINED — WARDEN` | `CharacterState.Unlocked` (CharacterState.cs:92) is banked (`SaveUnlocked`, Game1.cs:912). **"Seen" is not** — add an explained-list key `Champion:<id>` written when the roster is opened (same mechanism as `SlotKey`, no new telemetry, one save field already exists). Replaces the in-memory `_rosterNews`. |
| VAULT | `2 CHESTS ARE WAITING` | `_forge.UnopenedChests.Count` (already the badge, Game1.cs:5431). |
| STATS | `YOU CAN TRAIN MIGHT FOR 108 GLEAM` (or the cheapest affordable) | `HunterProgression.CanTrain(stat)` / `NextRankCost(stat)` (HunterProgression.cs:444–455). |
| WARREN | `AN UPGRADE IS AFFORDABLE — NURSERY` | `Warren.CanUpgrade(kind, gleam, dust)` (Warrens/Warren.cs:300). |
| GEAR | `AN UNWORN ITEM MAY BEAT WHAT YOU WEAR` | **NEEDS a per-slot compare** — gear.md owns whether an honest comparison exists; do not ship this line without it. |
| FORGE | none | Nothing in Core says "this item wants work"; §99 law 14. |
| HUNT | onboarding lessons only (Watch / MeetABoss / Conquer), then nothing | `Tutorial.Showing` as today. |

All of the above are a pure function of state the host already holds → put it in Core as
`Onboarding.HintFor(Activity, HintFacts) : ScreenHint?` beside `BannerFor`, with a `HintFacts`
record (RegionsNewlyOpen, TraitPointsFree, MasteryPointsFree, EmptySkillSlots, UnseenChampions,
ChestsWaiting, AffordableTraining, AffordableUpgrade). Testable exactly like `BannerFor`
(`test_only_the_build_screen_ever_owes_a_banner` becomes a table test).

### A.3 Zones (1920×1080)

- **Hint slot on menu screens**: `(450, 86, 1200, 48)` — the slot-note banner's x/y today
  (Game1.cs:3229–3234), one line, gold 5 px left rule, title at the raised Body rung, × at
  `GuideCloseRect`. Screens keep y 86–134 free of interactive content (MAP's panel starts at ≈130 and
  STATS' at ≈140 already — `map.png`, `stats.png`). Multi-line slot notes (`SkillSlotNote`) stay
  allowed here on BUILD only, as today.
- **Hint / lesson slot on HUNT**: the boot-toast rect `(630, HeaderStackBottom + 8, 560, 88)`
  (Game1.cs:3841, :3852) — under the stage header, over nothing the player watches. The three fight
  lessons render here as a two-line PanelQuiet card with ×; they yield to the boot and notice toasts
  (queue, not stack).
- **Rail mark**: unchanged `(X+10, Y+14, 50, 26)`; raise its text to Body.
- **REMOVED**: `GuideBannerRect` at `(470, 952, 980, ~102)` on every screen. **REMOVED**: the strip over
  the Log / reveal. **MOVED**: SpendGleam / OpenChest / EquipItem / WeaveBuild bodies to their own
  screen's hint slot. **RENAMED**: the intro's last card "LESSONS … appears down here"
  (Onboarding.cs:213–215) → "When a screen has something new for you, its tile wears a gold NEW mark,
  and the screen says what it is at the top." and `TourTarget.GuideStrip` → `TourTarget.NavRail`
  reuse (or a new `HintSlot` target pointing at (630, HeaderStackBottom+8, 560, 88)); `DrawTour`'s
  `DrawGuideStrip(TutorialStep.Watch)` at Game1.cs:3424 goes with it.

### A.4 What is REMOVED / kept from `Tutorial`

Nothing in Core is removed. `Tutorial.Title/Body` stay (they become the HUNT lesson card's and the
per-screen hint's words); `Sends` gains a second job (which tile to mark). The Watch rung's body
("You are here to decide WHAT it is, not to swing for it") is a good first sentence and keeps its
place as the first thing after the intro.

---

## 8. Proposal B — settings regrouped (§35–37), same modal

Keep `SettingsPanel (335,100,1250,880)`, ornate gold (a modal — the one place gold framing is the
rule), title "SETTINGS" at `ModalTitleTop`, × at `CloseRect`. Lay the body out in **two columns**
so each control can be bigger and the danger zone can be its own surface.

| Zone | Rect | Contents |
|---|---|---|
| Left column | (380, 190, 620, 690) | **DISPLAY** header (Secondary-as-gold-caps → raise to Body): MODE dropdown (row h 64, field 620×58); WINDOW SIZE (disabled outside WINDOWED, `Slate` — the *only* Slate in the panel); **UI SCALE** — see note. Hairline. **AUDIO**: EFFECTS, MUSIC — track 620×36, handle 18 px, value at Body in Bone, not Slate. Hairline. **GAMEPLAY**: ASK BEFORE SELL OR SALVAGE — toggle 160×52 right-aligned, label "ON — IT ASKS"/"OFF" kept. |
| Right column | (1040, 190, 500, 480) | **ACCESSIBILITY**: DAMAGE NUMBERS, SKILL NAME CALLOUTS, HIT EFFECTS, SCREEN FLASH ON DEFEAT — four stacked rows, toggles 160×52. **CONTROLS**: one button "KEYS AND HELP — F1" (opens the help sheet); no fake bindings. |
| Danger zone | (1040, 700, 500, 180) | Header "DANGER ZONE" in Ember; one sentence at Body: "Deletes your save — champion, Warren, traits, gear and chests. Cannot be undone."; button START A NEW GAME with an Ember outline (the only red control in the panel); armed state as today (:4459–4470) but spelled "CLICK AGAIN TO DELETE THE SAVE". Never hovers a tip while armed (already :4780). |
| Footer | (380, 900, 1160, 52) | left: `BUILD 1.0.0+…` at Secondary in Slate (moved inside the panel); centre: COPY FEEDBACK CODE 300×48 quiet button; right: QUIT TO DESKTOP 300×48. |

**RENAMED**: "SOUND" → "AUDIO"; "FIGHT TEXT AND EFFECTS" → "ACCESSIBILITY"; "SKILL NAMES" → "SKILL
NAME CALLOUTS"; "FIGHT EFFECTS" → "HIT EFFECTS"; "RED FLASH" → "SCREEN FLASH ON DEFEAT". Untitled
first group → "DISPLAY". **MOVED**: START A NEW GAME → danger zone; COPY FEEDBACK CODE, BUILD stamp,
QUIT → footer. **REMOVED**: nothing — every control is real (`GamePrefs`, DisplaySettings.cs:156–158).

**NOT added (§36 "do not add fake settings")**: *Screen shake* — no global shake exists
(Game1.cs:4010 remark: removed; the Forge's chest rattle is a reveal animation, ForgeScreen.cs:150).
*Reduced motion* — nothing reads it yet; add the toggle **with its first consumer** (the chest
shake, node pulses and inspector crossfades of §87), not before. *Default combat speed* — BATTLE
SPEED was removed with the feature (SoloExpeditionScreen.cs:3107). *Key bindings* — none are
rebindable; CONTROLS is a link to Help.

**UI SCALE — honesty note.** The game renders a fixed 1920×1080 canvas (`CanvasFit.cs:31–32` ×
ArtScale 4) and presents it through `PresentFit`. Every layout constant is absolute in that space, so
a 125 % UI Scale cannot be a uniform zoom (it crops) and cannot be text-only (fixed rects overflow).
It requires authoring at a smaller logical canvas (1536×864 for 125 %, 1280×720 for 150 %) with
layouts that re-flow, i.e. the typography/layout lead's decision. List the row in DISPLAY only when
that path exists; until then the honest P0 is raising the ladder itself (§3–5). Do not ship a
dropdown that says 125 % and does nothing.

---

## 9. Proposal C — the help sheet (F1)

Keep the modal `(112, 92, 1696, 840)`. Replace the left column's seven rows (Game1.cs:5040–5048)
with the current model, numbers still read live:

```
HOW A BUILD WORKS
  STYLE        six ways to fight — Hammer, Snare, Sign, Volley, Field, Drain
  SKILL        {SkillCapacity} slots; twelve skills, two per style, learned on MASTERY for good
  VARIATION    one per skill, changes what it does
  REINFORCE    small rules that stack on the skill
  VOW          a promise on one skill — pays while kept
  KEYSTONES    {KeystoneCapacity} sockets, learned on TRAITS
```

(Style names from `SkillCatalogue.Style`, SkillCatalogue.cs:34–52.) Right column: keep the rail order,
drop "T — BUILD, THE SAME PLACE AS B" (or "B or T"), add "ESC — ALSO CLOSES THIS". Row pitch 38 → 46
at the raised Body rung (15 rows × 46 = 690, fits). Second paragraph at full Bone, not `*0.7`
(Game1.cs:5105). **REMOVED**: "SOURCE x FORM x VOW", "FORM IS HOW YOU FIGHT", "SOURCE VS REGION",
"EACH HAS A COST", "TRAITS (P) SELL MORE OF BOTH" (folded into the KEYSTONES line).

---

## 10. Proposal D — rail, pills, toasts (small, no re-layout)

- **Rail**: keep geometry. Locked icon `0.22` → `0.40` plus a 16 px lock glyph over it; label `0.35` →
  `0.5`. Requirement on hover: draw as a two-line Body block *right of the rail* at (188, Y+8, 300, 82)
  on a dark plate instead of shortening into 156 px. NEW mark and Vault count text → Body. Optional
  P2: hotkey letter at (Right−26, Y+8) Caption.
- **Pills**: QUIET treatment — dark translucent capsule, 1 px bronze border, no ornate art; height 60
  → 64; value at Headline 24 (→16 px at 720p); keep the hover tip with full name and exact digits
  (`Game1.cs:4881–4898`). Zone unchanged: right edge 1826, y 16.
- **Toasts** → one `NotificationToast` (§90): PanelQuiet, 800×96, title Headline / body Body, at the
  HUNT toast slot or `(560, 176, 800, 96)` on menus; the locked toast adopts it (loses the flat band
  and the y=96 collision with the hint slot). Fade rules as today.

---

## 11. Legacy vocabulary reaching the player, or naming the classes (this area)

| Where | Text / name | Reaches |
|---|---|---|
| Game1.cs:5041 | `"WOVEN SKILLS"` | player (F1 help, `help.png`) |
| Game1.cs:5042 | `"SOURCE x FORM x VOW"` | player |
| Game1.cs:5046 | `"FORM IS HOW YOU FIGHT"` | player |
| Game1.cs:5047 | `"SOURCE VS REGION"` (Source-vs-region rule; verify it still exists before keeping) | player |
| Game1.cs:5062 | `"BUILD, THE SAME PLACE AS B"` for T — stale duplicate hotkey | player |
| Unlocks.cs:254 | `"A FOURTH SKILL. The full weave…"` | player (BUILD slot note) |
| Game1.cs:5224–5228, 3317, 4097, 317–318 | `WeaveScreen` / `_weave` / `_showWeave` — the BUILD screen's class | class name; also `"weave"` fixture mode (Game1.cs:477, 1318, 2048) |
| Game1.cs:4092, 4098 | `PrestigeScreen _prestige` = TRAITS; `BuildScreen _buildScreen` = MASTERY; `ChestScreen _chests` = VAULT; `CharacterScreen _character` = GEAR | class names ≠ screen names (§101 "stale screen names") |
| Game1.cs:633 | `save.WovenSkills … (string?)s.Form` — save record still carries a `Form` column | persistence only (compat read); `PlayerLoadout.SkillChoice(Source …)`, `CycleSource`, `SetSource`, `WovenDefs()` (PlayerLoadout.cs:40, 127, 160, 335) keep Source as a per-skill axis — Core/build-loadout audit to confirm whether Source is still a live choice |
| Tutorial.cs:39, 67, TutorialFacts `SkillsWoven` | `TutorialStep.WeaveBuild`, "woven" | internal names only; titles/bodies are current |
| `_dust` (`MemoryDustTree`) as the TRAITS tree; `"dust"` fixture mode | old Dust terminology for the trait tree (§88) — the *currency* MEMORY DUST is still real and still pays the Warren so the pill tooltip at Game1.cs:4886 is honest (`WarrenCost(Gleam, Dust)`, Warrens/Warren.cs:68) | class/fixture names |

Neighbouring hits seen while grepping, owned by other reports: BuildScreen.cs:690, 763, 779, 1398,
1400, 1519 ("SOURCE AND A FORM", "FORMS", "FAR FORMS", "WEAVE IT"); ChestScreen.cs:742, 750 ("NONE
WOVEN", `s.Form`); WeaveScreen.cs:647, 691, 860 ("SLOT UNWOVEN/WOVEN", "WOVEN INTO A SLOT").

---

## 12. Data honesty — what the brief wants shown vs what Core computes

| Brief wants | Status |
|---|---|
| Per-screen hint: new region | EXISTS — `World.IsUnlocked`, `IsConquered`, `RegionFarm(id).BestDepth` |
| Trait point available | EXISTS — `MemoryDustTree.Available` |
| Mastery points available | EXISTS — `MasteryTree.Available` |
| New skill slot | EXISTS — `Unlocks.SkillSlots(f)` vs `PlayerLoadout.Skills.Count`; notes via `Onboarding.BannerFor` |
| New hunter joined (persistent) | PARTIAL — unlocked set banked (`CharacterState.SaveUnlocked`); *seen* needs one explained-list key (no telemetry) |
| Chests waiting | EXISTS — `Forge.UnopenedChests.Count` |
| Affordable training | EXISTS — `HunterProgression.CanTrain` / `NextRankCost` |
| Affordable Warren upgrade | EXISTS — `Warren.CanUpgrade(kind, gleam, dust)` |
| Gear "better item in bag" | NEEDS verification in gear.md; not assumed |
| Lock reasons on the rail | EXISTS — `Unlocks.Requirement(activity)` |
| Offline summary line | EXISTS as `_bootMessage` (Game1.cs:796–804); its structure is the HUNT report's §25 |
| UI Scale | NOT REAL today — see §8 note |
| Screen shake / reduced motion / combat speed settings | NOT REAL — no consumer or feature; do not add |

---

## 13. Fixtures

**Exist today** (`Game1.cs` RH_SHOT_MODE handlers; `tools/asset-pipeline/capture.sh`):
- `settings` (:1880), `settingsfull` (:1881), `settingsopen` + `RH_SHOT_DROPDOWN=mode|size` (:1886–1890)
  — pair with `RH_SHOT_MOUSE=x,y` (:3872–3876) to pose a hover tip.
- `help` (:1879).
- `intro <png> <card>` — the HUNT tour, card 1–8 (:462–486, BeginTour :3059–3069).
- `tour <png> <Activity> <card>` — any screen's tour over its own fixture (`ShotMode` :470–486;
  `SeedExplained` owes that screen :1256).
- `gemtour` + `RH_SHOT_STEP` (:1260).
- `RH_SHOT_EXPLAIN=Stats,Map,SkillSlot2..4,FirstGem` leaves tours / slot notes owed under any fixture
  (:1252–1265) — this is how the slot-note banner is photographed.
- The guide strip itself has no fixture; it appears wherever a fixture's facts leave a rung current
  (`map.png`, `stats.png`) — which is accidental, not posed.
- The boot toast is posed inside `fight` (:1793–1794). Notice / locked toasts have no pose.
- `tools/check_nav_gates.py` keeps `Nav` and `NavActivity` parallel.

**Brief §95 does not list a chrome fixture.** For this area it additionally needs:
1. `guide <png> <Rung>` — poses one `TutorialStep` on the screen `Sends()` names, with facts that make
   it ready (e.g. SpendGleam: Gleam ≥ 25, no rank), and proves it does **not** draw on another screen
   (a second capture with RH_SHOT_TAB=Map must show no lesson).
2. `hints <png> <Activity>` — poses the new contextual hint with real state: MAP with one unlocked
   never-hunted region; TRAITS with `Available ≥ 1`; MASTERY with points; BUILD with an empty slot;
   ROSTER with an unseen banked champion; VAULT with 2 chests; STATS with an affordable rank; WARREN
   with an affordable upgrade.
3. `navstate <png>` — rail with: lit tile, one locked tile under `RH_SHOT_MOUSE` (requirement shown),
   two NEW marks, Vault badge "9+".
4. `toasts <png>` — boot toast + queued notice + locked toast in their final shared slot.
5. `settings` — extend to pose the armed START A NEW GAME (a `RH_SHOT_ARMED=1` flag) and the
   danger zone; the existing `settingsopen` covers the dropdown list.
6. Every one of the above at 1280×720 as well (capture at 1080 and downscale, as the baseline does).

---

## 14. Summary — REMOVED / MOVED / RENAMED

**REMOVED**: the bottom guide strip on all screens (`GuideBannerRect` 470,952,980,~102); the strip over
the Log and the chest reveal; help rows "SOURCE x FORM x VOW", "FORM IS HOW YOU FIGHT", "SOURCE VS
REGION", "EACH HAS A COST", "TRAITS (P) SELL MORE OF BOTH"; help row "T — BUILD, THE SAME PLACE AS B";
ornate art on the currency pills; the flat locked-toast band; the `Slate` percentage on sliders.

**MOVED**: SpendGleam / OpenChest / EquipItem / WeaveBuild lessons → their own screen's hint slot
(450,86,1200,48); Watch / MeetABoss / Conquer → HUNT toast slot (630, HeaderStackBottom+8, 560, 88);
"something new here" signal → the rail's existing NEW mark; START A NEW GAME → DANGER ZONE
(1040,700,500,180); COPY FEEDBACK CODE, BUILD stamp, QUIT → settings footer (380,900,1160,52);
locked-tile requirement → a plate right of the rail (188, Y+8, 300, 82).

**RENAMED**: SOUND → AUDIO; FIGHT TEXT AND EFFECTS → ACCESSIBILITY; SKILL NAMES → SKILL NAME
CALLOUTS; FIGHT EFFECTS → HIT EFFECTS; RED FLASH → SCREEN FLASH ON DEFEAT; untitled display group →
DISPLAY; intro card 8 "LESSONS … down here" → the NEW-mark explanation; `Unlocks.SkillSlotNote(4)`
"The full weave" → "All four slots"; class names `WeaveScreen`→`BuildScreen`, `BuildScreen`→
`MasteryScreen`, `PrestigeScreen`→`TraitsScreen`, `ChestScreen`→`VaultScreen`, `CharacterScreen`→
`GearScreen` (mechanical, when the owning screen passes happen).
