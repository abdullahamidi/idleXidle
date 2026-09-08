export const meta = {
  name: 'polish-density-reflow-resume',
  description: 'RESUME: per-screen UI SCALE density reflow - agents inherit and review their predecessors uncommitted worktree edits, then finish with captures as proof',
  phases: [{ title: 'Reflow', detail: 'one screen per agent, worktree per agent' }],
}

const BASE = '645db62'
const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'
const WT = 'C:/Users/sanan/wt'

const SCHEMA = {
  type: 'object',
  properties: {
    screen: { type: 'string' },
    branch: { type: 'string' },
    commit: { type: 'string', description: 'the commit hash on the branch, or "" if nothing was committed' },
    filesChanged: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string', description: '3-6 sentences: what the screen does now at 125 and 150 that it did not' },
    changes: { type: 'array', items: { type: 'string' }, description: 'each concrete change, with file:line' },
    captures: { type: 'array', items: { type: 'string' }, description: 'absolute paths of the captures you VIEWED and judged clean, with the profile each is at' },
    limitations: { type: 'array', items: { type: 'string' }, description: 'anything still wrong, cut, or not verified - be honest' },
    sharedRequests: { type: 'array', items: { type: 'string' }, description: 'helpers you wanted in UiKit/UiMetrics but had to write locally' },
    verification: { type: 'string', description: 'exact results of build, Game tests, gates' },
  },
  required: ['screen', 'branch', 'commit', 'filesChanged', 'summary', 'changes', 'captures', 'limitations', 'sharedRequests', 'verification'],
}

