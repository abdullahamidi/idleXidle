# IdleXIdle UX Screen Guide Standard

This document defines the reusable standard for every remaining IdleXIdle / IDLExIDLE screen guide.

It incorporates the implementation lessons learned from the Hunt screen and is intended to prevent asset misuse, layout ambiguity, rendering errors, invented data, and invalid fallback behavior.

The standard applies to:

1. Gear
2. Stats
3. Build
4. Forge
5. Warren
6. Map
7. Dust

The approved art direction remains the asset language established in Packages 01–09.

---

# 1. Required Deliverables Per Screen

Every screen must be completed in this order.

## 1.1 UX Reference Image

A polished 1920 × 1080 reference composition using the approved art direction.

It must demonstrate:

- layout hierarchy,
- panel weight,
- actual screen purpose,
- important states,
- intended asset placement,
- real navigation order,
- readable typography,
- no invented features.

The reference image is a target composition, not sufficient implementation documentation by itself.

## 1.2 Production Specification

A screen-specific `.md` file containing:

- exact component rectangles,
- asset IDs,
- rendering mode,
- nine-slice rules,
- safe padding,
- typography roles,
- data source,
- empty and locked states,
- overflow rules,
- z-order,
- interaction rules,
- approved fallback behavior,
- acceptance criteria.

## 1.3 Minimal Deterministic Fixture

Every screen must have at least one developer-controlled fixture that renders a known state.

Examples:

- Gear: fixed equipped loadout and selected inventory item
- Stats: fixed stat values and source alignment
- Build: fixed Source + Form + Vow setup
- Forge: fixed item, materials, and upgrade result
- Warren: fixed producers and automation levels
- Map: fixed unlocked/locked regions and selected region
- Dust: fixed Dust balance and upgrade list

A full QA framework is not required unless the screen needs it.

## 1.4 Visual Verification Screenshot

The implementation agent must provide a 1920 × 1080 screenshot of the deterministic fixture.

The screen is not ready for review without this screenshot.

---

# 2. Base Rendering Rules

```text
Logical canvas:        1920 × 1080
Primary render target: 1920 × 1080
Reference aspect:      16:9
```

Do not render the primary screen into a lower-resolution intermediate target.

Do not load preview, concept, contact-sheet, or source-reference assets at runtime.

---

# 3. Dependency Classification

Every dependency must be classified.

## 3.1 Blocking

The component must stop and report the missing input.

Typical blocking dependencies:

- missing animation metadata,
- missing actor visible bounds,
- invalid sprite sheet,
- missing required data model,
- missing screen state required for the component,
- missing clipping when content can escape its region.

## 3.2 Approved Fallback

Only explicitly documented fallbacks may be used.

Examples:

- no recent history data → real aggregate summary,
- no cooldown values → Ready/Casting/Recovering,
- no Form glyph → documented temporary label fallback,
- no secondary resource → a real secondary stat.

## 3.3 Cosmetic

May be deferred:

- small particles,
- hover polish,
- secondary transition effects,
- decorative animation.

The agent must not invent a fallback outside the approved fallback table.

---

# 4. Runtime Asset Whitelist

Allowed runtime roots may include:

```text
assets/
normalized/
frames_512/
frames_1024/
metadata/
runtime/
```

Forbidden runtime names and roots include:

```text
preview/
source_reference/
source_sheet/
concept/
contact_sheet/
runtime_assets_preview/
poster/
showcase/
mood_board/
pitchboard/
```

A forbidden runtime asset load is a development error.

---

# 5. Component Contract

Every major screen component must define:

```text
ID
Rectangle
Content-safe rectangle
Asset ID
Render mode
Nine-slice insets
Safe padding
Typography token
Data source
Empty state
Locked state
Disabled state
Loading state
Overflow behavior
Z-index
Interaction behavior
Blocking dependencies
Approved fallback
```

An asset name and destination rectangle alone are not enough.

---

# 6. Rendering Modes

Use one of:

```text
FixedSize
AspectFit
AspectFillCrop
NineSlice
Tile
NativeScale
AnimationStrip
PrimitiveSurface
```

## FixedSize

Use for icons and small glyphs.

## AspectFit

