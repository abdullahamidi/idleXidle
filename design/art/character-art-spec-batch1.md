# Resonance Hunter — Character Art Generation Spec (Batch 1: the Hunter, rigged parts)

> **Hand this file to the art-generation agent verbatim.** The agent has no access to the game
> codebase — everything it needs is here.
>
> **ALSO ATTACH, as the style reference image:** `hunter_idle.png` (the current *dressed* champion — a
> front-facing dark-fantasy figure in void-black/purple plate, gold filigree, a horned crown, a tattered
> ground-length cloak, faint violet glow). Match its rendered world. **BUT NOTE:** the reference is fully
> armored and helmeted, so it does **not** show the undressed body you are drawing this batch — for that,
> follow the base-identity brief in §5.3, which is authoritative where the image cannot help.

---

## 0. TL;DR for the art agent

Deliver, **once**, for the player Hunter:

- **16 separated body-part PNGs** (transparent, hand-drawn, front-facing), one neutral rest pose.
- **1 rig sidecar** `hunter_rig.json` — per part: pivot, child joints, master-canvas position, rest
  angle, draw order; plus the 8 equipment sockets.
- **Proof images** (full-figure composite, per-part pivot proofs, grayscale) in a `/preview/` folder.

You are **NOT** drawing animation frames, strips, or a single flat sprite. Motion is done in code by
rotating these parts about their pivots. You draw each part **once**.

**Two approval gates (do these before finishing):** (1) deliver the full-figure composite (§4) and get
the base-character design signed off **before** cutting the 16 parts; (2) confirm the "MAIN side" mapping
in §2.

---

## 1. What a "character" is here (Option B — cutout rig)

The game (Resonance Hunter, MonoGame 3.8.4.1, C#/.NET 8) will render battle characters as a **2D cutout
rig**: a parent→child bone hierarchy. At runtime the engine walks the tree, computes each bone's world
position + rotation, and draws **one texture per bone**, rotated about that texture's pivot, in a fixed
back-to-front paint order (§2). Idle sway, breathing, attack swings, hit recoil, and death are authored
later as **code-side pose/keyframe tables** — never as art.

Equipment (helm, chest, weapon, …) is layered on in a **later batch** by attaching an item sprite to a
named **socket** on the body; it inherits that socket's position + rotation and moves with the animation
automatically. **This batch draws no equipment** — it only exposes the sockets and shapes the body so
equipment drops in cleanly.

**The base body is the Hunter UNDRESSED of its 8 equipment slots**, wearing only simple underlayers, so
equipment parts add the plate/helm/weapon later:

- **head:** bare (short hair, no hood, no horns) — a *helm* (which carries the horned crown) attaches later.
- **torso:** simple underarmor/gambeson — a *chest* piece attaches later.
- **hands:** bare — *gloves* and a *weapon* attach later.
- **feet:** soft foot-wraps — *boots* attach later.
- **cloak:** the Hunter's signature tattered cloak **is part of the base** (see §2, §5.3) — it is not one
  of the 8 gear slots, and the character is inseparable from it.

### Hard rules (do not violate)

- **One sprite per part. No animation frames, flipbooks, strips, or per-angle variants.** The engine
  rotates each single sprite at draw time.
- **One neutral rest pose only.** No idle-vs-attack pose pair.
- **No baked overlays.** Do not paint equipment, held weapons, ability glyphs, ground/cast shadows, or
  full-screen lighting into a part. A part's own *form* shading (rim light, occlusion in crevices,
  metallic speculars) is expected and good; a *cast shadow on the floor* is not (the engine draws that).
- **Front-facing only** (§4). No side profiles, turnarounds, or multiple facings.

> **Note on the existing overlay system (for the owner/engineers, not the artist):** this socket rig
> **replaces** the currently-coded full-canvas overlay approach (`champion_<pose>_base` +
> `overlay_<slot>_<pose>`, `asset-integration-spec.md §2`). Adopting the rig supersedes that contract and
> any shipped `overlay_*` art; batch-2 equipment is authored as socket-anchored single sprites, not
> full-canvas per-pose overlays. `asset-integration-spec.md §2` must be revised to match.

---

