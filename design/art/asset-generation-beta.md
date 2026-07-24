# Asset Generation — Beta ("make it a game, not a text screen")

This is the **second batch**. Batch 1 (creature parts, item glyphs, HUD glyphs) is done and in the
game. This batch is what turns flat shapes-on-black into a real, animated beta: **scene backgrounds,
a UI kit, effect/animation sprites, a front-end, and icons.**

> Style is unchanged — reuse the **Master Style Block** and the palette from
> [`asset-generation-prompt.md`](asset-generation-prompt.md). Everything here is the same 480×270
> point-sampled pixel-art world, same 10-colour discipline, same locked palette.

---

## How the game will use these (so you generate the right thing)

- **The virtual screen is 480×270**, upscaled 3×. A **full-screen background is exactly 480×270** and
  **opaque**. Everything else is **transparent PNG**.
- **Animations are horizontal sprite strips.** One PNG, frames left→right, every frame the same size.
  I give you the frame size and count; the file width = frame_width × frame_count.
- **Creatures are already animated in CODE** from the parts you delivered (breathing idle, attack
  lunge, hit recoil, death) via the cutout rig. **You do NOT need to draw creature animation frames.**
  What creatures still need is only the *effects that play on top of them* (hits, breaks, death puffs)
  — those are in the VFX section.
- **Point-sampled, hard-edged, no anti-aliasing.** Same as batch 1.

**Priority order** (generate top-down; each tier alone makes the game look dramatically more finished):

1. **Backgrounds + title** — the single biggest "it's a game now" jump.
2. **Core combat VFX** — impacts, breaks, death. This is what "attacking has animation" means.
3. **UI kit** — panels/buttons/bars so menus stop being flat rectangles.
4. **Ability effects + icons** — identity and polish.

---

## 1. Backgrounds  →  `assets/art/backgrounds/`

**Opaque, exactly 480×270.** CRITICAL: keep them **low-contrast and desaturated** — they sit *behind*
the creature and the HUD, which must stay readable. Think "flattened, gallery-neutral lighting"
(art-bible: Cold Slate ambient). No bright focal points in the center where the creature stands. A
gentle darkened vignette at the edges helps the UI bands read.

| File | Scene | Direction |
|---|---|---|
| `bg_title.png` | Main menu | The hunter's silhouette on a ridge over a corrupted valley at dusk. Moody, Void Ink + Corruption Bloom haze, one point of Hearth Gold (a distant ward-fire). Room at top-center for the logo. |
| `bg_arena_verdant.png` | Combat (Verdant Hollow) | A shallow forest clearing — mossy ground plane in the lower third, blurred thorn-thicket treeline behind, soft god-rays. Desaturated Moss Green + Cold Slate. **Center-middle must stay calm/empty** so the creature reads on top. |
| `bg_forge.png` | The Forge | A dim smithy interior: a dark anvil/altar, Void Ink stonework, veins of Hearth Gold inlay and a low ember glow. Warm but dark. |
| `bg_warren.png` | Automation / farm | A burrow-den overlook: earthen alcoves/pods where creatures nest, a soft Moss Green ambient, a faint grid of "work" glow-points. Calm, organized, sleepy. |
| `bg_constellation.png` | Memory Dust | A deep Void Ink starfield with faint Bone-Parchment motes and thin connecting lines — a night sky that reads as a finite constellation map. No ground. |
| `bg_regionmap.png` | Region select / world map | A stylized parchment-and-ink map: Verdant Hollow marked, other regions faded/"not yet". Bone Parchment map on Void Ink, Hearth Gold for the conquered marker. |

*Results and Help reuse `bg_arena_verdant.png` dimmed by the code, so you don't need separate ones.*

---

## 2. Title / branding  →  `assets/art/ui/`

| File | Size | What |
|---|---|---|
| `logo_title.png` | ~320×90, transparent | The words **RESONANCE HUNTER** as a glyph-styled wordmark. A hard Void Ink contour, Bone Parchment faces, one Hearth Gold accent (e.g. a ward-ring behind/through the "O"). Reads at a glance; not ornate. |

---

## 3. Core combat VFX (animations)  →  `assets/art/vfx/`

**Transparent horizontal strips.** Frames play left→right then stop (one-shot) unless noted "loop".
These are the "attacking has animation" assets — they flash on the creature when hits land.

