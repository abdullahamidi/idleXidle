using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Traits;

namespace IdleXIdle.Game;

/// <summary>
/// THE TRAITS SCREEN: three worn characteristics, the collection you have awakened, and the ones you
/// have not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a tree, and not an achievement list</b> (BRIEF §37, §90). There is no node graph, no
/// currency, no cost, no percentage, no counter and no progress bar anywhere on this screen — an
/// undiscovered trait is a dim sigil and the word <c>???</c>, and its view model literally carries
/// nothing else, so there is no field a tooltip COULD leak (LAW 8). The mental model is: this hunter,
/// the three it wears, what the account has awakened, and the mystery of what it has not.
/// </para>
/// <para>
/// <b>The sigils are drawn, not authored.</b> Twenty-six glyphs plus an unknown mark is a lot of art
/// to commission for tiles this size, and this project's standing rule is that new art goes through
/// the pipeline and the arena contract rather than piecemeal. A constellation seeded from the trait's
/// own id is deterministic (the same trait always draws the same stars), scale-free, costs no asset,
/// and is exactly the "subtle star/constellation treatment" §39 asks for. If a glyph family is
/// authored later, only <see cref="Sigil"/> changes.
/// </para>
/// <para>
/// <b>Every rectangle is arithmetic</b> off <see cref="UiKit.Page"/> and <see cref="UiMetrics"/>, and
/// the grid SOLVES for its cell size rather than declaring one: the page is 1920×1080 at every UI
/// SCALE (ADR-005 — the profile is density, not zoom), so at 150 % the type is half again as large in
/// the same space and a fixed tile size would push the last row off the panel. Columns come from the
/// width, rows from the count, and the tile is whatever is left.
/// </para>
/// </remarks>
public sealed class TraitCollectionScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;

    /// <summary>An unknown sigil's ink — dim, but never absent. A mystery must still read as a shape.</summary>
    private static readonly Color Unknown = new(0x6A, 0x64, 0x80);

    // ── DRAG AND DROP ────────────────────────────────────────────────────────────────────────────
    //
    // Playtest 2026-09-09: "centre the trait slots, and add drag-and-drop placement." Wearing a trait
    // was select-then-click-a-slot, which is two gestures for one intent and gives no answer to "which
    // of the three am I replacing" until after it has happened. Dragging answers it while the hand is
    // still moving: the slot under the cursor lights, and letting go is the commit.
    //
    // The click path is UNTOUCHED. It is the keyboard-and-one-button path, it is what the tour teaches,
    // and a drag that replaced it would take the screen away from anyone who does not drag. The same
    // ledger call is behind both.
    //
    // The lifecycle mirrors LoadoutScreen's carry exactly — press, slop, move, release — because a
    // second grammar for the same gesture in the same game is how one of them ends up subtly wrong.

    /// <summary>How far the pointer must travel before a press becomes a drag rather than a click.</summary>
    private const int DragSlop = 4;

    /// <summary>The trait in hand, or null. Set on the press, read by the ghost, spent on the release.</summary>
    private string? _carryId;

    /// <summary>Which worn slot it came out of, or -1 when it came from the collection.</summary>
    private int _carryFromSlot = -1;

    private Point _carryFrom;
    private Point _carryAt;
    private bool _carryMoved;
    private bool _wasHeld;

    /// <summary>Is a trait actually in flight? A press that never moved is still a click.</summary>
    private bool Dragging => _carryId is not null && _carryMoved;

    /// <summary>A slot that has just taken a trait — the one-shot the drop plays on it.</summary>
    private static int TraitSlotKey(int slot) => HashCode.Combine("traits.slot.set", slot);

    private readonly UiKit _ui;

    /// <summary>The trait the inspector is reading, or "" for none. An UNKNOWN slot selects as "?<index>".</summary>
    private string _selected = "";

    /// <summary>The first visible ROW of the collection when the page cannot hold every sigil (UI SCALE 150).</summary>
    private int _scroll;

    /// <summary>
    /// THE SCROLL, CLAMPED ON EVERY READ — the one reading <see cref="Cell"/> and <see cref="OnPage"/>
    /// position from, and therefore the one both halves of the frame agree on.
    /// </summary>
    /// <remarks>
    /// A CLAMP IS A READ HERE AND A WRITE ONLY IN UPDATE. <see cref="UpdateCollection"/> owns the field
    /// (<c>UiKit.Scrolled</c> is both its notch and its clamp), but Game1's Update returns before this
    /// screen while a host modal is up and the board is still PAINTED behind that modal — so a profile
    /// change made from inside Settings would otherwise leave the field out of range for as long as the
    /// panel stayed open, and the grid behind it would position its rows off the page. Reading through a
    /// clamp costs nothing and removes the whole class. Same shape as WarrenScreen's <c>GridScroll</c>
    /// and TrainingScreen's locally-clamped first line, both migrated in this pass.
    /// </remarks>
    private int ScrollAt(int visible, int rowsAll) => Math.Clamp(_scroll, 0, Math.Max(0, rowsAll - visible));

    private string? _cue;

    public TraitCollectionScreen(UiKit ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
    }

    /// <summary>True when something the save cares about changed this frame — the host writes to disk.</summary>
    public bool Dirty { get; private set; }

    /// <summary>The sound the host should play, cleared by reading. The screen owns no audio.</summary>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    /// <summary>Pin the inspector on a trait — the capture rig's one dial for this screen.</summary>
    public void DevSelect(string id) => _selected = id;

    /// <summary>
    /// Pin the inspector on an UNDISCOVERED tile, so the `???` reading can be photographed.
    /// </summary>
    /// <remarks>
    /// A state no dial can pose has never been looked at, and this is one of them: the unknown
    /// reading is only reachable by clicking a tile, and a capture never clicks. It takes the first
    /// undiscovered position in catalogue order, and a position is all it takes — the unknown branch
    /// of the inspector is handed an index and never an id, which is what makes LAW 8 structural
    /// rather than a promise.
    /// </remarks>
    /// <param name="ledger">The account, to find the first tile that has not awakened.</param>
    public void DevSelectUnknown(TraitLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        for (var i = 0; i < TraitCatalogue.All.Count; i++)
            if (!ledger.Has(TraitCatalogue.All[i].Id)) { _selected = UnknownKey(i); return; }
    }

    /// <summary>What the host asks for when a tour card wants to light a region of this screen.</summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.TraitSlots => new[] { WornStrip },
        TourTarget.TraitCollection => new[] { CollectionArea },
        TourTarget.TraitDetail => new[] { Inspector },
        _ => Array.Empty<Rectangle>(),
    };

    // ── GEOMETRY. Every rectangle below is derived; there is not one literal row or control size. ──

    private const int Margin = 24;

    /// <summary>Where the screen's name sits — the same 24 every screen in the game uses.</summary>
    private const int ScreenTitleTop = 24;

    /// <summary>The gold rule under the screen's name, and how thick it is.</summary>
    private static int RuleY => ScreenTitleTop + UiTypography.Pitch(UiTypography.ScreenTitle) + 1;

    private static int RuleH => UiMetrics.Control(3);

    /// <summary>
    /// Where the panels start: under the name, its rule and its subtitle — DERIVED, not a constant.
    /// </summary>
    /// <remarks>
    /// It was 150, which is a promise about one font at one UI SCALE. The screen title and the caption
    /// both grow at 125 and 150 % while a constant does not, so the panels would have climbed into the
    /// caption exactly where nobody had looked.
    /// </remarks>
    private static int PanelTop =>
        Math.Max(RuleY + RuleH + UiMetrics.Space(7) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(16),
                 UiKit.PageTop);   // and never above the page's own top, where the hint slot hangs

    private static int Gap => UiMetrics.Gap;

    /// <summary>The reading panel on the right — the house's own inspector width.</summary>
    private static Rectangle Inspector
    {
        get
        {
            var w = UiMetrics.InspectorWidth(UiKit.Page.Width - Margin * 2);
            return new Rectangle(UiKit.PageRight(Margin) - w, PanelTop, w, UiKit.PageBottom(Margin) - PanelTop);
        }
    }

    /// <summary>The collection panel on the left — everything the inspector does not take.</summary>
    private static Rectangle Board =>
        new(Margin, PanelTop, Inspector.X - Gap - Margin, UiKit.PageBottom(Margin) - PanelTop);

    /// <summary>The three worn slots, as one strip across the top of the board.</summary>
    private static Rectangle WornStrip
    {
        get
        {
            var b = Board;
            var top = UiKit.BodyTopBare(b);
            return new Rectangle(UiKit.ContentLeft(b), top, UiKit.ContentRight(b) - UiKit.ContentLeft(b), WornSide);
        }
    }

    /// <summary>A worn slot's side. The loudest tile on the screen, so the biggest.</summary>
    private static int WornSide => UiMetrics.Control(104);

    /// <summary>One worn slot's square, counted from the left of the CENTRED block of three.</summary>
    /// <remarks>
    /// <b>Centred since 2026-09-09</b>, on the designer's call: <i>"centre the trait slots on the trait
    /// screen."</i> They were pinned hard left with a note explaining why — the eye read the title and
    /// then crossed 420 px of black to find its subject. That reasoning was sound and the fix was the
    /// wrong one: the title moved instead. It is centred over the block now, so the two are one shape
    /// and there is no gap between them to travel.
    /// </remarks>
    private static Rectangle WornSlot(int i)
    {
        var strip = WornStrip;
        var blockW = TraitCatalogue.SlotsPerCharacter * WornSide + (TraitCatalogue.SlotsPerCharacter - 1) * Gap;
        var x = strip.X + (strip.Width - blockW) / 2;
        return new Rectangle(x + i * (WornSide + Gap), strip.Y, WornSide, WornSide);
    }

    /// <summary>
    /// The foot of the worn strip INCLUDING the names under the slots, which are drawn below it.
    /// </summary>
    /// <remarks>
    /// <see cref="WornStrip"/> is only as tall as the squares, so anything measuring from its bottom
    /// is measuring from above the names. At UI SCALE 150 that put the collection's caption inside the
    /// band the worn names occupy — they missed each other only because one is centred over the slots
    /// and the other is hard left, which is not a layout, it is luck.
    /// </remarks>
    private static int WornBlockBottom =>
        WornStrip.Bottom + UiMetrics.Space(4) + UiTypography.Pitch(UiTypography.Caption);

    /// <summary>The caption over the collection, and the rule it hangs under.</summary>
    private static int CollectionCaptionY =>
        WornBlockBottom + UiMetrics.Space(12) + UiTypography.Pitch(UiTypography.SectionLabel);

    /// <summary>Everything the grid of sigils may use.</summary>
    private static Rectangle CollectionArea
    {
        get
        {
            var b = Board;
            var top = CollectionCaptionY + UiTypography.Pitch(UiTypography.SectionLabel) + UiMetrics.Space(6);
            return new Rectangle(UiKit.ContentLeft(b), top,
                                 UiKit.ContentRight(b) - UiKit.ContentLeft(b),
                                 Math.Max(1, UiKit.ContentBottom(b) - top));
        }
    }

    /// <summary>
    /// The grid, SOLVED rather than declared: columns from the width, rows from the count, and the
    /// cell is what is left.
    /// </summary>
    /// <remarks>
    /// This is the one piece of arithmetic that has to be right at 100, 125 and 150 %. The page is the
    /// same size at all three and only the type and the controls grow, so a fixed tile size that fits
    /// at 100 % pushes the last row through the panel's foot at 150 %. Columns are clamped to a
    /// sensible band so a very wide page does not lay twenty-six sigils out in one thin line.
    /// </remarks>
    private (int Cols, int Rows, int CellW, int CellH, int Visible) Grid(int count)
    {
        var area = CollectionArea;
        count = Math.Max(1, count);

        // SOLVED BY TRYING EVERY COLUMN COUNT AND KEEPING THE BEST TILE. The first version derived
        // the columns from the WIDTH alone (area.Width / Control(112)), which is exactly backwards:
        // at UI SCALE 150 that gives FEWER columns, so MORE rows, while the caption under each tile
        // is half again as tall — the rows overlapped and the last one fell through the panel's foot.
        // Photographed at 150 %, which is the only way that was ever going to be found.
        //
        // Width and height pull opposite ways here (more columns is narrower cells but fewer rows),
        // so there is no formula to derive it from one axis. Twenty-six candidates is nothing.
        var best = 0;
        var bestSide = int.MinValue;
        for (var cols = 4; cols <= 9 && cols <= count; cols++)
        {
            var rows = (count + cols - 1) / cols;
            var side = Math.Min(area.Width / cols - Gap, area.Height / rows - CaptionBlock);
            // Ties go to FEWER columns, which is the larger cell — strictly greater keeps the first.
            if (side <= bestSide) continue;
            bestSide = side;
            best = cols;
        }
        if (best == 0) best = Math.Min(9, count);

        if (bestSide >= MinTile)
        {
            var chosenRows = Math.Max(1, (count + best - 1) / best);
            return (best, chosenRows, area.Width / best, area.Height / chosenRows, chosenRows);
        }

        // OVERFLOW. At UI SCALE 150 no column count holds twenty-six sigils with a two-line caption at
        // a legible size: the best the solver could do was a 72 px tile with UNDISCOVERED running into
        // its neighbour (release polish 2026-09-05). So the grid keeps a legible tile and a cell wide
        // enough for its widest caption word, and SCROLLS BY ROWS — the GEAR bag's contract — with a
        // lane for the bar inside the content edge.
        var lane = UiMetrics.ScrollbarWidth + UiMetrics.Space(8);
        var width = Math.Max(1, area.Width - lane);
        var minCellW = _ui.MeasureBig("UNDISCOVERED", UiTypography.Caption) + UiMetrics.Space(12);
        var lanes = Math.Clamp(width / Math.Max(1, minCellW), 3, 9);
        var cellW = width / lanes;
        var tileSide = Math.Max(1, Math.Min(cellW - Gap, MinTile + UiMetrics.Space(16)));
        var cellH = tileSide + CaptionBlock;
        var rowsAll = Math.Max(1, (count + lanes - 1) / lanes);
        var visible = Math.Clamp(area.Height / cellH, 1, rowsAll);
        return (lanes, rowsAll, cellW, cellH, visible);
    }

    /// <summary>The smallest sigil tile the grid will draw before it chooses to scroll instead.</summary>
    private static int MinTile => UiMetrics.Control(64);

    /// <summary>
    /// What a cell must keep under its tile: the trait's name, the WORN line, and a breath.
    /// </summary>
    /// <remarks>
    /// Named because <see cref="Grid"/> and <see cref="Tile"/> must reserve the SAME height. They did
    /// not, and the row's names landed on the next row's sigils at UI SCALE 150.
    /// </remarks>
    private static int CaptionBlock => UiTypography.Pitch(UiTypography.Caption) * 2 + UiMetrics.Space(6);

    /// <summary>
    /// The cell one entry of the collection occupies — by its row LESS the scroll, so a row above the
    /// first visible one lands above the area.
    /// </summary>
    /// <remarks>
    /// ONE GEOMETRY, NOT TWO: <see cref="UpdateCollection"/> hit-tests the rectangle this returns and
    /// <see cref="DrawCollection"/> paints it. It takes the SOLVED columns and cell size rather than the
    /// count, so neither half re-solves <see cref="Grid"/> once per index — which is also what stops the
    /// overflow profile measuring a caption twenty-six times per pass.
    /// </remarks>
    private Rectangle Cell(int index, int cols, int cellW, int cellH, int visible, int rowsAll)
    {
        var area = CollectionArea;
        var scroll = ScrollAt(visible, rowsAll);
        return new Rectangle(area.X + index % cols * cellW, area.Y + (index / cols - scroll) * cellH, cellW, cellH);
    }

    /// <summary>Is this entry on the page the scroll has moved to? A tile that is not drawn is not clickable.</summary>
    /// <remarks>
    /// Lifted out of DrawCollection's paint loop so the input half asks the window question in exactly
    /// the same words. Two copies of one scroll window is how a click lands on the tile above the one
    /// the player can see, and it fails silently.
    /// </remarks>
    private bool OnPage(int index, int cols, int visible, int rowsAll)
    {
        var row = index / cols;
        var scroll = ScrollAt(visible, rowsAll);
        return row >= scroll && row < scroll + visible;
    }

    /// <summary>The square inside a cell the sigil is drawn in — the rest of the cell is its name.</summary>
    private static Rectangle Tile(Rectangle cell)
    {
        // NO FLOOR. A Math.Max floor here is what made the rows overlap: the grid was told the tile
        // would fit and the tile then drew larger than the cell it was given. The grid now chooses
        // the columns that make the tile big enough, so this simply honours the cell it is handed.
        var side = Math.Max(1, Math.Min(cell.Width - Gap, cell.Height - CaptionBlock));
        return new Rectangle(cell.X + (cell.Width - side) / 2, cell.Y, side, side);
    }

    /// <summary>The inspector's one button — WEAR IT / TAKE IT OFF, or the reason it is off.</summary>
    private static Rectangle ActionButton
    {
        get
        {
            var p = Inspector;
            var w = UiKit.ContentRight(p) - UiKit.ContentLeft(p);
            return new Rectangle(UiKit.ContentLeft(p),
                                 UiKit.ContentBottom(p) - UiMetrics.ButtonHeight,
                                 w, UiMetrics.ButtonHeight);
        }
    }

    // ── INPUT. Every edge is consumed HERE, never in a draw pass ─────────────────────────────────

    /// <summary>
    /// Answer the screen's input: the drag, the three worn slots, the collection, the one button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DRAW MUST NOT CONSUME INPUT.</b> All of this lived inside <see cref="Draw"/> until
    /// 2026-09-12. MonoGame's fixed timestep makes AT LEAST ONE call to Update and exactly one to Draw
    /// per tick, so a frame over budget runs Update TWICE and Draw once: the first Update latches the
    /// click edge, the second recomputes it FALSE, and the single Draw that follows hit-tests an edge
    /// that is already gone. A press held three to six frames never re-arms — the click is silently
    /// dropped, which is not theoretical: the title screen shipped with exactly that bug. The drag was
    /// the worse half, because a release resolved from a draw pass EQUIPPED a trait — and a draw pass
    /// must be safe to run three times over with no Update between it.
    /// </para>
    /// <para>
    /// <b>One geometry, not two.</b> Every rectangle hit-tested here is solved by the same member the
    /// paint calls — <see cref="WornSlot"/>, <see cref="Cell"/> behind <see cref="OnPage"/>,
    /// <see cref="ActionButton"/> — so the two halves cannot drift apart. A copied rectangle fails
    /// silently, which is worse than the bug this fixes.
    /// </para>
    /// <para>
    /// The order is the order the Draw halves ran in, deliberately: the carry first (a release is spent
    /// before anything can pick a new trait up), then the worn strip, then the collection, then the
    /// inspector's button. <see cref="Dirty"/> is cleared at the TOP of this method, so the host must
    /// read it — and <see cref="ConsumeCue"/> — immediately after THIS call and never at the foot of
    /// Draw: on a catch-up tick the second Update would wipe the flag before a draw-side read saw it.
    /// </para>
    /// </remarks>
    /// <param name="ledger">The account's ledger. The screen reads and equips; it never discovers.</param>
    /// <param name="characterId">Whose three slots these are — the loadout is per champion (§26).</param>
    public void Update(TraitLedger ledger, string characterId, Point mouse, bool clicked, int wheel = 0)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        Dirty = false;

        // ── THE CARRY, RESOLVED BEFORE ANYTHING ELSE. ────────────────────────────────────────────
        //
        // The drop is hit-tested against WornSlot and CollectionArea, which are arithmetic and do not
        // need the frame to have been drawn — so the release is spent on the tick the button came up,
        // rather than being latched for a later draw to find. A latched click that no draw spends is
        // this codebase's own bug species, and it is exactly what the chest reveal had to be fixed for.
        var worn0 = ledger.LoadoutOf(characterId);
        var held = UiKit.MouseHeld;
        var released = _wasHeld && !held;
        _wasHeld = held;
        _carryAt = mouse;
        if (_carryId is not null && held
            && (Math.Abs(mouse.X - _carryFrom.X) > DragSlop || Math.Abs(mouse.Y - _carryFrom.Y) > DragSlop))
            _carryMoved = true;
        if (released || !held)
        {
            if (released && _carryId is { } dropped && _carryMoved) DropTrait(ledger, characterId, dropped, _carryFromSlot, mouse);
            _carryId = null;
            _carryFromSlot = -1;
            _carryMoved = false;
        }
        var pressed = held && _carryId is null && !released;

        // ── A POSED DRAG, for the shutter. ───────────────────────────────────────────────────────
        //
        // A drag is a state the capture rig cannot reach — it has no hands — and a state no capture can
        // pose is a state nobody has ever looked at. RH_SHOT_TRAIT_DRAG=<slot> holds the first awakened
        // trait over that worn slot, already past the slop, so the ghost, the lit target and the dimmed
        // tile it came from are all on one frame. It poses AFTER the lifecycle and BEFORE the slots
        // answer, which is exactly where it sat inside the old Draw.
        if (Environment.GetEnvironmentVariable("RH_SHOT_TRAIT_DRAG") is { Length: > 0 } dragTo
            && int.TryParse(dragTo, out var dragSlot))
        {
            var pick = TraitCatalogue.All.FirstOrDefault(d => ledger.Has(d.Id) && !worn0.Contains(d.Id, StringComparer.Ordinal));
            if (pick is not null)
            {
                _carryId = pick.Id;
                _carryFromSlot = -1;
                _carryMoved = true;
                _carryAt = WornSlot(Math.Clamp(dragSlot, 0, TraitCatalogue.SlotsPerCharacter - 1)).Center;
            }
        }

        // THE LOADOUT IS READ ONCE here and handed to the halves, as the old Draw read it once and
        // handed it down. TraitLedger.LoadoutOf returns the champion's LIVE row, so a swap in the worn
        // strip is already visible to the inspector's WEAR IT / TAKE IT OFF below — re-reading between
        // the halves would change what one click does.
        var worn = ledger.LoadoutOf(characterId);
        UpdateWorn(ledger, characterId, worn, mouse, clicked, pressed);
        UpdateCollection(ledger, mouse, clicked, wheel, pressed);
        UpdateInspector(ledger, characterId, worn, mouse, clicked);
    }

    /// <summary>
    /// Put the trait in hand where it was dropped: into a worn slot, or back into the collection.
    /// </summary>
    /// <remarks>
    /// The same <see cref="TraitLedger.EquipInto"/> the click path calls, so the two gestures cannot
    /// diverge. A drop that lands on nothing is a CANCEL rather than an unequip — letting go over empty
    /// space is what a person does when they change their mind, and reading it as "take it off" would
    /// make the gesture unsafe. Taking one off is its own deliberate drop, back onto the collection it
    /// came from.
    /// </remarks>
    private void DropTrait(TraitLedger ledger, string characterId, string traitId, int fromSlot, Point at)
    {
        for (var i = 0; i < TraitCatalogue.SlotsPerCharacter; i++)
        {
            if (!WornSlot(i).Contains(at)) continue;
            if (ledger.EquipInto(characterId, i, traitId))
            {
                Dirty = true;
                _selected = traitId;
                _cue = "sfx_trait_lit";
                UiMotion.Flash(TraitSlotKey(i), UiMotion.Reward);
            }
            else _cue = "sfx_error";
            return;
        }

        // BACK INTO THE COLLECTION IS HOW YOU TAKE ONE OFF. It is the only unequip gesture on the
        // screen — the click path has none, because a click on a worn slot READS it — and it is
        // deliberate enough not to fire by accident.
        if (fromSlot >= 0 && CollectionArea.Contains(at) && ledger.Unequip(characterId, traitId))
        {
            Dirty = true;
            _cue = "sfx_click";
        }
    }

    /// <summary>The three worn slots: a press lifts one, a click swaps into it or reads it.</summary>
    /// <remarks>
    /// The squares come from <see cref="WornSlot"/>, the one solve <see cref="DrawWorn"/> paints, and
    /// the three are walked in the order they are drawn in — so a swap into slot 0 is visible to slot 1
    /// on the same frame, exactly as it was when the click sat inside the paint loop.
    /// </remarks>
    private void UpdateWorn(TraitLedger ledger, string characterId, IReadOnlyList<string> worn,
                            Point mouse, bool clicked, bool pressed)
    {
        for (var i = 0; i < TraitCatalogue.SlotsPerCharacter; i++)
        {
            var slot = WornSlot(i);
            var id = i < worn.Count ? worn[i] : null;
            var hot = slot.Contains(mouse);

            // A PRESS ON A WORN SLOT PICKS IT UP. It only becomes a drag once the pointer travels
            // (DragSlop); short of that the click below still reads the slot, which is what it has
            // always done.
            if (pressed && hot && id is not null)
            {
                _carryId = id;
                _carryFromSlot = i;
                _carryFrom = mouse;
                _carryMoved = false;
            }

            if (UiKit.ClickedIn(slot, mouse, clicked))
            {
                // A CLICK ON A SLOT: put the selected trait in it if it is a discovered one that is
                // not already worn — that is the swap when all three are full, and the reason there is
                // no separate "replace which?" question anywhere on this screen. Otherwise it simply
                // reads what is in the slot.
                if (TraitCatalogue.Find(_selected) is { } pick && ledger.Has(pick.Id)
                    && !ledger.IsEquipped(characterId, pick.Id))
                {
                    if (ledger.EquipInto(characterId, i, pick.Id)) { Dirty = true; _cue = "sfx_trait_lit"; }
                }
                else if (id is not null)
                {
                    _selected = id;
                    _cue = "sfx_click";
                }
            }
        }
    }

    /// <summary>The collection grid: the wheel and its clamp, a press that lifts a tile, a click that reads one.</summary>
    /// <remarks>
    /// The wheel comes FIRST, as it did inside the old Draw, so a notch and a click on the same frame
    /// hit-test the page the notch moved to. <see cref="UiKit.Scrolled"/> is also the CLAMP, and a clamp
    /// is a state write — the second reason that line could not stay in a draw pass. Its gate is
    /// <see cref="Board"/> rather than <see cref="CollectionArea"/>, so wheeling over the worn strip
    /// still scrolls the grid: pre-existing, moved verbatim rather than tightened.
    /// </remarks>
    private void UpdateCollection(TraitLedger ledger, Point mouse, bool clicked, int wheel, bool pressed)
    {
        var all = TraitCatalogue.All;
        var (cols, rowsAll, cellW, cellH, visible) = Grid(all.Count);
        _scroll = UiKit.Scrolled(_scroll, wheel != 0 && Board.Contains(mouse) ? Math.Sign(wheel) : 0, visible, rowsAll);
        for (var i = 0; i < all.Count; i++)
        {
            // A TILE THAT IS NOT DRAWN IS NOT CLICKABLE — the same window DrawCollection paints through.
            if (!OnPage(i, cols, visible, rowsAll)) continue;
            var def = all[i];
            var known = ledger.Has(def.Id);
            var cell = Cell(i, cols, cellW, cellH, visible, rowsAll);
            var hot = cell.Contains(mouse);

            // A PRESS ON AN AWAKENED TILE PICKS IT UP. An undiscovered one cannot be carried — there is
            // nothing to carry — and the click below still selects it to read.
            if (pressed && hot && known)
            {
                _carryId = def.Id;
                _carryFromSlot = -1;
                _carryFrom = mouse;
                _carryMoved = false;
            }

            if (UiKit.ClickedIn(cell, mouse, clicked))
            {
                _selected = known ? def.Id : UnknownKey(i);
                _cue = "sfx_click";
            }
        }
    }

    /// <summary>The inspector's one button — WEAR IT / TAKE IT OFF. Nothing else on that panel clicks.</summary>
    /// <remarks>
    /// The two readings that paint NO button refuse here in the SAME order <see cref="DrawInspector"/>
    /// returns in: an unknown tile (it holds a position, never an id — LAW 8), and a selection the
    /// ledger has not awakened. The third refusal — all three slots full — is the same <c>enabled</c>
    /// expression the paint hands <see cref="UiKit.Button"/>, honoured here for the same reason: the kit
    /// never reports a click on a disabled control, so neither may this.
    /// </remarks>
    private void UpdateInspector(TraitLedger ledger, string characterId, IReadOnlyList<string> worn,
                                 Point mouse, bool clicked)
    {
        if (_selected.StartsWith('?')) return;
        if (TraitCatalogue.Find(_selected) is not { } def || !ledger.Has(def.Id)) return;

        var isWorn = worn.Contains(def.Id, StringComparer.Ordinal);
        var full = worn.Count >= TraitCatalogue.SlotsPerCharacter;
        var enabled = isWorn || !full;
        if (!enabled || !UiKit.ClickedIn(ActionButton, mouse, clicked)) return;
        if (isWorn) { if (ledger.Unequip(characterId, def.Id)) { Dirty = true; _cue = "sfx_click"; } }
        else if (ledger.Equip(characterId, def.Id)) { Dirty = true; _cue = "sfx_trait_lit"; }
    }

    // ── DRAWING. Presentation only: no edge reaches this half ────────────────────────────────────

    /// <summary>
    /// Draw the screen. It answers nothing — every click, the wheel and the drag live in
    /// <see cref="Update"/>, and the host reads <see cref="Dirty"/> and <see cref="ConsumeCue"/> there.
    /// </summary>
    /// <param name="ledger">The account's ledger, read for what has awakened and who first lived it.</param>
    /// <param name="characterId">Whose three slots these are — the loadout is per champion (§26).</param>
    /// <param name="characterName">What to call them, in the strip's caption.</param>
    public void Draw(SpriteBatch b, TraitLedger ledger, string characterId, string characterName, Point mouse)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(ledger);

        // The starfield behind it is the host's (bg_constellation) and it still works — §37 and §41
        // both say to keep it. A scrim over it, like every other menu screen, so text reads.
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0B, 0x09, 0x08, 0xD2));

        _ui.TextCenterBig(b, "TRAITS", UiKit.PageCenterX, ScreenTitleTop, Gold,
                          UiTypography.ScreenTitle, TextFace.Display);
        // NO TALLY. "17 of 26" is the achievement menu §90 forbids in as many words; what the screen
        // says instead is what traits ARE and how they arrive.
        // TRAITS, the rail's own word — CHARACTERISTICS was a synonym the player was never taught
        // (release polish 2026-09-05, traits-09).
        const string caption = "TRAITS AWAKEN THROUGH WHAT YOU HAVE LIVED THROUGH";
        // THE RULE IS AS WIDE AS THE SENTENCE UNDER IT, measured rather than guessed: a literal width
        // is a promise about a font at one UI SCALE, and at 125 and 150 the caption grows past it
        // while the rule does not. Measured, the two agree at every scale.
        var ruleW = _ui.MeasureBig(caption, UiTypography.Secondary) + UiMetrics.Space(24);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - ruleW / 2, RuleY, ruleW, RuleH), Gold * 0.5f);
        // Clear of the rule by a real gap: the tall letters touched it at UI SCALE 100.
        _ui.TextCenterBig(b, caption, UiKit.PageCenterX, RuleY + RuleH + UiMetrics.Space(7),
                          Slate, UiTypography.Secondary);

        var board = Board;
        var inspector = Inspector;
        _ui.PanelQuiet(b, board);
        _ui.PanelQuiet(b, inspector);

        var worn = ledger.LoadoutOf(characterId);
        DrawWorn(b, characterName, worn, mouse);
        DrawCollection(b, ledger, worn, mouse);
        DrawInspector(b, ledger, worn, mouse);
        DrawCarriedTrait(b);
    }

    /// <summary>The trait under the cursor while it is in flight — a small plate with its sigil.</summary>
    /// <remarks>
    /// Drawn LAST, over the inspector, so the hand is never behind a panel. A ghost at half weight
    /// rather than a full tile: it has to read as something being moved, not as a tile that has already
    /// landed somewhere odd.
    /// </remarks>
    private void DrawCarriedTrait(SpriteBatch b)
    {
        if (!Dragging || TraitCatalogue.Find(_carryId!) is not { } def) return;
        var side = WornSide * 3 / 4;
        var r = new Rectangle(_carryAt.X - side / 2, _carryAt.Y - side / 2, side, side);
        _ui.Fill(b, new Rectangle(r.X + 5, r.Y + 6, r.Width, r.Height), new Color(0, 0, 0) * 0.45f);
        _ui.Plate(b, r, Gold);
        Face(b, r, def.Id, Gold);
        _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, r.Width + Gap, UiTypography.Caption),
                          r.Center.X, r.Bottom + UiMetrics.Space(4), Bone, UiTypography.Caption);
    }

    private void DrawWorn(SpriteBatch b, string characterName, IReadOnlyList<string> worn, Point mouse)
    {
        var board = Board;
        // A breath past the corner flourish, which points straight at the title's first letter on the
        // medium frame (traits-07).
        // OVER THE BLOCK IT DESCRIBES, now that the block is centred — see WornSlot.
        _ui.TextCenterBig(b, $"{characterName.ToUpperInvariant()} WEARS THREE",
                          WornStrip.Center.X, UiKit.TitleTop(board), Bone, UiTypography.PanelTitle);

        for (var i = 0; i < TraitCatalogue.SlotsPerCharacter; i++)
        {
            var slot = WornSlot(i);
            var id = i < worn.Count ? worn[i] : null;
            var hot = slot.Contains(mouse);
            // THE SLOT THE DROP WOULD LAND IN, lit while the hand is still moving — the whole reason
            // dragging beats select-then-click here is that it answers "which of the three am I
            // replacing" BEFORE the replacing happens.
            var target = Dragging && slot.Contains(_carryAt);
            // ...and the one it came out of goes quiet, so the strip shows the move rather than showing
            // the trait in two places at once.
            var lifted = Dragging && _carryFromSlot == i;

            _ui.Plate(b, slot, id is null ? null : Gold);
            if (id is not null && TraitCatalogue.Find(id) is { } def)
            {
                Face(b, slot, def.Id, Gold);
                var nameY = slot.Bottom + UiMetrics.Space(4);
                _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, slot.Width + Gap, UiTypography.Caption),
                                  slot.Center.X, nameY, Bone, UiTypography.Caption);
            }
            else
            {
                // AN EMPTY SLOT SAYS SO, in a word, rather than being a hole. Never "LOCKED": the
                // slots are not earned, the traits are.
                _ui.TextCenterBig(b, "EMPTY", slot.Center.X,
                                  slot.Center.Y - UiTypography.Caption / 2, UiInk.Empty, UiTypography.Caption);
            }

            if (hot && !Dragging) _ui.Fill(b, slot, Color.White * 0.05f);
            if (lifted) _ui.Fill(b, slot, new Color(0x0B, 0x09, 0x08) * 0.55f);
            if (target)
            {
                _ui.Fill(b, slot, Gold * 0.16f);
                Outline(b, slot, Gold, 3);
            }
            // AND THE DROP ITSELF LANDS. A trait arriving in a slot flashes it gold over the reward
            // beat: the gesture ends with something happening, which is the half a swap was missing.
            if (UiMotion.Pulse(TraitSlotKey(i)) is var landed && landed > 0f && !UiMotion.Reduced)
                Outline(b, slot, Gold * UiMotion.Smooth(landed), 3);
        }
    }

    private void DrawCollection(SpriteBatch b, TraitLedger ledger, IReadOnlyList<string> worn, Point mouse)
    {
        var board = Board;
        _ui.TextBig(b, "WHAT YOU HAVE AWAKENED", UiKit.ContentLeft(board),
                    CollectionCaptionY - UiTypography.Pitch(UiTypography.SectionLabel),
                    Slate, UiTypography.SectionLabel);
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(board), CollectionCaptionY - UiMetrics.Space(4),
                                  UiKit.ContentRight(board) - UiKit.ContentLeft(board), 1), Dim);

        var all = TraitCatalogue.All;
        var (cols, rowsAll, cellW, cellH, visible) = Grid(all.Count);
        var area = CollectionArea;
        // (The wheel notch AND its clamp belong to UpdateCollection — both of them are state writes.)
        for (var i = 0; i < all.Count; i++)
        {
            if (!OnPage(i, cols, visible, rowsAll)) continue;   // rows beyond the page wait for the wheel
            var def = all[i];
            var known = ledger.Has(def.Id);
            var cell = Cell(i, cols, cellW, cellH, visible, rowsAll);   // the rectangle UpdateCollection hit-tests
            var tile = Tile(cell);
            var hot = cell.Contains(mouse);
            var isWorn = worn.Contains(def.Id, StringComparer.Ordinal);
            var picked = string.Equals(_selected, known ? def.Id : UnknownKey(i), StringComparison.Ordinal);

            // EVERY TILE IS THE HOUSE PLATE, as the worn strip's slots are — a bare sigil on the scrim with
            // a hollow outline for its states was one object in two visual languages (traits-06). A worn
            // tile carries the gold accent the strip gives a worn slot, so WORN needs no third line.
            _ui.Plate(b, tile, isWorn ? Gold : null);
            if (hot && !picked) _ui.Fill(b, tile, Color.White * 0.05f);
            if (known)
            {
                Face(b, tile, def.Id, isWorn ? Gold : Bone);
                // The name WRAPS to the two Caption lines the cell reserves — at 125/150 half the awakened
                // names were cut to stubs, and the name is the tile's only identifier (traits-02).
                var ny = tile.Bottom + UiMetrics.Space(4);
                foreach (var l in _ui.WrapBig(def.Name, cell.Width - UiMetrics.Space(6), UiTypography.Caption).Take(2))
                {
                    _ui.TextCenterBig(b, l, cell.Center.X, ny, isWorn ? Gold : Bone, UiTypography.Caption);
                    ny += UiTypography.Pitch(UiTypography.Caption);
                }
            }
            else
            {
                // AN UNDISCOVERED TRAIT IS A SEAL AND A QUESTION — the house's unknown seal, the ???, the
                // word — the same reading the BUILD library gives an unreached skill (traits-05). Its own
                // constellation stays behind the seal, faint, so a player who awakens it recognises the
                // shape that was there all along.
                var sealSide = tile.Width * 3 / 4;
                var seal = new Rectangle(tile.Center.X - sealSide / 2, tile.Center.Y - sealSide / 2, sealSide, sealSide);
                if (!_ui.Icon(b, "icon_unknown_seal", seal, Color.White * (hot ? 0.9f : 0.75f))) _ui.Diamond(b, seal, Unknown * 0.6f);
                _ui.TextCenterBig(b, "???", cell.Center.X, tile.Bottom + UiMetrics.Space(4), Unknown, UiTypography.Caption);
                // The word only where it fits its cell: the seal and the ??? already say it, and a word
                // that ran into its neighbour's said nothing.
                if (_ui.MeasureBig("UNDISCOVERED", UiTypography.Caption) <= cell.Width - UiMetrics.Space(4))
                    _ui.TextCenterBig(b, "UNDISCOVERED", cell.Center.X, tile.Bottom + UiMetrics.Space(4) + UiTypography.Pitch(UiTypography.Caption),
                                      Unknown * 0.8f, UiTypography.Caption);
            }

            if (picked) Outline(b, tile, Gold * 0.9f, 2);
            // The tile in hand dims where it lies, for the same reason a lifted worn slot does.
            if (Dragging && string.Equals(_carryId, def.Id, StringComparison.Ordinal) && _carryFromSlot < 0)
                _ui.Fill(b, tile, new Color(0x0B, 0x09, 0x08) * 0.55f);
        }

        // THE SCROLLBAR, in its lane inside the content edge — only when a row waits beyond the last
        // visible one, and then the same words the GEAR bag uses for the same gesture.
        if (rowsAll > visible)
        {
            _ui.ScrollBar(b, new Rectangle(area.Right - UiMetrics.ScrollbarWidth, area.Y, UiMetrics.ScrollbarWidth, visible * cellH),
                          ScrollAt(visible, rowsAll), visible, rowsAll);
            if (ScrollAt(visible, rowsAll) + visible < rowsAll)
                _ui.TextBig(b, "MORE BELOW — THE MOUSE WHEEL SCROLLS", area.X, area.Y + visible * cellH + UiMetrics.Space(2),
                            Slate, UiTypography.Caption);
        }
    }

    /// <summary>The inspector's key for an unknown tile — a position, never an id (LAW 8).</summary>
    private static string UnknownKey(int index) => "?" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void DrawInspector(SpriteBatch b, TraitLedger ledger, IReadOnlyList<string> worn, Point mouse)
    {
        var p = Inspector;
        var left = UiKit.ContentLeft(p);
        var room = UiKit.ContentRight(p) - left;
        var y = UiKit.TitleTop(p);

        if (_selected.StartsWith('?'))
        {
            // THE UNKNOWN READING. Two lines, and neither of them is a hint — §40 says so outright,
            // and the way to guarantee it is that this branch has no access to the trait at all: it
            // was handed a position, not an id.
            _ui.TextBig(b, "???", left, y, Unknown, UiTypography.PanelTitle);
            y += UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(8);
            foreach (var line in _ui.WrapBig("SOMETHING REMAINS UNDISCOVERED.", room, UiTypography.Body))
            {
                _ui.TextBig(b, line, left, y, Slate, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }
            y += UiMetrics.Space(10);
            foreach (var line in _ui.WrapBig(
                         "A TRAIT AWAKENS FROM WHAT YOU DO, NOT FROM WHAT YOU BUY. KEEP HUNTING.",
                         room, UiTypography.Secondary))
            {
                _ui.TextBig(b, line, left, y, Slate, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }
            return;
        }

        if (TraitCatalogue.Find(_selected) is not { } def || !ledger.Has(def.Id))
        {
            _ui.TextBig(b, "PICK A TRAIT", left, y, Slate, UiTypography.PanelTitle);
            y += UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(8);
            foreach (var line in _ui.WrapBig(
                         "CLICK ONE BELOW TO READ IT. YOU MAY WEAR THREE AT A TIME, AND CHANGING THEM COSTS NOTHING.",
                         room, UiTypography.Body))
            {
                _ui.TextBig(b, line, left, y, Slate, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }
            return;
        }

        var isWorn = worn.Contains(def.Id, StringComparer.Ordinal);

        // THE INSPECTOR GRAMMAR (UX guide §6): CATEGORY in Secondary, then the NAME at Headline — gold
        // only while it is worn (traits-08). The identity line is the trait's own sentence, in the
        // Primary ink: it was the hairline ink, about 1.5:1, and unreadable at every profile (traits-03).
        _ui.TextBig(b, TraitCatalogue.TagName(def.Tag), left, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextBig(b, _ui.ShortenBig(def.Name, room, UiTypography.Headline), left, y, isWorn ? Gold : Bone,
                    UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);

        // THE HERO SIGIL — the constellation at a size the grid cannot afford, on a plate, so the column
        // is the trait's identity rather than five lines over half a panel of black (traits-04). Skipped
        // when the column has no room for it above the button.
        var heroSide = UiMetrics.Control(120);
        var textBelow = UiTypography.Pitch(UiTypography.Body) * 6 + UiTypography.Pitch(UiTypography.Secondary) * 4 + UiMetrics.Space(60);
        if (y + heroSide + textBelow < ActionButton.Y)
        {
            var hero = new Rectangle(left, y, heroSide, heroSide);
            _ui.Plate(b, hero, isWorn ? Gold : null);
            Face(b, hero, def.Id, isWorn ? Gold : Bone);
            y += heroSide + UiMetrics.Space(12);
        }

        foreach (var line in _ui.WrapBig(def.Flavour.ToUpperInvariant(), room, UiTypography.Body))
        {
            _ui.TextBig(b, line, left, y, Bone, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }
        y += UiMetrics.Space(12);

        _ui.TextBig(b, "WHAT IT DOES", left, y, Slate, UiTypography.SectionLabel);
        y += UiTypography.Pitch(UiTypography.SectionLabel) + UiMetrics.Space(4);
        foreach (var line in _ui.WrapBig(def.Line.ToUpperInvariant(), room, UiTypography.Body))
        {
            _ui.TextBig(b, line, left, y, Bone, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }
        y += UiMetrics.Space(14);

        _ui.TextBig(b, "CURRENT STATE", left, y, Slate, UiTypography.SectionLabel);   // the grammar's word, as BUILD, MAP and ROSTER say it (traits-08)
        y += UiTypography.Pitch(UiTypography.SectionLabel) + UiMetrics.Space(4);
        _ui.TextBig(b, isWorn ? "YOU ARE WEARING IT" : "AWAKENED, AND NOT WORN", left, y,
                    isWorn ? Gold : Bone, UiTypography.Body);
        y += UiTypography.Pitch(UiTypography.Body);

        // WHO FIRST LIVED IT (§33). One line, from one saved row — not a history.
        if (ledger.FirstOf(def.Id) is { } first && first.CharacterId.Length > 0)
        {
            var who = CharacterName(first.CharacterId);
            var where = RegionName(first.RegionId);
            var line = where.Length > 0
                ? $"FIRST AWAKENED BY {who} IN {where}"
                : $"FIRST AWAKENED BY {who}";
            foreach (var wrapped in _ui.WrapBig(line, room, UiTypography.Secondary))
            {
                _ui.TextBig(b, wrapped, left, y, Slate, UiTypography.Secondary);   // Secondary ink, not the hairline's (traits-03)
                y += UiTypography.Pitch(UiTypography.Secondary);
            }
        }

        // THE ONE BUTTON. Free and reversible either way (§27); when all three are full it says so
        // and points at the swap that does exist — clicking a slot up there.
        var full = worn.Count >= TraitCatalogue.SlotsPerCharacter;
        var label = isWorn ? "TAKE IT OFF" : full ? "CLICK A SLOT ABOVE TO SWAP" : "WEAR IT";
        var enabled = isWorn || !full;
        // PAINTED WITH `false`, the way every button on the reference screens paints: UiKit.Button's
        // clicked argument affects ONLY its return value — the hover lift and the pressed face both come
        // from the static UiKit.MouseHeld — so this is pixel-identical to the call that used to answer
        // here. UpdateInspector resolves it, against this same ActionButton and this same `enabled`.
        _ui.Button(b, ActionButton, label, mouse, false, enabled, isWorn ? ButtonStyle.Secondary : ButtonStyle.Primary);
    }

    /// <summary>The champion's own name, or the raw id if the roster no longer has them.</summary>
    private static string CharacterName(string id)
        => IdleXIdle.Core.Characters.CharacterRoster.Find(id)?.Name.ToUpperInvariant() ?? id.ToUpperInvariant();

    /// <summary>The region's own name, or nothing — an awakening outside a run has no region.</summary>
    private static string RegionName(string id)
        => IdleXIdle.Core.Encounters.Regions.Find(id)?.Name.ToUpperInvariant() ?? "";

    // ── THE SIGIL ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A constellation, drawn from the trait's own id: the same trait always draws the same stars.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deterministic from a hash of the id, so nothing is stored, nothing is authored, and an unknown
    /// trait shows the SAME shape it will show once it awakens — which is the whole point of a
    /// mysterious sigil rather than a padlock: the player recognises the constellation that was there
    /// all along.
    /// </para>
    /// <para>
    /// A LIT sigil joins its stars with hairlines and draws them larger; an unknown one shows the
    /// stars alone, dim and unjoined. No text, no number, and nothing derived from the trait's rule —
    /// the shape must not encode the condition.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A DISCOVERED trait's face: its authored icon (assets/art/UI/icons/traits, PixelLab 2026-09-06 —
    /// one family, bone-white etched on black stone), or the drawn constellation while an icon is missing.
    /// </summary>
    /// <remarks>
    /// The icon is drawn in its own colours: the WORN state is said by the plate's gold accent around
    /// it, not by tinting the art gold, which only muddied the etching. An UNDISCOVERED trait never
    /// reaches this method — it wears the unknown seal and nothing of its own.
    /// </remarks>
    private void Face(SpriteBatch b, Rectangle box, string id, Color ink)
    {
        if (_ui.Icon(b, $"icon_trait_{id}", box)) return;
        Sigil(b, box, id, ink, lit: true);
    }

    private void Sigil(SpriteBatch b, Rectangle box, string id, Color ink, bool lit)
    {
        if (box.Width <= 0 || box.Height <= 0) return;

        // A stable hash of the id. String.GetHashCode is randomised per process, which would redraw
        // every constellation on every launch — the one thing a sigil must never do.
        var h = 2166136261u;
        foreach (var c in id) h = (h ^ c) * 16777619u;

        var pad = Math.Max(2, box.Width / 8);
        var inner = new Rectangle(box.X + pad, box.Y + pad, box.Width - pad * 2, box.Height - pad * 2);
        var stars = 4 + (int)(h % 3);                       // four, five or six
        var dot = Math.Max(2, inner.Width / (lit ? 11 : 14));

        Span<Point> at = stackalloc Point[6];
        for (var i = 0; i < stars; i++)
        {
            h = h * 1664525u + 1013904223u;
            var px = (int)(h >> 8 & 0xFFFF) / 65535f;
            h = h * 1664525u + 1013904223u;
            var py = (int)(h >> 8 & 0xFFFF) / 65535f;
            at[i] = new Point(inner.X + (int)(px * (inner.Width - dot)),
                              inner.Y + (int)(py * (inner.Height - dot)));
        }

        if (lit)
            for (var i = 1; i < stars; i++)
                _ui.LineSeg(b,
                            new Vector2(at[i - 1].X + dot / 2f, at[i - 1].Y + dot / 2f),
                            new Vector2(at[i].X + dot / 2f, at[i].Y + dot / 2f),
                            1f, ink * 0.35f);

        for (var i = 0; i < stars; i++)
            _ui.Fill(b, new Rectangle(at[i].X, at[i].Y, dot, dot), ink * (lit ? 1f : 0.75f));
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
