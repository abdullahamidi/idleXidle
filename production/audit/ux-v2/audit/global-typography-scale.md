# UX V2 audit — GLOBAL: typography, readability and the UI SCALE design

Area: brief sections 3-8 (readability, UI scale, typography target, text roles, secondary-vs-disabled,
colour-alone) and 93-94 (performance, resolution matrix). Read-only audit of `main`-equivalent working tree
(branch `feat/hunter-cutout-rig`, clean). Every claim below cites a `file:line` or a screenshot.

Screenshots examined: `baseline/720/{fight,forge,weave,dust,settings,stats}.png` (1280x720) and
`baseline/{fight,forge}.png` (1920x1080).

---

## 0. The question this area answers

Brief section 100 has no line for "global", so the operative questions are section 3 ("Body text must remain
comfortably readable at 720p") and SETTINGS ("How should the game behave and present itself?").

**Today: no.** At 1280x720 the page screens' Body rung lands at 11.3 physical px and the Secondary rung —
which is the rung most of the game is actually set in — at 9.6 px. There is no UI Scale control anywhere
(`DisplaySettings.cs:156-158` `GamePrefs` has no such field; `Game1.cs:4358-4470` `DrawSettings` draws MODE
and WINDOW SIZE only). The code itself already says where the floor is: `DisplaySettings.cs:56-58`
"1280x720 is the floor: the chrome's body type ... lands at 12.7 physical px there — the smallest the
hand-drawn face still reads at".

---

## 1. How a pixel actually reaches the monitor (measured, not assumed)

The task premise was "a fixed 1920x1080 canvas presented through a bilinear downscale". That is true and
also incomplete. There are **three** coordinate spaces and **two** resamples between a glyph and the eye,
and the second space is where the readability is lost.

| Layer | Where | Transform | Text raster |
|---|---|---|---|
| Render target `_canvas` 1920x1080 | `Game1.cs:1162` | — | — |
| Batch A background / Batch C chrome (rail, pills, title, toasts, settings modal, tour) | `Game1.cs:4086`, `4118`, `BeginCanvas(1)` at `3997-4003` | identity | rasterised at rung px (`SmoothFont.cs:186`, `Scale=1`) → 1:1 on RT |
| Batch B **HUNT** (`SoloExpeditionScreen`) | `Game1.cs:4091` else-branch, `ScreenScale()=1` at `4047` | identity | 1:1 |
| Batch B **every page screen** (GEAR, STATS, BUILD, MASTERY, VAULT, FORGE, WARREN, MAP, TRAITS, ROSTER) | `BeginOverlayCanvas` `Game1.cs:4006-4018`; `OverlayTransform` `4028-4029` | **Scale(0.8958) · Translate(180, 0)**; `OverlayScale = (1920-180-20)/1920` at `5323` | rasterised at rung px, then drawn through the 0.8958 matrix → **first resample** |
| Present blit | `Game1.cs:4179-4184`, `Display.PresentFit` → `CanvasFit.PresentFit` `CanvasFit.cs:73-79` | uniform fit, LinearClamp | **second resample** (0.667 at 720p) |

Consequences:

1. **A page screen's rung is 10.4% smaller than the same rung on HUNT or in the chrome.** Body 19 is 19 canvas
   px on HUNT and 17.0 canvas px on FORGE. The ladder is one set of numbers that renders at two sizes depending
   on which side of the rail you are on. (`UiTypography.cs:24-26` documents the values as "LOGICAL pixel heights
   in the 1920x1080 authoring space ... `UiKit.Scale == 1`" — the doc does not know about the 0.8958.)
2. **Page text is soft even at native 1080p**, because `SmoothFont.FontAt` (`SmoothFont.cs:185-186`) rasterises
   at `logicalPx * Scale` with `Scale = 1` (`Game1.cs:4008-4009`) and the matrix then shrinks the bitmap by
   0.8958. At 720p a second bilinear pass follows. The 720 baselines were produced offline (no script exists
   under `production/audit/ux-v2/`; method unknown) and are very likely *sharper* than what the game shows a
   720p player, since an image tool's resample is one pass and typically better than bilinear.
3. **The overlay only covers 967 of 1080 canvas rows.** `UiKit.cs:717-730` (`OverlayScrim`) explains: authored
   y 1080 lands at canvas y 967, leaving a 112 px band under every page screen. The band currently hosts only
   the guide banner (`Game1.cs:3142-3148`: banner at `1080 - height - 26`).
4. **Mouse mapping is asymmetric.** Chrome hit-tests use `ChromeMouse` (`Game1.cs:3906-3914`, full resolution,
   with a comment explaining why the x4 route quantised clicks to a 4 px grid). Page screens still receive
   `CanvasMouse` (480x270 space, `3875-3880`) and invert with `ToOverlay(canvasMouse)` =
   `((x*4 - 180)/0.8958, y*4/0.8958)` (`5336-5337`) — so every page-screen click is quantised to a
   4/0.8958 ≈ 4.5 px grid. Not a readability finding, but it is the exact seam UI Scale has to touch.

### 1.1 Physical size of every rung (em height, px)

Present scale p: 720p 0.667 · 900p 0.833 · 1080p 1.0 · 1440p 1.333. Page factor 0.8958.
IBM Plex Sans Condensed cap height ≈ 0.70 em, x-height ≈ 0.52 em — so multiply by 0.7 for what a capital
actually measures.

| Rung | logical | chrome/HUNT @1080 | page @1080 | chrome/HUNT @720 | **page @720** | page @1440 |
|---|---|---|---|---|---|---|
| ScreenTitle | 36 | 36.0 | (chrome only) | 24.0 | — | — |
| PrimaryValue | 30 | 30.0 | 26.9 | 20.0 | 17.9 | 35.8 |
| PanelTitle | 26 | 26.0 | 23.3 | 17.3 | 15.5 | 31.1 |
| Headline | 24 | 24.0 | 21.5 | 16.0 | 14.3 | 28.7 |
| NavigationLabel | 21 | 21.0 | 18.8 | 14.0 | 12.5 | 25.1 |
| Body | 19 | 19.0 | 17.0 | 12.7 | **11.3** (cap 7.9) | 22.7 |
| Secondary | 16 | 16.0 | 14.3 | 10.7 | **9.6** (cap 6.7, x-height 5.0) | 19.1 |
| Caption | 14 | 14.0 | 12.5 | 9.3 | **8.4** (cap 5.9) | 16.7 |

A 5 px x-height is below the legibility threshold for lowercase (the Forge's "upgrades · stats · the shop",
`ForgeScreen.cs:2327`, is lowercase Secondary — on `720/forge.png` it is a grey smear under GLEAM).

### 1.2 Which rungs the game is actually set in

Counted with `grep -o "UiTypography.<Rung>"` over `src/IdleXIdle.Game/*.cs`, plus the unsized proxies:

| Rung | call sites |
|---|---|
| **Secondary** (16) | **295** (+4 SectionLabel) |
| Body (19) | 138 sized + **136 unsized** `Text/TextRight/TextCenter` (`UiKit.cs:1095-1097` → `Label = Body`) |
| PanelTitle | 52 |
| Headline | 37 |
| Caption | 23 (16 of them on HUNT) |
| ScreenTitle / PrimaryValue | 13 / 11 |
| NavigationLabel / ButtonText | 2 / 4 |

**The second-smallest rung is the game's modal rung.** Secondary outnumbers sized Body 2:1. Whatever the ladder
values become, the assignment is upside down against brief section 6 ("Body — readable prose and normal
rows; Secondary — supporting metadata"). Per-file Secondary density: ForgeScreen 61, SoloExpeditionScreen 33,
CharacterScreen 31, BuildScreen 27, MapScreen 26, ChestScreen 24, WarrenScreen 20 — every dense screen is
mostly Secondary.

---

## 2. What the 720p pixels show

### `720/fight.png` (HUNT)
- **Eye lands on:** six gold ornate frames of identical weight — hunter card, stage header, WELCOME BACK
  toast, SKILLS rail, IDLE RATE, REWARD ACTIVITY — before it lands on the champion. The one thing the screen is
  for (the fight) is the least framed thing on it. `UiKit.cs:541-545` already names this failure ("eleven
  regions ... all make it, none of them wins").
- **Illegible:** every skill's description line ("HEAVY DAMAGE TO ONE TARGET", "FIRES 5 ARROWS AT RANDOM
  ENEMIES, WITH FEWER ENEMIES THEY ARE SPLIT BETWEEN") — these are **sentences at Caption 14** →
  9.3 px em, 6.5 px caps. Source: `SoloExpeditionScreen.cs:3078-3080` wraps `railDef.Line` with
  `UiTypography.Caption` and an 18 px pitch. Brief section 6: "Caption must NOT carry sentences."
- **Illegible:** cadence captions "EVERY SIXTH ACTION", "ALWAYS ON", "WHEN BITTEN" (Caption,
  `SoloExpeditionScreen.cs:3032`); "TEMPO 1.06x ACTION SPEED"; "CONQUEST 1 / 20"; "DEEPEST WAVE REACHED 1";
  the whole CHEST FILTER row (`2738-2740`, Caption).
- **Readable:** nav labels (NavigationLabel 21 → 14 px), region name (Cinzel 36 → 24 px), "-10 CRITICAL"
  (46 → 31 px), "WAVE 1", "OPEN 1 CHEST (K)".
- **Wasted:** the SKILLS rail is 286 px wide and 700 tall for four medallions plus captions nobody can read;
  the right column's CHEST FILTER card is a management control on the combat screen (brief 20).

### `720/forge.png` (FORGE) — the worst case
- **Eye lands on:** four equal gold columns. Nothing says "the item" or "the action" is the subject.
- **Illegible (≤ 9.6 px em, page Secondary):** bag row LEVEL chips; "LEGENDARY · WEAPON"; "SET PIECE · SHADOW /
  for merging — not the fight's Source"; every stat row ("LOOT +10.7% ▸ +11.0%") — the before→after
  comparison the brief calls the Forge's most important interaction (section 56) is the smallest text on the
  screen; "Every stat on it grows. The card on the left shows before → after." (`ForgeScreen.cs:1666`, a
  sentence at Secondary); "YOU HOLD 12,600 SCRAP · 131.9M GLEAM"; all of YOUR CHARTS (`ForgeScreen.cs:2213`,
  sentences at Secondary); "upgrades · stats · the shop" (lowercase).
- **Borderline (11.3 px, page Body):** "UPGRADE — RAISE THE ITEM ONE LEVEL", bag item names, materials
  names.
- **Readable:** THE FORGE (chrome), panel titles (15.5 px), UPGRADE +1 LEVEL button.
- **Chrome subtitle** "PICK AN ITEM IN YOUR BAG, THEN CHOOSE A TAB — …" is Secondary in chrome → 10.7 px:
  the one line that explains the screen is unreadable at the resolution the brief says is common.

### `720/weave.png` (BUILD)
- Slot rows: skill name (Body-ish) readable; "BODY / ACTIVE" chips, "BLOW +1", and the "EMPTY VOW SOCKET"
  placeholders ≈ 8-9 px, illegible. Reinforcement boxes TWIN / NOCK / FLIGHT and "HOVER ANYTHING TO READ IT"
  are drawn so dim they read as **disabled**, not as "empty" (brief 7). "NO VOW SWORN" is a grey bar
  indistinguishable from a disabled button.
- **Overflow bug:** the "IN VERDANT HOLLOW / NATURE STRONG x1.25 / BODY WEAK x0.87 / MIND even x1.00" block
  runs *below* the left panel's bottom frame (x≈165-430, y≈560-615 on the 720 image) — text on top of
  ornament.
- **Legacy vocabulary on screen:** panel title "WHAT YOU ARE WEAVING" (`WeaveScreen.cs:926`), subtitle "A
  SKILL IS LEARNED ON THE MASTERY TREE, THEN WOVEN INTO A SLOT" (`:860`), "NO DISCIPLINE YET — A
  SPECIALISATION NODE ON THE MASTERY TREE (E) GIVES YOU ONE" (`:869`).
- VOWS panel is ~50% empty below two rows while its text is 9-10 px.

### `720/dust.png` (TRAITS)
- Node labels are world units (`PrestigeScreen.cs:919-928`, opted out of the gate) at home zoom ≈ 0.55 →
  ≈ 7-8 px: "HARDER HITS III", "KEYSTONE — REAPER" are shapes, not words. Road sub-lines ("Hit harder. Live
  closer to death." / "5 OF 8 · 45 POINTS") ≈ 7 px.
- Trait detail: "WHAT IT DOES / WHAT IT COSTS / YOU NEED FIRST" gold heads at Secondary → 9.6 px; body at
  11.3 px borderline. "6 TRAIT POINTS" (red) and "LEARN THIS TRAIT" read.
- **Secondary vs disabled collision, on one panel:** "Permanent. Never resets." (secondary, Slate-ish) and the
  disabled LEARN THIS TRAIT label (`UiKit.cs:892`, `7C7688`) are the same grey to the eye. Measured: Slate
  `8A96A8` 6.45:1, disabled `7C7688` 4.42:1, PrestigeScreen `LockedInk 847E96` 4.98:1 on the panel interior
  (~`#100C16`). Three greys within 2 contrast points, three different meanings.

### `720/settings.png`
- Labels at Body in chrome → 12.7 px; slider percentages "80%" at Secondary → 10.7 px; the whole panel is
  1250x880 with generous vertical slack between rows (brief 35 — "use the available space"). DISPLAY has
  exactly two rows; the dropdown geometry comment already anticipates "a third dropdown added later"
  (`Game1.cs:4288-4290`).

### `720/stats.png`
- Row labels MIGHT/RESONANCE readable; "RANK 12 OF 60" and the blue rule text ("YOUR BASIC ATTACK HITS FOR 96")
  at Secondary → 9.6 px: the one line per row that answers "what changes if I spend this" is the illegible
  one. Left column: PROGRESS card values at ≈9 px.

---

## 3. Findings

### P0 — readability / hierarchy

| # | Finding | Evidence |
|---|---|---|
| P0-1 | Page screens carry a hidden 0.8958 squeeze; Body = 17 px at 1080p, 11.3 px at 720p; Secondary 9.6 px | `Game1.cs:5323`, `4006-4029`; table 1.1; `720/forge.png` stat rows |
| P0-2 | Secondary (16) is the modal rung — 295 sites vs 138 sized Body; most of the game's text is the second-smallest size | section 1.2 counts; `ForgeScreen.cs` 61 sites |
| P0-3 | Sentences set at Caption/Secondary, against brief 6 | `SoloExpeditionScreen.cs:3078-3080` (Caption, 18 px pitch), `ForgeScreen.cs:1666`, `:2213`, `UiKit.cs:1171` (HoverTip), `Game1.cs:3145` (guide banner body) |
| P0-4 | Page text is resampled twice; soft at native 1080p, softer at 720p | `SmoothFont.cs:185-186` rasterises at logical px with `Scale=1` (`Game1.cs:4008`) under a 0.8958 matrix; present blit `Game1.cs:4184` |
| P0-5 | No UI Scale setting; no prefs field; no Auto | `DisplaySettings.cs:156-158`, `Game1.cs:4358-4470` |
| P0-6 | Secondary and disabled are the same grey: Slate `8A96A8` 6.45:1 vs disabled label `7C7688` 4.42:1 vs `LockedInk 847E96` 4.98:1 vs `NavLabel*0.9` 4.42:1 | `UiKit.cs:863`, `:892`, `PrestigeScreen.cs:47`, `Game1.cs:5406`; `720/dust.png`, `720/weave.png` reinforcement boxes |
| P0-7 | No shared colour tokens: Bone/Gold/Slate/Dim are re-declared privately in 14 files with three different `Dim`s (`3A3A44`, `2C2C36`, `5E5A6E`) and two `InkFaint`s (`2C2C36` in UiKit, `9A92A8` in Forge) | `grep "static readonly Color"`: BuildScreen.cs:30-36, CharacterScreen.cs:39-50, ForgeScreen.cs:38-47, PrestigeScreen.cs:37-47, SoloExpeditionScreen.cs:40-44, UiKit.cs:53-84, 857-863, … |
| P0-8 | Ornate frame competition flattens hierarchy on HUNT (six equal gold frames) | `720/fight.png`; `UiKit.cs:541-545` names the problem; `PanelQuiet` exists (`:567`) but HUNT's plates are all `Panel` |

### P1 — layout / UX (global)

| # | Finding | Evidence |
|---|---|---|
| P1-1 | The same rung renders at two sizes across the rail: HUNT identity vs page 0.8958 | `Game1.cs:4091` vs `4006` |
| P1-2 | ~110 hand-set line pitches (`+= 22`, `lineH = 22`, `rowH`) across 13 files; no `Pitch()` helper; the type gate does not cover pitch, so a ladder change silently overlaps rows | counts: PrestigeScreen 18, ItemTooltip 15, WeaveScreen 15, CharacterScreen 12, ChestScreen 11, ForgeScreen 10; `UiKit.cs:1170` `lineH = 22`; `Game1.cs:3145` `* 22`; `SoloExpeditionScreen.cs:3081` `hy += 18` |
| P1-3 | Page cursor quantised to ~4.5 px via 480-space `CanvasMouse` → `ToOverlay` | `Game1.cs:3875-3880`, `5336-5337`; contrast `ChromeMouse` `3906-3914` |
| P1-4 | 112 px dead band under every page screen; only the guide banner lives there | `UiKit.cs:717-730`, `Game1.cs:3142-3148` |
| P1-5 | Chrome subtitle under each screen title is Secondary in chrome → 10.7 px at 720p; it is the line that explains the screen | `720/forge.png`, `720/stats.png`, `720/weave.png` |
| P1-6 | Settings controls small and sparse; DISPLAY group has no UI SCALE row though the geometry was designed for a third dropdown | `Game1.cs:4214-4232`, `4288-4290`; `720/settings.png` |
| P1-7 | BUILD affinity block overflows its panel | `720/weave.png` x≈165-430, y≈560-615 |
| P1-8 | Weight boundaries (`SemiBoldFrom 21`, `BoldFrom 30`) are literals duplicated from the ladder; a ladder change silently re-weights rungs | `SmoothFont.cs:85, 92` |

### P2 — polish

| # | Finding | Evidence |
|---|---|---|
| P2-1 | `UiTypography.Label` alias (= Body) still has 5 uses and `UiKit.Measure` defaults to it — one rung, two names | `UiTypography.cs:152`, `UiKit.cs:476` |
| P2-2 | `UiKit.Title()` is a dead 480-space helper (`4 / Scale`) | `UiKit.cs:1247-1256` |
| P2-3 | `CanvasFit` class doc still says "Integer scale only, always" while `PresentFit` is fractional | `CanvasFit.cs:15-21` vs `73-79` |
| P2-4 | `HoverTip` width 430 / pitch 22 / flip bounds 1912/1072 are literals; will break under any page size other than 1920x1080 | `UiKit.cs:1170-1177` |
| P2-5 | `NavLegend` no-op stub | `UiKit.cs:1242-1244` |
| P2-6 | `SectionLabel` heading "carried on colour and caps, not size" (`UiTypography.cs:163-171`) — at 720p caps of 6.7 px carry nothing | `720/dust.png` WHAT IT DOES |

---

## 4. Keep — what is right and must survive the refactor

- **One ladder, one gate.** `UiTypography` as the single source of sizes and `tools/check_ui_type.py` refusing
  bare numerals (with the reasoned `ui-size-ok` opt-out list printed every run). The *discipline* is exactly
  right; only the values, the rung assignment and the pitch coverage need work.
- **`SmoothFont`**: tabular IBM Plex Sans Condensed + Cinzel for ceremony, weight derived from size in one place,
  Cinzel backed by Plex for glyph fallback, the pixel-font fallback, and the `check_font_digits.py` /
  `check_font_coverage.py` gates. Nothing here should change except the raster density arithmetic.
- **One transform for all page screens** (`OverlayTransform` / `OverlayToCanvas` / `ToOverlay`,
  `Game1.cs:4028-4036`, `5336`), already used by the two screens that reopen scissored batches
  (`BuildScreen.cs:1034-1045`, `PrestigeScreen.cs:763-775`). This is precisely the seam UI Scale needs and it
  is already single-sourced.
- **Fixed 1920x1080 render target + uniform letterboxed present** (`CanvasFit` in Core, tested in
  `tests/unit/IdleXIdle.Core.Tests/Presentation/CanvasFitTests.cs`).
- **Panel grid derived from the art** — `TitleTop/CaptionTop/BodyTop/PadX/FrameDrop/ContentLeft/Right/Bottom`
  (`UiKit.cs:600-634`) and the `PanelTitleTop 22 / PanelBodyTop 92 / PanelPadX 40` constants. Every V2
  layout should keep routing through these.
- **`PanelQuiet` as a tint of the same art** (`UiKit.cs:536-570`) — the frame-reduction mechanism brief 9 asks
  for already exists; the fix is to *use* it.
- **Slate `8A96A8` (6.45:1) as the secondary ink** and **Bone `E8DFC8` (14.6:1) as primary** — correct values;
  they just need to be tokens, not copies.
- **The nav rail's three-state ink** (`Game1.cs:5406`: `NavGold` selected / `NavLabel*0.9` idle /
  `NavLabel*0.35` locked) is the correct primary/secondary/disabled triad — the model to generalise.
- **`HoverTip` as a flat plate, not an ornate panel** (`UiKit.cs:1163-1167`) — right instinct for brief 9/15.
- **Buttons never shrink below Caption and clear the art's ornament** (`UiKit.cs:904-909`).
- **The screenshot rig**: `RH_SHOT`, `RH_SHOT_MODE`, `RH_SHOT_MOUSE`, `RH_SHOT_T`, `RH_SHOT_SEQ`, `capture.sh`.
- **Hearth Gold as meaning** already articulated in code (`Game1.cs:89`, `UiKit.cs:68-84`).

---

## 5. UI SCALE — design for this architecture

### 5.1 Candidate mechanisms, evaluated

**(a) Global text-size multiplier only.** Multiply the rung at draw time; leave every rectangle as is.
- Breaks immediately: ~110 hand-set pitches (P1-2), fixed column widths fed to `Shorten/ShortenBig/WrapBig`,
  `Button`'s fit loop (`UiKit.cs:909`) would shrink labels straight back to Caption, chips and rows would
  overflow their frames. Bigger text in the same boxes is exactly what brief 6 forbids ("do not reduce text
  size to force content into a bad layout" — the mirror image is just as bad).
- Verdict: reject as the mechanism. Keep as the *only* option for the chrome zone (see 5.3).

**(b) A logical page that shrinks; one matrix scales it up.** Draw the page at `1920/s x 1080/s` logical and
scale by `s` inside the same physical area. Every *relative* layout constant stays valid; every text size
grows physically; pitches, paddings and column widths all grow with the text so nothing overlaps.
- The task description claims this "keeps every layout constant valid". **That is only true of constants
  expressed relative to the page.** This codebase's layouts are literal 1920-space numbers: 61 four-int
  `new Rectangle(...)` literals (23 of them reach beyond 1536x864, 41 beyond 1280x720 — counted by
  `scratchpad/rects.py`), ~100 `const int` position/size constants (`SoloExpeditionScreen.cs` 25,
  `BuildScreen.cs` 18, `Game1.cs` 17, …) and ~480 computed `new Rectangle(` calls built from them. A panel at
  x 1420..1850 (FORGE's YOUR MATERIALS) is off a 1536-wide page. So (b) at 125% *crops the right and bottom
  fifth* of every unconverted screen.
- But this is the honest shape of the problem: **at 125% a 1920x1080 layout can only show 1536x864 of itself.**
  No mechanism avoids that — you crop, you scroll, or you re-lay out. The brief has already chosen re-layout
  (sections 53-54 three columns for FORGE, 47 fewer larger Vault cards, 28 "use empty space" on MAP, 94
  "720p is not an afterthought"). (b) is the plumbing that makes those passes land on a variable page and
  makes 720p-first the default discipline rather than a test matrix.
- Verdict: **recommend**, with the anchoring rule in 5.4 and the rollout in 5.7.

**(c) Per-screen relayout for each scale.** Three layouts per screen, or breakpoints.
- Eleven screens × three scales, no shared component layer yet (brief 90-91 forbids a layout engine). Every
  future change is made three times. It also does not solve the raster softness or the mouse seam.
- Verdict: reject as a *mechanism*; the per-screen passes happen anyway, once, against `UiKit.Page`.

**(e) — optional upgrade, not required for P0 — UI drawn at backbuffer resolution.** Render only the world
(background + arena) into the 1920 RT, present it, then draw Batch B/C straight onto the backbuffer with
`OverlayTransform × PresentTransform`. Text rasterised at its true physical size, zero resamples at 720p.
Costs: `RH_SHOT` must save the backbuffer (`GetBackBufferData`) instead of `_canvas` (`Game1.cs:4165-4167`);
scissor rects (`BuildScreen.cs:1034`, `PrestigeScreen.cs:763`) convert through the present rect; the fight's
HUD/arena split has to move the HUD out of the RT. Worth doing in P3 (#23 final responsive pass) after (b)
has made text sizes right; it turns "readable" into "crisp".

### 5.2 Recommendation: (b), as ONE page transform, with per-zone application

```
UiScale s ∈ { 1.00, 1.25, 1.50 } | Auto      (prefs; Auto resolves from the present rect)

PageScale   = BaseOverlayScale * s           BaseOverlayScale = 1720/1920 = 0.8958 (today's constant)
Page        = Rectangle(0, 0, round(1920/s), round(1080/s))     → 1920x1080 | 1536x864 | 1280x720
Transform   = Scale(PageScale) · Translate(OverlayLeft=180 + kick.X, kick.Y)
Inverse     = ((mx - 180)/PageScale, my/PageScale)               from ChromeMouse (full-res)
Density     = PageScale                                          glyph raster factor for page text
```

At `s = 1` the page is pixel-identical to today (same 0.8958, same 180 shift) — no screen moves when the
feature lands. At `s = 1.25` the page is 1536x864 logical drawn at 1.1198; at `s = 1.5` it is 1280x720 logical
drawn at 1.3437 (1280 × 1.3437 = 1720 ✓, 720 × 1.3437 = 967 ✓ — the same 1720x967 area).

### 5.3 The three zones

| Zone | What | Scales? | Why |
|---|---|---|---|
| **World** — scene background (Batch A), HUNT arena pass (actors, VFX, damage numbers, scissor `ArenaClip`) | `Game1.cs:4086-4088`; `SoloExpeditionScreen.cs:548, 564, 1440-1450` | **Never** | Brief 4: "not the game-world camera". The arena already runs in its own scissored pass that ends the host batch and reopens an unclipped one for the HUD (`SoloExpeditionScreen.cs:1440-1446`) — the seam exists. |
| **Chrome** — nav rail, currency pills, screen title band, toasts, guide/screen banners, settings modal, tour | Batch C `Game1.cs:4118-4137` | **No** (ladder bump only — mechanism (a) is safe here because the chrome is ~a dozen hand-checked elements) | The rail is 11 tiles × `1080/11 = 98` px (`Game1.cs:5339`); it cannot scale 1.25× and stay on the canvas, and brief 13 keeps it. Pills/title/toasts are edge-anchored singletons. |
| **Page** — the ten menu screens (Batch B under `OverlayActive`) and, after the HUNT pass, the HUNT HUD | `Game1.cs:4091-4110` | **Yes**, through `Transform` | Every page screen already draws through this matrix. |

**HUNT's HUD** (hunter card, SKILLS rail, stage header, right column) is drawn today at identity in the same
batch as the arena. It should move into the page zone: after the arena pass, reopen the HUD batch with
`Game1.OverlayTransform(Vector2.Zero)` instead of identity and lay the HUD out against `UiKit.Page`, with the
right column anchored to `Page.Right` and the arena's `ArenaRect` derived from the HUD's *canvas* footprint
(`OverlayToCanvas(rail)`.Right .. `OverlayToCanvas(rightColumn)`.Left). Today's HUD footprint makes that
arithmetic fail: at 150% the 286 px rail becomes 429 canvas px and the 350 px right column 525 px, leaving
~790 px of stage. That is the concrete reason brief 17-20 (bottom skill strip, compact hunter card, light
right column) is a prerequisite for HUNT to accept UI Scale — so HUNT's scaling lands with the HUNT pass
(P1 #7), not in P0. Until then HUNT's HUD takes the ladder bump only.

### 5.4 Exact touch points

1. **Prefs** — `DisplaySettings.cs:156-158` `GamePrefs` gains `int UiScalePercent` (0 = Auto). File format
   `:160-173`: append a keyed line `uiscale=100|125|150|auto` after the `volume=percent` marker so an old build
   ignores it and a new build reads `auto` when absent. `Save` `:175-187`, `Load` `:216-243`. Also load the
   value under `RH_SHOT` from `RH_SHOT_UISCALE` (the `Initialize` guard at `Game1.cs:512-519` skips prefs under
   the rig on purpose).
2. **Resolution of Auto** — `Game1.RecomputePresent` (`:3970-3971`) is the one place the present rect changes;
   compute `_uiScale = Resolve(prefs, _present.Width)` there. Rule (derived in 6.2): `Auto = present width
   < 1600 ? 1.25 : 1.00`. 150% is a manual accessibility step, never chosen by Auto.
3. **The matrix** — `Game1.cs:5323` `OverlayScale` const → `internal static float PageScale =>
   BaseOverlayScale * UiScaleFactor`. `OverlayTransform` (`4028`), `OverlayToCanvas` (`4032-4036`) and
   `ToOverlay` (`5336`) already read it; the two screens that rebuild the batch (`BuildScreen.cs:1036, 1045`,
   `PrestigeScreen.cs:766, 775`) call `Game1.OverlayTransform` — zero edits there.
4. **The page rectangle** — `UiKit.Page` (static Rectangle, set by `Game1` when `_uiScale` changes) plus three
   tiny anchors: `UiKit.PageRight(int inset)`, `UiKit.PageBottom(int inset)`, `UiKit.PageCenterX`. No layout
   engine (brief 91) — just the numbers screens already subtract from 1920/1080.
5. **Mouse** — `ToOverlay` takes `ChromeMouse` (already full-res, `Game1.cs:3906-3914`) instead of `CanvasMouse`:
   `((m.X - OverlayLeft) / PageScale, m.Y / PageScale)`. This also removes the 4.5 px click grid (P1-3).
   `tools/check_mouse_space.py:244` detects entry points by `"CanvasMouse" in args` — extend to
   `"CanvasMouse" in args or "ChromeMouse" in args`, or the gate reports "NO SCREEN ENTRY POINTS FOUND" and
   fails. Its `CONVERSIONS` tuple (`:29`) already lists `ToOverlay(`, so the conversion check keeps working;
   `mouse.X * 4` stays for HUNT until the HUNT pass.
6. **Text density** — `SmoothFont.Scale` (int, `SmoothFont.cs:99`) becomes `float Density`. `FontAt`
   (`:185-186`) → `GetFont(Math.Max(6, logicalPx) * Density)`; `Draw` (`:196, 210`) scale `1f / Density`;
   `Measure` (`:190, 205`) divides by `Density`. `FontSystem.GetFont(float)` exists in FontStashSharp 1.5.6
   (`FontStashSharp.MonoGame.xml` member `M:FontStashSharp.FontSystem.GetFont(System.Single)`);
   `FontSystemSettings.FontResolutionFactor` also exists but per-call density is more precise and matches the
   existing `Scale` pattern. `BeginOverlayCanvas` (`Game1.cs:4008-4009`) sets `Density = PageScale`;
   `BeginCanvas(1)` sets `1f`. `UiKit.Scale` (int, used by `NineSlice` `:771` and `Title` `:1252`) stays 1 for
   both — the frame art is authored 1:1 for the 1080 canvas and is *meant* to be scaled by the matrix.
   Fonts are cached per (weight, size): 8 rungs × 4 densities (1.0, 0.8958, 1.1198, 1.3437) × 3 weights +
   Cinzel ≈ 100 `DynamicSpriteFont`s, created once — no per-frame allocation (brief 93). **Never** feed a
   continuously varying present scale into `Density`; quantise to the four values.
7. **Kit literals that assume a 1920x1080 page** — `HoverTip` flip bounds 1912/1072 (`UiKit.cs:1175, 1177`) →
   `Page.Right - 8 / Page.Bottom - 8`; `OverlayScrim` 2144x1206 (`:730`) may stay (oversize is clipped) or
   become `Page`-derived. `Background`/`Scrim` 1920x1080 (`:511, 519`) are chrome — unchanged.
8. **Settings row** — DISPLAY group gets UI SCALE as the third `DropdownClosed` (`OpenDropdown(3)`), options
   `100% · 125% · 150% · AUTO`, the closed row echoing what Auto resolved to in the WINDOW SIZE row's own idiom
   (`Game1.cs:4391-4393` "YOUR SCREEN — …"): `AUTO — 125% FOR THIS WINDOW`. Geometry: MODE `(860,202,524,58)`,
   WINDOW SIZE `(860,278,524,58)` → UI SCALE `(860,354,524,58)`; everything below shifts +76; the panel
   `(335,100,1250,880)` becomes `(335,70,1250,940)` (aspect 1.33, still the medium frame ≥ 1.30,
   `UiKit.cs:584`). Live preview: applying `s` only changes the page matrix — the modal is chrome and does not
   move under the cursor. Hover tip text: "Makes the game's panels and text larger. Auto picks 125% for
   windows under 1600 px wide."
9. **Type gate** — unchanged. Rungs stay named constants; `check_ui_type.py`'s `DECL` regex (`:63-64`) flags
   `const int *Px|*TextSize|*FontSize = <n>` — name the new constants `UiScalePercent*`, not `*Px`. Add one
   rule in the same style: `(lineH|LineH|rowH|RowH|pitch)\s*=\s*\d+` must reference `UiTypography.Pitch(...)`
   (see 6.3) so the ladder can move without re-auditing 110 literals by eye.
10. **Fixtures** — `capture.sh` forwards `RH_SHOT_UISCALE`; every page-screen mode is captured at 100/125/150;
    a `RH_SHOT_WINDOW=1280x720` path saves the backbuffer after the present blit (today only the 1920 RT is
    saved, `Game1.cs:4165-4167`, so the real 720p output has never been photographed).

### 5.5 What breaks, and what does not

- **Breaks (by design, visibly):** every unconverted page screen at 125/150% crops at `Page.Right/Bottom` —
  FORGE's four columns (200..1850), GEAR's inspector, VAULT's popovers, the STATS training panel (405..1185
  fits 1536; its RESET row at y 520..585 fits 864 ✓), BUILD's VOWS panel (860..1240 in 720-space ✓ at 125%,
  ✗ at 150%). The 125/150 captures *are* the punch list for the per-screen passes.
- **Follows automatically:** scissor rects (`OverlayToCanvas`), tree cameras (Traits/Mastery draw in world
  units under the same matrix), tour spotlights on page screens (host applies the page transform — verify in
  `DrawTour`; HUNT's `Spotlights` at `SoloExpeditionScreen.cs:591-600` are hand-measured canvas rects and move
  with the HUNT pass).
- **Does not break:** the arena, backgrounds, present fit, the letterbox, `CanvasFit` tests, the type gate,
  the save file (prefs live in `display.txt`).
- **Frame art at >100% on a 1080p monitor** is bilinearly upscaled 1.12/1.34× (the 256 px `ui_panel_*` is drawn
  1:1 at 100%). At 720p the net is 0.75/0.90 — still a downscale. Auto never picks >100% at ≥1600 px wide, so
  only a player who *asks* for 150% on a 1080p monitor sees softer filigree. Acceptable; this is not pixel art
  (`technical-preferences.md` forbidden-patterns note).

### 5.6 Why not simply remove the 0.8958 squeeze first?

Tempting: author the page at 1740x1080 and draw 1:1. But it moves every panel on every screen (a relayout),
and it only buys 10.4%. Under (b) the squeeze is the `BaseOverlayScale` term and disappears naturally when
each screen's pass re-authors it against `UiKit.Page` — at which point `BaseOverlayScale` can become
`1740/1920` with the page height 1080 and the 112 px band (P1-4) is reclaimed. Do it per screen, not globally.

### 5.7 Rollout

P0 (global foundation, brief 97 #1): items 1-10 above, the ladder (section 6), the tokens (section 7), the
chrome's ladder bump, the settings row. Ship the row **gated** (`RH_DEV`, or offered only at 100%) until the
last page screen passes its 125/150 capture; enable Auto/125% as soon as the P1 main-loop + build-loop + loot-
loop screens pass (they are the ones a 720p player lives on); enable 150% after the P2 progression screens.

---

## 6. The ladder — proposed values

### 6.1 What the brief's numbers mean

Brief 5 lists "an effective 720p scale comparable to ScreenTitle 38-40 … Body 24-26 … Caption 17-18" and says
not to use them blindly. Read as *effective 1080-canvas sizes seen by a 720p player*, the Body target is
24-26 × 0.8958 × 0.667 ≈ **14.3-15.5 physical px em (10-11 px caps)** on a page screen. Today's Body is 11.3.
Using the brief's values as literal rungs would collapse the hierarchy: Body 24 = NavigationLabel 24, two
points from Headline 26. So: **move the ladder one honest step and let UI Scale carry the rest at 720p.**

### 6.2 Proposed ladder (logical, at 100%)

| Rung | today | **proposed** | weight | page @720 100% | page @720 **125%** | page @720 150% | page @1080 |
|---|---|---|---|---|---|---|---|
| ScreenTitle (Cinzel, chrome) | 36 | **38** | Display | 25.3 (chrome) | — | — | 38 |
| PrimaryValue | 30 | **32** | Bold | 19.1 | 23.9 | 28.7 | 28.7 |
| PanelTitle | 26 | **28** | SemiBold | 16.7 | 20.9 | 25.1 | 25.1 |
| Headline | 24 | **26** | SemiBold | 15.5 | 19.4 | 23.3 | 23.3 |
| NavigationLabel / ButtonText | 21 | **24** | SemiBold | 14.3 (16.0 chrome) | 17.9 | 21.5 | 21.5 |
| **Body** (default, unsized) | 19 | **22** | Regular | 13.1 | **16.4** | 19.7 | 19.7 |
| **Secondary** / SectionLabel | 16 | **19** | Regular | 11.3 | **14.2** | 17.0 | 17.0 |
| Caption (tags only) | 14 | **16** | Regular | 9.6 | 11.9 | 14.3 | 14.3 |
| DamageNormal/Skill/Critical | 34/40/46 | unchanged | Bold | world zone, never scaled | | | |

Reasoning:
- **Body 22** keeps Body Regular and one clear step under a SemiBold 24 button label; at 125% on a 720p page it
  is 16.4 px em / 11.5 px caps — inside the brief's target band. At 100% on 1080p it is 19.7 (today's HUNT
  Body is 19 — the 1080p player barely notices; the 720p player gets +45%).
- **Secondary 19** is the old Body; the brief's floor for prose (UX guide §9, `idlexidle_ux_screen_guide_standard.md:338`
  "body text at least 18 px") is respected *even by the metadata rung*, so a Secondary line that is really a
  sentence is at least not illegible while the per-screen passes re-assign it to Body.
- **Caption 16** stays a tag rung; 11.9 px at 720p/125% for a CHIP is fine; it must never carry a sentence
  (the gate cannot check that; the per-screen passes must — P0-3 lists the offenders).
- **NavigationLabel 24**: nav tiles are 98 px; icon `clamp(98-60,30,48)=38` at Y+24..62, label at
  `Bottom-34 = Y+64` (`Game1.cs:5398-5406`) → a 24 px em ends at Y+88 < 98 ✓. Buttons: `Button` centres at
  `Center.Y - px*27/40` (`UiKit.cs:910`) — the 52 px buttons (`SettingsCopyFeedback`) hold 24 (`r.Height-14 =
  38 ≥ 24` ✓); the 44 px chips clamp to 30 ✓.
- **Auto rule derivation:** page Body physical = 22 × 0.8958 × s × p. Target ≥ 15: p = 0.667 (720p) → s ≥ 1.14
  → 125%; p = 0.833 (1600x900) → s ≥ 0.91 → 100%; ≥1080p → 100%. Hence `Auto = presentWidth < 1600 ? 125 : 100`.
- **1440p check (brief 5):** at 100%, page Body = 26.3 px, Secondary 22.7 — comfortable; ScreenTitle 50.7 in
  chrome — large but it is Cinzel and there is one per screen. No down-scale option needed.

### 6.3 Weight boundaries and pitch

- `SmoothFont.SemiBoldFrom` / `BoldFrom` (`SmoothFont.cs:85, 92`) must be **derived**, not literal:
  `SemiBoldFrom = UiTypography.NavigationLabel` (24), `BoldFrom = UiTypography.PrimaryValue` (32). Under the
  proposed ladder Body 22 stays Regular, Headline 26 / PanelTitle 28 SemiBold, PrimaryValue 32 and the damage
  family Bold. Today's literal 21 would silently make a Body of 22 SemiBold.
- Add `UiTypography.Pitch(int rung) => rung * 13 / 10` (Body 22 → 28, Secondary 19 → 24, Caption 16 → 20) and
  route the kit's literals through it (`UiKit.HoverTip` `lineH`, `GuideBannerRect * 22`, `Pill` offsets,
  `SoloExpeditionScreen.cs:3081 hy += 18`). The per-screen passes replace their own `+= 22`s; the gate rule in
  5.4 #9 stops new ones.
- Delete the `Label` alias (`UiTypography.cs:152`, 5 uses) — one rung, one name. Keep `SectionLabel =
  Secondary` but change its doc: at 19 px with gold ink it is now a readable sub-head at 720p/125%.

### 6.4 Rung re-assignment rule for the per-screen passes (brief 6)

Screen title — one per screen, Cinzel, chrome. Panel title — one per panel. Headline — the object's name / the
key value. **Body — every sentence, every list row's primary text, every "what changes" line.** Secondary —
labels, units, category names, column heads, cost lines. Caption — chips, badges, slot letters, stack counts.
The immediate offenders are in P0-3; the 295 Secondary sites are the sweep.

---

## 7. Colour tokens — Primary / Secondary / Disabled (brief 7)

### 7.1 What exists (all measured against the panel interior ≈ `#100C16`; WCAG ratio)

| Current colour | Where | Ratio | Used as |
|---|---|---|---|
| Bone `E8DFC8` | `UiKit.cs:857`, 13 private copies | 14.6:1 | primary |
| Vellum `EDE3C8` | `UiKit.cs:66` (public) | 15.1:1 | "text on the scene" — same job as Bone |
| Gold `F0A830` / NavGold `F0B24A` / FormHex `F0B24A` | `UiKit.cs:858`, `Game1.cs:5162`, `FormHexDiagram.cs:43` | 9.5:1 | earned / selected / heading |
| **Slate `8A96A8`** | `UiKit.cs:863` + 13 copies (314 uses) | **6.45:1** | secondary |
| Forge `InkFaint 9A92A8` | `ForgeScreen.cs:47` | 6.49:1 | secondary (a second secondary) |
| `NavLabel*0.9` ≈ `7C7590` | `Game1.cs:5406` | 4.42:1 | nav idle (secondary) |
| **Button disabled label `7C7688`** | `UiKit.cs:892` | **4.42:1** | disabled |
| Prestige `LockedInk 847E96` | `PrestigeScreen.cs:47` | 4.98:1 | locked |
| CharacterScreen `Faint 6A7282` | `CharacterScreen.cs:48` | 4.00:1 | faint secondary |
| Prestige `Dim 5E5A6E` | `PrestigeScreen.cs:45` | 2.91:1 | dim |
| `Dim 3A3A44` | `UiKit.cs:31` + 9 copies | 1.72:1 | rules / dim |
| Build/Solo `Dim 2C2C36` | `BuildScreen.cs:34`, `SoloExpeditionScreen.cs:44`, = `UiKit.InkFaint` `:63` | 1.40:1 | dim |
| `NavLabel*0.35` | `Game1.cs:5406` | 1.43:1 | locked nav |
| `UiKit.Ink 14101A` / `InkFaint 2C2C36` / `Bronze 3D2604` | `UiKit.cs:53-84` | — | **for parchment panels that no longer exist** (the 2026-08 comment block describes light panels; every panel in the baselines is near-black) |

The collision is exact: **secondary (Slate 6.45, NavLabel*0.9 4.42) and disabled (7C7688 4.42, LockedInk 4.98)
overlap in luminance**, so "less important" and "unavailable" are the same grey (`720/dust.png` LEARN THIS
TRAIT vs "Permanent. Never resets."; `720/weave.png` TWIN/NOCK/FLIGHT empty boxes read as disabled).

### 7.2 Proposed tokens (one public class, `UiKit.Ink*` or a new `UiInk`; delete the 14 private copies)

| Token | Value | Ratio on `#100C16` | Rule |
|---|---|---|---|
| `Primary` | Bone `E8DFC8` (keep; retire `Vellum` as a second name) | 14.6:1 | names, values, sentences, anything the player must read |
| `Secondary` | Slate `8A96A8` (keep) | 6.45:1 | labels, units, metadata — **never below 6:1**; at 720p a 9.6 px grey label needs the contrast more than a 1080p one |
| `Accent` | Hearth Gold `F0A830` (keep; fold `F0B24A` into it) | 9.5:1 | earned / selected / active / primary action — never decoration |
| `Disabled` | `5A5664` | ~2.7:1 | **the only ink allowed under 4:1**; button labels off, locked rows, unavailable actions. Always paired with a second cue (strike, lock glyph, `LOCKED`/requirement text — brief 8) |
| `Rule` | `3A3A44` (keep one `Dim`) | — | hairlines and dividers only; never text |
| `Empty` | Slate at 60% alpha over the panel | ~4.0:1 | an empty slot's placeholder — distinct from Disabled by shape (dashed/outlined cell) not only tone |

Delete: per-screen `Bone/Gold/Slate/Dim` (BuildScreen.cs:30-34, CharacterScreen.cs:39-48, ChestScreen.cs:42-45,
ForgeScreen.cs:38-47, MapScreen.cs:29-33, PrestigeScreen.cs:37-47, RosterScreen.cs:33-37,
SoloExpeditionScreen.cs:40-44, StatsScreen.cs:39-43, WarrenScreen.cs:21-25, WeaveScreen.cs:56-62,
Game1.cs:100-104), `UiKit.Ink/InkFaint/Bronze` and the parchment comment block (`UiKit.cs:33-84`) — they
describe surfaces the game does not draw. `ItemTooltip.cs:40-42` has its own `Ink/Dim/Gold` — fold in.

### 7.3 Colour-alone (brief 8) — global observations only

Source colours already pair with a glyph on the skill medallions (`720/fight.png`) and with a word in the
BUILD slot rows ("BODY", "MIND", "NATURE", "SHADOW" chips). The bag rows on `720/forge.png` carry rarity
only as the left bar's colour plus the name's tint — no word, no glyph — and the set-piece line says "SHADOW"
in purple only. Per-screen audits own the fixes; the global rule is: **a Source or rarity is (colour + glyph)
or (colour + word); never colour alone.**

---

## 8. Data honesty (brief 92) — this area

| Display | Status | Source |
|---|---|---|
| UI Scale value | **NEEDS a new prefs field** (not Core, not telemetry) | `DisplaySettings.GamePrefs` `DisplaySettings.cs:156-158`; file format `:160-173` |
| Auto resolution | **EXISTS** | `_present` via `Display.PresentFit` (`Game1.cs:3970-3971`), desktop via `GraphicsAdapter.DefaultAdapter.CurrentDisplayMode` (`:4384`) |
| Text measurement | **EXISTS** | `SmoothFont.Measure` (`SmoothFont.cs:190, 205`); physical = logical × PageScale × presentScale |
| Colour tokens | **EXIST as 14 duplicated private copies**; need one shared owner | section 7.1 |
| Anything else brief 3-8 asks to show | none needs Core data | — |

Nothing in this area requires Core changes or new simulation telemetry. Core↔MonoGame stays decoupled
(brief 93): `CanvasFit` (Core) keeps owning the present arithmetic; `PageScale`/`Page` live in the Game
assembly.

---

## 9. Fixtures

### 9.1 What exists (`Game1.cs:470-490` `ShotMode`, `tools/asset-pipeline/capture.sh`)
`fight boss expedition forge build character stats warren map dust world region2 region3 conquered lootforge
reforge vow hybrid rig vfx help buildtree buildzoom itemmenu runlog fightgear fightfilter traitlit intro tour
vaultfirst gemtour settingsopen`; dials `RH_SHOT_T`, `RH_SHOT_ZOOM`, `RH_SHOT_MOUSE`, `RH_SHOT_STEP`,
`RH_SHOT_TAB`, `RH_SHOT_DROPDOWN=mode|size`, `RH_SHOT_EXPLAIN`, `RH_SHOT_SEQ`. All save the 1920x1080 RT
(`Game1.cs:4165-4167`).

### 9.2 Gaps for brief 94-96 and this area
- **No real 720p capture.** The rig never photographs the present blit; `baseline/720/*` are offline resamples
  of unknown filter (no script in `production/audit/ux-v2/`). Add `RH_SHOT_WINDOW=WxH` → after the present
  blit, `GraphicsDevice.GetBackBufferData` → PNG. Until then every 720p readability claim is an estimate.
- **`RH_SHOT_UISCALE=100|125|150`** forwarded by `capture.sh`; the honest §95 set is every page mode × {100,
  125, 150} × {1080, 720}.
- **`RH_SHOT_DROPDOWN=uiscale`** for `settingsopen` (`Game1.cs:1885-1889`).
- **A `typespec` mode**: one screen with every rung × the three inks on the three surfaces (panel, quiet fill,
  scene) plus a 720p-crop overlay — so ladder changes are photographed, not argued (brief 96).
- **A "worst row" fixture per rung**: the Forge stat row, the Stats rule line, the HUNT cadence caption — the
  three lines the 720 baselines show failing — captured before/after in the same frame.

---

## 10. Legacy vocabulary met while auditing (owned by the BUILD/MASTERY/VAULT audits; listed for the ledger)

Player-facing strings: `WeaveScreen.cs:926` "WHAT YOU ARE WEAVING" (panel title, `720/weave.png`);
`WeaveScreen.cs:860` "…THEN WOVEN INTO A SLOT"; `:866` "DISCIPLINE: … A VOW ON A FAR-STYLE SKILL…"; `:869`
"NO DISCIPLINE YET…"; `:647`/`:691` "SLOT UNWOVEN." / "SLOT WOVEN."; `BuildScreen.cs:690` "EVERY SKILL IS A
SOURCE AND A FORM — YOU CHOOSE BOTH"; `:763` "FORMS" row; `:779` "Every skill is a SOURCE and a FORM.";
`:1398-1400` "THE FAR FORMS HIT SOFTER" / "A VOW ON A FAR-FORM SKILL"; `:581` "ATTUNED. {Form} IS YOURS";
`:1499-1504`, `:1542`, `:1587` DISCIPLINE; `ChestScreen.cs:735-736` "DISCIPLINE: …" / "NO DISCIPLINE CHOSEN";
`:742` "NONE WOVEN"; `:750` `{s.Source} {s.Form}` label; `Game1.cs:5041-5046` "WOVEN SKILLS" / "SOURCE x FORM
x VOW" / "FORM IS HOW YOU FIGHT".
Class names: `WeaveScreen` (`WeaveScreen.cs:37`, opened by the BUILD tile), `BuildScreen` (`BuildScreen.cs:28`,
opened by the MASTERY tile — names inverted against the rail, `Game1.cs:5290-5297`), `FormHexDiagram`
(`FormHexDiagram.cs:41`), field `_attuneForm` (`BuildScreen.cs:581`).

---

## 11. Summary of the proposal in one paragraph

Keep the 1920x1080 render target, the letterboxed present and the chrome zone (rail x 0..180 at canvas scale,
pills/title/toasts edge-anchored) exactly where they are; make the ten page screens and — after its own pass —
the HUNT HUD draw through the existing single page transform with `PageScale = 0.8958 × s`, laying out
against a logical `UiKit.Page` of 1920x1080 / 1536x864 / 1280x720 (100/125/150%) with right-anchored
inspectors (`Page.Right − 40 − W`) and the primary action at the inspector's foot; rasterise page glyphs at
`PageScale` density; map the page cursor from the full-res `ChromeMouse` through the one inverse; keep the
arena and backgrounds in the unscaled world zone; move the ladder to 38/32/28/26/24/22/19/16 with weights
derived from the rungs and a `Pitch()` helper; add UI SCALE as the third DISPLAY dropdown at
`(860,354,524,58)` in a settings panel grown to `(335,70,1250,940)`, with Auto = 125% under 1600 px wide;
replace fourteen private colour copies with `Primary E8DFC8 / Secondary 8A96A8 / Accent F0A830 / Disabled
5A5664 / Rule 3A3A44`; and photograph all of it at real 720p before calling any screen done.
