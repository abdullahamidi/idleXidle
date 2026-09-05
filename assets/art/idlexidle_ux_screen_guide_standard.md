# IdleXIdle UX Screen Guide Standard — V2

**Version 2 · 2026-09-01.** Supersedes V1 (2026-08-28). V2 is the standard every screen pass of the
UX V2 refactor is measured against (`production/audit/ux-v2/BRIEF.md` §100). It describes what is
BUILT and enforced today — `UiKit`, `UiInk`, `UiTypography`, `Onboarding`, the capture rig — not a
target the code has yet to reach. Where a rule is enforced by a tool, the tool is named; where a rule
is a judgement, the judge is a screenshot.

What changed from V1, in one paragraph: the ladder moved up one step and rows follow it through
`Pitch()`; the palette has one owner (`UiInk`) with contrast law; there is a real QUIET tier
(`UiKit.Plate`) beside the ornate frame and the quiet frame; a menu screen lays out to `UiKit.Page`
so UI SCALE can exist; every screen with a detail column uses ONE inspector grammar; teaching is
contextual (a hint slot, a lesson card, a rail mark — no persistent strip); a screen is done when its
1080 capture AND its real 1280×720 capture have been looked at. V1's stale content — Source × Form,
"Stats", "Dust", Warren automation, the Gear production queue — is gone with the systems it described.

---

# 0. The laws (brief §§ 84–99, as applied)

1. **Simpler, saying more.** A screen answers "where am I · what do I have · what can I do · why can't I"
   with fewer surfaces than before. Framing is spent on the one thing that matters.
2. **One primary action per screen.** Exactly one `ButtonStyle.Primary` at a time; everything else is
   `Secondary`. Two lit buttons is a screen that has not decided.
3. **Gold means something.** `UiInk.Accent` = earned · selected · active · the primary action. Never on a
   requirement line, never on a section head, never as decoration.
4. **Data honesty.** Every displayed value is computed by Core today. No hint, verdict, difficulty word,
   comparison or prediction ships without the Core method that backs it. A blank is honest; an
   invention is a bug.
5. **720p is not an afterthought.** The smaller half of players present the 1920 canvas in a 1280-wide
   window. A player-facing line must be ≥ 12 physical px there (`Secondary` 19 → 12.7). `Caption` never
   carries a sentence.
6. **No widget engine.** Components are `UiKit` methods or small static classes, extracted on their
   second real user. No layout DSL, no data-bound screen base class, no reflection. The eleven screen
   classes keep their `static readonly Rectangle` zones and their `Draw`.
7. **Never reintroduce retired architecture through UI.** Form, weave, discipline, attunement, squads,
   Warren services/targeting/automation do not exist in Core and may not appear in copy or layout.
8. **Looked at, not compiled.** A checkpoint is done when compile · tests · gates · captures at 1080
   and real 720 · inspection · commit have all happened, in that order.

---

# 1. Canvas, page, scale

```text
Canvas (render target)   1920 × 1080, fixed; presented with CanvasFit.PresentFit (bilinear, letterboxed)
Chrome                   drawn at identity: nav rail (0..180), currency pills, screen title band, settings gear,
                         toasts, the hint slot, the lesson card, tours
Page (menu screens)      drawn through Game1.OverlayTransform: scale OverlayScale, x offset OverlayLeft (180)
                         OverlayScale = BaseOverlayScale (0.8958) × UiScaleFactor (1 / 1.25 / 1.5)
UiKit.Page               the logical page rectangle a menu screen lays out into:
                         1920×1080 @100% · 1536×864 @125% · 1280×720 @150%
HUNT                     not a page — drawn at identity, laid out around the rail directly
```

Rules:

- A menu screen centres its title at `UiKit.PageCenterX`, hangs right columns from `UiKit.PageRight(inset)`
  and bottoms from `UiKit.PageBottom(inset)`. A literal `1920 − 40` or `960` is a screen that clips at
  125% (defects register: every screen still does; each P1/P2 pass converts its own).