const PRE = (screen, files, branch) => `You are a UI programmer on IDLExIDLE, a MonoGame 3.8.4.1 / C# idle game. Repo: ${REPO} (branch feat/hunter-cutout-rig; the reflow base is ${BASE}). You will work in YOUR OWN git worktree so eleven colleagues can work in parallel.

THIS IS A RESUME. A colleague started this exact task earlier and was cut off mid-work (a session limit, not a judgement on the work). Their worktree MAY already exist at ${WT}/${screen} on branch ${branch}, possibly with UNCOMMITTED edits to your files. First:

  cd ${REPO}
  if [ -d ${WT}/${screen} ]; then cd ${WT}/${screen}; git status --short; git diff --stat; else git worktree add -b ${branch} ${WT}/${screen} ${BASE}; cd ${WT}/${screen}; fi

If there IS a diff: read it in full (git diff), build it (dotnet build src/IdleXIdle.Game -v q --nologo), and judge it against the task below. Keep what is right and finish what is half-done; if a change is wrong or unbuildable beyond repair, revert that file (git checkout -- <file>) and do it properly. Do NOT throw away good work to start clean, and do NOT keep work you have not read. Then continue with the task as written.

Do ALL work, builds and captures inside ${WT}/${screen}. Never edit files under ${REPO} directly. Windows + Git Bash: use forward slashes; the Bash tool corrupts a literal "\\b" in heredocs (it becomes a backspace byte) - write any Python you need to a file with the Write tool and run it.

## The change you are part of (read this carefully)
UI SCALE (100 / 125 / 150 %) is now a DENSITY PROFILE, not a canvas zoom (UI polish brief sec.7-sec.11; production/audit/ui-polish/BRIEF.md and PLAN.md in the repo). The page UiKit.Page stays 1920x1080 at every profile. What grows:
- every UiTypography rung (Body, Headline, Caption ...) is now a profile-scaled PROPERTY: Body is 22 / 28 / 33 px. Every text call already scales. UiTypography.Pitch(rung) follows.
- UiMetrics (src/IdleXIdle.Game/UiMetrics.cs) is THE one source for everything else: UiMetrics.Text(px), UiMetrics.Control(px) (full factor: 1.25 / 1.5 - for row pitches, button heights, icon boxes, chip heights, hit targets), UiMetrics.Space(px) (half rate: 1.125 / 1.25 - for pads, gaps, insets), and the named sizes RowHeight, ButtonHeight, ButtonHeightSmall, ButtonHeightPrimary, IconSize, IconSmall, PanelPadding, Gap, ScrollbarWidth, HitTargetMinimum, InspectorWidth(pageWidth).
- The panel grid (UiTypography.PanelTitleTop / PanelCaptionTop / PanelBodyTop / PanelBodyTopBare / ModalTitleTop / PanelPadBottom / ButtonPadX / ChipPadX / ChipPadY / HairlineGap) already follows the profile. PanelPadX / PanelPadNarrow / PanelCorner / SquareFrameDrop are ART geometry and stay constant.
- The cursor the host hands your Update/Draw is ALREADY in page space (Game1.PageCursor, floored once) - hit-test the parameter as is, never convert it (tools/check_mouse_space.py refuses conversions).
- UiKit.Button now has the standard states itself (hover eases in, pressed drops 2 px while held, disabled keeps a readable label). UiKit.ScrollBar(b, track, first, visible, total) draws a scrollbar; UiKit.Scrolled(first, wheel, visible, total) clamps a wheel step. UiMotion.Ease(key, target) / Flash(key) / Pulse(key) exist for small eases (UiMotion.Reduced honours Reduced Motion).
- Read UiMetrics.cs, UiTypography.cs and the relevant parts of UiKit.cs first.

## Your task - make ${screen} correct at 100, 125 and 150 % (brief sec.8-sec.9, sec.17-sec.18, sec.107, sec.120)
Files you may edit: ${files}. Nothing else - not Game1.cs, UiKit.cs, UiMetrics.cs, UiTypography.cs, not another screen, not Core. If you need a shared helper, write a private one in your file and list it under sharedRequests.

1. Every literal that is a row pitch, slot pitch, button height, chip height, icon/glyph box, tab height, card height, padding, gap or inset must come from UiMetrics: name the base at 100 % and call Control() or Space() (or the named size where the semantic matches). Never write "* 1.25f" or a per-screen factor. Literals that are ART geometry (a frame's corner reach, a texture's source pixels) stay.
2. Derive every vertical rhythm from the height that is there: rows per panel = (available height) / pitch, clamped; never let rows print over a footer, a button, or the next row. The 125 % overflows UX V2 found and the 150 % overflows that got the step withdrawn were all "assumed 1080 px of height".
3. Where the content genuinely no longer fits at 150 % (brief sec.9): introduce a wheel-scrolled region with UiKit.ScrollBar where sec.18 allows (inventory, bag, long inspectors, long lists, tree details), reduce columns (an inventory can drop from 4 to 3 columns at 150), stack controls, wrap or shorten secondary text. NEVER shrink a font to make it fit (sec.17) - the profile exists to make text bigger. Primary actions (the panel's one lit button, OPEN ALL, EQUIP, TRAIN, the close icon of a modal) stay anchored and reachable - never below a scroll region.
4. Hover and selected remain visually distinct; the drawn rectangle of a control is the rectangle it hit-tests (LAW 5). Interactive targets should be at least UiMetrics.HitTargetMinimum tall where the layout allows (sec.107).
5. Any "static readonly Rectangle" whose value depends on UiMetrics or a rung must become a property (a static readonly is frozen at class load and cannot follow the setting). Static rects that are pure page anchors may stay.
6. Do NOT redesign the screen (brief sec.1, LAW 1): keep its information architecture, its columns, its copy. This is a reflow, not a layout pass. Do not add information, panels or decoration.
7. Strings may only use ASCII plus the few marks tools/check_font_coverage.py lists as proven (middle dot, multiplication sign, dashes, arrows, angle quotes, ellipsis) - it refuses every other glyph, so a filled or hollow circle, a triangle or a tick is DRAWN as a shape, never typed.

## How you prove it (mandatory - LAW 17: nothing is done until it is looked at)
The capture rig renders the real game headless: bash tools/asset-pipeline/capture.sh <mode> build/shots/<name>.png - it builds first, then saves a 1920x1080 PNG at frame 60. Environment dials: RH_SHOT_UISCALE=100|125|150 (the profile), RH_SHOT_WINDOW=1280x720 (a REAL small window; the capture is then the presented backbuffer), RH_SHOT_PAGE_MOUSE=x,y (pose the cursor in page space for a hover/tooltip), plus the per-mode dials documented at the top of capture.sh. Modes for your screen: ${MODES[screen]}.
- Capture EVERY listed mode at 100, 125 and 150, and at least the main mode with RH_SHOT_WINDOW=1280x720 RH_SHOT_UISCALE=125 and RH_SHOT_UISCALE=150. Name files build/shots/<screen>-<mode>-<profile>.png. VIEW every capture with the Read tool and judge it: no text over text, no text over a button, no control under a footer, no clipped last row, no panel past the page edge, no hover/selected confusion. Iterate - edit, re-capture, re-view - until every capture is clean. Compare the 100 % capture against the state before your change (capture it first, from ${BASE}, before you edit) to make sure 100 % did not regress: at 100 % the screen must look the same as before (a few pixels of derived arithmetic are fine; a moved column is not).
- Then run, in your worktree: dotnet build src/IdleXIdle.Game -v q --nologo (0 errors); dotnet test tests/unit/IdleXIdle.Game.Tests --nologo (all green - it checks every static layout rect at all three profiles by reflection); bash tools/check_all.sh (must end with "all gates green"; check_ui_type.py refuses a bare number in a text-size or lineH/rowH/pitch slot - name it or take it from UiMetrics; check_page_anchors.py refuses 1920/1080/960 in layout members of converted screens).

## When you are done
git add ONLY your files; git commit with a Conventional Commit message ("feat(ui): <screen> reflows to the density profile - ...", body says what moved to UiMetrics, what scrolls, what reflows; end the body with "Story: UI-POLISH P2 (UI scale)"). Report the branch and hash. Leave the worktree in place. If the worktree build fails with a path-too-long error, say so in the report immediately. Return the structured result; be honest in limitations - a screen you could not make clean at 150 is reported, not hidden.`

