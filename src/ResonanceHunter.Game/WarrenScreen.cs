using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Warrens;

using ResonanceHunter.Core.Progression;
namespace ResonanceHunter.Client;

/// <summary>
/// The WARREN screen (nav: WARREN): a facility-management dashboard. Overview + 8-facility grid + a bonuses
/// strip + a facility detail/upgrade panel, built to the Warren production spec (rev 1).
/// </summary>
/// <remarks>
/// Every value is real, from the <see cref="Core.Warrens.Warren"/> model the host owns: facility levels
/// and outputs, the Warren level/XP, the derived production bonuses, and the real upgrade costs in Gleam /
/// Mastery / Dust. The creature "den" (the old Warren) is preserved as a sub-view reached via the CREATURES
/// button. The host sets the model + owned balances each frame and consumes the upgrade / den requests.
/// </remarks>
public sealed class WarrenScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color GleamC = new(0xF0, 0xA8, 0x30);   // gold coin track
    private static readonly Color MasteryC = new(0xC0, 0x6E, 0xE0); // purple gem track
    private static readonly Color DustC = new(0x5F, 0xE0, 0xC8);    // teal gem track

    private readonly UiKit _ui;
    public WarrenScreen(UiKit ui) => _ui = ui;

    // ── Host-set each frame ───────────────────────────────────────────────────────────────────────
    public Warren Warren { get; set; } = null!;
    public long GleamOwned { get; set; }
    public long MasteryOwned { get; set; }
    public long DustOwned { get; set; }
    public bool DevWarrenDebug { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private FacilityKind? _upgradeRequest;
    public FacilityKind? ConsumeUpgrade() { var r = _upgradeRequest; _upgradeRequest = null; return r; }

    private FacilityKind _selected = FacilityKind.Nursery;

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    private static readonly Rectangle OverviewPanel = new(28, 154, 388, 708);
    private static readonly Rectangle GridPanel = new(446, 154, 920, 500);
    // TEN PIXELS TALLER, upward. Its title was at +12, which is INSIDE the medium frame's 20 px top
    // rail — the one panel on this screen whose header was printed on its own ornament. Nothing here
    // could move down (the strip already ends level with the two columns beside it), so the strip took
    // the room from the gap above it, which had 22 to spare.
    private static readonly Rectangle BonusStrip = new(446, 666, 920, 196);
    private static readonly Rectangle DetailPanel = new(1400, 154, 480, 708);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Facilities => new[] { GridPanel },
        TourTarget.FacilityDetail => new[] { DetailPanel },
        TourTarget.WarrenOverview => new[] { OverviewPanel },
        _ => Array.Empty<Rectangle>(),
    };

    private static Rectangle Card(int i)
    {
        var col = i % 4;
        var row = i / 4;
        // Centred, not inset by a hand-picked 24: this panel's whole content is the grid, so the slack
        // is split rather than assigned. Written as +24 it was symmetric by coincidence.
        var left = (GridPanel.Width - (3 * 222 + 206)) / 2;
        return new Rectangle(GridPanel.X + left + col * 222, GridPanel.Y + 58 + row * 212, 206, 200);
    }

    private Color ResColor(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => GleamC, WarrenResource.Mastery => MasteryC, _ => DustC,
    };

    /// <summary>
    /// The currency's real icon (Gleam coin, Memory Dust, Insight medallion), the tinted diamond only
    /// when the art is missing. Playtest 2026-08-23: "Gleam, Dust ve Insight kaynaklarının ikonları yok."
    /// </summary>
    private void ResGlyph(SpriteBatch b, Rectangle box, WarrenResource? r, Color? fallback = null)
    {
        var key = r switch
        {
            WarrenResource.Gleam => "ui_gleam_coin",
            WarrenResource.Dust => "ui_memory_dust",
            WarrenResource.Mastery => "ui_insight",
            _ => "",
        };
        if (key.Length > 0 && _ui.Assets.Get(key) is { } tex) { b.Draw(tex, box, Color.White); return; }
        _ui.Diamond(b, box, fallback ?? (r is { } rr ? ResColor(rr) : Dim));
    }

    private static WarrenResource? ResFromLabel(string label) => label.ToUpperInvariant() switch
    {
        "GLEAM" => WarrenResource.Gleam,
        "INSIGHT" => WarrenResource.Mastery,
        "DUST" or "MEMORY DUST" => WarrenResource.Dust,
        _ => null,
    };

    private static string ResName(WarrenResource r) => r switch
    {
        // THE GAME'S OWN NAMES FOR ITS OWN CURRENCIES. Two of these three were simply wrong: the first
        // pool is Hunter.Gleam, which every other screen calls GLEAM, and the third is
        // MemoryDust.MemoryDust, which the TRAITS screen spends and calls DUST — this screen called them
        // GOLD and NATURE. The middle one is renamed rather than corrected: it is a Warren-only pool
        // (see the note on WarrenResource) and calling it MASTERY now collides with a rail tile, a tree
        // and a point currency that it has nothing to do with.
        WarrenResource.Gleam => "GLEAM", WarrenResource.Mastery => "INSIGHT", _ => "DUST",
    };

    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    public void Draw(SpriteBatch b) => Draw(b, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "WARREN", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // THE SUBTITLE SAYS WHAT THIS SCREEN IS FOR, not what its panels are called.
        //
        // It used to recite the headers directly beneath it, which spends the largest 100px on the page
        // to tell the player something they can already read, and teaches them that big text in this
        // band is not worth reading. Same slot, same cost, real content.
        _ui.TextCenterBig(b, "THE WARREN EARNS WHILE YOU ARE AWAY — COME BACK AND SPEND",
                          960, 80, Slate, UiTypography.Secondary);

        DrawOverview(b, hit, clicked);
        DrawGrid(b, hit, clicked);
        DrawBonuses(b);
        DrawDetail(b, hit, clicked);
        if (DevWarrenDebug) DrawDebug(b);
    }

    /// <summary>
    /// THE WARREN'S RANK, STRUCK ON ITS OWN MEDALLION — the shipping gold octagon with the number in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-08-29: "in the warren overview panel, there is no LEVEL icon". There WAS a crest,
    /// and that was the problem: it was <see cref="UiKit.Diamond"/> in the Insight purple, which is the
    /// shape this kit draws when a key is MISSING. So the one badge on the screen that names the base's
    /// own rank wore the placeholder for art that had not shipped — except it had.
    /// <c>ui_medallion_hex</c> is a hollow gold medallion frame drawn for exactly this, and a medallion
    /// with a numeral inside it is the one badge every player already reads as a level without being
    /// told. The words WARREN LEVEL stay beside it, spelled out, for the reader who does not.
    /// </para>
    /// <para>
    /// The numeral SHRINKS rather than spilling over the rim. The medallion's hollow is about 56% of its
    /// width; a three-figure warren is a long game away, but it is a game away rather than impossible,
    /// and a badge that breaks at level 100 is a bug with a date on it.
    /// </para>
    /// </remarks>
    private void LevelBadge(SpriteBatch b, Rectangle box, int level)
    {
        if (!_ui.Icon(b, "ui_medallion_hex", box, Gold)) _ui.Diamond(b, box, MasteryC);
        var num = level.ToString();
        var px = UiTypography.PrimaryValue;
        var hollow = box.Width * 56 / 100;
        while (px > UiTypography.Caption && _ui.MeasureBig(num, px) > hollow) px--;
        _ui.TextCenterBig(b, num, box.Center.X, box.Center.Y - px * 27 / 40, Bone, px);
    }

    private void DrawOverview(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, OverviewPanel);
        var left = UiKit.ContentLeft(OverviewPanel);
        var right = UiKit.ContentRight(OverviewPanel);
        var width = right - left;

        _ui.TextCenterBig(b, "WARREN OVERVIEW", OverviewPanel.Center.X, UiKit.TitleTop(OverviewPanel), Gold, UiTypography.PanelTitle);
        // THE CAPTION SLOT CARRIES THE PLACE'S OWN NAME. It used to be the third line of a hand-placed
        // stack beside the crest; the house grid keeps the line that says WHICH one of these you are
        // looking at directly under the title, which is where every other panel in the game puts it.
        _ui.TextCenterBig(b, _ui.ShortenBig(Warren.Name, width, UiTypography.Secondary),
                          OverviewPanel.Center.X, UiKit.CaptionTop(OverviewPanel), MasteryC, UiTypography.Secondary);

        // ── THE LEVEL, ON ITS BADGE, WITH WHAT THE LEVEL BUYS BESIDE IT ──
        var crest = new Rectangle(left, UiKit.BodyTop(OverviewPanel), 84, 84);
        LevelBadge(b, crest, Warren.Level);
        _ui.TextBig(b, "WARREN LEVEL", crest.Right + 20, crest.Y + 12, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"+{Warren.AllProductionBonus * 100f:0}% ALL PRODUCTION", crest.Right + 20, crest.Y + 48, Met, UiTypography.Body);

        // XP BAR TO THE NEXT WARREN LEVEL — the ART's bar, at a height the art can hold.
        //
        // It was UiKit.Bar, which stretches the whole 256×64 ui_bar_frame into whatever rectangle it is
        // handed: 332 × 26 here, so the end scrollwork was squashed to two fifths of its height and
        // smeared across five times its width (playtest 2026-08-29: "the level bar's frame is stretched
        // — its quality has dropped"). BarArt five-slices a WIDE bar, so the two cap ornaments and the
        // centre scroll keep their proportions and only the plain stone between them stretches; 34 px of
        // height then gives those caps 17 px to be drawn in rather than 13.
        var bar = new Rectangle(left, crest.Bottom + 26, width, 34);
        var pct = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 1f;
        _ui.BarArt(b, bar, pct, "xp");
        // ONE INK, not two. The old readout switched to near-black past 55% because cream on the GOLD
        // fill measures about 1.6:1; the xp art's fill is deep violet, which cream clears at both ends
        // of the bar, so the conditional goes with the gold it was compensating for.
        _ui.TextCenterBig(b, $"{Warren.Xp:N0} / {Warren.XpToNext:N0}", bar.Center.X,
                          bar.Y + (bar.Height - UiTypography.Secondary) / 2, Bone, UiTypography.Secondary);
        _ui.TextBig(b, $"NEXT LEVEL {Warren.Level + 1}", left, bar.Bottom + 12, Slate, UiTypography.Secondary);

        var rule = bar.Bottom + 44;
        _ui.Fill(b, new Rectangle(left, rule, width, 2), Dim);

        // Total production, per real currency (bonuses applied).
        _ui.TextBig(b, "TOTAL PRODUCTION", left, rule + 18, Gold, UiTypography.Body);
        _ui.TextRightBig(b, "(IDLE RATE)", right, rule + 22, Slate, UiTypography.Secondary);
        var y = rule + 66;
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Mastery, WarrenResource.Dust })
        {
            ResGlyph(b, new Rectangle(left, y - 2, 38, 38), r);
            _ui.TextBig(b, $"+{Ab(Warren.ProductionPerMinute(r))} /min", left + 60, y + 2, Bone, UiTypography.Headline);
            // The three rows are spaced to REACH the rule above the closing paragraph rather than to a
            // tight 52: the panel is 708 px tall and its content used to stop 110 px short of that rule,
            // which reads as a panel that ran out of things to say rather than as breathing room.
            y += 72;
        }

        // Production runs on every screen (see Game1.TickFarms) — a quiet reminder that the base earns idle.
        _ui.Fill(b, new Rectangle(left, OverviewPanel.Bottom - 124, width, 2), Dim);
        // WHAT EACH OF THE THREE IS FOR, because the screen was silent about it and one of them is
        // unlike the other two. GLEAM and DUST are the game's shared currencies — spent on stats and on
        // the trait tree — while INSIGHT is produced here and spent here, on nothing else. That is a
        // legitimate design (it paces the base's own growth) but the player has no way to learn it from
        // a row of three coloured diamonds, and its old name made it look like the mastery points it
        // has nothing to do with.
        DrawWrapped(b, "The warren earns while you are away. GLEAM and DUST are used all over the game. "
                     + "INSIGHT is used only here, on upgrades.",
            left, OverviewPanel.Bottom - 104, width, Slate);
    }

    private void DrawGrid(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, GridPanel);
        var i = 0;
        foreach (var f in Warren.AllFacilities)
        {
            var card = Card(i++);
            var sel = f.Kind == _selected;
            if (UiKit.ClickedIn(card, hit, clicked)) _selected = f.Kind;

            _ui.Fill(b, card, new Color(0x16, 0x12, 0x20, 0xD0));
            var rc = ResColor(f.Info.Produces);
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 4), sel ? Gold : rc * 0.6f);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 4, card.Width, 4), sel ? Gold : rc * 0.35f);
            if (sel) { _ui.Fill(b, new Rectangle(card.X, card.Y, 4, card.Height), Gold); _ui.Fill(b, new Rectangle(card.Right - 4, card.Y, 4, card.Height), Gold); }

            _ui.TextCenterBig(b, f.Info.Name, card.Center.X, card.Y + 14, sel ? Bone : Slate, UiTypography.Secondary);
            // Milestone pips (top-right) — one gold gem per milestone crossed, the at-a-glance long-term goal.
            for (var m = 0; m < f.MilestoneTier && m < 5; m++)
                _ui.Diamond(b, new Rectangle(card.Right - 18 - m * 30, card.Y + 30, 11, 11), Gold);
            // The facility's own picture, on a resource-tinted hex plate. Eight identical hexagons
            // distinguished only by colour told the player nothing about what each place does.
            var plate = new Rectangle(card.Center.X - 44, card.Y + 46, 88, 76);
            _ui.Hex(b, plate, rc * (sel ? 0.55f : 0.38f));
            var icon = $"icon_facility_{f.Kind.ToString().ToLowerInvariant()}";
            if (!_ui.Icon(b, icon, new Rectangle(plate.X + 2, plate.Y - 4, plate.Width - 4, plate.Height + 8)))
                _ui.Hex(b, plate, rc);
            _ui.TextCenterBig(b, $"LEVEL {f.Level}", card.Center.X, card.Bottom - 70, Gold, UiTypography.Body);
            ResGlyph(b, new Rectangle(card.Center.X - 68, card.Bottom - 42, 26, 26), f.Info.Produces, rc);
            _ui.TextCenterBig(b, $"+{Ab(f.BaseOutputPerMin)} /min", card.Center.X + 8, card.Bottom - 38, Bone, UiTypography.Secondary);
        }
    }

    private void DrawBonuses(SpriteBatch b)
    {
        _ui.PanelQuiet(b, BonusStrip);
        _ui.TextCenterBig(b, "WARREN BONUSES", BonusStrip.Center.X, UiKit.TitleTop(BonusStrip), Gold, UiTypography.PanelTitle);
        // Playtest 2026-08-23: "ALL PRODUCTION, CONQUEST and WARREN have no icons, and I could not
        // understand what they do. CONQUEST what, +40%?" Each chip now carries its own icon and the
        // one line that says where its number comes from, and the strip opens by saying what the
        // numbers DO. The old sixth chip (WARREN LV n) was not a bonus at all and repeated the
        // overview panel two hand-widths to the left — it is gone.
        // The first cut said "add them up", which is wrong: only ONE of the three currency chips ever
        // applies to a given currency (Warren.Multiplier = 1 + all three + conquest + that currency's
        // own chip). So the line gives the three answers instead, straight from Multiplier — the screen
        // must never re-derive the sum.
        _ui.TextCenterBig(b,
            $"GLEAM x{Warren.Multiplier(WarrenResource.Gleam):0.00}   ·   "
            + $"INSIGHT x{Warren.Multiplier(WarrenResource.Mastery):0.00}   ·   "
            + $"DUST x{Warren.Multiplier(WarrenResource.Dust):0.00}"
            + "   —   EACH IS 1 PLUS ALL THREE PLUS CONQUEST PLUS ITS OWN CHIP",
            BonusStrip.Center.X, UiKit.CaptionTop(BonusStrip), Slate, UiTypography.Secondary);

        var regions = Warren.ConqueredRegions;
        var entries = new (string Key, Color Gem, string Value, string Label, string Why)[]
        {
            ("ui_gleam_coin", GleamC, $"+{Warren.ResourceBonus(WarrenResource.Gleam) * 100f:0}%", "GLEAM", "2% A LEVEL"),
            ("ui_insight", MasteryC, $"+{Warren.ResourceBonus(WarrenResource.Mastery) * 100f:0}%", "INSIGHT", "1.3% A LEVEL"),
            ("ui_memory_dust", DustC, $"+{Warren.ResourceBonus(WarrenResource.Dust) * 100f:0}%", "DUST", "0.9% A LEVEL"),
            ("icon_blessing_amplifier", Met, $"+{Warren.AllProductionBonus * 100f:0}%", "ALL THREE", "3% A LEVEL AFTER 1"),
            ("icon_blessing_expansion", Ember, $"+{Warren.ConquestBonus * 100f:0}%", "CONQUEST",
                regions == 1 ? "1 REGION TAKEN" : $"{regions} REGIONS TAKEN"),
        };
        var slot = (BonusStrip.Width - UiKit.PadX(BonusStrip) * 2) / entries.Length;
        for (var i = 0; i < entries.Length; i++)
        {
            var (key, gem, value, label, why) = entries[i];
            var cx = UiKit.ContentLeft(BonusStrip) + slot * i + slot / 2;
            if (_ui.Assets.Get(key) is { } ic) b.Draw(ic, new Rectangle(cx - 76, BonusStrip.Y + 88, 40, 40), Color.White);
            else _ui.Diamond(b, new Rectangle(cx - 74, BonusStrip.Y + 90, 36, 36), gem);
            _ui.TextBig(b, value, cx - 26, BonusStrip.Y + 86, Bone, UiTypography.Headline);
            _ui.TextCenterBig(b, label, cx, BonusStrip.Y + 134, Bone, UiTypography.Secondary);
            _ui.TextCenterBig(b, why, cx, BonusStrip.Y + 160, Slate, UiTypography.Secondary);
        }
    }

    private void DrawDetail(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);
        var f = Warren.Facility(_selected);
        var rc = ResColor(f.Info.Produces);

        _ui.TextCenterBig(b, f.Info.Name, DetailPanel.Center.X, UiKit.TitleTop(DetailPanel), Gold, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"LEVEL {f.Level}", DetailPanel.Center.X, UiKit.CaptionTop(DetailPanel), Slate, UiTypography.Body);
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(DetailPanel), UiKit.BodyTop(DetailPanel), DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 2), Dim);

        DrawWrapped(b, f.Info.Description, UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 112, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, Bone);

        // Output: current -> next (the NEXT figure includes any milestone jump the upgrade crosses).
        _ui.TextBig(b, "OUTPUT", UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 190, Slate, UiTypography.Body);
        ResGlyph(b, new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 224, 34, 34), f.Info.Produces);
        _ui.TextBig(b, $"+{Ab(f.BaseOutputPerMin)} /min", UiKit.ContentLeft(DetailPanel) + 44, DetailPanel.Y + 226, Bone, UiTypography.Headline);
        _ui.TextRightBig(b, $"NEXT  +{Ab(f.NextLevelOutput)} /min", UiKit.ContentRight(DetailPanel), DetailPanel.Y + 230, Met, UiTypography.Secondary);

        // Milestones — the long-term goal. Show the crossed multiplier and where the next one lands.
        var mile = new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 274, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 44);
        _ui.Fill(b, mile, new Color(0x16, 0x12, 0x20, 0xC0));
        _ui.TextBig(b, f.MilestoneTier > 0 ? $"MILESTONES  ×{f.MilestoneMultiplier:0.00} OUTPUT" : "MILESTONES  —", mile.X + 12, mile.Y + 12, Gold, UiTypography.Secondary);
        _ui.TextRightBig(b, $"→ LEVEL {f.NextMilestoneLevel}", mile.Right - 12, mile.Y + 12, Slate, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 336, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 2), Dim);
        _ui.TextBig(b, "COST TO UPGRADE", UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 352, Slate, UiTypography.Body);

        var cost = f.UpgradeCost();
        var y = DetailPanel.Y + 388;
        DrawReq(b, y, GleamC, "GLEAM", GleamOwned, cost.Gleam);
        DrawReq(b, y + 42, MasteryC, "INSIGHT", MasteryOwned, cost.Mastery);
        DrawReq(b, y + 84, DustC, "DUST", DustOwned, cost.Dust);

        var afford = GleamOwned >= cost.Gleam && MasteryOwned >= cost.Mastery && DustOwned >= cost.Dust;

        // The cap is stated, not merely enforced. A greyed button with no reason reads as a bug; a
        // player who is told the ceiling is theirs to raise knows the answer is to go and descend.
        //
        // It names the DEPTH the next level wants, not the cap. The cap is the wrong number twice over:
        // it is not actionable (the player cannot spend a cap), and it can contradict the level printed
        // directly above it — the ceiling comes from the deepest run anywhere, so if that figure ever
        // falls the screen announces "CAPPED AT LEVEL 1" beneath a facility reading LEVEL 18.
        var capped = Warren!.IsAtLevelCap(_selected);
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 502, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 2), Dim);
        _ui.TextBig(b,
            capped
                ? $"REACH DEPTH {Warren.DepthForNextLevel(_selected)} ON AN EXPEDITION TO UPGRADE"
                : "INSTANT UPGRADE  ·  NO WAIT",
            UiKit.ContentLeft(DetailPanel), DetailPanel.Y + 518, capped ? GleamC : Met, UiTypography.Secondary);

        if (_ui.Button(b, new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Bottom - 96,
                                      DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 72),
                capped ? "DEPTH LOCKED" : "UPGRADE", hit, clicked, enabled: afford && !capped))
            _upgradeRequest = _selected;
    }

    private void DrawReq(SpriteBatch b, int y, Color gem, string label, long owned, int required)
    {
        var ok = owned >= required;
        ResGlyph(b, new Rectangle(UiKit.ContentLeft(DetailPanel), y, 30, 30), ResFromLabel(label), gem);
        _ui.TextBig(b, label, UiKit.ContentLeft(DetailPanel) + 40, y, Bone, UiTypography.Body);
        // The verdict is a fixed 46px column; the ratio ends where that column starts. Previously both were
        // right-aligned 28px apart, so "131.9M / 7.8M" ran straight through the "OK" beside it.
        _ui.TextRightBig(b, $"{Ab(owned)} / {Ab(required)}", UiKit.ContentRight(DetailPanel) - 46, y, ok ? Met : Ember, UiTypography.Secondary);
        _ui.TextRightBig(b, ok ? "OK" : "X", UiKit.ContentRight(DetailPanel), y, ok ? Met : Ember, UiTypography.Secondary);
    }

    /// <summary>
    /// Word-wrapped body text at the shared Body size.
    /// </summary>
    /// <remarks>
    /// It used to MEASURE with <c>_ui.Measure</c> (the default size) and DRAW with <c>_ui.Text</c>
    /// (also the default), while every other label on this screen goes through the sized calls. The
    /// default is much larger than <see cref="UiTypography.Body"/>, so the description paragraphs
    /// rendered roughly twice the size of their own headings and wrapped in the wrong places — the
    /// only text on the screen off the type scale.
    /// </remarks>
    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        const int px = UiTypography.Body;
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.MeasureBig(probe, px) > width && line.Length > 0)
            {
                _ui.TextBig(b, line, x, y, c, px);
                y += px * 3 / 2;
                line = w;
            }
            else line = probe;
        }
        if (line.Length > 0) _ui.TextBig(b, line, x, y, c, px);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { OverviewPanel, GridPanel, BonusStrip, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav WARREN  facilities  sel {_selected}", 446, 118, Gold, UiTypography.Secondary);
    }
}