- The mouse is inverted through the same transform it was drawn with: `Game1.ToOverlay(mouse)` before
  any page hit-test (`tools/check_mouse_space.py` enforces the conversion on every entry point).
- Fonts rasterise at the page density (`SmoothFont.Density = OverlayScale` in the overlay batch, 1 for
  chrome, 4 for the 480-space fight layer) so glyphs land 1:1 after the matrix. Only these quantised
  densities may reach the font cache — never a continuously varying present scale.
- UI SCALE lives in prefs as `uiscale=100|125|150|auto` (`Display.GamePrefs.UiScalePercent`, 0 = AUTO;
  AUTO = 125 when the present width is under 1600 px). Default 100 until every page screen lays out to
  `UiKit.Page`; then AUTO. Dev: F8 under `RH_DEV=1` cycles it. Rig: `RH_SHOT_UISCALE`.

---

# 2. Surface hierarchy — three tiers, three calls

| Tier | Call | Wears | Use for |
|---|---|---|---|
| PRIMARY | `UiKit.Panel(b, r)` / `PanelNine` (gold `ui_panel_modal_wide`) | the ornate gold filigree | the ONE subject of a screen; a modal that takes the screen and asks something |
| SECONDARY | `UiKit.PanelQuiet(b, r)` | the same frame tinted dark bronze | every panel that lives IN a screen — columns, detail panels, cards that must read as containers |
| QUIET | `UiKit.Plate(b, r, accent?)` | a dark translucent plate + 1 px `UiInk.Rule`; optional 5 px accent bar | lists, grids, metadata rows, resource strips, toasts, the hint slot, empty cells |

Rules:

- The gold nine-slice is for MODALS only (playtest 2026-08-26). A screen with one gold column and one
  brown column reads as two screens.
- Three PRIMARY surfaces on one screen is zero hierarchy. Count them in the capture.
- A plate is not a panel: it has no title row and no frame drop. Do not hand-draw plates with `Fill`;
  call `Plate` so the tier is visible in code. A plate whose FACE is art (a region banner, a portrait
  well) draws the art first and then `UiKit.PlateEdge(r, accent)` over it, so the hairline and the
  accent rule are never painted out by the picture.
- **Cards are plates with a state (release polish 2026-09-05).** A roster card, a trait tile, a map node:
  the house `Plate`, then the STATE as a stroke or a wash — gold for active, the class or Source colour
  at a quarter for available, nothing for locked. Never a stack of hand-filled rectangles with saturated
  bands: ten of them outrank the art they frame and read as a colour legend.
- **Frames are sliced at the ornament (release polish 2026-09-05).** Every frame texture — panels,
  buttons, fields, tabs, bars, the currency pill — is drawn through `UiKit.SliceFrame`, which MEASURES
  each texture's corner flourish and centre ornament off its pixels (`UiKit.FrameSpec`, `Measure`) and
  stretches only the plain rails. Nothing ornamental is ever magnified or squashed; a rectangle too
  small for its ornaments shrinks them together. Never `b.Draw(frameTexture, rect)`. New frame art needs
  no constant — check it once with `RH_UI_FRAMES=1` (prints every measured spec) against
  `tools/measure_frames.py`. The ledger (`RH_UI_BUDGET=1`) reports the corner's magnification.
- Panel padding and header grid are `UiTypography`'s (`PanelTitleTop 22 · PanelCaptionTop 56 ·
  PanelBodyTop 92 · PanelBodyTopBare 62 · ModalTitleTop 44 · PanelPadX 40 · PanelPadNarrow 28 ·
  PanelPadBottom 32`), applied through `UiKit.TitleTop / CaptionTop / BodyTop / ContentLeft /
  ContentRight`, which add the SQUARE frame's deeper crest (`SquareFrameDrop 28`) when the rectangle
  selects it. `UiKit.PanelArtKey(r)` says which frame a rectangle will wear; resizing a panel can change
  its art.

---

# 3. Inks — one owner, with law

`src/IdleXIdle.Game/UiInk.cs`. Ratios are on the panel interior (`#100C16`).