Use for item icons, portraits, and preview art that must remain fully visible.

## AspectFillCrop

Use for backgrounds and region previews.

## NineSlice

Use for scalable panels, tabs, and buttons.

Never stretch decorative corners.

## NativeScale

Use for actors and VFX with pivot and visible-bounds metadata.

## AnimationStrip

Use only with matching frame metadata.

## PrimitiveSurface

Use for quiet UI:

- dark scrims,
- shared rails,
- dividers,
- subtle selected rows,
- non-ornate containers.

Not every component should use an ornate PNG.

---

# 7. Panel Hierarchy

## Primary

Use for:

- screen identity,
- main selected item,
- important modal,
- active navigation state.

## Secondary

Use for:

- contextual details,
- comparison panels,
- upgrade details.

## Quiet

Use for:

- resource rows,
- filters,
- minor stats,
- navigation rail,
- metadata,
- compact status surfaces.

The content area must remain more important than decorative framing.

---

# 8. Nine-Slice and Padding

Nine-slice metadata must be centralized.

Provisional panel padding:

```text
Primary:
28, 24, 28, 24

Secondary:
22, 20, 22, 20

Quiet:
14, 10, 14, 10
```

Text and icons must not enter decorative borders.

---

# 9. Typography Tokens

Use centralized tokens rather than inline arbitrary sizes.

```csharp
public static class UiTypography
{
    public const int ScreenTitle     = 36;   // the screen's name, ceremony face
    public const int PrimaryValue    = 30;   // a headline number
    public const int PanelTitle      = 26;   // the title of a panel
    public const int Headline        = 24;   // the biggest thing INSIDE a panel
    public const int NavigationLabel = 21;   // a thing you click: nav tile, button label
    public const int Body            = 19;   // prose, list rows, the unsized default
    public const int Secondary       = 16;   // captions, column heads, gold sub-headings
    public const int Caption         = 14;   // chips and badges only -- never a sentence
}
```

> **Revised 2026-08-28.** The two title names were swapped against their roles: `SectionTitle` (26)
> was what every panel's title actually used, while `PanelTitle` (24) was the headline INSIDE a panel.
> They are `PanelTitle` and `Headline` now; the values did not change. `Label`, `OverlayBody` and
> `OverlayTitle` were three more names for sizes the ladder already had and are aliases now, and
> `Caption` was added at 14 to name the floor the arena had already drifted below (12 and 13).
> The arena's three combat callouts (34 / 40 / 46) are a family beside the ladder, not rungs on it.
> The live source is `src/IdleXIdle.Game/UiTypography.cs`; `tools/check_ui_type.py` enforces it.

Rules:

- body text at least 18 px (`Body` is 19; `Secondary` and `Caption` are for captions and tags, never prose),
- navigation labels at least 20 px,
- active screen title must not be ellipsized,
- use tabular numerals for values,
- long content wraps or uses a defined short format,
- never shrink below the minimum to force content into a panel.

## 9.1 Panel Grid

Every panel is the same nine-sliced frame, so every panel lays its header out the same way. The
numbers below are `UiTypography`'s, applied through `UiKit.TitleTop / CaptionTop / BodyTop /
ContentLeft / ContentRight`, which add what the SQUARE frame's deeper crest and side diamonds need
(`SquareFrameDrop`, 28 px) without the screen having to know which frame its rectangle selected.

```text
panel.Y  +22   PanelTitleTop      the panel's title
         +56   PanelCaptionTop    its one-line caption
         +92   PanelBodyTop       the first content row (title + caption)
         +62   PanelBodyTopBare   the first content row (title only)
         +44   ModalTitleTop      a modal's title -- it shares the close icon's row

left/right   40   PanelPadX        panels 512 px wide and up
             28   PanelPadNarrow   narrower plates and detail columns
