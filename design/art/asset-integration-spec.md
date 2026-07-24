# Asset Integration Spec & Gap List — Vector Art Pack v1

> **Status**: Authoritative integration contract for the locked vector art direction (Hades × AFK Arena).
> **Created**: 2026-07-22
> **Pipeline facts (verified in code)**: assets load from `assets/art/**` keyed by **filename without
> extension or folder** (`assets/art/items/item_weapon.png` → key `item_weapon`). The renderer already
> draws to a **1920×1080** internal target (`ArtScale = 4` over a 480×270 logical grid) with
> **`SamplerState.LinearClamp`** and premultiplied alpha — i.e. it is already set up for smooth, high-res
> vector art, not pixels. Default presentation is now **Fullscreen / 1920×1080** (changeable in Settings).

This document is the single source of truth for **how each asset must be authored** and **exactly what is
still missing**. The rule is absolute: **no asset is ever displayed in place of another.** Where an asset is
absent, the game keeps its old flat-shape greybox for that one element — it never borrows a different PNG.

> **Pack v2 update (2026-07-22): almost the entire §6 gap list was filled and wired.** Now integrated:
> the full champion layering (all 8 slots × idle **and** attack poses), all six backgrounds + `logo_title`,
> all six region bosses (drawn on boss waves), the whole UI chrome (`ui_panel`/`ui_button`/`ui_bar_*`/
> `ui_keycap` — the menus now carry the vector look), and combat FX (semantic events mapped to the generic
> effect strips). **Still open:** optional per-role creatures (Model B, §4); a generic `item_material` glyph
> (the Material base-type loot still draws a flat square); `fx_bolt`/`fx_aura`/`fx_heal` strips are authored
> but nothing triggers them yet; and FX use alpha blend, not the additive the manifest recommends (a polish
> pass). See §7 for the current status.

---

## 1. Naming — the code will be re-pointed to YOUR names

The pack's names are the ones we keep. The code currently asks for older keys; I am re-pointing the code to
the pack, so **author to the names below and they will just work**. Filename = key, always.

| Domain | Key pattern | Notes |
|---|---|---|
| Champion base | `champion_<pose>_base` | pose ∈ `idle`, `attack` |
| Champion gear overlay | `overlay_<slot>_<pose>` | slot ∈ `helm chest gloves boots weapon focus ring charm`; pose ∈ `idle`, `attack` |
| Champion (pre-composited ref) | `champion_<pose>_full` | base + a fixed sample kit; used only for the title/menu, **never** for the dressed champion |
| Creature (per source) | `crea_<source>` | source ∈ `body mind nature machine shadow spirit` — see §4 for the model decision |
| Region boss | `boss_<region>` | region ∈ `verdant_hollow cinderworks umbral_reach the_pale_choir the_still_archive marrow_wastes` |
| Gear icon | `item_<type>` | type ∈ `weapon charm focus helm chest gloves boots ring` |
| Material icon | `mat_<name>` | see §5 — the four names map to the four tiers |
| Rarity frame | `frame_<rarity>` | rarity ∈ `common uncommon rare epic legendary` |
| Loot chest / hatch core | `chest_loot`, `core_hatch` | |
| Form glyph (white, tinted in-engine) | `form_<form>` | form ∈ `strike volley aura trap mark morph` |
| Source gem | `source_<source>` | the six elements |
| Vow sigil | `vow_<vow>` | vow ∈ `bloodied boss_bound fragility patience reckless_offering vanguard` |
| FX strip | `fx_<name>_strip<N>` | horizontal strip, 128² cells, N frames, 12fps, additive |
| UI icon | `icon_gleam`, `icon_dust` | |

Canvases (from `manifest.json`): champion **800×1040** (foot anchor y=992), creature **920×800** (foot
y=744), boss **1120×960**, item **512×512**, skill **256×256**, fx cell **128×128**. All transparent, @2x.

---

## 2. THE CHARACTER + ITEM CONTRACT (the important part)

The requirement: **whatever the player has equipped must appear worn on the champion** — on the Character
sheet (where they dress it) and in the fight. This is done with a **base body + one overlay per equipped
slot**, all drawn at the *same* rectangle so they register automatically. No per-item body art; no
combinatorial explosion.

### 2.1 How the layering works (already how your `overlay_*` files are built)

