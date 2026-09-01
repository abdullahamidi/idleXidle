# ADR-005 — UI SCALE is a density profile, the cursor is mapped once, and motion has one vocabulary

| Field | Value |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-01 |
| **Deciders** | technical-director + ui-programmer (autonomous session, per standing user instruction) |
| **Supersedes** | the scale model in ADR-003 (already Superseded) and UX V2's `UiKit.Page` shrink |
| **Related** | ADR-001 (Core owns the arithmetic; the Game assembly owns MonoGame types) |

## Context

The game renders a fixed 1920×1080 canvas, presents it aspect-fit into any window, and draws each menu
page through an inset matrix (`Game1.OverlayScale` × translate past the nav rail). Three things about that
pipeline were wrong going into the UI polish pass (brief: `production/audit/ui-polish/BRIEF.md`).

1. **UI SCALE was a zoom.** UX V2 shipped 100 / 125 % by multiplying the overlay matrix by the factor and
   shrinking `UiKit.Page` to 1920/f × 1080/f. Every screen laid out for 1080 px of height overflowed at
   150 % (GEAR's paper doll through its footer, TRAINING through its reset bar), so the step was withdrawn.
   A 1600×900 window "at 150 %" would have had 1067×600 of room; FORGE cannot fit in that at any scale.
2. **The cursor lived in three spaces.** The host floored the mouse to a 480×270 canvas, handed that to
   every screen, and each screen inverted the matrix itself (`Game1.ToOverlay`, `mouse * 4`): a ~4.5
   page-pixel hit grid, truncated toward zero, and one screen that forgot the conversion shipped with
   every button dead ("Stat upgrade tiklayamiyorum").
3. **Motion and interaction states were per screen.** Hover was an instant art swap, pressed did not
   exist, the only eased curve was the vault card's, and Reduced Motion had two consumers.

## Decision

### The profile is a density, not a zoom (brief §7–§11, LAW 7)

- `UiKit.Page` is 1920×1080 at every profile and `Game1.OverlayScale` is `BaseOverlayScale` at every
  profile. Top-level regions (navigation, header, main, inspector, footer) are page-anchored and do not
  move with the setting.
- `UiMetrics` (Game assembly) is the one source of scale-sensitive sizes: `Percent` (100 / 125 / 150),
  `TextScale` and `ControlScale` at the full factor, `SpacingScale` at half rate (1.125 / 1.25), and the
  scalers `Text()`, `Control()`, `Space()` plus named sizes (`RowHeight`, `ButtonHeight`, `IconSize`,
  `PanelPadding`, `Gap`, `ScrollbarWidth`, `HitTargetMinimum`, `InspectorWidth`). No screen writes
  `* 1.25f`; a screen names a base and asks for the profile's version.
- Every `UiTypography` rung is a profile-scaled property, and the panel grid is derived from the rungs it
  stacks. `SmoothFont` keeps rasterising at integer pixel sizes × a fixed density, so the atlas cache is
  bounded. Art geometry (the frame's corner reach, the button art's end cap) does not scale.
- A screen reflows to the bigger type: rows per panel from the height that is there; wheel-scrolled
  regions with `UiKit.ScrollBar` where the brief allows (inventories, bags, long inspectors, long lists);
  fewer columns or stacked controls at 150 %; never a smaller font to make it fit; primary actions stay
  anchored above any scroll region. Settings → UI SCALE is reachable at every profile.

### One cursor, mapped once (brief §12–§16, LAW 5, LAW 6)

- `Core.Presentation.PageFrame` is the exact inverse of the draw matrices in floating point:
  screen ↔ canvas ↔ page. It is tested (inverse exactness at six window sizes; a click half a screen pixel
  inside a drawn edge is inside; half a pixel outside is outside; a point left of the page never rounds
  onto its first column).
- `Game1.ReadCursor` maps the raw mouse once a frame into `PageCursor` (page space, floored once, toward
  negative infinity), `PageMouseF` (float), `ChromeMouse` / `ChromeMouseF` (true 1920). Inset menu screens
  receive `PageCursor`; the fight, the expedition log and the chest reveal receive `ChromeMouse`. No screen
  converts anything; the rectangle a control draws is the rectangle it hit-tests.
- The rig poses the cursor with `RH_SHOT_PAGE_MOUSE=x,y` (page space) or the historical `RH_SHOT_MOUSE`.

### One motion vocabulary (brief §30–§32)

- `UiMotion`: `Fast` 100 ms (hover, press, select), `Transition` 180 ms (equip, purchase, unlock, an
  inspector's new content, a number changing), `Reward` 350 ms (a chest, a capstone, a set completing, a
  shield breaking); one smoothstep; a keyed store of eased values and one-shot pulses with no per-frame
  allocation. Reduced Motion jumps eases to their target and keeps fades.
- `UiKit.Button` carries the standard states itself: hover eases in as a thin luminance lift over the art
  swap; pressed drops the face 2 px while the mouse is held; disabled keeps a readable label so a control
  can say why it is off (`ALREADY EQUIPPED IN SLOT 1`, `NEEDS 1 CRYSTAL`).

## Consequences

- **Gates changed.** `tools/check_mouse_space.py` now enforces the inverse rule (the host hands the right
  cursor; no screen scales or re-rounds it). `check_ui_type.py` and `check_page_anchors.py` are unchanged
  and pass. A Game test project exists (`tests/unit/IdleXIdle.Game.Tests`): the profile arithmetic, the
  ladder's order, the fixed page, the offered steps, and every static layout rectangle of every screen
  inside the page at all three profiles, by reflection.
- **What a screen author must know.** Name a base, ask `UiMetrics`; derive vertical rhythm from available
  height; a `static readonly Rectangle` that depends on a metric is frozen at class load and must be a
  property; strings only use the font gate's proven characters.
- **The rig.** A fight fixture pins its enemy baseline through the host (`_shotEnemyBaseline`) because the
  host's per-frame push restarts a DevStart run once; a posed seek rebuilds the wave's replay and lands two
  frames before the shutter (`HuntScreen.DevSeekBefore`); `RH_SHOT_DUMP=1` writes the wave beside the shot.
- **Cost.** Profile changes are one integer changing in one place; nothing rebuilds. The only new per-frame
  work is `UiMotion.Tick` over the (small, self-emptying) pulse store.

## Alternatives considered

- **Keep the zoom and finish the 150 % vertical pass.** Every screen would still have had to derive its
  rhythm from the height that is there, and the art (paper doll, map, tree) would have grown 1.5× for no
  reader's benefit. The density model does the same per-screen work and spends the page on text.
- **Scale text inside `SmoothFont` and leave the rung constants alone.** Cheaper to land, but ~40 layout
  sites compute from a rung (`(h - Body) / 2`, chip heights) and would have mis-centred at every profile.
  Scale-aware rung properties fix all ~930 uses at once; ten `const` and nine default-parameter sites
  became expressions.
- **Hand every screen a float cursor and `Rectangle.Contains(Vector2)`.** A floor to the page pixel is
  exact against a drawn integer rectangle's edge at every present scale, so integer hit-testing loses
  nothing; the float is kept (`PageMouseF`) for drags.
