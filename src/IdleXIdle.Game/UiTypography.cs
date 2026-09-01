namespace IdleXIdle.Game;

/// <summary>
/// THE HOUSE STANDARD for type and spacing. Every drawn size and every panel margin in the game comes
/// from here, so no two panels can differ by an accident nobody meant.
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
/// Values are LOGICAL pixel heights in the 1920×1080 authoring space, which is the space every menu
/// screen and the arena HUD are drawn in (<c>UiKit.Scale == 1</c>).
/// </para>
/// </remarks>
public static class UiTypography
{
    // ── THE LADDER ────────────────────────────────────────────────────────────────────────────────
    //
    // Eight rungs, each with one job. Read it top to bottom as "how loud is this line?".
    //
    //   36  ScreenTitle      the screen's own name, in the ceremony face
    //   30  PrimaryValue     a headline number
    //   26  PanelTitle       the title of a panel
    //   24  Headline         the biggest thing INSIDE a panel — an item's name, a rank
    //   21  NavigationLabel  a thing you click: a nav tile, a button label
    //   19  Body             prose, list rows, and the default an unsized call draws at
    //   16  Secondary        captions, column heads, gold sub-headings
    //   14  Caption          chips, badges, the tightest columns — never a sentence
    //
    // The weight follows the size on its own (see SmoothFont.WeightFor): ≤19 Regular, 21–26 SemiBold,
    // ≥30 Bold. The rungs were chosen so that boundary lands where the meaning changes.
    //
    // The combat callouts are a FAMILY BESIDE the ladder, not rungs on it — see below.

    /// <summary>
    /// The screen's own name in the top strip — HUNT, THE FORGE, TRAITS. Always the ceremony face
    /// (<see cref="TextFace.Display"/>), always centred at the top, never more than one per screen.
    /// </summary>
    public const int ScreenTitle = 36;

    /// <summary>A banner over the arena — the region's name, BOSS INCOMING. The screen title's rung.</summary>
    public const int RegionTitle = ScreenTitle;

    /// <summary>A headline number: GEAR POWER's figure, MASTERY POINTS' figure, the idle rate.</summary>
    public const int PrimaryValue = 30;

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
    // the arena's real ones; the arena reads them from here now.

    /// <summary>An ordinary hit's number, floating over the creature it landed on.</summary>
    public const int DamageNormal = 34;

    /// <summary>A skill's hit — louder than a basic attack, so a cast reads as a cast.</summary>
    public const int DamageSkill = 40;

    /// <summary>A CRITICAL. The loudest text in the game, and the only thing allowed to be.</summary>
    public const int DamageCritical = 46;

    /// <summary>
    /// THE TITLE OF A PANEL — LOADOUT, TRAINING, YOUR MATERIALS, VOWS.
    /// </summary>
    /// <remarks>
    /// This was called <c>SectionTitle</c> while the name <c>PanelTitle</c> sat on the rung BELOW it, so
    /// the constant named after a panel's title was never the one a panel's title used. Renamed rather
    /// than documented around: a name that lies is how the next drift starts. The value did not change,
    /// so nothing moved a pixel when the rename landed.
    /// </remarks>
    public const int PanelTitle = 26;

    /// <summary>The arena's wave line — "WAVE 12 — RECOVERING". A panel title's rung, in the ceremony face.</summary>
    public const int StageLabel = PanelTitle;

    /// <summary>
    /// The biggest thing INSIDE a panel: an item's name, a rank, a chest's tier, a champion's name.
    /// One rung under the panel's own title, so the panel still wins its own header.
    /// </summary>
    public const int Headline = 24;

    /// <summary>A toast's or a tour card's title. The in-panel headline rung.</summary>
    public const int OverlayTitle = Headline;

    /// <summary>
    /// A THING YOU CLICK — a nav-rail tile, a button's label, a dropdown's value.
    /// </summary>
    /// <remarks>
    /// Buttons used to size their label from their own HEIGHT (<c>Clamp(h / 2, 14, 22)</c>), so two
    /// buttons side by side with different heights spoke at different sizes, and a 22px label sat one
    /// pixel under a rung nothing else in the game used. One size for every control; it shrinks only when
    /// the label genuinely does not fit, and never below <see cref="Caption"/>.
    /// </remarks>
    public const int NavigationLabel = 21;

    /// <inheritdoc cref="NavigationLabel"/>
    public const int ButtonText = NavigationLabel;

    /// <summary>
    /// Prose, list rows, an explanation. The paragraph size.
    /// </summary>
    /// <remarks>
    /// The UX guide (<c>assets/art/idlexidle_ux_screen_guide_standard.md</c> §9) sets 18 px as the floor
    /// for body text; this clears it. Three separate values — Label 18, Body 19, OverlayBody 18 — used to
    /// do this one job, which is a distinction no reader can see and every author has to guess at.
    /// </remarks>
    public const int Body = 19;