| Token | Value | Ratio | Use |
|---|---|---|---|
| `Primary` | `#E8DFC8` | 14.6:1 | names, values, sentences — anything the player must read |
| `Secondary` | `#8A96A8` | 6.45:1 | labels, units, category names, section heads, metadata. **Never below 6:1.** |
| `Accent` | `#F0A830` | 9.5:1 | earned · selected · active · the primary action (law 3). The only gold; `#F0B24A` is folded into it. |
| `Disabled` | `#5A5664` | 2.7:1 | the ONLY ink under 4:1. Off buttons, locked rows, actions you cannot take now — **always with a second cue** (lock glyph, strike, requirement line). |
| `Rule` | `#3A3A44` | — | hairlines and dividers. Never text. |
| `Good` / `Danger` | `#6EC87A` / `#D8483A` | — | a met condition or gain / an unmet condition, loss or destructive action |
| `Plate` | `#100D18` @ E0 | — | the QUIET tier's surface |
| `Empty` | `Secondary × 0.6` | — | an empty slot's placeholder — distinct from Disabled by SHAPE (an outline), never by tone alone |

Screens keep their short local names (`Bone`, `Gold`, `Slate`, `Dim`, `Met`, `Ember`) only as aliases of
these tokens. A new `new Color(...)` for text in a screen is a review failure. Domain colours (Source
tints, rarity, item class via `UiKit.ClassColor`, Warren resources) are not inks and stay where they are.

---

# 4. Typography — the ladder, the pitch, the gate

`src/IdleXIdle.Game/UiTypography.cs`; `tools/check_ui_type.py` refuses a bare number in a size slot
or a pitch slot (`lineH` / `rowH` / `pitch`). Opt out only with `// ui-size-ok: <why this is not type>`.

```text
38  ScreenTitle       the screen's own name, ceremony face          Pitch 49
32  PrimaryValue      a headline number                              Pitch 41   Bold from here
28  PanelTitle        the title of a panel                           Pitch 36
26  Headline          the biggest thing INSIDE a panel               Pitch 33
24  NavigationLabel   a thing you click: nav tile, button label      Pitch 31   SemiBold from here
22  Body              prose, list rows, the unsized default          Pitch 28   (14.7 physical px at 720p)
19  Secondary         captions, column heads, sub-headings           Pitch 24   (12.7 px at 720p — the floor)
16  Caption           chips, badges, the tightest columns            Pitch 20   NEVER a sentence

Combat callouts (a family beside the ladder, arena only):  34 DamageNormal · 40 DamageSkill · 46 DamageCritical
```

Rules:

- `UiTypography.Pitch(rung) = rung × 13 / 10` is the distance between two lines of the same text. A
  pitch is not a gap: the space from a caption to the block under it is a layout offset and stays a
  named literal on the screen.
- Weights are derived: `SmoothFont.SemiBoldFrom = NavigationLabel`, `BoldFrom = PrimaryValue`. Moving
  the ladder moves the boundary.
- Any measure that mirrors a draw (a card height summing rows) uses the same `Pitch()` — a measure and
  a draw that disagree is how a card grows a blank bottom.
- A label beside a figure (`ITEM POWER  468`) is `Secondary`; a label that must sit in a row with a
  right-aligned value it could collide with may drop to `Caption`. A sentence never may.
- Word wrap is `UiKit.WrapBig(text, width, px)` — there is one; do not write another.
- Fixture `typespec` draws the ladder, the callouts and the inks on the plain page
  (`production/audit/ux-v2/evidence/p06-typespec-1080.png`, `…-720real.png`).

---

# 5. Buttons and controls

