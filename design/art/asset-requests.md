# Asset Requests — Deep Item Pool + Content Gaps

> Chosen item model (2026-07-28): **bespoke named types**. You author one art per named item type;
> code assigns a constant passive per type and generates 20+ variations from a prefix + random-attribute
> system. This document is the production list.

---

## 0. Read first — how art becomes an item

- **Format**: 512×512, transparent PNG. Icon fills ~72%, centred. A rarity frame is drawn *behind* it by
  code (don't bake a frame into the icon).
- **Style**: the locked rich-vector direction — jewel-tone gradient, faceted, rim light, no black outline
  (see `design/art/art-bible.md` and `design/art/equipment-design-language.md`).
- **Key = filename.** The engine loads each PNG under the key `Path.GetFileNameWithoutExtension().ToLower()`.
  So `elven_bow.png` → the code refers to it as `elven_bow`. Name files to the keys below.
- **Variations are DATA, not art.** "Furious Elven Bow" and "Immortal Elven Bow" share ONE Elven Bow art.
  You author **one art per named type**; the 20+ variations (prefix + rolled attributes) are generated in
  code. This is the whole point — a huge pool at 1/20th the art cost.

---

## 1. Weapons — the priority (bespoke named roster)

There are **4 weapon categories** today: `blade`, `bow`, `spear`, `scythe`. (Say if you want more — e.g.
mace, staff, dagger, gauntlet.)

**What to produce:** one 512×512 icon per **named weapon type**, filename `weapon_<snake_name>.png`
(e.g. `weapon_elven_bow.png`, `weapon_bone_scythe.png`). Recommend **~6 named types per category ≈ 24
weapons** to start; add more anytime.

**What I need with each:** the fantasy **name**, its **category**, and a one-line **feel** (piercing,
crushing, fast, brutal, protective, greedy…). I map the feel to a **constant passive** (the item's "soul")
and wire the prefix/attribute variation. Or just give names + art and let me assign passives.

Give me a table like:

| Type name | Category | Intended feel → I assign the passive |
|---|---|---|
| Elven Bow | bow | fast / precise → attack speed |
| Furyfang Blade | blade | brutal → attack power, more damage taken |
| Aegis Spear | spear | protective → defense |
| … | … | … |

> The existing 20 generic weapon thumbnails (`weapon_blade_01..05`, etc.) stay as the fallback until your
> bespoke arts land, so nothing looks broken in the meantime.

---

## 2. Every other slot — same logic

Apply the named-type model to all 8 slots. Produce bespoke named arts (512×512, key = filename):

| Slot | Filename key prefix | Feel / passive family | Notes |
|---|---|---|---|
| Charm (amulet/talisman) | `charm_<name>` | tough / vital / greedy / wild | |
| Focus (tome/orb/sigil) | `focus_<name>` | skill-speed / attuned | |
| Helm | `helm_<name>` | defense / utility | generic `helmet_01..06` exist as fallback |
| Chest | `chest_<name>` | defense / health | **fully missing** — only 3 named sets exist; needs the most |
| Gloves | `gloves_<name>` | crit / haste | `gloves_01..06` fallback |
| Boots | `boots_<name>` | evasion / haul | `boots_01..06` fallback |
| Ring | `ring_<name>` | crit / resonance / greed | uses `accessory_01..07` fallback |

Recommend ~4–6 named types per slot. **Chest is the biggest gap** (no thumbnail set at all).

---

## 3. Item support art — status (mostly complete)

| Asset | Have | Missing |
|---|---|---|
| Rarity frames `ui_frame_rarity_*` | 5 ✓ | — |
| Affix glyphs `affix_*` | 7 ✓ | — |
| Enchant glyphs `ench_*` | 11 ✓ | — |
| Source/element glyphs `source_*` | 6 ✓ | — |
| Category icons `category_icon_equipment_*` | 9 ✓ | — |
| Currency/material `currency_*` | gleam, dust, resonance_shard ✓ | 4 material *tiers* (Scrap/Essence/Core/Crystal) use tinted gems — bespoke icons optional |

> The code *also* looks for per-trait overlay glyphs `item_<slot>_<trait>` (e.g. `item_weapon_keen`) — **none
> exist** and it falls back. With the bespoke-named-type model you do **not** need these; listed for completeness.

---

## 4. Enemies

- **Bosses: complete.** 6 bosses (crystal_lich, forge_colossus, lumen_angel, spirit_matron, thorn_regent,
  void_reaper), each with idle/attack/hurt/special/binding — one per region. ✓
- **Standard mobs: partial.** ~15 enemy sets exist (nightstalker, oblivion_stalker, void_mage, wisp, …).
  The 6 regions each want **3 standard creatures** (an attacker, a support, a defender) themed to the
  region's element = **18 slots**. Please confirm the region→enemy mapping; produce themed art for any gaps.
  **Per mob, produce:** `<name>_idle_strip`, `<name>_attack_strip` (horizontal frame strips), a
  `<name>_die`/`hurt`, a `<name>_portrait`, a `<name>_ground_shadow`, and a `<name>_silhouette`.

Regions and their elements (for theming mobs): Verdant Hollow (Nature), Cinderworks (Machine),
Umbral Reach (Shadow), Marrow Wastes (Body), The Still Archive (Mind), The Pale Choir (Spirit).

---

## 5. Maps / regions

- **Region emblems** `icon_region_<region>`: only `verdant`, `cinderworks`, `umbral` are referenced;
  **`marrow_wastes`, `still_archive`, `pale_choir` are missing** (the Map screen falls back to a Source gem).
  Produce 3 emblems (matching the 3 that exist).
- **World map illustration** `bg_regionmap`: exists but is a dark stone texture, not a painted world map.
  An illustrated map (or per-region map tiles) would elevate the Map screen. Optional but high-impact.
- **Per-region Hunt backdrops** `bg_<region>`: confirm which exist; produce any missing so each region's
  fight has its own scene.

---

## 6. What I build in code (no art needed from you)

- A **named-type registry**: type name → art key → constant passive.
- **Prefix system**: the dominant rolled attribute picks a prefix — e.g. Power→"Furious", Health/Regen→
  "Immortal", Crit→"Vicious", Defense→"Warded", Haste→"Swift", Haul→"Greedy". (Final list TBD with you.)
- **Attribute pool** expansion + a deterministic per-item roll → 20+ variations per type.
- **Item naming**: `PREFIX ELEMENT TYPENAME` (e.g. "FURIOUS SHADOW ELVEN BOW").
- Wire it through loot drops, the Forge, and the Gear screen.

---

## 7. To start, send me

1. A **weapon roster** — names + category + one-line feel (even a rough ~20–30 names).
2. Whether to extend the same to other slots now or **weapons first**.
3. Confirmation of the **region→standard-enemy** theming, so I can name the mob gaps precisely.

I'll wire the passive-per-type + prefix + variation system as the art arrives, and each type immediately
yields its 20+ named variations.
