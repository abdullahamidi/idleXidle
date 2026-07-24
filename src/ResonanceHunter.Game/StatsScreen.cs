using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The STATS page: the nine commander stats, each explained in full, trained here and nowhere else.
/// </summary>
/// <remarks>
/// Training used to share the Character sheet with the gear and the bag — the player asked for it to be its
/// OWN page, with proper explanations of what each stat actually does. So this is a master-detail screen: a
/// list of the nine on the left (value + train button), and on the right the SELECTED stat spelled out —
/// what it feeds, the exact per-point effect, and where it stands right now. A live DAMAGE / SEC gauge sits
/// beneath, so training a stat is judged by whether the number it promises to move actually moves.
/// </remarks>
public sealed class StatsScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Bg = new(0x0E, 0x0C, 0x12);
    private static readonly Color RowBg = new(0x1C, 0x18, 0x24);
    private static readonly Color RowHot = new(0x2A, 0x24, 0x14);
    private static readonly Color Sky = new(0x7A, 0x9A, 0xC0);

    private readonly UiKit _ui;

    public PlayerLoadout Loadout { get; set; } = null!;
    public MasteryTree Mastery { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;

    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    private DamageReadout _readout;
    private int _selected;
    private string _msg = "";
    private Color _msgColor = Bone;

    public StatsScreen(UiKit ui) => _ui = ui;

    /// <summary>One stat's full teaching card: what it feeds, the per-point effect, and a live readout.</summary>
    private sealed record StatInfo(
        HunterStat Stat, string Name, string Effect, string PerPoint, string[] Body, Func<Hunter, string> Readout);

    // The per-point numbers are the SAME constants the sim reads (HunterProgression), quoted here so the
    // explanation can never drift from the formula: MIGHT 0.010, VITALITY 0.012, TEMPO 0.006, GUILE 0.010,
    // FOCUS 0.01 crit-damage, CRIT 1% chance, DEFENSE the 100/(100+def) curve.
    private static readonly StatInfo[] Stats =
    {
        new(HunterStat.AttackPower, "MIGHT", "DAMAGE", "+1% champion damage per point",
            new[] { "The headline of the power curve.", "Multiplies every blow the champion", "lands — skills and auto-attacks alike." },
            h => $"damage now x{1f + 0.010f * h.AttackPower:0.00} from MIGHT alone"),
        new(HunterStat.ResonanceAffinity, "RESONANCE", "ABILITY", "raises the base power of every skill",
            new[] { "Skills are cast from Resonance.", "The higher it climbs, the harder the", "Forms you weave hit at their core." },
            h => $"skill base scales with Resonance {(int)h.ValueOf(HunterStat.ResonanceAffinity)}"),
        new(HunterStat.Engineering, "TEMPO", "SKILL SPD", "+0.6% skill speed per point",
            new[] { "How fast skills come back round.", "Faster TEMPO means more casts per", "fight — the visible half of pacing." },
            h => $"skills come back x{h.SquadSkillRate:0.00} as fast"),
        new(HunterStat.CriticalChance, "CRIT", "CRIT %", "+1% chance a skill hit crits (cap 75%)",
            new[] { "The odds a skill hit lands a", "critical. Pairs with FOCUS, which", "decides how hard that crit strikes." },
            // Include worn-affix crit — the fight clamps (stat + affix)/100 to 75%, so the page must show the same.
            h => $"crit chance {Math.Min(75f, h.ValueOf(HunterStat.CriticalChance) + h.AffixTotal(AffixStat.Crit)):0}%"),
        new(HunterStat.Focus, "FOCUS", "CRIT DMG", "+1% critical damage per point",
            new[] { "How hard a crit lands. A crit hits", "for x1.5 at base; every FOCUS point", "adds to that multiplier. Feeds CRIT." },
            h => $"a crit now hits for x{1.5f + 0.01f * h.ValueOf(HunterStat.Focus):0.00}"),
        new(HunterStat.Vitality, "VITALITY", "MAX HP %", "+1.2% max health per point",
            new[] { "Toughness as a multiplier — it", "scales your whole health pool, on", "top of the flat HEALTH stat." },
            h => $"health x{1f + 0.012f * h.ValueOf(HunterStat.Vitality):0.00} from VITALITY"),
        new(HunterStat.MaxHealth, "HEALTH", "MAX HP", "+5 flat max health per rank",
            new[] { "Flat health, before VITALITY", "multiplies it and the worn charm", "adds its own. The champion's floor." },
            h => $"max health {h.MaxHealth}"),
        new(HunterStat.Defense, "DEFENSE", "MITIGATE", "softens every incoming hit",
            new[] { "Mitigation on a curve: each bite is", "cut by 100 / (100 + DEFENSE), so it", "always helps and never reaches 100%." },
            h => $"incoming damage taken x{100f / (100f + h.Defense):0.00}"),
        new(HunterStat.Guile, "GUILE", "HAUL", "+1% haul per point",
            new[] { "Greed as a stat. Multiplies the", "gleam and loot-quality a cleared", "wave pays out." },
            h => $"haul x{h.HaulMultiplier:0.00}"),
    };

    // ── Layout ─────────────────────────────────────────────────────────────────────────────────
    private static readonly Rectangle ListPanel = new(6, 26, 200, 206);
    private static readonly Rectangle DetailPanel = new(212, 26, 262, 150);
    private static readonly Rectangle DpsPanel = new(212, 182, 262, 50);

    private static Rectangle Row(int i) => new(ListPanel.X + 6, ListPanel.Y + 22 + i * 20, ListPanel.Width - 12, 18);
    private static Rectangle TrainBtn(int i) { var r = Row(i); return new(r.Right - 48, r.Y + 3, 46, 16); }

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        if (Loadout is not null && Tree is not null && Mastery is not null)
            _readout = DamageBench.Measure(Loadout.ToBuild(Tree, Mastery), hunter);

        // Keyboard: up/down selects, Enter/Space trains the selected stat.
        if (Down(keys, prev, Keys.Down)) _selected = (_selected + 1) % Stats.Length;
        if (Down(keys, prev, Keys.Up)) _selected = (_selected - 1 + Stats.Length) % Stats.Length;
        if (Down(keys, prev, Keys.Enter) || Down(keys, prev, Keys.Space)) TryTrain(hunter, Stats[_selected].Stat);

        if (!clicked) return;

        for (var i = 0; i < Stats.Length; i++)
        {
            if (TrainBtn(i).Contains(mouse)) { TryTrain(hunter, Stats[i].Stat); _selected = i; return; }
            if (Row(i).Contains(mouse)) { _selected = i; return; }
        }
    }

    private static bool Down(KeyboardState now, KeyboardState prev, Keys k) => now.IsKeyDown(k) && prev.IsKeyUp(k);

    private void TryTrain(Hunter hunter, HunterStat stat)
    {
        if (hunter.Train(stat)) { Dirty = true; Say("TRAINED.", Gold); }
        else Say(hunter.RankOf(stat) >= 60 ? "ALREADY AT THE CAP." : "NOT ENOUGH GLEAM.", Ember);
    }

    private void Say(string msg, Color color) { _msg = msg; _msgColor = color; }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        _ui.Fill(b, new Rectangle(0, 0, 480, 270), Bg);

        _ui.Title(b, "COMMANDER STATS", $"LV {hunter.HunterLevel} · TRAIN WITH GLEAM");
        // Gleam/Dust/Materials come from the shared currency pills (Game1.DrawCurrencyPills).

        DrawList(b, mouse, hunter);
        DrawDetail(b, hunter);
        DrawDps(b);

        // The train result shows itself — the stat value steps up and the detail panel re-reads. The
        // top-right is the shared currency row now, so no toast rides there.
        _ = _msg;
        _ui.NavLegend(b);
    }

    private void DrawList(SpriteBatch b, Point mouse, Hunter hunter)
    {
        _ui.Panel(b, ListPanel);
        _ui.Text(b, "COMMANDER STATS", ListPanel.X + 6, ListPanel.Y + 8, Slate);

        for (var i = 0; i < Stats.Length; i++)
        {
            var s = Stats[i];
            var row = Row(i);
            var sel = i == _selected;
            var btn = TrainBtn(i);
            var can = hunter.CanTrain(s.Stat);
            var hot = btn.Contains(mouse) && can;

            _ui.Fill(b, row, sel ? RowHot : RowBg);
            if (sel) _ui.Fill(b, new Rectangle(row.X, row.Y, 2, row.Height), Gold);
            _ui.Text(b, s.Name, row.X + 6, row.Y + 3, sel ? Gold : Bone);
            _ui.Text(b, s.Effect, row.X + 6, row.Y + 12, Slate);
            _ui.TextRight(b, $"{(int)hunter.ValueOf(s.Stat)}", row.Right - 52, row.Y + 7, Sky);

            if (hunter.RankOf(s.Stat) >= 60) { _ui.TextRight(b, "MAX", btn.Right - 4, btn.Y + 4, Slate); continue; }
            _ui.Fill(b, btn, hot ? new Color(0x3A, 0x30, 0x14) : new Color(0x24, 0x20, 0x16));
            _ui.TextCenter(b, $"+{hunter.NextRankCost(s.Stat)}", btn.Center.X, btn.Y + 4, can ? Gold : Dim);
        }
    }

    private void DrawDetail(SpriteBatch b, Hunter hunter)
    {
        _ui.Panel(b, DetailPanel);
        var s = Stats[_selected];

        _ui.Text(b, s.Name, DetailPanel.X + 10, DetailPanel.Y + 10, Gold);
        _ui.TextRight(b, s.Effect, DetailPanel.Right - 10, DetailPanel.Y + 10, Sky);

        // The big current value.
        _ui.Text(b, $"{(int)hunter.ValueOf(s.Stat)}", DetailPanel.X + 10, DetailPanel.Y + 24, Bone);
        _ui.Text(b, $"RANK {hunter.RankOf(s.Stat)} / 60", DetailPanel.X + 44, DetailPanel.Y + 28, Slate);

        var y = DetailPanel.Y + 52;
        foreach (var line in s.Body) { _ui.Text(b, line, DetailPanel.X + 10, y, Bone); y += 11; }

        // The exact per-point promise, and where it stands right now.
        _ui.Fill(b, new Rectangle(DetailPanel.X + 8, y + 4, DetailPanel.Width - 16, 1), Dim);
        _ui.Text(b, s.PerPoint, DetailPanel.X + 10, y + 10, Slate);
        _ui.Text(b, s.Readout(hunter), DetailPanel.X + 10, y + 22, new Color(0x6E, 0xC8, 0x7A));

        if (hunter.RankOf(s.Stat) < 60)
            _ui.Text(b, $"NEXT RANK COSTS {hunter.NextRankCost(s.Stat)} GLEAM", DetailPanel.X + 10, DetailPanel.Bottom - 14, Gold);
        else
            _ui.Text(b, "MAXED — this stat is at its cap.", DetailPanel.X + 10, DetailPanel.Bottom - 14, Slate);
    }

    private void DrawDps(SpriteBatch b)
    {
        _ui.Panel(b, DpsPanel, gold: true);
        _ui.Text(b, "DAMAGE / SEC", DpsPanel.X + 10, DpsPanel.Y + 10, Slate);
        _ui.Text(b, Short((long)MathF.Round(_readout.Dps)), DpsPanel.X + 10, DpsPanel.Y + 24, Ember);
        _ui.TextRight(b, "VS DUMMY — TRAIN TO MOVE IT", DpsPanel.Right - 10, DpsPanel.Y + 26, Slate);
        _ui.Text(b, $"OVER {_readout.Seconds:0}S AT FULL HP", DpsPanel.X + 10, DpsPanel.Bottom - 14, Slate);
    }

    private static string Short(long n)
        => n >= 1_000_000 ? $"{n / 1_000_000f:0.0}M" : n >= 1_000 ? $"{n / 1_000f:0.0}K" : n.ToString();
}