const MODES = {
  loadout: 'weave (BUILD; the fixture poses four slots, keystones, the skill tree; RH_SHOT_POSE=<vowId>[,0..1] opens the vow list)',
  mastery: 'buildtree (first-open framing), buildzoom with a third argument zoom e.g. "bash tools/asset-pipeline/capture.sh buildzoom out.png 0.95", attune / attuned (the specialisation ceremony modal) - RH_SHOT_NODE=<node id> pins the inspector',
  traits: 'dust (the tree; third argument = zoom, 1.2 frames a road), traitlit and traitterm (the purchase flourish; third argument = seconds), RH_SHOT_NODE=<trait id> selects a trait for the inspector',
  gear: 'character (GEAR with a full bag and a selected item), itemmenu (the right-click menu open). ItemTooltip.cs is yours too: its line heights are hand-set and must become Pitch-based; it is drawn on GEAR, FORGE and VAULT hovers - pose one with RH_SHOT_PAGE_MOUSE over an inventory cell',
  forge: 'forge (RH_SHOT_ITEM=dev_hero|dev_rung|dev_cap|dev_low and RH_SHOT_FILTER=all|gear|gems pose every item state), reforge (the RE-ROLL tab), forgeempty, lootforge (the chest reveal overlay - drawn by the host in canvas space from ForgeScreen; keep it readable at 150 too)',
  vault: 'vault, vaultmany, vaultempty, vaultfilter (the CHEST FILTER popover), vaultsell, vaultfirst, trader (the trader modal)',
  training: 'stats (TRAINING; RH_SHOT_SELECT=<stat> selects a row), trainingpoor, trainingreset',
  roster: 'roster, rosterlocked, rosterswitch',
  warren: 'warren, warrenready, warrenfresh',
  map: 'map, mapdeep, maplocked, world, conquered',
  hunt: 'fight (STANDARD), fightfive (five slots), fightstatus (chips), fightshieldbroken (the shield strip), fightreport and fightfall (the fall plate), runlog (the EXPEDITION LOG modal - HuntScreen.DrawLog), boss. The fight is NOT inset: it draws at scale 1 in 1920 space and receives Game1.ChromeMouse. Its HUD rects are static readonly literals (HunterCard, SkillStrip, EnemyStrip, LogButtonRect, LogPanel ...): the hunter card and the skill strip must derive their heights from the text they hold and the arena must give up that room (the arena shrinks; HUNT never scrolls - sec.18). The welcome panel, toasts and tour cards are Game1 chrome and belong to a colleague.',
  chrome: 'settings (the SETTINGS modal), settingsopen (RH_SHOT_DROPDOWN=mode|size, a dropdown list open), help (F1 sheet), welcome (the WELCOME BACK panel over the fight), intro (RH_SHOT_STEP=n, the first-run tour cards over HUNT), tour with a third argument screen e.g. "bash tools/asset-pipeline/capture.sh tour out.png Stats 1", plus any page screen for the nav rail / currency pills / hint slot / toasts (fight shows the boot toast; stats shows the hint slot). Your file is Game1.cs ONLY - the chrome: the nav rail tiles (label + icon must fit the 98-px tile at 150 - derive the tile\'s layout from the label rung; the rail itself stays 180 wide), the currency pills (the row must not crowd the stage header - right-align from the gear icon and let Pill sizes come from UiMetrics only if UiKit\'s Pill signature allows; it does not, so keep pills as they are and only make sure they do not overlap), the settings modal (this is the big one: rows and headers from UiMetrics; the panel grows with the profile and, when the content is taller than the page allows, the panel content SCROLLS with UiKit.ScrollBar while the title, the close icon and the UI SCALE row stay reachable - brief sec.83-sec.85: at 150 % the player must always be able to reach Settings -> UI SCALE and switch back; the hover-tip zones are stale literals (DrawSettingsTips) and must be derived from the row rectangles you lay out), the dropdown list (row height from UiMetrics.RowHeight), toasts / hint slot / lesson card / tour card (heights from their text), the help sheet, the welcome panel, the title menu. The static readonly Settings* rectangles must become properties. Apply UI SCALE immediately when the row is clicked (it already calls ApplyUiScale via CycleUiScale - keep it immediate). Do NOT touch the fixture-seeding block (lines ~1520-2500), ReadCursor, the cursor properties, or any screen call.',
}

