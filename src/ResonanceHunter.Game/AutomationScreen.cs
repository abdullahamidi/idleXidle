using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Evolution;
using ResonanceHunter.Core.Progression;

namespace ResonanceHunter.Client;

/// <summary>
/// The farm as a WARREN SCENE built around FOUR ROLE STATIONS — Generator, Crafter, Defender, Support.
/// </summary>
/// <remarks>
/// The composition puzzle IS the team, so the den shows the four role stations directly: each stall
/// holds that role's worker or glows "NEED". Filling all four is an optimized farm — you see it, you
/// don't read it. Idle creatures wait below in the pen; hatching rolls a varied role so you can fill the
/// stalls you're missing. Deliberately few words: labels and numbers only, everything else is the scene.
/// </remarks>
public sealed class AutomationScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color GroundShadow = new(0x10, 0x0E, 0x14);
    private static readonly Color StallBg = new(0x14, 0x11, 0x1A, 0xC8);   // translucent, so the warren shows behind
    private static readonly Color PenBg = new(0x0E, 0x0C, 0x12, 0xB4);

    private static readonly Dictionary<Role, string> RoleIcon = new()
    {
        [Role.Attacker] = "icon_role_attacker", [Role.Defender] = "icon_role_defender",
        [Role.Support] = "icon_role_support", [Role.Crafter] = "icon_role_crafter",
        [Role.Producer] = "icon_role_producer",
    };
    /// <summary>Role → key for the generic worker sprite (crea_worker_&lt;key&gt;), tinted by Source.</summary>
    private static readonly Dictionary<Role, string> RoleKey = new()
    {
        [Role.Attacker] = "attacker", [Role.Defender] = "defender", [Role.Support] = "support",
        [Role.Crafter] = "crafter", [Role.Producer] = "producer",
    };
    /// <summary>
    /// Source identity colour — the SAME hex the art is painted with.
    /// </summary>
    /// <remarks>
    /// These had DRIFTED from the art palette (code Body #C05050 vs bible #8C2E42), so a label and the
    /// creature it named were different colours. Both now come from one place: the manuscript-pigment
    /// ladder in design/art/asset-generation-epsilon.md §2.2, where every Source is separated by
    /// LUMINANCE — so the six stay distinguishable with the colour removed entirely. The old set had
    /// Machine and Ember Threat at IDENTICAL luminance.
    /// </remarks>
    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x52, 0x45, 0x7E),   // woad      L=76
        [Source.Body] = new(0xD6, 0x48, 0x5C),     // kermes    L=104
        [Source.Machine] = new(0xBC, 0x78, 0x40),  // bole      L=130
        [Source.Nature] = new(0x48, 0xB8, 0x88),   // verdigris L=157
        [Source.Mind] = new(0x74, 0xC6, 0xE8),     // lapis     L=183
        [Source.Spirit] = new(0xDC, 0xD4, 0xEC),   // bone ash  L=215
    };
    private static readonly string[] MasteryNames = ["NEWLY CONQUERED", "PARTIALLY MASTERED", "FULLY MASTERED", "OPTIMIZED"];

    private static readonly Dictionary<(Source, Role), string> CreatureArt = new()
    {
        [(Source.Nature, Role.Attacker)] = "crea_nature_atk_whelp",
        [(Source.Nature, Role.Support)] = "crea_nature_sup_mossling",
        [(Source.Nature, Role.Defender)] = "crea_nature_def_bramble",
        [(Source.Nature, Role.Producer)] = "crea_nature_prd_sporeling",
        [(Source.Machine, Role.Attacker)] = "crea_machine_atk_warden",
        [(Source.Machine, Role.Support)] = "crea_machine_sup_drone",
        [(Source.Machine, Role.Defender)] = "crea_machine_def_bulwark",
        [(Source.Shadow, Role.Attacker)] = "crea_shadow_atk_stalker",
        [(Source.Shadow, Role.Support)] = "crea_shadow_sup_wisp",
        [(Source.Shadow, Role.Defender)] = "crea_shadow_def_bulwark",

        // The rest of the 12 HERO species (design/art/asset-generation-epsilon.md §4.5). Mapped ahead
        // of the art: an unmapped cell silently falls back to a tinted generic worker, so a hero could
        // have been drawn beautifully and never once appeared.
        [(Source.Nature, Role.Crafter)] = "crea_nature_crf_sapwright",
        [(Source.Machine, Role.Crafter)] = "crea_machine_crf_cogwright",
        [(Source.Machine, Role.Producer)] = "crea_machine_prd_boiler",

        // ALL SIX SOURCES — the full 30 arrived. See ExpeditionScreen for why an unmapped cell is a
        // silent bug rather than a missing feature.
        [(Source.Body, Role.Attacker)] = "crea_body_atk_sinew",
        [(Source.Body, Role.Defender)] = "crea_body_def_bonewall",
        [(Source.Body, Role.Support)] = "crea_body_sup_pulsekin",
        [(Source.Body, Role.Crafter)] = "crea_body_crf_marrow",
        [(Source.Body, Role.Producer)] = "crea_body_prd_brood",

        [(Source.Mind, Role.Attacker)] = "crea_mind_atk_lance",
        [(Source.Mind, Role.Defender)] = "crea_mind_def_aegis",
        [(Source.Mind, Role.Support)] = "crea_mind_sup_chorus",
        [(Source.Mind, Role.Crafter)] = "crea_mind_crf_schema",
        [(Source.Mind, Role.Producer)] = "crea_mind_prd_bloom",

        [(Source.Spirit, Role.Attacker)] = "crea_spirit_atk_echofang",
        [(Source.Spirit, Role.Defender)] = "crea_spirit_def_vigil",
        [(Source.Spirit, Role.Support)] = "crea_spirit_sup_solace",
        [(Source.Spirit, Role.Crafter)] = "crea_spirit_crf_rite",
        [(Source.Spirit, Role.Producer)] = "crea_spirit_prd_emberfont",

        [(Source.Shadow, Role.Crafter)] = "crea_shadow_crf_weaver",
        [(Source.Shadow, Role.Producer)] = "crea_shadow_prd_spore",
    };

    /// <summary>The four stations of the composition chain. A Generator is an Attacker OR a Producer.</summary>
    private static readonly (string Label, Role[] Roles, Role Icon, string Sub)[] Stations =
    {
        ("GENERATOR", new[] { Role.Attacker, Role.Producer }, Role.Attacker, "PASSIVE GLEAM"),
        ("CRAFTER",   new[] { Role.Crafter },                 Role.Crafter,  "PASSIVE MATERIALS"),
        ("DEFENDER",  new[] { Role.Defender },                Role.Defender, "SOFTENS DEATH"),
        ("SUPPORT",   new[] { Role.Support },                 Role.Support,  "RAISES IDLE RATE"),
    };

    private readonly UiKit _ui;
    private readonly Random _rng = new();
    private readonly List<Creature> _roster = new();

    private string _selectedId = "";

    /// <summary>A refusal the player can read. A button that silently does nothing reads as a bug.</summary>
    private string _msg = "";
    private int _penScroll;
    private int _hatched;
    private AutomationYield _lastYield = new();
    private float _anim;
    private float _flash;
    private AutomationYield _flashYield = new();
    private KeyboardState _prevKeys;

    private const int PenVisible = 7;

    public AutomationScreen(UiKit ui) => _ui = ui;

    public int Cores { get; set; }
    public IReadOnlyList<Creature> Roster => _roster;

    public void RestoreRoster(IEnumerable<Creature> creatures) { _roster.Clear(); _roster.AddRange(creatures); _hatched = _roster.Count; }

    public void ReportOffline(AutomationYield y)
    {
        _lastYield = y;
        if (y.Kills > 0 || y.CoresProduced > 0 || y.GleamRealized > 0) { _flash = 1.7f; _flashYield = y; }
    }

    // RecordActiveProgress is DELETED. It was the sole writer of "part_break", it had ZERO callers in
    // src/ or tests/, and it was the reason the THORNSTALKER branch could never fire for any player.
    // ExpeditionRecord.Credit replaces it — in Core, where the rule belongs, and actually called.

    /// <summary>DEV ONLY: populate the warren (varied hatches + a filled chain) for screenshots.</summary>
    public void DevPopulate(Region region)
    {
        Cores = 12;
        for (var i = 0; i < 14; i++)
        {
            _hatched++;
            var c = Creature.HatchRandom($"d{_hatched}", _rng);
            // EVERY creature, whatever its Role, and its OWN Source's tree. It was Attackers only, and
            // always the Nature tree — so a Machine Support had no tree at all.
            c.BeginEvolution(EvolutionTrees.For(c.Source), new EvolutionProgress());
            _roster.Add(c);
        }
        for (var i = 0; i < Stations.Length; i++)
        {
            var pick = _roster.FirstOrDefault(c => Stations[i].Roles.Contains(c.Role) && region.Team.All(t => t.Id != c.Id));
            if (pick is not null) PutToWork(region, pick);
        }
    }

    public void AddCapturedCreature(string speciesId, Source source, int headStart)
    {
        _hatched++;
        var c = Creature.Hatch($"cap{_hatched}", source, Role.Attacker, 2);
        var p = new EvolutionProgress();
        p.FeedMaterial(headStart);
        c.BeginEvolution(EvolutionTrees.For(c.Source), p);
        _roster.Add(c);
    }

    // ── Model helpers ─────────────────────────────────────────────────────────────────────────────
    /// <summary>The inspected creature. Public so the Forge can FEED the one you are looking at.</summary>
    public Creature? Selected => _roster.FirstOrDefault(c => c.Id == _selectedId);

    private static int StationOf(Role role)
    {
        for (var i = 0; i < Stations.Length; i++) if (Stations[i].Roles.Contains(role)) return i;
        return -1;
    }

    private static Creature? Occupant(Region region, int station)
        => region.Team.Where(c => Stations[station].Roles.Contains(c.Role))
                      .OrderByDescending(c => c.IsHealthy).ThenByDescending(c => c.PowerTier).FirstOrDefault();

    /// <summary>Creatures not currently standing in a station — the pen.</summary>
    private List<Creature> PenList(Region region)
    {
        var inStalls = new HashSet<string>();
        for (var i = 0; i < Stations.Length; i++)
            if (Occupant(region, i) is { } o) inStalls.Add(o.Id);
        return _roster.Where(c => !inStalls.Contains(c.Id)).ToList();
    }

    private void PutToWork(Region region, Creature c)
    {
        var s = StationOf(c.Role);
        if (s < 0) return;
        // One worker per station: clear whoever holds this role, then seat the newcomer.
        foreach (var occ in region.Team.Where(t => Stations[s].Roles.Contains(t.Role)).ToList()) region.Unassign(occ.Id);
        region.Assign(c);
    }

    private void ToggleWork(Region region, Creature? c)
    {
        if (c is null) return;
        if (region.Team.Any(t => t.Id == c.Id)) region.Unassign(c.Id); // send to pen
        else PutToWork(region, c);
    }

    private void Hatch()
    {
        if (Cores <= 0) return;
        Cores--;
        _hatched++;
        var c = Creature.HatchRandom($"c{_hatched}", _rng);
        c.BeginEvolution(EvolutionTrees.For(c.Source), new EvolutionProgress());
        _roster.Add(c);
        _selectedId = c.Id;
    }

    private void Evolve(Region region, Creature? c)
    {
        if (c is null) return;
        var tree = EvolutionTrees.For(c.Source);
        if (c.TryEvolve(tree) is not null && region.Team.Any(t => t.Id == c.Id))
        {
            // Its role may have changed — re-seat it in the correct station.
            region.Unassign(c.Id);
            PutToWork(region, c);
        }
    }

    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    // ── Layout ──────────────────────────────────────────────────────────────────────────────────
    private static Rectangle Station(int i) => new(40 + i * 472, 136, 448, 456);
    private static Rectangle PenChip(int i) => new(48 + i * 256, 680, 232, 160);
    private static readonly Rectangle HatchBtn = new(1320, 608, 552, 56);
    private static readonly Rectangle WorkBtn = new(48, 880, 464, 56);
    private static readonly Rectangle FeedBtn = new(528, 880, 240, 56);

    /// <summary>How many materials one press of FEED spends.</summary>
    private const int FeedPortion = 10;

    /// <summary>
    /// Spend salvaged materials on a creature's evolution.
    /// </summary>
    /// <remarks>
    /// This was <c>sel.Evolution?.FeedMaterial(10)</c> — it consumed NOTHING. The button conjured
    /// evolution progress from thin air, so the only reachable evolution in the game was also free, and
    /// "unwanted loot fed to creatures" had no connection to loot whatsoever. Materials now come from
    /// dismantling, which is the whole point of dismantling.
    /// </remarks>
    private void Feed(Creature c, Hunter hunter)
    {
        if (c.Evolution is null) return;

        if (!hunter.SpendMaterials(FeedPortion))
        {
            _msg = $"NOT ENOUGH MATERIALS — DISMANTLE SOME LOOT IN THE FORGE [F].";
            return;
        }

        c.Evolution.FeedMaterial(FeedPortion);
        _msg = "";
    }
    private static readonly Rectangle EvolveBtn = new(784, 880, 280, 56);
    private static readonly Rectangle StageMinus = new(1488, 880, 64, 56);
    private static readonly Rectangle StagePlus = new(1808, 880, 64, 56);

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Update(GameTime time, KeyboardState keys, Point mouse, bool clicked, int wheel, Region region, Hunter hunter)
    {
        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _anim += dt;
        _flash = Math.Max(0f, _flash - dt);
        EnsureSelection(region);

        // Mouse wheel scrolls the pen. Without this, every worker past the 7th was unreachable.
        if (wheel != 0)
        {
            var maxScroll = Math.Max(0, PenList(region).Count - PenVisible);
            _penScroll = Math.Clamp(_penScroll - wheel, 0, maxScroll);
        }

        if (Pressed(keys, Keys.H)) Hatch();
        if (Pressed(keys, Keys.Enter)) ToggleWork(region, Selected);
        if (Pressed(keys, Keys.F) && Selected is { } fsel) Feed(fsel, hunter);
        if (Pressed(keys, Keys.V)) Evolve(region, Selected);
        if (Pressed(keys, Keys.Left)) CycleSelection(region, -1);
        if (Pressed(keys, Keys.Right)) CycleSelection(region, +1);
        if (Pressed(keys, Keys.OemPlus) || Pressed(keys, Keys.Add)) region.AutomationStage = Math.Min(3, region.AutomationStage + 1);
        if (Pressed(keys, Keys.OemMinus) || Pressed(keys, Keys.Subtract)) region.AutomationStage = Math.Max(1, region.AutomationStage - 1);

        if (clicked)
        {
            // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
            if (UiKit.ClickedIn(HatchBtn, hit, true)) Hatch();

            for (var i = 0; i < Stations.Length; i++)
                if (Station(i).Contains(hit) && Occupant(region, i) is { } occ) _selectedId = occ.Id;

            var pen = PenList(region);
            for (var i = 0; i < PenVisible && _penScroll + i < pen.Count; i++)
                if (PenChip(i).Contains(hit)) _selectedId = pen[_penScroll + i].Id;

            var sel = Selected;
            if (sel is not null)
            {
                if (UiKit.ClickedIn(WorkBtn, hit, true)) ToggleWork(region, sel);
                if (UiKit.ClickedIn(FeedBtn, hit, true)) Feed(sel, hunter);
                if (UiKit.ClickedIn(EvolveBtn, hit, true)) Evolve(region, sel);
            }
            if (UiKit.ClickedIn(StageMinus, hit, true)) region.AutomationStage = Math.Max(1, region.AutomationStage - 1);
            if (UiKit.ClickedIn(StagePlus, hit, true)) region.AutomationStage = Math.Min(3, region.AutomationStage + 1);
        }

        _prevKeys = keys;
    }

    private void EnsureSelection(Region region)
    {
        if (Selected is not null) return;
        _selectedId = (PenList(region).FirstOrDefault() ?? _roster.FirstOrDefault())?.Id ?? "";
    }

    private void CycleSelection(Region region, int d)
    {
        if (_roster.Count == 0) return;
        var order = new List<Creature>();
        for (var i = 0; i < Stations.Length; i++) if (Occupant(region, i) is { } o) order.Add(o);
        var pen = PenList(region);
        order.AddRange(pen);

        var idx = order.FindIndex(c => c.Id == _selectedId);
        if (idx < 0) idx = 0;
        var next = order[(idx + d + order.Count) % order.Count];
        _selectedId = next.Id;

        var pi = pen.FindIndex(c => c.Id == _selectedId);
        if (pi >= 0) { if (pi < _penScroll) _penScroll = pi; if (pi >= _penScroll + PenVisible) _penScroll = pi - PenVisible + 1; }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Region region, Hunter hunter) => Draw(b, region, hunter, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, Region region, Hunter hunter, Point mouse, bool clicked)
    {
        EnsureSelection(region);
        var pen = PenList(region);
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        // ═══ HEADER + STATUS ═══════════════════════════════════════════════════════════════════════
        _ui.Title(b, "THE WARREN", MasteryNames[(int)region.MasteryLevel]);
        // Gleam/Dust/Materials are the shared currency pills top-right (Game1.DrawCurrencyPills).

        // Region-mastery progress — a full labelled bar (uiref_warren), not the old thin sliver.
        _ui.Text(b, "MASTERY", 32, 80, Slate);
        _ui.Bar(b, 264, 84, 1280, 24, region.ProgressToNextLevel() ?? 1f, Gold);
        _ui.TextRight(b, $"IDLE {region.IdleEfficiencyPercent():0}%", 1872, 80, Slate);

        // Production toast — the payoff, made visible as it happens (rises and fades).
        if (_flash > 0f)
        {
            var parts = new List<string>();
            if (_flashYield.Kills > 0) parts.Add($"{_flashYield.Kills} KILL");
            if (_flashYield.CoresProduced > 0) parts.Add($"+{_flashYield.CoresProduced} CORE");
            if (_flashYield.GleamRealized > 0) parts.Add($"+{_flashYield.GleamRealized}g");
            // Show what the auto-sell cap swallowed, so vanishing gleam reads as a CAP, not a bug — the exact
            // thing AutomationYield.GleamForfeitedToCap was created to surface, and until now it never was.
            if (_flashYield.GleamForfeitedToCap > 0) parts.Add($"({_flashYield.GleamForfeitedToCap}g CAPPED)");
            if (parts.Count > 0)
                _ui.TextCenter(b, string.Join("   ", parts), 960, (int)(600 - (1f - _flash / 1.7f) * 48), _flash > 0.5f ? Gold : Slate);
        }

        // ═══ THE DEN — four role stations ══════════════════════════════════════════════════════════
        var filled = 0;
        for (var i = 0; i < Stations.Length; i++)
        {
            var st = Station(i);
            var occ = Occupant(region, i);
            var isFilled = occ is not null && occ.IsHealthy;
            if (isFilled) filled++;

            _ui.Fill(b, st, StallBg);
            _ui.Fill(b, new Rectangle(st.X, st.Y, st.Width, 4), isFilled ? Gold : Slate);
            _ui.Fill(b, new Rectangle(st.X, st.Bottom - 4, st.Width, 4), isFilled ? Gold : Dim);

            // Role header — a gold diamond bullet + role name (uiref_warren), and a one-line purpose beneath.
            _ui.Diamond(b, new Rectangle(st.X + 28, st.Y + 24, 32, 32), isFilled ? Gold : Slate);
            _ui.Text(b, Stations[i].Label, st.X + 76, st.Y + 20, isFilled ? Gold : Slate);
            _ui.Text(b, Stations[i].Sub, st.X + 32, st.Y + 60, Dim);

            var spriteBox = new Rectangle(st.X + 64, st.Y + 104, st.Width - 128, 240);
            if (occ is not null)
            {
                DrawCreature(b, spriteBox, occ, working: true);
                _ui.Text(b, $"{occ.Source.ToString().ToUpperInvariant()}  T{occ.PowerTier}", st.X + 32, st.Bottom - 48, isFilled ? Bone : Ember);
                if (occ.Id == _selectedId) Reticle(b, new Rectangle(st.X + 4, st.Y + 4, st.Width - 8, st.Height - 8), Bone);
            }
            else
            {
                // Empty stall: a big dim role glyph and a clear call to action.
                _ui.Icon(b, RoleIcon[Stations[i].Icon], new Rectangle(st.Center.X - 48, st.Y + 136, 96, 96), Dim);
                _ui.TextCenter(b, "NEED", st.Center.X, st.Bottom - 48, Ember);
            }
        }

        // A refusal takes this line over — it is transient and the chain status is not urgent.
        if (_msg.Length > 0) _ui.TextCenter(b, _msg, 960, 608, Ember);
        else _ui.TextCenter(b, filled == 4 ? "CHAIN COMPLETE — FARM OPTIMIZED" : $"{filled} / 4 ROLES WORKING",
            960, 608, filled == 4 ? Gold : Slate);

        // ═══ THE PEN — idle creatures + the hatchery ═══════════════════════════════════════════════
        _ui.Fill(b, new Rectangle(24, 664, 1872, 184), PenBg);
        var idleLabel = pen.Count > PenVisible
            ? $"IDLE ({pen.Count})   SHOWING {_penScroll + 1}-{Math.Min(_penScroll + PenVisible, pen.Count)}  ( < > )"
            : $"IDLE ({pen.Count})";
        _ui.Text(b, idleLabel, 48, 640, Slate);
        _ui.Button(b, HatchBtn, $"HATCH A CORE  ({Cores})", hit, clicked, enabled: Cores > 0); // acted on in Update

        if (_roster.Count == 0)
            _ui.Text(b, "EVERY KILL DROPS A CORE — HATCH ONE, THEN FILL THE FOUR STATIONS ABOVE.", 48, 744, Dim);

        for (var i = 0; i < PenVisible && _penScroll + i < pen.Count; i++)
        {
            var c = pen[_penScroll + i];
            // A worker whose role station is empty is exactly what the farm needs — flag it.
            var needed = StationOf(c.Role) is var s && s >= 0 && Occupant(region, s) is null;
            DrawCreature(b, PenChip(i), c, working: false, selected: c.Id == _selectedId, tag: true, needed: needed);
        }

        // ═══ ACTION BAR ════════════════════════════════════════════════════════════════════════════
        var sel = Selected;
        if (sel is not null)
        {
            var assigned = region.Team.Any(t => t.Id == sel.Id);
            // The creature's OWN tree. Against the Nature tree, a Machine creature's node ids (machine_*)
            // would fail HasNode and the panel would report it unevolvable forever.
            var tree = EvolutionTrees.For(sel.Source);
            var evolvable = sel.Evolution is not null && sel.EvolutionNodeId.Length > 0 && tree.HasNode(sel.EvolutionNodeId);

            // An evolvable creature's GOAL gets the whole line — the one actionable thing here — since its
            // identity (element tint, role icon, tier) already rides its own chip. Prefixing the line with
            // that identity pushed the long hint off the right edge. A non-evolvable one just gets named.
            var next = evolvable ? tree.Options(sel.EvolutionNodeId, sel.Evolution!).FirstOrDefault() : default;
            var detail = next.Edge is not null
                ? next.Edge.Hint
                : $"{sel.Source.ToString().ToUpperInvariant()} {sel.Role.ToString().ToUpperInvariant()}  ·  T{sel.PowerTier}";
            _ui.Text(b, detail, 48, 852, Bone);

            _ui.Button(b, WorkBtn, assigned ? "SEND TO PEN" : "PUT TO WORK", hit, clicked);
            if (evolvable)
            {
                _ui.Button(b, FeedBtn, "FEED +10", hit, clicked);
                _ui.Button(b, EvolveBtn, "EVOLVE", hit, clicked,
                    enabled: tree.EligibleBranch(sel.EvolutionNodeId, sel.Evolution!) is not null);
            }
        }

        // Automation stage — compact, on the right.
        var stageText = region.AutomationStage switch { 1 => "KILL", 2 => "KILL+KEEP", _ => "KILL+SELL" };
        _ui.Text(b, $"STAGE {region.AutomationStage}/3", 1200, 892, Slate);
        _ui.Button(b, StageMinus, "-", hit, clicked);
        _ui.TextCenter(b, stageText, 1680, 892, Slate);
        _ui.Button(b, StagePlus, "+", hit, clicked);
    }

    /// <summary>Draw a creature as a sprite (torso art, else a source-coloured critter + role glyph).</summary>
    private void DrawCreature(SpriteBatch b, Rectangle box, Creature c, bool working, bool selected = false, bool tag = false, bool needed = false)
    {
        var bob = working ? (int)(MathF.Sin(_anim * 2.1f + box.X) * 8f) : 0;

        if (selected) { _ui.Fill(b, box, StallBg); Reticle(b, box, Bone); }
        else if (needed)
        {
            // A gently breathing gold edge: "this worker fills an empty station."
            var a = 0.35f + 0.35f * MathF.Sin(_anim * 4f);
            _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 4), Gold * a);
            _ui.Fill(b, new Rectangle(box.X, box.Bottom - 4, box.Width, 4), Gold * a);
        }

        // Ground shadow so it stands, not floats.
        _ui.Fill(b, new Rectangle(box.Center.X - box.Width / 4, box.Bottom - (tag ? 48 : 24), box.Width / 2, 16), GroundShadow);

        var draw = new Rectangle(box.X, box.Y + bob, box.Width, box.Height - (tag ? 48 : 32));

        // Sprite pick, best-to-worst: a species-specific torso, else a role-generic worker sprite
        // tinted by the creature's Source colour, else a flat coloured critter. The middle path lets
        // FIVE worker sprites (one per role) art the whole warren — see asset spec.
        var srcCol = SourceColor.GetValueOrDefault(c.Source, Slate);
        // Art pack v1 is one creature per SOURCE (design/art/asset-integration-spec.md §4). Per-role creatures
        // are a listed gap; until they arrive, an unmatched source draws the flat critter below — never a
        // borrowed sprite.
        var torso = _ui.Assets.Get($"crea_{c.Source.ToString().ToLowerInvariant()}");
        if (torso is not null)
        {
            var tint = Color.White;   // real per-source art draws full-colour (only the old flat body was tinted)
            var scale = MathF.Min(draw.Width / (float)torso.Width, draw.Height / (float)torso.Height);
            var w = Math.Max(1, (int)(torso.Width * scale));
            var h = Math.Max(1, (int)(torso.Height * scale));
            b.Draw(torso, new Rectangle(draw.Center.X - w / 2, draw.Bottom - h, w, h), tint);
        }
        else
        {
            var body = new Rectangle(draw.Center.X - 52, draw.Bottom - 96, 104, 88);
            _ui.Fill(b, new Rectangle(body.X + 12, body.Y, body.Width - 24, body.Height), srcCol);
            _ui.Fill(b, new Rectangle(body.X, body.Y + 16, body.Width, body.Height - 32), srcCol);
            _ui.Icon(b, RoleIcon[c.Role], new Rectangle(body.Center.X - 28, body.Center.Y - 28, 56, 56), Bone);
        }

        // Small tier tag for pen chips (stations print their own detail line).
        if (tag)
        {
            _ui.Icon(b, RoleIcon[c.Role], new Rectangle(box.X + 8, box.Bottom - 40, 36, 36));
            _ui.Text(b, $"T{c.PowerTier}", box.X + 52, box.Bottom - 36, Slate);
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
