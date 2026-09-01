namespace IdleXIdle.Game;

/// <summary>
/// THE HOUSE STANDARD for type and spacing. Every drawn size and every panel margin in the game comes
/// from here, so no two panels can differ by an accident nobody meant — and since 2026-09-01 every
/// rung FOLLOWS THE UI SCALE PROFILE through <see cref="UiMetrics"/>, so a screen that reads
/// <see cref="Body"/> is already right at 100, 125 and 150 %.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY IT EXISTS.</b> Playtest 2026-08-28: "there is an inconsistency between the text sizes inside
/// the game — some texts are smaller, some bigger, and some panels' and texts' placements differ."
/// Both halves were true and both were measurable. The sizes had drifted to fourteen distinct values
/// (12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 26, 30, 36, 40) with three of them — 18, 19 and 20 —
/// doing the same job one pixel apart. And a panel's title sat anywhere between 12 and 70 px below its
/// own top frame depending on which screen you were looking at: the FORGE alone drew its four titles at
/// +40, +32, +22 and +22, so the four columns of one screen had four different headers.
/// </para>
/// <para>
/// <b>THE RULE.</b> There is ONE ladder of sizes and ONE set of panel margins. A deliberate deviation is
/// allowed — the arena's damage numbers are meant to shout — but it is a NAMED constant here, never a
/// literal at a call site, so it reads as a decision rather than as drift. <c>tools/check_ui_type.py</c>
/// fails the build on a bare numeric size passed to a text call.
/// </para>
/// <para>
/// <b>THE PROFILE.</b> The ladder's numbers below are the 100 % values, in LOGICAL pixels of the
/// 1920×1080 page. Each public rung is a PROPERTY that returns the profile's version of its base
/// (<see cref="UiMetrics.Text"/>), and the panel grid is DERIVED from the rungs it stacks, so a bigger
/// title pushes its caption down rather than overprinting it. A rung was a <c>const</c> until the UI
/// polish pass; the ~930 call sites that read one did not change, which is the whole point of a
/// ladder. (A <c>const</c> cannot follow a setting; the handful of <c>const int X = UiTypography.Y</c>
/// and default-parameter sites became expressions on the same day.)
/// </para>
/// </remarks>
public static class UiTypography
{
    // ── THE LADDER ────────────────────────────────────────────────────────────────────────────────
    //
    // Eight rungs, each with one job. Read it top to bottom as "how loud is this line?".
    //
    //   38  ScreenTitle      the screen's own name, in the ceremony face
    //   32  PrimaryValue     a headline number
    //   28  PanelTitle       the title of a panel
    //   26  Headline         the biggest thing INSIDE a panel — an item's name, a rank
    //   24  NavigationLabel  a thing you click: a nav tile, a button label
    //   22  Body             prose, list rows, and the default an unsized call draws at
    //   19  Secondary        captions, column heads, gold sub-headings
    //   16  Caption          chips, badges, the tightest columns — never a sentence
    //
    // THE LADDER MOVED UP on 2026-09-01 (UX V2 P0.6, typography audit §6). The game is presented at
    // 1280×720 on the smaller half of its players' monitors, and there Body 19 landed at 12.7 physical
    // px and Caption 14 at 9.3 — under the 12 px floor the brief sets for anything a player must read.
    // Every rung is one step louder; Body 22 is 14.7 px at 720p. Rows follow through Pitch(), so the
    // move cost no re-auditing of row heights — that is what P0.4 was for.
    //
    // The weight follows the size on its own (see SmoothFont.WeightFor): under NavigationLabel Regular,
    // NavigationLabel..Headline SemiBold, PrimaryValue and up Bold. SmoothFont reads the thresholds FROM
    // these two rungs, so the boundary moves with the ladder — and with the profile — and always lands
    // where the meaning changes.
    //
    // The combat callouts are a FAMILY BESIDE the ladder, not rungs on it — see below.

    /// <summary>
    /// The screen's own name in the top strip — HUNT, THE FORGE, TRAITS. Always the ceremony face
    /// (<see cref="TextFace.Display"/>), always centred at the top, never more than one per screen.
    /// </summary>
    public static int ScreenTitle => UiMetrics.Text(38);

