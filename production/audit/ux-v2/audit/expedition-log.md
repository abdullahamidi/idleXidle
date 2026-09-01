# UX V2 audit — EXPEDITION LOG (and its two doors: the fallen banner, the welcome-back toast)

Audited 2026-09-01 against `production/audit/ux-v2/BRIEF.md` §0-15, §22-25, §82-102, §100 and the
runtime at `feat/hunter-cutout-rig` (post 2026-08-31 refactor). Read-only on source.

Code read in full: `src/IdleXIdle.Game/SoloExpeditionScreen.cs` `DrawLog` (2013-2059), `LogPanel` (2073),
`DiffLabel/DiffValue` (2076-2093), `DrawReportPanel` (2109-2224), `DrawArenaOverlay` HunterDown case
(2244-2259), `DrawLogButton` (2376-2392); `src/IdleXIdle.Core/Expeditions/RunReport.cs` (all 194 lines);
`src/IdleXIdle.Core/Expeditions/RunLog.cs` (all 160 lines); `WaveMetrics` in
`src/IdleXIdle.Core/Builds/SoloBattle.cs` (105-143, ledger write at 1097-1104); the report build path in
`src/IdleXIdle.Core/Builds/SoloExpedition.cs` (98, 185-191, 338-357); host routing in
`src/IdleXIdle.Game/Game1.cs` (2492-2515, 3038-3044, 4112-4120, fixtures 1718-1832, offline 737-806,
help 5069). Screenshots: `baseline/runlog.png`, `baseline/720/runlog.png`, `baseline/fightreport.png`,
`baseline/720/fightreport.png`.

Scale fact used throughout: 1280x720 presents the 1920x1080 canvas at x0.667, so a rung of N px is
physically N x 0.667 px at 720p — RegionTitle 36→24, PanelTitle 26→17.3, Headline 24→16, Body 19→12.7,
Secondary 16→10.7, Caption 14→9.3.

---

## 0. Does the screen answer its question today?

Brief §100: *Expedition Log — "Why did I stop progressing?"*

**Half.** It names the wave, the wall and one honest sentence about the demand that beat you
(`RunReport.Verdict()`, RunReport.cs:68-83), and it shows what moved since the previous run in the same
region (`DiffEntries`, RunReport.cs:96-107). That is the right skeleton and the brief (§23) says keep it.
But the sentence IS the number ("You reached 1.0 of 3.0 creatures per cast.", runlog.png y≈334) — exactly
the §24 anti-pattern — the interpretation column ("POINTS AT → HIT SIZE / HITS PER CAST / STAYING ALIVE /
SPEED") is cryptic lever jargon pushed 500 px away from the figures it annotates, the comparison block the
brief calls the core concept is the smallest text on the page, and there is no door from the diagnosis to
the fix (§1 "CHANGE ONE THING": the player must close the log and walk the rail). At 720p the lower half
of the panel is unreadable.

---

## 1. What the eye does (pixels)

**1080p (`runlog.png`)** — First landing: the ember band `FELL AT WAVE 13` (RegionTitle 36, Display face,
on an ember 16% fill with a 5 px left rule, y 222-278). Correct — that is the headline. Second: the
vellum verdict sentence at Headline 24 (y≈334). Third: the five-row table. The eye then falls into
~95 px of empty panel (y≈740-825) before hitting two ornate gold buttons `‹ OLDER` / `NEWER ›` at the
foot. `SINCE YOUR LAST RUN HERE` — the concept the brief singles out — is a 16 px gold label (y≈612) above
four 16 px rows with 14 px chips; it reads as a footnote. The panel occupies 1042x800 of 1920x1080; the
right 60% of every table row is empty (values end at x≈982, the next ink is `POINTS AT` at x≈1504).

Chrome competes: the nav rail (x 0-180), the three currency pills and the settings gear are drawn AFTER the
log's scrim at full brightness (Game1.cs:4112-4120 — `DrawLog` is in batch B, `DrawCurrencyPills`/nav/gear
in batch C), while the hunt beneath is dimmed to 10% (`0xE6` scrim, SoloExpeditionScreen.cs:2025). The
rail is not clickable while the log is open (Game1.cs:2251, 2260 gate on `!_expedition.LogOpen`), so it
LOOKS available and is not — the §7 confusion, inverted.