    /// <summary>
    /// The size an UNSIZED <c>_ui.Text</c> draws at. The default for a label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It used to be 32 — larger than every token here except ScreenTitle.</b> That is not a
    /// stylistic quibble; it inverted the hierarchy of half the game. 146 call sites use the unsized
    /// proxies, and they are almost all labels and captions, so the biggest text on a screen was
    /// routinely its least important line: on GEAR an affix row outsized the item's own name, on ROSTER
    /// the word "READY" was twice the size of the champion it belonged to, on HELP every body row
    /// outsized the panel title above it.
    /// </para>
    /// <para>
    /// Changed in ONE place rather than at 146 call sites, deliberately. A sweep of that size cannot be
    /// verified by reading it — this can be verified by looking at every screen, which is what was done.
    /// A site that genuinely wants to shout can still say so with the sized <c>*Big</c> proxies.
    /// </para>
    /// <para>
    /// It is now simply <see cref="Body"/>. An unsized call is a paragraph until it says otherwise; it
    /// used to be a paragraph MINUS ONE PIXEL, which is a difference the eye cannot see and the code
    /// cannot justify.
    /// </para>
    /// </remarks>
    public const int Label = Body;

    /// <summary>A toast's or a tour card's body. The paragraph rung.</summary>
    public const int OverlayBody = Body;

    /// <summary>
    /// A caption, a column head, a secondary value, a gold sub-heading inside a panel (SET BONUSES,
    /// WHAT IT DOES, COST TO UPGRADE).
    /// </summary>
    public const int Secondary = 16;

    /// <summary>
    /// A sub-heading inside a panel. Deliberately the SAME size as the caption it heads.
    /// </summary>
    /// <remarks>
    /// The screens that do this well — GEAR's SET BONUSES, WARREN's OUTPUT, TRAITS' WHAT IT DOES — carry
    /// the heading on COLOUR and CAPS, not on size. Giving it a rung of its own would have added a fourth
    /// value between 16 and 19 doing nothing the gold ink was not already doing.
    /// </remarks>
    public const int SectionLabel = Secondary;

    /// <summary>
    /// The floor: chips, badges, a slot label under an icon, the arena's tightest columns.
    /// </summary>
    /// <remarks>
    /// BELOW the UX guide's 18 px body floor, on purpose and only for text that is a TAG. This rung must
    /// never carry a sentence — if a sentence does not fit at <see cref="Secondary"/>, the column is too
    /// narrow and the fix is the column. It exists because the arena had drifted to 12 and 13 px in its
    /// skill rail and its chest filter; naming the floor at 14 raises those and stops the next one.
    /// </remarks>
    public const int Caption = 14;

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
    // The numbers are TrainingScreen's, unchanged — it is the screen the 2026-08-28 captures show behaving,
    // and picking a screen that already works beats inventing a fashion.

    /// <summary>
    /// Where a panel's TITLE sits: this far below the panel's top edge.
    /// </summary>
    /// <remarks>
    /// TrainingScreen (HUNTER, TRAINING), WarrenScreen (WARREN OVERVIEW, the facility detail) and
    /// ForgeScreen (WHAT TO DO WITH IT, YOUR MATERIALS) all already used 22, which is the largest
    /// agreement in the codebase and reads correctly against the frame's top rail. Everything else was
    /// pulled to it: the FORGE's bag (40), the ROSTER's grid (44), the BUILD screen's three columns
    /// (44 / 66 / 66) and the mastery overview's passives (62).
    /// </remarks>
    public const int PanelTitleTop = 22;

    /// <summary>
    /// How much lower everything sits on a panel wearing the SQUARE frame, whose crest is 51 source px
    /// deep against the medium frame's 20 and the vertical frame's 21.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a taste; the art measures this way. The square frame hangs a diamond finial from the middle
    /// of its top rail and a second one from the middle of each side; measured off the ROSTER capture
    /// they reach 40 px down and 64 px in, against roughly 20 for the other two frames. 22 + 28 = 50
    /// puts a title ten pixels clear of the finial, and 40 + 28 = 68 puts a column four pixels clear of
    /// the side diamond.
    /// </para>
    /// <para>
    /// Both screens that own a square panel had already found this the hard way and landed on different
    /// numbers — TrainingScreen's PROGRESS wrote +40 and inset 60, RosterScreen's detail column wrote +74
    /// and inset 74 with a comment beginning "DERIVED FROM THE PANEL'S INTERIOR, not a hand-picked +44".
    /// Applied by <see cref="UiKit.FrameDrop"/> now, so no screen has to know which frame its own
    /// rectangle happens to select — which is a thing a screen cannot know, since the frame is chosen
    /// by aspect ratio and changes when the panel is resized.
    /// </para>
    /// </remarks>
    public const int SquareFrameDrop = 28;

