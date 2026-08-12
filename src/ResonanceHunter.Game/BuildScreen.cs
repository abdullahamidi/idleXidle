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
/// The BUILD screen (nav: BUILD): a four-panel overview of the auto-skill loadout — a build summary, the
/// Source/Form/Vow composition, the four equipped aura cards, and the passives / resonance / notes column.
/// An EDIT sub-view (the mastery tree + skill sidebar) is one button away, so all editing is preserved.
/// </summary>
/// <remarks>
/// Built to the Build production spec (rev 1). All data is REAL: the game has ONE live loadout (not the
/// reference's named "saved loadouts" presets, which the spec's own layout drops), so the screen shows the
/// current build. Resonance is the real source composition of the equipped skills; passives are the taken
/// mastery nodes; there is no numeric "trait bonus %" table, so that is shown as the real passive list.
/// </remarks>
public sealed class BuildScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color Quiet = new(0x16, 0x12, 0x20, 0xE0);
    private static readonly Color SbBg = new(0x0F, 0x0D, 0x15);
    private static readonly Color Hi = new(0x2A, 0x24, 0x14);
    private static readonly Color Path = new(0x39, 0x33, 0x44);
    private static readonly Color Verd = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Purple = new(0x8A, 0x5A, 0xC8);

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x8A, 0x5A, 0xC8), [Source.Body] = new(0xD6, 0x48, 0x5C),
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
    private bool _editMode;   // false = the overview; true = the mastery-tree + skill editor sub-view

    public BuildScreen(UiKit ui) => _ui = ui;

    public PlayerLoadout Loadout { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();
    public int Power { get; set; }   // host-set: PowerRating (the Build screen has no Hunter reference)
    public int Level { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevBuildDebug { get; set; }

    // ── Spec §4 overview layout. ──
    private static readonly Rectangle SummaryPanel = new(40, 138, 360, 746);
    private static readonly Rectangle CorePanel = new(426, 138, 774, 450);
    private static readonly Rectangle AuraPanel = new(426, 606, 774, 278);
    private static readonly Rectangle PassivePanel = new(1228, 138, 652, 746);
    private static readonly Rectangle EditBtn = new(704, 902, 260, 44);
    private static readonly Rectangle ResetBtn = new(984, 902, 232, 44);
    private static Rectangle AuraCard(int i) => new(452 + i * 182, 648, 168, 198);
    // Under the panel's top crest, which the title now clears too — at y=196 the button was drawn
    // straight through it.
    private static readonly Rectangle ViewTreeBtn = new(1544, 250, 250, 44);

    // ── Edit sub-view: the mastery tree + skill sidebar (unchanged geometry). ──
    private const int Cx = 600, Cy = 520, R = 464;
    private static Point Centre => new(Cx, Cy);
    private static Point Corner(Form arm)
    {
        var a = (-90 + (int)arm * 60) * MathF.PI / 180f;
        return new Point((int)(Cx + R * MathF.Cos(a)), (int)(Cy + R * MathF.Sin(a)));
    }
    private static Point NodePos(MasteryNode n)
    {
        if (n.Kind == MasteryKind.Start) return Centre;
        if (n.Link is { } link)
        {
            float mx = (Corner(n.Arm).X + Corner(link).X) / 2f - Cx;
            float my = (Corner(n.Arm).Y + Corner(link).Y) / 2f - Cy;
            var len = MathF.Max(1f, MathF.Sqrt(mx * mx + my * my));
            return new Point(Cx + (int)(mx / len * R * 0.42f), Cy + (int)(my / len * R * 0.42f));
        }
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
    private static readonly Rectangle TreeResetBtn = new(48, 128, 216, 52);
    private static readonly Rectangle BackBtn = new(48, 128, 216, 52);
    private static int NodeRadius(MasteryKind k) => k switch
    {
        MasteryKind.Start => 60, MasteryKind.Mastery => 64, MasteryKind.Notable => 40, _ => 28,
    };
    private const int SbX = 1200;
    private static readonly Rectangle Sidebar = new(SbX, 200, 1920 - SbX - 16, 728);
    private static Rectangle SkillCard(int i) => new(SbX + 16, 260 + i * 144, 680, 132);
    private static Rectangle CSource(int i) { var c = SkillCard(i); return new(c.X, c.Y + 44, 328, 44); }
    private static Rectangle CForm(int i) { var c = SkillCard(i); return new(c.X + 344, c.Y + 44, 336, 44); }
    private static Rectangle CVow(int i) { var c = SkillCard(i); return new(c.X, c.Y + 88, 680, 44); }
    private static Rectangle CRemove(int i) { var c = SkillCard(i); return new(c.Right - 52, c.Y, 52, 44); }
    private static Rectangle KeystoneChip(int i) => new(SbX + 16, 830 + i * 52, 680, 48);

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, MemoryDustTree tree)
    {
        Tree = tree;
        if (!clicked) return;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        if (!_editMode)
        {
            if (EditBtn.Contains(hit) || ViewTreeBtn.Contains(hit)) { _editMode = true; return; }
            if (ResetBtn.Contains(hit) && Mastery.Spent > 0) { Mastery.Respec(); _msg = "MASTERY RESET."; Dirty = true; }
            return;
        }

        // ── Edit sub-view: tree + sidebar. ──
        if (BackBtn.Contains(hit)) { _editMode = false; return; }
        if (Mastery.Spent > 0 && TreeResetBtn.Contains(hit)) { /* Back occupies the same corner; handled above */ }

        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Kind == MasteryKind.Start) continue;
            var p = NodePos(node);
            var rad = NodeRadius(node.Kind) + 8;
            if (Math.Abs(hit.X - p.X) > rad || Math.Abs(hit.Y - p.Y) > rad) continue;
            if (Mastery.Take(node.Id)) { _msg = ""; Dirty = true; }
            else _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
                Mastery.Available <= 0 ? "NO MASTERY POINTS — GO DEEPER." :
                node.Kind == MasteryKind.Mastery && Mastery.MasteryForm() is not null ? "YOU'VE ALREADY MASTERED A FORM." :
                "TAKE A CONNECTED NODE FIRST.";
            return;
        }

        var known = DustEffects.KnownVows(tree);
        var skills = Loadout.Skills;
        for (var i = 0; i < skills.Count; i++)
        {
            if (Cycle(CSource(i), hit, out var d1)) { Loadout.CycleSource(i, d1); Dirty = true; return; }
            if (Cycle(CForm(i), hit, out var d2)) { Loadout.CycleForm(i, d2); Dirty = true; return; }
            if (Cycle(CVow(i), hit, out var d3)) { Loadout.CycleVow(i, d3, known); Dirty = true; return; }
            if (CRemove(i).Contains(hit)) { Loadout.RemoveSkill(i); Dirty = true; return; }
        }
        if (skills.Count < PlayerLoadout.MaxSkills && SkillCard(skills.Count).Contains(hit)) { Loadout.AddSkill(); Dirty = true; return; }

        var learned = DustEffects.LearnedKeystones(tree);
        for (var i = 0; i < learned.Count && i < 3; i++)
            if (KeystoneChip(i).Contains(hit))
            {
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

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        Tree = tree;
        _hoverInfo = "";
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xD8));

        if (_editMode) { DrawEditor(b, hit, tree); return; }

        _ui.TextCenterBig(b, "BUILD", 960, 24, Gold, UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "SOURCE   ·   FORM   ·   VOW   ·   AURAS", 960, 80, Slate, UiTypography.Secondary);

        DrawSummary(b);
        DrawCore(b);
        DrawAuraCards(b, hit);
        DrawPassives(b, hit, tree);

        Button(b, EditBtn, "EDIT BUILD", hit, true);
        Button(b, ResetBtn, "RESET MASTERY", hit, Mastery.Spent > 0);
        if (DevBuildDebug) DrawDebug(b);
    }

    private void DrawSummary(SpriteBatch b)
    {
        _ui.Panel(b, SummaryPanel);
        _ui.TextCenterBig(b, "BUILD OVERVIEW", SummaryPanel.Center.X, SummaryPanel.Y + 24, Gold, UiTypography.SectionTitle);

        var por = new Rectangle(SummaryPanel.Center.X - 66, SummaryPanel.Y + 72, 132, 132);
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);

        var adept = Mastery.MasteryForm() is { } mf ? $"{Short(mf)} ADEPT" : "SEEKER";
        _ui.TextCenterBig(b, adept, SummaryPanel.Center.X, SummaryPanel.Y + 220, Bone, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"LEVEL {Level}", SummaryPanel.Center.X, SummaryPanel.Y + 254, Gold, UiTypography.Body);
        _ui.TextCenterBig(b, "BUILD POWER", SummaryPanel.Center.X, SummaryPanel.Y + 300, Slate, UiTypography.Secondary);
        _ui.TextCenterBig(b, $"{Power:N0}", SummaryPanel.Center.X, SummaryPanel.Y + 328, Bone, UiTypography.PrimaryValue);

        var skills = Loadout.Skills;
        var focus = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).FirstOrDefault();
        var vows = skills.Count(s => Weaving.ById(s.VowId) is not null);
        var y = SummaryPanel.Y + 400;
        Row(b, SummaryPanel, "SOURCE FOCUS", focus is null ? "—" : focus.Key.ToString().ToUpperInvariant(), ref y);
        Row(b, SummaryPanel, "ACTIVE FORMS", $"{skills.Count}", ref y);
        Row(b, SummaryPanel, "VOW-BOUND", $"{vows}", ref y);
        Row(b, SummaryPanel, "MASTERY", $"{Mastery.Spent} NODES", ref y);
    }

    private void DrawCore(SpriteBatch b)
    {
        _ui.Panel(b, CorePanel);
        _ui.TextCenterBig(b, "CORE COMPOSITION", CorePanel.Center.X, CorePanel.Y + 24, Gold, UiTypography.SectionTitle);
        var skills = Loadout.Skills;
        var focus = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).FirstOrDefault();
        var forms = string.Join("  ·  ", skills.Select(s => Short(s.Form)).Distinct());
        var vowList = skills.Select(s => Weaving.ById(s.VowId)?.Short.ToUpperInvariant()).Where(v => v is not null).Distinct();
        var vows = vowList.Any() ? string.Join("  ·  ", vowList) : "NONE";

        var x = CorePanel.X + 44;
        var y = CorePanel.Y + 90;
        Big(b, "SOURCE", focus is null ? "—" : focus.Key.ToString().ToUpperInvariant(), focus is null ? Slate : SourceColor.GetValueOrDefault(focus.Key, Bone), x, ref y);
        Big(b, "FORMS", forms.Length == 0 ? "—" : forms, Bone, x, ref y);
        Big(b, "VOWS", vows, vows == "NONE" ? Slate : Gold, x, ref y);

        // A real, derived synergy note (no invented copy).
        _ui.Fill(b, new Rectangle(CorePanel.X + 44, CorePanel.Bottom - 96, CorePanel.Width - 88, 2), Dim);
        var affinity = Mastery.MasteryForm() is { } mf ? Short(mf) : "no";
        var note = focus is null
            ? "Add skills to compose Source × Form × Vow."
            : $"{focus.Key.ToString().ToUpperInvariant()}-led auto-skills with {affinity} mastery and layered vow uptime.";
        _ui.TextBig(b, "SYNERGY", CorePanel.X + 44, CorePanel.Bottom - 76, Slate, UiTypography.Secondary);
        _ui.TextBig(b, note, CorePanel.X + 44, CorePanel.Bottom - 48, Bone, UiTypography.Body);
    }

    private void DrawAuraCards(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, AuraPanel);
        _ui.TextCenterBig(b, "AUTO-SKILL LOADOUT", AuraPanel.Center.X, AuraPanel.Y + 14, Gold, UiTypography.SectionTitle);
        var skills = Loadout.Skills;
        for (var i = 0; i < PlayerLoadout.MaxSkills; i++)
        {
            var card = AuraCard(i);
            if (i < skills.Count)
            {
                var s = skills[i];
                var sc = SourceColor.GetValueOrDefault(s.Source, Bone);
                _ui.Fill(b, card, Quiet);
                _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 4), sc);
                Outline(b, card, sc * 0.7f, 2);
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(card.Center.X - 38, card.Y + 24, 76, 76), Color.White);
                else _ui.Diamond(b, new Rectangle(card.Center.X - 32, card.Y + 28, 64, 64), sc);
                // Title = the real skill identity (Source + Form); state line = the Vow (§8 concise state line).
                _ui.TextCenterBig(b, $"{s.Source.ToString().ToUpperInvariant()} {Short(s.Form)}", card.Center.X, card.Y + 118, Bone, UiTypography.Body);
                var vow = Weaving.ById(s.VowId);
                _ui.TextCenterBig(b, vow is null ? "NO VOW" : $"VOW · {vow.Short.ToUpperInvariant()}", card.Center.X, card.Y + 152, vow is null ? Slate : Gold, UiTypography.Secondary);
            }
            else
            {
                _ui.Fill(b, card, Quiet * 0.6f);
                _ui.TextCenter(b, i == skills.Count ? "+ EDIT" : "—", card.Center.X, card.Center.Y - 8, Dim);
            }
        }
    }

    private void DrawPassives(SpriteBatch b, Point hit, MemoryDustTree tree)
    {
        _ui.Panel(b, PassivePanel);
        // +44 clears the panel art's centred top crest, which a centred title at +24 ran into.
        _ui.TextCenterBig(b, "PASSIVES & RESONANCE", PassivePanel.Center.X, PassivePanel.Y + 62, Gold, UiTypography.SectionTitle);

        // 52px, the width of the ornate border. At 32 the left column sat ON the frame and the
        // right-aligned resonance percentages were clipped by the opposite edge.
        var x = PassivePanel.X + 52;
        _ui.TextBig(b, $"MASTERY NODES   {Mastery.Spent} / {MasteryCatalog.Nodes.Count(n => n.Kind != MasteryKind.Start)}", x, PassivePanel.Y + 122, Bone, UiTypography.Body);
        Button(b, ViewTreeBtn, "VIEW TREE", hit, true);

        // PASSIVES — the real taken mastery nodes (notables + mastery). No invented "trait bonus %" table.
        _ui.TextBig(b, "PASSIVES", x, PassivePanel.Y + 190, Gold, UiTypography.Secondary);
        var taken = MasteryCatalog.Nodes.Where(n => n.Kind is MasteryKind.Notable or MasteryKind.Mastery && Mastery.IsTaken(n.Id)).ToList();
        var py = PassivePanel.Y + 224;
        if (taken.Count == 0) _ui.TextBig(b, "None yet — walk the tree.", x, py, Slate, UiTypography.Body);
        foreach (var n in taken.Take(6))
        {
            _ui.Diamond(b, new Rectangle(x, py + 2, 18, 18), n.Kind == MasteryKind.Mastery ? Gold : Purple);
            _ui.TextBig(b, n.Kind == MasteryKind.Mastery ? $"{Short(n.Arm)} MASTERY" : n.Label, x + 30, py, Bone, UiTypography.Body);
            py += 36;
        }

        // RESONANCE — the real source composition of the equipped skills (share per source).
        _ui.TextBig(b, "RESONANCE", x, PassivePanel.Y + 430, Gold, UiTypography.Secondary);
        var skills = Loadout.Skills;
        var ry = PassivePanel.Y + 464;
        var groups = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).ToList();
        if (groups.Count == 0) _ui.TextBig(b, "No skills equipped.", x, ry, Slate, UiTypography.Body);
        foreach (var g in groups)
        {
            var pct = skills.Count == 0 ? 0 : 100 * g.Count() / skills.Count;
            _ui.Diamond(b, new Rectangle(x, ry + 2, 18, 18), SourceColor.GetValueOrDefault(g.Key, Bone));
            _ui.TextBig(b, $"{g.Key.ToString().ToUpperInvariant()} RESONANCE", x + 30, ry, Bone, UiTypography.Body);
            _ui.TextRightBig(b, $"{pct}%", PassivePanel.Right - 52, ry, SourceColor.GetValueOrDefault(g.Key, Bone), UiTypography.Body);
            ry += 40;
        }

        // KEYSTONES — worn sockets (real).
        var worn = DustEffects.LearnedKeystones(tree).Where(k => Loadout.HasKeystone(k.Id)).ToList();
        // Above the panel art's bottom crest.
        _ui.TextBig(b, "KEYSTONES", x, PassivePanel.Y + 596, Gold, UiTypography.Secondary);
        if (worn.Count == 0) _ui.TextBig(b, "None socketed.", x, PassivePanel.Y + 630, Slate, UiTypography.Body);
        else _ui.TextBig(b, string.Join("  ·  ", worn.Select(k => k.Name.ToUpperInvariant())), x, PassivePanel.Y + 630, Bone, UiTypography.Body);
    }

    private void Big(SpriteBatch b, string label, string value, Color color, int x, ref int y)
    {
        _ui.TextBig(b, label, x, y, Slate, UiTypography.Secondary);
        _ui.TextBig(b, value, x + 200, y - 4, color, UiTypography.PanelTitle);
        y += 74;
    }

    private void Row(SpriteBatch b, Rectangle panel, string label, string value, ref int y)
    {
        _ui.Fill(b, new Rectangle(panel.X + 24, y, panel.Width - 48, 44), Quiet);
        _ui.TextBig(b, label, panel.X + 38, y + 12, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, value, panel.Right - 38, y + 10, Bone, UiTypography.Body);
        y += 52;
    }

    private void Button(SpriteBatch b, Rectangle r, string label, Point hit, bool enabled)
    {
        var hot = enabled && r.Contains(hit);
        var key = !enabled ? "ui_button_disabled" : hot ? "ui_button_primary" : "ui_button_secondary";
        if (_ui.Assets.Get(key) is { } t) b.Draw(t, r, Color.White);
        else _ui.Fill(b, r, !enabled ? Dim : hot ? Purple * 0.6f : Quiet);
        _ui.TextCenterBig(b, label, r.Center.X, r.Center.Y - 12, !enabled ? Slate : hot ? Gold : Bone, UiTypography.Body);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { SummaryPanel, CorePanel, AuraPanel, PassivePanel })
            Outline(b, r, Ember, 2);
        for (var i = 0; i < 4; i++) Outline(b, AuraCard(i), Verd, 2);
        _ui.TextBig(b, "nav BUILD  overview  4 aura cards", 60, 112, Gold, UiTypography.Secondary);
    }

    // ── Edit sub-view (the existing tree + skill sidebar). ──
    private void DrawEditor(SpriteBatch b, Point hit, MemoryDustTree tree)
    {
        _ui.Scrim(b, 0.6f);
        _ui.Title(b, "MASTERY TREE");
        _ui.Text(b, $"POINTS  {Mastery.Available}", 104, 60, Mastery.Available > 0 ? Gold : Slate);
        _ui.Fill(b, BackBtn, BackBtn.Contains(hit) ? Hi : PanelBg);
        _ui.TextCenter(b, "‹ BACK", BackBtn.Center.X, BackBtn.Y + 12, BackBtn.Contains(hit) ? Gold : Slate);
        var aff = Mastery.MasteryForm();

        foreach (var node in MasteryCatalog.Nodes)
            foreach (var pre in node.Prereqs)
                if (MasteryCatalog.ById(pre) is { } p)
                    Line(b, NodePos(p), NodePos(node), Mastery.IsTaken(node.Id) && Mastery.IsTaken(pre) ? Gold : Path);
        foreach (var node in MasteryCatalog.Nodes) DrawNode(b, node, hit, aff);
        DrawSidebar(b, hit, tree);

        var info = _hoverInfo.Length > 0 ? _hoverInfo : _msg.Length > 0 ? _msg : "WALK A PATH TO A CORNER TO MASTER THAT FORM.";
        var infoColor = _hoverInfo.Length > 0 ? Bone : _msg.Length > 0 ? Ember : Dim;
        _ui.Text(b, info, 48, 904, infoColor);
    }

    private void DrawSidebar(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        _ui.Fill(b, Sidebar, SbBg);
        _ui.Fill(b, new Rectangle(Sidebar.X, Sidebar.Y, 4, Sidebar.Height), Path);
        _ui.Text(b, "YOUR SKILLS", SbX + 24, 204, Slate);
        var skills = Loadout.Skills;
        for (var i = 0; i < PlayerLoadout.MaxSkills; i++)
        {
            var card = SkillCard(i);
            _ui.Fill(b, card, PanelBg);
            if (i < skills.Count)
            {
                var s = skills[i];
                _ui.Text(b, $"SLOT {i + 1}", card.X + 16, card.Y + 8, Slate);
                var rm = CRemove(i);
                _ui.TextCenter(b, "X", rm.Center.X, rm.Y + 8, rm.Contains(mouse) ? Ember : Slate);
                DrawCell(b, CSource(i), s.Source.ToString().ToUpperInvariant(), SourceColor.GetValueOrDefault(s.Source, Bone), mouse);
                DrawCell(b, CForm(i), Short(s.Form), Bone, mouse);
                var vow = Weaving.ById(s.VowId);
                DrawCell(b, CVow(i), vow is null ? "NO VOW" : vow.Short.ToUpperInvariant(), vow is null ? Dim : Gold, mouse);
            }
            else if (i == skills.Count) _ui.TextCenter(b, "+ ADD SKILL", card.Center.X, card.Y + 56, card.Contains(mouse) ? Gold : Slate);
            else _ui.TextCenter(b, "— LOCKED —", card.Center.X, card.Y + 56, Dim);
        }
        var learned = DustEffects.LearnedKeystones(tree);
        _ui.Text(b, learned.Count == 0 ? "KEYSTONES — LEARN IN DUST (P)" : $"KEYSTONES — WEAR {PlayerLoadout.MaxKeystones}", SbX + 24, 806, Slate);
        for (var i = 0; i < learned.Count && i < 3; i++)
        {
            var chip = KeystoneChip(i);
            var worn = Loadout.HasKeystone(learned[i].Id);
            var hover = chip.Contains(mouse);
            _ui.Fill(b, chip, worn ? Hi : PanelBg);
            if (worn) _ui.Fill(b, new Rectangle(chip.X, chip.Y, 8, chip.Height), Gold);
            _ui.Text(b, learned[i].Name, chip.X + 24, chip.Y + 16, worn ? Gold : hover ? Bone : Slate);
            if (hover) _hoverInfo = learned[i].Blurb.ToUpperInvariant();
        }
    }

    private void DrawNode(SpriteBatch b, MasteryNode node, Point mouse, Form? aff)
    {
        var p = NodePos(node);
        var rad = NodeRadius(node.Kind);
        var box = new Rectangle(p.X - rad, p.Y - rad, rad * 2, rad * 2);
        var hover = Math.Abs(mouse.X - p.X) <= rad + 8 && Math.Abs(mouse.Y - p.Y) <= rad + 8;
        var taken = Mastery.IsTaken(node.Id);
        var canTake = Mastery.CanTake(node.Id);
        var fill = taken ? Gold : canTake ? Hi : PanelBg;
        var edge = taken ? Gold : canTake ? Verd : Path;
        if (node.Kind == MasteryKind.Mastery)
        {
            var wide = new Rectangle(p.X - 120, p.Y - 44, 240, 88);
            _ui.Fill(b, wide, taken ? Hi : PanelBg);
            Outline(b, wide, node.Arm == aff ? Gold : hover ? Bone : edge, node.Arm == aff ? 8 : 4);
            _ui.TextCenter(b, Short(node.Arm), wide.Center.X, wide.Y + 28, node.Arm == aff ? Gold : taken ? Gold : Slate);
        }
        else if (node.Kind == MasteryKind.Start)
        {
            _ui.Fill(b, box, PanelBg); Outline(b, box, Slate, 4); _ui.TextCenter(b, "YOU", p.X, p.Y - 12, Bone);
        }
        else { _ui.Fill(b, box, fill); Outline(b, box, hover ? Bone : edge, 4); }
        if (hover && node.Kind != MasteryKind.Start)
            _hoverInfo = node.Kind == MasteryKind.Mastery ? $"{Short(node.Arm)} MASTERY — SPECIALISE IN {Short(node.Arm)}" : node.Label;
    }

    private void DrawCell(SpriteBatch b, Rectangle r, string text, Color color, Point mouse)
    {
        _ui.Fill(b, r, r.Contains(mouse) ? Hi : PanelBg);
        _ui.Text(b, "<", r.X + 4, r.Y + 12, Slate);
        _ui.Text(b, ">", r.Right - 28, r.Y + 12, Slate);
        _ui.TextCenter(b, text, r.Center.X, r.Y + 12, color);
    }

    private void Line(SpriteBatch b, Point a, Point c, Color col)
    {
        var dx = c.X - a.X; var dy = c.Y - a.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0) return;
        for (var i = 0; i <= steps; i++)
            _ui.Fill(b, new Rectangle(a.X + dx * i / steps - 4, a.Y + dy * i / steps - 4, 8, 8), col);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
