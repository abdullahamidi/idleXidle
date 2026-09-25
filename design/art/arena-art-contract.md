# Arena Art Contract — the 2026-08-22 consistency pass

> **Status**: AUTHORITATIVE for every figure and effect drawn on the HUNT stage (champion, enemies,
> bosses, combat VFX). Supersedes the geometry parts of `production/qa/art-consistency-audit.md`
> (the audit that asked for this pass) and the arena rows of `asset-integration-spec.md`.
> Generated through the PixelLab MCP tools; the post-processing lives in `tools/asset-pipeline/v2/`.

## 0. Why one contract

The previous art mixed three cameras (front-on painterly roster, 3/4 pixel enemies, front-on
symmetrical bosses), three scales, and two facings — the champion's clips wound up away from the
enemies and had to be mirrored at draw time. Playtest (2026-08-19, item 8): "perspektif, animasyon
ve boyut tutarsızlıkları var." Every rule below removes one of those degrees of freedom.

## 1. Camera and facing (the non-negotiables)

| Rule | Value |
|---|---|
| Camera | PixelLab `view="side"` (eye level), **three-quarter toward the viewer** |
| Champion | generated **SOUTH-EAST** rotation → **faces RIGHT**, natively, no mirroring |
| Enemies + bosses | generated **SOUTH-WEST** rotation → **face LEFT**, natively |
| Image-route creatures | prompted "three-quarter side view facing left"; verified by eye on the sheet |
| Code | `SoloExpeditionScreen.ArtFacesLeft = false` — the champion is drawn un-flipped |

A frame that faces the wrong way is a failed asset. The sheet command exists so a human looks at
every strip before it is filed; nothing is filed unseen.

## 2. Size chart

On-screen height is decided by the renderer's boxes (it top-crops the headroom and fills the box
height, bottom-anchored), so the chart is in BOXES plus the pixel density that keeps the whole
stage reading as one drawing:

| Figure | Box / target | Source size (PixelLab) | Frame in strip | Density |
|---|---|---|---|---|
| Champion | 430 px (ChampBox) | character size **128** | 512 | ~3.4 px/px |
| Normal enemy | 440 px × archetype scale (0.58 swarm … 1.12 bruiser) | character **128** / image **192–256** | 512 | ~3.4 |
| Boss | 540 px body | character **160** | 512 | ~3.4 |
| Effect | a RATIO of the figure — see below | pixen **256** | 512 | — |

Every strip is **8 square 512-px frames in one row** (`*_strip8_512.png`). Character and enemy strips
are sliced by `width / height`; **effect strips declare their frame count** in the VFX profile table
(`src/IdleXIdle.Game/Vfx/VfxProfiles.cs`), so a regenerated effect at another aspect fails loudly
instead of being sliced wrong in silence. Bosses moved from `_strip8_1024` to the same 512 contract.

### Effects are sized by RATIO, and the art must fill its frame

**Updated 2026-09-03, with the VFX placement contract.** "Played at scale 1–3" is dead: an effect no
longer carries a display multiplier at all. It carries a fraction of its SUBJECT's visible size, and
the renderer derives the frame from that and from how much of the frame the art actually fills
(`src/IdleXIdle.Game/Vfx/VfxContract.cs`). The bands: a shield barrier 1.10–1.20× the hunter's
visible height, an ordinary impact 0.25–0.40× the target's, an execute 0.50–0.70×, a body aura
1.0–1.2×.

Two consequences for anyone GENERATING an effect:

1. **Padding is not free.** The renderer draws the whole frame, so a strip whose art fills half its
   frame needs a frame twice as large to show the same picture — and that size is reported as a ratio
   against the strip's authored 512. Above **1.25×** the renderer refuses to magnify any further and
   the effect is flagged; below **0.75×** the strip is simply bigger than it is ever drawn. The worked
   case: `fx_shield` used to fill **0.492** of its frame, so the barrier could not reach the 1.10–1.20×
   band at any honest size — the renderer clamped it and drew a 315-px dome across a 412-px hunter. It
   was **regenerated 2026-09-04** and now fills **1.000**, so the same authored 1.15 draws 474 px at
   **0.93×** native. `fx_shield_break` fills **0.922**. Measure any new strip with
   `tools/asset-pipeline/fx_bounds.py`.
2. **Author the art at the SHAPE the effect is.** A ground ring wants a wide, short strip; every trap
   strip in the tree is a near-square burst, so the ring anchors as a burst standing on the floor
   rather than as an ellipse lying on it. The same rule cost the shield a whole pass: a barrier is a
   thing that ENCLOSES a standing figure, and the strip drawn for it was a squat hemisphere sitting in
   the middle of a frame that was 46 % empty below it. No placement number can turn a half-dome into a
   shell. When an effect must surround its subject, the art must be a closed shape filling its frame.

## 3. Clips

