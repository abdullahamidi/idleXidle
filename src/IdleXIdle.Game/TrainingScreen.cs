using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>The keystones the world has taught this account - set by the host.</summary>
    /// <remarks>
    /// This screen resolves ONE build and every stat row reads from it, so it needs the same two
    /// catalogues the fight does. Keystone and Vow knowledge left the trait tree for the world, and a
    /// screen still composing from the tree would state a stat line no fight would ever produce.
    /// </remarks>
    public IReadOnlyList<Keystone> DiscoveredKeystones { get; set; } = Array.Empty<Keystone>();

    /// <summary>The Vows the account has found - set by the host.</summary>
    public IReadOnlyList<Vow> KnownVows { get; set; } = Array.Empty<Vow>();

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
    public static string WordFor(HunterStat s) => HunterStatWords.Word(s);

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

    /// <summary>The nine stats in the order the list draws them.</summary>
    private static readonly HunterStat[] Order = Groups.SelectMany(g => g.Stats).ToArray();

    /// <summary>
    /// One thing the list draws, top to bottom: a group's caption (<see cref="Stat"/> is -1) or one of its
    /// rows. <see cref="Closes"/> marks a group's last row when another group follows — the group gap
    /// hangs under it, so a caption scrolled to the top of the region starts flush, not a gap lower.
    /// </summary>
    private readonly record struct Entry(int Group, int Stat, bool Closes);

    /// <summary>The list's entries — four captions and nine rows, in draw order. Built once.</summary>
    private static readonly Entry[] Entries = BuildEntries();

    private static Entry[] BuildEntries()
    {
        var list = new List<Entry>();
        for (var g = 0; g < Groups.Length; g++)
        {
            list.Add(new Entry(g, -1, false));
            for (var k = 0; k < Groups[g].Stats.Length; k++)
                list.Add(new Entry(g, k, k == Groups[g].Stats.Length - 1 && g < Groups.Length - 1));
        }
        return list.ToArray();
    }

    /// <summary>The row's icon and its flat-diamond fallback, if the art ever goes missing.</summary>
    private static (string Key, Color Gem) ArtFor(HunterStat s) => s switch
    {
        HunterStat.AttackPower => ("stat_might", new Color(0xD6, 0x48, 0x5C)),
        HunterStat.ResonanceAffinity => ("stat_resonance", new Color(0x74, 0xC6, 0xE8)),
        HunterStat.Engineering => ("stat_tempo", new Color(0x48, 0xB8, 0x88)),
        HunterStat.Vitality => ("stat_vitality", new Color(0xC0, 0x6E, 0xE0)),
        HunterStat.MaxHealth => ("stat_health", UiInk.Good),
        HunterStat.Defense => ("stat_defense", new Color(0x8A, 0x96, 0xA8)),
        HunterStat.CriticalChance => ("stat_critical", new Color(0xF0, 0xA8, 0x30)),
        HunterStat.Focus => ("stat_focus", new Color(0xE8, 0xC8, 0x7A)),
        _ => ("stat_guile", UiInk.Accent),
    };

    /// <summary>One line saying what the stat is, for the inspector and for a row's hover tip.</summary>
    private static string IdentityOf(HunterStat s) => HunterStatWords.Definition(s);

    // ── LAYOUT. Two columns to the page plus a reset footer. Every rectangle here used to be a
    //    hand-placed 1920x1080 literal; since the UI polish pass (brief §7–§11) every size that is not
    //    a page anchor comes from UiMetrics, so the density profile grows the type, the rows and the
    //    buttons while the page and its margins stay where they are. ────────────────────────────────
    private static int Top => UiKit.PageTop;   // the first row under the chrome band, at this profile
    private const int BottomMargin = 60;  // the page's foot — a page anchor

    /// <summary>
    /// The inspector's width — the house 496 at every profile, deliberately NOT
    /// <see cref="UiMetrics.InspectorWidth"/>. A wider inspector at 150 % would take its width from the list
    /// beside it, whose rows need every pixel to keep NAME · RANK · NOW → AFTER · TRAIN on one line at
    /// Headline 39 (the value column alone wants ~410 px, the button ~280). The inspector's prose scrolls
    /// instead (§18), which costs a reader a wheel notch rather than costing the list its labels.
    /// </summary>
    private const int InspectorW = 496;

    /// <summary>
    /// The reset footer's height: two Body lines under a Space(12) head and over a Space(16) foot, and never
    /// shorter than its button wants — 84 at 100 %.
    /// </summary>
    private static int ResetH => Math.Max(
        UiMetrics.Space(12) + 2 * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(16),
        UiMetrics.ButtonHeight + 2 * UiMetrics.Space(16));

    /// <summary>The gap between the two columns and the footer under them (16 at 100 %).</summary>
    private static int FooterGap => UiMetrics.Space(16);

    /// <summary>The gap between the list and the inspector (20 at 100 %).</summary>
    private static int ColumnGap => UiMetrics.Space(20);

    private static Rectangle ResetBar =>
        new(38, UiKit.PageBottom(BottomMargin) - ResetH, UiKit.PageRight(40) - 38, ResetH);

    private static Rectangle InspectorPanel =>
        new(UiKit.PageRight(40) - InspectorW, Top, InspectorW, ResetBar.Y - FooterGap - Top);

    private static Rectangle ListPanel =>
        new(38, Top, InspectorPanel.X - ColumnGap - 38, ResetBar.Y - FooterGap - Top);

    /// <summary>
    /// The inspector's call to action, anchored to the panel's foot — the ONE rectangle the paint frames
    /// and <see cref="UpdateInspector"/> hit-tests.
    /// </summary>
    private static Rectangle InspectorCta
    {
        get
        {
            var left = UiKit.ContentLeft(InspectorPanel);
            return new Rectangle(left, InspectorPanel.Bottom - UiMetrics.Space(24) - UiMetrics.ButtonHeightPrimary,
                                 UiKit.ContentRight(InspectorPanel) - left, UiMetrics.ButtonHeightPrimary);
        }
    }

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

    /// <summary>
    /// The sound the host should play, read once and cleared — <c>sfx_error</c> the moment a TRAIN is
    /// refused for Gleam (a click on a NEED button, or Enter on a row the purse cannot cover). The host
    /// owns audio; the same shape as <see cref="TraitsScreen.ConsumeCue"/>.
    /// </summary>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    private string? _cue;

    /// <summary>
    /// A NEED button was clicked, or Enter pressed on a row that costs more than the purse holds. The
    /// input half only records it; the next Update turns it into the cue and the pill's one Ember flash.
    /// </summary>
    private bool _refused;

    /// <summary>
    /// What the row and the pill SHOWED the frame a TRAIN was requested — taken by
    /// <see cref="SnapPending"/> at the tail of the Update that asked, and answered in the next one: if
    /// the Hunter's rank grew, the host bought it, and the feedback plays from these values to the
    /// live ones. Shown, not true, values: a second click while the first tick is still moving
    /// continues from where the number is, not from where it was.
    /// </summary>
    private readonly record struct Pending(HunterStat Stat, int Rank, float Gleam, float Now, float After, float RankShown);

    private Pending? _pending;

    /// <summary>The one TRAIN feedback in flight — cost paid → stat highlights → progress animates (§39).</summary>
    private readonly TrainFeedback _fx = new();

    /// <summary>The refusal's pulse: the pill flashes Ember ONCE, keyed to the event, never re-armed by Draw.</summary>
    private static readonly int RefuseKey = HashCode.Combine("training-refused", 0);

    /// <summary>How much of the refusal flash is left this frame, 1 → 0. Read in Update, drawn in Draw.</summary>
    private float _refuseGlow;

    /// <summary>
    /// The GLEAM figure the pill SHOWS — mid-tick, the number a snapshot continues from.
    /// </summary>
    /// <remarks>
    /// A read, not a field the paint fills in. The snapshot is taken in <see cref="Update"/> now, where
    /// the paint's leftovers are not available, and one expression cannot disagree with itself.
    /// </remarks>
    private float GleamShown(Hunter hunter) =>
        _fx.Live ? TrainFeedback.Mix(_fx.GleamFrom, hunter.Gleam, _fx.Progress) : hunter.Gleam;

    /// <summary>The salt a row's hover ease is keyed under — by stat, so a scroll does not restart the fade.</summary>
    private const int RowHoverSalt = 0x7A11;

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

    /// <summary>
    /// DEV ONLY: <c>RH_SHOT_TRAIN=&lt;word&gt;</c> (MIGHT, TEMPO …) poses the feedback of a TRAIN just
    /// bought, held at one instant, so the frame-60 shutter can photograph a 180 ms event.
    /// </summary>
    /// <remarks>
    /// The pose goes through the REAL path. The screen selects the row and requests the train exactly as
    /// a click would; the HOST spends the Gleam (this screen never touches the Hunter); the next Update
    /// sees the rank grow and starts the feedback — then holds it at <c>RH_SHOT_TRAIN_T</c> (0..1 through
    /// the Transition; 0.5 by default: numbers half-ticked, bar half-way, flash at half strength) instead
    /// of reading the clock. A row the purse cannot afford poses the REFUSAL instead: the pill's Ember
    /// flash, the moment sfx_error is cued. <c>RH_SHOT_HOLD=1</c> holds the left mouse button for the
    /// capture, so RH_SHOT_PAGE_MOUSE over a row or a button poses its PRESSED state. All read once, and
    /// only while <c>RH_SHOT</c> itself is set, so a normal run never looks at them.
    /// </remarks>
    private string? _devTrainPending =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
            ? Environment.GetEnvironmentVariable("RH_SHOT_TRAIN") : null;

    /// <summary>DEV: the phase the posed feedback is held at — null outside a posed capture.</summary>
    private readonly float? _devPhase =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_TRAIN") is { Length: > 0 }
            ? float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_TRAIN_T"), NumberStyles.Float,
                             CultureInfo.InvariantCulture, out var t) ? Math.Clamp(t, 0f, 1f) : 0.5f
            : null;

    /// <summary>DEV: hold the left mouse button, so a PRESSED state can be photographed.</summary>
    private readonly bool _devHold =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_HOLD") == "1";

    /// <summary>DEV: the posed refusal stays lit at the phase — its pulse alone would be over by frame 11.</summary>
    private bool _devRefuseLit;

    /// <summary>DEV: pose the inspector on a named stat for a capture (RH_SHOT_SELECT).</summary>
    public void DevSelect(string word)
    {
        foreach (var s in Order)
            if (string.Equals(WordFor(s), word, StringComparison.OrdinalIgnoreCase)) { _selected = s; _revealSelected = true; return; }
    }

    /// <summary>The row the inspector is reading. Set by a click, by the keys, or by the first draw.</summary>
    private HunterStat? _selected;

    /// <summary>
    /// Set when the selection moved by a path other than a click (the keys, the first draw, a capture's
    /// RH_SHOT_SELECT), so a scrolled list brings the selected row into view on the next Update.
    /// </summary>
    private bool _revealSelected;

    /// <summary>
    /// EVERY input this screen takes: the keyboard — up and down move the selection, Enter buys, Escape
    /// withdraws the armed reset — and then every click, every wheel notch and every scroll clamp, in the
    /// order the three paint halves used to resolve them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DRAW MUST NOT CONSUME INPUT. MonoGame's fixed timestep makes at least one call to Update and
    /// exactly one to Draw per tick, and the host recomputes the click edge at the top of every Update —
    /// so on a frame over budget the second Update erases the edge and the single Draw that follows
    /// hit-tests nothing. A TRAIN that spends Gleam, a row select, a wheel notch and the irreversible
    /// RESET were all resolved inside <see cref="Draw"/> until this pass, and every one of them was
    /// silently dropped on a slow frame.
    /// </para>
    /// <para>
    /// They are resolved here now, against the SAME rectangles the paint uses — <see cref="LayoutList"/>,
    /// <see cref="BuyRect"/>, <see cref="InspectorCta"/>, <see cref="ResetButton"/> — so what is drawn is
    /// what is hit-tested. Nothing about the gating changed: the <paramref name="clicked"/> edge arrives
    /// already narrowed by the host, and is passed through exactly as given.
    /// </para>
    /// </remarks>
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        DevPose(hunter);
        var order = Order;
        var at = _selected is { } s ? Math.Max(0, Array.IndexOf(order, s)) : 0;

        bool Pressed(Keys k) => keys.IsKeyDown(k) && !prev.IsKeyDown(k);
        if (Pressed(Keys.Down)) { _selected = order[(at + 1) % order.Length]; _revealSelected = true; }
        if (Pressed(Keys.Up)) { _selected = order[(at - 1 + order.Length) % order.Length]; _revealSelected = true; }
        if (Pressed(Keys.Enter) && _selected is { } pick)
        {
            if (hunter.CanTrain(pick)) _trainRequest = pick;
            // The same refusal a click on NEED gets. A maxed row is not refused — nothing was asked for.
            else if (hunter.RankOf(pick) < hunter.StatRankCap) _refused = true;
        }
        if (Pressed(Keys.Escape)) CancelConfirm();

        // ── THE FEEDBACK, advanced here with the host's dt — never from Draw. ────────────────────
        // A request is answered one frame later: this half asked, the host spent (or did not), and the
        // rank is the proof. Every from-value was photographed by SnapPending at the moment of the ask;
        // every to-value is the live one the rows read anyway, so the tick cannot end anywhere but on
        // the truth.
        //
        // `_trainRequest is null` ASKS WHETHER THE HOST HAS HAD ITS TURN. A frame over budget runs
        // Update twice, and the second one would otherwise consume the snapshot the first had just
        // taken — reading a rank the host has not been asked for yet, finding it unchanged, and
        // throwing the feedback away. The purchase would still go through; only its number-tick and
        // flash would vanish. On an ordinary tick the request is always null here, because the host's
        // ConsumeTrain runs after Draw.
        if (_trainRequest is null && _pending is { } p)
        {
            _pending = null;
            if (hunter.RankOf(p.Stat) > p.Rank) _fx.Start(p.Stat, p.Gleam, p.Now, p.After, p.RankShown);
        }
        if (_refused)
        {
            _refused = false;
            _cue = "sfx_error";
            UiMotion.Flash(RefuseKey, UiMotion.Transition);
            if (_devPhase is not null) _devRefuseLit = true;
        }
        _fx.Advance(_devPhase);
        _refuseGlow = _devPhase is { } phase && _devRefuseLit ? 1f - phase : UiMotion.Pulse(RefuseKey);

        // ── THE MOUSE, in the order the three paint halves used to take it. ──────────────────────
        // The panel is never empty (§82): it opens on the first thing you could actually buy. Settled
        // here rather than in the paint, because a selection is state and a paint may not change state.
        if (_selected is null) { _revealSelected = true; _selected = DefaultSelection(hunter); }
        UpdateList(mouse, wheel, clicked, hunter);
        UpdateInspector(mouse, wheel, clicked, hunter);
        UpdateReset(mouse, clicked, hunter);
        // LAST, once, for whichever of the four paths asked for a rank: a click on a row's TRAIN, the
        // inspector's call to action, Enter, or a posed capture.
        SnapPending(hunter);
    }

    /// <summary>The row the panel opens on when nothing has been picked: the first thing the purse can buy.</summary>
    /// <remarks>
    /// <see cref="Array.Find{T}(T[], Predicate{T})"/> answers <c>default(HunterStat)</c> — AttackPower,
    /// ordinal 0 — when nothing is affordable, which is why the old third <c>??= AttackPower</c> line
    /// under it could never fire. Same answer, one step.
    /// </remarks>
    private static HunterStat DefaultSelection(Hunter hunter) => Array.Find(Order, s => hunter.CanTrain(s));

    /// <summary>
    /// The stat the inspector is reading — the one Update settled, or the default until it has. ONE
    /// expression, so the paint and the hit test can never be looking at different rows.
    /// </summary>
    private HunterStat Selection(Hunter hunter) => _selected ?? DefaultSelection(hunter);

    /// <summary>
    /// THE LIST'S INPUT: the wheel over the panel, the keyboard reveal, the scroll clamp, and a click on
    /// a row — its TRAIN button, its NEED refusal, or its plate.
    /// </summary>
    private void UpdateList(Point hit, int wheel, bool clicked, Hunter hunter)
    {
        // THE ROWS SCROLL at the profiles the panel cannot hold them all — 125 % and 150 % (brief §9,
        // §17) — and a keyboard move brings its row into view. The clamp lives here, not in the paint:
        // three paints with no Update between them must leave the offset exactly where they found it.
        if (ListScrolls)
        {
            if (wheel != 0 && ListPanel.Contains(hit)) _listFirst = Math.Clamp(_listFirst - wheel, 0, ListMaxFirst);
            if (_revealSelected) RevealRow(Selection(hunter));
            _listFirst = Math.Clamp(_listFirst, 0, ListMaxFirst);
        }
        else _listFirst = 0;
        _revealSelected = false;

        if (!clicked) return;
        // THE SAME LAYOUT THE PAINT WALKS — one solve, two callers, so a row cannot be drawn in one
        // place and hit-tested in another.
        var laid = LayoutList(ListFirst);
        for (var n = 0; n < laid.Count; n++)
        {
            var (i, _, row) = laid[n];
            if (Entries[i].Stat < 0) continue;
            var stat = Groups[Entries[i].Group].Stats[Entries[i].Stat];
            if (BuyRect(row).Contains(hit))
            {
                if (hunter.CanTrain(stat)) _trainRequest = stat;
                // A click on NEED is a refusal: felt at the pill, heard as sfx_error — never silent
                // (§29). A MAXED row is not refused — nothing was asked for.
                else if (hunter.RankOf(stat) < hunter.StatRankCap) _refused = true;
                return;
            }
            // A click anywhere else in the row SELECTS it — click selects, the button commits (D5).
            if (row.Contains(hit)) { _selected = stat; return; }
        }
    }

    /// <summary>
    /// THE INSPECTOR'S INPUT: the prose wheel, its reset on a new stat, its clamp, and the call to
    /// action's train-or-refuse.
    /// </summary>
    private void UpdateInspector(Point hit, int wheel, bool clicked, Hunter hunter)
    {
        var stat = Selection(hunter);
        // A NEW STAT STARTS AT THE TOP of its own prose.
        if (_inspectorFor != stat) { _inspectorFor = stat; _inspectorFirst = 0; }

        // THE PROSE SCROLLS on the wheel over the panel. How far it CAN scroll is a text measurement —
        // how many wrapped lines the paint fitted between the header and the state block — so no
        // rectangle derives it and only the paint can know it (see <see cref="_proseReach"/>). The notch
        // and the clamp are spent here; the paint clamps its OWN local first line against the reach it
        // has just measured, so an offset out of range is never drawn.
        if (wheel != 0 && InspectorPanel.Contains(hit)) _inspectorFirst -= wheel;
        _inspectorFirst = Math.Clamp(_inspectorFirst, 0, _proseReach);

        // The same predicate UiKit.Button answered with: enabled, over the rect, on the edge.
        if (!UiKit.ClickedIn(InspectorCta, hit, clicked)) return;
        if (hunter.CanTrain(stat)) _trainRequest = stat;
        else if (hunter.RankOf(stat) < hunter.StatRankCap) _refused = true;
    }

    /// <summary>
    /// THE RESET FOOTER'S INPUT: the dev pose, the arm standing itself down, the arming first press and
    /// the confirming second one.
    /// </summary>
    /// <remarks>
    /// The two presses are preserved exactly — the arming sits in the <c>else</c> of the armed branch, so
    /// one edge can still only ever do one of the two, and any click that is not the confirmation stands
    /// the reset down. The button fires only in the one branch the paint draws it live in: something to
    /// reset, and the Crystal to pay for it. The other two branches draw it off, and a button drawn off
    /// never returned true.
    /// </remarks>
    private void UpdateReset(Point hit, bool clicked, Hunter hunter)
    {
        if (_devArmPending) { _resetArmed = true; _resetArmedAtMs = Environment.TickCount64; _devArmPending = false; }
        if (!ResetArmedNow) _resetArmed = false;   // the settings pattern this mirrors auto-disarms; so does this

        if (_resetArmed)
        {
            if (UiKit.ClickedIn(ResetButton, hit, clicked))
            {
                _resetArmed = false;
                _resetRequest = true;
            }
            else if (clicked) _resetArmed = false;   // any click that is not the confirmation disarms
            return;
        }

        if (hunter.TotalTrainedRanks > 0
            && hunter.MaterialOf(Material.Crystal) >= hunter.TrainingResetCrystalCost
            && UiKit.ClickedIn(ResetButton, hit, clicked))
        {
            _resetArmed = true;
            _resetArmedAtMs = Environment.TickCount64;
        }
    }

    /// <summary>
    /// The from-values a TRAIN's feedback plays out of, photographed once at the tail of the Update that
    /// asked for it — SHOWN values, so a second rank bought while the first tick is still moving carries
    /// on from where the number is rather than jumping back to where it was.
    /// </summary>
    /// <remarks>
    /// The paint used to take this snapshot twice, in the row and again in the inspector, with identical
    /// formulas — the inspector's copy existed because Enter can ask for a row scrolled out of the list.
    /// One call covers all four paths. The build is resolved here only on a frame a rank is actually
    /// asked for, which is at most one frame per press, and the paint resolves the same build every
    /// frame regardless.
    /// </remarks>
    private void SnapPending(Hunter hunter)
    {
        if (_trainRequest is not { } stat) return;
        var build = Loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows);
        var mods = build.Resolve(hunter);
        var (_, _, nowV, aftV) = Effect(stat, hunter, build, mods, build.Shape);
        var playing = _fx.Playing(stat);
        var rank = hunter.RankOf(stat);
        _pending = new Pending(
            stat, rank, GleamShown(hunter),
            playing ? TrainFeedback.Mix(_fx.NowFrom, nowV, _fx.Progress) : nowV,
            playing ? TrainFeedback.Mix(_fx.AfterFrom, aftV, _fx.Progress) : aftV,
            playing ? TrainFeedback.Mix(_fx.RankFrom, rank, _fx.Progress) : rank);
    }

    /// <summary>DEV: apply the capture dials once — see <see cref="_devTrainPending"/>.</summary>
    private void DevPose(Hunter hunter)
    {
        if (_devHold) UiKit.MouseHeld = true;
        if (_devTrainPending is not { } word) return;
        _devTrainPending = null;
        foreach (var s in Order)
        {
            if (!string.Equals(WordFor(s), word, StringComparison.OrdinalIgnoreCase)) continue;
            _selected = s;
            _revealSelected = true;
            if (hunter.CanTrain(s)) _trainRequest = s;                   // the host spends; Draw snapshots
            else if (hunter.RankOf(s) < hunter.StatRankCap) _refused = true;
            return;
        }
    }

    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        // THE CURSOR ARRIVES IN PAGE SPACE — the space every rect here is authored in — mapped once by
        // the host (Game1.PageCursor through Core's PageFrame). This screen once forgot to invert the
        // old canvas-space cursor and its TRAIN buttons were unreachable; check_mouse_space.py now
        // refuses any screen that converts at all.
        var hit = mouse;
        _tip = null;
        _tipAt = Rectangle.Empty;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0B, 0x09, 0x08, 0xD8));

        // TRAINING, not STATS: this screen is a purchase, and the subtitle that used to sit under it
        // repeated the Gleam pill two inches above it. The exact figure moves into the list header,
        // where it stands beside the prices it is compared against.
        _ui.TextCenterBig(b, "TRAINING", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        // ONE build, resolved ONCE, and every row reads from it — the same composition the fight uses
        // (Build.Resolve folds keystones, worn gear, trained stats and the passive tree together).
        var build = Loadout.ToBuild(Mastery, Character, SkillLevels, DiscoveredKeystones, KnownVows);
        var mods = build.Resolve(hunter);
        var shape = build.Shape;

        DrawList(b, hunter, build, mods, shape, hit);
        DrawInspector(b, hunter, build, mods, shape, hit);
        DrawReset(b, hunter, hit);
        if (DevStatsDebug) DrawDebug(b);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
    }

    /// <summary>Every trainable stat, grouped by what it changes, each row saying NOW and AFTER.</summary>
    private void DrawList(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape, Point hit)
    {
        _ui.PanelQuiet(b, ListPanel);
        var left = UiKit.ContentLeft(ListPanel);
        var right = UiKit.ContentRight(ListPanel);

        // THE HEADER LINE — what you have to spend, beside the prices, at the rung a number that
        // matters is set in. Not gold: a wallet is neither earned, selected nor active. The Body line
        // sits on the Headline figure's baseline — 6 px lower at 100 %, derived from the two rungs.
        var hy = ListPanel.Y + UiTypography.PanelTitleTop;
        var headDrop = (UiTypography.Headline - UiTypography.Body) * 3 / 2;
        _ui.TextBig(b, $"{hunter.TotalTrainedRanks} RANKS TRAINED  ·  HUNTER LEVEL {hunter.HunterLevel}",
                    left, hy + headDrop, Slate, UiTypography.Body);
        // SPENDING REACTS AT THE PILL (§37): on TRAIN the figure ticks down from what it showed to what
        // is left, over the Transition, under one gold flash — and a refused TRAIN flashes it Ember
        // instead, because the purse is the answer to "why not". Each fades once; Reduced Motion keeps
        // the fade and drops the tick.
        var glow = _fx.Live ? _fx.Glow : 0f;
        var purse = _fx.Live ? $"GLEAM  {MathF.Round(GleamShown(hunter)):N0}" : $"GLEAM  {hunter.Gleam:N0}";
        var purseInk = Tint(Bone, glow);
        if (_refuseGlow > 0f) purseInk = Color.Lerp(purseInk, Ember, _refuseGlow);
        if (glow > 0f || _refuseGlow > 0f)
        {
            var w = _ui.MeasureBig(purse, UiTypography.Headline);
            var pad = UiMetrics.Space(8);
            _ui.Fill(b, new Rectangle(right - w - pad, hy - UiMetrics.Space(2), w + 2 * pad, UiTypography.Pitch(UiTypography.Headline)),
                     (_refuseGlow > glow ? Ember : Gold) * (0.16f * MathF.Max(glow, _refuseGlow)));
        }
        _ui.TextRightBig(b, purse, right, hy, purseInk, UiTypography.Headline);
        // LEVEL is a medal, not a stat — the honest answer stays, as a tip rather than a panel. The
        // ROW is remembered beside the words: the tip hangs off the thing it explains, never the cursor.
        var levelRow = new Rectangle(left, hy, UiMetrics.Text(460), UiTypography.Pitch(UiTypography.Body));
        if (levelRow.Contains(hit))
        {
            _tip = "HUNTER LEVEL IS ONE PER FIVE RANKS TRAINED. IT DOES NOT CHANGE THE FIGHT — IT IS A MEDAL, NOT A STAT.";
            _tipAt = levelRow;
        }
        var ruleY = hy + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(8);
        _ui.Fill(b, new Rectangle(left, ruleY, right - left, 1), Dim);

        // THE ROWS. Nine rows and four captions at the profile's pitch. When the panel cannot hold them
        // all — 125 % and 150 % (brief §9, §17) — the region scrolls by the wheel behind a scrollbar in
        // its own lane, and a keyboard move brings its row into view. An entry is drawn whole or not at
        // all: nothing is clipped against the panel's foot, and a caption is never left orphaned at the
        // bottom without the first row it introduces.
        if (ListScrolls)
            _ui.ScrollBar(b, new Rectangle(right - UiMetrics.ScrollbarWidth, RowsTop, UiMetrics.ScrollbarWidth, RowsAvail),
                          ListFirst, Entries.Length - ListMaxFirst, Entries.Length);

        // ONE LAYOUT. The wheel, the keyboard reveal and the clamp are UpdateList's; where the entries
        // land is LayoutList's, and UpdateList walks the very same list to decide what was clicked.
        var laid = LayoutList(ListFirst);
        for (var n = 0; n < laid.Count; n++)
        {
            var (i, y, row) = laid[n];
            var (g, k, _) = Entries[i];
            if (k < 0)
            {
                _ui.TextBig(b, Groups[g].Caption, left, y, Slate, UiTypography.Secondary);
                _ui.Fill(b, new Rectangle(left, y + UiTypography.Pitch(UiTypography.Secondary) - 2, RowW, 1), Dim);
            }
            else
                DrawRow(b, hunter, build, mods, shape, Groups[g].Stats[k], row, hit);
        }
    }

    /// <summary>The first entry the scrolled list draws. Zero whenever the list fits.</summary>
    private int _listFirst;

    /// <summary>The clamped first entry the list is laid out from — the offset BOTH halves read.</summary>
    private int ListFirst => ListScrolls ? Math.Clamp(_listFirst, 0, ListMaxFirst) : 0;

    /// <summary>One drawn entry and where it lands: its index, its top, and — for a row — its plate.</summary>
    private readonly record struct Laid(int Index, int Y, Rectangle Row);

    /// <summary>The layout's backing list. Update fills it, then the paint refills it; never walked re-entrantly.</summary>
    private readonly List<Laid> _laid = new();

    /// <summary>
    /// WHAT IS DRAWN IS WHAT IS HIT-TESTED: the entries the list shows from <paramref name="first"/> and
    /// where each one lands, solved once and walked by both the paint and <see cref="UpdateList"/>.
    /// </summary>
    /// <remarks>
    /// THE LEFTOVER GOES INTO THE GROUP GAPS. An entry is drawn whole or not at all, so when the next one
    /// does not fit the drawn ones ended above a band of bare panel (45 px at 125 %) with the scroll track
    /// running through it. The room is shared out over the group gaps between the drawn entries instead,
    /// so the list ends on its floor (release polish 2026-09-05, training-03).
    /// </remarks>
    private List<Laid> LayoutList(int first)
    {
        _laid.Clear();
        var bottom = RowsTop + RowsAvail;
        int drawnH = 0, closes = 0, last = first - 1;
        for (var i = first; i < Entries.Length && EntryFits(i, bottom - RowsTop - drawnH); i++)
        {
            drawnH += EntryH(i);
            if (Entries[i].Closes && i + 1 < Entries.Length) closes++;
            last = i;
        }
        var spare = last >= 0 && last + 1 < Entries.Length && closes > 0 ? Math.Max(0, bottom - RowsTop - drawnH) / closes : 0;
        var y = RowsTop;
        for (var i = first; i <= last; i++)
        {
            _laid.Add(new Laid(i, y, Entries[i].Stat < 0 ? Rectangle.Empty : RowRect(y)));
            y += EntryH(i) + (Entries[i].Closes && i < last ? spare : 0);
        }
        return _laid;
    }

    /// <summary>A row's plate at <paramref name="y"/> — the ONE rectangle the paint frames and the click tests.</summary>
    private static Rectangle RowRect(int y) => new(UiKit.ContentLeft(ListPanel), y, RowW, RowH);

    /// <summary>The TRAIN button inside a row — likewise one expression, read by both halves.</summary>
    private static Rectangle BuyRect(Rectangle row) =>
        new(row.Right - BtnW, row.Y + (row.Height - RowButtonH) / 2, BtnW, RowButtonH);

    /// <summary>Scroll the list the least distance that shows <paramref name="stat"/>'s row — with its caption, when it is the group's first.</summary>
    private void RevealRow(HunterStat stat)
    {
        var idx = Array.FindIndex(Entries, e => e.Stat >= 0 && Groups[e.Group].Stats[e.Stat] == stat);
        if (idx < 0) return;
        var top = idx > 0 && Entries[idx - 1].Stat < 0 ? idx - 1 : idx;
        if (top < _listFirst) _listFirst = top;
        while (_listFirst < ListMaxFirst && idx >= _listFirst + EntriesFitting(_listFirst)) _listFirst++;
    }

    // ── ROW METRICS, DERIVED. ────────────────────────────────────────────────────────────────
    //
    // Written as fixed pixels the list overflowed its own panel at UI SCALE 125 %: nine rows plus
    // four captions need 660 px and the page leaves 447, so the last two groups drew over the reset
    // footer, and the value column ran under the TRAIN button because its offset was written for a
    // 1246 px row and the row was 862. The first fix let the pitch shrink to whatever the height
    // allowed, floored at 36 — and at 150 % that floor held 33 px names over a rank bar (UX V2 REPORT
    // §4). Now every size is the profile's (UiMetrics), the pitch may shrink only as far as the row's
    // own button allows, and past that the list SCROLLS rather than squeezes. The columns stay
    // fractions of the row, which gives back the scrollbar's lane when it is drawn.
    private static int GroupGap => UiMetrics.Gap;                                // 12 at 100 %
    private static int IconCol => UiMetrics.Space(6);
    private static int IconArt => UiMetrics.Control(36);
    private static int NameCol => IconCol + IconArt + UiMetrics.Gap;             // 54 at 100 %
    // The small button, so the ornate frame keeps 5–6 px of air inside its row at every profile: at 44
    // in a 51 px row its top and bottom gems sat on the row rules (release polish 2026-09-05, training-07).
    private static int RowButtonH => UiMetrics.ButtonHeightSmall;
    private static int RowGap => UiMetrics.Space(4);                             // the plate's pitch minus its height
    private static int RankBarH => UiMetrics.Control(8);

    /// <summary>The comfortable pitch — 56 at 100 %; the derived pitch never exceeds it.</summary>
    private static int RowPitchMax => UiMetrics.Control(56);

    /// <summary>The tightest pitch a row can take and still frame its button. Past this the list scrolls.</summary>
    private static int RowPitchMin => RowButtonH + UiMetrics.Space(8);

    private static int RowsTop =>
        ListPanel.Y + UiTypography.PanelTitleTop + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(24);

    private static int RowsAvail => UiKit.ContentBottom(ListPanel) - RowsTop;

    private static int CaptionH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8);

    /// <summary>
    /// The row pitch. When the nine rows and their captions fit, it is the pitch that fits them, up to
    /// the comfortable one — 55 at 100 %. When they do not fit even at the tightest pitch, the list is
    /// going to scroll whatever the pitch is, so the rows take the most comfortable pitch that still
    /// shows as many rows as the tightest would: no row is squeezed to buy a band of empty panel.
    /// </summary>
    private static int RowPitch
    {
        get
        {
            var avail = RowsAvail;
            if (_pitchFor.Percent == UiMetrics.Percent && _pitchFor.Avail == avail) return _pitchFor.Pitch;
            var fit = (avail - Groups.Length * CaptionH - (Groups.Length - 1) * GroupGap) / Order.Length;
            var pitch = Math.Min(fit, RowPitchMax);
            if (fit < RowPitchMin)
            {
                var rows = RowsShowing(0, RowPitchMin, avail);
                pitch = RowPitchMax;
                while (pitch > RowPitchMin && RowsShowing(0, pitch, avail) < rows) pitch--;
            }
            _pitchFor = (UiMetrics.Percent, avail, pitch);
            return pitch;
        }
    }

    /// <summary>The pitch last derived, keyed on what it was derived from — it is read many times a frame.</summary>
    private static (int Percent, int Avail, int Pitch) _pitchFor = (-1, -1, 0);

    private static int RowH => RowPitch - RowGap;

    /// <summary>An entry's pitch: a caption's line and breath, or a row's pitch plus the group gap it closes.</summary>
    private static int EntryH(int i) => EntryH(i, RowPitch);

    private static int EntryH(int i, int pitch) =>
        Entries[i].Stat < 0 ? CaptionH : pitch + (Entries[i].Closes ? GroupGap : 0);

    /// <summary>The part of an entry that is actually drawn — what must clear the region's foot.</summary>
    private static int EntryVisibleH(int i) => EntryVisibleH(i, RowPitch);

    private static int EntryVisibleH(int i, int pitch) =>
        Entries[i].Stat < 0 ? UiTypography.Pitch(UiTypography.Secondary) : pitch - RowGap;

    /// <summary>Whether entry <paramref name="i"/> may be drawn with <paramref name="room"/> left: a caption also needs its first row.</summary>
    private static bool EntryFits(int i, int room) => EntryFits(i, room, RowPitch);

    private static bool EntryFits(int i, int room, int pitch) =>
        EntryVisibleH(i, pitch) <= room
        && (Entries[i].Stat >= 0 || (i + 1 < Entries.Length && EntryH(i, pitch) + EntryVisibleH(i + 1, pitch) <= room));

    /// <summary>How many entries draw from <paramref name="first"/> before the region's foot.</summary>
    private static int EntriesFitting(int first)
    {
        int h = 0, n = 0;
        for (var i = first; i < Entries.Length && EntryFits(i, RowsAvail - h); i++) { h += EntryH(i); n++; }
        return n;
    }

    /// <summary>How many ROWS draw from <paramref name="first"/> at <paramref name="pitch"/> in <paramref name="avail"/> — the pitch's own question.</summary>
    private static int RowsShowing(int first, int pitch, int avail)
    {
        int h = 0, n = 0;
        for (var i = first; i < Entries.Length && EntryFits(i, avail - h, pitch); i++)
        {
            h += EntryH(i, pitch);
            if (Entries[i].Stat >= 0) n++;
        }
        return n;
    }

    /// <summary>
    /// The furthest the list scrolls: the first entry from which everything after it still fits. Zero
    /// when the whole list fits, which is also the profile's "does it scroll?" answer.
    /// </summary>
    private static int ListMaxFirst
    {
        get
        {
            var h = 0;
            for (var i = Entries.Length - 1; i >= 0; i--)
            {
                h += i == Entries.Length - 1 ? EntryVisibleH(i) : EntryH(i);
                if (h > RowsAvail) return i + 1;
            }
            return 0;
        }
    }

    private static bool ListScrolls => ListMaxFirst > 0;

    /// <summary>The lane the scrollbar takes from the rows' right edge — nothing when the list fits.</summary>
    private static int ListLane => ListScrolls ? UiMetrics.ScrollbarWidth + UiMetrics.Gap : 0;

    private static int RowW => UiKit.ContentRight(ListPanel) - UiKit.ContentLeft(ListPanel) - ListLane;
    private static int RankCol => RowW * 20 / 100;
    // The value column takes five more hundredths from the rank bar, so DAMAGE TAKEN and CRITICAL DAMAGE
    // keep their nouns at 150 % instead of printing as bare figures (release polish 2026-09-05, training-02).
    private static int ValueCol => RowW * 34 / 100;
    private static int RankBarW => RowW * 12 / 100;

    /// <summary>
    /// The TRAIN button's width — 224 at 100 %, growing with its label, never more than 27 % of the
    /// row. The share is what NEED 1,249 GLEAM needs at 150 % beside the button art's end ornament;
    /// a narrower button would have the label shrunk to fit it, which §17 forbids.
    /// </summary>
    private static int BtnW => Math.Min(UiMetrics.Control(224), RowW * 27 / 100);

    private void DrawRow(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                         HunterStat stat, Rectangle row, Point hit)
    {
        var selected = Selection(hunter) == stat;
        var hot = row.Contains(hit);
        var btn = BuyRect(row);
        var onButton = btn.Contains(hit);
        var playing = _fx.Playing(stat);
        var glow = playing ? _fx.Glow : 0f;
        // The 5 px gold left rule IS the selection mark — one accent, one meaning. HOVER is a wash over
        // the plate, never the rule, so the two states cannot be confused (LAW 4). THE STATES (§25–§27):
        // hover eases in over Fast; PRESSED — the button held over the plate, not over its own TRAIN
        // button, which presses itself — darkens the plate under a lit lip for exactly as long as it is
        // held; a row just trained carries one faint gold wash that fades with its figures.
        _ui.Plate(b, row, selected ? Gold : null);
        var inner = new Rectangle(row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2);
        var lift = UiMotion.Ease(HashCode.Combine(RowHoverSalt, (int)stat), hot && !selected ? 1f : 0f);
        if (lift > 0f) _ui.Fill(b, inner, Slate * (0.10f * lift));
        if (hot && !onButton && UiKit.MouseHeld)
        {
            _ui.Fill(b, inner, Color.Black * 0.30f);
            _ui.Fill(b, new Rectangle(inner.X, inner.Y, inner.Width, UiMetrics.Control(2)), Slate * 0.6f);
        }
        if (glow > 0f) _ui.Fill(b, inner, Gold * (0.06f * glow));
        // No hover tip on a row: it repeated the identity sentence the inspector prints on the click, in
        // a box across the panel's foot (release polish 2026-09-05, training-04). Click selects; the
        // inspector explains.

        var (key, gem) = ArtFor(stat);
        var art = Math.Min(IconArt, row.Height - UiMetrics.Space(8));
        var iconBox = new Rectangle(row.X + IconCol, row.Y + (row.Height - art) / 2, art, art);
        if (!_ui.Icon(b, key, iconBox, Color.White))
        {
            var inset = UiMetrics.Space(6);
            _ui.Diamond(b, new Rectangle(iconBox.X + inset, iconBox.Y + inset, art - 2 * inset, art - 2 * inset), gem);
        }

        var textY = row.Y + (row.Height - UiTypography.Body) / 2 - 2;
        _ui.TextBig(b, WordFor(stat), row.X + NameCol, textY, Bone, UiTypography.Body);

        // RANK AS A SHAPE as well as a number (§8): sixty ranks is a long way, and a bare "12 / 60"
        // does not say how far. The figure hangs from the row's top and the bar from its foot, so the
        // two part as the row grows with the profile rather than meeting in the middle. ON TRAIN the bar
        // EASES to its new length over the Transition (UiMotion.Ease, keyed to the row) and the figure
        // flashes with it.
        var rank = hunter.RankOf(stat);
        var rankShown = playing ? TrainFeedback.Mix(_fx.RankFrom, rank, _fx.Progress) : rank;
        _ui.TextBig(b, $"{rank} / {hunter.StatRankCap}", row.X + RankCol, row.Y + UiMetrics.Space(4),
                    Tint(Slate, glow), UiTypography.Secondary);
        // The house SLIM bar (UiKit.Bar — the progress bar contract), not a hand-drawn pair of fills, so
        // a rank bar and a roster gate's bar are the same control at every profile.
        var barY = row.Bottom - UiMetrics.Space(14);
        _ui.Bar(b, row.X + RankCol, barY, RankBarW, RankBarH, rankShown / hunter.StatRankCap, Tint(Bone, glow));

        // NOW → AFTER, at Headline, because this is the decision. When the two format the same — at
        // the cap, or a gain too small to show — the arrow is dropped: a "96 → 96" promises a change
        // the number does not make. ON TRAIN both figures tick to their new numbers over the
        // Transition and flash once (§36: "160 → 162", brief emphasis); the label keeps its ink, so
        // the flash lights the number, not the words.
        var (label, style, nowV, aftV) = Effect(stat, hunter, build, mods, shape);
        var nowShown = playing ? TrainFeedback.Mix(_fx.NowFrom, nowV, _fx.Progress) : nowV;
        var aftShown = playing ? TrainFeedback.Mix(_fx.AfterFrom, aftV, _fx.Progress) : aftV;
        var now = Fmt(style, nowShown);
        var after = Fmt(style, aftShown);
        var nowInk = Tint(Bone, glow);
        var aftInk = Tint(Met, glow);
        var x = row.X + ValueCol;
        var vy = row.Y + (row.Height - UiTypography.Headline) / 2 - 2;
        // The value column ends where the button begins — it used to be written as an offset for one
        // page width and ran straight under the button at any other.
        var valueGap = UiMetrics.Space(16);
        var room = btn.X - valueGap - x;
        var head = $"{label} {now}";
        if (after != now)
        {
            const string arrow = " → ";   // single-spaced: the double spaces cost two Headline spaces a side at 150 %
            var full = $"{head}{arrow}{after}";
            if (_ui.MeasureBig(full, UiTypography.Headline) <= room)
            {
                x = DrawFigure(b, label, now, x, vy, nowInk);
                _ui.TextBig(b, arrow, x, vy, Slate, UiTypography.Headline);
                x += _ui.MeasureBig(arrow, UiTypography.Headline);
                _ui.TextBig(b, after, x, vy, aftInk, UiTypography.Headline);
            }
            else
            {
                // Too narrow for all three — the widest labels at 150 % — so NOW gives way and the NOUN
                // stays: the inspector carries NOW, and a row that read "-9.1% → -10.7%" was a signed
                // percentage of nothing (release polish 2026-09-05, training-02). The figures still flow
                // as one phrase, exactly as they do in the rows that fit.
                var lead = $"{label}{arrow}";
                var leadW = _ui.MeasureBig(lead, UiTypography.Headline);
                if (leadW + _ui.MeasureBig(after, UiTypography.Headline) <= room)
                {
                    _ui.TextBig(b, label, x, vy, nowInk, UiTypography.Headline);
                    _ui.TextBig(b, arrow, x + _ui.MeasureBig(label, UiTypography.Headline), vy, Slate, UiTypography.Headline);
                    _ui.TextBig(b, after, x + leadW, vy, aftInk, UiTypography.Headline);
                }
                else
                    _ui.TextBig(b, _ui.ShortenBig(lead + after, room, UiTypography.Headline), x, vy, nowInk, UiTypography.Headline);
            }
        }
        else if (_ui.MeasureBig(head, UiTypography.Headline) <= room)
            DrawFigure(b, label, now, x, vy, nowInk);
        else
            _ui.TextBig(b, _ui.ShortenBig(head, room, UiTypography.Headline), x, vy, nowInk, UiTypography.Headline);

        var maxed = rank >= hunter.StatRankCap;
        var cost = hunter.NextRankCost(stat);
        var afford = hunter.CanTrain(stat);
        // DRAWN, NOT DECIDED. The TRAIN press, the NEED refusal and the plate's select are all resolved
        // in UpdateList, against BuyRect(row) and row — the same two rectangles this paint uses.
        Button(b, btn, maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM",
               hit, afford);
    }

    /// <summary>A button on this screen — painted here, decided in <see cref="Update"/>.</summary>
    /// <remarks>
    /// It delegates to <see cref="UiKit.Button"/> with the edge as a literal <c>false</c>, exactly as
    /// MasteryScreen's does: that argument feeds only the return value, while the hover ease and the
    /// pressed face come from the static <see cref="UiKit.MouseHeld"/> — so the face is pixel-identical
    /// and the button can never fire from a paint.
    /// </remarks>
    private void Button(SpriteBatch b, Rectangle r, string label, Point hit, bool enabled = true,
                        ButtonStyle style = ButtonStyle.Secondary)
        => _ui.Button(b, r, label, hit, false, enabled, style);

    /// <summary>
    /// A row's label and its NOW figure — the figure in its own ink, so a flash lights the number and not
    /// the words. Returns the x after the figure.
    /// </summary>
    private int DrawFigure(SpriteBatch b, string label, string now, int x, int y, Color ink)
    {
        var lead = label + " ";
        _ui.TextBig(b, lead, x, y, Bone, UiTypography.Headline);
        x += _ui.MeasureBig(lead, UiTypography.Headline);
        _ui.TextBig(b, now, x, y, ink, UiTypography.Headline);
        return x + _ui.MeasureBig(now, UiTypography.Headline);
    }

    /// <summary>A figure's ink under the flash: its resting colour lifted toward gold by how much of the pulse is left.</summary>
    private static Color Tint(Color rest, float glow) => glow <= 0f ? rest : Color.Lerp(rest, Gold, glow);

    // ── THE INSPECTOR — the house grammar (§6). It replaces a 640 px hover document that covered the
    //    rows beside the one it was explaining, and could only be read by holding the pointer still. ──
    private void DrawInspector(SpriteBatch b, Hunter hunter, Build build, BuildMods mods, SkillShape shape,
                               Point hit)
    {
        _ui.PanelQuiet(b, InspectorPanel);
        var stat = Selection(hunter);

        var left = UiKit.ContentLeft(InspectorPanel);
        var right = UiKit.ContentRight(InspectorPanel);
        var width = right - left;
        var lineH = UiTypography.Pitch(UiTypography.Body);
        var capH = UiTypography.Pitch(UiTypography.Secondary);
        var breath = UiMetrics.Space(8);

        // THE STATE BLOCK IS ANCHORED to the foot, the CTA under it, and the prose stops above it.
        // Flowed, it walked into the button at 125 %, where the panel is 554 px tall instead of 770 and
        // the prose fills the difference. The refusal line keeps two lines of room so a long one wraps.
        var cta = InspectorCta;
        // THE REFUSAL'S ROOM IS RESERVED ONLY WHEN THERE IS A REFUSAL. Two lines were always kept for
        // it, so on the common path (the rank is affordable) 56–86 px sat blank above the button while
        // WHAT IT DOES had nothing under it at 150 % (release polish 2026-09-05, training-01).
        var maxed = hunter.RankOf(stat) >= hunter.StatRankCap;
        var afford = hunter.CanTrain(stat);
        var refusalRoom = afford ? 0 : breath + 2 * capH;
        var refusalY = cta.Y - refusalRoom;
        var stateH = capH + breath + lineH + UiTypography.Pitch(UiTypography.PrimaryValue) + lineH * 2;
        var stateTop = refusalY - breath - stateH;
        var floor = stateTop - UiMetrics.Space(8);

        // THE HEADER stays put — the category, the name, the identity line and the WHAT IT DOES caption
        // are what the scrolled prose is about, so they never scroll off (training-01).
        var y = InspectorPanel.Y + UiTypography.PanelTitleTop;
        _ui.TextBig(b, _ui.ShortenBig($"{GroupOf(stat)}  ·  TRAINED STAT", width, UiTypography.Secondary),
                    left, y, Slate, UiTypography.Secondary);
        y += capH;
        _ui.TextBig(b, _ui.ShortenBig(WordFor(stat), width, UiTypography.Headline), left, y, Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline);
        // The identity line, unless the description's first sentence already IS it (MIGHT's does): the
        // same sentence twice, three lines apart, was two of the five lines the 125 % viewport could
        // show (training-06).
        var identity = IdentityOf(stat);
        var core = identity.TrimEnd('.');
        if (core.StartsWith("Your ", StringComparison.OrdinalIgnoreCase)) core = core[5..];
        var firstPara = Describe(stat, hunter, build, mods, shape).FirstOrDefault() ?? "";
        if (!firstPara.Contains(core, StringComparison.OrdinalIgnoreCase))
            foreach (var l in _ui.WrapBig(identity, width, UiTypography.Body))
            {
                _ui.TextBig(b, l, left, y, Slate, UiTypography.Body);
                y += lineH;
            }
        _ui.Fill(b, new Rectangle(left, y + UiMetrics.Space(4), width, 1), Dim);
        y += UiMetrics.Space(14);
        _ui.TextBig(b, "WHAT IT DOES", left, y, Slate, UiTypography.Secondary);
        y += capH;

        // THE PROSE — the identity line, WHAT IT DOES and its paragraphs — fills the room between the
        // header and the state block. At 100 % it fits. At the larger profiles it does not (three
        // paragraphs at Body 33 in 416 px are twice the room), and rather than truncating a sentence it
        // SCROLLS (brief §18: long inspectors): laid out at full width first, and only if that overflows
        // laid out again beside a scrollbar's lane, so the 100 % wrap never moves for a lane it does not use.
        var proseTop = y;
        var proseH = floor - proseTop;
        var textW = width;
        // THE REACH IS MEASURED HERE AND SPENT IN UPDATE. The stat reset, the wheel and the clamp on the
        // _inspectorFirst FIELD all live in UpdateInspector; this paint clamps only its own LOCAL first
        // line, against the reach it has this instant measured, so an offset out of range is never drawn
        // — not even on the one frame after the UI SCALE profile changes.
        var first = 0;
        if (BuildProse(stat, hunter, build, mods, shape, textW, proseH)) _proseReach = 0;
        else
        {
            textW = width - UiMetrics.ScrollbarWidth - UiMetrics.Gap;
            BuildProse(stat, hunter, build, mods, shape, textW, proseH);
            _proseReach = ProseMaxFirst(proseH);
            first = Math.Clamp(_inspectorFirst, 0, _proseReach);
            _ui.ScrollBar(b, new Rectangle(right - UiMetrics.ScrollbarWidth, proseTop, UiMetrics.ScrollbarWidth, proseH),
                          first, _prose.Count - _proseReach, _prose.Count);
        }
        for (var i = first; i < _prose.Count; i++)
        {
            var (text, ink, px, h) = _prose[i];
            if (y + ProseVisibleH(i) > floor) break;
            if (text is null) _ui.Fill(b, new Rectangle(left, y + UiMetrics.Space(4), textW, 1), ink);
            else _ui.TextBig(b, text, left, y, ink, px);
            y += h;
        }

        // CURRENT STATE — anchored to the foot so the loudest number sits in the same place on every
        // stat, which is what the eye compares between rows. Sized above, so nothing here can run
        // into the button.
        // ON TRAIN the inspector updates IN PLACE (§39 — no modal): the rank, NOW and AFTER tick to
        // their new numbers with the row and flash once with it — the same feedback, the same instant.
        var playing = _fx.Playing(stat);
        var glow = playing ? _fx.Glow : 0f;
        var (_, style, nowV, aftV) = Effect(stat, hunter, build, mods, shape);
        var rank = hunter.RankOf(stat);
        var now = Fmt(style, playing ? TrainFeedback.Mix(_fx.NowFrom, nowV, _fx.Progress) : nowV);
        var after = Fmt(style, playing ? TrainFeedback.Mix(_fx.AfterFrom, aftV, _fx.Progress) : aftV);
        var cost = hunter.NextRankCost(stat);

        void Pair(string k, string v, Color ink)
        {
            _ui.TextBig(b, k, left, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, v, right, y, ink, UiTypography.Body);
            y += lineH;
        }

        y = stateTop;
        _ui.Fill(b, new Rectangle(left, y, width, 1), Dim);
        y += UiMetrics.Space(12);
        _ui.TextBig(b, "CURRENT STATE", left, y, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"RANK {rank} OF {hunter.StatRankCap}", right, y - UiMetrics.Space(4), Tint(Bone, glow),
                         UiTypography.Headline);
        y += capH + UiMetrics.Space(4);
        Pair("NOW", now, Tint(Bone, glow));
        if (!maxed)
        {
            _ui.TextBig(b, "AFTER ONE RANK", left, y + UiMetrics.Space(6), Slate, UiTypography.Body);
            _ui.TextRightBig(b, after, right, y, Tint(Met, glow), UiTypography.PrimaryValue);
            y += UiTypography.Pitch(UiTypography.PrimaryValue);
            _ui.TextBig(b, _ui.ShortenBig($"ONE RANK ADDS {hunter.GainPerRank(stat):0.##} {WordFor(stat)}", width, UiTypography.Body),
                        left, y, Slate, UiTypography.Body);
            y += lineH;
        }

        Pair("WHAT IT COSTS", maxed ? "—" : $"{cost:N0} GLEAM", maxed ? Slate : afford ? Bone : Ember);

        if (!afford)
        {
            // Two lines of room were reserved for it: at the larger profiles the sentence needs both.
            var refusal = maxed ? $"THIS STAT IS FULLY TRAINED. RANK {hunter.StatRankCap} IS THE MOST IT CAN REACH."
                                : $"NOT ENOUGH GLEAM — {cost:N0} NEEDED, YOU HAVE {hunter.Gleam:N0}.";
            var lines = _ui.WrapBig(refusal, width, UiTypography.Secondary);
            var ry = refusalY;
            for (var i = 0; i < Math.Min(2, lines.Count); i++, ry += capH)
                _ui.TextBig(b, lines.Count > 2 && i == 1 ? _ui.ShortenBig(lines[i] + "…", width, UiTypography.Secondary) : lines[i],
                            left, ry, Ember, UiTypography.Secondary);
        }

        // DRAWN, NOT DECIDED: the train and the refusal are UpdateInspector's, against InspectorCta.
        Button(b, cta, maxed ? "MAXED" : afford ? $"TRAIN  {cost:N0} GLEAM" : $"NEED  {cost:N0} GLEAM",
               hit, afford, afford ? ButtonStyle.Primary : ButtonStyle.Secondary);
    }

    /// <summary>The inspector's prose this frame — one drawn line each; a null text is a hairline rule. Reused, not reallocated.</summary>
    private readonly List<(string? Text, Color Ink, int Px, int H)> _prose = new();

    /// <summary>The first prose line the inspector draws, and the stat it was scrolled for — a new stat starts at the top.</summary>
    private int _inspectorFirst;
    private HunterStat? _inspectorFor;

    /// <summary>How far the prose CAN scroll, as the last paint measured it.</summary>
    /// <remarks>
    /// The one presentation number the paint still records, because the reach is a count of WRAPPED LINES
    /// rather than a rectangle: Update could only learn it by laying the prose out a second time.
    /// <see cref="UpdateInspector"/> spends the wheel against it and clamps <see cref="_inspectorFirst"/>
    /// to it. Writing it is idempotent — three paints with no Update between them measure the same prose
    /// and write the same number — so the "Draw changes nothing semantic" property holds.
    /// </remarks>
    private int _proseReach;

    /// <summary>
    /// Lay the selected stat's prose out at <paramref name="width"/> into <see cref="_prose"/>: the identity
    /// line, a rule, WHAT IT DOES, and each paragraph with a breath after it. True when it all fits in
    /// <paramref name="height"/>.
    /// </summary>
    private bool BuildProse(HunterStat stat, Hunter hunter, Build build, BuildMods mods, SkillShape shape, int width, int height)
    {
        _prose.Clear();
        var lineH = UiTypography.Pitch(UiTypography.Body);
        // Only the paragraphs scroll: the identity line, its rule and the WHAT IT DOES caption are drawn
        // fixed under the name by DrawInspector (training-01).
        foreach (var para in Describe(stat, hunter, build, mods, shape))
        {
            var lines = _ui.WrapBig(para, width, UiTypography.Body);
            for (var i = 0; i < lines.Count; i++)
                _prose.Add((lines[i], Bone, UiTypography.Body, lineH + (i == lines.Count - 1 ? UiMetrics.Space(6) : 0)));
        }
        var h = 0;
        for (var i = 0; i < _prose.Count; i++)
        {
            if (h + ProseVisibleH(i) > height) return false;
            h += _prose[i].H;
        }
        return true;
    }

    /// <summary>The drawn part of a prose line — its own pitch, without the paragraph breath folded under it.</summary>
    private int ProseVisibleH(int i) =>
        _prose[i].Text is null ? UiMetrics.Space(4) + 1 : UiTypography.Pitch(_prose[i].Px);

    /// <summary>The furthest the prose scrolls: the first line from which the rest still fits in <paramref name="height"/>.</summary>
    private int ProseMaxFirst(int height)
    {
        var h = 0;
        for (var i = _prose.Count - 1; i >= 0; i--)
        {
            h += i == _prose.Count - 1 ? ProseVisibleH(i) : _prose[i].H;
            if (h > height) return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// How a row's figure is written — the shape of the number, kept apart from its value, so a value
    /// part-way through a tick is written exactly the way its ends are.
    /// </summary>
    private enum FigureStyle { Whole, Thousands, Percent, Percent1, PlusPercent, MinusPercent1, Times }

    /// <summary>Write a figure in its style. The percentages arrive already ×100.</summary>
    private static string Fmt(FigureStyle style, float v) => style switch
    {
        FigureStyle.Whole => $"{v:0}",
        FigureStyle.Thousands => $"{v:N0}",
        FigureStyle.Percent => $"{v:0}%",
        FigureStyle.Percent1 => $"{v:0.0}%",
        FigureStyle.PlusPercent => $"+{v:0}%",
        FigureStyle.MinusPercent1 => $"-{v:0.0}%",
        _ => $"{v:0.00}×",
    };

    /// <summary>
    /// The row's label, how its figure is written, its NOW and its AFTER — the AFTER through
    /// <see cref="Hunter.Preview{T}"/>. Numbers rather than strings, so a TRAIN can tick between them.
    /// </summary>
    private (string Label, FigureStyle Style, float Now, float After) Effect(HunterStat stat, Hunter hunter, Build build,
                                                                             BuildMods mods, SkillShape shape)
    {
        switch (stat)
        {
            case HunterStat.AttackPower:
            {
                var now = SoloBattle.AutoAttackDamage * hunter.AutoDamageMultiplier * mods.Damage;
                var aft = hunter.Preview(stat, h => SoloBattle.AutoAttackDamage * h.AutoDamageMultiplier * build.Resolve(h).Damage);
                return ("BASIC HIT", FigureStyle.Whole, now, aft);
            }
            case HunterStat.ResonanceAffinity:
            {
                var per = 100f * SkillCatalogue.ResonancePerPoint;
                var now = per * hunter.ValueOf(stat);
                var aft = hunter.Preview(stat, h => per * h.ValueOf(stat));
                return ("SKILL POWER", FigureStyle.PlusPercent, now, aft);
            }
            case HunterStat.CriticalChance:
            {
                var now = SoloBattle.CritChance(hunter, shape);
                var aft = hunter.Preview(stat, h => SoloBattle.CritChance(h, shape));
                return ("CRITICAL HITS", FigureStyle.Percent1, 100f * now, 100f * aft);
            }
            case HunterStat.Focus:
            {
                var now = SoloBattle.CritMultiplier(hunter);
                var aft = hunter.Preview(stat, h => SoloBattle.CritMultiplier(h));
                return ("CRITICAL DAMAGE", FigureStyle.Percent, 100f * now, 100f * aft);
            }
            case HunterStat.MaxHealth:
            {
                var now = SoloBattle.ChampionHealth(build, hunter);
                var aft = hunter.Preview(stat, h => SoloBattle.ChampionHealth(build, h));
                return ("LIFE", FigureStyle.Thousands, now, aft);
            }
            case HunterStat.Defense:
            {
                var k = SoloBattle.DefenseMitigationConstant;
                var now = 100f - 100f * (k / (k + hunter.Defense));
                var aft = hunter.Preview(stat, h => 100f - 100f * (k / (k + h.Defense)));
                return ("DAMAGE TAKEN", FigureStyle.MinusPercent1, now, aft);
            }
            case HunterStat.Vitality:
            {
                var now = Regen(hunter, SoloBattle.ChampionHealth(build, hunter));
                var aft = hunter.Preview(stat, h => Regen(h, SoloBattle.ChampionHealth(build, h)));
                return ("LIFE EACH SECOND", FigureStyle.Thousands, now, aft);
            }
            case HunterStat.Engineering:
            {
                var now = mods.SkillRate * shape.SkillRate;
                var aft = hunter.Preview(stat, h => build.Resolve(h).SkillRate * shape.SkillRate);
                return ("ACTION SPEED", FigureStyle.Times, now, aft);
            }
            default:
            {
                var now = 100f * (mods.Haul - 1f);
                var aft = hunter.Preview(stat, h => 100f * (build.Resolve(h).Haul - 1f));
                return ("LOOT", FigureStyle.PlusPercent, now, aft);
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

    /// <summary>The control <see cref="_tip"/> explains — the tip is placed beside IT, not the pointer.</summary>
    private Rectangle _tipAt;

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

    // DrawTrain — a 176×44 button helper nothing called since the row grew its own — is gone with the
    // density-profile pass: a dormant literal is the next overflow waiting to be copied.

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
    private void DrawReset(SpriteBatch b, Hunter hunter, Point hit)
    {
        _ui.Plate(b, ResetBar);

        var ranks = hunter.TotalTrainedRanks;
        var crystals = hunter.MaterialOf(Material.Crystal);
        var price = hunter.TrainingResetCrystalCost;

        // THE BUTTON IS BUTTON-SIZED. The ornate button art is a whole-image stretch, so a 960 px one
        // smeared its corner scrollwork into a streak; the explanation lives in text beside it. It is
        // the house button height, centred in the bar, and its width grows with its label.
        var pad = UiMetrics.Space(24);
        var button = ResetButton;
        var tx = ResetBar.X + pad;
        var textW = button.X - pad - tx;
        var titleY = ResetBar.Y + UiMetrics.Space(12);
        var lineY = titleY + UiTypography.Pitch(UiTypography.Body);

        // THE ARM IS READ HERE, never stood down — that is UpdateReset's. The four seconds are part of
        // the read, so the face is exactly the one the old code drew: an expired arm paints disarmed the
        // instant it expires, even on a frame the host skipped Update for an open modal.
        if (ResetArmedNow)
        {
            _ui.TextBig(b, $"SURE? ALL {ranks} RANKS GO BACK TO ZERO.", tx, titleY,
                        Color.White, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig("THE GLEAM YOU SPENT DOES NOT COME BACK. CLICK THE RED BUTTON AGAIN TO DO IT.",
                                          textW, UiTypography.Body),
                        tx, lineY, Ember, UiTypography.Body);
            // THE ARMED PLATE IS DRAWN BY HAND, so it owes its own states (§25–§27) — every other
            // control on this page gets them from UiKit.Button, and without them the one irreversible
            // control on the screen was the only one that did not answer the cursor. HOVER eases a
            // luminance lift over Fast; PRESSED drops the face 2 px and darkens it for exactly as long
            // as the button is held, the same depression the ornate buttons beside it make.
            var armedHot = button.Contains(hit);
            var armedLift = UiMotion.Ease(UiMotion.KeyOf(button), armedHot ? 1f : 0f);
            var armedHeld = armedHot && UiKit.MouseHeld;
            var armedFace = armedHeld ? new Rectangle(button.X, button.Y + 2, button.Width, button.Height) : button;
            _ui.Fill(b, armedFace, ArmedRed);
            if (armedLift > 0f) _ui.Fill(b, armedFace, Color.White * (0.10f * armedLift));
            if (armedHeld) _ui.Fill(b, armedFace, Color.Black * 0.22f);
            _ui.Fill(b, new Rectangle(armedFace.X, armedFace.Y, armedFace.Width, UiMetrics.Control(3)), Ember);
            _ui.TextCenterBig(b, _ui.ShortenBig("YES, RESET — NO GLEAM BACK", armedFace.Width - 2 * UiTypography.ButtonPadX, UiTypography.Body),
                              armedFace.Center.X, armedFace.Center.Y - UiTypography.Body * 10 / 22,
                              Color.White, UiTypography.Body, TextFace.Strong);
            // The confirming second press, and the any-click disarm, are UpdateReset's.
            return;
        }

        _ui.TextBig(b, "RESET ALL TRAINING", tx, titleY, Bone, UiTypography.Body);
        var cost = $"{ranks} RANKS GO BACK TO ZERO. IT COSTS {price:N0} CRYSTAL — YOU HAVE {crystals:N0}. THE GLEAM YOU SPENT DOES NOT COME BACK.";
        if (ranks <= 0)
        {
            _ui.TextBig(b, "NOTHING TO RESET — YOU HAVE NOT TRAINED ANYTHING YET.", tx, lineY,
                        Slate, UiTypography.Body);
            Button(b, button, "RESET", hit, enabled: false);
        }
        else if (crystals < price)
        {
            // THE LAST CLAUSE IS THE WARNING. Cut from the right this sentence loses "THE GLEAM YOU
            // SPENT DOES NOT COME BACK", which is the whole reason it is being asked. It steps down a
            // rung instead; the row is one line and there is nowhere to wrap to.
            _ui.TextBig(b, _ui.ShortenBig(cost, textW, _ui.FitRung(cost, textW, UiTypography.Body)), tx, lineY,
                        Bone, _ui.FitRung(cost, textW, UiTypography.Body));
            Button(b, button, $"NEEDS {price:N0} CRYSTAL", hit, enabled: false);
        }
        else
        {
            // THE LAST CLAUSE IS THE WARNING. Cut from the right this sentence loses "THE GLEAM YOU
            // SPENT DOES NOT COME BACK", which is the whole reason it is being asked. It steps down a
            // rung instead; the row is one line and there is nowhere to wrap to.
            _ui.TextBig(b, _ui.ShortenBig(cost, textW, _ui.FitRung(cost, textW, UiTypography.Body)), tx, lineY,
                        Bone, _ui.FitRung(cost, textW, UiTypography.Body));
            // The arming first press is UpdateReset's, against the same ResetButton this frames.
            Button(b, button, $"RESET — {price:N0} CRYSTAL", hit);
        }
    }

    /// <summary>The reset button's width — 360 at 100 %, wide enough for YES, RESET — NO GLEAM BACK at Body.</summary>
    private static int ResetButtonW => UiMetrics.Control(360);

    /// <summary>
    /// RESET ALL TRAINING's button — the ONE rectangle the ornate face, the hand-drawn armed red face and
    /// <see cref="UpdateReset"/> all read.
    /// </summary>
    private static Rectangle ResetButton =>
        new(ResetBar.Right - UiMetrics.Space(24) - ResetButtonW,
            ResetBar.Y + (ResetBar.Height - UiMetrics.ButtonHeight) / 2, ResetButtonW, UiMetrics.ButtonHeight);

    /// <summary>Whether the red are-you-sure face is live: the arm, inside the four seconds it waits in.</summary>
    /// <remarks>
    /// A READ, so the paint can show the expiry without writing it. <see cref="UpdateReset"/> is the one
    /// place the flag is actually stood down.
    /// </remarks>
    private bool ResetArmedNow =>
        _resetArmed && Environment.TickCount64 - _resetArmedAtMs <= (long)(ArmSeconds * 1000);

    // DrawProgression, LevelCard, SetHover and DrawHoverCard are gone with UX V2 P2.3.
    //
    // The PROGRESS panel showed HIGHEST WAVE, CHESTS OPENED, MASTERY POINTS and HUNTER LEVEL — the
    // Map's number, the Vault's, the Mastery tree's, and one this screen already printed elsewhere.
    // None of the four moves when you train, which is the only question this screen exists to answer.
    // LEVEL's honest "it is a medal, not a stat" answer survives as the header's hover tip.
    //
    // The 640 px hover document is replaced by the inspector: it covered the rows beside the one it
    // explained, and it could only be read by holding the pointer perfectly still.

    /// <summary>
    /// The feedback for one TRAIN — what the row and the pill showed the moment it was bought, and how far
    /// the change has played. The TARGET values are never stored: they are the live ones the screen reads
    /// from the Hunter every frame, so the tick cannot end anywhere but on the truth (LAW 10: motion
    /// explains a change; it never states a number of its own).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Time comes from <see cref="UiMotion"/> — the host's Tick — never from a clock of its own. The tick
    /// and the bar are one <see cref="UiMotion.Ease"/> keyed to the row, rewound at <see cref="Start"/> so
    /// a second rank on the same row plays again from the start; the flash is one
    /// <see cref="UiMotion.Flash"/> under the same key, once per event. Under Reduced Motion the ease
    /// jumps and only the flash fades: the same end state, at once.
    /// </para>
    /// <para>
    /// Public because the Game tests drive it: a capture can hold it at one instant
    /// (<c>RH_SHOT_TRAIN</c>) but cannot photograph t = 0, mid and end of a 180 ms event.
    /// </para>
    /// </remarks>
    public sealed class TrainFeedback
    {
        /// <summary>The row that was trained; null once the feedback has played out.</summary>
        public HunterStat? Stat { get; private set; }

        /// <summary>What the pill showed when the rank was bought.</summary>
        public float GleamFrom { get; private set; }

        /// <summary>What the row's NOW showed when the rank was bought.</summary>
        public float NowFrom { get; private set; }

        /// <summary>What the row's AFTER showed when the rank was bought.</summary>
        public float AfterFrom { get; private set; }

        /// <summary>How far the rank bar had reached, in ranks, when the rank was bought.</summary>
        public float RankFrom { get; private set; }

        /// <summary>
        /// How far the numbers and the bar have moved: 0 → 1 over <see cref="UiMotion.Transition"/>, and 1
        /// at once under Reduced Motion.
        /// </summary>
        public float Progress { get; private set; } = 1f;

        /// <summary>The flash: 1 the frame it fires → 0 over <see cref="UiMotion.Transition"/>.</summary>
        public float Glow { get; private set; }

        /// <summary>True while anything is still moving or fading.</summary>
        public bool Live => Stat is not null;

        /// <summary>True while THIS row's feedback is playing.</summary>
        public bool Playing(HunterStat stat) => Stat == stat;

        /// <summary>The key the row's ease and its flash live under.</summary>
        public static int KeyFor(HunterStat stat) => HashCode.Combine("train", (int)stat);

        /// <summary>Begin — from what was SHOWN at the click, toward whatever the Hunter now says.</summary>
        public void Start(HunterStat stat, float gleamFrom, float nowFrom, float afterFrom, float rankFrom)
        {
            Stat = stat;
            GleamFrom = gleamFrom;
            NowFrom = nowFrom;
            AfterFrom = afterFrom;
            RankFrom = rankFrom;
            var key = KeyFor(stat);
            UiMotion.Ease(key, 0f, 0f);                 // rewind the row's ease: a settled 1 would answer 1 at once
            UiMotion.Flash(key, UiMotion.Transition);   // ONE flash, keyed to this event
            Progress = 0f;
            Glow = 1f;
        }

        /// <summary>
        /// Advance — from Update, with the host's dt already in <see cref="UiMotion"/>. <paramref name="pin"/>
        /// holds the phase for a capture (0..1 through the Transition) instead of reading the clock.
        /// </summary>
        public void Advance(float? pin = null)
        {
            if (Stat is not { } stat) return;
            if (pin is { } t)
            {
                Progress = UiMotion.Smooth(t);
                Glow = 1f - t;
                return;
            }
            var key = KeyFor(stat);
            Progress = UiMotion.Ease(key, 1f, UiMotion.Transition);
            Glow = UiMotion.Pulse(key);
            if (Progress >= 1f && Glow <= 0f) Stat = null;
        }

        /// <summary>A figure part-way through its tick.</summary>
        public static float Mix(float from, float to, float progress) => from + (to - from) * progress;
    }

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
