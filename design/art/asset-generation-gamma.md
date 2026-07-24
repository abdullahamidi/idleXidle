# Asset Generation — Gamma ("a world to conquer")

This is the **third batch**. Batch 1 (creature parts, item/HUD glyphs) and Batch 2 (backgrounds, UI
kit, VFX, front-end, icons — see [`asset-generation-beta.md`](asset-generation-beta.md)) are done and
in the game. Since then the game grew a **world**: a three-region conquest chain (Verdant Hollow →
Cinderworks → Umbral Reach), a world map, per-region idle farms, and combat that now *fights*
differently per region. This batch is the art that world is missing.

> Style is unchanged — reuse the **Master Style Block** and locked palette from
> [`asset-generation-prompt.md`](asset-generation-prompt.md). Same 480×270 point-sampled pixel-art
> world, same 10-colour discipline, hard edges, no anti-aliasing.

---

## What's already in vs. what's missing (so you don't redo work)

| Thing | Status |
|---|---|
| Verdant Hollow creatures (Whelp, Mossling, Bramblehide, Thornmaw boss) | ✅ in game |
| Backgrounds: title, verdant arena, forge, warren, constellation, region map | ✅ in game |
| Full UI kit, VFX strips, stat/role/ability/nav icons | ✅ in game |
| **The Hunter (you) sprite** | ❌ **still a placeholder silhouette** (carried over from Batch 2) |
| **Cinderworks + Umbral Reach arena backgrounds** | ❌ combat there reuses the verdant arena |
| **Cinderworks + Umbral Reach creatures** | ❌ render as flat grey "greybox" boxes |

**The greybox is intentional and safe.** Every new region works right now — it just draws flat shapes
where art is missing. Nothing crashes, and each file you drop in replaces its greybox the next launch.

**Priority order** (top-down; each tier alone is a visible jump):

1. **The Hunter** — it's *you*, on screen in every fight, and still a placeholder. Biggest single win.
2. **Two arena backgrounds** — makes travelling to a new region actually *look* like a new place.
3. **Cinderworks creatures** — the first region past the tutorial; kills the greyboxes there.
4. **Umbral Reach creatures** — the final region.
5. *(Optional)* world-map region emblems.

---

## 1. The Hunter (YOU)  →  `assets/art/ui/`   ★ TOP PRIORITY

Combat shows **you** on the left fighting the enemy on the right. A placeholder cloaked silhouette is
drawn until these exist; the moment they land, the game uses them automatically (no code change).

| File | Size | What |
|---|---|---|
| `hunter.png` | ~44×60, transparent | The Hunter in an **idle/ready stance, facing RIGHT** (toward the enemy). A hooded/cloaked ranger silhouette with a visible weapon (blade or focus). Void Ink contour, muted cloak (deep slate/indigo), one small Hearth Gold accent (a ward-charm). Grounded, readable at small size. |
| `hunter_attack.png` | ~44×60, transparent | The same Hunter in a **lunging/striking pose**, weapon thrust toward the right. Used automatically on each attack; if you skip it, the idle sprite just dashes forward instead. |

Keep both the **same canvas size and the same foot position** so swapping poses doesn't make the
figure jump. Facing right, always.

*(Optional richer path later: the cutout-parts approach as creatures use — `hunter_torso/arm/weapon/
legs` — but the two single poses above are enough.)*

---

## 2. Region arena backgrounds  →  `assets/art/backgrounds/`

**Opaque, exactly 480×270.** Same rules as Batch 2's backgrounds: **low-contrast, desaturated**,
**centre-middle kept calm/empty** so the creature and HUD read on top, gentle edge vignette. The code
already looks for `bg_arena_<theme>` and falls back to the verdant arena — so these appear the instant
they exist, no wiring needed.

| File | Region | Direction |
|---|---|---|
| `bg_arena_machine.png` | **Cinderworks** (Machine) | An industrial foundry floor: dark iron catwalks/pipework, a low furnace glow (muted Ember, never bright), Cold Slate metal, soot-black. Heavy, oppressive, mechanical. Matches its combat feel — slow, grinding, heavy. |
| `bg_arena_shadow.png` | **Umbral Reach** (Shadow) | A lightless rift-edge: Umbra-Indigo murk, faint Wisp-Lavender fog, torn silhouettes of dead structures, no horizon. Creeping and cold. Matches its combat feel — fast, harrying strikes from the dark. |

*Verdant Hollow keeps `bg_arena_verdant.png` (already delivered). Results/Help still reuse the active
arena dimmed by code.*

---

## 3. Cinderworks creatures (Machine)  →  `assets/art/creatures/`

Four creatures. **Same cutout-part discipline as the Whelp/Bramblehide set**: each part on its own
transparent PNG with a squared attachment end (the pivot), so the rig can animate it. Parts are scaled
to fit in combat, so exact pixel size is flexible — **match the existing creatures' proportions**
(standard torso ≈ 26×16, claw ≈ 16×14; boss torso ≈ 42×28). All in the **Machine** palette
(Cold Slate / iron / soot with a restrained Ember-glow accent).

