using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The MASTERY TREE: a Nen hexagon you walk outward to specialise, given a panel of its own.
/// </summary>
/// <remarks>
/// <para>
/// Six arms, one per Form, radiating from a centre "YOU". Each arm is a PATH — two minor stat nodes, a
/// notable with a real effect, and the corner Mastery that claims your affinity. You spend mastery points
/// (earned by going deeper) walking a path node by node; a node lights only when a neighbour is already
/// taken. Opposite Forms sit at opposite corners, so committing an arm genuinely turns its opposite weak —
/// the tree IS the identity choice, drawn like a small Path-of-Exile web.
/// </para>
/// <para>
/// The tree used to be crushed into the left 40% of the screen next to an equal-sized skills panel; the
/// player asked for it to breathe. It now owns the left two-thirds at a far larger radius, and the build
/// it powers — four woven skills and the keystone sockets — sits in a slim sidebar on the right. It edits
/// a <see cref="PlayerLoadout"/> and a <see cref="MasteryTree"/> in place; the host persists both.
/// </para>
/// </remarks>
public sealed class BuildScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color SbBg = new(0x0F, 0x0D, 0x15);
    private static readonly Color Hi = new(0x2A, 0x24, 0x14);
    private static readonly Color Path = new(0x39, 0x33, 0x44);
    private static readonly Color Verd = new(0x5A, 0x9A, 0x4A);   // "can take" — a live, affordable node

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x52, 0x45, 0x7E), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };

    private static string Short(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };

    private readonly UiKit _ui;
    private string _msg = "";
    private string _hoverInfo = "";

    public BuildScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    // ── Tree geometry. Recentred into the left two-thirds, at a radius that lets the arms breathe.
    //    Cy/R are sized so the top and bottom corner Masteries clear the canvas edges (270 tall). ─────
    private const int Cx = 150, Cy = 130, R = 116;
    private static Point Centre => new(Cx, Cy);
    private static Point Corner(Form arm)
    {
        var a = (-90 + (int)arm * 60) * MathF.PI / 180f;
        return new Point((int)(Cx + R * MathF.Cos(a)), (int)(Cy + R * MathF.Sin(a)));
    }
    private static Point NodePos(MasteryNode n)
    {
        if (n.Kind == MasteryKind.Start) return Centre;

        // A BRIDGE sits between its two arms, on an inner ring — the cross-link, drawn off the spokes.
        if (n.Link is { } link)
        {
            float mx = (Corner(n.Arm).X + Corner(link).X) / 2f - Cx;
            float my = (Corner(n.Arm).Y + Corner(link).Y) / 2f - Cy;
            var len = MathF.Max(1f, MathF.Sqrt(mx * mx + my * my));
            return new Point(Cx + (int)(mx / len * R * 0.42f), Cy + (int)(my / len * R * 0.42f));
        }

        // Rings 1-2 FAN into two routes flanking the spoke — the AGGRESSIVE path (_1/_2) on one side, the
        // SUSTAIN path (_b1/_b2) on the other — that converge again at the notable (ring 3, on the spoke). A
        // constant angular spread reads as two parallel climbs to the same corner.
        var baseAng = (-90 + (int)n.Arm * 60) * MathF.PI / 180f;
        if (n.Ring is 1 or 2)
        {
            var side = n.Id.EndsWith("b1") || n.Id.EndsWith("b2") ? 1f : -1f;
            var spread = (n.Ring == 1 ? 15f : 9f) * MathF.PI / 180f;
            var ang = baseAng + side * spread;
            var rad = R * (n.Ring / 4f);
            return new Point(Cx + (int)(rad * MathF.Cos(ang)), Cy + (int)(rad * MathF.Sin(ang)));
        }

        var c = Corner(n.Arm);
        var t = n.Ring / 4f;
        return new Point(Cx + (int)((c.X - Cx) * t), Cy + (int)((c.Y - Cy) * t));
    }

    private static readonly Rectangle ResetBtn = new(12, 32, 54, 13);
    private static int NodeRadius(MasteryKind k) => k switch
    {
        MasteryKind.Start => 15, MasteryKind.Mastery => 16, MasteryKind.Notable => 10, _ => 7,
    };

    // ── The build sidebar (right). Skills as compact three-line cards, keystones as chips below. ─────
    private const int SbX = 300;
    private static readonly Rectangle Sidebar = new(SbX, 22, 480 - SbX - 4, 210);
    private static Rectangle SkillCard(int i) => new(SbX + 4, 40 + i * 36, 170, 33);
    private static Rectangle CSource(int i) { var c = SkillCard(i); return new(c.X, c.Y + 11, 82, 11); }
    private static Rectangle CForm(int i) { var c = SkillCard(i); return new(c.X + 86, c.Y + 11, 84, 11); }
    private static Rectangle CVow(int i) { var c = SkillCard(i); return new(c.X, c.Y + 22, 170, 11); }
    private static Rectangle CRemove(int i) { var c = SkillCard(i); return new(c.Right - 13, c.Y, 13, 11); }
    private static Rectangle KeystoneChip(int i) => new(SbX + 4, 197 + i * 13, 170, 12);

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, MemoryDustTree tree)
    {
        Tree = tree;
        if (!clicked) return;

        if (Mastery.Spent > 0 && ResetBtn.Contains(mouse)) { Mastery.Respec(); _msg = "MASTERY RESET — POINTS RETURNED."; Dirty = true; return; }

        // ── The tree: click a node to take it, if it's live. ────────────────────────────────────
        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Kind == MasteryKind.Start) continue;
            var p = NodePos(node);
            var rad = NodeRadius(node.Kind) + 2;
            if (Math.Abs(mouse.X - p.X) > rad || Math.Abs(mouse.Y - p.Y) > rad) continue;

            if (Mastery.Take(node.Id)) { _msg = ""; Dirty = true; }
            else _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
                Mastery.Available <= 0 ? "NO MASTERY POINTS — GO DEEPER." :
                node.Kind == MasteryKind.Mastery && Mastery.MasteryForm() is not null ? "YOU'VE ALREADY MASTERED A FORM." :
                "TAKE A CONNECTED NODE FIRST.";
            return;
        }

        // ── The sidebar: skills and keystones. ───────────────────────────────────────────────────
        var known = DustEffects.KnownVows(tree);
        var skills = Loadout.Skills;
        for (var i = 0; i < skills.Count; i++)
        {
            if (Cycle(CSource(i), mouse, out var d1)) { Loadout.CycleSource(i, d1); Dirty = true; return; }
            if (Cycle(CForm(i), mouse, out var d2)) { Loadout.CycleForm(i, d2); Dirty = true; return; }
            if (Cycle(CVow(i), mouse, out var d3)) { Loadout.CycleVow(i, d3, known); Dirty = true; return; }
            if (CRemove(i).Contains(mouse)) { Loadout.RemoveSkill(i); Dirty = true; return; }
        }
        if (skills.Count < PlayerLoadout.MaxSkills && SkillCard(skills.Count).Contains(mouse)) { Loadout.AddSkill(); Dirty = true; return; }

        var learned = DustEffects.LearnedKeystones(tree);
        for (var i = 0; i < learned.Count; i++)
        {
            if (!KeystoneChip(i).Contains(mouse)) continue;
            if (!Loadout.ToggleKeystone(learned[i].Id, learned)) _msg = $"ONLY {PlayerLoadout.MaxKeystones} SOCKETS.";
            else _msg = "";
            Dirty = true;
            return;
        }
    }

    private static bool Cycle(Rectangle r, Point mouse, out int dir)
    {
        dir = 0;
        if (!r.Contains(mouse)) return false;
        dir = mouse.X < r.Center.X ? -1 : 1;
        return true;
    }

    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        _hoverInfo = "";
        _ui.Scrim(b, 0.6f);   // darken the scene behind the tree so the nodes and paths read cleanly

        // ── Header (top-left, clear of the tree's top corner). ───────────────────────────────────
        _ui.Title(b, "MASTERY TREE");
        _ui.Text(b, $"POINTS  {Mastery.Available}", 26, 15, Mastery.Available > 0 ? Gold : Slate);
        if (Mastery.Spent > 0)
        {
            _ui.Fill(b, ResetBtn, ResetBtn.Contains(mouse) ? Hi : PanelBg);
            _ui.TextCenter(b, "RESET", ResetBtn.Center.X, ResetBtn.Y + 3, ResetBtn.Contains(mouse) ? Ember : Slate);
        }
        var aff = Mastery.MasteryForm();

        // Paths first, under the nodes: a segment lit if BOTH ends are taken, dim otherwise.
        foreach (var node in MasteryCatalog.Nodes)
            foreach (var pre in node.Prereqs)
                if (MasteryCatalog.ById(pre) is { } p)
                    Line(b, NodePos(p), NodePos(node), Mastery.IsTaken(node.Id) && Mastery.IsTaken(pre) ? Gold : Path);

        foreach (var node in MasteryCatalog.Nodes)
            DrawNode(b, node, mouse, aff);

        DrawSidebar(b, mouse, tree);   // may also set _hoverInfo (keystone blurbs), so draw the line after

        // One info line under the tree: a hovered node's effect, else the last message, else the hint.
        var info = _hoverInfo.Length > 0 ? _hoverInfo
            : _msg.Length > 0 ? _msg
            : "WALK A PATH TO A CORNER TO MASTER THAT FORM.";
        var infoColor = _hoverInfo.Length > 0 ? Bone : _msg.Length > 0 ? Ember : Dim;
        _ui.Text(b, info, 12, 226, infoColor);
    }

    private void DrawSidebar(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        _ui.Fill(b, Sidebar, SbBg);
        _ui.Fill(b, new Rectangle(Sidebar.X, Sidebar.Y, 1, Sidebar.Height), Path);   // a seam, tree | build

        _ui.Text(b, "YOUR SKILLS", SbX + 6, 26, Slate);
        var skills = Loadout.Skills;
        for (var i = 0; i < PlayerLoadout.MaxSkills; i++)
        {
            var card = SkillCard(i);
            _ui.Fill(b, card, PanelBg);
            if (i < skills.Count)
            {
                var s = skills[i];
                _ui.Text(b, $"SLOT {i + 1}", card.X + 4, card.Y + 2, Slate);
                var rm = CRemove(i);
                _ui.TextCenter(b, "X", rm.Center.X, rm.Y + 2, rm.Contains(mouse) ? Ember : Slate);
                DrawCell(b, CSource(i), s.Source.ToString().ToUpperInvariant(), SourceColor.GetValueOrDefault(s.Source, Bone), mouse);
                DrawCell(b, CForm(i), Short(s.Form), Bone, mouse);
                var vow = Weaving.ById(s.VowId);
                DrawCell(b, CVow(i), vow is null ? "NO VOW" : vow.Short.ToUpperInvariant(), vow is null ? Dim : Gold, mouse);
            }
            else if (i == skills.Count)
                _ui.TextCenter(b, "+ ADD SKILL", card.Center.X, card.Y + 14, card.Contains(mouse) ? Gold : Slate);
            else _ui.TextCenter(b, "— LOCKED —", card.Center.X, card.Y + 14, Dim);
        }

        var learned = DustEffects.LearnedKeystones(tree);
        var kHead = learned.Count == 0 ? "KEYSTONES — LEARN IN DUST (P)"
            : learned.Count > 3 ? $"KEYSTONES — WEAR {PlayerLoadout.MaxKeystones} · {learned.Count} LEARNED"
            : $"KEYSTONES — WEAR {PlayerLoadout.MaxKeystones}";
        _ui.Text(b, kHead, SbX + 6, 184, Slate);
        for (var i = 0; i < learned.Count && i < 3; i++)
        {
            var chip = KeystoneChip(i);
            var worn = Loadout.HasKeystone(learned[i].Id);
            var hover = chip.Contains(mouse);
            _ui.Fill(b, chip, worn ? Hi : PanelBg);
            if (worn) _ui.Fill(b, new Rectangle(chip.X, chip.Y, 2, chip.Height), Gold);
            _ui.Text(b, learned[i].Name, chip.X + 6, chip.Y + 4, worn ? Gold : hover ? Bone : Slate);
            if (hover) _hoverInfo = learned[i].Blurb.ToUpperInvariant();
        }
    }

    private void DrawNode(SpriteBatch b, MasteryNode node, Point mouse, Form? aff)
    {
        var p = NodePos(node);
        var rad = NodeRadius(node.Kind);
        var box = new Rectangle(p.X - rad, p.Y - rad, rad * 2, rad * 2);
        var hover = Math.Abs(mouse.X - p.X) <= rad + 2 && Math.Abs(mouse.Y - p.Y) <= rad + 2;

        var taken = Mastery.IsTaken(node.Id);
        var canTake = Mastery.CanTake(node.Id);
        var fill = taken ? Gold : canTake ? Hi : PanelBg;
        var edge = taken ? Gold : canTake ? Verd : Path;

        if (node.Kind == MasteryKind.Mastery)
        {
            var wide = new Rectangle(p.X - 30, p.Y - 11, 60, 22);
            _ui.Fill(b, wide, taken ? Hi : PanelBg);
            Outline(b, wide, node.Arm == aff ? Gold : hover ? Bone : edge, node.Arm == aff ? 2 : 1);
            _ui.TextCenter(b, Short(node.Arm), wide.Center.X, wide.Y + 7, node.Arm == aff ? Gold : taken ? Gold : Slate);
        }
        else if (node.Kind == MasteryKind.Start)
        {
            _ui.Fill(b, box, PanelBg);
            Outline(b, box, Slate, 1);
            _ui.TextCenter(b, "YOU", p.X, p.Y - 3, Bone);
        }
        else
        {
            _ui.Fill(b, box, fill);
            Outline(b, box, hover ? Bone : edge, 1);
        }

        if (hover && node.Kind != MasteryKind.Start)
            _hoverInfo = node.Kind == MasteryKind.Mastery ? $"{Short(node.Arm)} MASTERY — SPECIALISE IN {Short(node.Arm)}" : node.Label;
    }

    private void DrawCell(SpriteBatch b, Rectangle r, string text, Color color, Point mouse)
    {
        _ui.Fill(b, r, r.Contains(mouse) ? Hi : PanelBg);
        _ui.Text(b, "<", r.X + 1, r.Y + 3, Slate);
        _ui.Text(b, ">", r.Right - 7, r.Y + 3, Slate);
        _ui.TextCenter(b, text, r.Center.X, r.Y + 3, color);
    }

    private void Line(SpriteBatch b, Point a, Point c, Color col)
    {
        var dx = c.X - a.X; var dy = c.Y - a.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0) return;
        for (var i = 0; i <= steps; i++)
            _ui.Fill(b, new Rectangle(a.X + dx * i / steps - 1, a.Y + dy * i / steps - 1, 2, 2), col);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