## 2. Part list (front-facing humanoid)

**MAIN vs OFF side — normative:** **MAIN = the viewer's RIGHT** side of the figure (screen-right, toward
where the enemy stands); **OFF = the viewer's LEFT**. All `*_main` parts/sockets are on screen-right.
(Owner: confirm this mapping — flip if you want the weapon on screen-left.)

Rooted at **pelvis** (the only bone with no parent; its transform origin is the character's ground/root
point). The figure faces the viewer in a slight 3/4, so it has a **near/main** side (weapon side, drawn
frontmost) and an **off** side (drawn behind the torso). **Both sides are fully lit** — do not darken the
off side to a flat silhouette; at most a very subtle ambient cooling for depth.

**16 sprite-bearing parts (one PNG each):**

| Part id | Parent | Depicts | Pivot (proximal joint) |
|---------|--------|---------|------------------------|
| `pelvis` | *(root)* | Hips/waist + belt line, in underarmor. Root of the whole figure; carries both legs, the torso, and the belt socket. | Hip line, on the figure's vertical centerline (the ground-column) |
| `torso` | `pelvis` | Chest + abdomen in underarmor/gambeson. Carries head, both arms, and the cloak. | **Waist / lower spine** (where it meets the pelvis) |
| `head` | `torso` | Head + short neck, **bare** (no helm, no horns). Faces forward. Carries the helm + aura sockets. | Neck base |
| `cloak` | `torso` | The signature tattered cloak, hanging from the shoulders/upper back to ~ankle length. Drawn **backmost**. | Upper back / between the shoulder blades |
| `arm_upper_main` | `torso` | Weapon-side upper arm (shoulder→elbow). | Shoulder |
| `arm_fore_main` | `arm_upper_main` | Weapon-side forearm (elbow→wrist). | Elbow |
| `hand_main` | `arm_fore_main` | Weapon hand, **empty open grip** oriented for a side-swing. | Wrist |
| `arm_upper_off` | `torso` | Off-side upper arm. | Shoulder |
| `arm_fore_off` | `arm_upper_off` | Off-side forearm. | Elbow |
| `hand_off` | `arm_fore_off` | Off hand (open). | Wrist |
| `leg_thigh_main` | `pelvis` | Weapon-side thigh (hip→knee). | Hip |
| `leg_shin_main` | `leg_thigh_main` | Weapon-side shin (knee→ankle). | Knee |
| `foot_main` | `leg_shin_main` | Weapon-side foot (ground contact). | Ankle |
| `leg_thigh_off` | `pelvis` | Off-side thigh. | Hip |
| `leg_shin_off` | `leg_thigh_off` | Off-side shin. | Knee |
| `foot_off` | `leg_shin_off` | Off-side foot. | Ankle |

**Transform hierarchy (parent → children):**
```
pelvis (root)
├─ torso
│  ├─ head
│  ├─ cloak
│  ├─ arm_upper_main → arm_fore_main → hand_main
│  └─ arm_upper_off  → arm_fore_off  → hand_off
├─ leg_thigh_main → leg_shin_main → foot_main
└─ leg_thigh_off  → leg_shin_off  → foot_off
```

**Non-sprite socket bones** (declared in the sidecar, NO texture): `socket_head`, `socket_torso`,
`socket_hand_main`, `socket_hand_off`, `socket_foot_main`, `socket_foot_off`, `socket_belt`,
`socket_aura`. See §6.

**Paint order (`draw_order`, back → front).** This is the ONLY source of `draw_order` — do not use any
table row position. It is *independent* of the transform hierarchy above.

```
0  cloak                 (backmost)
1  arm_upper_off  2 arm_fore_off  3 hand_off      (off arm, behind torso)
4  leg_thigh_off  5 leg_shin_off  6 foot_off      (off leg, behind)
7  pelvis
8  torso
9  leg_thigh_main 10 leg_shin_main 11 foot_main   (near leg, in front)
12 head
13 arm_upper_main 14 arm_fore_main 15 hand_main   (weapon arm, frontmost)
```

Equipment layers on top of the body later, in this order (awareness only; not this batch):
`chest → boots → gloves → helm → weapon → ring → charm → focus`.

