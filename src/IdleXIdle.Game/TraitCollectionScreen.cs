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

    private readonly UiKit _ui;

    /// <summary>The trait the inspector is reading, or "" for none. An UNKNOWN slot selects as "?<index>".</summary>
    private string _selected = "";

    /// <summary>The first visible ROW of the collection when the page cannot hold every sigil (UI SCALE 150).</summary>
    private int _scroll;

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

    /// <summary>One worn slot's square, counted from the left of the strip.</summary>
    private static Rectangle WornSlot(int i)
    {
        // LEFT-ALIGNED under the title, not centred in a 1150 px strip: the eye read the title, then
        // travelled 420 px of black to find its subject (traits-11).
        var strip = WornStrip;
        var x = strip.X + UiMetrics.Space(16);
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

    /// <summary>The cell one entry of the collection occupies — by its row LESS the scroll, so a row above the first visible one lands above the area.</summary>
    private Rectangle Cell(int index, int count)
    {
        var (cols, _, cw, ch, _) = Grid(count);
        var area = CollectionArea;
        return new Rectangle(area.X + index % cols * cw, area.Y + (index / cols - _scroll) * ch, cw, ch);
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

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw the screen and answer its clicks.
    /// </summary>
    /// <param name="ledger">The account's ledger. The screen reads and equips; it never discovers.</param>
    /// <param name="characterId">Whose three slots these are — the loadout is per champion (§26).</param>
    /// <param name="characterName">What to call them, in the strip's caption.</param>
    public void Draw(SpriteBatch b, TraitLedger ledger, string characterId, string characterName,
                     Point mouse, bool clicked, int wheel = 0)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(ledger);
        Dirty = false;

        // The starfield behind it is the host's (bg_constellation) and it still works — §37 and §41
        // both say to keep it. A scrim over it, like every other menu screen, so text reads.
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD2));

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
        DrawWorn(b, ledger, characterId, characterName, worn, mouse, clicked);
        DrawCollection(b, ledger, characterId, worn, mouse, clicked, wheel);
        DrawInspector(b, ledger, characterId, worn, mouse, clicked);
    }

    private void DrawWorn(SpriteBatch b, TraitLedger ledger, string characterId, string characterName,
                          IReadOnlyList<string> worn, Point mouse, bool clicked)
    {
        var board = Board;
        // A breath past the corner flourish, which points straight at the title's first letter on the
        // medium frame (traits-07).
        _ui.TextBig(b, $"{characterName.ToUpperInvariant()} WEARS THREE",
                    UiKit.ContentLeft(board) + UiMetrics.Space(16), UiKit.TitleTop(board), Bone, UiTypography.PanelTitle);

        for (var i = 0; i < TraitCatalogue.SlotsPerCharacter; i++)
        {
            var slot = WornSlot(i);
            var id = i < worn.Count ? worn[i] : null;
            var hot = slot.Contains(mouse);

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

            if (hot) _ui.Fill(b, slot, Color.White * 0.05f);

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

    private void DrawCollection(SpriteBatch b, TraitLedger ledger, string characterId,
                               IReadOnlyList<string> worn, Point mouse, bool clicked, int wheel)
    {
        var board = Board;
        _ui.TextBig(b, "WHAT YOU HAVE AWAKENED", UiKit.ContentLeft(board),
                    CollectionCaptionY - UiTypography.Pitch(UiTypography.SectionLabel),
                    Slate, UiTypography.SectionLabel);
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(board), CollectionCaptionY - UiMetrics.Space(4),
                                  UiKit.ContentRight(board) - UiKit.ContentLeft(board), 1), Dim);

        var all = TraitCatalogue.All;
        var (cols, rowsAll, _, cellH, visible) = Grid(all.Count);
        var area = CollectionArea;
        _scroll = UiKit.Scrolled(_scroll, wheel != 0 && Board.Contains(mouse) ? Math.Sign(wheel) : 0, visible, rowsAll);
        for (var i = 0; i < all.Count; i++)
        {
            var row = i / cols;
            if (row < _scroll || row >= _scroll + visible) continue;   // rows beyond the page wait for the wheel
            var def = all[i];
            var known = ledger.Has(def.Id);
            var cell = Cell(i, all.Count);
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

            if (UiKit.ClickedIn(cell, mouse, clicked))
            {
                _selected = known ? def.Id : UnknownKey(i);
                _cue = "sfx_click";
            }
        }

        // THE SCROLLBAR, in its lane inside the content edge — only when a row waits beyond the last
        // visible one, and then the same words the GEAR bag uses for the same gesture.
        if (rowsAll > visible)
        {
            _ui.ScrollBar(b, new Rectangle(area.Right - UiMetrics.ScrollbarWidth, area.Y, UiMetrics.ScrollbarWidth, visible * cellH),
                          _scroll, visible, rowsAll);
            if (_scroll + visible < rowsAll)
                _ui.TextBig(b, "MORE BELOW — THE MOUSE WHEEL SCROLLS", area.X, area.Y + visible * cellH + UiMetrics.Space(2),
                            Slate, UiTypography.Caption);
        }
    }

    /// <summary>The inspector's key for an unknown tile — a position, never an id (LAW 8).</summary>
    private static string UnknownKey(int index) => "?" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void DrawInspector(SpriteBatch b, TraitLedger ledger, string characterId,
                               IReadOnlyList<string> worn, Point mouse, bool clicked)
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
        if (_ui.Button(b, ActionButton, label, mouse, clicked, enabled,
                       isWorn ? ButtonStyle.Secondary : ButtonStyle.Primary))
        {
            if (isWorn) { if (ledger.Unequip(characterId, def.Id)) { Dirty = true; _cue = "sfx_click"; } }
            else if (ledger.Equip(characterId, def.Id)) { Dirty = true; _cue = "sfx_trait_lit"; }
        }
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
