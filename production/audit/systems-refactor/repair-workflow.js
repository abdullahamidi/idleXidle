export const meta = {
  name: 'systems-refactor-repair',
  description: 'Repair the ten signature skills against the verifier 24 defects, then re-verify',
  phases: [
    { title: 'Repair', detail: 'fix every defect the verifier proved, keep everything it confirmed' },
    { title: 'Reverify', detail: 'a fresh adversarial reader checks the repaired design' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'

const GROUND = [
  'REPO: ' + REPO + ', branch `feat/hunter-cutout-rig`.',
  '',
  'READ:',
  '- `build/synth_raw.json` - the design and the verifier verdict, in full. `design` is what you are repairing; `verdict.defects` is the list, each with the evidence that proves it; `verdict.confirmations` is what was checked and found TRUE and must not be disturbed.',
  '- `src/IdleXIdle.Core/Builds/SoloBattle.cs` - the fight loop. Open it at every line either document cites; do not take a citation on trust from either side.',
  '- `src/IdleXIdle.Core/Builds/SkillCatalogue.cs` - the vocabulary and the twelve shipped skills.',
  '- `src/IdleXIdle.Core/Characters/CharacterRoster.cs` - the ten innates and their exact numbers.',
  '- `production/audit/systems-refactor/BRIEF.md` sections 2-8 - the laws the design answers to.',
].join('\n')

const SIGS = {
  type: 'object',
  required: ['signatures', 'newDials', 'repairs', 'notes'],
  properties: {
    signatures: {
      type: 'array', minItems: 10, maxItems: 10,
      items: {
        type: 'object',
        required: ['characterId', 'characterName', 'skillId', 'name', 'style', 'kind',
                   'fantasy', 'baseMechanic', 'dials', 'whyItFits', 'innateInteraction', 'variations', 'presentation'],
        properties: {
          characterId: { type: 'string' }, characterName: { type: 'string' },
          skillId: { type: 'string' }, name: { type: 'string' },
          style: { type: 'string', enum: ['Hammer', 'Snare', 'Sign', 'Volley', 'Field', 'Drain'] },
          kind: { type: 'string', enum: ['Active', 'Field', 'Reaction'] },
          fantasy: { type: 'string' }, baseMechanic: { type: 'string' },
          dials: { type: 'string', description: 'exact fields with values, and the VERIFIED SoloBattle.cs line that reads each' },
          whyItFits: { type: 'string' }, innateInteraction: { type: 'string' },
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
                    required: ['name', 'line', 'dials', 'liveness', 'weakOnOther'],
                    properties: {
                      name: { type: 'string' }, line: { type: 'string' }, dials: { type: 'string' },
                      liveness: { type: 'string' },
                      weakOnOther: { type: 'string', description: 'HONESTLY: why this reinforcement is worth much less on the other variation. Say STRUCTURALLY DEAD only when the code cannot execute the read; otherwise say how much smaller the payout is and why.' },
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
          readSite: { type: 'string', description: 'the exact line, the enclosing block, and the identifiers in scope there - verified by opening the file' },
          why: { type: 'string' },
        },
      },
    },
    repairs: {
      type: 'array',
      items: {
        type: 'object',
        required: ['defect', 'action'],
        properties: {
          defect: { type: 'string', description: 'the verifier defect, in a few words' },
          action: { type: 'string', description: 'FIXED (how) / REJECTED (why the verifier is wrong, with the evidence)' },
        },
      },
    },
    notes: { type: 'array', items: { type: 'string' } },
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
        required: ['signature', 'severity', 'defect', 'evidence', 'fix'],
        properties: {
          signature: { type: 'string' },
          severity: { type: 'string', enum: ['blocking', 'minor'], description: 'blocking = the design cannot be implemented as written. minor = the design works but a claim in its prose is wrong.' },
          defect: { type: 'string' }, evidence: { type: 'string' }, fix: { type: 'string' },
        },
      },
    },
    confirmations: { type: 'array', items: { type: 'string' } },
  },
}

const REPAIR = [
  'You are repairing the final ten SIGNATURE SKILLS for IDLExIDLE. An adversarial verifier read the design against the fight loop and returned 24 defects with evidence. Fix them.',
  '',
  GROUND,
  '',
  'THE RULES OF THIS REPAIR:',
  '- Work through `verdict.defects` one at a time. For each, OPEN THE CITED LINE yourself. If the verifier is right, fix the design. If the verifier is wrong, reject it and say what the code actually shows. Record every one in `repairs` - a defect you neither fixed nor rejected is a defect you hid.',
  '- Do not disturb anything in `verdict.confirmations`. Those were checked and found true.',
  '- Keep every design decision the verifier did not fault. This is a repair, not a redesign.',
  '',
  'THE THREE FAULT CLASSES IT FOUND, which you must sweep for across ALL TEN even where it only cited one:',
  '',
  '1. WRONG READ SITE. Two new dials name a line inside the wrong enclosing block, or before the identifier they use is declared. For EVERY dial in the design - the six new ones and every existing one - open the cited line and confirm: does that line read that field, is it inside a block that runs on the path this skill fires from, and are the identifiers the added code needs in scope there? Rewrite each `readSite` to name the enclosing block and the identifiers in scope.',
  '',
  '2. THE OVERWRITE FALLACY. The design repeatedly claims a reinforcement is "STRUCTURALLY DEAD" on the other variation. A reinforcement is a `Func<SkillDef,SkillDef>` applied AFTER the variation, so it can set an absolute value onto the resolved definition and switch on a rule the other variation never turned on. That makes most of these claims false. The schema field is renamed `weakOnOther` for that reason: say STRUCTURALLY DEAD only where the code genuinely cannot execute the read (for example a loop that runs zero times because a count is 1), and otherwise state honestly how much smaller the payout is and why a player would not buy it there. An honest "worth about a quarter as much, because the branch gave that clause away" is a good answer; a false "dead" is not.',
  '',
  '3. DUPLICATED SHIPPED CONTENT. At least two reinforcements reproduce shipped ones (one repeats DRAIN/SIPHON/TRICKLE\'s dial, clause and channel at a bigger number; another reintroduces the SHEAR reinforcement that the shipped catalogue deleted from BLOW by name and for cause). Read all twelve shipped skills and check every one of your sixty reinforcements against them. A renamed shipped purchase is a failed design.',
  '',
  'AND FIX THE CARD-VERSUS-CODE MISMATCHES: at least one card says "every enemy IT kills" where the specified read fires on every death from any source. The card must say what the code does, in plain English, in game terms.',
  '',
  'Return ONLY the structured design.',
].join('\n')

function reverifyPrompt(design) {
  return [
    'You are a fresh adversarial reviewer. A design for the ten SIGNATURE SKILLS of IDLExIDLE has been repaired against a previous verifier 24 defects. Check it again from scratch. Do not assume the repair was done correctly, and do not assume the previous verifier was right about everything.',
    '',
    GROUND,
    '',
    'THE REPAIRED DESIGN: ' + JSON.stringify(design),
    '',
    'CHECK, opening the source at every line either the design or you cite:',
    '1. Every dial, existing and new: does the cited line read it, on the path this skill fires from, with the needed identifiers in scope?',
    '2. Every reinforcement: can it move damage-dealt or health-kept in the liveness fixture, which composes with a null character and a road-only tree? A reinforcement that only pays under a vow, a keystone or an innate will FAIL the suite.',
    '3. Every `weakOnOther`: is the claim true? A "STRUCTURALLY DEAD" that is merely "smaller" is a defect. A "smaller" that is actually equal is a defect.',
    '4. Against the twelve shipped skills: does anything reproduce a shipped variation or reinforcement?',
    '5. Does any card sentence say something different from what its dials do?',
    '6. Does any signature restate its own innate rather than interacting with it?',
    '',
    'Mark each defect `blocking` (cannot be implemented as written) or `minor` (implementable, but a prose claim is wrong). Be specific about which.',
    '',
    'Return ONLY the structured verdict.',
  ].join('\n')
}

phase('Repair')
const repaired = await agent(REPAIR, { label: 'repair', phase: 'Repair', schema: SIGS, agentType: 'general-purpose' })

phase('Reverify')
const verdict = await agent(reverifyPrompt(repaired), { label: 'reverify', phase: 'Reverify', schema: VERDICT, agentType: 'general-purpose' })

return { design: repaired, verdict: verdict }
