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
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
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
    private static string Short(Branch b) => b.ToString().ToUpperInvariant();

    private static string Short(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };

    private readonly UiKit _ui;
    private string _msg = "";
    private string _hoverInfo = "";
    private string? _hoverNodeId;          // the node under the pointer this frame
    private string? _pinnedNodeId;         // the last node clicked — what the detail panel shows when nothing is hovered
    private bool _showSkills;              // the tree page's right panel: node detail (false) or the skill editor (true)
    private bool _editMode;   // false = the overview; true = the mastery-tree + skill editor sub-view

    /// <summary>Open straight onto the tree — used by the headless capture so the shot shows the tree.</summary>
    /// <summary>DEV ONLY: open the tree sub-view, optionally framed on the centre at a given zoom.</summary>
    /// <remarks>
    /// The zoom argument exists because node ART cannot be verified from the default 0.30 overview —
    /// at that scale a Notable is thirty pixels across and a capture proves only that something was
    /// drawn there. The whole point of a camera is that the same layout has a near view.
    /// </remarks>
    public void DevOpenTree(float zoom = 0f)
    {
        _editMode = true;
        if (zoom > 0f) _zoom = zoom;
    }

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

    // ══ THE TREE'S OWN SPACE ═══════════════════════════════════════════════════════════════════
    //
    // Node positions are computed in WORLD units around (0,0) and a camera decides what is on screen.
    // The tree used to be laid out directly in pixels around (600,520) with a radius of 464 — which
    // meant the LAYOUT was the size of the window, so every node added made the tree tighter and there
    // was no way to add a second cross, a deeper ring, or a whole new branch without the thing becoming
    // unreadable. A world plus a camera has no such ceiling: growing the tree moves the far edge, not
    // the spacing.
    private const float WorldR = 1500f;

    private Vector2 _pan;                 // world point at the centre of the view
    // 0.30 frames the whole tree: WorldR is 1500 and the view is 900 tall, so anything above
    // 450/1500 pushes the north and south masteries off the top and bottom edges — which is what 0.42
    // did, hiding two of the four things the layout exists to show.
    private float _zoom = 0.30f;          // screen pixels per world unit
    private Point? _dragFrom;             // where a drag started, in screen space
    private bool _draggedThisPress;       // the drag moved far enough to swallow the click
    private Vector2 _dragPanFrom;

    private const float MinZoom = 0.16f, MaxZoom = 1.40f;

    /// <summary>The tree owns the whole canvas. It is the only thing on its page.</summary>
    private static readonly Rectangle TreeView = new(180, 96, 1740, 900);

    private Vector2 Screen(Vector2 world)
        => new(TreeView.Center.X + (world.X - _pan.X) * _zoom,
               TreeView.Center.Y + (world.Y - _pan.Y) * _zoom);

    private Vector2 World(Point screen)
        => new((screen.X - TreeView.Center.X) / _zoom + _pan.X,
               (screen.Y - TreeView.Center.Y) / _zoom + _pan.Y);

    /// <summary>
    /// Zoom about the POINTER, not about the centre.
    /// </summary>
    /// <remarks>
    /// Zooming about the centre makes the thing under the cursor slide away, so a player who scrolls to
    /// look closer at a node has to chase it. Anchoring on the pointer is what makes a map feel like a
    /// map rather than a slider.
    /// </remarks>
    private void ZoomAt(Point anchor, float factor)
    {
        var before = World(anchor);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        var after = World(anchor);
        _pan += before - after;
        ClampPan();
    }

    /// <summary>Keep the tree reachable: the centre may leave the view, but never by more than a screen.</summary>
    private void ClampPan()
    {
        var limit = WorldR * 1.25f;
        _pan = new Vector2(Math.Clamp(_pan.X, -limit, limit), Math.Clamp(_pan.Y, -limit, limit));
    }

    private static Point Centre => new(0, 0);
    /// <summary>
    /// The four branches are laid out as a CROSS, not a hexagon: Weight up, Spread down, Tempo right,
    /// Endure left.
    /// </summary>
    /// <remarks>
    /// The geometry states the design. Weight sits directly opposite Spread and Tempo directly opposite
    /// Endure because those pairs are opposed — a player looking at the screen should be able to see that
    /// walking north means walking away from south, before reading a single node. The old hexagon had six
    /// Form arms with no opposition in it at all, so its shape carried no information.
    /// </remarks>
    private static float AngleOf(Branch b) => b switch
    {
        Branch.Weight => -90f,
        Branch.Tempo => 0f,
        Branch.Spread => 90f,
        _ => 180f,
    } * MathF.PI / 180f;

    private static Vector2 Corner(Branch b)
    {
        var a = AngleOf(b);
        return new Vector2(WorldR * MathF.Cos(a), WorldR * MathF.Sin(a));
    }

    /// <summary>Which of its ring's siblings this is, and how many there are — drives the fan.</summary>
    private static (int Index, int Count) Sibling(MasteryNode n)
    {
        var peers = MasteryCatalog.Nodes
            .Where(x => x.Branch == n.Branch && x.Kind == n.Kind && x.Link is null)
            .ToList();
        return (Math.Max(0, peers.FindIndex(x => x.Id == n.Id)), Math.Max(1, peers.Count));
    }

    /// <summary>Where a node lives in WORLD units. Independent of zoom, pan, and the window.</summary>
    private static Vector2 NodePos(MasteryNode n)
    {
        if (n.Kind == MasteryKind.Start) return Vector2.Zero;

        // A BRIDGE sits between the two branches it spans, close in — it is a shortcut, and drawing it
        // out at the rim would suggest it is a destination.
        if (n.Link is { } link)
        {
            var mid = (AngleOf(n.Branch) + AngleOf(link)) / 2f;
            // Endure (180) and Weight (-90) average to 45, which points at Tempo. Rotate that one case.
            if (MathF.Abs(AngleOf(n.Branch) - AngleOf(link)) > MathF.PI) mid += MathF.PI;
            return new Vector2(WorldR * 0.46f * MathF.Cos(mid), WorldR * 0.46f * MathF.Sin(mid));
        }

        var baseAng = AngleOf(n.Branch);
        var (idx, count) = Sibling(n);

        // A SPECIALISATION hangs off the side of its branch rather than on the spine, so the spine still
        // reads as one walk outward and the Form nodes read as an aside.
        if (n.Kind == MasteryKind.Specialisation)
        {
            var side = idx == 0 ? -1f : 1f;
            var ang = baseAng + side * 26f * MathF.PI / 180f;
            return new Vector2(WorldR * 0.86f * MathF.Cos(ang), WorldR * 0.86f * MathF.Sin(ang));
        }

        // Rings fan their siblings across a spread that narrows as they go out, so a branch reads as a
        // funnel: four minors converging on three notables, on two greaters, on one mastery.
        var spreadDeg = n.Ring switch { 1 => 26f, 2 => 16f, 3 => 9f, _ => 0f };
        var offset = count <= 1 ? 0f : (idx - (count - 1) / 2f) * spreadDeg;
        var angle = baseAng + offset * MathF.PI / 180f;

        // NOT ring/4. Even spacing put ring 1 at a quarter of the radius, which is inside the START node's
        // own box — the four minors of the left and right branches sat on top of "YOU". The first ring has
        // to clear the centre, and after that the gaps can close up as the fan narrows.
        var rad = WorldR * n.Ring switch { 1 => 0.37f, 2 => 0.61f, 3 => 0.81f, _ => 1f };
        return new Vector2(rad * MathF.Cos(angle), rad * MathF.Sin(angle));
    }

    private static readonly Rectangle TreeResetBtn = new(48, 128, 216, 52);
    private static readonly Rectangle BackBtn = new(48, 128, 216, 52);
    /// <summary>Size states price: a node you can see is expensive before you read it.</summary>
    private static int NodeRadius(MasteryKind k) => k switch
    {
        MasteryKind.Start => 60,
        MasteryKind.Mastery => 64,
        MasteryKind.Greater => 46,
        MasteryKind.Bridge or MasteryKind.Specialisation => 42,
        MasteryKind.Notable => 38,
        _ => 26,
    };
    private const int SbX = 1200;
    private static readonly Rectangle SkillsToggle = new(1700, 100, 200, 40);
    // A DOCKED CARD, not a column. The tree is the page now; a 700px panel permanently taking a third
    // of the canvas is exactly the "sıkışmış" the layout was accused of.
    private static readonly Rectangle NodePanel = new(1408, 588, 496, 460);
    // Sized for FIVE skill cards, not four.
    //
    // The trait tree's spine sells a fifth weave, and the old geometry (four cards of 132 at a pitch of
    // 144, sidebar ending at 928) had no room for it — the fifth card would have run over the keystone
    // chips, and the keystone header already sat on top of slot four's Vow row in a capture. Everything
    // below is derived from fitting five cards plus a header plus three chips inside 1080.
    private static readonly Rectangle Sidebar = new(SbX, 200, 1920 - SbX - 16, 860);
    private static Rectangle SkillCard(int i) => new(SbX + 16, 252 + i * 128, 680, 120);
    private static Rectangle CSource(int i) { var c = SkillCard(i); return new(c.X, c.Y + 40, 328, 40); }
    private static Rectangle CForm(int i) { var c = SkillCard(i); return new(c.X + 344, c.Y + 40, 336, 40); }
    private static Rectangle CVow(int i) { var c = SkillCard(i); return new(c.X, c.Y + 80, 680, 40); }
    private static Rectangle CRemove(int i) { var c = SkillCard(i); return new(c.Right - 52, c.Y, 52, 40); }
    private static Rectangle KeystoneChip(int i) => new(SbX + 16, 920 + i * 44, 680, 40);

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, MemoryDustTree tree)
        => Update(keys, prev, mouse, clicked, false, 0, tree);

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked,
                       bool held, int wheel, MemoryDustTree tree)
    {
        Tree = tree;

        // ── THE CAMERA. Only while the tree page is open; the overview has nothing to pan. ────────
        if (_editMode)
        {
            var over = Game1.ToOverlay(mouse);

            if (wheel != 0 && TreeView.Contains(over))
                ZoomAt(over, wheel > 0 ? 1.16f : 1f / 1.16f);

            // DRAG TO PAN. Held, not clicked: a click is a node take, and a tree you can only move with
            // a scrollbar is a tree nobody moves.
            if (held && TreeView.Contains(over))
            {
                if (_dragFrom is null) { _dragFrom = over; _dragPanFrom = _pan; }
                else
                {
                    var d = _dragFrom.Value;
                    _pan = _dragPanFrom + new Vector2((d.X - over.X) / _zoom, (d.Y - over.Y) / _zoom);
                    ClampPan();
                    if (Math.Abs(d.X - over.X) + Math.Abs(d.Y - over.Y) > 6) _draggedThisPress = true;
                }
            }
            else _dragFrom = null;

            // Keyboard, for anyone who would rather not drag.
            var step = 260f / _zoom * 0.06f;
            if (keys.IsKeyDown(Keys.A)) { _pan.X -= step; ClampPan(); }
            if (keys.IsKeyDown(Keys.D)) { _pan.X += step; ClampPan(); }
            bool Tapped(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
            if (Tapped(Keys.OemPlus) || Tapped(Keys.Add)) ZoomAt(TreeView.Center, 1.25f);
            if (Tapped(Keys.OemMinus) || Tapped(Keys.Subtract)) ZoomAt(TreeView.Center, 1f / 1.25f);
            if (Tapped(Keys.Home)) { _pan = Vector2.Zero; _zoom = 0.30f; }
        }

        if (!clicked) return;

        // A click that ENDED a drag is a pan, not a take. Without this every attempt to move the tree
        // also spent a point on whatever node the pointer happened to stop over.
        if (_draggedThisPress) { _draggedThisPress = false; return; }
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        if (!_editMode)
        {
            if (EditBtn.Contains(hit) || ViewTreeBtn.Contains(hit)) { _editMode = true; return; }
            if (ResetBtn.Contains(hit) && Mastery.Spent > 0) { Mastery.Respec(); _msg = "MASTERY RESET."; Dirty = true; }
            return;
        }

        // ── Edit sub-view: tree + right column. ──
        if (BackBtn.Contains(hit)) { _editMode = false; return; }
        if (SkillsToggle.Contains(hit)) { _showSkills = !_showSkills; return; }
        if (Mastery.Spent > 0 && TreeResetBtn.Contains(hit)) { /* Back occupies the same corner; handled above */ }

        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Kind == MasteryKind.Start) continue;
            // The SAME transform the drawing uses, or the click lands where the node used to be.
            var sp = Screen(NodePos(node));
            var rad = Math.Max(6, (int)(NodeRadius(node.Kind) * _zoom * 1.9f)) + 6;
            var halfW = node.Kind == MasteryKind.Mastery ? (int)(120 * _zoom * 1.9f) + 6 : rad;
            if (Math.Abs(hit.X - sp.X) > halfW || Math.Abs(hit.Y - sp.Y) > rad) continue;
            // Clicking a node PINS it in the detail panel whether or not it could be taken — a node you
            // cannot afford is exactly the one you most want to read.
            _pinnedNodeId = node.Id;
            _showSkills = false;
            if (Mastery.Take(node.Id)) { _msg = ""; Dirty = true; }
            else _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
                Mastery.Available <= 0 ? "NO MASTERY POINTS — GO DEEPER." :
                node.Kind == MasteryKind.Mastery && Mastery.Affinity() is not null ? "YOU'VE ALREADY MASTERED A FORM." :
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
        _hoverNodeId = null;
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

        var adept = Mastery.Affinity() is { } mf ? $"{Short(mf)} ADEPT" : "SEEKER";
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
        var affinity = Mastery.Affinity() is { } mf ? Short(mf) : "no";
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
        _ui.TextBig(b, $"SKILL POINTS   {Mastery.Spent} SPENT  ·  {Mastery.Available} FREE", x, PassivePanel.Y + 122, Bone, UiTypography.Body);
        Button(b, ViewTreeBtn, "VIEW TREE", hit, true);

        // PASSIVES — the real taken mastery nodes (notables + mastery). No invented "trait bonus %" table.
        _ui.TextBig(b, "PASSIVES", x, PassivePanel.Y + 190, Gold, UiTypography.Secondary);
        var taken = MasteryCatalog.Nodes.Where(n => n.Kind is MasteryKind.Notable or MasteryKind.Mastery && Mastery.IsTaken(n.Id)).ToList();
        var py = PassivePanel.Y + 224;
        if (taken.Count == 0) _ui.TextBig(b, "None yet — walk the tree.", x, py, Slate, UiTypography.Body);
        foreach (var n in taken.Take(6))
        {
            _ui.Diamond(b, new Rectangle(x, py + 2, 18, 18), n.Kind == MasteryKind.Mastery ? Gold : Purple);
            _ui.TextBig(b, n.Label, x + 30, py, Bone, UiTypography.Body);
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
        _ui.Text(b, $"POINTS  {Mastery.Available}  ·  {Mastery.Spent} SPENT", 104, 60, Mastery.Available > 0 ? Gold : Slate);
        _ui.Fill(b, BackBtn, BackBtn.Contains(hit) ? Hi : PanelBg);
        _ui.TextCenter(b, "‹ BACK", BackBtn.Center.X, BackBtn.Y + 12, BackBtn.Contains(hit) ? Gold : Slate);
        var aff = Mastery.Affinity();

        // ONE EDGE PER NODE, to its NEAREST prerequisite.
        //
        // Drawing every prerequisite drew a spider's web: each notable lists all four of its branch's
        // minors and each minor lists START, so a single branch produced sixteen crossing lines and the
        // whole screen read as scribble. A prerequisite list is an "any of these" rule, not a diagram —
        // so the drawing shows the SPINE (what the funnel looks like) and the tooltip carries the rule.
        // Gold only when both ends are taken, so a walked branch reads as one continuous path.
        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Prereqs.Count == 0) continue;
            var to = NodePos(node);

            MasteryNode? nearest = null;
            var best = float.MaxValue;
            foreach (var pre in node.Prereqs.Concat(node.SecondPrereqs))
            {
                if (MasteryCatalog.ById(pre) is not { } p) continue;
                var from = NodePos(p);
                // Prefer a TAKEN prerequisite when there is one, so the gold path follows the route the
                // player actually walked rather than whichever node happens to sit closest.
                var d = (from.X - to.X) * (from.X - to.X) + (from.Y - to.Y) * (from.Y - to.Y)
                        - (Mastery.IsTaken(pre) ? 1_000_000 : 0);
                if (d >= best) continue;
                best = d;
                nearest = p;
            }

            if (nearest is null) continue;
            var a = Screen(NodePos(nearest));
            var c = Screen(to);
            Line(b, new Point((int)a.X, (int)a.Y), new Point((int)c.X, (int)c.Y),
                 Mastery.IsTaken(node.Id) && Mastery.IsTaken(nearest.Id) ? Gold : Path);
        }
        foreach (var node in MasteryCatalog.Nodes) DrawNode(b, node, hit, aff);

        // The right column answers "what am I looking at", not "what are my skill slots".
        //
        // It used to be the SKILL EDITOR — four slots and a keystone strip — sitting beside a tree it has
        // nothing to do with, so the screen asked the player to hold two unrelated jobs at once and
        // answered neither. The tree's own question ("what does this node do, can I afford it, what does
        // it cost me") had no home at all except a one-line strip at the very bottom of the screen.
        _ui.Fill(b, SkillsToggle, SkillsToggle.Contains(hit) ? Hi : PanelBg);
        _ui.TextCenter(b, _showSkills ? "\u2039 NODE" : "SKILLS \u203a",
                       SkillsToggle.Center.X, SkillsToggle.Y + 12, SkillsToggle.Contains(hit) ? Gold : Slate);

        if (_showSkills) DrawSidebar(b, hit, tree);
        else DrawNodeDetail(b, hit);

        var info = _hoverInfo.Length > 0 ? _hoverInfo : _msg.Length > 0 ? _msg : "WEIGHT ↔ SPREAD AND TEMPO ↔ ENDURE ARE OPPOSED.  ONE BRANCH IS AFFORDABLE; TWO ARE NOT.";
        var infoColor = _hoverInfo.Length > 0 ? Bone : _msg.Length > 0 ? Ember : Dim;
        _ui.Text(b, info, 200, 1024, infoColor);
    }

    /// <summary>
    /// What the node under the pointer — or the last one clicked — actually does.
    /// </summary>
    /// <remarks>
    /// The one thing the tree page never had. A node was a coloured rectangle whose entire description
    /// was a single line of text at the bottom of the screen, printed only while the pointer was exactly
    /// on it; a player could not read a node and then look at the tree, which is what reading a tree IS.
    /// Hover shows, click PINS — including a node you cannot afford, which is the one you most want to
    /// read before deciding what to walk toward.
    /// </remarks>
    private void DrawNodeDetail(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, NodePanel);

        var id = _hoverNodeId ?? _pinnedNodeId;
        if (id is null || MasteryCatalog.ById(id) is not { } n)
        {
            // No panel title: the frame's centred top medallion sits exactly where one would go, and the
            // two lines below already say what this panel is.
            _ui.TextCenter(b, "HOVER A NODE TO READ IT.", NodePanel.Center.X, NodePanel.Y + 96, Slate);
            // Three words, not a sentence: the card is 496 wide and the long form ran out of both sides
            // of its own frame.
            _ui.TextCenter(b, "DRAG  \u00b7  WHEEL  \u00b7  HOME", NodePanel.Center.X, NodePanel.Y + 132, Dim);

            var y0 = NodePanel.Y + 190;
            foreach (var br in new[] { Branch.Weight, Branch.Spread, Branch.Tempo, Branch.Endure })
            {
                var taken = MasteryCatalog.Nodes.Count(x => x.Branch == br && Mastery.IsTaken(x.Id));
                var spent = MasteryCatalog.Nodes.Where(x => x.Branch == br && Mastery.IsTaken(x.Id)).Sum(x => x.Cost);
                _ui.Fill(b, new Rectangle(NodePanel.X + 74, y0 - 4, 6, 34), BranchColor(br));
                _ui.TextBig(b, Short(br), NodePanel.X + 94, y0, Bone, UiTypography.Body);
                _ui.TextRightBig(b, $"{taken} \u00b7 {spent} PTS", NodePanel.Right - 74, y0,
                                 spent > 0 ? Gold : Dim, UiTypography.Body);
                y0 += 46;
            }
            return;
        }

        var col = BranchColor(n.Branch);
        var taken2 = Mastery.IsTaken(n.Id);
        var can = Mastery.CanTake(n.Id);

        _ui.Fill(b, new Rectangle(NodePanel.X + 64, NodePanel.Y + 40, NodePanel.Width - 128, 4), col);
        _ui.TextBig(b, KindWord(n.Kind), NodePanel.X + 68, NodePanel.Y + 56, col, UiTypography.Secondary);
        _ui.TextRightBig(b, Short(n.Branch), NodePanel.Right - 68, NodePanel.Y + 56, Slate, UiTypography.Secondary);

        DrawWrapped(b, n.Label, NodePanel.X + 68, NodePanel.Y + 100, NodePanel.Width - 136, Bone);

        var y = NodePanel.Y + 240;
        _ui.Fill(b, new Rectangle(NodePanel.X + 64, y - 16, NodePanel.Width - 128, 2), Dim);
        _ui.TextBig(b, "COST", NodePanel.X + 68, y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, $"{n.Cost} PT{(n.Cost == 1 ? "" : "S")}", NodePanel.Right - 68, y,
                         taken2 ? Gold : can ? Bone : Ember, UiTypography.PanelTitle);

        y += 56;
        _ui.TextBig(b, "YOU HAVE", NodePanel.X + 68, y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, $"{Mastery.Available}", NodePanel.Right - 68, y,
                         Mastery.Available >= n.Cost ? Bone : Ember, UiTypography.PanelTitle);

        y += 72;
        var state = taken2 ? "TAKEN" : can ? "AVAILABLE — CLICK THE NODE"
                    : Mastery.Available < n.Cost ? "NOT ENOUGH POINTS"
                    : "LOCKED — WALK TO IT FIRST";
        _ui.TextCenter(b, state, NodePanel.Center.X, y, taken2 ? Gold : can ? Verd : Ember);
    }

    /// <summary>Word-wrap a node's label into the panel. Node labels are sentences, not headings.</summary>
    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 30; line = w; }
            else line = probe;
        }
        if (line.Length > 0) _ui.Text(b, line, x, y, c);
    }

    private static string KindWord(MasteryKind k) => k switch
    {
        MasteryKind.Minor => "MINOR",
        MasteryKind.Notable => "NOTABLE",
        MasteryKind.Greater => "GREATER",
        MasteryKind.Mastery => "MASTERY",
        MasteryKind.Bridge => "BRIDGE",
        MasteryKind.Specialisation => "FORM SPECIALIST",
        _ => "START",
    };

    private static Color BranchColor(Branch b) => b switch
    {
        Branch.Weight => new Color(0xD6, 0x48, 0x5C),
        Branch.Spread => new Color(0x48, 0xB8, 0x88),
        Branch.Tempo => new Color(0x74, 0xC6, 0xE8),
        _ => new Color(0xC0, 0x6E, 0xE0),
    };

    /// <summary>The socket a node is set in. Shape says KIND — how big a commitment this is.</summary>
    private static string KindFrame(MasteryKind k) => k switch
    {
        MasteryKind.Start => "ui_node_start",
        MasteryKind.Minor => "ui_node_minor",
        MasteryKind.Notable => "ui_node_notable",
        MasteryKind.Greater => "ui_node_greater",
        MasteryKind.Mastery => "ui_node_mastery",
        MasteryKind.Bridge => "ui_node_bridge",
        _ => "ui_node_spec",
    };

    /// <summary>What is set in the socket. The glyph says BRANCH — which of the four roads this is.</summary>
    private static string BranchGlyph(Branch b) => b switch
    {
        Branch.Weight => "icon_branch_weight",
        Branch.Spread => "icon_branch_spread",
        Branch.Tempo => "icon_branch_tempo",
        _ => "icon_branch_endure",
    };

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
                // SOURCE and FORM carry no inline label: their cells are half-width and a label plus a
                // centred value overprint each other ("SOURCEBODY"). The VOW row is full width and is the
                // one that needed naming — it is the only row whose value can be NONE, so without a label
                // it read as a status message rather than a control.
                DrawCell(b, CSource(i), s.Source.ToString().ToUpperInvariant(),
                         SourceColor.GetValueOrDefault(s.Source, Bone), mouse);
                DrawCell(b, CForm(i), Short(s.Form), Bone, mouse);

                var vow = Weaving.ById(s.VowId);
                DrawCell(b, CVow(i),
                         vow is null ? "NONE — SWEAR ONE" : vow.Short.ToUpperInvariant(),
                         vow is null ? Slate : Gold, mouse, "VOW");

                // The Vow's full demand, on hover. A Vow is a restriction the build must MEET, and the
                // four-word Short cannot carry that — "ONE FORM ONLY" does not say it pays nothing if
                // you break it, which is the entire bargain.
                if (CVow(i).Contains(mouse))
                    _hoverInfo = vow is null
                        ? "A VOW PAYS BIG, BUT ONLY IF YOUR BUILD MEETS ITS DEMAND. LEARN THEM IN TRAITS (P)."
                        : vow.Description;
            }
            else if (i == skills.Count) _ui.TextCenter(b, "+ ADD SKILL", card.Center.X, card.Y + 56, card.Contains(mouse) ? Gold : Slate);
            else _ui.TextCenter(b, "— LOCKED —", card.Center.X, card.Y + 56, Dim);
        }
        var learned = DustEffects.LearnedKeystones(tree);
        _ui.Text(b, learned.Count == 0 ? "KEYSTONES — LEARN IN TRAITS (P)" : $"KEYSTONES — {Loadout.KeystoneCapacity} SOCKET(S)", SbX + 24, 894, Slate);
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

    /// <summary>
    /// One node, drawn through the camera.
    /// </summary>
    /// <remarks>
    /// Radius, outline and label all scale with zoom, so zooming out is a real overview rather than the
    /// same glyphs at the same size on a shrinking layout — which is what makes a big tree navigable at
    /// all. Labels vanish below a threshold: forty overlapping words is less readable than none, and the
    /// detail panel is where reading happens anyway.
    /// </remarks>
    private void DrawNode(SpriteBatch b, MasteryNode node, Point mouse, Form? aff)
    {
        var w = NodePos(node);
        var sp = Screen(w);
        var rad = Math.Max(4, (int)(NodeRadius(node.Kind) * _zoom * 1.9f));

        // Cull. A tree meant to grow will one day have far more nodes off screen than on it.
        if (sp.X < TreeView.X - 200 || sp.X > TreeView.Right + 200
            || sp.Y < TreeView.Y - 200 || sp.Y > TreeView.Bottom + 200) return;

        var cx = (int)sp.X;
        var cy = (int)sp.Y;
        var box = new Rectangle(cx - rad, cy - rad, rad * 2, rad * 2);
        var hover = Math.Abs(mouse.X - cx) <= rad + 6 && Math.Abs(mouse.Y - cy) <= rad + 6;

        var taken = Mastery.IsTaken(node.Id);
        var canTake = Mastery.CanTake(node.Id);
        var branchCol = BranchColor(node.Branch);

        // Colour says BRANCH; brightness says state. A field of identical grey boxes told the player
        // neither, and the tree's whole shape — four opposed roads — was invisible until they read
        // labels one at a time.
        var fill = taken ? branchCol : canTake ? branchCol * 0.34f : new Color(0x14, 0x11, 0x1E, 0xE0);
        var edge = taken ? Gold : canTake ? branchCol : Path;
        var thick = Math.Max(2, (int)(4 * _zoom * 1.6f));

        // A Mastery is drawn as a wide plaque, not a stud: it is the branch's whole identity and its
        // name is printed inside it. Everything below shares one path, so the box is decided first.
        if (node.Kind == MasteryKind.Mastery)
        {
            var half = (int)(120 * _zoom * 1.9f);
            box = new Rectangle(cx - half, cy - rad, half * 2, rad * 2);
            hover = box.Contains(mouse);
        }

        // THE ART. Three channels, one fact each: the FRAME's shape is the kind, the GLYPH inside it is
        // the branch, and the tint on both is the state. Before this the three facts shared one channel
        // — a coloured square, sized by kind, with a smaller square in the middle of the expensive ones
        // — so "which road is this" and "how much does it cost me" were both answered in area and hue,
        // the two things a player reads last.
        //
        // Stretched, not fitted: these are frames, and a frame that letterboxes stops framing what is
        // inside it. The square art goes into a square box, so only the Mastery plaque scales unevenly,
        // and it is authored at almost exactly the plaque's aspect.
        var frame = _ui.Assets.Get(KindFrame(node.Kind));
        var owned = node.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() == node.Branch;

        if (frame is not null)
        {
            // The socket's field, inset well inside the rim so it fits the DIAMOND and the octagon too,
            // not just the round frames. This is the only surface that goes bright when a node is taken.
            //
            // START is exempt. It is permanently allocated, so it takes the "taken" branch of `fill`
            // and came out as a bright WEIGHT-red square at the dead centre of the tree — reading as
            // the most emphatic Weight node on the page when it belongs to no branch at all.
            var pad = (int)(box.Width * 0.30f);
            if (node.Kind != MasteryKind.Mastery)   // the plaque brings its own dark centre
                _ui.Fill(b, new Rectangle(box.X + pad, box.Y + pad, box.Width - pad * 2, box.Height - pad * 2),
                         node.Kind == MasteryKind.Start ? PanelBg : fill);

            b.Draw(frame, box, hover ? Bone : owned ? Gold : taken ? Gold : canTake ? branchCol : Path);
        }
        else
        {
            // No art on disk: the flat shapes this screen shipped with. The game must run with an empty
            // assets/art, so every draw site keeps its greybox rather than borrowing someone else's art.
            _ui.Fill(b, box, node.Kind == MasteryKind.Start ? PanelBg : fill);
            Outline(b, box, node.Kind == MasteryKind.Start ? Slate : hover ? Bone : edge,
                    owned ? thick * 2 : thick);
        }

        if (node.Kind == MasteryKind.Start)
        {
            // >= the default zoom, not >. The threshold was 0.3 and HOME resets to exactly 0.30, so the
            // one label naming the centre of the tree was absent from the default view of it.
            if (_zoom >= 0.3f) _ui.TextCenter(b, "YOU", cx, cy - 12, Bone);
        }
        else if (node.Kind == MasteryKind.Mastery)
        {
            if (_zoom > 0.28f)
                _ui.TextCenter(b, Short(node.Branch), box.Center.X, box.Center.Y - 11,
                               owned || taken ? Bone : branchCol);
        }
        // The branch glyph, over the frame's hollow centre. Below about twenty pixels it is a smudge
        // that only muddies the socket, and the frame alone still carries the kind — so it drops out
        // rather than degrading, the same way the labels do.
        else if (box.Width >= 20
                 && _ui.Assets.Get(BranchGlyph(node.Branch)) is { } glyph)
        {
            var g = (int)(box.Width * 0.46f);
            // Dark on a lit field once taken, lit on a dark field before: whichever way round, the
            // glyph is the thing with contrast against what is behind it.
            var tint = taken ? new Color(0x14, 0x11, 0x1E) : canTake ? branchCol : new Color(0x4A, 0x46, 0x58);
            _ui.SpriteFit(b, glyph, new Rectangle(cx - g / 2, cy - g / 2, g, g), tint);
        }
        else if (frame is null && _zoom > 0.34f
                 && node.Kind is MasteryKind.Notable or MasteryKind.Greater
                                 or MasteryKind.Bridge or MasteryKind.Specialisation)
        {
            // The greybox kind mark, kept for the no-art path only.
            var pip = Math.Max(3, rad / 4);
            _ui.Fill(b, new Rectangle(cx - pip, cy - pip, pip * 2, pip * 2),
                     taken ? new Color(0x14, 0x11, 0x1E) : branchCol);
        }

        if (hover && node.Kind != MasteryKind.Start)
        {
            _hoverInfo = $"{node.Label}   ({node.Cost} PT{(node.Cost == 1 ? "" : "S")})";
            _hoverNodeId = node.Id;
        }
    }

    /// <summary>
    /// One cycling row of a skill card. <paramref name="label"/> names what is being cycled.
    /// </summary>
    /// <remarks>
    /// The label is not decoration. All three rows of a card were arrow-flanked values with nothing
    /// saying which was which, so the Vow row — the only one whose value can be NONE — read as a status
    /// message rather than a control, and a player looking for "where do I set a Vow" had no reason to
    /// think the row that said NO VOW was the answer.
    /// </remarks>
    private void DrawCell(SpriteBatch b, Rectangle r, string text, Color color, Point mouse, string label = "")
    {
        _ui.Fill(b, r, r.Contains(mouse) ? Hi : PanelBg);
        _ui.Text(b, "<", r.X + 4, r.Y + 12, Slate);
        _ui.Text(b, ">", r.Right - 28, r.Y + 12, Slate);
        if (label.Length > 0) _ui.Text(b, label, r.X + 30, r.Y + 12, Dim);
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
