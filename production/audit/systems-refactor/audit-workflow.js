export const meta = {
  name: 'systems-refactor-audit',
  description: 'Phase 1 audit: map every system the signature/mastery/trait/vow/keystone/VFX refactor touches',
  phases: [
    { title: 'Audit', detail: 'one reader per subsystem, structured map back' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'

const AREAS = [
  {
    key: 'characters',
    title: 'CHARACTERS, ROSTER, INNATES, STARTING SKILLS',
    files: 'src/IdleXIdle.Core/Characters/*.cs (Character, CharacterRoster, CharacterState, LegacyUnlocks), src/IdleXIdle.Game/RosterScreen.cs',
    ask: [
      'The FULL roster: every character id, display name, class, innate (its exact rule and numbers), and its current "starting skill" if any. A table, not prose.',
      'How a character is unlocked, and what unlocking does to the shared skill library - does a starting skill get added account-wide? Quote the code that does it.',
      'How the ACTIVE character is chosen and stored, and what happens to the loadout when it changes.',
      'Whether loadouts are per-character or global. Quote the storage.',
      'What RosterScreen prints for the starting skill (exact strings and the file:line).',
    ],
  },
  {
    key: 'skills',
    title: 'SKILL ARCHITECTURE, LOADOUT, PROGRESSION',
    files: 'src/IdleXIdle.Core/Builds/{SkillCatalogue,SkillShape,SkillProgress,PlayerLoadout,Build,BuildComposer,StyleAffinity}.cs',
    ask: [
      'The exact authoring shape of a skill: every field of SkillDef / the resolved skill, what Style / Kind / variation / reinforcement mean, and how a skill is authored end to end. Give one complete authored example verbatim.',
      'All twelve shared skills: id, name, style, kind, one-line effect.',
      'How skill progression works: what counts as a use, how levels are earned, where the level lives, and how it is saved.',
      'PlayerLoadout: slot count and kinds (how many active / passive), the duplicate-skill rule (LAW 13 from the previous pass), and every method that mutates a slot.',
      'Whether anything today expresses OWNERSHIP of a skill by a character. If not, say so plainly.',
    ],
  },
  {
    key: 'mastery',
    title: 'MASTERY TREE, SKILL UNLOCK SEMANTICS, RESPEC',
    files: 'src/IdleXIdle.Core/Builds/{MasteryTree,MasteryCatalog,MasteryPoints,MasteryLayout}.cs, src/IdleXIdle.Core/Progression/MasteryLevel.cs, src/IdleXIdle.Game/MasteryScreen.cs',
    ask: [
      'How a mastery node unlocks a shared skill today. Quote the code path from node allocation to "this skill is available".',
      'THE KEY QUESTION: is skill access permanent once discovered? Find the field/method that makes it permanent (the brief calls it "permanently learned through Mastery" / RestoreLearned / LearnedSkills) and quote every site that reads or writes it.',
      'What a mastery respec does today: what it clears, what it keeps, whether it repairs the loadout, and whether it warns first.',
      'The specialisation / style ceremony: what it is, what it gates.',
      'Every place the phrase "learned" or "discovered" appears in mastery code or copy, with file:line.',
    ],
  },
  {
    key: 'traits',
    title: 'TRAIT TREE, TRAIT POINTS, STRUCTURAL NODES',
    files: 'src/IdleXIdle.Core/Prestige/{TraitRoads,TraitTreeLayout,MemoryDust,DustEffects,MemoryDustText}.cs, src/IdleXIdle.Game/TraitsScreen.cs',
    ask: [
      'The COMPLETE current trait catalogue: every node id, display name, road, cost, and exactly what it does. A table. Mark each node as STRUCTURAL (it unlocks a system: forge, auto-sell, a skill slot, vows, a keystone socket) or COMBAT (it changes a number or a rule in a fight) or OTHER.',
      'Where trait POINTS come from: the earning rule, the cap, and how many a full career yields.',
      'How a purchase is stored and restored (the save fields).',
      'Every consumer of a structural trait node: for each one, the file:line where the game asks "does the player own this node" and what it gates.',
      'What DustEffects does and how the tree feeds combat.',
    ],
  },
  {
    key: 'vows-keystones',
    title: 'VOWS, KEYSTONES, UNLOCKS, CAREER MILESTONES',
    files: 'src/IdleXIdle.Core/Builds/{Vows,Keystones}.cs, src/IdleXIdle.Core/Progression/{Unlocks,Career,Onboarding,Tutorial}.cs, src/IdleXIdle.Core/Encounters/*.cs',
    ask: [
      'The full Vow catalogue: id, name, restriction, reward, and how a vow is unlocked and equipped today. How many can be equipped, and what raises that number.',
      'The full Keystone catalogue: id, name, effect, and how a keystone is unlocked and socketed today. How many sockets, and what raises that number.',
      'The Unlocks/Activity gate system: every Activity, what opens it, and the exact requirement text shown to the player.',
      'World/region progression: what a region conquest is, what it awards today, what region MASTERY is, and every existing milestone the game already recognises (hunter level, waves, conquests).',
      'Which of these currently depend on a TRAIT NODE. Name each dependency with file:line.',
    ],
  },
  {
    key: 'persistence',
    title: 'SAVE FORMAT AND MIGRATION',
    files: 'src/IdleXIdle.Core/Persistence/*.cs (SaveGame, SaveStore, LegacySkillForm, ShareCodes, BuildStamp), src/IdleXIdle.Core/Characters/LegacyUnlocks.cs',
    ask: [
      'EVERY field of SaveGame, with its type, default and meaning. A complete table - this is the migration surface.',
      'How migration is done today: is there a version number, or is it additive-with-defaults? Quote an example of a past migration.',
      'Which fields belong to: mastery access, trait tree state, trait points, vows, keystones, skill progression, character state, structural unlocks.',
      'What LegacySkillForm and LegacyUnlocks exist for, and whether they are still live.',
      'How save round-tripping is tested today, and where those tests live.',
    ],
  },
  {
    key: 'vfx',
    title: 'VFX PLAYER, ANCHORS, ACTOR BOUNDS, ANIMATION RIG',
    files: 'src/IdleXIdle.Game/VfxPlayer.cs, src/IdleXIdle.Game/HuntScreen.cs (the arena, actor rects, _vfx.Play call sites), src/IdleXIdle.Core/Animation/*.cs, src/IdleXIdle.Core/Presentation/*.cs, src/IdleXIdle.Game/AssetLibrary.cs (the fx_ aliases)',
    ask: [
      'How VfxPlayer works end to end: what Play takes, how a strip is animated, how a destination rectangle is computed, and what layering exists.',
      'EVERY _vfx.Play call site in the game: file:line, the effect key, and exactly how its position and size are computed. This is the list the new contract has to replace - be exhaustive.',
      'How the champion and the enemies are positioned and sized on the HUNT screen: the rects, where they come from, and whether any VISUAL BOUNDS (as opposed to raw texture size) exist anywhere.',
      'The animation rig: how a character strip is drawn, whether frames have transparent padding, and whether anything measures the opaque bounds of a sprite.',
      'Which effects are PERSISTENT (shield barrier, fields) and how they survive a frame where their event is not replayed.',
    ],
  },
  {
    key: 'ui-assets',
    title: 'UI FRAMES, NINE-SLICE, RASTER QUALITY, GEAR INVENTORY',
    files: 'src/IdleXIdle.Game/UiKit.cs (Panel, PanelQuiet, PanelNine, NineSlice, HSliceScaled, PanelArtKey), src/IdleXIdle.Game/GearScreen.cs, assets/art/UI/**',
    ask: [
      'Every frame-drawing helper in UiKit: what it does, whether it nine-slices, and what asset it uses. Quote the nine-slice implementation.',
      'The frame assets on disk: path, native pixel size, and which helper draws them. Then, for the GEAR screen EQUIPPED panel specifically, compute the destination rectangle at UI scale 100/125/150 and give the effective scale ratio (destination / native). The brief says decorative raster must stay near native (0.75x-1.25x) - report the real numbers.',
      'The same ratio for every other large decorative frame you can find (item detail, forge, vault, traits inspector, modal).',
      'The GEAR inventory panel as it stands: how the header, the filter tabs, the grid and the footer are drawn, and exactly which parts use ad-hoc rectangles rather than the house components.',
      'What UI art already exists that a polished SECONDARY panel could reuse (thin frames, headers, tab art), with paths.',
    ],
  },
]

const MAP = {
  type: 'object',
  required: ['area', 'findings', 'files', 'risks'],
  properties: {
    area: { type: 'string' },
    findings: {
      type: 'array',
      description: 'One entry per question asked, in order. Answer with facts and file:line, never with guesses.',
      items: {
        type: 'object',
        required: ['question', 'answer'],
        properties: {
          question: { type: 'string' },
          answer: { type: 'string', description: 'The full answer. Tables as markdown. Quote code where the question asks for it.' },
        },
      },
    },
    files: { type: 'array', items: { type: 'string' }, description: 'Every file that will have to change for this area, with a word on why' },
    risks: { type: 'array', items: { type: 'string' }, description: 'What will break, what is load-bearing, what the refactor must not lose' },
  },
}

function prompt(a) {
  return [
    'You are auditing ONE subsystem of IDLExIDLE (MonoGame 3.8.4.1, C#, .NET 8) before a large refactor. You are READ-ONLY: do not edit, create or delete a single file. Your entire job is to come back with the truth about what is there now.',
    '',
    'REPO: ' + REPO + '. The authoritative branch is `feat/hunter-cutout-rig` (it is ahead of main; main is stale). Read the working tree.',
    '',
    'THE REFACTOR that follows this audit is specified in production/audit/systems-refactor/BRIEF.md. Read its section 0 (the inspection list) and sections 1 and 118 (the ownership map and the final mental model) so you know what the refactor is FOR, then read whichever numbered sections concern your area. Do not design anything; do not propose an implementation. Report what exists.',
    '',
    'YOUR AREA: ' + a.title,
    'START WITH: ' + a.files + ' - but follow the code wherever it goes. Grep the whole repo for every consumer; a system is defined by who reads it, not by the file it lives in.',
    '',
    'ANSWER EXACTLY THESE, IN THIS ORDER:',
    ...a.ask.map(function (q, i) { return '  ' + (i + 1) + '. ' + q }),
    '',
    'RULES:',
    '- Every claim carries a file:line. A claim you cannot point at is a guess, and a guess in an audit becomes a bug in the refactor.',
    '- Do not trust comments, doc-comments or the GDDs in design/ - the brief says recent runtime behaviour takes precedence over stale documentation. Where a comment and the code disagree, say so and believe the code.',
    '- Where a question asks for a table, give a complete table, not a sample. If a catalogue has 51 entries, list 51.',
    '- Where a question asks you to compute something (a scale ratio, a count), compute it and show the arithmetic.',
    '- If the answer to a question is "this does not exist", say that plainly and say what exists instead.',
    '- You may run read-only commands (grep, sed -n, git log, dotnet build) but never anything that writes to the repo.',
    '',
    'Your final message is data, not prose: return ONLY the structured map.',
  ].join('\n')
}

phase('Audit')
const maps = await parallel(AREAS.map(function (a) {
  return function () {
    return agent(prompt(a), { label: 'audit:' + a.key, phase: 'Audit', schema: MAP, agentType: 'general-purpose' })
  }
}))

const out = maps.filter(Boolean)
log('audit: ' + out.length + '/' + AREAS.length + ' areas mapped')
return out
