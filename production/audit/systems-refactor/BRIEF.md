You are working on:

https://github.com/abdullahamidi/idleXidle

The game is **IDLExIDLE**.

This task is a focused gameplay/progression/presentation refactor after the recent skill architecture, set/shield, and UI/UX work.

The current high-level architecture is already much healthier.

Do NOT perform another generic architecture rewrite.

This task has six major goals:

# 1. CHARACTER-EXCLUSIVE SIGNATURE SKILLS
# 2. CORRECT MASTERY SKILL ACCESS SEMANTICS
# 3. REPLACE THE OLD TRAIT TREE WITH DISCOVERABLE TRAITS
# 4. MOVE VOW / KEYSTONE / STRUCTURAL UNLOCKS TO THEIR CORRECT SYSTEMS
# 5. CREATE A CONSISTENT VFX VISUAL CONTRACT
# 6. FIX RASTER / NINE-SLICE / GEAR INVENTORY PRESENTATION QUALITY

PixelLab MCP is directly available.

If required presentation assets do not exist:

# GENERATE THEM YOURSELF.
# INTEGRATE THEM.
# TEST THEM IN THE ACTUAL GAME.

Do not ask the user to provide art.

---

# 0. FIRST — INSPECT LATEST MAIN

Before modifying anything:

1. inspect latest `main`,
2. inspect recent skill/set/shield changes,
3. inspect current Character roster,
4. inspect current Character starting-skill logic,
5. inspect current Mastery skill-unlock logic,
6. inspect skill progression/save state,
7. inspect current Trait Tree and Trait Point systems,
8. inspect Vow unlock/capacity logic,
9. inspect Keystone unlock/slot logic,
10. inspect Forge / Auto-Sell / Skill Slot unlocks,
11. inspect Warren,
12. inspect Build UI,
13. inspect Roster UI,
14. inspect Traits UI,
15. inspect Gear UI,
16. inspect current VFX resolver/player,
17. inspect Hunter/Enemy presentation bounds and animation rig,
18. inspect UI frame assets and nine-slice support,
19. inspect PixelLab MCP capabilities and current art references,
20. inspect persistence/migrations/tests/docs.

Do not trust stale comments or GDDs blindly.

Recent runtime behavior and current authoritative design take precedence.

Before implementation, write a short concrete migration plan.

---

# 1. FINAL SYSTEM OWNERSHIP

Use these responsibilities.

## CHARACTER

> Who am I?

Owns:

- visual identity,
- class,
- innate passive,
- exclusive Signature Skill.

## SIGNATURE SKILL

> What technique belongs specifically to this Hunter?

Character-exclusive.

## MASTERY

> Which shared combat techniques and build philosophies am I investing into RIGHT NOW?

Respeccable.

## SHARED SKILLS

> Which techniques does my current Mastery investment give me access to?

Access may change with Mastery.

Experience does not.

## TRAITS

> What strange characteristics has my account discovered through playing?

Discovered through gameplay history.

Three are equipped per Character.

## VOW

> Which rule have I proven I can obey, and am now choosing to swear for power?

Discovered by satisfying the restriction before receiving its reward.

## KEYSTONE

> Which major build-changing doctrine has my journey through the world revealed?

Unlocked through world/region progression.

## GEAR

> What tools tune the current build?

## WARREN

> What automation/services support the account while away?

Do not let these responsibilities overlap again.

---

# 2. SIGNATURE SKILLS — CORE DESIGN

The current Character "starting skill" model is no longer sufficient.

A Character's characteristic opening technique should be:

# SIGNATURE SKILL

Signature Skills are:

- exclusive to one Character,
- part of that Character's identity,
- unavailable to other Characters,
- not granted account-wide when the Character unlocks,
- not unlocked through Mastery,
- normal combat skills using the same combat grammar.

Do NOT create a separate Signature Skill engine.

A Signature Skill should still use the normal:

`SkillDef / resolved skill / semantic effects / presentation`

architecture.

The difference is OWNERSHIP.

---

# 3. SIGNATURE SKILL OWNERSHIP

Add an explicit ownership concept.

Conceptually something like:

```csharp
OwnerCharacterId?
```

or another strongly typed equivalent.

Normal shared skill:

```text id="wy8y0i"
OwnerCharacterId = null
```

Signature skill:

```text id="8tedcv"
OwnerCharacterId = seeker
```

Do not identify ownership through string naming conventions.

---

# 4. ONE SIGNATURE SKILL PER CURRENT CHARACTER

The current roster has ten Characters.

Each Character should receive exactly one authored Signature Skill.

Audit the latest roster rather than assuming stale data.

Current identities likely include:

- SEEKER
- MAGPIE
- ANVIL
- FALLING TOWER
- CHORUS
- OATHBOUND
- METRONOME
- QUIVER
- UNBROKEN
- THORNWALL

Preserve Character identities.

Do NOT casually rename Characters or their Innates.

---

# 5. SIGNATURE SKILL DESIGN

A Signature Skill must strongly fit:

- Character fantasy,
- current Innate,
- Character combat identity.

But:

# DO NOT MAKE THE SIGNATURE SKILL SIMPLY DUPLICATE THE INNATE.

Example philosophy:

If the Character's Innate alters Overkill:

the Signature may create an unusual heavy-hit opportunity that benefits from that Innate.

It should NOT simply say:

`More Overkill`.

The two systems should interact.

---

# 6. DESIGN ALL TEN SIGNATURES BEFORE CODING THEM

Before implementation, produce a design table containing:

- Character
- Signature Skill name
- Style
- Kind: Active / Passive / Reaction
- one-sentence combat fantasy
- base mechanic
- why it fits the Character
- how it interacts with their Innate
- Source variations if the current skill architecture requires them
- reinforcement direction
- presentation identity

Review the current character kit first.

Do not create ten reskins of BLOW.

Every Signature should have a memorable reason to exist.

---

# 7. SIGNATURE SKILLS USE CURRENT SKILL ARCHITECTURE

Do NOT create a simplified legacy exception.

If the current normal Skill architecture is:

`STYLE → SKILL → VARIATION → REINFORCEMENTS`

then Signature Skills should use the same authoring pipeline wherever practical.

They should have:

- stable SkillId,
- Style,
- Kind,
- timing,
- delivery,
- semantic effects,
- presentation identity,
- progression through use.

If current skill progression requires two variations and reinforcement paths:

use that same model rather than inventing:

`SignatureSkillSpecialCase`.

