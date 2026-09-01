using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Prestige;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// TRAINING: the nine things a hunter can train, grouped by what they change, each row saying what it
/// is NOW and what it becomes AFTER one rank — with a right-hand inspector that explains the selected
/// one in full, and RESET ALL TRAINING along the foot.
/// </summary>
/// <remarks>
/// <para>
/// Every figure is read through <see cref="Build.Resolve"/> and <see cref="SoloBattle"/>'s own public
/// formulas, never recomputed here, because a screen that recomputes a formula is a second
/// implementation of it and the second one is the one that drifts. The AFTER figures obey the same
/// rule: they come from <see cref="Hunter.Preview{T}"/>, which raises the rank, measures with the real
/// formula and restores it — so the screen cannot promise a number the purchase will not deliver.
/// </para>
/// <para>
/// Before UX V2 P2.3 the screen said what each stat IS and what a rank COSTS but never what a rank
/// would DO, so the one decision it exists for was made blind. It also spent a third of its width on
/// GEAR POWER, MASTERY POINTS, CHESTS OPENED and HIGHEST WAVE — four numbers that belong to four other
/// screens and none of which move when you train.
/// </para>
/// <para>
/// RESET ALL TRAINING costs one Crystal and returns NO Gleam. Two clicks on purpose, exactly like the
/// settings panel's START A NEW GAME: the first arms a red are-you-sure state, the second confirms,
/// any other click disarms.
/// </para>
/// </remarks>
public sealed class TrainingScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;
    private static readonly Color ArmedRed = new(0x8C, 0x1E, 0x1E);   // the settings panel's are-you-sure red

    private readonly UiKit _ui;

    // Set by the host each frame (like the other screens).
    public MasteryTree Mastery { get; set; } = null!;
    public PlayerLoadout Loadout { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;

    /// <summary>
    /// The active character, because this page states the player's numbers and a character is one of
    /// the three things that changes them.
    /// </summary>
    /// <remarks>
    /// Passing this to <c>ToBuild</c> is not optional bookkeeping. The two-argument overload silently
    /// substitutes <c>null</c>, and a shape built without the character is a shape the sim never uses.
    /// </remarks>
    public Character? Character { get; set; }

    /// <summary>The skills' earned choices — without it this page priced a build the sim never runs.</summary>
    public SkillProgress? SkillLevels { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevStatsDebug { get; set; }

    public TrainingScreen(UiKit ui) => _ui = ui;

    /// <summary>The player's word for a trained stat — MIGHT, never AttackPower.</summary>
    /// <remarks>
    /// Public because the HOST builds this screen's hint line, and was printing the enum name into it:
    /// "YOU CAN TRAIN ATTACKPOWER FOR 25 GLEAM". The words existed only inside a tuple in this file,
    /// where the host could not reach them.
    /// </remarks>
    public static string WordFor(HunterStat s) => s switch
    {
        HunterStat.AttackPower => "MIGHT",
        HunterStat.ResonanceAffinity => "RESONANCE",
        HunterStat.CriticalChance => "CRITICAL",
        HunterStat.Focus => "FOCUS",
        HunterStat.MaxHealth => "HEALTH",
        HunterStat.Defense => "DEFENSE",
        HunterStat.Vitality => "VITALITY",
        HunterStat.Engineering => "TEMPO",
        _ => "GUILE",
    };

    /// <summary>The nine stats grouped by what each one actually changes in the fight.</summary>
    /// <remarks>
    /// Not a taste: DAMAGE is the four stats the damage formulas read; SURVIVAL is the three the pool
    /// and the mitigation read; SPEED is the one the beat reads; REWARD is the one the payout reads.
    /// Every stat is in exactly one group, and no group is invented.
    /// </remarks>
    private static readonly (string Caption, HunterStat[] Stats)[] Groups =
    {
        ("DAMAGE — WHAT YOUR HITS ARE WORTH",
            new[] { HunterStat.AttackPower, HunterStat.ResonanceAffinity, HunterStat.CriticalChance, HunterStat.Focus }),
        ("SURVIVAL — HOW LONG YOU STAY UP",
            new[] { HunterStat.MaxHealth, HunterStat.Defense, HunterStat.Vitality }),
        ("SPEED — HOW OFTEN YOU ACT", new[] { HunterStat.Engineering }),
        ("REWARD — WHAT A CLEARED WAVE PAYS", new[] { HunterStat.Guile }),
    };

    /// <summary>The group a stat belongs to — the inspector's CATEGORY line reads it.</summary>
    private static string GroupOf(HunterStat s)
    {
        foreach (var (caption, stats) in Groups)
            if (Array.IndexOf(stats, s) >= 0)
                return caption.Split(" — ")[0];
        return "TRAINED STAT";
    }

    /// <summary>The row's icon and its flat-diamond fallback, if the art ever goes missing.</summary>
    private static (string Key, Color Gem) ArtFor(HunterStat s) => s switch
    {
        HunterStat.AttackPower => ("stat_might", new Color(0xD6, 0x48, 0x5C)),
        HunterStat.ResonanceAffinity => ("stat_resonance", new Color(0x74, 0xC6, 0xE8)),
        HunterStat.Engineering => ("stat_tempo", new Color(0x48, 0xB8, 0x88)),
        HunterStat.Vitality => ("stat_vitality", new Color(0xC0, 0x6E, 0xE0)),
        HunterStat.MaxHealth => ("stat_health", new Color(0x6E, 0xC8, 0x7A)),
        HunterStat.Defense => ("stat_defense", new Color(0x8A, 0x96, 0xA8)),
        HunterStat.CriticalChance => ("stat_critical", new Color(0xF0, 0xA8, 0x30)),
        HunterStat.Focus => ("stat_focus", new Color(0xE8, 0xC8, 0x7A)),
        _ => ("stat_guile", UiInk.Accent),
    };

    /// <summary>One line saying what the stat is, for the inspector and for a row's hover tip.</summary>
    private static string IdentityOf(HunterStat s) => s switch
    {
        HunterStat.AttackPower => "Your basic attack — the plain swing between skills.",
        HunterStat.ResonanceAffinity => "Your skills' damage.",
        HunterStat.CriticalChance => "How often a hit becomes a critical hit.",
        HunterStat.Focus => "How hard a critical hit lands.",
        HunterStat.MaxHealth => "The base of your hunter's life.",
        HunterStat.Defense => "How much smaller every hit you take becomes.",
        HunterStat.Vitality => "Life you regain every second of a fight.",
        HunterStat.Engineering => "How fast you act — swings, skills and animations.",
        _ => "How much a cleared wave pays.",
    };

    // ── LAYOUT. Two columns to the page plus a reset footer, so UI SCALE can shrink the page under
    //    them. Every rectangle here used to be a hand-placed 1920x1080 literal. ───────────────────
    private const int Top = 150;          // under the hint slot's band (canvas y 86-134 stays free)
    private const int BottomMargin = 60;
    private const int ResetH = 84;

    private static Rectangle ResetBar =>
        new(38, UiKit.PageBottom(BottomMargin) - ResetH, UiKit.PageRight(40) - 38, ResetH);

    private static Rectangle InspectorPanel =>
        new(UiKit.PageRight(40) - 496, Top, 496, ResetBar.Y - 16 - Top);

    private static Rectangle ListPanel =>
        new(38, Top, InspectorPanel.X - 20 - 38, ResetBar.Y - 16 - Top);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.TrainingRows => new[] { ListPanel },
        TourTarget.TrainingDetail => new[] { InspectorPanel },
        TourTarget.ResetBar => new[] { ResetBar },
        _ => Array.Empty<Rectangle>(),
    };

    // ── Request/consume, like WarrenScreen: the screen never mutates the Hunter, so the host stays the
    //    only place a currency is spent and the screen stays drawable in a capture with no game state. ──

    /// <summary>The stat a TRAIN button was pressed for this frame, taken once.</summary>
    private HunterStat? _trainRequest;

    public HunterStat? ConsumeTrain()
    {
        var r = _trainRequest;
        _trainRequest = null;
        return r;
    }

    /// <summary>True once, after the armed RESET ALL TRAINING button's confirming second click.</summary>
    /// <remarks>
    /// The host answers it with <c>Hunter.ResetTraining()</c> — which validates again (Core is the
    /// gate), spends the Crystal and zeroes the ranks — no Gleam comes back — then saves.
    /// </remarks>
    public bool ConsumeReset()
    {
        var r = _resetRequest;
        _resetRequest = false;
        return r;
    }

    private bool _resetRequest;
    private bool _resetArmed;   // first click landed; the next click confirms or disarms
    private long _resetArmedAtMs;   // when it landed — the arm expires after ArmSeconds like settings' reset

    /// <summary>How long the armed red state waits for the confirming click before standing down.</summary>
    private const float ArmSeconds = 4f;

    /// <summary>Withdraw the armed reset — navigating away must never leave a live one-click wipe.</summary>
    public void CancelConfirm() => _resetArmed = false;

    /// <summary>
    /// DEV ONLY: <c>RH_SHOT_ARM=reset</c> poses the armed red are-you-sure state for a capture.
    /// </summary>
    /// <remarks>
    /// Read once, and only while <c>RH_SHOT</c> itself is set, so a normal run never looks at it —
    /// the same pattern as ForgeScreen's <c>RH_SHOT_HOVER</c>. It sets draw-state only; no Hunter is
    /// touched, and no confirming click can come from a headless capture.
    /// </remarks>
    private bool _devArmPending =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_ARM") == "reset";

    /// <summary>DEV: pose the inspector on a named stat for a capture (RH_SHOT_SELECT).</summary>
    public void DevSelect(string word)
    {
        foreach (var s in Order)
            if (string.Equals(WordFor(s), word, StringComparison.OrdinalIgnoreCase)) { _selected = s; return; }
    }

    /// <summary>The row the inspector is reading. Set by a click, by the keys, or by the first draw.</summary>
    private HunterStat? _selected;

    /// <summary>
    /// The keyboard path: up and down move the selection, Enter buys, Escape withdraws the armed reset.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT hit-test the mouse — only Draw does, and check_mouse_space.py's entry-point
    /// set stays as it is.
    /// </remarks>
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        var order = Order;
        var at = _selected is { } s ? Math.Max(0, Array.IndexOf(order, s)) : 0;

        bool Pressed(Keys k) => keys.IsKeyDown(k) && !prev.IsKeyDown(k);
        if (Pressed(Keys.Down)) _selected = order[(at + 1) % order.Length];
        if (Pressed(Keys.Up)) _selected = order[(at - 1 + order.Length) % order.Length];
        if (Pressed(Keys.Enter) && _selected is { } pick && hunter.CanTrain(pick)) _trainRequest = pick;
        if (Pressed(Keys.Escape)) CancelConfirm();
    }

    /// <summary>The nine stats in the order the list draws them.</summary>
    private static HunterStat[] Order => Groups.SelectMany(g => g.Stats).ToArray();

    public void Draw(SpriteBatch b, Point mouse, Hunter hunter, bool clicked = false)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        // INVERT THE OVERLAY INSET BEFORE ANY HIT-TEST. Every rect on this screen is authored in
        // 1920x1080 and drawn through Game1.BeginOverlayCanvas; the cursor arrives in 480x270 canvas
        // space. check_mouse_space.py enforces it.
        var hit = Game1.ToOverlay(mouse);
        _tip = null;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        // TRAINING, not STATS: this screen is a purchase, and the subtitle that used to sit under it
        // repeated the Gleam pill two inches above it. The exact figure moves into the list header,
        // where it stands beside the prices it is compared against.
        _ui.TextCenterBig(b, "TRAINING", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        // ONE build, resolved ONCE, and every row reads from it — the same composition the fight uses
        // (Build.Resolve folds keystones, worn gear, trained stats and the passive tree together).
        var build = Loadout.ToBuild(Tree, Mastery, Character, SkillLevels);
        var mods = build.Resolve(hunter);
        var shape = build.Shape;

        // The panel is never empty (§82): it opens on the first thing you could actually buy.
        _selected ??= Array.Find(Order, s => hunter.CanTrain(s));
        _selected ??= HunterStat.AttackPower;

        DrawList(b, hunter, build, mods, shape, hit, clicked);
        DrawInspector(b, hunter, build, mods, shape, hit, clicked);
        DrawReset(b, hunter, hit, clicked);
        if (DevStatsDebug) DrawDebug(b);
        if (_tip is { } tip) _ui.HoverTip(b, tip, hit);
    }

    /// <summary>Every trainable stat, grouped by what it changes, each row saying NOW and AFTER.</summary>
    private void DrawList(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                          Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, ListPanel);
        var left = UiKit.ContentLeft(ListPanel);
        var right = UiKit.ContentRight(ListPanel);

        // THE HEADER LINE — what you have to spend, beside the prices, at the rung a number that
        // matters is set in. Not gold: a wallet is neither earned, selected nor active.
        var hy = ListPanel.Y + UiTypography.PanelTitleTop;
        _ui.TextBig(b, $"{hunter.TotalTrainedRanks} RANKS TRAINED  ·  HUNTER LEVEL {hunter.HunterLevel}",
                    left, hy + 6, Slate, UiTypography.Body);
        _ui.TextRightBig(b, $"GLEAM  {hunter.Gleam:N0}", right, hy, Bone, UiTypography.Headline);
        // LEVEL is a medal, not a stat — the honest answer stays, as a tip rather than a panel.
        if (new Rectangle(left, hy, 460, UiTypography.Pitch(UiTypography.Body)).Contains(hit))
            _tip = "HUNTER LEVEL IS ONE PER FIVE RANKS TRAINED. IT DOES NOT CHANGE THE FIGHT — IT IS A MEDAL, NOT A STAT.";
        var ruleY = hy + UiTypography.Pitch(UiTypography.Body) + 8;
        _ui.Fill(b, new Rectangle(left, ruleY, right - left, 1), Dim);

        var y = RowsTop;
        var first = true;
        foreach (var (caption, stats) in Groups)
        {
            if (!first) y += GroupGap;
            first = false;
            if (TightRows)
            {
                _ui.Fill(b, new Rectangle(left, y + 4, right - left, 1), Dim);
                y += 10;
            }
            else
            {
                _ui.TextBig(b, caption, left, y, Slate, UiTypography.Secondary);
                _ui.Fill(b, new Rectangle(left, y + UiTypography.Pitch(UiTypography.Secondary) - 2, right - left, 1), Dim);
                y += CaptionH;
            }

            foreach (var stat in stats)
            {
                DrawRow(b, hunter, build, mods, shape, stat, new Rectangle(left, y, right - left, RowH), hit, clicked);
                y += RowPitch;
            }
        }
    }

    // ── ROW METRICS, DERIVED. ────────────────────────────────────────────────────────────────
    //
    // Written as fixed pixels the list overflowed its own panel at UI SCALE 125 %: nine rows plus
    // four captions need 660 px and the page leaves 447, so the last two groups drew over the reset
    // footer, and the value column ran under the TRAIN button because its offset was written for a
    // 1246 px row and the row was 862. Height comes from the space, the columns are fractions of the
    // row, and when even the tightest row does not fit, the captions give way to a hairline — the
    // grouping survives as spacing, which is what it was doing anyway.
    private const int GroupGap = 12, IconCol = 6, NameCol = 54, BtnH = 44;   // ui-size-ok: control sizes

    private static int RowsTop =>
        ListPanel.Y + UiTypography.PanelTitleTop + UiTypography.Pitch(UiTypography.Body) + 24;

    private static int RowsAvail => UiKit.ContentBottom(ListPanel) - RowsTop;

    private static int CaptionH => UiTypography.Pitch(UiTypography.Secondary) + 8;

    /// <summary>True when the captions do not fit and the groups are marked by a rule instead.</summary>
    private static bool TightRows =>
        RowsAvail < 9 * 40 + Groups.Length * CaptionH + (Groups.Length - 1) * GroupGap;

    private static int RowPitch => Math.Clamp(
        (RowsAvail - (TightRows ? Groups.Length * 10 : Groups.Length * CaptionH)
                   - (Groups.Length - 1) * GroupGap) / 9,
        36, 56);

    private static int RowH => RowPitch - 4;

    private static int RowW => UiKit.ContentRight(ListPanel) - UiKit.ContentLeft(ListPanel);
    private static int RankCol => RowW * 20 / 100;
    private static int ValueCol => RowW * 39 / 100;
    private static int RankBarW => RowW * 16 / 100;
    private static int BtnW => Math.Min(224, RowW * 19 / 100);

    private void DrawRow(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                         HunterStat stat, Rectangle row, Point hit, bool clicked)
    {
        var selected = _selected == stat;
        var hot = row.Contains(hit);
        // The 5 px gold left rule IS the selection mark — one accent, one meaning.
        _ui.Plate(b, row, selected ? Gold : null);
        if (hot && !selected)
            _ui.Fill(b, new Rectangle(row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2), Slate * 0.10f);
        if (hot) _tip = IdentityOf(stat);

        var (key, gem) = ArtFor(stat);
        var art = Math.Min(36, row.Height - 8);
        var iconBox = new Rectangle(row.X + IconCol, row.Y + (row.Height - art) / 2, art, art);
        if (!_ui.Icon(b, key, iconBox, Color.White))
            _ui.Diamond(b, new Rectangle(iconBox.X + 6, iconBox.Y + 6, art - 12, art - 12), gem);

        var textY = row.Y + (row.Height - UiTypography.Body) / 2 - 2;
        _ui.TextBig(b, WordFor(stat), row.X + NameCol, textY, Bone, UiTypography.Body);

        // RANK AS A SHAPE as well as a number (§8): sixty ranks is a long way, and a bare "12 / 60"
        // does not say how far.
        _ui.TextBig(b, $"{hunter.RankOf(stat)} / {hunter.StatRankCap}", row.X + RankCol, row.Y + 4,
                    Slate, UiTypography.Secondary);
        var barY = row.Y + row.Height - 14;
        _ui.Fill(b, new Rectangle(row.X + RankCol, barY, RankBarW, 6), Dim);
        _ui.Fill(b, new Rectangle(row.X + RankCol, barY,
                                  (int)(RankBarW * (hunter.RankOf(stat) / (float)hunter.StatRankCap)), 6), Bone);

        // NOW → AFTER, at Headline, because this is the decision. When the two format the same — at
        // the cap, or a gain too small to show — the arrow is dropped: a "96 → 96" promises a change
        // the number does not make.
        var (label, now, after) = Effect(stat, hunter, build, mods, shape);
        var btn = new Rectangle(row.Right - BtnW, row.Y + (row.Height - BtnH) / 2, BtnW, BtnH);
        var x = row.X + ValueCol;
        var vy = row.Y + (row.Height - UiTypography.Headline) / 2 - 2;
        // The value column ends where the button begins — it used to be written as an offset for one
        // page width and ran straight under the button at any other.
        var room = btn.X - 16 - x;
        var head = $"{label} {now}";
        if (after != now)
        {
            var full = $"{head}  →  {after}";
            if (_ui.MeasureBig(full, UiTypography.Headline) <= room)
            {
                _ui.TextBig(b, head, x, vy, Bone, UiTypography.Headline);
                x += _ui.MeasureBig(head, UiTypography.Headline);
                _ui.TextBig(b, "  →  ", x, vy, Slate, UiTypography.Headline);
                x += _ui.MeasureBig("  →  ", UiTypography.Headline);
                _ui.TextBig(b, after, x, vy, Met, UiTypography.Headline);
            }
            else
            {
                // Too narrow for both: the AFTER is the decision, so the label gives way first.
                _ui.TextBig(b, _ui.ShortenBig($"{now}  →  ", room - _ui.MeasureBig(after, UiTypography.Headline),
                                              UiTypography.Headline),
                            x, vy, Bone, UiTypography.Headline);
                _ui.TextRightBig(b, after, btn.X - 16, vy, Met, UiTypography.Headline);
            }
        }
        else
            _ui.TextBig(b, _ui.ShortenBig(head, room, UiTypography.Headline), x, vy, Bone, UiTypography.Headline);

        var maxed = hunter.RankOf(stat) >= hunter.StatRankCap;
        var cost = hunter.NextRankCost(stat);
        var afford = hunter.CanTrain(stat);
        if (_ui.Button(b, btn, maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM",
                       hit, clicked, enabled: afford))
            _trainRequest = stat;
        // A click anywhere else in the row SELECTS it — click selects, the button commits (D5).
        else if (UiKit.ClickedIn(row, hit, clicked) && !btn.Contains(hit))
            _selected = stat;
    }

    // ── THE INSPECTOR — the house grammar (§6). It replaces a 640 px hover document that covered the
    //    rows beside the one it was explaining, and could only be read by holding the pointer still. ──
    private void DrawInspector(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                               Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, InspectorPanel);
        if (_selected is not { } stat) return;

        var left = UiKit.ContentLeft(InspectorPanel);
        var right = UiKit.ContentRight(InspectorPanel);
        var width = right - left;
        var cta = new Rectangle(left, InspectorPanel.Bottom - 92, width, 68);
        var refusalY = cta.Y - 8 - 2 * UiTypography.Pitch(UiTypography.Secondary);
        var lineH = UiTypography.Pitch(UiTypography.Body);
        // THE STATE BLOCK IS ANCHORED and the prose stops above it. Flowed, it walked into the button
        // at 125 %, where the panel is 554 px tall instead of 770 and the prose fills the difference.
        var stateH = UiTypography.Pitch(UiTypography.Secondary) + 8 + lineH
                     + UiTypography.Pitch(UiTypography.PrimaryValue) + lineH * 2;
        var stateTop = refusalY - 8 - stateH;
        var floor = stateTop - 12;
        var y = InspectorPanel.Y + UiTypography.PanelTitleTop;

        void Line(string s, Color ink, int px)
        {
            if (y + UiTypography.Pitch(px) > floor) return;
            _ui.TextBig(b, _ui.ShortenBig(s, width, px), left, y, ink, px);
            y += UiTypography.Pitch(px);
        }

        void Wrap(string s, Color ink, int px, int max)
        {
            foreach (var l in _ui.WrapBig(s, width, px).Take(max))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, left, y, ink, px);
                y += UiTypography.Pitch(px);
            }
        }

        void Rule()
        {
            if (y + 12 > floor) return;
            _ui.Fill(b, new Rectangle(left, y + 4, width, 1), Dim);
            y += 14;
        }

        void Pair(string k, string v, Color ink)
        {
            if (y + lineH > floor) return;
            _ui.TextBig(b, k, left, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, v, right, y, ink, UiTypography.Body);
            y += lineH;
        }

        Line($"{GroupOf(stat)}  ·  TRAINED STAT", Slate, UiTypography.Secondary);
        Line(WordFor(stat), Bone, UiTypography.Headline);
        Wrap(IdentityOf(stat), Slate, UiTypography.Body, 2);

        Rule();
        Line("WHAT IT DOES", Slate, UiTypography.Secondary);
        foreach (var para in Describe(stat, hunter, build, mods, shape))
        {
            Wrap(para, Bone, UiTypography.Body, 4);
            y += 6;
        }

        // CURRENT STATE — anchored to the foot so the loudest number sits in the same place on every
        // stat, which is what the eye compares between rows.
        var (_, now, after) = Effect(stat, hunter, build, mods, shape);
        var maxed = hunter.RankOf(stat) >= hunter.StatRankCap;
        var cost = hunter.NextRankCost(stat);
        var afford = hunter.CanTrain(stat);

        // The closures stop at the prose's floor; the state block lives BELOW it, so the floor is
        // lifted to the button before the block is drawn — otherwise Pair() politely refuses to draw
        // the very rows the block exists for.
        y = stateTop;
        floor = cta.Y - 8;
        _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
        y += 12;
        _ui.TextBig(b, "CURRENT STATE", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"RANK {hunter.RankOf(stat)} OF {hunter.StatRankCap}", right, y - 4, Bone,
                         UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Secondary) + 4;
        Pair("NOW", now, Bone);
        if (!maxed)
        {
            _ui.TextBig(b, "AFTER ONE RANK", left, y + 6, Slate, UiTypography.Body);
            _ui.TextRightBig(b, after, right, y, Met, UiTypography.PrimaryValue);
            y += UiTypography.Pitch(UiTypography.PrimaryValue);
            Line($"ONE RANK ADDS {hunter.GainPerRank(stat):0.##} {WordFor(stat)}", Slate, UiTypography.Body);
        }

        Pair("WHAT IT COSTS", maxed ? "—" : $"{cost:N0} GLEAM", maxed ? Slate : afford ? Bone : Ember);

        if (!afford)
            _ui.TextBig(b, _ui.ShortenBig(
                            maxed ? $"THIS STAT IS FULLY TRAINED. RANK {hunter.StatRankCap} IS THE MOST IT CAN REACH."
                                  : $"NOT ENOUGH GLEAM — {cost:N0} NEEDED, YOU HAVE {hunter.Gleam:N0}.",
                            width, UiTypography.Secondary),
                        left, refusalY, Ember, UiTypography.Secondary);

        if (_ui.Button(b, cta, maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM",
                       hit, clicked, enabled: afford, afford ? ButtonStyle.Primary : ButtonStyle.Secondary))
            _trainRequest = stat;
    }

    /// <summary>The row's label, its NOW and its AFTER — the AFTER through <see cref="Hunter.Preview{T}"/>.</summary>
    private (string Label, string Now, string After) Effect(HunterStat stat, Hunter hunter, Build build,
                                                            BuildMods mods, SkillShape shape)
    {
        switch (stat)
        {
            case HunterStat.AttackPower:
            {
                var now = SoloBattle.AutoAttackDamage * hunter.AutoDamageMultiplier * mods.Damage;
                var aft = hunter.Preview(stat, h => SoloBattle.AutoAttackDamage * h.AutoDamageMultiplier * build.Resolve(h).Damage);
                return ("BASIC HIT", $"{now:0}", $"{aft:0}");
            }
            case HunterStat.ResonanceAffinity:
            {
                var per = 100f * SkillCatalogue.ResonancePerPoint;
                var now = per * hunter.ValueOf(stat);
                var aft = hunter.Preview(stat, h => per * h.ValueOf(stat));
                return ("SKILL POWER", $"+{now:0}%", $"+{aft:0}%");
            }
            case HunterStat.CriticalChance:
            {
                var now = SoloBattle.CritChance(hunter, shape);
                var aft = hunter.Preview(stat, h => SoloBattle.CritChance(h, shape));
                return ("CRITICAL HITS", $"{100f * now:0.0}%", $"{100f * aft:0.0}%");
            }
            case HunterStat.Focus:
            {
                var now = SoloBattle.CritMultiplier(hunter);
                var aft = hunter.Preview(stat, h => SoloBattle.CritMultiplier(h));
                return ("CRITICAL DAMAGE", $"{100f * now:0}%", $"{100f * aft:0}%");
            }
            case HunterStat.MaxHealth:
            {
                var now = SoloBattle.ChampionHealth(build, hunter);
                var aft = hunter.Preview(stat, h => SoloBattle.ChampionHealth(build, h));
                return ("LIFE", $"{now:N0}", $"{aft:N0}");
            }
            case HunterStat.Defense:
            {
                var k = SoloBattle.DefenseMitigationConstant;
                var now = 100f - 100f * (k / (k + hunter.Defense));
                var aft = hunter.Preview(stat, h => 100f - 100f * (k / (k + h.Defense)));
                return ("DAMAGE TAKEN", $"-{now:0.0}%", $"-{aft:0.0}%");
            }
            case HunterStat.Vitality:
            {
                var now = Regen(hunter, SoloBattle.ChampionHealth(build, hunter));
                var aft = hunter.Preview(stat, h => Regen(h, SoloBattle.ChampionHealth(build, h)));
                return ("LIFE EACH SECOND", $"{now:N0}", $"{aft:N0}");
            }
            case HunterStat.Engineering:
            {
                var now = mods.SkillRate * shape.SkillRate;
                var aft = hunter.Preview(stat, h => build.Resolve(h).SkillRate * shape.SkillRate);
                return ("ACTION SPEED", $"{now:0.00}×", $"{aft:0.00}×");
            }
            default:
            {
                var now = 100f * (mods.Haul - 1f);
                var aft = hunter.Preview(stat, h => 100f * (build.Resolve(h).Haul - 1f));
                return ("LOOT", $"+{now:0}%", $"+{aft:0}%");
            }
        }
    }

    /// <summary>
    /// Life regained each second — the sim's own guard, verbatim.
    /// </summary>
    /// <remarks>
    /// Zero stays zero: the sim heals nothing at 0 VITALITY, and a card that promised "1 life every
    /// second" to a fresh hunter lied. Max(1, …) is the sim's own floor once the rate is above zero.
    /// </remarks>
    private static int Regen(Hunter h, float pool) =>
        h.RegenPerSecond > 0f ? Math.Max(1, (int)MathF.Round(pool * h.RegenPerSecond)) : 0;

    /// <summary>What a hovered row or the header is saying this frame — drawn last, over everything.</summary>
    private string? _tip;

    /// <summary>
    /// One stat's live headline and its plain-words hover card, with the player's real numbers filled in.
    /// </summary>
    /// <remarks>
    /// Every number is read from the same code path the fight executes — the code-path comments beside
    /// each case name it. Nothing here re-derives a formula; the worst bug this screen can have is
    /// stating a number the simulation does not use.
    /// </remarks>
    private string[] Describe(HunterStat stat, Hunter hunter, Build build, BuildMods mods, SkillShape shape)
    {
        switch (stat)
        {
            case HunterStat.AttackPower:
            {
                // Hunter.AutoDamageMultiplier = weapon × (1 + 0.010 × MIGHT); the basic attack's raw damage
                // is AutoAttackDamage × that, then the fight's shared multiplier (mods.Damage: worn mods,
                // keystones, tree) like every hit. Skills never read MIGHT (2026-08-26).
                var v = hunter.ValueOf(stat);
                var swing = SoloBattle.AutoAttackDamage * hunter.AutoDamageMultiplier * mods.Damage;
                return new[]
                {
                    "MIGHT is your basic attack — the plain swing between skills. Every point of MIGHT adds 1% to it. Skills do not read MIGHT; they read RESONANCE.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} MIGHT. You have {v:0} MIGHT, so your MIGHT alone gives the swing +{v:0}%.",
                    $"With your weapon, keystones and tree counted in, one basic attack hits for {swing:0}. This is the number the fight uses.",
                };
            }
            case HunterStat.ResonanceAffinity:
            {
                // SkillCatalogue.PoweredBase: a skill's base = BasePower × (1 + ResonancePerPoint × R),
                // read by SoloBattle for every skill hit.
                var v = hunter.ValueOf(stat);
                var per = 100f * SkillCatalogue.ResonancePerPoint;
                return new[]
                {
                    $"RESONANCE is your skills' damage. Every point adds {per:0.0}% to a skill's base power, before anything else multiplies it. The basic attack does not read it; it reads MIGHT.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} RESONANCE. You have {v:0} RESONANCE.",
                    $"Your skills now start +{per * v:0}% above their base power.",
                };
            }
            case HunterStat.Engineering:
            {
                // Hunter.SquadSkillRate = worn focus × worn mods × (1 + 0.006 × TEMPO); the fight divides
                // every skill's waiting time, the basic attack's cadence AND every action's animation
                // by mods.SkillRate × shape.SkillRate (SoloBattle cooldowns and the beat, SoloBattle.BeatFor).
                var rate = mods.SkillRate * shape.SkillRate;
                var v = hunter.ValueOf(stat);
                return new[]
                {
                    "TEMPO is your speed: the basic attack swings sooner, skills come back sooner, and every animation plays faster. Every point makes it 0.6% quicker.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} TEMPO. You have {v:0} TEMPO.",
                    $"With your gear and your build counted in, you act {rate:0.00}× as fast. This is the number the fight uses.",
                };
            }
            case HunterStat.Vitality:
            {
                // SoloBattle: every second of a fight the champion regains MaxHealth × Hunter.RegenPerSecond
                // (= VITALITY × RegenPerVitalityPoint). Since 2026-08-26 VITALITY is regeneration, not a
                // second multiplier on the pool.
                var v = hunter.ValueOf(stat);
                var pool = SoloBattle.ChampionHealth(build, hunter);
                // Zero stays zero: the sim heals nothing at 0 VITALITY, and a card that promised "1 life
                // every second" to a fresh hunter lied (review 2026-08-26). Max(1, …) is the sim's own
                // floor once the rate is above zero.
                var perSecond = hunter.RegenPerSecond > 0f ? Math.Max(1, (int)MathF.Round(pool * hunter.RegenPerSecond)) : 0;
                var pct = 100f * hunter.RegenPerSecond;
                var perPoint = 100f * hunter.RegenPerVitalityPoint;
                return new[]
                {
                    "VITALITY heals your hunter a little every second of a fight, even while it is being hit.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} VITALITY. You have {v:0} VITALITY, and every point regains {perPoint:0.00}% of your life each second — {pct:0.00}% in total.",
                    perSecond > 0
                        ? $"With {pool:N0} life, that is {perSecond:N0} life back every second. It cannot take you past full."
                        : "With no VITALITY trained, nothing comes back on its own.",
                };
            }
            case HunterStat.MaxHealth:
            {
                // SoloBattle.ChampionHealth: the pool a fight starts with = max(60, Hunter.MaxHealth)
                // × the build's health multiplier (vows and passives) × mods.Health (vitality, gear).
                var pool = SoloBattle.ChampionHealth(build, hunter);
                return new[]
                {
                    $"HEALTH is the base of your hunter's life. One rank of training adds {hunter.GainPerRank(stat):0} health.",
                    $"With your charm counted in, your base health is {hunter.MaxHealth:N0}. After your gear, your vows and your passives, your hunter starts a fight with {pool:N0} life — the same number the fight screen shows.",
                    "When it reaches zero, the descent ends.",
                };
            }
            case HunterStat.Defense:
            {
                // SoloBattle: taken × 100 ÷ (100 + DEFENSE) — a curve that never reaches 100%.
                var k = SoloBattle.DefenseMitigationConstant;
                var less = 100f - 100f * (k / (k + hunter.Defense));
                return new[]
                {
                    "DEFENSE shrinks every hit you take. The rule: damage is multiplied by 100, then divided by 100 plus your DEFENSE.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} DEFENSE. With your gear you have {hunter.Defense} DEFENSE, so you take {less:0.0}% less damage.",
                    "Each new point helps a little less than the last, and it can never reach 100%.",
                };
            }
            case HunterStat.CriticalChance:
            {
                // SoloBattle.CritChance: trained + gear affixes + tree nodes, capped at 75%.
                var p = SoloBattle.CritChance(hunter, shape);
                return new[]
                {
                    $"CRITICAL is your chance that a hit becomes a critical hit. One rank of training adds {hunter.GainPerRank(stat):0.00}% chance.",
                    $"With your gear and tree counted in, {100f * p:0.0}% of your hits are critical right now.",
                    $"The most it can ever be is {100f * SoloBattle.MaxCritChance:0}%. FOCUS decides how hard a critical hit lands.",
                };
            }
            case HunterStat.Focus:
            {
                // SoloBattle.CritMultiplier: 1.5 + 0.01 × FOCUS — FOCUS is the crit-DAMAGE stat.
                var m = SoloBattle.CritMultiplier(hunter);
                var v = hunter.ValueOf(stat);
                return new[]
                {
                    $"FOCUS makes your critical hits land harder. A critical hit deals {100f * SoloBattle.CritBaseMultiplier:0}% damage, plus 1% for every point of FOCUS.",
                    $"One rank of training adds {hunter.GainPerRank(stat):0} FOCUS. You have {v:0} FOCUS.",
                    $"Your critical hits now deal {100f * m:0}% damage. CRITICAL decides how often they happen.",
                };
            }
            default:   // HunterStat.Guile
            {
                // Hunter.HaulMultiplier = (1 + 0.010 × GUILE) × worn mods; SoloExpedition multiplies
                // every wave's Gleam payout by mods.Haul.
                var v = hunter.ValueOf(HunterStat.Guile);
                var pct = 100f * (mods.Haul - 1f);
                var sign = pct >= 0f ? "+" : "";
                return new[]
                {
                    "GUILE raises what a fight pays. Every point adds 1% to the Gleam every cleared wave hands over.",
                    $"One rank of training adds {hunter.GainPerRank(HunterStat.Guile):0} GUILE. You have {v:0} GUILE.",
                    $"With gear and keystones counted in, your waves now pay {sign}{pct:0}% loot.",
                };
            }
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
        var maxed = hunter.RankOf(stat) >= hunter.StatRankCap;
        var cost = hunter.NextRankCost(stat);
        var rect = new Rectangle(x, y, 176, 44);
        var afford = hunter.CanTrain(stat);

        var label = maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM";

        if (_ui.Button(b, rect, label, mouse, clicked, enabled: afford))
            _trainRequest = stat;
    }

    /// <summary>
    /// RESET ALL TRAINING — the respec footer. Costs one Crystal, and returns NO Gleam.
    /// </summary>
    /// <remarks>
    /// The no-refund warning is the decision, so it is drawn at the rung a decision is drawn at. It
    /// used to be the smallest type on the page, under a Body label — and the player reads the price
    /// BEFORE arming, not after. Two clicks like START A NEW GAME: first arms red, second confirms,
    /// any other click disarms, and the arm expires by itself.
    ///
    /// This bar is also the only place in the game a Crystal count is visible outside a pill's hover.
    /// </remarks>
    private void DrawReset(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        if (_devArmPending) { _resetArmed = true; _resetArmedAtMs = Environment.TickCount64; _devArmPending = false; }

        _ui.Plate(b, ResetBar);

        var ranks = hunter.TotalTrainedRanks;
        var crystals = hunter.MaterialOf(Material.Crystal);
        var price = hunter.TrainingResetCrystalCost;

        // THE BUTTON IS BUTTON-SIZED. The ornate button art is a whole-image stretch, so a 960 px one
        // smeared its corner scrollwork into a streak; the explanation lives in text beside it.
        var button = new Rectangle(ResetBar.Right - 24 - 360, ResetBar.Y + 18, 360, 48);
        var tx = ResetBar.X + 24;
        var textW = button.X - 24 - tx;
        var lineY = ResetBar.Y + 12 + UiTypography.Pitch(UiTypography.Body);

        if (_resetArmed && Environment.TickCount64 - _resetArmedAtMs > (long)(ArmSeconds * 1000))
            _resetArmed = false;   // the settings pattern this mirrors auto-disarms; so does this
        if (_resetArmed)
        {
            _ui.TextBig(b, $"SURE? ALL {ranks} RANKS GO BACK TO ZERO.", tx, ResetBar.Y + 12,
                        Color.White, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig("THE GLEAM YOU SPENT DOES NOT COME BACK. CLICK THE RED BUTTON AGAIN TO DO IT.",
                                          textW, UiTypography.Body),
                        tx, lineY, Ember, UiTypography.Body);
            _ui.Fill(b, button, ArmedRed);
            _ui.Fill(b, new Rectangle(button.X, button.Y, button.Width, 3), Ember);
            _ui.TextCenterBig(b, "YES, RESET — NO GLEAM BACK", button.Center.X, button.Center.Y - 10,
                              Color.White, UiTypography.Body, TextFace.Strong);
            if (UiKit.ClickedIn(button, hit, clicked))
            {
                _resetArmed = false;
                _resetRequest = true;
            }
            else if (clicked) _resetArmed = false;   // any click that is not the confirmation disarms
            return;
        }

        _ui.TextBig(b, "RESET ALL TRAINING", tx, ResetBar.Y + 12, Bone, UiTypography.Body);
        var cost = $"{ranks} RANKS GO BACK TO ZERO. IT COSTS {price:N0} CRYSTAL — YOU HAVE {crystals:N0}. THE GLEAM YOU SPENT DOES NOT COME BACK.";
        if (ranks <= 0)
        {
            _ui.TextBig(b, "NOTHING TO RESET — YOU HAVE NOT TRAINED ANYTHING YET.", tx, lineY,
                        Slate, UiTypography.Body);
            _ui.Button(b, button, "RESET", hit, clicked, enabled: false);
        }
        else if (crystals < price)
        {
            _ui.TextBig(b, _ui.ShortenBig(cost, textW, UiTypography.Body), tx, lineY, Bone, UiTypography.Body);
            _ui.Button(b, button, $"NEEDS {price:N0} CRYSTAL", hit, clicked, enabled: false);
        }
        else
        {
            _ui.TextBig(b, _ui.ShortenBig(cost, textW, UiTypography.Body), tx, lineY, Bone, UiTypography.Body);
            if (_ui.Button(b, button, $"RESET — {price:N0} CRYSTAL", hit, clicked))
            {
                _resetArmed = true;
                _resetArmedAtMs = Environment.TickCount64;
            }
        }
    }

    // DrawProgression, LevelCard, SetHover and DrawHoverCard are gone with UX V2 P2.3.
    //
    // The PROGRESS panel showed HIGHEST WAVE, CHESTS OPENED, MASTERY POINTS and HUNTER LEVEL — the
    // Map's number, the Vault's, the Mastery tree's, and one this screen already printed elsewhere.
    // None of the four moves when you train, which is the only question this screen exists to answer.
    // LEVEL's honest "it is a medal, not a stat" answer survives as the header's hover tip.
    //
    // The 640 px hover document is replaced by the inspector: it covered the rows beside the one it
    // explained, and it could only be read by holding the pointer perfectly still.

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { ListPanel, InspectorPanel, ResetBar })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, "nav TRAINING  grouped list + inspector + respec", 60, 112, Gold, UiTypography.Secondary);
    }

}
