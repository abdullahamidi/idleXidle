# SYSTEMS REFACTOR — the migration plan

Brief: `BRIEF.md` (118 sections). Audit: `AUDIT.md` (eight subsystems, file:line evidence).
Section 0 asks for a short concrete migration plan before anything is modified. This is it.

## What the audit changed about the brief's premises

The brief was written from the outside. Five of its assumptions are wrong about this codebase, and
the plan follows the code, as §0 instructs.

| Brief says | The code says | What the plan does |
|---|---|---|
| §55 Forge is "hidden behind a Trait purchase" | `Activity.Forge` opens on chests or two owned items (`Unlocks.cs:127`). Only `efficient_forge` and `auto_merge` are bought; a NODE NAME says "OPENS AUTO-SELL AND FORGE" and lies. | Nothing to unlock. Delete the lying node name, keep the gate, move auto-sell to the Warren. |
| §13 "If the game stores loadouts per Character, preserve each" | It does not. One `PlayerLoadout` (`Game1.cs:367`), one `WovenSkills` row set (`SaveGame.cs:153`). | Repair on switch, do not invent per-character skill loadouts. Traits DO get per-character storage — that is §26 and it is new either way. |
| §60 convert the Trait Point economy | Trait points are DERIVED (`Career.TraitPointsEarned`), recomputed from world state every frame. There is no balance. | Nothing to convert. Delete the function and its one gate consumer. |
| §43/§48/§52 "early Hunter Level" can own a gate | `HunterProgression.cs:168`: "Cosmetic only — never a gate", display readers only. | Build the axis, or gate on what already gates: conquests, region mastery, waves. The plan uses conquest and wave depth, which are already monotone and already tested. |
| §69 persistent VFX must become reconstructable | The Shield already is: `WaveReplay.CurrentShield` is accumulated state and survives a seek. | Do not replace it. Copy its shape for Break and Amplify, which have no visual at all. |

Two more facts that shape everything:

- **The trait tree is the sole producer of two whole catalogues.** All 19 keystones
  (`DustEffects.LearnedKeystones`) and all 13 vows (`DustEffects.KnownVows`) are reachable only
  through it. Deleting the tree without replacing both producers deletes both systems.
- **`MemoryDustTree` is two systems in one class**: the trait tree AND the Memory Dust wallet, a live
  currency with faucets (offline, waves, conquest) and sinks (Warren upgrades, expedition
  checkpoints). The wallet stays. Only the node catalogue goes.

## Decisions

**D1 — Ownership is a field on the definition, not a naming rule.**
`SkillDef` gains `string? OwnerCharacterId = null` as its last optional parameter. It is a 64-member
positional record and a trailing optional does not disturb one existing call site, nor
`tools/check_skill_doc.py`, whose regex pins only the first line. `null` is shared; a character id is
exclusive. §3 satisfied without a second engine (§2, §7).

**D2 — The ten Signatures are NEW skills. The twelve shared skills stay shared.**
§94 is explicit: BLOW stays whatever shared skill it is, and Seeker receives its Signature
separately. So `Character.StartingSkillId` becomes `SignatureSkillId` and points at a newly authored
exclusive skill. This also dissolves three existing problems at once: `hammer_blow` is currently the
birth skill of TWO champions, it is `PlayerLoadout.Starter()`'s hard-coded slot, and it is taught by
`road_hammer` — none of which can survive a skill becoming exclusive.

**D3 — Signature skills use the ordinary pipeline, including its costs.**
Two variations, three reinforcements each, per §7. That is 10 more `SkillDef`s, 20 more variations
and 60 more reinforcements, and the liveness suites (`variation_liveness_test.cs`,
`reinforcement_liveness_test.cs`) will demand every one of them move damage-dealt or health-kept in a
real fight. No signature may be a reskin of BLOW (§6), and none may merely restate its Innate (§5).

**D4 — Access is recomputed, never latched.**
`MasteryTree._learned` and its `LearnSkill` entry point go. Access becomes a pure function:
the skills taught by CURRENTLY TAKEN nodes, plus the active character's Signature. `SaveGame.LearnedSkills`
stays in the format as a read-ignored field for one version so a rollback still loads, and stops
being written. The per-frame latch at `Game1.cs:4326` is deleted — it is the account-wide leak §9
forbids, and it is already in every existing save.