If a Signature genuinely requires a narrower structure, explain why before introducing an exception.

---

# 8. SIGNATURE SKILL PROGRESSION

Signature Skill experience belongs to that Skill and is permanent.

Character switching does NOT erase it.

Example:

```text id="we8byn"
SEEKER SIGNATURE
Lv.4
```

Switch to Magpie.

Later return to Seeker:

```text id="9ubtb4"
SEEKER SIGNATURE
Lv.4
```

Progression remains.

---

# 9. CHARACTER UNLOCK DOES NOT UNLOCK SIGNATURE ACCOUNT-WIDE

When MAGPIE unlocks:

MAGPIE's Signature becomes available while playing MAGPIE.

It does NOT become a shared Skill usable by SEEKER.

This is a hard invariant.

---

# 10. SHARED SKILLS REMAIN SHARED

Do NOT convert the current twelve shared skills into Character-locked skills.

The shared Skill Library remains valuable.

Architecture:

```text id="2uyjrg"
CURRENT CHARACTER
    │
    ├── its exclusive Signature Skill
    │
    └── shared Skills currently granted by Mastery
```

This creates two distinct sources of skill access.

---

# 11. SIGNATURE UI

Update Roster terminology:

`STARTING SKILL`

becomes:

# SIGNATURE SKILL

Character inspector should show:

```text id="t9m6w8"
SIGNATURE SKILL
<skill name>

<short description>

EXCLUSIVE TO <CHARACTER>
```

Do not imply it becomes account-wide.

---

# 12. BUILD UI — SIGNATURE PRESENTATION

The active Character's Signature should be clearly identifiable.

Possible label:

`SIGNATURE · SEEKER`

It may be pinned near the top of the Skill Library or visually marked.

Do not hide it among twelve shared Skills without distinction.

Other Characters' Signature Skills should NOT appear as normal available skills.

---

# 13. CHARACTER SWITCHING AND SIGNATURE VALIDITY

When Character switching causes an equipped Signature Skill to become invalid:

the runtime must repair the loadout safely.

Do NOT leave an invalid foreign Signature in a slot.

At minimum:

- unequip the old Character's Signature,
- preserve its progression,
- expose the new Character's Signature as available.

If the current game stores loadouts per Character, preserve each Character's valid loadout instead.

Inspect the current save/loadout architecture and choose the least surprising behavior.

Do not silently convert one Signature into another unrelated Signature.

---

# 14. DUPLICATE-SKILL INVARIANT REMAINS

The same SkillId may not occupy multiple Skill slots.

This applies to:

- shared Skills,
- Signature Skills.

A Signature cannot be duplicated either.

Enforce below UI level.

---

# 15. MASTERY ACCESS SEMANTICS — CHANGE THE OLD RULE

Remove the rule:

> "Once a Skill has been discovered through Mastery, it remains permanently usable after Mastery reset."

That rule is no longer desired.

It makes Skill unlock nodes effectively free.

---

# 16. NEW MASTERY RULE

Use:

# ACTIVE MASTERY NODE → SHARED SKILL AVAILABLE

If the Skill's unlock node is no longer allocated:

# SHARED SKILL BECOMES UNAVAILABLE

This is intentional.

Mastery reflects the CURRENT build.

---

# 17. SKILL EXPERIENCE REMAINS PERMANENT

Mastery access and Skill experience are separate concepts.

Example:

BLOW has:

```text id="6sju6m"
Lv.4
```

Player resets Mastery and loses BLOW's unlock node.

BLOW becomes:

```text id="afnw87"
LOCKED BY MASTERY
Lv.4
```

Later the player allocates the node again.

BLOW returns at:

```text id="m1c56t"
Lv.4
```

Use the rule:

# ACCESS IS TEMPORARY
# EXPERIENCE IS PERMANENT

---

# 18. SIGNATURE SKILLS ARE EXEMPT FROM MASTERY ACCESS

The current Character's Signature Skill does NOT require a Mastery Skill-unlock node.

Do not duplicate Signature unlocks inside Mastery.

---

# 19. MASTERY RESPEC LOADOUT REPAIR

Before confirming a Mastery reset/respec:

calculate which equipped shared Skills will become unavailable.

If any are affected:

show:

```text id="ksjku1"
THIS RESPEC WILL UNEQUIP:

BLOW
MIRE
```

Then perform the respec and clear invalid slots.

Do not destroy their progression.

Do not block respec merely because a Skill is equipped.

---

# 20. BUILD UI LOCK STATE

A shared Skill without its active Mastery node may remain visible in the Library for progression/history clarity.

Show something like:

```text id="c31bmf"
BLOW
Lv.4

LOCKED
Requires active Mastery node.
```

Do not show:

`UNLEARNED`

if the Skill has existing experience.

---

# 21. REMOVE PERMANENT SKILL DISCOVERY STATE WHERE OBSOLETE

Audit fields representing:

- permanently learned through Mastery,
- discovered forever,
- permanent Mastery Skill unlock.

If no longer needed:

remove them from current runtime.

Keep narrow migration compatibility only where necessary.

---

# 22. TRAITS — REMOVE THE TRAIT TREE

The existing Trait Tree architecture is no longer the desired system.

Traits are NOT:

- a second Mastery Tree,
- a permanent passive tree,
- a structural unlock tree.

Remove the current Trait Tree as the authoritative progression model.

Do not merely rearrange its nodes.

---

# 23. REMOVE TRAIT POINT SPENDING

Traits should no longer be bought with:

`Trait Points`

There is no Trait allocation tree.

Audit and remove:

- Trait Point UI,
- Trait Point spend logic,
- tree path requirements,
- permanent node purchases,
- road/capstone tree logic

from normal current runtime where superseded.

Migrate old saves safely.

---

# 24. NEW TRAIT MODEL

Traits work like hidden gameplay achievements.

A Trait is:

# DISCOVERED THROUGH WHAT THE PLAYER HAS DONE

After discovery:

it enters the account Trait collection.

The player then chooses which Traits to equip.

---

# 25. TRAIT DISCOVERY IS ACCOUNT-WIDE

Once a Trait has been discovered:

every Character may potentially equip it.

Do NOT require ten Characters to independently rediscover the entire Trait catalogue.

That would create ten parallel progression careers.

---

# 26. TRAIT LOADOUT IS PER CHARACTER

Each Character has exactly:

# 3 ACTIVE TRAIT SLOTS

Example:

```text id="2c16d3"
SEEKER

ACTIVE TRAITS

[SCAR TISSUE]
[PATIENT]
[LAST WORD]
```

Magpie may equip a different three.

Trait selection is part of Character identity/build configuration.

---

# 27. TRAIT SWAPPING

Trait swapping should be:

- free,
- available outside active combat/descent where appropriate,
- reversible.

The discovery is permanent.

The equipped combination is flexible.

Do not require another currency to respec Traits.

---

# 28. TRAIT DISCOVERY CONDITIONS ARE HIDDEN

Before discovery:

the player should NOT see a checklist like:

```text id="h2jyvl"
Absorb 10,000 Shield
6,322 / 10,000
```

Instead:

```text id="vl7xse"
???
```

or a mysterious locked glyph.

This is intentional.

Traits should feel discovered rather than farmed from a visible achievement list.

---

# 29. TRAIT DISCOVERY RULES STILL EXIST INTERNALLY

Hidden from the player does NOT mean random.

Every Trait must have a deterministic discovery rule.

Rules may examine accumulated gameplay history such as:

- Shield absorbed,
- Shield breaks,
- low-health survival,
- overkill,
- executions,
- Crit behavior,
- repeated hits,
- healing,
- damage taken,
- reflected damage,
- Vow completions,
- region conquest,
- difficult failed runs,
- long fights,
- fast clears,
- set behavior,
- Source behavior.

Use real existing telemetry/counters where possible.

Do not store full combat history when a bounded counter is sufficient.

---

# 30. TRAIT DISCOVERY MUST NOT BE RNG

No Trait should require:

- random proc,
- lucky item drop,
- rare hidden enemy RNG,
- missable one-time event.

Discovery should happen because of player behavior.

Mystery is good.

Lottery is not.

---

# 31. TRAITS SHOULD BE NATURALLY DISCOVERABLE

Do not design conditions so obscure that a player would only discover them by reading source code.

A player experimenting with different:

- builds,
- Characters,
- Sources,
- defensive styles,
- offensive styles

should gradually awaken Traits naturally.

No exact condition needs to be displayed.

---

# 32. TRAIT REVEAL

When a Trait unlocks:

show a meaningful reveal.

Example:

```text id="ymfjn2"
A TRAIT HAS AWAKENED

SCAR TISSUE

"The body remembers what survives."
```

Then make it available in Traits.

Do not turn it into a giant blocking achievement ceremony every few minutes.

Rare enough to feel meaningful.

---

# 33. OPTIONAL DISCOVERY PROVENANCE

Where inexpensive and useful, record:

- Character who first awakened it,
- Region,
- timestamp/progression context.

Player-facing flavor may show:

`FIRST AWAKENED BY THE SEEKER`

This supports:

> What you lived through changed you.

Do not overbuild a historical database.

---

# 34. TRAIT CONTENT PHILOSOPHY

Traits should NOT look like Mastery nodes.

Avoid a catalogue dominated by:

- +5% damage,
- +8% health,
- +4% skill rate.

Training, Gear and Mastery already own broad numeric scaling.

Trait should be memorable in one sentence.

Examples of suitable design shape:

```text id="gji15k"
SCAR TISSUE
When Shield breaks, your next Shield gain this wave is stronger.
```

```text id="31e9xi"
PATIENT
If an enemy survives long enough, your next Active becomes stronger.
```

```text id="2ro0oj"
LAST WORD
Gain an advantage against the final living enemy.
```

Exact final catalogue must be designed against current combat systems.

---

# 35. TRAIT SLOT LIMIT IS THE OPPORTUNITY COST

Not every Trait needs a downside.

The player only gets:

# 3 ACTIVE TRAITS

That is already a meaningful opportunity cost.

Some Traits may contain tradeoffs where thematic.

Do not force every Trait into:

`upside + downside`.

---

# 36. DESIGN A NEW TRAIT CATALOGUE

Create approximately:

# 24–30 TRAITS

as the initial target.

Do not blindly use exactly 24 if content quality suffers.

The catalogue should cover diverse behavior:

- heavy hits,
- overkill,
- multi-hit,
- Crit,
- Shield,
- low Health,
- healing,
- reflect,
- kill chains,
- persistent effects,
- Vows,
- Source commitment,
- set interactions,
- region experiences,
- failure/survival.

Do NOT create 30 variants of "deal more damage".

Before implementation provide a Trait design table:

- stable ID
- display name
- one-line effect
- semantic category/tag
- hidden discovery rule
- why the rule relates to the Trait
- reset/state requirements
- liveness test scenario

---

# 37. TRAITS UI — NO TREE

The Traits screen must be redesigned around:

# DISCOVERED CHARACTERISTICS
+
# 3 ACTIVE TRAIT SLOTS

Do not present another node graph.

Keep the existing mystical/starfield atmosphere if it still works.

---

# 38. TRAITS UI — CONCEPTUAL LAYOUT

A possible structure:

```text id="7arp5k"
                   ACTIVE TRAITS

          [TRAIT]   [TRAIT]   [TRAIT]


                 [ACTIVE HUNTER]


         DISCOVERED / UNKNOWN TRAITS

      [trait] [trait] [???] [trait] [???]
      [???]   [trait] [???] [???]   [trait]
```

Do not treat this exact geometry as mandatory.

The important mental model is:

- Hunter identity,
- three equipped Traits,
- discovered collection,
- mysterious unknown Traits.

---

# 39. UNKNOWN TRAIT PRESENTATION

Undiscovered Traits should appear as:

`???`

Use:

- mysterious silhouette,
- restrained glyph,
- subtle star/constellation treatment.

Do NOT show:

- exact unlock condition,
- progress bar,
- percentage,
- internal counter.

---

# 40. TRAIT INSPECTOR

For discovered Trait:

```text id="0i6jfi"
SCAR TISSUE

<flavor>

WHAT IT DOES
...

CURRENT STATE
Equipped by Seeker / Available

[EQUIP]
```

For unknown Trait:

```text id="syl8fz"
???

Something remains undiscovered.
```

Do not expose developer hints unless there is an intentional design reason.

---

# 41. PIXELLAB — TRAIT VISUALS

The current Trait Tree assets were designed for the old node system.

Do NOT force them into the new system if they look inappropriate.

Use PixelLab MCP where necessary to create:

- Trait glyph family,
- unknown `???` sigil,
- active Trait slot treatment,
- mystical constellation/background accents,
- Trait reveal emblem.

Reuse current starfield/background if good.

Do not ask the user for assets.

