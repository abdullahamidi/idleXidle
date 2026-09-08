export const meta = {
  name: 'systems-refactor-design',
  description: 'Phase 1 design: ten signature skills (judged panel), trait catalogue, vow/keystone/structural maps, VFX contract',
  phases: [
    { title: 'Signatures', detail: 'three independent design panels for the ten signature skills' },
    { title: 'Judge', detail: 'three lenses score each panel; the best is synthesised' },
    { title: 'Catalogues', detail: 'traits, vows, keystones, structural unlocks, VFX contract' },
  ],
}

const REPO = 'C:/Users/sanan/OneDrive/Masa' + '\u00fc' + 'st' + '\u00fc' + '/idleXidle'

const GROUND = [
  'REPO: ' + REPO + ', branch `feat/hunter-cutout-rig` (main is stale; read the working tree).',
  '',
  'READ FIRST, all of it, before you write a word of design:',
  '- `production/audit/systems-refactor/BRIEF.md` - the task. 118 sections.',
  '- `production/audit/systems-refactor/AUDIT.md` - what the code actually is, mapped by eight readers with file:line evidence. Where a brief premise and this disagree, the audit wins; the brief was written from outside the codebase.',
  '- `production/audit/systems-refactor/PLAN.md` - the eleven decisions already made. They are settled. Design inside them; if one is genuinely impossible, say why rather than quietly designing around it.',
  '',
  'AND READ THE REAL CODE. A design that cannot be expressed in the dials that exist is not a design, it is a wish:',
  '- `src/IdleXIdle.Core/Builds/SkillCatalogue.cs` - `SkillDef` (about 60 dials), `SkillRules` (19 more), `SkillVariation`, `Reinforcement`, and the twelve authored shared skills. This is the vocabulary.',
  '- `src/IdleXIdle.Core/Builds/SoloBattle.cs` - the fight loop. EVERY dial you name must already be read here, or you must say exactly which new dial and which single read site the design adds.',
  '- `src/IdleXIdle.Core/Builds/SkillShape.cs` - what a character innate can say.',
  '- `src/IdleXIdle.Core/Characters/CharacterRoster.cs` - the ten characters, their innates and the exact numbers.',
  '- `src/IdleXIdle.Core/Expeditions/WaveModel.cs` - `WaveMetrics` and `BattleEventKind`: the telemetry a discovery rule may read.',
  '',
  'HOUSE RULES that outrank your taste:',
  '- Gameplay values are DATA, never constants in the loop.',
  '- Copy is plain English in game terms: stun, slow, defence break, cooldown. No genre jargon, no unexplained abbreviations. The player reads English as a second language.',
  '- A dial with no consumer is the project\'s signature bug. Every effect you name must be readable by a real site in the fight.',
  '- The liveness suites run EVERY variation and EVERY reinforcement through a real fight and demand that damage-dealt or health-kept actually moves. A design whose reinforcement cannot move either number will fail the build.',
].join('\n')

