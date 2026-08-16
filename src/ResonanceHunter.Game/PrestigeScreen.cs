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
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
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
    private static readonly Rectangle TreePanel = new(38, 144, 1246, 718);
    private static readonly Rectangle DetailPanel = new(1320, 144, 560, 718);

    /// <summary>Node radius in pixels. A terminal draws at 1.5x this.</summary>
    private const int NodeR = 26;

    private static string CatName(UnlockEffect e) => e switch
    {
        UnlockEffect.Amplifier => "AMPLIFIER", UnlockEffect.Expansion => "EXPANSION", _ => "CONVENIENCE",
    };

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

    private static string RoadBlurb(TraitRoad r) => r switch
    {
        TraitRoad.Spine => "CAPACITY — SOCKETS, WEAVES, VOWS",
        TraitRoad.Ruin => "POWER BOUGHT WITH SAFETY",
        TraitRoad.Aegis => "THE WALL",
        TraitRoad.Avarice => "THE ECONOMY BUILD",
        _ => "BEHAVIOUR, NOT NUMBERS",
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
        if (tree.Owns(u.Id)) _msg = "ALREADY TAKEN — TRAITS ARE PERMANENT.";
        else if (tree.Purchase(u.Id)) { _msg = $"TAKEN: {u.Name}."; BeginLit(u); }
        else if (tree.Available < u.Cost) _msg = "NOT ENOUGH TRAIT POINTS — CONQUER, CORRUPT, MASTER.";
        else _msg = "LOCKED — LIGHT ITS PREREQUISITES FIRST.";
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

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xC0));
        // TRAITS, not DUST. The screen stopped spending Memory Dust when the tree stopped being buyable
        // by idling; a title naming a currency it does not charge is the kind of small lie that makes a
        // player mistrust every other number on the screen. Dust is still real — it is the Warren's
        // material, and it still sits in the top bar.
        _ui.TextCenterBig(b, "TRAITS", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "THE SPINE  ·  FOUR ROADS  ·  NO RESPEC", 960, 80, Slate, UiTypography.Secondary);

        // The two numbers the deleted overview panel existed to carry. They belong in a header rather
        // than a panel: a player checks "can I afford this" constantly and "what does the whole tree
        // cost" once, and neither is worth a third of the page.
        var spent = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        _ui.TextBig(b, "TRAIT POINTS", 60, 108, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{tree.Available}", 210, 104, tree.Available > 0 ? Gold : Slate, UiTypography.PanelTitle);
        _ui.TextRightBig(b, $"{spent} SPENT  ·  {tree.TotalTreeCost} FOR EVERYTHING",
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
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080),
                 new Color(0x06, 0x04, 0x0A) * (hold * (_litTerminal ? 0.66f : 0.55f)));

        // 2. THE FLASH. Short and bright: it is the frame in which the decision landed.
        if (p < 0.14f)
        {
            var f = 1f - p / 0.14f;
            _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), accent * (f * f * 0.55f));
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

        _ui.TextCenterBig(b, _litTerminal ? "A ROAD ENDS HERE" : "TRAIT LIT — AND IT IS PERMANENT",
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
    /// </remarks>
    private void DrawTree(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.Panel(b, TreePanel);
        var owned = tree.All.Count(u => tree.Owns(u.Id));
        // No panel title. The screen is already called TRAITS, and a second centred heading sat exactly
        // where the four road labels want to be — which is the top of the diagram, over the terminals.
        _ui.TextRightBig(b, $"{owned} / {tree.All.Count} LIT", TreePanel.Right - 56, TreePanel.Y + 30,
                         Slate, UiTypography.Secondary);

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
            if (u.Requires.Count == 0 || !TraitTreeLayout.Positions.TryGetValue(u.Id, out var to)) continue;

            string? nearest = null;
            var best = float.MaxValue;
            foreach (var reqId in u.Requires)
            {
                if (!TraitTreeLayout.Positions.TryGetValue(reqId, out var f)) continue;
                var d = Vector2.DistanceSquared(f, to) - (tree.Owns(reqId) ? 10_000f : 0f);
                if (d >= best) continue;
                best = d;
                nearest = reqId;
            }
            if (nearest is null) continue;

            var lit = tree.Owns(u.Id) && tree.Owns(nearest);
            Line(b, ToScreen(TraitTreeLayout.Positions[nearest]), ToScreen(to),
                 lit ? Gold : RoadColor(u.Road) * 0.30f, lit ? 4 : 3);
        }

        // The four road labels, above their terminals — the diagram's only text, and the one line that
        // names what the player is choosing between.
        var heads = new[]
            {
                (TraitRoad.Ruin, "ks_reaper"), (TraitRoad.Aegis, "ks_titan"),
                (TraitRoad.Artifice, "ks_weaver"), (TraitRoad.Avarice, "ks_hoarder"),
            }
            .Where(h => TraitTreeLayout.Positions.ContainsKey(h.Item2))
            .Select(h => (h.Item1, At: ToScreen(TraitTreeLayout.Positions[h.Item2])))
            .OrderBy(h => h.At.X)
            .ToList();

        // THE COLUMN, MEASURED. These four summaries are centred on their terminals at fixed positions,
        // and each was drawn at whatever width its text happened to be — so ARTIFICE's "30 PTS" ran
        // straight into AVARICE's "0/4" and the row read "…30 PTS0/4 · 30 PTS". Deriving the width from
        // the actual gap between neighbouring roads means the label shortens instead of colliding, and
        // it keeps holding when a road's cost grows a digit. A road's whole price beside the points a
        // career earns IS the decision this screen exists to present, so it has to stay readable.
        var column = heads.Count < 2
            ? 400
            : (int)Enumerable.Range(1, heads.Count - 1)
                             .Min(i => heads[i].At.X - heads[i - 1].At.X) - 24;

        foreach (var (road, p) in heads)
        {
            var nodes = tree.All.Where(u => u.Road == road).ToList();
            var lit = nodes.Count(u => tree.Owns(u.Id));
            var top = (int)p.Y - (int)(NodeR * 1.5f);
            _ui.TextCenterBig(b, _ui.ShortenBig(RoadName(road), column, UiTypography.Body),
                              (int)p.X, top - 56,
                              lit > 0 ? RoadColor(road) : RoadColor(road) * 0.75f, UiTypography.Body);
            _ui.TextCenterBig(b,
                _ui.ShortenBig($"{lit}/{nodes.Count}  ·  {nodes.Sum(u => u.Cost)} POINTS", column,
                               UiTypography.Secondary),
                (int)p.X, top - 26, lit > 0 ? Gold : Slate, UiTypography.Secondary);
        }

        foreach (var u in tree.All) DrawNode(b, tree, u, hit, clicked);
    }

    /// <summary>One node: a socket whose SHAPE is its road and whose brightness is its state.</summary>
    private void DrawNode(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u, Point hit, bool clicked)
    {
        if (!TraitTreeLayout.Positions.TryGetValue(u.Id, out var w)) return;
        var p = ToScreen(w);

        var terminal = TerminalArt(u);
        // A terminal is drawn half again as large. It costs twelve where its neighbours cost four, and
        // size is the only channel that says "this one is the end" without a word.
        var r = terminal is not null ? (int)(NodeR * 1.5f) : NodeR;
        var box = new Rectangle((int)p.X - r, (int)p.Y - r, r * 2, r * 2);
        var hover = box.Contains(hit);

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var sel = u.Id == _selectedId;
        var road = RoadColor(u.Road);

        if (clicked && hover) { _selectedId = u.Id; _msg = ""; }
        if (hover) _hoverId = u.Id;

        _ui.Fill(b, box, isOwned ? road * 0.85f : buyable ? road * 0.30f : new Color(0x14, 0x11, 0x1E, 0xE8));
        Outline(b, box, sel ? Bone : isOwned ? Gold : buyable ? road : Dim, sel || isOwned ? 3 : 2);

        if (terminal is not null)
            _ui.Icon(b, terminal, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8),
                     isOwned || buyable ? Color.White : new Color(0x6A, 0x64, 0x60));
        else if (!_ui.Icon(b, CatIcon(u.Effect),
                           new Rectangle(box.X + 6, box.Y + 6, box.Width - 12, box.Height - 12),
                           isOwned ? new Color(0x14, 0x11, 0x1E) : buyable ? road : new Color(0x4A, 0x46, 0x58))
                 && _ui.Assets.Get("ui_memory_dust") is { } ic)
            b.Draw(ic, new Rectangle(box.X + 6, box.Y + 6, box.Width - 12, box.Height - 12), road);

        // The cost, under the node. Small, because it is the answer to a question the player asks
        // second — the first is "what road is this on", which the position already answered.
        // DRAWN INSIDE THE NODE, not under it. At the unsized 32px face this needed ~24px of clearance
        // below the box, and one rung of the tree is only ~16px taller than a node — so every cost digit
        // in the diagram was printed on top of the node beneath it. Inside its own node it cannot
        // collide with anything at any zoom.
        if (!isOwned)
            _ui.TextCenterBig(b, $"{u.Cost}", box.Center.X, box.Bottom - 20,
                              buyable ? Bone : Slate, UiTypography.Secondary);
    }

    /// <summary>How many screen pixels one rung of the diagram is worth. Shared with <see cref="Line"/>.</summary>
    private float RungPixels()
    {
        var (min, max) = TraitTreeLayout.Bounds();
        var view = new Rectangle(TreePanel.X + 84, TreePanel.Y + 168,
                                 TreePanel.Width - 168, TreePanel.Height - 244);
        var span = Vector2.Max(max - min, new Vector2(0.001f));
        return MathF.Min(view.Width / span.X, view.Height / span.Y) * TraitTreeLayout.ChainStep;
    }

    /// <summary>World units to screen pixels, fitted to the panel once.</summary>
    private Vector2 ToScreen(Vector2 world)
    {
        var (min, max) = TraitTreeLayout.Bounds();
        // The top inset reserves room for the four ROAD LABELS, which are drawn above the terminals and
        // are part of the diagram rather than decoration on it — at +108 both lines of RUIN and AEGIS
        // were sliced by the panel's own frame.
        var view = new Rectangle(TreePanel.X + 84, TreePanel.Y + 168,
                                 TreePanel.Width - 168, TreePanel.Height - 244);
        var span = Vector2.Max(max - min, new Vector2(0.001f));
        // Uniform scale, so the diagram is never stretched — a squashed tree reads as a different shape
        // from the one the layout authored.
        var scale = MathF.Min(view.Width / span.X, view.Height / span.Y);
        var drawn = span * scale;
        return new Vector2(view.X + (view.Width - drawn.X) / 2f + (world.X - min.X) * scale,
                           view.Y + (view.Height - drawn.Y) / 2f + (world.Y - min.Y) * scale);
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

        // THE ROAD, named and drawn. The detail panel — the one place a trait is read properly — did not
        // mention which of the five roads it belonged to at all.
        var badge = new Rectangle(left, y, 28, 28);
        var hasGlyph = _ui.Icon(b, RoadGlyph(u.Road), badge, roadCol);
        _ui.TextBig(b, RoadName(u.Road), hasGlyph ? badge.Right + 10 : left, y + 5, roadCol, UiTypography.Body);
        _ui.TextRightBig(b, RoadBlurb(u.Road), DetailPanel.Right - 28, y + 7, Slate, UiTypography.Secondary);
        y += 40;

        // A TERMINAL gets its own face, larger and untinted. This is the panel where a player decides
        // whether thirty points go here or somewhere they can then never also reach, and until now it
        // showed them the same up-arrow that a one-point convenience node shows.
        var size = terminal is not null ? 132 : 100;
        var icon = new Rectangle(DetailPanel.Center.X - size / 2, y, size, size);
        if (terminal is not null)
            _ui.Icon(b, terminal, icon, isOwned || buyable ? Color.White : new Color(0x8A, 0x82, 0x7A));
        else if (!_ui.Icon(b, CatIcon(u.Effect), icon, isOwned ? Gold : cat)
                 && _ui.Assets.Get("ui_memory_dust") is { } ic) b.Draw(ic, icon, isOwned ? Gold : cat);
        y += size + 10;

        _ui.TextCenterBig(b, u.Name, DetailPanel.Center.X, y, isOwned ? Gold : buyable ? Bone : Slate, UiTypography.PanelTitle);
        y += 34;
        var state = isOwned ? "LIT" : buyable ? "AVAILABLE" : "LOCKED";
        _ui.TextCenterBig(b, terminal is not null ? $"TERMINAL — THE END OF A ROAD  ·  {state}"
                                                  : $"{CatName(u.Effect)}  ·  {state}",
                          DetailPanel.Center.X, y, isOwned ? Gold : buyable ? Met : Slate, UiTypography.Secondary);
        y += 30;

        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, "EFFECT", left, y, Slate, UiTypography.Secondary);
        y += 30;
        y = DrawWrapped(b, Cap(u.Description), left, y, width, Bone) + 30;
        if (u.GrantsKeystone is not null)
        {
            _ui.TextBig(b, "GRANTS A KEYSTONE — A BUILD-DEFINING CHOICE.", left, y, Violet, UiTypography.Secondary);
            y += 30;
        }

        // Prerequisites (real Requires), each with an owned check.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim);
        y += 14;
        _ui.TextBig(b, "PREREQUISITES", left, y, Gold, UiTypography.Secondary);
        y += 32;
        // The cost block below is anchored to the panel's bottom, so the list has a hard floor. Say what
        // was dropped rather than drawing a row through the divider — a silently truncated list reads as
        // "these are all the prerequisites", which is the one thing it must never say.
        var floor = DetailPanel.Bottom - 172;
        if (u.Requires.Count == 0) _ui.TextBig(b, "NONE — START HERE.", left + 2, y, Slate, UiTypography.Body);
        else
            for (var i = 0; i < u.Requires.Count; i++)
            {
                if (y > floor)
                {
                    _ui.TextBig(b, $"+{u.Requires.Count - i} MORE", left + 2, y, Slate, UiTypography.Secondary);
                    break;
                }
                var reqId = u.Requires[i];
                var req = tree.All.FirstOrDefault(x => x.Id == reqId);
                var got = tree.Owns(reqId);
                DrawTick(b, new Rectangle(left + 2, y + 2, 20, 18), got);
                _ui.TextBig(b, req?.Name ?? reqId, left + 32, y, got ? Bone : Slate, UiTypography.Body);
                y += 34;
            }

        // Cost + UPGRADE.
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Bottom - 156, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "COST", DetailPanel.X + 28, DetailPanel.Bottom - 138, Slate, UiTypography.Body);
        if (!isOwned)
            // On the COST line, not under it: at Bottom-112 this ran into the UPGRADE button's own top
            // ornament, and the panel has no spare row between the two.
            _ui.TextBig(b, $"YOU HAVE {tree.Available}", DetailPanel.X + 96, DetailPanel.Bottom - 136,
                        tree.Available >= u.Cost ? Dim : Ember, UiTypography.Secondary);
        if (isOwned) _ui.TextRightBig(b, "OWNED", DetailPanel.Right - 28, DetailPanel.Bottom - 140, Gold, UiTypography.PanelTitle);
        else
        {
            // Priced in TRAIT POINTS and it says so, next to how many you have. The Dust mote that used
            // to sit here named the wrong currency entirely — see the card's cost line.
            _ui.TextRightBig(b, $"{u.Cost:N0} PT{(u.Cost == 1 ? "" : "S")}", DetailPanel.Right - 28,
                             DetailPanel.Bottom - 140, buyable ? Bone : Ember, UiTypography.PanelTitle);
        }

        var label = isOwned ? "LIT" : "UPGRADE";
        if (_ui.Button(b, new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 92, DetailPanel.Width - 80, 68), label, hit, clicked, enabled: buyable))
            Buy(tree, u);
        if (!isOwned && !buyable)
            // Above the COST divider, not on top of the cost row it was overlapping — and it names TRAIT
            // POINTS, because this screen has not charged Memory Dust since the tree stopped being
            // buyable by idling.
            _ui.TextCenter(b, tree.Available < u.Cost ? "NOT ENOUGH TRAIT POINTS" : "WALK ITS PREREQUISITES FIRST",
                           DetailPanel.Center.X, DetailPanel.Bottom - 186, Ember);
    }

    private static string Cap(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant() : s;

    /// <summary>Word-wrap into a width. Returns the y of the LAST line, so a cursor can carry on from it.</summary>
    private int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 26; line = w; }
            else line = probe;
        }
        if (line.Length > 0) _ui.Text(b, line, x, y, c);
        return y;
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
    }
}
