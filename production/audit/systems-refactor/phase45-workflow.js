export const meta = {
  name: 'systems-refactor-p456',
  description: 'Phases 5, 4 and 6 in parallel worktrees: producers leave the tree, the trait system replaces it, and VFX gets a placement contract',
  phases: [
    { title: 'Build', detail: 'three isolated worktrees, one phase each' },
    { title: 'Verify', detail: 'an adversarial reader rebuilds, recaptures and judges each' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'
const WT = 'C:/Users/sanan/wt'
const BASE = '08116a9'

const GROUND = [
  'REPO: ' + REPO + '. The authoritative branch is `feat/hunter-cutout-rig` at ' + BASE + '.',
  '',
  'READ BEFORE YOU TOUCH ANYTHING:',
  '- `production/audit/systems-refactor/BRIEF.md` - the task, 118 sections. Your sections are named below.',
  '- `production/audit/systems-refactor/AUDIT.md` - what the code IS, mapped by eight readers with file:line evidence. Where the brief and this disagree, the audit wins: the brief was written from outside the codebase and five of its premises are wrong about it.',
  '- `production/audit/systems-refactor/PLAN.md` - the eleven decisions and the eight adjudications. They are SETTLED. Build inside them.',
  '- `production/audit/systems-refactor/DESIGN-catalogues.md` - the finished design for your area. Follow it; where the code refutes it, follow the code and say so in your report.',
  '',
  'WHAT HAS ALREADY LANDED, so you neither redo nor undo it:',
  '- Signature skills exist: `SkillDef.OwnerCharacterId`, `Character.SignatureSkillId`, ten authored skills, the composer refuses another champion\'s signature, the starter loadout carries the champion\'s own.',
  '- Mastery access follows the CURRENT allocation: `MasteryTree.AvailableSkills()` is a pure function of taken nodes, the permanent latch and the account-wide birth-skill leak are deleted, and the skill roads hang off their branch\'s first minor rather than off a specialisation (PLAN D11).',
  '- The respec warns then repairs; a character switch empties a slot holding a foreign signature.',
  '',
  'HOUSE RULES:',
  '- Gameplay values are DATA, never constants in the fight loop.',
  '- A dial, field or method with no consumer is this project\'s named failure mode. Everything you add must be read by something that runs.',
  '- Player copy is plain English in game terms. No jargon, no abbreviations the player has not been taught. The player reads English as a second language.',
  '- Layout is `UiMetrics` arithmetic, never a literal, and must hold at UI SCALE 100, 125 and 150.',
  '- Never delete a test to make a suite pass. Update it to the new truth and say in a comment what changed and why.',
].join('\n')

const REPORT = {
  type: 'object',
  required: ['commit', 'summary', 'designDepartures', 'saveChanges', 'limitations', 'evidence'],
  properties: {
    commit: { type: 'string', description: 'short hash on your branch, or "none"' },
    summary: { type: 'string' },
    designDepartures: {
      type: 'array',
      items: {
        type: 'object',
        required: ['design', 'code', 'didInstead'],
        properties: {
          design: { type: 'string', description: 'what the design said' },
          code: { type: 'string', description: 'what the code showed, with file:line' },
          didInstead: { type: 'string' },
        },
      },
    },
    saveChanges: { type: 'array', items: { type: 'string' }, description: 'every SaveGame field added, stopped or changed, and how an OLD save still loads' },
    limitations: { type: 'array', items: { type: 'string' } },
    evidence: { type: 'array', items: { type: 'string' }, description: 'tests added, captures looked at, final build/test/gate state' },
  },
}

const VERDICT = {
  type: 'object',
  required: ['pass', 'defects', 'confirmations'],
  properties: {
    pass: { type: 'boolean' },
    defects: {
      type: 'array',
      items: {
        type: 'object',
        required: ['severity', 'defect', 'evidence', 'fix'],
        properties: {
          severity: { type: 'string', enum: ['blocking', 'minor'] },
          defect: { type: 'string' }, evidence: { type: 'string' }, fix: { type: 'string' },
        },
      },
    },
    confirmations: { type: 'array', items: { type: 'string' } },
  },
}

const SLICES = [
  {
    key: 'p5',
    title: 'PHASE 5 - THE PRODUCERS LEAVE THE TREE',
    secs: '42-60, 92, 93, 97, 102, 103, 104',
    design: 'the STRUCTURAL UNLOCKS AND SAVE MIGRATION, VOW DISCOVERY and Keystone Acquisition sections of DESIGN-catalogues.md',
    body: [
      'THIS PHASE MUST LAND FIRST AND IT IS WHY: the trait tree is the SOLE producer of all 19 keystones (`DustEffects.LearnedKeystones`) and all 13 vows (`DustEffects.KnownVows`). Phase 4 deletes that tree. If the replacements do not exist when it does, both systems die silently. You are building the replacements. The tree itself STAYS in this phase - you are moving what depends on it, not removing it.',
      '',
      'KEYSTONES (sec.49-52, LAW 11). All nineteen come from world progression instead of nodes. The design maps every one onto a specific event: six conquests, six PARTLY MASTERED, six FULLY MASTERED, and the first corruption deepening. Socket capacity becomes conquest-count milestones, separately from discovery. Preserve every keystone identity - you are reassigning who hands it over, not redesigning it. The conquest that reveals one must SAY so to the player (sec.51).',
      '',
      'VOWS (sec.42-48, LAW 10). Discovery is proof-before-reward: the player obeys the restriction, without the vow equipped, and then it reveals itself. One vow is available openly from the start so the system is discoverable at all. No vow may be unlocked by a random roll or a drop. The design also argues that "vow capacity" should become a real build-level number rather than an accident of slot count - read its argument, decide, and say which you did and why.',
      '',
      'STRUCTURAL UNLOCKS (sec.53-60, LAWS 12-13). Every old node that unlocks a SYSTEM moves to its correct owner or dies, per PLAN decision D9: skill slots to progression alone capped at four (the fifth slot goes - D8, and it is the one capability this refactor deliberately removes); auto-sell and auto-merge into Warren facilities; the Forge gate is already free so only the node NAME that claims otherwise is deleted; the tree numerics with no clean equivalent die.',
      '',
      'THE MIGRATION IS THE HARD PART (sec.58, 94-97). A save that already bought these must not lose them. The design has a table for all 51 node ids. Respect the audit\'s warnings: the ten-second autosave destroys any field the build stops writing, so verify a field is genuinely dead before you stop writing it; `MemoryDustUnlocks` carries four systems in one list; the Dust WALLET must survive the tree\'s deletion untouched; `Activity.Traits` currently gates on trait points, which are DERIVED and are going away; the save version is 3 and a load above it is refused outright; and every gate fact the Unlocks layer reads must be MONOTONE or an announcement re-fires.',
      '',
      'Sweep the player-facing copy that will become false (sec.92, 93): anything saying a keystone or a vow or a slot is learned in TRAITS.',
    ].join('\n'),
    modes: 'map, world, region2, conquered, weave, warren, buildtree',
  },
  {
    key: 'p4',
    title: 'PHASE 4 - THE TRAIT SYSTEM, WITHOUT A TREE',
    secs: '22-41, 90, 96, 100, 101, 109',
    design: 'THE TRAIT CATALOGUE section of DESIGN-catalogues.md, and PLAN.md\'s adjudications on it',
    body: [
      'DO NOT DELETE THE TREE IN THIS PHASE. Another agent is moving the keystone and vow producers off it in parallel; the deletion happens when both land. Build the NEW system beside the old one, and leave `MemoryDustTree`, `DustEffects`, `TraitRoads` and `TraitTreeLayout` untouched.',
      '',
      'TWENTY-SIX TRAITS, not the design\'s thirty. PLAN.md records which four are cut and why, and they are the design\'s own cheapest cuts. Each trait: one sentence a player can hold, one dial, one read site in the fight, and a hidden deterministic discovery rule that RELATES to what the trait does. No trait may be a bare "+5% damage" - sec.34 forbids a catalogue that looks like mastery nodes.',
      '',
      'THE RULES ARE HIDDEN BUT NEVER RANDOM (sec.28-31, LAW 8). Every rule is deterministic, none can be missed, none needs a lucky roll, and none is so obscure a player would only find it by reading source. The player never sees a condition, a counter or a progress bar - an undiscovered trait is `???`.',
      '',
      'DISCOVERY IS ACCOUNT-WIDE; THE LOADOUT IS PER CHARACTER (sec.25-26, LAWS 6-7). Exactly three traits equipped per champion, swapping free and reversible, and per-character trait slots are the one piece of per-character save state this refactor adds.',
      '',
      'THE SCREEN IS NOT A TREE (sec.37-40). Three active slots, the discovered collection, and the unknown as mysterious sigils. Keep the starfield if it still works. The inspector reads one way for a discovered trait and another for an unknown one. It must hold at UI SCALE 100/125/150 and must be posable by the capture rig - a state no fixture can pose has never been looked at, which is this project\'s standing rule.',
      '',
      'THE REVEAL (sec.32): meaningful, rare enough to matter, never a blocking ceremony every few minutes. PLAN.md decides that the six traits an established save discovers retroactively on first load arrive as ONE combined plate rather than six.',
      '',
      'EVERY TRAIT NEEDS A LIVENESS TEST (sec.101): the same fight with and without it, and the observable difference when its condition is met. A decorative trait is a bug.',
      '',
      'Read `WaveMetrics` in `src/IdleXIdle.Core/Expeditions/WaveModel.cs` before designing any counter - the fight already keeps shield absorbed, damage prevented, health damage, kills, hits, duration and per-style damage and casts. Add a lifetime accumulator only where a bounded counter genuinely cannot answer the rule.',
    ].join('\n'),
    modes: 'dust, traitlit, weave, fight',
  },
  {
    key: 'vfx',
    title: 'PHASE 6 - THE VFX PLACEMENT CONTRACT',
    secs: '61-73, 105, 106, 112',
    design: 'The VFX Placement Contract section of DESIGN-catalogues.md',
    body: [
      'A TYPED, SMALL CONTRACT: an anchor vocabulary with only anchors that have real consumers, a relative scale against the subject\'s VISUAL bounds rather than its texture, a normalized offset, and semantic layers. Records and enums - no dictionary of object, no reflection, no scripting layer.',
      '',
      'THE MEASUREMENT ALREADY EXISTS AND IS CORRECT. `UiKit`\'s pad fractions measure a sprite\'s opaque bounds, and `ContentPad` returns a full bounding box and has ZERO consumers today. Expose it, cache it, and build the contract on it.',
      '',
      'THE FACTS THE AUDIT FOUND, which you must design against rather than discover again: champion-side effects anchor to the un-pushed `ChampBox` while the champion is drawn with a lunge offset; enemy effect size is quantised to five integer buckets and the projectile scale to four; `_creatureRect` is one frame stale and falls back to a literal on a wave\'s first frame; all 68 fx strips are 4096x512 and `VfxPlayer` infers the frame count from that aspect, so a regenerated asset at another aspect breaks `Hold` silently; the `_held` dictionary is keyed by asset key, so two persistent effects sharing a strip collapse into one; the additive pass is DELIBERATELY unscissored and must stay so; and three effects - press, weep, wilt - name keys that resolve to no asset at all.',
      '',
      'SHIELD IS THE ACCEPTANCE CASE (sec.106, 71). It must look right on THE SEEKER and on THE MAGPIE, whose drawn widths differ by more than two to one while their heights do not. Give the numbers: anchor, scale against visual height, offset, layer. Prove it with captures of BOTH.',
      '',
      'DO NOT REPLACE THE SHIELD\'S PERSISTENCE MODEL. `WaveReplay.CurrentShield` is accumulated state that already survives a dev seek, which is exactly what sec.69 asks for. Copy its shape for the states that have no visual at all.',
      '',
      'ADD THE DEBUG VIEW (sec.70): actor bounds, target bounds, the anchor point, the effect\'s destination rect. Off in normal play, and posable by the rig.',
      '',
      'LAW 16: do not fix a badly sized asset with a giant runtime multiplier. Where an asset is genuinely wrong, say so and leave it flagged - PixelLab regeneration is a later slice and is not your job here.',
      '',
      'THERE IS NO TEST IN THE REPO TOUCHING VfxPlayer, an actor rect or a pad fraction. You have no baseline. Write characterisation tests FIRST, so you can prove you did not break the working parts.',
    ].join('\n'),
    modes: 'fight, fightshield, fightshieldbroken, fightstatus, fightmulti, boss',
  },
]

const RESUME = [
  'RESUME. A previous agent began this exact task in your worktree and its run died. It may have COMMITTED work on your branch and it may have left UNCOMMITTED edits.',
  'BEFORE ANY EDIT: run `git -C <your worktree> log --oneline ' + BASE + '..HEAD` and `git -C <your worktree> status --short` and `git diff`. Read everything you inherit and judge it against the sections below. Committed work was at least built; uncommitted work may never have compiled. Keep what is right, fix what is wrong, and `git checkout --` any file whose diff is confused enough that starting over is cheaper. Say in your report what you inherited and what you did with it.',
  'If the worktree is clean and has no commits, start from the base as normal.',
  '',
].join('\n')

function buildPrompt(s) {
  return [
    RESUME,
    'You are implementing ' + s.title + ' of the IDLExIDLE systems refactor.',
    '',
    GROUND,
    '',
    'WHERE: your worktree is ' + WT + '/' + s.key + ' on branch `refactor/' + s.key + '`, already checked out at ' + BASE + '. Work ONLY there. Use `git -C ' + WT + '/' + s.key + ' ...` and run dotnet and bash from inside that worktree. Never touch the main checkout or another worktree - two other agents are working in theirs right now.',
    '',
    'YOUR SECTIONS: BRIEF.md ' + s.secs + '. YOUR DESIGN: ' + s.design + '.',
    '',
    s.body,
    '',
    'THE BAR - run these yourself and iterate until they pass:',
    '- `dotnet build IdleXIdle.sln -c Debug --nologo -v q` - 0 errors, 0 warnings.',
    '- `dotnet test tests/unit/IdleXIdle.Core.Tests --nologo` and `dotnet test tests/unit/IdleXIdle.Game.Tests --nologo` - all green. Summaries print in Turkish: "Basarili" passed, "Basarisiz" failed.',
    '- `bash tools/check_all.sh` ends with "all gates green"; `bash tools/check_boot.sh` with "boot green". check_boot catches the failure class that kills the game before the window opens, which no capture can see.',
    '- CAPTURES, and LOOK at each with the Read tool: `RH_SHOT_UISCALE=<100|125|150> bash tools/asset-pipeline/capture.sh <mode> build/shots/<name>.png` for your modes (' + s.modes + '). Judge them: no text over text, no text over a button, no control under a footer, no clipped last row, no panel past the page edge. A state no dial can pose has never been looked at - add a dial and document it in the header of `tools/asset-pipeline/capture.sh`.',
    '- Commit on your branch with Conventional Commits. Do NOT push, do NOT merge, do NOT touch another branch.',
    '',
    'TOOL CAVEATS: the Bash tool mangles backslash escapes inside heredocs - write scripts with the Write tool and run them by path. `rm -rf` is denied. `dotnet test -v q` prints nothing at all.',
    '',
    'Return ONLY the structured report.',
  ].join('\n')
}

function verifyPrompt(s, report) {
  return [
    'You are an adversarial reviewer for ' + s.title + ' of the IDLExIDLE systems refactor. Assume it is wrong until the code and the captures prove otherwise.',
    '',
    GROUND,
    '',
    'WHERE: worktree ' + WT + '/' + s.key + ', branch `refactor/' + s.key + '`, base ' + BASE + '. Work only there.',
    'The implementer reported: ' + (report ? JSON.stringify(report) : '(nothing - inspect the branch yourself)'),
    '',
    'DO, in order:',
    '1. `git -C ' + WT + '/' + s.key + ' diff --stat ' + BASE + '..HEAD`, then read the WHOLE diff. Check it against BRIEF.md ' + s.secs + ' and against PLAN.md\'s settled decisions.',
    '2. THE DORMANCY SWEEP, which is this project\'s named failure mode: for every field, dial, method, catalogue entry and save value the diff adds, find the line that READS it at runtime. Anything with no live consumer is a defect however well tested it is. Say how many you checked.',
    '3. THE MIGRATION SWEEP: for every save field added, stopped or changed, prove an OLD save still loads and keeps what the player already had. The ten-second autosave destroys any field the build stops writing.',
    '4. Rebuild and re-run everything yourself: build, both test suites, check_all.sh, check_boot.sh. Do not trust the report\'s numbers.',
    '5. Recapture the modes yourself (' + s.modes + ') at 100 and 150, and LOOK at every image. Judge the reflow contract.',
    '6. Fix what is small and certain, commit it on the same branch with a fix(...) message, and leave anything larger as a defect with file:line and the capture that shows it.',
    '',
    'Mark each defect `blocking` (cannot ship as written) or `minor`. Return ONLY the structured verdict.',
  ].join('\n')
}

phase('Build')
const results = await pipeline(
  SLICES,
  function (s) {
    return agent(buildPrompt(s), { label: 'build:' + s.key, phase: 'Build', schema: REPORT, agentType: 'general-purpose' })
  },
  function (r, s) {
    return agent(verifyPrompt(s, r), { label: 'verify:' + s.key, phase: 'Verify', schema: VERDICT, agentType: 'general-purpose' })
      .then(function (v) { return { slice: s.key, report: r, verdict: v } })
  }
)

const out = results.filter(Boolean)
log('phases 4/5/6: ' + out.length + '/' + SLICES.length + ' slices built and verified')
return out
