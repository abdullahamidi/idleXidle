export const meta = {
  name: 'systems-refactor-synth',
  description: 'Synthesise the final ten signature skills from two judged panels, then verify every dial against the fight loop',
  phases: [
    { title: 'Synthesise', detail: 'one designer builds the final ten from the keeps, replaces the rejects' },
    { title: 'Verify', detail: 'an adversarial reader checks every named dial and read site against SoloBattle' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'

const GROUND = [
  'REPO: ' + REPO + ', branch `feat/hunter-cutout-rig`.',
  '',
  'READ, in this order:',
  '- `production/audit/systems-refactor/design-panels.json` - the two signature panels and all three judgements, in full. This is your input.',
  '- `production/audit/systems-refactor/BRIEF.md` sections 2-8 and 115-118 - the task and its laws.',
  '- `production/audit/systems-refactor/PLAN.md` - the eleven settled decisions.',
  '- `src/IdleXIdle.Core/Builds/SkillCatalogue.cs` - the vocabulary: SkillDef, SkillRules, SkillVariation, Reinforcement, and the twelve shared skills you must not duplicate.',
  '- `src/IdleXIdle.Core/Builds/SoloBattle.cs` - the fight loop. Every dial must be read here.',
  '- `src/IdleXIdle.Core/Characters/CharacterRoster.cs` - the ten characters and their exact innate numbers.',
].join('\n')

const SIGS = {
  type: 'object',
  required: ['signatures', 'newDials', 'rejected', 'notes'],
  properties: {
    signatures: {
      type: 'array', minItems: 10, maxItems: 10,
      items: {
        type: 'object',
        required: ['characterId', 'characterName', 'skillId', 'name', 'style', 'kind', 'provenance',
                   'fantasy', 'baseMechanic', 'dials', 'whyItFits', 'innateInteraction', 'variations', 'presentation'],
        properties: {
          characterId: { type: 'string' },
          characterName: { type: 'string' },
          skillId: { type: 'string' },
          name: { type: 'string' },
          style: { type: 'string', enum: ['Hammer', 'Snare', 'Sign', 'Volley', 'Field', 'Drain'] },
          kind: { type: 'string', enum: ['Active', 'Field', 'Reaction'] },
          provenance: { type: 'string', description: 'KEPT FROM PANEL A / KEPT FROM PANEL B / EDITED FROM <panel> / NEW - and one line on why' },
          fantasy: { type: 'string' },
          baseMechanic: { type: 'string' },
          dials: { type: 'string', description: 'exact SkillDef fields with values, and the SoloBattle.cs line that reads each' },
          whyItFits: { type: 'string' },
          innateInteraction: { type: 'string' },
          variations: {
            type: 'array', minItems: 2, maxItems: 2,
            items: {
              type: 'object',
              required: ['name', 'line', 'source', 'dials', 'reinforcements'],
              properties: {
                name: { type: 'string' }, line: { type: 'string' },
                source: { type: 'string', enum: ['Body', 'Machine', 'Mind', 'Nature', 'Shadow', 'Spirit'] },
                dials: { type: 'string' },
                reinforcements: {
                  type: 'array', minItems: 3, maxItems: 3,
                  items: {
                    type: 'object',
                    required: ['name', 'line', 'dials', 'liveness', 'deadOnOther'],
                    properties: {
                      name: { type: 'string' }, line: { type: 'string' }, dials: { type: 'string' },
                      liveness: { type: 'string', description: 'which number moves in a real fight' },
                      deadOnOther: { type: 'string', description: 'why this reinforcement is worthless to the OTHER variation' },
                    },
                  },
                },
              },
            },
          },
          presentation: { type: 'string' },
        },
      },
    },
    newDials: {
      type: 'array',
      items: {
        type: 'object',
        required: ['name', 'type', 'readSite', 'why'],
        properties: {
          name: { type: 'string' }, type: { type: 'string' },
          readSite: { type: 'string', description: 'the ONE SoloBattle.cs line that reads it, and what the added line is' },
          why: { type: 'string' },
        },
      },
    },
    rejected: { type: 'array', items: { type: 'string' }, description: 'every panel design you did NOT keep, and the judgement you acted on' },
    notes: { type: 'array', items: { type: 'string' } },
  },
}

const VERDICT = {
  type: 'object',
  required: ['pass', 'defects', 'confirmations'],
  properties: {
    pass: { type: 'boolean', description: 'true only if every dial is read where the design says, every reinforcement can move a number, and no signature duplicates a shipped skill' },
    defects: {
      type: 'array',
      items: {
        type: 'object',
        required: ['signature', 'defect', 'evidence', 'fix'],
        properties: {
          signature: { type: 'string' }, defect: { type: 'string' },
          evidence: { type: 'string', description: 'the file:line that proves it' },
          fix: { type: 'string' },
        },
      },
    },
    confirmations: { type: 'array', items: { type: 'string' }, description: 'the claims you checked and found true' },
  },
}

const SYNTH = [
  'You are the lead designer. Two panels designed the ten character-exclusive SIGNATURE SKILLS for IDLExIDLE and three judges scored every design of both. Your job is to produce THE FINAL TEN.',
  '',
  GROUND,
  '',
  'HOW TO USE THE JUDGEMENTS:',
  '- Where all three judges say KEEP VERBATIM, keep it verbatim. Do not improve it.',
  '- Where the judges say REPLACE, replace it. They verified read sites against the fight loop and caught designs naming dials that are not read on the path the skill fires from; those are facts, not opinions.',
  '- Where two judges disagree, decide, and say in `provenance` which judgement you acted on and why.',
  '- Three characters need designs neither panel got right (the judges name them). Author those fresh.',
  '',
  'THE SET-LEVEL DEFECTS THE JUDGES FOUND, which you must fix across the whole ten:',
  '- One panel put four Reactions in ten signatures, so "something happens when you are bitten or when something dies" became the house identity instead of a character\'s. Balance the kinds.',
  '- The same bleed trio (BleedFromHits + a BleedRate reinforcement + BleedCarriesWaves) was used on three different signatures. A dial group may headline ONE signature.',
  '- Several designs reproduced a shipped skill\'s variations and reinforcement lines almost word for word. Every one of your twenty variations and sixty reinforcements must be checked against the twelve shipped skills; a renamed FLATTEN or TRAIL or BRINK is a failed design.',
  '',
  'THE ARCHAEOLOGICAL FINDS the judges told you to adopt even where the skill carrying them is replaced:',
  '- `isBoss` is a live ResolveWave parameter that nothing reads. A dial that reads it is free.',
  '- The basic attack is invisible to nearly every damage node, because Amp() returns early when skillDef is null. A dial that reaches the swing is genuinely new ground.',
  '- Three ReactionOn values (WaveStart, Kill, LowHealth) are declared and never dispatched; only Bitten is. Reviving one is a real capability, not a new engine.',
  'Use them where they belong. Do not force all of them in.',
  '',
  'HARD RULES:',
  '- Ten signatures, one per character, in roster order: seeker, anvil, chorus, metronome, unbroken, tower, quiver, thornwall, oathbound, magpie.',
  '- Each has two variations of three reinforcements. Each reinforcement must move damage-dealt or health-kept in a real fight, and must be worthless to the other variation - say why in `deadOnOther`.',
  '- Every dial you name must be read by a real line of SoloBattle.cs. Cite it. If a design needs a NEW dial, it goes in `newDials` with the ONE line that would read it. Keep the total at six or fewer.',
  '- No signature may restate its own innate (brief sec.5). The interaction is the design.',
  '- Copy is plain English in game terms. The player reads English as a second language.',
  '',
  'Return ONLY the structured design.',
].join('\n')

function verifyPrompt(design) {
  return [
    'You are an adversarial reviewer. A designer has produced the final ten SIGNATURE SKILLS for IDLExIDLE. Assume each is wrong until the code proves it right.',
    '',
    GROUND,
    '',
    'THE DESIGN: ' + JSON.stringify(design),
    '',
    'CHECK EVERY ONE OF THESE, against the actual source:',
    '1. For every dial named in every `dials` field: open SoloBattle.cs at the cited line and confirm that line reads that dial, on the path this skill actually fires from. A Field skill that names a dial only the cast block reads is a defect. A Reaction that names a dial only LandSpread reads with a non-null def is a defect.',
    '2. For every reinforcement: could it move damage-dealt or health-kept in a real fight? The liveness suites (tests/unit/IdleXIdle.Core.Tests/Weaving/reinforcement_liveness_test.cs) compose picks with a null character and a road-only tree - check whether the fixture can even reach the condition the reinforcement needs. A reinforcement that only pays under a Vow, a keystone or a character innate will FAIL the suite.',
    '3. For every variation pair: is each reinforcement genuinely worthless to the other variation?',
    '4. Against SkillCatalogue.cs: does any signature, variation or reinforcement reproduce a shipped one? Compare the lines, not just the names.',
    '5. For every entry in `newDials`: is the read site real, is it ONE site, and does the surrounding code let that line work?',
    '6. Does any signature merely restate its character\'s innate (CharacterRoster.cs)?',
    '',
    'Report every defect with the file:line that proves it and a concrete fix. Confirm the claims you checked and found true, so the record shows what was verified rather than assumed.',
    '',
    'Return ONLY the structured verdict.',
  ].join('\n')
}

phase('Synthesise')
const design = await agent(SYNTH, { label: 'synth', phase: 'Synthesise', schema: SIGS, agentType: 'general-purpose' })

phase('Verify')
const verdict = await agent(verifyPrompt(design), { label: 'verify', phase: 'Verify', schema: VERDICT, agentType: 'general-purpose' })

return { design: design, verdict: verdict }