const SIGS = {
  type: 'object',
  required: ['signatures', 'newDials', 'notes'],
  properties: {
    signatures: {
      type: 'array',
      minItems: 10,
      maxItems: 10,
      items: {
        type: 'object',
        required: ['characterId', 'characterName', 'skillId', 'name', 'style', 'kind', 'fantasy',
                   'baseMechanic', 'dials', 'whyItFits', 'innateInteraction', 'variations', 'presentation'],
        properties: {
          characterId: { type: 'string' },
          characterName: { type: 'string' },
          skillId: { type: 'string', description: 'stable snake_case id, prefixed sig_' },
          name: { type: 'string', description: 'the display name, one or two words, upper case' },
          style: { type: 'string', enum: ['Hammer', 'Snare', 'Sign', 'Volley', 'Field', 'Drain'] },
          kind: { type: 'string', enum: ['Active', 'Field', 'Reaction'] },
          fantasy: { type: 'string', description: 'one sentence: what it feels like to fight with this' },
          baseMechanic: { type: 'string', description: 'the base line as the player reads it, one sentence in game terms' },
          dials: { type: 'string', description: 'the exact SkillDef fields the base line sets, with values, and the SoloBattle line that reads each' },
          whyItFits: { type: 'string' },
          innateInteraction: { type: 'string', description: 'how it INTERACTS with the innate. Restating the innate is a failed design (brief sec.5)' },
          variations: {
            type: 'array', minItems: 2, maxItems: 2,
            items: {
              type: 'object',
              required: ['name', 'line', 'source', 'dials', 'reinforcements'],
              properties: {
                name: { type: 'string' },
                line: { type: 'string' },
                source: { type: 'string', enum: ['Body', 'Machine', 'Mind', 'Nature', 'Shadow', 'Spirit'] },
                dials: { type: 'string' },
                reinforcements: {
                  type: 'array', minItems: 3, maxItems: 3,
                  items: {
                    type: 'object',
                    required: ['name', 'line', 'dials', 'liveness'],
                    properties: {
                      name: { type: 'string' },
                      line: { type: 'string' },
                      dials: { type: 'string' },
                      liveness: { type: 'string', description: 'which number moves in a real fight, so the liveness suite can see it' },
                    },
                  },
                },
              },
            },
          },
          presentation: { type: 'string', description: 'ClipKey, FxKey, and what the effect should look like' },
        },
      },
    },
    newDials: { type: 'array', items: { type: 'string' }, description: 'every NEW SkillDef/SkillRules member the ten designs need, with the single SoloBattle site that would read it. Fewer is better.' },
    notes: { type: 'array', items: { type: 'string' } },
  },
}

const SCORE = {
  type: 'object',
  required: ['verdict', 'perSignature', 'best', 'worst'],
  properties: {
    verdict: { type: 'string', description: 'one paragraph: is this panel shippable, and what is its character' },
    perSignature: {
      type: 'array',
      items: {
        type: 'object',
        required: ['characterId', 'score', 'why'],
        properties: {
          characterId: { type: 'string' },
          score: { type: 'integer', minimum: 1, maximum: 5 },
          why: { type: 'string' },
        },
      },
    },
    best: { type: 'array', items: { type: 'string' }, description: 'the designs worth keeping verbatim, and why' },
    worst: { type: 'array', items: { type: 'string' }, description: 'the designs that must be replaced, and what is wrong' },
  },
}

const ANGLES = [
  {
    key: 'a',
    lens: 'START FROM THE INNATE. For each character, ask what its innate makes POSSIBLE that is not yet worth doing, and author the skill that makes it worth doing. The interaction is the design (brief sec.5): if the innate carries overkill, the signature should manufacture overkill worth carrying, not add more carry.',
  },
  {
    key: 'b',
    lens: 'START FROM THE STYLE RING. The six styles are three oppositions (HAMMER/VOLLEY, SNARE/FIELD, SIGN/DRAIN). Give the ten signatures a distribution across styles and kinds that the twelve shared skills do NOT already cover, so a signature genuinely changes what techniques are available when you switch character (brief sec.118). Check what each style already has before you add to it.',
  },
  {
    key: 'c',
    lens: 'START FROM THE FIGHT. Read SoloBattle first and find the moments the twelve skills leave unclaimed - the wave opening, the last living enemy, the beat after a kill, the bite that is about to land, the second wave of a long descent. Author each signature as a claim on one of those moments, and let the character it belongs to be the one whose innate cares most about that moment.',
  },
]

function sigPrompt(a) {
  return [
    'You are a systems designer authoring the TEN CHARACTER-EXCLUSIVE SIGNATURE SKILLS for IDLExIDLE.',
    '',
    GROUND,
    '',
    'YOUR ANGLE: ' + a.lens,
    '',
    'THE JOB (brief sections 2-8): one signature per character, exclusive to it, using the ordinary skill pipeline - stable id, style, kind, timing, delivery, semantic effects, two variations of three reinforcements each. Not a separate engine, not a simplified exception, and NOT ten reskins of BLOW. Every one needs a memorable reason to exist.',
    '',
    'HARD CONSTRAINTS:',
    '- The ten characters and their exact innate numbers are in CharacterRoster.cs. Preserve every character identity and every innate. You are adding a skill, not redesigning a character.',
    '- A signature must NOT simply restate its innate. If the innate is "+5% damage per living enemy", the signature must not be "+more damage per living enemy" - it should be something that makes a crowded wave into a decision.',
    '- Each of the twenty variations must change what the skill DOES, not how much of it. Each of the sixty reinforcements must be worthless to the OTHER variation of its skill - that is what makes the fork real.',
    '- Prefer dials that already exist. Every new dial is a new read site in a fight loop that is already 2600 lines. If your ten designs need more than about six new dials, you are designing past the architecture.',
    '- Signatures are exempt from mastery unlock (brief sec.18) and are never account-wide (sec.9).',
    '',
    'Return ONLY the structured design. Ten entries, in roster order.',
  ].join('\n')
}

