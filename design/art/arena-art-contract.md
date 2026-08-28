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
| Effect | played at scale 1–3 | pixen **256** | 512 | — |

Every strip is **8 square 512-px frames in one row** (`*_strip8_512.png`); the renderer derives the
frame count from `width / height`. Bosses moved from `_strip8_1024` to the same 512 contract.

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

## 4. Naming (unchanged where the code already asks)

| Asset | Path | Key |
|---|---|---|
| Champion clip | `assets/art/Animations/Roster/<id>_<clip>/` | `char_<id>_<clip>_strip8_512` |
| Champion still | `assets/art/Characters/Roster/` | `char_<id>_base` (the SE rotation, 512²) |
| Enemy clip | `assets/art/Animations/Enemies/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Enemy still | `assets/art/Enemies/enemies/<key>/` | `<key>_idle_01`, `<key>_attack_01` (frame 0) |
| Boss clip | `assets/art/Animations/Bosses/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Effect | `assets/art/VFX/<name>/` | `fx_<name>_strip8_512` |

Enemy keys (one per Source): bonecrawler (Body), soul_leech (Mind), wisp (Nature),
stone_sentinel (Machine), shadeling (Shadow), rift_guardian (Spirit).
Boss keys (one per region): thorn_regent, forge_colossus, void_reaper, crystal_lich, lumen_angel,
spirit_matron.

## 5. Effects (the skill layer)

Per Form, played on the enemy row unless noted, tinted by the casting skill's **Source** colour:

| Key | What | Where |
|---|---|---|
| `fx_strike` | a crescent slash arc sweeping across | enemy |
| `fx_projectile` | a bolt streaking left→right into a small burst | enemy |
| `fx_aura` | an expanding ring pulse | champion |
| `fx_trap` | a ground burst of shards erupting upward | enemy |
| `fx_mark` | a sigil that flashes and locks | enemy |
| `fx_transformation` | an upward surge of light | champion |

UI flourishes generated the same way: `fx_bind_chain` — a ring of heavy gold links with inward
spikes, drawn wide and contracting — plays over a skill's Source medallion on the BUILD screen when a
Vow is bound to it. It is an asset rather than drawn primitives on purpose (playtest 2026-08-28:
"kendin bir kutucuk veya buton oluşturup görsel olarak onu kullanıyorsun").

Combat beats: `fx_hit` (spark), `fx_weakhit` (puff), `fx_crit` (starburst), `fx_death` (ash plume),
`fx_heal` (rising motes), `fx_shield` (dome flash), `fx_levelup` (column of light).

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

## 7. Quality gate (`rhart.py gate`)

8 square frames; no empty frame; no disconnected blob above 1.5 % of the figure; scale drift
≤ 12 % (≤ 30 % for attack/death/cast, whose silhouettes legitimately change); baseline drift
≤ 6 % of a frame. Then the sheet, then a human.