    /// <summary>A banner over the arena — the region's name, BOSS INCOMING. The screen title's rung.</summary>
    public static int RegionTitle => ScreenTitle;

    /// <summary>A headline number: GEAR POWER's figure, MASTERY POINTS' figure, the idle rate.</summary>
    public static int PrimaryValue => UiMetrics.Text(32);

    // ── THE COMBAT CALLOUTS — a family, not rungs ─────────────────────────────────────────────────
    //
    // The three sizes that float over the arena floor and land on nothing else in the game. They are
    // allowed to be louder than a screen title because they are read at a glance in the middle of a
    // fight, they never sit beside prose, and they have to be told apart from each other at speed —
    // which is the whole reason there are three of them rather than one.
    //
    // THEY LIVED IN HuntScreen AS A SECOND LADDER (DamagePx 34 / SkillHitPx 40 / CritPx 46)
    // while the DamageNormal and DamageCritical named here were 30 and 40 and used by NOTHING. Two
    // ladders, one of them dead, is exactly the shape the drift takes: the file that draws the thing
    // quietly grows its own copy, and the shared one stops describing the game. The values below are
    // the arena's real ones; the arena reads them from here now. They follow the profile too: a player
    // who asked for bigger text asked for bigger damage numbers.

    /// <summary>An ordinary hit's number, floating over the creature it landed on.</summary>
    public static int DamageNormal => UiMetrics.Text(34);

    /// <summary>A skill's hit — louder than a basic attack, so a cast reads as a cast.</summary>
    public static int DamageSkill => UiMetrics.Text(40);

    /// <summary>A CRITICAL. The loudest text in the game, and the only thing allowed to be.</summary>
    public static int DamageCritical => UiMetrics.Text(46);

    /// <summary>
    /// THE TITLE OF A PANEL — LOADOUT, TRAINING, YOUR MATERIALS, VOWS.
    /// </summary>
    /// <remarks>
    /// This was called <c>SectionTitle</c> while the name <c>PanelTitle</c> sat on the rung BELOW it, so
    /// the constant named after a panel's title was never the one a panel's title used. Renamed rather
    /// than documented around: a name that lies is how the next drift starts. The value did not change,
    /// so nothing moved a pixel when the rename landed.
    /// </remarks>
    public static int PanelTitle => UiMetrics.Text(28);

    /// <summary>The arena's wave line — "WAVE 12 — RECOVERING". A panel title's rung, in the ceremony face.</summary>
    public static int StageLabel => PanelTitle;

    /// <summary>
    /// The biggest thing INSIDE a panel: an item's name, a rank, a chest's tier, a hunter's name.
    /// One rung under the panel's own title, so the panel still wins its own header.
    /// </summary>
    public static int Headline => UiMetrics.Text(26);

    /// <summary>A toast's or a tour card's title. The in-panel headline rung.</summary>
    public static int OverlayTitle => Headline;

    /// <summary>
    /// A THING YOU CLICK — a nav-rail tile, a button's label, a dropdown's value.
    /// </summary>
    /// <remarks>
    /// Buttons used to size their label from their own HEIGHT (<c>Clamp(h / 2, 14, 22)</c>), so two
    /// buttons side by side with different heights spoke at different sizes, and a 22px label sat one
    /// pixel under a rung nothing else in the game used. One size for every control; it shrinks only when
    /// the label genuinely does not fit, and never below <see cref="Caption"/>.
    /// </remarks>
    public static int NavigationLabel => UiMetrics.Text(24);

    /// <inheritdoc cref="NavigationLabel"/>
    public static int ButtonText => NavigationLabel;

    /// <summary>
    /// Prose, list rows, an explanation. The paragraph size.
    /// </summary>
    /// <remarks>
    /// The UX guide (<c>assets/art/idlexidle_ux_screen_guide_standard.md</c> §9) sets 18 px as the floor
    /// for body text; this clears it. Three separate values — Label 18, Body 19, OverlayBody 18 — used to
    /// do this one job, which is a distinction no reader can see and every author has to guess at.
    /// </remarks>
    public static int Body => UiMetrics.Text(22);