- `UiKit.Button(b, r, label, mouse, clicked, enabled, style)` — `ButtonStyle.Secondary` (dark at rest,
  lit on hover) for every ordinary action; `ButtonStyle.Primary` (lit at rest) for the screen's ONE
  primary decision (EQUIP · TAKE · OPEN ALL · HUNT HERE · LEARN THIS TRAIT). Disabled buttons draw the
  grey art with the `Disabled` ink and are always accompanied by the line that says why.
- Labels are `ButtonText` (= `NavigationLabel`), shrinking only when the label truly does not fit and
  never past `Caption`; they clear the art's end ornament (`UiKit.FieldCapWidth`).
- Every button is sliced (`SliceFrame`): its end scrollwork and centre ornament keep their proportion at
  any width and any control height; only the plain runs stretch.
- **Progress bars — two, and only two.** `UiKit.BarArt(r, pct, type)` is the ORNATE bar (health · boss ·
  shield · progress · xp) through the slicer, for bars 22 px and taller. `UiKit.Bar(x, y, w, h, pct, c)`
  is the SLIM bar — a crisp procedural strip (dark well, one-pixel bronze frame, the fill in the caller's
  colour with a highlight and a shade line) for every thin bar: a gate's progress, a training rank, a
  quest count. It has no raster, so it cannot stretch or blur at any UI SCALE. Never hand-draw a bar from
  two `Fill`s, and never draw a bar frame texture whole.
- Dropdowns are `UiKit.Field` + `DropdownClosed` (value left, chevron right). Sliders are the settings'
  `SliderRow` — the slim `Bar` for the track and a `Diamond` for the knob. Close icons sit at
  `UiKit.CloseRect(panel)`; slot/card closes at `HintCloseRect`.
- **The lock is one glyph:** `UiKit.LockGlyph(box, ink)` — the padlock cut from the centre of the
  locked-slot tile, in the caller's ink (Primary as a rule). Never draw the whole `ui_slot_locked` tile
  into a glyph box: at 18–24 px it is a smudge, and tinted dark on a coloured band it is a black square.
- **A grid that cannot hold its tiles scrolls by rows** (the GEAR bag, the TRAITS collection at 150 %):
  the tile keeps a legible size (`Control(64)` and up), the cell keeps the width its widest caption word
  needs, rows past the page wait for the wheel, a `ScrollBar` takes a lane inside the content edge, and
  the foot says "MORE BELOW — THE MOUSE WHEEL SCROLLS". A caption word that does not fit its cell is
  dropped, never overprinted onto the neighbour's.
- **A modal's actions are pinned, never scrolled.** SETTINGS keeps QUIT, COPY FEEDBACK CODE and the
  DANGER ZONE in a footer at every profile; only the rows above scroll. A clip line must never cross a
  plate or a button. A modal is its content, centred — never a design height with a bare fifth of frame.
- Hover = highlight + one-line tip (`UiKit.HoverTip`, flips inside `UiKit.Page`). Click = select (feeds
  the inspector). Primary button = commit. Nothing is hover-only; nothing commits on hover.