---

## 3. Per-part deliverable rules

### 3.1 One PNG per part
- **32-bit RGBA PNG**, transparent background.
- **Straight (non-premultiplied) alpha** — the engine premultiplies at load; do **not** pre-multiply.
- **One part per file**, tightly cropped to the part's bounding box **+ exactly 4px transparent padding**
  on every side.
- **sRGB, no embedded ICC/color profile.**
- **No BC/DXT/block compression** — RGBA8 only.
- **COORDINATE SPACE (critical):** every part-pixel coordinate in the sidecar (`pivot`, `child_joints`,
  socket `local_offset`) and the sidecar `canvas.w/h` refer to the **FINAL exported PNG *including* the
  4px pad**, top-left pixel = (0,0), +X right, +Y down. Do **not** measure against the un-padded crop.

### 3.2 Pivot / rotation origin — the load-bearing metadata
Every part rotates about a **pivot pixel** the engine uses as the sprite's rotation origin. In each
part's own pixel space mark:

- **`pivot`** — the **proximal joint** where this part attaches to its parent (see the Pivot column in
  §2). This is the rotation origin.
- **`child_joints`** — for each direct child, the pixel where that child attaches, **keyed by the child's
  exact Part id from §2** (not a joint name). Examples:
  - `pelvis.child_joints` = `{ "torso": {…waist…}, "leg_thigh_main": {…hip…}, "leg_thigh_off": {…hip…} }`
  - `torso.child_joints` = `{ "head": {…neck…}, "cloak": {…upper-back…}, "arm_upper_main": {…shoulder…}, "arm_upper_off": {…shoulder…} }`
  - `arm_upper_main.child_joints` = `{ "arm_fore_main": {…elbow…} }`

- **`rest_angle_deg` — datum (must follow exactly):** `rest_angle_deg` = the angle, in degrees, measured
  **clockwise** (screen space, +Y down) **from straight-DOWN (the +Y axis = 0°)** to the part's
  **proximal→distal axis**, i.e. the vector from `pivot` to that part's primary `child_joint`, *as drawn*.
  For terminal parts with no child (hands, feet, head, cloak) measure to the part's anatomical long axis
  (head/cloak point up ≈ 180°; a foot points forward ≈ 90°). A part drawn hanging straight down = **0°**.

Deliver these in the sidecar (§8.3), not baked into the image. Author each part in its **natural rest
orientation** (arm hanging, leg standing) — you do not have to align limbs to any fixed axis; the sidecar
records the drawn orientation via `rest_angle_deg` and the engine rotates away from it. Also deliver a
**pivot-proof** image per part (crosshair at `pivot`, dot at each child joint) in `/preview/`.

### 3.3 Motion envelope & joint coverage
This is a **front-facing, in-plane-rotation-only** rig (no depth/foreshortening). Parts rotate about
their pivots in the screen plane. Author cuffs sized for the reachable arc, and do **not** let a limb
cross the torso silhouette (a fixed paint order would occlude a cross-body reach wrong).

- `arm_upper_main` / `arm_fore_main` / `hand_main`: **widest** — a **lateral side-swing** toward the
  enemy (up to ~90° at the shoulder outward + ~90° at the elbow). **Most generous cuffs.**
  - **Honest note:** in-plane rotation gives an AFK-Arena-style **lateral/overhead swing**, **not** the
    shipped forward two-hand thrust (that is a depth motion this rig cannot do). Orient the main hand /
    grip for a **side-swing arc**, not a forward stab.
- `arm_*_off`: moderate, **shallow** cross-body only (~±30°, kept inside the torso silhouette).
- `head`: small tilt/nod (~±15°).
- `torso`: lean/sway about the **waist** (~±10°) — because the root is the pelvis, this leaves the feet
  planted.
- `pelvis`: tiny weight-shift only.
- legs: subtle shift (~±10°), a step/stagger on hurt/death (~±30° on the main leg).
- `cloak`: sways with the torso; secondary cloth motion may be added in code later.

