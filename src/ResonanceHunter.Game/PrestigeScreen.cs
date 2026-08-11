using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The DUST screen (nav: DUST): the Memory Dust prestige tree, presented as the reference's blessing
/// browser — a Dust overview, a filterable blessing grid, and a selected-blessing detail/upgrade panel.
/// </summary>
/// <remarks>
/// Built to the Dust production spec (rev 1). Every value is real, from <see cref="MemoryDustTree"/>: the
/// 36 unlocks with their names, costs, descriptions, prerequisites, and their <see cref="UnlockEffect"/>
/// category. The real tree is a graph of BINARY unlocks (owned / available / locked), not tiered ranks, so
/// the reference's "Tier 4/10" becomes an honest LIT / AVAILABLE / LOCKED state (UX standard §10/§11). The
/// class keeps its name and Draw/Update signatures so the host wiring is unchanged.
/// </remarks>
public sealed class PrestigeScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Violet = new(0xC0, 0x6E, 0xE0);
    private static readonly Color Teal = new(0x5F, 0xE0, 0xC8);

    private readonly UiKit _ui;
    private string _selectedId = "";
    private UnlockEffect? _filter;
    private int _scroll;
    private string _msg = "";
    private KeyboardState _prevKeys;

    public bool DevDustDebug { get; set; }

    public PrestigeScreen(UiKit ui, MemoryDustTree tree)
    {
        _ui = ui;
        _selectedId = tree.All.OrderBy(u => u.Cost).First().Id;
    }

    /// <summary>DEV ONLY: pose the detail panel on a specific blessing for the screenshot fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    private static readonly Rectangle OverviewPanel = new(38, 144, 366, 718);
    private static readonly Rectangle GridPanel = new(434, 144, 858, 718);
    private static readonly Rectangle DetailPanel = new(1320, 144, 560, 718);

    private const int Cols = 3, VisRows = 4;

    private static Rectangle Card(int vis)
    {
        var col = vis % Cols;
        var row = vis / Cols;
        return new Rectangle(GridPanel.X + 24 + col * 274, GridPanel.Y + 58 + row * 160, 258, 148);
    }

    private static string CatName(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => "AMPLIFIER", UnlockEffect.Expansion => "EXPANSION", _ => "CONVENIENCE",
    };

    private static Color CatColor(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => Gold, UnlockEffect.Expansion => Violet, _ => Teal,
    };

    // Display order: Amplifier (the common boosts) first, then Expansion (keystones/vows), then Convenience.
    // NOT the raw enum order (which is Convenience, Expansion, Amplifier) — that buries the everyday nodes.
    private static int CatOrder(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => 0, UnlockEffect.Expansion => 1, _ => 2,
    };

    private List<MemoryDustUnlock> Ordered(MemoryDustTree tree) =>
        tree.All.OrderBy(u => CatOrder(u.Effect)).ThenBy(u => u.Cost).ThenBy(u => u.Name).ToList();

    private List<MemoryDustUnlock> Filtered(MemoryDustTree tree) =>
        (_filter is { } f ? Ordered(tree).Where(u => u.Effect == f) : Ordered(tree)).ToList();

    private MemoryDustUnlock Selected(MemoryDustTree tree) =>
        tree.All.FirstOrDefault(u => u.Id == _selectedId) ?? Ordered(tree).First();

    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    public void Update(KeyboardState keys, Point mouse, bool clicked, int wheel, MemoryDustTree tree)
    {
        var list = Filtered(tree);
        if (wheel != 0)
        {
            var maxScroll = Math.Max(0, (list.Count - 1) / Cols - (VisRows - 1));
            _scroll = Math.Clamp(_scroll - wheel, 0, maxScroll);
        }

        // Arrows walk the filtered grid; Enter buys the selected blessing.
        var idx = Math.Max(0, list.FindIndex(u => u.Id == _selectedId));
        if (Pressed(keys, Keys.Left)) idx = Math.Max(0, idx - 1);
        if (Pressed(keys, Keys.Right)) idx = Math.Min(list.Count - 1, idx + 1);
        if (Pressed(keys, Keys.Up)) idx = Math.Max(0, idx - Cols);
        if (Pressed(keys, Keys.Down)) idx = Math.Min(list.Count - 1, idx + Cols);
        if (list.Count > 0) { _selectedId = list[idx].Id; KeepVisible(idx); }
        if (Pressed(keys, Keys.Enter)) Buy(tree, Selected(tree));

        _prevKeys = keys;
    }

    private void KeepVisible(int idx)
    {
        var row = idx / Cols;
        if (row < _scroll) _scroll = row;
        if (row >= _scroll + VisRows) _scroll = row - VisRows + 1;
    }

    private void Buy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (tree.Owns(u.Id)) _msg = "ALREADY LIT — PURCHASES ARE PERMANENT.";
        else if (tree.Purchase(u.Id)) _msg = $"LIT: {u.Name}.";
        else if (tree.MemoryDust < u.Cost) _msg = "NOT ENOUGH MEMORY DUST.";
        else _msg = "LOCKED — LIGHT ITS PREREQUISITES FIRST.";
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, MemoryDustTree tree) => Draw(b, tree, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, MemoryDustTree tree, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xC0));
        _ui.TextCenterBig(b, "DUST", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "BLESSINGS  ·  INVESTMENT  ·  UPGRADES", 960, 80, Slate, UiTypography.Secondary);

        DrawOverview(b, tree, hit, clicked);
        DrawGrid(b, tree, hit, clicked);
        DrawDetail(b, tree, hit, clicked);
        if (DevDustDebug) DrawDebug(b);
    }

    private void DrawOverview(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.Panel(b, OverviewPanel);
        _ui.TextCenterBig(b, "MEMORY DUST", OverviewPanel.Center.X, OverviewPanel.Y + 20, Gold, UiTypography.SectionTitle);

        var crest = new Rectangle(OverviewPanel.Center.X - 44, OverviewPanel.Y + 60, 88, 88);
        if (_ui.Assets.Get("ui_memory_dust") is { } ic) b.Draw(ic, crest, Violet);
        else _ui.Diamond(b, crest, Violet);

        var owned = tree.All.Count(u => tree.Owns(u.Id));
        var invested = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        _ui.TextCenterBig(b, "CURRENT BALANCE", OverviewPanel.Center.X, OverviewPanel.Y + 162, Slate, UiTypography.Secondary);
        _ui.TextCenterBig(b, $"{tree.MemoryDust:N0}", OverviewPanel.Center.X, OverviewPanel.Y + 188, Violet, UiTypography.PrimaryValue);

        _ui.Fill(b, new Rectangle(OverviewPanel.X + 24, OverviewPanel.Y + 236, OverviewPanel.Width - 48, 2), Dim);
        DrawStat(b, OverviewPanel.Y + 252, "BLESSINGS ACTIVE", $"{owned} / {tree.All.Count}");
        DrawStat(b, OverviewPanel.Y + 290, "TOTAL INVESTED", $"{invested:N0}");
        DrawStat(b, OverviewPanel.Y + 328, "TREE VALUE", $"{tree.TotalTreeCost:N0}");

        // FILTER BY CATEGORY — the real UnlockEffect axis (with counts).
        _ui.Fill(b, new Rectangle(OverviewPanel.X + 24, OverviewPanel.Y + 372, OverviewPanel.Width - 48, 2), Dim);
        _ui.TextBig(b, "FILTER BY CATEGORY", OverviewPanel.X + 30, OverviewPanel.Y + 388, Gold, UiTypography.Body);

        var cats = new (UnlockEffect? Cat, string Label)[]
        {
            (null, "ALL"), (UnlockEffect.Amplifier, "AMPLIFIER"), (UnlockEffect.Expansion, "EXPANSION"), (UnlockEffect.Convenience, "CONVENIENCE"),
        };
        var y = OverviewPanel.Y + 426;
        foreach (var (cat, label) in cats)
        {
            var row = new Rectangle(OverviewPanel.X + 26, y, OverviewPanel.Width - 52, 46);
            if (UiKit.ClickedIn(row, hit, clicked)) { _filter = cat; _scroll = 0; }
            var active = _filter?.Equals(cat) ?? cat is null;
            _ui.Fill(b, row, active ? new Color(0x3A, 0x2E, 0x52) : new Color(0x16, 0x12, 0x20, 0xC0));
            var col = cat is { } c ? CatColor(c) : Bone;
            _ui.Fill(b, new Rectangle(row.X, row.Y, 4, row.Height), col);
            _ui.TextBig(b, label, row.X + 18, row.Y + 12, active ? Bone : Slate, UiTypography.Body);
            var count = cat is { } cc ? tree.All.Count(u => u.Effect == cc) : tree.All.Count;
            _ui.TextRightBig(b, $"{count}", row.Right - 18, row.Y + 12, col, UiTypography.Body);
            y += 52;
        }

        if (_ui.Button(b, new Rectangle(OverviewPanel.X + 30, OverviewPanel.Bottom - 78, OverviewPanel.Width - 60, 54), "RESET", hit, clicked, enabled: _filter is not null))
        { _filter = null; _scroll = 0; }
    }

    private void DrawStat(SpriteBatch b, int y, string label, string value)
    {
        _ui.TextBig(b, label, OverviewPanel.X + 30, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, value, OverviewPanel.Right - 30, y, Bone, UiTypography.Body);
    }

    private void DrawGrid(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.Panel(b, GridPanel);
        var owned = tree.All.Count(u => tree.Owns(u.Id));
        _ui.TextCenterBig(b, "DUST BLESSINGS", GridPanel.Center.X, GridPanel.Y + 20, Gold, UiTypography.SectionTitle);
        _ui.TextRightBig(b, $"{owned} / {tree.All.Count} LIT", GridPanel.Right - 28, GridPanel.Y + 24, Slate, UiTypography.Secondary);

        var list = Filtered(tree);
        var maxScroll = Math.Max(0, (list.Count - 1) / Cols - (VisRows - 1));
        _scroll = Math.Clamp(_scroll, 0, maxScroll);

        for (var vis = 0; vis < Cols * VisRows; vis++)
        {
            var idx = _scroll * Cols + vis;
            if (idx >= list.Count) break;
            var u = list[idx];
            var card = Card(vis);
            var isOwned = tree.Owns(u.Id);
            var buyable = tree.CanUnlock(u.Id);
            var sel = u.Id == _selectedId;
            if (UiKit.ClickedIn(card, hit, clicked)) _selectedId = u.Id;

            _ui.Fill(b, card, isOwned ? new Color(0x2A, 0x22, 0x10, 0xF0) : new Color(0x16, 0x12, 0x20, 0xE0));
            var edge = sel ? Bone : isOwned ? Gold : buyable ? CatColor(u.Effect) : Dim;
            var t = sel ? 4 : 3;
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - t, card.Width, t), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Y, t, card.Height), edge);
            _ui.Fill(b, new Rectangle(card.Right - t, card.Y, t, card.Height), edge);

            var iconTint = isOwned ? Gold : buyable ? CatColor(u.Effect) : new Color(0x50, 0x50, 0x5C);
            var ib = new Rectangle(card.Center.X - 26, card.Y + 16, 52, 52);
            if (_ui.Assets.Get("ui_memory_dust") is { } ic) b.Draw(ic, ib, iconTint);
            else _ui.Diamond(b, ib, iconTint);

            _ui.TextCenterBig(b, u.Name, card.Center.X, card.Y + 76, isOwned || buyable ? Bone : Slate, UiTypography.Secondary);
            var state = isOwned ? "LIT" : buyable ? "AVAILABLE" : "LOCKED";
            _ui.TextCenter(b, state, card.Center.X, card.Bottom - 46, isOwned ? Gold : buyable ? Met : Slate);
            if (!isOwned)
            {
                if (_ui.Assets.Get("ui_memory_dust") is { } di) b.Draw(di, new Rectangle(card.Center.X - 42, card.Bottom - 26, 18, 18), buyable ? Violet : Dim);
                _ui.TextCenter(b, $"{u.Cost}", card.Center.X + 8, card.Bottom - 24, buyable ? Bone : Slate);
            }
        }

        if (maxScroll > 0)
            _ui.TextCenter(b, $"ROW {_scroll + 1} / {maxScroll + 1}   ·   SCROLL", GridPanel.Center.X, GridPanel.Bottom - 30, Slate);
    }

    private void DrawDetail(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.Panel(b, DetailPanel);
        var u = Selected(tree);
        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var cat = CatColor(u.Effect);

        _ui.TextCenterBig(b, "BLESSING DETAIL", DetailPanel.Center.X, DetailPanel.Y + 18, Gold, UiTypography.SectionTitle);

        var icon = new Rectangle(DetailPanel.Center.X - 52, DetailPanel.Y + 58, 104, 104);
        if (_ui.Assets.Get("ui_memory_dust") is { } ic) b.Draw(ic, icon, isOwned ? Gold : cat);
        else _ui.Diamond(b, icon, isOwned ? Gold : cat);

        _ui.TextCenterBig(b, u.Name, DetailPanel.Center.X, DetailPanel.Y + 176, isOwned ? Gold : buyable ? Bone : Slate, UiTypography.PanelTitle);
        var state = isOwned ? "LIT" : buyable ? "AVAILABLE" : "LOCKED";
        _ui.TextCenterBig(b, $"{CatName(u.Effect)}  ·  {state}", DetailPanel.Center.X, DetailPanel.Y + 210, isOwned ? Gold : buyable ? Met : Slate, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 244, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "EFFECT", DetailPanel.X + 28, DetailPanel.Y + 258, Slate, UiTypography.Secondary);
        DrawWrapped(b, Cap(u.Description), DetailPanel.X + 28, DetailPanel.Y + 290, DetailPanel.Width - 56, Bone);
        if (u.GrantsKeystone is not null)
            _ui.TextBig(b, "GRANTS A KEYSTONE — A BUILD-DEFINING CHOICE.", DetailPanel.X + 28, DetailPanel.Y + 356, Violet, UiTypography.Secondary);

        // Prerequisites (real Requires), each with an owned check.
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 392, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "PREREQUISITES", DetailPanel.X + 28, DetailPanel.Y + 406, Gold, UiTypography.Secondary);
        var py = DetailPanel.Y + 440;
        if (u.Requires.Count == 0) _ui.TextBig(b, "NONE — A ROOT BLESSING.", DetailPanel.X + 30, py, Slate, UiTypography.Body);
        else
            foreach (var reqId in u.Requires)
            {
                var req = tree.All.FirstOrDefault(x => x.Id == reqId);
                var got = tree.Owns(reqId);
                DrawTick(b, new Rectangle(DetailPanel.X + 30, py + 2, 20, 18), got);
                _ui.TextBig(b, req?.Name ?? reqId, DetailPanel.X + 60, py, got ? Bone : Slate, UiTypography.Body);
                py += 34;
            }

        // Cost + UPGRADE.
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Bottom - 156, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "COST", DetailPanel.X + 28, DetailPanel.Bottom - 138, Slate, UiTypography.Body);
        if (isOwned) _ui.TextRightBig(b, "OWNED", DetailPanel.Right - 28, DetailPanel.Bottom - 140, Gold, UiTypography.PanelTitle);
        else
        {
            if (_ui.Assets.Get("ui_memory_dust") is { } di) b.Draw(di, new Rectangle(DetailPanel.Right - 150, DetailPanel.Bottom - 144, 34, 34), Violet);
            _ui.TextRightBig(b, $"{u.Cost:N0}", DetailPanel.Right - 28, DetailPanel.Bottom - 140, buyable ? Bone : Ember, UiTypography.PanelTitle);
        }

        var label = isOwned ? "LIT" : "UPGRADE";
        if (_ui.Button(b, new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 92, DetailPanel.Width - 80, 68), label, hit, clicked, enabled: buyable))
            Buy(tree, u);
        if (!isOwned && !buyable)
            _ui.TextCenter(b, tree.MemoryDust < u.Cost ? "NOT ENOUGH DUST" : "LIGHT ITS PREREQUISITES FIRST", DetailPanel.Center.X, DetailPanel.Bottom - 108, Ember);
    }

    private static string Cap(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant() : s;

    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 26; line = w; }
            else line = probe;
        }
        if (line.Length > 0) _ui.Text(b, line, x, y, c);
    }

    private void DrawTick(SpriteBatch b, Rectangle r, bool on)
    {
        if (on) { for (var k = 0; k < 5; k++) _ui.Fill(b, new Rectangle(r.X + k, r.Bottom - 6 + k / 2 - 2, 3, 3), Met); for (var k = 0; k < 10; k++) _ui.Fill(b, new Rectangle(r.X + 5 + k, r.Bottom - 2 - k, 3, 3), Met); }
        else _ui.Fill(b, new Rectangle(r.X + 2, r.Center.Y - 2, r.Width - 4, 4), Slate);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { OverviewPanel, GridPanel, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav DUST  filter {(_filter?.ToString() ?? "ALL")}  sel {_selectedId}", 434, 112, Gold, UiTypography.Secondary);
    }
}
