using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The STATS screen (nav: STATS): a read-only three-column overview — a hunter summary card, the primary
/// attributes + derived combat stats, and a secondary-attributes + career-progression column.
/// </summary>
/// <remarks>
/// Built to the Stats production spec (rev 1). Every value is REAL: the spec's fixture named Strength/
/// Dexterity/Intelligence, seven elemental resistances, and career counters (monsters killed, deaths, play
/// time) the model does not have. Per the UX standard's data-honesty rules those are replaced with the game's
/// real nine commander stats, a real secondary-attributes panel in place of resistances (no resistance model
/// exists), and only the career counters the game actually tracks (highest wave, chests opened, mastery).
/// The spec called this screen read-only, which left stat TRAINING (hunter.Train) with no home anywhere in
/// the game: nine stats with geometric costs and a rank cap, fully modelled, and no button that bought one.
/// Gleam is one of the three payouts a descent makes, and this is the layer it buys — so the rows train here.
/// </remarks>
public sealed class StatsScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Sky = new(0x7A, 0x9A, 0xC0);
    private static readonly Color Quiet = new(0x16, 0x12, 0x20, 0xE0);
    private static readonly Color RowBg = new(0x1A, 0x16, 0x24, 0xC0);

    private readonly UiKit _ui;

    // Set by the host each frame (like the other screens). Mastery gives the adept title; the career values
    // come from the game loop (they live in Game1, not on the Hunter).
    public MasteryTree Mastery { get; set; } = null!;
    public PlayerLoadout Loadout { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;

    /// <summary>
    /// The active character, because this page states the player's numbers and a character is one of
    /// the three things that changes them.
    /// </summary>
    /// <remarks>
    /// Passing this to <c>ToBuild</c> is not optional bookkeeping. The two-argument overload silently
    /// substitutes <c>null</c>, and a shape built without the character is a shape the sim never uses —
    /// which is the exact bug <see cref="DrawDerived"/> already carries a comment about having fixed
    /// once for the mastery tree. The character layer arrived later and re-opened it.
    /// </remarks>
    public Character? Character { get; set; }
    public int HighestWave { get; set; }
    public int ChestsOpened { get; set; }
    public int MasteryPoints { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevStatsDebug { get; set; }

    public StatsScreen(UiKit ui) => _ui = ui;

    /// <summary>The house abbreviation, so the subtitle agrees with the currency pill above it.</summary>
    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    // ── Spec §4 layout: hunter card + two stacked columns, all clearing the shared nav rail. ──
    // PANELS SIZED TO WHAT THEY DRAW. The hunter card was 702 tall for ~430 of content, so a third of
    // the tallest panel on the page was framed emptiness — which reads as "failed to load", never as
    // headroom. Deleting DERIVED STATS left the same hole in the centre, so PROGRESSION moves under
    // PRIMARY and the right column keeps COMBAT alone. Three columns that each end where their content
    // does, instead of three columns padded to the same arbitrary floor.
    private static readonly Rectangle HunterCard = new(56, 150, 372, 480);
    private static readonly Rectangle PrimaryPanel = new(500, 150, 590, 356);
    private static readonly Rectangle CombatPanel = new(1144, 150, 646, 356);      // real secondary attributes (no resistances)
    private static readonly Rectangle ProgressPanel = new(500, 546, 590, 300);

    // The read-only screen only needs the frame's readout; training moved off this screen (spec §1).
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter) { }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The stat a TRAIN button was pressed for this frame, taken once.</summary>
    /// <remarks>
    /// Request/consume, like WarrenScreen: the screen never mutates the Hunter, so the host stays the
    /// only place a currency is spent and the screen stays drawable in a capture with no game state.
    /// </remarks>
    private HunterStat? _trainRequest;

    public HunterStat? ConsumeTrain()
    {
        var r = _trainRequest;
        _trainRequest = null;
        return r;
    }

    public void Draw(SpriteBatch b, Point mouse, Hunter hunter, bool clicked = false)
    {
        // INVERT THE OVERLAY INSET BEFORE ANY HIT-TEST. Every rect on this screen is authored in
        // 1920x1080 and drawn through Game1.BeginOverlayCanvas; the cursor arrives in 480x270 canvas
        // space. Without this conversion the two spaces never meet.
        //
        // THIS IS WHY THE TRAIN BUTTONS COULD NOT BE CLICKED. Playtest: "Stat upgrade tıklayamıyorum."
        // The nearest TRAIN button sits at authored x=938 while the incoming cursor's x cannot exceed
        // ~479, so Rectangle.Contains was false at EVERY cursor position, on every frame, for every
        // button — the stat economy was unreachable, not merely awkward.
        //
        // Nine other screens already do exactly this (ForgeScreen.cs:863 is the twin). StatsScreen was
        // the only one that did not, which is the whole bug: a shared transform that each screen has to
        // remember to invert is a rule enforced by nobody. check_mouse_space.py now enforces it.
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xD8));   // scrim over the shared backdrop

        _ui.TextCenterBig(b, "STATS", 960, 24, Gold, UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        // THE SUBTITLE SAYS WHAT THIS SCREEN IS FOR, not what its panels are called.
        //
        // It used to recite the headers directly beneath it, which spends the largest 100px on the page
        // to tell the player something they can already read, and teaches them that big text in this
        // band is not worth reading. Same slot, same cost, real content.
        _ui.TextCenterBig(b, $"SPEND GLEAM TO RAISE A STAT — YOU HAVE {Ab(hunter.Gleam)}", 960, 80, Slate, UiTypography.Secondary);

        DrawHunterCard(b, hunter);
        DrawPrimary(b, hunter, hit, clicked);
        DrawCombat(b, hunter, hit, clicked);
        DrawProgression(b, hunter);
        if (DevStatsDebug) DrawDebug(b);
    }

    private void DrawHunterCard(SpriteBatch b, Hunter hunter)
    {
        _ui.Panel(b, HunterCard);
        _ui.TextCenterBig(b, "HUNTER", HunterCard.Center.X, HunterCard.Y + 22, Gold, UiTypography.SectionTitle);

        var por = new Rectangle(HunterCard.X + 40, HunterCard.Y + 66, 118, 118);
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);

        var tx = por.Right + 20;
        var adept = Mastery?.Affinity() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.TextBig(b, adept, tx, HunterCard.Y + 74, Bone, UiTypography.PanelTitle);
        _ui.TextBig(b, $"LEVEL {hunter.HunterLevel}", tx, HunterCard.Y + 106, Gold, UiTypography.Body);
        var hpBar = new Rectangle(tx, HunterCard.Y + 138, HunterCard.Right - tx - 24, 26);
        _ui.BarArt(b, hpBar, 1f, "health");
        _ui.TextCenterBig(b, $"{hunter.MaxHealth:N0} / {hunter.MaxHealth:N0}", hpBar.Center.X, hpBar.Y + 4, Bone, UiTypography.Secondary);
        _ui.TextBig(b, $"TEMPO {hunter.SquadSkillRate:0.00}x SKILL RATE", tx, HunterCard.Y + 176, Slate, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(HunterCard.X + 24, HunterCard.Y + 224, HunterCard.Width - 48, 2), Dim);

        // Gear power (real: PowerRating) + a real focus row (no fictional named armour set exists).
        if (_ui.Assets.Get("state_resonance_128") is { } gi) b.Draw(gi, new Rectangle(HunterCard.X + 32, HunterCard.Y + 258, 44, 44), Ember);
        _ui.TextBig(b, "GEAR POWER", HunterCard.X + 88, HunterCard.Y + 256, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{hunter.PowerRating:N0}", HunterCard.X + 88, HunterCard.Y + 284, Bone, UiTypography.PrimaryValue);

        var srcTxt = Mastery?.Affinity() is not null ? "AURA" : "—";
        if (_ui.Assets.Get("state_mastery_128") is { } mi) b.Draw(mi, new Rectangle(HunterCard.X + 32, HunterCard.Y + 350, 44, 44), Gold);
        _ui.TextBig(b, "MASTERY POINTS", HunterCard.X + 88, HunterCard.Y + 348, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{MasteryPoints:N0}", HunterCard.X + 88, HunterCard.Y + 376, Bone, UiTypography.PrimaryValue);
    }

    private void DrawPrimary(SpriteBatch b, Hunter hunter, Point mouse, bool clicked)
    {
        _ui.Panel(b, PrimaryPanel);
        _ui.TextCenterBig(b, "PRIMARY ATTRIBUTES", PrimaryPanel.Center.X, PrimaryPanel.Y + 22, Gold, UiTypography.SectionTitle);
        var rows = new (string, HunterStat, Color)[]
        {
            ("MIGHT", HunterStat.AttackPower, new(0xD6, 0x48, 0x5C)),
            ("RESONANCE", HunterStat.ResonanceAffinity, new(0x74, 0xC6, 0xE8)),
            ("TEMPO", HunterStat.Engineering, new(0x48, 0xB8, 0x88)),
            ("VITALITY", HunterStat.Vitality, new(0xC0, 0x6E, 0xE0)),
        };
        var y = PrimaryPanel.Y + 84;
        foreach (var (label, stat, gem) in rows)
        {
            _ui.Diamond(b, new Rectangle(PrimaryPanel.X + 30, y + 2, 26, 26), gem);
            _ui.TextBig(b, label, PrimaryPanel.X + 74, y, Bone, UiTypography.Body);
            _ui.TextRightBig(b, $"{(int)hunter.ValueOf(stat)}", PrimaryPanel.Right - 218, y, Bone, UiTypography.Body);
            DrawTrain(b, hunter, stat, PrimaryPanel.Right - 202, y - 6, mouse, clicked);
            y += 58;
        }
    }

    // DERIVED STATS IS GONE, and every row of it now lives in the panel that produces it.
    //
    // It was eight rows and every one restated something already on screen: MAX HEALTH and CRITICAL
    // CHANCE and MITIGATION and HAUL are what the COMBAT rows buy (they say so now), ATTACK POWER was
    // MIGHT under a second name, and POWER RATING and SKILL RATE were already in the hunter card. It
    // occupied the centre of the screen — the largest single panel — to say nothing new, while the nine
    // buttons that are the screen's actual purpose sat unlabelled in the margin.
    //
    // Checked row by row before deleting: all eight have a home, so nothing was lost with it.
    private void DrawCombat(SpriteBatch b, Hunter hunter, Point mouse, bool clicked)
    {
        // Spec §9.1 is RESISTANCES, but the game has no elemental resistance model (§12 fallback). This is the
        // honest substitute: the remaining real commander attributes with their one-line effect.
        _ui.Panel(b, CombatPanel);
        _ui.TextCenterBig(b, "COMBAT ATTRIBUTES", CombatPanel.Center.X, CombatPanel.Y + 22, Gold, UiTypography.SectionTitle);
        // EACH ROW STATES WHAT IT PRODUCES, not just what it is called.
        //
        // This panel used to print the effect's NAME ("CRIT CHANCE") while a separate DERIVED STATS
        // panel printed the effect's VALUE ("6.5%") — so neither panel alone answered the only question
        // a player has here, which is what a rank actually buys. The two panels also disagreed under
        // identical labels: this one showed the rank, that one showed the result. Merged, the row says
        // "CRIT · 6 · 6.5% CHANCE · [TRAIN 34 G]" and DERIVED STATS is deleted.
        var shape = Loadout.ToBuild(Tree, Mastery, Character).Shape;
        var rows = new (string, HunterStat, string)[]
        {
            ("CRIT", HunterStat.CriticalChance, $"{100f * SoloBattle.CritChance(hunter, shape):0.0}% CHANCE"),
            ("FOCUS", HunterStat.Focus, $"{100f * SoloBattle.CritMultiplier(hunter):0}% CRIT DMG"),
            ("HEALTH", HunterStat.MaxHealth, $"{hunter.MaxHealth:N0} MAX HP"),
            ("DEFENSE", HunterStat.Defense, $"{100f - 100f * (100f / (100f + hunter.Defense)):0.0}% MITIGATION"),
            ("GUILE", HunterStat.Guile, $"+{100f * (hunter.HaulMultiplier - 1f):0}% HAUL"),
        };
        var y = CombatPanel.Y + 74;
        foreach (var (label, stat, effect) in rows)
        {
            _ui.Fill(b, new Rectangle(CombatPanel.X + 24, y - 6, CombatPanel.Width - 48, 44), RowBg);
            _ui.TextBig(b, label, CombatPanel.X + 40, y, Bone, UiTypography.Body);
            _ui.TextBig(b, effect, CombatPanel.X + 176, y + 2, Sky, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{(int)hunter.ValueOf(stat)}", CombatPanel.Right - 222, y, Slate, UiTypography.Body);
            DrawTrain(b, hunter, stat, CombatPanel.Right - 206, y - 4, mouse, clicked);
            y += 52;
        }
    }

    /// <summary>
    /// One rank of one stat, priced. The cost is on the button because it changes every purchase.
    /// </summary>
    /// <remarks>
    /// Geometric growth means the tenth rank of a stat costs many times the first, so a player choosing
    /// where gleam goes is making a real decision — and a button that only said "TRAIN" would hide the
    /// entire decision behind a click.
    /// </remarks>
    private void DrawTrain(SpriteBatch b, Hunter hunter, HunterStat stat, int x, int y, Point mouse, bool clicked)
    {
        var maxed = !hunter.CanTrain(stat) && hunter.Gleam >= hunter.NextRankCost(stat);
        var cost = hunter.NextRankCost(stat);
        var rect = new Rectangle(x, y, 168, 38);
        var afford = hunter.CanTrain(stat);

        // VERB FIRST, THEN THE PRICE. These nine buttons ARE the screen's purpose — Gleam exists to be
        // spent here — and they used to read "45 G" in a chip with no verb on it at all, so the one
        // interactive thing on the page looked like another data column. A reviewer seeing the screen
        // cold could not tell the stats were buyable.
        //
        // The price stays ON the button (geometric growth means the tenth rank costs many times the
        // first, so the cost IS the decision), but it is now the object of a verb rather than a bare
        // number: "TRAIN 45 G" cannot be misread as "this adds 45", which "45 G" beside a stat of 34
        // could.
        var label = maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} G" : $"NEED  {cost:N0} G";

        if (_ui.Button(b, rect, label, mouse, clicked, enabled: afford))
            _trainRequest = stat;
    }

    private void DrawProgression(SpriteBatch b, Hunter hunter)
    {
        _ui.Panel(b, ProgressPanel);
        _ui.TextCenterBig(b, "PROGRESSION", ProgressPanel.Center.X, ProgressPanel.Y + 22, Gold, UiTypography.SectionTitle);
        // Only the counters the game actually tracks (§12) — monsters/bosses/play-time/deaths aren't recorded.
        var rows = new (string, string)[]
        {
            ("HUNTER LEVEL", $"{hunter.HunterLevel}"),
            ("HIGHEST WAVE", $"{HighestWave}"),
            ("CHESTS OPENED", $"{ChestsOpened:N0}"),
            ("MASTERY POINTS", $"{MasteryPoints:N0}"),
        };
        var y = ProgressPanel.Y + 74;
        foreach (var (label, value) in rows)
        {
            _ui.TextBig(b, label, ProgressPanel.X + 40, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, value, ProgressPanel.Right - 40, y, Bone, UiTypography.Body);
            _ui.Fill(b, new Rectangle(ProgressPanel.X + 40, y + 36, ProgressPanel.Width - 80, 2), Dim * 0.5f);
            y += 48;
        }
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { HunterCard, PrimaryPanel, CombatPanel, ProgressPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, "nav STATS  read-only overview", 60, 112, Gold, UiTypography.Secondary);
    }

    private static string FormShort(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };
}