**D5 — Respec tells the truth first.**
Before a respec commits, compute which equipped shared skills lose their node, show
"THIS RESPEC WILL UNEQUIP: …" (§19), then respec and clear exactly those slots. Skill XP is never
touched (§17, LAW 4). A skill with experience but no node reads `LOCKED · Lv.N`, never `UNLEARNED` (§20).

**D6 — Traits are a new Core system; the tree is deleted, the wallet is not.**
`MemoryDustTree` keeps the balance and its faucets and sinks. Its 51-node catalogue,
`TraitTreeLayout`, `TraitRoads` and `Career.TraitPointsEarned` go. A new `Traits` catalogue carries
24–30 traits, each with a deterministic hidden discovery rule reading counters the fight already
keeps (`WaveMetrics` has shield absorbed, damage prevented, health damage, kills, hits, duration and
per-style damage and casts — §29's "real existing telemetry"). Discovery is account-wide (§25); the
three-slot loadout is per character (§26) and is the one piece of per-character save state this
refactor adds.

**D7 — `Activity.Traits` gates on discovery, not on a currency.**
It currently opens at `TraitPointsEarned >= 1`. It will open when the first trait is discovered,
which is monotone — the invariant `Unlocks.cs:116-122` was hard-won and must not be broken by a
non-monotone fact.

**D8 — The fifth skill slot goes, and that is a deliberate removal.**
§54 makes 2 Active + 2 Passive authoritative, and at five slots `ActiveSlotsFor(5)` is 3 active,
which pushes beat demand back to the number the slot rework existed to reduce. `weave_5` is deleted
rather than migrated. This is the one place the plan takes a capability away, against §58's general
rule, because §54 names it specifically. A save holding `weave_5` keeps four slots and loses the
fifth; the migration says so in a toast rather than silently.

**D9 — Structural unlocks move to owners that already exist and are already monotone.**

| Capability | Was | Becomes |
|---|---|---|
| Skill slots 1–4 | `Unlocks.SkillSlots` (progression) and `weave_5` (tree) | `Unlocks.SkillSlots` alone, capped at 4 |
| Keystone discovery | 19 tree nodes | Region conquest and region mastery (§50) |
| Keystone sockets | `socket_2`, `socket_3` (tree) | Conquest count milestones (§52) |
| Vow knowledge | 5 tree nodes → 13 vows | One tutorial vow, free; the rest discovered by obeying the restriction first (§44) |
| Auto-sell / auto-merge | `filter_common`, `filter_uncommon`, `auto_merge` | Warren facilities — Hoard Vaults and Scavenger Runs (§56, LAW 13) |
| Forge screen | already free | unchanged; delete the node name that claims otherwise |
| `recall_*` mastery rate, `artifice_*` vow power, `efficient_forge` | tree numerics | deleted (§59: map only where the equivalent is clean) |

**D10 — The VFX contract is a typed profile plus real visual bounds.**
`UiKit`'s pad-fraction measurement already exists and is correct; `ContentPad` returns a full
bounding box and has zero consumers. Expose it, cache it, and give every effect an anchor
(ActorCenter / ActorFeet / ActorHead / TargetCenter / TargetFeet / WorldPoint), a relative scale
against the subject's visual height, a normalized offset and a layer. Three effects (`press`, `weep`,
`wilt`) currently resolve to nothing and must either get art or stop claiming it. Champion-side
effects anchor to the un-pushed `ChampBox` while the champion is drawn with a lunge offset — the
contract fixes that by construction.

**D11 — Skill roads leave the specialisations, or §16 makes the game unplayable.**
This is the decision the whole of Phase 3 turns on, and the code states the problem itself
(`MasteryTree.cs:86-90`): "one discipline gates one style's road, so learning across respecs is the
only way a four-slot build ever fills from twelve skills." Only ONE specialisation may be taken
(`CanTake`, `:201`), each specialisation heads exactly one style's road, and a road teaches two
skills. Delete the permanence latch and leave that wiring alone, and a hunter has two shared skills
plus a Signature for four slots — permanently, at every point in the career.

So the road's prerequisite stops being its specialisation and becomes its branch's own ring-1 minor,
which costs 1. The economics land where they should: the cheapest first skill falls from 15 points to
6, all twelve roads cost about 64 of a full career's ~66 points, and a mid-career hunter with thirty
points chooses four or five styles to invest in. That is §1's own sentence for MASTERY — "which
shared combat techniques am I investing into RIGHT NOW" — which is only a question you can ask if the
answer can be more than one.

The specialisation keeps the job it is actually for: the affinity ring, its trigger, its shape, and
one per hunter. It stops being a toll gate on the catalogue.