| Owner | Clips | Notes |
|---|---|---|
| Champion (each of the ten) | `idle`, `attack`, `death`, and one clip PER FORM: `strike`, `projectile`, `mark`, `trap`, `transformation` | idle loops, the rest play once; a Form's clip plays on that Form's Skill event, `attack` on the basic swing, `death` on the fall |
| Enemy | `idle`, `attack`, `death` | the stone sentinel's `slam` is gone — every enemy's clip is `attack`; `death` (2026-08-23) plays from the kill, holds, fades — the plume rises over the body half a second later |
| Boss | `idle`, `attack`, `death` | the boss's fall holds for the whole wave break |
| Effect | one 8-frame strip | authored white / pale so the Source tint at play time carries the colour |

Clip prompts: idle = "standing idle, feet planted and never moving, only breathing … returns to the
start by the last frame so it loops"; attack = the figure's own verb toward the front, returning
to the start by the last frame; cast = raise a hand, gather glow, release forward; death = stagger,
knees, collapse forward, fully fallen by the last frame.

### 3.1 One clip per Form (2026-08-28)

Every Form used to share two clips — `attack` for a Strike and `cast` for everything else — so ten
characters threw the same two shapes for five different verbs, and the Quiver's Projectile was the
Anvil's Transformation with a different name. Each character now owns a clip per Form, and the verb is
*theirs*: the Seeker throws a knife where the Anvil slings an iron weight; the Seeker bends to set a
snare where the Anvil drives a fist into the ground.

`Character.StripKeys(clip)` falls back — a Form's own clip, then the generic it stands in for
(`strike`→`attack`, the rest→`cast`), then the idle at the draw site. **So the code ships ahead of the
art and nothing ever blanks.** Fifty strips do not arrive at once; a character with three of five
filed plays three of its own and two of the old pair, and looks deliberate either way.

`trap` has no generic, and that is the point: a Trap answers the enemy's bite rather than the beat, so
it never had a champion animation at all. The screen commits it opportunistically
(`WaveReplay.LastTrapBefore`) and only when no beat action is due — a Trap consumes no beat and must
never steal the clip a real action was about to use.

### 3.2 Two prompt rules the first batch paid for

Both were learned by looking at the sheet, which is what the sheet is for.

* **"the same figure throughout, never changing outfit or shape"** — the Seeker's first
  `transformation` read the word literally and morphed the character into a featureless robe by frame
  six. Every action prompt now opens with that clause.
* **Bend, do not kneel.** A full kneel changes silhouette height by more than the gate's 30 % loose
  ceiling and is rejected — the Seeker's first `trap` failed at 33 %. "Bends forward at the waist and
  reaches one hand to the ground" reads as the same act and passes.
* **Never ask an effect to end at nothing.** "Fades away to almost nothing by the last frame" and
  "gone off the right side by the last frame" both produced a literally EMPTY final frame, which the
  gate rejects outright ("an input frame is empty"). The renderer fades an effect itself; the strip
  only has to carry the motion. Say instead: "keep a clear visible remnant in every frame including
  the last one, never empty."

### 3.3 One effect per Form PER CHARACTER (2026-08-28)

