using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Warrens;

using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Game;

/// <summary>
/// WARREN: what each facility pays, where that is spent, and what the place made while you were away.
/// A summary strip, an eight-facility grid, and the shared inspector.
/// </summary>
/// <remarks>
/// <para>
/// Every value is real, from the <see cref="Core.Warrens.Warren"/> model the host owns: facility levels
/// and outputs, the Warren level and XP, the derived production multipliers, and the real upgrade costs
/// in Gleam and Dust. The host sets the model and the owned balances each frame and consumes the
/// upgrade request.
/// </para>
/// <para>
/// UX V2 P2.4 (brief §77–§81, as corrected by PLAN D11 — the Warren has no services, no targeting and
/// no automation in Core, so those sections are honoured as what each facility PAYS and where that is
/// spent). The screen used to answer a question nobody asked: its loudest panel was five derived
/// percentages and the formula that produced them, while the two facts a player comes here for were
/// missing — no card said whether it could be upgraded now, so the only way to find out was to click
/// all eight, and what the place earned while the game was closed was never shown at all, though the
/// host had already computed it.
/// </para>
/// <para>
/// UI POLISH P2 (brief §8–§9, §17–§18): UI SCALE is a density profile, so every row, chip, glyph box
/// and pad on this screen is a base size read through <see cref="UiMetrics"/>, and every vertical
/// rhythm is derived from the rungs it stacks rather than from an assumed 1080 px of height. The
/// strip's height is its tallest cell's stack; the grid gets what is under the strip and a card lays
/// its stack out from what it is given (the icon plate and the milestone caption yield, in that
/// order, when the height is not there); the inspector's rows scroll under a wheel when they no
/// longer fit above its refusal line and button, which stay anchored where they were.
/// </para>
/// </remarks>
public sealed class WarrenScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;
    private static readonly Color GleamC = new(0xF0, 0xA8, 0x30);   // gold coin track
    private static readonly Color DustC = new(0x5F, 0xE0, 0xC8);    // teal gem track
    private static readonly Color ScrapC = new(0x9A, 0xC0, 0x88);   // scrap green — the Forge wallet's tint
    private static readonly Color EssenceC = new(0x74, 0xC6, 0xE8); // essence blue — the Forge wallet's tint
    // The lit end of every flash on this screen — the same cream UiKit.Button lights its label with,
    // so a number that just changed and a button under the cursor speak with one voice.
    private static readonly Color Flare = new(0xF6, 0xEA, 0xC6);

    private readonly UiKit _ui;
    public WarrenScreen(UiKit ui) => _ui = ui;

    // ── Host-set each frame ───────────────────────────────────────────────────────────────────────
    public Warren Warren { get; set; } = null!;
    public long GleamOwned { get; set; }
    public long DustOwned { get; set; }
    public bool DevWarrenDebug { get; set; }

    /// <summary>The deepest wave held anywhere — the number the facility ceiling comes from. Host-set.</summary>
    public int DeepestWave { get; set; }

    /// <summary>What the last real absence paid, or null when there was none. Host-set.</summary>
    public WelcomeSummary? LastReturn { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private FacilityKind? _upgradeRequest;
    public FacilityKind? ConsumeUpgrade() { var r = _upgradeRequest; _upgradeRequest = null; return r; }

    /// <summary>
    /// The sound this screen owes, taken once. The HOST owns audio (UI polish brief §86–§87): the screen
    /// names what just happened and the host plays it, the same shape TraitsScreen hands back.
    /// </summary>
    /// <remarks>
    /// Two moments, both semantic and both fired outside <c>Draw</c>'s per-frame path: an upgrade that
    /// LANDED (the model's level actually moved) is <c>sfx_upgrade</c>; an upgrade the player asked for
    /// and could not have — the capped UPGRADE button, a locked card — is <c>sfx_error</c>. Nothing here
    /// plays a sound for merely looking at a card.
    /// </remarks>
    private string? _cue;

    /// <summary>Take the pending sound cue, if any. Call it after <see cref="Draw"/>, once a frame.</summary>
    public string? ConsumeCue() { var c = _cue; _cue = null; return c; }

    private FacilityKind _selected = FacilityKind.Nursery;

    // ══ FEEDBACK (UI polish P3–P6, brief §22–§38) ════════════════════════════════════════════════
    //
    // The screen used to answer an upgrade by simply drawing different numbers on the next frame: the
    // level was one higher, the figure was bigger, the wallet was smaller, and NOTHING said that you
    // had done that. This section is the whole of what changed — no new label, no new panel, no new
    // copy (§22): the same words, shown CHANGING.
    //
    // Every timer belongs to UiMotion, which the host ticks once a frame before anything draws, so
    // Reduced Motion collapses the eases here and the durations stay inside the brief's bands (§31).
    // A pulse is armed at the semantic moment — the model's level moved, the player asked for
    // something they cannot have — and never by the act of drawing (LAW: once per event).

    /// <summary>
    /// What this screen animates: the four things an upgrade makes move — each keyed separately, because
    /// they do not all last the same time — and the reason a refused one lights.
    /// </summary>
    private enum Feed { Level, Output, Cost, Frame, Refuse }

    /// <summary>A stable key per facility per moving thing — the same shape <see cref="UiMotion.KeyOf"/> makes for a rect.</summary>
    private static int FeedKey(FacilityKind kind, Feed what) => HashCode.Combine(0x5741_2E, (int)kind, (int)what);

    /// <summary>
    /// What an upgrade CHANGED — kept so the screen can show the change rather than only the result.
    /// </summary>
    /// <remarks>
    /// Snapshotted when the player clicks UPGRADE (the values that are true one instant BEFORE the
    /// spend), because the host applies the upgrade after <see cref="Draw"/> returns and by the next
    /// frame the old numbers are gone. <c>Milestone</c> is read from Core — the facility's own
    /// <c>MilestoneTier</c> crossing — never from a "% 5" written here.
    /// </remarks>
    private readonly record struct Landed(FacilityKind Kind, int FromOutput, int FromCostGleam, int FromCostDust,
                                          long FromGleam, long FromDust, bool Milestone);

    /// <summary>The upgrade whose feedback is currently running, or null when the screen is at rest.</summary>
    private Landed? _landed;

    /// <summary>The click that asked for an upgrade, waiting one frame for the host to apply it.</summary>
    private (FacilityKind Kind, int Level, int Tier, int Output, WarrenCost Cost, long Gleam, long Dust)? _asked;

    /// <summary>The facility whose refusal is lit — a capped or unaffordable UPGRADE, or a locked card.</summary>
    private FacilityKind? _refused;

    /// <summary>
    /// Fire the feedback for an upgrade that actually landed, and let a finished one go.
    /// </summary>
    /// <remarks>
    /// The screen has no <c>Update</c> of its own (the host draws it and consumes its request), so the
    /// EVENT it keys on is the model's own change: the level the player asked for is now on the
    /// facility. That can happen exactly once per request, which is what keeps the pulse one-per-event
    /// rather than something the draw pass re-arms.
    /// </remarks>
    private void Settle()
    {
        if (_asked is { } a)
        {
            _asked = null;
            var f = Warren.Facility(a.Kind);
            if (f.Level > a.Level)
            {
                // A MILESTONE IS STRONGER, not different: the same four things move, the level and the
                // frame for a REWARD rather than a TRANSITION, and the frame's envelope gains a second
                // lobe. The number itself always ticks over a transition — a figure changing is a
                // transition in the brief's table whatever occasioned it (§31).
                var milestone = f.MilestoneTier > a.Tier;
                var span = milestone ? UiMotion.Reward : UiMotion.Transition;
                _landed = new Landed(a.Kind, a.Output, a.Cost.Gleam, a.Cost.Dust, a.Gleam, a.Dust, milestone);
                UiMotion.Flash(FeedKey(a.Kind, Feed.Level), span);
                UiMotion.Flash(FeedKey(a.Kind, Feed.Frame), span);
                UiMotion.Flash(FeedKey(a.Kind, Feed.Output), UiMotion.Transition);
                UiMotion.Flash(FeedKey(a.Kind, Feed.Cost), UiMotion.Transition);
                _cue = "sfx_upgrade";
            }
        }
        if (PosedPhase is not null || PosedRefusePhase is not null) return;   // the rig holds its pose
        if (_landed is { } l && !UiMotion.Pulsing(FeedKey(l.Kind, Feed.Frame))
                             && !UiMotion.Pulsing(FeedKey(l.Kind, Feed.Output))) _landed = null;
        if (_refused is { } r && !UiMotion.Pulsing(FeedKey(r, Feed.Refuse))) _refused = null;
    }

    /// <summary>Snapshot what is true before the spend, and ask the host for the upgrade.</summary>
    private void Ask(Facility f)
    {
        _upgradeRequest = f.Kind;
        _asked = (f.Kind, f.Level, f.MilestoneTier, Boosted(f), f.UpgradeCost(), GleamOwned, DustOwned);
    }

    /// <summary>An action the player asked for and cannot have: one sound, one pulse on the reason.</summary>
    private void Refuse(FacilityKind kind)
    {
        _refused = kind;
        UiMotion.Flash(FeedKey(kind, Feed.Refuse), UiMotion.Transition);
        _cue = "sfx_error";
    }

    /// <summary>
    /// The cards whose hover is still fading out — the only ones that still have a question to ask
    /// <see cref="UiMotion"/>.
    /// </summary>
    private readonly HashSet<int> _cooling = new(8);

    /// <summary>
    /// A card's hover lift: 0 at rest, easing to 1 under the cursor and back down when it leaves.
    /// </summary>
    /// <remarks>
    /// IT ONLY ASKS WHILE SOMETHING IS MOVING, and that is not a micro-optimisation. UiMotion drops an
    /// ease the moment it settles at 0, and a MISSING key is assumed to have been at the target's
    /// opposite — so asking "ease to 0" for a control that is merely at rest starts a fresh fade from 1
    /// every time the last one finishes: 0.93 · 0.74 · 0.50 · 0.26 · 0.07 · 0 · 0.93 … , a six-frame
    /// sawtooth that never ends. (Probed 2026-09-02; the capture rig shoots frame 60, a multiple of six,
    /// which is exactly the trough — which is why no screenshot has ever shown it.) A card that has not
    /// been hovered since its last fade ended therefore asks for nothing and draws nothing, which is
    /// what "nothing flashes continuously" requires. The same guard is owed by every caller of
    /// UiMotion.Ease with a zero target — UiKit.Button included — and belongs in the kit, not here.
    /// </remarks>
    private float Hover(Rectangle card, bool hot)
    {
        var key = UiMotion.KeyOf(card);
        if (hot) { _cooling.Add(key); return UiMotion.Ease(key, 1f); }
        if (!_cooling.Contains(key)) return 0f;
        var lift = UiMotion.Ease(key, 0f);
        if (lift <= 0f) _cooling.Remove(key);
        return lift;
    }

    /// <summary>How lit one part of a landed upgrade is: 1 the instant it fired, 0 when it is over.</summary>
    /// <remarks>
    /// UNDER THE RIG the four pulses are read at ONE INSTANT rather than at one phase. They do not all
    /// last the same time — a milestone's level and frame run for a reward while the figures tick over a
    /// transition — so freezing them all at "half" would photograph a moment that never happens. The dial
    /// is the LONGEST pulse's value (1 = the instant it fired) and the shorter ones are derived from the
    /// same elapsed time, which is exactly what a real frame would have caught.
    /// </remarks>
    private float Lit(FacilityKind kind, Feed what)
    {
        if (_landed is not { } l || l.Kind != kind) return 0f;
        var key = FeedKey(kind, what);
        if (PosedPhase is not { } dial) return UiMotion.Pulse(key);
        var longest = l.Milestone ? UiMotion.Reward : UiMotion.Transition;
        var mine = what is Feed.Level or Feed.Frame ? longest : UiMotion.Transition;
        return Math.Clamp(1f - (1f - dial) * longest / mine, 0f, 1f);
    }

    /// <summary>How lit a refusal is, for the card or the line that says why.</summary>
    private float RefusalLit(FacilityKind kind)
        => _refused == kind ? PosedRefusePhase ?? UiMotion.Pulse(FeedKey(kind, Feed.Refuse)) : 0f;

    /// <summary>
    /// A number ON ITS WAY from one value to another. <paramref name="p"/> is a pulse — 1 the instant it
    /// fired, 0 when it is done — so the figure leaves <paramref name="from"/> and arrives at
    /// <paramref name="to"/>, on the house easing curve.
    /// </summary>
    /// <remarks>
    /// Reduced Motion has no journey, only the destination: the END STATE is identical either way,
    /// which is the whole contract a collapsed animation has to keep (brief §32).
    /// </remarks>
    public static long Ticked(long from, long to, float p)
        => UiMotion.Reduced || p <= 0f ? to : to + (long)MathF.Round((from - to) * UiMotion.Smooth(Math.Clamp(p, 0f, 1f)));

    /// <summary>
    /// The envelope a card frame's one-shot glow follows: <paramref name="lobes"/> humps over the
    /// pulse's life, each quieter than the last.
    /// </summary>
    /// <remarks>
    /// A MILESTONE'S "extra pulse" is a second hump in ONE pulse, not a second <see cref="UiMotion.Flash"/>
    /// armed when the first ends — re-arming from the draw pass is exactly the "flashes continuously"
    /// the brief forbids, and a pulse that can only be armed by an event can only play once per event.
    /// </remarks>
    public static float Lobes(float p, int lobes)
    {
        p = Math.Clamp(p, 0f, 1f);
        if (p <= 0f || lobes < 1) return 0f;
        var t = 1f - p;                                            // 0 when it fires → 1 when it ends
        var hump = MathF.Abs(MathF.Sin(t * lobes * MathF.PI));     // `lobes` humps across the life
        return hump * (1f - t * 0.45f);                            // and each one quieter than the last
    }

    // ══ THE RIG'S DIALS ══════════════════════════════════════════════════════════════════════════
    //
    // A state no capture can pose has never been looked at (project rule), and every state above is a
    // TRANSIENT: the shutter fires at frame 60 and an upgrade's feedback is over in a fifth of a
    // second. So the rig can pose the whole sequence and FREEZE it at a chosen instant. The pose runs
    // the real path — it asks for the real upgrade through the host, on the real model — and only the
    // phase the pulses are read at is held still, so what is photographed is the shipping feedback.
    //
    //   RH_SHOT_FACILITY=<FacilityKind>  which card is selected and acted on (default NURSERY)
    //   RH_SHOT_UPGRADE=<0..1>           ask for that facility's upgrade, freeze its feedback at that
    //                                    phase (1 = the instant it fired, 0.5 = mid-tick, 0 = over)
    //   RH_SHOT_REFUSE=<0..1>            ask for an upgrade that is refused, frozen the same way
    //   RH_SHOT_PRESS=1                  the hovered card (and the button under the posed cursor) held
    //   RH_SHOT_SCROLL=<first row>       the inspector scrolled (see PosedScroll below)
    //
    // Used with RH_SHOT_PAGE_MOUSE=x,y (the host's posed cursor) for the hover and pressed states.
    // The shots this pass was checked against:
    //
    //   RH_SHOT_FACILITY=SentryBurrows RH_SHOT_UPGRADE=0.5   warrenready  ... an upgrade, mid-tick
    //   RH_SHOT_FACILITY=RitualNest    RH_SHOT_UPGRADE=0.75  warrenready  ... a milestone, first hump
    //   RH_SHOT_REFUSE=1                                     warren       ... refused: capped
    //   RH_SHOT_FACILITY=RitualNest    RH_SHOT_REFUSE=1      warren       ... refused: cannot pay
    //   RH_SHOT_FACILITY=RitualNest    RH_SHOT_REFUSE=1      warrenfresh  ... refused: a locked card
    //   RH_SHOT_PAGE_MOUSE=520,500 [RH_SHOT_PRESS=1]         warrenready  ... hover / pressed
    //
    // A milestone's two humps peak a quarter and three quarters through its pulse, so 0.75 catches
    // the first at its peak WITH the figures still travelling; 0.5 would photograph the trough.
    //
    private static readonly FacilityKind? PosedFacility =
        Enum.TryParse<FacilityKind>(Environment.GetEnvironmentVariable("RH_SHOT_FACILITY"), true, out var pf) ? pf : null;

    private static readonly float? PosedPhase = PosedPhaseOf("RH_SHOT_UPGRADE");
    private static readonly float? PosedRefusePhase = PosedPhaseOf("RH_SHOT_REFUSE");
    private static readonly bool PosedPress = Environment.GetEnvironmentVariable("RH_SHOT_PRESS") is not null;

    private static float? PosedPhaseOf(string variable)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (raw is null) return null;
        return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, 0f, 1f) : 1f;
    }

    /// <summary>The rig's pose is set up once, on the first frame the screen draws.</summary>
    private bool _posed;

    private void PoseForTheRig()
    {
        if (_posed) return;
        _posed = true;
        if (PosedFacility is { } kind && Warren.IsUnlocked(kind)) _selected = kind;
        if (PosedPhase is not null && Warren.CanUpgrade(_selected, GleamOwned, DustOwned))
            Ask(Warren.Facility(_selected));
        // A LOCKED facility can be the refusal's target without being selected — it has no inspector,
        // and its refusal is the chip on its own card.
        var refused = PosedFacility ?? _selected;
        if (PosedRefusePhase is not null && !Warren.CanUpgrade(refused, GleamOwned, DustOwned))
            Refuse(refused);
    }

    // ── Layout: one summary strip, the grid under it, the inspector beside both. Every edge comes
    //    from UiKit.Page and every size from UiMetrics, so a profile reflows the screen instead of
    //    clipping it. The four page anchors below are the only literals: they are where the page's
    //    chrome (the hint slot, the margins) ends, and they do not grow with the type. ────────────
    private static int Top => UiKit.PageTop;   // the first row under the chrome band, at this profile
    private const int BottomMargin = 60;
    private const int LeftMargin = 38;
    private const int RightInset = 40;

    private static int PanelGap => UiMetrics.Space(20);      // the strip and the grid ↔ the inspector
    private static int StripGap => UiMetrics.Space(16);      // the strip ↔ the grid
    private static int GridPad => UiMetrics.PanelPadding;    // 24 — the plate has no ornament to clear
    private static int CardGap => UiMetrics.Space(18);
    private static int CardPadX => UiMetrics.Space(18);
    private static int ChipHeight => UiMetrics.Control(38);

    /// <summary>
    /// What a card must be given to say UPGRADE READY or NEEDS 12.4K GLEAM at the profile's Body: 200
    /// at 100 %, which is that chip's text plus the chip's and the card's own insets.
    /// </summary>
    private static int CardMinWidth => UiMetrics.Control(200);

    private static int GridMinWidth => CardMinWidth * 4 + CardGap * 3 + GridPad * 2;

    /// <summary>
    /// The inspector takes the house inspector width — but never so much that the eight cards beside
    /// it cannot say their one line. This inspector SCROLLS at the larger profiles (§18), so it can
    /// afford a shorter line; a card cannot scroll, so it wins the room.
    /// </summary>
    private static int InspectorW =>
        Math.Min(UiMetrics.InspectorWidth(UiKit.Page.Width),
                 UiKit.Page.Width - RightInset - LeftMargin - PanelGap - GridMinWidth);

    private static Rectangle InspectorPanel =>
        new(UiKit.PageRight(RightInset) - InspectorW, Top, InspectorW, UiKit.PageBottom(BottomMargin) - Top);

    private static Rectangle SummaryStrip =>
        new(LeftMargin, Top, InspectorPanel.X - PanelGap - LeftMargin, StripHeight);

    private static Rectangle GridPanel =>
        new(LeftMargin, Top + StripHeight + StripGap, SummaryStrip.Width,
            UiKit.PageBottom(BottomMargin) - (Top + StripHeight + StripGap));

    // ── THE STRIP'S RHYTHM. Cell C's stack (a caption, the four figures, their names, the multipliers,
    //    the regions line) is the tallest of the three cells, so it sets the strip's height, and every
    //    step in it is a rung's pitch plus a breath — 200 at 100 %, the height it always had. ─────
    private static int StripCaptionY => UiMetrics.Space(16);
    private static int StripGlyphRowY => StripCaptionY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripNamesY => StripGlyphRowY + UiTypography.Pitch(UiTypography.Headline) - 2;
    private static int StripMultiplierY => StripNamesY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripRegionsY => StripMultiplierY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripRuleY => StripRegionsY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    private static int StripAwayY => StripRuleY + UiMetrics.Space(8);
    private static int StripHeight => StripAwayY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Facilities => new[] { GridPanel },
        TourTarget.FacilityDetail => new[] { InspectorPanel },
        TourTarget.WarrenOverview => new[] { SummaryStrip },
        _ => Array.Empty<Rectangle>(),
    };

    private static int CardW => (GridPanel.Width - GridPad * 2 - CardGap * 3) / 4;
    /// <summary>
    /// A card's height: half the grid, but NEVER under the height its rows need. When the page is too
    /// short for two whole rows — 150 % with the notice lane open, where the cards collapsed onto
    /// their own chips — the grid scrolls instead (the mouse wheel over it; a caption says so).
    /// </summary>
    private static int CardH => Math.Max(MinCardH, (GridPanel.Height - GridPad * 2 - CardGap) / 2);

    /// <summary>The least a card can be: name, resource row, the figure with its caption, the chip, and their breaths.</summary>
    private static int MinCardH
        => UiMetrics.Space(12) + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Body)
           + UiMetrics.Space(9) + UiMetrics.IconSize + UiMetrics.Space(4)
           + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Secondary)
           + ChipHeight + UiMetrics.Space(16);

    /// <summary>The caption band at the grid's foot while it scrolls — the cards stop above it.</summary>
    private static int GridCaptionBand => UiTypography.Secondary + UiMetrics.Space(12);

    /// <summary>How much taller the two rows are than the grid's panel — what the wheel may scroll.</summary>
    private static int GridOverflow => Math.Max(0, GridPad * 2 + CardH * 2 + CardGap + GridCaptionBand - GridPanel.Height);

    /// <summary>The grid's scroll, in page pixels down from the top row.</summary>
    private int _gridScroll;

    private RasterizerState? _clip;
    private RasterizerState Clip => _clip ??= new RasterizerState { ScissorTestEnable = true };

    private Rectangle Card(int i) =>
        new(GridPanel.X + GridPad + i % 4 * (CardW + CardGap),
            GridPanel.Y + GridPad + i / 4 * (CardH + CardGap) - _gridScroll, CardW, CardH);

    /// <summary>
    /// WHERE A CARD'S ROWS SIT, from the height it was given. The name, the resource row, the figure and
    /// the chip are always there; the icon plate and the milestone caption take what is left between the
    /// resource row and the figure, in that order — a facility's own art is what tells eight cards
    /// apart at a glance, and the milestone is repeated in the inspector.
    /// </summary>
    private readonly record struct CardRows(int NameY, int RowY, int FigureY, int MilestoneY, Rectangle Plate, Rectangle Chip)
    {
        public bool HasMilestone => MilestoneY >= 0;
        public bool HasPlate => Plate.Height > 0;
    }

    private static CardRows LayoutCard(Rectangle card)
    {
        var nameY = card.Y + UiMetrics.Space(12);
        var rowY = nameY + UiTypography.Pitch(UiTypography.Headline) - 1;
        var rowEnd = rowY + UiTypography.Pitch(UiTypography.Body);
        var chip = new Rectangle(card.X + CardPadX, card.Bottom - UiMetrics.Space(16) - ChipHeight,
                                 card.Width - CardPadX * 2, ChipHeight);
        var plateTop = rowEnd + UiMetrics.Space(5);
        var plateMax = UiMetrics.Control(100);
        var plateMin = UiMetrics.IconSize;

        // The figure with the caption under it, or the figure alone against the chip.
        var milestoneY = chip.Y - 2 - UiTypography.Pitch(UiTypography.Secondary);
        var figureWith = milestoneY - UiTypography.Pitch(UiTypography.Headline) + 2;
        var figureAlone = chip.Y - UiTypography.Pitch(UiTypography.Headline);
        var roomWith = figureWith - UiMetrics.Space(4) - plateTop;
        var roomAlone = figureAlone - UiMetrics.Space(4) - plateTop;

        Rectangle PlateOf(int h)
        {
            // The hex keeps its shape as it shrinks: 88 wide for every 100 tall, as the art was drawn.
            var w = Math.Min(UiMetrics.Control(88), h * 88 / 100);
            return new Rectangle(card.Center.X - w / 2, plateTop, w, h);
        }

        if (roomWith >= plateMin)
            return new CardRows(nameY, rowY, figureWith, milestoneY, PlateOf(Math.Min(plateMax, roomWith)), chip);
        if (roomAlone >= plateMin)
            return new CardRows(nameY, rowY, figureAlone, -1, PlateOf(Math.Min(plateMax, roomAlone)), chip);
        if (roomWith >= 0)
            return new CardRows(nameY, rowY, figureWith, milestoneY, Rectangle.Empty, chip);
        return new CardRows(nameY, rowY, figureAlone, -1, Rectangle.Empty, chip);
    }

    private Color ResColor(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => GleamC, WarrenResource.Dust => DustC,
        WarrenResource.Scrap => ScrapC, _ => EssenceC,
    };

    private static string ResName(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => "GLEAM", WarrenResource.Dust => "DUST",
        WarrenResource.Scrap => "SCRAP", _ => "ESSENCE",
    };

    /// <summary>Where this currency is actually spent — the Warren's honest answer to "what for?".</summary>
    private static string SinkLine(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => "GLEAM PAYS FOR TRAINING AND THE FORGE",
        WarrenResource.Dust => "DUST PAYS TO START A HUNT DEEPER",
        WarrenResource.Scrap => "SCRAP PAYS FOR FORGE UPGRADES",
        _ => "ESSENCE PAYS TO SET GEMS IN THE FORGE",
    };

    /// <summary>What this facility actually pays per minute — Core's raw output through Core's own
    /// multiplier, which is what makes a card's figure reconcile with the strip's total.</summary>
    private int Boosted(Facility f) => (int)MathF.Round(Warren.OutputPerMinute(f.Kind));

    /// <summary>What the facility pays after one more level, bought anywhere — the budget's share grows with every level (WarrenBudget).</summary>
    private int BoostedNext(Facility f) => (int)MathF.Round(Warren.NextLevelOutputPerMinute(f.Kind));

    /// <summary>A rate as the card prints it: whole above ten a minute, a tenth below — Essence trickles.</summary>
    private static string Rate(float perMinute)
        => perMinute >= 10f ? Ab((long)MathF.Round(perMinute)) : perMinute.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The now → next pair. When both round to the same whole number — a deep Warren's share climbs by
    /// less each level — the tenths are shown, so "+92 → +92" never claims a level buys nothing.
    /// </summary>
    private static (string Now, string Next) RatePair(float now, float next)
    {
        if (now >= 10f && MathF.Round(now) == MathF.Round(next))
            return (now.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    next.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        return (Rate(now), Rate(next));
    }

    /// <summary>
    /// The currency's real icon (Gleam coin, Memory Dust, the Forge's material gems), the tinted
    /// diamond only when the art is missing.
    /// </summary>
    private void ResGlyph(SpriteBatch b, Rectangle box, WarrenResource? r, Color? fallback = null)
    {
        var key = r switch
        {
            WarrenResource.Gleam => "ui_gleam_coin",
            WarrenResource.Dust => "ui_memory_dust",
            WarrenResource.Scrap => "mat_scrap",
            WarrenResource.Essence => "mat_essence",
            _ => "",
        };
        if (key.Length > 0 && _ui.Assets.Get(key) is { } tex) { b.Draw(tex, box, Color.White); return; }
        _ui.Diamond(b, box, fallback ?? (r is { } rr ? ResColor(rr) : Dim));
    }

    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    private void Outline(SpriteBatch b, Rectangle r, Color c, int px)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, px), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - px, r.Width, px), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, px, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - px, r.Y, px, r.Height), c);
    }

    private string? _tip;
    private Point _tipAt;

    private void Tip(Rectangle r, Point hit, string text)
    {
        if (r.Contains(hit)) { _tip = text; _tipAt = hit; }
    }

    // The away line is built only when the summary it describes changes — this screen redraws sixty
    // times a second and the string is the same every one of them.
    private WelcomeSummary? _awayFrom;
    // Seeded with the never-been-away line: both fields start null, so a cache keyed on "did it
    // change?" would never build the first string and the row would draw empty.
    private string _awayLine = "";
    private bool _awayBuilt;

    // THE CAMP'S CAPACITY, in the Warren's own words (OfflineCamp, 2026-09-06): what an absence holds
    // now, and what one more facility level buys — the offline dimension every upgrade improves.
    private string AwayNever =>
        $"THE CAMP HOLDS {OfflineCamp.HoursText(OfflineCamp.HoursFor(Warren))} OF HUNTING WHILE THE GAME IS CLOSED — "
        + $"EACH FACILITY LEVEL HOLDS {OfflineCamp.HoursPerFacilityLevel * 60f:0} MINUTES MORE, UP TO {OfflineCamp.MaxHours:0} HOURS";

    // ── THE WHEEL. The host latches the wheel once a frame and hands it to the screens whose Update
    //    takes it; this screen's entry point predates any of them and takes only the cursor and the
    //    click, so the notches are latched here from the same source the host reads. It is the wheel
    //    only — the cursor is the parameter, hit-tested as it is. (A `wheel` argument on Draw, passed
    //    from Game1.MouseWheel, is the shared shape this should take.) ─────────────────────────────
    private int _wheelPrev;
    private long _wheelSeenAt;

    /// <summary>
    /// A gap longer than this between two frames means the screen was not being drawn — the player was
    /// elsewhere — and the wheel they turned there must not arrive here as one leap through the list.
    /// </summary>
    private const int WheelResyncMs = 250;

    private int WheelNotches()
    {
        var v = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
        var now = Environment.TickCount64;
        var stale = now - _wheelSeenAt > WheelResyncMs;   // also the first frame ever, which latches only
        _wheelSeenAt = now;
        var notches = stale ? 0 : (v - _wheelPrev) / 120;   // one notch is 120, as Game1 reads it
        _wheelPrev = v;
        return notches;
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // The cursor arrives in page space (Game1.PageCursor); it is hit-tested as it is.
        var hit = mouse;
        _tip = null;
        // The host applied (or refused) last frame's request between then and now: light what changed
        // BEFORE this frame's clicks can ask for anything else.
        Settle();
        PoseForTheRig();
        // The rig cannot hold a mouse button down, so the PRESSED state of every control on the page —
        // this screen's cards and the kit's own button — would be unphotographable without this.
        if (PosedPress) UiKit.MouseHeld = true;
        // Latched every frame so a notch turned over the grid is not applied later over the inspector.
        var notches = WheelNotches();
        var wheel = InspectorPanel.Contains(hit) ? notches : 0;
        if (GridPanel.Contains(hit) && notches != 0)
            _gridScroll = Math.Clamp(_gridScroll - notches * UiMetrics.Control(48), 0, GridOverflow);
        _gridScroll = Math.Clamp(_gridScroll, 0, GridOverflow);   // the page may have grown back

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0B, 0x09, 0x08, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "WARREN", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // (The subtitle is gone. What it promised — the place earns while you are away — is now the
        //  strip's own last line, said with the figures from the absence that actually happened.)

        DrawSummary(b);
        DrawGrid(b, hit, clicked);
        DrawInspector(b, hit, clicked, wheel);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
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
    /// <summary>
    /// THE WARREN'S CREST: its own drawn icon (a lit burrow mouth) with a GOLD PROGRESS ARC around it —
    /// the arc closes as the warren fills its level, so the badge shows how far along the place is
    /// before a number is read.
    /// </summary>
    /// <remarks>
    /// The level's numeral used to be struck inside a medallion. That is not what a crest is for
    /// (playtest 2026-08-30: "I did not say write the level on the icon — make a warren icon that
    /// conveys its progress, think of it as a medallion"), and the number is already beside it in words.
    /// The arc starts at twelve o'clock and runs clockwise, the same direction as the hunt's cooldowns.
    /// </remarks>
    private void LevelBadge(SpriteBatch b, Rectangle box, float progress)
    {
        if (!_ui.Icon(b, "icon_warren_crest", box, Color.White)) _ui.Diamond(b, box, UiInk.Empty);

        var centre = new Vector2(box.Center.X, box.Center.Y);
        var radius = box.Width * 0.54f;
        const int segments = 96;
        var filled = Math.Clamp(progress, 0f, 1f);
        for (var k = 0; k < segments; k++)
        {
            var t0 = k / (float)segments;
            var a0 = -MathF.PI / 2f + MathF.Tau * t0;
            var a1 = -MathF.PI / 2f + MathF.Tau * (k + 1) / segments;
            var lit = t0 < filled;
            var p0 = centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            _ui.LineSeg(b, p0, p1, lit ? 5f : 3f, lit ? Gold : Dim);
        }
    }

    // ── THE SUMMARY STRIP. One region, five facts, and the away line. ────────────────────────
    //
    // It replaces a 388 px OVERVIEW column and a 920 px WARREN BONUSES panel that between them spent
    // half the screen on five derived percentages and the formula behind them — a question nobody
    // asked — while the ceiling that stops every upgrade was explained eight times, once per card,
    // and the away earnings were not shown at all.
    private void DrawSummary(SpriteBatch b)
    {
        var strip = SummaryStrip;
        _ui.Plate(b, strip);
        var ix = strip.X + GridPad;
        var iw = strip.Width - GridPad * 2;
        // Proportional cells: written as fixed widths the third one ran off the strip at 125 %.
        var aw = iw * 26 / 100;
        var bw = iw * 28 / 100;
        var cw = iw - aw - bw;
        var ax = ix;
        var bx = ax + aw;
        var cx = bx + bw;
        // Everything in the strip ends above the away line's rule.
        var stripFloor = strip.Y + StripRuleY - 2;

        // CELL A — the Warren's own rank.
        var pct = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 1f;
        var badge = UiMetrics.Control(72);
        LevelBadge(b, new Rectangle(ax, strip.Y + UiMetrics.Space(20), badge, badge), pct);
        var labelX = ax + badge + UiMetrics.Space(14);
        var labelW = aw - badge - UiMetrics.Space(14) - UiMetrics.Space(8);
        // The WORDS stop a breath short of the hairline that closes the cell; only the bar may run to
        // the cell's edge. Measured against the bar's width, "0 OF 3,000 TO LEVEL 2" sat on the hairline
        // at 125 %.
        var wordsW = bx - UiMetrics.Space(14) - UiMetrics.Space(6) - labelX;
        var labelY = strip.Y + UiMetrics.Space(24);
        // A SHORTER SENTENCE, NEVER A SHORTER NUMBER. Shortened to fit, this read "WARREN LEV..."
        // at 125 % — the one thing the cell exists to say was the half that got cut. The word WARREN
        // is the screen's own title, so it is what goes.
        var levelLabel = $"WARREN LEVEL {Warren.Level}";
        if (_ui.MeasureBig(levelLabel, UiTypography.Headline) > wordsW) levelLabel = $"LEVEL {Warren.Level}";
        _ui.TextBig(b, levelLabel, labelX, labelY, Bone, UiTypography.Headline);
        var bar = new Rectangle(labelX, labelY + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2),
                                labelW, UiMetrics.Control(26));
        _ui.BarArt(b, bar, pct, "xp");
        // UNDER the bar, not inside it: a number printed on a bar's own fill is a number read against
        // whatever colour happens to be behind it.
        var xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0} TO LEVEL {Warren.Level + 1}";
        if (_ui.MeasureBig(xpLabel, UiTypography.Secondary) > wordsW)
            xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0}";
        _ui.TextBig(b, xpLabel, labelX, bar.Bottom + UiMetrics.Space(8), Slate, UiTypography.Secondary);

        // TWO HAIRLINES between the three cells. Without them cell B's right-aligned figures sat
        // against cell C's first words and read as C's numbers.
        var hairTop = strip.Y + UiMetrics.Space(20);
        _ui.Fill(b, new Rectangle(bx - UiMetrics.Space(14), hairTop, 1, stripFloor - hairTop), Dim);
        _ui.Fill(b, new Rectangle(cx - UiMetrics.Space(14), hairTop, 1, stripFloor - hairTop), Dim);

        // CELL B — the ceiling, explained ONCE for all eight cards.
        // THE LABEL AND ITS NUMBER SHARE A ROW WHEN THEY FIT AND THE LABEL WRAPS BESIDE THE NUMBER
        // WHEN THEY DO NOT. Held on one row at every width, "FACILITIES CAN REACH LEVEL" was cut to
        // "FACILITIES CAN REACH L..." at 125 %, which says nothing at all. Stacked — the label over
        // the number — it fit at 125 % but at 150 % the stack pushed the sentence under it through
        // the strip's floor; two caption lines beside a headline figure are shorter than a caption
        // OVER a figure, and that difference is the sentence's second line.
        var factY = strip.Y + UiMetrics.Space(18);
        var factRoom = bw - UiMetrics.Space(20);
        var factGap = UiMetrics.Space(20);

        void Fact(string label, string value)
        {
            var vw = _ui.MeasureBig(value, UiTypography.Headline);
            if (_ui.MeasureBig(label, UiTypography.Secondary) + vw + factGap <= factRoom)
            {
                _ui.TextBig(b, label, bx, factY + UiMetrics.Space(8), Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, value, bx + bw - UiMetrics.Space(28), factY, Bone, UiTypography.Headline);
                factY += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
                return;
            }
            var beside = _ui.WrapBig(label, factRoom - vw - factGap, UiTypography.Secondary);
            if (beside.Count <= 2)
            {
                _ui.TextRightBig(b, value, bx + bw - UiMetrics.Space(28), factY, Bone, UiTypography.Headline);
                var ly = factY;
                foreach (var l in beside)
                {
                    _ui.TextBig(b, l, bx, ly, Slate, UiTypography.Secondary);
                    ly += UiTypography.Pitch(UiTypography.Secondary);
                }
                factY = Math.Max(ly, factY + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6));
                return;
            }
            // Only when even two lines cannot hold the label beside its number: the label over the number.
            _ui.TextBig(b, _ui.ShortenBig(label, factRoom, UiTypography.Secondary), bx, factY, Slate, UiTypography.Secondary);
            factY += UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, value, bx, factY, Bone, UiTypography.Headline);
            factY += UiTypography.Pitch(UiTypography.Headline);
        }

        Fact("YOUR DEEPEST WAVE", $"{DeepestWave}");
        Fact("FACILITIES CAN REACH LEVEL", $"{Warren.FacilityLevelCap}");
        var ruleY = factY + UiMetrics.Space(4);
        var ruleW = bw - UiMetrics.Space(8);
        var ruleLines = _ui.WrapBig($"EVERY {Warren.DepthPerFacilityLevel} WAVES DEEPER RAISES IT BY ONE LEVEL", ruleW, UiTypography.Secondary);
        // As many of its lines as the strip's floor allows, two at most — and a sentence that loses a
        // line SAYS SO: the last line it keeps ends in an ellipsis, never mid-thought.
        var ruleFit = Math.Min(Math.Min(ruleLines.Count, 2), Math.Max(0, (stripFloor - ruleY) / UiTypography.Pitch(UiTypography.Secondary)));
        for (var n = 0; n < ruleFit; n++)
        {
            var text = n == ruleFit - 1 && ruleFit < ruleLines.Count
                ? _ui.ShortenBig(string.Join(" ", ruleLines.Skip(n)), ruleW, UiTypography.Secondary)
                : ruleLines[n];
            _ui.TextBig(b, text, bx, ruleY, Slate, UiTypography.Secondary);
            ruleY += UiTypography.Pitch(UiTypography.Secondary);
        }

        // CELL C — what it all makes, per minute, with the bonuses already in it.
        _ui.TextBig(b, "EVERY MINUTE, WHILE THE GAME IS OPEN", cx, strip.Y + StripCaptionY, Slate, UiTypography.Secondary);
        var chw = cw / 4;
        var glyph = UiMetrics.Control(28);
        var k = 0;
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Dust, WarrenResource.Scrap, WarrenResource.Essence })
        {
            var v = Warren.ProductionPerMinute(r);
            ResGlyph(b, new Rectangle(cx + k * chw, strip.Y + StripGlyphRowY, glyph, glyph), r);
            // A zero rate is drawn in the placeholder ink — the honest fresh-Warren state, where both
            // material facilities are still locked.
            _ui.TextBig(b, $"+{Rate(v)}", cx + k * chw + glyph + UiMetrics.Space(6), strip.Y + StripGlyphRowY - UiMetrics.Space(4),
                        v > 0f ? Bone : UiInk.Empty, UiTypography.Headline);
            _ui.TextBig(b, ResName(r), cx + k * chw, strip.Y + StripNamesY, v > 0f ? ResColor(r) : Slate, UiTypography.Secondary);
            k++;
        }
        // THE CAMP, in two lines (the rate model, 2026-09-06): what an absence holds and at what share
        // of live pay, then what every level buys — the three things a level moves, in player words.
        // The figures are Core's (OfflineCamp, WarrenBudget); nothing here owns a number.
        _ui.TextBig(b, _ui.ShortenBig(
                        $"WHILE THE GAME IS CLOSED IT PAYS {OfflineCamp.EfficiencyFor(Warren):P0} OF LIVE PAY  (+{OfflineCamp.EfficiencyPerFacilityLevel:P1} A LEVEL)",
                        cw, UiTypography.Secondary),
                    cx, strip.Y + StripMultiplierY, Bone, UiTypography.Secondary);
        _ui.TextBig(b, _ui.ShortenBig(
                        $"EACH LEVEL: A LARGER SHARE OF THE HUNT'S PAY  ({Warren.Share:P0} NOW)",
                        cw, UiTypography.Secondary),
                    cx, strip.Y + StripRegionsY, Slate, UiTypography.Secondary);

        // THE AWAY LINE — the screen's own question, answered with the figures from the absence that
        // actually happened. The host already had them; this screen never showed them.
        _ui.Fill(b, new Rectangle(ix, strip.Y + StripRuleY, iw, 1), Dim);
        if (!_awayBuilt || !Equals(_awayFrom, LastReturn))
        {
            _awayBuilt = true;
            _awayFrom = LastReturn;
            _awayLine = LastReturn is { } w && w.WarrenParts().Count > 0
                ? $"WHILE YOU WERE AWAY {WelcomeSummary.AwayText(w.AwaySeconds)} THE WARREN MADE {string.Join("  ·  ", w.WarrenParts())}"
                : AwayNever;
        }
        _ui.TextBig(b, _ui.ShortenBig(_awayLine.Length > 0 ? _awayLine : AwayNever, iw, UiTypography.Secondary), ix, strip.Y + StripAwayY, Bone, UiTypography.Secondary);
    }

    // ── THE GRID. Eight cards, each answering "can I upgrade this one?" without being clicked. ──
    private void DrawGrid(SpriteBatch b, Point hit, bool clicked)
    {
        // QUIET, not ornate: this is a grid of things to pick between, and it wore the gold frame
        // while the column that explains them wore the brown.
        _ui.Plate(b, GridPanel);
        var scrolls = GridOverflow > 0;
        if (scrolls)
        {
            // The cards are clipped ABOVE the caption band at the panel's foot, so the caption never
            // prints through a card's own rows.
            var clipTo = new Rectangle(GridPanel.X, GridPanel.Y, GridPanel.Width, GridPanel.Height - GridCaptionBand);
            b.End();
            _ui.Device.ScissorRectangle = Rectangle.Intersect(Game1.OverlayToCanvas(clipTo, Vector2.Zero), _ui.Device.Viewport.Bounds);
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, Clip, null, Game1.OverlayTransform(Vector2.Zero));
            if (!GridPanel.Contains(hit)) hit = new Point(-1, -1);   // a card scrolled under the strip takes nothing
        }
        var lockGlyph = UiMetrics.Control(20);
        // WHERE A LOCKED CARD WEARS ITS BADGE is decided once for the grid, not once per card: at 125 %
        // RITUAL NEST kept LOCKED beside its name while its three neighbours, whose names are longer,
        // had moved it down a row, and the four read as two kinds of card.
        var lockedRoom = CardPadX + _ui.MeasureBig("LOCKED", UiTypography.Caption) + UiMetrics.Space(22);
        var lockedBadgeBelow = false;
        foreach (var f in Warren.AllFacilities)
            if (!Warren.IsUnlocked(f.Kind) && _ui.MeasureBig(f.Info.Name, UiTypography.Headline) > CardW - lockedRoom - UiMetrics.Space(20))
                lockedBadgeBelow = true;
        var i = 0;
        foreach (var f in Warren.AllFacilities)
        {
            var index = i++;
            var card = Card(index);
            var open = Warren.IsUnlocked(f.Kind);
            // ── THE STANDARD STATES (§25–§29), on a card the kit does not draw for us. HOVER is a thin
            //    luminance lift eased in over ~100 ms, the same one UiKit.Button gets, so a card is
            //    noticed without jumping. PRESSED drops the whole card two pixels and darkens it for
            //    exactly as long as the button is held. SELECTED keeps its persistent gold frame, which
            //    hover can never be mistaken for. DISABLED — a locked facility — takes neither hover nor
            //    press and sits back under a wash, so "not yet" reads before a word of it is read.
            var hot = open && card.Contains(hit);
            var lift = Hover(card, hot);
            var held = hot && UiKit.MouseHeld;
            var drawn = held ? new Rectangle(card.X, card.Y + 2, card.Width, card.Height) : card;
            var inner = new Rectangle(drawn.X + 3, drawn.Y + 3, drawn.Width - 6, drawn.Height - 6);
            var rows = LayoutCard(drawn);
            var chip = rows.Chip;
            // The chip's icon and text sit centred in the chip's own height, whatever the profile made it.
            var chipIcon = new Rectangle(chip.X + UiMetrics.Space(10), chip.Y + (chip.Height - lockGlyph) / 2, lockGlyph, lockGlyph);
            var chipTextY = chip.Y + (chip.Height - UiTypography.Body) / 2;
            var chipTextX = chipIcon.Right + UiMetrics.Space(8);

            // Facilities unlock by conquest. A locked card is a promise, not a faucet — it says what
            // would open it, by NAME, and takes no clicks.
            if (!open)
            {
                _ui.Plate(b, card);
                // DISABLED, at a glance: the card's ground goes back a step so the seven words on it are
                // read as a promise rather than as a faucet. The wash sits UNDER the content — a
                // disabled control has to stay readable and has to say why (§29), and the reason is the
                // chip at the bottom of this card.
                _ui.Fill(b, inner, Color.Black * 0.25f);
                // A CLICK ON A LOCKED CARD IS AN ANSWER, not silence: the reason lights and the host
                // plays the refusal. Selection is unchanged — a locked facility has no inspector.
                if (UiKit.ClickedIn(card, hit, clicked)) Refuse(f.Kind);
                var lockedLit = RefusalLit(f.Kind);
                // Centred in the space the LOCKED badge leaves, not in the whole card — BREEDING
                // CHAMBER at Headline reached the badge and the two words touched. And when that space
                // cannot hold a locked name (125 % and up: "SCAVENGE...", "HOARD VAU..."), the badge
                // moves down to the resource row — the row where an open card says its LEVEL, empty on
                // a locked one — and the name is centred in the whole card like every other name.
                if (!lockedBadgeBelow)
                {
                    _ui.TextCenterBig(b, f.Info.Name, card.X + (card.Width - lockedRoom) / 2, rows.NameY, Bone, UiTypography.Headline);
                    _ui.TextRightBig(b, "LOCKED", card.Right - CardPadX, card.Y + UiMetrics.Space(16), UiInk.Disabled, UiTypography.Caption);
                }
                else
                {
                    _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, card.Width - UiMetrics.Space(24), UiTypography.Headline),
                                      card.Center.X, rows.NameY, Bone, UiTypography.Headline);
                    _ui.TextRightBig(b, "LOCKED", card.Right - CardPadX, rows.RowY + (UiTypography.Body - UiTypography.Caption) / 2,
                                     UiInk.Disabled, UiTypography.Caption);
                }

                if (rows.HasPlate)
                {
                    var plateBox = rows.Plate;
                    _ui.Hex(b, plateBox, UiInk.Empty * 0.4f);
                    var lockBox = Math.Min(UiMetrics.IconSize, plateBox.Height);
                    _ui.Icon(b, "ui_slot_locked",
                             new Rectangle(plateBox.Center.X - lockBox / 2, plateBox.Center.Y - lockBox / 2, lockBox, lockBox), UiInk.Empty);
                }

                // WHICH conquest, not "another region": the chain is linear, so the next one is known.
                // Facility k opens at ConqueredRegions >= k - 1 (UnlockedFacilityCount = 2 + conquered),
                // so the shortfall is measured from the card's OWN index — not the loop counter, which
                // has already moved on and made every locked card ask for one region too many.
                var need = index - 1 - Warren.ConqueredRegions;
                var next = Warren.ConqueredRegions >= 0 && Warren.ConqueredRegions < Regions.All.Count
                    ? Regions.All[Warren.ConqueredRegions].Name : "";
                var line = need <= 1 && next.Length > 0 ? $"CONQUER {next}"
                         : need <= 1 ? "CONQUER ONE MORE REGION"
                         : $"CONQUER {need} MORE REGIONS";
                // A SHORTER SENTENCE BEFORE AN ELLIPSIS. At 125 % the chip read "CONQUER 2 MOR..." and
                // "CONQUER CINDE..." — the verb kept and the one word that mattered cut. The chip's lock
                // glyph already says "a requirement", so the verb is what goes: "2 MORE REGIONS",
                // "CINDERWORKS". The tooltip keeps the whole line.
                var chipRoom = chip.Right - UiMetrics.Space(10) - chipTextX;
                var chipLine = _ui.MeasureBig(line, UiTypography.Body) <= chipRoom ? line
                             : _ui.ShortenBig(need <= 1 && next.Length > 0 ? next
                                              : need <= 1 ? "ONE MORE REGION"
                                              : $"{need} MORE REGIONS", chipRoom, UiTypography.Body);
                _ui.Plate(b, chip);
                // The refused click pulses the chip that holds the answer — once, and on the reason
                // itself, so the eye is sent to the sentence rather than to a noise somewhere else.
                if (lockedLit > 0f)
                    _ui.Fill(b, new Rectangle(chip.X + 3, chip.Y + 3, chip.Width - 6, chip.Height - 6), Ember * (0.30f * lockedLit));
                _ui.Icon(b, "ui_slot_locked", chipIcon, Bone);
                _ui.TextBig(b, chipLine, chipTextX, chipTextY, Color.Lerp(Bone, Ember, 0.7f * lockedLit), UiTypography.Body);
                Tip(card, hit, $"{f.Info.Name} — LOCKED. {line} TO OPEN IT.");
                continue;
            }

            var sel = f.Kind == _selected;
            // A new selection opens its inspector at the top, wherever the last one was scrolled to.
            if (UiKit.ClickedIn(card, hit, clicked) && !sel) { _selected = f.Kind; _inspectorFirst = 0; }
            var rc = ResColor(f.Info.Produces);

            _ui.Plate(b, drawn);
            // HOVER lifts the card's own surface; PRESSED darkens it instead and drops the glow — the
            // two are never on at once, which is what makes them tell apart.
            if (held) _ui.Fill(b, inner, Color.Black * 0.18f);
            else if (lift > 0f) _ui.Fill(b, inner, Color.White * (0.07f * lift));
            // The name is ALWAYS legible: unselected cards drew it in Slate, so "not selected" read as
            // "unavailable" on seven of the eight.
            _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, drawn.Width - UiMetrics.Space(24), UiTypography.Headline),
                              drawn.Center.X, rows.NameY, Bone, UiTypography.Headline);

            ResGlyph(b, new Rectangle(drawn.X + CardPadX, rows.RowY + 2, UiMetrics.IconSmall, UiMetrics.IconSmall), f.Info.Produces, rc);
            _ui.TextBig(b, ResName(f.Info.Produces), drawn.X + CardPadX + UiMetrics.IconSmall + UiMetrics.Space(6), rows.RowY, rc, UiTypography.Body);
            // GOLD ONLY WHEN SELECTED. Every card printed its LEVEL in gold, so selection had nothing
            // left to win with.
            //
            // AND THE LEVEL IS WHAT AN UPGRADE BUYS, so it is what flashes when one lands: one pulse,
            // on the number that changed, behind it and in it. (§36 — the change is shown where the
            // change is, not as a banner somewhere else.)
            var levelLit = Lit(f.Kind, Feed.Level);
            var levelText = $"LEVEL {f.Level}";
            if (levelLit > 0f)
            {
                var lw = _ui.MeasureBig(levelText, UiTypography.Body);
                _ui.Fill(b, new Rectangle(drawn.Right - CardPadX - lw - UiMetrics.Space(6), rows.RowY - 2,
                                          lw + UiMetrics.Space(12), UiTypography.Pitch(UiTypography.Body)),
                         Gold * (0.30f * levelLit));
            }
            _ui.TextRightBig(b, levelText, drawn.Right - CardPadX, rows.RowY,
                             Color.Lerp(sel ? Gold : Bone, Flare, levelLit), UiTypography.Body);

            if (rows.HasPlate)
            {
                var plate = rows.Plate;
                // The hex behind the art warms under the cursor — the hover's luminance lift, on the
                // one part of the card that carries the facility's colour.
                _ui.Hex(b, plate, rc * (sel ? 0.55f : 0.38f + 0.10f * lift));
                var icon = $"icon_facility_{f.Kind.ToString().ToLowerInvariant()}";
                if (!_ui.Icon(b, icon, new Rectangle(plate.X + 2, plate.Y - 4, plate.Width - 4, plate.Height + 8)))
                    _ui.Hex(b, plate, rc);
            }

            // THE FIGURE THE CARD PAYS, with the bonuses in it — the raw one never reconciled with the
            // strip's total, and nothing said which was which.
            //
            // AND IT TRAVELS: an upgrade leaves the old figure and arrives at the new one over a
            // transition, so what the player bought is visibly the thing that moved (§36).
            var outLit = Lit(f.Kind, Feed.Output);
            var pay = _landed is { } paid && paid.Kind == f.Kind
                ? Ticked(paid.FromOutput, Boosted(f), outLit) : Boosted(f);
            var rateNow = Warren.OutputPerMinute(f.Kind);
            _ui.TextBig(b, rateNow < 10f ? $"+{Rate(rateNow)} /min" : $"+{Ab(pay)} /min", drawn.X + CardPadX, rows.FigureY,
                        Color.Lerp(Bone, Flare, outLit), UiTypography.Headline);
            // THE NEXT LEVEL'S PROMISE, under the figure — what the upgrade changes, answered on the card.
            if (rows.HasMilestone)
                _ui.TextBig(b, _ui.ShortenBig($"NEXT LEVEL  +{RatePair(Warren.OutputPerMinute(f.Kind), Warren.NextLevelOutputPerMinute(f.Kind)).Next} /min", drawn.Width - CardPadX * 2, UiTypography.Secondary),
                            drawn.X + CardPadX, rows.MilestoneY, Slate, UiTypography.Secondary);

            // ── THE CHIP: the answer the player had to click eight cards to find. ──
            //
            // It is also where a REFUSED upgrade is answered: asking for a level this facility cannot
            // take pulses the chip that already says why, once. (The words do not change — §22.)
            var refuseLit = RefusalLit(f.Kind);
            var chipInner = new Rectangle(chip.X + 3, chip.Y + 3, chip.Width - 6, chip.Height - 6);
            if (Warren.CanUpgrade(f.Kind, GleamOwned, DustOwned))
            {
                _ui.Plate(b, chip, Gold);
                _ui.TextBig(b, "UPGRADE READY", chip.X + UiMetrics.Space(14), chipTextY, Gold, UiTypography.Body);
            }
            else if (Warren.IsAtLevelCap(f.Kind))
            {
                _ui.Plate(b, chip);
                if (refuseLit > 0f) _ui.Fill(b, chipInner, Ember * (0.30f * refuseLit));
                _ui.Icon(b, "ui_slot_locked", chipIcon, Bone);
                _ui.TextBig(b, _ui.ShortenBig($"REACH WAVE {Warren.DepthForNextLevel(f.Kind)}", chip.Right - UiMetrics.Space(10) - chipTextX, UiTypography.Body),
                            chipTextX, chipTextY, Bone, UiTypography.Body);
            }
            else
            {
                var cost = Warren.UpgradeCost(f.Kind);
                var shortG = cost.Gleam - GleamOwned;
                var shortD = cost.Dust - DustOwned;
                var parts = new List<string>();
                if (shortG > 0) parts.Add($"{Ab(shortG)} GLEAM");
                if (shortD > 0) parts.Add($"{Ab(shortD)} DUST");
                _ui.Plate(b, chip);
                if (refuseLit > 0f) _ui.Fill(b, chipInner, Ember * (0.30f * refuseLit));
                _ui.TextBig(b, _ui.ShortenBig($"NEEDS {string.Join("  ·  ", parts)}", chip.Width - UiMetrics.Space(14) - UiMetrics.Space(10), UiTypography.Body),
                            chip.X + UiMetrics.Space(14), chipTextY, Color.Lerp(Ember, Flare, 0.5f * refuseLit), UiTypography.Body);
            }

            if (sel) Outline(b, drawn, Gold, 3);
            else if (lift > 0f) Outline(b, drawn, Bone * lift, 1);
            // THE ONE-SHOT ON THE FRAME. One hump for an upgrade; TWO for a milestone, inside a single
            // pulse of a reward's length (see Lobes) — a second Flash armed when the first ended would
            // be the draw pass re-arming its own animation.
            //
            // AND UNDER REDUCED MOTION THE SECOND HUMP GOES (brief §32). One hump is the sanctioned
            // pulse — a short fade up and back, which is what UiMotion keeps when the setting is on.
            // Two is a blink-blink, and flashing is the thing the setting exists to stop. A milestone
            // is still the stronger moment either way: its level and its frame run for a REWARD where
            // an ordinary upgrade's run for a transition, and the end state is identical.
            var frameLit = Lobes(Lit(f.Kind, Feed.Frame),
                                 !UiMotion.Reduced && _landed is { } lit && lit.Kind == f.Kind && lit.Milestone ? 2 : 1);
            if (frameLit > 0f)
                Outline(b, new Rectangle(drawn.X - 3, drawn.Y - 3, drawn.Width + 6, drawn.Height + 6), Gold * frameLit, 3);
            Tip(card, hit, $"{f.Info.Name} — {f.Info.Description}");
        }

        if (scrolls)
        {
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Game1.OverlayTransform(Vector2.Zero));
            // The caption every scrolling grid wears (TRAITS, the VAULT), at the panel's foot, only
            // while there is more below.
            _ui.TextBig(b, _gridScroll < GridOverflow ? "MORE BELOW — THE MOUSE WHEEL SCROLLS" : "THE MOUSE WHEEL SCROLLS BACK UP", GridPanel.X + GridPad,
                        GridPanel.Bottom - GridCaptionBand + UiMetrics.Space(4), Slate, UiTypography.Secondary);
            _ui.ScrollBar(b, new Rectangle(GridPanel.Right - UiMetrics.ScrollbarWidth - 2, GridPanel.Y + GridPad, UiMetrics.ScrollbarWidth, GridPanel.Height - GridCaptionBand - GridPad),
                          _gridScroll, GridPanel.Height - GridCaptionBand, GridPanel.Height - GridCaptionBand + GridOverflow);
        }
    }

    // ── THE INSPECTOR — the house grammar (§6). ─────────────────────────────────────────────────
    //
    // Its rows are BUILT first and DRAWN second. Everything above the refusal line is a list of rows
    // with known heights; when the list is taller than the room (which it is at 150 %, and at 125 %
    // for a capped facility), the rows scroll under the wheel with a bar in the lane the text gives
    // up, and the refusal line and the one lit button stay anchored at the bottom where they can
    // always be reached (§17–§18). At 100 % every row fits and nothing moves.

    private enum RowKind { Head, Name, Line, Gap, Rule, Pair, Locked, Cost }

    /// <summary>
    /// One built row. <c>Lit</c> is how much of a one-shot is on it — 1 the instant an upgrade landed,
    /// 0 at rest — which is the only thing this pass added to the inspector's grammar.
    /// </summary>
    private readonly record struct Row(RowKind Kind, int H, string A = "", string B = "", string C = "",
                                       Color Ink = default, int Px = 0, WarrenResource Res = WarrenResource.Gleam,
                                       bool Ok = true, float Lit = 0f);

    private readonly List<Row> _rows = new();
    private int _inspectorFirst = PosedScroll;

    /// <summary>
    /// The capture rig can pose the inspector SCROLLED — RH_SHOT_SCROLL=&lt;first row&gt;, clamped to the
    /// list's end like a wheel would be — so the scrolled state is photographed rather than described.
    /// The rig cannot turn a wheel headlessly, and a state no fixture can pose has never been looked at.
    /// </summary>
    private static readonly int PosedScroll =
        int.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_SCROLL"), out var posed) ? Math.Max(0, posed) : 0;

    private void BuildInspectorRows(Facility f, int w, bool capped, WarrenCost cost)
    {
        _rows.Clear();
        void Head(string s) => _rows.Add(new Row(RowKind.Head, UiTypography.Pitch(UiTypography.Secondary), s, Ink: Slate, Px: UiTypography.Secondary));
        void Line(string s, Color c, int px, int max)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(max))
                _rows.Add(new Row(RowKind.Line, UiTypography.Pitch(px), l, Ink: c, Px: px));
        }
        void Gap() => _rows.Add(new Row(RowKind.Gap, UiMetrics.Space(6)));
        void Rule() => _rows.Add(new Row(RowKind.Rule, UiMetrics.Space(14)));

        Head($"FACILITY · {ResName(f.Info.Produces)}");
        _rows.Add(new Row(RowKind.Name, UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4),
                          _ui.ShortenBig(f.Info.Name, w, UiTypography.Headline), Ink: Bone, Px: UiTypography.Headline));
        Line(f.Info.Description, Bone, UiTypography.Body, 3);
        Gap();
        Rule();

        // THE SAME THREE THINGS THE CARD LIGHTS, lit here too — the inspector is the same facility, and
        // a change shown in one place and not the other reads as two facilities.
        var outLit = Lit(f.Kind, Feed.Output);
        var levelLit = Lit(f.Kind, Feed.Level);
        var costLit = Lit(f.Kind, Feed.Cost);
        var landed = _landed is { } l && l.Kind == f.Kind ? l : (Landed?)null;

        Head("WHAT IT DOES");
        _rows.Add(new Row(RowKind.Pair, UiTypography.Pitch(UiTypography.Headline),
                          $"+{RatePair(Warren.OutputPerMinute(f.Kind), Warren.NextLevelOutputPerMinute(f.Kind)).Now} /min",
                          $"+{RatePair(Warren.OutputPerMinute(f.Kind), Warren.NextLevelOutputPerMinute(f.Kind)).Next} /min", Lit: outLit));
        Line(SinkLine(f.Info.Produces), Bone, UiTypography.Body, 2);
        // THE MODEL, in one player sentence: a level bought ANYWHERE raises every facility, because
        // the whole Warren draws one share of what a hunt at this progression pays (WarrenBudget).
        Line($"A LEVEL BOUGHT ANYWHERE RAISES EVERY FACILITY — THE WARREN EARNS {Warren.Share:P0} OF WHAT A HUNT AT YOUR PROGRESS PAYS", Slate, UiTypography.Secondary, 3);
        // AUTOMATION, WHICH IS THE OTHER THING A LEVEL BUYS. Auto-sell and auto-merge used to be trait
        // purchases; they belong to the Warren, which already owns the account's idle layer. The card
        // states what this facility already does for you, and what its next automation level will do.
        // THE LINE COUNTS ARE THE SENTENCES' OWN, not the two rows every other line here takes. Line()
        // DROPS the wrapped rows past its max with no ellipsis, so at UI SCALE 150 a two-row cap cut
        // "SELLS YOUR COMMON AND UNCOMMON ITEMS WHEN A CHEST IS OPENED." to "...WHEN A CHEST IS" — an
        // unfinished sentence about the one thing this facility now owns. Three rows hold both ACTIVE
        // notes at 150; the AT LEVEL note carries a "AT LEVEL n: " prefix and the longest of them
        // (SCAVENGER RUNS at level 4) needs six.
        if (WarrenAutomation.ActiveNoteFor(Warren, f.Kind) is { Length: > 0 } doing)
            Line(doing, Bone, UiTypography.Body, 3);
        if (WarrenAutomation.NoteFor(Warren, f.Kind, f.Level + 1) is { Length: > 0 } next)
            Line($"AT LEVEL {f.Level + 1}: {next}", Bone, UiTypography.Body, 6);
        Gap();
        Rule();

        // YOU NEED FIRST is drawn ONLY when there is a requirement. It used to hold "INSTANT UPGRADE ·
        // NO WAIT" the rest of the time — filler in the slot reserved for the reason you cannot act.
        if (capped)
        {
            Head("YOU NEED FIRST");
            _rows.Add(new Row(RowKind.Locked, UiTypography.Pitch(UiTypography.Body),
                              $"REACH WAVE {Warren.DepthForNextLevel(_selected)} ON A HUNT", Ink: Bone, Px: UiTypography.Body));
            Gap();
            Rule();
        }

        Head("CURRENT STATE");
        _rows.Add(new Row(RowKind.Line, UiTypography.Pitch(UiTypography.Body), $"LEVEL {f.Level}",
                          Ink: Bone, Px: UiTypography.Body, Lit: levelLit));
        Line($"THE CAMP HOLDS {OfflineCamp.HoursText(OfflineCamp.HoursFor(Warren))} AT {OfflineCamp.EfficiencyFor(Warren):P0} OF LIVE PAY WHILE THE GAME IS CLOSED — THIS LEVEL HOLDS {OfflineCamp.HoursPerFacilityLevel * 60f:0} MINUTES MORE",
             Slate, UiTypography.Secondary, 3);
        Gap();
        Rule();

        Head("WHAT IT COSTS");
        CostRow(WarrenResource.Gleam, "GLEAM", GleamOwned, cost.Gleam, costLit, landed?.FromGleam, landed?.FromCostGleam);
        CostRow(WarrenResource.Dust, "DUST", DustOwned, cost.Dust, costLit, landed?.FromDust, landed?.FromCostDust);
    }

    /// <summary>One cost row: what it takes, what you hold, and how much MORE you need — never an "X".</summary>
    /// <remarks>
    /// WHAT A PURCHASE LOOKS LIKE HERE (§37 — spending reacts where the price is): the row pulses once
    /// and both of its figures travel — what you hold ticks DOWN from the balance you had a moment ago,
    /// and the price ticks up to what the next level asks. The row's WORDS are decided by the real
    /// numbers, never by the travelling ones, so "YOU HAVE" can never turn into a lie mid-tick.
    /// </remarks>
    private void CostRow(WarrenResource res, string label, long owned, int required,
                         float lit = 0f, long? fromOwned = null, int? fromRequired = null)
    {
        var ok = owned >= required;
        var shownRequired = fromRequired is { } fr ? Ticked(fr, required, lit) : required;
        var shownOwned = fromOwned is { } fo ? Ticked(fo, owned, lit) : owned;
        _rows.Add(new Row(RowKind.Cost,
                          UiTypography.Pitch(UiTypography.Body) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4),
                          label,
                          shownRequired.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                          ok ? $"YOU HAVE {Ab(shownOwned)}" : $"YOU NEED {required - owned:N0} MORE",
                          Res: res, Ok: ok, Lit: lit));
    }

    private void DrawRow(SpriteBatch b, in Row r, int x, int y, int w)
    {
        // A ROW THAT JUST CHANGED is washed once behind its own words — the same gold, on the same
        // pulse, as the card's level, so the two places showing one facility agree about the moment.
        if (r.Lit > 0f)
            _ui.Fill(b, new Rectangle(x - UiMetrics.Space(6), y - 2, w + UiMetrics.Space(12), r.H), Gold * (0.16f * r.Lit));
        switch (r.Kind)
        {
            case RowKind.Head:
            case RowKind.Name:
            case RowKind.Line:
                _ui.TextBig(b, r.A, x, y, Color.Lerp(r.Ink, Flare, r.Lit), r.Px);
                break;
            case RowKind.Rule:
                _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(4), w, 1), Dim);
                break;
            case RowKind.Pair:
                _ui.TextBig(b, r.A, x, y, Color.Lerp(Bone, Flare, r.Lit), UiTypography.Headline);
                _ui.TextRightBig(b, r.B, x + w, y, Met, UiTypography.Headline);
                _ui.TextCenterBig(b, "→", x + w / 2, y + 2, Slate, UiTypography.Headline);
                break;
            case RowKind.Locked:
            {
                var glyph = UiMetrics.Control(22);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, glyph, glyph), Bone);
                var tx = x + glyph + UiMetrics.Space(8);
                _ui.TextBig(b, _ui.ShortenBig(r.A, x + w - tx, UiTypography.Body), tx, y, Bone, UiTypography.Body);
                break;
            }
            case RowKind.Cost:
            {
                var glyph = UiMetrics.Control(26);
                ResGlyph(b, new Rectangle(x, y, glyph, glyph), r.Res, ResColor(r.Res));
                _ui.TextBig(b, r.A, x + glyph + UiMetrics.Space(10), y, Bone, UiTypography.Body);
                _ui.TextRightBig(b, r.B, x + w, y, Color.Lerp(r.Ok ? Bone : Ember, Flare, r.Lit), UiTypography.Body);
                _ui.TextRightBig(b, r.C, x + w, y + UiTypography.Pitch(UiTypography.Body),
                                 Color.Lerp(r.Ok ? Slate : Ember, Flare, r.Lit), UiTypography.Secondary);
                break;
            }
        }
    }

    private void DrawInspector(SpriteBatch b, Point hit, bool clicked, int wheel)
    {
        var panel = InspectorPanel;
        _ui.PanelQuiet(b, panel);
        // A locked selection can only arrive by state reset (the grid never selects a locked card);
        // fall back to the first open facility rather than posing a locked one.
        if (!Warren.IsUnlocked(_selected))
            _selected = Warren.AllFacilities.First(x => Warren.IsUnlocked(x.Kind)).Kind;
        var f = Warren.Facility(_selected);

        var x = UiKit.ContentLeft(panel);
        var w = UiKit.ContentRight(panel) - x;
        var cta = new Rectangle(x, panel.Bottom - UiMetrics.PanelPadding - UiMetrics.ButtonHeightPrimary, w, UiMetrics.ButtonHeightPrimary);
        var capped = Warren.IsAtLevelCap(_selected);
        var cost = f.UpgradeCost();
        var afford = GleamOwned >= cost.Gleam && DustOwned >= cost.Dust;
        // THE REFUSAL WRAPS, NEVER SHORTENS: it is the one line that says why the button is off, and at
        // 150 % the narrower inspector cut it to "THE WARREN CANNOT PASS YOUR DEE...". Its room is the
        // longer of the two sentences' wrapped height — one line at 100 and 125 %, two at 150 — and is
        // reserved whether or not a refusal is showing, so the rows above it never move between one
        // facility and the next.
        const string CappedRefusal = "THE WARREN CANNOT PASS YOUR DEEPEST WAVE.";
        const string PoorRefusal = "YOU CANNOT PAY FOR THIS LEVEL YET.";
        var refusalRows = Math.Clamp(Math.Max(_ui.WrapBig(CappedRefusal, w, UiTypography.Body).Count,
                                              _ui.WrapBig(PoorRefusal, w, UiTypography.Body).Count), 1, 2);
        var refusalY = cta.Y - UiMetrics.Space(8) - refusalRows * UiTypography.Pitch(UiTypography.Body);
        var floor = refusalY - UiMetrics.Space(8);
        var top = UiKit.TitleTop(panel);
        var room = floor - top;

        // Built at the full width first; only a list that overflows gives up the scrollbar's lane and
        // is built again for the narrower column it then has.
        BuildInspectorRows(f, w, capped, cost);
        var rowsW = w;
        if (_rows.Sum(r => r.H) > room)
        {
            rowsW = w - UiMetrics.ScrollbarWidth - UiMetrics.Gap;
            BuildInspectorRows(f, rowsW, capped, cost);
        }

        // The last first-row that still shows the list's end, so the wheel cannot run past it. The rows
        // from there to the end are the list's last page, which is what the bar's thumb stands for: it
        // reaches the track's foot exactly when the list is at its end.
        var total = _rows.Count;
        var maxFirst = total;
        for (int i = total - 1, h = 0; i >= 0; i--)
        {
            h += _rows[i].H;
            if (h > room) break;
            maxFirst = i;
        }
        var lastPage = total - maxFirst;
        _inspectorFirst = UiKit.Scrolled(_inspectorFirst, wheel, lastPage, total);

        var y = top;
        for (var i = _inspectorFirst; i < total; i++)
        {
            var r = _rows[i];
            if (y + r.H > floor) break;
            // A heading is never the last thing on the page with nothing under it: it waits for the
            // scroll that brings its first row along.
            if (r.Kind == RowKind.Head && i + 1 < total && y + r.H + _rows[i + 1].H > floor) break;
            DrawRow(b, in r, x, y, rowsW);
            y += r.H;
        }
        if (rowsW < w)
            _ui.ScrollBar(b, new Rectangle(x + w - UiMetrics.ScrollbarWidth, top, UiMetrics.ScrollbarWidth, room),
                          _inspectorFirst, lastPage, total);

        // ASKING FOR WHAT YOU CANNOT HAVE IS ANSWERED, not ignored. UiKit.Button never reports a click
        // on a disabled control (correctly — it must not fire), so the refusal is caught here: the one
        // line that already says why pulses once, and the host plays the refusal sound.
        var canUpgrade = afford && !capped;
        if (!canUpgrade && UiKit.ClickedIn(cta, hit, clicked)) Refuse(_selected);
        var refusalLit = RefusalLit(_selected);

        if (!afford || capped)
        {
            var ry = refusalY;
            if (refusalLit > 0f)
                _ui.Fill(b, new Rectangle(x - UiMetrics.Space(6), refusalY - 2,
                                          w + UiMetrics.Space(12), refusalRows * UiTypography.Pitch(UiTypography.Body)),
                         Ember * (0.26f * refusalLit));
            foreach (var line in _ui.WrapBig(capped ? CappedRefusal : PoorRefusal, w, UiTypography.Body).Take(refusalRows))
            {
                _ui.TextBig(b, line, x, ry, Color.Lerp(Ember, Flare, 0.55f * refusalLit), UiTypography.Body);
                ry += UiTypography.Pitch(UiTypography.Body);
            }
        }

        // The label IS the reason when capped — a button reading DEPTH LOCKED told the player a state,
        // not a next step.
        if (_ui.Button(b, cta, capped ? $"REACH WAVE {Warren.DepthForNextLevel(_selected)}" : "UPGRADE",
                       hit, clicked, enabled: canUpgrade, ButtonStyle.Primary))
            Ask(f);   // snapshot what is true BEFORE the spend, then let the host apply it
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
        var px = UiTypography.Body;
        foreach (var line in _ui.WrapBig(text, width, px)) { _ui.TextBig(b, line, x, y, c, px); y += UiTypography.Pitch(px); }
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { SummaryStrip, GridPanel, InspectorPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav WARREN  facilities  sel {_selected}", GridPanel.X, 118, Gold, UiTypography.Secondary);
    }
}
