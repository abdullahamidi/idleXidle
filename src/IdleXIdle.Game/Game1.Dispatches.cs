using System;
using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Progression;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game;

/// <summary>
/// DISPATCHES — the reading surface for the account's own news, and the envelope that opens it.
/// </summary>
/// <remarks>
/// <para>
/// This half of the host is the PANEL: its geometry, and its paint. Every decision the panel makes —
/// the envelope's click, the M key, the rows, MARK ALL READ, the way out — lives in
/// <c>Game1.Update</c> and <c>TakeDispatchesInput</c> over in Game1.cs, because a surface whose input
/// is read where it is painted is the exact fault ADR-006 exists to prevent, and because the host's
/// input invariants are pinned by reading that one file.
/// </para>
/// <para>
/// WHY A PANEL AND NOT A FEED OF TOASTS. A toast is a claim on the player's eyes for six seconds and
/// then it is gone whether it was read or not; the things this surface carries — a region conquered,
/// a trait awoken, a champion joining — are worth keeping and worth coming back to. So they are a
/// LIST that waits, in the game's own vellum-and-gold grammar, with the letter the player is looking
/// at beside it. No delete, no archive, no folders: an inbox to manage is a second game.
/// </para>
/// </remarks>
public partial class Game1
{
    // ── THE PANEL'S GEOMETRY. One frame, read by the paint and by Update. ────────────────────────
    //
    // The EXPEDITION LOG's rule to the pixel (HuntScreen.LogPanel): the two full-screen reads in this
    // game are one shape, so a player who has learned where the close icon is has learned it for both.

    /// <summary>The footer button's height — the LOG's own 56, at the profile.</summary>
    private static int DispatchesButtonHeight => UiMetrics.Control(56);

    /// <summary>Where the rows start under the title row: the modal title, its line, and a breath.</summary>
    private static int DispatchesBandTop
        => UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(20);

    /// <summary>The list column's width — a subject line and its kicker, never half the panel.</summary>
    private static int DispatchesListWidth => UiMetrics.Control(440);

    /// <summary>
    /// The DISPATCHES panel at the current profile — page space, rows UNSCROLLED. Pure arithmetic over
    /// <see cref="UiMetrics"/>: no text is measured, so the static rects below hold with no font, which
    /// is what lets <c>chrome_reflow_test</c> and <c>page_layout_test</c> check them at every density.
    /// </summary>
    private struct DispatchesFrame
    {
        public Rectangle Panel, Close, List, Pane, MarkAll;
        /// <summary>How many letters the list column holds at this profile.</summary>
        public int Rows;
    }

    /// <summary>Lay the DISPATCHES panel out for the current profile. See <see cref="DispatchesFrame"/>.</summary>
    private static DispatchesFrame DispatchesFrameNow()
    {
        var page = UiKit.Page;
        var w = UiMetrics.Space(1200);
        var h = Math.Min(UiMetrics.Space(800), page.Height - UiMetrics.Space(60));
        var panel = new Rectangle((page.Width - w) / 2, (page.Height - h) / 2, w, h);
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var top = panel.Y + DispatchesBandTop;
        var footY = panel.Bottom - UiMetrics.Space(48) - DispatchesButtonHeight;
        // WHOLE ROWS ONLY. A column that ends mid-row paints half a letter the click cannot reach, and
        // the two halves would then disagree about where the last row is.
        var rows = Math.Max(1, (footY - UiMetrics.Space(20) - top) / UiMetrics.RowHeight);
        var columnH = rows * UiMetrics.RowHeight;
        var paneX = x0 + DispatchesListWidth + UiMetrics.Space(24);
        return new DispatchesFrame
        {
            Panel = panel,
            Close = UiKit.CloseRect(panel),
            List = new Rectangle(x0, top, DispatchesListWidth, columnH),
            Pane = new Rectangle(paneX, top, x1 - paneX, columnH),
            // MARK ALL READ, at the panel's foot where the LOG puts its doors. Secondary, not Primary:
            // nothing on this surface is the screen's decision — it is a tidy-up, not a choice.
            MarkAll = new Rectangle(x1 - UiMetrics.Space(280), footY, UiMetrics.Space(280), DispatchesButtonHeight),
            Rows = rows,
        };
    }

    // The frame's rectangles as static properties, so the layout tests can walk every one of them at
    // every profile. The paint reads the frame once per frame instead.
    private static Rectangle DispatchesPanel => DispatchesFrameNow().Panel;
    /// <summary>The panel's corner close icon — the house rule, inside the ornament, level with the title.</summary>
    private static Rectangle DispatchesCornerClose => DispatchesFrameNow().Close;
    /// <summary>The column of letters, newest first.</summary>
    private static Rectangle DispatchesListView => DispatchesFrameNow().List;
    /// <summary>The reading pane — the letter the player has open.</summary>
    private static Rectangle DispatchesPane => DispatchesFrameNow().Pane;
    /// <summary>MARK ALL READ, live only while something is unread.</summary>
    private static Rectangle DispatchesMarkAll => DispatchesFrameNow().MarkAll;