function judgePrompt(lens, panels) {
  return [
    'You are judging three independent designs for the ten character-exclusive SIGNATURE SKILLS of IDLExIDLE. Score them; do not design your own.',
    '',
    GROUND,
    '',
    'YOUR LENS: ' + lens,
    '',
    'THE THREE PANELS (A, B, C) are below as JSON. For EACH panel, score every one of its ten signatures 1-5 through your lens, give the panel a verdict, and name which designs are worth keeping verbatim and which must be replaced.',
    'Score 5 only for a design you would ship unchanged. Score 1 for a reskin, a restatement of the innate, or a design that names a dial the fight does not read.',
    '',
    'PANEL A:', JSON.stringify(panels[0]),
    '',
    'PANEL B:', JSON.stringify(panels[1]),
    '',
    'PANEL C:', JSON.stringify(panels[2]),
    '',
    'Return ONLY the structured score. Cover all three panels: put the panel letter at the start of every `why` and every `best`/`worst` entry.',
  ].join('\n')
}

const CATALOGUES = [
  {
    key: 'traits',
    title: 'THE TRAIT CATALOGUE',
    schemaKey: 'traits',
    prompt: [
      'Design the NEW TRAIT CATALOGUE that replaces the trait tree (brief sections 22-36, and LAWS 5-9).',
      '',
      '24 to 30 traits. Every one:',
      '- is DISCOVERED by playing, never bought - there are no trait points any more;',
      '- has a DETERMINISTIC hidden rule, never RNG, never a missable event (sec.30);',
      '- reads counters the game ALREADY keeps where possible (sec.29). `WaveMetrics` in Expeditions/WaveModel.cs has RawDamage, DeliveredDamage, Hits, Activations, TargetsStruck, CreaturesPresent, CreaturesKilled, HealthLost, DamageAttempted, DamagePrevented, ShieldAbsorbed, HealthDamage, DurationMs, StyleDamage and StyleActivations. Say for each trait exactly which counters its rule reads and whether that counter must become a lifetime accumulator in the save;',
      '- is MEMORABLE IN ONE SENTENCE and is NOT a mastery node (sec.34). A catalogue of "+5% damage" is a failed catalogue. The brief\'s own examples are the shape: SCAR TISSUE, when shield breaks your next shield gain this wave is stronger; PATIENT, if an enemy survives long enough your next active is stronger; LAST WORD, an advantage against the final living enemy;',
      '- has a rule that RELATES to what the trait does - the fantasy is that living through something changed you, so absorbing a great deal of shield should awaken something shield-shaped;',
      '- is naturally discoverable by a player experimenting with builds, characters, sources and defensive or offensive styles (sec.31), not by reading source code;',
      '- has a liveness test scenario: the same fight with and without the trait, and the observable difference when its condition is met (sec.101).',
      '',
      'Cover the behaviour the brief lists (sec.36): heavy hits, overkill, multi-hit, crit, shield, low health, healing, reflect, kill chains, persistent effects, vows, source commitment, set interactions, region experiences, failure and survival. Only three traits may be equipped at once (sec.35), and that limit IS the opportunity cost - do not force a downside onto every trait.',
      '',
      'Also answer: which of the 51 OLD trait-tree nodes have a clean semantic equivalent among your new traits, and which should simply die (sec.59)? Old nodes are in src/IdleXIdle.Core/Prestige/MemoryDust.cs.',
    ].join('\n'),
  },
  {
    key: 'vows',
    title: 'VOW DISCOVERY',
    schemaKey: 'generic',
    prompt: [
      'Design VOW DISCOVERY (brief sections 42-48, and LAW 10).',
      '',
      'Today all 13 vows are taught by five trait-tree nodes; the tree is being deleted, so vows need a new source. The rule the brief wants: THE PLAYER MUST OBEY THE RESTRICTION BEFORE RECEIVING ITS REWARD. A vow that demands a single Source is discovered by finishing a real descent using only one Source WITHOUT the vow equipped.',
      '',
      'Read src/IdleXIdle.Core/Builds/Vows.cs for the real 13 and what each demands and pays. Then give, for every vow: its discovery rule stated as a condition on things the game can already see, whether that condition is checkable at end-of-descent or needs a running counter, and the reveal copy. At least one vow must be available openly from the start so the player learns the system exists (sec.46). No vow may be discovered by RNG or a drop (sec.47).',
      '',
      'Also answer: vows are equipped PER SKILL SLOT today (Build.cs, PlayerLoadout.SkillChoice.VowId), so "vow capacity" (sec.48) is emergent from slot count rather than a number. Say whether that should stay as it is or become a real capacity, and why - the audit warns that three separate systems already multiply vow power.',
    ].join('\n'),
  },
  {
    key: 'keystones',
    title: 'KEYSTONE ACQUISITION',
    schemaKey: 'generic',
    prompt: [
      'Design KEYSTONE ACQUISITION (brief sections 49-52, and LAW 11).',
      '',
      'Today all 19 keystones are taught by trait-tree nodes. They must come from world progression instead: region conquest and region mastery. Read src/IdleXIdle.Core/Builds/Keystones.cs for the real 19, and src/IdleXIdle.Core/Progression/MasteryLevel.cs plus the Encounters region catalogue for what conquest and region mastery actually are.',
      '',
      'There are six regions. Map every one of the 19 keystones to a specific world event - which region\'s conquest, or which region\'s mastery level. Preserve the existing keystone identities; you are assigning them, not redesigning them. Say what a player sees on the conquest that reveals one (sec.51).',
      '',
      'Then design SOCKET CAPACITY separately from discovery (sec.49, sec.52): sockets 1, 2 and 3 should come from automatic account progression. Name the exact milestones, using facts that are already monotone - conquest count, deepest wave. The audit warns that hunter level is explicitly not a gate today.',
      '',
      'Also answer: four enchantments (Fervour, Reverb, Bulwark, Tithe) are inert without a specific keystone or a sworn vow. If keystones get rarer, do those enchantments become dead content? Say what the mapping must guarantee to keep them alive.',
    ].join('\n'),
  },
  {
    key: 'structural',
    title: 'STRUCTURAL UNLOCKS AND SAVE MIGRATION',
    schemaKey: 'generic',
    prompt: [
      'Design the STRUCTURAL UNLOCK MOVE and its SAVE MIGRATION (brief sections 53-60 and 94-97, and LAWS 12-13).',
      '',
      'Every old trait-tree node that unlocks a SYSTEM must move to its correct owner or die. The plan (PLAN.md, decision D9) already assigns them; your job is to make that assignment concrete and to write the migration that preserves what players already have (sec.58: do not take unlocked functionality away).',
      '',
      'Read src/IdleXIdle.Core/Prestige/MemoryDust.cs (the 51 nodes), DustEffects.cs (what each one does), src/IdleXIdle.Core/Progression/Unlocks.cs (the gate system), src/IdleXIdle.Core/Warrens/Warren.cs (the facilities), and src/IdleXIdle.Core/Persistence/SaveGame.cs (the save surface).',
      '',
      'Produce: (1) a row per structural node - what it is, what it gates, its new owner, and the exact milestone; (2) the auto-sell and auto-merge move into Warren facilities, naming which facility and at what level; (3) the migration table - for each old node id in a save, what the new state becomes; (4) every SaveGame field this adds, removes or stops writing, and how a save written by the OLD build still loads.',
      '',
      'The audit\'s warnings you must respect: the ten-second autosave destroys any field the new build stops writing; MemoryDustUnlocks carries four systems in one list; the Dust WALLET must survive the tree\'s deletion; Activity.Traits currently gates on trait points; save version is 3 and a load is refused above it; the Unlocks layer requires every gate fact to be monotone.',
    ].join('\n'),
  },
  {
    key: 'vfx',
    title: 'THE VFX CONTRACT',
    schemaKey: 'generic',
    prompt: [
      'Design the VFX PLACEMENT CONTRACT (brief sections 61-73, and LAWS 14-16).',
      '',
      'Read src/IdleXIdle.Game/VfxPlayer.cs, the arena and every `_vfx.Play` and `_vfx.Hold` site in src/IdleXIdle.Game/HuntScreen.cs, and the pad-fraction measurement in src/IdleXIdle.Game/UiKit.cs (AnimSprite, SpriteGrounded, ContentPad - which returns a full bounding box and has zero consumers today).',
      '',
      'Produce a TYPED, SMALL contract: the anchor vocabulary (only anchors with real consumers), the relative-scale model against the subject\'s VISUAL bounds rather than its texture, the normalized offset model, the layer list, and the follow/facing/lifetime members. Name the C# shape exactly - records and enums, no dictionaries of object, no reflection, no scripting.',
      '',
      'Then give the MIGRATION TABLE: every current effect spawn site, its present ad-hoc arithmetic, and the profile that replaces it. The audit found these facts you must design against: champion-side effects anchor to the un-pushed ChampBox while the champion draws with a lunge offset; enemy effect size is quantised to five integer buckets; the projectile scale is integer division with four reachable values; `_creatureRect` is one frame stale; all 68 fx strips are 4096x512 and VfxPlayer infers frame count from the aspect; the _held dictionary is keyed by asset key so two persistent effects sharing a strip collapse; the additive pass is deliberately unscissored; and three effects (press, weep, wilt) resolve to no asset at all.',
      '',
      'Say what SHIELD specifically must look like on THE SEEKER and THE MAGPIE (sec.106), in numbers: the anchor, the scale against visual height, the offset, the layer. And design the debug view (sec.70) - what it draws and how it is turned on.',
    ].join('\n'),
  },
]