    /// <summary>
    /// The size an UNSIZED <c>_ui.Text</c> draws at. The default for a label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It used to be 32 — larger than every token here except ScreenTitle.</b> That is not a
    /// stylistic quibble; it inverted the hierarchy of half the game. 146 call sites use the unsized
    /// proxies, and they are almost all labels and captions, so the biggest text on a screen was
    /// routinely its least important line: on GEAR an affix row outsized the item's own name, on ROSTER
    /// the word "READY" was twice the size of the hunter it belonged to, on HELP every body row
    /// outsized the panel title above it.
    /// </para>
    /// <para>
    /// It is now simply <see cref="Body"/>. An unsized call is a paragraph until it says otherwise.
    /// </para>
    /// </remarks>
    public static int Label => Body;

    /// <summary>A toast's or a tour card's body. The paragraph rung.</summary>
    public static int OverlayBody => Body;

    /// <summary>
    /// A caption, a column head, a secondary value, a gold sub-heading inside a panel (SET BONUSES,
    /// WHAT IT DOES, COST TO UPGRADE).
    /// </summary>
    public static int Secondary => UiMetrics.Text(19);

    /// <summary>
    /// A sub-heading inside a panel. Deliberately the SAME size as the caption it heads.
    /// </summary>
    /// <remarks>
    /// The screens that do this well — GEAR's SET BONUSES, WARREN's OUTPUT, TRAITS' WHAT IT DOES — carry
    /// the heading on COLOUR and CAPS, not on size. Giving it a rung of its own would have added a fourth
    /// value between 16 and 19 doing nothing the gold ink was not already doing.
    /// </remarks>
    public static int SectionLabel => Secondary;

    /// <summary>
    /// The floor: chips, badges, a slot label under an icon, the arena's tightest columns.
    /// </summary>
    /// <remarks>
    /// BELOW the UX guide's 18 px body floor, on purpose and only for text that is a TAG. This rung must
    /// never carry a sentence — if a sentence does not fit at <see cref="Secondary"/>, the column is too
    /// narrow and the fix is the column. It exists because the arena had drifted to 12 and 13 px in its
    /// skill rail and its chest filter; naming the floor at 14 raises those and stops the next one.
    /// </remarks>
    public static int Caption => UiMetrics.Text(16);

    /// <summary>
    /// THE LINE PITCH for text drawn at <paramref name="rung"/>: the distance from one baseline to the
    /// next in a list, a paragraph, a table. 13/10 of the rung — Body 22 → 28, Secondary 19 → 24.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 2026-09-01 UX audit counted ~110 hand-set pitches (<c>y += 26</c>, <c>lineH = 28</c>,
    /// <c>px * 3 / 2</c>) across thirteen files, for the SAME 19 px text at 26, 28, 30 and 32 — four row
    /// heights for one voice, chosen by whoever wrote the screen. Worse, none of them knew about the
    /// ladder: a rung could grow and every row it sat in would silently overlap the next. This ties the
    /// pitch to the rung, so the ladder can move (and UI SCALE does scale it) and the rows follow.
    /// </para>
    /// <para>
    /// A pitch is NOT a gap. The space between a caption and the block under it, or between two
    /// sections, is a layout offset and stays a named literal on the screen; this is only the step from
    /// one line of the same text to the next. <c>tools/check_ui_type.py</c> refuses a bare number in a
    /// <c>lineH</c> / <c>rowH</c> / <c>pitch</c> slot for the same reason it refuses one in a size slot.
    /// </para>
    /// </remarks>
    public static int Pitch(int rung) => rung * 13 / 10;

    // ── THE PANEL GRID ────────────────────────────────────────────────────────────────────────────
    //
    // Every panel in the game is the same nine-sliced frame (UiKit.Panel / PanelQuiet — the same art,
    // one of them tinted). So every panel can lay its header out the same way, and the reason none of
    // them did is that each screen measured the frame by eye and wrote its own number.
    //
    //      panel.Y ┬─────────────────────────────  the top of the frame
    //              │  22   PanelTitleTop      TRAINING
    //              │  56   PanelCaptionTop    every row shows the real number…
    //              │  92   PanelBodyTop       ▸ first row
    //              …
    //              │       PanelPadBottom     the last row clears the bottom frame by this
    //     panel.Bottom ────────────────────────
    //
    // The numbers are TrainingScreen's at 100 %, unchanged — it is the screen the 2026-08-28 captures
    // show behaving. They are DERIVED, not written: the caption sits one title line under the title
    // and the first row one caption line under that, so at 150 % (title 42, caption 29) the stack is
    // 22 → 76 → 128 and nothing overprints. What the frame's ART dictates (the corner's reach, the
    // square crest's drop) stays a constant: it does not grow with the type.