    /// <summary>One letter's row in the list, by its place in the visible column.</summary>
    private static Rectangle DispatchRowRect(DispatchesFrame f, int k)
        => new(f.List.X, f.List.Y + k * UiMetrics.RowHeight, f.List.Width, UiMetrics.RowHeight);

    /// <summary>
    /// Which letter the cursor is on — an index into the inbox's newest-first rows, or -1 for none.
    /// </summary>
    /// <remarks>
    /// ONE GEOMETRY, READ BY BOTH HALVES: <see cref="DrawDispatches"/> paints the rows this resolves and
    /// <c>TakeDispatchesInput</c> hit-tests them, so the row that lights is the row that answers.
    /// <c>internal</c> for <c>catch_up_tick_test</c>, which drives this exact seam.
    /// </remarks>
    internal static int DispatchRowAt(Point mouse, int first, int count)
    {
        var f = DispatchesFrameNow();
        if (!f.List.Contains(mouse)) return -1;
        var k = (mouse.Y - f.List.Y) / UiMetrics.RowHeight;
        if (k < 0 || k >= f.Rows) return -1;
        var i = first + k;
        return i >= 0 && i < count ? i : -1;
    }

    /// <summary>
    /// The one-line source note at the right end of a letter's row: where the news came from, when a
    /// region owns it, and otherwise nothing. Never a date — an idle game's calendar is not the point.
    /// </summary>
    private static string DispatchKicker(Dispatch d)
        => d.RegionId is { Length: > 0 } region ? (Regions.Find(region)?.Name ?? region).ToUpperInvariant() : "";

    // ── THE PAINT. Nothing below decides anything (ADR-006, tools/check_draw_purity.py). ─────────