bottom       32   PanelPadBottom
```

---

# 10. Asset-Specific Metadata

## 10.1 Actors

Actor assets require:

- pivot,
- visible bounds,
- ground anchor,
- UI anchor,
- optional attachment anchors.

## 10.2 Equipment

Equipment assets require:

- slot type,
- icon bounds,
- rarity frame,
- equipped state,
- compare state,
- optional character attachment point.

Approved equipment slots currently include:

```text
Head
Shoulder
Chest
Weapon
Neck
Ring
Trinket
Cloak
```

## 10.3 Items

Item icons must use `AspectFit` inside a slot.

Do not stretch item art to fill the slot.

## 10.4 Backgrounds

Use `AspectFillCrop`.

Do not downscale and enlarge repeatedly.

---

# 11. Data Honesty

Every visible value must come from a real presentation view model.

The agent must not invent:

- stats,
- cooldowns,
- loot history,
- currencies,
- equipment effects,
- crafting chances,
- upgrade costs,
- locked regions,
- production timers.

When data does not exist:

1. use a documented fallback,
2. hide the unsupported component,
3. or report a blocking data gap.

---

# 12. Dynamic Layout Rules

Every screen guide must define:

- zero items,
- one item,
- many items,
- long names,
- large numbers,
- locked items,
- disabled actions,
- empty filters,
- selected and unselected states,
- loading state,
- error state.

Do not design only the ideal full-data screenshot.

---

# 13. Overlay and Modal Policy

Only one major modal or overlay may be active.

A screen guide must define:

- priority,
- suppression,
- queue behavior,
- dismiss behavior,
- background dim,
- click/input capture.

Tooltips are local overlays and must not conflict with major modals.

---

# 14. Navigation Policy

Canonical order:

```text
Hunt
Gear
Stats
Build
Forge
Warren
Map
Dust
```

Use one shared quiet navigation rail.

Inactive items should not each use a full ornate frame.

Only the active screen receives strong decorative emphasis.

---

# 15. Deterministic Fixture Contract

Each fixture must define:

```text
Fixture ID
Screen
Selected tab
Selected item/entity
Exact values
Exact asset IDs
Locked/unlocked states
Active overlay
Expected actions
Expected visible components
Expected hidden components
```

The implementation agent must not choose arbitrary fixture content.

---

# 16. Minimal Debug Requirements

A full QA harness is not required for every screen.

Each screen must support at least one lightweight verification mode showing:

- component bounds,
- content-safe bounds,
- selected asset ID,
- render mode,
- text bounds,
- active state,
- collision warnings.

Screen-specific debug tools should be added only when necessary.

---

# 17. Acceptance Metrics

Every screen guide must include measurable requirements, such as:

- no sibling component overlap,
- no text inside border insets,
- no forbidden runtime asset,
- minimum font sizes,
- correct item icon aspect ratio,
- correct selected-state visibility,
- maximum one major modal,
- exact number of visible fixture items,
- no clipped primary action,
- no data invented.

---

# 18. Completion Response Format

The implementation agent must return:

```text
<Screen Name> UX Pass

Preflight:
PASS / FAIL

Implemented:
- ...

Blocking dependencies:
- None
or
- ...

Approved fallbacks:
- ...

Fixture:
PASS / FAIL

Screenshot:
<path or attachment>

Known cosmetic follow-ups:
- ...

Final status:
READY FOR VISUAL REVIEW / NOT READY
```

The agent must not call the screen complete when `Final status` is `NOT READY`.

---

# 19. Screen Production Order

We will proceed in the actual navigation order:

1. Gear
2. Stats
3. Build
4. Forge
5. Warren
6. Map
7. Dust

For each screen:

1. inspect the current repository behavior and backing data,
2. identify the real asset families,
3. create the UX reference,
4. review the reference,
5. write the production specification,
6. implement using a deterministic fixture,
7. compare the screenshot,
8. revise only where necessary.

---

# 20. Next Screen — Gear

The next screen is **Gear**.

The Gear UX guide must account for:

- Hunter presentation,
- equipped gear slots,
- inventory grid,
- rarity frames,
- selected-item details,
- compare state,
- equip/unequip behavior,
- loadout or preset behavior only if the repository actually supports it,
- empty inventory,
- locked slot,
- item filtering,
- item overflow,
- equipment layering on the Hunter where supported.

The guide must use the approved Package 01 UI assets, Package 02 Hunter assets, Package 05 item and rarity assets, and Package 08 glyphs.

No feature will be added to the Gear UX unless it is present in the game model or explicitly approved.