    /// <summary>
    /// Where a panel's TITLE sits: this far below the panel's top edge.
    /// </summary>
    /// <remarks>
    /// TrainingScreen (HUNTER, TRAINING), WarrenScreen (WARREN OVERVIEW, the facility detail) and
    /// ForgeScreen (WHAT TO DO WITH IT, YOUR MATERIALS) all already used 22, which is the largest
    /// agreement in the codebase and reads correctly against the frame's top rail.
    /// </remarks>
    public static int PanelTitleTop => UiMetrics.Space(22);

    /// <summary>
    /// How much lower everything sits on a panel wearing the SQUARE frame, whose crest is 51 source px
    /// deep against the medium frame's 20 and the vertical frame's 21. Art geometry — unscaled.
    /// </summary>
    public const int SquareFrameDrop = 28;

    /// <summary>
    /// Where a panel's one-line CAPTION sits, under its title: one <see cref="PanelTitle"/> line below
    /// <see cref="PanelTitleTop"/>. 56 at 100 % — TrainingScreen's TRAINING caption is the precedent.
    /// </summary>
    public static int PanelCaptionTop => PanelTitleTop + Pitch(PanelTitle) - 2;

    /// <summary>
    /// Where a panel's first content row starts when it has BOTH a title and a caption: one caption
    /// line plus a breath under <see cref="PanelCaptionTop"/>. 92 at 100 %.
    /// </summary>
    public static int PanelBodyTop => PanelCaptionTop + Pitch(Secondary) + UiMetrics.Space(12);

    /// <summary>
    /// Where a panel's first content row starts when it has a title and NO caption — one title line plus
    /// the same breathing room a caption would have had above it. 62 at 100 %.
    /// </summary>
    public static int PanelBodyTopBare => PanelTitleTop + Pitch(PanelTitle) + UiMetrics.Space(4);

    /// <summary>
    /// A MODAL's title — a panel that takes the screen and can be closed. It sits lower than a plain
    /// panel's title because it shares its row with the close icon, which <see cref="UiKit.CloseRect"/>
    /// places clear of the frame's corner ornament.
    /// </summary>
    public static int ModalTitleTop => UiMetrics.Space(44);

    /// <summary>
    /// The left and right inset for content inside a panel wide enough to afford it (roughly 480 px and
    /// up). It is <see cref="UiKit.PanelCorner"/>: the frame's corner ornament reaches exactly this far
    /// in, so content at this inset can never sit on the filigree. Art geometry — unscaled.
    /// </summary>
    public const int PanelPadX = 40;

    /// <summary>
    /// The left and right inset for a NARROW plate — the arena's rail, a detail card — where 40 each
    /// side would eat a quarter of the column. Clears the vertical frame's 24 px side rail. Art
    /// geometry — unscaled.
    /// </summary>
    public const int PanelPadNarrow = 28;

    /// <summary>The width at which a panel is wide enough to afford <see cref="PanelPadX"/> rather than <see cref="PanelPadNarrow"/>.</summary>
    public const int WidePanelFrom = 512;

    /// <summary>How far a panel's last row clears its bottom frame.</summary>
    public static int PanelPadBottom => UiMetrics.Space(32);

    /// <summary>
    /// The room a button's label leaves at each end, on top of the button art's own end ornament.
    /// </summary>
    public static int ButtonPadX => UiMetrics.Space(16);

    /// <summary>The ink inset inside a chip — a bordered plate with one short word in it.</summary>
    public static int ChipPadX => UiMetrics.Space(8);

    /// <summary>How far a chip's text sits below the chip's top edge.</summary>
    public static int ChipPadY => UiMetrics.Space(4);

    /// <summary>The gap a hairline rule leaves above and below itself.</summary>
    public static int HairlineGap => UiMetrics.Space(12);
}
