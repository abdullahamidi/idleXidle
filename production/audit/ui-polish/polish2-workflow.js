export const meta = {
  name: 'ui-polish-p3-p6-screens',
  description: 'UI polish Phases 3-6 per screen: feedback, motion, audio cues in worktrees, each verified',
  phases: [
    { title: 'Implement', detail: 'one agent per screen in C:/Users/sanan/wt/<screen> on polish2/<screen>' },
    { title: 'Verify', detail: 'a second agent builds, captures, judges and fixes small things' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'
const WT = 'C:/Users/sanan/wt'
const BASE = '4e4cb16'

const SCREENS = [
  {
    key: 'hunt', name: 'HUNT', files: 'src/IdleXIdle.Game/HuntScreen.cs (plus a Game test file if you add one)',
    modes: 'fight, fightshield, fightshieldbroken, fightstatus, fightfive, fightmulti (RH_SHOT_T=<seconds>, RH_SHOT_LEAD, RH_SHOT_DUMP=1 - read the header of tools/asset-pipeline/capture.sh)',
    secs: '61-64 (HUNT), 65-70 (SHIELD)',
    spec: [
      'SHIELD HUD on the reused bar art: BarArt(..., "shield") (AssetLibrary aliases ui_bar_shield_frame / ui_bar_shield_fill to the mana bar art), drawn THINNER than the health bar, with the icon_shield glyph before the word SHIELD; the bar appears only while a shield exists; the health bar keeps its place.',
      'ShieldGained -> sfx_shield_gain + a quick rim build on the bar (UiMotion.Flash, Transition). ShieldAbsorbed -> sfx_shield_hit + a small impact at the barrier (a short Flash on the bar or a one-frame notch, never a screen shake). ShieldBroken -> the fx_shield_break strip (assets/art/VFX/shield_break/fx_shield_break_strip8_512.png, AssetLibrary key fx_shield_break, 8 frames in a row, play once at the champion over roughly Reward length) + sfx_shield_break: the loudest non-boss moment, so it replaces the current sfx_champ_down 0.30 played for the shield. Keep the shell subtle: no permanent glow.',
      'A Reaction blow (JAWS on bite etc.) is no longer captioned CRITICAL: a crit is an expected value there. The callout carries the skill\'s own name (e.g. "-4 JAWS"). Find where the callout text is built and change only that case; a true critical from a basic or active hit keeps CRITICAL.',
      '_shieldSeen resets when a new run starts, so SHIELD BROKEN copy can teach again next run. Presentation priority per sec. 63; the multi-hit fold ("-N x5") stays.',
      'Skill strip states already read in words (READY / ACTIVE / ON BITE); make sure nothing on it flashes continuously; a skill firing may pulse its tile ONCE (UiMotion.Flash keyed to the cast event).',
      'This screen holds its own SoundBank (HuntScreen.Sound): play the three shield cues there, throttled by SoundBank (MinGapMs); everything else stays as it is.',
    ],
  },
  {
    key: 'loadout', name: 'BUILD', files: 'src/IdleXIdle.Game/LoadoutScreen.cs',
    modes: 'weave (RH_SHOT_PAGE_MOUSE=x,y for a hover)', secs: '39-44 (BUILD), 2-6 (duplicate skill copy)',
    spec: [
      'Equip: the target loadout slot pulses once (UiMotion.Flash at Transition, keyed to the slot rect); the inspector content fades in over Fast when the selection changes (UiMotion.Ease on an alpha keyed to the inspector); a variation choice brightens its branch in the skill tree once; a reinforcement taken pulses once; RESPEC stays calm (no pulse).',
      'ALREADY EQUIPPED IN SLOT N refusal (LAW 13, already in copy) -> cue sfx_error; a refused click also gives the pressed state so the click is acknowledged. Expose public string? ConsumeCue() (TraitsScreen shape) and list the cues under hostWiring; the host keeps sfx_weave / sfx_bind.',
      'Every custom-drawn row and cell (loadout slots, the skill grid cells, the tree nodes) shows hover / pressed / selected / disabled states per sec. 22-29; disabled cells say why in one plain line (already mostly there - verify each).',
    ],
  },
  {
    key: 'gear', name: 'GEAR', files: 'src/IdleXIdle.Game/GearScreen.cs and src/IdleXIdle.Game/ItemTooltip.cs',
    modes: 'character, fightgear, itemmenu (RH_SHOT_PAGE_MOUSE for a hover)', secs: '48-52 (GEAR and sets), 45-47 (Source and Style tokens)',
    spec: [
      'Equip: the target equipment slot pulses once (UiMotion.Flash); the GEAR POWER number TICKS from the old value to the new over Transition (UiMotion.Ease on the displayed number, the true value shown when Reduced) and flashes once; a newly reached set ladder rung reveals with one pulse.',
      'The set ladder rungs are drawn as SHAPES (filled / hollow discs via _ui.Fill or the circle helper you find in UiKit), never as the glyph characters "filled circle" / "hollow circle" (the font gate forbids them); the capstone rung shows icon_set_<capstone> (assets/art/UI/icons/sets/icon_set_*.png - read the six file names; AssetLibrary keys are the basenames) tinted by the set\'s Source colour; empty cells stay quiet (no pulse, no border glow).',
      'First five-piece completion of a set: record the set in the save\'s SaveGame.CompletedSets (the field exists; find where the screen can reach the save or the hunter - if the screen cannot reach the save, expose the completed-set name via public string? ConsumeCompletedSet() and let the host record it; say which in hostWiring) and expose a one-time notice "MACHINE SET COMPLETE / PLATING ACTIVE" (two lines: set name + set bonus name, from the set catalogue, no invented copy) via public string? ConsumeNotice(); the host will toast it and play sfx_levelup. Never repeat for a set already in CompletedSets.',
      'Inventory cells, filter tabs and the slot buttons: hover / pressed / selected / disabled states per sec. 22-29; a locked or unusable cell says why in the tooltip.',
    ],
  },
  {
    key: 'forge', name: 'FORGE', files: 'src/IdleXIdle.Game/ForgeScreen.cs',
    modes: 'forge (RH_SHOT_ITEM=dev_hero|dev_rung|dev_cap|dev_low, RH_SHOT_FILTER=all|gear|gems), lootforge, reforge, forgeempty', secs: '53-55 (FORGE)',
    spec: [
      'On an operation the before -> after values TICK to the new number over Transition and flash once (UiMotion.Ease / Flash; Reduced shows the end value at once); the materials strip pill that paid reacts (a short flash on the pill and its number ticking down); a brief forge flash on the item art (an additive white flash fading over Fast, no screen shake).',
      'Cues per operation via public string? ConsumeCue(): UPGRADE / GREATER UPGRADE -> sfx_upgrade; RE-ROLL -> sfx_reroll; SOCKET -> sfx_gem; SALVAGE / MERGE -> sfx_salvage. The host currently plays sfx_forge for all of these from Game1 (around the ForgeScreen consume calls); list under hostWiring that the host should play the screen\'s cue and drop its own sfx_forge for these operations.',
      'Tabs, bag rows, socket cells, the two action buttons: hover / pressed / selected / disabled states per sec. 22-29; a disabled action says what it needs (already in copy - verify each).',
    ],
  },
  {
    key: 'vault', name: 'VAULT', files: 'src/IdleXIdle.Game/VaultScreen.cs',
    modes: 'vault, vaultfilter, vaultempty, vaultemptyfilter, vaultsell, vaultmany', secs: '56-60 (VAULT)',
    spec: [
      'OPEN: the card responds at once (pressed state on mouse-down, a Flash on release); the reveal stays short (within Reward); emphasis scales with rarity (the ring count already scales - keep it; Epic and up may add ONE extra pulse, never a longer wait).',
      'Cues via public string? ConsumeCue(): an open -> sfx_chest_open, and for Epic or better additionally sfx_chest_rare (two names in one frame is fine: return them joined by a comma and document it, or expose ConsumeCues() returning a list). OPEN ALL aggregates (already) and plays the pair once. The host currently plays sfx_forge here; list under hostWiring.',
      'Confirm PASTE A CODE (the plate at the far left) never outranks OPEN / OPEN ALL visually: plain plate, no gold, no pulse; TRADER / CHEST FILTER / OPEN ALL keep the states of sec. 22-29.',
    ],
  },
  {
    key: 'training', name: 'TRAINING', files: 'src/IdleXIdle.Game/TrainingScreen.cs',
    modes: 'stats, trainingpoor, trainingreset (RH_SHOT_PAGE_MOUSE for a hover)', secs: '39-44 (TRAINING and BUILD)',
    spec: [
      'On TRAIN: the cost pill reacts (a short flash and the GLEAM number ticking down), the NOW -> AFTER value in the row and in the inspector ticks to the new number and flashes once, the rank bar EASES to its new length over Transition (UiMotion.Ease keyed to the row); the inspector updates in place, no modal.',
      'The host plays sfx_click when ConsumeTrain() succeeds; list under hostWiring that it should play sfx_train instead (no screen change needed for that) and sfx_error when a TRAIN is refused for gleam (expose ConsumeCue() returning sfx_error at that moment).',
      'Rows: hover / selected / pressed / disabled states per sec. 22-29 (already distinct - verify pressed exists); the RESET plate\'s disabled reason stays one plain line.',
    ],
  },
  {
    key: 'traits', name: 'TRAITS', files: 'src/IdleXIdle.Game/TraitsScreen.cs',
    modes: 'dust, traitlit (RH_SHOT_NODE=<id>[@row], RH_SHOT_PAGE_MOUSE)', secs: '73-82 (MAP / ROSTER / TRAITS / WARREN)',
    spec: [
      'Road framing (a click on a road\'s name, keys 1-4, ALL / HOME) EASES pan and zoom to the target over Transition instead of jumping (UiMotion.Ease on both; Reduced jumps as today); the first-open framing is kept exactly.',
      'A purchase pulses along the lit connection from the prerequisite to the taken node (a travelling highlight over Reward, keyed to the purchase; Reduced: the connection simply lights); a terminal taken is stronger (the existing flourish - keep it, do not lengthen it).',
      'The foot band under the tree (hint / tally / camera buttons, merged at 4e4cb16) and the point plate stay exactly as they are; the camera buttons show hover / pressed states.',
    ],
  },
  {
    key: 'mastery', name: 'MASTERY', files: 'src/IdleXIdle.Game/MasteryScreen.cs and src/IdleXIdle.Game/StyleAffinityDiagram.cs',
    modes: 'buildtree, buildzoom, hybrid', secs: '39-44 (BUILD), 45-47 (Source and Style tokens)',
    spec: [
      'Taking a node: the node pulses once (UiMotion.Flash at Transition) and its connection lights over Fast; NO continuous tree animation - audit every per-frame animated value in this screen and the diagram and make sure each is either a one-shot keyed to an event or honours UiMotion.Reduced (the unchosen-specialisation "breath" must stop breathing under Reduced Motion and hold its brighter state).',
      'Node hover / pressed / selected / disabled states per sec. 22-29; a node you cannot afford says the reason in the inspector in one plain line (already - verify).',
      'The SPECIALISATION ceremony keeps its length; TAKE EVERY POINT BACK stays calm.',
    ],
  },
  {
    key: 'map', name: 'MAP', files: 'src/IdleXIdle.Game/MapScreen.cs',
    modes: 'map, region2, region3, conquered (RH_SHOT_PAGE_MOUSE for a hover)', secs: '73-82 (MAP / ROSTER / TRAITS / WARREN)',
    spec: [
      'Region cards: hover state (a lift of the frame tint over Fast), pressed state, the selected bar (already); a LOCKED card\'s hover says what conquers it (already in copy - verify it shows on hover, not only in the inspector).',
      'A one-time reveal pulse on a region that has JUST become available (unlocked since the last visit): pulse once (UiMotion.Flash at Reward) the first time it is drawn available, then never again this session - remember the set of regions already shown available in the screen\'s own state (no save change).',
      'RESUME HERE / the region action button keep the states of sec. 22-29.',
    ],
  },
  {
    key: 'roster', name: 'ROSTER', files: 'src/IdleXIdle.Game/RosterScreen.cs',
    modes: 'roster, rosterswitch', secs: '73-82 (MAP / ROSTER / TRAITS / WARREN)',
    spec: [
      'SET ACTIVE: the chosen card and the inspector update at once with a short highlight (UiMotion.Flash at Transition on the card and on the inspector header); no confirm dialog (already).',
      'Cue: switching plays sfx_nav - expose public string? ConsumeCue() and list it under hostWiring (the host plays sfx_click today at the switch; say where if you can find it).',
      'Cards: hover / pressed / selected (PLAYING) states per sec. 22-29; READY / PLAYING chips are not buttons and must not look pressed.',
    ],
  },
  {
    key: 'warren', name: 'WARREN', files: 'src/IdleXIdle.Game/WarrenScreen.cs',
    modes: 'warren, warrenready, warrenfresh', secs: '73-82 (MAP / ROSTER / TRAITS / WARREN)',
    spec: [
      'UPGRADE: the facility\'s LEVEL flashes once, its output number ticks to the new value over Transition, the cost pill reacts (a flash and the number ticking down), and a MILESTONE upgrade (every 5 levels / the level-20 step) is stronger (Reward length, one extra pulse on the card frame).',
      'Cue: an upgrade plays sfx_upgrade; a refused one (NEEDS N GLEAM / REACH WAVE N) sfx_error - expose public string? ConsumeCue() and list under hostWiring.',
      'Facility cards: hover / pressed / selected / disabled states per sec. 22-29; the disabled reason stays one plain line (already).',
    ],
  },
]

const REPORT = {
  type: 'object',
  required: ['commit', 'summary', 'hostWiring', 'limitations', 'evidence'],
  properties: {
    commit: { type: 'string', description: 'short hash of the commit on polish2/<key>, or "none"' },
    summary: { type: 'string', description: 'what feedback and states were added, in a few sentences' },
    hostWiring: { type: 'array', items: { type: 'string' }, description: 'each cue / notice / helper the host must wire, with the method name and the moment' },
    limitations: { type: 'array', items: { type: 'string' } },
    evidence: { type: 'array', items: { type: 'string' }, description: 'capture paths looked at, tests added' },
  },
}

const VERDICT = {
  type: 'object',
  required: ['pass', 'commit', 'fixed', 'remaining'],
  properties: {
    pass: { type: 'boolean' },
    commit: { type: 'string', description: 'the branch head after your own fixes, if any' },
    fixed: { type: 'array', items: { type: 'string' } },
    remaining: { type: 'array', items: { type: 'string' }, description: 'defects left for the merge owner, each with file:line and the capture that shows it' },
  },
}

function rules(s) {
  return [
    'You are the UI programmer for the ' + s.name + ' screen of IDLExIDLE (MonoGame 3.8.4.1, C#, .NET 8), doing the FINAL UI POLISH Phases 3-6 (interaction states, screen feedback, motion, audio cues) for this one screen.',
    '',
    'WHERE: the repository is ' + REPO + '. Your worktree is ' + WT + '/' + s.key + ' on branch polish2/' + s.key + ', already checked out at ' + BASE + ' (the merged density reflow). Work ONLY in that worktree; never touch the main checkout or any other worktree. Use `git -C ' + WT + '/' + s.key + ' ...` and run dotnet / bash from inside that worktree.',
    '',
    'SCOPE: edit ONLY ' + s.files + ' (you may also add a test file under tests/unit/IdleXIdle.Game.Tests or tests/unit/IdleXIdle.Core.Tests). FROZEN this round: Game1.cs, UiKit.cs, UiMotion.cs, UiMetrics.cs, UiTypography.cs, SmoothFont.cs, SoundBank.cs, AssetLibrary.cs, every other screen, and IdleXIdle.Core (no Core edits). A shared helper you need becomes a private method in your screen file, listed under limitations so the host pass can lift it.',
    '',
    'READ FIRST, end to end: production/audit/ui-polish/BRIEF.md sections 22-29 (polish principle, ornament, states), 30-38 (motion and feedback), 86-87 (audio), 102-107 (accessibility, Reduced Motion) and your screen\'s sections ' + s.secs + '; production/audit/ui-polish/PLAN.md, the "Phase 3-5" and "Phase 6" headings (your screen\'s bullet is the spec, repeated below); src/IdleXIdle.Game/UiMotion.cs (Fast 0.10 s / Transition 0.18 s / Reward 0.35 s; Ease(key, target, seconds) returns the eased value; Flash(key, seconds) starts a one-shot; Pulse(key) reads it 1 -> 0; Pulsing(key); Reduced; KeyOf(rect); Smooth(t)); UiKit.Button (hover ease, pressed via UiKit.MouseHeld, disabled with a reason); your screen file end to end (it is long: read it in slices, all of it).',
    '',
    'THE SPEC FOR ' + s.name + ':',
    ...s.spec.map(function (line, i) { return '  ' + (i + 1) + '. ' + line }),
    '',
    'LAWS:',
    '- Every animation goes through UiMotion, so UiMotion.Reduced collapses it to an instant state change with the SAME end state. Timings stay inside the brief\'s bands: 80-120 ms micro, 150-220 ms transition, 250-450 ms reward (use the UiMotion constants). Nothing flashes continuously. A pulse plays ONCE per event, keyed to the event, never re-armed by Draw. Animation state is advanced from Update with dt, never from Draw.',
    '- Feedback, not information (sec. 22): add no label, panel or copy beyond what the spec names. Preserve the screen\'s information architecture and the C6 reflow. Re-check the reflow contract on your captures: no text over text, no text over a button, no control under a footer, no clipped last row, no panel past the page edge.',
    '- States (sec. 22-29): normal / hover / pressed / selected / disabled / focused, each distinguishable at a glance; a disabled control says why in one plain line. UiKit.Button already does this; custom-drawn rows, cards and cells must do it themselves.',
    '- Audio: the HOST owns audio. If your screen already holds a SoundBank (only HuntScreen.Sound does), play there. Otherwise expose `public string? ConsumeCue()` (TraitsScreen has the shape: a private _cue set at the semantic moment, returned once and cleared) and list every cue with its moment under hostWiring. Cue vocabulary that exists (assets/audio/ui, assets/audio/combat): sfx_nav sfx_error sfx_train sfx_reroll sfx_chest_open sfx_chest_rare sfx_shield_gain sfx_shield_hit sfx_shield_break sfx_salvage sfx_upgrade sfx_gem sfx_levelup sfx_equip sfx_forge sfx_weave sfx_bind sfx_click sfx_conquer sfx_deepen sfx_reveal_tick sfx_trait_lit sfx_trait_terminal. Never play from Draw.',
    '- Copy: plain English in game terms; no genre jargon, no abbreviations the player has not been taught.',
    '- Do not add gameplay values as literals: read them from the catalogue / Core objects you already have.',
    '',
    'PROVE IT:',
    '- Build inside the worktree: `dotnet build src/IdleXIdle.Game/IdleXIdle.Game.csproj -c Debug --nologo -v q` (0 errors, no new warnings).',
    '- Capture: `RH_SHOT_UISCALE=100 bash tools/asset-pipeline/capture.sh <mode> build/shots/p2_' + s.key + '_<state>_100.png` and the same at RH_SHOT_UISCALE=150, for your modes: ' + s.modes + '. LOOK at every capture with the Read tool (the image itself). A state no capture can pose has never been looked at (project rule): a transient (a pulse, a tick, a flash, an eased camera) needs a fixture. The rig shoots frame 60 (ShotAtFrame); your screen can add a dev dial the way HuntScreen.DevSeekBefore / TraitsScreen.DevSelect / TraitsScreen._litFrozen do - but a new dial must be CALLED by something, and Game1 is frozen, so: freeze the effect at its peak when an env var YOUR screen reads (Environment.GetEnvironmentVariable("RH_SHOT_" + something), documented in a comment and in the capture.sh-style note in your report) is set, and photograph that. If a state truly cannot be posed, write a Game test that drives the state machine and asserts the displayed values at t = 0, mid and end, and say exactly which state was not photographed and why under limitations.',
    '- Tests inside the worktree: `dotnet test tests/unit/IdleXIdle.Game.Tests --nologo` and `dotnet test tests/unit/IdleXIdle.Core.Tests --nologo` (summaries print in Turkish: "Basarili" = passed, "Basarisiz" = failed; both must be all green). Gates: `bash tools/check_all.sh` must end with "all gates green" (the font gate rejects glyph characters the font lacks - use shapes).',
    '- Commit on polish2/' + s.key + ' with a Conventional Commits message (feat(' + s.key + '): ...) whose body says what feedback was added, what fixture or test proves it, and which host wiring is owed; end the body with "Brief: production/audit/ui-polish/BRIEF.md sec. ' + s.secs + ' (UI polish P3-P6)". Do not push. Do not merge. Do not touch other branches.',
    '',
    'TOOL CAVEATS: the Bash tool mangles backslash escapes inside heredocs - write any script with the Write tool. `rm -rf` is denied; leave capture files in build/shots. Do not run the game outside capture.sh.',
    '',
    'Your final message is data, not prose: return ONLY the structured report.',
  ].join('\n')
}

function verifyPrompt(s, r) {
  const rep = r ? JSON.stringify(r) : '(the implementer returned nothing - inspect the branch yourself)'
  return [
    'You are the reviewer for the ' + s.name + ' screen\'s UI polish Phases 3-6 branch. Adversarial: assume the work is wrong until the captures and the diff prove otherwise.',
    '',
    'WHERE: repository ' + REPO + '; worktree ' + WT + '/' + s.key + ' on branch polish2/' + s.key + ' (base ' + BASE + '). Work only there.',
    'The implementer\'s report: ' + rep,
    '',
    'DO, in order:',
    '1. `git -C ' + WT + '/' + s.key + ' diff --stat ' + BASE + '..HEAD` - fail the branch if any file outside ' + s.files + ' (or a new test file) changed.',
    '2. Read the full diff (`git diff ' + BASE + '..HEAD`) against production/audit/ui-polish/BRIEF.md sections 22-38, 86-87, 102-107 and ' + s.secs + ', and the screen\'s bullet under "Phase 3-5" in production/audit/ui-polish/PLAN.md. Check every law: UiMotion for every animation; Reduced Motion collapses to the same end state; timings within 80-120 / 150-220 / 250-450 ms; nothing continuous; one pulse per event keyed to the event; state advanced in Update, not Draw; no new labels or panels; no literal gameplay values; audio through HuntScreen.Sound or ConsumeCue(), never from Draw; copy plain English.',
    '3. Build and capture yourself inside the worktree: `dotnet build src/IdleXIdle.Game/IdleXIdle.Game.csproj -c Debug --nologo -v q`, then `RH_SHOT_UISCALE=100 bash tools/asset-pipeline/capture.sh <mode> build/shots/v2_' + s.key + '_<mode>_100.png` and RH_SHOT_UISCALE=150 for the modes ' + s.modes + ' and for any dev dial the implementer added. LOOK at every capture with the Read tool. Judge the reflow contract (no text over text, no text over a button, no control under a footer, no clipped last row, no panel past the page edge) and the spec.',
    '4. Run `dotnet test tests/unit/IdleXIdle.Game.Tests --nologo`, `dotnet test tests/unit/IdleXIdle.Core.Tests --nologo` ("Basarili" = passed) and `bash tools/check_all.sh` ("all gates green").',
    '5. Fix what is small and certain (a wrong timing, a missing Reduced branch, a pulse re-armed every frame, a stray file) directly in the worktree, re-verify, and commit on the same branch with a fix(' + s.key + '): message. Leave anything larger as `remaining` with file:line and the capture path that shows it.',
    '',
    'TOOL CAVEATS: the Bash tool mangles backslash escapes inside heredocs - write scripts with the Write tool. `rm -rf` is denied. Never touch the main checkout or another worktree.',
    '',
    'Return ONLY the structured verdict.',
  ].join('\n')
}

phase('Implement')
const results = await pipeline(
  SCREENS,
  function (s) {
    return agent(rules(s), { label: 'p2:' + s.key, phase: 'Implement', schema: REPORT, agentType: 'general-purpose' })
  },
  function (r, s) {
    return agent(verifyPrompt(s, r), { label: 'verify:' + s.key, phase: 'Verify', schema: VERDICT, agentType: 'general-purpose' })
      .then(function (v) { return { screen: s.key, report: r, verdict: v } })
  }
)

const out = results.filter(Boolean)
log('round two: ' + out.length + '/' + SCREENS.length + ' screens reported')
return out
