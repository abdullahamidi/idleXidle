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
/// Built to the Dust production spec (rev 1). Every value is real, from <see cref="MemoryDustTree"/>: every
/// unlock with its name, cost, description, prerequisites, and its <see cref="UnlockEffect"/> category,
/// explained in plain sentences by <see cref="MemoryDustText"/>. The real tree is a graph of BINARY unlocks
/// (owned / available / locked), not tiered ranks, so the reference's "Tier 4/10" becomes an honest
/// LEARNED / AVAILABLE / LOCKED state (UX standard §10/§11). The class keeps its name and Draw/Update
/// signatures so the host wiring is unchanged.
/// </remarks>
public sealed class PrestigeScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    // 0x5E5A6E, up from 0x3A3A44. Dim is the LOCKED outline and the divider rule, and at the old value a
    // locked node on the 0xC0-alpha scrim was a square of near-black on near-black — the playtest said
    // the tree was hard to read, and forty of its fifty-one nodes are locked at any given career. A
    // locked trait should look unbought, not absent.
    private static readonly Color Dim = new(0x5E, 0x5A, 0x6E);
    /// <summary>The tint of a locked node's icon — lighter than Dim, so the glyph still reads as a glyph.</summary>
    private static readonly Color LockedInk = new(0x84, 0x7E, 0x96);
    /// <summary>The dark plate a name sits on, so a wire running behind it never runs through it.</summary>
    private static readonly Color Plate = new(0x0A, 0x08, 0x10);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Violet = new(0xC0, 0x6E, 0xE0);
    private static readonly Color Teal = new(0x5F, 0xE0, 0xC8);

    private readonly UiKit _ui;
    private string _selectedId = "";
    /// <summary>The node under the pointer this frame — the detail panel prefers it to the pinned one.</summary>
    private string? _hoverId;
    private string _msg = "";
    private KeyboardState _prevKeys;

    // ── The unlock flourish ──────────────────────────────────────────────────────────────────────
    //
    // A trait is PERMANENT and there is no respec. That is the most consequential button in the game,
    // and pressing it used to change one word in a status line from AVAILABLE to LIT. The screen was
    // accused of being "çok basit ve kalitesiz" and this was the heart of it: nothing about taking a
    // trait felt like it had happened.
    //
    // Time-driven, not frame-driven, so the shape of the celebration is the same on any machine.
    private float _litT = -1f;             // seconds into the flourish; negative means idle
    private string _litName = "";
    private TraitRoad _litRoad;
    private string? _litArt;               // the terminal's own emblem, when a terminal was taken
    private bool _litTerminal;
    private bool _litFrozen;               // DEV: hold one frame of the flourish for a capture
    private string? _cue;                  // sound cue waiting for the host to play

    /// <summary>How long the flourish runs. A terminal ends a road; it is allowed to take twice as long.</summary>
    private float LitDuration => _litTerminal ? 2.2f : 1.0f;

    /// <summary>
    /// The camera kick, in 1920-space pixels. The host adds it to the overlay transform.
    /// </summary>
    /// <remarks>
    /// Owned here rather than in the host's present blit, which is where a global shake used to live and
    /// where it sat at a permanent zero for the whole of development because nothing drove it. A screen
    /// that wants a kick should own one; a shared one nobody owns decays into dead code.
    ///
    /// Deliberately NOT applied to the mouse mapping: a pointer that slides out from under the cursor
    /// for a fifth of a second is a bug, not game feel.
    /// </remarks>
    public Vector2 Shake { get; private set; }

    /// <summary>
    /// The sound cue for a trait just taken, cleared by reading — the host owns audio, this screen does not.
    /// </summary>
    /// <remarks>Same shape as StatsScreen.ConsumeTrain, so the host's Update reads one way everywhere.</remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    public bool DevDustDebug { get; set; }

    public PrestigeScreen(UiKit ui, MemoryDustTree tree)
    {
        _ui = ui;
        _selectedId = tree.All.OrderBy(u => u.Cost).First().Id;
        // Every node this tree holds must have a home in the diagram. The layout covers the catalogue by
        // construction; this is the loud version for a debug build, so a tree the layout has never seen
        // is noticed at the desk rather than in a capture — and Pos() still draws it either way.
        System.Diagnostics.Debug.Assert(TraitTreeLayout.Covers(tree.All.Select(u => u.Id)),
            "TraitTreeLayout does not cover every node of this tree — see TraitTreeLayout.Unauthored / PrestigeScreen.Pos");
    }

    /// <summary>DEV ONLY: pose the detail panel on a specific blessing for the screenshot fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    /// <summary>DEV ONLY: freeze the unlock flourish part-way through so a capture can prove it draws.</summary>
    /// <remarks>
    /// A celebration is the one thing a still screenshot cannot catch by accident — it is over in a
    /// second, and the capture rig renders a fixed frame count and exits. Without this, "the flourish
    /// works" would be a claim rather than a verified fact, which is exactly what this project's
    /// verification rule exists to prevent.
    /// </remarks>
    public void DevPoseLit(MemoryDustTree tree, string id, float t)
    {
        if (tree.All.FirstOrDefault(u => u.Id == id) is not { } u) return;
        _selectedId = id;
        BeginLit(u);
        _litT = t;
        // FROZEN, or the fixture is useless: the capture rig renders sixty frames before it saves, so
        // an un-frozen pose advances a full second and every mode would screenshot the same empty
        // moment after the flourish had already finished.
        _litFrozen = true;
        TickFlourish(0f);   // settle Shake for the posed instant, so the kick is in the picture too
    }

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    // The diagram takes the grid's slot AND the overview's. The overview existed to carry a road
    // FILTER and a table of road prices; a drawn tree answers both — you can see the roads, and each
    // one prints its own count and price over its terminal.
    // 790, NOT 718 — the two panels stopped at y=862 while the page runs to 934, so the screen wore a
    // 72px empty margin under both columns AND squeezed the diagram into what was left. The tree's scale
    // is derived from the view height, so those 72 pixels are worth about 15% on every node and every
    // gap between them: this is the only lever that makes the diagram bigger without moving a node.
    private static readonly Rectangle TreePanel = new(38, 144, 1246, 790);
    private static readonly Rectangle DetailPanel = new(1320, 144, 560, 790);

    /// <summary>
    /// The tree is grouped by ROAD, not by effect category.
    /// </summary>
    /// <remarks>
    /// Amplifier / Expansion / Convenience describes what a node DOES to the engine, which is an
    /// implementation fact and tells a player nothing about the decision in front of them. The decision
    /// is which of four roads to walk, and it is the one thing the screen has to communicate: two roads
    /// cost sixty against roughly thirty-four earnable, so the road you do not take is permanent.
    /// </remarks>
    private static string RoadName(TraitRoad r) => r switch
    {
        TraitRoad.Spine => "THE SPINE",
        TraitRoad.Ruin => "RUIN",
        TraitRoad.Aegis => "AEGIS",
        TraitRoad.Avarice => "AVARICE",
        _ => "ARTIFICE",
    };

    /// <summary>What walking a road means, in a few plain words — not its slogan.</summary>
    private static string RoadBlurb(TraitRoad r) => r switch
    {
        TraitRoad.Spine => "Room to grow: sockets, slots, vows, filters, the forge",
        TraitRoad.Ruin => "Hit harder; live closer to death",
        TraitRoad.Aegis => "Take more hits; strike less often",
        TraitRoad.Avarice => "Bring back more loot; hit softer",
        _ => "Do strange things, not big ones",
    };

    private static Color RoadColor(TraitRoad r) => r switch
    {
        TraitRoad.Spine => Teal,
        TraitRoad.Ruin => new Color(0xD6, 0x48, 0x5C),
        TraitRoad.Aegis => new Color(0x74, 0x9A, 0xE8),
        TraitRoad.Avarice => Gold,
        _ => Violet,
    };

    private static Color CatColor(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => Gold, UnlockEffect.Expansion => Violet, _ => Teal,
    };

    /// <summary>The road's emblem. Tinted at the draw site, so one asset serves every state.</summary>
    private static string RoadGlyph(TraitRoad r) => r switch
    {
        TraitRoad.Spine => "icon_road_spine",
        TraitRoad.Ruin => "icon_road_ruin",
        TraitRoad.Aegis => "icon_road_aegis",
        TraitRoad.Avarice => "icon_road_avarice",
        _ => "icon_road_artifice",
    };

    /// <summary>
    /// A TERMINAL's own emblem — the four 12-point nodes each road ends on, and nothing else.
    /// </summary>
    /// <remarks>
    /// Keyed off the granted keystone rather than the cost, because "costs 12" is a balance value that
    /// may move and "is the end of the Ruin road" is not. Everything else on the screen shares three
    /// category icons; these four are the only traits expensive enough to deserve a picture of their own,
    /// and two of them cost more than a career earns — a player will mostly meet them by reading them.
    /// </remarks>
    private static string? TerminalArt(MemoryDustUnlock u) => u.GrantsKeystone switch
    {
        "reaper" => "art_terminal_reaper",
        "titan" => "art_terminal_titan",
        "hoarder" => "art_terminal_hoarder",
        "weaver" => "art_terminal_weaver",
        _ => null,
    };

    private static string CatIcon(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => "icon_blessing_amplifier",
        UnlockEffect.Expansion => "icon_blessing_expansion",
        _ => "icon_blessing_convenience",
    };

    // Spine first (everyone walks it), then the four roads in enum order. WITHIN a road, by cost —
    // which is the order it is walked, so a road reads left to right as the chain it actually is.
    private List<MemoryDustUnlock> Ordered(MemoryDustTree tree) =>
        tree.All.OrderBy(u => (int)u.Road).ThenBy(u => u.Cost).ThenBy(u => u.Name).ToList();

    private MemoryDustUnlock Selected(MemoryDustTree tree) =>
        tree.All.FirstOrDefault(u => u.Id == _selectedId) ?? Ordered(tree).First();

    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    public void Update(KeyboardState keys, Point mouse, bool clicked, int wheel, MemoryDustTree tree)
        => Update(keys, mouse, clicked, wheel, tree, 1f / 60f);

    public void Update(KeyboardState keys, Point mouse, bool clicked, int wheel, MemoryDustTree tree, float dt)
    {
        TickFlourish(dt);
        var list = Ordered(tree);

        // Arrows walk the tree in road order and Enter buys. The grid's row/column arithmetic is gone
        // with the grid: a diagram has no rows, so up and down step by one exactly as left and right do.
        var idx = Math.Max(0, list.FindIndex(u => u.Id == _selectedId));
        if (Pressed(keys, Keys.Left) || Pressed(keys, Keys.Up)) idx = Math.Max(0, idx - 1);
        if (Pressed(keys, Keys.Right) || Pressed(keys, Keys.Down)) idx = Math.Min(list.Count - 1, idx + 1);
        if (list.Count > 0) _selectedId = list[idx].Id;
        if (Pressed(keys, Keys.Enter)) Buy(tree, Selected(tree));

        _prevKeys = keys;
    }

    private void Buy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (tree.Owns(u.Id)) _msg = "ALREADY LEARNED — A TRAIT IS PERMANENT.";
        else if (tree.Purchase(u.Id)) { _msg = $"LEARNED: {u.Name}."; BeginLit(u); }
        else if (tree.Available < u.Cost) _msg = "NOT ENOUGH TRAIT POINTS — CONQUER A REGION, GO DEEPER INTO THE CORRUPTION ON THE MAP, OR RAISE A REGION'S MASTERY TO EARN MORE.";
        else _msg = "LOCKED — LEARN WHAT IT NEEDS FIRST.";
    }

    /// <summary>Arm the flourish for a trait that was just bought.</summary>
    private void BeginLit(MemoryDustUnlock u)
    {
        _litT = 0f;
        _litFrozen = false;
        _litName = u.Name;
        _litRoad = u.Road;
        _litArt = TerminalArt(u);
        _litTerminal = _litArt is not null;
        // Specific first, then generic: a terminal gets its own cue if one is authored, and falls back
        // to the ordinary one rather than to silence.
        _cue = _litTerminal ? "sfx_trait_terminal" : "sfx_trait_lit";
    }

    /// <summary>
    /// Advance the flourish and the camera kick it drives.
    /// </summary>
    /// <remarks>
    /// The kick is a decaying oscillation, NOT a random jitter: a capture has to be able to reproduce
    /// the same frame twice, and a shake sampled from an RNG makes every screenshot of it a different
    /// picture. Two incommensurate frequencies read as a knock rather than a wobble.
    /// </remarks>
    private void TickFlourish(float dt)
    {
        if (_litT < 0f) { Shake = Vector2.Zero; return; }

        if (!_litFrozen) _litT += dt;
        if (_litT >= LitDuration) { _litT = -1f; Shake = Vector2.Zero; return; }

        // Shake belongs to the first third and then gets out of the way — a screen that is still
        // moving while the player reads the name is a screen they cannot read.
        var window = LitDuration * 0.34f;
        if (_litT > window) { Shake = Vector2.Zero; return; }

        var energy = 1f - _litT / window;
        var amp = (_litTerminal ? 22f : 8f) * energy * energy;
        Shake = new Vector2(MathF.Sin(_litT * 71f) * amp, MathF.Cos(_litT * 53f) * amp * 0.7f);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, MemoryDustTree tree) => Draw(b, tree, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, MemoryDustTree tree, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));
        // TRAITS, not DUST. The screen stopped spending Memory Dust when the tree stopped being buyable
        // by idling; a title naming a currency it does not charge is the kind of small lie that makes a
        // player mistrust every other number on the screen. Dust is still real — it is the Warren's
        // material, and it still sits in the top bar.
        _ui.TextCenterBig(b, "TRAITS", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // "NO TAKING BACK", not "NO RESPEC" — the reader plays in English as a second language, and
        // "respec" is a word only the genre knows.
        _ui.TextCenterBig(b, "ONE SPINE  ·  FOUR ROADS  ·  NO TAKING BACK", 960, 80, Slate, UiTypography.Secondary);

        // The numbers the deleted overview panel existed to carry, said in words: how many points you
        // have to spend, how much of the tree you have learned, and what all of it would cost. A header
        // rather than a panel: a player checks "can I afford this" constantly and "what does the whole
        // tree cost" once, and neither is worth a third of the page.
        var spent = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        var learned = tree.All.Count(u => tree.Owns(u.Id));
        _ui.TextBig(b, "TRAIT POINTS TO SPEND", 60, 108, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{tree.Available}", 282, 104, tree.Available > 0 ? Gold : Slate, UiTypography.PanelTitle);
        _ui.TextRightBig(b, $"{learned} OF {tree.All.Count} TRAITS LEARNED  ·  {spent} POINTS SPENT  ·  {tree.TotalTreeCost} FOR EVERYTHING",
                         1284, 108, Slate, UiTypography.Secondary);

        _hoverId = null;
        DrawTree(b, tree, hit, clicked);
        DrawDetail(b, tree, hit, clicked);
        DrawFlourish(b);
        if (DevDustDebug) DrawDebug(b);
    }

    /// <summary>
    /// What taking a trait looks like: a flash, a shockwave, a burst, and the name of what you just
    /// became — over the whole screen, because the whole screen is what changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drawn LAST and over everything, including the three panels. The decision it is celebrating is
    /// irreversible, so it is allowed to interrupt; it also never blocks input, because a player who
    /// wants to keep spending should not be made to wait out an animation they have already seen.
    /// </para>
    /// <para>
    /// Every beat is a slice of one normalised clock, so retiming the whole thing is one constant.
    /// A terminal ends a road and gets the long version: its own emblem, a third ring, and a second
    /// line naming the road that just finished.
    /// </para>
    /// </remarks>
    private void DrawFlourish(SpriteBatch b)
    {
        if (_litT < 0f) return;

        var p = Math.Clamp(_litT / LitDuration, 0f, 1f);
        var accent = _litTerminal ? Gold : RoadColor(_litRoad);
        var centre = new Point(960, 486);

        // EVERY fade below is `colour * float`, never `new Color(r, g, b, someByte)`.
        //
        // This batch blends PREMULTIPLIED, so a colour's RGB must already be scaled by its own alpha.
        // Building one component-wise leaves the RGB at full strength, and the blend then reads it as
        // "add all of this gold, keep most of what was underneath" — the first capture of this flourish
        // came out as a solid amber sheet with the entire screen visible faintly through it. MonoGame's
        // Color*float scales all four components, which is exactly the premultiplied form.
        //
        // 1. THE PAGE STANDS BACK. Ramped in over a tenth of a second and out over the last quarter, so
        // the celebration owns the screen while it runs instead of competing with a grid of forty-four
        // cards for the player's eye. Without it the burst and the banner are just more things drawn on
        // a busy page, which is the whole complaint this pass is answering.
        var hold = Math.Min(1f, p / 0.10f) * Math.Min(1f, (1f - p) / 0.25f);
        _ui.Fill(b, UiKit.OverlayScrim,
                 new Color(0x06, 0x04, 0x0A) * (hold * (_litTerminal ? 0.66f : 0.55f)));

        // 2. THE FLASH. Short and bright: it is the frame in which the decision landed.
        if (p < 0.14f)
        {
            var f = 1f - p / 0.14f;
            _ui.Fill(b, UiKit.OverlayScrim, accent * (f * f * 0.55f));
        }

        // 3. THE SHOCKWAVES. Plotted, not scaled from a sprite — see build_spec.py, where the generated
        // ring was abandoned. A 256px texture blown up to 900 across is softest exactly when it is
        // biggest; a plotted circumference is one crisp band at any radius.
        var waves = _litTerminal ? 3 : 2;
        for (var i = 0; i < waves; i++)
        {
            var lead = p - i * 0.10f;
            if (lead <= 0f || lead >= 1f) continue;
            var eased = 1f - (1f - lead) * (1f - lead);            // fast out, settling
            var radius = (int)(60 + eased * (_litTerminal ? 940 : 620));
            var fade = (1f - lead) * (1f - lead) * 0.9f;
            Ring(b, centre, radius, Math.Max(2, (int)(9 * (1f - lead))), accent * fade);
        }

        // 4. THE BURST, twice: a wide slow one for the glare and a tight fast one for the spark.
        if (_ui.Assets.Get("vfx_trait_burst") is { } burst)
            for (var i = 0; i < 2; i++)
            {
                var lead = p / (i == 0 ? 1f : 0.55f);
                if (lead >= 1f) continue;
                var size = (int)((_litTerminal ? 560 : 380) * (i == 0 ? 0.5f + lead * 1.9f : 0.3f + lead * 0.9f));
                var fade = (1f - lead) * (1f - lead) * (i == 0 ? 0.75f : 1f);
                b.Draw(burst, new Rectangle(centre.X - size / 2, centre.Y - size / 2, size, size), accent * fade);
            }

        // 5. THE FACE. A terminal shows its own emblem; every other trait shows the ROAD it just moved
        // you along — which is the thing that actually changed, and the reason a burst alone was not
        // enough. Either way the picture swells out of the flare and settles behind the name, so the
        // celebration has a subject rather than being light with a caption under it.
        var faceKey = _litTerminal ? _litArt : RoadGlyph(_litRoad);
        if (faceKey is { } key && _ui.Assets.Get(key) is { } art)
        {
            var grow = Math.Min(1f, p / 0.30f);
            var eased = 1f - (1f - grow) * (1f - grow) * (1f - grow);
            var size = (int)(_litTerminal ? 150 + eased * 250 : 90 + eased * 130);
            var fade = Math.Min(1f, (1f - p) * 3.4f);
            _ui.SpriteFit(b, art, new Rectangle(centre.X - size / 2, centre.Y - size / 2 - 40, size, size),
                          (_litTerminal ? Color.White : accent) * fade);
        }

        // 6. THE NAME. It arrives after the flash rather than under it, rises as it settles, and holds
        // legible for most of the run — this is the only part a player actually has to READ.
        if (p < 0.08f) return;
        var t = (p - 0.08f) / 0.92f;
        var rise = (int)((1f - Math.Min(1f, t * 4f)) * 46f);
        var alpha = Math.Min(1f, t * 6f) * Math.Min(1f, (1f - t) * 5f);
        if (alpha <= 0.01f) return;

        var y = (_litTerminal ? 706 : 626) + rise;
        // Edge to edge. At 1320 wide it started and stopped at two arbitrary points inside the overview
        // and detail panels, so its two rules read as a box someone had dropped on the page rather than
        // as a banner the page was showing.
        var plate = new Rectangle(0, y - 26, 1920, _litTerminal ? 154 : 114);
        _ui.Fill(b, plate, new Color(0x0A, 0x08, 0x10) * (alpha * 0.86f));
        _ui.Fill(b, new Rectangle(plate.X, plate.Y, plate.Width, 3), accent * alpha);
        _ui.Fill(b, new Rectangle(plate.X, plate.Bottom - 3, plate.Width, 3), accent * alpha);

        _ui.TextCenterBig(b, _litTerminal ? "A ROAD ENDS HERE" : "TRAIT LEARNED — AND IT IS PERMANENT",
                          centre.X, plate.Y + 16, accent * alpha, UiTypography.Secondary);
        _ui.TextCenterBig(b, _litName, centre.X, plate.Y + 48, Bone * alpha, UiTypography.ScreenTitle, TextFace.Display);
        if (_litTerminal)
            _ui.TextCenterBig(b, RoadName(_litRoad) + "  ·  WALKED TO ITS END", centre.X, plate.Y + 106,
                              Gold * alpha, UiTypography.Body);
    }

    /// <summary>
    /// A hollow circle of small squares. The shockwave the generator would not draw.
    /// </summary>
    /// <remarks>
    /// Step count follows the radius so the band stays continuous as it expands — a fixed step count
    /// draws a solid ring when it is small and a dotted one when it is large, which is precisely
    /// backwards from what an expanding shockwave should do.
    /// </remarks>
    private void Ring(SpriteBatch b, Point c, int radius, int thick, Color col)
    {
        var steps = Math.Clamp(radius * 4, 48, 1600);
        for (var i = 0; i < steps; i++)
        {
            var a = i / (float)steps * MathF.Tau;
            var x = c.X + (int)(MathF.Cos(a) * radius);
            var y = c.Y + (int)(MathF.Sin(a) * radius);
            if (x < -thick || x > 1920 + thick || y < -thick || y > 1080 + thick) continue;
            _ui.Fill(b, new Rectangle(x - thick / 2, y - thick / 2, thick, thick), col);
        }
    }

    /// <summary>
    /// THE TREE, as a diagram: a spine along the bottom and four roads climbing out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaced a grid of forty-four cards grouped by road. The grid was readable — every card
    /// said its name, its cost and its state — and it still told the player nothing, because the one
    /// thing this tree is FOR cannot be written on a card. Two roads cost sixty points against a career
    /// that earns about thirty-four; the road you do not walk is the permanent shape of your character.
    /// A list cannot say that. A picture of four terminals side by side at the top, each thirty points
    /// up its own ladder, says it before a word is read.
    /// </para>
    /// <para>
    /// Edges are the REAL prerequisites, drawn gold once both ends are lit, so a walked road reads as
    /// one continuous line. The spine hangs downward and the roads climb upward from the exact node
    /// that gates them, which is how the diagram shows that the spine is not optional.
    /// </para>
    /// <para>
    /// EVERY NODE PRINTS ITS NAME. The first diagram drew an icon and a cost digit and nothing else, so
    /// a player had to hover thirty-nine squares to learn what any of them was called — the playtest's
    /// "make it more readable" was mostly this. Names sit under their node on a dark plate, in two
    /// short lines, and the layout's lanes are sized for them.
    /// </para>
    /// </remarks>
    private void DrawTree(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.Panel(b, TreePanel);

        // ONE EDGE PER NODE, to its NEAREST prerequisite — the rule the mastery tree arrived at, and
        // for the identical reason. COMPLETE ATTUNEMENT requires one node from the head of all four
        // roads plus a socket; drawing all five made it a spider whose legs crossed every other chain
        // on the page, and the first capture of this diagram had teal wires running corner to corner.
        //
        // A prerequisite list is an "all of these" rule, and the DETAIL PANEL states it exactly. The
        // drawing's job is the SHAPE — which chain a node hangs off — and one line says that better
        // than five. A taken prerequisite wins ties, so a walked road reads as one continuous gold run.
        foreach (var u in tree.All)
        {
            if (u.Requires.Count == 0) continue;
            var to = Pos(u.Id);

            string? nearest = null;
            var best = float.MaxValue;
            foreach (var reqId in u.Requires)
            {
                var d = Vector2.DistanceSquared(Pos(reqId), to) - (tree.Owns(reqId) ? 10_000f : 0f);
                if (d >= best) continue;
                best = d;
                nearest = reqId;
            }
            if (nearest is null) continue;

            var lit = tree.Owns(u.Id) && tree.Owns(nearest);
            Line(b, ToScreen(Pos(nearest)), ToScreen(to),
                 lit ? Gold : RoadColor(u.Road) * 0.34f, lit ? 4 : 3);
        }

        // The four road labels, above their terminals — the one line that names what the player is
        // choosing between, and what each road costs against the points a career earns.
        var heads = new[]
            {
                (TraitRoad.Ruin, "ks_reaper"), (TraitRoad.Aegis, "ks_titan"),
                (TraitRoad.Artifice, "ks_weaver"), (TraitRoad.Avarice, "ks_hoarder"),
            }
            .Where(h => tree.All.Any(u => u.Id == h.Item2))
            .Select(h => (h.Item1, At: ToScreen(Pos(h.Item2))))
            .OrderBy(h => h.At.X)
            .ToList();

        // THE COLUMN, MEASURED. These four summaries are centred on their terminals at fixed positions,
        // and each was drawn at whatever width its text happened to be — so ARTIFICE's "30 PTS" ran
        // straight into AVARICE's "0/4" and the row read "…30 PTS0/4 · 30 PTS". Deriving the width from
        // the actual gap between neighbouring roads means the label shortens instead of colliding, and
        // it keeps holding when a road's cost grows a digit.
        var column = heads.Count < 2
            ? 400
            : (int)Enumerable.Range(1, heads.Count - 1)
                             .Min(i => heads[i].At.X - heads[i - 1].At.X) - 20;

        foreach (var (road, p) in heads)
        {
            var nodes = tree.All.Where(u => u.Road == road).ToList();
            var lit = nodes.Count(u => tree.Owns(u.Id));
            var top = (int)p.Y - TerminalR;
            _ui.TextCenterBig(b, _ui.ShortenBig(RoadName(road), column, UiTypography.Body),
                              (int)p.X, top - 58,
                              lit > 0 ? RoadColor(road) : RoadColor(road) * 0.8f, UiTypography.Body);
            _ui.TextCenterBig(b,
                _ui.ShortenBig($"{lit} OF {nodes.Count}  ·  {nodes.Sum(u => u.Cost)} POINTS", column,
                               UiTypography.Secondary),
                (int)p.X, top - 30, lit > 0 ? Gold : Slate, UiTypography.Secondary);
        }

        // Names first, then nodes, then their cost tags: a plate never covers a node, and a tag sits
        // over whatever wire runs under it.
        foreach (var u in tree.All) DrawName(b, tree, u);
        foreach (var u in tree.All) DrawNode(b, tree, u, hit, clicked);
    }

    /// <summary>Node radius in pixels for everything but a terminal.</summary>
    private const int NodeR = 19;

    /// <summary>A terminal draws larger — it costs twelve where its neighbours cost four, and size is the channel.</summary>
    private const int TerminalR = 27;

    /// <summary>The size the names under the nodes are drawn at. The smallest face on the screen, and still a face.</summary>
    private const int NamePx = 15;
    private const int NameLineH = 16;

    /// <summary>The name under a node: two short lines on a dark plate, sized to the lane.</summary>
    private void DrawName(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u)
    {
        var p = ToScreen(Pos(u.Id));
        var r = TerminalArt(u) is not null ? TerminalR : NodeR;
        var lines = NameLines(u.Name);
        if (lines.Count == 0) return;

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var hot = u.Id == _selectedId || u.Id == _hoverId;
        var ink = isOwned ? Gold : buyable || hot ? Bone : Slate;

        var w = lines.Max(l => _ui.MeasureBig(l, NamePx));
        var top = (int)p.Y + r + 3;
        _ui.Fill(b, new Rectangle((int)p.X - w / 2 - 4, top - 1, w + 8, lines.Count * NameLineH + 2),
                 Plate * (hot ? 0.95f : 0.82f));
        for (var i = 0; i < lines.Count; i++)
            _ui.TextCenterBig(b, lines[i], (int)p.X, top + i * NameLineH, ink, NamePx);
    }

    /// <summary>
    /// A name as at most two lines that fit a lane.
    /// </summary>
    /// <remarks>
    /// Balanced, not greedy: "THE HOARDER'S SHARE" greedy-packed is "THE" over "HOARDER'S SHARE", and
    /// the second line then has to be cut. Of the splits where both lines fit, the one whose longer line
    /// is shortest wins; if no split fits, the least-bad one is taken and each line is shortened with an
    /// ellipsis — the detail panel always carries the full name. Breaks after a hyphen as well as at a
    /// space, for THE TWICE-SPOKEN.
    /// </remarks>
    private IReadOnlyList<string> NameLines(string name)
    {
        var width = LanePixels() - 8;
        if (_nameLines.TryGetValue((name, width), out var cached)) return cached;

        var tokens = new List<string>();
        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var cut = word.IndexOf('-');
            if (cut > 0 && cut < word.Length - 1) { tokens.Add(word[..(cut + 1)]); tokens.Add(word[(cut + 1)..]); }
            else tokens.Add(word);
        }

        string Join(IEnumerable<string> ts)
        {
            var s = "";
            foreach (var t in ts) s = s.Length == 0 || s.EndsWith('-') ? s + t : s + " " + t;
            return s;
        }

        List<string> result;
        var whole = Join(tokens);
        if (tokens.Count <= 1 || _ui.MeasureBig(whole, NamePx) <= width) result = new() { whole };
        else
        {
            (string A, string B, int Over, int Max)? best = null;
            for (var i = 1; i < tokens.Count; i++)
            {
                var a = Join(tokens.Take(i));
                var c = Join(tokens.Skip(i));
                var wa = _ui.MeasureBig(a, NamePx);
                var wc = _ui.MeasureBig(c, NamePx);
                var over = Math.Max(0, wa - width) + Math.Max(0, wc - width);
                var max = Math.Max(wa, wc);
                if (best is null || over < best.Value.Over || (over == best.Value.Over && max < best.Value.Max))
                    best = (a, c, over, max);
            }
            var (l1, l2, _, _) = best!.Value;
            result = new() { _ui.ShortenBig(l1, width, NamePx), _ui.ShortenBig(l2, width, NamePx) };
        }

        _nameLines[(name, width)] = result;
        return result;
    }

    private readonly Dictionary<(string, int), IReadOnlyList<string>> _nameLines = new();

    /// <summary>One node: a socket whose colour is its road and whose brightness is its state.</summary>
    private void DrawNode(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u, Point hit, bool clicked)
    {
        var p = ToScreen(Pos(u.Id));

        var terminal = TerminalArt(u);
        var r = terminal is not null ? TerminalR : NodeR;
        var box = new Rectangle((int)p.X - r, (int)p.Y - r, r * 2, r * 2);
        // The name plate is part of the node for the pointer: a player aims at the word as readily as
        // at the square, and a hit box that stops at the square makes the plate feel broken.
        var lines = NameLines(u.Name);
        // …so the hit box is the UNION of the square and the plate, the plate measured the way DrawName draws it.
        var plateW = lines.Count == 0 ? 0 : lines.Max(l => _ui.MeasureBig(l, NamePx)) + 8;
        var left = Math.Min(box.X - 4, (int)p.X - plateW / 2);
        var right = Math.Max(box.Right + 4, (int)p.X - plateW / 2 + plateW);
        var hitBox = new Rectangle(left, box.Y, right - left, box.Height + 3 + lines.Count * NameLineH + 2);
        var hover = hitBox.Contains(hit);

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var sel = u.Id == _selectedId;
        var road = RoadColor(u.Road);

        if (clicked && hover) { _selectedId = u.Id; _msg = ""; }
        if (hover) _hoverId = u.Id;

        _ui.Fill(b, box, isOwned ? road * 0.85f : buyable ? road * 0.30f : new Color(0x1A, 0x16, 0x26, 0xF0));
        Outline(b, box, sel ? Bone : isOwned ? Gold : buyable ? road : Dim, sel || isOwned ? 3 : 2);

        if (terminal is not null)
            _ui.Icon(b, terminal, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8),
                     isOwned || buyable ? Color.White : new Color(0x8A, 0x84, 0x80));
        else if (!_ui.Icon(b, CatIcon(u.Effect),
                           new Rectangle(box.X + 5, box.Y + 5, box.Width - 10, box.Height - 10),
                           isOwned ? new Color(0x14, 0x11, 0x1E) : buyable ? road : LockedInk)
                 && _ui.Assets.Get("ui_memory_dust") is { } ic)
            b.Draw(ic, new Rectangle(box.X + 5, box.Y + 5, box.Width - 10, box.Height - 10), road);

        // The cost, as a small tag on the node's right edge. It used to be printed inside the square,
        // over the icon; now the square is smaller and the name is under it, so the tag hangs off the
        // side where nothing else is. Unlit only — a bought trait's price is history.
        if (!isOwned)
        {
            var tag = new Rectangle(box.Right - 3, box.Center.Y - 9, 20, 18);
            _ui.Fill(b, tag, Plate * 0.92f);
            Outline(b, tag, buyable ? road : Dim, 1);
            _ui.TextCenterBig(b, $"{u.Cost}", tag.Center.X, tag.Y + 1, buyable ? Bone : Slate, NamePx);
        }
    }

    /// <summary>
    /// Where a node is, in world units — for EVERY id, including one the layout never heard of.
    /// </summary>
    /// <remarks>
    /// The layout covers the whole catalogue by construction (see <see cref="TraitTreeLayout"/>), so
    /// this is the fallback for a custom tree — a fixture, a future catalogue the layout predates. It
    /// parks the stranger along the bottom row, visibly, instead of returning early: the CHARGE spur's
    /// four keystones were invisible for as long as they existed because the old DrawNode did exactly
    /// that, and the header counted them all the while.
    /// </remarks>
    private Vector2 Pos(string id)
    {
        if (TraitTreeLayout.Positions.TryGetValue(id, out var v)) return v;
        if (_strays.TryGetValue(id, out var s)) return s;
        var (min, max) = TraitTreeLayout.Bounds();
        var stray = new Vector2(max.X + TraitTreeLayout.LaneStep * (_strays.Count + 1), max.Y);
        _strays[id] = stray;
        return stray;
    }

    private readonly Dictionary<string, Vector2> _strays = new();

    /// <summary>The part of the tree panel the diagram is fitted into.</summary>
    /// <remarks>
    /// Top inset: room for the two road-label lines above the terminals, which are part of the diagram.
    /// Bottom inset: room for the two name lines under the lowest row. Sides: half a lane, so the
    /// outermost names and the outermost road label are inside the frame.
    /// </remarks>
    private static readonly Rectangle View = new(TreePanel.X + 62, TreePanel.Y + 112,
                                                 TreePanel.Width - 124, TreePanel.Height - 112 - 84);

    /// <summary>Screen pixels per world unit — the uniform scale that fits the diagram into <see cref="View"/>.</summary>
    private static float Scale()
    {
        var (min, max) = TraitTreeLayout.Bounds();
        var span = Vector2.Max(max - min, new Vector2(0.001f));
        return MathF.Min(View.Width / span.X, View.Height / span.Y);
    }

    /// <summary>How many screen pixels one rung of the diagram is worth. Shared with <see cref="Line"/>.</summary>
    private static float RungPixels() => Scale() * TraitTreeLayout.ChainStep;

    /// <summary>How many screen pixels one lane is worth — the width a name has to fit.</summary>
    private static int LanePixels() => (int)(Scale() * TraitTreeLayout.LaneStep);

    /// <summary>World units to screen pixels, fitted to the panel once.</summary>
    private Vector2 ToScreen(Vector2 world)
    {
        var (min, max) = TraitTreeLayout.Bounds();
        // Strays (see Pos) can sit past the authored bounds; they are fitted as best they can be.
        foreach (var s in _strays.Values) max = Vector2.Max(max, s);
        var span = Vector2.Max(max - min, new Vector2(0.001f));
        // Uniform scale, so the diagram is never stretched — a squashed tree reads as a different shape
        // from the one the layout authored.
        var scale = MathF.Min(View.Width / span.X, View.Height / span.Y);
        var drawn = span * scale;
        return new Vector2(View.X + (View.Width - drawn.X) / 2f + (world.X - min.X) * scale,
                           View.Y + (View.Height - drawn.Y) / 2f + (world.Y - min.Y) * scale);
    }

    /// <summary>A wire between two nodes. Long ones are DASHED — see remarks.</summary>
    /// <remarks>
    /// A road segment joins neighbours; ATTUNEMENT wants one node from the head of every road, so its
    /// edge crosses a third of the diagram whichever prerequisite is nearest. Drawn solid, at any alpha,
    /// a line that long reads as structure — as though the two roads it cuts between were joined — and
    /// the eye follows it instead of the roads. Dashed, it reads as what it is: a dependency that lives
    /// somewhere else. The detail panel still states the full prerequisite list exactly.
    /// </remarks>
    private void Line(SpriteBatch b, Vector2 a, Vector2 c, Color col, int thick)
    {
        var dx = c.X - a.X;
        var dy = c.Y - a.Y;
        var steps = (int)MathF.Max(MathF.Abs(dx), MathF.Abs(dy));
        if (steps <= 0) return;

        // Measured against the diagram's own rung, so it follows the layout rather than a screen size:
        // anything more than twice a normal step is a wire that has left its neighbourhood.
        var longRun = steps > (int)(RungPixels() * 2.2f);
        for (var i = 0; i <= steps; i++)
        {
            if (longRun && (i / 9) % 2 == 1) continue;   // 9 on, 9 off
            _ui.Fill(b, new Rectangle((int)(a.X + dx * i / steps) - thick / 2,
                                      (int)(a.Y + dy * i / steps) - thick / 2, thick, thick), col);
        }
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// <summary>
    /// The detail panel: one trait, read properly — its road, its picture, its name, what it does in
    /// plain sentences, what it needs first, and what it costs.
    /// </summary>
    /// <remarks>
    /// The text comes from <see cref="MemoryDustText.Describe"/>, which composes the hand-written
    /// description with the keystone's own blurb and a generated line for the numbers. Before that the
    /// panel printed the description alone, and twenty of thirty-nine descriptions said "LEARN
    /// &lt;KEYSTONE&gt;." without a word about what the keystone did — on the one screen where the player
    /// decides whether to spend permanent points on it.
    /// </remarks>
    private void DrawDetail(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);
        // Hover reads, click PINS — the lesson the mastery tree's node panel learned. A player cannot
        // read a node and then look at the tree if reading requires keeping the pointer still.
        var u = (_hoverId is not null ? tree.All.FirstOrDefault(x => x.Id == _hoverId) : null)
                ?? Selected(tree);
        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var cat = CatColor(u.Effect);

        var terminal = TerminalArt(u);
        var roadCol = RoadColor(u.Road);
        var left = DetailPanel.X + 28;
        var width = DetailPanel.Width - 56;

        _ui.TextCenterBig(b, "TRAIT DETAIL", DetailPanel.Center.X, DetailPanel.Y + 18, Gold, UiTypography.SectionTitle);

        // Everything below runs off ONE CURSOR. This panel used to be a column of twelve literal offsets
        // from DetailPanel.Y, which meant adding a single row — the road, which is the whole decision the
        // screen presents — would silently have pushed the prerequisite list down through the cost
        // divider, and a terminal's taller art would have pushed it further. A cursor turns "this layout
        // has a spare row in it somewhere" into a question the code answers rather than one a capture has
        // to catch.
        var y = DetailPanel.Y + 56;

        // THE ROAD, named and drawn, with what walking it means in a few plain words.
        var badge = new Rectangle(left, y, 28, 28);
        var hasGlyph = _ui.Icon(b, RoadGlyph(u.Road), badge, roadCol);
        _ui.TextBig(b, RoadName(u.Road), hasGlyph ? badge.Right + 10 : left, y + 5, roadCol, UiTypography.Body);
        _ui.TextRightBig(b, _ui.ShortenBig(RoadBlurb(u.Road), width - 170, UiTypography.Secondary),
                         DetailPanel.Right - 28, y + 7, Slate, UiTypography.Secondary);
        y += 40;

        // A TERMINAL gets its own face, larger and untinted. This is the panel where a player decides
        // whether thirty points go here or somewhere they can then never also reach, and until now it
        // showed them the same up-arrow that a one-point convenience node shows.
        var size = terminal is not null ? 120 : 88;
        var icon = new Rectangle(DetailPanel.Center.X - size / 2, y, size, size);
        if (terminal is not null)
            _ui.Icon(b, terminal, icon, isOwned || buyable ? Color.White : new Color(0x8A, 0x82, 0x7A));
        else if (!_ui.Icon(b, CatIcon(u.Effect), icon, isOwned ? Gold : cat)
                 && _ui.Assets.Get("ui_memory_dust") is { } ic) b.Draw(ic, icon, isOwned ? Gold : cat);
        y += size + 8;

        _ui.TextCenterBig(b, u.Name, DetailPanel.Center.X, y, isOwned ? Gold : buyable ? Bone : Slate, UiTypography.PanelTitle);
        y += 34;
        // The state in a word, beside what kind of thing this is — in the player's words, not the
        // engine's. AMPLIFIER / EXPANSION / CONVENIENCE described what a node did to the code.
        var state = isOwned ? "LEARNED" : buyable ? "AVAILABLE NOW" : "LOCKED";
        var kind = terminal is not null ? "The end of a road" : MemoryDustText.EffectInPlainWords(u.Effect);
        _ui.TextCenterBig(b, $"{kind}  ·  {state}",
                          DetailPanel.Center.X, y, isOwned ? Gold : buyable ? Met : Slate, UiTypography.Secondary);
        y += 30;

        // WHAT IT DOES — the full plain-English text, at Body size, wrapped to the measured width, and
        // never allowed to run into the prerequisite list: the two blocks share the panel's middle, so
        // the text gets as many lines as leave the prerequisites their room and ends with an ellipsis
        // past that (a longer keystone blurb is the only thing that could get there).
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim * 0.7f);
        y += 14;
        _ui.TextBig(b, "WHAT IT DOES", left, y, Slate, UiTypography.Secondary);
        y += 30;
        const int lineH = 24;
        var floor = DetailPanel.Bottom - 172;
        var reserve = 62 + 34 * Math.Max(1, Math.Min(u.Requires.Count, 3));   // the prerequisite block's minimum
        var lines = _ui.WrapBig(MemoryDustText.Describe(u), width, UiTypography.Body);
        var room = Math.Max(1, (floor - reserve - y) / lineH);
        for (var i = 0; i < Math.Min(lines.Count, room); i++)
        {
            var text = i == room - 1 && lines.Count > room ? _ui.ShortenBig(lines[i] + " …", width, UiTypography.Body) : lines[i];
            _ui.TextBig(b, text, left, y, Bone, UiTypography.Body);
            y += lineH;
        }
        y += 16;

        // What it needs first (the real Requires), each with a tick once you have it.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim * 0.7f);
        y += 14;
        _ui.TextBig(b, "YOU NEED FIRST", left, y, Gold, UiTypography.Secondary);
        y += 32;
        // The cost block below is anchored to the panel's bottom, so the list has a hard floor. Say what
        // was dropped rather than drawing a row through the divider — a silently truncated list reads as
        // "these are all the prerequisites", which is the one thing it must never say.
        if (u.Requires.Count == 0) _ui.TextBig(b, "Nothing — you can start here.", left + 2, y, Slate, UiTypography.Body);
        else
            for (var i = 0; i < u.Requires.Count; i++)
            {
                if (y > floor)
                {
                    _ui.TextBig(b, $"and {u.Requires.Count - i} more — hover them on the tree", left + 2, y, Slate, UiTypography.Secondary);
                    break;
                }
                var reqId = u.Requires[i];
                var req = tree.All.FirstOrDefault(x => x.Id == reqId);
                var got = tree.Owns(reqId);
                DrawTick(b, new Rectangle(left + 2, y + 2, 20, 18), got);
                _ui.TextBig(b, req?.Name ?? reqId, left + 32, y, got ? Bone : Slate, UiTypography.Body);
                _ui.TextRightBig(b, got ? "learned" : "not yet", DetailPanel.Right - 28, y + 3, got ? Met : Slate, UiTypography.Secondary);
                y += 34;
            }

        // Cost + LEARN.
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Bottom - 156, DetailPanel.Width - 56, 2), Dim * 0.7f);
        _ui.TextBig(b, "COST", DetailPanel.X + 28, DetailPanel.Bottom - 138, Slate, UiTypography.Body);
        if (!isOwned)
            // On the COST line, not under it: at Bottom-112 this ran into the button's own top
            // ornament, and the panel has no spare row between the two.
            _ui.TextBig(b, $"you have {tree.Available}", DetailPanel.X + 96, DetailPanel.Bottom - 136,
                        tree.Available >= u.Cost ? Slate : Ember, UiTypography.Secondary);
        if (isOwned) _ui.TextRightBig(b, "LEARNED", DetailPanel.Right - 28, DetailPanel.Bottom - 140, Gold, UiTypography.PanelTitle);
        else
            // Priced in TRAIT POINTS and it says so, next to how many you have — the whole word, not "PTS".
            _ui.TextRightBig(b, $"{u.Cost} {(u.Cost == 1 ? "POINT" : "POINTS")}", DetailPanel.Right - 28,
                             DetailPanel.Bottom - 140, buyable ? Bone : Ember, UiTypography.PanelTitle);

        // _msg IS WRITTEN FOUR TIMES AND WAS DRAWN NOWHERE. Every refusal this screen has — already
        // taken, not enough points, prerequisites unlit — was assigned to a field no draw call read, so
        // clicking a node you cannot afford did nothing at all and said nothing at all. On the page that
        // produced it, above the button that produced it.
        if (_msg.Length > 0)
            _ui.TextCenterBig(b, _ui.ShortenBig(_msg, width, UiTypography.Secondary), DetailPanel.Center.X, DetailPanel.Bottom - 120, Ember, UiTypography.Secondary);

        var label = isOwned ? "LEARNED" : "LEARN THIS TRAIT";
        if (_ui.Button(b, new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 92, DetailPanel.Width - 80, 68), label, hit, clicked, enabled: buyable))
            Buy(tree, u);
        if (!isOwned && !buyable)
            // Above the COST divider, not on top of the cost row it was overlapping — and it names TRAIT
            // POINTS, because this screen has not charged Memory Dust since the tree stopped being
            // buyable by idling.
            _ui.TextCenter(b, tree.Available < u.Cost ? "NOT ENOUGH TRAIT POINTS" : "LEARN WHAT IT NEEDS FIRST",
                           DetailPanel.Center.X, DetailPanel.Bottom - 186, Ember);
    }

    private void DrawTick(SpriteBatch b, Rectangle r, bool on)
    {
        if (on) { for (var k = 0; k < 5; k++) _ui.Fill(b, new Rectangle(r.X + k, r.Bottom - 6 + k / 2 - 2, 3, 3), Met); for (var k = 0; k < 10; k++) _ui.Fill(b, new Rectangle(r.X + 5 + k, r.Bottom - 2 - k, 3, 3), Met); }
        else _ui.Fill(b, new Rectangle(r.X + 2, r.Center.Y - 2, r.Width - 4, 4), Slate);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { TreePanel, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav TRAITS  sel {_selectedId}", 434, 112, Gold, UiTypography.Secondary);
        // Which nodes the layout had to place by itself — a to-do list, drawn where a developer looks.
        if (TraitTreeLayout.Unauthored.Count > 0 || _strays.Count > 0)
            _ui.TextBig(b, $"auto-placed: {string.Join(", ", TraitTreeLayout.Unauthored.Concat(_strays.Keys))}",
                        TreePanel.X + 20, TreePanel.Bottom - 24, Ember, UiTypography.Secondary);
    }
}