Every champion layer is authored on the **identical 800×1040 canvas with the feet at y=992**. The engine
picks the champion box, scales the **base** to fit (bottom-anchored to the foot line), then draws each
equipped slot's overlay into the **exact same box**. Because all layers share the canvas, they line up with
zero engine-side alignment math. A slot with nothing equipped simply draws no overlay.

**Draw order (back → front)** — this is what I will implement, so author the overlays to occlude correctly
in this order:

```
champion_<pose>_base
  → overlay_chest_<pose>      (torso armour, behind the arms)
  → overlay_boots_<pose>
  → overlay_gloves_<pose>
  → overlay_helm_<pose>
  → overlay_weapon_<pose>     (held in the forward hand)
  → overlay_ring_<pose>       (small; on the weapon hand)
  → overlay_charm_<pose>      (a hanging talisman on the chest/belt)
  → overlay_focus_<pose>      (the ability focus — a floating sigil/orb beside the head, drawn last/on top)
```

### 2.2 What "pose" means, and why I need BOTH poses for every slot

The champion has two poses: **`idle`** (Character sheet + between attacks) and **`attack`** (the lunge on
every skill). The two poses move the body, so an idle-drawn sleeve will not sit on an attacking arm.
Therefore **every slot needs an overlay for each pose it is visible in.** Right now the weapon has both
(`overlay_weapon_idle`, `overlay_weapon_attack`) — that is the model to follow for the rest. The armour/focus
overlays you shipped are idle-only, so during the attack lunge they currently cannot be drawn (see gap list).

### 2.3 Element / rarity expression on worn gear (design call — please confirm)

Item icons and worn overlays are authored **per slot type, not per item instance** — one `overlay_helm_idle`
serves every helm the player finds. The item's **rarity** is shown by its inventory **frame** (§3), and its
**element** can be expressed by an **engine-side colour tint** on the overlay (I can tint the worn overlay by
the item's Source colour so a Nature helm reads green, a Shadow helm violet — the six colours are already
defined). **Author overlays in neutral white/grey so the tint reads cleanly.** If you'd rather each element
have bespoke gear art, that multiplies the overlay count by six — tell me and I'll list that instead.

### 2.4 Item icons (§ item card)

Loot is a card: **`frame_<rarity>` drawn behind, `item_<type>` icon centred at 72% scale** (per your
manifest). The icon is generic per slot type; the frame carries the rarity. This is already how the eight
`item_*` icons + five `frame_*` frames are built — they drop straight in. Materials, chest, and core use
their own icons over a neutral frame.

---

## 3. What integrates NOW (100% present — no gaps)

These sets are complete in the pack and I am wiring them this pass:

- **8 gear icons** `item_{weapon,charm,focus,helm,chest,gloves,boots,ring}` + **5 frames** `frame_*`.
- **6 form glyphs** `form_{strike,volley,aura,trap,mark,morph}` (tinted in-engine per source).
- **6 source gems** `source_*` and **6 vow sigils** `vow_*` (all six ids match the catalog exactly).
- **4 material icons** `mat_*` (mapped in §5), **`chest_loot`**, **`core_hatch`**.
- **2 UI icons** `icon_gleam`, `icon_dust`.
- **7 FX strips** `fx_*_strip*` (slash/impact/bolt/aura/heal/shield/levelup).
- **Champion idle layering**: `champion_idle_base` + `overlay_{chest,gloves,helm,weapon,focus}_idle`.

---

## 4. Creatures — a model decision, then the list

The game mechanically has **6 sources × 5 roles = 30 distinct creatures** (a Nature Attacker and a Nature
Producer are different units with different stats and evolutions), plus **6 region bosses**. Your pack
shipped **6 per-source creatures** + 1 boss. So we must pick the model:

- **Model A — per source (6 total).** One creature look per element; the role is already shown by the role
  badge + tier on each chip. Cheapest. Your pack already fits this. **I will wire this now** (each source's
  creature shows for all its roles). *This is not substitution — there simply is one designated creature per
  element in this model.*
- **Model B — per (source, role) (30 total).** Every cell a distinct unit — the richest, matches the
  game's role differentiation, but 30 bespoke creatures to draw.

**My recommendation: ship Model A now, upgrade to Model B later if you want role-distinct units.** If you
choose B, author these keys (drop the trailing role word or keep it — tell me the exact names and I map them):