| File | Frame size | Frames | Sheet size | What it is |
|---|---|---|---|---|
| `vfx_hit.png` | 24×24 | 6 | 144×24 | A small impact spark for a normal basic-attack hit. Bone/Ember. |
| `vfx_weakhit.png` | 32×32 | 8 | 256×32 | A big **gold** burst for a weak-point hit — the payoff moment. Radiating shards. |
| `vfx_crit.png` | 32×32 | 8 | 256×32 | A sharper white-gold critical flash (reuses weakhit's energy, more violent). |
| `vfx_block.png` | 24×24 | 5 | 120×24 | A Cold-Slate spark/clang deflection for a successful block. |
| `vfx_dodge.png` | 40×28 | 5 | 200×28 | A pale after-image swoosh for a dodge (a smear of Bone that fades). |
| `vfx_interrupt.png` | 32×32 | 6 | 192×32 | A gold shockwave ring for an interrupt (the best defence). |
| `vfx_break.png` | 40×40 | 7 | 280×40 | A part **shattering** — chips/thorns flying off. Plays when a part breaks. |
| `vfx_death.png` | 56×56 | 8 | 448×56 | A creature **dissolving/scattering** into motes on defeat. Corruption Bloom → nothing. |
| `vfx_capture.png` | 56×56 | 8 | 448×56 | A **ward-seal closing**: concentric Hearth Gold rings contracting and locking. The capture-success moment. |

*Optional but nice:* `vfx_levelup.png` (32×32, 6) — a soft gold ring when a Training rank is bought.

---

## 4. Ability effects (animations)  →  `assets/art/vfx/`

One per starting ability. Transparent strips.

| File | Frame size | Frames | Sheet size | Ability |
|---|---|---|---|---|
| `vfx_ability_emberlance.png` | 40×24 | 6 | 240×24 | **Ember Lance** (Body / projectile): a bright lance/bolt that streaks and bursts. Crimson-into-white. |
| `vfx_ability_ruinstrike.png` | 56×56 | 6 | 336×56 | **Ruin Strike** (Shadow / strike): a torn Umbra-Indigo slash arc with frayed edges. |
| `vfx_ability_lastbreath.png` | 48×48 | 7 | 336×48 | **Last Breath** (Spirit / trap): a pale Wisp-Lavender glyph that blooms and pulses on the ground. |

---

## 5. UI kit  →  `assets/art/ui/`

Transparent. This replaces the flat rectangles and hard outlines that make menus feel like a
spreadsheet. Give panels a **soft, slightly rounded, engraved** feel (a Void Ink contour + a 1px inner
highlight), not sharp corners.

| File | Size | Notes |
|---|---|---|
| `ui_panel.png` | 48×48 | A window/panel frame designed for **9-slice** (16px corners, tileable 16px edges, hollow center). This one image skins every menu box. |
| `ui_panel_gold.png` | 48×48 | Same, but with a Hearth Gold accent border — for "earned"/highlighted panels. |
| `ui_button.png` | 64×20 | A soft button (idle). 9-sliceable (8px caps). |
| `ui_button_active.png` | 64×20 | The same button, highlighted/pressed (brighter edge, slight gold). |
| `ui_bar_frame.png` | 80×10 | An empty meter frame (endcaps + track) for health/resonance/progress bars. |
| `ui_bar_fill.png` | 76×6 | The fill that goes inside it — a plain light strip the code tints (red/gold/violet). 9-slice horizontally. |
| `ui_keycap.png` | 16×16 | A key-cap chip background; the code prints the letter (SPACE, F, R…) on top. |
| `ui_cursor.png` | 24×24 | A targeting reticle (replaces the code's corner brackets on the aimed part). Ember. |
| `ui_selector.png` | 12×12 | A small pointer/chevron marking the selected row in a list. Bone. |

---

## 6. Icons  →  `assets/art/icons/`

**16×16 transparent** unless noted. Single glyph, single-or-two-colour, must read at 16px.

**Hunter stats (9)** — used on the Training screen instead of text names:
`icon_stat_attack`, `icon_stat_focus`, `icon_stat_vitality`, `icon_stat_engineering`,
`icon_stat_guile`, `icon_stat_resonance`, `icon_stat_health`, `icon_stat_defense`, `icon_stat_crit`.
(Suggestions: attack = a fang/blade; focus = an eye; vitality = a leaf-heart; engineering = a gear;
guile = a hook; resonance = a tuning-wave; health = a heart; defense = a shield; crit = a starburst.)

**Roles (5)** — used on the farm team UI (currently text): `icon_role_attacker`, `icon_role_defender`,
`icon_role_support`, `icon_role_crafter`, `icon_role_producer`. Match the art-bible mass silhouettes
(attacker = forward blade; defender = broad shield; support = body+satellite; crafter = many small
prongs; producer = one swollen sac).

**Sources (6, optional)** — tiny element marks: `icon_src_body`, `icon_src_mind`, `icon_src_nature`,
`icon_src_machine`, `icon_src_shadow`, `icon_src_spirit`. Each in its locked Source colour.

**Ability icons (3, 24×24)** — for the combat ability slots (currently a tinted square):
`icon_ability_emberlance`, `icon_ability_ruinstrike`, `icon_ability_lastbreath`.

**Navigation (16×16, optional):** `icon_hunt`, `icon_forge`, `icon_farm`, `icon_dust` — for a bottom
nav bar. (`icon_gleam` and `icon_dust`-mote already exist as `ui_gleam_coin` / `ui_memory_dust`.)

---

## 7. (Only if you want more creatures)

The beta works with the 7 creatures you already delivered. If you want variety, each new creature is
the **same part set** as its role, transparent PNGs, named `crea_[source]_[role]_[variant]_[part]`:

- **Attacker / Defender:** `torso, left_claw, right_claw, flank, core` (+ optional `upper_arm, forearm`).
- **Support:** `torso, satellite, tether, flank, core`.
- **Producer:** `torso, sac, flank, core`.
- **Boss:** `torso, left_blade, right_blade, maw_upper, maw_lower, tail_thorn, flank, core`.

Keep each part on its own transparent sprite with a squared attachment end (the pivot), exactly as the
Whelp set is — that's what lets the rig move them.

---

## 8. The Hunter (YOU) — needed for the two-sided fight  →  `assets/art/ui/`

Combat now shows **you** on the left fighting the enemy on the right (a placeholder cloaked
silhouette is drawn until you provide these). Generate the player character:

| File | Size | What |
|---|---|---|
| `hunter.png` | ~44×60, transparent | The Hunter in an **idle/ready stance, facing RIGHT** (toward the enemy). A hooded/cloaked ranger silhouette with a visible weapon (blade or focus). Void Ink contour, muted cloak (deep slate/indigo), one small Hearth Gold accent (a ward-charm). Grounded, readable at small size. |
| `hunter_attack.png` | ~44×60, transparent | The same Hunter in a **lunging/striking pose**, weapon thrust toward the right. Used automatically on each attack; if you skip it, the idle sprite just dashes forward instead. |

Keep both the **same canvas size and the same foot position**, so swapping poses doesn't make the
figure jump. Facing right, always.

*(If you'd rather animate the Hunter richly later, the same cutout-parts approach as creatures works:
`hunter_torso/arm/weapon/legs` — but the two single poses above are enough for the beta.)*

---

## Naming & folders (recap)

```
assets/art/
├── backgrounds/   bg_*.png          (480×270, opaque)
├── vfx/           vfx_*.png         (transparent horizontal strips)
├── ui/            ui_*.png, logo_*.png
├── icons/         icon_*.png        (16×16, transparent)
├── creatures/     crea_*.png        (done — add here if making more)
└── items/         item_*.png        (done)
```

Filenames must match exactly (lowercase, underscores) — the loader keys on them. A missing asset never
crashes the game; it just falls back to the current flat shape, so you can deliver in any order and see
progress each time.

---

## What I'll build once these arrive (so you know it's not just decoration)

Assets alone don't animate — I'll write the code that plays them:

1. **Scene rendering** — each screen draws its `bg_*` behind the UI; menus reskin onto `ui_panel`.
2. **A real front-end** — a title screen (`bg_title` + `logo_title`) with PLAY / CONTINUE / QUIT, and a
   region-select using `bg_regionmap`. Right now the game drops you straight into a fight.
3. **Creature animation via the cutout rig** — idle breathing, an attack lunge on each auto-tick, a hit
   recoil, a topple-and-`vfx_death` on defeat. Uses the parts you already gave me; no new creature art.
4. **A VFX layer** — `vfx_hit`/`vfx_weakhit` fire where a hit lands, `vfx_break` on a part break,
   block/dodge/interrupt sparks on defence, ability sheets on cast, `vfx_capture` on a capture.
5. **Icon swaps** — stat/role/ability text replaced by the icons.

That is the "beta complete" pass: a front-end, animated fights with real impact feedback, and menus
that look built rather than printed. Ping me when you've generated a batch (even just the backgrounds)
and I'll wire it in.
