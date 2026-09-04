export const meta = {
  name: 'systems-refactor-finish',
  description: 'Re-author the two collided traits, split the keystone reveal, and make Shield surround both silhouettes',
  phases: [
    { title: 'Finish', detail: 'three isolated slices' },
    { title: 'Verify', detail: 'an adversarial reader rebuilds and re-judges each' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'
const WT = 'C:/Users/sanan/wt'
const BASE = '63a4c7f'

const GROUND = [
  'REPO: ' + REPO + '. Branch `feat/hunter-cutout-rig` at ' + BASE + ', where phases 4, 5 and 6 are all merged and green (Core 1612, Game 191, gates, boot).',
  '',
  'READ FIRST: `production/audit/systems-refactor/BRIEF.md` (the task), `AUDIT.md` (what the code IS, file:line), `PLAN.md` (the settled decisions). Where the brief and the audit disagree, the audit wins.',
  '',
  'THE USER HAS JUST RULED ON THE VOW MODEL, and these rulings are final:',
  '- The build-level vow architecture STAYS. The per-slot model is RETIRED. Do not restore it, and do not leave slot-level terminology anywhere a player or a reader can see.',
  '- Vow evaluation stays CENTRALISED: each equipped vow validates once against the whole build, each valid vow contributes once, and capacity is independent of skill slots.',
  '- A trait that collided with the retired model is to be RE-AUDITED, not migrated. Old behaviour is obsolete design, not migration debt.',
  '',
  'HOUSE RULES: gameplay values are data, never constants in the loop. Anything you add must be READ by something that runs - a dormant dial is this project\'s named failure mode. Player copy is plain English in game terms; the player reads English as a second language. Layout is UiMetrics arithmetic, never a literal, and holds at UI SCALE 100/125/150. Never delete a test to make a suite pass.',
].join('\n')

const REPORT = {
  type: 'object',
  required: ['commit', 'summary', 'decisions', 'limitations', 'evidence'],
  properties: {
    commit: { type: 'string' },
    summary: { type: 'string' },
    decisions: {
      type: 'array',
      items: {
        type: 'object',
        required: ['question', 'chose', 'why'],
        properties: { question: { type: 'string' }, chose: { type: 'string' }, why: { type: 'string' } },
      },
    },
    limitations: { type: 'array', items: { type: 'string' } },
    evidence: { type: 'array', items: { type: 'string' } },
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
    key: 'traits2',
    title: 'RE-AUTHOR THE TWO TRAITS THAT COLLIDED WITH THE RETIRED VOW MODEL',
    files: 'src/IdleXIdle.Core/Builds/{TraitCatalogue,TraitRules,TraitEffects,Vows,BuildComposer,Build,SoloBattle}.cs and tests/unit/IdleXIdle.Core.Tests/Builds/trait_liveness_test.cs',
    body: [
      'Two traits were authored against a vow model that no longer exists, and the merge kept them alive mechanically. The user has ruled on both.',
      '',
      'TRAIT ONE - `t_kept_word`, THE KEPT WORD. KEEP IT, as a build-level rule: it rewards a hunter who has ZERO equipped vows. That is already roughly what the merge left, but the flavour, the name if necessary, the card line and every comment must be rewritten so NO slot-level terminology remains anywhere - not "a skill with no vow", not "an unsworn slot", not "the slot it pays". A reader coming to this in a year must not be able to reconstruct the retired model from its prose. Check the trait\'s name still fits what it does; rename it if it does not, and if you rename it, its id is a save key, so migrate rather than break.',
      '',
      'TRAIT TWO - `t_price_paid`, THE PRICE PAID ("a vow whose demand you have broken still pays half"). DELETE IT AND AUTHOR A NEW ONE. The user\'s reasoning is that paying for a broken vow undermines the core law that restriction buys power, and it gets worse with several build-level vows at once. Do not keep it merely because the merge found somewhere to put it. Remove `BrokenVowShare` and the `brokenShare` parameter on `Vows.CombinedFactor` entirely - a dial with no consumer is the failure mode this project is named for.',
      '  The replacement must be a MEANINGFUL BUILD-LEVEL VOW INTERACTION THAT STILL REWARDS ACTUALLY SATISFYING VOWS. Design it against what the vow layer really is now: `Build.Vows` is the deduplicated list, `Vows.IsActive` is the per-vow validity test against the whole build, `Vows.CombinedFactor` sums what every KEPT vow pays under one ceiling, and `Unlocks.VowCapacity` is a real number granted by milestones. Somewhere in there is a trait that makes keeping vows more interesting - keeping several at once, keeping one under pressure, keeping one you could have broken. Author it with the same rigour as the other twenty-five: one sentence a player can hold, one dial, ONE read site, and a hidden deterministic discovery rule that relates to what the trait does.',
      '',
      'THEN SWEEP: grep the whole repository for any remaining trace of the retired per-slot vow concept - in code, in comments, in player copy, in tests, in the design docs under design/. `SkillChoice.VowId` is the SAVE SHAPE and stays (a vow is stored on a slot because that is where a player swears it), but nothing may still SAY that a vow belongs to, pays for, or is evaluated per slot. Report what you found and what you changed.',
      '',
      'Both traits need liveness tests that pass, and the negative control matters as much as the positive: a trait whose condition is not met must change nothing at all.',
    ].join('\n'),
    modes: 'dust, traitlit, weave',
  },
  {
    key: 'reveal',
    title: 'THE KEYSTONE REVEAL FITS ITS PRESENTATION CONTRACT',
    files: 'src/IdleXIdle.Game/MapScreen.cs, src/IdleXIdle.Game/Game1.cs (the notice toast and the reveal that raises it)',
    body: [
      'A conquest keystone reveal is currently appended to a MAP STRIP that renders one line, and the extra lines spill out of the plate across the map board. The user has ruled on the fix.',
      '',
      'PRESERVE THE ONE-LINE MAP STRIP CONTRACT. The strip shows only a compact headline - `NEW KEYSTONE - <NAME>` is the user\'s own example. One line, at every UI scale, on every region card.',
      '',
      'THE FULL WRAPPED DESCRIPTION GOES IN THE TYPED NOTICE. That presentation already supports multiline body copy: the notice record carries Head, Detail and an optional Third, its height is summed from the rungs it actually draws, and its ordinary (non-awakening) branch WRAPS its body to several lines and grows the plate to hold them. The longest keystone description is 164 characters and it must arrive whole.',
      '',
      'DO NOT shrink text to make it fit, and do not force a 164-character description into a one-line strip. Those are the two failures the ruling names.',
      '',
      'Check every other consumer of that strip and of that notice while you are here: a keystone reveal is not the only thing either draws, and a fix that fits the longest keystone must not break the shortest quest line.',
      '',
      'PROVE IT WITH CAPTURES at 100 and 150, of the map card carrying a reveal AND of the notice showing the longest description in the catalogue. There is a `keystonenotice` capture mode; use it, and add a dial if you need to pose the longest one specifically.',
    ].join('\n'),
    modes: 'map, world, conquered, keystonenotice',
  },
  {
    key: 'shield',
    title: 'SHIELD MUST SURROUND BOTH SILHOUETTES',
    files: 'src/IdleXIdle.Game/{HuntScreen,VfxPlayer,UiKit}.cs, assets/art/VFX/**',
    body: [
      'THE VFX CONTRACT LANDED AND ITS OWN ACCEPTANCE CLAUSE DOES NOT PASS. The verifier found that the standing shield barrier surrounds neither champion: it clears neither the crown nor the soles on any silhouette. The user\'s ruling is that a small effect centred on the torso is a FAILURE, and that Shield must visually SURROUND both THE SEEKER and THE MAGPIE using the actor\'s visual bounds, the semantic anchors and the relative sizing the contract now provides.',
      '',
      'YOU HAVE THE TOOLS THE CONTRACT BUILT. `vfxdebug` is a capture mode that draws every figure\'s visible bounds, every live effect\'s frame and content rectangles, its anchor point and its native-scale ratio - green inside the asset-scale budget, red over it. `RH_SHOT_HUNTER=<id>` poses any fight fixture on any of the ten champions. Use both: measure before you change anything, and measure after.',
      '',
      'THE MEASUREMENT THAT MATTERS. All ten champions draw within four pixels of the same HEIGHT, but their drawn WIDTHS run from 162 px (THE OATHBOUND) to 373 px (THE QUIVER) - a factor of 2.3. So a barrier sized against height alone will surround one champion and float inside another. SEEKER and MAGPIE are the pair the brief names; OATHBOUND against QUIVER is the pair that can actually fail. Make it work for all four.',
      '',
      'IF THE RASTER ASSET CANNOT SATISFY THIS AT NATIVE-QUALITY SCALE, GENERATE THE CORRECT ONE. PixelLab MCP is available to you and the brief forbids asking the user for art (sec.110). The existing effect strips are 4096x512 - eight 512-px square frames - and `VfxPlayer` infers the frame count from that aspect, so a regenerated asset MUST keep it or `Hold` breaks silently. The audit also found that `fx_shield`\'s content is 386x234 sitting in rows 110-361 of its 512 frame, which is 46% down and 46% of the height: the art itself is a squat dome with dead space, which is a large part of why scaling it to surround a figure fails. Judge whether the asset or the placement is the real fault, and fix the real one. LAW 16: do not paper over a badly sized asset with a giant runtime multiplier.',
      '',
      'If you generate art: file it through `tools/asset-pipeline/file_generated.py`, keep the strip geometry, and TEST AT ACTUAL RENDER SIZE in a real gameplay capture (sec.111) - not at full size, and not in a contact sheet alone.',
      '',
      'VALIDATE IN ACTUAL GAMEPLAY SCREENSHOTS: `fightshield` on SEEKER, MAGPIE, OATHBOUND and QUIVER, at 100 and 150, plus `vfxdebug` on at least the narrowest and widest. LOOK at every one with the Read tool. The barrier must read as surrounding the figure - clearing crown and soles - on every one of them.',
    ].join('\n'),
    modes: 'fightshield, fightshieldbroken, vfxdebug, fight',
  },
]

function buildPrompt(s) {
  return [
    'You are finishing ' + s.title + ' in the IDLExIDLE systems refactor.',
    '',
    GROUND,
    '',
    'WHERE: your worktree is ' + WT + '/' + s.key + ' on branch `finish/' + s.key + '`, checked out at ' + BASE + '. Work ONLY there. Two other agents are working in their own worktrees right now - never touch the main checkout or another worktree.',
    '',
    'FILES YOU OWN: ' + s.files,
    '',
    s.body,
    '',
    'THE BAR - run these yourself and iterate until they pass:',
    '- `dotnet build IdleXIdle.sln -c Debug --nologo -v q` - 0 errors, 0 warnings.',
    '- `dotnet test tests/unit/IdleXIdle.Core.Tests --nologo` and `tests/unit/IdleXIdle.Game.Tests --nologo` - all green. Turkish summaries: "Basarili" passed, "Basarisiz" failed.',
    '- `bash tools/check_all.sh` ends "all gates green"; `bash tools/check_boot.sh` ends "boot green".',
    '- CAPTURES for your modes (' + s.modes + ') at 100 and 150, and LOOK at every one with the Read tool. No text over text, no text over a button, no control under a footer, no clipped last row, nothing past the page edge.',
    '- Commit on your branch, Conventional Commits. Do NOT push or merge.',
    '',
    'TOOL CAVEATS: the Bash tool mangles backslash escapes inside heredocs - write scripts with the Write tool and run them by path. `rm -rf` is denied.',
    '',
    'Return ONLY the structured report.',
  ].join('\n')
}

function verifyPrompt(s, report) {
  return [
    'You are an adversarial reviewer for ' + s.title + '. Assume it is wrong until the code and the captures prove otherwise.',
    '',
    GROUND,
    '',
    'WHERE: worktree ' + WT + '/' + s.key + ', branch `finish/' + s.key + '`, base ' + BASE + '.',
    'The implementer reported: ' + (report ? JSON.stringify(report) : '(nothing - inspect the branch)'),
    '',
    'DO:',
    '1. Read the whole diff against ' + BASE + '. Check it against the user rulings quoted above - those are not negotiable and a slice that quietly worked around one is a blocking defect.',
    '2. THE DORMANCY SWEEP: for every field, dial, rule and catalogue entry the diff adds, find the line that READS it at runtime. Say how many you checked. Also check the diff did not leave a dial it REMOVED still referenced, or one it kept now unreferenced.',
    '3. For the traits slice specifically: grep the repository yourself for surviving per-slot vow language, in code, comments, player copy, tests and design docs. Report anything the implementer missed.',
    '4. For the shield slice specifically: recapture `fightshield` on SEEKER, MAGPIE, OATHBOUND and QUIVER yourself and LOOK at them. Does the barrier surround the figure - clearing crown and soles - on all four? A small effect on the torso is a failure, and so is one that surrounds the narrow champion and floats inside the wide one.',
    '5. Rebuild and re-run everything yourself: build, both suites, check_all.sh, check_boot.sh. Do not trust the report\'s numbers.',
    '6. Fix what is small and certain on the same branch; leave anything larger as a defect with file:line and the capture that shows it.',
    '',
    'Mark each defect `blocking` or `minor`. Return ONLY the structured verdict.',
  ].join('\n')
}

phase('Finish')
const out = await pipeline(
  SLICES,
  function (s) { return agent(buildPrompt(s), { label: 'do:' + s.key, phase: 'Finish', schema: REPORT, agentType: 'general-purpose' }) },
  function (r, s) {
    return agent(verifyPrompt(s, r), { label: 'check:' + s.key, phase: 'Verify', schema: VERDICT, agentType: 'general-purpose' })
      .then(function (v) { return { slice: s.key, report: r, verdict: v } })
  }
)

log('finish: ' + out.filter(Boolean).length + '/' + SLICES.length)
return out.filter(Boolean)