const GENERIC = {
  type: 'object',
  required: ['title', 'sections', 'openQuestions'],
  properties: {
    title: { type: 'string' },
    sections: {
      type: 'array',
      items: {
        type: 'object',
        required: ['heading', 'body'],
        properties: {
          heading: { type: 'string' },
          body: { type: 'string', description: 'markdown. Tables where the prompt asks for a table, complete rather than sampled.' },
        },
      },
    },
    openQuestions: { type: 'array', items: { type: 'string' }, description: 'decisions the implementer must make that this design deliberately leaves open, and the options' },
  },
}

phase('Signatures')
const panels = await parallel(ANGLES.map(function (a) {
  return function () {
    return agent(sigPrompt(a), { label: 'sig:' + a.key, phase: 'Signatures', schema: SIGS, agentType: 'general-purpose' })
  }
}))
const good = panels.filter(Boolean)
log('signature panels: ' + good.length + '/3')

phase('Judge')
const LENSES = [
  'IDENTITY. Does each signature make its character feel like someone specific? Would switching to this character genuinely change how you fight (brief sec.118), or is it the same fight with a different number?',
  'MECHANICS. Does each design work in the dials that exist? Name any dial the fight does not read. Does each variation change what the skill DOES rather than how much? Is each reinforcement worthless to the other variation? Would each reinforcement move damage-dealt or health-kept in a real fight, as the liveness suite demands?',
  'RESTRAINT. Count what the panel adds: new dials, new read sites, new concepts the player must hold. Is any signature a reskin of an existing skill, or a restatement of its own innate? Is the copy plain English in game terms?',
]
const scores = await parallel(LENSES.map(function (lens, i) {
  return function () {
    return agent(judgePrompt(lens, good), { label: 'judge:' + ['identity', 'mechanics', 'restraint'][i], phase: 'Judge', schema: SCORE, agentType: 'general-purpose' })
  }
}))

phase('Catalogues')
const cats = await parallel(CATALOGUES.map(function (c) {
  return function () {
    return agent([c.prompt, '', GROUND, '', 'Return ONLY the structured design.'].join('\n'),
                 { label: 'cat:' + c.key, phase: 'Catalogues', schema: GENERIC, agentType: 'general-purpose' })
      .then(function (r) { return { key: c.key, title: c.title, design: r } })
  }
}))

return { panels: good, scores: scores.filter(Boolean), catalogues: cats.filter(Boolean) }