const SCREENS = [
  { key: 'loadout',  branch: 'polish/loadout',  files: 'src/IdleXIdle.Game/LoadoutScreen.cs' },
  { key: 'mastery',  branch: 'polish/mastery',  files: 'src/IdleXIdle.Game/MasteryScreen.cs and src/IdleXIdle.Game/StyleAffinityDiagram.cs' },
  { key: 'traits',   branch: 'polish/traits',   files: 'src/IdleXIdle.Game/TraitsScreen.cs' },
  { key: 'gear',     branch: 'polish/gear',     files: 'src/IdleXIdle.Game/GearScreen.cs and src/IdleXIdle.Game/ItemTooltip.cs' },
  { key: 'forge',    branch: 'polish/forge',    files: 'src/IdleXIdle.Game/ForgeScreen.cs' },
  { key: 'vault',    branch: 'polish/vault',    files: 'src/IdleXIdle.Game/VaultScreen.cs' },
  { key: 'training', branch: 'polish/training', files: 'src/IdleXIdle.Game/TrainingScreen.cs' },
  { key: 'roster',   branch: 'polish/roster',   files: 'src/IdleXIdle.Game/RosterScreen.cs' },
  { key: 'warren',   branch: 'polish/warren',   files: 'src/IdleXIdle.Game/WarrenScreen.cs' },
  { key: 'map',      branch: 'polish/map',      files: 'src/IdleXIdle.Game/MapScreen.cs' },
  { key: 'hunt',     branch: 'polish/hunt',     files: 'src/IdleXIdle.Game/HuntScreen.cs' },
  { key: 'chrome',   branch: 'polish/chrome',   files: 'src/IdleXIdle.Game/Game1.cs' },
]

const NOTES = {
  loadout: 'Observed at 150 % before any change: the slot rows overprint (name / line 2 / line 3 inside a 90-px SlotH), the library tiles\' ACTIVE/PASSIVE captions print into the next tile (LibRowPitch 62 / LibTileH 52), keystone chips crowd the bench, variation cards and reinforcement chips are tight. The inspector\'s lines are OK but its RESPEC / COPY / button stack must derive from the button height. The three columns\' widths are page shares and stay; consider the skills column becoming a scroll region at 150 rather than shrinking anything.',
  gear: 'Observed at 150 % before any change: the header stack (portrait, name, class, WEARS..., innate line) overprints; slot labels print into the slot rings (SlotPitch clamp 74-130, SlotBox); the right-click menu rows are tight; the inventory 4x6 grid should become 3 columns at 150 with wheel scrolling; the inspector flows but its verb row + EQUIP button stack must come from ButtonHeight. ItemTooltip: hand-set line advances (34, 26, 32+14 ...) must follow Pitch(rung) and its HeightFor must agree with Draw.',
  forge: 'Observed at 150 % before any change: the screen mostly holds - bag rows (40 px) are tight against 33-px text, the tab strip, the ChangeRows, the feedback plate and the price lines must come from metrics; the questions (socket / crush / scrap / junk) and the reveal overlay sizes must too. The Tight branch (ForgePanel.Height < 700) already steps sizes - fold it into metrics rather than keeping two ladders.',
  chrome: 'Observed at 150 % before any change: the SETTINGS title collides with the two column headers; ASK BEFORE SELL OR SALVAGE overprints its button; the UI SCALE caption overprints the AUDIO header; the DANGER ZONE text overprints its button; the toggle column is fine. The nav rail labels fit at 150 (TRAINING is the widest).',
  hunt: 'Observed at 150 % before any change: the hunter card\'s name line overprints POWER; the skill strip fits but its slot text is tight; the stage header and enemy strip fit. Keep the arena visually dominant (sec.61).',
  mastery: '', traits: '', vault: '', training: '', roster: '', warren: '', map: '',
}

phase('Reflow')
const results = await parallel(SCREENS.map(s => () =>
  agent(PRE(s.key, s.files, s.branch) + (NOTES[s.key] ? '\n\n## Notes from the lead\n' + NOTES[s.key] : ''),
        { label: `reflow:${s.key}`, phase: 'Reflow', schema: SCHEMA })
    .then(r => r ? { key: s.key, ...r } : { key: s.key, failed: true })))

const out = {}
for (const r of results.filter(Boolean)) out[r.key] = r
log(`reflowed ${Object.values(out).filter(r => !r.failed && r.commit).length}/${SCREENS.length} screens committed`)
return out