```
crea_body_atk    crea_body_def    crea_body_sup    crea_body_crf    crea_body_prd
crea_mind_atk    crea_mind_def    crea_mind_sup    crea_mind_crf    crea_mind_prd
crea_nature_atk  crea_nature_def  crea_nature_sup  crea_nature_crf  crea_nature_prd
crea_machine_atk crea_machine_def crea_machine_sup crea_machine_crf crea_machine_prd
crea_shadow_atk  crea_shadow_def  crea_shadow_sup  crea_shadow_crf  crea_shadow_prd
crea_spirit_atk  crea_spirit_def  crea_spirit_sup  crea_spirit_crf  crea_spirit_prd
```

**Region bosses (6 needed, 1 present).** The fight draws a per-region boss on boss waves. Present:
`boss_thorn_regent` (Verdant Hollow). **Missing 5** — author on the 1120×960 boss canvas:

```
boss_cinderworks      boss_umbral_reach      boss_the_pale_choir
boss_the_still_archive boss_marrow_wastes
```
(Names are placeholders keyed to region id — give each its own creature name and I'll map the file to the
region.)

---

## 5. Material name mapping (resolved)

Your four material files are thematically named; the code's four tiers are Scrap/Essence/Core/Crystal. I am
wiring this mapping (no rename needed on your side):

| Tier (code) | Salvaged from | Your file |
|---|---|---|
| Scrap | Common/Uncommon | `mat_shard` |
| Essence | Rare | `mat_essence` |
| Core | Epic | `mat_catalyst` |
| Crystal | Legendary | `mat_crystal` |

---

## 6. THE COMPLETE MISSING LIST (author these to finish the pack)

Ranked by how visible the gap is in play.

### 6.1 Character — attack-pose + remaining-slot overlays (HIGH — the emphasized feature)
The dressed champion is only fully correct in the **idle** pose today. To make equipped gear show in the
**fight** (which animates) and to cover all 8 slots, author:

| Need | Keys |
|---|---|
| Attack-pose overlays for the slots that have idle ones | `overlay_chest_attack`, `overlay_gloves_attack`, `overlay_helm_attack`, `overlay_focus_attack` |
| Boots overlay (both poses) | `overlay_boots_idle`, `overlay_boots_attack` |
| Ring overlay (both poses) | `overlay_ring_idle`, `overlay_ring_attack` |
| Charm overlay (both poses) | `overlay_charm_idle`, `overlay_charm_attack` |

*(If you add more champion poses later — e.g. a `hurt`/`downed` pose — each new pose needs the base + the
full overlay set for that pose.)*

### 6.2 Creatures / bosses (HIGH if Model B, else just bosses) — see §4
- Model B: 30 `crea_*` (24 beyond the 6 you shipped, if going per-role).
- Bosses: 5 region bosses (§4).

### 6.3 UI chrome (MEDIUM — currently procedural, only if you want a skinned UI)
The code can skin its panels/buttons/bars/keycaps if these exist; without them it draws the current clean
flat-shape UI (this is **not** a broken state — it's the intended greybox chrome). Author only if you want
the panels to carry the vector look:

```
ui_panel        ui_panel_gold      ui_button       ui_button_active
ui_keycap       ui_bar_frame       ui_bar_fill
```

### 6.4 Backgrounds (MEDIUM — the pack swap REMOVED the old ones)
The previous pack had backgrounds; this renewal dropped them, so every screen now shows a flat dark fill
where a backdrop used to be. That is graceful (no crash, never a borrowed image) but it's a look regression.
The code asks for these exact keys — author them in the vector style to restore the backdrops (16:9, they
fill the whole 1920×1080 frame):

```
bg_title          (title screen)          bg_forge          (the Forge)
bg_warren         (Warren + Build bench)   bg_regionmap      (world map)
bg_constellation  (Memory Dust)            bg_arena_<theme>  (the fight, one per element theme:
                                                              body mind nature machine shadow spirit)
```
The fight falls back to `bg_arena_verdant`-style per-theme art; if a theme's arena is absent it uses a flat
fill. `logo_title` — the title-screen logo — is also gone (currently a text title).

---

## 7. Integration status after this pass
- **Wired to new art:** gear icons, rarity frames, materials, chest/core, form/source/vow icons, gleam/dust
  icons, FX strips, champion **idle** layering (base + present overlays), per-source creatures (Model A),
  Verdant Hollow boss.
- **Still greybox (awaiting the §6 list):** champion attack-pose gear, boots/ring/charm overlays, the other
  5 region bosses, optional UI chrome, logo, backgrounds. Each stays its old flat shape — never a borrowed
  asset.