    /// <summary>
    /// Where a panel's one-line CAPTION sits, under its title. Exactly one <see cref="PanelTitle"/> line
    /// below <see cref="PanelTitleTop"/>. TrainingScreen's TRAINING caption is the precedent.
    /// </summary>
    public const int PanelCaptionTop = 56;

    /// <summary>
    /// Where a panel's first content row starts when it has BOTH a title and a caption.
    /// TrainingScreen's TRAINING rows are the precedent.
    /// </summary>
    public const int PanelBodyTop = 92;

    /// <summary>
    /// Where a panel's first content row starts when it has a title and NO caption — one title line plus
    /// the same breathing room a caption would have had above it.
    /// </summary>
    public const int PanelBodyTopBare = 62;

    /// <summary>
    /// A MODAL's title — a panel that takes the screen and can be closed. It sits lower than a plain
    /// panel's title because it shares its row with the close icon, which <see cref="UiKit.CloseRect"/>
    /// places clear of the frame's corner ornament.
    /// </summary>
    /// <remarks>
    /// This is the one place two title heights are correct, and it is caused by the icon rather than by
    /// taste. The EXPEDITION LOG (44) and the CONTROLS sheet (46) already agreed; the settings panel and
    /// the vault's popovers were pulled to them.
    /// </remarks>
    public const int ModalTitleTop = 44;

    /// <summary>
    /// The left and right inset for content inside a panel wide enough to afford it (roughly 480 px and
    /// up).
    /// </summary>
    /// <remarks>
    /// It is <see cref="UiKit.PanelCorner"/>: the frame's corner ornament reaches exactly this far in, so
    /// content at this inset can never sit on the filigree even level with a corner. TrainingScreen's
    /// TRAINING rows and every RosterScreen column already used it.
    /// </remarks>
    public const int PanelPadX = 40;

    /// <summary>
    /// The left and right inset for a NARROW plate — the arena's 326 px rail, a detail card — where 40
    /// each side would eat a quarter of the column.
    /// </summary>
    /// <remarks>
    /// The frame's ornament is only about 18 px deep along a straight EDGE; the 40 of
    /// <see cref="PanelPadX"/> is what the CORNER needs. A narrow plate has no room to honour the corner
    /// and nothing sitting beside one, so it honours the edge. The arena's rail (RailInset) and
    /// TrainingScreen's hunter card and the arena's rail already used 24, and WARREN, MAP and TRAITS
    /// already used 28 — 28 across 20 call sites is the single largest agreement on a left margin in
    /// the codebase, and it clears the vertical frame's 24 px side rail where 24 sits flush on it.
    /// </remarks>
    public const int PanelPadNarrow = 28;

    /// <summary>
    /// The width at which a panel is wide enough to afford <see cref="PanelPadX"/> rather than
    /// <see cref="PanelPadNarrow"/>.
    /// </summary>
    /// <remarks>
    /// Chosen off the roster of panels this game actually has: the arena's 326 px rail and the 366–500 px
    /// detail columns (GEAR's item detail, MAP's region card, WARREN's facility card, TRAITS' trait card,
    /// the FORGE's bag and item cards) stay narrow, while the 514 px and wider content panels take the
    /// house margin. Every one of the narrow group had already written 24–30 by hand and every one of the
    /// wide group 36–40, so this threshold is where the codebase had already drawn the line.
    /// </remarks>
    public const int WidePanelFrom = 512;

    /// <summary>How far a panel's last row clears its bottom frame.</summary>
    public const int PanelPadBottom = 32;

    /// <summary>
    /// The room a button's label leaves at each end, on top of the button art's own end ornament.
    /// </summary>
    /// <remarks>
    /// <c>UiKit.Button</c> reserved a flat 12 px per side, which is less than the ornament the 256×96
    /// button art draws at its ends — so long labels printed straight onto the scrollwork ("MERGE THREES
    /// INTO BETTER", "SALVAGE ALL THE JUNK"). The reserve is now this OR the ornament, whichever is
    /// larger, so the label clears the art at every button height.
    /// </remarks>
    public const int ButtonPadX = 16;

    /// <summary>The ink inset inside a chip — a bordered plate with one short word in it.</summary>
    public const int ChipPadX = 8;

    /// <summary>How far a chip's text sits below the chip's top edge.</summary>
    public const int ChipPadY = 4;

    /// <summary>The gap a hairline rule leaves above and below itself.</summary>
    public const int HairlineGap = 12;
}
