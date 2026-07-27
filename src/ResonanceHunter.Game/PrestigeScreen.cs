using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// Memory Dust — a real CONSTELLATION of permanent unlocks, not a list. Each unlock is a star; its
/// prerequisites are the dotted lines that lead to it. Click a star to light it. Nothing ever resets.
/// </summary>
/// <remarks>
/// Rebuilt from a vertical text list (which sat pointlessly on top of the constellation backdrop) into
/// the constellation itself: nodes are laid out in columns by prerequisite depth and wired to what they
/// require, so the shape of the progression is something you SEE and navigate, not read.
/// </remarks>
public sealed class PrestigeScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x38, 0x38, 0x46);
    private static readonly Color Bloom = new(0x8A, 0x6A, 0xB0);

    private readonly UiKit _ui;
    private readonly List<(MemoryDustUnlock U, Point Pos)> _nodes = new();
    private readonly Dictionary<string, Point> _posById = new();
    private int _cursor;
    private string _msg = "";
    private float _anim;
    private KeyboardState _prevKeys;

    public PrestigeScreen(UiKit ui, MemoryDustTree tree)
    {
        _ui = ui;
        LayOutConstellation(tree);
    }

    /// <summary>Place each unlock as a star: column = how deep its prerequisite chain runs.</summary>
    private void LayOutConstellation(MemoryDustTree tree)
    {
        var byId = tree.All.ToDictionary(u => u.Id);
        var memo = new Dictionary<string, int>();

        int Depth(string id)
        {
            if (memo.TryGetValue(id, out var d)) return d;
            var u = byId[id];
            d = u.Requires.Count == 0 ? 0 : 1 + u.Requires.Max(Depth);
            memo[id] = d;
            return d;
        }

        var withDepth = tree.All.Select(u => (u, d: Depth(u.Id))).ToList();
        var maxD = withDepth.Max(x => x.d);

        const int xL = 184, xR = 1736, yT = 216, yB = 752;
        foreach (var col in withDepth.GroupBy(x => x.d).OrderBy(g => g.Key))
        {
            var d = col.Key;
            var x = maxD == 0 ? (xL + xR) / 2 : xL + d * (xR - xL) / maxD;
            var items = col.OrderBy(x => x.u.Cost).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var y = yT + (int)((i + 0.5f) * (yB - yT) / items.Count);
                var pos = new Point(x, y);
                _nodes.Add((items[i].u, pos));
                _posById[items[i].u.Id] = pos;
            }
        }
    }

    public void Update(KeyboardState keys, Point mouse, bool clicked, MemoryDustTree tree)
    {
        if (Pressed(keys, Keys.Left)) Move(-1);
        if (Pressed(keys, Keys.Right)) Move(1);
        if (Pressed(keys, Keys.Up)) Move(-1);
        if (Pressed(keys, Keys.Down)) Move(1);
        if (Pressed(keys, Keys.Enter)) Buy(tree, _nodes[_cursor].U);

        var hit = new Point(mouse.X * 4, mouse.Y * 4);
        for (var i = 0; i < _nodes.Count; i++)
        {
            var hitBox = new Rectangle(_nodes[i].Pos.X - 36, _nodes[i].Pos.Y - 36, 72, 72);
            if (UiKit.ClickedIn(hitBox, hit, clicked)) { _cursor = i; Buy(tree, _nodes[i].U); }
        }

        _prevKeys = keys;
    }

    private void Move(int d) => _cursor = (_cursor + d + _nodes.Count) % _nodes.Count;

    private void Buy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (tree.Owns(u.Id)) _msg = "ALREADY LIT — PURCHASES ARE PERMANENT.";
        else if (tree.Purchase(u.Id)) _msg = $"LIT: {u.Name}.";
        else if (tree.MemoryDust < u.Cost) _msg = "NOT ENOUGH MEMORY DUST.";
        else _msg = "LOCKED — LIGHT ITS PREREQUISITES FIRST.";
    }

    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, MemoryDustTree tree) => Draw(b, tree, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, MemoryDustTree tree, Point mouse, bool clicked)
    {
        _anim += 1f / 60f;
        var pulse = 0.5f + 0.5f * MathF.Sin(_anim * 4f);

        // Mute the decorative backdrop so the FUNCTIONAL stars (our nodes) are unmistakably the layer
        // you interact with, not the painted ones behind them.
        _ui.Scrim(b, 0.62f);

        // ═══ HEADER ══════════════════════════════════════════════════════════════════════════════
        var owned = tree.All.Count(u => tree.Owns(u.Id));
        _ui.Title(b, "MEMORY DUST", $"{owned} / {tree.All.Count} LIT");
        // Dust (and Gleam/Materials) are the shared currency pills top-right (Game1.DrawCurrencyPills).
        _ui.TextRight(b, "NOTHING HERE EVER RESETS", 1872, 88, Slate);
        var spent = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        _ui.Bar(b, 48, 136, 1824, 16, (float)spent / tree.TotalTreeCost, Gold);

        // ═══ CONNECTOR LINES — the prerequisite constellation, drawn as dotted star-lines ══════════
        foreach (var (u, pos) in _nodes)
            foreach (var reqId in u.Requires)
                if (_posById.TryGetValue(reqId, out var from))
                {
                    // A LIT path is a solid gold wire; an unwalked one stays a faint dotted trail.
                    var walked = tree.Owns(u.Id) && tree.Owns(reqId);
                    if (walked) SolidLine(b, from, pos, Gold, 8);
                    else if (tree.Owns(reqId)) SolidLine(b, from, pos, Slate, 4); // reachable next step
                    else DottedLine(b, from, pos, Dim);
                }

        // ═══ NODES — a framed, icon-bearing chip. Unmistakably the thing you click. ════════════════
        for (var i = 0; i < _nodes.Count; i++)
        {
            var (u, pos) = _nodes[i];
            var isOwned = tree.Owns(u.Id);
            var buyable = tree.CanUnlock(u.Id);
            var selected = i == _cursor;

            var edge = isOwned ? Gold : buyable ? Bone : Slate;
            var box = new Rectangle(pos.X - 44, pos.Y - 44, 88, 88);

            // Glow behind lit / buyable nodes (buyable ones breathe so they read as "you can take this").
            if (isOwned || buyable)
            {
                var g = (int)(isOwned ? 16 : 8 + pulse * 16);
                _ui.Fill(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2),
                    (isOwned ? Gold : Bloom) * 0.25f);
            }

            // Body + a full highlight border — this is what made them invisible before.
            _ui.Fill(b, box, isOwned ? new Color(0x3A, 0x2C, 0x10) : new Color(0x14, 0x13, 0x1C));
            Border(b, box, edge, selected ? 8 : 4);

            // The icon inside.
            var icon = _ui.Assets.Get("ui_memory_dust");
            var ib = new Rectangle(box.X + 16, box.Y + 16, 56, 56);
            if (icon is not null) b.Draw(icon, ib, isOwned ? Gold : buyable ? Bone : new Color(0x50, 0x50, 0x5C));
            else DrawDiamond(b, pos, 24, isOwned || buyable ? edge : Dim);

            if (selected) Reticle(b, new Rectangle(box.X - 12, box.Y - 12, box.Width + 24, box.Height + 24), Bone);
        }

        // ═══ DETAIL BAR — the selected star ═══════════════════════════════════════════════════════
        var sel = _nodes[Math.Clamp(_cursor, 0, _nodes.Count - 1)].U;
        var selOwned = tree.Owns(sel.Id);
        var selBuyable = tree.CanUnlock(sel.Id);
        _ui.Panel(b, new Rectangle(0, 800, 1920, 128));

        // The bar is DARK glass, so it takes LIGHT text — it used to use dark parchment inks that were
        // invisible here. Buyable-ness shows in the gold cost; LIT is the earned gold the player chased.
        _ui.Text(b, sel.Name, 40, 816, selOwned ? Gold : selBuyable ? Bone : Slate);
        if (selOwned) _ui.TextRight(b, "LIT", 1880, 816, Gold);
        else
        {
            if (_ui.Assets.Get("ui_memory_dust") is { } d2) b.Draw(d2, new Rectangle(1784, 816, 36, 36), selBuyable ? Bone : Dim);
            _ui.TextRight(b, $"{sel.Cost}", 1776, 816, selBuyable ? Gold : Slate);
        }
        _ui.Text(b, _msg.Length > 0 ? _msg : sel.Description, 40, 856, _msg.Length > 0 ? Gold : Bone);
        _ui.TextRight(b, "CLICK A STAR TO LIGHT IT     P  BACK", 1880, 896, Slate);
    }

    /// <summary>A full highlight border around a node — what makes it read as a clickable chip.</summary>
    private void Border(SpriteBatch b, Rectangle r, Color col, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), col);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), col);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), col);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), col);
    }

    /// <summary>A solid connection wire between two nodes (Bresenham-ish, thickness in pixels).</summary>
    private void SolidLine(SpriteBatch b, Point a, Point c, Color col, int thickness)
    {
        var dx = c.X - a.X;
        var dy = c.Y - a.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0) return;

        for (var s = 0; s <= steps; s++)
        {
            var x = a.X + dx * s / steps;
            var y = a.Y + dy * s / steps;
            _ui.Fill(b, new Rectangle(x, y, thickness, thickness), col);
        }
    }

    /// <summary>A dotted line between two stars — the constellation's connective tissue.</summary>
    private void DottedLine(SpriteBatch b, Point a, Point c, Color col)
    {
        const int steps = 12;
        for (var s = 1; s < steps; s++)
        {
            var x = a.X + (c.X - a.X) * s / steps;
            var y = a.Y + (c.Y - a.Y) * s / steps;
            _ui.Fill(b, new Rectangle(x, y, 4, 4), col);
        }
    }

    private void DrawDiamond(SpriteBatch b, Point p, int r, Color col)
    {
        for (var dy = -r; dy <= r; dy++)
        {
            var w = r - Math.Abs(dy);
            _ui.Fill(b, new Rectangle(p.X - w, p.Y + dy, w * 2 + 1, 1), col);
        }
    }

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 20, t = 4;
        _ui.Fill(b, new Rectangle(r.X, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Bottom - len, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }
}