---

# 42. VOWS — REMOVE TRAIT TREE UNLOCKS

Remove old Trait nodes such as:

- LEARN VOWS I
- LEARN VOWS II
- LEARN VOWS III

or current equivalents.

Vow access/capacity should not consume Trait choices.

---

# 43. VOW SYSTEM ACCESS

The Vow system itself should unlock automatically through an early account/world progression milestone.

Choose a milestone that fits the current pacing after auditing the game.

Examples of valid ownership:

- early Hunter Level,
- first Region Conquest,
- early progression milestone.

Do not require Trait Points.

---

# 44. VOW DISCOVERY — PROVE THE RULE FIRST

Individual Vows should use a thematic discovery rule:

> The player must obey the restriction BEFORE receiving its reward.

Example concept:

A Vow requires:

`Single Source`.

Before it is discovered:

the player completes a meaningful run/descent using only one Source WITHOUT the Vow equipped.

Then:

```text id="a8kcbn"
A VOW HAS REVEALED ITSELF

ONE VOICE

"You followed the law before it promised you power."
```

The Vow becomes permanently available.

---

# 45. VOW DISCOVERY IS ACCOUNT-WIDE

Once discovered:

the Vow becomes part of the account's Vow collection.

It remains a build choice.

---

# 46. VOW DISCOVERY CONDITIONS MAY ALSO BE HIDDEN

Like Traits:

do not necessarily reveal exact unlock conditions.

At least one early/tutorial Vow should be available openly so the player knows the system exists.

The rest may be mysterious discoveries.

---

# 47. VOW CONDITIONS MUST BE PLAYER-CONTROLLABLE

Vow discovery rules must be based on intentional build restrictions.

Good:

- one Source,
- one Style,
- empty slot,
- no Crit investment,
- no defensive investment,
- cadence threshold.

Bad:

- random proc,
- rare drop,
- enemy RNG.

---

# 48. VOW CAPACITY

If the game supports multiple simultaneously equipped Vows:

their capacity should unlock through AUTOMATIC account progression.

Do not move:

`LEARN VOWS II`

from Trait Tree into another spend tree.

Use clear milestones.

Exact count/pacing should be based on current balance.

---

# 49. KEYSTONES — REMOVE TRAIT TREE UNLOCKS

Keystone access must no longer depend on Trait nodes.

Separate:

# KEYSTONE DISCOVERY
from
# KEYSTONE SLOT CAPACITY

---

# 50. KEYSTONE DISCOVERY

Keystones should be discovered primarily through:

# REGION CONQUEST / REGION MASTERY

This makes world progression expand build vocabulary.

Audit current Keystone catalogue.

Preserve good existing Keystone identities.

Assign them to meaningful world progression.

A useful model may be:

- first conquest → one Keystone,
- deeper Region Mastery → another Keystone.

Do not force this exact count if current content makes another distribution better.

---

# 51. KEYSTONE REVEAL

A conquest may produce:

```text id="o1926x"
CINDERWORKS CONQUERED

NEW KEYSTONE DISCOVERED

GLASS CANNON
```

This should feel like discovering a doctrine/relic from the world.

Not buying a menu feature.

---

# 52. KEYSTONE SLOTS

Keystone slot capacity should unlock automatically through account progression.

Examples:

- early milestone → slot 1,
- mid progression → slot 2,
- late progression → slot 3.

Audit current balance.

Do not make the player choose between:

`interesting Trait`

and:

`basic Keystone socket`.

---

# 53. STRUCTURAL UNLOCKS LEAVE TRAITS

Audit all old Trait Tree structural nodes.

They must move to their correct owners.

Use these design directions.

---

# 54. SKILL SLOTS

The authoritative build remains:

- 2 Active
- 2 Passive

Do not retain a legacy fifth Skill slot unless the current design has explicitly changed.

Skill-slot access should come from:

# AUTOMATIC HUNTER / ACCOUNT PROGRESSION

not Traits.

If all four slots should not be available immediately:

unlock them at natural progression milestones.

Do not charge Trait Points.

---

# 55. FORGE ACCESS

Forge is a core equipment system.

Do not hide it behind a Trait purchase.

Unlock it automatically through an early meaningful progression milestone.

Preferred thematic owner:

- first Region Conquest,
- or equivalent early account progression.

Preserve access for existing saves that already had Forge.

---

# 56. AUTO-SELL

Auto-Sell belongs naturally to:

# WARREN AUTOMATION

Move its unlock/progression toward facilities such as:

- Hoard Vaults,
- Scavenger Runs,

or the most semantically appropriate current facility.

Do not keep:

`AUTO-SELL`

as a Trait.

Warren already owns:

- automation,
- account services,
- idle support.

---

# 57. OTHER STRUCTURAL TRAITS

Repository-wide audit old Trait nodes involving:

- Forge,
- Auto-Sell,
- Skill Slots,
- Vows,
- Keystone sockets,
- system access.

Every one must be:

- moved,
- migrated,
- or deleted.

No old structural Trait should remain merely because saves/tests reference it.

---

# 58. OLD TRAIT SAVE MIGRATION

Save migration is important.

Existing saves may already have purchased structural Trait nodes.

Do NOT take unlocked functionality away.

For each legacy structural purchase:

map it into the new system.

Examples:

Old save had Forge Trait:

→ Forge remains unlocked.

Old save had second Keystone socket:

→ preserve equivalent Keystone slot access.

Old save had Auto-Sell:

→ preserve automation access or migrate into the appropriate Warren capability.

Old save had additional Skill slot:

→ preserve valid current slot accessibility up to the authoritative 2A+2P model.

---

# 59. OLD COMBAT TRAITS

Some old Trait nodes may represent actual combat bonuses.

Do not automatically convert every old node into a new discovered Trait.

Only map when there is a clean semantic equivalent.

Otherwise:

- remove obsolete node,
- preserve unrelated progression,
- do not create meaningless one-to-one baggage.

Because the new Trait system is fundamentally different, exact tree topology does not need to survive.

---

# 60. TRAIT POINT MIGRATION

Trait Points no longer have a spending purpose.

After structural migration:

remove Trait Points from current progression/UI.

If there is a meaningful current economy conversion that does not create imbalance, evaluate it.

Do not invent another currency merely to compensate.

Since this is a design-breaking pre-release progression migration, clarity is preferable to carrying a meaningless currency forever.

Document the decision.

---

# 61. VFX — DEFINE A VISUAL CONTRACT

