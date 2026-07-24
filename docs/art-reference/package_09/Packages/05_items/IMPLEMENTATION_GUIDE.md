# IMPLEMENTATION_GUIDE

## Package 05 — Items, Loot & Glyphs
This package contains import-ready item and loot assets for **Resonance Hunter**, targeting a default **1920x1080** presentation.

## Included content
- weapon item icons: spear, scythe, blade, bow (5 visual variants each)
- armor item icons: Shadow Warden, Verdant Guard, Iron Sentinel
- accessory icons: helmets, shoulders, gloves, boots, capes, accessories
- 11 enchant glyphs
- 6 Source gems
- affix and category glyphs
- 4 crafting materials
- 3 currencies
- loot chest and hatch core
- rarity frames
- 128x128 thumbnails for item grid usage

## Recommended runtime sizes
- inventory thumbnail: `64–96 px`
- selected item / tooltip art: `160–256 px`
- chest reveal / reward modal: `256–384 px`
- source / enchant glyph: `24–48 px` in cards, `64–96 px` in detail panels

## Item composition order
For a wearable item card, draw in this order:
1. rarity frame
2. item icon
3. Source gem or Source-colored corner pip
4. enchant glyph (Rare+ only)
5. stack count / item level / lock indicator
6. hover / selected highlight

## Folder usage
### `assets/items`
The main 512x512 item art. Use for tooltip panels, comparison screens and reward reveals.

### `assets/items/thumbnails`
Pre-scaled 128x128 versions. Use for inventory grids and lists to avoid runtime downscaling cost.

### `assets/glyphs/enchant`
One shared glyph per enchant. Do not bake the enchant into every item icon; overlay the glyph in code.

### `assets/glyphs/source`
Use `source_body`, `source_mind`, `source_nature`, `source_machine`, `source_shadow`, `source_spirit` for region/element identity.

### `assets/frames`
Rarity frames. Recommended mapping:
- Common → `ui_frame_rarity_common`
- Uncommon → `ui_frame_rarity_uncommon`
- Rare → `ui_frame_rarity_rare`
- Epic → `ui_frame_rarity_epic`
- Legendary → `ui_frame_rarity_legendary`

## Enchant mapping
- `ench_splinter` — loot burst / shatter
- `ench_harvest` — extra core / growth
- `ench_venom` — damage over time
- `ench_undying` — fatal-hit protection
- `ench_desperation` — low-health power
- `ench_siphon` — leech / drain
- `ench_overdraw` — extra projectile
- `ench_linger` — extended duration
- `ench_radiance` — faster aura pulse
- `ench_execute` — finisher
- `ench_coiled` — faster trap reset

## MonoGame loading
Use the normal Content Pipeline or load PNGs directly as `Texture2D`. Recommended sampler:
- `PointClamp` for small inventory icons
- `LinearClamp` for enlarged tooltip art

## Example draw flow
```csharp
spriteBatch.Draw(rarityFrame, itemRect, Color.White);
spriteBatch.Draw(itemTexture, itemRect, Color.White);

if (sourceGlyph != null)
{
    spriteBatch.Draw(sourceGlyph, sourceRect, Color.White);
}

if (enchantGlyph != null)
{
    spriteBatch.Draw(enchantGlyph, enchantRect, Color.White);
}
```

## Pivot and bounds
All main item icons are centered in a transparent 512x512 canvas. Use `origin = texture.Bounds.Center.ToVector2()` when animating or rotating an item reveal.

## Tinting
The included art is already colored. Use `Color.White` by default. For disabled/locked items, multiply with a neutral gray; avoid strong Source tinting over the full icon because it can flatten the existing material highlights.

## Inventory performance
Prefer the prepared 128x128 thumbnails for large grids. Load 512x512 versions only for the selected item or reward reveal.

## Notes
- Weapon and wearable icons are standardized from the approved Hunter art direction, giving visual consistency between equipped gear and inventory art.
- Enchant, Source, currency and material glyphs are modular overlays intended to prevent combinatorial asset growth.