## Phases, as checkpoints

| | Phase | Gate |
|---|---|---|
| P1 | Audit + design tables | this file, `AUDIT.md`, `DESIGN.md` |
| P2 | Signature skills: ownership, ten authored skills, roster + build UI, migration, tests | liveness suites green |
| P3 | Mastery access semantics: recompute, respec warning, loadout repair, lock copy | access tests green |
| P4 | Traits: delete the tree, discovery engine, catalogue, per-character slots, new screen | trait + liveness tests |
| P5 | Vows, keystones, structural unlocks, stale copy | unlock tests |
| P6 | VFX contract, bounds, anchors, layers, debug view, shield validation | two-silhouette captures |
| P7 | Raster quality: nine-slice, frame families, scale audit | ratio diagnostic |
| P8 | Gear inventory as a polished secondary surface | 100/125/150 captures |
| P9 | Validation, migration fixtures, docs, report | everything green |

Each phase is its own commit or small series. Not one unreviewable commit (§114).

## Decisions on what the design left open

The five catalogue designs each end with open questions. They are answered here so no phase starts
blocked, and each answer says what it costs.

**Traits: TWENTY-SIX, not thirty.** The catalogue at thirty costs 38 new dials and 28 new read sites
in a 2,600-line fight loop. Four go, in the design's own order of cheapness: THE LONG SWING (closest
to a bare multiplier, which §34 forbids), DRUMBEAT (needs a new wave-local for a ramp SETTLING WEIGHT
already provides), THE RETURNED BLOW (the third shield trait), THE SETTLED GROUND. That is
twenty-six, inside the brief's 24-30 band, still two traits for most of the fifteen behaviours §36
lists, and about six fewer dials.

**`TraitRules` is a sub-record, not 38 more fields on `SkillShape`.** `SkillCatalogue.cs:130-141`
already argues this case for `SkillRules` — a record at 125 fields is a field list nobody reads. The
cost is one hop at each read site and a second `Combine` to keep in step.

**STANDING PLATE carries HALF the shield, not all of it.** `ShieldRules` states in as many words that
a shield accumulating while nothing happens would make standing still the strongest defensive play.
Halving the carry keeps the invariant nearly intact and still says the sentence the trait is for. The
full-carry version needed a sign-off nobody is here to give, and the conservative reading of a
documented invariant is the right default.

**THE MATCHED SUIT is the narrow version.** "+20% to skills that share the element of your completed
set", not "the set's element becomes every skill's element". Forcing the source takes the player's
own choice away and interacts with PURE, THE SINGLE NOTE and all six Source variations at once. A
trait is a characteristic, not a build override.

**The six retroactive discoveries arrive as ONE plate.** Six traits read save fields that already
exist, so an established account awakens them all on first load. §32 asks for a meaningful awakening
and warns against a ceremony every few minutes; six separate ceremonies in one second is the failure
mode of both. One combined plate, then the ordinary one-at-a-time reveal from there.

**WHAT KILLED YOU needs three consecutive deaths in ONE region.** Across regions the sentence reads
oddly — angry at the armoured of one place because the armoured of another killed you. Slower to
earn and coherent.

**Discovery still requires a cleared wave.** That matches the skill-progression rule the game already
has: a run that dies teaches nothing on its way out. WHAT KILLED YOU is the deliberate exception and
is evaluated at run end, which is how §36's "failure" behaviour is covered.

**Vow capacity becomes a real number.** The vow design's finding is that today's "capacity" is an
accident of slot count, and that the dominant play is to wear the same vow on every skill because the
sim bills a vow once and pays it per skill. That asymmetry was deliberate once; it is degenerate now
that vows are meant to be earned. Capacity becomes a build-level number with automatic milestones.

## Standing risks the plan accepts

- **The ten-second autosave window.** Autosave fires every 10 s and unknown JSON members are dropped
  on read, so the first autosave of a build that stopped writing a field destroys it. Every field this
  refactor stops writing must be verified as genuinely dead first, and the save backup must be taken.
- **Removing permanence removes reach.** One specialisation at a time, and every skill road hangs off
  one, so recomputed access means a hunter reaches fewer skills than today. That is §16's stated
  intent; the plan checks the resulting reachable-skill count before committing, and treats a count
  below the slot count as a balance bug to fix in the tree, not a reason to keep the latch.
- **No test covers VfxPlayer, actor rects or pad fractions.** Phase 6 has no baseline; it writes one
  before it changes anything.