**Rotation cuff:** extend each limb **past its pivot** with a rounded cap of body material (radius ≥ the
limb's cross-section at that joint, centered on the pivot) so the seam stays covered through the bend.
**Parent socket cup:** give the parent a matching rounded pad around each child's attach point so the
child's cuff always overlaps parent material.

### 3.4 Neutral rest orientation
Draw each part in a single neutral rest pose (§4). No anticipation, squash, or attack skew — the rig adds
all motion.

---

## 4. Perspective & pose (mandatory — matches the shipped game)

- **FRONT-FACING, slight 3/4, one facing.** The Hunter faces the **viewer** (as in `hunter_idle.png`),
  turned a few degrees so the MAIN/weapon side (screen-right) reads slightly forward. **Not** a side
  profile, turnaround, or isometric. This matches every character in the game.
- **Neutral rest pose:** relaxed heroic standing — weight even or slightly on the near/main leg, spine
  near-vertical with a subtle forward-lean presence, **arms hanging slightly bent and slightly outward at
  the sides** (open hands, ready), head level facing forward, main foot a touch forward of the off foot
  for a stable planted stance, cloak hanging behind to ~ankle. This is the pose all parts are cut from.
- **APPROVAL GATE:** author a **full-figure composite** in this neutral pose at the master scale (§7) and
  deliver it as `/preview/hunter_ref.png` for **design sign-off first** — it must be approved as a
  *character design* (face, hair, build, cloak, proportions per §5.3), not just proportions, **before**
  you separate it into the 16 parts along natural seams (shoulder, elbow, wrist, neck, waist, hips,
  knees, ankles, upper-back for the cloak).
- Runtime rotation is **stepped** (the code currently quantizes angles; step count is a code-side tuning
  knob still being validated). You do not author for this — it is not an art constraint.

---

## 5. Visual style

**This game is hand-drawn "vector"/painterly, rendered with smooth (bilinear/LinearClamp) filtering —
NOT flat-fill pixel art.** Match the reference image. (Some checked-in project docs still describe an old
flat-fill/PointClamp/indexed-palette direction; those are being formally updated by the owner to the
painterly reality — follow the reference and this spec, and do not author to the old pixel rules.)

### 5.1 Rendering
- **Painterly-with-structure:** solid volumetric forms with **soft internal shading and gradients** (rim
  light, ambient occlusion in crevices, metallic speculars on trim). Smooth filtering is on.
- **Opaque parts on transparent background.** Front-lit. No base-layer glow-bloom (glow is a narrow
  effect layer added later, not the body's default). No baked ground/cast shadow (engine draws it).

### 5.2 Palette / world
Dark high-fantasy. The dressed reference reads **void-black & deep desaturated purple** armor, **warm
gold** filigree, faint **violet** arcane glow, cool near-black shadows. For the **undressed base** you
draw this batch, keep colors **grounded and muted** (worn leather / dark cloth underarmor, natural skin,
muted metal on a base buckle) so painted/tinted gear pops on top. **Do not** cover the base in gold
filigree or full void-plate — that belongs to the equipment parts. No hard palette-count cap; stay
cohesive with the reference and avoid saturated colors that would fight tinted equipment.

### 5.3 Undressed-base character brief (DEFAULT — owner to confirm/redirect at the §4 gate)
The reference shows only the armored/helmeted champion, so the base identity is specified here:

- **Build:** lean adult heroic; androgynous-leaning read. *(Owner may redirect.)*
- **Head:** **no hood, no horns** (the helm equipment owns all headgear/horns); short-to-mid dark hair;
  pale, cool-toned skin; calm, determined face. Optionally retain a faint violet eye-glow as a subtle
  base trait.
- **Torso/arms:** dark fitted gambeson/underarmor, muted charcoal with a cool undertone; bare forearms
  or simple wraps.
- **Hands:** bare, neutral skin, open relaxed grip.
- **Feet:** soft foot-wraps / soft boots (armored *boots* attach later).
- **Cloak:** tattered dark cloak from the shoulders to ~ankle, muted with a faint cool sheen — the
  Hunter's signature; it stays on the base.

### 5.4 Readability
- **Strong, unbroken outer silhouette** — one clear readable shape at a distance; the outer contour must
  not notch inward to reveal a seam.
- **Part boundaries read through form, shading, and slight proportion differences**, not a flat 1px line.
- **Colorblind-safe by construction:** never rest a meaningful distinction on hue alone — carry it in
  value and shape. The figure must stay legible in grayscale (`/preview/hunter_grayscale_test.png`).
- **Scale-aware:** the figure renders fairly small in the arena (§7) though authored large — favor clear
  shapes and value contrast over tiny filigree that muddies when scaled down.

---

## 6. Attachment sockets (expose now; equipment drops in later)

A **socket** is a named, textureless child bone parented to a body bone at a fixed local offset (and an
optional rest rotation for grip orientation). A later equipment part maps to a socket id and draws at the
socket's resolved position/angle. **This batch:** mark the 8 socket anchors + orientation in the sidecar
and shape the body so each makes sense. **Draw no equipment.**

| Socket id | Parent | Anchor to mark | `accepts` (canonical tokens) |
|-----------|--------|----------------|------------------------------|
| `socket_head` | `head` | Crown/brow, centered | `["helm"]` |
| `socket_torso` | `torso` | Center of the chest front plane | `["chest"]` |
| `socket_hand_main` | `hand_main` | Center of the weapon grip (thumb-web) | `["weapon","ring","gloves"]` |
| `socket_hand_off` | `hand_off` | Center of the off-hand grip | `["gloves"]` |
| `socket_foot_main` | `foot_main` | Instep / top of the main foot | `["boots"]` |
| `socket_foot_off` | `foot_off` | Instep / top of the off foot | `["boots"]` |
| `socket_belt` | `pelvis` | Front of the hip / belt line | `["charm"]` |
| `socket_aura` | `head` (see note) | one head-width to the OFF side and slightly up | `["focus"]` |

- `socket_hand_main` is the most load-bearing (widest arc) — make the main hand's grip open and
  unambiguous, with clearance for a weapon to rotate with the wrist.
- **`socket_aura` must NOT inherit rotation.** Set `inherit_rotation: false` so the floating focus orb
  hovers near the head instead of swinging on a rigid stick when the head tilts. Suggested
  `local_offset ≈ (−90, −40)` px from the head pivot (one head-width toward the OFF/left side, slightly
  up).
- **Chest caveat (batch-2 awareness):** the dressed reference's plackart spans the torso *and* the hip.
  A single torso-mounted chest sprite cannot bend at the waist when torso and pelvis lean independently,
  so batch-2 chest will likely split into a torso piece (`socket_torso`) and a tasset/hip piece
  (`socket_belt`). Shape the base torso/pelvis so that split reads cleanly.

---

## 7. Resolution, canvas & pipeline

**Live-verified against the shipping renderer:** canvas, foot anchor, sampler, and alpha handling below
are confirmed in code. **To-be-built:** the one-texture-per-bone cutout-rig renderer and the
`hunter_rig.json` loader do not exist yet (the bone rig is a validated dev spike) — they are implemented
alongside this art. The art deliverable does not depend on that.

- **Master canvas: 800 × 1040 px, authored @2x**, full standing figure, **feet planted at y = 992**
  (foot-anchor line), head near the top. This is the champion canvas the engine already uses.
- **Pipeline:** `SamplerState.LinearClamp` (smooth filtering), 1920×1080 internal target, **non-integer
  scaling is fine**, **premultiplied at load** (so export **straight** alpha), RGBA8, **no block
  compression**. Author **high-res and smooth**, not to a pixel grid.
- **In-arena size (context only — engine scales the master down):** the Hunter draws into a ~360×390 box,
  ground line y≈735, foot-center near screen (560, 735). You author at the 800×1040 master; the engine
  fits and bottom-anchors it.
- **`foot_anchor`:** the ground-contact point the engine plants on the arena floor — author it at the
  **horizontal midpoint between the two feet**, at y=992 (x=400).
- **Per-part export:** cut each part from the composite, export its own tightly-cropped PNG (+4px pad),
  and record in the sidecar: `canvas` (w/h of the padded PNG), `pivot`, `child_joints`, `master_pos`
  (where the pivot sits on the 800×1040 canvas), `rest_angle_deg`, `draw_order`.

**Why parts-not-frames:** resident texture ceiling is 512 MB; hand-drawing every animation frame for the
Hunter + enemy + boss roster would blow past it. A cutout rig authors each character's parts once and
animates in code, so the whole motion matrix costs ~zero extra art. That is the entire reason for the rig.

---

## 8. File naming & sidecar (what the loader consumes)

### 8.1 Key rule
The engine's asset key = the **filename without extension** (matched **case-insensitively** — use
all-lowercase `snake_case`). The loader scans `assets/art/**` recursively and **folder names do NOT enter
the key**, so the map is flat: **every basename must be globally unique.** The loader **excludes** any
file whose path contains a skipped-folder segment or a forbidden token (§8.4); for every non-excluded
file, key = basename.

### 8.2 Part filenames
Prefix **`hunter_part_`** (a new namespace; the old keys `hunter_idle` / `hunter_attack_01` /
`hunter_defeated` / `hunter_portrait` remain until the rig is wired).

| Part | Filename → key |
|------|----------------|
| pelvis | `hunter_part_pelvis.png` |
| torso | `hunter_part_torso.png` |
| head | `hunter_part_head.png` |
| cloak | `hunter_part_cloak.png` |
| arm_upper_main | `hunter_part_arm_upper_main.png` |
| arm_fore_main | `hunter_part_arm_fore_main.png` |
| hand_main | `hunter_part_hand_main.png` |
| arm_upper_off | `hunter_part_arm_upper_off.png` |
| arm_fore_off | `hunter_part_arm_fore_off.png` |
| hand_off | `hunter_part_hand_off.png` |
| leg_thigh_main | `hunter_part_leg_thigh_main.png` |
| leg_shin_main | `hunter_part_leg_shin_main.png` |
| foot_main | `hunter_part_foot_main.png` |
| leg_thigh_off | `hunter_part_leg_thigh_off.png` |
| leg_shin_off | `hunter_part_leg_shin_off.png` |
| foot_off | `hunter_part_foot_off.png` |

### 8.3 Rig sidecar `hunter_rig.json` (rig data, not a texture)
```json
{
  "character": "hunter",
  "facing": "front",
  "master_canvas": { "w": 800, "h": 1040, "authored_scale": 2 },   // authored_scale informational; leave 2
  "foot_anchor": { "x": 400, "y": 992 },                            // midpoint between feet, on the floor line
  "root_bone": "pelvis",
  "parts": [
    {
      "id": "arm_upper_main",
      "parent": "torso",
      "file": "hunter_part_arm_upper_main.png",
      "canvas": { "w": 128, "h": 240 },            // padded-PNG dimensions
      "pivot": { "x": 64, "y": 24 },               // shoulder, in padded-PNG pixels = rotation origin
      "master_pos": { "x": 470, "y": 300 },        // where the pivot sits on the 800x1040 canvas
      "rest_angle_deg": 0,                          // arm drawn hanging straight down (pivot->elbow = +Y)
      "draw_order": 13,
      "child_joints": { "arm_fore_main": { "x": 64, "y": 224 } }   // elbow (part-id key), padded-PNG pixels
    }
    // ... one entry per part in §2 (16 total)
  ],
  "sockets": [
    {
      "id": "socket_hand_main",
      "parent": "hand_main",
      "local_offset": { "x": 10, "y": 26 },        // from the parent part's pivot, in parent padded-PNG pixels
      "rest_angle_deg": 0,                          // intended grip orientation
      "accepts": ["weapon", "ring", "gloves"]
    },
    {
      "id": "socket_aura",
      "parent": "head",
      "local_offset": { "x": -90, "y": -40 },
      "rest_angle_deg": 0,
      "inherit_rotation": false,                    // hovers; does not swing with head tilt
      "accepts": ["focus"]
    }
    // ... all 8 sockets from §6
  ]
}
```
Angles in **degrees**, clockwise, screen space (+Y down), datum per §3.2. All pixel coords are in the
named part's **padded-PNG** space, top-left = (0,0).

### 8.4 Skipped folders & forbidden tokens (matches the loader exactly)
- **Skipped FOLDERS** (any file whose path contains these folder segments is ignored — put non-runtime
  images here): **`preview/`, `native/`, `mask/`, `medallion/`.** → Put all proof/reference images in a
  **`/preview/`** folder; they will be ignored by the game and are safe.
- **Forbidden filename/path TOKENS** (these hard-error the load — never use in a runtime filename):
  `source_reference`, `source_sheet`, `concept`, `contact_sheet`, `runtime_assets_preview`, `poster`,
  `showcase`, `mood_board`, `pitchboard`.
- The `hunter_part_*` and `hunter_rig` names contain none of these and load fine. Proof files
  (`hunter_ref`, `hunter_part_*_proof`, `hunter_grayscale_test`) are safe **because they live in
  `/preview/`**, not because of their names.

---

## 9. Batch-one scope

**The player Hunter only** (16 part PNGs + `hunter_rig.json` + proofs). Rationale: the Hunter is on
screen 100% of the time, so rigging it validates the whole pipeline (canvas, foot-anchor, pivots,
sockets, code-pose motion) before any other character. Enemies and bosses reuse this exact framework with
creature-specific part sets — **deferred to batch two.**

**Delivery checklist:**
- [ ] `/preview/hunter_ref.png` — full-figure neutral composite, **approved as a character design first** (§4).
- [ ] 16 `hunter_part_*.png` (§8.2), each the correct part, neutral rest pose, front-facing, undressed base.
- [ ] `hunter_rig.json` — all 16 parts (canvas, pivot, master_pos, rest_angle_deg, draw_order,
      child_joints keyed by child part-id) + all 8 sockets (parent, local_offset, rest_angle_deg,
      accepts; `socket_aura` with `inherit_rotation:false`), `root_bone:"pelvis"`.
- [ ] `/preview/hunter_part_*_proof.png` — each part with pivot crosshair + child-joint dots.
- [ ] `/preview/hunter_grayscale_test.png` — full figure desaturated.

---

## 10. Owner acceptance checklist (verify on return)

1. **Exactly one sprite per part** — no strips, no per-angle variants, no idle/attack pose pair.
2. **16 part PNGs**, each the correct part, neutral rest pose, **front-facing**, base **undressed** of the
   8 gear slots, **cloak included**, head **bare (no helm/horns/hood)**.
3. **Hand-drawn / painterly**, matching `hunter_idle.png`'s world (soft shading & gradients OK) — not
   flat-fill pixel art, not side-view.
4. **`hunter_rig.json` complete** — `root_bone:"pelvis"`; every part has canvas (padded-PNG), pivot,
   master_pos, rest_angle_deg (datum per §3.2), draw_order (from the §2 paint list), child_joints keyed
   by child **part-id**; all 8 sockets present; `socket_aura` has `inherit_rotation:false`.
5. **Pivot proofs match** — crosshairs land exactly on joints; child-joint dots land where the child's
   pivot will sit; all coords in padded-PNG space.
6. **Joint overlap** — each limb's rotation cuff covers its seam through its §3.3 envelope; no gap opens;
   no limb crosses the torso silhouette.
7. **Pelvis-rooted rig sanity** — a ±10° torso lean about the waist leaves the feet planted (verify the
   torso pivot is at the waist, not the neck).
8. **Strong unbroken outer silhouette**; parts read via form/shading/proportion; grayscale test passes.
9. **Straight (non-premultiplied) alpha, sRGB no profile, RGBA8 (no BC/DXT), 4px pad**, lowercase
   snake_case unique filenames; proofs isolated in `/preview/`.
10. **Foot anchor at (400, 992)** midpoint between feet on the 800×1040 master; per-part master positions
    consistent with the composite.
11. **No baked equipment, glyphs, ground shadow, or full-screen lighting** in any part.

---

### Appendix — the render contract in one paragraph
The engine resolves each bone's transform **parent-before-child** (a child inherits its parent's position
and rotation), then **paints** the parts in the sidecar's explicit `draw_order` (back → front, §2) —
these are two different orderings; do not conflate them. Each part is drawn as **one texture, rotated
about its `pivot`, at its resolved world position**; textureless bones (sockets) are skipped and later
carry equipment. Everything above exists to make that produce a believable, riggable, equip-able,
front-facing Hunter that matches the shipped art.