**720p (`720/runlog.png`)** — `FELL AT WAVE 13` at 24 px: fine. `EXPEDITION LOG` 17 px: fine. Verdict
16 px: readable. Table labels/values at 12.7 px: borderline; their units ("% of your damage", "of 3.0
creatures per cast", "seconds") at 10.7 px in Slate: hard. `THE WAVE THAT ENDED IT  ARMOURED x 3 · NUMBERS`
10.7 px: hard. The entire `SINCE YOUR LAST RUN HERE` block — header 10.7 px, four rows 10.7 px, delta
chips (`-361.4`, `+0.0 per cast`, `-0.4%`) 9.3 px inside 22 px plates — illegible without leaning in. The
footer hint `‹ › STEP THROUGH THE LOG · L CLOSES IT` 10.7 px Slate: illegible and unnecessary. Column
heads `MEASURE / OVER THE LAST 3 WAVES / POINTS AT` 10.7 px: lost.

**Fallen banner (`fightreport.png`, `720/fightreport.png`)** — `YOUR CHAMPION FELL AT WAVE 14` StageLabel
26 (17 px at 720p) over a translucent plate 920x110 at (500,200); its top edge touches the enemy strip's
foot (strip bottom y=204, SoloExpeditionScreen.cs:2456-2459 vs 2251). Second line `THE FULL REPORT IS IN
THE LOG — PRESS L` Body 19 → 12.7 px. It tells a mouse player to press a key while the log medallion sits
unreferenced at (1206,40). It carries no diagnosis although the report exists at that exact frame
(`Log.Add` at SoloExpeditionScreen.cs:1340 precedes the banner).

---

## 2. Findings

### P0 — readability / hierarchy

**P0-1. Everything under the verdict is Secondary or Caption and dies at 720p.**
Evidence: units, wall line, column heads, `POINTS AT`, diff header, diff rows all `UiTypography.Secondary`
(SoloExpeditionScreen.cs:2149, 2165-2168, 2176, 2181, 2193, 2199, 2208-2212); delta chips and `NEW
RECORD` chip default to `UiTypography.Caption` (2139, 2217 → `Chip(...)` default px at 2541). Observed
`720/runlog.png`: diff rows ≈10 px, chips ≈9 px. Brief §3/§5: body must be comfortably readable at 720p;
§6: caption is tags only — here it carries the run-to-run deltas, the most decision-relevant figures.

**P0-2. The hierarchy is upside down: the comparison the brief calls core is the quietest block.**
`SINCE YOUR LAST RUN HERE` header is Secondary 16 (2193) under a table whose rows are Body 19 (2171); its
rows are Secondary (2208-2212) at 26 px pitch vs the table's 36 px pitch (2175, 2201). Brief §23 names it
the concept to preserve; §99 law 2 says important is large. In `runlog.png` it is visibly a footnote.

**P0-3. The verdict restates a row instead of interpreting it (§24 anti-pattern, verbatim).**
`Verdict()` reach branch: `"You reached {TargetsPerActivation:F1} of {CreaturesPerWave:F1} creatures per
cast."` (RunReport.cs:76-77); the REACH row prints the identical figures one block lower (2179). The
brief wants `MAIN LIMIT — REACH` + a plain sentence + the numbers as magnitude. Today the page says the
same thing twice and never names the limit as a category.

**P0-4. The interpretation column is jargon at arm's length.**
`POINTS AT` column: `HIT SIZE`, `HIT SIZE`, `HITS PER CAST`, `STAYING ALIVE`, `SPEED` (2177-2181) —
right-aligned to x1≈1504 while the figures end at x≈982 (2164 `xv = x0 + 440`). Observed `runlog.png`:
~520 px of empty row between value and its annotation; at 720p the annotation is 10.7 px Slate. A player
cannot connect "0 % of your damage … HIT SIZE" into a thought. The memory rule (`ui-copy-must-avoid-genre-
jargon`) and §24 both point the same way: fold the lever into the diagnosis, drop the column.

**P0-5. Chrome outranks the modal.**
Nav rail, pills and gear at full brightness over a 90% scrim (Game1.cs:4112-4120 draw order;
`runlog.png` left rail and top-right pills bright). §9: one PRIMARY surface; §7: nothing that is
unavailable may look available. The rail is dead while the log is open (Game1.cs:2251/2260).

### P1 — layout / UX

**P1-1. No door from diagnosis to decision.** The panel has OLDER / NEWER / close only (2050-2058). Brief
§22 asks for `[ADJUST BUILD] [GEAR] [RETRY]`; §1 loop is UNDERSTAND WHY → CHANGE ONE THING. RETRY is moot
(the champion regroups automatically after `DownedSeconds`, SoloExpeditionScreen.cs:1343-1346), but BUILD
and GEAR are the two levers every verdict points at and neither is reachable from here.

**P1-2. The entry never says WHERE.** `DrawReportPanel` never reads `r.RegionId` (2109-2224). The log
holds runs from several regions (`RunLog.OlderThan` is per-region, RunLog.cs:61-68) and the header says
"SINCE YOUR LAST RUN **HERE**" — "here" is undefined on screen. A player paging through mixed-region
entries cannot tell which run was where, and the empty-diff line `NO EARLIER RUN IN THIS REGION TO COMPARE
WITH.` (2198) names a region it never shows. Data exists: `RunReport.RegionId` → `Regions.Get(id).Name`
(the host already does this, Game1.cs:4111).

**P1-3. The empty log traps a mouse player.** `Log.Count == 0` branch (2027-2032) draws two lines on the
scrim and `return`s BEFORE the panel, the close icon (2044-2045) and the footer. Only `L CLOSES THIS.`
remains. The 2026-08-26 close-icon fix did not reach this state. §82 also wants the empty state to be
purposeful: it should say what will appear here and when ("Your first report is written the moment your
champion falls or stalls").

**P1-4. ~95 px of dead band above the footer, while the block above it is starved.** `LogPanel` 1042x800
(2073); diff ends at y≈735 in `runlog.png`; buttons begin at `panel.Bottom - 104` = 826 (2050-2051). The
`room` clamp (2203-2204) reserves space for rows that never come (4 numeric rows + at most 1 named row,
RunReport.cs:100-106). Meanwhile the diff itself is squeezed to 26 px pitch (2203).

**P1-5. Width is wasted: a single-column table in a 962 px column.** Content x0=542..x1=1504 (`PadX`
40 + frame drop, UiKit.cs:618-631; UiTypography.cs:275). Values right-aligned at x0+440 leave x 990-1400
blank on every row. The comparison could sit beside the numbers at equal rank.

**P1-6. Permanent manual text.** `‹ › STEP THROUGH THE LOG · L CLOSES IT` (2054) is a sentence in
Secondary under two buttons that already say OLDER/NEWER and a close icon. §99 law 4, §55's spirit; the L
key is already in Help (Game1.cs:5069).

**P1-7. Affixes and archetypes are raw enum names.** `r.WallAffixes.Select(a => a.ToString().ToUpper())`
(2144-2146) prints `NUMBERS`, `PLATED`, `WARDED`, `LEGION`… — the live enemy strip does the same
(2436-2450, visible as the `NUMBERS` chip in `fightreport.png`). No word table exists anywhere
(`ItemAffixes.Describe` is for ITEM affixes, a different enum). A player reads "ARMOURED x 3 · NUMBERS" and
does not know NUMBERS is "more creatures". One shared `AffixWords` helper would serve the strip, the log and
the fallen banner.

**P1-8. The fallen banner is a signpost, not a diagnosis, and points at a key.** `DrawArenaOverlay`
HunterDown (2244-2259): "THE FULL REPORT IS IN THE LOG — PRESS L". The report is already in `Log.Newest`
at that frame (1340). §22 wants the main limit stated here; the banner should carry the `MAIN LIMIT —
REACH` line and be a click target that opens the log (the log medallion at (1206,40) is 700 px away and
unmentioned). The banner box also touches the enemy strip (see §1 above).

**P1-9. Welcome-back is a 7-second two-line toast (§25).** Game1.cs:796-805 builds `WELCOME BACK — 6.4
HOURS AWAY / +11.3K GLEAM (7.1K WARREN · 4.2K HUNT)`; drawn as an 800x96 quiet plate for `_bootTimer = 7f`
(Game1.cs:531, 3283-3290) at Headline/Body → 16/12.7 px at 720p. Dust, Scrap and Essence are CREDITED
(755-758) but never REPORTED (796-798); `OfflineHunt.Result.WavesCleared/Falls/DeepestWave` exist
(OfflineHunt.cs:54-56) and are unshown. Brief §25 asks for a compact return summary with `[CONTINUE]`, not a
transient line. See §4 for what can honestly be shown.

### P2 — polish

**P2-1. One number, two roundings.** Table: `{AbsorbedFraction*100:F0}` → `0` (2177); diff: `F1` →
`0.9% → 0.5%` (2089). Table `AVERAGE HIT 3249` (F0, 2178) vs diff `3610.5 → 3249.1` (F1, 2092). Observed in
`runlog.png` rows 1/2 vs diff rows 2/4. One formatter per measure.

**P2-2. No-change chips are noise.** `+0` and `+0.0 per cast` chips in Slate (2214-2218; `Improved == null`
when |Δ|<0.05, RunReport.cs:134). Print "no change" or an em dash instead of a signed zero in a plate.

**P2-3. OLDER / NEWER wear the ornate gold button frame** (`_ui.Button`, 2055-2057; visible in
`runlog.png`). §10 gold is meaning; §84 one primary action — paging is neither. Quiet/bronze buttons.

**P2-4. The one celebratory fact is the smallest text.** `NEW RECORD` chip at Caption 14 (2139) — 9.3 px at
720p. It should be at least Secondary, and gold with the band (it is the only gold the band ever earns).

**P2-5. Two counting conventions for one death.** Log: `FELL AT WAVE 13` + `12 WAVES CLEARED`
(2134-2137; WallWave = Wave+1, SoloExpedition.cs:188). HUNT reward panel: `DEEPEST WAVE REACHED 13`
(`fightreport.png`, banner says `FELL AT WAVE 14`). Consistent internally, but the player meets both
"fell at N+1" and "reached N" for the same run. Pick one phrasing across screens, or say "CLEARED 12 ·
FELL ON THE 13TH".

**P2-6. `Take(2)` on the verdict silently truncates** (2157) — harmless at 962 px width today; becomes a
risk if the column narrows in a two-column layout. Let the diagnosis block own its height.

**P2-7. Delta chip x is a literal** (`x0 + 520`, 2217) while the was/now columns are relative to `xa`
(2205) — the chip drifts from its row values if labels lengthen.

---

## 3. Keep (good, must survive the pass)

- The log as a full-screen READ over the dimmed hunt, entered by L, the medallion button (2376-2392, hover
  tip names both), and listed in Help (Game1.cs:5069). One gold nine-slice for THE modal (2111) — this is
  exactly where the frame standard says gold belongs.
- Outcome band coloured by outcome with a left rule, `FELL AT / STALLED AT WAVE N` in the display face
  (2126-2134). It is what the eye lands on first in both screenshots. Keep it as the headline.
- A verdict that names the DEMAND, never the fix (RunReport.cs:61-83) — and it is tested
  (`RunReportTests.test_the_verdict_never_prescribes_a_fix`, :97). The §92 honesty bar is already met here.
- Last-band scoping and the label that admits it — `OVER THE LAST 3 WAVES` (2166; RunReport.cs:159-163).
- `SINCE YOUR LAST RUN HERE` per-REGION diff with before → after and a delta coloured by direction
  (RunReport.cs:96-135; RunLog.cs:61-68; tests :133, :146). The content is right; only its rank and size are
  wrong.
- History: OLDER/NEWER + Left/Right keys (Game1.cs:2511-2515) + `ENTRY n OF m` + close icon at
  `UiKit.CloseRect` (2044). Ten entries persisted with save round-trip tests (RunLog.cs:29, tests :93, :151).
- Sentence-case prose (the verdict) against all-caps labels — the one place on the page that distinguishes
  reading from scanning. Extend, don't remove.
- Units set dimmer beside the figure (2173) — good typographic idea, only too small.
- Nothing in this screen's copy speaks Form / Weave / Woven / Aptitude / Source x Form (see §5).
- The fallen banner NOT being a full report that vanishes with the next descent (ResolveOverlay comment,
  614-621) — the log is the right home for the report.

---

## 4. Data honesty — what the brief wants shown vs what Core computes

| Brief wants | Status | Where |
|---|---|---|
| Failure headline: wave, outcome, waves cleared, record | EXISTS | `RunReport.WallWave/Outcome/Depth/IsRecord` (RunReport.cs:29-35) |
| The wall: archetype, affixes, count | EXISTS (raw enum words) | `RunReport.WallArchetype/WallAffixes/WallCreatures` (36-38); no player-word table for `Affix` anywhere |
| Region of the run | EXISTS, unshown | `RunReport.RegionId` (29) → `Regions.Get(id).Name` |
| Diagnosis sentence | EXISTS | `RunReport.Verdict()` (68-83) |
| `MAIN LIMIT — REACH` as a category | NOT a member; NO new telemetry needed | The if-chain in `Verdict()` already picks one of {Stalled, HitSize (absorbed ≥45%), Reach, Sustain (health ≥18%), Scale}; expose it as `RunReport.MainLimit` + `Explain()` from the same fields |
| `MAIN PRESSURE — could not sustain incoming damage` | EXISTS | `HealthLostPerWaveFraction` (53) |
| Measures: absorbed, avg hit, reach vs present, health lost, s/wave, sampled waves | EXIST | RunReport.cs:43-59 |
| Before → after | EXISTS | `DiffEntries(previous)` (96-107) + `RunLog.OlderThan` (RunLog.cs:61) |
| `BEST PERFORMER — BLOW — 43% of your damage` | NEEDS telemetry | `WaveMetrics.StyleDamage` is per **STYLE** per **wave** (SoloBattle.cs:141, written 1097-1104), consumed only for WARDED (SoloExpedition.cs:350-356); NOT aggregated in `RunRecorder.Build` (RunReport.cs:154-192), NOT in `RunReportSave` (SaveGame.cs:339+). Per-style share = aggregation + new report/save field (no new sim hook). Per-**skill** needs a `SkillId`-keyed ledger. Swing damage is excluded from the ledger (`fromSkill` guard), so the denominator must be `DeliveredDamage`, labelled "of your skill damage" or computed honestly |
| Fallen-banner diagnosis at the moment of death | EXISTS | `Log.Newest` right after `Log.Add` (SoloExpeditionScreen.cs:1340) |
| Offline: away span | EXISTS | `SaveSystem.CreditedOfflineSeconds` (Game1.cs:740) |
| Offline: HUNT gleam | EXISTS | `OfflineHunt.Result.Gleam` (OfflineHunt.cs:54) |
| Offline: HUNT waves cleared / falls / deepest | EXIST, unshown | `OfflineHunt.Result.WavesCleared/Falls/DeepestWave` (54-56) — "informational; never a record" |
| Offline: WARREN Gleam / Dust / Scrap / Essence | EXIST; only Gleam shown | `WarrenYield` (Warren.cs:71); credited 755-758, reported 796-798 Gleam only |
| Offline NOTABLE: skill levels, set pieces, rare items, auto-salvage count | NEEDS telemetry — or omit | Offline deliberately does NOT level skills, drop chests/items or advance records (OfflineHunt.cs:29-32). Showing these would be invention (§92). Omit until the design decides offline should produce them |
| Unlock progress | n/a for this screen | — |

---

## 5. Legacy vocabulary

**Reaching the player from the LOG: none.** `DrawLog`/`DrawReportPanel`/`DrawLogButton`
(SoloExpeditionScreen.cs:2013-2224, 2376-2392) and `RunReport.Verdict()`/`DiffEntries` contain no Form,
Weave, Woven, Aptitude or Source x Form copy.

Stale NAMES in the class that hosts the log and in the host path that opens it (code/comment level — the
`Form` enum no longer exists in Core, so these comments describe a deleted type):
- `src/IdleXIdle.Game/Game1.cs:2508` — the L handler clears `_showWeave`; `Game1.cs:317` `WeaveScreen _weave`.
- `src/IdleXIdle.Game/WeaveScreen.cs:37` — `public sealed class WeaveScreen` (the screen Help now calls
  "BUILD, THE SAME PLACE AS B", Game1.cs:5062 — copy renamed, class not).
- `src/IdleXIdle.Game/FormHexDiagram.cs` — file/class named after Form.
- `src/IdleXIdle.Game/SoloExpeditionScreen.cs:314` "keyed by (int)Form"; `:789` "woven skills"; `:848-850`
  "WHICHEVER PASSIVE FIELD IS WOVEN … Form.Aura"; `:1246` "(Source, Form) ordinal pair"; `:1352-1355` "one
  effect strip per FORM"; `:2290` "a new Form cannot silently print a digit" (in `OrdinalWord`, 2 lines above
  the log's own helpers); `:2842-2846` "its Form"; `:3006` "Source+Form label"; `:3252` "EACH FORM THROWS ITS
  OWN SHAPE"; `:3317` "effect key for a Form"; `:3354` "Core Form".
- `src/IdleXIdle.Core/Builds/SoloBattle.cs:132, 137` — `WaveMetrics` doc comments call absorbed "The
  Weight axis" and reach "The Spread axis"; `:1108` "the Spread/Endure bridge". Axis vocabulary that no
  player-facing screen uses today — confirm whether Weight/Spread/Endure are retired; if so, comment-only
  cleanup.

---

## 6. Proposed layout (1920x1080 logical)

Keep it a full-screen modal read; widen and rebalance; add the diagnosis layer and the two doors.

**Scrim & chrome.** Scrim 0,0,1920,1080 at the current alpha, drawn so that the nav rail, pills and gear
are UNDER it (move `DrawLog` after batch C's chrome, or have Game1 dim its chrome when `LogOpen`). The
only bright things on screen are the panel and the close icon.

**Panel** `(360, 90, 1200, 900)` — aspect 1.33 (stays ≥1.30 so `UiKit.Panel` keeps the medium frame, per the
comment at 2065-2071). Gold nine-slice (the one PRIMARY surface). Content column x0=400 … x1=1520
(w=1120) with `PadX` 40.

**Zone A — header row** y 134-160: `EXPEDITION LOG` PanelTitle 26 Gold left; right of centre, Body 19 Slate
`UMBRAL REACH · ENTRY 1 OF 3`; close icon at `UiKit.CloseRect(panel)`. *(MOVED: `ENTRY n OF m` up from the
footer. ADDED: region name.)*

**Zone B — outcome band** y 180-244 (64 tall): left `FELL AT WAVE 13` RegionTitle 36 Display in
Ember/Gold on the 16% fill with the 5 px rule (unchanged); right `12 WAVES CLEARED` Body 19 Bone and the
`NEW RECORD` chip at Secondary 16 Gold. Under the band y 256-280, Body 19: `ENDED BY  ARMOURED x 3 ·
MORE CREATURES` (Vellum archetype, Bone affix WORDS). *(RENAMED: "THE WAVE THAT ENDED IT" → "ENDED BY";
raw affix enums → plain words via one shared `AffixWords`.)*

**Zone C — diagnosis** y 300-420, full width, dark translucent surface (QUIET, no frame) with a 5 px gold
rule on the left (gold = the important thing):
- line 1 Headline 24 Vellum: `MAIN LIMIT — REACH`
- line 2 Body 19 Bone, sentence case: `Your attacks reached only 1 of the 3 creatures in each wave.`
- line 3 Secondary 16 (lifted with the global rung work) Slate: `WHAT TO LOOK AT — hits per cast.`
Source: `RunReport.MainLimit` (new accessor over existing fields) + `Explain()`; no per-skill "best
performer" until the ledger exists (§4). *(REMOVED: the `POINTS AT` column — its content becomes line 3.
MOVED: the verdict from a bare sentence to this block.)*

**Zone D — two columns** y 440-800:
- **D-left** x 400-1020 (620 wide) `THE NUMBERS · LAST 3 WAVES` header Body 19 Slate; five rows at pitch 56:
  label Body 19 Bone left, figure Headline 24 Vellum right-aligned at x 900, unit Body 19 Slate after it.
  The row the diagnosis names gets the same gold left rule as Zone C. Hairlines between rows as today.
  One formatter per measure (fixes P2-1).
- **D-right** x 1060-1520 (460 wide) `SINCE YOUR LAST RUN HERE` header **Headline 24 Gold** (it is the core
  concept — rank it so); rows at pitch 48: label Body 19 Slate; `was → now` Body 19 (Bone → Vellum); delta
  chip Secondary 16 coloured by `Improved`, "no change" as text not `+0`. Empty state, Body 19:
  `FIRST RUN IN UMBRAL REACH — nothing to compare yet.` *(MOVED: the diff from under the table, Secondary, to
  a right column at Headline rank.)*

**Zone E — footer** y 830-900:
- left: `‹ OLDER` and `NEWER ›` as QUIET buttons 180x56 at x 400 and x 596 (bronze/thin frame, not gold);
  keys Left/Right unchanged.
- right: **primary** `[ADJUST BUILD]` gold 280x64 ending at x 1520; secondary `[GEAR]` quiet 200x64 to its
  left. Both close the log and open the screen (the host already routes B/G; wire through `Wants*` flags like
  `WantsLog`). No RETRY — the champion already regroups. *(ADDED. REMOVED: the `‹ › STEP THROUGH THE LOG · L
  CLOSES IT` sentence.)*

**Empty log** (Count == 0): draw the same panel, header and close icon; centre: `NO EXPEDITIONS YET`
(RegionTitle) + Body 19 `Your first report is written the moment your champion falls or stalls. Every report
stays here.` No footer buttons.

**Typography floor for this screen:** nothing below the Secondary rung, and Secondary only for chips. With
the global UI-scale work (§4) at 125% this puts body at ≈16 px physical on a 720p display.

**Fallen banner (HUNT, §22)** — keep the two-line plate at (500,214,920,110) (moved 14 px down so it clears
the enemy strip): line 1 `YOUR CHAMPION FELL AT WAVE 14` as today; line 2 replaced by the report's
`MAIN LIMIT — REACH · 1 of 3 creatures per cast` (from `Log.Newest`), Body 19 Bone; the plate is a click
target that opens the log, and the L hint moves into the hover tip. *(REMOVED: "THE FULL REPORT IS IN THE
LOG — PRESS L".)*

**Welcome back (§25)** — a small PRIMARY panel (gold frame, it is the one thing on screen) `(610, 300,
700, 400)` with `[CONTINUE]`, replacing the 7 s toast when `credited ≥ 60 s`: `WELCOME BACK` /
`AWAY 6h 42m` / `HUNT +11,320 Gleam · 47 waves · fell 3 times` / `WARREN +7,100 Gleam · +1,240 Dust ·
+310 Scrap · +12 Essence`. Only lines with a non-zero figure. No NOTABLE block until offline produces
notable things (§4). Keep the short-trip toast for `credited < 60 s`.

---

## 7. Fixtures

**Exist today**
- `RH_SHOT_MODE=runlog` (Game1.cs:1718-1832): dresses the champion, conquers two regions, sets
  `umbral_reach`, runs `DevRunToDeath` x3 (SoloExpeditionScreen.cs:3735-3760) and `ToggleLog()`. Listed in
  `tools/asset-pipeline/capture.sh:13`. Produces exactly `baseline/runlog.png`.
- `RH_SHOT_MODE=fightreport` (Game1.cs:1811-1813): one `DevRunToDeath`, banner shown, log closed.
- `fightfall` (RH_SHOT_T) for the collapse itself.

**Gaps against the brief (§95 does not list a LOG fixture at all — it needs one)**
All three `runlog` entries are the same build in the same region, so they differ only by RNG: every entry
has the REACH verdict, the same wall, a numeric-only diff. Not photographed, therefore never reviewed:
1. a STALLED entry (gold band, stall verdict) — `WaveOutcome.Stalled`;
2. a `NEW RECORD` chip (the fixture's honest `isRecord` is false after the first death);
3. a cross-region entry — `OlderThan` returns null → the `NO EARLIER RUN…` empty diff;
4. a WALL archetype change → the only NAMED diff row (`DiffEntry.Named`, RunReport.cs:105-106);
5. the armour verdict (the longest sentence; the only one that can wrap to two lines) and the sustain and
   out-scaled verdicts;
6. the empty log (`Log.Count == 0`) — the state that today has no close icon;
7. a 720p capture of each (the brief §96 asks for one per screen pass).
A `runlog` fixture that seeds the log through `RunLog.Restore` with five authored `RunReport`s (two regions,
one stall, one record, one wall change) — real domain data, deterministic, no simulation needed — covers
1-6 in one capture plus an `RH_SHOT_LOGINDEX` env var to page.
- For the welcome-back panel: `RH_SHOT_MODE=fight` already poses `_bootMessage` (Game1.cs:1793); a
  `welcome` mode should pose a `WarrenYield` with all four currencies and an `OfflineHunt.Result` with
  waves/falls so the full panel renders.

---

## 8. Priority order for the pass

1. P0-1/2/5 — rungs, rank of the diff, chrome under the scrim (foundation; pairs with the global type work).
2. P0-3/4 — `RunReport.MainLimit` + diagnosis zone, drop `POINTS AT` (Core accessor + screen).
3. P1-1/2/3 — doors to BUILD/GEAR, region name, empty-log panel.
4. P1-7 — `AffixWords` shared by strip, log, banner.
5. P1-8, P1-9 — banner diagnosis line; welcome-back panel.
6. P2s with the layout rewrite (they fall out of it).
7. Fixture: authored five-report log + 720p captures before marking the screen done (§96).