The current VFX system needs a consistent placement/scale standard.

Recent generated effects may appear:

- too small,
- in the actor's mathematical center,
- inconsistent across differently sized Characters.

Fix the SYSTEM, not just individual Shield sprites.

---

# 62. DO NOT POSITION EFFECTS USING RANDOM PIXEL OFFSETS

Avoid scattered code like:

```text id="qx9y7t"
x += 17
y -= 23
scale = 0.42
```

for individual skills.

Presentation definitions should describe effects semantically relative to the visual subject.

---

# 63. VFX ANCHOR VOCABULARY

Create a small strongly typed anchor vocabulary.

At minimum consider:

## ActorCenter

- body aura,
- barrier.

## ActorFeet

- ground field,
- circle,
- footprint effect.

## ActorHead

- status sigil,
- overhead marker.

## ActorChest

- body-centered heal/buff where useful.

## TargetCenter

- hit impact,
- execute.

## TargetFeet

- enemy field/trap.

## Weapon / Hand

If current cutout rig supports a reliable semantic anchor.

## WorldPoint

For explicitly world-positioned effects.

Do not create dozens of anchors.

Use only real consumers.

---

# 64. USE VISUAL BOUNDS, NOT RAW TEXTURE CENTER

Character sprites have different proportions.

Seeker and Magpie are obvious examples.

Effect placement should reference:

# ACTOR VISUAL BOUNDS

not merely:

`texture.Width / 2`

if the texture contains transparent padding or very different silhouettes.

Use:

- rig bounds,
- authored presentation bounds,
- sprite visible bounds,

whichever is robust in the current architecture.

---

# 65. VFX RELATIVE SIZE

Effect size should usually be relative to:

- actor height,
- target height,
- world radius,

rather than arbitrary raw pixels.

Conceptual examples:

## Shield barrier

~1.10–1.20 × Hunter visual height.

## Normal impact

~0.25–0.40 × Target visual height.

## Execute impact

~0.50–0.70 × Target visual height.

## Body aura

~1.0–1.2 × actor height.

## Ground field

derived from semantic field/world radius.

These are starting guidelines, not mandatory exact constants.

---

# 66. VFX PRESENTATION PROFILE

Create or evolve a strongly typed profile concept such as:

```text id="8ngz88"
Anchor
RelativeScale
NormalizedOffset
Layer
FollowMode
FacingMode
Lifetime / HoldMode
```

Exact names are up to the implementation.

Avoid:

- Dictionary<string, object>
- generic reflection metadata
- visual scripting.

This should remain small and typed.

---

# 67. NORMALIZED OFFSET

Offset should usually be normalized relative to visual bounds.

Example:

```text id="zis1k0"
OffsetX = 0.10 actor widths
OffsetY = -0.15 actor heights
```

rather than resolution-dependent magic numbers.

Small authored pixel correction may still exist at final presentation layer if required.

But normalized placement should be the primary model.

---

# 68. VFX LAYERING

Define consistent semantic layers such as:

- GroundBehind
- BehindActor
- ActorOverlay
- FrontImpact
- Overhead

Only create what current rendering actually needs.

Shield barrier must not accidentally render behind the background or over every HUD element.

---

# 69. PERSISTENT VFX

Stateful effects such as:

- Shield,
- Break,
- persistent Field,
- Amplification

must remain reconstructable from battle state.

Do not rely only on having seen the original event.

The existing Hold/state infrastructure should be reused where good.

---

# 70. VFX DEBUG MODE

Add a development/debug visualization if practical.

It should be able to show:

- Actor visual bounds,
- Target bounds,
- anchor point,
- effect destination bounds,
- normalized offset.

This will make future PixelLab/generated VFX much easier to integrate correctly.

Do not ship it enabled in normal gameplay.

---

# 71. VFX CROSS-CHARACTER VALIDATION

Every actor-relative VFX must be tested on at least two significantly different Character silhouettes.

For example:

- SEEKER
- MAGPIE

Shield must not look correct only on one Character.

Likewise test relevant enemy sizes.

---

# 72. PIXELLAB VFX ASSETS

PixelLab MCP is available.

If an effect asset is:

- too small,
- stylistically inconsistent,
- wrong aspect ratio,
- impossible to scale cleanly,

regenerate it.

Do NOT permanently compensate for a badly sized source asset with:

`scale = 3.4`.

Generate the correct visual asset.

---

# 73. ASSET SCALE BUDGET

For ordinary raster UI/VFX assets that are not intentionally scalable:

aim for actual rendering close to native scale.

A useful default budget:

# approximately 0.75× – 1.25× native

Going outside this range should trigger review.

This is not a universal mathematical prohibition.

Large intentional effects may differ.

But:

> "We generated the wrong resolution and stretched it"

is not acceptable.

---

# 74. UI RASTER QUALITY — NO ARBITRARY FRAME STRETCHING

The current large `EQUIPPED` frame visibly loses quality because decorative raster art appears enlarged beyond its intended scale.

Fix this class of problem globally.

Hard rule:

# DECORATIVE RASTER FRAMES MAY NOT BE ARBITRARILY STRETCHED.

---

# 75. USE NINE-SLICE WHERE APPROPRIATE

For scalable UI frames:

use proper nine-slice.

Corners:

- remain native quality.

Edges:

- tile/stretch only in their intended direction.

Center:

- fills.

Do not scale detailed corners 2×–3×.

---

# 76. SIZE-SPECIFIC FRAME FAMILIES

Some ornament frames are too illustrated for clean nine-slicing.

In that case:

use size-specific assets.

Example family:

```text id="mjcpq9"
PanelFrameSmall
PanelFrameMedium
PanelFrameGrand
```

Generate them through PixelLab MCP if necessary.

Do NOT stretch `Small` into `Grand`.

---

# 77. PIXELLAB FRAME GENERATION

If the `EQUIPPED` panel needs a new high-resolution frame:

generate it directly.

Requirements:

- same IDLExIDLE gold/bronze ornament language,
- transparent center where pipeline expects it,
- designed for the actual target aspect ratio,
- corners readable at actual game size,
- not excessively detailed,
- compatible with the surrounding Inspector frame.

Integrate and screenshot-test it.

Do not ask the user.

---

# 78. UI SCALE MUST NOT DESTROY FRAME QUALITY

The UI Scale system should not simply enlarge ornate raster textures.

100/125/150 are density/reflow profiles.

Decorative chrome should remain at appropriate pixel density.

Use:

- nine-slice,
- alternate asset size,
- native-size composition

where required.

---

# 79. ADD A DEVELOPMENT ASSET QUALITY AUDIT IF PRACTICAL

Where inexpensive, add a development-only diagnostic for UI textures.

For decorated raster surfaces, it may log:

- texture native dimensions,
- destination dimensions,
- effective scale ratio.

Flag suspicious cases such as:

`3.1× decorative frame scale`.

Do not enforce this on every tiny icon if it creates noise.

The purpose is preventing future large low-quality frames.

---

# 80. GEAR — INVENTORY PANEL NEEDS ANOTHER POLISH PASS

The current GEAR screen has a quality imbalance.

`EQUIPPED`

looks like a production-quality fantasy panel.

`ITEM DETAIL`

also has strong presentation.

The middle:

`INVENTORY`

still looks comparatively prototype/debug-like.

Fix it.

---

# 81. DO NOT TURN INVENTORY INTO ANOTHER HEAVY ORNATE PANEL

Inventory is a:

# SECONDARY SURFACE

It should look finished without competing with Equipped.

Use:

- thin bronze/gold edge,
- clean panel header,
- proper padding,
- subtle surface treatment,
- consistent tab system,
- quiet inventory grid.

---

# 82. INVENTORY HEADER

The top should clearly communicate:

```text id="js5rij"
INVENTORY                    8 ITEMS
```

Use the same typography hierarchy as other production panels.

Do not use tiny debug-like text.

---

# 83. INVENTORY FILTER TABS

Current:

- ALL
- WEAPONS
- ARMOR
- ACCESSORY

should use the same finished tab component family.

States:

- normal,
- hover,
- selected,
- disabled if necessary.

Selected:

gold/accent.

Inactive:

quiet.

Do not make them look like generic prototype rectangles.

---

# 84. INVENTORY GRID

Give the inventory grid a subtle authored surface.

Possible:

- slightly darker inner panel,
- quiet separators,
- subtle slot recesses.

Empty cells remain subdued.

Items should visually emerge from the grid.

Do not use strong frames around every empty slot.

---

# 85. INVENTORY ITEM STATES

Clearly support:

- normal,
- hover,
- selected,
- equipped,
- locked/favorite,
- rarity,
- Source.

Do not communicate all of these through border color alone.

Use icons/badges where appropriate.

---

# 86. INVENTORY FOOTER

Information such as:

`SORTED BY RARITY`

and:

`RIGHT-CLICK FOR MORE`

should use consistent Secondary typography.

Keep them readable.

Do not leave them floating in the middle of empty space.

---

# 87. INVENTORY RESPONSIVE BEHAVIOR

Inventory should behave correctly at:

- 100%
- 125%
- 150%

UI Scale.

At high scale:

- reduce visible columns if necessary,
- scroll,
- preserve item readability.

Do not shrink text back down to make the grid fit.

---

# 88. PIXELLAB — INVENTORY ASSETS

If no suitable secondary-panel frame/header/tab treatment exists:

generate the necessary assets through PixelLab MCP.

Do NOT make Inventory ornate enough to compete with Equipped.

The visual target is:

# POLISHED SECONDARY PANEL

not:

# THIRD GRAND FRAME

---

# 89. TRAITS UI ASSET QUALITY

The new Trait system will require a new presentation language.

Do not reuse old rectangular Trait-node assets merely because they exist.

Use PixelLab where necessary to create a coherent family.

Potential assets:

- active Trait slot,
- discovered Trait sigil,
- unknown `???` sigil,
- Trait reveal glyph,
- selected Trait accent.

Keep starfield/mystical background if it still supports the concept.

---

# 90. TRAITS SHOULD FEEL MYSTERIOUS, NOT LIKE AN ACHIEVEMENT MENU

Although internally Traits behave like achievements:

do not present:

- trophies,
- completion percentages,
- checklist bars,
- achievement badges.

The player-facing fantasy is:

# CHARACTERISTICS AWAKEN THROUGH EXPERIENCE

not:

# COMPLETE ACHIEVEMENT 17/30

---

# 91. VOW UI DISCOVERY

Before individual Vow discovery:

the Vow may appear as:

`???`

or remain hidden depending on current Build UX.

Do not show the exact condition.

Upon discovery:

use a short Vow reveal.

Keep Vow configuration in its current appropriate build-facing location.

Do NOT turn Vows into a second collection screen unless needed.

---

# 92. KEYSTONE UI

Keystones remain build tools.

Their discovery source changes.

Update UI copy so it no longer says:

`Learn this in Traits`

or equivalent.

Instead:

if locked and provenance is appropriate:

```text id="h8om1k"
UNDISCOVERED
Found through Region Conquest.
```

Do not necessarily reveal the exact future Region if mystery is preferred.

Use actual final design consistently.

---

# 93. STRUCTURAL UI COPY CLEANUP

Repository-wide search for player-facing strings implying Traits unlock:

- Skill slots,
- Vows,
- Forge,
- Keystone slots,
- Auto-Sell.

Replace with the new real source.

Examples:

Old:

`MORE SLOTS — TRAITS`

New:

`UNLOCKS AT HUNTER LEVEL ...`

or current chosen progression wording.

Do not leave stale information.

---

# 94. SAVE MIGRATION — SIGNATURE SKILLS

Old saves may have Character starting Skills added to the shared Skill library.

Do NOT delete the shared Skill progression.

If BLOW was historically Seeker's starting skill:

BLOW remains whatever shared Skill it now is.

Seeker receives its new Signature separately.

Do not steal BLOW XP and move it away.

For already unlocked Characters:

their Signature Skill becomes available according to ownership.

Initial Signature progression can start at the normal baseline unless a clean migration model exists.

Do not manufacture duplicate progression.

---

# 95. SAVE MIGRATION — MASTERY ACCESS

Old save may contain:

`permanently learned Skill`

without active Mastery node.

After migration:

- preserve Uses/Level,
- recompute access from current Mastery allocation,
- lock the Skill if node inactive.

Do not silently allocate Mastery points to preserve permanent access.

Mastery allocation remains authoritative.

---

# 96. SAVE MIGRATION — TRAITS

Old Trait Tree state must be migrated.

Priorities:

1. preserve structural capabilities,
2. preserve account progression,
3. remove obsolete Trait Point/tree state,
4. optionally map clean combat Trait equivalents,
5. do not preserve old tree topology.

The new Trait catalogue begins as discovered/undiscovered based on explicit migration mappings where appropriate.