| Creature | Role | Filename prefix | Parts (one PNG each) |
|---|---|---|---|
| **Cinder Warden** | Attacker | `crea_machine_atk_warden` | `torso, left_claw, right_claw, flank, core` |
| **Slag Drone** | Support | `crea_machine_sup_drone` | `torso, satellite, tether, flank, core` |
| **Iron Bulwark** | Defender | `crea_machine_def_bulwark` | `torso, left_claw, right_claw, flank, core` |
| **The Foundry Colossus** | Boss | `crea_machine_atk_foundry` | `torso, left_blade, right_blade, flank, core` |

Example files for one creature: `crea_machine_atk_warden_torso.png`,
`crea_machine_atk_warden_left_claw.png`, `…_right_claw.png`, `…_flank.png`, `…_core.png`.

**Efficiency note on the boss:** combat only renders `torso + two "claw" slots + flank + core`. So for
the boss, bake the "menace" (maw, extra plating) into the **torso** silhouette and deliver its weapons
as `left_blade` / `right_blade` (they fill the claw slots). Don't make separate maw/tail parts — the
combat view won't draw them.

---

## 4. Umbral Reach creatures (Shadow)  →  `assets/art/creatures/`

Same structure, in the **Shadow** palette (Umbra-Indigo / Void Ink with faint Wisp-Lavender edges).
These should read as darker, thinner, quicker things than Cinderworks' heavy machines.

| Creature | Role | Filename prefix | Parts (one PNG each) |
|---|---|---|---|
| **Umbral Stalker** | Attacker | `crea_shadow_atk_stalker` | `torso, left_claw, right_claw, flank, core` |
| **Gloom Wisp** | Support | `crea_shadow_sup_wisp` | `torso, satellite, tether, flank, core` |
| **Shade Bulwark** | Defender | `crea_shadow_def_bulwark` | `torso, left_claw, right_claw, flank, core` |
| **The Nightmaw** | Boss | `crea_shadow_atk_nightmaw` | `torso, left_blade, right_blade, flank, core` |

---

## 5. Optional — world-map region emblems  →  `assets/art/icons/`

The world map (press **W**) draws each region as a panel node with a status label. Small emblems would
give each node identity. **24×24 transparent**, one bold glyph each, in the region's Source colour.
Purely optional — the map reads fine without them.

| File | Region | Suggestion |
|---|---|---|
| `icon_region_verdant.png` | Verdant Hollow | A leaf / thorn-sprig (Moss Green). |
| `icon_region_cinderworks.png` | Cinderworks | A gear / furnace (Ember on iron). |
| `icon_region_umbral.png` | Umbral Reach | A rift / crescent (Umbra-Indigo). |

---

## Already delivered, not yet used (your call)

You've previously delivered a few **Nature** creatures the game doesn't spawn yet:
`crea_nature_atk_stalker`, `crea_nature_prd_sporeling`, `crea_nature_def_bulwark`. If you'd like more
variety in Verdant Hollow, say so and I'll wire them into its spawn pool — no new art required.

---

## Naming & folders (recap)

```
assets/art/
├── backgrounds/   bg_*.png          (480×270, opaque)   ← +bg_arena_machine, +bg_arena_shadow
├── creatures/     crea_[source]_[role]_[variant]_[part].png  (transparent, squared pivot end)
├── ui/            ui_*.png, logo_*.png  ← +hunter.png, +hunter_attack.png
├── icons/         icon_*.png        (transparent)       ← optional region emblems
├── vfx/           vfx_*.png         (done)
└── items/         item_*.png        (done)
```

Filenames must match exactly (lowercase, underscores) — the loader keys on them. A missing asset never
crashes; it falls back to the current placeholder, so deliver in any order and see progress each time.

---

## What I'll wire when these arrive

- **`hunter.png` / `hunter_attack.png`** and **`bg_arena_machine/shadow.png`** need **no code** — the
  game already looks for them and swaps the placeholder automatically. Just drop them in.
- **The new creatures DO need one code step from me:** each creature template is registered to its
  sprite prefix (and its part-slot map — e.g. a Support's `satellite`/`tether` fill the combat "claw"
  slots). I'll add those the moment the art lands. ⚠️ **Until I register them, deliver the art and tell
  me** — a registered-but-missing creature would draw *nothing*, so the greybox stays until both the
  art and the mapping are in. (This is the one place "just drop it in" doesn't fully apply.)

That's the "world complete" art pass: you on screen, two new regions that look as distinct as they now
fight, and no greyboxes left. Ping me when a batch is ready (even just the Hunter) and I'll wire it in.

---

## Not in this batch: audio

The game still has **no sound** — it needs the MonoGame content pipeline (MGCB) stood up first, and no
audio assets exist yet. That's a separate future batch (music beds per region, combat SFX, UI clicks).
Flagging it so it's on the radar, not asking for it here.