    /// <summary>
    /// The DISPATCHES panel: the letters on the left, the one being read on the right.
    /// </summary>
    private void DrawDispatches()
    {
        var f = DispatchesFrameNow();
        var mouse = ChromeMouse;

        _ui.Scrim(_batch, 0.72f);
        _ui.Panel(_batch, f.Panel);   // the gold nine-slice: this IS a modal, the one surface on screen
        var x0 = UiKit.ContentLeft(f.Panel);
        var titleY = f.Panel.Y + UiTypography.ModalTitleTop;
        _ui.TextBig(_batch, "DISPATCHES", x0, titleY, Gold, UiTypography.PanelTitle, TextFace.Display);
        _ui.CloseButton(_batch, f.Close, mouse, false);   // painted here; TakeDispatchesInput decides it

        var rows = _inbox.Rows;
        var unread = _inbox.Unread;
        // THE COUNT, where the LOG puts "ENTRY 3 OF 9": the one number this surface is about.
        if (rows.Count > 0)
            _ui.TextRightBig(_batch, unread > 0 ? $"{unread} UNREAD  ·  {rows.Count} KEPT" : $"{rows.Count} KEPT",
                             f.Close.X - UiMetrics.Space(20), titleY + UiMetrics.Space(6), Slate, UiTypography.Body);

        if (rows.Count == 0)
        {
            // THE LOG'S EMPTY STATE, word for word in shape: what this is, and when the first one comes.
            var emptyY = f.Panel.Center.Y - UiMetrics.Space(70);
            _ui.TextCenterBig(_batch, "NO DISPATCHES YET", f.Panel.Center.X, emptyY, Slate, UiTypography.RegionTitle, TextFace.Display);
            var lines = _ui.WrapBig("News about your account is kept here — a region held, a trait awoken, somebody new at your side. Nothing is lost by not reading it now.",
                                    UiKit.ContentRight(f.Panel) - x0 - UiMetrics.Space(200), UiTypography.Body);
            var ey = emptyY + UiTypography.Pitch(UiTypography.RegionTitle) + UiMetrics.Space(8);
            foreach (var line in lines) { _ui.TextCenterBig(_batch, line, f.Panel.Center.X, ey, Bone, UiTypography.Body); ey += UiTypography.Pitch(UiTypography.Body); }
            return;
        }

        // ── THE LETTERS. The host's own list grammar (the settings dropdown's): a warm fill and a
        //    gold left edge under the cursor, a hairline between rows, the open letter in gold.
        var first = Math.Clamp(_dispatchScroll, 0, Math.Max(0, rows.Count - f.Rows));
        var scrolling = rows.Count > f.Rows;
        var lane = scrolling ? UiMetrics.ScrollbarWidth + UiMetrics.Space(8) : 0;
        var dot = Math.Max(8, UiMetrics.Control(12));
        BeginChromeClip(f.List);
        for (var k = 0; k < f.Rows; k++)
        {
            var i = first + k;
            if (i >= rows.Count) break;
            var d = rows[i];
            var r = DispatchRowRect(f, k);
            r.Width -= lane;
            var hot = r.Contains(mouse) || i == _dispatchCursor;
            var open = i == _dispatchSelected;

            if (hot)
            {
                _ui.Fill(_batch, r, DropRowHover);
                _ui.Fill(_batch, new Rectangle(r.X, r.Y, 3, r.Height), DropRowEdge);
            }
            if (k > 0) _ui.Fill(_batch, new Rectangle(r.X, r.Y, r.Width, 1), DropRowRule);

            // AN UNREAD LETTER WEARS THE RAIL'S DOT and its subject is in the primary ink; a read one
            // has neither. State, not animation — it holds until the letter is read.
            var textX = r.X + UiTypography.ButtonPadX + dot + UiMetrics.Space(10);
            if (!d.Read)
            {
                var at = new Rectangle(r.X + UiTypography.ButtonPadX, r.Center.Y - dot / 2, dot, dot);
                _ui.Disc(_batch, new Rectangle(at.X - 2, at.Y - 2, at.Width + 4, at.Height + 4), UiInk.Ground * 0.85f);
                _ui.Disc(_batch, at, UiInk.Danger);
            }
            var kicker = DispatchKicker(d);
            var kickerW = kicker.Length > 0 ? _ui.MeasureBig(kicker, UiTypography.Caption) + UiMetrics.Space(12) : 0;
            var ink = open ? Gold : d.Read ? Slate : Bone;
            _ui.TextBig(_batch, _ui.ShortenBig(DispatchCopy.Headline(d), r.Right - UiTypography.ButtonPadX - kickerW - textX, UiTypography.Body),
                        textX, r.Center.Y - UiTypography.Body * 27 / 40, ink, UiTypography.Body);
            if (kicker.Length > 0)
                _ui.TextRightBig(_batch, kicker, r.Right - UiTypography.ButtonPadX,
                                 r.Center.Y - UiTypography.Caption * 27 / 40, Slate, UiTypography.Caption);
        }
        EndChromeClip();
        if (scrolling)
            _ui.ScrollBar(_batch, new Rectangle(f.List.Right - UiMetrics.ScrollbarWidth, f.List.Y, UiMetrics.ScrollbarWidth, f.List.Height),
                          first, f.Rows, rows.Count);

        // ── THE LETTER ITSELF. Source, headline, and the two or three lines that say what happened.
        //
        // NOTHING IS OPEN UNTIL THE PLAYER OPENS ONE (see _dispatchSelected): with one letter waiting,
        // a pane that showed the newest on sight would clear the envelope's mark before a word of it
        // had been read. So the pane names the act instead, and the list is where the choosing happens.
        if (_dispatchSelected < 0 || _dispatchSelected >= rows.Count)
        {
            _ui.TextBig(_batch, "CHOOSE A DISPATCH", f.Pane.X, f.Pane.Y, Slate, UiTypography.Headline, TextFace.Display);
            _ui.TextBig(_batch, "Click one, or move with UP and DOWN and open it with ENTER.",
                        f.Pane.X, f.Pane.Y + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(10),
                        Slate, UiTypography.Body);
            _ui.Button(_batch, f.MarkAll, "MARK ALL READ", mouse, false, enabled: unread > 0);
            return;
        }

        var open2 = rows[_dispatchSelected];
        var py = f.Pane.Y;
        var source = DispatchKicker(open2);
        if (source.Length > 0)
        {
            _ui.TextBig(_batch, source, f.Pane.X, py, Slate, UiTypography.Secondary);
            py += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
        }
        foreach (var line in _ui.WrapBig(DispatchCopy.Headline(open2), f.Pane.Width, UiTypography.Headline))
        {
            _ui.TextBig(_batch, line, f.Pane.X, py, Gold, UiTypography.Headline, TextFace.Display);
            py += UiTypography.Pitch(UiTypography.Headline);
        }
        py += UiMetrics.Space(10);
        foreach (var line in _ui.WrapBig(DispatchCopy.Body(open2), f.Pane.Width, UiTypography.Body))
        {
            _ui.TextBig(_batch, line, f.Pane.X, py, Bone, UiTypography.Body);
            py += UiTypography.Pitch(UiTypography.Body);
        }

        // ── THE FOOTER. Painted with a false edge; TakeDispatchesInput hit-tests this same rectangle.
        _ui.Button(_batch, f.MarkAll, "MARK ALL READ", mouse, false, enabled: unread > 0);
    }
}