Do not automatically unlock all new Traits because the old tree was progressed.

---

# 97. SAVE MIGRATION — VOWS / KEYSTONES

Already unlocked Vows remain unlocked.

Already available Keystones remain discovered.

Already earned capacities remain preserved.

Do not force existing players to rediscover functionality they already had.

---

# 98. TESTS — SIGNATURE SKILLS

Add tests for:

1. each Character has exactly one Signature Skill,
2. every Signature has valid OwnerCharacterId,
3. no two Character signatures accidentally share the same SkillId,
4. active Character may equip its Signature,
5. other Character cannot equip it,
6. unlocking Character does not grant Signature account-wide,
7. progression persists through Character switches,
8. Signature does not require Mastery unlock,
9. duplicate Signature equip is rejected,
10. save/load preserves ownership.

---

# 99. TESTS — MASTERY ACCESS

Test:

1. allocate Skill unlock node,
2. Skill available,
3. gain Skill experience,
4. respec node away,
5. Skill inaccessible,
6. Skill level retained,
7. reallocate node,
8. Skill returns at same level.

Also verify:

- equipped inaccessible Skill is automatically unequipped,
- UI/build model receives correct availability reason.

---

# 100. TESTS — TRAITS

For every Trait:

- deterministic discovery rule,
- discovery persists account-wide,
- no RNG,
- real consumer,
- equip works,
- only 3 active per Character,
- swapping free where intended,
- different Characters maintain different Trait selections.

Test unknown Traits do not expose internal criteria through UI model.

---

# 101. TRAIT LIVENESS TESTS

Every Trait needs an end-to-end liveness test.

Same scenario:

without Trait

versus

with Trait

must produce the intended observable difference when its condition is satisfied.

No decorative Traits.

---

# 102. TESTS — VOW DISCOVERY

For each discoverable Vow:

- restriction can be satisfied without Vow equipped,
- satisfying it under valid conditions unlocks the Vow,
- unlock is account-wide,
- no reward applies before discovery/equip,
- Vow remains unlocked after respec/load.

No RNG unlocks.

---

# 103. TESTS — KEYSTONE DISCOVERY

Verify:

- intended Region/world milestone discovers Keystone,
- Keystone remains discovered,
- slot capacity is independent,
- Traits are not required,
- old save preserves unlocked Keystone.

---

# 104. TESTS — STRUCTURAL UNLOCKS

Test new owners:

- Skill slots,
- Forge,
- Vow capacity,
- Keystone capacity,
- Auto-Sell/Warren automation.

No Trait Point/node requirement should remain.

---

# 105. VFX CONTRACT TESTS

Test representative VFX on different actor sizes.

At minimum:

- Seeker,
- Magpie,
- small enemy,
- large enemy where available.

Verify:

- anchor correctness,
- relative scale,
- layer,
- offset,
- persistent state reconstruction.

---

# 106. SHIELD VFX TEST

Shield is the best acceptance case for the new VFX contract.

Verify:

- correctly surrounds Seeker,
- correctly surrounds Magpie,
- does not sit as a tiny sprite in torso center,
- scales with visual bounds,
- follows movement,
- absorb impact lands on barrier,
- break effect is positioned correctly.

---

# 107. UI FRAME QUALITY TEST

Inspect all major decorative frames.

At minimum:

- Equipped,
- Item Detail,
- Forge operation,
- Vault main surface,
- Traits inspector,
- modal frame.

Ensure no large decorative raster is visibly degraded by arbitrary upscaling.

Use nine-slice or correct-size assets.

---

# 108. GEAR SCREENSHOT FIXTURES

Create/review Gear fixtures at:

- 100%
- 125%
- 150%

Show:

- Equipped,
- several inventory items,
- selected item,
- set ladder.

Inventory should no longer look like a prototype panel between two polished surfaces.

---

# 109. TRAITS SCREENSHOT FIXTURE

Create a deterministic Traits fixture showing:

- active Character,
- 3 active Trait slots,
- multiple discovered Traits,
- multiple `???`,
- selected Trait inspector,
- no Trait Point,
- no tree.

The screen should look mystical, not like an achievement checklist.

---

# 110. PIXELLAB MCP POLICY

PixelLab is available directly.

If a required asset does not exist:

1. inspect nearby IDLExIDLE art,
2. generate it,
3. integrate it,
4. test at actual render size,
5. screenshot it,
6. regenerate if necessary.

Do not ask the user to create assets.

---

# 111. PIXELLAB ACTUAL-SIZE RULE

A generated asset is not finished because it looks good enlarged.

Test at actual game size.

This is especially important for:

- VFX,
- Trait sigils,
- UI corners,
- Inventory chrome,
- Signature Skill icons.

If detail disappears or pixels become muddy:

regenerate simpler/larger/correctly sized art.

---

# 112. REMOVE UNUSED GENERATED ASSETS

Do not leave:

- rejected VFX,
- failed frame variants,
- unused Trait glyphs,
- temporary Signature icons

inside production asset folders.

Every committed generated asset needs a live consumer.

---

# 113. DOCUMENTATION

Update authoritative GDDs/docs.

Document:

## Character

- Signature Skills are exclusive.

## Mastery

- Skill access tied to active allocation.
- Skill experience persists.

## Traits

- no tree,
- no Trait Points,
- hidden gameplay discovery,
- account-wide collection,
- 3 equipped per Character.

## Vows

- discovered by proving restrictions,
- capacity automatic progression.

## Keystones

- discovered through world progression,
- slots automatic progression.

## Structural unlocks

- correct new owner.

## VFX

- anchor/scale/offset/layer contract.

## UI Assets

- nine-slice / native-quality rules.

No stale old Trait Tree documentation should remain authoritative.

---

# 114. IMPLEMENTATION PHASES

Use logical checkpoints.

## PHASE 1 — Audit / Design

- current ownership map
- 10 Signature Skill design table
- new Trait catalogue
- Vow discovery map
- Keystone acquisition map
- structural unlock map
- VFX contract plan
- UI asset-quality audit

## PHASE 2 — Signature Skills

- domain ownership
- authored skills
- progression
- Roster
- Build
- migration/tests

## PHASE 3 — Mastery Semantics

- access calculation
- respec
- loadout repair
- UI lock states
- migration/tests

## PHASE 4 — Trait System Replacement

- remove Trait tree/points
- discovery engine
- catalogue
- 3-slot per Character loadout
- Traits UI
- migration/tests

