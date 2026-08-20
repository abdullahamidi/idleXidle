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
/// The BUILD screen: a four-panel overview of the woven loadout — an identity card, the Source/Form/Vow
/// composition, the skill cards with their two actions docked beneath them, and the passives / resonance
/// column. The mastery tree is the SAME class in its other view (see ShowTree) and has its own rail tile.
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
    private string? _attuneNodeId;         // non-null: THE ATTUNEMENT ceremony is open for this just-taken node
    private Form _attuneForm;
    /// <summary>Set when the player asked for the weave editor. The host opens it and clears this.</summary>
    public bool WantsWeave { get; set; }
    private bool _resetArmed;   // the reset button has been pressed once and is waiting for the second
    private bool _editMode;   // false = the overview; true = the mastery-tree + skill editor sub-view

    /// <summary>Which of this screen's two views is open. The rail's BUILD and MASTERY tiles both set it.</summary>
    /// <remarks>
    /// The tree used to be reachable only from inside this screen, so the flag was private. It has its
    /// own rail tile now, and a tile that opened "whichever view you happened to leave open" would read
    /// as a broken button rather than as stale state.
    /// </remarks>
    public bool ShowTree
    {
        get => _editMode;
        // Arming survives no navigation. The two-click reset used to stay armed when the player left
        // BUILD and came back, so a single click on a screen they had just opened wiped the whole tree.
        set { _editMode = value; _resetArmed = false; }
    }

    /// <summary>
    /// Whether <see cref="Activity.Mastery"/> is open yet. Host-set, like every other fact here.
    /// </summary>
    /// <remarks>
    /// The rail refuses the MASTERY tile until wave 8, and OPEN THE MASTERY TREE on this screen walked
    /// straight past that — so the tree was reachable at wave 5 from a button drawn beside a tile that
    /// was dimmed and saying "REACH WAVE 8". Two doors into one room have to agree about the lock.
    /// </remarks>
    public bool TreeUnlocked { get; set; } = true;

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

    /// <summary>DEV: pose THE ATTUNEMENT ceremony for a capture, without touching the tree's state.</summary>
    public void DevAttune(Form form)
    {
        _attuneNodeId = MasteryCatalog.Nodes
            .First(n => n.Kind == MasteryKind.Specialisation && n.Form == form).Id;
        _attuneForm = form;
    }

    public PlayerLoadout Loadout { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();
    public MemoryDustTree Tree { get; set; } = new();
    public int Power { get; set; }   // host-set: PowerRating (the Build screen has no Hunter reference)
    public int Level { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevBuildDebug { get; set; }

    // ── Spec §4 overview layout. ──
    private static readonly Rectangle SummaryPanel = new(40, 138, 360, 460);
    private static readonly Rectangle CorePanel = new(426, 138, 774, 450);
    // 330, NOT 278 — the buttons live inside it now. They sat at y=902 while this panel ended at 884,
    // so the two controls that act on the build floated on the dungeon wall below the frame holding it,
    // reading as chrome that belonged to no panel at all.
    private static readonly Rectangle AuraPanel = new(426, 606, 774, 330);
    // 800, not 746. This column is the only one on the screen whose height is DATA — six passives, up
    // to five resonance rows, then the keystone block — and at 746 the worst case ran off the bottom.
    // The extra 54 puts its foot level with the middle column's button row rather than short of it.
    private static readonly Rectangle PassivePanel = new(1228, 138, 652, 800);
    // Docked in AuraPanel's foot, sharing its interior width. EditBtn is gone: it ran the same line as
    // VIEW TREE, and the tree has its own rail tile now.
    private static readonly Rectangle WeaveBtn = new(466, 842, 337, 44);
    // ON THE TREE PAGE, NOT THE OVERVIEW. Playtest: "'Take all mastery points back' buranın butonu
    // değil, mastery tree'nin butonu." Right — it un-spends every point in a tree the overview does not
    // even draw, so pressing it there meant watching nothing happen to anything on screen. It sits
    // where BACK used to: MASTERY is a rail destination now, and a BACK button on a page with its own
    // door reads as "you are somewhere nested" when you are not (playtest asked what it even meant).
    private static readonly Rectangle ResetBtn = new(48, 128, 320, 52);
    /// <summary>The i-th of <paramref name="n"/> auto-skill cards, sharing the panel's width between them.</summary>
    /// <remarks>
    /// Divided rather than fixed at a pitch of 182, which fitted exactly four and put a fifth at
    /// x=1180..1348 — off the end of a panel that stops at 1200 and across the passives column beside
    /// it. FIFTH WEAVE is a node the player can buy, so "four" was never a safe constant to lay out
    /// against; the cards now narrow to make room instead of walking off the edge.
    /// </remarks>
    private static Rectangle AuraCard(int i, int n)
    {
        const int gap = 14;
        var left = AuraPanel.X + UiKit.PanelCorner;
        var usable = AuraPanel.Width - UiKit.PanelCorner * 2;
        var w = (usable - (Math.Max(1, n) - 1) * gap) / Math.Max(1, n);
        return new Rectangle(left + i * (w + gap), 648, w, 178);
    }
    // Under the panel's top crest, which the title now clears too — at y=196 the button was drawn
    // straight through it.
    // Left-aligned to the panel's text column (PassivePanel.X + 52) and on its OWN row, below the
    // points line rather than beside it. See DrawPassives — sharing that row clipped the line.

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

    private static Vector2 Corner(Branch b)
    {
        var a = MasteryLayout.AngleOf(b);
        return new Vector2(WorldR * MathF.Cos(a), WorldR * MathF.Sin(a));
    }

    /// <summary>Where a node lives in WORLD units. Independent of zoom, pan, and the window.</summary>
    /// <remarks>
    /// The geometry itself is <see cref="MasteryLayout"/>, in Core. It was here, which meant the one
    /// claim worth making about a tree layout — that no two nodes land on top of each other — could not
    /// be tested at all. This is now the conversion to the drawing library's vector type and nothing
    /// else.
    /// </remarks>
    private static Vector2 NodePos(MasteryNode n)
    {
        var p = MasteryLayout.PositionOf(n);
        return new Vector2(p.X, p.Y);
    }


    /// <summary>Size states price: a node you can see is expensive before you read it.</summary>
    /// <remarks>
    /// The sizes live in <see cref="MasteryLayout.NodeWorldRadius"/> with the 1.9 already folded in,
    /// because the overlap test has to reason about how big a node actually is. Divided back out here
    /// so the two call sites below keep the multiply they have always had.
    /// </remarks>
    private static int NodeRadius(MasteryKind k) => (int)(MasteryLayout.NodeWorldRadius(k) / 1.9f);
    private const int SbX = 1200;

    // A DOCKED CARD, not a column. The tree is the page now; a 700px panel permanently taking a third
    // of the canvas is exactly the "sıkışmış" the layout was accused of.
    private static readonly Rectangle NodePanel = new(1408, 588, 496, 460);
    private static readonly Rectangle HexPanel = new(1408, 96, 496, 476);
    private static readonly Rectangle AttunePanel = new(480, 180, 960, 720);
    private static readonly Rectangle AttuneSealBtn = new(560, 816, 360, 52);
    private static readonly Rectangle AttuneUndoBtn = new(1000, 816, 360, 52);
    // Sized for FIVE skill cards, not four.
    //
    // The trait tree's spine sells a fifth weave, and the old geometry (four cards of 132 at a pitch of
    // 144, sidebar ending at 928) had no room for it — the fifth card would have run over the keystone
    // chips, and the keystone header already sat on top of slot four's Vow row in a capture. Everything
    // below is derived from fitting five cards plus a header plus three chips inside 1080.
    private static readonly Rectangle Sidebar = new(SbX, 200, 1920 - SbX - 16, 860);

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, MemoryDustTree tree)
        => Update(keys, prev, mouse, clicked, false, 0, tree);

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked,
                       bool held, int wheel, MemoryDustTree tree, bool rightClicked = false)
    {
        Tree = tree;

        // ── THE CAMERA. Only while the tree page is open; the overview has nothing to pan. ────────
        if (_editMode && _attuneNodeId is null)
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
            //
            // LEFT/RIGHT, NOT A/D. A and D are nav-rail hotkeys — A opens the WARREN, D is free but sits
            // beside it — and the rail's hotkey loop runs on every frame the tree is open, so holding A
            // to pan west left the tree and opened another screen. The arrows are already the pan keys
            // everywhere else in this game and collide with nothing here.
            var step = 260f / _zoom * 0.06f;
            if (keys.IsKeyDown(Keys.Left)) { _pan.X -= step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Right)) { _pan.X += step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Up)) { _pan.Y -= step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Down)) { _pan.Y += step; ClampPan(); }
            bool Tapped(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
            if (Tapped(Keys.OemPlus) || Tapped(Keys.Add)) ZoomAt(TreeView.Center, 1.25f);
            if (Tapped(Keys.OemMinus) || Tapped(Keys.Subtract)) ZoomAt(TreeView.Center, 1f / 1.25f);
            if (Tapped(Keys.Home)) { _pan = Vector2.Zero; _zoom = 0.30f; }
        }

        // THE RIGHT-CLICK REFUND, ABOVE THE LEFT-CLICK GATE. It was written below `if (!clicked)
        // return;` — and `clicked` is the LEFT button's edge while `rightClicked` is the right's, two
        // independent latches — so the refund could only fire on a frame where both buttons were pressed
        // at once. Shipped dead, in the same commit whose comment celebrated wiring up a dead method.
        // Everything under the gate assumes a left click, so this runs first rather than widening it.
        if (rightClicked && _editMode && !_draggedThisPress && _attuneNodeId is null)
        {
            var rhit = Game1.ToOverlay(mouse);
            foreach (var node in MasteryCatalog.Nodes)
            {
                if (node.Kind == MasteryKind.Start) continue;
                var rp = Screen(NodePos(node));
                var rr = Math.Max(6, (int)(NodeRadius(node.Kind) * _zoom * 1.9f)) + 6;
                var rw = node.Kind == MasteryKind.Mastery ? (int)(120 * _zoom * 1.9f) + 6 : rr;
                if (Math.Abs(rhit.X - rp.X) > rw || Math.Abs(rhit.Y - rp.Y) > rr) continue;
                _pinnedNodeId = node.Id;
                if (Mastery.Refund(node.Id)) { _msg = "ONE POINT RETURNED."; Dirty = true; }
                else _msg = Mastery.IsTaken(node.Id)
                    ? "ANOTHER NODE DEPENDS ON THIS ONE."
                    : "NOTHING SPENT HERE.";
                return;
            }
        }

        if (!clicked) return;

        // A click that ENDED a drag is a pan, not a take. Without this every attempt to move the tree
        // also spent a point on whatever node the pointer happened to stop over.
        if (_draggedThisPress) { _draggedThisPress = false; return; }
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        // ── THE ATTUNEMENT swallows the click while it is open. Seal keeps the node; undo is a
        //    real Refund, so backing out costs nothing — deliberation, not punishment. ──────────
        if (_attuneNodeId is { } attId)
        {
            if (AttuneSealBtn.Contains(hit))
            {
                _attuneNodeId = null;
                _msg = $"ATTUNED. {_attuneForm.ToString().ToUpperInvariant()} IS YOURS — ITS SKILLS STRIKE AT DOUBLE WORTH.";
            }
            else if (AttuneUndoBtn.Contains(hit))
            {
                Mastery.Refund(attId);
                _attuneNodeId = null;
                Dirty = true;
                _msg = "THE POINTS ARE BACK. ATTUNE WHEN YOU ARE READY.";
            }
            return;
        }

        if (!_editMode)
        {
            if (WeaveBtn.Contains(hit)) { WantsWeave = true; return; }
            // AN EMPTY SKILL CARD IS A DOOR. It drew a "+B" hint and did nothing when clicked, which is
            // the one place on this screen a player is most likely to press: the hole where a skill
            // should be.
            for (var i = Loadout.Skills.Count; i < Loadout.SkillCapacity; i++)
                if (AuraCard(i, Loadout.SkillCapacity).Contains(hit)) { WantsWeave = true; return; }
            _resetArmed = false;
            return;
        }

        // ── Edit sub-view: tree + right column. (No BACK and no WEAVE door here any more: MASTERY is
        //    its own rail tile, and a BACK on a page with its own door claims a nesting that does not
        //    exist — the rail is how you leave, same as every other screen.) ──

        if (ResetBtn.Contains(hit) && Mastery.Spent > 0)
        {
            // TWO CLICKS, because it un-spends every point in the tree and there is no undo prompt
            // anywhere else in this game. The respec itself stays free and instant — that is the
            // load-bearing difference between this tree and the permanent one — so the guard is
            // deliberation, not cost.
            if (!_resetArmed) { _resetArmed = true; _msg = "PRESS AGAIN TO TAKE EVERY POINT BACK."; return; }
            _resetArmed = false;
            Mastery.Respec();
            _msg = "ALL MASTERY POINTS RETURNED.";
            Dirty = true;
            return;
        }
        // Any other click on the tree disarms it — but must NOT return, or no node could be taken.
        _resetArmed = false;

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


            var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
            if (Mastery.Take(node.Id))
            {
                _msg = "";
                Dirty = true;
                // THE ATTUNEMENT: your first Specialisation is the moment this game is named for,
                // so it gets a ceremony instead of a click-sound — the hexagon shown whole, the
                // choice sealed or taken back, nothing else clickable until you decide.
                if (firstSpec && node.Form is { } nf) { _attuneNodeId = node.Id; _attuneForm = nf; }
            }
            else _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
                Mastery.Available <= 0 ? "NO MASTERY POINTS — GO DEEPER." :
                node.Kind == MasteryKind.Mastery && Mastery.Affinity() is not null ? "YOU'VE ALREADY MASTERED A FORM." :
                node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null
                    ? "YOU ARE ALREADY ATTUNED — ONE DISCIPLINE PER HUNTER." :
                "TAKE A CONNECTED NODE FIRST.";
            return;
        }

        // Skills, Vows and keystone sockets are all WeaveScreen's now.
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        Tree = tree;
        _hoverInfo = "";
        _hoverNodeId = null;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        if (_editMode) { DrawEditor(b, hit, tree); return; }

        _ui.TextCenterBig(b, "BUILD", 960, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        // The screen's PROMISE, not a recital of the panel headings under it. It read
        // "SOURCE · FORM · VOW · AURAS" — four words already printed larger a few hundred pixels below,
        // spending the biggest line on the page to say nothing the eye had not already reached.
        _ui.TextCenterBig(b, "EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH", 960, 80, Slate, UiTypography.Secondary);

        DrawSummary(b);
        DrawCore(b);
        DrawAuraCards(b, hit);
        DrawPassives(b, hit, tree);

        // TWO BUTTONS, NOT THREE. "EDIT BUILD" and "VIEW TREE" ran the same line of code — both set
        // _editMode — so the screen offered two differently-named doors into one room while the room
        // itself is now a rail tile. What is left names what it opens and what it costs.
        Button(b, WeaveBtn, "CHOOSE YOUR SKILLS", hit, true);
        if (DevBuildDebug) DrawDebug(b);
    }

    private void DrawSummary(SpriteBatch b)
    {
        _ui.PanelQuiet(b, SummaryPanel);
        _ui.TextCenterBig(b, "BUILD OVERVIEW", SummaryPanel.Center.X, SummaryPanel.Y + 24, Gold, UiTypography.SectionTitle);

        var por = new Rectangle(SummaryPanel.Center.X - 66, SummaryPanel.Y + 72, 132, 132);
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);

        var adept = Mastery.Affinity() is { } mf ? $"{Short(mf)} ADEPT" : "SEEKER";
        _ui.TextCenterBig(b, adept, SummaryPanel.Center.X, SummaryPanel.Y + 220, Bone, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"LEVEL {Level}", SummaryPanel.Center.X, SummaryPanel.Y + 254, Gold, UiTypography.Body);
        // "BUILD POWER" IS DELETED, AND THE REASON IS THAT IT WAS NOT A BUILD NUMBER.
        //
        // Playtest: "Build power denen şey nedir? Neye bağlı? Ben skillerimi neye göre seçiyorum da
        // artıyor azalıyor?" Traced it: this was Hunter.PowerRating (Game1 assigns it), which is
        //
        //     damage x skillRate x critFactor x 120  +  defence x 2  +  healthMultiplier x maxHealth x 0.5
        //
        // and every one of those three multipliers is built from GEAR AND STATS ONLY —
        // SquadDamageMultiplier is gear x AttackPower x worn item mods, SquadSkillRate is focus
        // attunement x worn mods x Engineering, SquadHealthMultiplier is Vitality x charm x worn mods.
        // Nothing in it reads a woven skill, a Source, a Form, a Vow, a keystone or the mastery tree.
        //
        // So the answer to "why does it move when I choose skills" is that IT DOES NOT. It is the gear
        // number, printed in the largest type on the one screen where gear is not the subject — and the
        // same figure is already the headline of the GEAR screen (GEAR POWER) and of the STATS card.
        // Detailing it, which was the other option offered, would have meant explaining at length why a
        // number on this page cannot respond to anything on this page.
        //
        // What belongs here instead is the readout the Weave already computes and this screen does not
        // show: damage per second for the build as woven. That arrives with the Weave merge; until then
        // an honest gap beats a confident wrong number.

        // THE FOUR ROWS THAT USED TO SIT HERE ARE GONE, and the panel is a pure identity card.
        //
        // SOURCE FOCUS / ACTIVE FORMS / VOW-BOUND / MASTERY restated, one panel to the right and in
        // larger type, exactly what CORE COMPOSITION already says: SOURCE, FORMS, VOWS. Two panels, the
        // same three facts, four hundred pixels apart — and they had already contradicted each other
        // once (ACTIVE FORMS counted skills while CORE counted distinct Forms, so a build running two
        // BODY STRIKEs reported five against four on one screen).
        //
        // CORE keeps them because it is the panel the screen is named for and it has the room to say
        // them properly; what is left here is who you are and what that is worth.
    }

    private void DrawCore(SpriteBatch b)
    {
        _ui.PanelQuiet(b, CorePanel);
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

        // EVERY CLAUSE IS NOW CONDITIONAL ON THE FACT IT ASSERTS, and the panel no longer contradicts
        // itself. The old line was "{SOURCE}-led auto-skills with {affinity} mastery and layered vow
        // uptime" — and its last four words were a CONSTANT. A build with no vows at all was told it
        // had "layered vow uptime" while the row two lines above it read VOWS: NONE. The mastery
        // clause was wrong in the other direction: Affinity() returns the tree's DOMINANT FORM, so a
        // tree with 24 points spent but no single leading Form printed "no mastery" beside a row in
        // the next panel reading MASTERY 24 POINTS.
        //
        // A note that LOOKS derived and is partly hardcoded is worse than no note: the player cannot
        // tell which half to trust, and here both halves were readable as false from the same screen.
        // The comment above it said "no invented copy" — the invented copy was already there.
        var vowCount = skills.Count(s => Weaving.ById(s.VowId) is not null);
        var note = focus is null
            ? "Add a skill to begin. Every skill is a SOURCE and a FORM."
            : Sentences(focus.Key.ToString().ToUpperInvariant(), vowCount);

        _ui.Fill(b, new Rectangle(CorePanel.X + 44, CorePanel.Bottom - 140, CorePanel.Width - 88, 2), Dim);
        _ui.TextBig(b, "IN SHORT", CorePanel.X + 44, CorePanel.Bottom - 124, Slate, UiTypography.Secondary);
        // WRAPPED, because the sentences are now written to be read rather than to fit. Two lines is
        // what the space below the rows allows once the divider moves up into the void that was there.
        var lines = _ui.WrapBig(note, CorePanel.Width - 88, UiTypography.Body);
        for (var i = 0; i < Math.Min(3, lines.Count); i++)
            _ui.TextBig(b, lines[i], CorePanel.X + 44, CorePanel.Bottom - 96 + i * 28, Bone, UiTypography.Body);
    }

    /// <summary>The plain-words reading of a build: what it leads on, what it has bound, where it leans.</summary>
    private string Sentences(string lead, int vowCount)
    {
        var said = new List<string> { $"Most of your skills draw on {lead}." };
        said.Add(vowCount switch
        {
            0 => "None of them carries a vow yet — a vow makes a skill stronger and charges you for it.",
            1 => "One of them carries a vow.",
            _ => $"{vowCount} of them carry a vow.",
        });
        if (Mastery.Affinity() is { } lean) said.Add($"Your mastery leans {Short(lean)}.");
        else if (Mastery.Spent > 0) said.Add("Your mastery is spread evenly across forms.");
        return string.Join(" ", said);
    }

    private void DrawAuraCards(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, AuraPanel);
        _ui.TextCenterBig(b, "YOUR SKILLS", AuraPanel.Center.X, AuraPanel.Y + 14, Gold, UiTypography.SectionTitle);
        var skills = Loadout.Skills;
        // The slots this player HAS. Against the const, a fifth woven skill was invisible on the one
        // screen whose whole job is to show the build.
        for (var i = 0; i < Loadout.SkillCapacity; i++)
        {
            var card = AuraCard(i, Loadout.SkillCapacity);
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
        _ui.PanelQuiet(b, PassivePanel);
        // +44 clears the panel art's centred top crest, which a centred title at +24 ran into.
        _ui.TextCenterBig(b, "PASSIVES & RESONANCE", PassivePanel.Center.X, PassivePanel.Y + 62, Gold, UiTypography.SectionTitle);

        // 52px, the width of the ornate border. At 32 the left column sat ON the frame and the
        // right-aligned resonance percentages were clipped by the opposite edge.
        var x = PassivePanel.X + 52;

        // THE POINTS LINE GETS ITS OWN ROW. It shared one with the VIEW TREE button, whose left edge is
        // at 1544 — 264px from the text's start, and the string needs about 300. It rendered as
        // "MASTERY POINTS   18 SPENT  ·  6 FI" with the rest under the button. Shortening the label would
        // have hidden it for now and brought it back the moment a player earned a third digit: mastery
        // points are 3 + deepestEver/5 + conquered*5, so three digits is a real endgame value, not a
        // hypothetical. The row is free instead.
        // ONE NAME FOR ONE POOL. This line called them SKILL POINTS while STATS calls the same number
        // MASTERY POINTS and the rail tile that spends them is now labelled MASTERY. Three names for one
        // currency is three currencies as far as a player is concerned.
        _ui.TextBig(b, $"MASTERY POINTS   {Mastery.Spent} SPENT  ·  {Mastery.Available} FREE", x, PassivePanel.Y + 112, Bone, UiTypography.Body);

        // PASSIVES — the real taken mastery nodes (notables + mastery). No invented "trait bonus %" table.
        _ui.TextBig(b, "PASSIVES", x, PassivePanel.Y + 206, Gold, UiTypography.Secondary);
        var taken = MasteryCatalog.Nodes.Where(n => n.Kind is MasteryKind.Notable or MasteryKind.Mastery && Mastery.IsTaken(n.Id)).ToList();
        var py = PassivePanel.Y + 240;
        if (taken.Count == 0) _ui.TextBig(b, "None yet — walk the tree.", x, py, Slate, UiTypography.Body);
        // SHORTENED TO THE COLUMN. Node labels are authored freely and nothing caps their length —
        // MARK MASTERY's runs off the panel and was drawn through the frame art, ending mid-word on
        // "…HITS 40% HA". That reads as a rendering glitch rather than as a label that is too long,
        // which is the worst way for it to fail: nobody files it, and the sentence a player needs in
        // order to evaluate the node is the part that went missing.
        // To the FRAME, not to the 52px text margin. That margin exists so the right-aligned resonance
        // percentages clear the ornate border; these labels are left-aligned and run the other way, so
        // holding them to it truncated three that fit with pixels to spare. The frame's own inset is
        // the real edge.
        var labelWidth = PassivePanel.Right - UiKit.PanelCorner - 4 - (x + 30);
        foreach (var n in taken.Take(6))
        {
            _ui.Diamond(b, new Rectangle(x, py + 2, 18, 18), n.Kind == MasteryKind.Mastery ? Gold : Purple);
            // WRAPPED, NOT SHORTENED. These labels are sentences — cutting them mid-clause left four of
            // five reading as fragments while 150px of the same panel sat empty underneath. Bounded to
            // two lines so six notables still cannot push the KEYSTONES block off the bottom.
            var wrapped = _ui.WrapBig(n.Label, labelWidth, UiTypography.Body);
            for (var li = 0; li < Math.Min(2, wrapped.Count); li++)
            {
                _ui.TextBig(b, wrapped[li], x + 30, py, Bone, UiTypography.Body);
                py += 26;
            }
            py += 12;
        }

        // RESONANCE — the real source composition of the equipped skills (share per source).
        //
        // ANCHORED BELOW THE PASSIVES LIST, not pinned at a fixed +430, for the same reason KEYSTONES
        // below is anchored below the resonance list: the block above it is variable-length. The list
        // takes up to six nodes at 36px from +240, so a player who has walked six notables ends at
        // +456 and this heading at +430 was drawn THROUGH their last passive. Six is not an unusual
        // build; it is what the tree is for. The old fixed position stays as a floor, so a short list
        // still puts the heading exactly where it has always been.
        _ui.TextBig(b, "RESONANCE", x, Math.Max(PassivePanel.Y + 430, py + 16), Gold, UiTypography.Secondary);
        var skills = Loadout.Skills;
        var ry = Math.Max(PassivePanel.Y + 464, py + 50);
        var groups = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).ToList();
        if (groups.Count == 0) _ui.TextBig(b, "No skills equipped.", x, ry, Slate, UiTypography.Body);
        foreach (var g in groups)
        {
            var pct = skills.Count == 0 ? 0 : 100 * g.Count() / skills.Count;
            _ui.Diamond(b, new Rectangle(x, ry + 2, 18, 18), SourceColor.GetValueOrDefault(g.Key, Bone));
            _ui.TextBig(b, $"{g.Key.ToString().ToUpperInvariant()} RESONANCE", x + 30, ry, Bone, UiTypography.Body);
            _ui.TextRightBig(b, $"{pct}%", PassivePanel.Right - 52, ry, SourceColor.GetValueOrDefault(g.Key, Bone), UiTypography.Body);
            ry += 40;
            // A FLOOR, which the keystone block below already has and this loop did not. One row per
            // Source in the loadout, so a six-Source build walks this list straight through the panel's
            // bottom frame and out the other side — and the list is data, so "six" is not hypothetical.
            if (ry > PassivePanel.Bottom - UiKit.PanelCorner - 58) break;
        }

        // KEYSTONES — worn sockets (real). Anchored BELOW the resonance list rather than at a fixed
        // +596: the list is one row per Source in the loadout, so a four-Source build ran its last row
        // (SPIRIT RESONANCE, at +584) straight through this heading. Four is the common case.
        var worn = DustEffects.LearnedKeystones(tree).Where(k => Loadout.HasKeystone(k.Id)).ToList();

        // AND CLAMPED, because pushing it down only moved the collision. Anchoring to the resonance
        // list fixed the overlap and handed the block to the panel's bottom ornament instead — the
        // heading and its line were being drawn on the frame art. The floor here is the last position
        // at which BOTH rows still clear the frame, so the block gets pushed down by a long resonance
        // list exactly as far as there is room for and no further.
        const int keystoneBlockH = 34 + 24;                       // heading, then its line
        var floor = PassivePanel.Bottom - UiKit.PanelCorner - keystoneBlockH;
        var keyY = Math.Min(Math.Max(PassivePanel.Y + 596, ry + 16), floor);
        _ui.TextBig(b, "KEYSTONES", x, keyY, Gold, UiTypography.Secondary);
        if (worn.Count == 0) _ui.TextBig(b, "NONE YET — WEAVE A SKILL TO SLOT ONE", x, keyY + 34, Slate, UiTypography.Body);
        else _ui.TextBig(b, string.Join("  \u00b7  ", worn.Select(k => k.Name.ToUpperInvariant())), x, keyY + 34, Bone, UiTypography.Body);
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
        for (var i = 0; i < Loadout.SkillCapacity; i++) Outline(b, AuraCard(i, Loadout.SkillCapacity), Verd, 2);
        _ui.TextBig(b, $"nav BUILD  overview  {Loadout.SkillCapacity} aura cards", 60, 112, Gold, UiTypography.Secondary);
    }

    // ── Edit sub-view (the existing tree + skill sidebar). ──
    private void DrawEditor(SpriteBatch b, Point hit, MemoryDustTree tree)
    {
        _ui.Scrim(b, 0.6f);
        _ui.Title(b, "MASTERY TREE");
        _ui.Text(b, $"POINTS  {Mastery.Available}  ·  {Mastery.Spent} SPENT", 104, 60, Mastery.Available > 0 ? Gold : Slate);
        if (Mastery.Spent > 0)
            Button(b, ResetBtn, _resetArmed ? "PRESS AGAIN TO CONFIRM" : "TAKE EVERY POINT BACK", hit, true);
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
            // AN UNWALKED WIRE IS DIMMER THAN THE NODE IT TOUCHES, which it was not before: Path is also
            // the locked-node frame tint, so a node and the fatter wire running into it were the same
            // colour and the node disappeared into the web. Half-alpha puts the wire behind the studs.
            var walked = Mastery.IsTaken(node.Id) && Mastery.IsTaken(nearest.Id);
            _ui.LineSeg(b, a, c, WireThickness, walked ? Gold : Path * 0.55f);
        }
        foreach (var node in MasteryCatalog.Nodes) DrawNode(b, node, hit, aff);

        // The right column answers "what am I looking at", not "what are my skill slots".
        //
        // It used to be the SKILL EDITOR — four slots and a keystone strip — sitting beside a tree it has
        // nothing to do with, so the screen asked the player to hold two unrelated jobs at once and
        // answered neither. The tree's own question ("what does this node do, can I afford it, what does
        // it cost me") had no home at all except a one-line strip at the very bottom of the screen.
        DrawNodeDetail(b, hit);
        DrawHexPanel(b, aff);

        var info = _hoverInfo.Length > 0 ? _hoverInfo : _msg.Length > 0 ? _msg : "WEIGHT OPPOSES SPREAD.  TEMPO OPPOSES ENDURE.  ONE BRANCH IS AFFORDABLE; TWO ARE NOT.";
        // SLATE FOR THE IDLE HINT. In Dim this measured ~1.35:1 — the single line that explains the
        // tree's central rule ("one branch is affordable; two are not") was the least readable text on
        // the screen it governs. Bone and Ember still mark the hover and message states.
        var infoColor = _hoverInfo.Length > 0 ? Bone : _msg.Length > 0 ? Ember : Slate;
        _ui.Text(b, info, 200, 1024, infoColor);

        if (_attuneNodeId is not null) DrawAttunement(b, hit);
    }

    /// <summary>
    /// The hexagon column — the tree page's standing answer to "what does my discipline reach".
    /// </summary>
    /// <remarks>
    /// Sits above the node-detail panel so the right side reads as one column: WHO YOU ARE on top,
    /// WHAT YOU ARE READING below. Unattuned it draws quiet and says where attunement lives.
    /// </remarks>
    private void DrawHexPanel(SpriteBatch b, Form? aff)
    {
        _ui.Fill(b, HexPanel, Quiet);
        Outline(b, HexPanel, Path, 2);
        _ui.TextCenter(b, aff is { } a ? $"YOUR ATTUNEMENT — {a.ToString().ToUpperInvariant()}" : "UNATTUNED",
                       HexPanel.Center.X, HexPanel.Y + 14, aff is null ? Slate : Gold);
        if (aff is null)
            _ui.TextCenter(b, "SEAL A SPECIALISATION TO ATTUNE", HexPanel.Center.X, HexPanel.Y + 38, Slate);
        FormHexDiagram.Draw(_ui, b, new Point(HexPanel.Center.X, HexPanel.Y + 268), 105, aff,
                            showFactors: aff is not null);
    }

    /// <summary>
    /// THE ATTUNEMENT — the ceremony for the first Specialisation.
    /// </summary>
    /// <remarks>
    /// The game's founding system deserves a moment, not a click-sound. Deliberately NOT the
    /// inspiration's divination scene: this game's language is the weave, so the ceremony is the
    /// loom shown whole — six seals, your thread bound in gold — and a choice you may still hand
    /// back. Sealing changes nothing the Take didn't already do; the ceremony IS the information.
    /// </remarks>
    private void DrawAttunement(SpriteBatch b, Point hit)
    {
        _ui.Scrim(b, 0.75f);
        _ui.Panel(b, AttunePanel, gold: true);

        var name = _attuneForm.ToString().ToUpperInvariant();
        _ui.TextCenterBig(b, "THE ATTUNEMENT", 960, AttunePanel.Y + 36, Gold, UiTypography.SectionTitle);
        _ui.TextCenter(b, "SIX FORMS ON THE LOOM — ONE ANSWERS YOU.", 960, AttunePanel.Y + 86, Slate);

        FormHexDiagram.Draw(_ui, b, new Point(960, AttunePanel.Y + 340), 150, _attuneForm, showFactors: true);

        _ui.TextCenter(b, $"YOUR {name} SKILLS STRIKE AT DOUBLE WORTH. THE FAR FORMS RESIST —",
                       960, AttunePanel.Y + 604, Bone);
        _ui.TextCenter(b, "A SWORN VOW PULLS AN OFF-DISCIPLINE SKILL ONE RING CLOSER.",
                       960, AttunePanel.Y + 626, Bone);

        Button(b, AttuneSealBtn, $"SEAL IT — {name} IS MINE", hit, true);
        Button(b, AttuneUndoBtn, "NOT YET — TAKE THE POINTS BACK", hit, true);
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
        _ui.PanelQuiet(b, NodePanel);

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
                _ui.TextRightBig(b, $"{taken} \u00b7 {spent} POINTS", NodePanel.Right - 74, y0,
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

    // THE SKILL EDITOR MOVED OUT. It was a column of `< VALUE >` cycling cells wedged into this page's
    // right margin, and it lived here only because the tree happened to have spare width. Cycling is
    // the wrong verb for a list of six — picking SPIRIT from BODY was five clicks and five reads, with
    // nothing on screen naming the other five — and a Vow's whole bargain (does my BUILD meet its
    // demand?) could not be answered from a cell that showed one word at a time.
    //
    // WeaveScreen owns it now, at full width, with every Source, every Form and every studied Vow
    // visible at once and each Vow's demand checked live against the build. Deleted rather than left
    // behind a flag: two editors for one loadout is the parallel-systems failure this codebase has
    // been bitten by twice already.

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
            // THE FIELD FOLLOWS THE FRAME'S SHAPE. It was an axis-aligned Rectangle inset 30% into every
            // frame — a square patch inside a circle, an octagon and a diamond, so a taken Minor read as
            // a coloured square wearing a ring and the DIAMOND kind wore a square that poked out of its
            // own points. The three procedural shapes UiKit already owns cover all four frame outlines.
            //
            // START is exempt. It is permanently allocated, so it takes the "taken" branch of `fill` and
            // came out as a bright WEIGHT-red field at the dead centre of the tree — reading as the most
            // emphatic Weight node on the page when it belongs to no branch at all.
            var pad = (int)(box.Width * 0.30f);
            var field = new Rectangle(box.X + pad, box.Y + pad, box.Width - pad * 2, box.Height - pad * 2);
            var fieldCol = node.Kind == MasteryKind.Start ? PanelBg : fill;
            if (node.Kind == MasteryKind.Specialisation) _ui.Diamond(b, field, fieldCol);
            else if (node.Kind == MasteryKind.Notable || node.Kind == MasteryKind.Bridge) _ui.Hex(b, field, fieldCol);
            else if (node.Kind != MasteryKind.Mastery) _ui.Diamond(b, Circleish(field), fieldCol);

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

    /// <summary>
    /// A wire's thickness in pixels at the current zoom.
    /// </summary>
    /// <remarks>
    /// 5.7 is not a taste call. PrestigeScreen — the tree the same playtester has NOT complained about —
    /// draws a 3px wire against a 52px node, a ratio of 1:17.3. A Minor node here is 98.8 * _zoom pixels
    /// across, and 98.8 / 17.3 = 5.7. So these wires land at exactly the weight the trait tree already
    /// ships, at every zoom, instead of a constant 8 that is 29% of a node at the default camera and 57%
    /// of one when the player zooms out to see the whole tree.
    /// </remarks>
    private float WireThickness => Math.Clamp(5.7f * _zoom, 1.2f, 9f);

    /// <summary>
    /// A field for a ROUND frame, drawn as a diamond grown to touch the circle it sits in.
    /// </summary>
    /// <remarks>
    /// UiKit has no disc primitive and this file must not add art. A diamond inscribed in the circle
    /// leaves visible gaps at the diagonals, so it is grown by sqrt(2)/2 of the inset instead: the
    /// points now reach the rim and the flats sit just inside it, which at a Minor's 28px default size
    /// reads as a filled socket rather than as a rotated square.
    /// </remarks>
    private static Rectangle Circleish(Rectangle r)
    {
        var grow = (int)MathF.Round(r.Width * 0.14f);
        return new Rectangle(r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