- A card that IS the commit (the VAULT's chest) carries its action as a labelled `Plate` chip, not a
  `Button`: the whole card is the click, so a real button inside it would be a second click source on
  one gesture. The chip lights with the CARD, and it is drawn at rest — an action that appears only on
  hover is the "hover-only" fault under another name.
- Keyboard/gamepad reach every control by cycle-and-confirm; no stick-cursor emulation.

---

# 6. The inspector — one grammar for every detail column (D3)

Used by MAP, ROSTER, BUILD, MASTERY, TRAITS, GEAR (the VAULT is the documented exception: no inspector;
the chest card carries its own OPEN).

```text
Rectangle (overlay space)   x = Page.Right − 40 − 496 · y 112 · w 496 · h to Page.Bottom − 40
Surface                     PanelQuiet (SECONDARY) — the inspector is never the gold subject
Sections, in this order, these words, Secondary heads:
   CATEGORY            Secondary          "KEYSTONE" · "REGION" · "HUNTER" · "SKILL"
   NAME                Headline           the thing's name, Primary (Accent only if owned/selected)
   IDENTITY line       Body               one line of what it is
   WHAT IT DOES        Body               one plain line per effect, in game terms (stun / slow / defence break)
   YOU NEED FIRST      Body + glyph       requirements as Primary text with a lock/tick glyph — never Accent
   WHAT IT COSTS  or  CURRENT STATE       the price in its own unit · or the live state (RANK 3 OF 60)
   refusal line        Danger             why the CTA is off, in one sentence, only when it is off
   CTA                 h 68 at Bottom − 92, full content width, Primary style when available
Behaviour                   follows CLICK (hover only highlights); empty when nothing is selected, and then
                            says so in one line (Empty ink) rather than showing a blank frame
```

---

# 7. Teaching — where a lesson may appear (P0.7)

There is no persistent guide strip. Teaching has four channels, each with one place:

| Channel | Where | Source | Closes |
|---|---|---|---|
| Tour | spotlight cards over the screen, first visit | `Onboarding.TourFor(screen)` | finishing or skipping writes `ScreenKey` |
| Hint slot (menu screens) | canvas `(450, 86, 1200, 48)` — one line, `Plate` + gold rule + ×; taller only for a slot note or a lesson body | in order: `Onboarding.BannerFor` (slot notes) · the guide rung whose `Tutorial.Sends` is THIS screen · `Onboarding.HintFor(screen, HintFacts)` | note → explained list · lesson → dismissed rungs · hint → this session |
| Lesson card (HUNT) | the toast slot `(630, HeaderStackBottom + 8, 560, …)`, `PanelQuiet`, ×; yields to the welcome and notice toasts | the fight's own rungs (Watch · MeetABoss · Conquer · OpenChest while none waits) | dismissed rungs (saved) |
| Rail mark | the gold NEW mark on a tile | `Onboarding.IsNew(…, showing, facts)`: tour owed · note owed · a rung that sends here · a champion joined | visiting the screen (a rung's mark stays while the rung shows) |

Rules:

- A lesson renders only on the screen it is about. Elsewhere it is a mark, not a banner.
- A hint is one line, is a pure function of a Core fact, appears when the fact becomes true and leaves
  when it stops. Its key carries the fact (`Hint:Vault:2`), so closing one count does not silence the
  next. GEAR, FORGE and HUNT have no hint — Core has no fact to back one.
- Screens keep y 86–134 free of interactive content on the page (the slot sits there).
- ONE toast anchor per zone: menu screens under the title (`ToastTop 176`), HUNT under the header stack.
  Toasts queue; they never stack.
- **Every teaching surface is the same plate** (release polish 2026-09-05): the hint slot, the tour card
  and the notice toast are `Fill(SlotGround)` + `Plate(r, UiInk.Accent)`, the title in the accent, the
  body in Primary, the counter and footer in Secondary. A notice is a title and at most two body lines
  (`NoticeBodyLines`); a keystone reveal posts its first sentence and points at the BUILD screen.
- Under a tour the screen gets no pointer (`PageCursor` is off-page while `_tourActive`, as the keyboard
  is muted): nothing hovers, tips or lifts under the scrim. Every lit hole is cut to the canvas; a hole
  that would light nothing scrolls its subject into view instead. The card keeps off the currency row
  and, on the HUNT, the skill dock.

---

# 8. Vocabulary (D7) and copy

HUNTER (not champion) · WAVE (never depth) · POWER (one label) · STYLE (never discipline / attunement) ·
EQUIP / UNEQUIP (never weave) · INNATE for a character's own passive · LEARN on the tree · titles without
the article: VAULT · FORGE · GEAR · TRAINING · BUILD · MASTERY · TRAITS · ROSTER · MAP · WARREN · HUNT.

- Effects are written in game terms — stun / slow / defence break / cooldown — one plain line each.
- No abbreviation the player has not been taught in a sentence first. Say it in words.
- A shown property must DO something. If it does not (yet), it is not shown.
- Empty state = one line in `Empty` ink saying what would fill this and where it comes from
  (`NO CHESTS — BOSSES DROP THEM, AND GIFTS`). Locked state = the requirement as Primary text with a
  lock glyph, never as a gold promise.
- **Undiscovered state (release polish 2026-09-05)** = a SEAL and a QUESTION: `icon_unknown_seal`, the
  name `???` in the unknown ink (`#6A6480`, TRAITS and BUILD share it), the word `UNDISCOVERED`, and
  nothing else — no glyph, no kind, no level, no lock (a lock says "you know what this is"). The
  inspector's unknown reading says how it is revealed, never what it is. LOCKED is for a thing the
  player has already seen (a shared skill whose road was refunded keeps its name and its level);
  UNDISCOVERED is for a thing they have never reached (`Game1._discoveredSkills`, the
  `SaveGame.LearnedSkills` latch; `TraitLedger` for traits).
- **A vow is a seal.** Its medallion (`assets/art/UI/icons/vows/icon_<vowId>.png`) carries its identity;
  its state is drawn ON it — full gold when it HOLDS, dimmed with an ember crack when BROKEN. The
  inspector shows a sworn vow as a SEAL CARD (name · reward multiplier · ASKS / YOUR BUILD · RISK pips
  from `Vow.Severity` · a HOLDS / BROKEN ribbon); the swear list is seal rows; a slot row carries the
  seal and a state chip that yields its text to the skill's name before the name is shortened.

---

# 9. Defeat, log, welcome back (D6)

- The defeat plate is two lines and a click target into the log: `FELL AT WAVE n` /
  `MAIN LIMIT — <one Verdict sentence from RunReport.MainLimit>`. No buttons over the arena; the fight
  restarts in 1.6 s. Doors (ADJUST BUILD · GEAR) live in the LOG footer.
- The EXPEDITION LOG shows the diagnosis line above the numbers; the numbers come from `RunReport` only.
- The welcome-back PANEL appears only when the credited absence is ≥ 60 s (AWAY · HUNT · WARREN ·
  CONTINUE), else the two-line toast. Every figure comes from `OfflineHunt.Result` / `WarrenYield`.
  "Best performer" and any other value the model does not compute are omitted, not estimated.

---

# 10. Data honesty — the operational form

Every value on screen has one of these provenances, and the screen's spec names it:

1. a Core property or method the screen reads directly (the whitelist is the audit's data-honesty table
   per screen in `production/audit/ux-v2/audit/`);
2. a documented fallback from the approved table (§11 below);
3. hidden, with the component's empty state shown instead;
4. reported as a blocking data gap in the completion response.

Never invented: stats, cooldowns, loot history, currencies, equipment effects, crafting odds, upgrade
costs, locked regions, production timers, "recommended" builds, difficulty words, comparisons the model
does not make, anything implying intelligence the system does not have.

---

# 11. Dependencies and fallbacks

**Blocking** — stop and report: missing animation metadata, missing actor visible bounds, invalid sprite
sheet, missing required data model, missing screen state, missing clipping where content can escape.

**Approved fallbacks** (the only ones): no recent history → real aggregate summary · no cooldown value →
Ready / Casting / Recovering · no secondary resource → a real secondary stat · a skill with no glyph →
its name on the tile.

**Cosmetic** — may be deferred: small particles, hover polish, secondary transitions, decorative animation.

---

# 12. Runtime assets

Allowed roots: `assets/ normalized/ frames_512/ frames_1024/ metadata/ runtime/`. Forbidden at runtime:
`preview/ source_reference/ source_sheet/ concept/ contact_sheet/ runtime_assets_preview/ poster/
showcase/ mood_board/ pitchboard/`. `tools/check_asset_keys.py` verifies every hand-typed key resolves.

Rendering modes: `FixedSize` (icons) · `AspectFit` (item icons, portraits — never stretched) ·
`AspectFillCrop` (backgrounds, region previews) · `SliceFrame` (panels, tabs, buttons, bars, the pill —
corners AND centre ornaments never stretch; measured per texture, §2) · `NativeScale` (actors/VFX with
pivot + visible bounds) · `AnimationStrip` (with frame metadata) · `PrimitiveSurface` (= the QUIET tier:
`Plate`, the slim `Bar`, scrims, rails, dividers, selected rows).

No BC/DXT on character, creature, glyph or UI-content art. `SamplerState.LinearClamp`, non-integer scale
allowed. Never `SpriteSortMode.Immediate` outside debugging; never per-entity `Effect` switching.

---

# 13. Component contract (per major component, in the screen spec)

```text
ID · Rectangle (Page-relative for page screens) · Content-safe rectangle · Asset ID · Render mode ·
Nine-slice insets · Safe padding · Tier (PRIMARY / SECONDARY / QUIET) · Typography rungs · Ink tokens ·
Data source (Core member) · Empty state · Locked state · Disabled state (with its second cue) ·
Overflow behaviour (wrap / Shorten / scroll) · Z-index · Interaction (hover / click / primary) ·
Blocking dependencies · Approved fallback
```

Every screen spec also defines: zero / one / many items, long names, large numbers, locked items,
disabled actions, empty filters, selected and unselected states, loading, error. Not only the full-data
screenshot.

---

# 14. Navigation

Canonical rail order (`Game1.Nav`): HUNT · GEAR · STATS→TRAINING · BUILD · MASTERY · VAULT · FORGE ·
WARREN · MAP · TRAITS · ROSTER. One shared quiet rail; only the active tile is lit; inactive tiles are
never framed. Tile labels are `NavigationLabel` in `UiInk.Secondary` (gold on the lit tile); the icon is
`Control(38)` and gives up its top pad before its floor as the profile grows, so a bigger profile never
makes the glyphs smaller. The NEW mark (§7) sits left and the VAULT's chest count right, both as the
house chip — `Plate` with the accent rule, the word in the accent, the count in Primary — never a solid
red or gold square. The currency capsules (`UiKit.Pill`) and the settings gear are `Control(60)` tall,
so the chrome grows with the digits inside it. Locked tiles say their price in a toast on click, never a
blank.

---

# 15. Fixtures and the capture rig — how a screen is verified

`bash tools/asset-pipeline/capture.sh <mode> <repo-relative out.png> [arg]` renders one posed frame
headlessly and exits. The out path must be repo-relative.

```text
RH_SHOT_MODE      the fixture (weave/loadout, buildtree/mastery, character/gear, stats/training, dust/traits,
                  fight/hunt, vault, vaultfirst, forge, warren, warrenready, warrenfresh, map, roster, rosterlocked, runlog, fightreport,
                  settings, settingsopen, help, intro <card>, tour <Activity> <card>, typespec, …)
RH_SHOT_T / _ZOOM / _STEP / _MOUSE / _TAB / _DROPDOWN / _EXPLAIN   pose dials (see capture.sh header)
RH_SHOT_UISCALE   100 | 125 | 150 | auto — the page scale (100 when unset)
RH_SHOT_WINDOW    WxH — a REAL window; the capture is then the presented backbuffer (letterbox, bilinear
                  shrink and all), not the 1920 render target. 1280x720 is the required second capture.
```

Rules:

- Every fixture seeds facts that make the rail agree with the screen (`Unlocks` is monotone on
  `DeepestWave`); a fixture that shows a locked tile beside an open screen is wrong.
- Fixture content is chosen by the spec, never by the agent; exact values, exact asset IDs, expected
  visible and hidden components are written down.
- Baselines for the V2 pass live in `production/audit/ux-v2/baseline/` (pre-refactor, 1080) and
  `baseline/720/` (offline downscales — estimates); evidence of each checkpoint in
  `production/audit/ux-v2/evidence/`. Per-image diffs against a baseline: changed-pixel count + bbox
  (scratch `diffshots.py` pattern); the fight fixture is not pixel-deterministic (wall-clock animation) —
  compare menu screens, inspect the fight.
- Gates before every commit: `bash tools/check_all.sh` (font coverage/digits · type sizes and pitches ·
  asset keys · init order · nav gates · mouse space) then `bash tools/check_boot.sh`. Never disable a gate
  to pass.

---

# 16. Acceptance — what "done" means for a screen pass

Measured in the 1080 capture AND the real 1280×720 capture:

- no sibling overlap; no text inside a frame's border inset; no clipped primary action;
- PRIMARY surfaces ≤ 1 (plus at most one modal); every list/grid/metadata surface is QUIET;
- exactly one `Primary` button; every disabled control has its second cue and its reason;
- every text line ≥ `Secondary` unless it is a chip/badge; no sentence at `Caption`; 12 px physical at 720;
- every displayed value traceable to a Core member or an approved fallback; no invented word;
- the hint slot region (y 86–134) free of page controls; one toast anchor;
- copy in the D7 vocabulary; no retired-architecture word (`Form`, `weave`, `discipline`, `attunement`,
  `depth`, `champion` in player copy);
- the inspector, where the screen has one, in the §6 grammar; the exception (VAULT) documented;
- a screen may hold one `Primary` per STATE, not one per file: the VAULT's is OPEN ALL when a chest is
  waiting and RETURN TO HUNT when none is, and the two are never drawn together;
- every state a fixture cannot pose is a state that has never been checked — the VAULT's empty state
  shipped for months in the hairline ink, describing a control that had been deleted, because no mode
  could empty the pile (P1.8). A pass adds the fixture for each state it writes copy for;
- gates green; suite green; the checkpoint committed with its captures inspected and its evidence kept.

---

# 17. Completion response format

```text
<SCREEN> UX pass — UX-V2 P<n>

Compile / tests / gates:   PASS · <count> tests · all gates green
Implemented:               - …
Data provenance:           - <value> ← <Core member>  (one line per new value)
Blocking dependencies:     None | - …
Approved fallbacks:        - …
Fixtures:                  <mode> (1080) · <mode> @ RH_SHOT_WINDOW=1280x720 · <posed states>
Captures inspected:        <paths> — what was looked for, what was found
Known follow-ups:          - … (registered in PLAN.md §4 with an owner checkpoint)
Final status:              READY FOR VISUAL REVIEW | NOT READY
```

A screen is not complete while `Final status` is `NOT READY`, and never on code review alone.

---

# 18. Where things live

```text
src/IdleXIdle.Game/UiInk.cs            the inks (§3)
src/IdleXIdle.Game/UiTypography.cs     the ladder, Pitch(), the panel grid (§4, §2)
src/IdleXIdle.Game/UiKit.cs            Panel / PanelQuiet / Plate · Button(style) · Field · HoverTip · WrapBig · Page anchors
src/IdleXIdle.Game/SmoothFont.cs       Density; derived weights
src/IdleXIdle.Game/Game1.cs            OverlayScale / UiScaleFactor / ApplyUiScale · hint slot · lesson card · toasts · rail mark
src/IdleXIdle.Core/Progression/Onboarding.cs   TourFor · BannerFor · HintFor · IsNew
src/IdleXIdle.Core/Progression/Tutorial.cs     the rungs, Sends
tools/check_ui_type.py                 size + pitch gate         tools/check_all.sh · tools/check_boot.sh
tools/asset-pipeline/capture.sh        the rig (§15)
production/audit/ux-v2/                BRIEF · PLAN (decisions D1–D12, checkpoints, defects register) · audit/ · baseline/ · evidence/
```