## PHASE 5 — Vows / Keystones / Structural Unlocks

- Vow discovery
- Keystone conquest
- slot milestones
- Forge unlock
- Skill slot progression
- Auto-Sell → Warren
- stale UI cleanup

## PHASE 6 — VFX Contract

- anchors
- visual bounds
- relative scaling
- normalized offsets
- layers
- debug tooling
- migrate current VFX
- Shield validation

## PHASE 7 — Asset Quality

- nine-slice
- Equipped frame
- other degraded frames
- PixelLab generation where needed
- asset scale audit

## PHASE 8 — Gear Inventory Polish

- header
- secondary frame
- filters
- grid
- item states
- footer
- responsive scale
- PixelLab support if needed

## PHASE 9 — Validation

- save fixtures
- combat tests
- Traits tests
- Vow/Keystone tests
- VFX screenshots
- Gear screenshots
- build/performance/docs

Do not make this one giant unreviewable commit.

---

# 115. DESIGN LAWS

## LAW 1

Character Signature Skills are exclusive.

## LAW 2

Character unlock does not create account-wide Signature access.

## LAW 3

Shared Skill access follows current Mastery allocation.

## LAW 4

Skill experience is permanent even when access is lost.

## LAW 5

Traits are discovered, not bought.

## LAW 6

Traits are account-wide discoveries but Character-specific loadout choices.

## LAW 7

Exactly three Traits may be active per Character.

## LAW 8

Trait discovery conditions are deterministic but hidden from the player.

## LAW 9

Traits are not Mastery 2.

## LAW 10

Vows are discovered by first obeying the rule without its reward.

## LAW 11

Keystones come from world progression, not Traits.

## LAW 12

Structural functionality is never an exciting Trait choice.

## LAW 13

Automation belongs to Warren.

## LAW 14

VFX placement uses semantic anchors and visual bounds.

## LAW 15

VFX size should be relative to the subject when appropriate.

## LAW 16

Do not fix bad VFX assets with arbitrary giant runtime scale multipliers.

## LAW 17

Decorative raster frames must remain high quality.

## LAW 18

Use nine-slice or correct-size assets rather than stretching ornate corners.

## LAW 19

Inventory is a polished secondary surface, not a debug panel.

## LAW 20

Use PixelLab directly when necessary; do not hand asset work to the user.

---

# 116. FINAL ACCEPTANCE CRITERIA

This refactor is complete only when:

## Signature Skills

- every Character has one exclusive Signature,
- other Characters cannot equip it,
- current Character can,
- progression persists,
- Signature access is not Mastery-based.

## Mastery

- reset removes shared Skill access when unlock node disappears,
- Skill XP remains,
- invalid equipped Skills are removed safely,
- permanent Skill discovery exploit no longer exists.

## Traits

- old tree is gone,
- Trait Points are gone from current design,
- Traits unlock through hidden deterministic gameplay discoveries,
- discovered Traits are account-wide,
- each Character equips exactly 3,
- unknown Traits show `???`,
- no visible achievement checklist exists.

## Vows

- no Trait-node dependency,
- at least one tutorial Vow accessible,
- discoverable Vows use proof-before-reward rules.

## Keystones

- no Trait-node dependency,
- discovered through world progression,
- slots come from automatic progression.

## Structural systems

- Forge, Skill slots, Vows, Keystone slots, Auto-Sell have correct owners.

## VFX

- actor-relative standard exists,
- Shield looks correct across differently sized Characters,
- no tiny center-spawned generated effects remain,
- persistent states reconstruct properly.

## UI asset quality

- Equipped frame is no longer low-resolution/stretched,
- large ornate frames are nine-sliced or correctly sized,
- generated assets look correct at actual render size.

## Gear Inventory

- polished header,
- coherent secondary panel,
- consistent tabs,
- clean grid,
- readable footer,
- responsive behavior,
- no prototype visual mismatch.

## Migration

- existing progression/features preserved reasonably,
- old saves load,
- old runtime concepts do not remain merely for compatibility.

## Validation

- tests pass,
- fixtures pass,
- screenshots reviewed,
- no orphan generated assets,
- docs match runtime.

---

# 117. FINAL REPORT

Report:

## Signature Skills

1. final Signature Skill for every Character
2. Style/Kind
3. relationship with Innate
4. ownership implementation
5. progression behavior

## Mastery

6. final Skill-access rule
7. respec behavior
8. loadout repair
9. XP persistence

## Traits

10. new Trait architecture
11. final Trait catalogue
12. hidden discovery rules
13. 3-slot Character loadout
14. UI redesign
15. removed old tree systems

## Vows

16. discovery rules
17. capacity progression
18. tutorial access

## Keystones

19. acquisition mapping
20. slot progression

## Structural unlocks

21. Skill slots
22. Forge
23. Auto-Sell
24. other moved capabilities

## VFX

25. anchor system
26. visual-bounds system
27. relative scale
28. offsets/layers
29. Shield result
30. debug tooling

## UI quality

31. nine-slice changes
32. frame replacements
33. Equipped frame fix
34. Inventory polish

## PixelLab

35. generated assets
36. paths
37. actual rendered sizes
38. rejected/regenerated assets
39. unused assets removed

## Persistence

40. save migrations
41. preserved old capabilities
42. removed obsolete fields

## Validation

43. tests
44. fixtures
45. screenshots
46. build
47. remaining balance/design debt

---

# 118. MOST IMPORTANT FINAL INSTRUCTION

The goal is not to add more progression systems.

The goal is to make every existing system mean something different.

The final player mental model should be:

```text id="w3plmc"
CHARACTER
Who am I?

SIGNATURE SKILL
What can only I do?

MASTERY
What shared techniques am I investing into right now?

SKILL PROGRESSION
What techniques have I become experienced with?

TRAITS
What strange characteristics have I discovered through living this game?

VOWS
Which rules did I prove I could obey, and now willingly swear?

KEYSTONES
What doctrines did the world reveal as I conquered it?

GEAR
How do I tune the build?

WARREN
What does my organization automate while I am away?
```

No two systems should answer the same question.

Traits must feel like mysterious consequences of experience.

Vows must feel earned before they are sworn.

Keystones must make world progression expand the build vocabulary.

Signature Skills must make switching Characters genuinely change what techniques are available.

Mastery must remain a real allocation decision rather than a free permanent-unlock exploit.

VFX must look authored for the subject they belong to.

And every visible production asset must meet the visual quality bar of the rest of IDLExIDLE.