The effect used to be one strip per Form, tinted by the casting skill's Source — so a Strike was the
same crescent whoever swung it. Now that each character throws its own shape, a shared effect lands on
a motion it was not drawn for (designer, 2026-08-28: *"hepsinin efektinin farklı olması daha özel
hissettirir"*). Key: `fx_<char>_<form>_strip8_512`, resolved by `SoloExpeditionScreen.FxFor` with the
shared `fx_<form>` as the fallback — the same shape as the clip fallback, and for the same reason.

The Source TINT still applies on top, so the shape says WHO cast it and the colour says what it is made
of. Effects are therefore still authored white/pale; a coloured effect multiplies badly against the
tint, which is why the Seeker's first `trap` (generated in gold) was re-rolled.

**Timing (the contact frame).** Every eight-frame action clip is authored so the blow CONNECTS at
frame 5 of 8 (wind-up 0-2, commit 3-4, touch 5, recover 6-7) and the renderer plays it in two halves
(`SoloExpeditionScreen.ContactFraction`): the anticipation clock runs frames 0-5 up to the moment the
sim credits the hit, the hit starts the follow-through, and frames 6-7 play after it. This holds for
the champion's auto-swing (0.625 s wind-up + 0.375 s follow of the 1.2 s cadence), the champion's
skill beat (cast clip; the attack clip for a Strike — anticipated off `WaveReplay.NextSkillEventAfter`),
and every enemy / boss bite (900 ms wind-up, ~0.3 s follow). Effects spawn AT the hit, so the slash
and the blade, the burst and the open hand, land on the same frame. Verified with the filmstrip rig
(`tools/asset-pipeline/capture_seq.sh`, `v2/filmstrip.py`).

### 3.4 The gate cannot see whether a clip reads (2026-08-28)

`rhart.gate` measures geometry: frame count, square frames, a shared baseline, silhouette drift,
emptiness, stray blobs. **It has no opinion about whether anything happened.** The Quiver's first five
and the Thornwall's first five all returned PASS and all five of each were unusable — the figure stood
still and the prop rocked. A clip is not done when the gate passes it; it is done when it has been
looked at on `rhart.py sheet`.

A motion floor was measured and rejected rather than added: across the 67 strips filed so far, the
worst frame-0-to-frame-N silhouette difference put `quiver_strike` (which does nothing legible) at 0.46
and `chorus_projectile` (which reads perfectly) at 0.25. The bad clips are not the still ones — they
are the ones whose motion does not spell the verb, and no pixel metric separates those. The review
sheet is the gate for that, and a human (or the model) has to run their eye down it.

### 3.5 Write the verb through the prop the character already holds

Both failed batches failed the same way and for the same reason. The Quiver holds a longbow in both
hands; the Thornwall carries a kite shield that covers half its body. Asked for a generic action —
"swings the right fist", "points two fingers", "bends and sets a snare" — v3 preserved the reference
pose, rotated the prop a few degrees, and called it an animation. It is not being lazy: the prop is the
strongest signal in the start frame, and a prompt that ignores the prop is a prompt fighting it.

So the verb has to be performed BY the object:

| Character | Wrong (first pass) | Right (second pass) |
|---|---|---|
| Quiver (longbow) | "swings the right fist forward" | "swings the upper limb of the bow forward like a club" |
| Quiver (longbow) | "throws a dart at the front-right" | "nocks, hauls the string past the cheek, looses — the arrow flies clear of the bow" |
| Thornwall (kite shield) | "strikes forward with the arm" | "rams the whole shield forward, the barbed face leading" |
| Thornwall (kite shield) | "bends and sets a snare" | "drives the bottom point of the shield into the ground so it stands planted" |

Two further clauses earn their place in every action prompt, both learned here: name the frame the blow
lands on ("the impact landing at the fifth frame") so the contact frame matches §3.3, and say what is
STILL VISIBLE in the last frame, which is the same rule as "never end at nothing" applied to a prop
rather than an effect.

### 3.6 An effect strip does not travel — the renderer moves it (2026-08-28)

An attempt to pin effect motion by handing `animate_image` a derived LAST frame (interpolation
instead of an open-ended prompt) was built, tested and **abandoned**. Two things killed it, and both
are worth writing down so nobody rebuilds it:

* **The endpoint frame cannot get there.** `animate_image` takes the frame as base64 or a URL, and a
  derived frame is a local file, so base64 is the only route. Every derived PNG was refused with
  "broken data stream when reading image file" — at 1.6 KB and at 2.2 KB, with the byte count in the
  error EXACTLY matching the file on disk, and with the same bytes round-tripping through base64 and
  Pillow locally without complaint. The receiver, not the size, is the problem.
* **It was solving a problem the renderer does not have.** `SoloExpeditionScreen` already plays the
  projectile effect at the MIDPOINT between the champion and the target, and every other Form's
  effect on the body it belongs to. The strip never has to cross the gap — the draw site places it.

So every effect is authored as an **in-place** motion: spin, pulse, bloom outward, snap shut, rise.
Those are exactly the motions an open-ended `animate_image` prompt is good at, which is why the
Seeker's five read well. The recipe is one `create_image_pixen` shape (1 generation) plus one
`animate_image` pass (5 generations at 192x192x8), filed by `fxclips.py`.

**Prompting the shape.** pixen takes similes literally and fills dark discs when asked for a glow:
"like a beetle carapace" returned a beetle, "a white glowing rune brand" returned a dark disc with a
rune on it, "a ring of charms" returned a christmas tree, and "a bow" returned a ribbon bow.
Describe the SHAPE and nothing else, say "solid white", say what must be EMPTY, and end with "on
empty transparent space with nothing behind it". Roughly two in five come back usable on the first
try, and a re-roll is 1 generation.

**And the effect must not fill the frame.** Asked for "a tangle of barbed wire lying on the ground",
pixen drew the ground too — rocks, rubble, edge to edge — and the strip came out as a textured square
that would sit on the arena like a decal of a photograph. An effect is ONE object with air around it.
Every shape prompt therefore ends with "wide empty space around it" and names the scenery it must not
draw. This matters more than it looks: the strip is composited over the fight at 3x scale, so
whatever touches the frame edge touches half the arena.

**Colour is fixed by the pipeline, not by re-rolling.** `rhart.py whiten` — luminance, lifted so the
darkest surviving pixel is 96, written back over the same alpha — runs on every strip `fxclips.py`
files. The generator returns gold traps and blue tick bars whatever the prompt says, and re-rolling
for colour spends a generation on something a transform does for free and for certain.

**Alpha is honest, and the renderer adds it once (ADR-009, 2026-09-23).** A strip's alpha means what it
says: the fading post-pass (whiten → glow → soften → feather) gives it partial alpha, `AssetLibrary`
premultiplies at load, and the VFX pass blends with `VfxBlend.PremultipliedAdditive` (One + One), so a
texel at alpha 0.5 adds half its light. Never bake a correction (such as √alpha) into a strip to make it
read brighter. Judge brightness in the arena, at combat size, where the effect is drawn at about 0.3×
its native frame.

**An impact is ONE event (2026-09-23).** Impact → expansion → breakup → fade, with one energy peak and
the light trending down after it; shards may keep flying outward, but nothing re-forms at the centre.
Open-ended `animate_image` drifts back toward a cycle. Three of four clips of the Seeker strike
collapsed into a compact centre and burst again, and in the arena that reads as a SECOND HIT. So the action
prompt names the one direction of travel and forbids the failure ("one single impact that happens once
… the motion only goes outward, nothing moves back toward the middle, no second burst, no new shapes
appear, it does not repeat or restart"). The reusable rules, with what each one cost, live in
`tools/asset-pipeline/v2/spec.json` → `effects.prompt_rules`. `soften` blurs by about one SOURCE pixel
(`fxclips.py`); `tools/check_fx_edges.py` fails a strip that is unreadable at combat size (LIVE), and
`tools/fx_energy.py` draws the per-frame curve that shows a second hit.

**Owner decisions after the archetype cohort (2026-09-23).** The contracts are in
`spec.json` → `effects.archetypes`. In short:
- **Projectiles have two motion layers.** The renderer moves the strip to its target: one pass of the 8
  frames over the 1.0 s flight, of which frames 0–5 are seen. The strip itself must also visibly change
  (a blade tumbles or glints, an orb pulses). A picture that only translates is not approved.
- **Impact assembly.** An open-ended IMPACT whose last generated frame comes back empty is assembled
  from the input frame + generated 1–7 (`fxclips.py --impact-from-input`, refused for anything held).
- **PRESS** is an abstract pressure field: a rune or line-art force symbol like the other fields. Never a
  literal weight, box or machine.
- **A trap's activation is a physical event**: appear, then a tighten or tension cue, then the held state.
  After that it may stay still.
- **The 512 frame stays.** On the widest row the budget clamp already draws a trap slightly smaller;
  anything more is a world-space trap problem, not a new asset format.

### 3.7 A projectile is COMPOSED, not played (ADR-010, 2026-09-23 — Seeker pilot, awaiting approval)

Two PixelLab pilots tried to put a projectile's motion inside its strip. One came back static: a PNG
translated across the arena. The other tumbled through about 110° and, because a strip is sized by the
box around all its frames, drew the knife at 40 % of its old size. A thrown thing's motion is mostly its
TRAIL, and only the runtime knows where the projectile was. So a projectile listed in `ProjectileLooks`
is drawn by `ProjectileVisual`, from parts:

| Layer | What it is | Seeker knife |
|---|---|---|
| **Head** (primary) | one white frame, drawn along the direction of travel | `fxp_seeker_knife_head`, 0.68 × the hunter's height, ±6° wobble |
| **Core trail** (secondary) | a short, narrow, hot streak starting at the head's rear edge | 0.09 s of the real path, 0.32 × the head's thickness, 50 % toward white |
| **Wake** (tertiary) | a longer, softer, dimmer ribbon with a small lateral wave | 0.30 s, 0.9 × thickness, tapering and dissolving with age |
| **Glint** (tertiary) | one point of light that crosses the blade once | from 0.30 to 0.46 of the flight, rear to tip |
| **Sparks** (tertiary) | at most 3, shed from the rear quarter, falling back | 0.22 s each |
| **Impact** | a hot contact flash, then slim shards thrown mostly FORWARD | 0.09 s flash; 8 shards, 75 % inside ±32° of the incoming direction, gone in 0.26 s |

- **The body has one size.** Head length = `HeadLength` × the caster's height, measured on the head's
  own texture. The strip's union box, the trail and the wobble never enter it.
- **The flight does not change.** Same from and to, same ease-out, same 1.0 s clock. The head strikes
  when its centre is `ContactReach` × its length from the target (about 0.67 of the flight for the knife,
  where the old strip began to fade), and the trail then tapers and dims away in 0.12 s. It never stands
  as a block.
- **The colour is the Source**, on every layer. The Seeker's red/pink is Body's glow.
- **PixelLab's role is parts.** It draws the head and the glint, through the usual post-pass. Pure
  gradients (the streak, the spark, the flash, the shard) are written by
  `tools/asset-pipeline/v2/fxparts.py`. Do not ask PixelLab for projectile motion, rotation or a baked
  trail.
- **A new projectile is a new `ProjectileLook`**, not a new code path. An orb's pulse or a shard's small
  tumble is an optional field on the look. Nothing in the shared path spins the head.
- Projectiles that are not yet listed still follow the two-layer strip rule above.

### 3.8 An important action is PERFORMED from key poses (ADR-011, Accepted 2026-09-25 — the Seeker's SPRAY is the projectile / travel reference; his HARD HANDS is the melee / direct-contact reference, both accepted)

The rule is one coherent action: intention, preparation, release, motion, contact, consequence, recovery. The player should not be noticing the separate systems (clip, effect, number, sound) that make it.

- **The contact is the fight's beat.** A projectile's clip starts early enough that it releases one flight (about 250 ms for a thrown blade) before the beat. The blade lands on the beat, together with the fight's health, flash, number, death and contact sound.
- **Clips have authored timing.** A `<strip>.clip.json` beside the strip gives:
  - each frame's duration;
  - the elastic frames (the wait and the settle, which absorb TEMPO);
  - markers: `anticipation`, `commit`, `release`, `recovery`, `settle`;
  - hand sockets.

  Its last frame is the idle's first pose, so the idle restarts cleanly. A strip without a timing file keeps the old uniform "contact on frame 5 of 8".
- **Key poses, not "eight frames of an attack".**
  - Pose the action as 18-joint skeletons.
  - Generate with `animate_with_skeleton_v3` from ONE reference frame of the champion, so his identity holds.
  - Generate on an opaque ground: the no-background mode keyed the Seeker's dark cloak away as background.
  - Key it with `keyposes.py`, which also places the frames with the champion's own idle transform (pixel-exact, so the switch from idle and back never pops).
  - Test every pose as a flat silhouette at HUNT size.
  - Record every job, accepted or rejected, with the reason.
- **Props are the same object in the hand and in the air.**
  - A thrown prop is drawn at a hand socket until the release. The projectile is the same texture at the same scale, so the ratio is exactly 1:1.
  - The Seeker's THROWING KNIFE (`assets/art/Props/prop_seeker_throwing_knife.png`, drawn by `seeker_knife.py`) is a kunai-style blade, 30 source px long: about 99 px on screen, 0.24 of his height.
  - It is not his MELEE blade (the curved knife of his strike and attack clips), and his idle shows neither. SPRAY draws the bundle from his belt on the ready frame.
- **Colour roles.**
  - Material stays material: steel is drawn untinted.
  - The Source is light: edge, glint, trail, sparks, contact.
  - The champion's motif is the object's shape.
- **The hand tells the throw** (polish pass, 2026-09-24).
  - The release frame shows an OPEN, flicked hand pointing along the fan, and the follow-through keeps it open. A fist on the release frame reads as a punch.
  - The follow-through outlasts the flight (release frame + follow-through = `TravelMs` + one or two frames). The hand still reaches toward the targets on the frame they are hit, and the recovery starts just after. If the recovery starts on the contact frame, the arm drops at the hit.
  - A hand may be corrected with a PixelLab edit (`edit_image_pixen` of the accepted key pose). Take ONLY the hand's box from it: the edits also redraw part of the body, paint props and redraw the other arm. Match the champion's own materials: the Seeker is gloved in every frame, so the whole edited hand takes his glove ramp. An edit that draws bare skin changes his costume for a quarter-second.
  - After two failed edits, correct the hand by hand. Do not keep generating.
- **An action ends on an EXIT POSE** (the action handoff, ADR-011, 2026-09-25). Its last frame is where the next action starts from, so it must be compatible with idle and with any action's first pose: standing, weapon away or at rest.
  - When the next action needs the figure early, the outgoing recovery is played faster to ARRIVE at that pose, never cut mid-pose. So a crouched or lunging last frame pops however it is timed.
  - HARD HANDS (`seeker_hard_hands`) was rebuilt (2026-09-25) to end on the idle's own first frame, reached through a retreat pose (a guarded backstep): its follow-through is the lowest pose and the rise back is two even steps, never "crouch → instant stand". The basic swing still ends in a half-rise with its blade out: its blade disappears at the join (a plain clip, not yet rebuilt).
  - **A signature action has its OWN strip** (`char_<id>_<action>`, named by the recipe's clip), never the Form's shared one. The Seeker's Strike Form strip (`seeker_strike`, the knife swing) is BLOW's; HARD HANDS' hammer-fist is `seeker_hard_hands`. A signature strip written over a Form strip made every other skill of that Form play the signature's poses without its recipe: a leap in place, hundreds of pixels from the creature.
  - An authored clip is DRAWN at the idle's scale and on the idle's ground line (`UiKit.ResolveFrame` `placeAs`), whatever its own extremes: a raised fist above the idle's head or a stance wider than its feet must not rescale or re-ground the whole action. Measured on its own extremes, HARD HANDS drew 0.8 % larger and 3 px lower than the idle it starts and ends on.
  - No bespoke `a_to_b` transition clips and no whole-sprite crossfades: the exit pose + recovery compression + the next action's authored first pose are the bridge.
- **A melee blow is CARRIED to its target** (HARD HANDS, 2026-09-25). The champion stands hundreds of pixels from the creatures; an arm is never stretched and the body is never teleported.
  - The lunge is PRESENTATION ROOT MOTION (`MeleePerformance.RootMotion`): a small pull-back through the anticipation, the whole distance during the commit, accelerating into the blow (the fastest spacing is at contact), a small overshoot through the follow-through. The fight's positions never move; the body, its effects and its sockets all follow the drawn box.
  - **The way home is a RETREAT, not a spring** (second pass, 2026-09-25). Explosive approach, controlled withdrawal: the follow-through stays planted, then the body leaves slowly (the blow spent its momentum), is fastest mid-way and settles into home, ~250 ms at normal TEMPO, in a retreat POSE (guard up, rear leg taking the weight back, front foot pushing off) with a low bound (~7 px, `RootLift`, only through the retreat's middle; landed before the exit pose). The first cut carried the recovery pose home in 136 ms with a 25 px hop: it read as a reset, not a body recovering. A 400 px move in a planted pose is a slide; a large parabola is a spring.
  - The reach is measured at the clip's start from the fist's authored socket on the contact frame to the target's contact point, so the fist lands where the creature actually stands.
  - The way home has a minimum length (110 ms): a handoff that squeezes the recovery, or a yield after the contact at the fastest TEMPO, carries it on under the next action's wind-up. It is never a one-frame snap.
  - Speed lines trail the body on the commit only. The way home is a controlled return, not a second attack.
  - The impact follows the force and starts AT the fist: a white-hot flash compressed across the blow, a soft shock ring squashed along it (gone in ~110 ms), light chips thrown mostly along the force with a share kicked back, dark material slivers, a few sparks. On shadow creatures over a dark floor, debris drawn only as dark matter is invisible: draw it as light, as SPRAY does.
  - Every effect that lands ON the champion's body (a bite, a heal, a shield) is pinned to him and follows the lunge.
- **Key poses from a light ground are repaired, not regenerated** (HARD HANDS). `animate_with_skeleton_v3` ignored "pure black background" twice and drew a light ground in the reference's own glint colour, which it also used to FILL his face and sleeves. `keyposes.py` keys the ground from the border, transplants the reference's own face onto the blank face region nearest the pose's nose joint, gives other blanks his sleeve grey, and turns every light edge pixel into his near-black outline. Accepted sources are cached in `tools/asset-pipeline/v2/keypose_sources/`: PixelLab's links expire.
  - **A garment keeps its colours through the action.** The generator painted HARD HANDS' flared cloak in the HOOD's grey-green highlights on the contact and follow-through; the idle's cloak is dark purple with a grey-green rim. `keyposes.py` (`"cloak"` boxes, `recolour_cloak`) remaps the cloak colours inside the flare, in order of brightness, to the idle cloak's own distribution, and takes out the pale light-ground fringe along its edge: the silhouette, the pose, the shading's direction and the outline are the generator's. Regenerating a pose for a colour is not worth a generation.
- **A smear is an accent, not a path.** It shows the last part of the hand's travel only, starts on the release frame (never before it, because the hand is not there yet), and never crosses the head or face. A full-length streak from the coil read as a beam from the eye.
- **Sound is one phrase** (`design/audio/seeker-spray-audio-brief.md`).
  - The release is the action's own cue. The flight is silent of the action's sounds.
  - The contact is ONE cue, with at most two quiet ticks at the outermost targets.
  - Everything else is ducked from the release until 160 ms after the contact.
  - A cue exists only when it has passed a listening test. A measured file is a candidate.
- **Impact shape follows force.**
  - A blade's contact is a hot slash carried through the target along the incoming line, with slivers mostly forward. It is never a round burst.
  - A performed hit replaces the generic hit puff and thud, which describe the same blow.
  - The enemy's flash and number stay, scaled by the recipe so a fan across the whole pack does not turn it solid white.
  - A thrown object has ARRIVED on its contact frame, so its flash peaks there and is short (SPRAY: 0.38, 130 ms, no rise). A swing's flash keeps its swell.
- **Review gate.** A performed action is approved only when all of these read at combat size:
  - true speed and slow motion;
  - a frame sheet with its markers;
  - silhouettes;
  - effects off;
  - champion hidden;
  - the socket overlay;
  - several consecutive casts;
  - the widest real case (five targets), and a fast TEMPO build;
  - the sound, rendered from the trace (`film_audio.py`) and then listened to.

  Technical pass is not artistic pass.

### 3.9 A reaction is a LAYER, never a clip (ADR-011, JAWS base slice 2026-09-25 — awaiting the owner's review)

A REACTION (JAWS) takes no beat: the enemy's bite is its cause. It is drawn on its own layer, over both figures, and
never takes the champion's figure: no cast, no lunge, no post-bite "lay a trap" clip (that clip is REPAY's cast).

- **The object, in two states from ONE source.** JAWS is an iron jaw trap (the skill's name and its icon): OPEN
  (`prop_seeker_jaws_open`, PixelLab pixflux `2126658f`) and SHUT (`prop_seeker_jaws_shut`, a PixelLab EDIT of that
  very image, `b3c7ee25`), so the hinge, the rings and the framing are the same pixels' descendants. The shut ring's
  teeth carry an emissive mask (`prop_seeker_jaws_shut_edge`). Built and post-passed by
  `tools/asset-pipeline/v2/seeker_jaws.py` (the white fill made transparent, the palette drift snapped back, the rust
  teeth recoloured to steel, the iron lifted one shade so it reads on the floor). A rejected candidate is recorded
  there with its reason.
- **PixelLab draws no motion.** The rise, the snap, the overshoot, the chain's tension and shiver, the recoil, the
  release, the sparks and the fade are the runtime's (`ReactionPerformance`). Never ask for a trap animation or a chain.
- **The chain is runtime geometry.** One drawn link in two cells (face-on, edge-on: `prop_seeker_chain_link`), placed
  along a curve between two LIVE anchors: the Seeker's BELT (a share of his drawn body, so it rides a leap) and the
  jaws' eye. Never one long chain image tied to one arena distance. At most 64 links; a longer chain draws bigger
  links, never gaps.
- **Size and place.** The jaws are ~0.48 of the caught creature's height (56 to 130 px) and close on its FRONT-LOWER
  silhouette facing the Seeker, read off the frame it bites in (never a fixed "foreleg": the anatomy differs).
- **Material vs Source**, as SPRAY: the iron is untinted; the Source is light only (the teeth's glint, a small
  flattened snap flash, sparks, a restrained tension accent). Never tint the object.
- **Review gate:** true speed and slow motion, effects only, champion only, during SPRAY, during HARD HANDS, during a
  basic swing, sixteen seconds of repeats, the dock's sweep, a killing answer, a fatal bite, the REPAY isolation, the
  anchor overlay (`RH_SHOT_SOCKETS`), and the sound heard.

## 4. Naming (unchanged where the code already asks)

| Asset | Path | Key |
|---|---|---|
| Champion clip | `assets/art/Animations/Roster/<id>_<clip>/` | `char_<id>_<clip>_strip8_512` |
| Champion still | `assets/art/Characters/Roster/` | `char_<id>_base` (the SE rotation, 512²) |
| Enemy clip | `assets/art/Animations/Enemies/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Enemy still | `assets/art/Enemies/enemies/<key>/` | `<key>_idle_01`, `<key>_attack_01` (frame 0) |
| Boss clip | `assets/art/Animations/Bosses/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Effect | `assets/art/VFX/<name>/` | `fx_<name>_strip8_512` |

Enemy keys (2026-09-16: one per REGION and ARCHETYPE, `<region slug>_<archetype>`; the table is
`src/IdleXIdle.Game/EnemyPresentation.cs`, the recipe `tools/asset-pipeline/v2/spec.json` `enemy_matrix`):

| Region | Swarm | Caster | Armoured | Bruiser |
|---|---|---|---|---|
| Verdant Hollow (`verdant_`) | BRIAR MITE | BOG WEAVER | BARK WARDEN | THORN OGRE |
| Cinderworks (`cinder_`) | CINDER GNAT | FURNACE PRIEST | BOILER KNIGHT | SLAG HULK |
| Umbral Reach (`umbral_`) | GLOOM WHELP | HOLLOW SEER | NIGHT CARAPACE | DUSK APE |
| Marrow Wastes (`marrow_`) | MARROW TICK | CARRION SHAMAN | SHELL GHOUL | FLAYED BRUTE |
| The Still Archive (`archive_`) | GLYPH MOTE | LENS ARCHIVIST | RUNE SENTRY | TABLET GOLEM |
| The Pale Choir (`choir_`) | CHOIR WISP | PALE CANTOR | HALO GUARD | BELL GIANT |

Each role reads from its SILHOUETTE first (swarm small, low and busy; caster tall, thin or
hovering; armoured closed and plated with its weight low; bruiser broad and dominant), and
`HuntScreen.ArchetypeScale` sizes the row as supporting language only. A creature's SOURCE is not in
its body: the enemy strip shows the Sources the wave carries and the inspector the hovered
creature's own. The six 2026-08-22 bodies (bonecrawler, soul_leech, wisp, stone_sentinel, shadeling,
rift_guardian) are retired. Swarm bodies come from the image route (`create_image_pixen` +
`animate_image`, 192²), the other three from the character route (128², south-west). Every clip is
gated by `matrixclips.py`: the rhart gate per clip, and a body-size jump of at most 12 % between a
clip and its idle (`clip_pop.py`), since the renderer scales each strip by its own headroom.

Boss keys (one per region): thorn_regent, forge_colossus, void_reaper, crystal_lich, lumen_angel,
spirit_matron.

All of these load on FIRST USE (`AssetLibrary.IsDeferred`), warmed from Update by
`HuntScreen.WarmArt` for the champion, the region's family and its boss. A new family costs nothing
at boot; it costs 8 MB per strip while it is on screen.

## 5. Effects (the skill layer)

Per Form, played on the enemy row unless noted, tinted by the casting skill's **Source** colour:

| Key | What | Where |
|---|---|---|
| `fx_strike` | a crescent slash arc sweeping across | enemy |
| `fx_projectile` | a bolt streaking left→right into a small burst | enemy |
| `fx_aura` | an expanding ring pulse | champion |
| `fx_trap` | a ground burst of shards erupting upward | enemy |
| `fx_mark` | a sigil that flashes and locks | over the enemy's head |
| `fx_transformation` | an upward surge of light | champion |
| `fx_press` | the weight that sits on the front enemy — PRESS's field | champion (held) |
| `fx_weep` | a falling column, left by a kill — WEEP's rain | over the enemy's head |
| `fx_wilt` | the withering that drains the wave — WILT's field | champion (held) |

The last three were on disk and loaded for months and DREW NOTHING: their keys had no entry in
`AssetLibrary.Aliases`, the resolver returned null, and `VfxPlayer` skips a missing texture in
silence. PRESS and WILT are two of the four Field skills, and a Field's art is what the champion's
held field wears — so a build running either had no field effect at all. Any new effect key belongs
in that alias table and in `VfxProfiles`, which is what `tools/check_asset_keys.py` now reads.

UI flourishes generated the same way: `fx_bind_chain` — a ring of heavy gold links with inward
spikes, drawn wide and contracting — plays over a skill's Source medallion on the BUILD screen when a
Vow is bound to it. It is an asset rather than drawn primitives on purpose (playtest 2026-08-28:
"kendin bir kutucuk veya buton oluşturup görsel olarak onu kullanıyorsun").

Combat beats: `fx_hit` (spark), `fx_weakhit` (puff), `fx_crit` (starburst), `fx_death` (ash plume),
`fx_heal` (rising motes), `fx_shield` (a closed ring of light, held around the hunter for as long as
the shield stands), `fx_shield_break` (that shell bursting into shards).

`fx_levelup` (a column of light) is generated, on disk, and **played by nothing**. Its alias was
removed 2026-09-03 so it stops claiming a consumer it does not have. The level-up moment it was made
for is chrome (a skill level, a region conquered) rather than an arena beat, and the effect layer
only knows how to place things against arena figures — so wiring it needs a second placement context,
which is a decision for the desk, not a number to turn. Until then it is a flagged orphan, not art.

## 6. Generation recipe (what the agents ran)

* Humanoids — `create_character(mode="v3", view="side", size=128|160, outline="single color black
  outline")`, then `animate_character(mode="v3", directions=[SE|SW], frame_count=8,
  keep_first_frame=false, action_description=…)`. Frame URLs:
  `…/animations/<animId>/<dir>/<0..7>.png` (read from `get_character`).
* Non-humanoid creatures and every effect — `create_image_pixen(no_background=true, view="side",
  direction="west")` for frame 0, then `animate_image(first_frame_url=…, frame_count=8)`; frames
  `download?index=1..8` (index 0 is the input).
* Per-Form champion clips — `tools/asset-pipeline/v2/skillclips.py <char> <clip>=<animId> …`, which
  wraps clip.py so a batch of five is one command with one report, and files each strip under the key
  `Character.StripKey(clip)` asks for. The character UUIDs live in that script so a re-run months from
  now does not have to find them again.
* Assembly — `tools/asset-pipeline/v2/clip.py` (fetch → union-bbox strip → gate → sheet). The
  union bounding box across frames keeps the motion AND keeps the feet on one row, which is what
  `UiKit.AnimSprite` assumes when it measures a single bottom pad for the whole clip.
* A HELD effect needs a LOOP, and the generator does not give you one by asking. "Pulses" and
  "ripples once" both came back with a frame that fades to nothing — measured, frame 5 of 8 was empty —
  which on a held effect is a barrier that blinks off once a second. Two things fixed it: pin the
  ending (`animate_image(first_frame_url=X, last_frame_url=X, …)`, so the clip interpolates back to
  where it started) and describe motion that CANNOT vanish ("the whole ring rotates slowly clockwise,
  staying complete and equally bright in every frame"). Check it with a per-frame fill measurement
  before filing, not by watching it.
* `fx_shield`, regenerated 2026-09-04 (the shell that must surround the hunter):
  `create_image_pixen(256², no_background, view="side", direction="west")` job
  `4200157a-9a34-4006-919b-c33ebbbe711c` — "a solid white ring of light: one big round circle band,
  thick and bright, the middle of the circle completely EMPTY, the ring reaching almost to all four
  edges of the picture" — then `animate_image` job `4ed34cb6-2e05-4c20-a42c-0227194d327d` (first and
  last frame pinned to that still), then
  `clip.py job --job … --out assets/art/VFX/shield/fx_shield_strip8_512.png --effect` and
  `rhart.py whiten`. Filling the frame is the whole point: a ring inscribed in the square means the
  content box IS the drawn diameter, so 1.15 × the hunter's height is 1.15 × the hunter's height.

## 7. Quality gate (`rhart.py gate`)

8 square frames; no empty frame; no disconnected blob above 1.5 % of the figure; scale drift
≤ 12 % (≤ 30 % for attack/death/cast, whose silhouettes legitimately change); baseline drift
≤ 6 % of a frame. Then the sheet, then a human.
